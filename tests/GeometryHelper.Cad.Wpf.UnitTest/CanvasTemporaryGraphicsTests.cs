using System;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GeometryHelper.UnitTest;
using Xunit;

namespace GeometryHelper.Cad.Wpf.UnitTest
{
    /// <summary>
    /// The vectors AutoLISP's <c>grdraw</c> and <c>grvecs</c> put on the screen, drawn by the canvas over its picture of the
    /// drawing, offscreen on an STA thread: in the colour of the index, a pixel wide, and gone after REDRAW or <c>grclear</c>.
    /// </summary>
    public class CanvasTemporaryGraphicsTests
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
                        model.Open(folder.Write("room.dxf", TestFiles.Room(out string bottom, out string column)));
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

        /// <summary>The canvas as the screen shows it, as 0xRRGGBB by row.</summary>
        private static int[] Pixels(CadCanvas canvas)
        {
            canvas.UpdateLayout();
            var bitmap = new RenderTargetBitmap(Width, Height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(canvas);
            var pixels = new int[Width * Height];
            bitmap.CopyPixels(pixels, Width * 4, 0);

            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] &= 0xFFFFFF;
            }

            return pixels;
        }

        /// <summary>
        /// Whether a pixel on or next to the screen point of a drawing point shows a primary colour: each channel of the colour
        /// that is full at 100 or more and each that is none at 60 or less. A line a pixel wide drawn on the border of two rows
        /// is half in each of them.
        /// </summary>
        private static bool Shows(CadCanvas canvas, int[] pixels, double x, double y, int rgb)
        {
            Point at = CanvasMouseTests.At(canvas, x, y);

            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    int px = (int)Math.Round(at.X) + dx;
                    int py = (int)Math.Round(at.Y) + dy;

                    if (px < 0 || py < 0 || px >= Width || py >= Height)
                    {
                        continue;
                    }

                    int found = pixels[py * Width + px];
                    bool tints = true;

                    for (int shift = 16; shift >= 0; shift -= 8)
                    {
                        int channel = (found >> shift) & 0xFF;
                        tints &= ((rgb >> shift) & 0xFF) == 0xFF ? channel >= 100 : channel <= 60;
                    }

                    if (tints)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        [Fact]
        public void AVectorOfGrdraw_IsDrawnInTheColourOfItsIndex_AndGoneAfterRedraw()
        {
            OnCanvas(
                (model, canvas) =>
                {
                    Assert.False(Shows(canvas, Pixels(canvas), 1000, 300, 0xFF0000));

                    model.Session.Feed("(grdraw '(500 300) '(1500 300) 1)");
                    canvas.RenderNow();

                    // Colour 1 is red, the middle of the vector is on the screen.
                    Assert.True(Shows(canvas, Pixels(canvas), 1000, 300, 0xFF0000));

                    model.Session.Feed("(redraw)");
                    canvas.RenderNow();

                    Assert.False(Shows(canvas, Pixels(canvas), 1000, 300, 0xFF0000));
                }
            );
        }

        [Fact]
        public void TheVectorsOfGrvecs_AreDrawnEachInItsColour_AndGoneAfterGrclear()
        {
            OnCanvas(
                (model, canvas) =>
                {
                    model.Session.Feed("(grvecs '(3 (500 300) (1500 300) 5 (500 350) (1500 350)))");
                    canvas.RenderNow();
                    int[] pixels = Pixels(canvas);

                    // Colour 3 is green, 5 blue.
                    Assert.True(Shows(canvas, pixels, 1000, 300, 0x00FF00));
                    Assert.True(Shows(canvas, pixels, 1000, 350, 0x0000FF));

                    model.Session.Feed("(grclear)");
                    canvas.RenderNow();
                    pixels = Pixels(canvas);

                    Assert.False(Shows(canvas, pixels, 1000, 300, 0x00FF00));
                    Assert.False(Shows(canvas, pixels, 1000, 350, 0x0000FF));
                }
            );
        }

        [Fact]
        public void RegenTakesTheVectorsOffAsRedrawDoes()
        {
            OnCanvas(
                (model, canvas) =>
                {
                    model.Session.Feed("(grdraw '(500 300) '(1500 300) 1)");
                    canvas.RenderNow();
                    Assert.True(Shows(canvas, Pixels(canvas), 1000, 300, 0xFF0000));

                    model.Session.Feed("REGEN");
                    canvas.RenderNow();

                    Assert.False(Shows(canvas, Pixels(canvas), 1000, 300, 0xFF0000));
                }
            );
        }
    }
}
