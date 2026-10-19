using System;
using GeometryHelper;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// The point of an infinite line nearest a point, and the shortest segment joining a line to another shape in space.
    /// </summary>
    /// <remarks>
    /// Both ends of a shortest segment sit on a boundary, and it leaves this line. A line runs on both ways for ever, so one
    /// through a closed shape crosses its surface, and the segment is the point where it does: the line through (50, 50, 10)
    /// along X, in the box 100 by 100 by 20 with a corner at the origin, gets the segment of no length at (0, 50, 10) or
    /// (100, 50, 10). The curved shapes are not measured, as they are not for a ray or a segment (see
    /// <see cref="GeoXLine3.DistanceTo(GeoPoint3)"/>), and the boxes go through <c>ToSolid()</c>.
    /// </remarks>
    public readonly partial struct GeoXLine3
    {
        /// <summary>
        /// Gets the closest point on this line to a target point: the foot of the perpendicular.
        /// </summary>
        /// <remarks>
        /// From the line through the origin along X, the points (50, 40, 30) and (-30, 40, 30) land at (50, 0, 0) and
        /// (-30, 0, 0): unlike a ray, a line has no start to stop at.
        /// </remarks>
        public GeoPoint3 GetClosestPointOnBoundary(GeoPoint3 point) => XLine3.Project(this, point);

        /// <summary>
        /// Gets the shortest segment leaving this infinite line and landing on a point, using the default tolerance.
        /// </summary>
        public GeoLine3 GetShortestLineTo(GeoPoint3 point) => GetShortestLineTo(point, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving this infinite line and landing on a point, within a tolerance.
        /// </summary>
        /// <remarks>
        /// It starts where <see cref="GetClosestPointOnBoundary(GeoPoint3)"/> lands and is exactly as long as
        /// <see cref="DistanceTo(GeoPoint3)"/>: from the line through the origin along X to the point (-30, 40, 30), it runs
        /// from (-30, 0, 0) and is 50 long.
        /// </remarks>
        public GeoLine3 GetShortestLineTo(GeoPoint3 point, Tolerance tolerance) => Projection3.GetShortestLineTo(this, point, tolerance);

        /// <summary>
        /// Gets the shortest segment leaving this infinite line and landing on a line segment, using the default tolerance.
        /// </summary>
        public GeoLine3 GetShortestLineTo(GeoLine3 line) => GetShortestLineTo(line, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving this infinite line and landing on a line segment, within a tolerance.
        /// </summary>
        public GeoLine3 GetShortestLineTo(GeoLine3 line, Tolerance tolerance) => Projection3.GetShortestLineTo(this, line, tolerance);

        /// <summary>
        /// Gets the shortest segment leaving this infinite line and landing on a ray, using the default tolerance.
        /// </summary>
        public GeoLine3 GetShortestLineTo(GeoRay3 ray) => GetShortestLineTo(ray, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving this infinite line and landing on a ray, within a tolerance.
        /// </summary>
        /// <remarks>
        /// Where the ray reaches the common perpendicular of the two lines it is that; otherwise it runs from the line to the
        /// origin of the ray. The line through the origin along X gets the segment from (50, 0, 0) to (50, 30, 40) to the ray
        /// from (50, 30, 40) along Y, 50 long, and the segment from (50, 0, 0) to (50, 0, 40) to the ray from (50, 30, 40)
        /// along -Y.
        /// </remarks>
        public GeoLine3 GetShortestLineTo(GeoRay3 ray, Tolerance tolerance) => Projection3.GetShortestLineTo(this, ray, tolerance);

        /// <summary>
        /// Gets the shortest segment leaving this infinite line and landing on another infinite line, using the default tolerance.
        /// </summary>
        public GeoLine3 GetShortestLineTo(GeoXLine3 other) => GetShortestLineTo(other, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving this infinite line and landing on another infinite line, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The common perpendicular of the two lines, a segment of no length where they cross. Asked either way round it
        /// is the same segment turned round, to the bit. The line through the origin along X gets the segment from
        /// (0, 0, 0) to (0, 0, 30) to the line through (0, 0, 30) along Y.
        /// </remarks>
        public GeoLine3 GetShortestLineTo(GeoXLine3 other, Tolerance tolerance) => Projection3.GetShortestLineTo(this, other, tolerance);

        /// <summary>
        /// Gets the shortest segment leaving this infinite line and landing on a plane, using the default tolerance.
        /// </summary>
        public GeoLine3 GetShortestLineTo(GeoPlane3 plane) => GetShortestLineTo(plane, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving this infinite line and landing on a plane, within a tolerance.
        /// </summary>
        /// <remarks>
        /// A segment of no length where the line crosses the plane; otherwise the perpendicular from the origin of the line.
        /// The line through (10, 20, 7) along X is joined to the floor from (10, 20, 7) straight down. A line starting within
        /// the planar tolerance of the plane is joined by that perpendicular too, short as it is, where
        /// <see cref="DistanceTo(GeoPlane3)"/> reads nought, as it is for a ray.
        /// </remarks>
        public GeoLine3 GetShortestLineTo(GeoPlane3 plane, Tolerance tolerance) => Projection3.GetShortestLineTo(this, plane, tolerance);

        /// <summary>
        /// Gets the shortest segment leaving this infinite line and landing on a triangle, using the default tolerance.
        /// </summary>
        public GeoLine3 GetShortestLineTo(GeoTriangle3 triangle) => GetShortestLineTo(triangle, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving this infinite line and landing on a triangle, within a tolerance.
        /// </summary>
        public GeoLine3 GetShortestLineTo(GeoTriangle3 triangle, Tolerance tolerance) => Projection3.GetShortestLineTo(this, triangle, tolerance);

        /// <summary>
        /// Gets the shortest segment leaving this infinite line and landing on a polygon, using the default tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public GeoLine3 GetShortestLineTo(GeoPolygon3 polygon) => GetShortestLineTo(polygon, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving this infinite line and landing on a polygon, within a tolerance.
        /// </summary>
        /// <remarks>
        /// A line piercing the polygon gets the segment of no length where it does; a line beside it gets the segment from the
        /// line to the nearest point of the polygon.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public GeoLine3 GetShortestLineTo(GeoPolygon3 polygon, Tolerance tolerance) => Projection3.GetShortestLineTo(this, polygon, tolerance);

        /// <summary>
        /// Gets the shortest segment leaving this infinite line and landing on a face, using the default tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public GeoLine3 GetShortestLineTo(GeoFace3 face) => GetShortestLineTo(face, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving this infinite line and landing on a face, within a tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public GeoLine3 GetShortestLineTo(GeoFace3 face, Tolerance tolerance) => Projection3.GetShortestLineTo(this, face, tolerance);

        /// <summary>
        /// Gets the shortest segment leaving this infinite line and landing on a solid, using the default tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the solid is null.</exception>
        public GeoLine3 GetShortestLineTo(GeoSolid3 solid) => GetShortestLineTo(solid, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving this infinite line and landing on a solid, within a tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the solid is null.</exception>
        public GeoLine3 GetShortestLineTo(GeoSolid3 solid, Tolerance tolerance) => Projection3.GetShortestLineTo(this, solid, tolerance);

        /// <summary>
        /// Gets the shortest segment leaving this infinite line and landing on a polyline, using the default tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the polyline is null.</exception>
        public GeoLine3 GetShortestLineTo(GeoPolyline3 polyline) => GetShortestLineTo(polyline, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment leaving this infinite line and landing on a polyline, within a tolerance.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the polyline is null.</exception>
        public GeoLine3 GetShortestLineTo(GeoPolyline3 polyline, Tolerance tolerance) => Projection3.GetShortestLineTo(this, polyline, tolerance);
    }
}
