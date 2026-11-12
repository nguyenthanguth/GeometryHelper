using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// The grips of a polyline as AutoCAD 2021 draws them: a bar along the middle of a segment, the squares of the vertices
    /// sized by GRIPSIZE, the grip under the cursor pink, and the hover menu that opens once the cursor has rested on a grip.
    /// </summary>
    public sealed class GripHoverCanvasTests : IDisposable
    {
        private const int Width = 800;
        private const int Height = 600;
        private const int Blue = 0x3D9BFF;
        private const int Edge = 0x5A5A5A;
        private const int Pink = 0xFF7F7F;
        private const int Red = 0xF85149;
        private const int MenuFill = 0xF0F0F0;
        private const int MenuRow = 0xCCE8FF;

        private readonly TestFolder _folder = new TestFolder();

        /// <inheritdoc/>
        public void Dispose() => _folder.Dispose();

        /// <summary>
        /// Runs a test on an STA thread: a polyline of the vertices (x, y, bulge each) at handle "P" selected under a canvas of
        /// 800 by 600 showing -20,-20 to 120,120.
        /// </summary>
        private static void OnCanvas(bool closed, double[] xyBulge, Action<MainViewModel, CadCanvas> test)
        {
            Exception failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    var database = new DxfDatabase("AC1027");
                    var polyline = new DxfPolyline();

                    for (int i = 0; i + 2 < xyBulge.Length; i += 3)
                    {
                        polyline.AddVertexAt(i / 3, new GeoPoint2(xyBulge[i], xyBulge[i + 1]), xyBulge[i + 2], 0, 0);
                    }

                    polyline.Closed = closed;
                    database.ModelSpace.AppendEntity(polyline);
                    var line = new DxfLine(new GeoPoint3(0, 110, 0), new GeoPoint3(100, 110, 0));
                    database.ModelSpace.AppendEntity(line);
                    var model = new MainViewModel();
                    model.OpenDocument(CadDocument.From(database, "grips.dxf"), "grips.dxf");
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

                    // The selected object is drawn in its own colour, not the grips' blue, so that the grips can be measured.
                    canvas.Options.SelectionColor = 0xD29922;
                    model.Document.Selection.Set(new[] { polyline.Handle });
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

        /// <summary>An open polyline (0, 0), (100, 0), (100, 100), all straight.</summary>
        private static void OnElbow(Action<MainViewModel, CadCanvas> test) =>
            OnCanvas(false, new double[] { 0, 0, 0, 100, 0, 0, 100, 100, 0 }, test);

        private static Point At(CadCanvas canvas, double x, double y) => CanvasMouseTests.At(canvas, x, y);

        private static void Hover(CadCanvas canvas, Point at) =>
            CanvasMouseTests.Mouse(canvas, UIElement.MouseMoveEvent, at);

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

        /// <summary>The box (width, height) of the pixels of the given colours within 40 pixels of a point; 0 by 0 where there are none.</summary>
        private static Size Extent(int[] pixels, Point at, params int[] colours)
        {
            int left = int.MaxValue,
                right = int.MinValue,
                top = int.MaxValue,
                bottom = int.MinValue;

            for (int y = (int)at.Y - 40; y <= (int)at.Y + 40; y++)
            {
                for (int x = (int)at.X - 40; x <= (int)at.X + 40; x++)
                {
                    if (
                        x < 0
                        || y < 0
                        || x >= Width
                        || y >= Height
                        || !colours.Contains(pixels[y * Width + x] & 0xFFFFFF)
                    )
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

        /// <summary>Lets the dispatcher run for a time, as a window's does while the cursor rests.</summary>
        private static void Rest(int milliseconds)
        {
            var frame = new DispatcherFrame();
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(milliseconds) };
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                frame.Continue = false;
            };
            timer.Start();
            Dispatcher.PushFrame(frame);
        }

        [Theory]
        [InlineData(1, 7, 10, 3)]
        [InlineData(3, 11, 16, 4)]
        [InlineData(5, 15, 22, 6)]
        [InlineData(8, 21, 31, 8)]
        [InlineData(12, 29, 43, 12)]
        public void TheSquareAndTheBarAtEachGripSize_AreTheDesignsSizes(int size, int square, int length, int thickness)
        {
            Assert.Equal(square, GripShapes.Square(size));
            Assert.Equal(length, GripShapes.BarLength(size));
            Assert.Equal(thickness, GripShapes.BarThickness(size));
        }

        [Fact]
        public void TheVerticesAreSquaresOf15_TheSegmentMiddlesBarsOf22By6_AlongTheSegment()
        {
            OnElbow(
                (model, canvas) =>
                {
                    int[] pixels = Pixels(canvas);

                    Assert.Equal(new Size(15, 15), Extent(pixels, At(canvas, 0, 0), Blue, Edge));
                    Assert.Equal(new Size(15, 15), Extent(pixels, At(canvas, 100, 100), Blue, Edge));

                    // Along x for the segment to (100, 0); along y for the one up to (100, 100).
                    Assert.Equal(new Size(22, 6), Extent(pixels, At(canvas, 50, 0), Blue, Edge));
                    Assert.Equal(new Size(6, 22), Extent(pixels, At(canvas, 100, 50), Blue, Edge));

                    // A square is edged in dark grey and filled blue.
                    Point corner = At(canvas, 0, 0);
                    Assert.Equal(Blue, Pixel(pixels, corner, 0, 0));
                    Assert.Equal(new Size(15, 15), Extent(pixels, corner, Edge));
                    Assert.Equal(new Size(13, 13), Extent(pixels, corner, Blue));
                }
            );
        }

        [Fact]
        public void TheBarOfAHalfCircleSegmentFromBottomToTop_StandsUpright_AtTheMiddleOfTheArc()
        {
            OnCanvas(false, new double[] { 0, 0, 1, 0, 100, 0 }, (model, canvas) =>
            {
                // Bulge 1 from (0, 0) to (0, 100) bulges to +x: the middle is (50, 50), the tangent there vertical.
                int[] pixels = Pixels(canvas);

                Assert.Equal(new Size(6, 22), Extent(pixels, At(canvas, 50, 50), Blue, Edge));
            });
        }

        [Fact]
        public void AtGripSize10_TheSquareIs25AndTheBar37By10()
        {
            OnElbow(
                (model, canvas) =>
                {
                    canvas.GripSize = 10;
                    int[] pixels = Pixels(canvas);

                    Assert.Equal(new Size(25, 25), Extent(pixels, At(canvas, 0, 0), Blue, Edge));
                    Assert.Equal(new Size(37, 10), Extent(pixels, At(canvas, 50, 0), Blue, Edge));
                }
            );
        }

        [Fact]
        public void TheGripSizeIsKeptBetween1And255()
        {
            OnElbow(
                (model, canvas) =>
                {
                    Assert.Equal(5, canvas.GripSize);
                    canvas.GripSize = 0;
                    Assert.Equal(1, canvas.GripSize);
                    canvas.GripSize = 300;
                    Assert.Equal(255, canvas.GripSize);
                }
            );
        }

        [Fact]
        public void TheMiddleGripOfALine_StaysASquare()
        {
            OnElbow(
                (model, canvas) =>
                {
                    DxfEntity line = null;

                    foreach (DxfEntity entity in model.Document.Database.ModelSpace)
                    {
                        line = entity;
                    }

                    model.Document.Selection.Set(new[] { line.Handle });
                    int[] pixels = Pixels(canvas);

                    Assert.Equal(new Size(15, 15), Extent(pixels, At(canvas, 50, 110), Blue, Edge));
                }
            );
        }

        [Fact]
        public void TheGripUnderTheCursorIsPinkAndUnedged_AClickedGripRed()
        {
            OnElbow(
                (model, canvas) =>
                {
                    Point vertex = At(canvas, 100, 0);
                    Hover(canvas, vertex);
                    int[] pixels = Pixels(canvas);

                    Assert.Equal(new Size(15, 15), Extent(pixels, vertex, Pink));
                    Assert.Equal(new Size(0, 0), Extent(pixels, vertex, Edge));

                    Hover(canvas, At(canvas, 50, 0));
                    pixels = Pixels(canvas);
                    Assert.Equal(new Size(22, 6), Extent(pixels, At(canvas, 50, 0), Pink));

                    // Clicked: the grip edit starts and the grip is the hot red.
                    Hover(canvas, vertex);
                    CanvasMouseTests.Mouse(canvas, UIElement.MouseDownEvent, vertex);
                    CanvasMouseTests.Mouse(canvas, UIElement.MouseUpEvent, vertex);
                    pixels = Pixels(canvas);
                    Assert.True(model.Session.IsGripEditing);
                    Assert.Equal(new Size(15, 15), Extent(pixels, vertex, Red));
                }
            );
        }

        [Fact]
        public void TheCursorMustBeInTheGripToHoverIt_ASquareSevenAndAHalfAwayNot()
        {
            OnElbow(
                (model, canvas) =>
                {
                    Point vertex = At(canvas, 0, 0);

                    Hover(canvas, new Point(vertex.X + 7, vertex.Y + 7));
                    Assert.Equal(new Size(15, 15), Extent(Pixels(canvas), vertex, Pink));

                    Hover(canvas, new Point(vertex.X + 9, vertex.Y));
                    Assert.Equal(new Size(0, 0), Extent(Pixels(canvas), vertex, Pink));

                    // A bar is hit along its length (22) and a square's side across it (15): 10 along, 7 across.
                    Point middle = At(canvas, 50, 0);
                    Hover(canvas, new Point(middle.X + 10, middle.Y + 7));
                    Assert.Equal(new Size(22, 6), Extent(Pixels(canvas), middle, Pink));
                    Hover(canvas, new Point(middle.X + 12, middle.Y));
                    Assert.Equal(new Size(0, 0), Extent(Pixels(canvas), middle, Pink));
                }
            );
        }

        [Fact]
        public void AVertexGripHoveredFor400Milliseconds_OpensItsMenuBelowAndRightOfIt_WithThreeRows()
        {
            OnElbow(
                (model, canvas) =>
                {
                    Point vertex = At(canvas, 100, 0);
                    Hover(canvas, vertex);
                    Assert.False(canvas.IsGripMenuOpen);

                    Rest(150);
                    Assert.False(canvas.IsGripMenuOpen);
                    Rest(500);

                    Assert.True(canvas.IsGripMenuOpen);
                    Assert.Equal(
                        new[] { "Stretch Vertex", "Add Vertex", "Remove Vertex" },
                        canvas.GripMenuItems.Select(i => i.Text)
                    );
                    Rect box = canvas.GripMenuBox;
                    Assert.Equal(vertex.X + 19, box.X, 0);
                    Assert.Equal(vertex.Y + 24, box.Y, 0);
                    Assert.Equal(3 + 66 + 3, box.Height);

                    // Light inside, the first row not highlighted, a thin border.
                    int[] pixels = Pixels(canvas);
                    Assert.Equal(MenuFill, Pixel(pixels, new Point(box.X + 10, box.Y + 14), 0, 0));
                    Assert.Equal(0xCCCCCC, Pixel(pixels, new Point(box.X, box.Y + 14), 0, 0));
                }
            );
        }

        [Fact]
        public void TheMenuOfASegmentMiddle_IsStretchAddVertexConvertToArc_AnArcsConvertToLine()
        {
            OnCanvas(false, new double[] { 0, 0, 0, 100, 0, 0.5, 100, 100, 0 }, (model, canvas) =>
            {
                Hover(canvas, At(canvas, 50, 0));
                Assert.True(canvas.OpenGripMenuNow());
                Assert.Equal(
                    new[] { "Stretch", "Add Vertex", "Convert to Arc" },
                    canvas.GripMenuItems.Select(i => i.Text)
                );

                // The design's menu is 167 across for this longest item.
                Assert.Equal(167.0, canvas.GripMenuBox.Width, 0);
            });
        }

        [Fact]
        public void TheMenuCloses_WhenTheCursorLeavesTheGripAndTheMenu_OnEsc_AndOnAClickElsewhere()
        {
            OnElbow(
                (model, canvas) =>
                {
                    Point vertex = At(canvas, 100, 0);
                    Hover(canvas, vertex);
                    Assert.True(canvas.OpenGripMenuNow());

                    // On the way to the menu and on it the menu stays; far from both it closes.
                    Rect box = canvas.GripMenuBox;
                    Hover(canvas, new Point(vertex.X + 10, vertex.Y + 12));
                    Assert.True(canvas.IsGripMenuOpen);
                    Hover(canvas, new Point(box.X + 20, box.Y + 20));
                    Assert.True(canvas.IsGripMenuOpen);
                    Hover(canvas, new Point(box.Right + 60, box.Bottom + 60));
                    Assert.False(canvas.IsGripMenuOpen);

                    Hover(canvas, vertex);
                    Assert.True(canvas.OpenGripMenuNow());
                    canvas.Cancel();
                    Assert.False(canvas.IsGripMenuOpen);
                    Assert.Equal(1, model.Document.Selection.Count);

                    Hover(canvas, At(canvas, 0, 0));
                    Assert.True(canvas.OpenGripMenuNow());
                    Point away = new Point(canvas.GripMenuBox.Right + 40, canvas.GripMenuBox.Bottom + 40);
                    CanvasMouseTests.Mouse(canvas, UIElement.MouseDownEvent, away);
                    Assert.False(canvas.IsGripMenuOpen);
                }
            );
        }

        [Fact]
        public void NoMenuOpens_WithGripMultifunctionalOneOrNought_OrWhileACommandRuns()
        {
            OnElbow(
                (model, canvas) =>
                {
                    Point vertex = At(canvas, 100, 0);

                    canvas.GripMultifunctional = 1;
                    Hover(canvas, vertex);
                    Rest(700);
                    Assert.False(canvas.IsGripMenuOpen);

                    canvas.GripMultifunctional = 0;
                    Hover(canvas, At(canvas, 0, 0));
                    Hover(canvas, vertex);
                    Rest(700);
                    Assert.False(canvas.IsGripMenuOpen);

                    canvas.GripMultifunctional = 3;
                    Hover(canvas, At(canvas, 0, 0));
                    model.Execute("LINE");
                    Hover(canvas, vertex);
                    Rest(700);
                    Assert.False(canvas.IsGripMenuOpen);
                    Assert.False(canvas.OpenGripMenuNow());
                }
            );
        }

        [Fact]
        public void TheMenuIsKeptOnTheCanvas_FlippedLeftAndUpWhereItWouldRunOff()
        {
            OnCanvas(false, new double[] { 0, 0, 0, 100, 0, 0, 100, 100, 0 }, (model, canvas) =>
            {
                canvas.View.ZoomTo(new DisplayBox(-300, -300, 105, 105));
                canvas.RenderNow();
                Point vertex = At(canvas, 100, 100);
                Hover(canvas, vertex);
                Assert.True(canvas.OpenGripMenuNow());

                Rect box = canvas.GripMenuBox;
                Assert.True(box.Right <= Width);
                Assert.True(box.Bottom <= Height);
                Assert.True(box.Right <= vertex.X);
            });
        }

        [Fact]
        public void ARowChosen_StartsItsItem_RemoveVertexAtOnceAsOneUndoStep()
        {
            OnElbow(
                (model, canvas) =>
                {
                    Hover(canvas, At(canvas, 100, 0));
                    Assert.True(canvas.OpenGripMenuNow());
                    Rect box = canvas.GripMenuBox;

                    // The third row, Remove Vertex.
                    Point row = new Point(box.X + 40, box.Y + 3 + 22 * 2 + 11);
                    Hover(canvas, row);
                    int[] pixels = Pixels(canvas);
                    Assert.Equal(MenuRow, Pixel(pixels, new Point(box.X + 5, row.Y), 0, 0));

                    CanvasMouseTests.Click(canvas, row);

                    Assert.False(canvas.IsGripMenuOpen);
                    Assert.False(model.Session.IsRunning);
                    DxfPolyline polyline = (DxfPolyline)
                        model.Document.FindEntity(model.Document.Selection.Handles.Single());
                    Assert.Equal(2, polyline.NumberOfVertices);
                    Assert.Equal("GRIP", model.Document.NextUndo.Name);
                    Assert.True(model.Document.Undo());
                    Assert.Equal(3, ((DxfPolyline)model.Document.FindEntity(polyline.Handle)).NumberOfVertices);
                }
            );
        }

        [Fact]
        public void AddVertexChosen_AsksForThePoint_AndAClickPutsTheVertexAfterTheGrip()
        {
            OnElbow(
                (model, canvas) =>
                {
                    Hover(canvas, At(canvas, 0, 0));
                    Assert.True(canvas.OpenGripMenuNow());
                    Rect box = canvas.GripMenuBox;

                    CanvasMouseTests.Click(canvas, new Point(box.X + 40, box.Y + 3 + 22 + 11));

                    Assert.True(model.Session.IsGripEditing);
                    Assert.Equal("Specify new vertex point or [Base point/Copy/eXit]:", model.Prompt);
                    Assert.Contains("** ADD VERTEX **", model.History);

                    CanvasMouseTests.Click(canvas, At(canvas, 40, 30));

                    DxfPolyline polyline = (DxfPolyline)
                        model.Document.FindEntity(model.Document.Selection.Handles.Single());
                    Assert.Equal(4, polyline.NumberOfVertices);
                    Assert.Equal(40.0, polyline.GetPoint2dAt(1).X, 3);
                    Assert.Equal(30.0, polyline.GetPoint2dAt(1).Y, 3);
                    Assert.False(model.Session.IsRunning);
                }
            );
        }

        [Fact]
        public void ConvertToArcChosen_AsksForTheMidpoint_AndEscChangesNothing()
        {
            OnElbow(
                (model, canvas) =>
                {
                    Hover(canvas, At(canvas, 50, 0));
                    Assert.True(canvas.OpenGripMenuNow());
                    Rect box = canvas.GripMenuBox;

                    CanvasMouseTests.Click(canvas, new Point(box.X + 40, box.Y + 3 + 44 + 11));

                    Assert.Equal("Specify the arc midpoint:", model.Prompt);
                    canvas.Cancel();
                    Assert.False(model.Session.IsRunning);
                    DxfPolyline polyline = (DxfPolyline)
                        model.Document.FindEntity(model.Document.Selection.Handles.Single());
                    Assert.Equal(0.0, polyline.GetBulgeAt(0));
                    Assert.False(model.Document.CanUndo);
                }
            );
        }

        [Fact]
        public void AGreyRow_DoesNothing_AndTheMenuStays()
        {
            OnCanvas(false, new double[] { 0, 0, 0, 100, 0, 0 }, (model, canvas) =>
            {
                Hover(canvas, At(canvas, 100, 0));
                Assert.True(canvas.OpenGripMenuNow());
                Assert.False(canvas.GripMenuItems[2].Enabled);
                Rect box = canvas.GripMenuBox;

                CanvasMouseTests.Click(canvas, new Point(box.X + 40, box.Y + 3 + 44 + 11));

                Assert.False(model.Document.CanUndo);
                Assert.False(model.Session.IsRunning);
            });
        }

        [Fact]
        public void GripSizeIs5ByDefault_RefusesBelow1AndAbove255_AndIsKeptInTheFile()
        {
            var settings = new GlobalSettings();
            Assert.Equal(5, settings.GripSize);
            Assert.Equal(3, settings.GripMultifunctional);
            Assert.Throws<ArgumentOutOfRangeException>(() => settings.GripSize = 0);
            Assert.Throws<ArgumentOutOfRangeException>(() => settings.GripSize = 256);
            Assert.Throws<ArgumentOutOfRangeException>(() => settings.GripMultifunctional = 4);

            settings.GripSize = 10;
            settings.GripMultifunctional = 2;
            string path = Path.Combine(_folder.FullPath, "settings.json");
            settings.Save(path);
            GlobalSettings read = GlobalSettings.Load(path);

            Assert.Equal(10, read.GripSize);
            Assert.Equal(2, read.GripMultifunctional);
            Assert.Contains("\"gripSize\": 10", File.ReadAllText(path));
            Assert.Equal(settings.ToJson(), read.ToJson());

            File.WriteAllText(path, "{ \"appearance\": { \"gripSize\": 500, \"pickboxPixels\": 7 } }");
            GlobalSettings bad = GlobalSettings.Load(path);
            Assert.Equal(5, bad.GripSize);
            Assert.Equal(7, bad.PickboxPixels);
        }

        [Fact]
        public void TheGripSizeRow_IsRightBelowPickbox_AndRefusesWhatIsOutOfRange()
        {
            var model = new MainViewModel { SettingsPath = Path.Combine(_folder.FullPath, "settings.json") };
            GlobalSettingsViewModel dialog = model.CreateGlobalSettings();
            GlobalSettingRow pickbox = dialog.Row("Appearance", "Pickbox");
            GlobalSettingRow grip = dialog.Row("Appearance", "Grip size");

            Assert.NotNull(grip);
            Assert.Equal("px", grip.Unit);
            Assert.Equal("5", grip.Value);

            // Right below Pickbox in the Cursor section.
            GlobalSettingSection cursor = dialog.Sections.Single(s => s.Rows.Contains(pickbox));
            Assert.Equal(cursor.Rows.ToList().IndexOf(pickbox) + 1, cursor.Rows.ToList().IndexOf(grip));

            grip.Value = "256";
            Assert.True(grip.HasError);
            grip.Value = "0";
            Assert.True(grip.HasError);
            grip.Value = "10";
            Assert.False(grip.HasError);
            Assert.True(dialog.Ok());
            Assert.Equal(10, model.Settings.GripSize);
        }

        [Fact]
        public void GripsizeAndGripmultifunctional_SetTheSettingsInUse_AtOnce_AndTheCanvasFollows()
        {
            OnElbow(
                (model, canvas) =>
                {
                    int applied = 0;
                    model.SettingsApplied += (s, e) => applied++;
                    model.SettingsApplied += (s, e) =>
                    {
                        canvas.GripSize = model.Settings.GripSize;
                        canvas.GripMultifunctional = model.Settings.GripMultifunctional;
                    };

                    model.Execute("GRIPSIZE");
                    Assert.Equal("Enter new value for GRIPSIZE <5>:", model.Prompt);
                    model.Execute("0");
                    Assert.Equal("Requires an integer between 1 and 255.", model.History.Last());
                    model.Execute("GRIPSIZE");
                    model.Execute("10");
                    model.Execute("GRIPMULTIFUNCTIONAL");
                    Assert.Equal("Enter new value for GRIPMULTIFUNCTIONAL <3>:", model.Prompt);
                    model.Execute("2");

                    Assert.Equal(10, model.Settings.GripSize);
                    Assert.Equal(2, model.Settings.GripMultifunctional);
                    Assert.Equal(2, applied);
                    Assert.Equal(10, canvas.GripSize);
                    Assert.False(model.Document.CanUndo);
                    Assert.Equal(new Size(25, 25), Extent(Pixels(canvas), At(canvas, 0, 0), Blue, Edge));
                }
            );
        }

        [Fact]
        public void TheSettingsAppliedByTheWorkspace_GiveTheCanvasItsGripSize()
        {
            Exception failure = null;
            int size = 0,
                ways = 0;
            var thread = new Thread(() =>
            {
                try
                {
                    var model = new MainViewModel();
                    var workspace = new CadWorkspace { DataContext = model };
                    model.ApplySettings(new GlobalSettings { GripSize = 12, GripMultifunctional = 1 });
                    size = workspace.DrawingCanvas.GripSize;
                    ways = workspace.DrawingCanvas.GripMultifunctional;
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
            Assert.Equal(12, size);
            Assert.Equal(1, ways);
        }
    }
}
