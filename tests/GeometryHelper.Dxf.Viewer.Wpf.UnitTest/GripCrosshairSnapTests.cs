using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// The crosshair pulled onto a grip, as AutoCAD 2021 does: while no command runs and the cursor is inside the box of a
    /// grip (its square, or the bar of a polyline's segment middle), the crosshair is drawn with its centre on the grip's point
    /// and the status bar shows that point; outside the box it is under the pointer again. The pointer of the operating system
    /// is never moved.
    /// </summary>
    public sealed class GripCrosshairSnapTests
    {
        private const int Width = 800;
        private const int Height = 600;

        /// <summary>
        /// Runs a test on an STA thread: an open polyline (0, 0), (100, 0), (100, 100) selected at handle "P" under a canvas of
        /// 800 by 600 showing -20,-20 to 120,120, its crosshair 100 per cent of the canvas and turned 20 degrees, so that its arms
        /// are slanted and show in the pixels.
        /// </summary>
        private static void OnElbow(Action<MainViewModel, CadCanvas> test)
        {
            Exception failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    var database = new DxfDatabase("AC1027");
                    var polyline = new DxfPolyline();
                    polyline.AddVertexAt(0, new GeoPoint2(0, 0), 0, 0, 0);
                    polyline.AddVertexAt(1, new GeoPoint2(100, 0), 0, 0, 0);
                    polyline.AddVertexAt(2, new GeoPoint2(100, 100), 0, 0, 0);
                    database.ModelSpace.AppendEntity(polyline);
                    var model = new MainViewModel();
                    model.OpenDocument(CadDocument.From(database, "grips.dxf"), "grips.dxf");
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
                    canvas.View.ZoomTo(new DisplayBox(-20, -20, 120, 120));
                    canvas.Options.SelectionColor = 0xD29922;
                    canvas.CrosshairPercent = 100;
                    canvas.SnapAngle = Math.PI / 9;
                    model.Document.Selection.Set(new[] { polyline.Handle });
                    canvas.RenderNow();
                    test(model, canvas);
                    canvas.Dispose();
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

        private static Point At(CadCanvas canvas, double x, double y) => CanvasMouseTests.At(canvas, x, y);

        private static void Hover(CadCanvas canvas, Point at) =>
            CanvasMouseTests.Mouse(canvas, UIElement.MouseMoveEvent, at);

        private static int[] Pixels(CadCanvas canvas)
        {
            canvas.RenderNow();
            canvas.UpdateLayout();
            var bitmap = new RenderTargetBitmap(Width, Height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(canvas);
            var pixels = new int[Width * Height];
            bitmap.CopyPixels(pixels, Width * 4, 0);
            return pixels;
        }

        /// <summary>The point 100 pixels from a centre along the crosshair's arm that runs at 20 degrees.</summary>
        private static Point Arm(Point centre)
        {
            double angle = 20.0 * Math.PI / 180.0;
            return new Point(centre.X + 100.0 * Math.Cos(angle), centre.Y - 100.0 * Math.Sin(angle));
        }

        /// <summary>Whether a pixel within one of a point is as light as the crosshair, which is light on the dark background.</summary>
        private static bool Light(int[] pixels, Point at)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    int pixel = pixels[((int)Math.Round(at.Y) + dy) * Width + (int)Math.Round(at.X) + dx] & 0xFFFFFF;

                    if (((pixel >> 16) & 0xFF) > 0xA0 && ((pixel >> 8) & 0xFF) > 0xA0 && (pixel & 0xFF) > 0xA0)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        [Fact]
        public void ThePointerInsideAGripsSquare_DrawsTheCrosshairOnTheGripsPoint()
        {
            OnElbow(
                (model, canvas) =>
                {
                    Point grip = At(canvas, 100, 100);
                    Point pointer = new Point(grip.X + 5.0, grip.Y + 5.0);
                    Hover(canvas, pointer);
                    int[] pixels = Pixels(canvas);

                    Assert.True(Light(pixels, Arm(grip)));
                    Assert.False(Light(pixels, Arm(pointer)));
                }
            );
        }

        [Fact]
        public void ThePointerOutsideTheSquare_KeepsTheCrosshairUnderIt()
        {
            OnElbow(
                (model, canvas) =>
                {
                    Point grip = At(canvas, 100, 100);
                    Point pointer = new Point(grip.X + 20.0, grip.Y + 20.0);
                    Hover(canvas, pointer);
                    int[] pixels = Pixels(canvas);

                    Assert.True(Light(pixels, Arm(pointer)));
                    Assert.False(Light(pixels, Arm(grip)));
                }
            );
        }

        [Fact]
        public void TheCrosshairLeavesTheGrip_WhenThePointerMovesOutOfTheSquare()
        {
            OnElbow(
                (model, canvas) =>
                {
                    Point grip = At(canvas, 100, 0);
                    Hover(canvas, new Point(grip.X - 4.0, grip.Y + 6.0));
                    Assert.True(Light(Pixels(canvas), Arm(grip)));

                    Point away = new Point(grip.X - 4.0, grip.Y + 30.0);
                    Hover(canvas, away);
                    int[] pixels = Pixels(canvas);
                    Assert.True(Light(pixels, Arm(away)));
                    Assert.False(Light(pixels, Arm(grip)));
                }
            );
        }

        [Fact]
        public void ThePointerOnTheBarOfASegmentMiddle_DrawsTheCrosshairOnTheBarsPoint()
        {
            OnElbow(
                (model, canvas) =>
                {
                    // The bar of the middle of the bottom segment is 22 pixels long by 15 across: 10 pixels along it is inside the
                    // bar though outside the 7.5 of a square.
                    Point middle = At(canvas, 50, 0);
                    Point pointer = new Point(middle.X + 10.0, middle.Y + 2.0);
                    Hover(canvas, pointer);
                    int[] pixels = Pixels(canvas);

                    Assert.True(Light(pixels, Arm(middle)));
                    Assert.False(Light(pixels, Arm(pointer)));
                }
            );
        }

        [Fact]
        public void TheStatusBarShowsTheGripsPoint_WhileThePointerIsInsideItsSquare()
        {
            OnElbow(
                (model, canvas) =>
                {
                    var seen = new List<CadCursorEventArgs>();
                    canvas.CursorMoved += (s, e) => seen.Add(e);
                    Point grip = At(canvas, 100, 100);

                    Hover(canvas, new Point(grip.X + 4.0, grip.Y - 3.0));
                    CadCursorEventArgs on = seen[seen.Count - 1];
                    Assert.Equal(100.0, on.X, 6);
                    Assert.Equal(100.0, on.Y, 6);
                    Assert.Equal(100.0, on.BandX, 6);
                    Assert.Equal(100.0, on.BandY, 6);

                    Hover(canvas, new Point(grip.X + 40.0, grip.Y - 3.0));
                    CadCursorEventArgs off = seen[seen.Count - 1];
                    Assert.True(off.X > 100.0 + 5.0);
                }
            );
        }

        [Fact]
        public void WhileACommandRuns_TheCrosshairStaysUnderThePointer()
        {
            OnElbow(
                (model, canvas) =>
                {
                    Point grip = At(canvas, 100, 100);
                    model.Execute("LINE");
                    Point pointer = new Point(grip.X + 5.0, grip.Y + 5.0);
                    Hover(canvas, pointer);
                    int[] pixels = Pixels(canvas);

                    Assert.True(Light(pixels, Arm(pointer)));
                    Assert.False(Light(pixels, Arm(grip)));
                }
            );
        }
    }
}
