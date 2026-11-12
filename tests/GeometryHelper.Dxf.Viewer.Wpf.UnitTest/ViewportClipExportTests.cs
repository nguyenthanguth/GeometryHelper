using System;
using System.IO;
using System.Linq;
using System.Threading;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using SkiaSharp;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// A viewport clipped by an entity (VPCLIP) in the image and plot writers, as pixels: the room's viewport on the sheet of 12
    /// by 9, 8 by 6 about (6, 4.5), showing the room at 1:500, clipped to a circle of radius 2.4 about (6, 4.5). The walls at
    /// y = 1.5 and 7.5 lie outside the circle, so nothing of them may show in an image of the layout (IMAGEOUT, PNGOUT) or in its
    /// plot to a PNG (PLOT with the PNG device), as on the canvas.
    /// </summary>
    public class ViewportClipExportTests
    {
        private const int Width = 1200;
        private const int Height = 900;

        private static readonly DisplayClip2 Circle = new DisplayClip2(
            new[] { 8.4, 4.5, 3.6, 4.5 },
            new[] { 1.0, 1.0 }
        );

        /// <summary>Runs a test on a document of the room with its viewport, clipped to the circle where asked, on a thread of its own.</summary>
        private static void OnRoom(bool clipped, Action<CadDocument, CadSpace> test)
        {
            Exception failure = null;
            var thread = new Thread(() =>
            {
                try
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

                        if (clipped)
                        {
                            var round = new DxfCircle(new GeoPoint3(6, 4.5, 0), new GeoVector3(0, 0, 1), 2.4);
                            ((DxfBlockTableRecord)frame.Owner).AppendEntity(round);
                            frame.ClipTo(round);
                        }

                        var model = new MainViewModel();
                        model.Open(folder.Write("room.dxf", db));
                        CadSpace layout = model.Document.Spaces.Single(s => s.Name == "Layout1");
                        test(model.Document, layout);
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

        /// <summary>
        /// The pixels of a picture that lie farther than three pixels from the circle and inside the viewport's old rectangle, and are
        /// not the colour of the paper: how many outside the circle and how many inside it. <paramref name="toPixel"/> gives the
        /// pixel of a point of the drawing.
        /// </summary>
        private static void Count(
            SKBitmap picture,
            Func<double, double, double[]> toPixel,
            int paper,
            out int outside,
            out int inside
        )
        {
            outside = 0;
            inside = 0;

            for (double y = 1.4; y <= 7.6; y += 0.02)
            {
                for (double x = 1.9; x <= 10.1; x += 0.02)
                {
                    double[] p = toPixel(x, y);
                    int ix = (int)Math.Round(p[0]);
                    int iy = (int)Math.Round(p[1]);

                    if (ix < 0 || iy < 0 || ix >= picture.Width || iy >= picture.Height)
                    {
                        continue;
                    }

                    if (Circle.DistanceToEdge(x, y) <= 0.1)
                    {
                        continue;
                    }

                    SKColor c = picture.GetPixel(ix, iy);
                    int rgb = (c.Red << 16) | (c.Green << 8) | c.Blue;

                    if (rgb == paper)
                    {
                        continue;
                    }

                    if (Circle.Contains(x, y))
                    {
                        inside++;
                    }
                    else
                    {
                        outside++;
                    }
                }
            }
        }

        private static void CountImage(CadDocument document, CadSpace space, out int outside, out int inside)
        {
            var view = new CadView(Width, Height);
            view.ZoomTo(new DisplayBox(0, 0, 12, 9), 0.0);
            var options = new CanvasOptions();

            using (SKBitmap picture = CadRasterWriter.Draw(document, space, view, options, null))
            {
                view.DrawingToPixel(0.5, 4.5, out double sx, out double sy);
                SKColor c = picture.GetPixel((int)Math.Round(sx), (int)Math.Round(sy));
                Count(
                    picture,
                    (x, y) =>
                    {
                        view.DrawingToPixel(x, y, out double px, out double py);
                        return new[] { px, py };
                    },
                    (c.Red << 16) | (c.Green << 8) | c.Blue,
                    out outside,
                    out inside
                );
            }
        }

        private static void CountPlot(CadDocument document, CadSpace space, out int outside, out int inside)
        {
            var settings = new CadPlotSettings();
            settings.UseDevice(CadPlotSettings.PngDevice);
            settings.Paper = CadPaper.Pixels(Width, Height);
            settings.Area = CadPlotArea.Window;
            settings.Window = new DisplayBox(0, 0, 12, 9);
            settings.Centered = true;
            string path = Path.Combine(Path.GetTempPath(), "gh-vpclip-" + Guid.NewGuid().ToString("N") + ".png");

            try
            {
                CadPlotPage page = CadRasterWriter.WritePlot(path, document, space, settings, DisplayBox.Empty);

                using (SKBitmap picture = SKBitmap.Decode(path))
                {
                    Assert.Equal(Width, picture.Width);
                    Count(
                        picture,
                        (x, y) => new[] { page.DrawingToSheet.X(x, y), picture.Height - page.DrawingToSheet.Y(x, y) },
                        0xFFFFFF,
                        out outside,
                        out inside
                    );
                }
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void TheImageOfALayout_ShowsTheModelOutsideAnUnclippedViewport_AndNothingOutsideTheCircleOnceItIsClipped()
        {
            int before = 0;
            OnRoom(false, (document, space) => CountImage(document, space, out before, out _));
            Assert.True(
                before > 100,
                "Only " + before + " pixels of the model lie outside the circle before the clip."
            );

            OnRoom(true, (document, space) =>
            {
                CountImage(document, space, out int outside, out int inside);
                Assert.Equal(0, outside);
                Assert.True(inside > 10, "Only " + inside + " pixels of the model lie inside the circle.");
            });
        }

        [Fact]
        public void ThePlotOfALayoutToAPng_ShowsTheModelOutsideAnUnclippedViewport_AndNothingOutsideTheCircleOnceItIsClipped()
        {
            int before = 0;
            OnRoom(false, (document, space) => CountPlot(document, space, out before, out _));
            Assert.True(
                before > 100,
                "Only " + before + " pixels of the model lie outside the circle before the clip."
            );

            OnRoom(true, (document, space) =>
            {
                CountPlot(document, space, out int outside, out int inside);
                Assert.Equal(0, outside);
                Assert.True(inside > 10, "Only " + inside + " pixels of the model lie inside the circle.");
            });
        }
    }
}
