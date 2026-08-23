using System;
using System.Collections.Generic;
using GeometryHelper.CommonGeometry;
using GeometryHelper.SolidGeometry.Geometry;
using TSG = Tekla.Structures.Geometry3d;
using TSS = Tekla.Structures.Solid;

namespace GeometryHelper.TeklaConvert
{
    /// <summary>
    /// Provides extension methods to convert loops from Tekla Structures Solids to GeometryHelper.SolidGeometry.
    /// </summary>
    public static class LoopConvert
    {
        /// <summary>
        /// Converts one loop of a Tekla face into a polygon facing a given way.
        /// </summary>
        /// <param name="loop">The Tekla loop to convert.</param>
        /// <param name="outward">The target normal direction of the face containing this loop.</param>
        /// <param name="tolerance">The tolerance for planar flatness and co-directional checking.</param>
        /// <param name="result">The converted <see cref="GeoPolygon3"/> when the method returns true.</param>
        /// <returns>true if the loop was successfully read; false otherwise.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="loop"/> is null.</exception>
        /// <remarks>
        /// Tekla does not promise which way round a loop is walked, so the polygon is turned to agree with
        /// the face normal rather than assumed to already. Holes are turned the same way as the boundary,
        /// which is the convention this library keeps so that area and volume come out by subtraction.
        /// </remarks>
        public static bool TryReadLoop(this TSS.Loop loop, GeoVector3 outward, Tolerance tolerance, out GeoPolygon3 result)
        {
            if (loop == null)
            {
                throw new ArgumentNullException(nameof(loop));
            }

            result = null;

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

            if (vertices.Count < 3)
            {
                return false;
            }

            try
            {
                GeoPolygon3 polygon = new GeoPolygon3(vertices, tolerance);

                result = polygon.Normal.IsCodirectionalTo(outward, tolerance) ? polygon : polygon.Flip();
                return true;
            }
            catch (ArgumentException)
            {
                // Too few distinct vertices, all of them in a line, or not flat enough to be a polygon.
                return false;
            }
        }
    }
}
