using System.Collections.Generic;
using GeometryHelper.Geometry;
using TSG = Tekla.Structures.Geometry3d;
using TSS = Tekla.Structures.Solid;

namespace GeometryHelper.TeklaConvert
{
    /// <summary>
    /// Reads the loops of Tekla Structures Solids.
    /// </summary>
    /// <remarks>
    /// A loop is read as corners, not as a polygon: a loop out of flat has no polygon that keeps its corners, and one
    /// that moved them would no longer meet the faces beside it. <see cref="FaceConvert.TryReadFaces(TSS.Face, Tolerance, out GeoFace3[])"/>
    /// makes faces of them.
    /// </remarks>
    internal static class LoopConvert
    {
        /// <summary>
        /// Reads the corners of a Tekla loop, in the order Tekla walks them.
        /// </summary>
        internal static List<GeoPoint3> ReadCorners(this TSS.Loop loop)
        {
            List<GeoPoint3> vertices = new List<GeoPoint3>();

            TSS.VertexEnumerator vertexEnumerator = loop.GetVertexEnumerator();

            while (vertexEnumerator.MoveNext())
            {
                TSG.Point point = vertexEnumerator.Current as TSG.Point;

                if (point != null)
                {
                    vertices.Add(point.ToGeoPoint3());
                }
            }

            return vertices;
        }
    }
}
