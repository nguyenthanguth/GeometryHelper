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
        /// A shape coming end-on at the other and stopping within the point tolerance of it is read as crossing it, so
        /// the line can be of no length where the distance is the gap: the segment from (5, 5, 0.0004) to (5, 5, 9),
        /// over the square 100 by 100 in the floor, is joined by a segment of no length at (5, 5, 0.0004), and is
        /// 0.0004 from it by its distance.
        /// </remarks>
        public GeoLine3 GetShortestLineTo(GeoTriangle3 other) => Projection3.GetShortestLineTo(this, other);

        /// <summary>
        /// Gets the shortest segment joining this triangle to another, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The segment leaves this triangle and lands on the other triangle, so its length is the clearance
        /// between them and nought where they touch.
        /// A shape coming end-on at the other and stopping within the point tolerance of it is read as crossing it, so
        /// the line can be of no length where the distance is the gap: the segment from (5, 5, 0.0004) to (5, 5, 9),
        /// over the square 100 by 100 in the floor, is joined by a segment of no length at (5, 5, 0.0004), and is
        /// 0.0004 from it by its distance.
        /// </remarks>
        public GeoLine3 GetShortestLineTo(GeoTriangle3 other, Tolerance tolerance) => Projection3.GetShortestLineTo(this, other, tolerance);

        /// <summary>
        /// Gets the shortest segment joining this triangle to a segment, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// The segment leaves this triangle and lands on the segment, so its length is the clearance
        /// between them and nought where they touch.
        /// A shape coming end-on at the other and stopping within the point tolerance of it is read as crossing it, so
        /// the line can be of no length where the distance is the gap: the segment from (5, 5, 0.0004) to (5, 5, 9),
        /// over the square 100 by 100 in the floor, is joined by a segment of no length at (5, 5, 0.0004), and is
        /// 0.0004 from it by its distance.
        /// </remarks>
        public GeoLine3 GetShortestLineTo(GeoLine3 line) => Projection3.GetShortestLineTo(line, this).Reverse();

        /// <summary>
        /// Gets the shortest segment joining this triangle to a segment, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The segment leaves this triangle and lands on the segment, so its length is the clearance
        /// between them and nought where they touch.
        /// A shape coming end-on at the other and stopping within the point tolerance of it is read as crossing it, so
        /// the line can be of no length where the distance is the gap: the segment from (5, 5, 0.0004) to (5, 5, 9),
        /// over the square 100 by 100 in the floor, is joined by a segment of no length at (5, 5, 0.0004), and is
        /// 0.0004 from it by its distance.
        /// </remarks>
        public GeoLine3 GetShortestLineTo(GeoLine3 line, Tolerance tolerance) => Projection3.GetShortestLineTo(line, this, tolerance).Reverse();

        /// <summary>
        /// Gets the shortest segment joining this triangle to a ray, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// The segment leaves this triangle and lands on the ray, so its length is the clearance
        /// between them and nought where they touch.
        /// A shape coming end-on at the other and stopping within the point tolerance of it is read as crossing it, so
        /// the line can be of no length where the distance is the gap: the segment from (5, 5, 0.0004) to (5, 5, 9),
        /// over the square 100 by 100 in the floor, is joined by a segment of no length at (5, 5, 0.0004), and is
        /// 0.0004 from it by its distance.
        /// </remarks>
        public GeoLine3 GetShortestLineTo(GeoRay3 ray) => Projection3.GetShortestLineTo(ray, this).Reverse();

        /// <summary>
        /// Gets the shortest segment joining this triangle to a ray, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The segment leaves this triangle and lands on the ray, so its length is the clearance
        /// between them and nought where they touch.
        /// A shape coming end-on at the other and stopping within the point tolerance of it is read as crossing it, so
        /// the line can be of no length where the distance is the gap: the segment from (5, 5, 0.0004) to (5, 5, 9),
        /// over the square 100 by 100 in the floor, is joined by a segment of no length at (5, 5, 0.0004), and is
        /// 0.0004 from it by its distance.
        /// </remarks>
        public GeoLine3 GetShortestLineTo(GeoRay3 ray, Tolerance tolerance) => Projection3.GetShortestLineTo(ray, this, tolerance).Reverse();

        /// <summary>
        /// Gets the shortest segment joining this triangle to a solid, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// The segment leaves this triangle and lands on the solid, so its length is the clearance
        /// between them and nought where they touch.
        /// A shape coming end-on at the other and stopping within the point tolerance of it is read as crossing it, so
        /// the line can be of no length where the distance is the gap: the segment from (5, 5, 0.0004) to (5, 5, 9),
        /// over the square 100 by 100 in the floor, is joined by a segment of no length at (5, 5, 0.0004), and is
        /// 0.0004 from it by its distance.
        /// </remarks>
        public GeoLine3 GetShortestLineTo(GeoSolid3 solid) => Projection3.GetShortestLineTo(solid, this).Reverse();

        /// <summary>
        /// Gets the shortest segment joining this triangle to a solid, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The segment leaves this triangle and lands on the solid, so its length is the clearance
        /// between them and nought where they touch.
        /// A shape coming end-on at the other and stopping within the point tolerance of it is read as crossing it, so
        /// the line can be of no length where the distance is the gap: the segment from (5, 5, 0.0004) to (5, 5, 9),
        /// over the square 100 by 100 in the floor, is joined by a segment of no length at (5, 5, 0.0004), and is
        /// 0.0004 from it by its distance.
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
        /// A shape coming end-on at the other and stopping within the point tolerance of it is read as crossing it, so
        /// the line can be of no length where the distance is the gap: the segment from (5, 5, 0.0004) to (5, 5, 9),
        /// over the square 100 by 100 in the floor, is joined by a segment of no length at (5, 5, 0.0004), and is
        /// 0.0004 from it by its distance.
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
        /// A shape coming end-on at the other and stopping within the point tolerance of it is read as crossing it, so
        /// the line can be of no length where the distance is the gap: the segment from (5, 5, 0.0004) to (5, 5, 9),
        /// over the square 100 by 100 in the floor, is joined by a segment of no length at (5, 5, 0.0004), and is
        /// 0.0004 from it by its distance.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the polyline is null.</exception>
        public GeoLine3 GetShortestLineTo(GeoPolyline3 polyline, Tolerance tolerance)
            => Projection3.GetShortestLineTo(polyline, this, tolerance).Reverse();

        /// <summary>
        /// Gets the shortest segment joining this triangle to a face, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// The face is read by its material; the edges of this triangle are walked first, and a tie goes to the first. The
        /// triangle with corners (110, 50, 0), (200, 50, 0) and (110, 50, 100), standing beside the square 100 by 100 in
        /// the floor with a corner at the origin and a hole 40 by 40 in its middle, is joined from its corner (110, 50, 0)
        /// to (100, 50, 0) on the outline, 10 long, as <see cref="DistanceTo(GeoFace3)"/> measures it.
        /// A shape coming end-on at the other and stopping within the point tolerance of it is read as crossing it, so
        /// the line can be of no length where the distance is the gap: the segment from (5, 5, 0.0004) to (5, 5, 9),
        /// over the square 100 by 100 in the floor, is joined by a segment of no length at (5, 5, 0.0004), and is
        /// 0.0004 from it by its distance.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public GeoLine3 GetShortestLineTo(GeoFace3 face) => Projection3.GetShortestLineTo(this, face);

        /// <summary>
        /// Gets the shortest segment joining this triangle to a face, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The face is read by its material; the edges of this triangle are walked first, and a tie goes to the first. The
        /// triangle with corners (110, 50, 0), (200, 50, 0) and (110, 50, 100), standing beside the square 100 by 100 in
        /// the floor with a corner at the origin and a hole 40 by 40 in its middle, is joined from its corner (110, 50, 0)
        /// to (100, 50, 0) on the outline, 10 long, as <see cref="DistanceTo(GeoFace3)"/> measures it.
        /// A shape coming end-on at the other and stopping within the point tolerance of it is read as crossing it, so
        /// the line can be of no length where the distance is the gap: the segment from (5, 5, 0.0004) to (5, 5, 9),
        /// over the square 100 by 100 in the floor, is joined by a segment of no length at (5, 5, 0.0004), and is
        /// 0.0004 from it by its distance.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public GeoLine3 GetShortestLineTo(GeoFace3 face, Tolerance tolerance) => Projection3.GetShortestLineTo(this, face, tolerance);

        /// <summary>
        /// Gets the shortest segment joining this triangle to a polygon, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// Both are read as filled surfaces; the edges of this triangle are walked first, and a tie goes to the first. The
        /// triangle with corners (0, 0, 0), (100, 0, 0) and (0, 100, 0) and the square 100 by 100 standing upright along y
        /// = 50 from z = 10 to 110 are joined from (50, 50, 0) straight up, 10 long, as
        /// <see cref="DistanceTo(GeoPolygon3)"/> measures it; standing from z = -50, the square crosses the triangle and
        /// the segment has no length at (50, 50, 0).
        /// A shape coming end-on at the other and stopping within the point tolerance of it is read as crossing it, so
        /// the line can be of no length where the distance is the gap: the segment from (5, 5, 0.0004) to (5, 5, 9),
        /// over the square 100 by 100 in the floor, is joined by a segment of no length at (5, 5, 0.0004), and is
        /// 0.0004 from it by its distance.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public GeoLine3 GetShortestLineTo(GeoPolygon3 polygon) => Projection3.GetShortestLineTo(this, polygon);

        /// <summary>
        /// Gets the shortest segment joining this triangle to a polygon, within a tolerance.
        /// </summary>
        /// <remarks>
        /// Both are read as filled surfaces; the edges of this triangle are walked first, and a tie goes to the first. The
        /// triangle with corners (0, 0, 0), (100, 0, 0) and (0, 100, 0) and the square 100 by 100 standing upright along y
        /// = 50 from z = 10 to 110 are joined from (50, 50, 0) straight up, 10 long, as
        /// <see cref="DistanceTo(GeoPolygon3)"/> measures it; standing from z = -50, the square crosses the triangle and
        /// the segment has no length at (50, 50, 0).
        /// A shape coming end-on at the other and stopping within the point tolerance of it is read as crossing it, so
        /// the line can be of no length where the distance is the gap: the segment from (5, 5, 0.0004) to (5, 5, 9),
        /// over the square 100 by 100 in the floor, is joined by a segment of no length at (5, 5, 0.0004), and is
        /// 0.0004 from it by its distance.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public GeoLine3 GetShortestLineTo(GeoPolygon3 polygon, Tolerance tolerance) => Projection3.GetShortestLineTo(this, polygon, tolerance);

        /// <summary>
        /// Gets the shortest segment joining this triangle to a plane, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// A triangle that crosses or touches the plane meets it in a segment of no length at the first point of contact
        /// walking its edges from A to B; one clear of it is joined from its nearest corner, the first of them where
        /// several are equally near. The triangle with corners (0, 50, -50), (100, 50, -50) and (0, 50, 50) meets the floor
        /// plane at (50, 50, 0); the triangle with corners (0, 50, 10), (100, 50, 10) and (0, 50, 110) is joined from (0,
        /// 50, 10) straight down, 10 long, as <see cref="DistanceTo(GeoPlane3)"/> measures it. The segment is never
        /// snapped: a triangle a hair off the plane is joined by a segment that short, though the distance, read within the
        /// point tolerance, is nought.
        /// </remarks>
        public GeoLine3 GetShortestLineTo(GeoPlane3 plane) => Projection3.GetShortestLineTo(this, plane);

        /// <summary>
        /// Gets the shortest segment joining this triangle to a plane, within a tolerance.
        /// </summary>
        /// <remarks>
        /// A triangle that crosses or touches the plane meets it in a segment of no length at the first point of contact
        /// walking its edges from A to B; one clear of it is joined from its nearest corner, the first of them where
        /// several are equally near. The triangle with corners (0, 50, -50), (100, 50, -50) and (0, 50, 50) meets the floor
        /// plane at (50, 50, 0); the triangle with corners (0, 50, 10), (100, 50, 10) and (0, 50, 110) is joined from (0,
        /// 50, 10) straight down, 10 long, as <see cref="DistanceTo(GeoPlane3)"/> measures it. The segment is never
        /// snapped: a triangle a hair off the plane is joined by a segment that short, though the distance, read within the
        /// point tolerance, is nought.
        /// </remarks>
        public GeoLine3 GetShortestLineTo(GeoPlane3 plane, Tolerance tolerance) => Projection3.GetShortestLineTo(this, plane, tolerance);
    }
}
