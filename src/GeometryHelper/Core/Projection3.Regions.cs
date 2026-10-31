using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    /// <summary>
    /// The shortest segment joining two flat regions in space, a region or a ray to a plane, a segment, a chain or a ray
    /// to a face, a ray to a polygon, a ray or a chain, and a body to a face.
    /// </summary>
    /// <remarks>
    /// <para>
    /// These are the pairs neither side could measure. Each is the least of the joining lines already here: every edge of
    /// the first shape against the second, then every edge of the second against the first, a ray standing in for an
    /// edge. Two flat regions that do not meet always have their nearest pair on an edge of one of them, so nothing else
    /// need be weighed. The segment leaves the shape named first and lands on the other, and both ends lie on their
    /// shapes.
    /// </para>
    /// <para>
    /// <b>A face is its material</b>, the outline less its holes. A segment crossing the material meets it in a segment
    /// of no length; one passing through a hole is joined to the nearest rim, or straight down from an end that stands
    /// over the material.
    /// </para>
    /// <para>
    /// <b>Where the two meet</b>, the segment has no length and sits at the first point of contact walking the edges in
    /// that order: for two regions in planes that are not one, the first of the crossings
    /// <see cref="Intersection3.GetIntersections(GeoPolygon3, GeoPolygon3, Tolerance)"/> names. As for the segment and
    /// the chain against a polygon, an edge coming at a region end-on and stopping within the point tolerance of it is
    /// read as crossing it, so the segment of no length sits at that end, though the distance there is the true gap; a
    /// region lying level within the tolerance is joined by its true gap, which is its distance too. The segment from (5, 5, 0.0004)
    /// to (5, 5, 9) over the square 100 by 100 in the floor is joined by a segment of no length at (5, 5, 0.0004), and is 0.0004
    /// from it by its distance.
    /// </para>
    /// <para>
    /// <b>A plane</b> is read by the heights of the corners, as a chain is: a shape that crosses or touches it is joined
    /// by a segment of no length at the first point of contact walking its edges in order, and a shape clear of it from
    /// its nearest corner, the first of those equally near. No tolerance widens the plane here, so a shape a hair off it
    /// is joined by a segment that short.
    /// </para>
    /// <para>
    /// Where two candidates are equally short, the one met first stands. A length that is not a number is kept, so a
    /// shape with a corner that is not a number is not a number away.
    /// </para>
    /// </remarks>
    public static partial class Projection3
    {
        #region A segment, a chain and a ray against a face

        /// <summary>
        /// Gets the shortest segment joining a line segment to the material of a face.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public static GeoLine3 GetShortestLineTo(GeoLine3 line, GeoFace3 face) => GetShortestLineTo(line, face, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment joining a line segment to the material of a face, within a tolerance.
        /// </summary>
        /// <param name="line">The segment the answer leaves.</param>
        /// <param name="face">The face the answer lands on, read by its material.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// A segment of no length at the crossing where the segment passes through the material or touches it; otherwise
        /// the shortest of the segments to each edge of the outline and then of each hole, and from each end to the
        /// nearest point of the material. <see cref="Distance3.DistanceTo(GeoLine3, GeoFace3, Tolerance)"/> is its
        /// length. Over the square 100 by 100 in the floor with a corner at the origin and a hole 40 by 40 in its middle,
        /// the segment from (45, 50, -10) to (45, 50, 10), through the hole, is joined from (45, 50, 0) to the rim at
        /// (30, 50, 0), 15 long, and the segment from (20, 20, -10) to (20, 20, 10) meets the material at (20, 20, 0).
        /// </returns>
        /// <remarks>
        /// As for a polygon: a segment passing clean through the material meets none of its edges, so the crossing is
        /// looked for first; after that the nearest pair is either between the segment and an edge of a ring, or under
        /// one of the segment's ends, where the nearest material is straight down when the foot lands on it and on the
        /// rim when it lands in a hole. A segment no longer than the point tolerance is read as its start where it does not
        /// truly meet the material, as for a polygon.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public static GeoLine3 GetShortestLineTo(GeoLine3 line, GeoFace3 face, Tolerance tolerance)
        {
            if (face == null)
            {
                throw new ArgumentNullException(nameof(face));
            }

            if (Stub3.ReadsAsStart(line, tolerance))
            {
                return Stub3.Nearest(
                    line,
                    Closest(line, face, Stub3.AsItIs(line, tolerance), Stub3.AsItIs(line, tolerance)),
                    new GeoLine3(line.StartPoint, ProjectToFace(face, line.StartPoint, tolerance, Exactly)));
            }

            return Closest(line, face, tolerance, Exactly);
        }

        /// <summary>
        /// Gets the shortest segment joining a segment to the material of a face by the search a side of a shape is joined by,
        /// the segment not read as one you give.
        /// </summary>
        /// <param name="line">The segment.</param>
        /// <param name="face">The face.</param>
        /// <param name="tolerance">The tolerance the crossing and the feet on the material are read within.</param>
        /// <param name="sides">The tolerance the segment is joined to each side of a rim within: nought, so a side no longer than
        /// the point tolerance is measured as the side it is.</param>
        internal static GeoLine3 Closest(GeoLine3 line, GeoFace3 face, Tolerance tolerance, Tolerance sides)
        {
            if (Intersection3.TryCross(line, face, out GeoPoint3 through, tolerance))
            {
                return new GeoLine3(through, through);
            }

            List<GeoLine3> edges = Intersection3.EdgesOf(face);

            GeoLine3 best = Closest(line, edges[0], sides);
            double shortest = best.Length;

            for (int i = 1; i < edges.Count; i++)
            {
                Consider(ref best, ref shortest, Closest(line, edges[i], sides));
            }

            Consider(ref best, ref shortest, new GeoLine3(line.StartPoint, ProjectToFace(face, line.StartPoint, tolerance, sides)));
            Consider(ref best, ref shortest, new GeoLine3(line.EndPoint, ProjectToFace(face, line.EndPoint, tolerance, sides)));

            return best;
        }

        /// <summary>
        /// Gets the shortest segment joining a polyline to the material of a face.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the polyline or the face is null.</exception>
        public static GeoLine3 GetShortestLineTo(GeoPolyline3 polyline, GeoFace3 face) => GetShortestLineTo(polyline, face, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment joining a polyline to the material of a face, within a tolerance.
        /// </summary>
        /// <param name="polyline">The chain the answer leaves.</param>
        /// <param name="face">The face the answer lands on, read by its material.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// The shortest of the segments <see cref="GetShortestLineTo(GeoLine3, GeoFace3, Tolerance)"/> gives for each
        /// edge, the first edge winning a tie, and of no length where the chain passes through the material. It is as long
        /// as <see cref="Distance3.DistanceTo(GeoPolyline3, GeoFace3, Tolerance)"/>. Over the square 100 by 100 in the
        /// floor with a corner at the origin and a hole 40 by 40 in its middle, the chain (20, 20, 30), (20, 20, 80),
        /// (50, 50, 80) is joined from (20, 20, 30) straight down, 30 long, and the chain (45, 50, 30), (45, 50, -30),
        /// through the hole, from (45, 50, 0) to the rim at (30, 50, 0), 15 long.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when the polyline or the face is null.</exception>
        public static GeoLine3 GetShortestLineTo(GeoPolyline3 polyline, GeoFace3 face, Tolerance tolerance)
        {
            if (polyline == null)
            {
                throw new ArgumentNullException(nameof(polyline));
            }

            if (face == null)
            {
                throw new ArgumentNullException(nameof(face));
            }

            GeoLine3 best = Closest(polyline.GetEdgeAt(0), face, tolerance, Exactly);
            double shortest = best.Length;

            for (int i = 1; i < polyline.EdgeCount && shortest > 0.0; i++)
            {
                Consider(ref best, ref shortest, Closest(polyline.GetEdgeAt(i), face, tolerance, Exactly));
            }

            return best;
        }

        /// <summary>
        /// Gets the shortest segment joining a ray to the material of a face.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public static GeoLine3 GetShortestLineTo(GeoRay3 ray, GeoFace3 face) => GetShortestLineTo(ray, face, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment joining a ray to the material of a face, within a tolerance.
        /// </summary>
        /// <param name="ray">The ray the answer leaves.</param>
        /// <param name="face">The face the answer lands on, read by its material.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// A segment of no length at the crossing where the ray runs into the material; otherwise the shortest of the
        /// segment from the origin to the nearest point of the material and those from the ray to each edge of the
        /// outline and then of each hole. <see cref="Distance3.DistanceTo(GeoRay3, GeoFace3, Tolerance)"/> is its length.
        /// Over the square 100 by 100 in the floor with a corner at the origin and a hole 40 by 40 in its middle, the ray
        /// from (45, 50, 10) pointing straight down, through the hole, is joined from (45, 50, 0) to the rim at
        /// (30, 50, 0), 15 long, and the ray from (20, 20, 10) pointing straight down meets the material at (20, 20, 0).
        /// </returns>
        /// <remarks>
        /// As for a triangle: a ray that pierces the material touches none of its edges, so the crossing is looked for
        /// first; after that the nearest pair lies either between the ray and an edge, or under the origin of the ray.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public static GeoLine3 GetShortestLineTo(GeoRay3 ray, GeoFace3 face, Tolerance tolerance)
        {
            if (face == null)
            {
                throw new ArgumentNullException(nameof(face));
            }

            if (Intersection3.TryCrossFace(ray, face, out GeoPoint3 through, tolerance, false))
            {
                return new GeoLine3(through, through);
            }

            var best = new GeoLine3(ray.Origin, ProjectToFace(face, ray.Origin, tolerance, tolerance));
            double shortest = best.Length;

            foreach (GeoLine3 edge in Intersection3.EdgesOf(face))
            {
                Consider(ref best, ref shortest, Closest(ray, edge, Exactly, Exactly));
            }

            return best;
        }

        /// <summary>
        /// Gets the shortest segment joining a ray to a polygon.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public static GeoLine3 GetShortestLineTo(GeoRay3 ray, GeoPolygon3 polygon) => GetShortestLineTo(ray, polygon, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment joining a ray to a polygon, within a tolerance.
        /// </summary>
        /// <param name="ray">The ray the answer leaves.</param>
        /// <param name="polygon">The polygon the answer lands on, read as a filled surface.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// A segment of no length at the crossing where the ray runs into the polygon; otherwise the shortest of the
        /// segment from the origin down to the surface and those from the ray to each edge in turn.
        /// <see cref="Distance3.DistanceTo(GeoRay3, GeoPolygon3, Tolerance)"/> is its length. Beside the square 100 by
        /// 100 in the floor with a corner at the origin, the ray from (130, 50, 40) along X is joined from its origin to
        /// (100, 50, 0) on the edge, 50 long, and the ray from (20, 20, 10) pointing straight down meets the square at
        /// (20, 20, 0).
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public static GeoLine3 GetShortestLineTo(GeoRay3 ray, GeoPolygon3 polygon, Tolerance tolerance)
        {
            if (polygon == null)
            {
                throw new ArgumentNullException(nameof(polygon));
            }

            if (Intersection3.TryCrossPolygon(ray, polygon, out GeoPoint3 through, tolerance))
            {
                return new GeoLine3(through, through);
            }

            var best = new GeoLine3(ray.Origin, ProjectToPolygon(polygon, ray.Origin, tolerance, tolerance));
            double shortest = best.Length;

            for (int i = 0; i < polygon.EdgeCount; i++)
            {
                Consider(ref best, ref shortest, Closest(ray, polygon.GetEdgeAt(i), Exactly, Exactly));
            }

            return best;
        }

        #endregion

        #region Two flat regions

        /// <summary>
        /// Gets the shortest segment joining a triangle to a polygon.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public static GeoLine3 GetShortestLineTo(GeoTriangle3 triangle, GeoPolygon3 polygon) => GetShortestLineTo(triangle, polygon, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment joining a triangle to a polygon, within a tolerance.
        /// </summary>
        /// <param name="triangle">The triangle the answer leaves, read as a filled surface.</param>
        /// <param name="polygon">The polygon the answer lands on, read as a filled surface.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// The shortest of the segments joining each edge of the triangle to the polygon and each edge of the polygon to
        /// the triangle, in that order, the first winning a tie, and of no length where the two meet. Where they do not
        /// meet it is as long as <see cref="Distance3.DistanceTo(GeoTriangle3, GeoPolygon3, Tolerance)"/>. The triangle
        /// with corners (0, 0, 0), (100, 0, 0) and (0, 100, 0) and the square 100 by 100 standing upright along y = 50
        /// from z = 10 to 110 are joined straight up, 10 long; standing from z = -50, the square crosses the triangle and
        /// the segment has no length at (50, 50, 0).
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public static GeoLine3 GetShortestLineTo(GeoTriangle3 triangle, GeoPolygon3 polygon, Tolerance tolerance)
        {
            if (polygon == null)
            {
                throw new ArgumentNullException(nameof(polygon));
            }

            return Join(triangle, polygon, 0.0, tolerance);
        }

        /// <summary>
        /// Gets the shortest segment joining a triangle to the material of a face.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public static GeoLine3 GetShortestLineTo(GeoTriangle3 triangle, GeoFace3 face) => GetShortestLineTo(triangle, face, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment joining a triangle to the material of a face, within a tolerance.
        /// </summary>
        /// <param name="triangle">The triangle the answer leaves, read as a filled surface.</param>
        /// <param name="face">The face the answer lands on, read by its material.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// The shortest of the segments joining each edge of the triangle to the material and each edge of the face,
        /// outline and then holes, to the triangle, in that order, the first winning a tie, and of no length where the two
        /// meet. Where they do not meet it is as long as
        /// <see cref="Distance3.DistanceTo(GeoTriangle3, GeoFace3, Tolerance)"/>. The triangle with corners
        /// (110, 50, 0), (200, 50, 0) and (110, 50, 100), standing beside the square 100 by 100 in the floor with a corner
        /// at the origin and a hole 40 by 40 in its middle, is joined from its corner (110, 50, 0) to (100, 50, 0) on the
        /// outline, 10 long.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public static GeoLine3 GetShortestLineTo(GeoTriangle3 triangle, GeoFace3 face, Tolerance tolerance)
        {
            if (face == null)
            {
                throw new ArgumentNullException(nameof(face));
            }

            return Join(triangle, face, 0.0, tolerance);
        }

        /// <summary>
        /// Gets the shortest segment joining two polygons.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when either polygon is null.</exception>
        public static GeoLine3 GetShortestLineTo(GeoPolygon3 polygon1, GeoPolygon3 polygon2) => GetShortestLineTo(polygon1, polygon2, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment joining two polygons, within a tolerance.
        /// </summary>
        /// <param name="polygon1">The polygon the answer leaves, read as a filled surface.</param>
        /// <param name="polygon2">The polygon the answer lands on, read as a filled surface.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// The shortest of the segments joining each edge of the first to the second and each edge of the second to the
        /// first, in that order, the first winning a tie, and of no length where the two meet. Where they do not meet it
        /// is as long as <see cref="Distance3.DistanceTo(GeoPolygon3, GeoPolygon3, Tolerance)"/>. A square 100 by 100 in
        /// the floor with a corner at the origin and one standing upright along y = 50 from z = 10 to 110 are joined from
        /// (100, 50, 0) to (100, 50, 10), 10 long; standing from z = -50, the second crosses the first and the segment has
        /// no length at (100, 50, 0).
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when either polygon is null.</exception>
        public static GeoLine3 GetShortestLineTo(GeoPolygon3 polygon1, GeoPolygon3 polygon2, Tolerance tolerance)
        {
            if (polygon1 == null)
            {
                throw new ArgumentNullException(nameof(polygon1));
            }

            if (polygon2 == null)
            {
                throw new ArgumentNullException(nameof(polygon2));
            }

            return Join(polygon1, polygon2, 0.0, tolerance);
        }

        /// <summary>
        /// Gets the shortest segment joining the material of a face to a polygon.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the face or the polygon is null.</exception>
        public static GeoLine3 GetShortestLineTo(GeoFace3 face, GeoPolygon3 polygon) => GetShortestLineTo(face, polygon, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment joining the material of a face to a polygon, within a tolerance.
        /// </summary>
        /// <param name="face">The face the answer leaves, read by its material.</param>
        /// <param name="polygon">The polygon the answer lands on, read as a filled surface.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// The shortest of the segments joining each edge of the face, outline and then holes, to the polygon and each
        /// edge of the polygon to the material, in that order, the first winning a tie, and of no length where the two
        /// meet. Where they do not meet it is as long as
        /// <see cref="Distance3.DistanceTo(GeoFace3, GeoPolygon3, Tolerance)"/>. The square 100 by 100 in the floor with a
        /// corner at the origin and a hole 40 by 40 in its middle, and a square 20 by 20 standing upright in the middle of
        /// the hole along y = 50 from x = 40 to 60, are joined from the rim at (70, 50, 0) to (60, 50, 0), 10 long.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when the face or the polygon is null.</exception>
        public static GeoLine3 GetShortestLineTo(GeoFace3 face, GeoPolygon3 polygon, Tolerance tolerance)
        {
            if (face == null)
            {
                throw new ArgumentNullException(nameof(face));
            }

            if (polygon == null)
            {
                throw new ArgumentNullException(nameof(polygon));
            }

            return Join(face, polygon, 0.0, tolerance);
        }

        /// <summary>
        /// Gets the shortest segment joining the materials of two faces.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when either face is null.</exception>
        public static GeoLine3 GetShortestLineTo(GeoFace3 face1, GeoFace3 face2) => GetShortestLineTo(face1, face2, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment joining the materials of two faces, within a tolerance.
        /// </summary>
        /// <param name="face1">The face the answer leaves, read by its material.</param>
        /// <param name="face2">The face the answer lands on, read by its material.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// The shortest of the segments joining each edge of the first, outline and then holes, to the second and each edge
        /// of the second to the first, in that order, the first winning a tie, and of no length where the two meet. Where
        /// they do not meet it is as long as <see cref="Distance3.DistanceTo(GeoFace3, GeoFace3, Tolerance)"/>. The square
        /// 100 by 100 in the floor with a corner at the origin and a hole 40 by 40 in its middle, and the square 20 by 20
        /// lying in the hole about (50, 50), meet nowhere and are joined from the rim at (40, 30, 0) to (40, 40, 0),
        /// 10 long.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when either face is null.</exception>
        public static GeoLine3 GetShortestLineTo(GeoFace3 face1, GeoFace3 face2, Tolerance tolerance)
        {
            if (face1 == null)
            {
                throw new ArgumentNullException(nameof(face1));
            }

            if (face2 == null)
            {
                throw new ArgumentNullException(nameof(face2));
            }

            return Join(face1, face2, 0.0, tolerance);
        }

        /// <summary>
        /// Joins a triangle to a polygon, walking the edges until a segment no longer than <paramref name="near"/> is
        /// found.
        /// </summary>
        /// <param name="triangle">The triangle the answer leaves.</param>
        /// <param name="polygon">The polygon the answer lands on.</param>
        /// <param name="near">
        /// How short a segment ends the walk: nought for the shortest line, and the point tolerance for
        /// <see cref="Collision3"/>, which asks only whether the two come within it.
        /// </param>
        /// <param name="tolerance">The tolerance.</param>
        internal static GeoLine3 Join(GeoTriangle3 triangle, GeoPolygon3 polygon, double near, Tolerance tolerance)
        {
            return JoinRegions(
                Intersection3.EdgesOf(triangle), Region(triangle), edge => ToRegion(edge, polygon, tolerance),
                polygon.GetEdges(), Region(polygon), edge => ToRegion(edge, triangle, tolerance),
                near, tolerance);
        }

        /// <summary>
        /// Joins a triangle to the material of a face, as
        /// <see cref="Join(GeoTriangle3, GeoPolygon3, double, Tolerance)"/> joins a triangle to a polygon.
        /// </summary>
        internal static GeoLine3 Join(GeoTriangle3 triangle, GeoFace3 face, double near, Tolerance tolerance)
        {
            return JoinRegions(
                Intersection3.EdgesOf(triangle), Region(triangle), edge => ToRegion(edge, face, tolerance),
                Intersection3.EdgesOf(face), Region(face), edge => ToRegion(edge, triangle, tolerance),
                near, tolerance);
        }

        /// <summary>
        /// Joins two polygons, as <see cref="Join(GeoTriangle3, GeoPolygon3, double, Tolerance)"/> joins a triangle to
        /// a polygon.
        /// </summary>
        internal static GeoLine3 Join(GeoPolygon3 polygon1, GeoPolygon3 polygon2, double near, Tolerance tolerance)
        {
            return JoinRegions(
                polygon1.GetEdges(), Region(polygon1), edge => ToRegion(edge, polygon2, tolerance),
                polygon2.GetEdges(), Region(polygon2), edge => ToRegion(edge, polygon1, tolerance),
                near, tolerance);
        }

        /// <summary>
        /// Joins the material of a face to a polygon, as
        /// <see cref="Join(GeoTriangle3, GeoPolygon3, double, Tolerance)"/> joins a triangle to a polygon.
        /// </summary>
        internal static GeoLine3 Join(GeoFace3 face, GeoPolygon3 polygon, double near, Tolerance tolerance)
        {
            return JoinRegions(
                Intersection3.EdgesOf(face), Region(face), edge => ToRegion(edge, polygon, tolerance),
                polygon.GetEdges(), Region(polygon), edge => ToRegion(edge, face, tolerance),
                near, tolerance);
        }

        /// <summary>
        /// Joins the materials of two faces, as <see cref="Join(GeoTriangle3, GeoPolygon3, double, Tolerance)"/> joins
        /// a triangle to a polygon.
        /// </summary>
        internal static GeoLine3 Join(GeoFace3 face1, GeoFace3 face2, double near, Tolerance tolerance)
        {
            return JoinRegions(
                Intersection3.EdgesOf(face1), Region(face1), edge => ToRegion(edge, face2, tolerance),
                Intersection3.EdgesOf(face2), Region(face2), edge => ToRegion(edge, face1, tolerance),
                near, tolerance);
        }

        /// <summary>
        /// Joins an edge of one region to a triangle: a segment of no length where the edge passes through it, the
        /// level edge of a side cut finely included, otherwise the segment-against-triangle line.
        /// </summary>
        private static GeoLine3 ToRegion(GeoLine3 edge, GeoTriangle3 triangle, Tolerance tolerance)
        {
            return Intersection3.TryCrossEdge(edge, triangle, out GeoPoint3 at, tolerance)
                ? new GeoLine3(at, at)
                : Closest(edge, triangle, tolerance, Exactly);
        }

        /// <summary>
        /// Joins an edge of one region to a polygon, as <see cref="ToRegion(GeoLine3, GeoTriangle3, Tolerance)"/> joins
        /// one to a triangle.
        /// </summary>
        private static GeoLine3 ToRegion(GeoLine3 edge, GeoPolygon3 polygon, Tolerance tolerance)
        {
            return Intersection3.TryCrossEdge(edge, polygon, out GeoPoint3 at, tolerance)
                ? new GeoLine3(at, at)
                : Closest(edge, polygon, tolerance, Exactly);
        }

        /// <summary>
        /// Joins an edge of one region to the material of a face, as
        /// <see cref="ToRegion(GeoLine3, GeoTriangle3, Tolerance)"/> joins one to a triangle.
        /// </summary>
        private static GeoLine3 ToRegion(GeoLine3 edge, GeoFace3 face, Tolerance tolerance)
        {
            return Intersection3.TryCrossEdge(edge, face, out GeoPoint3 at, tolerance)
                ? new GeoLine3(at, at)
                : Closest(edge, face, tolerance, Exactly);
        }

        /// <summary>
        /// Gets the box and the plane of a triangle, which bound how near an edge of another region can come to it.
        /// </summary>
        private static (GeoAabb3 Box, GeoPlane3 Plane) Region(GeoTriangle3 triangle)
            => (triangle.GetAabb(), Intersection3.Flat.Of(triangle).Plane);

        /// <summary>
        /// Gets the box and the plane of a polygon, which bound how near an edge of another region can come to it.
        /// </summary>
        private static (GeoAabb3 Box, GeoPlane3 Plane) Region(GeoPolygon3 polygon)
            => (polygon.GetAabb(), polygon.GetPlane());

        /// <summary>
        /// Gets the box and the plane of a face, which bound how near an edge of another region can come to it.
        /// </summary>
        private static (GeoAabb3 Box, GeoPlane3 Plane) Region(GeoFace3 face) => (face.GetAabb(), face.GetPlane());

        /// <summary>
        /// Keeps the shortest of the segments joining each edge of the first region to the second, then each edge of the
        /// second to the first, turned round so that it leaves the first; the walk stops at the first no longer than
        /// <paramref name="near"/>.
        /// </summary>
        /// <remarks>
        /// An edge that cannot come as near the other region as the shortest segment so far, or as
        /// <paramref name="near"/> where that is set, by more than four times the point tolerance, is passed over
        /// unread. Two things bound how near it can come: the gap between its box and the region's, and, where both its
        /// ends stand on one side of the region's plane, the height of the nearer one. Every segment from the edge is at
        /// least that long, so it could not stand, and the slack keeps rounding from deciding it. Two octagons 300 apart
        /// were joined in about 44 microseconds a pair before on .NET Framework 4.8, and are in about 12 now, to the same
        /// bits.
        /// </remarks>
        private static GeoLine3 JoinRegions(
            IReadOnlyList<GeoLine3> first,
            (GeoAabb3 Box, GeoPlane3 Plane) firstRegion,
            Func<GeoLine3, GeoLine3> toSecond,
            IReadOnlyList<GeoLine3> second,
            (GeoAabb3 Box, GeoPlane3 Plane) secondRegion,
            Func<GeoLine3, GeoLine3> toFirst,
            double near,
            Tolerance tolerance)
        {
            double slack = 4.0 * tolerance.EqualPoint;

            GeoLine3 best = toSecond(first[0]);
            double shortest = best.Length;

            for (int i = 1; i < first.Count && shortest > near; i++)
            {
                if (Bound(first[i], secondRegion, tolerance) - slack > (near > 0.0 ? near : shortest))
                {
                    continue;
                }

                Consider(ref best, ref shortest, toSecond(first[i]));
            }

            for (int i = 0; i < second.Count && shortest > near; i++)
            {
                if (Bound(second[i], firstRegion, tolerance) - slack > (near > 0.0 ? near : shortest))
                {
                    continue;
                }

                Consider(ref best, ref shortest, toFirst(second[i]).Reverse());
            }

            return best;
        }

        /// <summary>
        /// Gets how near, at the least, an edge can come to a region: the gap between their boxes, or the height of the
        /// nearer end over the region's plane where both ends stand on one side of it, less the planar tolerance its
        /// corners may stand off that plane, whichever is the greater.
        /// </summary>
        private static double Bound(GeoLine3 edge, (GeoAabb3 Box, GeoPlane3 Plane) region, Tolerance tolerance)
        {
            double gap = edge.GetAabb().DistanceTo(region.Box);
            double from = region.Plane.SignedDistanceTo(edge.StartPoint);
            double to = region.Plane.SignedDistanceTo(edge.EndPoint);
            double height = from > 0.0 && to > 0.0 ? Math.Min(from, to)
                : from < 0.0 && to < 0.0 ? -Math.Max(from, to)
                : 0.0;

            return Math.Max(gap, height - tolerance.EqualPlanar);
        }

        #endregion

        #region A plane against a flat region and a ray

        /// <summary>
        /// Gets the shortest segment joining a triangle to a plane.
        /// </summary>
        public static GeoLine3 GetShortestLineTo(GeoTriangle3 triangle, GeoPlane3 plane) => GetShortestLineTo(triangle, plane, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment joining a triangle to a plane.
        /// </summary>
        /// <param name="triangle">The triangle the answer leaves.</param>
        /// <param name="plane">The plane the answer lands on.</param>
        /// <param name="tolerance">
        /// The tolerance; not read, since the segment is the true one: a corner a hair off the plane is joined to it by a
        /// segment that short, where <see cref="Distance3.DistanceTo(GeoPlane3, GeoTriangle3, Tolerance)"/> reads nought.
        /// </param>
        /// <returns>
        /// A segment of no length where the triangle crosses or touches the plane, at the first point of contact walking
        /// its edges from A to B: the start of the first edge that reaches the plane when that start lies on it, otherwise
        /// where the edge passes through, otherwise its end. Clear of the plane, the perpendicular from the nearest corner,
        /// the first of them where several are equally near; beyond the point tolerance its length is
        /// <see cref="Distance3.DistanceTo(GeoPlane3, GeoTriangle3, Tolerance)"/>. The triangle with corners
        /// (0, 50, -50), (100, 50, -50) and (0, 50, 50) meets the floor plane at (50, 50, 0); the triangle with corners
        /// (0, 50, 10), (100, 50, 10) and (0, 50, 110) is joined from (0, 50, 10) straight down, 10 long.
        /// </returns>
        public static GeoLine3 GetShortestLineTo(GeoTriangle3 triangle, GeoPlane3 plane, Tolerance tolerance)
        {
            return JoinToPlane(new[] { triangle.A, triangle.B, triangle.C }, plane);
        }

        /// <summary>
        /// Gets the shortest segment joining a polygon to a plane.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public static GeoLine3 GetShortestLineTo(GeoPolygon3 polygon, GeoPlane3 plane) => GetShortestLineTo(polygon, plane, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment joining a polygon to a plane.
        /// </summary>
        /// <param name="polygon">The polygon the answer leaves.</param>
        /// <param name="plane">The plane the answer lands on.</param>
        /// <param name="tolerance">
        /// The tolerance; not read, since the segment is the true one: a corner a hair off the plane is joined to it by a
        /// segment that short, where <see cref="Distance3.DistanceTo(GeoPlane3, GeoPolygon3, Tolerance)"/> reads nought.
        /// </param>
        /// <returns>
        /// A segment of no length where the polygon crosses or touches the plane, at the first point of contact walking
        /// its edges in order round it: the start of the first edge that reaches the plane when that start lies on it,
        /// otherwise where the edge passes through, otherwise its end. Clear of the plane, the perpendicular from the
        /// nearest corner, the first of them where several are equally near; beyond the point tolerance its length is
        /// <see cref="Distance3.DistanceTo(GeoPlane3, GeoPolygon3, Tolerance)"/>. A square 100 by 100 standing upright
        /// along y = 50 from z = -50 to 50 meets the floor plane at (100, 50, 0); standing from z = 10 to 110, it is joined
        /// from its corner (0, 50, 10) straight down, 10 long.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public static GeoLine3 GetShortestLineTo(GeoPolygon3 polygon, GeoPlane3 plane, Tolerance tolerance)
        {
            if (polygon == null)
            {
                throw new ArgumentNullException(nameof(polygon));
            }

            return JoinToPlane(polygon.Vertices, plane);
        }

        /// <summary>
        /// Gets the shortest segment joining the material of a face to a plane.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public static GeoLine3 GetShortestLineTo(GeoFace3 face, GeoPlane3 plane) => GetShortestLineTo(face, plane, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment joining the material of a face to a plane.
        /// </summary>
        /// <param name="face">The face the answer leaves, read by its material.</param>
        /// <param name="plane">The plane the answer lands on.</param>
        /// <param name="tolerance">
        /// The tolerance; not read, since the segment is the true one: a corner a hair off the plane is joined to it by a
        /// segment that short, where <see cref="Distance3.DistanceTo(GeoPlane3, GeoFace3, Tolerance)"/> reads nought.
        /// </param>
        /// <returns>
        /// The answer for the outline, which decides: a plane reaching the outline reaches the material along it, the holes
        /// lie within it, and the corner of the material nearest a plane clear of it is a corner of the outline. A segment
        /// of no length at the first point of contact walking the outline's edges in order, or the perpendicular from the
        /// nearest corner of the outline, the first of them where several are equally near; beyond the point tolerance its
        /// length is <see cref="Distance3.DistanceTo(GeoPlane3, GeoFace3, Tolerance)"/>. The square 100 by 100 with a hole
        /// 40 by 40 in its middle, stood upright along y = 50 from z = 10 to 110, is joined from its corner (0, 50, 10)
        /// to the floor plane, 10 long; stood from z = -50 to 50, it meets the floor at (100, 50, 0).
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public static GeoLine3 GetShortestLineTo(GeoFace3 face, GeoPlane3 plane, Tolerance tolerance)
        {
            if (face == null)
            {
                throw new ArgumentNullException(nameof(face));
            }

            return JoinToPlane(face.Boundary.Vertices, plane);
        }

        /// <summary>
        /// Gets the shortest segment joining a ray to a plane.
        /// </summary>
        public static GeoLine3 GetShortestLineTo(GeoRay3 ray, GeoPlane3 plane) => GetShortestLineTo(ray, plane, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment joining a ray to a plane.
        /// </summary>
        /// <param name="ray">The ray the answer leaves.</param>
        /// <param name="plane">The plane the answer lands on.</param>
        /// <param name="tolerance">
        /// The tolerance; not read, since the segment is the true one: an origin a hair off the plane, the ray running
        /// along it or away from it, is joined by a segment that short, where
        /// <see cref="Distance3.DistanceTo(GeoRay3, GeoPlane3, Tolerance)"/> reads nought.
        /// </param>
        /// <returns>
        /// A segment of no length at the origin where that lies on the plane, and where the ray passes through when it runs
        /// towards the plane; otherwise the perpendicular from the origin, the ray's only corner. The ray from (0, 0, 10)
        /// pointing straight down meets the floor plane at the origin; pointing straight up, or along X, it is joined from
        /// (0, 0, 10) straight down, 10 long.
        /// </returns>
        public static GeoLine3 GetShortestLineTo(GeoRay3 ray, GeoPlane3 plane, Tolerance tolerance)
        {
            double height = plane.SignedDistanceTo(ray.Origin);
            double climb = ray.Direction.DotProduct(plane.Normal);

            // Standing strictly to one side and running along the plane or away from it, the ray is nearest at its origin.
            bool clear = (height > 0.0 && climb >= 0.0) || (height < 0.0 && climb <= 0.0);

            if (!clear)
            {
                double along = height > 0.0 || height < 0.0 ? -height / climb : 0.0;

                // A ray parallel to rounding would reach the plane past any number there is; it is read as running along it.
                if (!double.IsInfinity(along))
                {
                    GeoPoint3 at = ray.GetPointAtDistance(along);

                    return new GeoLine3(at, at);
                }
            }

            return new GeoLine3(ray.Origin, ProjectToPlane(plane, ray.Origin));
        }

        /// <summary>
        /// Joins a run of corners, closed round from the last to the first, to a plane: a segment of no length at the
        /// first point of contact, otherwise the perpendicular from the nearest corner, the first of those equally near.
        /// </summary>
        private static GeoLine3 JoinToPlane(IReadOnlyList<GeoPoint3> corners, GeoPlane3 plane)
        {
            // A corner that is not a number stands at no height, and the shape is not a number away (amendment D7): the
            // walk below would read it as reaching the plane and give a segment of no length.
            foreach (GeoPoint3 corner in corners)
            {
                if (double.IsNaN(plane.SignedDistanceTo(corner)))
                {
                    return new GeoLine3(corner, ProjectToPlane(plane, corner));
                }
            }

            if (TryReachPlane(corners, true, plane, out GeoPoint3 at))
            {
                return new GeoLine3(at, at);
            }

            GeoPoint3 nearest = corners[0];
            double reach = double.MaxValue;

            Nearer(corners, plane, ref nearest, ref reach);

            return new GeoLine3(nearest, ProjectToPlane(plane, nearest));
        }

        #endregion

        #region Rays

        /// <summary>
        /// Gets the shortest segment joining two rays.
        /// </summary>
        public static GeoLine3 GetShortestLineTo(GeoRay3 ray1, GeoRay3 ray2) => GetShortestLineTo(ray1, ray2, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment joining two rays.
        /// </summary>
        /// <param name="ray1">The ray the answer leaves.</param>
        /// <param name="ray2">The ray the answer lands on.</param>
        /// <param name="tolerance">
        /// The tolerance; not read, since the segment is the true one: rays passing a hair apart are joined by a segment
        /// that short, which is their distance <see cref="Distance3.DistanceTo(GeoRay3, GeoRay3, Tolerance)"/> too.
        /// </param>
        /// <returns>
        /// Where the two run past each other, the common perpendicular of their lines when both rays reach it, and
        /// otherwise the shorter of the segments from one origin to the nearest point of the other ray, the one from the
        /// first origin on a tie; for rays parallel within rounding, where every facing pair is as near as any other, that
        /// shorter of the two from the origins. The ray from the origin along X and the ray from (50, 30, 40) along Y are
        /// joined from (50, 0, 0) to (50, 30, 40), 50 long; the ray from (50, -50, 0) along Y crosses the first and the
        /// segment has no length at (50, 0, 0).
        /// </returns>
        public static GeoLine3 GetShortestLineTo(GeoRay3 ray1, GeoRay3 ray2, Tolerance tolerance)
        {
            if (Intersection3.TryGetBridge(ray1, ray2, out GeoLine3 bridge))
            {
                return bridge;
            }

            var fromFirst = new GeoLine3(ray1.Origin, ProjectToRay(ray2, ray1.Origin));
            var toSecond = new GeoLine3(ProjectToRay(ray1, ray2.Origin), ray2.Origin);

            return toSecond.Length < fromFirst.Length ? toSecond : fromFirst;
        }

        /// <summary>
        /// Gets the shortest segment joining a polyline to a ray.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the polyline is null.</exception>
        public static GeoLine3 GetShortestLineTo(GeoPolyline3 polyline, GeoRay3 ray) => GetShortestLineTo(polyline, ray, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment joining a polyline to a ray, within a tolerance.
        /// </summary>
        /// <param name="polyline">The chain the answer leaves.</param>
        /// <param name="ray">The ray the answer lands on.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// The shortest of the segments <see cref="GetShortestLineTo(GeoRay3, GeoLine3, Tolerance)"/> gives for each edge,
        /// turned round, the first edge winning a tie. Beyond the point tolerance it is as long as
        /// <see cref="Distance3.DistanceTo(GeoPolyline3, GeoRay3, Tolerance)"/>. The chain (0, 0, 0), (100, 0, 0),
        /// (100, 100, 0) is joined to the ray from (60, 50, 30) pointing straight up from (100, 50, 0), on its second edge,
        /// to (60, 50, 30), 50 long.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when the polyline is null.</exception>
        public static GeoLine3 GetShortestLineTo(GeoPolyline3 polyline, GeoRay3 ray, Tolerance tolerance)
        {
            if (polyline == null)
            {
                throw new ArgumentNullException(nameof(polyline));
            }

            GeoLine3 best = JoinRayToSegment(ray, polyline.GetEdgeAt(0)).Reverse();
            double shortest = best.Length;

            for (int i = 1; i < polyline.EdgeCount && shortest > 0.0; i++)
            {
                Consider(ref best, ref shortest, JoinRayToSegment(ray, polyline.GetEdgeAt(i)).Reverse());
            }

            return best;
        }

        /// <summary>
        /// Gets the segment <see cref="Closest(GeoRay3, GeoLine3, Tolerance)"/> gives, the same candidates
        /// weighed in the same order, but keeping a length that is not a number, so that a chain with a corner that is
        /// not a number is not a number away from a ray. The segment is a side of a chain, measured as the side it is however
        /// short.
        /// </summary>
        private static GeoLine3 JoinRayToSegment(GeoRay3 ray, GeoLine3 line)
        {
            var best = new GeoLine3(ProjectToRay(ray, line.StartPoint), line.StartPoint);
            double shortest = best.Length;

            Consider(ref best, ref shortest, new GeoLine3(ProjectToRay(ray, line.EndPoint), line.EndPoint));
            Consider(ref best, ref shortest, new GeoLine3(ray.Origin, ProjectToLine(line, ray.Origin, Exactly)));

            GeoLine3 across = GetShortestLineTo(ray.ToLine(1.0), line, Enums.LineExtension.First, Exactly);

            Consider(ref best, ref shortest, new GeoLine3(
                ProjectToRay(ray, across.StartPoint),
                ProjectToLine(line, across.EndPoint, Exactly)));

            return best;
        }

        #endregion

        #region A body against a face

        /// <summary>
        /// Gets the shortest segment joining a solid to the material of a face.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the solid or the face is null.</exception>
        public static GeoLine3 GetShortestLineTo(GeoSolid3 solid, GeoFace3 face) => GetShortestLineTo(solid, face, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment joining a solid to the material of a face, within a tolerance.
        /// </summary>
        /// <param name="solid">The solid the answer leaves.</param>
        /// <param name="face">The face the answer lands on, read by its material.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// A segment leaving the surface of the solid and landing on the material, of no length where the two surfaces
        /// meet. A face lying wholly inside the body is joined out to the nearest face, where
        /// <see cref="Distance3.DistanceTo(GeoSolid3, GeoFace3, Tolerance)"/> reads the body as solid through and answers
        /// nought; outside, it is as long as that distance. The material is cut into the triangles
        /// <see cref="GeoFace3.TriangulateSurface(Tolerance)"/> gives, as a polygon is for
        /// <see cref="GetShortestLineTo(GeoSolid3, GeoPolygon3, Tolerance)"/>, so no triangle covers a hole. A tie goes to
        /// the first face of the body, and within it to the first of those triangles. The box 100 by 100 by 100 with a
        /// corner at the origin and the square 200 by 200 from (-50, -50, 50) to (150, 150, 50) with a hole 150 by 150 in
        /// its middle, which the box passes through, are joined across the 25 between a side of the box and the rim.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when the solid or the face is null.</exception>
        public static GeoLine3 GetShortestLineTo(GeoSolid3 solid, GeoFace3 face, Tolerance tolerance)
        {
            if (solid == null)
            {
                throw new ArgumentNullException(nameof(solid));
            }

            if (face == null)
            {
                throw new ArgumentNullException(nameof(face));
            }

            // The faces run straight across every opening; the nearest material can sit on any opening's rim.
            solid = Material3.Whole(solid, tolerance);

            GeoTriangle3[] faces = solid.TriangulateQuietly(tolerance);
            GeoTriangle3[] pieces = face.TriangulateSurface(tolerance);
            GeoLine3 best = GetShortestLineTo(faces[0], pieces[0], tolerance);
            double shortest = best.Length;

            for (int i = 0; i < faces.Length; i++)
            {
                for (int j = i == 0 ? 1 : 0; j < pieces.Length; j++)
                {
                    if (shortest <= 0.0)
                    {
                        return best;
                    }

                    Consider(ref best, ref shortest, GetShortestLineTo(faces[i], pieces[j], tolerance));
                }
            }

            return best;
        }

        #endregion
    }
}
