using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// What followed from AutoCAD 2021's answers to the D33e runs of 2 November 2026 (headless, AutoCAD's own edits saved and
    /// read back): what a fill or a copy takes from a cell emptied in the session, the turn a clear of the overrides takes
    /// away, the looks a row out of its kind's place keeps through a clear, the flag of its kind of value a value set gives,
    /// and the merging groups of a merge and of a new cell. Each drawing is built in the form AutoCAD wrote, read as a file is.
    /// </summary>
    public class DxfTableD33eTests
    {
        private const string None = "93 7 90 0";

        private const string NoneRead = "93 5 90 512";

        private const string Text = "93 6 90 4";

        private static DxfTable Table(DxfDatabase db) => DxfAutoCadChecksD16Tests.Table(db);

        private static DxfTable Reread(DxfDatabase db) => Table(DxfDatabase.Parse(db.ToText()));

        private static DxfColor Aci(short index) => DxfColor.FromColorIndex(DxfColorMethod.ByAci, index);

        private static DxfCellRange Cell(int row, int column) => new DxfCellRange(row, column, row, column);

        private static DxfDatabase Drawn() => DxfDatabase.Parse(DxfDimensionDrawings.R2018);

        /// <summary>The groups of each cell of the table's record, row by row, from its 172 on.</summary>
        private static string[] Cells(DxfDatabase db)
        {
            string record = DxfAutoCadChecksD16Tests.Record(db.ToText(), "F6");
            string[] cells = record.Split(new[] { "\n171\n" }, StringSplitOptions.None);
            var found = new string[cells.Length - 1];
            Array.Copy(cells, 1, found, 0, found.Length);
            return found;
        }

        /// <summary>The flags (93) and kind (90) of the value of a cell, as <c>93 7 90 0</c>.</summary>
        private static string Value(DxfDatabase db, int cell)
        {
            Match m = Regex.Match(Cells(db)[cell], "301\nCELL_VALUE\n 93\n(-?\\d+)\n 90\n(-?\\d+)\n");
            return m.Success ? "93 " + m.Groups[1].Value + " 90 " + m.Groups[2].Value : "-";
        }

        /// <summary>A whole number of a cell's head, before its value: its flags (91), its merging (173, 175, 176).</summary>
        private static int Head(DxfDatabase db, int cell, int code)
        {
            string groups = Cells(db)[cell];
            int value = groups.IndexOf("301\nCELL_VALUE\n", StringComparison.Ordinal);
            Match m = Regex.Match(value >= 0 ? groups.Substring(0, value) : groups, "(?:^|\n) *" + code + "\n(-?\\d+)\n");
            return m.Success ? int.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) : -1;
        }

        /// <summary>The R2018 drawing of a table of 3 rows and 2 columns, each cell given the flag of its kind of value (91 262144), as AutoCAD 2021 writes a cell of a value.</summary>
        private static string ValuedText() => DxfDimensionDrawings.R2018.Replace("\n 91\n0\n178\n", "\n 91\n262144\n178\n").Replace("\n 91\n49\n178\n", "\n 91\n262193\n178\n");

        private static DxfDatabase Valued() => DxfDatabase.Parse(ValuedText());

        /// <summary>That drawing with the TABLECONTENT AutoCAD 2021 writes, which keeps the kind of each row.</summary>
        private static DxfDatabase Kept() => DxfDatabase.Parse(DxfAutoCadChecksD16Tests.LinkedByAutoCad(ValuedText()));

        /// <summary>The R2018 drawing of D33b's table of its own header and data looks, its cells B-12 and M12 holding the data's own looks of 9, GH_TXT, red and top left as their own (D28c o0).</summary>
        private static DxfDatabase RowLooks()
        {
            const string Head = " 93\n0\n 94\n0\n 95\n0\n 96\n0\n141\n10.0\n";
            Assert.Contains(Head, DxfDimensionDrawings.R2018, StringComparison.Ordinal);
            DxfDatabase db = DxfDatabase.Parse(DxfDimensionDrawings.R2018.Replace(Head, " 93\n6886016\n 94\n0\n 95\n0\n 96\n0\n  7\nGH_TXT\n140\n9.0\n170\n1\n 63\n3\n 64\n1\n140\n9.0\n283\n0\n141\n10.0\n"));
            DxfTable table = Table(db);

            for (int column = 0; column < 2; column++)
            {
                table.SetAlignment(2, column, DxfCellAlignment.TopLeft);
                table.SetTextStyle(2, column, db.TextStyleTable["GH_TXT"]);
                table.SetContentColor(2, column, Aci(1));
                table.SetTextHeight(2, column, 9.0);
            }

            return DxfDatabase.Parse(db.ToText());
        }

        /// <summary>The R2018 drawing whose B-12 holds a look of every kind of its own: green behind, red, 5 high, GH_TXT, bottom right, turned 90 degrees, a red line over it (D33c b-r).</summary>
        private static DxfDatabase EveryLook()
        {
            DxfDatabase db = Drawn();
            DxfTable table = Table(db);
            table.SetBackgroundColor(2, 0, Aci(3));
            table.SetContentColor(2, 0, Aci(1));
            table.SetTextHeight(2, 0, 5.0);
            table.SetTextStyle(2, 0, db.TextStyleTable["GH_TXT"]);
            table.SetAlignment(2, 0, DxfCellAlignment.BottomRight);
            table.SetTextRotation(2, 0, DxfRotationAngle.Degrees090);
            table.SetGridColor(2, 0, DxfGridLineType.HorizontalTop, Aci(1));
            return DxfDatabase.Parse(db.ToText());
        }

        #region Fills and copies of a cell emptied in the session

        [Fact]
        public void AFillOrACopyOfACellEmptiedInTheSession_LeavesTheTargetsContentAsItWas()
        {
            // D33d a16, D33e f01, f02, f09 to f12, f14, f17 to f19, g01 to g05, g13: AutoCAD 2021's Fill with CopyContent, with
            // or without CopyFormat, and its CopyFrom of a cell emptied while the drawing was open (reset, given an empty text,
            // put in by SetSize or InsertRows, emptied by a merge undone since, a block cell reset) left the target as it was:
            // Z stayed Z, through a clear of the overrides too. The library wrote the empty cell over it.
            var sources = new List<(Action<DxfTable> Empty, int Row, int Column)>
            {
                (t => t.ResetValue(2, 0), 2, 0),
                (t => t.SetTextString(2, 0, string.Empty), 2, 0),
                (t => t.SetSize(5, 2), 4, 0),
                (t => t.ResetValue(2, 1), 2, 1),
                (t => t.InsertRows(2, 9.0, 1), 2, 0),
                (t => { t.MergeCells(new DxfCellRange(1, 0, 1, 1)); t.UnmergeCells(new DxfCellRange(1, 0, 1, 1)); }, 1, 1),
            };
            var copies = new List<Action<DxfTable, int, int>>
            {
                (t, r, c) => t.Fill(Cell(3, c), Cell(r, c), DxfTableFillOptions.CopyContent),
                (t, r, c) => t.Fill(Cell(3, c), Cell(r, c), DxfTableFillOptions.CopyContent | DxfTableFillOptions.CopyFormat),
                (t, r, c) => t.CopyFrom(t, DxfTableCopyOptions.None, Cell(r, c), Cell(3, c)),
                (t, r, c) => t.CopyFrom(t, DxfTableCopyOptions.SkipFormat, Cell(r, c), Cell(3, c)),
            };

            foreach (var (empty, row, column) in sources)
            {
                foreach (Action<DxfTable, int, int> copy in copies)
                {
                    DxfDatabase db = Valued();
                    DxfTable table = Table(db);
                    empty(table);
                    table.SetSize(Math.Max(table.NumRows, 4), 2);
                    table.SetValue(3, column, "Z");
                    copy(table, row, column);
                    Assert.Equal("Z", Reread(db).TextString(3, column));
                    table.ClearTableStyleOverrides(0);
                    Assert.Equal("Z", Reread(db).TextString(3, column));
                    Assert.Equal(Text, Value(db, 6 + column));
                }
            }

            // g09: a cell reset and given a value again is copied as any other.
            DxfDatabase again = Valued();
            DxfTable valued = Table(again);
            valued.ResetValue(2, 0);
            valued.SetValue(2, 0, "Q");
            valued.SetSize(4, 2);
            valued.SetValue(3, 0, "Z");
            valued.Fill(Cell(3, 0), Cell(2, 0), DxfTableFillOptions.CopyContent);
            Assert.Equal("Q", Reread(again).TextString(3, 0));

            // f06, f07: one read of no value from the file is copied as none, the target keeping its flags (91 262144).
            DxfDatabase read = Valued();
            Table(read).SetTextString(2, 0, string.Empty);
            read = DxfDatabase.Parse(read.ToText());
            Table(read).SetSize(4, 2);
            Table(read).SetValue(3, 0, "Z");
            Table(read).Fill(Cell(3, 0), Cell(2, 0), DxfTableFillOptions.CopyContent);
            Assert.Equal(None, Value(read, 6));
            Assert.Equal(0x40000, Head(read, 6, 91));
        }

        [Fact]
        public void AFormatCopiedFromACellEmptiedInTheSession_IsTheCellsLookAlone()
        {
            // D33e g06, g07, g16: AutoCAD 2021 gave the target of a CopyFormat fill or a CopyFrom of B-12, reset of the table of
            // its own data looks, the alignment B-12 kept (top left, 91 262145) and nothing of the looks of its content, the
            // target keeping its kind of value; and Mark, given B-12 of no looks by a CopyFrom of everything, lost its middle
            // centre and kept its content, GH_TXT and 3.5. The library copied the text style, colour and height too, and took
            // the target's kind of value away.
            foreach (bool fill in new[] { true, false })
            {
                DxfDatabase db = RowLooks();
                DxfTable table = Table(db);
                table.ResetValue(2, 0);
                table.SetSize(4, 2);
                table.SetValue(3, 0, "Z");

                if (fill)
                {
                    table.Fill(Cell(3, 0), Cell(2, 0), DxfTableFillOptions.CopyFormat);
                }
                else
                {
                    table.CopyFrom(table, DxfTableCopyOptions.SkipContent, Cell(2, 0), Cell(3, 0));
                }

                DxfTable read = Reread(db);
                Assert.Equal("Z", read.TextString(3, 0));
                Assert.Equal(DxfCellAlignment.TopLeft, read.Alignment(3, 0));
                Assert.Equal(DxfCellProperties.DataType | DxfCellProperties.DataFormat | DxfCellProperties.Alignment, read.GetOverrides(3, 0, 0));
                Assert.Equal(0x40001, Head(db, 6, 91));
            }

            DxfDatabase header = Drawn();
            Table(header).ResetValue(2, 0);
            Table(header).CopyFrom(Table(header), DxfTableCopyOptions.None, Cell(2, 0), Cell(1, 0));
            DxfTable mark = Reread(header);
            Assert.Equal("Mark", mark.TextString(1, 0));
            Assert.Equal(3.5, mark.TextHeight(1, 0));
            Assert.Equal(DxfCellProperties.TextStyle | DxfCellProperties.TextHeight, mark.GetOverrides(1, 0, 0));
        }

        [Fact]
        public void ASeriesFromACellOfNoValue_EmptiesTheTarget_OfNoKind_AndNotInTheSession()
        {
            // D33e f04, f05, g08, g12, g15, g20, g21: AutoCAD 2021's Fill with GenerateSeries, CopyContent with it or not, from a
            // cell of no value, reset or read so, emptied the target of its flag of a kind of value (91 0), and a clear of the
            // overrides wrote it as one read of no value (93 5, 90 512). The library kept the flag, or gave it.
            foreach (DxfTableFillOptions options in new[] { DxfTableFillOptions.GenerateSeries, DxfTableFillOptions.GenerateSeries | DxfTableFillOptions.CopyContent })
            {
                foreach (bool reset in new[] { true, false })
                {
                    DxfDatabase db = Valued();

                    if (reset)
                    {
                        Table(db).ResetValue(2, 0);
                    }
                    else
                    {
                        Table(db).SetTextString(2, 0, string.Empty);
                        db = DxfDatabase.Parse(db.ToText());
                    }

                    DxfTable table = Table(db);
                    table.SetSize(4, 2);
                    table.SetValue(3, 0, "Z");
                    table.Fill(Cell(3, 0), Cell(2, 0), options);
                    Assert.Equal(None, Value(db, 6));
                    Assert.Equal(0, Head(db, 6, 91));
                    table.ClearTableStyleOverrides(0);
                    Assert.Equal(NoneRead, Value(db, 6));
                }
            }

            // g14: a cell put in by SetSize given the whole of one read of no value is written as one read so when cleared.
            DxfDatabase whole = Valued();
            Table(whole).SetTextString(2, 0, string.Empty);
            whole = DxfDatabase.Parse(whole.ToText());
            Table(whole).SetSize(4, 2);
            Table(whole).Fill(Cell(3, 0), Cell(2, 0), DxfTableFillOptions.CopyContent | DxfTableFillOptions.CopyFormat);
            Table(whole).ClearTableStyleOverrides(0);
            Assert.Equal(NoneRead, Value(whole, 6));
            Assert.Equal(None, Value(whole, 7));
        }

        #endregion

        #region The turn a clear takes

        [Fact]
        public void ClearingTheCellsOverrides_OrACellsOwn_TurnsItsTextAndItsBlockBackToNought()
        {
            // D33d b01, D33e t01 to t11: AutoCAD 2021's ClearTableStyleOverrides of 0 or 2 and its RemoveAllOverrides set the
            // turn of a cell's text (145 of 1.5708 on B-12) and of a block cell's block (0.5 on M12) to nought, whether set in
            // the session or read so; of 1 it kept them. RemoveAllOverrides also took the block cell's flag of its kind of value
            // (91 0). The library kept the turns.
            foreach (Action<DxfTable> clear in new Action<DxfTable>[] { t => t.ClearTableStyleOverrides(0), t => t.ClearTableStyleOverrides(2), t => t.RemoveAllOverrides(2, 0) })
            {
                DxfDatabase db = EveryLook();
                Table(db).ResetValue(2, 1);
                clear(Table(db));
                Assert.Equal(DxfRotationAngle.Degrees000, Reread(db).TextRotation(2, 0));
            }

            DxfDatabase kept = EveryLook();
            Table(kept).ClearTableStyleOverrides(1);
            Assert.Equal(DxfRotationAngle.Degrees090, Reread(kept).TextRotation(2, 0));

            foreach (Action<DxfTable> clear in new Action<DxfTable>[] { t => t.ClearTableStyleOverrides(0), t => t.ClearTableStyleOverrides(2), t => t.RemoveAllOverrides(2, 1) })
            {
                DxfDatabase db = Valued();
                Table(db).SetBlockRotation(2, 1, 0.5);
                db = DxfDatabase.Parse(db.ToText());
                clear(Table(db));
                Assert.Equal(0.0, Reread(db).BlockRotation(2, 1));
                Assert.Equal(0, Head(db, 5, 91));
            }

            // A text turned in the session alone is taken back by RemoveAllOverrides, though it holds no other override.
            DxfDatabase turned = Drawn();
            Table(turned).SetTextRotation(2, 0, DxfRotationAngle.Degrees090);
            Table(turned).RemoveAllOverrides(2, 0);
            Assert.Equal(DxfRotationAngle.Degrees000, Reread(turned).TextRotation(2, 0));
        }

        #endregion

        #region Rows out of their kind's place

        [Fact]
        public void ARowOutOfItsKindsPlace_KeepsItsKindsLooks_AndTheTableItsTitleAndHeaderLeftOut_ThroughAClear()
        {
            // D33d s08, D33e m01 to m10, n01, n02, n06, n09: AutoCAD 2021, the title and the header moved down by a row put in
            // before them, kept through ClearTableStyleOverrides of every option the title's and header's being left out
            // (93 3, 280 1, 281 1), and through a clear of 0 or 2 and RemoveAllOverrides the looks of their kinds as their
            // cells' own (170 5 and 140 6.0, the header 170 5), as it did for a row put in taking the title's look; it then
            // merged the new first row on load, the head telling it the title was left out. The library took both away.
            foreach (Action<DxfTable> clear in new Action<DxfTable>[]
            {
                t => t.ClearTableStyleOverrides(0),
                t => t.ClearTableStyleOverrides(1),
                t => t.ClearTableStyleOverrides(2),
                t => { t.RemoveAllOverrides(1, 0); t.RemoveAllOverrides(2, 0); },
            })
            {
                DxfDatabase db = Kept();
                Table(db).SetTextString(2, 0, string.Empty);
                Table(db).InsertRows(0, 9.0, 1);
                clear(Table(db));
                Assert.True(Table(db).IsTitleSuppressed);
                Assert.True(Table(db).IsHeaderSuppressed);
                Assert.Equal(0x40021, Head(db, 2, 91));
                Assert.Equal(0x40021, Head(db, 3, 91));
                Assert.Equal(0x40001, Head(db, 5, 91));

                DxfTable read = Reread(db);
                Assert.Equal(DxfCellAlignment.MiddleCenter, read.Alignment(1, 0));
                Assert.Equal(6.0, read.TextHeight(1, 0));
                Assert.Equal(DxfCellAlignment.MiddleCenter, read.Alignment(2, 1));
            }

            // n06: a row put in taking the title's look keeps it so too.
            foreach (Action<DxfTable> clear in new Action<DxfTable>[] { t => t.ClearTableStyleOverrides(0), t => t.ClearTableStyleOverrides(2) })
            {
                DxfDatabase db = Kept();
                Table(db).InsertRowsAndInherit(3, 0, 1);
                clear(Table(db));
                DxfTable read = Reread(db);
                Assert.Equal(DxfCellAlignment.MiddleCenter, read.Alignment(3, 0));
                Assert.Equal(6.0, read.TextHeight(3, 0));
                Assert.False(read.IsTitleSuppressed);
            }
        }

        #endregion

        #region What a value set and a merge write

        [Fact]
        public void AValueSet_GivesTheCellTheFlagOfItsKindOfValue()
        {
            // D33d a17, b02, D33e w01, w03 to w06, w09, w10: AutoCAD 2021's SetValue, of a text or a number, and its
            // SetTextString gave a cell of no flags, reset or read of no value, the flag of its kind of value (91 262144); the
            // library wrote 91 0.
            foreach (Action<DxfTable> set in new Action<DxfTable>[] { t => t.SetValue(2, 0, "Z"), t => t.SetValue(2, 0, 2.5), t => t.SetTextString(2, 0, "Z") })
            {
                DxfDatabase reset = Valued();
                Table(reset).ResetValue(2, 0);
                set(Table(reset));
                Assert.Equal(0x40000, Head(reset, 4, 91));

                DxfDatabase read = Drawn();
                Assert.Equal(0, Head(read, 4, 91));
                set(Table(read));
                Assert.Equal(0x40000, Head(read, 4, 91));
            }

            DxfDatabase block = Valued();
            Table(block).ResetValue(2, 1);
            Table(block).SetValue(2, 1, "Z");
            Assert.Equal(0x40000, Head(block, 5, 91));

            // Before R2007 a cell holds its text alone, of no such flag.
            DxfDatabase old = DxfDatabase.Parse(DxfDimensionDrawings.R2000);
            string before = DxfAutoCadChecksD16Tests.Record(old.ToText(), "F6");
            Table(old).SetValue(2, 0, "Z");
            Assert.Equal(before.Replace("\n  1\nB-12\n", "\n  1\nZ\n"), DxfAutoCadChecksD16Tests.Record(old.ToText(), "F6"));
        }

        [Fact]
        public void AMergeAndANewCell_WriteTheirMergingGroupsAsAutoCadDoes()
        {
            // D33d a02 to a13, D33e w07, w08, w11: AutoCAD 2021 wrote each cell put in by SetSize, InsertRows or InsertColumns
            // 175 1 and 176 1, a merge's top left cell 173 0 with its spans, its other cells 173 1, 175 1, 176 1, and a cell
            // unmerged 175 1, 176 1; the library wrote 175 0, 176 0 and the top left 173 1. AutoCAD read both alike.
            DxfDatabase db = Drawn();
            DxfTable table = Table(db);
            table.SetSize(4, 2);
            Assert.Equal(new[] { 0, 0, 1, 1 }, new[] { Head(db, 6, 173), Head(db, 6, 174), Head(db, 6, 175), Head(db, 6, 176) });
            Assert.Equal(1, Head(db, 7, 175));
            Assert.Equal(1, Head(db, 7, 176));

            table.MergeCells(new DxfCellRange(3, 0, 3, 1));
            Assert.Equal(new[] { 0, 2, 1 }, new[] { Head(db, 6, 173), Head(db, 6, 175), Head(db, 6, 176) });
            Assert.Equal(new[] { 1, 1, 1 }, new[] { Head(db, 7, 173), Head(db, 7, 175), Head(db, 7, 176) });
            Assert.Equal(new DxfCellRange(3, 0, 3, 1), Reread(db).GetMergeRange(3, 1));

            table.UnmergeCells(new DxfCellRange(3, 0, 3, 1));
            Assert.Equal(new[] { 0, 1, 1 }, new[] { Head(db, 6, 173), Head(db, 6, 175), Head(db, 6, 176) });
            Assert.Equal(new[] { 0, 1, 1 }, new[] { Head(db, 7, 173), Head(db, 7, 175), Head(db, 7, 176) });
            Assert.False(Reread(db).IsMergedCell(3, 0, out _));
        }

        #endregion
    }
}
