using System;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    public class EditingGripTests
    {
        [Fact]
        public void ALineHasItsTwoEndsAndItsMiddle_AnEndDraggedMovesThatEnd_TheMiddleMovesTheLine()
        {
            CadDocument document = Plan();
            DxfLine line = Entity<DxfLine>(document, "A1");

            Assert.Equal(
                new[] { CadGripKind.End, CadGripKind.End, CadGripKind.Middle },
                CadGrips.Of(line).Select(g => g.Kind)
            );
            Near(P(50, 0), CadGrips.Of(line)[2].Point);

            Assert.True(document.GripDrag("A1", 1, P(100, 30)).Succeeded);
            Near(new GeoPoint3(100, 30, 0), Entity<DxfLine>(document, "A1").EndPoint);
            Near(new GeoPoint3(0, 0, 0), Entity<DxfLine>(document, "A1").StartPoint);

            Assert.True(document.GripDrag("A1", 2, P(60, 25)).Succeeded);
            Near(new GeoPoint3(10, 10, 0), Entity<DxfLine>(document, "A1").StartPoint);
            Assert.Equal("GRIP", document.NextUndo.Name);
        }

        [Fact]
        public void ACircleHasItsCentreAndFourQuadrants_AQuadrantDraggedSetsTheRadius()
        {
            CadDocument document = Plan();

            Assert.Equal(5, CadGrips.Of(Entity<DxfCircle>(document, "A3")).Count);
            Near(P(200, 10), CadGrips.Of(Entity<DxfCircle>(document, "A3"))[2].Point);

            document.GripDrag("A3", 2, P(200, 25));
            Assert.Equal(25, Entity<DxfCircle>(document, "A3").Radius, 9);

            document.GripDrag("A3", 0, P(0, 0));
            Near(new GeoPoint3(0, 0, 0), Entity<DxfCircle>(document, "A3").Center);
            Assert.Equal(25, Entity<DxfCircle>(document, "A3").Radius, 9);
        }

        [Fact]
        public void AnArcDraggedByAnEnd_IsTheArcThroughTheNewEndItsMiddleAndItsOtherEnd()
        {
            CadDocument document = Plan();

            // The arc of radius 20 about (300, 0) from 0 to 90 degrees; its start dragged round the circle to 270 degrees.
            Assert.True(document.GripDrag("A4", 0, P(300, -20)).Succeeded);

            DxfArc arc = Entity<DxfArc>(document, "A4");
            Near(new GeoPoint3(300, 0, 0), arc.Center, 1E-9);
            Assert.Equal(20, arc.Radius, 9);
            Assert.Equal(1.5 * Math.PI, arc.StartAngle, 9);
            Assert.Equal(0.5 * Math.PI, arc.EndAngle, 9);
        }

        [Fact]
        public void AnArcDraggedByItsMiddle_BendsThroughThePoint()
        {
            CadDocument document = Plan();

            // The arc from (320, 0) to (300, 20) about (300, 0), its middle pulled across the chord: about (320, 20) the other way round.
            double q = 20.0 - 20.0 / Math.Sqrt(2.0);
            document.GripDrag("A4", 2, P(300 + q, q));

            DxfArc arc = Entity<DxfArc>(document, "A4");
            Near(new GeoPoint3(320, 20, 0), arc.Center, 1E-9);
            Assert.Equal(20, arc.Radius, 9);
            Near(P(300, 20), CadGrips.Of(arc)[0].Point, 1E-9);
            Near(P(320, 0), CadGrips.Of(arc)[1].Point, 1E-9);
            Near(P(300 + q, q), CadGrips.Of(arc)[2].Point, 1E-9);
        }

        [Fact]
        public void AnArcMirrored_WhichAutoCadLeavesSeenFromBelow_HasItsGripsWhereItIsDrawn_AndIsDraggedThere()
        {
            CadDocument document = Plan();
            Run(Session(document), "MI", new[] { "A4" }, Enter, P(400, 0), P(400, 10), "Y");
            DxfArc arc = Entity<DxfArc>(document, "A4");
            Assert.True(arc.Normal.Z < 0.0);

            var grips = CadGrips.Of(arc);
            Near(P(arc.StartPoint.X, arc.StartPoint.Y), grips[0].Point, 1E-9);
            Near(P(arc.EndPoint.X, arc.EndPoint.Y), grips[1].Point, 1E-9);
            Near(P(500 - 20 / Math.Sqrt(2.0), 20 / Math.Sqrt(2.0)), grips[2].Point, 1E-9);

            // The end at (480, 0) dragged round to (500, -20): the half circle about (500, 0) on the left.
            int end = grips.Single(g => (g.Point - P(480, 0)).Length < 1E-9).Index;
            Assert.True(document.GripDrag("A4", end, P(500, -20)).Succeeded);

            arc = Entity<DxfArc>(document, "A4");
            Near(new GeoPoint3(500, 0, 0), arc.Center, 1E-9);
            Near(P(480, 0), CadGrips.Of(arc)[2].Point, 1E-9);
            Assert.Contains(new[] { arc.StartPoint, arc.EndPoint }, p =>
                p.DistanceTo(new GeoPoint3(500, -20, 0)) < 1E-9
            );
        }

        [Fact]
        public void APolylineHasItsVerticesThenTheMiddlesOfItsSegments()
        {
            CadDocument document = Plan();
            DxfPolyline polyline = Entity<DxfPolyline>(document, "A5");

            Assert.Equal(
                new[]
                {
                    CadGripKind.Vertex,
                    CadGripKind.Vertex,
                    CadGripKind.Vertex,
                    CadGripKind.SegmentMiddle,
                    CadGripKind.SegmentMiddle,
                },
                CadGrips.Of(polyline).Select(g => g.Kind)
            );

            document.GripDrag("A5", 1, P(110, 90));
            Near(P(110, 90), Entity<DxfPolyline>(document, "A5").GetPoint2dAt(1));

            // The middle of the first segment, from (0, 100) to (110, 90), dragged up 10: the segment moves whole.
            document.GripDrag("A5", 3, P(55, 105));
            Near(P(0, 110), Entity<DxfPolyline>(document, "A5").GetPoint2dAt(0));
            Near(P(110, 100), Entity<DxfPolyline>(document, "A5").GetPoint2dAt(1));
        }

        [Fact]
        public void TheMiddleOfAnArcSegmentDragged_SetsItsBulgeThroughThePoint()
        {
            CadDocument document = CadDocument.From(
                TestDrawings.Drawing(null, null, TestDrawings.Polyline("0", "B1", false, 0, 0, 1, 20, 0, 0)),
                "bulge.dxf"
            );

            // A half circle below the chord from (0, 0) to (20, 0); its middle is (10, -10). Dragged to (10, 5): above, a quarter as high.
            Near(P(10, -10), CadGrips.Of(document.FindEntity("B1"))[2].Point, 1E-9);
            document.GripDrag("B1", 2, P(10, 5));

            DxfPolyline polyline = Entity<DxfPolyline>(document, "B1");
            Assert.Equal(-0.5, polyline.GetBulgeAt(0), 9);
            Near(P(10, 5), CadGrips.Of(polyline)[2].Point, 1E-9);
        }

        [Fact]
        public void ATextAndAnInsertAreMovedByTheirInsertionPoint()
        {
            CadDocument document = Plan();

            document.GripDrag("A6", 0, P(10, 210));
            document.GripDrag("A7", 0, P(600, 50));

            Near(new GeoPoint3(10, 210, 0), Entity<DxfDBText>(document, "A6").Position);
            Near(new GeoPoint3(600, 50, 0), Entity<DxfBlockReference>(document, "A7").Position);
        }

        [Fact]
        public void EachGripDragIsOneUndoStep_AndUndoingThemAllGivesTheStartBack()
        {
            CadDocument document = Plan();
            string start = document.Database.ToText();

            document.GripDrag("A1", 0, P(-10, -10));
            document.GripDrag("A3", 1, P(230, 0));
            document.GripDrag("A4", 1, P(280, 0));
            document.GripDrag("A5", 4, P(120, 125));
            AssertReopens(document);

            int steps = 0;

            while (document.Undo())
            {
                steps++;
            }

            Assert.Equal(4, steps);
            Assert.Equal(start, document.Database.ToText());
        }

        private static CadDocument WithViewport(out string handle)
        {
            CadDocument document = Blank();
            CadCommandSession session = Session(document);
            Run(session, "REC", P(0, 0), P(2000, 1000));
            document.CurrentSpace = document.Spaces[1];
            Run(session, "MV", P(10, 10), P(210, 110));
            handle = All<DxfViewport>(document.CurrentSpace).Last().Handle;
            return document;
        }

        [Fact]
        public void AViewportHasItsFourCornersAndItsMiddle_ACornerDraggedResizesTheFrameAtTheSameScale()
        {
            CadDocument document = WithViewport(out string handle);
            var grips = CadGrips.Of(document.FindEntity(handle));

            Assert.Equal(
                new[]
                {
                    CadGripKind.Corner,
                    CadGripKind.Corner,
                    CadGripKind.Corner,
                    CadGripKind.Corner,
                    CadGripKind.Center,
                },
                grips.Select(g => g.Kind)
            );
            Near(P(210, 110), grips[2].Point);
            Assert.Empty(CadGrips.Of(All<DxfViewport>(document.CurrentSpace).First(v => v.Number == 1)));

            // The upper right corner dragged out to (310, 160): the lower left kept, the view at 1:10 about the same point.
            Assert.True(document.GripDrag(handle, 2, P(310, 160)).Succeeded);

            var viewport = Entity<DxfViewport>(document, handle);
            Near(new GeoPoint3(160, 85, 0), viewport.CenterPoint);
            Assert.Equal(300, viewport.Width, 9);
            Assert.Equal(150, viewport.Height, 9);
            Assert.Equal(0.1, viewport.Height / viewport.ViewHeight, 12);
            Assert.Equal(1000, viewport.ViewCenter.X, 9);
            Assert.Equal(
                1.5,
                Assert.Single(document.GetDisplay(document.CurrentSpace).Items.OfType<DisplayViewport2>()).Width
                    / 200.0,
                9
            );
        }

        [Fact]
        public void AViewportsMiddleDraggedMovesItsFrame_AndALockedViewportIsRefused()
        {
            CadDocument document = WithViewport(out string handle);

            document.GripDrag(handle, 4, P(300, 200));
            Near(new GeoPoint3(300, 200, 0), Entity<DxfViewport>(document, handle).CenterPoint);
            Assert.Equal(200, Entity<DxfViewport>(document, handle).Width, 9);

            Entity<DxfViewport>(document, handle).Locked = true;
            CadEditResult refused = document.GripDrag(handle, 0, P(0, 0));
            Assert.True(refused.Refused);
            Assert.Equal("The viewport is locked.", refused.Message);
        }

        [Fact]
        public void AGripOfAnEntityOnALockedLayer_OrOneItHasNot_IsRefused()
        {
            CadDocument document = Plan();

            Assert.Equal("The object is on a locked layer.", document.GripDrag("A8", 0, P(5, 5)).Message);
            Assert.True(document.GripDrag("A1", 7, P(5, 5)).Refused);
            Assert.False(document.CanUndo);
        }
    }
}
