using System;
using System.Linq;
using System.Text;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The work of dxfver13: a reset of a cell's value, a change of what it holds and the lines of a cell written into the
    /// place of that cell's own bytes, as the other setters of a cell are; the end of the last cell of a table found again
    /// after a write, which the place kept for it left wrong once the cell's flags changed; the first save after an edit
    /// finding the tables of a drawing without indexing its every entity. The speed is held by the bytes allocated, which do
    /// not vary with the machine as a time does.
    /// </summary>
    public class DxfSpeed13Tests
    {
        private static DxfColor Aci(int index) => DxfColor.FromColorIndex(DxfColorMethod.ByAci, (short)index);

        private static DxfTable Filled(DxfDatabase db, int rows, int columns)
        {
            var table = new DxfTable(new GeoPoint3(0, 0, 0), rows, columns, 8.0, 30.0);
            db.ModelSpace.AppendEntity(table);

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < columns; c++)
                {
                    table.SetTextString(r, c, "cell " + r + "," + c);
                }
            }

            return table;
        }

        #region The last cell

        [Theory]
        [InlineData("AC1032")]
        [InlineData("AC1027")]
        [InlineData("AC1021")]
        public void ATextSetInTheLastCell_AfterItsLinesAndItsOverrides_IsReadBackAsSet(string version)
        {
            // A table of 2 rows and 2 columns: the lines of its last cell, then its overrides, then its text. The overrides
            // changed the flags that decide where the cell ends and the table's own part begins, and the place kept for that end
            // was left behind: the text went into the value of the table, which follows the cell, and the cell read as empty.
            DxfDatabase db = DxfSpeed12Scenarios.Metric(version);
            var table = new DxfTable(new GeoPoint3(0, 0, 0), 2, 2, 8.0, 30.0);
            db.ModelSpace.AppendEntity(table);

            table.SetGridColor(1, 1, DxfGridLineType.HorizontalBottom, Aci(3));
            table.SetOverrides(1, 1, 0, (DxfCellProperties)0x3F);
            table.SetTextString(1, 1, "LAST");

            Assert.Equal("LAST", table.TextString(1, 1));
            Assert.Equal(string.Empty, table.TextString(0, 0));
            Assert.Equal(
                "LAST",
                DxfDatabase.Parse(db.ToText()).ModelSpace.OfType<DxfTable>().Single().TextString(1, 1)
            );
        }

        #endregion

        #region The same edits on a record read anew each time

        // One edit of a table of 5 rows and 4 columns, the cell and the kind picked by a generator of numbers of its own, the
        // last cell among them: of the kinds that write a cell, with the resets and the lines of a cell.
        private static void Edit(DxfTable table, long[] state, int i)
        {
            int Next(int below)
            {
                state[0] = (state[0] * 6364136223846793005L + 1442695040888963407L) & long.MaxValue;
                return (int)((state[0] >> 33) % below);
            }

            int r = Next(table.NumRows);
            int c = Next(table.NumColumns);

            if (Next(4) == 0)
            {
                r = table.NumRows - 1;
                c = table.NumColumns - 1;
            }

            int p = Next(1000);
            var lines = new[]
            {
                DxfGridLineType.HorizontalTop,
                DxfGridLineType.HorizontalBottom,
                DxfGridLineType.VerticalLeft,
                DxfGridLineType.VerticalRight,
            };

            switch (Next(14))
            {
                case 0:
                case 1:
                    table.SetTextString(r, c, "cell " + r + "," + c + " #" + i);
                    break;
                case 2:
                    table.SetValue(r, c, r * 10 + c + p / 7.0);
                    break;
                case 3:
                    table.SetTextHeight(r, c, 1.5 + (p % 5));
                    break;
                case 4:
                    table.SetContentColor(r, c, Aci(1 + (p % 7)));
                    break;
                case 5:
                    table.SetOverrides(r, c, 0, (DxfCellProperties)(p % 256));
                    break;
                case 6:
                case 7:
                    table.ResetValue(r, c);
                    break;
                case 8:
                    table.SetCellType(r, c, DxfTableCellType.TextCell);
                    break;
                case 9:
                case 10:
                    table.SetGridColor(r, c, lines[p % 4], Aci(1 + (p % 7)));
                    break;
                case 11:
                    table.SetGridVisibility(r, c, lines[p % 4], p % 2 == 0);
                    break;
                case 12:
                    table.SetFormat(r, c, p % 2 == 0 ? "%lu2%pr2" : "%lu4%pr2");
                    break;
                default:
                    table.SetDataType(r, c, DxfDataType.Double, DxfUnitType.Distance);
                    break;
            }
        }

        private static string Run(string version, int seed, int edits, bool reopen)
        {
            DxfDatabase db = DxfSpeed12Scenarios.Metric(version);
            var table = new DxfTable(new GeoPoint3(0, 0, 0), 5, 4, 8.0, 30.0);
            db.ModelSpace.AppendEntity(table);
            long[] state = { seed * 2654435761L + 12345 };

            for (int i = 0; i < edits; i++)
            {
                // Read anew, the record is not one this table wrote: each edit reads the whole of it, as it did before the cells
                // were read from their own bytes.
                if (reopen)
                {
                    db = DxfDatabase.Parse(db.ToText());
                    table = db.ModelSpace.OfType<DxfTable>().Single();
                }

                try
                {
                    Edit(table, state, i);
                }
                catch (InvalidOperationException) { }
                catch (ArgumentException) { }
            }

            return db.ToText();
        }

        [Theory]
        [InlineData("AC1032", 7)]
        [InlineData("AC1032", 18)]
        [InlineData("AC1032", 25)]
        [InlineData("AC1027", 7)]
        [InlineData("AC1027", 18)]
        [InlineData("AC1021", 7)]
        [InlineData("AC1021", 25)]
        [InlineData("AC1032", 3)]
        [InlineData("AC1018", 8)]
        public void RunsOfEdits_WithResetsLinesAndTheLastCell_MakeTheTextOfTheSameEditsOnARecordReadAnewEachTime(
            string version,
            int seed
        )
        {
            Assert.Equal(Run(version, seed, 60, true), Run(version, seed, 60, false));
        }

        #endregion

        #region Resets and lines of every cell

        [Fact]
        public void AResetOfEveryCellOf931Cells_AllocatesLessThan300Megabytes()
        {
            // Each reset read and wrote every group of the record: 6.2 GB for these 931 cells, 8 to 10 s.
            DxfDatabase db = DxfSpeed12Scenarios.Metric();
            DxfTable table = Filled(db, 49, 19);
            long before = GC.GetAllocatedBytesForCurrentThread();

            for (int r = 0; r < 49; r++)
            {
                for (int c = 0; c < 19; c++)
                {
                    table.ResetValue(r, c);
                }
            }

            long used = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.Equal(string.Empty, table.TextString(0, 0));
            Assert.Equal(string.Empty, table.TextString(30, 7));
            Assert.Equal(string.Empty, table.TextString(48, 18));
            Assert.True(used < 300_000_000L, "allocated " + used);
        }

        [Fact]
        public void TheLinesOfEveryCellOf931Cells_SetOneByOne_AllocateLessThan150Megabytes()
        {
            // Each call copied the record, its groups and the places of its groups: 672 MB for these 931 cells.
            DxfDatabase db = DxfSpeed12Scenarios.Metric();
            DxfTable table = Filled(db, 49, 19);
            long before = GC.GetAllocatedBytesForCurrentThread();

            for (int r = 0; r < 49; r++)
            {
                for (int c = 0; c < 19; c++)
                {
                    table.SetGridColor(
                        r,
                        c,
                        DxfGridLineType.HorizontalBottom | DxfGridLineType.VerticalRight,
                        Aci(1 + ((r + c) % 7))
                    );
                }
            }

            long used = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.Equal(1 + (30 + 7) % 7, table.GetGridColor(30, 7, DxfGridLineType.HorizontalBottom).ColorIndex);
            Assert.Equal(1 + (10 + 3) % 7, table.GetGridColor(10, 3, DxfGridLineType.VerticalRight).ColorIndex);
            Assert.True(used < 150_000_000L, "allocated " + used);
        }

        #endregion

        #region The first save after an edit

        [Fact]
        public void TheFirstSaveAfterAnEdit_AllocatesAsMuchAsTheSecond_ForADrawingOf60000Lines()
        {
            // The tables written were looked up with TryGetObject, which indexed every entity of the drawing by its handle:
            // 7 MB more than any later save for these 60 000 lines (12 MB and 55 ms for 100 000).
            DxfDatabase made = DxfSpeed12Scenarios.Metric();

            for (int i = 0; i < 60000; i++)
            {
                made.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(i, 0, 0), new GeoPoint3(i, 10 + (i % 7), 0)));
            }

            Filled(made, 6, 4);
            DxfDatabase db = DxfDatabase.Parse(Encoding.UTF8.GetString(DxfSpeed12Scenarios.Saved(made)));
            DxfTable table = db.ModelSpace.OfType<DxfTable>().Single();

            table.SetTextString(1, 1, "first");
            long start = GC.GetAllocatedBytesForCurrentThread();
            byte[] first = DxfSpeed12Scenarios.Saved(db);
            long firstUsed = GC.GetAllocatedBytesForCurrentThread() - start;

            table.SetTextString(1, 1, "second");
            start = GC.GetAllocatedBytesForCurrentThread();
            byte[] second = DxfSpeed12Scenarios.Saved(db);
            long secondUsed = GC.GetAllocatedBytesForCurrentThread() - start;

            Assert.Contains("first", Encoding.UTF8.GetString(first));
            Assert.Contains("second", Encoding.UTF8.GetString(second));
            Assert.True(firstUsed < secondUsed + 2_000_000L, "first " + firstUsed + ", second " + secondUsed);
        }

        #endregion
    }
}
