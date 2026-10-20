using System;
using GeometryHelper;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// An infinite line asked about a ball: how far apart they are, whether they touch, where they cross, and where the shortest segment between them runs.
    /// </summary>
    public readonly partial struct GeoXLine3
    {
        #region Spheres

        /// <summary>
        /// Calculates the shortest distance from this infinite line to a ball, using the default tolerance.
        /// </summary>
        /// <returns>Nought where the infinite line reaches the ball, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoSphere3 sphere) => DistanceTo(sphere, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance from this infinite line to a ball, within a tolerance.
        /// </summary>
        /// <returns>Nought where the infinite line reaches the ball, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoSphere3 sphere, Tolerance tolerance) => sphere.DistanceTo(this, tolerance);

        /// <summary>
        /// Determines whether this infinite line touches a ball, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoSphere3 sphere) => CollidesWith(sphere, Tolerance.Global);

        /// <summary>
        /// Determines whether this infinite line touches a ball, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoSphere3 sphere, Tolerance tolerance) => sphere.CollidesWith(this, tolerance);

        /// <summary>
        /// Gets the points where this infinite line crosses the surface of a ball, using the default tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoSphere3 sphere) => GetIntersections(sphere, Tolerance.Global);

        /// <summary>
        /// Gets the points where this infinite line crosses the surface of a ball, within a tolerance.
        /// </summary>
        public GeoPoint3[] GetIntersections(GeoSphere3 sphere, Tolerance tolerance) => sphere.GetIntersections(this, tolerance);

        /// <summary>
        /// Tries to find where this infinite line crosses the surface of a ball, using the default tolerance.
        /// </summary>
        /// <param name="sphere">The ball.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        public bool TryIntersectWith(GeoSphere3 sphere, out GeoPoint3[] intersections) => TryIntersectWith(sphere, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where this infinite line crosses the surface of a ball, within a tolerance.
        /// </summary>
        /// <param name="sphere">The ball.</param>
        /// <param name="intersections">The crossing points when the method returns true; empty otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryIntersectWith(GeoSphere3 sphere, out GeoPoint3[] intersections, Tolerance tolerance) => sphere.TryIntersectWith(this, out intersections, tolerance);

        /// <summary>
        /// Gets the shortest segment leaving this infinite line and landing on a ball, using the default tolerance.
        /// </summary>
        public GeoLine3 GetShortestLineTo(GeoSphere3 sphere) => GetShortestLineTo(sphere, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving this infinite line and landing on a ball, within a tolerance.
        /// </summary>
        /// <returns>The segment of <see cref="GeoSphere3.GetShortestLineTo(GeoXLine3, Tolerance)"/> turned round, so that it leaves this infinite line and lands on the surface of the ball.</returns>
        public GeoLine3 GetShortestLineTo(GeoSphere3 sphere, Tolerance tolerance) => sphere.GetShortestLineTo(this, tolerance).Reverse();

        #endregion
    }
}
