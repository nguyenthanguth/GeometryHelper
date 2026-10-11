using System;
using System.Collections.Generic;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// The point of a polyline nearest another shape, the shortest segment joining the two, and which
    /// edge of it faces one.
    /// </summary>
    public sealed partial class GeoPolyline3
    {
        /// <summary>
        /// Gets the point on this polyline closest to a target point.
        /// </summary>
        public GeoPoint3 GetClosestPointOnBoundary(GeoPoint3 point) => Projection3.ProjectToPolyline(this, point);

        /// <summary>
        /// Gets the shortest line from this chain to the boundary of a curved loop in the same plane:
        /// <see cref="GeoPolygonArc3.GetShortestLineTo(GeoPolyline3)"/> turned round, so that it leaves this chain and
        /// lands on the loop.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        /// <exception cref="ArgumentException">Thrown when this chain does not lie in the loop's plane.</exception>
        public GeoLine3 GetShortestLineTo(GeoPolygonArc3 other)
            => (other ?? throw new ArgumentNullException(nameof(other))).GetShortestLineTo(this).Reverse();

        /// <summary>
        /// Gets the shortest line from this chain to the boundary of a curved loop in the same plane, within a
        /// tolerance: <see cref="GeoPolygonArc3.GetShortestLineTo(GeoPolyline3, Tolerance)"/> turned round, so that it
        /// leaves this chain and lands on the loop.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the loop is null.</exception>
        /// <exception cref="ArgumentException">Thrown when this chain does not lie in the loop's plane.</exception>
        public GeoLine3 GetShortestLineTo(GeoPolygonArc3 other, Tolerance tolerance)
            => (other ?? throw new ArgumentNullException(nameof(other))).GetShortestLineTo(this, tolerance).Reverse();

        /// <summary>
        /// Gets the shortest segment joining this chain to a point, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// The segment leaves the nearest point of the chain, the first of them where two edges are equally near, so it
        /// is as long as <see cref="DistanceTo(GeoPoint3)"/>: from the chain (0, 0, 0), (100, 0, 0), (100, 100, 0), the
        /// point (50, 30, 40) gets the one from (50, 0, 0), 50 long.
        /// </remarks>
        public GeoLine3 GetShortestLineTo(GeoPoint3 point) => Projection3.GetShortestLineTo(this, point);

        /// <summary>
        /// Gets the shortest segment joining this chain to a point, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The segment leaves the nearest point of the chain, the first of them where two edges are equally near, so it
        /// is as long as <see cref="DistanceTo(GeoPoint3)"/>: from the chain (0, 0, 0), (100, 0, 0), (100, 100, 0), the
        /// point (50, 30, 40) gets the one from (50, 0, 0), 50 long.
        /// </remarks>
        public GeoLine3 GetShortestLineTo(GeoPoint3 point, Tolerance tolerance)
            => Projection3.GetShortestLineTo(this, point, tolerance);

        /// <summary>
        /// Gets the shortest segment joining this chain to a segment, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// The segment leaves this chain and lands on the segment, of no length where they meet, and a tie goes to the
        /// first edge of this chain. The chain (0, 0, 0), (100, 0, 0), (100, 100, 0) is joined to the segment from (60,
        /// 50, 30) to (60, 50, 80) by the one from (100, 50, 0) to (60, 50, 30), 50 long, as
        /// <see cref="DistanceTo(GeoLine3)"/> measures it.
        /// </remarks>
        public GeoLine3 GetShortestLineTo(GeoLine3 line) => Projection3.GetShortestLineTo(this, line);

        /// <summary>
        /// Gets the shortest segment joining this chain to a segment, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The segment leaves this chain and lands on the segment, of no length where they meet, and a tie goes to the
        /// first edge of this chain. The chain (0, 0, 0), (100, 0, 0), (100, 100, 0) is joined to the segment from (60,
        /// 50, 30) to (60, 50, 80) by the one from (100, 50, 0) to (60, 50, 30), 50 long, as
        /// <see cref="DistanceTo(GeoLine3)"/> measures it.
        /// </remarks>
        public GeoLine3 GetShortestLineTo(GeoLine3 line, Tolerance tolerance)
            => Projection3.GetShortestLineTo(this, line, tolerance);

        /// <summary>
        /// Gets the shortest segment joining this chain to another chain, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// The segment leaves this chain and lands on the other, of no length where they meet. A tie goes to the first
        /// edge of this chain, and within it to the first edge of the other. The chain (0, 0, 0), (100, 0, 0) is joined
        /// to the chain (50, 30, 40), (50, 30, 100) by the segment from (50, 0, 0) to (50, 30, 40), 50 long, as
        /// <see cref="DistanceTo(GeoPolyline3)"/> measures it.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the other chain is null.</exception>
        public GeoLine3 GetShortestLineTo(GeoPolyline3 other) => Projection3.GetShortestLineTo(this, other);

        /// <summary>
        /// Gets the shortest segment joining this chain to another chain, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The segment leaves this chain and lands on the other, of no length where they meet. A tie goes to the first
        /// edge of this chain, and within it to the first edge of the other. The chain (0, 0, 0), (100, 0, 0) is joined
        /// to the chain (50, 30, 40), (50, 30, 100) by the segment from (50, 0, 0) to (50, 30, 40), 50 long, as
        /// <see cref="DistanceTo(GeoPolyline3)"/> measures it.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the other chain is null.</exception>
        public GeoLine3 GetShortestLineTo(GeoPolyline3 other, Tolerance tolerance)
            => Projection3.GetShortestLineTo(this, other, tolerance);

        /// <summary>
        /// Gets the shortest segment joining this chain to a triangle, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// The triangle is read as a filled surface, as its distance reads it, and a tie goes to the first edge of this
        /// chain. The chain (20, 20, 30), (20, 20, 80) is joined to the triangle (0, 0, 0), (100, 0, 0), (0, 100, 0)
        /// from (20, 20, 30) straight down, 30 long, as <see cref="DistanceTo(GeoTriangle3)"/> measures it.
        /// </remarks>
        public GeoLine3 GetShortestLineTo(GeoTriangle3 triangle) => Projection3.GetShortestLineTo(this, triangle);

        /// <summary>
        /// Gets the shortest segment joining this chain to a triangle, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The triangle is read as a filled surface, as its distance reads it, and a tie goes to the first edge of this
        /// chain. The chain (20, 20, 30), (20, 20, 80) is joined to the triangle (0, 0, 0), (100, 0, 0), (0, 100, 0)
        /// from (20, 20, 30) straight down, 30 long, as <see cref="DistanceTo(GeoTriangle3)"/> measures it.
        /// </remarks>
        public GeoLine3 GetShortestLineTo(GeoTriangle3 triangle, Tolerance tolerance)
            => Projection3.GetShortestLineTo(this, triangle, tolerance);

        /// <summary>
        /// Gets the shortest segment joining this chain to a polygon, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// The polygon is read as a filled surface, as its distance reads it, and a tie goes to the first edge of this
        /// chain. Over the square 100 by 100 on the XY plane with a corner at the origin, the chain (50, 50, 30), (50,
        /// 50, 80), (150, 50, 80) is joined from (50, 50, 30) straight down, 30 long, as
        /// <see cref="DistanceTo(GeoPolygon3)"/> measures it.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public GeoLine3 GetShortestLineTo(GeoPolygon3 polygon) => Projection3.GetShortestLineTo(this, polygon);

        /// <summary>
        /// Gets the shortest segment joining this chain to a polygon, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The polygon is read as a filled surface, as its distance reads it, and a tie goes to the first edge of this
        /// chain. Over the square 100 by 100 on the XY plane with a corner at the origin, the chain (50, 50, 30), (50,
        /// 50, 80), (150, 50, 80) is joined from (50, 50, 30) straight down, 30 long, as
        /// <see cref="DistanceTo(GeoPolygon3)"/> measures it.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public GeoLine3 GetShortestLineTo(GeoPolygon3 polygon, Tolerance tolerance)
            => Projection3.GetShortestLineTo(this, polygon, tolerance);

        /// <summary>
        /// Gets the shortest segment joining this chain to a plane, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// A chain that crosses or touches the plane meets it in a segment of no length at the first point of contact,
        /// walking its edges in order; one clear of it is joined from its nearest vertex, the first where several are
        /// as near. The chain (0, 0, 10), (50, 0, 30), (100, 0, -10) meets the XY plane at (87.5, 0, 0); the chain (0,
        /// 0, 10), (50, 0, 30) is joined from (0, 0, 10) to the origin, 10 long, as <see cref="DistanceTo(GeoPlane3)"/>
        /// measures it. The segment is never snapped: a shape a hair off the plane is joined by a segment that short,
        /// though the distance, read within the point tolerance, is nought.
        /// </remarks>
        public GeoLine3 GetShortestLineTo(GeoPlane3 plane) => Projection3.GetShortestLineTo(this, plane);

        /// <summary>
        /// Gets the shortest segment joining this chain to a plane, within a tolerance.
        /// </summary>
        /// <remarks>
        /// A chain that crosses or touches the plane meets it in a segment of no length at the first point of contact,
        /// walking its edges in order; one clear of it is joined from its nearest vertex, the first where several are
        /// as near. The chain (0, 0, 10), (50, 0, 30), (100, 0, -10) meets the XY plane at (87.5, 0, 0); the chain (0,
        /// 0, 10), (50, 0, 30) is joined from (0, 0, 10) to the origin, 10 long, as <see cref="DistanceTo(GeoPlane3)"/>
        /// measures it. The segment is never snapped: a shape a hair off the plane is joined by a segment that short,
        /// though the distance, read within the point tolerance, is nought.
        /// </remarks>
        public GeoLine3 GetShortestLineTo(GeoPlane3 plane, Tolerance tolerance)
            => Projection3.GetShortestLineTo(this, plane, tolerance);

        /// <summary>
        /// Gets the shortest segment joining this chain to a solid:
        /// <see cref="GeoSolid3.GetShortestLineTo(GeoPolyline3)"/> turned round, so that it leaves this chain and lands
        /// on the solid.
        /// </summary>
        /// <remarks>
        /// The segment runs from this chain to the surface of the body, so a chain lying wholly inside is joined out to
        /// the nearest face, where <see cref="DistanceTo(GeoSolid3)"/> answers nought; outside, it is as long as that
        /// distance. In the box 100 by 100 by 100 with a corner at the origin, the chain (50, 50, 10), (50, 50, 60),
        /// (50, 70, 60) is joined from (50, 50, 10) to the bottom face, 10 long.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the solid is null.</exception>
        public GeoLine3 GetShortestLineTo(GeoSolid3 solid) => Projection3.GetShortestLineTo(solid, this).Reverse();

        /// <summary>
        /// Gets the shortest segment joining this chain to a solid, within a tolerance:
        /// <see cref="GeoSolid3.GetShortestLineTo(GeoPolyline3, Tolerance)"/> turned round, so that it leaves this
        /// chain and lands on the solid.
        /// </summary>
        /// <remarks>
        /// The segment runs from this chain to the surface of the body, so a chain lying wholly inside is joined out to
        /// the nearest face, where <see cref="DistanceTo(GeoSolid3)"/> answers nought; outside, it is as long as that
        /// distance. In the box 100 by 100 by 100 with a corner at the origin, the chain (50, 50, 10), (50, 50, 60),
        /// (50, 70, 60) is joined from (50, 50, 10) to the bottom face, 10 long.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the solid is null.</exception>
        public GeoLine3 GetShortestLineTo(GeoSolid3 solid, Tolerance tolerance)
            => Projection3.GetShortestLineTo(solid, this, tolerance).Reverse();
    }
}
