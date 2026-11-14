using System;
using System.Diagnostics;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The lines of a table's cells set one cell at a time (<c>SetGridColor</c>, <c>SetGridLineWeight</c>,
    /// <c>SetGridVisibility</c> and <c>SetGridProperty</c> of a cell) write the bytes of the cells they change and copy the rest
    /// of the record, where each call read and wrote every group of it.
    /// </summary>
    public class DxfTableGridSpeedTests
    {
        private static readonly DxfGridLineType[] Edges =
        {
            DxfGridLineType.HorizontalTop,
            DxfGridLineType.VerticalRight,
            DxfGridLineType.HorizontalBottom,
            DxfGridLineType.VerticalLeft,
        };

        private static DxfDatabase Drawing(int rows, int columns)
        {
            var db = new DxfDatabase("AC1032");
            db.SetHeader("$MEASUREMENT", new DxfTypedValue(70, "1"));
            db.NamedObjectsDictionary.SetAt("ACAD_TABLESTYLE", new DxfDBDictionary());
            db.TableStyleDictionary.SetAt("Standard", new DxfTableStyle());
            db.ModelSpace.AppendEntity(new DxfTable(new GeoPoint3(0, 0, 0), rows, columns, 9.0, 30.0));
            return db;
        }

        private static DxfTable TableOf(DxfDatabase db) => db.ModelSpace.OfType<DxfTable>().Single();

        private static DxfColor Index(int index) => DxfColor.FromColorIndex(DxfColorMethod.ByAci, (short)index);

        /// <summary>One setting of a cell's lines, of four kinds: a lineweight, a colour, whether it shows, a colour and a lineweight.</summary>
        private static void Setting(DxfTable table, int row, int column, int seed, int kind)
        {
            switch (kind)
            {
                case 0:
                    table.SetGridLineWeight(row, column, Edges[seed % 4], (DxfLineWeight)(seed % 2 == 0 ? 50 : 70));
                    break;
                case 1:
                    table.SetGridColor(row, column, Edges[(seed + 1) % 4], Index(1 + seed % 7));
                    break;
                case 2:
                    table.SetGridVisibility(row, column, Edges[(seed + 2) % 4], seed % 3 != 0);
                    break;
                default:
                    table.SetGridProperty(
                        row,
                        column,
                        Edges[(seed + 3) % 4],
                        new DxfGridPropertyParameter
                        {
                            PropertyMask = DxfGridProperties.Color | DxfGridProperties.LineWeight,
                            Color = Index(2 + seed % 5),
                            LineWeight = (DxfLineWeight)100,
                        }
                    );
                    break;
            }
        }

        /// <summary>One cell's lines set the way a loop over a table does: the four kinds of setting.</summary>
        private static void Lines(DxfTable table, int row, int column, int seed)
        {
            for (int kind = 0; kind < 4; kind++)
            {
                Setting(table, row, column, seed, kind);
            }
        }

        [Fact]
        public void TheLinesOfEveryCellOfATableOf600Cells_SetOneCellAtATime_AreWrittenInTimeLinearInItsBytes()
        {
            // 60 rows by 10 columns, 2 400 calls: a call read and wrote the 30 000 groups of the record, 8 ms each, 19 s in all
            // (1 000 cells took 40 s for 4 000 calls); now each copies the bytes of the cells it changes into one of its own.
            DxfDatabase db = Drawing(60, 10);
            DxfTable table = TableOf(db);
            var watch = Stopwatch.StartNew();

            for (int r = 0; r < 60; r++)
            {
                for (int c = 0; c < 10; c++)
                {
                    Lines(table, r, c, 10 * r + c);
                }
            }

            watch.Stop();
            Assert.True(
                watch.Elapsed.TotalSeconds < 5.0,
                "2 400 line settings of 600 cells took " + watch.Elapsed.TotalSeconds + " s."
            );

            // The last cell's left (70) and bottom (100) are its own, read from it whatever its neighbours were given.
            string text = db.ToText();
            DxfDatabase reread = DxfDatabase.Parse(text);
            DxfTable read = TableOf(reread);
            Assert.Equal(text, reread.ToText());
            Assert.Equal((DxfLineWeight)70, read.GetGridLineWeight(59, 9, DxfGridLineType.VerticalLeft));
            Assert.Equal((DxfLineWeight)100, read.GetGridLineWeight(59, 9, DxfGridLineType.HorizontalBottom));
        }

        [Fact]
        public void LinesSetOneCellAtATime_AreWrittenAsTheyAreWhenEveryGroupOfTheRecordIsWrittenEachTime()
        {
            // The same settings made twice. In one, a text set on another cell before each of them writes the whole record
            // (every group as the file writes it); in the other nothing does, and the cells' own bytes alone are written. The
            // drawings are the same, byte for byte.
            DxfDatabase whole = Drawing(5, 4);
            DxfDatabase cells = Drawing(5, 4);
            DxfTable wholeTable = TableOf(whole);
            DxfTable cellsTable = TableOf(cells);
            var random = new Random(7);

            for (int step = 0; step < 120; step++)
            {
                int row = random.Next(4);
                int column = random.Next(3);
                int seed = random.Next(1000);
                wholeTable.SetTextString(4, 3, "text " + step);

                // Two blocks of merged cells, the lines set on one before and after it is merged.
                if (step == 30 || step == 70)
                {
                    var block = step == 30 ? new DxfCellRange(1, 0, 2, 1) : new DxfCellRange(2, 2, 3, 3);
                    wholeTable.MergeCells(block);
                    cellsTable.MergeCells(block);
                }

                Setting(wholeTable, row, column, seed, step % 4);
                Setting(cellsTable, row, column, seed, step % 4);
            }

            cellsTable.SetTextString(4, 3, "text 119");
            Assert.Equal(whole.ToText(), cells.ToText());
        }
    }
}
