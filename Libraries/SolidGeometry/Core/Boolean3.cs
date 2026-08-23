using System;
using System.Collections.Generic;
using CommonGeometry;
using CommonGeometry.Enums;
using SolidGeometry.Geometry;

namespace SolidGeometry.Core
{
    /// <summary>
    /// Combines solids: the union of two bodies, the part they share, and one taken out of the other.
    /// <para>
    /// The method is the same one <c>Splition3</c> uses to cut a plate against a body, carried up a
    /// dimension. Both bodies are divided by one shared set of planes — the face planes of each of them
    /// together — which leaves cells that are each wholly inside or wholly outside the other body, since
    /// the surface of a body never leaves the planes of its own faces. The cells wanted for the operation
    /// are then glued back together: a face shared by two kept cells appears twice, once each way round,
    /// and dropping both leaves exactly the outer skin.
    /// </para>
    /// <para>
    /// Using one shared set of planes for both bodies rather than each against the other is what makes
    /// the gluing work where they meet. Cut that way, the two sides of the interface are the same plane
    /// carved by the same knives, so they come out as the same polygon and cancel. Cut each against the
    /// other only, the two sides are subdivided differently and the interface survives as a wall inside
    /// the result.
    /// </para>
    /// <para>
    /// Dividing A by the planes of B already lays a face along every part of the surface of B that runs
    /// through A. That is why an intersection is just the cells of A that fall inside B, and a difference
    /// just the cells of A that fall outside it: the walls of the cavity are already there, and adding the
    /// faces of B on top of them would describe the same surface twice.
    /// </para>
    /// <para>
    /// Every operation reports <c>false</c> when the answer is nothing at all — two bodies that do not
    /// touch have no shared part, and a body wholly swallowed by another leaves nothing behind. That is
    /// an outcome rather than a failure, which is why it comes back as <c>false</c> with no result rather
    /// than as an exception or an empty body.
    /// </para>
    /// </summary>
    public static class Boolean3
    {
        /// <summary>
        /// Joins two solids into one, using the default tolerance.
        /// </summary>
        public static bool TryUnion(GeoSolid3 first, GeoSolid3 second, out GeoSolid3 result)
        {
            return TryUnion(first, second, out result, Tolerance.Global);
        }

        /// <summary>
        /// Joins two solids into one, within a tolerance.
        /// </summary>
        /// <param name="first">The first body.</param>
        /// <param name="second">The second body.</param>
        /// <param name="result">The combined body.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>false when the two could not be combined into a closed body.</returns>
        /// <remarks>
        /// Two bodies that do not touch still combine: the result is one solid carrying both shells, which
        /// measures and answers containment correctly because each shell is closed and wound outwards.
        /// </remarks>
        public static bool TryUnion(GeoSolid3 first, GeoSolid3 second, out GeoSolid3 result, Tolerance tolerance)
        {
            Guard(first, second);

            result = null;

            if (!first.GetAabb().CollidesWith(second.GetAabb(), tolerance))
            {
                // Nothing to resolve: the two shells simply sit side by side.
                List<GeoFace3> apart = new List<GeoFace3>(first.Faces);
                apart.AddRange(second.Faces);

                result = new GeoSolid3(apart);
                return true;
            }

            List<GeoPlane3> planes = SharedPlanes(first, second, tolerance);

            List<GeoFace3> kept = new List<GeoFace3>();

            // All of the first body, and only the part of the second that reaches beyond it: the shared
            // region belongs to the union once, and it is already carried by the first.
            foreach (GeoSolid3 cell in SplitIntoCells(first, planes, tolerance))
            {
                kept.AddRange(cell.Faces);
            }

            kept.AddRange(FacesOfCells(SplitIntoCells(second, planes, tolerance), first, false, tolerance));

            return TryGlue(kept, tolerance, out result);
        }

        /// <summary>
        /// Gets the part two solids have in common, using the default tolerance.
        /// </summary>
        public static bool TryIntersect(GeoSolid3 first, GeoSolid3 second, out GeoSolid3 result)
        {
            return TryIntersect(first, second, out result, Tolerance.Global);
        }

        /// <summary>
        /// Gets the part two solids have in common, within a tolerance.
        /// </summary>
        /// <returns>false when the two bodies share no volume.</returns>
        public static bool TryIntersect(GeoSolid3 first, GeoSolid3 second, out GeoSolid3 result, Tolerance tolerance)
        {
            Guard(first, second);

            result = null;

            if (!first.GetAabb().CollidesWith(second.GetAabb(), tolerance))
            {
                return false;
            }

            List<GeoPlane3> planes = SharedPlanes(first, second, tolerance);

            List<GeoFace3> kept = FacesOfCells(SplitIntoCells(first, planes, tolerance), second, true, tolerance);

            return TryGlue(kept, tolerance, out result);
        }

        /// <summary>
        /// Takes one solid out of another, using the default tolerance.
        /// </summary>
        public static bool TrySubtract(GeoSolid3 subject, GeoSolid3 tool, out GeoSolid3 result)
        {
            return TrySubtract(subject, tool, out result, Tolerance.Global);
        }

        /// <summary>
        /// Takes one solid out of another, within a tolerance.
        /// </summary>
        /// <param name="subject">The body to cut material from.</param>
        /// <param name="tool">The body to remove.</param>
        /// <param name="result">What is left of the subject.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>false when nothing is left, because the tool swallowed the subject whole.</returns>
        /// <remarks>
        /// The walls of the cavity are not taken from the tool; they are already there. Dividing the
        /// subject by the planes of the tool lays a face along every part of the surface of the tool that
        /// passes through it, so keeping the cells that fall outside the tool keeps those faces with them.
        /// A tool that misses the subject changes nothing, and the subject comes back unaltered.
        /// </remarks>
        public static bool TrySubtract(GeoSolid3 subject, GeoSolid3 tool, out GeoSolid3 result, Tolerance tolerance)
        {
            Guard(subject, tool);

            result = null;

            if (!subject.GetAabb().CollidesWith(tool.GetAabb(), tolerance))
            {
                result = subject;
                return true;
            }

            List<GeoPlane3> planes = SharedPlanes(subject, tool, tolerance);

            List<GeoFace3> kept = FacesOfCells(SplitIntoCells(subject, planes, tolerance), tool, false, tolerance);

            return TryGlue(kept, tolerance, out result);
        }

        #region Machinery

        /// <summary>
        /// Rejects null arguments for every operation in one place.
        /// </summary>
        private static void Guard(GeoSolid3 first, GeoSolid3 second)
        {
            if (first == null)
            {
                throw new ArgumentNullException(nameof(first));
            }

            if (second == null)
            {
                throw new ArgumentNullException(nameof(second));
            }
        }

        /// <summary>
        /// Collects the face planes of both bodies, with duplicates removed.
        /// </summary>
        /// <remarks>
        /// Both bodies are cut by this one set rather than each by the other. That costs a few more cells
        /// and buys the thing that makes gluing work: where the two bodies meet, both sides have been
        /// carved by the same knives, so the two faces at the interface are the same polygon and cancel.
        /// </remarks>
        private static List<GeoPlane3> SharedPlanes(GeoSolid3 first, GeoSolid3 second, Tolerance tolerance)
        {
            List<GeoPlane3> planes = Splition3.CollectFacePlanes(first, tolerance);

            foreach (GeoPlane3 plane in Splition3.CollectFacePlanes(second, tolerance))
            {
                bool known = false;

                foreach (GeoPlane3 existing in planes)
                {
                    if (existing.IsEqualTo(plane, tolerance) || existing.IsEqualTo(plane.Flip(), tolerance))
                    {
                        known = true;
                        break;
                    }
                }

                if (!known)
                {
                    planes.Add(plane);
                }
            }

            return planes;
        }

        /// <summary>
        /// Divides a body by a set of planes, so that no piece straddles any of them.
        /// </summary>
        /// <remarks>
        /// A plane that does not actually cut a cell leaves it alone, so the number of pieces grows only
        /// with the number of planes that really pass through the body rather than with how many were
        /// offered.
        /// </remarks>
        private static List<GeoSolid3> SplitIntoCells(GeoSolid3 subject, List<GeoPlane3> planes, Tolerance tolerance)
        {
            List<GeoSolid3> cells = new List<GeoSolid3> { subject };

            foreach (GeoPlane3 plane in planes)
            {
                List<GeoSolid3> divided = new List<GeoSolid3>();

                foreach (GeoSolid3 cell in cells)
                {
                    if (Splition3.TrySplitBy(cell, plane, out GeoSolid3 above, out GeoSolid3 below, tolerance))
                    {
                        divided.Add(above);
                        divided.Add(below);
                    }
                    else
                    {
                        divided.Add(cell);
                    }
                }

                cells = divided;
            }

            return cells;
        }

        /// <summary>
        /// Collects the faces of the cells that lie on the wanted side of another body.
        /// </summary>
        /// <param name="cells">The cells to sort.</param>
        /// <param name="against">The body deciding inside from outside.</param>
        /// <param name="wantInside">true to keep the cells within that body, false to keep those beyond it.</param>
        /// <param name="tolerance">The tolerance.</param>
        private static List<GeoFace3> FacesOfCells(List<GeoSolid3> cells, GeoSolid3 against, bool wantInside, Tolerance tolerance)
        {
            List<GeoFace3> faces = new List<GeoFace3>();

            foreach (GeoSolid3 cell in cells)
            {
                if (!TryGetInteriorPoint(cell, tolerance, out GeoPoint3 sample))
                {
                    continue;
                }

                bool within = Containment3.Locate(against, sample, tolerance) == PointLocation.Inside;

                if (within == wantInside)
                {
                    faces.AddRange(cell.Faces);
                }
            }

            return faces;
        }

        /// <summary>
        /// Finds a point strictly inside a body.
        /// </summary>
        /// <remarks>
        /// The centroid of a concave body can fall outside it, so it is checked rather than trusted. When
        /// it fails, the search steps a little way inwards from the middle of each surface triangle, along
        /// the inward normal. The step shrinks on each round because a body can be thinner in one place
        /// than the first step assumes, and a step that overshoots comes out the far side.
        /// </remarks>
        private static bool TryGetInteriorPoint(GeoSolid3 solid, Tolerance tolerance, out GeoPoint3 point)
        {
            point = GeoPoint3.Origin;

            GeoPoint3 centroid = solid.Centroid;

            if (Containment3.Locate(solid, centroid, tolerance) == PointLocation.Inside)
            {
                point = centroid;
                return true;
            }

            double reach = solid.GetAabb().Diagonal.Length;

            if (reach <= 0.0)
            {
                return false;
            }

            GeoTriangle3[] mesh = solid.Triangulate();

            for (double fraction = 1E-2; fraction >= 1E-5; fraction *= 0.1)
            {
                foreach (GeoTriangle3 triangle in mesh)
                {
                    if (triangle.IsDegenerate(tolerance))
                    {
                        continue;
                    }

                    GeoPoint3 candidate = triangle.Centroid.Subtract(triangle.Normal.Multiply(reach * fraction));

                    if (Containment3.Locate(solid, candidate, tolerance) == PointLocation.Inside)
                    {
                        point = candidate;
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Glues a collection of cell faces into one closed body.
        /// </summary>
        /// <remarks>
        /// A face between two cells that were both kept is interior to the result, and it appears twice
        /// among the collected faces, once each way round. Dropping both leaves exactly the outer skin.
        /// The survivors are then merged where they are coplanar and touching, which undoes the
        /// subdivision the cutting introduced.
        /// </remarks>
        private static bool TryGlue(List<GeoFace3> faces, Tolerance tolerance, out GeoSolid3 result)
        {
            result = null;

            bool[] dropped = new bool[faces.Count];

            for (int i = 0; i < faces.Count; i++)
            {
                if (dropped[i])
                {
                    continue;
                }

                for (int j = i + 1; j < faces.Count; j++)
                {
                    if (dropped[j])
                    {
                        continue;
                    }

                    if (faces[i].Boundary.IsEqualTo(faces[j].Boundary.Flip(), tolerance))
                    {
                        dropped[i] = true;
                        dropped[j] = true;
                        break;
                    }
                }
            }

            List<GeoFace3> skin = new List<GeoFace3>();

            for (int i = 0; i < faces.Count; i++)
            {
                if (!dropped[i])
                {
                    skin.Add(faces[i]);
                }
            }

            if (skin.Count < 4)
            {
                return false;
            }

            result = Merge3.CoplanarFaces(new GeoSolid3(skin), tolerance);
            return true;
        }

        #endregion
    }
}
