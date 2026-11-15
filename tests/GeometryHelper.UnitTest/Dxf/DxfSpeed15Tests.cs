using System;
using System.Linq;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The work of tables6: a text typed into a cell (<c>SetValueFromText</c>), the fit, scale and turn of a block cell, the
    /// value it gives an attribute and a formula written into the place of the cell's own bytes, as the other setters of a cell
    /// are, where each call read and wrote every group of the record. The speed is held by the bytes allocated, which do not
    /// vary with the machine as a time does.
    /// </summary>
    public class DxfSpeed15Tests
    {
        // The SHA-256 (24 hex) of the file each run of 120 edits saves, as the library gave it before the work of tables6.
        private static readonly string[] RunsBefore =
        {
            "AC1032 seed 1 5A2F29407425CEBB8D908EDF",
            "AC1032 seed 2 682C3F07EBA8DCE260477737",
            "AC1032 seed 3 C8E27E10525BA027F9E61DA8",
            "AC1032 seed 4 2237827926BB0293878646C7",
            "AC1032 seed 5 9C0539062DB2E7BB3665C59D",
            "AC1032 seed 6 57ED77DF09D71D47C653C158",
            "AC1027 seed 1 D041AB7D2CCD06257406712B",
            "AC1027 seed 2 F10A9D2DB206D72B59E00768",
            "AC1027 seed 3 8835D74D1D8554D4D7AF2C5B",
            "AC1027 seed 4 79AEC4BB23A0B4538B009D78",
            "AC1027 seed 5 7FF52EB9D9C1D8742DA60F19",
            "AC1027 seed 6 D4F9125050C9338B9E42ACC1",
            "AC1021 seed 1 8C4F7575ADCC9ECB99D4B844",
            "AC1021 seed 2 7F379C77555E171B5697DBE8",
            "AC1021 seed 3 8D4B29EF225AE712CF2F92C5",
            "AC1021 seed 4 08263885470AF3E028E5F776",
            "AC1021 seed 5 392D3E4882562C0E5B775CEE",
            "AC1021 seed 6 8190A01CB8931ED9EEB7709B",
            "AC1018 seed 1 8DC6ADCD029CB1A09E0CE8A8",
            "AC1018 seed 2 3607720D6504257BF1DCD1DB",
            "AC1018 seed 3 79E73AFA4EEC2C372D786A44",
            "AC1018 seed 4 1E4FC79D0BEBB77B8C8A5BBE",
            "AC1018 seed 5 A83EA2CC400781CFC2A4F485",
            "AC1018 seed 6 B0E7152DA415CF018991D21B",
        };

        [Fact]
        public void RunsOfEdits_WithATypedTextAFormulaAndABlocksLooks_SaveToTheBytesTheyDidBefore()
        {
            Assert.Equal(RunsBefore, DxfSpeed15Scenarios.Runs());
        }

        [Theory]
        [InlineData("AC1032", 3)]
        [InlineData("AC1032", 11)]
        [InlineData("AC1032", 24)]
        [InlineData("AC1032", 37)]
        [InlineData("AC1027", 6)]
        [InlineData("AC1027", 19)]
        [InlineData("AC1021", 8)]
        [InlineData("AC1021", 29)]
        [InlineData("AC1018", 12)]
        [InlineData("AC1018", 33)]
        public void RunsOfEdits_WithATypedTextAFormulaAndABlocksLooks_MakeTheTextOfTheSameEditsOnARecordReadAnewEachTime(
            string version,
            int seed
        )
        {
            Assert.Equal(
                DxfSpeed15Scenarios.Run(version, seed, 60, true),
                DxfSpeed15Scenarios.Run(version, seed, 60, false)
            );
        }

        private const int Rows = 49;
        private const int Columns = 19;

        // The edit is timed by the bytes it allocates; what it is read back as is checked after it, as reading the table puts
        // in the record the cells kept apart from it, which the edit does not.
        private static void Measure(
            Action<DxfTable, DxfAttributeDefinition> edit,
            Action<DxfTable, DxfAttributeDefinition> check,
            long limit
        )
        {
            DxfDatabase db = DxfSpeed15Scenarios.WithBlockCells("AC1032", Rows, Columns);
            DxfTable table = db.ModelSpace.OfType<DxfTable>().Single();
            DxfAttributeDefinition size = db.BlockTable["BOLT"].OfType<DxfAttributeDefinition>().Single();
            table.SetTextHeight(0, 1, 2.0);
            long before = GC.GetAllocatedBytesForCurrentThread();
            edit(table, size);
            long used = GC.GetAllocatedBytesForCurrentThread() - before;
            check(table, size);
            Assert.True(used < limit, "allocated " + used);
        }

        private static void Each(bool block, Action<int, int> act)
        {
            for (int r = 0; r < Rows; r++)
            {
                for (int c = 0; c < Columns; c++)
                {
                    if (((r + c) % 2 == 0) == block)
                    {
                        act(r, c);
                    }
                }
            }
        }

        [Fact]
        public void ATextTypedIntoEveryTextCellOf931Cells_AllocatesLessThan40Megabytes()
        {
            // Each call read and wrote every group of the record: 3.7 GB for these 466 cells (9 MB now).
            Measure(
                (table, size) => Each(false, (r, c) => table.SetValueFromText(r, c, "12")),
                (table, size) => Assert.Equal("12", table.TextString(30, 7)),
                40_000_000L
            );
        }

        [Fact]
        public void TheFitOfEveryBlockCellOf931Cells_SetOneByOne_AllocatesLessThan40Megabytes()
        {
            // 3.8 GB for these 465 cells.
            Measure(
                (table, size) => Each(true, (r, c) => table.SetAutoScale(r, c, true)),
                (table, size) => Assert.True(table.IsAutoScale(30, 8)),
                40_000_000L
            );
        }

        [Fact]
        public void TheScaleAndTheTurnOfEveryBlockCellOf931Cells_AllocateLessThan40Megabytes()
        {
            // 4.8 GB for the scales of these 465 cells, as much for the turns.
            Measure(
                (table, size) =>
                {
                    Each(true, (r, c) => table.SetBlockScale(r, c, 2.0));
                    Each(true, (r, c) => table.SetBlockRotation(r, c, 0.5));
                },
                (table, size) =>
                {
                    Assert.Equal(2.0, table.BlockScale(30, 8));
                    Assert.Equal(0.5, table.BlockRotation(30, 8));
                },
                40_000_000L
            );
        }

        [Fact]
        public void TheAttributeValueOfEveryBlockCellOf931Cells_AllocatesLessThan40Megabytes()
        {
            // 3.8 GB for these 465 cells.
            Measure(
                (table, size) => Each(true, (r, c) => table.SetBlockAttributeValue(r, c, size, "M" + r)),
                (table, size) => Assert.Equal("M30", table.GetBlockAttributeValue(30, 8, size)),
                40_000_000L
            );
        }

        [Fact]
        public void AFormulaInEveryTextCellOf931Cells_AllocatesLessThan250Megabytes()
        {
            // 6.6 GB for these 466 cells (86 MB now); the fields they point at are written one by one into the drawing, too.
            Measure(
                (table, size) => Each(false, (r, c) => table.SetFormula(r, c, 0, "=A1+B" + r)),
                (table, size) =>
                {
                    Assert.Equal("=A1+B30", table.GetFormula(30, 7, 0));
                    Assert.Equal("=A1+B2", table.GetFormula(2, 1, 0));
                },
                250_000_000L
            );
        }

        [Fact]
        public void TheSettersOfABlockCell_BetweenReadsOfTheTable_AreReadBackAsSet_AndNoCellIsLost()
        {
            // The cells set and kept apart from the record, a read of the table between, then more cells: each is read back as
            // set, and the file saved and read again holds them all.
            DxfDatabase db = DxfSpeed15Scenarios.WithBlockCells("AC1032", 12, 5);
            DxfTable table = db.ModelSpace.OfType<DxfTable>().Single();
            DxfAttributeDefinition size = db.BlockTable["BOLT"].OfType<DxfAttributeDefinition>().Single();

            table.SetBlockScale(0, 0, 2.0);
            table.SetBlockRotation(2, 0, 1.0);
            Assert.Equal(2.0, table.BlockScale(0, 0));
            table.SetAutoScale(4, 0, true);
            table.SetBlockAttributeValue(6, 0, size, "M99");
            Assert.Equal("M99", table.GetBlockAttributeValue(6, 0, size));
            table.SetValueFromText(1, 0, "1.5");
            table.SetFormula(3, 0, 0, "=A1+B1");
            table.SetBlockScale(10, 0, 3.0);
            table.SetValueFromText(11, 4, "7");

            Assert.Equal(1.0, table.BlockRotation(2, 0));
            Assert.Equal(3.0, table.BlockScale(10, 0));
            Assert.True(table.IsAutoScale(4, 0));
            Assert.Equal("=A1+B1", table.GetFormula(3, 0, 0));
            Assert.Equal("7", table.TextString(11, 4));

            DxfTable again = DxfDatabase.Parse(db.ToText()).ModelSpace.OfType<DxfTable>().Single();
            Assert.Equal(2.0, again.BlockScale(0, 0));
            Assert.Equal(1.0, again.BlockRotation(2, 0));
            Assert.True(again.IsAutoScale(4, 0));
            Assert.Equal("=A1+B1", again.GetFormula(3, 0, 0));
            Assert.Equal(3.0, again.BlockScale(10, 0));
            Assert.Equal("7", again.TextString(11, 4));
            Assert.Equal(table.TextString(1, 0), again.TextString(1, 0));
            Assert.Equal(
                "M99",
                again.GetBlockAttributeValue(6, 0, db.BlockTable["BOLT"].OfType<DxfAttributeDefinition>().Single())
            );
        }
    }
}
