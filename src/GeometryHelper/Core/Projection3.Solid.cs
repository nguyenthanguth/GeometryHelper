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
            // Two triangles one of which lies clear to one side of the other's plane have no crossing to find, and
            // saying so costs three distances rather than six crossings looked for in vain.
            bool apart = !first.IsDegenerate(tolerance) && !second.IsDegenerate(tolerance)
                && (Collision3.IsClearOf(first, second, tolerance) || Collision3.IsClearOf(second, first, tolerance));

            for (int i = 0; i < 3 && !apart; i++)
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

            // The faces run straight across every opening; the nearest material can sit on any opening's rim.
            solid = Material3.Whole(solid, tolerance);

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

            // The faces run straight across every opening; the nearest material can sit on any opening's rim.
            solid = Material3.Whole(solid, tolerance);

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

            // The faces run straight across every opening; the nearest material can sit on any opening's rim.
            first = Material3.Whole(first, tolerance);
            second = Material3.Whole(second, tolerance);

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

        /// <summary>
        /// Gets the shortest segment joining a solid to a polyline.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the solid or the polyline is null.</exception>
        public static GeoLine3 GetShortestLineTo(GeoSolid3 solid, GeoPolyline3 polyline) => GetShortestLineTo(solid, polyline, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment joining a solid to a polyline, within a tolerance.
        /// </summary>
        /// <param name="solid">The solid the answer leaves.</param>
        /// <param name="polyline">The chain the answer lands on.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// A segment leaving the surface of the solid and landing on the chain, of no length where the chain passes
        /// through the surface. A chain lying wholly inside the body is joined out to the nearest face, where
        /// <see cref="Distance3.DistanceTo(GeoSolid3, GeoPolyline3, Tolerance)"/> reads the body as solid through and
        /// answers nought; outside, it is as long as that distance. A tie goes to the first face, and within it to the first
        /// edge of the chain. In the box 100 by 100 by 100 with a corner at the origin, the chain (50, 50, 10),
        /// (50, 50, 60), (50, 70, 60) is joined from the bottom face at (50, 50, 0) to (50, 50, 10), 10 long.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when the solid or the polyline is null.</exception>
        public static GeoLine3 GetShortestLineTo(GeoSolid3 solid, GeoPolyline3 polyline, Tolerance tolerance)
        {
            if (solid == null)
            {
                throw new ArgumentNullException(nameof(solid));
            }

            if (polyline == null)
            {
                throw new ArgumentNullException(nameof(polyline));
            }

            // The faces run straight across every opening; the nearest material can sit on any opening's rim.
            solid = Material3.Whole(solid, tolerance);

            GeoTriangle3[] faces = solid.Triangulate(tolerance);
            GeoLine3 best = GetShortestLineTo(polyline.GetEdgeAt(0), faces[0], tolerance).Reverse();
            double shortest = best.Length;

            for (int i = 0; i < faces.Length; i++)
            {
                for (int j = i == 0 ? 1 : 0; j < polyline.EdgeCount; j++)
                {
                    if (shortest <= 0.0)
                    {
                        return best;
                    }

                    Consider(ref best, ref shortest, GetShortestLineTo(polyline.GetEdgeAt(j), faces[i], tolerance).Reverse());
                }
            }

            return best;
        }

        /// <summary>
        /// Gets the shortest segment joining a solid to a polygon.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the solid or the polygon is null.</exception>
        public static GeoLine3 GetShortestLineTo(GeoSolid3 solid, GeoPolygon3 polygon) => GetShortestLineTo(solid, polygon, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment joining a solid to a polygon, within a tolerance.
        /// </summary>
        /// <param name="solid">The solid the answer leaves.</param>
        /// <param name="polygon">The polygon the answer lands on, read as a filled surface.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// A segment leaving the surface of the solid and landing on the polygon, of no length where the two surfaces meet.
        /// A polygon lying wholly inside the body is joined out to the nearest face, where
        /// <see cref="Distance3.DistanceTo(GeoSolid3, GeoPolygon3, Tolerance)"/> reads the body as solid through and
        /// answers nought; outside, it is as long as that distance. The polygon is cut into triangles that each lie inside
        /// it, as for the distance. A tie goes to the first face, and within it to the first of those triangles. In the box
        /// 100 by 100 by 100 with a corner at the origin, the square 20 by 20 lying flat at a height of 30 with a corner at
        /// (40, 40, 30) is joined from the bottom face to it, 30 long.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when the solid or the polygon is null.</exception>
        public static GeoLine3 GetShortestLineTo(GeoSolid3 solid, GeoPolygon3 polygon, Tolerance tolerance)
        {
            if (solid == null)
            {
                throw new ArgumentNullException(nameof(solid));
            }

            if (polygon == null)
            {
                throw new ArgumentNullException(nameof(polygon));
            }

            // The faces run straight across every opening; the nearest material can sit on any opening's rim.
            solid = Material3.Whole(solid, tolerance);

            GeoTriangle3[] faces = solid.Triangulate(tolerance);
            GeoTriangle3[] pieces = new GeoFace3(polygon).TriangulateSurface(tolerance);
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

        /// <summary>
        /// Gets the shortest segment joining a solid to a plane.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the solid is null.</exception>
        public static GeoLine3 GetShortestLineTo(GeoSolid3 solid, GeoPlane3 plane) => GetShortestLineTo(solid, plane, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment joining a solid to a plane, within a tolerance.
        /// </summary>
        /// <param name="solid">The solid the answer leaves.</param>
        /// <param name="plane">The plane the answer lands on.</param>
        /// <param name="tolerance">The tolerance the openings are cut in and the faces cut into triangles with.</param>
        /// <returns>
        /// A segment of no length where the body crosses or touches the plane, at the first point of contact walking the
        /// triangles of its surface in the order the other shortest lines here walk them, and the edges of each in turn:
        /// the start of the first edge that reaches the plane when that start lies on it, otherwise where the edge passes
        /// through, otherwise its end. Clear of the plane, the perpendicular from the nearest corner, the first of them
        /// where several are equally near; beyond the point tolerance its length is
        /// <see cref="Distance3.DistanceTo(GeoSolid3, GeoPlane3, Tolerance)"/>. The box 100 by 100 by 100 with a corner at
        /// the origin is joined to the plane 30 under its bottom face from a corner of that face, 30 long, and to the
        /// plane through its middle by a segment of no length at the height of 50.
        /// </returns>
        /// <remarks>
        /// The segment is never snapped. A body standing 0.0004 above the plane is joined by a segment 0.0004 long,
        /// though the distance, read within the point tolerance, is nought. The distance reads only where the corners
        /// stand, so a body of two separate pieces lying either side of the plane, neither touching it, is nought from
        /// it too, and is joined from its nearest corner all the same.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the solid is null.</exception>
        public static GeoLine3 GetShortestLineTo(GeoSolid3 solid, GeoPlane3 plane, Tolerance tolerance)
        {
            if (solid == null)
            {
                throw new ArgumentNullException(nameof(solid));
            }

            // The faces run straight across every opening; the nearest material can sit on any opening's rim.
            solid = Material3.Whole(solid, tolerance);

            GeoTriangle3[] faces = solid.Triangulate(tolerance);

            foreach (GeoTriangle3 face in faces)
            {
                if (TryReachPlane(new[] { face.A, face.B, face.C }, true, plane, out GeoPoint3 at))
                {
                    return new GeoLine3(at, at);
                }
            }

            GeoPoint3 nearest = faces[0].A;
            double reach = double.MaxValue;

            foreach (GeoTriangle3 face in faces)
            {
                Nearer(new[] { face.A, face.B, face.C }, plane, ref nearest, ref reach);
            }

            return new GeoLine3(nearest, ProjectToPlane(plane, nearest));
        }

        #endregion

        #region Rays

        /// <summary>
        /// Gets the shortest segment joining a ray to a line segment.
        /// </summary>
        public static GeoLine3 GetShortestLineTo(GeoRay3 ray, GeoLine3 line) => GetShortestLineTo(ray, line, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment joining a ray to a line segment, within a tolerance.
        /// </summary>
        /// <param name="ray">The ray the answer leaves.</param>
        /// <param name="line">The segment the answer lands on.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>A segment leaving <paramref name="ray"/> and landing on <paramref name="line"/>.</returns>
        /// <remarks>
        /// The nearest pair is either where the two run past each other, or at the origin of the ray, or
        /// under an end of the segment. The first of those is found by reading the ray as the whole line
        /// through it and then clamping what comes back onto the ray and onto the segment, so the pair is
        /// one both shapes really hold whether or not the perpendicular falls where they live.
        /// </remarks>
        public static GeoLine3 GetShortestLineTo(GeoRay3 ray, GeoLine3 line, Tolerance tolerance)
        {
            var best = new GeoLine3(ProjectToRay(ray, line.StartPoint), line.StartPoint);

            Consider(ref best, new GeoLine3(ProjectToRay(ray, line.EndPoint), line.EndPoint));
            Consider(ref best, new GeoLine3(ray.Origin, ProjectToLine(line, ray.Origin)));

            GeoLine3 across = GetShortestLineTo(ray.ToLine(1.0), line, Enums.LineExtension.First, tolerance);

            Consider(ref best, new GeoLine3(
                ProjectToRay(ray, across.StartPoint),
                ProjectToLine(line, across.EndPoint)));

            return best;
        }

        /// <summary>
        /// Gets the shortest segment joining a ray to a triangle.
        /// </summary>
        public static GeoLine3 GetShortestLineTo(GeoRay3 ray, GeoTriangle3 triangle) => GetShortestLineTo(ray, triangle, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment joining a ray to a triangle, within a tolerance.
        /// </summary>
        /// <returns>A segment leaving <paramref name="ray"/> and landing on <paramref name="triangle"/>, of no length at all where the ray pierces the face.</returns>
        public static GeoLine3 GetShortestLineTo(GeoRay3 ray, GeoTriangle3 triangle, Tolerance tolerance)
        {
            if (Intersection3.TryIntersectWith(ray, triangle, out GeoPoint3 through, tolerance))
            {
                return new GeoLine3(through, through);
            }

            var best = new GeoLine3(ray.Origin, ProjectToTriangle(triangle, ray.Origin));

            for (int i = 0; i < 3; i++)
            {
                Consider(ref best, GetShortestLineTo(ray, triangle.GetEdgeAt(i), tolerance));
            }

            return best;
        }

        /// <summary>
        /// Gets the shortest segment joining a solid to a ray.
        /// </summary>
        public static GeoLine3 GetShortestLineTo(GeoSolid3 solid, GeoRay3 ray) => GetShortestLineTo(solid, ray, Tolerance.Global);

        /// <summary>
        /// Gets the shortest segment joining a solid to a ray, within a tolerance.
        /// </summary>
        /// <returns>A segment leaving the surface of the solid and landing on <paramref name="ray"/>.</returns>
        /// <remarks>
        /// A ray starting inside a closed body has to come out through a face, so the answer there is of no
        /// length rather than a measurement out to the skin.
        /// </remarks>
        public static GeoLine3 GetShortestLineTo(GeoSolid3 solid, GeoRay3 ray, Tolerance tolerance)
        {
            if (solid == null)
            {
                throw new ArgumentNullException(nameof(solid));
            }

            // The faces run straight across every opening; the nearest material can sit on any opening's rim.
            solid = Material3.Whole(solid, tolerance);

            GeoTriangle3[] faces = solid.Triangulate(tolerance);
            GeoLine3 best = GetShortestLineTo(ray, faces[0], tolerance).Reverse();

            foreach (GeoTriangle3 face in faces)
            {
                Consider(ref best, GetShortestLineTo(ray, face, tolerance).Reverse());

                if (best.Length <= 0.0)
                {
                    return best;
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
