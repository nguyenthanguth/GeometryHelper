using System;
using GeometryHelper;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// The point of a ray nearest another shape, the shortest segment joining the two, and which
    /// edge of it faces one.
    /// </summary>
    public readonly partial struct GeoRay3
    {
        /// <summary>
        /// Gets the closest point on this ray to a target point, clamped to the origin.
        /// </summary>
        public GeoPoint3 GetClosestPointOnBoundary(GeoPoint3 point) => Projection3.ProjectToRay(this, point);

        /// <summary>
        /// Gets the shortest segment joining this ray to a segment, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// The segment leaves this ray and lands on the segment, so its length is the clearance
        /// between them and nought where they touch.
        /// </remarks>
        public GeoLine3 GetShortestLineTo(GeoLine3 line) => Projection3.GetShortestLineTo(this, line);

        /// <summary>
        /// Gets the shortest segment joining this ray to a segment, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The segment leaves this ray and lands on the segment, so its length is the clearance
        /// between them and nought where they touch.
        /// </remarks>
        public GeoLine3 GetShortestLineTo(GeoLine3 line, Tolerance tolerance) => Projection3.GetShortestLineTo(this, line, tolerance);

        /// <summary>
        /// Gets the shortest segment joining this ray to a triangle, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// The segment leaves this ray and lands on the triangle, so its length is the clearance
        /// between them and nought where they touch.
        /// </remarks>
        public GeoLine3 GetShortestLineTo(GeoTriangle3 triangle) => Projection3.GetShortestLineTo(this, triangle);

        /// <summary>
        /// Gets the shortest segment joining this ray to a triangle, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The segment leaves this ray and lands on the triangle, so its length is the clearance
        /// between them and nought where they touch.
        /// </remarks>
        public GeoLine3 GetShortestLineTo(GeoTriangle3 triangle, Tolerance tolerance) => Projection3.GetShortestLineTo(this, triangle, tolerance);

        /// <summary>
        /// Gets the shortest segment joining this ray to a solid, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// The segment leaves this ray and lands on the solid, so its length is the clearance
        /// between them and nought where they touch.
        /// </remarks>
        public GeoLine3 GetShortestLineTo(GeoSolid3 solid) => Projection3.GetShortestLineTo(solid, this).Reverse();

        /// <summary>
        /// Gets the shortest segment joining this ray to a solid, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The segment leaves this ray and lands on the solid, so its length is the clearance
        /// between them and nought where they touch.
        /// </remarks>
        public GeoLine3 GetShortestLineTo(GeoSolid3 solid, Tolerance tolerance) => Projection3.GetShortestLineTo(solid, this, tolerance).Reverse();
    }
}
