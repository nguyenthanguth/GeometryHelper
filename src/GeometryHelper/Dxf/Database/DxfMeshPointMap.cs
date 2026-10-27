using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// A point of the mesh a drawing's geographic location is fitted by, as AutoCAD's <c>MeshPointMap</c>: a point of the
    /// drawing and the point of the coordinate system it is taken to (groups 13 and 14 of a GEODATA of version 2).
    /// </summary>
    public readonly struct DxfMeshPointMap
    {
        /// <summary>
        /// Initializes a point of the mesh, as AutoCAD's <c>MeshPointMap(sourcePt, destPt)</c>.
        /// </summary>
        /// <param name="sourcePoint">The point of the drawing.</param>
        /// <param name="destPoint">The point of the coordinate system it is taken to.</param>
        public DxfMeshPointMap(GeoPoint2 sourcePoint, GeoPoint2 destPoint)
        {
            SourcePoint = sourcePoint;
            DestPoint = destPoint;
        }

        /// <summary>Gets the point of the drawing, as AutoCAD's <c>SourcePoint</c>.</summary>
        public GeoPoint2 SourcePoint { get; }

        /// <summary>Gets the point of the coordinate system the point of the drawing is taken to, as AutoCAD's <c>DestPoint</c>.</summary>
        public GeoPoint2 DestPoint { get; }

        /// <summary>Gives the point of the mesh as a line for debugging: the two points.</summary>
        /// <returns>The line, such as <c>(0, 0) -&gt; (500000, 4100000)</c>.</returns>
        public override string ToString() => SourcePoint + " -> " + DestPoint;
    }
}
