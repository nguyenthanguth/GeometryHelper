using GeometryHelper.Internal.Nurbs;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// How far a spline is from a point, and whether a point lies on it.
    /// </summary>
    public sealed partial class GeoSpline3
    {
        /// <summary>
        /// Gets the distance from this spline to a point, using the default tolerance.
        /// </summary>
        /// <param name="point">The point.</param>
        /// <returns>
        /// The distance from the point to the nearest point of the curve. Nought only where the point truly lies on it,
        /// otherwise the gap, measured exactly; exact to rounding, however the curve bends.
        /// </returns>
        public double DistanceTo(GeoPoint3 point) => DistanceTo(point, Tolerance.Global);

        /// <summary>
        /// Gets the distance from this spline to a point, within a tolerance.
        /// </summary>
        /// <param name="point">The point.</param>
        /// <param name="tolerance">The tolerance; not read, since the nearest point takes none.</param>
        /// <returns>
        /// The distance from the point to the nearest point of the curve, measured exactly. The circle of radius 100 about
        /// the origin in the floor, as <see cref="FromCircle(GeoCircle3)"/> makes it, is 60 from (160, 0, 0), 100 from its
        /// own centre and 104.4 from (0, 0, 30), which is 100 across and 30 up from every point of the rim.
        /// </returns>
        /// <remarks>
        /// The nearest point is found as <see cref="GetKnotAtPoint(GeoPoint3)"/> finds it: on each span the parameters
        /// where the curve stops coming nearer, all of them, from the roots of a polynomial in Bernstein form, and the ends;
        /// whichever span cannot be nearer than the best found, by the box of its control points, is not solved. The boxes are
        /// kept in a tree, built once, so the spans near the point are reached without looking at the rest: a query costs time in
        /// the logarithm of the number of spans and the number whose boxes are near the point, as in the plane.
        /// </remarks>
        public double DistanceTo(GeoPoint3 point, Tolerance tolerance)
        {
            _curve.ClosestParameter(new[] { point.X, point.Y, point.Z }, out double distance);

            return distance;
        }

        /// <summary>
        /// Determines whether a point lies on the spline, within the default tolerance.
        /// </summary>
        public bool IsPointOn(GeoPoint3 point) => IsPointOn(point, Tolerance.Global);

        /// <summary>
        /// Determines whether a point lies on the spline, within a tolerance: within the point tolerance of its nearest point.
        /// </summary>
        public bool IsPointOn(GeoPoint3 point, Tolerance tolerance) => DistanceTo(point, tolerance) <= tolerance.EqualPoint;
    }
}
