using System;
using GeometryHelper;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// A plane asked about a ball: how far apart they are, whether they touch, and where the shortest segment between them runs.
    /// </summary>
    public readonly partial struct GeoPlane3
    {
        #region Spheres

        /// <summary>
        /// Calculates the shortest distance from this plane to a ball.
        /// </summary>
        /// <returns>Nought where the plane reaches the ball, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoSphere3 sphere) => sphere.DistanceTo(this);

        /// <summary>
        /// Determines whether this plane touches a ball, using the default tolerance.
        /// </summary>
        public bool CollidesWith(GeoSphere3 sphere) => CollidesWith(sphere, Tolerance.Global);

        /// <summary>
        /// Determines whether this plane touches a ball, within a tolerance.
        /// </summary>
        public bool CollidesWith(GeoSphere3 sphere, Tolerance tolerance) => sphere.CollidesWith(this, tolerance);

        /// <summary>
        /// Tries to find the circle this plane cuts from the surface of a ball, using the default tolerance.
        /// </summary>
        /// <param name="sphere">The ball.</param>
        /// <param name="circle">The circle when the method returns true; default otherwise.</param>
        public bool TryGetIntersectionCircle(GeoSphere3 sphere, out GeoCircle3 circle) => sphere.TryGetIntersectionCircle(this, out circle);

        /// <summary>
        /// Tries to find the circle this plane cuts from the surface of a ball, within a tolerance.
        /// </summary>
        /// <param name="sphere">The ball.</param>
        /// <param name="circle">The circle when the method returns true; default otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryGetIntersectionCircle(GeoSphere3 sphere, out GeoCircle3 circle, Tolerance tolerance) => sphere.TryGetIntersectionCircle(this, out circle, tolerance);

        /// <summary>
        /// Tries to find the point where this plane only touches the surface of a ball, using the default tolerance.
        /// </summary>
        /// <param name="sphere">The ball.</param>
        /// <param name="point">The point when the method returns true; default otherwise.</param>
        public bool TryGetTangentPoint(GeoSphere3 sphere, out GeoPoint3 point) => sphere.TryGetTangentPoint(this, out point);

        /// <summary>
        /// Tries to find the point where this plane only touches the surface of a ball, within a tolerance.
        /// </summary>
        /// <param name="sphere">The ball.</param>
        /// <param name="point">The point when the method returns true; default otherwise.</param>
        /// <param name="tolerance">The tolerance.</param>
        public bool TryGetTangentPoint(GeoSphere3 sphere, out GeoPoint3 point, Tolerance tolerance) => sphere.TryGetTangentPoint(this, out point, tolerance);

        /// <summary>
        /// Gets the shortest segment leaving this plane and landing on a ball, using the default tolerance.
        /// </summary>
        public GeoLine3 GetShortestLineTo(GeoSphere3 sphere) => GetShortestLineTo(sphere, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving this plane and landing on a ball, within a tolerance.
        /// </summary>
        /// <returns>The segment of <see cref="GeoSphere3.GetShortestLineTo(GeoPlane3, Tolerance)"/> turned round, so that it leaves this plane and lands on the surface of the ball.</returns>
        public GeoLine3 GetShortestLineTo(GeoSphere3 sphere, Tolerance tolerance) => sphere.GetShortestLineTo(this, tolerance).Reverse();

        #endregion
    }
}
