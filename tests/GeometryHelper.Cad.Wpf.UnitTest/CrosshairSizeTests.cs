using System;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Cad.Wpf.UnitTest.CanvasMouseTests;

namespace GeometryHelper.Cad.Wpf.UnitTest
{
    /// <summary>
    /// The size of the crosshair as AutoCAD 2021's CURSORSIZE sets it, offscreen on a canvas of 800 by 600: at 100 the two
    /// lines run from edge to edge of the canvas wherever the cursor is, and through a viewport of a layout they run from edge
    /// to edge of its frame and not past it; below 100 they are a share of the canvas long. The crosshair shows as the pixels
    /// that differ from the same canvas drawn with CURSORSIZE 0.
    /// </summary>
    public class CrosshairSizeTests
    {
        private const int Width = 800;
        private const int Height = 600;

        private static void Run(Action body)
        {
            Exception failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    body();
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

        private static CadCanvas CanvasOver(MainViewModel model)
        {
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
            return canvas;
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

        /// <summary>The pixels the crosshair adds: the canvas drawn with a crosshair of a size, less the one drawn with none.</summary>
        private static bool[] Crosshair(CadCanvas canvas, Point cursor, double percent)
        {
            // The canvas keeps the cursor only while it draws a crosshair, so the cursor moves with one of the size set.
            canvas.CrosshairPercent = percent;
            Mouse(canvas, UIElement.MouseMoveEvent, cursor);
            int[] drawn = Pixels(canvas);
            canvas.CrosshairPercent = 0;
            int[] bare = Pixels(canvas);
            return drawn.Select((p, i) => p != bare[i]).ToArray();
        }

        /// <summary>Whether a pixel within one of a point is part of the crosshair.</summary>
        private static bool Ink(bool[] ink, double x, double y)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    int px = (int)Math.Round(x) + dx;
                    int py = (int)Math.Round(y) + dy;

                    if (px >= 0 && py >= 0 && px < Width && py < Height && ink[py * Width + px])
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        [Fact]
        public void AtOneHundred_TheLinesReachEveryEdge_WhereverTheCursorIs()
        {
            Run(() =>
            {
                using (var folder = new TestFolder())
                {
                    var model = new MainViewModel();
                    model.Open(folder.Write("room.dxf", TestFiles.Room(out _, out _)));
                    CadCanvas canvas = CanvasOver(model);

                    // The middle, the four corners, and the lower right of the maintainer's screenshot, each on a pixel's middle.
                    foreach (
                        (double x, double y) in new[]
                        {
                            (400.5, 300.5),
                            (10.5, 10.5),
                            (789.5, 10.5),
                            (10.5, 589.5),
                            (789.5, 589.5),
                            (680.5, 570.5),
                        }
                    )
                    {
                        bool[] ink = Crosshair(canvas, new Point(x, y), 100);
                        string where = $"cursor ({x}, {y})";

                        Assert.True(Ink(ink, 0, y), $"left edge, {where}");
                        Assert.True(Ink(ink, Width - 1, y), $"right edge, {where}");
                        Assert.True(Ink(ink, x, 0), $"top edge, {where}");
                        Assert.True(Ink(ink, x, Height - 1), $"bottom edge, {where}");
                    }

                    canvas.Dispose();
                }
            });
        }

        [Fact]
        public void AtOneHundred_WithTheLinesTurned_TheyStillReachTheEdges()
        {
            Run(() =>
            {
                using (var folder = new TestFolder())
                {
                    var model = new MainViewModel();
                    model.Open(folder.Write("room.dxf", TestFiles.Room(out _, out _)));
                    CadCanvas canvas = CanvasOver(model);
                    canvas.SnapAngle = Math.PI / 4.0;
                    var cursor = new Point(789.5, 589.5);
                    bool[] ink = Crosshair(canvas, cursor, 100);

                    // The arm at 45 degrees, up and to the left, runs on to the canvas's upper left corner region: the point
                    // where it leaves is (789.5 - 589.5, 0) = (200, 0), the arm down and to the right leaves at the corner.
                    Assert.True(Ink(ink, 200, 0));
                    Assert.True(Ink(ink, 789.5 - 400.0, 589.5 - 400.0));
                    Assert.True(Ink(ink, Width - 1, Height - 1));
                    canvas.Dispose();
                }
            });
        }

        [Fact]
        public void BelowOneHundred_TheLinesAreAShareOfTheCanvasLong_AsTheyWere()
        {
            Run(() =>
            {
                using (var folder = new TestFolder())
                {
                    var model = new MainViewModel();
                    model.Open(folder.Write("room.dxf", TestFiles.Room(out _, out _)));
                    CadCanvas canvas = CanvasOver(model);
                    bool[] ink = Crosshair(canvas, new Point(400.5, 300.5), 50);

                    // 50 per cent of 800 is 400 long, 200 each way.
                    Assert.True(Ink(ink, 400.5 + 190, 300.5));
                    Assert.False(Ink(ink, 400.5 + 210, 300.5));
                    Assert.True(Ink(ink, 400.5, 300.5 - 190));
                    Assert.False(Ink(ink, 400.5, 300.5 - 210));
                    canvas.Dispose();
                }
            });
        }

        [Fact]
        public void ThroughAViewportAtOneHundred_TheLinesReachTheFramesEdges_AndNotPastThem()
        {
            Run(() =>
            {
                using (var folder = new TestFolder())
                {
                    DxfDatabase db = TestFiles.RoomWithViewport(folder, out _, out string handle);
                    DxfViewport frame = db
                        .Layouts.Single(l => l.LayoutName == "Layout1")
                        .GetViewports()
                        .Single(v => v.Handle == handle);
                    frame.CenterPoint = new GeoPoint3(6, 4.5, 0);
                    frame.Width = 8;
                    frame.Height = 6;
                    frame.ViewHeight = 3000;
                    var model = new MainViewModel();
                    model.Open(folder.Write("room.dxf", db));
                    model.SelectSpace(model.Document.Spaces.Single(s => s.Name == "Layout1"));
                    CadCanvas canvas = CanvasOver(model);
                    model.Toggles.Single(t => t.Name == "OSNAP").IsOn = false;
                    Assert.True(canvas.ViewportClick(At(canvas, 6, 4.5), true));

                    // The frame is 2 to 10 across and 1.5 to 7.5 up on the sheet; the cursor is inside, on a pixel's middle.
                    Point inside = At(canvas, 7, 5);
                    var cursor = new Point(Math.Floor(inside.X) + 0.5, Math.Floor(inside.Y) + 0.5);
                    Point left = At(canvas, 2, 5);
                    Point right = At(canvas, 10, 5);
                    Point top = At(canvas, 7, 7.5);
                    Point bottom = At(canvas, 7, 1.5);
                    bool[] ink = Crosshair(canvas, cursor, 100);

                    Assert.True(Ink(ink, left.X + 3, cursor.Y));
                    Assert.True(Ink(ink, right.X - 3, cursor.Y));
                    Assert.True(Ink(ink, cursor.X, top.Y + 3));
                    Assert.True(Ink(ink, cursor.X, bottom.Y - 3));
                    Assert.False(Ink(ink, left.X - 6, cursor.Y));
                    Assert.False(Ink(ink, right.X + 6, cursor.Y));
                    Assert.False(Ink(ink, cursor.X, top.Y - 6));
                    Assert.False(Ink(ink, cursor.X, bottom.Y + 6));
                    canvas.Dispose();
                }
            });
        }
    }
}
