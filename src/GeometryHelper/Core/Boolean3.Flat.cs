using System;
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

            return Lift(frame, Boolean2.Union(
                PlanarMap.ProjectToFace2(frame, subject), PlanarMap.ProjectToFace2(frame, other), tolerance));
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

            return Lift(frame, Boolean2.Union(
                PlanarMap.ProjectToFace2(frame, subject), PlanarMap.ProjectToFace2(frame, other), tolerance));
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

            return Lift(frame, Boolean2.Union(
                PlanarMap.ProjectToFace2(frame, subject), PlanarMap.ProjectToFace2(frame, other), tolerance));
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

            return Lift(frame, Boolean2.Union(
                PlanarMap.ProjectToFace2(frame, subject), PlanarMap.ProjectToFace2(frame, other), tolerance));
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

            return Lift(frame, Boolean2.Intersect(
                PlanarMap.ProjectToFace2(frame, subject), PlanarMap.ProjectToFace2(frame, other), tolerance));
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

            return Lift(frame, Boolean2.Intersect(
                PlanarMap.ProjectToFace2(frame, subject), PlanarMap.ProjectToFace2(frame, other), tolerance));
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

            return Lift(frame, Boolean2.Intersect(
                PlanarMap.ProjectToFace2(frame, subject), PlanarMap.ProjectToFace2(frame, other), tolerance));
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

            return Lift(frame, Boolean2.Intersect(
                PlanarMap.ProjectToFace2(frame, subject), PlanarMap.ProjectToFace2(frame, other), tolerance));
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

            return Lift(frame, Boolean2.Subtract(
                PlanarMap.ProjectToFace2(frame, subject), PlanarMap.ProjectToFace2(frame, other), tolerance));
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

            return Lift(frame, Boolean2.Subtract(
                PlanarMap.ProjectToFace2(frame, subject), PlanarMap.ProjectToFace2(frame, other), tolerance));
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

            return Lift(frame, Boolean2.Subtract(
                PlanarMap.ProjectToFace2(frame, subject), PlanarMap.ProjectToFace2(frame, other), tolerance));
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

            return Lift(frame, Boolean2.Subtract(
                PlanarMap.ProjectToFace2(frame, subject), PlanarMap.ProjectToFace2(frame, other), tolerance));
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

            return Lift(frame, Boolean2.Xor(
                PlanarMap.ProjectToFace2(frame, subject), PlanarMap.ProjectToFace2(frame, other), tolerance));
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

            return Lift(frame, Boolean2.Xor(
                PlanarMap.ProjectToFace2(frame, subject), PlanarMap.ProjectToFace2(frame, other), tolerance));
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

            return Lift(frame, Boolean2.Xor(
                PlanarMap.ProjectToFace2(frame, subject), PlanarMap.ProjectToFace2(frame, other), tolerance));
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

            return Lift(frame, Boolean2.Xor(
                PlanarMap.ProjectToFace2(frame, subject), PlanarMap.ProjectToFace2(frame, other), tolerance));
        }

        #endregion

        #region Laying the two out in one frame

        /// <summary>
        /// Determines whether two planes are the same plane.
        /// </summary>
        /// <param name="plane">The plane to measure against.</param>
        /// <param name="other">The other plane.</param>
        /// <param name="onOther">A point known to lie on <paramref name="other"/>.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when the two are the same plane, so that work in one frame is exact.</returns>
        /// <remarks>
        /// Parallel is not enough: two parallel planes a metre apart share nothing at all — so a point of the
        /// other plane has to land on this one. It must be a point of <b>the other</b> plane: measuring one of
        /// this plane's own points against this plane is true whatever the other plane does, which makes every
        /// parallel plane pass. A caller with no point in hand passes <c>other.Origin</c>.
        /// </remarks>
        internal static bool SharesPlane(GeoPlane3 plane, GeoPlane3 other, GeoPoint3 onOther, Tolerance tolerance)
        {
            return Parallel3.IsParallel(plane, other, tolerance)
                && Containment3.IsPointOn(plane, onOther, tolerance);
        }

        /// <summary>
        /// The frame to work the two shapes out in, refusing a second shape that lies in another plane.
        /// </summary>
        private static GeoCoordinateSystem3 FlatFrame(GeoFace3 first, GeoFace3 second, Tolerance tolerance, string name)
        {
            if (!SharesPlane(first.GetPlane(), second.GetPlane(), second.Boundary.Vertices[0], tolerance))
            {
                throw new ArgumentException(
                    "The two shapes lie in different planes, so there is no plane to work in. Bring them into one plane first.",
                    name);
            }

            return PlanarMap.FrameOf(first);
        }

        /// <summary>
        /// Lifts the faces the plane gave back into the plane the two shapes share.
        /// </summary>
        private static GeoFace3[] Lift(GeoCoordinateSystem3 frame, GeoFace2[] flat)
        {
            var lifted = new GeoFace3[flat.Length];

            for (int i = 0; i < flat.Length; i++)
            {
                lifted[i] = PlanarMap.ToFace3(frame, flat[i]);
            }

            return lifted;
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
