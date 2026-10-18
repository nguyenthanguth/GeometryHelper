using System;
using System.Collections.Generic;
using GeometryHelper;
using GeometryHelper.Enums;

namespace GeometryHelper.Geometry
{
    /// <summary>
    /// How far an axis-aligned box is from another shape, and how deep inside one it sits.
    /// </summary>
    public readonly partial struct GeoAabb3
    {
        /// <summary>
        /// Calculates the shortest distance from this box to a point. A point inside is at distance zero,
        /// and an empty box is infinitely far from everything.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        /// <remarks>
        /// This is what prunes a search through a tree of boxes: a box already farther away than the best
        /// answer so far cannot hold anything nearer, so the whole branch below it can be skipped without
        /// being looked at.
        /// </remarks>
        public double DistanceTo(GeoPoint3 point)
        {
            if (IsEmpty)
            {
                return double.PositiveInfinity;
            }

            return GetClosestPointOnBoundary(point).DistanceTo(point);
        }

        /// <summary>
        /// Calculates the shortest distance between this box and another one. Boxes that overlap are at
        /// distance zero, and an empty box is infinitely far from everything.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        /// <remarks>
        /// The gap between two axis-aligned boxes separates on each axis independently, so the distance is
        /// the length of the vector of per-axis gaps. This is what prunes a traversal of two trees at once:
        /// a pair of boxes already farther apart than the best answer so far cannot hold a nearer pair.
        /// </remarks>
        public double DistanceTo(GeoAabb3 other)
        {
            if (IsEmpty || other.IsEmpty)
            {
                return double.PositiveInfinity;
            }

            double gapX = Math.Max(0.0, Math.Max(Min.X - other.Max.X, other.Min.X - Max.X));
            double gapY = Math.Max(0.0, Math.Max(Min.Y - other.Max.Y, other.Min.Y - Max.Y));
            double gapZ = Math.Max(0.0, Math.Max(Min.Z - other.Max.Z, other.Min.Z - Max.Z));

            return Math.Sqrt(gapX * gapX + gapY * gapY + gapZ * gapZ);
        }

        /// <summary>
        /// Gets the distance from this box to a polyline, using the default tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoPolyline3 polyline) => Core.Distance3.DistanceTo(polyline, this);

        /// <summary>
        /// Gets the distance from this box to a polyline, within a tolerance.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        public double DistanceTo(GeoPolyline3 polyline, Tolerance tolerance) => Core.Distance3.DistanceTo(polyline, this, tolerance);

        /// <summary>
        /// Gets the distance from this box to a solid, using the default tolerance.
        /// </summary>
        /// <returns>Nought where the two lie within the global point tolerance of each other, otherwise the gap.</returns>
        public double DistanceTo(GeoSolid3 solid) => Core.Distance3.DistanceTo(solid, this);

        /// <summary>
        /// Gets the distance from this box to a solid, within a tolerance.
        /// </summary>
        /// <returns>Nought where the two lie within the tolerance given of each other, otherwise the gap.</returns>
        public double DistanceTo(GeoSolid3 solid, Tolerance tolerance) => Core.Distance3.DistanceTo(solid, this, tolerance);

        /// <summary>
        /// Calculates the distance from this box to a point, negative for a point within it.
        /// </summary>
        /// <remarks>
        /// The magnitude is the distance to the surface, whichever side of it the point is on, and the sign
        /// says which side: negative inside, nought on it, positive outside. <see cref="DistanceTo(GeoPoint3)"/>
        /// reads this box as filled and so answers nothing at all for a point inside, which is the one
        /// place the two part company.
        /// </remarks>
        public double SignedDistanceTo(GeoPoint3 point) => Core.Distance3.SignedDistanceTo(this, point);

        /// <summary>
        /// Calculates the distance from this box to a point, negative for a point within it, within a tolerance.
        /// </summary>
        public double SignedDistanceTo(GeoPoint3 point, Tolerance tolerance) => Core.Distance3.SignedDistanceTo(this, point, tolerance);

        /// <summary>
        /// Calculates the shortest distance between this axis-aligned box and a segment: the answer of
        /// <see cref="Core.Distance3.DistanceTo(GeoLine3, GeoAabb3)"/>.
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
        public double DistanceTo(GeoLine3 line) => Core.Distance3.DistanceTo(line, this);

        /// <summary>
        /// Calculates the shortest distance between this axis-aligned box and a segment, within a tolerance: the answer of
        /// <see cref="Core.Distance3.DistanceTo(GeoLine3, GeoAabb3, Tolerance)"/>.
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
        public double DistanceTo(GeoLine3 line, Tolerance tolerance) => Core.Distance3.DistanceTo(line, this, tolerance);

        /// <summary>
        /// Calculates the shortest distance between this axis-aligned box and a ray: the answer of
        /// <see cref="Core.Distance3.DistanceTo(GeoRay3, GeoAabb3)"/>.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        /// <remarks>
        /// The answer about the box's body, <c>ToObb().ToSolid()</c>. The box from the origin to (100, 100, 100) is 50 from
        /// the ray from (150, 50, 50) along X, and nought from the same ray turned round.
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown when the box is empty.</exception>
        public double DistanceTo(GeoRay3 ray) => Core.Distance3.DistanceTo(ray, this);

        /// <summary>
        /// Calculates the shortest distance between this axis-aligned box and a ray, within a tolerance: the answer of
        /// <see cref="Core.Distance3.DistanceTo(GeoRay3, GeoAabb3, Tolerance)"/>.
        /// </summary>
        /// <returns>Nought only where they truly meet, otherwise the gap, measured exactly.</returns>
        /// <remarks>
        /// The answer about the box's body, <c>ToObb().ToSolid()</c>. The box from the origin to (100, 100, 100) is 50 from
        /// the ray from (150, 50, 50) along X, and nought from the same ray turned round.
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown when the box is empty.</exception>
        public double DistanceTo(GeoRay3 ray, Tolerance tolerance) => Core.Distance3.DistanceTo(ray, this, tolerance);

        /// <summary>
        /// Calculates the shortest distance between this axis-aligned box and a plane: the answer of
        /// <see cref="Core.Distance3.DistanceTo(GeoPlane3, GeoAabb3)"/>.
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
        public double DistanceTo(GeoPlane3 plane) => Core.Distance3.DistanceTo(plane, this);

        /// <summary>
        /// Calculates the shortest distance between this axis-aligned box and a plane, within a tolerance: the answer of
        /// <see cref="Core.Distance3.DistanceTo(GeoPlane3, GeoAabb3, Tolerance)"/>.
        /// </summary>
        /// <returns>
        /// Nought where the plane crosses the shape or comes within the tolerance given of it, otherwise the gap.
        /// </returns>
        /// <remarks>
        /// The answer about the box's body, <c>ToObb().ToSolid()</c>. The box from the origin to (100, 100, 100) is 30 from
        /// the plane z = 130, and nought from the plane z = 50.
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown when the box is empty.</exception>
        public double DistanceTo(GeoPlane3 plane, Tolerance tolerance) => Core.Distance3.DistanceTo(plane, this, tolerance);

        /// <summary>
        /// Calculates the shortest distance between this axis-aligned box and a triangle: the answer of
        /// <see cref="Core.Distance3.DistanceTo(GeoTriangle3, GeoAabb3)"/>.
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
        public double DistanceTo(GeoTriangle3 triangle) => Core.Distance3.DistanceTo(triangle, this);

        /// <summary>
        /// Calculates the shortest distance between this axis-aligned box and a triangle, within a tolerance: the answer of
        /// <see cref="Core.Distance3.DistanceTo(GeoTriangle3, GeoAabb3, Tolerance)"/>.
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
        public double DistanceTo(GeoTriangle3 triangle, Tolerance tolerance) => Core.Distance3.DistanceTo(triangle, this, tolerance);

        /// <summary>
        /// Calculates the shortest distance between this axis-aligned box and a polygon: the answer of
        /// <see cref="Core.Distance3.DistanceTo(GeoPolygon3, GeoAabb3)"/>.
        /// </summary>
        /// <returns>
        /// Nought only where they truly meet, otherwise the gap, measured exactly. A triangle, polygon or face within a
        /// billionth of a millimetre of the outside of a box reads nought, for the same reason.
        /// </returns>
        /// <remarks>
        /// The answer about the box's body, <c>ToObb().ToSolid()</c>. The box from the origin to (100, 100, 100) is 30 from
        /// the square 100 by 100 lying flat at a height of 130 over it, and nought from the square 200 by 200 lying across
        /// it at half its height.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the box is empty.</exception>
        public double DistanceTo(GeoPolygon3 polygon) => Core.Distance3.DistanceTo(polygon, this);

        /// <summary>
        /// Calculates the shortest distance between this axis-aligned box and a polygon, within a tolerance: the answer of
        /// <see cref="Core.Distance3.DistanceTo(GeoPolygon3, GeoAabb3, Tolerance)"/>.
        /// </summary>
        /// <returns>
        /// Nought only where they truly meet, otherwise the gap, measured exactly. A triangle, polygon or face within a
        /// billionth of a millimetre of the outside of a box reads nought, for the same reason.
        /// </returns>
        /// <remarks>
        /// The answer about the box's body, <c>ToObb().ToSolid()</c>. The box from the origin to (100, 100, 100) is 30 from
        /// the square 100 by 100 lying flat at a height of 130 over it, and nought from the square 200 by 200 lying across
        /// it at half its height.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the box is empty.</exception>
        public double DistanceTo(GeoPolygon3 polygon, Tolerance tolerance) => Core.Distance3.DistanceTo(polygon, this, tolerance);

        /// <summary>
        /// Calculates the shortest distance between this axis-aligned box and a face: the answer of
        /// <see cref="Core.Distance3.DistanceTo(GeoFace3, GeoAabb3)"/>.
        /// </summary>
        /// <returns>
        /// Nought only where they truly meet, otherwise the gap, measured exactly. A triangle, polygon or face within a
        /// billionth of a millimetre of the outside of a box reads nought, for the same reason.
        /// </returns>
        /// <remarks>
        /// The answer about the box's body, <c>ToObb().ToSolid()</c>. The face is read by its material. The box from the
        /// origin to (100, 100, 100) passes through the hole of the square 200 by 200 from (-50, -50, 50) to (150, 150, 50)
        /// with a hole 150 by 150 in its middle and is 25 from its rim.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the box is empty.</exception>
        public double DistanceTo(GeoFace3 face) => Core.Distance3.DistanceTo(face, this);

        /// <summary>
        /// Calculates the shortest distance between this axis-aligned box and a face, within a tolerance: the answer of
        /// <see cref="Core.Distance3.DistanceTo(GeoFace3, GeoAabb3, Tolerance)"/>.
        /// </summary>
        /// <returns>
        /// Nought only where they truly meet, otherwise the gap, measured exactly. A triangle, polygon or face within a
        /// billionth of a millimetre of the outside of a box reads nought, for the same reason.
        /// </returns>
        /// <remarks>
        /// The answer about the box's body, <c>ToObb().ToSolid()</c>. The face is read by its material. The box from the
        /// origin to (100, 100, 100) passes through the hole of the square 200 by 200 from (-50, -50, 50) to (150, 150, 50)
        /// with a hole 150 by 150 in its middle and is 25 from its rim.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the box is empty.</exception>
        public double DistanceTo(GeoFace3 face, Tolerance tolerance) => Core.Distance3.DistanceTo(face, this, tolerance);

        /// <summary>
        /// Calculates the shortest distance between this axis-aligned box and an oriented box: the answer of
        /// <see cref="Core.Distance3.DistanceTo(GeoObb3, GeoAabb3)"/>.
        /// </summary>
        /// <returns>Nought where the two lie within the global point tolerance of each other, otherwise the gap.</returns>
        /// <remarks>
        /// The answer of <see cref="Core.Distance3.DistanceTo(GeoObb3, GeoObb3, Tolerance)"/> with the axis-aligned box as
        /// <c>ToObb()</c>: two oriented boxes are measured as their bodies once the separating axis test has found them
        /// apart. The box from the origin to (100, 100, 100) and the box 100 by 100 by 100 about (250, 50, 50) are 100
        /// apart.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the oriented box is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the box is empty.</exception>
        public double DistanceTo(GeoObb3 box) => Core.Distance3.DistanceTo(box, this);

        /// <summary>
        /// Calculates the shortest distance between this axis-aligned box and an oriented box, within a tolerance: the
        /// answer of <see cref="Core.Distance3.DistanceTo(GeoObb3, GeoAabb3, Tolerance)"/>.
        /// </summary>
        /// <returns>Nought where the two lie within the tolerance given of each other, otherwise the gap.</returns>
        /// <remarks>
        /// The answer of <see cref="Core.Distance3.DistanceTo(GeoObb3, GeoObb3, Tolerance)"/> with the axis-aligned box as
        /// <c>ToObb()</c>: two oriented boxes are measured as their bodies once the separating axis test has found them
        /// apart. The box from the origin to (100, 100, 100) and the box 100 by 100 by 100 about (250, 50, 50) are 100
        /// apart.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the oriented box is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the box is empty.</exception>
        public double DistanceTo(GeoObb3 box, Tolerance tolerance) => Core.Distance3.DistanceTo(box, this, tolerance);
    }
}
