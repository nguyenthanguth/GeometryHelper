using System;
using System.Collections.Generic;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    /// <summary>
    /// Provides static methods for deciding whether two 3D shapes overlap.
    /// <para>
    /// <c>CollidesWith</c> answers yes or no and says nothing about where. Shapes that merely touch count
    /// as colliding, and so do shapes within the point tolerance of each other: for points and straight shapes in space
    /// the answer is exactly whether <c>Distance3</c> reports no more than the point tolerance for the same pair, with one
    /// stated exception: a triangle with no area touches nothing, whatever its distance, which still reads the gap to the
    /// segment it is. Two bodies and a plane are not read so (see <c>Distance3</c>). Where
    /// the shapes meet is <c>Intersection3</c>, and how far apart they are when they do not is
    /// <c>Distance3</c>.
    /// </para>
    /// </summary>
    public static partial class Collision3
    {
        #region Bounding boxes

        /// <summary>
        /// Checks whether two axis-aligned boxes overlap, using the default tolerance.
        /// </summary>
        public static bool CollidesWith(GeoAabb3 box1, GeoAabb3 box2) => box1.CollidesWith(box2);

        /// <summary>
        /// Checks whether two axis-aligned boxes overlap, within a tolerance.
        /// </summary>
        public static bool CollidesWith(GeoAabb3 box1, GeoAabb3 box2, Tolerance tolerance) => box1.CollidesWith(box2, tolerance);

        #endregion

        #region Oriented boxes

        /// <summary>
        /// Checks whether two oriented boxes overlap, using the default tolerance.
        /// </summary>
        public static bool CollidesWith(GeoObb3 box1, GeoObb3 box2) => CollidesWith(box1, box2, Tolerance.Global);

        /// <summary>
        /// Checks whether two oriented boxes overlap, within a tolerance.
        /// </summary>
        /// <param name="box1">The first box.</param>
        /// <param name="box2">The second box.</param>
        /// <param name="tolerance">The tolerance; boxes closer than this count as touching.</param>
        /// <returns>true if the boxes overlap or touch; otherwise, false.</returns>
        /// <remarks>
        /// This is the separating axis theorem. Two convex shapes miss each other exactly when some
        /// direction exists along which their shadows do not overlap, and for a pair of boxes it is enough
        /// to try fifteen directions: the three axes of each box, and the nine cross products pairing one
        /// axis from each. The first six catch a face separating them and the last nine catch an edge, the
        /// case where two boxes pass by each other at an angle with no face facing the gap. Along every one of
        /// them the boxes may stand the point tolerance apart and no more, so two boxes whose nearest edges
        /// cross at a sine of a tenth, 0.005 apart, do not touch, and 0.0009 apart they do.
        /// </remarks>
        public static bool CollidesWith(GeoObb3 box1, GeoObb3 box2, Tolerance tolerance)
        {
            if (box1 == null)
            {
                throw new ArgumentNullException(nameof(box1));
            }

            if (box2 == null)
            {
                throw new ArgumentNullException(nameof(box2));
            }

            double[] e1 = { box1.ExtentX, box1.ExtentY, box1.ExtentZ };
            double[] e2 = { box2.ExtentX, box2.ExtentY, box2.ExtentZ };

            double[,] r = new double[3, 3];
            double[,] absR = new double[3, 3];

            // A tiny addition keeps the cross-product axes usable when two axes are nearly parallel: their
            // cross product is then near zero and its normalized direction is dominated by rounding error,
            // which without this would show as a spurious separation.
            const double parallelGuard = 1E-12;

            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    r[i, j] = box1.GetAxisAt(i).DotProduct(box2.GetAxisAt(j));
                    absR[i, j] = Math.Abs(r[i, j]) + parallelGuard;
                }
            }

            GeoVector3 between = box1.Center.GetVectorTo(box2.Center);
            double[] t =
            {
                between.DotProduct(box1.AxisX),
                between.DotProduct(box1.AxisY),
                between.DotProduct(box1.AxisZ)
            };

            double slack = tolerance.EqualPoint;

            // The three axes of the first box.
            for (int i = 0; i < 3; i++)
            {
                double reach1 = e1[i];
                double reach2 = e2[0] * absR[i, 0] + e2[1] * absR[i, 1] + e2[2] * absR[i, 2];

                if (Math.Abs(t[i]) > reach1 + reach2 + slack)
                {
                    return false;
                }
            }

            // The three axes of the second box.
            for (int j = 0; j < 3; j++)
            {
                double reach1 = e1[0] * absR[0, j] + e1[1] * absR[1, j] + e1[2] * absR[2, j];
                double reach2 = e2[j];
                double separation = Math.Abs(t[0] * r[0, j] + t[1] * r[1, j] + t[2] * r[2, j]);

                if (separation > reach1 + reach2 + slack)
                {
                    return false;
                }
            }

            // The nine axes pairing one axis from each box. The expressions below are the general
            // projection written out for each pair, with the zero terms of the cross product dropped. That
            // cross product is as long as the sine between the two axes, and so is everything measured along
            // it, so the slack is scaled by the same length: added whole, it let two edges crossing at a sine
            // of a tenth stand ten point tolerances apart and still touch. Two axes parallel to within rounding
            // have a cross product of no length and allow no slack, and the guard above keeps rounding from
            // reading as a separation there; the axes of the boxes themselves have already been asked.
            for (int i = 0; i < 3; i++)
            {
                int i1 = (i + 1) % 3;
                int i2 = (i + 2) % 3;

                for (int j = 0; j < 3; j++)
                {
                    int j1 = (j + 1) % 3;
                    int j2 = (j + 2) % 3;

                    double reach1 = e1[i1] * absR[i2, j] + e1[i2] * absR[i1, j];
                    double reach2 = e2[j1] * absR[i, j2] + e2[j2] * absR[i, j1];
                    double separation = Math.Abs(t[i2] * r[i1, j] - t[i1] * r[i2, j]);
                    double length = Math.Sqrt(r[i1, j] * r[i1, j] + r[i2, j] * r[i2, j]);

                    if (separation > reach1 + reach2 + slack * length)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Checks whether a line segment reaches into an oriented box, using the default tolerance.
        /// </summary>
        public static bool CollidesWith(GeoLine3 line, GeoObb3 box) => CollidesWith(line, box, Tolerance.Global);

        /// <summary>
        /// Checks whether a line segment reaches into an oriented box, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The box is read as its body, as <see cref="Distance3.DistanceTo(GeoLine3, GeoObb3, Tolerance)"/> reads it, and
        /// they touch where that distance is no longer than the point tolerance: the box 100 by 100 by 50 with its top in
        /// the floor is touched by a segment in the floor starting 0.0005 off its side, and not by one starting 0.0012 off
        /// its corner, 0.00085 off in each direction.
        /// </remarks>
        public static bool CollidesWith(GeoLine3 line, GeoObb3 box, Tolerance tolerance)
        {
            if (box == null)
            {
                throw new ArgumentNullException(nameof(box));
            }

            return new Gap3.Block(box).Within(line.StartPoint, line.EndPoint, tolerance, Reach(tolerance));
        }

        #endregion

        #region Triangles

        /// <summary>
        /// Checks whether two triangles overlap, using the default tolerance.
        /// </summary>
        public static bool CollidesWith(GeoTriangle3 triangle1, GeoTriangle3 triangle2) => CollidesWith(triangle1, triangle2, Tolerance.Global);

        /// <summary>
        /// Checks whether two triangles overlap, within a tolerance.
        /// </summary>
        /// <remarks>
        /// Two triangles that are not coplanar can only meet along a line, so it is enough to ask whether
        /// any edge of one pierces the other. Coplanar triangles meet across an area instead and that test
        /// finds nothing, so they are handled separately by looking for a vertex of one inside the other
        /// or for a pair of edges crossing. They count as coplanar when their normals are parallel and either
        /// lies wholly within the planar tolerance of the other's plane, as two flat regions do, so the answer is
        /// the same whichever is asked first. Two that do neither touch where they come within the point
        /// tolerance of each other, the gap their distance measures.
        /// </remarks>
        public static bool CollidesWith(GeoTriangle3 triangle1, GeoTriangle3 triangle2, Tolerance tolerance)
        {
            if (triangle1.IsDegenerate(tolerance) || triangle2.IsDegenerate(tolerance))
            {
                return false;
            }

            if (IsClearOf(triangle1, triangle2, tolerance) || IsClearOf(triangle2, triangle1, tolerance))
            {
                return false;
            }

            for (int i = 0; i < 3; i++)
            {
                if (Intersection3.TryIntersectWith(triangle1.GetEdgeAt(i), triangle2, out _, tolerance) ||
                    Intersection3.TryIntersectWith(triangle2.GetEdgeAt(i), triangle1, out _, tolerance))
                {
                    return true;
                }
            }

            if (!Parallel3.IsParallel(triangle1.Normal, triangle2.Normal, tolerance))
            {
                return ComeWithin(triangle1, triangle2, tolerance);
            }

            // In one plane as two flat regions are (Intersection3.Flat.InOnePlane): either lies wholly in the
            // other's plane. One corner of the second against the first's plane read the pair one way round only,
            // so two triangles a hundred-thousandth of a radian apart touched or not by which was asked first.
            if (!Boolean3.LiesIn(triangle1.GetPlane(), new[] { triangle2.A, triangle2.B, triangle2.C }, tolerance) &&
                !Boolean3.LiesIn(triangle2.GetPlane(), new[] { triangle1.A, triangle1.B, triangle1.C }, tolerance))
            {
                return ComeWithin(triangle1, triangle2, tolerance);
            }

            for (int i = 0; i < 3; i++)
            {
                if (Containment3.Contains(triangle2, triangle1[i], tolerance) ||
                    Containment3.Contains(triangle1, triangle2[i], tolerance))
                {
                    return true;
                }
            }

            return ComeWithin(triangle1, triangle2, tolerance);
        }

        /// <summary>
        /// Determines whether two triangles that neither cross nor overlap in one plane still come within the point
        /// tolerance of each other, the gap their distance measures.
        /// </summary>
        /// <remarks>
        /// Two flat regions touch wherever their gap is no longer than the point tolerance
        /// (<see cref="CollidesWith(GeoPolygon3, GeoPolygon3, Tolerance)"/>), and a triangle is answered the same way: a
        /// pair 0.0006 apart, turned a hundred-thousandth of a radian against each other so that neither lies in the
        /// other's plane, touches, as two boxes 0.0009 apart do. Apart, the nearest pair lies on two edges or on a corner
        /// of one and the other triangle, as <see cref="Distance3.DistanceTo(GeoTriangle3, GeoTriangle3, Tolerance)"/>
        /// measures them; each against the other's plane and the boxes of the two are asked first, so that a pair
        /// plainly apart pays nothing more.
        /// </remarks>
        private static bool ComeWithin(GeoTriangle3 triangle1, GeoTriangle3 triangle2, Tolerance tolerance)
        {
            double near = tolerance.EqualPoint;

            // Either standing wholly to one side of the other's plane, further than the point tolerance, is further than
            // that from all of the other: the test of IsClearOf, at the band's own width. Then the boxes of the two. Both
            // are asked a millionth past the tolerance, so that the rounding of a height never refuses a pair standing
            // exactly the tolerance apart, which the distances below take.
            double clear = near * (1.0 + 1E-6);

            if (StandsClear(triangle1.GetPlane(), triangle2, clear) || StandsClear(triangle2.GetPlane(), triangle1, clear) ||
                Apart(triangle1.A.X, triangle1.B.X, triangle1.C.X, triangle2.A.X, triangle2.B.X, triangle2.C.X, clear) ||
                Apart(triangle1.A.Y, triangle1.B.Y, triangle1.C.Y, triangle2.A.Y, triangle2.B.Y, triangle2.C.Y, clear) ||
                Apart(triangle1.A.Z, triangle1.B.Z, triangle1.C.Z, triangle2.A.Z, triangle2.B.Z, triangle2.C.Z, clear))
            {
                return false;
            }

            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    // A side of a triangle is the side it is, however short: it is not a segment a caller gave, which is read as its
                    // start when it is no longer than the tolerance.
                    GeoLine3 side = triangle1.GetEdgeAt(i);
                    GeoLine3 against = triangle2.GetEdgeAt(j);

                    if (Gap3.SegmentToSegment(side.StartPoint, side.EndPoint, against.StartPoint, against.EndPoint) <= near)
                    {
                        return true;
                    }
                }
            }

            for (int i = 0; i < 3; i++)
            {
                if (Distance3.DistanceTo(triangle2, triangle1[i]) <= near ||
                    Distance3.DistanceTo(triangle1, triangle2[i]) <= near)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Checks whether a triangle lies wholly to one side of another's plane, clear of it by more than twice
        /// the larger of the point and planar tolerances.
        /// </summary>
        /// <param name="triangle">The triangle whose plane is the dividing one; it must not be degenerate.</param>
        /// <param name="other">The triangle judged against it.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <remarks>
        /// Such a pair cannot meet, and three distances to a plane say so for the price of a cross product, where
        /// the full test asks six edges whether they cross a face. The margin is what makes it safe. Every way the
        /// full test finds contact — an edge of one reaching the other's plane with the segment stretched by the
        /// point tolerance at each end, a point placed on the other triangle within the point or the planar
        /// tolerance, the two lying in one plane within the planar tolerance — puts some point of
        /// <paramref name="other"/> within the larger of the two tolerances of this plane, and all of it lies
        /// farther away than that.
        /// </remarks>
        internal static bool IsClearOf(GeoTriangle3 triangle, GeoTriangle3 other, Tolerance tolerance)
        {
            GeoPlane3 plane = triangle.GetPlane();
            double clear = 2.0 * Math.Max(tolerance.EqualPoint, tolerance.EqualPlanar);

            double a = plane.SignedDistanceTo(other.A);
            double b = plane.SignedDistanceTo(other.B);
            double c = plane.SignedDistanceTo(other.C);

            return (a > clear && b > clear && c > clear) || (a < -clear && b < -clear && c < -clear);
        }

        /// <summary>
        /// Determines whether the spans of two triangles along one axis, given by their corners, stand more than a
        /// distance apart.
        /// </summary>
        private static bool Apart(double a1, double b1, double c1, double a2, double b2, double c2, double clear)
            => Math.Min(a1, Math.Min(b1, c1)) - clear > Math.Max(a2, Math.Max(b2, c2)) ||
               Math.Min(a2, Math.Min(b2, c2)) - clear > Math.Max(a1, Math.Max(b1, c1));

        /// <summary>
        /// Determines whether every corner of a triangle stands more than a distance to one side of a plane.
        /// </summary>
        private static bool StandsClear(GeoPlane3 plane, GeoTriangle3 triangle, double clear)
        {
            double a = plane.SignedDistanceTo(triangle.A);
            double b = plane.SignedDistanceTo(triangle.B);
            double c = plane.SignedDistanceTo(triangle.C);

            return (a > clear && b > clear && c > clear) || (a < -clear && b < -clear && c < -clear);
        }

        #endregion

        #region Solids

        /// <summary>
        /// Checks whether two solids overlap, using the default tolerance.
        /// </summary>
        public static bool CollidesWith(GeoSolid3 solid1, GeoSolid3 solid2) => CollidesWith(solid1, solid2, Tolerance.Global);

        /// <summary>
        /// Checks whether two solids overlap, within a tolerance.
        /// </summary>
        /// <param name="solid1">The first solid.</param>
        /// <param name="solid2">The second solid.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true if the solids share any space; otherwise, false.</returns>
        /// <remarks>
        /// Two closed bodies overlap in one of exactly two ways: their surfaces cross, or one sits wholly
        /// inside the other with no surface contact at all. The first is found by testing the surface
        /// triangles against each other, the second by testing a single vertex of each body against the
        /// other, which settles it because a body that is inside without touching is entirely inside.
        /// Bounding boxes are checked first, since that rejects most pairs before any triangle is touched.
        /// </remarks>
        public static bool CollidesWith(GeoSolid3 solid1, GeoSolid3 solid2, Tolerance tolerance)
        {
            if (solid1 == null)
            {
                throw new ArgumentNullException(nameof(solid1));
            }

            if (solid2 == null)
            {
                throw new ArgumentNullException(nameof(solid2));
            }

            // The faces run straight across every opening; read the material the probe can reach instead.
            GeoAabb3 reach1 = solid1.GetAabb();
            solid1 = Material3.Near(solid1, solid2.GetAabb(), tolerance);
            solid2 = Material3.Near(solid2, reach1, tolerance);

            if (!solid1.GetAabb().CollidesWith(solid2.GetAabb(), tolerance))
            {
                return false;
            }

            GeoTriangle3[] mesh1 = solid1.TriangulateQuietly(tolerance);
            GeoTriangle3[] mesh2 = solid2.TriangulateQuietly(tolerance);

            if (SurfacesTouch(mesh1, mesh2, tolerance))
            {
                return true;
            }

            if (mesh1.Length > 0 && Containment3.Locate(solid2, mesh1[0].A, tolerance) == PointLocation.Inside)
            {
                return true;
            }

            if (mesh2.Length > 0 && Containment3.Locate(solid1, mesh2[0].A, tolerance) == PointLocation.Inside)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Checks whether any triangle of one mesh touches any triangle of another.
        /// </summary>
        /// <remarks>
        /// Comparing every pair costs time in the product of the two counts, which is fine for a handful of
        /// triangles and hopeless for a real model. Above a threshold the meshes are indexed first and
        /// walked as a pair of trees instead, so a pair of boxes that miss each other discards everything
        /// beneath them at once. The threshold exists because building the index is itself work: below it,
        /// the plain scan wins.
        /// </remarks>
        private static bool SurfacesTouch(GeoTriangle3[] mesh1, GeoTriangle3[] mesh2, Tolerance tolerance)
        {
            const int indexThreshold = 64;

            if ((long)mesh1.Length * mesh2.Length > indexThreshold * indexThreshold)
            {
                return new Spatial.GeoBvh3(mesh1).CollidesWith(new Spatial.GeoBvh3(mesh2), tolerance);
            }

            foreach (GeoTriangle3 t1 in mesh1)
            {
                foreach (GeoTriangle3 t2 in mesh2)
                {
                    if (CollidesWith(t1, t2, tolerance))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Checks whether an oriented box overlaps a solid, using the default tolerance.
        /// </summary>
        public static bool CollidesWith(GeoObb3 box, GeoSolid3 solid) => CollidesWith(box, solid, Tolerance.Global);

        /// <summary>
        /// Checks whether an oriented box overlaps a solid, within a tolerance.
        /// </summary>
        public static bool CollidesWith(GeoObb3 box, GeoSolid3 solid, Tolerance tolerance)
        {
            if (box == null)
            {
                throw new ArgumentNullException(nameof(box));
            }

            return CollidesWith(box.ToSolid(), solid, tolerance);
        }

        #endregion

        #region Mixed pairs

        /// <summary>
        /// Checks whether an oriented box overlaps an axis-aligned one, using the default tolerance.
        /// </summary>
        public static bool CollidesWith(GeoObb3 box1, GeoAabb3 box2) => CollidesWith(box1, box2, Tolerance.Global);

        /// <summary>
        /// Checks whether an oriented box overlaps an axis-aligned one, within a tolerance.
        /// </summary>
        /// <remarks>
        /// An axis-aligned box is an oriented box that happens to line up with the world, so this is the
        /// same separating axis test with one of the two frames already known.
        /// </remarks>
        public static bool CollidesWith(GeoObb3 box1, GeoAabb3 box2, Tolerance tolerance)
        {
            if (box2.IsEmpty)
            {
                return false;
            }

            return CollidesWith(box1, box2.ToObb(), tolerance);
        }

        /// <summary>
        /// Checks whether a line segment reaches into an axis-aligned box, using the default tolerance.
        /// </summary>
        public static bool CollidesWith(GeoLine3 line, GeoAabb3 box) => CollidesWith(line, box, Tolerance.Global);

        /// <summary>
        /// Checks whether a line segment reaches into an axis-aligned box, within a tolerance.
        /// </summary>
        public static bool CollidesWith(GeoLine3 line, GeoAabb3 box, Tolerance tolerance)
        {
            if (box.IsEmpty)
            {
                return false;
            }

            return CollidesWith(line, box.ToObb(), tolerance);
        }

        /// <summary>
        /// Checks whether a line segment reaches into a solid, using the default tolerance.
        /// </summary>
        public static bool CollidesWith(GeoLine3 line, GeoSolid3 solid) => CollidesWith(line, solid, Tolerance.Global);

        /// <summary>
        /// Checks whether a line segment reaches into a solid, within a tolerance.
        /// </summary>
        /// <remarks>
        /// A segment lying wholly inside the body touches it without crossing its surface, so where its ends lie is
        /// asked as well as where it crosses. They touch where <see cref="Distance3.DistanceTo(GeoLine3, GeoSolid3, Tolerance)"/>
        /// is no longer than the point tolerance, the openings the segment can reach cut in: a segment in the plane of the
        /// top of the box 100 by 100 by 50, starting 0.0005 off its side, touches it, and one starting 0.0012 off its
        /// corner, 0.00085 off in each direction, does not.
        /// Whether a point is in the body is asked to within rounding, a billionth of a millimetre (1E-9, not nought),
        /// so that rounding in a body turned about in space cannot lose a crossing: a point within 1E-9 outside the
        /// body reads as in it, and a point 5E-10 outside a box is nought away from it.
        /// </remarks>
        public static bool CollidesWith(GeoLine3 line, GeoSolid3 solid, Tolerance tolerance)
        {
            if (solid == null)
            {
                throw new ArgumentNullException(nameof(solid));
            }

            GeoAabb3 reach = line.GetAabb();

            if (!solid.GetAabb().CollidesWith(reach, tolerance))
            {
                return false;
            }

            // The faces run straight across every opening; read the material the segment can reach instead.
            solid = Material3.Near(solid, reach, tolerance);

            return Gap3.SurfaceSegment(solid, line.StartPoint, line.EndPoint, true, tolerance, Reach(tolerance), tolerance.EqualPoint) <= tolerance.EqualPoint
                || Gap3.Holds(solid, line.StartPoint) || Gap3.Holds(solid, line.EndPoint);
        }

        /// <summary>
        /// Checks whether a polyline reaches into a solid, using the default tolerance.
        /// </summary>
        public static bool CollidesWith(GeoPolyline3 polyline, GeoSolid3 solid) => CollidesWith(polyline, solid, Tolerance.Global);

        /// <summary>
        /// Checks whether a polyline reaches into a solid, within a tolerance.
        /// </summary>
        /// <remarks>
        /// Whether a point is in the body is asked to within rounding, a billionth of a millimetre (1E-9, not nought),
        /// so that rounding in a body turned about in space cannot lose a crossing: a point within 1E-9 outside the
        /// body reads as in it, and a point 5E-10 outside a box is nought away from it.
        /// </remarks>
        public static bool CollidesWith(GeoPolyline3 polyline, GeoSolid3 solid, Tolerance tolerance)
        {
            if (polyline == null)
            {
                throw new ArgumentNullException(nameof(polyline));
            }

            // The faces run straight across every opening; read the material the probe can reach instead.
            solid = Material3.Near(solid, polyline.GetAabb(), tolerance);

            for (int i = 0; i < polyline.EdgeCount; i++)
            {
                if (CollidesWith(polyline.GetEdgeAt(i), solid, tolerance))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Checks whether a polygon overlaps a solid, using the default tolerance.
        /// </summary>
        public static bool CollidesWith(GeoPolygon3 polygon, GeoSolid3 solid) => CollidesWith(polygon, solid, Tolerance.Global);

        /// <summary>
        /// Checks whether a polygon overlaps a solid, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The polygon is read filled and the body by its material, every opening the polygon can reach cut in, as a
        /// triangle is read against a body (<see cref="CollidesWith(GeoTriangle3, GeoSolid3, Tolerance)"/>): they
        /// meet where an edge of the polygon crosses the surface, where an edge of the body crosses the polygon, or
        /// where a corner of the polygon lies in the material. Through the box 40 by 40 by 20 from the origin with an opening 10 by 10
        /// from x, y = 15 to 25 right through it, the square standing upright in y = 20 from x = 17 to 23, 2 clear
        /// of the opening's walls, touches nothing.
        /// Whether a point is in the body is asked to within rounding, a billionth of a millimetre (1E-9, not nought),
        /// so that rounding in a body turned about in space cannot lose a crossing: a point within 1E-9 outside the
        /// body reads as in it, and a point 5E-10 outside a box is nought away from it.
        /// </remarks>
        public static bool CollidesWith(GeoPolygon3 polygon, GeoSolid3 solid, Tolerance tolerance)
        {
            if (polygon == null)
            {
                throw new ArgumentNullException(nameof(polygon));
            }

            if (solid == null)
            {
                throw new ArgumentNullException(nameof(solid));
            }

            if (!polygon.GetAabb().CollidesWith(solid.GetAabb(), tolerance))
            {
                return false;
            }

            // The faces run straight across every opening; read the material the region can reach instead.
            GeoSolid3 material = Material3.Near(solid, polygon.GetAabb(), tolerance);

            return ReachesMaterial(Gap3.Region.Of(polygon), polygon.Vertices, material, tolerance);
        }

        /// <summary>
        /// Checks whether a polygon overlaps an oriented box, using the default tolerance.
        /// </summary>
        public static bool CollidesWith(GeoPolygon3 polygon, GeoObb3 box) => CollidesWith(polygon, box, Tolerance.Global);

        /// <summary>
        /// Checks whether a polygon overlaps an oriented box, within a tolerance.
        /// </summary>
        public static bool CollidesWith(GeoPolygon3 polygon, GeoObb3 box, Tolerance tolerance)
        {
            if (box == null)
            {
                throw new ArgumentNullException(nameof(box));
            }

            return CollidesWith(polygon, box.ToSolid(), tolerance);
        }

        /// <summary>
        /// Checks whether a face overlaps a solid, using the default tolerance.
        /// </summary>
        public static bool CollidesWith(GeoFace3 face, GeoSolid3 solid) => CollidesWith(face, solid, Tolerance.Global);

        /// <summary>
        /// Checks whether a face overlaps a solid, within a tolerance.
        /// </summary>
        /// <remarks>
        /// The face is read by its material and the body by its own, every opening the face can reach cut in, as a
        /// polygon is: they meet where an edge of the face, round its outline or round a hole, crosses the surface,
        /// where an edge of the body crosses the material, or where a corner of the face lies in the body. A hole is
        /// material that is not there, so a body reaching only into a hole touches nothing, while the rim of the hole
        /// is material and counts. A diamond standing on its corner on the top of the box 40 by 40 by 20 from the
        /// origin touches it whatever holes it has above.
        /// Whether a point is in the body is asked to within rounding, a billionth of a millimetre (1E-9, not nought),
        /// so that rounding in a body turned about in space cannot lose a crossing: a point within 1E-9 outside the
        /// body reads as in it, and a point 5E-10 outside a box is nought away from it.
        /// </remarks>
        public static bool CollidesWith(GeoFace3 face, GeoSolid3 solid, Tolerance tolerance)
        {
            if (face == null)
            {
                throw new ArgumentNullException(nameof(face));
            }

            if (solid == null)
            {
                throw new ArgumentNullException(nameof(solid));
            }

            if (!face.GetAabb().CollidesWith(solid.GetAabb(), tolerance))
            {
                return false;
            }

            // The faces run straight across every opening; read the material the region can reach instead.
            GeoSolid3 material = Material3.Near(solid, face.GetAabb(), tolerance);
            var corners = new List<GeoPoint3>(face.Boundary.Vertices);

            foreach (GeoPolygon3 hole in face.Holes)
            {
                corners.AddRange(hole.Vertices);
            }

            return ReachesMaterial(Gap3.Region.Of(face), corners, material, tolerance);
        }

        /// <summary>
        /// Determines whether a flat region reaches a body's material: it comes within the point tolerance of a face of
        /// the surface, or a corner of it lies in the material, which is where its distance to the body is no more than
        /// the point tolerance.
        /// </summary>
        internal static bool ReachesMaterial(
            Gap3.Region region, IEnumerable<GeoPoint3> corners, GeoSolid3 material, Tolerance tolerance)
        {
            if (Gap3.SurfaceRegion(material, region, Reach(tolerance), tolerance.EqualPoint) <= tolerance.EqualPoint)
            {
                return true;
            }

            foreach (GeoPoint3 corner in corners)
            {
                if (Gap3.Holds(material, corner))
                {
                    return true;
                }
            }

            return false;
        }
        #endregion
    }
}
