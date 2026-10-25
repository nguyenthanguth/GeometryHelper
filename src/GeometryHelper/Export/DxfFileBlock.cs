using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Export
{
    /// <summary>
    /// A block of a <see cref="DxfFile"/>: its name, its base point, its flags and its entities, as its BLOCK record and the
    /// records up to its ENDBLK give them.
    /// </summary>
    /// <remarks>
    /// The block BOLT drawn about the point (1, 2) is <see cref="Name"/> BOLT, <see cref="BasePoint"/> (1, 2, 0) and
    /// <see cref="Flags"/> 0, with its circle and its line in <see cref="Items"/>, in the block's own coordinates. Its shapes
    /// are read as those of the drawing are, nothing placed: an INSERT inside it is a <see cref="DxfInsert"/>, which places
    /// its own block within this one.
    /// </remarks>
    internal sealed class DxfFileBlock
    {
        internal DxfFileBlock(DxfFile file, string name, GeoPoint3 basePoint, int flags, List<DxfItem> items, DxfItem head, int end)
        {
            Name = name;
            BasePoint = basePoint;
            Flags = flags;
            Head = head;
            End = end;
            Items = new DxfSpace(file, items, DxfSpaceKind.Block, this);
        }

        /// <summary>The BLOCK record of the block, which holds its name, base point and flags.</summary>
        internal DxfItem Head { get; }

        /// <summary>
        /// The index of the record of the file that ends the block, its ENDBLK, or the record after its last entity where it
        /// has none; -1 for a block added, whose records are its own.
        /// </summary>
        internal int End { get; }

        /// <summary>The ENDBLK of a block added, before which its entities go; null for a block of the file as it was read.</summary>
        internal DxfFile.Piece EndPiece { get; set; }

        /// <summary>The list of pieces the records of a block added are in; null for a block of the file as it was read.</summary>
        internal List<DxfFile.Piece> Home { get; set; }

        /// <summary>
        /// Gets the name of the block (group 2, or 3 where it has no 2), its escapes (<c>\U+00E9</c>) read as letters, such as
        /// <c>BOLT_M20</c>, <c>*U12</c> for a block AutoCAD names itself or <c>*Paper_Space0</c> for the block of a layout.
        /// </summary>
        public string Name { get; internal set; }

        /// <summary>
        /// Gets the base point of the block (groups 10, 20 and 30): the point of the block an INSERT puts at its insertion
        /// point; the origin where the record gives none, or a number of it is not a finite number.
        /// </summary>
        public GeoPoint3 BasePoint { get; }

        /// <summary>
        /// Gets the flags of the block (group 70), as <see cref="DxfBlock.Flags"/> numbers them: 1 for a block AutoCAD names
        /// itself, 2 for one with attributes, 4 for an external reference; nought for an ordinary block or one that gives none.
        /// </summary>
        public int Flags { get; }

        /// <summary>
        /// Gets the entities between the block's BLOCK and its ENDBLK, in the order of the file, a POLYLINE with its vertices
        /// and an INSERT with its attributes as in <see cref="DxfFile.ModelSpace"/>.
        /// </summary>
        public DxfSpace Items { get; }

        /// <summary>
        /// Gives the block as a line for debugging: its name and how many entities it holds.
        /// </summary>
        /// <returns>The block as a line, such as <c>BLOCK BOLT, 2 entities</c>.</returns>
        public override string ToString() => "BLOCK " + Name + ", " + Items.Count.ToString(System.Globalization.CultureInfo.InvariantCulture) + " entities";
    }
}
