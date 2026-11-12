using System;
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
    /// An MTEXT's grips drawn on the canvas, to the design MTextGrips: the square base grip, the triangle pointing right of a
    /// width or the last column, the triangle pointing down of a height and the one pointing into the corner, filled in the
    /// grip colour, red under the mouse.
    /// </summary>
    public sealed class MTextGripCanvasTests
    {
        private const int Width = 800;
        private const int Height = 600;
        private const int Grip = 0x3D9BFF;
        private const int Hot = 0xF85149;
        private const int Pink = 0xFF7F7F;

        /// <summary>Runs a test on an STA thread: an MTEXT at 100,100 selected under a canvas of 800 by 600 showing 90,75 to 140,110.</summary>
        private static void OnCanvas(Action<DxfMText> make, Action<MainViewModel, CadCanvas> test)
        {
            Exception failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    var database = new DxfDatabase("AC1027");
                    var mtext = new DxfMText
                    {
                        Contents = "Plate\\PBeam",
                        Location = new GeoPoint3(100, 100, 0),
                        TextHeight = 2.5,
                        Width = 30.0,
                    };
                    database.ModelSpace.AppendEntity(mtext);
                    make(mtext);
                    var model = new MainViewModel();
                    model.OpenDocument(CadDocument.From(database, "notes.dxf"), "notes.dxf");
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
                    canvas.View.ZoomTo(new DisplayBox(90, 75, 140, 110));
                    model.Document.Selection.Set(new[] { mtext.Handle });
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

        private static Point At(CadCanvas canvas, double x, double y) => CanvasMouseTests.At(canvas, 100 + x, 100 + y);

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

        private static int Pixel(int[] pixels, Point at, double dx, double dy) =>
            pixels[(int)Math.Round(at.Y + dy) * Width + (int)Math.Round(at.X + dx)] & 0xFFFFFF;

        [Fact]
        public void AnMTextOfNoColumns_HasTheSquareAtItsLocation_ATriangleRight_ATriangleDown_AndOneIntoTheCorner()
        {
            OnCanvas(
                t => t.Height = 15.0,
                (model, canvas) =>
                {
                    int[] pixels = Pixels(canvas);
                    Point a = At(canvas, 0, 0);
                    Point b = At(canvas, 30, 0);
                    Point c = At(canvas, 0, -15);
                    Point d = At(canvas, 30, -15);

                    // The square fills its corners; a triangle leaves them.
                    Assert.Equal(Grip, Pixel(pixels, a, 3, -3));
                    Assert.Equal(Grip, Pixel(pixels, b, 0, 0));
                    Assert.Equal(Grip, Pixel(pixels, b, 1, 0));
                    Assert.NotEqual(Grip, Pixel(pixels, b, 5, -5));
                    Assert.Equal(Grip, Pixel(pixels, c, 0, 1));
                    Assert.NotEqual(Grip, Pixel(pixels, c, 5, 5));
                    Assert.NotEqual(Grip, Pixel(pixels, c, -5, 5));
                    Assert.Equal(Grip, Pixel(pixels, d, 3, 3));
                    Assert.NotEqual(Grip, Pixel(pixels, d, -3, -3));
                }
            );
        }

        [Fact]
        public void StaticColumns_HaveTheTrianglesOfAutoCadsGripPoints_AndTheOneUnderTheMouseIsPink_AClickedOneRed()
        {
            OnCanvas(
                t =>
                {
                    t.Height = 10.0;
                    t.SetStaticColumns(12.0, 2.0, 2);
                },
                (model, canvas) =>
                {
                    Point b = At(canvas, 12, 0);
                    Point e = At(canvas, 26, 0);
                    Point c = At(canvas, 0, -10);
                    Point d = At(canvas, 26, -10);
                    int[] pixels = Pixels(canvas);
                    Assert.Equal(Grip, Pixel(pixels, b, 1, 0));
                    Assert.NotEqual(Grip, Pixel(pixels, b, 5, -5));
                    Assert.Equal(Grip, Pixel(pixels, e, 1, 0));
                    Assert.Equal(Grip, Pixel(pixels, c, 0, 1));
                    Assert.NotEqual(Grip, Pixel(pixels, c, 5, 5));
                    Assert.Equal(Grip, Pixel(pixels, d, 3, 3));
                    Assert.NotEqual(Grip, Pixel(pixels, d, -3, -3));

                    // Under the cursor a grip is AutoCAD's pink (GRIPHOVER), as a plain grip's square is.
                    CanvasMouseTests.Mouse(canvas, UIElement.MouseMoveEvent, e);
                    pixels = Pixels(canvas);
                    Assert.Equal(Pink, Pixel(pixels, e, 1, 0));
                    Assert.NotEqual(Pink, Pixel(pixels, e, 5, -5));
                    Assert.Equal(Grip, Pixel(pixels, b, 1, 0));

                    // Clicked, the grip edit starts and it is the hot red.
                    CanvasMouseTests.Mouse(canvas, UIElement.MouseDownEvent, e);
                    CanvasMouseTests.Mouse(canvas, UIElement.MouseUpEvent, e);
                    pixels = Pixels(canvas);
                    Assert.True(model.Session.IsGripEditing);
                    Assert.Equal(Hot, Pixel(pixels, e, 1, 0));
                    Assert.NotEqual(Hot, Pixel(pixels, e, 5, -5));
                }
            );
        }

        /// <summary>The box (width, height) of the pixels near the grip colour within 30 pixels of a point; 0 by 0 where there are none.</summary>
        private static Size Extent(int[] pixels, Point at, int colour)
        {
            int left = int.MaxValue,
                right = int.MinValue,
                top = int.MaxValue,
                bottom = int.MinValue;

            for (int y = (int)at.Y - 30; y <= (int)at.Y + 30; y++)
            {
                for (int x = (int)at.X - 30; x <= (int)at.X + 30; x++)
                {
                    if (x < 0 || y < 0 || x >= Width || y >= Height)
                    {
                        continue;
                    }

                    int rgb = pixels[y * Width + x] & 0xFFFFFF;
                    int off =
                        Math.Abs(((rgb >> 16) & 0xFF) - ((colour >> 16) & 0xFF))
                        + Math.Abs(((rgb >> 8) & 0xFF) - ((colour >> 8) & 0xFF))
                        + Math.Abs((rgb & 0xFF) - (colour & 0xFF));

                    if (off > 60)
                    {
                        continue;
                    }

                    left = Math.Min(left, x);
                    right = Math.Max(right, x);
                    top = Math.Min(top, y);
                    bottom = Math.Max(bottom, y);
                }
            }

            return right < left ? new Size(0, 0) : new Size(right - left + 1, bottom - top + 1);
        }

        [Theory]
        [InlineData(5, 15)]
        [InlineData(8, 21)]
        [InlineData(12, 29)]
        public void TheTrianglesOfAnMText_AreAsLargeAsTheSquareAtEveryGripSize(int size, int square)
        {
            OnCanvas(
                t => t.Height = 15.0,
                (model, canvas) =>
                {
                    canvas.GripSize = size;
                    int[] pixels = Pixels(canvas);

                    // Each grip's box is the square's side, less the dark edge of a triangle (one pixel a side).
                    foreach (Point at in new[] { At(canvas, 30, 0), At(canvas, 0, -15), At(canvas, 30, -15) })
                    {
                        Size extent = Extent(pixels, at, Grip);
                        Assert.InRange(extent.Width, square - 3, square);
                        Assert.InRange(extent.Height, square - 3, square);
                    }

                    // Not the old fixed 10 across, whatever the size.
                    Assert.True(Extent(pixels, At(canvas, 30, 0), Grip).Width > 10);
                }
            );
        }
    }
}
