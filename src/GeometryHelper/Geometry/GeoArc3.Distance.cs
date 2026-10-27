using System;
using GeometryHelper.Enums;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// How far an arc is from another shape, and how deep inside one it sits.
    /// </summary>
    public readonly partial struct GeoArc3
    {
        /// <summary>
        /// Gets the distance from this arc to a point.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        /// <remarks>
        /// The distance is the exact one, for a point near the centre of the arc too; see
        /// <see cref="DistanceTo(GeoPoint3, Tolerance)"/>.
        /// </remarks>
        public double DistanceTo(GeoPoint3 point) => point.DistanceTo(GetClosestPointOnBoundary(point, Exactly));

        /// <summary>
        /// Gets the distance from this arc to a point, within a tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        /// <remarks>
        /// The tolerance changes nothing in the distance: a point at the centre of the arc is as far from it as any other,
        /// and a point near the centre is not carried to the arc's start. At a tolerance of a thousandth, the half circle of
        /// radius 0.001 about the origin in the plan, from (0.001, 0, 0) through (0, 0.001, 0) to (-0.001, 0, 0), is 0.0003
        /// from (0, 0.0007, 0), and the point is on it; the projection
        /// <see cref="GetClosestPointOnBoundary(GeoPoint3, Tolerance)"/> keeps carrying it to the start, 0.00122 away.
        /// </remarks>
        public double DistanceTo(GeoPoint3 point, Tolerance tolerance) => point.DistanceTo(GetClosestPointOnBoundary(point, Exactly));

        /// <summary>
        /// The tolerance of nought, at which only a point exactly at the centre is carried to the arc's start.
        /// </summary>
        private static Tolerance Exactly => default(Tolerance);
    }
}
