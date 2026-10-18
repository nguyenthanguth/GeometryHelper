using System;
using System.Collections.Generic;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// How far a solid is from another shape, and how deep inside one it sits.
    /// </summary>
    public sealed partial class GeoSolid3
    {
        /// <summary>
        /// Calculates the shortest distance from this solid to a point. A point inside is at distance zero.
        /// </summary>
        /// <remarks>
        /// Whether a point is in the body is asked to within rounding, a billionth of a millimetre (1E-9, not nought),
        /// so that rounding in a body turned about in space cannot lose a crossing: a point within 1E-9 outside the
        /// body reads as in it, and a point 5E-10 outside a box is nought away from it.
        /// </remarks>
        public double DistanceTo(GeoPoint3 point) => Distance3.DistanceTo(this, point);

        /// <summary>
        /// Calculates the distance from this solid to a point, negative for a point in the material.
        /// </summary>
        /// <remarks>
        /// The magnitude is the distance to the surface, whichever side of it the point is on, and the sign
        /// says which side: negative inside, nought on it, positive outside. <see cref="DistanceTo(GeoPoint3)"/>
        /// reads this solid as filled and so answers nothing at all for a point inside, which is the one
        /// place the two part company.
        /// </remarks>
        public double SignedDistanceTo(GeoPoint3 point) => Distance3.SignedDistanceTo(this, point);

        /// <summary>
        /// Calculates the distance from this solid to a point, negative for a point in the material, within a tolerance.
        /// </summary>
        public double SignedDistanceTo(GeoPoint3 point, Tolerance tolerance) => Distance3.SignedDistanceTo(this, point, tolerance);

        /// <summary>
        /// Calculates the shortest distance from this solid to a point, within a tolerance.
        /// </summary>
        /// <remarks>
        /// A point inside the body is at distance zero.
        /// Whether a point is in the body is asked to within rounding, a billionth of a millimetre (1E-9, not nought),
        /// so that rounding in a body turned about in space cannot lose a crossing: a point within 1E-9 outside the
        /// body reads as in it, and a point 5E-10 outside a box is nought away from it.
        /// </remarks>
        public double DistanceTo(GeoPoint3 point, Tolerance tolerance) => Distance3.DistanceTo(this, point, tolerance);

        /// <summary>
        /// Calculates the shortest distance from this solid to a line segment.
        /// </summary>
        /// <remarks>
        /// Whether a point is in the body is asked to within rounding, a billionth of a millimetre (1E-9, not nought),
        /// so that rounding in a body turned about in space cannot lose a crossing: a point within 1E-9 outside the
        /// body reads as in it, and a point 5E-10 outside a box is nought away from it.
        /// </remarks>
        public double DistanceTo(GeoLine3 line) => Distance3.DistanceTo(line, this);

        /// <summary>
        /// Calculates the shortest distance from this solid to a line segment, within a tolerance.
        /// </summary>
        /// <remarks>
        /// A segment reaching into the body is at distance zero.
        /// Whether a point is in the body is asked to within rounding, a billionth of a millimetre (1E-9, not nought),
        /// so that rounding in a body turned about in space cannot lose a crossing: a point within 1E-9 outside the
        /// body reads as in it, and a point 5E-10 outside a box is nought away from it.
        /// </remarks>
        public double DistanceTo(GeoLine3 line, Tolerance tolerance) => Distance3.DistanceTo(line, this, tolerance);

        /// <summary>
        /// Calculates the shortest distance from this solid to another solid.
        /// </summary>
        public double DistanceTo(GeoSolid3 other) => Distance3.DistanceTo(this, other);

        /// <summary>
        /// Calculates the shortest distance from this solid to another solid, within a tolerance.
        /// </summary>
        /// <remarks>
        /// Bodies that touch or overlap are at distance zero, which includes one sitting wholly inside the
        /// other without their surfaces meeting.
        /// </remarks>
        public double DistanceTo(GeoSolid3 other, Tolerance tolerance) => Distance3.DistanceTo(this, other, tolerance);

        /// <summary>
        /// Calculates the shortest distance from this solid to a triangle.
        /// </summary>
        /// <remarks>
        /// Whether a point is in the body is asked to within rounding, a billionth of a millimetre (1E-9, not nought),
        /// so that rounding in a body turned about in space cannot lose a crossing: a point within 1E-9 outside the
        /// body reads as in it, and a point 5E-10 outside a box is nought away from it.
        /// </remarks>
        public double DistanceTo(GeoTriangle3 triangle) => Distance3.DistanceTo(this, triangle);

        /// <summary>
        /// Calculates the shortest distance from this solid to a triangle, within a tolerance.
        /// </summary>
        /// <remarks>
        /// Whether a point is in the body is asked to within rounding, a billionth of a millimetre (1E-9, not nought),
        /// so that rounding in a body turned about in space cannot lose a crossing: a point within 1E-9 outside the
        /// body reads as in it, and a point 5E-10 outside a box is nought away from it.
        /// </remarks>
        public double DistanceTo(GeoTriangle3 triangle, Tolerance tolerance) => Distance3.DistanceTo(this, triangle, tolerance);

        /// <summary>
        /// Calculates the shortest distance from this solid to a polygon.
        /// </summary>
        /// <remarks>
        /// Whether a point is in the body is asked to within rounding, a billionth of a millimetre (1E-9, not nought),
        /// so that rounding in a body turned about in space cannot lose a crossing: a point within 1E-9 outside the
        /// body reads as in it, and a point 5E-10 outside a box is nought away from it.
        /// </remarks>
        public double DistanceTo(GeoPolygon3 polygon) => Distance3.DistanceTo(this, polygon);

        /// <summary>
        /// Calculates the shortest distance from this solid to a polygon, within a tolerance.
        /// </summary>
        /// <remarks>
        /// Whether a point is in the body is asked to within rounding, a billionth of a millimetre (1E-9, not nought),
        /// so that rounding in a body turned about in space cannot lose a crossing: a point within 1E-9 outside the
        /// body reads as in it, and a point 5E-10 outside a box is nought away from it.
        /// </remarks>
        public double DistanceTo(GeoPolygon3 polygon, Tolerance tolerance) => Distance3.DistanceTo(this, polygon, tolerance);

        /// <summary>
        /// Calculates the shortest distance from this solid to a polyline.
        /// </summary>
        /// <remarks>
        /// Whether a point is in the body is asked to within rounding, a billionth of a millimetre (1E-9, not nought),
        /// so that rounding in a body turned about in space cannot lose a crossing: a point within 1E-9 outside the
        /// body reads as in it, and a point 5E-10 outside a box is nought away from it.
        /// </remarks>
        public double DistanceTo(GeoPolyline3 polyline) => Distance3.DistanceTo(this, polyline);

        /// <summary>
        /// Calculates the shortest distance from this solid to a polyline, within a tolerance.
        /// </summary>
        /// <remarks>
        /// Whether a point is in the body is asked to within rounding, a billionth of a millimetre (1E-9, not nought),
        /// so that rounding in a body turned about in space cannot lose a crossing: a point within 1E-9 outside the
        /// body reads as in it, and a point 5E-10 outside a box is nought away from it.
        /// </remarks>
        public double DistanceTo(GeoPolyline3 polyline, Tolerance tolerance) => Distance3.DistanceTo(this, polyline, tolerance);

        /// <summary>
        /// Calculates the shortest distance from this solid to a plane.
        /// </summary>
        /// <remarks>
        /// A body reaching the plane or crossing it is at distance zero.
        /// </remarks>
        public double DistanceTo(GeoPlane3 plane) => Distance3.DistanceTo(this, plane);

        /// <summary>
        /// Calculates the shortest distance from this solid to a plane, within a tolerance.
        /// </summary>
        public double DistanceTo(GeoPlane3 plane, Tolerance tolerance) => Distance3.DistanceTo(this, plane, tolerance);

        /// <summary>
        /// Calculates the shortest distance from this solid to an oriented box.
        /// </summary>
        public double DistanceTo(GeoObb3 box) => Distance3.DistanceTo(this, box);

        /// <summary>
        /// Calculates the shortest distance from this solid to an oriented box, within a tolerance.
        /// </summary>
        public double DistanceTo(GeoObb3 box, Tolerance tolerance) => Distance3.DistanceTo(this, box, tolerance);

        /// <summary>
        /// Calculates the shortest distance from this solid to an axis-aligned box.
        /// </summary>
        public double DistanceTo(GeoAabb3 box) => Distance3.DistanceTo(this, box);

        /// <summary>
        /// Calculates the shortest distance from this solid to an axis-aligned box, within a tolerance.
        /// </summary>
        public double DistanceTo(GeoAabb3 box, Tolerance tolerance) => Distance3.DistanceTo(this, box, tolerance);

        /// <summary>
        /// Calculates the shortest distance from this solid to a ray.
        /// </summary>
        /// <remarks>
        /// A ray starting inside the body, or running into it, is at distance zero.
        /// Whether a point is in the body is asked to within rounding, a billionth of a millimetre (1E-9, not nought),
        /// so that rounding in a body turned about in space cannot lose a crossing: a point within 1E-9 outside the
        /// body reads as in it, and a point 5E-10 outside a box is nought away from it.
        /// </remarks>
        public double DistanceTo(GeoRay3 ray) => Distance3.DistanceTo(ray, this);

        /// <summary>
        /// Calculates the shortest distance from this solid to a ray, within a tolerance.
        /// </summary>
        /// <remarks>
        /// Whether a point is in the body is asked to within rounding, a billionth of a millimetre (1E-9, not nought),
        /// so that rounding in a body turned about in space cannot lose a crossing: a point within 1E-9 outside the
        /// body reads as in it, and a point 5E-10 outside a box is nought away from it.
        /// </remarks>
        public double DistanceTo(GeoRay3 ray, Tolerance tolerance) => Distance3.DistanceTo(ray, this, tolerance);

        /// <summary>
        /// Calculates the shortest distance between this solid and a face: the answer of
        /// <see cref="Distance3.DistanceTo(GeoSolid3, GeoFace3)"/>.
        /// </summary>
        /// <remarks>
        /// The face is read by its material and the body solid through. The box 100 by 100 by 100 with a corner at the
        /// origin passes through the hole of the square 200 by 200 from (-50, -50, 50) to (150, 150, 50) with a hole 150 by
        /// 150 in its middle and is 25 from its rim; the same square without its hole cuts the box and is nought from it.
        /// Whether a point is in the body is asked to within rounding, a billionth of a millimetre (1E-9, not nought),
        /// so that rounding in a body turned about in space cannot lose a crossing: a point within 1E-9 outside the
        /// body reads as in it, and a point 5E-10 outside a box is nought away from it.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public double DistanceTo(GeoFace3 face) => Distance3.DistanceTo(this, face);

        /// <summary>
        /// Calculates the shortest distance between this solid and a face, within a tolerance: the answer of
        /// <see cref="Distance3.DistanceTo(GeoSolid3, GeoFace3, Tolerance)"/>.
        /// </summary>
        /// <remarks>
        /// The face is read by its material and the body solid through. The box 100 by 100 by 100 with a corner at the
        /// origin passes through the hole of the square 200 by 200 from (-50, -50, 50) to (150, 150, 50) with a hole 150 by
        /// 150 in its middle and is 25 from its rim; the same square without its hole cuts the box and is nought from it.
        /// Whether a point is in the body is asked to within rounding, a billionth of a millimetre (1E-9, not nought),
        /// so that rounding in a body turned about in space cannot lose a crossing: a point within 1E-9 outside the
        /// body reads as in it, and a point 5E-10 outside a box is nought away from it.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public double DistanceTo(GeoFace3 face, Tolerance tolerance) => Distance3.DistanceTo(this, face, tolerance);
    }
}
