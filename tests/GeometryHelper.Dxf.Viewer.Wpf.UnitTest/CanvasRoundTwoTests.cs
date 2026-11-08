using System;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>A viewport's corner grips dragged on the canvas, and the grid's dots drawn under the drawing, offscreen on an STA thread.</summary>
    public class CanvasRoundTwoTests
    {
        private const int Width = 800;
        private const int Height = 600;

        private static void OnCanvas(bool layout, Action<MainViewModel, CadCanvas, string> test)
        {
            Exception failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    using (var folder = new TestFolder())
                    {
                        var model = new MainViewModel();
                        model.Open(folder.Write("room.dxf", TestFiles.RoomWithViewport(folder, out _, out string viewport)));

                        if (layout)
                        {
                            model.SelectSpace(model.Document.Spaces.Single(s => s.Name == "Layout1"));
                        }

                        var canvas = new CadCanvas { Document = model.Document, Space = model.Space, View = model.View, Session = model.Session, Snap = model.Snap };
                        canvas.PixelSizeChanged += (s, e) => model.SetCanvasSize(canvas.PixelWidth, canvas.PixelHeight);
                        canvas.Measure(new Size(Width, Height));
                        canvas.Arrange(new Rect(0, 0, Width, Height));
                        canvas.RenderNow();
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

        [Fact]
        public void ASelectedViewportShowsItsFourCornersAndMiddleAndDraggingACornerResizesItsFrame()
        {
            OnCanvas(true, (model, canvas, viewport) =>
            {
                model.Document.Selection.Set(new[] { viewport });
                canvas.RenderNow();

                Assert.Equal(5, canvas.GripCount);

                // The upper right corner, (250, 175), dragged to (270, 190): the lower left kept, the frame 220 by 165.
                model.Toggles.Single(t => t.Name == "OSNAP").IsOn = false;
                CanvasMouseTests.Drag(canvas, CanvasMouseTests.At(canvas, 250, 175), CanvasMouseTests.At(canvas, 270, 190));

                var frame = (DxfViewport)model.Document.FindEntity(viewport);
                Assert.Equal(220.0, frame.Width, 6);
                Assert.Equal(165.0, frame.Height, 6);
                Assert.True(model.HasUnsavedChanges);
            });
        }

        [Fact]
        public void TheGridIsDrawnAsLinesUnderTheDrawingEveryFifthStrongerAndNoneCloserThanSixPixels()
        {
            OnCanvas(false, (model, canvas, viewport) =>
            {
                canvas.ShowGrid = true;
                canvas.RenderNow();
                canvas.UpdateLayout();
                var bitmap = new RenderTargetBitmap(Width, Height, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(canvas);
                var pixels = new int[Width * Height];
                bitmap.CopyPixels(pixels, Width * 4, 0);

                // A grid of 10 at 0.18 pixels to the unit is drawn every 50, 9 pixels apart; every fifth line, from the origin, is major.
                double step = CadCanvas.GridStep(canvas.View.PixelsPerUnit, 10, 6);
                Assert.Equal(50.0, step);

                int Pixel(double x, double y)
                {
                    canvas.View.DrawingToPixel(x, y, out double px, out double py);
                    return pixels[(int)Math.Round(py) * Width + (int)Math.Round(px)] & 0xFFFFFF;
                }

                // Minor lines 8 per cent lighter than the black background, the major 18 per cent, all along the canvas.
                int minor = pixels.Count(p => (p & 0xFFFFFF) == 0x141414);
                Assert.True(minor > 10000, minor + " pixels of minor lines");

                // Between two horizontal lines, at x 1 000 (the twentieth line, major) and 1 050 (minor); between both, black.
                Assert.Equal(0x2D2D2D, Pixel(1000, 1025));
                Assert.Equal(0x141414, Pixel(1050, 1025));
                Assert.Equal(0x000000, Pixel(1025, 1025));

                // Where the bottom wall is drawn, the line goes under it: the pixel stays the wall's.
                Assert.NotEqual(0x141414, Pixel(1050, 0));
            });
        }
    }
}
