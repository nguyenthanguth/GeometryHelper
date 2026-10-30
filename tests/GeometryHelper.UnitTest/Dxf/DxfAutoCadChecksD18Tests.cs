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
    /// What followed from AutoCAD 2021's answers to plugin run v10 of 30 October 2026: a range linked in the TABLECONTENT is
    /// linked at its top left cell alone, though every cell of it gives the range and the link (73h); an MTEXT in columns before
    /// R2018 is as wide as its columns and the gutters between them, though its group 41 holds a column's width (09e, 09a); and
    /// an MTEXT of R2000 in dynamic columns of their own heights is as high as its defined height (09a). Each drawing is one of
    /// ezdxf's, or built in the form AutoCAD wrote, read as a file is.
    /// </summary>
    public class DxfAutoCadChecksD18Tests
    {
        private static string G(int code, string value) => code.ToString(CultureInfo.InvariantCulture).PadLeft(3) + "\n" + value + "\n";

        private static DxfTable Table(DxfDatabase db) => DxfAutoCadChecksD16Tests.Table(db);

        private static string Record(string text, string handle) => DxfAutoCadChecksD16Tests.Record(text, handle);

        #region Point 73h: a range linked in the TABLECONTENT

        [Fact]
        public void ARangeLinkedInTheTableContent_IsLinkedAtItsTopLeftCellAlone_ThoughEveryCellGivesTheRange()
        {
            // Plugin v10, 73h: AutoCAD 2021 read the library's link over (1, 0) to (2, 1) with IsLinked True at (1, 0) alone and
            // False at (1, 1), (2, 0) and (2, 1), each of which gave the range (1, 0)-(2, 1) and the link 142.
            DxfDatabase db = DxfDatabase.Parse(DxfAutoCadChecksD16Tests.LinkedByAutoCad(DxfDimensionDrawings.R2018));
            DxfTable table = Table(db);
            var link = (DxfDataLink)db.DataLinkDictionary["GH_PARTS"];
            table.RemoveDataLink();

            table.SetDataLink(new DxfCellRange(1, 0, 2, 1), link);

            foreach (DxfTable read in new[] { table, Table(DxfDatabase.Parse(db.ToText())) })
            {
                Assert.True(read.IsLinked(1, 0));
                Assert.False(read.IsLinked(1, 1));
                Assert.False(read.IsLinked(2, 0));
                Assert.False(read.IsLinked(2, 1));
                Assert.False(read.IsLinked(0, 0));

                foreach (var (row, column) in new[] { (1, 0), (1, 1), (2, 0), (2, 1) })
                {
                    Assert.Equal(new DxfCellRange(1, 0, 2, 1), read.GetDataLinkRange(row, column));
                    Assert.Equal("GH_PARTS", read.GetDataLink(row, column).Name);
                }
            }
        }

        [Fact]
        public void ARangeLinked_LeavesTheOtherCellsOfTheTableContentAsAutoCadSavedThem()
        {
            // AutoCAD 2021's save of the library's link over (1, 0) to (2, 1) (v10-73h-resaved): the TABLECONTENT's cells (1, 1),
            // (2, 0) and (2, 1) of the state 0 and 92 0, as the library wrote them; the ACAD_TABLE's cells as the library wrote them.
            DxfDatabase db = DxfDatabase.Parse(DxfAutoCadChecksD16Tests.LinkedByAutoCad(DxfDimensionDrawings.R2018));
            DxfTable table = Table(db);
            table.RemoveDataLink();
            string before = db.ToText();

            table.SetDataLink(new DxfCellRange(1, 0, 2, 1), (DxfDataLink)db.DataLinkDictionary["GH_PARTS"]);

            string content = Record(db.ToText(), "B03");
            string[] cells = content.Split(new[] { G(1, "LINKEDTABLEDATACELL_BEGIN") }, StringSplitOptions.None).Skip(1).ToArray();
            Assert.Equal(6, cells.Length);

            foreach (int k in new[] { 3, 4, 5 })
            {
                Assert.StartsWith(G(90, "0"), cells[k], StringComparison.Ordinal);
                Assert.Contains(G(309, "DATAMAP_END") + G(92, "0") + G(95, "0"), cells[k], StringComparison.Ordinal);
                Assert.Equal(Record(before, "B03").Split(new[] { G(1, "LINKEDTABLEDATACELL_BEGIN") }, StringSplitOptions.None)[k + 1], cells[k]);
            }

            Assert.Equal(Record(before, "F6"), Record(db.ToText(), "F6"));
        }

        #endregion

        #region Points 09e and 09a: the width of an MTEXT in columns before R2018

        [Fact]
        public void AnMTextOfR2007InColumns_IsAsWideAsItsColumnsAndGutters_ThoughIts41IsAColumns()
        {
            // Plugin v10, 09e: AutoCAD 2021 read the library's R2007 MTEXTs 8D (two static columns 50 wide, a gutter of 10) and 8F
            // (two dynamic columns of their own heights) 110 wide, 8E (one dynamic column of auto height) 50; it wrote 41 50 on
            // each when it saved them. R2010 and R2013 keep the columns in the same extended data.
            foreach (string version in new[] { "AC1021", "AC1024", "AC1027" })
            {
                string text = DxfFileEzdxfDrawings.R2018.Replace("AC1032", version);
                DxfDatabase db = DxfDatabase.Parse(text);
                DxfMText mtext = ByHandle<DxfMText>(db, "73");

                mtext.SetStaticColumns(50.0, 10.0, 2);

                Assert.Equal(110.0, mtext.Width);
                Assert.Contains("\n 41\n50\n", Record(db.ToText(), "73"), StringComparison.Ordinal);
                DxfMText read = ByHandle<DxfMText>(DxfDatabase.Parse(db.ToText()), "73");
                Assert.Equal(110.0, read.Width);
                Assert.Equal(50.0, read.ColumnWidth);

                mtext.SetDynamicColumns(50.0, 10.0, true);
                Assert.Equal(110.0, mtext.Width);
                mtext.ColumnCount = 1;
                Assert.Equal(50.0, mtext.Width);
                mtext.ColumnAutoHeight = false;
                mtext.ColumnCount = 2;
                Assert.Equal(110.0, ByHandle<DxfMText>(DxfDatabase.Parse(db.ToText()), "73").Width);
                Assert.Contains("\n 41\n50\n", Record(db.ToText(), "73"), StringComparison.Ordinal);

                // Given back what it reads, it is written as it was.
                string saved = db.ToText();
                mtext.Width = mtext.Width;
                Assert.Equal(saved, db.ToText());
            }
        }

        [Fact]
        public void AnMTextOfR2018InColumns_AndOneOfR2007InNone_KeepTheWidthOfTheir41()
        {
            // AutoCAD 2021 keeps the width of all the columns in the 41 of R2018 (09-a), and an MTEXT of no columns wraps at it.
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            DxfMText mtext = ByHandle<DxfMText>(db, "73");
            mtext.SetStaticColumns(50.0, 10.0, 2);
            Assert.Equal(110.0, ByHandle<DxfMText>(DxfDatabase.Parse(db.ToText()), "73").Width);

            DxfMText plain = ByHandle<DxfMText>(DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018.Replace("AC1032", "AC1021")), "73");
            Assert.Equal(DxfColumnType.NoColumns, plain.ColumnType);
            Assert.Equal(40.0, plain.Width);
        }

        /// <summary>
        /// The R2000 drawing of ezdxf with the MTEXT 73 in columns as AutoCAD 2021 saved 09-a as R2000: its column data, the
        /// column MTEXTs after its first (B1 on, owned by the model space) linked by ACAD_MTEXT_COLUMNS, the extended data AutoCAD
        /// gives them, and its 41 the width of a column, 50.
        /// </summary>
        private static string InColumnsByAutoCad(string info, int count, string defined)
        {
            var columns = new StringBuilder();
            var links = new StringBuilder();

            for (int k = 1; k < count; k++)
            {
                string handle = (0xB0 + k).ToString("X", CultureInfo.InvariantCulture);
                links.Append(G(1005, handle));
                columns.Append(G(0, "MTEXT")).Append(G(5, handle)).Append(G(330, "17")).Append(G(100, "AcDbEntity")).Append(G(8, "0")).Append(G(100, "AcDbMText"))
                    .Append(G(10, (60.0 * k).ToString("R", CultureInfo.InvariantCulture))).Append(G(20, "40.0")).Append(G(30, "0.0")).Append(G(40, "3.0"))
                    .Append(G(41, "50.0")).Append(G(71, "1")).Append(G(1, "column")).Append(G(1001, "ACAD")).Append(info);
            }

            string data = "1001\nACAD\n" + info + G(1000, "ACAD_MTEXT_COLUMNS_BEGIN") + G(1070, "47") + G(1070, count.ToString(CultureInfo.InvariantCulture)) + links
                + G(1000, "ACAD_MTEXT_COLUMNS_END") + defined;
            return DxfFileEzdxfDrawings.R2000
                .Replace("\n 41\n40.0\n 71\n1\n  1\n{\\fArial|b1;Bold}\\Pline two é\n  0\n", "\n 41\n50.0\n 71\n1\n  1\n{\\fArial|b1;Bold}\\Pline two é\n" + data + columns + "  0\n");
        }

        private static string Info(params int[] whole)
        {
            var text = new StringBuilder(G(1000, "ACAD_MTEXT_COLUMN_INFO_BEGIN"));

            for (int i = 0; i < whole.Length; i += 2)
            {
                text.Append(G(1070, whole[i].ToString(CultureInfo.InvariantCulture)));
                text.Append(whole[i] == 48 || whole[i] == 49 ? G(1040, whole[i + 1].ToString(CultureInfo.InvariantCulture)) : G(1070, whole[i + 1].ToString(CultureInfo.InvariantCulture)));
            }

            return text.ToString();
        }

        [Fact]
        public void AnMTextOfR2000InColumnsAutoCadWrote_IsAsWideAsItsColumns_AndOfItsDefinedHeight()
        {
            // Plugin v10, 09a: AutoCAD 2021 read its own R2000 save of 09-a: 8D static, 2 columns, Width 110, Height 0; 8E dynamic of
            // auto height, 13 columns, Width 770, Height 0; 8F dynamic of its own heights 15, 25 and 0, 3 columns, Width 170,
            // Height 15, its ACAD_MTEXT_DEFINED_HEIGHT 15. Each column 50 wide, the gutters 10, the 41 of each 50.
            const string End = "1000\nACAD_MTEXT_COLUMN_INFO_END\n";
            const string Defined = "1000\nACAD_MTEXT_DEFINED_HEIGHT_BEGIN\n1070\n46\n1040\n15.0\n1000\nACAD_MTEXT_DEFINED_HEIGHT_END\n";
            var cases = new[]
            {
                (Info(75, 1, 79, 0, 76, 2, 78, 0, 48, 50, 49, 10) + End, 2, string.Empty, DxfColumnType.StaticColumns, 110.0, 0.0),
                (Info(75, 2, 79, 1, 76, 13, 78, 0, 48, 50, 49, 10) + End, 13, string.Empty, DxfColumnType.DynamicColumns, 770.0, 0.0),
                (Info(75, 2, 79, 0, 76, 3, 78, 0, 48, 50, 49, 10, 50, 3) + "1040\n15.0\n1040\n25.0\n1040\n0.0\n" + End, 3, Defined, DxfColumnType.DynamicColumns, 170.0, 15.0),
            };

            foreach (var (info, count, defined, type, width, height) in cases)
            {
                string text = InColumnsByAutoCad(info, count, defined);
                DxfDatabase db = DxfDatabase.Parse(text);
                DxfMText mtext = ByHandle<DxfMText>(db, "73");

                Assert.Equal(type, mtext.ColumnType);
                Assert.Equal(count, mtext.ColumnCount);
                Assert.Equal(50.0, mtext.ColumnWidth);
                Assert.Equal(10.0, mtext.ColumnGutterWidth);
                Assert.Equal(width, mtext.Width);
                Assert.Equal(height, mtext.Height);

                // Read so, it is written as it was.
                mtext.Width = mtext.Width;
                Assert.Equal(text, db.ToText());
            }
        }

        [Fact]
        public void AnMTextOfR2007InDynamicColumnsOfTheirOwnHeights_WithNo46_IsOfNoHeight_AsAutoCadReadIt()
        {
            // Plugin v10, 09e: AutoCAD 2021 read the library's R2007 MTEXT 8F, dynamic columns 15 and 25 high, its
            // ACAD_MTEXT_DEFINED_HEIGHT 15 and no group 46, as Height 0.
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018.Replace("AC1032", "AC1021"));
            DxfMText mtext = ByHandle<DxfMText>(db, "73");
            mtext.SetDynamicColumns(50.0, 10.0, false);
            mtext.ColumnCount = 2;
            mtext.SetColumnHeight(0, 15.0);
            mtext.SetColumnHeight(1, 25.0);

            string text = db.ToText();
            Assert.Contains("1000\nACAD_MTEXT_DEFINED_HEIGHT_BEGIN\n1070\n46\n1040\n15\n", text, StringComparison.Ordinal);
            Assert.DoesNotContain("\n 46\n", Record(text, "73"), StringComparison.Ordinal);
            Assert.Equal(0.0, ByHandle<DxfMText>(DxfDatabase.Parse(text), "73").Height);
        }

        #endregion
    }
}
