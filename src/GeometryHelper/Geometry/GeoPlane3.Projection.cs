using System;
using System.Collections.Generic;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// The point of a plane nearest another shape, the shortest segment joining the two, and which
    /// edge of it faces one.
    /// </summary>
    public readonly partial struct GeoPlane3
    {
        /// <summary>
        /// Gets the point of this plane nearest a target point.
        /// </summary>
        /// <remarks>
        /// A plane is endless, so this is the foot of the perpendicular and never anything else. The sign of
        /// which side the point was on is in <see cref="SignedDistanceTo(GeoPoint3)"/>.
        /// </remarks>
        public GeoPoint3 GetClosestPointOnBoundary(GeoPoint3 point) => Projection3.ProjectToPlane(this, point);

        /// <summary>
        /// Gets the point of this plane nearest a target point, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The tolerance changes nothing here; it is taken so that this reads like every other shape.
        /// </remarks>
        public GeoPoint3 GetClosestPointOnBoundary(GeoPoint3 point, Tolerance tolerance) => Projection3.ProjectToPlane(this, point);

        /// <summary>
        /// Gets the shortest segment joining this plane to a point, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// The perpendicular from the foot of the point, as long as <see cref="DistanceTo(GeoPoint3)"/>: from the XY
        /// plane, the point (10, 20, 30) gets the one from (10, 20, 0), 30 long.
        /// </remarks>
        public GeoLine3 GetShortestLineTo(GeoPoint3 point) => Projection3.GetShortestLineTo(this, point);

        /// <summary>
        /// Gets the shortest segment joining this plane to a point, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The perpendicular from the foot of the point, as long as <see cref="DistanceTo(GeoPoint3)"/>: from the XY
        /// plane, the point (10, 20, 30) gets the one from (10, 20, 0), 30 long. The tolerance is taken so that this
        /// reads like every other shape; it changes nothing here.
        /// </remarks>
        public GeoLine3 GetShortestLineTo(GeoPoint3 point, Tolerance tolerance)
            => Projection3.GetShortestLineTo(this, point, tolerance);

        /// <summary>
        /// Gets the shortest segment joining this plane to a segment:
        /// <see cref="GeoLine3.GetShortestLineTo(GeoPlane3)"/> turned round, so that it leaves this plane and lands on
        /// the segment.
        /// </summary>
        /// <remarks>
        /// A segment that crosses or touches this plane meets it in a segment of no length; one clear of it is joined
        /// to its nearer end, the start where both are as near. The XY plane is joined to the segment from (0, 0, 10)
        /// to (50, 0, 30) from the origin to (0, 0, 10), 10 long, as <see cref="DistanceTo(GeoLine3)"/> measures it.
        /// </remarks>
        public GeoLine3 GetShortestLineTo(GeoLine3 line) => Projection3.GetShortestLineTo(line, this).Reverse();

        /// <summary>
        /// Gets the shortest segment joining this plane to a segment, within a tolerance:
        /// <see cref="GeoLine3.GetShortestLineTo(GeoPlane3, Tolerance)"/> turned round, so that it leaves this plane
        /// and lands on the segment.
        /// </summary>
        /// <remarks>
        /// A segment that crosses or touches this plane meets it in a segment of no length; one clear of it is joined
        /// to its nearer end, the start where both are as near. The XY plane is joined to the segment from (0, 0, 10)
        /// to (50, 0, 30) from the origin to (0, 0, 10), 10 long, as <see cref="DistanceTo(GeoLine3)"/> measures it.
        /// The tolerance is taken so that this reads like every other shape; it changes nothing, since the distance
        /// reads a segment against a plane exactly.
        /// </remarks>
        public GeoLine3 GetShortestLineTo(GeoLine3 line, Tolerance tolerance)
            => Projection3.GetShortestLineTo(line, this, tolerance).Reverse();

        /// <summary>
        /// Gets the shortest segment joining this plane to a polyline:
        /// <see cref="GeoPolyline3.GetShortestLineTo(GeoPlane3)"/> turned round, so that it leaves this plane and lands
        /// on the polyline.
        /// </summary>
        /// <remarks>
        /// A chain that crosses or touches this plane meets it in a segment of no length at its first point of contact;
        /// one clear of it is joined to its nearest vertex. The XY plane is joined to the chain (0, 0, 10), (50, 0, 30)
        /// from the origin to (0, 0, 10), 10 long, as <see cref="DistanceTo(GeoPolyline3)"/> measures it. The segment
        /// is never snapped: a shape a hair off the plane is joined by a segment that short, though the distance, read
        /// within the point tolerance, is nought.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the polyline is null.</exception>
        public GeoLine3 GetShortestLineTo(GeoPolyline3 polyline)
            => Projection3.GetShortestLineTo(polyline, this).Reverse();

        /// <summary>
        /// Gets the shortest segment joining this plane to a polyline, within a tolerance:
        /// <see cref="GeoPolyline3.GetShortestLineTo(GeoPlane3, Tolerance)"/> turned round, so that it leaves this
        /// plane and lands on the polyline.
        /// </summary>
        /// <remarks>
        /// A chain that crosses or touches this plane meets it in a segment of no length at its first point of contact;
        /// one clear of it is joined to its nearest vertex. The XY plane is joined to the chain (0, 0, 10), (50, 0, 30)
        /// from the origin to (0, 0, 10), 10 long, as <see cref="DistanceTo(GeoPolyline3)"/> measures it. The segment
        /// is never snapped: a shape a hair off the plane is joined by a segment that short, though the distance, read
        /// within the point tolerance, is nought.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the polyline is null.</exception>
        public GeoLine3 GetShortestLineTo(GeoPolyline3 polyline, Tolerance tolerance)
            => Projection3.GetShortestLineTo(polyline, this, tolerance).Reverse();

        /// <summary>
        /// Gets the shortest segment joining this plane to a solid:
        /// <see cref="GeoSolid3.GetShortestLineTo(GeoPlane3)"/> turned round, so that it leaves this plane and lands on
        /// the solid.
        /// </summary>
        /// <remarks>
        /// A body that crosses or touches this plane meets it in a segment of no length at its first point of contact;
        /// one clear of it is joined to its nearest corner. The plane 30 under the box 100 by 100 by 100 with a corner
        /// at the origin is joined to a corner of its bottom face, 30 long, as <see cref="DistanceTo(GeoSolid3)"/>
        /// measures it. The segment is never snapped: a shape a hair off the plane is joined by a segment that short,
        /// though the distance, read within the point tolerance, is nought.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the solid is null.</exception>
        public GeoLine3 GetShortestLineTo(GeoSolid3 solid) => Projection3.GetShortestLineTo(solid, this).Reverse();

        /// <summary>
        /// Gets the shortest segment joining this plane to a solid, within a tolerance:
        /// <see cref="GeoSolid3.GetShortestLineTo(GeoPlane3, Tolerance)"/> turned round, so that it leaves this plane
        /// and lands on the solid.
        /// </summary>
        /// <remarks>
        /// A body that crosses or touches this plane meets it in a segment of no length at its first point of contact;
        /// one clear of it is joined to its nearest corner. The plane 30 under the box 100 by 100 by 100 with a corner
        /// at the origin is joined to a corner of its bottom face, 30 long, as <see cref="DistanceTo(GeoSolid3)"/>
        /// measures it. The segment is never snapped: a shape a hair off the plane is joined by a segment that short,
        /// though the distance, read within the point tolerance, is nought.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the solid is null.</exception>
        public GeoLine3 GetShortestLineTo(GeoSolid3 solid, Tolerance tolerance)
            => Projection3.GetShortestLineTo(solid, this, tolerance).Reverse();
    }
}
