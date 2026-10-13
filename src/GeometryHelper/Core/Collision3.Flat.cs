using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    /// <summary>
    /// Whether an open shape reaches a flat one, whether a ray reaches a box, and whether an axis-aligned
    /// box reaches a body.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A segment, a ray and a flat region have no thickness between them, so there is nothing for them to
    /// overlap in: they touch exactly where they cross. Each of these is therefore the crossing test with
    /// the point thrown away, which is worth having under its own name because a caller who only wants to
    /// know whether they meet should not have to declare somewhere to put an answer it will not read. A
    /// segment or a ray lying in the plane of a triangle, a polygon or a face is the exception: it crosses
    /// the shape nowhere, so it is laid out in the plane and touches where it reaches the shape there.
    /// </para>
    /// <para>
    /// A ray against a box is the one that needs a second question. A box is bounded, so a ray that starts
    /// inside it always leaves and does cross the surface — but a ray starting on the inside is the case a
    /// crossing count is least sure of, so where it starts is asked first, as it is for a ray against a
    /// solid.
    /// </para>
    /// </remarks>
    public static partial class Collision3
    {
        #region Segments against flat shapes

        /// <summary>
        /// Determines whether a segment touches a plane.
        /// </summary>
        public static bool CollidesWith(GeoLine3 line, GeoPlane3 plane) => CollidesWith(line, plane, Tolerance.Global);

        /// <summary>
        /// Determines whether a segment touches a plane, within a tolerance.
        /// </summary>
        /// <remarks>
        /// A plane is endless, so this is not a crossing test: a segment lying in the plane touches it along
        /// its whole length and crosses it nowhere. The two ends decide it — either one on the plane, or one
        /// each side of it.
        /// </remarks>
        public static bool CollidesWith(GeoLine3 line, GeoPlane3 plane, Tolerance tolerance)
        {
            double atStart = plane.SignedDistanceTo(line.StartPoint);
            double atEnd = plane.SignedDistanceTo(line.EndPoint);

            if (System.Math.Abs(atStart) <= tolerance.EqualPlanar || System.Math.Abs(atEnd) <= tolerance.EqualPlanar)
            {
                return true;
            }

            return atStart < 0.0 != atEnd < 0.0;
        }

        /// <summary>
        /// Determines whether a segment touches a triangle.
        /// </summary>
        public static bool CollidesWith(GeoLine3 line, GeoTriangle3 triangle) => CollidesWith(line, triangle, Tolerance.Global);

        /// <summary>
        /// Determines whether a segment touches a triangle, within a tolerance.
        /// </summary>
        /// <remarks>
        /// A segment lying in the plane of the triangle, both ends within the planar tolerance of it, crosses it
        /// nowhere, so it is laid out in that plane and touches where it reaches the triangle there: the segment
        /// from (-10, 10, 0) to (110, 10, 0) touches the triangle with corners (0, 0, 0), (100, 0, 0) and
        /// (0, 100, 0), and the one from (60, 60, 0) to (90, 60, 0), beyond its long side, touches nothing. A
        /// triangle with no area touches nothing.
        /// </remarks>
        public static bool CollidesWith(GeoLine3 line, GeoTriangle3 triangle, Tolerance tolerance)
            => Intersection3.TryIntersectWith(line, triangle, out _, tolerance)
                || ReachesInPlane(line.StartPoint, line.EndPoint, triangle, tolerance);

        /// <summary>
        /// Determines whether a segment touches a polygon.
        /// </summary>
        public static bool CollidesWith(GeoLine3 line, GeoPolygon3 polygon) => CollidesWith(line, polygon, Tolerance.Global);

        /// <summary>
        /// Determines whether a segment touches a polygon, within a tolerance.
        /// </summary>
        /// <remarks>
        /// A segment lying in the plane of the polygon, both ends within the planar tolerance of it, crosses it
        /// nowhere, so it is laid out in that plane and touches where it reaches the polygon there, read filled:
        /// the segment from (-10, 10, 0) to (110, 10, 0) touches the square 100 by 100 in the floor with a corner
        /// at the origin, and so does one lying wholly inside it. A polygon too thin to have an area under the
        /// tolerance has nothing to lay out, and a segment lying in its plane touches it nowhere.
        /// </remarks>
        /// <exception cref="System.ArgumentNullException">Thrown when the polygon is null.</exception>
        public static bool CollidesWith(GeoLine3 line, GeoPolygon3 polygon, Tolerance tolerance)
            => Intersection3.TryIntersectWith(line, polygon, out _, tolerance)
                || ReachesInPlane(line.StartPoint, line.EndPoint, polygon, tolerance);

        /// <summary>
        /// Determines whether a segment touches a face.
        /// </summary>
        public static bool CollidesWith(GeoLine3 line, GeoFace3 face) => CollidesWith(line, face, Tolerance.Global);

        /// <summary>
        /// Determines whether a segment touches a face, within a tolerance.
        /// </summary>
        /// <remarks>
        /// A segment passing over a hole of the face touches nothing, because the crossing test reads the
        /// holes as the absences they are. A segment lying in the plane of the face, both ends within the
        /// planar tolerance of it, crosses it nowhere, so it is laid out in that plane and touches where it
        /// reaches the material there: over the square 100 by 100 in the floor with a hole 40 by 40 in its
        /// middle, the segment from (-10, 10, 0) to (110, 10, 0) touches it, and the one from (40, 50, 0) to
        /// (60, 50, 0), lying in the hole, touches nothing. A face too thin to have an area under the
        /// tolerance has nothing to lay out, and a segment lying in its plane touches it nowhere.
        /// </remarks>
        /// <exception cref="System.ArgumentNullException">Thrown when the face is null.</exception>
        public static bool CollidesWith(GeoLine3 line, GeoFace3 face, Tolerance tolerance)
            => Intersection3.TryIntersectWith(line, face, out _, tolerance)
                || ReachesInPlane(line.StartPoint, line.EndPoint, face, tolerance);

        #endregion

        #region Rays against flat shapes

        /// <summary>
        /// Determines whether a ray runs into a triangle.
        /// </summary>
        public static bool CollidesWith(GeoRay3 ray, GeoTriangle3 triangle) => CollidesWith(ray, triangle, Tolerance.Global);

        /// <summary>
        /// Determines whether a ray runs into a triangle, within a tolerance.
        /// </summary>
        /// <remarks>
        /// A ray lying in the plane of the triangle crosses it nowhere. It is read as a segment lying in the plane
        /// as far as the corner farthest from its origin, since past that it only draws away: the ray from
        /// (-10, 10, 0) along X runs into the triangle with corners (0, 0, 0), (100, 0, 0) and (0, 100, 0), and the
        /// one pointing along -X from there misses it.
        /// </remarks>
        public static bool CollidesWith(GeoRay3 ray, GeoTriangle3 triangle, Tolerance tolerance)
            => Intersection3.TryIntersectWith(ray, triangle, out _, tolerance)
                || (!triangle.IsDegenerate(tolerance) && StartsIn(triangle.GetPlane(), ray, tolerance)
                    && ReachesInPlane(ray.Origin, FarEnough(ray, new[] { triangle.A, triangle.B, triangle.C }), triangle, tolerance));

        /// <summary>
        /// Determines whether a ray runs into a polygon.
        /// </summary>
        public static bool CollidesWith(GeoRay3 ray, GeoPolygon3 polygon) => CollidesWith(ray, polygon, Tolerance.Global);

        /// <summary>
        /// Determines whether a ray runs into a polygon, within a tolerance.
        /// </summary>
        /// <remarks>
        /// A ray lying in the plane of the polygon crosses it nowhere. It is read as a segment lying in the plane
        /// as far as the corner farthest from its origin, since past that it only draws away: the ray from
        /// (-10, 10, 0) along X runs into the square 100 by 100 in the floor with a corner at the origin, and one
        /// starting inside the square touches it whichever way along the floor it points.
        /// </remarks>
        /// <exception cref="System.ArgumentNullException">Thrown when the polygon is null.</exception>
        public static bool CollidesWith(GeoRay3 ray, GeoPolygon3 polygon, Tolerance tolerance)
            => Intersection3.TryIntersectWith(ray, polygon, out _, tolerance)
                || (StartsIn(polygon.GetPlane(), ray, tolerance)
                    && ReachesInPlane(ray.Origin, FarEnough(ray, polygon.Vertices), polygon, tolerance));

        /// <summary>
        /// Determines whether a ray runs into a face.
        /// </summary>
        public static bool CollidesWith(GeoRay3 ray, GeoFace3 face) => CollidesWith(ray, face, Tolerance.Global);

        /// <summary>
        /// Determines whether a ray runs into a face, within a tolerance.
        /// </summary>
        /// <remarks>
        /// A ray lying in the plane of the face crosses it nowhere. It is read as a segment lying in the plane as
        /// far as the corner of the outline farthest from its origin, since past that it only draws away: the ray
        /// from (-10, 10, 0) along X runs into the square 100 by 100 in the floor, and the one from (50, 50, 0), in
        /// the middle of a hole 40 by 40 cut out of it, runs into the rim of the hole whichever way along the
        /// floor it points.
        /// </remarks>
        /// <exception cref="System.ArgumentNullException">Thrown when the face is null.</exception>
        public static bool CollidesWith(GeoRay3 ray, GeoFace3 face, Tolerance tolerance)
            => Intersection3.TryIntersectWith(ray, face, out _, tolerance)
                || (StartsIn(face.GetPlane(), ray, tolerance)
                    && ReachesInPlane(ray.Origin, FarEnough(ray, face.Boundary.Vertices), face, tolerance));

        #endregion

        #region Lying in the plane of a flat shape

        /// <summary>
        /// Gets the point of a ray as far from its origin as the farthest of some corners, past which it only
        /// draws away from every point among them.
        /// </summary>
        private static GeoPoint3 FarEnough(GeoRay3 ray, System.Collections.Generic.IReadOnlyList<GeoPoint3> corners)
        {
            double reach = 0.0;

            foreach (GeoPoint3 corner in corners)
            {
                reach = System.Math.Max(reach, ray.Origin.DistanceTo(corner));
            }

            return Parametrization3.GetPointAtDistance(ray, reach);
        }

        /// <summary>
        /// Determines whether a ray starts within the planar tolerance of a plane, which it must to lie in it; asked
        /// first, so that a ray starting off the plane is not walked out to the corners.
        /// </summary>
        private static bool StartsIn(GeoPlane3 plane, GeoRay3 ray, Tolerance tolerance)
            => System.Math.Abs(plane.SignedDistanceTo(ray.Origin)) <= tolerance.EqualPlanar;

        /// <summary>
        /// Determines whether both ends of a segment lie within the planar tolerance of a plane.
        /// </summary>
        private static bool LiesIn(GeoPlane3 plane, GeoPoint3 from, GeoPoint3 to, Tolerance tolerance)
            => System.Math.Abs(plane.SignedDistanceTo(from)) <= tolerance.EqualPlanar
                && System.Math.Abs(plane.SignedDistanceTo(to)) <= tolerance.EqualPlanar;

        /// <summary>
        /// Lays a segment out in a frame, dropping each end's height over its plane.
        /// </summary>
        private static GeoLine2 LayOut(GeoCoordinateSystem3 frame, GeoPoint3 from, GeoPoint3 to)
            => new GeoLine2(PlanarMap.ProjectToPoint2(frame, from), PlanarMap.ProjectToPoint2(frame, to));

        /// <summary>
        /// Determines whether a segment lying in the plane of a triangle reaches it when the two are laid out in
        /// that plane, as two regions in one plane are.
        /// </summary>
        private static bool ReachesInPlane(GeoPoint3 from, GeoPoint3 to, GeoTriangle3 triangle, Tolerance tolerance)
        {
            if (triangle.IsDegenerate(tolerance) || !LiesIn(triangle.GetPlane(), from, to, tolerance))
            {
                return false;
            }

            GeoCoordinateSystem3 frame = Intersection3.Flat.Of(triangle).Frame;

            return Triangle2.CrossesOrStartsIn(PlanarMap.ProjectToTriangle2(frame, triangle), LayOut(frame, from, to), tolerance);
        }

        /// <summary>
        /// Determines whether a segment lying in the plane of a polygon reaches it when the two are laid out in
        /// that plane.
        /// </summary>
        private static bool ReachesInPlane(GeoPoint3 from, GeoPoint3 to, GeoPolygon3 polygon, Tolerance tolerance)
        {
            if (!LiesIn(polygon.GetPlane(), from, to, tolerance))
            {
                return false;
            }

            GeoCoordinateSystem3 frame = PlanarMap.FrameOf(polygon);

            return PlanarMap.TryProjectToPolygon2(frame, polygon, tolerance, out GeoPolygon2 flat)
                && Collision2.CrossesOrStartsIn(flat, LayOut(frame, from, to), tolerance, true);
        }

        /// <summary>
        /// Determines whether a segment lying in the plane of a face reaches the material when the two are laid
        /// out in that plane.
        /// </summary>
        private static bool ReachesInPlane(GeoPoint3 from, GeoPoint3 to, GeoFace3 face, Tolerance tolerance)
        {
            if (!LiesIn(face.GetPlane(), from, to, tolerance))
            {
                return false;
            }

            GeoCoordinateSystem3 frame = PlanarMap.FrameOf(face);

            return PlanarMap.TryProjectToFace2(frame, face, tolerance, out GeoFace2 flat)
                && Face2.CrossesOrLiesOn(flat, LayOut(frame, from, to), tolerance, true);
        }

        #endregion

        #region Rays against boxes

        /// <summary>
        /// Determines whether a ray starts inside an axis-aligned box or runs into it.
        /// </summary>
        public static bool CollidesWith(GeoRay3 ray, GeoAabb3 box) => CollidesWith(ray, box, Tolerance.Global);

        /// <summary>
        /// Determines whether a ray starts inside an axis-aligned box or runs into it, within a tolerance.
        /// </summary>
        public static bool CollidesWith(GeoRay3 ray, GeoAabb3 box, Tolerance tolerance)
            => box.Contains(ray.Origin, tolerance) || Intersection3.GetIntersections(ray, box, tolerance).Length > 0;

        /// <summary>
        /// Determines whether a ray starts inside an oriented box or runs into it.
        /// </summary>
        public static bool CollidesWith(GeoRay3 ray, GeoObb3 box) => CollidesWith(ray, box, Tolerance.Global);

        /// <summary>
        /// Determines whether a ray starts inside an oriented box or runs into it, within a tolerance.
        /// </summary>
        public static bool CollidesWith(GeoRay3 ray, GeoObb3 box, Tolerance tolerance)
            => box.Contains(ray.Origin, tolerance) || Intersection3.GetIntersections(ray, box, tolerance).Length > 0;

        #endregion

        #region An axis-aligned box against a body

        /// <summary>
        /// Determines whether an axis-aligned box touches or overlaps a solid.
        /// </summary>
        public static bool CollidesWith(GeoAabb3 box, GeoSolid3 solid) => CollidesWith(box, solid, Tolerance.Global);

        /// <summary>
        /// Determines whether an axis-aligned box touches or overlaps a solid, within a tolerance.
        /// </summary>
        /// <remarks>
        /// An axis-aligned box is an oriented box whose axes happen to be the world's, so this is the
        /// oriented answer and not an approximation of it.
        /// </remarks>
        /// <exception cref="System.ArgumentNullException">Thrown when the solid is null.</exception>
        public static bool CollidesWith(GeoAabb3 box, GeoSolid3 solid, Tolerance tolerance)
            => CollidesWith(box.ToObb(), solid, tolerance);

        #endregion
    }
}
