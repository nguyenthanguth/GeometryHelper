using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    /// <summary>
    /// Where two bodies lie against each other, face to face.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two bodies that only touch share no volume, so <see cref="TryIntersect(GeoSolid3, GeoSolid3, out GeoSolid3, Tolerance)"/>
    /// says false and <see cref="Collision3.CollidesWith(GeoSolid3, GeoSolid3, Tolerance)"/> says true, and
    /// neither says where. A column standing on a footing, a plate bolted to a flange: what bears on what is the
    /// patch where the two surfaces lie against each other.
    /// </para>
    /// <para>
    /// Two faces are in contact where they lie in one plane and face <b>opposite</b> ways — back to back, one
    /// body on each side. Two faces flush and facing the same way are side by side, not in contact. The patch is
    /// the area the two faces share, which the plane library works out exactly through the coplanar lift, and
    /// the patches in one plane are joined, so a contact drawn across several faces comes back as the one
    /// region it is.
    /// </para>
    /// <para>
    /// Both bodies are read as their material first, so a column standing over a bolt hole in a base plate does
    /// not report the hole as bearing. Only contact with area is a face: two bodies meeting along an edge or at
    /// a point have no patch, and the return type says so rather than inventing a face of no area.
    /// </para>
    /// </remarks>
    public static partial class Boolean3
    {
        #region Contact

        /// <summary>
        /// Gets the patches where two bodies lie against each other, face to face.
        /// </summary>
        public static bool TryGetContact(GeoSolid3 first, GeoSolid3 second, out GeoFace3[] contact)
            => TryGetContact(first, second, out contact, Tolerance.Global);

        /// <summary>
        /// Gets the patches where two bodies lie against each other, face to face, within a tolerance.
        /// </summary>
        /// <param name="first">The first body.</param>
        /// <param name="second">The second body.</param>
        /// <param name="contact">
        /// The patches, each lying on a face of the first body and facing the way that face does; empty when
        /// the method returns false.
        /// </param>
        /// <param name="tolerance">The tolerance deciding what counts as one plane.</param>
        /// <returns>true when the two lie against each other over some area; otherwise, false.</returns>
        /// <exception cref="ArgumentNullException">Thrown when either body is null.</exception>
        public static bool TryGetContact(GeoSolid3 first, GeoSolid3 second, out GeoFace3[] contact, Tolerance tolerance)
        {
            Guard(first, second);

            contact = new GeoFace3[0];

            GeoAabb3 firstBox = first.GetAabb();
            GeoAabb3 secondBox = second.GetAabb();

            if (!firstBox.CollidesWith(secondBox, tolerance))
            {
                return false;
            }

            GeoSolid3 ours = Material3.Near(first, secondBox, tolerance);
            GeoSolid3 theirs = Material3.Near(second, firstBox, tolerance);

            // The patches found so far, grouped by the plane they lie in, so that each plane's are joined.
            var byPlane = new List<List<GeoFace3>>();
            var planes = new List<GeoPlane3>();

            foreach (GeoFace3 face in ours.Faces)
            {
                GeoAabb3 faceBox = face.GetAabb();
                GeoVector3 normal = face.Boundary.Normal;
                GeoPlane3 plane = face.GetPlane();

                foreach (GeoFace3 other in theirs.Faces)
                {
                    if (normal.DotProduct(other.Boundary.Normal) >= 0.0
                        || !faceBox.CollidesWith(other.GetAabb(), tolerance)
                        || !SharesPlane(plane, other.GetPlane(), other.Boundary.Vertices[0], tolerance))
                    {
                        continue;
                    }

                    foreach (GeoFace3 patch in Intersect(face, other, tolerance))
                    {
                        if (patch.Area > tolerance.EqualPoint * tolerance.EqualPoint)
                        {
                            Collect(byPlane, planes, plane, patch, tolerance);
                        }
                    }
                }
            }

            var joined = new List<GeoFace3>();

            foreach (List<GeoFace3> patches in byPlane)
            {
                joined.AddRange(JoinPatches(patches, tolerance));
            }

            contact = joined.ToArray();

            return contact.Length > 0;
        }

        /// <summary>
        /// Files a patch with the others lying in the same plane.
        /// </summary>
        private static void Collect(List<List<GeoFace3>> byPlane, List<GeoPlane3> planes, GeoPlane3 plane, GeoFace3 patch, Tolerance tolerance)
        {
            for (int i = 0; i < planes.Count; i++)
            {
                if (planes[i].IsEqualTo(plane, tolerance))
                {
                    byPlane[i].Add(patch);
                    return;
                }
            }

            planes.Add(plane);
            byPlane.Add(new List<GeoFace3> { patch });
        }

        /// <summary>
        /// Joins the patches of one plane into the regions they make together.
        /// </summary>
        /// <remarks>
        /// A contact spread across several faces of either body comes back from the face pairs in pieces, and
        /// pieces that touch are one region.
        /// </remarks>
        private static List<GeoFace3> JoinPatches(List<GeoFace3> patches, Tolerance tolerance)
        {
            var regions = new List<GeoFace3>(patches);
            bool merged = true;

            while (merged && regions.Count > 1)
            {
                merged = false;

                for (int i = 0; i < regions.Count && !merged; i++)
                {
                    for (int j = i + 1; j < regions.Count && !merged; j++)
                    {
                        // Patches whose boxes are apart cannot join; for the rest, joining them is the test --
                        // two that touch come back as one region, two that do not come back as two.
                        if (!regions[i].GetAabb().CollidesWith(regions[j].GetAabb(), tolerance))
                        {
                            continue;
                        }

                        GeoFace3[] union = Union(regions[i], regions[j], tolerance);

                        if (union.Length == 1)
                        {
                            regions[i] = union[0];
                            regions.RemoveAt(j);
                            merged = true;
                        }
                    }
                }
            }

            return regions;
        }

        #endregion
    }
}
