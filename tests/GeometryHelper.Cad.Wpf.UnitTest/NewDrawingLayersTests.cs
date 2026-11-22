using System.Linq;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.Cad.Wpf.UnitTest
{
    /// <summary>
    /// A blank drawing (NEW with no template, Ctrl+N, QNEW with no default template) holds layer 0 and no other: the layers
    /// of a drawing come from its template, no longer from a list in Global settings. A drawing opened from a file is left
    /// as it is.
    /// </summary>
    public class NewDrawingLayersTests
    {
        [Fact]
        public void ABlankDrawing_HasLayer0Only_InTheMetricTemplate()
        {
            var model = new MainViewModel();

            model.Execute("NEW");

            DxfDatabase database = model.Document.Database;
            Assert.Equal(new[] { "0" }, database.LayerTable.Select(l => l.Name));
            Assert.Equal("0", database.Clayer);
            Assert.Equal(DxfMeasurementValue.Metric, database.Measurement);
            Assert.Equal(CadTemplate.DimensionStyle, database.Dimstyle);
        }

        [Fact]
        public void EveryBlankDrawing_HasLayer0Only_MetricOrImperial_AndItSurvivesSavingAsDxfAndDwg()
        {
            using (var folder = new TestFolder())
            {
                var model = new MainViewModel();
                model.NewDrawing();
                CadDocument second = model.NewDrawing();
                CadDocument imperial = model.NewDrawingWithNoTemplate(false);

                Assert.Equal(new[] { "0" }, second.Database.LayerTable.Select(l => l.Name));
                Assert.Equal(new[] { "0" }, imperial.Database.LayerTable.Select(l => l.Name));

                string dxf = System.IO.Path.Combine(folder.FullPath, "new.dxf");
                Assert.True(model.SaveAs(dxf));
                Assert.Equal(new[] { "0" }, DxfDatabase.Open(dxf).LayerTable.Select(l => l.Name));

                string dwg = System.IO.Path.Combine(folder.FullPath, "new.dwg");
                Assert.True(model.SaveAs(dwg));
                Assert.Equal(new[] { "0" }, GeometryHelper.Dwg.DwgReader.Open(dwg).LayerTable.Select(l => l.Name));
            }
        }

        [Fact]
        public void ASettingsFileThatStillListsLayers_DoesNotGiveABlankDrawingAny()
        {
            using (var folder = new TestFolder())
            {
                string path = folder.WriteText(
                    "settings.json",
                    "{ \"layers\": [ { \"name\": \"Steel\", \"colour\": \"#112233\", \"linetype\": \"DASHED\", \"lineweight\": 0.5 } ] }"
                );
                var model = new MainViewModel();
                model.ApplySettings(GlobalSettings.Load(path));

                model.NewDrawing();

                Assert.Equal(new[] { "0" }, model.Document.Database.LayerTable.Select(l => l.Name));
            }
        }

        [Fact]
        public void GlobalSettings_HasNoLayersPage()
        {
            GlobalSettingsViewModel dialog = new MainViewModel().CreateGlobalSettings();

            Assert.DoesNotContain(dialog.Categories, c => c.Name.Contains("Layers"));
        }

        [Fact]
        public void ADrawingOpenedFromAFile_IsLeftAsItIs()
        {
            using (var folder = new TestFolder())
            {
                var model = new MainViewModel();

                Assert.True(model.Open(folder.Write("room.dxf", TestFiles.Room(out _, out _))));

                Assert.DoesNotContain(model.Document.Database.LayerTable, l => l.Name == "Hidden");
            }
        }
    }
}
