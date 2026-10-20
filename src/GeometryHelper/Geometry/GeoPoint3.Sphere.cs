using System;
using GeometryHelper;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// A point asked about a ball: how far apart they are, whether they touch, and where the shortest segment between them runs.
    /// </summary>
    public readonly partial struct GeoPoint3
    {
        #region Spheres

        /// <summary>
        /// Calculates the shortest distance from this point to a ball.
        /// </summary>
        /// <returns>Nought where the point lies in the ball, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoSphere3 sphere) => sphere.DistanceTo(this);

        /// <summary>
        /// Gets the shortest segment leaving this point and landing on a ball, using the default tolerance.
        /// </summary>
        public GeoLine3 GetShortestLineTo(GeoSphere3 sphere) => GetShortestLineTo(sphere, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving this point and landing on a ball, within a tolerance.
        /// </summary>
        /// <returns>The segment of <see cref="GeoSphere3.GetShortestLineTo(GeoPoint3, Tolerance)"/> turned round, so that it leaves this point and lands on the surface of the ball.</returns>
        public GeoLine3 GetShortestLineTo(GeoSphere3 sphere, Tolerance tolerance) => sphere.GetShortestLineTo(this, tolerance).Reverse();

        #endregion
    }
}
