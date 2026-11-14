using System;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using GeometryHelper.UnitTest.Dxf;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// A table's rows and columns grow to hold the block of a block cell, and a row that mixes cells of looks of their own is
    /// laid out, as AutoCAD 2021's <c>GenerateLayout</c> lays them out. The numbers are those its console gave the same
    /// tables (margins 1.5): a block 10 by 40 at scale 1 makes its row 43, over two merged rows 21.5 each, at scale 2 41.5
    /// each.
    /// </summary>
    public class TableBlockCellFitTests
    {
        private const int Places = 5;

        /// <summary>
        /// AutoCAD's own table drawing (3 rows of 10, 8 and 8, columns of 40 and 30, a block cell at (2, 1), margins 1.5), its
        /// block BOLT holding a rectangle of the size given at its base point in place of its circle, laid out as the editor
        /// lays a table out.
        /// </summary>
        private static DxfTable Drawn(double width, double height, Action<DxfTable> change, string head = null)
        {
            string text = DxfDimensionDrawings.R2018;

            if (head != null)
            {
                const string Head = " 93\n0\n 94\n0\n 95\n0\n 96\n0\n141\n10.0\n";
                Assert.Contains(Head, text, StringComparison.Ordinal);
                text = text.Replace(Head, head);
            }

            DxfDatabase db = DxfDatabase.Parse(text);
            DxfBlockTableRecord bolt = db.BlockTable["BOLT"];

            foreach (DxfCircle circle in bolt.OfType<DxfCircle>().ToList())
            {
                circle.Erase();
            }

            var corners = new[]
            {
                new GeoPoint3(0, 0, 0),
                new GeoPoint3(width, 0, 0),
                new GeoPoint3(width, height, 0),
                new GeoPoint3(0, height, 0),
            };

            for (int i = 0; i < 4; i++)
            {
                bolt.AppendEntity(new DxfLine(corners[i], corners[(i + 1) % 4]));
            }

            DxfTable table = db.ModelSpace.OfType<DxfTable>().First();
            change(table);
            CadDocument document = CadDocument.From(db);
            document.Fonts = new CadFonts(null, CadFonts.BuiltInName, null);
            table = (DxfTable)document.FindEntity(table.Handle);
            CadTableEditor.Redraw(table, document);
            return table;
        }

        [Theory]
        [InlineData(1.0, 43.0)]
        [InlineData(2.0, 83.0)]
        public void ABlockCellNotFittedToItsCell_GrowsItsRow_ToItsHeightAndTheMargins(double scale, double row)
        {
            // A block 10 by 40 at scale 1: 40 and two margins of 1.5, 43, where the row stood at 8.
            DxfTable table = Drawn(10, 40, t => t.SetBlockScale(2, 1, scale));

            Assert.Equal(row, table.RowHeight(2), Places);
            Assert.Equal(40.0, table.ColumnWidth(0), Places);
            Assert.Equal(30.0, table.ColumnWidth(1), Places);
        }

        [Theory]
        [InlineData(1.0, 21.5)]
        [InlineData(2.0, 41.5)]
        public void ABlockCellAcrossTwoRows_SharesItsNeed_AmongThem_AsABlockOfWordsDoes(double scale, double each)
        {
            DxfTable table = Drawn(10, 40, t =>
            {
                t.InsertRows(3, 8.0, 1);
                t.MergeCells(new DxfCellRange(2, 1, 3, 1));
                t.SetBlockScale(2, 1, scale);
            });

            Assert.Equal(each, table.RowHeight(2), Places);
            Assert.Equal(each, table.RowHeight(3), Places);
        }

        [Fact]
        public void ABlockCellFittedToItsCell_GrowsNoRow_AndNoColumn()
        {
            DxfTable table = Drawn(10, 40, t => t.SetAutoScale(2, 1, true));

            Assert.Equal(9.0, table.RowHeight(2), Places);
            Assert.Equal(30.0, table.ColumnWidth(1), Places);
        }

        [Fact]
        public void ABlockCellWiderThanItsColumn_GrowsTheColumn_AsAutoCadLaysItOut()
        {
            // A block 100 by 5 in a column 30 wide: 100 and two margins, 103; its row stands at the 9 of B-12 beside it (one line of 4.5 and the margins), which holds the 5 and the margins.
            DxfTable table = Drawn(100, 5, t => { });

            Assert.Equal(103.0, table.ColumnWidth(1), Places);
            Assert.Equal(9.0, table.RowHeight(2), Places);
        }

        [Fact]
        public void ATurnedBlockCell_GrowsItsRowAndColumn_ToTheBoxItsTurnedExtentsFill()
        {
            // 45 degrees: the box of a block 10 by 40 is 50 times the root of a half in each side, 35.35534, and 38.35534 with
            // the margins, in the row and in the column.
            DxfTable table = Drawn(10, 40, t => t.SetBlockRotation(2, 1, Math.PI / 4.0));

            Assert.Equal(38.35534, table.RowHeight(2), Places);
            Assert.Equal(38.35534, table.ColumnWidth(1), Places);
        }

        /// <summary>The look groups AutoCAD's console gave a table whose data and header have a text height of their own of 8.</summary>
        private const string OwnEight =
            " 93\n6886016\n 94\n0\n 95\n0\n 96\n0\n  7\nGH_TXT\n140\n8.0\n170\n1\n 63\n3\n 64\n1\n140\n8.0\n283\n0\n141\n10.0\n";

        [Fact]
        public void ARowWhoseFirstCellHasATextHeightOfItsOwn_IsLaidOutByItsFirstCellsOneLine_AsInAutoCad()
        {
            // AutoCAD 2021, the data of the table at 8 (13.66667 for a row of empty cells), the first cell of a row given 6: the
            // row is 6 * 4 / 3 + 2 * 1.5 = 11, not the 13.66667 the empty cells at 8 made it; the first cell at 8 and the
            // second given 6, or 12 and empty, leaves 13.66667; a first cell of 12 makes it 19.
            DxfTable table = Drawn(
                4,
                4,
                t =>
                {
                    t.InsertRows(3, 8.0, 3);
                    t.SetTextHeight(3, 0, 6.0);
                    t.SetTextHeight(4, 1, 6.0);
                    t.SetTextHeight(5, 0, 12.0);
                },
                OwnEight
            );

            Assert.Equal(11.0, table.RowHeight(3), Places);
            Assert.Equal(13.66667, table.RowHeight(4), Places);
            Assert.Equal(19.0, table.RowHeight(5), Places);
        }

        [Fact]
        public void ACellAfterTheFirst_IsLaidOutByItsWordsAlone_AndAnEmptyOneAddsNothing()
        {
            // AutoCAD 2021: the first cell given 6 (11 alone) and the second, holding x, given 12: the row is the 12 of the x and
            // the margins, 15, not 12 * 4 / 3 + 3 = 19; the same cell empty leaves the 11.
            DxfTable table = Drawn(
                4,
                4,
                t =>
                {
                    t.InsertRows(3, 8.0, 2);
                    t.SetTextHeight(3, 0, 6.0);
                    t.SetTextHeight(3, 1, 12.0);
                    t.SetTextString(3, 1, "x");
                    t.SetTextHeight(4, 0, 6.0);
                    t.SetTextHeight(4, 1, 12.0);
                },
                OwnEight
            );

            Assert.Equal(15.0, table.RowHeight(3), Places);
            Assert.Equal(11.0, table.RowHeight(4), Places);
        }
    }
}
