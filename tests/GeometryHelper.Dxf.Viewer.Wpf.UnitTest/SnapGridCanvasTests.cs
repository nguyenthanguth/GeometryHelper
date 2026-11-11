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
    /// The grid and the crosshair of the canvas following the snap, as AutoCAD 2021 draws them: a major line every GRIDMAJOR,
    /// the grid laid from SNAPBASE and turned by SNAPANG, and with an isometric snap the grid and the crosshair on the two
    /// axes of the isoplane.
    /// </summary>
    public class SnapGridCanvasTests
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

        /// <summary>Whether a pixel within one of a point of the canvas is of a colour, or as light as one where the colour is null.</summary>
        private static bool Near(int[] pixels, Point at, int? rgb)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    int x = (int)Math.Round(at.X) + dx;
                    int y = (int)Math.Round(at.Y) + dy;
                    int pixel = pixels[y * Width + x] & 0xFFFFFF;

                    if (
                        rgb.HasValue
                            ? pixel == rgb.Value
                            : ((pixel >> 16) & 0xFF) > 0xA0 && ((pixel >> 8) & 0xFF) > 0xA0 && (pixel & 0xFF) > 0xA0
                    )
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool Lined(int[] pixels, CadCanvas canvas, GeoPoint2 world) =>
            Near(pixels, Screen(canvas, world), Minor) || Near(pixels, Screen(canvas, world), Major);

        private static Point Screen(CadCanvas canvas, GeoPoint2 world)
        {
            canvas.View.DrawingToPixel(world.X, world.Y, out double px, out double py);
            return new Point(px, py);
        }

        /// <summary>The spacing the grid is drawn at: the spacing, then the major lines, then 2, 5, 2 and on times, 6 pixels apart at least.</summary>
        private static double Drawn(CadCanvas canvas, double spacing, int major)
        {
            double step = spacing;

            for (int i = 0; step * canvas.View.PixelsPerUnit < 6; i++)
            {
                step *=
                    i == 0 ? major
                    : i % 2 == 0 ? 5
                    : 2;
            }

            return step;
        }

        [Fact]
        public void AGridMajorOf4_DrawsEveryFourthLineStronger()
        {
            OnCanvas(
                (model, canvas) =>
                {
                    canvas.ShowGrid = true;
                    canvas.GridMajor = 4;
                    int[] pixels = Pixels(canvas);
                    double step = Drawn(canvas, 10, 4);
                    double x = Math.Round(1000 / (4 * step)) * 4 * step;

                    Assert.True(Near(pixels, Screen(canvas, new GeoPoint2(x, x + step / 2)), Major));
                    Assert.True(Near(pixels, Screen(canvas, new GeoPoint2(x + step, x + step / 2)), Minor));
                    Assert.False(Lined(pixels, canvas, new GeoPoint2(x + step / 2, x + step / 2)));
                }
            );
        }

        [Fact]
        public void TheGridTurnedBy30DegreesFromASnapBase_RunsAlongTheTurnedAxesThroughIt()
        {
            OnCanvas(
                (model, canvas) =>
                {
                    canvas.ShowGrid = true;
                    canvas.SnapAngle = Math.PI / 6;
                    canvas.SnapBase = new GeoPoint2(25, 0);
                    int[] pixels = Pixels(canvas);
                    CadUcs frame = CadUcs.InPlane(new GeoPoint2(25, 0), Math.PI / 6);
                    double step = Drawn(canvas, 10, 5);
                    double major = 5 * step;
                    double x = Math.Round(1000 / major) * major;

                    Assert.True(Near(pixels, Screen(canvas, frame.ToWorld(new GeoPoint2(x, x + step / 2))), Major));
                    Assert.True(
                        Near(pixels, Screen(canvas, frame.ToWorld(new GeoPoint2(x + step, x + step / 2))), Minor)
                    );
                    Assert.False(Lined(pixels, canvas, frame.ToWorld(new GeoPoint2(x + step / 2, x + step / 2))));

                    // Where the grid of the world would have its major line, the turned grid has none.
                    Assert.False(Lined(pixels, canvas, new GeoPoint2(x, x + step / 2 + 0.3 * step)));
                }
            );
        }

        [Fact]
        public void AnIsometricGridOnTheTopPlane_RunsAt30And150Degrees()
        {
            OnCanvas(
                (model, canvas) =>
                {
                    canvas.ShowGrid = true;
                    canvas.GridSpacingY = 2.5;
                    canvas.Isoplane = CadIsoplane.Top;
                    int[] pixels = Pixels(canvas);
                    double step = Drawn(canvas, 5 * Math.Sqrt(3.0) / 2.0, 5);

                    // The point a steps off the 30 degree line through the origin and b off the 150 degree one.
                    GeoPoint2 At(double a, double b) => new GeoPoint2(-(a + b) * step, (a - b) * step / Math.Sqrt(3.0));

                    Assert.True(Near(pixels, Screen(canvas, At(10, -32.5)), Major));
                    Assert.True(Near(pixels, Screen(canvas, At(11, -32.5)), Minor));
                    Assert.True(Near(pixels, Screen(canvas, At(10.5, -35)), Major));
                    Assert.False(Lined(pixels, canvas, At(10.5, -32.5)));
                }
            );
        }

        [Fact]
        public void TheCrosshair_TurnsWithTheSnapAngle_AndRunsOnTheIsoplanesAxes()
        {
            OnCanvas(
                (model, canvas) =>
                {
                    canvas.CrosshairPercent = 100;
                    var middle = new GeoPoint2(2500, 1500);
                    canvas.MoveCursorTo(middle.X, middle.Y);
                    Point at = Screen(canvas, middle);
                    Vector Way(double degrees) =>
                        new Vector(
                            100 * Math.Cos(degrees * Math.PI / 180.0),
                            -100 * Math.Sin(degrees * Math.PI / 180.0)
                        );

                    // Turned by 20 degrees, the arms run at 20 and 110 degrees. (An arm straight up or across is drawn
                    // between two pixels, too faint for the test of lightness, so the angles checked are slanted.)
                    canvas.SnapAngle = Math.PI / 9;
                    int[] pixels = Pixels(canvas);
                    Assert.True(Near(pixels, at + Way(20), null));
                    Assert.True(Near(pixels, at + Way(110), null));
                    Assert.False(Near(pixels, at + Way(0), null));

                    // On the top isoplane, at 30 and 150 degrees.
                    canvas.SnapAngle = 0;
                    canvas.Isoplane = CadIsoplane.Top;
                    pixels = Pixels(canvas);
                    Assert.True(Near(pixels, at + Way(30), null));
                    Assert.True(Near(pixels, at + Way(150), null));
                    Assert.False(Near(pixels, at + Way(0), null));
                    Assert.False(Near(pixels, at + Way(20), null));

                    // On the left isoplane turned by 20 degrees, at 110 and 170.
                    canvas.SnapAngle = Math.PI / 9;
                    canvas.Isoplane = CadIsoplane.Left;
                    pixels = Pixels(canvas);
                    Assert.True(Near(pixels, at + Way(110), null));
                    Assert.True(Near(pixels, at + Way(170), null));
                    Assert.False(Near(pixels, at + Way(20), null));
                    Assert.False(Near(pixels, at + Way(150), null));
                }
            );
        }
    }
}
