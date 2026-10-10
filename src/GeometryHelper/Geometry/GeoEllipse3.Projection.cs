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
    }
}
