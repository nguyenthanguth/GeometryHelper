using System;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// The Layer manager's delete, linetype and weight; the live View group; the entity count of the list; undo through the
    /// command line; the ghost of a transforming command; and the grid's spacing, all without a window.
    /// </summary>
    public class RoundTwoTests
    {
        private static MainViewModel Open(TestFolder folder, out string bottom, out string viewport)
        {
            var model = new MainViewModel();
            Assert.True(model.Open(folder.Write("room.dxf", TestFiles.RoomWithViewport(folder, out bottom, out viewport))));
            return model;
        }

        private static string Row(MainViewModel model, string label) => model.PropertyGroups.SelectMany(g => g.Rows).Single(r => r.Label == label).Value;

        [Fact]
        public void TheLayerManagerDeletesAnUnusedLayerAndRefusesOneInUseSayingWhy()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Open(folder, out _, out _);

                using (LayerManagerViewModel manager = model.CreateLayerManager())
                {
                    string added = manager.NewLayer();
                    Assert.True(manager.DeleteCommand.CanExecute(null));

                    Assert.True(manager.Delete());

                    Assert.DoesNotContain(manager.Rows, r => r.Name == added);
                    Assert.DoesNotContain(model.Layers, l => l.Name == added);

                    manager.SelectedRow = manager.Rows.Single(r => r.Name == "COLUMNS");

                    Assert.False(manager.Delete());
                    Assert.Contains("objects are on it", model.History.Last());
                    Assert.Contains(manager.Rows, r => r.Name == "COLUMNS");

                    // The delete is one undo step.
                    model.Undo();
                    model.Undo();
                    Assert.DoesNotContain(model.Layers, l => l.Name == added);
                }
            }
        }

        [Fact]
        public void TheLayerManagerSetsALinetypeOfTheDrawingAndAWeightAndRefusesOthers()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Open(folder, out _, out _);

                using (LayerManagerViewModel manager = model.CreateLayerManager())
                {
                    LayerRowViewModel columns = manager.Rows.Single(r => r.Name == "COLUMNS");
                    Assert.Contains("GH_DASH", columns.LinetypeOptions);
                    Assert.DoesNotContain("ByLayer", columns.LinetypeOptions);

                    columns.Linetype = "GH_DASH";

                    Assert.Equal("GH_DASH", manager.Rows.Single(r => r.Name == "COLUMNS").Linetype);
                    Assert.Equal("GH_DASH", model.Document.Database.LayerTable["COLUMNS"].LinetypeName);

                    manager.Rows.Single(r => r.Name == "COLUMNS").Linetype = "NO_SUCH";

                    Assert.Contains("no linetype \"NO_SUCH\"", model.History.Last());
                    Assert.Equal("GH_DASH", manager.Rows.Single(r => r.Name == "COLUMNS").Linetype);

                    Assert.Equal("Default", manager.Rows.Single(r => r.Name == "COLUMNS").Weight);
                    manager.Rows.Single(r => r.Name == "COLUMNS").Weight = "0.50";

                    Assert.Equal("0.50", manager.Rows.Single(r => r.Name == "COLUMNS").Weight);
                    Assert.Equal(DxfLineWeight.LineWeight050, model.Document.Database.LayerTable["COLUMNS"].LineWeight);

                    manager.Rows.Single(r => r.Name == "COLUMNS").Weight = "0.33";

                    Assert.StartsWith("Invalid lineweight \"0.33\"", model.History.Last());
                    Assert.Equal("0.50", manager.Rows.Single(r => r.Name == "COLUMNS").Weight);

                    manager.Rows.Single(r => r.Name == "COLUMNS").ColorName = "Mauve";

                    Assert.StartsWith("Invalid colour \"Mauve\"", model.History.Last());
                }
            }
        }

        [Fact]
        public void TheViewGroupOfPropertiesFollowsTheCanvasView()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Open(folder, out _, out _);
                model.SetCanvasSize(1000, 600);
                string before = Row(model, "Center X");

                model.View.Pan(-100, 0);
                model.NotifyViewChangedByUser();

                Assert.NotEqual(before, Row(model, "Center X"));
                double shown = double.Parse(Row(model, "Center X").Replace(" ", string.Empty), System.Globalization.CultureInfo.InvariantCulture);
                Assert.Equal(model.View.CenterX, shown, 3);
            }
        }

        [Fact]
        public void TheFileListCountsTheEntitiesAgainAfterAnEdit()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Open(folder, out _, out _);
                FileRowViewModel row = model.FileRows.Single(r => r.Label == "room.dxf");
                Assert.StartsWith("DXF · 3 entities", row.Detail);

                model.Execute("LINE 0,0 100,100");
                model.Enter();

                Assert.StartsWith("DXF · 4 entities", model.FileRows.Single(r => r.Label == "room.dxf").Detail);
            }
        }

        [Fact]
        public void UTypedAndCtrlZTakeTheSamePathAndRedoBringsTheStepBack()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Open(folder, out string bottom, out _);
                model.PickAt(1000, 0, 5, false);
                model.Execute("MOVE 0,0 0,500");
                Assert.Equal(500.0, ((DxfLine)model.Document.FindEntity(bottom)).StartPoint.Y, 9);

                model.Execute("U");

                Assert.Equal(0.0, ((DxfLine)model.Document.FindEntity(bottom)).StartPoint.Y, 9);
                Assert.Equal("MOVE", model.History.Last());

                Assert.True(model.Redo());
                Assert.Equal(500.0, ((DxfLine)model.Document.FindEntity(bottom)).StartPoint.Y, 9);

                Assert.True(model.Undo());
                Assert.Equal(0.0, ((DxfLine)model.Document.FindEntity(bottom)).StartPoint.Y, 9);
                Assert.Contains("Command: U", model.History);
                Assert.False(model.Undo());
            }
        }

        [Fact]
        public void DimAngularStartsFromItsRibbonButton()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Open(folder, out _, out _);

                model.StartCommandCommand.Execute("DIMANGULAR");

                Assert.True(model.IsCommandRunning);
                Assert.Contains("Command: DIMANGULAR", model.History);
            }
        }

        [Fact]
        public void TheGhostOfMoveRotateScaleAndMirrorFollowsTheCursorFromTheBasePoint()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Open(folder, out string bottom, out _);
                var ghost = new CadGhost();
                model.Session.PromptChanged += (s, e) => ghost.Follow(model.Session);
                model.PickAt(1000, 0, 5, false);

                model.Execute("MOVE");

                Assert.Equal(new[] { bottom }, ghost.Handles);
                Assert.Null(CadGhost.Transform(model.Session, new GeoPoint2(5, 5)));

                model.Execute("0,0");
                Affine2 move = CadGhost.Transform(model.Session, new GeoPoint2(100, 200)).Value;

                Assert.Equal(101.0, move.X(1, 2), 9);
                Assert.Equal(202.0, move.Y(1, 2), 9);
                Assert.NotEmpty(ghost.Paths(model.Document, model.Space, 1.0));

                model.Escape();
                model.Execute("ROTATE 100,0");
                Affine2 turn = CadGhost.Transform(model.Session, new GeoPoint2(100, 50)).Value;

                // A quarter turn about (100, 0): (200, 0) goes to (100, 100).
                Assert.Equal(100.0, turn.X(200, 0), 9);
                Assert.Equal(100.0, turn.Y(200, 0), 9);

                model.Escape();
                model.Execute("SCALE 100,0");
                Affine2 scale = CadGhost.Transform(model.Session, new GeoPoint2(102, 0)).Value;

                Assert.Equal(300.0, scale.X(200, 0), 9);

                model.Escape();
                model.Execute("MIRROR 0,0");
                Affine2 mirror = CadGhost.Transform(model.Session, new GeoPoint2(0, 10)).Value;

                Assert.Equal(-7.0, mirror.X(7, 3), 9);
                Assert.Equal(3.0, mirror.Y(7, 3), 9);
            }
        }

        [Fact]
        public void TheGhostTakesWhatSelectObjectsGatheredWhenNothingWasSelectedBefore()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Open(folder, out string bottom, out _);
                var ghost = new CadGhost();
                model.Session.PromptChanged += (s, e) => ghost.Follow(model.Session);

                model.Execute("COPY");
                model.SelectBox(new DisplayBox(-10, -10, 4010, 10), false, false);
                model.Enter();

                Assert.StartsWith("Specify base point", model.Prompt);
                Assert.Equal(new[] { bottom }, ghost.Handles);

                model.Escape();

                Assert.Empty(ghost.Handles);
            }
        }

        [Fact]
        public void TheGridIsDrawnEveryTenOrEveryFiftyHundredAndOnSoThatItsDotsLieEightPixelsApartAtLeast()
        {
            Assert.Equal(10.0, CadCanvas.GridStep(1.0, 10, 8));
            Assert.Equal(50.0, CadCanvas.GridStep(0.18, 10, 8));
            Assert.Equal(1000.0, CadCanvas.GridStep(0.01, 10, 8));
            Assert.Equal(0.0, CadCanvas.GridStep(1.0, 0, 8));
            Assert.True(CadCanvas.GridStep(1E-9, 10, 8) * 1E-9 >= 8.0);
        }

        [Fact]
        public void TheGridToggleIsGridOn()
        {
            var model = new MainViewModel();
            Assert.False(model.GridOn);

            model.Toggles.Single(t => t.Name == "GRID").IsOn = true;

            Assert.True(model.GridOn);
        }
    }
}
