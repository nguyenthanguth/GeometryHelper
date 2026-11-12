using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.Wpf.UnitTest.CanvasMouseTests;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// The wheel and the middle button on the paper of a layout and through one of its viewports: each gesture goes on the
    /// ZOOM Previous list of the paper or of the viewport and is an undo step, and a double click of the middle button is
    /// ZOOM Extents of the space or the viewport worked in, as typed. Offscreen on an STA thread; the room's viewport is 200 by
    /// 150 about (150, 100) on the sheet, showing the room at 1:20.
    /// </summary>
    public class CanvasLayoutWheelTests
    {
        private const int Width = 800;
        private const int Height = 600;

        private static void OnLayout(Action<MainViewModel, CadCanvas, string> test, bool model = false)
        {
            Exception failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    using (var folder = new TestFolder())
                    {
                        var main = new MainViewModel();
                        main.Open(
                            folder.Write("room.dxf", TestFiles.RoomWithViewport(folder, out _, out string viewport))
                        );

                        if (!model)
                        {
                            main.SelectSpace(main.Document.Spaces.Single(s => s.Name == "Layout1"));
                        }

                        var canvas = new CadCanvas
                        {
                            Document = main.Document,
                            Space = main.Space,
                            View = main.View,
                            Session = main.Session,
                            Snap = main.Snap,
                        };
                        canvas.PixelSizeChanged += (s, e) => main.SetCanvasSize(canvas.PixelWidth, canvas.PixelHeight);
                        canvas.Measure(new Size(Width, Height));
                        canvas.Arrange(new Rect(0, 0, Width, Height));
                        canvas.RenderNow();
                        main.Toggles.Single(t => t.Name == "OSNAP").IsOn = false;
                        test(main, canvas, viewport);
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

        private static DxfViewport Frame(MainViewModel model, string handle) =>
            (DxfViewport)model.Document.FindEntity(handle);

        private static void ZoomPrevious(MainViewModel model)
        {
            model.StartCommand("ZOOM");
            model.Execute("P");
        }

        private static void DoubleClickMiddle(CadCanvas canvas, Point at)
        {
            Mouse(canvas, UIElement.MouseMoveEvent, at);
            canvas.Pointer = _ => at;
            var args = new MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice, 0, MouseButton.Middle)
            {
                RoutedEvent = UIElement.MouseDownEvent,
            };
            typeof(MouseButtonEventArgs)
                .GetProperty(nameof(MouseButtonEventArgs.ClickCount))
                .GetSetMethod(true)
                .Invoke(args, new object[] { 2 });
            canvas.RaiseEvent(args);
        }

        [Fact]
        public void TheWheelOnThePaper_IsAUndoStepAndAnEntryOfThePapersZoomPreviousList_OneForEachGesture()
        {
            OnLayout(
                (model, canvas, viewport) =>
                {
                    double zoom = canvas.View.PixelsPerUnit;
                    CadUndoStep start = model.Document.NextUndo;
                    Point at = At(canvas, 150, 100);

                    // Two notches before the view is still, a pause, then one more: two gestures.
                    Mouse(canvas, UIElement.MouseWheelEvent, at, delta: 120);
                    Mouse(canvas, UIElement.MouseWheelEvent, at, delta: 120);
                    canvas.RenderNow();
                    Mouse(canvas, UIElement.MouseWheelEvent, at, delta: 120);
                    canvas.RenderNow();

                    Assert.Equal(zoom * 1.2 * 1.2 * 1.2, canvas.View.PixelsPerUnit, 9);
                    Assert.Equal("ZOOM", model.Document.NextUndo.Name);

                    ZoomPrevious(model);
                    Assert.Equal(zoom * 1.44, canvas.View.PixelsPerUnit, 9);
                    ZoomPrevious(model);
                    Assert.Equal(zoom, canvas.View.PixelsPerUnit, 9);
                    ZoomPrevious(model);
                    Assert.Equal("No previous view saved.", model.Session.History.Last());

                    // The wheel's steps, one for the gestures made straight after each other, then those of ZOOM Previous, come off by U.
                    while (model.Document.NextUndo != null && !ReferenceEquals(model.Document.NextUndo, start))
                    {
                        Assert.True(model.Document.Undo());
                    }

                    Assert.Equal(zoom, canvas.View.PixelsPerUnit, 9);
                }
            );
        }

        [Fact]
        public void TheMiddleButtonOnThePaper_PansWithAStepNamedPan_AndZoomPreviousBringsThePaperBack()
        {
            OnLayout(
                (model, canvas, viewport) =>
                {
                    double x = canvas.View.CenterX;

                    Drag(canvas, new Point(100, 100), new Point(150, 130), MouseButton.Middle);
                    canvas.RenderNow();

                    Assert.NotEqual(x, canvas.View.CenterX, 6);
                    Assert.Equal("PAN", model.Document.NextUndo.Name);

                    ZoomPrevious(model);
                    Assert.Equal(x, canvas.View.CenterX, 6);

                    // U takes the ZOOM Previous back (it was a step too), then the pan.
                    Assert.True(model.Document.Undo());
                    Assert.NotEqual(x, canvas.View.CenterX, 6);
                    Assert.True(model.Document.Undo());
                    Assert.Equal(x, canvas.View.CenterX, 6);
                }
            );
        }

        [Fact]
        public void TheWheelAndAPanThroughAViewport_GoOnItsOwnZoomPreviousList_ThePaperNotMoved()
        {
            OnLayout(
                (model, canvas, viewport) =>
                {
                    model.Document.EnterViewport(viewport);
                    canvas.RenderNow();
                    double paperZoom = canvas.View.PixelsPerUnit;
                    Point at = At(canvas, 150, 100);

                    Mouse(canvas, UIElement.MouseWheelEvent, at, delta: 120);
                    canvas.RenderNow();
                    Drag(canvas, new Point(400, 300), new Point(440, 300), MouseButton.Middle);
                    canvas.RenderNow();
                    double centerX = Frame(model, viewport).ViewCenter.X;
                    Assert.NotEqual(2000, centerX, 6);

                    ZoomPrevious(model);
                    Assert.Equal(2000, Frame(model, viewport).ViewCenter.X, 6);
                    Assert.Equal(3000 / 1.2, Frame(model, viewport).ViewHeight, 6);
                    ZoomPrevious(model);
                    Assert.Equal(3000, Frame(model, viewport).ViewHeight, 6);
                    ZoomPrevious(model);
                    Assert.Equal("No previous view saved.", model.Session.History.Last());
                    Assert.Equal(paperZoom, canvas.View.PixelsPerUnit, 9);
                }
            );
        }

        [Fact]
        public void ADoubleClickOfTheMiddleButton_IsZoomExtentsOfThePaper_ForZoomPreviousAndUndo()
        {
            OnLayout(
                (model, canvas, viewport) =>
                {
                    Point at = At(canvas, 150, 100);
                    Mouse(canvas, UIElement.MouseWheelEvent, at, delta: 120);
                    Mouse(canvas, UIElement.MouseWheelEvent, at, delta: 120);
                    canvas.RenderNow();
                    double zoomed = canvas.View.PixelsPerUnit;
                    CadUndoStep wheel = model.Document.NextUndo;

                    DoubleClickMiddle(canvas, new Point(300, 200));
                    canvas.RenderNow();
                    Assert.True(canvas.View.PixelsPerUnit < zoomed);
                    Assert.NotSame(wheel, model.Document.NextUndo);
                    Assert.Equal("ZOOM", model.Document.NextUndo.Name);

                    ZoomPrevious(model);
                    Assert.Equal(zoomed, canvas.View.PixelsPerUnit, 9);
                }
            );
        }

        [Fact]
        public void ADoubleClickOfTheMiddleButtonThroughAViewport_IsItsZoomExtents_ForZoomPrevious()
        {
            OnLayout(
                (model, canvas, viewport) =>
                {
                    model.Document.EnterViewport(viewport);
                    canvas.RenderNow();
                    Mouse(canvas, UIElement.MouseWheelEvent, At(canvas, 150, 100), delta: 120);
                    canvas.RenderNow();
                    Assert.Equal(3000 / 1.2, Frame(model, viewport).ViewHeight, 6);

                    // The room fits the viewport at 3 000 high: the double click shows all of it again.
                    DoubleClickMiddle(canvas, new Point(300, 200));
                    canvas.RenderNow();
                    Assert.Equal(3000, Frame(model, viewport).ViewHeight, 6);

                    ZoomPrevious(model);
                    Assert.Equal(3000 / 1.2, Frame(model, viewport).ViewHeight, 6);
                    Assert.Equal(2000, Frame(model, viewport).ViewCenter.X, 6);
                }
            );
        }

        [Fact]
        public void Zoomwheel_TurnsTheWheelsDirection_OnThePaperAndThroughAViewport_AndIsKeptInTheSettingsFile()
        {
            OnLayout(
                (model, canvas, viewport) =>
                {
                    CadUndoStep shown = model.Document.NextUndo;
                    model.Execute("ZOOMWHEEL");
                    Assert.Equal("Enter new value for ZOOMWHEEL <0>:", model.Prompt);
                    model.Execute("1");
                    Assert.True(model.Settings.WheelZoomReversed);
                    Assert.Same(shown, model.Document.NextUndo);

                    // The window gives the canvas the settings; forward now zooms out, on the paper and through a viewport.
                    canvas.WheelReversed = model.Settings.WheelZoomReversed;
                    double zoom = canvas.View.PixelsPerUnit;
                    Mouse(canvas, UIElement.MouseWheelEvent, At(canvas, 150, 100), delta: 120);
                    canvas.RenderNow();
                    Assert.Equal(zoom / 1.2, canvas.View.PixelsPerUnit, 9);

                    model.Document.EnterViewport(viewport);
                    Mouse(canvas, UIElement.MouseWheelEvent, At(canvas, 150, 100), delta: 120);
                    canvas.RenderNow();
                    Assert.Equal(3000 * 1.2, Frame(model, viewport).ViewHeight, 6);

                    string path = System.IO.Path.Combine(
                        System.IO.Path.GetTempPath(),
                        "gh-zoomwheel-" + Guid.NewGuid().ToString("N") + ".json"
                    );

                    try
                    {
                        model.Settings.Save(path);
                        Assert.True(GlobalSettings.Load(path).WheelZoomReversed);
                    }
                    finally
                    {
                        System.IO.File.Delete(path);
                    }
                }
            );
        }

        [Fact]
        public void TheWheelInModelSpace_IsAUndoStepAndAnEntryOfModelSpacesZoomPreviousList()
        {
            OnLayout(
                (model, canvas, viewport) =>
                {
                    double zoom = canvas.View.PixelsPerUnit;

                    Mouse(canvas, UIElement.MouseWheelEvent, new Point(300, 200), delta: 120);
                    canvas.RenderNow();

                    Assert.Equal(zoom * 1.2, canvas.View.PixelsPerUnit, 9);
                    Assert.Equal("ZOOM", model.Document.NextUndo.Name);

                    ZoomPrevious(model);
                    Assert.Equal(zoom, canvas.View.PixelsPerUnit, 9);
                    Assert.True(model.Document.Undo());
                    Assert.Equal(zoom * 1.2, canvas.View.PixelsPerUnit, 9);
                    Assert.True(model.Document.Undo());
                    Assert.Equal(zoom, canvas.View.PixelsPerUnit, 9);
                },
                model: true
            );
        }

        [Fact]
        public void TheUndoButton_RecordsAWheelGestureNotYetRecorded_ThenTakesItBack_AsCtrlZDoes()
        {
            OnLayout(
                (model, canvas, viewport) =>
                {
                    model.CommitPendingView = canvas.CommitView;
                    double zoom = canvas.View.PixelsPerUnit;
                    CadUndoStep start = model.Document.NextUndo;

                    // The wheel turned, and the Undo button pressed within the 80 milliseconds the canvas waits for the view to be still.
                    Mouse(canvas, UIElement.MouseWheelEvent, new Point(300, 200), delta: 120);
                    Assert.Equal(zoom * 1.2, canvas.View.PixelsPerUnit, 9);
                    Assert.Same(start, model.Document.NextUndo);

                    model.UndoCommand.Execute(null);

                    Assert.Equal(zoom, canvas.View.PixelsPerUnit, 9);
                    Assert.Same(start, model.Document.NextUndo);
                    Assert.True(model.Document.CanRedo);

                    model.RedoCommand.Execute(null);
                    Assert.Equal(zoom * 1.2, canvas.View.PixelsPerUnit, 9);
                },
                model: true
            );
        }
    }
}
