using System;
using System.Collections.Generic;
using CommonGeometry;
using PlaneGeometry.Geometry;

namespace PlaneGeometry.Core
{
    /// <summary>
    /// Provides static calculation methods for geometric intersections and finding exact intersection points.
    /// </summary>
    public static class Intersection2
    {
        #region Line - Line

        /// <summary>
        /// Tries to calculate the intersection point between two line segments using default tolerance.
        /// </summary>
        /// <param name="line1">The first line segment.</param>
        /// <param name="line2">The second line segment.</param>
        /// <param name="intersection">The resulting intersection point if successful.</param>
        /// <returns>true if the line segments intersect; otherwise, false.</returns>
        public static bool TryIntersectWith(GeoLine2 line1, GeoLine2 line2, out GeoPoint2 intersection)
        {
            return TryIntersectWith(line1, line2, out intersection, Tolerance.Global);
        }

        /// <summary>
        /// Tries to calculate the intersection point between two line segments within tolerance.
        /// </summary>
        /// <param name="line1">The first line segment.</param>
        /// <param name="line2">The second line segment.</param>
        /// <param name="intersection">The resulting intersection point if successful.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the line segments intersect within tolerance; otherwise, false.</returns>
        public static bool TryIntersectWith(GeoLine2 line1, GeoLine2 line2, out GeoPoint2 intersection, Tolerance tolerance)
        {
            intersection = new GeoPoint2(0, 0);
            GeoVector2 r = line1.Direction;
            GeoVector2 s = line2.Direction;

            double rLength = r.Length;
            double sLength = s.Length;

            // A degenerate segment has no direction, so no intersection parameter can be derived.
            if (rLength <= tolerance.EqualPoint || sLength <= tolerance.EqualPoint)
            {
                return false;
            }

            double rCrossS = r.CrossProduct(s);

            // |r x s| equals |r| * |s| * sin(angle), so it has units of length squared. Comparing it
            // directly against a length threshold makes the parallel test depend on the scale of the
            // input: the same pair of segments scaled up would be reported as intersecting while the
            // small version would not. Dividing by both lengths reduces it to sin(angle), which is
            // scale invariant and lets EqualAngleRad act as the angular threshold it is meant to be.
            // This also keeps the result consistent with Parallel2.IsParallel for the same two lines.
            if (Math.Abs(rCrossS) <= tolerance.EqualAngleSin * rLength * sLength)
            {
                return false; // Parallel2 or collinear
            }

            GeoVector2 qMinusP = line1.StartPoint.GetVectorTo(line2.StartPoint);
            double t = qMinusP.CrossProduct(s) / rCrossS;
            double u = qMinusP.CrossProduct(r) / rCrossS;

            // t and u are dimensionless parameters along each segment, so the slack allowed past an
            // endpoint has to be converted from a distance into parameter space. Using EqualPoint
            // directly would let a 100000 unit long segment reach 10 units beyond its own endpoint.
            double tTolerance = tolerance.EqualPoint / rLength;
            double uTolerance = tolerance.EqualPoint / sLength;

            if (t >= -tTolerance && t <= 1.0 + tTolerance &&
                u >= -uTolerance && u <= 1.0 + uTolerance)
            {
                intersection = line1.GetPointAtParameter(Math.Max(0.0, Math.Min(1.0, t)));
                return true;
            }

            return false;
        }

        /// <summary>
        /// Gets the intersection point between two line segments using default tolerance.
        /// Returns null if lines do not intersect.
        /// </summary>
        public static GeoPoint2? GetIntersection(GeoLine2 line1, GeoLine2 line2)
        {
            return GetIntersection(line1, line2, Tolerance.Global);
        }

        /// <summary>
        /// Gets the intersection point between two line segments within tolerance.
        /// Returns null if lines do not intersect.
        /// </summary>
        public static GeoPoint2? GetIntersection(GeoLine2 line1, GeoLine2 line2, Tolerance tolerance)
        {
            return TryIntersectWith(line1, line2, out GeoPoint2 pt, tolerance) ? pt : (GeoPoint2?)null;
        }

        #endregion

        #region Circle - Line

        /// <summary>
        /// Tries to calculate the intersection points between a circle and a line segment using default tolerance.
        /// </summary>
        public static bool TryIntersectWith(GeoCircle2 circle, GeoLine2 line, out GeoPoint2[] intersections)
        {
            return TryIntersectWith(circle, line, out intersections, Tolerance.Global);
        }

        /// <summary>
        /// Tries to calculate the intersection points between a circle and a line segment within tolerance.
        /// </summary>
        public static bool TryIntersectWith(GeoCircle2 circle, GeoLine2 line, out GeoPoint2[] intersections, Tolerance tolerance)
        {
            GeoVector2 d = line.Direction;
            double dLength = d.Length;

            if (dLength <= tolerance.EqualPoint)
            {
                // Degenerate segment: it can only meet the circle if its single point lies on the circumference.
                if (Math.Abs(Distance2.DistanceTo(circle.Center, line.StartPoint) - circle.Radius) <= tolerance.EqualPoint)
                {
                    intersections = new[] { line.StartPoint };
                    return true;
                }
                intersections = Array.Empty<GeoPoint2>();
                return false;
            }

            // Solving b * b - 4 * a * c would compare a value in units of length to the fourth against a
            // length threshold, which makes the tangency band meaningless at large scales and far too wide
            // at small ones. Working from the perpendicular distance between the centre and the infinite
            // line keeps every comparison in the same units as EqualPoint.
            double centerParameter = line.StartPoint.GetVectorTo(circle.Center).DotProduct(d) / (dLength * dLength);
            double centerDistance = Distance2.DistanceTo(circle.Center, line.GetPointAtParameter(centerParameter));

            if (centerDistance > circle.Radius + tolerance.EqualPoint)
            {
                intersections = Array.Empty<GeoPoint2>();
                return false;
            }

            List<GeoPoint2> points = new List<GeoPoint2>(2);

            if (centerDistance >= circle.Radius - tolerance.EqualPoint)
            {
                // Tangent within tolerance: a single contact point at the foot of the perpendicular.
                AddPointOnSegment(points, line, centerParameter, dLength, tolerance);
            }
            else
            {
                double halfChord = Math.Sqrt(circle.Radius * circle.Radius - centerDistance * centerDistance) / dLength;
                AddPointOnSegment(points, line, centerParameter - halfChord, dLength, tolerance);
                AddPointOnSegment(points, line, centerParameter + halfChord, dLength, tolerance);
            }

            intersections = points.ToArray();
            return intersections.Length > 0;
        }

        /// <summary>
        /// Gets all intersection points between a circle and a line segment using default tolerance.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoCircle2 circle, GeoLine2 line)
        {
            return GetIntersections(circle, line, Tolerance.Global);
        }

        /// <summary>
        /// Gets all intersection points between a circle and a line segment within tolerance.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoCircle2 circle, GeoLine2 line, Tolerance tolerance)
        {
            TryIntersectWith(circle, line, out GeoPoint2[] pts, tolerance);
            return pts;
        }

        #endregion

        #region Circle - Circle

        /// <summary>
        /// Tries to calculate the intersection points between two circles using default tolerance.
        /// </summary>
        public static bool TryIntersectWith(GeoCircle2 c1, GeoCircle2 c2, out GeoPoint2[] intersections)
        {
            return TryIntersectWith(c1, c2, out intersections, Tolerance.Global);
        }

        /// <summary>
        /// Tries to calculate the intersection points between two circles within tolerance.
        /// </summary>
        public static bool TryIntersectWith(GeoCircle2 c1, GeoCircle2 c2, out GeoPoint2[] intersections, Tolerance tolerance)
        {
            double d = Distance2.DistanceTo(c1.Center, c2.Center);

            // Coincident circles or separate circles
            if (d > c1.Radius + c2.Radius + tolerance.EqualPoint ||
                d < Math.Abs(c1.Radius - c2.Radius) - tolerance.EqualPoint ||
                d <= tolerance.EqualPoint)
            {
                intersections = Array.Empty<GeoPoint2>();
                return false;
            }

            double a = (c1.Radius * c1.Radius - c2.Radius * c2.Radius + d * d) / (2.0 * d);
            double hSq = c1.Radius * c1.Radius - a * a;
            double h = hSq > 0 ? Math.Sqrt(hSq) : 0.0;

            GeoPoint2 p2 = new GeoPoint2(
                c1.Center.X + a * (c2.Center.X - c1.Center.X) / d,
                c1.Center.Y + a * (c2.Center.Y - c1.Center.Y) / d);

            if (h <= tolerance.EqualPoint)
            {
                intersections = new[] { p2 };
                return true;
            }

            double rx = -(c2.Center.Y - c1.Center.Y) * (h / d);
            double ry = (c2.Center.X - c1.Center.X) * (h / d);

            intersections = new[]
            {
                new GeoPoint2(p2.X + rx, p2.Y + ry),
                new GeoPoint2(p2.X - rx, p2.Y - ry)
            };
            return true;
        }

        /// <summary>
        /// Gets all intersection points between two circles using default tolerance.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoCircle2 c1, GeoCircle2 c2)
        {
            return GetIntersections(c1, c2, Tolerance.Global);
        }

        /// <summary>
        /// Gets all intersection points between two circles within tolerance.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoCircle2 c1, GeoCircle2 c2, Tolerance tolerance)
        {
            TryIntersectWith(c1, c2, out GeoPoint2[] pts, tolerance);
            return pts;
        }

        #endregion

        #region Rectangle - Shapes

        /// <summary>
        /// Gets all intersection points between a rectangle's boundary and a line segment using default tolerance.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoRectangle2 rect, GeoLine2 line)
        {
            return GetIntersections(rect, line, Tolerance.Global);
        }

        /// <summary>
        /// Gets all intersection points between a rectangle's boundary and a line segment within tolerance.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoRectangle2 rect, GeoLine2 line, Tolerance tolerance)
        {
            List<GeoPoint2> points = new List<GeoPoint2>();
            foreach (var edge in rect.GetEdges())
            {
                if (TryIntersectWith(edge, line, out GeoPoint2 pt, tolerance))
                {
                    AddUniquePoint(points, pt, tolerance);
                }
            }
            return points.ToArray();
        }

        /// <summary>
        /// Gets all intersection points between the boundaries of two rotated rectangles using default tolerance.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoRectangle2 rect1, GeoRectangle2 rect2)
        {
            return GetIntersections(rect1, rect2, Tolerance.Global);
        }

        /// <summary>
        /// Gets all intersection points between the boundaries of two rotated rectangles within tolerance.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoRectangle2 rect1, GeoRectangle2 rect2, Tolerance tolerance)
        {
            List<GeoPoint2> points = new List<GeoPoint2>();
            GeoLine2[] edges1 = rect1.GetEdges();
            GeoLine2[] edges2 = rect2.GetEdges();

            foreach (var e1 in edges1)
            {
                foreach (var e2 in edges2)
                {
                    if (TryIntersectWith(e1, e2, out GeoPoint2 pt, tolerance))
                    {
                        AddUniquePoint(points, pt, tolerance);
                    }
                }
            }

            return points.ToArray();
        }

        /// <summary>
        /// Gets all intersection points between a rectangle's boundary and a circle using default tolerance.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoRectangle2 rect, GeoCircle2 circle)
        {
            return GetIntersections(rect, circle, Tolerance.Global);
        }

        /// <summary>
        /// Gets all intersection points between a rectangle's boundary and a circle within tolerance.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoRectangle2 rect, GeoCircle2 circle, Tolerance tolerance)
        {
            List<GeoPoint2> points = new List<GeoPoint2>();
            foreach (var edge in rect.GetEdges())
            {
                if (TryIntersectWith(circle, edge, out GeoPoint2[] pts, tolerance))
                {
                    foreach (var pt in pts)
                    {
                        AddUniquePoint(points, pt, tolerance);
                    }
                }
            }
            return points.ToArray();
        }

        #endregion

        #region Polygon - Shapes

        /// <summary>
        /// Gets all intersection points between a polygon's boundary and a line segment using default tolerance.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoPolygon2 poly, GeoLine2 line)
        {
            return GetIntersections(poly, line, Tolerance.Global);
        }

        /// <summary>
        /// Gets all intersection points between a polygon's boundary and a line segment within tolerance.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoPolygon2 poly, GeoLine2 line, Tolerance tolerance)
        {
            if (poly == null) throw new ArgumentNullException(nameof(poly));

            List<GeoPoint2> points = new List<GeoPoint2>();
            foreach (var edge in poly.GetEdges())
            {
                if (TryIntersectWith(edge, line, out GeoPoint2 pt, tolerance))
                {
                    AddUniquePoint(points, pt, tolerance);
                }
            }
            return points.ToArray();
        }

        /// <summary>
        /// Gets all intersection points between the boundaries of two polygons using default tolerance.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoPolygon2 poly1, GeoPolygon2 poly2)
        {
            return GetIntersections(poly1, poly2, Tolerance.Global);
        }

        /// <summary>
        /// Gets all intersection points between the boundaries of two polygons within tolerance.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoPolygon2 poly1, GeoPolygon2 poly2, Tolerance tolerance)
        {
            if (poly1 == null) throw new ArgumentNullException(nameof(poly1));
            if (poly2 == null) throw new ArgumentNullException(nameof(poly2));

            List<GeoPoint2> points = new List<GeoPoint2>();
            foreach (var e1 in poly1.GetEdges())
            {
                foreach (var e2 in poly2.GetEdges())
                {
                    if (TryIntersectWith(e1, e2, out GeoPoint2 pt, tolerance))
                    {
                        AddUniquePoint(points, pt, tolerance);
                    }
                }
            }
            return points.ToArray();
        }

        /// <summary>
        /// Gets all intersection points between a polygon and a rectangle using default tolerance.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoPolygon2 poly, GeoRectangle2 rect)
        {
            return GetIntersections(poly, rect, Tolerance.Global);
        }

        /// <summary>
        /// Gets all intersection points between a polygon and a rectangle within tolerance.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoPolygon2 poly, GeoRectangle2 rect, Tolerance tolerance)
        {
            if (poly == null) throw new ArgumentNullException(nameof(poly));

            List<GeoPoint2> points = new List<GeoPoint2>();
            GeoLine2[] rectEdges = rect.GetEdges();

            foreach (var e1 in poly.GetEdges())
            {
                foreach (var e2 in rectEdges)
                {
                    if (TryIntersectWith(e1, e2, out GeoPoint2 pt, tolerance))
                    {
                        AddUniquePoint(points, pt, tolerance);
                    }
                }
            }
            return points.ToArray();
        }

        /// <summary>
        /// Gets all intersection points between a polygon and a circle using default tolerance.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoPolygon2 poly, GeoCircle2 circle)
        {
            return GetIntersections(poly, circle, Tolerance.Global);
        }

        /// <summary>
        /// Gets all intersection points between a polygon and a circle within tolerance.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoPolygon2 poly, GeoCircle2 circle, Tolerance tolerance)
        {
            if (poly == null) throw new ArgumentNullException(nameof(poly));

            List<GeoPoint2> points = new List<GeoPoint2>();
            foreach (var edge in poly.GetEdges())
            {
                if (TryIntersectWith(circle, edge, out GeoPoint2[] pts, tolerance))
                {
                    foreach (var pt in pts)
                    {
                        AddUniquePoint(points, pt, tolerance);
                    }
                }
            }
            return points.ToArray();
        }

        #endregion

        #region Polyline - Shapes

        /// <summary>
        /// Tries to calculate all intersection points between a polyline and a line segment using default tolerance.
        /// </summary>
        public static bool TryIntersectWith(GeoPolyline2 polyline, GeoLine2 line, out GeoPoint2[] intersections)
        {
            return TryIntersectWith(polyline, line, out intersections, Tolerance.Global);
        }

        /// <summary>
        /// Tries to calculate all intersection points between a polyline and a line segment within tolerance.
        /// </summary>
        public static bool TryIntersectWith(GeoPolyline2 polyline, GeoLine2 line, out GeoPoint2[] intersections, Tolerance tolerance)
        {
            if (polyline == null) throw new ArgumentNullException(nameof(polyline));

            List<GeoPoint2> points = new List<GeoPoint2>();
            for (int i = 0; i < polyline.EdgeCount; i++)
            {
                if (TryIntersectWith(polyline.GetEdgeAt(i), line, out GeoPoint2 pt, tolerance))
                {
                    AddUniquePoint(points, pt, tolerance);
                }
            }

            intersections = points.ToArray();
            return intersections.Length > 0;
        }

        /// <summary>
        /// Gets all intersection points between a polyline and a line segment using default tolerance.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoPolyline2 polyline, GeoLine2 line)
        {
            return GetIntersections(polyline, line, Tolerance.Global);
        }

        /// <summary>
        /// Gets all intersection points between a polyline and a line segment within tolerance.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoPolyline2 polyline, GeoLine2 line, Tolerance tolerance)
        {
            TryIntersectWith(polyline, line, out GeoPoint2[] pts, tolerance);
            return pts;
        }

        /// <summary>
        /// Gets all intersection points between two polylines using default tolerance.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoPolyline2 pl1, GeoPolyline2 pl2)
        {
            return GetIntersections(pl1, pl2, Tolerance.Global);
        }

        /// <summary>
        /// Gets all intersection points between two polylines within tolerance.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoPolyline2 pl1, GeoPolyline2 pl2, Tolerance tolerance)
        {
            if (pl1 == null) throw new ArgumentNullException(nameof(pl1));
            if (pl2 == null) throw new ArgumentNullException(nameof(pl2));

            List<GeoPoint2> points = new List<GeoPoint2>();
            for (int i = 0; i < pl1.EdgeCount; i++)
            {
                GeoLine2 e1 = pl1.GetEdgeAt(i);
                for (int j = 0; j < pl2.EdgeCount; j++)
                {
                    GeoLine2 e2 = pl2.GetEdgeAt(j);
                    if (TryIntersectWith(e1, e2, out GeoPoint2 pt, tolerance))
                    {
                        AddUniquePoint(points, pt, tolerance);
                    }
                }
            }

            return points.ToArray();
        }

        /// <summary>
        /// Gets all intersection points between a polyline and a rectangle using default tolerance.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoPolyline2 polyline, GeoRectangle2 rect)
        {
            return GetIntersections(polyline, rect, Tolerance.Global);
        }

        /// <summary>
        /// Gets all intersection points between a polyline and a rectangle within tolerance.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoPolyline2 polyline, GeoRectangle2 rect, Tolerance tolerance)
        {
            if (polyline == null) throw new ArgumentNullException(nameof(polyline));

            List<GeoPoint2> points = new List<GeoPoint2>();
            GeoLine2[] rectEdges = rect.GetEdges();

            for (int i = 0; i < polyline.EdgeCount; i++)
            {
                GeoLine2 e1 = polyline.GetEdgeAt(i);
                foreach (var e2 in rectEdges)
                {
                    if (TryIntersectWith(e1, e2, out GeoPoint2 pt, tolerance))
                    {
                        AddUniquePoint(points, pt, tolerance);
                    }
                }
            }

            return points.ToArray();
        }

        /// <summary>
        /// Gets all intersection points between a polyline and a circle using default tolerance.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoPolyline2 polyline, GeoCircle2 circle)
        {
            return GetIntersections(polyline, circle, Tolerance.Global);
        }

        /// <summary>
        /// Gets all intersection points between a polyline and a circle within tolerance.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoPolyline2 polyline, GeoCircle2 circle, Tolerance tolerance)
        {
            if (polyline == null) throw new ArgumentNullException(nameof(polyline));

            List<GeoPoint2> points = new List<GeoPoint2>();
            for (int i = 0; i < polyline.EdgeCount; i++)
            {
                if (TryIntersectWith(circle, polyline.GetEdgeAt(i), out GeoPoint2[] pts, tolerance))
                {
                    foreach (var pt in pts)
                    {
                        AddUniquePoint(points, pt, tolerance);
                    }
                }
            }

            return points.ToArray();
        }

        /// <summary>
        /// Gets all intersection points between a polyline and a polygon using default tolerance.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoPolyline2 polyline, GeoPolygon2 poly)
        {
            return GetIntersections(polyline, poly, Tolerance.Global);
        }

        /// <summary>
        /// Gets all intersection points between a polyline and a polygon within tolerance.
        /// </summary>
        public static GeoPoint2[] GetIntersections(GeoPolyline2 polyline, GeoPolygon2 poly, Tolerance tolerance)
        {
            if (polyline == null) throw new ArgumentNullException(nameof(polyline));
            if (poly == null) throw new ArgumentNullException(nameof(poly));

            List<GeoPoint2> points = new List<GeoPoint2>();
            for (int i = 0; i < polyline.EdgeCount; i++)
            {
                GeoLine2 e1 = polyline.GetEdgeAt(i);
                foreach (var e2 in poly.GetEdges())
                {
                    if (TryIntersectWith(e1, e2, out GeoPoint2 pt, tolerance))
                    {
                        AddUniquePoint(points, pt, tolerance);
                    }
                }
            }

            return points.ToArray();
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Adds the point at the given parameter when it falls on the segment, allowing EqualPoint of
        /// slack past either endpoint. The slack is converted from a distance into parameter space so
        /// that it stays the same real distance regardless of how long the segment is.
        /// </summary>
        private static void AddPointOnSegment(List<GeoPoint2> points, GeoLine2 line, double parameter, double lineLength, Tolerance tolerance)
        {
            double parameterTolerance = tolerance.EqualPoint / lineLength;
            if (parameter < -parameterTolerance || parameter > 1.0 + parameterTolerance)
            {
                return;
            }
            points.Add(line.GetPointAtParameter(Math.Max(0.0, Math.Min(1.0, parameter))));
        }

        /// <summary>
        /// Adds a point to the list only if it is not already present within the specified tolerance.
        /// </summary>
        /// <param name="list">The target list of points.</param>
        /// <param name="pt">The point to be added.</param>
        /// <param name="tolerance">The tolerance used to evaluate if points are equal.</param>
        private static void AddUniquePoint(List<GeoPoint2> list, GeoPoint2 pt, Tolerance tolerance)
        {
            foreach (var p in list)
            {
                if (p.IsEqualTo(pt, tolerance))
                {
                    return;
                }
            }
            list.Add(pt);
        }

        #endregion
    }
}
