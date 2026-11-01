using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using GeometryHelper.Dxf;
using Xunit;
using static GeometryHelper.UnitTest.Dxf.DxfModel;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// What followed from AutoCAD 2021's answers to the D28 runs of 1 November 2026 (headless, the library's files read and
    /// AutoCAD's own edits saved): AutoCAD reads a table's text, values, formats, heights, widths and merges from the ACAD_TABLE,
    /// whatever its TABLECONTENT holds, and its links, the styles of its rows and the borders of its cells from the
    /// TABLECONTENT, which it keeps in step as rows and columns go in and out; a table of no TABLECONTENT is given one when a
    /// cell is linked, which AutoCAD reads only for a table the drawing's ACDB_RECOMPOSE_DATA lists. Each drawing is built in
    /// the form AutoCAD wrote, read as a file is.
    /// </summary>
    public class DxfTableD28Tests
    {
        private static string G(int code, string value) => code.ToString(CultureInfo.InvariantCulture).PadLeft(3) + "\n" + value + "\n";

        private static DxfTable Table(DxfDatabase db) => DxfAutoCadChecksD16Tests.Table(db);

        private static string Record(string text, string handle) => DxfAutoCadChecksD16Tests.Record(text, handle);

        private static string Linked(int rows = 3) => DxfAutoCadChecksD16Tests.LinkedByAutoCad(DxfDimensionDrawings.R2018, rows);

        private static DxfDataLink Link(DxfDatabase db) => (DxfDataLink)db.DataLinkDictionary["GH_PARTS"];

        #region The form AutoCAD 2021 wrote

        /// <summary>
        /// A cell of a TABLECONTENT as AutoCAD 2021 wrote the cells of the row it put in (its a20 of D28): the state given, the
        /// checksum given, a 92 of 0 and the run of a link where one is given, no content, and its format of nought.
        /// </summary>
        private static string Cell(int state = 0, string checksum = "0.0", string link = null)
        {
            var text = new StringBuilder();
            text.Append(G(300, "CELL")).Append(G(1, "LINKEDTABLEDATACELL_BEGIN")).Append(G(90, state.ToString(CultureInfo.InvariantCulture)))
                .Append(G(300, string.Empty)).Append(G(91, "0")).Append(G(301, "CUSTOMDATA")).Append(G(1, "DATAMAP_BEGIN")).Append(G(90, "1"))
                .Append(G(300, "ACAD_ROUNDTRIP_2008_CELL_CHECKSUM")).Append(G(301, "DATAMAP_VALUE")).Append(G(93, "2")).Append(G(90, "2"))
                .Append(G(140, checksum)).Append(G(94, "0")).Append(G(300, string.Empty)).Append(G(302, string.Empty)).Append(G(304, "ACVALUE_END"))
                .Append(G(309, "DATAMAP_END"));
            text.Append(link == null ? G(92, "0") : G(92, "1") + link);
            text.Append(G(95, "0")).Append(G(309, "LINKEDTABLEDATACELL_END")).Append(G(1, "FORMATTEDTABLEDATACELL_BEGIN")).Append(G(300, "CELLTABLEFORMAT"))
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

            foreach (string cell in cells)
            {
                text.Append(cell);
            }

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

        /// <summary>The run of a link AutoCAD 2021 writes after a cell's 92 of 1: the DATALINK, the rows and columns of the range, a 96 of 1.</summary>
        private static string Run(string link, int rows, int columns)
            => G(340, link) + G(93, rows.ToString(CultureInfo.InvariantCulture)) + G(94, columns.ToString(CultureInfo.InvariantCulture)) + G(96, "1");

        /// <summary>
        /// The parts of a TABLECONTENT record: its head to its column count (the 90 after AcDbLinkedTableData) taken out, its
        /// columns, its rows, and what follows its last row; the count of rows (91) between columns and rows taken out.
        /// </summary>
        private static (string Head, List<string> Columns, List<string> Rows, string Tail) Parts(string record)
        {
            string marker = G(100, "AcDbLinkedTableData");
            int at = record.IndexOf(marker, StringComparison.Ordinal) + marker.Length;
            string head = record.Substring(0, at);
            string rest = record.Substring(at);
            rest = rest.Substring(rest.IndexOf(G(300, "COLUMN"), StringComparison.Ordinal));
            var columns = new List<string>();

            while (rest.StartsWith(G(300, "COLUMN"), StringComparison.Ordinal))
            {
                int end = rest.IndexOf(G(309, "TABLECOLUMN_END"), StringComparison.Ordinal) + G(309, "TABLECOLUMN_END").Length;
                columns.Add(rest.Substring(0, end));
                rest = rest.Substring(end);
            }

            rest = rest.Substring(rest.IndexOf(G(301, "ROW"), StringComparison.Ordinal));
            var rows = new List<string>();

            while (rest.StartsWith(G(301, "ROW"), StringComparison.Ordinal))
            {
                int end = rest.IndexOf(G(309, "TABLEROW_END"), StringComparison.Ordinal) + G(309, "TABLEROW_END").Length;
                rows.Add(rest.Substring(0, end));
                rest = rest.Substring(end);
            }

            return (head, columns, rows, rest);
        }

        /// <summary>A TABLECONTENT record put together from its parts, the counts of its columns and rows written.</summary>
        private static string Content(string head, IList<string> columns, IList<string> rows, string tail)
            => head + G(90, columns.Count.ToString(CultureInfo.InvariantCulture)) + string.Concat(columns) + G(91, rows.Count.ToString(CultureInfo.InvariantCulture))
                + string.Concat(rows) + tail;

        /// <summary>The tail of a TABLECONTENT after its rows, of no field, its merged ranges given (top, left, bottom, right) and its table style.</summary>
        private static string Tail(string style, params int[][] merges)
        {
            var text = new StringBuilder();
            text.Append(G(92, "0")).Append(G(100, "AcDbFormattedTableData")).Append(G(300, "TABLEFORMAT")).Append(G(1, "TABLEFORMAT_BEGIN"))
                .Append(G(90, "4")).Append(G(170, "0")).Append(G(309, "TABLEFORMAT_END")).Append(G(90, merges.Length.ToString(CultureInfo.InvariantCulture)));

            foreach (int[] m in merges)
            {
                text.Append(G(91, m[0].ToString(CultureInfo.InvariantCulture))).Append(G(92, m[1].ToString(CultureInfo.InvariantCulture)))
                    .Append(G(93, m[2].ToString(CultureInfo.InvariantCulture))).Append(G(94, m[3].ToString(CultureInfo.InvariantCulture)));
            }

            return text.Append(G(100, "AcDbTableContent")).Append(G(340, style)).ToString();
        }

        #endregion

        #region Rows and columns, the TABLECONTENT kept in step

        [Fact]
        public void ARowPutInAboveTheLinkedCell_MovesTheLinkInTheTableContent_AsAutoCadMovedIt()
        {
            // D28 b08: AutoCAD 2021's InsertRows(1, 10, 1) of its linked 73 wrote a TABLECONTENT of four rows, the new one a row of
            // data (90 3) 10 high of empty cells, and the link in the cell (2, 0); it read the link there. The library's file of
            // the same edit, its TABLECONTENT left as it was, AutoCAD read linked at (1, 0), the new row (c07).
            string text = Linked();
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfTable table = Table(db);
            var before = Parts(Record(text, "B03"));

            table.InsertRows(1, 10.0, 1);

            string saved = db.ToText();
            var rows = new List<string>(before.Rows);
            rows.Insert(1, Row(3, "10", Cell(), Cell()));
            Assert.Equal(Content(before.Head, before.Columns, rows, Tail("126", new[] { 0, 0, 0, 1 })), Record(saved, "B03"));
            DxfTable again = Table(DxfDatabase.Parse(saved));
            Assert.False(again.IsLinked(1, 0));
            Assert.True(again.IsLinked(2, 0));
            Assert.Equal(new DxfCellRange(2, 0, 2, 0), again.GetDataLinkRange(2, 0));
            Assert.Equal("Mark", again.TextString(2, 0));
        }

        [Fact]
        public void ARowPutInBelowTheLinkedCell_LeavesTheLinkWhereItIs_AndTheTitlesMergeMovesWithARowPutInAboveIt()
        {
            // D28 b09: InsertRows(2, 10, 1), the link left at (1, 0); b01: InsertRows(0, 10, 1), the merge of the title listed
            // over (1, 0) to (1, 1) and the new row one of data.
            string text = Linked();
            var before = Parts(Record(text, "B03"));
            DxfDatabase below = DxfDatabase.Parse(text);
            Table(below).InsertRows(2, 10.0, 1);

            var rows = new List<string>(before.Rows);
            rows.Insert(2, Row(3, "10", Cell(), Cell()));
            Assert.Equal(Content(before.Head, before.Columns, rows, Tail("126", new[] { 0, 0, 0, 1 })), Record(below.ToText(), "B03"));
            Assert.True(Table(below).IsLinked(1, 0));

            DxfDatabase above = DxfDatabase.Parse(text);
            Table(above).InsertRows(0, 10.0, 1);

            rows = new List<string>(before.Rows);
            rows.Insert(0, Row(3, "10", Cell(), Cell()));
            Assert.Equal(Content(before.Head, before.Columns, rows, Tail("126", new[] { 1, 0, 1, 1 })), Record(above.ToText(), "B03"));
            Assert.True(Table(above).IsLinked(2, 0));
        }

        [Fact]
        public void ARowOfTheLinkedCellTakenOut_TakesItsCellOutOfTheTableContent_AndTheTableContentOutOfTheLinksTargets()
        {
            // D28 b14: AutoCAD 2021's DeleteRows(1, 1) of its linked 73 wrote a TABLECONTENT of the rows 0 and 2 (styles 1 and 3),
            // no link, and the DATALINK tied to the table alone, 94 1, 330 F6.
            string text = Linked();
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfTable table = Table(db);
            var before = Parts(Record(text, "B03"));

            table.DeleteRows(1, 1);

            string saved = db.ToText();
            Assert.Equal(Content(before.Head, before.Columns, new[] { before.Rows[0], before.Rows[2] }, Tail("126", new[] { 0, 0, 0, 1 })), Record(saved, "B03"));
            Assert.Contains(G(304, string.Empty) + G(94, "1") + G(330, "F6") + G(360, "0"), Record(saved, "B06"), StringComparison.Ordinal);
            Assert.Empty(Table(DxfDatabase.Parse(saved)).GetDataLink());
        }

        [Fact]
        public void AColumnPutInBeforeTheLinkedCell_MovesTheLinkAColumnOn_AndAColumnTakenOutTakesItsCells()
        {
            // D28 b15: InsertColumns(0, 20, 1), a column 20 wide first, an empty cell first in every row, the link in (1, 1);
            // b05: DeleteColumns(0, 1), the first cell of every row gone, and with it the link of (1, 0).
            string text = Linked();
            var before = Parts(Record(text, "B03"));
            DxfDatabase db = DxfDatabase.Parse(text);
            Table(db).InsertColumns(0, 20.0, 1);

            string saved = db.ToText();
            var after = Parts(Record(saved, "B03"));
            Assert.Equal(new[] { Column("20") }.Concat(before.Columns), after.Columns);
            Assert.All(after.Rows, r => Assert.StartsWith(G(301, "ROW") + G(1, "LINKEDTABLEDATAROW_BEGIN") + G(90, "3") + Cell(), r, StringComparison.Ordinal));
            Assert.Equal(Tail("126", new[] { 0, 1, 0, 2 }), after.Tail);
            DxfTable again = Table(DxfDatabase.Parse(saved));
            Assert.True(again.IsLinked(1, 1));
            Assert.False(again.IsLinked(1, 0));

            DxfDatabase taken = DxfDatabase.Parse(text);
            Table(taken).DeleteColumns(0, 1);
            var less = Parts(Record(taken.ToText(), "B03"));
            Assert.Single(less.Columns);
            Assert.Equal(before.Columns[1], less.Columns[0]);
            Assert.All(less.Rows, r => Assert.StartsWith(G(301, "ROW") + G(1, "LINKEDTABLEDATAROW_BEGIN") + G(90, "1") + Cell(), r, StringComparison.Ordinal));
            Assert.Equal(Tail("126", new[] { 0, 0, 0, 0 }), less.Tail);
            Assert.Empty(Table(DxfDatabase.Parse(taken.ToText())).GetDataLink());
        }

        [Fact]
        public void ATableGivenAnotherSize_HasATableContentOfThatSize()
        {
            // D28 b11: AutoCAD 2021's SetSize(4, 3) wrote a fourth row of data as high as the last and a third column.
            string text = Linked();
            var before = Parts(Record(text, "B03"));
            DxfDatabase db = DxfDatabase.Parse(text);
            Table(db).SetSize(4, 3);

            var after = Parts(Record(db.ToText(), "B03"));
            Assert.Equal(3, after.Columns.Count);
            Assert.Equal(Column("30.0"), after.Columns[2]);
            Assert.Equal(4, after.Rows.Count);
            Assert.Equal(Row(3, "8.0", Cell(), Cell(), Cell()), after.Rows[3]);
            Assert.True(Table(db).IsLinked(1, 0));

            DxfDatabase small = DxfDatabase.Parse(text);
            Table(small).SetSize(1, 1);
            var less = Parts(Record(small.ToText(), "B03"));
            Assert.Single(less.Columns);
            Assert.Single(less.Rows);
            Assert.Equal(Tail("126", new[] { 0, 0, 0, 0 }), less.Tail);
            Assert.Empty(Table(small).GetDataLink());
        }

        [Fact]
        public void ARowsInheritingAnother_TakeItsStyleInTheTableContent()
        {
            // D28 b03: InsertRowsAndInherit(1, 2, 1) wrote the new row of the style of the row 2, one of data, 8 high.
            string text = Linked();
            var before = Parts(Record(text, "B03"));
            DxfDatabase db = DxfDatabase.Parse(text);
            Table(db).InsertRowsAndInherit(1, 2, 1);

            var after = Parts(Record(db.ToText(), "B03"));
            Assert.Equal(4, after.Rows.Count);
            Assert.Equal(Row(3, "8.0", Cell(), Cell()), after.Rows[1]);
            Assert.Equal(before.Rows[1], after.Rows[2]);
        }

        [Fact]
        public void CellsMergedAndUnmerged_AreListedSoInTheTableContent()
        {
            // D28 a07 and b12: AutoCAD 2021 listed (1, 0) to (1, 1) after the title once it merged them, and took it out again.
            string text = Linked();
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfTable table = Table(db);

            table.MergeCells(new DxfCellRange(2, 0, 2, 1));
            Assert.Equal(Tail("126", new[] { 0, 0, 0, 1 }, new[] { 2, 0, 2, 1 }), Parts(Record(db.ToText(), "B03")).Tail);

            table.UnmergeCells(new DxfCellRange(2, 0, 2, 0));
            Assert.Equal(Record(text, "B03"), Record(db.ToText(), "B03"));
        }

        [Fact]
        public void ATableContentOfFewerRowsThanItsTable_IsLeftAsItIsPastItsLastRow()
        {
            // AutoCAD 2021's 73e: a table of three rows over a TABLECONTENT of two; a row put in after both is not put in the TABLECONTENT.
            string text = Linked(2);
            DxfDatabase db = DxfDatabase.Parse(text);
            Table(db).InsertRows(3, 10.0, 1);
            Assert.Equal(Record(text, "B03"), Record(db.ToText(), "B03"));

            Table(db).InsertRows(1, 10.0, 1);
            Assert.Equal(3, Parts(Record(db.ToText(), "B03")).Rows.Count);
            Assert.True(Table(db).IsLinked(2, 0));
        }

        #endregion

        #region A link set on a table of no TABLECONTENT

        /// <summary>
        /// The drawing of the table F6 with no TABLECONTENT, as ezdxf writes one, its table style 126 named (342), and the DATALINK
        /// GH_PARTS (B06, in ACAD_DATALINK B05) tied to nothing.
        /// </summary>
        private static string Bare(string version)
        {
            string text = version
                .Replace("$HANDSEED\n  5\n10A\n", "$HANDSEED\n  5\nB10\n")
                .Replace("100\nAcDbTable\n280\n0\n343\nE6\n", "100\nAcDbTable\n280\n0\n342\n126\n343\nE6\n")
                .Replace("  3\nEZDXF_META\n350\n2D\n", "  3\nACAD_DATALINK\n350\nB05\n  3\nEZDXF_META\n350\n2D\n");
            var objects = new StringBuilder();
            objects.Append(G(0, "DICTIONARY")).Append(G(5, "B05")).Append(G(330, "A")).Append(G(100, "AcDbDictionary")).Append(G(281, "1")).Append(G(3, "GH_PARTS")).Append(G(350, "B06"));
            objects.Append(G(0, "DATALINK")).Append(G(5, "B06")).Append(G(102, "{ACAD_REACTORS")).Append(G(330, "B05")).Append(G(102, "}")).Append(G(330, "B05"))
                .Append(G(100, "AcDbDataLink")).Append(G(1, "AcExcel")).Append(G(300, string.Empty)).Append(G(301, string.Empty)).Append(G(302, "73-parts.csv"))
                .Append(G(90, "2")).Append(G(91, "0")).Append(G(92, "0")).Append(G(170, "1900")).Append(G(171, "1")).Append(G(172, "0")).Append(G(173, "0"))
                .Append(G(174, "0")).Append(G(175, "0")).Append(G(176, "0")).Append(G(177, "0")).Append(G(93, "654")).Append(G(304, string.Empty))
                .Append(G(94, "0")).Append(G(360, "0")).Append(G(305, "CUSTOMDATA")).Append(G(1, "DATAMAP_BEGIN")).Append(G(90, "0")).Append(G(309, "DATAMAP_END"));
            return text.Replace("  0\nENDSEC\n  0\nEOF\n", objects + "  0\nENDSEC\n  0\nEOF\n");
        }

        /// <summary>The value of the first group of a code after a text in a record; null where there is none.</summary>
        private static string After(string record, string text, int code)
        {
            int at = record.IndexOf(text, StringComparison.Ordinal);
            string key = code.ToString(CultureInfo.InvariantCulture).PadLeft(3) + "\n";

            for (int i = record.IndexOf('\n', at < 0 ? 0 : at); at >= 0 && i >= 0 && i + 1 < record.Length; i = record.IndexOf('\n', i + 1))
            {
                if (string.CompareOrdinal(record, i + 1, key, 0, key.Length) == 0)
                {
                    int start = i + 1 + key.Length;
                    return record.Substring(start, record.IndexOf('\n', start) - start);
                }
            }

            return null;
        }

        [Fact]
        public void ATableOfNoTableContent_IsGivenOneWhenACellIsLinked_AsAutoCadGaveOne()
        {
            // D28 d01 to d05: AutoCAD 2021 read the link of its own 73 with its TABLEGEOMETRY, every cell's content and the class
            // of TABLEGEOMETRY taken out, the text of every cell from the ACAD_TABLE; with ACDB_RECOMPOSE_DATA taken out as
            // well it read no link at all. AutoCAD's own SetDataLink of a table of none (its 73) wrote a TABLECONTENT whole.
            foreach (string version in new[] { DxfAutoCadChecksD16Tests.R2007, DxfDimensionDrawings.R2018 })
            {
                DxfDatabase db = DxfDatabase.Parse(Bare(version));
                DxfTable table = Table(db);

                table.SetDataLink(1, 0, Link(db));

                string saved = db.ToText();
                string f6 = Record(saved, "F6");
                string dictionary = After(f6, G(102, "{ACAD_XDICTIONARY"), 360);
                string roundtrip = After(Record(saved, dictionary), G(3, "ACAD_XREC_ROUNDTRIP"), 360);
                string xrecord = Record(saved, roundtrip);
                string content = After(xrecord, G(102, "ACAD_ROUNDTRIP_2008_TABLE_ENTITY"), 360);
                Assert.EndsWith(G(100, "AcDbXrecord") + G(280, "1") + G(102, "ACAD_ROUNDTRIP_2008_TABLE_ENTITY") + G(360, content) + G(70, "2") + G(90, "1")
                    + G(10, "0.0") + G(20, "0.0") + G(30, "0.0") + G(90, "0") + G(90, "2"), xrecord, StringComparison.Ordinal);

                string head = G(0, "TABLECONTENT") + G(5, content) + G(330, roundtrip) + G(100, "AcDbLinkedData") + G(1, string.Empty) + G(300, string.Empty)
                    + G(100, "AcDbLinkedTableData");
                var rows = new[]
                {
                    Row(1, "10.0", Cell(0, "1655.0"), Cell()),
                    Row(2, "8.0", Cell(4, "1041.0", Run("B06", 1, 1)), Cell(0, "676.0")),
                    Row(3, "8.0", Cell(0, "503.0"), Cell()),
                };
                Assert.Equal(Content(head, new[] { Column("40.0"), Column("30.0") }, rows, Tail("126", new[] { 0, 0, 0, 1 })), Record(saved, content));

                string recompose = Record(saved, After(Record(saved, "A"), G(3, "ACDB_RECOMPOSE_DATA"), 350) ?? After(Record(saved, "A"), G(3, "ACDB_RECOMPOSE_DATA"), 360));
                Assert.EndsWith(G(100, "AcDbXrecord") + G(280, "1") + G(90, "1") + G(330, "F6") + G(330, "126"), recompose, StringComparison.Ordinal);
                Assert.Contains(G(0, "CLASS") + G(1, "TABLECONTENT") + G(2, "AcDbTableContent") + G(3, "ObjectDBX Classes") + G(90, "1152"), saved, StringComparison.Ordinal);
                Assert.Contains(G(304, string.Empty) + G(94, "2") + G(330, "F6") + G(330, content) + G(360, "0"), Record(saved, "B06"), StringComparison.Ordinal);

                DxfDatabase again = DxfDatabase.Parse(saved);
                Assert.True(Table(again).IsLinked(1, 0));
                Assert.Same(Link(again), Table(again).GetDataLink(1, 0));
                Assert.Equal(new[] { Table(again) }, Link(again).GetTargets());
                Assert.Equal("Mark", Table(again).TextString(1, 0));
                Assert.Equal(saved, again.ToText());
            }
        }

        [Fact]
        public void ATableOfNoTableContentBeforeR2007_IsStillNotLinked()
        {
            // A TABLECONTENT came with R2007 (AutoCAD saved its 73 as R2007 with one); a drawing of R2000 cannot hold one.
            string text = Bare(DxfDimensionDrawings.R2018.Replace("$ACADVER\n  1\nAC1032\n", "$ACADVER\n  1\nAC1018\n"));
            DxfDatabase db = DxfDatabase.Parse(text);

            Assert.Throws<InvalidOperationException>(() => Table(db).SetDataLink(1, 0, Link(db)));
            Assert.Equal(text, db.ToText());
        }

        #endregion

        #region A value given to a cell of a null value

        [Fact]
        public void ATextOrValueGivenToACellOfANullValue_IsWrittenAsAValueAutoCadReads()
        {
            // D28 e19, c03, c04 and v: AutoCAD 2021 read a cell whose value has the flag 1 (93 of 1, 5 or 7, as it writes a block
            // cell's, a formula's and an empty one's) as empty, whatever text it held; of 0, 2, 4 or 6, the text. The library kept
            // the flags of the block cell made a text cell, M16 read as nothing.
            string text = DxfDimensionDrawings.R2018.Replace(" 93\n6\n 90\n4\n  1\nQty\n", " 93\n1\n 90\n4\n  1\nQty\n");
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfTable table = Table(db);

            table.SetTextString(1, 1, "Count");
            Assert.Contains("301\nCELL_VALUE\n 93\n6\n 90\n4\n  1\nCount\n", Record(db.ToText(), "F6"), StringComparison.Ordinal);

            DxfDatabase valued = DxfDatabase.Parse(text);
            Table(valued).SetValue(1, 1, 5.0);
            Assert.Contains("301\nCELL_VALUE\n 93\n6\n 90\n2\n140\n", Record(valued.ToText(), "F6"), StringComparison.Ordinal);
            Assert.Equal(5.0, Table(DxfDatabase.Parse(valued.ToText())).Value(1, 1));
        }

        #endregion
    }
}
