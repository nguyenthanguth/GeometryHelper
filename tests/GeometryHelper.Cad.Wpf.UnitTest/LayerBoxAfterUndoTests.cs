using System;
using System.Collections.Generic;
using System.IO.Packaging;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.Cad.Wpf.UnitTest
{
    /// <summary>
    /// The layer box of the ribbon after U, REDO, UNDO n and a change of the layer filter. Each of them makes the list of
    /// layers again, new objects for the same names, and the box must go on showing the current layer. The room has the
    /// layers 0, WALLS and COLUMNS, the current one 0.
    /// </summary>
    public sealed class LayerBoxAfterUndoTests
    {
        private static MainViewModel Open(TestFolder folder)
        {
            var model = new MainViewModel();
            DxfDatabase db = TestFiles.Room(out _, out _);
            db.Clayer = "0";
            Assert.True(model.Open(folder.Write("room.dxf", db)));
            return model;
        }

        private static void Run(MainViewModel model, string lines)
        {
            foreach (string line in lines.Split(';'))
            {
                model.Execute(line);
            }
        }

        /// <summary>Builds the model and the workspace on a thread of their own, laid out offscreen, and runs the check on the layer box.</summary>
        private static void OnLayerBox(Action<MainViewModel, ComboBox> check)
        {
            Exception failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    _ = PackUriHelper.UriSchemePack;

                    using (var folder = new TestFolder())
                    {
                        MainViewModel model = Open(folder);
                        var workspace = new CadWorkspace { DataContext = model };

                        try
                        {
                            workspace.Measure(new Size(1600, 960));
                            workspace.Arrange(new Rect(0, 0, 1600, 960));
                            workspace.UpdateLayout();
                            var box = (ComboBox)workspace.FindName("LayerBox");
                            Assert.Equal("0", Shown(box));
                            check(model, box);
                        }
                        finally
                        {
                            workspace.DrawingCanvas.Dispose();
                        }
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

        private static string Shown(ComboBox box) => ((CadLayerInfo)box.SelectedItem)?.Name;

        [Fact]
        public void WhileTheListOfLayersIsReplaced_TheBoxIsHandedNoLayer_AsItsOwnResetWouldLeaveItBlank()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Open(folder);
                var handed = new List<CadLayerInfo>();

                // What the ComboBox does when its ItemsSource is replaced: it clears its selection, which pushes null into
                // the model, and WPF reads the property again at once, inside the reset.
                model.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(MainViewModel.Layers))
                    {
                        model.CurrentLayer = null;
                        handed.Add(model.CurrentLayer);
                    }
                };

                Run(model, "LINE;0,0;10,10;;U");

                Assert.NotEmpty(handed);
                Assert.All(handed, Assert.Null);
                Assert.Equal("0", model.CurrentLayer.Name);
            }
        }

        [Fact]
        public void AfterAnUndoOfADrawnLine_TheLayerBoxStillShowsTheCurrentLayer()
        {
            OnLayerBox(
                (model, box) =>
                {
                    Run(model, "LINE;0,0;10,10;;U");

                    Assert.Equal("0", Shown(box));

                    Run(model, ";U");

                    Assert.Equal("0", Shown(box));
                }
            );
        }

        [Fact]
        public void AfterARedo_TheLayerBoxStillShowsTheCurrentLayer()
        {
            OnLayerBox(
                (model, box) =>
                {
                    Run(model, "CIRCLE;0,0;5;U");

                    Assert.Equal("0", Shown(box));

                    Run(model, "REDO");

                    Assert.Equal("0", Shown(box));
                }
            );
        }

        [Fact]
        public void AfterUndoAndRedoAcrossAChangeOfTheCurrentLayer_TheBoxShowsTheLayerOfThatMoment()
        {
            OnLayerBox(
                (model, box) =>
                {
                    model.CurrentLayer = model.Layers.Single(l => l.Name == "WALLS");
                    Run(model, "CIRCLE;0,0;5");

                    Assert.Equal("WALLS", Shown(box));

                    Run(model, "U");

                    Assert.Equal("WALLS", Shown(box));

                    Run(model, "U");

                    Assert.Equal("0", model.Document.Database.Clayer);
                    Assert.Equal("0", Shown(box));

                    Run(model, "REDO");

                    Assert.Equal("WALLS", Shown(box));
                }
            );
        }

        [Fact]
        public void AfterUndoOfTwoSteps_TheLayerBoxShowsTheLayerOfThatMoment()
        {
            OnLayerBox(
                (model, box) =>
                {
                    model.CurrentLayer = model.Layers.Single(l => l.Name == "COLUMNS");
                    Run(model, "CIRCLE;0,0;5");

                    Assert.Equal("COLUMNS", Shown(box));

                    Run(model, "UNDO;2");

                    Assert.Equal("0", model.Document.Database.Clayer);
                    Assert.Equal("0", Shown(box));
                }
            );
        }

        [Fact]
        public void WhenTheCurrentLayerIsSetByACommand_TheLayerBoxFollows_AndUndoAndRedoCarryItBackAndForth()
        {
            OnLayerBox(
                (model, box) =>
                {
                    Run(model, "-LAYER;S;WALLS;");

                    Assert.Equal("WALLS", model.Document.Database.Clayer);
                    Assert.Equal("WALLS", Shown(box));

                    Run(model, "CLAYER;COLUMNS");

                    Assert.Equal("COLUMNS", Shown(box));

                    Run(model, "U");

                    Assert.Equal("WALLS", Shown(box));

                    Run(model, "REDO");

                    Assert.Equal("COLUMNS", Shown(box));
                }
            );
        }
    }
}
