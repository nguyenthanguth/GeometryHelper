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
    /// between two knots; a mesh SURFU by SURFV spans, from end to end of an open direction, one point more than the spans
    /// there, and round a closed one.
    /// </para>
    /// <para>
    /// A curve fit puts a pair of arcs on each segment, meeting at a vertex the fitting adds, with the tangent at each vertex
    /// the one given (flag 2 and group 50) or Akima's blend of the segments either side, the ends of an open polyline taking the
    /// circle through the first or last three, as AutoCAD 2021's PEDIT Fit does to 1E-11 (probed on 60 polylines). A segment
    /// whose tangents are within a millionth of a radian of its line stays straight, with no vertex added.
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
            int q = Spline(frame, degree, closed, out List<GeoPoint3> points, out List<double> knots, out start, out end);
            return new DxfNurbs(q, knots, points, null);
        }

        /// <summary>
        /// The control points and knots of the uniform B-spline on a frame <see cref="Spline(IReadOnlyList{GeoPoint3}, int, bool, out double, out double)"/>
        /// evaluates, and its degree: an open frame's own points, clamped; a closed one's with its first points again after its
        /// last, as many as the degree.
        /// </summary>
        internal static int Spline(IReadOnlyList<GeoPoint3> frame, int degree, bool closed, out List<GeoPoint3> points, out List<double> knots, out double start, out double end)
        {
            int n = frame.Count;
            int q = Math.Max(1, Math.Min(degree, n - 1));
            points = new List<GeoPoint3>(frame);
            knots = new List<double>();

            if (closed)
            {
                for (int i = 0; i < q; i++)
                {
                    points.Add(frame[i]);
                }

                for (int i = 0; i < points.Count + q + 1; i++)
                {
                    knots.Add(i);
                }

                start = q;
                end = q + n;
                return q;
            }

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
            return q;
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
        /// The curve-fit tangents at the vertices of a frame in its plane (x and y), as AutoCAD 2021 takes them: the one given
        /// where it is; else at an end of an open run the tangent of the circle through it and its two neighbours, and inside one
        /// Akima's blend of the two segments at the vertex, each segment s as s / |s|², the one before weighted by the sine of
        /// the turn at the next vertex and the one after by that at the vertex before (1 where that turn is past an end). A
        /// segment of no length splits the frame into runs fitted each on its own; a run of two vertices is its chord, and one
        /// of a single vertex has no tangent (nought).
        /// </summary>
        internal static List<GeoVector2> Tangents(IReadOnlyList<GeoPoint2> p, IReadOnlyList<double?> given, bool closed)
        {
            int n = p.Count;
            var tangents = new GeoVector2[n];
            int segments = closed ? n : n - 1;
            var empty = new List<int>();

            // AutoCAD 2021 breaks the curve only where two vertices are one point: a segment 1E-11 long is fitted as any other.
            for (int i = 0; i < segments; i++)
            {
                GeoPoint2 a = p[i], b = p[(i + 1) % n];

                if (a.X == b.X && a.Y == b.Y)
                {
                    empty.Add(i);
                }
            }

            if (closed && empty.Count == 0)
            {
                Run(p, given, Indices(0, n, n), true, tangents);
                return new List<GeoVector2>(tangents);
            }

            if (!closed)
            {
                empty.Add(n - 1);
            }

            // Each run reaches from the vertex after one empty segment to the vertex that starts the next, round a closed frame.
            int start = closed ? (empty[empty.Count - 1] + 1) % n : 0;

            foreach (int end in empty)
            {
                Run(p, given, Indices(start, (end - start + n) % n + 1, n), false, tangents);
                start = (end + 1) % n;
            }

            return new List<GeoVector2>(tangents);
        }

        /// <summary>A count of indices from one onwards, round a frame of a count of vertices.</summary>
        private static List<int> Indices(int from, int count, int n)
        {
            var indices = new List<int>(count);

            for (int k = 0; k < count; k++)
            {
                indices.Add((from + k) % n);
            }

            return indices;
        }

        /// <summary>The tangents of one run of vertices, open or round a closed frame, into their places.</summary>
        private static void Run(IReadOnlyList<GeoPoint2> p, IReadOnlyList<double?> given, List<int> run, bool cyclic, GeoVector2[] tangents)
        {
            int m = run.Count;
            int count = cyclic ? m : m - 1;
            var segments = new GeoVector2[count];

            for (int k = 0; k < count; k++)
            {
                GeoPoint2 a = p[run[k]], b = p[run[(k + 1) % m]];
                segments[k] = new GeoVector2(b.X - a.X, b.Y - a.Y);
            }

            for (int k = 0; k < m; k++)
            {
                int at = run[k];

                if (given[at].HasValue)
                {
                    tangents[at] = new GeoVector2(Math.Cos(given[at].Value), Math.Sin(given[at].Value));
                }
                else if (m == 1)
                {
                    tangents[at] = new GeoVector2(0.0, 0.0);
                }
                else if (!cyclic && m == 2)
                {
                    tangents[at] = Unit(segments[0].X, segments[0].Y);
                }
                else if (!cyclic && k == 0)
                {
                    tangents[at] = Mirrored(Blend(segments[0], 1.0, segments[1], 1.0), segments[0]);
                }
                else if (!cyclic && k == m - 1)
                {
                    tangents[at] = Mirrored(Blend(segments[count - 2], 1.0, segments[count - 1], 1.0), segments[count - 1]);
                }
                else
                {
                    // The turns at the vertices either side, 1 past an end of an open run.
                    double after = cyclic || k + 1 < count ? Turn(segments[k % count], segments[(k + 1) % count]) : 1.0;
                    double before = cyclic || k >= 2 ? Turn(segments[(k - 2 + count) % count], segments[(k - 1 + count) % count]) : 1.0;
                    tangents[at] = Blend(segments[(k - 1 + count) % count], after, segments[k % count], before);
                }
            }
        }

        /// <summary>The unit direction of two segments each taken as s / |s|² and weighted; nought where they cancel.</summary>
        private static GeoVector2 Blend(GeoVector2 a, double wa, GeoVector2 b, double wb)
        {
            double la = a.X * a.X + a.Y * a.Y, lb = b.X * b.X + b.Y * b.Y;
            return Unit(wa * a.X / la + wb * b.X / lb, wa * a.Y / la + wb * b.Y / lb);
        }

        /// <summary>
        /// The tangent at one end of a chord of a circle from that at its other end, both along the way the circle runs: the
        /// given direction mirrored in the chord's line. Nought stays nought.
        /// </summary>
        private static GeoVector2 Mirrored(GeoVector2 t, GeoVector2 chord)
        {
            GeoVector2 u = Unit(chord.X, chord.Y);
            double along = 2.0 * (t.X * u.X + t.Y * u.Y);
            return new GeoVector2(along * u.X - t.X, along * u.Y - t.Y);
        }

        /// <summary>The sine of the turn from one segment to the next, without its sign.</summary>
        private static double Turn(GeoVector2 a, GeoVector2 b)
        {
            GeoVector2 ua = Unit(a.X, a.Y), ub = Unit(b.X, b.Y);
            return Math.Abs(ua.X * ub.Y - ua.Y * ub.X);
        }

        /// <summary>
        /// The pair of arcs AutoCAD 2021 puts on a segment from one point to another, leaving the first along one tangent and
        /// reaching the second along another: where the arcs meet, and the bulge of each. Where the tangents are either side of
        /// the chord the joint is the incentre of the triangle the chord and the two tangents make, so that the curve there runs
        /// along the chord; where they are on one side (an S) the radii of the arcs are as the sines of the tangents' angles to
        /// the chord, the second's to the first's. False, and no arcs, where either tangent is within a millionth of a radian of
        /// the chord's line, or is nought: the segment stays straight, as AutoCAD keeps it.
        /// </summary>
        internal static bool Biarc(GeoPoint2 p0, GeoVector2 t0, GeoPoint2 p1, GeoVector2 t1, out GeoPoint2 joint, out double first, out double second)
        {
            joint = p0;
            first = 0.0;
            second = 0.0;
            double cx = p1.X - p0.X, cy = p1.Y - p0.Y;
            double length = Math.Sqrt(cx * cx + cy * cy);

            if (length == 0.0)
            {
                return false;
            }

            double ux = cx / length, uy = cy / length;

            // AutoCAD 2021 keeps a segment straight where the sine of either tangent's angle to it is below 1E-6 (probed to 1%).
            if (Math.Abs(ux * t0.Y - uy * t0.X) < 1E-6 || Math.Abs(ux * t1.Y - uy * t1.X) < 1E-6)
            {
                return false;
            }

            double a0 = Signed(ux, uy, t0.X, t0.Y);
            double a1 = Signed(ux, uy, t1.X, t1.Y);

            if (a0 * a1 < 0.0)
            {
                double reach = length * Math.Sin(0.5 * Math.Abs(a1)) / Math.Sin(0.5 * (Math.Abs(a0) + Math.Abs(a1)));
                double cos = Math.Cos(0.5 * a0), sin = Math.Sin(0.5 * a0);
                joint = new GeoPoint2(p0.X + reach * (ux * cos - uy * sin), p0.Y + reach * (ux * sin + uy * cos));
            }
            else
            {
                // The arcs' centres, either side of the curve, are their radii apart: |c0 - c1| = r0 + r1 with r1 = r0 / k.
                double k = Math.Sin(a1) / Math.Sin(a0);
                double side = a0 < 0.0 ? 1.0 : -1.0;
                double n0x = -t0.Y, n0y = t0.X, n1x = -t1.Y, n1y = t1.X;
                double wx = side * (n0x + n1x / k), wy = side * (n0y + n1y / k);
                double dx = p0.X - p1.X, dy = p0.Y - p1.Y;
                double qa = 2.0 * (n0x * n1x + n0y * n1y - 1.0) / k;
                double qb = 2.0 * (dx * wx + dy * wy);
                double qc = dx * dx + dy * dy;
                double disc = qb * qb - 4.0 * qa * qc;

                if (disc < 0.0)
                {
                    return false;
                }

                double q = -0.5 * (qb + (qb < 0.0 ? -1.0 : 1.0) * Math.Sqrt(disc));
                double small = q != 0.0 ? qc / q : double.NaN;
                double large = qa != 0.0 ? q / qa : double.NaN;
                double r0 = small > 0.0 && !(large > 0.0 && large < small) ? small : large;

                if (!(r0 > 0.0) || double.IsInfinity(r0))
                {
                    return false;
                }

                double r1 = r0 / k;
                double c0x = p0.X + side * r0 * n0x, c0y = p0.Y + side * r0 * n0y;
                double c1x = p1.X - side * r1 * n1x, c1y = p1.Y - side * r1 * n1y;
                double share = r0 / (r0 + r1);
                joint = new GeoPoint2(c0x + (c1x - c0x) * share, c0y + (c1y - c0y) * share);
            }

            first = Math.Tan(0.5 * Signed(t0.X, t0.Y, joint.X - p0.X, joint.Y - p0.Y));
            second = Math.Tan(0.5 * Signed(p1.X - joint.X, p1.Y - joint.Y, t1.X, t1.Y));
            return true;
        }

        /// <summary>The angle from one direction to another, above nought counter-clockwise.</summary>
        private static double Signed(double ax, double ay, double bx, double by) => Math.Atan2(ax * by - ay * bx, ax * bx + ay * by);

        private static GeoVector2 Unit(double x, double y)
        {
            double length = Math.Sqrt(x * x + y * y);
            return length > 0.0 ? new GeoVector2(x / length, y / length) : new GeoVector2(0.0, 0.0);
        }
    }
}
