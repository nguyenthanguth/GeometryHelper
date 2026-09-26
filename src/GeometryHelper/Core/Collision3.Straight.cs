using System;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    /// <summary>
    /// Whether two straight pieces in space touch.
    /// </summary>
    /// <remarks>
    /// A collision between two open shapes is the crossing test with the place thrown away, except in the one
    /// case where there is no place to throw away: two pieces lying along each other touch along a length and
    /// name no point. So this is not <c>GetIntersections(...).Length &gt; 0</c> — it is the shortest line between
    /// the two having no length, which answers that case as well, and is the same reading two arcs of one circle
    /// already get.
    /// </remarks>
    public static partial class Collision3
    {
        /// <summary>
        /// Determines whether two segments touch.
        /// </summary>
        public static bool CollidesWith(GeoLine3 line1, GeoLine3 line2) => CollidesWith(line1, line2, Tolerance.Global);

        /// <summary>
        /// Determines whether two segments touch, within a tolerance.
        /// </summary>
        /// <param name="line1">The first segment.</param>
        /// <param name="line2">The second segment.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// true when they meet, whether at a point or along a length; false when they are apart or skew.
        /// </returns>
        public static bool CollidesWith(GeoLine3 line1, GeoLine3 line2, Tolerance tolerance)
            => Projection3.GetShortestLineTo(line1, line2, tolerance).Length <= tolerance.EqualPoint;

        /// <summary>
        /// Determines whether a segment touches a ray.
        /// </summary>
        public static bool CollidesWith(GeoLine3 line, GeoRay3 ray) => CollidesWith(line, ray, Tolerance.Global);

        /// <summary>
        /// Determines whether a segment touches a ray, within a tolerance.
        /// </summary>
        /// <param name="line">The segment.</param>
        /// <param name="ray">The ray.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when they meet, whether at a point or along a length; otherwise, false.</returns>
        public static bool CollidesWith(GeoLine3 line, GeoRay3 ray, Tolerance tolerance)
            => Projection3.GetShortestLineTo(ray, line, tolerance).Length <= tolerance.EqualPoint;

        /// <summary>
        /// Determines whether a ray touches a segment.
        /// </summary>
        public static bool CollidesWith(GeoRay3 ray, GeoLine3 line) => CollidesWith(line, ray, Tolerance.Global);

        /// <summary>
        /// Determines whether a ray touches a segment, within a tolerance.
        /// </summary>
        /// <param name="ray">The ray.</param>
        /// <param name="line">The segment.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when they meet, whether at a point or along a length; otherwise, false.</returns>
        public static bool CollidesWith(GeoRay3 ray, GeoLine3 line, Tolerance tolerance) => CollidesWith(line, ray, tolerance);
    }
}
