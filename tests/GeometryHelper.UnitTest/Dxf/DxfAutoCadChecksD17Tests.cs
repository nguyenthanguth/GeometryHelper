using System;
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
    /// What followed from AutoCAD 2021's answers to plugin run v9 of 30 October 2026: a formula AutoCAD kept in the TABLECONTENT
    /// alone is written in its cell when the link is taken away (78b); a link of the TABLECONTENT is read where its cell is the
    /// table's, whatever rows the TABLECONTENT has (73e); the column data of an MTEXT of R2000 with no column MTEXTs linked are
    /// no columns (09d); a cell is linked in the TABLECONTENT as AutoCAD links one (73d); a periodic spline AutoCAD keeps by its
    /// fit points has its knots in place, though its DXF holds none (17g). Each drawing is one of ezdxf's, or built in the form
    /// AutoCAD wrote, read as a file is.
    /// </summary>
    public class DxfAutoCadChecksD17Tests
    {
        private static readonly Tolerance Tight = new Tolerance(1E-9, 1E-9, 1E-9);

        private static GeoPoint3 P3(double x, double y, double z) => new GeoPoint3(x, y, z);

        private static string G(int code, string value) => code.ToString(CultureInfo.InvariantCulture).PadLeft(3) + "\n" + value + "\n";

        private static string G(int code, double value) => G(code, value.ToString("R", CultureInfo.InvariantCulture));

        private static DxfTable Table(DxfDatabase db) => DxfAutoCadChecksD16Tests.Table(db);

        private static string Record(string text, string handle) => DxfAutoCadChecksD16Tests.Record(text, handle);

        private static string Linked(int rows = 3, bool formula = false) => DxfAutoCadChecksD16Tests.LinkedByAutoCad(DxfDimensionDrawings.R2018, rows, formula);

        #region Point 78b: a formula of the TABLECONTENT alone, its link taken away

        [Fact]
        public void AFormulaAutoCadKeptInTheTableContentAlone_IsWrittenInItsCell_WhenTheLinkIsTakenAway()
        {
            // Plugin v9, 78b: AutoCAD 2021 read no formula in the cell (1, 0) of its 78 once the library had taken the link away,
            // its 344 192 naming no record; the formula the library wrote in a cell of its own (78c) it read.
            foreach (bool one in new[] { false, true })
            {
                DxfDatabase db = DxfDatabase.Parse(Linked(3, true));
                DxfTable table = Table(db);

                if (one)
                {
                    table.RemoveDataLink(1, 0);
                }
                else
                {
                    table.RemoveDataLink();
                }

                string saved = db.ToText();
                DxfDatabase again = DxfDatabase.Parse(saved);
                string handle = FieldOfMark(Record(saved, "F6"));
                Assert.NotEqual("B09", handle);
                string field = Record(saved, handle);
                Assert.Contains(G(330, "F6") + G(100, "AcDbField") + G(1, "AcExpr") + G(2, "\\AcExpr (1+2)"), field, StringComparison.Ordinal);
                Assert.Equal("=1+2", Table(again).GetFormula(1, 0, 0));
                Assert.Empty(Table(again).GetDataLink());

                // The TABLECONTENT keeps its field of text, as AutoCAD wrote it.
                Assert.Contains(G(5, "B07"), saved, StringComparison.Ordinal);
                Assert.Contains(G(5, "B08"), saved, StringComparison.Ordinal);
            }
        }

        [Fact]
        public void AFormulaOfACellOfItsOwn_IsLeftAsItIs_WhenTheLinkIsTakenAway()
        {
            // A cell whose 344 names a record of the file keeps its field: only one AutoCAD wrote in the TABLECONTENT alone is given one.
            DxfDatabase db = DxfDatabase.Parse(Linked(3, true));
            DxfTable table = Table(db);
            table.SetFormula(1, 0, 0, "=2+3");
            string set = FieldOfMark(Record(db.ToText(), "F6"));

            table.RemoveDataLink();

            Assert.Equal(set, FieldOfMark(Record(db.ToText(), "F6")));
            Assert.Equal("=2+3", Table(DxfDatabase.Parse(db.ToText())).GetFormula(1, 0, 0));
        }

        /// <summary>The handle the cell (1, 0) Mark of the table's record points its field at (the 344 before its 92).</summary>
        private static string FieldOfMark(string record)
        {
            int at = record.IndexOf("\n 92\n0\n301\nCELL_VALUE\n 93\n6\n 90\n4\n  1\nMark\n", StringComparison.Ordinal);
            string before = record.Substring(0, at);
            int field = before.LastIndexOf("344\n", StringComparison.Ordinal);
            return before.Substring(field + 4).Split('\n')[0];
        }

        #endregion

        #region Point 73e: a link of a TABLECONTENT of other rows than its table

        [Fact]
        public void ALinkOfATableContentOfFewerRowsThanItsTable_IsReadAtItsCell_AsAutoCadReadIt()
        {
            // Plugin v9, 73e: AutoCAD 2021 opened the table of four rows whose TABLECONTENT had three and kept the link at (1, 0).
            DxfDatabase db = DxfDatabase.Parse(Linked(2));
            DxfTable table = Table(db);
            var link = (DxfDataLink)db.DataLinkDictionary["GH_PARTS"];

            Assert.Equal(3, table.NumRows);
            Assert.True(table.IsLinked(1, 0));
            Assert.Equal(new DxfCellRange(1, 0, 1, 0), table.GetDataLinkRange(1, 0));
            Assert.Equal(new[] { table }, link.GetTargets());

            // A row put in after it leaves the link where it was, read by its cell; taken away, it is gone as AutoCAD took it.
            DxfDatabase grown = DxfDatabase.Parse(Linked(3));
            Table(grown).InsertRows(3, 8.0, 1);
            Assert.Equal(4, Table(grown).NumRows);
            Assert.True(Table(grown).IsLinked(1, 0));
            Table(grown).RemoveDataLink();
            Assert.Empty(Table(DxfDatabase.Parse(grown.ToText())).GetDataLink());
        }

        [Fact]
        public void ALinkOfTheTableContentPastTheTablesLastRow_NamesNoCell()
        {
            DxfDatabase db = DxfDatabase.Parse(Linked(3));
            DxfTable table = Table(db);

            table.DeleteRows(1, 2);

            Assert.Equal(1, table.NumRows);
            Assert.Empty(table.GetDataLink());
            Assert.Empty(((DxfDataLink)db.DataLinkDictionary["GH_PARTS"]).GetTargets());
        }

        #endregion

        #region Point 73d: a cell linked in the TABLECONTENT

        [Fact]
        public void ACellLinkedAgain_IsNamedInTheTableContentAndTiesTheDataLink_AsAutoCadWroteIt()
        {
            // Plugin v3 and v9, 73: AutoCAD 2021 linked (1, 0) with 90 4, 92 1, 340 142, 93 1, 94 1, 96 1 in the TABLECONTENT,
            // the DATALINK tied by 94 2, 330 F6, 330 of the TABLECONTENT; its link taken away and set again, the file is AutoCAD's.
            string text = Linked();
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfTable table = Table(db);
            var link = (DxfDataLink)db.DataLinkDictionary["GH_PARTS"];

            table.RemoveDataLink();
            table.SetDataLink(1, 0, link);

            Assert.Equal(text, db.ToText());
            Assert.True(table.IsLinked(1, 0));
            Assert.Equal(new[] { table }, link.GetTargets());

            // Linked so already, it is written no more.
            table.SetDataLink(new DxfCellRange(1, 0, 1, 0), link);
            Assert.Equal(text, db.ToText());
        }

        [Fact]
        public void ARangeLinked_IsNamedByItsTopLeftCell_WithItsRowsAndColumns()
        {
            DxfDatabase db = DxfDatabase.Parse(Linked());
            DxfTable table = Table(db);
            var link = (DxfDataLink)db.DataLinkDictionary["GH_PARTS"];
            table.RemoveDataLink();

            table.SetDataLink(new DxfCellRange(1, 0, 2, 1), link);

            string saved = db.ToText();
            Assert.Contains(G(92, "1") + G(340, "B06") + G(93, "2") + G(94, "2") + G(96, "1") + G(95, "0"), Record(saved, "B03"), StringComparison.Ordinal);
            Assert.Contains(G(304, string.Empty) + G(94, "2") + G(330, "F6") + G(330, "B03") + G(360, "0"), Record(saved, "B06"), StringComparison.Ordinal);
            DxfTable again = Table(DxfDatabase.Parse(saved));
            Assert.Equal(new DxfCellRange(1, 0, 2, 1), again.GetDataLinkRange(2, 1));
            Assert.False(again.IsLinked(0, 1));
        }

        [Fact]
        public void ALinkSet_IsRefusedWhereTheTableCannotHoldIt()
        {
            string text = Linked();
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfTable table = Table(db);
            var link = (DxfDataLink)db.DataLinkDictionary["GH_PARTS"];

            // A range that meets one linked already, a link of another drawing, a cell outside the table, none.
            Assert.Throws<InvalidOperationException>(() => table.SetDataLink(new DxfCellRange(0, 0, 1, 1), link));
            Assert.Throws<ArgumentException>(() => table.SetDataLink(2, 1, (DxfDataLink)DxfDatabase.Parse(text).DataLinkDictionary["GH_PARTS"]));
            Assert.Throws<ArgumentOutOfRangeException>(() => table.SetDataLink(3, 0, link));
            Assert.Throws<ArgumentOutOfRangeException>(() => table.SetDataLink(new DxfCellRange(2, 0, 2, 2), link));
            Assert.Throws<ArgumentNullException>(() => table.SetDataLink(2, 1, null));
            Assert.Throws<ArgumentNullException>(() => table.SetDataLink(null, link));
            Assert.Equal(text, db.ToText());

            // A table of no TABLECONTENT, as a program other than AutoCAD writes one.
            DxfDatabase bare = DxfDatabase.Parse(text.Replace("  0\nACAD_TABLE\n  5\nF6\n102\n{ACAD_XDICTIONARY\n360\nB01\n102\n}\n", "  0\nACAD_TABLE\n  5\nF6\n"));
            Assert.Throws<InvalidOperationException>(() => Table(bare).SetDataLink(2, 1, (DxfDataLink)bare.DataLinkDictionary["GH_PARTS"]));
        }

        #endregion

        #region Point 09d: the column data of an MTEXT of R2000

        /// <summary>
        /// The R2000 drawing of ezdxf with the MTEXT 73 given the column data the library wrote in 09-d, with no column MTEXTs
        /// linked: as given.
        /// </summary>
        private static string WithColumnData(string data) => DxfFileEzdxfDrawings.R2000.Replace("line two é\n  0\n", "line two é\n1001\nACAD\n" + data + "  0\n");

        private static string Data(params object[] groups)
        {
            var text = new StringBuilder();

            for (int i = 0; i < groups.Length; i += 2)
            {
                text.Append(Convert.ToString(groups[i], CultureInfo.InvariantCulture)).Append('\n').Append(Convert.ToString(groups[i + 1], CultureInfo.InvariantCulture)).Append('\n');
            }

            return text.ToString();
        }

        [Fact]
        public void TheColumnDataOfAnMTextOfR2000_WithNoColumnMTextsLinked_AreNoColumns_AsAutoCadReadThem()
        {
            // Plugin v9, 09d: AutoCAD 2021 read 8D (two static columns 50 wide, a gutter of 10), 8E (dynamic, of auto height) and
            // 8F (dynamic, 15 and 25 high, a defined height of 15) as of no columns, Width 50, Height 0, 0 and 15.
            string info = "1000\nACAD_MTEXT_COLUMN_INFO_BEGIN\n";
            string end = "1000\nACAD_MTEXT_COLUMN_INFO_END\n";
            string[] data =
            {
                info + Data(1070, 75, 1070, 1, 1070, 79, 1070, 0, 1070, 76, 1070, 2, 1070, 78, 1070, 0, 1070, 48, 1040, 50, 1070, 49, 1040, 10) + end,
                info + Data(1070, 75, 1070, 2, 1070, 79, 1070, 1, 1070, 76, 1070, 1, 1070, 78, 1070, 0, 1070, 48, 1040, 50, 1070, 49, 1040, 10) + end,
                info + Data(1070, 75, 1070, 2, 1070, 79, 1070, 0, 1070, 76, 1070, 2, 1070, 78, 1070, 0, 1070, 48, 1040, 50, 1070, 49, 1040, 10, 1070, 50, 1070, 2, 1040, 15, 1040, 25) + end
                    + "1000\nACAD_MTEXT_DEFINED_HEIGHT_BEGIN\n1070\n46\n1040\n15\n1000\nACAD_MTEXT_DEFINED_HEIGHT_END\n",
            };
            double[] heights = { 0.0, 0.0, 15.0 };

            for (int k = 0; k < data.Length; k++)
            {
                string text = WithColumnData(data[k]).Replace("\n 41\n10.0\n", "\n 41\n50.0\n");
                DxfDatabase db = DxfDatabase.Parse(text);
                DxfMText mtext = ByHandle<DxfMText>(db, "73");

                Assert.Equal(DxfColumnType.NoColumns, mtext.ColumnType);
                Assert.Equal(0, mtext.ColumnCount);
                Assert.Equal(0.0, mtext.ColumnWidth);
                Assert.Equal(0.0, mtext.GetColumnHeight(0));
                Assert.Equal(heights[k], mtext.Height);

                // Read so, it is written as it was; the column data AutoCAD keeps as it read them.
                mtext.Width = mtext.Width;
                mtext.Contents = mtext.Contents;
                Assert.Equal(text, db.ToText());
            }
        }

        [Fact]
        public void ColumnsGivenToAnMTextOfR2000_WithNoColumnMTextsLinked_AreRefused()
        {
            // AutoCAD 2021 writes the columns of R2000 with an MTEXT of its own for each column after the first, which the
            // library does not make; the column data alone it reads as no columns.
            string text = DxfFileEzdxfDrawings.R2000;
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfMText mtext = ByHandle<DxfMText>(db, "73");

            Assert.Throws<InvalidOperationException>(() => mtext.SetStaticColumns(50.0, 10.0, 2));
            Assert.Throws<InvalidOperationException>(() => mtext.SetDynamicColumns(50.0, 10.0, true));
            Assert.Throws<InvalidOperationException>(() => mtext.ColumnType = DxfColumnType.StaticColumns);
            mtext.ColumnType = DxfColumnType.NoColumns;
            Assert.Equal(text, db.ToText());

            // In R2018, and in an MTEXT of R2000 whose later columns are MTEXTs of their own, they are set.
            DxfMText r2018 = ByHandle<DxfMText>(DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018), "73");
            r2018.SetStaticColumns(50.0, 10.0, 2);
            Assert.Equal(DxfColumnType.StaticColumns, r2018.ColumnType);
        }

        #endregion

        #region Point 17g: a periodic spline AutoCAD keeps by its fit points

        /// <summary>
        /// A periodic cubic as AutoCAD 2021 saved one it fitted (v8-17f-autocad-knot-halfway.dxf and -near-knots.dxf): its 70 of
        /// 3115, the flag 1024 among them, its control points and clamped knots, and no fit points (74 0).
        /// </summary>
        private static DxfSpline SavedByAutoCad(int flags, double[] knots, GeoPoint3[] points)
        {
            var record = new StringBuilder();
            record.Append(G(0, "SPLINE")).Append(G(5, "F00")).Append(G(330, "17")).Append(G(100, "AcDbEntity")).Append(G(8, "0")).Append(G(100, "AcDbSpline"))
                .Append(G(210, "0.0")).Append(G(220, "0.0")).Append(G(230, "1.0")).Append(G(70, flags.ToString(CultureInfo.InvariantCulture))).Append(G(71, "3"))
                .Append(G(72, knots.Length.ToString(CultureInfo.InvariantCulture))).Append(G(73, points.Length.ToString(CultureInfo.InvariantCulture)))
                .Append(G(74, "0")).Append(G(42, "0.000000001")).Append(G(43, "0.0000000001"));

            foreach (double knot in knots)
            {
                record.Append(G(40, knot));
            }

            foreach (GeoPoint3 point in points)
            {
                record.Append(G(10, point.X)).Append(G(20, point.Y)).Append(G(30, point.Z));
            }

            string text = DxfFileEzdxfDrawings.R2018.Replace("\r\n", "\n").Replace("$HANDSEED\n  5\nA1\n", "$HANDSEED\n  5\nF01\n")
                .Replace("  0\nENDSEC\n  0\nSECTION\n  2\nOBJECTS\n", record + "  0\nENDSEC\n  0\nSECTION\n  2\nOBJECTS\n");
            return ByHandle<DxfSpline>(DxfDatabase.Parse(text), "F00");
        }

        private static readonly double[] HalfwayKnots = { 0.0, 0, 0, 0, 30, 45, 55, 80, 80, 80, 80 };

        private static readonly GeoPoint3[] HalfwayPoints =
        {
            P3(0.0000000000000007, -0.0000000000000004, 0), P3(1.9696772796188, -6.041152263374485, 0), P3(33.61425167857916, -10.88065843621399, 0),
            P3(33.34423507327991, 17.56241426611796, 0), P3(14.65605371453325, 14.81893004115226, 0), P3(-1.641397733015665, 5.03429355281207, 0),
            P3(0.0000000000000007, -0.0000000000000004, 0),
        };

        [Fact]
        public void APeriodicSplineAutoCadKeepsByItsFitPoints_HasItsKnotsInPlace_ThoughItsDxfHoldsNone()
        {
            // Plugin v9, 17g: AutoCAD 2021 read its own HALFWAY again, periodic of 5 fit points, and put the knots in place at 0
            // (0, 20, 45, 55, 80), as for the spline it fitted in v8; NEAR at 0 at 0, 40.71, 71.70, 112.01 and its end.
            DxfSpline halfway = SavedByAutoCad(3115, HalfwayKnots, HalfwayPoints);
            Assert.False(halfway.HasFitData);

            DxfAutoCadChecksD16Tests.AtParameters(halfway, 0, new[] { 0.0, 20, 45, 55, 80 });

            DxfSpline near = SavedByAutoCad(
                3115,
                new[] { 0.0, 0, 0, 0, 40, 40.70710678118654, 71.69904121581001, 112.0103299573027, 138.9361539929752, 138.9361539929752, 138.9361539929752, 138.9361539929752 },
                new[] { P3(0.0000000000000004, 0.0000000000000022, 0), P3(5.581908864552045, -9.637933498405571, 0), P3(30.82057656148167, -9.244936922688699, 0), P3(47.57923142560183, 7.619549683548036, 0), P3(61.79166624560641, 41.95179922457442, 0), P3(8.605513742194279, 34.03076806443996, 0), P3(-3.757437396752223, 6.487732536139555, 0), P3(0.0000000000000004, 0.0000000000000022, 0) });

            DxfAutoCadChecksD16Tests.AtParameters(near, 0, new[] { 0.0, 40.70710678118654, 71.69904121581001, 112.0103299573027, 138.9361539929752 });
        }

        [Fact]
        public void AClosedSplineAutoCadKeepsByItsControlPoints_IsTheHalvingAlone()
        {
            // AutoCAD 2021's closed spline that is not periodic, its 70 of 11 with no flag 1024 (v8-17f-autocad-closed-clamped.dxf):
            // the halving into quarters at 0.
            DxfSpline closed = SavedByAutoCad(11, HalfwayKnots, HalfwayPoints);

            DxfAutoCadChecksD16Tests.AtParameters(closed, 0, new[] { 0.0, 20, 40, 60, 80 });
        }

        #endregion
    }
}
