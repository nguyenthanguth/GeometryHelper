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
    }
}
