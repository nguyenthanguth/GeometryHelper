using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// The canvas against the second round of the maintainer's AutoCAD 2021 pictures: the implied window asks its opposite
    /// corner on the command line with absolute X and Y fields, F, WP and CP turning it; no square marks a base point; the
    /// cursor badges of a window, a crossing and SCALE; the AutoComplete list five rows tall.
    /// </summary>
    public sealed class AutoCadReferencePictures2Tests
    {
        private const int Width = 800;
        private const int Height = 600;
        private const string Opposite = "Specify opposite corner or [Fence/WPolygon/CPolygon]:";

        /// <summary>Runs a test on an STA thread with the room open in a view model and a canvas of 800 by 600 over it.</summary>
        private static void OnCanvas(Action<MainViewModel, CadCanvas, string> test)
        {
            Exception failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    using (var folder = new TestFolder())
                    {
                        var model = new MainViewModel();
                        Assert.True(model.Open(folder.Write("room.dxf", TestFiles.Room(out string bottom, out _))));
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
                        test(model, canvas, bottom);
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

        /// <summary>The canvas drawn now, as 0xRRGGBB a pixel, row after row.</summary>
        private static int[] Picture(CadCanvas canvas)
        {
            canvas.RenderNow();
            canvas.UpdateLayout();
            var bitmap = new RenderTargetBitmap(Width, Height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(canvas);
            var pixels = new int[Width * Height];
            bitmap.CopyPixels(pixels, Width * 4, 0);
            return pixels.Select(p => p & 0xFFFFFF).ToArray();
        }

        private static int At(int[] pixels, int x, int y) => pixels[y * Width + x];

        private static Point Pixel(CadCanvas canvas, double x, double y)
        {
            canvas.View.DrawingToPixel(x, y, out double px, out double py);
            return new Point(Math.Round(px) + 0.5, Math.Round(py) + 0.5);
        }

        private static void Move(CadCanvas canvas, Point at) =>
            CanvasMouseTests.Mouse(canvas, UIElement.MouseMoveEvent, at);

        private static void Click(CadCanvas canvas, Point at)
        {
            Move(canvas, at);
            CanvasMouseTests.Mouse(canvas, UIElement.MouseDownEvent, at);
            CanvasMouseTests.Mouse(canvas, UIElement.MouseUpEvent, at);
        }

        private static double Number(string field) =>
            double.Parse(Regex.Replace(field, "[^0-9.-]", string.Empty), CultureInfo.InvariantCulture);

        [Fact]
        public void AClickOnNothing_AsksTheOppositeCornerWithItsOptions_AndDynamicInputShowsTheCursorsXAndY()
        {
            OnCanvas(
                (model, canvas, bottom) =>
                {
                    model.SnapModes = CadSnapModes.None;
                    Click(canvas, Pixel(canvas, 500, 2500));

                    Assert.Equal(Opposite, model.Prompt);
                    Assert.True(model.IsCommandRunning);

                    // As AutoCAD's 4826.511 and 915.8892: the cursor's own X and Y, not the way from the first corner.
                    model.SetCursor(1234.5, 678.25);

                    Assert.True(model.IsDynamicInputFields);
                    Assert.False(model.IsDynamicInputPolar);
                    Assert.Equal("Specify opposite corner or", model.DynamicInputPrompt);
                    Assert.Equal(1234.5, Number(model.DynamicInputFirst), 1);
                    Assert.Equal(678.25, Number(model.DynamicInputSecond), 1);

                    // The click at the other corner ends it: nothing of the room lies in the box, and the history keeps the prompt.
                    Click(canvas, Pixel(canvas, 1500, 500));

                    Assert.False(model.IsCommandRunning);
                    Assert.Empty(model.Document.Selection.Handles);
                    Assert.Contains("Command: " + Opposite, model.Session.History);
                }
            );
        }

        [Fact]
        public void AWindowAroundTheBottomLine_SelectsIt_AsBefore()
        {
            OnCanvas(
                (model, canvas, bottom) =>
                {
                    model.SnapModes = CadSnapModes.None;
                    Click(canvas, Pixel(canvas, -300, 300));
                    Click(canvas, Pixel(canvas, 4300, -300));

                    Assert.Contains(bottom, model.Document.Selection.Handles);
                    Assert.False(model.IsCommandRunning);
                }
            );
        }

        [Fact]
        public void FTypedAtTheOppositeCorner_TurnsTheBoxIntoAFenceFromTheCorner()
        {
            OnCanvas(
                (model, canvas, bottom) =>
                {
                    model.SnapModes = CadSnapModes.None;
                    Click(canvas, Pixel(canvas, 500, 2500));
                    model.Execute("F");

                    Assert.Equal("Specify next fence point or [Undo]:", model.Prompt);

                    Click(canvas, Pixel(canvas, 500, -300));
                    model.Execute(string.Empty);

                    Assert.Equal(new[] { bottom }, model.Document.Selection.Handles.ToArray());
                }
            );
        }

        [Fact]
        public void EscWithTheWindowOpen_CancelsIt_AsAutoCadWritesIt()
        {
            OnCanvas(
                (model, canvas, bottom) =>
                {
                    Click(canvas, Pixel(canvas, 500, 2500));
                    canvas.Cancel();

                    Assert.False(model.IsCommandRunning);
                    Assert.Equal("Command: " + Opposite + " *Cancel*", model.Session.History.Last());
                }
            );
        }

        [Fact]
        public void ScaleAskingItsFactor_DrawsNoSquareAtTheBasePoint()
        {
            OnCanvas(
                (model, canvas, bottom) =>
                {
                    model.SnapModes = CadSnapModes.None;
                    model.Document.Selection.Set(new[] { bottom });
                    model.Execute("SCALE");
                    model.Execute("2000,1500");

                    Assert.StartsWith("Specify scale factor", model.Prompt);

                    Move(canvas, Pixel(canvas, 3000, 2000));
                    int[] pixels = Picture(canvas);
                    Point from = Pixel(canvas, 2000, 1500);
                    int greens = 0;

                    for (int y = (int)from.Y - 7; y <= (int)from.Y + 7; y++)
                    {
                        for (int x = (int)from.X - 7; x <= (int)from.X + 7; x++)
                        {
                            int p = At(pixels, x, y);
                            int r = (p >> 16) & 0xFF;
                            int g = (p >> 8) & 0xFF;
                            int b = p & 0xFF;
                            greens += g > r + 50 && g > b + 30 ? 1 : 0;
                        }
                    }

                    Assert.Equal(0, greens);
                    model.Escape();
                }
            );
        }

        [Fact]
        public void TheCursorCarriesAutoCadsBadge_BlueForAWindow_GreenForACrossing_TheScaleSquareForScale()
        {
            OnCanvas(
                (model, canvas, bottom) =>
                {
                    canvas.SelectByDragging = true;
                    canvas.CrosshairPercent = 5;
                    var cursor = new Point(400.5, 300.5);

                    // A window: the badge, 9 across with its corner 12 right of the cursor and 21 above, filled blue.
                    Move(canvas, new Point(200.5, 150.5));
                    CanvasMouseTests.Mouse(canvas, UIElement.MouseDownEvent, new Point(200.5, 150.5));
                    Move(canvas, new Point(300.5, 200.5));
                    Move(canvas, cursor);
                    Assert.Equal(0x0060F4, At(Picture(canvas), 417, 284));
                    canvas.Cancel();

                    // A crossing: a green middle.
                    Move(canvas, new Point(600.5, 450.5));
                    CanvasMouseTests.Mouse(canvas, UIElement.MouseDownEvent, new Point(600.5, 450.5));
                    Move(canvas, new Point(500.5, 400.5));
                    Move(canvas, cursor);
                    int[] crossing = Picture(canvas);
                    Assert.Equal(0x00F430, At(crossing, 417, 284));
                    Assert.NotEqual(0x0060F4, At(crossing, 414, 282));
                    canvas.Cancel();

                    // No box, no badge.
                    Move(canvas, cursor);
                    Assert.NotEqual(0x0060F4, At(Picture(canvas), 417, 284));

                    // SCALE: a small square filled in the badge's lower left corner, in the crosshair's colour.
                    model.Document.Selection.Set(new[] { bottom });
                    model.Execute("SCALE");
                    Move(canvas, cursor);
                    int[] scale = Picture(canvas);
                    Assert.Equal(0xE6E9ED, At(scale, 414, 287));
                    Assert.Equal(0xE6E9ED, At(scale, 421, 283));
                    model.Escape();
                }
            );
        }

        [Fact]
        public void TheAutoCompleteList_ShowsFiveRowsAtATime_TheRestScrolledTo()
        {
            Exception failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    using (var folder = new TestFolder())
                    {
                        var model = new MainViewModel();
                        Assert.True(model.Open(folder.Write("room.dxf", TestFiles.Room(out _, out _))));
                        model.CommandText = "A";
                        model.ShowSuggestions();
                        Assert.True(model.Suggestions.Count > 10);

                        var list = new CommandSuggestionList { DataContext = model };
                        list.Measure(new Size(380, 1000));
                        list.Arrange(new Rect(0, 0, 380, list.DesiredSize.Height));
                        list.UpdateLayout();

                        var scroller = (ScrollViewer)list.FindName("Scroller");
                        var rows = (ItemsControl)list.FindName("Rows");
                        var sixth = (FrameworkElement)rows.ItemContainerGenerator.ContainerFromIndex(5);
                        double five = sixth.TranslatePoint(new Point(0, 0), rows).Y;

                        Assert.Equal(five, scroller.ViewportHeight, 1);
                        Assert.True(scroller.ExtentHeight > 2 * five, scroller.ExtentHeight + " " + five);
                        Assert.True(list.DesiredSize.Height < five + 10, list.DesiredSize.Height + " " + five);
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
    }
}
