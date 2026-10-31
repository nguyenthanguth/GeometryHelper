using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    /// <summary>
    /// The shortest segment joining a segment or a straight chain to a plane, a polygon, a triangle or another chain.
    /// </summary>
    /// <remarks>
    /// <para>
    /// These are the pairs <see cref="Distance3"/> could measure and could not draw. Each is built from the joining
    /// lines already here: segment to segment, segment to triangle, and point to shape. The segment leaves the shape
    /// named first and lands on the other, both ends lie on their shapes, and it is as long as the distance. A segment
    /// to a polygon and the chain to a segment or to another chain are where those distances are now read from.
    /// </para>
    /// <para>
    /// A polygon is read as a filled surface, as its distance reads it, so a chain passing through one meets it in a
    /// segment of no length at the crossing, and a chain ending above it is joined straight down. A chain that comes at
    /// a polygon or a triangle end-on and stops within the point tolerance of it is read as crossing it, as the segment
    /// to a triangle always has: the segment of no length then sits at that end, up to the tolerance off the surface, while
    /// <see cref="Distance3"/> reads the gap, which is exact: the segment from (5, 5, 0.0004) to (5, 5, 9) over the square 100 by
    /// 100 in the floor is joined by a segment of no length at (5, 5, 0.0004), and is 0.0004 from it by its distance. One lying
    /// level within the tolerance is joined by its true gap, which is its distance too.
    /// </para>
    /// <para>
    /// <b>A plane</b> is endless, so a shape that crosses or touches it is joined by a segment of no length at the
    /// first point of contact in the shape's own order: along a segment from its start, and edge by edge along a chain.
    /// A shape clear of the plane is joined from its nearest point, which is a vertex, and where several are equally
    /// near as the arithmetic reads them, from the first of them. A shape lying parallel to a plane that is not level
    /// has heights that differ in their last digits, so it may be joined from any of its nearest vertices; the length is
    /// the same. The segment is never snapped: a
    /// chain standing 0.0004 above the plane is joined by a segment 0.0004 long, though its distance, read within the
    /// point tolerance, is nought.
    /// </para>
    /// <para>
    /// Where two candidates are equally short, the one met first stands: the edges of a chain in order, and for two
    /// chains, each edge of the first against every edge of the second in turn.
    /// </para>
    /// </remarks>
    public static partial class Projection3
    {
        #region Segments

        /// <summary>
        /// Gets the shortest segment joining a line segment to a plane.
        /// </summary>
        public static GeoLine3 GetShortestLineTo(GeoLine3 line, GeoPlane3 plane) => GetShortestLineTo(line, plane, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment joining a line segment to a plane.
        /// </summary>
        /// <param name="line">The segment the answer leaves.</param>
        /// <param name="plane">The plane the answer lands on.</param>
        /// <param name="tolerance">
        /// The tolerance; not read, since <see cref="Distance3.DistanceTo(GeoPlane3, GeoLine3)"/> reads the segment against
        /// the plane exactly.
        /// </param>
        /// <returns>
        /// A segment of no length where the segment crosses or touches the plane: at its start when that lies on the plane,
        /// otherwise where it passes through. Clear of the plane, the perpendicular from the nearer end, the start where
        /// the two are equally near. The segment from (0, 0, 10) to (0, 0, -10) meets the XY plane at the origin; the
        /// segment from (0, 0, 10) to (50, 0, 30) is joined from its start to the origin, 10 long, as
        /// <see cref="Distance3.DistanceTo(GeoPlane3, GeoLine3)"/> measures it.
        /// </returns>
        public static GeoLine3 GetShortestLineTo(GeoLine3 line, GeoPlane3 plane, Tolerance tolerance)
        {
            double start = plane.SignedDistanceTo(line.StartPoint);
            double end = plane.SignedDistanceTo(line.EndPoint);

            if (Distance3.DistanceTo(plane, line) <= 0.0)
            {
                GeoPoint3 at = FirstContact(line.StartPoint, line.EndPoint, start, end);

                return new GeoLine3(at, at);
            }

            GeoPoint3 near = Math.Abs(end) < Math.Abs(start) ? line.EndPoint : line.StartPoint;

            return new GeoLine3(near, ProjectToPlane(plane, near));
        }

        /// <summary>
        /// Gets the shortest segment joining a line segment to a polygon.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public static GeoLine3 GetShortestLineTo(GeoLine3 line, GeoPolygon3 polygon) => GetShortestLineTo(line, polygon, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment joining a line segment to a polygon, within a tolerance.
        /// </summary>
        /// <param name="line">The segment the answer leaves.</param>
        /// <param name="polygon">The polygon the answer lands on, read as a filled surface.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// A segment of no length at the crossing where the segment passes through the polygon or touches it; otherwise
        /// the shortest of the segments to each edge in turn and from each end down to the surface.
        /// <see cref="Distance3.DistanceTo(GeoLine3, GeoPolygon3, Tolerance)"/> is its length. Over the square 100 by 100
        /// on the XY plane with a corner at the origin, the segment from (20, 20, 30) to (80, 80, 60) is joined straight
        /// down from its start, 30 long.
        /// </returns>
        /// <remarks>
        /// As for a triangle: a segment passing clean through the surface meets none of its edges, so the crossing is
        /// looked for first; after that the nearest pair is either between the segment and an edge, or under one of the
        /// segment's ends. A segment no longer than the point tolerance is read as its start where it does not truly meet
        /// the polygon: at a thousandth, the one from (5, 5, 0.0015) down to (5, 5, 0.0006) is joined to the square from
        /// its start, by a segment 0.0015 long, and the one from (5, 5, 0.0004) to (5, 5, -0.0005), passing through it,
        /// by a segment of no length at (5, 5, 0).
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public static GeoLine3 GetShortestLineTo(GeoLine3 line, GeoPolygon3 polygon, Tolerance tolerance)
        {
            if (polygon == null)
            {
                throw new ArgumentNullException(nameof(polygon));
            }

            if (Stub3.ReadsAsStart(line, tolerance))
            {
                return Stub3.Nearest(
                    line,
                    Closest(line, polygon, Stub3.AsItIs(line, tolerance), Stub3.AsItIs(line, tolerance)),
                    new GeoLine3(line.StartPoint, ProjectToPolygon(polygon, line.StartPoint, tolerance, Exactly)));
            }

            return Closest(line, polygon, tolerance, Exactly);
        }

        /// <summary>
        /// Gets the shortest segment joining a segment to a polygon by the search a side of a shape is joined by, the segment
        /// not read as one you give.
        /// </summary>
        /// <param name="line">The segment.</param>
        /// <param name="polygon">The polygon.</param>
        /// <param name="tolerance">The tolerance the crossing and the feet on the surface are read within.</param>
        /// <param name="sides">The tolerance the segment is joined to each side within: nought, so a side no longer than the point
        /// tolerance is measured as the side it is.</param>
        internal static GeoLine3 Closest(GeoLine3 line, GeoPolygon3 polygon, Tolerance tolerance, Tolerance sides)
        {
            if (Intersection3.TryCross(line, polygon, out GeoPoint3 through, tolerance))
            {
                return new GeoLine3(through, through);
            }

            GeoLine3 best = Closest(line, polygon.GetEdgeAt(0), sides);
            double shortest = best.Length;

            for (int i = 1; i < polygon.EdgeCount; i++)
            {
                Consider(ref best, ref shortest, Closest(line, polygon.GetEdgeAt(i), sides));
            }

            Consider(ref best, ref shortest, new GeoLine3(line.StartPoint, ProjectToPolygon(polygon, line.StartPoint, tolerance, sides)));
            Consider(ref best, ref shortest, new GeoLine3(line.EndPoint, ProjectToPolygon(polygon, line.EndPoint, tolerance, sides)));

            return best;
        }

        #endregion

        #region Chains

        /// <summary>
        /// Gets the shortest segment joining a polyline to a line segment.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the polyline is null.</exception>
        public static GeoLine3 GetShortestLineTo(GeoPolyline3 polyline, GeoLine3 line) => GetShortestLineTo(polyline, line, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment joining a polyline to a line segment, within a tolerance.
        /// </summary>
        /// <param name="polyline">The chain the answer leaves.</param>
        /// <param name="line">The segment the answer lands on.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// The shortest of the segments joining each edge to <paramref name="line"/>, the first edge winning a tie, and of
        /// no length where the two meet. <see cref="Distance3.DistanceTo(GeoPolyline3, GeoLine3, Tolerance)"/> is its
        /// length. The chain (0, 0, 0), (100, 0, 0), (100, 100, 0) is joined to the segment from (60, 50, 30) to
        /// (60, 50, 80) by the one from (100, 50, 0), on its second edge, to (60, 50, 30), 50 long. A segment no longer
        /// than the point tolerance is read as its start where it does not truly meet the chain, and joined where it
        /// crosses an edge where it does; the edges are the sides they are however short.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when the polyline is null.</exception>
        public static GeoLine3 GetShortestLineTo(GeoPolyline3 polyline, GeoLine3 line, Tolerance tolerance)
        {
            if (polyline == null)
            {
                throw new ArgumentNullException(nameof(polyline));
            }

            if (Stub3.ReadsAsStart(line, tolerance))
            {
                GeoLine3 asItIs = Closest(polyline, line, Stub3.AsItIs(line, tolerance));

                // Not meeting it, the segment is its start, joined to the edges as they are.
                return Stub3.Meets(asItIs.Length, line)
                    ? asItIs
                    : Closest(polyline, new GeoLine3(line.StartPoint, line.StartPoint), Exactly);
            }

            return Closest(polyline, line, Exactly);
        }

        /// <summary>
        /// Gets the shortest segment joining a polyline to a segment, edge by edge, a segment no longer than the tolerance read
        /// as its start: at nought, each edge measured as the side it is however short.
        /// </summary>
        private static GeoLine3 Closest(GeoPolyline3 polyline, GeoLine3 line, Tolerance tolerance)
        {
            GeoLine3 best = Closest(polyline.GetEdgeAt(0), line, tolerance);
            double shortest = best.Length;

            for (int i = 1; i < polyline.EdgeCount && shortest > 0.0; i++)
            {
                Consider(ref best, ref shortest, Closest(polyline.GetEdgeAt(i), line, tolerance));
            }

            return best;
        }

        /// <summary>
        /// Gets the shortest segment joining two polylines.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when either polyline is null.</exception>
        public static GeoLine3 GetShortestLineTo(GeoPolyline3 polyline, GeoPolyline3 other) => GetShortestLineTo(polyline, other, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment joining two polylines, within a tolerance.
        /// </summary>
        /// <param name="polyline">The chain the answer leaves.</param>
        /// <param name="other">The chain the answer lands on.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// The shortest of the segments joining each edge of <paramref name="polyline"/> to each edge of
        /// <paramref name="other"/>, of no length where the two meet. A tie goes to the first edge of
        /// <paramref name="polyline"/>, and within it to the first edge of <paramref name="other"/>.
        /// <see cref="Distance3.DistanceTo(GeoPolyline3, GeoPolyline3, Tolerance)"/> is its length. The chain (0, 0, 0),
        /// (100, 0, 0) and the chain (50, 30, 40), (50, 30, 100) are joined from (50, 0, 0) to (50, 30, 40), 50 long.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when either polyline is null.</exception>
        public static GeoLine3 GetShortestLineTo(GeoPolyline3 polyline, GeoPolyline3 other, Tolerance tolerance)
        {
            if (polyline == null)
            {
                throw new ArgumentNullException(nameof(polyline));
            }

            if (other == null)
            {
                throw new ArgumentNullException(nameof(other));
            }

            GeoLine3 best = Closest(polyline.GetEdgeAt(0), other.GetEdgeAt(0), Exactly);
            double shortest = best.Length;

            for (int i = 0; i < polyline.EdgeCount; i++)
            {
                GeoLine3 edge = polyline.GetEdgeAt(i);

                for (int j = i == 0 ? 1 : 0; j < other.EdgeCount; j++)
                {
                    if (shortest <= 0.0)
                    {
                        return best;
                    }

                    Consider(ref best, ref shortest, Closest(edge, other.GetEdgeAt(j), Exactly));
                }
            }

            return best;
        }

        /// <summary>
        /// Gets the shortest segment joining a polyline to a triangle.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the polyline is null.</exception>
        public static GeoLine3 GetShortestLineTo(GeoPolyline3 polyline, GeoTriangle3 triangle) => GetShortestLineTo(polyline, triangle, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment joining a polyline to a triangle, within a tolerance.
        /// </summary>
        /// <param name="polyline">The chain the answer leaves.</param>
        /// <param name="triangle">The triangle the answer lands on, read as a filled surface.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// The shortest of the segments <see cref="GetShortestLineTo(GeoLine3, GeoTriangle3, Tolerance)"/> gives for each
        /// edge, the first edge winning a tie, and of no length where the chain passes through the triangle. It is as long
        /// as <see cref="Distance3.DistanceTo(GeoPolyline3, GeoTriangle3, Tolerance)"/>. The chain (20, 20, 30),
        /// (20, 20, 80) is joined to the triangle (0, 0, 0), (100, 0, 0), (0, 100, 0) from (20, 20, 30) straight down,
        /// 30 long.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when the polyline is null.</exception>
        public static GeoLine3 GetShortestLineTo(GeoPolyline3 polyline, GeoTriangle3 triangle, Tolerance tolerance)
        {
            if (polyline == null)
            {
                throw new ArgumentNullException(nameof(polyline));
            }

            GeoLine3 best = Closest(polyline.GetEdgeAt(0), triangle, tolerance, Exactly);
            double shortest = best.Length;

            for (int i = 1; i < polyline.EdgeCount && shortest > 0.0; i++)
            {
                Consider(ref best, ref shortest, Closest(polyline.GetEdgeAt(i), triangle, tolerance, Exactly));
            }

            return best;
        }

        /// <summary>
        /// Gets the shortest segment joining a polyline to a polygon.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the polyline or the polygon is null.</exception>
        public static GeoLine3 GetShortestLineTo(GeoPolyline3 polyline, GeoPolygon3 polygon) => GetShortestLineTo(polyline, polygon, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment joining a polyline to a polygon, within a tolerance.
        /// </summary>
        /// <param name="polyline">The chain the answer leaves.</param>
        /// <param name="polygon">The polygon the answer lands on, read as a filled surface.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// The shortest of the segments joining each edge to each of the triangles the polygon's surface is cut into, of no
        /// length where the chain passes through the polygon. A tie goes to the first edge, and within it to the first
        /// triangle. It is as long as <see cref="Distance3.DistanceTo(GeoPolyline3, GeoPolygon3, Tolerance)"/>. Over the
        /// square 100 by 100 on the XY plane with a corner at the origin, the chain (50, 50, 30), (50, 50, 80),
        /// (150, 50, 80) is joined from (50, 50, 30) straight down, 30 long.
        /// </returns>
        /// <remarks>
        /// The triangles each lie inside the polygon, as for the distance: the fan a polygon breaks into by default
        /// reaches across the notch of a concave one, and would join a chain passing through the notch to a surface that
        /// is not there. A polygon with a side no longer than the point tolerance, which its triangles lose, is joined as it
        /// is, each edge to the polygon itself, a tie going to the first edge: at a hundredth, the polygon (-7, -7, 0),
        /// (0, 0, 0), (0.008, 0, 0), (7, -7, 0) is joined to the chain (0.006, 0.02, -5), (0.006, 0.02, 5), (3, 3, 5) by a
        /// segment 0.02 long, to its short side, where it was joined 0.0209 away.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the polyline or the polygon is null.</exception>
        public static GeoLine3 GetShortestLineTo(GeoPolyline3 polyline, GeoPolygon3 polygon, Tolerance tolerance)
        {
            if (polyline == null)
            {
                throw new ArgumentNullException(nameof(polyline));
            }

            if (polygon == null)
            {
                throw new ArgumentNullException(nameof(polygon));
            }

            // The triangles lose a side no longer than the tolerance, so the chain is joined to the polygon itself there.
            if (HasShortSide(polygon, tolerance))
            {
                return ClosestBySides(polyline, polygon, tolerance);
            }

            GeoTriangle3[] pieces = new GeoFace3(polygon).TriangulateSurface(tolerance);

            GeoLine3 best = Closest(polyline.GetEdgeAt(0), pieces[0], tolerance);
            double shortest = best.Length;

            for (int i = 0; i < polyline.EdgeCount; i++)
            {
                GeoLine3 edge = polyline.GetEdgeAt(i);

                for (int j = i == 0 ? 1 : 0; j < pieces.Length; j++)
                {
                    if (shortest <= 0.0)
                    {
                        return best;
                    }

                    Consider(ref best, ref shortest, Closest(edge, pieces[j], tolerance));
                }
            }

            return best;
        }

        /// <summary>
        /// Determines whether a polygon has a side no longer than the point tolerance, which its triangles do not keep.
        /// </summary>
        private static bool HasShortSide(GeoPolygon3 polygon, Tolerance tolerance)
        {
            double near = tolerance.EqualPoint * tolerance.EqualPoint;

            for (int i = 0; i < polygon.EdgeCount; i++)
            {
                GeoLine3 side = polygon.GetEdgeAt(i);

                if (side.StartPoint.GetDistanceSquaredTo(side.EndPoint) <= near)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Gets the shortest segment joining a polyline to a polygon read filled, each edge joined to the polygon itself, its
        /// sides the sides they are however short; a tie goes to the first edge.
        /// </summary>
        private static GeoLine3 ClosestBySides(GeoPolyline3 polyline, GeoPolygon3 polygon, Tolerance tolerance)
        {
            GeoLine3 best = Closest(polyline.GetEdgeAt(0), polygon, tolerance, Exactly);
            double shortest = best.Length;

            for (int i = 1; i < polyline.EdgeCount && shortest > 0.0; i++)
            {
                Consider(ref best, ref shortest, Closest(polyline.GetEdgeAt(i), polygon, tolerance, Exactly));
            }

            return best;
        }

        /// <summary>
        /// Gets the shortest segment joining a polyline to a plane.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the polyline is null.</exception>
        public static GeoLine3 GetShortestLineTo(GeoPolyline3 polyline, GeoPlane3 plane) => GetShortestLineTo(polyline, plane, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment joining a polyline to a plane, within a tolerance.
        /// </summary>
        /// <param name="polyline">The chain the answer leaves.</param>
        /// <param name="plane">The plane the answer lands on.</param>
        /// <param name="tolerance">
        /// The tolerance; not read, since the segment is the true one: a vertex a hair off the plane is joined to it by a
        /// segment that short, where <see cref="Distance3.DistanceTo(GeoPolyline3, GeoPlane3, Tolerance)"/> reads nought.
        /// </param>
        /// <returns>
        /// A segment of no length where the chain crosses or touches the plane, at the first point of contact walking the
        /// edges in order: the start of the first edge that reaches the plane when that start lies on it, otherwise where
        /// the edge passes through, otherwise its end. Clear of the plane, the perpendicular from the nearest vertex, the
        /// first of them where several are equally near; beyond the point tolerance its length is
        /// <see cref="Distance3.DistanceTo(GeoPolyline3, GeoPlane3, Tolerance)"/>. The chain (0, 0, 10), (50, 0, 30),
        /// (100, 0, -10) meets the XY plane at (87.5, 0, 0), on its second edge; the chain (0, 0, 10), (50, 0, 30) is
        /// joined from (0, 0, 10) to the origin, 10 long.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when the polyline is null.</exception>
        public static GeoLine3 GetShortestLineTo(GeoPolyline3 polyline, GeoPlane3 plane, Tolerance tolerance)
        {
            if (polyline == null)
            {
                throw new ArgumentNullException(nameof(polyline));
            }

            if (TryReachPlane(polyline.Vertices, false, plane, out GeoPoint3 at))
            {
                return new GeoLine3(at, at);
            }

            GeoPoint3 nearest = polyline.StartPoint;
            double reach = double.MaxValue;

            Nearer(polyline.Vertices, plane, ref nearest, ref reach);

            return new GeoLine3(nearest, ProjectToPlane(plane, nearest));
        }

        #endregion

        #region Reaching a plane

        /// <summary>
        /// Finds the first point of a run of edges, walked in order, that reaches a plane.
        /// </summary>
        /// <param name="corners">The corners, in order.</param>
        /// <param name="closed">Whether an edge runs back from the last corner to the first, as round a loop.</param>
        /// <param name="plane">The plane.</param>
        /// <param name="at">The first point of contact, on the first edge that has one.</param>
        /// <returns>true if an edge crosses or touches the plane; otherwise, false.</returns>
        /// <remarks>
        /// No tolerance widens the plane: an edge reaches it only where its ends do not both stand strictly on one side,
        /// so a segment of no length is never made for a gap the distance merely reads as nought.
        /// </remarks>
        private static bool TryReachPlane(IReadOnlyList<GeoPoint3> corners, bool closed, GeoPlane3 plane, out GeoPoint3 at)
        {
            int edges = closed ? corners.Count : corners.Count - 1;

            for (int i = 0; i < edges; i++)
            {
                GeoPoint3 start = corners[i];
                GeoPoint3 end = corners[(i + 1) % corners.Count];
                double from = plane.SignedDistanceTo(start);
                double to = plane.SignedDistanceTo(end);

                if (Math.Max(from, to) < 0.0 || Math.Min(from, to) > 0.0)
                {
                    continue;
                }

                at = FirstContact(start, end, from, to);
                return true;
            }

            at = default;
            return false;
        }

        /// <summary>
        /// Gets the first point of an edge that reaches a plane, the edge being known to reach it.
        /// </summary>
        /// <param name="start">The start of the edge.</param>
        /// <param name="end">The end of the edge.</param>
        /// <param name="from">The signed height of the start above the plane.</param>
        /// <param name="to">The signed height of the end above the plane.</param>
        /// <returns>
        /// Where the edge passes through when its ends stand on either side; otherwise the end lying on the plane, the
        /// start where both do.
        /// </returns>
        private static GeoPoint3 FirstContact(GeoPoint3 start, GeoPoint3 end, double from, double to)
        {
            if ((from < 0.0 && to > 0.0) || (from > 0.0 && to < 0.0))
            {
                return start.Add(start.GetVectorTo(end).Multiply(from / (from - to)));
            }

            return Math.Abs(to) < Math.Abs(from) ? end : start;
        }

        /// <summary>
        /// Keeps the corner nearest a plane, the first of those equally near.
        /// </summary>
        private static void Nearer(IReadOnlyList<GeoPoint3> corners, GeoPlane3 plane, ref GeoPoint3 nearest, ref double reach)
        {
            foreach (GeoPoint3 corner in corners)
            {
                double height = Math.Abs(plane.SignedDistanceTo(corner));

                if (height < reach)
                {
                    reach = height;
                    nearest = corner;
                }
            }
        }

        #endregion

        /// <summary>
        /// Keeps the shorter of the segment in hand, whose length is <paramref name="shortest"/>, and the one offered.
        /// </summary>
        /// <remarks>
        /// As <see cref="Consider(ref GeoLine3, GeoLine3)"/>, strict so that the earlier of two equally short segments
        /// stands, with the length kept rather than measured again for every candidate. A length that is not a number
        /// is kept and then stays, as <see cref="Math.Min(double, double)"/> carries it: the chain distances read their
        /// length from here, and a chain with a corner that is not a number was not a number away before they did.
        /// </remarks>
        private static void Consider(ref GeoLine3 best, ref double shortest, GeoLine3 candidate)
        {
            double length = candidate.Length;

            if (length < shortest || double.IsNaN(length))
            {
                best = candidate;
                shortest = length;
            }
        }
    }
}
