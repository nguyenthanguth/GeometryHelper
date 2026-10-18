using System;
using System.Collections.Generic;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// How far a plane is from another shape, and how deep inside one it sits.
    /// </summary>
    public readonly partial struct GeoPlane3
    {
        /// <summary>
        /// Calculates the unsigned distance from a point to the plane.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoPoint3 point) => Distance3.DistanceTo(this, point);

        /// <summary>
        /// Calculates the shortest distance from a line segment to the plane. A segment that crosses the
        /// plane is at distance zero.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoLine3 line) => Distance3.DistanceTo(this, line);

        /// <summary>
        /// Calculates the signed distance from a point to the plane.
        /// </summary>
        /// <param name="point">The target point.</param>
        /// <returns>Positive if the point is on the side the normal points towards, negative otherwise.</returns>
        public double SignedDistanceTo(GeoPoint3 point) => Origin.GetVectorTo(point).DotProduct(Normal);

        /// <summary>
        /// Gets the distance from this plane to a polyline, using the default tolerance.
        /// </summary>
        /// <returns>
        /// Nought where the plane crosses the shape or comes within the global point tolerance of it, otherwise the
        /// gap.
        /// </returns>
        public double DistanceTo(GeoPolyline3 polyline) => Distance3.DistanceTo(polyline, this);

        /// <summary>
        /// Gets the distance from this plane to a polyline, within a tolerance.
        /// </summary>
        /// <returns>
        /// Nought where the plane crosses the shape or comes within the tolerance given of it, otherwise the gap.
        /// </returns>
        public double DistanceTo(GeoPolyline3 polyline, Tolerance tolerance) => Distance3.DistanceTo(polyline, this, tolerance);

        /// <summary>
        /// Gets the distance from this plane to a solid, using the default tolerance.
        /// </summary>
        /// <returns>
        /// Nought where the plane crosses the shape or comes within the global point tolerance of it, otherwise the
        /// gap.
        /// </returns>
        public double DistanceTo(GeoSolid3 solid) => Distance3.DistanceTo(solid, this);

        /// <summary>
        /// Gets the distance from this plane to a solid, within a tolerance.
        /// </summary>
        /// <returns>
        /// Nought where the plane crosses the shape or comes within the tolerance given of it, otherwise the gap.
        /// </returns>
        public double DistanceTo(GeoSolid3 solid, Tolerance tolerance) => Distance3.DistanceTo(solid, this, tolerance);

        /// <summary>
        /// Gets the distance from this plane to a plane, using the default tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoPlane3 other) => Distance3.DistanceTo(this, other);

        /// <summary>
        /// Gets the distance from this plane to a plane, within a tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoPlane3 other, Tolerance tolerance) => Distance3.DistanceTo(this, other, tolerance);

        /// <summary>
        /// Calculates the shortest distance between this plane and a face: the answer of
        /// <see cref="Distance3.DistanceTo(GeoPlane3, GeoFace3)"/>.
        /// </summary>
        /// <returns>
        /// Nought where the plane crosses the shape or comes within the global point tolerance of it, otherwise the
        /// gap.
        /// </returns>
        /// <remarks>
        /// The heights of the corners of the outline decide, as for a chain. The square 100 by 100 with a hole 40 by 40 in
        /// its middle, stood upright along y = 50 from z = 10 to 110, is 10 above the floor plane; stood from z = -50 to
        /// 50, it crosses it and is nought away.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public double DistanceTo(GeoFace3 face) => Distance3.DistanceTo(this, face);

        /// <summary>
        /// Calculates the shortest distance between this plane and a face, within a tolerance: the answer of
        /// <see cref="Distance3.DistanceTo(GeoPlane3, GeoFace3, Tolerance)"/>.
        /// </summary>
        /// <returns>
        /// Nought where the plane crosses the shape or comes within the tolerance given of it, otherwise the gap.
        /// </returns>
        /// <remarks>
        /// The heights of the corners of the outline decide, as for a chain. The square 100 by 100 with a hole 40 by 40 in
        /// its middle, stood upright along y = 50 from z = 10 to 110, is 10 above the floor plane; stood from z = -50 to
        /// 50, it crosses it and is nought away.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public double DistanceTo(GeoFace3 face, Tolerance tolerance) => Distance3.DistanceTo(this, face, tolerance);

        /// <summary>
        /// Calculates the shortest distance between this plane and a polygon: the answer of
        /// <see cref="Distance3.DistanceTo(GeoPlane3, GeoPolygon3)"/>.
        /// </summary>
        /// <returns>
        /// Nought where the plane crosses the shape or comes within the global point tolerance of it, otherwise the
        /// gap.
        /// </returns>
        /// <remarks>
        /// The heights of the corners decide, as for a chain. A square 100 by 100 standing upright along y = 50 from z = 10
        /// to 110 is 10 above the floor plane; one standing from z = -50 crosses it and is nought away.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public double DistanceTo(GeoPolygon3 polygon) => Distance3.DistanceTo(this, polygon);

        /// <summary>
        /// Calculates the shortest distance between this plane and a polygon, within a tolerance: the answer of
        /// <see cref="Distance3.DistanceTo(GeoPlane3, GeoPolygon3, Tolerance)"/>.
        /// </summary>
        /// <returns>
        /// Nought where the plane crosses the shape or comes within the tolerance given of it, otherwise the gap.
        /// </returns>
        /// <remarks>
        /// The heights of the corners decide, as for a chain. A square 100 by 100 standing upright along y = 50 from z = 10
        /// to 110 is 10 above the floor plane; one standing from z = -50 crosses it and is nought away.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public double DistanceTo(GeoPolygon3 polygon, Tolerance tolerance) => Distance3.DistanceTo(this, polygon, tolerance);

        /// <summary>
        /// Calculates the shortest distance between this plane and a ray: the answer of
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
        public double DistanceTo(GeoRay3 ray) => Distance3.DistanceTo(ray, this);

        /// <summary>
        /// Calculates the shortest distance between this plane and a ray, within a tolerance: the answer of
        /// <see cref="Distance3.DistanceTo(GeoRay3, GeoPlane3, Tolerance)"/>.
        /// </summary>
        /// <returns>
        /// Nought where the plane crosses the shape or comes within the tolerance given of it, otherwise the gap.
        /// </returns>
        /// <remarks>
        /// The ray from (0, 0, 10) pointing straight up is 10 above the floor plane; pointing straight down it reaches it
        /// and is nought away.
        /// </remarks>
        public double DistanceTo(GeoRay3 ray, Tolerance tolerance) => Distance3.DistanceTo(ray, this, tolerance);

        /// <summary>
        /// Calculates the shortest distance between this plane and a triangle: the answer of
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
        public double DistanceTo(GeoTriangle3 triangle) => Distance3.DistanceTo(this, triangle);

        /// <summary>
        /// Calculates the shortest distance between this plane and a triangle, within a tolerance: the answer of
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
        public double DistanceTo(GeoTriangle3 triangle, Tolerance tolerance) => Distance3.DistanceTo(this, triangle, tolerance);

        /// <summary>
        /// Calculates the shortest distance between this plane and an oriented box: the answer of
        /// <see cref="Distance3.DistanceTo(GeoPlane3, GeoObb3)"/>.
        /// </summary>
        /// <returns>
        /// Nought where the plane crosses the shape or comes within the global point tolerance of it, otherwise the
        /// gap.
        /// </returns>
        /// <remarks>
        /// The answer about the box's body, <see cref="GeoObb3.ToSolid"/>. The box 100 by 100 by 100 about (50, 50, 50) is
        /// 30 from the plane z = 130, and nought from the plane z = 50.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the oriented box is null.</exception>
        public double DistanceTo(GeoObb3 box) => Distance3.DistanceTo(this, box);

        /// <summary>
        /// Calculates the shortest distance between this plane and an oriented box, within a tolerance: the answer of
        /// <see cref="Distance3.DistanceTo(GeoPlane3, GeoObb3, Tolerance)"/>.
        /// </summary>
        /// <returns>
        /// Nought where the plane crosses the shape or comes within the tolerance given of it, otherwise the gap.
        /// </returns>
        /// <remarks>
        /// The answer about the box's body, <see cref="GeoObb3.ToSolid"/>. The box 100 by 100 by 100 about (50, 50, 50) is
        /// 30 from the plane z = 130, and nought from the plane z = 50.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the oriented box is null.</exception>
        public double DistanceTo(GeoObb3 box, Tolerance tolerance) => Distance3.DistanceTo(this, box, tolerance);

        /// <summary>
        /// Calculates the shortest distance between this plane and an axis-aligned box: the answer of
        /// <see cref="Distance3.DistanceTo(GeoPlane3, GeoAabb3)"/>.
        /// </summary>
        /// <returns>
        /// Nought where the plane crosses the shape or comes within the global point tolerance of it, otherwise the
        /// gap.
        /// </returns>
        /// <remarks>
        /// The answer about the box's body, <c>ToObb().ToSolid()</c>. The box from the origin to (100, 100, 100) is 30 from
        /// the plane z = 130, and nought from the plane z = 50.
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown when the box is empty.</exception>
        public double DistanceTo(GeoAabb3 box) => Distance3.DistanceTo(this, box);

        /// <summary>
        /// Calculates the shortest distance between this plane and an axis-aligned box, within a tolerance: the answer of
        /// <see cref="Distance3.DistanceTo(GeoPlane3, GeoAabb3, Tolerance)"/>.
        /// </summary>
        /// <returns>
        /// Nought where the plane crosses the shape or comes within the tolerance given of it, otherwise the gap.
        /// </returns>
        /// <remarks>
        /// The answer about the box's body, <c>ToObb().ToSolid()</c>. The box from the origin to (100, 100, 100) is 30 from
        /// the plane z = 130, and nought from the plane z = 50.
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown when the box is empty.</exception>
        public double DistanceTo(GeoAabb3 box, Tolerance tolerance) => Distance3.DistanceTo(this, box, tolerance);
    }
}
