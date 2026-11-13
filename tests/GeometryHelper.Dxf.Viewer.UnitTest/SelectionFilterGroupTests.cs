using System;
using System.IO;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// FILTER's AND, OR, XOR and NOT groups, its value tests on radii, heights, points and the rest, the Select Filter list of
    /// AutoCAD 2021 and the named lists in the form of filter.nfl. The plan: A1 line (0,0)-(100,0) on Steel, A2 line
    /// (0,0)-(0,50), A3 circle of radius 10 at (200,0), A4 arc of radius 20 at (300,0), A5 polyline, A6 text "NOTE" 5 high at
    /// (0,200), A7 block BOX at (500,0), A8 line (0,-50)-(100,-50) on Locked.
    /// </summary>
    public class SelectionFilterGroupTests
    {
        private static readonly string[] All = { "A1", "A2", "A3", "A4", "A5", "A6", "A7", "A8" };

        private static string[] Pass(CadSelectionFilter filter) => filter.Apply(Plan(), All).ToArray();

        private static CadSelectionFilter Of(params CadFilterLine[] lines)
        {
            var filter = new CadSelectionFilter();

            foreach (CadFilterLine line in lines)
            {
                filter.Lines.Add(line);
            }

            return filter;
        }

        private static CadFilterLine Line(CadFilterProperty property, string value, params string[] operators) =>
            new CadFilterLine(property, value, operators);

        private static CadFilterLine Group(CadFilterProperty property) => new CadFilterLine(property, string.Empty);

        [Theory]
        [InlineData(">=", "15", new[] { "A4" })]
        [InlineData("<", "30", new[] { "A4" })]
        [InlineData("=", "20", new[] { "A4" })]
        [InlineData("!=", "20", new string[0])]
        [InlineData(">", "20", new string[0])]
        [InlineData("<=", "20", new[] { "A4" })]
        public void ArcRadius_IsTestedAsTheOperatorSays(string op, string value, string[] expected)
        {
            Assert.Equal(expected, Pass(Of(Line(CadFilterProperty.ArcRadius, value, op))));
        }

        [Fact]
        public void CircleRadius_TenAgainstTen_IsEqualAndNotGreater()
        {
            Assert.Equal(new[] { "A3" }, Pass(Of(Line(CadFilterProperty.CircleRadius, "10", "="))));
            Assert.Empty(Pass(Of(Line(CadFilterProperty.CircleRadius, "10", ">"))));
            Assert.Equal(new[] { "A3" }, Pass(Of(Line(CadFilterProperty.CircleRadius, "10", ">="))));
            Assert.Equal(new[] { "A3" }, Pass(Of(Line(CadFilterProperty.CircleRadius, "10.00000000001", "="))));
        }

        [Fact]
        public void TextHeight_AndRotation_AreRead()
        {
            Assert.Equal(new[] { "A6" }, Pass(Of(Line(CadFilterProperty.TextHeight, "5", "="))));
            Assert.Equal(new[] { "A6" }, Pass(Of(Line(CadFilterProperty.TextHeight, "4", ">"))));
            Assert.Empty(Pass(Of(Line(CadFilterProperty.TextHeight, "6", ">"))));
            Assert.Equal(new[] { "A6" }, Pass(Of(Line(CadFilterProperty.TextRotation, "0", "="))));
            Assert.Empty(Pass(Of(Line(CadFilterProperty.TextRotation, "90", "="))));
            Assert.Equal(new[] { "A6" }, Pass(Of(Line(CadFilterProperty.TextValue, "NO*"))));
        }

        [Fact]
        public void LineEnd_TestsEachCoordinateWithItsOwnOperator_AndStarLeavesOneOut()
        {
            // X at least 100 and Y and Z not tested: A1 and A8 end at x = 100.
            Assert.Equal(new[] { "A1", "A8" }, Pass(Of(Line(CadFilterProperty.LineEnd, "100,0,0", ">=", "*", "*"))));
            // X at least 100 and Y below 0: A8 alone.
            Assert.Equal(new[] { "A8" }, Pass(Of(Line(CadFilterProperty.LineEnd, "100,0,0", ">=", "<", "*"))));
            // One operator for the three coordinates: the lines that start at the origin.
            Assert.Equal(new[] { "A1", "A2" }, Pass(Of(Line(CadFilterProperty.LineStart, "0,0,0", "="))));
        }

        [Fact]
        public void BlockName_AndPosition_AreRead()
        {
            Assert.Equal(new[] { "A7" }, Pass(Of(Line(CadFilterProperty.BlockName, "bo?"))));
            Assert.Equal(new[] { "A7" }, Pass(Of(Line(CadFilterProperty.BlockPosition, "500,0,0", ">=", "=", "*"))));
            Assert.Empty(Pass(Of(Line(CadFilterProperty.BlockPosition, "500,1,0", "="))));
        }

        [Fact]
        public void NotGroup_TakesOutWhatItsOperandPasses()
        {
            // Every line except those on Steel.
            CadSelectionFilter filter = Of(
                Line(CadFilterProperty.Object, "Line"),
                Group(CadFilterProperty.BeginNot),
                Line(CadFilterProperty.Layer, "Steel"),
                Group(CadFilterProperty.EndNot)
            );
            Assert.Equal(new[] { "A2", "A8" }, Pass(filter));
        }

        [Fact]
        public void OrGroup_PassesWhatOneOperandPasses()
        {
            CadSelectionFilter filter = Of(
                Group(CadFilterProperty.BeginOr),
                Line(CadFilterProperty.Object, "Circle"),
                Line(CadFilterProperty.Object, "Arc"),
                Group(CadFilterProperty.EndOr)
            );
            Assert.Equal(new[] { "A3", "A4" }, Pass(filter));
        }

        [Fact]
        public void XorGroup_PassesWhatExactlyOneOperandPasses()
        {
            // Lines not on Steel, and what is not a line on Steel (nothing).
            CadSelectionFilter filter = Of(
                Group(CadFilterProperty.BeginXor),
                Line(CadFilterProperty.Layer, "Steel"),
                Line(CadFilterProperty.Object, "Line"),
                Group(CadFilterProperty.EndXor)
            );
            Assert.Equal(new[] { "A2", "A8" }, Pass(filter));
        }

        [Fact]
        public void Groups_MayNest()
        {
            // Lines or arcs, but not on Locked: A1, A2, A4.
            CadSelectionFilter filter = Of(
                Group(CadFilterProperty.BeginOr),
                Line(CadFilterProperty.Object, "Line"),
                Line(CadFilterProperty.Object, "Arc"),
                Group(CadFilterProperty.EndOr),
                Group(CadFilterProperty.BeginNot),
                Line(CadFilterProperty.Layer, "Locked"),
                Group(CadFilterProperty.EndNot)
            );
            Assert.Equal(new[] { "A1", "A2", "A4" }, Pass(filter));
        }

        [Fact]
        public void CircleRadiusUnderOne_AsTheHelpWritesIt_UsesNot()
        {
            // Object = Circle, NOT (Circle Radius >= 1): the circle has radius 10, so it is not passed.
            CadSelectionFilter filter = Of(
                Line(CadFilterProperty.Object, "Circle"),
                Group(CadFilterProperty.BeginNot),
                Line(CadFilterProperty.CircleRadius, "1", ">="),
                Group(CadFilterProperty.EndNot)
            );
            Assert.Empty(Pass(filter));
        }

        [Theory]
        [InlineData("EndAnd", "Invalid Filter list - Begin/end clause mismatch.")]
        [InlineData("BeginAnd", "Invalid Filter list - Premature end.")]
        [InlineData("BeginAnd,EndAnd", "Invalid Filter list - Empty clause/bad nesting.")]
        [InlineData("BeginOr,Layer,EndAnd", "Invalid Filter list - Begin/end clause mismatch.")]
        [InlineData("BeginNot,Layer,Layer,EndNot", "Invalid Filter list - Wrong number of XOR/NOT operands.")]
        [InlineData("BeginXor,Layer,EndXor", "Invalid Filter list - Wrong number of XOR/NOT operands.")]
        [InlineData("BeginXor,Layer,Layer,Layer,EndXor", "Invalid Filter list - Wrong number of XOR/NOT operands.")]
        [InlineData(
            "BeginAnd,BeginAnd,BeginAnd,BeginAnd,BeginAnd,BeginAnd,BeginAnd,BeginAnd,BeginAnd,BeginAnd,BeginAnd",
            "Invalid Filter list - Maximum nesting exceeded."
        )]
        public void Validate_RefusesGroupsThatAreNotWellFormed_InAutoCADsWords(string lines, string message)
        {
            var filter = new CadSelectionFilter();

            foreach (string name in lines.Split(','))
            {
                var property = (CadFilterProperty)Enum.Parse(typeof(CadFilterProperty), name);
                filter.Lines.Add(CadFilterLine.IsGroup(property) ? Group(property) : Line(property, "x"));
            }

            Assert.Equal(message, filter.Validate());
            InvalidOperationException refused = Assert.Throws<InvalidOperationException>(() =>
                filter.Passes(Plan().FindEntity("A1"))
            );
            Assert.Equal(message, refused.Message);
        }

        [Fact]
        public void Validate_WellFormedGroups_AreAccepted()
        {
            CadSelectionFilter filter = Of(
                Group(CadFilterProperty.BeginXor),
                Group(CadFilterProperty.BeginAnd),
                Line(CadFilterProperty.Layer, "Steel"),
                Line(CadFilterProperty.Object, "Line"),
                Group(CadFilterProperty.EndAnd),
                Line(CadFilterProperty.Object, "Arc"),
                Group(CadFilterProperty.EndXor)
            );
            Assert.Null(filter.Validate());
            Assert.Null(new CadSelectionFilter().Validate());
        }

        [Fact]
        public void FilterLines_RefuseWhatAutoCADsDialogRefuses()
        {
            Assert.Equal(
                "Invalid Number.",
                Assert.Throws<ArgumentException>(() => Line(CadFilterProperty.ArcRadius, "ten")).Message.Split('\r')[0]
            );
            Assert.Equal(
                "Invalid Y coordinate.",
                Assert.Throws<ArgumentException>(() => Line(CadFilterProperty.LineEnd, "1,x,3")).Message.Split('\r')[0]
            );
            Assert.Equal(
                "Invalid Z coordinate.",
                Assert.Throws<ArgumentException>(() => Line(CadFilterProperty.LineEnd, "1,2")).Message.Split('\r')[0]
            );
            Assert.Equal(
                "Invalid Name.",
                Assert.Throws<ArgumentException>(() => Line(CadFilterProperty.Layer, "  ")).Message.Split('\r')[0]
            );
            Assert.Throws<ArgumentException>(() => Line(CadFilterProperty.Layer, "A", "<"));
            Assert.Throws<ArgumentException>(() => Line(CadFilterProperty.ArcRadius, "1", "*"));
            Assert.Throws<ArgumentException>(() => Line(CadFilterProperty.LineEnd, "1,2,3", "=", "="));
        }

        [Fact]
        public void FilterLine_Color_TestsTheIndexWithTheOperator()
        {
            CadDocument document = Plan();
            // Every object of the plan is ByLayer (256): above 7 passes all, below 8 none, equal to 256 all.
            Assert.Equal(All, Of(Line(CadFilterProperty.Color, "256", "=")).Apply(document, All).ToArray());
            CadSelectionFilter over = Of(Line(CadFilterProperty.Color, "7", ">"));
            Assert.Equal(All, over.Apply(document, All).ToArray());
            CadSelectionFilter under = Of(Line(CadFilterProperty.Color, "8", "<"));
            Assert.Empty(under.Apply(document, All));
        }

        [Fact]
        public void FilterLine_Text_ShowsTheOperatorAndEachCoordinate()
        {
            Assert.Equal("Arc Radius           >= 15.0000", Line(CadFilterProperty.ArcRadius, "15", ">=").Text);
            Assert.Equal(
                "Line End             X >= 100.0000  Y * 0.0000  Z * 0.0000",
                Line(CadFilterProperty.LineEnd, "100,0,0", ">=", "*", "*").Text
            );
            Assert.Equal("** Begin   NOT", Group(CadFilterProperty.BeginNot).Text);
            Assert.Equal("Text Rotation        = 90.0000", Line(CadFilterProperty.TextRotation, "90").Text);
        }

        [Fact]
        public void Choices_AreAutoCADs2021SelectFilterList()
        {
            string[] names = CadSelectionFilter.Choices.Select(c => c.Name).ToArray();
            Assert.Equal("Arc", names[0]);
            Assert.Equal("Arc Center", names[1]);
            Assert.Equal("Arc Radius", names[2]);
            Assert.Contains("Circle Radius", names);
            Assert.Contains("Xdata ID", names);
            Assert.Contains("Linetype Scale", names);
            Assert.Contains("Text Rotation", names);
            Assert.Equal(
                new[]
                {
                    "** Begin   AND",
                    "** End      AND",
                    "** Begin   OR",
                    "** End      OR",
                    "** Begin   XOR",
                    "** End      XOR",
                    "** Begin   NOT",
                    "** End      NOT",
                },
                names.Skip(names.Length - 8)
            );
            CadFilterChoice line = CadSelectionFilter.Choices.First(c => c.Name == "Line");
            Assert.Equal(CadFilterProperty.Object, line.Property);
            Assert.Equal("Line", line.Value);
            Assert.Null(CadSelectionFilter.Choices.First(c => c.Name == "Layer").Value);
        }

        [Fact]
        public void ObjectKinds_OfAutoCADsList_MatchTheEntitiesTheyName()
        {
            Assert.Equal(new[] { "A5" }, Pass(Of(Line(CadFilterProperty.Object, "Polyline"))));
            Assert.Equal(new[] { "A7" }, Pass(Of(Line(CadFilterProperty.Object, "Block"))));
            Assert.Equal(new[] { "A6" }, Pass(Of(Line(CadFilterProperty.Object, "Text"))));
            Assert.Equal(new[] { "A1", "A2", "A8" }, Pass(Of(Line(CadFilterProperty.Object, "Line"))));
            Assert.Equal(new[] { "A7" }, Pass(Of(Line(CadFilterProperty.Object, "Block Reference"))));
        }

        [Fact]
        public void Names_OfASavedList_RefuseTheCharactersAutoCADRefuses()
        {
            CadSelectionFilter filter = Of(Line(CadFilterProperty.Layer, "WALLS"));
            Assert.Equal("Invalid Filter name.", filter.SaveAs("*unnamed"));
            Assert.Equal("Invalid characters in Filter name.", filter.SaveAs("walls.v1"));
            Assert.Equal("Invalid characters in Filter name.", filter.SaveAs("a(b)"));
            Assert.Equal("Invalid characters in Filter name.", filter.SaveAs("a#b"));
            Assert.Null(filter.SaveAs("Walls and doors"));
        }

        [Fact]
        public void Nfl_IsWrittenAsAutoCADsFilterNflIs_AndReadBack()
        {
            CadSelectionFilter filter = Of(
                Line(CadFilterProperty.Object, "Circle"),
                Line(CadFilterProperty.Layer, "WALLS"),
                Group(CadFilterProperty.BeginNot),
                Line(CadFilterProperty.CircleRadius, "1.5", ">="),
                Group(CadFilterProperty.EndNot),
                Line(CadFilterProperty.CircleCenter, "1,1,0", ">=", ">=", "*"),
                Line(CadFilterProperty.Color, "3", "<"),
                Line(CadFilterProperty.TextRotation, "90")
            );
            Assert.Null(filter.SaveAs("Round"));

            string text = filter.ToNfl();
            string[] rows = text.Split('\n');
            Assert.Equal("Filter.nfl  --  Do not edit this file.", rows[0]);
            Assert.Equal(":ai_lisp|Round", rows[1]);
            Assert.Equal("(0 . \"CIRCLE\")", rows[2]);
            Assert.Equal("(8 . \"WALLS\")", rows[3]);
            Assert.Equal("(-4 . \"<NOT\")", rows[4]);
            Assert.Equal("(-4 . \">=\")", rows[5]);
            Assert.Equal("(40 . 1.5)", rows[6]);
            Assert.Equal("(-4 . \"NOT>\")", rows[7]);
            Assert.Equal("(-4 . \">=,>=,*\")", rows[8]);
            Assert.Equal("(10 1.0 1.0 0.0)", rows[9]);
            Assert.Equal("(-4 . \"<\")", rows[10]);
            Assert.Equal("(62 . 3)", rows[11]);
            Assert.Equal("(50 . 1.5707963267948966)", rows[12]);
            Assert.Equal(":ai_str|Round", rows[13]);
            Assert.Equal("Object\t\t=\tCircle", rows[14]);
            Assert.Equal("Layer\t\t=\tWALLS", rows[15]);
            Assert.Equal("** Begin   NOT\t", rows[16]);
            Assert.Equal("Circle Radius\t\t>=\t1.5000", rows[17]);
            Assert.Equal("** End      NOT\t", rows[18]);
            Assert.Equal("Circle Center\tX\t>=\t1.0000\tY\t>=\t1.0000\tZ\t*\t0.0000", rows[19]);

            var again = new CadSelectionFilter();
            Assert.Null(again.LoadNfl(text));
            Assert.Equal(new[] { CadSelectionFilter.Unnamed, "Round" }, again.Names);
            Assert.True(again.Restore("Round"));
            Assert.Equal(filter.Lines.Select(l => l.Text), again.Lines.Select(l => l.Text));
            Assert.Equal(filter.Lines.Select(l => l.Value), again.Lines.Select(l => l.Value));
            Assert.Equal(text, again.ToNfl());
        }

        [Fact]
        public void Nfl_FromAutoCAD_IsReadWithItsPaddedWordsAndCrLf()
        {
            string text =
                "Filter.nfl  --  Do not edit this file.\r\n"
                + ":ai_lisp|Small\r\n"
                + "(0 . \"ARC\")\r\n"
                + "(-4 . \"<=\")\r\n"
                + "(40 . 2.5)\r\n"
                + ":ai_str|Small\r\n"
                + "Entity         \t\t=\tArc\r\n"
                + "Arc Radius\t\t<=\t2.5000\r\n";
            var filter = new CadSelectionFilter();
            Assert.Null(filter.LoadNfl(text));
            Assert.True(filter.Restore("Small"));
            Assert.Equal(
                new[] { "Object               = Arc", "Arc Radius           <= 2.5000" },
                filter.Lines.Select(l => l.Text)
            );
        }

        [Fact]
        public void Nfl_ThatDoesNotRead_SaysSoAndKeepsTheListsHeld()
        {
            CadSelectionFilter filter = Of(Line(CadFilterProperty.Layer, "WALLS"));
            filter.SaveAs("Keep");

            Assert.Equal(
                "Syntax error in Named Filter List file.  Delete filter.nfl.",
                filter.LoadNfl("Filter.nfl  --  Do not edit this file.\n:ai_lisp|X\n(8 . \"A\")\n")
            );
            Assert.Equal(
                "Syntax error in Named Filter List file.  Delete filter.nfl.",
                filter.LoadNfl("x\n:ai_lisp|X\nnot a list\n:ai_str|X\nLayer\t\t=\tA\n")
            );
            Assert.Equal(new[] { CadSelectionFilter.Unnamed, "Keep" }, filter.Names);
            Assert.Null(filter.LoadNfl("Filter.nfl  --  Do not edit this file.\n"));
            Assert.Equal(new[] { CadSelectionFilter.Unnamed }, filter.Names);
        }

        [Fact]
        public void FilePath_KeepsTheNamedLists_AcrossFilters()
        {
            string folder = Path.Combine(Path.GetTempPath(), "gh-nfl-" + Guid.NewGuid().ToString("N"));
            string path = Path.Combine(folder, "filter.nfl");

            try
            {
                CadSelectionFilter first = Of(Line(CadFilterProperty.Layer, "WALLS"));
                first.FilePath = path;
                Assert.Null(first.SaveAs("Walls"));
                Assert.True(File.Exists(path));

                var second = new CadSelectionFilter { FilePath = path };
                Assert.Null(second.Reload());
                Assert.Equal(new[] { CadSelectionFilter.Unnamed, "Walls" }, second.Names);

                Assert.True(second.Restore("Walls"));
                Assert.Null(second.DeleteCurrent());
                Assert.Equal("Walls", first.Names[1]);
                Assert.Null(first.Reload());
                Assert.Equal(new[] { CadSelectionFilter.Unnamed }, first.Names);
            }
            finally
            {
                if (Directory.Exists(folder))
                {
                    Directory.Delete(folder, true);
                }
            }
        }

        [Fact]
        public void Filter_Apply_WithGroupsThatAreNotWellFormed_SaysWhyAndAppliesNothing()
        {
            CadDocument document = Plan();
            CadCommandSession session = Session(document);
            session.UiRequested += (s, e) =>
            {
                if (e.Name == "FILTER")
                {
                    var filter = (CadSelectionFilter)e.Payload;
                    filter.Lines.Clear();
                    filter.Lines.Add(Group(CadFilterProperty.BeginNot));
                    e.Answer = "Apply";
                    e.Handled = true;
                }
            };
            Run(session, "FILTER");

            Assert.False(session.IsRunning);
            Assert.Equal("Invalid Filter list - Premature end.", session.History.Last());
        }

        [Fact]
        public void Filter_AddSelectedObject_AddsWhatBelongsToItsKind()
        {
            CadDocument document = Plan();
            CadCommandSession session = Session(document);
            int asked = 0;
            session.UiRequested += (s, e) =>
            {
                if (e.Name == "FILTER")
                {
                    e.Answer = asked++ == 0 ? "Add Selected Object" : null;
                    e.Handled = true;
                }
            };
            Run(session, "FILTER");
            session.FeedSelection(new[] { "A4" });

            Assert.Equal(
                new[]
                {
                    "Object               = Arc",
                    "Layer                = 0",
                    "Color                = 256 - By Layer",
                    "Linetype             = ByLayer",
                    "Arc Center           X = 300.0000  Y = 0.0000  Z = 0.0000",
                    "Arc Radius           = 20.0000",
                },
                session.SelectionFilter.Lines.Select(l => l.Text)
            );
            Assert.Equal(new[] { "A4" }, session.SelectionFilter.Apply(document, All));
        }
    }
}
