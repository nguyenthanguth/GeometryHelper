using System;
using System.Collections.Generic;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// The point of a solid nearest another shape, the shortest segment joining the two, and which
    /// edge of it faces one.
    /// </summary>
    public sealed partial class GeoSolid3
    {
        /// <summary>
        /// Gets the point on the surface of this solid closest to a target point.
        /// </summary>
        /// <remarks>
        /// The answer is always on the surface, including for a point inside the body, where
        /// <see cref="DistanceTo(GeoPoint3)"/> reports nothing at all. The two deliberately disagree there,
        /// the same way they do in the plane.
        /// </remarks>
        public GeoPoint3 GetClosestPointOnBoundary(GeoPoint3 point) => Projection3.ProjectToSolid(this, point);

        /// <summary>
        /// Gets the point on the surface of this solid closest to a target point, within a tolerance.
        /// </summary>
        public GeoPoint3 GetClosestPointOnBoundary(GeoPoint3 point, Tolerance tolerance) => Projection3.ProjectToSolid(this, point, tolerance);

        /// <summary>
        /// Gets the shortest segment joining this solid to a point.
        /// </summary>
        /// <remarks>
        /// The segment leaves the surface, so it has a length even for a point inside the body, where
        /// <see cref="DistanceTo(GeoPoint3)"/> reports nothing at all.
        /// </remarks>
        public GeoLine3 GetShortestLineTo(GeoPoint3 point) => Projection3.GetShortestLineTo(this, point);

        /// <summary>
        /// Gets the shortest segment joining this solid to a point, within a tolerance.
        /// </summary>
        public GeoLine3 GetShortestLineTo(GeoPoint3 point, Tolerance tolerance) => Projection3.GetShortestLineTo(this, point, tolerance);

        /// <summary>
        /// Gets the shortest segment joining this solid to a line segment.
        /// </summary>
        public GeoLine3 GetShortestLineTo(GeoLine3 line) => Projection3.GetShortestLineTo(this, line);

        /// <summary>
        /// Gets the shortest segment joining this solid to a line segment, within a tolerance.
        /// </summary>
        public GeoLine3 GetShortestLineTo(GeoLine3 line, Tolerance tolerance) => Projection3.GetShortestLineTo(this, line, tolerance);

        /// <summary>
        /// Gets the shortest segment joining this solid to a triangle.
        /// </summary>
        public GeoLine3 GetShortestLineTo(GeoTriangle3 triangle) => Projection3.GetShortestLineTo(this, triangle);

        /// <summary>
        /// Gets the shortest segment joining this solid to a triangle, within a tolerance.
        /// </summary>
        public GeoLine3 GetShortestLineTo(GeoTriangle3 triangle, Tolerance tolerance) => Projection3.GetShortestLineTo(this, triangle, tolerance);

        /// <summary>
        /// Gets the shortest segment joining this solid to another solid.
        /// </summary>
        public GeoLine3 GetShortestLineTo(GeoSolid3 other) => Projection3.GetShortestLineTo(this, other);

        /// <summary>
        /// Gets the shortest segment joining this solid to another solid, within a tolerance.
        /// </summary>
        public GeoLine3 GetShortestLineTo(GeoSolid3 other, Tolerance tolerance) => Projection3.GetShortestLineTo(this, other, tolerance);

        /// <summary>
        /// Gets the shortest segment joining this solid to a ray.
        /// </summary>
        public GeoLine3 GetShortestLineTo(GeoRay3 ray) => Projection3.GetShortestLineTo(this, ray);

        /// <summary>
        /// Gets the shortest segment joining this solid to a ray, within a tolerance.
        /// </summary>
        public GeoLine3 GetShortestLineTo(GeoRay3 ray, Tolerance tolerance) => Projection3.GetShortestLineTo(this, ray, tolerance);
    }
}
