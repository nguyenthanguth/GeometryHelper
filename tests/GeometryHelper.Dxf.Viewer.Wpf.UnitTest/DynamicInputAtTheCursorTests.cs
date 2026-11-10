using System.Linq;
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

        private static DxfLine LastLine(MainViewModel model) => model.Document.Database.ModelSpace.Cast<DxfEntity>().OfType<DxfLine>().Last();

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

                // Another command's point with a base point is no dimensional input: MOVE's second point has X and Y.
                model.Escape();
                model.Execute("MOVE");
                model.Execute("ALL");
                model.Execute(string.Empty);
                model.Execute("0,0");

                Assert.True(model.IsDynamicInputFields);
                Assert.False(model.IsDynamicInputPolar);
            }
        }
    }
}
