using System;
using System.Globalization;
using System.Linq;
using System.Text;
using GeometryHelper.Dxf;
using Xunit;
using static GeometryHelper.UnitTest.Dxf.DxfModel;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// What followed from AutoCAD 2021's answers to the D28b runs of 1 November 2026 (headless, hand-made files and the
    /// library's read, AutoCAD's own edits saved): a cell of a linked range is linked where AutoCAD lays it out from the
    /// ACAD_TABLE; merges are read by the spans of their top left cells; an override of a cell is written where it stands; a
    /// line of a cell is read by its edge flag (172); links are read only for a table ACDB_RECOMPOSE_DATA lists; and the small
    /// forms of the rows, columns and cells AutoCAD writes. Each drawing is built in the form AutoCAD wrote, read as a file is.
    /// </summary>
    public class DxfTableD28bTests
    {
        private static string G(int code, string value) => code.ToString(CultureInfo.InvariantCulture).PadLeft(3) + "\n" + value + "\n";

        private static DxfTable Table(DxfDatabase db) => DxfAutoCadChecksD16Tests.Table(db);

        private static string Record(string text, string handle) => DxfAutoCadChecksD16Tests.Record(text, handle);

        private static string Linked(bool formula = false) => DxfAutoCadChecksD16Tests.LinkedByAutoCad(DxfDimensionDrawings.R2018, 3, formula);

        private static DxfDataLink Link(DxfDatabase db) => (DxfDataLink)db.DataLinkDictionary["GH_PARTS"];

        #region The form AutoCAD 2021 wrote

        /// <summary>A content of a TABLECONTENT cell as AutoCAD 2021 wrote one: a value of the flags (93) and kind (90) given, its data and text.</summary>
        private static string Value(int flags, int kind, string data, string text)
            => G(302, "CONTENT") + G(1, "CELLCONTENT_BEGIN") + G(90, "1") + G(300, "VALUE") + G(93, flags.ToString(CultureInfo.InvariantCulture))
                + G(90, kind.ToString(CultureInfo.InvariantCulture)) + data + G(94, "0") + G(300, string.Empty) + G(302, text) + G(304, "ACVALUE_END") + G(91, "0")
                + G(309, "CELLCONTENT_END") + G(1, "FORMATTEDCELLCONTENT_BEGIN") + G(170, "1") + G(300, "CONTENTFORMAT") + G(1, "CONTENTFORMAT_BEGIN")
                + G(90, "0") + G(91, "0") + G(92, "0") + G(93, "0") + G(300, string.Empty) + G(40, "0.0") + G(140, "1.0") + G(94, "1") + G(62, "0")
                + G(340, "0") + G(144, "0.18") + G(309, "CONTENTFORMAT_END") + G(309, "FORMATTEDCELLCONTENT_END");

        /// <summary>The empty value AutoCAD 2021 gives a cell of no text (93 7, 90 0).</summary>
        private static readonly string EmptyValue = Value(7, 0, string.Empty, string.Empty);

        /// <summary>
        /// A cell of a TABLECONTENT as AutoCAD 2021 wrote one: the state given, its checksum given (none for null, its custom data
        /// then of no entry), a 92 of 0 or the run of a link, the content given or none, and its format of nought.
        /// </summary>
        private static string Cell(int state = 0, string checksum = "0.0", string link = null, string content = null)
        {
            var text = new StringBuilder();
            text.Append(G(300, "CELL")).Append(G(1, "LINKEDTABLEDATACELL_BEGIN")).Append(G(90, state.ToString(CultureInfo.InvariantCulture)))
                .Append(G(300, string.Empty)).Append(G(91, "0")).Append(G(301, "CUSTOMDATA")).Append(G(1, "DATAMAP_BEGIN"));
            text.Append(checksum == null ? G(90, "0") : G(90, "1") + G(300, "ACAD_ROUNDTRIP_2008_CELL_CHECKSUM") + G(301, "DATAMAP_VALUE") + G(93, "2") + G(90, "2")
                + G(140, checksum) + G(94, "0") + G(300, string.Empty) + G(302, string.Empty) + G(304, "ACVALUE_END"));
            text.Append(G(309, "DATAMAP_END")).Append(link == null ? G(92, "0") : G(92, "1") + link);
            text.Append(content == null ? G(95, "0") : G(95, "1") + content);
            text.Append(G(309, "LINKEDTABLEDATACELL_END")).Append(G(1, "FORMATTEDTABLEDATACELL_BEGIN")).Append(G(300, "CELLTABLEFORMAT"))
                .Append(G(1, "TABLEFORMAT_BEGIN")).Append(G(90, "1")).Append(G(170, "1")).Append(G(91, "0")).Append(G(92, "0")).Append(G(62, "257"))
                .Append(G(93, "1")).Append(G(300, "CONTENTFORMAT")).Append(G(1, "CONTENTFORMAT_BEGIN")).Append(G(90, "0")).Append(G(91, "0"))
                .Append(G(92, "512")).Append(G(93, "0")).Append(G(300, string.Empty)).Append(G(40, "0.0")).Append(G(140, "1.0")).Append(G(94, "1"))
                .Append(G(62, "0")).Append(G(340, "0")).Append(G(144, "0.18")).Append(G(309, "CONTENTFORMAT_END")).Append(G(171, "0")).Append(G(94, "0"))
                .Append(G(309, "TABLEFORMAT_END")).Append(G(309, "FORMATTEDTABLEDATACELL_END")).Append(G(1, "TABLECELL_BEGIN")).Append(G(90, "0"))
                .Append(G(91, "0")).Append(G(309, "TABLECELL_END"));
            return text.ToString();
        }

        /// <summary>A row of a TABLECONTENT as AutoCAD 2021 wrote one: the cells given, then its style (1 title, 2 header, 3 data) and height.</summary>
        private static string Row(int style, string height, params string[] cells)
        {
            var text = new StringBuilder();
            text.Append(G(301, "ROW")).Append(G(1, "LINKEDTABLEDATAROW_BEGIN")).Append(G(90, cells.Length.ToString(CultureInfo.InvariantCulture)));
            cells.ToList().ForEach(c => text.Append(c));
            text.Append(G(91, "0")).Append(G(301, "CUSTOMDATA")).Append(G(1, "DATAMAP_BEGIN")).Append(G(90, "0")).Append(G(309, "DATAMAP_END"))
                .Append(G(309, "LINKEDTABLEDATAROW_END")).Append(G(1, "FORMATTEDTABLEDATAROW_BEGIN")).Append(G(300, "ROWTABLEFORMAT"))
                .Append(G(1, "TABLEFORMAT_BEGIN")).Append(G(90, "2")).Append(G(170, "0")).Append(G(309, "TABLEFORMAT_END"))
                .Append(G(309, "FORMATTEDTABLEDATAROW_END")).Append(G(1, "TABLEROW_BEGIN")).Append(G(90, style.ToString(CultureInfo.InvariantCulture)))
                .Append(G(40, height)).Append(G(309, "TABLEROW_END"));
            return text.ToString();
        }

        /// <summary>A column of a TABLECONTENT as AutoCAD 2021 wrote one: its style 0 and width.</summary>
        private static string Column(string width)
            => G(300, "COLUMN") + G(1, "LINKEDTABLEDATACOLUMN_BEGIN") + G(300, string.Empty) + G(91, "0") + G(301, "CUSTOMDATA") + G(1, "DATAMAP_BEGIN")
                + G(90, "0") + G(309, "DATAMAP_END") + G(309, "LINKEDTABLEDATACOLUMN_END") + G(1, "FORMATTEDTABLEDATACOLUMN_BEGIN") + G(300, "COLUMNTABLEFORMAT")
                + G(1, "TABLEFORMAT_BEGIN") + G(90, "3") + G(170, "0") + G(309, "TABLEFORMAT_END") + G(309, "FORMATTEDTABLEDATACOLUMN_END")
                + G(1, "TABLECOLUMN_BEGIN") + G(90, "0") + G(40, width) + G(309, "TABLECOLUMN_END");

        /// <summary>
        /// The drawing of AutoCAD's linked table F6 (<see cref="DxfAutoCadChecksD16Tests.LinkedByAutoCad"/>) with its TABLECONTENT
        /// B03 of the cells given, row by row, the link B06 named by (1, 0) over the rows and columns given.
        /// </summary>
        private static string Ranged(int rows, int columns, params string[] cells)
        {
            string text = Linked();
            string record = Record(text, "B03");
            string marker = G(100, "AcDbLinkedTableData");
            string head = record.Substring(0, record.IndexOf(marker, StringComparison.Ordinal) + marker.Length);
            string run = G(340, "B06") + G(93, rows.ToString(CultureInfo.InvariantCulture)) + G(94, columns.ToString(CultureInfo.InvariantCulture)) + G(96, "1");
            var made = new StringBuilder(head);
            made.Append(G(90, "2")).Append(Column("40.0")).Append(Column("30.0")).Append(G(91, "3"));
            made.Append(Row(1, "10.0", cells[0], cells[1])).Append(Row(2, "8.0", cells[2].Replace("{link}", run), cells[3])).Append(Row(3, "8.0", cells[4], cells[5]));
            made.Append(G(92, "0")).Append(G(100, "AcDbFormattedTableData")).Append(G(300, "TABLEFORMAT")).Append(G(1, "TABLEFORMAT_BEGIN"))
                .Append(G(90, "4")).Append(G(170, "0")).Append(G(309, "TABLEFORMAT_END")).Append(G(90, "1")).Append(G(91, "0")).Append(G(92, "0"))
                .Append(G(93, "0")).Append(G(94, "1")).Append(G(100, "AcDbTableContent")).Append(G(340, "126"));
            return text.Replace(record, made.ToString());
        }

        /// <summary>The cell (1, 0) of <see cref="Ranged"/>, of the state 4, naming the link, its checksum Mark's.</summary>
        private static string Anchor(string content = null) => Cell(4, "1041.0", "{link}", content);

        #endregion

        #region A cell of a linked range

        [Fact]
        public void ACellOfALinkedRangeOfNoContent_IsLinked_AsAutoCadLaysItOutFromTheTable()
        {
            // D28 p2 and s3: AutoCAD 2021 read every cell of the library's link over (1, 0) to (2, 1), whose TABLECONTENT cells
            // held no content, as linked, the text Qty given to (1, 1) in the ACAD_TABLE alone too; the library read (1, 0) alone.
            DxfTable table = Table(DxfDatabase.Parse(Ranged(2, 2, Cell(0, "1655.0"), Cell(), Anchor(), Cell(0, "676.0"), Cell(0, "503.0"), Cell())));

            Assert.True(table.IsLinked(1, 0));
            Assert.True(table.IsLinked(1, 1));
            Assert.True(table.IsLinked(2, 0));
            Assert.True(table.IsLinked(2, 1));
            Assert.False(table.IsLinked(0, 0));
        }

        [Fact]
        public void ACellOfALinkedRangeHoldingAContentItsChecksumAgreesWith_IsNotLinked_AndOneItDisagreesWithIs()
        {
            // D28 s1, s4, L1 to L14: a TABLECONTENT cell of the range holding a content AutoCAD 2021 read as not linked where the
            // checksum of its custom data agreed with the cell's text in the ACAD_TABLE, or it had none (L9: Qty and 676; L12),
            // whatever the content (an empty value, a text, a number); and as linked where it disagreed (L10, L11, L14), as where
            // the cell held none (s3) or its state had the flag 4 (73-k).
            string qty = Value(6, 4, G(1, "Qty"), "Qty");
            DxfTable agreeing = Table(DxfDatabase.Parse(Ranged(2, 2, Cell(0, "1655.0"), Cell(), Anchor(), Cell(0, "676.0", null, qty), Cell(0, "503.0", null, EmptyValue), Cell(0, null, null, EmptyValue))));

            Assert.True(agreeing.IsLinked(1, 0));
            Assert.False(agreeing.IsLinked(1, 1));
            Assert.False(agreeing.IsLinked(2, 0));
            Assert.False(agreeing.IsLinked(2, 1));
            Assert.Equal(new DxfCellRange(1, 0, 2, 1), agreeing.GetDataLinkRange(1, 0));

            DxfTable disagreeing = Table(DxfDatabase.Parse(Ranged(2, 2, Cell(0, "1655.0"), Cell(), Anchor(EmptyValue), Cell(0, "0.0", null, EmptyValue), Cell(0, "1.0", null, Value(6, 2, G(140, "5.0"), "5")), Cell(4, "0.0", null, EmptyValue))));

            Assert.True(disagreeing.IsLinked(1, 0));
            Assert.True(disagreeing.IsLinked(1, 1));
            Assert.True(disagreeing.IsLinked(2, 0));
            Assert.True(disagreeing.IsLinked(2, 1));
        }

        [Fact]
        public void ACellOfTheStateOfALinkOutsideEveryRange_IsLinkedWhereItHoldsAContent_WhateverItsChecksum()
        {
            // D28 L13: AutoCAD 2021 read the title Parts of the state 4 beside a range over (1, 0) to (1, 1) as linked, its checksum
            // made 1, as it had with its own (73-r).
            DxfTable table = Table(DxfDatabase.Parse(Ranged(1, 2, Cell(4, "1.0", null, Value(6, 4, G(1, "Parts"), "Parts")), Cell(), Anchor(EmptyValue), Cell(0, "676.0", null, EmptyValue), Cell(), Cell())));

            Assert.True(table.IsLinked(0, 0));
            Assert.False(table.IsLinked(1, 1));
            Assert.True(table.IsLinked(1, 0));
            Assert.Null(table.GetDataLinkRange(0, 0));
        }

        #endregion

        #region Merges read by their spans

        /// <summary>The drawing of R2018 with its title merged as AutoCAD writes a merge: its top left cell 173 0 and 175 2, the other 173 1 and 175 and 176 of 1.</summary>
        private static string MergedByAutoCad()
            => DxfDimensionDrawings.R2018
                .Replace("171\n1\n172\n0\n173\n1\n174\n0\n175\n2\n176\n1\n", "171\n1\n172\n0\n173\n0\n174\n0\n175\n2\n176\n1\n")
                .Replace("171\n1\n172\n0\n173\n1\n174\n0\n175\n0\n176\n0\n", "171\n1\n172\n0\n173\n1\n174\n0\n175\n1\n176\n1\n");

        [Fact]
        public void AMergeAutoCadWrote_IsReadByTheSpansOfItsTopLeftCell()
        {
            // D28 a07, g2 and g3: AutoCAD 2021 writes a merge's top left cell 173 0 and its spans in 175 and 176, and reads a
            // merge from the spans alone (g2), not from a 173 of 1 in a cell of no spans (g3).
            string text = MergedByAutoCad();
            DxfTable table = Table(DxfDatabase.Parse(text));

            Assert.Equal(new DxfCellRange(0, 0, 0, 1), table.GetMergeRange(0, 1));
            Assert.True(table.IsMergedCell(0, 0, out DxfCellRange range));
            Assert.Equal(new DxfCellRange(0, 0, 0, 1), range);

            // A 173 of 1 in a cell of no spans merges nothing.
            DxfTable flagged = Table(DxfDatabase.Parse(DxfDimensionDrawings.R2018.Replace(
                "171\n1\n172\n0\n173\n0\n174\n0\n175\n0\n176\n0\n 91\n0\n178\n0\n145\n0.0\n 92\n0\n301\nCELL_VALUE\n 93\n6\n 90\n4\n  1\nQty\n",
                "171\n1\n172\n0\n173\n1\n174\n0\n175\n1\n176\n1\n 91\n0\n178\n0\n145\n0.0\n 92\n0\n301\nCELL_VALUE\n 93\n6\n 90\n4\n  1\nQty\n")));
            Assert.False(flagged.IsMergedCell(1, 1, out _));

            // AutoCAD's merge merged again is left as it is, and unmerged it is unmerged.
            DxfDatabase db = DxfDatabase.Parse(text);
            Table(db).MergeCells(new DxfCellRange(0, 0, 0, 1));
            Assert.Equal(text, db.ToText());
            Table(db).UnmergeCells(new DxfCellRange(0, 0, 0, 1));
            Assert.False(Table(DxfDatabase.Parse(db.ToText())).IsMergedCell(0, 1, out _));
        }

        #endregion

        #region Overrides written where they stand

        [Fact]
        public void ATextStyleOrHeightSetOnACellThatHasOne_IsWrittenInItsPlace()
        {
            // D28 e14: the library's SetTextStyle of AutoCAD's (1, 0), whose 7 GH_TXT stands before its 92, wrote a second 7
            // after its value; AutoCAD read the second, the library the first. D28b t1: AutoCAD writes a cell's overrides there.
            const string Mark = "145\n0.0\n 92\n0\n301\nCELL_VALUE\n 93\n6\n 90\n4\n  1\nMark\n";
            string text = DxfDimensionDrawings.R2018
                .Replace("302\nMark\n304\nACVALUE_END\n  7\nGH_TXT\n140\n3.5\n170\n5\n", "302\nMark\n304\nACVALUE_END\n")
                .Replace(Mark, "145\n0.0\n170\n5\n  7\nGH_TXT\n140\n3.5\n 92\n0\n301\nCELL_VALUE\n 93\n6\n 90\n4\n  1\nMark\n");
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfTable table = Table(db);

            table.SetTextStyle(1, 0, (DxfTextStyleTableRecord)db.TextStyleTable["Standard"]);
            table.SetTextHeight(1, 0, 5.0);

            Assert.Equal(text.Replace("170\n5\n  7\nGH_TXT\n140\n3.5\n 92\n", "170\n5\n  7\nStandard\n140\n5\n 92\n"), db.ToText());
            DxfTable again = Table(DxfDatabase.Parse(db.ToText()));
            Assert.Equal("Standard", again.TextStyle(1, 0).Name);
            Assert.Equal(5.0, again.TextHeight(1, 0));
        }

        #endregion

        #region A line of a cell

        [Fact]
        public void ALineSetOnACell_IsFlaggedInItsEdges_AsAutoCadReadsIt()
        {
            // D28 e17, D28b G1 to G10: AutoCAD 2021 read a line of a cell from the ACAD_TABLE, wherever it stood in the cell, only
            // where the cell's edge flags (172: 1 top, 2 right, 4 bottom, 8 left) named its edge; it wrote them so (x3, x4).
            DxfDatabase db = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
            DxfTable table = Table(db);

            table.SetGridColor(1, 1, DxfGridLineType.HorizontalBottom, DxfColor.FromColorIndex(DxfColorMethod.ByAci, 1));
            table.SetGridLineWeight(1, 1, DxfGridLineType.HorizontalTop, DxfLineWeight.LineWeight050);

            Assert.Contains("171\n1\n172\n5\n173\n0\n174\n0\n175\n0\n176\n0\n 91\n1280\n178\n0\n145\n0.0\n 92\n0\n301\nCELL_VALUE\n 93\n6\n 90\n4\n  1\nQty\n 94\n0\n300\n\n302\nQty\n304\nACVALUE_END\n 66\n1\n279\n50\n171\n", Record(db.ToText(), "F6"), StringComparison.Ordinal);
            DxfTable again = Table(DxfDatabase.Parse(db.ToText()));
            Assert.Equal(1, again.GetGridColor(1, 1, DxfGridLineType.HorizontalBottom).ColorIndex);
        }

        #endregion

        #region Links read only for a table ACDB_RECOMPOSE_DATA lists

        /// <summary>The XRECORD ACDB_RECOMPOSE_DATA (B0A) of the drawing of <see cref="Linked"/>, listing the table F6 and its style 126.</summary>
        private const string Recompose = "330\nA\n100\nAcDbXrecord\n280\n1\n 90\n1\n330\nF6\n330\n126\n";

        [Fact]
        public void ALinkOfATableAcdbRecomposeDataDoesNotList_IsNotRead_AndOneSetListsTheTable()
        {
            // D28 d05 and D28b R1 to R3: AutoCAD 2021 read no link of its own 73 where ACDB_RECOMPOSE_DATA was taken out, listed
            // its table style alone or nothing; listing the table alone it read the link.
            string text = Linked();
            Assert.Contains(Recompose, text, StringComparison.Ordinal);
            Assert.True(Table(DxfDatabase.Parse(text.Replace(Recompose, Recompose.Replace("330\n126\n", string.Empty)))).IsLinked(1, 0));

            string unlisted = text.Replace(Recompose, Recompose.Replace("330\nF6\n", string.Empty));
            DxfDatabase db = DxfDatabase.Parse(unlisted);
            DxfTable table = Table(db);

            Assert.False(table.IsLinked(1, 0));
            Assert.Null(table.GetDataLink(1, 0));
            Assert.Empty(table.GetDataLink());
            Assert.Empty(Link(db).GetTargets());

            // A link set is listed, so that AutoCAD reads it.
            table.SetDataLink(2, 1, Link(db));
            string saved = db.ToText();
            Assert.Contains(Recompose, saved, StringComparison.Ordinal);
            DxfTable again = Table(DxfDatabase.Parse(saved));
            Assert.True(again.IsLinked(2, 1));
            Assert.True(again.IsLinked(1, 0));
        }

        #endregion

        #region The small forms AutoCAD writes

        [Fact]
        public void ANewCell_HoldsNoValue_AndAnInheritedOneTheEmptyValueAutoCadInherits()
        {
            // D28 b11 and b03: AutoCAD 2021 wrote each new cell of InsertRows, InsertColumns and SetSize of no value (93 7, 90 0,
            // no 1), which it reads as null, and each cell of InsertRowsAndInherit of an empty text (93 5, 90 4, no 1).
            DxfDatabase db = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
            Table(db).InsertRows(1, 10.0, 1);
            Assert.Contains("301\nCELL_VALUE\n 93\n7\n 90\n0\n 94\n0\n300\n\n302\n\n304\nACVALUE_END\n", Record(db.ToText(), "F6"), StringComparison.Ordinal);
            Assert.Equal(string.Empty, Table(db).TextString(1, 0));

            DxfDatabase inherited = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
            Table(inherited).InsertRowsAndInherit(1, 2, 1);
            Assert.Contains("145\n0.0\n 92\n0\n301\nCELL_VALUE\n 93\n5\n 90\n4\n 94\n0\n300\n\n302\n\n304\nACVALUE_END\n171\n", Record(inherited.ToText(), "F6"), StringComparison.Ordinal);
            Assert.Equal(string.Empty, Table(inherited).TextString(1, 1));
        }

        [Fact]
        public void TheTitleMergedAcrossTheTable_GrowsWithColumnsPutInAnywhere_AnotherMergeDoesNot()
        {
            // D28b c1, c2, c4, c8 and s1: AutoCAD 2021 grew the title merged across every column by the columns InsertColumns put
            // in first or last, as SetSize's; c3 and c7: a merge of another row it moved, as the library did.
            foreach (Action<DxfTable> grow in new Action<DxfTable>[] { t => t.InsertColumns(0, 20.0, 1), t => t.InsertColumns(2, 20.0, 1), t => t.SetSize(3, 3) })
            {
                DxfDatabase db = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
                grow(Table(db));
                DxfTable again = Table(DxfDatabase.Parse(db.ToText()));

                Assert.Equal(new DxfCellRange(0, 0, 0, 2), again.GetMergeRange(0, 2));
                Assert.Equal("Parts", again.TextString(0, 0));
            }

            DxfDatabase row = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
            Table(row).MergeCells(new DxfCellRange(1, 0, 1, 1));
            Table(row).InsertColumns(0, 20.0, 1);
            Assert.Equal(new DxfCellRange(1, 1, 1, 2), Table(row).GetMergeRange(1, 1));
            Assert.Equal(new DxfCellRange(0, 0, 0, 2), Table(row).GetMergeRange(0, 0));

            // The TABLECONTENT lists the title so grown, as AutoCAD 2021 listed it (0, 0, 0, 2).
            DxfDatabase linked = DxfDatabase.Parse(Linked());
            Table(linked).InsertColumns(0, 20.0, 1);
            Assert.EndsWith(G(90, "1") + G(91, "0") + G(92, "0") + G(93, "0") + G(94, "2") + G(100, "AcDbTableContent") + G(340, "126"), Record(linked.ToText(), "B03"), StringComparison.Ordinal);
        }

        [Fact]
        public void ANewColumnOfSetSize_IsAsWideAsAutoCadMakesIt_AndTheTableContentsSizesAreWrittenAsAutoCadWritesThem()
        {
            // D28b s1 to s12: AutoCAD 2021 made each column SetSize put in as wide as the header's text height and the margins
            // either side, 4.5 and 1.5 of its Standard, 7.5, whatever the other columns' widths (s4), the data's text height
            // (s5, s8) or the title's (s10): 12 for a header of 9 (s9), 10.5 for margins of 3 (s3). It writes the heights and
            // widths of its TABLECONTENT as reals, 10.0 and 20.0.
            DxfDatabase db = DxfDatabase.Parse(Linked());
            Table(db).SetSize(3, 3);
            Assert.Equal(7.5, Table(db).ColumnWidth(2));
            Assert.Contains(Column("7.5"), Record(db.ToText(), "B03"), StringComparison.Ordinal);

            // D28b s14 and s16: the title unmerged, its 6, 9 wide; the table's own header of 9, before its heights where
            // AutoCAD writes it, 12.
            DxfDatabase unmerged = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
            Table(unmerged).UnmergeCells(new DxfCellRange(0, 0, 0, 1));
            Table(unmerged).SetSize(3, 3);
            Assert.Equal(9.0, Table(unmerged).ColumnWidth(2));

            const string Head = " 93\n0\n 94\n0\n 95\n0\n 96\n0\n141\n10.0\n";
            Assert.Contains(Head, DxfDimensionDrawings.R2018, StringComparison.Ordinal);
            DxfDatabase header = DxfDatabase.Parse(DxfDimensionDrawings.R2018.Replace(Head, " 93\n2097152\n 94\n0\n 95\n0\n 96\n0\n140\n9.0\n141\n10.0\n"));
            Table(header).SetSize(3, 3);
            Assert.Equal(12.0, Table(header).ColumnWidth(2));

            DxfDatabase put = DxfDatabase.Parse(Linked());
            Table(put).InsertRows(1, 10.0, 1);
            Table(put).InsertColumns(0, 20.0, 1);
            string content = Record(put.ToText(), "B03");
            Assert.Contains(G(90, "3") + G(40, "10.0") + G(309, "TABLEROW_END"), content, StringComparison.Ordinal);
            Assert.Contains(Column("20.0"), content, StringComparison.Ordinal);
        }

        [Fact]
        public void ARowTakenOutOfTheTableContent_TakesTheFieldsOfItsCellsOutOfItsList()
        {
            // D28b d1 and d2: AutoCAD 2021's DeleteRows(1, 1) and DeleteColumns(0, 1) of its 78 took the formula's field 192 of the
            // cell (1, 0) out of the list after the TABLECONTENT's rows (92 1, 360 192 came out 92 0).
            string text = Linked(true).Replace(G(309, "TABLEROW_END") + G(92, "0") + G(100, "AcDbFormattedTableData"), G(309, "TABLEROW_END") + G(92, "1") + G(360, "B07") + G(100, "AcDbFormattedTableData"));

            foreach (Action<DxfTable> take in new Action<DxfTable>[] { t => t.DeleteRows(1, 1), t => t.DeleteColumns(0, 1) })
            {
                DxfDatabase db = DxfDatabase.Parse(text);
                take(Table(db));
                string saved = db.ToText();

                Assert.Contains(G(309, "TABLEROW_END") + G(92, "0") + G(100, "AcDbFormattedTableData"), Record(saved, "B03"), StringComparison.Ordinal);
                Assert.Null(Records(saved).SingleOrDefault(r => HandleOf(r) == "B07"));
                Assert.Null(Records(saved).SingleOrDefault(r => HandleOf(r) == "B08"));
            }

            // A row of no field keeps the list.
            DxfDatabase kept = DxfDatabase.Parse(text);
            Table(kept).DeleteRows(2, 1);
            Assert.Contains(G(92, "1") + G(360, "B07") + G(100, "AcDbFormattedTableData"), Record(kept.ToText(), "B03"), StringComparison.Ordinal);
        }

        #endregion
    }
}
