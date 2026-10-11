using System;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// How far an oriented box is from another shape, and how deep inside one it sits.
    /// </summary>
    public sealed partial class GeoObb3
    {
        /// <summary>
        /// Calculates the shortest distance from this box to a point. A point inside the box is at
        /// distance zero.
        /// </summary>
        public double DistanceTo(GeoPoint3 point) => Distance3.DistanceTo(this, point);

        /// <summary>
        /// Gets the distance from this box to a polyline, using the default tolerance.
        /// </summary>
        public double DistanceTo(GeoPolyline3 polyline) => Distance3.DistanceTo(polyline, this);

        /// <summary>
        /// Gets the distance from this box to a polyline, within a tolerance.
        /// </summary>
        public double DistanceTo(GeoPolyline3 polyline, Tolerance tolerance) => Distance3.DistanceTo(polyline, this, tolerance);

        /// <summary>
        /// Gets the distance from this box to a solid, using the default tolerance.
        /// </summary>
        public double DistanceTo(GeoSolid3 solid) => Distance3.DistanceTo(solid, this);

        /// <summary>
        /// Gets the distance from this box to a solid, within a tolerance.
        /// </summary>
        public double DistanceTo(GeoSolid3 solid, Tolerance tolerance) => Distance3.DistanceTo(solid, this, tolerance);

        /// <summary>
        /// Gets the distance from this box to a box, using the default tolerance.
        /// </summary>
        public double DistanceTo(GeoObb3 other) => Distance3.DistanceTo(this, other);

        /// <summary>
        /// Gets the distance from this box to a box, within a tolerance.
        /// </summary>
        public double DistanceTo(GeoObb3 other, Tolerance tolerance) => Distance3.DistanceTo(this, other, tolerance);

        /// <summary>
        /// Calculates the distance from this box to a point, negative for a point within it.
        /// </summary>
        /// <remarks>
        /// The magnitude is the distance to the surface, whichever side of it the point is on, and the sign
        /// says which side: negative inside, nought on it, positive outside. <see cref="DistanceTo(GeoPoint3)"/>
        /// reads this box as filled and so answers nothing at all for a point inside, which is the one
        /// place the two part company.
        /// </remarks>
        public double SignedDistanceTo(GeoPoint3 point) => Distance3.SignedDistanceTo(this, point);

        /// <summary>
        /// Calculates the distance from this box to a point, negative for a point within it, within a tolerance.
        /// </summary>
        public double SignedDistanceTo(GeoPoint3 point, Tolerance tolerance) => Distance3.SignedDistanceTo(this, point, tolerance);

        /// <summary>
        /// Calculates the shortest distance between this oriented box and a segment: the answer of
        /// <see cref="Distance3.DistanceTo(GeoLine3, GeoObb3)"/>.
        /// </summary>
        /// <remarks>
        /// The answer about the box's body, <see cref="GeoObb3.ToSolid"/>. The box 100 by 100 by 100 about (50, 50, 50) is
        /// 50 from the segment from (150, 50, 50) to (150, 50, 150), and nought from one reaching into it.
        /// </remarks>
        public double DistanceTo(GeoLine3 line) => Distance3.DistanceTo(line, this);

        /// <summary>
        /// Calculates the shortest distance between this oriented box and a segment, within a tolerance: the answer of
        /// <see cref="Distance3.DistanceTo(GeoLine3, GeoObb3, Tolerance)"/>.
        /// </summary>
        /// <remarks>
        /// The answer about the box's body, <see cref="GeoObb3.ToSolid"/>. The box 100 by 100 by 100 about (50, 50, 50) is
        /// 50 from the segment from (150, 50, 50) to (150, 50, 150), and nought from one reaching into it.
        /// </remarks>
        public double DistanceTo(GeoLine3 line, Tolerance tolerance) => Distance3.DistanceTo(line, this, tolerance);

        /// <summary>
        /// Calculates the shortest distance between this oriented box and a ray: the answer of
        /// <see cref="Distance3.DistanceTo(GeoRay3, GeoObb3)"/>.
        /// </summary>
        /// <remarks>
        /// The answer about the box's body, <see cref="GeoObb3.ToSolid"/>. The box 100 by 100 by 100 about (50, 50, 50) is
        /// 50 from the ray from (150, 50, 50) along X, and nought from the same ray turned round.
        /// </remarks>
        public double DistanceTo(GeoRay3 ray) => Distance3.DistanceTo(ray, this);

        /// <summary>
        /// Calculates the shortest distance between this oriented box and a ray, within a tolerance: the answer of
        /// <see cref="Distance3.DistanceTo(GeoRay3, GeoObb3, Tolerance)"/>.
        /// </summary>
        /// <remarks>
        /// The answer about the box's body, <see cref="GeoObb3.ToSolid"/>. The box 100 by 100 by 100 about (50, 50, 50) is
        /// 50 from the ray from (150, 50, 50) along X, and nought from the same ray turned round.
        /// </remarks>
        public double DistanceTo(GeoRay3 ray, Tolerance tolerance) => Distance3.DistanceTo(ray, this, tolerance);

        /// <summary>
        /// Calculates the shortest distance between this oriented box and a plane: the answer of
        /// <see cref="Distance3.DistanceTo(GeoPlane3, GeoObb3)"/>.
        /// </summary>
        /// <remarks>
        /// The answer about the box's body, <see cref="GeoObb3.ToSolid"/>. The box 100 by 100 by 100 about (50, 50, 50) is
        /// 30 from the plane z = 130, and nought from the plane z = 50.
        /// </remarks>
        public double DistanceTo(GeoPlane3 plane) => Distance3.DistanceTo(plane, this);

        /// <summary>
        /// Calculates the shortest distance between this oriented box and a plane, within a tolerance: the answer of
        /// <see cref="Distance3.DistanceTo(GeoPlane3, GeoObb3, Tolerance)"/>.
        /// </summary>
        /// <remarks>
        /// The answer about the box's body, <see cref="GeoObb3.ToSolid"/>. The box 100 by 100 by 100 about (50, 50, 50) is
        /// 30 from the plane z = 130, and nought from the plane z = 50.
        /// </remarks>
        public double DistanceTo(GeoPlane3 plane, Tolerance tolerance) => Distance3.DistanceTo(plane, this, tolerance);

        /// <summary>
        /// Calculates the shortest distance between this oriented box and a triangle: the answer of
        /// <see cref="Distance3.DistanceTo(GeoTriangle3, GeoObb3)"/>.
        /// </summary>
        /// <remarks>
        /// The answer about the box's body, <see cref="GeoObb3.ToSolid"/>. The box 100 by 100 by 100 about (50, 50, 50) is
        /// 30 from the triangle with corners (0, 0, 130), (100, 0, 130) and (0, 100, 130), and nought from one wholly
        /// inside it.
        /// </remarks>
        public double DistanceTo(GeoTriangle3 triangle) => Distance3.DistanceTo(triangle, this);

        /// <summary>
        /// Calculates the shortest distance between this oriented box and a triangle, within a tolerance: the answer of
        /// <see cref="Distance3.DistanceTo(GeoTriangle3, GeoObb3, Tolerance)"/>.
        /// </summary>
        /// <remarks>
        /// The answer about the box's body, <see cref="GeoObb3.ToSolid"/>. The box 100 by 100 by 100 about (50, 50, 50) is
        /// 30 from the triangle with corners (0, 0, 130), (100, 0, 130) and (0, 100, 130), and nought from one wholly
        /// inside it.
        /// </remarks>
        public double DistanceTo(GeoTriangle3 triangle, Tolerance tolerance) => Distance3.DistanceTo(triangle, this, tolerance);

        /// <summary>
        /// Calculates the shortest distance between this oriented box and a polygon: the answer of
        /// <see cref="Distance3.DistanceTo(GeoPolygon3, GeoObb3)"/>.
        /// </summary>
        /// <remarks>
        /// The answer about the box's body, <see cref="GeoObb3.ToSolid"/>. The box 100 by 100 by 100 about (50, 50, 50) is
        /// 30 from the square 100 by 100 lying flat at a height of 130 over it, and nought from the square 200 by 200 lying
        /// across it at half its height.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public double DistanceTo(GeoPolygon3 polygon) => Distance3.DistanceTo(polygon, this);

        /// <summary>
        /// Calculates the shortest distance between this oriented box and a polygon, within a tolerance: the answer of
        /// <see cref="Distance3.DistanceTo(GeoPolygon3, GeoObb3, Tolerance)"/>.
        /// </summary>
        /// <remarks>
        /// The answer about the box's body, <see cref="GeoObb3.ToSolid"/>. The box 100 by 100 by 100 about (50, 50, 50) is
        /// 30 from the square 100 by 100 lying flat at a height of 130 over it, and nought from the square 200 by 200 lying
        /// across it at half its height.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public double DistanceTo(GeoPolygon3 polygon, Tolerance tolerance) => Distance3.DistanceTo(polygon, this, tolerance);

        /// <summary>
        /// Calculates the shortest distance between this oriented box and a face: the answer of
        /// <see cref="Distance3.DistanceTo(GeoFace3, GeoObb3)"/>.
        /// </summary>
        /// <remarks>
        /// The answer about the box's body, <see cref="GeoObb3.ToSolid"/>. The face is read by its material. The box 100 by
        /// 100 by 100 about (50, 50, 50) passes through the hole of the square 200 by 200 from (-50, -50, 50) to (150, 150,
        /// 50) with a hole 150 by 150 in its middle and is 25 from its rim.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public double DistanceTo(GeoFace3 face) => Distance3.DistanceTo(face, this);

        /// <summary>
        /// Calculates the shortest distance between this oriented box and a face, within a tolerance: the answer of
        /// <see cref="Distance3.DistanceTo(GeoFace3, GeoObb3, Tolerance)"/>.
        /// </summary>
        /// <remarks>
        /// The answer about the box's body, <see cref="GeoObb3.ToSolid"/>. The face is read by its material. The box 100 by
        /// 100 by 100 about (50, 50, 50) passes through the hole of the square 200 by 200 from (-50, -50, 50) to (150, 150,
        /// 50) with a hole 150 by 150 in its middle and is 25 from its rim.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public double DistanceTo(GeoFace3 face, Tolerance tolerance) => Distance3.DistanceTo(face, this, tolerance);

        /// <summary>
        /// Calculates the shortest distance between this oriented box and an axis-aligned box: the answer of
        /// <see cref="Distance3.DistanceTo(GeoObb3, GeoAabb3)"/>.
        /// </summary>
        /// <remarks>
        /// The answer of <see cref="Distance3.DistanceTo(GeoObb3, GeoObb3, Tolerance)"/> with the axis-aligned box as
        /// <c>ToObb()</c>: two oriented boxes are measured as their bodies once the separating axis test has found them
        /// apart. The box from the origin to (100, 100, 100) and the box 100 by 100 by 100 about (250, 50, 50) are 100
        /// apart.
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown when the box is empty.</exception>
        public double DistanceTo(GeoAabb3 box) => Distance3.DistanceTo(this, box);

        /// <summary>
        /// Calculates the shortest distance between this oriented box and an axis-aligned box, within a tolerance: the
        /// answer of <see cref="Distance3.DistanceTo(GeoObb3, GeoAabb3, Tolerance)"/>.
        /// </summary>
        /// <remarks>
        /// The answer of <see cref="Distance3.DistanceTo(GeoObb3, GeoObb3, Tolerance)"/> with the axis-aligned box as
        /// <c>ToObb()</c>: two oriented boxes are measured as their bodies once the separating axis test has found them
        /// apart. The box from the origin to (100, 100, 100) and the box 100 by 100 by 100 about (250, 50, 50) are 100
        /// apart.
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown when the box is empty.</exception>
        public double DistanceTo(GeoAabb3 box, Tolerance tolerance) => Distance3.DistanceTo(this, box, tolerance);
    }
}
