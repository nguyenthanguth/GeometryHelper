using System;
using System.Collections.Generic;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// How far a face is from another shape, and how deep inside one it sits.
    /// </summary>
    public sealed partial class GeoFace2
    {
        /// <summary>
        /// Calculates the shortest distance from this face to a point.
        /// </summary>
        /// <remarks>
        /// The face is read as filled, so a point on the material is nought away and a point in one of
        /// its holes is measured to the rim it sits in.
        /// </remarks>
        public double DistanceTo(GeoPoint2 point) => Distance2.DistanceTo(this, point);

        /// <summary>
        /// Calculates the shortest distance from this face to a point, within a tolerance.
        /// </summary>
        public double DistanceTo(GeoPoint2 point, Tolerance tolerance) => Distance2.DistanceTo(this, point, tolerance);

        /// <summary>
        /// Calculates the distance from this face to a point, negative for a point on the material.
        /// </summary>
        /// <remarks>
        /// The magnitude is the distance to the boundary, whichever side of it the point is on, and the sign
        /// says which side: negative on the material, nought on an edge, positive off it.
        /// <see cref="DistanceTo(GeoPoint2)"/> reads the face as filled and so answers nothing at all for a
        /// point on the material, which is the one place the two part company. The boundary of a face is its
        /// outline and the rim of every hole, so a point in a hole is off the material.
        /// </remarks>
        public double SignedDistanceTo(GeoPoint2 point) => Distance2.SignedDistanceTo(this, point);

        /// <summary>
        /// Calculates the distance from this face to a point, negative for a point on the material, within a tolerance.
        /// </summary>
        public double SignedDistanceTo(GeoPoint2 point, Tolerance tolerance) => Distance2.SignedDistanceTo(this, point, tolerance);
    }
}
