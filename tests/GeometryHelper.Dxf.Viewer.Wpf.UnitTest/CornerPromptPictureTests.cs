using System;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// The canvas at a prompt for the opposite corner of a box: STRETCH draws the box of "Select objects:" (a fill and an
    /// edge, no line from the first corner to the cursor, as the maintainer's picture of AutoCAD shows), ZOOM Window a
    /// rectangle and no line either. The room has nothing inside the box from (500, 2 500) to (1 500, 500).
    /// </summary>
    public sealed class CornerPromptPictureTests
    {
        private const int Width = 800;
        private const int Height = 600;

        /// <summary>Runs a test on an STA thread with the room open in a view model and a canvas of 800 by 600 over it.</summary>
        private static void OnCanvas(Action<MainViewModel, CadCanvas> test)
        {
            Exception failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    using (var folder = new TestFolder())
                    {
                        var model = new MainViewModel();
                        Assert.True(model.Open(folder.Write("room.dxf", TestFiles.Room(out _, out _))));
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
                        test(model, canvas);
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

        private static int[] Picture(CadCanvas canvas)
        {
            canvas.RenderNow();
            canvas.UpdateLayout();
            var bitmap = new RenderTargetBitmap(Width, Height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(canvas);
            var pixels = new int[Width * Height];
            bitmap.CopyPixels(pixels, Width * 4, 0);
            return pixels.Select(p => p & 0xFFFFFF).ToArray();
        }

        private static int At(int[] pixels, int x, int y) => pixels[y * Width + x];

        /// <summary>The pixels of the diagonal from the first corner to the cursor, away from both ends.</summary>
        private static int[] Diagonal(int[] pixels, double ax, double ay, double bx, double by) =>
            Enumerable
                .Range(10, 81)
                .Select(i => i / 100.0)
                .Select(t => At(pixels, (int)Math.Round(ax + t * (bx - ax)), (int)Math.Round(ay + t * (by - ay))))
                .ToArray();

        [Fact]
        public void StretchAskingItsOppositeCorner_DrawsTheBoxOfSelectObjects_AndNoLineFromTheFirstCorner()
        {
            OnCanvas(
                (model, canvas) =>
                {
                    model.SnapModes = CadSnapModes.None;
                    model.Execute("STRETCH");
                    model.Execute("500,2500");
                    Assert.StartsWith("Specify opposite corner", model.Prompt);
                    canvas.View.DrawingToPixel(500, 2500, out double ax, out double ay);
                    canvas.View.DrawingToPixel(1500, 500, out double bx, out double by);
                    CanvasMouseTests.Mouse(canvas, UIElement.MouseMoveEvent, new Point(bx, by));
                    int[] pixels = Picture(canvas);

                    // Left to right a window: one fill all along the diagonal, which is not the colour of the room outside it.
                    int[] diagonal = Diagonal(pixels, ax, ay, bx, by);
                    Assert.Single(diagonal.Distinct());
                    int outside = At(pixels, (int)Math.Round(Math.Max(ax, bx)) + 40, (int)Math.Round((ay + by) / 2));
                    Assert.NotEqual(outside, diagonal[0]);
                    model.Escape();
                }
            );
        }

        [Fact]
        public void ZoomWindowAskingItsOppositeCorner_DrawsARectangle_AndNoLineFromTheFirstCorner()
        {
            OnCanvas(
                (model, canvas) =>
                {
                    model.SnapModes = CadSnapModes.None;
                    model.Execute("ZOOM");
                    model.Execute("W");
                    model.Execute("500,2500");
                    Assert.StartsWith("Specify opposite corner", model.Prompt);
                    canvas.View.DrawingToPixel(500, 2500, out double ax, out double ay);
                    canvas.View.DrawingToPixel(1500, 500, out double bx, out double by);
                    CanvasMouseTests.Mouse(canvas, UIElement.MouseMoveEvent, new Point(bx, by));
                    int[] pixels = Picture(canvas);

                    // Inside the rectangle nothing is drawn, the diagonal included; its top edge has the rubber pen's ink.
                    int[] diagonal = Diagonal(pixels, ax, ay, bx, by);
                    Assert.Single(diagonal.Distinct());
                    int inside = diagonal[0];
                    int[] edge = Enumerable
                        .Range((int)Math.Round(ax) + 5, (int)Math.Round(bx - ax) - 10)
                        .SelectMany(x =>
                            new[] { At(pixels, x, (int)Math.Round(ay)), At(pixels, x, (int)Math.Round(ay) - 1) }
                        )
                        .ToArray();
                    Assert.Contains(edge, p => p != inside);
                    model.Escape();
                }
            );
        }
    }
}
