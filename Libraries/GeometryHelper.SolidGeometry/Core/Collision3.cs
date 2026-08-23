using System;
using GeometryHelper.CommonGeometry;
using GeometryHelper.CommonGeometry.Enums;
using GeometryHelper.SolidGeometry.Geometry;

namespace GeometryHelper.SolidGeometry.Core
{
    /// <summary>
    /// Provides static methods for deciding whether two 3D shapes overlap.
    /// <para>
    /// <c>CollidesWith</c> answers yes or no and says nothing about where. Shapes that merely touch count
    /// as colliding, so the answer agrees with <c>Distance3</c> reporting zero for the same pair. Where
    /// the shapes meet is <c>Intersection3</c>, and how far apart they are when they do not is
    /// <c>Distance3</c>.
    /// </para>
    /// </summary>
    public static class Collision3
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
        /// case where two boxes pass by each other at an angle with no face facing the gap.
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
            // projection written out for each pair, with the zero terms of the cross product dropped.
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

                    if (separation > reach1 + reach2 + slack)
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
        public static bool CollidesWith(GeoLine3 line, GeoObb3 box, Tolerance tolerance)
        {
            if (box == null)
            {
                throw new ArgumentNullException(nameof(box));
            }

            if (Containment3.Contains(box, line.StartPoint, tolerance) ||
                Containment3.Contains(box, line.EndPoint, tolerance))
            {
                return true;
            }

            return Intersection3.GetIntersections(line, box, tolerance).Length > 0;
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
        /// or for a pair of edges crossing.
        /// </remarks>
        public static bool CollidesWith(GeoTriangle3 triangle1, GeoTriangle3 triangle2, Tolerance tolerance)
        {
            if (triangle1.IsDegenerate(tolerance) || triangle2.IsDegenerate(tolerance))
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
                return false;
            }

            if (Math.Abs(triangle1.GetPlane().SignedDistanceTo(triangle2.A)) > tolerance.EqualPlanar)
            {
                return false;
            }

            for (int i = 0; i < 3; i++)
            {
                if (Containment3.Contains(triangle2, triangle1[i], tolerance) ||
                    Containment3.Contains(triangle1, triangle2[i], tolerance))
                {
                    return true;
                }
            }

            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    if (Distance3.DistanceTo(triangle1.GetEdgeAt(i), triangle2.GetEdgeAt(j), tolerance) <= tolerance.EqualPoint)
                    {
                        return true;
                    }
                }
            }

            return false;
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

            if (!solid1.GetAabb().CollidesWith(solid2.GetAabb(), tolerance))
            {
                return false;
            }

            GeoTriangle3[] mesh1 = solid1.Triangulate();
            GeoTriangle3[] mesh2 = solid2.Triangulate();

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
        /// A segment lying wholly inside the body touches it without crossing its surface, so containment
        /// is checked as well as crossing.
        /// </remarks>
        public static bool CollidesWith(GeoLine3 line, GeoSolid3 solid, Tolerance tolerance)
        {
            if (solid == null)
            {
                throw new ArgumentNullException(nameof(solid));
            }

            if (Containment3.Contains(solid, line.StartPoint, tolerance) ||
                Containment3.Contains(solid, line.EndPoint, tolerance))
            {
                return true;
            }

            return Intersection3.GetIntersections(line, solid, tolerance).Length > 0;
        }

        /// <summary>
        /// Checks whether a polyline reaches into a solid, using the default tolerance.
        /// </summary>
        public static bool CollidesWith(GeoPolyline3 polyline, GeoSolid3 solid) => CollidesWith(polyline, solid, Tolerance.Global);

        /// <summary>
        /// Checks whether a polyline reaches into a solid, within a tolerance.
        /// </summary>
        public static bool CollidesWith(GeoPolyline3 polyline, GeoSolid3 solid, Tolerance tolerance)
        {
            if (polyline == null)
            {
                throw new ArgumentNullException(nameof(polyline));
            }

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
        /// A flat region and a body meet in one of two ways: their surfaces cross, or the region lies
        /// wholly within the body without touching its boundary. The first is found by testing the
        /// triangles of each against the other, the second by testing one point of the region.
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

            GeoTriangle3[] region = polygon.Triangulate();
            GeoTriangle3[] body = solid.Triangulate();

            if (SurfacesTouch(region, body, tolerance))
            {
                return true;
            }

            return Containment3.Contains(solid, polygon.Centroid, tolerance);
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
        /// Only the outer boundary of the face is tested. A hole is material that is not there, so a body
        /// reaching a face only through one of its holes touches nothing — but a face whose holes were
        /// ignored would report a collision that is not real. The boundary test is therefore the
        /// conservative one, and is refined by asking whether the meeting place is over a hole.
        /// </remarks>
        public static bool CollidesWith(GeoFace3 face, GeoSolid3 solid, Tolerance tolerance)
        {
            if (face == null)
            {
                throw new ArgumentNullException(nameof(face));
            }

            if (!CollidesWith(face.Boundary, solid, tolerance))
            {
                return false;
            }

            if (face.Holes.Count == 0)
            {
                return true;
            }

            // The body reaches the outer boundary somewhere. It only counts if it reaches material rather
            // than a hole, so the crossing points of the body edges against the face are checked.
            foreach (GeoTriangle3 triangle in solid.Triangulate())
            {
                for (int i = 0; i < 3; i++)
                {
                    if (Intersection3.TryIntersectWith(triangle.GetEdgeAt(i), face, out _, tolerance))
                    {
                        return true;
                    }
                }
            }

            // No edge of the body pierces the material, so either the face sits inside the body or the
            // contact was over a hole.
            return Containment3.Contains(solid, face.Boundary.Centroid, tolerance) &&
                   face.Contains(face.Boundary.Centroid, tolerance);
        }

        #endregion
    }
}
