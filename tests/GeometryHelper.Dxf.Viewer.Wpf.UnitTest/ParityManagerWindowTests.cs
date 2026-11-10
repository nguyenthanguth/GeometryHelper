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
        public void ViewManager_NewViewOfAWindowPicked_SetCurrentAndOk_RestoreIt_AndCancelTakesChangesBack()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Open(folder);
                model.SetCanvasSize(400, 300);
                int shown = 0;
                model.ShowViewManager = dialog =>
                {
                    shown++;

                    if (shown == 1)
                    {
                        Assert.Equal(new[] { "Current", "Model Views", "Layout Views", "Preset Views" }, dialog.Nodes.Select(n => n.Name));
                        Assert.False(dialog.Nodes.Last().IsEnabled);
                        Assert.False(dialog.IsNamedChosen);
                        dialog.ShowNewView = view =>
                        {
                            view.ViewName = "Stair core";
                            view.Pick();
                            return true;
                        };
                        Assert.True(dialog.New());
                        return true;
                    }

                    // Back from the corners: New View again, its window picked.
                    Assert.True(dialog.Request.IsNewViewOpen);
                    dialog.ShowNewView = view =>
                    {
                        Assert.Equal("Window 14 400 × 9 000", view.WindowText);
                        Assert.True(view.DefineWindow);
                        Assert.True(view.Ok());
                        return false;
                    };
                    Assert.False(dialog.New());
                    ViewNodeViewModel node = Assert.Single(dialog.Nodes, n => n.IsNamed);
                    Assert.Same(node, dialog.Selected);
                    Assert.Equal("Properties of Stair core", dialog.PropertiesTitle);
                    Assert.Contains(dialog.Properties, p => p.Label == "Width" && p.Value == "14 400.00");
                    Assert.Contains(dialog.Properties, p => p.Label == "Layer snapshot" && p.Value == "Yes");
                    dialog.SetCurrent();
                    Assert.True(dialog.Ok());
                    return true;
                };

                model.Execute("VIEW");
                Assert.Equal("Specify first corner:", model.Session.PromptText);
                model.Execute("11250,1700");
                model.Execute("25650,10700");
                Assert.False(model.Session.IsRunning);
                Assert.Equal(2, shown);
                DxfViewTableRecord saved = model.Document.Database.ViewTable["Stair core"];
                Assert.Equal(18450.0, saved.CenterPoint.X, 6);
                Assert.Equal("ACAD_VIEWS_Stair core", saved.LayerState);
                Assert.Equal(18450.0, model.View.CenterX, 6);
                Assert.Equal("VIEW", model.Document.NextUndo.Name);

                model.ShowViewManager = dialog =>
                {
                    dialog.Selected = dialog.Nodes.Single(n => n.IsNamed);
                    Assert.True(dialog.Delete());
                    Assert.DoesNotContain(dialog.Nodes, n => n.IsNamed);
                    dialog.Cancel();
                    return true;
                };

                model.Execute("VIEW");
                Assert.True(model.Document.Database.ViewTable.Has("Stair core"));
            }
        }

        [Fact]
        public void DrawingUnits_SampleOutputFollowsEachChange_DirectionPickComesBack_AndOkWritesTheHeaderAsOneStep()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Open(folder);
                int shown = 0;
                model.ShowDrawingUnits = dialog =>
                {
                    shown++;

                    if (shown == 1)
                    {
                        Assert.Equal("Decimal", dialog.LengthType);
                        dialog.LengthPrecision = "0.00";
                        Assert.Equal("1.50,2.00,0.00", dialog.SampleLength);
                        dialog.AngleType = "Deg/Min/Sec";
                        dialog.AnglePrecision = "0d";
                        Assert.Equal("3.00<45d,0.00", dialog.SampleAngle);
                        dialog.LengthType = "Architectural";
                        dialog.LengthPrecision = "0'-0 1/2\"";
                        Assert.Equal("0'-1 1/2\",0'-2\",0'-0\"", dialog.SampleLength);
                        dialog.InsertionUnits = DxfUnits.Meters;
                        dialog.Clockwise = true;
                        dialog.ShowDirection = direction =>
                        {
                            Assert.True(direction.East);
                            direction.Other = true;
                            direction.Pick();
                            return true;
                        };
                        Assert.True(dialog.Direction());
                        return true;
                    }

                    dialog.ShowDirection = direction =>
                    {
                        Assert.True(direction.North);
                        Assert.True(direction.Ok());
                        return false;
                    };
                    Assert.False(dialog.Direction());
                    dialog.Close(CadDialogAction.Ok);
                    return true;
                };

                model.Execute("UNITS");
                Assert.Equal("Pick angle:", model.Session.PromptText);
                model.Execute("0,0");
                model.Execute("0,100");
                Assert.False(model.Session.IsRunning);
                Assert.Equal(2, shown);
                DxfDatabase db = model.Document.Database;
                Assert.Equal(4, db.Lunits);
                Assert.Equal(1, db.Luprec);
                Assert.Equal(1, db.Aunits);
                Assert.Equal(0, db.Auprec);
                Assert.True(db.Angdir);
                Assert.Equal(DxfUnits.Meters, db.Insunits);
                Assert.Equal(System.Math.PI / 2.0, db.Angbase, 12);
                Assert.Equal("UNITS", model.Document.NextUndo.Name);

                // The status bar follows LUNITS: architectural to the half inch.
                model.SetCursor(15.5, 0.0);
                Assert.Equal("X 1'-3 1/2\"   Y 0'-0\"", model.CursorText);
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
                        model.ShowViewManager = d => { shown = d; return true; };
                        model.Execute("VIEW");
                        windows.Add(new ViewManagerWindow((ViewManagerViewModel)shown));
                        windows.Add(new NewViewWindow(new NewViewViewModel(((ViewManagerViewModel)shown).Request)));
                        model.ShowDrawingUnits = d => { shown = d; return true; };
                        model.Execute("UNITS");
                        windows.Add(new DrawingUnitsWindow((DrawingUnitsViewModel)shown));
                        windows.Add(new DirectionControlWindow(new DirectionControlViewModel(((DrawingUnitsViewModel)shown).Request)));

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
