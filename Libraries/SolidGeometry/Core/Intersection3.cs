using System;
using System.Collections.Generic;
using CommonGeometry;
using SolidGeometry.Geometry;

namespace SolidGeometry.Core
{
    /// <summary>
    /// Provides static methods for finding where 3D shapes meet.
    /// <para>
    /// Each <c>TryIntersectWith</c> overload returns a single result and reports false when there is not
    /// exactly one. Two shapes that overlap along a whole line or a whole area — a segment lying in a
    /// plane, two coincident planes, two collinear segments — have no single crossing to name, so they
    /// come back false rather than picking an arbitrary point out of the overlap. Ask <c>Distance3</c>
    /// or <c>Collision3</c> when the question is whether they touch at all.
    /// </para>
    /// </summary>
    public static class Intersection3
    {
        #region Line - Line

        /// <summary>
        /// Tries to find the point where two line segments meet, using the default tolerance.
        /// </summary>
        public static bool TryIntersectWith(GeoLine3 line1, GeoLine3 line2, out GeoPoint3 intersection)
        {
            return TryIntersectWith(line1, line2, out intersection, Tolerance.Global);
        }

        /// <summary>
        /// Tries to find the point where two line segments meet, within a tolerance.
        /// </summary>
        /// <param name="line1">The first segment.</param>
        /// <param name="line2">The second segment.</param>
        /// <param name="intersection">The crossing point when the method returns true.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the segments meet at a single point; otherwise, false.</returns>
        /// <remarks>
        /// Two segments in space usually pass each other without meeting, so this asks for the closest
        /// approach and accepts it only when the gap is within tolerance. Parallel segments are refused
        /// even when they overlap, matching the 2D behaviour: a shared stretch is not a point.
        /// </remarks>
        public static bool TryIntersectWith(GeoLine3 line1, GeoLine3 line2, out GeoPoint3 intersection, Tolerance tolerance)
        {
            intersection = GeoPoint3.Origin;

            if (line1.IsDegenerate(tolerance) || line2.IsDegenerate(tolerance))
            {
                return false;
            }

            if (Parallel3.IsParallel(line1, line2, tolerance))
            {
                return false;
            }

            GeoLine3 bridge = Projection3.GetClosestSegment(line1, line2, tolerance);

            if (bridge.Length > tolerance.EqualPoint)
            {
                return false;
            }

            intersection = bridge.MidPoint;
            return true;
        }

        /// <summary>
        /// Gets the point where two line segments meet, or null when they do not, using the default tolerance.
        /// </summary>
        public static GeoPoint3? GetIntersection(GeoLine3 line1, GeoLine3 line2)
        {
            return GetIntersection(line1, line2, Tolerance.Global);
        }

        /// <summary>
        /// Gets the point where two line segments meet, or null when they do not, within a tolerance.
        /// </summary>
        public static GeoPoint3? GetIntersection(GeoLine3 line1, GeoLine3 line2, Tolerance tolerance)
        {
            return TryIntersectWith(line1, line2, out GeoPoint3 point, tolerance) ? point : (GeoPoint3?)null;
        }

        #endregion

        #region Line - Plane

        /// <summary>
        /// Tries to find the point where a line segment crosses a plane, using the default tolerance.
        /// </summary>
        public static bool TryIntersectWith(GeoLine3 line, GeoPlane3 plane, out GeoPoint3 intersection)
        {
            return TryIntersectWith(line, plane, out intersection, Tolerance.Global);
        }

        /// <summary>
        /// Tries to find the point where a line segment crosses a plane, within a tolerance.
        /// </summary>
        /// <remarks>
        /// A segment running parallel to the plane is refused whether it lies in the plane or beside it:
        /// in the first case every one of its points is a crossing, in the second none is, and neither
        /// gives the single point this method promises.
        /// </remarks>
        public static bool TryIntersectWith(GeoLine3 line, GeoPlane3 plane, out GeoPoint3 intersection, Tolerance tolerance)
        {
            intersection = GeoPoint3.Origin;

            GeoVector3 direction = line.Direction;
            double length = direction.Length;

            if (length <= tolerance.EqualPoint)
            {
                return false;
            }

            double denominator = direction.DotProduct(plane.Normal);

            // The normal is a unit vector, so dividing by the segment length turns this into the sine of
            // the angle between the segment and the plane. Comparing that against the angular threshold
            // keeps the parallel test independent of how long the segment is, and agrees with what
            // Parallel3.IsParallel reports for the same pair.
            if (Math.Abs(denominator) <= tolerance.EqualAngleSin * length)
            {
                return false;
            }

            double parameter = line.StartPoint.GetVectorTo(plane.Origin).DotProduct(plane.Normal) / denominator;

            // The parameter is dimensionless, so the slack allowed past an endpoint has to be converted
            // from a distance into parameter space; using EqualPoint directly would let a very long
            // segment reach far beyond its own endpoint.
            double slack = tolerance.EqualPoint / length;

            if (parameter < -slack || parameter > 1.0 + slack)
            {
                return false;
            }

            intersection = Parametrization3.GetPointAtParameter(line, Math.Max(0.0, Math.Min(1.0, parameter)));
            return true;
        }

        /// <summary>
        /// Tries to find the point where a ray crosses a plane, using the default tolerance.
        /// </summary>
        public static bool TryIntersectWith(GeoRay3 ray, GeoPlane3 plane, out GeoPoint3 intersection)
        {
            return TryIntersectWith(ray, plane, out intersection, Tolerance.Global);
        }

        /// <summary>
        /// Tries to find the point where a ray crosses a plane, within a tolerance.
        /// </summary>
        public static bool TryIntersectWith(GeoRay3 ray, GeoPlane3 plane, out GeoPoint3 intersection, Tolerance tolerance)
        {
            intersection = GeoPoint3.Origin;

            double denominator = ray.Direction.DotProduct(plane.Normal);

            // Both vectors are unit length here, so the dot product is already the sine of the angle
            // between the ray and the plane and needs no scaling.
            if (Math.Abs(denominator) <= tolerance.EqualAngleSin)
            {
                return false;
            }

            double distance = ray.Origin.GetVectorTo(plane.Origin).DotProduct(plane.Normal) / denominator;

            if (distance < -tolerance.EqualPoint)
            {
                return false;
            }

            intersection = Parametrization3.GetPointAtDistance(ray, Math.Max(0.0, distance));
            return true;
        }

        #endregion

        #region Line - Triangle

        /// <summary>
        /// Tries to find the point where a line segment crosses a triangle, using the default tolerance.
        /// </summary>
        public static bool TryIntersectWith(GeoLine3 line, GeoTriangle3 triangle, out GeoPoint3 intersection)
        {
            return TryIntersectWith(line, triangle, out intersection, Tolerance.Global);
        }

        /// <summary>
        /// Tries to find the point where a line segment crosses a triangle, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The crossing is found against the triangle's carrier plane and then tested for containment, so
        /// the edges of the triangle are decided by the same tolerance-aware test that
        /// <c>Containment3.Contains</c> uses. A segment lying in the plane of the triangle is refused for
        /// the same reason it is refused against a bare plane.
        /// </remarks>
        public static bool TryIntersectWith(GeoLine3 line, GeoTriangle3 triangle, out GeoPoint3 intersection, Tolerance tolerance)
        {
            intersection = GeoPoint3.Origin;

            if (triangle.IsDegenerate(tolerance))
            {
                return false;
            }

            if (!TryIntersectWith(line, triangle.GetPlane(), out GeoPoint3 candidate, tolerance))
            {
                return false;
            }

            if (!Containment3.Contains(triangle, candidate, tolerance))
            {
                return false;
            }

            intersection = candidate;
            return true;
        }

        /// <summary>
        /// Tries to find the point where a ray crosses a triangle, using the default tolerance.
        /// </summary>
        public static bool TryIntersectWith(GeoRay3 ray, GeoTriangle3 triangle, out GeoPoint3 intersection)
        {
            return TryIntersectWith(ray, triangle, out intersection, Tolerance.Global);
        }

        /// <summary>
        /// Tries to find the point where a ray crosses a triangle, within a tolerance.
        /// </summary>
        public static bool TryIntersectWith(GeoRay3 ray, GeoTriangle3 triangle, out GeoPoint3 intersection, Tolerance tolerance)
        {
            intersection = GeoPoint3.Origin;

            if (triangle.IsDegenerate(tolerance))
            {
                return false;
            }

            if (!TryIntersectWith(ray, triangle.GetPlane(), out GeoPoint3 candidate, tolerance))
            {
                return false;
            }

            if (!Containment3.Contains(triangle, candidate, tolerance))
            {
                return false;
            }

            intersection = candidate;
            return true;
        }

        #endregion

        #region Plane - Plane

        /// <summary>
        /// Tries to find the line where two planes meet, using the default tolerance.
        /// </summary>
        public static bool TryIntersectWith(GeoPlane3 plane1, GeoPlane3 plane2, out GeoRay3 intersection)
        {
            return TryIntersectWith(plane1, plane2, out intersection, Tolerance.Global);
        }

        /// <summary>
        /// Tries to find the line where two planes meet, within a tolerance.
        /// </summary>
        /// <param name="plane1">The first plane.</param>
        /// <param name="plane2">The second plane.</param>
        /// <param name="intersection">
        /// A ray along the line of intersection when the method returns true. The line is infinite in both
        /// directions and the ray only names it: <c>GetPointAtDistance</c> takes negative distances, so
        /// the half behind the origin is reachable too.
        /// </param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the planes meet in a line; otherwise, false.</returns>
        /// <remarks>
        /// Parallel planes are refused whether they are apart, in which case they never meet, or
        /// coincident, in which case they meet everywhere. The origin of the returned ray is the point of
        /// the intersection line closest to the world origin, which makes the result depend only on the
        /// two planes and not on where their own origins happen to sit.
        /// </remarks>
        public static bool TryIntersectWith(GeoPlane3 plane1, GeoPlane3 plane2, out GeoRay3 intersection, Tolerance tolerance)
        {
            intersection = default;

            GeoVector3 n1 = plane1.Normal;
            GeoVector3 n2 = plane2.Normal;
            GeoVector3 direction = n1.CrossProduct(n2);

            // Both normals are unit vectors, so the length of their cross product is the sine of the
            // angle between the planes.
            if (direction.Length <= tolerance.EqualAngleSin)
            {
                return false;
            }

            double d1 = plane1.DistanceFromWorldOrigin;
            double d2 = plane2.DistanceFromWorldOrigin;
            double n1Dotn2 = n1.DotProduct(n2);
            double denominator = 1.0 - n1Dotn2 * n1Dotn2;

            double c1 = (d1 - d2 * n1Dotn2) / denominator;
            double c2 = (d2 - d1 * n1Dotn2) / denominator;

            GeoPoint3 origin = GeoPoint3.Origin.Add(n1.Multiply(c1)).Add(n2.Multiply(c2));

            intersection = new GeoRay3(origin, direction);
            return true;
        }

        #endregion

        #region Line - Polygon

        /// <summary>
        /// Tries to find the point where a line segment crosses a polygon, using the default tolerance.
        /// </summary>
        public static bool TryIntersectWith(GeoLine3 line, GeoPolygon3 polygon, out GeoPoint3 intersection)
        {
            return TryIntersectWith(line, polygon, out intersection, Tolerance.Global);
        }

        /// <summary>
        /// Tries to find the point where a line segment crosses a polygon, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The crossing is found against the polygon's carrier plane and then tested for containment, so
        /// the boundary is decided by the same tolerance-aware test <c>Containment3.Contains</c> uses. A
        /// segment lying in the plane of the polygon is refused, as it is against a bare plane: every one
        /// of its points inside the outline would be a crossing.
        /// </remarks>
        public static bool TryIntersectWith(GeoLine3 line, GeoPolygon3 polygon, out GeoPoint3 intersection, Tolerance tolerance)
        {
            intersection = GeoPoint3.Origin;

            if (polygon == null)
            {
                throw new ArgumentNullException(nameof(polygon));
            }

            if (!TryIntersectWith(line, polygon.GetPlane(), out GeoPoint3 candidate, tolerance))
            {
                return false;
            }

            if (!Containment3.Contains(polygon, candidate, tolerance))
            {
                return false;
            }

            intersection = candidate;
            return true;
        }

        /// <summary>
        /// Tries to find the point where a ray crosses a polygon, using the default tolerance.
        /// </summary>
        public static bool TryIntersectWith(GeoRay3 ray, GeoPolygon3 polygon, out GeoPoint3 intersection)
        {
            return TryIntersectWith(ray, polygon, out intersection, Tolerance.Global);
        }

        /// <summary>
        /// Tries to find the point where a ray crosses a polygon, within a tolerance.
        /// </summary>
        public static bool TryIntersectWith(GeoRay3 ray, GeoPolygon3 polygon, out GeoPoint3 intersection, Tolerance tolerance)
        {
            intersection = GeoPoint3.Origin;

            if (polygon == null)
            {
                throw new ArgumentNullException(nameof(polygon));
            }

            if (!TryIntersectWith(ray, polygon.GetPlane(), out GeoPoint3 candidate, tolerance))
            {
                return false;
            }

            if (!Containment3.Contains(polygon, candidate, tolerance))
            {
                return false;
            }

            intersection = candidate;
            return true;
        }

        #endregion

        #region Line - Box

        /// <summary>
        /// Finds where a line segment enters and leaves an oriented box, using the default tolerance.
        /// </summary>
        public static GeoPoint3[] GetIntersections(GeoLine3 line, GeoObb3 box) => GetIntersections(line, box, Tolerance.Global);

        /// <summary>
        /// Finds where a line segment enters and leaves an oriented box, within a tolerance.
        /// </summary>
        /// <param name="line">The line segment.</param>
        /// <param name="box">The box.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>
        /// The crossing points in order along the segment: none when it misses the box, one when it starts
        /// or ends inside, two when it passes through.
        /// </returns>
        /// <remarks>
        /// This is the slab method. Taking the segment into the local frame of the box turns it into three
        /// pairs of parallel planes, and the part of the segment inside the box is the overlap of the three
        /// intervals those pairs cut out of it. The overlap is empty exactly when some axis separates them,
        /// which is the same reasoning the separating axis test in <c>Collision3</c> rests on.
        /// </remarks>
        public static GeoPoint3[] GetIntersections(GeoLine3 line, GeoObb3 box, Tolerance tolerance)
        {
            if (box == null)
            {
                throw new ArgumentNullException(nameof(box));
            }

            GeoPoint3 start = box.CoordinateSystem.ToLocal(line.StartPoint);
            GeoVector3 direction = box.CoordinateSystem.ToLocal(line.Direction);

            double enter = 0.0;
            double exit = 1.0;

            double[] origins = { start.X, start.Y, start.Z };
            double[] steps = { direction.X, direction.Y, direction.Z };
            double[] extents = { box.ExtentX, box.ExtentY, box.ExtentZ };

            for (int axis = 0; axis < 3; axis++)
            {
                double step = steps[axis];
                double origin = origins[axis];
                double extent = extents[axis];

                if (Math.Abs(step) <= tolerance.EqualPoint)
                {
                    // The segment does not move along this axis, so the slab either holds all of it or
                    // none of it.
                    if (Math.Abs(origin) > extent + tolerance.EqualPoint)
                    {
                        return new GeoPoint3[0];
                    }

                    continue;
                }

                double t1 = (-extent - origin) / step;
                double t2 = (extent - origin) / step;

                if (t1 > t2)
                {
                    double swap = t1;
                    t1 = t2;
                    t2 = swap;
                }

                enter = Math.Max(enter, t1);
                exit = Math.Min(exit, t2);

                if (enter > exit)
                {
                    return new GeoPoint3[0];
                }
            }

            GeoPoint3 entryPoint = Parametrization3.GetPointAtParameter(line, enter);
            GeoPoint3 exitPoint = Parametrization3.GetPointAtParameter(line, exit);

            if (entryPoint.IsEqualTo(exitPoint, tolerance))
            {
                return new[] { entryPoint };
            }

            List<GeoPoint3> hits = new List<GeoPoint3>();

            // A crossing is only reported where the segment actually pierces a face. An endpoint sitting
            // inside the box is a place the segment stops, not a place it crosses the surface.
            if (enter > 0.0)
            {
                hits.Add(entryPoint);
            }

            if (exit < 1.0)
            {
                hits.Add(exitPoint);
            }

            return hits.ToArray();
        }

        #endregion

        #region Plane - Solid

        /// <summary>
        /// Finds where a plane cuts the edges of a solid, using the default tolerance.
        /// </summary>
        public static GeoPoint3[] GetIntersections(GeoPlane3 plane, GeoSolid3 solid) => GetIntersections(plane, solid, Tolerance.Global);

        /// <summary>
        /// Finds where a plane cuts the edges of a solid, within a tolerance.
        /// </summary>
        /// <param name="plane">The cutting plane.</param>
        /// <param name="solid">The solid.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The crossing points, with duplicates from shared edges removed.</returns>
        /// <remarks>
        /// These are the corners of the section the plane cuts, but not the section itself: they come back
        /// as a bag of points with no ordering, because ordering them into a loop needs the face adjacency
        /// that this method does not look at. <c>Splition3</c> is where that belongs.
        /// </remarks>
        public static GeoPoint3[] GetIntersections(GeoPlane3 plane, GeoSolid3 solid, Tolerance tolerance)
        {
            if (solid == null)
            {
                throw new ArgumentNullException(nameof(solid));
            }

            List<GeoPoint3> hits = new List<GeoPoint3>();

            foreach (GeoFace3 face in solid.Faces)
            {
                for (int i = 0; i < face.Boundary.EdgeCount; i++)
                {
                    if (!TryIntersectWith(face.Boundary.GetEdgeAt(i), plane, out GeoPoint3 hit, tolerance))
                    {
                        continue;
                    }

                    bool alreadyFound = false;

                    foreach (GeoPoint3 existing in hits)
                    {
                        if (existing.IsEqualTo(hit, tolerance))
                        {
                            alreadyFound = true;
                            break;
                        }
                    }

                    if (!alreadyFound)
                    {
                        hits.Add(hit);
                    }
                }
            }

            return hits.ToArray();
        }

        #endregion

        #region Face, solid and axis-aligned box

        /// <summary>
        /// Tries to find the point where a line segment crosses a face, using the default tolerance.
        /// </summary>
        public static bool TryIntersectWith(GeoLine3 line, GeoFace3 face, out GeoPoint3 intersection)
        {
            return TryIntersectWith(line, face, out intersection, Tolerance.Global);
        }

        /// <summary>
        /// Tries to find the point where a line segment crosses a face, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The holes in the face are respected: a segment threading through a hole passes through nothing
        /// and is reported as missing the face.
        /// </remarks>
        public static bool TryIntersectWith(GeoLine3 line, GeoFace3 face, out GeoPoint3 intersection, Tolerance tolerance)
        {
            intersection = GeoPoint3.Origin;

            if (face == null)
            {
                throw new ArgumentNullException(nameof(face));
            }

            if (!TryIntersectWith(line, face.GetPlane(), out GeoPoint3 candidate, tolerance))
            {
                return false;
            }

            if (!face.Contains(candidate, tolerance))
            {
                return false;
            }

            intersection = candidate;
            return true;
        }

        /// <summary>
        /// Tries to find the point where a ray crosses a face, using the default tolerance.
        /// </summary>
        public static bool TryIntersectWith(GeoRay3 ray, GeoFace3 face, out GeoPoint3 intersection)
        {
            return TryIntersectWith(ray, face, out intersection, Tolerance.Global);
        }

        /// <summary>
        /// Tries to find the point where a ray crosses a face, within a tolerance.
        /// </summary>
        public static bool TryIntersectWith(GeoRay3 ray, GeoFace3 face, out GeoPoint3 intersection, Tolerance tolerance)
        {
            intersection = GeoPoint3.Origin;

            if (face == null)
            {
                throw new ArgumentNullException(nameof(face));
            }

            if (!TryIntersectWith(ray, face.GetPlane(), out GeoPoint3 candidate, tolerance))
            {
                return false;
            }

            if (!face.Contains(candidate, tolerance))
            {
                return false;
            }

            intersection = candidate;
            return true;
        }

        /// <summary>
        /// Finds every point where a line segment crosses the surface of a solid, using the default tolerance.
        /// </summary>
        public static GeoPoint3[] GetIntersections(GeoLine3 line, GeoSolid3 solid) => GetIntersections(line, solid, Tolerance.Global);

        /// <summary>
        /// Finds every point where a line segment crosses the surface of a solid, within a tolerance.
        /// </summary>
        /// <param name="line">The segment.</param>
        /// <param name="solid">The body.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>The crossing points, in order along the segment.</returns>
        /// <remarks>
        /// The walls of the openings count as surface, since they too separate material from void. Two
        /// crossings closer together than the tolerance are reported once, so a segment grazing an edge
        /// does not come back as having entered and left.
        /// </remarks>
        public static GeoPoint3[] GetIntersections(GeoLine3 line, GeoSolid3 solid, Tolerance tolerance)
        {
            if (solid == null)
            {
                throw new ArgumentNullException(nameof(solid));
            }

            List<double> distances = new List<double>();
            CollectSolidCrossings(line, solid, tolerance, distances);

            distances.Sort();

            List<GeoPoint3> hits = new List<GeoPoint3>();

            foreach (double distance in distances)
            {
                if (hits.Count > 0 && distance - Parametrization3.GetDistanceAtPoint(line, hits[hits.Count - 1]) <= tolerance.EqualPoint)
                {
                    continue;
                }

                hits.Add(Parametrization3.GetPointAtDistance(line, distance));
            }

            return hits.ToArray();
        }

        /// <summary>
        /// Gathers the arc lengths at which a segment meets the surface of a body and of its openings.
        /// </summary>
        private static void CollectSolidCrossings(GeoLine3 line, GeoSolid3 solid, Tolerance tolerance, List<double> distances)
        {
            foreach (GeoFace3 face in solid.Faces)
            {
                if (TryIntersectWith(line, face, out GeoPoint3 hit, tolerance))
                {
                    distances.Add(Parametrization3.GetDistanceAtPoint(line, hit));
                }
            }

            foreach (GeoSolid3 opening in solid.Openings)
            {
                CollectSolidCrossings(line, opening, tolerance, distances);
            }
        }

        /// <summary>
        /// Finds where a line segment enters and leaves an axis-aligned box, using the default tolerance.
        /// </summary>
        public static GeoPoint3[] GetIntersections(GeoLine3 line, GeoAabb3 box) => GetIntersections(line, box, Tolerance.Global);

        /// <summary>
        /// Finds where a line segment enters and leaves an axis-aligned box, within a tolerance.
        /// </summary>
        public static GeoPoint3[] GetIntersections(GeoLine3 line, GeoAabb3 box, Tolerance tolerance)
        {
            if (box.IsEmpty)
            {
                return new GeoPoint3[0];
            }

            return GetIntersections(line, box.ToObb(), tolerance);
        }

        /// <summary>
        /// Finds where a ray enters and leaves an oriented box, using the default tolerance.
        /// </summary>
        public static GeoPoint3[] GetIntersections(GeoRay3 ray, GeoObb3 box) => GetIntersections(ray, box, Tolerance.Global);

        /// <summary>
        /// Finds where a ray enters and leaves an oriented box, within a tolerance.
        /// </summary>
        /// <remarks>
        /// A ray runs to infinity, so it is sampled as a segment long enough to reach past the far side of
        /// the box. Nothing beyond that point can be a crossing: past it the ray is only moving away.
        /// </remarks>
        public static GeoPoint3[] GetIntersections(GeoRay3 ray, GeoObb3 box, Tolerance tolerance)
        {
            if (box == null)
            {
                throw new ArgumentNullException(nameof(box));
            }

            double reach = 0.0;

            foreach (GeoPoint3 corner in box.GetCorners())
            {
                reach = Math.Max(reach, Parametrization3.GetDistanceAtPoint(ray, corner));
            }

            if (reach <= 0.0)
            {
                return new GeoPoint3[0];
            }

            return GetIntersections(ray.ToLine(reach + tolerance.EqualPoint), box, tolerance);
        }

        /// <summary>
        /// Finds where a ray enters and leaves an axis-aligned box, using the default tolerance.
        /// </summary>
        public static GeoPoint3[] GetIntersections(GeoRay3 ray, GeoAabb3 box) => GetIntersections(ray, box, Tolerance.Global);

        /// <summary>
        /// Finds where a ray enters and leaves an axis-aligned box, within a tolerance.
        /// </summary>
        public static GeoPoint3[] GetIntersections(GeoRay3 ray, GeoAabb3 box, Tolerance tolerance)
        {
            if (box.IsEmpty)
            {
                return new GeoPoint3[0];
            }

            return GetIntersections(ray, box.ToObb(), tolerance);
        }

        #endregion
    }
}
