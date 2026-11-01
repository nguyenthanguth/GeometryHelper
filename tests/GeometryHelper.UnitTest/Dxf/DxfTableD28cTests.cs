using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// What followed from AutoCAD 2021's answers to the D28c runs of 1 November 2026 (headless, hand-made files and the
    /// library's read, AutoCAD's own edits saved): a line between two cells is read from the cell below or right of it where
    /// that cell holds it; an override is taken away wherever it stands; a cell that takes the look of another takes its
    /// overrides, its lines and, in the TABLECONTENT, its format; a new cell of a column put in takes the table's own looks of
    /// its kind of row; the table's own overrides are read in its head where AutoCAD writes them; and the width of a column
    /// put in a table of one row. Each drawing is built in the form AutoCAD wrote, read as a file is.
    /// </summary>
    public class DxfTableD28cTests
    {
        private static string G(int code, string value) => code.ToString(CultureInfo.InvariantCulture).PadLeft(3) + "\n" + value + "\n";

        private static DxfTable Table(DxfDatabase db) => DxfAutoCadChecksD16Tests.Table(db);

        private static DxfTable Reread(DxfDatabase db) => Table(DxfDatabase.Parse(db.ToText()));

        private static string Record(string text, string handle) => DxfAutoCadChecksD16Tests.Record(text, handle);

        private static DxfColor Aci(short index) => DxfColor.FromColorIndex(DxfColorMethod.ByAci, index);

        #region The form AutoCAD 2021 wrote

        private const string Qty = "172\n0\n173\n0\n174\n0\n175\n0\n176\n0\n 91\n0\n178\n0\n145\n0.0\n 92\n0\n301\nCELL_VALUE\n 93\n6\n 90\n4\n  1\nQty\n 94\n0\n300\n\n302\nQty\n304\nACVALUE_END\n";

        private const string B12 = "172\n0\n173\n0\n174\n0\n175\n0\n176\n0\n 91\n0\n178\n0\n145\n0.0\n 92\n0\n301\nCELL_VALUE\n 93\n6\n 90\n4\n  1\nB-12\n 94\n0\n300\n\n302\nB-12\n304\nACVALUE_END\n";

        private const string Block = "171\n2\n172\n0\n173\n0\n174\n0\n175\n0\n176\n0\n 91\n0\n178\n0\n145\n0.0\n 92\n0\n340\n48\n144\n1.0\n179\n1\n331\n4C\n300\nM12\n";

        /// <summary>
        /// The table of the drawing given with lines of its own on the cells (1, 1) Qty, (2, 0) B-12 and (2, 1), the block: each
        /// its edge flags (172), its flags (91) and its groups, as AutoCAD 2021 writes them before its 92 (null for a cell left as it is).
        /// </summary>
        private static string Lines(string text, string qty = null, string b12 = null, string block = null)
        {
            string made = text;

            foreach (var cell in new[] { Tuple.Create(Qty, qty), Tuple.Create(B12, b12), Tuple.Create(Block, block) })
            {
                if (cell.Item2 == null)
                {
                    continue;
                }

                string[] parts = cell.Item2.Split('|');
                Assert.Contains(cell.Item1, made, StringComparison.Ordinal);
                made = made.Replace(cell.Item1, cell.Item1.Replace("172\n0\n", "172\n" + parts[0] + "\n").Replace(" 91\n0\n", " 91\n" + parts[1] + "\n").Replace(" 92\n0\n", parts[2] + " 92\n0\n"));
            }

            return made;
        }

        #endregion

        #region A line between two cells

        [Fact]
        public void ALineBetweenTwoCells_IsReadFromTheCellBelowOrRightOfIt_WhereThatCellHoldsIt()
        {
            // D28b G3 and D28c B11: AutoCAD 2021 read the top of (2, 1) red as the bottom of (1, 1) too, the bottom of (1, 1)
            // left as it was; the library read the bottom of (1, 1) as none.
            DxfTable below = Table(DxfDatabase.Parse(Lines(DxfDimensionDrawings.R2018, block: "1|64|" + G(69, "1"))));
            Assert.Equal(1, below.GetGridColor(1, 1, DxfGridLineType.HorizontalBottom).ColorIndex);
            Assert.Equal(1, below.GetGridColor(2, 1, DxfGridLineType.HorizontalTop).ColorIndex);
            Assert.Null(below.GetGridColor(1, 0, DxfGridLineType.HorizontalBottom));

            // D28c B1 and B2: the bottom of (1, 1) red and the top of (2, 1) green read green either side, as did the right of a
            // cell red and the left of the one right of it green.
            DxfTable both = Table(DxfDatabase.Parse(Lines(DxfDimensionDrawings.R2018, qty: "4|256|" + G(66, "1"), b12: "2|128|" + G(65, "1"), block: "9|576|" + G(69, "3") + G(68, "3"))));
            Assert.Equal(3, both.GetGridColor(1, 1, DxfGridLineType.HorizontalBottom).ColorIndex);
            Assert.Equal(3, both.GetGridColor(2, 1, DxfGridLineType.HorizontalTop).ColorIndex);
            Assert.Equal(3, both.GetGridColor(2, 0, DxfGridLineType.VerticalRight).ColorIndex);
            Assert.Equal(3, both.GridColor(2, 1, DxfCellEdgeMasks.LeftMask).ColorIndex);

            // D28c B12 and B15: the line is the edge of the one cell, each of its properties: the top of (2, 1) of a colour alone
            // and the bottom of (1, 1) of a lineweight alone read the colour and no lineweight.
            DxfTable edge = Table(DxfDatabase.Parse(Lines(DxfDimensionDrawings.R2018, qty: "4|4096|" + G(276, "50"), block: "1|64|" + G(69, "3"))));
            Assert.Equal(3, edge.GetGridColor(1, 1, DxfGridLineType.HorizontalBottom).ColorIndex);
            Assert.Null(edge.GetGridLineWeight(1, 1, DxfGridLineType.HorizontalBottom));
            Assert.Equal(DxfGridProperties.Color, edge.GetGridProperty(1, 1, DxfGridLineType.HorizontalBottom).PropertyMask);

            // D28c B13 and B14: an edge flag with no override, or an override with no edge flag, holds no line.
            DxfTable flagOnly = Table(DxfDatabase.Parse(Lines(DxfDimensionDrawings.R2018, qty: "4|256|" + G(66, "1"), block: "1|0|")));
            Assert.Equal(1, flagOnly.GetGridColor(2, 1, DxfGridLineType.HorizontalTop).ColorIndex);
            DxfTable groupOnly = Table(DxfDatabase.Parse(Lines(DxfDimensionDrawings.R2018, qty: "4|256|" + G(66, "1"), block: "0|64|" + G(69, "3"))));
            Assert.Equal(1, groupOnly.GetGridColor(2, 1, DxfGridLineType.HorizontalTop).ColorIndex);
            Assert.Equal(1, groupOnly.GetGridColor(1, 1, DxfGridLineType.HorizontalBottom).ColorIndex);
        }

        [Fact]
        public void ALineSetOnACell_IsSetOnTheCellThatHoldsIt_SoThatItReadsAsSet()
        {
            // The bottom of (1, 1) set red where the top of (2, 1) holds green: AutoCAD reads the line from (2, 1), and its own
            // SetGridColor writes the line there (D28b x4), so it is set there too.
            DxfDatabase db = DxfDatabase.Parse(Lines(DxfDimensionDrawings.R2018, block: "1|64|" + G(69, "3")));
            Table(db).SetGridColor(1, 1, DxfGridLineType.HorizontalBottom, Aci(1));
            DxfTable read = Reread(db);
            Assert.Equal(1, read.GetGridColor(1, 1, DxfGridLineType.HorizontalBottom).ColorIndex);
            Assert.Equal(1, read.GetGridColor(2, 1, DxfGridLineType.HorizontalTop).ColorIndex);

            // The top of (2, 1) given a lineweight where the bottom of (1, 1) holds the line red keeps it red.
            DxfDatabase kept = DxfDatabase.Parse(Lines(DxfDimensionDrawings.R2018, qty: "4|256|" + G(66, "1")));
            Table(kept).SetGridLineWeight(2, 1, DxfGridLineType.HorizontalTop, DxfLineWeight.LineWeight050);
            DxfTable again = Reread(kept);
            Assert.Equal(1, again.GetGridColor(2, 1, DxfGridLineType.HorizontalTop).ColorIndex);
            Assert.Equal(DxfLineWeight.LineWeight050, again.GetGridLineWeight(1, 1, DxfGridLineType.HorizontalBottom));
        }

        #endregion

        #region Overrides taken away wherever they stand

        /// <summary>The cell (1, 0) Mark of the R2018 drawing with its overrides before its 92, where AutoCAD 2021 writes them.</summary>
        private static string MarkAsAutoCadWritesIt()
        {
            const string Mark = "145\n0.0\n 92\n0\n301\nCELL_VALUE\n 93\n6\n 90\n4\n  1\nMark\n";
            return DxfDimensionDrawings.R2018
                .Replace("302\nMark\n304\nACVALUE_END\n  7\nGH_TXT\n140\n3.5\n170\n5\n", "302\nMark\n304\nACVALUE_END\n")
                .Replace(Mark, "145\n0.0\n170\n5\n  7\nGH_TXT\n140\n3.5\n 92\n0\n301\nCELL_VALUE\n 93\n6\n 90\n4\n  1\nMark\n");
        }

        [Fact]
        public void AllOverridesTakenAway_AreTakenOutBeforeTheValueToo_AndTheEdgesOfTheLines()
        {
            // D28c x2: AutoCAD 2021's RemoveAllOverrides(1, 0) of its cell of 170 5, 7 GH_TXT and 140 3.5 before its 92 took them
            // out, 91 262193 to 262144; the library cleared the flags and left the groups.
            DxfDatabase db = DxfDatabase.Parse(MarkAsAutoCadWritesIt());
            Table(db).RemoveAllOverrides(1, 0);
            string record = Record(db.ToText(), "F6");
            Assert.DoesNotContain("GH_TXT", record, StringComparison.Ordinal);
            Assert.DoesNotContain("140\n3.5\n", record, StringComparison.Ordinal);
            Assert.Null(Reread(db).TextHeight(1, 0));

            // D28c x1 and x3: the cell's edge flags (172) go with its lines, 9 to 0.
            DxfDatabase lines = DxfDatabase.Parse(Lines(DxfDimensionDrawings.R2018, block: "9|576|" + G(69, "1") + G(68, "1")));
            Table(lines).RemoveAllOverrides(2, 1);
            Assert.Contains("171\n2\n172\n0\n", Record(lines.ToText(), "F6"), StringComparison.Ordinal);
            Assert.Null(Reread(lines).GetGridColor(2, 1, DxfGridLineType.HorizontalTop));

            // And so for every cell of the table.
            DxfDatabase all = DxfDatabase.Parse(MarkAsAutoCadWritesIt());
            Table(all).ClearTableStyleOverrides(2);
            Assert.DoesNotContain("GH_TXT", Record(all.ToText(), "F6"), StringComparison.Ordinal);
        }

        #endregion

        #region A cell that takes the look of another

        [Fact]
        public void ARowThatTakesTheLookOfAnother_TakesItsOverridesBeforeItsValue_AndItsLines()
        {
            // D28 b06 and D28c i2: AutoCAD 2021's InsertRowsAndInherit(2, 1, 1) gave the new (2, 0) the 170 5, 7 GH_TXT and 140 3.5
            // its (1, 0) held before its 92; the library took those after a value alone.
            DxfDatabase db = DxfDatabase.Parse(MarkAsAutoCadWritesIt());
            Table(db).InsertRowsAndInherit(2, 1, 1);
            DxfTable read = Reread(db);
            Assert.Equal(3.5, read.TextHeight(2, 0));
            Assert.Equal("GH_TXT", read.TextStyle(2, 0).Name);

            // D28c i1: the new (3, 1) of InsertRowsAndInherit(3, 2, 1) took 172 9 with the top and left of (2, 1).
            DxfDatabase lines = DxfDatabase.Parse(Lines(DxfDimensionDrawings.R2018, block: "9|576|" + G(69, "1") + G(68, "1")));
            Table(lines).InsertRowsAndInherit(3, 2, 1);
            string record = Record(lines.ToText(), "F6");
            Assert.Equal(2, record.Split(new[] { "172\n9\n" }, StringSplitOptions.None).Length - 1);
            DxfTable again = Reread(lines);
            Assert.Equal(1, again.GetGridColor(3, 1, DxfGridLineType.VerticalLeft).ColorIndex);
            Assert.Equal(1, again.GetGridColor(2, 1, DxfGridLineType.HorizontalBottom).ColorIndex);

            // D28c i4: a cell of a value of no kind (93 7, 90 0) gives the empty value of the kind 512.
            const string Data = " 93\n6\n 90\n4\n  1\nB-12\n 94\n0\n300\n\n302\nB-12\n";
            DxfDatabase none = DxfDatabase.Parse(DxfDimensionDrawings.R2018.Replace(Data, " 93\n7\n 90\n0\n 94\n0\n300\n\n302\n\n"));
            Table(none).InsertRowsAndInherit(3, 2, 1);
            Assert.Contains("301\nCELL_VALUE\n 93\n5\n 90\n512\n", Record(none.ToText(), "F6"), StringComparison.Ordinal);
        }

        /// <summary>The cells of a TABLECONTENT, each from its 300 CELL to its TABLECELL_END.</summary>
        private static List<string> ContentCells(string record)
        {
            var cells = new List<string>();

            for (int at = record.IndexOf("300\nCELL\n", StringComparison.Ordinal); at >= 0; at = record.IndexOf("300\nCELL\n", at + 1, StringComparison.Ordinal))
            {
                int end = record.IndexOf("309\nTABLECELL_END\n", at, StringComparison.Ordinal);
                cells.Add(record.Substring(at, end - at));
            }

            return cells;
        }

        private static string Format(string cell)
        {
            int from = cell.IndexOf("FORMATTEDTABLEDATACELL_BEGIN", StringComparison.Ordinal);
            return cell.Substring(from, cell.IndexOf("FORMATTEDTABLEDATACELL_END", StringComparison.Ordinal) - from);
        }

        [Fact]
        public void ARowThatTakesTheLookOfAnother_IsWrittenInTheTableContentWithItsFormat_AndAnEmptyValue()
        {
            // D28 b03 and D28c i1 to i7: AutoCAD 2021 wrote each TABLECONTENT cell of InsertRowsAndInherit an empty value of the
            // flags 5 with the format of the content of the cell it took the look of, and that cell's format, its margins and
            // lines; the library wrote cells of no content and no format.
            // Each cell of no link given a text and margins of 3, as AutoCAD writes a cell's content and margins.
            string margins = G(171, "1") + G(301, "MARGIN") + G(1, "CELLMARGIN_BEGIN") + string.Concat(Enumerable.Repeat(G(40, "3.0"), 4)) + G(40, "0.18") + G(40, "0.18") + G(309, "CELLMARGIN_END") + G(94, "0");
            string content = G(95, "1") + G(302, "CONTENT") + G(1, "CELLCONTENT_BEGIN") + G(90, "1") + G(300, "VALUE") + G(93, "6") + G(90, "4") + G(1, "B-12")
                + G(94, "0") + G(300, string.Empty) + G(302, "B-12") + G(304, "ACVALUE_END") + G(91, "0") + G(309, "CELLCONTENT_END") + G(1, "FORMATTEDCELLCONTENT_BEGIN")
                + G(170, "1") + G(300, "CONTENTFORMAT") + G(1, "CONTENTFORMAT_BEGIN") + G(90, "3") + G(91, "0") + G(92, "4") + G(93, "0") + G(300, string.Empty)
                + G(40, "0.0") + G(140, "1.0") + G(94, "1") + G(62, "0") + G(340, "0") + G(144, "0.18") + G(309, "CONTENTFORMAT_END") + G(309, "FORMATTEDCELLCONTENT_END");
            string text = DxfAutoCadChecksD16Tests.LinkedByAutoCad(DxfDimensionDrawings.R2018, 3)
                .Replace(G(171, "0") + G(94, "0") + G(309, "TABLEFORMAT_END"), margins + G(309, "TABLEFORMAT_END"))
                .Replace(G(92, "0") + G(95, "0") + G(309, "LINKEDTABLEDATACELL_END"), G(92, "0") + content + G(309, "LINKEDTABLEDATACELL_END"));
            List<string> before = ContentCells(Record(text, "B03"));
            Assert.Contains("CELLMARGIN_BEGIN", before[4], StringComparison.Ordinal);
            Assert.Contains("FORMATTEDCELLCONTENT_BEGIN", before[5], StringComparison.Ordinal);
            DxfDatabase db = DxfDatabase.Parse(text);

            Table(db).InsertRowsAndInherit(3, 2, 1);

            List<string> after = ContentCells(Record(db.ToText(), "B03"));
            Assert.Equal(8, after.Count);

            for (int c = 0; c < 2; c++)
            {
                string made = after[6 + c];
                Assert.Equal(Format(before[4 + c]), Format(made));
                Assert.Contains(G(140, "0.0") + G(94, "0") + G(300, string.Empty) + G(302, string.Empty) + G(304, "ACVALUE_END") + G(309, "DATAMAP_END") + G(92, "0"), made, StringComparison.Ordinal);
                Assert.Contains(G(95, "1") + G(302, "CONTENT") + G(1, "CELLCONTENT_BEGIN") + G(90, "1") + G(300, "VALUE") + G(93, "5") + G(90, "4") + G(94, "0") + G(300, string.Empty)
                    + G(302, string.Empty) + G(304, "ACVALUE_END") + G(91, "0") + G(309, "CELLCONTENT_END") + G(1, "FORMATTEDCELLCONTENT_BEGIN") + G(170, "1"), made, StringComparison.Ordinal);
                Assert.DoesNotContain("B-12", made, StringComparison.Ordinal);
            }

            Assert.Equal("B-12", Reread(db).TextString(2, 0));
        }

        #endregion

        #region New cells of a column put in

        /// <summary>
        /// The R2018 drawing whose table holds, in its head where AutoCAD 2021 wrote them (D28c o0), its own header and data of
        /// 9, its data GH_TXT, red and top left, and its header's background green.
        /// </summary>
        private static string RowLooks()
        {
            const string Head = " 93\n0\n 94\n0\n 95\n0\n 96\n0\n141\n10.0\n";
            Assert.Contains(Head, DxfDimensionDrawings.R2018, StringComparison.Ordinal);
            return DxfDimensionDrawings.R2018.Replace(Head, " 93\n6886016\n 94\n0\n 95\n0\n 96\n0\n  7\nGH_TXT\n140\n9.0\n170\n1\n 63\n3\n 64\n1\n140\n9.0\n283\n0\n141\n10.0\n");
        }

        [Fact]
        public void ANewCellOfAColumnPutIn_TakesTheTablesOwnLooksOfItsKindOfRow_AndOneOfARowPutInNone()
        {
            // D28c o1, o3 and o4: AutoCAD 2021 gave each new cell of a column InsertColumns or SetSize put in the table's own looks
            // of its row's kind as its own (the header 63 3, 283 0, 140 9.0; the data 170 1, 7 GH_TXT, 64 1, 140 9.0), and the
            // cells of a row put in none, which it read by the table style alone (4.5, Standard, TopCenter).
            foreach (Action<DxfTable> put in new Action<DxfTable>[] { t => t.InsertColumns(2, 20.0, 1), t => t.SetSize(4, 3) })
            {
                DxfDatabase db = DxfDatabase.Parse(RowLooks());
                put(Table(db));
                DxfTable read = Reread(db);

                Assert.Equal(9.0, read.TextHeight(1, 2));
                Assert.Equal(3, read.BackgroundColor(1, 2).ColorIndex);
                Assert.Null(read.Alignment(1, 2));
                Assert.Equal(9.0, read.TextHeight(2, 2));
                Assert.Equal("GH_TXT", read.TextStyle(2, 2).Name);
                Assert.Equal(1, read.ContentColor(2, 2).ColorIndex);
                Assert.Equal(DxfCellAlignment.TopLeft, read.Alignment(2, 2));
                Assert.Null(read.TextHeight(0, 2));
            }

            DxfDatabase rows = DxfDatabase.Parse(RowLooks());
            Table(rows).SetSize(4, 3);
            Assert.Null(Reread(rows).TextHeight(3, 0));
            Assert.Null(Reread(rows).TextHeight(3, 2));
            Table(rows).InsertRows(3, 8.0, 1);
            Assert.Null(Reread(rows).TextHeight(3, 1));
        }

        #endregion

        #region The table's own overrides in its head

        [Fact]
        public void TheTablesOwnOverridesInItsHead_AreReadAsAutoCadWritesThem()
        {
            // D28b s3 and s12, D28c i3: AutoCAD 2021 wrote the table's own margins (93 8, 40 3.0; 93 16, 41 3.0) and its header
            // left out (93 2, 281 1) in its head after its flags, before its heights; the library read them after its last cell
            // alone.
            const string Head = " 93\n0\n 94\n0\n 95\n0\n 96\n0\n141\n10.0\n";
            DxfDatabase db = DxfDatabase.Parse(DxfDimensionDrawings.R2018.Replace(Head, " 93\n26\n 94\n0\n 95\n0\n 96\n0\n281\n1\n 40\n3.0\n 41\n2.0\n141\n10.0\n"));
            DxfTable table = Table(db);
            Assert.Equal(3.0, table.HorizontalCellMargin);
            Assert.Equal(2.0, table.VerticalCellMargin);
            Assert.True(table.IsHeaderSuppressed);
            Assert.Equal(DxfRowType.DataRow, table.RowType(1));

            table.ClearTableStyleOverrides(1);
            DxfTable cleared = Reread(db);
            Assert.Equal(0.06, cleared.HorizontalCellMargin);
            Assert.False(cleared.IsHeaderSuppressed);
            Assert.Contains(" 93\n0\n 94\n0\n 95\n0\n 96\n0\n141\n10.0\n", Record(db.ToText(), "F6"), StringComparison.Ordinal);

            // The table's own text height of its data there is scaled with it, and given the cells of none of their own.
            DxfDatabase scaled = DxfDatabase.Parse(RowLooks());
            Table(scaled).TransformBy(GeoTransform3.Scaling(2.0));
            DxfTable big = Reread(scaled);
            Assert.Equal(18.0, big.TextHeight(2, 0));
            Assert.Equal(18.0, big.TextHeight(1, 1));
        }

        #endregion

        #region The width of a column put in, and merges of rows

        [Fact]
        public void AColumnPutInATableOfOneRowUnderItsTitle_IsOneWide_AndMergesOfRowsGrowAsAutoCadGrowsThem()
        {
            // D28c w1: AutoCAD 2021's SetSize(1, 3) of its table of one row, its title merged across it, made the column 1 wide
            // and grew the title over it; the library made it 9, the title's 6 and its margins.
            DxfDatabase db = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
            DxfTable table = Table(db);
            table.DeleteRows(1, 2);
            table.SetSize(1, 3);
            DxfTable read = Reread(db);
            Assert.Equal(1.0, read.ColumnWidth(2));
            Assert.Equal(new DxfCellRange(0, 0, 0, 2), read.GetMergeRange(0, 2));

            // D28c w2, w3: a title merged across some of the columns does not grow, and the column is as wide as the title's text
            // height and its margins, 9.
            DxfDatabase some = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
            DxfTable part = Table(some);
            part.InsertColumns(2, 20.0, 1);
            part.UnmergeCells(new DxfCellRange(0, 0, 0, 2));
            part.MergeCells(new DxfCellRange(0, 1, 0, 2));
            part.SetSize(3, 4);
            DxfTable partRead = Reread(some);
            Assert.Equal(new DxfCellRange(0, 1, 0, 2), partRead.GetMergeRange(0, 1));
            Assert.Equal(9.0, partRead.ColumnWidth(3));

            // D28c m1 to m3, m5 to m7: a merge down to the last row grows by rows put in among it, not by those put in after it.
            foreach (var put in new[]
            {
                Tuple.Create<Action<DxfTable>, DxfCellRange>(t => t.SetSize(4, 2), new DxfCellRange(1, 1, 2, 1)),
                Tuple.Create<Action<DxfTable>, DxfCellRange>(t => t.InsertRows(3, 8.0, 1), new DxfCellRange(1, 1, 2, 1)),
                Tuple.Create<Action<DxfTable>, DxfCellRange>(t => t.InsertRows(2, 8.0, 1), new DxfCellRange(1, 1, 3, 1)),
            })
            {
                DxfDatabase merged = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
                Table(merged).MergeCells(new DxfCellRange(1, 1, 2, 1));
                put.Item1(Table(merged));
                Assert.Equal(put.Item2, Reread(merged).GetMergeRange(1, 1));
            }
        }

        #endregion
    }
}
