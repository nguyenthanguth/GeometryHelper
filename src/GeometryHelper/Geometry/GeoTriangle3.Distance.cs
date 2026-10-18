using System;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// How far a triangle is from another shape, and how deep inside one it sits.
    /// </summary>
    public readonly partial struct GeoTriangle3
    {
        /// <summary>
        /// Calculates the shortest distance from this triangle to a point.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoPoint3 point) => Distance3.DistanceTo(this, point);

        /// <summary>
        /// Gets the distance from this triangle to a segment, using the default tolerance.
        /// </summary>
        /// <returns>
        /// Nought only where they truly meet, otherwise the gap, measured exactly. A segment no longer than the global
        /// point tolerance is read as its start point, except where it truly meets the other shape.
        /// </returns>
        public double DistanceTo(GeoLine3 line) => Distance3.DistanceTo(line, this);

        /// <summary>
        /// Gets the distance from this triangle to a segment, within a tolerance.
        /// </summary>
        /// <returns>
        /// Nought only where they truly meet, otherwise the gap, measured exactly. A segment no longer than the
        /// tolerance given is read as its start point, except where it truly meets the other shape.
        /// </returns>
        public double DistanceTo(GeoLine3 line, Tolerance tolerance) => Distance3.DistanceTo(line, this, tolerance);

        /// <summary>
        /// Gets the distance from this triangle to a ray, using the default tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoRay3 ray) => Distance3.DistanceTo(ray, this);

        /// <summary>
        /// Gets the distance from this triangle to a ray, within a tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoRay3 ray, Tolerance tolerance) => Distance3.DistanceTo(ray, this, tolerance);

        /// <summary>
        /// Gets the distance from this triangle to a polyline, using the default tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoPolyline3 polyline) => Distance3.DistanceTo(polyline, this);

        /// <summary>
        /// Gets the distance from this triangle to a polyline, within a tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoPolyline3 polyline, Tolerance tolerance) => Distance3.DistanceTo(polyline, this, tolerance);

        /// <summary>
        /// Gets the distance from this triangle to a solid, using the default tolerance.
        /// </summary>
        /// <returns>
        /// Nought only where they truly meet, otherwise the gap, measured exactly. Within a billionth of a millimetre
        /// of the outside of a solid it reads nought, so that rounding in a turned body cannot lose a crossing.
        /// </returns>
        public double DistanceTo(GeoSolid3 solid) => Distance3.DistanceTo(solid, this);

        /// <summary>
        /// Gets the distance from this triangle to a solid, within a tolerance.
        /// </summary>
        /// <returns>
        /// Nought only where they truly meet, otherwise the gap, measured exactly. Within a billionth of a millimetre
        /// of the outside of a solid it reads nought, so that rounding in a turned body cannot lose a crossing.
        /// </returns>
        public double DistanceTo(GeoSolid3 solid, Tolerance tolerance) => Distance3.DistanceTo(solid, this, tolerance);

        /// <summary>
        /// Gets the distance from this triangle to a triangle, using the default tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoTriangle3 other) => Distance3.DistanceTo(this, other);

        /// <summary>
        /// Gets the distance from this triangle to a triangle, within a tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoTriangle3 other, Tolerance tolerance) => Distance3.DistanceTo(this, other, tolerance);

        /// <summary>
        /// Calculates the shortest distance between this triangle and a face: the answer of
        /// <see cref="Distance3.DistanceTo(GeoTriangle3, GeoFace3)"/>.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        /// <remarks>
        /// The face is read by its material. The triangle with corners (110, 50, 0), (200, 50, 0) and (110, 50, 100),
        /// standing beside the square 100 by 100 in the floor with a corner at the origin and a hole 40 by 40 in its
        /// middle, is 10 from its outline.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public double DistanceTo(GeoFace3 face) => Distance3.DistanceTo(this, face);

        /// <summary>
        /// Calculates the shortest distance between this triangle and a face, within a tolerance: the answer of
        /// <see cref="Distance3.DistanceTo(GeoTriangle3, GeoFace3, Tolerance)"/>.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        /// <remarks>
        /// The face is read by its material. The triangle with corners (110, 50, 0), (200, 50, 0) and (110, 50, 100),
        /// standing beside the square 100 by 100 in the floor with a corner at the origin and a hole 40 by 40 in its
        /// middle, is 10 from its outline.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public double DistanceTo(GeoFace3 face, Tolerance tolerance) => Distance3.DistanceTo(this, face, tolerance);

        /// <summary>
        /// Calculates the shortest distance between this triangle and a polygon: the answer of
        /// <see cref="Distance3.DistanceTo(GeoTriangle3, GeoPolygon3)"/>.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        /// <remarks>
        /// Both are read as filled surfaces. The triangle with corners (0, 0, 0), (100, 0, 0) and (0, 100, 0) and the
        /// square 100 by 100 standing upright along y = 50 from z = 10 to 110 are 10 apart; standing from z = -50, the
        /// square crosses the triangle and is nought from it.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public double DistanceTo(GeoPolygon3 polygon) => Distance3.DistanceTo(this, polygon);

        /// <summary>
        /// Calculates the shortest distance between this triangle and a polygon, within a tolerance: the answer of
        /// <see cref="Distance3.DistanceTo(GeoTriangle3, GeoPolygon3, Tolerance)"/>.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        /// <remarks>
        /// Both are read as filled surfaces. The triangle with corners (0, 0, 0), (100, 0, 0) and (0, 100, 0) and the
        /// square 100 by 100 standing upright along y = 50 from z = 10 to 110 are 10 apart; standing from z = -50, the
        /// square crosses the triangle and is nought from it.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public double DistanceTo(GeoPolygon3 polygon, Tolerance tolerance) => Distance3.DistanceTo(this, polygon, tolerance);

        /// <summary>
        /// Calculates the shortest distance between this triangle and a plane: the answer of
        /// <see cref="Distance3.DistanceTo(GeoPlane3, GeoTriangle3)"/>.
        /// </summary>
        /// <returns>
        /// Nought where the plane crosses the shape or comes within the global point tolerance of it, otherwise the
        /// gap.
        /// </returns>
        /// <remarks>
        /// The heights of the corners decide, as for a chain. The triangle with corners (0, 50, 10), (100, 50, 10) and (0,
        /// 50, 110) is 10 above the floor plane, and the triangle with corners (0, 50, -50), (100, 50, -50) and (0, 50, 50)
        /// crosses it and is nought away.
        /// </remarks>
        public double DistanceTo(GeoPlane3 plane) => Distance3.DistanceTo(plane, this);

        /// <summary>
        /// Calculates the shortest distance between this triangle and a plane, within a tolerance: the answer of
        /// <see cref="Distance3.DistanceTo(GeoPlane3, GeoTriangle3, Tolerance)"/>.
        /// </summary>
        /// <returns>
        /// Nought where the plane crosses the shape or comes within the tolerance given of it, otherwise the gap.
        /// </returns>
        /// <remarks>
        /// The heights of the corners decide, as for a chain. The triangle with corners (0, 50, 10), (100, 50, 10) and (0,
        /// 50, 110) is 10 above the floor plane, and the triangle with corners (0, 50, -50), (100, 50, -50) and (0, 50, 50)
        /// crosses it and is nought away.
        /// </remarks>
        public double DistanceTo(GeoPlane3 plane, Tolerance tolerance) => Distance3.DistanceTo(plane, this, tolerance);

        /// <summary>
        /// Calculates the shortest distance between this triangle and an oriented box: the answer of
        /// <see cref="Distance3.DistanceTo(GeoTriangle3, GeoObb3)"/>.
        /// </summary>
        /// <returns>
        /// Nought only where they truly meet, otherwise the gap, measured exactly. A triangle, polygon or face within a
        /// billionth of a millimetre of the outside of a box reads nought, for the same reason.
        /// </returns>
        /// <remarks>
        /// The answer about the box's body, <see cref="GeoObb3.ToSolid"/>. The box 100 by 100 by 100 about (50, 50, 50) is
        /// 30 from the triangle with corners (0, 0, 130), (100, 0, 130) and (0, 100, 130), and nought from one wholly
        /// inside it.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the oriented box is null.</exception>
        public double DistanceTo(GeoObb3 box) => Distance3.DistanceTo(this, box);

        /// <summary>
        /// Calculates the shortest distance between this triangle and an oriented box, within a tolerance: the answer of
        /// <see cref="Distance3.DistanceTo(GeoTriangle3, GeoObb3, Tolerance)"/>.
        /// </summary>
        /// <returns>
        /// Nought only where they truly meet, otherwise the gap, measured exactly. A triangle, polygon or face within a
        /// billionth of a millimetre of the outside of a box reads nought, for the same reason.
        /// </returns>
        /// <remarks>
        /// The answer about the box's body, <see cref="GeoObb3.ToSolid"/>. The box 100 by 100 by 100 about (50, 50, 50) is
        /// 30 from the triangle with corners (0, 0, 130), (100, 0, 130) and (0, 100, 130), and nought from one wholly
        /// inside it.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the oriented box is null.</exception>
        public double DistanceTo(GeoObb3 box, Tolerance tolerance) => Distance3.DistanceTo(this, box, tolerance);

        /// <summary>
        /// Calculates the shortest distance between this triangle and an axis-aligned box: the answer of
        /// <see cref="Distance3.DistanceTo(GeoTriangle3, GeoAabb3)"/>.
        /// </summary>
        /// <returns>
        /// Nought only where they truly meet, otherwise the gap, measured exactly. A triangle, polygon or face within a
        /// billionth of a millimetre of the outside of a box reads nought, for the same reason.
        /// </returns>
        /// <remarks>
        /// The answer about the box's body, <c>ToObb().ToSolid()</c>. The box from the origin to (100, 100, 100) is 30 from
        /// the triangle with corners (0, 0, 130), (100, 0, 130) and (0, 100, 130), and nought from one wholly inside it.
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown when the box is empty.</exception>
        public double DistanceTo(GeoAabb3 box) => Distance3.DistanceTo(this, box);

        /// <summary>
        /// Calculates the shortest distance between this triangle and an axis-aligned box, within a tolerance: the answer
        /// of <see cref="Distance3.DistanceTo(GeoTriangle3, GeoAabb3, Tolerance)"/>.
        /// </summary>
        /// <returns>
        /// Nought only where they truly meet, otherwise the gap, measured exactly. A triangle, polygon or face within a
        /// billionth of a millimetre of the outside of a box reads nought, for the same reason.
        /// </returns>
        /// <remarks>
        /// The answer about the box's body, <c>ToObb().ToSolid()</c>. The box from the origin to (100, 100, 100) is 30 from
        /// the triangle with corners (0, 0, 130), (100, 0, 130) and (0, 100, 130), and nought from one wholly inside it.
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown when the box is empty.</exception>
        public double DistanceTo(GeoAabb3 box, Tolerance tolerance) => Distance3.DistanceTo(this, box, tolerance);
    }
}
