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
    /// has always read it.
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
        public static bool CollidesWith(GeoTriangle3 triangle, GeoSolid3 solid) => CollidesWith(triangle, solid, Tolerance.Global);

        /// <summary>
        /// Determines whether a triangle touches the material of a solid, within a tolerance.
        /// </summary>
        /// <param name="triangle">The triangle.</param>
        /// <param name="solid">The solid, read by its material.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// true when an edge of the triangle crosses the surface, an edge of the body crosses the triangle, or the
        /// triangle lies in the material; otherwise, false.
        /// </returns>
        /// <remarks>
        /// A triangle wholly inside the body crosses no face and touches it all the same, so where its first corner is
        /// is asked once nothing crosses. A box 100 by 100 by 100 with a corner at the origin is touched by a triangle
        /// lying across it at half its height and by one wholly inside it, and not by one standing 10 clear of its top.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the solid is null.</exception>
        public static bool CollidesWith(GeoTriangle3 triangle, GeoSolid3 solid, Tolerance tolerance)
        {
            if (solid == null)
            {
                throw new ArgumentNullException(nameof(solid));
            }

            GeoSolid3 material = Material3.Whole(solid, tolerance);

            for (int i = 0; i < 3; i++)
            {
                if (Intersection3.SurfaceCrossings(triangle.GetEdgeAt(i), material, tolerance).Length > 0)
                {
                    return true;
                }
            }

            foreach (GeoFace3 face in material.Faces)
            {
                foreach (GeoLine3 edge in Intersection3.EdgesOf(face))
                {
                    if (Intersection3.TryCrossEdge(edge, triangle, out _, tolerance))
                    {
                        return true;
                    }
                }
            }

            // Every corner, as the distance asks them (Distance3.DistanceTo(GeoSolid3, GeoTriangle3, Tolerance)): one
            // standing within the tolerance outside the surface, the others clear of it, is nought from the body.
            return Containment3.Contains(material, triangle.A, tolerance)
                || Containment3.Contains(material, triangle.B, tolerance)
                || Containment3.Contains(material, triangle.C, tolerance);
        }

        #endregion

        #region A flat region and a plane against a box

        /// <summary>
        /// Determines whether a triangle touches an oriented box, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// The answer of <see cref="CollidesWith(GeoTriangle3, GeoSolid3, Tolerance)"/> for <see cref="GeoObb3.ToSolid"/>:
        /// a box 100 by 100 by 100 is touched by a triangle wholly inside it and not by one 10 clear of its top.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the box is null.</exception>
        public static bool CollidesWith(GeoTriangle3 triangle, GeoObb3 box) => CollidesWith(triangle, box, Tolerance.Global);

        /// <summary>
        /// Determines whether a triangle touches an oriented box, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The answer of <see cref="CollidesWith(GeoTriangle3, GeoSolid3, Tolerance)"/> for <see cref="GeoObb3.ToSolid"/>:
        /// a box 100 by 100 by 100 is touched by a triangle wholly inside it and not by one 10 clear of its top.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the box is null.</exception>
        public static bool CollidesWith(GeoTriangle3 triangle, GeoObb3 box, Tolerance tolerance)
            => CollidesWith(triangle, Intersection3.BodyOf(box), tolerance);

        /// <summary>
        /// Determines whether a triangle touches an axis-aligned box, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// The answer of <see cref="CollidesWith(GeoTriangle3, GeoSolid3, Tolerance)"/> for <c>ToObb().ToSolid()</c>: the
        /// box from the origin to (100, 100, 100) is touched by a triangle wholly inside it and not by one 10 clear of
        /// its top.
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown when the box is empty.</exception>
        public static bool CollidesWith(GeoTriangle3 triangle, GeoAabb3 box) => CollidesWith(triangle, box, Tolerance.Global);

        /// <summary>
        /// Determines whether a triangle touches an axis-aligned box, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The answer of <see cref="CollidesWith(GeoTriangle3, GeoSolid3, Tolerance)"/> for <c>ToObb().ToSolid()</c>: the
        /// box from the origin to (100, 100, 100) is touched by a triangle wholly inside it and not by one 10 clear of
        /// its top.
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown when the box is empty.</exception>
        public static bool CollidesWith(GeoTriangle3 triangle, GeoAabb3 box, Tolerance tolerance)
            => CollidesWith(triangle, box.ToObb().ToSolid(), tolerance);

        /// <summary>
        /// Determines whether a polygon touches an axis-aligned box, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// The answer of <see cref="CollidesWith(GeoPolygon3, GeoSolid3, Tolerance)"/> for <c>ToObb().ToSolid()</c>, as
        /// <see cref="CollidesWith(GeoPolygon3, GeoObb3, Tolerance)"/> reads an oriented box: the box from the origin to
        /// (100, 100, 100) is touched by the square 200 by 200 lying across it at half its height and not by one lying
        /// 10 above its top.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the box is empty.</exception>
        public static bool CollidesWith(GeoPolygon3 polygon, GeoAabb3 box) => CollidesWith(polygon, box, Tolerance.Global);

        /// <summary>
        /// Determines whether a polygon touches an axis-aligned box, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The answer of <see cref="CollidesWith(GeoPolygon3, GeoSolid3, Tolerance)"/> for <c>ToObb().ToSolid()</c>, as
        /// <see cref="CollidesWith(GeoPolygon3, GeoObb3, Tolerance)"/> reads an oriented box: the box from the origin to
        /// (100, 100, 100) is touched by the square 200 by 200 lying across it at half its height and not by one lying
        /// 10 above its top.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the box is empty.</exception>
        public static bool CollidesWith(GeoPolygon3 polygon, GeoAabb3 box, Tolerance tolerance)
            => CollidesWith(polygon, box.ToObb().ToSolid(), tolerance);

        /// <summary>
        /// Determines whether the material of a face touches an oriented box, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// The answer of <see cref="CollidesWith(GeoFace3, GeoSolid3, Tolerance)"/> for <see cref="GeoObb3.ToSolid"/>: a
        /// box 100 by 100 by 100 is touched by the square 200 by 200 lying across it at half its height.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the face or the box is null.</exception>
        public static bool CollidesWith(GeoFace3 face, GeoObb3 box) => CollidesWith(face, box, Tolerance.Global);

        /// <summary>
        /// Determines whether the material of a face touches an oriented box, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The answer of <see cref="CollidesWith(GeoFace3, GeoSolid3, Tolerance)"/> for <see cref="GeoObb3.ToSolid"/>: a
        /// box 100 by 100 by 100 is touched by the square 200 by 200 lying across it at half its height.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the face or the box is null.</exception>
        public static bool CollidesWith(GeoFace3 face, GeoObb3 box, Tolerance tolerance)
            => CollidesWith(face, Intersection3.BodyOf(box), tolerance);

        /// <summary>
        /// Determines whether the material of a face touches an axis-aligned box, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// The answer of <see cref="CollidesWith(GeoFace3, GeoSolid3, Tolerance)"/> for <c>ToObb().ToSolid()</c>: the box
        /// from the origin to (100, 100, 100) is touched by the square 200 by 200 lying across it at half its height.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the box is empty.</exception>
        public static bool CollidesWith(GeoFace3 face, GeoAabb3 box) => CollidesWith(face, box, Tolerance.Global);

        /// <summary>
        /// Determines whether the material of a face touches an axis-aligned box, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The answer of <see cref="CollidesWith(GeoFace3, GeoSolid3, Tolerance)"/> for <c>ToObb().ToSolid()</c>: the box
        /// from the origin to (100, 100, 100) is touched by the square 200 by 200 lying across it at half its height.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the box is empty.</exception>
        public static bool CollidesWith(GeoFace3 face, GeoAabb3 box, Tolerance tolerance)
            => CollidesWith(face, box.ToObb().ToSolid(), tolerance);

        /// <summary>
        /// Determines whether an oriented box touches a plane, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// The answer of <see cref="CollidesWith(GeoPlane3, GeoSolid3, Tolerance)"/> for <see cref="GeoObb3.ToSolid"/>: a
        /// box 100 by 100 by 100 standing on the floor is touched by the plane z = 100 through its top and missed by the
        /// plane z = 101.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the box is null.</exception>
        public static bool CollidesWith(GeoPlane3 plane, GeoObb3 box) => CollidesWith(plane, box, Tolerance.Global);

        /// <summary>
        /// Determines whether an oriented box touches a plane, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The answer of <see cref="CollidesWith(GeoPlane3, GeoSolid3, Tolerance)"/> for <see cref="GeoObb3.ToSolid"/>: a
        /// box 100 by 100 by 100 standing on the floor is touched by the plane z = 100 through its top and missed by the
        /// plane z = 101.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the box is null.</exception>
        public static bool CollidesWith(GeoPlane3 plane, GeoObb3 box, Tolerance tolerance)
            => CollidesWith(plane, Intersection3.BodyOf(box), tolerance);

        /// <summary>
        /// Determines whether an axis-aligned box touches a plane, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// The answer of <see cref="CollidesWith(GeoPlane3, GeoSolid3, Tolerance)"/> for <c>ToObb().ToSolid()</c>: the box
        /// from the origin to (100, 100, 100) is touched by the plane z = 100 and missed by the plane z = 101.
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown when the box is empty.</exception>
        public static bool CollidesWith(GeoPlane3 plane, GeoAabb3 box) => CollidesWith(plane, box, Tolerance.Global);

        /// <summary>
        /// Determines whether an axis-aligned box touches a plane, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The answer of <see cref="CollidesWith(GeoPlane3, GeoSolid3, Tolerance)"/> for <c>ToObb().ToSolid()</c>: the box
        /// from the origin to (100, 100, 100) is touched by the plane z = 100 and missed by the plane z = 101.
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown when the box is empty.</exception>
        public static bool CollidesWith(GeoPlane3 plane, GeoAabb3 box, Tolerance tolerance)
            => CollidesWith(plane, box.ToObb().ToSolid(), tolerance);

        #endregion
    }
}
