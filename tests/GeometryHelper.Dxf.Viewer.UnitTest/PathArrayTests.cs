using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// ARRAYPATH and the PAth type of ARRAY, checked against what AutoCAD 2021's console did (probed through its standard
    /// input with a line 10 long as the object): the items follow the path from the end nearest the pick, a step of the
    /// Measure distance or an equal share of Divide apart, each carrying the object's place relative to the path's start,
    /// turned with the path unless Align items is No, the rows to the left of the way the path runs.
    /// </summary>
    public class PathArrayTests
    {
        private const string Grip =
            "Select grip to edit array or [ASsociative/Method/Base point/Tangent direction/Items/Rows/Levels/Align items/Z direction/eXit]<eXit>:";

        private static CadDocument Drawing(params string[] entities) =>
            CadDocument.From(TestDrawings.Drawing(string.Empty, string.Empty, entities), "path.dxf");

        private static CadCommandSession Start(CadDocument document, params object[] inputs)
        {
            CadCommandSession session = Session(document);
            session.Tolerance = Tolerance.Default;
            return Run(session, inputs);
        }

        /// <summary>The line of 10 to array, and a straight path of 100 along Y = 100 from X 0 to 100.</summary>
        private static CadDocument Straight() =>
            Drawing(TestDrawings.Line(0, 0, 10, 0, handle: "B1"), TestDrawings.Line(0, 100, 100, 100, handle: "B2"));

        /// <summary>The line of 10 to array, and a path of two segments: along Y = 100 to X 100, then up to Y 200.</summary>
        private static CadDocument Bent() =>
            Drawing(
                TestDrawings.Line(0, 0, 10, 0, handle: "B1"),
                TestDrawings.Polyline("0", "B2", false, 0, 100, 0, 100, 100, 0, 100, 200, 0)
            );

        /// <summary>The lines of the model space as "start-end" in four decimals, in the order they were made.</summary>
        private static List<string> Lines(CadDocument document, string except) =>
            All<DxfLine>(document.ModelSpace)
                .Where(l => l.Handle != except)
                .Select(l => Text(l.StartPoint) + "-" + Text(l.EndPoint))
                .ToList();

        private static string Text(GeoPoint3 p) =>
            Math.Round(p.X + 0.0, 4).ToString("0.####", System.Globalization.CultureInfo.InvariantCulture)
            + ","
            + Math.Round(p.Y + 0.0, 4).ToString("0.####", System.Globalization.CultureInfo.InvariantCulture)
            + ","
            + Math.Round(p.Z + 0.0, 4).ToString("0.####", System.Globalization.CultureInfo.InvariantCulture);

        [Fact]
        public void ArrayPathAsksForThePathThenTheOptions_AsAutoCadWords()
        {
            CadDocument document = Straight();

            CadCommandSession session = Start(document, "ARRAYPATH", new[] { "B1" }, Enter);
            Assert.Contains("Type = Path  Associative = No", session.History);
            Assert.Equal("Select path curve:", session.PromptText);

            Run(session, P(20, 100));
            Assert.Equal(Grip, session.PromptText);

            Run(session, "M");
            Assert.Equal("Enter path method [Divide/Measure] <Measure>:", session.PromptText);
            Run(session, "D");
            Assert.Equal(Grip, session.PromptText);

            Run(session, "I");
            Assert.Equal("Enter number of items along path or [Expression] <7>:", session.PromptText);
            Run(session, Esc);
            Assert.False(session.IsRunning);
        }

        [Fact]
        public void MeasureAsksTheDistanceThenTheItems_TheMostBeingWhatFitsTheLength()
        {
            CadDocument document = Straight();
            CadCommandSession session = Start(document, "ARRAYPATH", new[] { "B1" }, Enter, P(20, 100), "I");

            Assert.Equal(
                "Specify the distance between items along path or [Expression] <15.0000>:",
                session.PromptText
            );
            Run(session, "30");
            Assert.Contains("Maximum items = 4", session.History);
            Assert.Equal("Specify number of items or [Fill entire path/Expression] <4>:", session.PromptText);

            Run(session, Enter);
            Assert.Equal(Grip, session.PromptText);
        }

        [Fact]
        public void MeasureFromTheStart_PutsAnItemEveryThirty_TheObjectsStayingWhereTheyAre_OneUndoStep()
        {
            CadDocument document = Straight();
            string start = document.Database.ToText();

            Start(document, "ARRAYPATH", new[] { "B1" }, Enter, P(20, 100), "I", "30", Enter, Enter);

            Assert.Equal(new[] { "30,0,0-40,0,0", "60,0,0-70,0,0", "90,0,0-100,0,0" }, Lines(document, "B2").Skip(1));
            Assert.Equal("0,0,0-10,0,0", Lines(document, "B2")[0]);
            Assert.Equal("ARRAYPATH", document.NextUndo.Name);
            Assert.True(document.Undo());
            Assert.Equal(start, document.Database.ToText());
        }

        [Fact]
        public void APickNearTheEnd_BeginsTheArrayAtTheEnd()
        {
            CadDocument document = Straight();

            Start(document, "ARRAYPATH", new[] { "B1" }, Enter, P(80, 100), "I", "30", Enter, Enter);

            Assert.Equal(
                new[] { "-30,0,0--20,0,0", "-60,0,0--50,0,0", "-90,0,0--80,0,0" },
                Lines(document, "B2").Skip(1)
            );
        }

        [Fact]
        public void DivideSharesThePathEvenly_SevenItemsOfAPathOf100FromTheEndAreSixteenAndTwoThirdsApart()
        {
            CadDocument document = Straight();

            Start(document, "ARRAYPATH", new[] { "B1" }, Enter, P(60, 100), "M", "D", Enter);

            List<string> lines = Lines(document, "B2");
            Assert.Equal(7, lines.Count);
            Assert.Equal("-16.6667,0,0--6.6667,0,0", lines[1]);
            Assert.Equal("-100,0,0--90,0,0", lines[6]);
        }

        [Fact]
        public void ABentPath_TurnsTheAlignedItemsAndPutsRowsToTheLeftOfTheWayItRuns()
        {
            CadDocument document = Bent();

            Start(
                document,
                "ARRAYPATH",
                new[] { "B1" },
                Enter,
                P(20, 100),
                "R",
                "2",
                "8",
                "0",
                "I",
                "60",
                Enter,
                Enter
            );

            List<string> lines = Lines(document, "B2");
            Assert.Equal(
                new[]
                {
                    "0,0,0-10,0,0",
                    "0,8,0-10,8,0",
                    "60,0,0-70,0,0",
                    "60,8,0-70,8,0",
                    "200,120,0-200,130,0",
                    "192,120,0-192,130,0",
                    "200,180,0-200,190,0",
                    "192,180,0-192,190,0",
                },
                lines
            );
        }

        [Fact]
        public void AlignNo_KeepsTheItemsUpright_ButTheRowsStillRunToTheLeftOfThePath()
        {
            CadDocument document = Bent();

            Start(
                document,
                "ARRAYPATH",
                new[] { "B1" },
                Enter,
                P(20, 100),
                "A",
                "N",
                "R",
                "2",
                "8",
                "0",
                "I",
                "60",
                Enter,
                Enter
            );

            Assert.Equal(
                new[]
                {
                    "0,0,0-10,0,0",
                    "0,8,0-10,8,0",
                    "60,0,0-70,0,0",
                    "60,8,0-70,8,0",
                    "100,20,0-110,20,0",
                    "92,20,0-102,20,0",
                    "100,80,0-110,80,0",
                    "92,80,0-102,80,0",
                },
                Lines(document, "B2")
            );
        }

        [Fact]
        public void ABasePoint_MovesTheObjectsSoThatItStandsOnTheStartOfThePath()
        {
            CadDocument document = Straight();

            Start(document, "ARRAYPATH", new[] { "B1" }, Enter, P(20, 100), "B", P(5, 0), "I", "30", Enter, Enter);

            Assert.Equal(
                new[] { "-5,100,0-5,100,0", "25,100,0-35,100,0", "55,100,0-65,100,0", "85,100,0-95,100,0" },
                Lines(document, "B2")
            );
        }

        [Fact]
        public void ATangentDirectionOfNinetyDegrees_TurnsTheFirstItemMinusNinety()
        {
            CadDocument document = Straight();

            Start(
                document,
                "ARRAYPATH",
                new[] { "B1" },
                Enter,
                P(20, 100),
                "T",
                P(0, 0),
                P(0, 10),
                "I",
                "60",
                Enter,
                Enter
            );

            Assert.Equal(new[] { "-100,100,0--100,90,0", "-40,100,0--40,90,0" }, Lines(document, "B2"));
        }

        [Fact]
        public void TangentNormal_StandsTheItemsUpOnThePath_ZAlongTheWay()
        {
            CadDocument document = Straight();

            Start(document, "ARRAYPATH", new[] { "B1" }, Enter, P(20, 100), "T", "N", "I", "30", Enter, Enter);

            Assert.Equal(
                new[]
                {
                    "0,100,-100-0,110,-100",
                    "30,100,-100-30,110,-100",
                    "60,100,-100-60,110,-100",
                    "90,100,-100-90,110,-100",
                },
                Lines(document, "B2")
            );
        }

        [Fact]
        public void AnArcFromItsEnd_TurnsTheSecondItemWithTheArc()
        {
            CadDocument document = Drawing(
                TestDrawings.Line(0, 0, 10, 0, handle: "B1"),
                TestDrawings.Arc(100, 100, 50, 0, 90, handle: "B2")
            );

            Start(document, "ARRAYPATH", new[] { "B1" }, Enter, P(100, 150), "I", "60", Enter, Enter);

            List<string> lines = Lines(document, "B2");
            Assert.Equal(2, lines.Count);
            Assert.Equal("-29.4397,156.9681,0--25.8161,147.6477,0", lines[1]);
        }

        [Fact]
        public void ACirclePath_HoldsThreeItemsOfAHundredAndDividesIntoTheItemsAsked()
        {
            CadDocument document = Drawing(
                TestDrawings.Line(0, 0, 10, 0, handle: "B1"),
                TestDrawings.Circle(100, 100, 50, handle: "B2")
            );

            CadCommandSession session = Start(document, "ARRAYPATH", new[] { "B1" }, Enter, P(100, 150), "I", "100");
            Assert.Contains("Maximum items = 3", session.History);
            Run(session, Enter, Enter);
            Assert.Equal(3, Lines(document, "B2").Count);

            document.Undo();
            Start(document, "ARRAYPATH", new[] { "B1" }, Enter, P(100, 150), "M", "D", "I", "4", Enter);
            Assert.Equal(4, Lines(document, "B2").Count);
        }

        [Fact]
        public void RowsWithAnElevationAndLevels_RaiseTheItemsInZ()
        {
            CadDocument document = Straight();

            Start(
                document,
                "ARRAYPATH",
                new[] { "B1" },
                Enter,
                P(20, 100),
                "R",
                "2",
                "10",
                "5",
                "L",
                "2",
                "10",
                "I",
                "100",
                "1",
                Enter,
                Enter
            );

            Assert.Equal(
                new[] { "0,0,0-10,0,0", "0,0,10-10,0,10", "0,10,5-10,10,5", "0,10,15-10,10,15" },
                Lines(document, "B2")
            );
        }

        [Fact]
        public void ABaseObjectThatIsNotACurve_IsFilteredOut_AndTheSourceMayNotBeThePath()
        {
            CadDocument document = Drawing(
                TestDrawings.Line(0, 0, 10, 0, handle: "B1"),
                TestDrawings.Text(0, 50, 5, "NOTE", handle: "B3")
            );

            CadCommandSession session = Start(document, "ARRAYPATH", new[] { "B1" }, Enter, new[] { "B3" });
            Assert.Contains("1 was filtered out", session.History);
            Assert.Equal("Select path curve:", session.PromptText);

            Run(session, P(5, 0));
            Assert.Contains("Objects selected to be arrayed may not be selected as path curve.", session.History);
            Assert.Equal("Select path curve:", session.PromptText);

            Run(session, Esc);
            Assert.Single(All<DxfLine>(document.ModelSpace));
        }

        [Fact]
        public void ArrayOfTypePath_NowMakesThePathArray()
        {
            CadDocument document = Straight();

            CadCommandSession session = Start(document, "AR", new[] { "B1" }, Enter);
            Assert.Equal("Enter array type [Rectangular/PAth/POlar] <Rectangular>:", session.PromptText);
            Run(session, "PA");
            Assert.Equal("Select path curve:", session.PromptText);
            Run(session, P(20, 100), "I", "30", Enter, Enter);

            Assert.Equal(4, Lines(document, "B2").Count);
        }

        [Fact]
        public void TheMeasureDistanceIsOneAndAHalfTheObjectsWidth_ByDefault()
        {
            CadDocument document = Straight();
            CadCommandSession session = Start(document, "ARRAYPATH", new[] { "B1" }, Enter, P(20, 100), Enter);

            Assert.False(session.IsRunning);
            Assert.Equal(7, Lines(document, "B2").Count);
            Assert.Equal("15,0,0-25,0,0", Lines(document, "B2")[1]);
        }
    }
}
