using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// The canvas and its menus against pictures of AutoCAD 2021: SCALE draws the objects faded where they stand and their
    /// scaled copy in their own colour, with one field for the factor; the Shift+right-click menu takes AutoCAD's access keys;
    /// a selection window being dragged leaves the cursor a cross alone, in AutoCAD's colours and edges.
    /// </summary>
    public sealed class AutoCadReferencePicturesTests
    {
        private const int Width = 800;
        private const int Height = 600;

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

        /// <summary>
        /// The blue of the pixels within two rows of a point of the drawing added up: what a level line through it puts there,
        /// however its smoothing shares it between two rows. A white line one pixel wide on black gives about 255.
        /// </summary>
        private static int Ink(int[] pixels, CadCanvas canvas, double x, double y)
        {
            canvas.View.DrawingToPixel(x, y, out double px, out double py);
            int sum = 0;

            for (int row = (int)Math.Round(py) - 2; row <= (int)Math.Round(py) + 2; row++)
            {
                sum += At(pixels, (int)Math.Round(px), row) & 0xFF;
            }

            return sum;
        }

        private static void Move(CadCanvas canvas, Point at) =>
            CanvasMouseTests.Mouse(canvas, UIElement.MouseMoveEvent, at);

        [Fact]
        public void WhileAWindowIsDragged_TheCursorIsTheCrossAlone_AndThePickboxComesBackOnceItEnds()
        {
            OnCanvas(
                (model, canvas, bottom) =>
                {
                    canvas.SelectByDragging = true;
                    canvas.CrosshairPercent = 5;

                    // The left edge of the pickbox, 5 pixels left of the cursor at (400.5, 300.5), three rows below it.
                    var cursor = new Point(400.5, 300.5);

                    foreach (Point from in new[] { new Point(200.5, 150.5), new Point(600.5, 450.5) })
                    {
                        Move(canvas, from);
                        CanvasMouseTests.Mouse(canvas, UIElement.MouseDownEvent, from);
                        Move(canvas, new Point((from.X + cursor.X) / 2, (from.Y + cursor.Y) / 2));
                        Move(canvas, cursor);
                        int dragged = At(Picture(canvas), 395, 303);
                        canvas.CrosshairPercent = 0;
                        int none = At(Picture(canvas), 395, 303);
                        canvas.CrosshairPercent = 5;

                        Assert.Equal(none, dragged);

                        CanvasMouseTests.Mouse(canvas, UIElement.MouseUpEvent, cursor);
                        model.ClearSelection();
                        int ended = At(Picture(canvas), 395, 303);

                        Assert.NotEqual(none, ended);
                    }
                }
            );
        }

        [Fact]
        public void AWindowIsBlueEdgedSolid_ACrossingGreenEdgedDashed_AQuarterStrongOverTheDrawing_AsAutoCadsAre()
        {
            OnCanvas(
                (model, canvas, bottom) =>
                {
                    canvas.SelectByDragging = true;
                    int[] before = Picture(canvas);

                    // Left to right, a window from (200.5, 150.5) to (400.5, 300.5): its top edge is a solid line in the
                    // crosshair's colour, and inside a quarter of (0, 96, 244) over the drawing.
                    Move(canvas, new Point(200.5, 150.5));
                    CanvasMouseTests.Mouse(canvas, UIElement.MouseDownEvent, new Point(200.5, 150.5));
                    Move(canvas, new Point(300.5, 200.5));
                    Move(canvas, new Point(400.5, 300.5));
                    int[] window = Picture(canvas);

                    for (int x = 210; x < 390; x++)
                    {
                        Assert.Equal(0xE6E9ED, At(window, x, 150));
                    }

                    int under = At(before, 300, 250);
                    int Quarter(int shift, int strong) =>
                        (int)Math.Round(0.75 * ((under >> shift) & 0xFF) + 0.25 * strong);
                    int fill = At(window, 300, 250);
                    Assert.InRange((fill >> 16) & 0xFF, Quarter(16, 0x00) - 2, Quarter(16, 0x00) + 2);
                    Assert.InRange((fill >> 8) & 0xFF, Quarter(8, 0x60) - 2, Quarter(8, 0x60) + 2);
                    Assert.InRange(fill & 0xFF, Quarter(0, 0xF4) - 2, Quarter(0, 0xF4) + 2);
                    canvas.Cancel();

                    // Right to left, a crossing: the top edge dashed, 4 on and 2 off, and green inside.
                    Move(canvas, new Point(400.5, 150.5));
                    CanvasMouseTests.Mouse(canvas, UIElement.MouseDownEvent, new Point(400.5, 150.5));
                    Move(canvas, new Point(300.5, 200.5));
                    Move(canvas, new Point(200.5, 300.5));
                    int[] crossing = Picture(canvas);
                    int[] edge = Enumerable.Range(210, 180).Select(x => At(crossing, x, 150)).ToArray();

                    Assert.Contains(0xE6E9ED, edge);
                    Assert.True(
                        edge.Count(p => p == 0xE6E9ED) < edge.Length * 0.8,
                        string.Join(" ", edge.Take(24).Select(p => p.ToString("X6")))
                    );
                    int green = At(crossing, 300, 250);
                    Assert.True(((green >> 8) & 0xFF) > ((green >> 16) & 0xFF) + 40, green.ToString("X6"));
                    canvas.Cancel();
                }
            );
        }

        [Fact]
        public void TheWindowOfSelectObjects_IsDrawnAlone_NoRubberBandOrBasePointInIt()
        {
            OnCanvas(
                (model, canvas, bottom) =>
                {
                    model.SnapModes = CadSnapModes.None;
                    model.Execute("MOVE");
                    model.Execute("W");
                    model.Execute("500,2500");
                    Assert.StartsWith("Specify opposite corner", model.Prompt);
                    canvas.View.DrawingToPixel(500, 2500, out double ax, out double ay);
                    canvas.View.DrawingToPixel(1500, 500, out double bx, out double by);
                    Move(canvas, new Point(bx, by));
                    int[] pixels = Picture(canvas);

                    // Nothing of the room lies in the window: every pixel inside it is the one fill, along the line from
                    // the first corner to the cursor and by the first corner too.
                    int fill = At(pixels, (int)Math.Round((ax + bx) / 2 + 20), (int)Math.Round((ay + by) / 2));
                    for (double t = 0.1; t <= 0.9; t += 0.01)
                    {
                        int x = (int)Math.Round(ax + t * (bx - ax));
                        int y = (int)Math.Round(ay + t * (by - ay));
                        Assert.Equal(fill.ToString("X6"), At(pixels, x, y).ToString("X6"));
                    }

                    Assert.Equal(fill, At(pixels, (int)Math.Round(ax) + 5, (int)Math.Round(ay) + 3));
                    model.Escape();
                }
            );
        }

        [Fact]
        public void ScaleAskingItsFactor_FadesTheObjectsWhereTheyStand_AndDrawsTheScaledCopyInItsOwnColour()
        {
            OnCanvas(
                (model, canvas, bottom) =>
                {
                    model.Execute("SCALE");
                    model.Execute("FENCE");
                    model.Execute("10,-10");
                    model.Execute("10,10");
                    model.Execute(string.Empty);
                    model.Execute(string.Empty);
                    model.Execute("0,0");

                    Assert.StartsWith("Specify scale factor", model.Prompt);
                    Assert.Equal(new[] { bottom }, canvas.Faded());

                    // The cursor half a unit from the base point, no snap holding it there: the copy, half the white line 4 000
                    // long, runs to 2 000.
                    model.SnapModes = CadSnapModes.None;
                    canvas.View.DrawingToPixel(0.0, 0.5, out double px, out double py);
                    Move(canvas, new Point(px, py));
                    int[] pixels = Picture(canvas);

                    // A fifth of white where the line stands alone, as AutoCAD's 40 and 12 over two rows; white where the copy is.
                    int faded = Ink(pixels, canvas, 3000, 0);
                    int copy = Ink(pixels, canvas, 1000, 0);
                    Assert.InRange(faded, 35, 70);
                    Assert.True(copy >= 220, copy.ToString());
                    canvas.View.DrawingToPixel(1000, 0, out double cx, out double cy);
                    int shade = Enumerable
                        .Range((int)Math.Round(cy) - 2, 5)
                        .Select(row => At(pixels, (int)Math.Round(cx), row))
                        .OrderByDescending(p => p & 0xFF)
                        .First();
                    Assert.Equal((shade >> 16) & 0xFF, shade & 0xFF);

                    // Copy keeps the objects where they are: nothing is faded, and DYN shows the factor alone.
                    model.Execute("C");

                    Assert.Empty(canvas.Faded());
                }
            );
        }

        [Fact]
        public void MoveFadesTheObjectsItDrags_CopyDoesNot()
        {
            OnCanvas(
                (model, canvas, bottom) =>
                {
                    foreach (string command in new[] { "MOVE", "COPY" })
                    {
                        model.Document.Selection.Set(new[] { bottom });
                        model.Execute(command);

                        if (model.Prompt.StartsWith("Select objects", StringComparison.Ordinal))
                        {
                            model.Execute(string.Empty);
                        }

                        model.Execute("0,0");

                        Assert.StartsWith("Specify second point", model.Prompt);
                        Assert.Equal(command == "MOVE" ? new[] { bottom } : Array.Empty<string>(), canvas.Faded());
                        model.Escape();
                    }
                }
            );
        }

        [Fact]
        public void AtTheScaleFactor_DynamicInputShowsTheFactorAlone_NotADistanceAndAnAngle()
        {
            using (var folder = new TestFolder())
            {
                var model = new MainViewModel();
                Assert.True(model.Open(folder.Write("room.dxf", TestFiles.Room(out _, out _))));
                model.Execute("SCALE");
                model.Execute("ALL");
                model.Execute(string.Empty);
                model.Execute("1000,1000");
                model.Session.Cursor = new GeometryHelper.Geometry.GeoPoint2(1000.3, 999.6);
                model.SetCursor(1000.3, 999.6);

                Assert.Equal("0.50", model.DynamicInputValue);

                // Reference keeps its own prompts: the new length is a length in a field beside the rubber band.
                model.Execute("R");
                model.Execute("2");
                Assert.StartsWith("Specify new length", model.Prompt);
                model.Session.Cursor = new GeometryHelper.Geometry.GeoPoint2(1300, 1400);
                model.SetCursor(1300, 1400);

                Assert.True(model.IsDynamicInputLength);
            }
        }

        [Fact]
        public void TheShiftRightClickMenu_TakesAutoCadsAccessKeys_FForFrom_EachKeyOnce()
        {
            var model = new MainViewModel();
            using (var folder = new TestFolder())
            {
                Assert.True(model.Open(folder.Write("room.dxf", TestFiles.Room(out _, out _))));
                model.StartCommand("LINE");

                IReadOnlyList<CadMenuItemViewModel> items = model.SnapOverrideMenu();
                Dictionary<string, char> keys = items
                    .Where(i => !i.IsSeparator)
                    .ToDictionary(i => i.Header, i => i.AccessKey);

                Assert.Equal('F', keys["From"]);
                Assert.Equal('k', keys["Temporary track point"]);
                Assert.Equal('2', keys["Mid Between 2 Points"]);
                Assert.Equal('E', keys["Endpoint"]);
                Assert.Equal('M', keys["Midpoint"]);
                Assert.Equal('I', keys["Intersection"]);
                Assert.Equal('A', keys["Apparent Intersection"]);
                Assert.Equal('x', keys["Extension"]);
                Assert.Equal('C', keys["Center"]);
                Assert.Equal('G', keys["Geometric Center"]);
                Assert.Equal('Q', keys["Quadrant"]);
                Assert.Equal('T', keys["Tangent"]);
                Assert.Equal('P', keys["Perpendicular"]);
                Assert.Equal('l', keys["Parallel"]);
                Assert.Equal('d', keys["Node"]);
                Assert.Equal('s', keys["Insertion"]);
                Assert.Equal('r', keys["Nearest"]);
                Assert.Equal('N', keys["None"]);
                Assert.Equal('O', keys["Osnap Settings..."]);

                // No key is given twice, and each is a letter of its item's words.
                char[] given = keys.Values.Where(k => k != '\0').Select(char.ToUpperInvariant).ToArray();
                Assert.Equal(given.Length, given.Distinct().Count());
                Assert.All(keys.Where(k => k.Value != '\0'), k =>
                    Assert.Contains(char.ToUpperInvariant(k.Value), k.Key.ToUpperInvariant())
                );
            }
        }

        [Fact]
        public async System.Threading.Tasks.Task TheScreenshotWithWblockTyped_AsksOnTheCommandLine_AndShowsNoWriteBlockWindow()
        {
            using (var folder = new TestFolder())
            {
                string room = folder.Write("room.dxf", TestFiles.Room(out _, out _));
                string png = System.IO.Path.Combine(folder.FullPath, "wblock.png");
                var bin = new System.IO.DirectoryInfo(
                    AppDomain.CurrentDomain.BaseDirectory.TrimEnd(System.IO.Path.DirectorySeparatorChar)
                );
                string exe = System.IO.Path.Combine(
                    bin.Parent.Parent.Parent.Parent.Parent.FullName,
                    "src",
                    "GeometryHelper.Dxf.App",
                    "bin",
                    bin.Parent.Name,
                    "net8.0-windows",
                    "GeometryHelper.Dxf.App.exe"
                );
                Assert.True(System.IO.File.Exists(exe), exe);
                var start = new System.Diagnostics.ProcessStartInfo(exe)
                {
                    Arguments = "--screenshot \"" + png + "\" \"" + room + "\" --size 1200 800 --run W",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };

                // A Write Block window shown would hold the app open: it is killed after a minute and the test fails.
                using (System.Diagnostics.Process process = System.Diagnostics.Process.Start(start))
                {
                    System.Threading.Tasks.Task<string> output = process.StandardOutput.ReadToEndAsync();
                    System.Threading.Tasks.Task<string> error = process.StandardError.ReadToEndAsync();
                    bool exited = process.WaitForExit(60000);

                    if (!exited)
                    {
                        process.Kill();
                    }

                    Assert.True(exited, "The app was held open, as by a dialog shown.");
                    Assert.Equal(0, process.ExitCode);
                    Assert.Contains("(1200 by 800)", await output + await error);
                }
            }
        }

        [Fact]
        public void TheMenuMadeOfTheItems_MarksEachAccessKeyInItsWords()
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
                        model.StartCommand("LINE");
                        ContextMenu menu = CadMenus.Make(model.SnapOverrideMenu());
                        MenuItem from = menu.Items.OfType<MenuItem>().ElementAt(1);
                        var words = Assert.IsType<AccessText>(from.Header);

                        Assert.Equal("_From", words.Text);
                        Assert.Equal('F', words.AccessKey);

                        // An item with its marker keeps the marker beside words that carry the key: Nearest, "Nea_rest".
                        MenuItem nearest = menu
                            .Items.OfType<MenuItem>()
                            .Single(i =>
                                i.Header is StackPanel row && row.Children[1] is AccessText t && t.Text == "Nea_rest"
                            );
                        Assert.Equal('r', ((AccessText)((StackPanel)nearest.Header).Children[1]).AccessKey);

                        // Point Filters has no key of its own, every letter of it being another item's.
                        Assert.Equal("Point Filters", menu.Items.OfType<MenuItem>().ElementAt(3).Header);
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
