using System;
using System.Linq;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// What followed from AutoCAD 2021's answers to the D33 runs of 2 November 2026 (headless, AutoCAD's own edits read in
    /// memory and saved, its files and the library's read back): a number shown in feet and inches, a kind of value set on a
    /// value not of it, the overrides a cell is given by <c>SetOverrides</c>, and the cells <c>Fill</c> fills. Each drawing is
    /// built in the form AutoCAD wrote, read as a file is.
    /// </summary>
    public class DxfTableD33Tests
    {
        private static DxfTable Table(DxfDatabase db) => DxfAutoCadChecksD16Tests.Table(db);

        private static DxfTable Reread(DxfDatabase db) => Table(DxfDatabase.Parse(db.ToText()));

        private static string Record(string text, string handle) => DxfAutoCadChecksD16Tests.Record(text, handle);

        private static DxfColor Aci(short index) => DxfColor.FromColorIndex(DxfColorMethod.ByAci, index);

        /// <summary>The value groups of a cell of the table as written, from its 301 to its 304.</summary>
        private static string ValueOf(DxfDatabase db, string text)
        {
            string record = Record(db.ToText(), "F6");
            int at = record.IndexOf("302\n" + text + "\n304\n", StringComparison.Ordinal);
            Assert.True(at >= 0, "No value shows " + text + ".");
            int from = record.LastIndexOf("301\nCELL_VALUE\n", at, StringComparison.Ordinal);
            return record.Substring(from, at - from + text.Length + 6);
        }

        #region Numbers in feet and inches

        [Theory]
        [InlineData(12.5, "%lu4%pr2", "1'-0 1/2\"")]
        [InlineData(12.5, "%lu4", "1'-0 1/2\"")]
        [InlineData(-1.0 / 3.0, "%lu4%pr4", "-0'-0 5/16\"")]
        [InlineData(23.875, "%lu4%pr2", "2'-0\"")]
        [InlineData(1234.5678, "%lu4%pr8", "102'-10 145/256\"")]
        [InlineData(0.25, "%lu4%pr0", "0'-0\"")]
        [InlineData(-12.5, "%lu4%pr0", "-1'-1\"")]
        [InlineData(-0.25, "%lu4%pr2", "-0'-0 1/4\"")]
        [InlineData(-0.001, "%lu4%pr2", "0'-0\"")]
        [InlineData(0.03125, "%lu4%pr4", "0'-0 1/16\"")]
        [InlineData(11.99, "%lu4%pr5", "1'-0\"")]
        [InlineData(11.99, "%lu4%pr6", "0'-11 63/64\"")]
        [InlineData(0.001, "%lu4%pr9", "0'-0 1/512\"")]
        [InlineData(1000000.0, "%lu4%pr2", "83333'-4\"")]
        [InlineData(7.25, "%lu4%pr2%zs1", "7 1/4\"")]
        [InlineData(0.75, "%lu4%pr2%zs1", "3/4\"")]
        [InlineData(6.0, "%lu4%pr2%zs1", "6\"")]
        [InlineData(12.0, "%lu4%pr2%zs1", "1'-0\"")]
        [InlineData(12.0, "%lu4%pr2%zs2", "1'")]
        [InlineData(0.0, "%lu4%pr2%zs2", "0'")]
        [InlineData(0.25, "%lu4%pr2%zs2", "0'-0 1/4\"")]
        [InlineData(0.0, "%lu4%pr2%zs3", "0\"")]
        [InlineData(-7.25, "%lu4%pr2%zs3", "-7 1/4\"")]
        [InlineData(12.0, "%lu4%pr2%zs12", "1'-0\"")]
        public void ANumberOfAnArchitecturalFormat_IsShownInFeetAndInches_AsAutoCadShowsIt(double value, string format, string shown)
        {
            // D33 P, P2, u1 to u5, u8: AutoCAD 2021 wrote 12.5 of %lu4%pr2 as 1'-0 1/2", to the nearest 2^N part of an inch, the
            // fraction reduced, a half away from nought, the sign only where the rounded number is not nought, and %zs1 leaving
            // out nought feet and %zs2 nought inches; the library left 12.5 as it was.
            DxfDatabase db = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
            DxfTable table = Table(db);
            table.SetValue(2, 0, value);
            table.SetFormat(2, 0, format);

            Assert.Equal(shown, Reread(db).TextString(2, 0));
            Assert.Equal(format, Reread(db).Format(2, 0));
        }

        [Fact]
        public void ANumberOfFeetBeyond32Bits_KeepsItsTextUnderAnArchitecturalFormat()
        {
            // D33 P2: AutoCAD 2021 wrote 1E11 of %lu4%pr2 as -2147483648'--2147483648 -1/0" and 5E11 as 5.00E+11; the library
            // writes neither, a number of more feet than 32 bits hold keeping the text it had.
            DxfDatabase db = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
            DxfTable table = Table(db);
            table.SetValue(2, 0, 1E11);
            table.SetFormat(2, 0, "%lu4%pr2");

            Assert.Equal("100000000000.000000", Reread(db).TextString(2, 0));
        }

        [Fact]
        public void AValueSet_TakesTheFormatOfTheCellAway()
        {
            // D33 u7: AutoCAD 2021's SetValue(100.125) of a cell of 1 in %lu4%pr3 wrote 300 empty and the text 100.125000; the
            // library kept the format.
            DxfDatabase db = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
            DxfTable table = Table(db);
            table.SetValue(2, 0, 1.0);
            table.SetFormat(2, 0, "%lu4%pr3");
            table.SetValue(2, 0, 100.125);

            DxfTable read = Reread(db);
            Assert.Equal(string.Empty, read.Format(2, 0));
            Assert.Equal("100.125000", read.TextString(2, 0));
        }

        #endregion

        #region Kinds of values set

        [Fact]
        public void AKindSetOnAValueNotOfIt_LeavesAValueOfNoneOfThatKind()
        {
            // D33 d1, d6, t14 (and D28e v12): AutoCAD 2021's SetDataType(Double) of the text B-12 wrote 93 7, 90 2, no data, an
            // empty text, and of Long 90 1, the unit not written; the library refused both.
            foreach (DxfDataType kind in new[] { DxfDataType.Double, DxfDataType.Long })
            {
                DxfDatabase db = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
                Table(db).SetDataType(2, 0, kind, DxfUnitType.Angle);

                DxfTable read = Reread(db);
                Assert.Equal(kind, read.DataType(2, 0));
                Assert.Equal(DxfUnitType.Unitless, read.UnitType(2, 0));
                Assert.Equal(string.Empty, read.TextString(2, 0));
                string record = Record(db.ToText(), "F6");
                Assert.Contains("301\nCELL_VALUE\n 93\n7\n 90\n" + (int)kind + "\n 94\n0\n300\n\n302\n\n304\n", record, StringComparison.Ordinal);
            }
        }

        [Fact]
        public void AKindSetOnAValueOfNone_IsWrittenOfTheFlags5()
        {
            // D33 d9, t7, t8: a value of none (93 7) given a kind kept no value and took the flag 2 away, 93 5.
            foreach (DxfDataType kind in new[] { DxfDataType.Double, DxfDataType.Long, DxfDataType.String })
            {
                DxfDatabase db = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
                DxfTable table = Table(db);
                table.ResetValue(2, 0);
                table.SetDataType(2, 0, kind, DxfUnitType.Unitless);

                Assert.Contains("301\nCELL_VALUE\n 93\n5\n 90\n" + (int)kind + "\n 94\n0\n300\n\n302\n\n304\n", Record(db.ToText(), "F6"), StringComparison.Ordinal);
            }
        }

        [Fact]
        public void AValueTurnedIntoAnotherKind_IsWrittenAsAutoCadTurnsIt()
        {
            // D33 d2, d3, d4, d10, d11, t1 to t6, t11, t15, t16: whole numbers cut towards nought, a text read as a number
            // shown in the drawing's units (%lu6, its LUNITS 2 and LUPREC 4) or as written in powers of ten (%lu1), the flag 2
            // of the value taken away but where a text of a whole number became that number.
            (Action<DxfTable> set, DxfDataType kind, string value)[] cases =
            {
                (t => t.SetValue(2, 0, "12"), DxfDataType.Long, "301\nCELL_VALUE\n 93\n6\n 90\n1\n 91\n12\n 94\n0\n300\n\n302\n12\n304\n"),
                (t => t.SetValue(2, 0, " 12 "), DxfDataType.Long, "301\nCELL_VALUE\n 93\n6\n 90\n1\n 91\n12\n 94\n0\n300\n\n302\n12\n304\n"),
                (t => t.SetValue(2, 0, "12.25"), DxfDataType.Long, "301\nCELL_VALUE\n 93\n4\n 90\n1\n 91\n12\n 94\n0\n300\n\n302\n12\n304\n"),
                (t => t.SetValue(2, 0, 12.7), DxfDataType.Long, "301\nCELL_VALUE\n 93\n4\n 90\n1\n 91\n12\n 94\n0\n300\n\n302\n12\n304\n"),
                (t => t.SetValue(2, 0, -12.7), DxfDataType.Long, "301\nCELL_VALUE\n 93\n4\n 90\n1\n 91\n-12\n 94\n0\n300\n\n302\n-12\n304\n"),
                (t => t.SetValue(2, 0, 3E9), DxfDataType.Long, "301\nCELL_VALUE\n 93\n4\n 90\n1\n 91\n-2147483648\n 94\n0\n300\n\n302\n-2147483648\n304\n"),
                (t => t.SetValue(2, 0, "12"), DxfDataType.Double, "301\nCELL_VALUE\n 93\n4\n 90\n2\n140\n12\n 94\n0\n300\n%lu6\n302\n12.0000\n304\n"),
                (t => t.SetValue(2, 0, "-0.5"), DxfDataType.Double, "301\nCELL_VALUE\n 93\n4\n 90\n2\n140\n-0.5\n 94\n0\n300\n%lu6\n302\n-0.5000\n304\n"),
                (t => t.SetValue(2, 0, "1e3"), DxfDataType.Double, "301\nCELL_VALUE\n 93\n4\n 90\n2\n140\n1000\n 94\n0\n300\n%lu1\n302\n1.0000E+03\n304\n"),
                (t => t.SetValue(2, 0, 7), DxfDataType.Double, "301\nCELL_VALUE\n 93\n4\n 90\n2\n140\n7\n 94\n0\n300\n\n302\n7.000000\n304\n"),
                (t => t.SetValue(2, 0, 7), DxfDataType.String, "301\nCELL_VALUE\n 93\n4\n 90\n4\n  1\n7\n 94\n0\n300\n\n302\n7\n304\n"),
                (t => t.SetValue(2, 0, 12.5), DxfDataType.String, "301\nCELL_VALUE\n 93\n4\n 90\n4\n  1\n12.500000\n 94\n0\n300\n\n302\n12.500000\n304\n"),
            };

            foreach ((Action<DxfTable> set, DxfDataType kind, string value) in cases)
            {
                DxfDatabase db = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
                DxfTable table = Table(db);
                set(table);
                table.SetDataType(2, 0, kind, DxfUnitType.Unitless);

                Assert.Contains(value, Record(db.ToText(), "F6"), StringComparison.Ordinal);
                Assert.Equal(kind, Reread(db).DataType(2, 0));
            }
        }

        #endregion

        #region Overrides kept and given

        [Fact]
        public void SetOverrides_GivesACellTheOverridesNamed_OfTheValuesAutoCadGivesThem()
        {
            // D33 o1 to o5, o20 (and D28e v42): AutoCAD 2021's SetOverrides of an override Qty does not hold gave it one, the
            // text height 0.18 of its content's own format, the alignment its row shows (the header's middle centre 5, the
            // data's top centre 2), the colour 0, a background of none (63 0, 64 0, 283 1), a text style of none; its flags
            // exactly those named, the kind of value 0x40000 taken out where it is not named. The library refused them.
            (DxfCellProperties given, int row, string flags, string looks)[] cases =
            {
                (DxfCellProperties.TextHeight, 1, " 91\n32\n", "140\n0.18\n"),
                (DxfCellProperties.Alignment, 1, " 91\n1\n", "170\n5\n"),
                (DxfCellProperties.Alignment, 2, " 91\n1\n", "170\n2\n"),
                (DxfCellProperties.ContentColor, 1, " 91\n8\n", " 64\n0\n"),
                (DxfCellProperties.BackgroundColor, 1, " 91\n6\n", " 63\n0\n"),
                (DxfCellProperties.TextStyle, 1, " 91\n16\n", string.Empty),
            };

            foreach ((DxfCellProperties given, int row, string flags, string looks) in cases)
            {
                string text = DxfDimensionDrawings.R2018.Replace("176\n0\n 91\n0\n178\n", "176\n0\n 91\n262144\n178\n");
                DxfDatabase db = DxfDatabase.Parse(text);
                Table(db).SetOverrides(row, row == 1 ? 1 : 0, 0, given);

                DxfTable read = Reread(db);
                Assert.Equal(given, read.GetOverrides(row, row == 1 ? 1 : 0, 0) & ~DxfCellProperties.DataFormat);
                string record = Record(db.ToText(), "F6");
                int at = record.IndexOf(row == 1 ? "302\nQty\n" : "302\nB-12\n", StringComparison.Ordinal);
                int from = record.LastIndexOf("171\n", at, StringComparison.Ordinal);
                string cell = record.Substring(from, at - from);
                Assert.Contains(flags, cell, StringComparison.Ordinal);
                Assert.Contains(looks, cell, StringComparison.Ordinal);
            }

            DxfDatabase height = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
            Table(height).SetOverrides(1, 1, 0, DxfCellProperties.TextHeight);
            Assert.Equal(0.18, Reread(height).TextHeight(1, 1));
        }

        [Fact]
        public void SetOverrides_KeepsTheAlignmentAndBackgroundOfACell_AndTakesTheRestNotNamed()
        {
            // D33 o7, o13, o22, o24: Mark (170 5, 7 GH_TXT, 140 3.5) given Invalid kept its alignment (91 1, 170 5); given
            // TextHeight and ContentColor kept its height and alignment, took its style away and was given the colour 0
            // (91 41); a cell of a green background given Invalid kept it (91 6), one of a height of 7 given TextStyle lost it.
            DxfDatabase invalid = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
            Table(invalid).SetOverrides(1, 0, 0, DxfCellProperties.Invalid);
            DxfTable read = Reread(invalid);
            Assert.Equal(DxfCellAlignment.MiddleCenter, read.Alignment(1, 0));
            Assert.Null(read.TextHeight(1, 0));
            Assert.Null(read.TextStyle(1, 0));

            DxfDatabase two = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
            Table(two).SetOverrides(1, 0, 0, DxfCellProperties.TextHeight | DxfCellProperties.ContentColor);
            DxfTable both = Reread(two);
            Assert.Equal(3.5, both.TextHeight(1, 0));
            Assert.Equal(DxfCellAlignment.MiddleCenter, both.Alignment(1, 0));
            Assert.Null(both.TextStyle(1, 0));
            Assert.Equal(DxfColor.ByBlock, both.ContentColor(1, 0));
            Assert.Contains(" 91\n41\n", Record(two.ToText(), "F6"), StringComparison.Ordinal);

            DxfDatabase green = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
            Table(green).SetBackgroundColor(2, 0, Aci(3));
            Table(green).SetTextHeight(2, 0, 7.0);
            Table(green).SetOverrides(2, 0, 0, DxfCellProperties.TextStyle);
            DxfTable kept = Reread(green);
            Assert.Equal(Aci(3), kept.BackgroundColor(2, 0));
            Assert.Null(kept.TextHeight(2, 0));
            Assert.Equal(DxfCellProperties.BackgroundColor | DxfCellProperties.TextStyle, kept.GetOverrides(2, 0, 0));
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

        private static string[] Column(DxfTable table, int col, int from, int to) => Enumerable.Range(from, to - from + 1).Select(r => table.TextString(r, col)).ToArray();

        [Fact]
        public void AFillOfNoneOfContentFormatAndSeries_ChangesNothing()
        {
            // D33 f01 (and D28e v35, f3): AutoCAD 2021's Fill of the options None changed nothing; the library copied the cells.
            DxfDatabase db = Filled();
            string before = db.ToText();
            Table(db).Fill(new DxfCellRange(4, 0, 6, 0), new DxfCellRange(2, 0, 3, 0), DxfTableFillOptions.None);
            Assert.Equal(before, db.ToText());
        }

        [Fact]
        public void AFillThatCopiesContent_CopiesItThoughASeriesIsAskedFor_FromTheFillRangesFirstCell()
        {
            // D33 f03, f06, f08, f16: CopyContent copied 1.5, 2.5, 1.5 down, a series asked for too or reversed; into rows 5 and
            // 6 of a gap 1.5 and 2.5, the source repeated from the fill range's first cell. The library made a series.
            DxfDatabase db = Filled();
            Table(db).Fill(new DxfCellRange(4, 0, 6, 0), new DxfCellRange(2, 0, 3, 0), DxfTableFillOptions.GenerateSeries | DxfTableFillOptions.CopyContent | DxfTableFillOptions.Reverse);
            Assert.Equal(new[] { "1.500000", "2.500000", "1.500000" }, Column(Reread(db), 0, 4, 6));

            DxfDatabase gap = Filled();
            Table(gap).Fill(new DxfCellRange(5, 0, 6, 0), new DxfCellRange(2, 0, 3, 0), DxfTableFillOptions.CopyContent);
            DxfTable read = Reread(gap);
            Assert.Equal(new[] { string.Empty, "1.500000", "2.500000" }, Column(read, 0, 4, 6));
            Assert.Null(read.ContentColor(5, 0));
        }

        [Fact]
        public void ASeries_GoesOnFromTheSourcesLastNumber_OrBackFromItsFirstInReverse_WhereverTheCellsStand()
        {
            // D33 f02, g01 to g03, g05, g26: down, a gap left, or above, a series went on from the last number (3.5, 4.5, 5.5;
            // into rows 5 and 6, 3.5 and 4.5; into rows 2 and 3 above 1.5 and 2.5, 3.5 and 4.5), and in reverse back from the
            // first, the fill range's last cell next to it (-1.5, -0.5, 0.5; 10 and 20 to -20, -10, 0).
            DxfDatabase db = Filled();
            Table(db).Fill(new DxfCellRange(4, 0, 6, 0), new DxfCellRange(2, 0, 3, 0), DxfTableFillOptions.GenerateSeries);
            DxfTable read = Reread(db);
            Assert.Equal(new[] { "3.500000", "4.500000", "5.500000" }, Column(read, 0, 4, 6));
            Assert.Equal(DxfDataType.Double, read.DataType(4, 0));
            Assert.Null(read.ContentColor(4, 0));

            DxfDatabase gap = Filled();
            Table(gap).Fill(new DxfCellRange(5, 0, 6, 0), new DxfCellRange(2, 0, 3, 0), DxfTableFillOptions.GenerateSeries);
            Assert.Equal(new[] { string.Empty, "3.500000", "4.500000" }, Column(Reread(gap), 0, 4, 6));

            DxfDatabase back = Filled();
            Table(back).Fill(new DxfCellRange(4, 0, 6, 0), new DxfCellRange(2, 0, 3, 0), DxfTableFillOptions.GenerateSeries | DxfTableFillOptions.Reverse);
            Assert.Equal(new[] { "-1.500000", "-0.500000", "0.500000" }, Column(Reread(back), 0, 4, 6));

            DxfDatabase whole = Filled();
            Table(whole).Fill(new DxfCellRange(4, 2, 6, 2), new DxfCellRange(2, 2, 3, 2), DxfTableFillOptions.GenerateSeries | DxfTableFillOptions.Reverse);
            Assert.Equal(new[] { "-20", "-10", "0" }, Column(Reread(whole), 2, 4, 6));

            DxfDatabase above = Filled();
            DxfTable moved = Table(above);
            moved.Fill(new DxfCellRange(4, 0, 5, 0), new DxfCellRange(2, 0, 3, 0), DxfTableFillOptions.CopyContent | DxfTableFillOptions.CopyFormat);
            moved.Fill(new DxfCellRange(2, 0, 3, 0), new DxfCellRange(4, 0, 5, 0), DxfTableFillOptions.GenerateSeries);
            Assert.Equal(new[] { "3.500000", "4.500000" }, Column(Reread(above), 0, 2, 3));
        }

        [Fact]
        public void AFillOfTheFormatAlone_GivesTheCellsTheLookAndKindOfTheSource_AndNoValue()
        {
            // D33 f04: CopyFormat alone gave rows 4 and 6 the red of 1.5 and every cell its kind Double, of no value (93 5,
            // 90 2, an empty text).
            DxfDatabase db = Filled();
            Table(db).Fill(new DxfCellRange(4, 0, 6, 0), new DxfCellRange(2, 0, 3, 0), DxfTableFillOptions.CopyFormat);
            DxfTable read = Reread(db);
            Assert.Equal(new[] { string.Empty, string.Empty, string.Empty }, Column(read, 0, 4, 6));
            Assert.Equal(Aci(1), read.ContentColor(4, 0));
            Assert.Null(read.ContentColor(5, 0));
            Assert.Equal(DxfDataType.Double, read.DataType(5, 0));
            Assert.Contains("301\nCELL_VALUE\n 93\n5\n 90\n2\n 94\n0\n300\n\n302\n\n304\n", Record(db.ToText(), "F6"), StringComparison.Ordinal);
        }

        [Fact]
        public void AFill_FillsTheSourcesColumns_AndRefusesARangeThatMeetsTheSource()
        {
            // D33 g10, g24, f09 (and D28e f1, f2): AutoCAD 2021 filled the source's column whatever columns the fill range named,
            // and threw eInvalidInput for a fill range that holds the source.
            DxfDatabase db = Filled();
            Table(db).Fill(new DxfCellRange(4, 1, 6, 1), new DxfCellRange(2, 0, 3, 0), DxfTableFillOptions.CopyContent);
            DxfTable read = Reread(db);
            Assert.Equal(new[] { "1.500000", "2.500000", "1.500000" }, Column(read, 0, 4, 6));
            Assert.Equal(new[] { string.Empty, string.Empty, string.Empty }, Column(read, 1, 4, 6));

            Assert.Throws<ArgumentException>(() => Table(Filled()).Fill(new DxfCellRange(2, 0, 6, 0), new DxfCellRange(2, 0, 3, 0), DxfTableFillOptions.CopyContent));
        }

        #endregion
    }
}
