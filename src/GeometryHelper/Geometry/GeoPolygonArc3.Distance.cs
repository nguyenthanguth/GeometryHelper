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
        /// <remarks>
        /// Measured to the outline, so a point inside the loop is not nought away. That is what
        /// <see cref="Contains(GeoPoint3)"/> is for.
        /// </remarks>
        public double DistanceTo(GeoPoint3 point) => DistanceTo(point, Tolerance.Global);

        /// <summary>
        /// Gets the distance from the outline of the loop to a point, within a tolerance.
        /// </summary>
        public double DistanceTo(GeoPoint3 point, Tolerance tolerance) => point.DistanceTo(GetClosestPointOnBoundary(point, tolerance));
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
