using System;
using GeometryHelper.Enums;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// How far an edge is from another shape, and how deep inside one it sits.
    /// </summary>
    public readonly partial struct GeoEdge3
    {
        /// <summary>
        /// Gets the distance from the edge to a point.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        /// <remarks>
        /// The distance to a bend is the exact one, for a point near the centre of its arc too; see
        /// <see cref="DistanceTo(GeoPoint3, Tolerance)"/>.
        /// </remarks>
        public double DistanceTo(GeoPoint3 point) => DistanceTo(point, Tolerance.Global);

        /// <summary>
        /// Gets the distance from the edge to a point, within a tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        /// <remarks>
        /// The tolerance changes nothing in the distance to a bend: a point at the centre of its arc is as far from it as any
        /// other, and a point near the centre is not carried to the bend's start, as <see cref="GeoArc3.DistanceTo(GeoPoint3, Tolerance)"/>
        /// measures it. At a tolerance of a thousandth, the half circle of radius 0.001 about the origin in the plan, from
        /// (0.001, 0, 0) through (0, 0.001, 0) to (-0.001, 0, 0), is 0.0003 from (0, 0.0007, 0) as an edge, and the point is on
        /// it; the projection <see cref="GetClosestPointOnBoundary(GeoPoint3, Tolerance)"/> keeps carrying it to the start,
        /// 0.00122 away. A straight edge shorter than the tolerance is read as its start point.
        /// </remarks>
        public double DistanceTo(GeoPoint3 point, Tolerance tolerance) => point.DistanceTo(GetMeasuredPoint(point, tolerance));

        /// <summary>
        /// Gets the point of the edge its distance to a point is measured to: on a bend the nearest point found at a tolerance
        /// of nought, so a point near the centre of the arc is not carried to its start, and on a straight edge the one
        /// <see cref="GetClosestPointOnBoundary(GeoPoint3, Tolerance)"/> gives.
        /// </summary>
        internal GeoPoint3 GetMeasuredPoint(GeoPoint3 point, Tolerance tolerance)
        {
            return IsArc
                ? ToArc().GetClosestPointOnBoundary(point, Exactly)
                : GetPointAtParameter(GetParameterAtPoint(point, tolerance));
        }

        /// <summary>
        /// Gets how far along the edge the point its distance to a point is measured to sits, from nought to one: on a bend
        /// found at a tolerance of nought, as <see cref="GetMeasuredPoint(GeoPoint3, Tolerance)"/> finds the point, and on a
        /// straight edge as <see cref="GetParameterAtPoint(GeoPoint3, Tolerance)"/> gives it.
        /// </summary>
        internal double GetMeasuredParameter(GeoPoint3 point, Tolerance tolerance)
            => IsArc ? ToArc().GetParameterAtPoint(point, Exactly) : GetParameterAtPoint(point, tolerance);

        /// <summary>
        /// The tolerance of nought, at which only a point exactly at the centre of a bend is carried to its start.
        /// </summary>
        private static Tolerance Exactly => default(Tolerance);
    }
}
