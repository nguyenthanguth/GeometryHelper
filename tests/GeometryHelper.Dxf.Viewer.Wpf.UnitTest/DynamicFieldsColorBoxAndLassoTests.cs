using System;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.Wpf.UnitTest
{
    /// <summary>
    /// The two fields of dynamic input at a point prompt, the canvas's tip beside them, the colour box of the ribbon and
    /// CECOLOR, a box whose first corner stays on the drawing while the wheel zooms, and the lasso.
    /// </summary>
    public class DynamicFieldsColorBoxAndLassoTests
    {
        private const int Width = 800;
        private const int Height = 600;

        private static MainViewModel Room(TestFolder folder, out string column)
        {
            var model = new MainViewModel();
            Assert.True(model.Open(folder.Write("room.dxf", TestFiles.Room(out _, out column))));
            return model;
        }

        /// <summary>Runs a test on an STA thread with the room open in a view model and a canvas of 800 by 600 over it, at 0.18 pixels to the unit.</summary>
        private static void OnCanvas(Action<MainViewModel, CadCanvas, string> test)
        {
            Exception failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    using (var folder = new TestFolder())
                    {
                        MainViewModel model = Room(folder, out string column);
                        var canvas = new CadCanvas { Document = model.Document, Space = model.Space, View = model.View, Session = model.Session, Snap = model.Snap };
                        canvas.PixelSizeChanged += (s, e) => model.SetCanvasSize(canvas.PixelWidth, canvas.PixelHeight);
                        canvas.Measure(new Size(Width, Height));
                        canvas.Arrange(new Rect(0, 0, Width, Height));
                        canvas.RenderNow();
                        test(model, canvas, column);
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

        private static Point At(CadCanvas canvas, double x, double y) => CanvasMouseTests.At(canvas, x, y);

        private static void Cursor(MainViewModel model, double x, double y)
        {
            // As the canvas does: the session's cursor and the view model's are the same point.
            model.Session.Cursor = new GeoPoint2(x, y);
            model.SetCursor(x, y);
        }

        private static DxfLine LastLine(MainViewModel model) => model.Document.Database.ModelSpace.Cast<DxfEntity>().OfType<DxfLine>().Last();

        private static void Lasso(CadCanvas canvas, params Point[] points)
        {
            CanvasMouseTests.Mouse(canvas, UIElement.MouseMoveEvent, points[0]);
            CanvasMouseTests.Mouse(canvas, UIElement.MouseDownEvent, points[0]);

            foreach (Point point in points.Skip(1))
            {
                CanvasMouseTests.Mouse(canvas, UIElement.MouseMoveEvent, point);
            }

            CanvasMouseTests.Mouse(canvas, UIElement.MouseUpEvent, points[points.Length - 1]);
        }

        [Fact]
        public void AtTheNextPointOfALine_TheFieldsAreDistanceAndAngle_TabLocksTheDistance_AndEnterTakesTheCursorsAngle()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Room(folder, out _);
                model.Execute("LINE");
                model.Execute("1000,1000");
                Cursor(model, 1300, 1400);

                Assert.True(model.IsDynamicInputFields);
                Assert.True(model.IsDynamicInputPolar);
                Assert.Equal("500.00", model.DynamicInputFirst);
                Assert.Equal("53.1", model.DynamicInputSecond);
                Assert.False(model.IsDynamicInputFirstLocked);
                Assert.True(model.IsDynamicInputFirstActive);

                // 300 typed into the distance, then Tab: the distance locked, the angle takes what is typed next.
                model.CommandText = "300";

                Assert.Equal("300", model.DynamicInputFirst);
                Assert.True(model.IsDynamicInputFirstLocked);
                Assert.True(model.TabDynamicInput());
                Assert.Equal(string.Empty, model.CommandText);
                Assert.True(model.IsDynamicInputFirstLocked);
                Assert.True(model.IsDynamicInputSecondActive);

                // The cursor straight up: the locked distance stays, the angle follows; Enter takes 300 at 90 degrees.
                Cursor(model, 1000, 2000);

                Assert.Equal("300", model.DynamicInputFirst);
                Assert.Equal("90.0", model.DynamicInputSecond);

                model.Enter();

                DxfLine first = LastLine(model);
                Assert.Equal(1000.0, first.EndPoint.X, 9);
                Assert.Equal(1300.0, first.EndPoint.Y, 9);
                Assert.True(model.IsCommandRunning);
                Assert.False(model.IsDynamicInputFirstLocked);

                // Tab to the angle, 45 typed and Enter: the cursor's distance, 500, at 45 degrees from (1000, 1300).
                Cursor(model, 1000, 1800);
                Assert.True(model.TabDynamicInput());
                model.CommandText = "45";

                Assert.True(model.IsDynamicInputSecondLocked);
                Assert.Equal("500.00", model.DynamicInputFirst);

                model.SubmitCommand();

                DxfLine second = LastLine(model);
                Assert.Equal(1000.0 + 500.0 * Math.Sqrt(0.5), second.EndPoint.X, 9);
                Assert.Equal(1300.0 + 500.0 * Math.Sqrt(0.5), second.EndPoint.Y, 9);
            }
        }

        [Fact]
        public void WithNoBasePoint_TheFieldsAreXAndY_AnXTypedTakesTheCursorsY_AndAWholeCoordinateGoesAsOnTheCommandLine()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Room(folder, out _);
                model.Execute("LINE");
                Cursor(model, 250, 700);

                Assert.True(model.IsDynamicInputFields);
                Assert.False(model.IsDynamicInputPolar);
                Assert.Equal("250.00", model.DynamicInputFirst);
                Assert.Equal("700.00", model.DynamicInputSecond);

                // 10,20 typed: the comma moves to Y; both fields show what is typed, and Enter takes it as the command line does.
                model.CommandText = "10,20";

                Assert.Equal("10", model.DynamicInputFirst);
                Assert.Equal("20", model.DynamicInputSecond);
                Assert.True(model.IsDynamicInputSecondActive);
                Assert.True(model.IsDynamicInputFirstLocked && model.IsDynamicInputSecondLocked);

                model.SubmitCommand();

                Assert.Equal(10.0, model.Session.Prompt.BasePoint.Value.X, 9);
                Assert.Equal(20.0, model.Session.Prompt.BasePoint.Value.Y, 9);

                // @300<0 shows 300 and 0 and goes to the command line whole: relative to the last point.
                model.CommandText = "@300<0";

                Assert.Equal("300", model.DynamicInputFirst);
                Assert.Equal("0", model.DynamicInputSecond);

                model.SubmitCommand();

                DxfLine line = LastLine(model);
                Assert.Equal(310.0, line.EndPoint.X, 9);
                Assert.Equal(20.0, line.EndPoint.Y, 9);

                // A first point with X alone typed: the cursor's Y.
                model.Escape();
                model.Execute("LINE");
                Cursor(model, 250, 700);
                model.CommandText = "40";
                model.SubmitCommand();

                Assert.Equal(40.0, model.Session.Prompt.BasePoint.Value.X, 9);
                Assert.Equal(700.0, model.Session.Prompt.BasePoint.Value.Y, 9);

                // DYN off: no fields, and Tab is left to the window.
                model.DynamicInput.IsOn = false;

                Assert.False(model.IsDynamicInputFields);
                Assert.False(model.TabDynamicInput());
            }
        }

        [Fact]
        public void WithDynamicInputShown_TheCanvasTipGivesTheSnapAlone_AndWithoutItTheDistanceAndAngleToo()
        {
            OnCanvas((model, canvas, column) =>
            {
                model.Toggles.Single(t => t.Name == "OSNAP").IsOn = false;
                model.StartCommand("LINE");
                CanvasMouseTests.Click(canvas, At(canvas, 500, 500));
                CanvasMouseTests.Mouse(canvas, UIElement.MouseMoveEvent, At(canvas, 800, 900));

                Assert.Contains(" < ", canvas.PromptTip);

                // DYN gives the distance and angle; the grid is no object snap, so the canvas shows no tip of its own.
                canvas.DynamicInput = true;

                Assert.Equal(string.Empty, canvas.PromptTip);

                // An object snap is still named: the end of the bottom wall.
                model.Toggles.Single(t => t.Name == "OSNAP").IsOn = true;
                CanvasMouseTests.Mouse(canvas, UIElement.MouseMoveEvent, At(canvas, 4005, 5));

                Assert.Equal("Endpoint", canvas.PromptTip);
            });
        }

        [Fact]
        public void MoreColorsOfTheColourBoxShowsSelectColor_AndALineDrawnAfterTakesTheColourChosen()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Room(folder, out _);

                Assert.Equal("ByLayer", model.CurrentColor);
                Assert.Equal(MainViewModel.MoreColorsOption, model.CurrentColorOptions.Last());
                Assert.Equal(0xFFFFFF, model.CurrentColorRgb);

                bool offered = false;
                model.PickColor = dialog =>
                {
                    offered = dialog.OffersByLayerAndByBlock && dialog.Original.IsByLayer;
                    dialog.PickIndex(30);
                    return true;
                };

                model.CurrentColor = MainViewModel.MoreColorsOption;

                Assert.True(offered);
                Assert.Equal(30, model.Document.Database.Cecolor.ColorIndex);
                Assert.Equal("30", model.CurrentColor);
                Assert.Contains("30", model.CurrentColorOptions);
                Assert.Equal(0xFF7F00, model.CurrentColorRgb);

                model.Execute("LINE");
                model.Execute("0,0");
                model.Execute("100,0");
                model.Enter();

                Assert.Equal(30, LastLine(model).Color.ColorIndex);

                // A name of the list; then CECOLOR typed, which the box follows.
                model.CurrentColor = "Red";

                Assert.Equal(1, model.Document.Database.Cecolor.ColorIndex);

                model.Execute("CECOLOR");

                Assert.Equal("Enter new object color [Truecolor/COlorbook] <red>:", model.Prompt);

                model.Execute("ByBlock");

                Assert.Equal("ByBlock", model.CurrentColor);

                // A true colour is refused: the header holds an index.
                model.PickColor = dialog =>
                {
                    dialog.Red = 10;
                    dialog.Green = 20;
                    dialog.Blue = 30;
                    return true;
                };
                model.SelectCurrentColorCommand.Execute(null);

                Assert.True(model.Document.Database.Cecolor.IsByBlock);
                Assert.Equal("The drawing keeps the colour of new objects as an index: ByLayer, ByBlock or 1 to 255.", model.History.Last());
            }
        }

        [Fact]
        public void TheFirstCornerOfABoxStaysOnTheDrawingWhileTheWheelZooms()
        {
            OnCanvas((model, canvas, column) =>
            {
                // The first corner at (1800, 1300), then a notch about (3000, 2500): the box ends at (2200, 1700), a window
                // that holds the column of radius 100 at (2000, 1500). Kept in pixels, the corner would have moved to
                // (2000, 1500), and the window would hold nothing.
                CanvasMouseTests.Click(canvas, At(canvas, 1800, 1300));
                CanvasMouseTests.Mouse(canvas, UIElement.MouseWheelEvent, At(canvas, 3000, 2500), delta: 120);
                CanvasMouseTests.Click(canvas, At(canvas, 2200, 1700));

                Assert.Equal(new[] { column }, model.Document.Selection.Handles);
            });
        }

        [Fact]
        public void ALassoDrawnClockwiseTakesWhatItHoldsWhole_CounterClockwiseWhatItTouches_AndEscDropsIt()
        {
            OnCanvas((model, canvas, column) =>
            {
                // Clockwise round the column: a window that holds it whole.
                Lasso(canvas, At(canvas, 1800, 1700), At(canvas, 2200, 1700), At(canvas, 2200, 1300), At(canvas, 1800, 1300));

                Assert.Equal(new[] { column }, model.Document.Selection.Handles);

                // Over its right half only, clockwise: a window, which takes nothing.
                model.ClearSelection();
                Lasso(canvas, At(canvas, 2050, 1700), At(canvas, 2300, 1700), At(canvas, 2300, 1300), At(canvas, 2050, 1300));

                Assert.Empty(model.Document.Selection.Handles);

                // The same counter-clockwise: a crossing, which takes the column its rim runs through.
                Lasso(canvas, At(canvas, 2050, 1700), At(canvas, 2050, 1300), At(canvas, 2300, 1300), At(canvas, 2300, 1700));

                Assert.Equal(new[] { column }, model.Document.Selection.Handles);

                // Esc while drawing drops the lasso: the release selects nothing.
                model.ClearSelection();
                CanvasMouseTests.Mouse(canvas, UIElement.MouseMoveEvent, At(canvas, 1800, 1700));
                CanvasMouseTests.Mouse(canvas, UIElement.MouseDownEvent, At(canvas, 1800, 1700));
                CanvasMouseTests.Mouse(canvas, UIElement.MouseMoveEvent, At(canvas, 2200, 1700));
                CanvasMouseTests.Mouse(canvas, UIElement.MouseMoveEvent, At(canvas, 2200, 1300));
                canvas.Cancel();
                CanvasMouseTests.Mouse(canvas, UIElement.MouseMoveEvent, At(canvas, 1800, 1300));
                CanvasMouseTests.Mouse(canvas, UIElement.MouseUpEvent, At(canvas, 1800, 1300));

                Assert.Empty(model.Document.Selection.Handles);
            });
        }
    }
}
