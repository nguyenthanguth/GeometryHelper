using System;
using System.Collections.Generic;
using GeometryHelper;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    /// <summary>
    /// Turns loops of corners, as a modeller gives the faces of a body, into faces: one face where the loops lie flat,
    /// triangles on their own corners where they do not.
    /// </summary>
    /// <remarks>
    /// A face in this library is flat, and a polygon refuses corners out of flat. The faces of a closed body can still
    /// come that way: Tekla Structures left the top face beside a notch in a beam 0.04 mm out at one corner, and
    /// refused, that face was a hole in the body, which then gave no section and the wrong volume. Split into triangles
    /// on the corners it came with, each exactly flat, the face keeps every edge it shares with its neighbours, so the
    /// body is as closed as it was given, and no corner moves. Moving the corners onto one plane would not do: a corner
    /// is shared with the faces round it, and they would come out of flat instead.
    /// </remarks>
    internal static class Loops3
    {
        /// <summary>
        /// Makes the faces that cover a boundary less its holes.
        /// </summary>
        /// <remarks>See <see cref="GeoFace3.FromLoops(IEnumerable{GeoPoint3}, IEnumerable{IEnumerable{GeoPoint3}}, Tolerance)"/>.</remarks>
        internal static GeoFace3[] ToFaces(IEnumerable<GeoPoint3> boundary, IEnumerable<IEnumerable<GeoPoint3>> holes, Tolerance tolerance)
        {
            if (boundary == null)
            {
                throw new ArgumentNullException(nameof(boundary));
            }

            List<GeoPoint3> outer = Distinct(boundary, tolerance);
            var rings = new List<IReadOnlyList<GeoPoint3>>();

            if (holes != null)
            {
                foreach (IEnumerable<GeoPoint3> hole in holes)
                {
                    if (hole == null)
                    {
                        throw new ArgumentException("A face cannot carry a null hole.", nameof(holes));
                    }

                    List<GeoPoint3> ring = Distinct(hole, tolerance);

                    // A hole with no area takes nothing away, and has no wall round it for the face to meet.
                    if (Encloses(ring, tolerance, out _))
                    {
                        rings.Add(ring);
                    }
                }
            }

            if (!Encloses(outer, tolerance, out GeoVector3 normal))
            {
                return Array.Empty<GeoFace3>();
            }

            if (TryFlat(outer, rings, tolerance, out GeoFace3 face))
            {
                return new[] { face };
            }

            // Seen along the boundary's own normal, which follows its winding as a polygon's does, so the triangles face
            // the way the one face would have.
            var frame = new GeoCoordinateSystem3(new GeoPlane3(outer[0], normal));

            if (!EarClipping.TryTriangulate(outer, rings, frame, tolerance, out GeoTriangle3[] triangles))
            {
                return Array.Empty<GeoFace3>();
            }

            var faces = new List<GeoFace3>(triangles.Length);

            foreach (GeoTriangle3 triangle in triangles)
            {
                try
                {
                    faces.Add(triangle.ToFace3(tolerance));
                }
                catch (ArgumentException)
                {
                    // A triangle with no area covers nothing, and its neighbours meet along its edges without it.
                }
            }

            return faces.ToArray();
        }

        /// <summary>
        /// Gets the corners of a loop without consecutive duplicates or a repeated closing corner, as a polygon keeps them.
        /// </summary>
        private static List<GeoPoint3> Distinct(IEnumerable<GeoPoint3> corners, Tolerance tolerance)
        {
            var kept = new List<GeoPoint3>();

            foreach (GeoPoint3 corner in corners)
            {
                if (kept.Count == 0 || !kept[kept.Count - 1].IsEqualTo(corner, tolerance))
                {
                    kept.Add(corner);
                }
            }

            while (kept.Count > 1 && kept[kept.Count - 1].IsEqualTo(kept[0], tolerance))
            {
                kept.RemoveAt(kept.Count - 1);
            }

            return kept;
        }

        /// <summary>
        /// Determines whether a loop encloses an area, by the test a polygon puts it to, and which way it faces.
        /// </summary>
        private static bool Encloses(List<GeoPoint3> loop, Tolerance tolerance, out GeoVector3 normal)
        {
            normal = default;

            return loop.Count >= 3 && Newell.GetAreaVector(loop).TryGetNormal(out normal, tolerance);
        }

        /// <summary>
        /// Makes one face of the loops as the constructors would, when every loop lies flat on the plane of the boundary.
        /// </summary>
        private static bool TryFlat(List<GeoPoint3> outer, List<IReadOnlyList<GeoPoint3>> rings, Tolerance tolerance, out GeoFace3 face)
        {
            face = null;

            try
            {
                var boundary = new GeoPolygon3(outer, tolerance);
                var holes = new List<GeoPolygon3>(rings.Count);

                foreach (IReadOnlyList<GeoPoint3> ring in rings)
                {
                    holes.Add(new GeoPolygon3(ring, tolerance));
                }

                face = new GeoFace3(boundary, holes, tolerance);
                return true;
            }
            catch (ArgumentException)
            {
                // Out of flat: a loop off its own plane, or a hole off the boundary's.
                return false;
            }
        }
    }
}
