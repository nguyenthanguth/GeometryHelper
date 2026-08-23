using System;
using System.Collections.Generic;
using CommonGeometry;
using PlaneGeometry.Geometry;

namespace PlaneGeometry.Core
{
    /// <summary>
    /// Provides static calculation methods for checking spatial collisions and geometric overlaps.
    /// </summary>
    public static class Collision2
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
        public static bool CollidesWith(GeoLine2 line1, GeoLine2 line2, Tolerance tolerance)
        {
            if (Intersection2.TryIntersectWith(line1, line2, out _, tolerance))
            {
                return true;
            }
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
            return Distance2.DistanceTo(line, circle.Center) <= circle.Radius + tolerance.EqualPoint;
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
            if (Containment2.Contains(rect, circle.Center))
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
            return Distance2.DistanceTo(poly, circle.Center) <= circle.Radius + tolerance.EqualPoint;
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
        public static bool CollidesWith(GeoRectangle2 rect, GeoLine2 line, Tolerance tolerance)
        {
            foreach (var edge in rect.GetEdges())
            {
                if (Intersection2.TryIntersectWith(line, edge, out _, tolerance))
                {
                    return true;
                }
            }

            if (Containment2.Contains(rect, line.StartPoint))
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
        public static bool CollidesWith(GeoRectangle2 rect, GeoPolygon2 poly, Tolerance tolerance)
        {
            if (poly == null) throw new ArgumentNullException(nameof(poly));

            GeoLine2[] rectEdges = rect.GetEdges();

            foreach (var polyEdge in poly.GetEdges())
            {
                foreach (var rectEdge in rectEdges)
                {
                    if (Intersection2.TryIntersectWith(polyEdge, rectEdge, out _, tolerance))
                    {
                        return true;
                    }
                }
            }

            if (Containment2.Contains(rect, poly.Vertices[0]))
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
        public static bool CollidesWith(GeoPolygon2 poly, GeoLine2 line, Tolerance tolerance)
        {
            if (poly == null) throw new ArgumentNullException(nameof(poly));

            foreach (var polyEdge in poly.GetEdges())
            {
                if (Intersection2.TryIntersectWith(line, polyEdge, out _, tolerance))
                {
                    return true;
                }
            }

            return Containment2.Contains(poly, line.StartPoint, tolerance);
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
        public static bool CollidesWith(GeoPolygon2 poly1, GeoPolygon2 poly2, Tolerance tolerance)
        {
            if (poly1 == null) throw new ArgumentNullException(nameof(poly1));
            if (poly2 == null) throw new ArgumentNullException(nameof(poly2));

            foreach (var edge1 in poly1.GetEdges())
            {
                foreach (var edge2 in poly2.GetEdges())
                {
                    if (Intersection2.TryIntersectWith(edge1, edge2, out _, tolerance))
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
        public static bool CollidesWith(GeoPolyline2 polyline, GeoLine2 line, Tolerance tolerance)
        {
            if (polyline == null) throw new ArgumentNullException(nameof(polyline));

            for (int i = 0; i < polyline.EdgeCount; i++)
            {
                if (Intersection2.TryIntersectWith(polyline.GetEdgeAt(i), line, out _, tolerance))
                {
                    return true;
                }
            }

            // A segment lying exactly along one of the edges is reported as parallel by the intersection
            // test above, never as a crossing, so this is what catches the overlap.
            return Containment2.IsPointOn(polyline, line.StartPoint, tolerance);
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
        /// Checks whether two polylines collide using default tolerance.
        /// </summary>
        public static bool CollidesWith(GeoPolyline2 pl1, GeoPolyline2 pl2)
        {
            return CollidesWith(pl1, pl2, Tolerance.Global);
        }

        /// <summary>
        /// Checks whether two polylines collide within tolerance.
        /// </summary>
        public static bool CollidesWith(GeoPolyline2 pl1, GeoPolyline2 pl2, Tolerance tolerance)
        {
            if (pl1 == null) throw new ArgumentNullException(nameof(pl1));
            if (pl2 == null) throw new ArgumentNullException(nameof(pl2));

            for (int i = 0; i < pl1.EdgeCount; i++)
            {
                GeoLine2 edge1 = pl1.GetEdgeAt(i);
                for (int j = 0; j < pl2.EdgeCount; j++)
                {
                    GeoLine2 edge2 = pl2.GetEdgeAt(j);
                    if (Intersection2.TryIntersectWith(edge1, edge2, out _, tolerance))
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
    }
}
