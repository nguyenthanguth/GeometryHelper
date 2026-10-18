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
        /// A shape coming end-on at the other and stopping within the point tolerance of it is read as crossing it, so
        /// the line can be of no length where the distance is the gap: the segment from (5, 5, 0.0004) to (5, 5, 9),
        /// over the square 100 by 100 in the floor, is joined by a segment of no length at (5, 5, 0.0004), and is
        /// 0.0004 from it by its distance.
        /// </remarks>
        public GeoLine3 GetShortestLineTo(GeoTriangle3 triangle) => Projection3.GetShortestLineTo(this, triangle);

        /// <summary>
        /// Gets the shortest segment joining this ray to a triangle, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The segment leaves this ray and lands on the triangle, so its length is the clearance
        /// between them and nought where they touch.
        /// A shape coming end-on at the other and stopping within the point tolerance of it is read as crossing it, so
        /// the line can be of no length where the distance is the gap: the segment from (5, 5, 0.0004) to (5, 5, 9),
        /// over the square 100 by 100 in the floor, is joined by a segment of no length at (5, 5, 0.0004), and is
        /// 0.0004 from it by its distance.
        /// </remarks>
        public GeoLine3 GetShortestLineTo(GeoTriangle3 triangle, Tolerance tolerance) => Projection3.GetShortestLineTo(this, triangle, tolerance);

        /// <summary>
        /// Gets the shortest segment joining this ray to a solid, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// The segment leaves this ray and lands on the solid, so its length is the clearance
        /// between them and nought where they touch.
        /// A shape coming end-on at the other and stopping within the point tolerance of it is read as crossing it, so
        /// the line can be of no length where the distance is the gap: the segment from (5, 5, 0.0004) to (5, 5, 9),
        /// over the square 100 by 100 in the floor, is joined by a segment of no length at (5, 5, 0.0004), and is
        /// 0.0004 from it by its distance.
        /// </remarks>
        public GeoLine3 GetShortestLineTo(GeoSolid3 solid) => Projection3.GetShortestLineTo(solid, this).Reverse();

        /// <summary>
        /// Gets the shortest segment joining this ray to a solid, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The segment leaves this ray and lands on the solid, so its length is the clearance
        /// between them and nought where they touch.
        /// A shape coming end-on at the other and stopping within the point tolerance of it is read as crossing it, so
        /// the line can be of no length where the distance is the gap: the segment from (5, 5, 0.0004) to (5, 5, 9),
        /// over the square 100 by 100 in the floor, is joined by a segment of no length at (5, 5, 0.0004), and is
        /// 0.0004 from it by its distance.
        /// </remarks>
        public GeoLine3 GetShortestLineTo(GeoSolid3 solid, Tolerance tolerance) => Projection3.GetShortestLineTo(solid, this, tolerance).Reverse();

        /// <summary>
        /// Gets the shortest segment joining this ray to a point, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// The segment leaves this ray and ends on the point, so it is as long as <see cref="DistanceTo(GeoPoint3)"/>:
        /// from the ray leaving the origin along X, the point (-30, 0, 40) gets the one from the origin, 50 long.
        /// </remarks>
        public GeoLine3 GetShortestLineTo(GeoPoint3 point) => Projection3.GetShortestLineTo(this, point);

        /// <summary>
        /// Gets the shortest segment joining this ray to a point, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The segment leaves this ray and ends on the point, so it is as long as <see cref="DistanceTo(GeoPoint3)"/>:
        /// from the ray leaving the origin along X, the point (-30, 0, 40) gets the one from the origin, 50 long. The
        /// tolerance is taken so that this reads like every other shape; it changes nothing here.
        /// </remarks>
        public GeoLine3 GetShortestLineTo(GeoPoint3 point, Tolerance tolerance)
            => Projection3.GetShortestLineTo(this, point, tolerance);

        /// <summary>
        /// Gets the shortest segment joining this ray to a face, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// The face is read by its material: a ray running into it meets it in a segment of no length at the crossing, and
        /// one through a hole is joined to the rim. Over the square 100 by 100 in the floor with a corner at the origin and
        /// a hole 40 by 40 in its middle, the ray from (45, 50, 10) pointing straight down is joined from (45, 50, 0) to
        /// the rim at (30, 50, 0), 15 long, as <see cref="DistanceTo(GeoFace3)"/> measures it; from (20, 20, 10) it meets
        /// the material at (20, 20, 0).
        /// A shape coming end-on at the other and stopping within the point tolerance of it is read as crossing it, so
        /// the line can be of no length where the distance is the gap: the segment from (5, 5, 0.0004) to (5, 5, 9),
        /// over the square 100 by 100 in the floor, is joined by a segment of no length at (5, 5, 0.0004), and is
        /// 0.0004 from it by its distance.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public GeoLine3 GetShortestLineTo(GeoFace3 face) => Projection3.GetShortestLineTo(this, face);

        /// <summary>
        /// Gets the shortest segment joining this ray to a face, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The face is read by its material: a ray running into it meets it in a segment of no length at the crossing, and
        /// one through a hole is joined to the rim. Over the square 100 by 100 in the floor with a corner at the origin and
        /// a hole 40 by 40 in its middle, the ray from (45, 50, 10) pointing straight down is joined from (45, 50, 0) to
        /// the rim at (30, 50, 0), 15 long, as <see cref="DistanceTo(GeoFace3)"/> measures it; from (20, 20, 10) it meets
        /// the material at (20, 20, 0).
        /// A shape coming end-on at the other and stopping within the point tolerance of it is read as crossing it, so
        /// the line can be of no length where the distance is the gap: the segment from (5, 5, 0.0004) to (5, 5, 9),
        /// over the square 100 by 100 in the floor, is joined by a segment of no length at (5, 5, 0.0004), and is
        /// 0.0004 from it by its distance.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public GeoLine3 GetShortestLineTo(GeoFace3 face, Tolerance tolerance) => Projection3.GetShortestLineTo(this, face, tolerance);

        /// <summary>
        /// Gets the shortest segment joining this ray to a polygon, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// The polygon is read as a filled surface: a ray running into it meets it in a segment of no length at the
        /// crossing. Beside the square 100 by 100 in the floor with a corner at the origin, the ray from (130, 50, 40)
        /// along X is joined from its origin to (100, 50, 0) on the edge, 50 long, as <see cref="DistanceTo(GeoPolygon3)"/>
        /// measures it; the ray from (20, 20, 10) pointing straight down meets the square at (20, 20, 0).
        /// A shape coming end-on at the other and stopping within the point tolerance of it is read as crossing it, so
        /// the line can be of no length where the distance is the gap: the segment from (5, 5, 0.0004) to (5, 5, 9),
        /// over the square 100 by 100 in the floor, is joined by a segment of no length at (5, 5, 0.0004), and is
        /// 0.0004 from it by its distance.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public GeoLine3 GetShortestLineTo(GeoPolygon3 polygon) => Projection3.GetShortestLineTo(this, polygon);

        /// <summary>
        /// Gets the shortest segment joining this ray to a polygon, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The polygon is read as a filled surface: a ray running into it meets it in a segment of no length at the
        /// crossing. Beside the square 100 by 100 in the floor with a corner at the origin, the ray from (130, 50, 40)
        /// along X is joined from its origin to (100, 50, 0) on the edge, 50 long, as <see cref="DistanceTo(GeoPolygon3)"/>
        /// measures it; the ray from (20, 20, 10) pointing straight down meets the square at (20, 20, 0).
        /// A shape coming end-on at the other and stopping within the point tolerance of it is read as crossing it, so
        /// the line can be of no length where the distance is the gap: the segment from (5, 5, 0.0004) to (5, 5, 9),
        /// over the square 100 by 100 in the floor, is joined by a segment of no length at (5, 5, 0.0004), and is
        /// 0.0004 from it by its distance.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public GeoLine3 GetShortestLineTo(GeoPolygon3 polygon, Tolerance tolerance) => Projection3.GetShortestLineTo(this, polygon, tolerance);

        /// <summary>
        /// Gets the shortest segment joining this ray to a plane, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// A ray running into the plane meets it in a segment of no length where it passes through, or at its origin where
        /// that lies on it; one running along it or away from it is joined from its origin. The ray from (0, 0, 10)
        /// pointing straight down meets the floor plane at the origin; pointing straight up, or along X, it is joined from
        /// (0, 0, 10) straight down, 10 long, as <see cref="DistanceTo(GeoPlane3)"/> measures it. The segment is never
        /// snapped: a ray a hair off the plane and running along it is joined by a segment that short, though the distance,
        /// read within the tolerance, is nought.
        /// </remarks>
        public GeoLine3 GetShortestLineTo(GeoPlane3 plane) => Projection3.GetShortestLineTo(this, plane);

        /// <summary>
        /// Gets the shortest segment joining this ray to a plane, within a tolerance.
        /// </summary>
        /// <remarks>
        /// A ray running into the plane meets it in a segment of no length where it passes through, or at its origin where
        /// that lies on it; one running along it or away from it is joined from its origin. The ray from (0, 0, 10)
        /// pointing straight down meets the floor plane at the origin; pointing straight up, or along X, it is joined from
        /// (0, 0, 10) straight down, 10 long, as <see cref="DistanceTo(GeoPlane3)"/> measures it. The segment is never
        /// snapped: a ray a hair off the plane and running along it is joined by a segment that short, though the distance,
        /// read within the tolerance, is nought.
        /// </remarks>
        public GeoLine3 GetShortestLineTo(GeoPlane3 plane, Tolerance tolerance) => Projection3.GetShortestLineTo(this, plane, tolerance);

        /// <summary>
        /// Gets the shortest segment joining this ray to another one, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// The two are joined by the common perpendicular of their lines where both rays reach it, and otherwise from an
        /// origin to the nearest point of the other ray. The ray from the origin along X and the ray from (50, 30, 40)
        /// along Y are joined from (50, 0, 0) to (50, 30, 40), 50 long, as <see cref="DistanceTo(GeoRay3)"/> measures it;
        /// the ray from (50, -50, 0) along Y crosses the first and the segment has no length at (50, 0, 0). The segment is
        /// never snapped: rays passing a hair apart are joined by a segment that short, and are that far apart by the
        /// distance too.
        /// </remarks>
        public GeoLine3 GetShortestLineTo(GeoRay3 other) => Projection3.GetShortestLineTo(this, other);

        /// <summary>
        /// Gets the shortest segment joining this ray to another one, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The two are joined by the common perpendicular of their lines where both rays reach it, and otherwise from an
        /// origin to the nearest point of the other ray. The ray from the origin along X and the ray from (50, 30, 40)
        /// along Y are joined from (50, 0, 0) to (50, 30, 40), 50 long, as <see cref="DistanceTo(GeoRay3)"/> measures it;
        /// the ray from (50, -50, 0) along Y crosses the first and the segment has no length at (50, 0, 0). The segment is
        /// never snapped: rays passing a hair apart are joined by a segment that short, and are that far apart by the
        /// distance too.
        /// </remarks>
        public GeoLine3 GetShortestLineTo(GeoRay3 other, Tolerance tolerance) => Projection3.GetShortestLineTo(this, other, tolerance);

        /// <summary>
        /// Gets the shortest segment joining this ray to a polyline: <see cref="GeoPolyline3.GetShortestLineTo(GeoRay3)"/>
        /// turned round, so that it leaves this ray and lands on the polyline.
        /// </summary>
        /// <remarks>
        /// A tie goes to the first edge of the chain. The ray from (60, 50, 30) pointing straight up is joined to the chain
        /// (0, 0, 0), (100, 0, 0), (100, 100, 0) from its origin to (100, 50, 0), on the chain's second edge, 50 long, as
        /// <see cref="DistanceTo(GeoPolyline3)"/> measures it.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the polyline is null.</exception>
        public GeoLine3 GetShortestLineTo(GeoPolyline3 polyline) => Projection3.GetShortestLineTo(polyline, this).Reverse();

        /// <summary>
        /// Gets the shortest segment joining this ray to a polyline, within a tolerance:
        /// <see cref="GeoPolyline3.GetShortestLineTo(GeoRay3, Tolerance)"/> turned round, so that it leaves this ray and
        /// lands on the polyline.
        /// </summary>
        /// <remarks>
        /// A tie goes to the first edge of the chain. The ray from (60, 50, 30) pointing straight up is joined to the chain
        /// (0, 0, 0), (100, 0, 0), (100, 100, 0) from its origin to (100, 50, 0), on the chain's second edge, 50 long, as
        /// <see cref="DistanceTo(GeoPolyline3)"/> measures it.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the polyline is null.</exception>
        public GeoLine3 GetShortestLineTo(GeoPolyline3 polyline, Tolerance tolerance)
            => Projection3.GetShortestLineTo(polyline, this, tolerance).Reverse();
    }
}
