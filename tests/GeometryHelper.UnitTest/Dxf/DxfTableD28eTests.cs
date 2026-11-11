using System;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// What followed from AutoCAD 2021's answers to the D28e runs of 2 November 2026 (headless, AutoCAD's own edits read in
    /// memory and saved, its files and the library's read back): the kind of a row put into a table of one row, the kinds and
    /// looks of the rows left when rows are taken out, the cells MergeCells empties and the lines it gives a block, a value of
    /// none, the text of a number, a background left unfilled, the lines of a kind of row written in the head, and the
    /// margins kept while the drawing is not changed. Each drawing is built in the form AutoCAD wrote, read as a file is.
    /// </summary>
    public class DxfTableD28eTests
    {
        private static DxfTable Table(DxfDatabase db) => DxfAutoCadChecksD16Tests.Table(db);

        private static DxfTable Reread(DxfDatabase db) => Table(DxfDatabase.Parse(db.ToText()));

        private static string Record(string text, string handle) => DxfAutoCadChecksD16Tests.Record(text, handle);

        private static DxfColor Aci(short index) => DxfColor.FromColorIndex(DxfColorMethod.ByAci, index);

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

        #region The kinds of rows

        [Fact]
        public void ATableOfOneRowGivenRows_HasItsFirstNewRowTheHeader_ShownAgain()
        {
            // D28e s1, s3 to s6, s8, s14, s17: AutoCAD 2021's DeleteRows(1, 2) left the title alone and the header out (93 2,
            // 281 1); its SetSize and InsertRows put the header back as the first new row, whether the header had been left out
            // by the rows taken or by IsHeaderSuppressed, 93 0 and no 281. The library kept the header shown after the rows went,
            // reading data as the header, and then left it out for good.
            DxfDatabase db = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
            Table(db).DeleteRows(1, 2);
            DxfTable one = Reread(db);
            Assert.True(one.IsHeaderSuppressed);
            Assert.Contains(" 93\n2\n", Record(db.ToText(), "F6"), StringComparison.Ordinal);

            foreach (
                Action<DxfTable> grow in new Action<DxfTable>[] { t => t.SetSize(3, 2), t => t.InsertRows(1, 9.0, 2) }
            )
            {
                DxfDatabase grown = DxfDatabase.Parse(db.ToText());
                grow(Table(grown));
                DxfTable read = Reread(grown);
                Assert.False(read.IsHeaderSuppressed);
                Assert.Equal(DxfRowType.HeaderRow, read.RowType(1));
                Assert.Equal(DxfRowType.DataRow, read.RowType(2));
                Assert.DoesNotContain("281\n", Record(grown.ToText(), "F6"), StringComparison.Ordinal);
            }
        }

        [Fact]
        public void TheFirstRowPutIntoATableOfOneRowOfData_IsOfTheHeader_ReadAsDataWhereItIsOutOfPlace()
        {
            // D28e s12, e08, e09: the rows of the title and the header taken out, AutoCAD 2021 left both out (93 3, 280 1, 281 1);
            // a row put in before the row of data was the header, shown (93 1, no 281), and one put in after it the header out of
            // its place, read as data with the header's alignment of its style as its own (170 5).
            DxfDatabase db = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
            Table(db).DeleteRows(0, 2);
            DxfTable data = Reread(db);
            Assert.True(data.IsTitleSuppressed);
            Assert.True(data.IsHeaderSuppressed);
            Assert.Equal("B-12", data.TextString(0, 0));

            DxfDatabase before = DxfDatabase.Parse(db.ToText());
            Table(before).InsertRows(0, 9.0, 1);
            DxfTable header = Reread(before);
            Assert.True(header.IsTitleSuppressed);
            Assert.False(header.IsHeaderSuppressed);
            Assert.Equal(DxfRowType.HeaderRow, header.RowType(0));

            DxfDatabase after = DxfDatabase.Parse(db.ToText());
            Table(after).InsertRows(1, 9.0, 1);
            DxfTable outOfPlace = Reread(after);
            Assert.True(outOfPlace.IsHeaderSuppressed);
            Assert.Equal(DxfRowType.DataRow, outOfPlace.RowType(1));
            Assert.Equal(DxfCellAlignment.MiddleCenter, outOfPlace.Alignment(1, 0));
        }

        [Fact]
        public void TakingTheTitleOrTheHeaderOut_LeavesItOut_AndATitleMovedBackIntoPlaceIsTheTitleAgain()
        {
            // D28e x1, x2: AutoCAD 2021's DeleteRows of the title left it out (93 1, 280 1), the header now first and shown; of
            // the header, the header out (93 2, 281 1). The library wrote neither, reading the header as the title.
            DxfDatabase title = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
            Table(title).DeleteRows(0, 1);
            DxfTable untitled = Reread(title);
            Assert.True(untitled.IsTitleSuppressed);
            Assert.Equal(DxfRowType.HeaderRow, untitled.RowType(0));
            Assert.Equal("Mark", untitled.TextString(0, 0));

            DxfDatabase header = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
            Table(header).DeleteRows(1, 1);
            DxfTable headless = Reread(header);
            Assert.True(headless.IsHeaderSuppressed);
            Assert.Equal(DxfRowType.DataRow, headless.RowType(1));

            // D28e x12, x14: a row put in before the title left it and the header out, its TABLECONTENT keeping each row's style
            // (3, 1, 2, 3); the new row taken out again, AutoCAD read the title and the header shown (93 0, no 280 nor 281).
            DxfDatabase linked = DxfDatabase.Parse(
                DxfAutoCadChecksD16Tests.LinkedByAutoCad(DxfDimensionDrawings.R2018)
            );
            Table(linked).InsertRows(0, 9.0, 1);
            Assert.True(Reread(linked).IsTitleSuppressed);
            Table(linked).DeleteRows(0, 1);
            DxfTable back = Reread(linked);
            Assert.False(back.IsTitleSuppressed);
            Assert.False(back.IsHeaderSuppressed);
            Assert.Equal(DxfRowType.TitleRow, back.RowType(0));
            Assert.Equal(DxfRowType.HeaderRow, back.RowType(1));
        }

        [Fact]
        public void TheTablesOwnLooksOfAKindOfWhichNoRowIsLeft_GoFromItsHead()
        {
            // D28e x8, x9, x16: AutoCAD 2021's DeleteRows of the header took the table's own looks of the header (63 3, 283 0,
            // 140 9.0) out of its head with their flags, 93 4784258 with the header's 281; of the last row of data, those of the
            // data. The library kept them.
            DxfDatabase header = DxfDatabase.Parse(RowLooks());
            Table(header).DeleteRows(1, 1);
            string record = Record(header.ToText(), "F6");
            Assert.Contains(" 93\n4784258\n", record, StringComparison.Ordinal);
            Assert.DoesNotContain(" 63\n3\n", record, StringComparison.Ordinal);
            Assert.Contains("  7\nGH_TXT\n", record, StringComparison.Ordinal);
            Assert.True(Reread(header).IsHeaderSuppressed);

            DxfDatabase data = DxfDatabase.Parse(RowLooks());
            Table(data).DeleteRows(2, 1);
            string kept = Record(data.ToText(), "F6");
            string head = kept.Substring(0, kept.IndexOf("171\n", StringComparison.Ordinal));
            Assert.Contains(" 93\n2101760\n", head, StringComparison.Ordinal);
            Assert.DoesNotContain("GH_TXT", head, StringComparison.Ordinal);
            Assert.Contains(" 63\n3\n", head, StringComparison.Ordinal);
        }

        [Fact]
        public void TakingOutTheTopLeftCellOfABlock_LeavesTheFirstCellLeftItsOwnContent()
        {
            // D28e x5, k1, k3, k4: AutoCAD 2021's DeleteColumns(0, 1) of the title merged across two columns left the cell left
            // of its own empty text, and its DeleteRows of the top row of a block merged down left the cell under it empty, as its
            // MergeCells had emptied it. The library moved Parts and Qty into them.
            DxfDatabase columns = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
            Table(columns).DeleteColumns(0, 1);
            DxfTable one = Reread(columns);
            Assert.Equal(string.Empty, one.TextString(0, 0));
            Assert.False(one.IsMergedCell(0, 0, out _));

            DxfDatabase rows = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
            Table(rows).MergeCells(new DxfCellRange(1, 1, 2, 1));
            Table(rows).DeleteRows(1, 1);
            DxfTable left = Reread(rows);
            Assert.Equal(string.Empty, left.TextString(1, 1));
            Assert.Equal("B-12", left.TextString(1, 0));
        }

        #endregion

        #region Merging

        [Fact]
        public void MergeCells_EmptiesTheOtherCellsOfTheBlock_KeepingTheirAlignment()
        {
            // D28e q1, q5, p8: AutoCAD 2021's MergeCells left each cell of the block but its top left a text cell of no value
            // (91 0, 93 7, 90 0), its text style and height of its own taken away and its alignment kept (Mark's 170 5 with the
            // flag 1); the library kept their content unseen, Qty shown again once unmerged.
            DxfDatabase header = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
            Table(header).MergeCells(new DxfCellRange(1, 0, 1, 1));
            DxfTable merged = Reread(header);
            Assert.Equal(string.Empty, merged.TextString(1, 1));
            Assert.Equal(DxfDataType.Unknown, merged.GetDataType(1, 1, 0).DataType);
            Assert.Equal("Mark", merged.TextString(1, 0));
            Table(header).UnmergeCells(new DxfCellRange(1, 0, 1, 1));
            Assert.Equal(string.Empty, Reread(header).TextString(1, 1));

            DxfDatabase title = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
            Table(title).MergeCells(new DxfCellRange(0, 0, 1, 1));
            DxfTable big = Reread(title);
            Assert.Equal(string.Empty, big.TextString(1, 0));
            Assert.Equal(DxfCellAlignment.MiddleCenter, big.Alignment(1, 0));
            Assert.Null(big.TextHeight(1, 0));
            Assert.Null(big.TextStyle(1, 0));
            Assert.Equal("Parts", big.TextString(0, 0));
        }

        [Fact]
        public void MergeCells_GivesTheEdgesOfTheBlockTheLinesOfItsCorners()
        {
            // D28e m3, q3, q4: AutoCAD 2021's MergeCells of (1, 0) to (2, 1) gave the top of (1, 1) the red top of (1, 0) where it
            // was green, the right of (2, 1) the cyan right of (1, 1) where it was red, and the bottom of (2, 1) the yellow bottom
            // of (2, 0) where it was green; the library kept each cell's own.
            DxfDatabase db = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
            DxfTable table = Table(db);
            table.SetGridColor(1, 0, DxfGridLineType.HorizontalTop, Aci(1));
            table.SetGridColor(1, 1, DxfGridLineType.HorizontalTop, Aci(3));
            table.SetGridColor(1, 1, DxfGridLineType.VerticalRight, Aci(4));
            table.SetGridColor(2, 0, DxfGridLineType.HorizontalBottom, Aci(2));
            table.SetGridColor(2, 1, DxfGridLineType.HorizontalBottom, Aci(3));
            table.SetGridColor(2, 1, DxfGridLineType.VerticalRight, Aci(1));
            DxfDatabase lines = DxfDatabase.Parse(db.ToText());
            Table(lines).MergeCells(new DxfCellRange(1, 0, 2, 1));
            DxfTable read = Reread(lines);
            Assert.Equal(1, read.GetGridColor(1, 1, DxfGridLineType.HorizontalTop).ColorIndex);
            Assert.Equal(4, read.GetGridColor(2, 1, DxfGridLineType.VerticalRight).ColorIndex);
            Assert.Equal(2, read.GetGridColor(2, 1, DxfGridLineType.HorizontalBottom).ColorIndex);
            Assert.Equal(1, read.GetGridColor(1, 0, DxfGridLineType.HorizontalTop).ColorIndex);
        }

        #endregion

        #region Values

        [Fact]
        public void ResetValueAndAText_CellMadeOfABlock_HoldNoValue()
        {
            // D28e v10, v19: AutoCAD 2021's ResetValue of Qty, and its SetCellType of the block cell M12 to a text cell, wrote a
            // value of none (91 0, 93 7, 90 0), which it read as of no kind; the library wrote an empty text.
            DxfDatabase db = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
            Table(db).ResetValue(1, 1);
            Table(db).SetCellType(2, 1, DxfTableCellType.TextCell);
            DxfTable read = Reread(db);
            Assert.Equal(DxfDataType.Unknown, read.GetDataType(1, 1, 0).DataType);
            Assert.Equal(string.Empty, read.TextString(1, 1));
            Assert.Equal(DxfTableCellType.TextCell, read.CellType(2, 1));
            Assert.Equal(DxfDataType.Unknown, read.GetDataType(2, 1, 0).DataType);
        }

        [Fact]
        public void ANumberValue_IsWrittenAsAutoCadShowsIt_OfSixPlacesOrOfItsFormats()
        {
            // D28e v09, v11, w30 to w33: AutoCAD 2021 wrote 12.5 set as 12.500000, 1/3 as 0.333333 and -0.125 as -0.125000, and
            // 12.500 under the format %lu2%pr3, and showed the library's 12.5 as it was. Before R2007 the text is all the cell
            // holds, and the library keeps the number whole in it where AutoCAD wrote 12.500000 (w09).
            DxfDatabase db = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
            DxfTable table = Table(db);
            table.SetValue(2, 0, 12.5);
            Assert.Equal("12.500000", Reread(db).TextString(2, 0));
            table.SetFormat(2, 0, "%lu2%pr3");
            Assert.Equal("12.500", Reread(db).TextString(2, 0));
            Assert.Equal(12.5, Reread(db).Value(2, 0));
            table.SetValue(1, 1, -0.125);
            Assert.Equal("-0.125000", Reread(db).TextString(1, 1));

            DxfDatabase old = DxfDatabase.Parse(DxfDimensionDrawings.R2000);
            Table(old).SetValue(2, 0, 1.0 / 3.0);
            Assert.Equal(
                1.0 / 3.0,
                double.Parse(Reread(old).TextString(2, 0), System.Globalization.CultureInfo.InvariantCulture)
            );
        }

        #endregion

        #region Backgrounds and lines

        [Fact]
        public void ABackgroundLeftUnfilled_IsWrittenAndReadAsAutoCadDoes()
        {
            // D28e v17, v18, v47, m1: AutoCAD 2021 wrote a filled background 283 0 with a colour of its own (63 0, BYBLOCK, where
            // the cell had none), and read 283 1 as unfilled; one left unfilled where the style fills none it took out (63, 283),
            // and in a header of the table's own green it wrote 63 0 and 283 1. The library wrote and read 283 the other way
            // about.
            DxfDatabase db = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
            DxfTable table = Table(db);
            table.SetBackgroundColorNone(1, 1, false);
            DxfTable filled = Reread(db);
            Assert.False(filled.IsBackgroundColorNone(1, 1));
            Assert.Equal(0, filled.BackgroundColor(1, 1).ColorIndex);
            table.SetBackgroundColor(1, 1, Aci(3));
            table.SetBackgroundColorNone(1, 1, true);
            DxfTable none = Reread(db);
            Assert.Null(none.IsBackgroundColorNone(1, 1));
            Assert.Null(none.BackgroundColor(1, 1));

            DxfDatabase green = DxfDatabase.Parse(RowLooks());
            Table(green).SetBackgroundColorNone(1, 0, true);
            DxfTable header = Reread(green);
            Assert.True(header.IsBackgroundColorNone(1, 0));
            Assert.Equal(0, header.BackgroundColor(1, 0).ColorIndex);
        }

        [Fact]
        public void TheLinesOfAKindOfRow_AreWrittenInTheHead_WhereAutoCadReadsThem()
        {
            // D28e v27: AutoCAD 2021 wrote SetGridColor(red, AllGridLines, DataRow) in the table's head after its flags (94
            // 258048 and 64 to 69 of 1, before its heights) and read them there on each cell of the data; it read the library's,
            // put in after the last cell, as none.
            DxfDatabase db = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
            Table(db).SetGridColor(Aci(1), DxfGridLineType.AllGridLines, DxfRowType.DataRow);
            Assert.Contains(
                " 94\n258048\n 95\n0\n 96\n0\n 64\n1\n 65\n1\n 66\n1\n 63\n1\n 68\n1\n 69\n1\n141\n",
                Record(db.ToText(), "F6"),
                StringComparison.Ordinal
            );
            Assert.Equal(1, Reread(db).GridColor(DxfGridLineType.HorizontalInside, DxfRowType.DataRow).ColorIndex);
        }

        #endregion

        #region Margins

        [Fact]
        public void TheMarginsOfTheStyleKept_AreReadAgainOnceTheDrawingChanges()
        {
            // The margins of a table of none of its own are read from its style once and kept while the drawing is not changed:
            // its units made inches, the table of no style reads AutoCAD's Standard of inches, 0.06, at once.
            DxfDatabase db = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
            DxfTable table = Table(db);
            Assert.Equal(1.5, table.HorizontalCellMargin);
            Assert.Equal(1.5, table.VerticalCellMargin);
            db.Measurement = DxfMeasurementValue.English;
            Assert.Equal(0.06, table.HorizontalCellMargin);
            Assert.Equal(0.06, table.VerticalCellMargin);
        }

        #endregion
    }
}
