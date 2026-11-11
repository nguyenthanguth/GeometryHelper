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
    /// Dynamic input at the cursor (design DynInputCursor): the AutoComplete list under the box at the Command prompt, the
    /// options menu of the ↓, the X and Y fields relative to the base point with # and @, and the dimensional input of LINE.
    /// </summary>
    public class DynamicInputAtTheCursorTests
    {
        private static MainViewModel Room(TestFolder folder)
        {
            var model = new MainViewModel();
            Assert.True(model.Open(folder.Write("room.dxf", TestFiles.Room(out _, out _))));
            return model;
        }

        private static void Cursor(MainViewModel model, double x, double y)
        {
            model.Session.Cursor = new GeoPoint2(x, y);
            model.SetCursor(x, y);
        }

        /// <summary>The corner of a rectangle across from its first, its third vertex, within a thousandth.</summary>
        private static void Corner(double x, double y, DxfPolyline rectangle)
        {
            GeoPoint2 corner = rectangle.GetPoint2dAt(2);
            Assert.Equal(x, corner.X, 3);
            Assert.Equal(y, corner.Y, 3);
        }

        private static DxfLine LastLine(MainViewModel model) =>
            model.Document.Database.ModelSpace.Cast<DxfEntity>().OfType<DxfLine>().Last();

        [Fact]
        public void RecTypedAtTheCommandPrompt_ShowsTheBoxWithTheListUnderIt_AndTheCommandLineListStaysClosed()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Room(folder);

                // Nothing typed: nothing at the cursor.
                Assert.False(model.IsDynamicInputShown);

                model.CommandText = "REC";
                model.ShowSuggestions();

                Assert.True(model.IsDynamicInputShown);
                Assert.Equal(string.Empty, model.DynamicInputPrompt);
                Assert.Equal("REC", model.DynamicInputValue);
                Assert.True(model.IsSuggestionListOpen);
                Assert.True(model.IsSuggestionListAtCursor);
                Assert.False(model.IsSuggestionListAtCommandLine);
                Assert.Equal("RECTANG", model.SelectedSuggestion.Command);

                // F12: the list moves to the command line, the text stays.
                model.DynamicInput.IsOn = false;

                Assert.False(model.IsDynamicInputShown);
                Assert.False(model.IsSuggestionListAtCursor);
                Assert.True(model.IsSuggestionListAtCommandLine);
                Assert.Equal("REC", model.CommandText);

                // Back on, Esc clears the text and closes the list at once, saying *Cancel*.
                model.DynamicInput.IsOn = true;
                model.Escape();

                Assert.Equal(string.Empty, model.CommandText);
                Assert.False(model.IsSuggestionListOpen);
                Assert.False(model.IsDynamicInputShown);
                Assert.Equal("*Cancel*", model.History.Last());
            }
        }

        [Fact]
        public void TheDownArrowOpensTheOptionsOfLinesThirdPoint_EscClosesTheMenuAlone_AndEnterPicksTheOptionHighlighted()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Room(folder);
                model.Execute("LINE");

                // "Specify first point:" has no options.
                Assert.False(model.HasDynamicInputOptions);
                Assert.False(model.OpenDynamicInputMenu());

                model.Execute("0,0");
                model.Execute("1000,0");
                model.Execute("1000,1000");

                Assert.Equal("Specify next point or", model.DynamicInputPrompt);
                Assert.True(model.HasDynamicInputOptions);
                Assert.True(model.OpenDynamicInputMenu());
                Assert.True(model.IsDynamicInputMenuOpen);
                Assert.Equal(new[] { "Close", "Undo" }, model.DynamicInputOptions.Select(o => o.Keyword));
                Assert.True(model.DynamicInputOptions[0].IsSelected);

                // Esc closes the menu only; the command goes on.
                model.Escape();

                Assert.False(model.IsDynamicInputMenuOpen);
                Assert.True(model.IsCommandRunning);

                // A key typed closes the menu and goes into the field.
                model.OpenDynamicInputMenu();
                model.CommandText = "5";

                Assert.False(model.IsDynamicInputMenuOpen);
                Assert.Equal("5", model.DynamicInputFirst);

                // Down to Undo and Enter: the last segment is taken back, the prompt asks again.
                model.CommandText = string.Empty;
                model.OpenDynamicInputMenu();
                model.MoveDynamicInputOption(1);
                model.MoveDynamicInputOption(1);

                Assert.True(model.DynamicInputOptions[1].IsSelected);
                Assert.True(model.PickDynamicInputOption());
                Assert.False(model.IsDynamicInputMenuOpen);
                Assert.True(model.IsCommandRunning);
                Assert.Equal(1000.0, model.Session.Prompt.BasePoint.Value.X, 9);
                Assert.Equal(0.0, model.Session.Prompt.BasePoint.Value.Y, 9);
            }
        }

        [Fact]
        public void RectangsOtherCorner_ShowsXAndYFromTheFirstCorner_AndTakesThemRelative_UnlessHashIsTyped()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Room(folder);
                model.Execute("RECTANG");
                model.Execute("100,200");
                Cursor(model, 1300, 1000);

                // Relative, as DYNPIFORMAT 0: 1 200 by 800 from the first corner.
                Assert.True(model.IsDynamicInputFields);
                Assert.False(model.IsDynamicInputPolar);
                Assert.Equal("1 200.00", model.DynamicInputFirst);
                Assert.Equal("800.00", model.DynamicInputSecond);

                // 300, Tab, Enter: the width locked, the height the cursor's, both from the first corner.
                model.CommandText = "300";
                Assert.True(model.TabDynamicInput());
                model.Enter();

                DxfPolyline first = model.Document.Database.ModelSpace.Cast<DxfEntity>().OfType<DxfPolyline>().Last();
                Corner(400, 1000, first);

                // "500,300" typed whole is relative too: the corner at (600, 500).
                model.Execute("RECTANG");
                model.Execute("100,200");
                model.CommandText = "500,300";

                Assert.Equal("500", model.DynamicInputFirst);
                Assert.Equal("300", model.DynamicInputSecond);

                model.SubmitCommand();

                DxfPolyline second = model.Document.Database.ModelSpace.Cast<DxfEntity>().OfType<DxfPolyline>().Last();
                Corner(600, 500, second);

                // # first makes it absolute: the corner at (500, 300) itself.
                model.Execute("RECTANG");
                model.Execute("100,200");
                model.CommandText = "#500,300";

                Assert.Equal("500", model.DynamicInputFirst);

                model.SubmitCommand();

                DxfPolyline third = model.Document.Database.ModelSpace.Cast<DxfEntity>().OfType<DxfPolyline>().Last();
                Corner(500, 300, third);
            }
        }

        [Fact]
        public void LinesNextPoint_IsDimensionalInput_ALengthAndAngleTypedWithALessThanAreRelative_AndHashAbsolute()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Room(folder);
                model.Execute("LINE");
                model.Execute("1000,1000");
                Cursor(model, 1300, 1400);

                Assert.True(model.IsDynamicInputPolar);
                Assert.Equal("500.00", model.DynamicInputFirst);
                Assert.Equal("53.1", model.DynamicInputSecond);

                // 400<90 typed: relative to the last point.
                model.CommandText = "400<90";

                Assert.Equal("400", model.DynamicInputFirst);
                Assert.Equal("90", model.DynamicInputSecond);

                model.SubmitCommand();

                Assert.Equal(1000.0, LastLine(model).EndPoint.X, 9);
                Assert.Equal(1400.0, LastLine(model).EndPoint.Y, 9);

                // #0,0 typed: the world's origin.
                model.CommandText = "#0,0";
                model.SubmitCommand();

                Assert.Equal(0.0, LastLine(model).EndPoint.X, 9);
                Assert.Equal(0.0, LastLine(model).EndPoint.Y, 9);

                // RECTANG's other corner is no dimensional input: it has X and Y, its two sizes.
                model.Escape();
                model.Execute("RECTANG");
                model.Execute("0,0");

                Assert.True(model.IsDynamicInputFields);
                Assert.False(model.IsDynamicInputPolar);
                Assert.False(model.IsDynamicInputDimensional);
            }
        }

        [Fact]
        public void LinesNextPoint_DrawsNoDimensionLineOrArc_TheLengthBesideTheMiddleOfTheBandAndTheAngleNearTheBasePoint()
        {
            using (var folder = new TestFolder())
            {
                string room = folder.Write("room.dxf", TestFiles.Room(out _, out _));

                OnSta(() =>
                {
                    _ = PackUriHelper.UriSchemePack;
                    var model = new MainViewModel();
                    Assert.True(model.Open(room));
                    var workspace = new CadWorkspace { DataContext = model };
                    Lay(workspace);
                    model.Execute("LINE");
                    model.Execute("1000,1000");

                    // Laid out again before the cursor moves: the prompt changes the command line's height, and the canvas's with it.
                    Lay(workspace);
                    workspace.DrawingCanvas.MoveCursorTo(1300, 1400);
                    Lay(workspace);

                    Assert.True(model.IsDynamicInputPolar);
                    Assert.Equal("500.00", model.DynamicInputFirst);
                    Assert.Equal("53.1", model.DynamicInputSecond);

                    // The two fields alone: no dimension line, extension lines, arc or dashed axis.
                    Assert.Equal(
                        new UIElement[] { workspace.LengthField, workspace.AngleField },
                        workspace.DimensionalInput.Children.Cast<UIElement>()
                    );
                    Assert.Equal(Visibility.Visible, workspace.LengthField.Visibility);
                    Assert.Equal(Visibility.Visible, workspace.AngleField.Visibility);

                    // The band on the canvas, from (1 000, 1 000) to (1 300, 1 400), in the canvas's own units.
                    CadCanvas canvas = workspace.DrawingCanvas;
                    double scale = canvas.PixelWidth / canvas.ActualWidth;
                    canvas.View.DrawingToPixel(1000, 1000, out double bx, out double by);
                    canvas.View.DrawingToPixel(1300, 1400, out double cx, out double cy);
                    var from = new Point(bx / scale, by / scale);
                    var to = new Point(cx / scale, cy / scale);
                    Rect length = Box(workspace.LengthField, canvas);
                    Rect angle = Box(workspace.AngleField, canvas);

                    // Each field wholly on one side of the band, at least 7 units off it, the two on either side.
                    double lengthSide = Side(length, from, to);
                    double angleSide = Side(angle, from, to);
                    Assert.True(Math.Abs(lengthSide) >= 7.0, "length " + lengthSide);
                    Assert.True(Math.Abs(angleSide) >= 7.0, "angle " + angleSide);
                    Assert.True(lengthSide * angleSide < 0.0, lengthSide + " " + angleSide);

                    // The length by the middle of the band, the angle by the base point.
                    Point middle = from + (to - from) / 2.0;
                    Point lengthCentre = length.TopLeft + (length.BottomRight - length.TopLeft) / 2.0;
                    Point angleCentre = angle.TopLeft + (angle.BottomRight - angle.TopLeft) / 2.0;
                    Assert.True((lengthCentre - middle).Length < 60.0, "length " + (lengthCentre - middle).Length);
                    Assert.True((angleCentre - from).Length < 90.0, "angle " + (angleCentre - from).Length);
                    Assert.True((angleCentre - from).Length < (angleCentre - to).Length);

                    canvas.Dispose();
                });
            }
        }

        [Fact]
        public void ANearlyFlatBandEndingByTheRightEdge_TheBoxFlippedLeftOfTheCursor_HidesNeitherField()
        {
            using (var folder = new TestFolder())
            {
                string room = folder.Write("room.dxf", TestFiles.Room(out _, out _));

                OnSta(() =>
                {
                    _ = PackUriHelper.UriSchemePack;
                    var model = new MainViewModel();
                    Assert.True(model.Open(room));
                    var workspace = new CadWorkspace { DataContext = model };
                    model.Execute("LINE");
                    Lay(workspace);
                    CadCanvas canvas = workspace.DrawingCanvas;
                    double scale = canvas.PixelWidth / canvas.ActualWidth;

                    // The cursor 30 units in from the right edge, the base point 300 to its left and 60 lower: a band rising 11 degrees,
                    // its middle 30 below the cursor, where the length stands and the box flipped left of the cursor would cover it.
                    var cursorAt = new Point(canvas.ActualWidth - 30.0, canvas.ActualHeight / 2.0);
                    var baseAt = new Point(cursorAt.X - 300.0, cursorAt.Y + 60.0);
                    canvas.View.ScreenToDrawing(baseAt.X * scale, baseAt.Y * scale, out double bx, out double by);
                    canvas.View.ScreenToDrawing(cursorAt.X * scale, cursorAt.Y * scale, out double cx, out double cy);
                    model.Execute(
                        string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:0.###},{1:0.###}", bx, by)
                    );
                    Lay(workspace);
                    canvas.MoveCursorTo(cx, cy);
                    Lay(workspace);

                    Assert.True(model.IsDynamicInputPolar);
                    Rect prompt = Box(workspace.DynamicInputBox, canvas);
                    Rect length = Box(workspace.LengthField, canvas);
                    Rect angle = Box(workspace.AngleField, canvas);

                    // The box no longer fits right of the cursor: it stands left of it, over the band's right half.
                    Assert.True(prompt.Right < cursorAt.X, prompt + " " + cursorAt);
                    Assert.False(prompt.IntersectsWith(length), prompt + " " + length);
                    Assert.False(prompt.IntersectsWith(angle), prompt + " " + angle);
                    Assert.False(length.IntersectsWith(angle), length + " " + angle);

                    canvas.Dispose();
                });
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

        private static Rect Box(FrameworkElement field, UIElement canvas)
        {
            Point corner = field.TranslatePoint(new Point(0, 0), canvas);
            return new Rect(corner, new Size(field.ActualWidth, field.ActualHeight));
        }

        /// <summary>The signed distance of a box's nearest corner from the line of the band, zero where the line crosses it.</summary>
        private static double Side(Rect box, Point from, Point to)
        {
            Vector along = to - from;
            along.Normalize();
            double[] signed = new[] { box.TopLeft, box.TopRight, box.BottomLeft, box.BottomRight }
                .Select(p => Vector.CrossProduct(along, p - from))
                .ToArray();
            return signed.Min() > 0.0 ? signed.Min()
                : signed.Max() < 0.0 ? signed.Max()
                : 0.0;
        }

        private static void OnSta(Action action)
        {
            Exception failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    action();
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

        [Theory]
        [InlineData("CIRCLE|2P|1000,1000")]
        [InlineData("ARC|1000,1000")]
        [InlineData("ELLIPSE|1000,1000")]
        [InlineData("POLYGON|4|E|1000,1000")]
        [InlineData("XLINE|1000,1000")]
        [InlineData("RAY|1000,1000")]
        [InlineData("PLINE|1000,1000")]
        [InlineData("MOVE|ALL||1000,1000")]
        [InlineData("COPY|ALL||1000,1000")]
        [InlineData("OFFSET|1000,1000")]
        [InlineData("STRETCH|4100,-100|3900,100||1000,1000")]
        public void TheSecondPointOfACommand_IsDimensionalInput_TheLengthAndTheAngleFromTheFirst(string typed)
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Room(folder);

                foreach (string line in typed.Split('|'))
                {
                    model.Execute(line);
                }

                Cursor(model, 1300, 1400);

                Assert.True(model.IsCommandRunning);
                Assert.True(model.IsDynamicInputPolar);
                Assert.True(model.IsDynamicInputLength);
                Assert.True(model.IsDynamicInputAngle);
                Assert.Equal("500.00", model.DynamicInputFirst);
                Assert.Equal("53.1", model.DynamicInputSecond);

                // The length locked holds the cursor 300 from the first point, towards it.
                model.CommandText = "300";
                Assert.True(model.TabDynamicInput());

                GeoPoint2 held = model.HoldDynamicInput(new GeoPoint2(1300, 1400)).Value;
                Assert.Equal(1180.0, held.X, 9);
                Assert.Equal(1240.0, held.Y, 9);
            }
        }

        [Fact]
        public void TheRadiusOfACircle_IsTheLengthFieldAlone_ANumberTypedHoldsTheCursorAndEnterTakesIt()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Room(folder);
                model.Execute("CIRCLE");
                model.Execute("1000,1000");
                Cursor(model, 1300, 1400);

                Assert.True(model.IsDynamicInputLength);
                Assert.False(model.IsDynamicInputAngle);
                Assert.False(model.IsDynamicInputPolar);
                Assert.False(model.IsDynamicInputFields);
                Assert.True(model.IsDynamicInputFirstActive);
                Assert.Equal("500.00", model.DynamicInputFirst);
                Assert.False(model.IsDynamicInputFirstLocked);
                Assert.Null(model.HoldDynamicInput(new GeoPoint2(1300, 1400)));

                model.CommandText = "250";

                Assert.Equal("250", model.DynamicInputFirst);
                Assert.True(model.IsDynamicInputFirstLocked);
                GeoPoint2 held = model.HoldDynamicInput(new GeoPoint2(1300, 1400)).Value;
                Assert.Equal(1150.0, held.X, 9);
                Assert.Equal(1200.0, held.Y, 9);

                // Tab is not the field's: there is no other to go to.
                Assert.False(model.TabDynamicInput());

                model.SubmitCommand();

                DxfCircle circle = model.Document.Database.ModelSpace.Cast<DxfEntity>().OfType<DxfCircle>().Last();
                Assert.Equal(250.0, circle.Radius, 9);
            }
        }

        [Fact]
        public void TheAngleOfRotate_IsTheAngleFieldAlone_AndTheScaleFactorHasNoField()
        {
            using (var folder = new TestFolder())
            {
                MainViewModel model = Room(folder);
                model.Execute("ROTATE");
                model.Execute("ALL");
                model.Execute(string.Empty);
                model.Execute("1000,1000");
                Cursor(model, 1000, 1500);

                Assert.True(model.IsDynamicInputAngle);
                Assert.False(model.IsDynamicInputLength);
                Assert.True(model.IsDynamicInputSecondActive);
                Assert.Equal("90.0", model.DynamicInputSecond);

                // 30 typed holds the cursor on the line at 30 degrees, at its distance of 500.
                model.CommandText = "30";

                Assert.Equal("30", model.DynamicInputSecond);
                GeoPoint2 held = model.HoldDynamicInput(new GeoPoint2(1000, 1500)).Value;
                Assert.Equal(1000.0 + 500.0 * Math.Cos(Math.PI / 6.0), held.X, 9);
                Assert.Equal(1250.0, held.Y, 9);

                model.CommandText = string.Empty;
                model.Escape();
                model.Execute("SCALE");
                model.Execute("ALL");
                model.Execute(string.Empty);
                model.Execute("1000,1000");
                Cursor(model, 1000, 1500);

                Assert.False(model.IsDynamicInputDimensional);
                Assert.False(string.IsNullOrEmpty(model.DynamicInputValue));
            }
        }
    }
}
