using System.IO;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>Commands, Properties edits, layers, undo and saving, driven through the view model as the window drives them.</summary>
    public class EditingTests
    {
        private static MainViewModel Room(TestFolder folder, out string bottom, out string column)
        {
            var model = new MainViewModel();
            Assert.True(model.Open(folder.Write("room.dxf", TestFiles.Room(out bottom, out column))));
            return model;
        }

        private static DxfLine Line(MainViewModel model, string handle) => (DxfLine)model.Document.FindEntity(handle);

        [Fact]
        public void MoveByTwoClicksMovesTheSelectedLineAndUndoPutsItBack()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Room(folder, out string bottom, out _);
                model.Toggles.Single(t => t.Name == "SNAP").IsOn = true;
                model.PickAt(1000, 0, 5, false);

                model.StartCommand("MOVE");

                Assert.StartsWith("Specify base point", model.Prompt);
                Assert.True(model.IsCommandRunning);

                // The base point snaps to the line's end; the second point is on the snap grid of 10.
                Assert.Equal(CadSnapKind.Endpoint, model.SnapAt(new GeoPoint2(2, 1), 5).Kind);
                model.ClickAt(new GeoPoint2(2, 1), 5);
                Assert.Equal(new GeoPoint2(0, 0), model.Session.Prompt.BasePoint.Value);
                model.ClickAt(new GeoPoint2(101, 199), 5);

                Assert.False(model.IsCommandRunning);
                Assert.Equal(100.0, Line(model, bottom).StartPoint.X, 9);
                Assert.Equal(200.0, Line(model, bottom).StartPoint.Y, 9);
                Assert.True(model.HasUnsavedChanges);
                Assert.Equal("room.dxf*", model.Title);
                Assert.Equal("Unsaved changes", model.StatusText);
                Assert.True(model.FileRows.Single(r => r.Label == "room.dxf").HasUnsavedChanges);

                Assert.True(model.Undo());

                Assert.Equal(0.0, Line(model, bottom).StartPoint.X, 9);
                Assert.Equal(0.0, Line(model, bottom).StartPoint.Y, 9);
                Assert.False(model.HasUnsavedChanges);
                Assert.Equal("MOVE", model.History.Last());

                Assert.True(model.Redo());

                Assert.Equal(200.0, Line(model, bottom).StartPoint.Y, 9);
            }
        }

        [Fact]
        public void LineTypedPointByPointDrawsSegmentsUntilEnter()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Room(folder, out _, out _);
                int before = model.Document.Database.ModelSpace.Cast<DxfEntity>().Count();

                model.Execute("L");
                model.Execute("500,500");
                model.Execute("@300,0");
                model.Execute("@0,300");
                model.Execute(string.Empty);

                Assert.False(model.IsCommandRunning);
                Assert.Equal(before + 2, model.Document.Database.ModelSpace.Cast<DxfEntity>().Count());
                Assert.Contains("Command: L", model.History);

                // The two segments are one undo step.
                model.Undo();

                Assert.Equal(before, model.Document.Database.ModelSpace.Cast<DxfEntity>().Count());
            }
        }

        [Fact]
        public void EraseWithNothingSelectedTakesAWindowAtSelectObjectsAndEnterEndsIt()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Room(folder, out string bottom, out _);

                model.StartCommand("ERASE");

                Assert.StartsWith("Select objects", model.Prompt);

                model.SelectBox(new DisplayBox(-10, -10, 4010, 10), false, false);

                Assert.Equal(new[] { bottom }, model.Session.Gathering);

                model.Enter();

                Assert.Null(model.Document.FindEntity(bottom));
                Assert.False(model.IsCommandRunning);
            }
        }

        [Fact]
        public void APropertiesEditMovesTheLineToAnotherLayerAndARefusedOneSaysWhyAndKeepsTheOldValue()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Room(folder, out string bottom, out _);
                model.PickAt(1000, 0, 5, false);
                PropertyRowViewModel layer = model.PropertyGroups.SelectMany(g => g.Rows).Single(r => r.Label == "Layer");

                Assert.True(layer.HasOptions);
                Assert.Contains("COLUMNS", layer.Options);

                layer.Value = "COLUMNS";

                Assert.Equal("COLUMNS", Line(model, bottom).Layer);
                Assert.Equal("COLUMNS", model.PropertyGroups.SelectMany(g => g.Rows).Single(r => r.Label == "Layer").Value);

                PropertyRowViewModel startX = model.PropertyGroups.SelectMany(g => g.Rows).Single(r => r.Label == "Start X");
                string old = startX.Value;
                int lines = model.History.Count;

                startX.Value = "not a number";

                Assert.Equal(old, startX.Value);
                Assert.Equal(lines + 1, model.History.Count);
                Assert.Equal(0.0, Line(model, bottom).StartPoint.X);

                startX.Value = "250";

                Assert.Equal(250.0, Line(model, bottom).StartPoint.X, 9);

                model.Undo();
                model.Undo();

                Assert.Equal("WALLS", Line(model, bottom).Layer);
            }
        }

        [Fact]
        public void OrthoAndOsnapToggledFromTheStatusBarChangeThePointAClickTakes()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Room(folder, out _, out _);
                StatusToggleViewModel ortho = model.Toggles.Single(t => t.Name == "ORTHO");
                StatusToggleViewModel osnap = model.Toggles.Single(t => t.Name == "OSNAP");
                model.Toggles.Single(t => t.Name == "SNAP").IsOn = true;
                model.Execute("LINE");
                model.Execute("1000,1000");

                Assert.Equal(new GeoPoint2(1500, 1240), model.SnapAt(new GeoPoint2(1501, 1238), 5).Point);

                ortho.ToggleCommand.Execute(null);

                Assert.True(model.Snap.Ortho);
                GeoPoint2 held = model.SnapAt(new GeoPoint2(1501, 1238), 5).Point;
                Assert.Equal(1000.0, held.Y, 9);

                // Near the column's centre with OSNAP on the centre is taken; off, the grid point.
                Assert.Equal(CadSnapKind.Center, model.SnapAt(new GeoPoint2(2003, 1502), 5).Kind);

                osnap.IsOn = false;

                Assert.False(model.Snap.IsOn);
                Assert.NotEqual(CadSnapKind.Center, model.SnapAt(new GeoPoint2(2003, 1502), 5).Kind);
            }
        }

        [Fact]
        public void TheLayerBoxAndTheLayerManagerSetTheCurrentLayerAndAFrozenCurrentLayerIsRefused()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Room(folder, out _, out _);

                model.CurrentLayer = model.Layers.Single(l => l.Name == "COLUMNS");

                Assert.Equal("COLUMNS", model.Document.Database.Clayer);
                Assert.Equal("COLUMNS", model.CurrentLayer.Name);

                using (LayerManagerViewModel manager = model.CreateLayerManager())
                {
                    Assert.Equal("Layer manager · room.dxf", manager.Title);
                    Assert.True(manager.Rows.Single(r => r.Name == "COLUMNS").IsCurrent);

                    manager.SelectedRow = manager.Rows.Single(r => r.Name == "WALLS");
                    manager.SetCurrentCommand.Execute(null);

                    Assert.Equal("WALLS", model.CurrentLayer.Name);

                    int lines = model.History.Count;
                    manager.Rows.Single(r => r.Name == "WALLS").IsFrozen = true;

                    Assert.False(manager.Rows.Single(r => r.Name == "WALLS").IsFrozen);
                    Assert.True(model.History.Count > lines);

                    manager.Rows.Single(r => r.Name == "COLUMNS").ColorName = "Blue";
                    Assert.Equal("Blue", manager.Rows.Single(r => r.Name == "COLUMNS").ColorName);

                    Assert.Equal("Layer1", manager.NewLayer());
                    Assert.Contains(model.Layers, l => l.Name == "Layer1");
                    Assert.StartsWith(MainViewModel.Count(model.Layers.Count) + " layers · 0 frozen", manager.Summary);
                }
            }
        }

        [Fact]
        public void MakeCurrentTakesTheLayerOfTheSelectedObjectAndMatchLayerMovesObjectsOntoThatOfOneClicked()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Room(folder, out string bottom, out string column);
                model.PickAt(2100, 1500, 5, false);

                model.MakeCurrentCommand.Execute(null);

                Assert.Equal("COLUMNS", model.CurrentLayer.Name);

                model.PickAt(1000, 0, 5, false);
                model.MatchLayerCommand.Execute(null);

                Assert.StartsWith("Select object on destination layer", model.Prompt);

                model.ClickAt(new GeoPoint2(2100, 1500), 5);

                Assert.Equal("COLUMNS", Line(model, bottom).Layer);
                Assert.False(model.IsCommandRunning);
            }
        }

        [Fact]
        public void SaveAsWritesTheFileAndTheTitleLosesItsStar()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Room(folder, out _, out _);
                model.Execute("LINE 0,0 100,100");
                model.Enter();
                Assert.True(model.HasUnsavedChanges);
                string copy = Path.Combine(folder.FullPath, "copy.dxf");

                Assert.True(model.SaveAs(copy));

                Assert.True(File.Exists(copy));
                Assert.Equal("copy.dxf", model.Title);
                Assert.Equal("Saved", model.StatusText);
                Assert.Contains(model.FileRows, r => r.Label == "copy.dxf" && r.IsCurrent);
                Assert.Equal(4, CadDocument.Open(copy).Database.ModelSpace.Cast<DxfEntity>().Count());
            }
        }

        [Fact]
        public void EscEndsTheCommandFirstAndClearsTheSelectionAfter()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Room(folder, out _, out _);
                model.PickAt(1000, 0, 5, false);
                model.StartCommand("ROTATE");

                model.Escape();

                Assert.False(model.IsCommandRunning);
                Assert.EndsWith("*Cancel*", model.History.Last());
                Assert.Equal(1, model.Document.Selection.Count);

                model.Escape();

                Assert.Equal(0, model.Document.Selection.Count);
            }
        }
    }
}
