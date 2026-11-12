using System.Linq;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// The fifth round of snap and grid follow-ups, checked against AutoCAD 2021's console: GRID, SNAP, SETVAR and a snap or
    /// grid variable typed alone are an undo step each, named by the command; F7, F9 and F8 pressed at the prompt join the
    /// step of the command before them, so that one U takes both back (with no command before them they are a step of
    /// their own); a step that changes only a setting keeps no copy of the drawing and reads nothing again.
    /// </summary>
    public sealed class SnapFollowUpsFiveTests
    {
        private static MainViewModel Opened(TestFolder folder)
        {
            var model = new MainViewModel();
            Assert.True(model.Open(folder.Write("room.dxf", TestFiles.Room(out _, out _))));
            return model;
        }

        private static StatusToggleViewModel Toggle(MainViewModel model, string name) =>
            model.Toggles.Single(t => t.Name == name);

        private static int Lines(MainViewModel model) => model.Document.Database.ModelSpace.OfType<DxfLine>().Count();

        private static void Line(MainViewModel model, double y)
        {
            model.Execute("LINE");
            model.Execute("0," + y);
            model.Execute("100," + y);
            model.Enter();
        }

        [Fact]
        public void GridAndSnapTyped_AreAnUndoStepEach_AsAutoCADsU()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Opened(folder);
                int lines = Lines(model);
                Line(model, 100);
                model.Execute("GRID 50");
                model.Execute("SNAP 25");
                Assert.True(model.GridOn);
                Assert.Equal(50.0, model.GridSpacingX);
                Assert.Equal(25.0, model.Snap.GridX);
                Assert.Equal("SNAP", model.Document.NextUndo.Name);

                // U takes back SNAP 25 alone: the spacing and the toggle as they were, the grid of 50 and the line left.
                model.Execute("U");
                Assert.Equal(10.0, model.Snap.GridX);
                Assert.False(model.Snap.GridSnap);
                Assert.True(model.GridOn);
                Assert.Equal(50.0, model.GridSpacingX);
                Assert.Equal(lines + 1, Lines(model));

                // The second takes back GRID 50, the third the line.
                model.Execute("U");
                Assert.False(model.GridOn);
                Assert.Equal(10.0, model.GridSpacingX);
                Assert.Equal(lines + 1, Lines(model));
                model.Execute("U");
                Assert.Equal(lines, Lines(model));

                // REDO puts them back in order.
                model.Execute("REDO");
                model.Execute("REDO");
                Assert.Equal(lines + 1, Lines(model));
                Assert.True(model.GridOn);
                Assert.Equal(50.0, model.GridSpacingX);
                Assert.Equal(10.0, model.Snap.GridX);
                model.Execute("REDO");
                Assert.Equal(25.0, model.Snap.GridX);
                Assert.True(model.Snap.GridSnap);
            }
        }

        [Fact]
        public void AVariableTypedAlone_IsAnUndoStepNamedByIt()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Opened(folder);
                int lines = Lines(model);
                Line(model, 100);
                model.Execute("GRIDMODE");
                model.Execute("1");
                model.Execute("SNAPUNIT");
                model.Execute("5,5");
                Assert.Equal("SNAPUNIT", model.Document.NextUndo.Name);
                Assert.Equal(5.0, model.Snap.GridX);

                model.Execute("U");
                Assert.Equal(10.0, model.Snap.GridX);
                Assert.True(model.GridOn);
                Assert.Equal("GRIDMODE", model.Document.NextUndo.Name);

                model.Execute("U");
                Assert.False(model.GridOn);
                Assert.Equal(lines + 1, Lines(model));
            }
        }

        [Fact]
        public void F7AfterALine_IsUndoneWithTheLine_NotWithTheEditAfterIt()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Opened(folder);
                int lines = Lines(model);
                Line(model, 100);

                // F7 at the prompt: the grid on, in the step of the line, as AutoCAD's U names LINE for both.
                Toggle(model, "GRID").IsOn = true;
                Assert.Equal("LINE", model.Document.NextUndo.Name);
                Line(model, 200);

                // U takes the second line back and leaves the grid as F7 left it.
                model.Execute("U");
                Assert.Equal(lines + 1, Lines(model));
                Assert.True(model.GridOn);
                Assert.True(model.Document.Database.ViewportTable["*Active"].GridEnabled);

                // The next takes the line and F7 together.
                model.Execute("U");
                Assert.Equal(lines, Lines(model));
                Assert.False(model.GridOn);
                Assert.False(model.Document.Database.ViewportTable["*Active"].GridEnabled);

                // REDO puts the line and F7 back together, and the second line after them.
                model.Execute("REDO");
                Assert.Equal(lines + 1, Lines(model));
                Assert.True(model.GridOn);
                model.Execute("REDO");
                Assert.Equal(lines + 2, Lines(model));
                Assert.True(model.GridOn);
            }
        }

        [Fact]
        public void F7F9AndF8PressedTwice_AfterALine_AreTakenBackByOneU()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Opened(folder);
                int lines = Lines(model);
                Line(model, 100);
                Toggle(model, "GRID").IsOn = true;
                Toggle(model, "SNAP").IsOn = true;
                Toggle(model, "ORTHO").IsOn = true;
                Toggle(model, "GRID").IsOn = false;

                model.Execute("U");
                Assert.Equal(lines, Lines(model));
                Assert.False(model.GridOn);
                Assert.False(model.Snap.GridSnap);
                Assert.False(Toggle(model, "ORTHO").IsOn);
                Assert.False(model.Document.Database.Orthomode);
                Assert.NotNull(model.Document.NextRedo);

                model.Execute("REDO");
                Assert.False(model.GridOn);
                Assert.True(model.Snap.GridSnap);
                Assert.True(Toggle(model, "ORTHO").IsOn);
                Assert.True(model.Document.Database.Orthomode);
            }
        }

        [Fact]
        public void F7BeforeAnyEdit_IsAStepOfItsOwn_AndTheDrawingCountsAsChanged()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Opened(folder);
                Assert.False(model.Document.IsModified);

                Toggle(model, "GRID").IsOn = true;
                Assert.True(model.Document.IsModified);
                Assert.True(model.Document.CanUndo);

                model.Execute("U");
                Assert.False(model.GridOn);
                Assert.False(model.Document.Database.ViewportTable["*Active"].GridEnabled);
                Assert.False(model.Document.IsModified);
            }
        }

        [Fact]
        public void ASettingsStep_KeepsNoCopyOfTheDrawing_AndUndoReadsNothingAgain()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Opened(folder);
                DxfDatabase shown = model.Document.Database;

                model.Execute("GRID 50");
                model.Execute("SNAP 25");
                Toggle(model, "ORTHO").IsOn = true;
                Assert.Same(shown, model.Document.Database);

                // F8 joined the step of SNAP: one U takes both back, and the drawing is not read again.
                model.Execute("U");
                Assert.Same(shown, model.Document.Database);
                Assert.False(model.Snap.GridSnap);
                Assert.False(Toggle(model, "ORTHO").IsOn);
                Assert.True(model.GridOn);

                model.Execute("U");
                Assert.Same(shown, model.Document.Database);
                Assert.False(model.GridOn);

                model.Execute("REDO");
                Assert.Same(shown, model.Document.Database);
                Assert.True(model.GridOn);
                Assert.Equal(50.0, model.GridSpacingX);
                Assert.False(model.Snap.GridSnap);

                model.Execute("REDO");
                Assert.Same(shown, model.Document.Database);
                Assert.True(model.Snap.GridSnap);
                Assert.True(Toggle(model, "ORTHO").IsOn);
            }
        }

        [Fact]
        public void AnEditUndone_LeavesTheSnapAndGridSetBeforeIt()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Opened(folder);
                int lines = Lines(model);
                model.Execute("GRID 50");
                model.Execute("SNAP 25");
                Line(model, 100);

                model.Execute("U");
                Assert.Equal(lines, Lines(model));
                Assert.Equal(50.0, model.GridSpacingX);
                Assert.Equal(25.0, model.Snap.GridX);
                Assert.True(model.GridOn);

                model.Execute("REDO");
                Assert.Equal(lines + 1, Lines(model));
                Assert.Equal(50.0, model.GridSpacingX);
                Assert.Equal(25.0, model.Snap.GridX);

                // And back to the start, step by step.
                model.Execute("U");
                model.Execute("U");
                model.Execute("U");
                Assert.False(model.GridOn);
                Assert.Equal(10.0, model.GridSpacingX);
                Assert.Equal(10.0, model.Snap.GridX);
            }
        }

        [Fact]
        public void TheDraftingSettingsDialogsOk_IsOneUndoStep()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Opened(folder);
                int lines = Lines(model);
                Line(model, 100);
                DraftingSettingsViewModel dialog = model.CreateDraftingSettings(
                    DraftingSettingsViewModel.SnapAndGridTab
                );
                dialog.IsGridOn = true;
                dialog.IsGridSnapOn = true;
                dialog.SnapX = "5";
                dialog.SnapY = "5";
                dialog.GridX = "50";
                dialog.GridY = "50";
                Assert.True(dialog.Ok());
                Assert.True(model.GridOn);
                Assert.Equal(50.0, model.GridSpacingX);
                Assert.Equal(5.0, model.Snap.GridX);
                Assert.Equal("DSETTINGS", model.Document.NextUndo.Name);

                model.Execute("U");
                Assert.False(model.GridOn);
                Assert.False(model.Snap.GridSnap);
                Assert.Equal(10.0, model.GridSpacingX);
                Assert.Equal(10.0, model.Snap.GridX);
                Assert.Equal(lines + 1, Lines(model));
            }
        }

        [Fact]
        public void ASettingChangedThroughAViewport_IsAStepThatTakesBackThatViewportsAlone()
        {
            using (var folder = new TestFolder())
            {
                DxfDatabase db = TestFiles.RoomWithViewport(folder, out _, out string viewport);
                var model = new MainViewModel();
                Assert.True(model.Open(folder.Write("viewport.dxf", db)));
                model.SelectSpace(model.Document.Spaces.Single(s => s.Name == "Layout1"));
                model.Execute("MSPACE");
                var frame = (DxfViewport)model.Document.FindEntity(viewport);
                DxfViewportTableRecord active = model.Document.Database.ViewportTable["*Active"];
                bool modelGrid = active.GridEnabled;

                model.Execute("GRID 9");
                Assert.True(frame.GridOn);
                Assert.Equal(9.0, model.GridSpacingX);

                model.Execute("U");
                frame = (DxfViewport)model.Document.FindEntity(viewport);
                Assert.False(frame.GridOn);
                Assert.NotEqual(9.0, model.GridSpacingX);
                Assert.Equal(modelGrid, model.Document.Database.ViewportTable["*Active"].GridEnabled);

                model.Execute("REDO");
                frame = (DxfViewport)model.Document.FindEntity(viewport);
                Assert.True(frame.GridOn);
                Assert.Equal(9.0, model.GridSpacingX);
            }
        }
    }
}
