using System;
using System.Collections.Generic;
using GeometryHelper.Core;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// A vertex of a polyline as the editing of the three polyline classes sees it: its point (in the system of the
    /// polyline's normal for one of the plane, in the world for a 3D one), the bulge and widths of the segment it starts, and
    /// whether it wrote its widths.
    /// </summary>
    internal struct DxfPolyVertex
    {
        internal DxfPolyVertex(double x, double y, double z, double bulge, double startWidth, double endWidth, bool widths)
        {
            X = x;
            Y = y;
            Z = z;
            Bulge = bulge;
            StartWidth = startWidth;
            EndWidth = endWidth;
            Widths = widths;
        }

        internal double X { get; set; }

        internal double Y { get; set; }

        internal double Z { get; set; }

        internal double Bulge { get; set; }

        internal double StartWidth { get; set; }

        internal double EndWidth { get; set; }

        internal bool Widths { get; set; }

        internal GeoPoint2 Point2 => new GeoPoint2(X, Y);
    }

    /// <summary>
    /// The editing the three polyline classes share, on their vertices: the pieces between parameters (the index of the vertex a
    /// segment leaves plus how far along it, in angle on an arc, as <see cref="DxfBulgeChain"/> reads one), the end segments run on
    /// to a parameter or a point, and the chains at a distance. An arc is worked out as <see cref="DxfBulgeChain"/> works one out.
    /// </summary>
    internal static class DxfPolylineEdit
    {
        private const double Turn = 2.0 * Math.PI;

        /// <summary>
        /// The pieces of a polyline cut at parameters (kept apart and inside, in increasing order, as
        /// <see cref="DxfCurveTools.Cuts"/> gives them): an open one from its start to the first, on to the next and to its
        /// end; a closed one from each to the next and from the last on round its start to the first. None for a closed one
        /// cut at fewer than two places.
        /// </summary>
        internal static List<List<DxfPolyVertex>> Split(List<DxfPolyVertex> v, bool closed, List<double> cuts)
        {
            var pieces = new List<List<DxfPolyVertex>>();
            int segments = closed ? v.Count : v.Count - 1;

            if (closed)
            {
                if (cuts.Count < 2)
                {
                    return pieces;
                }

                for (int i = 0; i < cuts.Count; i++)
                {
                    pieces.Add(Piece(v, cuts[i], i + 1 < cuts.Count ? cuts[i + 1] : cuts[0] + segments));
                }

                return pieces;
            }

            double from = 0.0;

            foreach (double cut in cuts)
            {
                pieces.Add(Piece(v, from, cut));
                from = cut;
            }

            pieces.Add(Piece(v, from, segments));
            return pieces;
        }

        /// <summary>
        /// The piece of a polyline from one parameter to a greater one, which may run past the count of the vertices round a
        /// closed polyline: a vertex at each end, those between kept as they are, the end segments cut, their bulges those of
        /// the parts of the arcs and their widths read off along them.
        /// </summary>
        internal static List<DxfPolyVertex> Piece(List<DxfPolyVertex> v, double s, double e)
        {
            int n = v.Count;
            int i0 = (int)Math.Floor(s);
            double f0 = s - i0;
            int i1 = (int)Math.Floor(e);
            double f1 = e - i1;

            if (f1 == 0.0 && i1 > i0)
            {
                i1--;
                f1 = 1.0;
            }

            var piece = new List<DxfPolyVertex>();

            for (int j = i0; j <= i1; j++)
            {
                DxfPolyVertex a = v[j % n];
                DxfPolyVertex b = v[(j + 1) % n];
                double from = j == i0 ? f0 : 0.0;
                double to = j == i1 ? f1 : 1.0;

                if (from == 0.0 && to == 1.0)
                {
                    piece.Add(a);
                    continue;
                }

                DxfPolyVertex start = from == 0.0 ? a : At(a, b, from);
                start.Bulge = Part(a.Bulge, from, to);
                start.StartWidth = Width(a, from);
                start.EndWidth = Width(a, to);
                start.Widths = a.Widths;
                piece.Add(start);
            }

            DxfPolyVertex last = v[i1 % n];
            DxfPolyVertex next = v[(i1 + 1) % n];
            DxfPolyVertex end = f1 == 1.0 ? next : At(last, next, f1);
            double width = Width(last, f1);
            end.Bulge = 0.0;
            end.StartWidth = f1 == 1.0 ? next.StartWidth : width;
            end.EndWidth = f1 == 1.0 ? next.EndWidth : width;
            end.Widths = f1 == 1.0 ? next.Widths : last.Widths;
            piece.Add(end);
            return piece;
        }

        /// <summary>
        /// Runs the end segment of an open polyline on to a parameter past an end, as AutoCAD's <c>Extend</c> does: the last
        /// segment to the count of segments less one plus the share along it (above 1), the first back to a share below
        /// nought; a line along itself, an arc round its circle, its widths carried on along it.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the end segment has no length to give it a direction.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when an arc would be run round a whole turn or more.</exception>
        internal static void Extend(List<DxfPolyVertex> v, double parameter, string name)
            => Extend(v, parameter > 0.0, parameter > 0.0 ? parameter - (v.Count - 2) : parameter, name);

        /// <summary>
        /// Moves an end of an open polyline to a share along its end segment run on: the end to a share of the last segment
        /// (above 1 runs it on, below shortens it), the start to a share of the first (below nought runs it back).
        /// </summary>
        internal static void Extend(List<DxfPolyVertex> v, bool atEnd, double share, string name)
        {
            int n = v.Count;
            int i = atEnd ? n - 2 : 0;
            DxfPolyVertex a = v[i];
            DxfPolyVertex b = v[i + 1];
            Reach(a, b, share, name);

            if (atEnd)
            {
                DxfPolyVertex moved = At(a, b, share);
                moved.Bulge = b.Bulge;
                moved.StartWidth = b.StartWidth;
                moved.EndWidth = b.EndWidth;
                moved.Widths = b.Widths;
                a.Bulge = Part(a.Bulge, 0.0, share);
                a.EndWidth = Math.Max(0.0, Width(a, share));
                v[i] = a;
                v[i + 1] = moved;
            }
            else
            {
                DxfPolyVertex moved = At(a, b, share);
                moved.Bulge = Part(a.Bulge, share, 1.0);
                moved.StartWidth = Math.Max(0.0, Width(a, share));
                moved.EndWidth = a.EndWidth;
                moved.Widths = a.Widths;
                v[0] = moved;
            }
        }

        /// <summary>
        /// The share along the end segment of an open polyline of a point on the segment run on: on the line through a line
        /// segment, on the circle of an arc round from its start the way it runs, a share above nought; NaN where the point is
        /// off it by more than the point tolerance.
        /// </summary>
        internal static double ShareOf(List<DxfPolyVertex> v, bool atStart, double x, double y, double z, bool plane)
        {
            int n = v.Count;
            int i = atStart ? 0 : n - 2;
            DxfPolyVertex a = v[i];
            DxfPolyVertex b = v[i + 1];
            double slack = Tolerance.Default.EqualPoint;

            if (!IsArc(a, b))
            {
                double dx = b.X - a.X, dy = b.Y - a.Y, dz = plane ? 0.0 : b.Z - a.Z;
                double ll = dx * dx + dy * dy + dz * dz;

                if (!(ll > 0.0))
                {
                    return double.NaN;
                }

                double qz = plane ? 0.0 : z - a.Z;
                double t = ((x - a.X) * dx + (y - a.Y) * dy + qz * dz) / ll;
                double gx = a.X + dx * t - x, gy = a.Y + dy * t - y, gz = plane ? 0.0 : a.Z + dz * t - z;
                return Math.Sqrt(gx * gx + gy * gy + gz * gz) <= slack ? t : double.NaN;
            }

            Circle(a, b, out double cx, out double cy, out double r, out double a0, out double theta);
            double rx = x - cx, ry = y - cy;

            if (!(Math.Abs(Math.Sqrt(rx * rx + ry * ry) - r) <= slack))
            {
                return double.NaN;
            }

            double angle = Math.Atan2(ry, rx);
            double delta = theta > 0.0 ? angle - a0 : a0 - angle;
            delta -= Turn * Math.Floor(delta / Turn);

            // On the arc the share is how far round it; past its start, how far back round the circle from there.
            return atStart && delta > Math.Abs(theta) ? -(Turn - delta) / Math.Abs(theta) : delta / Math.Abs(theta);
        }

        /// <summary>
        /// Refuses a share along an end segment that does not run past the end it is to extend: above 1 for the end, below
        /// nought for the start, as a spline's end is run on only past itself.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when the point is on the segment itself.</exception>
        internal static void Beyond(double share, bool atStart, GeoPoint3 toPoint, string name)
        {
            if (atStart ? !(share < 0.0) : !(share > 1.0))
            {
                throw new ArgumentException("The point " + toPoint + " is on the polyline itself, not past its " + (atStart ? "start" : "end") + ": an end is run on, not cut back.", name);
            }
        }

        /// <summary>The point at a share along the segment from one vertex to the next, its height that of the first on an arc.</summary>
        internal static DxfPolyVertex At(DxfPolyVertex a, DxfPolyVertex b, double f)
        {
            if (!IsArc(a, b))
            {
                return new DxfPolyVertex(a.X + (b.X - a.X) * f, a.Y + (b.Y - a.Y) * f, a.Z + (b.Z - a.Z) * f, 0.0, 0.0, 0.0, false);
            }

            Circle(a, b, out double cx, out double cy, out double r, out double a0, out double theta);
            double angle = a0 + theta * f;
            return new DxfPolyVertex(cx + Math.Cos(angle) * r, cy + Math.Sin(angle) * r, a.Z, 0.0, 0.0, 0.0, false);
        }

        /// <summary>
        /// The chains at a distance from a polyline of the plane, to the right of its way seen from the tip of its normal where
        /// the distance is above nought, as the library's arc offsets give them with corners mitred as far as they reach (as
        /// AutoCAD's OFFSETGAPTYPE 0 extends them): a closed polyline gives the loops of <see cref="Offset2.Offset(GeoPolygonArc2, double, OffsetOptions, Tolerance)"/>
        /// and an open one the chains of <see cref="Offset2.Offset(GeoPolylineArc2, double, OffsetOptions, Tolerance)"/>.
        /// </summary>
        internal static List<KeyValuePair<List<DxfPolyVertex>, bool>> Offset(List<DxfPolyVertex> v, bool closed, double distance, double z)
        {
            var options = new OffsetOptions(OffsetJoin.Miter, double.PositiveInfinity);
            var results = new List<KeyValuePair<List<DxfPolyVertex>, bool>>();

            if (closed)
            {
                List<DxfPolyVertex> loop = v;

                // A loop of two vertices, two arcs as a circle is drawn, is cut at the middle of each segment for the loop of three the library takes.
                if (loop.Count < 3)
                {
                    loop = new List<DxfPolyVertex>();

                    for (int i = 0; i < v.Count; i++)
                    {
                        List<DxfPolyVertex> half = Piece(v, i, i + 0.5);
                        List<DxfPolyVertex> other = Piece(v, i + 0.5, i + 1);
                        loop.Add(half[0]);
                        loop.Add(other[0]);
                    }
                }

                var polygon = new GeoPolygonArc2(Points(loop), Bulges(loop));
                double sideways = polygon.IsClockwise ? -distance : distance;

                foreach (GeoPolygonArc2 one in Offset2.Offset(polygon, sideways, options, Tolerance.Default))
                {
                    results.Add(new KeyValuePair<List<DxfPolyVertex>, bool>(Vertices(one.Vertices, one.Bulges, z), true));
                }

                return results;
            }

            var chain = new GeoPolylineArc2(Points(v), Bulges(v));

            foreach (GeoPolylineArc2 one in Offset2.Offset(chain, -distance, options, Tolerance.Default))
            {
                results.Add(new KeyValuePair<List<DxfPolyVertex>, bool>(Vertices(one.Vertices, one.Bulges, z), false));
            }

            return results;
        }

        /// <summary>
        /// The vertices of a chain with its arcs given as chords, each arc cut into as many as keep within the point tolerance
        /// of it, for a 3D polyline, which has no arcs.
        /// </summary>
        internal static List<DxfPolyVertex> Straightened(List<DxfPolyVertex> v, bool closed)
        {
            var straight = new List<DxfPolyVertex>();
            int segments = closed ? v.Count : v.Count - 1;

            for (int i = 0; i < v.Count; i++)
            {
                DxfPolyVertex a = v[i];
                a.Bulge = 0.0;
                straight.Add(a);

                if (i >= segments || !IsArc(v[i], v[(i + 1) % v.Count]))
                {
                    continue;
                }

                Circle(v[i], v[(i + 1) % v.Count], out _, out _, out double r, out _, out double theta);
                double step = 2.0 * Math.Acos(Math.Max(-1.0, 1.0 - Tolerance.Default.EqualPoint / r));
                int count = Math.Max(1, Math.Min(4096, (int)Math.Ceiling(Math.Abs(theta) / Math.Max(step, 1E-6))));

                for (int k = 1; k < count; k++)
                {
                    DxfPolyVertex mid = At(v[i], v[(i + 1) % v.Count], (double)k / count);
                    straight.Add(mid);
                }
            }

            return straight;
        }

        /// <summary>Whether the segment from one vertex to the next is an arc: a bulge, and ends apart.</summary>
        internal static bool IsArc(DxfPolyVertex a, DxfPolyVertex b)
            => Math.Abs(a.Bulge) >= 1E-300 && (b.X - a.X != 0.0 || b.Y - a.Y != 0.0);

        /// <summary>The circle of the arc from one vertex to the next, as <see cref="DxfBulgeChain"/> works it out.</summary>
        internal static void Circle(DxfPolyVertex a, DxfPolyVertex b, out double cx, out double cy, out double r, out double a0, out double theta)
        {
            double dx = b.X - a.X;
            double dy = b.Y - a.Y;
            double c = Math.Sqrt(dx * dx + dy * dy);
            theta = 4.0 * Math.Atan(a.Bulge);
            r = c / (2.0 * Math.Sin(Math.Abs(theta) / 2.0));
            double h = c / 2.0 / Math.Tan(theta / 2.0);
            cx = (a.X + b.X) * 0.5 + -dy / c * h;
            cy = (a.Y + b.Y) * 0.5 + dx / c * h;
            a0 = Math.Atan2(a.Y - cy, a.X - cx);
        }

        /// <summary>The bulge of the part of an arc of a bulge from one share of it to another.</summary>
        internal static double Part(double bulge, double from, double to)
            => bulge == 0.0 ? 0.0 : Math.Tan(4.0 * Math.Atan(bulge) * (to - from) / 4.0);

        /// <summary>The width at a share along the segment a vertex starts, read straight from its start width to its end width.</summary>
        internal static double Width(DxfPolyVertex a, double f) => a.StartWidth + (a.EndWidth - a.StartWidth) * f;

        /// <summary>Refuses a share that runs a segment of no length, or an arc round a whole turn or more.</summary>
        private static void Reach(DxfPolyVertex a, DxfPolyVertex b, double share, string name)
        {
            double dx = b.X - a.X, dy = b.Y - a.Y, dz = b.Z - a.Z;

            if (!(dx * dx + dy * dy + dz * dz > 0.0))
            {
                throw new InvalidOperationException("The end segment of the polyline has no length to give it a direction to be extended in.");
            }

            if (IsArc(a, b) && Math.Abs(4.0 * Math.Atan(a.Bulge) * (share >= 0.0 ? share : 1.0 - share)) >= Turn)
            {
                throw new ArgumentOutOfRangeException(name, share, "The parameter would take the end arc round a whole turn or more.");
            }
        }

        private static List<GeoPoint2> Points(List<DxfPolyVertex> v)
        {
            var points = new List<GeoPoint2>(v.Count);

            foreach (DxfPolyVertex q in v)
            {
                points.Add(q.Point2);
            }

            return points;
        }

        private static List<double> Bulges(List<DxfPolyVertex> v)
        {
            var bulges = new List<double>(v.Count);

            foreach (DxfPolyVertex q in v)
            {
                bulges.Add(q.Bulge);
            }

            return bulges;
        }

        private static List<DxfPolyVertex> Vertices(IReadOnlyList<GeoPoint2> points, IReadOnlyList<double> bulges, double z)
        {
            var vertices = new List<DxfPolyVertex>(points.Count);

            for (int i = 0; i < points.Count; i++)
            {
                vertices.Add(new DxfPolyVertex(points[i].X, points[i].Y, z, i < bulges.Count ? bulges[i] : 0.0, 0.0, 0.0, false));
            }

            return vertices;
        }
    }
}
