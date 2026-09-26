using System;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// The point of a triangle nearest another shape, the shortest segment joining the two, and which
    /// edge of it faces one.
    /// </summary>
    public readonly partial struct GeoTriangle3
    {
        /// <summary>
        /// Gets the closest point on this triangle to a target point.
        /// </summary>
        public GeoPoint3 GetClosestPointOnBoundary(GeoPoint3 point) => Projection3.ProjectToTriangle(this, point);

        /// <summary>
        /// Gets the shortest segment joining this triangle to another, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// The segment leaves this triangle and lands on the other triangle, so its length is the clearance
        /// between them and nought where they touch.
        /// </remarks>
        public GeoLine3 GetShortestLineTo(GeoTriangle3 other) => Projection3.GetShortestLineTo(this, other);

        /// <summary>
        /// Gets the shortest segment joining this triangle to another, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The segment leaves this triangle and lands on the other triangle, so its length is the clearance
        /// between them and nought where they touch.
        /// </remarks>
        public GeoLine3 GetShortestLineTo(GeoTriangle3 other, Tolerance tolerance) => Projection3.GetShortestLineTo(this, other, tolerance);

        /// <summary>
        /// Gets the shortest segment joining this triangle to a segment, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// The segment leaves this triangle and lands on the segment, so its length is the clearance
        /// between them and nought where they touch.
        /// </remarks>
        public GeoLine3 GetShortestLineTo(GeoLine3 line) => Projection3.GetShortestLineTo(line, this).Reverse();

        /// <summary>
        /// Gets the shortest segment joining this triangle to a segment, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The segment leaves this triangle and lands on the segment, so its length is the clearance
        /// between them and nought where they touch.
        /// </remarks>
        public GeoLine3 GetShortestLineTo(GeoLine3 line, Tolerance tolerance) => Projection3.GetShortestLineTo(line, this, tolerance).Reverse();

        /// <summary>
        /// Gets the shortest segment joining this triangle to a ray, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// The segment leaves this triangle and lands on the ray, so its length is the clearance
        /// between them and nought where they touch.
        /// </remarks>
        public GeoLine3 GetShortestLineTo(GeoRay3 ray) => Projection3.GetShortestLineTo(ray, this).Reverse();

        /// <summary>
        /// Gets the shortest segment joining this triangle to a ray, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The segment leaves this triangle and lands on the ray, so its length is the clearance
        /// between them and nought where they touch.
        /// </remarks>
        public GeoLine3 GetShortestLineTo(GeoRay3 ray, Tolerance tolerance) => Projection3.GetShortestLineTo(ray, this, tolerance).Reverse();

        /// <summary>
        /// Gets the shortest segment joining this triangle to a solid, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// The segment leaves this triangle and lands on the solid, so its length is the clearance
        /// between them and nought where they touch.
        /// </remarks>
        public GeoLine3 GetShortestLineTo(GeoSolid3 solid) => Projection3.GetShortestLineTo(solid, this).Reverse();

        /// <summary>
        /// Gets the shortest segment joining this triangle to a solid, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The segment leaves this triangle and lands on the solid, so its length is the clearance
        /// between them and nought where they touch.
        /// </remarks>
        public GeoLine3 GetShortestLineTo(GeoSolid3 solid, Tolerance tolerance) => Projection3.GetShortestLineTo(solid, this, tolerance).Reverse();
    }
}
