using System;
using System.Linq;
using System.Threading;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// C9 in the window: the Linetype Manager (LINETYPE) and its Load dialog, the ribbon's linetype box ($CELTYPE), the Select
    /// Linetype dialog of the Layer manager, and the Hatch dialog of HATCH's seTtings and HATCHEDIT, each driven with no window.
    /// </summary>
    public class LinetypeHatchWindowTests
    {
        private static MainViewModel Room(TestFolder folder)
        {
            var model = new MainViewModel();
            Assert.True(model.Open(folder.Write("room.dxf", TestFiles.Room(out _, out _))));
            model.Document.SupportFolders = Array.Empty<string>();
            return model;
        }

        private static void OnSta(Action test)
        {
            Exception failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    test();
                }
                catch (Exception ex)
                {
                    failure = ex;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            Assert.Null(failure);
        }

        [Fact]
        public void LinetypeShowsTheManager_WhichLoadsMakesCurrentAndAppliesAsOneUndoStep()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Room(folder);
                model.ShowLinetypeManager = manager =>
                {
                    Assert.Equal(new[] { "ByLayer", "ByBlock", "Continuous" }, manager.Rows.Select(r => r.Name).ToArray());
                    Assert.Equal("Current Linetype: ByLayer", manager.CurrentText);

                    var load = new LoadLinetypesViewModel(manager.Document) { File = CadLinetypeFile.BuiltInName };
                    Assert.Equal(39, load.Rows.Count);
                    load.Rows.First(r => r.Name == "DASHED").IsChosen = true;
                    load.Rows.First(r => r.Name == "CENTER").IsChosen = true;
                    manager.PickLinetypes = dialog => true;
                    manager.Add(load.Chosen, load.FilePath);
                    Assert.True(manager.Rows.Single(r => r.Name == "DASHED").IsPending);
                    Assert.Equal("M0,5 L32,5 M40,5 L72,5 M80,5 L112,5", manager.Rows.Single(r => r.Name == "DASHED").Appearance);

                    manager.SelectedRow = manager.Rows.Single(r => r.Name == "CENTER");
                    manager.MakeCurrent();
                    manager.GlobalScale = "10";
                    manager.IsoPenWidth = "0.5 mm";
                    Assert.Equal("0.5", manager.CurrentObjectScale);
                    return true;
                };

                model.Execute("LT");

                DxfDatabase db = model.Document.Database;
                Assert.True(db.LinetypeTable.Has("DASHED"));
                Assert.Equal("CENTER", db.Celtype);
                Assert.Equal(10.0, db.Ltscale);
                Assert.Equal(0.5, db.Celtscale);
                Assert.Equal("LINETYPE", model.Document.NextUndo.Name);
                Assert.Equal("CENTER", model.CurrentLinetype);
                Assert.Contains("DASHED", model.CurrentLinetypeOptions);

                model.Document.Undo();
                Assert.False(model.Document.Database.LinetypeTable.Has("DASHED"));
                Assert.Equal("ByLayer", model.Document.Database.Celtype);
            }
        }

        [Fact]
        public void TheManagerRefusesToDeleteALinetypeInUse_TheCurrentOneAndContinuous_AndDeletesOneUnused()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Room(folder);
                CadLinetypes.Load(model.Document, CadLinetypeFile.BuiltIn.Where(l => l.Name == "HIDDEN" || l.Name == "DOT"), CadLinetypeFile.BuiltInName);
                CadLayerEditor.SetLinetype(model.Document, "COLUMNS", "HIDDEN");
                LinetypeManagerViewModel manager = model.CreateLinetypeManager();

                manager.SelectedRow = manager.Rows.Single(r => r.Name == "HIDDEN");
                Assert.False(manager.Delete());
                Assert.Equal("The linetype HIDDEN is in use and cannot be deleted.", manager.Message);

                manager.SelectedRow = manager.Rows.Single(r => r.Name == "Continuous");
                Assert.False(manager.Delete());
                Assert.StartsWith("The linetype Continuous cannot be deleted", manager.Message);

                manager.SelectedRow = manager.Rows.Single(r => r.Name == "DOT");
                manager.MakeCurrent();
                Assert.False(manager.Delete());
                Assert.Equal("The linetype DOT is current and cannot be deleted.", manager.Message);

                manager.SelectedRow = manager.Rows.Single(r => r.Name == "ByLayer");
                manager.MakeCurrent();
                manager.SelectedRow = manager.Rows.Single(r => r.Name == "DOT");
                Assert.True(manager.Delete());
                Assert.True(manager.Apply().Succeeded);
                Assert.False(model.Document.Database.LinetypeTable.Has("DOT"));
                Assert.True(model.Document.Database.LinetypeTable.Has("HIDDEN"));
            }
        }

        [Fact]
        public void TheLinetypeBox_SetsTheLinetypeNewObjectsTake_AndOtherShowsTheManager()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Room(folder);
                CadLinetypes.Load(model.Document, CadLinetypeFile.BuiltIn.Where(l => l.Name == "PHANTOM"), CadLinetypeFile.BuiltInName);
                Assert.Equal(new[] { "ByLayer", "ByBlock", "Continuous", "PHANTOM", MainViewModel.OtherLinetypeOption }, model.CurrentLinetypeOptions.ToArray());

                model.CurrentLinetype = "PHANTOM";
                Assert.Equal("PHANTOM", model.Document.Database.Celtype);
                model.Execute("LINE");
                model.Execute("0,500");
                model.Execute("100,500");
                model.Execute(string.Empty);
                DxfLine line = model.Document.Database.ModelSpace.OfType<DxfLine>().Last();
                Assert.Equal("PHANTOM", line.Linetype);

                bool shown = false;
                model.ShowLinetypeManager = manager => shown = true;
                model.CurrentLinetype = MainViewModel.OtherLinetypeOption;
                Assert.True(shown);
                Assert.Equal("PHANTOM", model.CurrentLinetype);
            }
        }

        [Fact]
        public void SelectLinetype_LoadsIntoTheDrawing_AndGivesTheLayerTheOneChosen()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Room(folder);
                LayerManagerViewModel layers = model.CreateLayerManager();
                layers.PickLinetype = dialog =>
                {
                    Assert.Equal(new[] { "Continuous" }, dialog.Rows.Select(r => r.Name).ToArray());
                    dialog.PickLinetypes = load =>
                    {
                        load.File = CadLinetypeFile.BuiltInName;
                        load.Rows.First(r => r.Name == "BORDER").IsChosen = true;
                        return true;
                    };
                    dialog.Load();
                    Assert.Equal("BORDER", dialog.Linetype);
                    return true;
                };

                layers.Rows.Single(r => r.Name == "WALLS").ChooseLinetypeCommand.Execute(null);

                Assert.Equal("BORDER", model.Document.Database.LayerTable["WALLS"].LinetypeName);
                Assert.Equal("BORDER", layers.Rows.Single(r => r.Name == "WALLS").Linetype);
                layers.Dispose();
            }
        }

        [Fact]
        public void TheHatchDialog_SetsTheNextHatch_DrawsItsPreview_AndEditsAHatchAsOneUndoStep()
        {
            OnSta(() =>
            {
                using (var folder = new TestFolder())
                {
                    MainViewModel model = Room(folder);
                    HatchSettingsViewModel shownFirst = null;
                    model.ShowHatchSettings = dialog =>
                    {
                        shownFirst = dialog;
                        Assert.Equal("Hatch Creation", dialog.Title);
                        Assert.NotNull(dialog.Preview);
                        Assert.Contains(dialog.Patterns, p => p.Name == "HONEY");
                        dialog.Type = "User defined";
                        dialog.Spacing = "100";
                        dialog.Double = true;
                        dialog.Angle = "30";
                        dialog.IslandStyle = "Outer";
                        return true;
                    };

                    model.Execute("H");
                    model.Execute("T");
                    Assert.NotNull(shownFirst);
                    Assert.Equal(CadHatchType.UserDefined, model.Document.HatchSettings.Type);

                    // Inside the column: a circle of radius 100 about (2 000, 1 500).
                    model.Execute("2000,1500");
                    model.Execute(string.Empty);
                    DxfHatch hatch = model.Document.Database.ModelSpace.OfType<DxfHatch>().Single();
                    Assert.Equal(DxfHatchPatternType.UserDefined, hatch.PatternType);
                    Assert.Equal(100.0, hatch.PatternSpace);
                    Assert.True(hatch.PatternDouble);
                    Assert.Equal(DxfHatchStyle.Outer, hatch.HatchStyle);
                    Assert.Equal(Math.PI * 10000.0, hatch.Area, 6);

                    model.ShowHatchSettings = dialog =>
                    {
                        Assert.Equal("Hatch Editor", dialog.Title);
                        Assert.Equal("User defined", dialog.Type);
                        dialog.Type = "Pattern";
                        dialog.Pattern = dialog.Patterns.First(p => p.Name == "BRICK");
                        dialog.Scale = "2";
                        return true;
                    };

                    model.Execute("HE");
                    model.Session.FeedSelection(new[] { hatch.Handle });
                    DxfHatch edited = model.Document.Database.ModelSpace.OfType<DxfHatch>().Single();
                    Assert.Equal("BRICK", edited.PatternName);
                    Assert.Equal(2.0, edited.PatternScale);
                    Assert.Equal(2, edited.NumberOfPatternDefinitions);
                    Assert.Equal("HATCHEDIT", model.Document.NextUndo.Name);

                    model.Document.Undo();
                    Assert.Equal(DxfHatchPatternType.UserDefined, model.Document.Database.ModelSpace.OfType<DxfHatch>().Single().PatternType);

                    // Cancel changes nothing.
                    model.ShowHatchSettings = dialog => false;
                    model.Execute("HE");
                    model.Session.FeedSelection(new[] { hatch.Handle });
                    Assert.Equal("HATCH", model.Document.NextUndo.Name);
                }
            });
        }
    }
}
