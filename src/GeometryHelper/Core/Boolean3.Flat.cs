using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    /// <summary>
    /// Combining two flat shapes that lie in one plane in space.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="Boolean3"/> combined solids and nothing else, so a <see cref="GeoPolygon3"/> and a
    /// <see cref="GeoFace3"/> — which are areas, not bodies — could not be combined at all, though the plane
    /// library has done it all along and a curved loop in space already does it through its own lift.
    /// </para>
    /// <para>
    /// Nothing new is worked out. Both shapes are laid out in the first one's frame, the plane library answers,
    /// and the answer is lifted back, so it is exact rather than approximate. Everything comes back as
    /// <see cref="GeoFace3"/>, because joining two areas can leave a hole in the middle and only a face can
    /// hold one — the same reason the plane hands back <see cref="GeoFace2"/>.
    /// </para>
    /// <para>
    /// <b>The second shape has to lie in the first one's plane, and is refused when it does not.</b> Projecting
    /// it in would report two plates a metre apart as overlapping and say nothing about it. This is the reading
    /// the rest of the library takes, and <c>SharesPlaneWith</c> on either type asks the question beforehand.
    /// Winding does not matter: a shape wound the other way round is read as the same area, not as a hole.
    /// </para>
    /// </remarks>
    public static partial class Boolean3
    {
        #region Combining two flat shapes in one plane

        /// <summary>
        /// Joins two shapes lying in one plane.
        /// </summary>
        public static GeoFace3[] Union(GeoPolygon3 first, GeoPolygon3 second)
            => Union(first, second, Tolerance.Global);

        /// <summary>
        /// Joins two shapes lying in one plane, within a tolerance.
        /// </summary>
        /// <param name="first">The polygon.</param>
        /// <param name="second">The polygon; it has to lie in the first shape's plane.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The pieces the two make, in the plane they share.</returns>
        /// <exception cref="ArgumentNullException">Thrown when either shape is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the two lie in different planes.</exception>
        public static GeoFace3[] Union(GeoPolygon3 first, GeoPolygon3 second, Tolerance tolerance)
        {
            GeoFace3 subject = AsFace(first, nameof(first));
            GeoFace3 other = AsFace(second, nameof(second));
            GeoCoordinateSystem3 frame = FlatFrame(subject, other, tolerance, nameof(second));

            return Combine(Combination.Union, frame, subject, other, tolerance);
        }

        /// <summary>
        /// Joins two shapes lying in one plane.
        /// </summary>
        public static GeoFace3[] Union(GeoPolygon3 first, GeoFace3 second)
            => Union(first, second, Tolerance.Global);

        /// <summary>
        /// Joins two shapes lying in one plane, within a tolerance.
        /// </summary>
        /// <param name="first">The polygon.</param>
        /// <param name="second">The face; it has to lie in the first shape's plane.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The pieces the two make, in the plane they share.</returns>
        /// <exception cref="ArgumentNullException">Thrown when either shape is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the two lie in different planes.</exception>
        public static GeoFace3[] Union(GeoPolygon3 first, GeoFace3 second, Tolerance tolerance)
        {
            GeoFace3 subject = AsFace(first, nameof(first));
            GeoFace3 other = NotNull(second, nameof(second));
            GeoCoordinateSystem3 frame = FlatFrame(subject, other, tolerance, nameof(second));

            return Combine(Combination.Union, frame, subject, other, tolerance);
        }

        /// <summary>
        /// Joins two shapes lying in one plane.
        /// </summary>
        public static GeoFace3[] Union(GeoFace3 first, GeoPolygon3 second)
            => Union(first, second, Tolerance.Global);

        /// <summary>
        /// Joins two shapes lying in one plane, within a tolerance.
        /// </summary>
        /// <param name="first">The face.</param>
        /// <param name="second">The polygon; it has to lie in the first shape's plane.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The pieces the two make, in the plane they share.</returns>
        /// <exception cref="ArgumentNullException">Thrown when either shape is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the two lie in different planes.</exception>
        public static GeoFace3[] Union(GeoFace3 first, GeoPolygon3 second, Tolerance tolerance)
        {
            GeoFace3 subject = NotNull(first, nameof(first));
            GeoFace3 other = AsFace(second, nameof(second));
            GeoCoordinateSystem3 frame = FlatFrame(subject, other, tolerance, nameof(second));

            return Combine(Combination.Union, frame, subject, other, tolerance);
        }

        /// <summary>
        /// Joins two shapes lying in one plane.
        /// </summary>
        public static GeoFace3[] Union(GeoFace3 first, GeoFace3 second)
            => Union(first, second, Tolerance.Global);

        /// <summary>
        /// Joins two shapes lying in one plane, within a tolerance.
        /// </summary>
        /// <param name="first">The face.</param>
        /// <param name="second">The face; it has to lie in the first shape's plane.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The pieces the two make, in the plane they share.</returns>
        /// <exception cref="ArgumentNullException">Thrown when either shape is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the two lie in different planes.</exception>
        public static GeoFace3[] Union(GeoFace3 first, GeoFace3 second, Tolerance tolerance)
        {
            GeoFace3 subject = NotNull(first, nameof(first));
            GeoFace3 other = NotNull(second, nameof(second));
            GeoCoordinateSystem3 frame = FlatFrame(subject, other, tolerance, nameof(second));

            return Combine(Combination.Union, frame, subject, other, tolerance);
        }

        /// <summary>
        /// Keeps what two shapes lying in one plane both cover.
        /// </summary>
        public static GeoFace3[] Intersect(GeoPolygon3 first, GeoPolygon3 second)
            => Intersect(first, second, Tolerance.Global);

        /// <summary>
        /// Keeps what two shapes lying in one plane both cover, within a tolerance.
        /// </summary>
        /// <param name="first">The polygon.</param>
        /// <param name="second">The polygon; it has to lie in the first shape's plane.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The pieces the two make, in the plane they share.</returns>
        /// <exception cref="ArgumentNullException">Thrown when either shape is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the two lie in different planes.</exception>
        public static GeoFace3[] Intersect(GeoPolygon3 first, GeoPolygon3 second, Tolerance tolerance)
        {
            GeoFace3 subject = AsFace(first, nameof(first));
            GeoFace3 other = AsFace(second, nameof(second));
            GeoCoordinateSystem3 frame = FlatFrame(subject, other, tolerance, nameof(second));

            return Combine(Combination.Intersect, frame, subject, other, tolerance);
        }

        /// <summary>
        /// Keeps what two shapes lying in one plane both cover.
        /// </summary>
        public static GeoFace3[] Intersect(GeoPolygon3 first, GeoFace3 second)
            => Intersect(first, second, Tolerance.Global);

        /// <summary>
        /// Keeps what two shapes lying in one plane both cover, within a tolerance.
        /// </summary>
        /// <param name="first">The polygon.</param>
        /// <param name="second">The face; it has to lie in the first shape's plane.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The pieces the two make, in the plane they share.</returns>
        /// <exception cref="ArgumentNullException">Thrown when either shape is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the two lie in different planes.</exception>
        public static GeoFace3[] Intersect(GeoPolygon3 first, GeoFace3 second, Tolerance tolerance)
        {
            GeoFace3 subject = AsFace(first, nameof(first));
            GeoFace3 other = NotNull(second, nameof(second));
            GeoCoordinateSystem3 frame = FlatFrame(subject, other, tolerance, nameof(second));

            return Combine(Combination.Intersect, frame, subject, other, tolerance);
        }

        /// <summary>
        /// Keeps what two shapes lying in one plane both cover.
        /// </summary>
        public static GeoFace3[] Intersect(GeoFace3 first, GeoPolygon3 second)
            => Intersect(first, second, Tolerance.Global);

        /// <summary>
        /// Keeps what two shapes lying in one plane both cover, within a tolerance.
        /// </summary>
        /// <param name="first">The face.</param>
        /// <param name="second">The polygon; it has to lie in the first shape's plane.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The pieces the two make, in the plane they share.</returns>
        /// <exception cref="ArgumentNullException">Thrown when either shape is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the two lie in different planes.</exception>
        public static GeoFace3[] Intersect(GeoFace3 first, GeoPolygon3 second, Tolerance tolerance)
        {
            GeoFace3 subject = NotNull(first, nameof(first));
            GeoFace3 other = AsFace(second, nameof(second));
            GeoCoordinateSystem3 frame = FlatFrame(subject, other, tolerance, nameof(second));

            return Combine(Combination.Intersect, frame, subject, other, tolerance);
        }

        /// <summary>
        /// Keeps what two shapes lying in one plane both cover.
        /// </summary>
        public static GeoFace3[] Intersect(GeoFace3 first, GeoFace3 second)
            => Intersect(first, second, Tolerance.Global);

        /// <summary>
        /// Keeps what two shapes lying in one plane both cover, within a tolerance.
        /// </summary>
        /// <param name="first">The face.</param>
        /// <param name="second">The face; it has to lie in the first shape's plane.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The pieces the two make, in the plane they share.</returns>
        /// <exception cref="ArgumentNullException">Thrown when either shape is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the two lie in different planes.</exception>
        public static GeoFace3[] Intersect(GeoFace3 first, GeoFace3 second, Tolerance tolerance)
        {
            GeoFace3 subject = NotNull(first, nameof(first));
            GeoFace3 other = NotNull(second, nameof(second));
            GeoCoordinateSystem3 frame = FlatFrame(subject, other, tolerance, nameof(second));

            return Combine(Combination.Intersect, frame, subject, other, tolerance);
        }

        /// <summary>
        /// Takes one shape out of another lying in the same plane.
        /// </summary>
        public static GeoFace3[] Subtract(GeoPolygon3 first, GeoPolygon3 tool)
            => Subtract(first, tool, Tolerance.Global);

        /// <summary>
        /// Takes one shape out of another lying in the same plane, within a tolerance.
        /// </summary>
        /// <param name="first">The polygon.</param>
        /// <param name="tool">The polygon; it has to lie in the first shape's plane.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The pieces the two make, in the plane they share.</returns>
        /// <exception cref="ArgumentNullException">Thrown when either shape is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the two lie in different planes.</exception>
        public static GeoFace3[] Subtract(GeoPolygon3 first, GeoPolygon3 tool, Tolerance tolerance)
        {
            GeoFace3 subject = AsFace(first, nameof(first));
            GeoFace3 other = AsFace(tool, nameof(tool));
            GeoCoordinateSystem3 frame = FlatFrame(subject, other, tolerance, nameof(tool));

            return Combine(Combination.Subtract, frame, subject, other, tolerance);
        }

        /// <summary>
        /// Takes one shape out of another lying in the same plane.
        /// </summary>
        public static GeoFace3[] Subtract(GeoPolygon3 first, GeoFace3 tool)
            => Subtract(first, tool, Tolerance.Global);

        /// <summary>
        /// Takes one shape out of another lying in the same plane, within a tolerance.
        /// </summary>
        /// <param name="first">The polygon.</param>
        /// <param name="tool">The face; it has to lie in the first shape's plane.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The pieces the two make, in the plane they share.</returns>
        /// <exception cref="ArgumentNullException">Thrown when either shape is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the two lie in different planes.</exception>
        public static GeoFace3[] Subtract(GeoPolygon3 first, GeoFace3 tool, Tolerance tolerance)
        {
            GeoFace3 subject = AsFace(first, nameof(first));
            GeoFace3 other = NotNull(tool, nameof(tool));
            GeoCoordinateSystem3 frame = FlatFrame(subject, other, tolerance, nameof(tool));

            return Combine(Combination.Subtract, frame, subject, other, tolerance);
        }

        /// <summary>
        /// Takes one shape out of another lying in the same plane.
        /// </summary>
        public static GeoFace3[] Subtract(GeoFace3 first, GeoPolygon3 tool)
            => Subtract(first, tool, Tolerance.Global);

        /// <summary>
        /// Takes one shape out of another lying in the same plane, within a tolerance.
        /// </summary>
        /// <param name="first">The face.</param>
        /// <param name="tool">The polygon; it has to lie in the first shape's plane.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The pieces the two make, in the plane they share.</returns>
        /// <exception cref="ArgumentNullException">Thrown when either shape is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the two lie in different planes.</exception>
        public static GeoFace3[] Subtract(GeoFace3 first, GeoPolygon3 tool, Tolerance tolerance)
        {
            GeoFace3 subject = NotNull(first, nameof(first));
            GeoFace3 other = AsFace(tool, nameof(tool));
            GeoCoordinateSystem3 frame = FlatFrame(subject, other, tolerance, nameof(tool));

            return Combine(Combination.Subtract, frame, subject, other, tolerance);
        }

        /// <summary>
        /// Takes one shape out of another lying in the same plane.
        /// </summary>
        public static GeoFace3[] Subtract(GeoFace3 first, GeoFace3 tool)
            => Subtract(first, tool, Tolerance.Global);

        /// <summary>
        /// Takes one shape out of another lying in the same plane, within a tolerance.
        /// </summary>
        /// <param name="first">The face.</param>
        /// <param name="tool">The face; it has to lie in the first shape's plane.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The pieces the two make, in the plane they share.</returns>
        /// <exception cref="ArgumentNullException">Thrown when either shape is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the two lie in different planes.</exception>
        public static GeoFace3[] Subtract(GeoFace3 first, GeoFace3 tool, Tolerance tolerance)
        {
            GeoFace3 subject = NotNull(first, nameof(first));
            GeoFace3 other = NotNull(tool, nameof(tool));
            GeoCoordinateSystem3 frame = FlatFrame(subject, other, tolerance, nameof(tool));

            return Combine(Combination.Subtract, frame, subject, other, tolerance);
        }

        /// <summary>
        /// Keeps what two shapes lying in one plane cover between them but do not share.
        /// </summary>
        public static GeoFace3[] Xor(GeoPolygon3 first, GeoPolygon3 second)
            => Xor(first, second, Tolerance.Global);

        /// <summary>
        /// Keeps what two shapes lying in one plane cover between them but do not share, within a tolerance.
        /// </summary>
        /// <param name="first">The polygon.</param>
        /// <param name="second">The polygon; it has to lie in the first shape's plane.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The pieces the two make, in the plane they share.</returns>
        /// <exception cref="ArgumentNullException">Thrown when either shape is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the two lie in different planes.</exception>
        public static GeoFace3[] Xor(GeoPolygon3 first, GeoPolygon3 second, Tolerance tolerance)
        {
            GeoFace3 subject = AsFace(first, nameof(first));
            GeoFace3 other = AsFace(second, nameof(second));
            GeoCoordinateSystem3 frame = FlatFrame(subject, other, tolerance, nameof(second));

            return Combine(Combination.Xor, frame, subject, other, tolerance);
        }

        /// <summary>
        /// Keeps what two shapes lying in one plane cover between them but do not share.
        /// </summary>
        public static GeoFace3[] Xor(GeoPolygon3 first, GeoFace3 second)
            => Xor(first, second, Tolerance.Global);

        /// <summary>
        /// Keeps what two shapes lying in one plane cover between them but do not share, within a tolerance.
        /// </summary>
        /// <param name="first">The polygon.</param>
        /// <param name="second">The face; it has to lie in the first shape's plane.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The pieces the two make, in the plane they share.</returns>
        /// <exception cref="ArgumentNullException">Thrown when either shape is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the two lie in different planes.</exception>
        public static GeoFace3[] Xor(GeoPolygon3 first, GeoFace3 second, Tolerance tolerance)
        {
            GeoFace3 subject = AsFace(first, nameof(first));
            GeoFace3 other = NotNull(second, nameof(second));
            GeoCoordinateSystem3 frame = FlatFrame(subject, other, tolerance, nameof(second));

            return Combine(Combination.Xor, frame, subject, other, tolerance);
        }

        /// <summary>
        /// Keeps what two shapes lying in one plane cover between them but do not share.
        /// </summary>
        public static GeoFace3[] Xor(GeoFace3 first, GeoPolygon3 second)
            => Xor(first, second, Tolerance.Global);

        /// <summary>
        /// Keeps what two shapes lying in one plane cover between them but do not share, within a tolerance.
        /// </summary>
        /// <param name="first">The face.</param>
        /// <param name="second">The polygon; it has to lie in the first shape's plane.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The pieces the two make, in the plane they share.</returns>
        /// <exception cref="ArgumentNullException">Thrown when either shape is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the two lie in different planes.</exception>
        public static GeoFace3[] Xor(GeoFace3 first, GeoPolygon3 second, Tolerance tolerance)
        {
            GeoFace3 subject = NotNull(first, nameof(first));
            GeoFace3 other = AsFace(second, nameof(second));
            GeoCoordinateSystem3 frame = FlatFrame(subject, other, tolerance, nameof(second));

            return Combine(Combination.Xor, frame, subject, other, tolerance);
        }

        /// <summary>
        /// Keeps what two shapes lying in one plane cover between them but do not share.
        /// </summary>
        public static GeoFace3[] Xor(GeoFace3 first, GeoFace3 second)
            => Xor(first, second, Tolerance.Global);

        /// <summary>
        /// Keeps what two shapes lying in one plane cover between them but do not share, within a tolerance.
        /// </summary>
        /// <param name="first">The face.</param>
        /// <param name="second">The face; it has to lie in the first shape's plane.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The pieces the two make, in the plane they share.</returns>
        /// <exception cref="ArgumentNullException">Thrown when either shape is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the two lie in different planes.</exception>
        public static GeoFace3[] Xor(GeoFace3 first, GeoFace3 second, Tolerance tolerance)
        {
            GeoFace3 subject = NotNull(first, nameof(first));
            GeoFace3 other = NotNull(second, nameof(second));
            GeoCoordinateSystem3 frame = FlatFrame(subject, other, tolerance, nameof(second));

            return Combine(Combination.Xor, frame, subject, other, tolerance);
        }

        #endregion

        #region Laying the two out in one frame

        /// <summary>
        /// Determines whether every one of some points lies on a plane, within the planar tolerance.
        /// </summary>
        /// <param name="plane">The plane.</param>
        /// <param name="points">The points fixing a shape: its corners, and a point along each curved edge.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when the whole shape lies in the plane, so that work in the plane's frame is exact.</returns>
        /// <remarks>
        /// A shape lies in a plane when all of it does. Planes parallel within the angle tolerance with one point
        /// of the shape on this one are not enough: the angle tolerance is a whole degree, so a shape turned half
        /// a degree about a line through that point passed — a metre long, its far end stood nine millimetres
        /// off the plane, and it was projected onto it without a word. Two faces crossing at a shallow angle read
        /// the same way as two lying back to back, which is how a boolean once took a piece of real boundary for
        /// the inside of the body.
        /// </remarks>
        internal static bool LiesIn(GeoPlane3 plane, IEnumerable<GeoPoint3> points, Tolerance tolerance)
        {
            foreach (GeoPoint3 point in points)
            {
                if (!Containment3.IsPointOn(plane, point, tolerance))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Determines whether the whole of a face, holes and all, lies in a plane, within the planar tolerance.
        /// </summary>
        internal static bool LiesIn(GeoPlane3 plane, GeoFace3 face, Tolerance tolerance)
        {
            if (!LiesIn(plane, face.Boundary.Vertices, tolerance))
            {
                return false;
            }

            foreach (GeoPolygon3 hole in face.Holes)
            {
                if (!LiesIn(plane, hole.Vertices, tolerance))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Takes one face from another lying back to back with it, in the plane of the first: the second is laid out in
        /// that plane whether or not all of it lies there.
        /// </summary>
        /// <remarks>
        /// For the gluing of a boolean (<see cref="CancelBackToBack"/>), which pairs two faces when either lies in the plane
        /// of the other. A small face a hair out of a long one's plane lies in it, but the long one's far end can stand
        /// millimetres off the small one's, and taking the long face from the small one was refused as lying in another
        /// plane, which threw out the whole boolean. Near the small face, where anything is taken from it, the two planes
        /// are one within the tolerance, so the long face laid out in the small one's plane takes away what it should, and
        /// what is left of the small face stays where it was.
        /// </remarks>
        internal static GeoFace3[] SubtractLaidOut(GeoFace3 face, GeoFace3 tool, Tolerance tolerance)
            => Combine(Combination.Subtract, PlanarMap.FrameOf(face), face, tool, tolerance);

        /// <summary>
        /// The frame to work the two shapes out in, refusing a second shape that lies in another plane.
        /// </summary>
        private static GeoCoordinateSystem3 FlatFrame(GeoFace3 first, GeoFace3 second, Tolerance tolerance, string name)
        {
            if (!LiesIn(first.GetPlane(), second, tolerance))
            {
                throw new ArgumentException(
                    "The two shapes lie in different planes, so there is no plane to work in. Bring them into one plane first.",
                    name);
            }

            return PlanarMap.FrameOf(first);
        }

        /// <summary>
        /// The four ways two flat shapes combine.
        /// </summary>
        private enum Combination
        {
            Union,
            Intersect,
            Subtract,
            Xor,
        }

        /// <summary>
        /// Combines two faces of one plane by laying them out in it, combining them there and lifting the answer
        /// back.
        /// </summary>
        /// <remarks>
        /// A face with no area left once laid out — a sliver whose corners come within the plane's point tolerance
        /// of one another — adds nothing and takes nothing away: sharing with it gives nothing, taking it out
        /// changes nothing, and joining it or keeping what the two do not share gives the other. Laid out as it
        /// was, it threw, and took with it the union or difference of two bodies whose gluing compared such a
        /// sliver with the face lying against it.
        /// </remarks>
        private static GeoFace3[] Combine(Combination combination, GeoCoordinateSystem3 frame, GeoFace3 first, GeoFace3 second, Tolerance tolerance)
        {
            GeoFace2 one = Flatten(frame, first);
            GeoFace2 other = Flatten(frame, second);

            if (one == null || other == null)
            {
                if (combination == Combination.Intersect || one == null && (other == null || combination == Combination.Subtract))
                {
                    return new GeoFace3[0];
                }

                return new[] { one == null ? second : first };
            }

            switch (combination)
            {
                case Combination.Union:
                    return Lift(frame, Boolean2.Union(one, other, tolerance), tolerance);
                case Combination.Intersect:
                    return Lift(frame, Boolean2.Intersect(one, other, tolerance), tolerance);
                case Combination.Subtract:
                    return Lift(frame, Boolean2.Subtract(one, other, tolerance), tolerance);
                default:
                    return Lift(frame, Boolean2.Xor(one, other, tolerance), tolerance);
            }
        }

        /// <summary>
        /// Lays a face out in a frame, or gives null when nothing with an area is left of it there.
        /// </summary>
        private static GeoFace2 Flatten(GeoCoordinateSystem3 frame, GeoFace3 face)
        {
            try
            {
                return PlanarMap.ProjectToFace2(frame, face);
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        /// <summary>
        /// Lifts the faces the plane gave back into the plane the two shapes share.
        /// </summary>
        /// <remarks>
        /// Each piece is built as thin as the point tolerance allows (<see cref="LoopAssembly.ForPieces"/>), since the
        /// faces it came from can be that thin: a body cut near a corner keeps slivers a few thousandths across, and
        /// the gluing of a union takes such faces from one another here. Built within the ordinary tolerance, a
        /// sliver of the answer threw, and took the whole union with it. A piece with nothing left of it once lifted,
        /// its corners within the point tolerance of one another or of a line, is dropped rather than thrown over:
        /// the plane draws finer than space does, and such a piece has no area to speak of.
        /// </remarks>
        private static GeoFace3[] Lift(GeoCoordinateSystem3 frame, GeoFace2[] flat, Tolerance tolerance)
        {
            Tolerance pieces = LoopAssembly.ForPieces(tolerance);
            var lifted = new List<GeoFace3>(flat.Length);

            foreach (GeoFace2 face in flat)
            {
                GeoPolygon3 boundary = LiftRing(frame, face.Boundary, pieces);

                if (boundary == null)
                {
                    continue;
                }

                var holes = new List<GeoPolygon3>(face.Holes.Count);

                foreach (GeoPolygon2 hole in face.Holes)
                {
                    GeoPolygon3 ring = LiftRing(frame, hole, pieces);

                    if (ring != null)
                    {
                        holes.Add(ring);
                    }
                }

                lifted.Add(new GeoFace3(boundary, holes, pieces));
            }

            return lifted.ToArray();
        }

        /// <summary>
        /// Lifts one ring into the plane, or gives null when nothing of it is left there.
        /// </summary>
        private static GeoPolygon3 LiftRing(GeoCoordinateSystem3 frame, GeoPolygon2 ring, Tolerance tolerance)
        {
            var corners = new List<GeoPoint3>(ring.Vertices.Count);

            foreach (GeoPoint2 corner in ring.Vertices)
            {
                corners.Add(PlanarMap.ToPoint3(frame, corner));
            }

            try
            {
                return new GeoPolygon3(corners, tolerance);
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        /// <summary>
        /// Reads a polygon as a face with no holes, complaining in the caller's own words when it is null.
        /// </summary>
        private static GeoFace3 AsFace(GeoPolygon3 polygon, string name)
        {
            if (polygon == null)
            {
                throw new ArgumentNullException(name);
            }

            return new GeoFace3(polygon);
        }

        private static GeoFace3 NotNull(GeoFace3 face, string name)
        {
            if (face == null)
            {
                throw new ArgumentNullException(name);
            }

            return face;
        }

        #endregion
    }
}
