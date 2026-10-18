using System;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// How far an ellipse in space is from another shape.
    /// </summary>
    public readonly partial struct GeoEllipse3
    {
        /// <summary>
        /// Calculates the shortest distance from this disc to a point.
        /// </summary>
        /// <returns>Nought only where the point lies in the disc, otherwise the gap to it, measured exactly.</returns>
        /// <remarks>
        /// The disc counts as a filled surface, as a circle's does, so a point 50 above the centre of an ellipse 300 by 100
        /// is 50 from it, measured straight down to the surface rather than out to the rim; a point beyond the rim is
        /// measured to the nearest point of the rim.
        /// </remarks>
        public double DistanceTo(GeoPoint3 point) => Ellipse3.DistanceTo(this, point);
    }
}
