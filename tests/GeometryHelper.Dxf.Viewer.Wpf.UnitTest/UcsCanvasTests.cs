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
    /// The canvas in a user coordinate system (C7): the grid along its axes, the crosshair along them, the UCS icon at the lower
    /// left or at the origin turned with it, and the readout of the status bar in its coordinates.
    /// </summary>
    public class UcsCanvasTests
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

        private static Point Screen(CadCanvas canvas, GeoPoint2 world)
        {
            canvas.View.DrawingToPixel(world.X, world.Y, out double px, out double py);
            return new Point(px, py);
        }

        [Fact]
        public void TheGridOfAUcsTurned30Degrees_RunsAlongItsAxes_NotTheWorlds()
        {
            OnCanvas(
                (model, canvas) =>
                {
                    model.Execute("UCS Z 30");
                    canvas.ShowGrid = true;
                    int[] pixels = Pixels(canvas);
                    CadUcs ucs = model.Document.Ucs.Current;
                    Assert.Equal(50.0, CadCanvas.GridStep(canvas.View.PixelsPerUnit, 10, 6));

                    // Along the UCS: the twentieth line from its origin is major, the twenty-first minor, between them nothing.
                    Assert.True(Near(pixels, Screen(canvas, ucs.ToWorld(new GeoPoint2(1000, 1025))), 0x2D2D2D));
                    Assert.True(Near(pixels, Screen(canvas, ucs.ToWorld(new GeoPoint2(1050, 1025))), 0x141414));
                    Assert.False(Near(pixels, Screen(canvas, ucs.ToWorld(new GeoPoint2(1025, 1025))), 0x141414));

                    // Where the world's grid would have its major line, at x 1 000, the UCS has none.
                    Assert.False(Near(pixels, Screen(canvas, new GeoPoint2(1000, 1025)), 0x2D2D2D));
                    Assert.False(Near(pixels, Screen(canvas, new GeoPoint2(1000, 1025)), 0x141414));
                }
            );
        }

        [Fact]
        public void TheUcsIcon_SitsAtTheLowerLeftOrAtTheOrigin_AndTurnsWithTheUcs()
        {
            OnCanvas(
                (model, canvas) =>
                {
                    model.Execute("UCSICON N");
                    Assert.True(canvas.TryPlaceUcsIcon(out Point corner, out Vector xArm, out bool atOrigin));
                    Assert.False(atOrigin);
                    Assert.Equal(new Point(18, 582), corner);
                    Assert.Equal(new Vector(1, 0), xArm);
                    Assert.True(Near(Pixels(canvas), corner + xArm * 20, null));

                    // Turned a quarter, the X arm points up the canvas, and the icon moves right to keep its Y arm in view.
                    model.Execute("UCS Z 90");
                    Assert.True(canvas.TryPlaceUcsIcon(out corner, out xArm, out atOrigin));
                    Assert.Equal(0.0, xArm.X, 9);
                    Assert.Equal(-1.0, xArm.Y, 9);
                    Assert.Equal(74.0, corner.X, 9);
                    Assert.True(Near(Pixels(canvas), corner + xArm * 20, null));

                    // At the origin, where it is in view.
                    model.Execute("UCSICON OR");
                    model.Execute("UCS *2500,1500");
                    model.Execute(string.Empty);
                    Assert.True(canvas.TryPlaceUcsIcon(out corner, out xArm, out atOrigin));
                    Assert.True(atOrigin);
                    Point origin = Screen(canvas, new GeoPoint2(2500, 1500));
                    Assert.Equal(origin.X, corner.X, 6);
                    Assert.Equal(origin.Y, corner.Y, 6);

                    model.Execute("UCSICON OFF");
                    Assert.False(canvas.TryPlaceUcsIcon(out _, out _, out _));
                }
            );
        }

        [Fact]
        public void TheCrosshairRunsAlongTheUcs_AndTheReadoutIsInItsCoordinates()
        {
            OnCanvas(
                (model, canvas) =>
                {
                    model.SetCursor(10, 0);
                    string inWorld = model.CursorText;
                    model.Execute("UCS *100,50");
                    model.Execute(string.Empty);
                    model.SetCursor(110, 50);
                    Assert.Equal(inWorld, model.CursorText);

                    model.Execute("UCS Z 30");
                    canvas.CrosshairPercent = 100;
                    var middle = new GeoPoint2(2500, 1500);
                    canvas.MoveCursorTo(middle.X, middle.Y);
                    int[] pixels = Pixels(canvas);
                    Point at = Screen(canvas, middle);

                    // 100 pixels along the X axis of the UCS the crosshair is drawn; 100 pixels across the canvas it is not.
                    Assert.True(
                        Near(pixels, at + new Vector(100 * Math.Cos(Math.PI / 6), -100 * Math.Sin(Math.PI / 6)), null)
                    );
                    Assert.True(
                        Near(pixels, at + new Vector(-100 * Math.Sin(Math.PI / 6), -100 * Math.Cos(Math.PI / 6)), null)
                    );
                    Assert.False(Near(pixels, at + new Vector(100, 0), null));
                }
            );
        }
    }
}
