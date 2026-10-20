using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Geometry;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// A piece of a shape as the oracle reads it: how far a point is from it, a function that changes sign across it, and
    /// whether a crossing found where that function is nought counts, which is a matter of the length of a segment or the sweep of an
    /// arc. It shares no code with the library: the formulas are written out again here, for plain doubles.
    /// </summary>
    internal abstract class MeetPart
    {
        /// <summary>Gets the distance from a point to the piece, measured along its length and not as a region.</summary>
        internal abstract double Gap(double x, double y);

        /// <summary>Gets a number that is nought on the curve the piece lies on and changes sign across it.</summary>
        internal abstract double Signed(double x, double y);

        /// <summary>Says whether a point of the curve the piece lies on is a point of the piece itself.</summary>
        internal abstract bool InRange(double x, double y);
    }

    /// <summary>A segment, a ray or a line without ends, as the oracle reads it.</summary>
    internal sealed class MeetSegment : MeetPart
    {
        private readonly double _ox, _oy, _dx, _dy, _length;
        private readonly bool _ray, _endless;

        internal MeetSegment(double ox, double oy, double dx, double dy, double length, bool ray, bool endless)
        {
            _ox = ox;
            _oy = oy;
            _dx = dx;
            _dy = dy;
            _length = length;
            _ray = ray;
            _endless = endless;
        }

        internal static MeetSegment Between(GeoPoint2 a, GeoPoint2 b)
        {
            double dx = b.X - a.X;
            double dy = b.Y - a.Y;
            double length = Math.Sqrt(dx * dx + dy * dy);

            return length > 0.0 ? new MeetSegment(a.X, a.Y, dx / length, dy / length, length, false, false) : new MeetSegment(a.X, a.Y, 1.0, 0.0, 0.0, false, false);
        }

        internal override double Gap(double x, double y)
        {
            double t = (x - _ox) * _dx + (y - _oy) * _dy;

            if (!_endless)
            {
                t = Math.Max(t, 0.0);

                if (!_ray)
                {
                    t = Math.Min(t, _length);
                }
            }

            double ex = x - (_ox + t * _dx);
            double ey = y - (_oy + t * _dy);

            return Math.Sqrt(ex * ex + ey * ey);
        }

        internal override double Signed(double x, double y) => -_dy * (x - _ox) + _dx * (y - _oy);

        internal override bool InRange(double x, double y)
        {
            double t = (x - _ox) * _dx + (y - _oy) * _dy;

            return _endless || (t >= 0.0 && (_ray || t <= _length));
        }

        public override string ToString() => (_endless ? "xline" : _ray ? "ray" : "segment") + "(" + _ox.ToString("R") + ", " + _oy.ToString("R") + " along " + _dx.ToString("R") + ", " + _dy.ToString("R") + ", length " + _length.ToString("R") + ")";
    }

    /// <summary>A circle or an arc of one, as the oracle reads it.</summary>
    internal sealed class MeetRound : MeetPart
    {
        private readonly double _cx, _cy, _r, _start, _sweep;
        private readonly bool _whole;

        internal MeetRound(double cx, double cy, double r, double start, double sweep, bool whole)
        {
            _cx = cx;
            _cy = cy;
            _r = r;
            _start = start;
            _sweep = sweep;
            _whole = whole;
        }

        /// <summary>The arc of an edge with a bulge, worked out from the chord and the bulge alone.</summary>
        internal static MeetRound OfBulge(GeoPoint2 a, GeoPoint2 b, double bulge)
        {
            double angle = 4.0 * Math.Atan(bulge);
            double chord = Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y));
            double radius = chord / (2.0 * Math.Sin(Math.Abs(angle) / 2.0));

            // The centre is on the perpendicular bisector, on the left of a counter-clockwise arc: a distance of r cos(angle / 2).
            double mx = 0.5 * (a.X + b.X);
            double my = 0.5 * (a.Y + b.Y);
            double nx = -(b.Y - a.Y) / chord;
            double ny = (b.X - a.X) / chord;
            double offset = radius * Math.Cos(Math.Abs(angle) / 2.0) * (bulge > 0.0 ? 1.0 : -1.0);
            double cx = mx + nx * offset;
            double cy = my + ny * offset;

            return new MeetRound(cx, cy, radius, Math.Atan2(a.Y - cy, a.X - cx), angle, false);
        }

        private bool Within(double angle)
        {
            if (_whole)
            {
                return true;
            }

            double along = _sweep >= 0.0 ? angle - _start : _start - angle;
            along -= 2.0 * Math.PI * Math.Floor(along / (2.0 * Math.PI));

            return along <= Math.Abs(_sweep);
        }

        internal override double Gap(double x, double y)
        {
            double dx = x - _cx;
            double dy = y - _cy;
            double rho = Math.Sqrt(dx * dx + dy * dy);

            if (rho == 0.0)
            {
                return _r;
            }

            if (Within(Math.Atan2(dy, dx)))
            {
                return Math.Abs(rho - _r);
            }

            double sx = _cx + _r * Math.Cos(_start);
            double sy = _cy + _r * Math.Sin(_start);
            double ex = _cx + _r * Math.Cos(_start + _sweep);
            double ey = _cy + _r * Math.Sin(_start + _sweep);

            return Math.Min(Math.Sqrt((x - sx) * (x - sx) + (y - sy) * (y - sy)), Math.Sqrt((x - ex) * (x - ex) + (y - ey) * (y - ey)));
        }

        internal override double Signed(double x, double y) => Math.Sqrt((x - _cx) * (x - _cx) + (y - _cy) * (y - _cy)) - _r;

        internal override bool InRange(double x, double y) => Within(Math.Atan2(y - _cy, x - _cx));

        public override string ToString() => "round(" + _cx.ToString("R") + ", " + _cy.ToString("R") + ", r " + _r.ToString("R") + ", from " + _start.ToString("R") + " sweep " + _sweep.ToString("R") + (_whole ? ", whole" : string.Empty) + ")";
    }

    /// <summary>An ellipse as the oracle reads it: its distance found by a dense search over the eccentric angle and golden section.</summary>
    internal sealed class MeetEllipse : MeetPart
    {
        private readonly double _cx, _cy, _ux, _uy, _a, _b;

        internal MeetEllipse(double cx, double cy, double ux, double uy, double a, double b)
        {
            _cx = cx;
            _cy = cy;
            _ux = ux;
            _uy = uy;
            _a = a;
            _b = b;
        }

        internal override double Gap(double x, double y)
        {
            double dx = x - _cx;
            double dy = y - _cy;
            double y0 = Math.Abs(dx * _ux + dy * _uy);
            double y1 = Math.Abs(dy * _ux - dx * _uy);

            // The distance from a point of the first quadrant to the ellipse, by bisection on the multiplier of the normal condition
            // (Eberly, "Distance from a point to an ellipse", Geometric Tools): written out again here, with nothing taken from the library.
            double e0 = _a;
            double e1 = _b;

            if (y1 > 0.0)
            {
                if (y0 > 0.0)
                {
                    double z0 = y0 / e0;
                    double z1 = y1 / e1;
                    double g = z0 * z0 + z1 * z1 - 1.0;

                    if (g == 0.0)
                    {
                        return 0.0;
                    }

                    double r0 = (e0 / e1) * (e0 / e1);
                    double n0 = r0 * z0;
                    double s0 = z1 - 1.0;
                    double s1 = g < 0.0 ? 0.0 : Math.Sqrt(n0 * n0 + z1 * z1) - 1.0;
                    double s = 0.0;

                    for (int i = 0; i < 1100; i++)
                    {
                        s = 0.5 * (s0 + s1);

                        if (s == s0 || s == s1)
                        {
                            break;
                        }

                        double ratio0 = n0 / (s + r0);
                        double ratio1 = z1 / (s + 1.0);
                        g = ratio0 * ratio0 + ratio1 * ratio1 - 1.0;

                        if (g > 0.0)
                        {
                            s0 = s;
                        }
                        else if (g < 0.0)
                        {
                            s1 = s;
                        }
                        else
                        {
                            break;
                        }
                    }

                    double x0 = r0 * y0 / (s + r0);
                    double x1 = y1 / (s + 1.0);

                    return Math.Sqrt((x0 - y0) * (x0 - y0) + (x1 - y1) * (x1 - y1));
                }

                return Math.Abs(y1 - e1);
            }

            double numerator = e0 * y0;
            double denominator = e0 * e0 - e1 * e1;

            if (numerator < denominator)
            {
                double share = numerator / denominator;
                double px = e0 * share;
                double py = e1 * Math.Sqrt(1.0 - share * share);

                return Math.Sqrt((px - y0) * (px - y0) + py * py);
            }

            return Math.Abs(y0 - e0);
        }

        internal override double Signed(double x, double y)
        {
            double dx = x - _cx;
            double dy = y - _cy;
            double along = (dx * _ux + dy * _uy) / _a;
            double across = (dy * _ux - dx * _uy) / _b;

            return along * along + across * across - 1.0;
        }

        internal override bool InRange(double x, double y) => true;

        public override string ToString() => "ellipse(" + _cx.ToString("R") + ", " + _cy.ToString("R") + ", axis " + _ux.ToString("R") + ", " + _uy.ToString("R") + ", radii " + _a.ToString("R") + ", " + _b.ToString("R") + ")";
    }

    /// <summary>
    /// A shape as the oracle reads it, and the library's own object drawn from the same numbers: the pieces of its boundary and, when
    /// it is closed, a test of whether a point lies in its material.
    /// </summary>
    internal sealed class MeetShape
    {
        internal object Shape;
        internal string Kind;
        internal List<MeetPart> Parts = new List<MeetPart>();
        internal Func<double, double, bool> Inside;
    }

    /// <summary>
    /// The oracle for a spline against a shape: the spline is sampled densely over every span, the places it crosses each piece
    /// found where a function that changes sign across the piece does, by bisection, and the least distance by golden section round
    /// each least of the samples. Points are those of the Cox-de Boor recursion in plain doubles of the spline oracle, so the
    /// numbers share nothing with the library, neither the spline's nor the shape's.
    /// </summary>
    internal static class Spline2MeetOracle
    {
        internal static double Golden(Func<double, double> f, double low, double high) => Golden(f, low, high, out _);

        internal static double Golden(Func<double, double> f, double low, double high, out double at)
        {
            const double Ratio = 0.6180339887498949;
            double a = low;
            double b = high;
            double c = b - Ratio * (b - a);
            double d = a + Ratio * (b - a);
            double fc = f(c);
            double fd = f(d);

            for (int i = 0; i < 100; i++)
            {
                if (fc < fd)
                {
                    b = d;
                    d = c;
                    fd = fc;
                    c = b - Ratio * (b - a);
                    fc = f(c);
                }
                else
                {
                    a = c;
                    c = d;
                    fc = fd;
                    d = a + Ratio * (b - a);
                    fd = f(d);
                }
            }

            at = fc < fd ? c : d;
            return Math.Min(fc, fd);
        }

        /// <summary>The result of asking: the distance and the places the spline crosses or touches, as knot values.</summary>
        internal sealed class Answer
        {
            internal double Distance;

            /// <summary>The distance to the boundary alone: what the shortest segment is as long as, where a spline inside a closed shape is nought from it.</summary>
            internal double Boundary;
            internal List<double> Knots = new List<double>();

            /// <summary>The nearest approach that is not a crossing, or -1: when it is within twice the point tolerance the case is not clear-cut.</summary>
            internal double NearMiss = -1.0;

            /// <summary>
            /// Every place the spline comes near the shape, as points: where it crosses, and each least of its distance to a piece of the
            /// shape that is within the tolerance and that no other piece is nearer at; those within the tolerance of each other, one from
            /// the next, are one place.
            /// </summary>
            internal List<double[]> Places = new List<double[]>();

            /// <summary>The least of those leasts that is within the tolerance and no crossing, or -1.</summary>
            internal double LeastNear = -1.0;

            /// <summary>The length of the longest stretch of the spline that is within the tolerance of the shape with its distance changing by no more than half the tolerance.</summary>
            internal double LongestLevel;

            /// <summary>The value of every least of the distance to a piece of the shape that is within twice the tolerance: a case with one at the tolerance is not clear-cut.</summary>
            internal List<double> Leasts = new List<double>();
        }

        /// <summary>
        /// Asks the oracle about a spline and the pieces of a shape.
        /// </summary>
        internal static Answer Ask(SplineOracle spline, MeetShape shape, double tolerance, int samples = 500)
        {
            var answer = new Answer();
            double[] breaks = spline.Breaks();
            var us = new List<double>();

            for (int i = 0; i + 1 < breaks.Length; i++)
            {
                int n = Math.Max(samples, 40) / Math.Max(1, breaks.Length - 1) + 40;

                for (int j = 0; j < n; j++)
                {
                    us.Add(breaks[i] + (breaks[i + 1] - breaks[i]) * j / n);
                }
            }

            us.Add(breaks[breaks.Length - 1]);

            // A span run at a speed that changes a millionfold, as weights a thousandfold apart make it, leaves long gaps between samples
            // taken evenly in the parameter: any two neighbours further than a two-thousandth of the size apart are given a sample between.
            double reach = Math.Max(1.0, spline.Points.Max(Math.Abs));
            double spacing = 5E-4;
            double[][] points = us.Select(u => spline.FastPoint(u)).ToArray();
            Func<double[], double> toShape = p => shape.Parts.Min(part => part.Gap(p[0], p[1]));
            double[] shapeGaps = points.Select(toShape).ToArray();

            // Two neighbours within a segment's length of the shape, as near it as that, are given a sample between, down to a thousandth of
            // a millionth of the size: a curve can cross a piece twice, a hair apart, between two samples that are both on one side of it.
            for (int round = 0; round < 40 && us.Count < 400000; round++)
            {
                var nextUs = new List<double>(us.Count);
                var nextPoints = new List<double[]>(us.Count);
                var nextShapeGaps = new List<double>(us.Count);
                bool split = false;

                for (int i = 0; i < us.Count; i++)
                {
                    nextUs.Add(us[i]);
                    nextPoints.Add(points[i]);
                    nextShapeGaps.Add(shapeGaps[i]);

                    if (i + 1 < us.Count)
                    {
                        double gap = Math.Sqrt((points[i + 1][0] - points[i][0]) * (points[i + 1][0] - points[i][0]) + (points[i + 1][1] - points[i][1]) * (points[i + 1][1] - points[i][1]));
                        double middle = 0.5 * (us[i] + us[i + 1]);
                        bool wide = gap > spacing * reach;
                        bool close = us.Count < 60000 && Math.Min(shapeGaps[i], shapeGaps[i + 1]) <= 1.5 * gap && gap > 1E-5 * reach;

                        if ((wide || close) && middle > us[i] && middle < us[i + 1])
                        {
                            double[] p = spline.FastPoint(middle);
                            nextUs.Add(middle);
                            nextPoints.Add(p);
                            nextShapeGaps.Add(toShape(p));
                            split = true;
                        }
                    }
                }

                us = nextUs;
                points = nextPoints.ToArray();
                shapeGaps = nextShapeGaps.ToArray();

                if (!split)
                {
                    break;
                }
            }

            // The longest stretch along the spline that stays within the tolerance of the shape and level: its distance changing by no more
            // than half the tolerance. The library reads a stretch of that kind, long enough, as lying along an ellipse and gives no place in it.
            double level = 0.0;
            double levelLow = 0.0;
            double levelHigh = 0.0;
            double run = 0.0;
            bool inLevel = false;

            for (int i = 0; i < us.Count; i++)
            {
                double d = shapeGaps[i];

                if (d <= tolerance && inLevel && Math.Max(levelHigh, d) - Math.Min(levelLow, d) <= 0.5 * tolerance)
                {
                    run += Math.Sqrt((points[i][0] - points[i - 1][0]) * (points[i][0] - points[i - 1][0]) + (points[i][1] - points[i - 1][1]) * (points[i][1] - points[i - 1][1]));
                    levelLow = Math.Min(levelLow, d);
                    levelHigh = Math.Max(levelHigh, d);
                }
                else
                {
                    inLevel = d <= tolerance;
                    run = 0.0;
                    levelLow = d;
                    levelHigh = d;
                }

                level = Math.Max(level, run);
            }

            answer.LongestLevel = level;

            double least = double.MaxValue;
            double nearMiss = double.MaxValue;
            var nearCandidates = new List<double[]>();

            foreach (MeetPart part in shape.Parts)
            {
                var gaps = new double[us.Count];
                var signs = new double[us.Count];

                for (int i = 0; i < us.Count; i++)
                {
                    gaps[i] = part.Gap(points[i][0], points[i][1]);
                    signs[i] = part.Signed(points[i][0], points[i][1]);
                }

                bool crossed = false;

                for (int i = 0; i + 1 < us.Count; i++)
                {
                    if (signs[i] == 0.0 || (signs[i] < 0.0) != (signs[i + 1] < 0.0))
                    {
                        double low = us[i];
                        double high = us[i + 1];
                        double signLow = signs[i];

                        for (int step = 0; step < 100; step++)
                        {
                            double middle = 0.5 * (low + high);
                            double[] p = spline.FastPoint(middle);
                            double s = part.Signed(p[0], p[1]);

                            if ((s < 0.0) == (signLow < 0.0))
                            {
                                low = middle;
                                signLow = s;
                            }
                            else
                            {
                                high = middle;
                            }
                        }

                        double root = 0.5 * (low + high);
                        double[] at = spline.FastPoint(root);

                        if (part.InRange(at[0], at[1]))
                        {
                            answer.Knots.Add(root);
                            crossed = true;
                        }
                    }
                }

                // The least distance of the spline to this piece, from each least of the samples.
                for (int i = 0; i < us.Count; i++)
                {
                    bool isLeast = (i == 0 || gaps[i] <= gaps[i - 1]) && (i == us.Count - 1 || gaps[i] <= gaps[i + 1]);

                    if (!isLeast)
                    {
                        continue;
                    }

                    double v = Golden(u => { double[] p = spline.FastPoint(u); return part.Gap(p[0], p[1]); }, us[Math.Max(i - 1, 0)], us[Math.Min(i + 1, us.Count - 1)], out double leastAt);

                    if (gaps[i] < v)
                    {
                        v = gaps[i];
                        leastAt = us[i];
                    }

                    least = Math.Min(least, v);

                    if (v <= 2.0 * tolerance)
                    {
                        answer.Leasts.Add(v);
                    }

                    if (v <= tolerance)
                    {
                        nearCandidates.Add(new[] { leastAt, v });
                    }

                    if (!crossed && v > 0.0)
                    {
                        nearMiss = Math.Min(nearMiss, v);
                    }
                }
            }

            double[] start = spline.FastPoint(spline.Start);
            bool inside = shape.Inside != null && shape.Inside(start[0], start[1]);
            answer.Boundary = answer.Knots.Count > 0 ? 0.0 : least;
            answer.Distance = answer.Knots.Count > 0 || inside ? 0.0 : least;
            answer.NearMiss = nearMiss == double.MaxValue ? -1.0 : nearMiss;
            answer.Knots.Sort();
            var crossingKnots = answer.Knots.ToList();

            // Crossings no further apart than the tolerance are one place.
            var merged = new List<double>();
            var mergedPoints = new List<double[]>();

            foreach (double knot in answer.Knots)
            {
                double[] p = spline.FastPoint(knot);

                if (!mergedPoints.Any(q => Math.Sqrt((q[0] - p[0]) * (q[0] - p[0]) + (q[1] - p[1]) * (q[1] - p[1])) <= tolerance))
                {
                    merged.Add(knot);
                    mergedPoints.Add(p);
                }
            }

            answer.Knots = merged;

            // Every place: the crossings and the leasts within the tolerance that no other piece of the shape is nearer at, those within
            // the tolerance of each other, one from the next, being one place.
            var found = new List<double[]>();

            foreach (double knot in crossingKnots)
            {
                found.Add(spline.FastPoint(knot));
            }

            double slack = 1E-9 * Math.Max(1.0, reach);

            foreach (double[] candidate in nearCandidates.OrderBy(c => c[0]))
            {
                double[] p = spline.FastPoint(candidate[0]);

                if (shape.Parts.All(part => part.Gap(p[0], p[1]) >= candidate[1] - slack))
                {
                    found.Add(p);
                    answer.LeastNear = answer.LeastNear < 0.0 ? candidate[1] : Math.Min(answer.LeastNear, candidate[1]);
                }
            }

            var parent = Enumerable.Range(0, found.Count).ToArray();

            Func<int, int> find = null;
            find = i => parent[i] == i ? i : (parent[i] = find(parent[i]));

            for (int i = 0; i < found.Count; i++)
            {
                for (int j = 0; j < i; j++)
                {
                    double dx = found[i][0] - found[j][0];
                    double dy = found[i][1] - found[j][1];

                    if (Math.Sqrt(dx * dx + dy * dy) <= tolerance)
                    {
                        parent[find(i)] = find(j);
                    }
                }
            }

            for (int i = 0; i < found.Count; i++)
            {
                if (find(i) == i)
                {
                    answer.Places.Add(found[i]);
                }
            }

            return answer;
        }

        /// <summary>The result of asking about two splines: the distance, and the places they cross as points of the first.</summary>
        internal sealed class SplineAnswer
        {
            internal double Distance;
            internal List<double[]> Crossings = new List<double[]>();
        }

        /// <summary>
        /// Asks the oracle about two splines: both are sampled, each crossing of the two polylines of samples is brought to the curves by
        /// Newton's method on points exact to rounding, and the least distance, where they do not cross, is found from each least of the
        /// grid of sample pairs by a compass search that keeps inside the domains. A crossing narrower than the samples, two curves
        /// crossing twice within a sample, can be missed; the tests allow for that where it matters.
        /// </summary>
        internal static SplineAnswer AskSplines(SplineOracle a, SplineOracle b, double size, int samples = 400)
        {
            var answer = new SplineAnswer();
            double[] ua = Enumerable.Range(0, samples + 1).Select(i => a.Start + (a.End - a.Start) * i / samples).ToArray();
            double[] ub = Enumerable.Range(0, samples + 1).Select(i => b.Start + (b.End - b.Start) * i / samples).ToArray();
            double[][] pa = ua.Select(u => a.FastPoint(u)).ToArray();
            double[][] pb = ub.Select(u => b.FastPoint(u)).ToArray();
            var roots = new List<double[]>();

            for (int i = 0; i < samples; i++)
            {
                for (int j = 0; j < samples; j++)
                {
                    if (Math.Max(pa[i][0], pa[i + 1][0]) < Math.Min(pb[j][0], pb[j + 1][0]) || Math.Max(pb[j][0], pb[j + 1][0]) < Math.Min(pa[i][0], pa[i + 1][0])
                        || Math.Max(pa[i][1], pa[i + 1][1]) < Math.Min(pb[j][1], pb[j + 1][1]) || Math.Max(pb[j][1], pb[j + 1][1]) < Math.Min(pa[i][1], pa[i + 1][1]))
                    {
                        continue;
                    }

                    double d1x = pa[i + 1][0] - pa[i][0], d1y = pa[i + 1][1] - pa[i][1], d2x = pb[j + 1][0] - pb[j][0], d2y = pb[j + 1][1] - pb[j][1];
                    double den = d1x * d2y - d1y * d2x;

                    if (den == 0.0)
                    {
                        continue;
                    }

                    double wx = pb[j][0] - pa[i][0], wy = pb[j][1] - pa[i][1];
                    double t = (wx * d2y - wy * d2x) / den;
                    double u = (wx * d1y - wy * d1x) / den;

                    if (t >= 0.0 && t <= 1.0 && u >= 0.0 && u <= 1.0)
                    {
                        double[] root = Cross(a, b, ua[i] + t * (ua[i + 1] - ua[i]), ub[j] + u * (ub[j + 1] - ub[j]));
                        double[] qa = a.FastPoint(root[0]);
                        double[] qb = b.FastPoint(root[1]);

                        if (Math.Sqrt((qa[0] - qb[0]) * (qa[0] - qb[0]) + (qa[1] - qb[1]) * (qa[1] - qb[1])) < 1E-7 * Math.Max(1.0, size))
                        {
                            roots.Add(root);
                        }
                    }
                }
            }

            foreach (double[] root in roots.OrderBy(r => r[0]))
            {
                double[] p = a.FastPoint(root[0]);

                if (!answer.Crossings.Any(q => Math.Sqrt((q[0] - p[0]) * (q[0] - p[0]) + (q[1] - p[1]) * (q[1] - p[1])) <= 1E-3))
                {
                    answer.Crossings.Add(p);
                }
            }

            if (answer.Crossings.Count > 0)
            {
                answer.Distance = 0.0;
                return answer;
            }

            var grid = new double[samples + 1, samples + 1];

            for (int i = 0; i <= samples; i++)
            {
                for (int j = 0; j <= samples; j++)
                {
                    grid[i, j] = Math.Sqrt((pa[i][0] - pb[j][0]) * (pa[i][0] - pb[j][0]) + (pa[i][1] - pb[j][1]) * (pa[i][1] - pb[j][1]));
                }
            }

            double least = double.MaxValue;

            for (int i = 0; i <= samples; i++)
            {
                for (int j = 0; j <= samples; j++)
                {
                    bool isLeast = true;

                    for (int di = -1; di <= 1 && isLeast; di++)
                    {
                        for (int dj = -1; dj <= 1; dj++)
                        {
                            int ii = i + di;
                            int jj = j + dj;

                            if ((di == 0 && dj == 0) || ii < 0 || jj < 0 || ii > samples || jj > samples)
                            {
                                continue;
                            }

                            if (grid[ii, jj] < grid[i, j])
                            {
                                isLeast = false;
                                break;
                            }
                        }
                    }

                    if (isLeast)
                    {
                        least = Math.Min(least, Compass(a, b, ua[i], ub[j], (a.End - a.Start) / samples, (b.End - b.Start) / samples, grid[i, j]));
                    }
                }
            }

            answer.Distance = least;
            return answer;
        }

        private static double[] Cross(SplineOracle a, SplineOracle b, double s, double t)
        {
            for (int step = 0; step < 60; step++)
            {
                double[] p = a.FastPoint(s);
                double[] q = b.FastPoint(t);
                double fx = p[0] - q[0], fy = p[1] - q[1];
                double hs = 1E-6 * (a.End - a.Start), ht = 1E-6 * (b.End - b.Start);
                double sa = Math.Min(s + hs, a.End), sb = Math.Max(s - hs, a.Start), ta = Math.Min(t + ht, b.End), tb = Math.Max(t - ht, b.Start);
                double[] a1 = a.FastPoint(sa), a0 = a.FastPoint(sb), b1 = b.FastPoint(ta), b0 = b.FastPoint(tb);
                double axs = (a1[0] - a0[0]) / (sa - sb), ays = (a1[1] - a0[1]) / (sa - sb), bxt = (b1[0] - b0[0]) / (ta - tb), byt = (b1[1] - b0[1]) / (ta - tb);
                double det = axs * -byt - -bxt * ays;

                if (Math.Abs(det) < 1E-300)
                {
                    break;
                }

                double ds = (-fx * -byt - -bxt * -fy) / det;
                double dt = (axs * -fy - ays * -fx) / det;
                s = Math.Min(Math.Max(s + ds, a.Start), a.End);
                t = Math.Min(Math.Max(t + dt, b.Start), b.End);

                if (Math.Abs(ds) < 1E-14 && Math.Abs(dt) < 1E-14)
                {
                    break;
                }
            }

            return new[] { s, t };
        }

        /// <summary>A compass search for a least of the distance between two splines, kept inside their domains, from a start.</summary>
        private static double Compass(SplineOracle a, SplineOracle b, double s, double t, double stepS, double stepT, double value)
        {
            double Distance(double ss, double tt)
            {
                double[] p = a.FastPoint(ss);
                double[] q = b.FastPoint(tt);

                return Math.Sqrt((p[0] - q[0]) * (p[0] - q[0]) + (p[1] - q[1]) * (p[1] - q[1]));
            }

            for (int iteration = 0; iteration < 4000 && stepS > 1E-15 * (a.End - a.Start); iteration++)
            {
                bool moved = false;

                for (int d = 0; d < 8 && !moved; d++)
                {
                    double ds = d == 0 || d == 4 || d == 5 ? stepS : d == 1 || d == 6 || d == 7 ? -stepS : 0.0;
                    double dt = d == 2 || d == 4 || d == 6 ? stepT : d == 3 || d == 5 || d == 7 ? -stepT : 0.0;
                    double ns = Math.Min(Math.Max(s + ds, a.Start), a.End);
                    double nt = Math.Min(Math.Max(t + dt, b.Start), b.End);
                    double nv = Distance(ns, nt);

                    if (nv < value)
                    {
                        value = nv;
                        s = ns;
                        t = nt;
                        moved = true;
                    }
                }

                if (!moved)
                {
                    stepS *= 0.5;
                    stepT *= 0.5;
                }
            }

            return value;
        }

        /// <summary>The even-odd rule: whether a point is inside a ring of vertices.</summary>
        internal static bool InRing(IReadOnlyList<GeoPoint2> ring, double x, double y)
        {
            bool inside = false;

            for (int i = 0, j = ring.Count - 1; i < ring.Count; j = i++)
            {
                if ((ring[i].Y > y) != (ring[j].Y > y) && x < (ring[j].X - ring[i].X) * (y - ring[i].Y) / (ring[j].Y - ring[i].Y) + ring[i].X)
                {
                    inside = !inside;
                }
            }

            return inside;
        }
    }
}
