using System;
using System.IO.Packaging;
using System.Linq;
using System.Threading;
using System.Windows;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// The follow-ups of dynamic input: the fields measured to the point the rubber band ends at, the second points that show
    /// a length and an angle, CIRCLE 3P, and the fields of grip ROTATE and grip SCALE.
    /// </summary>
    public class DynamicInputFollowUpTests
    {
        private static MainViewModel Room(TestFolder folder)
        {
            var model = new MainViewModel();
            Assert.True(model.Open(folder.Write("room.dxf", TestFiles.Room(out _, out _))));
            return model;
        }

        /// <summary>Runs a test on an STA thread with the room open in a view model shown by a workspace 1 600 by 960.</summary>
        private static void OnWorkspace(Action<MainViewModel, CadWorkspace> test)
        {
            Exception failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    using (var folder = new TestFolder())
                    {
                        _ = PackUriHelper.UriSchemePack;
                        MainViewModel model = Room(folder);
                        var workspace = new CadWorkspace { DataContext = model };
                        Lay(workspace);
                        test(model, workspace);
                        workspace.DrawingCanvas.Dispose();
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

            if (failure != null)
            {
                throw new InvalidOperationException("The STA thread failed.", failure);
            }
        }

        private static void Lay(CadWorkspace workspace)
        {
            for (int pass = 0; pass < 2; pass++)
            {
                workspace.Measure(new Size(1600, 960));
                workspace.Arrange(new Rect(0, 0, 1600, 960));
                workspace.UpdateLayout();
                workspace.DrawingCanvas.RenderNow();
            }
        }

        /// <summary>Moves the cursor to a drawing point, laid out before and after, as the mouse moving there does.</summary>
        private static void Move(CadWorkspace workspace, double x, double y)
        {
            Lay(workspace);
            workspace.DrawingCanvas.MoveCursorTo(x, y);
            Lay(workspace);
        }

        private static Point Centre(FrameworkElement field, UIElement canvas)
        {
            Point corner = field.TranslatePoint(new Point(0, 0), canvas);
            return new Point(corner.X + field.ActualWidth / 2.0, corner.Y + field.ActualHeight / 2.0);
        }

        private static Point OnScreen(CadCanvas canvas, double x, double y)
        {
            double scale = canvas.PixelWidth / canvas.ActualWidth;
            canvas.View.DrawingToPixel(x, y, out double px, out double py);
            return new Point(px / scale, py / scale);
        }

        private static DxfLine LastLine(MainViewModel model) => model.Document.Database.ModelSpace.Cast<DxfEntity>().OfType<DxfLine>().Last();

        [Fact]
        public void WithAnEndpointSnapped_LinesFieldsMeasureToTheEndpoint_NotToTheCursorBesideIt()
        {
            OnWorkspace((model, workspace) =>
            {
                model.Snap.IsOn = true;
                model.Snap.Modes = CadSnapModes.Endpoint;
                model.Snap.Ortho = false;
                model.Snap.Polar = false;
                model.Execute("LINE");
                model.Execute("1000,1000");

                // The cursor 4 pixels right of and 3 below the wall's end at (4 000, 0): the band ends at the end itself,
                // 3 162.28 from the first point at 341.6 degrees.
                CadCanvas canvas = workspace.DrawingCanvas;
                double unit = 1.0 / canvas.View.PixelsPerUnit * (canvas.PixelWidth / canvas.ActualWidth);
                Move(workspace, 4000 + 4.0 * unit, -3.0 * unit);

                Assert.True(model.IsDynamicInputPolar);
                Assert.Equal("3 162.28", model.DynamicInputFirst);
                Assert.Equal("341.6", model.DynamicInputSecond);

                // The length beside the middle of the band to the end, not of a band to the cursor.
                Point middle = OnScreen(canvas, 2500, 500);
                Assert.True((Centre(workspace.LengthField, canvas) - middle).Length < 60.0, "length " + (Centre(workspace.LengthField, canvas) - middle).Length);

                // 300 typed and Enter: along the band, towards the endpoint.
                model.CommandText = "300";
                model.SubmitCommand();

                GeoVector2 way = new GeoPoint2(4000, 0) - new GeoPoint2(1000, 1000);
                Assert.Equal(1000.0 + 300.0 * way.X / way.Length, LastLine(model).EndPoint.X, 6);
                Assert.Equal(1000.0 + 300.0 * way.Y / way.Length, LastLine(model).EndPoint.Y, 6);
            });
        }

        [Fact]
        public void WithOrthoOn_TheFieldsMeasureToTheBandHeldUpright_AndALengthTypedGoesAlongIt()
        {
            OnWorkspace((model, workspace) =>
            {
                model.Snap.IsOn = false;
                model.Snap.Ortho = true;
                model.Execute("LINE");
                model.Execute("1000,1000");

                // The cursor at (1 300, 1 400): ortho holds the band upright, to (1 000, 1 400).
                Move(workspace, 1300, 1400);

                Assert.Equal("400.00", model.DynamicInputFirst);
                Assert.Equal("90.0", model.DynamicInputSecond);

                // 300 locked by Tab and Enter: 300 up, as the band runs.
                model.CommandText = "300";
                Assert.True(model.TabDynamicInput());
                model.Enter();

                Assert.Equal(1000.0, LastLine(model).EndPoint.X, 6);
                Assert.Equal(1300.0, LastLine(model).EndPoint.Y, 6);

                // 200 typed alone and Enter: on up the band, not towards the cursor.
                Move(workspace, 1300, 1700);
                model.CommandText = "200";
                model.SubmitCommand();

                Assert.Equal(1000.0, LastLine(model).EndPoint.X, 6);
                Assert.Equal(1500.0, LastLine(model).EndPoint.Y, 6);
            });
        }

        private static void Cursor(MainViewModel model, double x, double y)
        {
            model.Session.Cursor = new GeoPoint2(x, y);
            model.SetCursor(x, y);
        }

        private static void Run(MainViewModel model, string typed)
        {
            foreach (string line in typed.Split('|'))
            {
                model.Execute(line);
            }
        }

        [Theory]
        [InlineData("MIRROR|ALL||1000,1000", "Specify second point of mirror line")]
        [InlineData("ROTATE|ALL||0,0|R|1000,1000", "Specify second point")]
        [InlineData("ROTATE|ALL||0,0|R|0|P|1000,1000", "Specify second point")]
        [InlineData("SCALE|ALL||0,0|R|1000,1000", "Specify second point")]
        [InlineData("ALIGN|ALL||1000,1000", "Specify first destination point")]
        [InlineData("BREAK|2000,0|F|1000,0", "Specify second break point")]
        [InlineData("DIST|1000,1000", "Specify second point")]
        [InlineData("AREA|1000,1000", "Specify next point")]
        [InlineData("MEASUREGEOM|A||1000,1000", "Specify first angle endpoint")]
        [InlineData("TEXT|J|A|1000,1000", "Specify second endpoint of text baseline")]
        [InlineData("DIMLINEAR|1000,1000", "Specify second extension line origin")]
        [InlineData("DIMALIGNED|1000,1000", "Specify second extension line origin")]
        [InlineData("DIMANGULAR||1000,1000", "Specify first angle endpoint")]
        [InlineData("DIMLINEAR|0,-500|1000,-500|500,-900|DIMCONTINUE", "Specify a second extension line origin")]
        [InlineData("DIMLINEAR|0,-500|1000,-500|500,-900|DIMBASELINE", "Specify a second extension line origin")]
        [InlineData("REVCLOUD|P|1000,1000", "Specify next point")]
        public void ASecondPointFromABasePoint_ShowsTheLengthAndTheAngle_AsAutoCadsPolarDefault(string typed, string asked)
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Room(folder);
                Run(model, typed);

                Assert.True(model.IsCommandRunning);
                Assert.Equal(asked, model.Session.Prompt.Message);
                GeoPoint2 from = model.Session.Prompt.BasePoint.Value;
                Cursor(model, from.X + 300, from.Y + 400);

                Assert.True(model.IsDynamicInputPolar);
                Assert.True(model.IsDynamicInputLength && model.IsDynamicInputAngle);
                Assert.Equal("500.00", model.DynamicInputFirst);
                Assert.Equal("53.1", model.DynamicInputSecond);

                // 200 locked by Tab holds the cursor 200 from the base point, towards it.
                model.CommandText = "200";
                Assert.True(model.TabDynamicInput());
                GeoPoint2 held = model.HoldDynamicInput(new GeoPoint2(from.X + 300, from.Y + 400)).Value;
                Assert.Equal(from.X + 120.0, held.X, 9);
                Assert.Equal(from.Y + 160.0, held.Y, 9);
            }
        }

        [Fact]
        public void Circle3P_ShowsXAndYAtItsFirstPoint_AndTheLengthAndTheAngleFromTheLastAtTheSecondAndTheThird()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Room(folder);
                model.Execute("CIRCLE");
                model.Execute("3P");
                Cursor(model, 1000, 1000);

                // The first point: pointer input, X and Y.
                Assert.True(model.IsDynamicInputFields);
                Assert.False(model.IsDynamicInputPolar);
                Assert.Equal("1 000.00", model.DynamicInputFirst);
                Assert.Equal("1 000.00", model.DynamicInputSecond);

                // 1000 and Tab, 0 and Enter: the first point at (1 000, 0).
                model.CommandText = "1000";
                Assert.True(model.TabDynamicInput());
                model.CommandText = "0";
                model.SubmitCommand();

                // The second point: 500 at 53.1 from the first.
                Assert.Equal("Specify second point on circle", model.Session.Prompt.Message);
                Cursor(model, 1300, 400);

                Assert.True(model.IsDynamicInputPolar);
                Assert.Equal("500.00", model.DynamicInputFirst);
                Assert.Equal("53.1", model.DynamicInputSecond);

                // 400<90 typed: relative to the first, (1 000, 400).
                model.CommandText = "400<90";
                model.SubmitCommand();

                // The third point: from the second.
                Assert.Equal("Specify third point on circle", model.Session.Prompt.Message);
                Cursor(model, 1600, 0);

                Assert.True(model.IsDynamicInputPolar);
                Assert.Equal("721.11", model.DynamicInputFirst);
                Assert.Equal("326.3", model.DynamicInputSecond);

                // 600 locked, the angle the cursor's: through (1 000, 0), (1 000, 400) and 600 along the band to (1 600, 0).
                model.CommandText = "600";
                Assert.True(model.TabDynamicInput());
                model.Enter();

                DxfCircle circle = model.Document.Database.ModelSpace.Cast<DxfEntity>().OfType<DxfCircle>().Last();
                GeoPoint2 third = new GeoPoint2(1000, 400) + new GeoVector2(600 * 600 / 721.1102550927978, -400 * 600 / 721.1102550927978);
                Assert.Equal(circle.Radius, (new GeoPoint2(circle.Center.X, circle.Center.Y) - new GeoPoint2(1000, 0)).Length, 6);
                Assert.Equal(circle.Radius, (new GeoPoint2(circle.Center.X, circle.Center.Y) - new GeoPoint2(1000, 400)).Length, 6);
                Assert.Equal(circle.Radius, (new GeoPoint2(circle.Center.X, circle.Center.Y) - third).Length, 6);
            }
        }

        [Fact]
        public void Circle3P_TanTypedIntoTheFields_StillAsksForTheObjectTouched()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Room(folder);
                model.Execute("CIRCLE");
                model.Execute("3P");
                model.CommandText = "_tan";
                model.SubmitCommand();

                Assert.True(model.IsCommandRunning);
                Assert.Equal("to", model.Session.Prompt.Message);
            }
        }

        [Theory]
        [InlineData("RECTANG|1000,1000", "Specify other corner point")]
        [InlineData("ZOOM|W|1000,1000", "Specify opposite corner")]
        [InlineData("MTEXT|1000,1000", "Specify opposite corner")]
        [InlineData("REVCLOUD|R|1000,1000", "Specify opposite corner")]
        [InlineData("DIMLINEAR|0,-500|1000,-500", "Specify dimension line location")]
        public void ACornerOrALocation_KeepsTheXAndTheYFromTheBasePoint(string typed, string asked)
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Room(folder);
                Run(model, typed);

                Assert.Equal(asked, model.Session.Prompt.Message);
                GeoPoint2 from = model.Session.Prompt.BasePoint.Value;
                Cursor(model, from.X + 300, from.Y + 400);

                Assert.True(model.IsDynamicInputFields);
                Assert.False(model.IsDynamicInputPolar);
                Assert.Equal("300.00", model.DynamicInputFirst);
                Assert.Equal("400.00", model.DynamicInputSecond);
            }
        }
    }
}
