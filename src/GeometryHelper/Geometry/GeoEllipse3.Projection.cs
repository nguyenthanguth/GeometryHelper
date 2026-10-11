using System;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// The point of an ellipse in space nearest another shape.
    /// </summary>
    public readonly partial struct GeoEllipse3
    {
        /// <summary>
        /// Gets the point on the rim closest to a target point: the point of the rim nearest the target's foot on the plane,
        /// since every point of the rim stands as high above the target's foot as the target does.
        /// </summary>
        /// <remarks>
        /// Where two points of the rim are equally near, one is picked, as <see cref="GeoEllipse2"/> picks it: a point
        /// above the centre gives the end of the minor axis at t = 90°, a circle's centre too.
        /// </remarks>
        public GeoPoint3 GetClosestPointOnBoundary(GeoPoint3 point) => Ellipse3.GetClosestPointOnBoundary(this, point);

        /// <summary>
        /// Gets the shortest segment joining this ellipse to a point, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// The ellipse is read as the filled disc its distance reads, so a point above it is joined straight down and
        /// the segment is as long as <see cref="DistanceTo(GeoPoint3)"/>: from the ellipse 300 by 100 about the origin
        /// on the XY plane, the point (0, 0, 50) gets the one from the centre, 50 long, and the point (330, 0, 40) the
        /// one from (300, 0, 0), also 50 long.
        /// </remarks>
        public GeoLine3 GetShortestLineTo(GeoPoint3 point) => Ellipse3.GetShortestLineTo(this, point);

        /// <summary>
        /// Gets the shortest segment joining this ellipse to a point, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The ellipse is read as the filled disc its distance reads, so a point above it is joined straight down and
        /// the segment is as long as <see cref="DistanceTo(GeoPoint3)"/>: from the ellipse 300 by 100 about the origin
        /// on the XY plane, the point (0, 0, 50) gets the one from the centre, 50 long, and the point (330, 0, 40) the
        /// one from (300, 0, 0), also 50 long. The tolerance is taken so that this reads like every other shape; it
        /// changes nothing here.
        /// </remarks>
        public GeoLine3 GetShortestLineTo(GeoPoint3 point, Tolerance tolerance)
            => Ellipse3.GetShortestLineTo(this, point, tolerance);
    }
}
