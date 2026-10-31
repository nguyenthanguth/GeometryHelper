using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// A point parameter of a dynamic block, as <see cref="DxfBlockTableRecord.AddPointParameter"/> wrote it: a position shown
    /// on every INSERT of the block as two dynamic properties, its label with " X" and " Y" after it, and followed by the move
    /// actions tied to it
    /// (<see cref="DxfBlockTableRecord.AddMoveAction(DxfBlockPointParameter, System.Collections.Generic.IEnumerable{DxfEntity})"/>).
    /// </summary>
    /// <remarks>
    /// The parameter Tag at (80, 20, 0) gives each INSERT the properties Tag X of 80 and Tag Y of 20; Tag X set to 120, by
    /// AutoCAD or by <see cref="DxfDynamicBlockReferenceProperty.Value"/>, moves the point to (120, 20, 0), and a move action
    /// tied to it moves its entities 40 along x. What it holds is read once it is written: changing the block afterwards does
    /// not change it.
    /// </remarks>
    public sealed class DxfBlockPointParameter
    {
        internal DxfBlockPointParameter(DxfBlockTableRecord block, int node, string handle, string name, string description, GeoPoint3 location)
        {
            Block = block;
            Node = node;
            Handle = handle;
            Name = name;
            Description = description;
            Location = location;
        }

        /// <summary>The block the parameter is of.</summary>
        internal DxfBlockTableRecord Block { get; }

        /// <summary>The id of the parameter's node in the block's evaluation graph (90), which its actions name.</summary>
        internal int Node { get; }

        /// <summary>Gets the handle of the parameter's object, the BLOCKPOINTPARAMETER of the OBJECTS section.</summary>
        public string Handle { get; }

        /// <summary>Gets the label of the parameter (303), as Tag: the properties it gives each INSERT are named Tag X and Tag Y.</summary>
        public string Name { get; }

        /// <summary>Gets the description of both properties (304); empty where none was given.</summary>
        public string Description { get; }

        /// <summary>Gets the position, in the block's coordinates, every INSERT of the block shows until its properties are set.</summary>
        public GeoPoint3 Location { get; }
    }
}
