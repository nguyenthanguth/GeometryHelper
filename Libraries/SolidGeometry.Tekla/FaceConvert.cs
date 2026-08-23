using CommonGeometry;
using SolidGeometry.Geometry;
using System;
using System.Collections.Generic;
using TSS = Tekla.Structures.Solid;

namespace SolidGeometry.Tekla
{
    /// <summary>
    /// Provides extension methods to convert faces from Tekla Structures Solids to SolidGeometry.
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
        /// hole, whereas losing the face would put a gap in the body.
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
    }
}
