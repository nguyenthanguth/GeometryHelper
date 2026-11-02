using System;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// What followed from AutoCAD 2021's answers to the D33b runs of 2 November 2026 (headless, AutoCAD's own edits read in
    /// memory and saved, its files and the library's read back): the rows AutoCAD merges across when it loads a file, the
    /// format of a value that is not a number, the overrides read from a cell's flag of its kind of value, the cells left of
    /// a table cleared of its overrides, and the look a fill copies onto a value of another kind. Each drawing is built in the
    /// form AutoCAD wrote, read as a file is.
    /// </summary>
    public class DxfTableD33bTests
    {
        private static DxfTable Table(DxfDatabase db) => DxfAutoCadChecksD16Tests.Table(db);

        private static DxfTable Reread(DxfDatabase db) => Table(DxfDatabase.Parse(db.ToText()));

        private static string Record(string text, string handle) => DxfAutoCadChecksD16Tests.Record(text, handle);

        private static DxfColor Aci(short index) => DxfColor.FromColorIndex(DxfColorMethod.ByAci, index);

        /// <summary>The R2018 drawing of a table of 3 rows and the columns given, its title Parts unmerged, as read from a file.</summary>
        private static DxfDatabase Unmerged(int columns)
        {
            DxfDatabase db = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
            DxfTable table = Table(db);

            if (columns != 2)
            {
                table.SetSize(3, columns);
            }

            table.UnmergeCells(new DxfCellRange(0, 0, 0, columns - 1));
            return DxfDatabase.Parse(db.ToText());
        }

        #region Rows merged on load

        [Fact]
        public void ATitleShownButUnmerged_IsWrittenMergedAcross_WhenRowsOrColumnsAreTakenOut()
        {
            // D33b w01, w07, m01, m05, m12 (and D33 n1 to n15): AutoCAD 2021 merged the title across every column when it loaded
            // the file, and wrote that merge after DeleteRows and DeleteColumns, Parts across (0, 0) to (0, 1); the library
            // wrote the title unmerged, which AutoCAD merged again on reading, its hidden cells placed at the table's corner.
            DxfDatabase rows = Unmerged(2);
            Table(rows).DeleteRows(2, 1);
            DxfTable read = Reread(rows);
            Assert.Equal(new DxfCellRange(0, 0, 0, 1), read.GetMergeRange(0, 1));
            Assert.Equal("Parts", read.TextString(0, 0));
            Assert.Equal(string.Empty, read.TextString(0, 1));

            DxfDatabase columns = Unmerged(3);
            Table(columns).DeleteColumns(1, 1);
            Assert.Equal(new DxfCellRange(0, 0, 0, 1), Reread(columns).GetMergeRange(0, 0));

            DxfDatabase first = Unmerged(3);
            Table(first).DeleteColumns(0, 1);
            DxfTable cut = Reread(first);
            Assert.Equal(new DxfCellRange(0, 0, 0, 1), cut.GetMergeRange(0, 1));
            Assert.Equal(string.Empty, cut.TextString(0, 0));
        }

        [Fact]
        public void ARowPutInBeforeTheTitle_IsWrittenMergedAcross_ThoughTheTitleIsLeftOut()
        {
            // D33b w12, m09 (and D33 m01, n3a, n3b): the empty row AutoCAD put in before the title was merged across on load,
            // whether the title is shown or not, and written so after DeleteRows; the library left it unmerged.
            DxfDatabase db = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
            Table(db).InsertRows(0, 9.0, 1);
            db = DxfDatabase.Parse(db.ToText());
            Assert.True(Table(db).IsTitleSuppressed);
            Table(db).DeleteRows(3, 1);

            DxfTable read = Reread(db);
            Assert.Equal(new DxfCellRange(0, 0, 0, 1), read.GetMergeRange(0, 0));
            Assert.Equal(new DxfCellRange(1, 0, 1, 1), read.GetMergeRange(1, 0));
            Assert.Equal("Parts", read.TextString(1, 0));
        }

        [Fact]
        public void AFirstRowThatHoldsMoreThanItsFirstCell_IsLeftUnmerged()
        {
            // D33b w10, w24, w25, w29, w30, w08, w09, w37 and m08, m15: a text, a space, nought or a block in a cell after the
            // first, or a block merged down from the row, kept AutoCAD from merging it; a colour, background or alignment alone
            // did not (w22, w23, w31, m25). Each state is made in the session that unmerges the title, as AutoCAD made it: a file
            // read with the title unmerged has it merged at its first edit (D33c e01 to e20).
            foreach (Action<DxfTable> fill in new Action<DxfTable>[]
            {
                t => t.SetValue(0, 1, "X"),
                t => t.SetValue(0, 1, " "),
                t => t.SetValue(0, 1, 0),
                t => t.MergeCells(new DxfCellRange(0, 1, 1, 1)),
                t => t.MergeCells(new DxfCellRange(0, 0, 1, 0)),
            })
            {
                DxfDatabase db = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
                Table(db).UnmergeCells(new DxfCellRange(0, 0, 0, 1));
                fill(Table(db));
                db = DxfDatabase.Parse(db.ToText());
                DxfCellRange before = Table(db).GetMergeRange(0, 0);
                Table(db).DeleteRows(2, 1);
                Assert.Equal(before, Reread(db).GetMergeRange(0, 0));
            }

            DxfDatabase coloured = Unmerged(2);
            Table(coloured).SetContentColor(0, 1, Aci(1));
            coloured = DxfDatabase.Parse(coloured.ToText());
            Table(coloured).DeleteRows(2, 1);
            Assert.Equal(new DxfCellRange(0, 0, 0, 1), Reread(coloured).GetMergeRange(0, 0));
        }

        #endregion

        #region Formats

        [Fact]
        public void AFormatSetOnAWholeNumberOrAText_IsNotWritten_TheValueLosingItsFlag2()
        {
            // D33b s01 to s04, s12, s14 (and D33 u6, P2 FMTTEXT): AutoCAD 2021 wrote 7 and B-12 given %lu2%pr2 with no format,
            // 93 4, and read their format as empty, 7 shown 7; the library wrote the format, 7.00.
            DxfDatabase db = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
            DxfTable table = Table(db);
            table.SetValue(2, 0, 7);
            table.SetFormat(2, 0, "%lu2%pr2");
            table.SetFormat(1, 1, "%lu2%pr2");

            DxfTable read = Reread(db);
            Assert.Equal(string.Empty, read.Format(2, 0));
            Assert.Equal(string.Empty, read.Format(1, 1));
            Assert.Equal("7", read.TextString(2, 0));
            string record = Record(db.ToText(), "F6");
            Assert.Contains("301\nCELL_VALUE\n 93\n4\n 90\n1\n 91\n7\n 94\n0\n300\n\n302\n7\n304\n", record, StringComparison.Ordinal);
            Assert.Contains("301\nCELL_VALUE\n 93\n4\n 90\n4\n  1\nQty\n 94\n0\n300\n\n302\nQty\n304\n", record, StringComparison.Ordinal);
        }

        [Fact]
        public void AFormatSetOnANumber_IsWritten_TheValueLosingItsFlag2()
        {
            // D33b s06, s16: 12.5 given %lu2%pr3 was written 93 4 with its format and 12.500, and given none again 12.500000.
            DxfDatabase db = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
            DxfTable table = Table(db);
            table.SetValue(2, 0, 12.5);
            table.SetFormat(2, 0, "%lu2%pr3");
            Assert.Contains("301\nCELL_VALUE\n 93\n4\n 90\n2\n140\n12.5\n 94\n0\n300\n%lu2%pr3\n302\n12.500\n304\n", Record(db.ToText(), "F6"), StringComparison.Ordinal);

            table.SetFormat(2, 0, string.Empty);
            Assert.Equal("12.500000", Reread(db).TextString(2, 0));
        }

        [Fact]
        public void AFormatSetOnAValueOfNone_LeavesTheEmptyValueOfNoKind_AndOnABlockCellTakesItsKindAway()
        {
            // D33b s05, s17, s08, s19: a value of none given %lu2%pr2, or an empty format, was written 93 5, 90 512 and no
            // format; a block cell kept no format and lost the flag 0x40000 (91 262144 made 0).
            foreach (string format in new[] { "%lu2%pr2", string.Empty })
            {
                DxfDatabase db = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
                DxfTable table = Table(db);
                table.ResetValue(2, 0);
                table.SetFormat(2, 0, format);
                Assert.Contains("301\nCELL_VALUE\n 93\n5\n 90\n512\n 94\n0\n300\n\n302\n\n304\n", Record(db.ToText(), "F6"), StringComparison.Ordinal);

                DxfDatabase block = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
                Table(block).SetOverrides(2, 1, 0, DxfCellProperties.DataType);
                block = DxfDatabase.Parse(block.ToText());
                Table(block).SetFormat(2, 1, format);
                DxfTable read = Reread(block);
                Assert.Equal(string.Empty, read.Format(2, 1));
                Assert.Equal(DxfCellProperties.Invalid, read.GetOverrides(2, 1, 0));
            }
        }

        [Fact]
        public void TheFlagOfACellsKindOfValue_IsReadAsItsKindAndItsFormat()
        {
            // D33 P, acad-read1: AutoCAD 2021 read a cell of the flags 262 144 (0x40000) as DataType and DataFormat, given
            // either of them; the library read DataType alone.
            foreach (DxfCellProperties given in new[] { DxfCellProperties.DataType, DxfCellProperties.DataFormat })
            {
                DxfDatabase db = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
                Table(db).SetOverrides(1, 1, 0, given);
                Assert.Contains("176\n0\n 91\n262144\n178\n", Record(db.ToText(), "F6"), StringComparison.Ordinal);
                Assert.Equal(DxfCellProperties.DataType | DxfCellProperties.DataFormat, Reread(db).GetOverrides(1, 1, 0));
            }
        }

        #endregion

        #region Overrides cleared

        /// <summary>The R2018 drawing whose table holds its own header and data of 9, its data GH_TXT, red and top left, and its header's background green (D28c o0), its cells of none of them.</summary>
        private static string RowLooks()
        {
            const string Head = " 93\n0\n 94\n0\n 95\n0\n 96\n0\n141\n10.0\n";
            Assert.Contains(Head, DxfDimensionDrawings.R2018, StringComparison.Ordinal);
            return DxfDimensionDrawings.R2018.Replace(Head, " 93\n6886016\n 94\n0\n 95\n0\n 96\n0\n  7\nGH_TXT\n140\n9.0\n170\n1\n 63\n3\n 64\n1\n140\n9.0\n283\n0\n141\n10.0\n");
        }

        [Fact]
        public void ATableClearedOfItsCellsOverrides_GivesEachCellItsOwnLooksOfItsRowsKind()
        {
            // D33b c03, c06 (and D28e v33): AutoCAD 2021's ClearTableStyleOverrides(2) took Mark's GH_TXT, 3.5 and middle centre
            // away and gave each cell the table's own looks of its row's kind as its own, the header cells 63 3, 283 0, 140 9.0
            // and the data cells 170 1, 7 GH_TXT, 64 1, 140 9.0, a block cell without its flag 0x40000 (91 57); the library
            // gave the cells none.
            DxfDatabase db = DxfDatabase.Parse(RowLooks());
            Table(db).ClearTableStyleOverrides(2);

            DxfTable read = Reread(db);
            Assert.Equal(DxfCellProperties.BackgroundColor | DxfCellProperties.TextHeight, read.GetOverrides(1, 0, 0));
            Assert.Equal(9.0, read.TextHeight(1, 0));
            Assert.Equal("GH_TXT", read.TextStyle(2, 0).Name);
            Assert.Equal(1, read.ContentColor(2, 0).ColorIndex);
            Assert.Equal(DxfCellAlignment.TopLeft, read.Alignment(2, 1));
            Assert.Equal(DxfCellProperties.Alignment | DxfCellProperties.ContentColor | DxfCellProperties.TextStyle | DxfCellProperties.TextHeight, read.GetOverrides(2, 1, 0));
            Assert.NotEmpty(read.TableStyleOverrides());
        }

        [Fact]
        public void ATableClearedOfItsCellsOverrides_LeavesItsCellsOfNoValue_TheEmptyValueOfNoKind()
        {
            // D33b c13, c14, c15: of 0 and 2 each cell of no value (93 7, 90 0) was written 93 5, 90 512, which AutoCAD reads as
            // an empty text, and a block cell lost its flag 0x40000; of 1 the cells were left as they were.
            foreach (int options in new[] { 0, 2 })
            {
                DxfDatabase db = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
                Table(db).ResetValue(1, 1);
                db = DxfDatabase.Parse(db.ToText());
                Table(db).ClearTableStyleOverrides(options);
                string record = Record(db.ToText(), "F6");
                Assert.Contains("301\nCELL_VALUE\n 93\n5\n 90\n512\n 94\n0\n300\n\n302\n\n304\n", record, StringComparison.Ordinal);
                Assert.DoesNotContain("301\nCELL_VALUE\n 93\n7\n", record, StringComparison.Ordinal);
                Assert.Equal(DxfCellProperties.Invalid, Reread(db).GetOverrides(2, 1, 0));
            }

            DxfDatabase kept = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
            Table(kept).ResetValue(1, 1);
            kept = DxfDatabase.Parse(kept.ToText());
            string before = Record(kept.ToText(), "F6");
            Table(kept).ClearTableStyleOverrides(1);
            Assert.Contains("301\nCELL_VALUE\n 93\n7\n 90\n0\n", Record(kept.ToText(), "F6"), StringComparison.Ordinal);
            Assert.Equal(before, Record(kept.ToText(), "F6"));
        }

        #endregion

        #region Fill

        /// <summary>The drawing of AutoCAD's D33 b-fb: 7 rows and 3 columns, 1.5 (red) and 2.5 in column 0, a block and X1 in column 1, 10 and 20 (green) in column 2.</summary>
        private static DxfDatabase Filled()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
            DxfTable table = Table(db);
            table.SetSize(7, 3);
            table.SetValue(2, 0, 1.5);
            table.SetValue(3, 0, 2.5);
            table.SetValue(3, 1, "X1");
            table.SetValue(2, 2, 10);
            table.SetValue(3, 2, 20);
            table.SetContentColor(2, 0, Aci(1));
            table.SetBackgroundColor(3, 2, Aci(3));
            return DxfDatabase.Parse(db.ToText());
        }

        [Fact]
        public void AFillOfTheFormat_TurnsAValueOfAnotherKind_AsSetDataTypeTurnsIt()
        {
            // D33b f02, f23 to f26, f31, f32: AutoCAD 2021's CopyFormat of 1.5 onto Mark, the header, left a number of none (93 7,
            // 90 2), onto the text 12 the number 12 shown 12.0000 (%lu6), onto 7 the number 7.000000; the format of 10 onto 1.5
            // the whole number 1, of 20 onto X1 a whole number of none, of X1 onto 2.5 the text 2.500000. The library kept each
            // value as it was.
            DxfDatabase header = Filled();
            Table(header).Fill(new DxfCellRange(1, 0, 1, 0), new DxfCellRange(2, 0, 3, 0), DxfTableFillOptions.CopyFormat);
            DxfTable read = Reread(header);
            Assert.Equal(DxfDataType.Double, read.DataType(1, 0));
            Assert.Equal(string.Empty, read.TextString(1, 0));
            Assert.Equal(Aci(1), read.ContentColor(1, 0));

            foreach (object value in new object[] { "12", 7, "abc" })
            {
                DxfDatabase db = Filled();
                Table(db).SetValue(4, 0, value);
                Table(db).Fill(new DxfCellRange(4, 0, 4, 0), new DxfCellRange(2, 0, 2, 0), DxfTableFillOptions.CopyFormat);
                DxfTable turned = Reread(db);
                Assert.Equal(DxfDataType.Double, turned.DataType(4, 0));
                Assert.Equal(value is int ? "7.000000" : value as string == "12" ? "12.0000" : string.Empty, turned.TextString(4, 0));
            }

            DxfDatabase rows = Filled();
            DxfTable table = Table(rows);
            table.Fill(new DxfCellRange(2, 0, 2, 0), new DxfCellRange(2, 2, 2, 2), DxfTableFillOptions.CopyFormat | DxfTableFillOptions.Row);
            table.Fill(new DxfCellRange(3, 1, 3, 1), new DxfCellRange(3, 2, 3, 2), DxfTableFillOptions.CopyFormat | DxfTableFillOptions.Row);
            table.Fill(new DxfCellRange(3, 0, 3, 0), new DxfCellRange(3, 1, 3, 1), DxfTableFillOptions.CopyFormat | DxfTableFillOptions.Row);
            DxfTable done = Reread(rows);
            Assert.Equal(1, done.Value(2, 0));
            Assert.Equal(DxfDataType.Long, done.DataType(3, 1));
            Assert.Equal(string.Empty, done.TextString(3, 1));
            Assert.Equal(DxfDataType.Long, done.DataType(3, 0));
        }

        [Fact]
        public void AFill_FillsTheHeaderAndTheTitle_AsItFillsData()
        {
            // D33b f01, f03, f05, f06, f17 (and D33 f14): AutoCAD 2021 filled the header and the title as it filled rows of data,
            // 1.5 copied into Mark, 3.5 of a series into Parts; a guard of what the library did already.
            DxfDatabase db = Filled();
            Table(db).Fill(new DxfCellRange(1, 0, 1, 0), new DxfCellRange(2, 0, 3, 0), DxfTableFillOptions.CopyContent | DxfTableFillOptions.GenerateSeries);
            Assert.Equal("1.500000", Reread(db).TextString(1, 0));

            DxfDatabase title = Filled();
            Table(title).Fill(new DxfCellRange(0, 0, 0, 0), new DxfCellRange(2, 0, 3, 0), DxfTableFillOptions.GenerateSeries);
            Assert.Equal("3.500000", Reread(title).TextString(0, 0));
        }

        #endregion
    }
}
