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
    /// TRIM, EXTEND, BREAK, BREAKATPOINT, JOIN and LENGTHEN, run from the command line with no window: AutoCAD 2021's prompts,
    /// the shapes they leave measured, each run one undo step that gives the drawing back as its text was.
    /// </summary>
    public class EditingModifyTests
    {
        private static readonly GeoVector3 Up = new GeoVector3(0, 0, 1);

        private static CadDocument Drawing(params DxfEntity[] entities)
        {
            var database = new DxfDatabase("AC1027");

            foreach (DxfEntity entity in entities)
            {
                database.ModelSpace.AppendEntity(entity);
            }

            return CadDocument.From(database, "modify.dxf");
        }

        private static DxfLine Line(double x1, double y1, double x2, double y2) =>
            new DxfLine(new GeoPoint3(x1, y1, 0), new GeoPoint3(x2, y2, 0));

        private static CadCommandSession Start(CadDocument document, params object[] inputs)
        {
            CadCommandSession session = Session(document);
            session.Tolerance = Tolerance.Default;
            return Run(session, inputs);
        }

        private static void NearLine(DxfLine line, double x1, double y1, double x2, double y2)
        {
            Near(new GeoPoint3(x1, y1, 0), line.StartPoint);
            Near(new GeoPoint3(x2, y2, 0), line.EndPoint);
        }

        /// <summary>Undoes the one step the command made and checks the drawing is the text it was.</summary>
        private static void UndoesToTheBytes(CadDocument document, string before, string step)
        {
            Assert.Equal(step, document.NextUndo.Name);
            Assert.True(document.Undo());
            Assert.Equal(before, document.Database.ToText());
            Assert.False(document.CanUndo);
        }

        [Fact]
        public void TrimQuick_ALineCrossedByTwo_LosesThePieceBetweenThem_AndKeepsItsHandle()
        {
            DxfLine h = Line(0, 0, 100, 0);
            CadDocument document = Drawing(h, Line(30, -10, 30, 10), Line(70, -10, 70, 10));
            string before = document.Database.ToText();
            string handle = h.Handle;

            CadCommandSession session = Start(document, "TR");
            Assert.Contains("Current settings: Projection=UCS, Edge=None, Mode=Quick", session.History);
            Assert.Equal(
                "Select object to trim or shift-select to extend or [cuTting edges/Fence/Crossing/mOde/Project/eRase]:",
                session.PromptText
            );
            Run(session, P(50, 0));
            Assert.Equal(
                "Select object to trim or shift-select to extend or [cuTting edges/Fence/Crossing/mOde/Project/eRase/Undo]:",
                session.PromptText
            );
            Run(session, Enter);

            NearLine(Entity<DxfLine>(document, handle), 0, 0, 30, 0);
            DxfLine rest = All<DxfLine>(document.ModelSpace).Last();
            NearLine(rest, 70, 0, 100, 0);
            Assert.Equal(4, document.EntityCount);
            AssertReopens(document);
            UndoesToTheBytes(document, before, "TRIM");
        }

        [Fact]
        public void TrimPickedPastTheLastCut_TakesTheEndOff_AndAPickNearTheStartTheStart()
        {
            DxfLine h = Line(0, 0, 100, 0);
            CadDocument document = Drawing(h, Line(30, -10, 30, 10));

            Start(document, "TRIM", P(80, 0), Enter);
            NearLine(Entity<DxfLine>(document, h.Handle), 0, 0, 30, 0);

            DxfLine g = Line(0, 0, 100, 0);
            document = Drawing(g, Line(30, -10, 30, 10));
            Start(document, "TRIM", P(10, 0), Enter);
            NearLine(Entity<DxfLine>(document, g.Handle), 30, 0, 100, 0);
        }

        [Fact]
        public void TrimQuick_AnObjectNothingCuts_IsErased_AsAutoCad2021Does()
        {
            DxfLine alone = Line(0, 0, 100, 0);
            CadDocument document = Drawing(alone, Line(0, 50, 100, 50));

            Start(document, "TR", P(50, 0), Enter);
            Assert.Null(document.FindEntity(alone.Handle));
            Assert.Equal(1, document.EntityCount);
        }

        [Fact]
        public void TrimACircle_LeavesTheArcAwayFromThePick()
        {
            var circle = new DxfCircle(new GeoPoint3(0, 0, 0), Up, 10);
            CadDocument document = Drawing(circle, Line(-20, 0, 20, 0));
            string before = document.Database.ToText();

            Start(document, "TR", P(0, 10), Enter);
            Assert.Null(document.FindEntity(circle.Handle));
            DxfArc arc = Assert.Single(All<DxfArc>(document.ModelSpace));
            Near(new GeoPoint3(0, 0, 0), arc.Center);
            Assert.Equal(10.0, arc.Radius, 9);
            Assert.Equal(Math.PI, arc.StartAngle, 9);
            Assert.Equal(Math.PI, arc.TotalAngle, 9);
            Near(new GeoPoint3(0, -10, 0), arc.GetPointAtParameter(1.5 * Math.PI));
            UndoesToTheBytes(document, before, "TRIM");
        }

        [Fact]
        public void TrimAPolyline_KeepsItsVerticesAndTheBulgeOfThePartOfItsArc()
        {
            var polyline = new DxfPolyline();
            polyline.AddVertexAt(0, P(0, 0), 0, 0, 0);
            polyline.AddVertexAt(1, P(100, 0), 1, 0, 0);
            polyline.AddVertexAt(2, P(100, 100), 0, 0, 0);
            CadDocument document = Drawing(polyline, Line(90, 50, 200, 50));
            string before = document.Database.ToText();

            // The semicircle about (100, 50) is cut at (150, 50); its upper quarter is picked.
            Start(document, "TR", P(100 + 50 * Math.Sqrt(0.5), 50 + 50 * Math.Sqrt(0.5)), Enter);

            DxfPolyline kept = Assert.Single(All<DxfPolyline>(document.ModelSpace));
            Assert.Equal(3, kept.NumberOfVertices);
            Near(P(0, 0), kept.GetPoint2dAt(0));
            Near(P(100, 0), kept.GetPoint2dAt(1));
            Near(P(150, 50), kept.GetPoint2dAt(2), 1E-6);
            Assert.Equal(0.0, kept.GetBulgeAt(0), 12);
            Assert.Equal(Math.Tan(Math.PI / 8), kept.GetBulgeAt(1), 9);
            AssertReopens(document);
            UndoesToTheBytes(document, before, "TRIM");
        }

        [Fact]
        public void TrimAClosedPolyline_OpensItBetweenTheTwoCutsRoundThePick()
        {
            var rectangle = new DxfPolyline();
            rectangle.AddVertexAt(0, P(0, 0), 0, 0, 0);
            rectangle.AddVertexAt(1, P(100, 0), 0, 0, 0);
            rectangle.AddVertexAt(2, P(100, 50), 0, 0, 0);
            rectangle.AddVertexAt(3, P(0, 50), 0, 0, 0);
            rectangle.Closed = true;
            CadDocument document = Drawing(rectangle, Line(30, -10, 30, 60));

            Start(document, "TR", P(0, 25), Enter);
            DxfPolyline kept = Assert.Single(All<DxfPolyline>(document.ModelSpace));
            Assert.False(kept.Closed);
            Assert.Equal(190.0, kept.Length, 9);
            Near(P(100, 0), kept.GetPoint2dAt(1));
            Near(P(100, 50), kept.GetPoint2dAt(2));
            Assert.Equal(30.0, kept.GetPoint2dAt(0).X, 9);
            Assert.Equal(30.0, kept.GetPoint2dAt(kept.NumberOfVertices - 1).X, 9);
        }

        [Fact]
        public void TrimStandard_CutsAtTheEdgesSelectedOnly_AndEdgeExtendRunsAnEdgeOn()
        {
            DxfLine h = Line(0, 0, 100, 0);
            DxfLine near = Line(30, -10, 30, 10);
            DxfLine far = Line(70, -10, 70, 10);
            DxfLine shortEdge = Line(50, 5, 50, 10);
            CadDocument document = Drawing(h, near, far, shortEdge);

            CadCommandSession session = Start(document, "TR", "O");
            Assert.Equal("Enter a trim mode option [Quick/Standard] <Quick>:", session.PromptText);
            Run(session, "S");
            Assert.Contains("Select cutting edges ...", session.History);
            Assert.Equal("Select objects or <select all>:", session.PromptText);
            Run(session, new[] { near.Handle }, Enter);
            Assert.Equal(
                "Select object to trim or shift-select to extend or [Fence/Crossing/mOde/Project/Edge/eRase]:",
                session.PromptText
            );
            Run(session, P(90, 0), Enter);
            NearLine(Entity<DxfLine>(document, h.Handle), 0, 0, 30, 0);

            // The mode is kept for the next run, and with Edge=Extend the short edge cuts where its line would.
            session = Run(session, "TRIM");
            Assert.Contains("Current settings: Projection=UCS, Edge=None, Mode=Standard", session.History);
            Run(session, new[] { shortEdge.Handle }, Enter, "E");
            Assert.Equal("Enter an implied edge extension mode [Extend/No extend] <No extend>:", session.PromptText);
            Run(session, "E", P(10, 0));
            NearLine(Entity<DxfLine>(document, h.Handle), 0, 0, 30, 0);
            Assert.Contains("Object does not intersect an edge.", session.History);
            Run(session, Enter);
        }

        [Fact]
        public void TrimWithEdgeExtend_CutsWhereTheEdgeWouldRunTo()
        {
            DxfLine h = Line(0, 0, 100, 0);
            DxfLine edge = Line(50, 5, 50, 10);
            CadDocument document = Drawing(h, edge);

            Start(document, "TR", "O", "S", new[] { edge.Handle }, Enter, "E", "E", P(80, 0), Enter);
            NearLine(Entity<DxfLine>(document, h.Handle), 0, 0, 50, 0);
        }

        [Fact]
        public void ExtendQuick_RunsTheNearerEndToTheFirstEdge_AndShiftInTrimDoesTheSame()
        {
            DxfLine a = Line(0, 0, 40, 0);
            CadDocument document = Drawing(a, Line(100, -10, 100, 10), Line(150, -10, 150, 10));
            string before = document.Database.ToText();

            CadCommandSession session = Start(document, "EX");
            Assert.Equal(
                "Select object to extend or shift-select to trim or [Boundary edges/Fence/Crossing/mOde/Project/Edge]:",
                session.PromptText
            );
            Run(session, P(35, 0), Enter);
            NearLine(Entity<DxfLine>(document, a.Handle), 0, 0, 100, 0);
            UndoesToTheBytes(document, before, "EXTEND");

            session = Start(document, "TR");
            session.Feed(P(35, 0), true);
            Run(session, Enter);
            NearLine(Entity<DxfLine>(document, a.Handle), 0, 0, 100, 0);

            // Shift in EXTEND trims: the end past the wall it crosses is cut off.
            DxfLine b = Line(0, 20, 200, 20);
            document = Drawing(b, Line(100, 10, 100, 30));
            session = Start(document, "EX");
            session.Feed(P(180, 20), true);
            Run(session, Enter);
            NearLine(Entity<DxfLine>(document, b.Handle), 0, 20, 100, 20);
        }

        [Fact]
        public void ExtendAnArc_RunsRoundItsCircleToTheEdge()
        {
            var arc = new DxfArc(new GeoPoint3(0, 0, 0), 10, 0, Math.PI / 2);
            CadDocument document = Drawing(arc, Line(-20, 0, -5, 0));

            Start(document, "EXTEND", P(1, 10), Enter);
            DxfArc run = Entity<DxfArc>(document, arc.Handle);
            Assert.Equal(0.0, run.StartAngle, 9);
            Assert.Equal(Math.PI, run.EndAngle, 9);
        }

        [Fact]
        public void ExtendAPolylineEnd_ItsLastLineRunsOn()
        {
            var polyline = new DxfPolyline();
            polyline.AddVertexAt(0, P(0, 0), 0, 0, 0);
            polyline.AddVertexAt(1, P(10, 0), 0, 0, 0);
            polyline.AddVertexAt(2, P(10, 10), 0, 0, 0);
            CadDocument document = Drawing(polyline, Line(-10, 30, 30, 30));

            Start(document, "EX", P(10, 9), Enter);
            DxfPolyline run = Entity<DxfPolyline>(document, polyline.Handle);
            Assert.Equal(3, run.NumberOfVertices);
            Near(P(10, 30), run.GetPoint2dAt(2));
        }

        [Fact]
        public void TrimFence_CutsEachObjectWhereTheFenceCrossesIt()
        {
            DxfLine v1 = Line(10, 0, 10, 10);
            DxfLine v2 = Line(20, 0, 20, 10);
            DxfLine v3 = Line(30, 0, 30, 10);
            CadDocument document = Drawing(v1, v2, v3, Line(0, 5, 40, 5));

            CadCommandSession session = Start(document, "TR", "F");
            Assert.Equal("Specify first fence point:", session.PromptText);
            Run(session, P(15, 8));
            Assert.Equal("Specify next fence point or [Undo]:", session.PromptText);
            Run(session, P(35, 8), Enter, Enter);

            NearLine(Entity<DxfLine>(document, v1.Handle), 10, 0, 10, 10);
            NearLine(Entity<DxfLine>(document, v2.Handle), 20, 0, 20, 5);
            NearLine(Entity<DxfLine>(document, v3.Handle), 30, 0, 30, 5);
            Assert.Equal("TRIM", document.NextUndo.Name);
        }

        [Fact]
        public void TrimCrossing_CutsWhatTheBoxTakes()
        {
            DxfLine v1 = Line(10, 0, 10, 10);
            DxfLine v2 = Line(20, 0, 20, 10);
            CadDocument document = Drawing(v1, v2, Line(0, 5, 40, 5));

            Start(document, "TR", "C", P(15, 6), P(25, 9), Enter);
            NearLine(Entity<DxfLine>(document, v1.Handle), 10, 0, 10, 10);
            NearLine(Entity<DxfLine>(document, v2.Handle), 20, 0, 20, 5);
        }

        [Fact]
        public void UndoInsideTrim_TakesTheLastPickBack_AndTheRunStaysOneStep()
        {
            DxfLine h = Line(0, 0, 100, 0);
            DxfLine k = Line(0, 20, 100, 20);
            CadDocument document = Drawing(h, k, Line(50, -10, 50, 30));
            string before = document.Database.ToText();

            CadCommandSession session = Start(document, "TR", P(80, 0), P(80, 20));
            NearLine(Entity<DxfLine>(document, k.Handle), 0, 20, 50, 20);
            Run(session, "U");
            NearLine(Entity<DxfLine>(document, k.Handle), 0, 20, 100, 20);
            NearLine(Entity<DxfLine>(document, h.Handle), 0, 0, 50, 0);
            Run(session, "U");
            NearLine(Entity<DxfLine>(document, h.Handle), 0, 0, 100, 0);
            Assert.Equal(
                "Select object to trim or shift-select to extend or [cuTting edges/Fence/Crossing/mOde/Project/eRase]:",
                session.PromptText
            );
            Run(session, Enter);
            Assert.False(document.CanUndo);
            Assert.Equal(before, document.Database.ToText());

            Start(document, "TR", P(80, 0), "R", new[] { k.Handle }, Enter, Enter);
            Assert.Null(document.FindEntity(k.Handle));
            UndoesToTheBytes(document, before, "TRIM");
        }

        [Fact]
        public void TrimAnXline_LeavesRays()
        {
            var xline = new DxfXline { BasePoint = new GeoPoint3(0, 0, 0), UnitDir = new GeoVector3(1, 0, 0) };
            CadDocument document = Drawing(xline, Line(30, -10, 30, 10), Line(70, -10, 70, 10));

            // The xline is reached by a fence between the two lines, which crosses it at (50, 0) only.
            Start(document, "TR", "F", P(50, -5), P(50, 5), Enter, Enter);
            Assert.Null(document.FindEntity(xline.Handle));
            List<DxfRay> rays = All<DxfRay>(document.ModelSpace);
            Assert.Equal(2, rays.Count);
            Near(new GeoPoint3(30, 0, 0), rays[0].BasePoint);
            Assert.Equal(-1.0, rays[0].UnitDir.X, 12);
            Near(new GeoPoint3(70, 0, 0), rays[1].BasePoint);
            Assert.Equal(1.0, rays[1].UnitDir.X, 12);
        }

        [Fact]
        public void TrimAWholeEllipse_AndASpline_LeaveTheirPartsAwayFromThePick()
        {
            var ellipse = new DxfEllipse(new GeoPoint3(0, 0, 0), Up, new GeoVector3(20, 0, 0), 0.5, 0, 2 * Math.PI);
            CadDocument document = Drawing(ellipse, Line(-30, 0, 30, 0));

            Start(document, "TR", P(0, 10), Enter);
            DxfEllipse part = Assert.Single(All<DxfEllipse>(document.ModelSpace));
            Assert.False(part.Closed);
            Assert.Equal(Math.PI, part.StartParam, 9);
            Assert.Equal(2 * Math.PI, part.EndParam, 9);

            var spline = new DxfSpline(
                2,
                false,
                false,
                false,
                new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(50, 100, 0), new GeoPoint3(100, 0, 0) },
                new[] { 0.0, 0, 0, 1, 1, 1 },
                null,
                0,
                0
            );
            document = Drawing(spline, Line(-10, 25, 110, 25));
            Start(document, "TR", P(50, 50), Enter);
            List<DxfSpline> pieces = All<DxfSpline>(document.ModelSpace);
            Assert.Equal(2, pieces.Count);
            Near(new GeoPoint3(0, 0, 0), pieces[0].StartPoint, 1E-9);
            Assert.Equal(25.0, pieces[0].EndPoint.Y, 6);
            Assert.Equal(25.0, pieces[1].StartPoint.Y, 6);
            Near(new GeoPoint3(100, 0, 0), pieces[1].EndPoint, 1E-9);
        }

        [Fact]
        public void TrimRefusesALockedObjectAndOneOfTheModel_InWords()
        {
            DxfLine owned = Line(0, 0, 100, 0);
            DxfLine locked = Line(0, 20, 100, 20);
            CadDocument document = Drawing(owned, locked, Line(50, -10, 50, 30));
            new CadOwnerTag("Some.Owner", "VIEW-1").WriteTo(document.FindEntity(owned.Handle));
            document.Database.LayerTable.Add(new DxfLayerTableRecord { Name = "Locked", IsLocked = true });
            document.FindEntity(locked.Handle).Layer = "Locked";
            string before = document.Database.ToText();

            CadCommandSession session = Start(document, "TR", P(80, 0), P(80, 20), Enter);
            Assert.Contains("This line belongs to the model and cannot be trimmed here.", session.History);
            Assert.Contains("Object is on a locked layer.", session.History);
            Assert.Equal(before, document.Database.ToText());
            Assert.False(document.CanUndo);
        }

        [Fact]
        public void BreakALine_TakesOutThePartBetweenThePickAndTheSecondPoint()
        {
            DxfLine line = Line(0, 0, 100, 0);
            CadDocument document = Drawing(line);
            string before = document.Database.ToText();

            CadCommandSession session = Start(document, "BR");
            Assert.Equal("Select object:", session.PromptText);
            Run(session, P(20, 0));
            Assert.Equal("Specify second break point or [First point]:", session.PromptText);
            Run(session, P(60, 3));

            NearLine(Entity<DxfLine>(document, line.Handle), 0, 0, 20, 0);
            NearLine(All<DxfLine>(document.ModelSpace).Last(), 60, 0, 100, 0);
            UndoesToTheBytes(document, before, "BREAK");

            // @ for the second point breaks it in two at the first; First point gives the first anew.
            Start(document, "BREAK", P(20, 0), "@");
            Assert.Equal(2, All<DxfLine>(document.ModelSpace).Count);
            NearLine(Entity<DxfLine>(document, line.Handle), 0, 0, 20, 0);
            Assert.True(document.Undo());

            session = Start(document, "BREAK", P(20, 0), "F");
            Assert.Equal("Specify first break point:", session.PromptText);
            Run(session, P(40, 0), P(150, 0));
            DxfLine left = Assert.Single(All<DxfLine>(document.ModelSpace));
            NearLine(left, 0, 0, 40, 0);
        }

        [Fact]
        public void BreakACircle_TakesOutTheArcCounterClockwiseFromTheFirstPointToTheSecond()
        {
            var circle = new DxfCircle(new GeoPoint3(0, 0, 0), Up, 10);
            CadDocument document = Drawing(circle);

            Start(document, "BREAK", P(10, 0), P(0, 10));
            DxfArc arc = Assert.Single(All<DxfArc>(document.ModelSpace));
            Assert.Equal(Math.PI / 2, arc.StartAngle, 9);
            Assert.Equal(1.5 * Math.PI, arc.TotalAngle, 9);

            document = Drawing(new DxfCircle(new GeoPoint3(0, 0, 0), Up, 10));
            CadCommandSession session = Start(document, "BREAKATPOINT", P(10, 0), P(10, 0));
            Assert.Contains("Arc cannot be full 360 degrees.", session.History);
            Assert.False(document.CanUndo);
        }

        [Fact]
        public void BreakAtPoint_SplitsALineInTwo()
        {
            DxfLine line = Line(0, 0, 100, 0);
            CadDocument document = Drawing(line);

            CadCommandSession session = Start(document, "BREAKATPOINT", P(50, 0));
            Assert.Equal("Specify break point:", session.PromptText);
            Run(session, P(40, 0));
            NearLine(Entity<DxfLine>(document, line.Handle), 0, 0, 40, 0);
            NearLine(All<DxfLine>(document.ModelSpace).Last(), 40, 0, 100, 0);
        }

        [Fact]
        public void JoinAtOnce_CollinearLinesMakeALine_AndASquareOfLinesAClosedPolyline()
        {
            DxfLine first = Line(0, 0, 10, 0);
            DxfLine second = Line(15, 0, 30, 0);
            CadDocument document = Drawing(first, second);
            string before = document.Database.ToText();

            CadCommandSession session = Start(document, "J");
            Assert.Equal("Select source object or multiple objects to join at once:", session.PromptText);
            Run(session, new[] { first.Handle, second.Handle }, Enter);
            Assert.Contains("2 lines joined into 1 line", session.History);
            DxfLine joined = Assert.Single(All<DxfLine>(document.ModelSpace));
            NearLine(joined, 0, 0, 30, 0);
            UndoesToTheBytes(document, before, "JOIN");

            DxfLine[] sides = { Line(0, 0, 10, 0), Line(10, 10, 10, 0), Line(0, 10, 10, 10), Line(0, 0, 0, 10) };
            document = Drawing(sides);
            before = document.Database.ToText();
            session = Start(document, "JOIN", sides.Select(s => s.Handle).ToArray(), Enter);
            Assert.Contains("4 objects converted to 1 polyline", session.History);
            Assert.Empty(All<DxfLine>(document.ModelSpace));
            DxfPolyline square = Assert.Single(All<DxfPolyline>(document.ModelSpace));
            Assert.True(square.Closed);
            Assert.Equal(100.0, square.Area, 9);
            AssertReopens(document);
            UndoesToTheBytes(document, before, "JOIN");
        }

        [Fact]
        public void JoinAtOnce_ALineAndAnArcEndToEnd_MakeAPolylineWithTheArcsBulge()
        {
            DxfLine line = Line(0, 0, 10, 0);
            var arc = new DxfArc(new GeoPoint3(10, 5, 0), 5, -Math.PI / 2, Math.PI / 2);
            DxfLine far = Line(50, 50, 60, 50);
            CadDocument document = Drawing(line, arc, far);

            CadCommandSession session = Start(document, "J", new[] { line.Handle, arc.Handle, far.Handle }, Enter);
            DxfPolyline made = Assert.Single(All<DxfPolyline>(document.ModelSpace));
            Assert.Equal(3, made.NumberOfVertices);
            Near(P(0, 0), made.GetPoint2dAt(0));
            Near(P(10, 0), made.GetPoint2dAt(1));
            Near(P(10, 10), made.GetPoint2dAt(2), 1E-9);
            Assert.Equal(1.0, made.GetBulgeAt(1), 9);
            Assert.Contains("2 objects converted to 1 polyline", session.History);
            Assert.Contains("1 object discarded from the operation", session.History);
            Assert.NotNull(document.FindEntity(far.Handle));
        }

        [Fact]
        public void JoinToASource_ArcsOfItsCircleRunOnCounterClockwise()
        {
            var source = new DxfArc(new GeoPoint3(0, 0, 0), 10, 0, Math.PI / 2);
            var other = new DxfArc(new GeoPoint3(0, 0, 0), 10, Math.PI, 1.5 * Math.PI);
            DxfLine line = Line(0, 0, 5, 5);
            CadDocument document = Drawing(source, other, line);

            CadCommandSession session = Start(document, "JOIN", P(10 * Math.Sqrt(0.5), 10 * Math.Sqrt(0.5)), Enter);
            Assert.Equal("Select objects to join:", session.PromptText);
            Run(session, new[] { other.Handle, line.Handle }, Enter);
            DxfArc arc = Entity<DxfArc>(document, source.Handle);
            Assert.Equal(0.0, arc.StartAngle, 9);
            Assert.Equal(1.5 * Math.PI, arc.EndAngle, 9);
            Assert.Null(document.FindEntity(other.Handle));
            Assert.Contains("2 arcs joined into 1 arc", session.History);
            Assert.Contains("1 object discarded from the operation", session.History);
        }

        [Fact]
        public void LengthenDeltaPercentAndTotal_ChangeTheEndNearerThePick()
        {
            DxfLine line = Line(0, 0, 100, 0);
            CadDocument document = Drawing(line);
            string before = document.Database.ToText();

            CadCommandSession session = Start(document, "LEN");
            Assert.Equal("Select an object to measure or [DElta/Percent/Total/DYnamic] <Total>:", session.PromptText);
            Run(session, P(50, 0));
            Assert.Contains("Current length: 100.0000", session.History);
            Run(session, "DE");
            Assert.Equal("Enter delta length or [Angle] <0.0000>:", session.PromptText);
            Run(session, "10");
            Assert.Equal("Select an object to change:", session.PromptText);
            Run(session, P(90, 0));
            NearLine(Entity<DxfLine>(document, line.Handle), 0, 0, 110, 0);
            Assert.Equal("Select an object to change or [Undo]:", session.PromptText);
            Run(session, P(5, 0), Enter);
            NearLine(Entity<DxfLine>(document, line.Handle), -10, 0, 110, 0);
            UndoesToTheBytes(document, before, "LENGTHEN");

            Start(document, "LENGTHEN", "P", "50", P(10, 0), Enter);
            NearLine(Entity<DxfLine>(document, line.Handle), 50, 0, 100, 0);
            Assert.True(document.Undo());

            Start(document, "LENGTHEN", "T", "40", P(90, 0), Enter);
            NearLine(Entity<DxfLine>(document, line.Handle), 0, 0, 40, 0);
        }

        [Fact]
        public void LengthenAnArcByAngle_AndDynamic_MovesItsEnd()
        {
            var arc = new DxfArc(new GeoPoint3(0, 0, 0), 10, 0, Math.PI / 2);
            CadDocument document = Drawing(arc);

            CadCommandSession session = Start(document, "LEN", P(10 * Math.Sqrt(0.5), 10 * Math.Sqrt(0.5)));
            Assert.Contains("Current length: 15.7080, included angle: 90", session.History);
            Run(session, "T", "A");
            Assert.Equal("Specify total angle <57.2958>:", session.PromptText);
            Run(session, "180", P(1, 10), Enter);
            DxfArc run = Entity<DxfArc>(document, arc.Handle);
            Assert.Equal(0.0, run.StartAngle, 9);
            Assert.Equal(Math.PI, run.EndAngle, 9);

            DxfLine line = Line(0, 0, 100, 0);
            document = Drawing(line);
            session = Start(document, "LEN", "DY", P(90, 0));
            Assert.Equal("Specify new end point:", session.PromptText);
            Run(session, P(130, 7), P(10, 0), P(20, -3), Enter);
            NearLine(Entity<DxfLine>(document, line.Handle), 20, 0, 130, 0);
        }

        [Fact]
        public void LengthenAPolylineAndAnEllipticalArc_RunOnTheirEnds()
        {
            var polyline = new DxfPolyline();
            polyline.AddVertexAt(0, P(0, 0), 0, 0, 0);
            polyline.AddVertexAt(1, P(10, 0), 1, 0, 0);
            polyline.AddVertexAt(2, P(10, 10), 0, 0, 0);
            var ellipse = new DxfEllipse(new GeoPoint3(100, 0, 0), Up, new GeoVector3(20, 0, 0), 0.5, 0, Math.PI / 2);
            CadDocument document = Drawing(polyline, ellipse);
            double ellipseLength = ellipse.GetDistanceAtParameter(ellipse.EndParam);

            // The semicircle of radius 5 run on by a quarter of its length turns a further 45 degrees.
            Start(
                document,
                "LEN",
                "DE",
                (Math.PI * 5 / 4).ToString("R", System.Globalization.CultureInfo.InvariantCulture),
                P(10, 9.9),
                Enter
            );
            DxfPolyline run = Entity<DxfPolyline>(document, polyline.Handle);
            Near(P(10 - 5 * Math.Sqrt(0.5), 5 + 5 * Math.Sqrt(0.5)), run.GetPoint2dAt(2), 1E-9);
            Assert.Equal(Math.Tan(1.25 * Math.PI / 4), run.GetBulgeAt(1), 9);

            Start(document, "LEN", "DE", "5", P(100.5, 9.9), Enter);
            DxfEllipse longer = Entity<DxfEllipse>(document, ellipse.Handle);
            Assert.Equal(ellipseLength + 5, longer.GetDistanceAtParameter(longer.EndParam), 6);
            Assert.Equal(0.0, longer.StartParam, 12);
        }
    }
}
