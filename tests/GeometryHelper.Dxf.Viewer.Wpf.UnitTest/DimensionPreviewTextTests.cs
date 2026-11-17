using System;
using System.Collections.Generic;
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
    /// What the canvas draws under the cursor while a dimension is placed, offscreen on an STA thread: the words are the
    /// measured value's letters, as the finished dimension draws them, and not the empty box of their frame.
    /// </summary>
    public class DimensionPreviewTextTests
    {
        private const int Width = 800;
        private const int Height = 600;

        /// <summary>
        /// Runs a test on an STA thread with the room (4000 by 3000) open and a canvas of 800 by 600 over it, DIMSCALE 40 so
        /// that words 100 high stand 18 pixels high at 0.18 pixels to the unit.
        /// </summary>
        private static void OnCanvas(Action<MainViewModel, CadCanvas> test)
        {
            Exception failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    using (var folder = new TestFolder())
                    {
                        DxfDatabase database = TestFiles.Room(out _, out _);
                        database.Dimscale = 40.0;
                        var model = new MainViewModel();
                        model.Open(folder.Write("room.dxf", database));
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

        /// <summary>The texts the preview at the cursor draws, a text of several runs by its runs.</summary>
        private static List<DisplayText2> Words(IEnumerable<DisplayItem2> items)
        {
            var words = new List<DisplayText2>();

            foreach (DisplayItem2 item in items)
            {
                if (item is DisplayText2 text)
                {
                    if (text.Parts != null)
                    {
                        words.AddRange(Words(text.Parts));
                    }
                    else
                    {
                        words.Add(text);
                    }
                }
            }

            return words;
        }

        /// <summary>
        /// Counts the pixels of the box of a text that are not the background, the box drawn in from its sides by 3 pixels so
        /// that its own frame, drawn in a line a pixel wide, is not counted.
        /// </summary>
        private static int InkInside(CadCanvas canvas, int[] pixels, DisplayBox box)
        {
            canvas.View.DrawingToPixel(box.MinX, box.MaxY, out double left, out double top);
            canvas.View.DrawingToPixel(box.MaxX, box.MinY, out double right, out double bottom);
            int background = pixels[2 * Width + 2];
            int ink = 0;

            for (int y = (int)Math.Ceiling(top) + 3; y <= (int)bottom - 3; y++)
            {
                for (int x = (int)Math.Ceiling(left) + 3; x <= (int)right - 3; x++)
                {
                    if (pixels[y * Width + x] != background)
                    {
                        ink++;
                    }
                }
            }

            return ink;
        }

        [Theory]
        [InlineData("DIMLINEAR|0,500|4000,500", 2000, 1200, "4000", 150)]
        [InlineData("DIMALIGNED|0,500|4000,500", 2000, 1200, "4000", 150)]
        [InlineData("DIMRADIUS|2100,1500", 2600, 2200, "R100", 400)]
        [InlineData("DIMDIAMETER|2100,1500", 2600, 2200, "Ø200", 400)]
        [InlineData("DIMANGULAR||1000,1000|3000,1000|1000,2500", 2000, 1900, "", 200)]
        [InlineData("DIMORDINATE|1000,1000", 1000, 2200, "1000", 150)]
        public void TheWordsOfADimensionBeingPlaced_AreDrawnAsLetters_NotAsTheirBox(
            string steps,
            double x,
            double y,
            string expected,
            int least
        )
        {
            OnCanvas(
                (model, canvas) =>
                {
                    foreach (string step in steps.Split('|'))
                    {
                        model.Execute(step);
                    }

                    canvas.MoveCursorTo(x, y);
                    canvas.RenderNow();

                    List<DisplayText2> texts = Words(model.Session.Prompt.Preview.Items(new GeoPoint2(x, y)));
                    DisplayText2 measured = Assert.Single(texts);

                    if (expected.Length > 0)
                    {
                        Assert.Equal(expected, measured.Text);
                    }

                    int[] pixels = Pixels(canvas);
                    int ink = InkInside(canvas, pixels, measured.Bounds);

                    Assert.True(ink > least, measured.Text + ": " + ink);
                }
            );
        }
    }
}
