using System;
using CommonGeometry;
using SolidGeometry.Geometry;

namespace SolidGeometry.Core
{
    /// <summary>
    /// Provides static methods for geometric projections in 3D space.
    /// <para>
    /// Every <c>ProjectTo...</c> method answers the same question: which point of the target shape is
    /// closest to the supplied point. For a bounded shape the answer is clamped to the shape, so
    /// projecting onto a segment never returns a point past its endpoints, and projecting onto a triangle
    /// never returns a point outside its edges.
    /// </para>
    /// </summary>
    public static class Projection3
    {
        #region Vector projections

        /// <summary>
        /// Projects a vector onto an axis vector.
        /// </summary>
        /// <param name="vector">The vector to project.</param>
        /// <param name="axis">The axis to project onto; it need not be normalized.</param>
        /// <returns>The component of the vector along the axis, or zero for a degenerate axis.</returns>
        public static GeoVector3 Project(GeoVector3 vector, GeoVector3 axis) => Project(vector, axis, Tolerance.Global);

        /// <summary>
        /// Projects a vector onto an axis vector, within a tolerance.
        /// </summary>
        public static GeoVector3 Project(GeoVector3 vector, GeoVector3 axis, Tolerance tolerance)
        {
            if (!axis.TryGetNormal(out GeoVector3 unitAxis, tolerance))
            {
                return GeoVector3.Zero;
            }

            return unitAxis.Multiply(vector.DotProduct(unitAxis));
        }

        /// <summary>
        /// Projects a vector onto a plane through the origin, dropping its component along the normal.
        /// </summary>
        /// <param name="vector">The vector to project.</param>
        /// <param name="planeNormal">The plane normal; it need not be normalized.</param>
        /// <returns>The component of the vector lying in the plane, or the vector itself for a degenerate normal.</returns>
        public static GeoVector3 ProjectOntoPlane(GeoVector3 vector, GeoVector3 planeNormal) => ProjectOntoPlane(vector, planeNormal, Tolerance.Global);

        /// <summary>
        /// Projects a vector onto a plane through the origin, within a tolerance.
        /// </summary>
        public static GeoVector3 ProjectOntoPlane(GeoVector3 vector, GeoVector3 planeNormal, Tolerance tolerance)
        {
            if (!planeNormal.TryGetNormal(out GeoVector3 unitNormal, tolerance))
            {
                return vector;
            }

            return vector.Subtract(unitNormal.Multiply(vector.DotProduct(unitNormal)));
        }

        #endregion

        #region Point onto shapes

        /// <summary>
        /// Projects a point onto a plane.
        /// </summary>
        public static GeoPoint3 ProjectToPlane(GeoPlane3 plane, GeoPoint3 point)
        {
            return point.Subtract(plane.Normal.Multiply(plane.SignedDistanceTo(point)));
        }

        /// <summary>
        /// Projects a point onto a line segment, clamped to its endpoints.
        /// </summary>
        public static GeoPoint3 ProjectToLine(GeoLine3 line, GeoPoint3 point) => ProjectToLine(line, point, Tolerance.Global);

        /// <summary>
        /// Projects a point onto a line segment, clamped to its endpoints, within a tolerance.
        /// </summary>
        public static GeoPoint3 ProjectToLine(GeoLine3 line, GeoPoint3 point, Tolerance tolerance)
        {
            double parameter = Parametrization3.GetParameterAtPoint(line, point, tolerance);
            parameter = Math.Max(0.0, Math.Min(1.0, parameter));

            return Parametrization3.GetPointAtParameter(line, parameter);
        }

        /// <summary>
        /// Projects a point onto the infinite line carrying a segment, without clamping.
        /// </summary>
        public static GeoPoint3 ProjectToInfiniteLine(GeoLine3 line, GeoPoint3 point) => ProjectToInfiniteLine(line, point, Tolerance.Global);

        /// <summary>
        /// Projects a point onto the infinite line carrying a segment, without clamping, within a tolerance.
        /// </summary>
        public static GeoPoint3 ProjectToInfiniteLine(GeoLine3 line, GeoPoint3 point, Tolerance tolerance)
        {
            return Parametrization3.GetPointAtParameter(line, Parametrization3.GetParameterAtPoint(line, point, tolerance));
        }

        /// <summary>
        /// Projects a point onto a ray, clamped to its origin.
        /// </summary>
        public static GeoPoint3 ProjectToRay(GeoRay3 ray, GeoPoint3 point)
        {
            double distance = Math.Max(0.0, Parametrization3.GetDistanceAtPoint(ray, point));

            return Parametrization3.GetPointAtDistance(ray, distance);
        }

        /// <summary>
        /// Projects a point onto a triangle, clamped to its edges.
        /// </summary>
        /// <param name="triangle">The triangle to project onto.</param>
        /// <param name="point">The point to project.</param>
        /// <returns>The point of the triangle closest to the supplied point.</returns>
        /// <remarks>
        /// This is the Voronoi region method: the space around a triangle divides into seven regions — one
        /// per vertex, one per edge, and the interior — and each is recognised by the signs of a handful
        /// of dot products before any division happens. Solving the barycentric system directly instead
        /// would divide by a determinant that vanishes for a degenerate triangle, which is exactly the
        /// input the fallback at the end of this method handles.
        /// </remarks>
        public static GeoPoint3 ProjectToTriangle(GeoTriangle3 triangle, GeoPoint3 point)
        {
            GeoPoint3 a = triangle.A;
            GeoPoint3 b = triangle.B;
            GeoPoint3 c = triangle.C;

            GeoVector3 ab = a.GetVectorTo(b);
            GeoVector3 ac = a.GetVectorTo(c);
            GeoVector3 ap = a.GetVectorTo(point);

            double d1 = ab.DotProduct(ap);
            double d2 = ac.DotProduct(ap);
            if (d1 <= 0.0 && d2 <= 0.0)
            {
                return a;
            }

            GeoVector3 bp = b.GetVectorTo(point);
            double d3 = ab.DotProduct(bp);
            double d4 = ac.DotProduct(bp);
            if (d3 >= 0.0 && d4 <= d3)
            {
                return b;
            }

            GeoVector3 cp = c.GetVectorTo(point);
            double d5 = ab.DotProduct(cp);
            double d6 = ac.DotProduct(cp);
            if (d6 >= 0.0 && d5 <= d6)
            {
                return c;
            }

            double vc = d1 * d4 - d3 * d2;
            if (vc <= 0.0 && d1 >= 0.0 && d3 <= 0.0)
            {
                return a.Add(ab.Multiply(d1 / (d1 - d3)));
            }

            double vb = d5 * d2 - d1 * d6;
            if (vb <= 0.0 && d2 >= 0.0 && d6 <= 0.0)
            {
                return a.Add(ac.Multiply(d2 / (d2 - d6)));
            }

            double va = d3 * d6 - d5 * d4;
            if (va <= 0.0 && d4 - d3 >= 0.0 && d5 - d6 >= 0.0)
            {
                return b.Add(b.GetVectorTo(c).Multiply((d4 - d3) / ((d4 - d3) + (d5 - d6))));
            }

            double denominator = va + vb + vc;
            if (denominator == 0.0)
            {
                // A triangle with no area has no interior region, so the answer is on one of its edges.
                // The vertex and edge tests above cover a well formed triangle and never reach here.
                return ClosestPointOnEdges(triangle, point);
            }

            double v = vb / denominator;
            double w = vc / denominator;

            return a.Add(ab.Multiply(v)).Add(ac.Multiply(w));
        }

        /// <summary>
        /// Gets the point on the three edges of a triangle closest to the supplied point.
        /// </summary>
        private static GeoPoint3 ClosestPointOnEdges(GeoTriangle3 triangle, GeoPoint3 point)
        {
            GeoPoint3 best = triangle.A;
            double bestDistance = double.MaxValue;

            for (int i = 0; i < 3; i++)
            {
                GeoPoint3 candidate = ProjectToLine(triangle.GetEdgeAt(i), point);
                double distance = candidate.GetDistanceSquaredTo(point);

                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = candidate;
                }
            }

            return best;
        }

        #endregion

        #region Closest connecting segment

        /// <summary>
        /// Finds the shortest segment connecting a point on one line segment to a point on another, using
        /// the default tolerance.
        /// </summary>
        public static GeoLine3 GetClosestSegment(GeoLine3 line1, GeoLine3 line2) => GetClosestSegment(line1, line2, Tolerance.Global);

        /// <summary>
        /// Finds the shortest segment connecting a point on one line segment to a point on another, within
        /// a tolerance.
        /// </summary>
        /// <param name="line1">The first segment; the result starts on it.</param>
        /// <param name="line2">The second segment; the result ends on it.</param>
        /// <param name="tolerance">The tolerance used to detect degenerate segments.</param>
        /// <returns>The shortest segment from a point on the first to a point on the second.</returns>
        /// <remarks>
        /// Two segments in space usually do not meet, so the answer is a segment rather than a point, and
        /// its length is the distance between them. When they do cross, the result is a zero-length
        /// segment sitting at the crossing point.
        /// </remarks>
        public static GeoLine3 GetClosestSegment(GeoLine3 line1, GeoLine3 line2, Tolerance tolerance)
        {
            GeoVector3 d1 = line1.Direction;
            GeoVector3 d2 = line2.Direction;
            GeoVector3 r = line2.StartPoint.GetVectorTo(line1.StartPoint);

            double squaredTolerance = tolerance.EqualPoint * tolerance.EqualPoint;
            double a = d1.LengthSquared;
            double e = d2.LengthSquared;
            double f = d2.DotProduct(r);

            double s;
            double t;

            if (a <= squaredTolerance && e <= squaredTolerance)
            {
                // Both segments are points.
                s = 0.0;
                t = 0.0;
            }
            else if (a <= squaredTolerance)
            {
                s = 0.0;
                t = Clamp(f / e);
            }
            else
            {
                double c = d1.DotProduct(r);

                if (e <= squaredTolerance)
                {
                    t = 0.0;
                    s = Clamp(-c / a);
                }
                else
                {
                    double b = d1.DotProduct(d2);
                    double denominator = a * e - b * b;

                    // The determinant vanishes for parallel segments, where every pair of facing points is
                    // equally close and any of them is a correct answer. Starting from s = 0 picks the one
                    // at the start of the first segment and lets the clamping below slide it into range.
                    s = denominator != 0.0 ? Clamp((b * f - c * e) / denominator) : 0.0;

                    t = (b * s + f) / e;

                    if (t < 0.0)
                    {
                        t = 0.0;
                        s = Clamp(-c / a);
                    }
                    else if (t > 1.0)
                    {
                        t = 1.0;
                        s = Clamp((b - c) / a);
                    }
                }
            }

            return new GeoLine3(
                line1.StartPoint.Add(d1.Multiply(s)),
                line2.StartPoint.Add(d2.Multiply(t)));
        }

        /// <summary>
        /// Clamps a parameter into the [0, 1] range covering a segment.
        /// </summary>
        private static double Clamp(double value) => Math.Max(0.0, Math.Min(1.0, value));

        #endregion

        #region Point onto regions and volumes

        /// <summary>
        /// Projects a point onto a polyline, clamped to the chain.
        /// </summary>
        public static GeoPoint3 ProjectToPolyline(GeoPolyline3 polyline, GeoPoint3 point) => ProjectToPolyline(polyline, point, Tolerance.Global);

        /// <summary>
        /// Projects a point onto a polyline, clamped to the chain, within a tolerance.
        /// </summary>
        public static GeoPoint3 ProjectToPolyline(GeoPolyline3 polyline, GeoPoint3 point, Tolerance tolerance)
        {
            if (polyline == null)
            {
                throw new ArgumentNullException(nameof(polyline));
            }

            GeoPoint3 best = polyline.StartPoint;
            double bestDistanceSquared = double.MaxValue;

            for (int i = 0; i < polyline.EdgeCount; i++)
            {
                GeoPoint3 candidate = ProjectToLine(polyline.GetEdgeAt(i), point, tolerance);
                double distanceSquared = Distance3.GetDistanceSquaredTo(candidate, point);

                if (distanceSquared < bestDistanceSquared)
                {
                    bestDistanceSquared = distanceSquared;
                    best = candidate;
                }
            }

            return best;
        }

        /// <summary>
        /// Projects a point onto the boundary of a polygon, ignoring its interior.
        /// </summary>
        public static GeoPoint3 ProjectToPolygonBoundary(GeoPolygon3 polygon, GeoPoint3 point) => ProjectToPolygonBoundary(polygon, point, Tolerance.Global);

        /// <summary>
        /// Projects a point onto the boundary of a polygon, ignoring its interior, within a tolerance.
        /// </summary>
        public static GeoPoint3 ProjectToPolygonBoundary(GeoPolygon3 polygon, GeoPoint3 point, Tolerance tolerance)
        {
            if (polygon == null)
            {
                throw new ArgumentNullException(nameof(polygon));
            }

            GeoPoint3 best = polygon[0];
            double bestDistanceSquared = double.MaxValue;

            for (int i = 0; i < polygon.EdgeCount; i++)
            {
                GeoPoint3 candidate = ProjectToLine(polygon.GetEdgeAt(i), point, tolerance);
                double distanceSquared = Distance3.GetDistanceSquaredTo(candidate, point);

                if (distanceSquared < bestDistanceSquared)
                {
                    bestDistanceSquared = distanceSquared;
                    best = candidate;
                }
            }

            return best;
        }

        /// <summary>
        /// Projects a point onto a polygon, read as a filled surface.
        /// </summary>
        public static GeoPoint3 ProjectToPolygon(GeoPolygon3 polygon, GeoPoint3 point) => ProjectToPolygon(polygon, point, Tolerance.Global);

        /// <summary>
        /// Projects a point onto a polygon, read as a filled surface, within a tolerance.
        /// </summary>
        /// <remarks>
        /// A point above the interior lands straight down on the surface; a point beyond the outline
        /// lands on the nearest edge. That is the difference between this and
        /// <see cref="ProjectToPolygonBoundary(GeoPolygon3, GeoPoint3, Tolerance)"/>, which always lands
        /// on an edge.
        /// </remarks>
        public static GeoPoint3 ProjectToPolygon(GeoPolygon3 polygon, GeoPoint3 point, Tolerance tolerance)
        {
            if (polygon == null)
            {
                throw new ArgumentNullException(nameof(polygon));
            }

            GeoPoint3 flat = ProjectToPlane(polygon.GetPlane(), point);

            if (Containment3.Contains(polygon, flat, tolerance))
            {
                return flat;
            }

            return ProjectToPolygonBoundary(polygon, point, tolerance);
        }

        /// <summary>
        /// Projects a point onto an oriented box, read as a filled volume.
        /// </summary>
        /// <remarks>
        /// A point inside the box is already on the box under that reading, so it comes back unchanged
        /// rather than being pushed out to the nearest face.
        /// </remarks>
        public static GeoPoint3 ProjectToObb(GeoObb3 box, GeoPoint3 point)
        {
            if (box == null)
            {
                throw new ArgumentNullException(nameof(box));
            }

            GeoPoint3 local = box.CoordinateSystem.ToLocal(point);

            GeoPoint3 clamped = new GeoPoint3(
                Math.Max(-box.ExtentX, Math.Min(box.ExtentX, local.X)),
                Math.Max(-box.ExtentY, Math.Min(box.ExtentY, local.Y)),
                Math.Max(-box.ExtentZ, Math.Min(box.ExtentZ, local.Z)));

            return box.CoordinateSystem.ToGlobal(clamped);
        }

        /// <summary>
        /// Projects a point onto the surface of an oriented box, never returning a point inside it.
        /// </summary>
        /// <remarks>
        /// For a point outside the box this agrees with
        /// <see cref="ProjectToObb(GeoObb3, GeoPoint3)"/>. For a point inside, the answer is on the face
        /// it is nearest to, which is the question to ask when measuring clearance to a wall from within
        /// a room.
        /// </remarks>
        public static GeoPoint3 ProjectToObbSurface(GeoObb3 box, GeoPoint3 point)
        {
            if (box == null)
            {
                throw new ArgumentNullException(nameof(box));
            }

            GeoPoint3 local = box.CoordinateSystem.ToLocal(point);

            double x = Math.Max(-box.ExtentX, Math.Min(box.ExtentX, local.X));
            double y = Math.Max(-box.ExtentY, Math.Min(box.ExtentY, local.Y));
            double z = Math.Max(-box.ExtentZ, Math.Min(box.ExtentZ, local.Z));

            bool inside = x == local.X && y == local.Y && z == local.Z;

            if (inside)
            {
                // Push the coordinate with the least room left out to its own face, so the result sits on
                // the face the point is nearest to.
                double slackX = box.ExtentX - Math.Abs(local.X);
                double slackY = box.ExtentY - Math.Abs(local.Y);
                double slackZ = box.ExtentZ - Math.Abs(local.Z);

                if (slackX <= slackY && slackX <= slackZ)
                {
                    x = local.X >= 0.0 ? box.ExtentX : -box.ExtentX;
                }
                else if (slackY <= slackZ)
                {
                    y = local.Y >= 0.0 ? box.ExtentY : -box.ExtentY;
                }
                else
                {
                    z = local.Z >= 0.0 ? box.ExtentZ : -box.ExtentZ;
                }
            }

            return box.CoordinateSystem.ToGlobal(new GeoPoint3(x, y, z));
        }

        /// <summary>
        /// Projects a point onto the surface of a solid.
        /// </summary>
        public static GeoPoint3 ProjectToSolid(GeoSolid3 solid, GeoPoint3 point) => ProjectToSolid(solid, point, Tolerance.Global);

        /// <summary>
        /// Projects a point onto the surface of a solid, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The answer is always on the boundary, interior points included, because a solid is described
        /// only by its faces and there is nothing else to land on.
        /// </remarks>
        public static GeoPoint3 ProjectToSolid(GeoSolid3 solid, GeoPoint3 point, Tolerance tolerance)
        {
            if (solid == null)
            {
                throw new ArgumentNullException(nameof(solid));
            }

            GeoPoint3 best = point;
            double bestDistanceSquared = double.MaxValue;

            foreach (GeoFace3 face in solid.Faces)
            {
                GeoPoint3 candidate = ProjectToPolygon(face.Boundary, point, tolerance);

                foreach (GeoPolygon3 hole in face.Holes)
                {
                    // A point over a hole is not over material, so the nearest position on that face is on
                    // the rim of the hole instead of on the surface behind it.
                    if (Containment3.Contains(hole, ProjectToPlane(face.GetPlane(), point), tolerance))
                    {
                        candidate = ProjectToPolygonBoundary(hole, point, tolerance);
                        break;
                    }
                }

                double distanceSquared = Distance3.GetDistanceSquaredTo(candidate, point);

                if (distanceSquared < bestDistanceSquared)
                {
                    bestDistanceSquared = distanceSquared;
                    best = candidate;
                }
            }

            return best;
        }

        #endregion

        #region Point onto a circle

        /// <summary>
        /// Projects a point onto the circumference of a circle.
        /// </summary>
        /// <remarks>
        /// A point on the axis of the circle is the same distance from every point of the circumference,
        /// so any of them is a correct answer; the one at the zero parameter comes back.
        /// </remarks>
        public static GeoPoint3 ProjectToCircle(GeoCircle3 circle, GeoPoint3 point) => ProjectToCircle(circle, point, Tolerance.Global);

        /// <summary>
        /// Projects a point onto the circumference of a circle, within a tolerance.
        /// </summary>
        public static GeoPoint3 ProjectToCircle(GeoCircle3 circle, GeoPoint3 point, Tolerance tolerance)
        {
            GeoPoint3 flat = ProjectToPlane(circle.GetPlane(), point);
            GeoVector3 radial = circle.Center.GetVectorTo(flat);

            if (!radial.TryGetNormal(out GeoVector3 direction, tolerance))
            {
                return Parametrization3.GetPointAtParameter(circle, 0.0);
            }

            return circle.Center.Add(direction.Multiply(circle.Radius));
        }

        /// <summary>
        /// Projects a point onto a circular disc, read as a filled surface.
        /// </summary>
        /// <remarks>
        /// A point above the interior lands straight down on the surface; a point beyond the rim lands on
        /// the rim. That is the difference between this and
        /// <see cref="ProjectToCircle(GeoCircle3, GeoPoint3, Tolerance)"/>, which always lands on the rim.
        /// </remarks>
        public static GeoPoint3 ProjectToDisc(GeoCircle3 circle, GeoPoint3 point) => ProjectToDisc(circle, point, Tolerance.Global);

        /// <summary>
        /// Projects a point onto a circular disc, read as a filled surface, within a tolerance.
        /// </summary>
        public static GeoPoint3 ProjectToDisc(GeoCircle3 circle, GeoPoint3 point, Tolerance tolerance)
        {
            GeoPoint3 flat = ProjectToPlane(circle.GetPlane(), point);

            if (circle.Center.DistanceTo(flat) <= circle.Radius)
            {
                return flat;
            }

            return ProjectToCircle(circle, point, tolerance);
        }

        #endregion
    }
}
