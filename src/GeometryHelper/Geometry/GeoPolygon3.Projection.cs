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
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the solid is null.</exception>
        public GeoLine3 GetShortestLineTo(GeoSolid3 solid, Tolerance tolerance)
            => Projection3.GetShortestLineTo(solid, this, tolerance).Reverse();
    }
}
