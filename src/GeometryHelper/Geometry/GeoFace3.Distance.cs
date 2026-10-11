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
    public sealed partial class GeoFace3
    {
        /// <summary>
        /// Calculates the shortest distance from this face to a point.
        /// </summary>
        /// <remarks>
        /// The face is read as material with holes in it, so a point over a hole is over nothing and is
        /// measured to the rim of that hole rather than to the surface that is not there behind it.
        /// </remarks>
        public double DistanceTo(GeoPoint3 point) => Distance3.DistanceTo(this, point);

        /// <summary>
        /// Calculates the shortest distance from this face to a point, within a tolerance.
        /// </summary>
        public double DistanceTo(GeoPoint3 point, Tolerance tolerance) => Distance3.DistanceTo(this, point, tolerance);

        /// <summary>
        /// Calculates the shortest distance between this face and another one: the answer of
        /// <see cref="Distance3.DistanceTo(GeoFace3, GeoFace3)"/>.
        /// </summary>
        /// <remarks>
        /// The square 100 by 100 in the floor with a corner at the origin and a hole 40 by 40 in its middle is 10 from the
        /// square 20 by 20 lying in the hole about (50, 50), which touches nothing, and nought from one lying on its
        /// material.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public double DistanceTo(GeoFace3 other) => Distance3.DistanceTo(this, other);

        /// <summary>
        /// Calculates the shortest distance between this face and another one, within a tolerance: the answer of
        /// <see cref="Distance3.DistanceTo(GeoFace3, GeoFace3, Tolerance)"/>.
        /// </summary>
        /// <remarks>
        /// The square 100 by 100 in the floor with a corner at the origin and a hole 40 by 40 in its middle is 10 from the
        /// square 20 by 20 lying in the hole about (50, 50), which touches nothing, and nought from one lying on its
        /// material.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public double DistanceTo(GeoFace3 other, Tolerance tolerance) => Distance3.DistanceTo(this, other, tolerance);

        /// <summary>
        /// Calculates the shortest distance between this face and a segment: the answer of
        /// <see cref="Distance3.DistanceTo(GeoLine3, GeoFace3)"/>.
        /// </summary>
        /// <remarks>
        /// The face is read by its material. Over the square 100 by 100 in the floor with a corner at the origin and a hole
        /// 40 by 40 in its middle, the segment from (45, 50, -10) to (45, 50, 10), through the hole, is 15 from the rim;
        /// the segment from (20, 20, -10) to (20, 20, 10) crosses the material and is nought from it.
        /// </remarks>
        public double DistanceTo(GeoLine3 line) => Distance3.DistanceTo(line, this);

        /// <summary>
        /// Calculates the shortest distance between this face and a segment, within a tolerance: the answer of
        /// <see cref="Distance3.DistanceTo(GeoLine3, GeoFace3, Tolerance)"/>.
        /// </summary>
        /// <remarks>
        /// The face is read by its material. Over the square 100 by 100 in the floor with a corner at the origin and a hole
        /// 40 by 40 in its middle, the segment from (45, 50, -10) to (45, 50, 10), through the hole, is 15 from the rim;
        /// the segment from (20, 20, -10) to (20, 20, 10) crosses the material and is nought from it.
        /// </remarks>
        public double DistanceTo(GeoLine3 line, Tolerance tolerance) => Distance3.DistanceTo(line, this, tolerance);

        /// <summary>
        /// Calculates the shortest distance between this face and a plane: the answer of
        /// <see cref="Distance3.DistanceTo(GeoPlane3, GeoFace3)"/>.
        /// </summary>
        /// <remarks>
        /// The heights of the corners of the outline decide, as for a chain. The square 100 by 100 with a hole 40 by 40 in
        /// its middle, stood upright along y = 50 from z = 10 to 110, is 10 above the floor plane; stood from z = -50 to
        /// 50, it crosses it and is nought away.
        /// </remarks>
        public double DistanceTo(GeoPlane3 plane) => Distance3.DistanceTo(plane, this);

        /// <summary>
        /// Calculates the shortest distance between this face and a plane, within a tolerance: the answer of
        /// <see cref="Distance3.DistanceTo(GeoPlane3, GeoFace3, Tolerance)"/>.
        /// </summary>
        /// <remarks>
        /// The heights of the corners of the outline decide, as for a chain. The square 100 by 100 with a hole 40 by 40 in
        /// its middle, stood upright along y = 50 from z = 10 to 110, is 10 above the floor plane; stood from z = -50 to
        /// 50, it crosses it and is nought away.
        /// </remarks>
        public double DistanceTo(GeoPlane3 plane, Tolerance tolerance) => Distance3.DistanceTo(plane, this, tolerance);

        /// <summary>
        /// Calculates the shortest distance between this face and a polygon: the answer of
        /// <see cref="Distance3.DistanceTo(GeoFace3, GeoPolygon3)"/>.
        /// </summary>
        /// <remarks>
        /// The face is read by its material and the polygon as a filled surface. The square 100 by 100 in the floor with a
        /// corner at the origin and a hole 40 by 40 in its middle is 10 from a square 20 by 20 standing upright in the
        /// middle of the hole along y = 50 from x = 40 to 60, and nought from one lying on its material.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public double DistanceTo(GeoPolygon3 polygon) => Distance3.DistanceTo(this, polygon);

        /// <summary>
        /// Calculates the shortest distance between this face and a polygon, within a tolerance: the answer of
        /// <see cref="Distance3.DistanceTo(GeoFace3, GeoPolygon3, Tolerance)"/>.
        /// </summary>
        /// <remarks>
        /// The face is read by its material and the polygon as a filled surface. The square 100 by 100 in the floor with a
        /// corner at the origin and a hole 40 by 40 in its middle is 10 from a square 20 by 20 standing upright in the
        /// middle of the hole along y = 50 from x = 40 to 60, and nought from one lying on its material.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public double DistanceTo(GeoPolygon3 polygon, Tolerance tolerance) => Distance3.DistanceTo(this, polygon, tolerance);

        /// <summary>
        /// Calculates the shortest distance between this face and a polyline: the answer of
        /// <see cref="Distance3.DistanceTo(GeoPolyline3, GeoFace3)"/>.
        /// </summary>
        /// <remarks>
        /// The face is read by its material. Over the square 100 by 100 in the floor with a corner at the origin and a hole
        /// 40 by 40 in its middle, the chain (45, 50, 30), (45, 50, -30), through the hole, is 15 from the rim, and the
        /// chain (20, 20, 30), (20, 20, 80), (50, 50, 80) is 30 above the material.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the polyline is null.</exception>
        public double DistanceTo(GeoPolyline3 polyline) => Distance3.DistanceTo(polyline, this);

        /// <summary>
        /// Calculates the shortest distance between this face and a polyline, within a tolerance: the answer of
        /// <see cref="Distance3.DistanceTo(GeoPolyline3, GeoFace3, Tolerance)"/>.
        /// </summary>
        /// <remarks>
        /// The face is read by its material. Over the square 100 by 100 in the floor with a corner at the origin and a hole
        /// 40 by 40 in its middle, the chain (45, 50, 30), (45, 50, -30), through the hole, is 15 from the rim, and the
        /// chain (20, 20, 30), (20, 20, 80), (50, 50, 80) is 30 above the material.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the polyline is null.</exception>
        public double DistanceTo(GeoPolyline3 polyline, Tolerance tolerance) => Distance3.DistanceTo(polyline, this, tolerance);

        /// <summary>
        /// Calculates the shortest distance between this face and a ray: the answer of
        /// <see cref="Distance3.DistanceTo(GeoRay3, GeoFace3)"/>.
        /// </summary>
        /// <remarks>
        /// The face is read by its material. Over the square 100 by 100 in the floor with a corner at the origin and a hole
        /// 40 by 40 in its middle, the ray from (45, 50, 10) pointing straight down, through the hole, is 15 from the rim;
        /// from (20, 20, 10) it runs into the material and is nought from it.
        /// </remarks>
        public double DistanceTo(GeoRay3 ray) => Distance3.DistanceTo(ray, this);

        /// <summary>
        /// Calculates the shortest distance between this face and a ray, within a tolerance: the answer of
        /// <see cref="Distance3.DistanceTo(GeoRay3, GeoFace3, Tolerance)"/>.
        /// </summary>
        /// <remarks>
        /// The face is read by its material. Over the square 100 by 100 in the floor with a corner at the origin and a hole
        /// 40 by 40 in its middle, the ray from (45, 50, 10) pointing straight down, through the hole, is 15 from the rim;
        /// from (20, 20, 10) it runs into the material and is nought from it.
        /// </remarks>
        public double DistanceTo(GeoRay3 ray, Tolerance tolerance) => Distance3.DistanceTo(ray, this, tolerance);

        /// <summary>
        /// Calculates the shortest distance between this face and a solid: the answer of
        /// <see cref="Distance3.DistanceTo(GeoSolid3, GeoFace3)"/>.
        /// </summary>
        /// <remarks>
        /// The face is read by its material and the body solid through. The box 100 by 100 by 100 with a corner at the
        /// origin passes through the hole of the square 200 by 200 from (-50, -50, 50) to (150, 150, 50) with a hole 150 by
        /// 150 in its middle and is 25 from its rim; the same square without its hole cuts the box and is nought from it.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the solid is null.</exception>
        public double DistanceTo(GeoSolid3 solid) => Distance3.DistanceTo(solid, this);

        /// <summary>
        /// Calculates the shortest distance between this face and a solid, within a tolerance: the answer of
        /// <see cref="Distance3.DistanceTo(GeoSolid3, GeoFace3, Tolerance)"/>.
        /// </summary>
        /// <remarks>
        /// The face is read by its material and the body solid through. The box 100 by 100 by 100 with a corner at the
        /// origin passes through the hole of the square 200 by 200 from (-50, -50, 50) to (150, 150, 50) with a hole 150 by
        /// 150 in its middle and is 25 from its rim; the same square without its hole cuts the box and is nought from it.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the solid is null.</exception>
        public double DistanceTo(GeoSolid3 solid, Tolerance tolerance) => Distance3.DistanceTo(solid, this, tolerance);

        /// <summary>
        /// Calculates the shortest distance between this face and a triangle: the answer of
        /// <see cref="Distance3.DistanceTo(GeoTriangle3, GeoFace3)"/>.
        /// </summary>
        /// <remarks>
        /// The face is read by its material. The triangle with corners (110, 50, 0), (200, 50, 0) and (110, 50, 100),
        /// standing beside the square 100 by 100 in the floor with a corner at the origin and a hole 40 by 40 in its
        /// middle, is 10 from its outline.
        /// </remarks>
        public double DistanceTo(GeoTriangle3 triangle) => Distance3.DistanceTo(triangle, this);

        /// <summary>
        /// Calculates the shortest distance between this face and a triangle, within a tolerance: the answer of
        /// <see cref="Distance3.DistanceTo(GeoTriangle3, GeoFace3, Tolerance)"/>.
        /// </summary>
        /// <remarks>
        /// The face is read by its material. The triangle with corners (110, 50, 0), (200, 50, 0) and (110, 50, 100),
        /// standing beside the square 100 by 100 in the floor with a corner at the origin and a hole 40 by 40 in its
        /// middle, is 10 from its outline.
        /// </remarks>
        public double DistanceTo(GeoTriangle3 triangle, Tolerance tolerance) => Distance3.DistanceTo(triangle, this, tolerance);

        /// <summary>
        /// Calculates the shortest distance between this face and an oriented box: the answer of
        /// <see cref="Distance3.DistanceTo(GeoFace3, GeoObb3)"/>.
        /// </summary>
        /// <remarks>
        /// The answer about the box's body, <see cref="GeoObb3.ToSolid"/>. The face is read by its material. The box 100 by
        /// 100 by 100 about (50, 50, 50) passes through the hole of the square 200 by 200 from (-50, -50, 50) to (150, 150,
        /// 50) with a hole 150 by 150 in its middle and is 25 from its rim.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the oriented box is null.</exception>
        public double DistanceTo(GeoObb3 box) => Distance3.DistanceTo(this, box);

        /// <summary>
        /// Calculates the shortest distance between this face and an oriented box, within a tolerance: the answer of
        /// <see cref="Distance3.DistanceTo(GeoFace3, GeoObb3, Tolerance)"/>.
        /// </summary>
        /// <remarks>
        /// The answer about the box's body, <see cref="GeoObb3.ToSolid"/>. The face is read by its material. The box 100 by
        /// 100 by 100 about (50, 50, 50) passes through the hole of the square 200 by 200 from (-50, -50, 50) to (150, 150,
        /// 50) with a hole 150 by 150 in its middle and is 25 from its rim.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the oriented box is null.</exception>
        public double DistanceTo(GeoObb3 box, Tolerance tolerance) => Distance3.DistanceTo(this, box, tolerance);

        /// <summary>
        /// Calculates the shortest distance between this face and an axis-aligned box: the answer of
        /// <see cref="Distance3.DistanceTo(GeoFace3, GeoAabb3)"/>.
        /// </summary>
        /// <remarks>
        /// The answer about the box's body, <c>ToObb().ToSolid()</c>. The face is read by its material. The box from the
        /// origin to (100, 100, 100) passes through the hole of the square 200 by 200 from (-50, -50, 50) to (150, 150, 50)
        /// with a hole 150 by 150 in its middle and is 25 from its rim.
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown when the box is empty.</exception>
        public double DistanceTo(GeoAabb3 box) => Distance3.DistanceTo(this, box);

        /// <summary>
        /// Calculates the shortest distance between this face and an axis-aligned box, within a tolerance: the answer of
        /// <see cref="Distance3.DistanceTo(GeoFace3, GeoAabb3, Tolerance)"/>.
        /// </summary>
        /// <remarks>
        /// The answer about the box's body, <c>ToObb().ToSolid()</c>. The face is read by its material. The box from the
        /// origin to (100, 100, 100) passes through the hole of the square 200 by 200 from (-50, -50, 50) to (150, 150, 50)
        /// with a hole 150 by 150 in its middle and is 25 from its rim.
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown when the box is empty.</exception>
        public double DistanceTo(GeoAabb3 box, Tolerance tolerance) => Distance3.DistanceTo(this, box, tolerance);
    }
}
