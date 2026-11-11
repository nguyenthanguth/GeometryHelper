using System;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    /// <summary>
    /// Whether a triangle touches a body, and whether a flat region or a plane touches a box.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A region reaches a body's material when an edge of the region crosses the surface, an edge of the body crosses
    /// the region, or the region lies inside, which one of its corners then says. The body is read by its material,
    /// every opening cut in, so a triangle wholly inside an opening touches nothing.
    /// </para>
    /// <para>
    /// A box is read through its body: each answer about a box is the answer about <see cref="GeoObb3.ToSolid"/>, or
    /// about <c>ToObb().ToSolid()</c> for an axis-aligned one, as <see cref="CollidesWith(GeoPolygon3, GeoObb3, Tolerance)"/>
    /// has always read it. A box flat on some axis has no body and is read as the shape it has become: flat on one axis
    /// its rectangle, on two its segment, on all three its point. A box 100 by 100 by 0 is the square it spans, missed by
    /// a triangle lying 20 over it and touched by one standing across it.
    /// </para>
    /// </remarks>
    public static partial class Collision3
    {
        #region A triangle against a body

        /// <summary>
        /// Determines whether a triangle touches the material of a solid, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// A box 100 by 100 by 100 with a corner at the origin is touched by a triangle lying across it at half its
        /// height and by one wholly inside it, and not by one standing 10 clear of its top.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the solid is null.</exception>
        public static bool CollidesWith(GeoTriangle3 triangle, GeoSolid3 solid) =>
            CollidesWith(triangle, solid, Tolerance.Global);

        /// <summary>
        /// Determines whether a triangle touches the material of a solid, within a tolerance.
        /// </summary>
        /// <param name="triangle">The triangle.</param>
        /// <param name="solid">The solid, read by its material.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// true when the triangle comes within the point tolerance of the surface, crossing it included, or lies in the
        /// material; otherwise, false.
        /// </returns>
        /// <remarks>
        /// A triangle wholly inside the body crosses no face and touches it all the same, so where its first corner is
        /// is asked once nothing crosses. A box 100 by 100 by 100 with a corner at the origin is touched by a triangle
        /// lying across it at half its height and by one wholly inside it, and not by one standing 10 clear of its top. A
        /// triangle with no area is the segment it has collapsed to, and touches as its distance says. Whether a point is in the body is asked to within rounding, a billionth of a millimetre (1E-9, not nought),
        /// so that rounding in a body turned about in space cannot lose a crossing: a point within 1E-9 outside the
        /// body reads as in it, and a point 5E-10 outside a box is nought away from it. Further out than a million the
        /// billionth is 1E-15 of the largest coordinate of the body and the point, past the rounding of numbers that size.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the solid is null.</exception>
        public static bool CollidesWith(GeoTriangle3 triangle, GeoSolid3 solid, Tolerance tolerance)
        {
            if (solid == null)
            {
                throw new ArgumentNullException(nameof(solid));
            }

            GeoSolid3 material = Material3.Whole(solid, tolerance);

            return ReachesMaterial(
                Gap3.Region.Of(triangle),
                new[] { triangle.A, triangle.B, triangle.C },
                material,
                tolerance
            );
        }

        #endregion

        #region A flat region and a plane against a box

        /// <summary>
        /// Determines whether a triangle touches an oriented box, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// The answer of <see cref="CollidesWith(GeoTriangle3, GeoSolid3, Tolerance)"/> for <see cref="GeoObb3.ToSolid"/>:
        /// a box 100 by 100 by 100 is touched by a triangle wholly inside it and not by one 10 clear of its top. A box flat
        /// on some axis is read as its rectangle, its segment or its point: a box 100 by 100 by 0 is missed by a triangle
        /// lying 20 over it.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the box is null.</exception>
        public static bool CollidesWith(GeoTriangle3 triangle, GeoObb3 box) =>
            CollidesWith(triangle, box, Tolerance.Global);

        /// <summary>
        /// Determines whether a triangle touches an oriented box, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The answer of <see cref="CollidesWith(GeoTriangle3, GeoSolid3, Tolerance)"/> for <see cref="GeoObb3.ToSolid"/>:
        /// a box 100 by 100 by 100 is touched by a triangle wholly inside it and not by one 10 clear of its top. A box flat
        /// on some axis is read as its rectangle, its segment or its point: a box 100 by 100 by 0 is missed by a triangle
        /// lying 20 over it.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the box is null.</exception>
        public static bool CollidesWith(GeoTriangle3 triangle, GeoObb3 box, Tolerance tolerance) =>
            !Intersection3.FlatOf(box, out GeoPolygon3 rectangle, out GeoLine3 segment)
                ? CollidesWith(triangle, Intersection3.BodyOf(box), tolerance)
            : rectangle != null ? CollidesWith(triangle, rectangle, tolerance)
            : CollidesWith(segment, triangle, tolerance);

        /// <summary>
        /// Determines whether a triangle touches an axis-aligned box, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// The answer of <see cref="CollidesWith(GeoTriangle3, GeoSolid3, Tolerance)"/> for <c>ToObb().ToSolid()</c>: the
        /// box from the origin to (100, 100, 100) is touched by a triangle wholly inside it and not by one 10 clear of
        /// its top. A box flat on some axis is read as its rectangle, its segment or its point: the box from the origin to
        /// (100, 100, 0) is missed by a triangle lying 20 over it.
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown when the box is empty.</exception>
        public static bool CollidesWith(GeoTriangle3 triangle, GeoAabb3 box) =>
            CollidesWith(triangle, box, Tolerance.Global);

        /// <summary>
        /// Determines whether a triangle touches an axis-aligned box, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The answer of <see cref="CollidesWith(GeoTriangle3, GeoSolid3, Tolerance)"/> for <c>ToObb().ToSolid()</c>: the
        /// box from the origin to (100, 100, 100) is touched by a triangle wholly inside it and not by one 10 clear of
        /// its top. A box flat on some axis is read as its rectangle, its segment or its point: the box from the origin to
        /// (100, 100, 0) is missed by a triangle lying 20 over it.
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown when the box is empty.</exception>
        public static bool CollidesWith(GeoTriangle3 triangle, GeoAabb3 box, Tolerance tolerance) =>
            CollidesWith(triangle, box.ToObb(), tolerance);

        /// <summary>
        /// Determines whether a polygon touches an axis-aligned box, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// The answer of <see cref="CollidesWith(GeoPolygon3, GeoSolid3, Tolerance)"/> for <c>ToObb().ToSolid()</c>, as
        /// <see cref="CollidesWith(GeoPolygon3, GeoObb3, Tolerance)"/> reads an oriented box: the box from the origin to
        /// (100, 100, 100) is touched by the square 200 by 200 lying across it at half its height and not by one lying
        /// 10 above its top. A box flat on some axis is read as its rectangle, its segment or its point: the box from the
        /// origin to (100, 100, 0) is touched by a square standing upright across it.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the box is empty.</exception>
        public static bool CollidesWith(GeoPolygon3 polygon, GeoAabb3 box) =>
            CollidesWith(polygon, box, Tolerance.Global);

        /// <summary>
        /// Determines whether a polygon touches an axis-aligned box, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The answer of <see cref="CollidesWith(GeoPolygon3, GeoSolid3, Tolerance)"/> for <c>ToObb().ToSolid()</c>, as
        /// <see cref="CollidesWith(GeoPolygon3, GeoObb3, Tolerance)"/> reads an oriented box: the box from the origin to
        /// (100, 100, 100) is touched by the square 200 by 200 lying across it at half its height and not by one lying
        /// 10 above its top. A box flat on some axis is read as its rectangle, its segment or its point: the box from the
        /// origin to (100, 100, 0) is touched by a square standing upright across it.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the box is empty.</exception>
        public static bool CollidesWith(GeoPolygon3 polygon, GeoAabb3 box, Tolerance tolerance) =>
            CollidesWith(polygon, box.ToObb(), tolerance);

        /// <summary>
        /// Determines whether the material of a face touches an oriented box, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// The answer of <see cref="CollidesWith(GeoFace3, GeoSolid3, Tolerance)"/> for <see cref="GeoObb3.ToSolid"/>: a
        /// box 100 by 100 by 100 is touched by the square 200 by 200 lying across it at half its height. A box flat on some
        /// axis is read as its rectangle, its segment or its point: a box 100 by 100 by 0 is not touched by a ring round
        /// it in its own plane, 60 from its edges.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the face or the box is null.</exception>
        public static bool CollidesWith(GeoFace3 face, GeoObb3 box) => CollidesWith(face, box, Tolerance.Global);

        /// <summary>
        /// Determines whether the material of a face touches an oriented box, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The answer of <see cref="CollidesWith(GeoFace3, GeoSolid3, Tolerance)"/> for <see cref="GeoObb3.ToSolid"/>: a
        /// box 100 by 100 by 100 is touched by the square 200 by 200 lying across it at half its height. A box flat on some
        /// axis is read as its rectangle, its segment or its point: a box 100 by 100 by 0 is not touched by a ring round
        /// it in its own plane, 60 from its edges.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the face or the box is null.</exception>
        public static bool CollidesWith(GeoFace3 face, GeoObb3 box, Tolerance tolerance) =>
            !Intersection3.FlatOf(box, out GeoPolygon3 rectangle, out GeoLine3 segment)
                ? CollidesWith(face, Intersection3.BodyOf(box), tolerance)
            : rectangle != null ? CollidesWith(face, rectangle, tolerance)
            : CollidesWith(segment, face, tolerance);

        /// <summary>
        /// Determines whether the material of a face touches an axis-aligned box, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// The answer of <see cref="CollidesWith(GeoFace3, GeoSolid3, Tolerance)"/> for <c>ToObb().ToSolid()</c>: the box
        /// from the origin to (100, 100, 100) is touched by the square 200 by 200 lying across it at half its height. A box
        /// flat on some axis is read as its rectangle, its segment or its point: the box from the origin to (100, 100, 0)
        /// is not touched by a ring round it in its own plane, 60 from its edges.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the box is empty.</exception>
        public static bool CollidesWith(GeoFace3 face, GeoAabb3 box) => CollidesWith(face, box, Tolerance.Global);

        /// <summary>
        /// Determines whether the material of a face touches an axis-aligned box, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The answer of <see cref="CollidesWith(GeoFace3, GeoSolid3, Tolerance)"/> for <c>ToObb().ToSolid()</c>: the box
        /// from the origin to (100, 100, 100) is touched by the square 200 by 200 lying across it at half its height. A box
        /// flat on some axis is read as its rectangle, its segment or its point: the box from the origin to (100, 100, 0)
        /// is not touched by a ring round it in its own plane, 60 from its edges.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the box is empty.</exception>
        public static bool CollidesWith(GeoFace3 face, GeoAabb3 box, Tolerance tolerance) =>
            CollidesWith(face, box.ToObb(), tolerance);

        /// <summary>
        /// Determines whether an oriented box touches a plane, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// The answer of <see cref="CollidesWith(GeoPlane3, GeoSolid3, Tolerance)"/> for <see cref="GeoObb3.ToSolid"/>: a
        /// box 100 by 100 by 100 standing on the floor is touched by the plane z = 100 through its top and missed by the
        /// plane z = 101. A box flat on some axis is read as its rectangle, its segment or its point: a box 100 by 100 by 0
        /// lying on the floor is touched by the plane x = 30 and missed by the plane z = 20.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the box is null.</exception>
        public static bool CollidesWith(GeoPlane3 plane, GeoObb3 box) => CollidesWith(plane, box, Tolerance.Global);

        /// <summary>
        /// Determines whether an oriented box touches a plane, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The answer of <see cref="CollidesWith(GeoPlane3, GeoSolid3, Tolerance)"/> for <see cref="GeoObb3.ToSolid"/>: a
        /// box 100 by 100 by 100 standing on the floor is touched by the plane z = 100 through its top and missed by the
        /// plane z = 101. A box flat on some axis is read as its rectangle, its segment or its point: a box 100 by 100 by 0
        /// lying on the floor is touched by the plane x = 30 and missed by the plane z = 20.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the box is null.</exception>
        public static bool CollidesWith(GeoPlane3 plane, GeoObb3 box, Tolerance tolerance) =>
            !Intersection3.FlatOf(box, out GeoPolygon3 rectangle, out GeoLine3 segment)
                ? CollidesWith(plane, Intersection3.BodyOf(box), tolerance)
            : rectangle != null ? CollidesWith(plane, rectangle, tolerance)
            : CollidesWith(segment, plane, tolerance);

        /// <summary>
        /// Determines whether an axis-aligned box touches a plane, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// The answer of <see cref="CollidesWith(GeoPlane3, GeoSolid3, Tolerance)"/> for <c>ToObb().ToSolid()</c>: the box
        /// from the origin to (100, 100, 100) is touched by the plane z = 100 and missed by the plane z = 101. A box flat on
        /// some axis is read as its rectangle, its segment or its point: the box from the origin to (100, 100, 0) is
        /// touched by the plane x = 30 and missed by the plane z = 20.
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown when the box is empty.</exception>
        public static bool CollidesWith(GeoPlane3 plane, GeoAabb3 box) => CollidesWith(plane, box, Tolerance.Global);

        /// <summary>
        /// Determines whether an axis-aligned box touches a plane, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The answer of <see cref="CollidesWith(GeoPlane3, GeoSolid3, Tolerance)"/> for <c>ToObb().ToSolid()</c>: the box
        /// from the origin to (100, 100, 100) is touched by the plane z = 100 and missed by the plane z = 101. A box flat on
        /// some axis is read as its rectangle, its segment or its point: the box from the origin to (100, 100, 0) is
        /// touched by the plane x = 30 and missed by the plane z = 20.
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown when the box is empty.</exception>
        public static bool CollidesWith(GeoPlane3 plane, GeoAabb3 box, Tolerance tolerance) =>
            CollidesWith(plane, box.ToObb(), tolerance);

        #endregion
    }
}
