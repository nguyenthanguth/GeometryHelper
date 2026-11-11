using System;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// The grid drawn by GRIDSTYLE and GRIDDISPLAY, as AutoCAD 2021's help describes them: dots where the lines would cross in
    /// the spaces GRIDSTYLE names, the grid kept within the limits without GRIDDISPLAY 1, none too dense to show without 2,
    /// and lines added between as the view zooms in with 4.
    /// </summary>
    public class GridStyleCanvasTests
    {
        private const int Width = 800;
        private const int Height = 600;
        private const int Minor = 0x141414;
        private const int Major = 0x2D2D2D;

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
                        model.Open(folder.Write("room.dxf", TestFiles.Room(out _, out _)));
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
                        canvas.ShowGrid = true;
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

        /// <summary>Whether a pixel within one of a point of the canvas is of a colour.</summary>
        private static bool Near(int[] pixels, CadCanvas canvas, GeoPoint2 world, int rgb)
        {
            canvas.View.DrawingToPixel(world.X, world.Y, out double px, out double py);

            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    int x = (int)Math.Round(px) + dx;
                    int y = (int)Math.Round(py) + dy;

                    if ((pixels[y * Width + x] & 0xFFFFFF) == rgb)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool Lined(int[] pixels, CadCanvas canvas, GeoPoint2 world) =>
            Near(pixels, canvas, world, Minor) || Near(pixels, canvas, world, Major);

        /// <summary>The spacing the adaptive grid is drawn at: the spacing, then the major lines, then 2, 5, 2 and on times, 6 pixels apart at least.</summary>
        private static double Drawn(CadCanvas canvas, double spacing)
        {
            double step = spacing;

            for (int i = 0; step * canvas.View.PixelsPerUnit < 6; i++)
            {
                step *=
                    i == 0 ? 5
                    : i % 2 == 0 ? 5
                    : 2;
            }

            return step;
        }

        [Fact]
        public void GridStyle1_DrawsDotsInModelSpace_WhereTheLinesWouldCross()
        {
            OnCanvas(
                (model, canvas) =>
                {
                    double step = Drawn(canvas, 10);
                    double x = Math.Round(1000 / (5 * step)) * 5 * step;
                    Assert.True(Near(Pixels(canvas), canvas, new GeoPoint2(x + step, x + step / 2), Minor));

                    canvas.GridStyle = 1;
                    int[] pixels = Pixels(canvas);
                    Assert.True(Near(pixels, canvas, new GeoPoint2(x + step, x + step), Major));
                    Assert.False(Lined(pixels, canvas, new GeoPoint2(x + step, x + step / 2)));

                    // The dots of the Block Editor and the layouts leave model space in lines.
                    canvas.GridStyle = 6;
                    Assert.True(Near(Pixels(canvas), canvas, new GeoPoint2(x + step, x + step / 2), Minor));
                }
            );
        }

        [Fact]
        public void GridDisplayWithout1_KeepsTheGridWithinTheLimits()
        {
            OnCanvas(
                (model, canvas) =>
                {
                    double step = Drawn(canvas, 10);
                    double limit = Math.Round(1000 / (5 * step)) * 5 * step;
                    model.Document.Database.Limmin = new GeoPoint2(0, 0);
                    model.Document.Database.Limmax = new GeoPoint2(limit, limit);
                    var inside = new GeoPoint2(limit - step, limit - step / 2);
                    var outside = new GeoPoint2(limit + step, limit - step / 2);
                    int[] beyond = Pixels(canvas);
                    Assert.True(Lined(beyond, canvas, inside));
                    Assert.True(Lined(beyond, canvas, outside));

                    canvas.GridDisplay = 2;
                    int[] within = Pixels(canvas);
                    Assert.True(Lined(within, canvas, inside));
                    Assert.False(Lined(within, canvas, outside));
                }
            );
        }

        [Fact]
        public void GridDisplayWithout2_ShowsNoGridTooDenseToDisplay()
        {
            OnCanvas(
                (model, canvas) =>
                {
                    canvas.GridSpacing = 0.01;
                    canvas.GridSpacingY = 0.01;
                    double step = Drawn(canvas, 0.01);
                    double x = Math.Round(1000 / (5 * step)) * 5 * step;
                    var on = new GeoPoint2(x, x + step / 2);
                    Assert.True(Lined(Pixels(canvas), canvas, on));

                    canvas.GridDisplay = 1;
                    Assert.False(Lined(Pixels(canvas), canvas, on));
                }
            );
        }

        [Fact]
        public void GridDisplay4_AddsLinesBetweenAsTheViewZoomsIn()
        {
            OnCanvas(
                (model, canvas) =>
                {
                    // A grid 60 pixels apart: with 4 a line every fifth of it, 12 pixels apart.
                    double spacing = 60 / canvas.View.PixelsPerUnit;
                    canvas.GridSpacing = spacing;
                    canvas.GridSpacingY = spacing;
                    double x = Math.Round(1000 / spacing) * spacing;
                    var fifth = new GeoPoint2(x + spacing / 5, x + spacing / 10);
                    Assert.False(Lined(Pixels(canvas), canvas, fifth));

                    canvas.GridDisplay = 7;
                    int[] pixels = Pixels(canvas);
                    Assert.True(Near(pixels, canvas, fifth, Minor));
                    Assert.True(Near(pixels, canvas, new GeoPoint2(x, x + spacing / 10), Major));
                }
            );
        }
    }
}
