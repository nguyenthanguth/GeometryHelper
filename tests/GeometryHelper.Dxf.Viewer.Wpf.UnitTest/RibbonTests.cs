using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Packaging;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// The ribbon tabs Annotate and View, the new commands of the window (NEW, SAVEAS, PROPERTIESCLOSE, the toggles typed) and
    /// the + of the layout tabs, driven with no window; and every button the round added found in the window drawn offscreen,
    /// enabled, and run.
    /// </summary>
    public class RibbonTests
    {
        private static MainViewModel Open(TestFolder folder)
        {
            var model = new MainViewModel();
            Assert.True(model.Open(folder.Write("room.dxf", TestFiles.Room(out _, out _))));
            return model;
        }

        [Fact]
        public void AnnotateAndViewAreTabsThatCanBeShown()
        {
            var model = new MainViewModel();

            model.SelectTabCommand.Execute(model.AnnotateTab);

            Assert.False(model.IsHomeTabSelected);
            Assert.True(model.AnnotateTab.IsTabSelected);
            Assert.IsType<AnnotateRibbonViewModel>(model.AnnotateTab.TabContent);

            model.SelectTabCommand.Execute(model.ViewTab);

            Assert.False(model.AnnotateTab.IsTabSelected);
            Assert.True(model.ViewTab.IsTabSelected);
            Assert.IsType<ViewRibbonViewModel>(model.ViewTab.TabContent);
            Assert.Equal("View", model.ViewTab.TabHeader);
        }

        [Fact]
        public void NewMakesAnEmptyDrawingNamedAsAutoCadNamesItsOwn_WithOrWithoutADrawingOpen()
        {
            using (var folder = new TestFolder())
            {
                var model = new MainViewModel();

                model.Execute("NEW");

                Assert.True(model.HasDocument);
                Assert.Equal("Drawing1.dwg", model.Title);
                Assert.Empty(model.Document.ModelSpace.Block);
                Assert.False(model.Document.HasFile);
                Assert.Equal(new[] { "Model", "Layout1" }, model.SpaceTabs.Select(t => t.Name));

                model.NewCommand.Execute(null);
                Assert.Equal("Drawing2.dwg", model.Title);

                model.Open(folder.Write("room.dxf", TestFiles.Room(out _, out _)));
                model.Execute("NEW");

                Assert.Equal("Drawing3.dwg", model.Title);
                Assert.False(model.IsCommandRunning);
                Assert.Equal(4, model.FileRows.Count(r => r.IsOpen));
            }
        }

        [Fact]
        public void SaveAsWithNoWindowAsksForTheFileOnTheCommandLine_AndTheDrawingIsKnownByIt()
        {
            using (var folder = new TestFolder())
            {
                var model = new MainViewModel();
                model.NewCommand.Execute(null);
                model.Execute("LINE 0,0 100,0");
                model.Enter();
                string path = Path.Combine(folder.FullPath, "plan.dxf");

                model.SaveAsCommand.Execute(null);

                Assert.Equal("Save drawing as <Drawing1.dwg>:", model.Prompt);
                model.Execute(path);

                Assert.True(File.Exists(path));
                Assert.Equal(path, model.Document.FileName);
                Assert.False(model.HasUnsavedChanges);
                Assert.Equal("plan.dxf", model.Title);
                Assert.IsType<DxfLine>(Assert.Single(DxfDatabase.Open(path).ModelSpace));
            }
        }

        [Fact]
        public void ThePlusOfTheLayoutTabsAddsLayout2_AndUndoTakesTheTabBack()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Open(folder);

                model.NewLayoutCommand.Execute(null);

                Assert.Equal(new[] { "Model", "Layout1", "Layout2" }, model.SpaceTabs.Select(t => t.Name));
                Assert.True(model.SpaceTabs[0].IsActive);
                Assert.Equal("Layout \"Layout2\" created.", model.History.Last());

                model.SelectSpaceCommand.Execute(model.SpaceTabs[2]);
                Assert.Equal("Layout2", model.Space.Name);

                Assert.True(model.Undo());

                Assert.Equal(new[] { "Model", "Layout1" }, model.SpaceTabs.Select(t => t.Name));
                Assert.True(model.IsModelSpace);
                Assert.True(model.SpaceTabs[0].IsActive);
            }
        }

        [Fact]
        public void TheTogglesTypedSetTheSameStateAsTheStatusBar()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Open(folder);
                bool On(string name) => model.Toggles.Single(t => t.Name == name).IsOn;

                model.Execute("ORTHO");
                Assert.Equal("Enter mode [ON/OFF] <OFF>:", model.Prompt);
                model.Execute("ON");
                Assert.True(On("ORTHO"));
                Assert.True(model.Snap.Ortho);

                model.Execute("OSNAP OFF");
                Assert.False(On("OSNAP"));
                Assert.False(model.Snap.IsOn);

                model.Execute("GRID ON");
                Assert.True(model.GridOn);

                model.Execute("POLAR");
                model.Enter();
                Assert.True(On("POLAR"));

                model.Execute("SNAP OFF");
                Assert.False(model.Snap.GridSnap);
                model.Execute("SNAP");
                Assert.Equal("Specify snap spacing or [ON/OFF/Aspect/Legacy/Style/Type] <10.0000>:", model.Prompt);
                model.Execute("25");
                Assert.True(model.Snap.GridSnap);
                Assert.Equal(25.0, model.Snap.GridX, 12);
                Assert.Equal(25.0, model.Snap.GridY, 12);

                // ORTHO, GRID and SNAP typed are undo steps that change the drawing's settings, as AutoCAD's DBMOD 4 says.
                Assert.True(model.Document.IsModified);
            }
        }

        [Fact]
        public void PropertiesCloseHidesThePanel_PropertiesShowsIt_AndTheInterfaceTogglesHideEither()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Open(folder);
                Assert.True(model.IsPropertiesPanelShown);
                Assert.True(model.IsFileListShown);

                model.Execute("PROPERTIESCLOSE");
                Assert.False(model.IsPropertiesPanelShown);

                model.Execute("PR");
                Assert.True(model.IsPropertiesPanelShown);

                model.IsFileListShown = false;
                Assert.False(model.IsFileListShown);
            }
        }

        [Fact]
        public void TheNavigateButtonsZoomInOutAndBack_AndTheViewsSwitchModelAndLayout()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Open(folder);
                model.SetCanvasSize(1000, 600);
                var view = (ViewRibbonViewModel)model.ViewTab.TabContent;
                double fitted = model.View.PixelsPerUnit;

                view.RunCommand.Execute("ZOOM 2X");
                Assert.Equal(2.0 * fitted, model.View.PixelsPerUnit, 9);

                view.RunCommand.Execute("ZOOM 0.5X");
                view.RunCommand.Execute("ZOOM 0.5X");
                Assert.Equal(0.5 * fitted, model.View.PixelsPerUnit, 9);

                view.RunCommand.Execute("ZOOM P");
                Assert.Equal(fitted, model.View.PixelsPerUnit, 9);

                view.LayoutCommand.Execute(null);
                Assert.Equal("Layout1", model.Space.Name);
                view.ModelSpaceCommand.Execute(null);
                Assert.True(model.IsModelSpace);

                view.Grid.IsOn = true;
                Assert.True(model.GridOn);
            }
        }

        [Fact]
        public void EveryButtonOfAnnotateViewAndTheNewHomeButtonsIsEnabledAndRunsItsCommand()
        {
            using (var folder = new TestFolder())
            {
                string room = folder.Write("room.dxf", TestFiles.Room(out _, out _));
                Exception failure = null;

                var thread = new Thread(() =>
                {
                    try
                    {
                        PressEveryButton(room);
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
        }

        private static void PressEveryButton(string room)
        {
            // Registers the pack scheme, which the XAML of the controls is loaded through, with no Application running.
            _ = PackUriHelper.UriSchemePack;
            var model = new MainViewModel();
            Assert.True(model.Open(room));
            var workspace = new CadWorkspace { DataContext = model };

            try
            {
                // Home: the buttons this round added start their commands.
                Layout(workspace);
                var home = Descendants(workspace)
                    .OfType<Button>()
                    .Where(b => b.Command == model.StartCommandCommand)
                    .ToList();
                string[] added = { "RECTANG", "ELLIPSE", "POINT", "DIST", "AREA", "ID", "LIST" };
                Assert.Equal(added, home.Select(b => b.CommandParameter as string).Where(added.Contains));

                foreach (Button button in home.Where(b => added.Contains(b.CommandParameter as string)))
                {
                    Press(model, button, (string)button.CommandParameter);
                }

                // Annotate: each button starts its command as Home's do.
                model.SelectedTab = model.AnnotateTab;
                Layout(workspace);
                List<ButtonBase> annotate = ButtonsOf(workspace, model.AnnotateTab.TabContent)
                    .Where(b => b.CommandParameter is string)
                    .ToList();
                Assert.Equal(
                    new[] { "MTEXT", "TEXT", "DIMLINEAR", "QDIM", "DIMCONTINUE", "DIMSPACE", "DIMBREAK", "MLEADER" },
                    annotate.Select(b => b.CommandParameter as string)
                );

                foreach (ButtonBase button in annotate)
                {
                    Press(model, button, (string)button.CommandParameter);
                }

                // View: Navigate runs its line, Views switches the space, Interface and Drafting toggle the state they show.
                model.SelectedTab = model.ViewTab;
                Layout(workspace);
                // The panels' own toggles, which a collapsed panel draws, are not the tab's buttons.
                List<ButtonBase> view = ButtonsOf(workspace, model.ViewTab.TabContent)
                    .Where(b => !(b.TemplatedParent is RibbonPanel))
                    .ToList();
                Assert.Equal(16, view.Count);

                foreach (Button button in view.OfType<Button>().Where(b => b.CommandParameter is string))
                {
                    Press(model, button, ((string)button.CommandParameter).Split(' ')[0]);
                }

                Button layout = view.OfType<Button>().Single(b => (string)b.Content == "Layout");
                Assert.True(layout.IsEnabled);
                layout.Command.Execute(layout.CommandParameter);
                Assert.Equal("Layout1", model.Space.Name);
                Button modelSpace = view.OfType<Button>().Single(b => (string)b.Content == "Model");
                modelSpace.Command.Execute(modelSpace.CommandParameter);
                Assert.True(model.IsModelSpace);

                ToggleButton drawings = view.OfType<ToggleButton>().Single(b => (string)b.Content == "Drawings");
                Assert.True(drawings.IsChecked);
                drawings.IsChecked = false;
                Assert.False(model.IsFileListShown);
                ToggleButton properties = view.OfType<ToggleButton>().Single(b => (string)b.Content == "Properties");
                properties.IsChecked = false;
                Assert.False(model.IsPropertiesPanelShown);

                foreach (
                    ToggleButton toggle in view.OfType<ToggleButton>().Where(b => b != drawings && b != properties)
                )
                {
                    StatusToggleViewModel state = model.Toggles.Single(t =>
                        string.Equals(t.Name, (string)toggle.Content, StringComparison.OrdinalIgnoreCase)
                    );
                    Assert.True(toggle.IsEnabled);
                    Assert.Equal(state.IsOn, toggle.IsChecked);
                    toggle.IsChecked = !toggle.IsChecked;
                    Assert.Equal(state.IsOn, toggle.IsChecked);
                }

                // The quick access New and Save As, the + of the layout tabs.
                model.SelectedTab = null;
                Layout(workspace);
                Button plus = Descendants(workspace).OfType<Button>().Single(b => b.Command == model.NewLayoutCommand);
                Assert.True(plus.IsEnabled);
                plus.Command.Execute(null);
                Assert.Equal(3, model.SpaceTabs.Count);
                Button saveAs = Descendants(workspace).OfType<Button>().Single(b => b.Command == model.SaveAsCommand);
                Assert.True(saveAs.IsEnabled);
                Button newButton = Descendants(workspace).OfType<Button>().First(b => b.Command == model.NewCommand);
                Assert.True(newButton.IsEnabled);
                newButton.Command.Execute(null);
                Assert.Equal("Drawing1.dwg", model.Title);
            }
            finally
            {
                workspace.DrawingCanvas.Dispose();
            }
        }

        /// <summary>A button enabled, pressed, and the command it names started; then Esc.</summary>
        private static void Press(MainViewModel model, ButtonBase button, string command)
        {
            Assert.True(button.IsEnabled, command + " is not enabled.");
            int before = model.History.Count;
            button.Command.Execute(button.CommandParameter);
            Assert.Contains("Command: " + command, model.History.Skip(before));
            Assert.DoesNotContain(model.History.Skip(before), l =>
                l.StartsWith("Unknown command", StringComparison.Ordinal)
            );
            model.Escape();
        }

        private static void Layout(FrameworkElement element)
        {
            element.Measure(new Size(1600, 960));
            element.Arrange(new Rect(0, 0, 1600, 960));
            element.UpdateLayout();
        }

        /// <summary>The buttons of a ribbon tab: those under the presenter of its content, in the order they are drawn.</summary>
        private static List<ButtonBase> ButtonsOf(DependencyObject root, object content)
        {
            ContentPresenter presenter = Descendants(root)
                .OfType<ContentPresenter>()
                .First(p => ReferenceEquals(p.Content, content));
            return Descendants(presenter).OfType<ButtonBase>().ToList();
        }

        private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(root, i);
                yield return child;

                foreach (DependencyObject below in Descendants(child))
                {
                    yield return below;
                }
            }
        }
    }
}
