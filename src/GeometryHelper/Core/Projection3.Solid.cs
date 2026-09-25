using System;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    /// <summary>
    /// The shortest segment joining a solid, a triangle or a segment in space to another of them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The library could already say how far apart two of these stood; this says between which two points.
    /// The segment is exactly as long as that distance, and each of its ends lies on the surface it came
    /// from, so it can be drawn as it is.
    /// </para>
    /// <para>
    /// Both ends sit on a surface. A body lying wholly inside another therefore reports the gap out to the
    /// skin of the other, where <see cref="Distance3"/> reads a solid as a filled region and answers
    /// nothing at all. The two disagree there on purpose, the same way they do in the plane.
    /// </para>
    /// </remarks>
    public static partial class Projection3
    {
        #region Triangles

        /// <summary>
        /// Gets the shortest segment joining a segment to a triangle.
        /// </summary>
        public static GeoLine3 GetShortestLineTo(GeoLine3 line, GeoTriangle3 triangle) => GetShortestLineTo(line, triangle, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment joining a segment to a triangle, within a tolerance.
        /// </summary>
        /// <param name="line">The segment the answer leaves.</param>
        /// <param name="triangle">The triangle the answer lands on.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>A segment leaving <paramref name="line"/> and landing on <paramref name="triangle"/>, of no length at all where the two meet.</returns>
        /// <remarks>
        /// A segment that passes clean through the face meets none of its edges, so the crossing is looked
        /// for first; after that the nearest pair is either between the segment and an edge, or under one
        /// of the segment ends.
        /// </remarks>
        public static GeoLine3 GetShortestLineTo(GeoLine3 line, GeoTriangle3 triangle, Tolerance tolerance)
        {
            if (Intersection3.TryIntersectWith(line, triangle, out GeoPoint3 through, tolerance))
            {
                return new GeoLine3(through, through);
            }

            GeoLine3 best = GetShortestLineTo(line, triangle.GetEdgeAt(0), tolerance);

            for (int i = 1; i < 3; i++)
            {
                Consider(ref best, GetShortestLineTo(line, triangle.GetEdgeAt(i), tolerance));
            }

            Consider(ref best, new GeoLine3(line.StartPoint, ProjectToTriangle(triangle, line.StartPoint)));
            Consider(ref best, new GeoLine3(line.EndPoint, ProjectToTriangle(triangle, line.EndPoint)));

            return best;
        }

        /// <summary>
        /// Gets the shortest segment joining two triangles.
        /// </summary>
        public static GeoLine3 GetShortestLineTo(GeoTriangle3 first, GeoTriangle3 second) => GetShortestLineTo(first, second, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment joining two triangles, within a tolerance.
        /// </summary>
        /// <param name="first">The triangle the answer leaves.</param>
        /// <param name="second">The triangle the answer lands on.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>A segment leaving <paramref name="first"/> and landing on <paramref name="second"/>, of no length at all where the two meet.</returns>
        /// <remarks>
        /// Two faces that cross share a segment whose ends lie on an edge of one and inside the other, so
        /// the crossings of each edge with the other face are looked for first. Failing that the nearest
        /// pair is between two edges, or under a corner of one lying over the face of the other, which is
        /// the same set of candidates <see cref="Distance3.DistanceTo(GeoTriangle3, GeoTriangle3, Tolerance)"/>
        /// weighs.
        /// </remarks>
        public static GeoLine3 GetShortestLineTo(GeoTriangle3 first, GeoTriangle3 second, Tolerance tolerance)
        {
            for (int i = 0; i < 3; i++)
            {
                if (Intersection3.TryIntersectWith(first.GetEdgeAt(i), second, out GeoPoint3 through, tolerance) ||
                    Intersection3.TryIntersectWith(second.GetEdgeAt(i), first, out through, tolerance))
                {
                    return new GeoLine3(through, through);
                }
            }

            GeoLine3 best = GetShortestLineTo(first.GetEdgeAt(0), second.GetEdgeAt(0), tolerance);

            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    Consider(ref best, GetShortestLineTo(first.GetEdgeAt(i), second.GetEdgeAt(j), tolerance));
                }
            }

            for (int i = 0; i < 3; i++)
            {
                Consider(ref best, new GeoLine3(first[i], ProjectToTriangle(second, first[i])));
                Consider(ref best, new GeoLine3(ProjectToTriangle(first, second[i]), second[i]));
            }

            return best;
        }

        #endregion

        #region Solids

        /// <summary>
        /// Gets the shortest segment joining a solid to a point.
        /// </summary>
        public static GeoLine3 GetShortestLineTo(GeoSolid3 solid, GeoPoint3 point) => GetShortestLineTo(solid, point, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment joining a solid to a point, within a tolerance.
        /// </summary>
        /// <returns>A segment leaving the surface of the solid and ending at <paramref name="point"/>, which has a length even for a point inside the body.</returns>
        public static GeoLine3 GetShortestLineTo(GeoSolid3 solid, GeoPoint3 point, Tolerance tolerance)
        {
            if (solid == null)
            {
                throw new ArgumentNullException(nameof(solid));
            }

            return new GeoLine3(ProjectToSolid(solid, point, tolerance), point);
        }

        /// <summary>
        /// Gets the shortest segment joining a solid to a segment.
        /// </summary>
        public static GeoLine3 GetShortestLineTo(GeoSolid3 solid, GeoLine3 line) => GetShortestLineTo(solid, line, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment joining a solid to a segment, within a tolerance.
        /// </summary>
        public static GeoLine3 GetShortestLineTo(GeoSolid3 solid, GeoLine3 line, Tolerance tolerance)
        {
            if (solid == null)
            {
                throw new ArgumentNullException(nameof(solid));
            }

            GeoTriangle3[] faces = solid.Triangulate(tolerance);
            GeoLine3 best = GetShortestLineTo(line, faces[0], tolerance).Reverse();

            foreach (GeoTriangle3 face in faces)
            {
                Consider(ref best, GetShortestLineTo(line, face, tolerance).Reverse());

                if (best.Length <= 0.0)
                {
                    return best;
                }
            }

            return best;
        }

        /// <summary>
        /// Gets the shortest segment joining a solid to a triangle.
        /// </summary>
        public static GeoLine3 GetShortestLineTo(GeoSolid3 solid, GeoTriangle3 triangle) => GetShortestLineTo(solid, triangle, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment joining a solid to a triangle, within a tolerance.
        /// </summary>
        public static GeoLine3 GetShortestLineTo(GeoSolid3 solid, GeoTriangle3 triangle, Tolerance tolerance)
        {
            if (solid == null)
            {
                throw new ArgumentNullException(nameof(solid));
            }

            GeoTriangle3[] faces = solid.Triangulate(tolerance);
            GeoLine3 best = GetShortestLineTo(faces[0], triangle, tolerance);

            foreach (GeoTriangle3 face in faces)
            {
                Consider(ref best, GetShortestLineTo(face, triangle, tolerance));

                if (best.Length <= 0.0)
                {
                    return best;
                }
            }

            return best;
        }

        /// <summary>
        /// Gets the shortest segment joining two solids.
        /// </summary>
        public static GeoLine3 GetShortestLineTo(GeoSolid3 first, GeoSolid3 second) => GetShortestLineTo(first, second, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment joining two solids, within a tolerance.
        /// </summary>
        /// <param name="first">The solid the answer leaves.</param>
        /// <param name="second">The solid the answer lands on.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>A segment leaving the surface of <paramref name="first"/> and landing on the surface of <paramref name="second"/>.</returns>
        /// <remarks>
        /// Every face is weighed against every face, but a pair whose boxes already stand farther apart
        /// than the best segment found so far is passed over without being measured, which is what makes
        /// the walk bearable on bodies of any size.
        /// </remarks>
        public static GeoLine3 GetShortestLineTo(GeoSolid3 first, GeoSolid3 second, Tolerance tolerance)
        {
            if (first == null)
            {
                throw new ArgumentNullException(nameof(first));
            }

            if (second == null)
            {
                throw new ArgumentNullException(nameof(second));
            }

            GeoTriangle3[] ours = first.Triangulate(tolerance);
            GeoTriangle3[] theirs = second.Triangulate(tolerance);

            GeoLine3 best = GetShortestLineTo(ours[0], theirs[0], tolerance);

            foreach (GeoTriangle3 face in ours)
            {
                GeoAabb3 box = face.GetAabb();

                foreach (GeoTriangle3 other in theirs)
                {
                    if (box.DistanceTo(other.GetAabb()) >= best.Length)
                    {
                        continue;
                    }

                    Consider(ref best, GetShortestLineTo(face, other, tolerance));

                    if (best.Length <= 0.0)
                    {
                        return best;
                    }
                }
            }

            return best;
        }

        #endregion

        /// <summary>
        /// Keeps the shorter of the segment in hand and the one offered.
        /// </summary>
        /// <remarks>
        /// The comparison is strict, so where two candidates are the same length the earlier one stands and
        /// the answer does not turn on the order they were weighed in.
        /// </remarks>
        private static void Consider(ref GeoLine3 best, GeoLine3 candidate)
        {
            if (candidate.Length < best.Length)
            {
                best = candidate;
            }
        }
    }
}
