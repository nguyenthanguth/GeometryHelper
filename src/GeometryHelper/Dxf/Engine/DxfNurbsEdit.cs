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
        /// on, within the point tolerance, and of the same weights within the vector tolerance of the largest. AutoCAD's
        /// periodic cubic of 7 control points and the knots 0, 0, 0, 0, 30, 45, 55, 80, 80, 80, 80 is so to 1E-13; its closed
        /// cubic of 2059 with a point beside its seam moved by 5 is off by 20 and more. False where the spline has fewer points
        /// than twice the degree and one, or knots the unclamping cannot divide by.
        /// </summary>
        internal static bool IsPeriodicForm(int p, List<double> u, List<double[]> pw, Tolerance tolerance)
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

            foreach (double[] q in pw)
            {
                largest = Math.Max(largest, Math.Abs(q[3]));
            }

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
