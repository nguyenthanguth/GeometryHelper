using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Export
{
    /// <summary>
    /// An INSERT of a DXF file as <see cref="DxfFile"/> holds it in <see cref="DxfItem.Shape"/>: the block it places, where,
    /// turned and scaled how, and the array of a MINSERT, as the file gives them, nothing placed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The INSERT of the block BOLT at (100, 50) on the floor, turned 90 degrees and scaled by 2 along x, is
    /// <see cref="BlockName"/> BOLT, <see cref="Position"/> (100, 50, 0), <see cref="Normal"/> (0, 0, 1),
    /// <see cref="Rotation"/> pi / 2, <see cref="ScaleX"/> 2 and the other scales 1, one column and one row.
    /// <see cref="GetPlacedEntities()"/> gives the entities of the block placed where it puts them, as <see cref="DxfReader"/>
    /// reads them in <see cref="DxfDocument.Shapes"/>.
    /// </para>
    /// <para>
    /// It cannot be changed once made, and two INSERTs are the same only where they are one object: it does not compare its
    /// numbers.
    /// </para>
    /// </remarks>
    internal sealed class DxfInsert
    {
        private readonly Func<DxfReader.Placing> _placed;

        internal DxfInsert(string blockName, GeoPoint3 position, GeoVector3 normal, double rotation, double scaleX, double scaleY, double scaleZ, int columns, int rows, double columnSpacing, double rowSpacing, Func<DxfReader.Placing> placed)
        {
            BlockName = blockName;
            Position = position;
            Normal = normal;
            Rotation = rotation;
            ScaleX = scaleX;
            ScaleY = scaleY;
            ScaleZ = scaleZ;
            Columns = columns;
            Rows = rows;
            ColumnSpacing = columnSpacing;
            RowSpacing = rowSpacing;
            _placed = placed;
        }

        /// <summary>
        /// Gets the name of the block the INSERT places (group 2), its escapes (<c>\U+00E9</c>) read as letters; empty where
        /// the file gives none.
        /// </summary>
        public string BlockName { get; }

        /// <summary>
        /// Gets the insertion point in world coordinates: groups 10, 20 and 30, which are in the system of the extrusion's
        /// plane, taken to the world by the arbitrary axis algorithm. The block's base point lands on it.
        /// </summary>
        public GeoPoint3 Position { get; }

        /// <summary>
        /// Gets the extrusion of the INSERT (group 210), of unit length, (0, 0, 1) where the file gives none: the z axis of the
        /// system the block is placed in.
        /// </summary>
        public GeoVector3 Normal { get; }

        /// <summary>
        /// Gets how far the block is turned about <see cref="Normal"/>, in radians counter-clockwise from the x axis of the
        /// normal's own system: group 50 in degrees made radians, from nought up to but not including 2 pi, so -90 degrees is
        /// 3 pi / 2.
        /// </summary>
        public double Rotation { get; }

        /// <summary>
        /// Gets the scale along the block's x axis (group 41), 1 where the file gives none; below nought for a mirror.
        /// </summary>
        public double ScaleX { get; }

        /// <summary>
        /// Gets the scale along the block's y axis (group 42), 1 where the file gives none.
        /// </summary>
        public double ScaleY { get; }

        /// <summary>
        /// Gets the scale along the block's z axis (group 43), 1 where the file gives none.
        /// </summary>
        public double ScaleZ { get; }

        /// <summary>
        /// Gets the columns of a MINSERT's array (group 70) as the file gives it, 1 where it gives none, as for an INSERT.
        /// </summary>
        public int Columns { get; }

        /// <summary>
        /// Gets the rows of a MINSERT's array (group 71) as the file gives it, 1 where it gives none.
        /// </summary>
        public int Rows { get; }

        /// <summary>
        /// Gets how far apart the columns of a MINSERT's array stand (group 44), along the turned x axis, not scaled; nought
        /// where the file gives none, which places the array's columns as one.
        /// </summary>
        public double ColumnSpacing { get; }

        /// <summary>
        /// Gets how far apart the rows of a MINSERT's array stand (group 45), along the turned y axis, not scaled; nought
        /// where the file gives none, which places the array's rows as one.
        /// </summary>
        public double RowSpacing { get; }

        /// <summary>
        /// Gets the entities of the block placed where the INSERT puts them, each cell of a MINSERT's array in turn, as
        /// <see cref="DxfReader"/> reads them with its default options: each shape placed, layer 0 and BYBLOCK taking the
        /// INSERT's look, an INSERT inside the block placed within it, and each with the names of its blocks in
        /// <see cref="DxfShape.BlockPath"/>.
        /// </summary>
        /// <returns>
        /// The entities placed, a new list on each call, in world coordinates for an INSERT of the drawing and in the block's
        /// own for one inside a block, which is placed within that block as <see cref="DxfDocument.Blocks"/> places it. Empty
        /// where the reader places nothing: the block is not defined, is an external reference or a layout, or would take the
        /// entities placed past <see cref="DxfReadOptions.MaxPlacedEntities"/> by its default, which each call counts afresh.
        /// </returns>
        /// <remarks>
        /// <para>
        /// Each call is a placing of its own, and gives the same whatever was placed before it. The names of the layers are
        /// spelled as the layer table spells them, then as the entities of the drawing first spell them, then as the placed
        /// entities first spell them, two names differing only in case being one layer. Where a layer the table does not list
        /// is spelled in more than one case in the file, a placed entity may carry another of those spellings than the reader
        /// gives it, which takes the first it has met in its own order of reading.
        /// </para>
        /// <para>
        /// The blocks worked out for the sizes of INSERTs, at most 1 000 000, are for all the calls on one file together, as
        /// they are for one reading; once they are spent, an INSERT whose size would need one more is not placed.
        /// </para>
        /// </remarks>
        public IReadOnlyList<DxfShape> GetPlacedEntities() => _placed().Entities;

        /// <summary>
        /// Gets the entities of the block placed where the INSERT puts them, as <see cref="GetPlacedEntities()"/> does, and
        /// what the reader has to say of what it did not place.
        /// </summary>
        /// <param name="warnings">What the reader says of the placing, each a sentence: an INSERT of a block the file does not
        /// define, of a block inside itself, past the bound, and each type of entity of the block it did not place with the
        /// reasons, such as <c>1 HATCH not placed: 1 of a type that is not read.</c> Empty where everything was placed.</param>
        /// <returns>The entities placed, a new list on each call.</returns>
        public IReadOnlyList<DxfShape> GetPlacedEntities(out IReadOnlyList<string> warnings)
        {
            DxfReader.Placing placing = _placed();
            warnings = placing.Warnings;
            return placing.Entities;
        }

        /// <summary>
        /// Gives the INSERT as a line for debugging: its block, where it stands and its turn.
        /// </summary>
        /// <returns>The INSERT as a line, such as <c>INSERT BOLT at (100, 50, 0), turned 1.571</c>.</returns>
        public override string ToString() => $"INSERT {BlockName} at {Position}, turned {Rotation:0.###}";
    }
}
