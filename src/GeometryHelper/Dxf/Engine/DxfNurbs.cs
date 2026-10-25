using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// A spline as a SPLINE record gives it, walked by its knot value as AutoCAD walks one: its degree, knots, control points
    /// and weights as they are, clamped or not, the curve running from the knot of index degree to that of index count of the
    /// control points.
    /// </summary>
    /// <remarks>
    /// The point and its derivatives are worked out by de Boor's basis functions and their derivatives (Piegl and Tiller,
    /// The NURBS Book, A2.1, A2.3 and A4.2); the length by Gauss-Legendre quadrature of 20 points over each knot span, halved
    /// until the halves agree with the whole to 1E-15 of it, as the oracle's own integration is.
    /// </remarks>
    internal sealed class DxfNurbs
    {
        private static readonly double[] Nodes;
        private static readonly double[] NodeWeights;

        private readonly int _p;
        private readonly double[] _u;
        private readonly GeoPoint3[] _points;
        private readonly double[] _w;

        // The lengths to the knots, worked out at the first asking: the spline's data are fixed once made.
        private double[] _knotLengths;

        // How near the halves of a piece agree with the whole for a length worked out loosely, as for cuts.
        private const double Loose = 1E-9;

        static DxfNurbs()
        {
            const int N = 20;
            Nodes = new double[N];
            NodeWeights = new double[N];

            for (int i = 0; i < N; i++)
            {
                double x = Math.Cos(Math.PI * (i + 0.75) / (N + 0.5));
                double dp = 0.0;

                for (int iteration = 0; iteration < 100; iteration++)
                {
                    double p0 = 1.0;
                    double p1 = x;

                    for (int k = 2; k <= N; k++)
                    {
                        double p2 = ((2.0 * k - 1.0) * x * p1 - (k - 1.0) * p0) / k;
                        p0 = p1;
                        p1 = p2;
                    }

                    dp = N * (x * p1 - p0) / (x * x - 1.0);
                    double step = p1 / dp;
                    x -= step;

                    if (Math.Abs(step) < 1E-16)
                    {
                        break;
                    }
                }

                Nodes[i] = x;
                NodeWeights[i] = 2.0 / ((1.0 - x * x) * dp * dp);
            }
        }

        /// <summary>The spline of a degree, knots, control points and weights (null for a spline that is not rational).</summary>
        /// <exception cref="InvalidOperationException">Thrown when the counts do not make a spline: fewer control points than the degree and one, or knots not that count and the degree and one, or weights not one to each point, or knots that go down.</exception>
        internal DxfNurbs(int degree, IReadOnlyList<double> knots, IReadOnlyList<GeoPoint3> points, IReadOnlyList<double> weights)
        {
            int n = points.Count;

            if (degree < 1 || n < degree + 1 || knots.Count != n + degree + 1 || (weights != null && weights.Count != 0 && weights.Count != n))
            {
                throw new InvalidOperationException("The spline's degree, knots, control points and weights do not make a spline: degree " + degree + ", " + knots.Count + " knots, " + n + " control points.");
            }

            for (int i = 1; i < knots.Count; i++)
            {
                if (!(knots[i] >= knots[i - 1]))
                {
                    throw new InvalidOperationException("The knots of the spline go down.");
                }
            }

            _p = degree;
            _u = new double[knots.Count];
            _points = new GeoPoint3[n];
            _w = new double[n];

            for (int i = 0; i < knots.Count; i++)
            {
                _u[i] = knots[i];
            }

            for (int i = 0; i < n; i++)
            {
                _points[i] = points[i];
                _w[i] = weights == null || weights.Count == 0 ? 1.0 : weights[i];
            }

            if (!(End > Start))
            {
                throw new InvalidOperationException("The spline runs over no span of its knots.");
            }
        }

        /// <summary>The parameter at the start: the knot of index degree.</summary>
        internal double Start => _u[_p];

        /// <summary>The parameter at the end: the knot of index count of the control points.</summary>
        internal double End => _u[_points.Length];

        /// <summary>The length of the whole spline.</summary>
        internal double Length => LengthTo(End);

        /// <summary>The point, first and second derivatives by the parameter at a parameter of the domain.</summary>
        internal void Evaluate(double t, out GeoPoint3 point, out GeoVector3 first, out GeoVector3 second)
        {
            t = Math.Max(Start, Math.Min(End, t));
            int span = Span(t);
            double[,] ders = Derivatives(span, t, 2);
            double ax = 0, ay = 0, az = 0, aw = 0, dx = 0, dy = 0, dz = 0, dw = 0, ex = 0, ey = 0, ez = 0, ew = 0;

            for (int j = 0; j <= _p; j++)
            {
                int i = span - _p + j;
                double w = _w[i];
                GeoPoint3 c = _points[i];
                ax += ders[0, j] * w * c.X;
                ay += ders[0, j] * w * c.Y;
                az += ders[0, j] * w * c.Z;
                aw += ders[0, j] * w;
                dx += ders[1, j] * w * c.X;
                dy += ders[1, j] * w * c.Y;
                dz += ders[1, j] * w * c.Z;
                dw += ders[1, j] * w;
                ex += ders[2, j] * w * c.X;
                ey += ders[2, j] * w * c.Y;
                ez += ders[2, j] * w * c.Z;
                ew += ders[2, j] * w;
            }

            double px = ax / aw, py = ay / aw, pz = az / aw;
            double fx = (dx - dw * px) / aw, fy = (dy - dw * py) / aw, fz = (dz - dw * pz) / aw;
            double sx = (ex - 2.0 * dw * fx - ew * px) / aw;
            double sy = (ey - 2.0 * dw * fy - ew * py) / aw;
            double sz = (ez - 2.0 * dw * fz - ew * pz) / aw;
            point = new GeoPoint3(px, py, pz);
            first = new GeoVector3(fx, fy, fz);
            second = new GeoVector3(sx, sy, sz);
        }

        /// <summary>The point at a parameter.</summary>
        internal GeoPoint3 PointAt(double t)
        {
            Evaluate(t, out GeoPoint3 point, out _, out _);
            return point;
        }

        /// <summary>The length from the start to a parameter.</summary>
        internal double LengthTo(double t)
        {
            t = Math.Max(Start, Math.Min(End, t));
            double length = 0.0;
            double from = Start;

            for (int k = _p + 1; k <= _points.Length && from < t; k++)
            {
                double to = Math.Min(_u[k], t);

                if (to > from)
                {
                    length += Integrate(from, to, 0);
                    from = to;
                }
            }

            return length;
        }

        /// <summary>
        /// The length between two parameters, the second above the first, worked out span by span from the first: each piece
        /// halved until the halves agree with the whole to 1E-9 of a unit, a millionth of the point tolerance, rather than the
        /// 1E-15 of it <see cref="LengthTo"/> holds to, for cutting at many parameters in one walk along the spline.
        /// </summary>
        internal double LengthBetween(double a, double b)
        {
            a = Math.Max(Start, Math.Min(End, a));
            b = Math.Max(Start, Math.Min(End, b));

            if (!(b > a))
            {
                return 0.0;
            }

            double length = 0.0;
            double from = a;

            for (int k = Span(a) + 1; k <= _points.Length && from < b; k++)
            {
                double to = Math.Min(_u[k], b);

                if (to > from)
                {
                    length += Integrate(from, to, 0, Loose);
                    from = to;
                }
            }

            return from < b ? length + Integrate(from, b, 0, Loose) : length;
        }

        /// <summary>The length of the whole spline as <see cref="LengthBetween"/> measures it, each span measured once and kept.</summary>
        internal double LooseLength
        {
            get
            {
                double[] lengths = KnotLengths();
                return lengths[lengths.Length - 1];
            }
        }

        /// <summary>
        /// The parameter at a length from the start, the length measured as <see cref="LengthBetween"/> measures it: the span
        /// found among the lengths to the knots, kept once worked out, and Newton's steps taken within it alone, halving where a
        /// step would leave what is left of it, until the length is within 1E-9 of a unit, so that a hundred lengths along 200
        /// spans cost a hundred walks over one span each rather than over all of them.
        /// </summary>
        internal double ParameterAtLooseLength(double length)
        {
            double[] lengths = KnotLengths();

            if (length <= 0.0)
            {
                return Start;
            }

            if (length >= lengths[lengths.Length - 1])
            {
                return End;
            }

            // The last knot reached by the length, and the first beyond it: a span of some length.
            int lo = 0;
            int hi = lengths.Length - 1;

            while (hi - lo > 1)
            {
                int middle = (lo + hi) / 2;

                if (lengths[middle] <= length)
                {
                    lo = middle;
                }
                else
                {
                    hi = middle;
                }
            }

            double a = _u[_p + lo];
            double low = a;
            double high = _u[_p + hi];
            double target = length - lengths[lo];
            double t = a + (high - a) * target / (lengths[hi] - lengths[lo]);

            for (int i = 0; i < 100; i++)
            {
                double f = (t > a ? Integrate(a, t, 0, Loose) : 0.0) - target;

                if (Math.Abs(f) <= Loose)
                {
                    return t;
                }

                if (f > 0.0)
                {
                    high = t;
                }
                else
                {
                    low = t;
                }

                double speed = Speed(t);
                double next = speed > 0.0 ? t - f / speed : 0.5 * (low + high);

                if (!(next > low && next < high))
                {
                    next = 0.5 * (low + high);
                }

                if (next == t || high - low <= 1E-15 * Math.Max(1.0, Math.Abs(t)))
                {
                    return next;
                }

                t = next;
            }

            return t;
        }

        /// <summary>Whether the control points lie within a box whose diagonal is a distance at most, the spline in it.</summary>
        internal bool SpreadWithin(double distance)
        {
            double minX = double.PositiveInfinity, minY = double.PositiveInfinity, minZ = double.PositiveInfinity;
            double maxX = double.NegativeInfinity, maxY = double.NegativeInfinity, maxZ = double.NegativeInfinity;

            foreach (GeoPoint3 c in _points)
            {
                minX = Math.Min(minX, c.X);
                minY = Math.Min(minY, c.Y);
                minZ = Math.Min(minZ, c.Z);
                maxX = Math.Max(maxX, c.X);
                maxY = Math.Max(maxY, c.Y);
                maxZ = Math.Max(maxZ, c.Z);
            }

            double dx = maxX - minX, dy = maxY - minY, dz = maxZ - minZ;
            return Math.Sqrt(dx * dx + dy * dy + dz * dz) <= distance;
        }

        /// <summary>The lengths from the start to each knot of the domain, from the knot of index degree on, as <see cref="LengthBetween"/> measures them.</summary>
        private double[] KnotLengths()
        {
            if (_knotLengths != null)
            {
                return _knotLengths;
            }

            var lengths = new double[_points.Length - _p + 1];

            for (int k = _p; k < _points.Length; k++)
            {
                double a = _u[k];
                double b = _u[k + 1];
                lengths[k - _p + 1] = lengths[k - _p] + (b > a ? Integrate(a, b, 0, Loose) : 0.0);
            }

            _knotLengths = lengths;
            return lengths;
        }

        /// <summary>The parameter at a length from the start, found by halving and Newton's steps until it no longer moves.</summary>
        internal double ParameterAtLength(double length)
        {
            double low = Start;
            double high = End;

            if (length <= 0.0)
            {
                return low;
            }

            if (length >= Length)
            {
                return high;
            }

            double t = low + (high - low) * length / Length;

            for (int i = 0; i < 200; i++)
            {
                double f = LengthTo(t) - length;

                if (f > 0.0)
                {
                    high = t;
                }
                else
                {
                    low = t;
                }

                Evaluate(t, out _, out GeoVector3 d, out _);
                double speed = d.Length;
                double next = speed > 0.0 ? t - f / speed : 0.5 * (low + high);

                if (!(next > low && next < high))
                {
                    next = 0.5 * (low + high);
                }

                if (Math.Abs(next - t) <= 1E-15 * Math.Max(1.0, Math.Abs(t)) || high - low <= 1E-15 * Math.Max(1.0, Math.Abs(t)))
                {
                    return next;
                }

                t = next;
            }

            return t;
        }

        /// <summary>The parameter of the point of the spline nearest a point, and that point: the least parameter where two are as near.</summary>
        internal double Closest(GeoPoint3 q, out GeoPoint3 nearest)
        {
            double best = double.PositiveInfinity;
            double bestT = Start;
            const int Samples = 16;

            for (int k = _p; k < _points.Length; k++)
            {
                double a = _u[k];
                double b = _u[k + 1];

                if (!(b > a))
                {
                    continue;
                }

                for (int s = 0; s <= Samples; s++)
                {
                    double t = Refine(q, a + (b - a) * s / Samples, a, b);
                    double d = PointAt(t).GetVectorTo(q).LengthSquared;

                    if (d < best)
                    {
                        best = d;
                        bestT = t;
                    }
                }
            }

            nearest = PointAt(bestT);
            return bestT;
        }

        /// <summary>Newton's steps towards the nearest point from a start, kept within a span.</summary>
        private double Refine(GeoPoint3 q, double t, double a, double b)
        {
            for (int i = 0; i < 50; i++)
            {
                Evaluate(t, out GeoPoint3 c, out GeoVector3 d1, out GeoVector3 d2);
                GeoVector3 r = q.GetVectorTo(c);
                double f = r.DotProduct(d1);
                double g = d1.DotProduct(d1) + r.DotProduct(d2);

                if (!(g > 0.0))
                {
                    break;
                }

                double next = Math.Max(a, Math.Min(b, t - f / g));

                if (Math.Abs(next - t) <= 1E-15 * Math.Max(1.0, Math.Abs(t)))
                {
                    return next;
                }

                t = next;
            }

            return t;
        }

        private double Speed(double t)
        {
            Evaluate(t, out _, out GeoVector3 d, out _);
            return d.Length;
        }

        private double Integrate(double a, double b, int depth, double loose = 0.0)
        {
            double whole = Gauss(a, b);
            double middle = 0.5 * (a + b);
            double halves = Gauss(a, middle) + Gauss(middle, b);

            if (depth > 30 || Math.Abs(whole - halves) <= Math.Max(loose, 1E-15 * Math.Max(1.0, Math.Abs(halves))))
            {
                return halves;
            }

            return Integrate(a, middle, depth + 1, loose) + Integrate(middle, b, depth + 1, loose);
        }

        private double Gauss(double a, double b)
        {
            double h = 0.5 * (b - a);
            double m = 0.5 * (a + b);
            double sum = 0.0;

            for (int i = 0; i < Nodes.Length; i++)
            {
                sum += NodeWeights[i] * Speed(m + h * Nodes[i]);
            }

            return h * sum;
        }

        /// <summary>The span k of a parameter, u[k] &lt;= t &lt; u[k + 1], of non-zero length; the last such for the end.</summary>
        private int Span(double t)
        {
            int n = _points.Length - 1;

            if (t >= _u[n + 1])
            {
                int k = n;

                while (k > _p && !(_u[k + 1] > _u[k]))
                {
                    k--;
                }

                return k;
            }

            int low = _p;
            int high = n + 1;
            int mid = (low + high) / 2;

            while (t < _u[mid] || t >= _u[mid + 1])
            {
                if (t < _u[mid])
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

        /// <summary>The basis functions of a span and their derivatives up to an order, The NURBS Book's A2.3.</summary>
        private double[,] Derivatives(int span, double t, int order)
        {
            int p = _p;
            var ndu = new double[p + 1, p + 1];
            var left = new double[p + 1];
            var right = new double[p + 1];
            ndu[0, 0] = 1.0;

            for (int j = 1; j <= p; j++)
            {
                left[j] = t - _u[span + 1 - j];
                right[j] = _u[span + j] - t;
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
    }
}
