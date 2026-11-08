using System;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// The canvas driven by mouse events raised on it offscreen, on an STA thread, the pointer put where the test says: the
    /// wheel, panning, picking, a crossing box, grips, and a command answered by clicks.
    /// </summary>
    public class CanvasMouseTests
    {
        private const int Width = 800;
        private const int Height = 600;

        /// <summary>Runs a test on an STA thread with the room open in a view model and a canvas of 800 by 600 over it.</summary>
        private static void OnCanvas(Action<MainViewModel, CadCanvas, string, string> test)
        {
            Exception failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    using (var folder = new TestFolder())
                    {
                        var model = new MainViewModel();
                        model.Open(folder.Write("room.dxf", TestFiles.Room(out string bottom, out string column)));
                        var canvas = new CadCanvas { Document = model.Document, Space = model.Space, View = model.View, Session = model.Session, Snap = model.Snap };
                        canvas.PixelSizeChanged += (s, e) => model.SetCanvasSize(canvas.PixelWidth, canvas.PixelHeight);
                        canvas.Measure(new Size(Width, Height));
                        canvas.Arrange(new Rect(0, 0, Width, Height));
                        canvas.RenderNow();
                        test(model, canvas, bottom, column);
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

        internal static Point At(CadCanvas canvas, double x, double y)
        {
            canvas.View.DrawingToPixel(x, y, out double px, out double py);
            return new Point(px, py);
        }

        internal static void Mouse(CadCanvas canvas, RoutedEvent routed, Point at, MouseButton button = MouseButton.Left, int delta = 0)
        {
            canvas.Pointer = _ => at;
            MouseEventArgs args;

            if (routed == UIElement.MouseWheelEvent)
            {
                args = new MouseWheelEventArgs(System.Windows.Input.Mouse.PrimaryDevice, 0, delta);
            }
            else if (routed == UIElement.MouseMoveEvent)
            {
                args = new MouseEventArgs(System.Windows.Input.Mouse.PrimaryDevice, 0);
            }
            else
            {
                args = new MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice, 0, button);
            }

            args.RoutedEvent = routed;
            canvas.RaiseEvent(args);
        }

        internal static void Click(CadCanvas canvas, Point at, MouseButton button = MouseButton.Left)
        {
            Mouse(canvas, UIElement.MouseMoveEvent, at);
            Mouse(canvas, UIElement.MouseDownEvent, at, button);
            Mouse(canvas, UIElement.MouseUpEvent, at, button);
        }

        internal static void Drag(CadCanvas canvas, Point from, Point to, MouseButton button = MouseButton.Left)
        {
            Mouse(canvas, UIElement.MouseMoveEvent, from);
            Mouse(canvas, UIElement.MouseDownEvent, from, button);
            Mouse(canvas, UIElement.MouseMoveEvent, new Point((from.X + to.X) / 2, (from.Y + to.Y) / 2));
            Mouse(canvas, UIElement.MouseMoveEvent, to);
            Mouse(canvas, UIElement.MouseUpEvent, to, button);
        }

        [Fact]
        public void TheWheelZoomsAboutTheCursorAndTheMiddleButtonPans()
        {
            OnCanvas((model, canvas, bottom, column) =>
            {
                CadView view = canvas.View;
                double zoom = view.PixelsPerUnit;
                view.ScreenToDrawing(300, 200, out double x, out double y);

                Mouse(canvas, UIElement.MouseWheelEvent, new Point(300, 200), delta: 120);

                Assert.Equal(zoom * 1.2, view.PixelsPerUnit, 12);
                view.ScreenToDrawing(300, 200, out double x1, out double y1);
                Assert.Equal(x, x1, 6);
                Assert.Equal(y, y1, 6);

                Point before = At(canvas, 1000, 1000);
                Drag(canvas, new Point(100, 100), new Point(150, 130), MouseButton.Middle);
                Point after = At(canvas, 1000, 1000);

                Assert.Equal(before.X + 50, after.X, 6);
                Assert.Equal(before.Y + 30, after.Y, 6);
            });
        }

        [Fact]
        public void AClickPicksTheLineItsGripsShowAndDraggingAnEndGripMovesThatEnd()
        {
            OnCanvas((model, canvas, bottom, column) =>
            {
                Click(canvas, At(canvas, 1000, 0));

                Assert.Equal(new[] { bottom }, model.Document.Selection.Handles);
                Assert.Equal("Line (1)", model.SelectionLabel);

                canvas.RenderNow();

                // A line has its two ends and its middle.
                Assert.Equal(3, canvas.GripCount);

                Drag(canvas, At(canvas, 0, 0), At(canvas, 0, -500));

                var line = (DxfLine)model.Document.FindEntity(bottom);
                Assert.Equal(0.0, line.StartPoint.X, 9);
                Assert.Equal(-500.0, line.StartPoint.Y, 9);
                Assert.Equal(4000.0, line.EndPoint.X, 9);
                Assert.True(model.HasUnsavedChanges);
            });
        }

        [Fact]
        public void ABoxDraggedRightToLeftIsACrossingThatTakesWhatItTouches()
        {
            OnCanvas((model, canvas, bottom, column) =>
            {
                Drag(canvas, At(canvas, 2150, 1450), At(canvas, 1950, 1550));

                Assert.Equal(new[] { column }, model.Document.Selection.Handles);

                // The same box left to right is a window: the circle is not wholly inside it.
                model.ClearSelection();
                Drag(canvas, At(canvas, 1950, 1450), At(canvas, 2150, 1550));

                Assert.Empty(model.Document.Selection.Handles);
            });
        }

        [Fact]
        public void MoveIsAnsweredByClicksOnTheCanvasTheBaseSnappedToTheLinesEnd()
        {
            OnCanvas((model, canvas, bottom, column) =>
            {
                Click(canvas, At(canvas, 1000, 0));
                model.StartCommand("MOVE");

                Mouse(canvas, UIElement.MouseMoveEvent, At(canvas, 8, 6));

                Assert.Equal(CadSnapKind.Endpoint, canvas.CurrentSnap.Kind);

                Click(canvas, At(canvas, 8, 6));
                Click(canvas, At(canvas, 0, 1000));

                var line = (DxfLine)model.Document.FindEntity(bottom);
                Assert.Equal(1000.0, line.StartPoint.Y, 9);
                Assert.False(model.IsCommandRunning);
            });
        }

        [Fact]
        public void ARightClickIsEnterAndEndsALine()
        {
            OnCanvas((model, canvas, bottom, column) =>
            {
                int before = model.Document.Database.ModelSpace.Cast<DxfEntity>().Count();
                model.StartCommand("LINE");

                Click(canvas, At(canvas, 500, 500));
                Click(canvas, At(canvas, 800, 900));
                Click(canvas, At(canvas, 800, 900), MouseButton.Right);

                Assert.False(model.IsCommandRunning);
                Assert.Equal(before + 1, model.Document.Database.ModelSpace.Cast<DxfEntity>().Count());
            });
        }
    }
}
