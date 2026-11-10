using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// The parity managers driven with no window: the Layer States Manager (LAYERSTATE); and each of their windows made over
    /// its view model, as the screenshot mode draws them.
    /// </summary>
    public class ParityManagerWindowTests
    {
        /// <summary>A drawing of R2013 with the layers 0, WALLS red and DIMS off.</summary>
        private static DxfDatabase Drawing()
        {
            var db = new DxfDatabase("AC1027");
            db.LayerTable.Add(new DxfLayerTableRecord { Name = "WALLS", ColorIndex = 1 });
            db.LayerTable.Add(new DxfLayerTableRecord { Name = "DIMS", ColorIndex = 7 });
            db.LayerTable["DIMS"].IsOff = true;
            return db;
        }

        private static MainViewModel Open(TestFolder folder)
        {
            var model = new MainViewModel();
            Assert.True(model.Open(folder.Write("plan-level2.dxf", Drawing())));
            return model;
        }

        [Fact]
        public void LayerStatesManager_NewTicksRenameAndRestore_AsOneUndoStep()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Open(folder);
                model.ShowLayerStates = dialog =>
                {
                    Assert.Empty(dialog.States);
                    Assert.False(dialog.HasSelected);
                    Assert.False(dialog.CanOverride);
                    Assert.True(dialog.HideXrefStates);
                    Assert.Equal("*UNSAVED*", dialog.Current);
                    dialog.AskName = (title, offered) =>
                    {
                        Assert.Equal("New Layer State to Save", title);
                        return "Plan - all on";
                    };
                    Assert.True(dialog.New());
                    LayerStateRowViewModel row = Assert.Single(dialog.States);
                    Assert.Same(row, dialog.Selected);
                    Assert.Equal("Model", row.Space);
                    Assert.Equal("Yes", row.Same);
                    Assert.Equal("Plan - all on", dialog.Current);

                    // Every property ticked but plot style's greyed ones; Color unticked writes the mask.
                    Assert.All(dialog.Properties, p => Assert.True(p.IsChecked));
                    Assert.Equal(new[] { "Visibility in current viewport", "Plot style", "New VP freeze" }, dialog.Properties.Where(p => !p.IsEnabled).Select(p => p.Name));
                    dialog.Properties.Single(p => p.Name == "Color").IsChecked = false;
                    Assert.Equal(2047 & ~0x20, dialog.Selected.Row.Mask);
                    dialog.SetAll(false);
                    Assert.Equal(0x200 | 0x100 | 0x10, dialog.Selected.Row.Mask);
                    dialog.SetAll(true);
                    Assert.Equal(2047, dialog.Selected.Row.Mask);

                    dialog.AskName = (title, offered) => offered == "Plan - all on" ? "Plan" : null;
                    Assert.True(dialog.Rename());
                    Assert.Equal("Plan", dialog.Selected.Name);
                    dialog.TurnOffMissing = true;
                    dialog.Restore();
                    return true;
                };

                model.Execute("LAYERSTATE");
                Assert.False(model.Session.IsRunning);
                Assert.Equal(new[] { "Plan" }, model.Document.Database.LayerStateManager.GetLayerStateNames());
                Assert.Equal("LAYERSTATE", model.Document.NextUndo.Name);
                Assert.True(model.Document.Undo());
                Assert.Empty(model.Document.Database.LayerStateManager.GetLayerStateNames());
            }
        }

        [Fact]
        public void EveryParityWindow_IsMadeOverItsViewModel_AsTheScreenshotModeDrawsIt()
        {
            using (var folder = new TestFolder())
            {
                string path = folder.Write("plan-level2.dxf", Drawing());
                Exception failure = null;
                var thread = new Thread(() =>
                {
                    try
                    {
                        var model = new MainViewModel();
                        Assert.True(model.Open(path));
                        object shown = null;
                        model.ShowLayerStates = d => { shown = d; return true; };
                        model.Execute("LAYERSTATE");
                        var windows = new List<Window> { new LayerStatesWindow((LayerStatesViewModel)shown) };

                        foreach (Window window in windows)
                        {
                            var body = (FrameworkElement)window.Content;
                            window.Content = null;
                            body.DataContext = window.DataContext;
                            body.Measure(new Size(window.Width, window.Height));
                            body.Arrange(new Rect(0, 0, window.Width, window.Height));
                            body.UpdateLayout();
                            Assert.True(body.ActualWidth > 0, window.Title);
                            window.Close();
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
    }
}
