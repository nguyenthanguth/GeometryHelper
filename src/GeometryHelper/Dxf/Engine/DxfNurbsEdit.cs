using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// The changes a <see cref="DxfSpline"/> makes to its NURBS data, worked on the control points in homogeneous form (x w,
    /// y w, z w, w): knots put in by Boehm's rule, the degree raised, the spline cut and run on past its ends, fitted by least
    /// squares, and made to pass a point in a direction; after Piegl and Tiller, The NURBS Book (A2.1, A2.3, A5.1, A5.9, A9.7).
    /// </summary>
    /// <remarks>
    /// The knots are those of a clamped spline, the first and last degree and one times each, as AutoCAD writes a spline that
    /// is not periodic. Every change but the degree raised and the cut keeps the curve where it was; those two keep it exactly.
    /// </remarks>
    internal static class DxfNurbsEdit
    {
        // How far the seam of a periodic form may be off: AutoCAD's own point tolerance, 1E-10, and SeamShare of the spline's
        // size for the rounding of its unclamping. AutoCAD 2021's periodic splines it saved unclamp to 1.3E-15 of their size;
        // of a 2059 of size 61.5 it read a seam off by 4.1E-11 as periodic and one off by 4.1E-10 as not (its 17-w).
        private const double SeamGap = 1E-10;

        // The share of a spline's size its unclamped seam may be off by rounding alone, beyond SeamGap.
        private const double SeamShare = 1E-14;

        /// <summary>The control points in homogeneous form: each point times its weight, then the weight.</summary>
        internal static List<double[]> Homogeneous(IReadOnlyList<GeoPoint3> points, IReadOnlyList<double> weights)
        {
            var pw = new List<double[]>(points.Count);

            for (int i = 0; i < points.Count; i++)
            {
                double w = weights == null || weights.Count == 0 ? 1.0 : weights[i];
                pw.Add(new[] { points[i].X * w, points[i].Y * w, points[i].Z * w, w });
            }

            return pw;
        }

        /// <summary>The points and weights of control points in homogeneous form.</summary>
        internal static void Split(List<double[]> pw, out List<GeoPoint3> points, out List<double> weights)
        {
            points = new List<GeoPoint3>(pw.Count);
            weights = new List<double>(pw.Count);

            foreach (double[] q in pw)
            {
                points.Add(new GeoPoint3(q[0] / q[3], q[1] / q[3], q[2] / q[3]));
                weights.Add(q[3]);
            }
        }

        /// <summary>The span k of a parameter, u[k] &lt;= t &lt; u[k + 1] and of some length; the last such for the end.</summary>
        internal static int Span(int p, IReadOnlyList<double> u, int count, double t)
        {
            int n = count - 1;

            if (t >= u[n + 1])
            {
                int k = n;

                while (k > p && !(u[k + 1] > u[k]))
                {
                    k--;
                }

                return k;
            }

            if (t <= u[p])
            {
                int k = p;

                while (k < n && !(u[k + 1] > u[k]))
                {
                    k++;
                }

                return k;
            }

            int low = p;
            int high = n + 1;
            int mid = (low + high) / 2;

            while (t < u[mid] || t >= u[mid + 1])
            {
                if (t < u[mid])
                {
                    high = mid;
                }
                else
                {
                    low = mid;
                }

                mid = (low + high) / 2;
            }

            return mid;
        }

        /// <summary>How many times a value stands among the knots.</summary>
        internal static int Multiplicity(IReadOnlyList<double> u, double t)
        {
            int s = 0;

            foreach (double knot in u)
            {
                s += knot == t ? 1 : 0;
            }

            return s;
        }

        /// <summary>
        /// Makes the knots of a spline clamped over its domain, the curve kept: the knot at each end of the domain (that of index
        /// degree and that of index count of the points) raised to the degree, and the knots and points outside it dropped. A
        /// spline clamped already is left as it is.
        /// </summary>
        internal static void Clamp(int p, List<double> u, List<double[]> pw)
        {
            ClampStart(p, u, pw);
            Reverse(u, pw);
            ClampStart(p, u, pw);
            Reverse(u, pw);
        }

        private static void ClampStart(int p, List<double> u, List<double[]> pw)
        {
            double a = u[p];
            int times = p - Multiplicity(u, a);

            if (times > 0)
            {
                InsertKnot(p, u, pw, a, times);
            }

            int r = u.IndexOf(a);

            if (r <= 1)
            {
                for (int i = 0; i < r; i++)
                {
                    u[i] = a;
                }

                return;
            }

            // The run of a is p long from r: the curve starts at the point r - 1, and the knots before the run go.
            u.RemoveRange(0, r - 1);
            u[0] = a;
            pw.RemoveRange(0, r - 1);
        }

        /// <summary>Turns knots and points the other way, each knot made its negative, which is exact.</summary>
        private static void Reverse(List<double> u, List<double[]> pw)
        {
            u.Reverse();

            for (int i = 0; i < u.Count; i++)
            {
                u[i] = -u[i];
            }

            pw.Reverse();
        }

        /// <summary>Puts a knot in a number of times by Boehm's rule (A5.1), the curve kept: one control point more each time.</summary>
        internal static void InsertKnot(int p, List<double> u, List<double[]> pw, double t, int times)
        {
            for (int r = 0; r < times; r++)
            {
                int k = Span(p, u, pw.Count, t);
                var inserted = new List<double[]>(pw.Count + 1);

                for (int i = 0; i <= pw.Count; i++)
                {
                    if (i <= k - p)
                    {
                        inserted.Add(pw[i]);
                    }
                    else if (i > k)
                    {
                        inserted.Add(pw[i - 1]);
                    }
                    else
                    {
                        double a = (t - u[i]) / (u[i + p] - u[i]);
                        var q = new double[4];

                        for (int c = 0; c < 4; c++)
                        {
                            q[c] = (1.0 - a) * pw[i - 1][c] + a * pw[i][c];
                        }

                        inserted.Add(q);
                    }
                }

                u.Insert(k + 1, t);
                pw.Clear();
                pw.AddRange(inserted);
            }
        }

        /// <summary>
        /// Raises the degree of a clamped spline by some steps (A5.9): the curve kept exactly, each inner knot standing as many
        /// times more as the steps, so that the curve is as smooth there as it was.
        /// </summary>
        internal static void ElevateDegree(int p, List<double> u, List<double[]> pw, int t, out List<double> uh, out List<double[]> qw)
        {
            int n = pw.Count - 1;
            int m = n + p + 1;
            int ph = p + t;
            int ph2 = ph / 2;
            var bezalfs = new double[ph + 1, p + 1];
            var bpts = new double[p + 1][];
            var ebpts = new double[ph + 1][];
            var nextbpts = new double[Math.Max(1, p - 1)][];
            var alfs = new double[Math.Max(1, p - 1)];
            bezalfs[0, 0] = 1.0;
            bezalfs[ph, p] = 1.0;

            for (int i = 1; i <= ph2; i++)
            {
                double inv = 1.0 / Binomial(ph, i);
                int mpi = Math.Min(p, i);

                for (int j = Math.Max(0, i - t); j <= mpi; j++)
                {
                    bezalfs[i, j] = inv * Binomial(p, j) * Binomial(t, i - j);
                }
            }

            for (int i = ph2 + 1; i <= ph - 1; i++)
            {
                int mpi = Math.Min(p, i);

                for (int j = Math.Max(0, i - t); j <= mpi; j++)
                {
                    bezalfs[i, j] = bezalfs[ph - i, p - j];
                }
            }

            uh = new List<double>();
            qw = new List<double[]>();
            int kind = ph + 1;
            int r = -1;
            int a = p;
            int b = p + 1;
            int cind = 1;
            double ua = u[0];
            qw.Add((double[])pw[0].Clone());

            for (int i = 0; i <= ph; i++)
            {
                uh.Add(ua);
            }

            for (int i = 0; i <= p; i++)
            {
                bpts[i] = (double[])pw[i].Clone();
            }

            while (b < m)
            {
                int i0 = b;

                while (b < m && u[b] == u[b + 1])
                {
                    b++;
                }

                int mul = b - i0 + 1;
                double ub = u[b];
                int oldr = r;
                r = p - mul;
                int lbz = oldr > 0 ? (oldr + 2) / 2 : 1;
                int rbz = r > 0 ? ph - (r + 1) / 2 : ph;

                if (r > 0)
                {
                    double numer = ub - ua;

                    for (int k = p; k > mul; k--)
                    {
                        alfs[k - mul - 1] = numer / (u[a + k] - ua);
                    }

                    for (int j = 1; j <= r; j++)
                    {
                        int save = r - j;
                        int s = mul + j;

                        for (int k = p; k >= s; k--)
                        {
                            bpts[k] = Mix(alfs[k - s], bpts[k], bpts[k - 1]);
                        }

                        nextbpts[save] = bpts[p];
                    }
                }

                for (int i = lbz; i <= ph; i++)
                {
                    var e = new double[4];
                    int mpi = Math.Min(p, i);

                    for (int j = Math.Max(0, i - t); j <= mpi; j++)
                    {
                        for (int c = 0; c < 4; c++)
                        {
                            e[c] += bezalfs[i, j] * bpts[j][c];
                        }
                    }

                    ebpts[i] = e;
                }

                if (oldr > 1)
                {
                    int first = kind - 2;
                    int last = kind;
                    double den = ub - ua;
                    double bet = (ub - uh[kind - 1]) / den;

                    for (int tr = 1; tr < oldr; tr++)
                    {
                        int i = first;
                        int j = last;
                        int kj = j - kind + 1;

                        while (j - i > tr)
                        {
                            if (i < cind)
                            {
                                double alf = (ub - uh[i]) / (ua - uh[i]);
                                qw[i] = Mix(alf, qw[i], qw[i - 1]);
                            }

                            if (j >= lbz)
                            {
                                if (j - tr <= kind - ph + oldr)
                                {
                                    double gam = (ub - uh[j - tr]) / den;
                                    ebpts[kj] = Mix(gam, ebpts[kj], ebpts[kj + 1]);
                                }
                                else
                                {
                                    ebpts[kj] = Mix(bet, ebpts[kj], ebpts[kj + 1]);
                                }
                            }

                            i++;
                            j--;
                            kj--;
                        }

                        first--;
                        last++;
                    }
                }

                if (a != p)
                {
                    for (int i = 0; i < ph - oldr; i++)
                    {
                        uh.Add(ua);
                        kind++;
                    }
                }

                for (int j = lbz; j <= rbz; j++)
                {
                    if (cind < qw.Count)
                    {
                        qw[cind] = ebpts[j];
                    }
                    else
                    {
                        qw.Add(ebpts[j]);
                    }

                    cind++;
                }

                if (b < m)
                {
                    for (int j = 0; j < r; j++)
                    {
                        bpts[j] = nextbpts[j];
                    }

                    for (int j = r; j <= p; j++)
                    {
                        bpts[j] = (double[])pw[b - p + j].Clone();
                    }

                    a = b;
                    b++;
                    ua = ub;
                }
                else
                {
                    for (int i = 0; i <= ph; i++)
                    {
                        uh.Add(ub);
                    }
                }
            }

            while (qw.Count > uh.Count - ph - 1)
            {
                qw.RemoveAt(qw.Count - 1);
            }
        }

        /// <summary>
        /// Runs a clamped spline on past one of its ends to a parameter, the polynomial of its end span carried on: the knot
        /// that starts the span raised to the degree, which leaves that span a Bézier piece, whose points are then those of the
        /// same polynomial over the longer span, found by de Casteljau's rule at the share past the end.
        /// </summary>
        internal static void Extend(int p, List<double> u, List<double[]> pw, double to)
        {
            bool atEnd = to > u[u.Count - 1];
            double end = atEnd ? u[u.Count - 1] : u[0];
            double inner = end;

            // The other knot of the end span: the last below the end, or the first above the start.
            for (int i = atEnd ? u.Count - 1 : 0; atEnd ? i >= 0 : i < u.Count; i += atEnd ? -1 : 1)
            {
                if (u[i] != end)
                {
                    inner = u[i];
                    break;
                }
            }

            int times = p - Multiplicity(u, inner);

            if (inner != end && times > 0 && inner != u[0] && inner != u[u.Count - 1])
            {
                InsertKnot(p, u, pw, inner, times);
            }

            int n = pw.Count;
            var bezier = new double[p + 1][];

            for (int j = 0; j <= p; j++)
            {
                bezier[j] = atEnd ? pw[n - 1 - p + j] : pw[p - j];
            }

            // Over [inner, end] the piece runs from share 0 to 1; over [inner, to] it is the left part at the share of to.
            double s = (to - inner) / (end - inner);
            double[][] left = LeftPart(bezier, s);

            // A rational spline whose weight, carried on, would reach nought there has no point: it cannot run on so far.
            foreach (double[] q in left)
            {
                if (!(q[3] > 0.0))
                {
                    throw new InvalidOperationException("The spline cannot run on so far: a weight would reach nought.");
                }
            }

            for (int j = 0; j <= p; j++)
            {
                if (atEnd)
                {
                    pw[n - 1 - p + j] = left[j];
                }
                else
                {
                    pw[p - j] = left[j];
                }
            }

            for (int i = 0; i < u.Count; i++)
            {
                if (u[i] == end)
                {
                    u[i] = to;
                }
            }
        }

        /// <summary>The points of a Bézier piece over [0, s] of its parameter, s past 1 running it on: the first of each row of de Casteljau's triangle.</summary>
        internal static double[][] LeftPart(double[][] bezier, double s)
        {
            int p = bezier.Length - 1;
            var row = new double[p + 1][];

            for (int j = 0; j <= p; j++)
            {
                row[j] = (double[])bezier[j].Clone();
            }

            var left = new double[p + 1][];
            left[0] = (double[])row[0].Clone();

            for (int level = 1; level <= p; level++)
            {
                for (int j = 0; j <= p - level; j++)
                {
                    row[j] = Mix(1.0 - s, row[j], row[j + 1]);
                }

                left[level] = (double[])row[0].Clone();
            }

            return left;
        }

        /// <summary>The point of a Bézier piece in homogeneous form at a share of its parameter, by de Casteljau's rule.</summary>
        internal static double[] BezierAt(double[][] bezier, double s)
        {
            int p = bezier.Length - 1;
            var row = new double[p + 1][];

            for (int j = 0; j <= p; j++)
            {
                row[j] = (double[])bezier[j].Clone();
            }

            for (int level = 1; level <= p; level++)
            {
                for (int j = 0; j <= p - level; j++)
                {
                    row[j] = Mix(1.0 - s, row[j], row[j + 1]);
                }
            }

            return row[0];
        }

        /// <summary>
        /// The basis functions of a span and their first derivatives at a parameter (A2.3, to the first order), as two rows of
        /// degree and one numbers each.
        /// </summary>
        internal static double[,] BasisDerivatives(int p, IReadOnlyList<double> u, int span, double t, int order)
        {
            var ndu = new double[p + 1, p + 1];
            var left = new double[p + 1];
            var right = new double[p + 1];
            ndu[0, 0] = 1.0;

            for (int j = 1; j <= p; j++)
            {
                left[j] = t - u[span + 1 - j];
                right[j] = u[span + j] - t;
                double saved = 0.0;

                for (int r = 0; r < j; r++)
                {
                    ndu[j, r] = right[r + 1] + left[j - r];
                    double temp = ndu[r, j - 1] / ndu[j, r];
                    ndu[r, j] = saved + right[r + 1] * temp;
                    saved = left[j - r] * temp;
                }

                ndu[j, j] = saved;
            }

            var ders = new double[order + 1, p + 1];

            for (int j = 0; j <= p; j++)
            {
                ders[0, j] = ndu[j, p];
            }

            var a = new double[2, p + 1];

            for (int r = 0; r <= p; r++)
            {
                int s1 = 0;
                int s2 = 1;
                a[0, 0] = 1.0;

                for (int k = 1; k <= order; k++)
                {
                    double d = 0.0;
                    int rk = r - k;
                    int pk = p - k;

                    if (r >= k)
                    {
                        a[s2, 0] = a[s1, 0] / ndu[pk + 1, rk];
                        d = a[s2, 0] * ndu[rk, pk];
                    }

                    int j1 = rk >= -1 ? 1 : -rk;
                    int j2 = r - 1 <= pk ? k - 1 : p - r;

                    for (int j = j1; j <= j2; j++)
                    {
                        a[s2, j] = (a[s1, j] - a[s1, j - 1]) / ndu[pk + 1, rk + j];
                        d += a[s2, j] * ndu[rk + j, pk];
                    }

                    if (r <= pk)
                    {
                        a[s2, k] = -a[s1, k - 1] / ndu[pk + 1, r];
                        d += a[s2, k] * ndu[r, pk];
                    }

                    ders[k, r] = d;
                    int swap = s1;
                    s1 = s2;
                    s2 = swap;
                }
            }

            int factor = p;

            for (int k = 1; k <= order; k++)
            {
                for (int j = 0; j <= p; j++)
                {
                    ders[k, j] *= factor;
                }

                factor *= p - k;
            }

            return ders;
        }

        /// <summary>
        /// The clamped spline of a degree and a count of control points that fits points given at parameters best by least
        /// squares (A9.7), passing through the first and the last: its knots spread over the parameters as The NURBS Book's
        /// (9.69) spreads them, so that every span holds some of the points.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the system has no solution, as for points that leave a span empty.</exception>
        internal static void LeastSquares(IReadOnlyList<GeoPoint3> q, IReadOnlyList<double> ub, int p, int count, out List<double> knots, out List<GeoPoint3> points)
        {
            int m = q.Count - 1;
            int n = count - 1;
            knots = new List<double>(count + p + 1);

            for (int i = 0; i <= p; i++)
            {
                knots.Add(ub[0]);
            }

            double d = (m + 1.0) / (n - p + 1.0);

            for (int j = 1; j <= n - p; j++)
            {
                int i = (int)(j * d);
                double alpha = j * d - i;
                knots.Add((1.0 - alpha) * ub[i - 1] + alpha * ub[i]);
            }

            for (int i = 0; i <= p; i++)
            {
                knots.Add(ub[m]);
            }

            points = new List<GeoPoint3>(count) { q[0] };

            if (n <= 1)
            {
                if (n == 1)
                {
                    points.Add(q[m]);
                }

                return;
            }

            // The normal equations for the inner points P1 .. Pn-1: (N^T N) P = N^T R. A point of the curve lies under p + 1
            // control points, so N^T N is nought beyond p places off its diagonal: kept as that band, its upper half, and
            // solved by Cholesky's factoring of the band, so that 5 000 control points cost 5 000 rows of p + 1, not a
            // square of 25 million.
            int size = n - 1;
            var band = new double[size, p + 1];
            var rhs = new double[size, 3];

            for (int k = 1; k < m; k++)
            {
                int span = Span(p, knots, count, ub[k]);
                double[,] basis = BasisDerivatives(p, knots, span, ub[k], 0);
                double first = span - p == 0 ? basis[0, 0] : 0.0;
                double last = span == n ? basis[0, p] : 0.0;
                double rx = q[k].X - first * q[0].X - last * q[m].X;
                double ry = q[k].Y - first * q[0].Y - last * q[m].Y;
                double rz = q[k].Z - first * q[0].Z - last * q[m].Z;

                for (int a = 0; a <= p; a++)
                {
                    int i = span - p + a;

                    if (i < 1 || i > n - 1)
                    {
                        continue;
                    }

                    rhs[i - 1, 0] += basis[0, a] * rx;
                    rhs[i - 1, 1] += basis[0, a] * ry;
                    rhs[i - 1, 2] += basis[0, a] * rz;

                    for (int c = a; c <= p; c++)
                    {
                        int j = span - p + c;

                        if (j >= 1 && j <= n - 1)
                        {
                            band[i - 1, j - i] += basis[0, a] * basis[0, c];
                        }
                    }
                }
            }

            double[,] solved = SolveBand(band, rhs);

            for (int i = 0; i < size; i++)
            {
                points.Add(new GeoPoint3(solved[i, 0], solved[i, 1], solved[i, 2]));
            }

            points.Add(q[m]);
        }

        /// <summary>
        /// Whether a spline clamped at both ends has a periodic form, as AutoCAD 2021 gives one to a spline of the flag 2048:
        /// unclamped at its start and at its end (A12.1), the first degree of its control points the same as the degree a period
        /// on, within <see cref="SeamGap"/> and <see cref="SeamShare"/> of its size (one and the largest of its coordinates),
        /// and of the same weights within <see cref="SeamGap"/> of the largest. AutoCAD's periodic splines it saved are so to 1.3E-15 of their size, its cubic of 7 control
        /// points and the knots 0, 0, 0, 0, 30, 45, 55, 80, 80, 80, 80 to 1E-13; its 2059 of a point beside its seam moved by
        /// 1E-11, off by 4.1E-11, it read as periodic, and moved by 1E-10, off by 4.1E-10, as not (its 17-w). False where the spline has
        /// fewer points than twice the degree and one, or knots the unclamping cannot divide by.
        /// </summary>
        internal static bool IsPeriodicForm(int p, List<double> u, List<double[]> pw)
        {
            int count = pw.Count;
            int m = count - p;

            if (m < p + 1)
            {
                return false;
            }

            int n = count - 1;
            List<double[]> left = pw.ConvertAll(q => (double[])q.Clone());
            List<double[]> right = pw.ConvertAll(q => (double[])q.Clone());
            var ul = new List<double>(u);
            var ur = new List<double>(u);

            for (int i = 0; i <= p - 2; i++)
            {
                // The start: the knots before it laid out as those before the end, and the points before it worked back.
                ul[p - i - 1] = ul[p - i] - (ul[n - i + 1] - ul[n - i]);

                for (int j = i, k = p - 1; j >= 0; j--, k--)
                {
                    double span = ul[p + j + 1] - ul[k];
                    double a = (ul[p] - ul[k]) / span;

                    if (!(span > 0.0) || !(a < 1.0))
                    {
                        return false;
                    }

                    left[j] = Unmix(left[j], left[j + 1], a, 1.0 - a);
                }

                // The end: the knots after it laid out as those after the start, and the points after it worked back.
                ur[n + i + 2] = ur[n + i + 1] + (ur[p + i + 1] - ur[p + i]);

                for (int j = i; j >= 0; j--)
                {
                    double span = ur[n - j + i + 2] - ur[n - j];
                    double a = (ur[n + 1] - ur[n - j]) / span;

                    if (!(span > 0.0) || !(a > 0.0))
                    {
                        return false;
                    }

                    right[n - j] = Unmix(right[n - j], right[n - j - 1], 1.0 - a, a);
                }
            }

            double largest = 0.0;
            double size = 0.0;

            foreach (double[] q in pw)
            {
                largest = Math.Max(largest, Math.Abs(q[3]));

                for (int c = 0; c < 3; c++)
                {
                    size = Math.Max(size, Math.Abs(q[c] / q[3]));
                }
            }

            var tolerance = new Tolerance(SeamGap + SeamShare * (1.0 + size), SeamGap);

            for (int i = 0; i < p; i++)
            {
                // The points of the curve unclamped: those before the degree less one from the start, those after from the end.
                double[] a = i < p - 1 ? left[i] : pw[i];
                double[] b = i + m > n - p + 1 ? right[i + m] : pw[i + m];

                if (!(Math.Abs(a[3] - b[3]) <= tolerance.EqualVector * largest)
                    || !new GeoPoint3(a[0] / a[3], a[1] / a[3], a[2] / a[3]).IsEqualTo(new GeoPoint3(b[0] / b[3], b[1] / b[3], b[2] / b[3]), tolerance))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>The point that, mixed as a of it and b of another, gives a third: (q - b other) / a, coordinate by coordinate.</summary>
        private static double[] Unmix(double[] q, double[] other, double b, double a)
        {
            var point = new double[q.Length];

            for (int c = 0; c < q.Length; c++)
            {
                point[c] = (q[c] - b * other[c]) / a;
            }

            return point;
        }

        /// <summary>
        /// The cubic through fit points as AutoCAD 2021 fits a spline that is not periodic, whatever degree it is asked: the
        /// points one after another within its point tolerance taken as one (<see cref="SeamGap"/> and <see cref="SeamShare"/> of
        /// their size), each on the curve at its knot, the knots the lengths of the chords from the first point and the ends
        /// four times each, so that n points give n and 2 control points; at an end given a tangent the derivative there that
        /// tangent as it is, by the length along the chords, and at an end given none (or one of no length) the curve bent no
        /// more, its second derivative nought (The NURBS Book, 9.2.2 and (3.9), as ezdxf's <c>fit_points_to_cad_cv</c> has it).
        /// Through (0, 0), (30, 40) and (60, 0) it is (0, 0), (10, 20), (30, 60), (50, 20) and (60, 0) over the knots 0, 0, 0, 0,
        /// 50, 100, 100, 100, 100, as AutoCAD fitted it; through five points, alone or given tangents of length 1 or 5, the
        /// library's control points are AutoCAD's to 1E-13 (its D32 run). False where the points are all one, or the system
        /// has no solution.
        /// </summary>
        internal static bool InterpolateCubic(IReadOnlyList<GeoPoint3> fit, GeoVector3 start, GeoVector3 end, out List<GeoPoint3> through, out List<double> knots, out List<GeoPoint3> points)
        {
            knots = null;
            points = null;
            through = OneEach(fit, out _);
            int n = through.Count - 1;

            if (n < 1)
            {
                return false;
            }

            var t = new double[n + 1];

            for (int i = 1; i <= n; i++)
            {
                t[i] = t[i - 1] + through[i - 1].DistanceTo(through[i]);
            }

            double length = t[n];
            const int p = 3;
            int m = n + 3;
            var u = new List<double>(m + p + 1) { 0.0, 0.0, 0.0, 0.0 };

            for (int i = 1; i < n; i++)
            {
                u.Add(t[i]);
            }

            u.AddRange(new[] { length, length, length, length });
            var columns = new List<int>[m];
            var entries = new List<double>[m];
            var rhs = new double[m][];
            bool leaves = start.Length > 0.0;
            bool arrives = end.Length > 0.0;

            columns[0] = new List<int> { 0 };
            entries[0] = new List<double> { 1.0 };
            rhs[0] = new[] { through[0].X, through[0].Y, through[0].Z };

            if (leaves)
            {
                // The derivative at the start: 3 (P1 - P0) over the first chord.
                double share = u[p + 1] / p;
                columns[1] = new List<int> { 0, 1 };
                entries[1] = new List<double> { -1.0, 1.0 };
                rhs[1] = new[] { share * start.X, share * start.Y, share * start.Z };
            }
            else
            {
                // No second derivative at the start, the row divided down so that its largest entry is 1.
                double a = u[p + 1];
                double b = u[p + 2];
                columns[1] = new List<int> { 0, 1, 2 };
                entries[1] = new List<double> { b / (a + b), -1.0, a / (a + b) };
                rhs[1] = new double[3];
            }

            for (int j = 1; j < n; j++)
            {
                double[,] basis = BasisDerivatives(p, u, p + j, t[j], 0);
                var row = new List<int>(p + 1);
                var values = new List<double>(p + 1);

                for (int a = 0; a <= p; a++)
                {
                    if (basis[0, a] != 0.0)
                    {
                        row.Add(j + a);
                        values.Add(basis[0, a]);
                    }
                }

                columns[j + 1] = row;
                entries[j + 1] = values;
                rhs[j + 1] = new[] { through[j].X, through[j].Y, through[j].Z };
            }

            if (arrives)
            {
                double share = (length - u[m - 1]) / p;
                columns[m - 2] = new List<int> { m - 2, m - 1 };
                entries[m - 2] = new List<double> { -1.0, 1.0 };
                rhs[m - 2] = new[] { share * end.X, share * end.Y, share * end.Z };
            }
            else
            {
                double a = length - u[m - 1];
                double b = length - u[m - 2];
                columns[m - 2] = new List<int> { m - 3, m - 2, m - 1 };
                entries[m - 2] = new List<double> { a / (a + b), -1.0, b / (a + b) };
                rhs[m - 2] = new double[3];
            }

            columns[m - 1] = new List<int> { m - 1 };
            entries[m - 1] = new List<double> { 1.0 };
            rhs[m - 1] = new[] { through[n].X, through[n].Y, through[n].Z };
            double[][] solved = SolveSparse(columns, entries, rhs);

            if (solved == null)
            {
                return false;
            }

            points = new List<GeoPoint3>(m);

            foreach (double[] q in solved)
            {
                points.Add(new GeoPoint3(q[0], q[1], q[2]));
            }

            knots = u;
            return true;
        }

        /// <summary>
        /// Whether AutoCAD 2021 reads a SPLINE of fit points as another curve than its control points give, and that curve: it
        /// fits every SPLINE of two fit points or more anew through them when it reads it, as <see cref="InterpolateCubic"/>
        /// does, whatever its flags, degree and control points (its D32 run: a control point moved by 1, the knots doubled, the
        /// degree 2 or 5, the flags 8 or 40 in place of 1064, all read as the fit), save one of the flag 2048 of a periodic
        /// spline, which AutoCAD writes with no fit points, and one of a fit tolerance above 1E-10, which it fits otherwise.
        /// True where that fit is not the record's own (<see cref="IsTheFit"/>: another degree or count, weights, knots not the
        /// chords, or a condition of the fit not met within its point tolerance). AutoCAD's own splines of fit points
        /// are their fit to 3E-13, so that they are read as their records hold them, to the bit; the library's 17-g of v9,
        /// closed cubics of 70 11 through 5 and 6 fit points, are not, and AutoCAD read them as their fits.
        /// </summary>
        internal static bool FitsAnew(IReadOnlyList<GeoPoint3> fit, GeoVector3 start, GeoVector3 end, double fitTolerance, int flags, int degree, IReadOnlyList<GeoPoint3> points, IReadOnlyList<double> knots, IReadOnlyList<double> weights, out List<double> fitKnots, out List<GeoPoint3> fitPoints)
        {
            fitKnots = null;
            fitPoints = null;

            return fit.Count >= 2 && fitTolerance <= SeamGap && (flags & 2048) == 0
                && !IsTheFit(fit, start, end, degree, points, knots, weights)
                && InterpolateCubic(fit, start, end, out _, out fitKnots, out fitPoints);
        }

        /// <summary>
        /// Whether a record's control points and knots are the cubic <see cref="InterpolateCubic"/> fits through fit points, told
        /// without fitting: a cubic of no weights other than 1, of as many points and knots, its knots the chords' lengths, and
        /// every condition the fit is solved for met within <see cref="SeamGap"/> and <see cref="SeamShare"/> of the points'
        /// size: its ends the first and last fit points, its point at each inner knot the fit point there, and at each end the
        /// derivative the tangent gives, or no second derivative where none is given. The system having one solution, a curve
        /// that meets them is the fit's.
        /// </summary>
        private static bool IsTheFit(IReadOnlyList<GeoPoint3> fit, GeoVector3 start, GeoVector3 end, int degree, IReadOnlyList<GeoPoint3> points, IReadOnlyList<double> knots, IReadOnlyList<double> weights)
        {
            if (degree != 3)
            {
                return false;
            }

            foreach (double w in weights)
            {
                if (w != 1.0)
                {
                    return false;
                }
            }

            List<GeoPoint3> through = OneEach(fit, out Tolerance same);
            int n = through.Count - 1;
            int m = n + 3;

            if (n < 1 || points.Count != m || knots.Count != m + 4)
            {
                return false;
            }

            double t = 0.0;

            for (int i = 0; i <= m + 3; i++)
            {
                if (i > 3 && i <= n + 3)
                {
                    t += through[i - 4].DistanceTo(through[i - 3]);
                }

                if (!(Math.Abs(knots[i] - t) <= same.EqualPoint))
                {
                    return false;
                }
            }

            if (!points[0].IsEqualTo(through[0], same) || !points[m - 1].IsEqualTo(through[n], same))
            {
                return false;
            }

            for (int j = 1; j < n; j++)
            {
                double[,] basis = BasisDerivatives(3, knots, 3 + j, knots[3 + j], 0);
                double x = 0.0, y = 0.0, z = 0.0;

                for (int a = 0; a <= 3; a++)
                {
                    x += basis[0, a] * points[j + a].X;
                    y += basis[0, a] * points[j + a].Y;
                    z += basis[0, a] * points[j + a].Z;
                }

                if (!new GeoPoint3(x, y, z).IsEqualTo(through[j], same))
                {
                    return false;
                }
            }

            return EndHolds(points[0], points[1], points[2], knots[4], knots[5], start, same)
                && EndHolds(points[m - 1], points[m - 2], points[m - 3], t - knots[m - 1], t - knots[m - 2], -end, same);
        }

        /// <summary>
        /// Whether an end of a cubic meets the condition of <see cref="InterpolateCubic"/> there: the derivative from its end
        /// point towards the next the tangent, by the first span over 3, or of no tangent, no second derivative.
        /// </summary>
        private static bool EndHolds(GeoPoint3 p0, GeoPoint3 p1, GeoPoint3 p2, double a, double b, GeoVector3 tangent, Tolerance same)
        {
            if (tangent.Length > 0.0)
            {
                double share = a / 3.0;
                return new GeoPoint3(p1.X - p0.X, p1.Y - p0.Y, p1.Z - p0.Z).IsEqualTo(new GeoPoint3(share * tangent.X, share * tangent.Y, share * tangent.Z), same);
            }

            double s = a + b;
            return new GeoPoint3(b / s * p0.X - p1.X + a / s * p2.X, b / s * p0.Y - p1.Y + a / s * p2.Y, b / s * p0.Z - p1.Z + a / s * p2.Z).IsEqualTo(new GeoPoint3(0.0, 0.0, 0.0), same);
        }

        /// <summary>The fit points, one after another within AutoCAD's point tolerance of their size taken as one, and that tolerance.</summary>
        private static List<GeoPoint3> OneEach(IReadOnlyList<GeoPoint3> fit, out Tolerance same)
        {
            double size = 0.0;

            foreach (GeoPoint3 q in fit)
            {
                size = Math.Max(size, Math.Max(Math.Abs(q.X), Math.Max(Math.Abs(q.Y), Math.Abs(q.Z))));
            }

            same = new Tolerance(SeamGap + SeamShare * (1.0 + size), SeamGap);
            var through = new List<GeoPoint3>(fit.Count);

            foreach (GeoPoint3 q in fit)
            {
                if (through.Count == 0 || !through[through.Count - 1].IsEqualTo(q, same))
                {
                    through.Add(q);
                }
            }

            return through;
        }

        /// <summary>
        /// The periodic spline of a degree through points taken round, the last back to the first, as AutoCAD 2021 fits a
        /// periodic spline: each point on the curve at its knot, the knots the lengths of the chords from the first point, the m
        /// points' system solved for the m control points of a period, then clamped at the first knot and again a period on, as
        /// AutoCAD writes it, m and the degree control points and the end knots the degree and one times each. Its cubic
        /// through (0, 0), (40, 1), (50, 30) and (10, 25) is that of its 17-l to 1E-14: the knots 0, 40.01, 70.69, 111.00 and
        /// 137.93, the control points (0, 0), (4.78, -9.71), (44.07, -9.27), (60.39, 41.46), (8.49, 34.10), (-3.22, 6.53) and
        /// (0, 0). False where there are fewer points than the degree and one, a chord is of no length, or the system has no
        /// solution, as for any spline of an even degree through an even count of points, whose system is singular.
        /// </summary>
        internal static bool InterpolatePeriodic(IReadOnlyList<GeoPoint3> ring, int p, out List<double> knots, out List<GeoPoint3> points)
        {
            knots = null;
            points = null;
            int m = ring.Count;

            if (p < 1 || m < p + 1)
            {
                return false;
            }

            var t = new double[m + 1];

            for (int i = 1; i <= m; i++)
            {
                double chord = ring[i - 1].DistanceTo(ring[i % m]);

                if (!(chord > 0.0))
                {
                    return false;
                }

                t[i] = t[i - 1] + chord;
            }

            // The knots of the curve as it runs on round, a period before and after: the floating point k is the control
            // point (k + m - p) mod m of the period.
            double period = t[m];
            var u = new List<double>(m + 2 * p + 1);

            for (int i = m - p; i < m; i++)
            {
                u.Add(t[i] - period);
            }

            for (int i = 0; i <= m; i++)
            {
                u.Add(t[i]);
            }

            for (int i = 1; i <= p; i++)
            {
                u.Add(t[i] + period);
            }

            var columns = new List<int>[m];
            var entries = new List<double>[m];
            var rhs = new double[m][];

            for (int j = 0; j < m; j++)
            {
                double[,] basis = BasisDerivatives(p, u, p + j, t[j], 0);
                var row = new SortedDictionary<int, double>();

                for (int a = 0; a <= p; a++)
                {
                    if (basis[0, a] != 0.0)
                    {
                        int column = (j + a + m - p) % m;
                        row[column] = (row.TryGetValue(column, out double held) ? held : 0.0) + basis[0, a];
                    }
                }

                columns[j] = new List<int>(row.Keys);
                entries[j] = new List<double>(row.Values);
                rhs[j] = new[] { ring[j].X, ring[j].Y, ring[j].Z };
            }

            double[][] solved = SolveSparse(columns, entries, rhs);

            if (solved == null)
            {
                return false;
            }

            var pw = new List<double[]>(m + p);

            for (int k = 0; k < m + p; k++)
            {
                double[] q = solved[(k + m - p) % m];
                pw.Add(new[] { q[0], q[1], q[2], 1.0 });
            }

            Clamp(p, u, pw);
            Split(pw, out points, out _);
            knots = u;
            return true;
        }

        /// <summary>
        /// Solves a square system of few entries to a row, each row its columns rising and their entries, for three right sides:
        /// Gaussian elimination with the largest entry of each column as pivot, the first row of two as large, the rows kept as
        /// their entries alone, so that a ring of 5 000 points costs 5 000 short rows and not a square of 25 million; null where
        /// the system has no solution.
        /// </summary>
        private static double[][] SolveSparse(List<int>[] columns, List<double>[] entries, double[][] rhs)
        {
            int m = rhs.Length;

            // The rows by the column of their first entry, those of the column eliminated next among them.
            var byFirst = new List<int>[m];

            for (int r = 0; r < m; r++)
            {
                if (columns[r].Count == 0)
                {
                    return null;
                }

                (byFirst[columns[r][0]] ?? (byFirst[columns[r][0]] = new List<int>())).Add(r);
            }

            var pivotOf = new int[m];

            for (int c = 0; c < m; c++)
            {
                List<int> rows = byFirst[c];
                int pivot = -1;
                double largest = 0.0;

                foreach (int r in rows ?? new List<int>())
                {
                    double size = Math.Abs(entries[r][0]);

                    if (size > largest || (size == largest && pivot >= 0 && r < pivot))
                    {
                        largest = size;
                        pivot = r;
                    }
                }

                // The entries are values of the basis, from nought to 1: one this small leaves the column with no pivot.
                if (!(largest > 1E-12))
                {
                    return null;
                }

                pivotOf[c] = pivot;

                foreach (int r in rows)
                {
                    if (r == pivot)
                    {
                        continue;
                    }

                    double factor = entries[r][0] / entries[pivot][0];
                    Subtract(columns, entries, r, pivot, factor);

                    for (int k = 0; k < 3; k++)
                    {
                        rhs[r][k] -= factor * rhs[pivot][k];
                    }

                    if (columns[r].Count == 0)
                    {
                        return null;
                    }

                    (byFirst[columns[r][0]] ?? (byFirst[columns[r][0]] = new List<int>())).Add(r);
                }

                byFirst[c] = null;
            }

            var x = new double[m][];

            for (int c = m - 1; c >= 0; c--)
            {
                int r = pivotOf[c];
                var value = (double[])rhs[r].Clone();

                for (int i = 1; i < columns[r].Count; i++)
                {
                    for (int k = 0; k < 3; k++)
                    {
                        value[k] -= entries[r][i] * x[columns[r][i]][k];
                    }
                }

                for (int k = 0; k < 3; k++)
                {
                    value[k] /= entries[r][0];
                }

                x[c] = value;
            }

            return x;
        }

        /// <summary>Takes a factor of the pivot's row from a row whose first entry it clears, the entries left in their columns' order, none of nought kept.</summary>
        private static void Subtract(List<int>[] columns, List<double>[] entries, int row, int pivot, double factor)
        {
            List<int> rc = columns[row];
            List<double> re = entries[row];
            List<int> pc = columns[pivot];
            List<double> pe = entries[pivot];
            var keptColumns = new List<int>(rc.Count + pc.Count);
            var keptEntries = new List<double>(rc.Count + pc.Count);
            int i = 1;
            int j = 1;

            while (i < rc.Count || j < pc.Count)
            {
                int column;
                double entry;

                if (j >= pc.Count || (i < rc.Count && rc[i] < pc[j]))
                {
                    column = rc[i];
                    entry = re[i++];
                }
                else if (i >= rc.Count || pc[j] < rc[i])
                {
                    column = pc[j];
                    entry = -factor * pe[j++];
                }
                else
                {
                    column = rc[i];
                    entry = re[i++] - factor * pe[j++];
                }

                if (entry != 0.0)
                {
                    keptColumns.Add(column);
                    keptEntries.Add(entry);
                }
            }

            columns[row] = keptColumns;
            entries[row] = keptEntries;
        }

        /// <summary>
        /// Solves a symmetric system that is nought beyond a band of its diagonal, given as the band's upper half (row i, column
        /// d the entry at i, i + d), for three right sides: the band factored as U^T U by Cholesky, then the two triangles solved.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the system is not positive definite, as for points that leave a span empty.</exception>
        private static double[,] SolveBand(double[,] band, double[,] b)
        {
            int n = band.GetLength(0);
            int w = band.GetLength(1) - 1;
            int columns = b.GetLength(1);
            var u = new double[n, w + 1];

            for (int i = 0; i < n; i++)
            {
                for (int d = 0; d <= w && i + d < n; d++)
                {
                    int j = i + d;
                    double sum = band[i, d];

                    for (int k = Math.Max(0, j - w); k < i; k++)
                    {
                        sum -= u[k, i - k] * u[k, j - k];
                    }

                    if (d == 0)
                    {
                        if (!(sum > 1E-300))
                        {
                            throw new InvalidOperationException("The points do not give the spline a control point of its own for every span.");
                        }

                        u[i, 0] = Math.Sqrt(sum);
                    }
                    else
                    {
                        u[i, d] = sum / u[i, 0];
                    }
                }
            }

            var x = (double[,])b.Clone();

            for (int c = 0; c < columns; c++)
            {
                // U^T y = b, from the first row down; then U x = y, from the last row up.
                for (int i = 0; i < n; i++)
                {
                    double sum = x[i, c];

                    for (int k = Math.Max(0, i - w); k < i; k++)
                    {
                        sum -= u[k, i - k] * x[k, c];
                    }

                    x[i, c] = sum / u[i, 0];
                }

                for (int i = n - 1; i >= 0; i--)
                {
                    double sum = x[i, c];

                    for (int d = 1; d <= w && i + d < n; d++)
                    {
                        sum -= u[i, d] * x[i + d, c];
                    }

                    x[i, c] = sum / u[i, 0];
                }
            }

            return x;
        }

        /// <summary>The mix (1 - a) of the second and a of the first, coordinate by coordinate: a times the first, plus the rest of the second.</summary>
        private static double[] Mix(double a, double[] first, double[] second)
        {
            var q = new double[first.Length];

            for (int c = 0; c < q.Length; c++)
            {
                q[c] = a * first[c] + (1.0 - a) * second[c];
            }

            return q;
        }

        private static double Binomial(int n, int k)
        {
            if (k < 0 || k > n)
            {
                return 0.0;
            }

            double result = 1.0;

            for (int i = 1; i <= k; i++)
            {
                result = result * (n - k + i) / i;
            }

            return result;
        }
    }
}
