using System.Linq;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// A new drawing (NEW, Ctrl+N) gets the layers Global settings list under Layers, colours and lines, each with its colour,
    /// linetype, lineweight and plot flag; a drawing opened from a file is left as it is.
    /// </summary>
    public class NewDrawingLayersTests
    {
        private static readonly string[] DefaultNames =
        {
            "0",
            "Visible",
            "Hidden",
            "Centre",
            "Hatch",
            "Dimension",
            "Mark",
            "Symbol",
            "Sheet",
            "Viewport",
        };

        [Fact]
        public void ANewDrawing_HasTheDefaultLayersOfGlobalSettings_WithTheirLooks()
        {
            var model = new MainViewModel();

            model.Execute("NEW");

            DxfDatabase database = model.Document.Database;
            Assert.Equal(DefaultNames, database.LayerTable.Select(l => l.Name));
            Assert.Equal("0", database.Clayer);

            DxfLayerTableRecord hidden = database.LayerTable["Hidden"];
            Assert.Equal(0x9AA4B1, hidden.TrueColor);
            Assert.Equal("HIDDEN", hidden.LinetypeName);
            Assert.Equal(DxfLineWeight.LineWeight018, hidden.LineWeight);
            Assert.True(hidden.IsPlottable);
            Assert.True(database.LinetypeTable.Has("HIDDEN"));

            DxfLayerTableRecord centre = database.LayerTable["Centre"];
            Assert.Equal(0xF85149, centre.TrueColor);
            Assert.Equal("CENTER", centre.LinetypeName);
            Assert.True(database.LinetypeTable.Has("CENTER"));

            DxfLayerTableRecord visible = database.LayerTable["Visible"];
            Assert.Equal("Continuous", visible.LinetypeName);
            Assert.Equal(DxfLineWeight.LineWeight035, visible.LineWeight);

            DxfLayerTableRecord viewport = database.LayerTable["Viewport"];
            Assert.False(viewport.IsPlottable);
            Assert.Equal(DxfLineWeight.LineWeight013, viewport.LineWeight);
        }

        [Fact]
        public void EveryNewDrawing_GetsThem_AndTheLayersSurviveSavingAndReading()
        {
            using (var folder = new TestFolder())
            {
                var model = new MainViewModel();
                model.NewDrawing();
                CadDocument second = model.NewDrawing();

                Assert.Equal(DefaultNames, second.Database.LayerTable.Select(l => l.Name));

                string path = System.IO.Path.Combine(folder.FullPath, "new.dxf");
                Assert.True(model.SaveAs(path));
                DxfDatabase read = DxfDatabase.Open(path);

                Assert.Equal(DefaultNames, read.LayerTable.Select(l => l.Name));
                Assert.Equal("HIDDEN", read.LayerTable["Hidden"].LinetypeName);
                Assert.Equal(0xF85149, read.LayerTable["Centre"].TrueColor);
            }
        }

        [Fact]
        public void TheLayersSurviveSavingAsADwg()
        {
            using (var folder = new TestFolder())
            {
                var model = new MainViewModel();
                model.NewDrawing();
                string path = System.IO.Path.Combine(folder.FullPath, "new.dwg");
                Assert.True(model.SaveAs(path));

                DxfDatabase read = GeometryHelper.Dwg.DwgReader.Open(path);

                Assert.Equal(DefaultNames, read.LayerTable.Select(l => l.Name));
                Assert.Equal("CENTER", read.LayerTable["Centre"].LinetypeName);
                Assert.Equal(0xF85149, read.LayerTable["Centre"].TrueColor);
                Assert.Equal(DxfLineWeight.LineWeight018, read.LayerTable["Centre"].LineWeight);
                Assert.False(read.LayerTable["Viewport"].IsPlottable);
            }
        }

        [Fact]
        public void TheLayersOfTheSettingsInUse_AreWhatANewDrawingGets_AnUnknownLinetypeBeingContinuous()
        {
            var settings = new GlobalSettings
            {
                Layers = new[]
                {
                    new GlobalLayer("Steel", 0x112233, "DASHED", 0.5),
                    new GlobalLayer("Notes", 0x445566, "NoSuchLinetype", 0.24, plot: false),
                },
            };
            var model = new MainViewModel();
            model.ApplySettings(settings);

            model.NewDrawing();

            DxfDatabase database = model.Document.Database;
            Assert.Equal(new[] { "0", "Steel", "Notes" }, database.LayerTable.Select(l => l.Name));
            Assert.Equal("DASHED", database.LayerTable["Steel"].LinetypeName);
            Assert.Equal(DxfLineWeight.LineWeight050, database.LayerTable["Steel"].LineWeight);
            Assert.Equal(0x112233, database.LayerTable["Steel"].TrueColor);
            Assert.Equal("Continuous", database.LayerTable["Notes"].LinetypeName);
            Assert.Equal(DxfLineWeight.LineWeight025, database.LayerTable["Notes"].LineWeight);
            Assert.False(database.LayerTable["Notes"].IsPlottable);
            Assert.False(database.LinetypeTable.Has("NoSuchLinetype"));
        }

        [Fact]
        public void ATableWithNoLayers_GivesALayer0Drawing_AndALayerNamed0IsLeftAsTheTemplateMakesIt()
        {
            var settings = new GlobalSettings();
            settings.Layers = new GlobalLayer[0];
            var model = new MainViewModel();
            model.ApplySettings(settings);

            model.NewDrawing();

            Assert.Equal(new[] { "0" }, model.Document.Database.LayerTable.Select(l => l.Name));

            settings.Layers = new[] { new GlobalLayer("0", 0x112233, "Continuous", 0.5) };
            model.ApplySettings(settings);
            model.NewDrawing();

            DxfLayerTableRecord zero = model.Document.Database.LayerTable["0"];
            Assert.Single(model.Document.Database.LayerTable);
            Assert.NotEqual(0x112233, zero.TrueColor);
        }

        [Fact]
        public void ADrawingOpenedFromAFile_IsNotGivenTheLayers()
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
