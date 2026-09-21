using System;
using System.Collections.Generic;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    /// <summary>
    /// Provides static methods for spatial containment, inclusion, and boundary point tests.
    /// </summary>
    public static class Containment2
    {
        #region Point on Line / Circle / Polygon

        /// <summary>
        /// Checks whether a point lies on the line segment using default tolerance.
        /// </summary>
        /// <param name="line">The line segment.</param>
        /// <param name="point">The target point.</param>
        /// <returns>true if the point lies on the line segment; otherwise, false.</returns>
        public static bool IsPointOn(GeoLine2 line, GeoPoint2 point)
        {
            return IsPointOn(line, point, Tolerance.Global);
        }

        /// <summary>
        /// Checks whether a point lies on the line segment within tolerance.
        /// </summary>
        /// <param name="line">The line segment.</param>
        /// <param name="point">The target point.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the point lies on the line segment within tolerance; otherwise, false.</returns>
        public static bool IsPointOn(GeoLine2 line, GeoPoint2 point, Tolerance tolerance)
        {
            return Distance2.DistanceTo(line, point) <= tolerance.EqualPoint;
        }

        /// <summary>
        /// Checks whether a point lies on the circumference of a circle using default tolerance.
        /// </summary>
        /// <param name="circle">The circle.</param>
        /// <param name="point">The target point.</param>
        /// <returns>true if the point lies on the circle boundary; otherwise, false.</returns>
        public static bool IsPointOn(GeoCircle2 circle, GeoPoint2 point)
        {
            return IsPointOn(circle, point, Tolerance.Global);
        }

        /// <summary>
        /// Checks whether a point lies on the circumference of a circle within tolerance.
        /// </summary>
        /// <param name="circle">The circle.</param>
        /// <param name="point">The target point.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the point lies on the circle boundary within tolerance; otherwise, false.</returns>
        public static bool IsPointOn(GeoCircle2 circle, GeoPoint2 point, Tolerance tolerance)
        {
            return Math.Abs(Distance2.DistanceTo(circle.Center, point) - circle.Radius) <= tolerance.EqualPoint;
        }

        /// <summary>
        /// Checks whether a point lies on any boundary edge of the polygon using default tolerance.
        /// </summary>
        /// <param name="poly">The polygon.</param>
        /// <param name="point">The target point.</param>
        /// <returns>true if the point lies on the polygon boundary; otherwise, false.</returns>
        public static bool IsPointOn(GeoPolygon2 poly, GeoPoint2 point)
        {
            return IsPointOn(poly, point, Tolerance.Global);
        }

        /// <summary>
        /// Checks whether a point lies on any boundary edge of the polygon within tolerance.
        /// </summary>
        /// <param name="poly">The polygon.</param>
        /// <param name="point">The target point.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the point lies on the polygon boundary within tolerance; otherwise, false.</returns>
        public static bool IsPointOn(GeoPolygon2 poly, GeoPoint2 point, Tolerance tolerance)
        {
            if (poly == null) throw new ArgumentNullException(nameof(poly));

            for (int i = 0; i < poly.EdgeCount; i++)
            {
                if (IsPointOn(poly.GetEdgeAt(i), point, tolerance))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Checks whether a point lies on any segment of the polyline using default tolerance.
        /// </summary>
        /// <param name="polyline">The polyline.</param>
        /// <param name="point">The target point.</param>
        /// <returns>true if the point lies on the polyline; otherwise, false.</returns>
        public static bool IsPointOn(GeoPolyline2 polyline, GeoPoint2 point)
        {
            return IsPointOn(polyline, point, Tolerance.Global);
        }

        /// <summary>
        /// Checks whether a point lies on any segment of the polyline within tolerance.
        /// </summary>
        /// <param name="polyline">The polyline.</param>
        /// <param name="point">The target point.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the point lies on the polyline within tolerance; otherwise, false.</returns>
        public static bool IsPointOn(GeoPolyline2 polyline, GeoPoint2 point, Tolerance tolerance)
        {
            if (polyline == null) throw new ArgumentNullException(nameof(polyline));

            for (int i = 0; i < polyline.EdgeCount; i++)
            {
                if (IsPointOn(polyline.GetEdgeAt(i), point, tolerance))
                {
                    return true;
                }
            }
            return false;
        }

        #endregion

        #region Contains Point

        /// <summary>
        /// Checks whether a circle contains a point using default tolerance.
        /// </summary>
        /// <param name="circle">The circle.</param>
        /// <param name="point">The target point.</param>
        /// <returns>true if the circle contains the point; otherwise, false.</returns>
        public static bool Contains(GeoCircle2 circle, GeoPoint2 point)
        {
            return Contains(circle, point, Tolerance.Global);
        }

        /// <summary>
        /// Checks whether a circle contains a point within tolerance.
        /// </summary>
        /// <param name="circle">The circle.</param>
        /// <param name="point">The target point.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the circle contains the point within tolerance; otherwise, false.</returns>
        public static bool Contains(GeoCircle2 circle, GeoPoint2 point, Tolerance tolerance)
        {
            return Distance2.DistanceTo(circle.Center, point) <= circle.Radius + tolerance.EqualPoint;
        }

        /// <summary>
        /// Checks whether a rotated rectangle (GeoRectangle2 OBB) contains a point using default tolerance
        /// (accepts points on the boundary).
        /// </summary>
        /// <param name="rect">The rectangle.</param>
        /// <param name="point">The target point.</param>
        /// <returns>true if the rectangle contains the point; otherwise, false.</returns>
        public static bool Contains(GeoRectangle2 rect, GeoPoint2 point)
        {
            return Contains(rect, point, Tolerance.Global);
        }

        /// <summary>
        /// Checks whether a rotated rectangle (GeoRectangle2 OBB) contains a point within tolerance
        /// (accepts points on the boundary).
        /// </summary>
        /// <param name="rect">The rectangle.</param>
        /// <param name="point">The target point.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the rectangle contains the point; otherwise, false.</returns>
        public static bool Contains(GeoRectangle2 rect, GeoPoint2 point, Tolerance tolerance)
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

            return localX >= -halfW - tolerance.EqualPoint && localX <= halfW + tolerance.EqualPoint &&
                   localY >= -halfH - tolerance.EqualPoint && localY <= halfH + tolerance.EqualPoint;
        }

        /// <summary>
        /// Checks whether a polygon contains a point using default tolerance (accepts points on the boundary).
        /// </summary>
        /// <param name="poly">The polygon.</param>
        /// <param name="point">The target point.</param>
        /// <returns>true if the polygon contains the point; otherwise, false.</returns>
        public static bool Contains(GeoPolygon2 poly, GeoPoint2 point)
        {
            return Contains(poly, point, Tolerance.Global);
        }

        /// <summary>
        /// Checks whether a polygon contains a point using the Ray Casting algorithm (accepts points on the boundary).
        /// </summary>
        /// <param name="poly">The polygon.</param>
        /// <param name="point">The target point.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the polygon contains the point; otherwise, false.</returns>
        /// <remarks>
        /// Counting crossings makes this the even-odd rule, which matters only when the polygon is not
        /// simple: a region its edges enclose twice reads as outside. That is a definition rather than a
        /// failure, and it is the definition every operation resting on this one inherits.
        /// </remarks>
        public static bool Contains(GeoPolygon2 poly, GeoPoint2 point, Tolerance tolerance)
        {
            if (poly == null) throw new ArgumentNullException(nameof(poly));

            // Check if point is on the polygon boundary first
            if (IsPointOn(poly, point, tolerance))
            {
                return true;
            }

            bool inside = false;
            int n = poly.VertexCount;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                GeoPoint2 p1 = poly[i];
                GeoPoint2 p2 = poly[j];

                if (((p1.Y > point.Y) != (p2.Y > point.Y)) &&
                    (point.X < (p2.X - p1.X) * (point.Y - p1.Y) / (p2.Y - p1.Y) + p1.X))
                {
                    inside = !inside;
                }
            }

            return inside;
        }

        // A polyline has no interior, so there is no Contains overload taking one as the container:
        // it would only ever mean "the point lies on the path", which is what IsPointOn already says.
        // Asking about an enclosed area means converting to a GeoPolygon2 first.

        #endregion

        #region Locate Point (Inside / OutSide / OnSide)

        /// <summary>
        /// Classifies the spatial location of a point relative to a circle using default tolerance.
        /// </summary>
        /// <param name="circle">The circle.</param>
        /// <param name="point">The target point.</param>
        /// <returns>PointLocation.Inside, PointLocation.OnSide, or PointLocation.OutSide.</returns>
        public static PointLocation Locate(GeoCircle2 circle, GeoPoint2 point)
        {
            return Locate(circle, point, Tolerance.Global);
        }

        /// <summary>
        /// Classifies the spatial location of a point relative to a circle within tolerance.
        /// </summary>
        /// <param name="circle">The circle.</param>
        /// <param name="point">The target point.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>PointLocation.Inside, PointLocation.OnSide, or PointLocation.OutSide.</returns>
        public static PointLocation Locate(GeoCircle2 circle, GeoPoint2 point, Tolerance tolerance)
        {
            double dist = Distance2.DistanceTo(circle.Center, point);
            double diff = dist - circle.Radius;

            if (Math.Abs(diff) <= tolerance.EqualPoint)
            {
                return PointLocation.OnSide;
            }

            if (diff < -tolerance.EqualPoint)
            {
                return PointLocation.Inside;
            }

            return PointLocation.OutSide;
        }

        /// <summary>
        /// Classifies the spatial location of a point relative to a rotated rectangle (GeoRectangle2 OBB) using default tolerance.
        /// </summary>
        /// <param name="rect">The rectangle.</param>
        /// <param name="point">The target point.</param>
        /// <returns>PointLocation.Inside, PointLocation.OnSide, or PointLocation.OutSide.</returns>
        public static PointLocation Locate(GeoRectangle2 rect, GeoPoint2 point)
        {
            return Locate(rect, point, Tolerance.Global);
        }

        /// <summary>
        /// Classifies the spatial location of a point relative to a rotated rectangle (GeoRectangle2 OBB) within tolerance.
        /// </summary>
        /// <param name="rect">The rectangle.</param>
        /// <param name="point">The target point.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>PointLocation.Inside, PointLocation.OnSide, or PointLocation.OutSide.</returns>
        public static PointLocation Locate(GeoRectangle2 rect, GeoPoint2 point, Tolerance tolerance)
        {
            double dx = point.X - rect.Center.X;
            double dy = point.Y - rect.Center.Y;

            double cos = Math.Cos(rect.AngleRad);
            double sin = Math.Sin(rect.AngleRad);

            // Project point onto local rectangle coordinate system
            double localX = dx * cos + dy * sin;
            double localY = -dx * sin + dy * cos;

            double absX = Math.Abs(localX);
            double absY = Math.Abs(localY);

            double halfW = rect.Width * 0.5;
            double halfH = rect.Height * 0.5;

            double distX = halfW - absX;
            double distY = halfH - absY;

            // Outside bounding extent + tolerance
            if (distX < -tolerance.EqualPoint || distY < -tolerance.EqualPoint)
            {
                return PointLocation.OutSide;
            }

            // On one of the boundaries
            if ((Math.Abs(distX) <= tolerance.EqualPoint && absY <= halfH + tolerance.EqualPoint) ||
                (Math.Abs(distY) <= tolerance.EqualPoint && absX <= halfW + tolerance.EqualPoint))
            {
                return PointLocation.OnSide;
            }

            // Strictly inside
            if (distX > tolerance.EqualPoint && distY > tolerance.EqualPoint)
            {
                return PointLocation.Inside;
            }

            return PointLocation.OutSide;
        }

        /// <summary>
        /// Classifies the spatial location of a point relative to a polygon using default tolerance.
        /// </summary>
        /// <param name="poly">The polygon.</param>
        /// <param name="point">The target point.</param>
        /// <returns>PointLocation.Inside, PointLocation.OnSide, or PointLocation.OutSide.</returns>
        public static PointLocation Locate(GeoPolygon2 poly, GeoPoint2 point)
        {
            return Locate(poly, point, Tolerance.Global);
        }

        /// <summary>
        /// Classifies the spatial location of a point relative to a polygon within tolerance.
        /// </summary>
        /// <param name="poly">The polygon.</param>
        /// <param name="point">The target point.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>PointLocation.Inside, PointLocation.OnSide, or PointLocation.OutSide.</returns>
        public static PointLocation Locate(GeoPolygon2 poly, GeoPoint2 point, Tolerance tolerance)
        {
            if (poly == null) throw new ArgumentNullException(nameof(poly));

            // Check boundary first
            if (IsPointOn(poly, point, tolerance))
            {
                return PointLocation.OnSide;
            }

            // Ray-Casting algorithm for interior test
            bool inside = false;
            int n = poly.VertexCount;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                GeoPoint2 p1 = poly[i];
                GeoPoint2 p2 = poly[j];

                if (((p1.Y > point.Y) != (p2.Y > point.Y)) &&
                    (point.X < (p2.X - p1.X) * (point.Y - p1.Y) / (p2.Y - p1.Y) + p1.X))
                {
                    inside = !inside;
                }
            }

            return inside ? PointLocation.Inside : PointLocation.OutSide;
        }

        /// <summary>
        /// Classifies the spatial location of a point relative to a polyline using default tolerance.
        /// A polyline has no interior, so the result is either OnSide or OutSide.
        /// </summary>
        /// <param name="polyline">The polyline.</param>
        /// <param name="point">The target point.</param>
        /// <returns>PointLocation.Inside, PointLocation.OnSide, or PointLocation.OutSide.</returns>
        public static PointLocation Locate(GeoPolyline2 polyline, GeoPoint2 point)
        {
            return Locate(polyline, point, Tolerance.Global);
        }

        /// <summary>
        /// Classifies the spatial location of a point relative to a polyline within tolerance.
        /// A polyline has no interior, so the result is either OnSide or OutSide; convert it with
        /// <see cref="Geometry.GeoPolyline2.ToPolygon"/> to classify against an enclosed area.
        /// </summary>
        /// <param name="polyline">The polyline.</param>
        /// <param name="point">The target point.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>PointLocation.Inside, PointLocation.OnSide, or PointLocation.OutSide.</returns>
        public static PointLocation Locate(GeoPolyline2 polyline, GeoPoint2 point, Tolerance tolerance)
        {
            if (polyline == null) throw new ArgumentNullException(nameof(polyline));

            return IsPointOn(polyline, point, tolerance)
                ? PointLocation.OnSide
                : PointLocation.OutSide;
        }

        /// <summary>
        /// Classifies the spatial location of a point relative to a line segment using default tolerance.
        /// </summary>
        /// <param name="line">The line segment.</param>
        /// <param name="point">The target point.</param>
        /// <returns>PointLocation.OnSide if on the line segment; otherwise PointLocation.OutSide.</returns>
        public static PointLocation Locate(GeoLine2 line, GeoPoint2 point)
        {
            return Locate(line, point, Tolerance.Global);
        }

        /// <summary>
        /// Classifies the spatial location of a point relative to a line segment within tolerance.
        /// </summary>
        /// <param name="line">The line segment.</param>
        /// <param name="point">The target point.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>PointLocation.OnSide if on the line segment; otherwise PointLocation.OutSide.</returns>
        public static PointLocation Locate(GeoLine2 line, GeoPoint2 point, Tolerance tolerance)
        {
            return IsPointOn(line, point, tolerance) ? PointLocation.OnSide : PointLocation.OutSide;
        }

        /// <summary>
        /// Classifies the spatial location of a point relative to a face with holes using default tolerance.
        /// </summary>
        /// <param name="face">The face.</param>
        /// <param name="point">The target point.</param>
        /// <returns>PointLocation.Inside, PointLocation.OnSide, or PointLocation.OutSide.</returns>
        public static PointLocation Locate(GeoFace2 face, GeoPoint2 point)
        {
            return Locate(face, point, Tolerance.Global);
        }

        /// <summary>
        /// Classifies the spatial location of a point relative to a face with holes within tolerance.
        /// </summary>
        /// <param name="face">The face.</param>
        /// <param name="point">The target point.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>PointLocation.Inside, PointLocation.OnSide, or PointLocation.OutSide.</returns>
        /// <remarks>
        /// A point inside a hole is outside the face, and a point on the rim of a hole is on its boundary, since
        /// the rim is as much an edge of the material as the outer loop is.
        /// </remarks>
        public static PointLocation Locate(GeoFace2 face, GeoPoint2 point, Tolerance tolerance)
        {
            if (face == null) throw new ArgumentNullException(nameof(face));

            PointLocation outer = Locate(face.Boundary, point, tolerance);

            if (outer != PointLocation.Inside)
            {
                return outer;
            }

            foreach (GeoPolygon2 hole in face.Holes)
            {
                PointLocation inHole = Locate(hole, point, tolerance);

                if (inHole == PointLocation.OnSide)
                {
                    return PointLocation.OnSide;
                }

                if (inHole == PointLocation.Inside)
                {
                    return PointLocation.OutSide;
                }
            }

            return PointLocation.Inside;
        }

        /// <summary>
        /// Checks whether a face with holes holds a point, on its outline included, using default tolerance.
        /// </summary>
        public static bool Contains(GeoFace2 face, GeoPoint2 point)
        {
            return Contains(face, point, Tolerance.Global);
        }

        /// <summary>
        /// Checks whether a face with holes holds a point, on its outline included, within tolerance.
        /// </summary>
        public static bool Contains(GeoFace2 face, GeoPoint2 point, Tolerance tolerance)
        {
            return Locate(face, point, tolerance) != PointLocation.OutSide;
        }

        #endregion

        #region Contains Shapes

        /// <summary>
        /// Checks whether a rectangle entirely contains a line segment using default tolerance.
        /// </summary>
        public static bool Contains(GeoRectangle2 rect, GeoLine2 line)
        {
            return Contains(rect, line, Tolerance.Global);
        }

        /// <summary>
        /// Checks whether a rectangle entirely contains a line segment within tolerance.
        /// (Since a rectangle is a convex set, a line segment is contained iff both endpoints are contained).
        /// </summary>
        public static bool Contains(GeoRectangle2 rect, GeoLine2 line, Tolerance tolerance)
        {
            // A rectangle is convex, so containing both endpoints is enough to contain the whole segment.
            return Contains(rect, line.StartPoint, tolerance) && Contains(rect, line.EndPoint, tolerance);
        }

        /// <summary>
        /// Checks whether a rectangle entirely contains a polyline using default tolerance.
        /// </summary>
        public static bool Contains(GeoRectangle2 rect, GeoPolyline2 polyline)
        {
            return Contains(rect, polyline, Tolerance.Global);
        }

        /// <summary>
        /// Checks whether a rectangle entirely contains a polyline within tolerance.
        /// </summary>
        public static bool Contains(GeoRectangle2 rect, GeoPolyline2 polyline, Tolerance tolerance)
        {
            if (polyline == null) throw new ArgumentNullException(nameof(polyline));
            for (int i = 0; i < polyline.EdgeCount; i++)
            {
                if (!Contains(rect, polyline.GetEdgeAt(i), tolerance))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Checks whether a circle entirely contains another circle using default tolerance.
        /// </summary>
        /// <param name="circle">The outer circle.</param>
        /// <param name="other">The inner circle.</param>
        /// <returns>true if the outer circle entirely contains the inner circle; otherwise, false.</returns>
        public static bool Contains(GeoCircle2 circle, GeoCircle2 other)
        {
            return Contains(circle, other, Tolerance.Global);
        }

        /// <summary>
        /// Checks whether a circle entirely contains another circle within tolerance.
        /// </summary>
        /// <param name="circle">The outer circle.</param>
        /// <param name="other">The inner circle.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the outer circle entirely contains the inner circle within tolerance; otherwise, false.</returns>
        public static bool Contains(GeoCircle2 circle, GeoCircle2 other, Tolerance tolerance)
        {
            return Distance2.DistanceTo(circle.Center, other.Center) + other.Radius <= circle.Radius + tolerance.EqualPoint;
        }

        /// <summary>
        /// Checks whether a circle entirely contains a line segment using default tolerance.
        /// </summary>
        public static bool Contains(GeoCircle2 circle, GeoLine2 line)
        {
            return Contains(circle, line, Tolerance.Global);
        }

        /// <summary>
        /// Checks whether a circle entirely contains a line segment within tolerance.
        /// </summary>
        public static bool Contains(GeoCircle2 circle, GeoLine2 line, Tolerance tolerance)
        {
            // A disk is convex, so containing both endpoints is enough to contain the whole segment.
            return Contains(circle, line.StartPoint, tolerance) && Contains(circle, line.EndPoint, tolerance);
        }

        /// <summary>
        /// Checks whether a polygon entirely contains a line segment using default tolerance.
        /// </summary>
        /// <param name="poly">The polygon.</param>
        /// <param name="line">The line segment.</param>
        /// <returns>true if the polygon entirely contains the line segment; otherwise, false.</returns>
        public static bool Contains(GeoPolygon2 poly, GeoLine2 line)
        {
            return Contains(poly, line, Tolerance.Global);
        }

        /// <summary>
        /// Checks whether a polygon entirely contains a line segment within tolerance.
        /// </summary>
        /// <param name="poly">The polygon.</param>
        /// <param name="line">The line segment.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the polygon entirely contains the line segment; otherwise, false.</returns>
        public static bool Contains(GeoPolygon2 poly, GeoLine2 line, Tolerance tolerance)
        {
            if (poly == null) throw new ArgumentNullException(nameof(poly));

            if (!Contains(poly, line.StartPoint, tolerance) || !Contains(poly, line.EndPoint, tolerance))
            {
                return false;
            }

            // Both endpoints are inside, but a concave polygon can still let the segment leave and come
            // back, so the crossings matter. Treating any crossing as a failure would reject a diagonal
            // between two vertices, which touches the boundary without ever leaving the polygon. Instead
            // split the segment at every crossing and require the midpoint of each resulting piece to be
            // contained: touching a vertex or running along an edge stays contained, a genuine excursion
            // outside does not.
            List<double> parameters = new List<double> { 0.0, 1.0 };
            for (int i = 0; i < poly.EdgeCount; i++)
            {
                if (Intersection2.TryIntersectWith(line, poly.GetEdgeAt(i), out GeoPoint2 crossing, tolerance))
                {
                    parameters.Add(Parametrization2.GetParameterAtPoint(line, crossing));
                }
            }

            parameters.Sort();

            for (int i = 1; i < parameters.Count; i++)
            {
                double middle = (parameters[i - 1] + parameters[i]) * 0.5;
                if (!Contains(poly, line.GetPointAtParameter(middle), tolerance))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Checks whether a polygon entirely contains a polyline using default tolerance.
        /// </summary>
        /// <param name="poly">The polygon.</param>
        /// <param name="polyline">The polyline.</param>
        /// <returns>true if the polygon entirely contains the polyline; otherwise, false.</returns>
        public static bool Contains(GeoPolygon2 poly, GeoPolyline2 polyline)
        {
            return Contains(poly, polyline, Tolerance.Global);
        }

        /// <summary>
        /// Checks whether a polygon entirely contains a polyline within tolerance.
        /// </summary>
        /// <param name="poly">The polygon.</param>
        /// <param name="polyline">The polyline.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the polygon entirely contains the polyline; otherwise, false.</returns>
        public static bool Contains(GeoPolygon2 poly, GeoPolyline2 polyline, Tolerance tolerance)
        {
            if (poly == null) throw new ArgumentNullException(nameof(poly));
            if (polyline == null) throw new ArgumentNullException(nameof(polyline));

            for (int i = 0; i < polyline.EdgeCount; i++)
            {
                if (!Contains(poly, polyline.GetEdgeAt(i), tolerance))
                {
                    return false;
                }
            }

            return true;
        }

        #endregion

        #region Side of a line

        /// <summary>
        /// Determines which side of a segment a point lies on, using the default tolerance.
        /// </summary>
        /// <param name="line">The segment, read as the infinite line carrying it.</param>
        /// <param name="point">The point.</param>
        /// <returns>The side the point lies on, seen looking from the start of the segment to its end.</returns>
        public static LineSide GetSide(GeoLine2 line, GeoPoint2 point) => GetSide(line, point, Tolerance.Global);

        /// <summary>
        /// Determines which side of a segment a point lies on, within a tolerance.
        /// </summary>
        /// <param name="line">The segment, read as the infinite line carrying it.</param>
        /// <param name="point">The point.</param>
        /// <param name="tolerance">The tolerance: a point within <see cref="Tolerance.EqualPoint"/> of the line counts as on it.</param>
        /// <returns>The side the point lies on, seen looking from the start of the segment to its end.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the segment is too short to have a direction.</exception>
        public static LineSide GetSide(GeoLine2 line, GeoPoint2 point, Tolerance tolerance)
        {
            GeoVector2 along = line.StartPoint.GetVectorTo(line.EndPoint);
            double length = along.Length;

            if (length <= tolerance.EqualPoint)
            {
                throw new InvalidOperationException("A segment shorter than the tolerance has no direction, so it has no sides.");
            }

            // The cross product divided by the length is the distance off the line, signed by the side.
            double offset = along.CrossProduct(line.StartPoint.GetVectorTo(point)) / length;

            if (Math.Abs(offset) <= tolerance.EqualPoint)
            {
                return LineSide.On;
            }

            return offset > 0.0 ? LineSide.Left : LineSide.Right;
        }

        #endregion
        #region Chains that may curve

        /// <summary>
        /// Determines whether a point lies on a chain that may curve.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static bool IsPointOn(GeoPolylineArc2 chain, GeoPoint2 point) => IsPointOn(chain, point, Tolerance.Global);

        /// <summary>
        /// Determines whether a point lies on a chain that may curve, within a tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static bool IsPointOn(GeoPolylineArc2 chain, GeoPoint2 point, Tolerance tolerance)
        {
            if (chain == null) throw new ArgumentNullException(nameof(chain));

            return ArcChain2.DistanceTo(ArcChain2.EdgesOf(chain), point, tolerance) <= tolerance.EqualPoint;
        }

        /// <summary>
        /// Determines whether a point lies on a loop that may curve.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public static bool IsPointOn(GeoPolygonArc2 loop, GeoPoint2 point) => IsPointOn(loop, point, Tolerance.Global);

        /// <summary>
        /// Determines whether a point lies on a loop that may curve, within a tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public static bool IsPointOn(GeoPolygonArc2 loop, GeoPoint2 point, Tolerance tolerance)
        {
            if (loop == null) throw new ArgumentNullException(nameof(loop));

            return ArcChain2.DistanceTo(ArcChain2.EdgesOf(loop), point, tolerance) <= tolerance.EqualPoint;
        }

        /// <summary>
        /// Says where a point lies against a chain that may curve.
        /// </summary>
        /// <remarks>
        /// A chain is a curve, so it encloses nothing: a point is either on it or outside it, exactly as
        /// with <see cref="Locate(GeoPolyline2, GeoPoint2)"/>.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static PointLocation Locate(GeoPolylineArc2 chain, GeoPoint2 point) => Locate(chain, point, Tolerance.Global);

        /// <summary>
        /// Says where a point lies against a chain that may curve, within a tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public static PointLocation Locate(GeoPolylineArc2 chain, GeoPoint2 point, Tolerance tolerance)
        {
            return IsPointOn(chain, point, tolerance) ? PointLocation.OnSide : PointLocation.OutSide;
        }

        /// <summary>
        /// Says where a point lies against a loop that may curve.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public static PointLocation Locate(GeoPolygonArc2 loop, GeoPoint2 point) => Locate(loop, point, Tolerance.Global);

        /// <summary>
        /// Says where a point lies against a loop that may curve, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The answer is exact, with no arc cut into pieces anywhere in it: it is the straight loop through
        /// the vertices, turned inside out once for every piece an arc cuts off its own chord that the
        /// point happens to lie in.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public static PointLocation Locate(GeoPolygonArc2 loop, GeoPoint2 point, Tolerance tolerance)
        {
            if (loop == null) throw new ArgumentNullException(nameof(loop));

            if (IsPointOn(loop, point, tolerance))
            {
                return PointLocation.OnSide;
            }

            return ArcChain2.Encloses(loop, point, tolerance) ? PointLocation.Inside : PointLocation.OutSide;
        }

        /// <summary>
        /// Determines whether a loop that may curve encloses a point, counting its boundary as inside.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public static bool Contains(GeoPolygonArc2 loop, GeoPoint2 point) => Contains(loop, point, Tolerance.Global);

        /// <summary>
        /// Determines whether a loop that may curve encloses a point, within a tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public static bool Contains(GeoPolygonArc2 loop, GeoPoint2 point, Tolerance tolerance)
        {
            return Locate(loop, point, tolerance) != PointLocation.OutSide;
        }

        #endregion

    }
}
