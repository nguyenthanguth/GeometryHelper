using System;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The setters of a cell that changed the cell alone only for its text and looks (<c>SetTextString</c>, <c>SetValue</c>,
    /// the alignment, text height, colour and style) now write the bytes of their cell too: <c>SetFormat</c>,
    /// <c>SetDataType</c>, <c>SetBackgroundColorNone</c>, <c>SetOverrides</c> and the last cell of a table, whose end is where
    /// the table's own groups begin. Each wrote the whole record, every group of it, on every call.
    /// </summary>
    public class DxfTableCellSetterSpliceTests
    {
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

        /// <summary>
        /// One cell given a number, its format, whether it is filled, the looks it holds of its own and its kind, by the
        /// setters that were slow, <paramref name="before"/> run before each.
        /// </summary>
        private static void Settings(DxfTable table, int row, int column, int seed, Action before)
        {
            before();
            table.SetValue(row, column, 1.25 + seed);
            before();
            table.SetFormat(row, column, seed % 2 == 0 ? "%lu2%pr2" : "%lu2%pr1");
            before();
            table.SetBackgroundColorNone(row, column, seed % 3 == 0);
            before();
            table.SetOverrides(
                row,
                column,
                0,
                seed % 2 == 0
                    ? DxfCellProperties.Alignment | DxfCellProperties.ContentColor
                    : DxfCellProperties.TextHeight
            );
            before();
            table.SetDataType(row, column, seed % 5 == 0 ? DxfDataType.Long : DxfDataType.Double, DxfUnitType.Unitless);
        }

        [Fact]
        public void TheSettersOfEveryCellOfATableOf600Cells_AreWrittenInTimeLinearInItsBytes()
        {
            // 60 rows by 10 columns, 3 000 calls: each read and wrote the 30 000 groups of the record; now each copies the
            // bytes of the cell it changes. The ceiling is generous: a call to the whole record took some 8 ms.
            DxfDatabase db = null;
            SpeedGuard.AssertWithin(
                6,
                () =>
                {
                    db = Drawing(60, 10);

                    return TableOf(db);
                },
                table =>
                {
                    for (int r = 0; r < 60; r++)
                    {
                        for (int c = 0; c < 10; c++)
                        {
                            Settings(table, r, c, 10 * r + c, () => { });
                        }
                    }
                }
            );

            Assert.Equal(db.ToText(), DxfDatabase.Parse(db.ToText()).ToText());
        }

        [Fact]
        public void TheLastCellOfATable_SetOneCallAfterAnother_IsWrittenByItsOwnBytes()
        {
            // The last cell's end was not known to the bytes of its record, so that each of 1 000 calls on it of a table of
            // 100 rows read and wrote every group.
            SpeedGuard.AssertWithin(
                4,
                () => TableOf(Drawing(100, 10)),
                table =>
                {
                    for (int i = 0; i < 1000; i++)
                    {
                        table.SetBackgroundColorNone(99, 9, i % 2 == 0);
                    }
                }
            );
        }

        [Fact]
        public void TheSettersOfACell_WriteWhatTheyWroteWhenEveryGroupOfTheRecordWasWrittenEachTime()
        {
            // The same settings made twice. In one the width of a column is changed before each of them, which writes the
            // whole record (every group as the file writes it); in the other nothing is, and the cells' own bytes alone are
            // written. The drawings are the same, byte for byte, after each cell, the last of the table among those set.
            DxfDatabase whole = Drawing(5, 4);
            DxfDatabase cells = Drawing(5, 4);
            DxfTable wholeTable = TableOf(whole);
            DxfTable cellsTable = TableOf(cells);
            var random = new Random(11);
            bool wide = false;

            for (int step = 0; step < 150; step++)
            {
                int row = step % 7 == 0 ? 4 : random.Next(5);
                int column = step % 7 == 0 ? 3 : random.Next(4);
                int seed = random.Next(1000);

                if (step == 40 || step == 90)
                {
                    var block = step == 40 ? new DxfCellRange(1, 0, 2, 1) : new DxfCellRange(2, 2, 3, 3);
                    wholeTable.MergeCells(block);
                    cellsTable.MergeCells(block);
                }

                Settings(wholeTable, row, column, seed, () =>
                {
                    wide = !wide;
                    wholeTable.SetColumnWidth(0, wide ? 31.0 : 30.0);
                });
                wholeTable.SetColumnWidth(0, 30.0);
                wide = false;
                Settings(cellsTable, row, column, seed, () => { });
                cellsTable.SetColumnWidth(0, 30.0);
                Assert.Equal(whole.ToText(), cells.ToText());
            }
        }
    }
}
