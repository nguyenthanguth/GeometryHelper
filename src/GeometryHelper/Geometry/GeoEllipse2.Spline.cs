using GeometryHelper;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// An ellipse asked how far it is from a spline, whether they touch, where they cross and where the shortest segment between them runs.
    /// </summary>
    public readonly partial struct GeoEllipse2
    {
        #region Splines

        /// <summary>
        /// Calculates the shortest distance from this ellipse to a spline, using the default tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when the spline is null.</exception>
        public double DistanceTo(GeoSpline2 spline) => DistanceTo(spline, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance from this ellipse to a spline, within a tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly; the tolerance is not read.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when the spline is null.</exception>
        public double DistanceTo(GeoSpline2 spline, Tolerance tolerance) => Require(spline).DistanceTo(this, tolerance);

        /// <summary>
        /// Determines whether this ellipse touches a spline, using the default tolerance.
        /// </summary>
        /// <exception cref="System.ArgumentNullException">Thrown when the spline is null.</exception>
        public bool CollidesWith(GeoSpline2 spline) => CollidesWith(spline, Tolerance.Global);

        /// <summary>
        /// Determines whether this ellipse touches a spline, within a tolerance.
        /// </summary>
        /// <exception cref="System.ArgumentNullException">Thrown when the spline is null.</exception>
        public bool CollidesWith(GeoSpline2 spline, Tolerance tolerance) => Require(spline).CollidesWith(this, tolerance);

        /// <summary>
        /// Gets the points where this ellipse crosses a spline, using the default tolerance.
        /// </summary>
        /// <exception cref="System.ArgumentNullException">Thrown when the spline is null.</exception>
        public GeoPoint2[] GetIntersections(GeoSpline2 spline) => GetIntersections(spline, Tolerance.Global);

        /// <summary>
        /// Gets the points where this ellipse crosses a spline, within a tolerance.
        /// </summary>
        /// <returns>The same points the spline gives, in order along the spline from its start.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when the spline is null.</exception>
        public GeoPoint2[] GetIntersections(GeoSpline2 spline, Tolerance tolerance) => Require(spline).GetIntersections(this, tolerance);

        /// <summary>
        /// Tries to find where this ellipse crosses a spline, using the default tolerance.
        /// </summary>
        /// <param name="spline">The spline.</param>
        /// <param name="intersections">The points when the method returns true; empty otherwise.</param>
        /// <exception cref="System.ArgumentNullException">Thrown when the spline is null.</exception>
        public bool TryIntersectWith(GeoSpline2 spline, out GeoPoint2[] intersections) => TryIntersectWith(spline, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where this ellipse crosses a spline, within a tolerance.
        /// </summary>
        /// <param name="spline">The spline.</param>
        /// <param name="intersections">The points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <exception cref="System.ArgumentNullException">Thrown when the spline is null.</exception>
        public bool TryIntersectWith(GeoSpline2 spline, out GeoPoint2[] intersections, Tolerance tolerance) => Require(spline).TryIntersectWith(this, out intersections, tolerance);

        /// <summary>
        /// Gets the shortest segment leaving this ellipse and landing on a spline, using the default tolerance.
        /// </summary>
        /// <exception cref="System.ArgumentNullException">Thrown when the spline is null.</exception>
        public GeoLine2 GetShortestLineTo(GeoSpline2 spline) => GetShortestLineTo(spline, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving this ellipse and landing on a spline, within a tolerance.
        /// </summary>
        /// <returns>The segment the spline gives, turned round: it starts on this ellipse and lands on the spline.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when the spline is null.</exception>
        public GeoLine2 GetShortestLineTo(GeoSpline2 spline, Tolerance tolerance) => Require(spline).GetShortestLineTo(this, tolerance).Reverse();

        private static GeoSpline2 Require(GeoSpline2 spline) => spline ?? throw new System.ArgumentNullException(nameof(spline));

        #endregion
    }
}
