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
    /// The custom grips of a dynamic block drawn on the canvas: in the dynamic grip colour, AutoCAD's pink under the cursor
    /// (GRIPHOVER, as a plain grip's square), the hot red once clicked, and growing with the grip size.
    /// </summary>
    public sealed class DynamicGripCanvasTests
    {
        private const int Width = 800;
        private const int Height = 600;
        private const int Dynamic = 0x00BFFF;
        private const int Pink = 0xFF7F7F;
        private const int Red = 0xF85149;

        /// <summary>
        /// Runs a test on an STA thread: GH_PANEL, a rectangle 100 by 50 whose Width stretches its right edge, inserted at the
        /// origin and selected, under a canvas of 800 by 600 showing -20,-20 to 120,70; the test is given the pixel of the
        /// Width grip.
        /// </summary>
        private static void OnCanvas(Action<MainViewModel, CadCanvas, Point> test)
        {
            Exception failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    DxfDatabase database = CadTemplate.CreateMetric();
                    var panel = new DxfBlockTableRecord { Name = "GH_PANEL", Origin = new GeoPoint3(0, 0, 0) };
                    database.BlockTable.Add(panel);
                    DxfLine[] sides =
                    {
                        new DxfLine(new GeoPoint3(100, 0, 0), new GeoPoint3(100, 50, 0)),
                        new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0)),
                        new DxfLine(new GeoPoint3(0, 50, 0), new GeoPoint3(100, 50, 0)),
                    };

                    foreach (DxfLine side in sides)
                    {
                        panel.AppendEntity(side);
                    }

                    DxfBlockLinearParameter width = panel.AddLinearParameter(
                        "Width",
                        null,
                        new GeoPoint3(0, 0, 0),
                        new GeoPoint3(100, 0, 0),
                        -20,
                        DxfBlockParameterValueSet.List(new[] { 100.0, 150, 200 })
                    );
                    panel.AddStretchAction(width, new GeoPoint2(90, -10), new GeoPoint2(110, 60), sides);
                    var insert = new DxfBlockReference(new GeoPoint3(0, 0, 0), panel);
                    database.ModelSpace.AppendEntity(insert);

                    var model = new MainViewModel();
                    model.OpenDocument(CadDocument.From(database, "panel.dxf"));
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
                    canvas.View.ZoomTo(new DisplayBox(-20, -20, 120, 70));
                    model.Document.Selection.Set(new[] { insert.Handle });
                    canvas.RenderNow();

                    CadGrip grip = CadGrips.Of(model.Document.FindEntity(insert.Handle)).First(g => g.Custom != null);
                    test(model, canvas, CanvasMouseTests.At(canvas, grip.Point.X, grip.Point.Y));
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

        private static int Pixel(int[] pixels, Point at) =>
            pixels[(int)Math.Round(at.Y) * Width + (int)Math.Round(at.X)] & 0xFFFFFF;

        /// <summary>Whether two colours are within 12 of each other in every channel: the grip's edge and the lines under it blend a pixel a little.</summary>
        private static bool Near(int a, int b) =>
            Math.Abs(((a >> 16) & 0xFF) - ((b >> 16) & 0xFF)) <= 12
            && Math.Abs(((a >> 8) & 0xFF) - ((b >> 8) & 0xFF)) <= 12
            && Math.Abs((a & 0xFF) - (b & 0xFF)) <= 12;

        /// <summary>The width of the pixels of a colour within 40 pixels of a point; 0 where there are none.</summary>
        private static int Across(int[] pixels, Point at, int colour)
        {
            int left = int.MaxValue,
                right = int.MinValue;

            for (int y = (int)at.Y - 40; y <= (int)at.Y + 40; y++)
            {
                for (int x = (int)at.X - 40; x <= (int)at.X + 40; x++)
                {
                    if (x >= 0 && y >= 0 && x < Width && y < Height && (pixels[y * Width + x] & 0xFFFFFF) == colour)
                    {
                        left = Math.Min(left, x);
                        right = Math.Max(right, x);
                    }
                }
            }

            return right < left ? 0 : right - left + 1;
        }

        [Fact]
        public void ADynamicGrip_IsInTheDynamicColour_PinkUnderTheCursor_AndRedOnceClicked()
        {
            OnCanvas(
                (model, canvas, grip) =>
                {
                    Assert.Equal(Dynamic, Pixel(Pixels(canvas), grip));

                    CanvasMouseTests.Mouse(canvas, UIElement.MouseMoveEvent, grip);
                    Assert.Equal(Pink, Pixel(Pixels(canvas), grip));

                    CanvasMouseTests.Mouse(canvas, UIElement.MouseDownEvent, grip);
                    CanvasMouseTests.Mouse(canvas, UIElement.MouseUpEvent, grip);
                    Assert.True(model.Session.IsGripEditing);
                    Assert.True(Near(Red, Pixel(Pixels(canvas), grip)));
                }
            );
        }

        [Fact]
        public void ADynamicGrip_GrowsWithTheGripSize()
        {
            OnCanvas(
                (model, canvas, grip) =>
                {
                    int standard = Across(Pixels(canvas), grip, Dynamic);
                    canvas.GripSize = 10;
                    int large = Across(Pixels(canvas), grip, Dynamic);

                    Assert.True(standard > 0);
                    Assert.True(large > standard * 3 / 2);
                }
            );
        }
    }
}
