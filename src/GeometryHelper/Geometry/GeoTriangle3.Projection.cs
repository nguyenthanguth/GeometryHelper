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

        /// <summary>
        /// Gets the shortest segment joining this triangle to a point, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// The triangle is read as a filled surface, as its distance reads it, so the segment is as long as
        /// <see cref="DistanceTo(GeoPoint3)"/>: from the triangle (0, 0, 0), (100, 0, 0), (0, 100, 0), the point (20,
        /// 20, 30) gets the one straight down from (20, 20, 0), 30 long.
        /// </remarks>
        public GeoLine3 GetShortestLineTo(GeoPoint3 point) => Projection3.GetShortestLineTo(this, point);

        /// <summary>
        /// Gets the shortest segment joining this triangle to a point, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The triangle is read as a filled surface, as its distance reads it, so the segment is as long as
        /// <see cref="DistanceTo(GeoPoint3)"/>: from the triangle (0, 0, 0), (100, 0, 0), (0, 100, 0), the point (20,
        /// 20, 30) gets the one straight down from (20, 20, 0), 30 long. The tolerance is taken so that this reads like
        /// every other shape; it changes nothing here.
        /// </remarks>
        public GeoLine3 GetShortestLineTo(GeoPoint3 point, Tolerance tolerance)
            => Projection3.GetShortestLineTo(this, point, tolerance);

        /// <summary>
        /// Gets the shortest segment joining this triangle to a polyline:
        /// <see cref="GeoPolyline3.GetShortestLineTo(GeoTriangle3)"/> turned round, so that it leaves this triangle and
        /// lands on the polyline.
        /// </summary>
        /// <remarks>
        /// The segment leaves this triangle, read as a filled surface, and lands on the chain, of no length where the
        /// chain passes through. The triangle (0, 0, 0), (100, 0, 0), (0, 100, 0) is joined to the chain (20, 20, 30),
        /// (20, 20, 80) from (20, 20, 0) straight up, 30 long, as <see cref="DistanceTo(GeoPolyline3)"/> measures it.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the polyline is null.</exception>
        public GeoLine3 GetShortestLineTo(GeoPolyline3 polyline)
            => Projection3.GetShortestLineTo(polyline, this).Reverse();

        /// <summary>
        /// Gets the shortest segment joining this triangle to a polyline, within a tolerance:
        /// <see cref="GeoPolyline3.GetShortestLineTo(GeoTriangle3, Tolerance)"/> turned round, so that it leaves this
        /// triangle and lands on the polyline.
        /// </summary>
        /// <remarks>
        /// The segment leaves this triangle, read as a filled surface, and lands on the chain, of no length where the
        /// chain passes through. The triangle (0, 0, 0), (100, 0, 0), (0, 100, 0) is joined to the chain (20, 20, 30),
        /// (20, 20, 80) from (20, 20, 0) straight up, 30 long, as <see cref="DistanceTo(GeoPolyline3)"/> measures it.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the polyline is null.</exception>
        public GeoLine3 GetShortestLineTo(GeoPolyline3 polyline, Tolerance tolerance)
            => Projection3.GetShortestLineTo(polyline, this, tolerance).Reverse();
    }
}
