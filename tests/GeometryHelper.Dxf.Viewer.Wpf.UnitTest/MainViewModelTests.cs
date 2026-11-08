using System.IO;
using System.Linq;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    public class MainViewModelTests
    {
        [Fact]
        public void OpeningAFileFillsItsSpacesItsLayersAndTheListOfItsFolder()
        {
            using (var folder = new TestFolder())
            {
                string room = folder.Write("room.dxf", TestFiles.Room(out _, out _));
                folder.Write("other.dxf", TestFiles.Room(out _, out _));
                var model = new MainViewModel();

                Assert.True(model.Open(room));

                Assert.True(model.HasDocument);
                Assert.Equal(new[] { "Model", "Layout1" }, model.SpaceTabs.Select(t => t.Name));
                Assert.True(model.SpaceTabs[0].IsActive);
                Assert.Contains(model.Layers, l => l.Name == "WALLS");
                Assert.Contains(model.Layers, l => l.Name == "COLUMNS");
                Assert.Equal("WALLS", model.CurrentLayer.Name);

                // The folder's header, then its two files by name, the one open marked and described.
                Assert.Equal(3, model.FileRows.Count);
                Assert.True(model.FileRows[0].IsHeader);
                Assert.EndsWith(" · DXF", model.FileRows[0].Label);
                Assert.Equal(new[] { "other.dxf", "room.dxf" }, model.FileRows.Skip(1).Select(r => r.Label));
                Assert.False(model.FileRows[1].IsCurrent);
                Assert.True(model.FileRows[2].IsCurrent);
                Assert.Equal("DXF · 3 entities · 1 layout", model.FileRows[2].Detail);
                Assert.Equal("All 2", model.AllChipText);

                Assert.Equal("Opened room.dxf: 3 entities, " + model.Layers.Count + " layers.", model.History.Last());
                Assert.Equal("room.dxf", model.Title);
                Assert.Equal("MODEL", model.SpaceLabel);
                Assert.Equal(MainViewModel.ModelViewportLabel, model.ViewportLabel);
                Assert.Equal("Saved", model.StatusText);
                Assert.Equal("No selection", model.SelectionLabel);
            }
        }

        [Fact]
        public void ClickingAFileOfTheListOpensItAndTheSearchKeepsTheFilesWhoseNameHoldsIt()
        {
            using (var folder = new TestFolder())
            {
                string room = folder.Write("room.dxf", TestFiles.Room(out _, out _));
                folder.Write("other.dxf", TestFiles.Room(out _, out _));
                var model = new MainViewModel();
                model.Open(room);

                model.ActivateFileCommand.Execute(model.FileRows.Single(r => r.Label == "other.dxf"));

                Assert.Equal("other.dxf", model.Title);
                Assert.True(model.FileRows.Single(r => r.Label == "other.dxf").IsCurrent);

                model.SearchText = "ROO";

                Assert.Equal(new[] { "room.dxf" }, model.FileRows.Where(r => !r.IsHeader).Select(r => r.Label));

                model.SearchText = "nothing like it";

                Assert.Empty(model.FileRows);
            }
        }

        [Fact]
        public void SelectingALineByAWindowGivesPropertiesTheLabelLineOneAndEscClearsIt()
        {
            using (var folder = new TestFolder())
            {
                var model = new MainViewModel();
                model.Open(folder.Write("room.dxf", TestFiles.Room(out string bottom, out _)));

                // A window round the bottom wall alone, dragged left to right.
                Assert.Equal(new[] { bottom }, model.SelectBox(new DisplayBox(-10, -10, 4010, 10), false, false));

                Assert.Equal("Line (1)", model.SelectionLabel);
                Assert.Contains(model.PropertyGroups, g => g.Title == "General");

                model.Escape();

                Assert.Equal("No selection", model.SelectionLabel);
                Assert.Equal(0, model.Document.Selection.Count);
            }
        }

        [Fact]
        public void AWindowTakesOnlyWhatIsWhollyInsideItAndACrossingWhatItTouches()
        {
            using (var folder = new TestFolder())
            {
                var model = new MainViewModel();
                model.Open(folder.Write("room.dxf", TestFiles.Room(out string bottom, out string column)));
                var box = new DisplayBox(1000, -10, 2050, 1500);

                Assert.Empty(model.SelectBox(box, false, false));
                Assert.Equal(0, model.Document.Selection.Count);

                Assert.Equal(new[] { bottom, column }.OrderBy(h => h, System.StringComparer.Ordinal), model.SelectBox(box, true, false));
                Assert.Equal(2, model.Document.Selection.Count);

                // With Shift the box takes away what it touches.
                model.SelectBox(new DisplayBox(1900, 1390, 2100, 1410), true, true);

                Assert.Equal(new[] { bottom }, model.Document.Selection.Handles);
            }
        }

        [Fact]
        public void AClickAddsShiftClickTakesAwayAndAClickOnNothingClears()
        {
            using (var folder = new TestFolder())
            {
                var model = new MainViewModel();
                model.Open(folder.Write("room.dxf", TestFiles.Room(out string bottom, out string column)));

                Assert.Equal(bottom, model.PickAt(1000, 0.5, 5, false));
                Assert.Equal(column, model.PickAt(2100.5, 1500, 5, false));
                Assert.Equal(2, model.Document.Selection.Count);

                model.PickAt(1000, 0.5, 5, true);

                Assert.Equal(new[] { column }, model.Document.Selection.Handles);

                Assert.Null(model.PickAt(500, 800, 5, false));
                Assert.Equal(0, model.Document.Selection.Count);
            }
        }

        [Fact]
        public void ZoomExtentsWindowAndPreviousComeFromTheCoreCommandAndMoveTheCanvasView()
        {
            using (var folder = new TestFolder())
            {
                var model = new MainViewModel();
                model.Open(folder.Write("room.dxf", TestFiles.Room(out _, out _)));
                model.SetCanvasSize(1000, 600);
                CadView view = model.View;
                Assert.Same(view, model.Session.View);

                // The room, 4 000 by 3 000, fills 90 per cent of the canvas's height: 540 / 3 000.
                Assert.Equal(2000.0, view.CenterX, 6);
                Assert.Equal(1500.0, view.CenterY, 6);
                Assert.Equal(0.18, view.PixelsPerUnit, 9);

                view.Pan(200, 100);
                view.ZoomAt(300, 300, 2.0);
                model.NotifyViewChangedByUser();
                int revision = model.Revision;

                model.Execute("Z");

                Assert.StartsWith("Specify corner of window", model.Prompt);

                model.Execute("E");

                Assert.Equal(string.Empty, model.Prompt);
                Assert.Equal(2000.0, view.CenterX, 6);
                Assert.Equal(0.18, view.PixelsPerUnit, 12);
                Assert.True(model.Revision > revision);

                // A window from (1 000, 500) to (2 000, 1 500) by two clicks fills the canvas's height: 600 / 1 000.
                model.Execute("ZOOM W");
                model.ClickAt(new GeoPoint2(1000, 500), 1.0);
                model.ClickAt(new GeoPoint2(2000, 1500), 1.0);

                Assert.Equal(0.6, view.PixelsPerUnit, 9);
                Assert.Equal(1500.0, view.CenterX, 6);

                model.Execute("ZOOM P");

                Assert.Equal(0.18, view.PixelsPerUnit, 6);
            }
        }

        [Fact]
        public void AViewTheUserHasNotMovedIsRefittedToANewCanvasSizeAndOneHeHasMovedIsNot()
        {
            using (var folder = new TestFolder())
            {
                var model = new MainViewModel();
                model.Open(folder.Write("room.dxf", TestFiles.Room(out _, out _)));
                model.SetCanvasSize(1000, 600);
                double zoom = model.View.PixelsPerUnit;

                model.SetCanvasSize(2000, 1200);

                Assert.Equal(2.0 * zoom, model.View.PixelsPerUnit, 12);

                model.NotifyViewChangedByUser();
                model.SetCanvasSize(1000, 600);

                Assert.Equal(2.0 * zoom, model.View.PixelsPerUnit, 12);
                Assert.Equal(1000.0, model.View.Width);
            }
        }

        [Fact]
        public void AWordThatIsNoCommandIsSaidToBeUnknownAndAnEmptyLineRepeatsTheLastCommand()
        {
            var model = new MainViewModel();

            model.Execute("foo");

            Assert.Equal("No drawing is open: Ctrl+O opens a DXF.", model.History.Last());

            using (var folder = new TestFolder())
            {
                model.Open(folder.Write("room.dxf", TestFiles.Room(out _, out _)));

                model.Execute("foo");

                Assert.Equal("Unknown command \"foo\".  Press F1 for help.", model.History.Last());

                model.Execute("re");
                model.Execute(string.Empty);

                Assert.Equal(2, model.History.Count(l => l == "Regenerating model."));
                Assert.Equal("Regenerating model.", model.RecentHistory.Last());
            }
        }

        [Fact]
        public void UpAndDownRecallTheLinesTypedBefore()
        {
            var model = new MainViewModel();
            model.CommandText = "PAN";
            model.SubmitCommand();
            model.CommandText = "REGEN";
            model.SubmitCommand();

            model.RecallPrevious();
            Assert.Equal("REGEN", model.CommandText);
            model.RecallPrevious();
            Assert.Equal("PAN", model.CommandText);
            model.RecallPrevious();
            Assert.Equal("PAN", model.CommandText);
            model.RecallNext();
            Assert.Equal("REGEN", model.CommandText);
            model.RecallNext();
            Assert.Equal(string.Empty, model.CommandText);
        }

        [Fact]
        public void ShowingALayoutShowsPaperAndClearsTheSelection()
        {
            using (var folder = new TestFolder())
            {
                var model = new MainViewModel();
                model.Open(folder.Write("room.dxf", TestFiles.Room(out string bottom, out _)));
                model.PickAt(1000, 0, 5, false);

                model.SelectSpaceCommand.Execute(model.SpaceTabs[1]);

                Assert.Equal("PAPER", model.SpaceLabel);
                Assert.Equal(string.Empty, model.ViewportLabel);
                Assert.Equal("room.dxf · Layout1", model.Title);
                Assert.Equal(0, model.Document.Selection.Count);
                Assert.True(model.SpaceTabs[1].IsActive);
                Assert.False(model.SpaceTabs[0].IsActive);
            }
        }

        [Fact]
        public void AFileThatCannotBeReadLeavesTheDrawingShownAndSaysWhy()
        {
            using (var folder = new TestFolder())
            {
                var model = new MainViewModel();
                model.Open(folder.Write("room.dxf", TestFiles.Room(out _, out _)));
                string broken = folder.WriteText("broken.dxf", "this is no drawing");

                Assert.False(model.Open(broken));

                Assert.Equal("room.dxf", model.Title);
                Assert.StartsWith("Cannot open broken.dxf: ", model.History.Last());
                Assert.False(model.Open(Path.Combine(folder.FullPath, "missing.dxf")));
            }
        }

        [Fact]
        public void TheStatusTogglesStartAsTheDesignShowsThemAndSwitch()
        {
            var model = new MainViewModel();

            Assert.Equal(new[] { "GRID", "SNAP", "ORTHO", "POLAR", "OSNAP" }, model.Toggles.Select(t => t.Name));
            Assert.Equal(new[] { false, true, false, false, true }, model.Toggles.Select(t => t.IsOn));

            model.Toggles[0].ToggleCommand.Execute(null);

            Assert.True(model.Toggles[0].IsOn);
        }
    }
}
