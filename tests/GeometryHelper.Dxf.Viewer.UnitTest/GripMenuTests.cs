using System;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>
    /// The multi-functional grips of a polyline, as AutoCAD 2021's hover menus give them: a vertex grip offers Stretch
    /// Vertex, Add Vertex and Remove Vertex; the grip in the middle of a segment offers Stretch, Add Vertex and Convert to
    /// Arc (Convert to Line on an arc segment). Each action is one undo step, with AutoCAD's prompts.
    /// </summary>
    public class GripMenuTests
    {
        // A rectangle 100 by 50, closed: vertices 0 to 3, then the middles of its four segments (grips 4 to 7).
        private static CadDocument Rectangle(bool closed = true) =>
            CadDocument.From(
                TestDrawings.Drawing(
                    null,
                    null,
                    TestDrawings.Polyline("0", "R1", closed, 0, 0, 0, 100, 0, 0, 100, 50, 0, 0, 50, 0)
                ),
                "rect.dxf"
            );

        private static DxfPolyline Poly(CadDocument document, string handle = "R1") =>
            Entity<DxfPolyline>(document, handle);

        private static string[] Texts(CadDocument document, string handle, int grip) =>
            CadGripMenus.Of(document, handle, grip).Select(i => i.Text).ToArray();

        [Fact]
        public void TheMiddleOfAHorizontalSegmentHasADirectionAlongIt_AVerticalOneUp_AnArcsParallelToItsChord()
        {
            CadDocument document = CadDocument.From(
                TestDrawings.Drawing(
                    null,
                    null,
                    TestDrawings.Polyline("0", "B1", false, 0, 0, 0, 100, 0, 0, 100, 100, 0),
                    TestDrawings.Polyline("0", "B2", false, 0, 0, 1, 0, 100, 0)
                ),
                "dir.dxf"
            );

            CadGrip horizontal = CadGrips.Of(document.FindEntity("B1")).First(g => g.Index == 3);
            CadGrip vertical = CadGrips.Of(document.FindEntity("B1")).First(g => g.Index == 4);
            Assert.Equal(CadGripKind.SegmentMiddle, horizontal.Kind);
            Near(P(1, 0), new GeoPoint2(horizontal.Direction.X, horizontal.Direction.Y));
            Near(P(0, 1), new GeoPoint2(vertical.Direction.X, vertical.Direction.Y));

            // A half circle from (0, 0) to (0, 100) bulging to +x: the tangent at its middle (50, 50) is the chord's, straight up.
            CadGrip arc = CadGrips.Of(document.FindEntity("B2")).Single(g => g.Kind == CadGripKind.SegmentMiddle);
            Near(P(50, 50), arc.Point);
            Near(P(0, 1), new GeoPoint2(arc.Direction.X, arc.Direction.Y));
        }

        [Fact]
        public void AVertexGripOffersStretchVertexAddVertexAndRemoveVertex_AllOn()
        {
            CadDocument document = Rectangle();

            Assert.Equal(new[] { "Stretch Vertex", "Add Vertex", "Remove Vertex" }, Texts(document, "R1", 1));
            Assert.All(CadGripMenus.Of(document, "R1", 1), i => Assert.True(i.Enabled));
            Assert.Equal(
                new[] { CadGripAction.StretchVertex, CadGripAction.AddVertex, CadGripAction.RemoveVertex },
                CadGripMenus.Of(document, "R1", 1).Select(i => i.Action)
            );
        }

        [Fact]
        public void RemoveVertexIsGreyOnAPolylineOfTwoVertices_ButAClosedThreeLeaveTwo()
        {
            CadDocument two = CadDocument.From(
                TestDrawings.Drawing(null, null, TestDrawings.Polyline("0", "T2", false, 0, 0, 0, 10, 0, 0)),
                "two.dxf"
            );
            CadGripMenuItem remove = CadGripMenus.Of(two, "T2", 0).Single(i => i.Action == CadGripAction.RemoveVertex);
            Assert.False(remove.Enabled);

            CadDocument three = CadDocument.From(
                TestDrawings.Drawing(null, null, TestDrawings.Polyline("0", "T3", true, 0, 0, 0, 10, 0, 0, 10, 10, 0)),
                "three.dxf"
            );
            Assert.True(CadGripMenus.Of(three, "T3", 0).Single(i => i.Action == CadGripAction.RemoveVertex).Enabled);
        }

        [Fact]
        public void TheMiddleOfAStraightSegmentOffersStretchAddVertexConvertToArc_AnArcSegmentsConvertToLine()
        {
            CadDocument document = CadDocument.From(
                TestDrawings.Drawing(
                    null,
                    null,
                    TestDrawings.Polyline("0", "M1", false, 0, 0, 0, 100, 0, 1, 100, 100, 0)
                ),
                "mid.dxf"
            );

            Assert.Equal(new[] { "Stretch", "Add Vertex", "Convert to Arc" }, Texts(document, "M1", 3));
            Assert.Equal(new[] { "Stretch", "Add Vertex", "Convert to Line" }, Texts(document, "M1", 4));
        }

        [Fact]
        public void OtherEntitiesAndOtherGripsHaveNoMenu()
        {
            CadDocument document = Plan();

            Assert.Empty(CadGripMenus.Of(document, "A1", 0));
            Assert.Empty(CadGripMenus.Of(document, "A3", 0));
            Assert.Empty(CadGripMenus.Of(document, "A5", 99));
            Assert.Empty(CadGripMenus.Of(document, "ZZ", 0));
        }

        [Fact]
        public void AddVertexAfterVertexOneOfARectangle_MakesFiveVertices_TheNewOneAtIndexTwo_AsOneUndoStep()
        {
            CadDocument document = Rectangle();

            CadEditResult result = CadGrips.AddVertex(document, "R1", 1, P(120, 25));

            Assert.True(result.Succeeded);
            DxfPolyline polyline = Poly(document);
            Assert.Equal(5, polyline.NumberOfVertices);
            Near(P(100, 0), polyline.GetPoint2dAt(1));
            Near(P(120, 25), polyline.GetPoint2dAt(2));
            Near(P(100, 50), polyline.GetPoint2dAt(3));
            Assert.Equal(0.0, polyline.GetBulgeAt(1));
            Assert.Equal(0.0, polyline.GetBulgeAt(2));
            Assert.Equal("GRIP", document.NextUndo.Name);

            Assert.True(document.Undo());
            Assert.Equal(4, Poly(document).NumberOfVertices);
            Assert.False(document.CanUndo);
        }

        [Fact]
        public void AddVertexAfterTheLastVertexOfAnOpenPolyline_ExtendsItsEnd_OfAClosedOne_SplitsTheClosingSegment()
        {
            CadDocument open = Rectangle(false);
            Assert.True(CadGrips.AddVertex(open, "R1", 3, P(-20, 60)).Succeeded);
            Assert.Equal(5, Poly(open).NumberOfVertices);
            Near(P(-20, 60), Poly(open).GetPoint2dAt(4));

            CadDocument closed = Rectangle();
            Assert.True(CadGrips.AddVertex(closed, "R1", 3, P(-20, 25)).Succeeded);
            Assert.Equal(5, Poly(closed).NumberOfVertices);
            Near(P(-20, 25), Poly(closed).GetPoint2dAt(4));
            Assert.True(Poly(closed).Closed);
        }

        [Fact]
        public void AddVertexFromTheMiddleOfASegment_PutsTheVertexInsideThatSegment()
        {
            CadDocument document = Rectangle();

            // Grip 5 is the middle of the segment from vertex 1 to vertex 2, so the new vertex is index 2.
            Assert.True(CadGrips.AddVertex(document, "R1", 5, P(110, 25)).Succeeded);

            Near(P(110, 25), Poly(document).GetPoint2dAt(2));
            Near(P(100, 50), Poly(document).GetPoint2dAt(3));
        }

        [Fact]
        public void AddVertexInAnArcSegment_KeepsBothPartsArcs_ThroughTheNewPoint()
        {
            // A half circle from (0, 0) to (20, 0) below the chord (bulge 1); (10, -10) is on it, so the two parts are the
            // quarters of the same circle: a bulge of tan(45 / 4 degrees... a quarter turn / 4) = tan(pi / 8) each.
            CadDocument document = CadDocument.From(
                TestDrawings.Drawing(null, null, TestDrawings.Polyline("0", "H1", false, 0, 0, 1, 20, 0, 0)),
                "half.dxf"
            );

            Assert.True(CadGrips.AddVertex(document, "H1", 2, P(10, -10)).Succeeded);

            DxfPolyline polyline = Poly(document, "H1");
            Assert.Equal(3, polyline.NumberOfVertices);
            Assert.Equal(Math.Tan(Math.PI / 8.0), polyline.GetBulgeAt(0), 9);
            Assert.Equal(Math.Tan(Math.PI / 8.0), polyline.GetBulgeAt(1), 9);

            // Through a point off the old arc: the arc through (0, 0), (10, -5), (20, 0) has a bulge of 0.5 whole.
            CadDocument other = CadDocument.From(
                TestDrawings.Drawing(null, null, TestDrawings.Polyline("0", "H2", false, 0, 0, 1, 20, 0, 0)),
                "half2.dxf"
            );
            Assert.True(CadGrips.AddVertex(other, "H2", 2, P(10, -5)).Succeeded);
            DxfPolyline bent = Poly(other, "H2");
            Assert.Equal(bent.GetBulgeAt(0), bent.GetBulgeAt(1), 9);
            Assert.True(bent.GetBulgeAt(0) > 0.0);
            double sweep = 4.0 * Math.Atan(bent.GetBulgeAt(0)) * 2.0;
            Assert.Equal(4.0 * Math.Atan(0.5), sweep, 9);
        }

        [Fact]
        public void AddVertexInATaperedSegment_SplitsTheWidthWhereThePointProjects()
        {
            CadDocument document = CadDocument.From(
                TestDrawings.Drawing(null, null, TestDrawings.Polyline("0", "W1", false, 0, 0, 0, 100, 0, 0)),
                "wide.dxf"
            );
            DxfPolyline polyline = Poly(document, "W1");
            polyline.SetStartWidthAt(0, 2.0);
            polyline.SetEndWidthAt(0, 6.0);

            Assert.True(CadGrips.AddVertex(document, "W1", 2, P(50, 30)).Succeeded);

            polyline = Poly(document, "W1");
            Assert.Equal(2.0, polyline.GetStartWidthAt(0), 9);
            Assert.Equal(4.0, polyline.GetEndWidthAt(0), 9);
            Assert.Equal(4.0, polyline.GetStartWidthAt(1), 9);
            Assert.Equal(6.0, polyline.GetEndWidthAt(1), 9);
        }

        [Fact]
        public void ConvertToArcThroughAPoint20OffTheMiddleOfAChordOf100_GivesABulgeOfPointFour_OnTheSideOfThePoint()
        {
            CadDocument document = CadDocument.From(
                TestDrawings.Drawing(null, null, TestDrawings.Polyline("0", "C1", false, 0, 0, 0, 100, 0, 0)),
                "conv.dxf"
            );

            // Sagitta 20 over a half chord of 50. A positive bulge bends to the right of the way a to b, so above is negative.
            Assert.True(CadGrips.ConvertToArc(document, "C1", 2, P(50, 20)).Succeeded);
            Assert.Equal(-0.4, Poly(document, "C1").GetBulgeAt(0), 9);
            Assert.Equal("GRIP", document.NextUndo.Name);

            Assert.True(document.Undo());
            Assert.Equal(0.0, Poly(document, "C1").GetBulgeAt(0));

            Assert.True(CadGrips.ConvertToArc(document, "C1", 2, P(50, -20)).Succeeded);
            Assert.Equal(0.4, Poly(document, "C1").GetBulgeAt(0), 9);
        }

        [Fact]
        public void ConvertToArcRefusesAPointOnTheChord_AndASegmentThatIsAlreadyAnArc()
        {
            CadDocument document = CadDocument.From(
                TestDrawings.Drawing(
                    null,
                    null,
                    TestDrawings.Polyline("0", "C2", false, 0, 0, 0, 100, 0, 0.5, 100, 50, 0)
                ),
                "conv2.dxf"
            );

            Assert.False(CadGrips.ConvertToArc(document, "C2", 3, P(50, 0)).Succeeded);
            Assert.False(CadGrips.ConvertToArc(document, "C2", 4, P(130, 25)).Succeeded);
            Assert.False(document.CanUndo);
        }

        [Fact]
        public void ConvertToLineOnAnArcSegment_MakesItStraight_AsOneUndoStep()
        {
            CadDocument document = CadDocument.From(
                TestDrawings.Drawing(
                    null,
                    null,
                    TestDrawings.Polyline("0", "L1", false, 0, 0, 0.7, 100, 0, 0, 100, 50, 0)
                ),
                "toline.dxf"
            );

            Assert.True(CadGrips.ConvertToLine(document, "L1", 3).Succeeded);

            Assert.Equal(0.0, Poly(document, "L1").GetBulgeAt(0));
            Assert.Equal("GRIP", document.NextUndo.Name);
            Assert.True(document.Undo());
            Assert.Equal(0.7, Poly(document, "L1").GetBulgeAt(0), 9);

            // A segment already straight is refused.
            Assert.False(CadGrips.ConvertToLine(document, "L1", 4).Succeeded);
        }

        [Fact]
        public void RemoveVertex_TakesTheVertexOut_TheSegmentThatLeavesItsNeighbourBecomingStraight_AsOneUndoStep()
        {
            CadDocument document = CadDocument.From(
                TestDrawings.Drawing(
                    null,
                    null,
                    TestDrawings.Polyline("0", "D1", false, 0, 0, 0.5, 100, 0, 0, 100, 50, 0, 0, 50, 0)
                ),
                "remove.dxf"
            );

            Assert.True(CadGrips.RemoveVertex(document, "D1", 1).Succeeded);

            DxfPolyline polyline = Poly(document, "D1");
            Assert.Equal(3, polyline.NumberOfVertices);
            Near(P(100, 50), polyline.GetPoint2dAt(1));
            Assert.Equal(0.0, polyline.GetBulgeAt(0));
            Assert.Equal("GRIP", document.NextUndo.Name);

            Assert.True(document.Undo());
            Assert.Equal(4, Poly(document, "D1").NumberOfVertices);
            Assert.Equal(0.5, Poly(document, "D1").GetBulgeAt(0), 9);
        }

        [Fact]
        public void RemoveVertexRefusesBelowTwoVertices_AndOnASegmentGrip()
        {
            CadDocument two = CadDocument.From(
                TestDrawings.Drawing(null, null, TestDrawings.Polyline("0", "D2", false, 0, 0, 0, 10, 0, 0)),
                "remove2.dxf"
            );
            Assert.False(CadGrips.RemoveVertex(two, "D2", 0).Succeeded);
            Assert.Equal(2, Poly(two, "D2").NumberOfVertices);
            Assert.False(CadGrips.RemoveVertex(Rectangle(), "R1", 4).Succeeded);

            CadDocument three = CadDocument.From(
                TestDrawings.Drawing(null, null, TestDrawings.Polyline("0", "D3", true, 0, 0, 0, 10, 0, 0, 10, 10, 0)),
                "remove3.dxf"
            );
            Assert.True(CadGrips.RemoveVertex(three, "D3", 2).Succeeded);
            Assert.Equal(2, Poly(three, "D3").NumberOfVertices);
        }

        [Fact]
        public void TheEditsRefuseAnObjectOnALockedLayer()
        {
            CadDocument document = Plan();
            DxfPolyline polyline = Entity<DxfPolyline>(document, "A5");
            polyline.Layer = "Locked";

            Assert.False(CadGrips.AddVertex(document, "A5", 0, P(50, 100)).Succeeded);
            Assert.False(CadGrips.RemoveVertex(document, "A5", 1).Succeeded);
            Assert.False(CadGrips.ConvertToArc(document, "A5", 3, P(50, 120)).Succeeded);
            Assert.Equal(3, Entity<DxfPolyline>(document, "A5").NumberOfVertices);
        }

        [Fact]
        public void AddVertexFromTheMenu_AsksInAutoCadsWords_AndAPointInsertsIt_AsOneUndoStep()
        {
            CadDocument document = Rectangle();
            CadCommandSession session = Session(document);
            session.Tolerance = Tolerance.Default;

            Assert.True(session.StartGripAction("R1", 1, CadGripAction.AddVertex));

            Assert.True(session.IsGripEditing);
            Assert.Equal("** ADD VERTEX **", session.History.Last());
            Assert.Equal("Specify new vertex point or [Base point/Copy/eXit]:", session.PromptText);
            Near(P(100, 0), session.Prompt.BasePoint.Value);

            Run(session, P(120, 25));

            Assert.False(session.IsRunning);
            Assert.Equal(5, Poly(document).NumberOfVertices);
            Near(P(120, 25), Poly(document).GetPoint2dAt(2));
            Assert.Equal("GRIP", document.NextUndo.Name);
            Assert.True(document.Undo());
            Assert.False(document.CanUndo);
        }

        [Fact]
        public void AddVertexWithCopy_KeepsAsking_EachVertexAfterTheLast_TheWholeRunOneUndoStep()
        {
            CadDocument document = Rectangle(false);
            CadCommandSession session = Session(document);
            session.Tolerance = Tolerance.Default;
            Assert.True(session.StartGripAction("R1", 3, CadGripAction.AddVertex));

            Run(session, "C", P(-10, 40));
            Assert.True(session.IsRunning);
            Assert.Equal("** ADD VERTEX (multiple) **", session.History.Last());
            Run(session, P(-20, 30), "X");

            Assert.False(session.IsRunning);
            DxfPolyline polyline = Poly(document);
            Assert.Equal(6, polyline.NumberOfVertices);
            Near(P(-10, 40), polyline.GetPoint2dAt(4));
            Near(P(-20, 30), polyline.GetPoint2dAt(5));
            Assert.True(document.Undo());
            Assert.Equal(4, Poly(document).NumberOfVertices);
            Assert.False(document.CanUndo);
        }

        [Fact]
        public void EscapeAtAddVertexOrConvertToArc_ChangesNothing_AndLeavesNoUndoStep()
        {
            CadDocument document = Rectangle();
            CadCommandSession session = Session(document);
            Assert.True(session.StartGripAction("R1", 1, CadGripAction.AddVertex));
            Run(session, Esc);
            Assert.False(session.IsRunning);

            Assert.True(session.StartGripAction("R1", 4, CadGripAction.ConvertToArc));
            Run(session, Esc);
            Assert.False(session.IsRunning);

            Assert.Equal(4, Poly(document).NumberOfVertices);
            Assert.Equal(0.0, Poly(document).GetBulgeAt(0));
            Assert.False(document.CanUndo);
        }

        [Fact]
        public void ConvertToArcFromTheMenu_AsksForTheArcMidpoint_AndAPointBendsTheSegment()
        {
            CadDocument document = Rectangle();
            CadCommandSession session = Session(document);
            session.Tolerance = Tolerance.Default;

            Assert.True(session.StartGripAction("R1", 4, CadGripAction.ConvertToArc));

            Assert.Equal("** CONVERT TO ARC **", session.History.Last());
            Assert.Equal("Specify the arc midpoint:", session.PromptText);
            Assert.NotNull(session.Prompt.Preview);

            Run(session, P(50, -20));

            Assert.False(session.IsRunning);
            Assert.Equal(0.4, Poly(document).GetBulgeAt(0), 9);
            Assert.True(document.Undo());
            Assert.False(document.CanUndo);
        }

        [Fact]
        public void StretchVertexFromTheMenu_AsksStretchVertex_AndAPointMovesTheVertex()
        {
            CadDocument document = Rectangle();
            CadCommandSession session = Session(document);
            session.Tolerance = Tolerance.Default;

            Assert.True(session.StartGripAction("R1", 2, CadGripAction.StretchVertex));

            Assert.Equal("** STRETCH VERTEX **", session.History.Last());
            Assert.Equal("Specify stretch point or [Base point/Copy/Undo/eXit]:", session.PromptText);

            Run(session, P(110, 60));

            Assert.False(session.IsRunning);
            Near(P(110, 60), Poly(document).GetPoint2dAt(2));
            Assert.Equal("GRIP", document.NextUndo.Name);
        }

        [Fact]
        public void StretchFromTheMiddleOfASegment_MovesBothEndsByTheSameOffset()
        {
            CadDocument document = Rectangle();
            CadCommandSession session = Session(document);
            session.Tolerance = Tolerance.Default;

            Assert.True(session.StartGripAction("R1", 4, CadGripAction.Stretch));
            Assert.Equal("** STRETCH **", session.History.Last());
            Run(session, P(50, -10));

            Near(P(0, -10), Poly(document).GetPoint2dAt(0));
            Near(P(100, -10), Poly(document).GetPoint2dAt(1));
        }

        [Fact]
        public void RemoveVertexAndConvertToLineFromTheMenu_AreDoneAtOnce_OneUndoStepEach()
        {
            CadDocument document = Rectangle();
            CadCommandSession session = Session(document);

            Assert.True(session.StartGripAction("R1", 0, CadGripAction.RemoveVertex));

            Assert.False(session.IsRunning);
            Assert.Equal(3, Poly(document).NumberOfVertices);
            Assert.Equal("GRIP", document.NextUndo.Name);
            Assert.True(document.Undo());
            Assert.Equal(4, Poly(document).NumberOfVertices);
            Assert.False(document.CanUndo);

            Assert.True(CadGrips.ConvertToArc(document, "R1", 4, P(50, -20)).Succeeded);
            Assert.True(document.Undo());
            Assert.True(CadGrips.ConvertToArc(document, "R1", 4, P(50, -20)).Succeeded);
            Assert.True(session.StartGripAction("R1", 4, CadGripAction.ConvertToLine));
            Assert.False(session.IsRunning);
            Assert.Equal(0.0, Poly(document).GetBulgeAt(0));
        }

        [Fact]
        public void AnActionTheGripDoesNotOffer_StartsNothing()
        {
            CadDocument document = Rectangle();
            CadCommandSession session = Session(document);

            Assert.False(session.StartGripAction("R1", 0, CadGripAction.ConvertToArc));
            Assert.False(session.StartGripAction("R1", 4, CadGripAction.RemoveVertex));
            Assert.False(session.StartGripAction("R1", 4, CadGripAction.ConvertToLine));
            Assert.False(session.StartGripAction("R1", 99, CadGripAction.Stretch));
            Assert.False(session.StartGripAction("ZZ", 0, CadGripAction.Stretch));
            Assert.False(session.IsRunning);

            CadDocument two = CadDocument.From(
                TestDrawings.Drawing(null, null, TestDrawings.Polyline("0", "T2", false, 0, 0, 0, 10, 0, 0)),
                "two.dxf"
            );
            Assert.False(Session(two).StartGripAction("T2", 0, CadGripAction.RemoveVertex));
        }

        [Fact]
        public void AClickOnAGrip_StillAsksStretch_AsBefore()
        {
            CadDocument document = Rectangle();
            CadCommandSession session = Session(document);

            Assert.True(
                session.StartGripEdit(new[] { new System.Collections.Generic.KeyValuePair<string, int>("R1", 1) })
            );

            Assert.Equal("** STRETCH **", session.History.Last());
        }
    }
}
