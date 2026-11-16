using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GeometryHelper.Dwg;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// What the Table Cell tab sets of a single cell beyond its text: its state (Cell Locking), a block put into it, and a
    /// field or formula it shows with the words it came to. Each is written into a table of AutoCAD's own drawing
    /// (<see cref="DxfDimensionDrawings.R2018"/>: 3 rows of 2 columns, its title merged, a block cell of BOLT at (2, 1)) and read
    /// back from the DXF it saves as and from a DWG of R2018.
    /// </summary>
    public class DxfTableCellToolsTests
    {
        private static DxfTable Table(DxfDatabase db) => DxfAutoCadChecksD16Tests.Table(db);

        /// <summary>
        /// AutoCAD's table drawing with the overrides of its cell (1, 0) taken away: the sample writes them after the cell's value,
        /// where AutoCAD 2021 writes them in the cell's head, and the DWG writer reads the order AutoCAD 2021 writes.
        /// </summary>
        private static DxfDatabase Fresh()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfDimensionDrawings.R2018);
            Table(db).RemoveAllOverrides(1, 0);
            return db;
        }

        /// <summary>The drawing's table style Standard, made where the drawing has none.</summary>
        private static DxfTableStyle StyleOf(DxfDatabase db)
        {
            if (!db.TableStyleDictionary.Contains("Standard"))
            {
                db.TableStyleDictionary.SetAt("Standard", new DxfTableStyle());
            }

            return (DxfTableStyle)db.TableStyleDictionary["Standard"];
        }

        [Fact]
        public void ABlockCellAutoCadWrote_IsWrittenAsDwgWithItsBlock()
        {
            DxfDatabase db = Fresh();

            DxfTable back = Table(ThroughDwg(db));

            Assert.Equal(DxfTableCellType.BlockCell, back.CellType(2, 1));
            Assert.Equal("BOLT", back.GetBlockTableRecord(2, 1).Name);
            Assert.Equal("B-12", back.TextString(2, 0));
        }

        private static DxfDatabase ThroughDxf(DxfDatabase db) => DxfDatabase.Parse(db.ToText());

        private static DxfDatabase ThroughDwg(DxfDatabase db)
        {
            byte[] dwg = DwgWriter.ToDwg(db.ToText(), "AC1032", out IDictionary<string, int> _);
            return DxfDatabase.Open(new MemoryStream(DwgReader.ToDxf(dwg), false));
        }

        [Fact]
        public void ATableNeverLocked_HoldsEveryCellUnlocked()
        {
            DxfTable table = Table(Fresh());

            for (int r = 0; r < table.NumRows; r++)
            {
                for (int c = 0; c < table.NumColumns; c++)
                {
                    Assert.Equal(DxfCellState.None, table.GetCellState(r, c));
                }
            }
        }

        [Fact]
        public void ACellLockedInContentAndFormat_IsHeldByItsCellInTheTableContent_ThroughDxfAndDwg()
        {
            DxfDatabase db = Fresh();
            DxfTable table = Table(db);

            table.SetCellState(1, 0, DxfCellState.ContentLocked | DxfCellState.FormatLocked);
            table.SetCellState(1, 1, DxfCellState.FormatLocked);

            Assert.Equal((DxfCellState)17, table.GetCellState(1, 0));
            Assert.Equal(DxfCellState.FormatLocked, table.GetCellState(1, 1));
            Assert.Equal(DxfCellState.None, table.GetCellState(2, 0));

            foreach (DxfDatabase back in new[] { ThroughDxf(db), ThroughDwg(db) }) // dxf then dwg
            {
                DxfTable again = Table(back);
                Assert.Equal((DxfCellState)17, again.GetCellState(1, 0));
                Assert.Equal(DxfCellState.FormatLocked, again.GetCellState(1, 1));
                Assert.Equal(DxfCellState.None, again.GetCellState(2, 0));
                Assert.Equal("Mark", again.TextString(1, 0));
            }
        }

        [Fact]
        public void TheFirstCellOfARangeLockedInAWay_IsFoundInOneReading_AndATableOfNoStateHasNone()
        {
            DxfDatabase db = Fresh();
            DxfTable table = Table(db);
            var all = new DxfCellRange(0, 0, 2, 1);
            Assert.False(
                table.TryFindCellState(
                    all,
                    DxfCellState.ContentLocked | DxfCellState.FormatLocked,
                    out int row,
                    out int column
                )
            );
            Assert.Equal(-1, row);
            Assert.Equal(-1, column);

            table.SetCellState(1, 1, DxfCellState.ContentLocked);
            table.SetCellState(2, 0, DxfCellState.FormatLocked);

            Assert.True(table.TryFindCellState(all, DxfCellState.ContentLocked, out row, out column));
            Assert.Equal((1, 1), (row, column));
            Assert.True(table.TryFindCellState(all, DxfCellState.FormatLocked, out row, out column));
            Assert.Equal((2, 0), (row, column));
            Assert.True(
                table.TryFindCellState(all, DxfCellState.ContentLocked | DxfCellState.FormatLocked, out row, out column)
            );
            Assert.Equal((1, 1), (row, column));
            Assert.False(
                table.TryFindCellState(
                    new DxfCellRange(0, 0, 1, 0),
                    DxfCellState.ContentLocked | DxfCellState.FormatLocked,
                    out _,
                    out _
                )
            );
            Assert.Throws<System.ArgumentOutOfRangeException>(() =>
                table.TryFindCellState(new DxfCellRange(0, 0, 3, 1), DxfCellState.ContentLocked, out _, out _)
            );
        }

        [Fact]
        public void ACellUnlocked_HoldsNoFlag_AndTheLinkedFlagOfALinkedCellIsNeverSetOrClearedByTheState()
        {
            DxfDatabase db = Fresh();
            DxfTable table = Table(db);
            table.SetCellState(1, 0, DxfCellState.ContentLocked);
            table.SetCellState(1, 0, DxfCellState.None);

            Assert.Equal(DxfCellState.None, table.GetCellState(1, 0));

            // Asking for the flag of a linked cell does not link the cell.
            table.SetCellState(1, 0, DxfCellState.Linked | DxfCellState.ContentLocked);
            Assert.Equal(DxfCellState.ContentLocked, table.GetCellState(1, 0));
        }

        [Fact]
        public void ACellOfAnotherTable_OutOfRange_IsRefused()
        {
            DxfTable table = Table(Fresh());

            Assert.Throws<System.ArgumentOutOfRangeException>(() => table.GetCellState(3, 0));
            Assert.Throws<System.ArgumentOutOfRangeException>(() =>
                table.SetCellState(0, 2, DxfCellState.ContentLocked)
            );
        }

        [Fact]
        public void ABlockPutIntoATextCell_MakesItABlockCell_ThroughDxfAndDwg()
        {
            DxfDatabase db = Fresh();
            DxfTable table = Table(db);
            DxfBlockTableRecord bolt = db.BlockTable["BOLT"];

            table.SetBlockTableRecord(1, 1, bolt, true);

            Assert.Equal(DxfTableCellType.BlockCell, table.CellType(1, 1));
            Assert.Same(bolt, table.GetBlockTableRecord(1, 1));
            Assert.True(table.IsAutoScale(1, 1));
            Assert.Equal(1.0, table.BlockScale(1, 1), 6);
            Assert.Equal(string.Empty, table.TextString(1, 1));

            DxfTable dxf = Table(ThroughDxf(db));
            Assert.True(dxf.CellType(1, 1) == DxfTableCellType.BlockCell, "dxf");
            Assert.Equal("BOLT", dxf.GetBlockTableRecord(1, 1).Name);
            Assert.Equal(DxfTableCellType.TextCell, dxf.CellType(1, 0));

            DxfTable dwg = Table(ThroughDwg(db));
            Assert.True(dwg.CellType(1, 1) == DxfTableCellType.BlockCell, "dwg");
            Assert.Equal("BOLT", dwg.GetBlockTableRecord(1, 1).Name);
            Assert.Equal(DxfTableCellType.TextCell, dwg.CellType(1, 0));
        }

        [Fact]
        public void ABlockCellGivenAnotherBlock_KeepsItsScale_AndTakesTheNewBlock()
        {
            DxfDatabase db = Fresh();
            DxfTable table = Table(db);
            table.SetBlockTableRecord(1, 1, db.BlockTable["BOLT"], false);
            table.SetBlockScale(1, 1, 2.5);

            table.SetBlockTableRecord(1, 1, db.BlockTable["BOLT"], true);

            Assert.Equal(2.5, table.BlockScale(1, 1), 6);
            Assert.True(table.IsAutoScale(1, 1));
            Assert.Equal(
                DxfTableCellType.BlockCell,
                ThroughDxf(db).ModelSpace.OfType<DxfTable>().First().CellType(1, 1)
            );
        }

        [Fact]
        public void AFieldGivenToACell_IsKeptWithTheWordsItComesTo_ThroughDxf()
        {
            DxfDatabase db = Fresh();
            DxfTable table = Table(db);

            table.SetFieldCode(1, 1, 0, "%<\\AcVar Login>%");
            table.SetFieldValue(1, 1, "inuyasha");

            Assert.Equal("\\AcVar Login", table.GetFieldCode(1, 1, 0));
            Assert.Equal("inuyasha", table.TextString(1, 1));
            Assert.Contains(new DxfCellRange(1, 1, 1, 1), table.GetFieldCells());

            DxfTable again = Table(ThroughDxf(db));
            Assert.Equal("\\AcVar Login", again.GetFieldCode(1, 1, 0));
            Assert.Equal("inuyasha", again.TextString(1, 1));
        }

        [Fact]
        public void AFormulaCellGivenItsResult_KeepsItsFormula_AndShowsTheNumber()
        {
            DxfDatabase db = Fresh();
            DxfTable table = Table(db);
            table.SetFormula(2, 0, 0, "=1+2");

            table.SetFieldValue(2, 0, 3.0);

            Assert.Equal("=1+2", table.GetFormula(2, 0, 0));
            Assert.Equal(3.0, (double)table.GetValue(2, 0, 0), 6);
            DxfTable again = Table(ThroughDxf(db));
            Assert.Equal("=1+2", again.GetFormula(2, 0, 0));
            Assert.Equal(3.0, (double)again.GetValue(2, 0, 0), 6);
        }

        [Fact]
        public void AValueGivenToACellWithNoField_IsRefused()
        {
            DxfTable table = Table(Fresh());

            Assert.Throws<System.InvalidOperationException>(() => table.SetFieldValue(1, 1, "x"));
            Assert.Throws<System.ArgumentException>(() => table.SetFieldCode(1, 1, 0, "\\AcVar Login"));
        }

        [Fact]
        public void ACellStyleOfAUser_IsMadeFromAnotherOfTheStyle_RenamedAndTakenOut_AndKeptThroughDxf()
        {
            DxfDatabase db = Fresh();
            var style = StyleOf(db);
            Assert.Equal(new[] { "Title", "Header", "Data" }, style.GetCellStyleNames());

            int id = style.AddCellStyle("Heavy", "Header");

            Assert.Equal(101, id);
            Assert.Equal(new[] { "Title", "Header", "Data", "Heavy" }, style.GetCellStyleNames());
            Assert.Equal(101, style.GetCellStyleId("heavy"));
            Assert.Equal("Heavy", style.GetCellStyleName(101));
            DxfCellStyleLook header = style.GetCellStyleLook("Header");
            DxfCellStyleLook heavy = style.GetCellStyleLook("Heavy");
            Assert.Equal(header.Alignment, heavy.Alignment);
            Assert.Equal(header.TextHeight, heavy.TextHeight, 6);
            Assert.Equal(header.LineWeights[0], heavy.LineWeights[0]);
            Assert.Equal(header.BackgroundColor?.ColorIndex, heavy.BackgroundColor?.ColorIndex);
            Assert.Equal(102, style.AddCellStyle("Light", "Heavy"));

            var again = StyleOf(ThroughDxf(db));
            Assert.Equal(new[] { "Title", "Header", "Data", "Heavy", "Light" }, again.GetCellStyleNames());

            style.RenameCellStyle("Heavy", "Bold");
            style.RemoveCellStyle("Light");
            Assert.Equal(new[] { "Title", "Header", "Data", "Bold" }, style.GetCellStyleNames());
            Assert.Equal(new[] { "Title", "Header", "Data", "Bold" }, (StyleOf(ThroughDxf(db))).GetCellStyleNames());

            Assert.Throws<System.ArgumentException>(() => style.AddCellStyle("bold", "Data"));
            Assert.Throws<System.ArgumentException>(() => style.AddCellStyle("New", "Nothing"));
            Assert.Throws<System.ArgumentException>(() => style.RemoveCellStyle("Data"));
            Assert.Throws<System.ArgumentException>(() => style.RenameCellStyle("Title", "Top"));
        }

        [Fact]
        public void TheCellStyleACellTakes_IsItsIdInTheTableContent_ThroughDxfAndDwg()
        {
            DxfDatabase db = Fresh();
            DxfTable table = Table(db);
            Assert.Equal(0, table.GetCellStyleId(2, 0));

            table.SetCellStyleId(2, 0, 2);
            table.SetCellStyleId(1, 1, 101);

            Assert.Equal(2, table.GetCellStyleId(2, 0));
            Assert.Equal(101, table.GetCellStyleId(1, 1));
            Assert.Equal(0, table.GetCellStyleId(1, 0));

            Assert.Equal(2, Table(ThroughDxf(db)).GetCellStyleId(2, 0));
            Assert.Equal(101, Table(ThroughDxf(db)).GetCellStyleId(1, 1));
            Assert.Equal(2, Table(ThroughDwg(db)).GetCellStyleId(2, 0));

            table.SetCellStyleId(2, 0, 0);
            Assert.Equal(0, table.GetCellStyleId(2, 0));
        }
    }
}
