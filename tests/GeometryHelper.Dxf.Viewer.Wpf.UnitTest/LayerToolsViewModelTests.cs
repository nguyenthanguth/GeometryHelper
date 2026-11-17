using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// LAYWALK's LayerWalk and LAYTRANS's Layer Translator driven with no window through their view models, the dialogs shown
    /// by the functions the workspace sets: the layers walked while the dialog is open and put back when it closes, the filter
    /// box, Purge Layers, Select Objects, and the translator's Load, Map, Edit, Save and Translate.
    /// </summary>
    public class LayerToolsViewModelTests
    {
        /// <summary>A drawing of the layers WALL, DOOR and TRIM, a line on WALL and one on DOOR, saved as plan.dxf.</summary>
        private static MainViewModel Plan(TestFolder folder, out CadDocument document)
        {
            DxfDatabase database = CadTemplate.CreateMetric();

            foreach (string name in new[] { "WALL", "DOOR", "TRIM" })
            {
                database.LayerTable.Add(new DxfLayerTableRecord { Name = name, ColorIndex = 1 });
            }

            database.ModelSpace.AppendEntity(
                new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0)) { Layer = "WALL" }
            );
            database.ModelSpace.AppendEntity(
                new DxfLine(new GeoPoint3(0, 10, 0), new GeoPoint3(100, 10, 0)) { Layer = "DOOR" }
            );
            var model = new MainViewModel();
            Assert.True(model.Open(folder.Write("plan.dxf", database)));
            document = model.Document;
            return model;
        }

        private static string[] Off(CadDocument document) =>
            document.Database.LayerTable.Where(l => l.IsOff).Select(l => l.Name).OrderBy(n => n).ToArray();

        [Fact]
        public void LayWalk_ShowsTheDialog_TheLayersSelectedAloneShown_AndEveryLayerBackWhenItCloses()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Plan(folder, out CadDocument document);
                string[] whileOpen = null;
                LayerWalkViewModel dialog = null;
                model.ShowLayerWalk = shown =>
                {
                    dialog = shown;
                    Assert.Equal(new[] { "0", "DOOR", "TRIM", "WALL" }, shown.Rows.Select(r => r.Name));
                    shown.Select(new[] { "WALL" });
                    whileOpen = Off(document);
                    Assert.Equal("4 layers, 1 selected", shown.Summary);
                    return true;
                };

                model.Execute("LAYWALK");

                Assert.Equal(new[] { "0", "DOOR", "TRIM" }, whileOpen);
                Assert.Empty(Off(document));
                Assert.True(dialog.RestoreOnExit);
            }
        }

        [Fact]
        public void TheFilterNarrowsTheList_AndAClearedRestoreOnExitKeepsTheWalk()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Plan(folder, out CadDocument document);
                model.ShowLayerWalk = shown =>
                {
                    shown.Filter = "D*,T*";
                    Assert.Equal(new[] { "DOOR", "TRIM" }, shown.Rows.Select(r => r.Name));
                    Assert.Equal("2 layers, 0 selected", shown.Summary);
                    shown.Select(new[] { "DOOR" });
                    shown.RestoreOnExit = false;
                    return true;
                };

                model.Execute("LAYWALK");

                Assert.Equal(new[] { "0", "TRIM", "WALL" }, Off(document));
            }
        }

        [Fact]
        public void PurgeLayers_DeletesTheUnusedOnesSelected_SaysHowMany_AndTheListLosesThem()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Plan(folder, out CadDocument document);
                var said = new List<string>();
                model.ShowLayerWalk = shown =>
                {
                    Assert.Equal(1, shown.Purge(new[] { "TRIM", "WALL" }));
                    said.Add(shown.Message);
                    Assert.Equal(new[] { "0", "DOOR", "WALL" }, shown.Rows.Select(r => r.Name));
                    Assert.Equal(0, shown.Purge(new[] { "WALL" }));
                    said.Add(shown.Message);
                    return true;
                };

                model.Execute("LAYWALK");

                Assert.Equal(new[] { "1 layer purged.", "No unused layer is selected." }, said);
                Assert.False(document.Database.LayerTable.Has("TRIM"));
            }
        }

        [Fact]
        public void SelectObjects_ClosesTheDialogForThePickOnTheCanvas_AndItComesBackWithTheLayersOfTheObjectsSelected()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Plan(folder, out CadDocument document);
                var selected = new List<string[]>();
                int shown = 0;
                model.ShowLayerWalk = dialog =>
                {
                    selected.Add(dialog.Selected.ToArray());

                    if (shown++ == 0)
                    {
                        dialog.SelectObjects();
                    }
                    else
                    {
                        Assert.Equal(new[] { "0", "TRIM", "WALL" }, Off(document));
                        dialog.Close();
                    }

                    return true;
                };

                model.Execute("LAYWALK");
                Assert.True(model.Session.IsRunning);
                string door = document.Database.ModelSpace.OfType<DxfLine>().First(l => l.Layer == "DOOR").Handle;
                model.Session.FeedSelection(new[] { door });
                model.Session.Enter();

                Assert.False(model.Session.IsRunning);
                Assert.Equal(2, selected.Count);
                Assert.Empty(selected[0]);
                Assert.Equal(new[] { "DOOR" }, selected[1]);
                Assert.Empty(Off(document));
            }
        }

        /// <summary>A standards drawing of the layers WALL (colour 3) and DOOR (colour 4), saved as office.dxf.</summary>
        private static string Standards(TestFolder folder)
        {
            DxfDatabase database = CadTemplate.CreateMetric();
            database.LayerTable.Add(new DxfLayerTableRecord { Name = "WALL", ColorIndex = 3 });
            database.LayerTable.Add(new DxfLayerTableRecord { Name = "DOOR", ColorIndex = 4 });
            return folder.Write("office.dxf", database);
        }

        [Fact]
        public void LayTrans_LoadsAStandardsFile_MapsLayers_AndTranslatesInOneUndoStep()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Plan(folder, out CadDocument document);
                CadDocument start = document;
                string standards = Standards(folder);
                string text = document.Database.ToText();
                LayerTranslatorViewModel dialog = null;
                model.ShowLayerTranslator = shown =>
                {
                    dialog = shown;
                    shown.AskLoadFile = () => standards;
                    Assert.True(shown.Load());
                    Assert.Equal(2, shown.Map(new[] { "TRIM", "DOOR" }, "WALL"));
                    Assert.True(shown.CanTranslate);
                    Assert.True(shown.Translate());
                    return true;
                };

                model.Execute("LAYTRANS");

                Assert.NotNull(dialog);
                Assert.False(document.Database.LayerTable.Has("DOOR"));
                Assert.False(document.Database.LayerTable.Has("TRIM"));
                Assert.All(document.Database.ModelSpace.OfType<DxfLine>(), l => Assert.Equal("WALL", l.Layer));
                Assert.Equal(3, document.Database.LayerTable["WALL"].Color.ColorIndex);
                Assert.True(document.Undo());
                Assert.Equal(text, document.Database.ToText());
                Assert.Same(start, model.Document);
            }
        }

        [Fact]
        public void LayTrans_Cancel_ChangesNothing_AndTranslateWithNothingMappedIsRefused()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Plan(folder, out CadDocument document);
                string text = document.Database.ToText();
                model.ShowLayerTranslator = shown =>
                {
                    Assert.False(shown.Translate());
                    Assert.Equal("No layer is mapped.", shown.Message);
                    shown.MapSame();
                    shown.FromSelected(new[] { "WALL" });
                    shown.Cancel();
                    return true;
                };

                model.Execute("LAYTRANS");

                Assert.Equal(text, document.Database.ToText());
            }
        }

        [Fact]
        public void TheNewLayerDialog_ChecksTheName_AndEditChangesWhatAMappingGives_BeforeSaveWritesTheMappings()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Plan(folder, out CadDocument document);
                string saved = Path.Combine(folder.FullPath, "mine.dws");
                model.ShowLayerTranslator = shown =>
                {
                    var typed = new Queue<string>(new[] { "A*B", "doors", "DOORS" });
                    shown.ShowLayerDialog = dialog =>
                    {
                        if (dialog.IsNew)
                        {
                            dialog.Name = typed.Dequeue();
                            dialog.Transparency = "30";
                            return true;
                        }

                        Assert.Equal("doors", dialog.Name);
                        Assert.True(dialog.IsNameFixed);
                        Assert.Equal("30", dialog.Transparency);
                        dialog.Transparency = "95";
                        Assert.Null(dialog.Commit());
                        Assert.Equal("Transparency is a whole number from 0 to 90.", dialog.Error);
                        dialog.Transparency = "60";
                        dialog.SelectedWeight = dialog.LineWeights.First(w => w.Weight == DxfLineWeight.LineWeight035);
                        return true;
                    };

                    Assert.False(shown.NewLayer());
                    Assert.Equal("\"A*B\" is an invalid layer name.", shown.Message);
                    Assert.True(shown.NewLayer());
                    Assert.False(shown.NewLayer() && shown.NewLayer());
                    Assert.Contains(shown.ToRows, r => r.Name == "doors");
                    Assert.Equal(1, shown.Map(new[] { "DOOR" }, "doors"));
                    Assert.Equal(1, shown.Edit(new[] { "DOOR" }));
                    Assert.Equal("0.35 mm", shown.MappingRows.Single().LineWeight);
                    Assert.Equal("60", shown.MappingRows.Single().Transparency);

                    shown.AskSaveFile = () => saved;
                    Assert.True(shown.Save());
                    Assert.StartsWith("Saved ", shown.Message);
                    Assert.Equal(1, shown.Remove(new[] { "DOOR" }));
                    Assert.False(shown.CanTranslate);
                    shown.Cancel();
                    return true;
                };

                model.Execute("LAYTRANS");

                Assert.True(CadDrawingFile.IsDwg(saved));
                Assert.True(CadDrawingFile.Open(saved).LayerTable.Has("doors"));
            }
        }

        [Fact]
        public void TheSettingsDialog_WritesBackWhatWasTicked_AndSelectByFilterListsTheLayersThatMatch()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Plan(folder, out _);
                model.ShowLayerTranslator = shown =>
                {
                    shown.ShowSettings = dialog =>
                    {
                        dialog.ForceColorByLayer = false;
                        dialog.TranslateObjectsInBlocks = false;
                        return true;
                    };

                    Assert.True(shown.OpenSettings());
                    Assert.False(shown.Request.Settings.ForceColorByLayer);
                    Assert.False(shown.Request.Settings.TranslateObjectsInBlocks);
                    Assert.True(shown.Request.Settings.ForceLinetypeByLayer);

                    shown.SelectFilter = "T*,D?OR";
                    Assert.Equal(new[] { "DOOR", "TRIM" }, shown.SelectByFilter());
                    shown.SelectFilter = string.Empty;
                    Assert.Equal(4, shown.SelectByFilter().Count);
                    shown.Cancel();
                    return true;
                };

                model.Execute("LAYTRANS");
            }
        }
    }
}
