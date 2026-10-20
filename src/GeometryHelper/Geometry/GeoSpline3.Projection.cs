namespace GeometryHelper.Geometry
{
    /// <summary>
    /// The point of a spline nearest a point, where on the spline it lies, and the shortest segment joining the two.
    /// </summary>
    public sealed partial class GeoSpline3
    {
        /// <summary>
        /// Gets the value of the knots at the point of the spline nearest a point.
        /// </summary>
        /// <param name="point">The point; it does not have to lie on the spline.</param>
        /// <returns>
        /// A value of the domain, from <see cref="DomainStart"/> to <see cref="DomainEnd"/>: the knot value, which
        /// <see cref="GetPointAtKnot(double)"/> turns back into the point. Where two points of the spline are equally near,
        /// the lower one.
        /// </returns>
        /// <remarks>
        /// The nearest point is a point where the spline stops coming nearer, or an end. On each span that is a root of a
        /// polynomial in Bernstein form, and all of them are found, to rounding.
        /// </remarks>
        public double GetKnotAtPoint(GeoPoint3 point) => _curve.ClosestParameter(new[] { point.X, point.Y, point.Z }, out _);

        /// <summary>
        /// Gets the normalised parameter, from 0 to 1, of the point of the spline nearest a point, using the default tolerance.
        /// </summary>
        /// <param name="point">The point; it does not have to lie on the spline.</param>
        /// <returns>The share of the domain: 0 at the start and 1 at the end; <see cref="GetKnotAtPoint(GeoPoint3)"/> gives the knot value.</returns>
        public double GetParameterAtPoint(GeoPoint3 point) => GetParameterAtPoint(point, Tolerance.Global);

        /// <summary>
        /// Gets the normalised parameter, from 0 to 1, of the point of the spline nearest a point, within a tolerance.
        /// </summary>
        /// <param name="point">The point; it does not have to lie on the spline.</param>
        /// <param name="tolerance">The tolerance; not read, since the nearest point takes none.</param>
        /// <returns>The share of the domain: 0 at the start and 1 at the end.</returns>
        public double GetParameterAtPoint(GeoPoint3 point, Tolerance tolerance) => _curve.ParameterAtKnot(GetKnotAtPoint(point));

        /// <summary>
        /// Gets how far along the spline the point nearest a point lies, using the default tolerance.
        /// </summary>
        /// <param name="point">The point; it does not have to lie on the spline.</param>
        /// <returns>The length from the start of the spline to its nearest point.</returns>
        public double GetDistanceAtPoint(GeoPoint3 point) => GetDistanceAtPoint(point, Tolerance.Global);

        /// <summary>
        /// Gets how far along the spline the point nearest a point lies, within a tolerance.
        /// </summary>
        /// <param name="point">The point; it does not have to lie on the spline.</param>
        /// <param name="tolerance">The tolerance; not read, since the nearest point takes none.</param>
        /// <returns>The length from the start of the spline to its nearest point.</returns>
        public double GetDistanceAtPoint(GeoPoint3 point, Tolerance tolerance) => _curve.LengthTo(GetKnotAtPoint(point));

        /// <summary>
        /// Gets the point of this spline nearest a point.
        /// </summary>
        /// <param name="point">The point; it does not have to lie on the spline.</param>
        /// <returns>The spline's point at <see cref="GetKnotAtPoint(GeoPoint3)"/>.</returns>
        public GeoPoint3 GetClosestPointOnBoundary(GeoPoint3 point) => GetPointAtKnot(GetKnotAtPoint(point));

        /// <summary>
        /// Gets the shortest segment leaving this spline and landing on a point, using the default tolerance.
        /// </summary>
        /// <returns>The segment from the spline's nearest point to the point; as long as <see cref="DistanceTo(GeoPoint3)"/>, to rounding.</returns>
        public GeoLine3 GetShortestLineTo(GeoPoint3 point) => GetShortestLineTo(point, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving this spline and landing on a point, within a tolerance.
        /// </summary>
        /// <param name="point">The point.</param>
        /// <param name="tolerance">The tolerance; not read, since the nearest point takes none.</param>
        public GeoLine3 GetShortestLineTo(GeoPoint3 point, Tolerance tolerance) => new GeoLine3(GetClosestPointOnBoundary(point), point);
    }
}
