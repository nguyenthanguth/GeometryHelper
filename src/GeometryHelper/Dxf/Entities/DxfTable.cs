using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using GeometryHelper.Export;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// A table of a DXF file, as AutoCAD's <c>Table</c>: an ACAD_TABLE, its rows and columns, the text, value, kind and look of
    /// each cell, the cells merged, and the anonymous block (<c>*T12</c>) AutoCAD drew it with.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The table of three rows 10, 8 and 8 high and two columns 40 and 30 wide at (300, -100, 0), its title Parts merged across
    /// both columns, has <see cref="NumRows"/> 3, <see cref="NumColumns"/> 2, <see cref="Height"/> 26, <see cref="Width"/> 70,
    /// <see cref="TextString(int, int)"/> of (0, 0) Parts and <see cref="GetMergeRange(int, int)"/> of (0, 1) the range (0, 0)
    /// to (0, 1). Rows and columns count from nought, rows from the top, as AutoCAD counts them.
    /// </para>
    /// <para>
    /// The file holds the cells one after another, row by row, each from its group 171: its kind, flags, merging (173, 175,
    /// 176), the overrides it has of its own (177, or 91 from R2007 on), and its text in groups 2 and 1 in a file before R2007,
    /// or between <c>301 CELL_VALUE</c> and <c>304 ACVALUE_END</c> from R2007 on, its text in 302. A cell's look AutoCAD takes
    /// from the table style, which the library does not read: <see cref="Alignment(int, int)"/>, <see cref="TextHeight(int, int)"/>
    /// and the other reads of a cell's look give its own override, null where it has none.
    /// </para>
    /// <para>
    /// <see cref="SetTextString(int, int, string)"/>, <see cref="SetValue(int, int, object)"/>,
    /// <see cref="SetRowHeight(int, double)"/>, <see cref="SetColumnWidth(int, double)"/>, the setters of a cell's look
    /// (<see cref="SetAlignment(int, int, DxfCellAlignment)"/> and the others, each the cell's own override with its flag) and the
    /// rest write the table's data, every other group kept; the block AutoCAD drew the table
    /// with is not drawn anew, as its <c>RecomputeTableBlock</c> would draw it: AutoCAD draws the table anew from its data when
    /// it next updates it, and until then shows the old picture. Moved, turned, mirrored or scaled alike in every direction by
    /// <see cref="DxfEntity.TransformBy(GeoTransform3)"/>, its sizes scaled with it, a table carries its block's entities with it
    /// where it alone names that block; a picture another entity shares, as a
    /// copy of the table in its own drawing does, is left for AutoCAD to draw anew. A copy added to another drawing keeps the
    /// values its block cells give their blocks' attributes where the block comes with it, as it does to a drawing that has no
    /// block of its name: each pointer to an attribute definition (331) names the copy of that definition, as AutoCAD's deep
    /// clone maps it. Where the drawing has a block of the name, which the cell then shows, a pointer is left out with its
    /// value (300) and the cell's count (179) made to agree, as AutoCAD reads the pairs by that count. A copy in the table's own
    /// drawing shares the original's fields (344): a field one lets go of is kept while the other holds it, but erasing the
    /// original table leaves the copy's 344s naming nothing, a limit listed for later.
    /// </para>
    /// </remarks>
    public sealed partial class DxfTable : DxfEntity
    {
        // The groups of AcDbBlockReference and AcDbTable in the order AutoCAD writes them, the table's head.
        private static readonly IReadOnlyList<int> Order = Array.AsReadOnly(new[] { 100, 2, 10, 20, 30, 100, 280, 342, 343, 11, 21, 31, 90, 91, 92, 93, 94, 95, 96, 70, 141, 142 });

        private static readonly GeoPoint3 Origin = new GeoPoint3(0.0, 0.0, 0.0);

        private Layout _layout;
        private byte[] _layoutBytes;
        private int _layoutStart;
        private int _layoutEnd;

        private DxfTable(DxfItem item)
            : base(item)
        {
        }

        #region The table

        /// <summary>Gets the number of rows (group 91 of the table's head), as AutoCAD's <c>NumRows</c>.</summary>
        public int NumRows => Read().Rows;

        /// <summary>Gets the number of columns (group 92 of the table's head), as AutoCAD's <c>NumColumns</c>.</summary>
        public int NumColumns => Read().Columns;

        /// <summary>Gets the height of the table, its rows' heights together, as AutoCAD's <c>Height</c>: 26 for rows 10, 8 and 8 high.</summary>
        public double Height
        {
            get
            {
                Layout layout = Read();
                double sum = 0.0;

                foreach (int at in layout.RowAt)
                {
                    sum += Real(layout.Groups, at);
                }

                return sum;
            }
        }

        /// <summary>Gets the width of the table, its columns' widths together, as AutoCAD's <c>Width</c>: 70 for columns 40 and 30 wide.</summary>
        public double Width
        {
            get
            {
                Layout layout = Read();
                double sum = 0.0;

                foreach (int at in layout.ColumnAt)
                {
                    sum += Real(layout.Groups, at);
                }

                return sum;
            }
        }

        /// <summary>
        /// Gets the point the table stands at, its top left corner (groups 10, 20, 30), as AutoCAD's <c>Position</c>: in world
        /// coordinates; the origin where the record gives none.
        /// </summary>
        public GeoPoint3 Position
        {
            get
            {
                DxfDimGroups g = Own();
                return OcsToWorld(Normal, g.Point(10, g.Base, Origin));
            }
        }

        /// <summary>Gets the direction of the table's rows (groups 11, 21, 31), as AutoCAD's <c>Direction</c>; (1, 0, 0) where the record gives none.</summary>
        public GeoVector3 Direction
        {
            get
            {
                DxfDimGroups g = Own();
                GeoPoint3 d = g.Point(11, g.Sub, new GeoPoint3(1.0, 0.0, 0.0));
                return new GeoVector3(d.X, d.Y, d.Z);
            }
        }

        /// <summary>Gets the normal of the plane the table lies in (groups 210, 220, 230), as AutoCAD's <c>Normal</c>; (0, 0, 1) where the record gives none.</summary>
        public GeoVector3 Normal => GetNormal();

        /// <summary>
        /// Gets the anonymous block AutoCAD drew the table with (group 2), as AutoCAD's <c>BlockTableRecord</c> gives its id:
        /// <c>*T12</c>; null where the drawing has no block of its name.
        /// </summary>
        public DxfBlockTableRecord BlockTableRecord
        {
            get
            {
                DxfDimGroups g = Own();
                string name = DxfReader.NameOf(g.Text(2, g.Base) ?? string.Empty);
                DxfDatabase database = (IsResident ? Item.File : Item.File.Origin)?.Database;
                return name.Length == 0 || database == null || !database.BlockTable.Has(name) ? null : database.BlockTable[name];
            }
        }

        /// <summary>
        /// Gets the name of the table style (the entry of the dictionary ACAD_TABLESTYLE the table's 342 names), as AutoCAD's
        /// <c>TableStyleName</c>; null where the table names none the drawing has.
        /// </summary>
        public string TableStyleName
        {
            get
            {
                DxfDimGroups g = Own();
                string handle = g.Text(342, g.Sub)?.Trim();
                return handle == null || !IsResident ? null : StyleName(Item.File, handle);
            }
        }

        /// <summary>
        /// Gets whether the table leaves its title out (its own 280 after its cells), as AutoCAD's <c>IsTitleSuppressed</c>;
        /// false where the table gives none of its own, as AutoCAD's table style STANDARD shows it.
        /// </summary>
        public bool IsTitleSuppressed => TableWhole(280, 0) != 0;

        /// <summary>
        /// Gets whether the table leaves its header out (its own 281 after its cells), as AutoCAD's <c>IsHeaderSuppressed</c>;
        /// false where the table gives none of its own.
        /// </summary>
        public bool IsHeaderSuppressed => TableWhole(281, 0) != 0;

        /// <summary>
        /// Gets which way the rows follow one another (its own 70 after its cells: 0 down, 1 up), as AutoCAD's
        /// <c>FlowDirection</c>: <see cref="DxfFlowDirection.TopToBottom"/> or <see cref="DxfFlowDirection.BottomToTop"/>; down
        /// where the table gives none of its own.
        /// </summary>
        public DxfFlowDirection FlowDirection => TableWhole(70, 0) == 1 ? DxfFlowDirection.BottomToTop : DxfFlowDirection.TopToBottom;

        /// <summary>
        /// Gets the margin left and right of a cell's content (its own 40 after its cells), as AutoCAD's
        /// <c>HorizontalCellMargin</c>; 0.06, that of AutoCAD's table style STANDARD, where the table gives none of its own.
        /// </summary>
        public double HorizontalCellMargin => TableReal(40, 0.06);

        /// <summary>
        /// Gets the margin above and below a cell's content (its own 41 after its cells), as AutoCAD's <c>VerticalCellMargin</c>;
        /// 0.06 where the table gives none of its own.
        /// </summary>
        public double VerticalCellMargin => TableReal(41, 0.06);

        /// <summary>Gets the height of a row (the row's group 141), as AutoCAD's <c>RowHeight</c>: 10 for the title row of the table above.</summary>
        /// <param name="row">The row, from nought.</param>
        /// <returns>The height.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the row is not one of the table's.</exception>
        public double RowHeight(int row)
        {
            Layout layout = Read();
            CheckIndex(row, layout.RowAt.Count, nameof(row), "row");
            return Real(layout.Groups, layout.RowAt[row]);
        }

        /// <summary>Gets the width of a column (the column's group 142), as AutoCAD's <c>ColumnWidth</c>.</summary>
        /// <param name="col">The column, from nought.</param>
        /// <returns>The width.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the column is not one of the table's.</exception>
        public double ColumnWidth(int col)
        {
            Layout layout = Read();
            CheckIndex(col, layout.ColumnAt.Count, nameof(col), "column");
            return Real(layout.Groups, layout.ColumnAt[col]);
        }

        /// <summary>
        /// Gets what a row is, as AutoCAD's <c>RowType</c> tells it: the first the title and the next the header, each where
        /// the table does not leave it out (<see cref="IsTitleSuppressed"/>, <see cref="IsHeaderSuppressed"/>), the rest data.
        /// </summary>
        /// <param name="row">The row, from nought.</param>
        /// <returns>What it is.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the row is not one of the table's.</exception>
        public DxfRowType RowType(int row)
        {
            CheckIndex(row, NumRows, nameof(row), "row");
            int first = 0;

            if (!IsTitleSuppressed)
            {
                if (row == 0)
                {
                    return DxfRowType.TitleRow;
                }

                first = 1;
            }

            return !IsHeaderSuppressed && row == first ? DxfRowType.HeaderRow : DxfRowType.DataRow;
        }

        #endregion

        #region The cells

        /// <summary>Gets what a cell holds (its group 171), as AutoCAD's <c>CellType</c>.</summary>
        /// <param name="row">The row, from nought.</param>
        /// <param name="col">The column, from nought.</param>
        /// <returns>What it holds.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the cell is not one of the table's.</exception>
        public DxfTableCellType CellType(int row, int col) => (DxfTableCellType)Cell(row, col).Whole(171, 0);

        /// <summary>
        /// Gets the text of a cell, as AutoCAD's <c>TextString</c>: from R2007 on the text of its value (302), before R2007 its
        /// groups 2 and 1 one after another; empty for a cell of no text, as a block cell.
        /// </summary>
        /// <param name="row">The row, from nought.</param>
        /// <param name="col">The column, from nought.</param>
        /// <returns>The text, with its codes, as <c>\P</c> for a new line.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the cell is not one of the table's.</exception>
        public string TextString(int row, int col) => Cell(row, col).Text();

        /// <summary>Gets the text of a cell as the file holds it, as AutoCAD's <c>TextStringConst</c>: the same as <see cref="TextString(int, int)"/>.</summary>
        /// <param name="row">The row, from nought.</param>
        /// <param name="col">The column, from nought.</param>
        /// <returns>The text.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the cell is not one of the table's.</exception>
        public string TextStringConst(int row, int col) => TextString(row, col);

        /// <summary>Gets the text of a content of a cell, as AutoCAD's <c>GetTextString(row, column, contentIndex)</c>: a cell of a file holds one content, nought.</summary>
        /// <param name="row">The row, from nought.</param>
        /// <param name="column">The column, from nought.</param>
        /// <param name="contentIndex">The content, nought.</param>
        /// <returns>The text.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the cell is not one of the table's, or the content is not nought.</exception>
        public string GetTextString(int row, int column, int contentIndex)
        {
            CheckContent(contentIndex);
            return TextString(row, column);
        }

        /// <summary>
        /// Gets the value of a cell, as AutoCAD's <c>Value</c>: from R2007 on of the kind its value says (90: 1 a long, 2 a
        /// double, 4 a text), an <see cref="int"/>, a <see cref="double"/> or a <see cref="string"/>; before R2007 its text.
        /// </summary>
        /// <param name="row">The row, from nought.</param>
        /// <param name="col">The column, from nought.</param>
        /// <returns>The value.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the cell is not one of the table's.</exception>
        public object Value(int row, int col) => Cell(row, col).Value();

        /// <summary>Gets the value of a content of a cell, as AutoCAD's <c>GetValue(row, column, contentIndex)</c>: of its one content, nought.</summary>
        /// <param name="row">The row, from nought.</param>
        /// <param name="column">The column, from nought.</param>
        /// <param name="contentIndex">The content, nought.</param>
        /// <returns>The value, as <see cref="Value(int, int)"/>.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the cell is not one of the table's, or the content is not nought.</exception>
        public object GetValue(int row, int column, int contentIndex)
        {
            CheckContent(contentIndex);
            return Value(row, column);
        }

        /// <summary>Gets whether a cell holds nothing, as AutoCAD's <c>IsEmpty</c>: a text cell of no text.</summary>
        /// <param name="row">The row, from nought.</param>
        /// <param name="column">The column, from nought.</param>
        /// <returns>Whether it is empty.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the cell is not one of the table's.</exception>
        public bool IsEmpty(int row, int column) => CellType(row, column) != DxfTableCellType.BlockCell && TextString(row, column).Length == 0;

        /// <summary>Gets how many contents a cell holds, as AutoCAD's <c>GetNumberOfContents</c>: 1, nought for an empty one.</summary>
        /// <param name="row">The row, from nought.</param>
        /// <param name="column">The column, from nought.</param>
        /// <returns>The count.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the cell is not one of the table's.</exception>
        public int GetNumberOfContents(int row, int column) => IsEmpty(row, column) ? 0 : 1;

        /// <summary>
        /// Gets whether a cell is one of a block of cells merged into one, as AutoCAD's <c>IsMergedCell</c>, and that block: the
        /// cell whose 175 and 176 (merged 173) span it, columns and rows, its top left.
        /// </summary>
        /// <param name="row">The row, from nought.</param>
        /// <param name="col">The column, from nought.</param>
        /// <param name="range">The block of cells; the cell alone where it is merged with none.</param>
        /// <returns>Whether it is merged with others.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the cell is not one of the table's.</exception>
        public bool IsMergedCell(int row, int col, out DxfCellRange range)
        {
            range = GetMergeRange(row, col);
            return range.TopRow != range.BottomRow || range.LeftColumn != range.RightColumn;
        }

        /// <summary>Gets the block of merged cells a cell is in, as AutoCAD's <c>GetMergeRange</c>: the title of two columns, (0, 0) to (0, 1), whose cell <c>range[0, 0]</c> is the table's; the cell alone where it is merged with none.</summary>
        /// <param name="row">The row, from nought.</param>
        /// <param name="column">The column, from nought.</param>
        /// <returns>The range.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the cell is not one of the table's.</exception>
        public DxfCellRange GetMergeRange(int row, int column)
        {
            Layout layout = Read();
            layout.CellOf(row, column);

            for (int r = 0; r <= row; r++)
            {
                for (int c = 0; c <= column; c++)
                {
                    CellView cell = layout.CellAt(r, c);

                    if (cell.Whole(173, 0) == 0)
                    {
                        continue;
                    }

                    int width = cell.Whole(175, 0);
                    int height = cell.Whole(176, 0);

                    if ((width > 1 || height > 1) && row < r + Math.Max(1, height) && column < c + Math.Max(1, width))
                    {
                        return new DxfCellRange(this, r, c, r + Math.Max(1, height) - 1, c + Math.Max(1, width) - 1);
                    }
                }
            }

            return new DxfCellRange(this, row, column, row, column);
        }

        /// <summary>Gets where a cell's content stands, as AutoCAD's <c>Alignment</c>: the cell's own (170); null where it has none, its table style's then.</summary>
        /// <param name="row">The row, from nought.</param>
        /// <param name="col">The column, from nought.</param>
        /// <returns>The alignment, or null.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the cell is not one of the table's.</exception>
        public DxfCellAlignment? Alignment(int row, int col)
        {
            string text = Cell(row, col).Override(170, 0x01);
            return int.TryParse(text?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? (DxfCellAlignment)value : (DxfCellAlignment?)null;
        }

        /// <summary>Gets the height of a cell's text, as AutoCAD's <c>TextHeight</c>: the cell's own (140); null where it has none.</summary>
        /// <param name="row">The row, from nought.</param>
        /// <param name="col">The column, from nought.</param>
        /// <returns>The height, or null.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the cell is not one of the table's.</exception>
        public double? TextHeight(int row, int col)
            => Numbers.TryRead(Cell(row, col).Override(140, 0x20), out double value) && Guard.IsFinite(value) ? value : (double?)null;

        /// <summary>Gets the text style of a cell's text, as AutoCAD's <c>TextStyle</c> gives its id: the cell's own (7, by name); null where it has none, or names one the drawing has not.</summary>
        /// <param name="row">The row, from nought.</param>
        /// <param name="col">The column, from nought.</param>
        /// <returns>The style, or null.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the cell is not one of the table's.</exception>
        public DxfTextStyleTableRecord TextStyle(int row, int col)
        {
            string name = Cell(row, col).Override(7, 0x10);
            DxfDatabase database = IsResident ? Item.File.Database : null;
            return name == null || database == null || !database.TextStyleTable.Has(DxfReader.NameOf(name)) ? null : database.TextStyleTable[DxfReader.NameOf(name)];
        }

        /// <summary>Gets the colour of a cell's content, as AutoCAD's <c>ContentColor</c>: the cell's own (64, of the index); null where it has none.</summary>
        /// <param name="row">The row, from nought.</param>
        /// <param name="col">The column, from nought.</param>
        /// <returns>The colour, or null.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the cell is not one of the table's.</exception>
        public DxfColor ContentColor(int row, int col) => Colour(Cell(row, col).Override(64, 0x08));

        /// <summary>Gets the colour of a cell's background, as AutoCAD's <c>BackgroundColor</c>: the cell's own (63, of the index); null where it has none.</summary>
        /// <param name="row">The row, from nought.</param>
        /// <param name="col">The column, from nought.</param>
        /// <returns>The colour, or null.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the cell is not one of the table's.</exception>
        public DxfColor BackgroundColor(int row, int col) => Colour(Cell(row, col).Override(63, 0x04));

        /// <summary>Gets whether a cell's background is left unfilled, as AutoCAD's <c>IsBackgroundColorNone</c>: the cell's own (283 of 0); null where it has none.</summary>
        /// <param name="row">The row, from nought.</param>
        /// <param name="col">The column, from nought.</param>
        /// <returns>Whether it is, or null.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the cell is not one of the table's.</exception>
        public bool? IsBackgroundColorNone(int row, int col)
        {
            string text = Cell(row, col).Override(283, 0x02);
            return text == null ? (bool?)null : text.Trim() == "0";
        }

        /// <summary>
        /// Gets what a cell holds of its own over its table style, as AutoCAD's <c>CellStyleOverrides</c>: by its flags (177, or
        /// 91 from R2007 on), bit k the override numbered 130 + k as AutoCAD's own enum orders them: its alignment 130 (flag 1),
        /// its fill 131 and 132, its colour 133, its text style 134 and height 135 (flag 0x20), then its borders grouped by what
        /// they hold, the colours of the top, right, bottom and left 136 to 139 (flags 0x40 to 0x200), their lineweights 140 to 143
        /// (0x400 to 0x2000) and whether they are drawn 144 to 147 (0x4000 to 0x20000); the cell of flags 49 holds 130, 134 and
        /// 135, and one of 0x80 the colour of its right border.
        /// </summary>
        /// <param name="row">The row, from nought.</param>
        /// <param name="col">The column, from nought.</param>
        /// <returns>The overrides, in the order of their numbers.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the cell is not one of the table's.</exception>
        public DxfTableStyleOverride[] CellStyleOverrides(int row, int col)
        {
            int flags = Cell(row, col).Flags ?? 0;
            var found = new List<DxfTableStyleOverride>();

            for (int bit = 0; bit < 18; bit++)
            {
                if ((flags & (1 << bit)) != 0)
                {
                    found.Add((DxfTableStyleOverride)(130 + bit));
                }
            }

            return found.ToArray();
        }

        /// <summary>
        /// Gets the block a block cell draws (its 340), as AutoCAD's <c>BlockTableRecordId(row, col)</c> and
        /// <c>GetBlockTableRecordId</c> give its id; null for a text cell, or a block the drawing has not.
        /// </summary>
        /// <param name="row">The row, from nought.</param>
        /// <param name="column">The column, from nought.</param>
        /// <returns>The block, or null.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the cell is not one of the table's.</exception>
        public DxfBlockTableRecord GetBlockTableRecord(int row, int column)
        {
            string handle = Cell(row, column).Head(340)?.Trim();
            DxfDatabase database = IsResident ? Item.File.Database : null;

            if (string.IsNullOrEmpty(handle) || database == null)
            {
                return null;
            }

            foreach (DxfBlockTableRecord block in database.BlockTable)
            {
                if (string.Equals(block.Handle, handle, StringComparison.OrdinalIgnoreCase))
                {
                    return block;
                }
            }

            return null;
        }

        /// <summary>Gets the scale of a block cell's block (its 144), as AutoCAD's <c>BlockScale</c>; 1 where it gives none.</summary>
        /// <param name="row">The row, from nought.</param>
        /// <param name="col">The column, from nought.</param>
        /// <returns>The scale.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the cell is not one of the table's.</exception>
        public double BlockScale(int row, int col) => Numbers.TryRead(Cell(row, col).Head(144), out double scale) && Guard.IsFinite(scale) ? scale : 1.0;

        /// <summary>Gets the turn of a block cell's block (its 145), as AutoCAD's <c>BlockRotation</c>, in radians; nought where it gives none.</summary>
        /// <param name="row">The row, from nought.</param>
        /// <param name="col">The column, from nought.</param>
        /// <returns>The angle.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the cell is not one of the table's.</exception>
        public double BlockRotation(int row, int col) => Numbers.TryRead(Cell(row, col).Head(145), out double angle) && Guard.IsFinite(angle) ? angle : 0.0;

        /// <summary>Gets the turn of a cell's content (its 145), as AutoCAD's <c>GetRotation(row, column, contentIndex)</c>, in radians.</summary>
        /// <param name="row">The row, from nought.</param>
        /// <param name="column">The column, from nought.</param>
        /// <param name="contentIndex">The content, nought.</param>
        /// <returns>The angle.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the cell is not one of the table's, or the content is not nought.</exception>
        public double GetRotation(int row, int column, int contentIndex)
        {
            CheckContent(contentIndex);
            return BlockRotation(row, column);
        }

        /// <summary>
        /// Gets the value a block cell gives an attribute of its block, as AutoCAD's <c>GetBlockAttributeValue</c>: the 300 after
        /// the 331 that points at the definition; null where the cell gives it none.
        /// </summary>
        /// <param name="row">The row, from nought.</param>
        /// <param name="col">The column, from nought.</param>
        /// <param name="attributeDefinition">The definition, an ATTDEF of the cell's block.</param>
        /// <returns>The value, or null.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the definition is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the cell is not one of the table's.</exception>
        public string GetBlockAttributeValue(int row, int col, DxfAttributeDefinition attributeDefinition)
        {
            if (attributeDefinition == null)
            {
                throw new ArgumentNullException(nameof(attributeDefinition));
            }

            CellView cell = Cell(row, col);
            string handle = attributeDefinition.Handle;

            for (int i = cell.From; handle != null && i + 1 < cell.To; i++)
            {
                if (cell.Groups.List[i].Key == 331 && string.Equals(cell.Groups.List[i].Value.Trim(), handle, StringComparison.OrdinalIgnoreCase) && cell.Groups.List[i + 1].Key == 300)
                {
                    return cell.Groups.List[i + 1].Value;
                }
            }

            return null;
        }

        /// <summary>Gets the height of the text of a content of a cell, as AutoCAD's <c>GetTextHeight(row, column, contentIndex)</c>: of its one content, nought, as <see cref="TextHeight(int, int)"/>.</summary>
        /// <param name="row">The row, from nought.</param>
        /// <param name="column">The column, from nought.</param>
        /// <param name="contentIndex">The content, nought.</param>
        /// <returns>The height, or null where the cell has none of its own.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the cell is not one of the table's, or the content is not nought.</exception>
        public double? GetTextHeight(int row, int column, int contentIndex)
        {
            CheckContent(contentIndex);
            return TextHeight(row, column);
        }

        /// <summary>Gets the colour of a content of a cell, as AutoCAD's <c>GetContentColor(row, column, contentIndex)</c>: of its one content, nought, as <see cref="ContentColor(int, int)"/>.</summary>
        /// <param name="row">The row, from nought.</param>
        /// <param name="column">The column, from nought.</param>
        /// <param name="contentIndex">The content, nought.</param>
        /// <returns>The colour, or null where the cell has none of its own.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the cell is not one of the table's, or the content is not nought.</exception>
        public DxfColor GetContentColor(int row, int column, int contentIndex)
        {
            CheckContent(contentIndex);
            return ContentColor(row, column);
        }

        /// <summary>Gets the scale of a content of a cell, as AutoCAD's <c>GetScale(row, column, contentIndex)</c>: of its one content, nought, as <see cref="BlockScale(int, int)"/>.</summary>
        /// <param name="row">The row, from nought.</param>
        /// <param name="column">The column, from nought.</param>
        /// <param name="contentIndex">The content, nought.</param>
        /// <returns>The scale.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the cell is not one of the table's, or the content is not nought.</exception>
        public double GetScale(int row, int column, int contentIndex)
        {
            CheckContent(contentIndex);
            return BlockScale(row, column);
        }

        /// <summary>
        /// Gets the corners of a cell, as AutoCAD's <c>GetCellExtents</c> gives them: from the table's point along its direction
        /// by the widths of the columns before it, and down (up where its rows flow up) by the heights of the rows before it, in
        /// the plane of its normal; the cell (1, 1) of columns 40 and 30 wide and rows 10 and 8 high from (300, -100, 0) is
        /// (340, -110, 0), (370, -110, 0), (370, -118, 0) and (340, -118, 0).
        /// </summary>
        /// <param name="row">The row, from nought.</param>
        /// <param name="col">The column, from nought.</param>
        /// <param name="isOuterCell">Whether the corners are those of the block of merged cells it is in, as <see cref="GetMergeRange(int, int)"/> gives it.</param>
        /// <returns>The four corners, the top left first, then about the cell.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the cell is not one of the table's.</exception>
        public IReadOnlyList<GeoPoint3> GetCellExtents(int row, int col, bool isOuterCell)
        {
            Layout layout = Read();
            layout.CellOf(row, col);
            DxfCellRange range = isOuterCell ? GetMergeRange(row, col) : new DxfCellRange(row, col, row, col);
            double left = 0.0, right = 0.0, top = 0.0, bottom = 0.0;

            for (int c = 0; c <= range.RightColumn && c < layout.ColumnAt.Count; c++)
            {
                double width = Real(layout.Groups, layout.ColumnAt[c]);
                left += c < range.LeftColumn ? width : 0.0;
                right += width;
            }

            for (int r = 0; r <= range.BottomRow && r < layout.RowAt.Count; r++)
            {
                double height = Real(layout.Groups, layout.RowAt[r]);
                top += r < range.TopRow ? height : 0.0;
                bottom += height;
            }

            GeoVector3 along = DxfOcsTransform.Normalized(Direction);
            GeoVector3 down = Normal.CrossProduct(along).Multiply(FlowDirection == DxfFlowDirection.BottomToTop ? 1.0 : -1.0);
            GeoPoint3 origin = Position;
            GeoPoint3 Corner(double x, double y) => origin.Add(along.Multiply(x)).Add(down.Multiply(y));
            return new[] { Corner(left, top), Corner(right, top), Corner(right, bottom), Corner(left, bottom) };
        }

        /// <summary>
        /// Gets the table style of the table (its 342), as AutoCAD's <c>TableStyle</c> gives its id: the TABLESTYLE object of the
        /// dictionary ACAD_TABLESTYLE, read as the library keeps an object it has no class for; null where the table names none
        /// the drawing has.
        /// </summary>
        public DxfDBObject TableStyle
        {
            get
            {
                DxfDimGroups g = Own();
                string handle = g.Text(342, g.Sub)?.Trim();
                DxfItem style = handle == null || !IsResident ? null : Item.File.ObjectItem(handle);
                return style == null ? null : OfObject(style);
            }
        }

        #endregion

        #region Editing

        /// <summary>Sets the scale of a block cell's block (its 144), as AutoCAD's <c>SetBlockScale</c>; the block AutoCAD drew the table with is not drawn anew.</summary>
        /// <param name="row">The row, from nought.</param>
        /// <param name="col">The column, from nought.</param>
        /// <param name="scale">The scale, a finite number above nought.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the cell is not one of the table's, or the scale is not a finite number above nought.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the cell is not a block cell, or the table is erased.</exception>
        public void SetBlockScale(int row, int col, double scale)
        {
            SetBlockValue(row, col, 144, scale, BlockScale(row, col), () => Guard.Positive(scale, nameof(scale), "A block's scale has to be a finite number above nought."));
        }

        /// <summary>Sets the turn of a block cell's block (its 145), as AutoCAD's <c>SetBlockRotation</c>, in radians; the block AutoCAD drew the table with is not drawn anew.</summary>
        /// <param name="row">The row, from nought.</param>
        /// <param name="col">The column, from nought.</param>
        /// <param name="rotationalAngle">The angle, in radians.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the cell is not one of the table's, or the angle is not a finite number.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the cell is not a block cell, or the table is erased.</exception>
        public void SetBlockRotation(int row, int col, double rotationalAngle)
        {
            SetBlockValue(row, col, 145, rotationalAngle, BlockRotation(row, col), () => Guard.Finite(rotationalAngle, nameof(rotationalAngle), "An angle has to be a finite number."));
        }

        /// <summary>
        /// Sets the value a block cell gives an attribute of its block, as AutoCAD's <c>SetBlockAttributeValue</c>: the 300 after
        /// the 331 that points at the definition; the block AutoCAD drew the table with is not drawn anew.
        /// </summary>
        /// <param name="row">The row, from nought.</param>
        /// <param name="col">The column, from nought.</param>
        /// <param name="attributeDefinition">The definition, an ATTDEF of the cell's block that the cell gives a value.</param>
        /// <param name="value">The value.</param>
        /// <exception cref="ArgumentNullException">Thrown when the definition or the value is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the cell is not one of the table's.</exception>
        /// <exception cref="ArgumentException">Thrown when the cell gives the definition no value, or the value has a line break.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the table is erased.</exception>
        public void SetBlockAttributeValue(int row, int col, DxfAttributeDefinition attributeDefinition, string value)
        {
            if (attributeDefinition == null)
            {
                throw new ArgumentNullException(nameof(attributeDefinition));
            }

            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            if (value.IndexOf('\n') >= 0 || value.IndexOf('\r') >= 0)
            {
                throw new ArgumentException("A value is written on one line.", nameof(value));
            }

            CellView cell = CellToWrite(row, col);
            string handle = attributeDefinition.Handle;

            for (int i = cell.From; handle != null && i + 1 < cell.To; i++)
            {
                if (cell.Groups.List[i].Key == 331 && string.Equals(cell.Groups.List[i].Value.Trim(), handle, StringComparison.OrdinalIgnoreCase) && cell.Groups.List[i + 1].Key == 300)
                {
                    if (!string.Equals(cell.Groups.List[i + 1].Value, value, StringComparison.Ordinal))
                    {
                        CheckEditable();
                        cell.Groups.List[i + 1] = new KeyValuePair<int, string>(300, value);
                        cell.Groups.Write(Order);
                    }

                    return;
                }
            }

            throw new ArgumentException("The cell (" + row.ToString(CultureInfo.InvariantCulture) + ", " + col.ToString(CultureInfo.InvariantCulture) + ") gives the attribute " + attributeDefinition.Tag + " no value.", nameof(attributeDefinition));
        }

        /// <summary>Writes a number of a block cell's head in place, nothing where it is the one read.</summary>
        private void SetBlockValue(int row, int col, int code, double value, double now, Action check)
        {
            CellView cell = CellToWrite(row, col);

            if (cell.Whole(171, 1) != (int)DxfTableCellType.BlockCell)
            {
                throw new InvalidOperationException("The cell (" + row.ToString(CultureInfo.InvariantCulture) + ", " + col.ToString(CultureInfo.InvariantCulture) + ") holds a text, not a block.");
            }

            // The value the cell has changes nothing, whatever the file holds; another is checked before it is written.
            if (Same(value, now))
            {
                return;
            }

            check();
            CheckEditable();
            int end = cell.ValueFrom >= 0 ? cell.ValueFrom : cell.To;
            int at = -1;

            for (int i = cell.From; i < end && at < 0; i++)
            {
                at = cell.Groups.List[i].Key == code ? i : -1;
            }

            string text = Numbers.Exact(value + 0.0);

            if (at >= 0)
            {
                cell.Groups.List[at] = new KeyValuePair<int, string>(code, text);
            }
            else
            {
                // Before the block's handle (340), the scale after it, as AutoCAD writes them.
                int handle = -1;

                for (int i = cell.From; i < end && handle < 0; i++)
                {
                    handle = cell.Groups.List[i].Key == 340 ? i : -1;
                }

                cell.Groups.Insert(code == 145 ? (handle >= 0 ? handle : end) : (handle >= 0 ? handle + 1 : end), new KeyValuePair<int, string>(code, text));
            }

            cell.Groups.Write(Order);
        }

        /// <summary>
        /// Sets the text of a cell, as AutoCAD's <c>SetTextString</c>: before R2007 its groups 2 and 1 written anew, the text in
        /// pieces of 250 letters, the last in 1; from R2007 on its value written as a text (90 of 4) in 1 and 302. A pointer to a
        /// field (344) is taken out, the text being the cell's own now, and the field erased with its children where the table
        /// owns it and nothing else of the file points at it, a copy of the table among them. The block AutoCAD drew the table with is not drawn anew.
        /// </summary>
        /// <param name="row">The row, from nought.</param>
        /// <param name="col">The column, from nought.</param>
        /// <param name="value">The text, with its codes, as <c>\P</c> for a new line.</param>
        /// <exception cref="ArgumentNullException">Thrown when the text is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the cell is not one of the table's.</exception>
        /// <exception cref="ArgumentException">Thrown when the text has a line break in it, which a group does not hold.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the cell is a block cell, or the table is erased.</exception>
        public void SetTextString(int row, int col, string value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            if (value.IndexOf('\n') >= 0 || value.IndexOf('\r') >= 0)
            {
                throw new ArgumentException("A cell's text is written on one line: a new line is \\P.", nameof(value));
            }

            CellView cell = CellToWrite(row, col);

            if (cell.Whole(171, 1) == (int)DxfTableCellType.BlockCell)
            {
                throw new InvalidOperationException("The cell (" + row.ToString(CultureInfo.InvariantCulture) + ", " + col.ToString(CultureInfo.InvariantCulture) + ") holds a block, not a text.");
            }

            if (string.Equals(value, cell.Text(), StringComparison.Ordinal) && cell.Head(344) == null)
            {
                return;
            }

            CheckEditable();
            string field = cell.Head(344);
            cell.SetText(value);
            cell.Groups.Write(Order);
            EraseFields(new[] { field });
        }

        /// <summary>Sets the text of a content of a cell, as AutoCAD's <c>SetTextString(row, column, contentIndex, text)</c>: of its one content, nought, as <see cref="SetTextString(int, int, string)"/>.</summary>
        /// <param name="row">The row, from nought.</param>
        /// <param name="column">The column, from nought.</param>
        /// <param name="contentIndex">The content, nought.</param>
        /// <param name="text">The text.</param>
        /// <exception cref="ArgumentNullException">Thrown when the text is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the cell is not one of the table's, or the content is not nought.</exception>
        /// <exception cref="ArgumentException">Thrown when the text has a line break in it.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the cell is a block cell, or the table is erased.</exception>
        public void SetTextString(int row, int column, int contentIndex, string text)
        {
            CheckContent(contentIndex);
            SetTextString(row, column, text);
        }

        /// <summary>Sets the height of a row (its 141), as AutoCAD's <c>SetRowHeight</c>; the block AutoCAD drew the table with is not drawn anew.</summary>
        /// <param name="row">The row, from nought.</param>
        /// <param name="height">The height, above nought.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the row is not one of the table's, or the height is not a finite number above nought.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the table is erased.</exception>
        public void SetRowHeight(int row, double height)
        {
            Layout layout = ReadToWrite();
            CheckIndex(row, layout.RowAt.Count, nameof(row), "row");
            SetSizes(layout, new[] { layout.RowAt[row] }, height, nameof(height), "A row's height has to be a finite number above nought.");
        }

        /// <summary>Sets the height of every row, as AutoCAD's <c>SetRowHeight(height)</c>.</summary>
        /// <param name="height">The height, above nought.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the height is not a finite number above nought.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the table is erased.</exception>
        public void SetRowHeight(double height)
        {
            Layout layout = ReadToWrite();
            SetSizes(layout, layout.RowAt, height, nameof(height), "A row's height has to be a finite number above nought.");
        }

        /// <summary>Sets the width of a column (its 142), as AutoCAD's <c>SetColumnWidth</c>; the block AutoCAD drew the table with is not drawn anew.</summary>
        /// <param name="col">The column, from nought.</param>
        /// <param name="width">The width, above nought.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the column is not one of the table's, or the width is not a finite number above nought.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the table is erased.</exception>
        public void SetColumnWidth(int col, double width)
        {
            Layout layout = ReadToWrite();
            CheckIndex(col, layout.ColumnAt.Count, nameof(col), "column");
            SetSizes(layout, new[] { layout.ColumnAt[col] }, width, nameof(width), "A column's width has to be a finite number above nought.");
        }

        /// <summary>Sets the width of every column, as AutoCAD's <c>SetColumnWidth(width)</c>.</summary>
        /// <param name="width">The width, above nought.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the width is not a finite number above nought.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the table is erased.</exception>
        public void SetColumnWidth(double width)
        {
            Layout layout = ReadToWrite();
            SetSizes(layout, layout.ColumnAt, width, nameof(width), "A column's width has to be a finite number above nought.");
        }

        /// <summary>
        /// Moves, turns, scales or mirrors the table, as AutoCAD's <c>TransformBy</c> carries one: its point moved, its direction
        /// and normal turned, and the entities of the block AutoCAD drew it with carried by the same transformation, seen from the
        /// system of its normal, as the block is placed at the table's point, where the table is the one entity of the drawing
        /// that names the block; a block another entity names too, as a copy of the table in its own drawing, is left as drawn,
        /// for AutoCAD to draw anew. The record is written once.
        /// </summary>
        /// <remarks>
        /// A scale alike in every direction scales what the table holds of its own sizes: the heights of its rows, the widths of
        /// its columns, its cell margins, and the text heights and block scales it and its cells override; a text height the
        /// table takes from its table style, which the library does not read, stays the style's. A table of rows 10, 8 and 8
        /// high scaled by 2 has rows 20, 16 and 16 high. A mirror leaves the table readable, as AutoCAD 2021's
        /// <c>TransformBy</c> leaves it: its normal the mirror's image of the normal, not turned round, its direction the image
        /// of its direction reversed, and its point the image of the far end of its top edge, so that it fills the box the
        /// mirror makes of it, its first column still first along its direction. The table at (300, -100, 0), 70 wide, mirrored
        /// in the plane x = 280 stands at (190, -100, 0), its normal (0, 0, 1) and its direction (1, 0, 0), as AutoCAD's does.
        /// </remarks>
        /// <param name="transform">The transformation: one that scales alike in every direction, if it scales, as a move, a turn,
        /// a mirror and a scale.</param>
        /// <exception cref="ArgumentNullException">Thrown when the transformation is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the transformation shears, scales unlike along its axes or flattens,
        /// which would make the table's cells other than the rectangles AutoCAD lays it out in.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the table is erased.</exception>
        private protected override void TransformEntity(GeoTransform3 transform)
        {
            if (transform == null)
            {
                throw new ArgumentNullException(nameof(transform));
            }

            if (DxfOcsTransform.IsIdentity(transform))
            {
                return;
            }

            GeoVector3 x = transform.Transform(new GeoVector3(1.0, 0.0, 0.0));
            GeoVector3 y = transform.Transform(new GeoVector3(0.0, 1.0, 0.0));
            GeoVector3 z = transform.Transform(new GeoVector3(0.0, 0.0, 1.0));
            double scale = x.Length;
            double slack = 1E-9 * scale;

            if (!(scale > 0.0) || double.IsInfinity(scale) || Math.Abs(y.Length - scale) > slack || Math.Abs(z.Length - scale) > slack
                || Math.Abs(x.DotProduct(y)) > slack * scale || Math.Abs(x.DotProduct(z)) > slack * scale || Math.Abs(y.DotProduct(z)) > slack * scale)
            {
                throw new ArgumentException("A table is moved, turned, mirrored and scaled alike in every direction only: a shear or a scale unlike along its axes would make its cells other than the rectangles AutoCAD lays it out in.", nameof(transform));
            }

            CheckEditable();
            bool mirrored = x.CrossProduct(y).DotProduct(z) < 0.0;
            bool scaled = Math.Abs(scale - 1.0) > 1E-9;
            GeoVector3 normal = Normal;
            GeoVector3 turnedNormal = DxfOcsTransform.Normalized(transform.Transform(normal));
            GeoVector3 along = Direction;
            GeoPoint3 position = transform.Transform(Position);
            GeoVector3 direction = transform.Transform(along);
            direction = scaled ? direction.Divide(scale) : direction;

            GeoTransform3 linear = transform;

            // Mirrored, the table whose direction's image still reads left to right or upwards, seen from the tip of its normal,
            // stands at the image of its point along that image, its rows running the other way (its flow turned over), as
            // AutoCAD 2021's TransformBy leaves one mirrored in a level line or one at 45 degrees. Any other is the one a turn
            // puts in the box the mirror makes of it, as AutoCAD keeps it readable: its direction reversed from the image, its
            // point the image of the far end of its top edge.
            GeoPoint3 seen = WorldToOcs(turnedNormal, new GeoPoint3(direction.X, direction.Y, direction.Z));
            double eps = Tolerance.Default.EqualVector * direction.Length;
            bool flipFlow = mirrored && (seen.X > eps || (Math.Abs(seen.X) <= eps && seen.Y > 0.0));

            if (mirrored && !flipFlow)
            {
                double width = 0.0;

                for (int col = 0; col < NumColumns; col++)
                {
                    width += ColumnWidth(col);
                }

                GeoVector3 unit = along.Divide(along.Length);
                position = transform.Transform(Position + unit.Multiply(width));
                direction = new GeoVector3(-direction.X, -direction.Y, -direction.Z);

                // The flip of the table across its own column of rows, before the mirror: the turn the block is carried by.
                var flip = new GeoTransform3(new double[,]
                {
                    { 1.0 - 2.0 * unit.X * unit.X, -2.0 * unit.X * unit.Y, -2.0 * unit.X * unit.Z, 0.0 },
                    { -2.0 * unit.Y * unit.X, 1.0 - 2.0 * unit.Y * unit.Y, -2.0 * unit.Y * unit.Z, 0.0 },
                    { -2.0 * unit.Z * unit.X, -2.0 * unit.Z * unit.Y, 1.0 - 2.0 * unit.Z * unit.Z, 0.0 },
                    { 0.0, 0.0, 0.0, 1.0 },
                });
                linear = transform * flip;
            }

            DxfDimGroups g = Own();

            // The sizes first, each in its place, before a point is written where the record had none.
            if (scaled)
            {
                var layout = new Layout(g);
                ScaleSizes(layout, scale);
                ScaleStyleHeights(layout, scale);
            }

            if (flipFlow)
            {
                FlipFlow(g);
            }

            g.SetPoint(10, WorldToOcs(turnedNormal, position), g.Base, Order, Origin);
            g.SetPoint(11, new GeoPoint3(direction.X, direction.Y, direction.Z), g.Sub, Order, new GeoPoint3(1.0, 0.0, 0.0));

            foreach (KeyValuePair<int, string> group in NormalGroups(turnedNormal))
            {
                g.Set(group.Key, group.Value, g.Base, new[] { 100, 2, 10, 20, 30, 210, 220, 230 });
            }

            g.Write(Order);
            DxfBlockTableRecord block = IsResident ? BlockTableRecord : null;

            if (block == null)
            {
                return;
            }

            if (!NamesAlone(block.Name))
            {
                GeometryHelperLog.Debug("DxfTable: the block " + block.Name + " is named by another entity too, and is left as it was drawn, for AutoCAD to draw anew.");
                return;
            }

            // The block's entities stand in the system of the table's normal, placed at its point: turned by the transformation
            // seen from that system, without its move, which the point carries.
            var turn = new GeoTransform3(new double[,]
            {
                { linear[0, 0], linear[0, 1], linear[0, 2], 0.0 },
                { linear[1, 0], linear[1, 1], linear[1, 2], 0.0 },
                { linear[2, 0], linear[2, 1], linear[2, 2], 0.0 },
                { 0.0, 0.0, 0.0, 1.0 },
            });

            turn = DxfDimension.Frame(turnedNormal, true) * turn * DxfDimension.Frame(normal, false);

            foreach (DxfEntity entity in block)
            {
                try
                {
                    entity.TransformBy(turn);
                }
                catch (Exception e) when (e is NotSupportedException || e is ArgumentException)
                {
                    GeometryHelperLog.Debug("DxfTable: an entity of the block " + block.Name + " could not be turned with the table: " + e.Message);
                }
            }
        }

        /// <summary>
        /// Scales the sizes the table holds of its own, each in its place: its rows' heights (141) and columns' widths (142); each
        /// cell's text height (140) and block scale (144) outside its value; and its cell margins (40, 41) and text heights (140)
        /// after its last cell.
        /// </summary>
        private static void ScaleSizes(Layout layout, double scale)
        {
            List<KeyValuePair<int, string>> list = layout.Groups.List;
            var places = new List<int>(layout.RowAt);
            places.AddRange(layout.ColumnAt);

            for (int k = 0; k < layout.CellStart.Count; k++)
            {
                int to = k + 1 < layout.CellStart.Count ? layout.CellStart[k + 1] : layout.CellsEnd;
                bool value = false;

                for (int i = layout.CellStart[k] + 1; i < to; i++)
                {
                    int code = list[i].Key;
                    value = code == 301 && list[i].Value.Trim() == "CELL_VALUE" ? true : code == 304 ? false : value;

                    if (!value && (code == 140 || code == 144))
                    {
                        places.Add(i);
                    }
                }
            }

            int own = layout.CellStart.Count > 0 ? layout.CellsEnd : list.Count;

            for (int i = own; i < layout.Groups.Sub.To; i++)
            {
                int code = list[i].Key;

                if (code == 40 || code == 41 || code == 140)
                {
                    places.Add(i);
                }
            }

            foreach (int at in places)
            {
                if (Numbers.TryRead(list[at].Value, out double size) && Guard.IsFinite(size))
                {
                    list[at] = new KeyValuePair<int, string>(list[at].Key, Numbers.Exact(size * scale + 0.0));
                }
            }
        }

        /// <summary>
        /// Writes the text height the table style gives each kind of row the table gives none of its own, scaled, as the table's
        /// own of that kind (its 140 after its last cell, its flag in 93 from bit 20 on, the title's, the header's, the data's),
        /// as AutoCAD 2021's <c>TransformBy</c> doubles the heights of the style too: of the drawing of point 54, which has no
        /// table style, scaled by 2, the title 12 and the header and the data 9. The style is the one the table names (342), else
        /// the drawing's Standard, else AutoCAD's own Standard, which it makes where the drawing has none: the data and the header
        /// 4.5 and the title 6 in a drawing of millimetres (<c>$MEASUREMENT</c> 1), 0.18 and 0.25 in one of inches, as 43
        /// drawings AutoCAD 2021 saved give it. A table in no drawing has no style to read.
        /// </summary>
        private void ScaleStyleHeights(Layout layout, double scale)
        {
            DxfDimGroups g = layout.Groups;
            int headTo = layout.CellStart.Count > 0 ? layout.CellStart[0] : layout.CellsEnd;
            int cellsEnd = layout.CellsEnd;
            int all = 7 << RowLooks[140];

            // A table that gives every kind of row a height of its own has none of its style to scale.
            if (!IsResident || (g.Whole(93, new DxfDimGroups.Range(g.Sub.From, headTo), 0) & all) == all)
            {
                return;
            }

            double[] heights = StyleTextHeights();

            for (int rowKind = 0; rowKind < 3; rowKind++)
            {
                var head = new DxfDimGroups.Range(g.Sub.From, headTo);
                int looks = g.Whole(93, head, 0);
                int bit = RowLooks[140] + rowKind;

                if ((looks & (1 << bit)) != 0 || double.IsNaN(heights[rowKind]))
                {
                    continue;
                }

                int[] ranks = TailRanks(g, headTo, cellsEnd, out int to);
                g.Insert(PlaceOf(g, ranks, cellsEnd, to, 140, bit), new KeyValuePair<int, string>(140, Numbers.Exact(heights[rowKind] * scale + 0.0)));
                bool had = g.Find(93, head) >= 0;
                g.Set(93, (looks | (1 << bit)).ToString(CultureInfo.InvariantCulture), head, Order);

                if (!had)
                {
                    headTo++;
                    cellsEnd++;
                }
            }
        }

        /// <summary>
        /// The text heights a table style gives the title, the header and the data, in that order, NaN for one it gives none: a
        /// TABLESTYLE gives them as the 140 of each of its three kinds of cell, the data's, the title's and the header's, in
        /// that order, as AutoCAD writes its Standard (0.18, 0.25, 0.18).
        /// </summary>
        private double[] StyleTextHeights()
        {
            DxfFile file = Item.File;
            DxfDimGroups own = Own();
            string handle = own.Text(342, own.Sub)?.Trim();
            DxfItem style = handle == null ? null : file.ObjectItem(handle);

            if (style == null)
            {
                string root = file.RootDictionaryHandle();
                string styles = root == null ? null : Entry(file, root, e => string.Equals(e.Key, "ACAD_TABLESTYLE", StringComparison.OrdinalIgnoreCase));
                string standard = styles == null ? null : Entry(file, styles, e => string.Equals(e.Key, "Standard", StringComparison.OrdinalIgnoreCase));
                style = standard == null ? null : file.ObjectItem(standard);
            }

            if (style == null)
            {
                bool metric = file.Database == null || file.Database.Measurement == DxfMeasurementValue.Metric;
                return metric ? new[] { 6.0, 4.5, 4.5 } : new[] { 0.25, 0.18, 0.18 };
            }

            var found = new List<double>(3);
            bool inStyle = false;

            foreach (DxfTypedValue group in file.ObjectGroupsOf(style))
            {
                if (group.TypeCode == 100)
                {
                    inStyle = group.Value.Trim() == "AcDbTableStyle";
                }
                else if (inStyle && group.TypeCode == 140 && found.Count < 3)
                {
                    found.Add(Numbers.TryRead(group.Value, out double height) && Guard.IsFinite(height) && height > 0.0 ? height : double.NaN);
                }
            }

            while (found.Count < 3)
            {
                found.Add(double.NaN);
            }

            return new[] { found[1], found[2], found[0] };
        }

        #endregion

        #region Reading

        /// <summary>
        /// Gets the entities the table is drawn with, as AutoCAD's <c>Explode</c>: those of its block
        /// (<see cref="BlockTableRecord"/>, <c>*T12</c>), the lines of its cells and their words, placed where the table draws
        /// them, at its <see cref="Position"/> in the plane of its <see cref="Normal"/>, each a copy of its own look. The block is
        /// the picture AutoCAD drew last: a cell's text set since is not in it.
        /// </summary>
        /// <returns>The entities, in no file, to be added to a drawing by <see cref="DxfBlockTableRecord.AppendEntity(DxfEntity)"/>.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the drawing has no block of the name the table is drawn by.</exception>
        public override IReadOnlyList<DxfEntity> Explode()
        {
            DxfBlockTableRecord block = BlockTableRecord ?? throw new InvalidOperationException("The drawing has no block " + PictureName + " the table is drawn with, to explode it into.");
            GeoTransform3 placing = GeoTransform3.Translation(Position.ToVector()) * DxfDimension.Frame(Normal, false) * GeoTransform3.Translation(block.Origin.ToVector().Negate());
            return PlacedCopies(block, placing).AsReadOnly();
        }

        [DxfType("ACAD_TABLE")]
        internal static DxfEntity Create(DxfItem item) => new DxfTable(item);

        private DxfDimGroups Own() => new DxfDimGroups(Item);

        /// <summary>
        /// The table's layout, read once for the record as it stands: kept while the record's bytes are the same, as every
        /// writing gives a record new ones, so that reading every cell of a table of 1 000 rows reads its record once.
        /// </summary>
        private Layout Read()
        {
            DxfFile.Rec record = Item.File.RecordAt(Item, 0);

            if (_layout == null || !ReferenceEquals(_layoutBytes, record.Bytes) || _layoutStart != record.Start || _layoutEnd != record.End)
            {
                _layout = new Layout(Own());
                _layoutBytes = record.Bytes;
                _layoutStart = record.Start;
                _layoutEnd = record.End;
            }

            return _layout;
        }

        /// <summary>The name of the block the table is drawn with (its 2); empty where it names none.</summary>
        private string PictureName
        {
            get
            {
                DxfDimGroups g = Own();
                return DxfReader.NameOf(g.Text(2, g.Base) ?? string.Empty);
            }
        }

        /// <summary>
        /// Whether this table is the one entity of the drawing that names a block: no other table, INSERT or dimension, in model
        /// space, paper space or a block, names it too, as a copy of the table in its own drawing does.
        /// </summary>
        private bool NamesAlone(string name)
        {
            DxfDatabase database = Item.File.Database;

            if (database == null)
            {
                return false;
            }

            int count = 0;

            foreach (DxfBlockTableRecord owner in database.BlockTable)
            {
                foreach (DxfEntity entity in owner)
                {
                    string named = entity is DxfTable table ? table.PictureName : entity is DxfBlockReference insert ? insert.Name : entity is DxfDimension dimension ? dimension.PictureName : null;

                    if (named != null && string.Equals(named, name, StringComparison.OrdinalIgnoreCase) && ++count > 1)
                    {
                        return false;
                    }
                }
            }

            return count == 1;
        }

        /// <summary>The table's layout read anew, to be changed and written.</summary>
        private Layout ReadToWrite() => new Layout(Own());

        private CellView Cell(int row, int col) => Read().CellOf(row, col);

        private CellView CellToWrite(int row, int col) => ReadToWrite().CellOf(row, col);

        private void CheckEditable()
        {
            if (IsErased)
            {
                throw new InvalidOperationException("The ACAD_TABLE " + (Handle ?? string.Empty) + " is erased, and is not changed.");
            }
        }

        /// <summary>A whole number of the table's own after its cells; the fallback where it has none.</summary>
        private int TableWhole(int code, int fallback)
        {
            string text = TableText(code);
            return int.TryParse(text?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : fallback;
        }

        private double TableReal(int code, double fallback) => Numbers.TryRead(TableText(code), out double value) && Guard.IsFinite(value) ? value : fallback;

        /// <summary>
        /// The text of the last group of a code after the table's last cell, where the table's own overrides stand; of the flow
        /// (70), where there is none there, the one of its head, where AutoCAD 2021 writes it after 96.
        /// </summary>
        private string TableText(int code)
        {
            Layout layout = Read();
            int from = layout.CellStart.Count > 0 ? layout.CellsEnd : layout.Groups.List.Count;

            for (int i = layout.Groups.List.Count - 1; i >= from; i--)
            {
                if (layout.Groups.List[i].Key == code)
                {
                    return layout.Groups.List[i].Value;
                }
            }

            if (code == 70)
            {
                DxfDimGroups g = layout.Groups;
                int at = g.Find(70, new DxfDimGroups.Range(g.Sub.From, layout.CellStart.Count > 0 ? layout.CellStart[0] : layout.CellsEnd));
                return at < 0 ? null : g.List[at].Value;
            }

            return null;
        }

        /// <summary>
        /// Turns the table's flow over (its 70, 0 down and 1 up): where the table gives its own after its last cell, there; else
        /// in its head after 96, its flag 0x04 set in 93, where AutoCAD 2021 writes the flow of a table it mirrored.
        /// </summary>
        private void FlipFlow(DxfDimGroups g)
        {
            string turned = FlowDirection == DxfFlowDirection.BottomToTop ? "0" : "1";
            var layout = new Layout(g);

            for (int i = g.List.Count - 1; layout.CellStart.Count > 0 && i >= layout.CellsEnd; i--)
            {
                if (g.List[i].Key == 70)
                {
                    g.List[i] = new KeyValuePair<int, string>(70, turned);
                    return;
                }
            }

            int headTo = layout.CellStart.Count > 0 ? layout.CellStart[0] : layout.CellsEnd;
            var head = new DxfDimGroups.Range(g.Sub.From, headTo);
            bool had = g.Find(93, head) >= 0;
            g.Set(93, (g.Whole(93, head, 0) | 0x04).ToString(CultureInfo.InvariantCulture), head, Order);
            head = new DxfDimGroups.Range(g.Sub.From, had ? headTo : headTo + 1);
            g.Set(70, turned, head, Order);
        }

        /// <summary>
        /// Gives rows or columns a size: nothing where each has it already, whatever the file holds, as a row of height 0; else the
        /// size checked, above nought, and written where it differs.
        /// </summary>
        private void SetSizes(Layout layout, IReadOnlyList<int> places, double size, string parameter, string message)
        {
            bool differs = false;

            foreach (int at in places)
            {
                differs |= !Same(Real(layout.Groups, at), size);
            }

            if (!differs)
            {
                return;
            }

            Guard.Positive(size, parameter, message);
            CheckEditable();
            string text = Numbers.Exact(size + 0.0);

            foreach (int at in places)
            {
                if (!Same(Real(layout.Groups, at), size))
                {
                    layout.Groups.List[at] = new KeyValuePair<int, string>(layout.Groups.List[at].Key, text);
                }
            }

            layout.Groups.Write(Order);
        }

        private static double Real(DxfDimGroups groups, int at) => Numbers.TryRead(groups.List[at].Value, out double value) && Guard.IsFinite(value) ? value : 0.0;

        private static DxfColor Colour(string text)
            => int.TryParse(text?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int index) ? DxfColor.OfIndex(index) : null;

        private static void CheckIndex(int index, int count, string parameter, string what)
        {
            if (index < 0 || index >= count)
            {
                throw new ArgumentOutOfRangeException(parameter, index, "The table has " + count.ToString(CultureInfo.InvariantCulture) + " of " + what + "s, from nought.");
            }
        }

        private static void CheckContent(int contentIndex)
        {
            if (contentIndex != 0)
            {
                throw new ArgumentOutOfRangeException(nameof(contentIndex), contentIndex, "A cell of a file holds one content, nought.");
            }
        }

        /// <summary>The name of the table style of a handle: the key of the entry of ACAD_TABLESTYLE that points at it; null where none does.</summary>
        private static string StyleName(DxfFile file, string handle)
        {
            string root = file.RootDictionaryHandle();
            string styles = root == null ? null : Entry(file, root, e => string.Equals(e.Key, "ACAD_TABLESTYLE", StringComparison.OrdinalIgnoreCase));
            return styles == null ? null : Key(file, styles, handle);
        }

        private static string Entry(DxfFile file, string dictionary, Func<KeyValuePair<string, string>, bool> wanted)
        {
            foreach (KeyValuePair<string, string> entry in Entries(file, dictionary))
            {
                if (wanted(entry))
                {
                    return entry.Value;
                }
            }

            return null;
        }

        private static string Key(DxfFile file, string dictionary, string handle)
        {
            foreach (KeyValuePair<string, string> entry in Entries(file, dictionary))
            {
                if (string.Equals(entry.Value, handle, StringComparison.OrdinalIgnoreCase))
                {
                    return entry.Key;
                }
            }

            return null;
        }

        /// <summary>The entries of a DICTIONARY: each name (3) with the handle (350 or 360) after it.</summary>
        private static IEnumerable<KeyValuePair<string, string>> Entries(DxfFile file, string dictionary)
        {
            DxfItem item = file.ObjectItem(dictionary);

            if (item == null)
            {
                yield break;
            }

            string name = null;

            foreach (DxfTypedValue group in file.ObjectGroupsOf(item))
            {
                if (group.TypeCode == 3)
                {
                    name = group.Value;
                }
                else if ((group.TypeCode == 350 || group.TypeCode == 360) && name != null)
                {
                    yield return new KeyValuePair<string, string>(name.Trim(), group.Value.Trim());
                    name = null;
                }
            }
        }

        /// <summary>The table's head and cells, as the list of its own groups places them.</summary>
        private sealed class Layout
        {
            internal Layout(DxfDimGroups groups)
            {
                Groups = groups;
                DxfDimGroups.Range sub = groups.Sub;
                int first = groups.Find(171, sub);
                var head = new DxfDimGroups.Range(sub.From, first < 0 ? sub.To : first);
                Rows = groups.Whole(91, head, 0);
                Columns = groups.Whole(92, head, 0);

                for (int i = head.From; i < head.To; i++)
                {
                    int code = groups.List[i].Key;

                    if (code == 141)
                    {
                        RowAt.Add(i);
                    }
                    else if (code == 142)
                    {
                        ColumnAt.Add(i);
                    }
                }

                for (int i = first; first >= 0 && i < sub.To; i++)
                {
                    if (groups.List[i].Key == 171)
                    {
                        CellStart.Add(i);
                    }
                }

                CellsEnd = CellStart.Count == 0 ? head.To : EndOfLast(CellStart[CellStart.Count - 1], sub.To);
            }

            // The groups of a cell before its own overrides: its kind, flags, merging, turn, field, text, block and attributes.
            private static readonly HashSet<int> CellHead = new HashSet<int> { 172, 173, 174, 175, 176, 177, 91, 178, 145, 92, 344, 1, 2, 3, 340, 144, 179, 331, 300 };

            // The groups of a cell's own overrides, each with its flag in the cell's 177 or 91.
            internal static readonly Dictionary<int, int> CellOverrides = new Dictionary<int, int>
            {
                [170] = 0x01, [283] = 0x02, [63] = 0x04, [64] = 0x08, [7] = 0x10, [140] = 0x20,
                [69] = 0x40, [65] = 0x80, [66] = 0x100, [68] = 0x200,
                [279] = 0x400, [275] = 0x800, [276] = 0x1000, [278] = 0x2000,
                [289] = 0x4000, [285] = 0x8000, [286] = 0x10000, [288] = 0x20000,
            };

            /// <summary>
            /// Where the last cell ends and the table's own part begins: after its head, its value (301 to 304) and the overrides
            /// its flags give it, each once; the first group of no cell, or an override its flags do not give, is the table's. The
            /// link to data of a text cell, straight after its odd 92 (340, 93, 94, 96), is the cell's; a 93 anywhere else is not.
            /// </summary>
            private int EndOfLast(int from, int to)
            {
                int? flags = null;
                var taken = new HashSet<int>();
                int i = from + 1;

                while (i < to)
                {
                    int code = Groups.List[i].Key;

                    if (code == 301 && Groups.List[i].Value.Trim() == "CELL_VALUE")
                    {
                        while (i < to && Groups.List[i].Key != 304)
                        {
                            i++;
                        }

                        i++;
                        continue;
                    }

                    if (CellHead.Contains(code))
                    {
                        if ((code == 177 || code == 91) && flags == null && int.TryParse(Groups.List[i].Value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int read))
                        {
                            flags = read;
                        }

                        i += code == 92 && IsTextCell(Groups.List, from) ? 1 + LinkRun(Groups.List, i, to) : 1;
                        continue;
                    }

                    if (CellOverrides.TryGetValue(code, out int bit) && (flags == null || (flags.Value & bit) != 0) && taken.Add(code))
                    {
                        i++;
                        continue;
                    }

                    break;
                }

                return Math.Min(i, to);
            }

            /// <summary>Where the last cell ends: the table's own part, its overrides, from here on.</summary>
            internal int CellsEnd { get; }

            internal DxfDimGroups Groups { get; }

            internal int Rows { get; }

            internal int Columns { get; }

            internal List<int> RowAt { get; } = new List<int>();

            internal List<int> ColumnAt { get; } = new List<int>();

            internal List<int> CellStart { get; } = new List<int>();

            /// <summary>A cell of the table, its row and column checked.</summary>
            /// <exception cref="ArgumentOutOfRangeException">Thrown when the cell is not one of the table's.</exception>
            internal CellView CellOf(int row, int col)
            {
                CheckIndex(row, Rows, nameof(row), "row");
                CheckIndex(col, Columns, nameof(col), "column");
                int index = row * Columns + col;

                if (index >= CellStart.Count)
                {
                    throw new ArgumentOutOfRangeException(nameof(row), row, "The table's record holds " + CellStart.Count.ToString(CultureInfo.InvariantCulture) + " cells, fewer than its rows by its columns.");
                }

                return CellAt(row, col);
            }

            /// <summary>The cell of a row and a column, from its 171 to the next cell's, the last up to the table's own part.</summary>
            internal CellView CellAt(int row, int col)
            {
                int index = row * Columns + col;
                int to = index + 1 < CellStart.Count ? CellStart[index + 1] : CellsEnd;
                return new CellView(Groups, CellStart[index], to);
            }
        }

        /// <summary>A cell, as the groups from its 171 to the next cell's.</summary>
        private sealed class CellView
        {
            // The groups of a cell's head in the order AutoCAD writes them: its flags in 177 before R2007 and in 91 from R2007
            // on, in the one place; its text, before R2007, in 2 and 1.
            private static readonly int[] HeadOrder = { 171, 172, 173, 174, 175, 176, 177, 91, 178, 145, 92, 344, 2, 1, 3, 340, 144, 179, 331, 300 };

            // The groups of a cell's own overrides in the order of the DXF reference, after its head and its value.
            private static readonly int[] OverrideOrder = { 7, 140, 170, 64, 63, 69, 65, 66, 68, 279, 275, 276, 278, 283, 289, 285, 286, 288 };

            internal CellView(DxfDimGroups groups, int from, int to)
            {
                Groups = groups;
                From = from;
                To = to;
                ValueFrom = -1;
                ValueTo = -1;

                for (int i = from; i < to; i++)
                {
                    if (groups.List[i].Key == 301 && groups.List[i].Value.Trim() == "CELL_VALUE" && ValueFrom < 0)
                    {
                        ValueFrom = i;
                    }
                    else if (groups.List[i].Key == 304 && ValueFrom >= 0 && ValueTo < 0)
                    {
                        ValueTo = i;
                    }
                }
            }

            internal DxfDimGroups Groups { get; }

            internal int From { get; }

            internal int To { get; set; }

            /// <summary>Where its value of R2007 opens (301 CELL_VALUE); -1 where it has none.</summary>
            internal int ValueFrom { get; }

            /// <summary>Where that value closes (304 ACVALUE_END); -1 where it has none.</summary>
            internal int ValueTo { get; }

            /// <summary>The flags of its overrides: 91 from R2007 on, 177 before; null where it gives none.</summary>
            internal int? Flags
            {
                get
                {
                    string text = Head(91) ?? Head(177);
                    return int.TryParse(text?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int flags) ? flags : (int?)null;
                }
            }

            /// <summary>The first group of a code of the cell before its value (or its text, before R2007).</summary>
            internal string Head(int code)
            {
                int end = ValueFrom >= 0 ? ValueFrom : To;

                for (int i = From; i < end; i++)
                {
                    if (Groups.List[i].Key == code)
                    {
                        return Groups.List[i].Value;
                    }
                }

                return null;
            }

            internal int Whole(int code, int fallback)
                => int.TryParse(Head(code)?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : fallback;

            /// <summary>The cell's own override of a code, outside its value; null where its flags say it has none, or it has no such group.</summary>
            internal string Override(int code, int bit)
            {
                int? flags = Flags;

                if (flags.HasValue && (flags.Value & bit) == 0)
                {
                    return null;
                }

                for (int i = From + 1; i < To; i++)
                {
                    if ((ValueFrom < 0 || i < ValueFrom || i > ValueTo) && Groups.List[i].Key == code)
                    {
                        return Groups.List[i].Value;
                    }
                }

                return null;
            }

            /// <summary>The cell's text: the 302 of its value from R2007 on, else its 2s and 1 one after another.</summary>
            internal string Text()
            {
                if (ValueFrom >= 0)
                {
                    int end = ValueTo >= 0 ? ValueTo : To;

                    for (int i = ValueFrom; i < end; i++)
                    {
                        if (Groups.List[i].Key == 302)
                        {
                            return Groups.List[i].Value;
                        }
                    }

                    return Joined(ValueFrom, end);
                }

                return Joined(From, To);
            }

            /// <summary>The cell's value: of the kind its value says from R2007 on, else its text.</summary>
            internal object Value()
            {
                if (ValueFrom < 0)
                {
                    return Text();
                }

                int end = ValueTo >= 0 ? ValueTo : To;
                int kind = 4;
                string number = null;
                string real = null;

                for (int i = ValueFrom; i < end; i++)
                {
                    int code = Groups.List[i].Key;

                    if (code == 90)
                    {
                        int.TryParse(Groups.List[i].Value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out kind);
                    }
                    else if (code == 91)
                    {
                        number = number ?? Groups.List[i].Value;
                    }
                    else if (code == 140)
                    {
                        real = real ?? Groups.List[i].Value;
                    }
                }

                if (kind == 1 && int.TryParse(number?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int whole))
                {
                    return whole;
                }

                if (kind == 2 && Numbers.TryRead(real, out double value) && Guard.IsFinite(value))
                {
                    return value;
                }

                return Text();
            }

            /// <summary>Writes the cell's text into the list: its value as a text from R2007 on, else its 2s and 1; a 344 taken out.</summary>
            internal void SetText(string text)
            {
                List<KeyValuePair<int, string>> pieces = Pieces(text);

                if (ValueFrom >= 0)
                {
                    WriteValue("4", pieces, text, null, null);
                }
                else
                {
                    int at = -1;

                    for (int i = To - 1; i > From; i--)
                    {
                        int code = Groups.List[i].Key;

                        if (code == 1 || code == 2 || code == 3)
                        {
                            Groups.RemoveAt(i);
                            at = i;
                        }
                    }

                    if (at < 0)
                    {
                        // After the cell's turn (145), where AutoCAD writes a cell's text.
                        at = From + 1;

                        for (int i = From + 1; i < To; i++)
                        {
                            if (Groups.List[i].Key == 145 || Groups.List[i].Key == 344)
                            {
                                at = i + 1;
                            }
                        }
                    }

                    for (int k = 0; k < pieces.Count; k++)
                    {
                        Groups.Insert(at + k, pieces[k]);
                    }
                }

                DropField();
            }

            /// <summary>Takes out the cell's pointer to a field (344): its text is its own now, not a field's.</summary>
            internal void DropField()
            {
                for (int i = From; i < Groups.List.Count && (i == From || Groups.List[i].Key != 171); i++)
                {
                    if (Groups.List[i].Key == 344)
                    {
                        Groups.RemoveAt(i);
                        break;
                    }
                }
            }

            /// <summary>
            /// Writes the cell's value of R2007 anew, from its 301 to its 304: its flags (93) kept, its kind (90), its data, its
            /// unit (94) and format (300), each kept where null is given, and its text (302).
            /// </summary>
            internal void WriteValue(string kind, IEnumerable<KeyValuePair<int, string>> data, string text, string unit, string format)
            {
                int end = ValueTo >= 0 ? ValueTo : To - 1;
                var value = new List<KeyValuePair<int, string>> { Groups.List[ValueFrom] };
                string flags = null;
                string unitNow = null;
                string formatNow = null;

                for (int i = ValueFrom + 1; i < end; i++)
                {
                    int code = Groups.List[i].Key;
                    flags = code == 93 && flags == null ? Groups.List[i].Value : flags;
                    unitNow = code == 94 && unitNow == null ? Groups.List[i].Value : unitNow;
                    formatNow = code == 300 && formatNow == null ? Groups.List[i].Value : formatNow;
                }

                value.Add(new KeyValuePair<int, string>(93, flags ?? "6"));
                value.Add(new KeyValuePair<int, string>(90, kind));
                value.AddRange(data);
                value.Add(new KeyValuePair<int, string>(94, unit ?? unitNow ?? "0"));
                value.Add(new KeyValuePair<int, string>(300, format ?? formatNow ?? string.Empty));
                value.Add(new KeyValuePair<int, string>(302, text));

                for (int i = end - 1; i >= ValueFrom; i--)
                {
                    Groups.RemoveAt(i);
                }

                for (int k = 0; k < value.Count; k++)
                {
                    Groups.Insert(ValueFrom + k, value[k]);
                }
            }

            /// <summary>The first group of a code within the cell's value of R2007; null where it has none, or no value.</summary>
            internal string InValue(int code)
            {
                int end = ValueTo >= 0 ? ValueTo : To;

                for (int i = ValueFrom + 1; ValueFrom >= 0 && i < end; i++)
                {
                    if (Groups.List[i].Key == code)
                    {
                        return Groups.List[i].Value;
                    }
                }

                return null;
            }

            /// <summary>The groups of the cell's value of R2007 that carry its data: all but its flags, kind, unit, format and text.</summary>
            internal List<KeyValuePair<int, string>> ValueData()
            {
                var data = new List<KeyValuePair<int, string>>();
                int end = ValueTo >= 0 ? ValueTo : To - 1;

                for (int i = ValueFrom + 1; i < end; i++)
                {
                    int code = Groups.List[i].Key;

                    if (code != 93 && code != 90 && code != 94 && code != 300 && code != 302)
                    {
                        data.Add(Groups.List[i]);
                    }
                }

                return data;
            }

            /// <summary>Where the cell's own overrides begin: after its value from R2007 on, else at the first of them, else at its end.</summary>
            internal int OverridesFrom
            {
                get
                {
                    if (ValueFrom >= 0)
                    {
                        return ValueTo >= 0 ? ValueTo + 1 : To;
                    }

                    for (int i = From + 1; i < To; i++)
                    {
                        if (Layout.CellOverrides.ContainsKey(Groups.List[i].Key))
                        {
                            return i;
                        }
                    }

                    return To;
                }
            }

            /// <summary>
            /// Gives a group of the cell's head a text: in place where it has one, else put in after the last group of the head
            /// whose code comes before it in the order AutoCAD writes them.
            /// </summary>
            internal void SetHead(int code, string text)
            {
                int end = ValueFrom >= 0 ? ValueFrom : OverridesFrom;
                int rank = Array.IndexOf(HeadOrder, code);
                int after = From;

                for (int i = From + 1; i < end; i++)
                {
                    int key = Groups.List[i].Key;

                    if (key == code)
                    {
                        Groups.List[i] = new KeyValuePair<int, string>(code, text);
                        return;
                    }

                    int their = Array.IndexOf(HeadOrder, key);
                    after = their >= 0 && their < rank ? i : after;
                }

                // A text cell's link to data stays straight after its 92: what goes after the 92 goes after the link.
                if (Groups.List[after].Key == 92 && IsTextCell(Groups.List, From))
                {
                    after += LinkRun(Groups.List, after, end);
                }

                Groups.Insert(after + 1, new KeyValuePair<int, string>(code, text));
                To++;
            }

            /// <summary>
            /// Gives the cell an override of its own: its group written in place, or put in among its overrides in the order of
            /// the DXF reference, and its flag set; a cell of no flags given the flags of the overrides it holds, in a group of
            /// the code given (91 from R2007 on, 177 before).
            /// </summary>
            internal void SetOverride(int code, string text, int flagsCode)
            {
                int? flags = Flags;
                int now = flags ?? Held();
                int bit = Layout.CellOverrides[code];
                int start = OverridesFrom;
                int rank = Array.IndexOf(OverrideOrder, code);
                int at = -1;
                int after = -1;

                for (int i = start; i < To; i++)
                {
                    int key = Groups.List[i].Key;
                    at = key == code && at < 0 ? i : at;
                    int their = Array.IndexOf(OverrideOrder, key);
                    after = their >= 0 && their < rank ? i : after;
                }

                if (at >= 0)
                {
                    Groups.List[at] = new KeyValuePair<int, string>(code, text);
                }
                else
                {
                    Groups.Insert(after >= 0 ? after + 1 : start, new KeyValuePair<int, string>(code, text));
                    To++;
                }

                if (flags != (now | bit))
                {
                    SetFlags(now | bit, flagsCode);
                }
            }

            /// <summary>Takes out the cell's own overrides whose flags are in a mask, their groups and their flags; the other flags kept.</summary>
            internal void RemoveOverrides(int mask)
            {
                var marked = new bool[Groups.List.Count];
                MarkOverrides(mask, marked);
                To -= Groups.RemoveMarked(marked);
            }

            /// <summary>
            /// Marks the cell's own overrides whose flags are in a mask to be taken out, and takes their flags away, the other
            /// flags kept: every group stays where it stands until the groups marked of every cell are taken out together.
            /// </summary>
            internal void MarkOverrides(int mask, bool[] marked)
            {
                for (int i = OverridesFrom; i < To; i++)
                {
                    if (Layout.CellOverrides.TryGetValue(Groups.List[i].Key, out int bit) && (bit & mask) != 0)
                    {
                        marked[i] = true;
                    }
                }

                int? flags = Flags;

                if (flags.HasValue && (flags.Value & mask) != 0)
                {
                    SetFlags(flags.Value & ~mask, 177);
                }
            }

            /// <summary>The flags of the overrides the cell holds among its own, for a cell that gives none.</summary>
            internal int Held()
            {
                int held = 0;

                for (int i = OverridesFrom; i < To; i++)
                {
                    held |= Layout.CellOverrides.TryGetValue(Groups.List[i].Key, out int bit) ? bit : 0;
                }

                return held;
            }

            /// <summary>Writes the flags of the cell's overrides: in its 91, else its 177, else in a new group of the code given.</summary>
            private void SetFlags(int flags, int code)
            {
                string text = flags.ToString(CultureInfo.InvariantCulture);
                int end = ValueFrom >= 0 ? ValueFrom : To;

                foreach (int which in new[] { 91, 177 })
                {
                    for (int i = From + 1; i < end; i++)
                    {
                        if (Groups.List[i].Key == which)
                        {
                            Groups.List[i] = new KeyValuePair<int, string>(which, text);
                            return;
                        }
                    }
                }

                SetHead(code, text);
            }

            /// <summary>A text in pieces of 250 letters, as the DXF reference writes a long one: 2 for each but the last, 1 for that.</summary>
            internal static List<KeyValuePair<int, string>> Pieces(string text)
            {
                var pieces = new List<KeyValuePair<int, string>>();
                int at = 0;

                while (text.Length - at > 250)
                {
                    pieces.Add(new KeyValuePair<int, string>(2, text.Substring(at, 250)));
                    at += 250;
                }

                pieces.Add(new KeyValuePair<int, string>(1, text.Substring(at)));
                return pieces;
            }

            private string Joined(int from, int to)
            {
                var text = new StringBuilder();

                for (int i = from + 1; i < to; i++)
                {
                    int code = Groups.List[i].Key;

                    if (code == 1 || code == 2 || code == 3)
                    {
                        text.Append(Groups.List[i].Value);
                    }
                }

                return text.ToString();
            }
        }

        #endregion
    }
}
