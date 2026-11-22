using System;
using System.IO.Packaging;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Xunit;

namespace GeometryHelper.Cad.Wpf.UnitTest
{
    /// <summary>
    /// Home › Layers with the Previous button (LAYERP), found in the window drawn offscreen, and the Layer manager's dialog
    /// as one set of layer changes for it.
    /// </summary>
    public class LayerPreviousRibbonTests
    {
        // Previous moved from the body of Layers to its slide-out, as AutoCAD 2021 keeps it, and its tooltip is the tile's: the name
        // and the command.
        private const string Tip = "Previous (LAYERP)";

        [Fact]
        public void TheLayersSlideOutHasAPreviousTile_ThatStartsLayerp_WithAnAutoCadTooltip()
        {
            OnLayers(
                (model, group) =>
                {
                    Button previous = PreviousOf(group);

                    Assert.Equal("Previous", previous.Content);
                    Assert.Equal(Tip, previous.ToolTip);
                    Assert.True(previous.IsEnabled);
                    Assert.Same(model.StartCommandCommand, previous.Command);

                    int before = model.History.Count;
                    previous.Command.Execute(previous.CommandParameter);
                    Assert.Contains("Command: LAYERP", model.History.Skip(before));
                    Assert.Equal("*No previous layer status*", model.History.Last());
                }
            );
        }

        [Fact]
        public void EverythingChangedWhileTheLayerManagerIsOpen_IsOneSetForTheButton()
        {
            OnLayers(
                (model, group) =>
                {
                    LayerManagerViewModel manager = model.CreateLayerManager();
                    LayerRowViewModel walls = manager.Rows.Single(r => r.Name == "WALLS");
                    LayerRowViewModel columns = manager.Rows.Single(r => r.Name == "COLUMNS");
                    walls.IsOn = false;
                    columns.IsLocked = true;
                    columns.IsOn = false;
                    manager.Dispose();
                    Assert.True(model.Document.Database.LayerTable["WALLS"].IsOff);

                    Button previous = PreviousOf(group);
                    previous.Command.Execute(previous.CommandParameter);

                    Assert.Equal(
                        "Restored previous layer status.",
                        model.History.Reverse().First(l => l.StartsWith("Restored", StringComparison.Ordinal))
                    );
                    Assert.False(model.Document.Database.LayerTable["WALLS"].IsOff);
                    Assert.False(model.Document.Database.LayerTable["COLUMNS"].IsOff);
                    Assert.False(model.Document.Database.LayerTable["COLUMNS"].IsLocked);
                }
            );
        }

        /// <summary>The Previous tile of Layers, in its slide-out: not in the visual tree while the slide-out is shut, so found in the logical one.</summary>
        private static Button PreviousOf(Border group)
        {
            DependencyObject panel = group;

            while (panel != null && !(panel is RibbonPanel))
            {
                panel = VisualTreeHelper.GetParent(panel);
            }

            return LogicalDescendants(((RibbonPanel)panel).SlideOut)
                .OfType<Button>()
                .Single(b => "LAYERP".Equals(b.CommandParameter));
        }

        private static System.Collections.Generic.IEnumerable<object> LogicalDescendants(DependencyObject root)
        {
            yield return root;

            foreach (object child in LogicalTreeHelper.GetChildren(root))
            {
                if (child is DependencyObject below)
                {
                    foreach (object found in LogicalDescendants(below))
                    {
                        yield return found;
                    }
                }
            }
        }

        private static void OnLayers(Action<MainViewModel, Border> check)
        {
            using (var folder = new TestFolder())
            {
                string room = folder.Write("room.dxf", TestFiles.Room(out _, out _));
                Exception failure = null;

                var thread = new Thread(() =>
                {
                    try
                    {
                        _ = PackUriHelper.UriSchemePack;
                        var model = new MainViewModel();
                        Assert.True(model.Open(room));
                        var workspace = new CadWorkspace { DataContext = model };

                        try
                        {
                            workspace.Measure(new Size(1600, 960));
                            workspace.Arrange(new Rect(0, 0, 1600, 960));
                            workspace.UpdateLayout();

                            Border group = Descendants(workspace)
                                .OfType<Border>()
                                .Last(b =>
                                    Descendants(b).OfType<TextBlock>().Any(t => t.Text == "Layers")
                                    && Descendants(b).OfType<Button>().Any(x => Equals(x.Content, "Match Layer"))
                                );
                            check(model, group);
                        }
                        finally
                        {
                            workspace.DrawingCanvas.Dispose();
                        }
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

        private static System.Collections.Generic.IEnumerable<DependencyObject> Descendants(DependencyObject root)
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
