using System;
using GeometryHelper.CommonGeometry;
using GeometryHelper.PlaneGeometry.Geometry;

namespace GeometryHelper.PlaneGeometry.Core
{
    /// <summary>
    /// Provides static methods for locating positions along a curve, either by a normalized parameter or by
    /// an arc length measured from the start of the curve.
    /// <para>
    /// The <b>parameter</b> is normalized: 0 is the start of the curve and 1 is its end, whatever the curve
    /// actually measures. The <b>distance</b> is a true arc length from the start, running from 0 to the
    /// total length. The two are proportional, so <c>distance = parameter * length</c>.
    /// </para>
    /// <para>
    /// Each curve starts where its own geometry says it does: a line at its StartPoint, a polyline or
    /// polygon at its first vertex, a rectangle at its LowerLeft corner, and a circle at angle zero, that is
    /// the point directly right of its centre. All of them run in the direction their vertices are ordered,
    /// and a circle runs counter-clockwise.
    /// </para>
    /// <para>
    /// Values outside the natural range follow the shape of the curve. A closed curve — a polygon, a
    /// rectangle, a circle — wraps around, so a parameter of 1.25 is the same position as 0.25. A polyline
    /// clamps, because it is an open chain with no natural extension. A line segment extrapolates along the
    /// infinite line that carries it, which is the behaviour <c>GeoLine2.GetPointAtParameter</c> has always had.
    /// </para>
    /// </summary>
    public static class Parametrization2
    {
        /// <summary>
        /// Projects a point onto the infinite line containing the segment and gets the parameter value t
        /// using default tolerance. t=0 corresponds to StartPoint, t=1 corresponds to EndPoint.
        /// </summary>
        /// <param name="line">The line segment.</param>
        /// <param name="point">The target point.</param>
        /// <returns>The parameter value t along the line.</returns>
        public static double GetParameterAtPoint(GeoLine2 line, GeoPoint2 point)
        {
            return GetParameterAtPoint(line, point, Tolerance.Global);
        }

        /// <summary>
        /// Projects a point onto the infinite line containing the segment and gets the parameter value t
        /// within tolerance. t=0 corresponds to StartPoint, t=1 corresponds to EndPoint.
        /// </summary>
        /// <param name="line">The line segment.</param>
        /// <param name="point">The target point.</param>
        /// <param name="tolerance">The tolerance used to detect a degenerate segment.</param>
        /// <returns>The parameter value t along the line, or 0 for a degenerate segment.</returns>
        public static double GetParameterAtPoint(GeoLine2 line, GeoPoint2 point, Tolerance tolerance)
        {
            GeoVector2 dir = line.Direction;
            double lenSq = dir.LengthSquared;
            if (lenSq <= tolerance.EqualPoint * tolerance.EqualPoint) return 0.0;
            return line.StartPoint.GetVectorTo(point).DotProduct(dir) / lenSq;
        }

        /// <summary>
        /// Gets the point at a normalized parameter along a line segment. Values outside [0, 1] extrapolate
        /// along the infinite line carrying the segment.
        /// </summary>
        public static GeoPoint2 GetPointAtParameter(GeoLine2 line, double parameter)
        {
            return line.StartPoint.Add(line.Direction.Multiply(parameter));
        }

        /// <summary>
        /// Gets the arc length from the start of a line segment to a normalized parameter.
        /// </summary>
        public static double GetDistanceAtParameter(GeoLine2 line, double parameter)
        {
            return parameter * line.Length;
        }

        /// <summary>
        /// Gets the normalized parameter at an arc length measured from the start of a line segment. A
        /// degenerate segment has no direction to measure along, so the result is 0.
        /// </summary>
        public static double GetParameterAtDistance(GeoLine2 line, double distance)
        {
            double length = line.Length;
            return length <= 0.0 ? 0.0 : distance / length;
        }

        /// <summary>
        /// Gets the point at an arc length measured from the start of a line segment.
        /// </summary>
        public static GeoPoint2 GetPointAtDistance(GeoLine2 line, double distance)
        {
            return GetPointAtParameter(line, GetParameterAtDistance(line, distance));
        }

        /// <summary>
        /// Gets the arc length from the start of a line segment to the point on it closest to the supplied
        /// point.
        /// </summary>
        public static double GetDistanceAtPoint(GeoLine2 line, GeoPoint2 point)
        {
            return GetParameterAtPoint(line, point) * line.Length;
        }

        /// <summary>
        /// Gets the point at a normalized parameter along a circle, starting at angle zero and running
        /// counter-clockwise. The parameter wraps, so 1.25 gives the same point as 0.25.
        /// </summary>
        public static GeoPoint2 GetPointAtParameter(GeoCircle2 circle, double parameter)
        {
            double angle = Wrap(parameter) * 2.0 * Math.PI;
            return new GeoPoint2(
                circle.Center.X + circle.Radius * Math.Cos(angle),
                circle.Center.Y + circle.Radius * Math.Sin(angle));
        }

        /// <summary>
        /// Gets the normalized parameter of the point on a circle closest to the supplied point. The point
        /// does not have to lie on the circumference; only its direction from the centre matters.
        /// </summary>
        public static double GetParameterAtPoint(GeoCircle2 circle, GeoPoint2 point)
        {
            double angle = Math.Atan2(point.Y - circle.Center.Y, point.X - circle.Center.X);
            if (angle < 0.0) angle += 2.0 * Math.PI;
            return angle / (2.0 * Math.PI);
        }

        /// <summary>
        /// Gets the arc length from angle zero to a normalized parameter along a circle.
        /// </summary>
        public static double GetDistanceAtParameter(GeoCircle2 circle, double parameter)
        {
            return parameter * circle.Circumference;
        }

        /// <summary>
        /// Gets the normalized parameter at an arc length measured along a circle. A circle of zero radius
        /// has no circumference to measure along, so the result is 0.
        /// </summary>
        public static double GetParameterAtDistance(GeoCircle2 circle, double distance)
        {
            double circumference = circle.Circumference;
            return circumference <= 0.0 ? 0.0 : distance / circumference;
        }

        /// <summary>
        /// Gets the point at an arc length measured along a circle from angle zero.
        /// </summary>
        public static GeoPoint2 GetPointAtDistance(GeoCircle2 circle, double distance)
        {
            return GetPointAtParameter(circle, GetParameterAtDistance(circle, distance));
        }

        /// <summary>
        /// Gets the arc length from angle zero to the point on a circle closest to the supplied point.
        /// </summary>
        public static double GetDistanceAtPoint(GeoCircle2 circle, GeoPoint2 point)
        {
            return GetParameterAtPoint(circle, point) * circle.Circumference;
        }

        /// <summary>
        /// Gets the point at a normalized parameter along the perimeter of a rectangle, starting at its
        /// LowerLeft corner and running counter-clockwise. The parameter wraps.
        /// </summary>
        public static GeoPoint2 GetPointAtParameter(GeoRectangle2 rect, double parameter)
        {
            return PointAtDistance(rect.GetEdges(), rect.Length, Wrap(parameter) * rect.Length);
        }

        /// <summary>
        /// Gets the normalized parameter of the point on the boundary of a rectangle closest to the supplied
        /// point.
        /// </summary>
        public static double GetParameterAtPoint(GeoRectangle2 rect, GeoPoint2 point)
        {
            double length = rect.Length;
            return length <= 0.0 ? 0.0 : DistanceAtPoint(rect.GetEdges(), point) / length;
        }

        /// <summary>
        /// Gets the arc length from the LowerLeft corner to a normalized parameter along a rectangle.
        /// </summary>
        public static double GetDistanceAtParameter(GeoRectangle2 rect, double parameter)
        {
            return parameter * rect.Length;
        }

        /// <summary>
        /// Gets the normalized parameter at an arc length measured along the perimeter of a rectangle. A
        /// rectangle with no perimeter gives 0.
        /// </summary>
        public static double GetParameterAtDistance(GeoRectangle2 rect, double distance)
        {
            double length = rect.Length;
            return length <= 0.0 ? 0.0 : distance / length;
        }

        /// <summary>
        /// Gets the point at an arc length measured along the perimeter of a rectangle.
        /// </summary>
        public static GeoPoint2 GetPointAtDistance(GeoRectangle2 rect, double distance)
        {
            double length = rect.Length;
            return PointAtDistance(rect.GetEdges(), length, WrapDistance(distance, length));
        }

        /// <summary>
        /// Gets the arc length from the LowerLeft corner to the point on the boundary of a rectangle closest
        /// to the supplied point.
        /// </summary>
        public static double GetDistanceAtPoint(GeoRectangle2 rect, GeoPoint2 point)
        {
            return DistanceAtPoint(rect.GetEdges(), point);
        }

        /// <summary>
        /// Gets the point at a normalized parameter along the perimeter of a polygon, starting at its first
        /// vertex. The parameter wraps.
        /// </summary>
        public static GeoPoint2 GetPointAtParameter(GeoPolygon2 poly, double parameter)
        {
            double length = poly.Length;
            return PointAtDistance(Edges(poly), length, Wrap(parameter) * length);
        }

        /// <summary>
        /// Gets the normalized parameter of the point on the boundary of a polygon closest to the supplied
        /// point.
        /// </summary>
        public static double GetParameterAtPoint(GeoPolygon2 poly, GeoPoint2 point)
        {
            double length = poly.Length;
            return length <= 0.0 ? 0.0 : DistanceAtPoint(Edges(poly), point) / length;
        }

        /// <summary>
        /// Gets the arc length from the first vertex to a normalized parameter along a polygon.
        /// </summary>
        public static double GetDistanceAtParameter(GeoPolygon2 poly, double parameter)
        {
            return parameter * poly.Length;
        }

        /// <summary>
        /// Gets the normalized parameter at an arc length measured along the perimeter of a polygon.
        /// </summary>
        public static double GetParameterAtDistance(GeoPolygon2 poly, double distance)
        {
            double length = poly.Length;
            return length <= 0.0 ? 0.0 : distance / length;
        }

        /// <summary>
        /// Gets the point at an arc length measured along the perimeter of a polygon.
        /// </summary>
        public static GeoPoint2 GetPointAtDistance(GeoPolygon2 poly, double distance)
        {
            double length = poly.Length;
            return PointAtDistance(Edges(poly), length, WrapDistance(distance, length));
        }

        /// <summary>
        /// Gets the arc length from the first vertex to the point on the boundary of a polygon closest to
        /// the supplied point.
        /// </summary>
        public static double GetDistanceAtPoint(GeoPolygon2 poly, GeoPoint2 point)
        {
            return DistanceAtPoint(Edges(poly), point);
        }

        /// <summary>
        /// Gets the point at a normalized parameter along a polyline, starting at its first vertex,
        /// clamped to its endpoints.
        /// </summary>
        public static GeoPoint2 GetPointAtParameter(GeoPolyline2 polyline, double parameter)
        {
            if (polyline == null) throw new ArgumentNullException(nameof(polyline));

            double length = polyline.Length;
            double bounded = Clamp(parameter);
            return PointAtDistance(Edges(polyline), length, bounded * length);
        }

        /// <summary>
        /// Gets the normalized parameter of the point on a polyline closest to the supplied point.
        /// </summary>
        public static double GetParameterAtPoint(GeoPolyline2 polyline, GeoPoint2 point)
        {
            if (polyline == null) throw new ArgumentNullException(nameof(polyline));

            double length = polyline.Length;
            return length <= 0.0 ? 0.0 : DistanceAtPoint(Edges(polyline), point) / length;
        }

        /// <summary>
        /// Gets the arc length from the first vertex to a normalized parameter along a polyline.
        /// </summary>
        public static double GetDistanceAtParameter(GeoPolyline2 polyline, double parameter)
        {
            if (polyline == null) throw new ArgumentNullException(nameof(polyline));
            return parameter * polyline.Length;
        }

        /// <summary>
        /// Gets the normalized parameter at an arc length measured along a polyline.
        /// </summary>
        public static double GetParameterAtDistance(GeoPolyline2 polyline, double distance)
        {
            if (polyline == null) throw new ArgumentNullException(nameof(polyline));

            double length = polyline.Length;
            return length <= 0.0 ? 0.0 : distance / length;
        }

        /// <summary>
        /// Gets the point at an arc length measured along a polyline, clamped to its endpoints.
        /// </summary>
        public static GeoPoint2 GetPointAtDistance(GeoPolyline2 polyline, double distance)
        {
            if (polyline == null) throw new ArgumentNullException(nameof(polyline));

            double length = polyline.Length;
            double bounded = Math.Max(0.0, Math.Min(length, distance));
            return PointAtDistance(Edges(polyline), length, bounded);
        }

        /// <summary>
        /// Gets the arc length from the first vertex to the point on a polyline closest to the supplied
        /// point.
        /// </summary>
        public static double GetDistanceAtPoint(GeoPolyline2 polyline, GeoPoint2 point)
        {
            if (polyline == null) throw new ArgumentNullException(nameof(polyline));
            return DistanceAtPoint(Edges(polyline), point);
        }

        /// <summary>
        /// Wraps a normalized parameter into [0, 1), which is what a closed curve does when it runs past its
        /// own end.
        /// </summary>
        private static double Wrap(double parameter)
        {
            double wrapped = parameter % 1.0;
            if (wrapped < 0.0) wrapped += 1.0;
            if (wrapped >= 1.0) wrapped = 0.0;
            return wrapped;
        }

        /// <summary>
        /// Wraps an arc length into [0, length), the distance equivalent of <see cref="Wrap"/>.
        /// </summary>
        private static double WrapDistance(double distance, double length)
        {
            if (length <= 0.0) return 0.0;

            double wrapped = distance % length;
            if (wrapped < 0.0) wrapped += length;
            if (wrapped >= length) wrapped = 0.0;
            return wrapped;
        }

        /// <summary>
        /// Clamps a normalized parameter into [0, 1], which is what an open curve does since it has no
        /// natural extension past either end.
        /// </summary>
        private static double Clamp(double parameter)
        {
            return Math.Max(0.0, Math.Min(1.0, parameter));
        }

        /// <summary>
        /// Materializes the edges of a polygon so they can be walked more than once without re-enumerating.
        /// </summary>
        private static GeoLine2[] Edges(GeoPolygon2 poly)
        {
            if (poly == null) throw new ArgumentNullException(nameof(poly));

            GeoLine2[] edges = new GeoLine2[poly.EdgeCount];
            for (int i = 0; i < edges.Length; i++)
            {
                edges[i] = poly.GetEdgeAt(i);
            }
            return edges;
        }

        /// <summary>
        /// Materializes the edges of a polyline so they can be walked more than once without re-enumerating.
        /// </summary>
        private static GeoLine2[] Edges(GeoPolyline2 polyline)
        {
            GeoLine2[] edges = new GeoLine2[polyline.EdgeCount];
            for (int i = 0; i < edges.Length; i++)
            {
                edges[i] = polyline.GetEdgeAt(i);
            }
            return edges;
        }

        /// <summary>
        /// Walks a chain of edges until the given arc length is consumed and returns the point reached.
        /// </summary>
        /// <param name="edges">The edges in order.</param>
        /// <param name="totalLength">Sum of the edge lengths, passed in so it is not recomputed.</param>
        /// <param name="distance">Arc length from the start of the first edge, already bounded by the caller.</param>
        private static GeoPoint2 PointAtDistance(GeoLine2[] edges, double totalLength, double distance)
        {
            if (edges.Length == 0) return new GeoPoint2();
            if (totalLength <= 0.0) return edges[0].StartPoint;
            if (distance <= 0.0) return edges[0].StartPoint;
            if (distance >= totalLength) return edges[edges.Length - 1].EndPoint;

            double remaining = distance;
            for (int i = 0; i < edges.Length; i++)
            {
                double edgeLength = edges[i].Length;
                if (remaining <= edgeLength)
                {
                    // A zero-length edge cannot be subdivided, so its start point is the whole of it.
                    if (edgeLength <= 0.0) return edges[i].StartPoint;
                    return edges[i].GetPointAtParameter(remaining / edgeLength);
                }
                remaining -= edgeLength;
            }

            return edges[edges.Length - 1].EndPoint;
        }

        /// <summary>
        /// Finds the edge whose closest point to the supplied point is nearest overall, and returns the arc
        /// length from the start of the chain to that closest point.
        /// </summary>
        private static double DistanceAtPoint(GeoLine2[] edges, GeoPoint2 point)
        {
            if (edges.Length == 0) return 0.0;

            double bestDistanceSquared = double.MaxValue;
            double result = 0.0;
            double accumulated = 0.0;

            foreach (GeoLine2 edge in edges)
            {
                GeoPoint2 projected = Projection2.ProjectToLine(edge, point);
                double distanceSquared = Distance2.GetDistanceSquaredTo(point, projected);

                if (distanceSquared < bestDistanceSquared)
                {
                    bestDistanceSquared = distanceSquared;
                    result = accumulated + edge.StartPoint.DistanceTo(projected);
                }

                accumulated += edge.Length;
            }

            return result;
        }
    }
}
