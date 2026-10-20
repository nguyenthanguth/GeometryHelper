using GeometryHelper.Internal.Nurbs;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// How far a spline is from a point, and whether a point lies on it.
    /// </summary>
    public sealed partial class GeoSpline2
    {
        /// <summary>
        /// Gets the distance from this spline to a point, using the default tolerance.
        /// </summary>
        /// <param name="point">The point.</param>
        /// <returns>
        /// The distance from the point to the nearest point of the curve, which is a line and not a region: a point inside a
        /// closed spline is as far from it as from the nearest of its points. Nought only where the point truly lies on it,
        /// otherwise the gap, measured exactly; exact to rounding, however the curve bends.
        /// </returns>
        public double DistanceTo(GeoPoint2 point) => DistanceTo(point, Tolerance.Global);

        /// <summary>
        /// Gets the distance from this spline to a point, within a tolerance.
        /// </summary>
        /// <param name="point">The point.</param>
        /// <param name="tolerance">The tolerance; not read, since the nearest point takes none.</param>
        /// <returns>
        /// The distance from the point to the nearest point of the curve, measured exactly. The circle of radius 100 about the
        /// origin, as <see cref="FromCircle(GeoCircle2)"/> makes it, is 60 from (160, 0) and 100 from its own centre.
        /// </returns>
        /// <remarks>
        /// The nearest point is found as <see cref="GetKnotAtPoint(GeoPoint2)"/> finds it: on each span the parameters
        /// where the curve stops coming nearer, all of them, from the roots of a polynomial in Bernstein form, and the ends;
        /// whichever span cannot be nearer than the best found, by the box of its control points, is not solved. The boxes of
        /// all the spans are looked at, though, so a query costs time in proportion to the number of spans, 0.4 to 0.7 ms at 100 000, and a
        /// span solved costs about a microsecond; an index over the spans is not here yet.
        /// </remarks>
        public double DistanceTo(GeoPoint2 point, Tolerance tolerance)
        {
            _curve.ClosestParameter(new[] { point.X, point.Y }, out double distance);

            return distance;
        }

        /// <summary>
        /// Determines whether a point lies on the spline, within the default tolerance.
        /// </summary>
        public bool IsPointOn(GeoPoint2 point) => IsPointOn(point, Tolerance.Global);

        /// <summary>
        /// Determines whether a point lies on the spline, within a tolerance: within the point tolerance of its nearest point.
        /// </summary>
        public bool IsPointOn(GeoPoint2 point, Tolerance tolerance) => DistanceTo(point, tolerance) <= tolerance.EqualPoint;
    }
}
