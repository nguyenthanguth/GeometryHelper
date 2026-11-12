using System;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// A block reference clipped by XCLIP as the canvas draws it: what lies outside the boundary is not drawn, what lies inside is,
    /// the frame of the boundary is drawn over it, and the frame is left out where XCLIPFRAME is 0.
    /// </summary>
    public sealed class XClipCanvasTests
    {
        private const int Width = 800;
        private const int Height = 600;

        /// <summary>
        /// Runs a test on an STA thread: a block of a line from (0, 0) to (100, 0) inserted at the origin, clipped to the rectangle
        /// (20, -10) to (60, 10) by XCLIP, under a canvas of 800 by 600 showing -20,-20 to 120,120.
        /// </summary>
        private static void OnClippedLine(Action<MainViewModel, CadCanvas> test)
        {
            Exception failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    var database = new DxfDatabase("AC1027");
                    var block = new DxfBlockTableRecord { Name = "LINE100" };
                    database.BlockTable.Add(block);
                    block.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0)));
                    var insert = new DxfBlockReference(new GeoPoint3(0, 0, 0), block);
                    database.ModelSpace.AppendEntity(insert);
                    var model = new MainViewModel();
                    model.OpenDocument(CadDocument.From(database, "xclip.dxf"), "xclip.dxf");
                    model.Execute("XCLIP");
                    model.Session.FeedSelection(new[] { insert.Handle });
                    model.Session.Enter();
                    model.Session.Enter();
                    model.Session.Enter();
                    model.Session.Feed(new GeoPoint2(20, -10));
                    model.Session.Feed(new GeoPoint2(60, 10));
                    Assert.NotNull(insert.SpatialFilter);
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

        /// <summary>Whether a pixel within two of a point is as light as the line, which is white on the dark background.</summary>
        private static bool Drawn(int[] pixels, Point at)
        {
            for (int dy = -2; dy <= 2; dy++)
            {
                for (int dx = -2; dx <= 2; dx++)
                {
                    int x = (int)Math.Round(at.X) + dx;
                    int y = (int)Math.Round(at.Y) + dy;
                    int pixel = pixels[y * Width + x] & 0xFFFFFF;

                    if (((pixel >> 16) & 0xFF) > 0xA0 && ((pixel >> 8) & 0xFF) > 0xA0 && (pixel & 0xFF) > 0xA0)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        [Fact]
        public void TheLine_IsDrawnInsideTheBoundary_AndNotOutsideIt()
        {
            OnClippedLine(
                (model, canvas) =>
                {
                    int[] pixels = Pixels(canvas);

                    Assert.True(Drawn(pixels, At(canvas, 40, 0)));
                    Assert.True(Drawn(pixels, At(canvas, 25, 0)));
                    Assert.False(Drawn(pixels, At(canvas, 80, 0)), "80");
                    Assert.False(Drawn(pixels, At(canvas, 70, 0)), "70");
                    Assert.False(Drawn(pixels, At(canvas, 100, 0)), "100");
                }
            );
        }

        [Fact]
        public void TheFrameOfTheBoundary_IsDrawnOverIt_AndGoneAtXclipframeNought()
        {
            OnClippedLine(
                (model, canvas) =>
                {
                    Assert.True(Drawn(Pixels(canvas), At(canvas, 40, 10)));
                    Assert.True(Drawn(Pixels(canvas), At(canvas, 60, 5)));

                    model.Execute("XCLIPFRAME");
                    model.Execute("0");
                    int[] pixels = Pixels(canvas);

                    Assert.False(Drawn(pixels, At(canvas, 40, 10)));
                    Assert.True(Drawn(pixels, At(canvas, 40, 0)));
                }
            );
        }

        [Fact]
        public void TheClipSwitchedOff_ShowsTheWholeLine()
        {
            OnClippedLine(
                (model, canvas) =>
                {
                    DxfBlockReference insert = canvas.Document.Database.ModelSpace.OfType<DxfBlockReference>().Single();
                    model.Execute("XCLIP");
                    model.Session.FeedSelection(new[] { insert.Handle });
                    model.Session.Enter();
                    model.Session.Feed("OFF");
                    int[] pixels = Pixels(canvas);

                    Assert.True(Drawn(pixels, At(canvas, 80, 0)));
                    Assert.True(Drawn(pixels, At(canvas, 100, 0)));
                }
            );
        }
    }
}
