using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Dxf.DxfModel;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// What followed from AutoCAD 2021's answers to the checks of 29 and 30 October 2026 up to plugin run v8: a table's data
    /// link is kept in the TABLECONTENT of its extension dictionary, and a formula's field stands before a cell's 92 (points
    /// 73 to 78); an MTEXT of R2018 in columns is as wide as all its columns, and one of R2000 writes no defined height of
    /// nought (9); a viewport turned off has the number -1 (32); a MINSERT of one cell is not exploded either, and measures its
    /// first cell (51); a closed spline of no fit points is made a polyline by the halving alone (17f). Each drawing is one of
    /// ezdxf's, or built here in the form AutoCAD wrote, read as a file is.
    /// </summary>
    public class DxfAutoCadChecksD16Tests
    {
        private static readonly Tolerance Tol = Tolerance.Default;

        private static readonly Tolerance Tight = new Tolerance(1E-9, 1E-9, 1E-9);

        private static GeoPoint3 P3(double x, double y, double z) => new GeoPoint3(x, y, z);

        private static string G(int code, string value) => code.ToString(CultureInfo.InvariantCulture).PadLeft(3) + "\n" + value + "\n";

        private static string G(int code, double value) => G(code, value.ToString("R", CultureInfo.InvariantCulture));

        #region Points 73 to 78: a table's data link in its TABLECONTENT

        // The cells (1, 0) Mark of the table F6, before its value, and the R2007 drawing of the same text.
        private const string MarkCell = "145\n0.0\n 92\n0\n301\nCELL_VALUE\n 93\n6\n 90\n4\n  1\nMark\n";

        private static readonly string R2007 = DxfDimensionDrawings.R2018.Replace("$ACADVER\n  1\nAC1032\n", "$ACADVER\n  1\nAC1021\n");

        /// <summary>
        /// A cell of a TABLECONTENT as AutoCAD 2021 wrote its cells of no content in 73-autocad-linked-r2018.dxf: its state,
        /// its custom data of one checksum of nought, its 92 and the run of its link where it names one, no content, and its
        /// format of nought.
        /// </summary>
        private static string ContentCell(int state, string link, string field = null)
        {
            var text = new StringBuilder();
            text.Append(G(300, "CELL")).Append(G(1, "LINKEDTABLEDATACELL_BEGIN")).Append(G(90, state.ToString(CultureInfo.InvariantCulture)))
                .Append(G(300, string.Empty)).Append(G(91, "0")).Append(G(301, "CUSTOMDATA")).Append(G(1, "DATAMAP_BEGIN")).Append(G(90, "1"))
                .Append(G(300, "ACAD_ROUNDTRIP_2008_CELL_CHECKSUM")).Append(G(301, "DATAMAP_VALUE")).Append(G(93, "2")).Append(G(90, "2"))
                .Append(G(140, "0.0")).Append(G(94, "0")).Append(G(300, string.Empty)).Append(G(302, string.Empty)).Append(G(304, "ACVALUE_END"))
                .Append(G(309, "DATAMAP_END"));
            text.Append(link == null ? G(92, "0") : G(92, "1") + G(340, link) + G(93, "1") + G(94, "1") + G(96, "1"));
            text.Append(field == null ? G(95, "0") : G(95, "1") + G(302, "CONTENT") + G(1, "CELLCONTENT_BEGIN") + G(90, "2") + G(340, field) + G(91, "0")
                + G(309, "CELLCONTENT_END") + G(1, "FORMATTEDCELLCONTENT_BEGIN") + G(170, "0") + G(309, "FORMATTEDCELLCONTENT_END"));
            text.Append(G(309, "LINKEDTABLEDATACELL_END")).Append(G(1, "FORMATTEDTABLEDATACELL_BEGIN")).Append(G(300, "CELLTABLEFORMAT"))
                .Append(G(1, "TABLEFORMAT_BEGIN")).Append(G(90, "1")).Append(G(170, "1")).Append(G(91, "0")).Append(G(92, "0")).Append(G(62, "257"))
                .Append(G(93, "1")).Append(G(300, "CONTENTFORMAT")).Append(G(1, "CONTENTFORMAT_BEGIN")).Append(G(90, "0")).Append(G(91, "0"))
                .Append(G(92, "512")).Append(G(93, "0")).Append(G(300, string.Empty)).Append(G(40, "0.0")).Append(G(140, "1.0")).Append(G(94, "1"))
                .Append(G(62, "0")).Append(G(340, "0")).Append(G(144, "0.18")).Append(G(309, "CONTENTFORMAT_END")).Append(G(171, "0")).Append(G(94, "0"))
                .Append(G(309, "TABLEFORMAT_END")).Append(G(309, "FORMATTEDTABLEDATACELL_END")).Append(G(1, "TABLECELL_BEGIN")).Append(G(90, "0"))
                .Append(G(91, "0")).Append(G(309, "TABLECELL_END"));
            return text.ToString();
        }

        /// <summary>
        /// The TABLECONTENT B03 of the table F6 in the form AutoCAD 2021 wrote it when it linked the table (its 149): two columns
        /// 40 and 30 wide, the rows given, 10, 8 and 8 high, the cell given in the state 4 of a linked cell naming the DATALINK
        /// B06 over one row and one column, the merging of the title, and the table style 126.
        /// </summary>
        private static string TableContent(int rows, int linkedRow, int linkedColumn, string field = null)
        {
            var text = new StringBuilder();
            text.Append(G(0, "TABLECONTENT")).Append(G(5, "B03")).Append(G(330, "B02")).Append(G(100, "AcDbLinkedData")).Append(G(1, string.Empty))
                .Append(G(300, string.Empty)).Append(G(100, "AcDbLinkedTableData")).Append(G(90, "2"));

            foreach (string width in new[] { "40.0", "30.0" })
            {
                text.Append(G(300, "COLUMN")).Append(G(1, "LINKEDTABLEDATACOLUMN_BEGIN")).Append(G(300, string.Empty)).Append(G(91, "0"))
                    .Append(G(301, "CUSTOMDATA")).Append(G(1, "DATAMAP_BEGIN")).Append(G(90, "0")).Append(G(309, "DATAMAP_END"))
                    .Append(G(309, "LINKEDTABLEDATACOLUMN_END")).Append(G(1, "FORMATTEDTABLEDATACOLUMN_BEGIN")).Append(G(300, "COLUMNTABLEFORMAT"))
                    .Append(G(1, "TABLEFORMAT_BEGIN")).Append(G(90, "3")).Append(G(170, "0")).Append(G(309, "TABLEFORMAT_END"))
                    .Append(G(309, "FORMATTEDTABLEDATACOLUMN_END")).Append(G(1, "TABLECOLUMN_BEGIN")).Append(G(90, "0")).Append(G(40, width))
                    .Append(G(309, "TABLECOLUMN_END"));
            }

            text.Append(G(91, rows.ToString(CultureInfo.InvariantCulture)));

            for (int row = 0; row < rows; row++)
            {
                text.Append(G(301, "ROW")).Append(G(1, "LINKEDTABLEDATAROW_BEGIN")).Append(G(90, "2"));

                for (int column = 0; column < 2; column++)
                {
                    bool linked = row == linkedRow && column == linkedColumn;
                    text.Append(ContentCell(linked ? (field == null ? 4 : 76) : 0, linked ? "B06" : null, linked ? field : null));
                }

                text.Append(G(91, "0")).Append(G(301, "CUSTOMDATA")).Append(G(1, "DATAMAP_BEGIN")).Append(G(90, "0")).Append(G(309, "DATAMAP_END"))
                    .Append(G(309, "LINKEDTABLEDATAROW_END")).Append(G(1, "FORMATTEDTABLEDATAROW_BEGIN")).Append(G(300, "ROWTABLEFORMAT"))
                    .Append(G(1, "TABLEFORMAT_BEGIN")).Append(G(90, "2")).Append(G(170, "0")).Append(G(309, "TABLEFORMAT_END"))
                    .Append(G(309, "FORMATTEDTABLEDATAROW_END")).Append(G(1, "TABLEROW_BEGIN")).Append(G(90, (row + 1).ToString(CultureInfo.InvariantCulture)))
                    .Append(G(40, row == 0 ? "10.0" : "8.0")).Append(G(309, "TABLEROW_END"));
            }

            text.Append(G(92, "0")).Append(G(100, "AcDbFormattedTableData")).Append(G(300, "TABLEFORMAT")).Append(G(1, "TABLEFORMAT_BEGIN"))
                .Append(G(90, "4")).Append(G(170, "0")).Append(G(309, "TABLEFORMAT_END")).Append(G(90, "1")).Append(G(91, "0")).Append(G(92, "0"))
                .Append(G(93, "0")).Append(G(94, "1")).Append(G(100, "AcDbTableContent")).Append(G(340, "126"));
            return text.ToString();
        }

        /// <summary>
        /// The drawing of the table F6 (three rows 10, 8 and 8 high, two columns 40 and 30 wide, its title Parts merged across
        /// both) as AutoCAD 2021 wrote it linked: its extension dictionary B01 holding ACAD_XREC_ROUNDTRIP, the XRECORD B02 of
        /// ACAD_ROUNDTRIP_2008_TABLE_ENTITY pointing at its TABLECONTENT B03 and its TABLEGEOMETRY B04, the TABLECONTENT naming
        /// the DATALINK GH_PARTS (B06, in ACAD_DATALINK B05) from the cell (1, 0); the DATALINK tied to the table and its
        /// TABLECONTENT (94 2, 330 F6, 330 B03), its groups as AutoCAD's 142; the cells of the ACAD_TABLE naming no link.
        /// </summary>
        private static string LinkedByAutoCad(string version, int rows = 3, bool formula = false)
        {
            string text = version
                .Replace("$HANDSEED\n  5\n10A\n", "$HANDSEED\n  5\nB10\n")
                .Replace("  0\nACAD_TABLE\n  5\nF6\n330\n17\n", "  0\nACAD_TABLE\n  5\nF6\n102\n{ACAD_XDICTIONARY\n360\nB01\n102\n}\n330\n17\n")
                .Replace("  3\nEZDXF_META\n350\n2D\n", "  3\nACAD_DATALINK\n350\nB05\n  3\nEZDXF_META\n350\n2D\n");
            var objects = new StringBuilder();
            objects.Append(G(0, "DICTIONARY")).Append(G(5, "B01")).Append(G(330, "F6")).Append(G(100, "AcDbDictionary")).Append(G(280, "1")).Append(G(281, "1"))
                .Append(G(3, "ACAD_XREC_ROUNDTRIP")).Append(G(360, "B02"));
            objects.Append(G(0, "XRECORD")).Append(G(5, "B02")).Append(G(102, "{ACAD_REACTORS")).Append(G(330, "B01")).Append(G(102, "}")).Append(G(330, "B01"))
                .Append(G(100, "AcDbXrecord")).Append(G(280, "1")).Append(G(102, "ACAD_ROUNDTRIP_2008_TABLE_ENTITY")).Append(G(360, "B03")).Append(G(70, "2"))
                .Append(G(90, "1")).Append(G(10, "0.0")).Append(G(20, "0.0")).Append(G(30, "0.0")).Append(G(90, "0")).Append(G(90, "2")).Append(G(361, "B04"));
            objects.Append(TableContent(rows, 1, 0, formula ? "B07" : null));

            if (formula)
            {
                // The fields of AutoCAD's 195 and 196: a field of text holding the expression, owned by the TABLECONTENT.
                objects.Append(G(0, "FIELD")).Append(G(5, "B07")).Append(G(330, "B03")).Append(G(100, "AcDbField")).Append(G(1, "_text")).Append(G(2, "%<\\_FldIdx 0>%"))
                    .Append(G(90, "1")).Append(G(360, "B08")).Append(G(97, "0")).Append(G(91, "63")).Append(G(92, "0")).Append(G(94, "13")).Append(G(95, "2")).Append(G(96, "0"))
                    .Append(G(300, string.Empty)).Append(G(93, "0")).Append(G(7, "ACFD_FIELD_VALUE")).Append(G(93, "3")).Append(G(90, "0")).Append(G(94, "0"))
                    .Append(G(300, string.Empty)).Append(G(302, string.Empty)).Append(G(304, "ACVALUE_END")).Append(G(301, string.Empty)).Append(G(98, "0"));
                objects.Append(G(0, "FIELD")).Append(G(5, "B08")).Append(G(330, "B07")).Append(G(100, "AcDbField")).Append(G(1, "AcExpr")).Append(G(2, "\\AcExpr (1+2)"))
                    .Append(G(90, "0")).Append(G(97, "0")).Append(G(91, "63")).Append(G(92, "0")).Append(G(94, "27")).Append(G(95, "2")).Append(G(96, "0"))
                    .Append(G(300, string.Empty)).Append(G(93, "0")).Append(G(7, "ACFD_FIELD_VALUE")).Append(G(93, "0")).Append(G(90, "1")).Append(G(91, "3")).Append(G(94, "0"))
                    .Append(G(300, string.Empty)).Append(G(302, string.Empty)).Append(G(304, "ACVALUE_END")).Append(G(301, string.Empty)).Append(G(98, "1"));
                text = text.Replace(MarkCell, "145\n0.0\n344\nB09\n 92\n0\n301\nCELL_VALUE\n 93\n6\n 90\n4\n  1\nMark\n");
            }
            objects.Append(G(0, "TABLEGEOMETRY")).Append(G(5, "B04")).Append(G(330, "B02")).Append(G(100, "AcDbTableGeometry")).Append(G(90, "3")).Append(G(91, "2")).Append(G(92, "0"));
            objects.Append(G(0, "DICTIONARY")).Append(G(5, "B05")).Append(G(330, "A")).Append(G(100, "AcDbDictionary")).Append(G(281, "1")).Append(G(3, "GH_PARTS")).Append(G(350, "B06"));
            objects.Append(G(0, "DATALINK")).Append(G(5, "B06")).Append(G(102, "{ACAD_REACTORS")).Append(G(330, "B05")).Append(G(102, "}")).Append(G(330, "B05"))
                .Append(G(100, "AcDbDataLink")).Append(G(1, "AcExcel")).Append(G(300, string.Empty)).Append(G(301, string.Empty)).Append(G(302, "73-parts.csv"))
                .Append(G(90, "2")).Append(G(91, "0")).Append(G(92, "0")).Append(G(170, "1900")).Append(G(171, "1")).Append(G(172, "0")).Append(G(173, "0"))
                .Append(G(174, "0")).Append(G(175, "0")).Append(G(176, "0")).Append(G(177, "0")).Append(G(93, "654")).Append(G(304, string.Empty))
                .Append(G(94, "2")).Append(G(330, "F6")).Append(G(330, "B03")).Append(G(360, "0")).Append(G(305, "CUSTOMDATA")).Append(G(1, "DATAMAP_BEGIN"))
                .Append(G(90, "0")).Append(G(309, "DATAMAP_END"));
            return text.Replace("  0\nENDSEC\n  0\nEOF\n", objects + "  0\nENDSEC\n  0\nEOF\n");
        }

        private static DxfTable Table(DxfDatabase db) => db.ModelSpace.OfType<DxfTable>().First();

        private static string Record(string text, string handle) => Records(text).Single(r => HandleOf(r) == handle);

        [Fact]
        public void ALinkAutoCadKeepsInTheTableContent_IsReadFromItsCell_OverTheRangeItNames()
        {
            // Plugin v3, 73 and 77: AutoCAD 2021 linked F6 over (1, 0) to (2, 1) and read it back as (1, 0) alone, its link 142
            // named by the TABLECONTENT's cell (1, 0) (92 1, 340 142, 93 1, 94 1, 96 1), none by the ACAD_TABLE's cells.
            foreach (string version in new[] { R2007, DxfDimensionDrawings.R2018 })
            {
                string text = LinkedByAutoCad(version);
                DxfDatabase db = DxfDatabase.Parse(text);
                DxfTable table = Table(db);
                var link = (DxfDataLink)db.DataLinkDictionary["GH_PARTS"];

                Assert.Equal(text, db.ToText());
                Assert.True(table.IsLinked(1, 0));
                Assert.False(table.IsLinked(1, 1));
                Assert.False(table.IsLinked(2, 1));
                Assert.Same(link, table.GetDataLink(1, 0));
                Assert.Null(table.GetDataLink(0, 0));
                Assert.Equal(new DxfCellRange(1, 0, 1, 0), table.GetDataLinkRange(1, 0));
                Assert.Equal(new[] { link }, table.GetDataLink());
                Assert.Equal(new[] { table }, link.GetTargets());
                Assert.Equal("Mark", table.TextString(1, 0));
            }
        }

        [Fact]
        public void ATableContentOfOtherRowsThanItsTable_NamesNoLink()
        {
            // A TABLECONTENT left behind as the table changed, two rows where the table has three, is not read.
            DxfDatabase db = DxfDatabase.Parse(LinkedByAutoCad(DxfDimensionDrawings.R2018, 2));

            Assert.Empty(Table(db).GetDataLink());
            Assert.False(Table(db).IsLinked(1, 0));
        }

        [Fact]
        public void ALinkOfTheTableContentTakenAway_LeavesItsCellUnlinkedThere_AndTheDataLinkTiedToNothing()
        {
            // Plugin v3, 77: AutoCAD 2021's RemoveDataLink() left the cell (1, 0) of its TABLECONTENT of state 0 and 92 0 with
            // no link after it, and kept the DATALINK, its 94 0.
            string text = LinkedByAutoCad(DxfDimensionDrawings.R2018);
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfTable table = Table(db);
            var link = (DxfDataLink)db.DataLinkDictionary["GH_PARTS"];

            // A cell outside the link takes nothing away.
            table.RemoveDataLink(2, 1);
            Assert.Equal(text, db.ToText());

            table.RemoveDataLink(1, 0);

            string saved = db.ToText();
            Assert.Equal(Record(text, "F6"), Record(saved, "F6"));
            Assert.Equal(TableContent(3, -1, -1), Record(saved, "B03"));
            Assert.Contains(G(304, string.Empty) + G(94, "0") + G(360, "0") + G(305, "CUSTOMDATA"), Record(saved, "B06"), StringComparison.Ordinal);
            Assert.False(link.IsErased);
            Assert.Equal(1, db.DataLinkDictionary.Count);
            Assert.Empty(table.GetDataLink());
            Assert.Empty(link.GetTargets());

            // RemoveDataLink() of every link writes the same; of a table of none, nothing.
            DxfDatabase all = DxfDatabase.Parse(text);
            Table(all).RemoveDataLink();
            Assert.Equal(saved, all.ToText());
            Table(all).RemoveDataLink();
            Assert.Equal(saved, all.ToText());
        }

        [Fact]
        public void AFormulaSetOnACell_HasItsFieldBeforeThe92OfTheCell_AsAutoCadWritesIt()
        {
            // Plugin v3, 78: AutoCAD 2021 wrote the formula of its cell (1, 0) as 145 0.0, 344 192, 170 5, 92 0, then its value.
            foreach (string version in new[] { R2007, DxfDimensionDrawings.R2018 })
            {
                DxfDatabase db = DxfDatabase.Parse(version);
                DxfTable table = Table(db);

                table.SetFormula(1, 0, 0, "=1+2");

                string record = Record(db.ToText(), "F6");
                int at = record.IndexOf("145\n0.0\n344\n", StringComparison.Ordinal);
                Assert.True(at >= 0, record);
                Assert.Contains("\n 92\n0\n301\nCELL_VALUE\n 93\n6\n 90\n4\n  1\nMark\n", record.Substring(at), StringComparison.Ordinal);
                Assert.Equal("=1+2", Table(DxfDatabase.Parse(db.ToText())).GetFormula(1, 0, 0));
            }
        }

        [Fact]
        public void AFormulaOfALinkedCellAutoCadKeepsInTheTableContent_IsReadFromThere_WhereTheCellsFieldIsInNoRecord()
        {
            // Plugin v3, 78: AutoCAD 2021 wrote the formula of its linked cell (1, 0) as the field 195 of its TABLECONTENT,
            // \AcExpr (1+2) within a field of text, and the ACAD_TABLE's cell's 344 192 of no record in the file.
            string text = LinkedByAutoCad(DxfDimensionDrawings.R2018, 3, true);
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfTable table = Table(db);

            Assert.Equal("=1+2", table.GetFormula(1, 0, 0));
            Assert.True(table.HasFormula(1, 0, 0));
            Assert.Null(table.GetFormula(0, 0, 0));
            Assert.True(table.IsLinked(1, 0));

            // Taken away, the link leaves the formula where it is.
            table.RemoveDataLink();
            Assert.Equal("=1+2", Table(DxfDatabase.Parse(db.ToText())).GetFormula(1, 0, 0));
            Assert.Empty(Table(DxfDatabase.Parse(db.ToText())).GetDataLink());
        }

        #endregion

        #region Point 9: an MTEXT in columns

        [Fact]
        public void AnMTextOfR2018InStaticColumns_IsAsWideAsAllItsColumnsAndGutters_AsAutoCadWritesIt()
        {
            // AutoCAD 2021 saved the two static columns 50 wide with a gutter of 10 of 09-a with 41 110 and the embedded 40 110.
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            DxfMText mtext = ByHandle<DxfMText>(db, "73");

            mtext.SetStaticColumns(50.0, 10.0, 2);

            Assert.Equal(110.0, mtext.Width);
            string record = Record(db.ToText(), "73");
            Assert.Contains(" 40\n110\n 41\n0\n 42\n110\n", record, StringComparison.Ordinal);
            DxfMText again = ByHandle<DxfMText>(DxfDatabase.Parse(db.ToText()), "73");
            Assert.Equal(110.0, again.Width);
            Assert.Equal(50.0, again.ColumnWidth);
            Assert.Equal(10.0, again.ColumnGutterWidth);
            Assert.Equal(2, again.ColumnCount);

            // One column more, or a wider gutter, widens it; a column narrower, narrows it.
            mtext.ColumnCount = 3;
            Assert.Equal(170.0, mtext.Width);
            mtext.ColumnGutterWidth = 5.0;
            Assert.Equal(160.0, mtext.Width);
            mtext.ColumnWidth = 20.0;
            Assert.Equal(70.0, mtext.Width);
        }

        [Fact]
        public void AnMTextOfR2000InColumns_IsAsWideAsAColumn_AndWritesNoDefinedHeightOfNought_AsAutoCadWritesIt()
        {
            // AutoCAD 2021 saved 09-a as R2000: the static columns of 8D with 41 50 and no ACAD_MTEXT_DEFINED_HEIGHT, its
            // height being nought.
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2000);
            DxfMText mtext = ByHandle<DxfMText>(db, "73");

            mtext.SetStaticColumns(50.0, 10.0, 2);

            Assert.Equal(50.0, mtext.Width);
            Assert.Equal(
                new[] { "1001 ACAD", "1000 ACAD_MTEXT_COLUMN_INFO_BEGIN", "1070 75", "1070 1", "1070 79", "1070 0", "1070 76", "1070 2", "1070 78", "1070 0", "1070 48", "1040 50", "1070 49", "1040 10", "1000 ACAD_MTEXT_COLUMN_INFO_END" },
                ByHandle<DxfMText>(DxfDatabase.Parse(db.ToText()), "73").GetXDataForApplication("ACAD").Select(g => g.TypeCode + " " + g.Value));

            // A height above nought is written, as AutoCAD writes one.
            mtext.Height = 30.0;
            Assert.Contains("1000 ACAD_MTEXT_DEFINED_HEIGHT_BEGIN", ByHandle<DxfMText>(DxfDatabase.Parse(db.ToText()), "73").GetXDataForApplication("ACAD").Select(g => g.TypeCode + " " + g.Value));
            Assert.Equal(30.0, ByHandle<DxfMText>(DxfDatabase.Parse(db.ToText()), "73").Height);
        }

        [Fact]
        public void AnMTextOfR2000InColumnsOfTheirOwnHeights_WritesTheFirstColumnsHeightAsItsDefinedHeight_AsAutoCadWritesIt()
        {
            // AutoCAD 2021 saved the columns 15 and 25 high of 8F as R2000 with ACAD_MTEXT_DEFINED_HEIGHT 15 after the heights.
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2000);
            DxfMText mtext = ByHandle<DxfMText>(db, "73");

            mtext.SetDynamicColumns(50.0, 10.0, false);
            mtext.ColumnCount = 2;
            mtext.SetColumnHeight(0, 15.0);
            mtext.SetColumnHeight(1, 25.0);

            DxfMText again = ByHandle<DxfMText>(DxfDatabase.Parse(db.ToText()), "73");
            Assert.Equal(
                new[] { "1001 ACAD", "1000 ACAD_MTEXT_COLUMN_INFO_BEGIN", "1070 75", "1070 2", "1070 79", "1070 0", "1070 76", "1070 2", "1070 78", "1070 0", "1070 48", "1040 50", "1070 49", "1040 10", "1070 50", "1070 2", "1040 15", "1040 25", "1000 ACAD_MTEXT_COLUMN_INFO_END", "1000 ACAD_MTEXT_DEFINED_HEIGHT_BEGIN", "1070 46", "1040 15", "1000 ACAD_MTEXT_DEFINED_HEIGHT_END" },
                again.GetXDataForApplication("ACAD").Select(g => g.TypeCode + " " + g.Value));
            Assert.Equal(50.0, again.Width);
            Assert.Equal(new[] { 15.0, 25.0 }, new[] { again.GetColumnHeight(0), again.GetColumnHeight(1) });
            Assert.Equal(0.0, again.Height);
        }

        [Fact]
        public void AnMTextInColumnsMadeInCode_TakesTheWidthOfTheFormOfTheDrawingItIsAddedTo()
        {
            // Point 10: AutoCAD 2021 read the columns 40 wide 10 apart copied into R2018 as 140 wide.
            foreach (string text in new[] { DxfFileEzdxfDrawings.R2000, DxfFileEzdxfDrawings.R2018 })
            {
                DxfDatabase db = DxfDatabase.Parse(text);
                var made = new DxfMText { Contents = "a\\Nb\\Nc", Location = P3(1, 2, 0), Width = 40.0 };
                made.SetStaticColumns(40.0, 10.0, 3);

                db.ModelSpace.AppendEntity(made);

                DxfMText again = ByHandle<DxfMText>(DxfDatabase.Parse(db.ToText()), made.Handle);
                Assert.Equal(text == DxfFileEzdxfDrawings.R2018 ? 140.0 : 40.0, again.Width);
                Assert.Equal(40.0, again.ColumnWidth);
            }
        }

        #endregion

        #region Point 32: the number of a viewport turned off

        [Fact]
        public void AViewportTurnedOff_HasTheNumberMinusOne_AsAutoCadGivesIt_AndKeepsItsIdForTheNewOnes()
        {
            // Plugin v1, 32: AutoCAD 2021 gave the viewport 94 turned off, of id 3, the number -1, and those on 1 and 2.
            DxfDatabase db = DxfDatabase.Parse(DxfObjectsEzdxfDrawings.R2018);
            DxfViewport off = ByHandle<DxfViewport>(db, "41");
            Assert.False(off.On);

            Assert.Equal(-1, off.Number);
            Assert.Equal(new[] { 1, 3, -1 }, db.Layouts[2].GetViewports().Select(v => v.Number));
            Assert.Equal("VIEWPORT 41 4, ", off.ToString().Substring(0, 15));

            // A viewport appended takes the first free id: 2, then 5, the id 4 of the one turned off kept.
            DxfBlockTableRecord sheet = db.Layouts[2].BlockTableRecord;
            var first = new DxfViewport { Width = 50, Height = 40 };
            var second = new DxfViewport { Width = 50, Height = 40 };
            first.On = true;
            second.On = true;
            sheet.AppendEntity(first);
            sheet.AppendEntity(second);
            Assert.Equal(new[] { 2, 5 }, new[] { first.Number, second.Number });

            // Turned on, it has its id again.
            off.On = true;
            Assert.Equal(4, off.Number);
        }

        #endregion

        #region Point 51: a MINSERT is not exploded and measures its first cell

        /// <summary>The MINSERTs of 51-b: a block of one circle of radius 3, 3 columns 10 apart by 2 rows 8 apart at the origin, and one cell at (60, 0).</summary>
        private static string Grids()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            var ring = new DxfBlockTableRecord { Name = "GH_RING" };
            db.BlockTable.Add(ring);
            ring.AppendEntity(new DxfCircle(P3(0, 0, 0), new GeoVector3(0, 0, 1), 3.0));
            db.ModelSpace.AppendEntity(new DxfMInsertBlock(P3(0, 0, 0), ring, 3, 2, 10.0, 8.0));
            db.ModelSpace.AppendEntity(new DxfMInsertBlock(P3(60, 0, 0), ring, 1, 1, 10.0, 8.0));
            return db.ToText();
        }

        [Fact]
        public void AMInsertOfOneCellOrOfSix_IsNotExplodedNorTreatedAsABlockReference_AsAutoCadRefusedThem()
        {
            // Plugin v8, 51: AutoCAD 2021's Explode() refused both MINSERTs (eNotApplicable), TreatAsBlockRefForExplode false.
            DxfDatabase db = DxfDatabase.Parse(Grids());
            DxfMInsertBlock[] grids = db.ModelSpace.OfType<DxfMInsertBlock>().Where(g => g.Name == "GH_RING").ToArray();

            Assert.Equal(new[] { 6, 1 }, grids.Select(g => g.Columns * g.Rows));

            foreach (DxfMInsertBlock grid in grids)
            {
                Assert.Throws<NotSupportedException>(() => grid.Explode());
                Assert.False(grid.TreatAsBlockRefForExplode);
            }

            Assert.True(ByHandle<DxfBlockReference>(db, "65").TreatAsBlockRefForExplode);
        }

        [Fact]
        public void AMInsert_MeasuresItsFirstCell_AndHasNoBestFit_AsAutoCadGaveIt_WhileTheDrawingTakesInEveryCell()
        {
            // Plugin v8, 51: GeometricExtents (-3, -3, 0) to (3, 3, 0) and (57, -3, 0) to (63, 3, 0); GeometryExtentsBestFit()
            // threw eInvalidExtents for both.
            DxfDatabase db = DxfDatabase.Parse(Grids());
            DxfMInsertBlock[] grids = db.ModelSpace.OfType<DxfMInsertBlock>().Where(g => g.Name == "GH_RING").ToArray();
            GeoPoint3[][] boxes = { new[] { P3(-3, -3, 0), P3(3, 3, 0) }, new[] { P3(57, -3, 0), P3(63, 3, 0) } };

            for (int k = 0; k < grids.Length; k++)
            {
                GeoAabb3 box = grids[k].GeometricExtents;
                Assert.True(boxes[k][0].IsEqualTo(box.Min, Tol), box.Min.ToString());
                Assert.True(boxes[k][1].IsEqualTo(box.Max, Tol), box.Max.ToString());
                Assert.Throws<InvalidOperationException>(() => grids[k].GeometryExtentsBestFit());
                Assert.Throws<InvalidOperationException>(() => grids[k].GeometryExtentsBestFit(GeoTransform3.Translation(new GeoVector3(0, 0, 1))));
            }

            // The extents of model space still take in all six cells: (23, 11) the far corner of the last.
            DxfDatabase alone = DxfDatabase.Parse(new DxfWriter().ToString());
            var ring = new DxfBlockTableRecord { Name = "GH_RING" };
            alone.BlockTable.Add(ring);
            ring.AppendEntity(new DxfCircle(P3(0, 0, 0), new GeoVector3(0, 0, 1), 3.0));
            alone.ModelSpace.AppendEntity(new DxfMInsertBlock(P3(0, 0, 0), ring, 3, 2, 10.0, 8.0));
            alone.UpdateExt(true);
            Assert.True(P3(23, 11, 0).IsEqualTo(alone.Extmax, Tol), alone.Extmax.ToString());
        }

        #endregion

        #region Point 26: the order of the properties in the header

        [Fact]
        public void ThePropertiesOfADrawingOfR2018_AreWrittenInTheOrderOfAutoCadsHeader_WhoSavedItLastBeforeTheRevision()
        {
            // Plugin v8, 26: AutoCAD 2021 saved 26-d as R2018 with $TITLE, $SUBJECT, $AUTHOR, $KEYWORDS, $COMMENTS, $LASTSAVEDBY,
            // $REVISIONNUMBER and the pairs, straight after $DWGCODEPAGE.
            DxfDatabase db = DxfDatabase.Parse(DxfObjectsEzdxfDrawings.R2018);
            string saver = db.GetHeader("$LASTSAVEDBY")[0].Value;

            db.SummaryInfo = new DxfDatabaseSummaryInfoBuilder(db.SummaryInfo) { Title = "Plate B-13" }.ToDatabaseSummaryInfo();

            string text = db.ToText();
            string[] names = text.Split('\n').Select(l => l.TrimEnd('\r')).Where(l => l.StartsWith("$", StringComparison.Ordinal)).ToArray();
            int at = Array.IndexOf(names, "$DWGCODEPAGE");
            Assert.Equal(
                new[] { "$TITLE", "$SUBJECT", "$AUTHOR", "$KEYWORDS", "$COMMENTS", "$LASTSAVEDBY", "$REVISIONNUMBER", "$CUSTOMPROPERTYTAG", "$CUSTOMPROPERTY", "$CUSTOMPROPERTYTAG", "$CUSTOMPROPERTY" },
                names.Skip(at + 1).Take(11));
            Assert.Equal(1, names.Count(n => n == "$LASTSAVEDBY"));
            Assert.Equal(saver, DxfDatabase.Parse(text).GetHeader("$LASTSAVEDBY")[0].Value);
            Assert.Equal("Plate B-13", DxfDatabase.Parse(text).SummaryInfo.Title);
        }

        #endregion

        #region Point 17f: a closed spline made a polyline

        /// <summary>
        /// The closed cubic (g) of 17f as the library wrote it in 17-f and AutoCAD read it, of no fit points: seven control
        /// points, the knots clamped, through (0, 0), (40, 0), (50, 30) and (10, 25).
        /// </summary>
        private static DxfSpline G17()
        {
            var points = new[]
            {
                P3(0.0000000000000016, 0.0, 0.0), P3(4.762265129346506, -9.875169480547258, 0.0), P3(44.57965357801522, -10.52612047361797, 0.0),
                P3(60.52497076467426, 41.58068210387638, 0.0), P3(8.562414603983974, 34.00936527765936, 0.0), P3(-3.205697822100078, 6.647426893891476, 0.0),
                P3(0.0000000000000016, 0.0, 0.0),
            };
            var knots = new[] { 0.0, 0.0, 0.0, 0.0, 40.0, 71.62277660168379, 111.9340653431765, 138.859889378849, 138.859889378849, 138.859889378849, 138.859889378849 };
            return new DxfSpline(3, false, true, true, points, knots, null, 0.0000000001, 0.000000001);
        }

        /// <summary>
        /// A periodic cubic as AutoCAD 2021 keeps one it fitted, read from a drawing: its control points and clamped knots as
        /// its DXF of v8 gives them, and the fit points it runs through, the first again at the end.
        /// </summary>
        private static DxfSpline Fitted(double[] knots, GeoPoint3[] points, GeoPoint3[] fit)
        {
            var record = new StringBuilder();
            record.Append(G(0, "SPLINE")).Append(G(5, "F00")).Append(G(330, "17")).Append(G(100, "AcDbEntity")).Append(G(8, "0")).Append(G(100, "AcDbSpline"))
                .Append(G(210, "0.0")).Append(G(220, "0.0")).Append(G(230, "1.0")).Append(G(70, "11")).Append(G(71, "3"))
                .Append(G(72, knots.Length.ToString(CultureInfo.InvariantCulture))).Append(G(73, points.Length.ToString(CultureInfo.InvariantCulture)))
                .Append(G(74, fit.Length.ToString(CultureInfo.InvariantCulture))).Append(G(42, "0.000000001")).Append(G(43, "0.0000000001")).Append(G(44, "0.0000000001"));

            foreach (double knot in knots)
            {
                record.Append(G(40, knot));
            }

            foreach (GeoPoint3 point in points)
            {
                record.Append(G(10, point.X)).Append(G(20, point.Y)).Append(G(30, point.Z));
            }

            foreach (GeoPoint3 point in fit)
            {
                record.Append(G(11, point.X)).Append(G(21, point.Y)).Append(G(31, point.Z));
            }

            string text = DxfFileEzdxfDrawings.R2018.Replace("\r\n", "\n").Replace("$HANDSEED\n  5\nA1\n", "$HANDSEED\n  5\nF01\n")
                .Replace("  0\nENDSEC\n  0\nSECTION\n  2\nOBJECTS\n", record + "  0\nENDSEC\n  0\nSECTION\n  2\nOBJECTS\n");
            return ByHandle<DxfSpline>(DxfDatabase.Parse(text), "F00");
        }

        /// <summary>The closed cubic (g) of 17n as AutoCAD 2021 fitted it in v7: the control points and knots of <see cref="G17"/>, and its fit points.</summary>
        internal static DxfSpline FittedG() => Fitted(
            new[] { 0.0, 0.0, 0.0, 0.0, 40.0, 71.62277660168379, 111.9340653431765, 138.859889378849, 138.859889378849, 138.859889378849, 138.859889378849 },
            Enumerable.Range(0, 7).Select(i => G17().GetControlPointAt(i)).ToArray(),
            new[] { P3(0, 0, 0), P3(40, 0, 0), P3(50, 30, 0), P3(10, 25, 0), P3(0, 0, 0) });

        // The closed cubic through (0, 0), (30, 0), (30, 15) and (20, 15), its knots 0, 30, 45, 55 and 80, the knot 30 halfway
        // between the quarters 20 and 40 (v8-17f-autocad-knot-halfway.dxf).
        private static DxfSpline Halfway() => Fitted(
            new[] { 0.0, 0, 0, 0, 30, 45, 55, 80, 80, 80, 80 },
            new[] { P3(0.0000000000000007, -0.0000000000000004, 0), P3(1.9696772796188, -6.041152263374485, 0), P3(33.61425167857916, -10.88065843621399, 0), P3(33.34423507327991, 17.56241426611796, 0), P3(14.65605371453325, 14.81893004115226, 0), P3(-1.641397733015665, 5.03429355281207, 0), P3(0.0000000000000007, -0.0000000000000004, 0) },
            new[] { P3(0, 0, 0), P3(30, 0, 0), P3(30, 15, 0), P3(20, 15, 0), P3(0, 0, 0) });

        // The closed cubic through (0, 0), (40, 0), (40.5, 0.5), (50, 30) and (10, 25), its knots 40 and 40.71 nearer each
        // other than a quarter of the range (v8-17f-autocad-near-knots.dxf).
        private static DxfSpline Near() => Fitted(
            new[] { 0.0, 0, 0, 0, 40, 40.70710678118654, 71.69904121581001, 112.0103299573027, 138.9361539929752, 138.9361539929752, 138.9361539929752, 138.9361539929752 },
            new[] { P3(0.0000000000000004, 0.0000000000000022, 0), P3(5.581908864552045, -9.637933498405571, 0), P3(30.82057656148167, -9.244936922688699, 0), P3(47.57923142560183, 7.619549683548036, 0), P3(61.79166624560641, 41.95179922457442, 0), P3(8.605513742194279, 34.03076806443996, 0), P3(-3.757437396752223, 6.487732536139555, 0), P3(0.0000000000000004, 0.0000000000000022, 0) },
            new[] { P3(0, 0, 0), P3(40, 0, 0), P3(40.5, 0.5, 0), P3(50, 30, 0), P3(10, 25, 0), P3(0, 0, 0) });

        /// <summary>Checks a polyline of a spline: its vertices, closed, at the parameters AutoCAD 2021's stood at.</summary>
        private static void AtParameters(DxfSpline spline, int precision, double[] parameters)
        {
            foreach (bool arcs in new[] { false, true })
            {
                var polyline = Assert.IsType<DxfPolyline>(spline.ToPolylineWithPrecision(precision, arcs, true));
                Assert.Equal(parameters.Length, polyline.NumberOfVertices);
                Assert.True(polyline.Closed);

                for (int i = 0; i < parameters.Length; i++)
                {
                    GeoPoint3 wanted = spline.GetPointAtParameter(parameters[i]);
                    Assert.True(new GeoPoint2(wanted.X, wanted.Y).IsEqualTo(polyline.GetPoint2dAt(i), Tight), "at " + precision + ", vertex " + i + ": " + polyline.GetPoint2dAt(i) + " for " + wanted);
                }
            }
        }

        [Fact]
        public void AClosedSplineOfNoFitPoints_IsTheHalvingAlone_AsAutoCadMadeItsPolylines()
        {
            // Plugin v8, 17f: AutoCAD 2021's polylines of 17-f's spline, of lines and of arcs alike, at 0 and 10.
            DxfSpline spline = G17();
            Assert.False(spline.HasFitData);
            var lines = Assert.IsType<DxfPolyline>(spline.ToPolylineWithPrecision(0, false, true));
            var arcs = Assert.IsType<DxfPolyline>(spline.ToPolylineWithPrecision(0, true, true));
            GeoPoint2[] wanted = { new GeoPoint2(0, 0), new GeoPoint2(34.424781683703785, -3.5056497866629588), new GeoPoint2(50.862106256808033, 28.401362515909977), new GeoPoint2(18.149698545928722, 30.492839988707654), new GeoPoint2(0, 0) };
            double[] bulges = { 0.41791605272607152, 0.39799678903684749, 0.4353614073572229, 0.40586110215633231, 0.0 };

            Assert.Equal(5, lines.NumberOfVertices);

            for (int i = 0; i < wanted.Length; i++)
            {
                Assert.True(wanted[i].IsEqualTo(lines.GetPoint2dAt(i), Tight), "vertex " + i + ": " + lines.GetPoint2dAt(i));
                Assert.True(wanted[i].IsEqualTo(arcs.GetPoint2dAt(i), Tight), "vertex " + i + ": " + arcs.GetPoint2dAt(i));
                Assert.Equal(bulges[i], arcs.GetBulgeAt(i), 12);
            }

            var ten = Assert.IsType<DxfPolyline>(spline.ToPolylineWithPrecision(10, false, true));
            var tens = new Dictionary<int, GeoPoint2>
            {
                [13] = new GeoPoint2(39.0628179731125, -0.70840975675177731),
                [25] = new GeoPoint2(50.010486694434761, 29.984211337996708),
                [38] = new GeoPoint2(9.1707320653758142, 24.255635948627003),
                [47] = new GeoPoint2(0, 0),
            };

            Assert.Equal(48, ten.NumberOfVertices);

            foreach (KeyValuePair<int, GeoPoint2> vertex in tens)
            {
                Assert.True(vertex.Value.IsEqualTo(ten.GetPoint2dAt(vertex.Key), Tight), "vertex " + vertex.Key + ": " + ten.GetPoint2dAt(vertex.Key));
            }
        }

        [Fact]
        public void AClosedSplineOfFitPoints_HasEachKnotInThePlaceOfTheLaterVertexAsNear_AsAutoCadPutIt()
        {
            // Plugin v8, 17f (HALFWAY): the knot 30, halfway between the quarters 20 and 40, stood at neither at 0: the knot
            // 45 took the place of 40, the later of the two.
            DxfSpline spline = Halfway();
            Assert.True(spline.HasFitData);

            AtParameters(spline, 0, new[] { 0.0, 20, 45, 55, 80 });
            AtParameters(spline, 10, new[]
            {
                0.0, 1.25, 2.5, 3.75, 5, 6.25, 7.5, 8.75, 10, 12.5, 15, 17.5, 20, 21.25, 22.5, 23.75, 25, 26.25, 27.5, 28.75, 30, 31.25, 32.5,
                33.75, 35, 36.25, 37.5, 38.75, 40, 41.25, 42.5, 43.75, 45, 46.25, 47.5, 48.75, 50, 51.25, 52.5, 55, 57.5, 60, 62.5, 65, 67.5,
                70, 71.25, 72.5, 73.75, 75, 76.25, 77.5, 78.75, 80,
            });
        }

        [Fact]
        public void AClosedSplineOfFitPoints_HasTheLaterOfTwoKnotsNearOneVertexThere_AsAutoCadPutIt()
        {
            // Plugin v8, 17f (NEAR): at 0 the knot 40.71 stood in the place of the quarter 34.73, where the knot 40 would.
            DxfSpline spline = Near();

            AtParameters(spline, 0, new[] { 0.0, 40.707106781186546, 71.699041215810013, 112.01032995730276, 138.93615399297528 });
            AtParameters(spline, 10, new[]
            {
                0.0, 2.1708774061402325, 4.3417548122804792, 6.5126322184207144, 8.6835096245609549, 10.854387030701195, 13.025264436841431,
                17.36701924912191, 21.708774061402394, 26.050528873682868, 30.392283685963349, 32.563161092103584, 34.734038498243827,
                36.904915904384048, 40, 40.707106781186546, 43.417548122804774, 47.759302935085252, 52.101057747365729, 56.442812559646214,
                60.784567371926684, 62.955444778066926, 65.126322184207169, 67.2971995903474, 69.468076996487639, 71.699041215810013,
                73.809831808768109, 75.980709214908359, 78.151586621048608, 80.322464027188829, 82.493341433329078, 84.6642188394693,
                86.835096245609577, 89.0059736517498, 91.176851057890019, 95.518605870170489, 99.860360682450988, 104.20211549473146,
                108.54387030701193, 112.01032995730276, 117.2273799315729, 119.39825733771313, 121.56913474385337, 123.74001214999362,
                125.91088955613385, 128.08176696227406, 130.25264436841434, 132.42352177455456, 134.59439918069478, 136.76527658683506,
                138.93615399297528,
            });
        }

        #endregion
    }
}
