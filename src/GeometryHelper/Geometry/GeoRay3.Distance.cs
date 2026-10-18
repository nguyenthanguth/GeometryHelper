using System;
using GeometryHelper;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// How far a ray is from another shape, and how deep inside one it sits.
    /// </summary>
    public readonly partial struct GeoRay3
    {
        /// <summary>
        /// Calculates the shortest distance from this ray to a point.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoPoint3 point) => Distance3.DistanceTo(this, point);

        /// <summary>
        /// Calculates the shortest distance between this ray and a line segment.
        /// </summary>
        /// <returns>
        /// Nought only where they truly meet, otherwise the gap, measured exactly. A segment no longer than the global
        /// point tolerance is read as its start point, except where it truly meets the other shape.
        /// </returns>
        public double DistanceTo(GeoLine3 line) => Distance3.DistanceTo(this, line);

        /// <summary>
        /// Gets the distance from this ray to a solid, using the default tolerance.
        /// </summary>
        /// <returns>
        /// Nought only where they truly meet, otherwise the gap, measured exactly. Within a billionth of a millimetre
        /// of the outside of a solid it reads nought, so that rounding in a turned body cannot lose a crossing.
        /// </returns>
        public double DistanceTo(GeoSolid3 solid) => Distance3.DistanceTo(this, solid);

        /// <summary>
        /// Gets the distance from this ray to a solid, within a tolerance.
        /// </summary>
        /// <returns>
        /// Nought only where they truly meet, otherwise the gap, measured exactly. Within a billionth of a millimetre
        /// of the outside of a solid it reads nought, so that rounding in a turned body cannot lose a crossing.
        /// </returns>
        public double DistanceTo(GeoSolid3 solid, Tolerance tolerance) => Distance3.DistanceTo(this, solid, tolerance);

        /// <summary>
        /// Gets the distance from this ray to a triangle, using the default tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoTriangle3 triangle) => Distance3.DistanceTo(this, triangle);

        /// <summary>
        /// Gets the distance from this ray to a triangle, within a tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoTriangle3 triangle, Tolerance tolerance) => Distance3.DistanceTo(this, triangle, tolerance);

        /// <summary>
        /// Calculates the shortest distance between this ray and a face: the answer of
        /// <see cref="Distance3.DistanceTo(GeoRay3, GeoFace3)"/>.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        /// <remarks>
        /// The face is read by its material. Over the square 100 by 100 in the floor with a corner at the origin and a hole
        /// 40 by 40 in its middle, the ray from (45, 50, 10) pointing straight down, through the hole, is 15 from the rim;
        /// from (20, 20, 10) it runs into the material and is nought from it.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public double DistanceTo(GeoFace3 face) => Distance3.DistanceTo(this, face);

        /// <summary>
        /// Calculates the shortest distance between this ray and a face, within a tolerance: the answer of
        /// <see cref="Distance3.DistanceTo(GeoRay3, GeoFace3, Tolerance)"/>.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        /// <remarks>
        /// The face is read by its material. Over the square 100 by 100 in the floor with a corner at the origin and a hole
        /// 40 by 40 in its middle, the ray from (45, 50, 10) pointing straight down, through the hole, is 15 from the rim;
        /// from (20, 20, 10) it runs into the material and is nought from it.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public double DistanceTo(GeoFace3 face, Tolerance tolerance) => Distance3.DistanceTo(this, face, tolerance);

        /// <summary>
        /// Calculates the shortest distance between this ray and a polygon: the answer of
        /// <see cref="Distance3.DistanceTo(GeoRay3, GeoPolygon3)"/>.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        /// <remarks>
        /// The polygon is read as a filled surface. Beside the square 100 by 100 in the floor with a corner at the origin,
        /// the ray from (130, 50, 40) along X is 50 from its edge; the ray from (20, 20, 10) pointing straight down runs
        /// into it and is nought away.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public double DistanceTo(GeoPolygon3 polygon) => Distance3.DistanceTo(this, polygon);

        /// <summary>
        /// Calculates the shortest distance between this ray and a polygon, within a tolerance: the answer of
        /// <see cref="Distance3.DistanceTo(GeoRay3, GeoPolygon3, Tolerance)"/>.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        /// <remarks>
        /// The polygon is read as a filled surface. Beside the square 100 by 100 in the floor with a corner at the origin,
        /// the ray from (130, 50, 40) along X is 50 from its edge; the ray from (20, 20, 10) pointing straight down runs
        /// into it and is nought away.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public double DistanceTo(GeoPolygon3 polygon, Tolerance tolerance) => Distance3.DistanceTo(this, polygon, tolerance);

        /// <summary>
        /// Calculates the shortest distance between this ray and a plane: the answer of
        /// <see cref="Distance3.DistanceTo(GeoRay3, GeoPlane3)"/>.
        /// </summary>
        /// <returns>
        /// Nought where the plane crosses the shape or comes within the global point tolerance of it, otherwise the
        /// gap.
        /// </returns>
        /// <remarks>
        /// The ray from (0, 0, 10) pointing straight up is 10 above the floor plane; pointing straight down it reaches it
        /// and is nought away.
        /// </remarks>
        public double DistanceTo(GeoPlane3 plane) => Distance3.DistanceTo(this, plane);

        /// <summary>
        /// Calculates the shortest distance between this ray and a plane, within a tolerance: the answer of
        /// <see cref="Distance3.DistanceTo(GeoRay3, GeoPlane3, Tolerance)"/>.
        /// </summary>
        /// <returns>
        /// Nought where the plane crosses the shape or comes within the tolerance given of it, otherwise the gap.
        /// </returns>
        /// <remarks>
        /// The ray from (0, 0, 10) pointing straight up is 10 above the floor plane; pointing straight down it reaches it
        /// and is nought away.
        /// </remarks>
        public double DistanceTo(GeoPlane3 plane, Tolerance tolerance) => Distance3.DistanceTo(this, plane, tolerance);

        /// <summary>
        /// Calculates the shortest distance between this ray and another one: the answer of
        /// <see cref="Distance3.DistanceTo(GeoRay3, GeoRay3)"/>.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        /// <remarks>
        /// The ray from the origin along X and the ray from (50, 30, 40) along Y are 50 apart; the ray from (50, -50, 0)
        /// along Y crosses the first and is nought from it.
        /// </remarks>
        public double DistanceTo(GeoRay3 other) => Distance3.DistanceTo(this, other);

        /// <summary>
        /// Calculates the shortest distance between this ray and another one, within a tolerance: the answer of
        /// <see cref="Distance3.DistanceTo(GeoRay3, GeoRay3, Tolerance)"/>.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        /// <remarks>
        /// The ray from the origin along X and the ray from (50, 30, 40) along Y are 50 apart; the ray from (50, -50, 0)
        /// along Y crosses the first and is nought from it.
        /// </remarks>
        public double DistanceTo(GeoRay3 other, Tolerance tolerance) => Distance3.DistanceTo(this, other, tolerance);

        /// <summary>
        /// Calculates the shortest distance between this ray and a polyline: the answer of
        /// <see cref="Distance3.DistanceTo(GeoPolyline3, GeoRay3)"/>.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        /// <remarks>
        /// The ray from (60, 50, 30) pointing straight up is 50 from the chain (0, 0, 0), (100, 0, 0), (100, 100, 0).
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the polyline is null.</exception>
        public double DistanceTo(GeoPolyline3 polyline) => Distance3.DistanceTo(polyline, this);

        /// <summary>
        /// Calculates the shortest distance between this ray and a polyline, within a tolerance: the answer of
        /// <see cref="Distance3.DistanceTo(GeoPolyline3, GeoRay3, Tolerance)"/>.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        /// <remarks>
        /// The ray from (60, 50, 30) pointing straight up is 50 from the chain (0, 0, 0), (100, 0, 0), (100, 100, 0).
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the polyline is null.</exception>
        public double DistanceTo(GeoPolyline3 polyline, Tolerance tolerance) => Distance3.DistanceTo(polyline, this, tolerance);

        /// <summary>
        /// Calculates the shortest distance between this ray and an oriented box: the answer of
        /// <see cref="Distance3.DistanceTo(GeoRay3, GeoObb3)"/>.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        /// <remarks>
        /// The answer about the box's body, <see cref="GeoObb3.ToSolid"/>. The box 100 by 100 by 100 about (50, 50, 50) is
        /// 50 from the ray from (150, 50, 50) along X, and nought from the same ray turned round.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the oriented box is null.</exception>
        public double DistanceTo(GeoObb3 box) => Distance3.DistanceTo(this, box);

        /// <summary>
        /// Calculates the shortest distance between this ray and an oriented box, within a tolerance: the answer of
        /// <see cref="Distance3.DistanceTo(GeoRay3, GeoObb3, Tolerance)"/>.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        /// <remarks>
        /// The answer about the box's body, <see cref="GeoObb3.ToSolid"/>. The box 100 by 100 by 100 about (50, 50, 50) is
        /// 50 from the ray from (150, 50, 50) along X, and nought from the same ray turned round.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the oriented box is null.</exception>
        public double DistanceTo(GeoObb3 box, Tolerance tolerance) => Distance3.DistanceTo(this, box, tolerance);

        /// <summary>
        /// Calculates the shortest distance between this ray and an axis-aligned box: the answer of
        /// <see cref="Distance3.DistanceTo(GeoRay3, GeoAabb3)"/>.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        /// <remarks>
        /// The answer about the box's body, <c>ToObb().ToSolid()</c>. The box from the origin to (100, 100, 100) is 50 from
        /// the ray from (150, 50, 50) along X, and nought from the same ray turned round.
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown when the box is empty.</exception>
        public double DistanceTo(GeoAabb3 box) => Distance3.DistanceTo(this, box);

        /// <summary>
        /// Calculates the shortest distance between this ray and an axis-aligned box, within a tolerance: the answer of
        /// <see cref="Distance3.DistanceTo(GeoRay3, GeoAabb3, Tolerance)"/>.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        /// <remarks>
        /// The answer about the box's body, <c>ToObb().ToSolid()</c>. The box from the origin to (100, 100, 100) is 50 from
        /// the ray from (150, 50, 50) along X, and nought from the same ray turned round.
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown when the box is empty.</exception>
        public double DistanceTo(GeoAabb3 box, Tolerance tolerance) => Distance3.DistanceTo(this, box, tolerance);
    }
}
