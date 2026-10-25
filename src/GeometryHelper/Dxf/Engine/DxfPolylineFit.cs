using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// The fitting AutoCAD does to a POLYLINE: the points of a uniform B-spline on a frame of vertices, for a polyline fitted with
    /// a spline (SPLINETYPE, SPLINESEGS) or a mesh smoothed into a surface (SURFTYPE, SURFU, SURFV); and the pairs of arcs of a
    /// polyline fitted with a curve.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The B-spline is AutoCAD's as its PEDIT describes it: on the frame as control points, of the degree the type says (2 for
    /// quadratic, 3 for cubic, one less than the points where there are fewer), clamped to the first and last points of an open
    /// frame and running round a closed one; its knots one step apart. A polyline is given SPLINESEGS segments for each span
    /// between two knots; a mesh SURFU by SURFV points in all, from end to end of an open direction and round a closed one.
    /// </para>
    /// <para>
    /// A curve fit puts a pair of arcs on each segment, meeting at a vertex the fitting adds, with the tangent at each vertex
    /// the one given (flag 2 and group 50) or that of the circle through it and its two neighbours, the ends of an open polyline
    /// taking the circle through the first or last three; the pair is the biarc whose two tangent lengths are equal.
    /// </para>
    /// </remarks>
    internal static class DxfPolylineFit
    {
        /// <summary>A whole number of the header, the fallback where the file has none or it is not a whole number.</summary>
        internal static int Header(DxfItem item, string name, int fallback)
        {
            IReadOnlyList<DxfTypedValue> groups = item.File.HeaderOf(name);

            if (groups == null || groups.Count == 0)
            {
                return fallback;
            }

            return int.TryParse(groups[0].Value?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : fallback;
        }

        /// <summary>The degree of a spline type of the header, 5 quadratic and 6 cubic, or of a polyline's type.</summary>
        internal static int Degree(int splineType) => splineType == 5 ? 2 : 3;

        /// <summary>
        /// The curve of a uniform B-spline on a frame, as an evaluator, and the range of its parameter: clamped over an open
        /// frame, from nought to its count of spans; round a closed one, a span to each point.
        /// </summary>
        internal static DxfNurbs Spline(IReadOnlyList<GeoPoint3> frame, int degree, bool closed, out double start, out double end)
        {
            int n = frame.Count;

            if (closed)
            {
                int q = Math.Max(1, Math.Min(degree, n - 1));
                var points = new List<GeoPoint3>(frame);

                for (int i = 0; i < q; i++)
                {
                    points.Add(frame[i]);
                }

                var knots = new List<double>();

                for (int i = 0; i < points.Count + q + 1; i++)
                {
                    knots.Add(i);
                }

                start = q;
                end = q + n;
                return new DxfNurbs(q, knots, points, null);
            }
            else
            {
                int q = Math.Max(1, Math.Min(degree, n - 1));
                var knots = new List<double>();

                for (int i = 0; i <= q; i++)
                {
                    knots.Add(0.0);
                }

                for (int i = 1; i < n - q; i++)
                {
                    knots.Add(i);
                }

                for (int i = 0; i <= q; i++)
                {
                    knots.Add(n - q);
                }

                start = 0.0;
                end = n - q;
                return new DxfNurbs(q, knots, frame, null);
            }
        }

        /// <summary>
        /// The parameters of the vertices of a polyline fitted with a spline of a frame: a number of steps to each span, the
        /// last on the end of an open frame and short of it round a closed one.
        /// </summary>
        internal static List<double> Steps(double start, double end, int each, bool closed)
        {
            int spans = (int)Math.Round(end - start);
            int count = spans * each;
            var steps = new List<double>(count + 1);

            for (int k = 0; k < count; k++)
            {
                steps.Add(start + (double)k / each);
            }

            if (!closed)
            {
                steps.Add(end);
            }

            return steps;
        }

        /// <summary>
        /// The parameters of a direction of a surface: a count of points from end to end of an open direction, evenly; round a
        /// closed one, as many evenly short of the end.
        /// </summary>
        internal static List<double> Spread(double start, double end, int count, bool closed)
        {
            var spread = new List<double>(count);

            for (int k = 0; k < count; k++)
            {
                spread.Add(closed ? start + (end - start) * k / count : k == count - 1 ? end : start + (end - start) * k / (count - 1));
            }

            return spread;
        }

        /// <summary>
        /// The curve-fit tangents at the vertices of a frame in its plane (x and y): the one given where it is, else that of the
        /// circle through the vertex and its neighbours, along the way the polyline runs.
        /// </summary>
        internal static List<GeoVector2> Tangents(IReadOnlyList<GeoPoint2> p, IReadOnlyList<double?> given, bool closed)
        {
            int n = p.Count;
            var tangents = new List<GeoVector2>(n);

            for (int i = 0; i < n; i++)
            {
                if (given[i].HasValue)
                {
                    tangents.Add(new GeoVector2(Math.Cos(given[i].Value), Math.Sin(given[i].Value)));
                    continue;
                }

                if (n == 2)
                {
                    tangents.Add(Unit(p[1].X - p[0].X, p[1].Y - p[0].Y));
                    continue;
                }

                int a, b, c;

                if (closed || (i > 0 && i < n - 1))
                {
                    a = (i - 1 + n) % n;
                    b = i;
                    c = (i + 1) % n;
                    tangents.Add(Through(p[a], p[b], p[c], p[b], p[c].X - p[a].X, p[c].Y - p[a].Y));
                }
                else if (i == 0)
                {
                    tangents.Add(Through(p[0], p[1], p[2], p[0], p[1].X - p[0].X, p[1].Y - p[0].Y));
                }
                else
                {
                    tangents.Add(Through(p[n - 3], p[n - 2], p[n - 1], p[n - 1], p[n - 1].X - p[n - 2].X, p[n - 1].Y - p[n - 2].Y));
                }
            }

            return tangents;
        }

        /// <summary>
        /// The biarc from one point to another, leaving the first along one tangent and reaching the second along another, its two
        /// tangent lengths equal: where the arcs meet, and the bulge of each. Two straight halves where no such pair is.
        /// </summary>
        internal static void Biarc(GeoPoint2 p0, GeoVector2 t0, GeoPoint2 p1, GeoVector2 t1, out GeoPoint2 joint, out double first, out double second)
        {
            double vx = p1.X - p0.X, vy = p1.Y - p0.Y;
            double tx = t0.X + t1.X, ty = t0.Y + t1.Y;
            double vv = vx * vx + vy * vy;
            double vt = vx * tx + vy * ty;
            double a = 2.0 * (t0.X * t1.X + t0.Y * t1.Y - 1.0);
            double d = double.NaN;

            if (Math.Abs(a) <= 1E-12)
            {
                d = vt > 0.0 ? vv / (2.0 * vt) : double.NaN;
            }
            else
            {
                double disc = vt * vt - a * vv;

                if (disc >= 0.0)
                {
                    double root = Math.Sqrt(disc);
                    double r1 = (vt + root) / a;
                    double r2 = (vt - root) / a;
                    d = r1 > 0.0 && (r2 <= 0.0 || r1 < r2) ? r1 : r2 > 0.0 ? r2 : double.NaN;
                }
            }

            if (!(d > 0.0) || double.IsInfinity(d))
            {
                joint = new GeoPoint2(0.5 * (p0.X + p1.X), 0.5 * (p0.Y + p1.Y));
                first = 0.0;
                second = 0.0;
                return;
            }

            joint = new GeoPoint2(0.5 * (p0.X + d * t0.X + p1.X - d * t1.X), 0.5 * (p0.Y + d * t0.Y + p1.Y - d * t1.Y));
            first = Math.Tan(0.5 * Signed(t0.X, t0.Y, joint.X - p0.X, joint.Y - p0.Y));
            second = Math.Tan(0.5 * Signed(p1.X - joint.X, p1.Y - joint.Y, t1.X, t1.Y));
        }

        /// <summary>The angle from one direction to another, above nought counter-clockwise.</summary>
        private static double Signed(double ax, double ay, double bx, double by) => Math.Atan2(ax * by - ay * bx, ax * bx + ay * by);

        /// <summary>The tangent at a point of the circle through three points, along a direction; that direction where they are on a line.</summary>
        private static GeoVector2 Through(GeoPoint2 a, GeoPoint2 b, GeoPoint2 c, GeoPoint2 at, double alongX, double alongY)
        {
            double d = 2.0 * (a.X * (b.Y - c.Y) + b.X * (c.Y - a.Y) + c.X * (a.Y - b.Y));
            double scale = Math.Max(1.0, Math.Abs(a.X) + Math.Abs(a.Y) + Math.Abs(b.X) + Math.Abs(b.Y) + Math.Abs(c.X) + Math.Abs(c.Y));

            if (Math.Abs(d) <= 1E-12 * scale * scale)
            {
                return Unit(alongX, alongY);
            }

            double aa = a.X * a.X + a.Y * a.Y, bb = b.X * b.X + b.Y * b.Y, cc = c.X * c.X + c.Y * c.Y;
            double ox = (aa * (b.Y - c.Y) + bb * (c.Y - a.Y) + cc * (a.Y - b.Y)) / d;
            double oy = (aa * (c.X - b.X) + bb * (a.X - c.X) + cc * (b.X - a.X)) / d;
            double tx = -(at.Y - oy);
            double ty = at.X - ox;

            if (tx * alongX + ty * alongY < 0.0)
            {
                tx = -tx;
                ty = -ty;
            }

            return Unit(tx, ty);
        }

        private static GeoVector2 Unit(double x, double y)
        {
            double length = Math.Sqrt(x * x + y * y);
            return length > 0.0 ? new GeoVector2(x / length, y / length) : new GeoVector2(1.0, 0.0);
        }
    }
}
