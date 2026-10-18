using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    /// <summary>
    /// Whether two flat regions in space touch, whether a plane touches a region, a ray or a body, whether two rays
    /// touch, and whether a region touches a body or a box.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Two regions.</b> A triangle, a polygon and a face are read filled, a face by its material. Two of them touch
    /// exactly where an edge of either reaches the other region, which is the segment against a region asked once for
    /// each edge, a region lying inside the other included. A square 20 by 20 standing upright in the middle of a hole
    /// 40 by 40 in a plate 100 by 100 touches nothing.
    /// </para>
    /// <para>
    /// <b>Within the tolerance.</b> Two regions touch wherever the gap between them is no longer than the point
    /// tolerance, the gap <see cref="Distance3"/> measures, exact and in space, whether they lie level, tilted or in
    /// planes that are not one: a plate one side 0.0002 and the other 0.0011 over a square in the floor is 0.0002 from
    /// it and touches it, as it does lying level 0.0009 over it, and a square lying beside it 0.0012 off its side does
    /// not touch it. Nothing is laid out flat for the purpose: a region off another's plane by less than the planar
    /// tolerance is as far from it in space as it is.
    /// </para>
    /// <para>
    /// <b>A plane.</b> A bounded shape touches a plane when the heights of its corners above it change sign or reach
    /// nought within the point tolerance, as a straight chain is measured against a plane. A body touches it when one
    /// of its faces does, read over the material, so a body in two pieces either side of the plane does not.
    /// </para>
    /// </remarks>
    public static partial class Collision3
    {
        #region Two flat regions

        /// <summary>
        /// Determines whether a triangle and a polygon touch, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// The triangle with corners (0, 0, 0), (100, 0, 0) and (0, 100, 0) touches the square 100 by 100 standing
        /// upright across it along y = 50; moved up to stand on z = 10 the square misses it.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public static bool CollidesWith(GeoTriangle3 triangle, GeoPolygon3 polygon) => CollidesWith(triangle, polygon, Tolerance.Global);

        /// <summary>
        /// Determines whether a triangle and a polygon touch, within a tolerance.
        /// </summary>
        /// <param name="triangle">The triangle.</param>
        /// <param name="polygon">The polygon.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when they overlap or touch anywhere; otherwise, false.</returns>
        /// <remarks>
        /// The triangle with corners (0, 0, 0), (100, 0, 0) and (0, 100, 0) touches the square 100 by 100 standing
        /// upright across it along y = 50; moved up to stand on z = 10 the square misses it. A triangle with no area
        /// touches nothing, as <see cref="CollidesWith(GeoTriangle3, GeoTriangle3, Tolerance)"/> reads it.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public static bool CollidesWith(GeoTriangle3 triangle, GeoPolygon3 polygon, Tolerance tolerance)
        {
            if (polygon == null)
            {
                throw new ArgumentNullException(nameof(polygon));
            }

            return !triangle.IsDegenerate(tolerance)
                && triangle.GetAabb().DistanceTo(polygon.GetAabb()) <= Reach(tolerance)
                && Gap3.Regions(Gap3.Region.Of(triangle), Gap3.Region.Of(polygon), tolerance.EqualPoint) <= tolerance.EqualPoint;
        }

        /// <summary>
        /// Determines whether a triangle and the material of a face touch, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// The square 100 by 100 in the floor with a hole 40 by 40 in its middle is not touched by the triangle with
        /// corners (40, 50, -10), (60, 50, -10) and (50, 50, 10) standing in the hole; moved 30 along X, onto the
        /// material, it is.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public static bool CollidesWith(GeoTriangle3 triangle, GeoFace3 face) => CollidesWith(triangle, face, Tolerance.Global);

        /// <summary>
        /// Determines whether a triangle and the material of a face touch, within a tolerance.
        /// </summary>
        /// <param name="triangle">The triangle.</param>
        /// <param name="face">The face, read by its material.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when the triangle overlaps or touches the material anywhere; otherwise, false.</returns>
        /// <remarks>
        /// The square 100 by 100 in the floor with a hole 40 by 40 in its middle is not touched by the triangle with
        /// corners (40, 50, -10), (60, 50, -10) and (50, 50, 10) standing in the hole; moved 30 along X, onto the
        /// material, it is.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public static bool CollidesWith(GeoTriangle3 triangle, GeoFace3 face, Tolerance tolerance)
        {
            if (face == null)
            {
                throw new ArgumentNullException(nameof(face));
            }

            return !triangle.IsDegenerate(tolerance)
                && triangle.GetAabb().DistanceTo(face.GetAabb()) <= Reach(tolerance)
                && Gap3.Regions(Gap3.Region.Of(triangle), Gap3.Region.Of(face), tolerance.EqualPoint) <= tolerance.EqualPoint;
        }

        /// <summary>
        /// Determines whether two polygons touch, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// A square 100 by 100 lying in the floor with a corner at the origin touches one standing upright across it
        /// along y = 50, and one lying beside it from x = 100 to 200; one lying 10 above it touches nothing.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when either polygon is null.</exception>
        public static bool CollidesWith(GeoPolygon3 polygon1, GeoPolygon3 polygon2) => CollidesWith(polygon1, polygon2, Tolerance.Global);

        /// <summary>
        /// Determines whether two polygons touch, within a tolerance.
        /// </summary>
        /// <param name="polygon1">The first polygon.</param>
        /// <param name="polygon2">The second polygon.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when they overlap or touch anywhere; otherwise, false.</returns>
        /// <remarks>
        /// A square 100 by 100 lying in the floor with a corner at the origin touches one standing upright across it
        /// along y = 50, and one lying beside it from x = 100 to 200; one lying 10 above it touches nothing. They touch
        /// exactly where <see cref="Distance3.DistanceTo(GeoPolygon3, GeoPolygon3, Tolerance)"/> is no longer than the point
        /// tolerance: the square from x = 100.0005 to 200 touches it at a tolerance of a thousandth, and the one from
        /// x = 100.0012 does not.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when either polygon is null.</exception>
        public static bool CollidesWith(GeoPolygon3 polygon1, GeoPolygon3 polygon2, Tolerance tolerance)
        {
            if (polygon1 == null)
            {
                throw new ArgumentNullException(nameof(polygon1));
            }

            if (polygon2 == null)
            {
                throw new ArgumentNullException(nameof(polygon2));
            }

            return polygon1.GetAabb().DistanceTo(polygon2.GetAabb()) <= Reach(tolerance)
                && Gap3.Regions(Gap3.Region.Of(polygon1), Gap3.Region.Of(polygon2), tolerance.EqualPoint) <= tolerance.EqualPoint;
        }

        /// <summary>
        /// Determines whether the material of a face and a polygon touch, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// The square 100 by 100 in the floor with a hole 40 by 40 in its middle is not touched by a square 20 by 20
        /// standing upright in the middle of the hole, nor by one lying in the hole; one lying across the rim touches it.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the face or the polygon is null.</exception>
        public static bool CollidesWith(GeoFace3 face, GeoPolygon3 polygon) => CollidesWith(face, polygon, Tolerance.Global);

        /// <summary>
        /// Determines whether the material of a face and a polygon touch, within a tolerance.
        /// </summary>
        /// <param name="face">The face, read by its material.</param>
        /// <param name="polygon">The polygon.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when the polygon overlaps or touches the material anywhere; otherwise, false.</returns>
        /// <remarks>
        /// The square 100 by 100 in the floor with a hole 40 by 40 in its middle is not touched by a square 20 by 20
        /// standing upright in the middle of the hole, nor by one lying in the hole; one lying across the rim touches it.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the face or the polygon is null.</exception>
        public static bool CollidesWith(GeoFace3 face, GeoPolygon3 polygon, Tolerance tolerance)
        {
            if (face == null)
            {
                throw new ArgumentNullException(nameof(face));
            }

            if (polygon == null)
            {
                throw new ArgumentNullException(nameof(polygon));
            }

            return face.GetAabb().DistanceTo(polygon.GetAabb()) <= Reach(tolerance)
                && Gap3.Regions(Gap3.Region.Of(face), Gap3.Region.Of(polygon), tolerance.EqualPoint) <= tolerance.EqualPoint;
        }

        /// <summary>
        /// Determines whether the materials of two faces touch, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// A face lying wholly in a hole of another, in its plane or standing across it, touches nothing; one lying on
        /// its material touches it.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when either face is null.</exception>
        public static bool CollidesWith(GeoFace3 face1, GeoFace3 face2) => CollidesWith(face1, face2, Tolerance.Global);

        /// <summary>
        /// Determines whether the materials of two faces touch, within a tolerance.
        /// </summary>
        /// <param name="face1">The first face, read by its material.</param>
        /// <param name="face2">The second face, read by its material.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when the materials overlap or touch anywhere; otherwise, false.</returns>
        /// <remarks>
        /// A face lying wholly in a hole of another, in its plane or standing across it, touches nothing; one lying on
        /// its material touches it. The square 100 by 100 in the floor with a hole 40 by 40 in its middle is not
        /// touched by a square 20 by 20 about (50, 50) lying in its hole, and is by one about (15, 50).
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when either face is null.</exception>
        public static bool CollidesWith(GeoFace3 face1, GeoFace3 face2, Tolerance tolerance)
        {
            if (face1 == null)
            {
                throw new ArgumentNullException(nameof(face1));
            }

            if (face2 == null)
            {
                throw new ArgumentNullException(nameof(face2));
            }

            return face1.GetAabb().DistanceTo(face2.GetAabb()) <= Reach(tolerance)
                && Gap3.Regions(Gap3.Region.Of(face1), Gap3.Region.Of(face2), tolerance.EqualPoint) <= tolerance.EqualPoint;
        }

        #endregion

        #region A plane against a bounded shape

        /// <summary>
        /// Determines whether a triangle touches a plane, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// The floor plane touches the triangle with corners (0, 50, -50), (100, 50, -50) and (0, 50, 50) standing
        /// across it, and one lying in it; moved up 10 above the floor, a triangle lying flat misses it.
        /// </remarks>
        public static bool CollidesWith(GeoPlane3 plane, GeoTriangle3 triangle) => CollidesWith(plane, triangle, Tolerance.Global);

        /// <summary>
        /// Determines whether a triangle touches a plane, within a tolerance.
        /// </summary>
        /// <param name="plane">The plane.</param>
        /// <param name="triangle">The triangle.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// true when the heights of its corners above the plane change sign or one of them is nought within the point
        /// tolerance, or the triangle lies in the plane; otherwise, false.
        /// </returns>
        /// <remarks>
        /// The floor plane touches the triangle with corners (0, 50, -50), (100, 50, -50) and (0, 50, 50) standing
        /// across it, and one lying in it; moved up 10 above the floor, a triangle lying flat misses it.
        /// </remarks>
        public static bool CollidesWith(GeoPlane3 plane, GeoTriangle3 triangle, Tolerance tolerance)
        {
            GeoPoint3[] corners = { triangle.A, triangle.B, triangle.C };

            return Reaches(plane, corners, tolerance) || Boolean3.LiesIn(plane, corners, tolerance);
        }

        /// <summary>
        /// Determines whether a polygon touches a plane, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// The floor plane touches a square 100 by 100 standing upright across it from z = -50 to 50, one standing on it
        /// from z = 0 to 100, and one lying in it; one standing from z = 10 misses it.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public static bool CollidesWith(GeoPlane3 plane, GeoPolygon3 polygon) => CollidesWith(plane, polygon, Tolerance.Global);

        /// <summary>
        /// Determines whether a polygon touches a plane, within a tolerance.
        /// </summary>
        /// <param name="plane">The plane.</param>
        /// <param name="polygon">The polygon.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// true when the heights of its corners above the plane change sign or one of them is nought within the point
        /// tolerance, or the polygon lies in the plane; otherwise, false.
        /// </returns>
        /// <remarks>
        /// The floor plane touches a square 100 by 100 standing upright across it from z = -50 to 50, one standing on it
        /// from z = 0 to 100, and one lying in it; one standing from z = 10 misses it.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public static bool CollidesWith(GeoPlane3 plane, GeoPolygon3 polygon, Tolerance tolerance)
        {
            if (polygon == null)
            {
                throw new ArgumentNullException(nameof(polygon));
            }

            return Reaches(plane, polygon.Vertices, tolerance) || Boolean3.LiesIn(plane, polygon.Vertices, tolerance);
        }

        /// <summary>
        /// Determines whether the material of a face touches a plane, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// The floor plane touches the square 100 by 100 with a hole 40 by 40 in its middle, stood upright across it
        /// from z = -50 to 50; stood up from z = 10 it misses it.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public static bool CollidesWith(GeoPlane3 plane, GeoFace3 face) => CollidesWith(plane, face, Tolerance.Global);

        /// <summary>
        /// Determines whether the material of a face touches a plane, within a tolerance.
        /// </summary>
        /// <param name="plane">The plane.</param>
        /// <param name="face">The face.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// true when the heights of the corners of its outline above the plane change sign or one of them is nought
        /// within the point tolerance, or the face lies in the plane; otherwise, false.
        /// </returns>
        /// <remarks>
        /// The outline decides: a plane reaching the outline reaches the material along it, and the holes lie within
        /// it. The floor plane touches the square 100 by 100 with a hole 40 by 40 in its middle, stood upright across it
        /// from z = -50 to 50; stood up from z = 10 it misses it.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public static bool CollidesWith(GeoPlane3 plane, GeoFace3 face, Tolerance tolerance)
        {
            if (face == null)
            {
                throw new ArgumentNullException(nameof(face));
            }

            return Reaches(plane, face.Boundary.Vertices, tolerance) || Boolean3.LiesIn(plane, face, tolerance);
        }

        /// <summary>
        /// Determines whether a body touches a plane, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// A box 100 by 100 by 100 standing on the floor is touched by the plane z = 50 and by the plane z = 100 through
        /// its top, and missed by the plane z = 101.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the solid is null.</exception>
        public static bool CollidesWith(GeoPlane3 plane, GeoSolid3 solid) => CollidesWith(plane, solid, Tolerance.Global);

        /// <summary>
        /// Determines whether a body touches a plane, within a tolerance.
        /// </summary>
        /// <param name="plane">The plane.</param>
        /// <param name="solid">The solid, read by its material.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// true when the heights of the corners of one of its faces above the plane change sign or one of them is
        /// nought within the point tolerance; otherwise, false.
        /// </returns>
        /// <remarks>
        /// Every face is flat and its outline runs unbroken round it, so a plane meets the face exactly when the corners
        /// of the outline are not all on one side of it, and a plane meeting the body meets a face, since the body is
        /// bounded and the plane is not. Asked over all the corners at once, the test would find a body in two pieces
        /// either side of the plane touching it; asked face by face, it does not. The openings the plane passes through
        /// are cut in first, so a plane through an opening alone touches nothing. A box 100 by 100 by 100 standing on
        /// the floor is touched by the plane z = 50 and by the plane z = 100 through its top, and missed by the plane
        /// z = 101.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the solid is null.</exception>
        public static bool CollidesWith(GeoPlane3 plane, GeoSolid3 solid, Tolerance tolerance)
        {
            if (solid == null)
            {
                throw new ArgumentNullException(nameof(solid));
            }

            // The faces run straight across every opening; read the material the plane can reach instead.
            GeoSolid3 material = Material3.Near(solid, plane, tolerance);

            foreach (GeoFace3 face in material.Faces)
            {
                if (Reaches(plane, face.Boundary.Vertices, tolerance))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Determines whether a ray touches a plane, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// A ray from (0, 0, 10) pointing straight down touches the floor plane, and one lying in it touches it
        /// everywhere; one pointing straight up misses it.
        /// </remarks>
        public static bool CollidesWith(GeoRay3 ray, GeoPlane3 plane) => CollidesWith(ray, plane, Tolerance.Global);

        /// <summary>
        /// Determines whether a ray touches a plane, within a tolerance.
        /// </summary>
        /// <param name="ray">The ray.</param>
        /// <param name="plane">The plane.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// true when the ray starts on the plane within the planar tolerance, or reaches it as
        /// <see cref="Intersection3.TryIntersectWith(GeoRay3, GeoPlane3, out GeoPoint3, Tolerance)"/> reads it;
        /// otherwise, false.
        /// </returns>
        /// <remarks>
        /// A ray lying in the plane crosses it nowhere and touches it everywhere, which is the one place this and
        /// <see cref="Intersection3.GetIntersections(GeoRay3, GeoPlane3, Tolerance)"/> part. A ray from (0, 0, 10)
        /// pointing straight down touches the floor plane; one pointing straight up misses it.
        /// </remarks>
        public static bool CollidesWith(GeoRay3 ray, GeoPlane3 plane, Tolerance tolerance)
        {
            double rise = ray.Origin.GetVectorTo(plane.Origin).DotProduct(plane.Normal);

            return Math.Abs(rise) <= tolerance.EqualPlanar || Intersection3.TryIntersectWith(ray, plane, out _, tolerance);
        }

        /// <summary>
        /// Determines whether the heights of some corners above a plane change sign or reach nought within the point
        /// tolerance, as <see cref="Distance3.DistanceTo(GeoPolyline3, GeoPlane3, Tolerance)"/> reads a chain's.
        /// </summary>
        private static bool Reaches(GeoPlane3 plane, IReadOnlyList<GeoPoint3> corners, Tolerance tolerance)
        {
            double above = double.MinValue;
            double below = double.MaxValue;

            foreach (GeoPoint3 corner in corners)
            {
                double reach = plane.SignedDistanceTo(corner);

                above = Math.Max(above, reach);
                below = Math.Min(below, reach);
            }

            return above >= -tolerance.EqualPoint && below <= tolerance.EqualPoint;
        }

        #endregion

        #region Two rays

        /// <summary>
        /// Determines whether two rays touch, using the default tolerance.
        /// </summary>
        /// <remarks>
        /// A ray from the origin along X touches one from (50, -50, 0) along Y, and one along the X axis from 100 facing
        /// back; one from (50, 10, 0) along Y misses it.
        /// </remarks>
        public static bool CollidesWith(GeoRay3 ray1, GeoRay3 ray2) => CollidesWith(ray1, ray2, Tolerance.Global);

        /// <summary>
        /// Determines whether two rays touch, within a tolerance.
        /// </summary>
        /// <param name="ray1">The first ray.</param>
        /// <param name="ray2">The second ray.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when they meet, whether at a point or along a length; otherwise, false.</returns>
        /// <remarks>
        /// Two rays meet where the shortest line between them is no longer than the point tolerance, exactly where
        /// <see cref="Intersection3.GetIntersections(GeoRay3, GeoRay3, Tolerance)"/> names their crossing; rays parallel
        /// within rounding meet where either starts on the other, along the stretch they share, and name no place, as two
        /// segments lying along each other do. A ray from the origin
        /// along X touches one from (50, -50, 0) along Y, and one along the X axis from 100 facing back; one from
        /// (50, 10, 0) along Y misses it, and so does one along the X axis from -100 facing away.
        /// </remarks>
        public static bool CollidesWith(GeoRay3 ray1, GeoRay3 ray2, Tolerance tolerance)
            => Projection3.GetShortestLineTo(ray1, ray2, tolerance).Length <= tolerance.EqualPoint;

        #endregion
    }
}
