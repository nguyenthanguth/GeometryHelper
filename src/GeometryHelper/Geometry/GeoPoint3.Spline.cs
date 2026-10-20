using GeometryHelper;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// A point asked how far it is from a spline, which of the spline is nearest, and where the shortest segment between
    /// them runs.
    /// </summary>
    public readonly partial struct GeoPoint3
    {
        #region Splines

        /// <summary>
        /// Calculates the shortest distance from this point to a spline, using the default tolerance.
        /// </summary>
        /// <returns>Nought only where the point truly lies on the spline, otherwise the gap, measured exactly.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when the spline is null.</exception>
        public double DistanceTo(GeoSpline3 spline) => DistanceTo(spline, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance from this point to a spline, within a tolerance.
        /// </summary>
        /// <returns>Nought only where the point truly lies on the spline, otherwise the gap, measured exactly; the tolerance is not read.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when the spline is null.</exception>
        public double DistanceTo(GeoSpline3 spline, Tolerance tolerance) => Require(spline).DistanceTo(this, tolerance);

        /// <summary>
        /// Gets the point of a spline nearest this point.
        /// </summary>
        /// <exception cref="System.ArgumentNullException">Thrown when the spline is null.</exception>
        public GeoPoint3 GetClosestPointOnBoundary(GeoSpline3 spline) => Require(spline).GetClosestPointOnBoundary(this);

        /// <summary>
        /// Gets the shortest segment leaving this point and landing on a spline, using the default tolerance.
        /// </summary>
        /// <exception cref="System.ArgumentNullException">Thrown when the spline is null.</exception>
        public GeoLine3 GetShortestLineTo(GeoSpline3 spline) => GetShortestLineTo(spline, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving this point and landing on a spline, within a tolerance.
        /// </summary>
        /// <exception cref="System.ArgumentNullException">Thrown when the spline is null.</exception>
        public GeoLine3 GetShortestLineTo(GeoSpline3 spline, Tolerance tolerance) => Require(spline).GetShortestLineTo(this, tolerance).Reverse();

        /// <summary>
        /// Determines whether this point lies on a spline, within the default tolerance.
        /// </summary>
        /// <exception cref="System.ArgumentNullException">Thrown when the spline is null.</exception>
        public bool IsPointOn(GeoSpline3 spline) => IsPointOn(spline, Tolerance.Global);

        /// <summary>
        /// Determines whether this point lies on a spline, within a tolerance.
        /// </summary>
        /// <exception cref="System.ArgumentNullException">Thrown when the spline is null.</exception>
        public bool IsPointOn(GeoSpline3 spline, Tolerance tolerance) => Require(spline).IsPointOn(this, tolerance);

        private static GeoSpline3 Require(GeoSpline3 spline) => spline ?? throw new System.ArgumentNullException(nameof(spline));

        #endregion
    }
}
