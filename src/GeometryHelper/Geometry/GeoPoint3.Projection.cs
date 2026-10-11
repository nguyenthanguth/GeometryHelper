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

        /// <summary>
        /// Gets the shortest line from this point to the boundary of a curved loop:
        /// <see cref="GeoPolygonArc3.GetShortestLineTo(GeoPoint3)"/> turned round, so that it leaves this point and
        /// lands on the loop.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public GeoLine3 GetShortestLineTo(GeoPolygonArc3 loop)
            => (loop ?? throw new ArgumentNullException(nameof(loop))).GetShortestLineTo(this).Reverse();

        /// <summary>
        /// Gets the shortest line from this point to the boundary of a curved loop, within a tolerance:
        /// <see cref="GeoPolygonArc3.GetShortestLineTo(GeoPoint3, Tolerance)"/> turned round, so that it leaves this
        /// point and lands on the loop.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        public GeoLine3 GetShortestLineTo(GeoPolygonArc3 loop, Tolerance tolerance)
            => (loop ?? throw new ArgumentNullException(nameof(loop))).GetShortestLineTo(this, tolerance).Reverse();

        /// <summary>
        /// Gets the shortest segment joining this point to an arc: <see cref="GeoArc3.GetShortestLineTo(GeoPoint3)"/>
        /// turned round, so that it leaves this point and lands on the arc.
        /// </summary>
        /// <remarks>
        /// The segment leaves this point and lands on the arc, as long as <see cref="DistanceTo(GeoArc3)"/>.
        /// </remarks>
        public GeoLine3 GetShortestLineTo(GeoArc3 arc) => arc.GetShortestLineTo(this).Reverse();

        /// <summary>
        /// Gets the shortest segment joining this point to an arc, within a tolerance:
        /// <see cref="GeoArc3.GetShortestLineTo(GeoPoint3, Tolerance)"/> turned round, so that it leaves this point and
        /// lands on the arc.
        /// </summary>
        /// <remarks>
        /// The segment leaves this point and lands on the arc, as long as <see cref="DistanceTo(GeoArc3)"/>.
        /// </remarks>
        public GeoLine3 GetShortestLineTo(GeoArc3 arc, Tolerance tolerance)
            => arc.GetShortestLineTo(this, tolerance).Reverse();

        /// <summary>
        /// Gets the shortest segment joining this point to a circle:
        /// <see cref="GeoCircle3.GetShortestLineTo(GeoPoint3)"/> turned round, so that it leaves this point and lands
        /// on the circle.
        /// </summary>
        /// <remarks>
        /// The circle is read as the filled disc its distance reads, so the segment is as long as
        /// <see cref="DistanceTo(GeoCircle3)"/>: the point (80, 0, 40) is joined to the circle of radius 50 about the
        /// origin on the XY plane at (50, 0, 0), 50 long.
        /// </remarks>
        public GeoLine3 GetShortestLineTo(GeoCircle3 circle) => Projection3.GetShortestLineTo(circle, this).Reverse();

        /// <summary>
        /// Gets the shortest segment joining this point to a circle, within a tolerance:
        /// <see cref="GeoCircle3.GetShortestLineTo(GeoPoint3, Tolerance)"/> turned round, so that it leaves this point
        /// and lands on the circle.
        /// </summary>
        /// <remarks>
        /// The circle is read as the filled disc its distance reads, so the segment is as long as
        /// <see cref="DistanceTo(GeoCircle3)"/>: the point (80, 0, 40) is joined to the circle of radius 50 about the
        /// origin on the XY plane at (50, 0, 0), 50 long.
        /// </remarks>
        public GeoLine3 GetShortestLineTo(GeoCircle3 circle, Tolerance tolerance)
            => Projection3.GetShortestLineTo(circle, this, tolerance).Reverse();

        /// <summary>
        /// Gets the shortest segment joining this point to an edge: <see cref="GeoEdge3.GetShortestLineTo(GeoPoint3)"/>
        /// turned round, so that it leaves this point and lands on the edge.
        /// </summary>
        /// <remarks>
        /// The segment leaves this point and lands on the edge, as long as
        /// <see cref="DistanceTo(GeoEdge3, Tolerance)"/>.
        /// </remarks>
        public GeoLine3 GetShortestLineTo(GeoEdge3 edge) => edge.GetShortestLineTo(this).Reverse();

        /// <summary>
        /// Gets the shortest segment joining this point to an edge, within a tolerance:
        /// <see cref="GeoEdge3.GetShortestLineTo(GeoPoint3, Tolerance)"/> turned round, so that it leaves this point
        /// and lands on the edge.
        /// </summary>
        /// <remarks>
        /// The segment leaves this point and lands on the edge, as long as
        /// <see cref="DistanceTo(GeoEdge3, Tolerance)"/>.
        /// </remarks>
        public GeoLine3 GetShortestLineTo(GeoEdge3 edge, Tolerance tolerance)
            => edge.GetShortestLineTo(this, tolerance).Reverse();

        /// <summary>
        /// Gets the shortest segment joining this point to a face: <see cref="GeoFace3.GetShortestLineTo(GeoPoint3)"/>
        /// turned round, so that it leaves this point and lands on the face.
        /// </summary>
        /// <remarks>
        /// A point above a hole is joined to its rim, as <see cref="DistanceTo(GeoFace3)"/> measures it: the point (45,
        /// 50, 30) is joined to the square 100 by 100 on the XY plane with a corner at the origin and a hole 20 by 20
        /// in its middle at (40, 50, 0), about 30.41 long.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public GeoLine3 GetShortestLineTo(GeoFace3 face) => Projection3.GetShortestLineTo(face, this).Reverse();

        /// <summary>
        /// Gets the shortest segment joining this point to a face, within a tolerance:
        /// <see cref="GeoFace3.GetShortestLineTo(GeoPoint3, Tolerance)"/> turned round, so that it leaves this point
        /// and lands on the face.
        /// </summary>
        /// <remarks>
        /// A point above a hole is joined to its rim, as <see cref="DistanceTo(GeoFace3)"/> measures it: the point (45,
        /// 50, 30) is joined to the square 100 by 100 on the XY plane with a corner at the origin and a hole 20 by 20
        /// in its middle at (40, 50, 0), about 30.41 long.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public GeoLine3 GetShortestLineTo(GeoFace3 face, Tolerance tolerance)
            => Projection3.GetShortestLineTo(face, this, tolerance).Reverse();

        /// <summary>
        /// Gets the shortest segment joining this point to a segment:
        /// <see cref="GeoLine3.GetShortestLineTo(GeoPoint3)"/> turned round, so that it leaves this point and lands on
        /// the segment.
        /// </summary>
        /// <remarks>
        /// The segment leaves this point and lands on the segment, as long as <see cref="DistanceTo(GeoLine3)"/>: the
        /// point (50, 30, 40) is joined to the segment joining (0, 0, 0) and (100, 0, 0) at (50, 0, 0), 50 long.
        /// </remarks>
        public GeoLine3 GetShortestLineTo(GeoLine3 line) => Projection3.GetShortestLineTo(line, this).Reverse();

        /// <summary>
        /// Gets the shortest segment joining this point to a segment, within a tolerance:
        /// <see cref="GeoLine3.GetShortestLineTo(GeoPoint3, Tolerance)"/> turned round, so that it leaves this point
        /// and lands on the segment.
        /// </summary>
        /// <remarks>
        /// The segment leaves this point and lands on the segment, as long as <see cref="DistanceTo(GeoLine3)"/>: the
        /// point (50, 30, 40) is joined to the segment joining (0, 0, 0) and (100, 0, 0) at (50, 0, 0), 50 long.
        /// </remarks>
        public GeoLine3 GetShortestLineTo(GeoLine3 line, Tolerance tolerance)
            => Projection3.GetShortestLineTo(line, this, tolerance).Reverse();

        /// <summary>
        /// Gets the shortest segment joining this point to a plane:
        /// <see cref="GeoPlane3.GetShortestLineTo(GeoPoint3)"/> turned round, so that it leaves this point and lands on
        /// the plane.
        /// </summary>
        /// <remarks>
        /// The perpendicular from this point down to its foot, as long as <see cref="DistanceTo(GeoPlane3)"/>: the
        /// point (10, 20, 30) is joined to the XY plane at (10, 20, 0), 30 long.
        /// </remarks>
        public GeoLine3 GetShortestLineTo(GeoPlane3 plane) => Projection3.GetShortestLineTo(plane, this).Reverse();

        /// <summary>
        /// Gets the shortest segment joining this point to a plane, within a tolerance:
        /// <see cref="GeoPlane3.GetShortestLineTo(GeoPoint3, Tolerance)"/> turned round, so that it leaves this point
        /// and lands on the plane.
        /// </summary>
        /// <remarks>
        /// The perpendicular from this point down to its foot, as long as <see cref="DistanceTo(GeoPlane3)"/>: the
        /// point (10, 20, 30) is joined to the XY plane at (10, 20, 0), 30 long. The tolerance is taken so that this
        /// reads like every other shape; it changes nothing here.
        /// </remarks>
        public GeoLine3 GetShortestLineTo(GeoPlane3 plane, Tolerance tolerance)
            => Projection3.GetShortestLineTo(plane, this, tolerance).Reverse();

        /// <summary>
        /// Gets the shortest segment joining this point to a polygon:
        /// <see cref="GeoPolygon3.GetShortestLineTo(GeoPoint3)"/> turned round, so that it leaves this point and lands
        /// on the polygon.
        /// </summary>
        /// <remarks>
        /// The polygon is read as a filled surface, as its distance reads it: the point (50, 50, 30) is joined straight
        /// down to the square 100 by 100 on the XY plane with a corner at the origin, 30 long, as long as
        /// <see cref="DistanceTo(GeoPolygon3)"/>.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public GeoLine3 GetShortestLineTo(GeoPolygon3 polygon)
            => Projection3.GetShortestLineTo(polygon, this).Reverse();

        /// <summary>
        /// Gets the shortest segment joining this point to a polygon, within a tolerance:
        /// <see cref="GeoPolygon3.GetShortestLineTo(GeoPoint3, Tolerance)"/> turned round, so that it leaves this point
        /// and lands on the polygon.
        /// </summary>
        /// <remarks>
        /// The polygon is read as a filled surface, as its distance reads it: the point (50, 50, 30) is joined straight
        /// down to the square 100 by 100 on the XY plane with a corner at the origin, 30 long, as long as
        /// <see cref="DistanceTo(GeoPolygon3)"/>.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public GeoLine3 GetShortestLineTo(GeoPolygon3 polygon, Tolerance tolerance)
            => Projection3.GetShortestLineTo(polygon, this, tolerance).Reverse();

        /// <summary>
        /// Gets the shortest segment joining this point to a polyline:
        /// <see cref="GeoPolyline3.GetShortestLineTo(GeoPoint3)"/> turned round, so that it leaves this point and lands
        /// on the polyline.
        /// </summary>
        /// <remarks>
        /// The segment leaves this point and lands on the chain, as long as <see cref="DistanceTo(GeoPolyline3)"/>: the
        /// point (50, 30, 40) is joined to the chain (0, 0, 0), (100, 0, 0), (100, 100, 0) at (50, 0, 0), 50 long.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the polyline is null.</exception>
        public GeoLine3 GetShortestLineTo(GeoPolyline3 polyline)
            => Projection3.GetShortestLineTo(polyline, this).Reverse();

        /// <summary>
        /// Gets the shortest segment joining this point to a polyline, within a tolerance:
        /// <see cref="GeoPolyline3.GetShortestLineTo(GeoPoint3, Tolerance)"/> turned round, so that it leaves this
        /// point and lands on the polyline.
        /// </summary>
        /// <remarks>
        /// The segment leaves this point and lands on the chain, as long as <see cref="DistanceTo(GeoPolyline3)"/>: the
        /// point (50, 30, 40) is joined to the chain (0, 0, 0), (100, 0, 0), (100, 100, 0) at (50, 0, 0), 50 long.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the polyline is null.</exception>
        public GeoLine3 GetShortestLineTo(GeoPolyline3 polyline, Tolerance tolerance)
            => Projection3.GetShortestLineTo(polyline, this, tolerance).Reverse();

        /// <summary>
        /// Gets the shortest segment joining this point to a curved chain:
        /// <see cref="GeoPolylineArc3.GetShortestLineTo(GeoPoint3)"/> turned round, so that it leaves this point and
        /// lands on the curved chain.
        /// </summary>
        /// <remarks>
        /// The segment leaves this point and lands on the chain, measured on the arcs, as long as
        /// <see cref="DistanceTo(GeoPolylineArc3, Tolerance)"/>.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public GeoLine3 GetShortestLineTo(GeoPolylineArc3 chain)
            => (chain ?? throw new ArgumentNullException(nameof(chain))).GetShortestLineTo(this).Reverse();

        /// <summary>
        /// Gets the shortest segment joining this point to a curved chain, within a tolerance:
        /// <see cref="GeoPolylineArc3.GetShortestLineTo(GeoPoint3, Tolerance)"/> turned round, so that it leaves this
        /// point and lands on the curved chain.
        /// </summary>
        /// <remarks>
        /// The segment leaves this point and lands on the chain, measured on the arcs, as long as
        /// <see cref="DistanceTo(GeoPolylineArc3, Tolerance)"/>.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the chain is null.</exception>
        public GeoLine3 GetShortestLineTo(GeoPolylineArc3 chain, Tolerance tolerance)
            => (chain ?? throw new ArgumentNullException(nameof(chain))).GetShortestLineTo(this, tolerance).Reverse();

        /// <summary>
        /// Gets the shortest segment joining this point to a ray: <see cref="GeoRay3.GetShortestLineTo(GeoPoint3)"/>
        /// turned round, so that it leaves this point and lands on the ray.
        /// </summary>
        /// <remarks>
        /// The segment leaves this point and lands on the ray, as long as <see cref="DistanceTo(GeoRay3)"/>: the point
        /// (-30, 0, 40) is joined to the ray leaving the origin along X at its origin, 50 long.
        /// </remarks>
        public GeoLine3 GetShortestLineTo(GeoRay3 ray) => Projection3.GetShortestLineTo(ray, this).Reverse();

        /// <summary>
        /// Gets the shortest segment joining this point to a ray, within a tolerance:
        /// <see cref="GeoRay3.GetShortestLineTo(GeoPoint3, Tolerance)"/> turned round, so that it leaves this point and
        /// lands on the ray.
        /// </summary>
        /// <remarks>
        /// The segment leaves this point and lands on the ray, as long as <see cref="DistanceTo(GeoRay3)"/>: the point
        /// (-30, 0, 40) is joined to the ray leaving the origin along X at its origin, 50 long. The tolerance is taken
        /// so that this reads like every other shape; it changes nothing here.
        /// </remarks>
        public GeoLine3 GetShortestLineTo(GeoRay3 ray, Tolerance tolerance)
            => Projection3.GetShortestLineTo(ray, this, tolerance).Reverse();

        /// <summary>
        /// Gets the shortest segment joining this point to a triangle:
        /// <see cref="GeoTriangle3.GetShortestLineTo(GeoPoint3)"/> turned round, so that it leaves this point and lands
        /// on the triangle.
        /// </summary>
        /// <remarks>
        /// The triangle is read as a filled surface, as its distance reads it: the point (20, 20, 30) is joined
        /// straight down to the triangle (0, 0, 0), (100, 0, 0), (0, 100, 0), 30 long, as long as
        /// <see cref="DistanceTo(GeoTriangle3)"/>.
        /// </remarks>
        public GeoLine3 GetShortestLineTo(GeoTriangle3 triangle)
            => Projection3.GetShortestLineTo(triangle, this).Reverse();

        /// <summary>
        /// Gets the shortest segment joining this point to a triangle, within a tolerance:
        /// <see cref="GeoTriangle3.GetShortestLineTo(GeoPoint3, Tolerance)"/> turned round, so that it leaves this
        /// point and lands on the triangle.
        /// </summary>
        /// <remarks>
        /// The triangle is read as a filled surface, as its distance reads it: the point (20, 20, 30) is joined
        /// straight down to the triangle (0, 0, 0), (100, 0, 0), (0, 100, 0), 30 long, as long as
        /// <see cref="DistanceTo(GeoTriangle3)"/>. The tolerance is taken so that this reads like every other shape; it
        /// changes nothing here.
        /// </remarks>
        public GeoLine3 GetShortestLineTo(GeoTriangle3 triangle, Tolerance tolerance)
            => Projection3.GetShortestLineTo(triangle, this, tolerance).Reverse();
    }
}
