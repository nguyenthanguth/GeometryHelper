using System;
using CommonGeometry;
using PlaneGeometry.Geometry;

namespace PlaneGeometry.Core
{
    /// <summary>
    /// Provides static calculation methods for geometric distances, squared distances, and closest point projections.
    /// </summary>
    public static class Distance2
    {
        #region Point - Point

        /// <summary>
        /// Calculates the Euclidean distance between two points.
        /// </summary>
        /// <param name="p1">The first point.</param>
        /// <param name="p2">The second point.</param>
        /// <returns>The Euclidean distance between the two points.</returns>
        public static double DistanceTo(GeoPoint2 p1, GeoPoint2 p2)
        {
            return Math.Sqrt(GetDistanceSquaredTo(p1, p2));
        }

        /// <summary>
        /// Calculates the squared Euclidean distance between two points.
        /// </summary>
        /// <param name="p1">The first point.</param>
        /// <param name="p2">The second point.</param>
        /// <returns>The squared Euclidean distance between the two points.</returns>
        public static double GetDistanceSquaredTo(GeoPoint2 p1, GeoPoint2 p2)
        {
            double dx = p2.X - p1.X;
            double dy = p2.Y - p1.Y;
            return dx * dx + dy * dy;
        }

        #endregion

        #region Line - Shapes

        /// <summary>
        /// Calculates the shortest distance from a line segment to a point.
        /// </summary>
        /// <param name="line">The line segment.</param>
        /// <param name="point">The target point.</param>
        /// <returns>The shortest Euclidean distance.</returns>
        public static double DistanceTo(GeoLine2 line, GeoPoint2 point)
        {
            return DistanceTo(point, Projection2.ProjectToLine(line, point));
        }

        /// <summary>
        /// Calculates the shortest distance between two line segments using default tolerance.
        /// </summary>
        public static double DistanceTo(GeoLine2 line1, GeoLine2 line2)
        {
            return DistanceTo(line1, line2, Tolerance.Global);
        }

        /// <summary>
        /// Calculates the shortest distance between two line segments within tolerance.
        /// </summary>
        public static double DistanceTo(GeoLine2 line1, GeoLine2 line2, Tolerance tolerance)
        {
            // Not Intersection2.TryIntersectWith: that one reports no intersection for any pair meeting at
            // less than the angular tolerance, which is right when the intersection point is wanted but
            // would make two crossing segments measure the gap between their endpoints instead of zero,
            // by an amount that grows with their length.
            if (Projection2.TryGetCrossingPoint(line1, line2, tolerance, out _))
            {
                return 0.0;
            }

            double d1 = DistanceTo(line1, line2.StartPoint);
            double d2 = DistanceTo(line1, line2.EndPoint);
            double d3 = DistanceTo(line2, line1.StartPoint);
            double d4 = DistanceTo(line2, line1.EndPoint);

            return Math.Min(Math.Min(d1, d2), Math.Min(d3, d4));
        }

        #endregion

        #region Circle - Shapes

        /// <summary>
        /// Calculates the shortest boundary distance from a circle to a point.
        /// Returns 0 if the point is inside the circle.
        /// </summary>
        /// <param name="circle">The circle.</param>
        /// <param name="point">The point.</param>
        /// <returns>The shortest distance to the circle boundary.</returns>
        public static double DistanceTo(GeoCircle2 circle, GeoPoint2 point)
        {
            return Math.Max(0.0, DistanceTo(circle.Center, point) - circle.Radius);
        }

        /// <summary>
        /// Calculates the shortest boundary distance from a circle to a line segment.
        /// Returns 0 if the line segment intersects or is inside the circle.
        /// </summary>
        /// <param name="circle">The circle.</param>
        /// <param name="line">The line segment.</param>
        /// <returns>The shortest distance between circle and line segment.</returns>
        public static double DistanceTo(GeoCircle2 circle, GeoLine2 line)
        {
            return Math.Max(0.0, DistanceTo(line, circle.Center) - circle.Radius);
        }

        /// <summary>
        /// Calculates the shortest boundary distance between two circles.
        /// Returns 0 if the two circles intersect or overlap.
        /// </summary>
        /// <param name="c1">The first circle.</param>
        /// <param name="c2">The second circle.</param>
        /// <returns>The shortest boundary distance.</returns>
        public static double DistanceTo(GeoCircle2 c1, GeoCircle2 c2)
        {
            return Math.Max(0.0, DistanceTo(c1.Center, c2.Center) - (c1.Radius + c2.Radius));
        }

        /// <summary>
        /// Calculates the shortest boundary distance from a circle to a rectangle.
        /// Returns 0 if they intersect or overlap.
        /// </summary>
        /// <param name="circle">The circle.</param>
        /// <param name="rect">The rectangle.</param>
        /// <returns>The shortest distance between circle and rectangle.</returns>
        public static double DistanceTo(GeoCircle2 circle, GeoRectangle2 rect)
        {
            if (Containment2.Contains(rect, circle.Center))
            {
                return 0.0;
            }
            return Math.Max(0.0, DistanceTo(rect, circle.Center) - circle.Radius);
        }

        /// <summary>
        /// Calculates the shortest boundary distance from a circle to a polygon.
        /// Returns 0 if they intersect or overlap.
        /// </summary>
        /// <param name="circle">The circle.</param>
        /// <param name="poly">The polygon.</param>
        /// <returns>The shortest distance between circle and polygon.</returns>
        public static double DistanceTo(GeoCircle2 circle, GeoPolygon2 poly)
        {
            if (poly == null) throw new ArgumentNullException(nameof(poly));

            if (Containment2.Contains(poly, circle.Center))
            {
                return 0.0;
            }
            return Math.Max(0.0, DistanceTo(poly, circle.Center) - circle.Radius);
        }

        #endregion

        #region Rectangle - Shapes

        /// <summary>
        /// Calculates the shortest Euclidean distance from a rectangle to a point.
        /// Returns 0 if the point is inside the rectangle.
        /// </summary>
        /// <param name="rect">The rectangle.</param>
        /// <param name="point">The target point.</param>
        /// <returns>The shortest distance to the rectangle.</returns>
        public static double DistanceTo(GeoRectangle2 rect, GeoPoint2 point)
        {
            double dx = point.X - rect.Center.X;
            double dy = point.Y - rect.Center.Y;

            double cos = Math.Cos(rect.AngleRad);
            double sin = Math.Sin(rect.AngleRad);

            // Project point onto the local coordinate system around Center
            double localX = dx * cos + dy * sin;
            double localY = -dx * sin + dy * cos;

            double halfW = rect.Width * 0.5;
            double halfH = rect.Height * 0.5;

            double clampedX = Math.Max(-halfW, Math.Min(halfW, localX));
            double clampedY = Math.Max(-halfH, Math.Min(halfH, localY));

            double diffX = localX - clampedX;
            double diffY = localY - clampedY;

            return Math.Sqrt(diffX * diffX + diffY * diffY);
        }

        /// <summary>
        /// Calculates the shortest boundary distance from a rectangle to a line segment.
        /// Returns 0 if the line segment intersects or lies inside the rectangle.
        /// </summary>
        /// <param name="rect">The rectangle.</param>
        /// <param name="line">The line segment.</param>
        /// <returns>The shortest boundary distance.</returns>
        public static double DistanceTo(GeoRectangle2 rect, GeoLine2 line)
        {
            if (Collision2.CollidesWith(rect, line))
            {
                return 0.0;
            }

            double minDistance = double.MaxValue;
            GeoLine2[] rectEdges = rect.GetEdges();

            foreach (var re in rectEdges)
            {
                double d = DistanceTo(re, line);
                if (d < minDistance) minDistance = d;
            }

            return minDistance;
        }

        /// <summary>
        /// Calculates the shortest boundary distance between two rectangles.
        /// Returns 0 if they intersect or overlap.
        /// </summary>
        /// <param name="rect1">The first rectangle.</param>
        /// <param name="rect2">The second rectangle.</param>
        /// <returns>The shortest boundary distance.</returns>
        public static double DistanceTo(GeoRectangle2 rect1, GeoRectangle2 rect2)
        {
            if (Collision2.CollidesWith(rect1, rect2))
            {
                return 0.0;
            }

            double minDistance = double.MaxValue;
            GeoLine2[] r1Edges = rect1.GetEdges();
            GeoLine2[] r2Edges = rect2.GetEdges();

            foreach (var r1e in r1Edges)
            {
                foreach (var r2e in r2Edges)
                {
                    double d = DistanceTo(r1e, r2e);
                    if (d < minDistance) minDistance = d;
                }
            }

            return minDistance;
        }

        /// <summary>
        /// Calculates the shortest boundary distance from a rectangle to a polygon.
        /// Returns 0 if they intersect or overlap.
        /// </summary>
        /// <param name="rect">The rectangle.</param>
        /// <param name="poly">The polygon.</param>
        /// <returns>The shortest boundary distance.</returns>
        public static double DistanceTo(GeoRectangle2 rect, GeoPolygon2 poly)
        {
            if (poly == null) throw new ArgumentNullException(nameof(poly));

            if (Collision2.CollidesWith(rect, poly))
            {
                return 0.0;
            }

            double minDistance = double.MaxValue;
            GeoLine2[] rectEdges = rect.GetEdges();

            for (int i = 0; i < poly.EdgeCount; i++)
            {
                GeoLine2 pe = poly.GetEdgeAt(i);
                foreach (var re in rectEdges)
                {
                    double d = DistanceTo(pe, re);
                    if (d < minDistance) minDistance = d;
                }
            }

            return minDistance;
        }

        #endregion

        #region Polygon - Shapes

        /// <summary>
        /// Calculates the shortest distance from a polygon boundary to a point.
        /// </summary>
        /// <param name="poly">The polygon.</param>
        /// <param name="point">The target point.</param>
        /// <returns>The shortest distance to the polygon boundary.</returns>
        public static double DistanceTo(GeoPolygon2 poly, GeoPoint2 point)
        {
            if (poly == null) throw new ArgumentNullException(nameof(poly));

            // A polygon is a filled region here, exactly like a rectangle or a circle, so a point inside
            // it is at distance zero. Projection2 reports the boundary point instead, so the two
            // deliberately disagree for interior points.
            if (Containment2.Contains(poly, point))
            {
                return 0.0;
            }

            return DistanceTo(point, Projection2.ProjectToPolygon(poly, point));
        }

        /// <summary>
        /// Calculates the shortest boundary distance from a polygon to a line segment.
        /// </summary>
        public static double DistanceTo(GeoPolygon2 poly, GeoLine2 line)
        {
            if (poly == null) throw new ArgumentNullException(nameof(poly));

            // Zero when the segment crosses the polygon or lies inside it, matching DistanceTo(rect, line).
            if (Collision2.CollidesWith(poly, line))
            {
                return 0.0;
            }

            double minDistance = double.MaxValue;
            for (int i = 0; i < poly.EdgeCount; i++)
            {
                double d = DistanceTo(poly.GetEdgeAt(i), line);
                if (d < minDistance) minDistance = d;
            }
            return minDistance;
        }

        /// <summary>
        /// Calculates the shortest boundary distance between two polygons.
        /// Returns 0 if they intersect or overlap.
        /// </summary>
        public static double DistanceTo(GeoPolygon2 poly1, GeoPolygon2 poly2)
        {
            if (poly1 == null) throw new ArgumentNullException(nameof(poly1));
            if (poly2 == null) throw new ArgumentNullException(nameof(poly2));
            if (Collision2.CollidesWith(poly1, poly2)) return 0.0;

            double minDistance = double.MaxValue;
            for (int i = 0; i < poly1.EdgeCount; i++)
            {
                GeoLine2 e1 = poly1.GetEdgeAt(i);
                for (int j = 0; j < poly2.EdgeCount; j++)
                {
                    double d = DistanceTo(e1, poly2.GetEdgeAt(j));
                    if (d < minDistance) minDistance = d;
                }
            }
            return minDistance;
        }

        #endregion

        #region Polyline - Shapes

        /// <summary>
        /// Calculates the shortest Euclidean distance from a polyline to a point.
        /// </summary>
        /// <param name="polyline">The polyline.</param>
        /// <param name="point">The target point.</param>
        /// <returns>The shortest distance to the polyline.</returns>
        public static double DistanceTo(GeoPolyline2 polyline, GeoPoint2 point)
        {
            if (polyline == null) throw new ArgumentNullException(nameof(polyline));

            // A polyline is a curve, not a region, so only its path counts: a point sitting in the mouth
            // of a horseshoe is at its distance from the path, never at zero.
            return DistanceTo(point, Projection2.ProjectToPolyline(polyline, point));
        }

        /// <summary>
        /// Calculates the shortest distance between a polyline and a line segment.
        /// Returns 0 if they intersect.
        /// </summary>
        /// <param name="polyline">The polyline.</param>
        /// <param name="line">The line segment.</param>
        /// <returns>The shortest distance.</returns>
        public static double DistanceTo(GeoPolyline2 polyline, GeoLine2 line)
        {
            if (polyline == null) throw new ArgumentNullException(nameof(polyline));

            if (Collision2.CollidesWith(polyline, line))
            {
                return 0.0;
            }

            double minDistance = double.MaxValue;
            for (int i = 0; i < polyline.EdgeCount; i++)
            {
                GeoLine2 edge = polyline.GetEdgeAt(i);
                double d = DistanceTo(edge, line);
                if (d < minDistance) minDistance = d;
            }

            return minDistance;
        }

        /// <summary>
        /// Calculates the shortest distance between a polyline and a rectangle.
        /// Returns 0 if they intersect.
        /// </summary>
        public static double DistanceTo(GeoPolyline2 polyline, GeoRectangle2 rect)
        {
            if (polyline == null) throw new ArgumentNullException(nameof(polyline));
            if (Collision2.CollidesWith(polyline, rect)) return 0.0;

            double minDistance = double.MaxValue;
            GeoLine2[] rectEdges = rect.GetEdges();
            for (int i = 0; i < polyline.EdgeCount; i++)
            {
                GeoLine2 pe = polyline.GetEdgeAt(i);
                foreach (var re in rectEdges)
                {
                    double d = DistanceTo(pe, re);
                    if (d < minDistance) minDistance = d;
                }
            }
            return minDistance;
        }

        /// <summary>
        /// Calculates the shortest distance between a polyline and a polygon.
        /// Returns 0 if they intersect.
        /// </summary>
        public static double DistanceTo(GeoPolyline2 polyline, GeoPolygon2 poly)
        {
            if (polyline == null) throw new ArgumentNullException(nameof(polyline));
            if (poly == null) throw new ArgumentNullException(nameof(poly));
            if (Collision2.CollidesWith(polyline, poly)) return 0.0;

            double minDistance = double.MaxValue;
            for (int i = 0; i < polyline.EdgeCount; i++)
            {
                GeoLine2 ple = polyline.GetEdgeAt(i);
                for (int j = 0; j < poly.EdgeCount; j++)
                {
                    double d = DistanceTo(ple, poly.GetEdgeAt(j));
                    if (d < minDistance) minDistance = d;
                }
            }
            return minDistance;
        }

        /// <summary>
        /// Calculates the shortest distance between a polyline and a circle.
        /// Returns 0 if they intersect.
        /// </summary>
        public static double DistanceTo(GeoPolyline2 polyline, GeoCircle2 circle)
        {
            if (polyline == null) throw new ArgumentNullException(nameof(polyline));
            if (Collision2.CollidesWith(circle, polyline)) return 0.0;

            double minDistance = double.MaxValue;
            for (int i = 0; i < polyline.EdgeCount; i++)
            {
                double d = DistanceTo(circle, polyline.GetEdgeAt(i));
                if (d < minDistance) minDistance = d;
            }
            return minDistance;
        }

        /// <summary>
        /// Calculates the shortest distance between two polylines.
        /// Returns 0 if they intersect.
        /// </summary>
        public static double DistanceTo(GeoPolyline2 pl1, GeoPolyline2 pl2)
        {
            if (pl1 == null) throw new ArgumentNullException(nameof(pl1));
            if (pl2 == null) throw new ArgumentNullException(nameof(pl2));
            if (Collision2.CollidesWith(pl1, pl2)) return 0.0;

            double minDistance = double.MaxValue;
            for (int i = 0; i < pl1.EdgeCount; i++)
            {
                GeoLine2 e1 = pl1.GetEdgeAt(i);
                for (int j = 0; j < pl2.EdgeCount; j++)
                {
                    double d = DistanceTo(e1, pl2.GetEdgeAt(j));
                    if (d < minDistance) minDistance = d;
                }
            }
            return minDistance;
        }

        #endregion
    }
}
