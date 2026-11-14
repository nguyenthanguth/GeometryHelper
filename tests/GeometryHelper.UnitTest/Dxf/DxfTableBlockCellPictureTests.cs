using System;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The block of a block cell in the picture of a table (<see cref="DxfTable.RecomputeTableBlock"/>) stands as AutoCAD 2021
    /// draws it: its extents (the block's own box, in from the block's base point) aligned in the cell by the cell's alignment,
    /// the margins in from its sides, and the scale of a cell whose block is fitted the one that fits the extents into the
    /// cell less its margins, a cell of the middle (the tests set it, which the table style of the drawing leaves at the top). The numbers are those AutoCAD's console gave its own tables of 2 rows and columns, margins 1.5.
    /// </summary>
    public class DxfTableBlockCellPictureTests
    {
        private const int Places = 5;

        /// <summary>
        /// AutoCAD's own table drawing (3 rows of 10, 8 and 8, columns of 40 and 30, a block cell at (2, 1), margins 1.5), its
        /// block BOLT holding a rectangle of the corners given in place of its circle, the rows and columns of the sizes given.
        /// </summary>
        private static DxfTable Prepared(
            double[] heights,
            double[] widths,
            double x0,
            double y0,
            double width,
            double height
        )
        {
            DxfDatabase db = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
            DxfBlockTableRecord bolt = db.BlockTable["BOLT"];

            foreach (DxfCircle circle in bolt.OfType<DxfCircle>().ToList())
            {
                circle.Erase();
            }

            var corners = new[]
            {
                new GeoPoint3(x0, y0, 0),
                new GeoPoint3(x0 + width, y0, 0),
                new GeoPoint3(x0 + width, y0 + height, 0),
                new GeoPoint3(x0, y0 + height, 0),
            };

            for (int i = 0; i < 4; i++)
            {
                bolt.AppendEntity(new DxfLine(corners[i], corners[(i + 1) % 4]));
            }

            DxfTable table = db.ModelSpace.OfType<DxfTable>().First();
            table.SetAlignment(2, 1, DxfCellAlignment.MiddleCenter);

            for (int r = 0; r < heights.Length; r++)
            {
                table.SetRowHeight(r, heights[r]);
            }

            for (int c = 0; c < widths.Length; c++)
            {
                table.SetColumnWidth(c, widths[c]);
            }

            return table;
        }

        private static DxfTable Tens() => Prepared(new[] { 10.0, 8, 8 }, new[] { 40.0, 30 }, 0, 0, 10, 40);

        /// <summary>The table with a cell (2, 1) 60 high and 40 wide at x 40 and y -60, as AutoCAD's 2 by 2 tables of 60 by 40 have their last cell.</summary>
        private static DxfTable Sixties(double x0 = 0, double y0 = 0) =>
            Prepared(new[] { 30.0, 30, 60 }, new[] { 40.0, 40 }, x0, y0, 10, 40);

        private static DxfBlockReference Insert(DxfTable table)
        {
            table.RecomputeTableBlock();
            return table.BlockTableRecord.OfType<DxfBlockReference>().Single();
        }

        private static void At(DxfBlockReference insert, double x, double y, double scale, double turn = 0.0)
        {
            Assert.Equal(x, insert.Position.X, Places);
            Assert.Equal(y, insert.Position.Y, Places);
            Assert.Equal(scale, insert.ScaleFactors.X, Places);
            Assert.Equal(scale, insert.ScaleFactors.Y, Places);
            Assert.Equal(turn, insert.Rotation, Places);
        }

        [Fact]
        public void ABlockCellOfAnExtentsAwayFromTheBlocksBasePoint_IsCentredByItsExtents()
        {
            // Rows 10, 8 and 8, the cell (2, 1) at x 40 to 70 and y -18 to -26: the block, a rectangle 10 by 40 from its base
            // point, is centred on (55, -22) by its extents, so that its insertion point is at (50, -42), where the circle of
            // BOLT, which is centred on its base point, stood at the middle.
            DxfTable table = Tens();
            At(Insert(table), 50.0, -42.0, 1.0);

            // The circle of BOLT as AutoCAD's table drawing has it: its insertion point at the centre of the cell.
            DxfDatabase original = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
            DxfTable own = original.ModelSpace.OfType<DxfTable>().First();
            own.SetAlignment(2, 1, DxfCellAlignment.MiddleCenter);
            own.RecomputeTableBlock();
            At(own.BlockTableRecord.OfType<DxfBlockReference>().Single(), 55.0, -22.0, 1.0);
        }

        [Theory]
        [InlineData(DxfCellAlignment.TopLeft, 41.5, -59.5)]
        [InlineData(DxfCellAlignment.TopCenter, 50.0, -59.5)]
        [InlineData(DxfCellAlignment.MiddleLeft, 41.5, -42.0)]
        [InlineData(DxfCellAlignment.BottomRight, 58.5, -24.5)]
        public void ABlockCell_StandsInItsCellByItsAlignment_TheMarginsInFromItsSides(
            DxfCellAlignment alignment,
            double x,
            double y
        )
        {
            // AutoCAD 2021: the extents' top left corner at the cell's top left corner moved in by the margins, 1.5; the block
            // 10 wide and 40 high.
            DxfTable table = Tens();
            table.SetAlignment(2, 1, alignment);
            At(Insert(table), x, y, 1.0);
        }

        [Fact]
        public void ABlockCellFittedToItsCell_IsDrawnAtTheScaleThatFitsItsExtentsInTheCellLessItsMargins()
        {
            // The cell is 30 wide and 8 high, 27 and 5 within its margins: a block 10 by 40 fits at the least of 2.7 and 0.125.
            DxfTable table = Tens();
            table.SetAutoScale(2, 1, true);
            At(Insert(table), 54.375, -24.5, 0.125);

            // The same block in a cell 40 wide and 60 high (37 and 57 within the margins) fits at 1.425, the height: 57 over 40,
            // 14.25 wide, centred on (60, -90).
            DxfTable big = Sixties();
            big.SetAutoScale(2, 1, true);
            At(Insert(big), 52.875, -118.5, 1.425);
        }

        [Fact]
        public void ABlockCellFittedAcrossTwoMergedRows_IsFittedToTheWholeBlock()
        {
            // AutoCAD 2021: a block 10 by 40 fitted to cells merged over 60 by 18 gave 0.375 (15 within the margins over 40),
            // centred on the merged cell. Here the cell is 30 by 16 (2 rows of 8) at x 40 and y -18: 0.325, 13 over 40, centred
            // on (55, -26).
            DxfTable table = Tens();
            table.InsertRows(3, 8.0, 1);
            table.MergeCells(new DxfCellRange(2, 1, 3, 1));
            table.SetAutoScale(2, 1, true);
            At(Insert(table), 53.375, -32.5, 0.325);
        }

        [Theory]
        [InlineData(30.0, 67.31973, -115.58801, 1.29099)]
        [InlineData(90.0, 78.5, -103.875, 0.925)]
        [InlineData(180.0, 81.375, -118.5, 1.425)]
        public void ATurnedBlockCellFitted_IsFittedByTheBoxItsTurnedExtentsFill(
            double degrees,
            double x,
            double y,
            double scale
        )
        {
            // AutoCAD 2021, a block 10 by 40 in a cell 40 wide and 60 high: turned 30 degrees its box is 28.66 by 39.64 and fits
            // at 37 over 28.66; turned 90 degrees it is 40 by 10 and fits at 37 over 40.
            DxfTable table = Sixties();
            table.SetAutoScale(2, 1, true);
            table.SetBlockRotation(2, 1, degrees * Math.PI / 180.0);
            At(Insert(table), x, y, scale, degrees * Math.PI / 180.0);
        }

        [Theory]
        [InlineData(DxfCellAlignment.MiddleCenter, 45.0, 100.6066, -107.67767)]
        [InlineData(DxfCellAlignment.TopLeft, 30.0, 81.5, -101.14102)]
        [InlineData(DxfCellAlignment.BottomRight, 135.0, 118.5, -90.21573)]
        public void ATurnedBlockCell_StandsByTheBoxItsTurnedExtentsFill(
            DxfCellAlignment alignment,
            double degrees,
            double x,
            double y
        )
        {
            // The cell is 60 by 60 at x 60 and y -60, the block 10 by 40 turned about its base point: its turned box is aligned
            // in the cell, as AutoCAD 2021 aligned it at 30, 45, 135 and 315 degrees.
            DxfTable table = Prepared(new[] { 30.0, 30, 60 }, new[] { 60.0, 60 }, 0, 0, 10, 40);
            table.SetAlignment(2, 1, alignment);
            table.SetBlockRotation(2, 1, degrees * Math.PI / 180.0);
            At(Insert(table), x, y, 1.0, degrees * Math.PI / 180.0);
        }

        [Theory]
        [InlineData(90.0, DxfCellAlignment.TopLeft, 101.5, -81.5)]
        [InlineData(90.0, DxfCellAlignment.MiddleCenter, 110.0, -105.0)]
        [InlineData(180.0, DxfCellAlignment.TopLeft, 81.5, -101.5)]
        [InlineData(180.0, DxfCellAlignment.BottomRight, 128.5, -118.5)]
        [InlineData(270.0, DxfCellAlignment.TopLeft, 101.5, -61.5)]
        [InlineData(270.0, DxfCellAlignment.BottomRight, 118.5, -108.5)]
        public void ABlockCellTurnedAQuarter_StandsWhereAutoCadPutsIt_ItsBoxOffByTheBlocksWidthAndHeight(
            double degrees,
            DxfCellAlignment alignment,
            double x,
            double y
        )
        {
            // AutoCAD 2021 puts a block turned by exactly 90, 180 or 270 degrees off the box it aligns in the cell, by (0, -width),
            // (width, -height) and (height, 0) of the block's extents: a block 10 by 40 turned 90 degrees at the cell's top left
            // stands with its box 10 lower than the cell's top.
            DxfTable table = Prepared(new[] { 30.0, 30, 60 }, new[] { 60.0, 60 }, 0, 0, 10, 40);
            table.SetAlignment(2, 1, alignment);
            table.SetBlockRotation(2, 1, degrees * Math.PI / 180.0);
            At(Insert(table), x, y, 1.0, degrees * Math.PI / 180.0);
        }

        [Theory]
        [InlineData(5.0, 7.0, 90.0, DxfCellAlignment.TopLeft, 108.5, -86.5)]
        [InlineData(5.0, 7.0, 180.0, DxfCellAlignment.MiddleCenter, 110.0, -103.0)]
        [InlineData(-20.0, -3.0, 90.0, DxfCellAlignment.MiddleCenter, 107.0, -85.0)]
        [InlineData(-20.0, -3.0, 180.0, DxfCellAlignment.TopLeft, 61.5, -104.5)]
        [InlineData(-20.0, -3.0, 270.0, DxfCellAlignment.MiddleCenter, 113.0, -105.0)]
        public void ATurnedBlockCell_OfExtentsAwayFromItsBasePoint_IsAlignedByItsExtents(
            double x0,
            double y0,
            double degrees,
            DxfCellAlignment alignment,
            double x,
            double y
        )
        {
            DxfTable table = Prepared(new[] { 30.0, 30, 60 }, new[] { 60.0, 60 }, x0, y0, 10, 40);
            table.SetAlignment(2, 1, alignment);
            table.SetBlockRotation(2, 1, degrees * Math.PI / 180.0);
            At(Insert(table), x, y, 1.0, degrees * Math.PI / 180.0);
        }
    }
}
