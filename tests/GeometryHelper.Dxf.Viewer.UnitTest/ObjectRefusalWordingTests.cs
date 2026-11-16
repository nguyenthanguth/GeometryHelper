using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// What AutoCAD 2021's console says to an answer a prompt for an object, a corner of a window, a point of a fence, a radius,
    /// the diameters of a donut or the operations of UNDO cannot take, each asked through its standard input with text, nought,
    /// a negative number and one past the most: the words, and the prompt asked again where it asks again.
    /// </summary>
    public class ObjectRefusalWordingTests
    {
        /// <summary>Types one answer and checks the lines the history gains: the answer, then the words.</summary>
        private static void Refuses(CadCommandSession session, string typed, params string[] words)
        {
            string prompt = session.PromptText;
            int before = session.History.Count;

            Run(session, typed);

            Assert.Equal(new[] { prompt + " " + typed }.Concat(words), session.History.Skip(before));
        }

        private const string EveryOption =
            "Window/Last/Crossing/BOX/ALL/Fence/WPolygon/CPolygon/Group/Add/Remove/Multiple/Previous/Undo/AUto/SIngle";

        [Theory]
        [InlineData("BREAK", "Window/Last/Crossing/BOX/ALL/Fence/WPolygon/CPolygon")]
        [InlineData("FILLET", "Window/Last/Crossing/BOX/Fence/WPolygon/CPolygon/Undo/Polyline/Radius/Trim")]
        [InlineData(
            "CHAMFER",
            "Window/Last/Crossing/BOX/Fence/WPolygon/CPolygon/Undo/Polyline/Distance/Angle/Trim/mEthod"
        )]
        [InlineData("PEDIT", "Window/Last/Crossing/BOX/ALL/Fence/WPolygon/CPolygon/Multiple]:")]
        [InlineData("LENGTHEN", "Last/Fence/DElta/Percent/Total")]
        [InlineData("SPLINEDIT", "Window/Last/Crossing/BOX/ALL/Fence/WPolygon/CPolygon/Group/Previous/AUto")]
        [InlineData("HATCHEDIT", "Window/Last/Crossing/BOX/ALL/Fence/WPolygon/CPolygon")]
        [InlineData("LAYOFF", "Settings")]
        [InlineData("LAYFRZ", "Settings")]
        [InlineData("LAYULK", "Last")]
        [InlineData("TEXTEDIT", "Last/Undo")]
        public void APromptForOneObject_AnsweredWithText_ListsWhatItExpects(string command, string options)
        {
            CadCommandSession session = Run(Session(Plan()), command);

            Refuses(session, "abc", "*Invalid selection*", "Expects a point or", options);
        }

        [Fact]
        public void OffsetTrimAndExtend_ListTheirOwnOptions()
        {
            CadCommandSession session = Run(Session(Plan()), "OFFSET", "5");
            Refuses(session, "abc", "*Invalid selection*", "Expects a point or", "Exit");

            session = Run(Session(Plan()), "TRIM");
            Refuses(
                session,
                "abc",
                "*Invalid selection*",
                "Expects a point or",
                "Fence/cuTting edges/Crossing/mOde/Project/Edge"
            );

            session = Run(Session(Plan()), "EXTEND");
            Refuses(
                session,
                "abc",
                "*Invalid selection*",
                "Expects a point or",
                "Fence/Boundary edges/Crossing/mOde/Project"
            );
        }

        [Theory]
        [InlineData("DIVIDE")]
        [InlineData("MEASURE")]
        [InlineData("CENTERMARK")]
        public void APromptForOneObject_WithNoOptionsOfItsOwn_ExpectsASingleObject(string command)
        {
            CadCommandSession session = Run(Session(Plan()), command);

            Refuses(session, "abc", "*Invalid selection*", "Expects a single object.");
        }

        [Theory]
        [InlineData("DIMRADIUS")]
        [InlineData("DIMDIAMETER")]
        public void ARadialDimensionsObject_AnsweredWithText_ExpectsASingleObject(string command)
        {
            CadCommandSession session = Run(Session(Blank()), command);

            Refuses(session, "abc", "*Invalid selection*", "Expects a single object.");
        }

        [Theory]
        [InlineData("W")]
        [InlineData("C")]
        [InlineData("BOX")]
        public void ACornerThatIsNoPoint_IsAnInvalidWindowSpecification_AndTheFirstCornerIsAskedAgain(string option)
        {
            CadCommandSession session = Run(Session(Plan()), "ERASE", option);

            Refuses(session, "abc", "Invalid window specification.");
            Assert.Equal("Specify first corner:", session.PromptText);

            Run(session, "-5,-5");
            Assert.StartsWith("Specify opposite corner", session.PromptText);
            Refuses(session, "abc", "Invalid window specification.");
            Assert.Equal("Specify first corner:", session.PromptText);
        }

        [Fact]
        public void EnterAtTheFirstCornerOfAWindow_IsAnInvalidWindowSpecification_AndAsksAgain()
        {
            CadCommandSession session = Run(Session(Plan()), "ERASE", "W");
            int before = session.History.Count;

            Run(session, Enter);

            Assert.Equal(
                new[] { "Specify first corner:", "Invalid window specification." },
                session.History.Skip(before)
            );
            Assert.Equal("Specify first corner:", session.PromptText);
        }

        [Fact]
        public void TheOppositeCornerOfTheImpliedWindow_IsRefusedAsAnInvalidWindowSpecification_AndAskedAgain()
        {
            CadCommandSession session = Run(Session(Plan()), "ERASE", "-50,-50");
            string prompt = session.PromptText;

            Run(session, "abc");

            Assert.Equal("Invalid window specification.", session.History.Last());
            Assert.Equal(prompt, session.PromptText);
            Assert.True(session.IsWindowOpen);
        }

        [Theory]
        [InlineData("F", "-5,-5")]
        [InlineData("WP", "-5,-5")]
        [InlineData("CP", "-5,-5")]
        public void APointOfAFenceOrAPolygonThatIsNoPoint_IsAnInvalidResponse_AndAskedAgain(string option, string first)
        {
            CadCommandSession session = Run(Session(Plan()), "ERASE", option);
            string firstPrompt = session.PromptText;

            Refuses(session, "abc", "Invalid response.");
            Assert.Equal(firstPrompt, session.PromptText);

            Run(session, first);
            string next = session.PromptText;
            Refuses(session, "abc", "Invalid response.");
            Assert.Equal(next, session.PromptText);
        }

        [Fact]
        public void TheRadiusOfACircle_IsRefusedAsAutoCadRefusesIt_AndAskedAgain()
        {
            CadCommandSession session = Run(Session(Plan()), "CIRCLE", "20,20");
            string prompt = session.PromptText;

            Refuses(session, "abc", "Requires numeric radius, point on circumference, or \"D\".");
            Refuses(session, "0", "Value must be positive and nonzero.");
            Refuses(session, "-3", "Value must be positive and nonzero.");
            Assert.Equal(prompt, session.PromptText);

            Run(session, "5");
            Assert.False(session.IsRunning);
            Assert.Equal(5.0, ((DxfCircle)session.Document.WorkingSpace.Block.Last()).Radius, 9);
        }

        [Fact]
        public void TheDiameterOfACircle_IsRefusedAsAutoCadRefusesIt_AndAskedAgain()
        {
            CadCommandSession session = Run(Session(Plan()), "CIRCLE", "20,20", "D");
            string prompt = session.PromptText;
            Assert.StartsWith("Specify diameter of circle", prompt);

            Refuses(session, "abc", "Requires numeric distance or second point.");
            Refuses(session, "0", "Value must be positive and nonzero.");
            Refuses(session, "-3", "Value must be positive and nonzero.");
            Assert.Equal(prompt, session.PromptText);

            Run(session, "4");
            Assert.Equal(2.0, ((DxfCircle)session.Document.WorkingSpace.Block.Last()).Radius, 9);
        }

        [Fact]
        public void TheDiametersOfADonut_AreRefusedAsAutoCadRefusesThem_AndAskedAgain()
        {
            CadCommandSession session = Run(Session(Plan()), "DONUT");
            string inside = session.PromptText;

            Refuses(session, "abc", "Requires numeric distance or two points.");
            Refuses(session, "-1", "Value must be positive.");
            Assert.Equal(inside, session.PromptText);

            // The inside may be nought: a filled ring.
            Run(session, "0");
            string outside = session.PromptText;
            Assert.StartsWith("Specify outside diameter of donut", outside);

            Refuses(session, "abc", "Requires numeric distance or two points.");
            Refuses(session, "0", "Value must be positive and nonzero.");
            Refuses(session, "-5", "Value must be positive and nonzero.");
            Assert.Equal(outside, session.PromptText);

            Run(session, "4");
            Assert.StartsWith("Specify center of donut", session.PromptText);
        }

        [Fact]
        public void UndoAsksForTheNumberOfOperations_WithItsOptions_AndRefusesWhatIsNone()
        {
            CadCommandSession session = Run(Session(Plan()), "UNDO");
            const string asked = "Enter the number of operations to undo or [Auto/Control/BEgin/End/Mark/Back] <1>:";
            Assert.Equal(asked, session.PromptText);

            foreach (string typed in new[] { "abc", "-1", "0", "1.5", "32768", "100000" })
            {
                Refuses(session, typed, "Requires an integer between 1 and 32767, or option keyword.");
                Assert.Equal(asked, session.PromptText);
            }
        }

        [Fact]
        public void UndoMarkAndBack_TakeBackTheStepsDoneSinceTheMark()
        {
            CadDocument document = Plan();
            CadCommandSession session = Session(document);
            Run(
                session,
                "E",
                new[] { "A1" },
                Enter,
                "UNDO",
                "M",
                "E",
                new[] { "A2" },
                Enter,
                "E",
                new[] { "A3" },
                Enter
            );

            Run(session, "UNDO", "B");

            Assert.Equal("Mark encountered", session.History.Last());
            Assert.NotNull(document.FindEntity("A2"));
            Assert.NotNull(document.FindEntity("A3"));
            Assert.Null(document.FindEntity("A1"));
        }

        [Fact]
        public void UndoBackWithNoMark_AsksBeforeUndoingEverything()
        {
            CadDocument document = Plan();
            string start = document.Database.ToText();
            CadCommandSession session = Session(document);
            Run(session, "E", new[] { "A1" }, Enter, "UNDO", "B");

            Assert.Equal("This will undo everything. OK? <Y>", session.PromptText);

            Run(session, Enter);

            Assert.Equal("Everything has been undone", session.History.Last());
            Assert.Equal(start, document.Database.ToText());
        }

        [Fact]
        public void ArrayRectangularAsksForTheNumbersAndTheGripAsAutoCadDoes()
        {
            CadCommandSession session = Run(Session(Plan()), "ARRAYRECT", new[] { "A1" }, Enter);

            Assert.Equal(
                "Select grip to edit array or [ASsociative/Base point/COUnt/Spacing/COLumns/Rows/Levels/eXit]<eXit>:",
                session.PromptText
            );

            Run(session, "COU");
            Assert.Equal("Enter the number of columns or [Expression] <4>:", session.PromptText);

            Run(session, "4");
            Assert.Equal("Enter the number of rows or [Expression] <3>:", session.PromptText);
        }

        [Fact]
        public void ArrayPolarAsksForItsGripAsAutoCadDoes_AndItsRows()
        {
            CadCommandSession session = Run(Session(Plan()), "ARRAYPOLAR", new[] { "A1" }, Enter, P(50, 50));

            Assert.EndsWith("ROTate items/eXit]<eXit>:", session.PromptText);

            Run(session, "ROW");
            Assert.Equal("Enter the number of rows or [Expression] <1>:", session.PromptText);
        }

        [Fact]
        public void ScaleAsksForTheFactorWithNoDefault_AndEnterTakesTheFactorUsedLast()
        {
            CadDocument document = Plan();
            CadCommandSession session = Session(document);
            Run(session, "SCALE", new[] { "A1" }, Enter, P(0, 0));

            Assert.Equal("Specify scale factor or [Copy/Reference]:", session.PromptText);

            Run(session, "2");
            Run(session, "SCALE", new[] { "A1" }, Enter, P(0, 0));
            Assert.Equal("Specify scale factor or [Copy/Reference]:", session.PromptText);

            Run(session, Enter);
            Assert.Equal(400.0, ((DxfLine)document.FindEntity("A1")).EndPoint.X, 9);
        }
    }
}
