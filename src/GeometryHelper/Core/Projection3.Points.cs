using System;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    /// <summary>
    /// The shortest segment joining a shape in space to a point.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Each segment leaves the point of the shape that the matching <c>ProjectTo...</c> gives and ends on the point,
    /// and <see cref="Distance3"/> measures the distance from that same nearest point, so the segment is exactly as
    /// long as the distance.
    /// </para>
    /// <para>
    /// A flat region is read as its distance reads it: a triangle, a polygon, a face and a disc are filled, so a point
    /// above one goes straight down to it where its foot lands on the material, and a point lying on it gets a segment
    /// of no length. Only a solid differs, since its segment runs to the surface even from inside; see
    /// <see cref="GetShortestLineTo(GeoSolid3, GeoPoint3, Tolerance)"/>.
    /// </para>
    /// </remarks>
    public static partial class Projection3
    {
        /// <summary>
        /// Gets the shortest segment joining a line segment to a point.
        /// </summary>
        public static GeoLine3 GetShortestLineTo(GeoLine3 line, GeoPoint3 point) => GetShortestLineTo(line, point, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment joining a line segment to a point, within a tolerance.
        /// </summary>
        /// <param name="line">The segment the answer leaves.</param>
        /// <param name="point">The point the answer ends on.</param>
        /// <param name="tolerance">The tolerance a segment of no length is told by.</param>
        /// <returns>
        /// A segment from the point <see cref="ProjectToLine(GeoLine3, GeoPoint3, Tolerance)"/> gives to
        /// <paramref name="point"/>, as long as <see cref="Distance3.DistanceTo(GeoLine3, GeoPoint3, Tolerance)"/>. From
        /// the segment joining (0, 0, 0) and (100, 0, 0), the point (50, 30, 40) gets the segment from (50, 0, 0), 50
        /// long, and the point (130, 0, 40) the one from the end (100, 0, 0), also 50 long.
        /// </returns>
        public static GeoLine3 GetShortestLineTo(GeoLine3 line, GeoPoint3 point, Tolerance tolerance) => new GeoLine3(ProjectToLine(line, point, tolerance), point);

        /// <summary>
        /// Gets the shortest segment joining a ray to a point.
        /// </summary>
        public static GeoLine3 GetShortestLineTo(GeoRay3 ray, GeoPoint3 point) => GetShortestLineTo(ray, point, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment joining a ray to a point.
        /// </summary>
        /// <param name="ray">The ray the answer leaves.</param>
        /// <param name="point">The point the answer ends on.</param>
        /// <param name="tolerance">The tolerance; not read, since <see cref="ProjectToRay(GeoRay3, GeoPoint3)"/> takes none.</param>
        /// <returns>
        /// A segment from the point <see cref="ProjectToRay(GeoRay3, GeoPoint3)"/> gives to <paramref name="point"/>, as
        /// long as <see cref="Distance3.DistanceTo(GeoRay3, GeoPoint3)"/>. From the ray leaving the origin along X, the
        /// point (50, 30, 40) gets the segment from (50, 0, 0), 50 long, and the point (-30, 0, 40) the one from the origin,
        /// also 50 long.
        /// </returns>
        public static GeoLine3 GetShortestLineTo(GeoRay3 ray, GeoPoint3 point, Tolerance tolerance) => new GeoLine3(ProjectToRay(ray, point), point);

        /// <summary>
        /// Gets the shortest segment joining a plane to a point.
        /// </summary>
        public static GeoLine3 GetShortestLineTo(GeoPlane3 plane, GeoPoint3 point) => GetShortestLineTo(plane, point, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment joining a plane to a point.
        /// </summary>
        /// <param name="plane">The plane the answer leaves.</param>
        /// <param name="point">The point the answer ends on.</param>
        /// <param name="tolerance">The tolerance; not read, since <see cref="ProjectToPlane(GeoPlane3, GeoPoint3)"/> takes none.</param>
        /// <returns>
        /// The perpendicular from the foot of <paramref name="point"/> on the plane, as long as
        /// <see cref="Distance3.DistanceTo(GeoPlane3, GeoPoint3)"/>: the point (10, 20, 30) gets the segment from
        /// (10, 20, 0) on the XY plane, 30 long.
        /// </returns>
        public static GeoLine3 GetShortestLineTo(GeoPlane3 plane, GeoPoint3 point, Tolerance tolerance) => new GeoLine3(ProjectToPlane(plane, point), point);

        /// <summary>
        /// Gets the shortest segment joining a triangle to a point.
        /// </summary>
        public static GeoLine3 GetShortestLineTo(GeoTriangle3 triangle, GeoPoint3 point) => GetShortestLineTo(triangle, point, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment joining a triangle to a point.
        /// </summary>
        /// <param name="triangle">The triangle the answer leaves, read as a filled surface.</param>
        /// <param name="point">The point the answer ends on.</param>
        /// <param name="tolerance">The tolerance; not read, since <see cref="ProjectToTriangle(GeoTriangle3, GeoPoint3)"/> takes none.</param>
        /// <returns>
        /// A segment from the point <see cref="ProjectToTriangle(GeoTriangle3, GeoPoint3)"/> gives to
        /// <paramref name="point"/>, as long as <see cref="Distance3.DistanceTo(GeoTriangle3, GeoPoint3)"/>. From the
        /// triangle (0, 0, 0), (100, 0, 0), (0, 100, 0), the point (20, 20, 30) gets the segment straight down from
        /// (20, 20, 0), 30 long, and the point (-30, 50, 40) the one from the edge at (0, 50, 0), 50 long.
        /// </returns>
        public static GeoLine3 GetShortestLineTo(GeoTriangle3 triangle, GeoPoint3 point, Tolerance tolerance) => new GeoLine3(ProjectToTriangle(triangle, point), point);

        /// <summary>
        /// Gets the shortest segment joining a polyline to a point.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the polyline is null.</exception>
        public static GeoLine3 GetShortestLineTo(GeoPolyline3 polyline, GeoPoint3 point) => GetShortestLineTo(polyline, point, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment joining a polyline to a point, within a tolerance.
        /// </summary>
        /// <param name="polyline">The chain the answer leaves, a curve.</param>
        /// <param name="point">The point the answer ends on.</param>
        /// <param name="tolerance">The tolerance a segment of no length is told by.</param>
        /// <returns>
        /// A segment from the point of the chain nearest <paramref name="point"/>, as long as
        /// <see cref="Distance3.DistanceTo(GeoPolyline3, GeoPoint3, Tolerance)"/>, each edge measured as it is however short,
        /// as <see cref="ProjectToPolyline(GeoPolyline3, GeoPoint3, Tolerance)"/> measures it. Where two edges are equally
        /// near, the first of them along the chain is taken. From
        /// the chain (0, 0, 0), (100, 0, 0), (100, 100, 0), the point (50, 30, 40) gets the segment from (50, 0, 0), 50 long.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when the polyline is null.</exception>
        public static GeoLine3 GetShortestLineTo(GeoPolyline3 polyline, GeoPoint3 point, Tolerance tolerance) => new GeoLine3(ProjectToPolyline(polyline, point, Exactly), point);

        /// <summary>
        /// Gets the shortest segment joining a polygon to a point.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public static GeoLine3 GetShortestLineTo(GeoPolygon3 polygon, GeoPoint3 point) => GetShortestLineTo(polygon, point, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment joining a polygon to a point, within a tolerance.
        /// </summary>
        /// <param name="polygon">The polygon the answer leaves, read as a filled surface.</param>
        /// <param name="point">The point the answer ends on.</param>
        /// <param name="tolerance">The tolerance the foot of the point is placed with.</param>
        /// <returns>
        /// A segment from the point <see cref="ProjectToPolygon(GeoPolygon3, GeoPoint3, Tolerance)"/> gives to
        /// <paramref name="point"/>, as long as <see cref="Distance3.DistanceTo(GeoPolygon3, GeoPoint3, Tolerance)"/>.
        /// From the square 100 by 100 on the XY plane with a corner at the origin, the point (50, 50, 30) gets the segment
        /// straight down from (50, 50, 0), 30 long, and the point (130, 50, 40) the one from the edge at (100, 50, 0),
        /// 50 long. The foot is taken inside with the tolerance, so the segment may leave up to the point tolerance past
        /// the outline: the point (100.0005, 50, 30) gets the segment from (100.0005, 50, 0), 30 long, where the edge is
        /// 30.000000004 away. A point landing beyond the outline is carried onto an edge as the edge is, however short.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public static GeoLine3 GetShortestLineTo(GeoPolygon3 polygon, GeoPoint3 point, Tolerance tolerance) => new GeoLine3(ProjectToPolygon(polygon, point, tolerance, Exactly), point);

        /// <summary>
        /// Gets the shortest segment joining a face to a point.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public static GeoLine3 GetShortestLineTo(GeoFace3 face, GeoPoint3 point) => GetShortestLineTo(face, point, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment joining a face to a point, within a tolerance.
        /// </summary>
        /// <param name="face">The face the answer leaves: its outline, less its holes.</param>
        /// <param name="point">The point the answer ends on.</param>
        /// <param name="tolerance">The tolerance the foot of the point is placed with.</param>
        /// <returns>
        /// A segment from the point <see cref="ProjectToFace(GeoFace3, GeoPoint3, Tolerance)"/> gives to
        /// <paramref name="point"/>, as long as <see cref="Distance3.DistanceTo(GeoFace3, GeoPoint3, Tolerance)"/>.
        /// From the square 100 by 100 on the XY plane with a corner at the origin and a hole 20 by 20 in its middle, the
        /// point (20, 50, 30) gets the segment straight down from (20, 50, 0), 30 long, and the point (45, 50, 30), over
        /// the hole, the one from its rim at (40, 50, 0), 5 across and 30 down, about 30.41 long. The foot is taken on
        /// the material with the tolerance, so the segment may leave up to the point tolerance past a rim: the point
        /// (40.0005, 50, 30) gets the segment from (40.0005, 50, 0), 0.0005 into the hole and 30 long, where the rim is
        /// 30.000000004 away. A point landing off the material is carried onto a side of a rim as the side is, however short.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public static GeoLine3 GetShortestLineTo(GeoFace3 face, GeoPoint3 point, Tolerance tolerance) => new GeoLine3(ProjectToFace(face, point, tolerance, Exactly), point);

        /// <summary>
        /// Gets the shortest segment joining a circular disc to a point.
        /// </summary>
        public static GeoLine3 GetShortestLineTo(GeoCircle3 circle, GeoPoint3 point) => GetShortestLineTo(circle, point, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment joining a circular disc to a point, within a tolerance.
        /// </summary>
        /// <param name="circle">The circle the answer leaves, read as the filled disc its distance reads.</param>
        /// <param name="point">The point the answer ends on.</param>
        /// <param name="tolerance">The tolerance a point on the axis is told by.</param>
        /// <returns>
        /// A segment from the point <see cref="ProjectToDisc(GeoCircle3, GeoPoint3, Tolerance)"/> gives to
        /// <paramref name="point"/>, as long as <see cref="Distance3.DistanceTo(GeoCircle3, GeoPoint3, Tolerance)"/>.
        /// From the circle of radius 50 about the origin on the XY plane, the point (0, 0, 30) gets the segment straight
        /// down from the centre, 30 long, and the point (80, 0, 40) the one from the rim at (50, 0, 0), 50 long.
        /// </returns>
        public static GeoLine3 GetShortestLineTo(GeoCircle3 circle, GeoPoint3 point, Tolerance tolerance) => new GeoLine3(ProjectToDisc(circle, point, tolerance), point);
    }
}
