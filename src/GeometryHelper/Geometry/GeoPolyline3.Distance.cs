using System;
using System.Collections.Generic;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Core;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// How far a polyline is from another shape, and how deep inside one it sits.
    /// </summary>
    public sealed partial class GeoPolyline3
    {
        /// <summary>
        /// Calculates the shortest distance from this polyline to a point.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoPoint3 point) => Distance3.DistanceTo(this, point);

        /// <summary>
        /// Calculates the shortest distance from this chain to a line segment.
        /// </summary>
        /// <returns>
        /// Nought only where they truly meet, otherwise the gap, measured exactly. A segment no longer than the global
        /// point tolerance is read as its start point, except where it truly meets the other shape.
        /// </returns>
        public double DistanceTo(GeoLine3 line) => Distance3.DistanceTo(this, line);

        /// <summary>
        /// Calculates the shortest distance from this chain to a line segment, within a tolerance.
        /// </summary>
        /// <returns>
        /// Nought only where they truly meet, otherwise the gap, measured exactly. A segment no longer than the
        /// tolerance given is read as its start point, except where it truly meets the other shape.
        /// </returns>
        public double DistanceTo(GeoLine3 line, Tolerance tolerance) => Distance3.DistanceTo(this, line, tolerance);

        /// <summary>
        /// Calculates the shortest distance from this chain to another chain.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoPolyline3 other) => Distance3.DistanceTo(this, other);

        /// <summary>
        /// Calculates the shortest distance from this chain to another chain, within a tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoPolyline3 other, Tolerance tolerance) => Distance3.DistanceTo(this, other, tolerance);

        /// <summary>
        /// Calculates the shortest distance from this chain to a triangle.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoTriangle3 triangle) => Distance3.DistanceTo(this, triangle);

        /// <summary>
        /// Calculates the shortest distance from this chain to a triangle, within a tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoTriangle3 triangle, Tolerance tolerance) => Distance3.DistanceTo(this, triangle, tolerance);

        /// <summary>
        /// Calculates the shortest distance from this chain to a polygon.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoPolygon3 polygon) => Distance3.DistanceTo(this, polygon);

        /// <summary>
        /// Calculates the shortest distance from this chain to a polygon, within a tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoPolygon3 polygon, Tolerance tolerance) => Distance3.DistanceTo(this, polygon, tolerance);

        /// <summary>
        /// Calculates the shortest distance from this chain to a plane.
        /// </summary>
        /// <returns>
        /// Nought where the plane crosses the shape or comes within the global point tolerance of it, otherwise the
        /// gap.
        /// </returns>
        public double DistanceTo(GeoPlane3 plane) => Distance3.DistanceTo(this, plane);

        /// <summary>
        /// Calculates the shortest distance from this chain to a plane, within a tolerance.
        /// </summary>
        /// <returns>
        /// Nought where the plane crosses the shape or comes within the tolerance given of it, otherwise the gap.
        /// </returns>
        public double DistanceTo(GeoPlane3 plane, Tolerance tolerance) => Distance3.DistanceTo(this, plane, tolerance);

        /// <summary>
        /// Calculates the shortest distance from this chain to an oriented box.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoObb3 box) => Distance3.DistanceTo(this, box);

        /// <summary>
        /// Calculates the shortest distance from this chain to an oriented box, within a tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoObb3 box, Tolerance tolerance) => Distance3.DistanceTo(this, box, tolerance);

        /// <summary>
        /// Calculates the shortest distance from this chain to an axis-aligned box.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoAabb3 box) => Distance3.DistanceTo(this, box);

        /// <summary>
        /// Calculates the shortest distance from this chain to an axis-aligned box, within a tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoAabb3 box, Tolerance tolerance) => Distance3.DistanceTo(this, box, tolerance);

        /// <summary>
        /// Calculates the shortest distance from this chain to a solid.
        /// </summary>
        /// <returns>
        /// Nought only where they truly meet, otherwise the gap, measured exactly. Within a billionth of a millimetre
        /// of the outside of a solid it reads nought, so that rounding in a turned body cannot lose a crossing.
        /// </returns>
        public double DistanceTo(GeoSolid3 solid) => Distance3.DistanceTo(solid, this);

        /// <summary>
        /// Calculates the shortest distance from this chain to a solid, within a tolerance.
        /// </summary>
        /// <returns>
        /// Nought only where they truly meet, otherwise the gap, measured exactly. Within a billionth of a millimetre
        /// of the outside of a solid it reads nought, so that rounding in a turned body cannot lose a crossing.
        /// </returns>
        public double DistanceTo(GeoSolid3 solid, Tolerance tolerance) => Distance3.DistanceTo(solid, this, tolerance);

        /// <summary>
        /// Calculates the shortest distance between this polyline and a face: the answer of
        /// <see cref="Distance3.DistanceTo(GeoPolyline3, GeoFace3)"/>.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        /// <remarks>
        /// The face is read by its material. Over the square 100 by 100 in the floor with a corner at the origin and a hole
        /// 40 by 40 in its middle, the chain (45, 50, 30), (45, 50, -30), through the hole, is 15 from the rim, and the
        /// chain (20, 20, 30), (20, 20, 80), (50, 50, 80) is 30 above the material.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public double DistanceTo(GeoFace3 face) => Distance3.DistanceTo(this, face);

        /// <summary>
        /// Calculates the shortest distance between this polyline and a face, within a tolerance: the answer of
        /// <see cref="Distance3.DistanceTo(GeoPolyline3, GeoFace3, Tolerance)"/>.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        /// <remarks>
        /// The face is read by its material. Over the square 100 by 100 in the floor with a corner at the origin and a hole
        /// 40 by 40 in its middle, the chain (45, 50, 30), (45, 50, -30), through the hole, is 15 from the rim, and the
        /// chain (20, 20, 30), (20, 20, 80), (50, 50, 80) is 30 above the material.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public double DistanceTo(GeoFace3 face, Tolerance tolerance) => Distance3.DistanceTo(this, face, tolerance);

        /// <summary>
        /// Calculates the shortest distance between this polyline and a ray: the answer of
        /// <see cref="Distance3.DistanceTo(GeoPolyline3, GeoRay3)"/>.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        /// <remarks>
        /// The chain (0, 0, 0), (100, 0, 0), (100, 100, 0) is 50 from the ray from (60, 50, 30) pointing straight up.
        /// </remarks>
        public double DistanceTo(GeoRay3 ray) => Distance3.DistanceTo(this, ray);

        /// <summary>
        /// Calculates the shortest distance between this polyline and a ray, within a tolerance: the answer of
        /// <see cref="Distance3.DistanceTo(GeoPolyline3, GeoRay3, Tolerance)"/>.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        /// <remarks>
        /// The chain (0, 0, 0), (100, 0, 0), (100, 100, 0) is 50 from the ray from (60, 50, 30) pointing straight up.
        /// </remarks>
        public double DistanceTo(GeoRay3 ray, Tolerance tolerance) => Distance3.DistanceTo(this, ray, tolerance);
    }
}
