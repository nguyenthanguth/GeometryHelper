using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// The windows of batch 4 of acad.pgp driven with no window shown: Viewports and the tiles of model space, Object
    /// Selection Filters, the QuickCalc palette and the Quick Properties panel with QPMODE and Ctrl+Shift+P.
    /// </summary>
    public class ViewUtilityWindowTests
    {
        /// <summary>Two lines on WALLS, one on DOORS and a circle on 0.</summary>
        private static DxfDatabase Drawing()
        {
            var db = new DxfDatabase("AC1027");
            db.LayerTable.Add(new DxfLayerTableRecord { Name = "WALLS" });
            db.LayerTable.Add(new DxfLayerTableRecord { Name = "DOORS" });
            db.ModelSpace.AppendEntity(
                new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0)) { Layer = "WALLS" }
            );
            db.ModelSpace.AppendEntity(
                new DxfLine(new GeoPoint3(0, 50, 0), new GeoPoint3(100, 50, 0)) { Layer = "WALLS" }
            );
            db.ModelSpace.AppendEntity(
                new DxfLine(new GeoPoint3(0, 100, 0), new GeoPoint3(100, 100, 0)) { Layer = "DOORS" }
            );
            db.ModelSpace.AppendEntity(new DxfCircle(new GeoPoint3(300, 0, 0), GeoVector3.ZAxis, 25));
            return db;
        }

        private static MainViewModel Open(TestFolder folder)
        {
            var model = new MainViewModel();
            Assert.True(model.Open(folder.Write("batch4.dxf", Drawing())));
            return model;
        }

        private static int Count<T>(MainViewModel model)
            where T : DxfEntity => model.Document.ModelSpace.Block.OfType<T>().Count();

        [Fact]
        public void Vports_TheDialogsConfigurationAndNewName_SplitTheDisplayAndSaveIt()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Open(folder);
                model.ShowViewports = dialog =>
                {
                    Assert.Equal(ViewportsViewModel.ActiveConfiguration, dialog.Configurations[0]);
                    Assert.Equal(13, dialog.Configurations.Count);
                    dialog.Configuration = "Four: Equal";
                    Assert.Equal(4, dialog.Preview.Count);
                    dialog.NewName = "Quad";
                    return true;
                };

                model.Execute("VIEWPORTS");

                CadTiledViewports tiles = model.ModelTiles;
                Assert.Equal(4, tiles.Tiles.Count);
                Assert.Equal(new[] { "Quad" }, tiles.Names);
                Assert.Equal("Regenerating model.", model.History.Last());
            }
        }

        [Fact]
        public void Vports_NamedViewports_RestoresTheConfigurationPicked()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Open(folder);
                model.Execute("-VPORTS");
                model.Execute("3");
                model.Execute("A");
                model.Execute("-VPORTS");
                model.Execute("S");
                model.Execute("Three");
                model.Execute("-VPORTS");
                model.Execute("SI");
                model.ShowViewports = dialog =>
                {
                    dialog.IsNamedTab = true;
                    Assert.False(dialog.IsNewTab);
                    Assert.Equal("Three", dialog.Named);
                    Assert.Equal(3, dialog.Preview.Count);
                    return true;
                };

                model.Execute("VPORTS");

                Assert.Equal(3, model.ModelTiles.Tiles.Count);
            }
        }

        [Fact]
        public void Vports_ApplyToTheCurrentViewport_PreviewsTheCurrentTileSplitBesideTheOthers()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Open(folder);
                model.Execute("-VPORTS");
                model.Execute("2");
                model.Execute("V");
                var dialog = new ViewportsViewModel(model.ModelTiles)
                {
                    Configuration = "Two: Horizontal",
                    ApplyTo = "Current Viewport",
                };

                IReadOnlyList<DisplayBox> preview = dialog.Preview;
                Assert.Equal(3, preview.Count);
                Assert.Equal(new DisplayBox(0.5, 0.5, 1.0, 1.0), preview[0]);
                Assert.Equal(new DisplayBox(0.0, 0.0, 0.5, 1.0), preview[2]);
            }
        }

        [Fact]
        public void ActivateTile_ShowsTheTilesOwnView_AndKeepsTheViewOfTheTileLeft()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Open(folder);
                model.SetCanvasSize(800, 600);
                model.Execute("-VPORTS");
                model.Execute("2");
                model.Execute("V");
                CadTiledViewports tiles = model.ModelTiles;
                tiles.Tiles[1].Center = new GeoPoint2(300, 0);

                Assert.True(model.ActivateTile(3));
                Assert.Equal(3, tiles.Current.Id);
                Assert.Equal(300.0, model.View.CenterX, 6);
                Assert.Equal(0.0, model.View.CenterY, 6);

                model.View.Pan(100, 0);
                double moved = model.View.CenterX;
                Assert.True(model.ActivateTile(2));
                Assert.Equal(moved, tiles.Tiles.First(t => t.Id == 3).Center.X, 6);
                Assert.False(model.ActivateTile(9));
            }
        }

        [Fact]
        public void Filter_TheDialogsLines_FilterTheObjectsPicked_ForTheNextCommandsPrevious()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Open(folder);
                model.ShowSelectionFilters = dialog =>
                {
                    dialog.Property = CadFilterProperty.Layer;
                    Assert.Contains("WALLS", dialog.Values);
                    dialog.Value = "WALLS";
                    Assert.True(dialog.AddToList());
                    return true;
                };

                model.Execute("FI");
                model.Execute("ALL");
                model.Execute(string.Empty);
                Assert.Equal("Exiting filtered selection.", model.History.Last());

                model.Execute("ERASE");
                model.Execute("P");
                model.Execute(string.Empty);
                Assert.Equal(1, Count<DxfLine>(model));
                Assert.Equal(1, Count<DxfCircle>(model));
            }
        }

        [Fact]
        public void SelectionFilters_EditTheirLines_AndCancelLeavesTheFilterAsItWas()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Open(folder);
                var filter = new CadSelectionFilter();
                var dialog = new SelectionFiltersViewModel(model.Document, filter);
                Assert.Equal(new[] { "Circle", "Line" }, dialog.Values);

                dialog.Property = CadFilterProperty.Color;
                dialog.Value = "300";
                Assert.False(dialog.AddToList());
                Assert.Equal("Invalid Color number.", dialog.Message);

                dialog.Value = "1";
                Assert.True(dialog.AddToList());
                dialog.Property = CadFilterProperty.Object;
                dialog.Value = "Circle";
                Assert.True(dialog.AddToList());
                Assert.Equal(new[] { "Color                = 1 - Red", "Object               = Circle" }, dialog.Lines);

                dialog.SelectedLine = 0;
                dialog.EditItem();
                Assert.Equal(CadFilterProperty.Color, dialog.Property);
                dialog.Value = "ByBlock";
                Assert.True(dialog.Substitute());
                Assert.Equal("Color                = 0 - By Block", dialog.Lines[0]);
                dialog.Delete();
                Assert.Single(dialog.Lines);

                Assert.Empty(filter.Lines);
                dialog.Commit();
                Assert.Single(filter.Lines);

                dialog.SaveName = "Circles";
                dialog.SaveAs();
                Assert.Equal(new[] { CadSelectionFilter.Unnamed, "Circles" }, dialog.Names);
                Assert.Equal("Circles", dialog.CurrentName);
            }
        }

        [Fact]
        public void QuickCalc_EvaluatesKeysAndFunctions_IntoTheInputAndTheHistory()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Open(folder);
                QuickCalcViewModel shown = null;
                model.ShowQuickCalc = palette => shown = palette;
                model.Execute("QC");
                Assert.NotNull(shown);

                foreach (string key in new[] { "1", "+", "2", "*", "3", "=" })
                {
                    shown.Press(key);
                }

                Assert.Equal("7", shown.Input);
                Assert.Equal("7", shown.Result);
                Assert.Equal("1+2*3", Assert.Single(shown.History).Expression);

                shown.Press("x^2");
                shown.Press("=");
                Assert.Equal("49", shown.Input);
                shown.Press("MS");
                Assert.Equal(49.0, shown.Memory);
                shown.Press("sqrt");
                Assert.Equal("sqrt(49)", shown.Input);
                shown.Press("tan");
                Assert.Equal("tang(sqrt(49))", shown.Input);

                shown.Input = "1/0";
                Assert.False(shown.Evaluate());
                Assert.Equal("Error in Expression: Division by 0", shown.Message);

                shown.PasteToCommandLine();
                shown.Input = "[10,20]";
                shown.PasteToCommandLine();
                Assert.Equal("10,20,0", model.CommandText);
            }
        }

        [Fact]
        public void QuickCalc_Distance_AsksForTwoPointsThroughCal_AndPutsTheDistanceIntoTheInput()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Open(folder);
                QuickCalcViewModel shown = null;
                model.ShowQuickCalc = palette => shown = palette;
                model.Execute("QUICKCALC");

                shown.Distance();
                Assert.Equal(">> Enter a point:", model.Session.PromptText);
                model.Session.Feed(new GeoPoint2(0, 0));
                model.Session.Feed(new GeoPoint2(300, 400));

                Assert.False(model.Session.IsRunning);
                Assert.Equal("500", shown.Input);

                model.Execute("LINE");
                shown.GetCoordinates();
                Assert.Equal(
                    "Acquiring points from the drawing area is not possible during a command",
                    model.History.Last()
                );
            }
        }

        [Fact]
        public void Qpmode_On_ShowsTheGeneralRowsOfTheObjectsPicked_AndCtrlShiftPTurnsItOff()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Open(folder);
                Assert.Equal(-1, model.QuickPropertiesMode);
                string line = model.Document.ModelSpace.Block.OfType<DxfLine>().First().Handle;

                model.Document.Selection.Set(new[] { line });
                Assert.False(model.IsQuickPropertiesShown);

                Assert.True(model.RunKey("Ctrl+Shift+P"));
                Assert.Equal("<Quick Properties panel on>", model.History.Last());
                Assert.Equal(1, model.QuickPropertiesMode);
                model.Document.Selection.Clear();
                model.Document.Selection.Set(new[] { line });
                Assert.True(model.IsQuickPropertiesShown);
                Assert.Equal(new[] { "Color", "Layer", "Linetype" }, model.QuickPropertyRows.Select(r => r.Label));
                Assert.Equal("WALLS", model.QuickPropertyRows[1].Value);

                model.QuickPropertyRows[1].Value = "DOORS";
                Assert.Equal("DOORS", model.Document.ModelSpace.Block.OfType<DxfLine>().First().Layer);

                Assert.True(model.RunKey("Ctrl+Shift+P"));
                Assert.Equal("<Quick Properties panel off>", model.History.Last());
                Assert.Equal(-1, model.QuickPropertiesMode);
                Assert.False(model.IsQuickPropertiesShown);

                model.Execute("QPMODE");
                model.Execute("2");
                Assert.Equal(2, model.QuickPropertiesMode);
            }
        }

        [Fact]
        public void QuickProperties_ShowsThePanelWithQpmodeOff_UntilTheSelectionIsLetGo()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Open(folder);
                string line = model.Document.ModelSpace.Block.OfType<DxfLine>().First().Handle;
                model.Document.Selection.Set(new[] { line });

                model.Execute("QP");
                Assert.True(model.IsQuickPropertiesShown);

                model.Document.Selection.Clear();
                Assert.False(model.IsQuickPropertiesShown);
                model.Document.Selection.Set(new[] { line });
                Assert.False(model.IsQuickPropertiesShown);
            }
        }

        [Fact]
        public void EveryWindowOfBatchFour_IsMadeOverItsViewModel_AsTheScreenshotModeDrawsIt()
        {
            using (var folder = new TestFolder())
            {
                string path = folder.Write("windows.dxf", Drawing());
                Exception failure = null;
                var thread = new Thread(() =>
                {
                    try
                    {
                        var model = new MainViewModel();
                        Assert.True(model.Open(path));
                        model.Execute("-VPORTS");
                        model.Execute("4");
                        var windows = new List<Window>
                        {
                            new ViewportsWindow(new ViewportsViewModel(model.ModelTiles)),
                            new SelectionFiltersWindow(
                                new SelectionFiltersViewModel(model.Document, new CadSelectionFilter())
                            ),
                            new QuickCalcWindow(new QuickCalcViewModel(null, null)),
                            new QuickPropertiesWindow(model),
                        };

                        foreach (Window window in windows)
                        {
                            var body = (FrameworkElement)window.Content;
                            window.Content = null;
                            body.DataContext = window.DataContext;
                            double width = double.IsNaN(window.Width) ? 400 : window.Width;
                            double height = double.IsNaN(window.Height) ? 400 : window.Height;
                            body.Measure(new Size(width, height));
                            body.Arrange(new Rect(0, 0, width, height));
                            body.UpdateLayout();
                            Assert.True(body.ActualWidth > 0, window.Title);
                            window.Close();
                        }

                        var tilesView = new CadTilesView();
                        tilesView.Attach(model, new CanvasOptions());
                        tilesView.Measure(new Size(800, 600));
                        tilesView.Arrange(new Rect(0, 0, 800, 600));
                        Assert.Equal(new Rect(401, 301, 399, 299), tilesView.CurrentPlace);
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
