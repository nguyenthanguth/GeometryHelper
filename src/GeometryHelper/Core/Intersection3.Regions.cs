using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    /// <summary>
    /// Where two flat regions in space cross, where a plane crosses one, and where a ray crosses a ray, a plane or a
    /// region, read as a list.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Two regions in planes that are not one.</b> A triangle, a polygon and a face are read filled, a face by its
    /// material. Two of them can only share points on the line their planes share, and each stretch of that line they
    /// share ends on the boundary of one of them, so the ends are where an edge of either crosses the other region.
    /// That is the segment against a region the library already answers, asked once for each edge, the first
    /// region's edges first, ring by ring for a face, and a point found twice is named once. A square 100 by 100 lying
    /// in the floor with a corner at the origin, and one standing upright across it along y = 50 from z = -50 to 50,
    /// cross at (100, 50, 0) and (0, 50, 0). An edge the segment test refuses, as it refuses one climbing no more than
    /// the planar tolerance, is read by the signs of its ends' heights instead, so a square tilted a tenth of a degree
    /// with each side cut into 80 pieces crosses where the uncut square does.
    /// </para>
    /// <para>
    /// <b>Two regions in one plane.</b> Where the normals are parallel within the angle tolerance and either region
    /// lies wholly in the plane of the other within the planar tolerance, every edge runs in the other's plane and no
    /// segment crosses anything. The two are then laid out in that plane with <see cref="PlanarMap"/>, in the frame of
    /// the first unless only the first lies in the second's plane, and the plane half answers: the crossings of the
    /// outlines, lifted back in its order. Two squares 100 by 100 in the floor, the second moved 50 along X and 50
    /// along Y, cross at (100, 50, 0) and (50, 100, 0).
    /// </para>
    /// <para>
    /// A triangle with no area, by <see cref="GeoTriangle3.IsDegenerate(Tolerance)"/>, has no plane to lay a pair out in: it
    /// is the segment it has collapsed to, and is crossed where its sides are, as the segment against a triangle reads it.
    /// </para>
    /// </remarks>
    public static partial class Intersection3
    {
        #region Two flat regions

        /// <summary>
        /// Gets the points where two triangles cross, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// The triangle with corners (0, 0, 0), (100, 0, 0) and (0, 100, 0) and the upright one with corners
        /// (0, 50, -50), (100, 50, -50) and (0, 50, 50) cross at (50, 50, 0) and (0, 50, 0).
        /// </remarks>
        public static GeoPoint3[] GetIntersections(GeoTriangle3 triangle1, GeoTriangle3 triangle2) => GetIntersections(triangle1, triangle2, Tolerance.Global);

        /// <summary>
        /// Gets the points where two triangles cross, within a tolerance.
        /// </summary>
        /// <param name="triangle1">The first triangle; its edges are asked first.</param>
        /// <param name="triangle2">The second triangle.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// Where an edge of either crosses the other, the first triangle's edges first, each place named once; where
        /// the two lie in one plane, where their outlines cross, in the order of
        /// <see cref="Triangle2.GetIntersections(GeoTriangle2, GeoTriangle2, Tolerance)"/>.
        /// </returns>
        /// <remarks>
        /// The triangle with corners (0, 0, 0), (100, 0, 0) and (0, 100, 0) and the upright one with corners
        /// (0, 50, -50), (100, 50, -50) and (0, 50, 50) cross at (50, 50, 0) and (0, 50, 0). A triangle with no area is
        /// crossed where its sides are: the sliver with corners (0, 0, 0), (100, 0, 0) and (50, 1E-8, 0) crosses the upright
        /// triangle with corners (50, -10, -10), (50, 10, -10) and (50, 0, 10) at (50, 0, 0).
        /// </remarks>
        public static GeoPoint3[] GetIntersections(GeoTriangle3 triangle1, GeoTriangle3 triangle2, Tolerance tolerance)
        {
            var found = new List<GeoPoint3>();

            // A triangle with no area is the segment it has collapsed to, and has no plane to lay the pair out in: where the
            // sides of either cross the other is the answer.
            if (triangle1.IsDegenerate(tolerance) || triangle2.IsDegenerate(tolerance))
            {
                AddCrossings(found, EdgesOf(triangle1), triangle2, tolerance);
                AddCrossings(found, EdgesOf(triangle2), triangle1, tolerance);

                return found.ToArray();
            }

            Flat first = Flat.Of(triangle1);
            Flat second = Flat.Of(triangle2);

            if (Flat.AreClear(first, second, tolerance))
            {
                return new GeoPoint3[0];
            }

            if (Flat.InOnePlane(first, second, tolerance, out GeoCoordinateSystem3 frame))
            {
                return Lift(frame, Triangle2.GetIntersections(
                    PlanarMap.ProjectToTriangle2(frame, triangle1), PlanarMap.ProjectToTriangle2(frame, triangle2), tolerance));
            }

            AddCrossings(found, EdgesOf(triangle1), triangle2, tolerance);
            AddCrossings(found, EdgesOf(triangle2), triangle1, tolerance);

            return found.ToArray();
        }

        /// <summary>
        /// Tries to find where two triangles cross, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// The triangle with corners (0, 0, 0), (100, 0, 0) and (0, 100, 0) and the upright one with corners
        /// (0, 50, -50), (100, 50, -50) and (0, 50, 50) cross at two points.
        /// </remarks>
        public static bool TryIntersectWith(GeoTriangle3 triangle1, GeoTriangle3 triangle2, out GeoPoint3[] intersections)
            => TryIntersectWith(triangle1, triangle2, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where two triangles cross, within a tolerance.
        /// </summary>
        /// <param name="triangle1">The first triangle.</param>
        /// <param name="triangle2">The second triangle.</param>
        /// <param name="intersections">The points of <see cref="GetIntersections(GeoTriangle3, GeoTriangle3, Tolerance)"/>, in the same order; empty when there are none.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when they cross anywhere; otherwise, false.</returns>
        /// <remarks>
        /// The triangle with corners (0, 0, 0), (100, 0, 0) and (0, 100, 0) and the upright one with corners
        /// (0, 50, -50), (100, 50, -50) and (0, 50, 50) cross at two points.
        /// </remarks>
        public static bool TryIntersectWith(GeoTriangle3 triangle1, GeoTriangle3 triangle2, out GeoPoint3[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(triangle1, triangle2, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Gets the points where a triangle and a polygon cross, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// The triangle with corners (0, 0, 0), (100, 0, 0) and (0, 100, 0) and the square 100 by 100 standing upright
        /// across it along y = 50 from z = -50 to 50 cross at (50, 50, 0) and (0, 50, 0).
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public static GeoPoint3[] GetIntersections(GeoTriangle3 triangle, GeoPolygon3 polygon) => GetIntersections(triangle, polygon, Tolerance.Global);

        /// <summary>
        /// Gets the points where a triangle and a polygon cross, within a tolerance.
        /// </summary>
        /// <param name="triangle">The triangle; its edges are asked first.</param>
        /// <param name="polygon">The polygon.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// Where an edge of either crosses the other, the triangle's edges first, each place named once; where the two
        /// lie in one plane, where their outlines cross, in the order of
        /// <see cref="Triangle2.GetIntersections(GeoTriangle2, GeoPolygon2, Tolerance)"/>.
        /// </returns>
        /// <remarks>
        /// The triangle with corners (0, 0, 0), (100, 0, 0) and (0, 100, 0) and the square 100 by 100 standing upright
        /// across it along y = 50 from z = -50 to 50 cross at (50, 50, 0) and (0, 50, 0).
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public static GeoPoint3[] GetIntersections(GeoTriangle3 triangle, GeoPolygon3 polygon, Tolerance tolerance)
        {
            if (polygon == null)
            {
                throw new ArgumentNullException(nameof(polygon));
            }

            var found = new List<GeoPoint3>();

            // A triangle with no area is the segment it has collapsed to: where its sides cross the polygon, and the polygon's
            // edges its sides.
            if (triangle.IsDegenerate(tolerance))
            {
                AddCrossings(found, EdgesOf(triangle), polygon, tolerance);
                AddCrossings(found, EdgesOf(polygon), triangle, tolerance);

                return found.ToArray();
            }

            Flat first = Flat.Of(triangle);
            Flat second = Flat.Of(polygon);

            if (Flat.AreClear(first, second, tolerance))
            {
                return new GeoPoint3[0];
            }

            if (Flat.InOnePlane(first, second, tolerance, out GeoCoordinateSystem3 frame)
                && PlanarMap.TryProjectToPolygon2(frame, polygon, tolerance, out GeoPolygon2 flatPolygon))
            {
                return Lift(frame, Triangle2.GetIntersections(
                    PlanarMap.ProjectToTriangle2(frame, triangle),
                    flatPolygon,
                    tolerance));
            }

            AddCrossings(found, EdgesOf(triangle), polygon, tolerance);
            AddCrossings(found, EdgesOf(polygon), triangle, tolerance);

            return found.ToArray();
        }

        /// <summary>
        /// Tries to find where a triangle and a polygon cross, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// The triangle with corners (0, 0, 0), (100, 0, 0) and (0, 100, 0) and the square 100 by 100 standing upright
        /// across it along y = 50 from z = -50 to 50 cross at two points.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public static bool TryIntersectWith(GeoTriangle3 triangle, GeoPolygon3 polygon, out GeoPoint3[] intersections)
            => TryIntersectWith(triangle, polygon, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where a triangle and a polygon cross, within a tolerance.
        /// </summary>
        /// <param name="triangle">The triangle.</param>
        /// <param name="polygon">The polygon.</param>
        /// <param name="intersections">The points of <see cref="GetIntersections(GeoTriangle3, GeoPolygon3, Tolerance)"/>, in the same order; empty when there are none.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when they cross anywhere; otherwise, false.</returns>
        /// <remarks>
        /// The triangle with corners (0, 0, 0), (100, 0, 0) and (0, 100, 0) and the square 100 by 100 standing upright
        /// across it along y = 50 from z = -50 to 50 cross at two points.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public static bool TryIntersectWith(GeoTriangle3 triangle, GeoPolygon3 polygon, out GeoPoint3[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(triangle, polygon, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Gets the points where a triangle and the material of a face cross, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// A triangle standing upright in the middle of a hole of the face crosses nothing: the square 100 by 100 in
        /// the floor with a hole 40 by 40 in its middle, and the triangle with corners (40, 50, -10), (60, 50, -10)
        /// and (50, 50, 10), do not meet.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public static GeoPoint3[] GetIntersections(GeoTriangle3 triangle, GeoFace3 face) => GetIntersections(triangle, face, Tolerance.Global);

        /// <summary>
        /// Gets the points where a triangle and the material of a face cross, within a tolerance.
        /// </summary>
        /// <param name="triangle">The triangle; its edges are asked first.</param>
        /// <param name="face">The face, read by its material.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// Where an edge of the triangle crosses the material, then where an edge of the face, outline first and then
        /// the rim of each hole, crosses the triangle, each place named once; where the two lie in one plane, where the
        /// triangle's outline crosses the face's rims, in the order of
        /// <see cref="Triangle2.GetIntersections(GeoTriangle2, GeoFace2, Tolerance)"/>.
        /// </returns>
        /// <remarks>
        /// A triangle standing upright in the middle of a hole of the face crosses nothing: the square 100 by 100 in
        /// the floor with a hole 40 by 40 in its middle, and the triangle with corners (40, 50, -10), (60, 50, -10)
        /// and (50, 50, 10), do not meet.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public static GeoPoint3[] GetIntersections(GeoTriangle3 triangle, GeoFace3 face, Tolerance tolerance)
        {
            if (face == null)
            {
                throw new ArgumentNullException(nameof(face));
            }

            var found = new List<GeoPoint3>();

            // A triangle with no area is the segment it has collapsed to: where its sides cross the material, and the face's
            // edges its sides.
            if (triangle.IsDegenerate(tolerance))
            {
                AddCrossings(found, EdgesOf(triangle), face, tolerance);
                AddCrossings(found, EdgesOf(face), triangle, tolerance);

                return found.ToArray();
            }

            Flat first = Flat.Of(triangle);
            Flat second = Flat.Of(face);

            if (Flat.AreClear(first, second, tolerance))
            {
                return new GeoPoint3[0];
            }

            if (Flat.InOnePlane(first, second, tolerance, out GeoCoordinateSystem3 frame)
                && PlanarMap.TryProjectToFace2(frame, face, tolerance, out GeoFace2 flatFace))
            {
                return Lift(frame, Triangle2.GetIntersections(
                    PlanarMap.ProjectToTriangle2(frame, triangle),
                    flatFace,
                    tolerance));
            }

            AddCrossings(found, EdgesOf(triangle), face, tolerance);
            AddCrossings(found, EdgesOf(face), triangle, tolerance);

            return found.ToArray();
        }

        /// <summary>
        /// Tries to find where a triangle and the material of a face cross, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// The square 100 by 100 in the floor with a hole 40 by 40 in its middle, and the triangle with corners
        /// (40, 50, -10), (60, 50, -10) and (50, 50, 10) standing in the hole, do not meet, and the answer is false.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public static bool TryIntersectWith(GeoTriangle3 triangle, GeoFace3 face, out GeoPoint3[] intersections)
            => TryIntersectWith(triangle, face, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where a triangle and the material of a face cross, within a tolerance.
        /// </summary>
        /// <param name="triangle">The triangle.</param>
        /// <param name="face">The face, read by its material.</param>
        /// <param name="intersections">The points of <see cref="GetIntersections(GeoTriangle3, GeoFace3, Tolerance)"/>, in the same order; empty when there are none.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when they cross anywhere; otherwise, false.</returns>
        /// <remarks>
        /// The square 100 by 100 in the floor with a hole 40 by 40 in its middle, and the triangle with corners
        /// (40, 50, -10), (60, 50, -10) and (50, 50, 10) standing in the hole, do not meet, and the answer is false.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public static bool TryIntersectWith(GeoTriangle3 triangle, GeoFace3 face, out GeoPoint3[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(triangle, face, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Gets the points where two polygons cross, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// A square 100 by 100 lying in the floor with a corner at the origin, and one standing upright across it along
        /// y = 50 from z = -50 to 50, cross at (100, 50, 0) and (0, 50, 0).
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when either polygon is null.</exception>
        public static GeoPoint3[] GetIntersections(GeoPolygon3 polygon1, GeoPolygon3 polygon2) => GetIntersections(polygon1, polygon2, Tolerance.Global);

        /// <summary>
        /// Gets the points where two polygons cross, within a tolerance.
        /// </summary>
        /// <param name="polygon1">The first polygon; its edges are asked first.</param>
        /// <param name="polygon2">The second polygon.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// Where an edge of either crosses the other, the first polygon's edges first, each place named once; where
        /// the two lie in one plane, where their outlines cross, in the order of
        /// <see cref="Intersection2.GetIntersections(GeoPolygon2, GeoPolygon2, Tolerance)"/>.
        /// </returns>
        /// <remarks>
        /// A square 100 by 100 lying in the floor with a corner at the origin, and one standing upright across it along
        /// y = 50 from z = -50 to 50, cross at (100, 50, 0) and (0, 50, 0). Two squares 100 by 100 in the floor, the
        /// second moved 50 along X and 50 along Y, cross at (100, 50, 0) and (50, 100, 0).
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when either polygon is null.</exception>
        public static GeoPoint3[] GetIntersections(GeoPolygon3 polygon1, GeoPolygon3 polygon2, Tolerance tolerance)
        {
            if (polygon1 == null)
            {
                throw new ArgumentNullException(nameof(polygon1));
            }

            if (polygon2 == null)
            {
                throw new ArgumentNullException(nameof(polygon2));
            }

            Flat first = Flat.Of(polygon1);
            Flat second = Flat.Of(polygon2);

            if (Flat.AreClear(first, second, tolerance))
            {
                return new GeoPoint3[0];
            }

            if (Flat.InOnePlane(first, second, tolerance, out GeoCoordinateSystem3 frame)
                && PlanarMap.TryProjectToPolygon2(frame, polygon1, tolerance, out GeoPolygon2 flatPolygon1)
                && PlanarMap.TryProjectToPolygon2(frame, polygon2, tolerance, out GeoPolygon2 flatPolygon2))
            {
                return Lift(frame, Intersection2.GetIntersections(
                    flatPolygon1,
                    flatPolygon2,
                    tolerance));
            }

            var found = new List<GeoPoint3>();

            AddCrossings(found, EdgesOf(polygon1), polygon2, tolerance);
            AddCrossings(found, EdgesOf(polygon2), polygon1, tolerance);

            return found.ToArray();
        }

        /// <summary>
        /// Tries to find where two polygons cross, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// A square 100 by 100 lying in the floor with a corner at the origin, and one standing upright across it along
        /// y = 50 from z = -50 to 50, cross at two points.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when either polygon is null.</exception>
        public static bool TryIntersectWith(GeoPolygon3 polygon1, GeoPolygon3 polygon2, out GeoPoint3[] intersections)
            => TryIntersectWith(polygon1, polygon2, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where two polygons cross, within a tolerance.
        /// </summary>
        /// <param name="polygon1">The first polygon.</param>
        /// <param name="polygon2">The second polygon.</param>
        /// <param name="intersections">The points of <see cref="GetIntersections(GeoPolygon3, GeoPolygon3, Tolerance)"/>, in the same order; empty when there are none.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when they cross anywhere; otherwise, false.</returns>
        /// <remarks>
        /// A square 100 by 100 lying in the floor with a corner at the origin, and one standing upright across it along
        /// y = 50 from z = -50 to 50, cross at two points.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when either polygon is null.</exception>
        public static bool TryIntersectWith(GeoPolygon3 polygon1, GeoPolygon3 polygon2, out GeoPoint3[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(polygon1, polygon2, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Gets the points where the material of a face and a polygon cross, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// The square 100 by 100 in the floor with a hole 40 by 40 in its middle, and a square 100 by 100 standing
        /// upright across it along y = 50 from z = -50 to 50, cross where the material ends: (100, 50, 0), (0, 50, 0),
        /// (70, 50, 0) and (30, 50, 0).
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the face or the polygon is null.</exception>
        public static GeoPoint3[] GetIntersections(GeoFace3 face, GeoPolygon3 polygon) => GetIntersections(face, polygon, Tolerance.Global);

        /// <summary>
        /// Gets the points where the material of a face and a polygon cross, within a tolerance.
        /// </summary>
        /// <param name="face">The face, read by its material; its edges are asked first, outline and then holes.</param>
        /// <param name="polygon">The polygon.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// Where an edge of the face crosses the polygon, then where an edge of the polygon crosses the material, each
        /// place named once; where the two lie in one plane, where the polygon's outline crosses the face's rims, in
        /// the order of <see cref="Face2.GetIntersections(GeoFace2, GeoPolygon2, Tolerance)"/>.
        /// </returns>
        /// <remarks>
        /// The square 100 by 100 in the floor with a hole 40 by 40 in its middle, and a square 100 by 100 standing
        /// upright across it along y = 50 from z = -50 to 50, cross where the material ends: (100, 50, 0), (0, 50, 0),
        /// (70, 50, 0) and (30, 50, 0).
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the face or the polygon is null.</exception>
        public static GeoPoint3[] GetIntersections(GeoFace3 face, GeoPolygon3 polygon, Tolerance tolerance)
        {
            if (face == null)
            {
                throw new ArgumentNullException(nameof(face));
            }

            if (polygon == null)
            {
                throw new ArgumentNullException(nameof(polygon));
            }

            Flat first = Flat.Of(face);
            Flat second = Flat.Of(polygon);

            if (Flat.AreClear(first, second, tolerance))
            {
                return new GeoPoint3[0];
            }

            if (Flat.InOnePlane(first, second, tolerance, out GeoCoordinateSystem3 frame)
                && PlanarMap.TryProjectToFace2(frame, face, tolerance, out GeoFace2 flatFace)
                && PlanarMap.TryProjectToPolygon2(frame, polygon, tolerance, out GeoPolygon2 flatPolygon))
            {
                return Lift(frame, Face2.GetIntersections(
                    flatFace,
                    flatPolygon,
                    tolerance));
            }

            var found = new List<GeoPoint3>();

            AddCrossings(found, EdgesOf(face), polygon, tolerance);
            AddCrossings(found, EdgesOf(polygon), face, tolerance);

            return found.ToArray();
        }

        /// <summary>
        /// Tries to find where the material of a face and a polygon cross, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// The square 100 by 100 in the floor with a hole 40 by 40 in its middle, and a square 100 by 100 standing
        /// upright across it along y = 50 from z = -50 to 50, cross at four points.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the face or the polygon is null.</exception>
        public static bool TryIntersectWith(GeoFace3 face, GeoPolygon3 polygon, out GeoPoint3[] intersections)
            => TryIntersectWith(face, polygon, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where the material of a face and a polygon cross, within a tolerance.
        /// </summary>
        /// <param name="face">The face, read by its material.</param>
        /// <param name="polygon">The polygon.</param>
        /// <param name="intersections">The points of <see cref="GetIntersections(GeoFace3, GeoPolygon3, Tolerance)"/>, in the same order; empty when there are none.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when they cross anywhere; otherwise, false.</returns>
        /// <remarks>
        /// The square 100 by 100 in the floor with a hole 40 by 40 in its middle, and a square 100 by 100 standing
        /// upright across it along y = 50 from z = -50 to 50, cross at four points.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the face or the polygon is null.</exception>
        public static bool TryIntersectWith(GeoFace3 face, GeoPolygon3 polygon, out GeoPoint3[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(face, polygon, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Gets the points where the materials of two faces cross, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// The square 100 by 100 in the floor with a hole 40 by 40 in its middle, and a square 100 by 100 standing
        /// upright across it along y = 50 from z = -50 to 50 with a hole 40 by 40 in its middle, cross where both
        /// materials end: (100, 50, 0), (0, 50, 0), (70, 50, 0) and (30, 50, 0). A face standing wholly in the hole
        /// crosses nothing.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when either face is null.</exception>
        public static GeoPoint3[] GetIntersections(GeoFace3 face1, GeoFace3 face2) => GetIntersections(face1, face2, Tolerance.Global);

        /// <summary>
        /// Gets the points where the materials of two faces cross, within a tolerance.
        /// </summary>
        /// <param name="face1">The first face, read by its material; its edges are asked first, outline and then holes.</param>
        /// <param name="face2">The second face, read by its material.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// Where an edge of either crosses the other's material, the first face's edges first, each place named once;
        /// where the two lie in one plane, where their rims cross, in the order of
        /// <see cref="Face2.GetIntersections(GeoFace2, GeoFace2, Tolerance)"/>.
        /// </returns>
        /// <remarks>
        /// The square 100 by 100 in the floor with a hole 40 by 40 in its middle, and a square 100 by 100 standing
        /// upright across it along y = 50 from z = -50 to 50 with a hole 40 by 40 in its middle, cross where both
        /// materials end: (100, 50, 0), (0, 50, 0), (70, 50, 0) and (30, 50, 0). A face standing wholly in the hole
        /// crosses nothing.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when either face is null.</exception>
        public static GeoPoint3[] GetIntersections(GeoFace3 face1, GeoFace3 face2, Tolerance tolerance)
        {
            if (face1 == null)
            {
                throw new ArgumentNullException(nameof(face1));
            }

            if (face2 == null)
            {
                throw new ArgumentNullException(nameof(face2));
            }

            Flat first = Flat.Of(face1);
            Flat second = Flat.Of(face2);

            if (Flat.AreClear(first, second, tolerance))
            {
                return new GeoPoint3[0];
            }

            if (Flat.InOnePlane(first, second, tolerance, out GeoCoordinateSystem3 frame)
                && PlanarMap.TryProjectToFace2(frame, face1, tolerance, out GeoFace2 flatFace1)
                && PlanarMap.TryProjectToFace2(frame, face2, tolerance, out GeoFace2 flatFace2))
            {
                return Lift(frame, Face2.GetIntersections(
                    flatFace1,
                    flatFace2,
                    tolerance));
            }

            var found = new List<GeoPoint3>();

            AddCrossings(found, EdgesOf(face1), face2, tolerance);
            AddCrossings(found, EdgesOf(face2), face1, tolerance);

            return found.ToArray();
        }

        /// <summary>
        /// Tries to find where the materials of two faces cross, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// A face standing wholly in a hole of another, touching neither its material nor the rim, crosses nothing,
        /// and the answer is false.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when either face is null.</exception>
        public static bool TryIntersectWith(GeoFace3 face1, GeoFace3 face2, out GeoPoint3[] intersections)
            => TryIntersectWith(face1, face2, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where the materials of two faces cross, within a tolerance.
        /// </summary>
        /// <param name="face1">The first face.</param>
        /// <param name="face2">The second face.</param>
        /// <param name="intersections">The points of <see cref="GetIntersections(GeoFace3, GeoFace3, Tolerance)"/>, in the same order; empty when there are none.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when they cross anywhere; otherwise, false.</returns>
        /// <remarks>
        /// A face standing wholly in a hole of another, touching neither its material nor the rim, crosses nothing,
        /// and the answer is false.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when either face is null.</exception>
        public static bool TryIntersectWith(GeoFace3 face1, GeoFace3 face2, out GeoPoint3[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(face1, face2, tolerance);

            return intersections.Length > 0;
        }

        #endregion

        #region A plane against a flat region

        /// <summary>
        /// Gets the points where the edges of a triangle cross a plane, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// The floor plane and the triangle with corners (0, 50, -50), (100, 50, -50) and (0, 50, 50) cross at
        /// (50, 50, 0) and (0, 50, 0). A triangle lying in the plane crosses it nowhere.
        /// </remarks>
        public static GeoPoint3[] GetIntersections(GeoPlane3 plane, GeoTriangle3 triangle) => GetIntersections(plane, triangle, Tolerance.Global);

        /// <summary>
        /// Gets the points where the edges of a triangle cross a plane, within a tolerance.
        /// </summary>
        /// <param name="plane">The plane.</param>
        /// <param name="triangle">The triangle.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// Where each edge crosses the plane, edge by edge from A to B, each place named once. A triangle lying in the
        /// plane within the planar tolerance crosses it nowhere and gives none, which
        /// <see cref="Collision3.CollidesWith(GeoPlane3, GeoTriangle3, Tolerance)"/> reports instead.
        /// </returns>
        /// <remarks>
        /// The floor plane and the triangle with corners (0, 50, -50), (100, 50, -50) and (0, 50, 50) cross at
        /// (50, 50, 0) and (0, 50, 0).
        /// </remarks>
        public static GeoPoint3[] GetIntersections(GeoPlane3 plane, GeoTriangle3 triangle, Tolerance tolerance)
        {
            if (Boolean3.LiesIn(plane, new[] { triangle.A, triangle.B, triangle.C }, tolerance))
            {
                return new GeoPoint3[0];
            }

            var found = new List<GeoPoint3>();

            AddCrossings(found, EdgesOf(triangle), plane, tolerance);

            return found.ToArray();
        }

        /// <summary>
        /// Tries to find where the edges of a triangle cross a plane, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// The floor plane and the triangle with corners (0, 50, -50), (100, 50, -50) and (0, 50, 50) cross at two
        /// points.
        /// </remarks>
        public static bool TryIntersectWith(GeoPlane3 plane, GeoTriangle3 triangle, out GeoPoint3[] intersections)
            => TryIntersectWith(plane, triangle, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where the edges of a triangle cross a plane, within a tolerance.
        /// </summary>
        /// <param name="plane">The plane.</param>
        /// <param name="triangle">The triangle.</param>
        /// <param name="intersections">The points of <see cref="GetIntersections(GeoPlane3, GeoTriangle3, Tolerance)"/>, in the same order; empty when there are none.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when they cross anywhere; otherwise, false.</returns>
        /// <remarks>
        /// The floor plane and the triangle with corners (0, 50, -50), (100, 50, -50) and (0, 50, 50) cross at two
        /// points; a triangle lying in the floor crosses it nowhere, and the answer is false.
        /// </remarks>
        public static bool TryIntersectWith(GeoPlane3 plane, GeoTriangle3 triangle, out GeoPoint3[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(plane, triangle, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Gets the points where the edges of a polygon cross a plane, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// The floor plane and a square 100 by 100 standing upright along y = 50 from z = -50 to 50 cross at
        /// (100, 50, 0) and (0, 50, 0). A polygon lying in the plane crosses it nowhere.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public static GeoPoint3[] GetIntersections(GeoPlane3 plane, GeoPolygon3 polygon) => GetIntersections(plane, polygon, Tolerance.Global);

        /// <summary>
        /// Gets the points where the edges of a polygon cross a plane, within a tolerance.
        /// </summary>
        /// <param name="plane">The plane.</param>
        /// <param name="polygon">The polygon.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// Where each edge crosses the plane, edge by edge in order round the polygon, each place named once, as a
        /// straight chain is read. A polygon lying in the plane within the planar tolerance crosses it nowhere and
        /// gives none, which <see cref="Collision3.CollidesWith(GeoPlane3, GeoPolygon3, Tolerance)"/> reports instead.
        /// </returns>
        /// <remarks>
        /// The floor plane and a square 100 by 100 standing upright along y = 50 from z = -50 to 50 cross at
        /// (100, 50, 0) and (0, 50, 0).
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public static GeoPoint3[] GetIntersections(GeoPlane3 plane, GeoPolygon3 polygon, Tolerance tolerance)
        {
            if (polygon == null)
            {
                throw new ArgumentNullException(nameof(polygon));
            }

            if (Boolean3.LiesIn(plane, polygon.Vertices, tolerance))
            {
                return new GeoPoint3[0];
            }

            var found = new List<GeoPoint3>();

            AddCrossings(found, EdgesOf(polygon), plane, tolerance);

            return found.ToArray();
        }

        /// <summary>
        /// Tries to find where the edges of a polygon cross a plane, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// The floor plane and a square 100 by 100 standing upright along y = 50 from z = -50 to 50 cross at two points.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public static bool TryIntersectWith(GeoPlane3 plane, GeoPolygon3 polygon, out GeoPoint3[] intersections)
            => TryIntersectWith(plane, polygon, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where the edges of a polygon cross a plane, within a tolerance.
        /// </summary>
        /// <param name="plane">The plane.</param>
        /// <param name="polygon">The polygon.</param>
        /// <param name="intersections">The points of <see cref="GetIntersections(GeoPlane3, GeoPolygon3, Tolerance)"/>, in the same order; empty when there are none.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when they cross anywhere; otherwise, false.</returns>
        /// <remarks>
        /// The floor plane and a square 100 by 100 standing upright along y = 50 from z = -50 to 50 cross at two points;
        /// a square lying in the floor crosses it nowhere, and the answer is false.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public static bool TryIntersectWith(GeoPlane3 plane, GeoPolygon3 polygon, out GeoPoint3[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(plane, polygon, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Gets the points where the edges of a face, outline and holes, cross a plane, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// The floor plane and the square 100 by 100 with a hole 40 by 40 in its middle, stood upright along y = 50
        /// from z = -50 to 50, cross at (100, 50, 0) and (0, 50, 0) on the outline and at (70, 50, 0) and (30, 50, 0) on
        /// the rim of the hole.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public static GeoPoint3[] GetIntersections(GeoPlane3 plane, GeoFace3 face) => GetIntersections(plane, face, Tolerance.Global);

        /// <summary>
        /// Gets the points where the edges of a face, outline and holes, cross a plane, within a tolerance.
        /// </summary>
        /// <param name="plane">The plane.</param>
        /// <param name="face">The face.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// Where each edge crosses the plane, the outline first and then the rim of each hole, edge by edge in order
        /// round each, each place named once. A face lying in the plane within the planar tolerance crosses it nowhere
        /// and gives none, which <see cref="Collision3.CollidesWith(GeoPlane3, GeoFace3, Tolerance)"/> reports instead.
        /// </returns>
        /// <remarks>
        /// The floor plane and the square 100 by 100 with a hole 40 by 40 in its middle, stood upright along y = 50
        /// from z = -50 to 50, cross at (100, 50, 0) and (0, 50, 0) on the outline and at (70, 50, 0) and (30, 50, 0) on
        /// the rim of the hole.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public static GeoPoint3[] GetIntersections(GeoPlane3 plane, GeoFace3 face, Tolerance tolerance)
        {
            if (face == null)
            {
                throw new ArgumentNullException(nameof(face));
            }

            if (Boolean3.LiesIn(plane, face, tolerance))
            {
                return new GeoPoint3[0];
            }

            var found = new List<GeoPoint3>();

            AddCrossings(found, EdgesOf(face), plane, tolerance);

            return found.ToArray();
        }

        /// <summary>
        /// Tries to find where the edges of a face cross a plane, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// The floor plane and the square 100 by 100 with a hole 40 by 40 in its middle, stood upright along y = 50
        /// from z = -50 to 50, cross at four points.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public static bool TryIntersectWith(GeoPlane3 plane, GeoFace3 face, out GeoPoint3[] intersections)
            => TryIntersectWith(plane, face, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where the edges of a face cross a plane, within a tolerance.
        /// </summary>
        /// <param name="plane">The plane.</param>
        /// <param name="face">The face.</param>
        /// <param name="intersections">The points of <see cref="GetIntersections(GeoPlane3, GeoFace3, Tolerance)"/>, in the same order; empty when there are none.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when they cross anywhere; otherwise, false.</returns>
        /// <remarks>
        /// The floor plane and the square 100 by 100 with a hole 40 by 40 in its middle, stood upright along y = 50
        /// from z = -50 to 50, cross at four points.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public static bool TryIntersectWith(GeoPlane3 plane, GeoFace3 face, out GeoPoint3[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(plane, face, tolerance);

            return intersections.Length > 0;
        }

        #endregion

        #region A ray against a plane, a flat region and another ray

        /// <summary>
        /// Gets where a ray crosses a plane, as a list, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// A ray from (0, 0, 10) pointing straight down crosses the floor plane at the origin; one lying in the floor
        /// crosses it nowhere.
        /// </remarks>
        public static GeoPoint3[] GetIntersections(GeoRay3 ray, GeoPlane3 plane) => GetIntersections(ray, plane, Tolerance.Global);

        /// <summary>
        /// Gets where a ray crosses a plane, as a list, within a tolerance.
        /// </summary>
        /// <param name="ray">The ray.</param>
        /// <param name="plane">The plane.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// The one point of <see cref="TryIntersectWith(GeoRay3, GeoPlane3, out GeoPoint3, Tolerance)"/>, or nothing. A
        /// ray lying in the plane crosses it nowhere and gives none, which
        /// <see cref="Collision3.CollidesWith(GeoRay3, GeoPlane3, Tolerance)"/> reports instead.
        /// </returns>
        /// <remarks>
        /// A ray from (0, 0, 10) pointing straight down crosses the floor plane at the origin.
        /// </remarks>
        public static GeoPoint3[] GetIntersections(GeoRay3 ray, GeoPlane3 plane, Tolerance tolerance)
        {
            return TryIntersectWith(ray, plane, out GeoPoint3 crossing, tolerance)
                ? new[] { crossing }
                : new GeoPoint3[0];
        }

        /// <summary>
        /// Gets where a ray crosses a triangle, as a list, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// A ray from (20, 20, 10) pointing straight down crosses the triangle with corners (0, 0, 0), (100, 0, 0) and
        /// (0, 100, 0) at (20, 20, 0).
        /// </remarks>
        public static GeoPoint3[] GetIntersections(GeoRay3 ray, GeoTriangle3 triangle) => GetIntersections(ray, triangle, Tolerance.Global);

        /// <summary>
        /// Gets where a ray crosses a triangle, as a list, within a tolerance.
        /// </summary>
        /// <param name="ray">The ray.</param>
        /// <param name="triangle">The triangle.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// One point or nothing, and <see cref="TryIntersectWith(GeoRay3, GeoTriangle3, out GeoPoint3, Tolerance)"/> gives the
        /// same point, true exactly where this gives one. The ray is read as the line without ends along it, by how far that line climbs across the triangle, and is given
        /// what the line meets at its origin or ahead of it: the crossing, the point of a touch nearest the triangle, or none where the
        /// line lies level over the material within the planar tolerance of the triangle's plane. Where the line meets the triangle
        /// only behind the origin, or lies level over it only behind a ray running away from it, a ray whose origin touches it
        /// is given its origin. A point is given exactly where <see cref="Collision3.CollidesWith(GeoRay3, GeoTriangle3, Tolerance)"/>
        /// says the two touch, but where the ray holds more than the point tolerance of a part of its line lying level over the
        /// triangle.
        /// </returns>
        /// <remarks>
        /// A ray from (20, 20, 10) pointing straight down crosses the triangle with corners (0, 0, 0), (100, 0, 0) and
        /// (0, 100, 0) at (20, 20, 0). The ray from (50, -0.0006, 0.0003) along (1, -0.01, 0), 0.00067 from the triangle and
        /// running away from it, touches it at its origin, where it collided and was given nothing.
        /// </remarks>
        public static GeoPoint3[] GetIntersections(GeoRay3 ray, GeoTriangle3 triangle, Tolerance tolerance) => Meeting(ray, triangle, tolerance);

        /// <summary>
        /// Gets where a ray crosses a polygon, as a list, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// A ray from (20, 20, 10) pointing straight down crosses the square 100 by 100 in the floor with a corner at the
        /// origin at (20, 20, 0).
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public static GeoPoint3[] GetIntersections(GeoRay3 ray, GeoPolygon3 polygon) => GetIntersections(ray, polygon, Tolerance.Global);

        /// <summary>
        /// Gets where a ray crosses a polygon, as a list, within a tolerance.
        /// </summary>
        /// <param name="ray">The ray.</param>
        /// <param name="polygon">The polygon.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// One point or nothing, and <see cref="TryIntersectWith(GeoRay3, GeoPolygon3, out GeoPoint3, Tolerance)"/> gives the
        /// same point, true exactly where this gives one. The ray is read as the line without ends along it, by how far that line climbs across the polygon, and is given
        /// what the line meets at its origin or ahead of it: the crossing, the point of a touch nearest the polygon, or none where the
        /// line lies level over the material within the planar tolerance of the polygon's plane. Where the line meets the polygon
        /// only behind the origin, or lies level over it only behind a ray running away from it, a ray whose origin touches it
        /// is given its origin. A point is given exactly where <see cref="Collision3.CollidesWith(GeoRay3, GeoPolygon3, Tolerance)"/>
        /// says the two touch, but where the ray holds more than the point tolerance of a part of its line lying level over the
        /// polygon.
        /// </returns>
        /// <remarks>
        /// A ray from (20, 20, 10) pointing straight down crosses the square 100 by 100 in the floor with a corner at the
        /// origin at (20, 20, 0). The ray from (50, -0.0006, 0.0003) along (1, -0.01, 0), 0.00067 from the square and running
        /// away from it, touches it at its origin, where it collided and was given nothing.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public static GeoPoint3[] GetIntersections(GeoRay3 ray, GeoPolygon3 polygon, Tolerance tolerance) => Meeting(ray, polygon, tolerance);

        /// <summary>
        /// Gets where a ray crosses the material of a face, as a list, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// A ray from (50, 50, 10) pointing straight down passes through the hole 40 by 40 in the middle of the square
        /// 100 by 100 in the floor and crosses nothing; from (20, 20, 10) it crosses the material at (20, 20, 0).
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public static GeoPoint3[] GetIntersections(GeoRay3 ray, GeoFace3 face) => GetIntersections(ray, face, Tolerance.Global);

        /// <summary>
        /// Gets where a ray crosses the material of a face, as a list, within a tolerance.
        /// </summary>
        /// <param name="ray">The ray.</param>
        /// <param name="face">The face, read by its material.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// One point or nothing, and <see cref="TryIntersectWith(GeoRay3, GeoFace3, out GeoPoint3, Tolerance)"/> gives the
        /// same point, true exactly where this gives one. The ray is read as the line without ends along it, by how far that line climbs across the face, and is given
        /// what the line meets at its origin or ahead of it: the crossing, the point of a touch nearest the face, or none where the
        /// line lies level over the material within the planar tolerance of the face's plane. Where the line meets the face
        /// only behind the origin, or lies level over it only behind a ray running away from it, a ray whose origin touches it
        /// is given its origin. A point is given exactly where <see cref="Collision3.CollidesWith(GeoRay3, GeoFace3, Tolerance)"/>
        /// says the two touch, but where the ray holds more than the point tolerance of a part of its line lying level over the
        /// material.
        /// </returns>
        /// <remarks>
        /// A ray from (50, 50, 10) pointing straight down passes through the hole 40 by 40 in the middle of the square
        /// 100 by 100 in the floor and crosses nothing; from (20, 20, 10) it crosses the material at (20, 20, 0). The ray from
        /// (50, -0.0006, 0.0003) along (1, -0.01, 0), 0.00067 from the square and running away from it, touches it at its
        /// origin, where it collided and was given nothing.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public static GeoPoint3[] GetIntersections(GeoRay3 ray, GeoFace3 face, Tolerance tolerance) => Meeting(ray, face, tolerance);

        /// <summary>
        /// Gets where two rays cross, as a list, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// A ray from the origin along X and one from (50, -50, 0) along Y cross at (50, 0, 0). Two rays along one line
        /// meet along a length and name no place.
        /// </remarks>
        public static GeoPoint3[] GetIntersections(GeoRay3 ray1, GeoRay3 ray2) => GetIntersections(ray1, ray2, Tolerance.Global);

        /// <summary>
        /// Gets where two rays cross, as a list, within a tolerance.
        /// </summary>
        /// <param name="ray1">The first ray.</param>
        /// <param name="ray2">The second ray.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// The one place they cross, or nothing: two rays that miss each other name no place, and nor do two lying along
        /// one line, which meet along a length, as two segments lying along each other name none.
        /// </returns>
        /// <remarks>
        /// This is the reading two segments get (<see cref="TryIntersectWith(GeoLine3, GeoLine3, out GeoPoint3, Tolerance)"/>):
        /// the rays cross where the shortest line between them is no longer than the point tolerance, and the place named
        /// is the middle of that line. A ray runs on without end, so it draws away from another at any angle but none, and
        /// only rays parallel within rounding are read as lying along each other. A ray from the origin along X and one
        /// from (50, -50, 0) along Y cross at (50, 0, 0); one from (0, -1, 0) half a degree off X crosses the first at
        /// (114.59, 0, 0); one from (50, 10, 0) along Y misses it and names nothing.
        /// </remarks>
        public static GeoPoint3[] GetIntersections(GeoRay3 ray1, GeoRay3 ray2, Tolerance tolerance)
        {
            if (!TryGetBridge(ray1, ray2, out GeoLine3 bridge) || !(bridge.Length <= tolerance.EqualPoint))
            {
                return new GeoPoint3[0];
            }

            return new[] { bridge.MidPoint };
        }

        /// <summary>
        /// Tries to find where two rays cross, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// A ray from the origin along X and one from (50, -50, 0) along Y cross at (50, 0, 0).
        /// </remarks>
        public static bool TryIntersectWith(GeoRay3 ray1, GeoRay3 ray2, out GeoPoint3[] intersections)
            => TryIntersectWith(ray1, ray2, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where two rays cross, within a tolerance.
        /// </summary>
        /// <param name="ray1">The first ray.</param>
        /// <param name="ray2">The second ray.</param>
        /// <param name="intersections">The point of <see cref="GetIntersections(GeoRay3, GeoRay3, Tolerance)"/>; empty when there is none.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when they cross at a place; otherwise, false.</returns>
        /// <remarks>
        /// A ray from the origin along X and one from (50, -50, 0) along Y cross at (50, 0, 0); two rays along the X axis
        /// from 0 and from 100, facing each other, meet along a length and the answer is false.
        /// </remarks>
        public static bool TryIntersectWith(GeoRay3 ray1, GeoRay3 ray2, out GeoPoint3[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(ray1, ray2, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Gets the shortest line between two rays that are not parallel within rounding, from the first to the second.
        /// </summary>
        /// <returns>false when the two are parallel within rounding, where every facing pair is as close as any other.</returns>
        /// <remarks>
        /// The square of the gap is a convex function of the two distances along the rays, so the nearest pair is that
        /// of the two endless lines, the one <see cref="Projection3.GetShortestLineTo(GeoLine3, GeoLine3, Enums.LineExtension, Tolerance)"/>
        /// works out with both extended, wherever both rays reach it; and otherwise it lies where one of the two
        /// distances is nought: one origin and the point of the other ray nearest it, the nearer of the two, the first
        /// ray's origin on a tie.
        /// </remarks>
        internal static bool TryGetBridge(GeoRay3 ray1, GeoRay3 ray2, out GeoLine3 bridge)
        {
            bridge = default(GeoLine3);

            GeoVector3 d1 = ray1.Direction;
            GeoVector3 d2 = ray2.Direction;
            GeoVector3 r = ray2.Origin.GetVectorTo(ray1.Origin);

            double a = d1.LengthSquared;
            double e = d2.LengthSquared;
            double b = d1.DotProduct(d2);
            double c = d1.DotProduct(r);
            double f = d2.DotProduct(r);
            double denominator = a * e - b * b;

            if (!(denominator > 1e-24 * a * e))
            {
                return false;
            }

            double s = (b * f - c * e) / denominator;
            double t = (b * s + f) / e;

            if (s >= 0.0 && t >= 0.0)
            {
                bridge = new GeoLine3(ray1.Origin.Add(d1.Multiply(s)), ray2.Origin.Add(d2.Multiply(t)));
                return true;
            }

            var fromFirst = new GeoLine3(ray1.Origin, Projection3.ProjectToRay(ray2, ray1.Origin));
            var toSecond = new GeoLine3(Projection3.ProjectToRay(ray1, ray2.Origin), ray2.Origin);

            bridge = toSecond.Length < fromFirst.Length ? toSecond : fromFirst;
            return true;
        }

        #endregion

        #region Reading a flat region

        /// <summary>
        /// A triangle, a polygon or a face, read for the questions two of them are asked: its plane, its outline and
        /// its holes.
        /// </summary>
        internal readonly struct Flat
        {
            private Flat(GeoVector3 normal, GeoPlane3 plane, IReadOnlyList<GeoPoint3> outline, IReadOnlyList<GeoPolygon3> holes, GeoAabb3 box)
            {
                Normal = normal;
                Plane = plane;
                Outline = outline;
                Holes = holes;
                Box = box;
            }

            /// <summary>The unit normal, the one the shape's own frame is built on.</summary>
            internal GeoVector3 Normal { get; }

            /// <summary>The plane the shape lies in.</summary>
            internal GeoPlane3 Plane { get; }

            /// <summary>The corners of the outline, in order.</summary>
            internal IReadOnlyList<GeoPoint3> Outline { get; }

            /// <summary>The holes; none for a triangle or a polygon.</summary>
            internal IReadOnlyList<GeoPolygon3> Holes { get; }

            /// <summary>The box round the outline.</summary>
            internal GeoAabb3 Box { get; }

            /// <summary>The frame <see cref="PlanarMap"/> lays the shape out in: its plane, from its first corner.</summary>
            internal GeoCoordinateSystem3 Frame => PlanarMap.FrameOf(Outline, Normal);

            internal static Flat Of(GeoTriangle3 triangle)
            {
                // The normal is the triangle's own where the global tolerance takes it; a triangle smaller than
                // that, asked under a finer tolerance its caller has already found it not degenerate by, is read by
                // its area vector.
                GeoVector3 area = triangle.GetAreaVector();
                GeoVector3 normal = area.TryGetNormal(out GeoVector3 unit) ? unit : area.Divide(area.Length);

                GeoPoint3[] outline = { triangle.A, triangle.B, triangle.C };

                return new Flat(
                    normal, new GeoPlane3(triangle.A, normal), outline, new GeoPolygon3[0], triangle.GetAabb());
            }

            internal static Flat Of(GeoPolygon3 polygon)
                => new Flat(polygon.Normal, polygon.GetPlane(), polygon.Vertices, new GeoPolygon3[0], polygon.GetAabb());

            internal static Flat Of(GeoFace3 face)
                => new Flat(face.Boundary.Normal, face.GetPlane(), face.Boundary.Vertices, face.Holes, face.GetAabb());

            /// <summary>
            /// Determines whether the whole shape, holes and all, lies in a plane within the planar tolerance.
            /// </summary>
            internal bool LiesIn(GeoPlane3 plane, Tolerance tolerance)
            {
                if (!Boolean3.LiesIn(plane, Outline, tolerance))
                {
                    return false;
                }

                foreach (GeoPolygon3 hole in Holes)
                {
                    if (!Boolean3.LiesIn(plane, hole.Vertices, tolerance))
                    {
                        return false;
                    }
                }

                return true;
            }

            /// <summary>
            /// Determines whether two shapes stand so far apart that they cannot meet: their boxes clear of each other by
            /// more than twice the point and planar tolerances together, or either shape wholly to one side of the other's
            /// plane, clear of it by more than twice the larger of the two.
            /// </summary>
            /// <remarks>
            /// The second is <see cref="Collision3.IsClearOf(GeoTriangle3, GeoTriangle3, Tolerance)"/> for any outline, and
            /// both are safe for the same reason: every way the full test finds contact puts a point of one within the
            /// point tolerance of the other along its plane and within the planar tolerance across it. The holes lie within
            /// the outline, so the outline decides. Neither changes an answer; they spare the edge tests two octagons 300
            /// apart would otherwise run.
            /// </remarks>
            internal static bool AreClear(Flat first, Flat second, Tolerance tolerance)
            {
                double apart = 2.0 * (tolerance.EqualPoint + tolerance.EqualPlanar);
                GeoAabb3 a = first.Box;
                GeoAabb3 b = second.Box;

                if (a.Min.X - apart > b.Max.X || b.Min.X - apart > a.Max.X ||
                    a.Min.Y - apart > b.Max.Y || b.Min.Y - apart > a.Max.Y ||
                    a.Min.Z - apart > b.Max.Z || b.Min.Z - apart > a.Max.Z)
                {
                    return true;
                }

                double clear = 2.0 * Math.Max(tolerance.EqualPoint, tolerance.EqualPlanar);

                return StandsClear(second.Plane, first.Outline, clear) || StandsClear(first.Plane, second.Outline, clear);
            }

            private static bool StandsClear(GeoPlane3 plane, IReadOnlyList<GeoPoint3> corners, double clear)
            {
                bool above = true;
                bool below = true;

                for (int i = 0; i < corners.Count && (above || below); i++)
                {
                    double height = plane.SignedDistanceTo(corners[i]);

                    above &= height > clear;
                    below &= height < -clear;
                }

                return above || below;
            }

            /// <summary>
            /// Determines whether two shapes lie in one plane, and if so the frame to lay both out in.
            /// </summary>
            /// <remarks>
            /// The normals have to be parallel within the angle tolerance and either shape has to lie wholly in the
            /// other's plane within the planar tolerance. A small plate lying on a large one tilted a ten-millionth of a
            /// radian off it lies in the large one's plane, though the large one's far edge stands two thousandths off
            /// the small one's: every edge of the small plate runs in the large one's plane and no segment test sees the
            /// two touch, so either way round has to be enough. The frame is the first shape's where the second lies in
            /// its plane, and otherwise the second's, so that the shape the other lies on is laid out exactly.
            /// </remarks>
            internal static bool InOnePlane(Flat first, Flat second, Tolerance tolerance, out GeoCoordinateSystem3 frame)
            {
                frame = default(GeoCoordinateSystem3);

                if (!Parallel3.IsParallel(first.Normal, second.Normal, tolerance))
                {
                    return false;
                }

                if (second.LiesIn(first.Plane, tolerance))
                {
                    frame = first.Frame;
                    return true;
                }

                if (first.LiesIn(second.Plane, tolerance))
                {
                    frame = second.Frame;
                    return true;
                }

                return false;
            }
        }

        /// <summary>The edges of a triangle, A to B first.</summary>
        internal static GeoLine3[] EdgesOf(GeoTriangle3 triangle)
            => new[] { triangle.GetEdgeAt(0), triangle.GetEdgeAt(1), triangle.GetEdgeAt(2) };

        /// <summary>The edges of a polygon, in order round it.</summary>
        internal static GeoLine3[] EdgesOf(GeoPolygon3 polygon) => polygon.GetEdges();

        /// <summary>The edges of a face, ring by ring: the outline first, then each hole, each in order round it.</summary>
        internal static List<GeoLine3> EdgesOf(GeoFace3 face)
        {
            var edges = new List<GeoLine3>(face.Boundary.GetEdges());

            foreach (GeoPolygon3 hole in face.Holes)
            {
                edges.AddRange(hole.GetEdges());
            }

            return edges;
        }

        private static void AddCrossings(List<GeoPoint3> found, IEnumerable<GeoLine3> edges, GeoTriangle3 triangle, Tolerance tolerance)
        {
            foreach (GeoLine3 edge in edges)
            {
                if (TryCrossEdge(edge, triangle, out GeoPoint3 crossing, tolerance))
                {
                    Arc3.AddOnce(found, crossing, tolerance);
                }
            }
        }

        private static void AddCrossings(List<GeoPoint3> found, IEnumerable<GeoLine3> edges, GeoPolygon3 polygon, Tolerance tolerance)
        {
            foreach (GeoLine3 edge in edges)
            {
                if (TryCrossEdge(edge, polygon, out GeoPoint3 crossing, tolerance))
                {
                    Arc3.AddOnce(found, crossing, tolerance);
                }
            }
        }

        private static void AddCrossings(List<GeoPoint3> found, IEnumerable<GeoLine3> edges, GeoFace3 face, Tolerance tolerance)
        {
            foreach (GeoLine3 edge in edges)
            {
                if (TryCrossEdge(edge, face, out GeoPoint3 crossing, tolerance))
                {
                    Arc3.AddOnce(found, crossing, tolerance);
                }
            }
        }

        private static void AddCrossings(List<GeoPoint3> found, IEnumerable<GeoLine3> edges, GeoPlane3 plane, Tolerance tolerance)
        {
            foreach (GeoLine3 edge in edges)
            {
                if (TryIntersectWith(edge, plane, out GeoPoint3 crossing, tolerance) ||
                    TryReachRefused(edge, plane, out crossing))
                {
                    Arc3.AddOnce(found, crossing, tolerance);
                }
            }
        }

        /// <summary>
        /// Tries to find where an edge of one shape meets a triangle: the crossing of the segment test, or, where that
        /// refuses the edge, the place <see cref="TryReachRefused"/> finds on the triangle.
        /// </summary>
        /// <param name="edge">The edge.</param>
        /// <param name="triangle">The triangle, read as a filled surface.</param>
        /// <param name="at">The place they meet.</param>
        /// <param name="tolerance">The tolerance.</param>
        internal static bool TryCrossEdge(GeoLine3 edge, GeoTriangle3 triangle, out GeoPoint3 at, Tolerance tolerance)
        {
            if (TryIntersectWith(edge, triangle, out at, tolerance))
            {
                return true;
            }

            if (triangle.IsDegenerate(tolerance))
            {
                return false;
            }

            GeoPlane3 plane = triangle.GetPlane();

            return TryReachRefused(edge, plane, out at)
                && Containment3.Contains(triangle, Projection3.ProjectToPlane(plane, at), tolerance);
        }

        /// <summary>
        /// Tries to find where an edge of one shape meets a polygon, as
        /// <see cref="TryCrossEdge(GeoLine3, GeoTriangle3, out GeoPoint3, Tolerance)"/> does a triangle.
        /// </summary>
        internal static bool TryCrossEdge(GeoLine3 edge, GeoPolygon3 polygon, out GeoPoint3 at, Tolerance tolerance)
        {
            if (TryIntersectWith(edge, polygon, out at, tolerance))
            {
                return true;
            }

            GeoPlane3 plane = polygon.GetPlane();

            return TryReachRefused(edge, plane, out at)
                && Containment3.Contains(polygon, Projection3.ProjectToPlane(plane, at), tolerance);
        }

        /// <summary>
        /// Tries to find where an edge of one shape meets the material of a face, as
        /// <see cref="TryCrossEdge(GeoLine3, GeoTriangle3, out GeoPoint3, Tolerance)"/> does a triangle.
        /// </summary>
        internal static bool TryCrossEdge(GeoLine3 edge, GeoFace3 face, out GeoPoint3 at, Tolerance tolerance)
        {
            if (TryIntersectWith(edge, face, out at, tolerance))
            {
                return true;
            }

            GeoPlane3 plane = face.GetPlane();

            return TryReachRefused(edge, plane, out at)
                && face.Contains(Projection3.ProjectToPlane(plane, at), tolerance);
        }

        /// <summary>
        /// Reads an edge the segment test has refused against a plane by the signs of its two ends' heights, so that a
        /// side cut into pieces too short or too level for that test to take meets the plane where the uncut side does.
        /// </summary>
        /// <param name="edge">
        /// The edge, refused by <see cref="TryIntersectWith(GeoLine3, GeoPlane3, out GeoPoint3, Tolerance)"/>.
        /// </param>
        /// <param name="plane">The plane.</param>
        /// <param name="at">Where the edge passes through, or the one end lying on the plane.</param>
        /// <returns>
        /// true when the ends stand strictly either side, or one of them, and only one, lies on the plane; otherwise,
        /// false.
        /// </returns>
        /// <remarks>
        /// The segment test refuses an edge that climbs no more than the planar tolerance from end to end. A square 40
        /// by 40 tilted a tenth of a degree, each side cut into 80 pieces, has no piece the test takes, though the
        /// uncut square crosses a plate under it at two points: read by its signs, the one piece that passes through
        /// meets the plate where the uncut side does. No tolerance widens the plane here: a piece merely ending near it
        /// is not taken, since the pieces either side of a crossing all end near it and would each name a place of
        /// their own. An edge lying in the plane is left to the edges at its ends, as the segment test leaves it. Two
        /// regions within the tolerance of each other without crossing are found touching by their distance instead
        /// (<see cref="Collision3"/>).
        /// </remarks>
        internal static bool TryReachRefused(GeoLine3 edge, GeoPlane3 plane, out GeoPoint3 at)
        {
            double from = plane.SignedDistanceTo(edge.StartPoint);
            double to = plane.SignedDistanceTo(edge.EndPoint);

            if ((from < 0.0 && to > 0.0) || (from > 0.0 && to < 0.0))
            {
                at = edge.StartPoint.Add(edge.StartPoint.GetVectorTo(edge.EndPoint).Multiply(from / (from - to)));
                return true;
            }

            bool startOn = !(from < 0.0 || from > 0.0);
            bool endOn = !(to < 0.0 || to > 0.0);

            if (startOn != endOn && !double.IsNaN(from) && !double.IsNaN(to))
            {
                at = startOn ? edge.StartPoint : edge.EndPoint;
                return true;
            }

            at = default(GeoPoint3);
            return false;
        }

        /// <summary>
        /// Puts points of the plane back into space, in their order.
        /// </summary>
        private static GeoPoint3[] Lift(GeoCoordinateSystem3 frame, GeoPoint2[] points)
        {
            var lifted = new GeoPoint3[points.Length];

            for (int i = 0; i < points.Length; i++)
            {
                lifted[i] = PlanarMap.ToPoint3(frame, points[i]);
            }

            return lifted;
        }

        #endregion
    }
}
