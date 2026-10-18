using System;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// How far a segment is from another shape, and how deep inside one it sits.
    /// </summary>
    public readonly partial struct GeoLine3
    {
        /// <summary>
        /// Calculates the shortest distance from this segment to a point.
        /// </summary>
        /// <returns>
        /// Nought only where they truly meet, otherwise the gap, measured exactly. A segment no longer than the global
        /// point tolerance is read as its start point, except where it truly meets the other shape.
        /// </returns>
        public double DistanceTo(GeoPoint3 point) => Distance3.DistanceTo(this, point);

        /// <summary>
        /// Calculates the shortest distance between this segment and another segment.
        /// </summary>
        /// <returns>
        /// Nought only where they truly meet, otherwise the gap, measured exactly. A segment no longer than the global
        /// point tolerance is read as its start point, except where it truly meets the other shape.
        /// </returns>
        public double DistanceTo(GeoLine3 other) => Distance3.DistanceTo(this, other);

        /// <summary>
        /// Calculates the shortest distance between this segment and a ray.
        /// </summary>
        /// <returns>
        /// Nought only where they truly meet, otherwise the gap, measured exactly. A segment no longer than the global
        /// point tolerance is read as its start point, except where it truly meets the other shape.
        /// </returns>
        public double DistanceTo(GeoRay3 ray) => Distance3.DistanceTo(ray, this);

        /// <summary>
        /// Calculates the shortest distance between this segment and a plane.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoPlane3 plane) => Distance3.DistanceTo(plane, this);

        /// <summary>
        /// Calculates the shortest distance between this segment and a triangle.
        /// </summary>
        /// <returns>
        /// Nought only where they truly meet, otherwise the gap, measured exactly. A segment no longer than the global
        /// point tolerance is read as its start point, except where it truly meets the other shape.
        /// </returns>
        public double DistanceTo(GeoTriangle3 triangle) => Distance3.DistanceTo(this, triangle);

        /// <summary>
        /// Calculates the shortest distance between this segment and a triangle, within a tolerance: the same answer as
        /// <see cref="GeoTriangle3.DistanceTo(GeoLine3, Tolerance)"/>.
        /// </summary>
        /// <returns>
        /// Nought only where they truly meet, otherwise the gap, measured exactly. A segment no longer than the
        /// tolerance given is read as its start point, except where it truly meets the other shape.
        /// </returns>
        public double DistanceTo(GeoTriangle3 triangle, Tolerance tolerance) => Distance3.DistanceTo(this, triangle, tolerance);

        /// <summary>
        /// Calculates the shortest distance between this segment and a polygon.
        /// </summary>
        /// <returns>
        /// Nought only where they truly meet, otherwise the gap, measured exactly. A segment no longer than the global
        /// point tolerance is read as its start point, except where it truly meets the other shape.
        /// </returns>
        public double DistanceTo(GeoPolygon3 polygon) => Distance3.DistanceTo(this, polygon);

        /// <summary>
        /// Calculates the shortest distance between this segment and a polygon, within a tolerance: the same answer as
        /// <see cref="GeoPolygon3.DistanceTo(GeoLine3, Tolerance)"/>.
        /// </summary>
        /// <returns>
        /// Nought only where they truly meet, otherwise the gap, measured exactly. A segment no longer than the
        /// tolerance given is read as its start point, except where it truly meets the other shape.
        /// </returns>
        public double DistanceTo(GeoPolygon3 polygon, Tolerance tolerance) => Distance3.DistanceTo(this, polygon, tolerance);

        /// <summary>
        /// Calculates the shortest distance between this segment and a solid. A segment reaching into the
        /// body is at distance zero.
        /// </summary>
        /// <returns>
        /// Nought only where they truly meet, otherwise the gap, measured exactly. Within a billionth of a millimetre
        /// of the outside of a solid it reads nought, so that rounding in a turned body cannot lose a crossing. A
        /// segment no longer than the global point tolerance is read as its start point, except where it truly meets
        /// the other shape.
        /// </returns>
        public double DistanceTo(GeoSolid3 solid) => Distance3.DistanceTo(this, solid);

        /// <summary>
        /// Calculates the shortest distance between this segment and a solid, within a tolerance: the same answer as
        /// <see cref="GeoSolid3.DistanceTo(GeoLine3, Tolerance)"/>.
        /// </summary>
        /// <returns>
        /// Nought only where they truly meet, otherwise the gap, measured exactly. Within a billionth of a millimetre
        /// of the outside of a solid it reads nought, so that rounding in a turned body cannot lose a crossing. A
        /// segment no longer than the tolerance given is read as its start point, except where it truly meets the other
        /// shape.
        /// </returns>
        public double DistanceTo(GeoSolid3 solid, Tolerance tolerance) => Distance3.DistanceTo(this, solid, tolerance);

        /// <summary>
        /// Gets the distance from this segment to a polyline, using the default tolerance.
        /// </summary>
        /// <returns>
        /// Nought only where they truly meet, otherwise the gap, measured exactly. A segment no longer than the global
        /// point tolerance is read as its start point, except where it truly meets the other shape.
        /// </returns>
        public double DistanceTo(GeoPolyline3 polyline) => Distance3.DistanceTo(polyline, this);

        /// <summary>
        /// Gets the distance from this segment to a polyline, within a tolerance.
        /// </summary>
        /// <returns>
        /// Nought only where they truly meet, otherwise the gap, measured exactly. A segment no longer than the
        /// tolerance given is read as its start point, except where it truly meets the other shape.
        /// </returns>
        public double DistanceTo(GeoPolyline3 polyline, Tolerance tolerance) => Distance3.DistanceTo(polyline, this, tolerance);

        /// <summary>
        /// Calculates the shortest distance between this segment and a face: the answer of
        /// <see cref="Distance3.DistanceTo(GeoLine3, GeoFace3)"/>.
        /// </summary>
        /// <returns>
        /// Nought only where they truly meet, otherwise the gap, measured exactly. A segment no longer than the global
        /// point tolerance is read as its start point, except where it truly meets the other shape.
        /// </returns>
        /// <remarks>
        /// The face is read by its material. Over the square 100 by 100 in the floor with a corner at the origin and a hole
        /// 40 by 40 in its middle, the segment from (45, 50, -10) to (45, 50, 10), through the hole, is 15 from the rim;
        /// the segment from (20, 20, -10) to (20, 20, 10) crosses the material and is nought from it.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public double DistanceTo(GeoFace3 face) => Distance3.DistanceTo(this, face);

        /// <summary>
        /// Calculates the shortest distance between this segment and a face, within a tolerance: the answer of
        /// <see cref="Distance3.DistanceTo(GeoLine3, GeoFace3, Tolerance)"/>.
        /// </summary>
        /// <returns>
        /// Nought only where they truly meet, otherwise the gap, measured exactly. A segment no longer than the
        /// tolerance given is read as its start point, except where it truly meets the other shape.
        /// </returns>
        /// <remarks>
        /// The face is read by its material. Over the square 100 by 100 in the floor with a corner at the origin and a hole
        /// 40 by 40 in its middle, the segment from (45, 50, -10) to (45, 50, 10), through the hole, is 15 from the rim;
        /// the segment from (20, 20, -10) to (20, 20, 10) crosses the material and is nought from it.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public double DistanceTo(GeoFace3 face, Tolerance tolerance) => Distance3.DistanceTo(this, face, tolerance);

        /// <summary>
        /// Calculates the shortest distance between this segment and an oriented box: the answer of
        /// <see cref="Distance3.DistanceTo(GeoLine3, GeoObb3)"/>.
        /// </summary>
        /// <returns>
        /// Nought only where they truly meet, otherwise the gap, measured exactly. A segment no longer than the global
        /// point tolerance is read as its start point, except where it truly meets the other shape.
        /// </returns>
        /// <remarks>
        /// The answer about the box's body, <see cref="GeoObb3.ToSolid"/>. The box 100 by 100 by 100 about (50, 50, 50) is
        /// 50 from the segment from (150, 50, 50) to (150, 50, 150), and nought from one reaching into it.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the oriented box is null.</exception>
        public double DistanceTo(GeoObb3 box) => Distance3.DistanceTo(this, box);

        /// <summary>
        /// Calculates the shortest distance between this segment and an oriented box, within a tolerance: the answer of
        /// <see cref="Distance3.DistanceTo(GeoLine3, GeoObb3, Tolerance)"/>.
        /// </summary>
        /// <returns>
        /// Nought only where they truly meet, otherwise the gap, measured exactly. A segment no longer than the
        /// tolerance given is read as its start point, except where it truly meets the other shape.
        /// </returns>
        /// <remarks>
        /// The answer about the box's body, <see cref="GeoObb3.ToSolid"/>. The box 100 by 100 by 100 about (50, 50, 50) is
        /// 50 from the segment from (150, 50, 50) to (150, 50, 150), and nought from one reaching into it.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the oriented box is null.</exception>
        public double DistanceTo(GeoObb3 box, Tolerance tolerance) => Distance3.DistanceTo(this, box, tolerance);

        /// <summary>
        /// Calculates the shortest distance between this segment and an axis-aligned box: the answer of
        /// <see cref="Distance3.DistanceTo(GeoLine3, GeoAabb3)"/>.
        /// </summary>
        /// <returns>
        /// Nought only where they truly meet, otherwise the gap, measured exactly. A segment no longer than the global
        /// point tolerance is read as its start point, except where it truly meets the other shape.
        /// </returns>
        /// <remarks>
        /// The answer about the box's body, <c>ToObb().ToSolid()</c>. The box from the origin to (100, 100, 100) is 50 from
        /// the segment from (150, 50, 50) to (150, 50, 150), and nought from one reaching into it.
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown when the box is empty.</exception>
        public double DistanceTo(GeoAabb3 box) => Distance3.DistanceTo(this, box);

        /// <summary>
        /// Calculates the shortest distance between this segment and an axis-aligned box, within a tolerance: the answer of
        /// <see cref="Distance3.DistanceTo(GeoLine3, GeoAabb3, Tolerance)"/>.
        /// </summary>
        /// <returns>
        /// Nought only where they truly meet, otherwise the gap, measured exactly. A segment no longer than the
        /// tolerance given is read as its start point, except where it truly meets the other shape.
        /// </returns>
        /// <remarks>
        /// The answer about the box's body, <c>ToObb().ToSolid()</c>. The box from the origin to (100, 100, 100) is 50 from
        /// the segment from (150, 50, 50) to (150, 50, 150), and nought from one reaching into it.
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown when the box is empty.</exception>
        public double DistanceTo(GeoAabb3 box, Tolerance tolerance) => Distance3.DistanceTo(this, box, tolerance);
    }
}
