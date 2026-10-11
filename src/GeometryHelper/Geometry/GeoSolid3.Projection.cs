using System;
using System.Collections.Generic;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// The point of a solid nearest another shape, the shortest segment joining the two, and which
    /// edge of it faces one.
    /// </summary>
    public sealed partial class GeoSolid3
    {
        /// <summary>
        /// Gets the point on the surface of this solid closest to a target point.
        /// </summary>
        /// <remarks>
        /// The answer is always on the surface, including for a point inside the body, where
        /// <see cref="DistanceTo(GeoPoint3)"/> reports nothing at all. The two deliberately disagree there,
        /// the same way they do in the plane.
        /// </remarks>
        public GeoPoint3 GetClosestPointOnBoundary(GeoPoint3 point) => Projection3.ProjectToSolid(this, point);

        /// <summary>
        /// Gets the point on the surface of this solid closest to a target point, within a tolerance.
        /// </summary>
        public GeoPoint3 GetClosestPointOnBoundary(GeoPoint3 point, Tolerance tolerance) => Projection3.ProjectToSolid(this, point, tolerance);

        /// <summary>
        /// Gets the shortest segment joining this solid to a point.
        /// </summary>
        /// <remarks>
        /// The segment leaves the surface, so it has a length even for a point inside the body, where
        /// <see cref="DistanceTo(GeoPoint3)"/> reports nothing at all.
        /// </remarks>
        public GeoLine3 GetShortestLineTo(GeoPoint3 point) => Projection3.GetShortestLineTo(this, point);

        /// <summary>
        /// Gets the shortest segment joining this solid to a point, within a tolerance.
        /// </summary>
        public GeoLine3 GetShortestLineTo(GeoPoint3 point, Tolerance tolerance) => Projection3.GetShortestLineTo(this, point, tolerance);

        /// <summary>
        /// Gets the shortest segment joining this solid to a line segment.
        /// </summary>
        public GeoLine3 GetShortestLineTo(GeoLine3 line) => Projection3.GetShortestLineTo(this, line);

        /// <summary>
        /// Gets the shortest segment joining this solid to a line segment, within a tolerance.
        /// </summary>
        public GeoLine3 GetShortestLineTo(GeoLine3 line, Tolerance tolerance) => Projection3.GetShortestLineTo(this, line, tolerance);

        /// <summary>
        /// Gets the shortest segment joining this solid to a triangle.
        /// </summary>
        public GeoLine3 GetShortestLineTo(GeoTriangle3 triangle) => Projection3.GetShortestLineTo(this, triangle);

        /// <summary>
        /// Gets the shortest segment joining this solid to a triangle, within a tolerance.
        /// </summary>
        public GeoLine3 GetShortestLineTo(GeoTriangle3 triangle, Tolerance tolerance) => Projection3.GetShortestLineTo(this, triangle, tolerance);

        /// <summary>
        /// Gets the shortest segment joining this solid to another solid.
        /// </summary>
        public GeoLine3 GetShortestLineTo(GeoSolid3 other) => Projection3.GetShortestLineTo(this, other);

        /// <summary>
        /// Gets the shortest segment joining this solid to another solid, within a tolerance.
        /// </summary>
        public GeoLine3 GetShortestLineTo(GeoSolid3 other, Tolerance tolerance) => Projection3.GetShortestLineTo(this, other, tolerance);

        /// <summary>
        /// Gets the shortest segment joining this solid to a ray.
        /// </summary>
        public GeoLine3 GetShortestLineTo(GeoRay3 ray) => Projection3.GetShortestLineTo(this, ray);

        /// <summary>
        /// Gets the shortest segment joining this solid to a ray, within a tolerance.
        /// </summary>
        public GeoLine3 GetShortestLineTo(GeoRay3 ray, Tolerance tolerance) => Projection3.GetShortestLineTo(this, ray, tolerance);

        /// <summary>
        /// Gets the shortest segment joining this solid to a polyline, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// The segment runs from the surface of this body to the chain, so a chain lying wholly inside is joined out to
        /// the nearest face, where <see cref="DistanceTo(GeoPolyline3)"/> answers nought; outside, it is as long as
        /// that distance. In the box 100 by 100 by 100 with a corner at the origin, the chain (50, 50, 10), (50, 50,
        /// 60), (50, 70, 60) is joined from the bottom face at (50, 50, 0) to (50, 50, 10), 10 long.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the polyline is null.</exception>
        public GeoLine3 GetShortestLineTo(GeoPolyline3 polyline) => Projection3.GetShortestLineTo(this, polyline);

        /// <summary>
        /// Gets the shortest segment joining this solid to a polyline, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The segment runs from the surface of this body to the chain, so a chain lying wholly inside is joined out to
        /// the nearest face, where <see cref="DistanceTo(GeoPolyline3)"/> answers nought; outside, it is as long as
        /// that distance. In the box 100 by 100 by 100 with a corner at the origin, the chain (50, 50, 10), (50, 50,
        /// 60), (50, 70, 60) is joined from the bottom face at (50, 50, 0) to (50, 50, 10), 10 long.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the polyline is null.</exception>
        public GeoLine3 GetShortestLineTo(GeoPolyline3 polyline, Tolerance tolerance)
            => Projection3.GetShortestLineTo(this, polyline, tolerance);

        /// <summary>
        /// Gets the shortest segment joining this solid to a polygon, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// The segment runs from the surface of this body to the polygon, read as a filled surface, so a polygon lying
        /// wholly inside is joined out to the nearest face, where <see cref="DistanceTo(GeoPolygon3)"/> answers nought;
        /// outside, it is as long as that distance. In the box 100 by 100 by 100 with a corner at the origin, the
        /// square 20 by 20 lying flat at a height of 30 is joined from the bottom face, 30 long.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public GeoLine3 GetShortestLineTo(GeoPolygon3 polygon) => Projection3.GetShortestLineTo(this, polygon);

        /// <summary>
        /// Gets the shortest segment joining this solid to a polygon, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The segment runs from the surface of this body to the polygon, read as a filled surface, so a polygon lying
        /// wholly inside is joined out to the nearest face, where <see cref="DistanceTo(GeoPolygon3)"/> answers nought;
        /// outside, it is as long as that distance. In the box 100 by 100 by 100 with a corner at the origin, the
        /// square 20 by 20 lying flat at a height of 30 is joined from the bottom face, 30 long.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public GeoLine3 GetShortestLineTo(GeoPolygon3 polygon, Tolerance tolerance)
            => Projection3.GetShortestLineTo(this, polygon, tolerance);

        /// <summary>
        /// Gets the shortest segment joining this solid to a plane, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// A body that crosses or touches the plane meets it in a segment of no length at the first point of contact,
        /// walking the triangles of its surface in order; one clear of it is joined from its nearest corner, as long as
        /// <see cref="DistanceTo(GeoPlane3)"/>. The box 100 by 100 by 100 with a corner at the origin is joined to the
        /// plane 30 under it from a corner of its bottom face, 30 long. A body of separate pieces either side of the
        /// plane, neither touching it, is nought from it by the distance, which reads only its corners, and is joined
        /// from its nearest corner all the same. The segment is never snapped: a shape a hair off the plane is joined
        /// by a segment that short, though the distance, read within the point tolerance, is nought.
        /// </remarks>
        public GeoLine3 GetShortestLineTo(GeoPlane3 plane) => Projection3.GetShortestLineTo(this, plane);

        /// <summary>
        /// Gets the shortest segment joining this solid to a plane, within a tolerance.
        /// </summary>
        /// <remarks>
        /// A body that crosses or touches the plane meets it in a segment of no length at the first point of contact,
        /// walking the triangles of its surface in order; one clear of it is joined from its nearest corner, as long as
        /// <see cref="DistanceTo(GeoPlane3)"/>. The box 100 by 100 by 100 with a corner at the origin is joined to the
        /// plane 30 under it from a corner of its bottom face, 30 long. A body of separate pieces either side of the
        /// plane, neither touching it, is nought from it by the distance, which reads only its corners, and is joined
        /// from its nearest corner all the same. The segment is never snapped: a shape a hair off the plane is joined
        /// by a segment that short, though the distance, read within the point tolerance, is nought.
        /// </remarks>
        public GeoLine3 GetShortestLineTo(GeoPlane3 plane, Tolerance tolerance)
            => Projection3.GetShortestLineTo(this, plane, tolerance);

        /// <summary>
        /// Gets the shortest segment joining this solid to a face, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// The segment runs from the surface of this body to the material of the face, so a face lying wholly inside is
        /// joined out to the nearest face of the body, where <see cref="DistanceTo(GeoFace3)"/> answers nought; outside, it
        /// is as long as that distance. The box 100 by 100 by 100 with a corner at the origin passes through the hole of
        /// the square 200 by 200 from (-50, -50, 50) to (150, 150, 50) with a hole 150 by 150 in its middle and is joined
        /// to its rim across the 25 between them.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public GeoLine3 GetShortestLineTo(GeoFace3 face) => Projection3.GetShortestLineTo(this, face);

        /// <summary>
        /// Gets the shortest segment joining this solid to a face, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The segment runs from the surface of this body to the material of the face, so a face lying wholly inside is
        /// joined out to the nearest face of the body, where <see cref="DistanceTo(GeoFace3)"/> answers nought; outside, it
        /// is as long as that distance. The box 100 by 100 by 100 with a corner at the origin passes through the hole of
        /// the square 200 by 200 from (-50, -50, 50) to (150, 150, 50) with a hole 150 by 150 in its middle and is joined
        /// to its rim across the 25 between them.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public GeoLine3 GetShortestLineTo(GeoFace3 face, Tolerance tolerance) => Projection3.GetShortestLineTo(this, face, tolerance);
    }
}
