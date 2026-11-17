using System;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Cad.Wpf.UnitTest.CanvasMouseTests;

namespace GeometryHelper.Cad.Wpf.UnitTest
{
    /// <summary>
    /// A double click of the real mouse events on Layout1, offscreen on an STA thread: the room's viewport is 200 by 150 about
    /// (150, 100) on the sheet, showing the room at 1:20. A double click inside it works through it (MSPACE), wherever inside,
    /// and on the paper off every viewport, while working through one, goes back to the paper (PSPACE).
    /// </summary>
    public class CanvasViewportDoubleClickTests
    {
        private const int Width = 800;
        private const int Height = 600;

        internal static void OnLayout(
            Action<DxfDatabase, string> prepare,
            Action<MainViewModel, CadCanvas, string> test
        )
        {
            Exception failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    using (var folder = new TestFolder())
                    {
                        DxfDatabase db = TestFiles.RoomWithViewport(folder, out _, out string viewport);
                        prepare?.Invoke(db, viewport);
                        var model = new MainViewModel();
                        model.Open(folder.Write("room.dxf", db));
                        model.SelectSpace(model.Document.Spaces.Single(s => s.Name == "Layout1"));
                        var canvas = new CadCanvas
                        {
                            Document = model.Document,
                            Space = model.Space,
                            View = model.View,
                            Session = model.Session,
                            Snap = model.Snap,
                        };
                        canvas.PixelSizeChanged += (s, e) => model.SetCanvasSize(canvas.PixelWidth, canvas.PixelHeight);
                        canvas.Measure(new Size(Width, Height));
                        canvas.Arrange(new Rect(0, 0, Width, Height));
                        canvas.RenderNow();
                        model.Toggles.Single(t => t.Name == "OSNAP").IsOn = false;
                        test(model, canvas, viewport);
                        canvas.Dispose();
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

        internal static DxfViewport FrameOf(DxfDatabase db, string handle) =>
            db.Layouts.Single(l => l.LayoutName == "Layout1").GetViewports().Single(v => v.Handle == handle);

        /// <summary>The events of a left double click as Windows gives them: a press and a release, then a press counted as the second and its release.</summary>
        internal static void DoubleClick(CadCanvas canvas, Point at)
        {
            Mouse(canvas, UIElement.MouseMoveEvent, at);
            Press(canvas, at, 1);
            Mouse(canvas, UIElement.MouseUpEvent, at);
            Press(canvas, at, 2);
            Mouse(canvas, UIElement.MouseUpEvent, at);
        }

        private static void Press(CadCanvas canvas, Point at, int count)
        {
            canvas.Pointer = _ => at;
            var args = new MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice, 0, MouseButton.Left)
            {
                RoutedEvent = UIElement.MouseDownEvent,
            };
            typeof(MouseButtonEventArgs)
                .GetProperty(nameof(MouseButtonEventArgs.ClickCount))
                .GetSetMethod(true)
                .Invoke(args, new object[] { count });
            canvas.RaiseEvent(args);
        }

        [Fact]
        public void ADoubleClickOnEmptyPaperInsideTheViewport_WorksThroughIt_WhateverTheFirstClickOpened()
        {
            OnLayout(null, (model, canvas, viewport) =>
            {
                DoubleClick(canvas, At(canvas, 150, 120));

                Assert.Equal(viewport, canvas.ThroughHandle);
                Assert.Equal("MODEL", model.SpaceLabel);

                // The implied window the first click opened is gone, as the command line shows no command running, and
                // nothing is selected.
                Assert.False(model.Session.IsRunning);
                Assert.False(model.Session.IsWindowing);
                Assert.Empty(model.Document.Selection.Handles);
            });
        }

        [Fact]
        public void ADoubleClickNearEveryCornerAndTheMiddle_WorksThroughTheViewport()
        {
            double[][] spots =
            {
                new[] { 51.0, 26.0 },
                new[] { 249.0, 26.0 },
                new[] { 51.0, 174.0 },
                new[] { 249.0, 174.0 },
                new[] { 150.0, 100.0 },
                new[] { 60.0, 100.0 },
            };

            foreach (double[] spot in spots)
            {
                OnLayout(null, (model, canvas, viewport) =>
                {
                    DoubleClick(canvas, At(canvas, spot[0], spot[1]));
                    Assert.Equal(viewport, canvas.ThroughHandle);
                });
            }
        }

        [Fact]
        public void ADoubleClickOnTheFrameEdge_WorksThroughTheViewport()
        {
            OnLayout(null, (model, canvas, viewport) =>
            {
                DoubleClick(canvas, At(canvas, 150, 25));

                Assert.Equal(viewport, canvas.ThroughHandle);
            });
        }

        [Fact]
        public void ADoubleClickOnAPaperLineInsideTheViewport_GoesToThatObject_AndStaysOnThePaper()
        {
            OnLayout(
                (db, viewport) =>
                {
                    var line = new DxfLine(new GeoPoint3(80, 60, 0), new GeoPoint3(220, 60, 0));
                    ((DxfBlockTableRecord)FrameOf(db, viewport).Owner).AppendEntity(line);
                },
                (model, canvas, viewport) =>
                {
                    string doubled = null;
                    canvas.ObjectDoubleClicked += (s, e) => doubled = e.Handle;

                    DoubleClick(canvas, At(canvas, 150, 60));

                    Assert.NotNull(doubled);
                    Assert.IsType<DxfLine>(model.Document.FindEntity(doubled));
                    Assert.Null(canvas.ThroughHandle);
                }
            );
        }

        [Fact]
        public void ADoubleClickOnThePaperOffEveryViewport_WhileWorkingThrough_GoesBackToThePaper()
        {
            OnLayout(null, (model, canvas, viewport) =>
            {
                DoubleClick(canvas, At(canvas, 150, 120));
                Assert.Equal(viewport, canvas.ThroughHandle);

                DoubleClick(canvas, At(canvas, 10, 10));

                Assert.Null(canvas.ThroughHandle);
                Assert.Equal("PAPER", model.SpaceLabel);
                Assert.False(model.Session.IsRunning);
            });
        }

        [Fact]
        public void ADoubleClickInsideAViewportOnALayerSwitchedOff_WorksThroughNothing()
        {
            OnLayout(
                (db, viewport) =>
                {
                    db.LayerTable.Add(new DxfLayerTableRecord { Name = "VPORTS", IsOff = true });
                    FrameOf(db, viewport).Layer = "VPORTS";
                },
                (model, canvas, viewport) =>
                {
                    DoubleClick(canvas, At(canvas, 150, 120));

                    Assert.Null(canvas.ThroughHandle);
                    Assert.Equal("PAPER", model.SpaceLabel);
                    Assert.Empty(model.Document.EnterableViewports);
                }
            );
        }

        [Fact]
        public void ADoubleClickInsideAViewportOnAFrozenLayer_WorksThroughNothing()
        {
            OnLayout(
                (db, viewport) =>
                {
                    db.LayerTable.Add(new DxfLayerTableRecord { Name = "VPORTS", IsFrozen = true });
                    FrameOf(db, viewport).Layer = "VPORTS";
                },
                (model, canvas, viewport) =>
                {
                    DoubleClick(canvas, At(canvas, 150, 120));

                    Assert.Null(canvas.ThroughHandle);
                }
            );
        }

        [Fact]
        public void ADoubleClickInsideAViewportOnANoPlotLayer_WorksThroughIt()
        {
            OnLayout(
                (db, viewport) =>
                {
                    db.LayerTable.Add(new DxfLayerTableRecord { Name = "VPORTS", IsPlottable = false });
                    FrameOf(db, viewport).Layer = "VPORTS";
                },
                (model, canvas, viewport) =>
                {
                    DoubleClick(canvas, At(canvas, 150, 120));

                    Assert.Equal(viewport, canvas.ThroughHandle);
                }
            );
        }

        [Fact]
        public void ADoubleClickInsideTheViewport_WhileALineAsksItsFirstPoint_DoesNotWorkThroughIt()
        {
            OnLayout(null, (model, canvas, viewport) =>
            {
                model.StartCommand("LINE");

                DoubleClick(canvas, At(canvas, 150, 120));

                Assert.Null(canvas.ThroughHandle);
            });
        }
    }
}
