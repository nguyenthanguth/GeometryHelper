using System;
using System.Globalization;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// What followed from AutoCAD 2021's answers to the D33c runs of 2 November 2026 (headless, AutoCAD's own edits read in
    /// memory and saved, and 464 fills read in memory): the rows AutoCAD merges when it loads a file, written at every edit,
    /// fills into a hidden cell and above their source, the looks <c>ResetValue</c> takes away, and the hidden cell of a title
    /// moved down. Each drawing is built in the form AutoCAD wrote, read as a file is.
    /// </summary>
    public class DxfTableD33cTests
    {
        private static DxfTable Table(DxfDatabase db) => DxfAutoCadChecksD16Tests.Table(db);

        private static DxfTable Reread(DxfDatabase db) => Table(DxfDatabase.Parse(db.ToText()));

        private static string Record(string text, string handle) => DxfAutoCadChecksD16Tests.Record(text, handle);

        private static DxfColor Aci(short index) => DxfColor.FromColorIndex(DxfColorMethod.ByAci, index);

        /// <summary>The R2018 drawing of a table of 3 rows and 2 columns, its title Parts unmerged, as read from a file.</summary>
        private static DxfDatabase Unmerged()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
            Table(db).UnmergeCells(new DxfCellRange(0, 0, 0, 1));
            return DxfDatabase.Parse(db.ToText());
        }

        #region Rows merged on load

        [Fact]
        public void ATitleReadUnmerged_IsWrittenMergedAcross_AfterAnyEdit()
        {
            // D33c e01 to e20 (and D33b m30 to m32): AutoCAD 2021 merged the title across the table when it loaded the file and
            // wrote it merged whatever was done to the table, a row put in, a value set, a size, a height, a move; the library
            // merged it before DeleteRows and DeleteColumns alone, and wrote it unmerged after the rest.
            foreach (Action<DxfTable> edit in new Action<DxfTable>[]
            {
                t => t.InsertRows(3, 9.0, 1),
                t => t.SetValue(2, 0, "Z"),
                t => t.SetSize(4, 2),
                t => t.SetRowHeight(2, 12.0),
                t => t.SetTextHeight(2, 0, 5.0),
                t => t.SetGridColor(2, 0, DxfGridLineType.HorizontalTop, Aci(1)),
                t => t.TransformBy(GeoTransform3.Translation(new GeoVector3(10.0, 0.0, 0.0))),
            })
            {
                DxfDatabase db = Unmerged();
                edit(Table(db));
                DxfTable read = Reread(db);
                Assert.Equal(new DxfCellRange(0, 0, 0, 1), read.GetMergeRange(0, 1));
                Assert.Equal("Parts", read.TextString(0, 0));
                Assert.Equal(string.Empty, read.TextString(0, 1));
            }

            // e07, e08: a value set into the cell the merge hides is written there, the title merged still.
            DxfDatabase hidden = Unmerged();
            Table(hidden).SetValue(0, 1, "X");
            DxfTable shown = Reread(hidden);
            Assert.Equal(new DxfCellRange(0, 0, 0, 1), shown.GetMergeRange(0, 1));
            Assert.Equal("X", shown.TextString(0, 1));
        }

        [Fact]
        public void ATitleUnmergedInTheSameSession_StaysUnmerged()
        {
            // D33c e06: AutoCAD merged the title once, when it loaded the file; unmerged and given X beside Parts, it wrote it
            // unmerged. A guard of the merge made once, at the table's first edit.
            DxfDatabase db = Unmerged();
            DxfTable table = Table(db);
            table.UnmergeCells(new DxfCellRange(0, 0, 0, 1));
            table.SetValue(0, 1, "X");
            DxfTable read = Reread(db);
            Assert.Equal(new DxfCellRange(0, 1, 0, 1), read.GetMergeRange(0, 1));
            Assert.Equal("X", read.TextString(0, 1));
        }

        [Fact]
        public void AFillIntoACellAMergeHides_LeavesIt()
        {
            // D33b f12, D33c e09, e15, e16: AutoCAD 2021's Fill left a cell hidden by a merge as it was, the title's second cell
            // merged on load and filled from Parts, and the second cell of a block merged in the same session; the library
            // filled it.
            DxfDatabase title = Unmerged();
            Table(title).Fill(new DxfCellRange(0, 1, 0, 1), new DxfCellRange(0, 0, 0, 0), DxfTableFillOptions.CopyContent | DxfTableFillOptions.Row);
            DxfTable read = Reread(title);
            Assert.Equal(new DxfCellRange(0, 0, 0, 1), read.GetMergeRange(0, 1));
            Assert.Equal(string.Empty, read.TextString(0, 1));

            DxfDatabase block = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
            DxfTable table = Table(block);
            table.SetSize(5, 2);
            table.SetValue(3, 0, "A");
            table.MergeCells(new DxfCellRange(3, 0, 4, 0));
            table.Fill(new DxfCellRange(4, 0, 4, 0), new DxfCellRange(2, 0, 2, 0), DxfTableFillOptions.CopyContent);
            Assert.Equal(string.Empty, Reread(block).TextString(4, 0));
        }

        #endregion

        #region Fills above their source

        /// <summary>The R2018 drawing given 16 rows of 14 columns, each cell of data r5c3 by its place, the block cell left.</summary>
        private static DxfDatabase Grid()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
            DxfTable table = Table(db);
            table.SetSize(16, 14);

            for (int r = 2; r < 16; r++)
            {
                for (int c = 0; c < 14; c++)
                {
                    if (r != 2 || c != 1)
                    {
                        table.SetValue(r, c, "r" + r.ToString(CultureInfo.InvariantCulture) + "c" + c.ToString(CultureInfo.InvariantCulture));
                    }
                }
            }

            return DxfDatabase.Parse(db.ToText());
        }

        private static string[] Filled(DxfCellRange fill, DxfCellRange source, DxfTableFillOptions options, bool row, int line, int from, int to)
        {
            DxfDatabase db = Grid();
            Table(db).Fill(fill, source, options);
            DxfTable read = Reread(db);
            var texts = new string[to - from + 1];

            for (int k = from; k <= to; k++)
            {
                texts[k - from] = row ? read.TextString(line, k) : read.TextString(k, line);
            }

            return texts;
        }

        [Fact]
        public void AFillAboveItsSourceLongerThanIt_RepeatsTheSourceBackFromTheFillsLastCell()
        {
            // D33c h-u, h-l and f-u, f-l (464 fills of 1 to 4 sources by 1 to 7 cells): above (or left of) its source, a fill
            // longer than the source took the source's last cell into its own last, and went back through the source from
            // there, 2.5 and 3.5 filled into rows 10 to 12 giving 3.5, 2.5, 3.5; the library went on from the fill's first cell.
            const DxfTableFillOptions Content = DxfTableFillOptions.CopyContent;
            Assert.Equal(new[] { "r15c3", "r14c3", "r15c3" }, Filled(new DxfCellRange(10, 3, 12, 3), new DxfCellRange(14, 3, 15, 3), Content, false, 3, 10, 12));
            Assert.Equal(new[] { "r12c3", "r10c3", "r11c3", "r12c3" }, Filled(new DxfCellRange(6, 3, 9, 3), new DxfCellRange(10, 3, 12, 3), Content, false, 3, 6, 9));
            Assert.Equal(new[] { "r5c11", "r5c12", "r5c13", "r5c10", "r5c11", "r5c12", "r5c13" }, Filled(new DxfCellRange(5, 3, 5, 9), new DxfCellRange(5, 10, 5, 13), Content | DxfTableFillOptions.Row, true, 5, 3, 9));

            // One no longer than the source goes on from its first cell, as before (h-x01, h-x14, f-u-n3-L2), and so does a fill
            // below or right of it, to the table's end (h-x21, h-x22).
            Assert.Equal(new[] { "r5c2", "r5c3" }, Filled(new DxfCellRange(5, 0, 5, 1), new DxfCellRange(5, 2, 5, 4), Content | DxfTableFillOptions.Row, true, 5, 0, 1));
            Assert.Equal(new[] { "r5c3", "r6c3", "r7c3" }, Filled(new DxfCellRange(2, 3, 4, 3), new DxfCellRange(5, 3, 8, 3), Content, false, 3, 2, 4));
            Assert.Equal(new[] { "r5c6", "r5c7", "r5c8", "r5c6", "r5c7" }, Filled(new DxfCellRange(5, 9, 5, 13), new DxfCellRange(5, 6, 5, 8), Content | DxfTableFillOptions.Row, true, 5, 9, 13));
        }

        [Fact]
        public void AFillAboveItsSource_LeavesTheCellsOfARepeatThatWouldReachBeforeTheTablesFirstRow()
        {
            // D33c h-l-n4-L5..7-a3, h-x07, h-x11, h-x15, h-x18, h-x19, and D33b p6, p10: AutoCAD 2021 repeated the source in
            // whole lengths of it back from the fill's last cell, and left as they were the cells of a repeat that would reach
            // past the table's first column (or row): four sources from column 7 filled into columns 0 to 6 filled 3 to 6 and
            // left 0 to 2; the library filled them all.
            const DxfTableFillOptions Along = DxfTableFillOptions.CopyContent | DxfTableFillOptions.Row;
            Assert.Equal(new[] { "r5c0", "r5c1", "r5c2", "r5c7", "r5c8", "r5c9", "r5c10" }, Filled(new DxfCellRange(5, 0, 5, 6), new DxfCellRange(5, 7, 5, 10), Along, true, 5, 0, 6));
            Assert.Equal(new[] { "r5c0", "r5c3", "r5c4" }, Filled(new DxfCellRange(5, 0, 5, 2), new DxfCellRange(5, 3, 5, 4), Along | DxfTableFillOptions.CopyFormat, true, 5, 0, 2));
            Assert.Equal(new[] { "r5c0", "r5c1", "r5c5", "r5c6", "r5c7" }, Filled(new DxfCellRange(5, 0, 5, 4), new DxfCellRange(5, 5, 5, 7), Along, true, 5, 0, 4));
            Assert.Equal(new[] { string.Empty, "r5c3", "r6c3", "r7c3" }, Filled(new DxfCellRange(1, 3, 4, 3), new DxfCellRange(5, 3, 7, 3), DxfTableFillOptions.CopyContent, false, 3, 1, 4));
        }

        #endregion

        #region ResetValue

        [Fact]
        public void ResetValue_TakesTheContentsLooksAway_AndKeepsTheCellsOwn()
        {
            // D33c r02, r03, r06: AutoCAD 2021's ResetValue took a cell's text style, text height and colour (7, 140, 64 and
            // their flags) and its turn (145) away with its value, and kept its alignment, background and lines; Mark kept
            // middle centre alone. The library kept them all.
            DxfDatabase db = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
            DxfTable table = Table(db);
            table.SetBackgroundColor(2, 0, Aci(3));
            table.SetContentColor(2, 0, Aci(1));
            table.SetTextHeight(2, 0, 5.0);
            table.SetTextStyle(2, 0, db.TextStyleTable["GH_TXT"]);
            table.SetAlignment(2, 0, DxfCellAlignment.BottomRight);
            table.SetTextRotation(2, 0, DxfRotationAngle.Degrees090);
            table.SetGridColor(2, 0, DxfGridLineType.HorizontalTop, Aci(1));
            db = DxfDatabase.Parse(db.ToText());
            Table(db).ResetValue(2, 0);
            Table(db).ResetValue(1, 0);

            DxfTable read = Reread(db);
            Assert.Equal(DxfCellProperties.Alignment | DxfCellProperties.BackgroundColor, read.GetOverrides(2, 0, 0));
            Assert.Null(read.TextHeight(2, 0));
            Assert.Null(read.ContentColor(2, 0));
            Assert.Null(read.TextStyle(2, 0));
            Assert.Equal(DxfRotationAngle.Degrees000, read.TextRotation(2, 0));
            Assert.Equal(DxfCellAlignment.BottomRight, read.Alignment(2, 0));
            Assert.Equal(Aci(3), read.BackgroundColor(2, 0));
            Assert.Equal(Aci(1), read.GetGridColor(2, 0, DxfGridLineType.HorizontalTop));
            Assert.Equal(DxfCellProperties.Alignment, read.GetOverrides(1, 0, 0));
            Assert.Null(read.TextHeight(1, 0));
            Assert.Equal(DxfCellAlignment.MiddleCenter, read.Alignment(1, 0));
        }

        /// <summary>The R2018 drawing of D33b's table of its own header and data looks, its cell B-12 holding the data's own looks of 9, GH_TXT, red and top left as its own (D28c o0).</summary>
        private static DxfDatabase RowLooks()
        {
            const string Head = " 93\n0\n 94\n0\n 95\n0\n 96\n0\n141\n10.0\n";
            Assert.Contains(Head, DxfDimensionDrawings.R2018, StringComparison.Ordinal);
            DxfDatabase db = DxfDatabase.Parse(DxfDimensionDrawings.R2018.Replace(Head, " 93\n6886016\n 94\n0\n 95\n0\n 96\n0\n  7\nGH_TXT\n140\n9.0\n170\n1\n 63\n3\n 64\n1\n140\n9.0\n283\n0\n141\n10.0\n"));
            DxfTable table = Table(db);
            table.SetAlignment(2, 0, DxfCellAlignment.TopLeft);
            table.SetTextStyle(2, 0, db.TextStyleTable["GH_TXT"]);
            table.SetContentColor(2, 0, Aci(1));
            table.SetTextHeight(2, 0, 9.0);
            return DxfDatabase.Parse(db.ToText());
        }

        [Fact]
        public void ResetValue_KeepsTheLooksTheTableGivesItsRow_WhichClearingTheTablesOverridesTakesAway()
        {
            // D33c r01, D33b c16 to c18: AutoCAD 2021's ResetValue kept B-12's 7 GH_TXT, 64 1 and 140 9.0, the table's own looks
            // of the data (91 57); cleared of the table's own overrides after it (1), the cell lost them with the table, keeping
            // its alignment (91 1); cleared of the cells' (2) or both (0), it kept its value of none 93 7, 90 0, where the
            // library wrote 93 5, 90 512 and kept the looks.
            DxfDatabase kept = RowLooks();
            Table(kept).ResetValue(2, 0);
            DxfTable read = Reread(kept);
            Assert.Equal(DxfCellProperties.Alignment | DxfCellProperties.ContentColor | DxfCellProperties.TextStyle | DxfCellProperties.TextHeight, read.GetOverrides(2, 0, 0));
            Assert.Equal(9.0, read.TextHeight(2, 0));

            DxfDatabase table = RowLooks();
            Table(table).ResetValue(2, 0);
            Table(table).ClearTableStyleOverrides(1);
            DxfTable cleared = Reread(table);
            Assert.Equal(DxfCellProperties.Alignment, cleared.GetOverrides(2, 0, 0));
            Assert.Null(cleared.TextHeight(2, 0));
            Assert.Equal(DxfCellAlignment.TopLeft, cleared.Alignment(2, 0));

            foreach (int options in new[] { 0, 2 })
            {
                DxfDatabase db = RowLooks();
                Table(db).ResetValue(2, 0);
                Table(db).ClearTableStyleOverrides(options);
                string record = Record(db.ToText(), "F6");
                Assert.Contains("301\nCELL_VALUE\n 93\n7\n 90\n0\n 94\n0\n300\n\n302\n\n304\n", record, StringComparison.Ordinal);
            }
        }

        #endregion

        #region A title moved down

        [Fact]
        public void AMergeOfATitleMovedDown_LeavesItsHiddenCellTheTitlesLooks()
        {
            // D33c k01, k02, k05 and D33b m18, m23: a row of the title's kind moved down by a row put in before it, merged across
            // in the same session or on load, kept 170 5 and 140 6.0 in its hidden cell (91 33), even where that cell held a
            // height of its own, 3.0; the library took 140 away with the content. The drawing is the one AutoCAD linked, whose
            // TABLECONTENT keeps each row's kind.
            DxfDatabase db = DxfDatabase.Parse(DxfAutoCadChecksD16Tests.LinkedByAutoCad(DxfDimensionDrawings.R2018));
            DxfTable table = Table(db);
            table.InsertRows(0, 9.0, 1);
            table.UnmergeCells(new DxfCellRange(1, 0, 1, 1));
            table.SetTextHeight(1, 1, 3.0);
            table.MergeCells(new DxfCellRange(1, 0, 1, 1));
            DxfTable read = Reread(db);
            Assert.Equal(6.0, read.TextHeight(1, 1));
            Assert.Equal(new DxfCellRange(1, 0, 1, 1), read.GetMergeRange(1, 1));

            DxfDatabase loaded = DxfDatabase.Parse(DxfAutoCadChecksD16Tests.LinkedByAutoCad(DxfDimensionDrawings.R2018));
            DxfTable moved = Table(loaded);
            moved.SetSize(4, 2);
            moved.InsertRows(0, 9.0, 1);
            moved.UnmergeCells(new DxfCellRange(1, 0, 1, 1));
            loaded = DxfDatabase.Parse(loaded.ToText());
            Table(loaded).DeleteRows(4, 1);
            DxfTable merged = Reread(loaded);
            Assert.Equal(new DxfCellRange(1, 0, 1, 1), merged.GetMergeRange(1, 1));
            Assert.Equal(6.0, merged.TextHeight(1, 1));
        }

        #endregion
    }
}
