using GeometryHelper;
using GeometryHelper.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using TSS = Tekla.Structures.Solid;

namespace GeometryHelper.TeklaConvert
{
    /// <summary>
    /// Provides extension methods to convert faces from Tekla Structures Solids to GeometryHelper.
    /// </summary>
    public static class FaceConvert
    {
        /// <summary>
        /// Converts one face of a Tekla solid into the faces that cover it: one face where it lies flat, and triangles on
        /// its own corners where it does not.
        /// </summary>
        /// <param name="face">The face to read.</param>
        /// <param name="tolerance">The tolerance deciding duplicate corners and flatness.</param>
        /// <param name="result">The faces, turned to agree with the normal Tekla gives the face, when the method returns true.</param>
        /// <returns>false when the face encloses no area.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="face"/> is null.</exception>
        /// <remarks>
        /// Tekla's own cuts leave faces a little out of flat. A face further out than the planar tolerance, or with a hole
        /// off its plane, is not refused but comes as triangles on the corners Tekla gave (see
        /// <see cref="GeoFace3.FromLoops(IEnumerable{GeoPoint3}, IEnumerable{IEnumerable{GeoPoint3}}, Tolerance)"/>), which
        /// keep every edge it shares with its neighbours, so a solid read this way is as closed as Tekla holds it. The first
        /// loop is the boundary and the rest are holes, the order Tekla walks them in; a hole with no area is left out.
        /// There is no reader of one face only: a face out of flat cannot be one face without moving its corners off
        /// the edges it shares, and refusing it, or dropping its hole, would leave the body open.
        /// </remarks>
        public static bool TryReadFaces(this TSS.Face face, Tolerance tolerance, out GeoFace3[] result)
        {
            if (face == null)
            {
                throw new ArgumentNullException(nameof(face));
            }

            List<IReadOnlyList<GeoPoint3>> loops = new List<IReadOnlyList<GeoPoint3>>();

            TSS.LoopEnumerator loopEnumerator = face.GetLoopEnumerator();

            while (loopEnumerator.MoveNext())
            {
                if (loopEnumerator.Current is TSS.Loop loop)
                {
                    loops.Add(loop.ReadCorners());
                }
            }

            return TryReadFaces(face.Normal.ToGeoVector3(), loops, tolerance, out result);
        }

        /// <summary>
        /// Makes the faces of one Tekla face from its loops of corners, boundary first, and the normal Tekla gives it.
        /// </summary>
        internal static bool TryReadFaces(GeoVector3 outward, IReadOnlyList<IReadOnlyList<GeoPoint3>> loops, Tolerance tolerance, out GeoFace3[] result)
        {
            result = Array.Empty<GeoFace3>();

            if (loops.Count == 0)
            {
                return false;
            }

            GeoFace3[] faces = GeoFace3.FromLoops(loops[0], loops.Skip(1), tolerance);

            if (faces.Length == 0)
            {
                // Loops that cannot be split, seen along their normal: a boundary crossing itself, or a hole reaching out
                // of it. The boundary alone, when it is flat, still covers the face, as a face was read before.
                if (!TryBoundaryAlone(loops[0], tolerance, out GeoFace3 boundary))
                {
                    return false;
                }

                faces = new[] { boundary };
            }

            // Tekla does not promise which way round a loop is walked, so the faces are turned to agree with the normal it
            // gives the face, all together: the triangles of a face out of flat face the same way.
            if (Facing(faces).DotProduct(outward) < 0.0)
            {
                for (int i = 0; i < faces.Length; i++)
                {
                    faces[i] = faces[i].Flip();
                }
            }

            result = faces;
            return true;
        }

        /// <summary>
        /// Gets the way a set of faces faces as a whole: each normal weighted by its area.
        /// </summary>
        private static GeoVector3 Facing(GeoFace3[] faces)
        {
            GeoVector3 total = GeoVector3.Zero;

            foreach (GeoFace3 face in faces)
            {
                total = total.Add(face.Normal.Multiply(face.Area));
            }

            return total;
        }

        private static bool TryBoundaryAlone(IReadOnlyList<GeoPoint3> corners, Tolerance tolerance, out GeoFace3 face)
        {
            try
            {
                face = new GeoFace3(new GeoPolygon3(corners, tolerance));
                return true;
            }
            catch (ArgumentException)
            {
                face = null;
                return false;
            }
        }
    }
}
