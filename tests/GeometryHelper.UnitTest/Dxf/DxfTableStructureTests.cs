using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GeometryHelper.Dxf;
using Xunit;
using static GeometryHelper.UnitTest.Dxf.DxfModel;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The structure of the ACAD_TABLE of <see cref="DxfDimensionDrawings"/> (R2000, R2007 and R2018, three rows 10, 8 and 8
    /// high, two columns 40 and 30 wide, its title Parts merged across both, a cell of overrides and a block cell of BOLT)
    /// changed as AutoCAD's <c>Table</c> changes it: rows and columns put in and taken out, cells merged, the lines of its
    /// grid, formulas, cells copied and filled, each written and read back.
    /// </summary>
    public class DxfTableStructureTests
    {
        // The drawing of R2018 read as one of R2007, whose cells AutoCAD writes in the same form.
        private static readonly string R2007 = DxfDimensionDrawings.R2018.Replace("$ACADVER\n  1\nAC1032\n", "$ACADVER\n  1\nAC1021\n");

        private static readonly string[] Versions = { DxfDimensionDrawings.R2000, R2007, DxfDimensionDrawings.R2018 };

        private static DxfTable Table(DxfDatabase db) => db.ModelSpace.OfType<DxfTable>().Single();

        private static string TableRecord(string text) => Records(text).Single(r => HandleOf(r) == "F6");

        private static DxfColor Aci(short index) => DxfColor.FromColorIndex(DxfColorMethod.ByAci, index);

        private static DxfTable Reread(DxfDatabase db) => Table(DxfDatabase.Parse(db.ToText()));

        private static double[] Heights(DxfTable table) => Enumerable.Range(0, table.NumRows).Select(table.RowHeight).ToArray();

        private static double[] Widths(DxfTable table) => Enumerable.Range(0, table.NumColumns).Select(table.ColumnWidth).ToArray();

        private static string[] Texts(DxfTable table, int column) => Enumerable.Range(0, table.NumRows).Select(r => table.TextString(r, column)).ToArray();

        [Fact]
        public void RowsPutIn_TakeTheirPlaceAndHeight_AndTheRowsBelowMoveDown()
        {
            foreach (string text in Versions)
            {
                DxfDatabase db = DxfDatabase.Parse(text);
                DxfTable table = Table(db);
                Assert.True(table.CanInsertRow(3));
                Assert.False(table.CanInsertRow(4));

                table.InsertRows(2, 6.0, 2);

                string saved = db.ToText();
                OnlyTheseChanged(text, saved, "F6");
                Assert.Contains(" 91\n5\n 92\n2\n", TableRecord(saved), StringComparison.Ordinal);
                DxfDatabase readDb = DxfDatabase.Parse(saved);
                DxfTable read = Table(readDb);
                Assert.Equal(5, read.NumRows);
                Assert.Equal(new[] { 10.0, 8.0, 6.0, 6.0, 8.0 }, Heights(read));
                Assert.Equal(new[] { "Parts", "Mark", string.Empty, string.Empty, "B-12" }, Texts(read, 0));
                Assert.Equal(DxfTableCellType.BlockCell, read.CellType(4, 1));
                Assert.Equal("M12", read.GetBlockAttributeValue(4, 1, readDb.BlockTable["BOLT"].OfType<DxfAttributeDefinition>().Single()));
                Assert.Equal(DxfCellAlignment.MiddleCenter, read.Alignment(1, 0));
                Assert.Null(read.Alignment(2, 0));
                Assert.Equal(DxfTableCellType.TextCell, read.CellType(3, 1));
                Assert.Equal(new DxfCellRange(0, 0, 0, 1), read.GetMergeRange(0, 1));
                Assert.False(read.IsMergedCell(2, 0, out _));
                Assert.Equal(26.0 + 12.0, read.Height);

                // A new cell is of the form of its file: its text in 1 before R2007, in its value from R2007 on.
                bool r2000 = text == DxfDimensionDrawings.R2000;
                string fresh = r2000
                    ? "171\n1\n172\n0\n173\n0\n174\n0\n175\n0\n176\n0\n177\n0\n178\n0\n145\n0.0\n  1\n\n171\n"
                    : "171\n1\n172\n0\n173\n0\n174\n0\n175\n0\n176\n0\n 91\n0\n178\n0\n145\n0.0\n 92\n0\n301\nCELL_VALUE\n 93\n6\n 90\n4\n  1\n\n 94\n0\n300\n\n302\n\n304\nACVALUE_END\n171\n";
                Assert.Contains(fresh, TableRecord(saved), StringComparison.Ordinal);

                // Rows put in after the last, and before the first, which becomes the title.
                read.InsertRows(5, 4.0, 1);
                read.InsertRows(0, 12.0, 1);
                DxfTable again = Reread(readDb);
                Assert.Equal(new[] { 12.0, 10.0, 8.0, 6.0, 6.0, 8.0, 4.0 }, Heights(again));
                Assert.Equal("Parts", again.TextString(1, 0));
                Assert.Equal(new DxfCellRange(1, 0, 1, 1), again.GetMergeRange(1, 1));

                Refused<ArgumentOutOfRangeException>(() => read.InsertRows(8, 6.0, 1), "go before one");
                Refused<ArgumentOutOfRangeException>(() => read.InsertRows(0, 6.0, 0), "one or more");
                Refused<ArgumentOutOfRangeException>(() => read.InsertRows(0, 0.0, 1), "above nought");
            }
        }

        [Fact]
        public void ColumnsPutIn_WithinTheTitle_GrowItsMerge()
        {
            foreach (string text in Versions)
            {
                DxfDatabase db = DxfDatabase.Parse(text);
                DxfTable table = Table(db);

                table.InsertColumns(1, 25.0, 1);

                DxfDatabase readDb = DxfDatabase.Parse(db.ToText());
                DxfTable read = Table(readDb);
                Assert.Equal(3, read.NumColumns);
                Assert.Equal(new[] { 40.0, 25.0, 30.0 }, Widths(read));
                Assert.Equal(new DxfCellRange(0, 0, 0, 2), read.GetMergeRange(0, 2));
                Assert.Equal("Qty", read.TextString(1, 2));
                Assert.Equal(string.Empty, read.TextString(1, 1));
                Assert.Equal(DxfTableCellType.BlockCell, read.CellType(2, 2));
                Assert.Contains("175\n3\n176\n1\n", TableRecord(readDb.ToText()), StringComparison.Ordinal);

                // Put in after the last, the title is left as it was.
                read.InsertColumns(3, 15.0, 2);
                DxfTable again = Reread(readDb);
                Assert.Equal(new[] { 40.0, 25.0, 30.0, 15.0, 15.0 }, Widths(again));
                Assert.Equal(new DxfCellRange(0, 0, 0, 2), again.GetMergeRange(0, 0));
                Assert.False(again.IsMergedCell(0, 3, out _));
            }
        }

        [Fact]
        public void RowsAndColumnsTakenOut_ShrinkTheBlocksTheyCross_AndOneLeftOfOneCellIsUnmerged()
        {
            foreach (string text in Versions)
            {
                DxfDatabase db = DxfDatabase.Parse(text);
                DxfTable table = Table(db);
                Assert.True(table.CanDeleteRows(0, 2));
                Assert.False(table.CanDeleteRows(0, 3));
                Assert.False(table.CanDeleteRows(2, 2));
                Assert.True(table.CanDeleteColumns(1, 1));
                Assert.False(table.CanDeleteColumns(0, 2));

                table.DeleteRows(1, 1);

                DxfDatabase readDb = DxfDatabase.Parse(db.ToText());
                DxfTable read = Table(readDb);
                Assert.Equal(new[] { 10.0, 8.0 }, Heights(read));
                Assert.Equal(new[] { "Parts", "B-12" }, Texts(read, 0));
                Assert.Equal("M12", read.GetBlockAttributeValue(1, 1, readDb.BlockTable["BOLT"].OfType<DxfAttributeDefinition>().Single()));

                read.DeleteColumns(1, 1);
                DxfTable again = Reread(readDb);
                Assert.Equal(new[] { 40.0 }, Widths(again));
                Assert.False(again.IsMergedCell(0, 0, out _));
                Assert.Equal("Parts", again.TextString(0, 0));
                Assert.Contains("173\n0\n174\n0\n175\n0\n176\n0\n", TableRecord(readDb.ToText()).Substring(0, TableRecord(readDb.ToText()).IndexOf("Parts", StringComparison.Ordinal)), StringComparison.Ordinal);

                Refused<ArgumentOutOfRangeException>(() => read.DeleteRows(0, 2), "one row at least left");
                Refused<ArgumentOutOfRangeException>(() => read.DeleteColumns(0, 0), "one or more");
            }
        }

        [Fact]
        public void TheTopRowOfABlockTakenOut_LeavesTheBlockItsContentInTheFirstRowOfItLeft()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
            DxfTable table = Table(db);
            table.InsertRows(3, 8.0, 1);
            table.MergeCells(new DxfCellRange(1, 0, 3, 0));

            table.DeleteRows(1, 1);

            DxfTable read = Reread(db);
            Assert.Equal(new DxfCellRange(1, 0, 2, 0), read.GetMergeRange(2, 0));
            Assert.Equal("Mark", read.TextString(1, 0));
            Assert.Equal(DxfCellAlignment.MiddleCenter, read.Alignment(1, 0));
            Assert.Equal(DxfTableCellType.BlockCell, read.CellType(1, 1));
        }

        [Fact]
        public void TheFirstColumnOfTheTitleTakenOut_LeavesItsTextInTheCellLeft()
        {
            foreach (string text in Versions)
            {
                DxfDatabase db = DxfDatabase.Parse(text);
                DxfTable table = Table(db);

                table.DeleteColumns(0, 1);

                DxfTable read = Reread(db);
                Assert.Equal(new[] { 30.0 }, Widths(read));
                Assert.Equal("Parts", read.TextString(0, 0));
                Assert.False(read.IsMergedCell(0, 0, out _));
                Assert.Equal("Qty", read.TextString(1, 0));
            }
        }

        [Fact]
        public void NumbersOfDecimalsFilled_AreRoundedToThePlacesOfTheSource_NotLeftWithTheNoiseOfTheirSum()
        {
            foreach (string text in Versions)
            {
                DxfDatabase db = DxfDatabase.Parse(text);
                DxfTable table = Table(db);
                table.SetSize(8, 2);
                table.SetValue(3, 1, 0.1);
                table.SetValue(4, 1, 0.2);
                table.SetValue(3, 0, 1.25);

                table.Fill(new DxfCellRange(5, 1, 7, 1), new DxfCellRange(3, 1, 4, 1), DxfTableFillOptions.GenerateSeries);
                table.Fill(new DxfCellRange(4, 0, 5, 0), new DxfCellRange(3, 0, 3, 0), DxfTableFillOptions.GenerateSeries);

                DxfTable read = Reread(db);
                Assert.Equal(new[] { "0.3", "0.4", "0.5" }, Enumerable.Range(5, 3).Select(r => read.TextString(r, 1)).ToArray());
                Assert.Equal(new[] { "2.25", "3.25" }, Enumerable.Range(4, 2).Select(r => read.TextString(r, 0)).ToArray());
            }
        }

        [Fact]
        public void NumbersFilledInReverse_GoOnFromTheSourcesEndTheOtherWay()
        {
            foreach (string text in Versions)
            {
                DxfDatabase db = DxfDatabase.Parse(text);
                DxfTable table = Table(db);
                table.SetSize(8, 2);
                table.SetValue(3, 1, 10);
                table.SetValue(4, 1, 20);
                table.SetValue(5, 0, 5);

                table.Fill(new DxfCellRange(5, 1, 7, 1), new DxfCellRange(3, 1, 4, 1), DxfTableFillOptions.GenerateSeries | DxfTableFillOptions.Reverse);
                table.Fill(new DxfCellRange(6, 0, 7, 0), new DxfCellRange(5, 0, 5, 0), DxfTableFillOptions.GenerateSeries | DxfTableFillOptions.Reverse);

                DxfTable read = Reread(db);
                Assert.Equal(new[] { "10", "0", "-10" }, Enumerable.Range(5, 3).Select(r => read.TextString(r, 1)).ToArray());
                Assert.Equal(new[] { "4", "3" }, Enumerable.Range(6, 2).Select(r => read.TextString(r, 0)).ToArray());
            }
        }

        [Fact]
        public void ATextOfANumber_IsFilledInASeriesBeforeR2007Only_AsItsTextIsItsValueThere()
        {
            foreach (string text in Versions)
            {
                bool r2000 = text == DxfDimensionDrawings.R2000;
                DxfDatabase db = DxfDatabase.Parse(text);
                DxfTable table = Table(db);
                table.SetSize(5, 2);
                table.SetTextString(3, 0, "7");

                table.Fill(new DxfCellRange(4, 0, 4, 0), new DxfCellRange(3, 0, 3, 0), DxfTableFillOptions.GenerateSeries);

                Assert.Equal(r2000 ? "8" : "7", Reread(db).TextString(4, 0));
            }
        }

        [Fact]
        public void ACellsOwnKindOfValue_IsInheritedWithItsFormat()
        {
            string text = DxfDimensionDrawings.R2018.Replace("176\n0\n 91\n49\n178\n", "176\n0\n 91\n262193\n178\n");
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfTable table = Table(db);

            table.InsertRowsAndInherit(2, 1, 1);

            DxfTable read = Reread(db);
            Assert.Equal(DxfCellProperties.Alignment | DxfCellProperties.TextStyle | DxfCellProperties.TextHeight | DxfCellProperties.DataType, read.GetOverrides(2, 0, 0));
        }

        [Fact]
        public void AFormulaItsFieldCouldNotGiveBack_IsRefused()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
            DxfTable table = Table(db);

            Refused<ArgumentException>(() => table.SetFormula(2, 0, 0, "=(A2+B2"), "parentheses");
            Refused<ArgumentException>(() => table.SetFormula(2, 0, 0, "=A2+B2)"), "parentheses");
            Assert.Equal(DxfDimensionDrawings.R2018, db.ToText());
            table.SetFormula(2, 0, 0, "=Sum(A2:B2)*(1+2)");
            Assert.Equal("=Sum(A2:B2)*(1+2)", Reread(db).GetFormula(2, 0, 0));
        }

        [Fact]
        public void AnErasedTable_IsAskedNothingOfItsStructure()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
            DxfTable table = Table(db);
            table.Erase();

            Refused<InvalidOperationException>(() => table.CanInsertRow(0), "erased");
            Refused<InvalidOperationException>(() => table.CanInsertColumn(0), "erased");
            Refused<InvalidOperationException>(() => table.CanDeleteRows(0, 1), "erased");
            Refused<InvalidOperationException>(() => table.CanDeleteColumns(0, 1), "erased");
        }

        [Fact]
        public void ATableOfMoreCellsThanAWholeNumberCounts_IsRefused()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
            DxfTable table = Table(db);

            Refused<ArgumentOutOfRangeException>(() => table.SetSize(int.MaxValue, 2), "cells");
            Refused<ArgumentOutOfRangeException>(() => table.InsertRows(0, 8.0, int.MaxValue), "cells");
            Refused<ArgumentOutOfRangeException>(() => table.InsertColumns(0, 8.0, int.MaxValue - 1), "cells");
            Refused<ArgumentOutOfRangeException>(() => table.InsertRowsAndInherit(0, 0, int.MaxValue), "cells");
            Assert.Equal(DxfDimensionDrawings.R2018, db.ToText());
        }

        [Fact]
        public void ACopyThatDropsFieldsAnyway_TakesSkipFieldAndSkipFormula()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
            DxfTable table = Table(db);
            table.SetFormula(1, 1, 0, "=1+2");

            table.CopyFrom(table, DxfTableCopyOptions.SkipField | DxfTableCopyOptions.SkipFormula, new DxfCellRange(1, 1, 1, 1), new DxfCellRange(1, 0, 1, 0));

            DxfTable read = Reread(db);
            Assert.Equal("Qty", read.TextString(1, 0));
            Assert.False(read.HasFormula(1, 0, 0));
            Assert.True(read.HasFormula(1, 1, 0));
        }

        [Fact]
        public void TheLastCellChanged_LeavesTheTablesOwnGroupsAfterIt()
        {
            foreach (string version in new[] { DxfDimensionDrawings.R2000, DxfDimensionDrawings.R2018 })
            {
                // The table's own margin 1.5 and the colours of its title's top and inside lines after its last cell.
                string text = version
                    .Replace("\n 92\n2\n 93\n0\n 94\n0\n", "\n 92\n2\n 93\n8\n 94\n3\n")
                    .Replace(version == DxfDimensionDrawings.R2000 ? "331\n4C\n300\nM12\n" : "302\n\n304\nACVALUE_END\n  0\n", version == DxfDimensionDrawings.R2000 ? "331\n4C\n300\nM12\n 40\n1.5\n 64\n1\n 65\n2\n" : "302\n\n304\nACVALUE_END\n 40\n1.5\n 64\n1\n 65\n2\n  0\n");
                Assert.Equal(1.5, Table(DxfDatabase.Parse(text)).HorizontalCellMargin);

                var edits = new List<Action<DxfTable>>
                {
                    t => t.DeleteRows(2, 1),
                    t => t.SetSize(2, 1),
                    t => t.InsertColumns(2, 10.0, 1),
                    t => t.InsertRows(3, 10.0, 1),
                };

                foreach (Action<DxfTable> edit in edits)
                {
                    DxfDatabase db = DxfDatabase.Parse(text);
                    edit(Table(db));
                    DxfTable read = Reread(db);
                    Assert.Equal(1.5, read.HorizontalCellMargin);
                    Assert.Equal(1, read.GridColor(DxfGridLineType.HorizontalTop, DxfRowType.TitleRow).ColorIndex);
                    Assert.Equal(2, read.GridColor(DxfGridLineType.HorizontalInside, DxfRowType.TitleRow).ColorIndex);
                    Assert.EndsWith(" 40\n1.5\n 64\n1\n 65\n2\n", TableRecord(db.ToText()), StringComparison.Ordinal);
                    Assert.Equal(read.NumRows * read.NumColumns, read.Count());
                }
            }
        }

        [Fact]
        public void AColumnCanBePutInBeforeAnyOrAfterTheLast_NotBeyond()
        {
            DxfTable table = Table(DxfDatabase.Parse(DxfDimensionDrawings.R2018));

            Assert.True(table.CanInsertColumn(0));
            Assert.True(table.CanInsertColumn(2));
            Assert.False(table.CanInsertColumn(3));
            Assert.False(table.CanInsertColumn(-1));
            Assert.False(table.CanInsertRow(-1));
        }

        [Fact]
        public void HeightsAndWidthsCopied_AreThoseOfTheSourceOnlyWhereAsked()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
            DxfTable table = Table(db);
            table.SetRowHeight(2, 12.0);
            table.SetColumnWidth(0, 50.0);

            table.CopyFrom(table, DxfTableCopyOptions.None, new DxfCellRange(2, 0, 2, 0), new DxfCellRange(1, 1, 1, 1));
            Assert.Equal(new[] { 10.0, 8.0, 12.0 }, Heights(Reread(db)));
            Assert.Equal(new[] { 50.0, 30.0 }, Widths(Reread(db)));

            table.CopyFrom(table, DxfTableCopyOptions.TableCopyRowHeight | DxfTableCopyOptions.TableCopyColumnWidth, new DxfCellRange(2, 0, 2, 0), new DxfCellRange(1, 1, 1, 1));
            Assert.Equal(new[] { 10.0, 12.0, 12.0 }, Heights(Reread(db)));
            Assert.Equal(new[] { 50.0, 50.0 }, Widths(Reread(db)));
        }

        [Fact]
        public void RowsAndColumnsPutIn_TakeTheLookOfTheOneGiven_NotItsContent()
        {
            foreach (string text in Versions)
            {
                DxfDatabase db = DxfDatabase.Parse(text);
                DxfTable table = Table(db);
                table.SetTextRotation(1, 0, DxfRotationAngle.Degrees090);

                table.InsertRowsAndInherit(2, 1, 1);
                table.InsertColumnsAndInherit(2, 0, 1);

                DxfDatabase readDb = DxfDatabase.Parse(db.ToText());
                DxfTable read = Table(readDb);
                Assert.Equal(new[] { 10.0, 8.0, 8.0, 8.0 }, Heights(read));
                Assert.Equal(new[] { 40.0, 30.0, 40.0 }, Widths(read));
                Assert.Equal(DxfCellAlignment.MiddleCenter, read.Alignment(2, 0));
                Assert.Equal(3.5, read.TextHeight(2, 0));
                Assert.Equal("GH_TXT", read.TextStyle(2, 0).Name);
                Assert.Equal(DxfRotationAngle.Degrees090, read.TextRotation(2, 0));
                Assert.Equal(string.Empty, read.TextString(2, 0));
                Assert.Null(read.Alignment(2, 1));
                Assert.Equal(DxfTableCellType.TextCell, read.CellType(2, 1));
                Assert.Equal(DxfCellAlignment.MiddleCenter, read.Alignment(1, 2));
                Assert.Equal(string.Empty, read.TextString(1, 2));
                Assert.Equal(DxfTableCellType.TextCell, read.CellType(3, 2));
                Assert.False(read.IsMergedCell(0, 2, out _));
                Assert.Equal(new DxfCellRange(0, 0, 0, 1), read.GetMergeRange(0, 0));

                Refused<ArgumentOutOfRangeException>(() => read.InsertRowsAndInherit(0, 4, 1), "rows");
            }
        }

        [Fact]
        public void ATableGivenASize_GrowsAndShrinksFromItsBottomAndRight_AndItsOwnSizeWritesNothing()
        {
            foreach (string text in Versions)
            {
                DxfDatabase db = DxfDatabase.Parse(text);
                DxfTable table = Table(db);
                table.SetSize(3, 2);
                Assert.Equal(text, db.ToText());

                table.SetSize(5, 3);
                DxfTable grown = Reread(db);
                Assert.Equal(new[] { 10.0, 8.0, 8.0, 8.0, 8.0 }, Heights(grown));
                Assert.Equal(new[] { 40.0, 30.0, 30.0 }, Widths(grown));
                Assert.Equal(new DxfCellRange(0, 0, 0, 1), grown.GetMergeRange(0, 0));
                Assert.Equal("B-12", grown.TextString(2, 0));

                table.SetSize(2, 1);
                DxfTable shrunk = Reread(db);
                Assert.Equal(2, shrunk.NumRows);
                Assert.Equal(1, shrunk.NumColumns);
                Assert.False(shrunk.IsMergedCell(0, 0, out _));
                Assert.Equal(new[] { "Parts", "Mark" }, Texts(shrunk, 0));

                Refused<ArgumentOutOfRangeException>(() => table.SetSize(0, 2), "one or more");
            }
        }

        [Fact]
        public void CellsMerged_AreMarkedInEveryCell_TheirBlockOnItsTopLeft_AndUnmergedTakeAllBack()
        {
            foreach (string text in Versions)
            {
                DxfDatabase db = DxfDatabase.Parse(text);
                DxfTable table = Table(db);

                // The title merged already, and one cell alone, write nothing.
                table.MergeCells(new DxfCellRange(0, 0, 0, 1));
                table.MergeCells(new DxfCellRange(1, 1, 1, 1));
                table.UnmergeCells(new DxfCellRange(1, 0, 2, 1));
                Assert.Equal(text, db.ToText());

                table.MergeCells(new DxfCellRange(0, 0, 1, 1));
                string saved = db.ToText();
                OnlyTheseChanged(text, saved, "F6");
                DxfTable read = Table(DxfDatabase.Parse(saved));
                Assert.All(new[] { (0, 0), (0, 1), (1, 0), (1, 1) }, p => Assert.Equal(new DxfCellRange(0, 0, 1, 1), read.GetMergeRange(p.Item1, p.Item2)));
                Assert.Contains("173\n1\n174\n0\n175\n2\n176\n2\n", TableRecord(saved), StringComparison.Ordinal);
                Assert.Equal("Parts", read.TextString(0, 0));
                Assert.Equal("Qty", read.TextString(1, 1));

                Refused<ArgumentException>(() => table.MergeCells(new DxfCellRange(1, 0, 2, 0)), "reaches across");
                Refused<ArgumentOutOfRangeException>(() => table.MergeCells(new DxfCellRange(2, 0, 3, 0)), "reaches past");
                Assert.Throws<ArgumentNullException>(() => table.MergeCells(null));

                table.UnmergeCells(new DxfCellRange(1, 1, 1, 1));
                DxfTable unmerged = Reread(db);
                Assert.False(unmerged.IsMergedCell(0, 0, out _));
                Assert.False(unmerged.IsMergedCell(0, 1, out _));
                Assert.DoesNotContain("173\n1\n", TableRecord(db.ToText()), StringComparison.Ordinal);
            }
        }

        [Fact]
        public void TheLinesOfACell_AreItsOwnOverrides_WithTheirFlags_AndReadBack()
        {
            foreach (string text in Versions)
            {
                bool r2000 = text == DxfDimensionDrawings.R2000;
                DxfDatabase db = DxfDatabase.Parse(text);
                DxfTable table = Table(db);

                table.SetGridColor(1, 1, DxfGridLineType.OuterGridLines, Aci(1));
                table.SetGridLineWeight(1, 1, DxfGridLineType.HorizontalTop, DxfLineWeight.LineWeight035);
                table.SetGridVisibility(1, 1, DxfGridLineType.VerticalRight, false);

                string saved = db.ToText();
                OnlyTheseChanged(text, saved, "F6");
                Assert.Contains((r2000 ? "177" : " 91") + "\n" + (0x3C0 | 0x400 | 0x8000) + "\n", TableRecord(saved), StringComparison.Ordinal);
                Assert.Contains((r2000 ? "  1\nQty\n" : "302\nQty\n304\nACVALUE_END\n") + " 69\n1\n 65\n1\n 66\n1\n 68\n1\n279\n35\n285\n0\n171\n", TableRecord(saved), StringComparison.Ordinal);
                DxfTable read = Table(DxfDatabase.Parse(saved));
                Assert.Equal(1, read.GetGridColor(1, 1, DxfGridLineType.VerticalLeft).ColorIndex);
                Assert.Equal(1, read.GridColor(1, 1, DxfCellEdgeMasks.BottomMask).ColorIndex);
                Assert.Equal(DxfLineWeight.LineWeight035, read.GetGridLineWeight(1, 1, DxfGridLineType.HorizontalTop));
                Assert.Equal(DxfLineWeight.LineWeight035, read.GridLineWeight(1, 1, DxfCellEdgeMasks.TopMask));
                Assert.Null(read.GetGridLineWeight(1, 1, DxfGridLineType.HorizontalBottom));
                Assert.False(read.GetGridVisibility(1, 1, DxfGridLineType.VerticalRight));
                Assert.False(read.GridVisibility(1, 1, DxfCellEdgeMasks.RightMask));
                Assert.Null(read.GetGridVisibility(1, 1, DxfGridLineType.HorizontalTop));
                Assert.Null(read.GetGridColor(1, 0, DxfGridLineType.HorizontalTop));
                DxfGridPropertyParameter top = read.GetGridProperty(1, 1, DxfGridLineType.HorizontalTop);
                Assert.Equal(DxfGridProperties.Color | DxfGridProperties.LineWeight, top.PropertyMask);
                Assert.Equal(DxfLineWeight.LineWeight035, top.LineWeight);
                Assert.Equal(DxfGridProperties.Invalid, read.GetGridProperty(2, 0, DxfGridLineType.HorizontalTop).PropertyMask);

                // Each given what it has writes nothing; a property of no group is refused.
                table.SetGridColor(1, 1, DxfGridLineType.AllGridLines, Aci(1));
                table.SetGridProperty(1, 1, DxfGridLineType.HorizontalTop, new DxfGridPropertyParameter { PropertyMask = DxfGridProperties.LineWeight, LineWeight = DxfLineWeight.LineWeight035 });
                Assert.Equal(saved, db.ToText());
                Refused<ArgumentException>(() => table.SetGridColor(1, 1, DxfGridLineType.InnerGridLines, Aci(2)), "name no line");
                Refused<ArgumentException>(() => table.SetGridProperty(1, 1, DxfGridLineType.HorizontalTop, new DxfGridPropertyParameter { PropertyMask = DxfGridProperties.LineStyle }), "no groups");
                Refused<ArgumentOutOfRangeException>(() => table.GetGridColor(1, 1, DxfGridLineType.HorizontalInside), "one of its four");
                Refused<ArgumentOutOfRangeException>(() => table.SetGridLineWeight(1, 1, DxfGridLineType.HorizontalTop, (DxfLineWeight)33), "AutoCAD's widths");
                Refused<ArgumentException>(() => table.SetGridColor(1, 1, DxfGridLineType.HorizontalTop, DxfColor.FromRgb(1, 2, 3)), "AutoCAD's index");
            }
        }

        [Fact]
        public void TheLinesOfABlockOfCells_AreSetOnTheEdgesOfTheCellsEitherSideOfEach()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
            DxfTable table = Table(db);
            var red = new DxfGridPropertyParameter { PropertyMask = DxfGridProperties.Color | DxfGridProperties.Visibility, Color = Aci(1), Visible = true };

            table.SetGridProperty(new DxfCellRange(1, 0, 2, 1), DxfGridLineType.HorizontalInside, red);

            DxfTable read = Reread(db);
            Assert.Equal(1, read.GetGridColor(1, 0, DxfGridLineType.HorizontalBottom).ColorIndex);
            Assert.Equal(1, read.GetGridColor(1, 1, DxfGridLineType.HorizontalBottom).ColorIndex);
            Assert.Equal(1, read.GetGridColor(2, 0, DxfGridLineType.HorizontalTop).ColorIndex);
            Assert.True(read.GetGridVisibility(2, 1, DxfGridLineType.HorizontalTop));
            Assert.Null(read.GetGridColor(1, 0, DxfGridLineType.HorizontalTop));
            Assert.Null(read.GetGridColor(2, 0, DxfGridLineType.HorizontalBottom));
            Assert.Null(read.GetGridColor(1, 0, DxfGridLineType.VerticalRight));
            Refused<ArgumentException>(() => table.SetGridProperty(new DxfCellRange(1, 0, 1, 1), DxfGridLineType.HorizontalInside, red), "name no line");
        }

        [Fact]
        public void TheLinesOfTheRowsOfAKind_AreTheTablesOwn_InTheOrderOfTheirFlags()
        {
            foreach (string text in Versions)
            {
                DxfDatabase db = DxfDatabase.Parse(text);
                DxfTable table = Table(db);

                table.SetGridColor(Aci(1), DxfGridLineType.HorizontalTop | DxfGridLineType.HorizontalInside, DxfRowType.TitleRow);
                table.SetGridColor(Aci(3), DxfGridLineType.HorizontalTop, DxfRowType.DataRow);
                table.SetGridColor(Aci(2), DxfGridLineType.HorizontalTop, DxfRowType.HeaderRow);
                table.SetGridLineWeight(DxfLineWeight.LineWeight050, DxfGridLineType.VerticalLeft, DxfRowType.DataRow | DxfRowType.TitleRow);
                table.SetGridVisibility(false, DxfGridLineType.VerticalRight, DxfRowType.HeaderRow);

                string saved = db.ToText();
                OnlyTheseChanged(text, saved, "F6");
                string record = TableRecord(saved);
                Assert.Contains(" 93\n0\n 94\n" + (1 | 2 | 1 << 6 | 1 << 12) + "\n 95\n" + (1 << 3 | 1 << 15) + "\n 96\n" + (1 << 11) + "\n141\n", record, StringComparison.Ordinal);
                Assert.EndsWith(" 64\n1\n 65\n1\n 64\n2\n 64\n3\n277\n50\n277\n50\n289\n0\n", record, StringComparison.Ordinal);
                DxfTable read = Table(DxfDatabase.Parse(saved));
                Assert.Equal(1, read.GridColor(DxfGridLineType.HorizontalInside, DxfRowType.TitleRow).ColorIndex);
                Assert.Equal(2, read.GridColor(DxfGridLineType.HorizontalTop, DxfRowType.HeaderRow).ColorIndex);
                Assert.Equal(3, read.GridColor(DxfGridLineType.HorizontalTop, DxfRowType.DataRow).ColorIndex);
                Assert.Null(read.GridColor(DxfGridLineType.HorizontalBottom, DxfRowType.DataRow));
                Assert.Equal(DxfLineWeight.LineWeight050, read.GridLineWeight(DxfGridLineType.VerticalLeft, DxfRowType.TitleRow));
                Assert.False(read.GridVisibility(DxfGridLineType.VerticalRight, DxfRowType.HeaderRow));
                Assert.Null(read.GridVisibility(DxfGridLineType.VerticalRight, DxfRowType.DataRow));
                Assert.Contains(DxfTableStyleOverride.DataHorizontalTopColor, read.TableStyleOverrides());
                Assert.Equal("B-12", read.TextString(2, 0));

                // Given again what each has writes nothing; ClearTableStyleOverrides takes them away.
                table.SetGridColor(Aci(3), DxfGridLineType.HorizontalTop, DxfRowType.DataRow);
                table.SetGridVisibility(false, DxfGridLineType.VerticalRight, DxfRowType.HeaderRow);
                Assert.Equal(saved, db.ToText());
                table.ClearTableStyleOverrides(1);
                Assert.Null(Reread(db).GridColor(DxfGridLineType.HorizontalTop, DxfRowType.DataRow));
                Assert.Equal(text, db.ToText());

                Refused<ArgumentOutOfRangeException>(() => table.GridColor(DxfGridLineType.OuterGridLines, DxfRowType.DataRow), "six");
                Refused<ArgumentOutOfRangeException>(() => table.GridColor(DxfGridLineType.HorizontalTop, DxfRowType.UnknownRow), "one kind");
                Refused<ArgumentOutOfRangeException>(() => table.SetGridColor(Aci(1), DxfGridLineType.HorizontalTop, DxfRowType.UnknownRow), "one or more");
            }
        }

        [Fact]
        public void TheBorderColoursOfTheTable_AreReadAfterTheColoursOfItsRows()
        {
            // A title row of the colour 5 of its own (93 flag 0x20) before the colour 1 of the title's top line (94 flag 1).
            string text = DxfDimensionDrawings.R2000
                .Replace("\n 92\n2\n 93\n0\n 94\n0\n", "\n 92\n2\n 93\n32\n 94\n1\n")
                .Replace("331\n4C\n300\nM12\n", "331\n4C\n300\nM12\n 64\n5\n 64\n1\n");
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfTable table = Table(db);
            Assert.Equal(1, table.GridColor(DxfGridLineType.HorizontalTop, DxfRowType.TitleRow).ColorIndex);

            table.SetGridColor(Aci(4), DxfGridLineType.HorizontalTop, DxfRowType.TitleRow);
            table.SetGridColor(Aci(6), DxfGridLineType.HorizontalTop, DxfRowType.DataRow);

            Assert.EndsWith("300\nM12\n 64\n5\n 64\n4\n 64\n6\n", TableRecord(db.ToText()), StringComparison.Ordinal);
            DxfTable read = Reread(db);
            Assert.Equal(4, read.GridColor(DxfGridLineType.HorizontalTop, DxfRowType.TitleRow).ColorIndex);
            Assert.Equal(6, read.GridColor(DxfGridLineType.HorizontalTop, DxfRowType.DataRow).ColorIndex);
        }

        [Fact]
        public void AFormulaSet_IsAFieldOfAutoCADsExpressions_ThatTheCellPointsAt()
        {
            foreach (string text in new[] { R2007, DxfDimensionDrawings.R2018 })
            {
                DxfDatabase db = DxfDatabase.Parse(text);
                DxfTable table = Table(db);
                Assert.False(table.HasFormula(2, 0, 0));
                Assert.Null(table.GetFormula(2, 0, 0));

                table.SetFormula(2, 0, 0, "=A2+B2");

                string saved = db.ToText();
                string field = Records(saved).Single(r => r.StartsWith("  0\nFIELD\n", StringComparison.Ordinal));
                OnlyTheseChanged(text, saved, "F6", HandleOf(field), null);
                Assert.Contains("330\nF6\n100\nAcDbField\n  1\nAcExpr\n  2\n\\AcExpr (A2+B2)\n", field, StringComparison.Ordinal);
                Assert.Contains("344\n" + HandleOf(field) + "\n", TableRecord(saved), StringComparison.Ordinal);
                DxfDatabase readDb = DxfDatabase.Parse(saved);
                DxfTable read = Table(readDb);
                Assert.True(read.HasFormula(2, 0, 0));
                Assert.Equal("=A2+B2", read.GetFormula(2, 0, 0));
                Assert.Equal("B-12", read.TextString(2, 0));

                // The formula it holds writes nothing; a text set takes the field's place.
                read.SetFormula(2, 0, 0, " = A2+B2 ");
                Assert.Equal(saved, readDb.ToText());
                read.SetTextString(2, 0, "4");
                Assert.False(read.HasFormula(2, 0, 0));

                Refused<ArgumentException>(() => read.SetFormula(1, 0, 0, "A2+B2"), "begins with =");
                Refused<ArgumentException>(() => read.SetFormula(1, 0, 0, "="), "begins with =");
                Refused<InvalidOperationException>(() => read.SetFormula(2, 1, 0, "=1"), "holds a block");
                Refused<ArgumentOutOfRangeException>(() => read.SetFormula(1, 0, 1, "=1"), "one content");
            }
        }

        private static int Fields(string text) => Records(text).Count(r => r.StartsWith("  0\nFIELD\n", StringComparison.Ordinal));

        private static string WithFields(string owner)
        {
            // FA1 a field of text owned by the table, with its child FA2, pointed at by B-12 (2, 0); FA3 a date owned by the owner
            // given, pointed at by Qty (1, 1) and by Mark (1, 0).
            string objects = "  0\nFIELD\n  5\nFA1\n330\nF6\n100\nAcDbField\n  1\n_text\n  2\n%<\\_FldIdx 0>%\n 90\n1\n360\nFA2\n 97\n0\n"
                + "  0\nFIELD\n  5\nFA2\n330\nFA1\n100\nAcDbField\n  1\nAcExpr\n  2\n\\AcExpr (1+2)\n 90\n0\n 97\n0\n"
                + "  0\nFIELD\n  5\nFA3\n330\n" + owner + "\n100\nAcDbField\n  1\nAcVar\n  2\n\\AcVar Date\n 90\n0\n 97\n0\n";
            return DxfDimensionDrawings.R2018
                .Replace("  0\nENDSEC\n  0\nEOF\n", objects + "  0\nENDSEC\n  0\nEOF\n")
                .Replace(" 92\n0\n301\nCELL_VALUE\n 93\n6\n 90\n4\n  1\nB-12\n", " 92\n0\n344\nFA1\n301\nCELL_VALUE\n 93\n6\n 90\n4\n  1\nB-12\n")
                .Replace(" 92\n0\n301\nCELL_VALUE\n 93\n6\n 90\n4\n  1\nQty\n", " 92\n0\n344\nFA3\n301\nCELL_VALUE\n 93\n6\n 90\n4\n  1\nQty\n")
                .Replace(" 92\n0\n301\nCELL_VALUE\n 93\n6\n 90\n4\n  1\nMark\n", " 92\n0\n344\nFA3\n301\nCELL_VALUE\n 93\n6\n 90\n4\n  1\nMark\n");
        }

        [Fact]
        public void AFormulaSet_ListsTheClassOfFields_AsAutoCADListsAnObjectNotBuiltIn()
        {
            foreach (string text in new[] { R2007, DxfDimensionDrawings.R2018 })
            {
                DxfDatabase db = DxfDatabase.Parse(text);
                Table(db).SetFormula(2, 0, 0, "=1+2");
                Table(db).SetFormula(1, 1, 0, "=3");

                string saved = db.ToText();
                string expected = "  0\nCLASS\n  1\nFIELD\n  2\nAcDbField\n  3\nObjectDBX Classes\n 90\n1152\n 91\n0\n280\n0\n281\n0\n";
                Assert.Equal(expected, Records(saved).Single(r => r.StartsWith("  0\nCLASS\n  1\nFIELD\n", StringComparison.Ordinal)));
            }
        }

        [Fact]
        public void TheFieldOfACell_IsErasedWithItsChildren_WhenTheCellLosesIt()
        {
            var edits = new List<(string Name, Action<DxfTable> Edit)>
            {
                ("text", t => t.SetTextString(2, 0, "x")),
                ("value", t => t.SetValue(2, 0, 4)),
                ("reset", t => t.ResetValue(2, 0)),
                ("formula", t => t.SetFormula(2, 0, 0, "=5")),
                ("delete", t => t.DeleteRows(2, 1)),
                ("size", t => t.SetSize(2, 2)),
                ("copy", t => t.CopyFrom(t, DxfTableCopyOptions.None, new DxfCellRange(0, 0, 0, 0), new DxfCellRange(2, 0, 2, 0))),
                ("fill", t => t.Fill(new DxfCellRange(2, 0, 2, 0), new DxfCellRange(1, 0, 1, 0), DxfTableFillOptions.None)),
            };

            foreach (var (name, edit) in edits)
            {
                string text = WithFields("F6");
                DxfDatabase db = DxfDatabase.Parse(text);
                edit(Table(db));

                string saved = db.ToText();
                Assert.DoesNotContain(Records(saved), r => HandleOf(r) == "FA1");
                Assert.DoesNotContain(Records(saved), r => HandleOf(r) == "FA2");
                Assert.Contains(Records(saved), r => HandleOf(r) == "FA3");
                Assert.Equal(name == "formula" ? 2 : 1, Fields(saved));
            }
        }

        [Fact]
        public void AFieldAnotherCellOrAnotherObjectHolds_IsLeftWhenACellLosesIt()
        {
            // Qty and Mark both point at FA3, owned by the table: the one losing it leaves it to the other.
            DxfDatabase db = DxfDatabase.Parse(WithFields("F6"));
            Table(db).SetTextString(1, 1, "x");
            Assert.Contains(Records(db.ToText()), r => HandleOf(r) == "FA3");
            Table(db).SetTextString(1, 0, "y");
            Assert.DoesNotContain(Records(db.ToText()), r => HandleOf(r) == "FA3");

            // FA3 owned by another object is that object's, whatever cell lets it go.
            DxfDatabase other = DxfDatabase.Parse(WithFields("17"));
            Table(other).SetTextString(1, 1, "x");
            Table(other).SetTextString(1, 0, "y");
            Assert.Contains(Records(other.ToText()), r => HandleOf(r) == "FA3");

            // A copy of a cell drops the field from the copy and leaves it to the cell copied from.
            DxfDatabase copied = DxfDatabase.Parse(WithFields("F6"));
            Table(copied).CopyFrom(Table(copied), DxfTableCopyOptions.None, new DxfCellRange(2, 0, 2, 0), new DxfCellRange(0, 1, 0, 1));
            Assert.Equal(3, Fields(copied.ToText()));
            Assert.Equal("=1+2", Table(DxfDatabase.Parse(copied.ToText())).GetFormula(2, 0, 0));
        }

        [Fact]
        public void AFieldACopyOfTheTableHolds_IsLeftWhenTheOriginalLetsItGo_ByEitherWayOfCopying()
        {
            var copies = new List<(string Name, Action<DxfDatabase, DxfTable> Copy)>
            {
                ("wblock", (db, t) => db.WblockCloneObjects(new DxfEntity[] { t }, db.ModelSpace)),
                ("clone", (db, t) => db.ModelSpace.AppendEntity(t.Clone())),
            };

            foreach (var (name, copy) in copies)
            {
                DxfDatabase db = DxfDatabase.Parse(WithFields("F6"));
                DxfTable original = db.ModelSpace.OfType<DxfTable>().First(t => t.Handle == "F6");
                copy(db, original);
                Assert.Equal(2, db.ModelSpace.OfType<DxfTable>().Count());

                original.SetTextString(2, 0, "x");

                string saved = db.ToText();
                Assert.Contains(Records(saved), r => HandleOf(r) == "FA1");
                Assert.Contains(Records(saved), r => HandleOf(r) == "FA2");
                DxfTable copied = DxfDatabase.Parse(saved).ModelSpace.OfType<DxfTable>().Single(t => t.Handle != "F6");
                Assert.Equal("=1+2", copied.GetFormula(2, 0, 0));
            }
        }

        [Fact]
        public void AFieldAnXrecordPointsAt_IsLeftWhenTheCellLetsItGo()
        {
            string text = WithFields("F6").Replace("  0\nENDSEC\n  0\nEOF\n", "  0\nXRECORD\n  5\nFB2\n330\nA\n100\nAcDbXrecord\n280\n1\n340\nFA1\n  0\nENDSEC\n  0\nEOF\n");
            DxfDatabase db = DxfDatabase.Parse(text);

            Table(db).SetTextString(2, 0, "x");

            Assert.Contains(Records(db.ToText()), r => HandleOf(r) == "FA1");
            Assert.Contains(Records(db.ToText()), r => HandleOf(r) == "FA2");
        }

        [Fact]
        public void AChildFieldAnotherFieldHolds_IsLeftWhenItsParentIsErased()
        {
            string text = WithFields("F6").Replace("  0\nENDSEC\n  0\nEOF\n", "  0\nFIELD\n  5\nFB3\n330\nA\n100\nAcDbField\n  1\n_text\n  2\n%<\\_FldIdx 0>%\n 90\n1\n360\nFA2\n 97\n0\n  0\nENDSEC\n  0\nEOF\n");
            DxfDatabase db = DxfDatabase.Parse(text);

            Table(db).SetTextString(2, 0, "x");

            Assert.DoesNotContain(Records(db.ToText()), r => HandleOf(r) == "FA1");
            Assert.Contains("  5\nFA2\n330\nFB3\n100\nAcDbField\n", Records(db.ToText()).Single(r => HandleOf(r) == "FA2"), StringComparison.Ordinal);
        }

        [Fact]
        public void AFieldErased_IsTakenOutOfTheDrawingsListOfFields_WithItsChildren()
        {
            string list = "  0\nFIELDLIST\n  5\nFB1\n102\n{ACAD_REACTORS\n330\nA\n102\n}\n330\nA\n100\nAcDbIdSet\n 90\n3\n330\nFA1\n330\nFA2\n330\nFA3\n100\nAcDbFieldList\n";
            string text = WithFields("F6")
                .Replace("  5\nA\n330\n0\n100\nAcDbDictionary\n281\n1\n", "  5\nA\n330\n0\n100\nAcDbDictionary\n281\n1\n  3\nACAD_FIELDLIST\n350\nFB1\n")
                .Replace("  0\nENDSEC\n  0\nEOF\n", list + "  0\nENDSEC\n  0\nEOF\n");
            DxfDatabase db = DxfDatabase.Parse(text);

            Table(db).SetTextString(2, 0, "x");

            string saved = db.ToText();
            Assert.DoesNotContain(Records(saved), r => HandleOf(r) == "FA1");
            Assert.DoesNotContain(Records(saved), r => HandleOf(r) == "FA2");
            Assert.EndsWith("100\nAcDbIdSet\n 90\n1\n330\nFA3\n100\nAcDbFieldList\n", Records(saved).Single(r => HandleOf(r) == "FB1"), StringComparison.Ordinal);
        }

        [Fact]
        public void AFormulaBeforeR2004_IsRefused_AsAFileOfThatVersionHasNoFields()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfDimensionDrawings.R2000);

            Refused<InvalidOperationException>(() => Table(db).SetFormula(2, 0, 0, "=1+2"), "older than R2004 (AC1018)");
            Assert.Equal(DxfDimensionDrawings.R2000, db.ToText());
        }

        [Fact]
        public void WholeNumbersOfAStepWithDecimals_GoOnInThePlacesOfTheStep()
        {
            foreach (string text in Versions)
            {
                DxfDatabase db = DxfDatabase.Parse(text);
                DxfTable table = Table(db);
                table.SetSize(12, 2);
                table.SetValue(3, 1, 1);
                table.SetValue(4, 1, 2);
                table.SetValue(5, 1, 4);
                table.SetValue(3, 0, 1);
                table.SetValue(4, 0, 2);
                table.SetValue(5, 0, 4);

                table.Fill(new DxfCellRange(1, 1, 9, 1), new DxfCellRange(3, 1, 5, 1), DxfTableFillOptions.GenerateSeries);
                table.Fill(new DxfCellRange(6, 0, 9, 0), new DxfCellRange(3, 0, 5, 0), DxfTableFillOptions.GenerateSeries | DxfTableFillOptions.Reverse);

                DxfTable read = Reread(db);
                Assert.Equal(new[] { "-2", "-0.5" }, new[] { read.TextString(1, 1), read.TextString(2, 1) });
                Assert.Equal(new[] { "5.5", "7", "8.5", "10" }, Enumerable.Range(6, 4).Select(r => read.TextString(r, 1)).ToArray());
                Assert.Equal(new[] { "2.5", "1", "-0.5", "-2" }, Enumerable.Range(6, 4).Select(r => read.TextString(r, 0)).ToArray());
            }
        }

        [Fact]
        public void NumbersFarBelowOne_AreFilledToFifteenFiguresOfTheirOwn_NotRoundedAway()
        {
            foreach (string text in Versions)
            {
                DxfDatabase db = DxfDatabase.Parse(text);
                DxfTable table = Table(db);
                table.SetSize(6, 2);
                table.SetValue(3, 1, 1E-20);
                table.SetValue(4, 1, 2E-20);

                table.Fill(new DxfCellRange(5, 1, 5, 1), new DxfCellRange(3, 1, 4, 1), DxfTableFillOptions.GenerateSeries);

                Assert.Equal(3E-20, double.Parse(Reread(db).TextString(5, 1), System.Globalization.CultureInfo.InvariantCulture));
            }
        }

        [Fact]
        public void AFieldOfATextReplacedOrTakenOut_ReadsNotTheWholeFile_SoManyTextsCostTimeLinearInThem()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            DxfDBText text = db.ModelSpace.OfType<DxfDBText>().First();
            int before = db.File.WholeReadings;

            for (int k = 0; k < 20; k++)
            {
                text.SetField(new DxfField("Drawn %<\\AcVar Date>%", true));
                text.SetField(new DxfField("By %<\\AcVar Login>%", true));
                text.RemoveField();
                text.SetField(new DxfField("Drawn %<\\AcVar Date>%", true));
                text.ConvertFieldToText();
            }

            // The root dictionary found once, the drawing's list of fields looked for through it; nothing read per call.
            Assert.InRange(db.File.WholeReadings - before, 0, 1);
        }

        [Fact]
        public void AFieldOfATextTakenOut_LeavesTheDrawingsListOfFields_ItAndItsChildren()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            DxfDBText text = db.ModelSpace.OfType<DxfDBText>().First();
            DxfField field = text.SetField(new DxfField("Drawn %<\\AcVar Date>%", true));
            string child = field.GetChildren()[0].Handle;
            string saved = db.ToText();
            string root = Records(saved).First(r => r.StartsWith("  0\nDICTIONARY\n", StringComparison.Ordinal));
            Assert.DoesNotContain(Records(saved), r => HandleOf(r) == "FFF1");
            string listed = saved
                .Replace(root, root + "  3\nACAD_FIELDLIST\n350\nFFF1\n")
                .Replace("  0\nENDSEC\n  0\nEOF\n", "  0\nFIELDLIST\n  5\nFFF1\n330\n" + HandleOf(root) + "\n100\nAcDbIdSet\n 90\n2\n330\n" + field.Handle + "\n330\n" + child + "\n100\nAcDbFieldList\n  0\nENDSEC\n  0\nEOF\n");
            DxfDatabase readDb = DxfDatabase.Parse(listed);

            readDb.ModelSpace.OfType<DxfDBText>().First().RemoveField();

            Assert.EndsWith("100\nAcDbIdSet\n 90\n0\n100\nAcDbFieldList\n", Records(readDb.ToText()).Single(r => HandleOf(r) == "FFF1"), StringComparison.Ordinal);
        }

        [Fact]
        public void AChildFieldOnlyAnXrecordHolds_IsGivenToTheTableThatOwnedItsParent()
        {
            string text = WithFields("F6").Replace("  0\nENDSEC\n  0\nEOF\n", "  0\nXRECORD\n  5\nFB2\n330\nA\n100\nAcDbXrecord\n280\n1\n340\nFA2\n  0\nENDSEC\n  0\nEOF\n");
            DxfDatabase db = DxfDatabase.Parse(text);

            Table(db).SetTextString(2, 0, "x");

            Assert.DoesNotContain(Records(db.ToText()), r => HandleOf(r) == "FA1");
            Assert.Contains("  5\nFA2\n330\nF6\n100\nAcDbField\n", Records(db.ToText()).Single(r => HandleOf(r) == "FA2"), StringComparison.Ordinal);
        }

        [Fact]
        public void AChildFieldHeldByAFieldItOwns_IsGivenToTheTable_NotIntoACircleOfOwners()
        {
            // FB3, owned by FA2, holds FA2 by its 360: FA2 given to FB3 would own its own owner.
            string text = WithFields("F6").Replace("  0\nENDSEC\n  0\nEOF\n", "  0\nFIELD\n  5\nFB3\n330\nFA2\n100\nAcDbField\n  1\n_text\n  2\n%<\\_FldIdx 0>%\n 90\n1\n360\nFA2\n 97\n0\n  0\nENDSEC\n  0\nEOF\n");
            DxfDatabase db = DxfDatabase.Parse(text);

            Table(db).SetTextString(2, 0, "x");

            Assert.Contains("  5\nFA2\n330\nF6\n100\nAcDbField\n", Records(db.ToText()).Single(r => HandleOf(r) == "FA2"), StringComparison.Ordinal);
        }

        [Fact]
        public void TheCountOfTheDrawingsListOfFields_IsThatOfTheFieldsLeftInIt()
        {
            string list = "  0\nFIELDLIST\n  5\nFB1\n102\n{ACAD_REACTORS\n330\nA\n102\n}\n330\nA\n100\nAcDbIdSet\n 90\n5\n330\nFA1\n330\nFA2\n330\nFA3\n100\nAcDbFieldList\n";
            string text = WithFields("F6")
                .Replace("  5\nA\n330\n0\n100\nAcDbDictionary\n281\n1\n", "  5\nA\n330\n0\n100\nAcDbDictionary\n281\n1\n  3\nACAD_FIELDLIST\n350\nFB1\n")
                .Replace("  0\nENDSEC\n  0\nEOF\n", list + "  0\nENDSEC\n  0\nEOF\n");
            DxfDatabase db = DxfDatabase.Parse(text);

            Table(db).SetTextString(2, 0, "x");

            Assert.EndsWith("100\nAcDbIdSet\n 90\n1\n330\nFA3\n100\nAcDbFieldList\n", Records(db.ToText()).Single(r => HandleOf(r) == "FB1"), StringComparison.Ordinal);
        }

        [Fact]
        public void WholeNumbersOfSixteenFigures_GoOnInTheirExactStep()
        {
            foreach (string text in Versions)
            {
                DxfDatabase db = DxfDatabase.Parse(text);
                DxfTable table = Table(db);
                table.SetSize(7, 2);
                table.SetValue(3, 1, 1E15);
                table.SetValue(4, 1, 1E15 + 1);

                table.Fill(new DxfCellRange(5, 1, 6, 1), new DxfCellRange(3, 1, 4, 1), DxfTableFillOptions.GenerateSeries);

                DxfTable read = Reread(db);
                Assert.Equal(1E15 + 2, double.Parse(read.TextString(5, 1), System.Globalization.CultureInfo.InvariantCulture));
                Assert.Equal(1E15 + 3, double.Parse(read.TextString(6, 1), System.Globalization.CultureInfo.InvariantCulture));
            }
        }

        [Fact]
        public void AFormulaOfAutoCAD_IsReadFromItsFieldOfText_ItsFormatLeftOut()
        {
            string objects = "  0\nFIELD\n  5\nFA1\n330\nF6\n100\nAcDbField\n  1\n_text\n  2\n%<\\_FldIdx 0>%\n 90\n1\n360\nFA2\n 97\n0\n"
                + "  0\nFIELD\n  5\nFA2\n330\nFA1\n100\nAcDbField\n  1\nAcExpr\n  2\n\\AcExpr (Sum(A2:B3)) \\f \"%lu2\"\n 90\n0\n 97\n0\n"
                + "  0\nFIELD\n  5\nFA3\n330\nF6\n100\nAcDbField\n  1\nAcVar\n  2\n\\AcVar Date\n 90\n0\n 97\n0\n";
            string text = DxfDimensionDrawings.R2018
                .Replace("  0\nENDSEC\n  0\nEOF\n", objects + "  0\nENDSEC\n  0\nEOF\n")
                .Replace(" 92\n0\n301\nCELL_VALUE\n 93\n6\n 90\n4\n  1\nB-12\n", " 92\n0\n344\nFA1\n301\nCELL_VALUE\n 93\n6\n 90\n4\n  1\nB-12\n")
                .Replace(" 92\n0\n301\nCELL_VALUE\n 93\n6\n 90\n4\n  1\nQty\n", " 92\n0\n344\nFA3\n301\nCELL_VALUE\n 93\n6\n 90\n4\n  1\nQty\n");
            DxfTable table = Table(DxfDatabase.Parse(text));

            Assert.Equal("=Sum(A2:B3)", table.GetFormula(2, 0, 0));
            Assert.False(table.HasFormula(1, 1, 0));
        }

        [Fact]
        public void CellsCopied_BringTheirContentLookAndMerges_AndGrowTheTableWhereAsked()
        {
            foreach (string text in Versions)
            {
                DxfDatabase db = DxfDatabase.Parse(text);
                DxfTable table = Table(db);

                DxfCellRange copied = table.CopyFrom(table, DxfTableCopyOptions.ExpandOrContractTable, new DxfCellRange(1, 0, 2, 1), new DxfCellRange(3, 0, 3, 0));

                Assert.Equal(new DxfCellRange(3, 0, 4, 1), copied);
                DxfDatabase readDb = DxfDatabase.Parse(db.ToText());
                DxfTable read = Table(readDb);
                Assert.Equal(new[] { "Parts", "Mark", "B-12", "Mark", "B-12" }, Texts(read, 0));
                Assert.Equal(new[] { 10.0, 8.0, 8.0, 8.0, 8.0 }, Heights(read));
                Assert.Equal(DxfCellAlignment.MiddleCenter, read.Alignment(3, 0));
                Assert.Equal("M12", read.GetBlockAttributeValue(4, 1, readDb.BlockTable["BOLT"].OfType<DxfAttributeDefinition>().Single()));

                // The title copied onto the header brings its merge; without ExpandOrContractTable a copy past the last row is cut.
                read.CopyFrom(read, DxfTableCopyOptions.None, new DxfCellRange(0, 0, 0, 1), new DxfCellRange(1, 0, 1, 1));
                Assert.Equal(new DxfCellRange(4, 0, 4, 1), read.CopyFrom(read, DxfTableCopyOptions.None, new DxfCellRange(1, 0, 2, 1), new DxfCellRange(4, 0, 4, 0)));
                DxfTable again = Reread(readDb);
                Assert.Equal(new DxfCellRange(1, 0, 1, 1), again.GetMergeRange(1, 1));
                Assert.Equal(new DxfCellRange(4, 0, 4, 1), again.GetMergeRange(4, 1));
                Assert.Equal("Parts", again.TextString(1, 0));
                Assert.Equal("Parts", again.TextString(4, 0));
                Assert.Equal(5, again.NumRows);

                Refused<ArgumentException>(() => table.CopyFrom(Table(DxfDatabase.Parse(text)), DxfTableCopyOptions.None, null, null), "another drawing");
                Refused<ArgumentException>(() => table.CopyFrom(table, DxfTableCopyOptions.SkipValue, null, null), "SkipValue");
                Assert.Throws<ArgumentNullException>(() => table.CopyFrom(null, DxfTableCopyOptions.None, null, null));
            }
        }

        [Fact]
        public void CellsCopied_WithoutTheirFormatOrContent_KeepThatOfTheCellsTheyGoTo()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
            DxfTable table = Table(db);

            // Mark (1, 0), of its own look, copied to Qty (1, 1) as content alone, then its look alone to B-12 (2, 0).
            table.CopyFrom(table, DxfTableCopyOptions.SkipFormat, new DxfCellRange(1, 0, 1, 0), new DxfCellRange(1, 1, 1, 1));
            table.CopyFrom(table, DxfTableCopyOptions.SkipContent | DxfTableCopyOptions.SkipMerges, new DxfCellRange(1, 0, 1, 0), new DxfCellRange(2, 0, 2, 0));

            DxfTable read = Reread(db);
            Assert.Equal("Mark", read.TextString(1, 1));
            Assert.Null(read.Alignment(1, 1));
            Assert.Equal("B-12", read.TextString(2, 0));
            Assert.Equal(DxfCellAlignment.MiddleCenter, read.Alignment(2, 0));
            Assert.Equal(3.5, read.TextHeight(2, 0));
            Assert.Equal(DxfCellProperties.Alignment | DxfCellProperties.TextStyle | DxfCellProperties.TextHeight, read.GetOverrides(2, 0, 0));
        }

        [Fact]
        public void NumbersFilled_GoOnInASeries_AndOtherCellsAreCopied()
        {
            foreach (string text in Versions)
            {
                bool r2000 = text == DxfDimensionDrawings.R2000;
                DxfDatabase db = DxfDatabase.Parse(text);
                DxfTable table = Table(db);
                table.SetSize(8, 2);
                table.SetValue(3, 1, 10);
                table.SetValue(4, 1, 20);

                table.Fill(new DxfCellRange(5, 1, 7, 1), new DxfCellRange(3, 1, 4, 1), DxfTableFillOptions.GenerateSeries);
                table.Fill(new DxfCellRange(3, 0, 5, 0), new DxfCellRange(1, 0, 1, 0), DxfTableFillOptions.None);

                DxfTable read = Reread(db);
                Assert.Equal(new[] { "30", "40", "50" }, Enumerable.Range(5, 3).Select(r => read.TextString(r, 1)).ToArray());
                Assert.Equal(r2000 ? (object)"30" : 30, read.Value(5, 1));
                Assert.Equal(new[] { "Mark", "Mark", "Mark" }, Enumerable.Range(3, 3).Select(r => read.TextString(r, 0)).ToArray());
                Assert.Equal(DxfCellAlignment.MiddleCenter, read.Alignment(4, 0));

                // A cell above the source carries the series back; one number goes up by one, or down with Reverse; a step of a
                // half makes a number.
                table.Fill(new DxfCellRange(1, 1, 1, 1), new DxfCellRange(3, 1, 4, 1), DxfTableFillOptions.GenerateSeries);
                table.SetValue(6, 0, 5);
                table.Fill(new DxfCellRange(7, 0, 7, 0), new DxfCellRange(6, 0, 6, 0), DxfTableFillOptions.GenerateSeries | DxfTableFillOptions.Reverse);
                table.SetValue(3, 0, 0.5);
                table.SetValue(4, 0, 1.0);
                table.Fill(new DxfCellRange(5, 0, 5, 0), new DxfCellRange(3, 0, 4, 0), DxfTableFillOptions.GenerateSeries);
                DxfTable again = Reread(db);
                Assert.Equal("-10", again.TextString(1, 1));
                Assert.Equal("4", again.TextString(7, 0));
                Assert.Equal("1.5", again.TextString(5, 0));
                Assert.Equal(r2000 ? (object)"1.5" : 1.5, again.Value(5, 0));
                Assert.Equal(new DxfCellRange(0, 0, 0, 1), again.GetMergeRange(0, 1));

                Refused<ArgumentException>(() => table.Fill(new DxfCellRange(3, 0, 5, 1), new DxfCellRange(1, 0, 1, 0), DxfTableFillOptions.None), "columns of the source");
                Refused<ArgumentOutOfRangeException>(() => table.Fill(new DxfCellRange(3, 0, 5, 0), new DxfCellRange(1, 0, 1, 0), (DxfTableFillOptions)128), "flags");
            }
        }

        [Fact]
        public void NumbersFilledAlongARow_GoOnFromTheSourceLeftOfThem()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
            DxfTable table = Table(db);
            table.SetSize(3, 5);
            table.SetValue(1, 0, 7);

            table.Fill(new DxfCellRange(1, 1, 1, 4), new DxfCellRange(1, 0, 1, 0), DxfTableFillOptions.Row | DxfTableFillOptions.GenerateSeries | DxfTableFillOptions.CopyContent);

            DxfTable read = Reread(db);
            Assert.Equal(new object[] { 7, 8, 9, 10, 11 }, Enumerable.Range(0, 5).Select(c => read.Value(1, c)).ToArray());
            Assert.Equal(DxfCellAlignment.MiddleCenter, read.Alignment(1, 0));
            Assert.Null(read.Alignment(1, 2));
        }

        [Fact]
        public void ATableSetFromItsStyle_LosesItsOwnOverridesAndThoseOfItsCells()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
            DxfTable table = Table(db);
            table.SetGridColor(Aci(1), DxfGridLineType.HorizontalTop, DxfRowType.DataRow);

            Assert.True(table.SetFromStyle());

            DxfTable read = Reread(db);
            Assert.Empty(read.TableStyleOverrides());
            Assert.Null(read.Alignment(1, 0));
            string clean = db.ToText();
            DxfDBObject asObject = table;
            Assert.True(asObject.SetFromStyle());
            Assert.Equal(clean, db.ToText());
        }

        [Fact]
        public void RowsOfATableOf16001Rows_ArePutInTakenOutLinedAndFilled_InTimeLinearInItsGroups()
        {
            var cells = new StringBuilder();

            for (int i = 0; i < 32002; i++)
            {
                cells.Append("171\n1\n172\n0\n173\n0\n174\n0\n175\n0\n176\n0\n 91\n").Append(i % 2 == 0 ? "49" : "0").Append("\n178\n0\n145\n0.0\n 92\n0\n");
                cells.Append("301\nCELL_VALUE\n 93\n6\n 90\n4\n  1\nC").Append(i).Append("\n 94\n0\n300\n\n302\nC").Append(i).Append("\n304\nACVALUE_END\n");
                cells.Append(i % 2 == 0 ? "  7\nGH_TXT\n140\n3.5\n170\n5\n" : string.Empty);
            }

            string text = "  0\nSECTION\n  2\nHEADER\n  9\n$ACADVER\n  1\nAC1032\n  9\n$HANDSEED\n  5\n100\n  0\nENDSEC\n  0\nSECTION\n  2\nENTITIES\n"
                + "  0\nACAD_TABLE\n  5\n50\n100\nAcDbEntity\n  8\n0\n100\nAcDbBlockReference\n  2\n*T1\n 10\n0.0\n 20\n0.0\n 30\n0.0\n100\nAcDbTable\n"
                + " 91\n16001\n 92\n2\n 93\n8\n" + string.Concat(Enumerable.Repeat("141\n8.0\n", 16001)) + "142\n40.0\n142\n30.0\n" + cells
                + " 40\n1.5\n  0\nENDSEC\n  0\nEOF\n";
            DxfDatabase db = DxfDatabase.Parse(text);
            DxfTable table = Table(db);
            var watch = System.Diagnostics.Stopwatch.StartNew();

            table.InsertRowsAndInherit(8000, 0, 16000);
            table.DeleteRows(8000, 16000);
            table.InsertColumns(1, 20.0, 1);
            table.SetGridProperty(table.Cells, DxfGridLineType.AllGridLines, new DxfGridPropertyParameter { PropertyMask = DxfGridProperties.Color, Color = Aci(1) });
            table.MergeCells(new DxfCellRange(16000, 0, 16000, 2));
            table.Fill(new DxfCellRange(1, 1, 16000, 1), new DxfCellRange(0, 1, 0, 1), DxfTableFillOptions.CopyFormat);

            watch.Stop();
            Assert.True(watch.Elapsed.TotalSeconds < 10.0, "Six changes of the structure of 32 002 cells took " + watch.Elapsed.TotalSeconds + " s.");
            DxfTable read = Reread(db);
            Assert.Equal(16001, read.NumRows);
            Assert.Equal(3, read.NumColumns);
            Assert.Equal("C16000", read.TextString(8000, 0));
            Assert.Equal("C32001", read.TextString(16000, 2));
            Assert.Equal(1, read.GetGridColor(9000, 2, DxfGridLineType.VerticalRight).ColorIndex);
            Assert.Equal(new DxfCellRange(16000, 0, 16000, 2), read.GetMergeRange(16000, 2));
            Assert.Equal(1.5, read.HorizontalCellMargin);
        }

        private static void Refused<T>(Func<object> action, string part) where T : Exception
        {
            T refused = Assert.Throws<T>(action);
            Assert.Contains(part, refused.Message, StringComparison.Ordinal);
        }

        private static void Refused<T>(Action action, string part) where T : Exception
        {
            T refused = Assert.Throws<T>(action);
            Assert.Contains(part, refused.Message, StringComparison.Ordinal);
        }
    }
}
