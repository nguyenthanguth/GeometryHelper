using System;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.Wpf.UnitTest.CanvasMouseTests;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// A viewport clipped by an entity (VPCLIP) on the canvas, offscreen on an STA thread: the room's viewport moved onto the
    /// sheet of 12 by 9, 8 by 6 about (6, 4.5), showing the room at 1:500 about (2 000, 1 500), the bottom wall at y = 1.5, the
    /// top wall at y = 7.5 and the column of radius 0.2 at its middle. Nothing of the model, nor of the viewport's grid, shows outside the
    /// boundary; a double click works through the viewport only inside it.
    /// </summary>
    public class CanvasViewportClipTests
    {
        private const int Width = 800;
        private const int Height = 600;

        /// <summary>An L of 7 by 5 inside the viewport: the upper right corner left out, from x = 7 and y = 4 up.</summary>
        private static readonly double[] L = { 2.5, 2, 9.5, 2, 9.5, 4, 7, 4, 7, 7, 2.5, 7 };

        private static void OnClippedLayout(
            Func<DxfViewport, DxfEntity> boundary,
            bool grid,
            Action<MainViewModel, CadCanvas, DxfViewport> test
        )
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

                        if (grid)
                        {
                            frame.GridIncrement = new GeoVector2(200, 200);
                            frame.GridAdaptive = false;
                            frame.GridBoundToLimits = false;
                            frame.GridOn = true;
                        }

                        if (boundary != null)
                        {
                            DxfEntity made = boundary(frame);
                            ((DxfBlockTableRecord)frame.Owner).AppendEntity(made);
                            frame.ClipTo(made);
                        }

                        var model = new MainViewModel();
                        model.Open(folder.Write("room.dxf", db));
                        model.SelectSpace(model.Document.Spaces.Single(s => s.Name == "Layout1"));
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
                        model.Toggles.Single(t => t.Name == "OSNAP").IsOn = false;
                        test(model, canvas, (DxfViewport)model.Document.FindEntity(handle));
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

        private static DxfEntity LShape(DxfViewport frame)
        {
            var shape = new DxfPolyline();

            for (int i = 0; i < L.Length / 2; i++)
            {
                shape.AddVertexAt(i, new GeoPoint2(L[2 * i], L[2 * i + 1]), 0.0, 0.0, 0.0);
            }

            shape.Closed = true;
            return shape;
        }

        private static DxfEntity Round(DxfViewport frame) =>
            new DxfCircle(new GeoPoint3(6, 4.5, 0), new GeoVector3(0, 0, 1), 2.4);

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

        /// <summary>
        /// The pixels inside the viewport's old rectangle (50 to 250, 25 to 175 on the sheet) that lie farther than three pixels
        /// outside an outline, in the colour of the paper or not: how many of each.
        /// </summary>
        private static void Count(
            CadCanvas canvas,
            int[] pixels,
            DisplayClip2 outline,
            out int outsideNotPaper,
            out int insideNotPaper
        )
        {
            canvas.View.DrawingToPixel(1, 4.5, out double sx, out double sy);
            int paper = pixels[(int)Math.Round(sy) * Width + (int)Math.Round(sx)] & 0xFFFFFF;
            double margin = 3.0 / canvas.View.PixelsPerUnit;
            outsideNotPaper = 0;
            insideNotPaper = 0;

            for (int iy = 0; iy < Height; iy++)
            {
                for (int ix = 0; ix < Width; ix++)
                {
                    canvas.View.ScreenToDrawing(ix, iy, out double x, out double y);

                    if (x < 1.9 || x > 10.1 || y < 1.4 || y > 7.6)
                    {
                        continue;
                    }

                    bool inside = outline.Contains(x, y);
                    double edge = outline.DistanceToEdge(x, y);

                    if (edge <= margin)
                    {
                        continue;
                    }

                    bool notPaper = (pixels[iy * Width + ix] & 0xFFFFFF) != paper;

                    if (inside && notPaper)
                    {
                        insideNotPaper++;
                    }
                    else if (!inside && notPaper)
                    {
                        outsideNotPaper++;
                    }
                }
            }
        }

        [Fact]
        public void TheModelShowsOutsideTheRectangleOfAnUnclippedViewport_AndNothingOutsideAnLShapeOnceItIsClipped()
        {
            var outline = new DisplayClip2(L, new double[L.Length / 2]);
            int before = 0;

            OnClippedLayout(null, false, (model, canvas, viewport) =>
            {
                Count(canvas, Pixels(canvas), outline, out before, out _);
            });

            // Without the clip the walls at y = 25 and 175 and the column show outside the L.
            Assert.True(before > 100, "Only " + before + " pixels of the model lie outside the L before the clip.");

            OnClippedLayout(LShape, false, (model, canvas, viewport) =>
            {
                Assert.True(viewport.NonRectClipOn);
                Count(canvas, Pixels(canvas), outline, out int outside, out int inside);

                Assert.Equal(0, outside);

                // Inside the L the column and the walls' ends are still drawn.
                Assert.True(inside > 20, "Only " + inside + " pixels of the model lie inside the L.");
            });
        }

        [Fact]
        public void ACircleClip_KeepsTheModelWithinItsEdge_ArcsAndAll()
        {
            var round = new DisplayClip2(new[] { 8.4, 4.5, 3.6, 4.5 }, new[] { 1.0, 1.0 });

            OnClippedLayout(Round, false, (model, canvas, viewport) =>
            {
                Assert.Equal(4.8, viewport.Width, 9);
                Assert.Equal(4.8, viewport.Height, 9);
                Count(canvas, Pixels(canvas), round, out int outside, out int inside);

                Assert.Equal(0, outside);
                Assert.True(inside > 10);
            });
        }

        [Fact]
        public void TheGridOfAClippedViewport_StaysInsideTheOutline_AsItsModelDoes()
        {
            var outline = new DisplayClip2(L, new double[L.Length / 2]);
            int before = 0;

            OnClippedLayout(null, true, (model, canvas, viewport) =>
            {
                Count(canvas, Pixels(canvas), outline, out before, out _);
            });

            Assert.True(
                before > 500,
                "Only " + before + " pixels of grid and model lie outside the L before the clip."
            );

            OnClippedLayout(LShape, true, (model, canvas, viewport) =>
            {
                Count(canvas, Pixels(canvas), outline, out int outside, out int inside);

                Assert.Equal(0, outside);
                Assert.True(inside > 500, "Only " + inside + " pixels of grid lie inside the L.");
            });
        }

        [Fact]
        public void ADoubleClickWorksThroughTheViewportInsideTheOutline_AndNotInTheCornerLeftOut()
        {
            OnClippedLayout(LShape, false, (model, canvas, viewport) =>
            {
                // The corner of the rectangle the L leaves out.
                Assert.False(canvas.ViewportClick(At(canvas, 8.5, 5.5), true));
                Assert.Null(canvas.ThroughHandle);

                Assert.True(canvas.ViewportClick(At(canvas, 4, 3), true));
                Assert.Equal(viewport.Handle, canvas.ThroughHandle);
            });
        }

        [Fact]
        public void AClickInTheCornerLeftOut_IsTakenByNothing_WhileTheViewportIsWorkedThrough()
        {
            OnClippedLayout(LShape, false, (model, canvas, viewport) =>
            {
                Assert.True(canvas.ViewportClick(At(canvas, 4, 3), true));

                // Through the viewport, a click off the outline is taken by nothing, as a click off the frame is.
                Assert.True(canvas.ViewportClick(At(canvas, 8.5, 5.5), false));
                Assert.Equal(viewport.Handle, canvas.ThroughHandle);
                Assert.False(canvas.ViewportClick(At(canvas, 4, 3), false));
            });
        }

        private static int PixelAt(int[] pixels, CadCanvas canvas, double x, double y, double dx = 0.0)
        {
            canvas.View.DrawingToPixel(x, y, out double px, out double py);
            return pixels[(int)Math.Round(py) * Width + (int)Math.Round(px + dx)] & 0xFFFFFF;
        }

        /// <summary>Whether a pixel within two of a point is neither grey nor white: the colour of a highlight, not of the black lines of the drawing.</summary>
        private static bool Coloured(int[] pixels, CadCanvas canvas, double x, double y)
        {
            canvas.View.DrawingToPixel(x, y, out double px, out double py);

            for (int dy = -2; dy <= 2; dy++)
            {
                for (int dx = -2; dx <= 2; dx++)
                {
                    int c = pixels[((int)Math.Round(py) + dy) * Width + (int)Math.Round(px) + dx];
                    int r = (c >> 16) & 0xFF;
                    int g = (c >> 8) & 0xFF;
                    int b = c & 0xFF;

                    if (Math.Abs(r - b) > 40 || Math.Abs(g - b) > 40)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        [Fact]
        public void TheViewportWorkedThrough_HasItsThickFrameDrawnAlongTheOutline_NotAroundTheRectangle()
        {
            OnClippedLayout(LShape, false, (model, canvas, viewport) =>
            {
                Assert.True(canvas.ViewportClick(At(canvas, 4, 3), true));
                int[] pixels = Pixels(canvas);
                int paper = PixelAt(pixels, canvas, 1, 4.5);

                // The frame is 3 pixels thick in the ink that shows on the paper: a pixel a little way off the inner edge of the
                // L, x = 7, carries it, and the corner of the rectangle the L leaves out and the middle of its top side do not.
                Assert.NotEqual(paper, PixelAt(pixels, canvas, 7, 5.5, 1.0));
                Assert.Equal(paper, PixelAt(pixels, canvas, 10, 7.5));
                Assert.Equal(paper, PixelAt(pixels, canvas, 8.5, 7.5));
            });
        }

        [Fact]
        public void ASelectedClippedViewport_IsHighlightedAlongTheOutline_NotAroundTheRectangle()
        {
            OnClippedLayout(LShape, false, (model, canvas, viewport) =>
            {
                int paper = PixelAt(Pixels(canvas), canvas, 1, 4.5);
                model.Document.Selection.Set(new[] { viewport.Handle });
                int[] pixels = Pixels(canvas);

                Assert.True(Coloured(pixels, canvas, 9.5, 3), "No highlight on the right side of the L.");
                Assert.True(Coloured(pixels, canvas, 7, 5.5), "No highlight on the inner side of the L.");
                Assert.Equal(paper, PixelAt(pixels, canvas, 8.5, 7.5));
                Assert.Equal(paper, PixelAt(pixels, canvas, 10, 7.5));
            });
        }
    }
}
