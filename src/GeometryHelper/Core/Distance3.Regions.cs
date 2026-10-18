using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    /// <summary>
    /// Measuring two flat regions in space against each other, a region or a ray against a plane, a segment, a chain or a
    /// ray against a face, a ray against a polygon, a ray or a chain, a body against a face, and a box against those.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two shapes that truly meet are nought apart, a rim counting as meeting, and otherwise the distance is the gap,
    /// measured exactly: the least of every edge of the first against the second and every edge of the second against
    /// the first, a ray standing in for an edge. Nothing widens a shape by the point tolerance, so two regions in one plane
    /// 0.0005 apart are 0.0005 apart, and a region hovering 0.0003 over another is 0.0003 from it; they
    /// <see cref="Collision3">collide</see> exactly where that gap is within the point tolerance.
    /// </para>
    /// <para>
    /// A plane is measured by the heights of the corners, as a chain is
    /// (<see cref="DistanceTo(GeoPolyline3, GeoPlane3, Tolerance)"/>): nought where it touches, otherwise the height of
    /// the nearest corner. A body is read solid through, as it is against a polygon. A box is read through its body: each
    /// answer about a box is the answer about <see cref="GeoObb3.ToSolid"/>, or about <c>ToObb().ToSolid()</c> for an
    /// axis-aligned one, so an empty axis-aligned box is refused, as <see cref="DistanceTo(GeoPolyline3, GeoAabb3)"/>
    /// refuses it.
    /// </para>
    /// </remarks>
    public static partial class Distance3
    {
        #region A segment, a chain and a ray against a face, and a ray against a polygon

        /// <summary>
        /// Calculates the shortest distance between a line segment and the material of a face.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public static double DistanceTo(GeoLine3 line, GeoFace3 face) => DistanceTo(line, face, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance between a line segment and the material of a face, within a tolerance.
        /// </summary>
        /// <param name="line">The segment.</param>
        /// <param name="face">The face, read by its material.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// Nought only where the segment truly meets the material, crossing it, ending on it or lying across it in its
        /// plane, otherwise the gap, measured exactly and nothing widening the material. Over the square 100 by 100 in the
        /// floor with a corner at the origin and a hole 40 by 40 in its middle, the segment from (45, 50, -10) to
        /// (45, 50, 10), through the hole, is 15 from the rim, and the segment from (100.0005, 50, 0) to (150, 50, 0) is
        /// 0.0005 from the outline, whichever way round it is asked. A segment no longer than the point tolerance that
        /// does not pass through the material is read as its start.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public static double DistanceTo(GeoLine3 line, GeoFace3 face, Tolerance tolerance)
        {
            if (face == null)
            {
                throw new ArgumentNullException(nameof(face));
            }

            return Gap3.Segment(Gap3.Region.Of(face), line.StartPoint, line.EndPoint, true, tolerance, 0.0);
        }

        /// <summary>
        /// Calculates the shortest distance between a chain and the material of a face.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the polyline or the face is null.</exception>
        public static double DistanceTo(GeoPolyline3 polyline, GeoFace3 face) => DistanceTo(polyline, face, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance between a chain and the material of a face, within a tolerance.
        /// </summary>
        /// <param name="polyline">The chain.</param>
        /// <param name="face">The face, read by its material.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// Nought only where an edge truly meets the material, crossing it, ending on it or lying across it in its plane,
        /// otherwise the gap, measured exactly. Over the square 100 by 100 in the floor with a corner at the origin and a
        /// hole 40 by 40 in its middle, the chain (20, 20, 30), (20, 20, 80), (50, 50, 80) is 30 above the material.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when the polyline or the face is null.</exception>
        public static double DistanceTo(GeoPolyline3 polyline, GeoFace3 face, Tolerance tolerance)
        {
            if (polyline == null)
            {
                throw new ArgumentNullException(nameof(polyline));
            }

            if (face == null)
            {
                throw new ArgumentNullException(nameof(face));
            }

            return Gap3.Chain(Gap3.Region.Of(face), polyline, 0.0);
        }

        /// <summary>
        /// Calculates the shortest distance between a ray and the material of a face.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public static double DistanceTo(GeoRay3 ray, GeoFace3 face) => DistanceTo(ray, face, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance between a ray and the material of a face, within a tolerance.
        /// </summary>
        /// <param name="ray">The ray.</param>
        /// <param name="face">The face, read by its material.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// Nought only where the ray truly meets the material, running into it, starting on it or lying in its plane
        /// towards it, otherwise the gap, measured exactly. Over the square 100 by 100 in the floor with a corner at the
        /// origin and a hole 40 by 40 in its middle, the ray from (45, 50, 10) pointing straight down, through the hole, is
        /// 15 from the rim, and the ray from (100.0005, 50, 0) along X is 0.0005 from the outline.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public static double DistanceTo(GeoRay3 ray, GeoFace3 face, Tolerance tolerance)
        {
            if (face == null)
            {
                throw new ArgumentNullException(nameof(face));
            }

            return Gap3.Ray(Gap3.Region.Of(face), ray, 0.0);
        }

        /// <summary>
        /// Calculates the shortest distance between a ray and a polygon.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public static double DistanceTo(GeoRay3 ray, GeoPolygon3 polygon) => DistanceTo(ray, polygon, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance between a ray and a polygon, within a tolerance.
        /// </summary>
        /// <param name="ray">The ray.</param>
        /// <param name="polygon">The polygon, read as a filled surface.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// Nought only where the ray truly meets the polygon, running into it, starting on it or lying in its plane
        /// towards it, otherwise the gap, measured exactly. Beside the square 100 by 100 in the floor with a corner at the
        /// origin, the ray from (130, 50, 40) along X is 50 from its edge, and the ray from (100.0005, 50, 0) along X is
        /// 0.0005 from it.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public static double DistanceTo(GeoRay3 ray, GeoPolygon3 polygon, Tolerance tolerance)
        {
            if (polygon == null)
            {
                throw new ArgumentNullException(nameof(polygon));
            }

            return Gap3.Ray(Gap3.Region.Of(polygon), ray, 0.0);
        }

        #endregion

        #region Two flat regions

        /// <summary>
        /// Calculates the shortest distance between a triangle and a polygon.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public static double DistanceTo(GeoTriangle3 triangle, GeoPolygon3 polygon) => DistanceTo(triangle, polygon, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance between a triangle and a polygon, within a tolerance.
        /// </summary>
        /// <param name="triangle">The triangle, read as a filled surface.</param>
        /// <param name="polygon">The polygon, read as a filled surface.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// Nought only where the two truly meet, crossing, touching or one holding the other, otherwise the gap, measured
        /// exactly and nothing widening either. The triangle with corners (0, 0, 0), (100, 0, 0) and (0, 100, 0) and the
        /// square 100 by 100 standing upright along y = 50 from z = 10 to 110 are 10 apart.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public static double DistanceTo(GeoTriangle3 triangle, GeoPolygon3 polygon, Tolerance tolerance)
        {
            if (polygon == null)
            {
                throw new ArgumentNullException(nameof(polygon));
            }

            return Gap3.Regions(Gap3.Region.Of(triangle), Gap3.Region.Of(polygon), 0.0);
        }

        /// <summary>
        /// Calculates the shortest distance between a triangle and the material of a face.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public static double DistanceTo(GeoTriangle3 triangle, GeoFace3 face) => DistanceTo(triangle, face, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance between a triangle and the material of a face, within a tolerance.
        /// </summary>
        /// <param name="triangle">The triangle, read as a filled surface.</param>
        /// <param name="face">The face, read by its material.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// Nought only where the two truly meet, otherwise the gap, measured exactly. The triangle with corners
        /// (110, 50, 0), (200, 50, 0) and (110, 50, 100), standing beside the square 100 by 100 in the floor with a corner
        /// at the origin and a hole 40 by 40 in its middle, is 10 from its outline.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public static double DistanceTo(GeoTriangle3 triangle, GeoFace3 face, Tolerance tolerance)
        {
            if (face == null)
            {
                throw new ArgumentNullException(nameof(face));
            }

            return Gap3.Regions(Gap3.Region.Of(triangle), Gap3.Region.Of(face), 0.0);
        }

        /// <summary>
        /// Calculates the shortest distance between two polygons.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when either polygon is null.</exception>
        public static double DistanceTo(GeoPolygon3 polygon1, GeoPolygon3 polygon2) => DistanceTo(polygon1, polygon2, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance between two polygons, within a tolerance.
        /// </summary>
        /// <param name="polygon1">The first polygon, read as a filled surface.</param>
        /// <param name="polygon2">The second polygon, read as a filled surface.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// Nought only where the two truly meet, crossing, touching or one holding the other, otherwise the gap, measured
        /// exactly and nothing widening either. A square 100 by 100 in the floor with a corner at the origin and one
        /// standing upright along y = 50 from z = 10 to 110 are 10 apart; one lying 0.0004 above it is 0.0004 away, and a
        /// square beside it from x = 100.0005 to 200 is 0.0005 away, whichever way round they are asked.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when either polygon is null.</exception>
        public static double DistanceTo(GeoPolygon3 polygon1, GeoPolygon3 polygon2, Tolerance tolerance)
        {
            if (polygon1 == null)
            {
                throw new ArgumentNullException(nameof(polygon1));
            }

            if (polygon2 == null)
            {
                throw new ArgumentNullException(nameof(polygon2));
            }

            return Gap3.Regions(Gap3.Region.Of(polygon1), Gap3.Region.Of(polygon2), 0.0);
        }

        /// <summary>
        /// Calculates the shortest distance between the material of a face and a polygon.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the face or the polygon is null.</exception>
        public static double DistanceTo(GeoFace3 face, GeoPolygon3 polygon) => DistanceTo(face, polygon, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance between the material of a face and a polygon, within a tolerance.
        /// </summary>
        /// <param name="face">The face, read by its material.</param>
        /// <param name="polygon">The polygon, read as a filled surface.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// Nought only where the two truly meet, otherwise the gap, measured exactly. The square 100 by 100 in the floor
        /// with a corner at the origin and a hole 40 by 40 in its middle is 10 from a square 20 by 20 standing upright in
        /// the middle of the hole along y = 50 from x = 40 to 60.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when the face or the polygon is null.</exception>
        public static double DistanceTo(GeoFace3 face, GeoPolygon3 polygon, Tolerance tolerance)
        {
            if (face == null)
            {
                throw new ArgumentNullException(nameof(face));
            }

            if (polygon == null)
            {
                throw new ArgumentNullException(nameof(polygon));
            }

            return Gap3.Regions(Gap3.Region.Of(face), Gap3.Region.Of(polygon), 0.0);
        }

        /// <summary>
        /// Calculates the shortest distance between the materials of two faces.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when either face is null.</exception>
        public static double DistanceTo(GeoFace3 face1, GeoFace3 face2) => DistanceTo(face1, face2, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance between the materials of two faces, within a tolerance.
        /// </summary>
        /// <param name="face1">The first face, read by its material.</param>
        /// <param name="face2">The second face, read by its material.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// Nought only where the two truly meet, otherwise the gap, measured exactly. The square 100 by 100 in the floor
        /// with a corner at the origin and a hole 40 by 40 in its middle is 10 from the square 20 by 20 lying in the hole
        /// about (50, 50), and nought from one lying on its material.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when either face is null.</exception>
        public static double DistanceTo(GeoFace3 face1, GeoFace3 face2, Tolerance tolerance)
        {
            if (face1 == null)
            {
                throw new ArgumentNullException(nameof(face1));
            }

            if (face2 == null)
            {
                throw new ArgumentNullException(nameof(face2));
            }

            return Gap3.Regions(Gap3.Region.Of(face1), Gap3.Region.Of(face2), 0.0);
        }

        #endregion

        #region A plane against a flat region and a ray

        /// <summary>
        /// Calculates the shortest distance between a plane and a triangle.
        /// </summary>
        public static double DistanceTo(GeoPlane3 plane, GeoTriangle3 triangle) => DistanceTo(plane, triangle, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance between a plane and a triangle, within a tolerance.
        /// </summary>
        /// <param name="plane">The plane.</param>
        /// <param name="triangle">The triangle.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// Nought where <see cref="Collision3.CollidesWith(GeoPlane3, GeoTriangle3, Tolerance)"/> says the two touch,
        /// otherwise the height of the nearest corner. The triangle with corners (0, 50, 10), (100, 50, 10) and
        /// (0, 50, 110) is 10 above the floor plane.
        /// </returns>
        public static double DistanceTo(GeoPlane3 plane, GeoTriangle3 triangle, Tolerance tolerance)
        {
            return Collision3.CollidesWith(plane, triangle, tolerance)
                ? 0.0
                : Gap(plane, new[] { triangle.A, triangle.B, triangle.C });
        }

        /// <summary>
        /// Calculates the shortest distance between a plane and a polygon.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public static double DistanceTo(GeoPlane3 plane, GeoPolygon3 polygon) => DistanceTo(plane, polygon, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance between a plane and a polygon, within a tolerance.
        /// </summary>
        /// <param name="plane">The plane.</param>
        /// <param name="polygon">The polygon.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// Nought where <see cref="Collision3.CollidesWith(GeoPlane3, GeoPolygon3, Tolerance)"/> says the two touch,
        /// otherwise the height of the nearest corner. A square 100 by 100 standing upright along y = 50 from z = 10 to
        /// 110 is 10 above the floor plane; one standing from z = -50 crosses it and is nought away.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public static double DistanceTo(GeoPlane3 plane, GeoPolygon3 polygon, Tolerance tolerance)
        {
            return Collision3.CollidesWith(plane, polygon, tolerance)
                ? 0.0
                : Gap(plane, polygon.Vertices);
        }

        /// <summary>
        /// Calculates the shortest distance between a plane and the material of a face.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public static double DistanceTo(GeoPlane3 plane, GeoFace3 face) => DistanceTo(plane, face, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance between a plane and the material of a face, within a tolerance.
        /// </summary>
        /// <param name="plane">The plane.</param>
        /// <param name="face">The face, read by its material.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// Nought where <see cref="Collision3.CollidesWith(GeoPlane3, GeoFace3, Tolerance)"/> says the two touch,
        /// otherwise the height of the nearest corner of the outline, which the holes lie within. The square 100 by 100
        /// with a hole 40 by 40 in its middle, stood upright along y = 50 from z = 10 to 110, is 10 above the floor plane.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        public static double DistanceTo(GeoPlane3 plane, GeoFace3 face, Tolerance tolerance)
        {
            return Collision3.CollidesWith(plane, face, tolerance)
                ? 0.0
                : Gap(plane, face.Boundary.Vertices);
        }

        /// <summary>
        /// Calculates the shortest distance between a ray and a plane.
        /// </summary>
        public static double DistanceTo(GeoRay3 ray, GeoPlane3 plane) => DistanceTo(ray, plane, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance between a ray and a plane, within a tolerance.
        /// </summary>
        /// <param name="ray">The ray.</param>
        /// <param name="plane">The plane.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// Nought where <see cref="Collision3.CollidesWith(GeoRay3, GeoPlane3, Tolerance)"/> says the two touch,
        /// otherwise the height of the origin, since a ray clear of a plane runs along it or away from it. The ray from
        /// (0, 0, 10) pointing straight up is 10 above the floor plane; pointing straight down it reaches it and is nought
        /// away.
        /// </returns>
        public static double DistanceTo(GeoRay3 ray, GeoPlane3 plane, Tolerance tolerance)
        {
            double height = Math.Abs(plane.SignedDistanceTo(ray.Origin));

            // A ray that is not a number is not a number away, as the other distances carry it; the collision, which
            // reaches the plane at a crossing that is not a number, would read it as touching.
            if (double.IsNaN(height + ray.Direction.DotProduct(plane.Normal)))
            {
                return double.NaN;
            }

            return Collision3.CollidesWith(ray, plane, tolerance) ? 0.0 : height;
        }

        /// <summary>
        /// Gets the height above or below a plane of the nearest of some corners that all stand clear to one side of it.
        /// </summary>
        private static double Gap(GeoPlane3 plane, IReadOnlyList<GeoPoint3> corners)
        {
            double above = double.MinValue;
            double below = double.MaxValue;

            foreach (GeoPoint3 corner in corners)
            {
                double reach = plane.SignedDistanceTo(corner);

                above = Math.Max(above, reach);
                below = Math.Min(below, reach);
            }

            return below > 0.0 ? below : -above;
        }

        #endregion

        #region Rays

        /// <summary>
        /// Calculates the shortest distance between two rays.
        /// </summary>
        public static double DistanceTo(GeoRay3 ray1, GeoRay3 ray2) => DistanceTo(ray1, ray2, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance between two rays, within a tolerance.
        /// </summary>
        /// <param name="ray1">The first ray.</param>
        /// <param name="ray2">The second ray.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// The length of <see cref="Projection3.GetShortestLineTo(GeoRay3, GeoRay3, Tolerance)"/>: nought only where the two
        /// truly meet, otherwise the gap, measured exactly. The ray from the origin along X and the ray from (50, 30, 40)
        /// along Y are 50 apart, and two rays leaving points 0.0003 apart in opposite directions are 0.0003 apart.
        /// </returns>
        public static double DistanceTo(GeoRay3 ray1, GeoRay3 ray2, Tolerance tolerance)
        {
            return Projection3.GetShortestLineTo(ray1, ray2, tolerance).Length;
        }

        /// <summary>
        /// Calculates the shortest distance between a chain and a ray.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the polyline is null.</exception>
        public static double DistanceTo(GeoPolyline3 polyline, GeoRay3 ray) => DistanceTo(polyline, ray, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance between a chain and a ray, within a tolerance.
        /// </summary>
        /// <param name="polyline">The chain.</param>
        /// <param name="ray">The ray.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// The length of <see cref="Projection3.GetShortestLineTo(GeoPolyline3, GeoRay3, Tolerance)"/>: nought only where the
        /// two truly meet, otherwise the gap, measured exactly. The chain (0, 0, 0), (100, 0, 0), (100, 100, 0) is 50 from
        /// the ray from (60, 50, 30) pointing straight up.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when the polyline is null.</exception>
        public static double DistanceTo(GeoPolyline3 polyline, GeoRay3 ray, Tolerance tolerance)
        {
            return Projection3.GetShortestLineTo(polyline, ray, tolerance).Length;
        }

        #endregion

        #region A body against a face

        /// <summary>
        /// Calculates the shortest distance between a solid and the material of a face.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the solid or the face is null.</exception>
        public static double DistanceTo(GeoSolid3 solid, GeoFace3 face) => DistanceTo(solid, face, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance between a solid and the material of a face, within a tolerance.
        /// </summary>
        /// <param name="solid">The solid, read solid through.</param>
        /// <param name="face">The face, read by its material.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>Zero where the material reaches into the body, otherwise the gap between the two surfaces.</returns>
        /// <remarks>
        /// The material is cut into the triangles <see cref="GeoFace3.TriangulateSurface(Tolerance)"/> gives and each is
        /// measured against the body, as a polygon is in <see cref="DistanceTo(GeoSolid3, GeoPolygon3, Tolerance)"/>, so
        /// no triangle covers a hole. The box 100 by 100 by 100 with a corner at the origin passes through the hole 150 by
        /// 150 in the middle of the square 200 by 200 from (-50, -50, 50) to (150, 150, 50) and is 25 from its rim; the
        /// same square without its hole cuts the box and is nought away.
        /// Whether a point is in the body is asked to within rounding, a billionth of a millimetre (1E-9, not nought),
        /// so that rounding in a body turned about in space cannot lose a crossing: a point within 1E-9 outside the
        /// body reads as in it, and a point 5E-10 outside a box is nought away from it.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the solid or the face is null.</exception>
        public static double DistanceTo(GeoSolid3 solid, GeoFace3 face, Tolerance tolerance)
        {
            if (solid == null)
            {
                throw new ArgumentNullException(nameof(solid));
            }

            if (face == null)
            {
                throw new ArgumentNullException(nameof(face));
            }

            // The faces run straight across every opening; the nearest material can sit on any opening's rim.
            solid = Material3.Whole(solid, tolerance);

            double best = double.MaxValue;

            foreach (GeoTriangle3 piece in face.TriangulateSurface(tolerance))
            {
                best = Math.Min(best, DistanceTo(solid, piece, tolerance));

                if (best <= 0.0)
                {
                    return 0.0;
                }
            }

            return best;
        }

        #endregion

        #region Boxes, through their bodies

        /// <summary>
        /// Calculates the shortest distance between a line segment and an oriented box.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the box is null.</exception>
        public static double DistanceTo(GeoLine3 line, GeoObb3 box) => DistanceTo(line, box, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance between a line segment and an oriented box, within a tolerance: the answer
        /// of <see cref="DistanceTo(GeoLine3, GeoSolid3, Tolerance)"/> for <see cref="GeoObb3.ToSolid"/>.
        /// </summary>
        /// <remarks>
        /// The box 100 by 100 by 100 about (50, 50, 50) is 50 from the segment from (150, 50, 50) to (150, 50, 150), and
        /// nought from one reaching into it.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the box is null.</exception>
        public static double DistanceTo(GeoLine3 line, GeoObb3 box, Tolerance tolerance)
            => new Gap3.Block(box ?? throw new ArgumentNullException(nameof(box))).Segment(line.StartPoint, line.EndPoint, true, tolerance, 0.0);

        /// <summary>
        /// Calculates the shortest distance between a line segment and an axis-aligned box.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the box is empty.</exception>
        public static double DistanceTo(GeoLine3 line, GeoAabb3 box) => DistanceTo(line, box, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance between a line segment and an axis-aligned box, within a tolerance: the
        /// answer of <see cref="DistanceTo(GeoLine3, GeoSolid3, Tolerance)"/> for <c>ToObb().ToSolid()</c>.
        /// </summary>
        /// <remarks>
        /// The box from the origin to (100, 100, 100) is 50 from the segment from (150, 50, 50) to (150, 50, 150), and
        /// nought from one reaching into it.
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown when the box is empty.</exception>
        public static double DistanceTo(GeoLine3 line, GeoAabb3 box, Tolerance tolerance)
            => DistanceTo(line, box.ToObb(), tolerance);

        /// <summary>
        /// Calculates the shortest distance between a ray and an oriented box.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the box is null.</exception>
        public static double DistanceTo(GeoRay3 ray, GeoObb3 box) => DistanceTo(ray, box, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance between a ray and an oriented box, within a tolerance: the answer of
        /// <see cref="DistanceTo(GeoRay3, GeoSolid3, Tolerance)"/> for <see cref="GeoObb3.ToSolid"/>.
        /// </summary>
        /// <remarks>
        /// The box 100 by 100 by 100 about (50, 50, 50) is 50 from the ray from (150, 50, 50) along X, and nought from the
        /// same ray turned round.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the box is null.</exception>
        public static double DistanceTo(GeoRay3 ray, GeoObb3 box, Tolerance tolerance)
            => new Gap3.Block(box ?? throw new ArgumentNullException(nameof(box))).Ray(ray, 0.0);

        /// <summary>
        /// Calculates the shortest distance between a ray and an axis-aligned box.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the box is empty.</exception>
        public static double DistanceTo(GeoRay3 ray, GeoAabb3 box) => DistanceTo(ray, box, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance between a ray and an axis-aligned box, within a tolerance: the answer of
        /// <see cref="DistanceTo(GeoRay3, GeoSolid3, Tolerance)"/> for <c>ToObb().ToSolid()</c>.
        /// </summary>
        /// <remarks>
        /// The box from the origin to (100, 100, 100) is 50 from the ray from (150, 50, 50) along X, and nought from the
        /// same ray turned round.
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown when the box is empty.</exception>
        public static double DistanceTo(GeoRay3 ray, GeoAabb3 box, Tolerance tolerance)
            => DistanceTo(ray, box.ToObb(), tolerance);

        /// <summary>
        /// Calculates the shortest distance between a plane and an oriented box.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the box is null.</exception>
        public static double DistanceTo(GeoPlane3 plane, GeoObb3 box) => DistanceTo(plane, box, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance between a plane and an oriented box, within a tolerance: the answer of
        /// <see cref="DistanceTo(GeoSolid3, GeoPlane3, Tolerance)"/> for <see cref="GeoObb3.ToSolid"/>.
        /// </summary>
        /// <remarks>
        /// The box 100 by 100 by 100 about (50, 50, 50) is 30 from the plane z = 130, and nought from the plane z = 50.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the box is null.</exception>
        public static double DistanceTo(GeoPlane3 plane, GeoObb3 box, Tolerance tolerance)
            => DistanceTo(Intersection3.BodyOf(box), plane, tolerance);

        /// <summary>
        /// Calculates the shortest distance between a plane and an axis-aligned box.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the box is empty.</exception>
        public static double DistanceTo(GeoPlane3 plane, GeoAabb3 box) => DistanceTo(plane, box, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance between a plane and an axis-aligned box, within a tolerance: the answer of
        /// <see cref="DistanceTo(GeoSolid3, GeoPlane3, Tolerance)"/> for <c>ToObb().ToSolid()</c>.
        /// </summary>
        /// <remarks>
        /// The box from the origin to (100, 100, 100) is 30 from the plane z = 130, and nought from the plane z = 50.
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown when the box is empty.</exception>
        public static double DistanceTo(GeoPlane3 plane, GeoAabb3 box, Tolerance tolerance)
            => DistanceTo(box.ToObb().ToSolid(), plane, tolerance);

        /// <summary>
        /// Calculates the shortest distance between a triangle and an oriented box.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the box is null.</exception>
        public static double DistanceTo(GeoTriangle3 triangle, GeoObb3 box) => DistanceTo(triangle, box, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance between a triangle and an oriented box, within a tolerance: the answer of
        /// <see cref="DistanceTo(GeoSolid3, GeoTriangle3, Tolerance)"/> for <see cref="GeoObb3.ToSolid"/>.
        /// </summary>
        /// <remarks>
        /// The box 100 by 100 by 100 about (50, 50, 50) is 30 from the triangle with corners (0, 0, 130), (100, 0, 130) and
        /// (0, 100, 130), and nought from one wholly inside it.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the box is null.</exception>
        public static double DistanceTo(GeoTriangle3 triangle, GeoObb3 box, Tolerance tolerance)
            => DistanceTo(Intersection3.BodyOf(box), triangle, tolerance);

        /// <summary>
        /// Calculates the shortest distance between a triangle and an axis-aligned box.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the box is empty.</exception>
        public static double DistanceTo(GeoTriangle3 triangle, GeoAabb3 box) => DistanceTo(triangle, box, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance between a triangle and an axis-aligned box, within a tolerance: the answer of
        /// <see cref="DistanceTo(GeoSolid3, GeoTriangle3, Tolerance)"/> for <c>ToObb().ToSolid()</c>.
        /// </summary>
        /// <remarks>
        /// The box from the origin to (100, 100, 100) is 30 from the triangle with corners (0, 0, 130), (100, 0, 130) and
        /// (0, 100, 130), and nought from one wholly inside it.
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown when the box is empty.</exception>
        public static double DistanceTo(GeoTriangle3 triangle, GeoAabb3 box, Tolerance tolerance)
            => DistanceTo(box.ToObb().ToSolid(), triangle, tolerance);

        /// <summary>
        /// Calculates the shortest distance between a polygon and an oriented box.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the polygon or the box is null.</exception>
        public static double DistanceTo(GeoPolygon3 polygon, GeoObb3 box) => DistanceTo(polygon, box, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance between a polygon and an oriented box, within a tolerance: the answer of
        /// <see cref="DistanceTo(GeoSolid3, GeoPolygon3, Tolerance)"/> for <see cref="GeoObb3.ToSolid"/>.
        /// </summary>
        /// <remarks>
        /// The box 100 by 100 by 100 about (50, 50, 50) is 30 from the square 100 by 100 lying flat at a height of 130 over
        /// it, and nought from the square 200 by 200 lying across it at half its height.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the polygon or the box is null.</exception>
        public static double DistanceTo(GeoPolygon3 polygon, GeoObb3 box, Tolerance tolerance)
            => DistanceTo(Intersection3.BodyOf(box), polygon, tolerance);

        /// <summary>
        /// Calculates the shortest distance between a polygon and an axis-aligned box.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the box is empty.</exception>
        public static double DistanceTo(GeoPolygon3 polygon, GeoAabb3 box) => DistanceTo(polygon, box, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance between a polygon and an axis-aligned box, within a tolerance: the answer of
        /// <see cref="DistanceTo(GeoSolid3, GeoPolygon3, Tolerance)"/> for <c>ToObb().ToSolid()</c>.
        /// </summary>
        /// <remarks>
        /// The box from the origin to (100, 100, 100) is 30 from the square 100 by 100 lying flat at a height of 130 over
        /// it, and nought from the square 200 by 200 lying across it at half its height.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the box is empty.</exception>
        public static double DistanceTo(GeoPolygon3 polygon, GeoAabb3 box, Tolerance tolerance)
            => DistanceTo(box.ToObb().ToSolid(), polygon, tolerance);

        /// <summary>
        /// Calculates the shortest distance between the material of a face and an oriented box.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the face or the box is null.</exception>
        public static double DistanceTo(GeoFace3 face, GeoObb3 box) => DistanceTo(face, box, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance between the material of a face and an oriented box, within a tolerance: the
        /// answer of <see cref="DistanceTo(GeoSolid3, GeoFace3, Tolerance)"/> for <see cref="GeoObb3.ToSolid"/>.
        /// </summary>
        /// <remarks>
        /// The box 100 by 100 by 100 about (50, 50, 50) passes through the hole 150 by 150 in the middle of the square
        /// 200 by 200 from (-50, -50, 50) to (150, 150, 50) and is 25 from its rim.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the face or the box is null.</exception>
        public static double DistanceTo(GeoFace3 face, GeoObb3 box, Tolerance tolerance)
            => DistanceTo(Intersection3.BodyOf(box), face, tolerance);

        /// <summary>
        /// Calculates the shortest distance between the material of a face and an axis-aligned box.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the box is empty.</exception>
        public static double DistanceTo(GeoFace3 face, GeoAabb3 box) => DistanceTo(face, box, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance between the material of a face and an axis-aligned box, within a tolerance:
        /// the answer of <see cref="DistanceTo(GeoSolid3, GeoFace3, Tolerance)"/> for <c>ToObb().ToSolid()</c>.
        /// </summary>
        /// <remarks>
        /// The box from the origin to (100, 100, 100) passes through the hole 150 by 150 in the middle of the square
        /// 200 by 200 from (-50, -50, 50) to (150, 150, 50) and is 25 from its rim.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the face is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the box is empty.</exception>
        public static double DistanceTo(GeoFace3 face, GeoAabb3 box, Tolerance tolerance)
            => DistanceTo(box.ToObb().ToSolid(), face, tolerance);

        /// <summary>
        /// Calculates the shortest distance between an oriented box and an axis-aligned box.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when the oriented box is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the axis-aligned box is empty.</exception>
        public static double DistanceTo(GeoObb3 box1, GeoAabb3 box2) => DistanceTo(box1, box2, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance between an oriented box and an axis-aligned box, within a tolerance: the
        /// answer of <see cref="DistanceTo(GeoObb3, GeoObb3, Tolerance)"/> for <c>box2.ToObb()</c>, two oriented boxes
        /// being measured as their bodies once the separating axis test has found them apart.
        /// </summary>
        /// <remarks>
        /// The box from the origin to (100, 100, 100) and the box 100 by 100 by 100 about (250, 50, 50) are 100 apart;
        /// turned 45 degrees about the vertical, the second reaches a corner 50 * sqrt(2) towards the first and is about
        /// 79.29 from it.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the oriented box is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the axis-aligned box is empty.</exception>
        public static double DistanceTo(GeoObb3 box1, GeoAabb3 box2, Tolerance tolerance)
            => DistanceTo(box1, box2.ToObb(), tolerance);

        #endregion
    }
}
