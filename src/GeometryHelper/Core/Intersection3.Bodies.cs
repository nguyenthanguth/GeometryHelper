using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    /// <summary>
    /// Where a flat region crosses the surface of a body or a box, and where a plane crosses a box.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A region and a body meet where an edge of the region crosses the surface, or where an edge of the body crosses
    /// the region; a region lying wholly inside crosses nothing. So the crossings are both of those, the region's edges
    /// first, then the body's face by face, each place named once. The body is read by its material, every opening cut
    /// in, so the walls of an opening are surface and a region inside an opening crosses nothing. A box 100 by 100 by
    /// 100 with a corner at the origin and the square 200 by 200 lying across it at half its height cross where the
    /// four upright edges of the box pass through the square, at (0, 0, 50), (100, 0, 50), (100, 100, 50) and
    /// (0, 100, 50).
    /// </para>
    /// <para>
    /// A box is read through its body: each crossing with a box is the crossing with <see cref="GeoObb3.ToSolid"/>, or
    /// with <c>ToObb().ToSolid()</c> for an axis-aligned one, so it is exactly the answer the body would give.
    /// </para>
    /// </remarks>
    public static partial class Intersection3
    {
        #region A flat region against a body

        /// <summary>
        /// Gets the points where a triangle and the surface of a solid cross, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// A box 100 by 100 by 100 with a corner at the origin and the triangle with corners (-50, -50, 50),
        /// (300, -50, 50) and (-50, 300, 50) lying across it at half its height cross at (0, 0, 50), (100, 0, 50),
        /// (100, 100, 50) and (0, 100, 50), where the upright edges of the box pass through it.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the solid is null.</exception>
        public static GeoPoint3[] GetIntersections(GeoTriangle3 triangle, GeoSolid3 solid) => GetIntersections(triangle, solid, Tolerance.Global);

        /// <summary>
        /// Gets the points where a triangle and the surface of a solid cross, within a tolerance.
        /// </summary>
        /// <param name="triangle">The triangle.</param>
        /// <param name="solid">The solid, read by its material.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// Where an edge of the triangle crosses the surface, in order along each edge, then where an edge of the body
        /// crosses the triangle, face by face, each place named once. A triangle lying wholly inside the material
        /// crosses nothing and gives none, which <see cref="Collision3.CollidesWith(GeoTriangle3, GeoSolid3, Tolerance)"/>
        /// reports instead.
        /// </returns>
        /// <remarks>
        /// A box 100 by 100 by 100 with a corner at the origin and the triangle with corners (-50, -50, 50),
        /// (300, -50, 50) and (-50, 300, 50) lying across it at half its height cross at (0, 0, 50), (100, 0, 50),
        /// (100, 100, 50) and (0, 100, 50), where the upright edges of the box pass through it.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the solid is null.</exception>
        public static GeoPoint3[] GetIntersections(GeoTriangle3 triangle, GeoSolid3 solid, Tolerance tolerance)
        {
            if (solid == null)
            {
                throw new ArgumentNullException(nameof(solid));
            }

            GeoSolid3 material = Material3.Whole(solid, tolerance);
            var found = new List<GeoPoint3>();

            AddSurfaceCrossings(found, EdgesOf(triangle), material, tolerance);

            foreach (GeoFace3 face in material.Faces)
            {
                AddCrossings(found, EdgesOf(face), triangle, tolerance);
            }

            return found.ToArray();
        }

        /// <summary>
        /// Tries to find where a triangle and the surface of a solid cross, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// A box 100 by 100 by 100 with a corner at the origin and the triangle with corners (-50, -50, 50),
        /// (300, -50, 50) and (-50, 300, 50) lying across it at half its height cross at four points.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the solid is null.</exception>
        public static bool TryIntersectWith(GeoTriangle3 triangle, GeoSolid3 solid, out GeoPoint3[] intersections)
            => TryIntersectWith(triangle, solid, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where a triangle and the surface of a solid cross, within a tolerance.
        /// </summary>
        /// <param name="triangle">The triangle.</param>
        /// <param name="solid">The solid.</param>
        /// <param name="intersections">The points of <see cref="GetIntersections(GeoTriangle3, GeoSolid3, Tolerance)"/>, in the same order; empty when there are none.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when they cross anywhere; otherwise, false.</returns>
        /// <remarks>
        /// A box 100 by 100 by 100 with a corner at the origin and the triangle with corners (-50, -50, 50),
        /// (300, -50, 50) and (-50, 300, 50) lying across it at half its height cross at four points; a triangle wholly
        /// inside the box crosses nothing, and the answer is false.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the solid is null.</exception>
        public static bool TryIntersectWith(GeoTriangle3 triangle, GeoSolid3 solid, out GeoPoint3[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(triangle, solid, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Gets the points where a polygon and the surface of a solid cross, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// A box 100 by 100 by 100 with a corner at the origin and the square 200 by 200 from (-50, -50, 50) to
        /// (150, 150, 50) lying across it at half its height cross at (0, 0, 50), (100, 0, 50), (100, 100, 50) and
        /// (0, 100, 50), where the upright edges of the box pass through it.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the polygon or the solid is null.</exception>
        public static GeoPoint3[] GetIntersections(GeoPolygon3 polygon, GeoSolid3 solid) => GetIntersections(polygon, solid, Tolerance.Global);

        /// <summary>
        /// Gets the points where a polygon and the surface of a solid cross, within a tolerance.
        /// </summary>
        /// <param name="polygon">The polygon.</param>
        /// <param name="solid">The solid, read by its material.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// Where an edge of the polygon crosses the surface, in order along each edge, then where an edge of the body
        /// crosses the polygon, face by face, each place named once. A polygon lying wholly inside the material crosses
        /// nothing and gives none, which <see cref="Collision3.CollidesWith(GeoPolygon3, GeoSolid3, Tolerance)"/>
        /// reports instead.
        /// </returns>
        /// <remarks>
        /// A box 100 by 100 by 100 with a corner at the origin and the square 200 by 200 from (-50, -50, 50) to
        /// (150, 150, 50) lying across it at half its height cross at (0, 0, 50), (100, 0, 50), (100, 100, 50) and
        /// (0, 100, 50), where the upright edges of the box pass through it.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the polygon or the solid is null.</exception>
        public static GeoPoint3[] GetIntersections(GeoPolygon3 polygon, GeoSolid3 solid, Tolerance tolerance)
        {
            if (polygon == null)
            {
                throw new ArgumentNullException(nameof(polygon));
            }

            if (solid == null)
            {
                throw new ArgumentNullException(nameof(solid));
            }

            GeoSolid3 material = Material3.Whole(solid, tolerance);
            var found = new List<GeoPoint3>();

            AddSurfaceCrossings(found, EdgesOf(polygon), material, tolerance);

            foreach (GeoFace3 face in material.Faces)
            {
                AddCrossings(found, EdgesOf(face), polygon, tolerance);
            }

            return found.ToArray();
        }

        /// <summary>
        /// Tries to find where a polygon and the surface of a solid cross, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// A box 100 by 100 by 100 with a corner at the origin and the square 200 by 200 from (-50, -50, 50) to
        /// (150, 150, 50) lying across it at half its height cross at four points.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the polygon or the solid is null.</exception>
        public static bool TryIntersectWith(GeoPolygon3 polygon, GeoSolid3 solid, out GeoPoint3[] intersections)
            => TryIntersectWith(polygon, solid, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where a polygon and the surface of a solid cross, within a tolerance.
        /// </summary>
        /// <param name="polygon">The polygon.</param>
        /// <param name="solid">The solid.</param>
        /// <param name="intersections">The points of <see cref="GetIntersections(GeoPolygon3, GeoSolid3, Tolerance)"/>, in the same order; empty when there are none.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when they cross anywhere; otherwise, false.</returns>
        /// <remarks>
        /// A box 100 by 100 by 100 with a corner at the origin and the square 200 by 200 from (-50, -50, 50) to
        /// (150, 150, 50) lying across it at half its height cross at four points; a square wholly inside the box
        /// crosses nothing, and the answer is false.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the polygon or the solid is null.</exception>
        public static bool TryIntersectWith(GeoPolygon3 polygon, GeoSolid3 solid, out GeoPoint3[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(polygon, solid, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Gets the points where the material of a face and the surface of a solid cross, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// The square 200 by 200 from (-50, -50, 50) to (150, 150, 50) with a hole 150 by 150 in its middle, around a box
        /// 100 by 100 by 100 with a corner at the origin, crosses nothing: the box passes through the hole.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the face or the solid is null.</exception>
        public static GeoPoint3[] GetIntersections(GeoFace3 face, GeoSolid3 solid) => GetIntersections(face, solid, Tolerance.Global);

        /// <summary>
        /// Gets the points where the material of a face and the surface of a solid cross, within a tolerance.
        /// </summary>
        /// <param name="face">The face, read by its material.</param>
        /// <param name="solid">The solid, read by its material.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// Where an edge of the face, outline and then holes, crosses the surface, in order along each edge, then where
        /// an edge of the body crosses the material of the face, face by face, each place named once. A face lying
        /// wholly inside the material crosses nothing and gives none, which
        /// <see cref="Collision3.CollidesWith(GeoFace3, GeoSolid3, Tolerance)"/> reports instead.
        /// </returns>
        /// <remarks>
        /// The square 200 by 200 from (-50, -50, 50) to (150, 150, 50) with a hole 150 by 150 in its middle, around a box
        /// 100 by 100 by 100 with a corner at the origin, crosses nothing: the box passes through the hole. Without the
        /// hole the two cross at (0, 0, 50), (100, 0, 50), (100, 100, 50) and (0, 100, 50).
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the face or the solid is null.</exception>
        public static GeoPoint3[] GetIntersections(GeoFace3 face, GeoSolid3 solid, Tolerance tolerance)
        {
            if (face == null)
            {
                throw new ArgumentNullException(nameof(face));
            }

            if (solid == null)
            {
                throw new ArgumentNullException(nameof(solid));
            }

            GeoSolid3 material = Material3.Whole(solid, tolerance);
            var found = new List<GeoPoint3>();

            AddSurfaceCrossings(found, EdgesOf(face), material, tolerance);

            foreach (GeoFace3 side in material.Faces)
            {
                AddCrossings(found, EdgesOf(side), face, tolerance);
            }

            return found.ToArray();
        }

        /// <summary>
        /// Tries to find where the material of a face and the surface of a solid cross, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// The square 200 by 200 from (-50, -50, 50) to (150, 150, 50) with a hole 150 by 150 in its middle, around a box
        /// 100 by 100 by 100 with a corner at the origin, crosses nothing, and the answer is false.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the face or the solid is null.</exception>
        public static bool TryIntersectWith(GeoFace3 face, GeoSolid3 solid, out GeoPoint3[] intersections)
            => TryIntersectWith(face, solid, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where the material of a face and the surface of a solid cross, within a tolerance.
        /// </summary>
        /// <param name="face">The face.</param>
        /// <param name="solid">The solid.</param>
        /// <param name="intersections">The points of <see cref="GetIntersections(GeoFace3, GeoSolid3, Tolerance)"/>, in the same order; empty when there are none.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when they cross anywhere; otherwise, false.</returns>
        /// <remarks>
        /// The square 200 by 200 from (-50, -50, 50) to (150, 150, 50) with a hole 150 by 150 in its middle, around a box
        /// 100 by 100 by 100 with a corner at the origin, crosses nothing, and the answer is false.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the face or the solid is null.</exception>
        public static bool TryIntersectWith(GeoFace3 face, GeoSolid3 solid, out GeoPoint3[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(face, solid, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Gathers where each of some edges crosses the surface of a body with no openings left, in order along each
        /// edge, naming each place once.
        /// </summary>
        private static void AddSurfaceCrossings(List<GeoPoint3> found, IEnumerable<GeoLine3> edges, GeoSolid3 material, Tolerance tolerance)
        {
            foreach (GeoLine3 edge in edges)
            {
                foreach (GeoPoint3 crossing in SurfaceCrossings(edge, material, tolerance))
                {
                    Arc3.AddOnce(found, crossing, tolerance);
                }
            }
        }

        /// <summary>
        /// Gets where an edge of a flat region meets the surface of a body with no openings left, in order along the
        /// edge: the crossings of <see cref="GetIntersections(GeoLine3, GeoSolid3, Tolerance)"/>, and where the segment
        /// test refuses the edge against a face, the place its heights find on that face.
        /// </summary>
        /// <remarks>
        /// The edge is read against each face as an edge of one region is against another
        /// (<see cref="TryCrossEdge(GeoLine3, GeoFace3, out GeoPoint3, Tolerance)"/>), so a region lying across the
        /// top of a box a twentieth of a degree off level, its sides cut into 256 pieces none of which the segment test
        /// takes, crosses the top where its uncut form would. Two places closer than the point tolerance along the edge
        /// are named once, as the segment's own crossings are.
        /// </remarks>
        internal static GeoPoint3[] SurfaceCrossings(GeoLine3 edge, GeoSolid3 material, Tolerance tolerance)
        {
            var distances = new List<double>();

            CollectSurfaceCrossings(edge, material, tolerance, distances);

            distances.Sort();

            var hits = new List<GeoPoint3>();

            foreach (double distance in distances)
            {
                double last = hits.Count > 0 ? Parametrization3.GetDistanceAtPoint(edge, hits[hits.Count - 1]) : 0.0;

                if (hits.Count > 0 && distance - last <= tolerance.EqualPoint)
                {
                    continue;
                }

                hits.Add(Parametrization3.GetPointAtDistance(edge, distance));
            }

            return hits.ToArray();
        }

        private static void CollectSurfaceCrossings(
            GeoLine3 edge, GeoSolid3 solid, Tolerance tolerance, List<double> distances)
        {
            foreach (GeoFace3 face in solid.Faces)
            {
                if (TryCrossEdge(edge, face, out GeoPoint3 hit, tolerance))
                {
                    distances.Add(Parametrization3.GetDistanceAtPoint(edge, hit));
                }
            }

            foreach (GeoSolid3 opening in solid.Openings)
            {
                CollectSurfaceCrossings(edge, opening, tolerance, distances);
            }
        }

        #endregion

        #region A flat region and a plane against a box

        /// <summary>
        /// Gets the points where a triangle and the surface of an oriented box cross, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// The crossings with <see cref="GeoObb3.ToSolid"/>: a box 100 by 100 by 100 with a corner at the origin and the
        /// triangle with corners (-50, -50, 50), (300, -50, 50) and (-50, 300, 50) cross at four points at height 50.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the box is null.</exception>
        public static GeoPoint3[] GetIntersections(GeoTriangle3 triangle, GeoObb3 box) => GetIntersections(triangle, box, Tolerance.Global);

        /// <summary>
        /// Gets the points where a triangle and the surface of an oriented box cross, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The crossings with <see cref="GeoObb3.ToSolid"/>: a box 100 by 100 by 100 with a corner at the origin and the
        /// triangle with corners (-50, -50, 50), (300, -50, 50) and (-50, 300, 50) cross at four points at height 50.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the box is null.</exception>
        public static GeoPoint3[] GetIntersections(GeoTriangle3 triangle, GeoObb3 box, Tolerance tolerance)
            => GetIntersections(triangle, BodyOf(box), tolerance);

        /// <summary>
        /// Tries to find where a triangle and the surface of an oriented box cross, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// The answer of <see cref="TryIntersectWith(GeoTriangle3, GeoSolid3, out GeoPoint3[], Tolerance)"/> for
        /// <see cref="GeoObb3.ToSolid"/>: a triangle wholly inside the box crosses nothing, and the answer is false.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the box is null.</exception>
        public static bool TryIntersectWith(GeoTriangle3 triangle, GeoObb3 box, out GeoPoint3[] intersections)
            => TryIntersectWith(triangle, box, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where a triangle and the surface of an oriented box cross, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The answer of <see cref="TryIntersectWith(GeoTriangle3, GeoSolid3, out GeoPoint3[], Tolerance)"/> for
        /// <see cref="GeoObb3.ToSolid"/>: a triangle wholly inside the box crosses nothing, and the answer is false.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the box is null.</exception>
        public static bool TryIntersectWith(GeoTriangle3 triangle, GeoObb3 box, out GeoPoint3[] intersections, Tolerance tolerance)
            => TryIntersectWith(triangle, BodyOf(box), out intersections, tolerance);

        /// <summary>
        /// Gets the points where a triangle and the surface of an axis-aligned box cross, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// The crossings with <c>ToObb().ToSolid()</c>: the box from the origin to (100, 100, 100) and the triangle with
        /// corners (-50, -50, 50), (300, -50, 50) and (-50, 300, 50) cross at four points at height 50.
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown when the box is empty.</exception>
        public static GeoPoint3[] GetIntersections(GeoTriangle3 triangle, GeoAabb3 box) => GetIntersections(triangle, box, Tolerance.Global);

        /// <summary>
        /// Gets the points where a triangle and the surface of an axis-aligned box cross, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The crossings with <c>ToObb().ToSolid()</c>: the box from the origin to (100, 100, 100) and the triangle with
        /// corners (-50, -50, 50), (300, -50, 50) and (-50, 300, 50) cross at four points at height 50.
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown when the box is empty.</exception>
        public static GeoPoint3[] GetIntersections(GeoTriangle3 triangle, GeoAabb3 box, Tolerance tolerance)
            => GetIntersections(triangle, box.ToObb().ToSolid(), tolerance);

        /// <summary>
        /// Tries to find where a triangle and the surface of an axis-aligned box cross, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// The answer of <see cref="TryIntersectWith(GeoTriangle3, GeoSolid3, out GeoPoint3[], Tolerance)"/> for
        /// <c>ToObb().ToSolid()</c>: a triangle wholly inside the box crosses nothing, and the answer is false.
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown when the box is empty.</exception>
        public static bool TryIntersectWith(GeoTriangle3 triangle, GeoAabb3 box, out GeoPoint3[] intersections)
            => TryIntersectWith(triangle, box, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where a triangle and the surface of an axis-aligned box cross, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The answer of <see cref="TryIntersectWith(GeoTriangle3, GeoSolid3, out GeoPoint3[], Tolerance)"/> for
        /// <c>ToObb().ToSolid()</c>: a triangle wholly inside the box crosses nothing, and the answer is false.
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown when the box is empty.</exception>
        public static bool TryIntersectWith(GeoTriangle3 triangle, GeoAabb3 box, out GeoPoint3[] intersections, Tolerance tolerance)
            => TryIntersectWith(triangle, box.ToObb().ToSolid(), out intersections, tolerance);

        /// <summary>
        /// Gets the points where a polygon and the surface of an oriented box cross, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// The crossings with <see cref="GeoObb3.ToSolid"/>: a box 100 by 100 by 100 with a corner at the origin and the
        /// square 200 by 200 from (-50, -50, 50) to (150, 150, 50) cross at four points at height 50.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the polygon or the box is null.</exception>
        public static GeoPoint3[] GetIntersections(GeoPolygon3 polygon, GeoObb3 box) => GetIntersections(polygon, box, Tolerance.Global);

        /// <summary>
        /// Gets the points where a polygon and the surface of an oriented box cross, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The crossings with <see cref="GeoObb3.ToSolid"/>: a box 100 by 100 by 100 with a corner at the origin and the
        /// square 200 by 200 from (-50, -50, 50) to (150, 150, 50) cross at four points at height 50.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the polygon or the box is null.</exception>
        public static GeoPoint3[] GetIntersections(GeoPolygon3 polygon, GeoObb3 box, Tolerance tolerance)
            => GetIntersections(polygon, BodyOf(box), tolerance);

        /// <summary>
        /// Tries to find where a polygon and the surface of an oriented box cross, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// The answer of <see cref="TryIntersectWith(GeoPolygon3, GeoSolid3, out GeoPoint3[], Tolerance)"/> for
        /// <see cref="GeoObb3.ToSolid"/>: a square wholly inside the box crosses nothing, and the answer is false.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the polygon or the box is null.</exception>
        public static bool TryIntersectWith(GeoPolygon3 polygon, GeoObb3 box, out GeoPoint3[] intersections)
            => TryIntersectWith(polygon, box, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where a polygon and the surface of an oriented box cross, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The answer of <see cref="TryIntersectWith(GeoPolygon3, GeoSolid3, out GeoPoint3[], Tolerance)"/> for
        /// <see cref="GeoObb3.ToSolid"/>: a square wholly inside the box crosses nothing, and the answer is false.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the polygon or the box is null.</exception>
        public static bool TryIntersectWith(GeoPolygon3 polygon, GeoObb3 box, out GeoPoint3[] intersections, Tolerance tolerance)
            => TryIntersectWith(polygon, BodyOf(box), out intersections, tolerance);

        /// <summary>
        /// Gets the points where a polygon and the surface of an axis-aligned box cross, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// The crossings with <c>ToObb().ToSolid()</c>: the box from the origin to (100, 100, 100) and the square 200 by
        /// 200 from (-50, -50, 50) to (150, 150, 50) cross at four points at height 50.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the box is empty.</exception>
        public static GeoPoint3[] GetIntersections(GeoPolygon3 polygon, GeoAabb3 box) => GetIntersections(polygon, box, Tolerance.Global);

        /// <summary>
        /// Gets the points where a polygon and the surface of an axis-aligned box cross, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The crossings with <c>ToObb().ToSolid()</c>: the box from the origin to (100, 100, 100) and the square 200 by
        /// 200 from (-50, -50, 50) to (150, 150, 50) cross at four points at height 50.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the box is empty.</exception>
        public static GeoPoint3[] GetIntersections(GeoPolygon3 polygon, GeoAabb3 box, Tolerance tolerance)
            => GetIntersections(polygon, box.ToObb().ToSolid(), tolerance);

        /// <summary>
        /// Tries to find where a polygon and the surface of an axis-aligned box cross, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// The answer of <see cref="TryIntersectWith(GeoPolygon3, GeoSolid3, out GeoPoint3[], Tolerance)"/> for
        /// <c>ToObb().ToSolid()</c>: a square wholly inside the box crosses nothing, and the answer is false.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the box is empty.</exception>
        public static bool TryIntersectWith(GeoPolygon3 polygon, GeoAabb3 box, out GeoPoint3[] intersections)
            => TryIntersectWith(polygon, box, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where a polygon and the surface of an axis-aligned box cross, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The answer of <see cref="TryIntersectWith(GeoPolygon3, GeoSolid3, out GeoPoint3[], Tolerance)"/> for
        /// <c>ToObb().ToSolid()</c>: a square wholly inside the box crosses nothing, and the answer is false.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the box is empty.</exception>
        public static bool TryIntersectWith(GeoPolygon3 polygon, GeoAabb3 box, out GeoPoint3[] intersections, Tolerance tolerance)
            => TryIntersectWith(polygon, box.ToObb().ToSolid(), out intersections, tolerance);

        /// <summary>
        /// Gets the points where the material of a face and the surface of an oriented box cross, using the default
        /// tolerance.
        /// </summary>
        /// <remarks>
        /// The crossings with <see cref="GeoObb3.ToSolid"/>: the square 200 by 200 from (-50, -50, 50) to (150, 150, 50)
        /// with a hole 150 by 150 in its middle, around a box 100 by 100 by 100 with a corner at the origin, crosses
        /// nothing.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the face or the box is null.</exception>
        public static GeoPoint3[] GetIntersections(GeoFace3 face, GeoObb3 box) => GetIntersections(face, box, Tolerance.Global);

        /// <summary>
        /// Gets the points where the material of a face and the surface of an oriented box cross, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The crossings with <see cref="GeoObb3.ToSolid"/>: the square 200 by 200 from (-50, -50, 50) to (150, 150, 50)
        /// with a hole 150 by 150 in its middle, around a box 100 by 100 by 100 with a corner at the origin, crosses
        /// nothing.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the face or the box is null.</exception>
        public static GeoPoint3[] GetIntersections(GeoFace3 face, GeoObb3 box, Tolerance tolerance)
            => GetIntersections(face, BodyOf(box), tolerance);

        /// <summary>
        /// Tries to find where the material of a face and the surface of an oriented box cross, using the default
        /// tolerance.
        /// </summary>
        /// <remarks>
        /// The answer of <see cref="TryIntersectWith(GeoFace3, GeoSolid3, out GeoPoint3[], Tolerance)"/> for
        /// <see cref="GeoObb3.ToSolid"/>: a face around the box with a hole it passes through crosses nothing, and the
        /// answer is false.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the face or the box is null.</exception>
        public static bool TryIntersectWith(GeoFace3 face, GeoObb3 box, out GeoPoint3[] intersections)
            => TryIntersectWith(face, box, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where the material of a face and the surface of an oriented box cross, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The answer of <see cref="TryIntersectWith(GeoFace3, GeoSolid3, out GeoPoint3[], Tolerance)"/> for
        /// <see cref="GeoObb3.ToSolid"/>: a face around the box with a hole it passes through crosses nothing, and the
        /// answer is false.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the face or the box is null.</exception>
        public static bool TryIntersectWith(GeoFace3 face, GeoObb3 box, out GeoPoint3[] intersections, Tolerance tolerance)
            => TryIntersectWith(face, BodyOf(box), out intersections, tolerance);

        /// <summary>
        /// Gets the points where the material of a face and the surface of an axis-aligned box cross, using the default
        /// tolerance.
        /// </summary>
        /// <remarks>
        /// The crossings with <c>ToObb().ToSolid()</c>: the square 200 by 200 from (-50, -50, 50) to (150, 150, 50) with
        /// a hole 150 by 150 in its middle, around the box from the origin to (100, 100, 100), crosses nothing.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the box is empty.</exception>
        public static GeoPoint3[] GetIntersections(GeoFace3 face, GeoAabb3 box) => GetIntersections(face, box, Tolerance.Global);

        /// <summary>
        /// Gets the points where the material of a face and the surface of an axis-aligned box cross, within a
        /// tolerance.
        /// </summary>
        /// <remarks>
        /// The crossings with <c>ToObb().ToSolid()</c>: the square 200 by 200 from (-50, -50, 50) to (150, 150, 50) with
        /// a hole 150 by 150 in its middle, around the box from the origin to (100, 100, 100), crosses nothing.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the box is empty.</exception>
        public static GeoPoint3[] GetIntersections(GeoFace3 face, GeoAabb3 box, Tolerance tolerance)
            => GetIntersections(face, box.ToObb().ToSolid(), tolerance);

        /// <summary>
        /// Tries to find where the material of a face and the surface of an axis-aligned box cross, using the default
        /// tolerance.
        /// </summary>
        /// <remarks>
        /// The answer of <see cref="TryIntersectWith(GeoFace3, GeoSolid3, out GeoPoint3[], Tolerance)"/> for
        /// <c>ToObb().ToSolid()</c>: a face around the box with a hole it passes through crosses nothing, and the answer
        /// is false.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the box is empty.</exception>
        public static bool TryIntersectWith(GeoFace3 face, GeoAabb3 box, out GeoPoint3[] intersections)
            => TryIntersectWith(face, box, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where the material of a face and the surface of an axis-aligned box cross, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The answer of <see cref="TryIntersectWith(GeoFace3, GeoSolid3, out GeoPoint3[], Tolerance)"/> for
        /// <c>ToObb().ToSolid()</c>: a face around the box with a hole it passes through crosses nothing, and the answer
        /// is false.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the box is empty.</exception>
        public static bool TryIntersectWith(GeoFace3 face, GeoAabb3 box, out GeoPoint3[] intersections, Tolerance tolerance)
            => TryIntersectWith(face, box.ToObb().ToSolid(), out intersections, tolerance);

        /// <summary>
        /// Gets the points where a plane cuts the edges of an oriented box, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// The points of <see cref="GetIntersections(GeoPlane3, GeoSolid3, Tolerance)"/> for <see cref="GeoObb3.ToSolid"/>:
        /// the plane z = 50 cuts the four upright edges of a box 100 by 100 by 100 standing on the floor, at height 50.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the box is null.</exception>
        public static GeoPoint3[] GetIntersections(GeoPlane3 plane, GeoObb3 box) => GetIntersections(plane, box, Tolerance.Global);

        /// <summary>
        /// Gets the points where a plane cuts the edges of an oriented box, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The points of <see cref="GetIntersections(GeoPlane3, GeoSolid3, Tolerance)"/> for <see cref="GeoObb3.ToSolid"/>:
        /// the plane z = 50 cuts the four upright edges of a box 100 by 100 by 100 standing on the floor, at height 50.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the box is null.</exception>
        public static GeoPoint3[] GetIntersections(GeoPlane3 plane, GeoObb3 box, Tolerance tolerance)
            => GetIntersections(plane, BodyOf(box), tolerance);

        /// <summary>
        /// Tries to find where a plane cuts the edges of an oriented box, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// The plane z = 50 cuts a box 100 by 100 by 100 standing on the floor at four points; the plane z = 150 misses
        /// it, and the answer is false.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the box is null.</exception>
        public static bool TryIntersectWith(GeoPlane3 plane, GeoObb3 box, out GeoPoint3[] intersections)
            => TryIntersectWith(plane, box, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where a plane cuts the edges of an oriented box, within a tolerance.
        /// </summary>
        /// <param name="plane">The plane.</param>
        /// <param name="box">The box.</param>
        /// <param name="intersections">The points of <see cref="GetIntersections(GeoPlane3, GeoObb3, Tolerance)"/>, in the same order; empty when there are none.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when the plane cuts an edge anywhere; otherwise, false.</returns>
        /// <remarks>
        /// The plane z = 50 cuts a box 100 by 100 by 100 standing on the floor at four points; the plane z = 150 misses
        /// it, and the answer is false.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the box is null.</exception>
        public static bool TryIntersectWith(GeoPlane3 plane, GeoObb3 box, out GeoPoint3[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(plane, box, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Gets the points where a plane cuts the edges of an axis-aligned box, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// The points of <see cref="GetIntersections(GeoPlane3, GeoSolid3, Tolerance)"/> for <c>ToObb().ToSolid()</c>: the
        /// plane z = 50 cuts the four upright edges of the box from the origin to (100, 100, 100), at height 50.
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown when the box is empty.</exception>
        public static GeoPoint3[] GetIntersections(GeoPlane3 plane, GeoAabb3 box) => GetIntersections(plane, box, Tolerance.Global);

        /// <summary>
        /// Gets the points where a plane cuts the edges of an axis-aligned box, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The points of <see cref="GetIntersections(GeoPlane3, GeoSolid3, Tolerance)"/> for <c>ToObb().ToSolid()</c>: the
        /// plane z = 50 cuts the four upright edges of the box from the origin to (100, 100, 100), at height 50.
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown when the box is empty.</exception>
        public static GeoPoint3[] GetIntersections(GeoPlane3 plane, GeoAabb3 box, Tolerance tolerance)
            => GetIntersections(plane, box.ToObb().ToSolid(), tolerance);

        /// <summary>
        /// Tries to find where a plane cuts the edges of an axis-aligned box, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// The plane z = 50 cuts the box from the origin to (100, 100, 100) at four points; the plane z = 150 misses it,
        /// and the answer is false.
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown when the box is empty.</exception>
        public static bool TryIntersectWith(GeoPlane3 plane, GeoAabb3 box, out GeoPoint3[] intersections)
            => TryIntersectWith(plane, box, out intersections, Tolerance.Global);

        /// <summary>
        /// Tries to find where a plane cuts the edges of an axis-aligned box, within a tolerance.
        /// </summary>
        /// <param name="plane">The plane.</param>
        /// <param name="box">The box.</param>
        /// <param name="intersections">The points of <see cref="GetIntersections(GeoPlane3, GeoAabb3, Tolerance)"/>, in the same order; empty when there are none.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when the plane cuts an edge anywhere; otherwise, false.</returns>
        /// <remarks>
        /// The plane z = 50 cuts the box from the origin to (100, 100, 100) at four points; the plane z = 150 misses it,
        /// and the answer is false.
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown when the box is empty.</exception>
        public static bool TryIntersectWith(GeoPlane3 plane, GeoAabb3 box, out GeoPoint3[] intersections, Tolerance tolerance)
        {
            intersections = GetIntersections(plane, box, tolerance);

            return intersections.Length > 0;
        }

        /// <summary>
        /// Gets the body of an oriented box, refusing a box that is not there by the name of the argument.
        /// </summary>
        internal static GeoSolid3 BodyOf(GeoObb3 box)
        {
            if (box == null)
            {
                throw new ArgumentNullException(nameof(box));
            }

            return box.ToSolid();
        }

        #endregion
    }
}
