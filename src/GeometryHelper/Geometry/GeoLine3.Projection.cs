using System;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// The point of a segment nearest another shape, the shortest segment joining the two, and which
    /// edge of it faces one.
    /// </summary>
    public readonly partial struct GeoLine3
    {
        /// <summary>
        /// Gets the closest point on this segment to a target point, clamped to the endpoints.
        /// </summary>
        public GeoPoint3 GetClosestPointOnBoundary(GeoPoint3 point) => Projection3.ProjectToLine(this, point);

        /// <summary>
        /// Finds the shortest segment connecting a point on this segment to a point on another segment,
        /// using the default tolerance.
        /// </summary>
        public GeoLine3 GetShortestLineTo(GeoLine3 other) => Projection3.GetShortestLineTo(this, other);

        /// <summary>
        /// Finds the shortest segment connecting a point on this segment to a point on another segment,
        /// within a tolerance.
        /// </summary>
        public GeoLine3 GetShortestLineTo(GeoLine3 other, Tolerance tolerance) => Projection3.GetShortestLineTo(this, other, tolerance);

        /// <summary>
        /// Finds the shortest segment connecting this segment and another, reading this one, the other or both
        /// as the infinite line carrying it, using the default tolerance. With <see cref="LineExtension.Both"/>
        /// this is Tekla's <c>Intersection.LineToLine</c>.
        /// </summary>
        /// <param name="other">The other segment.</param>
        /// <param name="extension">Which segment may be reached past its endpoints: First is this one, Second the other.</param>
        public GeoLine3 GetShortestLineTo(GeoLine3 other, LineExtension extension) => Projection3.GetShortestLineTo(this, other, extension, Tolerance.Global);

        /// <summary>
        /// Finds the shortest segment connecting this segment and another, reading this one, the other or both
        /// as the infinite line carrying it, within a tolerance.
        /// </summary>
        /// <param name="other">The other segment.</param>
        /// <param name="extension">Which segment may be reached past its endpoints: First is this one, Second the other.</param>
        /// <param name="tolerance">The tolerance.</param>
        public GeoLine3 GetShortestLineTo(GeoLine3 other, LineExtension extension, Tolerance tolerance) => Projection3.GetShortestLineTo(this, other, extension, tolerance);

        /// <summary>
        /// Gets the shortest segment joining this one to a triangle, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// The segment leaves this segment and lands on the triangle, so its length is the clearance
        /// between them and nought where they touch.
        /// </remarks>
        public GeoLine3 GetShortestLineTo(GeoTriangle3 triangle) => Projection3.GetShortestLineTo(this, triangle);

        /// <summary>
        /// Gets the shortest segment joining this one to a triangle, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The segment leaves this segment and lands on the triangle, so its length is the clearance
        /// between them and nought where they touch.
        /// </remarks>
        public GeoLine3 GetShortestLineTo(GeoTriangle3 triangle, Tolerance tolerance) => Projection3.GetShortestLineTo(this, triangle, tolerance);

        /// <summary>
        /// Gets the shortest segment joining this one to a ray, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// The segment leaves this segment and lands on the ray, so its length is the clearance
        /// between them and nought where they touch.
        /// </remarks>
        public GeoLine3 GetShortestLineTo(GeoRay3 ray) => Projection3.GetShortestLineTo(ray, this).Reverse();

        /// <summary>
        /// Gets the shortest segment joining this one to a ray, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The segment leaves this segment and lands on the ray, so its length is the clearance
        /// between them and nought where they touch.
        /// </remarks>
        public GeoLine3 GetShortestLineTo(GeoRay3 ray, Tolerance tolerance) => Projection3.GetShortestLineTo(ray, this, tolerance).Reverse();

        /// <summary>
        /// Gets the shortest segment joining this one to a solid, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// The segment leaves this segment and lands on the solid, so its length is the clearance
        /// between them and nought where they touch.
        /// </remarks>
        public GeoLine3 GetShortestLineTo(GeoSolid3 solid) => Projection3.GetShortestLineTo(solid, this).Reverse();

        /// <summary>
        /// Gets the shortest segment joining this one to a solid, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The segment leaves this segment and lands on the solid, so its length is the clearance
        /// between them and nought where they touch.
        /// </remarks>
        public GeoLine3 GetShortestLineTo(GeoSolid3 solid, Tolerance tolerance) => Projection3.GetShortestLineTo(solid, this, tolerance).Reverse();
    }
}
