using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Export
{
    /// <summary>
    /// A block of a DXF file as <see cref="DxfReader"/> read it: its name, its base point, its flags and its own entities in
    /// the block's own coordinates, as the INSERTs that place it found them.
    /// </summary>
    /// <remarks>
    /// The entities are read as the drawing's are, with the same options, and an INSERT inside the block is placed within
    /// it, its entities carrying the names of the blocks they were placed through after this one in
    /// <see cref="DxfShape.BlockPath"/>. Nothing of the block is placed, layer 0 and BYBLOCK being kept as they are: a block
    /// of a bolt drawn on layer 0 about its own origin holds its circle about the origin on layer 0, wherever the drawing
    /// puts it.
    /// </remarks>
    public sealed class DxfBlock
    {
        internal DxfBlock(string name, GeoPoint3 basePoint, int flags, DxfShape[] entities)
        {
            Name = name;
            BasePoint = basePoint;
            Flags = flags;
            Shapes = Array.AsReadOnly(entities);
        }

        /// <summary>
        /// Gets the name of the block (group 2) as its definition writes it, such as <c>BOLT_M20</c>, or <c>*U12</c> for a
        /// block AutoCAD names itself.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the base point of the block (groups 10, 20, 30): the point of the block an INSERT puts at its insertion
        /// point, scaled to millimetres where <see cref="DxfReadOptions.ToMillimeters"/> asks.
        /// </summary>
        public GeoPoint3 BasePoint { get; }

        /// <summary>
        /// Gets the flags of the block (group 70), as the DXF reference numbers them: 1 for a block AutoCAD names itself,
        /// 2 for one with attributes, 4 for an external reference, 8 for one overlaid, 16 for one that depends on an
        /// external reference, 32 for a reference resolved; 0 for an ordinary block.
        /// </summary>
        public int Flags { get; }

        /// <summary>
        /// Gets the shapes of the entities of the block in the order of the file, in the block's own coordinates.
        /// </summary>
        public IReadOnlyList<DxfShape> Shapes { get; }
    }
}
