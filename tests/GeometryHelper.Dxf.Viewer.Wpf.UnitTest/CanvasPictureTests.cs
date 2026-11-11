using System;
using System.Diagnostics;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// The canvas's kept picture of the drawing, offscreen on an STA thread: the entity under the cursor drawn over it
    /// without drawing the drawing again, the picture moved with the wheel until the view is still, and boxes clicked
    /// corner to corner as AutoCAD takes them with PICKDRAG 0.
    /// </summary>
    public class CanvasPictureTests
    {
        private const int Width = 800;
        private const int Height = 600;

        /// <summary>Runs a test on an STA thread with the room open in a view model and a canvas of 800 by 600 over it, at 0.18 pixels to the unit.</summary>
        private static void OnCanvas(Action<MainViewModel, CadCanvas, string, string> test)
        {
            Exception failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    using (var folder = new TestFolder())
                    {
                        var model = new MainViewModel();
                        model.Open(folder.Write("room.dxf", TestFiles.Room(out string bottom, out string column)));
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
                        test(model, canvas, bottom, column);
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

        /// <summary>The canvas as the screen shows it, its picture and what it draws over the picture, as 0xRRGGBB by row.</summary>
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

        private static Point At(CadCanvas canvas, double x, double y) => CanvasMouseTests.At(canvas, x, y);

        [Fact]
        public void AnEntityUnderTheCursorIsDrawnOverTheKeptPictureWithoutDrawingTheDrawingAgain()
        {
            OnCanvas(
                (model, canvas, bottom, column) =>
                {
                    int scenes = canvas.SceneRenders;

                    // The bottom wall, white, along the border of the rows 569 and 570: hovered, both rows turn a light blue.
                    canvas.MoveCursorTo(1000, 0);
                    canvas.RenderNow();
                    int[] pixels = Pixels(canvas);

                    Assert.Equal(scenes, canvas.SceneRenders);
                    Assert.Equal(0x9ECDFF, pixels[569 * Width + 220]);
                    Assert.Equal(0x9ECDFF, pixels[570 * Width + 220]);

                    // The cursor moved off it: the wall is white again, still from the same picture.
                    canvas.MoveCursorTo(1000, 1000);
                    canvas.RenderNow();
                    pixels = Pixels(canvas);

                    Assert.Equal(scenes, canvas.SceneRenders);
                    Assert.NotEqual(0x9ECDFF, pixels[570 * Width + 220]);
                }
            );
        }

        [Fact]
        public void TheWheelMovesAndScalesThePictureAndTheDrawingIsDrawnAgainOnlyOnceTheViewIsStill()
        {
            OnCanvas(
                (model, canvas, bottom, column) =>
                {
                    int scenes = canvas.SceneRenders;

                    // A notch in about (400, 500): the wall at row 570 is shown at 500 + 70 x 1.2 = 584 before it is drawn again.
                    CanvasMouseTests.Mouse(canvas, UIElement.MouseWheelEvent, new Point(400, 500), delta: 120);
                    int[] pixels = Pixels(canvas);

                    Assert.Equal(scenes, canvas.SceneRenders);
                    Assert.True(canvas.IsSettling);
                    Assert.NotEqual(0, pixels[584 * Width + 184]);
                    Assert.Equal(0, pixels[570 * Width + 220]);

                    canvas.RenderNow();

                    Assert.Equal(scenes + 1, canvas.SceneRenders);
                    Assert.False(canvas.IsSettling);
                }
            );
        }

        [Fact]
        public void AClickOnNothingStartsABoxThatTheNextClickEndsLeftToRightAWindowRightToLeftACrossing()
        {
            OnCanvas(
                (model, canvas, bottom, column) =>
                {
                    // Left to right about the column of radius 100 at (2000, 1500): a window that holds it whole.
                    CanvasMouseTests.Click(canvas, At(canvas, 1850, 1350));

                    Assert.Empty(model.Document.Selection.Handles);

                    CanvasMouseTests.Click(canvas, At(canvas, 2150, 1650));

                    Assert.Equal(new[] { column }, model.Document.Selection.Handles);

                    // Left to right over its right half only: a window, which takes nothing.
                    model.ClearSelection();
                    CanvasMouseTests.Click(canvas, At(canvas, 2000, 1440));
                    CanvasMouseTests.Click(canvas, At(canvas, 2160, 1500));

                    Assert.Empty(model.Document.Selection.Handles);

                    // The same box right to left: a crossing, which takes the column its rim runs through.
                    CanvasMouseTests.Click(canvas, At(canvas, 2160, 1440));
                    CanvasMouseTests.Click(canvas, At(canvas, 2000, 1500));

                    Assert.Equal(new[] { column }, model.Document.Selection.Handles);
                }
            );
        }

        [Fact]
        public void EscDropsABoxWaitingForItsSecondCornerAndTheNextClickStartsAnother()
        {
            OnCanvas(
                (model, canvas, bottom, column) =>
                {
                    CanvasMouseTests.Click(canvas, At(canvas, 1850, 1350));
                    canvas.Cancel();
                    CanvasMouseTests.Click(canvas, At(canvas, 2150, 1650));

                    Assert.Empty(model.Document.Selection.Handles);

                    // That click was a first corner: the box it started ends at the next.
                    CanvasMouseTests.Click(canvas, At(canvas, 1850, 1350));

                    Assert.Equal(new[] { column }, model.Document.Selection.Handles);
                }
            );
        }

        [Fact]
        public void ABoxDraggedEndsOnReleaseOnlyWithSelectByDraggingElseItWaitsForAClick()
        {
            OnCanvas(
                (model, canvas, bottom, column) =>
                {
                    // PICKDRAG 0, the default: the release leaves the box open; the click at the far corner ends it.
                    CanvasMouseTests.Drag(canvas, At(canvas, 1850, 1350), At(canvas, 2150, 1650));

                    Assert.Empty(model.Document.Selection.Handles);

                    CanvasMouseTests.Click(canvas, At(canvas, 2150, 1650));

                    Assert.Equal(new[] { column }, model.Document.Selection.Handles);

                    // PICKDRAG 1: the release ends it.
                    model.ClearSelection();
                    canvas.SelectByDragging = true;
                    CanvasMouseTests.Drag(canvas, At(canvas, 1850, 1350), At(canvas, 2150, 1650));

                    Assert.Equal(new[] { column }, model.Document.Selection.Handles);
                }
            );
        }

        /// <summary>
        /// Half a million items, 5 000 INSERTs of a block of 100 lines, shown whole: a change of the entity under the cursor
        /// costs a copy of the kept picture and the 100 lines of one INSERT. The limit is three times the target of 16 ms, so
        /// the test guards against a fall that is not noise; drawing the whole drawing again, as each hover once did, takes
        /// hundreds of milliseconds.
        /// </summary>
        [Fact]
        public void AHoverChangeOverHalfAMillionItemsTakesWithinThreeTimesItsTargetOfSixteenMilliseconds()
        {
            Exception failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    var database = new DxfDatabase("AC1027");
                    var block = new DxfBlockTableRecord { Name = "STRIPES" };
                    database.BlockTable.Add(block);

                    for (int i = 0; i < 100; i++)
                    {
                        block.AppendEntity(
                            new DxfLine(new GeoPoint3(0, i * 0.1, 0), new GeoPoint3(10, i * 0.1 + 2, 0))
                        );
                    }

                    for (int i = 0; i < 5000; i++)
                    {
                        database.ModelSpace.AppendEntity(
                            new DxfBlockReference(new GeoPoint3((i % 100) * 20.0, (i / 100) * 20.0, 0), block)
                        );
                    }

                    CadDocument document = CadDocument.From(database);
                    DisplayList2 list = document.GetDisplay(document.ModelSpace);

                    Assert.Equal(500000, list.Count);

                    var view = new CadView(Width, Height);
                    var canvas = new CadCanvas
                    {
                        Document = document,
                        Space = document.ModelSpace,
                        View = view,
                    };
                    canvas.Measure(new Size(Width, Height));
                    canvas.Arrange(new Rect(0, 0, Width, Height));
                    view.ZoomTo(list.Bounds);
                    canvas.RenderNow();
                    int scenes = canvas.SceneRenders;
                    var watch = Stopwatch.StartNew();

                    // Twenty INSERTs one after another under the cursor, each at the middle of its stripes.
                    for (int k = 0; k < 20; k++)
                    {
                        int i = 37 + 241 * k;
                        canvas.MoveCursorTo((i % 100) * 20.0 + 5.0, (i / 100) * 20.0 + 6.0);
                        canvas.RenderNow();
                    }

                    double each = watch.Elapsed.TotalMilliseconds / 20.0;

                    Assert.Equal(scenes, canvas.SceneRenders);
                    Assert.True(
                        each < 48.0,
                        each.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " ms a hover change"
                    );
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
    }
}
