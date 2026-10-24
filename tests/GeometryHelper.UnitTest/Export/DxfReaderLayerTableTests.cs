using System;
using System.Linq;
using GeometryHelper.Export;
using Xunit;
using static GeometryHelper.UnitTest.Export.DxfLines;

namespace GeometryHelper.UnitTest.Export
{
    /// <summary>
    /// The layer table read into <see cref="DxfDocument.LayerTable"/>: each layer's look and state, a name spelled again in
    /// another case, and <see cref="DxfDocument.GetLayer(string)"/>.
    /// </summary>
    public class DxfReaderLayerTableTests
    {
        private static readonly Tolerance Tol = Tolerance.Default;

        private static string Table(params string[] records)
            => G(0, "SECTION", 2, "TABLES", 0, "TABLE", 2, "LAYER", 70, records.Length) + string.Concat(records) + G(0, "ENDTAB", 0, "ENDSEC");

        private static string Layer(string name, params object[] more)
            => G(0, "LAYER", 100, "AcDbSymbolTableRecord", 100, "AcDbLayerTableRecord", 2, name) + G(more);

        private static DxfDocument Read(string tables, string entities = "") => DxfReader.Parse(File("AC1015", 4, tables, entities), new DxfReadOptions { Tolerance = Tol });

        [Fact]
        public void EachLayerOfTheTable_HasItsColourLinetypeAndLineweight_AndWhetherItIsOffFrozenLockedAndPlotted()
        {
            DxfDocument read = Read(Table(
                Layer("0", 70, 0, 62, 7, 6, "Continuous", 370, -3),
                Layer("steel", 70, 0, 62, 1, 6, "DASHED", 370, 35, 420, 3175104),
                Layer("off", 70, 0, 62, -3),
                Layer("frozen", 70, 1, 62, 5),
                Layer("locked", 70, 4, 62, 2, 290, 1),
                Layer("unplotted", 70, 0, 62, 3, 290, 0),
                Layer("bare")));

            Assert.Equal(new[] { "0", "steel", "off", "frozen", "locked", "unplotted", "bare" }, read.LayerTable.Select(l => l.Name).ToArray());
            Assert.Equal(new int?[] { 7, 1, 3, 5, 2, 3, null }, read.LayerTable.Select(l => l.Color).ToArray());
            Assert.Equal(new[] { false, false, true, false, false, false, false }, read.LayerTable.Select(l => l.IsOff).ToArray());
            Assert.Equal(new[] { false, false, false, true, false, false, false }, read.LayerTable.Select(l => l.IsFrozen).ToArray());
            Assert.Equal(new[] { false, false, false, false, true, false, false }, read.LayerTable.Select(l => l.IsLocked).ToArray());
            Assert.Equal(new[] { true, true, true, true, true, false, true }, read.LayerTable.Select(l => l.IsPlotted).ToArray());

            DxfLayer steel = read.LayerTable[1];
            Assert.Equal(0x3072C0, steel.TrueColor);
            Assert.Equal("DASHED", steel.Linetype);
            Assert.Equal(35, steel.LineWeight);

            // The default lineweight (-3) is none, as on an entity, and a layer that says nothing has nothing.
            Assert.Null(read.LayerTable[0].LineWeight);
            Assert.Equal("Continuous", read.LayerTable[0].Linetype);
            Assert.Null(read.LayerTable[6].Linetype);
            Assert.Null(read.LayerTable[6].TrueColor);
        }

        [Fact]
        public void ANameSpelledAgainInAnotherCase_IsTheFirstLayer_AsLayersNamesIt()
        {
            DxfDocument read = Read(
                Table(Layer("Steel", 70, 0, 62, 1), Layer("STEEL", 70, 1, 62, 5), Layer("Caf\\U+00E9", 70, 0, 62, 2)),
                G(0, "LINE", 8, "steel", 10, "0", 20, "0", 11, "1", 21, "0") + G(0, "LINE", 8, "notes", 10, "0", 20, "0", 11, "1", 21, "0"));

            Assert.Equal(new[] { "Steel", "Café" }, read.LayerTable.Select(l => l.Name).ToArray());
            Assert.Equal(new[] { "Steel", "Café", "notes" }, read.Layers.ToArray());
            Assert.Equal(1, read.LayerTable[0].Color);
            Assert.False(read.LayerTable[0].IsFrozen);
            Assert.Equal("Steel", read.Shapes[0].Layer);
        }

        [Fact]
        public void ALayerIsFoundByItsNameInEitherCase_AndOneTheTableDoesNotListIsNot()
        {
            DxfDocument read = Read(
                Table(Layer("Steel", 70, 0, 62, 1)),
                G(0, "LINE", 8, "notes", 10, "0", 20, "0", 11, "1", 21, "0"));

            Assert.Same(read.LayerTable[0], read.GetLayer("STEEL"));
            Assert.Same(read.LayerTable[0], read.GetLayer("steel"));
            Assert.Null(read.GetLayer("notes"));
            Assert.Null(read.GetLayer(string.Empty));
            Assert.Throws<ArgumentNullException>(() => read.GetLayer(null));
        }

        [Fact]
        public void AColourOrLineweightOutsideItsRange_OrThatCannotBeRead_IsNone()
        {
            DxfDocument read = Read(Table(
                Layer("a", 70, 0, 62, 0),
                Layer("b", 70, 0, 62, 256, 370, 33),
                Layer("c", 70, 0, 62, "red", 420, "3271524352"),
                Layer("d", 70, 0, 62, int.MinValue)));

            Assert.All(read.LayerTable, l => Assert.Null(l.Color));
            Assert.All(read.LayerTable, l => Assert.Null(l.LineWeight));
            Assert.All(read.LayerTable, l => Assert.Null(l.TrueColor));
            Assert.Equal(new[] { false, false, false, true }, read.LayerTable.Select(l => l.IsOff).ToArray());
        }

        [Fact]
        public void AFileWithNoLayerTable_HasAnEmptyOne()
        {
            DxfDocument read = DxfReader.Parse(R2000(G(0, "LINE", 8, "steel", 10, "0", 20, "0", 11, "1", 21, "0")), new DxfReadOptions { Tolerance = Tol });

            Assert.Empty(read.LayerTable);
            Assert.Equal(new[] { "steel" }, read.Layers.ToArray());
            Assert.Null(read.GetLayer("steel"));
        }
    }
}
