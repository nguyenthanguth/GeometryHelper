using System;
using System.Collections.Generic;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// How far a polygon is from another shape, and how deep inside one it sits.
    /// </summary>
    public sealed partial class GeoPolygon3
    {
        /// <summary>
        /// Calculates the shortest distance from this polygon to a point.
        /// </summary>
        /// <remarks>
        /// The polygon counts as a filled surface, so a point above its interior is measured straight down
        /// to the surface rather than out to the nearest edge.
        /// </remarks>
        public double DistanceTo(GeoPoint3 point) => Distance3.DistanceTo(this, point);

        /// <summary>
        /// Gets the distance from this polygon to a segment, using the default tolerance.
        /// </summary>
        public double DistanceTo(GeoLine3 line) => Distance3.DistanceTo(line, this);

        /// <summary>
        /// Gets the distance from this polygon to a segment, within a tolerance.
        /// </summary>
        public double DistanceTo(GeoLine3 line, Tolerance tolerance) => Distance3.DistanceTo(line, this, tolerance);

        /// <summary>
        /// Gets the distance from this polygon to a polyline, using the default tolerance.
        /// </summary>
        public double DistanceTo(GeoPolyline3 polyline) => Distance3.DistanceTo(polyline, this);

        /// <summary>
        /// Gets the distance from this polygon to a polyline, within a tolerance.
        /// </summary>
        public double DistanceTo(GeoPolyline3 polyline, Tolerance tolerance) => Distance3.DistanceTo(polyline, this, tolerance);

        /// <summary>
        /// Gets the distance from this polygon to a solid, using the default tolerance.
        /// </summary>
        public double DistanceTo(GeoSolid3 solid) => Distance3.DistanceTo(solid, this);

        /// <summary>
        /// Gets the distance from this polygon to a solid, within a tolerance.
        /// </summary>
        public double DistanceTo(GeoSolid3 solid, Tolerance tolerance) => Distance3.DistanceTo(solid, this, tolerance);

        /// <summary>
        /// Calculates the shortest distance between this polygon and a face: the answer of
        /// <see cref="Distance3.DistanceTo(GeoFace3, GeoPolygon3)"/>.
        /// </summary>
        /// <remarks>
        /// The face is read by its material and the polygon as a filled surface. A square 20 by 20 standing upright in the
        /// middle of the hole of the square 100 by 100 in the floor with a corner at the origin and a hole 40 by 40 in its
        /// middle, along y = 50 from x = 40 to 60, is 10 from it.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public double DistanceTo(GeoFace3 face) => Distance3.DistanceTo(face, this);

        /// <summary>
        /// Calculates the shortest distance between this polygon and a face, within a tolerance: the answer of
        /// <see cref="Distance3.DistanceTo(GeoFace3, GeoPolygon3, Tolerance)"/>.
        /// </summary>
        /// <remarks>
        /// The face is read by its material and the polygon as a filled surface. A square 20 by 20 standing upright in the
        /// middle of the hole of the square 100 by 100 in the floor with a corner at the origin and a hole 40 by 40 in its
        /// middle, along y = 50 from x = 40 to 60, is 10 from it.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public double DistanceTo(GeoFace3 face, Tolerance tolerance) => Distance3.DistanceTo(face, this, tolerance);

        /// <summary>
        /// Calculates the shortest distance between this polygon and another one: the answer of
        /// <see cref="Distance3.DistanceTo(GeoPolygon3, GeoPolygon3)"/>.
        /// </summary>
        /// <remarks>
        /// Both are read as filled surfaces. A square 100 by 100 in the floor with a corner at the origin and one standing
        /// upright along y = 50 from z = 10 to 110 are 10 apart; one lying 0.0004 above it touches it and is nought away.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public double DistanceTo(GeoPolygon3 other) => Distance3.DistanceTo(this, other);

        /// <summary>
        /// Calculates the shortest distance between this polygon and another one, within a tolerance: the answer of
        /// <see cref="Distance3.DistanceTo(GeoPolygon3, GeoPolygon3, Tolerance)"/>.
        /// </summary>
        /// <remarks>
        /// Both are read as filled surfaces. A square 100 by 100 in the floor with a corner at the origin and one standing
        /// upright along y = 50 from z = 10 to 110 are 10 apart; one lying 0.0004 above it touches it and is nought away.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public double DistanceTo(GeoPolygon3 other, Tolerance tolerance) => Distance3.DistanceTo(this, other, tolerance);

        /// <summary>
        /// Calculates the shortest distance between this polygon and a ray: the answer of
        /// <see cref="Distance3.DistanceTo(GeoRay3, GeoPolygon3)"/>.
        /// </summary>
        /// <remarks>
        /// The polygon is read as a filled surface. Beside the square 100 by 100 in the floor with a corner at the origin,
        /// the ray from (130, 50, 40) along X is 50 from its edge; the ray from (20, 20, 10) pointing straight down runs
        /// into it and is nought away.
        /// </remarks>
        public double DistanceTo(GeoRay3 ray) => Distance3.DistanceTo(ray, this);

        /// <summary>
        /// Calculates the shortest distance between this polygon and a ray, within a tolerance: the answer of
        /// <see cref="Distance3.DistanceTo(GeoRay3, GeoPolygon3, Tolerance)"/>.
        /// </summary>
        /// <remarks>
        /// The polygon is read as a filled surface. Beside the square 100 by 100 in the floor with a corner at the origin,
        /// the ray from (130, 50, 40) along X is 50 from its edge; the ray from (20, 20, 10) pointing straight down runs
        /// into it and is nought away.
        /// </remarks>
        public double DistanceTo(GeoRay3 ray, Tolerance tolerance) => Distance3.DistanceTo(ray, this, tolerance);

        /// <summary>
        /// Calculates the shortest distance between this polygon and a triangle: the answer of
        /// <see cref="Distance3.DistanceTo(GeoTriangle3, GeoPolygon3)"/>.
        /// </summary>
        /// <remarks>
        /// Both are read as filled surfaces. The triangle with corners (0, 0, 0), (100, 0, 0) and (0, 100, 0) and the
        /// square 100 by 100 standing upright along y = 50 from z = 10 to 110 are 10 apart; standing from z = -50, the
        /// square crosses the triangle and is nought from it.
        /// </remarks>
        public double DistanceTo(GeoTriangle3 triangle) => Distance3.DistanceTo(triangle, this);

        /// <summary>
        /// Calculates the shortest distance between this polygon and a triangle, within a tolerance: the answer of
        /// <see cref="Distance3.DistanceTo(GeoTriangle3, GeoPolygon3, Tolerance)"/>.
        /// </summary>
        /// <remarks>
        /// Both are read as filled surfaces. The triangle with corners (0, 0, 0), (100, 0, 0) and (0, 100, 0) and the
        /// square 100 by 100 standing upright along y = 50 from z = 10 to 110 are 10 apart; standing from z = -50, the
        /// square crosses the triangle and is nought from it.
        /// </remarks>
        public double DistanceTo(GeoTriangle3 triangle, Tolerance tolerance) => Distance3.DistanceTo(triangle, this, tolerance);

        /// <summary>
        /// Calculates the shortest distance between this polygon and a plane: the answer of
        /// <see cref="Distance3.DistanceTo(GeoPlane3, GeoPolygon3)"/>.
        /// </summary>
        /// <remarks>
        /// The heights of the corners decide, as for a chain. A square 100 by 100 standing upright along y = 50 from z = 10
        /// to 110 is 10 above the floor plane; one standing from z = -50 crosses it and is nought away.
        /// </remarks>
        public double DistanceTo(GeoPlane3 plane) => Distance3.DistanceTo(plane, this);

        /// <summary>
        /// Calculates the shortest distance between this polygon and a plane, within a tolerance: the answer of
        /// <see cref="Distance3.DistanceTo(GeoPlane3, GeoPolygon3, Tolerance)"/>.
        /// </summary>
        /// <remarks>
        /// The heights of the corners decide, as for a chain. A square 100 by 100 standing upright along y = 50 from z = 10
        /// to 110 is 10 above the floor plane; one standing from z = -50 crosses it and is nought away.
        /// </remarks>
        public double DistanceTo(GeoPlane3 plane, Tolerance tolerance) => Distance3.DistanceTo(plane, this, tolerance);

        /// <summary>
        /// Calculates the shortest distance between this polygon and an oriented box: the answer of
        /// <see cref="Distance3.DistanceTo(GeoPolygon3, GeoObb3)"/>.
        /// </summary>
        /// <remarks>
        /// The answer about the box's body, <see cref="GeoObb3.ToSolid"/>. The box 100 by 100 by 100 about (50, 50, 50) is
        /// 30 from the square 100 by 100 lying flat at a height of 130 over it, and nought from the square 200 by 200 lying
        /// across it at half its height.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the oriented box is null.</exception>
        public double DistanceTo(GeoObb3 box) => Distance3.DistanceTo(this, box);

        /// <summary>
        /// Calculates the shortest distance between this polygon and an oriented box, within a tolerance: the answer of
        /// <see cref="Distance3.DistanceTo(GeoPolygon3, GeoObb3, Tolerance)"/>.
        /// </summary>
        /// <remarks>
        /// The answer about the box's body, <see cref="GeoObb3.ToSolid"/>. The box 100 by 100 by 100 about (50, 50, 50) is
        /// 30 from the square 100 by 100 lying flat at a height of 130 over it, and nought from the square 200 by 200 lying
        /// across it at half its height.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the oriented box is null.</exception>
        public double DistanceTo(GeoObb3 box, Tolerance tolerance) => Distance3.DistanceTo(this, box, tolerance);

        /// <summary>
        /// Calculates the shortest distance between this polygon and an axis-aligned box: the answer of
        /// <see cref="Distance3.DistanceTo(GeoPolygon3, GeoAabb3)"/>.
        /// </summary>
        /// <remarks>
        /// The answer about the box's body, <c>ToObb().ToSolid()</c>. The box from the origin to (100, 100, 100) is 30 from
        /// the square 100 by 100 lying flat at a height of 130 over it, and nought from the square 200 by 200 lying across
        /// it at half its height.
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown when the box is empty.</exception>
        public double DistanceTo(GeoAabb3 box) => Distance3.DistanceTo(this, box);

        /// <summary>
        /// Calculates the shortest distance between this polygon and an axis-aligned box, within a tolerance: the answer of
        /// <see cref="Distance3.DistanceTo(GeoPolygon3, GeoAabb3, Tolerance)"/>.
        /// </summary>
        /// <remarks>
        /// The answer about the box's body, <c>ToObb().ToSolid()</c>. The box from the origin to (100, 100, 100) is 30 from
        /// the square 100 by 100 lying flat at a height of 130 over it, and nought from the square 200 by 200 lying across
        /// it at half its height.
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown when the box is empty.</exception>
        public double DistanceTo(GeoAabb3 box, Tolerance tolerance) => Distance3.DistanceTo(this, box, tolerance);
    }
}
