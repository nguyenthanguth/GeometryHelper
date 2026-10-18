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
        /// A shape coming end-on at the other and stopping within the point tolerance of it is read as crossing it, so
        /// the line can be of no length where the distance is the gap: the segment from (5, 5, 0.0004) to (5, 5, 9),
        /// over the square 100 by 100 in the floor, is joined by a segment of no length at (5, 5, 0.0004), and is
        /// 0.0004 from it by its distance.
        /// </remarks>
        public GeoLine3 GetShortestLineTo(GeoTriangle3 triangle) => Projection3.GetShortestLineTo(this, triangle);

        /// <summary>
        /// Gets the shortest segment joining this one to a triangle, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The segment leaves this segment and lands on the triangle, so its length is the clearance
        /// between them and nought where they touch.
        /// A shape coming end-on at the other and stopping within the point tolerance of it is read as crossing it, so
        /// the line can be of no length where the distance is the gap: the segment from (5, 5, 0.0004) to (5, 5, 9),
        /// over the square 100 by 100 in the floor, is joined by a segment of no length at (5, 5, 0.0004), and is
        /// 0.0004 from it by its distance.
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
        /// A shape coming end-on at the other and stopping within the point tolerance of it is read as crossing it, so
        /// the line can be of no length where the distance is the gap: the segment from (5, 5, 0.0004) to (5, 5, 9),
        /// over the square 100 by 100 in the floor, is joined by a segment of no length at (5, 5, 0.0004), and is
        /// 0.0004 from it by its distance.
        /// </remarks>
        public GeoLine3 GetShortestLineTo(GeoSolid3 solid) => Projection3.GetShortestLineTo(solid, this).Reverse();

        /// <summary>
        /// Gets the shortest segment joining this one to a solid, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The segment leaves this segment and lands on the solid, so its length is the clearance
        /// between them and nought where they touch.
        /// A shape coming end-on at the other and stopping within the point tolerance of it is read as crossing it, so
        /// the line can be of no length where the distance is the gap: the segment from (5, 5, 0.0004) to (5, 5, 9),
        /// over the square 100 by 100 in the floor, is joined by a segment of no length at (5, 5, 0.0004), and is
        /// 0.0004 from it by its distance.
        /// </remarks>
        public GeoLine3 GetShortestLineTo(GeoSolid3 solid, Tolerance tolerance) => Projection3.GetShortestLineTo(solid, this, tolerance).Reverse();

        /// <summary>
        /// Gets the shortest line from this segment to the boundary of a curved loop in the same plane:
        /// <see cref="GeoPolygonArc3.GetShortestLineTo(GeoLine3)"/> turned round, so that it leaves this segment and
        /// lands on the loop.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        /// <exception cref="ArgumentException">Thrown when this segment does not lie in the loop's plane.</exception>
        public GeoLine3 GetShortestLineTo(GeoPolygonArc3 loop)
            => (loop ?? throw new ArgumentNullException(nameof(loop))).GetShortestLineTo(this).Reverse();

        /// <summary>
        /// Gets the shortest line from this segment to the boundary of a curved loop in the same plane, within a
        /// tolerance: <see cref="GeoPolygonArc3.GetShortestLineTo(GeoLine3, Tolerance)"/> turned round, so that it
        /// leaves this segment and lands on the loop.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        /// <exception cref="ArgumentException">Thrown when this segment does not lie in the loop's plane.</exception>
        public GeoLine3 GetShortestLineTo(GeoPolygonArc3 loop, Tolerance tolerance)
            => (loop ?? throw new ArgumentNullException(nameof(loop))).GetShortestLineTo(this, tolerance).Reverse();

        /// <summary>
        /// Gets the shortest segment joining this segment to a point, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// The segment leaves this segment and ends on the point, so it is as long as the distance between them: from
        /// the segment joining (0, 0, 0) and (100, 0, 0), the point (50, 30, 40) gets the one from (50, 0, 0), 50 long.
        /// </remarks>
        public GeoLine3 GetShortestLineTo(GeoPoint3 point) => Projection3.GetShortestLineTo(this, point);

        /// <summary>
        /// Gets the shortest segment joining this segment to a point, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The segment leaves this segment and ends on the point, so it is as long as the distance between them: from
        /// the segment joining (0, 0, 0) and (100, 0, 0), the point (50, 30, 40) gets the one from (50, 0, 0), 50 long.
        /// </remarks>
        public GeoLine3 GetShortestLineTo(GeoPoint3 point, Tolerance tolerance)
            => Projection3.GetShortestLineTo(this, point, tolerance);

        /// <summary>
        /// Gets the shortest segment joining this segment to a plane, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// A segment that crosses or touches the plane meets it in a segment of no length, at its start where that lies
        /// on the plane and otherwise where it passes through; one clear of it is joined from its nearer end, the start
        /// where both are as near. The segment from (0, 0, 10) to (50, 0, 30) is joined to the XY plane from its start
        /// to the origin, 10 long, as <see cref="DistanceTo(GeoPlane3)"/> measures it.
        /// </remarks>
        public GeoLine3 GetShortestLineTo(GeoPlane3 plane) => Projection3.GetShortestLineTo(this, plane);

        /// <summary>
        /// Gets the shortest segment joining this segment to a plane, within a tolerance.
        /// </summary>
        /// <remarks>
        /// A segment that crosses or touches the plane meets it in a segment of no length, at its start where that lies
        /// on the plane and otherwise where it passes through; one clear of it is joined from its nearer end, the start
        /// where both are as near. The segment from (0, 0, 10) to (50, 0, 30) is joined to the XY plane from its start
        /// to the origin, 10 long, as <see cref="DistanceTo(GeoPlane3)"/> measures it. The tolerance is taken so that
        /// this reads like every other shape; it changes nothing, since the distance reads a segment against a plane
        /// exactly.
        /// </remarks>
        public GeoLine3 GetShortestLineTo(GeoPlane3 plane, Tolerance tolerance)
            => Projection3.GetShortestLineTo(this, plane, tolerance);

        /// <summary>
        /// Gets the shortest segment joining this segment to a polygon, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// The polygon is read as a filled surface, as its distance reads it: a segment through it meets it in a
        /// segment of no length at the crossing, and one ending above it is joined straight down. Over the square 100
        /// by 100 on the XY plane with a corner at the origin, the segment from (20, 20, 30) to (80, 80, 60) is joined
        /// from its start straight down, 30 long, as <see cref="DistanceTo(GeoPolygon3)"/> measures it.
        /// A shape coming end-on at the other and stopping within the point tolerance of it is read as crossing it, so
        /// the line can be of no length where the distance is the gap: the segment from (5, 5, 0.0004) to (5, 5, 9),
        /// over the square 100 by 100 in the floor, is joined by a segment of no length at (5, 5, 0.0004), and is
        /// 0.0004 from it by its distance.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public GeoLine3 GetShortestLineTo(GeoPolygon3 polygon) => Projection3.GetShortestLineTo(this, polygon);

        /// <summary>
        /// Gets the shortest segment joining this segment to a polygon, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The polygon is read as a filled surface, as its distance reads it: a segment through it meets it in a
        /// segment of no length at the crossing, and one ending above it is joined straight down. Over the square 100
        /// by 100 on the XY plane with a corner at the origin, the segment from (20, 20, 30) to (80, 80, 60) is joined
        /// from its start straight down, 30 long, as <see cref="DistanceTo(GeoPolygon3)"/> measures it.
        /// A shape coming end-on at the other and stopping within the point tolerance of it is read as crossing it, so
        /// the line can be of no length where the distance is the gap: the segment from (5, 5, 0.0004) to (5, 5, 9),
        /// over the square 100 by 100 in the floor, is joined by a segment of no length at (5, 5, 0.0004), and is
        /// 0.0004 from it by its distance.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public GeoLine3 GetShortestLineTo(GeoPolygon3 polygon, Tolerance tolerance)
            => Projection3.GetShortestLineTo(this, polygon, tolerance);

        /// <summary>
        /// Gets the shortest segment joining this segment to a polyline:
        /// <see cref="GeoPolyline3.GetShortestLineTo(GeoLine3)"/> turned round, so that it leaves this segment and
        /// lands on the polyline.
        /// </summary>
        /// <remarks>
        /// The segment leaves this segment and lands on the chain, of no length where they meet, and a tie goes to the
        /// first edge of the chain. The segment from (60, 50, 30) to (60, 50, 80) is joined to the chain (0, 0, 0),
        /// (100, 0, 0), (100, 100, 0) from its start to (100, 50, 0), 50 long, as
        /// <see cref="DistanceTo(GeoPolyline3)"/> measures it.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the polyline is null.</exception>
        public GeoLine3 GetShortestLineTo(GeoPolyline3 polyline)
            => Projection3.GetShortestLineTo(polyline, this).Reverse();

        /// <summary>
        /// Gets the shortest segment joining this segment to a polyline, within a tolerance:
        /// <see cref="GeoPolyline3.GetShortestLineTo(GeoLine3, Tolerance)"/> turned round, so that it leaves this
        /// segment and lands on the polyline.
        /// </summary>
        /// <remarks>
        /// The segment leaves this segment and lands on the chain, of no length where they meet, and a tie goes to the
        /// first edge of the chain. The segment from (60, 50, 30) to (60, 50, 80) is joined to the chain (0, 0, 0),
        /// (100, 0, 0), (100, 100, 0) from its start to (100, 50, 0), 50 long, as
        /// <see cref="DistanceTo(GeoPolyline3)"/> measures it.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the polyline is null.</exception>
        public GeoLine3 GetShortestLineTo(GeoPolyline3 polyline, Tolerance tolerance)
            => Projection3.GetShortestLineTo(polyline, this, tolerance).Reverse();

        /// <summary>
        /// Gets the shortest segment joining this segment to a face, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// The face is read by its material: a segment through it meets it in a segment of no length at the crossing, and
        /// one through a hole is joined to the rim. Over the square 100 by 100 in the floor with a corner at the origin and
        /// a hole 40 by 40 in its middle, the segment from (45, 50, -10) to (45, 50, 10) is joined from (45, 50, 0) to the
        /// rim at (30, 50, 0), 15 long, as <see cref="DistanceTo(GeoFace3)"/> measures it, and the segment from (20, 20,
        /// -10) to (20, 20, 10) meets the material at (20, 20, 0).
        /// A shape coming end-on at the other and stopping within the point tolerance of it is read as crossing it, so
        /// the line can be of no length where the distance is the gap: the segment from (5, 5, 0.0004) to (5, 5, 9),
        /// over the square 100 by 100 in the floor, is joined by a segment of no length at (5, 5, 0.0004), and is
        /// 0.0004 from it by its distance.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public GeoLine3 GetShortestLineTo(GeoFace3 face) => Projection3.GetShortestLineTo(this, face);

        /// <summary>
        /// Gets the shortest segment joining this segment to a face, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The face is read by its material: a segment through it meets it in a segment of no length at the crossing, and
        /// one through a hole is joined to the rim. Over the square 100 by 100 in the floor with a corner at the origin and
        /// a hole 40 by 40 in its middle, the segment from (45, 50, -10) to (45, 50, 10) is joined from (45, 50, 0) to the
        /// rim at (30, 50, 0), 15 long, as <see cref="DistanceTo(GeoFace3)"/> measures it, and the segment from (20, 20,
        /// -10) to (20, 20, 10) meets the material at (20, 20, 0).
        /// A shape coming end-on at the other and stopping within the point tolerance of it is read as crossing it, so
        /// the line can be of no length where the distance is the gap: the segment from (5, 5, 0.0004) to (5, 5, 9),
        /// over the square 100 by 100 in the floor, is joined by a segment of no length at (5, 5, 0.0004), and is
        /// 0.0004 from it by its distance.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public GeoLine3 GetShortestLineTo(GeoFace3 face, Tolerance tolerance) => Projection3.GetShortestLineTo(this, face, tolerance);
    }
}
