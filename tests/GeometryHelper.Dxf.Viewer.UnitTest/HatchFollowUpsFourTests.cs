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
    /// HATCH follow-ups, round four: a boundary drawn by draW boundary and closed with two points only, and a gap tolerance that
    /// closes the loose ends of arcs. The numbers and prompts are those AutoCAD 2021's console gave in probes.
    /// </summary>
    public class HatchFollowUpsFourTests
    {
        private static CadCommandSession Start(CadDocument document, params object[] inputs)
        {
            CadCommandSession session = Session(document);
            session.Tolerance = Tolerance.Default;
            return Run(session, inputs);
        }

        private static DxfHatch OnlyHatch(CadDocument document) => Assert.Single(All<DxfHatch>(document.ModelSpace));

        #region Close with two points

        [Fact]
        public void DrawBoundary_ClosedWithTwoPoints_IsAHatchOfTheLineAndBack_OfNoArea()
        {
            // AutoCAD 2021: W, N, (0,0), (100,0), C makes a hatch of one External loop whose edges run (0,0)-(100,0) and back.
            CadDocument document = Blank();
            CadCommandSession session = Start(document, "-HATCH", "W", "N", P(0, 0), P(100, 0));
            Assert.Equal("Specify next point or [Arc/Close/Length/Undo]:", session.PromptText);

            Run(session, "C");
            Assert.Equal("Specify start point for new boundary or <Accept>:", session.PromptText);
            Assert.DoesNotContain("A boundary needs three corners at least.", session.History);
            Run(session, Enter, Enter);

            Assert.False(session.IsRunning);
            DxfHatch hatch = OnlyHatch(document);
            Assert.Equal(1, hatch.NumberOfLoops);
            Assert.Equal(DxfHatchLoopTypes.External, hatch.LoopTypeAt(0) & DxfHatchLoopTypes.External);
            DxfHatchLoop loop = hatch.GetLoopAt(0);
            Assert.True(loop.IsPolyline);
            // A loop so thin it closes no area is read as the chain, its first vertex again at its end.
            var polyline = Assert.IsType<GeoPolylineArc3>(loop.Polyline);
            Assert.Equal(3, polyline.Vertices.Count);
            Assert.Equal(new GeoPoint3(0, 0, 0), polyline.Vertices[0]);
            Assert.Equal(new GeoPoint3(100, 0, 0), polyline.Vertices[1]);
            Assert.Equal(new GeoPoint3(0, 0, 0), polyline.Vertices[2]);
            Assert.False(hatch.Associative);
            Assert.False(document.HatchSettings.Associative);
        }

        [Fact]
        public void DrawBoundary_ClosedWithTwoPoints_AndRetained_KeepsTheTwoPointPolylineClosed()
        {
            CadDocument document = Blank();
            Start(document, "-HATCH", "W", "Y", P(0, 0), P(100, 0), "C", Enter, Enter);

            DxfPolyline kept = Assert.Single(All<DxfPolyline>(document.ModelSpace));
            Assert.True(kept.Closed);
            Assert.Equal(2, kept.NumberOfVertices);
            Assert.Equal(1, OnlyHatch(document).NumberOfLoops);
        }

        [Fact]
        public void DrawBoundary_TwoPointsClosedInsideASquare_IsAnIslandOfNoArea_AndOutsideItAnotherOuterLoop()
        {
            // AutoCAD: a square drawn, then (10,10)-(30,10) closed, gives two loops, the second inside the first, and a second
            // closed line outside it (0,60)-(30,60) makes a loop of its own as outer as the square.
            CadDocument document = Blank();
            Start(
                document,
                "-HATCH",
                "W",
                "N",
                P(0, 0),
                P(50, 0),
                P(50, 50),
                P(0, 50),
                "C",
                P(10, 10),
                P(30, 10),
                "C",
                P(0, 60),
                P(30, 60),
                "C",
                Enter,
                Enter
            );

            DxfHatch hatch = OnlyHatch(document);
            Assert.Equal(3, hatch.NumberOfLoops);
            var depths = new List<(double, double)>();

            for (int i = 0; i < 3; i++)
            {
                bool outer = (hatch.LoopTypeAt(i) & DxfHatchLoopTypes.External) == DxfHatchLoopTypes.External;
                GeoPoint3 first = hatch.GetLoopAt(i).Polyline is GeoPolygonArc3 whole
                    ? whole.Vertices[0]
                    : ((GeoPolylineArc3)hatch.GetLoopAt(i).Polyline).Vertices[0];
                depths.Add((first.Y, outer ? 0 : 1));
            }

            Assert.Equal(new[] { (0.0, 0.0), (10.0, 1.0), (60.0, 0.0) }, depths.OrderBy(d => d.Item1).ToArray());
        }

        [Theory]
        [InlineData("SOLID", null)]
        [InlineData("ANSI31", "1")]
        public void ADegenerateHatch_IsDrawnWithoutFailing(string pattern, string scale)
        {
            CadDocument document = Blank();
            CadCommandSession session = Start(document, "-HATCH", "P", pattern);

            if (scale != null)
            {
                Run(session, scale, "0");
            }

            Run(session, "W", "N", P(0, 0), P(100, 0), "C", Enter, Enter);

            DisplayList2 list = document.GetDisplay(document.ModelSpace);

            Assert.NotNull(list);
            Assert.Single(All<DxfHatch>(document.ModelSpace));
        }

        [Fact]
        public void DrawBoundary_TwoPointsOfNoDistanceClosed_MakeNoHatch()
        {
            // AutoCAD: (0,0), (0,0), C says "Unable to fill the boundary with solid." and makes no hatch.
            CadDocument document = Blank();
            Start(document, "-HATCH", "W", "N", P(0, 0), P(0, 0), "C", Enter, Enter);

            Assert.Empty(All<DxfHatch>(document.ModelSpace));
        }

        [Theory]
        [InlineData(null, "Unable to hatch the boundary.")]
        [InlineData("SOLID", "Unable to fill the boundary with solid.")]
        [InlineData("ANSI31", "Unable to hatch the boundary.")]
        public void DrawBoundary_TwoPointsLeftOpen_MakeNoHatch_AndAutoCadSaysSoWhenTheHatchIsAskedFor(
            string pattern,
            string said
        )
        {
            // AutoCAD 2021 is silent as the shape ends and says it at the last Enter, in the words of the fill: "fill ... with solid" for
            // a solid or a gradient, "hatch" for a pattern.
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            session.Tolerance = Tolerance.Default;
            Run(session, "-HATCH");

            if (pattern != null)
            {
                Run(session, "P", pattern);

                if (pattern != "SOLID")
                {
                    Run(session, "1", "0");
                }
            }

            Run(session, "W", "N", P(0, 0), P(100, 0), Enter);
            Assert.DoesNotContain(said, session.History);
            Run(session, Enter, Enter);

            Assert.Empty(All<DxfHatch>(document.ModelSpace));
            Assert.False(session.IsRunning);
            Assert.Contains(said, session.History);
            Assert.DoesNotContain("A boundary needs three corners at least.", session.History);
        }

        [Fact]
        public void DrawBoundary_TwoPointsOfNoDistanceClosed_SayTheSame()
        {
            CadDocument document = Blank();
            CadCommandSession session = Start(
                document,
                "-HATCH",
                "P",
                "SOLID",
                "W",
                "N",
                P(0, 0),
                P(0, 0),
                "C",
                Enter,
                Enter
            );

            Assert.Contains("Unable to fill the boundary with solid.", session.History);
        }

        [Fact]
        public void DrawBoundary_OnePointAndEnter_GoesBackToTheMainPrompt_KeepingTheShapesDrawn()
        {
            // AutoCAD 2021 after a square closed and a start point (10, 10) answered with Enter asks no more start points: the main
            // prompt comes back, and Enter makes the hatch of the square.
            CadDocument document = Blank();
            CadCommandSession session = Start(
                document,
                "-HATCH",
                "W",
                "N",
                P(0, 0),
                P(50, 0),
                P(50, 50),
                "C",
                P(10, 10),
                Enter
            );

            Assert.StartsWith("Specify internal point or [", session.PromptText);
            Run(session, Enter);

            Assert.False(session.IsRunning);
            Assert.Equal(1250.0, OnlyHatch(document).Area, 6);
        }

        #endregion

        #region gap tolerance and the loose end of an arc

        private static void Line(DxfDatabase database, double x1, double y1, double x2, double y2) =>
            database.ModelSpace.AppendEntity(new DxfLine(new GeoPoint3(x1, y1, 0), new GeoPoint3(x2, y2, 0)));

        private static void Arc(DxfDatabase database, double x, double y, double radius, double from, double to) =>
            database.ModelSpace.AppendEntity(
                new DxfArc(new GeoPoint3(x, y, 0), radius, from * Math.PI / 180.0, to * Math.PI / 180.0)
            );

        private static IReadOnlyList<CadBoundaryLoop> Find(CadDocument document, double x, double y, double gap) =>
            CadBoundaryFinder.Find(document, document.ModelSpace, P(x, y), Tolerance.Default, null, null, gap);

        [Fact]
        public void Gap_AnArcGoesOnAlongItsCircleToTheLineAndNotAlongItsTangent()
        {
            // The arc of radius 50 about (50, 50) from 20 to 90 degrees ends at (96.9846, 67.1010), a line at y 64 below it. AutoCAD
            // 2021 at HPGAPTOL 5 closed the loop at (98, 64), where the circle meets the line (the tangent would have met it at
            // 98.113), as one arc of bulge 0.3333 from there to (50, 100), and hatched 3 072.7528 square units.
            var database = new DxfDatabase("AC1027");
            Line(database, 50, 100, 0, 100);
            Line(database, 0, 100, 0, 64);
            Line(database, 0, 64, 140, 64);
            Arc(database, 50, 50, 50, 20, 90);
            CadDocument document = CadDocument.From(database, "arc.dxf");

            Assert.Null(Find(document, 25, 80, 0.0));
            Assert.Null(Find(document, 25, 80, 1.0));
            CadBoundaryLoop loop = Assert.Single(Find(document, 25, 80, 5.0));

            Assert.Equal(3072.7528, loop.Area, 3);
            Assert.Contains(loop.Vertices, v => Math.Abs(v.X - 98.0) < 1E-6 && Math.Abs(v.Y - 64.0) < 1E-6);
            // The arc of the drawing and the piece it goes on by together turn through the 73.74 degrees of AutoCAD's single arc.
            Assert.Equal(4.0 * Math.Atan(1.0 / 3.0), loop.Bulges.Sum(b => 4.0 * Math.Atan(Math.Abs(b))), 3);
        }

        [Fact]
        public void Gap_AnArcEndingAtItsWidestPoint_GoesOnDownToTheLineBelow()
        {
            // The quarter circle of radius 50 about (50, 50) ends at (100, 50) going straight down; a line at y 47: AutoCAD closed
            // it at (99.9099, 47) and hatched 4 763.4054.
            var database = new DxfDatabase("AC1027");
            Line(database, 50, 100, 0, 100);
            Line(database, 0, 100, 0, 47);
            Line(database, 0, 47, 140, 47);
            Arc(database, 50, 50, 50, 0, 90);
            CadDocument document = CadDocument.From(database, "arc.dxf");

            CadBoundaryLoop loop = Assert.Single(Find(document, 25, 80, 5.0));

            Assert.Equal(4763.4054, loop.Area, 3);
            Assert.Contains(loop.Vertices, v => Math.Abs(v.X - 99.9099) < 1E-3 && Math.Abs(v.Y - 47.0) < 1E-6);
        }

        [Fact]
        public void Gap_TheLooseEndOfAnArcAtItsEndPoint_GoesOnAsTheOneAtItsStartPointDoes()
        {
            // The mirror image of the arc above, from 90 to 180 degrees: its loose end is its end point, going straight down.
            var database = new DxfDatabase("AC1027");
            Line(database, 50, 100, 100, 100);
            Line(database, 100, 100, 100, 47);
            Line(database, -40, 47, 100, 47);
            Arc(database, 50, 50, 50, 90, 180);
            CadDocument document = CadDocument.From(database, "arc.dxf");

            CadBoundaryLoop loop = Assert.Single(Find(document, 75, 80, 5.0));

            Assert.Equal(4763.4054, loop.Area, 3);
            Assert.Contains(loop.Vertices, v => Math.Abs(v.X - 0.0901) < 1E-3 && Math.Abs(v.Y - 47.0) < 1E-6);
        }

        [Fact]
        public void Gap_TheLooseEndOfAnArcGoingUp_GoesOnUpToTheLineAbove()
        {
            // The arc from 270 to 0 degrees ends at (100, 50) going up and back in; a line at y 53: closed at (99.9099, 53).
            var database = new DxfDatabase("AC1027");
            Line(database, 50, 0, 0, 0);
            Line(database, 0, 0, 0, 53);
            Line(database, 0, 53, 140, 53);
            Arc(database, 50, 50, 50, 270, 360);
            CadDocument document = CadDocument.From(database, "arc.dxf");

            CadBoundaryLoop loop = Assert.Single(Find(document, 25, 20, 5.0));

            Assert.Equal(4763.4054, loop.Area, 3);
            Assert.Contains(loop.Vertices, v => Math.Abs(v.X - 99.9099) < 1E-3 && Math.Abs(v.Y - 53.0) < 1E-6);
        }

        [Fact]
        public void Gap_TheWayOnOfAnArcAndOfALine_MeetAtTheCornerOfThem()
        {
            // The quarter circle ends at (100, 50) and a line leans in to (104, 45): the circle and the line's way on meet at
            // (99.9251, 47.2638), where AutoCAD closed it (7 407.87 square units).
            var database = new DxfDatabase("AC1027");
            Line(database, 50, 100, 0, 100);
            Line(database, 0, 100, 0, 25);
            Line(database, 0, 25, 140, 25);
            Line(database, 140, 25, 104, 45);
            Arc(database, 50, 50, 50, 0, 90);
            CadDocument document = CadDocument.From(database, "arc.dxf");

            CadBoundaryLoop loop = Assert.Single(Find(document, 25, 60, 5.0));

            Assert.Equal(7407.87, loop.Area, 2);
            Assert.Contains(loop.Vertices, v => Math.Abs(v.X - 99.9251) < 1E-3 && Math.Abs(v.Y - 47.2638) < 1E-3);
        }

        // The pixel of AutoCAD 2021's console at a view 140 high on its screen of 534 pixels: 140 / 534.
        private const double Pixel140 = 140.0 / 534.0;

        private static bool Closes(CadDocument document, double x, double y, double gap, double pixel) =>
            CadBoundaryFinder.Find(document, document.ModelSpace, P(x, y), Tolerance.Default, null, null, gap, pixel)
            != null;

        private static CadDocument LinesDocument(params double[][] lines)
        {
            var database = new DxfDatabase("AC1027");

            foreach (double[] l in lines)
            {
                Line(database, l[0], l[1], l[2], l[3]);
            }

            return CadDocument.From(database, "lines.dxf");
        }

        [Theory]
        [InlineData(3.0, 1.4, 1.35)]
        [InlineData(6.0, 2.9, 2.7)]
        [InlineData(20.0, 9.9, 9.85)]
        [InlineData(2.0, 0.9, 0.85)]
        public void Gap_AnEndStandingOffTheMiddleOfALine_ClosesWhereItsReachIsTwiceTheGapAndAPixel(
            double off,
            double closes,
            double doesNot
        )
        {
            // Probed in AutoCAD 2021 in a view 140 high: the right side of a box 100 high ends at (100, off) above a bottom that
            // runs on to x 120; the box is hatched at the gap in the second column and not at the third.
            CadDocument document = LinesDocument(
                new[] { 0.0, 0.0, 120.0, 0.0 },
                new[] { 100.0, off, 100.0, 100.0 },
                new[] { 100.0, 100.0, 0.0, 100.0 },
                new[] { 0.0, 100.0, 0.0, 0.0 }
            );

            Assert.True(Closes(document, 50, 50, closes, Pixel140));
            Assert.False(Closes(document, 50, 50, doesNot, Pixel140));
        }

        [Fact]
        public void Gap_ThePixelIsThatOfTheViewTheGapIsClosedIn()
        {
            // In a view 280 high a pixel is twice as wide: an end 20 off a bar closes at 9.75 and not at 9.7.
            CadDocument document = LinesDocument(
                new[] { 0.0, 0.0, 120.0, 0.0 },
                new[] { 100.0, 20.0, 100.0, 100.0 },
                new[] { 100.0, 100.0, 0.0, 100.0 },
                new[] { 0.0, 100.0, 0.0, 0.0 }
            );

            Assert.True(Closes(document, 50, 50, 9.75, 280.0 / 534.0));
            Assert.False(Closes(document, 50, 50, 9.7, 280.0 / 534.0));
        }

        [Theory]
        [InlineData(5.0, 5.0, 4.9, 4.8)]
        [InlineData(3.0, 3.0, 2.9, 2.8)]
        [InlineData(2.0, 8.0, 4.4, 3.5)]
        public void Gap_TwoWaysOnThatMeetAtACorner_CloseWhereTheShorterReachesAGapAndHalfAPixel(
            double across,
            double up,
            double closes,
            double doesNot
        )
        {
            // The bottom ends at (100, 0) and the right side at (100 + across, up): their ways on meet at (100 + across, 0), the
            // bottom going on across and the side down up. The shorter way no further than the gap and half a pixel, the longer no
            // further than twice the gap and a pixel: AutoCAD 2021 closed these at the gaps in the third column and not the fourth.
            CadDocument document = LinesDocument(
                new[] { 0.0, 0.0, 100.0, 0.0 },
                new[] { 100.0 + across, up, 100.0 + across, 100.0 },
                new[] { 100.0 + across, 100.0, 0.0, 100.0 },
                new[] { 0.0, 100.0, 0.0, 0.0 }
            );

            Assert.True(Closes(document, 50, 50, closes, Pixel140));
            Assert.False(Closes(document, 50, 50, doesNot, Pixel140));
        }

        [Fact]
        public void Gap_TwoLinesFacingEachOtherOnTheSameLine_AreJoinedAsTwoEnds_AGapAndHalfAPixelApart()
        {
            // The right side in two pieces with 4 between them: AutoCAD 2021 closed it at a gap of 3.9 and not at 3 (the longer reach
            // of a way on to a piece does not serve two ends facing each other).
            CadDocument document = LinesDocument(
                new[] { 0.0, 0.0, 100.0, 0.0 },
                new[] { 0.0, 100.0, 100.0, 100.0 },
                new[] { 0.0, 0.0, 0.0, 100.0 },
                new[] { 100.0, 100.0, 100.0, 52.0 },
                new[] { 100.0, 48.0, 100.0, 2.0 }
            );

            Assert.True(Closes(document, 50, 50, 3.9, Pixel140));
            Assert.False(Closes(document, 50, 50, 3.0, Pixel140));
        }

        [Fact]
        public void Gap_AnArcStandingOffALine_ClosesWhereTheReachAlongItsCircleIsTwiceTheGapAndAPixel()
        {
            // The arc of the first test of this group: AutoCAD 2021 closed it at a gap of 1.55 and not at 1.5.
            var database = new DxfDatabase("AC1027");
            Line(database, 50, 100, 0, 100);
            Line(database, 0, 100, 0, 64);
            Line(database, 0, 64, 140, 64);
            Arc(database, 50, 50, 50, 20, 90);
            CadDocument document = CadDocument.From(database, "arc.dxf");

            Assert.True(Closes(document, 25, 80, 1.55, Pixel140));
            Assert.False(Closes(document, 25, 80, 1.5, Pixel140));
        }

        [Fact]
        public void Gap_HatchWithGapTolerance_HatchesTheShapeWhoseArcStandsOffTheLine()
        {
            var database = new DxfDatabase("AC1027");
            Line(database, 50, 100, 0, 100);
            Line(database, 0, 100, 0, 64);
            Line(database, 0, 64, 140, 64);
            Arc(database, 50, 50, 50, 20, 90);
            CadDocument document = CadDocument.From(database, "arc.dxf");

            Start(document, "-HATCH", "A", "G", "5", Enter, P(25, 80), Enter);

            Assert.Equal(3072.7528, OnlyHatch(document).Area, 3);
        }

        #endregion
    }
}
