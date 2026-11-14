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
    /// HATCH follow-ups of round 3: -HATCH's draW boundary drawing arc segments, gap bridges from the end of a line to the middle of
    /// another, and Boundary set as AutoCAD 2021's console runs it. The prompts, the arcs drawn and the areas were read from
    /// AutoCAD 2021 by accoreconsole probes (an arc to (100, 50) leaving a line from (0, 0) to (100, 0) is the half circle of
    /// radius 25 about (100, 25); closed with CLose it is the arc of radius 125 about (100, -75) back to (0, 0)).
    /// </summary>
    public class HatchFollowUpsThreeTests
    {
        private const string ArcFirst =
            "Enter an arc boundary option [Angle/CEnter/Direction/Line/Radius/Second pt/Undo/Endpoint of arc] <Endpoint>:";
        private const string ArcLater =
            "Enter an arc boundary option [Angle/CEnter/CLose/Direction/Line/Radius/Second pt/Undo/Endpoint of arc] <Endpoint>:";
        private const string LinePrompt = "Specify next point or [Arc/Close/Length/Undo]:";

        private static CadCommandSession Start(CadDocument document, params object[] inputs)
        {
            CadCommandSession session = Session(document);
            session.Tolerance = Tolerance.Default;
            return Run(session, inputs);
        }

        private static DxfHatch OnlyHatch(CadDocument document) => Assert.Single(All<DxfHatch>(document.ModelSpace));

        /// <summary>The area between an arc of a radius and sweep and its chord.</summary>
        private static double Segment(double radius, double sweep) => radius * radius * (sweep - Math.Sin(sweep)) / 2.0;

        #region draW boundary: Arc

        [Fact]
        public void DrawBoundary_Arc_AsksAnArcBoundaryOptionWithCloseOnlyFromTheSecondSegment()
        {
            CadCommandSession session = Start(Blank(), "-HATCH", "W", "N", P(0, 0), "A");
            Assert.Equal(ArcFirst, session.PromptText);

            Run(session, P(50, 50));
            Assert.Equal(ArcLater, session.PromptText);
        }

        [Fact]
        public void DrawBoundary_Arc_EnterTakesTheDefaultEndpointAndEndsTheShape()
        {
            CadCommandSession session = Start(Blank(), "-HATCH", "W", "N", P(0, 0), "A", P(50, 50), Enter);

            Assert.Equal("Specify start point for new boundary or <Accept>:", session.PromptText);
        }

        [Fact]
        public void DrawBoundary_LineThenArcThenLineClosed_IsAnAreaOfTheRectangleAndTheHalfCircle()
        {
            CadDocument document = Blank();
            Start(document, "-HATCH", "W", "N", P(0, 0), P(100, 0), "A", P(100, 50), "L", P(0, 50), "C", Enter, Enter);

            DxfHatch hatch = OnlyHatch(document);
            Assert.Equal(1, hatch.NumberOfLoops);
            Assert.Equal(5000.0 + Segment(25, Math.PI), hatch.Area, 3);
        }

        [Fact]
        public void DrawBoundary_ArcClose_ClosesWithAnArcLeavingTheWayTheShapeCame()
        {
            CadDocument document = Blank();
            Start(document, "-HATCH", "W", "N", P(0, 0), P(100, 0), "A", P(100, 50), "CL", Enter, Enter);

            // The triangle (0, 0) (100, 0) (100, 50) of 2 500, the half circle of radius 25 on its right side, and the arc of radius
            // 125 about (100, -75) along its hypotenuse.
            double chord = Math.Sqrt(100.0 * 100.0 + 50.0 * 50.0);
            double sweep = 2.0 * Math.Asin(chord / 2.0 / 125.0);
            DxfHatch hatch = OnlyHatch(document);
            Assert.Equal(2500.0 + Segment(25, Math.PI) + Segment(125, sweep), hatch.Area, 3);
        }

        [Fact]
        public void DrawBoundary_ArcFirst_LeavesEastAndIsAChordAndASegment()
        {
            CadDocument document = Blank();
            Start(document, "-HATCH", "W", "N", P(0, 0), "A", P(50, 50), Enter, Enter, Enter);

            // Leaving along +X to (50, 50): the quarter circle about (0, 50), closed across by its chord.
            DxfHatch hatch = OnlyHatch(document);
            Assert.Equal(Segment(50, Math.PI / 2.0), hatch.Area, 3);
        }

        [Fact]
        public void DrawBoundary_Arc_FollowsTheWayTheLastLineWasDrawnAtTheFirstPoint()
        {
            CadDocument document = Blank();
            CadCommandSession session = Start(document, "LINE", P(0, 0), P(10, 10), Enter);
            Run(session, "-HATCH", "W", "N", P(0, 0), "A", P(50, 50));

            // (50, 50) lies along the line's way: no arc leaves it, and the arc options are asked again.
            Assert.Equal(ArcFirst, session.PromptText);
        }

        [Fact]
        public void DrawBoundary_ArcOptions_AngleCEnterDirectionRadiusAndSecondPt_DrawTheArcsOfPline()
        {
            // Angle: 90 degrees counter-clockwise from (100, 0) to (150, 50) about (100, 50).
            CadDocument angle = Blank();
            Start(angle, "-HATCH", "W", "N", P(0, 0), P(100, 0), "A", "A", "90", P(150, 50), Enter, Enter, Enter);
            double quarter = 2500.0 + Segment(50, Math.PI / 2.0);
            Assert.Equal(quarter, OnlyHatch(angle).Area, 3);

            // CEnter: about (100, 50) from (100, 0) to the way of (200, 50): the same arc.
            CadDocument center = Blank();
            Start(
                center,
                "-HATCH",
                "W",
                "N",
                P(0, 0),
                P(100, 0),
                "A",
                "CE",
                P(100, 50),
                P(200, 50),
                Enter,
                Enter,
                Enter
            );
            Assert.Equal(quarter, OnlyHatch(center).Area, 3);

            // Direction 90 degrees, to (150, 50): clockwise about (150, 0), the other quarter of the two.
            CadDocument direction = Blank();
            Start(direction, "-HATCH", "W", "N", P(0, 0), P(100, 0), "A", "D", "90", P(150, 50), Enter, Enter, Enter);
            Assert.Equal(2500.0 - Segment(50, Math.PI / 2.0), OnlyHatch(direction).Area, 3);

            // Radius 50 to (150, 50): the shorter arc counter-clockwise, about (100, 50).
            CadDocument radius = Blank();
            Start(radius, "-HATCH", "W", "N", P(0, 0), P(100, 0), "A", "R", "50", P(150, 50), Enter, Enter, Enter);
            Assert.Equal(quarter, OnlyHatch(radius).Area, 3);

            // Second pt (150, 0)... a point (135.3553, 14.6447) on that arc, to (150, 50).
            CadDocument second = Blank();
            double on = 100.0 + 50.0 * Math.Sin(Math.PI / 4.0);
            double up = 50.0 - 50.0 * Math.Cos(Math.PI / 4.0);
            Start(second, "-HATCH", "W", "N", P(0, 0), P(100, 0), "A", "S", P(on, up), P(150, 50), Enter, Enter, Enter);
            Assert.Equal(quarter, OnlyHatch(second).Area, 3);
        }

        [Fact]
        public void DrawBoundary_Arc_RadiusTooSmallSaysInvalidAndAsksAgain()
        {
            CadCommandSession session = Start(
                Blank(),
                "-HATCH",
                "W",
                "N",
                P(0, 0),
                P(100, 0),
                "A",
                "R",
                "10",
                P(150, 50)
            );

            Assert.Equal(ArcLater, session.PromptText);
            Assert.Contains("*Invalid*", session.History);
        }

        [Fact]
        public void DrawBoundary_Undo_TakesTheLastSegmentOffAndGoesOnInLineMode()
        {
            CadCommandSession session = Start(
                Blank(),
                "-HATCH",
                "W",
                "N",
                P(0, 0),
                P(100, 0),
                "A",
                P(100, 50),
                P(50, 100)
            );
            Assert.Equal(ArcLater, session.PromptText);

            Run(session, "U");
            Assert.Equal(LinePrompt, session.PromptText);

            // Undone at the first point there is nothing to take off, and the prompt stays.
            CadCommandSession first = Start(Blank(), "-HATCH", "W", "N", P(0, 0), "U");
            Assert.Equal("Specify next point or [Arc/Length/Undo]:", first.PromptText);
            Assert.Contains("All segments already undone.", first.History);
        }

        [Fact]
        public void DrawBoundary_Length_GoesOnAlongTheTangentOfTheArcLeftBehind()
        {
            CadDocument document = Blank();
            Start(
                document,
                "-HATCH",
                "W",
                "N",
                P(0, 0),
                P(100, 0),
                "A",
                P(100, 50),
                "L",
                "L",
                "30",
                Enter,
                Enter,
                Enter
            );

            // The arc leaves (100, 50) towards -X: the line of 30 ends at (70, 50).
            // The corners (0, 0) (100, 0) (100, 50) (70, 50) close 3 250 square units, and the half circle adds its own.
            Assert.Equal(3250.0 + Segment(25, Math.PI), OnlyHatch(document).Area, 3);
        }

        [Fact]
        public void DrawBoundary_ARetainedShapeWithAnArc_KeepsTheBulgeInItsPolyline()
        {
            CadDocument document = Blank();
            Start(document, "-HATCH", "W", "Y", P(0, 0), P(100, 0), "A", P(100, 50), "L", P(0, 50), "C", Enter, Enter);

            DxfPolyline kept = Assert.Single(All<DxfPolyline>(document.ModelSpace));
            Assert.True(kept.Closed);
            Assert.Equal(4, kept.NumberOfVertices);
            Assert.Equal(1.0, kept.GetBulgeAt(1), 6);
        }

        [Fact]
        public void DrawBoundary_ClockwiseShapeWithAnArc_IsTheSameAreaAsTheCounterClockwiseOne()
        {
            CadDocument ccw = Blank();
            Start(ccw, "-HATCH", "W", "N", P(0, 0), P(100, 0), "A", P(100, 50), "L", P(0, 50), "C", Enter, Enter);

            // The same rectangle and half circle drawn the other way round: along the top, the arc down the right, back along
            // the bottom.
            CadDocument cw = Blank();
            Start(cw, "-HATCH", "W", "N", P(0, 50), P(100, 50), "A", P(100, 0), "L", P(0, 0), "C", Enter, Enter);

            Assert.Equal(OnlyHatch(ccw).Area, OnlyHatch(cw).Area, 3);
        }

        #endregion

        #region Boundary set

        /// <summary>Four LINEs round a square of 100 and four round one of 60 inside it, as <c>Squares(0, 0, 100, 20, 20, 60)</c> does.</summary>
        private static CadDocument TwoSquares()
        {
            var database = new DxfDatabase("AC1027");

            foreach ((double x, double side) in new[] { (0.0, 100.0), (20.0, 60.0) })
            {
                GeoPoint3[] corners =
                {
                    new GeoPoint3(x, x, 0),
                    new GeoPoint3(x + side, x, 0),
                    new GeoPoint3(x + side, x + side, 0),
                    new GeoPoint3(x, x + side, 0),
                };

                for (int i = 0; i < 4; i++)
                {
                    database.ModelSpace.AppendEntity(new DxfLine(corners[i], corners[(i + 1) % 4]));
                }
            }

            return CadDocument.From(database, "two squares.dxf");
        }

        [Fact]
        public void BoundarySet_TheOuterSquareAlone_HatchesItWithNoIslandAndSaysNothingIsNotFound()
        {
            // AutoCAD 2021's console: the set of the outer square, a point inside the inner one: one loop, the outer, no complaint.
            CadDocument document = TwoSquares();
            string[] outer = All<DxfLine>(document.ModelSpace).Take(4).Select(l => l.Handle).ToArray();
            CadCommandSession session = Start(document, "-HATCH", "A", "B", "N", outer, Enter, Enter, P(50, 50), Enter);

            Assert.DoesNotContain("Valid hatch boundary not found.", session.History);
            DxfHatch hatch = OnlyHatch(document);
            Assert.Equal(1, hatch.NumberOfLoops);
            Assert.Equal(10000.0, hatch.Area, 6);
        }

        [Fact]
        public void BoundarySet_APointInNothing_SaysNotFoundOnceAndMakesNoHatch()
        {
            CadDocument document = TwoSquares();
            string[] inner = All<DxfLine>(document.ModelSpace).Skip(4).Select(l => l.Handle).ToArray();
            CadCommandSession session = Start(
                document,
                "-HATCH",
                "A",
                "B",
                "N",
                inner,
                Enter,
                Enter,
                P(500, 500),
                Enter
            );

            Assert.Single(session.History, "Valid hatch boundary not found.");
            Assert.Empty(All<DxfHatch>(document.ModelSpace));
        }

        [Fact]
        public void HatchPick_WithNoBoundarySet_SaysNotFoundWithoutAnalyzingIslands()
        {
            CadCommandSession session = Start(TwoSquares(), "-HATCH", P(500, 500));

            Assert.Contains("Analyzing the selected data...", session.History);
            Assert.Contains("Valid hatch boundary not found.", session.History);
            Assert.DoesNotContain("Analyzing internal islands...", session.History);
        }

        #endregion

        #region gap bridges

        /// <summary>
        /// A box 100 high of four LINEs: the bottom from (0, 0) to (<paramref name="bottomEnd"/>, 0), the top, the left side, and
        /// the right side from (<paramref name="rightX"/>, <paramref name="rightY"/>) up to (100, 100), where the last two points
        /// are the end of a line that stands off the bottom.
        /// </summary>
        private static CadDocument BoxWithARightSide(double bottomEnd, double rightX, double rightY)
        {
            var database = new DxfDatabase("AC1027");
            database.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(bottomEnd, 0, 0)));
            database.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(0, 100, 0), new GeoPoint3(100, 100, 0)));
            database.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(0, 100, 0)));
            database.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(rightX, rightY, 0), new GeoPoint3(100, 100, 0)));
            return CadDocument.From(database, "box.dxf");
        }

        private static IReadOnlyList<CadBoundaryLoop> FindWithGap(CadDocument document, double gap) =>
            CadBoundaryFinder.Find(document, document.ModelSpace, P(50, 50), Tolerance.Default, null, null, gap);

        [Fact]
        public void Gap_AnEndStandingTwoOffTheMiddleOfALine_IsBridgedToItWhereTheToleranceReaches()
        {
            // The bottom runs on to x 120, the right side ends at (100, 2): AutoCAD 2021 at HPGAPTOL 5 hatches the 100 by 100 box
            // and drops the 20 of the bottom that stands past it.
            CadDocument document = BoxWithARightSide(120, 100, 2);

            CadBoundaryLoop loop = Assert.Single(FindWithGap(document, 5.0));
            Assert.Equal(10000.0, loop.Area, 6);
            // AutoCAD closes a line ending 2 off a bar at a gap of 0.9 in a view 140 high (a pixel of 0.262) and not at 0.85: a way on
            // reaches twice the gap and a pixel; with no view, twice the gap.
            Assert.Null(FindWithGap(document, 0.5));
            Assert.Single(FindWithGap(document, 1.0));
        }

        [Fact]
        public void Gap_ALineThatLeansInGoesOnAlongItsOwnDirectionToTheOtherLine()
        {
            // The right side from (100, 100) to (97, 3) goes on to y 0 at x 96.9072: AutoCAD's loop has that corner, where the
            // straight piece from its end to the end of the bottom (4.24 long) was the other way to close it.
            var database = new DxfDatabase("AC1027");
            database.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0)));
            database.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(0, 100, 0), new GeoPoint3(100, 100, 0)));
            database.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(0, 100, 0)));
            database.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(100, 100, 0), new GeoPoint3(97, 3, 0)));
            CadDocument document = CadDocument.From(database, "lean.dxf");

            CadBoundaryLoop loop = Assert.Single(FindWithGap(document, 5.0));
            double corner = 97.0 - 3.0 * 3.0 / 97.0;
            Assert.Equal((corner * 100.0 + 10000.0) / 2.0, loop.Area, 4);
        }

        [Fact]
        public void Gap_TwoLinesWhoseDirectionsMeetPastTheirEnds_MeetAtTheCorner()
        {
            // The bottom ends at (100, 0) and the right side at (103, 3): their ways on meet at (103.0928, 0), 3.09 from each end,
            // where the straight piece between the ends would have been 4.24 long. AutoCAD's loop runs through that corner.
            var database = new DxfDatabase("AC1027");
            database.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0)));
            database.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(0, 100, 0), new GeoPoint3(100, 100, 0)));
            database.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(0, 100, 0)));
            database.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(100, 100, 0), new GeoPoint3(103, 3, 0)));
            CadDocument document = CadDocument.From(database, "corner.dxf");

            CadBoundaryLoop loop = Assert.Single(FindWithGap(document, 5.0));
            double corner = 103.0 + 3.0 * 3.0 / 97.0;
            Assert.Equal((corner * 100.0 + 10000.0) / 2.0, loop.Area, 4);
            Assert.Null(FindWithGap(document, 3.0));
        }

        [Fact]
        public void Gap_AGapOfTwoBetweenCollinearLinesAndTheEndStillClosesAsBefore()
        {
            // The right side in two pieces with 4 between them, standing 2 off the bottom: closed at 5 and not at 3.
            var database = new DxfDatabase("AC1027");
            database.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0)));
            database.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(0, 100, 0), new GeoPoint3(100, 100, 0)));
            database.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(0, 100, 0)));
            database.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(100, 100, 0), new GeoPoint3(100, 52, 0)));
            database.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(100, 48, 0), new GeoPoint3(100, 2, 0)));
            CadDocument document = CadDocument.From(database, "two.dxf");

            CadBoundaryLoop loop = Assert.Single(FindWithGap(document, 5.0));
            Assert.Equal(10000.0, loop.Area, 6);
            Assert.Null(FindWithGap(document, 3.0));
        }

        [Fact]
        public void Gap_HatchWithGapTolerance_HatchesTheBoxWhoseSideStandsOffTheMiddleOfTheBottom()
        {
            CadDocument document = BoxWithARightSide(120, 100, 2);
            Start(document, "-HATCH", "A", "G", "5", Enter, P(50, 50), Enter);

            Assert.Equal(10000.0, OnlyHatch(document).Area, 6);
        }

        #endregion
    }
}
