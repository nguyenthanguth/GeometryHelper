using System;
using System.IO.Packaging;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// Home › Layers with the Previous button (LAYERP), found in the window drawn offscreen, and the Layer manager's dialog
    /// as one set of layer changes for it.
    /// </summary>
    public class LayerPreviousRibbonTests
    {
        private const string Tip = "Undoes the last change or set of changes made to layer settings (LAYERP)";

        [Fact]
        public void LayersHasAPreviousButtonNextToMatchLayer_ThatStartsLayerp_WithAnAutoCadTooltip()
        {
            OnLayers(
                (model, group) =>
                {
                    Button previous = Buttons(group).Single(b => "LAYERP".Equals(b.CommandParameter));

                    Assert.Equal("Previous", previous.Content);
                    Assert.Equal(Tip, previous.ToolTip);
                    Assert.True(previous.IsEnabled);
                    Assert.Same(model.StartCommandCommand, previous.Command);
                    Button match = Buttons(group).Single(b => Equals(b.Content, "Match Layer"));
                    Assert.Equal(match.Style, previous.Style);

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

                    Button previous = Buttons(group).Single(b => "LAYERP".Equals(b.CommandParameter));
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

        private static System.Collections.Generic.List<Button> Buttons(Border group) =>
            Descendants(group).OfType<Button>().ToList();

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
