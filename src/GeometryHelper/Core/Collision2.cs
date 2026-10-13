using System;
using System.Collections.Generic;
using GeometryHelper;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    /// <summary>
    /// Provides static calculation methods for checking spatial collisions and geometric overlaps.
    /// </summary>
    public static partial class Collision2
    {
        #region Line - Line

        /// <summary>
        /// Checks whether two line segments collide / intersect using default tolerance.
        /// </summary>
        public static bool CollidesWith(GeoLine2 line1, GeoLine2 line2)
        {
            return CollidesWith(line1, line2, Tolerance.Global);
        }

        /// <summary>
        /// Checks whether two line segments collide / intersect within tolerance.
        /// </summary>
        /// <remarks>
        /// They collide exactly where their distance is at most the point tolerance: where they truly cross, or where an
        /// end of one comes that near the other. A crossing of their lines past the ends does not count, as it does for
        /// <see cref="Intersection2.TryIntersectWith(GeoLine2, GeoLine2, out GeoPoint2, Tolerance)"/>, which places points
        /// rather than testing for contact. The segment from the origin to (10, 0) and the one leaving (10.00135, 0.00078) at
        /// 60 degrees, 0.00156 apart at their corners, do not collide at a tolerance of a thousandth and do at two.
        /// </remarks>
        public static bool CollidesWith(GeoLine2 line1, GeoLine2 line2, Tolerance tolerance)
        {
            return Distance2.DistanceTo(line1, line2, tolerance) <= tolerance.EqualPoint;
        }

        #endregion

        #region Circle - Shapes

        /// <summary>
        /// Checks whether two circles collide or overlap using default tolerance.
        /// </summary>
        public static bool CollidesWith(GeoCircle2 c1, GeoCircle2 c2)
        {
            return CollidesWith(c1, c2, Tolerance.Global);
        }

        /// <summary>
        /// Checks whether two circles collide or overlap within tolerance.
        /// </summary>
        public static bool CollidesWith(GeoCircle2 c1, GeoCircle2 c2, Tolerance tolerance)
        {
            return Distance2.DistanceTo(c1.Center, c2.Center) <= c1.Radius + c2.Radius + tolerance.EqualPoint;
        }

        /// <summary>
        /// Checks whether a circle collides with a line segment using default tolerance.
        /// </summary>
        public static bool CollidesWith(GeoCircle2 circle, GeoLine2 line)
        {
            return CollidesWith(circle, line, Tolerance.Global);
        }

        /// <summary>
        /// Checks whether a circle collides with a line segment within tolerance.
        /// </summary>
        public static bool CollidesWith(GeoCircle2 circle, GeoLine2 line, Tolerance tolerance)
        {
            return Distance2.DistanceTo(line, circle.Center, tolerance) <= circle.Radius + tolerance.EqualPoint;
        }

        /// <summary>
        /// Checks whether a circle collides with a rotated rectangle using default tolerance.
        /// </summary>
        public static bool CollidesWith(GeoCircle2 circle, GeoRectangle2 rect)
        {
            return CollidesWith(circle, rect, Tolerance.Global);
        }

        /// <summary>
        /// Checks whether a circle collides with a rotated rectangle within tolerance.
        /// </summary>
        public static bool CollidesWith(GeoCircle2 circle, GeoRectangle2 rect, Tolerance tolerance)
        {
            if (Containment2.Contains(rect, circle.Center, tolerance))
            {
                return true;
            }
            return Distance2.DistanceTo(rect, circle.Center) <= circle.Radius + tolerance.EqualPoint;
        }

        /// <summary>
        /// Checks whether a circle collides with a polygon using default tolerance.
        /// </summary>
        public static bool CollidesWith(GeoCircle2 circle, GeoPolygon2 poly)
        {
            return CollidesWith(circle, poly, Tolerance.Global);
        }

        /// <summary>
        /// Checks whether a circle collides with a polygon within tolerance.
        /// </summary>
        public static bool CollidesWith(GeoCircle2 circle, GeoPolygon2 poly, Tolerance tolerance)
        {
            if (poly == null) throw new ArgumentNullException(nameof(poly));

            if (Containment2.Contains(poly, circle.Center, tolerance))
            {
                return true;
            }
            return Distance2.DistanceTo(poly, circle.Center, tolerance) <= circle.Radius + tolerance.EqualPoint;
        }

        /// <summary>
        /// Checks whether a circle collides with a polyline using default tolerance.
        /// </summary>
        public static bool CollidesWith(GeoCircle2 circle, GeoPolyline2 polyline)
        {
            return CollidesWith(circle, polyline, Tolerance.Global);
        }

        /// <summary>
        /// Checks whether a circle collides with a polyline within tolerance.
        /// </summary>
        public static bool CollidesWith(GeoCircle2 circle, GeoPolyline2 polyline, Tolerance tolerance)
        {
            if (polyline == null) throw new ArgumentNullException(nameof(polyline));

            for (int i = 0; i < polyline.EdgeCount; i++)
            {
                if (CollidesWith(circle, polyline.GetEdgeAt(i), tolerance))
                {
                    return true;
                }
            }

            // A last check for a centre sitting exactly on the chain.
            return Containment2.IsPointOn(polyline, circle.Center, tolerance);
        }

        #endregion

        #region Rectangle - Shapes (SAT & Bounding)

        /// <summary>
        /// Checks whether two rotated rectangles (GeoRectangle2 OBB) collide using default tolerance.
        /// </summary>
        public static bool CollidesWith(GeoRectangle2 rect1, GeoRectangle2 rect2)
        {
            return CollidesWith(rect1, rect2, Tolerance.Global);
        }

        /// <summary>
        /// Checks whether two rotated rectangles (GeoRectangle2 OBB) collide using the Separating Axis Theorem (SAT).
        /// </summary>
        public static bool CollidesWith(GeoRectangle2 rect1, GeoRectangle2 rect2, Tolerance tolerance)
        {
            GeoPoint2[] r1 = rect1.GetVertices();
            GeoPoint2[] r2 = rect2.GetVertices();

            GeoVector2[] edgeDirections =
            {
                r1[0].GetVectorTo(r1[1]),
                r1[0].GetVectorTo(r1[3]),
                r2[0].GetVectorTo(r2[1]),
                r2[0].GetVectorTo(r2[3])
            };

            foreach (var edgeDirection in edgeDirections)
            {
                // A rectangle with zero width or height has a degenerate edge. TryGetNormal skips that
                // axis instead of throwing; the remaining axes still separate the two boxes correctly.
                if (!edgeDirection.TryGetNormal(out GeoVector2 axis, tolerance)) continue;

                double min1 = double.MaxValue;
                double max1 = double.MinValue;
                foreach (var p in r1)
                {
                    double proj = p.X * axis.X + p.Y * axis.Y;
                    if (proj < min1) min1 = proj;
                    if (proj > max1) max1 = proj;
                }

                double min2 = double.MaxValue;
                double max2 = double.MinValue;
                foreach (var p in r2)
                {
                    double proj = p.X * axis.X + p.Y * axis.Y;
                    if (proj < min2) min2 = proj;
                    if (proj > max2) max2 = proj;
                }

                if (min2 - max1 > tolerance.EqualPoint || min1 - max2 > tolerance.EqualPoint)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Checks whether a rectangle collides with a line segment using default tolerance.
        /// </summary>
        public static bool CollidesWith(GeoRectangle2 rect, GeoLine2 line)
        {
            return CollidesWith(rect, line, Tolerance.Global);
        }

        /// <summary>
        /// Checks whether a rectangle collides with a line segment within tolerance.
        /// </summary>
        /// <remarks>
        /// They collide where they cross, where one holds the other, or where they come within the point tolerance of
        /// each other, edge against edge with no slack past the ends. At a tolerance of a thousandth, the segment from
        /// (40, 100.01) to (60, 100.0005) collides with the square from the origin to (100, 100), ending half a
        /// thousandth above its top, and one ending two thousandths above does not.
        /// </remarks>
        public static bool CollidesWith(GeoRectangle2 rect, GeoLine2 line, Tolerance tolerance)
        {
            GeoLine2[] edges = rect.GetEdges();

            return CrossesOrStartsIn(rect, edges, line, tolerance) || ComeWithin(edges, line, tolerance);
        }

        /// <summary>
        /// Whether a segment crosses a rectangle or starts inside it: where the distance between them is taken as
        /// nought without measuring.
        /// </summary>
        internal static bool CrossesOrStartsIn(GeoRectangle2 rect, GeoLine2 line, Tolerance tolerance)
            => CrossesOrStartsIn(rect, rect.GetEdges(), line, tolerance);

        /// <summary>
        /// Whether a segment crosses a rectangle, given as it is and as its edges, or starts inside it.
        /// </summary>
        private static bool CrossesOrStartsIn(GeoRectangle2 rect, GeoLine2[] edges, GeoLine2 line, Tolerance tolerance)
        {
            foreach (var edge in edges)
            {
                if (Crosses(line, edge, tolerance))
                {
                    return true;
                }
            }

            if (Containment2.Contains(rect, line.StartPoint, tolerance))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Checks whether a rectangle collides with a polygon using default tolerance.
        /// </summary>
        public static bool CollidesWith(GeoRectangle2 rect, GeoPolygon2 poly)
        {
            return CollidesWith(rect, poly, Tolerance.Global);
        }

        /// <summary>
        /// Checks whether a rectangle collides with a polygon within tolerance.
        /// </summary>
        /// <remarks>
        /// They collide where they cross, where one holds the other, or where they come within the point tolerance of
        /// each other, edge against edge with no slack past the ends. At a tolerance of a thousandth, the segment from
        /// (40, 100.01) to (60, 100.0005) collides with the square from the origin to (100, 100), ending half a
        /// thousandth above its top, and one ending two thousandths above does not.
        /// </remarks>
        public static bool CollidesWith(GeoRectangle2 rect, GeoPolygon2 poly, Tolerance tolerance)
        {
            if (poly == null) throw new ArgumentNullException(nameof(poly));

            GeoLine2[] rectEdges = rect.GetEdges();
            GeoLine2[] polyEdges = EdgesOf(poly);

            return CrossesOrStartsIn(rect, rectEdges, poly, polyEdges, tolerance) || ComeWithin(rectEdges, polyEdges, tolerance);
        }

        /// <summary>
        /// Whether a rectangle and a polygon cross or one starts inside the other: where the distance between them is
        /// taken as nought without measuring.
        /// </summary>
        internal static bool CrossesOrStartsIn(GeoRectangle2 rect, GeoPolygon2 poly, Tolerance tolerance)
            => CrossesOrStartsIn(rect, rect.GetEdges(), poly, EdgesOf(poly), tolerance);

        /// <summary>
        /// Whether a rectangle and a polygon, each given with its edges, cross or one starts inside the other.
        /// </summary>
        private static bool CrossesOrStartsIn(GeoRectangle2 rect, GeoLine2[] rectEdges, GeoPolygon2 poly, GeoLine2[] polyEdges, Tolerance tolerance)
        {
            foreach (var polyEdge in polyEdges)
            {
                foreach (var rectEdge in rectEdges)
                {
                    if (Crosses(polyEdge, rectEdge, tolerance))
                    {
                        return true;
                    }
                }
            }

            if (Containment2.Contains(rect, poly.Vertices[0], tolerance))
            {
                return true;
            }

            if (Containment2.Contains(poly, rect.Center, tolerance))
            {
                return true;
            }

            return false;
        }

        #endregion

        #region Polygon - Shapes

        /// <summary>
        /// Checks whether a polygon collides with a line segment using default tolerance.
        /// </summary>
        public static bool CollidesWith(GeoPolygon2 poly, GeoLine2 line)
        {
            return CollidesWith(poly, line, Tolerance.Global);
        }

        /// <summary>
        /// Checks whether a polygon collides with a line segment within tolerance.
        /// </summary>
        /// <remarks>
        /// They collide where they cross, where one holds the other, or where they come within the point tolerance of
        /// each other, edge against edge with no slack past the ends. At a tolerance of a thousandth, the segment from
        /// (40, 100.01) to (60, 100.0005) collides with the square from the origin to (100, 100), ending half a
        /// thousandth above its top, and one ending two thousandths above does not.
        /// </remarks>
        public static bool CollidesWith(GeoPolygon2 poly, GeoLine2 line, Tolerance tolerance)
        {
            if (poly == null) throw new ArgumentNullException(nameof(poly));

            GeoLine2[] edges = EdgesOf(poly);

            return CrossesOrStartsIn(poly, edges, line, tolerance, false) || ComeWithin(edges, line, tolerance);
        }

        /// <summary>
        /// Whether a segment crosses a polygon or starts inside it: where the distance between them is taken as nought
        /// without measuring.
        /// </summary>
        internal static bool CrossesOrStartsIn(GeoPolygon2 poly, GeoLine2 line, Tolerance tolerance)
            => CrossesOrStartsIn(poly, EdgesOf(poly), line, tolerance, false);

        /// <summary>
        /// Whether a segment crosses a polygon or starts inside it, a crossing up to the point tolerance past the ends
        /// counted when asked: the reading the shapes of space keep when they lay a polygon out in its plane.
        /// </summary>
        internal static bool CrossesOrStartsIn(GeoPolygon2 poly, GeoLine2 line, Tolerance tolerance, bool pastEnds)
            => CrossesOrStartsIn(poly, EdgesOf(poly), line, tolerance, pastEnds);

        /// <summary>
        /// Whether a segment crosses a polygon, given as it is and as its edges, or starts inside it.
        /// </summary>
        private static bool CrossesOrStartsIn(GeoPolygon2 poly, GeoLine2[] edges, GeoLine2 line, Tolerance tolerance, bool pastEnds)
        {
            foreach (var polyEdge in edges)
            {
                if (Cross(line, polyEdge, tolerance, pastEnds))
                {
                    return true;
                }
            }

            return Containment2.Contains(poly, line.StartPoint, tolerance);
        }

        /// <summary>
        /// Whether two segments cross: truly, at a point on both, or, with <paramref name="pastEnds"/>, where
        /// <see cref="Intersection2.TryIntersectWith(GeoLine2, GeoLine2, out GeoPoint2, Tolerance)"/> places a crossing,
        /// up to the point tolerance past the end of either.
        /// </summary>
        private static bool Cross(GeoLine2 first, GeoLine2 second, Tolerance tolerance, bool pastEnds)
            => pastEnds
                ? Intersection2.TryIntersectWith(first, second, out _, tolerance)
                : Crosses(first, second, tolerance);

        /// <summary>
        /// Whether two segments truly cross, at a point on both, with no slack past their ends: where the distance between
        /// them is nought. A touch at an end counts, and two segments on one line are left to the distances from the ends.
        /// </summary>
        internal static bool Crosses(GeoLine2 first, GeoLine2 second, Tolerance tolerance)
            => Projection2.TryGetCrossingPoint(first, second, tolerance, false, out _);

        /// <summary>
        /// Whether a segment truly crosses any of a run of segments, at a point on both.
        /// </summary>
        internal static bool Crosses(GeoLine2[] edges, GeoLine2 line, Tolerance tolerance)
        {
            for (int i = 0; i < edges.Length; i++)
            {
                if (Crosses(edges[i], line, tolerance))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Whether two runs of segments truly cross anywhere, at a point on both.
        /// </summary>
        internal static bool Crosses(GeoLine2[] first, GeoLine2[] second, Tolerance tolerance)
        {
            for (int i = 0; i < first.Length; i++)
            {
                if (Crosses(second, first[i], tolerance))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Checks whether two polygons collide using default tolerance.
        /// </summary>
        public static bool CollidesWith(GeoPolygon2 poly1, GeoPolygon2 poly2)
        {
            return CollidesWith(poly1, poly2, Tolerance.Global);
        }

        /// <summary>
        /// Checks whether two polygons collide within tolerance.
        /// </summary>
        /// <remarks>
        /// They collide where they cross, where one holds the other, or where they come within the point tolerance of
        /// each other, edge against edge with no slack past the ends. A triangle whose corner stands 0.00156 off the corner
        /// (100, 100) of the square from the origin, its sides leaving at 60 and 31 degrees, does not collide with the square
        /// at a tolerance of a thousandth, though the line of one side crosses the line of the square's top within a
        /// thousandth of both corners; at two thousandths it does.
        /// </remarks>
        public static bool CollidesWith(GeoPolygon2 poly1, GeoPolygon2 poly2, Tolerance tolerance)
        {
            if (poly1 == null) throw new ArgumentNullException(nameof(poly1));
            if (poly2 == null) throw new ArgumentNullException(nameof(poly2));

            GeoLine2[] edges1 = EdgesOf(poly1);
            GeoLine2[] edges2 = EdgesOf(poly2);

            return CrossesOrStartsIn(poly1, edges1, poly2, edges2, tolerance, false) || ComeWithin(edges1, edges2, tolerance);
        }

        /// <summary>
        /// Whether two polygons cross or one starts inside the other: where the distance between them is taken as
        /// nought without measuring.
        /// </summary>
        internal static bool CrossesOrStartsIn(GeoPolygon2 poly1, GeoPolygon2 poly2, Tolerance tolerance)
            => CrossesOrStartsIn(poly1, EdgesOf(poly1), poly2, EdgesOf(poly2), tolerance, false);

        /// <summary>
        /// Whether two polygons cross or one starts inside the other, a crossing up to the point tolerance past the ends
        /// of two edges counted when asked: the reading the shapes of space keep when they lay polygons out in a plane.
        /// </summary>
        internal static bool CrossesOrStartsIn(GeoPolygon2 poly1, GeoPolygon2 poly2, Tolerance tolerance, bool pastEnds)
            => CrossesOrStartsIn(poly1, EdgesOf(poly1), poly2, EdgesOf(poly2), tolerance, pastEnds);

        /// <summary>
        /// Whether two polygons, each given with its edges, cross or one starts inside the other.
        /// </summary>
        private static bool CrossesOrStartsIn(GeoPolygon2 poly1, GeoLine2[] edges1, GeoPolygon2 poly2, GeoLine2[] edges2, Tolerance tolerance, bool pastEnds)
        {
            foreach (var edge1 in edges1)
            {
                foreach (var edge2 in edges2)
                {
                    if (Cross(edge1, edge2, tolerance, pastEnds))
                    {
                        return true;
                    }
                }
            }

            return Containment2.Contains(poly1, poly2.Vertices[0], tolerance) ||
                   Containment2.Contains(poly2, poly1.Vertices[0], tolerance);
        }

        #endregion

        #region Polyline - Shapes

        /// <summary>
        /// Checks whether a polyline collides with a line segment using default tolerance.
        /// </summary>
        public static bool CollidesWith(GeoPolyline2 polyline, GeoLine2 line)
        {
            return CollidesWith(polyline, line, Tolerance.Global);
        }

        /// <summary>
        /// Checks whether a polyline collides with a line segment within tolerance.
        /// </summary>
        /// <remarks>
        /// They collide where an edge and the segment cross or come within the point tolerance of each other, as two
        /// segments do. The segment from (0, 0) to (21, 0) lying over the polyline from (10, 0) to (20, 0) collides with
        /// it, though the two cross nowhere.
        /// </remarks>
        public static bool CollidesWith(GeoPolyline2 polyline, GeoLine2 line, Tolerance tolerance)
        {
            if (polyline == null) throw new ArgumentNullException(nameof(polyline));

            for (int i = 0; i < polyline.EdgeCount; i++)
            {
                GeoLine2 edge = polyline.GetEdgeAt(i);

                if (!Clear(line, edge, tolerance) && CollidesWith(edge, line, tolerance))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Whether a segment lies more than twice the point tolerance clear of a box, on X or on Y. Then it neither
        /// crosses nor comes within the tolerance of anything in the box: a crossing accepted within the tolerance lies
        /// within it of both shapes.
        /// </summary>
        private static bool Clear(GeoLine2 line, double minX, double minY, double maxX, double maxY, Tolerance tolerance)
        {
            double reach = 2.0 * tolerance.EqualPoint;

            return Math.Min(line.StartPoint.X, line.EndPoint.X) - maxX > reach
                || minX - Math.Max(line.StartPoint.X, line.EndPoint.X) > reach
                || Math.Min(line.StartPoint.Y, line.EndPoint.Y) - maxY > reach
                || minY - Math.Max(line.StartPoint.Y, line.EndPoint.Y) > reach;
        }

        /// <summary>
        /// Whether a segment lies more than twice the point tolerance clear of another segment's box.
        /// </summary>
        private static bool Clear(GeoLine2 line, GeoLine2 other, Tolerance tolerance)
            => Clear(line,
                Math.Min(other.StartPoint.X, other.EndPoint.X), Math.Min(other.StartPoint.Y, other.EndPoint.Y),
                Math.Max(other.StartPoint.X, other.EndPoint.X), Math.Max(other.StartPoint.Y, other.EndPoint.Y),
                tolerance);

        /// <summary>
        /// Whether a segment comes within the point tolerance of another, measured exactly: they meet where they truly
        /// cross, and otherwise the least of the distances from an end of one to the other decides. Nothing past an end
        /// counts, so two corners between one and two tolerances apart do not meet here.
        /// </summary>
        internal static bool Near(GeoLine2 first, GeoLine2 second, Tolerance tolerance)
        {
            if (Clear(first, second, tolerance))
            {
                return false;
            }

            if (CrossExactly(first, second))
            {
                return true;
            }

            double reach = tolerance.EqualPoint;

            return Distance2.DistanceTo(first, second.StartPoint, tolerance) <= reach
                || Distance2.DistanceTo(first, second.EndPoint, tolerance) <= reach
                || Distance2.DistanceTo(second, first.StartPoint, tolerance) <= reach
                || Distance2.DistanceTo(second, first.EndPoint, tolerance) <= reach;
        }

        /// <summary>
        /// Whether two segments cross at a point inside both, each pair of ends lying strictly either side of the
        /// other's line. A touch at an end, or two segments on one line, is left to the distances from the ends.
        /// </summary>
        private static bool CrossExactly(GeoLine2 first, GeoLine2 second)
        {
            double a = Turn(first.StartPoint, first.EndPoint, second.StartPoint);
            double b = Turn(first.StartPoint, first.EndPoint, second.EndPoint);
            double c = Turn(second.StartPoint, second.EndPoint, first.StartPoint);
            double d = Turn(second.StartPoint, second.EndPoint, first.EndPoint);

            return ((a > 0.0 && b < 0.0) || (a < 0.0 && b > 0.0)) && ((c > 0.0 && d < 0.0) || (c < 0.0 && d > 0.0));
        }

        /// <summary>
        /// Which way a path from one point through a second turns to reach a third: positive anticlockwise.
        /// </summary>
        private static double Turn(GeoPoint2 from, GeoPoint2 through, GeoPoint2 to)
            => (through.X - from.X) * (to.Y - from.Y) - (through.Y - from.Y) * (to.X - from.X);

        /// <summary>
        /// Whether a segment comes within the point tolerance of any of a run of segments, measured exactly.
        /// </summary>
        internal static bool ComeWithin(GeoLine2[] edges, GeoLine2 line, Tolerance tolerance)
        {
            for (int i = 0; i < edges.Length; i++)
            {
                if (Near(line, edges[i], tolerance))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Whether two runs of segments come within the point tolerance of each other anywhere, measured exactly.
        /// </summary>
        internal static bool ComeWithin(GeoLine2[] first, GeoLine2[] second, Tolerance tolerance)
        {
            for (int i = 0; i < first.Length; i++)
            {
                for (int j = 0; j < second.Length; j++)
                {
                    if (Near(first[i], second[j], tolerance))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// The edges of a polygon, in order.
        /// </summary>
        private static GeoLine2[] EdgesOf(GeoPolygon2 poly)
        {
            var edges = new GeoLine2[poly.EdgeCount];

            for (int i = 0; i < edges.Length; i++)
            {
                edges[i] = poly.GetEdgeAt(i);
            }

            return edges;
        }

        /// <summary>
        /// The edges of a polyline, in order.
        /// </summary>
        private static GeoLine2[] EdgesOf(GeoPolyline2 polyline)
        {
            var edges = new GeoLine2[polyline.EdgeCount];

            for (int i = 0; i < edges.Length; i++)
            {
                edges[i] = polyline.GetEdgeAt(i);
            }

            return edges;
        }

        /// <summary>
        /// Whether a segment lies more than twice the point tolerance clear of an arc's whole circle: outside the
        /// circle's box, or with both ends that far inside the circle, which holds the whole segment that far inside.
        /// </summary>
        private static bool Clear(GeoLine2 line, GeoArc2 arc, Tolerance tolerance)
        {
            if (Clear(line,
                arc.Center.X - arc.Radius, arc.Center.Y - arc.Radius, arc.Center.X + arc.Radius, arc.Center.Y + arc.Radius,
                tolerance))
            {
                return true;
            }

            double inside = arc.Radius - 2.0 * tolerance.EqualPoint;

            return inside > 0.0
                && Distance2.GetDistanceSquaredTo(arc.Center, line.StartPoint) < inside * inside
                && Distance2.GetDistanceSquaredTo(arc.Center, line.EndPoint) < inside * inside;
        }

        /// <summary>
        /// Checks whether a polyline collides with a rectangle using default tolerance.
        /// </summary>
        public static bool CollidesWith(GeoPolyline2 polyline, GeoRectangle2 rect)
        {
            return CollidesWith(polyline, rect, Tolerance.Global);
        }

        /// <summary>
        /// Checks whether a polyline collides with a rectangle within tolerance.
        /// </summary>
        public static bool CollidesWith(GeoPolyline2 polyline, GeoRectangle2 rect, Tolerance tolerance)
        {
            if (polyline == null) throw new ArgumentNullException(nameof(polyline));

            for (int i = 0; i < polyline.EdgeCount; i++)
            {
                if (CollidesWith(rect, polyline.GetEdgeAt(i), tolerance))
                {
                    return true;
                }
            }

            // Catches a centre sitting exactly on the chain; a chain inside the rectangle is already
            // found by the edge loop, which contain-checks each segment against the rectangle.
            return Containment2.IsPointOn(polyline, rect.Center, tolerance);
        }

        /// <summary>
        /// Whether a polyline crosses a rectangle or starts an edge inside it: where the distance between them is
        /// taken as nought without measuring.
        /// </summary>
        internal static bool CrossesOrStartsIn(GeoPolyline2 polyline, GeoRectangle2 rect, Tolerance tolerance)
        {
            for (int i = 0; i < polyline.EdgeCount; i++)
            {
                if (CrossesOrStartsIn(rect, polyline.GetEdgeAt(i), tolerance))
                {
                    return true;
                }
            }

            return Containment2.IsPointOn(polyline, rect.Center, tolerance);
        }

        /// <summary>
        /// Checks whether a polyline collides with a polygon using default tolerance.
        /// </summary>
        public static bool CollidesWith(GeoPolyline2 polyline, GeoPolygon2 poly)
        {
            return CollidesWith(polyline, poly, Tolerance.Global);
        }

        /// <summary>
        /// Checks whether a polyline collides with a polygon within tolerance.
        /// </summary>
        public static bool CollidesWith(GeoPolyline2 polyline, GeoPolygon2 poly, Tolerance tolerance)
        {
            if (polyline == null) throw new ArgumentNullException(nameof(polyline));
            if (poly == null) throw new ArgumentNullException(nameof(poly));

            for (int i = 0; i < polyline.EdgeCount; i++)
            {
                if (CollidesWith(poly, polyline.GetEdgeAt(i), tolerance))
                {
                    return true;
                }
            }

            // Catches a vertex sitting exactly on the chain; a chain inside the polygon is already
            // found by the edge loop, which contain-checks each segment against the polygon.
            return Containment2.IsPointOn(polyline, poly.Vertices[0], tolerance);
        }

        /// <summary>
        /// Whether a polyline crosses a polygon or starts an edge inside it: where the distance between them is taken
        /// as nought without measuring.
        /// </summary>
        internal static bool CrossesOrStartsIn(GeoPolyline2 polyline, GeoPolygon2 poly, Tolerance tolerance)
        {
            for (int i = 0; i < polyline.EdgeCount; i++)
            {
                if (CrossesOrStartsIn(poly, polyline.GetEdgeAt(i), tolerance))
                {
                    return true;
                }
            }

            return Containment2.IsPointOn(polyline, poly.Vertices[0], tolerance);
        }

        /// <summary>
        /// Checks whether two polylines collide using default tolerance.
        /// </summary>
        public static bool CollidesWith(GeoPolyline2 pl1, GeoPolyline2 pl2)
        {
            return CollidesWith(pl1, pl2, Tolerance.Global);
        }

        /// <summary>
        /// Checks whether two polylines collide within tolerance.
        /// </summary>
        /// <remarks>
        /// Neither has an inside, so they collide where they cross or come within the point tolerance of each other,
        /// edge against edge with no slack past the ends. At a tolerance of a thousandth, the polyline from (40, 100.01)
        /// to (60, 100.0005) collides with the one from (0, 100) to (100, 100), and one ending two thousandths above
        /// does not.
        /// </remarks>
        public static bool CollidesWith(GeoPolyline2 pl1, GeoPolyline2 pl2, Tolerance tolerance)
        {
            if (pl1 == null) throw new ArgumentNullException(nameof(pl1));
            if (pl2 == null) throw new ArgumentNullException(nameof(pl2));

            return CrossesOrStartsOn(pl1, pl2, tolerance) || ComeWithin(EdgesOf(pl1), EdgesOf(pl2), tolerance);
        }

        /// <summary>
        /// Whether two polylines cross or one starts on the other: where the distance between them is taken as nought
        /// without measuring.
        /// </summary>
        internal static bool CrossesOrStartsOn(GeoPolyline2 pl1, GeoPolyline2 pl2, Tolerance tolerance)
        {
            for (int i = 0; i < pl1.EdgeCount; i++)
            {
                GeoLine2 edge1 = pl1.GetEdgeAt(i);
                for (int j = 0; j < pl2.EdgeCount; j++)
                {
                    GeoLine2 edge2 = pl2.GetEdgeAt(j);
                    if (Crosses(edge1, edge2, tolerance))
                    {
                        return true;
                    }
                }
            }

            // Neither chain encloses anything, but one may run exactly along an edge of the other,
            // which the intersection test above reports as parallel rather than as a crossing.
            return Containment2.IsPointOn(pl1, pl2[0], tolerance) ||
                   Containment2.IsPointOn(pl2, pl1[0], tolerance);
        }

        #endregion
        #region Chains that may curve

        /// <summary>
        /// Determines whether a chain that may curve touches or overlaps a straight segment.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static bool CollidesWith(GeoPolylineArc2 chain, GeoLine2 line) => CollidesWith(chain, line, Tolerance.Global);

        /// <summary>
        /// Determines whether a chain that may curve touches or overlaps a straight segment, within a tolerance.
        /// </summary>
        /// <remarks>
        /// They collide where an edge and the segment cross or come within the point tolerance of each other, as two
        /// segments do, a curved edge read as its arc. A segment lying along a straight edge of the chain collides with
        /// it, and so does one ending half a thousandth short of a curved edge, at a tolerance of a thousandth.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static bool CollidesWith(GeoPolylineArc2 chain, GeoLine2 line, Tolerance tolerance)
        {
            if (chain == null) throw new ArgumentNullException(nameof(chain));

            for (int i = 0; i < chain.EdgeCount; i++)
            {
                GeoEdge2 edge = chain.GetEdgeAt(i);
                bool touches = edge.IsArc
                    ? CollidesWith(line, edge.ToArc(), tolerance)
                    : !Clear(line, edge.ToLine(), tolerance) && CollidesWith(edge.ToLine(), line, tolerance);

                if (touches)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Determines whether a chain that may curve touches or overlaps an arc.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static bool CollidesWith(GeoPolylineArc2 chain, GeoArc2 arc) => CollidesWith(chain, arc, Tolerance.Global);

        /// <summary>
        /// Determines whether a chain that may curve touches or overlaps an arc, within a tolerance.
        /// </summary>
        /// <remarks>
        /// Neither has an inside, so they collide where they cross or come within the point tolerance of each other,
        /// edge against edge, and one lying along the other collides with it. At a tolerance of a thousandth, two ends
        /// half a thousandth apart collide and two ends two thousandths apart do not.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static bool CollidesWith(GeoPolylineArc2 chain, GeoArc2 arc, Tolerance tolerance)
        {
            if (chain == null) throw new ArgumentNullException(nameof(chain));

            return Intersection2.TryIntersectWith(chain, arc, out _, tolerance)
                || ArcChain2.ComeWithin(ArcChain2.EdgesOf(chain), arc, tolerance);
        }

        /// <summary>
        /// Determines whether a chain that may curve touches or overlaps a circle.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static bool CollidesWith(GeoPolylineArc2 chain, GeoCircle2 circle) => CollidesWith(chain, circle, Tolerance.Global);

        /// <summary>
        /// Determines whether a chain that may curve touches or overlaps a circle, within a tolerance.
        /// </summary>
        /// <remarks>
        /// One shape lying wholly inside the other counts, so the boundaries need not cross. Outside each other, they
        /// collide where they come within the point tolerance, edge against edge: at a tolerance of a thousandth, two
        /// boundaries half a thousandth apart collide and two thousandths apart do not.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static bool CollidesWith(GeoPolylineArc2 chain, GeoCircle2 circle, Tolerance tolerance)
        {
            if (chain == null) throw new ArgumentNullException(nameof(chain));

            return CrossesOrStartsIn(chain, circle, tolerance)
                || ArcChain2.ComeWithin(ArcChain2.EdgesOf(chain), circle, tolerance);
        }

        /// <summary>
        /// Whether two shapes cross or one starts inside the other: where the distance between them is taken as
        /// nought without measuring.
        /// </summary>
        internal static bool CrossesOrStartsIn(GeoPolylineArc2 chain, GeoCircle2 circle, Tolerance tolerance)
        {
            return Intersection2.TryIntersectWith(chain, circle, out _, tolerance)
                || Containment2.Contains(circle, chain[0], tolerance);
        }

        /// <summary>
        /// Determines whether a chain that may curve touches or overlaps a straight chain.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static bool CollidesWith(GeoPolylineArc2 chain, GeoPolyline2 polyline) => CollidesWith(chain, polyline, Tolerance.Global);

        /// <summary>
        /// Determines whether a chain that may curve touches or overlaps a straight chain, within a tolerance.
        /// </summary>
        /// <remarks>
        /// Neither has an inside, so they collide where they cross or come within the point tolerance of each other,
        /// edge against edge, and one lying along the other collides with it. At a tolerance of a thousandth, two ends
        /// half a thousandth apart collide and two ends two thousandths apart do not.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static bool CollidesWith(GeoPolylineArc2 chain, GeoPolyline2 polyline, Tolerance tolerance)
        {
            if (chain == null) throw new ArgumentNullException(nameof(chain));
            if (polyline == null) throw new ArgumentNullException(nameof(polyline));

            List<GeoEdge2> edges = ArcChain2.EdgesOf(chain);
            List<GeoEdge2> others = ArcChain2.EdgesOf(polyline);

            return ArcChain2.Cross(edges, others, tolerance) || ArcChain2.ComeWithin(edges, others, tolerance);
        }

        /// <summary>
        /// Determines whether a chain that may curve touches or overlaps a straight loop.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static bool CollidesWith(GeoPolylineArc2 chain, GeoPolygon2 polygon) => CollidesWith(chain, polygon, Tolerance.Global);

        /// <summary>
        /// Determines whether a chain that may curve touches or overlaps a straight loop, within a tolerance.
        /// </summary>
        /// <remarks>
        /// One shape lying wholly inside the other counts, so the boundaries need not cross. Outside each other, they
        /// collide where they come within the point tolerance, edge against edge: at a tolerance of a thousandth, two
        /// boundaries half a thousandth apart collide and two thousandths apart do not.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static bool CollidesWith(GeoPolylineArc2 chain, GeoPolygon2 polygon, Tolerance tolerance)
        {
            if (chain == null) throw new ArgumentNullException(nameof(chain));
            if (polygon == null) throw new ArgumentNullException(nameof(polygon));

            return CrossesOrStartsIn(chain, polygon, tolerance)
                || ArcChain2.ComeWithin(ArcChain2.EdgesOf(chain), ArcChain2.EdgesOf(polygon), tolerance);
        }

        /// <summary>
        /// Whether two shapes cross or one starts inside the other: where the distance between them is taken as
        /// nought without measuring.
        /// </summary>
        internal static bool CrossesOrStartsIn(GeoPolylineArc2 chain, GeoPolygon2 polygon, Tolerance tolerance)
        {
            return ArcChain2.Cross(ArcChain2.EdgesOf(chain), ArcChain2.EdgesOf(polygon), tolerance)
                || Containment2.Contains(polygon, chain[0], tolerance);
        }

        /// <summary>
        /// Determines whether a chain that may curve touches or overlaps a chain that may curve.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static bool CollidesWith(GeoPolylineArc2 chain, GeoPolylineArc2 other) => CollidesWith(chain, other, Tolerance.Global);

        /// <summary>
        /// Determines whether a chain that may curve touches or overlaps a chain that may curve, within a tolerance.
        /// </summary>
        /// <remarks>
        /// Neither has an inside, so they collide where they cross or come within the point tolerance of each other,
        /// edge against edge, and one lying along the other collides with it. At a tolerance of a thousandth, two ends
        /// half a thousandth apart collide and two ends two thousandths apart do not.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static bool CollidesWith(GeoPolylineArc2 chain, GeoPolylineArc2 other, Tolerance tolerance)
        {
            if (chain == null) throw new ArgumentNullException(nameof(chain));
            if (other == null) throw new ArgumentNullException(nameof(other));

            List<GeoEdge2> edges = ArcChain2.EdgesOf(chain);
            List<GeoEdge2> others = ArcChain2.EdgesOf(other);

            return ArcChain2.Cross(edges, others, tolerance) || ArcChain2.ComeWithin(edges, others, tolerance);
        }

        /// <summary>
        /// Determines whether a chain that may curve touches or overlaps a loop that may curve.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static bool CollidesWith(GeoPolylineArc2 chain, GeoPolygonArc2 loop) => CollidesWith(chain, loop, Tolerance.Global);

        /// <summary>
        /// Determines whether a chain that may curve touches or overlaps a loop that may curve, within a tolerance.
        /// </summary>
        /// <remarks>
        /// One shape lying wholly inside the other counts, so the boundaries need not cross. Outside each other, they
        /// collide where they come within the point tolerance, edge against edge: at a tolerance of a thousandth, two
        /// boundaries half a thousandth apart collide and two thousandths apart do not.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static bool CollidesWith(GeoPolylineArc2 chain, GeoPolygonArc2 loop, Tolerance tolerance)
        {
            if (chain == null) throw new ArgumentNullException(nameof(chain));
            if (loop == null) throw new ArgumentNullException(nameof(loop));

            return CrossesOrStartsIn(chain, loop, tolerance)
                || ArcChain2.ComeWithin(ArcChain2.EdgesOf(chain), ArcChain2.EdgesOf(loop), tolerance);
        }

        /// <summary>
        /// Whether two shapes cross or one starts inside the other: where the distance between them is taken as
        /// nought without measuring.
        /// </summary>
        internal static bool CrossesOrStartsIn(GeoPolylineArc2 chain, GeoPolygonArc2 loop, Tolerance tolerance)
        {
            return ArcChain2.Cross(ArcChain2.EdgesOf(chain), ArcChain2.EdgesOf(loop), tolerance)
                || Containment2.Contains(loop, chain[0], tolerance);
        }

        /// <summary>
        /// Determines whether a loop that may curve touches or overlaps a straight segment.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public static bool CollidesWith(GeoPolygonArc2 loop, GeoLine2 line) => CollidesWith(loop, line, Tolerance.Global);

        /// <summary>
        /// Determines whether a loop that may curve touches or overlaps a straight segment, within a tolerance.
        /// </summary>
        /// <remarks>
        /// One shape lying wholly inside the other counts, so the boundaries need not cross. Outside, they collide where
        /// an edge and the segment cross or come within the point tolerance of each other, as two segments do, a curved
        /// edge read as its arc. At a tolerance of a tenth, the segment from (101, 40) to (100.05, 60) collides with the
        /// square from the origin to (100, 100), coming within 0.05 of its right side, though its line meets the side only
        /// 1.05 past its end. Its distance stays the gap, 0.05.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public static bool CollidesWith(GeoPolygonArc2 loop, GeoLine2 line, Tolerance tolerance)
        {
            if (loop == null) throw new ArgumentNullException(nameof(loop));

            if (CrossesOrStartsIn(loop, line, tolerance))
            {
                return true;
            }

            for (int i = 0; i < loop.EdgeCount; i++)
            {
                GeoEdge2 edge = loop.GetEdgeAt(i);
                bool touches = edge.IsArc
                    ? !Clear(line, edge, tolerance) && CollidesWith(line, edge.ToArc(), tolerance)
                    : !Clear(line, edge.ToLine(), tolerance) && CollidesWith(edge.ToLine(), line, tolerance);

                if (touches)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Whether a segment lies more than twice the point tolerance clear of a box holding a curved edge, read before
        /// the arc is made. Every point of the arc lies within half the chord, or within the sagitta, half the chord
        /// times the bulge, of the chord's middle: so within half the chord times one plus the size of the bulge.
        /// </summary>
        private static bool Clear(GeoLine2 line, GeoEdge2 edge, Tolerance tolerance)
        {
            double midX = 0.5 * (edge.StartPoint.X + edge.EndPoint.X);
            double midY = 0.5 * (edge.StartPoint.Y + edge.EndPoint.Y);
            double reach = 0.5 * Distance2.DistanceTo(edge.StartPoint, edge.EndPoint) * (1.0 + Math.Abs(edge.Bulge));

            return Clear(line, midX - reach, midY - reach, midX + reach, midY + reach, tolerance);
        }

        /// <summary>
        /// Whether a segment crosses the boundary of a loop that may curve or starts inside it: where the distance
        /// between them is taken as nought without measuring. A segment that only comes within the tolerance of the
        /// boundary is measured, so its distance is the gap, not nought.
        /// </summary>
        internal static bool CrossesOrStartsIn(GeoPolygonArc2 loop, GeoLine2 line, Tolerance tolerance)
        {
            if (ArcChain2.Cross(ArcChain2.EdgesOf(loop), line, tolerance))
            {
                return true;
            }

            return Containment2.Contains(loop, line.StartPoint, tolerance);
        }

        /// <summary>
        /// Determines whether a loop that may curve touches or overlaps an arc.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public static bool CollidesWith(GeoPolygonArc2 loop, GeoArc2 arc) => CollidesWith(loop, arc, Tolerance.Global);

        /// <summary>
        /// Determines whether a loop that may curve touches or overlaps an arc, within a tolerance.
        /// </summary>
        /// <remarks>
        /// One shape lying wholly inside the other counts, so the boundaries need not cross. Outside each other, they
        /// collide where they come within the point tolerance, edge against edge: at a tolerance of a thousandth, two
        /// boundaries half a thousandth apart collide and two thousandths apart do not.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public static bool CollidesWith(GeoPolygonArc2 loop, GeoArc2 arc, Tolerance tolerance)
        {
            if (loop == null) throw new ArgumentNullException(nameof(loop));

            return CrossesOrStartsIn(loop, arc, tolerance)
                || ArcChain2.ComeWithin(ArcChain2.EdgesOf(loop), arc, tolerance);
        }

        /// <summary>
        /// Whether two shapes cross or one starts inside the other: where the distance between them is taken as
        /// nought without measuring.
        /// </summary>
        internal static bool CrossesOrStartsIn(GeoPolygonArc2 loop, GeoArc2 arc, Tolerance tolerance)
        {
            return Intersection2.TryIntersectWith(loop, arc, out _, tolerance)
                || Containment2.Contains(loop, arc.StartPoint, tolerance);
        }

        /// <summary>
        /// Determines whether a loop that may curve touches or overlaps a circle.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public static bool CollidesWith(GeoPolygonArc2 loop, GeoCircle2 circle) => CollidesWith(loop, circle, Tolerance.Global);

        /// <summary>
        /// Determines whether a loop that may curve touches or overlaps a circle, within a tolerance.
        /// </summary>
        /// <remarks>
        /// One shape lying wholly inside the other counts, so the boundaries need not cross. Outside each other, they
        /// collide where they come within the point tolerance, edge against edge: at a tolerance of a thousandth, two
        /// boundaries half a thousandth apart collide and two thousandths apart do not.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public static bool CollidesWith(GeoPolygonArc2 loop, GeoCircle2 circle, Tolerance tolerance)
        {
            if (loop == null) throw new ArgumentNullException(nameof(loop));

            return CrossesOrStartsIn(loop, circle, tolerance)
                || ArcChain2.ComeWithin(ArcChain2.EdgesOf(loop), circle, tolerance);
        }

        /// <summary>
        /// Whether two shapes cross or one starts inside the other: where the distance between them is taken as
        /// nought without measuring.
        /// </summary>
        internal static bool CrossesOrStartsIn(GeoPolygonArc2 loop, GeoCircle2 circle, Tolerance tolerance)
        {
            return Intersection2.TryIntersectWith(loop, circle, out _, tolerance)
                || Containment2.Contains(loop, circle.Center, tolerance)
                || Containment2.Contains(circle, loop[0], tolerance);
        }

        /// <summary>
        /// Determines whether a loop that may curve touches or overlaps a straight chain.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public static bool CollidesWith(GeoPolygonArc2 loop, GeoPolyline2 polyline) => CollidesWith(loop, polyline, Tolerance.Global);

        /// <summary>
        /// Determines whether a loop that may curve touches or overlaps a straight chain, within a tolerance.
        /// </summary>
        /// <remarks>
        /// One shape lying wholly inside the other counts, so the boundaries need not cross. Outside each other, they
        /// collide where they come within the point tolerance, edge against edge: at a tolerance of a thousandth, two
        /// boundaries half a thousandth apart collide and two thousandths apart do not.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public static bool CollidesWith(GeoPolygonArc2 loop, GeoPolyline2 polyline, Tolerance tolerance)
        {
            if (loop == null) throw new ArgumentNullException(nameof(loop));
            if (polyline == null) throw new ArgumentNullException(nameof(polyline));

            return CrossesOrStartsIn(loop, polyline, tolerance)
                || ArcChain2.ComeWithin(ArcChain2.EdgesOf(loop), ArcChain2.EdgesOf(polyline), tolerance);
        }

        /// <summary>
        /// Whether two shapes cross or one starts inside the other: where the distance between them is taken as
        /// nought without measuring.
        /// </summary>
        internal static bool CrossesOrStartsIn(GeoPolygonArc2 loop, GeoPolyline2 polyline, Tolerance tolerance)
        {
            return ArcChain2.Cross(ArcChain2.EdgesOf(loop), ArcChain2.EdgesOf(polyline), tolerance)
                || Containment2.Contains(loop, polyline[0], tolerance);
        }

        /// <summary>
        /// Determines whether a loop that may curve touches or overlaps a straight loop.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public static bool CollidesWith(GeoPolygonArc2 loop, GeoPolygon2 polygon) => CollidesWith(loop, polygon, Tolerance.Global);

        /// <summary>
        /// Determines whether a loop that may curve touches or overlaps a straight loop, within a tolerance.
        /// </summary>
        /// <remarks>
        /// One shape lying wholly inside the other counts, so the boundaries need not cross. Outside each other, they
        /// collide where they come within the point tolerance, edge against edge: at a tolerance of a thousandth, two
        /// boundaries half a thousandth apart collide and two thousandths apart do not.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public static bool CollidesWith(GeoPolygonArc2 loop, GeoPolygon2 polygon, Tolerance tolerance)
        {
            if (loop == null) throw new ArgumentNullException(nameof(loop));
            if (polygon == null) throw new ArgumentNullException(nameof(polygon));

            return CrossesOrStartsIn(loop, polygon, tolerance)
                || ArcChain2.ComeWithin(ArcChain2.EdgesOf(loop), ArcChain2.EdgesOf(polygon), tolerance);
        }

        /// <summary>
        /// Whether two shapes cross or one starts inside the other: where the distance between them is taken as
        /// nought without measuring.
        /// </summary>
        internal static bool CrossesOrStartsIn(GeoPolygonArc2 loop, GeoPolygon2 polygon, Tolerance tolerance)
        {
            return ArcChain2.Cross(ArcChain2.EdgesOf(loop), ArcChain2.EdgesOf(polygon), tolerance)
                || Containment2.Contains(loop, polygon[0], tolerance)
                || Containment2.Contains(polygon, loop[0], tolerance);
        }

        /// <summary>
        /// Determines whether a loop that may curve touches or overlaps a chain that may curve.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public static bool CollidesWith(GeoPolygonArc2 loop, GeoPolylineArc2 chain) => CollidesWith(loop, chain, Tolerance.Global);

        /// <summary>
        /// Determines whether a loop that may curve touches or overlaps a chain that may curve, within a tolerance.
        /// </summary>
        /// <remarks>
        /// One shape lying wholly inside the other counts, so the boundaries need not cross. Outside each other, they
        /// collide where they come within the point tolerance, edge against edge: at a tolerance of a thousandth, two
        /// boundaries half a thousandth apart collide and two thousandths apart do not.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public static bool CollidesWith(GeoPolygonArc2 loop, GeoPolylineArc2 chain, Tolerance tolerance)
        {
            if (loop == null) throw new ArgumentNullException(nameof(loop));
            if (chain == null) throw new ArgumentNullException(nameof(chain));

            return CrossesOrStartsIn(loop, chain, tolerance)
                || ArcChain2.ComeWithin(ArcChain2.EdgesOf(loop), ArcChain2.EdgesOf(chain), tolerance);
        }

        /// <summary>
        /// Whether two shapes cross or one starts inside the other: where the distance between them is taken as
        /// nought without measuring.
        /// </summary>
        internal static bool CrossesOrStartsIn(GeoPolygonArc2 loop, GeoPolylineArc2 chain, Tolerance tolerance)
        {
            return ArcChain2.Cross(ArcChain2.EdgesOf(loop), ArcChain2.EdgesOf(chain), tolerance)
                || Containment2.Contains(loop, chain[0], tolerance);
        }

        /// <summary>
        /// Determines whether a loop that may curve touches or overlaps a loop that may curve.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public static bool CollidesWith(GeoPolygonArc2 loop, GeoPolygonArc2 other) => CollidesWith(loop, other, Tolerance.Global);

        /// <summary>
        /// Determines whether a loop that may curve touches or overlaps a loop that may curve, within a tolerance.
        /// </summary>
        /// <remarks>
        /// One shape lying wholly inside the other counts, so the boundaries need not cross. Outside each other, they
        /// collide where they come within the point tolerance, edge against edge: at a tolerance of a thousandth, two
        /// boundaries half a thousandth apart collide and two thousandths apart do not.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public static bool CollidesWith(GeoPolygonArc2 loop, GeoPolygonArc2 other, Tolerance tolerance)
        {
            if (loop == null) throw new ArgumentNullException(nameof(loop));
            if (other == null) throw new ArgumentNullException(nameof(other));

            return CrossesOrStartsIn(loop, other, tolerance)
                || ArcChain2.ComeWithin(ArcChain2.EdgesOf(loop), ArcChain2.EdgesOf(other), tolerance);
        }

        /// <summary>
        /// Whether two shapes cross or one starts inside the other: where the distance between them is taken as
        /// nought without measuring.
        /// </summary>
        internal static bool CrossesOrStartsIn(GeoPolygonArc2 loop, GeoPolygonArc2 other, Tolerance tolerance)
        {
            return ArcChain2.Cross(ArcChain2.EdgesOf(loop), ArcChain2.EdgesOf(other), tolerance)
                || Containment2.Contains(loop, other[0], tolerance)
                || Containment2.Contains(other, loop[0], tolerance);
        }

        #endregion

    }
}
