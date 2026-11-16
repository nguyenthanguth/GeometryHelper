using System;
using System.IO;
using GeometryHelper.Dwg;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The plot style of a layer (<see cref="DxfLayerTableRecord.PlotStyleName"/>, the entry of the drawing's plot style names
    /// its group 390 points at), and the five columns of AutoCAD's Layer Properties Manager that were not in the library's own
    /// layer record before: Plot (290), Transparency and Description (extended data) and Plot Style (390) kept by a DXF and by a
    /// DWG of every version that has them.
    /// </summary>
    public class DxfLayerPlotStyleTests
    {
        /// <summary>A drawing of named plot styles (<c>$PSTYLEMODE</c> 0) whose plot style names are Normal and Style 1.</summary>
        private static DxfDatabase Named(string version = "AC1027")
        {
            var database = new DxfDatabase(version);
            database.SetHeader("$PSTYLEMODE", new DxfTypedValue(290, "0"));
            string text = database.ToText().Replace("\r\n", "\n");
            string dictionary = database.PlotStyleNameDictionary.Handle;
            string normal = "  3\nNormal\n350\n" + database.PlotStyleNameDictionary.GetAt("Normal").Handle + "\n";
            Assert.Contains(normal, text);
            text = text.Replace(normal, normal + "  3\nStyle 1\n350\nFF01\n");
            int end = text.LastIndexOf("  0\nENDSEC\n", StringComparison.Ordinal);
            text = text.Insert(
                end,
                "  0\nACDBPLACEHOLDER\n  5\nFF01\n102\n{ACAD_REACTORS\n330\n"
                    + dictionary
                    + "\n102\n}\n330\n"
                    + dictionary
                    + "\n"
            );
            return DxfDatabase.Parse(text);
        }

        [Fact]
        public void ALayersPlotStyleName_IsTheEntryItsGroup390PointsAt_AndIsSetByNameInADrawingOfNamedPlotStyles()
        {
            DxfDatabase database = Named();
            database.LayerTable.Add(new DxfLayerTableRecord { Name = "steel" });
            DxfLayerTableRecord steel = database.LayerTable["steel"];

            // A layer made in code takes layer 0's plot style, Normal.
            Assert.Equal("Normal", steel.PlotStyleName);

            steel.PlotStyleName = "style 1";
            Assert.Equal("Style 1", steel.PlotStyleName);
            Assert.Equal("Style 1", DxfDatabase.Parse(database.ToText()).LayerTable["steel"].PlotStyleName);
            Assert.Equal("Normal", database.LayerTable["0"].PlotStyleName);

            steel.PlotStyleName = "Normal";
            Assert.Equal("Normal", steel.PlotStyleName);
        }

        [Fact]
        public void APlotStyleName_IsRefusedInADrawingPlottedByColour_ForANameTheDrawingHasNot_AndForNull()
        {
            var byColour = new DxfDatabase("AC1027");
            Assert.True(byColour.PlotStyleMode);
            byColour.LayerTable.Add(new DxfLayerTableRecord { Name = "steel" });
            Assert.Throws<InvalidOperationException>(() => byColour.LayerTable["steel"].PlotStyleName = "Normal");

            DxfDatabase named = Named();
            named.LayerTable.Add(new DxfLayerTableRecord { Name = "steel" });
            Assert.Throws<ArgumentException>(() => named.LayerTable["steel"].PlotStyleName = "Style 9");
            Assert.Throws<ArgumentNullException>(() => named.LayerTable["steel"].PlotStyleName = null);
            Assert.Equal("Normal", named.LayerTable["steel"].PlotStyleName);
        }

        [Theory]
        [InlineData("AC1018")]
        [InlineData("AC1021")]
        [InlineData("AC1024")]
        [InlineData("AC1027")]
        [InlineData("AC1032")]
        public void TheFiveColumnsOfALayer_SurviveADxfAndADwgOfTheVersion(string version)
        {
            DxfDatabase database = Named(version);
            database.LayerTable.Add(new DxfLayerTableRecord { Name = "steel", ColorIndex = 1 });
            DxfLayerTableRecord steel = database.LayerTable["steel"];
            steel.IsPlottable = false;
            steel.Transparency = new DxfTransparency(127);
            steel.Description = "Columns of grid A";
            steel.PlotStyleName = "Style 1";

            AssertColumns(DxfDatabase.Parse(database.ToText()).LayerTable["steel"]);

            using (var stream = new MemoryStream())
            {
                DwgWriter.Write(database, stream, version);
                stream.Position = 0;
                AssertColumns(DwgReader.Open(stream).LayerTable["steel"]);
            }
        }

        private static void AssertColumns(DxfLayerTableRecord steel)
        {
            Assert.False(steel.IsPlottable);
            Assert.Equal(new DxfTransparency(127), steel.Transparency);
            Assert.Equal("Columns of grid A", steel.Description);
            Assert.Equal("Style 1", steel.PlotStyleName);
        }

        [Fact]
        public void TheFiveColumnsOfALayer_SurviveADwgOfR2000_WhereTransparencyIsNotKept()
        {
            DxfDatabase database = Named("AC1015");
            database.LayerTable.Add(new DxfLayerTableRecord { Name = "steel" });
            DxfLayerTableRecord steel = database.LayerTable["steel"];
            steel.IsPlottable = false;
            steel.Description = "Columns of grid A";
            steel.PlotStyleName = "Style 1";
            Assert.Throws<InvalidOperationException>(() => steel.Transparency = new DxfTransparency(127));

            using (var stream = new MemoryStream())
            {
                DwgWriter.Write(database, stream, "AC1015");
                stream.Position = 0;
                DxfLayerTableRecord again = DwgReader.Open(stream).LayerTable["steel"];
                Assert.False(again.IsPlottable);
                Assert.Equal("Columns of grid A", again.Description);
                Assert.Equal("Style 1", again.PlotStyleName);
            }
        }
    }
}
