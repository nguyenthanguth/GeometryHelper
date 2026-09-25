using System;
using System.Collections.Generic;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    /// <summary>
    /// Provides static calculation methods for geometric intersections and finding exact intersection points.
    /// </summary>
    public static partial class Intersection2
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
            return TryIntersectWith(line1, line2, LineExtension.None, out intersection, tolerance);
        }

        /// <summary>
        /// Tries to calculate the intersection point between two line segments, reading either or both as
        /// the infinite line carrying it, using default tolerance.
        /// </summary>
        /// <param name="line1">The first line segment.</param>
        /// <param name="line2">The second line segment.</param>
        /// <param name="extension">Which of the two may be reached past its endpoints.</param>
        /// <param name="intersection">The resulting intersection point if successful.</param>
        /// <returns>true if the lines meet within the reach allowed; otherwise, false.</returns>
        public static bool TryIntersectWith(GeoLine2 line1, GeoLine2 line2, LineExtension extension, out GeoPoint2 intersection)
        {
            return TryIntersectWith(line1, line2, extension, out intersection, Tolerance.Global);
        }

        /// <summary>
        /// Tries to calculate the intersection point between two line segments, reading either or both as
        /// the infinite line carrying it, within tolerance.
        /// </summary>
        /// <param name="line1">The first line segment.</param>
        /// <param name="line2">The second line segment.</param>
        /// <param name="extension">Which of the two may be reached past its endpoints.</param>
        /// <param name="intersection">The resulting intersection point if successful.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the lines meet within the reach allowed; otherwise, false.</returns>
        /// <remarks>
        /// This is AutoCAD's <c>IntersectWith</c> with its <c>Intersect</c> option: <see cref="LineExtension.Both"/>
        /// finds where two axes would cross however short they are drawn, <see cref="LineExtension.First"/>
        /// where the first would reach the second if it were extended. Parallel lines are refused whatever the
        /// extension, as they are without one: they either never meet or share a whole stretch, and neither is
        /// a single point. The point returned lies on every segment that was not extended.
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the extension is not a defined value.</exception>
        public static bool TryIntersectWith(GeoLine2 line1, GeoLine2 line2, LineExtension extension, out GeoPoint2 intersection, Tolerance tolerance)
        {
            bool extendFirst;
            bool extendSecond;

            switch (extension)
            {
                case LineExtension.None: extendFirst = false; extendSecond = false; break;
                case LineExtension.First: extendFirst = true; extendSecond = false; break;
                case LineExtension.Second: extendFirst = false; extendSecond = true; break;
                case LineExtension.Both: extendFirst = true; extendSecond = true; break;
                default: throw new ArgumentOutOfRangeException(nameof(extension), "Unknown line extension.");
            }

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

            if (!extendFirst && (t < -tTolerance || t > 1.0 + tTolerance))
            {
                return false;
            }

            if (!extendSecond && (u < -uTolerance || u > 1.0 + uTolerance))
            {
                return false;
            }

            // The point is placed on a segment that was not extended, clamped into it, so that a crossing
            // accepted within the endpoint slack still lies on that segment exactly.
            if (!extendFirst)
            {
                intersection = line1.GetPointAtParameter(Math.Max(0.0, Math.Min(1.0, t)));
            }
            else if (!extendSecond)
            {
                intersection = line2.GetPointAtParameter(Math.Max(0.0, Math.Min(1.0, u)));
            }
            else
            {
                intersection = line1.GetPointAtParameter(t);
            }

            return true;
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

        /// <summary>
        /// Gets the intersection point between two line segments, reading either or both as the infinite line
        /// carrying it, using default tolerance. Returns null if they do not meet within the reach allowed.
        /// </summary>
        public static GeoPoint2? GetIntersection(GeoLine2 line1, GeoLine2 line2, LineExtension extension)
        {
            return GetIntersection(line1, line2, extension, Tolerance.Global);
        }

        /// <summary>
        /// Gets the intersection point between two line segments, reading either or both as the infinite line
        /// carrying it, within tolerance. Returns null if they do not meet within the reach allowed.
        /// </summary>
        public static GeoPoint2? GetIntersection(GeoLine2 line1, GeoLine2 line2, LineExtension extension, Tolerance tolerance)
        {
            return TryIntersectWith(line1, line2, extension, out GeoPoint2 pt, tolerance) ? pt : (GeoPoint2?)null;
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

        #region Simplicity

        /// <summary>
        /// Checks whether a polygon is simple, using default tolerance: no edge crosses or touches another
        /// except where neighbours share their vertex.
        /// </summary>
        /// <param name="polygon">The polygon.</param>
        /// <returns>true if the polygon is simple; otherwise, false.</returns>
        public static bool IsSimple(GeoPolygon2 polygon)
        {
            return IsSimple(polygon, Tolerance.Global);
        }

        /// <summary>
        /// Checks whether a polygon is simple, within tolerance: no edge crosses or touches another except
        /// where neighbours share their vertex.
        /// </summary>
        /// <param name="polygon">The polygon.</param>
        /// <param name="tolerance">The tolerance: edges closer than the point tolerance count as touching.</param>
        /// <returns>true if the polygon is simple; otherwise, false.</returns>
        /// <remarks>
        /// <see cref="GeoPolygon2"/> does not check this when it is built, because the check costs more than
        /// building it. A polygon that is not simple is still read consistently, under the even-odd rule, but
        /// its area no longer means what it says. A vertex touching another edge counts as not simple, and so
        /// does an edge folding back over its neighbour. Edges are swept in order along X, so the cost grows
        /// with the number of edges whose extents overlap rather than with the square of the edge count.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public static bool IsSimple(GeoPolygon2 polygon, Tolerance tolerance)
        {
            if (polygon == null) throw new ArgumentNullException(nameof(polygon));

            int count = polygon.EdgeCount;
            GeoLine2[] edges = new GeoLine2[count];
            double[] minX = new double[count];
            double[] maxX = new double[count];
            double[] minY = new double[count];
            double[] maxY = new double[count];
            int[] order = new int[count];

            for (int i = 0; i < count; i++)
            {
                edges[i] = polygon.GetEdgeAt(i);
                minX[i] = Math.Min(edges[i].StartPoint.X, edges[i].EndPoint.X);
                maxX[i] = Math.Max(edges[i].StartPoint.X, edges[i].EndPoint.X);
                minY[i] = Math.Min(edges[i].StartPoint.Y, edges[i].EndPoint.Y);
                maxY[i] = Math.Max(edges[i].StartPoint.Y, edges[i].EndPoint.Y);
                order[i] = i;
            }

            double[] keys = (double[])minX.Clone();
            Array.Sort(keys, order);

            List<int> active = new List<int>();
            double slack = tolerance.EqualPoint;

            foreach (int i in order)
            {
                active.RemoveAll(j => maxX[j] < minX[i] - slack);

                foreach (int j in active)
                {
                    if (maxY[j] < minY[i] - slack || minY[j] > maxY[i] + slack)
                    {
                        continue;
                    }

                    bool iThenJ = j == (i + 1) % count;
                    bool jThenI = i == (j + 1) % count;

                    if (iThenJ || jThenI)
                    {
                        // Neighbours share a vertex and must meet nowhere else: the far end of either lying on
                        // the other means the boundary folds back over itself.
                        GeoLine2 incoming = iThenJ ? edges[i] : edges[j];
                        GeoLine2 outgoing = iThenJ ? edges[j] : edges[i];

                        if (Containment2.IsPointOn(outgoing, incoming.StartPoint, tolerance) ||
                            Containment2.IsPointOn(incoming, outgoing.EndPoint, tolerance))
                        {
                            return false;
                        }
                    }
                    else if (Collision2.CollidesWith(edges[i], edges[j], tolerance))
                    {
                        return false;
                    }
                }

                active.Add(i);
            }

            return true;
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
        #region Chains that may curve

        /// <summary>
        /// Gets the points where a chain that may curve meets a straight segment.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static GeoPoint2[] GetIntersections(GeoPolylineArc2 chain, GeoLine2 line) => GetIntersections(chain, line, Tolerance.Global);

        /// <summary>
        /// Gets the points where a chain that may curve meets a straight segment, within a tolerance.
        /// </summary>
        /// <remarks>
        /// Every meeting is worked out on the arcs themselves, and a point two edges share is reported
        /// once rather than twice.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static GeoPoint2[] GetIntersections(GeoPolylineArc2 chain, GeoLine2 line, Tolerance tolerance)
        {
            if (chain == null) throw new ArgumentNullException(nameof(chain));
            
            return ArcChain2.Intersections(ArcChain2.EdgesOf(chain), line, tolerance);
        }

        /// <summary>
        /// Tries to find where a chain that may curve meets a straight segment.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static bool TryIntersectWith(GeoPolylineArc2 chain, GeoLine2 line, out GeoPoint2[] intersections) => TryIntersectWith(chain, line, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where a chain that may curve meets a straight segment, within a tolerance.
        /// </summary>
        /// <returns>true when they meet anywhere; otherwise, false.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static bool TryIntersectWith(GeoPolylineArc2 chain, GeoLine2 line, out GeoPoint2[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(chain, line, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Gets the points where a chain that may curve meets an arc.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static GeoPoint2[] GetIntersections(GeoPolylineArc2 chain, GeoArc2 arc) => GetIntersections(chain, arc, Tolerance.Global);

        /// <summary>
        /// Gets the points where a chain that may curve meets an arc, within a tolerance.
        /// </summary>
        /// <remarks>
        /// Every meeting is worked out on the arcs themselves, and a point two edges share is reported
        /// once rather than twice.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static GeoPoint2[] GetIntersections(GeoPolylineArc2 chain, GeoArc2 arc, Tolerance tolerance)
        {
            if (chain == null) throw new ArgumentNullException(nameof(chain));
            
            return ArcChain2.Intersections(ArcChain2.EdgesOf(chain), arc, tolerance);
        }

        /// <summary>
        /// Tries to find where a chain that may curve meets an arc.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static bool TryIntersectWith(GeoPolylineArc2 chain, GeoArc2 arc, out GeoPoint2[] intersections) => TryIntersectWith(chain, arc, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where a chain that may curve meets an arc, within a tolerance.
        /// </summary>
        /// <returns>true when they meet anywhere; otherwise, false.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static bool TryIntersectWith(GeoPolylineArc2 chain, GeoArc2 arc, out GeoPoint2[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(chain, arc, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Gets the points where a chain that may curve meets a circle.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static GeoPoint2[] GetIntersections(GeoPolylineArc2 chain, GeoCircle2 circle) => GetIntersections(chain, circle, Tolerance.Global);

        /// <summary>
        /// Gets the points where a chain that may curve meets a circle, within a tolerance.
        /// </summary>
        /// <remarks>
        /// Every meeting is worked out on the arcs themselves, and a point two edges share is reported
        /// once rather than twice.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static GeoPoint2[] GetIntersections(GeoPolylineArc2 chain, GeoCircle2 circle, Tolerance tolerance)
        {
            if (chain == null) throw new ArgumentNullException(nameof(chain));
            
            return ArcChain2.Intersections(ArcChain2.EdgesOf(chain), circle, tolerance);
        }

        /// <summary>
        /// Tries to find where a chain that may curve meets a circle.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static bool TryIntersectWith(GeoPolylineArc2 chain, GeoCircle2 circle, out GeoPoint2[] intersections) => TryIntersectWith(chain, circle, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where a chain that may curve meets a circle, within a tolerance.
        /// </summary>
        /// <returns>true when they meet anywhere; otherwise, false.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static bool TryIntersectWith(GeoPolylineArc2 chain, GeoCircle2 circle, out GeoPoint2[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(chain, circle, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Gets the points where a chain that may curve meets a straight chain.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static GeoPoint2[] GetIntersections(GeoPolylineArc2 chain, GeoPolyline2 polyline) => GetIntersections(chain, polyline, Tolerance.Global);

        /// <summary>
        /// Gets the points where a chain that may curve meets a straight chain, within a tolerance.
        /// </summary>
        /// <remarks>
        /// Every meeting is worked out on the arcs themselves, and a point two edges share is reported
        /// once rather than twice.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static GeoPoint2[] GetIntersections(GeoPolylineArc2 chain, GeoPolyline2 polyline, Tolerance tolerance)
        {
            if (chain == null) throw new ArgumentNullException(nameof(chain));
            if (polyline == null) throw new ArgumentNullException(nameof(polyline));
            
            return ArcChain2.Intersections(ArcChain2.EdgesOf(chain), ArcChain2.EdgesOf(polyline), tolerance);
        }

        /// <summary>
        /// Tries to find where a chain that may curve meets a straight chain.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static bool TryIntersectWith(GeoPolylineArc2 chain, GeoPolyline2 polyline, out GeoPoint2[] intersections) => TryIntersectWith(chain, polyline, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where a chain that may curve meets a straight chain, within a tolerance.
        /// </summary>
        /// <returns>true when they meet anywhere; otherwise, false.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static bool TryIntersectWith(GeoPolylineArc2 chain, GeoPolyline2 polyline, out GeoPoint2[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(chain, polyline, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Gets the points where a chain that may curve meets a straight loop.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static GeoPoint2[] GetIntersections(GeoPolylineArc2 chain, GeoPolygon2 polygon) => GetIntersections(chain, polygon, Tolerance.Global);

        /// <summary>
        /// Gets the points where a chain that may curve meets a straight loop, within a tolerance.
        /// </summary>
        /// <remarks>
        /// Every meeting is worked out on the arcs themselves, and a point two edges share is reported
        /// once rather than twice.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static GeoPoint2[] GetIntersections(GeoPolylineArc2 chain, GeoPolygon2 polygon, Tolerance tolerance)
        {
            if (chain == null) throw new ArgumentNullException(nameof(chain));
            if (polygon == null) throw new ArgumentNullException(nameof(polygon));
            
            return ArcChain2.Intersections(ArcChain2.EdgesOf(chain), ArcChain2.EdgesOf(polygon), tolerance);
        }

        /// <summary>
        /// Tries to find where a chain that may curve meets a straight loop.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static bool TryIntersectWith(GeoPolylineArc2 chain, GeoPolygon2 polygon, out GeoPoint2[] intersections) => TryIntersectWith(chain, polygon, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where a chain that may curve meets a straight loop, within a tolerance.
        /// </summary>
        /// <returns>true when they meet anywhere; otherwise, false.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static bool TryIntersectWith(GeoPolylineArc2 chain, GeoPolygon2 polygon, out GeoPoint2[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(chain, polygon, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Gets the points where a chain that may curve meets a chain that may curve.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static GeoPoint2[] GetIntersections(GeoPolylineArc2 chain, GeoPolylineArc2 other) => GetIntersections(chain, other, Tolerance.Global);

        /// <summary>
        /// Gets the points where a chain that may curve meets a chain that may curve, within a tolerance.
        /// </summary>
        /// <remarks>
        /// Every meeting is worked out on the arcs themselves, and a point two edges share is reported
        /// once rather than twice.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static GeoPoint2[] GetIntersections(GeoPolylineArc2 chain, GeoPolylineArc2 other, Tolerance tolerance)
        {
            if (chain == null) throw new ArgumentNullException(nameof(chain));
            if (other == null) throw new ArgumentNullException(nameof(other));
            
            return ArcChain2.Intersections(ArcChain2.EdgesOf(chain), ArcChain2.EdgesOf(other), tolerance);
        }

        /// <summary>
        /// Tries to find where a chain that may curve meets a chain that may curve.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static bool TryIntersectWith(GeoPolylineArc2 chain, GeoPolylineArc2 other, out GeoPoint2[] intersections) => TryIntersectWith(chain, other, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where a chain that may curve meets a chain that may curve, within a tolerance.
        /// </summary>
        /// <returns>true when they meet anywhere; otherwise, false.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static bool TryIntersectWith(GeoPolylineArc2 chain, GeoPolylineArc2 other, out GeoPoint2[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(chain, other, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Gets the points where a chain that may curve meets a loop that may curve.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static GeoPoint2[] GetIntersections(GeoPolylineArc2 chain, GeoPolygonArc2 loop) => GetIntersections(chain, loop, Tolerance.Global);

        /// <summary>
        /// Gets the points where a chain that may curve meets a loop that may curve, within a tolerance.
        /// </summary>
        /// <remarks>
        /// Every meeting is worked out on the arcs themselves, and a point two edges share is reported
        /// once rather than twice.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static GeoPoint2[] GetIntersections(GeoPolylineArc2 chain, GeoPolygonArc2 loop, Tolerance tolerance)
        {
            if (chain == null) throw new ArgumentNullException(nameof(chain));
            if (loop == null) throw new ArgumentNullException(nameof(loop));
            
            return ArcChain2.Intersections(ArcChain2.EdgesOf(chain), ArcChain2.EdgesOf(loop), tolerance);
        }

        /// <summary>
        /// Tries to find where a chain that may curve meets a loop that may curve.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static bool TryIntersectWith(GeoPolylineArc2 chain, GeoPolygonArc2 loop, out GeoPoint2[] intersections) => TryIntersectWith(chain, loop, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where a chain that may curve meets a loop that may curve, within a tolerance.
        /// </summary>
        /// <returns>true when they meet anywhere; otherwise, false.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static bool TryIntersectWith(GeoPolylineArc2 chain, GeoPolygonArc2 loop, out GeoPoint2[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(chain, loop, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Gets the points where a loop that may curve meets a straight segment.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public static GeoPoint2[] GetIntersections(GeoPolygonArc2 loop, GeoLine2 line) => GetIntersections(loop, line, Tolerance.Global);

        /// <summary>
        /// Gets the points where a loop that may curve meets a straight segment, within a tolerance.
        /// </summary>
        /// <remarks>
        /// Every meeting is worked out on the arcs themselves, and a point two edges share is reported
        /// once rather than twice.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public static GeoPoint2[] GetIntersections(GeoPolygonArc2 loop, GeoLine2 line, Tolerance tolerance)
        {
            if (loop == null) throw new ArgumentNullException(nameof(loop));
            
            return ArcChain2.Intersections(ArcChain2.EdgesOf(loop), line, tolerance);
        }

        /// <summary>
        /// Tries to find where a loop that may curve meets a straight segment.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public static bool TryIntersectWith(GeoPolygonArc2 loop, GeoLine2 line, out GeoPoint2[] intersections) => TryIntersectWith(loop, line, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where a loop that may curve meets a straight segment, within a tolerance.
        /// </summary>
        /// <returns>true when they meet anywhere; otherwise, false.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public static bool TryIntersectWith(GeoPolygonArc2 loop, GeoLine2 line, out GeoPoint2[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(loop, line, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Gets the points where a loop that may curve meets an arc.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public static GeoPoint2[] GetIntersections(GeoPolygonArc2 loop, GeoArc2 arc) => GetIntersections(loop, arc, Tolerance.Global);

        /// <summary>
        /// Gets the points where a loop that may curve meets an arc, within a tolerance.
        /// </summary>
        /// <remarks>
        /// Every meeting is worked out on the arcs themselves, and a point two edges share is reported
        /// once rather than twice.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public static GeoPoint2[] GetIntersections(GeoPolygonArc2 loop, GeoArc2 arc, Tolerance tolerance)
        {
            if (loop == null) throw new ArgumentNullException(nameof(loop));
            
            return ArcChain2.Intersections(ArcChain2.EdgesOf(loop), arc, tolerance);
        }

        /// <summary>
        /// Tries to find where a loop that may curve meets an arc.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public static bool TryIntersectWith(GeoPolygonArc2 loop, GeoArc2 arc, out GeoPoint2[] intersections) => TryIntersectWith(loop, arc, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where a loop that may curve meets an arc, within a tolerance.
        /// </summary>
        /// <returns>true when they meet anywhere; otherwise, false.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public static bool TryIntersectWith(GeoPolygonArc2 loop, GeoArc2 arc, out GeoPoint2[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(loop, arc, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Gets the points where a loop that may curve meets a circle.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public static GeoPoint2[] GetIntersections(GeoPolygonArc2 loop, GeoCircle2 circle) => GetIntersections(loop, circle, Tolerance.Global);

        /// <summary>
        /// Gets the points where a loop that may curve meets a circle, within a tolerance.
        /// </summary>
        /// <remarks>
        /// Every meeting is worked out on the arcs themselves, and a point two edges share is reported
        /// once rather than twice.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public static GeoPoint2[] GetIntersections(GeoPolygonArc2 loop, GeoCircle2 circle, Tolerance tolerance)
        {
            if (loop == null) throw new ArgumentNullException(nameof(loop));
            
            return ArcChain2.Intersections(ArcChain2.EdgesOf(loop), circle, tolerance);
        }

        /// <summary>
        /// Tries to find where a loop that may curve meets a circle.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public static bool TryIntersectWith(GeoPolygonArc2 loop, GeoCircle2 circle, out GeoPoint2[] intersections) => TryIntersectWith(loop, circle, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where a loop that may curve meets a circle, within a tolerance.
        /// </summary>
        /// <returns>true when they meet anywhere; otherwise, false.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public static bool TryIntersectWith(GeoPolygonArc2 loop, GeoCircle2 circle, out GeoPoint2[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(loop, circle, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Gets the points where a loop that may curve meets a straight chain.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public static GeoPoint2[] GetIntersections(GeoPolygonArc2 loop, GeoPolyline2 polyline) => GetIntersections(loop, polyline, Tolerance.Global);

        /// <summary>
        /// Gets the points where a loop that may curve meets a straight chain, within a tolerance.
        /// </summary>
        /// <remarks>
        /// Every meeting is worked out on the arcs themselves, and a point two edges share is reported
        /// once rather than twice.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public static GeoPoint2[] GetIntersections(GeoPolygonArc2 loop, GeoPolyline2 polyline, Tolerance tolerance)
        {
            if (loop == null) throw new ArgumentNullException(nameof(loop));
            if (polyline == null) throw new ArgumentNullException(nameof(polyline));
            
            return ArcChain2.Intersections(ArcChain2.EdgesOf(loop), ArcChain2.EdgesOf(polyline), tolerance);
        }

        /// <summary>
        /// Tries to find where a loop that may curve meets a straight chain.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public static bool TryIntersectWith(GeoPolygonArc2 loop, GeoPolyline2 polyline, out GeoPoint2[] intersections) => TryIntersectWith(loop, polyline, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where a loop that may curve meets a straight chain, within a tolerance.
        /// </summary>
        /// <returns>true when they meet anywhere; otherwise, false.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public static bool TryIntersectWith(GeoPolygonArc2 loop, GeoPolyline2 polyline, out GeoPoint2[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(loop, polyline, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Gets the points where a loop that may curve meets a straight loop.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public static GeoPoint2[] GetIntersections(GeoPolygonArc2 loop, GeoPolygon2 polygon) => GetIntersections(loop, polygon, Tolerance.Global);

        /// <summary>
        /// Gets the points where a loop that may curve meets a straight loop, within a tolerance.
        /// </summary>
        /// <remarks>
        /// Every meeting is worked out on the arcs themselves, and a point two edges share is reported
        /// once rather than twice.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public static GeoPoint2[] GetIntersections(GeoPolygonArc2 loop, GeoPolygon2 polygon, Tolerance tolerance)
        {
            if (loop == null) throw new ArgumentNullException(nameof(loop));
            if (polygon == null) throw new ArgumentNullException(nameof(polygon));
            
            return ArcChain2.Intersections(ArcChain2.EdgesOf(loop), ArcChain2.EdgesOf(polygon), tolerance);
        }

        /// <summary>
        /// Tries to find where a loop that may curve meets a straight loop.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public static bool TryIntersectWith(GeoPolygonArc2 loop, GeoPolygon2 polygon, out GeoPoint2[] intersections) => TryIntersectWith(loop, polygon, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where a loop that may curve meets a straight loop, within a tolerance.
        /// </summary>
        /// <returns>true when they meet anywhere; otherwise, false.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public static bool TryIntersectWith(GeoPolygonArc2 loop, GeoPolygon2 polygon, out GeoPoint2[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(loop, polygon, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Gets the points where a loop that may curve meets a chain that may curve.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public static GeoPoint2[] GetIntersections(GeoPolygonArc2 loop, GeoPolylineArc2 chain) => GetIntersections(loop, chain, Tolerance.Global);

        /// <summary>
        /// Gets the points where a loop that may curve meets a chain that may curve, within a tolerance.
        /// </summary>
        /// <remarks>
        /// Every meeting is worked out on the arcs themselves, and a point two edges share is reported
        /// once rather than twice.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public static GeoPoint2[] GetIntersections(GeoPolygonArc2 loop, GeoPolylineArc2 chain, Tolerance tolerance)
        {
            if (loop == null) throw new ArgumentNullException(nameof(loop));
            if (chain == null) throw new ArgumentNullException(nameof(chain));
            
            return ArcChain2.Intersections(ArcChain2.EdgesOf(loop), ArcChain2.EdgesOf(chain), tolerance);
        }

        /// <summary>
        /// Tries to find where a loop that may curve meets a chain that may curve.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public static bool TryIntersectWith(GeoPolygonArc2 loop, GeoPolylineArc2 chain, out GeoPoint2[] intersections) => TryIntersectWith(loop, chain, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where a loop that may curve meets a chain that may curve, within a tolerance.
        /// </summary>
        /// <returns>true when they meet anywhere; otherwise, false.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public static bool TryIntersectWith(GeoPolygonArc2 loop, GeoPolylineArc2 chain, out GeoPoint2[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(loop, chain, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Gets the points where a loop that may curve meets a loop that may curve.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public static GeoPoint2[] GetIntersections(GeoPolygonArc2 loop, GeoPolygonArc2 other) => GetIntersections(loop, other, Tolerance.Global);

        /// <summary>
        /// Gets the points where a loop that may curve meets a loop that may curve, within a tolerance.
        /// </summary>
        /// <remarks>
        /// Every meeting is worked out on the arcs themselves, and a point two edges share is reported
        /// once rather than twice.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public static GeoPoint2[] GetIntersections(GeoPolygonArc2 loop, GeoPolygonArc2 other, Tolerance tolerance)
        {
            if (loop == null) throw new ArgumentNullException(nameof(loop));
            if (other == null) throw new ArgumentNullException(nameof(other));
            
            return ArcChain2.Intersections(ArcChain2.EdgesOf(loop), ArcChain2.EdgesOf(other), tolerance);
        }

        /// <summary>
        /// Tries to find where a loop that may curve meets a loop that may curve.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public static bool TryIntersectWith(GeoPolygonArc2 loop, GeoPolygonArc2 other, out GeoPoint2[] intersections) => TryIntersectWith(loop, other, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where a loop that may curve meets a loop that may curve, within a tolerance.
        /// </summary>
        /// <returns>true when they meet anywhere; otherwise, false.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public static bool TryIntersectWith(GeoPolygonArc2 loop, GeoPolygonArc2 other, out GeoPoint2[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(loop, other, tolerance);

            return intersections.Length > 0;
        }

        #endregion

    }
}
