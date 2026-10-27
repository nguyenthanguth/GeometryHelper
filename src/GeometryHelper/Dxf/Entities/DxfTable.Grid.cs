using System;
using System.Collections.Generic;
using System.Globalization;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// The lines of a table's grid, as AutoCAD's <c>Table</c> reads and sets them: the edges of a cell, each the cell's own
    /// override with its flag, and the lines of the rows of a kind, the table's own with its flags 94 to 96.
    /// </summary>
    public sealed partial class DxfTable
    {
        // The groups of a cell's edges, top, right, bottom and left: their colours, lineweights and showing.
        private static readonly int[][] EdgeCodes =
        {
            new[] { 69, 65, 66, 68 },
            new[] { 279, 275, 276, 278 },
            new[] { 289, 285, 286, 288 },
        };

        // The lines of a cell's edges, top, right, bottom and left, alone and inside a block of cells.
        private static readonly DxfGridLineType[] EdgeOuter = { DxfGridLineType.HorizontalTop, DxfGridLineType.VerticalRight, DxfGridLineType.HorizontalBottom, DxfGridLineType.VerticalLeft };

        // The groups of the table's lines of the rows of a kind, after its last cell, in the order of AutoCAD's GridLineType
        // (top, inside and bottom across, left, inside and right down): colours with the flags of 94, lineweights of 95,
        // showing of 96.
        private static readonly int[][] LineCodes =
        {
            new[] { 64, 65, 66, 63, 68, 69 },
            new[] { 274, 275, 276, 277, 278, 279 },
            new[] { 284, 285, 286, 287, 288, 289 },
        };

        // The table's row colours (64), fills (283, 63), alignments (170), text styles (7) and heights (140) by row kind, after
        // its last cell before the lines, each with its flag in 93 from the bit given on, as the DXF reference lists them.
        private static readonly Dictionary<int, int> RowLooks = new Dictionary<int, int> { [64] = 5, [283] = 8, [63] = 11, [170] = 14, [7] = 17, [140] = 20 };

        #region The edges of a cell

        /// <summary>
        /// Gets the colour of a line of a cell, as AutoCAD's <c>GetGridColor(row, column, gridLineType)</c>: the cell's own of
        /// its top (69), right (65), bottom (66) or left (68) edge, of the index; null where it has none, its table's then.
        /// </summary>
        /// <param name="row">The row, from nought.</param>
        /// <param name="column">The column, from nought.</param>
        /// <param name="gridLineType">The line: one of <see cref="DxfGridLineType.HorizontalTop"/>, <see cref="DxfGridLineType.HorizontalBottom"/>, <see cref="DxfGridLineType.VerticalLeft"/> and <see cref="DxfGridLineType.VerticalRight"/>.</param>
        /// <returns>The colour, or null.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the cell is not one of the table's, or the line is not one of a cell's four.</exception>
        public DxfColor GetGridColor(int row, int column, DxfGridLineType gridLineType) => Colour(EdgeOverride(row, column, EdgeOf(gridLineType, nameof(gridLineType)), 0));

        /// <summary>
        /// Gets the lineweight of a line of a cell, as AutoCAD's <c>GetGridLineWeight(row, column, gridLineType)</c>: the cell's
        /// own of its top (279), right (275), bottom (276) or left (278) edge; null where it has none.
        /// </summary>
        /// <param name="row">The row, from nought.</param>
        /// <param name="column">The column, from nought.</param>
        /// <param name="gridLineType">The line: one of a cell's four.</param>
        /// <returns>The lineweight, or null.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the cell is not one of the table's, or the line is not one of a cell's four.</exception>
        public DxfLineWeight? GetGridLineWeight(int row, int column, DxfGridLineType gridLineType) => Weight(EdgeOverride(row, column, EdgeOf(gridLineType, nameof(gridLineType)), 1));

        /// <summary>
        /// Gets whether a line of a cell is drawn, as AutoCAD's <c>GetGridVisibility(row, column, gridLineType)</c>: the cell's
        /// own of its top (289), right (285), bottom (286) or left (288) edge, 1 drawn and 0 not; null where it has none.
        /// </summary>
        /// <param name="row">The row, from nought.</param>
        /// <param name="column">The column, from nought.</param>
        /// <param name="gridLineType">The line: one of a cell's four.</param>
        /// <returns>Whether it is drawn, or null.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the cell is not one of the table's, or the line is not one of a cell's four.</exception>
        public bool? GetGridVisibility(int row, int column, DxfGridLineType gridLineType) => Shown(EdgeOverride(row, column, EdgeOf(gridLineType, nameof(gridLineType)), 2));

        /// <summary>
        /// Gets what a line of a cell holds of its own, as AutoCAD's <c>GetGridProperty(row, column, gridLineType)</c>: its
        /// colour, lineweight and showing where it has them, each named in the mask; the mask
        /// <see cref="DxfGridProperties.Invalid"/> for a line of none, which takes all from its table.
        /// </summary>
        /// <param name="row">The row, from nought.</param>
        /// <param name="column">The column, from nought.</param>
        /// <param name="gridLineType">The line: one of a cell's four.</param>
        /// <returns>The properties.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the cell is not one of the table's, or the line is not one of a cell's four.</exception>
        public DxfGridPropertyParameter GetGridProperty(int row, int column, DxfGridLineType gridLineType)
        {
            int edge = EdgeOf(gridLineType, nameof(gridLineType));
            var found = new DxfGridPropertyParameter();
            DxfColor color = Colour(EdgeOverride(row, column, edge, 0));
            DxfLineWeight? weight = Weight(EdgeOverride(row, column, edge, 1));
            bool? shown = Shown(EdgeOverride(row, column, edge, 2));

            if (color != null)
            {
                found.PropertyMask |= DxfGridProperties.Color;
                found.Color = color;
            }

            if (weight.HasValue)
            {
                found.PropertyMask |= DxfGridProperties.LineWeight;
                found.LineWeight = weight.Value;
            }

            if (shown.HasValue)
            {
                found.PropertyMask |= DxfGridProperties.Visibility;
                found.Visible = shown.Value;
            }

            return found;
        }

        /// <summary>Gets the colour of an edge of a cell, as AutoCAD's <c>GridColor(row, col, edge)</c>: as <see cref="GetGridColor(int, int, DxfGridLineType)"/>.</summary>
        /// <param name="row">The row, from nought.</param>
        /// <param name="col">The column, from nought.</param>
        /// <param name="edge">The edge, one of the four.</param>
        /// <returns>The colour, or null where the cell has none of its own.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the cell is not one of the table's, or the edge is not one of the four.</exception>
        public DxfColor GridColor(int row, int col, DxfCellEdgeMasks edge) => Colour(EdgeOverride(row, col, EdgeOf(edge, nameof(edge)), 0));

        /// <summary>Gets the lineweight of an edge of a cell, as AutoCAD's <c>GridLineWeight(row, col, edge)</c>: as <see cref="GetGridLineWeight(int, int, DxfGridLineType)"/>.</summary>
        /// <param name="row">The row, from nought.</param>
        /// <param name="col">The column, from nought.</param>
        /// <param name="edge">The edge, one of the four.</param>
        /// <returns>The lineweight, or null where the cell has none of its own.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the cell is not one of the table's, or the edge is not one of the four.</exception>
        public DxfLineWeight? GridLineWeight(int row, int col, DxfCellEdgeMasks edge) => Weight(EdgeOverride(row, col, EdgeOf(edge, nameof(edge)), 1));

        /// <summary>Gets whether an edge of a cell is drawn, as AutoCAD's <c>GridVisibility(row, col, edge)</c>: as <see cref="GetGridVisibility(int, int, DxfGridLineType)"/>.</summary>
        /// <param name="row">The row, from nought.</param>
        /// <param name="col">The column, from nought.</param>
        /// <param name="edge">The edge, one of the four.</param>
        /// <returns>Whether it is drawn, or null where the cell has none of its own.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the cell is not one of the table's, or the edge is not one of the four.</exception>
        public bool? GridVisibility(int row, int col, DxfCellEdgeMasks edge) => Shown(EdgeOverride(row, col, EdgeOf(edge, nameof(edge)), 2));

        /// <summary>
        /// Sets the colour of lines of a cell, as AutoCAD's <c>SetGridColor(row, column, gridLineType, color)</c>: the cell's own
        /// of each edge the lines name (top 69, right 65, bottom 66, left 68), of the index, its flag (0x40 to 0x200) set in its
        /// 177 (91 from R2007 on); <see cref="DxfGridLineType.OuterGridLines"/> gives all four, the lines inside, a block's,
        /// none of a cell alone. The edge of the cell beside it, which AutoCAD draws on the same line, is left as it is. The
        /// block AutoCAD drew the table with is not drawn anew.
        /// </summary>
        /// <param name="row">The row, from nought.</param>
        /// <param name="column">The column, from nought.</param>
        /// <param name="gridLineType">The lines, one or more of the cell's four.</param>
        /// <param name="color">The colour: of the index, BYLAYER or BYBLOCK.</param>
        /// <exception cref="ArgumentNullException">Thrown when the colour is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the cell is not one of the table's, or the lines hold a flag none of <see cref="DxfGridLineType"/> has.</exception>
        /// <exception cref="ArgumentException">Thrown when the lines name none of the cell's four, or the colour is a true colour or none.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the table is erased.</exception>
        public void SetGridColor(int row, int column, DxfGridLineType gridLineType, DxfColor color)
            => SetLines(OneCell(row, column), gridLineType, new[] { IndexText(color, nameof(color)), null, null }, nameof(gridLineType), nameof(row));

        /// <summary>
        /// Sets the lineweight of lines of a cell, as AutoCAD's <c>SetGridLineWeight(row, column, gridLineType, lineWeight)</c>:
        /// the cell's own of each edge the lines name (top 279, right 275, bottom 276, left 278), its flag (0x400 to 0x2000)
        /// set, as <see cref="SetGridColor(int, int, DxfGridLineType, DxfColor)"/> sets a colour.
        /// </summary>
        /// <param name="row">The row, from nought.</param>
        /// <param name="column">The column, from nought.</param>
        /// <param name="gridLineType">The lines, one or more of the cell's four.</param>
        /// <param name="lineWeight">The lineweight: one of AutoCAD's widths, BYLAYER, BYBLOCK or the default.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the cell is not one of the table's, the lines hold a flag none of <see cref="DxfGridLineType"/> has, or the lineweight is none DXF writes.</exception>
        /// <exception cref="ArgumentException">Thrown when the lines name none of the cell's four.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the table is erased.</exception>
        public void SetGridLineWeight(int row, int column, DxfGridLineType gridLineType, DxfLineWeight lineWeight)
            => SetLines(OneCell(row, column), gridLineType, new[] { null, WeightText(lineWeight, nameof(lineWeight)), null }, nameof(gridLineType), nameof(row));

        /// <summary>
        /// Sets whether lines of a cell are drawn, as AutoCAD's <c>SetGridVisibility(row, column, gridLineType, visibility)</c>:
        /// the cell's own of each edge the lines name (top 289, right 285, bottom 286, left 288), 1 drawn and 0 not, its flag
        /// (0x4000 to 0x20000) set.
        /// </summary>
        /// <param name="row">The row, from nought.</param>
        /// <param name="column">The column, from nought.</param>
        /// <param name="gridLineType">The lines, one or more of the cell's four.</param>
        /// <param name="visible">Whether they are drawn.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the cell is not one of the table's, or the lines hold a flag none of <see cref="DxfGridLineType"/> has.</exception>
        /// <exception cref="ArgumentException">Thrown when the lines name none of the cell's four.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the table is erased.</exception>
        public void SetGridVisibility(int row, int column, DxfGridLineType gridLineType, bool visible)
            => SetLines(OneCell(row, column), gridLineType, new[] { null, null, visible ? "1" : "0" }, nameof(gridLineType), nameof(row));

        /// <summary>
        /// Sets properties of lines of a cell, as AutoCAD's <c>SetGridProperty(row, column, gridLineType, gridProperty)</c>: those
        /// the mask names, each as its own setter writes it; a mask of the line style, the linetype or the spacing of a double
        /// line, for which a file of these versions has no groups, is refused.
        /// </summary>
        /// <param name="row">The row, from nought.</param>
        /// <param name="column">The column, from nought.</param>
        /// <param name="gridLineType">The lines, one or more of the cell's four.</param>
        /// <param name="gridProperty">The properties, those of its mask written.</param>
        /// <exception cref="ArgumentNullException">Thrown when the properties are null, or a colour named is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the cell is not one of the table's, the lines or the mask hold a flag none of their kind has, or a lineweight is none DXF writes.</exception>
        /// <exception cref="ArgumentException">Thrown when the lines name none of the cell's four, the mask names a property of no group, or a colour is a true colour or none.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the table is erased.</exception>
        public void SetGridProperty(int row, int column, DxfGridLineType gridLineType, DxfGridPropertyParameter gridProperty)
            => SetLines(OneCell(row, column), gridLineType, PropertyTexts(gridProperty, nameof(gridProperty)), nameof(gridLineType), nameof(row));

        /// <summary>
        /// Sets properties of the lines of a block of cells, as AutoCAD's <c>SetGridProperty(rangeIn, gridLineTypes,
        /// gridProp)</c>: the lines round the block on the outer edges of its outer cells, and the lines inside on the edges of
        /// the cells either side of each, so that the range (1, 0) to (2, 1) given <see cref="DxfGridLineType.HorizontalInside"/>
        /// sets the bottom edges of (1, 0) and (1, 1) and the top edges of (2, 0) and (2, 1). The cells are written in one
        /// writing of the record, so that the lines of a table of 16 000 rows are set in time linear in its groups.
        /// </summary>
        /// <param name="rangeIn">The block of cells, within the table.</param>
        /// <param name="gridLineTypes">The lines, one or more of the six.</param>
        /// <param name="gridProp">The properties, those of its mask written.</param>
        /// <exception cref="ArgumentNullException">Thrown when the range or the properties are null, or a colour named is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the range reaches past the table, the lines or the mask hold a flag none of their kind has, or a lineweight is none DXF writes.</exception>
        /// <exception cref="ArgumentException">Thrown when the lines name none of the block's, the mask names a property of no group, or a colour is a true colour or none.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the table is erased.</exception>
        public void SetGridProperty(DxfCellRange rangeIn, DxfGridLineType gridLineTypes, DxfGridPropertyParameter gridProp)
        {
            if (rangeIn == null)
            {
                throw new ArgumentNullException(nameof(rangeIn));
            }

            SetLines(rangeIn, gridLineTypes, PropertyTexts(gridProp, nameof(gridProp)), nameof(gridLineTypes), nameof(rangeIn));
        }

        #endregion

        #region The lines of the rows of a kind

        /// <summary>
        /// Gets the colour of a line of the rows of a kind, as AutoCAD's <c>GridColor(gridlineType, type)</c>: the table's own,
        /// after its last cell, of the index (64, 65, 66, 63, 68, 69 for the lines across top, inside and bottom and down left,
        /// inside and right), its flag in 94; null where it has none, its table style's then. The groups of one code are read
        /// in the order of their flags, the title's, the header's, then the data's, and after the table's colours of its rows
        /// (64 and 63 with their flags in 93), as AutoCAD writes them.
        /// </summary>
        /// <param name="gridlineType">The line: one of the six.</param>
        /// <param name="type">The rows: one of <see cref="DxfRowType.TitleRow"/>, <see cref="DxfRowType.HeaderRow"/> and <see cref="DxfRowType.DataRow"/>.</param>
        /// <returns>The colour, or null.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the line is not one of the six, or the rows not of one kind.</exception>
        public DxfColor GridColor(DxfGridLineType gridlineType, DxfRowType type) => Colour(TableLine(0, LineOf(gridlineType, nameof(gridlineType)), RowOf(type, nameof(type))));

        /// <summary>
        /// Gets the lineweight of a line of the rows of a kind, as AutoCAD's <c>GridLineWeight(gridlineType, type)</c>: the
        /// table's own (274 to 279 in the order of the lines), its flag in 95, as <see cref="GridColor(DxfGridLineType, DxfRowType)"/>
        /// reads a colour; null where it has none.
        /// </summary>
        /// <param name="gridlineType">The line: one of the six.</param>
        /// <param name="type">The rows: one kind.</param>
        /// <returns>The lineweight, or null.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the line is not one of the six, or the rows not of one kind.</exception>
        public DxfLineWeight? GridLineWeight(DxfGridLineType gridlineType, DxfRowType type) => Weight(TableLine(1, LineOf(gridlineType, nameof(gridlineType)), RowOf(type, nameof(type))));

        /// <summary>
        /// Gets whether a line of the rows of a kind is drawn, as AutoCAD's <c>GridVisibility(gridlineType, type)</c>: the
        /// table's own (284 to 289, 1 drawn and 0 not), its flag in 96; null where it has none.
        /// </summary>
        /// <param name="gridlineType">The line: one of the six.</param>
        /// <param name="type">The rows: one kind.</param>
        /// <returns>Whether it is drawn, or null.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the line is not one of the six, or the rows not of one kind.</exception>
        public bool? GridVisibility(DxfGridLineType gridlineType, DxfRowType type) => Shown(TableLine(2, LineOf(gridlineType, nameof(gridlineType)), RowOf(type, nameof(type))));

        /// <summary>
        /// Sets the colour of lines of the rows of kinds, as AutoCAD's <c>SetGridColor(color, borders, rows)</c>: the table's
        /// own of each line and kind, of the index, written in place or put in after its last cell in the order AutoCAD writes
        /// them, its flag set in 94: the title's lines across the top and inside, <c>SetGridColor(color, HorizontalTop |
        /// HorizontalInside, TitleRow)</c>, set the flags 1 and 2. The cells' own edges are left as they are. The block AutoCAD
        /// drew the table with is not drawn anew.
        /// </summary>
        /// <param name="color">The colour: of the index, BYLAYER or BYBLOCK.</param>
        /// <param name="borders">The lines, one or more of the six.</param>
        /// <param name="rows">The rows, one or more of the title, the header and the data.</param>
        /// <exception cref="ArgumentNullException">Thrown when the colour is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the lines or the rows are none, or hold a flag none of their kind has.</exception>
        /// <exception cref="ArgumentException">Thrown when the colour is a true colour or none.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the table is erased.</exception>
        public void SetGridColor(DxfColor color, DxfGridLineType borders, DxfRowType rows) => SetTableLines(0, IndexText(color, nameof(color)), borders, rows);

        /// <summary>
        /// Sets the lineweight of lines of the rows of kinds, as AutoCAD's <c>SetGridLineWeight(lineWeight, borders, rows)</c>:
        /// the table's own (274 to 279), its flag set in 95, as <see cref="SetGridColor(DxfColor, DxfGridLineType, DxfRowType)"/>
        /// sets a colour.
        /// </summary>
        /// <param name="lineWeight">The lineweight: one of AutoCAD's widths, BYLAYER, BYBLOCK or the default.</param>
        /// <param name="borders">The lines, one or more of the six.</param>
        /// <param name="rows">The rows, one or more of the three kinds.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the lines or the rows are none or hold a flag none of their kind has, or the lineweight is none DXF writes.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the table is erased.</exception>
        public void SetGridLineWeight(DxfLineWeight lineWeight, DxfGridLineType borders, DxfRowType rows) => SetTableLines(1, WeightText(lineWeight, nameof(lineWeight)), borders, rows);

        /// <summary>
        /// Sets whether lines of the rows of kinds are drawn, as AutoCAD's <c>SetGridVisibility(visible, borders, rows)</c>: the
        /// table's own (284 to 289, 1 drawn and 0 not), its flag set in 96.
        /// </summary>
        /// <param name="visible">Whether they are drawn.</param>
        /// <param name="borders">The lines, one or more of the six.</param>
        /// <param name="rows">The rows, one or more of the three kinds.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the lines or the rows are none, or hold a flag none of their kind has.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the table is erased.</exception>
        public void SetGridVisibility(bool visible, DxfGridLineType borders, DxfRowType rows) => SetTableLines(2, visible ? "1" : "0", borders, rows);

        #endregion

        #region Reading and writing the lines

        private DxfCellRange OneCell(int row, int column)
        {
            Layout layout = Read();
            layout.CellOf(row, column);
            return new DxfCellRange(row, column, row, column);
        }

        /// <summary>The cell's own override of an edge: its colour (0), lineweight (1) or showing (2).</summary>
        private string EdgeOverride(int row, int column, int edge, int kind)
        {
            int code = EdgeCodes[kind][edge];
            return Cell(row, column).Override(code, Layout.CellOverrides[code]);
        }

        /// <summary>The edge of a cell a line is, top, right, bottom or left: one of the cell's four alone.</summary>
        private static int EdgeOf(DxfGridLineType line, string parameter)
        {
            int edge = Array.IndexOf(EdgeOuter, line);

            if (edge < 0)
            {
                throw new ArgumentOutOfRangeException(parameter, line, "A line of a cell is one of its four, HorizontalTop, VerticalRight, HorizontalBottom or VerticalLeft; the lines inside are a block's.");
            }

            return edge;
        }

        private static int EdgeOf(DxfCellEdgeMasks edge, string parameter)
        {
            switch (edge)
            {
                case DxfCellEdgeMasks.TopMask:
                    return 0;
                case DxfCellEdgeMasks.RightMask:
                    return 1;
                case DxfCellEdgeMasks.BottomMask:
                    return 2;
                case DxfCellEdgeMasks.LeftMask:
                    return 3;
                default:
                    throw new ArgumentOutOfRangeException(parameter, edge, "An edge of a cell is one of TopMask, RightMask, BottomMask and LeftMask.");
            }
        }

        /// <summary>The place of a line among the six, as AutoCAD's GridLineType orders them: one line alone.</summary>
        private static int LineOf(DxfGridLineType line, string parameter)
        {
            for (int k = 0; k < 6; k++)
            {
                if ((int)line == 1 << k)
                {
                    return k;
                }
            }

            throw new ArgumentOutOfRangeException(parameter, line, "A line of the rows of a kind is one of the six of DxfGridLineType alone.");
        }

        /// <summary>The place of a kind of rows among the flags of the table: the title's 0, the header's 1, the data's 2.</summary>
        private static int RowOf(DxfRowType type, string parameter)
        {
            switch (type)
            {
                case DxfRowType.TitleRow:
                    return 0;
                case DxfRowType.HeaderRow:
                    return 1;
                case DxfRowType.DataRow:
                    return 2;
                default:
                    throw new ArgumentOutOfRangeException(parameter, type, "The rows are of one kind: TitleRow, HeaderRow or DataRow.");
            }
        }

        private static DxfLineWeight? Weight(string text)
            => int.TryParse(text?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int weight) ? (DxfLineWeight)weight : (DxfLineWeight?)null;

        private static bool? Shown(string text)
            => int.TryParse(text?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int shown) ? shown != 0 : (bool?)null;

        /// <summary>A colour as a group of the index holds it; a true colour and none refused.</summary>
        private static string IndexText(DxfColor color, string parameter)
        {
            if (color == null)
            {
                throw new ArgumentNullException(parameter);
            }

            switch (color.ColorMethod)
            {
                case DxfColorMethod.ByAci:
                case DxfColorMethod.ByLayer:
                case DxfColorMethod.ByBlock:
                case DxfColorMethod.Foreground:
                    return color.ColorIndex.ToString(CultureInfo.InvariantCulture);
                default:
                    throw new ArgumentException("A line's colour is written as a colour of AutoCAD's index: " + color + " is not one.", parameter);
            }
        }

        private static string WeightText(DxfLineWeight weight, string parameter)
        {
            int value = (int)weight;

            if (!(value >= -3 && value <= -1) && !DxfShape.IsLineweight(value))
            {
                throw new ArgumentOutOfRangeException(parameter, weight, "A line's lineweight is one of AutoCAD's widths, ByLayer, ByBlock or ByLineWeightDefault.");
            }

            return value.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>The texts of the properties a mask names, colour, lineweight and showing; null for one not named.</summary>
        private static string[] PropertyTexts(DxfGridPropertyParameter properties, string parameter)
        {
            if (properties == null)
            {
                throw new ArgumentNullException(parameter);
            }

            DxfGridProperties mask = properties.PropertyMask;

            if (((int)mask & ~0x3F) != 0)
            {
                throw new ArgumentOutOfRangeException(parameter, mask, "The mask is made of the flags of DxfGridProperties.");
            }

            if ((mask & (DxfGridProperties.LineStyle | DxfGridProperties.Linetype | DxfGridProperties.DoubleLineSpacing)) != 0)
            {
                throw new ArgumentException("A line's style, linetype and the spacing of a double line have no groups in a file of these versions that the DXF reference or ezdxf gives: the mask names " + mask + ".", parameter);
            }

            return new[]
            {
                (mask & DxfGridProperties.Color) != 0 ? IndexText(properties.Color, parameter) : null,
                (mask & DxfGridProperties.LineWeight) != 0 ? WeightText(properties.LineWeight, parameter) : null,
                (mask & DxfGridProperties.Visibility) != 0 ? (properties.Visible ? "1" : "0") : null,
            };
        }

        /// <summary>
        /// Writes the edges of the cells of a block that its lines name: each the cell's own override of each property given
        /// (its colour, lineweight, showing; null for one not given) with its flag, where it differs; every cell changed copied
        /// apart and the record written once.
        /// </summary>
        private void SetLines(DxfCellRange range, DxfGridLineType lines, string[] texts, string parameter, string rangeParameter)
        {
            if (lines == DxfGridLineType.InvalidGridLine || ((int)lines & ~(int)DxfGridLineType.AllGridLines) != 0)
            {
                throw new ArgumentOutOfRangeException(parameter, lines, "The lines are one or more of the six of DxfGridLineType.");
            }

            Layout layout = ReadToWrite();
            CheckRange(range, layout, rangeParameter);

            if ((long)range.BottomRow * layout.Columns + range.RightColumn >= layout.CellStart.Count)
            {
                throw new ArgumentOutOfRangeException(rangeParameter, range, "The table's record holds " + layout.CellStart.Count.ToString(CultureInfo.InvariantCulture) + " cells, fewer than its rows by its columns.");
            }

            int flagsCode = Item.File.Supports(1021) ? 91 : 177;
            var made = new List<KeyValuePair<int, string>>[layout.CellStart.Count];
            bool applies = false;
            bool any = false;

            for (int r = range.TopRow; r <= range.BottomRow; r++)
            {
                for (int c = range.LeftColumn; c <= range.RightColumn; c++)
                {
                    DxfGridLineType[] edges =
                    {
                        r == range.TopRow ? DxfGridLineType.HorizontalTop : DxfGridLineType.HorizontalInside,
                        c == range.RightColumn ? DxfGridLineType.VerticalRight : DxfGridLineType.VerticalInside,
                        r == range.BottomRow ? DxfGridLineType.HorizontalBottom : DxfGridLineType.HorizontalInside,
                        c == range.LeftColumn ? DxfGridLineType.VerticalLeft : DxfGridLineType.VerticalInside,
                    };

                    CellView cell = layout.CellAt(r, c);
                    CellView apart = null;

                    for (int edge = 0; edge < 4; edge++)
                    {
                        if ((lines & edges[edge]) == 0)
                        {
                            continue;
                        }

                        applies = true;

                        for (int kind = 0; kind < 3; kind++)
                        {
                            int code = EdgeCodes[kind][edge];

                            if (texts[kind] == null || SameWhole(cell.Override(code, Layout.CellOverrides[code]), texts[kind]))
                            {
                                continue;
                            }

                            apart = apart ?? View(Slice(cell));
                            apart.SetOverride(code, texts[kind], flagsCode);
                        }
                    }

                    if (apart != null)
                    {
                        made[r * layout.Columns + c] = apart.Groups.List;
                        any = true;
                    }
                }
            }

            if (!applies)
            {
                throw new ArgumentException("The lines " + lines + " name no line of the cells " + range + ": the lines inside are those between a block's rows and columns.", parameter);
            }

            if (any)
            {
                CheckEditable();
                Splice(layout, made);
            }
        }

        private static bool SameWhole(string now, string text)
            => int.TryParse(now?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int a) && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int b) && a == b;

        /// <summary>Writes the record with the cells given in the place of theirs, the others as they stand, in one writing.</summary>
        private void Splice(Layout layout, IList<List<KeyValuePair<int, string>>> cells)
        {
            DxfDimGroups g = layout.Groups;
            var list = new List<KeyValuePair<int, string>>(g.List.Count + 64);
            int first = layout.CellStart[0];

            for (int i = 0; i < first; i++)
            {
                list.Add(g.List[i]);
            }

            for (int k = 0; k < layout.CellStart.Count; k++)
            {
                if (cells[k] != null)
                {
                    list.AddRange(cells[k]);
                    continue;
                }

                int to = k + 1 < layout.CellStart.Count ? layout.CellStart[k + 1] : layout.CellsEnd;

                for (int i = layout.CellStart[k]; i < to; i++)
                {
                    list.Add(g.List[i]);
                }
            }

            for (int i = layout.CellsEnd; i < g.List.Count; i++)
            {
                list.Add(g.List[i]);
            }

            g.List.Clear();
            g.List.AddRange(list);
            g.Write(Order);
        }

        /// <summary>The text of the table's own group of a line of the rows of a kind: its colour (0), lineweight (1) or showing (2); null where it has none.</summary>
        private string TableLine(int kind, int line, int rowKind)
        {
            Layout layout = Read();
            int headTo = layout.CellStart.Count > 0 ? layout.CellStart[0] : layout.CellsEnd;
            int wanted = (kind + 1) * 100 + rowKind * 6 + line;
            int[] ranks = TailRanks(layout.Groups, headTo, layout.CellsEnd, out int to);

            for (int i = layout.CellsEnd; i < to; i++)
            {
                if (ranks[i - layout.CellsEnd] == wanted)
                {
                    return layout.Groups.List[i].Value;
                }
            }

            return null;
        }

        /// <summary>
        /// Writes the table's own groups of the lines and the kinds of rows given, each in place where it has one, else put in
        /// after its last cell in the order AutoCAD writes them, with its flag in 94 (colours), 95 (lineweights) or 96 (showing);
        /// the record written once, nothing where each has the value already.
        /// </summary>
        private void SetTableLines(int kind, string text, DxfGridLineType borders, DxfRowType rows)
        {
            if (borders == DxfGridLineType.InvalidGridLine || ((int)borders & ~(int)DxfGridLineType.AllGridLines) != 0)
            {
                throw new ArgumentOutOfRangeException(nameof(borders), borders, "The lines are one or more of the six of DxfGridLineType.");
            }

            if (rows == DxfRowType.UnknownRow || ((int)rows & ~7) != 0)
            {
                throw new ArgumentOutOfRangeException(nameof(rows), rows, "The rows are one or more of TitleRow, HeaderRow and DataRow.");
            }

            Layout layout = ReadToWrite();
            DxfDimGroups g = layout.Groups;
            int headTo = layout.CellStart.Count > 0 ? layout.CellStart[0] : layout.CellsEnd;
            int cellsEnd = layout.CellsEnd;
            DxfRowType[] kinds = { DxfRowType.TitleRow, DxfRowType.HeaderRow, DxfRowType.DataRow };

            for (int rowKind = 0; rowKind < 3; rowKind++)
            {
                for (int line = 0; line < 6; line++)
                {
                    if ((rows & kinds[rowKind]) == 0 || ((int)borders & (1 << line)) == 0)
                    {
                        continue;
                    }

                    int bit = rowKind * 6 + line;
                    int wanted = (kind + 1) * 100 + bit;
                    int code = LineCodes[kind][line];
                    int[] ranks = TailRanks(g, headTo, cellsEnd, out int to);
                    int at = Array.IndexOf(ranks, wanted);

                    if (at >= 0)
                    {
                        if (!SameWhole(g.List[cellsEnd + at].Value, text))
                        {
                            g.List[cellsEnd + at] = new KeyValuePair<int, string>(code, text);
                        }

                        continue;
                    }

                    g.Insert(PlaceOf(g, ranks, cellsEnd, to, code, wanted), new KeyValuePair<int, string>(code, text));
                    var head = new DxfDimGroups.Range(g.Sub.From, headTo);
                    int flagsCode = 94 + kind;
                    bool had = g.Find(flagsCode, head) >= 0;
                    g.Set(flagsCode, (g.Whole(flagsCode, head, 0) | (1 << bit)).ToString(CultureInfo.InvariantCulture), head, Order);

                    if (!had)
                    {
                        headTo++;
                        cellsEnd++;
                    }
                }
            }

            if (!g.Same())
            {
                CheckEditable();
                g.Write(Order);
            }
        }

        /// <summary>
        /// Where a new group of the table's own goes after its last cell: after the last of a lower rank in the order AutoCAD
        /// writes them, else before the first of a higher, else at the end of that part; never before a group of its code of a
        /// lower rank, nor after one of a higher, so that the groups of a code stay in the order of their flags.
        /// </summary>
        private static int PlaceOf(DxfDimGroups g, int[] ranks, int from, int to, int code, int wanted)
        {
            int lower = -1;
            int higher = -1;
            int sameLower = -1;
            int sameHigher = -1;

            for (int i = from; i < to; i++)
            {
                int rank = ranks[i - from];

                if (rank < 0)
                {
                    continue;
                }

                lower = rank < wanted ? i : lower;
                higher = rank > wanted && higher < 0 ? i : higher;
                sameLower = rank < wanted && g.List[i].Key == code ? i : sameLower;
                sameHigher = rank > wanted && g.List[i].Key == code && sameHigher < 0 ? i : sameHigher;
            }

            int at = lower >= 0 ? lower + 1 : higher >= 0 ? higher : to;
            at = sameHigher >= 0 ? Math.Min(at, sameHigher) : at;
            return sameLower >= 0 ? Math.Max(at, sameLower + 1) : at;
        }

        /// <summary>
        /// The rank of each of the table's own groups after its last cell in the order AutoCAD writes them, -1 for one of no
        /// rank: its title and header (280, 281), flow and margins (70, 40, 41) 0 to 4, its looks by row kind 5 to 22 by their
        /// flags in 93, then the colours, lineweights and showing of its lines 100, 200 and 300 on by their flags in 94, 95 and
        /// 96, the title's six lines, the header's, the data's; the groups of a code read in that order, one after another.
        /// </summary>
        private static int[] TailRanks(DxfDimGroups g, int headTo, int from, out int to)
        {
            to = from;

            while (to < g.List.Count && g.List[to].Key != 100)
            {
                to++;
            }

            var head = new DxfDimGroups.Range(g.Sub.From, headTo);
            int looks = g.Whole(93, head, 0);
            int[] lineFlags = { g.Whole(94, head, 0), g.Whole(95, head, 0), g.Whole(96, head, 0) };
            var ranks = new int[to - from];
            var seen = new Dictionary<int, int>();

            for (int i = from; i < to; i++)
            {
                int code = g.List[i].Key;
                seen.TryGetValue(code, out int n);
                seen[code] = n + 1;
                List<int> slots = Slots(code, looks, lineFlags);
                ranks[i - from] = n < slots.Count ? slots[n] : -1;
            }

            return ranks;
        }

        /// <summary>The ranks the groups of a code take after the table's last cell, one after another, by the flags of its head.</summary>
        private static List<int> Slots(int code, int looks, int[] lineFlags)
        {
            var slots = new List<int>();
            int single = Array.IndexOf(new[] { 280, 281, 70, 40, 41 }, code);

            if (single >= 0)
            {
                slots.Add(single);
                return slots;
            }

            if (RowLooks.TryGetValue(code, out int firstBit))
            {
                for (int bit = firstBit; bit < firstBit + 3; bit++)
                {
                    if ((looks & (1 << bit)) != 0)
                    {
                        slots.Add(bit);
                    }
                }
            }

            for (int kind = 0; kind < 3; kind++)
            {
                int line = Array.IndexOf(LineCodes[kind], code);

                for (int rowKind = 0; line >= 0 && rowKind < 3; rowKind++)
                {
                    int bit = rowKind * 6 + line;

                    if ((lineFlags[kind] & (1 << bit)) != 0)
                    {
                        slots.Add((kind + 1) * 100 + bit);
                    }
                }
            }

            return slots;
        }

        #endregion
    }
}
