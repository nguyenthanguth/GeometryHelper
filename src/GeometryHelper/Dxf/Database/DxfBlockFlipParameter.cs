using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// A flip parameter of a dynamic block, as <see cref="DxfBlockTableRecord.AddFlipParameter"/> wrote it: a line the block's
    /// entities are mirrored about, shown on every INSERT of the block as the dynamic property of its name, 0 not flipped and
    /// 1 flipped, and followed by the flip actions tied to it (<see cref="DxfBlockTableRecord.AddFlipAction"/>).
    /// </summary>
    /// <remarks>
    /// The parameter Hand of the line from (0, 0, 0) to (0, 50, 0) gives each INSERT the property Hand of 0; set to 1, by
    /// AutoCAD or by <see cref="DxfDynamicBlockReferenceProperty.Value"/>, it mirrors the entities of a flip action tied to it
    /// about the y axis, a line from (10, 0) to (40, 0) to one from (-10, 0) to (-40, 0). What it holds is read once it is
    /// written: changing the block afterwards does not change it.
    /// </remarks>
    public sealed class DxfBlockFlipParameter
    {
        internal DxfBlockFlipParameter(DxfBlockTableRecord block, int node, string handle, string name, string description, GeoPoint3 basePoint, GeoPoint3 endPoint)
        {
            Block = block;
            Node = node;
            Handle = handle;
            Name = name;
            Description = description;
            BasePoint = basePoint;
            EndPoint = endPoint;
        }

        /// <summary>The block the parameter is of.</summary>
        internal DxfBlockTableRecord Block { get; }

        /// <summary>The id of the parameter's node in the block's evaluation graph (90), which its actions name.</summary>
        internal int Node { get; }

        /// <summary>Gets the handle of the parameter's object, the BLOCKFLIPPARAMETER of the OBJECTS section.</summary>
        public string Handle { get; }

        /// <summary>Gets the label of the parameter, the name of the property it gives each INSERT (305), as Hand.</summary>
        public string Name { get; }

        /// <summary>Gets the description of the property (306); empty where none was given.</summary>
        public string Description { get; }

        /// <summary>Gets the point the reflection line starts at, in the block's coordinates.</summary>
        public GeoPoint3 BasePoint { get; }

        /// <summary>Gets the point the reflection line runs to, in the block's coordinates.</summary>
        public GeoPoint3 EndPoint { get; }
    }
}
