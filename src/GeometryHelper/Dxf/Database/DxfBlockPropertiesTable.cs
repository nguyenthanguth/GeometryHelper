using System.Collections.Generic;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// The block properties table of a dynamic block as the drawing keeps it, AutoCAD's <c>BlockPropertiesTable</c>: the name
    /// of the property its INSERTs show it by, the name of each column and the rows. An INSERT's property of that name reads
    /// the index of the row it matches and is set to one (see <see cref="DxfDynamicBlockReferenceProperty.Value"/>). See
    /// <see cref="DxfBlockTableRecord.BlockPropertiesTable"/>.
    /// </summary>
    /// <remarks>
    /// The table of AutoCAD's Parking sample reads Block Table1, the columns Spaces and Width, and ten rows from 1 Space and
    /// 2700 to 10 Spaces and 27000: an INSERT set to the row 2 shows 3 Spaces and a Width of 8100.
    /// </remarks>
    public sealed class DxfBlockPropertiesTable
    {
        internal DxfBlockPropertiesTable(string name, bool mustMatch, List<string> columns, List<DxfBlockPropertiesTableRow> rows)
        {
            Name = name;
            MustMatch = mustMatch;
            ColumnNames = columns.AsReadOnly();
            Rows = rows.AsReadOnly();
        }

        /// <summary>Gets the name of the property an INSERT shows the table by, Block Table1 as AutoCAD names it.</summary>
        public string Name { get; }

        /// <summary>
        /// Gets whether the block's properties must match a row, as AutoCAD's <c>MustMatch</c>; a property other than the
        /// table's is then not set by the library.
        /// </summary>
        public bool MustMatch { get; }

        /// <summary>
        /// Gets the name of each column, in the order of the columns: the name of the property it sets, as Width, or the tag of
        /// its attribute definition.
        /// </summary>
        public IReadOnlyList<string> ColumnNames { get; }

        /// <summary>
        /// Gets the rows, each a value for each column: a <see cref="double"/> (a distance in the block's units, an angle in
        /// radians, the real of a user parameter), a <see cref="string"/>, or null for a cell AutoCAD left empty.
        /// </summary>
        public IReadOnlyList<DxfBlockPropertiesTableRow> Rows { get; }
    }
}
