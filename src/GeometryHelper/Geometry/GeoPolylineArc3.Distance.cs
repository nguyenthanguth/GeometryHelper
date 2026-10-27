using System;
using System.Collections.Generic;
using GeometryHelper.Core;
using GeometryHelper.Enums;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// How far a curved chain is from another shape, and how deep inside one it sits.
    /// </summary>
    public sealed partial class GeoPolylineArc3
    {
        /// <summary>
        /// Gets the distance from the chain to a point.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        /// <remarks>
        /// The distance to a bend is the exact one, for a point near the centre of its arc too; see
        /// <see cref="DistanceTo(GeoPoint3, Tolerance)"/>.
        /// </remarks>
        public double DistanceTo(GeoPoint3 point) => DistanceTo(point, Tolerance.Global);

        /// <summary>
        /// Gets the distance from the chain to a point, within a tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        /// <remarks>
        /// Each edge is measured as <see cref="GeoEdge3.DistanceTo(GeoPoint3, Tolerance)"/> measures it, so the tolerance
        /// changes nothing in the distance to a bend: a point near the centre of its arc is not carried to the bend's start.
        /// At a tolerance of a thousandth, the chain from (0.01, 0, 0) to (0.001, 0, 0), round the half circle of radius
        /// 0.001 about the origin through (0, 0.001, 0) to (-0.001, 0, 0), and on to (-0.01, 0, 0), is 0.0003 from
        /// (0, 0.0007, 0), and the point is on it; the projection <see cref="GetClosestPointOnBoundary(GeoPoint3, Tolerance)"/>
        /// keeps carrying it to the bend's start, 0.00122 away.
        /// </remarks>
        public double DistanceTo(GeoPoint3 point, Tolerance tolerance) => ArcChain3.DistanceTo(ArcChain3.EdgesOf(this), point, tolerance);

        /// <summary>
        /// Gets the point of the chain its distance to a point is measured to, a point near the centre of a bend not carried
        /// to the bend's start (see <see cref="GeoEdge3.GetMeasuredPoint(GeoPoint3, Tolerance)"/>).
        /// </summary>
        internal GeoPoint3 GetMeasuredPoint(GeoPoint3 point, Tolerance tolerance)
            => ArcChain3.MeasuredPoint(ArcChain3.EdgesOf(this), point, tolerance);
    }
}
