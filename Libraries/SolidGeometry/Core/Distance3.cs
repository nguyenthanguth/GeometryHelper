using System;
using CommonGeometry;
using CommonGeometry.Enums;
using SolidGeometry.Geometry;

namespace SolidGeometry.Core
{
    /// <summary>
    /// Provides static methods for distance calculations in 3D space.
    /// <para>
    /// Every distance here is the shortest gap between the two shapes, and is zero when they touch or
    /// overlap. A bounded shape is measured as the set of points it actually occupies: a segment ends at
    /// its endpoints, a triangle includes its interior, and a plane is infinite in every direction.
    /// </para>
    /// </summary>
    public static class Distance3
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
        /// distance zero.
        /// </remarks>
        public static double DistanceTo(GeoTriangle3 triangle, GeoPoint3 point)
        {
            return DistanceTo(Projection3.ProjectToTriangle(triangle, point), point);
        }

        #endregion

        #region Line - Line, Ray

        /// <summary>
        /// Calculates the shortest distance between two line segments.
        /// </summary>
        public static double DistanceTo(GeoLine3 line1, GeoLine3 line2) => DistanceTo(line1, line2, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance between two line segments, within a tolerance.
        /// </summary>
        public static double DistanceTo(GeoLine3 line1, GeoLine3 line2, Tolerance tolerance)
        {
            return Projection3.GetClosestSegment(line1, line2, tolerance).Length;
        }

        /// <summary>
        /// Calculates the shortest distance between a ray and a line segment.
        /// </summary>
        public static double DistanceTo(GeoRay3 ray, GeoLine3 line) => DistanceTo(ray, line, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance between a ray and a line segment, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The ray is sampled as a segment long enough to reach past the far end of the other segment,
        /// which is enough because the closest approach between a ray and a bounded segment can never lie
        /// beyond that point: past it, the ray is only moving away.
        /// </remarks>
        public static double DistanceTo(GeoRay3 ray, GeoLine3 line, Tolerance tolerance)
        {
            double reach = Math.Max(
                Parametrization3.GetDistanceAtPoint(ray, line.StartPoint),
                Parametrization3.GetDistanceAtPoint(ray, line.EndPoint));

            GeoLine3 sample = ray.ToLine(Math.Max(0.0, reach));

            return DistanceTo(sample, line, tolerance);
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
        /// and a point on the surface is at distance zero.
        /// </remarks>
        public static double DistanceTo(GeoPolygon3 polygon, GeoPoint3 point, Tolerance tolerance)
        {
            return DistanceTo(Projection3.ProjectToPolygon(polygon, point, tolerance), point);
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
        /// the surface that is missing there.
        /// </remarks>
        public static double DistanceTo(GeoFace3 face, GeoPoint3 point, Tolerance tolerance)
        {
            if (face == null)
            {
                throw new ArgumentNullException(nameof(face));
            }

            GeoPoint3 flat = Projection3.ProjectToPlane(face.GetPlane(), point);

            if (Containment3.Contains(face.Boundary, flat, tolerance))
            {
                foreach (GeoPolygon3 hole in face.Holes)
                {
                    if (Containment3.Locate(hole, flat, tolerance) == PointLocation.Inside)
                    {
                        return DistanceTo(Projection3.ProjectToPolygonBoundary(hole, point, tolerance), point);
                    }
                }

                return DistanceTo(flat, point);
            }

            return DistanceTo(Projection3.ProjectToPolygonBoundary(face.Boundary, point, tolerance), point);
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
        public static double DistanceTo(GeoSolid3 solid, GeoPoint3 point, Tolerance tolerance)
        {
            if (Containment3.Contains(solid, point, tolerance))
            {
                return 0.0;
            }

            return DistanceTo(Projection3.ProjectToSolid(solid, point, tolerance), point);
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
        /// nowhere else it can be once the two no longer meet — so trying those cases covers it.
        /// </remarks>
        public static double DistanceTo(GeoTriangle3 triangle1, GeoTriangle3 triangle2, Tolerance tolerance)
        {
            if (Collision3.CollidesWith(triangle1, triangle2, tolerance))
            {
                return 0.0;
            }

            double best = double.MaxValue;

            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    best = Math.Min(best, DistanceTo(triangle1.GetEdgeAt(i), triangle2.GetEdgeAt(j), tolerance));
                }
            }

            for (int i = 0; i < 3; i++)
            {
                best = Math.Min(best, DistanceTo(triangle2, triangle1[i]));
                best = Math.Min(best, DistanceTo(triangle1, triangle2[i]));
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
        public static double DistanceTo(GeoLine3 line, GeoTriangle3 triangle, Tolerance tolerance)
        {
            if (Intersection3.TryIntersectWith(line, triangle, out _, tolerance))
            {
                return 0.0;
            }

            double best = Math.Min(DistanceTo(triangle, line.StartPoint), DistanceTo(triangle, line.EndPoint));

            for (int i = 0; i < 3; i++)
            {
                best = Math.Min(best, DistanceTo(line, triangle.GetEdgeAt(i), tolerance));
            }

            return best;
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
        /// measured straight down rather than out to the nearest edge.
        /// </remarks>
        public static double DistanceTo(GeoLine3 line, GeoPolygon3 polygon, Tolerance tolerance)
        {
            if (polygon == null)
            {
                throw new ArgumentNullException(nameof(polygon));
            }

            if (Intersection3.TryIntersectWith(line, polygon, out _, tolerance))
            {
                return 0.0;
            }

            double best = Math.Min(DistanceTo(polygon, line.StartPoint, tolerance), DistanceTo(polygon, line.EndPoint, tolerance));

            for (int i = 0; i < polygon.EdgeCount; i++)
            {
                best = Math.Min(best, DistanceTo(line, polygon.GetEdgeAt(i), tolerance));
            }

            return best;
        }

        /// <summary>
        /// Calculates the shortest distance between a line segment and a solid.
        /// </summary>
        public static double DistanceTo(GeoLine3 line, GeoSolid3 solid) => DistanceTo(line, solid, Tolerance.Global);

        /// <summary>
        /// Calculates the shortest distance between a line segment and a solid. A segment reaching into
        /// the body is at distance zero.
        /// </summary>
        public static double DistanceTo(GeoLine3 line, GeoSolid3 solid, Tolerance tolerance)
        {
            if (solid == null)
            {
                throw new ArgumentNullException(nameof(solid));
            }

            if (Containment3.Contains(solid, line.StartPoint, tolerance) ||
                Containment3.Contains(solid, line.EndPoint, tolerance))
            {
                return 0.0;
            }

            double best = double.MaxValue;

            foreach (GeoTriangle3 triangle in solid.Triangulate())
            {
                best = Math.Min(best, DistanceTo(line, triangle, tolerance));

                if (best <= 0.0)
                {
                    return 0.0;
                }
            }

            return best;
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
        /// everything beneath it.
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

            if (Collision3.CollidesWith(solid1, solid2, tolerance))
            {
                return 0.0;
            }

            return new Spatial.GeoBvh3(solid1.Triangulate()).DistanceTo(new Spatial.GeoBvh3(solid2.Triangulate()), tolerance);
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
        /// outright. Apart, they are measured as the bodies they are.
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
