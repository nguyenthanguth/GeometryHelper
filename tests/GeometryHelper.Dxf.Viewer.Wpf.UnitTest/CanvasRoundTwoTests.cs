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
        public void TheGridsDotsAreDrawnLighterThanTheBackgroundAndOnlyWhereNothingIsDrawn()
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
                double step = CadCanvas.GridStep(canvas.View.PixelsPerUnit, 10, 8);

                int Pixel(double x, double y)
                {
                    canvas.View.DrawingToPixel(x, y, out double px, out double py);
                    return pixels[(int)Math.Round(py) * Width + (int)Math.Round(px)] & 0xFFFFFF;
                }

                int dots = pixels.Count(p => (p & 0xFFFFFF) == 0x333333);
                Assert.True(dots > 1000, dots + " dots");

                // A grid point inside the room, where nothing is drawn, is a dot; one on the bottom wall stays the wall's.
                Assert.Equal(0x333333, Pixel(step * Math.Round(1000 / step), step * Math.Round(1000 / step)));
                Assert.NotEqual(0x333333, Pixel(step * Math.Round(1000 / step), 0));

                // Between two grid points the background stays black.
                Assert.Equal(0x000000, Pixel(step * Math.Round(1000 / step) + step / 2, step * Math.Round(1000 / step) + step / 2));
            });
        }
    }
}
