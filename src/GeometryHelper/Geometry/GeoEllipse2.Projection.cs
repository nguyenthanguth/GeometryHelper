using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// The point of the rim of an ellipse nearest a point.
    /// </summary>
    public readonly partial struct GeoEllipse2
    {
        /// <summary>
        /// Gets the point of the rim of this ellipse nearest a point, including for points inside it. The centre gives the
        /// end of the minor axis at t = 90°, and a point on the major axis inside, which two points of the rim are equally
        /// near, gives the one on the positive side. A circle's centre gives t = 90° too, where
        /// <see cref="GeoCircle2.GetClosestPointOnBoundary(GeoPoint2)"/> gives angle nought.
        /// </summary>
        public GeoPoint2 GetClosestPointOnBoundary(GeoPoint2 point) => Ellipse2.GetClosestPointOnBoundary(this, point);
    }
}
