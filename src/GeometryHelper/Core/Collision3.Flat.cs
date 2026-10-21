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
    /// overlap in: they touch where the gap between them is no longer than the point tolerance, as
    /// <see cref="Distance3"/> measures it. The gap is exact, nothing widening either shape: a segment in the plane of
    /// the square 100 by 100 starting 0.0005 off its side is 0.0005 from it, from either end, and touches it at a
    /// tolerance of a thousandth; one starting 0.0012 off its corner, 0.00085 off in each direction, is 0.0012 from it
    /// and touches nothing; one hovering 0.0003 over it is 0.0003 from it, and one 0.0009 up and 0.0009 out from its
    /// side, 0.00127 away, touches nothing. A segment lying in the plane of a region and crossing it is nought from it
    /// and touches it, and so does one lying wholly inside it.
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
        /// They touch where <see cref="Distance3.DistanceTo(GeoLine3, GeoTriangle3, Tolerance)"/> is no longer than the
        /// point tolerance: the segment from (-10, 10, 0) to (110, 10, 0), lying in the plane of the triangle with corners
        /// (0, 0, 0), (100, 0, 0) and (0, 100, 0) and across it, touches it; the one from (60, 60, 0) to (90, 60, 0),
        /// beyond its long side, touches nothing; and the one from (50, -0.0005, 0) to (50, -50, 0), starting 0.0005 off its
        /// side y = 0, touches it, from either end. A triangle with no area is the segment it has collapsed to, and touches as
        /// its distance says: the one with corners (0, 0, 0), (100, 0, 0) and (50, 1E-8, 0) is nought from a segment through its
        /// middle and collides with it, where it collided with nothing.
        /// </remarks>
        public static bool CollidesWith(GeoLine3 line, GeoTriangle3 triangle, Tolerance tolerance)
            => Gap3.BoxGap(line.StartPoint, line.EndPoint, triangle.GetAabb()) <= Reach(tolerance)
                && Gap3.Segment(Gap3.Region.Of(triangle), line.StartPoint, line.EndPoint, true, tolerance, tolerance.EqualPoint) <= tolerance.EqualPoint;

        /// <summary>
        /// Determines whether a segment touches a polygon.
        /// </summary>
        public static bool CollidesWith(GeoLine3 line, GeoPolygon3 polygon) => CollidesWith(line, polygon, Tolerance.Global);

        /// <summary>
        /// Determines whether a segment touches a polygon, within a tolerance.
        /// </summary>
        /// <remarks>
        /// They touch where <see cref="Distance3.DistanceTo(GeoLine3, GeoPolygon3, Tolerance)"/> is no longer than the
        /// point tolerance, the polygon read filled: the segment from (-10, 10, 0) to (110, 10, 0) touches the square 100
        /// by 100 in the floor with a corner at the origin, and so does one lying wholly inside it, and one starting
        /// 0.0005 off its side in its plane; one hovering 0.0003 over it touches it, and one 0.0009 up and 0.0009 out from
        /// its side does not.
        /// </remarks>
        /// <exception cref="System.ArgumentNullException">Thrown when the polygon is null.</exception>
        public static bool CollidesWith(GeoLine3 line, GeoPolygon3 polygon, Tolerance tolerance)
        {
            if (polygon == null)
            {
                throw new System.ArgumentNullException(nameof(polygon));
            }

            return Gap3.BoxGap(line.StartPoint, line.EndPoint, polygon.GetAabb()) <= Reach(tolerance)
                && Gap3.Segment(Gap3.Region.Of(polygon), line.StartPoint, line.EndPoint, true, tolerance, tolerance.EqualPoint) <= tolerance.EqualPoint;
        }

        /// <summary>
        /// Determines whether a segment touches a face.
        /// </summary>
        public static bool CollidesWith(GeoLine3 line, GeoFace3 face) => CollidesWith(line, face, Tolerance.Global);

        /// <summary>
        /// Determines whether a segment touches a face, within a tolerance.
        /// </summary>
        /// <remarks>
        /// They touch where <see cref="Distance3.DistanceTo(GeoLine3, GeoFace3, Tolerance)"/> is no longer than the point
        /// tolerance, the face read by its material: a segment passing over a hole of the face touches nothing, because the
        /// holes are the absences they are. Over the square 100 by 100 in the floor with a hole 40 by 40 in its middle, the
        /// segment from (-10, 10, 0) to (110, 10, 0) touches it, and the one from (40, 50, 0) to (60, 50, 0), lying in the
        /// hole and reaching its rim, touches it there; the one from (41, 50, 0) to (59, 50, 0), a thousandth short of the
        /// rim either side, touches nothing.
        /// </remarks>
        /// <exception cref="System.ArgumentNullException">Thrown when the face is null.</exception>
        public static bool CollidesWith(GeoLine3 line, GeoFace3 face, Tolerance tolerance)
        {
            if (face == null)
            {
                throw new System.ArgumentNullException(nameof(face));
            }

            return Gap3.BoxGap(line.StartPoint, line.EndPoint, face.GetAabb()) <= Reach(tolerance)
                && Gap3.Segment(Gap3.Region.Of(face), line.StartPoint, line.EndPoint, true, tolerance, tolerance.EqualPoint) <= tolerance.EqualPoint;
        }

        /// <summary>
        /// Gets how far apart two boxes may stand and their shapes still come within the point tolerance: the tolerance
        /// and a millionth over, so that the rounding of a box's gap never refuses a pair standing exactly the tolerance
        /// apart, which the gap itself takes, and the planar tolerance more, for a polygon or a face is read flat in its plane and
        /// its corners may stand that far off it, outside the box they make.
        /// </summary>
        internal static double Reach(Tolerance tolerance) => tolerance.EqualPoint * (1.0 + 1E-6) + tolerance.EqualPlanar;

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
        /// They touch where <see cref="Distance3.DistanceTo(GeoRay3, GeoTriangle3, Tolerance)"/> is no longer than the point
        /// tolerance. A ray lying in the plane of the triangle crosses it nowhere, and is read as a segment lying in the
        /// plane as far as the corner farthest from its origin, since past that it only draws away: the ray from
        /// (-10, 10, 0) along X runs into the triangle with corners (0, 0, 0), (100, 0, 0) and (0, 100, 0), and the
        /// one pointing along -X from there misses it. A triangle with no area is the segment it has collapsed to, and touches as
        /// its distance says.
        /// </remarks>
        public static bool CollidesWith(GeoRay3 ray, GeoTriangle3 triangle, Tolerance tolerance)
            => Gap3.Ray(Gap3.Region.Of(triangle), ray, tolerance.EqualPoint) <= tolerance.EqualPoint;

        /// <summary>
        /// Determines whether a ray runs into a polygon.
        /// </summary>
        public static bool CollidesWith(GeoRay3 ray, GeoPolygon3 polygon) => CollidesWith(ray, polygon, Tolerance.Global);

        /// <summary>
        /// Determines whether a ray runs into a polygon, within a tolerance.
        /// </summary>
        /// <remarks>
        /// They touch where <see cref="Distance3.DistanceTo(GeoRay3, GeoPolygon3, Tolerance)"/> is no longer than the point
        /// tolerance. A ray lying in the plane of the polygon crosses it nowhere, and is read as a segment lying in the plane
        /// as far as the corner farthest from its origin, since past that it only draws away: the ray from
        /// (-10, 10, 0) along X runs into the square 100 by 100 in the floor with a corner at the origin, one starting
        /// inside the square touches it whichever way along the floor it points, and one starting 0.0005 off its side and
        /// pointing away touches it.
        /// </remarks>
        /// <exception cref="System.ArgumentNullException">Thrown when the polygon is null.</exception>
        public static bool CollidesWith(GeoRay3 ray, GeoPolygon3 polygon, Tolerance tolerance)
        {
            if (polygon == null)
            {
                throw new System.ArgumentNullException(nameof(polygon));
            }

            return Gap3.Ray(Gap3.Region.Of(polygon), ray, tolerance.EqualPoint) <= tolerance.EqualPoint;
        }

        /// <summary>
        /// Determines whether a ray runs into a face.
        /// </summary>
        public static bool CollidesWith(GeoRay3 ray, GeoFace3 face) => CollidesWith(ray, face, Tolerance.Global);

        /// <summary>
        /// Determines whether a ray runs into a face, within a tolerance.
        /// </summary>
        /// <remarks>
        /// They touch where <see cref="Distance3.DistanceTo(GeoRay3, GeoFace3, Tolerance)"/> is no longer than the point
        /// tolerance. A ray lying in the plane of the face crosses it nowhere, and is read as a segment lying in the plane
        /// as far as the corner of the outline farthest from its origin, since past that it only draws away: the ray
        /// from (-10, 10, 0) along X runs into the square 100 by 100 in the floor, and the one from (50, 50, 0), in
        /// the middle of a hole 20 by 20 cut out of it, runs into the rim of the hole whichever way along the
        /// floor it points.
        /// </remarks>
        /// <exception cref="System.ArgumentNullException">Thrown when the face is null.</exception>
        public static bool CollidesWith(GeoRay3 ray, GeoFace3 face, Tolerance tolerance)
        {
            if (face == null)
            {
                throw new System.ArgumentNullException(nameof(face));
            }

            return Gap3.Ray(Gap3.Region.Of(face), ray, tolerance.EqualPoint) <= tolerance.EqualPoint;
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
            => !box.IsEmpty && CollidesWith(ray, box.ToObb(), tolerance);

        /// <summary>
        /// Determines whether a ray starts inside an oriented box or runs into it.
        /// </summary>
        public static bool CollidesWith(GeoRay3 ray, GeoObb3 box) => CollidesWith(ray, box, Tolerance.Global);

        /// <summary>
        /// Determines whether a ray starts inside an oriented box or runs into it, within a tolerance.
        /// </summary>
        public static bool CollidesWith(GeoRay3 ray, GeoObb3 box, Tolerance tolerance)
            => new Gap3.Block(box ?? throw new System.ArgumentNullException(nameof(box))).Ray(ray, tolerance.EqualPoint) <= tolerance.EqualPoint;

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
