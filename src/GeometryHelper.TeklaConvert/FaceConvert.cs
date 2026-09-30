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
        /// Converts one face of a Tekla solid.
        /// </summary>
        /// <param name="face">The face to read.</param>
        /// <param name="tolerance">The tolerance deciding flatness and duplicate vertices.</param>
        /// <param name="result">The converted face when the method returns true.</param>
        /// <returns>false when the face carries no usable outer loop.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="face"/> is null.</exception>
        /// <remarks>
        /// The first loop is the outer edge and the rest are holes, which is the order Tekla walks them in.
        /// A hole that cannot be read is dropped while the face is kept: losing a bolt hole understates the
        /// hole, whereas losing the face would put a gap in the body. A face out of flat is refused, and a hole off
        /// its plane dropped, either of which leaves its body open; <see cref="TryReadFaces(TSS.Face, Tolerance, out GeoFace3[])"/>
        /// keeps both, as triangles, and is what a whole solid is read with.
        /// </remarks>
        public static bool TryReadFace(this TSS.Face face, Tolerance tolerance, out GeoFace3 result)
        {
            if (face == null)
            {
                throw new ArgumentNullException(nameof(face));
            }

            result = null;

            GeoVector3 outward = face.Normal.ToGeoVector3();

            List<GeoPolygon3> loops = new List<GeoPolygon3>();

            TSS.LoopEnumerator loopEnumerator = face.GetLoopEnumerator();

            while (loopEnumerator.MoveNext())
            {
                TSS.Loop loop = loopEnumerator.Current as TSS.Loop;

                if (loop == null)
                {
                    continue;
                }

                if (loop.TryReadLoop(outward, tolerance, out GeoPolygon3 polygon))
                {
                    loops.Add(polygon);
                }
                else if (loops.Count == 0)
                {
                    // Without an outer edge there is no face to build; a later loop failing only costs a hole.
                    return false;
                }
            }

            if (loops.Count == 0)
            {
                return false;
            }

            GeoPolygon3 boundary = loops[0];
            List<GeoPolygon3> holes = new List<GeoPolygon3>();

            for (int i = 1; i < loops.Count; i++)
            {
                holes.Add(loops[i]);
            }

            try
            {
                result = new GeoFace3(boundary, holes, tolerance);
                return true;
            }
            catch (ArgumentException)
            {
                // A hole that does not sit on the plane of the boundary is the usual cause. The face is
                // still worth keeping without it, since a gap in the surface costs more than a lost hole.
                result = new GeoFace3(boundary);
                return true;
            }
        }

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
