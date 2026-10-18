using System;
using System.Collections.Generic;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// The point of a polygon nearest another shape, the shortest segment joining the two, and which
    /// edge of it faces one.
    /// </summary>
    public sealed partial class GeoPolygon3
    {
        /// <summary>
        /// Gets the point on this polygon closest to a target point.
        /// </summary>
        public GeoPoint3 GetClosestPointOnBoundary(GeoPoint3 point) => Projection3.ProjectToPolygon(this, point);

        /// <summary>
        /// Gets the shortest line from this polygon to the boundary of a curved loop in the same plane:
        /// <see cref="GeoPolygonArc3.GetShortestLineTo(GeoPolygon3)"/> turned round, so that it leaves this polygon and
        /// lands on the loop.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        /// <exception cref="ArgumentException">Thrown when this polygon does not lie in the loop's plane.</exception>
        public GeoLine3 GetShortestLineTo(GeoPolygonArc3 loop)
            => (loop ?? throw new ArgumentNullException(nameof(loop))).GetShortestLineTo(this).Reverse();

        /// <summary>
        /// Gets the shortest line from this polygon to the boundary of a curved loop in the same plane, within a
        /// tolerance: <see cref="GeoPolygonArc3.GetShortestLineTo(GeoPolygon3, Tolerance)"/> turned round, so that it
        /// leaves this polygon and lands on the loop.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        /// <exception cref="ArgumentException">Thrown when this polygon does not lie in the loop's plane.</exception>
        public GeoLine3 GetShortestLineTo(GeoPolygonArc3 loop, Tolerance tolerance)
            => (loop ?? throw new ArgumentNullException(nameof(loop))).GetShortestLineTo(this, tolerance).Reverse();

        /// <summary>
        /// Gets the shortest segment joining this polygon to a point, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// The polygon is read as a filled surface, as its distance reads it, so a point above it is joined straight
        /// down and the segment is as long as <see cref="DistanceTo(GeoPoint3)"/>: from the square 100 by 100 on the XY
        /// plane with a corner at the origin, the point (130, 50, 40) gets the one from (100, 50, 0), 50 long.
        /// </remarks>
        public GeoLine3 GetShortestLineTo(GeoPoint3 point) => Projection3.GetShortestLineTo(this, point);

        /// <summary>
        /// Gets the shortest segment joining this polygon to a point, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The polygon is read as a filled surface, as its distance reads it, so a point above it is joined straight
        /// down and the segment is as long as <see cref="DistanceTo(GeoPoint3)"/>: from the square 100 by 100 on the XY
        /// plane with a corner at the origin, the point (130, 50, 40) gets the one from (100, 50, 0), 50 long.
        /// </remarks>
        public GeoLine3 GetShortestLineTo(GeoPoint3 point, Tolerance tolerance)
            => Projection3.GetShortestLineTo(this, point, tolerance);

        /// <summary>
        /// Gets the shortest segment joining this polygon to a segment:
        /// <see cref="GeoLine3.GetShortestLineTo(GeoPolygon3)"/> turned round, so that it leaves this polygon and lands
        /// on the segment.
        /// </summary>
        /// <remarks>
        /// This polygon is read as a filled surface, as its distance reads it. The square 100 by 100 on the XY plane
        /// with a corner at the origin is joined to the segment from (20, 20, 30) to (80, 80, 60) from (20, 20, 0)
        /// straight up to its start, 30 long, as <see cref="DistanceTo(GeoLine3)"/> measures it.
        /// A shape coming end-on at the other and stopping within the point tolerance of it is read as crossing it, so
        /// the line can be of no length where the distance is the gap: the segment from (5, 5, 0.0004) to (5, 5, 9),
        /// over the square 100 by 100 in the floor, is joined by a segment of no length at (5, 5, 0.0004), and is
        /// 0.0004 from it by its distance.
        /// </remarks>
        public GeoLine3 GetShortestLineTo(GeoLine3 line) => Projection3.GetShortestLineTo(line, this).Reverse();

        /// <summary>
        /// Gets the shortest segment joining this polygon to a segment, within a tolerance:
        /// <see cref="GeoLine3.GetShortestLineTo(GeoPolygon3, Tolerance)"/> turned round, so that it leaves this
        /// polygon and lands on the segment.
        /// </summary>
        /// <remarks>
        /// This polygon is read as a filled surface, as its distance reads it. The square 100 by 100 on the XY plane
        /// with a corner at the origin is joined to the segment from (20, 20, 30) to (80, 80, 60) from (20, 20, 0)
        /// straight up to its start, 30 long, as <see cref="DistanceTo(GeoLine3)"/> measures it.
        /// A shape coming end-on at the other and stopping within the point tolerance of it is read as crossing it, so
        /// the line can be of no length where the distance is the gap: the segment from (5, 5, 0.0004) to (5, 5, 9),
        /// over the square 100 by 100 in the floor, is joined by a segment of no length at (5, 5, 0.0004), and is
        /// 0.0004 from it by its distance.
        /// </remarks>
        public GeoLine3 GetShortestLineTo(GeoLine3 line, Tolerance tolerance)
            => Projection3.GetShortestLineTo(line, this, tolerance).Reverse();

        /// <summary>
        /// Gets the shortest segment joining this polygon to a polyline:
        /// <see cref="GeoPolyline3.GetShortestLineTo(GeoPolygon3)"/> turned round, so that it leaves this polygon and
        /// lands on the polyline.
        /// </summary>
        /// <remarks>
        /// This polygon is read as a filled surface, as its distance reads it. The square 100 by 100 on the XY plane
        /// with a corner at the origin is joined to the chain (50, 50, 30), (50, 50, 80), (150, 50, 80) from (50, 50,
        /// 0) straight up, 30 long, as <see cref="DistanceTo(GeoPolyline3)"/> measures it.
        /// A shape coming end-on at the other and stopping within the point tolerance of it is read as crossing it, so
        /// the line can be of no length where the distance is the gap: the segment from (5, 5, 0.0004) to (5, 5, 9),
        /// over the square 100 by 100 in the floor, is joined by a segment of no length at (5, 5, 0.0004), and is
        /// 0.0004 from it by its distance.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the polyline is null.</exception>
        public GeoLine3 GetShortestLineTo(GeoPolyline3 polyline)
            => Projection3.GetShortestLineTo(polyline, this).Reverse();

        /// <summary>
        /// Gets the shortest segment joining this polygon to a polyline, within a tolerance:
        /// <see cref="GeoPolyline3.GetShortestLineTo(GeoPolygon3, Tolerance)"/> turned round, so that it leaves this
        /// polygon and lands on the polyline.
        /// </summary>
        /// <remarks>
        /// This polygon is read as a filled surface, as its distance reads it. The square 100 by 100 on the XY plane
        /// with a corner at the origin is joined to the chain (50, 50, 30), (50, 50, 80), (150, 50, 80) from (50, 50,
        /// 0) straight up, 30 long, as <see cref="DistanceTo(GeoPolyline3)"/> measures it.
        /// A shape coming end-on at the other and stopping within the point tolerance of it is read as crossing it, so
        /// the line can be of no length where the distance is the gap: the segment from (5, 5, 0.0004) to (5, 5, 9),
        /// over the square 100 by 100 in the floor, is joined by a segment of no length at (5, 5, 0.0004), and is
        /// 0.0004 from it by its distance.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the polyline is null.</exception>
        public GeoLine3 GetShortestLineTo(GeoPolyline3 polyline, Tolerance tolerance)
            => Projection3.GetShortestLineTo(polyline, this, tolerance).Reverse();

        /// <summary>
        /// Gets the shortest segment joining this polygon to a solid:
        /// <see cref="GeoSolid3.GetShortestLineTo(GeoPolygon3)"/> turned round, so that it leaves this polygon and
        /// lands on the solid.
        /// </summary>
        /// <remarks>
        /// The segment runs from this polygon to the surface of the body, so a polygon lying wholly inside is joined
        /// out to the nearest face, where <see cref="DistanceTo(GeoSolid3)"/> answers nought; outside, it is as long as
        /// that distance. The square 20 by 20 lying flat at a height of 30 in the box 100 by 100 by 100 with a corner
        /// at the origin is joined down to the bottom face, 30 long.
        /// A shape coming end-on at the other and stopping within the point tolerance of it is read as crossing it, so
        /// the line can be of no length where the distance is the gap: the segment from (5, 5, 0.0004) to (5, 5, 9),
        /// over the square 100 by 100 in the floor, is joined by a segment of no length at (5, 5, 0.0004), and is
        /// 0.0004 from it by its distance.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the solid is null.</exception>
        public GeoLine3 GetShortestLineTo(GeoSolid3 solid) => Projection3.GetShortestLineTo(solid, this).Reverse();

        /// <summary>
        /// Gets the shortest segment joining this polygon to a solid, within a tolerance:
        /// <see cref="GeoSolid3.GetShortestLineTo(GeoPolygon3, Tolerance)"/> turned round, so that it leaves this
        /// polygon and lands on the solid.
        /// </summary>
        /// <remarks>
        /// The segment runs from this polygon to the surface of the body, so a polygon lying wholly inside is joined
        /// out to the nearest face, where <see cref="DistanceTo(GeoSolid3)"/> answers nought; outside, it is as long as
        /// that distance. The square 20 by 20 lying flat at a height of 30 in the box 100 by 100 by 100 with a corner
        /// at the origin is joined down to the bottom face, 30 long.
        /// A shape coming end-on at the other and stopping within the point tolerance of it is read as crossing it, so
        /// the line can be of no length where the distance is the gap: the segment from (5, 5, 0.0004) to (5, 5, 9),
        /// over the square 100 by 100 in the floor, is joined by a segment of no length at (5, 5, 0.0004), and is
        /// 0.0004 from it by its distance.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the solid is null.</exception>
        public GeoLine3 GetShortestLineTo(GeoSolid3 solid, Tolerance tolerance)
            => Projection3.GetShortestLineTo(solid, this, tolerance).Reverse();

        /// <summary>
        /// Gets the shortest segment joining this polygon to a face: <see cref="GeoFace3.GetShortestLineTo(GeoPolygon3)"/>
        /// turned round, so that it leaves this polygon and lands on the face.
        /// </summary>
        /// <remarks>
        /// A square 20 by 20 standing upright in the middle of the hole of the square 100 by 100 in the floor with a corner
        /// at the origin and a hole 40 by 40 in its middle, along y = 50 from x = 40 to 60, is joined from (60, 50, 0) to
        /// the rim at (70, 50, 0), 10 long, as <see cref="DistanceTo(GeoFace3)"/> measures it.
        /// A shape coming end-on at the other and stopping within the point tolerance of it is read as crossing it, so
        /// the line can be of no length where the distance is the gap: the segment from (5, 5, 0.0004) to (5, 5, 9),
        /// over the square 100 by 100 in the floor, is joined by a segment of no length at (5, 5, 0.0004), and is
        /// 0.0004 from it by its distance.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public GeoLine3 GetShortestLineTo(GeoFace3 face) => Projection3.GetShortestLineTo(face, this).Reverse();

        /// <summary>
        /// Gets the shortest segment joining this polygon to a face, within a tolerance:
        /// <see cref="GeoFace3.GetShortestLineTo(GeoPolygon3, Tolerance)"/> turned round, so that it leaves this polygon
        /// and lands on the face.
        /// </summary>
        /// <remarks>
        /// A square 20 by 20 standing upright in the middle of the hole of the square 100 by 100 in the floor with a corner
        /// at the origin and a hole 40 by 40 in its middle, along y = 50 from x = 40 to 60, is joined from (60, 50, 0) to
        /// the rim at (70, 50, 0), 10 long, as <see cref="DistanceTo(GeoFace3)"/> measures it.
        /// A shape coming end-on at the other and stopping within the point tolerance of it is read as crossing it, so
        /// the line can be of no length where the distance is the gap: the segment from (5, 5, 0.0004) to (5, 5, 9),
        /// over the square 100 by 100 in the floor, is joined by a segment of no length at (5, 5, 0.0004), and is
        /// 0.0004 from it by its distance.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public GeoLine3 GetShortestLineTo(GeoFace3 face, Tolerance tolerance) => Projection3.GetShortestLineTo(face, this, tolerance).Reverse();

        /// <summary>
        /// Gets the shortest segment joining this polygon to another one, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// Both are read as filled surfaces; the edges of this polygon are walked first, and a tie goes to the first. A
        /// square 100 by 100 in the floor with a corner at the origin and one standing upright along y = 50 from z = 10 to
        /// 110 are joined from (100, 50, 0) to (100, 50, 10), 10 long, as <see cref="DistanceTo(GeoPolygon3)"/> measures
        /// it; standing from z = -50, the second crosses the first and the segment has no length at (100, 50, 0). The
        /// segment is never snapped: one lying 0.0004 above the first is joined by a segment 0.0004 long, as long as its
        /// distance, which is exact there too.
        /// A shape coming end-on at the other and stopping within the point tolerance of it is read as crossing it, so
        /// the line can be of no length where the distance is the gap: the segment from (5, 5, 0.0004) to (5, 5, 9),
        /// over the square 100 by 100 in the floor, is joined by a segment of no length at (5, 5, 0.0004), and is
        /// 0.0004 from it by its distance.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public GeoLine3 GetShortestLineTo(GeoPolygon3 other) => Projection3.GetShortestLineTo(this, other);

        /// <summary>
        /// Gets the shortest segment joining this polygon to another one, within a tolerance.
        /// </summary>
        /// <remarks>
        /// Both are read as filled surfaces; the edges of this polygon are walked first, and a tie goes to the first. A
        /// square 100 by 100 in the floor with a corner at the origin and one standing upright along y = 50 from z = 10 to
        /// 110 are joined from (100, 50, 0) to (100, 50, 10), 10 long, as <see cref="DistanceTo(GeoPolygon3)"/> measures
        /// it; standing from z = -50, the second crosses the first and the segment has no length at (100, 50, 0). The
        /// segment is never snapped: one lying 0.0004 above the first is joined by a segment 0.0004 long, as long as its
        /// distance, which is exact there too.
        /// A shape coming end-on at the other and stopping within the point tolerance of it is read as crossing it, so
        /// the line can be of no length where the distance is the gap: the segment from (5, 5, 0.0004) to (5, 5, 9),
        /// over the square 100 by 100 in the floor, is joined by a segment of no length at (5, 5, 0.0004), and is
        /// 0.0004 from it by its distance.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public GeoLine3 GetShortestLineTo(GeoPolygon3 other, Tolerance tolerance) => Projection3.GetShortestLineTo(this, other, tolerance);

        /// <summary>
        /// Gets the shortest segment joining this polygon to a ray: <see cref="GeoRay3.GetShortestLineTo(GeoPolygon3)"/>
        /// turned round, so that it leaves this polygon and lands on the ray.
        /// </summary>
        /// <remarks>
        /// This polygon is read as a filled surface. The square 100 by 100 in the floor with a corner at the origin is
        /// joined to the ray from (130, 50, 40) along X from (100, 50, 0) on its edge to the origin of the ray, 50 long, as
        /// <see cref="DistanceTo(GeoRay3)"/> measures it.
        /// A shape coming end-on at the other and stopping within the point tolerance of it is read as crossing it, so
        /// the line can be of no length where the distance is the gap: the segment from (5, 5, 0.0004) to (5, 5, 9),
        /// over the square 100 by 100 in the floor, is joined by a segment of no length at (5, 5, 0.0004), and is
        /// 0.0004 from it by its distance.
        /// </remarks>
        public GeoLine3 GetShortestLineTo(GeoRay3 ray) => Projection3.GetShortestLineTo(ray, this).Reverse();

        /// <summary>
        /// Gets the shortest segment joining this polygon to a ray, within a tolerance:
        /// <see cref="GeoRay3.GetShortestLineTo(GeoPolygon3, Tolerance)"/> turned round, so that it leaves this polygon and
        /// lands on the ray.
        /// </summary>
        /// <remarks>
        /// This polygon is read as a filled surface. The square 100 by 100 in the floor with a corner at the origin is
        /// joined to the ray from (130, 50, 40) along X from (100, 50, 0) on its edge to the origin of the ray, 50 long, as
        /// <see cref="DistanceTo(GeoRay3)"/> measures it.
        /// A shape coming end-on at the other and stopping within the point tolerance of it is read as crossing it, so
        /// the line can be of no length where the distance is the gap: the segment from (5, 5, 0.0004) to (5, 5, 9),
        /// over the square 100 by 100 in the floor, is joined by a segment of no length at (5, 5, 0.0004), and is
        /// 0.0004 from it by its distance.
        /// </remarks>
        public GeoLine3 GetShortestLineTo(GeoRay3 ray, Tolerance tolerance) => Projection3.GetShortestLineTo(ray, this, tolerance).Reverse();

        /// <summary>
        /// Gets the shortest segment joining this polygon to a triangle:
        /// <see cref="GeoTriangle3.GetShortestLineTo(GeoPolygon3)"/> turned round, so that it leaves this polygon and lands
        /// on the triangle.
        /// </summary>
        /// <remarks>
        /// Both are read as filled surfaces. The square 100 by 100 standing upright along y = 50 from z = 10 to 110 is
        /// joined to the triangle with corners (0, 0, 0), (100, 0, 0) and (0, 100, 0) from (50, 50, 10) straight down to
        /// (50, 50, 0), 10 long, as <see cref="DistanceTo(GeoTriangle3)"/> measures it.
        /// A shape coming end-on at the other and stopping within the point tolerance of it is read as crossing it, so
        /// the line can be of no length where the distance is the gap: the segment from (5, 5, 0.0004) to (5, 5, 9),
        /// over the square 100 by 100 in the floor, is joined by a segment of no length at (5, 5, 0.0004), and is
        /// 0.0004 from it by its distance.
        /// </remarks>
        public GeoLine3 GetShortestLineTo(GeoTriangle3 triangle) => Projection3.GetShortestLineTo(triangle, this).Reverse();

        /// <summary>
        /// Gets the shortest segment joining this polygon to a triangle, within a tolerance:
        /// <see cref="GeoTriangle3.GetShortestLineTo(GeoPolygon3, Tolerance)"/> turned round, so that it leaves this
        /// polygon and lands on the triangle.
        /// </summary>
        /// <remarks>
        /// Both are read as filled surfaces. The square 100 by 100 standing upright along y = 50 from z = 10 to 110 is
        /// joined to the triangle with corners (0, 0, 0), (100, 0, 0) and (0, 100, 0) from (50, 50, 10) straight down to
        /// (50, 50, 0), 10 long, as <see cref="DistanceTo(GeoTriangle3)"/> measures it.
        /// A shape coming end-on at the other and stopping within the point tolerance of it is read as crossing it, so
        /// the line can be of no length where the distance is the gap: the segment from (5, 5, 0.0004) to (5, 5, 9),
        /// over the square 100 by 100 in the floor, is joined by a segment of no length at (5, 5, 0.0004), and is
        /// 0.0004 from it by its distance.
        /// </remarks>
        public GeoLine3 GetShortestLineTo(GeoTriangle3 triangle, Tolerance tolerance)
            => Projection3.GetShortestLineTo(triangle, this, tolerance).Reverse();

        /// <summary>
        /// Gets the shortest segment joining this polygon to a plane, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// A polygon that crosses or touches the plane meets it in a segment of no length at the first point of contact
        /// walking its edges in order; one clear of it is joined from its nearest corner, the first of them where several
        /// are equally near. A square 100 by 100 standing upright along y = 50 from z = -50 to 50 meets the floor plane at
        /// (100, 50, 0); standing from z = 10 to 110, it is joined from its corner (0, 50, 10) straight down, 10 long, as
        /// <see cref="DistanceTo(GeoPlane3)"/> measures it. The segment is never snapped: a polygon a hair off the plane is
        /// joined by a segment that short, though the distance, read within the point tolerance, is nought.
        /// </remarks>
        public GeoLine3 GetShortestLineTo(GeoPlane3 plane) => Projection3.GetShortestLineTo(this, plane);

        /// <summary>
        /// Gets the shortest segment joining this polygon to a plane, within a tolerance.
        /// </summary>
        /// <remarks>
        /// A polygon that crosses or touches the plane meets it in a segment of no length at the first point of contact
        /// walking its edges in order; one clear of it is joined from its nearest corner, the first of them where several
        /// are equally near. A square 100 by 100 standing upright along y = 50 from z = -50 to 50 meets the floor plane at
        /// (100, 50, 0); standing from z = 10 to 110, it is joined from its corner (0, 50, 10) straight down, 10 long, as
        /// <see cref="DistanceTo(GeoPlane3)"/> measures it. The segment is never snapped: a polygon a hair off the plane is
        /// joined by a segment that short, though the distance, read within the point tolerance, is nought.
        /// </remarks>
        public GeoLine3 GetShortestLineTo(GeoPlane3 plane, Tolerance tolerance) => Projection3.GetShortestLineTo(this, plane, tolerance);
    }
}
