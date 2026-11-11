using System;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// What followed from AutoCAD 2021's answers to the D28d runs of 1 November 2026 (headless, AutoCAD's own edits read in
    /// memory and saved, its files and the library's read back): the title merged again over columns put in at its edge, the
    /// height of a row SetSize puts in, the kind of a row put in at the title or the header, the looks and merges of a row
    /// that takes the look of the title or the header, and the margins of a table of none of its own. Each drawing is built
    /// in the form AutoCAD wrote, read as a file is.
    /// </summary>
    public class DxfTableD28dTests
    {
        private static DxfTable Table(DxfDatabase db) => DxfAutoCadChecksD16Tests.Table(db);

        private static DxfTable Reread(DxfDatabase db) => Table(DxfDatabase.Parse(db.ToText()));

        private static string Record(string text, string handle) => DxfAutoCadChecksD16Tests.Record(text, handle);

        private static DxfColor Aci(short index) => DxfColor.FromColorIndex(DxfColorMethod.ByAci, index);

        /// <summary>
        /// The R2018 drawing whose table names a table style of its own, Parts: the data of the text height given, the title 6 and
        /// the header 4.5, as AutoCAD's Standard, of the margins given (40, 41).
        /// </summary>
        private static string Styled(string dataHeight, string horizontal, string vertical)
        {
            string style =
                "  0\nTABLESTYLE\n  5\n200\n330\n15\n100\nAcDbTableStyle\n280\n0\n  3\nParts\n 70\n0\n 71\n0\n 40\n"
                + horizontal
                + "\n 41\n"
                + vertical
                + "\n280\n0\n281\n0\n"
                + "  7\nStandard\n140\n"
                + dataHeight
                + "\n170\n2\n 62\n0\n 63\n7\n283\n0\n 90\n512\n 91\n0\n"
                + "  7\nStandard\n140\n6.0\n170\n5\n 62\n0\n 63\n7\n283\n0\n 90\n512\n 91\n0\n"
                + "  7\nStandard\n140\n4.5\n170\n5\n 62\n0\n 63\n7\n283\n0\n 90\n512\n 91\n0\n";
            string text = DxfDimensionDrawings.R2018.Replace("\r\n", "\n");
            const string Dictionary = "  0\nDICTIONARY\n  5\n15\n330\nA\n100\nAcDbDictionary\n281\n1\n";
            const string Head = "100\nAcDbTable\n280\n0\n343\nE6\n";
            Assert.Contains(Dictionary, text, StringComparison.Ordinal);
            Assert.Contains(Head, text, StringComparison.Ordinal);
            return text.Replace(Dictionary, Dictionary + "  3\nParts\n350\n200\n" + style)
                .Replace(Head, "100\nAcDbTable\n280\n0\n342\n200\n343\nE6\n")
                .Replace("$HANDSEED\n  5\n10A\n", "$HANDSEED\n  5\n201\n");
        }

        /// <summary>
        /// The R2018 drawing whose table holds, in its head where AutoCAD 2021 wrote them (D28c o0), its own header and data of
        /// 9, its data GH_TXT, red and top left, and its header's background green; its cells hold none of them of their own.
        /// </summary>
        private static string RowLooks()
        {
            const string Head = " 93\n0\n 94\n0\n 95\n0\n 96\n0\n141\n10.0\n";
            Assert.Contains(Head, DxfDimensionDrawings.R2018, StringComparison.Ordinal);
            return DxfDimensionDrawings.R2018.Replace(
                Head,
                " 93\n6886016\n 94\n0\n 95\n0\n 96\n0\n  7\nGH_TXT\n140\n9.0\n170\n1\n 63\n3\n 64\n1\n140\n9.0\n283\n0\n141\n10.0\n"
            );
        }

        /// <summary>The table of the R2018 drawing with the lines AutoCAD 2021's D28d g0 set: the title's top red, the line under it yellow at Mark and green at Qty.</summary>
        private static DxfDatabase Lines()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
            DxfTable table = Table(db);
            table.SetGridColor(0, 0, DxfGridLineType.HorizontalTop, Aci(1));
            table.SetGridColor(1, 0, DxfGridLineType.HorizontalTop, Aci(2));
            table.SetGridColor(1, 1, DxfGridLineType.HorizontalTop, Aci(3));
            return DxfDatabase.Parse(db.ToText());
        }

        #region The title merged again

        [Fact]
        public void AColumnPutInAtTheTitlesEdge_MergesItAgain_ItsOtherCellsEmptiedUnderItsTopLine_AndTheLineUnderItTheTitles()
        {
            // D28d g1, g2, g4, g5 (and b1, D28c i5): AutoCAD 2021's InsertColumns, InsertColumnsAndInherit and SetSize of columns
            // put in after the title's last column, or before its first, merged the title anew: its other cells of no value
            // (91 0, 93 7, 90 0), each under the title's top line, and the line under it the title's bottom (yellow, where Qty's
            // top was green) across every column, the new ones too. The library grew the title, (0, 1) still of an empty text
            // and the line under Qty green.
            foreach (
                Action<DxfTable> put in new Action<DxfTable>[]
                {
                    t => t.InsertColumns(2, 20.0, 1),
                    t => t.SetSize(3, 3),
                    t => t.InsertColumnsAndInherit(2, 1, 1),
                }
            )
            {
                DxfDatabase db = Lines();
                put(Table(db));
                DxfTable read = Reread(db);

                Assert.Equal(new DxfCellRange(0, 0, 0, 2), read.GetMergeRange(0, 1));
                Assert.Equal(DxfDataType.Unknown, read.GetDataType(0, 1, 0).DataType);
                Assert.Equal(2, read.GetGridColor(1, 1, DxfGridLineType.HorizontalTop).ColorIndex);
                Assert.Equal(2, read.GetGridColor(1, 2, DxfGridLineType.HorizontalTop).ColorIndex);
                Assert.Equal(1, read.GetGridColor(0, 2, DxfGridLineType.HorizontalTop).ColorIndex);
                Assert.Equal("Parts", read.TextString(0, 0));
            }

            // D28d g2: before its first column alike, Mark and Qty moved on under the yellow line.
            DxfDatabase first = Lines();
            Table(first).InsertColumns(0, 20.0, 1);
            DxfTable before = Reread(first);
            Assert.Equal("Parts", before.TextString(0, 0));
            Assert.Equal(2, before.GetGridColor(1, 0, DxfGridLineType.HorizontalTop).ColorIndex);
            Assert.Equal(2, before.GetGridColor(1, 2, DxfGridLineType.HorizontalTop).ColorIndex);
            Assert.Equal(DxfDataType.Unknown, before.GetDataType(0, 1, 0).DataType);

            // D28d g3: a column put in within the title grows it as it is, Qty's green line kept, the new cell under none.
            DxfDatabase within = Lines();
            Table(within).InsertColumns(1, 20.0, 1);
            DxfTable grown = Reread(within);
            Assert.Equal(3, grown.GetGridColor(1, 2, DxfGridLineType.HorizontalTop).ColorIndex);
            Assert.Null(grown.GetGridColor(1, 1, DxfGridLineType.HorizontalTop));
            Assert.Equal(DxfDataType.String, grown.GetDataType(0, 2, 0).DataType);
        }

        [Fact]
        public void AColumnThatTakesTheLookOfAnother_TakesItsMergesDownTheColumn_AndARowItsMergesAcrossTheRow()
        {
            // D28d m2: AutoCAD 2021's InsertColumnsAndInherit(2, 1, 1) of its table of (1, 1) merged down to (2, 1) merged the new
            // (1, 2) down to (2, 2); m1 and m3, its InsertRowsAndInherit of its row 2 merged across merged the new row alike.
            DxfDatabase down = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
            Table(down).MergeCells(new DxfCellRange(1, 1, 2, 1));
            Table(down).InsertColumnsAndInherit(2, 1, 1);
            Assert.Equal(new DxfCellRange(1, 2, 2, 2), Reread(down).GetMergeRange(2, 2));
            Assert.Equal(new DxfCellRange(1, 1, 2, 1), Reread(down).GetMergeRange(1, 1));

            foreach (int at in new[] { 3, 2 })
            {
                DxfDatabase across = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
                Table(across).MergeCells(new DxfCellRange(2, 0, 2, 1));
                Table(across).InsertRowsAndInherit(at, 2, 1);
                DxfTable read = Reread(across);
                Assert.Equal(new DxfCellRange(2, 0, 2, 1), read.GetMergeRange(2, 1));
                Assert.Equal(new DxfCellRange(3, 0, 3, 1), read.GetMergeRange(3, 1));
            }
        }

        #endregion

        #region The height of a new row

        [Fact]
        public void ARowSetSizePutsIn_IsFourThirdsOfTheDataTextHigh_AndTheVerticalMarginTwice()
        {
            // D28d h1 to h9, o1: AutoCAD 2021's SetSize made each new row 4/3 of its table style's data text height and twice the
            // vertical margin, the table's own where it has one: 9 for its Standard of 4.5 and 1.5 under rows of 15 or 20, 12 for
            // its own margin of 3, 6.333333333333333 for a style of 2.5, 8 for a style's margin of 1; whatever the table's own
            // text heights. The library made each as high as the last row.
            DxfDatabase db = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
            Table(db).SetSize(5, 2);
            DxfTable read = Reread(db);
            Assert.Equal(9.0, read.RowHeight(3));
            Assert.Equal(9.0, read.RowHeight(4));
            Assert.Equal(8.0, read.RowHeight(2));

            DxfDatabase small = DxfDatabase.Parse(Styled("2.5", "1.5", "1.5"));
            Table(small).SetSize(4, 2);
            Assert.Contains("141\n6.333333333333333\n", Record(small.ToText(), "F6"), StringComparison.Ordinal);

            DxfDatabase margin = DxfDatabase.Parse(Styled("4.5", "1.5", "1.0"));
            Table(margin).SetSize(4, 2);
            Assert.Equal(8.0, Reread(margin).RowHeight(3));

            const string Head = " 93\n0\n 94\n0\n 95\n0\n 96\n0\n141\n10.0\n";
            DxfDatabase own = DxfDatabase.Parse(
                DxfDimensionDrawings.R2018.Replace(Head, " 93\n16\n 94\n0\n 95\n0\n 96\n0\n 41\n3.0\n141\n10.0\n")
            );
            Table(own).SetSize(4, 2);
            Assert.Equal(12.0, Reread(own).RowHeight(3));

            DxfDatabase looks = DxfDatabase.Parse(RowLooks());
            Table(looks).SetSize(4, 2);
            Assert.Equal(9.0, Reread(looks).RowHeight(3));
        }

        #endregion

        #region Rows put in at the title or the header

        [Fact]
        public void ARowPutInAtTheTitleOrTheHeader_IsData_AndTheRowsItMovesOnKeepTheirLooksAsTheirOwn()
        {
            // D28d r1, r6 (D28c m7): AutoCAD 2021's InsertRows(0) left the title and the header out (93 3, 280 1, 281 1), every
            // row data, and gave the title's cells 170 5 and 140 6.0, the header's 170 5, its style's looks of them that are not
            // its data's. The library read the new row as the title.
            DxfDatabase db = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
            Table(db).InsertRows(0, 9.0, 1);
            DxfTable read = Reread(db);
            Assert.True(read.IsTitleSuppressed);
            Assert.True(read.IsHeaderSuppressed);
            Assert.Equal(DxfRowType.DataRow, read.RowType(0));
            Assert.Equal(DxfRowType.DataRow, read.RowType(1));
            Assert.Equal(6.0, read.TextHeight(1, 0));
            Assert.Equal(DxfCellAlignment.MiddleCenter, read.Alignment(1, 0));
            Assert.Equal(DxfCellAlignment.MiddleCenter, read.Alignment(2, 1));
            Assert.Null(read.TextHeight(2, 1));
            Assert.Null(read.Alignment(3, 0));
            Assert.Equal("Parts", read.TextString(1, 0));

            // D28d r2: InsertRows(1) left the header out (93 2, 281 1), the header's cells given 170 5; r4, of a header left out
            // already, nothing more.
            DxfDatabase one = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
            Table(one).InsertRows(1, 9.0, 1);
            DxfTable second = Reread(one);
            Assert.False(second.IsTitleSuppressed);
            Assert.True(second.IsHeaderSuppressed);
            Assert.Equal(DxfRowType.DataRow, second.RowType(1));
            Assert.Equal(DxfCellAlignment.MiddleCenter, second.Alignment(2, 1));
            Assert.Null(second.Alignment(1, 1));
            Assert.Contains(" 93\n2\n", Record(one.ToText(), "F6"), StringComparison.Ordinal);

            // D28d r5: the title left out, InsertRows(0) left the header out, its cells given the header's look.
            const string Head = " 93\n0\n 94\n0\n 95\n0\n 96\n0\n141\n10.0\n";
            DxfDatabase untitled = DxfDatabase.Parse(
                DxfDimensionDrawings.R2018.Replace(Head, " 93\n1\n 94\n0\n 95\n0\n 96\n0\n280\n1\n141\n10.0\n")
            );
            Table(untitled).InsertRows(0, 9.0, 1);
            DxfTable third = Reread(untitled);
            Assert.True(third.IsHeaderSuppressed);
            Assert.Equal(DxfCellAlignment.MiddleCenter, third.Alignment(1, 0));
            Assert.Null(third.TextHeight(1, 0));
        }

        [Fact]
        public void ARowPutInBeforeTheFirstRowOfData_TakesTheTablesOwnLooksOfDataAway_TheRowsOfDataKeepingThem()
        {
            // D28d d1 to d3, d5 (D28c o7): the table's own looks of its data, in its head, went from it where AutoCAD 2021's
            // InsertRows put a row in at or before its first row of data: AutoCAD read the new row by its style (4.5,
            // TopCenter) and gave the old rows of data those looks as their own (170 1, 7 GH_TXT, 64 1, 140 9.0); the header
            // moved on and left out took the header's (170 5, 63 3, 283 0, 140 9.0). A row put in after it took nothing away.
            foreach (int at in new[] { 1, 2 })
            {
                DxfDatabase db = DxfDatabase.Parse(RowLooks());
                Table(db).InsertRows(at, 9.0, 1);
                DxfTable read = Reread(db);
                Assert.Null(read.TextHeight(at, 0));
                Assert.Null(read.Alignment(at, 1));
                Assert.Equal(9.0, read.TextHeight(3, 0));
                Assert.Equal("GH_TXT", read.TextStyle(3, 0).Name);
                Assert.Equal(1, read.ContentColor(3, 1).ColorIndex);
                Assert.Equal(DxfCellAlignment.TopLeft, read.Alignment(3, 0));
                Assert.DoesNotContain(
                    "GH_TXT",
                    Record(db.ToText(), "F6")
                        .Substring(0, Record(db.ToText(), "F6").IndexOf("171\n", StringComparison.Ordinal)),
                    StringComparison.Ordinal
                );
            }

            DxfDatabase header = DxfDatabase.Parse(RowLooks());
            Table(header).InsertRows(1, 9.0, 1);
            DxfTable moved = Reread(header);
            Assert.True(moved.IsHeaderSuppressed);
            Assert.Equal(DxfCellAlignment.MiddleCenter, moved.Alignment(2, 0));
            Assert.Equal(3, moved.BackgroundColor(2, 1).ColorIndex);
            Assert.Equal(9.0, moved.TextHeight(2, 1));

            DxfDatabase after = DxfDatabase.Parse(RowLooks());
            Table(after).InsertRows(3, 9.0, 1);
            Assert.Contains(
                "GH_TXT",
                Record(after.ToText(), "F6")
                    .Substring(0, Record(after.ToText(), "F6").IndexOf("171\n", StringComparison.Ordinal)),
                StringComparison.Ordinal
            );
            Assert.Null(Reread(after).TextHeight(2, 0));
        }

        #endregion

        #region Rows that take the look of the title or the header

        [Fact]
        public void ARowThatTakesTheLookOfTheTitleOrTheHeader_TakesTheirLooksOfTheStyleAsItsOwn_AndTheTitlesMerge()
        {
            // D28d i1, i7: AutoCAD 2021's InsertRowsAndInherit(3, 0, 1) merged the new row across as the title is, and gave its
            // cells 170 5 and 140 6.0, the title's looks of its style that are not the data's; i2, i5, from the header, 170 5.
            DxfDatabase db = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
            Table(db).InsertRowsAndInherit(3, 0, 1);
            DxfTable read = Reread(db);
            Assert.Equal(new DxfCellRange(3, 0, 3, 1), read.GetMergeRange(3, 0));
            Assert.Equal(6.0, read.TextHeight(3, 0));
            Assert.Equal(DxfCellAlignment.MiddleCenter, read.Alignment(3, 1));
            Assert.Equal(DxfRowType.DataRow, read.RowType(3));

            foreach (int at in new[] { 3, 2 })
            {
                DxfDatabase header = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
                Table(header).InsertRowsAndInherit(at, 1, 1);
                DxfTable copied = Reread(header);
                Assert.Equal(DxfCellAlignment.MiddleCenter, copied.Alignment(at, 1));
                Assert.Equal(3.5, copied.TextHeight(at, 0));
                Assert.False(copied.IsHeaderSuppressed);
            }

            // D28d i3 (D28c i3): from the title before the header, the header left out (93 2, 281 1) and its cells given 170 5,
            // the new row merged and of the title's looks.
            DxfDatabase under = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
            Table(under).InsertRowsAndInherit(1, 0, 1);
            DxfTable second = Reread(under);
            Assert.True(second.IsHeaderSuppressed);
            Assert.Equal(new DxfCellRange(1, 0, 1, 1), second.GetMergeRange(1, 1));
            Assert.Equal(6.0, second.TextHeight(1, 0));
            Assert.Equal(DxfCellAlignment.MiddleCenter, second.Alignment(2, 1));

            // D28d i4: from the title before it, the new row the title and the old one data of the title's looks; i8, from the
            // header before it, the new row the header and the old one data of the header's.
            DxfDatabase top = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
            Table(top).InsertRowsAndInherit(0, 0, 1);
            DxfTable title = Reread(top);
            Assert.False(title.IsTitleSuppressed);
            Assert.True(title.IsHeaderSuppressed);
            Assert.Null(title.TextHeight(0, 0));
            Assert.Equal(new DxfCellRange(0, 0, 0, 1), title.GetMergeRange(0, 1));
            Assert.Equal(6.0, title.TextHeight(1, 0));
            Assert.Equal("Parts", title.TextString(1, 0));

            DxfDatabase twice = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
            Table(twice).InsertRowsAndInherit(1, 1, 1);
            DxfTable headers = Reread(twice);
            Assert.False(headers.IsHeaderSuppressed);
            Assert.Null(headers.Alignment(1, 1));
            Assert.Equal(DxfCellAlignment.MiddleCenter, headers.Alignment(2, 1));
        }

        #endregion

        #region Margins

        [Fact]
        public void TheMarginsOfATableOfNoneOfItsOwn_AreItsStyles()
        {
            // D28d h10 (D28c read 1.5 throughout): AutoCAD 2021 read a table of no margins of its own by its style's, 2 where the
            // style gave 2, 1.5 for its Standard; the library read 0.06.
            DxfTable styled = Table(DxfDatabase.Parse(Styled("4.5", "2.0", "1.0")));
            Assert.Equal(2.0, styled.HorizontalCellMargin);
            Assert.Equal(1.0, styled.VerticalCellMargin);

            DxfTable standard = Table(DxfDatabase.Parse(DxfDimensionDrawings.R2018));
            Assert.Equal(1.5, standard.HorizontalCellMargin);
            Assert.Equal(1.5, standard.VerticalCellMargin);

            // A drawing of inches of no table style, AutoCAD's Standard of inches, 0.06.
            const string Metric = "$MEASUREMENT\n 70\n1\n";
            Assert.Contains(Metric, DxfDimensionDrawings.R2018, StringComparison.Ordinal);
            Assert.Equal(
                0.06,
                Table(
                    DxfDatabase.Parse(DxfDimensionDrawings.R2018.Replace(Metric, "$MEASUREMENT\n 70\n0\n"))
                ).HorizontalCellMargin
            );
        }

        #endregion
    }
}
