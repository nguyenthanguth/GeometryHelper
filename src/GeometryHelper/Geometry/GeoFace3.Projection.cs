using System;
using System.Collections.Generic;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// The point of a face nearest another shape, the shortest segment joining the two, and which
    /// edge of it faces one.
    /// </summary>
    public sealed partial class GeoFace3
    {
        /// <summary>
        /// Gets the point of this face nearest a target point.
        /// </summary>
        /// <remarks>
        /// The foot of the perpendicular where that lands on the material, the rim of a hole where it lands
        /// in one, and the outline where it lands off the face altogether. This reads the way
        /// <see cref="GeoPolygon3.GetClosestPointOnBoundary(GeoPoint3)"/> reads, which answers with a point
        /// of the region rather than of its outline.
        /// </remarks>
        public GeoPoint3 GetClosestPointOnBoundary(GeoPoint3 point) => Projection3.ProjectToFace(this, point);

        /// <summary>
        /// Gets the point of this face nearest a target point, within a tolerance.
        /// </summary>
        public GeoPoint3 GetClosestPointOnBoundary(GeoPoint3 point, Tolerance tolerance) => Projection3.ProjectToFace(this, point, tolerance);

        /// <summary>
        /// Gets the shortest line from this face to the boundary of a curved loop in the same plane:
        /// <see cref="GeoPolygonArc3.GetShortestLineTo(GeoFace3)"/> turned round, so that it leaves this face and lands
        /// on the loop.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        /// <exception cref="ArgumentException">Thrown when this face does not lie in the loop's plane.</exception>
        public GeoLine3 GetShortestLineTo(GeoPolygonArc3 loop)
            => (loop ?? throw new ArgumentNullException(nameof(loop))).GetShortestLineTo(this).Reverse();

        /// <summary>
        /// Gets the shortest line from this face to the boundary of a curved loop in the same plane, within a
        /// tolerance: <see cref="GeoPolygonArc3.GetShortestLineTo(GeoFace3, Tolerance)"/> turned round, so that it
        /// leaves this face and lands on the loop.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        /// <exception cref="ArgumentException">Thrown when this face does not lie in the loop's plane.</exception>
        public GeoLine3 GetShortestLineTo(GeoPolygonArc3 loop, Tolerance tolerance)
            => (loop ?? throw new ArgumentNullException(nameof(loop))).GetShortestLineTo(this, tolerance).Reverse();

        /// <summary>
        /// Gets the shortest segment joining this face to a point, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// The face is its outline less its holes, as its distance reads it, so a point above a hole is joined from the
        /// rim: from the square 100 by 100 on the XY plane with a corner at the origin and a hole 20 by 20 in its
        /// middle, the point (45, 50, 30) gets the one from (40, 50, 0), about 30.41 long, as long as
        /// <see cref="DistanceTo(GeoPoint3)"/>.
        /// </remarks>
        public GeoLine3 GetShortestLineTo(GeoPoint3 point) => Projection3.GetShortestLineTo(this, point);

        /// <summary>
        /// Gets the shortest segment joining this face to a point, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The face is its outline less its holes, as its distance reads it, so a point above a hole is joined from the
        /// rim: from the square 100 by 100 on the XY plane with a corner at the origin and a hole 20 by 20 in its
        /// middle, the point (45, 50, 30) gets the one from (40, 50, 0), about 30.41 long, as long as
        /// <see cref="DistanceTo(GeoPoint3)"/>.
        /// </remarks>
        public GeoLine3 GetShortestLineTo(GeoPoint3 point, Tolerance tolerance)
            => Projection3.GetShortestLineTo(this, point, tolerance);

        /// <summary>
        /// Gets the shortest segment joining this face to another one, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// The segment leaves the material of this face and lands on that of the other, of no length where the two meet;
        /// the edges of this face, outline and then holes, are walked first, and a tie goes to the first. The square 100 by
        /// 100 in the floor with a corner at the origin and a hole 40 by 40 in its middle, and the square 20 by 20 lying in
        /// the hole about (50, 50), are joined from the rim at (40, 30, 0) to (40, 40, 0), 10 long, as
        /// <see cref="DistanceTo(GeoFace3)"/> measures it.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public GeoLine3 GetShortestLineTo(GeoFace3 other) => Projection3.GetShortestLineTo(this, other);

        /// <summary>
        /// Gets the shortest segment joining this face to another one, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The segment leaves the material of this face and lands on that of the other, of no length where the two meet;
        /// the edges of this face, outline and then holes, are walked first, and a tie goes to the first. The square 100 by
        /// 100 in the floor with a corner at the origin and a hole 40 by 40 in its middle, and the square 20 by 20 lying in
        /// the hole about (50, 50), are joined from the rim at (40, 30, 0) to (40, 40, 0), 10 long, as
        /// <see cref="DistanceTo(GeoFace3)"/> measures it.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public GeoLine3 GetShortestLineTo(GeoFace3 other, Tolerance tolerance) => Projection3.GetShortestLineTo(this, other, tolerance);

        /// <summary>
        /// Gets the shortest segment joining this face to a segment: <see cref="GeoLine3.GetShortestLineTo(GeoFace3)"/>
        /// turned round, so that it leaves this face and lands on the segment.
        /// </summary>
        /// <remarks>
        /// This face is read by its material, as its distance reads it. The square 100 by 100 in the floor with a corner at
        /// the origin and a hole 40 by 40 in its middle is joined to the segment from (45, 50, -10) to (45, 50, 10),
        /// through its hole, from the rim at (30, 50, 0) to (45, 50, 0), 15 long, as <see cref="DistanceTo(GeoLine3)"/>
        /// measures it.
        /// </remarks>
        public GeoLine3 GetShortestLineTo(GeoLine3 line) => Projection3.GetShortestLineTo(line, this).Reverse();

        /// <summary>
        /// Gets the shortest segment joining this face to a segment, within a tolerance:
        /// <see cref="GeoLine3.GetShortestLineTo(GeoFace3, Tolerance)"/> turned round, so that it leaves this face and
        /// lands on the segment.
        /// </summary>
        /// <remarks>
        /// This face is read by its material, as its distance reads it. The square 100 by 100 in the floor with a corner at
        /// the origin and a hole 40 by 40 in its middle is joined to the segment from (45, 50, -10) to (45, 50, 10),
        /// through its hole, from the rim at (30, 50, 0) to (45, 50, 0), 15 long, as <see cref="DistanceTo(GeoLine3)"/>
        /// measures it.
        /// </remarks>
        public GeoLine3 GetShortestLineTo(GeoLine3 line, Tolerance tolerance) => Projection3.GetShortestLineTo(line, this, tolerance).Reverse();

        /// <summary>
        /// Gets the shortest segment joining this face to a plane, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// A face that crosses or touches the plane meets it in a segment of no length at the first point of contact along
        /// its outline; one clear of it is joined from its nearest corner. The square 100 by 100 with a hole 40 by 40 in
        /// its middle, stood upright along y = 50 from z = 10 to 110, is joined from its corner (0, 50, 10) to the floor
        /// plane, 10 long, as <see cref="DistanceTo(GeoPlane3)"/> measures it; stood from z = -50 to 50, it meets the floor
        /// at (100, 50, 0). The segment is never snapped: a face a hair off the plane is joined by a segment that short,
        /// though the distance, read within the point tolerance, is nought.
        /// </remarks>
        public GeoLine3 GetShortestLineTo(GeoPlane3 plane) => Projection3.GetShortestLineTo(this, plane);

        /// <summary>
        /// Gets the shortest segment joining this face to a plane, within a tolerance.
        /// </summary>
        /// <remarks>
        /// A face that crosses or touches the plane meets it in a segment of no length at the first point of contact along
        /// its outline; one clear of it is joined from its nearest corner. The square 100 by 100 with a hole 40 by 40 in
        /// its middle, stood upright along y = 50 from z = 10 to 110, is joined from its corner (0, 50, 10) to the floor
        /// plane, 10 long, as <see cref="DistanceTo(GeoPlane3)"/> measures it; stood from z = -50 to 50, it meets the floor
        /// at (100, 50, 0). The segment is never snapped: a face a hair off the plane is joined by a segment that short,
        /// though the distance, read within the point tolerance, is nought.
        /// </remarks>
        public GeoLine3 GetShortestLineTo(GeoPlane3 plane, Tolerance tolerance) => Projection3.GetShortestLineTo(this, plane, tolerance);

        /// <summary>
        /// Gets the shortest segment joining this face to a polygon, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// The segment leaves the material of this face and lands on the polygon, of no length where the two meet; the
        /// edges of this face are walked first, and a tie goes to the first. The square 100 by 100 in the floor with a
        /// corner at the origin and a hole 40 by 40 in its middle, and a square 20 by 20 standing upright in the middle of
        /// the hole along y = 50 from x = 40 to 60, are joined from the rim at (70, 50, 0) to (60, 50, 0), 10 long, as
        /// <see cref="DistanceTo(GeoPolygon3)"/> measures it.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public GeoLine3 GetShortestLineTo(GeoPolygon3 polygon) => Projection3.GetShortestLineTo(this, polygon);

        /// <summary>
        /// Gets the shortest segment joining this face to a polygon, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The segment leaves the material of this face and lands on the polygon, of no length where the two meet; the
        /// edges of this face are walked first, and a tie goes to the first. The square 100 by 100 in the floor with a
        /// corner at the origin and a hole 40 by 40 in its middle, and a square 20 by 20 standing upright in the middle of
        /// the hole along y = 50 from x = 40 to 60, are joined from the rim at (70, 50, 0) to (60, 50, 0), 10 long, as
        /// <see cref="DistanceTo(GeoPolygon3)"/> measures it.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public GeoLine3 GetShortestLineTo(GeoPolygon3 polygon, Tolerance tolerance) => Projection3.GetShortestLineTo(this, polygon, tolerance);

        /// <summary>
        /// Gets the shortest segment joining this face to a polyline:
        /// <see cref="GeoPolyline3.GetShortestLineTo(GeoFace3)"/> turned round, so that it leaves this face and lands on
        /// the polyline.
        /// </summary>
        /// <remarks>
        /// The square 100 by 100 in the floor with a corner at the origin and a hole 40 by 40 in its middle is joined to
        /// the chain (45, 50, 30), (45, 50, -30), through its hole, from the rim at (30, 50, 0) to (45, 50, 0), 15 long, as
        /// <see cref="DistanceTo(GeoPolyline3)"/> measures it.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the polyline is null.</exception>
        public GeoLine3 GetShortestLineTo(GeoPolyline3 polyline) => Projection3.GetShortestLineTo(polyline, this).Reverse();

        /// <summary>
        /// Gets the shortest segment joining this face to a polyline, within a tolerance:
        /// <see cref="GeoPolyline3.GetShortestLineTo(GeoFace3, Tolerance)"/> turned round, so that it leaves this face and
        /// lands on the polyline.
        /// </summary>
        /// <remarks>
        /// The square 100 by 100 in the floor with a corner at the origin and a hole 40 by 40 in its middle is joined to
        /// the chain (45, 50, 30), (45, 50, -30), through its hole, from the rim at (30, 50, 0) to (45, 50, 0), 15 long, as
        /// <see cref="DistanceTo(GeoPolyline3)"/> measures it.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the polyline is null.</exception>
        public GeoLine3 GetShortestLineTo(GeoPolyline3 polyline, Tolerance tolerance)
            => Projection3.GetShortestLineTo(polyline, this, tolerance).Reverse();

        /// <summary>
        /// Gets the shortest segment joining this face to a ray: <see cref="GeoRay3.GetShortestLineTo(GeoFace3)"/> turned
        /// round, so that it leaves this face and lands on the ray.
        /// </summary>
        /// <remarks>
        /// The square 100 by 100 in the floor with a corner at the origin and a hole 40 by 40 in its middle is joined to
        /// the ray from (45, 50, 10) pointing straight down, through its hole, from the rim at (30, 50, 0) to (45, 50, 0),
        /// 15 long, as <see cref="DistanceTo(GeoRay3)"/> measures it.
        /// </remarks>
        public GeoLine3 GetShortestLineTo(GeoRay3 ray) => Projection3.GetShortestLineTo(ray, this).Reverse();

        /// <summary>
        /// Gets the shortest segment joining this face to a ray, within a tolerance:
        /// <see cref="GeoRay3.GetShortestLineTo(GeoFace3, Tolerance)"/> turned round, so that it leaves this face and lands
        /// on the ray.
        /// </summary>
        /// <remarks>
        /// The square 100 by 100 in the floor with a corner at the origin and a hole 40 by 40 in its middle is joined to
        /// the ray from (45, 50, 10) pointing straight down, through its hole, from the rim at (30, 50, 0) to (45, 50, 0),
        /// 15 long, as <see cref="DistanceTo(GeoRay3)"/> measures it.
        /// </remarks>
        public GeoLine3 GetShortestLineTo(GeoRay3 ray, Tolerance tolerance) => Projection3.GetShortestLineTo(ray, this, tolerance).Reverse();

        /// <summary>
        /// Gets the shortest segment joining this face to a solid: <see cref="GeoSolid3.GetShortestLineTo(GeoFace3)"/>
        /// turned round, so that it leaves this face and lands on the solid.
        /// </summary>
        /// <remarks>
        /// The segment runs from the material of this face to the surface of the body, so a face lying wholly inside is
        /// joined out to the nearest face of the body, where <see cref="DistanceTo(GeoSolid3)"/> answers nought; outside,
        /// it is as long as that distance. The square 200 by 200 from (-50, -50, 50) to (150, 150, 50) with a hole 150 by
        /// 150 in its middle, around the box 100 by 100 by 100 with a corner at the origin, is joined from its rim to the
        /// box across the 25 between them.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the solid is null.</exception>
        public GeoLine3 GetShortestLineTo(GeoSolid3 solid) => Projection3.GetShortestLineTo(solid, this).Reverse();

        /// <summary>
        /// Gets the shortest segment joining this face to a solid, within a tolerance:
        /// <see cref="GeoSolid3.GetShortestLineTo(GeoFace3, Tolerance)"/> turned round, so that it leaves this face and
        /// lands on the solid.
        /// </summary>
        /// <remarks>
        /// The segment runs from the material of this face to the surface of the body, so a face lying wholly inside is
        /// joined out to the nearest face of the body, where <see cref="DistanceTo(GeoSolid3)"/> answers nought; outside,
        /// it is as long as that distance. The square 200 by 200 from (-50, -50, 50) to (150, 150, 50) with a hole 150 by
        /// 150 in its middle, around the box 100 by 100 by 100 with a corner at the origin, is joined from its rim to the
        /// box across the 25 between them.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the solid is null.</exception>
        public GeoLine3 GetShortestLineTo(GeoSolid3 solid, Tolerance tolerance) => Projection3.GetShortestLineTo(solid, this, tolerance).Reverse();

        /// <summary>
        /// Gets the shortest segment joining this face to a triangle:
        /// <see cref="GeoTriangle3.GetShortestLineTo(GeoFace3)"/> turned round, so that it leaves this face and lands on
        /// the triangle.
        /// </summary>
        /// <remarks>
        /// The square 100 by 100 in the floor with a corner at the origin and a hole 40 by 40 in its middle is joined to
        /// the triangle with corners (110, 50, 0), (200, 50, 0) and (110, 50, 100), standing beside it, from (100, 50, 0)
        /// on its outline to the corner (110, 50, 0), 10 long, as <see cref="DistanceTo(GeoTriangle3)"/> measures it.
        /// </remarks>
        public GeoLine3 GetShortestLineTo(GeoTriangle3 triangle) => Projection3.GetShortestLineTo(triangle, this).Reverse();

        /// <summary>
        /// Gets the shortest segment joining this face to a triangle, within a tolerance:
        /// <see cref="GeoTriangle3.GetShortestLineTo(GeoFace3, Tolerance)"/> turned round, so that it leaves this face and
        /// lands on the triangle.
        /// </summary>
        /// <remarks>
        /// The square 100 by 100 in the floor with a corner at the origin and a hole 40 by 40 in its middle is joined to
        /// the triangle with corners (110, 50, 0), (200, 50, 0) and (110, 50, 100), standing beside it, from (100, 50, 0)
        /// on its outline to the corner (110, 50, 0), 10 long, as <see cref="DistanceTo(GeoTriangle3)"/> measures it.
        /// </remarks>
        public GeoLine3 GetShortestLineTo(GeoTriangle3 triangle, Tolerance tolerance)
            => Projection3.GetShortestLineTo(triangle, this, tolerance).Reverse();
    }
}
