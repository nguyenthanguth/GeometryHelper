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
    /// know whether they meet should not have to declare somewhere to put an answer it will not read.
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
        public static bool CollidesWith(GeoLine3 line, GeoTriangle3 triangle, Tolerance tolerance)
            => Intersection3.TryIntersectWith(line, triangle, out _, tolerance);

        /// <summary>
        /// Determines whether a segment touches a polygon.
        /// </summary>
        public static bool CollidesWith(GeoLine3 line, GeoPolygon3 polygon) => CollidesWith(line, polygon, Tolerance.Global);

        /// <summary>
        /// Determines whether a segment touches a polygon, within a tolerance.
        /// </summary>
        /// <exception cref="System.ArgumentNullException">Thrown when the polygon is null.</exception>
        public static bool CollidesWith(GeoLine3 line, GeoPolygon3 polygon, Tolerance tolerance)
            => Intersection3.TryIntersectWith(line, polygon, out _, tolerance);

        /// <summary>
        /// Determines whether a segment touches a face.
        /// </summary>
        public static bool CollidesWith(GeoLine3 line, GeoFace3 face) => CollidesWith(line, face, Tolerance.Global);

        /// <summary>
        /// Determines whether a segment touches a face, within a tolerance.
        /// </summary>
        /// <remarks>
        /// A segment passing over a hole of the face touches nothing, because the crossing test reads the
        /// holes as the absences they are.
        /// </remarks>
        /// <exception cref="System.ArgumentNullException">Thrown when the face is null.</exception>
        public static bool CollidesWith(GeoLine3 line, GeoFace3 face, Tolerance tolerance)
            => Intersection3.TryIntersectWith(line, face, out _, tolerance);

        #endregion

        #region Rays against flat shapes

        /// <summary>
        /// Determines whether a ray runs into a triangle.
        /// </summary>
        public static bool CollidesWith(GeoRay3 ray, GeoTriangle3 triangle) => CollidesWith(ray, triangle, Tolerance.Global);

        /// <summary>
        /// Determines whether a ray runs into a triangle, within a tolerance.
        /// </summary>
        public static bool CollidesWith(GeoRay3 ray, GeoTriangle3 triangle, Tolerance tolerance)
            => Intersection3.TryIntersectWith(ray, triangle, out _, tolerance);

        /// <summary>
        /// Determines whether a ray runs into a polygon.
        /// </summary>
        public static bool CollidesWith(GeoRay3 ray, GeoPolygon3 polygon) => CollidesWith(ray, polygon, Tolerance.Global);

        /// <summary>
        /// Determines whether a ray runs into a polygon, within a tolerance.
        /// </summary>
        /// <exception cref="System.ArgumentNullException">Thrown when the polygon is null.</exception>
        public static bool CollidesWith(GeoRay3 ray, GeoPolygon3 polygon, Tolerance tolerance)
            => Intersection3.TryIntersectWith(ray, polygon, out _, tolerance);

        /// <summary>
        /// Determines whether a ray runs into a face.
        /// </summary>
        public static bool CollidesWith(GeoRay3 ray, GeoFace3 face) => CollidesWith(ray, face, Tolerance.Global);

        /// <summary>
        /// Determines whether a ray runs into a face, within a tolerance.
        /// </summary>
        /// <exception cref="System.ArgumentNullException">Thrown when the face is null.</exception>
        public static bool CollidesWith(GeoRay3 ray, GeoFace3 face, Tolerance tolerance)
            => Intersection3.TryIntersectWith(ray, face, out _, tolerance);

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
