using System;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// The point of a point nearest another shape, the shortest segment joining the two, and which
    /// edge of it faces one.
    /// </summary>
    public readonly partial struct GeoPoint3
    {
        /// <summary>
        /// Gets the shortest segment joining this point to a solid, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// The segment leaves this point and lands on the solid, so its length is the clearance
        /// between them and nought where they touch.
        /// </remarks>
        public GeoLine3 GetShortestLineTo(GeoSolid3 solid) => Projection3.GetShortestLineTo(solid, this).Reverse();

        /// <summary>
        /// Gets the shortest segment joining this point to a solid, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The segment leaves this point and lands on the solid, so its length is the clearance
        /// between them and nought where they touch.
        /// </remarks>
        public GeoLine3 GetShortestLineTo(GeoSolid3 solid, Tolerance tolerance) => Projection3.GetShortestLineTo(solid, this, tolerance).Reverse();

        /// <summary>
        /// Gets the closest point on a line segment to this point, clamped to its endpoints.
        /// </summary>
        public GeoPoint3 GetClosestPointOnBoundary(GeoLine3 line) => Projection3.ProjectToLine(line, this);

        /// <summary>
        /// Gets the closest point on a ray to this point, clamped to its origin.
        /// </summary>
        public GeoPoint3 GetClosestPointOnBoundary(GeoRay3 ray) => Projection3.ProjectToRay(ray, this);

        /// <summary>
        /// Gets the closest point on a plane to this point.
        /// </summary>
        public GeoPoint3 GetClosestPointOnBoundary(GeoPlane3 plane) => Projection3.ProjectToPlane(plane, this);

        /// <summary>
        /// Gets the closest point on a triangle to this point.
        /// </summary>
        public GeoPoint3 GetClosestPointOnBoundary(GeoTriangle3 triangle) => Projection3.ProjectToTriangle(triangle, this);

        /// <summary>
        /// Gets the closest point on a polyline to this point.
        /// </summary>
        public GeoPoint3 GetClosestPointOnBoundary(GeoPolyline3 polyline) => Projection3.ProjectToPolyline(polyline, this);

        /// <summary>
        /// Gets the closest point on a polygon to this point, read as a filled surface.
        /// </summary>
        public GeoPoint3 GetClosestPointOnBoundary(GeoPolygon3 polygon) => Projection3.ProjectToPolygon(polygon, this);

        /// <summary>
        /// Gets the closest point on the circumference of a circle to this point.
        /// </summary>
        public GeoPoint3 GetClosestPointOnBoundary(GeoCircle3 circle) => Projection3.ProjectToCircle(circle, this);

        /// <summary>
        /// Gets the closest point of an oriented box to this point.
        /// </summary>
        public GeoPoint3 GetClosestPointOnBoundary(GeoObb3 box) => Projection3.ProjectToObb(box, this);

        /// <summary>
        /// Gets the closest point of an axis-aligned box to this point.
        /// </summary>
        public GeoPoint3 GetClosestPointOnBoundary(GeoAabb3 box) => box.GetClosestPointOnBoundary(this);

        /// <summary>
        /// Gets the closest point on the surface of a solid to this point.
        /// </summary>
        public GeoPoint3 GetClosestPointOnBoundary(GeoSolid3 solid) => Projection3.ProjectToSolid(solid, this);
    }
}
