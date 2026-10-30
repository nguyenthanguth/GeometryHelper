using System;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    /// <summary>
    /// Provides static methods for distance calculations in 3D space.
    /// <para>
    /// Every distance here is the shortest gap between the two shapes, and is zero when they touch or
    /// overlap. A bounded shape is measured as the set of points it actually occupies: a segment ends at
    /// its endpoints, a triangle includes its interior, and a plane is infinite in every direction.
    /// </para>
    /// </summary>
    public static partial class Distance3
    {
        #region Point - Point

        /// <summary>
        /// Calculates the distance between two points.
        /// </summary>
        public static double DistanceTo(GeoPoint3 p1, GeoPoint3 p2) => Math.Sqrt(GetDistanceSquaredTo(p1, p2));

        /// <summary>
        /// Calculates the squared distance between two points.
        /// </summary>
        /// <remarks>
        /// Comparing squared distances avoids a square root, which matters in the loops that scan every
        /// vertex or edge of a shape looking for the nearest one.
        /// </remarks>
        public static double GetDistanceSquaredTo(GeoPoint3 p1, GeoPoint3 p2)
        {
            double dx = p2.X - p1.X;
            double dy = p2.Y - p1.Y;
            double dz = p2.Z - p1.Z;

            return dx * dx + dy * dy + dz * dz;
        }

        #endregion

        #region Point - Line, Ray

        /// <summary>
        /// Calculates the shortest distance from a line segment to a point.
        /// </summary>
        public static double DistanceTo(GeoLine3 line, GeoPoint3 point) => DistanceTo(line, point, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance from a line segment to a point, within a tolerance.
        /// </summary>
        public static double DistanceTo(GeoLine3 line, GeoPoint3 point, Tolerance tolerance)
        {
            return DistanceTo(Projection3.ProjectToLine(line, point, tolerance), point);
        }

        /// <summary>
        /// Calculates the shortest distance from a ray to a point.
        /// </summary>
        public static double DistanceTo(GeoRay3 ray, GeoPoint3 point)
        {
            return DistanceTo(Projection3.ProjectToRay(ray, point), point);
        }

        #endregion

        #region Point - Plane, Triangle

        /// <summary>
        /// Calculates the perpendicular distance from a plane to a point.
        /// </summary>
        public static double DistanceTo(GeoPlane3 plane, GeoPoint3 point) => Math.Abs(plane.SignedDistanceTo(point));

        /// <summary>
        /// Calculates the shortest distance from a triangle to a point.
        /// </summary>
        /// <remarks>
        /// The triangle counts as a filled surface, so a point sitting directly above its interior is
        /// measured to the surface rather than out to the nearest edge, and a point on the surface is at
        /// distance zero. The gap is measured from the triangle's own corners and plane, so a point on a sliver 0.0001 wide
        /// and 100 long reads nought, where the closest point found by projection can be 0.0005 away.
        /// </remarks>
        public static double DistanceTo(GeoTriangle3 triangle, GeoPoint3 point)
            => Gap3.PointToTriangle(triangle, point);

        /// <summary>
        /// Calculates the shortest distance from a triangle to a point as the closest-point search of
        /// <see cref="Projection3.ProjectToTriangle(GeoTriangle3, GeoPoint3)"/> finds it.
        /// </summary>
        /// <remarks>
        /// This is what the search for the nearest triangles of two bodies and the collision of two triangles ask, which the
        /// clash reads and keeps as it was. <see cref="DistanceTo(GeoTriangle3, GeoPoint3)"/> measures a sliver where it is.
        /// </remarks>
        internal static double DistanceToByProjection(GeoTriangle3 triangle, GeoPoint3 point)
            => DistanceTo(Projection3.ProjectToTriangle(triangle, point), point);

        #endregion

        #region Line - Line, Ray

        /// <summary>
        /// Calculates the shortest distance between two line segments.
        /// </summary>
        public static double DistanceTo(GeoLine3 line1, GeoLine3 line2) => DistanceTo(line1, line2, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance between two line segments, within a tolerance.
        /// </summary>
        /// <remarks>
        /// Nought only where the two truly meet, otherwise the gap, measured exactly and the same whichever is asked first:
        /// two segments meeting end to end, one tilted by 8E-6 radian, are as far apart one way as the other. A segment no
        /// longer than the tolerance is read as its start where it does not truly meet the other. The segment from the origin
        /// to (60, 0, 0) and the one from (60.0004, 0, 0) to (100, 0, 0) are 0.0004 apart.
        /// </remarks>
        public static double DistanceTo(GeoLine3 line1, GeoLine3 line2, Tolerance tolerance)
            => Gap3.Segments(line1.StartPoint, line1.EndPoint, true, line2.StartPoint, line2.EndPoint, true, tolerance);

        /// <summary>
        /// Calculates the shortest distance between two line segments as the closest-point search of
        /// <see cref="Projection3.Closest(GeoLine3, GeoLine3, Tolerance)"/> finds it, a segment no longer than the tolerance read as its start.
        /// </summary>
        /// <remarks>
        /// This is what the search for the nearest triangles of two bodies and the collision of two triangles ask, which the
        /// clash reads and keeps as it was.
        /// </remarks>
        internal static double DistanceToByProjection(GeoLine3 line1, GeoLine3 line2, Tolerance tolerance)
            => Projection3.Closest(line1, line2, tolerance).Length;

        /// <summary>
        /// Calculates the shortest distance between a ray and a line segment.
        /// </summary>
        public static double DistanceTo(GeoRay3 ray, GeoLine3 line) => DistanceTo(ray, line, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance between a ray and a line segment, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The ray is taken as a segment as far as the segment's farther end stands from its origin, which is enough because
        /// the closest approach between a ray and a bounded segment can never lie beyond that point: past it, the ray is only
        /// moving away. Nought only where they truly meet, otherwise the gap, measured exactly: a ray that passes through
        /// the end of the segment is nought from it, however the pass is rounded. The ray from (59.9996, -0.0004, 0)
        /// along (1, 1, 0) passes through the end (60, 0, 0) of the segment from the origin, and is nought from it, and 0.0003 from it lifted by 0.0003.
        /// </remarks>
        public static double DistanceTo(GeoRay3 ray, GeoLine3 line, Tolerance tolerance)
        {
            double reach = Math.Max(ray.Origin.DistanceTo(line.StartPoint), ray.Origin.DistanceTo(line.EndPoint));

            return Gap3.Segments(
                ray.Origin, Gap3.RayEnd(ray, reach), false, line.StartPoint, line.EndPoint, true, tolerance);
        }

        #endregion

        #region Plane - Line, Plane

        /// <summary>
        /// Calculates the shortest distance from a plane to a line segment.
        /// </summary>
        /// <remarks>
        /// A segment with endpoints on opposite sides of the plane crosses it, so the distance is zero.
        /// Otherwise the nearest point is whichever endpoint sits closer.
        /// </remarks>
        public static double DistanceTo(GeoPlane3 plane, GeoLine3 line)
        {
            double start = plane.SignedDistanceTo(line.StartPoint);
            double end = plane.SignedDistanceTo(line.EndPoint);

            if (start * end <= 0.0)
            {
                return 0.0;
            }

            return Math.Min(Math.Abs(start), Math.Abs(end));
        }

        /// <summary>
        /// Calculates the distance between two planes.
        /// </summary>
        public static double DistanceTo(GeoPlane3 plane1, GeoPlane3 plane2) => DistanceTo(plane1, plane2, Tolerance.Global);

        /// <summary>
        /// Calculates the distance between two planes, within a tolerance.
        /// </summary>
        /// <remarks>
        /// Two planes that are not parallel meet somewhere, so their distance is zero. Parallel planes
        /// keep a constant gap, measured along the shared normal.
        /// </remarks>
        public static double DistanceTo(GeoPlane3 plane1, GeoPlane3 plane2, Tolerance tolerance)
        {
            if (!Parallel3.IsParallel(plane1, plane2, tolerance))
            {
                return 0.0;
            }

            return Math.Abs(plane1.SignedDistanceTo(plane2.Origin));
        }

        #endregion

        #region Point - regions and volumes

        /// <summary>
        /// Calculates the shortest distance from a polyline to a point.
        /// </summary>
        public static double DistanceTo(GeoPolyline3 polyline, GeoPoint3 point) => DistanceTo(polyline, point, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance from a polyline to a point, within a tolerance.
        /// </summary>
        /// <remarks>
        /// A polyline is a curve and holds only the points on its path, so a chain tracing a square is
        /// measured to that path and reports a positive distance for a point in the middle of it. Call
        /// <c>ToPolygon</c> when the enclosed area is what should count.
        /// </remarks>
        public static double DistanceTo(GeoPolyline3 polyline, GeoPoint3 point, Tolerance tolerance)
        {
            return DistanceTo(Projection3.ProjectToPolyline(polyline, point, tolerance), point);
        }

        /// <summary>
        /// Calculates the shortest distance from a polygon to a point.
        /// </summary>
        public static double DistanceTo(GeoPolygon3 polygon, GeoPoint3 point) => DistanceTo(polygon, point, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance from a polygon to a point, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The polygon counts as a filled surface, so a point above its interior is measured straight down
        /// and a point on the surface or its rim is at distance zero. Nothing widens the polygon: a point
        /// 0.0005 beside the square 100 by 100 in its plane is 0.0005 from it, and one 0.0004 under the plane
        /// and 0.0005 beside it is 0.00064.
        /// </remarks>
        public static double DistanceTo(GeoPolygon3 polygon, GeoPoint3 point, Tolerance tolerance)
        {
            if (polygon == null)
            {
                throw new ArgumentNullException(nameof(polygon));
            }

            return Gap3.Region.Of(polygon).PointGap(point);
        }

        /// <summary>
        /// Calculates the shortest distance from a face to a point.
        /// </summary>
        public static double DistanceTo(GeoFace3 face, GeoPoint3 point) => DistanceTo(face, point, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance from a face to a point, within a tolerance.
        /// </summary>
        /// <remarks>
        /// A hole is not material, so a point above a hole is measured out to the rim rather than down to
        /// the surface that is missing there. Nothing widens the face: a point in a hole 0.0005 from its rim is
        /// 0.0005 from the face, not nought.
        /// </remarks>
        public static double DistanceTo(GeoFace3 face, GeoPoint3 point, Tolerance tolerance)
        {
            if (face == null)
            {
                throw new ArgumentNullException(nameof(face));
            }

            return Gap3.Region.Of(face).PointGap(point);
        }

        /// <summary>
        /// Calculates the shortest distance from an oriented box to a point. A point inside the box is at
        /// distance zero.
        /// </summary>
        public static double DistanceTo(GeoObb3 box, GeoPoint3 point)
        {
            return DistanceTo(Projection3.ProjectToObb(box, point), point);
        }

        /// <summary>
        /// Calculates the shortest distance from a solid to a point. A point inside the solid is at
        /// distance zero.
        /// </summary>
        public static double DistanceTo(GeoSolid3 solid, GeoPoint3 point) => DistanceTo(solid, point, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance from a solid to a point, within a tolerance.
        /// </summary>
        /// <remarks>
        /// Nought for a point in the material or on its surface, otherwise the gap to the nearest face, openings cut in,
        /// measured exactly: a point 0.0005 outside the box 100 by 100 by 50, in the plane of its top, is 0.0005 from it.
        /// Whether a point is in the body is asked to within rounding, a billionth of a millimetre (1E-9, not nought),
        /// so that rounding in a body turned about in space cannot lose a crossing: a point within 1E-9 outside the
        /// body reads as in it, and a point 5E-10 outside a box is nought away from it. Further out than a million the
        /// billionth is 1E-15 of the largest coordinate of the body and the point, past the rounding of numbers that size.
        /// </remarks>
        public static double DistanceTo(GeoSolid3 solid, GeoPoint3 point, Tolerance tolerance)
        {
            if (solid == null)
            {
                throw new ArgumentNullException(nameof(solid));
            }

            if (Gap3.Holds(solid, point))
            {
                return 0.0;
            }

            // The faces run straight across every opening; the nearest material can sit on any opening's rim.
            return Gap3.SurfacePoint(Material3.Whole(solid, tolerance), point);
        }

        /// <summary>
        /// Calculates the shortest distance from a circular disc to a point.
        /// </summary>
        public static double DistanceTo(GeoCircle3 circle, GeoPoint3 point) => DistanceTo(circle, point, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance from a circular disc to a point, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The disc counts as a filled surface, so a point directly above the centre is measured straight
        /// down rather than out to the circumference.
        /// </remarks>
        public static double DistanceTo(GeoCircle3 circle, GeoPoint3 point, Tolerance tolerance)
        {
            return DistanceTo(Projection3.ProjectToDisc(circle, point, tolerance), point);
        }

        #endregion

        #region Shape - shape

        /// <summary>
        /// Calculates the perpendicular distance from an axis-aligned box to a point.
        /// </summary>
        public static double DistanceTo(GeoAabb3 box, GeoPoint3 point) => box.DistanceTo(point);

        /// <summary>
        /// Calculates the shortest distance between two axis-aligned boxes.
        /// </summary>
        public static double DistanceTo(GeoAabb3 box1, GeoAabb3 box2) => box1.DistanceTo(box2);

        /// <summary>
        /// Calculates the shortest distance between two triangles.
        /// </summary>
        public static double DistanceTo(GeoTriangle3 triangle1, GeoTriangle3 triangle2) => DistanceTo(triangle1, triangle2, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance between two triangles, within a tolerance.
        /// </summary>
        /// <remarks>
        /// Two triangles that touch or cross are at distance zero. Otherwise the closest pair of points
        /// lies either on a pair of edges or on a vertex of one and the face of the other — there is
        /// nowhere else it can be once the two no longer meet — so the gap is the least of those. It is
        /// measured exactly, nothing widening either triangle: two triangles in one plane 0.0005 apart are
        /// 0.0005 apart, and a triangle hovering 0.0003 over another is 0.0003 from it. A sliver is measured where it is: the foot of
        /// a point on a triangle is taken along its own normal, and the side tests that decide a crossing decide it.
        /// </remarks>
        public static double DistanceTo(GeoTriangle3 triangle1, GeoTriangle3 triangle2, Tolerance tolerance)
        {
            return Gap3.Regions(Gap3.Region.Of(triangle1), Gap3.Region.Of(triangle2), 0.0);
        }

        /// <summary>
        /// Calculates the shortest distance between two triangles as the surfaces of two bodies meet: nought wherever
        /// <see cref="Collision3.CollidesWith(GeoTriangle3, GeoTriangle3, Tolerance)"/> says they touch, which includes a
        /// pair within the point tolerance, and otherwise the gap.
        /// </summary>
        /// <remarks>
        /// The distance between two bodies is the least of the distances of their triangles, and a pair of bodies within the
        /// point tolerance of each other touch, so the search for the nearest triangles takes this reading.
        /// </remarks>
        internal static double DistanceToWithinBand(GeoTriangle3 triangle1, GeoTriangle3 triangle2, Tolerance tolerance)
        {
            if (Collision3.CollidesWith(triangle1, triangle2, tolerance))
            {
                return 0.0;
            }

            return Nearest(triangle1, triangle2, tolerance);
        }

        /// <summary>
        /// Gets the gap between two triangles that do not cross: the least of every edge of one against every edge of the
        /// other and every corner of one against the other.
        /// </summary>
        private static double Nearest(GeoTriangle3 triangle1, GeoTriangle3 triangle2, Tolerance tolerance)
        {
            double best = double.MaxValue;

            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    best = Math.Min(best, DistanceToByProjection(triangle1.GetEdgeAt(i), triangle2.GetEdgeAt(j), tolerance));
                }
            }

            for (int i = 0; i < 3; i++)
            {
                best = Math.Min(best, DistanceToByProjection(triangle2, triangle1[i]));
                best = Math.Min(best, DistanceToByProjection(triangle1, triangle2[i]));
            }

            return best;
        }

        /// <summary>
        /// Calculates the shortest distance between a line segment and a triangle.
        /// </summary>
        public static double DistanceTo(GeoLine3 line, GeoTriangle3 triangle) => DistanceTo(line, triangle, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance between a line segment and a triangle, within a tolerance.
        /// </summary>
        /// <remarks>
        /// Nought only where the segment truly meets the triangle: crosses it, ends on it, lies across it in its plane.
        /// Otherwise the gap, measured exactly and nothing widening the triangle: the segment from (-10, 10, 0.0004) to
        /// (110, 10, 0.0004), level over the triangle with corners (0, 0, 0), (100, 0, 0) and (0, 100, 0), is 0.0004 from
        /// it. A segment no longer than the point tolerance that does not pass through the triangle is read as its start.
        /// </remarks>
        public static double DistanceTo(GeoLine3 line, GeoTriangle3 triangle, Tolerance tolerance)
        {
            return Gap3.Segment(Gap3.Region.Of(triangle), line.StartPoint, line.EndPoint, true, tolerance, 0.0);
        }

        /// <summary>
        /// Calculates the shortest distance between a line segment and a polygon.
        /// </summary>
        public static double DistanceTo(GeoLine3 line, GeoPolygon3 polygon) => DistanceTo(line, polygon, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance between a line segment and a polygon, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The polygon counts as a filled surface, so a segment ending directly above its interior is
        /// measured straight down rather than out to the nearest edge. This is nought only where the segment truly
        /// meets the polygon, crossing it, ending on it or lying across it in its plane, and otherwise the gap,
        /// measured exactly and nothing widening the polygon: the segment from (100.0005, 50, 0) to (150, 50, 0) is
        /// 0.0005 from the square 100 by 100 in the floor with a corner at the origin, whichever way round it is
        /// asked, and one 0.0003 above it is 0.0003 from it. A segment no longer than the point tolerance that does
        /// not pass through the polygon is read as its start.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the polygon is null.</exception>
        public static double DistanceTo(GeoLine3 line, GeoPolygon3 polygon, Tolerance tolerance)
        {
            if (polygon == null)
            {
                throw new ArgumentNullException(nameof(polygon));
            }

            return Gap3.Segment(Gap3.Region.Of(polygon), line.StartPoint, line.EndPoint, true, tolerance, 0.0);
        }

        /// <summary>
        /// Calculates the shortest distance between a line segment and a solid.
        /// </summary>
        public static double DistanceTo(GeoLine3 line, GeoSolid3 solid) => DistanceTo(line, solid, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance between a line segment and a solid. A segment reaching into
        /// the body is at distance zero.
        /// </summary>
        /// <remarks>
        /// Whether a point is in the body is asked to within rounding, a billionth of a millimetre (1E-9, not nought),
        /// so that rounding in a body turned about in space cannot lose a crossing: a point within 1E-9 outside the
        /// body reads as in it, and a point 5E-10 outside a box is nought away from it. Further out than a million the
        /// billionth is 1E-15 of the largest coordinate of the body and the point, past the rounding of numbers that size.
        /// </remarks>
        public static double DistanceTo(GeoLine3 line, GeoSolid3 solid, Tolerance tolerance)
        {
            if (solid == null)
            {
                throw new ArgumentNullException(nameof(solid));
            }

            // The faces run straight across every opening; the nearest material can sit on any opening's rim.
            solid = Material3.Whole(solid, tolerance);

            double best = Gap3.SurfaceSegment(solid, line.StartPoint, line.EndPoint, true, tolerance, double.MaxValue, 0.0);

            if (best <= 0.0 || Gap3.Holds(solid, line.StartPoint) || Gap3.Holds(solid, line.EndPoint))
            {
                return 0.0;
            }

            return best;
        }

        /// <summary>
        /// Calculates the shortest distance between a solid and a triangle.
        /// </summary>
        public static double DistanceTo(GeoSolid3 solid, GeoTriangle3 triangle) => DistanceTo(solid, triangle, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance between a solid and a triangle, within a tolerance.
        /// </summary>
        /// <param name="solid">The solid.</param>
        /// <param name="triangle">The triangle.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>Zero where the triangle reaches into the body, otherwise the gap between the two surfaces.</returns>
        /// <remarks>
        /// A triangle lying wholly inside the body meets none of its faces, so it is caught by asking where
        /// its corners are before any face is measured. One that passes through is caught by the faces it
        /// cuts, which are at no distance from it.
        /// Whether a point is in the body is asked to within rounding, a billionth of a millimetre (1E-9, not nought),
        /// so that rounding in a body turned about in space cannot lose a crossing: a point within 1E-9 outside the
        /// body reads as in it, and a point 5E-10 outside a box is nought away from it. Further out than a million the
        /// billionth is 1E-15 of the largest coordinate of the body and the point, past the rounding of numbers that size.
        /// </remarks>
        public static double DistanceTo(GeoSolid3 solid, GeoTriangle3 triangle, Tolerance tolerance)
        {
            if (solid == null)
            {
                throw new ArgumentNullException(nameof(solid));
            }

            // The faces run straight across every opening; the nearest material can sit on any opening's rim.
            solid = Material3.Whole(solid, tolerance);

            double best = Gap3.SurfaceRegion(solid, Gap3.Region.Of(triangle), double.MaxValue, 0.0);

            if (best <= 0.0 || Gap3.Holds(solid, triangle.A) || Gap3.Holds(solid, triangle.B) || Gap3.Holds(solid, triangle.C))
            {
                return 0.0;
            }

            return best;
        }

        /// <summary>
        /// Calculates the shortest distance between a solid and a polygon.
        /// </summary>
        public static double DistanceTo(GeoSolid3 solid, GeoPolygon3 polygon) => DistanceTo(solid, polygon, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance between a solid and a polygon, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The polygon is cut into triangles and each is measured against the body, so a polygon that is
        /// not convex is handled like any other.
        /// Whether a point is in the body is asked to within rounding, a billionth of a millimetre (1E-9, not nought),
        /// so that rounding in a body turned about in space cannot lose a crossing: a point within 1E-9 outside the
        /// body reads as in it, and a point 5E-10 outside a box is nought away from it. Further out than a million the
        /// billionth is 1E-15 of the largest coordinate of the body and the point, past the rounding of numbers that size.
        /// </remarks>
        public static double DistanceTo(GeoSolid3 solid, GeoPolygon3 polygon, Tolerance tolerance)
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

            double best = double.MaxValue;

            // Triangles that each lie inside the polygon. The fan a polygon breaks into by default covers a
            // concave one only by signed sum, reaching across a notch with one triangle and taking it back
            // with another, so a body sitting in the notch of an L measured nought to it.
            foreach (GeoTriangle3 piece in new GeoFace3(polygon).TriangulateSurface(tolerance))
            {
                best = Math.Min(best, DistanceTo(solid, piece, tolerance));

                if (best <= 0.0)
                {
                    return 0.0;
                }
            }

            return best;
        }

        /// <summary>
        /// Calculates the shortest distance between a solid and a polyline.
        /// </summary>
        public static double DistanceTo(GeoSolid3 solid, GeoPolyline3 polyline) => DistanceTo(solid, polyline, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance between a solid and a polyline, within a tolerance.
        /// </summary>
        /// <remarks>
        /// Whether a point is in the body is asked to within rounding, a billionth of a millimetre (1E-9, not nought),
        /// so that rounding in a body turned about in space cannot lose a crossing: a point within 1E-9 outside the
        /// body reads as in it, and a point 5E-10 outside a box is nought away from it. Further out than a million the
        /// billionth is 1E-15 of the largest coordinate of the body and the point, past the rounding of numbers that size.
        /// </remarks>
        public static double DistanceTo(GeoSolid3 solid, GeoPolyline3 polyline, Tolerance tolerance)
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

            double best = double.MaxValue;

            foreach (GeoLine3 edge in polyline.GetEdges())
            {
                best = Math.Min(best, DistanceTo(edge, solid, tolerance));

                if (best <= 0.0)
                {
                    return 0.0;
                }
            }

            return best;
        }

        /// <summary>
        /// Calculates the shortest distance between a solid and a plane.
        /// </summary>
        public static double DistanceTo(GeoSolid3 solid, GeoPlane3 plane) => DistanceTo(solid, plane, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance between a solid and a plane, within a tolerance.
        /// </summary>
        /// <returns>Zero where the body reaches the plane or crosses it, otherwise the gap.</returns>
        /// <remarks>
        /// A plane is flat and endless, so nothing needs measuring but the corners of the body: the nearest
        /// point of a solid to a plane is always one of them. The body reaches the plane where one of its
        /// faces does, the corners of that face changing side or one of them lying on it within the point
        /// tolerance, as <see cref="Collision3.CollidesWith(GeoPlane3, GeoSolid3, Tolerance)"/> reads it;
        /// asked over all the corners at once, a body in two pieces either side would read as crossing. The
        /// boxes 40 by 40 from z = 10 to 20 and from z = -20 to -10, read as one body, are 10 from the plane
        /// z = 0, and so is the box from z = -20 to 20 with an opening from z = -10 to 10 cut right across it.
        /// </remarks>
        public static double DistanceTo(GeoSolid3 solid, GeoPlane3 plane, Tolerance tolerance)
        {
            if (solid == null)
            {
                throw new ArgumentNullException(nameof(solid));
            }

            // The faces run straight across every opening; the nearest material can sit on any opening's rim.
            solid = Material3.Whole(solid, tolerance);

            double nearest = double.MaxValue;

            foreach (GeoFace3 face in solid.Faces)
            {
                double above = double.MinValue;
                double below = double.MaxValue;

                foreach (GeoPoint3 corner in face.Boundary.Vertices)
                {
                    double reach = plane.SignedDistanceTo(corner);

                    above = Math.Max(above, reach);
                    below = Math.Min(below, reach);
                }

                // A face straddling the plane, or touching it, leaves nothing between them.
                if (above >= -tolerance.EqualPoint && below <= tolerance.EqualPoint)
                {
                    return 0.0;
                }

                nearest = Math.Min(nearest, below > 0.0 ? below : -above);
            }

            return nearest == double.MaxValue ? 0.0 : nearest;
        }

        /// <summary>
        /// Calculates the shortest distance between a solid and an oriented box.
        /// </summary>
        public static double DistanceTo(GeoSolid3 solid, GeoObb3 box) => DistanceTo(solid, box, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance between a solid and an oriented box, within a tolerance.
        /// </summary>
        /// <remarks>
        /// A box is a solid of six faces, so the two are measured against each other as two solids are.
        /// </remarks>
        public static double DistanceTo(GeoSolid3 solid, GeoObb3 box, Tolerance tolerance)
        {
            return DistanceTo(solid, box.ToSolid(), tolerance);
        }

        /// <summary>
        /// Calculates the shortest distance between a solid and an axis-aligned box.
        /// </summary>
        public static double DistanceTo(GeoSolid3 solid, GeoAabb3 box) => DistanceTo(solid, box, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance between a solid and an axis-aligned box, within a tolerance.
        /// </summary>
        public static double DistanceTo(GeoSolid3 solid, GeoAabb3 box, Tolerance tolerance)
        {
            return DistanceTo(solid, box.ToObb().ToSolid(), tolerance);
        }

        /// <summary>
        /// Calculates the shortest distance between two solids.
        /// </summary>
        public static double DistanceTo(GeoSolid3 solid1, GeoSolid3 solid2) => DistanceTo(solid1, solid2, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance between two solids, within a tolerance.
        /// </summary>
        /// <remarks>
        /// Bodies that touch or overlap are at distance zero, which includes one sitting wholly inside the
        /// other without their surfaces meeting. Otherwise both surfaces are indexed and walked as a pair
        /// of trees, so a pair of boxes farther apart than the closest triangles found so far discards
        /// everything beneath it. Two bodies within the point tolerance of each other are nought apart by this search, as they
        /// collide: unlike a point or a straight shape against a body, which is the exact gap, a body against a body still reads
        /// the band, so two solid boxes 0.0004 apart are nought apart.
        /// </remarks>
        public static double DistanceTo(GeoSolid3 solid1, GeoSolid3 solid2, Tolerance tolerance)
        {
            if (solid1 == null)
            {
                throw new ArgumentNullException(nameof(solid1));
            }

            if (solid2 == null)
            {
                throw new ArgumentNullException(nameof(solid2));
            }

            // The faces run straight across every opening; the nearest material can sit on any opening's rim.
            solid1 = Material3.Whole(solid1, tolerance);
            solid2 = Material3.Whole(solid2, tolerance);

            if (Collision3.CollidesWith(solid1, solid2, tolerance))
            {
                return 0.0;
            }

            return new Spatial.GeoBvh3(solid1.TriangulateQuietly(tolerance)).DistanceTo(new Spatial.GeoBvh3(solid2.TriangulateQuietly(tolerance)), tolerance);
        }

        /// <summary>
        /// Calculates the shortest distance between two oriented boxes.
        /// </summary>
        public static double DistanceTo(GeoObb3 box1, GeoObb3 box2) => DistanceTo(box1, box2, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance between two oriented boxes, within a tolerance.
        /// </summary>
        /// <remarks>
        /// Boxes that overlap or touch are at distance zero, which the separating axis test settles
        /// outright. Apart, they are measured as the bodies they are. Oriented boxes within the point tolerance of each other are nought
        /// apart, as for two solids: the exact gap is not read between bodies.
        /// </remarks>
        public static double DistanceTo(GeoObb3 box1, GeoObb3 box2, Tolerance tolerance)
        {
            if (box1 == null)
            {
                throw new ArgumentNullException(nameof(box1));
            }

            if (box2 == null)
            {
                throw new ArgumentNullException(nameof(box2));
            }

            if (Collision3.CollidesWith(box1, box2, tolerance))
            {
                return 0.0;
            }

            return DistanceTo(box1.ToSolid(), box2.ToSolid(), tolerance);
        }

        #endregion
    }
}
