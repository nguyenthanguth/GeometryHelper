using GeometryHelper;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// A plane asked how far it is from a spline, whether they touch, where they cross and where the shortest segment between them runs.
    /// </summary>
    public readonly partial struct GeoPlane3
    {
        #region Splines

        /// <summary>
        /// Calculates the shortest distance from this plane to a spline, using the default tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when the spline is null.</exception>
        public double DistanceTo(GeoSpline3 spline) => DistanceTo(spline, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance from this plane to a spline, within a tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly; the tolerance is not read.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when the spline is null.</exception>
        public double DistanceTo(GeoSpline3 spline, Tolerance tolerance) => Require(spline).DistanceTo(this, tolerance);

        /// <summary>
        /// Determines whether this plane touches a spline, using the default tolerance.
        /// </summary>
        /// <exception cref="System.ArgumentNullException">Thrown when the spline is null.</exception>
        public bool CollidesWith(GeoSpline3 spline) => CollidesWith(spline, Tolerance.Global);

        /// <summary>
        /// Determines whether this plane touches a spline, within a tolerance.
        /// </summary>
        /// <exception cref="System.ArgumentNullException">Thrown when the spline is null.</exception>
        public bool CollidesWith(GeoSpline3 spline, Tolerance tolerance) => Require(spline).CollidesWith(this, tolerance);

        /// <summary>
        /// Gets the points where this plane meets a spline, using the default tolerance.
        /// </summary>
        /// <exception cref="System.ArgumentNullException">Thrown when the spline is null.</exception>
        public GeoPoint3[] GetIntersections(GeoSpline3 spline) => GetIntersections(spline, Tolerance.Global);

        /// <summary>
        /// Gets the points where this plane meets a spline, within a tolerance.
        /// </summary>
        /// <returns>The same points the spline gives, in order along the spline from its start.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when the spline is null.</exception>
        public GeoPoint3[] GetIntersections(GeoSpline3 spline, Tolerance tolerance) => Require(spline).GetIntersections(this, tolerance);

        /// <summary>
        /// Tries to find where this plane meets a spline, using the default tolerance.
        /// </summary>
        /// <param name="spline">The spline.</param>
        /// <param name="intersections">The points when the method returns true; empty otherwise.</param>
        /// <exception cref="System.ArgumentNullException">Thrown when the spline is null.</exception>
        public bool TryIntersectWith(GeoSpline3 spline, out GeoPoint3[] intersections) => TryIntersectWith(spline, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where this plane meets a spline, within a tolerance.
        /// </summary>
        /// <param name="spline">The spline.</param>
        /// <param name="intersections">The points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <exception cref="System.ArgumentNullException">Thrown when the spline is null.</exception>
        public bool TryIntersectWith(GeoSpline3 spline, out GeoPoint3[] intersections, Tolerance tolerance) => Require(spline).TryIntersectWith(this, out intersections, tolerance);

        /// <summary>
        /// Gets the shortest segment leaving this plane and landing on a spline, using the default tolerance.
        /// </summary>
        /// <exception cref="System.ArgumentNullException">Thrown when the spline is null.</exception>
        public GeoLine3 GetShortestLineTo(GeoSpline3 spline) => GetShortestLineTo(spline, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving this plane and landing on a spline, within a tolerance.
        /// </summary>
        /// <returns>The segment the spline gives, turned round: it starts on this plane and lands on the spline.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when the spline is null.</exception>
        public GeoLine3 GetShortestLineTo(GeoSpline3 spline, Tolerance tolerance) => Require(spline).GetShortestLineTo(this, tolerance).Reverse();

        private static GeoSpline3 Require(GeoSpline3 spline) => spline ?? throw new System.ArgumentNullException(nameof(spline));

        #endregion
    }
}
