using System;
using GeometryHelper;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// An oriented box asked about a ball: how far apart they are, whether they touch, and where the shortest segment between them runs.
    /// </summary>
    public sealed partial class GeoObb3
    {
        #region Spheres

        /// <summary>
        /// Calculates the shortest distance from this oriented box to a ball.
        /// </summary>
        /// <returns>Nought where the oriented box reaches the ball, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoSphere3 sphere) => sphere.DistanceTo(this);

        /// <summary>
        /// Determines whether this oriented box touches a ball, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoSphere3 sphere) => CollidesWith(sphere, Tolerance.Global);

        /// <summary>
        /// Determines whether this oriented box touches a ball, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoSphere3 sphere, Tolerance tolerance) => sphere.CollidesWith(this, tolerance);

        /// <summary>
        /// Gets the shortest segment leaving this oriented box and landing on a ball, using the default tolerance.
        /// </summary>
        public GeoLine3 GetShortestLineTo(GeoSphere3 sphere) => GetShortestLineTo(sphere, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving this oriented box and landing on a ball, within a tolerance.
        /// </summary>
        /// <returns>The segment of <see cref="GeoSphere3.GetShortestLineTo(GeoObb3, Tolerance)"/> turned round, so that it leaves this oriented box and lands on the surface of the ball.</returns>
        public GeoLine3 GetShortestLineTo(GeoSphere3 sphere, Tolerance tolerance) => sphere.GetShortestLineTo(this, tolerance).Reverse();

        #endregion
    }
}
