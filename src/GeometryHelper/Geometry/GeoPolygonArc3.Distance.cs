using System;
using System.Collections.Generic;
using GeometryHelper.Core;
using GeometryHelper.Enums;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// How far a curved loop is from another shape, and how deep inside one it sits.
    /// </summary>
    public sealed partial class GeoPolygonArc3
    {
        /// <summary>
        /// Gets the distance from the outline of the loop to a point.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        /// <remarks>
        /// Measured to the outline, so a point inside the loop is not nought away. That is what
        /// <see cref="Contains(GeoPoint3)"/> is for.
        /// </remarks>
        public double DistanceTo(GeoPoint3 point) => DistanceTo(point, Tolerance.Global);

        /// <summary>
        /// Gets the distance from the outline of the loop to a point, within a tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        /// <remarks>
        /// Each edge is measured as <see cref="GeoEdge3.DistanceTo(GeoPoint3, Tolerance)"/> measures it, so the tolerance
        /// changes nothing in the distance to a bend: a point near the centre of its arc is not carried to the bend's start,
        /// and the line <see cref="GetShortestLineTo(GeoPoint3, Tolerance)"/> gives can be longer than the distance there.
        /// </remarks>
        public double DistanceTo(GeoPoint3 point, Tolerance tolerance) => ArcChain3.DistanceTo(Edges(), point, tolerance);

        /// <summary>
        /// Gets the point of the outline its distance to a point is measured to, a point near the centre of a bend not
        /// carried to the bend's start (see <see cref="GeoEdge3.GetMeasuredPoint(GeoPoint3, Tolerance)"/>).
        /// </summary>
        internal GeoPoint3 GetMeasuredPoint(GeoPoint3 point, Tolerance tolerance) => ArcChain3.MeasuredPoint(Edges(), point, tolerance);

        /// <summary>
        /// Gets how far a point is from the boundary of the loop, negative inside it.
        /// </summary>
        public double SignedDistanceTo(GeoPoint3 point) => SignedDistanceTo(point, Tolerance.Global);

        /// <summary>
        /// Gets how far a point is from the boundary of the loop, negative inside it, within a tolerance.
        /// </summary>
        /// <param name="point">The point.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// The distance out to the loop, negative where the loop encloses the point. The whole definition is
        /// tied to <see cref="Locate(GeoPoint3, Tolerance)"/>, so a point off the loop's plane is positive: the
        /// loop calls it OutSide, because a flat region encloses nothing above itself.
        /// </returns>
        public double SignedDistanceTo(GeoPoint3 point, Tolerance tolerance)
        {
            GeoCoordinateSystem3 frame = GetFrame();

            if (Math.Abs(frame.ToLocal(point).Z) > tolerance.EqualPlanar)
            {
                return DistanceTo(point, tolerance);
            }

            return ProjectToPolygonArc2(frame).SignedDistanceTo(PlanarMap.ProjectToPoint2(frame, point), tolerance);
        }

    }
}
