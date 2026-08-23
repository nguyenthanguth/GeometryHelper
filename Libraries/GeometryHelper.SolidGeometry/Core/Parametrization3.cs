using System;
using GeometryHelper.CommonGeometry;
using GeometryHelper.SolidGeometry.Geometry;

namespace GeometryHelper.SolidGeometry.Core
{
    /// <summary>
    /// Provides static methods for locating positions along a 3D curve, either by a normalized parameter
    /// or by an arc length measured from the start of the curve.
    /// <para>
    /// The <b>parameter</b> is normalized: 0 is the start of the curve and 1 is its end, whatever the
    /// curve actually measures. The <b>distance</b> is a true arc length from the start, running from 0
    /// to the total length. The two are proportional, so <c>distance = parameter * length</c>.
    /// </para>
    /// <para>
    /// Values outside the natural range follow the shape of the curve. A line segment extrapolates along
    /// the infinite line that carries it, and so does a ray, which likewise has a well defined carrier.
    /// A polyline clamps, because it is an open chain of segments with no single direction to extend
    /// along. A closed curve — a polygon, a circle — wraps, so a parameter of 1.25 is the same position
    /// as 0.25.
    /// </para>
    /// </summary>
    public static class Parametrization3
    {
        #region Line

        /// <summary>
        /// Gets the point at a normalized parameter along a line segment. Values outside [0, 1]
        /// extrapolate along the infinite line carrying the segment.
        /// </summary>
        public static GeoPoint3 GetPointAtParameter(GeoLine3 line, double parameter)
        {
            return line.StartPoint.Add(line.Direction.Multiply(parameter));
        }

        /// <summary>
        /// Projects a point onto the infinite line carrying a segment and gets the parameter there, using
        /// the default tolerance.
        /// </summary>
        public static double GetParameterAtPoint(GeoLine3 line, GeoPoint3 point)
        {
            return GetParameterAtPoint(line, point, Tolerance.Global);
        }

        /// <summary>
        /// Projects a point onto the infinite line carrying a segment and gets the parameter there, within
        /// a tolerance.
        /// </summary>
        /// <param name="line">The line segment.</param>
        /// <param name="point">The target point; it need not lie on the segment.</param>
        /// <param name="tolerance">The tolerance used to detect a degenerate segment.</param>
        /// <returns>The parameter along the segment, or 0 for a degenerate segment.</returns>
        public static double GetParameterAtPoint(GeoLine3 line, GeoPoint3 point, Tolerance tolerance)
        {
            GeoVector3 direction = line.Direction;
            double lengthSquared = direction.LengthSquared;

            if (lengthSquared <= tolerance.EqualPoint * tolerance.EqualPoint)
            {
                return 0.0;
            }

            return line.StartPoint.GetVectorTo(point).DotProduct(direction) / lengthSquared;
        }

        /// <summary>
        /// Gets the arc length from the start of a line segment to a normalized parameter.
        /// </summary>
        public static double GetDistanceAtParameter(GeoLine3 line, double parameter) => parameter * line.Length;

        /// <summary>
        /// Gets the normalized parameter at an arc length measured from the start of a line segment. A
        /// degenerate segment has no direction to measure along, so the result is 0.
        /// </summary>
        public static double GetParameterAtDistance(GeoLine3 line, double distance)
        {
            double length = line.Length;
            return length <= 0.0 ? 0.0 : distance / length;
        }

        /// <summary>
        /// Gets the point at an arc length measured from the start of a line segment.
        /// </summary>
        public static GeoPoint3 GetPointAtDistance(GeoLine3 line, double distance)
        {
            return GetPointAtParameter(line, GetParameterAtDistance(line, distance));
        }

        /// <summary>
        /// Gets the arc length from the start of a line segment to the point on it closest to the
        /// supplied point.
        /// </summary>
        public static double GetDistanceAtPoint(GeoLine3 line, GeoPoint3 point)
        {
            return GetParameterAtPoint(line, point) * line.Length;
        }

        #endregion

        #region Ray

        /// <summary>
        /// Gets the point at an arc length measured from the origin of a ray. A negative distance
        /// extrapolates behind the origin, off the ray itself.
        /// </summary>
        public static GeoPoint3 GetPointAtDistance(GeoRay3 ray, double distance)
        {
            return ray.Origin.Add(ray.Direction.Multiply(distance));
        }

        /// <summary>
        /// Gets the arc length from the origin of a ray to the point on its carrier line closest to the
        /// supplied point. The result is negative when that point falls behind the origin.
        /// </summary>
        public static double GetDistanceAtPoint(GeoRay3 ray, GeoPoint3 point)
        {
            return ray.Origin.GetVectorTo(point).DotProduct(ray.Direction);
        }

        #endregion

        #region Polyline

        /// <summary>
        /// Gets the point at a normalized parameter along a polyline. Values outside [0, 1] clamp to the
        /// ends, because an open chain has no single direction to extend along.
        /// </summary>
        public static GeoPoint3 GetPointAtParameter(GeoPolyline3 polyline, double parameter)
        {
            if (polyline == null)
            {
                throw new ArgumentNullException(nameof(polyline));
            }

            return GetPointAtDistance(polyline, GetDistanceAtParameter(polyline, parameter));
        }

        /// <summary>
        /// Gets the arc length from the start of a polyline to a normalized parameter, clamped to the chain.
        /// </summary>
        public static double GetDistanceAtParameter(GeoPolyline3 polyline, double parameter)
        {
            if (polyline == null)
            {
                throw new ArgumentNullException(nameof(polyline));
            }

            return Clamp(parameter) * polyline.Length;
        }

        /// <summary>
        /// Gets the normalized parameter at an arc length measured from the start of a polyline, clamped
        /// to the chain.
        /// </summary>
        public static double GetParameterAtDistance(GeoPolyline3 polyline, double distance)
        {
            if (polyline == null)
            {
                throw new ArgumentNullException(nameof(polyline));
            }

            double length = polyline.Length;

            return length <= 0.0 ? 0.0 : Clamp(distance / length);
        }

        /// <summary>
        /// Gets the point at an arc length measured from the start of a polyline, clamped to the chain.
        /// </summary>
        public static GeoPoint3 GetPointAtDistance(GeoPolyline3 polyline, double distance)
        {
            if (polyline == null)
            {
                throw new ArgumentNullException(nameof(polyline));
            }

            if (distance <= 0.0)
            {
                return polyline.StartPoint;
            }

            if (distance >= polyline.Length)
            {
                return polyline.EndPoint;
            }

            double remaining = distance;

            for (int i = 0; i < polyline.EdgeCount; i++)
            {
                GeoLine3 edge = polyline.GetEdgeAt(i);
                double edgeLength = edge.Length;

                if (remaining <= edgeLength)
                {
                    return GetPointAtDistance(edge, remaining);
                }

                remaining -= edgeLength;
            }

            // Only reachable when rounding in the running subtraction leaves a sliver over the total
            // length that the early return above did not catch.
            return polyline.EndPoint;
        }

        /// <summary>
        /// Gets the arc length from the start of a polyline to the point on it closest to the supplied
        /// point.
        /// </summary>
        public static double GetDistanceAtPoint(GeoPolyline3 polyline, GeoPoint3 point) => GetDistanceAtPoint(polyline, point, Tolerance.Global);

        /// <summary>
        /// Gets the arc length from the start of a polyline to the point on it closest to the supplied
        /// point, within a tolerance.
        /// </summary>
        /// <remarks>
        /// A polyline can pass near the same point more than once, so the closest edge is found first and
        /// the arc length is measured to that. Walking the chain and stopping at the first edge within
        /// tolerance would give a different answer depending on which end the chain was built from.
        /// </remarks>
        public static double GetDistanceAtPoint(GeoPolyline3 polyline, GeoPoint3 point, Tolerance tolerance)
        {
            if (polyline == null)
            {
                throw new ArgumentNullException(nameof(polyline));
            }

            double travelled = 0.0;
            double bestTravelled = 0.0;
            double bestDistanceSquared = double.MaxValue;

            for (int i = 0; i < polyline.EdgeCount; i++)
            {
                GeoLine3 edge = polyline.GetEdgeAt(i);
                GeoPoint3 candidate = Projection3.ProjectToLine(edge, point, tolerance);
                double distanceSquared = Distance3.GetDistanceSquaredTo(candidate, point);

                if (distanceSquared < bestDistanceSquared)
                {
                    bestDistanceSquared = distanceSquared;
                    bestTravelled = travelled + edge.StartPoint.DistanceTo(candidate);
                }

                travelled += edge.Length;
            }

            return bestTravelled;
        }

        /// <summary>
        /// Gets the normalized parameter of the point on a polyline closest to the supplied point.
        /// </summary>
        public static double GetParameterAtPoint(GeoPolyline3 polyline, GeoPoint3 point) => GetParameterAtPoint(polyline, point, Tolerance.Global);

        /// <summary>
        /// Gets the normalized parameter of the point on a polyline closest to the supplied point, within
        /// a tolerance.
        /// </summary>
        public static double GetParameterAtPoint(GeoPolyline3 polyline, GeoPoint3 point, Tolerance tolerance)
        {
            if (polyline == null)
            {
                throw new ArgumentNullException(nameof(polyline));
            }

            double length = polyline.Length;

            return length <= 0.0 ? 0.0 : GetDistanceAtPoint(polyline, point, tolerance) / length;
        }

        /// <summary>
        /// Clamps a normalized parameter into the [0, 1] range covering a curve.
        /// </summary>
        private static double Clamp(double value) => Math.Max(0.0, Math.Min(1.0, value));

        #endregion

        #region Closed curves

        /// <summary>
        /// Gets the point at a normalized parameter around the boundary of a polygon, starting at its
        /// first vertex. The parameter wraps, so 1.25 gives the same point as 0.25.
        /// </summary>
        public static GeoPoint3 GetPointAtParameter(GeoPolygon3 polygon, double parameter)
        {
            if (polygon == null)
            {
                throw new ArgumentNullException(nameof(polygon));
            }

            return GetPointAtDistance(polygon, Wrap(parameter) * polygon.Length);
        }

        /// <summary>
        /// Gets the arc length from the first vertex of a polygon to a normalized parameter around it.
        /// </summary>
        public static double GetDistanceAtParameter(GeoPolygon3 polygon, double parameter)
        {
            if (polygon == null)
            {
                throw new ArgumentNullException(nameof(polygon));
            }

            return Wrap(parameter) * polygon.Length;
        }

        /// <summary>
        /// Gets the normalized parameter at an arc length measured around the boundary of a polygon.
        /// </summary>
        public static double GetParameterAtDistance(GeoPolygon3 polygon, double distance)
        {
            if (polygon == null)
            {
                throw new ArgumentNullException(nameof(polygon));
            }

            double length = polygon.Length;

            return length <= 0.0 ? 0.0 : Wrap(distance / length);
        }

        /// <summary>
        /// Gets the point at an arc length measured around the boundary of a polygon from its first vertex.
        /// The distance wraps, so going once round and a bit more lands just past the start again.
        /// </summary>
        public static GeoPoint3 GetPointAtDistance(GeoPolygon3 polygon, double distance)
        {
            if (polygon == null)
            {
                throw new ArgumentNullException(nameof(polygon));
            }

            double length = polygon.Length;

            if (length <= 0.0)
            {
                return polygon[0];
            }

            double remaining = Wrap(distance / length) * length;

            for (int i = 0; i < polygon.EdgeCount; i++)
            {
                GeoLine3 edge = polygon.GetEdgeAt(i);
                double edgeLength = edge.Length;

                if (remaining <= edgeLength)
                {
                    return GetPointAtDistance(edge, remaining);
                }

                remaining -= edgeLength;
            }

            // Only reachable when rounding in the running subtraction leaves a sliver over the total
            // length; the loop has then walked the whole boundary and is back at the start.
            return polygon[0];
        }

        /// <summary>
        /// Gets the arc length from the first vertex of a polygon to the point on its boundary closest to
        /// the supplied point.
        /// </summary>
        public static double GetDistanceAtPoint(GeoPolygon3 polygon, GeoPoint3 point) => GetDistanceAtPoint(polygon, point, Tolerance.Global);

        /// <summary>
        /// Gets the arc length from the first vertex of a polygon to the point on its boundary closest to
        /// the supplied point, within a tolerance.
        /// </summary>
        /// <remarks>
        /// This measures around the boundary, not across the interior: a point in the middle of a polygon
        /// still reports the position of the nearest place on its outline.
        /// </remarks>
        public static double GetDistanceAtPoint(GeoPolygon3 polygon, GeoPoint3 point, Tolerance tolerance)
        {
            if (polygon == null)
            {
                throw new ArgumentNullException(nameof(polygon));
            }

            double travelled = 0.0;
            double bestTravelled = 0.0;
            double bestDistanceSquared = double.MaxValue;

            for (int i = 0; i < polygon.EdgeCount; i++)
            {
                GeoLine3 edge = polygon.GetEdgeAt(i);
                GeoPoint3 candidate = Projection3.ProjectToLine(edge, point, tolerance);
                double distanceSquared = Distance3.GetDistanceSquaredTo(candidate, point);

                if (distanceSquared < bestDistanceSquared)
                {
                    bestDistanceSquared = distanceSquared;
                    bestTravelled = travelled + edge.StartPoint.DistanceTo(candidate);
                }

                travelled += edge.Length;
            }

            return bestTravelled;
        }

        /// <summary>
        /// Gets the normalized parameter of the point on the boundary of a polygon closest to the supplied
        /// point.
        /// </summary>
        public static double GetParameterAtPoint(GeoPolygon3 polygon, GeoPoint3 point) => GetParameterAtPoint(polygon, point, Tolerance.Global);

        /// <summary>
        /// Gets the normalized parameter of the point on the boundary of a polygon closest to the supplied
        /// point, within a tolerance.
        /// </summary>
        public static double GetParameterAtPoint(GeoPolygon3 polygon, GeoPoint3 point, Tolerance tolerance)
        {
            if (polygon == null)
            {
                throw new ArgumentNullException(nameof(polygon));
            }

            double length = polygon.Length;

            return length <= 0.0 ? 0.0 : GetDistanceAtPoint(polygon, point, tolerance) / length;
        }

        /// <summary>
        /// Gets the point at a normalized parameter around the circumference of a circle. The parameter
        /// wraps, so 1.25 gives the same point as 0.25.
        /// </summary>
        /// <remarks>
        /// A circle in space has no natural place to start measuring from, so the zero parameter sits on
        /// the first of the two axes <see cref="GeoPlane3.GetAxes"/> supplies for the carrying plane.
        /// Which direction that is stays the same for a given circle but is not otherwise specified.
        /// </remarks>
        public static GeoPoint3 GetPointAtParameter(GeoCircle3 circle, double parameter)
        {
            return circle.GetPointAtAngle(Wrap(parameter) * 2.0 * Math.PI);
        }

        /// <summary>
        /// Gets the arc length from the zero parameter of a circle to a normalized parameter around it.
        /// </summary>
        public static double GetDistanceAtParameter(GeoCircle3 circle, double parameter) => Wrap(parameter) * circle.Length;

        /// <summary>
        /// Gets the normalized parameter at an arc length measured around the circumference of a circle.
        /// </summary>
        public static double GetParameterAtDistance(GeoCircle3 circle, double distance)
        {
            double length = circle.Length;

            return length <= 0.0 ? 0.0 : Wrap(distance / length);
        }

        /// <summary>
        /// Gets the point at an arc length measured around the circumference of a circle.
        /// </summary>
        public static GeoPoint3 GetPointAtDistance(GeoCircle3 circle, double distance)
        {
            return GetPointAtParameter(circle, GetParameterAtDistance(circle, distance));
        }

        /// <summary>
        /// Gets the normalized parameter of the point on the circumference of a circle closest to the
        /// supplied point.
        /// </summary>
        /// <remarks>
        /// The point is projected onto the carrying plane first, then read as an angle about the centre. A
        /// point on the axis of the circle is the same distance from every point of the circumference, and
        /// there being no nearest one, the zero parameter comes back.
        /// </remarks>
        public static double GetParameterAtPoint(GeoCircle3 circle, GeoPoint3 point) => GetParameterAtPoint(circle, point, Tolerance.Global);

        /// <summary>
        /// Gets the normalized parameter of the point on the circumference of a circle closest to the
        /// supplied point, within a tolerance.
        /// </summary>
        public static double GetParameterAtPoint(GeoCircle3 circle, GeoPoint3 point, Tolerance tolerance)
        {
            GeoPlane3 plane = circle.GetPlane();
            plane.GetAxes(out GeoVector3 uAxis, out GeoVector3 vAxis);

            GeoVector3 radial = circle.Center.GetVectorTo(Projection3.ProjectToPlane(plane, point));

            if (radial.IsZeroLength(tolerance))
            {
                return 0.0;
            }

            double angle = Math.Atan2(radial.DotProduct(vAxis), radial.DotProduct(uAxis));

            return Wrap(angle / (2.0 * Math.PI));
        }

        /// <summary>
        /// Gets the arc length from the zero parameter of a circle to the point on its circumference
        /// closest to the supplied point.
        /// </summary>
        public static double GetDistanceAtPoint(GeoCircle3 circle, GeoPoint3 point) => GetDistanceAtPoint(circle, point, Tolerance.Global);

        /// <summary>
        /// Gets the arc length from the zero parameter of a circle to the point on its circumference
        /// closest to the supplied point, within a tolerance.
        /// </summary>
        public static double GetDistanceAtPoint(GeoCircle3 circle, GeoPoint3 point, Tolerance tolerance)
        {
            return GetParameterAtPoint(circle, point, tolerance) * circle.Length;
        }

        /// <summary>
        /// Wraps a normalized parameter into [0, 1), the form a closed curve measures in.
        /// </summary>
        /// <remarks>
        /// A closed curve has no end to run past: going once round and a quarter more is the same place as
        /// going a quarter round. That is what separates it from a polyline, which clamps because an open
        /// chain has nowhere further to go, and from a segment, which extrapolates along its carrier.
        /// </remarks>
        private static double Wrap(double parameter)
        {
            double wrapped = parameter % 1.0;

            if (wrapped < 0.0)
            {
                wrapped += 1.0;

                // A tiny negative input rounds up to exactly one when shifted, which would fall outside the
                // half-open range this promises.
                if (wrapped >= 1.0)
                {
                    wrapped = 0.0;
                }
            }

            return wrapped;
        }

        #endregion
    }
}
