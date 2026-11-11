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
        internal DxfBlockPropertiesTable(
            string name,
            bool mustMatch,
            List<string> columns,
            List<DxfBlockPropertiesTableRow> rows
        )
        {
            Name = name;
            MustMatch = mustMatch;
            ColumnNames = columns.AsReadOnly();
            Rows = rows.AsReadOnly();
        }

        /// <summary>Gets the name of the property an INSERT shows the table by, Block Table1 as AutoCAD names it.</summary>
        public string Name { get; }

        /// <summary>
        /// Gets whether the block's properties must match a row, as AutoCAD's <c>MustMatch</c>. A distance or an angle of a
        /// column set then takes the INSERT to the first row that holds it beside the first row's values in the other columns,
        /// else, where its column holds no value twice, to the row that holds it; else it stands beside the first row's values
        /// and no row matches, as AutoCAD 2021 set the library's tables made to match (plugin D26b): of the rows 100 x 50,
        /// 200 x 80 and 150 x 60, Width 200 takes the row 1, Width 120 stands beside Height 50. A lookup or a user parameter
        /// of such a block, and the parameters of a table of a column of a user parameter or of the visibility state, are not
        /// set by the library.
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
