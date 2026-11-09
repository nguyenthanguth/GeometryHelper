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
    /// The modify commands of C11 built here: OFFSET, FILLET, CHAMFER, STRETCH, ARRAY (ARRAYRECT, ARRAYPOLAR, -ARRAY) and ALIGN,
    /// each driven on the command line with no window, its values measured, and one undo step giving the drawing back as it was.
    /// </summary>
    public class ModifyCommandTests
    {
        private static CadDocument Drawing(params string[] entities)
            => CadDocument.From(TestDrawings.Drawing(TestDrawings.Layer("Steel", 1), string.Empty, entities), "modify.dxf");

        private static CadCommandSession Start(CadDocument document, params object[] inputs)
        {
            CadCommandSession session = Session(document);
            session.Tolerance = Tolerance.Default;
            return Run(session, inputs);
        }

        private static List<T> Made<T>(CadDocument document, params string[] old)
            where T : DxfEntity
            => All<T>(document.ModelSpace).Where(e => !old.Contains(e.Handle)).ToList();

        private static void UndoesToStart(CadDocument document, string start, string step)
        {
            Assert.Equal(step, document.NextUndo.Name);
            Assert.True(document.Undo());
            Assert.Equal(start, document.Database.ToText());
        }

        private static double Height(GeoPoint3 from, GeoPoint3 to, GeoPoint3 middle)
        {
            double cx = to.X - from.X, cy = to.Y - from.Y;
            double chord = Math.Sqrt(cx * cx + cy * cy);
            return Math.Abs((middle.X - (from.X + to.X) / 2.0) * -cy + (middle.Y - (from.Y + to.Y) / 2.0) * cx) / chord;
        }

        [Fact]
        public void OffsetALineByTen_PutsACopyTenAlongTheNormalOnTheSidePicked_AsOneUndoStep()
        {
            CadDocument document = Drawing(TestDrawings.Line(0, 0, 100, 0, handle: "B1"));
            string start = document.Database.ToText();

            CadCommandSession session = Start(document, "O");
            Assert.Contains("Current settings: Erase source=No  Layer=Source  OFFSETGAPTYPE=0", session.History);
            Assert.Equal("Specify offset distance or [Through/Erase/Layer] <Through>:", session.PromptText);
            Run(session, "10");
            Assert.Equal("Select object to offset or [Exit/Undo] <Exit>:", session.PromptText);
            Run(session, P(50, 0));
            Assert.Equal("Specify point on side to offset or [Exit/Multiple/Undo] <Exit>:", session.PromptText);
            Run(session, P(50, -3));
            Run(session, P(50, 0), P(20, 40), Enter);

            List<DxfLine> made = Made<DxfLine>(document, "B1");
            Assert.Equal(2, made.Count);
            Near(new GeoPoint3(0, -10, 0), made[0].StartPoint);
            Near(new GeoPoint3(100, -10, 0), made[0].EndPoint);
            Near(new GeoPoint3(0, 10, 0), made[1].StartPoint);
            Near(new GeoPoint3(100, 10, 0), made[1].EndPoint);
            Assert.False(session.IsRunning);
            AssertReopens(document);
            UndoesToStart(document, start, "OFFSET");
        }

        [Fact]
        public void OffsetACircleInwardAndAnArcOutward_ChangeTheirRadiiByTheDistance()
        {
            CadDocument document = Drawing(TestDrawings.Circle(0, 0, 20, handle: "B1"), TestDrawings.Arc(100, 0, 20, 0, 90, handle: "B2"));

            Start(document, "OFFSET", "5", P(20, 0), P(3, 0), P(120, 0), P(130, 30), Enter);

            DxfCircle circle = Assert.Single(Made<DxfCircle>(document, "B1"));
            Assert.Equal(15.0, circle.Radius, 9);
            DxfArc arc = Assert.Single(Made<DxfArc>(document, "B2"));
            Assert.Equal(25.0, arc.Radius, 9);
            Near(new GeoPoint3(100, 0, 0), arc.Center);
        }

        [Fact]
        public void OffsetAClosedPolylineOutward_RunsTheCornersOnUntilTheyMeet_AndKeepsItsArcs()
        {
            CadDocument document = Drawing(
                TestDrawings.Polyline("0", "B1", true, 0, 0, 0, 100, 0, 0, 100, 50, 0, 0, 50, 0),
                TestDrawings.Polyline("0", "B2", false, 200, 0, 1, 250, 0, 0, 300, 0, 0));

            Start(document, "O", "5", P(50, 0), P(50, -2), P(280, 0), P(280, 3), Enter);

            List<DxfPolyline> made = Made<DxfPolyline>(document, "B1", "B2");
            Assert.Equal(2, made.Count);
            DxfPolyline grown = made[0];
            Assert.True(grown.Closed);
            Assert.Equal(4, grown.NumberOfVertices);
            var corners = Enumerable.Range(0, 4).Select(i => grown.GetPoint2dAt(i)).OrderBy(p => p.X).ThenBy(p => p.Y).ToList();
            Near(P(-5, -5), corners[0]);
            Near(P(-5, 55), corners[1]);
            Near(P(105, -5), corners[2]);
            Near(P(105, 55), corners[3]);

            // The half circle of radius 25 below (200, 0)-(250, 0) offset to its inside keeps its centre and takes 20, and runs on round
            // it until it meets the straight segment moved up 5.
            DxfPolyline slot = made[1];
            GeoArc2 first = slot.GetArcSegment2dAt(0);
            Assert.Equal(20.0, first.Radius, 9);
            Near(P(225, 0), first.Center);
            Near(P(225 + Math.Sqrt(375), 5), slot.GetPoint2dAt(1), 1E-9);
        }

        [Fact]
        public void OffsetThroughAPoint_AndMultiple_OffsetsEachNewCurveFromTheLast()
        {
            CadDocument document = Drawing(TestDrawings.Line(0, 0, 100, 0, handle: "B1"));

            CadCommandSession session = Start(document, "O", "T", P(50, 0));
            Assert.Equal("Specify through point or [Exit/Multiple/Undo] <Exit>:", session.PromptText);
            Run(session, P(10, 7), Enter);
            DxfLine through = Assert.Single(Made<DxfLine>(document, "B1"));
            Assert.Equal(7.0, through.StartPoint.Y, 9);

            session = Start(document, "O", "10", P(50, 0), "M");
            Assert.Equal("Specify point on side to offset or [Exit/Undo] <next object>:", session.PromptText);
            Run(session, P(50, -100), P(50, -100), P(50, -100), Enter, Enter);
            var levels = Made<DxfLine>(document, "B1", through.Handle).Select(l => l.StartPoint.Y).OrderBy(y => y).ToList();
            Assert.Equal(new[] { -30.0, -20.0, -10.0 }, levels.Select(y => Math.Round(y, 9)));
            Assert.Equal("OFFSET", document.NextUndo.Name);
        }

        [Fact]
        public void OffsetUndoInside_TakesTheLastOffsetBack_AndLeavesNoStepWhereNothingIsLeft()
        {
            CadDocument document = Drawing(TestDrawings.Line(0, 0, 100, 0, handle: "B1"));
            string start = document.Database.ToText();

            Start(document, "O", "10", P(50, 0), P(50, 5), "U", Enter);

            Assert.Equal(start, document.Database.ToText());
            Assert.False(document.CanUndo);
        }

        [Fact]
        public void OffsetWithEraseAndLayerCurrent_ErasesTheSourceAndPutsTheCopyOnTheCurrentLayer()
        {
            DxfDatabase database = TestDrawings.Drawing(TestDrawings.Layer("Steel", 1), string.Empty, TestDrawings.Line(0, 0, 100, 0, handle: "B1"));
            database.Clayer = "Steel";
            CadDocument document = CadDocument.From(database, "modify.dxf");
            string start = document.Database.ToText();

            CadCommandSession session = Start(document, "O", "E", "Y", "L", "C");
            Assert.Equal("Specify offset distance or [Through/Erase/Layer] <Through>:", session.PromptText);
            Run(session, "4", P(50, 0), P(50, 1), Enter);

            Assert.Null(document.FindEntity("B1"));
            DxfLine made = Assert.Single(All<DxfLine>(document.ModelSpace));
            Assert.Equal("Steel", made.Layer);
            Assert.Equal(4.0, made.StartPoint.Y, 9);
            UndoesToStart(document, start, "OFFSET");
        }

        [Fact]
        public void OffsetOfTextOrAnOwnedLine_RefusesTheTextAndCopiesTheLineAsThePresentersOwn()
        {
            CadDocument document = Drawing(TestDrawings.Text(0, 50, 5, "NOTE", handle: "B2"), TestDrawings.Line(0, 0, 100, 0, handle: "B1"));
            new CadOwnerTag("Test.Nobody", "L-1").WriteTo(document.FindEntity("B1"));

            CadCommandSession session = Start(document, "O", "5", new[] { "B2" });
            Assert.Contains("Cannot offset that object.", session.History);
            Run(session, P(50, 0), P(50, 1), Enter);

            DxfLine made = Assert.Single(Made<DxfLine>(document, "B1"));
            Assert.False(CadOwnerTag.TryRead(made, out _));
        }

        [Fact]
        public void FilletOfTwoCrossingLines_RoundsTheQuadrantPicked_TheArcTangentToBoth()
        {
            CadDocument document = Drawing(TestDrawings.Line(-50, 0, 100, 0, handle: "B1"), TestDrawings.Line(0, -50, 0, 100, handle: "B2"));
            string start = document.Database.ToText();

            CadCommandSession session = Start(document, "F");
            Assert.Contains("Current settings: Mode = TRIM, Radius = 10.0000", session.History);
            Assert.Equal("Select first object or [Undo/Polyline/Radius/Trim/Multiple]:", session.PromptText);
            Run(session, "R", "12", P(60, 0));
            Assert.Equal("Select second object or shift-select to apply corner or [Radius]:", session.PromptText);
            Run(session, P(0, 60));

            Assert.Equal(12.0, document.Database.Filletrad);
            DxfArc arc = Assert.Single(All<DxfArc>(document.ModelSpace));
            Assert.Equal(12.0, arc.Radius, 9);
            Near(new GeoPoint3(12, 12, 0), arc.Center);
            DxfLine one = Entity<DxfLine>(document, "B1");
            DxfLine two = Entity<DxfLine>(document, "B2");
            Near(new GeoPoint3(12, 0, 0), one.StartPoint);
            Near(new GeoPoint3(100, 0, 0), one.EndPoint);
            Near(new GeoPoint3(0, 12, 0), two.StartPoint);

            // Tangent: the arc's ends are the lines' new ends, each square to its line from the centre.
            Near(two.StartPoint, arc.StartPoint);
            Near(one.StartPoint, arc.EndPoint);
            Assert.Equal(Math.PI, arc.StartAngle, 9);
            Assert.Equal(1.5 * Math.PI, arc.EndAngle, 9);
            AssertReopens(document);
            UndoesToStart(document, start, "FILLET");
        }

        [Fact]
        public void FilletOfRadiusNought_RunsTwoLinesThatDoNotMeetToTheirCorner()
        {
            CadDocument document = Drawing(TestDrawings.Line(0, 0, 40, 0, handle: "B1"), TestDrawings.Line(50, 10, 50, 60, handle: "B2"));

            Start(document, "F", "R", "0", P(20, 0), P(50, 30));

            Near(new GeoPoint3(50, 0, 0), Entity<DxfLine>(document, "B1").EndPoint);
            Near(new GeoPoint3(50, 0, 0), Entity<DxfLine>(document, "B2").StartPoint);
            Assert.Empty(All<DxfArc>(document.ModelSpace));
        }

        [Fact]
        public void FilletOfParallelLines_JoinsThemByAHalfCircleAtTheEndPicked()
        {
            CadDocument document = Drawing(TestDrawings.Line(0, 0, 100, 0, handle: "B1"), TestDrawings.Line(0, 20, 80, 20, handle: "B2"));

            Start(document, "F", P(90, 0), P(40, 20));

            DxfArc arc = Assert.Single(All<DxfArc>(document.ModelSpace));
            Assert.Equal(10.0, arc.Radius, 9);
            Near(new GeoPoint3(100, 10, 0), arc.Center);
            Assert.Equal(1.5 * Math.PI, arc.StartAngle, 9);
            Assert.Equal(0.5 * Math.PI, arc.EndAngle, 9);
            Near(new GeoPoint3(100, 20, 0), Entity<DxfLine>(document, "B2").EndPoint);
        }

        [Fact]
        public void FilletOfALineAndAnArc_TrimsBothToTheirPointsOfTouching()
        {
            CadDocument document = Drawing(TestDrawings.Line(0, 0, 100, 0, handle: "B1"), TestDrawings.Arc(50, 30, 20, 180, 360, handle: "B2"));

            Start(document, "F", "R", "6", P(20, 0), P(50 + 20 * Math.Cos(Math.PI * 7 / 6), 30 + 20 * Math.Sin(Math.PI * 7 / 6)));

            DxfArc fillet = Assert.Single(Made<DxfArc>(document, "B2"));
            Near(new GeoPoint3(40, 6, 0), fillet.Center);
            Assert.Equal(6.0, fillet.Radius, 9);
            Assert.Equal(26.0, new GeoVector3(fillet.Center.X - 50, fillet.Center.Y - 30, 0).Length, 9);
            Near(new GeoPoint3(40, 0, 0), Entity<DxfLine>(document, "B1").EndPoint);
            DxfArc arc = Entity<DxfArc>(document, "B2");
            Assert.Equal(Math.PI, arc.StartAngle, 9);
            Near(new GeoPoint3(50 - 20 * 10.0 / 26.0, 30 - 20 * 24.0 / 26.0, 0), arc.EndPoint);
            Near(arc.EndPoint, fillet.EndPoint);
            Near(new GeoPoint3(40, 0, 0), fillet.StartPoint);
        }

        [Fact]
        public void FilletPolyline_RoundsEveryCornerWithRoom_AndCountsThoseTooShort()
        {
            CadDocument document = Drawing(TestDrawings.Polyline("0", "B1", true, 0, 0, 0, 100, 0, 0, 100, 50, 0, 0, 50, 0));
            string start = document.Database.ToText();

            CadCommandSession session = Start(document, "F", "P");
            Assert.Equal("Select 2D polyline or [Radius]:", session.PromptText);
            Run(session, P(50, 0));

            Assert.Contains("4 lines were filleted", session.History);
            var polyline = Entity<DxfPolyline>(document, "B1");
            Assert.Equal(8, polyline.NumberOfVertices);
            Near(P(0, 10), polyline.GetPoint2dAt(0));
            Assert.Equal(Math.Tan(Math.PI / 8), polyline.GetBulgeAt(0), 12);
            Near(P(10, 0), polyline.GetPoint2dAt(1));
            Assert.Equal(10.0, polyline.GetArcSegment2dAt(0).Radius, 9);
            UndoesToStart(document, start, "FILLET");

            session = Start(document, "F", "P", "R", "30", P(50, 0));
            Assert.Contains("2 lines were filleted", session.History);
            Assert.Contains("2 were too short", session.History);
        }

        [Fact]
        public void FilletOfTwoSegmentsOfAPolyline_PutsAnArcSegmentAtTheirVertex()
        {
            CadDocument document = Drawing(TestDrawings.Polyline("0", "B1", false, 0, 0, 0, 100, 0, 0, 100, 50, 0));

            Start(document, "F", P(50, 0), P(100, 25));

            var polyline = Entity<DxfPolyline>(document, "B1");
            Assert.Equal(4, polyline.NumberOfVertices);
            Near(P(90, 0), polyline.GetPoint2dAt(1));
            Near(P(100, 10), polyline.GetPoint2dAt(2));
            Assert.Equal(Math.Tan(Math.PI / 8), polyline.GetBulgeAt(1), 12);
        }

        [Fact]
        public void FilletMultipleWithUndo_KeepsTheFirstCorner_AndIsOneStep()
        {
            CadDocument document = Drawing(
                TestDrawings.Line(0, 0, 100, 0, handle: "B1"), TestDrawings.Line(0, 0, 0, 100, handle: "B2"),
                TestDrawings.Line(200, 0, 300, 0, handle: "B3"), TestDrawings.Line(200, 0, 200, 100, handle: "B4"));
            string start = document.Database.ToText();

            Start(document, "F", "M", P(50, 0), P(0, 50), P(250, 0), P(200, 50), "U", Enter);

            DxfArc arc = Assert.Single(All<DxfArc>(document.ModelSpace));
            Near(new GeoPoint3(10, 10, 0), arc.Center);
            Near(new GeoPoint3(200, 0, 0), Entity<DxfLine>(document, "B3").StartPoint);
            UndoesToStart(document, start, "FILLET");
        }

        [Fact]
        public void FilletNoTrim_LeavesTheLines_AndAnOwnedLineIsRefusedInWords()
        {
            CadDocument document = Drawing(TestDrawings.Line(0, 0, 100, 0, handle: "B1"), TestDrawings.Line(0, 0, 0, 100, handle: "B2"));

            Start(document, "F", "T", "N", P(50, 0), P(0, 50));
            Assert.Single(All<DxfArc>(document.ModelSpace));
            Near(new GeoPoint3(0, 0, 0), Entity<DxfLine>(document, "B1").StartPoint);

            new CadOwnerTag("Test.Nobody", "L-1").WriteTo(document.FindEntity("B1"));
            CadCommandSession session = Start(document, "F", "T", "T", P(50, 0));
            Assert.Contains("This line belongs to the model and cannot be filleted here.", session.History);
            Run(session, Esc);
            Assert.Single(All<DxfArc>(document.ModelSpace));
        }

        [Fact]
        public void ChamferByTwoDistances_CutsTenAlongTheFirstAndFiveAlongTheSecond()
        {
            CadDocument document = Drawing(TestDrawings.Line(-50, 0, 100, 0, handle: "B1"), TestDrawings.Line(0, -50, 0, 100, handle: "B2"));
            string start = document.Database.ToText();

            CadCommandSession session = Start(document, "CHA");
            Assert.Contains("(TRIM mode) Current chamfer Dist1 = 0.0000, Dist2 = 0.0000", session.History);
            Assert.Equal("Select first line or [Undo/Polyline/Distance/Angle/Trim/mEthod/Multiple]:", session.PromptText);
            Run(session, "D", "10");
            Assert.Equal("Specify second chamfer distance <10.0000>:", session.PromptText);
            Run(session, "5", P(60, 0));
            Assert.Equal("Select second line or shift-select to apply corner or [Distance/Angle/Method]:", session.PromptText);
            Run(session, P(0, 60));

            Assert.Equal(10.0, document.Database.Chamfera);
            Assert.Equal(5.0, document.Database.Chamferb);
            DxfLine cut = Assert.Single(Made<DxfLine>(document, "B1", "B2"));
            Near(new GeoPoint3(10, 0, 0), cut.StartPoint);
            Near(new GeoPoint3(0, 5, 0), cut.EndPoint);
            Near(new GeoPoint3(10, 0, 0), Entity<DxfLine>(document, "B1").StartPoint);
            Near(new GeoPoint3(0, 5, 0), Entity<DxfLine>(document, "B2").StartPoint);
            UndoesToStart(document, start, "CHAMFER");
        }

        [Fact]
        public void ChamferByALengthAndAnAngle_CutsAtThirtyDegreesFromTheFirstLine()
        {
            CadDocument document = Drawing(TestDrawings.Line(0, 0, 100, 0, handle: "B1"), TestDrawings.Line(0, 0, 0, 100, handle: "B2"));

            CadCommandSession session = Start(document, "CHA", "A", "10", "30", P(50, 0), P(0, 50));

            DxfLine cut = Assert.Single(Made<DxfLine>(document, "B1", "B2"));
            Near(new GeoPoint3(10, 0, 0), cut.StartPoint);
            Near(new GeoPoint3(0, 10 * Math.Tan(Math.PI / 6), 0), cut.EndPoint);
            Assert.Equal(Math.PI / 6, Math.Atan2(cut.EndPoint.Y - cut.StartPoint.Y, cut.StartPoint.X - cut.EndPoint.X), 12);

            Run(session, "CHA");
            Assert.Contains("(TRIM mode) Current chamfer Length = 10.0000, Angle = 30", session.History);
        }

        [Fact]
        public void ChamferPolyline_CutsEveryCorner()
        {
            CadDocument document = Drawing(TestDrawings.Polyline("0", "B1", true, 0, 0, 0, 100, 0, 0, 100, 50, 0, 0, 50, 0));

            CadCommandSession session = Start(document, "CHA", "D", "5", "5", "P", P(50, 0));

            Assert.Contains("4 lines were chamfered", session.History);
            var polyline = Entity<DxfPolyline>(document, "B1");
            Assert.Equal(8, polyline.NumberOfVertices);
            Near(P(0, 5), polyline.GetPoint2dAt(0));
            Near(P(5, 0), polyline.GetPoint2dAt(1));
            Assert.Equal(0.0, polyline.GetBulgeAt(0));
        }

        [Fact]
        public void StretchByACrossingWindow_MovesTheEndsInsideAndWholeWhatLiesInside()
        {
            CadDocument document = Drawing(
                TestDrawings.Line(0, 0, 100, 0, handle: "B1"),
                TestDrawings.Polyline("0", "B2", false, 0, 10, 0, 100, 10, 0, 100, 50, 0),
                TestDrawings.Circle(100, 80, 5, handle: "B3"),
                TestDrawings.Line(95, 30, 105, 30, handle: "B4"),
                TestDrawings.Line(0, 120, 50, 120, handle: "B5"));
            string start = document.Database.ToText();

            CadCommandSession session = Start(document, "S");
            Assert.Contains("Select objects to stretch by crossing-window or crossing-polygon...", session.History);
            Assert.Equal("Select objects:", session.PromptText);
            Run(session, P(110, -10));
            Assert.Equal("Specify opposite corner:", session.PromptText);
            Run(session, P(90, 90));
            Assert.Contains("4 found", session.History);
            Run(session, Enter);
            Assert.Equal("Specify base point or [Displacement] <Displacement>:", session.PromptText);
            Run(session, P(0, 0), P(20, 0));

            Near(new GeoPoint3(0, 0, 0), Entity<DxfLine>(document, "B1").StartPoint);
            Near(new GeoPoint3(120, 0, 0), Entity<DxfLine>(document, "B1").EndPoint);
            var polyline = Entity<DxfPolyline>(document, "B2");
            Near(P(0, 10), polyline.GetPoint2dAt(0));
            Near(P(120, 10), polyline.GetPoint2dAt(1));
            Near(P(120, 50), polyline.GetPoint2dAt(2));
            Near(new GeoPoint3(120, 80, 0), Entity<DxfCircle>(document, "B3").Center);
            Near(new GeoPoint3(115, 30, 0), Entity<DxfLine>(document, "B4").StartPoint);
            Near(new GeoPoint3(50, 120, 0), Entity<DxfLine>(document, "B5").EndPoint);
            UndoesToStart(document, start, "STRETCH");
        }

        [Fact]
        public void StretchOfAnArcByOneEnd_KeepsTheHeightOfItsMiddleOffItsChord()
        {
            CadDocument document = Drawing(TestDrawings.Arc(0, 0, 10, 0, 90, handle: "B1"));
            var arc = Entity<DxfArc>(document, "B1");
            double before = Height(arc.StartPoint, arc.EndPoint, arc.GetPointAtParameter((arc.StartParam + arc.EndParam) / 2.0));

            Start(document, "S", "C", P(5, -5), P(15, 5), Enter, P(0, 0), P(10, 0));

            arc = Entity<DxfArc>(document, "B1");
            Near(new GeoPoint3(20, 0, 0), arc.StartPoint, 1E-9);
            Near(new GeoPoint3(0, 10, 0), arc.EndPoint, 1E-9);
            Assert.Equal(before, Height(arc.StartPoint, arc.EndPoint, arc.GetPointAtParameter((arc.StartParam + arc.EndParam) / 2.0)), 9);
            Assert.Equal(10 - 10 * Math.Sqrt(0.5), before, 12);
        }

        [Fact]
        public void StretchByACrossingPolygon_MovesTheVerticesInsideIt_AndRefusesAnOwnedLine()
        {
            CadDocument document = Drawing(TestDrawings.Line(0, 0, 100, 0, handle: "B1"), TestDrawings.Line(0, 10, 100, 10, handle: "B2"));
            new CadOwnerTag("Test.Nobody", "L-1").WriteTo(document.FindEntity("B2"));

            CadCommandSession session = Start(document, "S", "CP", P(90, -5), P(110, -5), P(110, 20), P(90, 20), Enter, Enter, "D", "0,15");

            Near(new GeoPoint3(100, 15, 0), Entity<DxfLine>(document, "B1").EndPoint);
            Near(new GeoPoint3(100, 10, 0), Entity<DxfLine>(document, "B2").EndPoint);
            Assert.Contains("This line belongs to the model and cannot be stretched here.", session.History);
        }

        [Fact]
        public void AlignByTwoPairs_TurnsTheObjects_AndScalesThemWhenAsked()
        {
            CadDocument document = Drawing(TestDrawings.Line(0, 0, 10, 0, handle: "B1"));
            string start = document.Database.ToText();

            CadCommandSession session = Start(document, "AL", new[] { "B1" }, Enter);
            Assert.Equal("Specify first source point:", session.PromptText);
            Run(session, P(0, 0), P(100, 100), P(10, 0), P(100, 120));
            Assert.Equal("Specify third source point or <continue>:", session.PromptText);
            Run(session, Enter);
            Assert.Equal("Scale objects based on alignment points? [Yes/No] <N>:", session.PromptText);
            Run(session, "Y");

            Near(new GeoPoint3(100, 100, 0), Entity<DxfLine>(document, "B1").StartPoint);
            Near(new GeoPoint3(100, 120, 0), Entity<DxfLine>(document, "B1").EndPoint);
            UndoesToStart(document, start, "ALIGN");

            Start(document, "AL", new[] { "B1" }, Enter, P(0, 0), P(100, 100), P(10, 0), P(100, 120), Enter, Enter);
            Near(new GeoPoint3(100, 110, 0), Entity<DxfLine>(document, "B1").EndPoint);
        }

        [Fact]
        public void AlignByOneAndByThreePairs_MovesOrTurnsTheObjectsOver()
        {
            CadDocument document = Drawing(TestDrawings.Line(0, 0, 10, 0, handle: "B1"), TestDrawings.Line(5, 0, 5, 5, handle: "B2"));

            Start(document, "AL", new[] { "B1" }, Enter, P(0, 0), P(3, 4), Enter);
            Near(new GeoPoint3(13, 4, 0), Entity<DxfLine>(document, "B1").EndPoint);

            Start(document, "AL", new[] { "B2" }, Enter, P(0, 0), P(0, 0), P(10, 0), P(10, 0), P(5, 5), P(5, -5));
            Near(new GeoPoint3(5, 0, 0), Entity<DxfLine>(document, "B2").StartPoint);
            Near(new GeoPoint3(5, -5, 0), Entity<DxfLine>(document, "B2").EndPoint);
        }

        [Fact]
        public void ArrayRect_MakesSeparateCopiesInColumnsAndRows_OneUndoStep()
        {
            CadDocument document = Drawing(TestDrawings.Line(0, 0, 10, 0, handle: "B1"));
            string start = document.Database.ToText();

            CadCommandSession session = Start(document, "ARRAYRECT", new[] { "B1" }, Enter);
            Assert.Contains("Type = Rectangular  Associative = No", session.History);
            Assert.Equal("Select grip to edit array or [ASsociative/Base point/COUnt/Spacing/COLumns/Rows/Levels/eXit] <eXit>:", session.PromptText);
            Run(session, "AS", "Y");
            Assert.Contains("Associative arrays are not built: the items are made as separate objects (ARRAYASSOCIATIVITY 0).", session.History);
            Run(session, "COU", "3", "2", "S", "20", "15", "X");

            List<DxfLine> lines = All<DxfLine>(document.ModelSpace);
            Assert.Equal(6, lines.Count);
            var starts = lines.Select(l => (Math.Round(l.StartPoint.X, 9), Math.Round(l.StartPoint.Y, 9))).OrderBy(p => p).ToList();
            Assert.Equal(new[] { (0.0, 0.0), (0.0, 15.0), (20.0, 0.0), (20.0, 15.0), (40.0, 0.0), (40.0, 15.0) }, starts);
            UndoesToStart(document, start, "ARRAYRECT");
        }

        [Fact]
        public void ArrayPolar_TurnsSixItemsRoundTheCentre_AndAFillAngleSharesItAmongTheItems()
        {
            CadDocument document = Drawing(TestDrawings.Circle(10, 0, 1, handle: "B1"));

            CadCommandSession session = Start(document, "ARRAYPOLAR", new[] { "B1" }, Enter);
            Assert.Equal("Specify center point of array or [Base point/Axis of rotation]:", session.PromptText);
            Run(session, P(0, 0), Enter);
            List<DxfCircle> circles = All<DxfCircle>(document.ModelSpace);
            Assert.Equal(6, circles.Count);
            Near(new GeoPoint3(10 * Math.Cos(Math.PI / 3), 10 * Math.Sin(Math.PI / 3), 0), circles[1].Center);

            document.Undo();
            Start(document, "ARRAYPOLAR", new[] { "B1" }, Enter, P(0, 0), "I", "4", "F", "90", "X");
            circles = All<DxfCircle>(document.ModelSpace);
            Assert.Equal(4, circles.Count);
            Near(new GeoPoint3(0, 10, 0), circles[3].Center);
        }

        [Fact]
        public void ArrayTyped_RectangularAndPolar_AsAutoCadsDashArray()
        {
            CadDocument document = Drawing(TestDrawings.Line(0, 0, 5, 0, handle: "B1"));

            CadCommandSession session = Start(document, "-AR", new[] { "B1" }, Enter);
            Assert.Equal("Enter the type of array [Rectangular/Polar] <R>:", session.PromptText);
            Run(session, Enter, "2", "3");
            Assert.Equal("Enter the distance between rows or specify unit cell (---):", session.PromptText);
            Run(session, "10");
            Assert.Equal("Specify the distance between columns (|||):", session.PromptText);
            Run(session, "20");
            Assert.Equal(6, All<DxfLine>(document.ModelSpace).Count);
            Assert.Contains(All<DxfLine>(document.ModelSpace), l => Math.Abs(l.StartPoint.X - 40) < 1E-9 && Math.Abs(l.StartPoint.Y - 10) < 1E-9);

            document.Undo();
            Start(document, "-ARRAY", new[] { "B1" }, Enter, "P", P(0, 0), "4", Enter, "N");
            List<DxfLine> lines = All<DxfLine>(document.ModelSpace);
            Assert.Equal(4, lines.Count);
            Assert.All(lines, l => Assert.Equal(5.0, l.EndPoint.X - l.StartPoint.X, 9));
        }

        [Fact]
        public void ArrayAskedForAPath_SaysItIsNotBuilt()
        {
            CadDocument document = Drawing(TestDrawings.Line(0, 0, 5, 0, handle: "B1"));

            CadCommandSession session = Start(document, "AR", new[] { "B1" }, Enter);
            Assert.Equal("Enter array type [Rectangular/PAth/POlar] <Rectangular>:", session.PromptText);
            Run(session, "PA");

            Assert.Contains("Path arrays are not built yet: use Rectangular or POlar.", session.History);
            Assert.False(document.CanUndo);
        }
    }
}
