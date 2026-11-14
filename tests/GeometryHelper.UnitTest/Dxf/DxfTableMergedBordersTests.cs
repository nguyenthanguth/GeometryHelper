using System;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The lines set on the top left cell of a block of merged cells of a table made in code are given to the cells along the
    /// block's edges, as AutoCAD 2021 reads them from a file: the numbers are those its console gave the same tables saved
    /// as DXF and read back (8 tables of 4 rows and 4 columns, every cell's four lines).
    /// </summary>
    public class DxfTableMergedBordersTests
    {
        private static DxfTable Table()
        {
            var db = new DxfDatabase("AC1032");
            db.SetHeader("$MEASUREMENT", new DxfTypedValue(70, "1"));
            db.NamedObjectsDictionary.SetAt("ACAD_TABLESTYLE", new DxfDBDictionary());
            db.TableStyleDictionary.SetAt("Standard", new DxfTableStyle());
            var table = new DxfTable(new GeoPoint3(0, 0, 0), 4, 4, 9.0, 30.0);
            db.ModelSpace.AppendEntity(table);
            return table;
        }

        private static DxfColor Index(int index) => DxfColor.FromColorIndex(DxfColorMethod.ByAci, (short)index);

        /// <summary>The four lines of a cell given weights and colours: top 50 red, right 70 yellow, bottom 90 green, left 100 cyan.</summary>
        private static void FourLines(DxfTable table, int row, int column)
        {
            table.SetGridLineWeight(row, column, DxfGridLineType.HorizontalTop, (DxfLineWeight)50);
            table.SetGridLineWeight(row, column, DxfGridLineType.VerticalRight, (DxfLineWeight)70);
            table.SetGridLineWeight(row, column, DxfGridLineType.HorizontalBottom, (DxfLineWeight)90);
            table.SetGridLineWeight(row, column, DxfGridLineType.VerticalLeft, (DxfLineWeight)100);
            table.SetGridColor(row, column, DxfGridLineType.HorizontalTop, Index(1));
            table.SetGridColor(row, column, DxfGridLineType.VerticalRight, Index(2));
            table.SetGridColor(row, column, DxfGridLineType.HorizontalBottom, Index(3));
            table.SetGridColor(row, column, DxfGridLineType.VerticalLeft, Index(4));
        }

        /// <summary>The top, right, bottom and left lines of a cell as "weight/colour", a dash for one it holds none of.</summary>
        private static string Lines(DxfTable table, int row, int column)
        {
            DxfGridLineType[] edges =
            {
                DxfGridLineType.HorizontalTop,
                DxfGridLineType.VerticalRight,
                DxfGridLineType.HorizontalBottom,
                DxfGridLineType.VerticalLeft,
            };
            return string.Join(
                " ",
                edges.Select(e =>
                {
                    DxfLineWeight? weight = table.GetGridLineWeight(row, column, e);
                    DxfColor color = table.GetGridColor(row, column, e);
                    return weight.HasValue
                        ? (int)weight.Value + (color == null ? string.Empty : "/" + color.ColorIndex)
                        : "-";
                })
            );
        }

        [Fact]
        public void LinesSetOnTheAnchorOfABlockOfTwoRowsAndColumns_AreGivenAlongItsTopAndLeft_AsAutoCadReadsThem()
        {
            DxfTable table = Table();
            table.MergeCells(new DxfCellRange(1, 1, 2, 2));
            FourLines(table, 1, 1);

            // AutoCAD 2021, the table written as DXF and read back: the anchor's top over (1, 2) too, and its left beside (2, 1);
            // its right and bottom lie inside the block, read as the left of (1, 2) and the top of (2, 1), and are given to no
            // cell along the block's right and bottom.
            Assert.Equal("50/1 70/2 90/3 100/4", Lines(table, 1, 1));
            Assert.Equal("50/1 - - 70/2", Lines(table, 1, 2));
            Assert.Equal("90/3 - - 100/4", Lines(table, 2, 1));
            Assert.Equal("- - - -", Lines(table, 2, 2));
            Assert.Equal("- - 50/1 -", Lines(table, 0, 1));
            Assert.Equal("- - 50/1 -", Lines(table, 0, 2));
            Assert.Equal("- 100/4 - -", Lines(table, 1, 0));
            Assert.Equal("- 100/4 - -", Lines(table, 2, 0));
            Assert.Equal("- - - -", Lines(table, 3, 1));
            Assert.Equal("- - - -", Lines(table, 1, 3));
        }

        [Fact]
        public void LinesSetOnTheAnchorBeforeAndAfterTheMerge_ReadTheSame()
        {
            DxfTable after = Table();
            after.MergeCells(new DxfCellRange(1, 1, 2, 2));
            FourLines(after, 1, 1);
            DxfTable before = Table();
            FourLines(before, 1, 1);
            before.MergeCells(new DxfCellRange(1, 1, 2, 2));

            for (int r = 0; r < 4; r++)
            {
                for (int c = 0; c < 4; c++)
                {
                    Assert.Equal(Lines(before, r, c), Lines(after, r, c));
                }
            }
        }

        [Fact]
        public void ABlockOfOneRow_GivesItsBottomAlong_AndABlockOfOneColumn_ItsRight()
        {
            // AutoCAD 2021: the title merged across 4 columns, its four lines set; the top and the bottom are along the four
            // cells, the right is inside the block, read as the left of the second cell, and no cell has it at the block's end.
            DxfTable title = Table();
            FourLines(title, 0, 0);
            Assert.Equal("50/1 - 90/3 70/2", Lines(title, 0, 1));
            Assert.Equal("50/1 - 90/3 -", Lines(title, 0, 2));
            Assert.Equal("50/1 - 90/3 -", Lines(title, 0, 3));
            Assert.Equal("90/3 - - -", Lines(title, 1, 3));

            // A block of three rows in one column, its four lines set: left and right are along its three cells, its bottom
            // (inside the block) is the top of the second, and its top reaches no further.
            DxfTable column = Table();
            column.MergeCells(new DxfCellRange(1, 1, 3, 1));
            FourLines(column, 1, 1);
            Assert.Equal("90/3 70/2 - 100/4", Lines(column, 2, 1));
            Assert.Equal("- 70/2 - 100/4", Lines(column, 3, 1));
            Assert.Equal("- - - 70/2", Lines(column, 3, 2));
            Assert.Equal("- 100/4 - -", Lines(column, 3, 0));

            // A block of one row among others, merged across 4 columns, its bottom set: the bottom of each of its cells.
            DxfTable row = Table();
            row.MergeCells(new DxfCellRange(1, 0, 1, 3));
            row.SetGridLineWeight(1, 0, DxfGridLineType.HorizontalBottom, (DxfLineWeight)90);

            for (int c = 0; c < 4; c++)
            {
                Assert.Equal("- - 90 -", Lines(row, 1, c));
                Assert.Equal("90 - - -", Lines(row, 2, c));
            }
        }

        [Fact]
        public void ALineAnotherCellOfTheBlockHoldsOfItsOwn_IsKept_WhateverTheOrder()
        {
            // AutoCAD 2021 read (1, 2) with its own top of 140 and the anchor's top of 50 as they stood: each its own.
            DxfTable table = Table();
            table.MergeCells(new DxfCellRange(1, 1, 2, 2));
            table.SetGridLineWeight(1, 2, DxfGridLineType.HorizontalTop, (DxfLineWeight)140);
            table.SetGridLineWeight(1, 1, DxfGridLineType.HorizontalTop, (DxfLineWeight)50);

            Assert.Equal((DxfLineWeight)50, table.GetGridLineWeight(1, 1, DxfGridLineType.HorizontalTop));
            Assert.Equal((DxfLineWeight)140, table.GetGridLineWeight(1, 2, DxfGridLineType.HorizontalTop));
        }

        [Fact]
        public void TheColourOfALineSetAfterItsWeight_IsGivenAlongToo_AndAHiddenLineIsHiddenAlong()
        {
            DxfTable table = Table();
            table.MergeCells(new DxfCellRange(1, 1, 2, 2));
            table.SetGridLineWeight(1, 1, DxfGridLineType.HorizontalTop, (DxfLineWeight)50);
            table.SetGridColor(1, 1, DxfGridLineType.HorizontalTop, Index(1));
            table.SetGridVisibility(1, 1, DxfGridLineType.VerticalLeft, false);

            Assert.Equal(Index(1), table.GetGridColor(1, 2, DxfGridLineType.HorizontalTop));
            Assert.Equal((DxfLineWeight)50, table.GetGridLineWeight(1, 2, DxfGridLineType.HorizontalTop));
            Assert.False(table.GetGridVisibility(2, 1, DxfGridLineType.VerticalLeft));
            Assert.False(table.GetGridVisibility(2, 0, DxfGridLineType.VerticalRight));
            Assert.Null(table.GetGridVisibility(2, 2, DxfGridLineType.VerticalLeft));
        }

        [Fact]
        public void ACellOfNoBlock_GivesNothingAlong()
        {
            DxfTable table = Table();
            table.SetGridLineWeight(1, 1, DxfGridLineType.HorizontalBottom, (DxfLineWeight)90);

            Assert.Equal("- - 90 -", Lines(table, 1, 1));
            Assert.Equal("90 - - -", Lines(table, 2, 1));
            Assert.Equal("- - - -", Lines(table, 1, 2));
        }
    }
}
