using System;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.Dxf.Viewer.UnitTest.EditingDrawings;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    public class EditingSnapTests
    {
        private static PickIndex Index(params string[] entities)
        {
            CadDocument document = CadDocument.From(TestDrawings.Drawing(null, null, entities), "snap.dxf");
            return document.GetPickIndex(document.ModelSpace);
        }

        private static ObjectSnap Snaps(CadSnapModes modes) => new ObjectSnap(Tight) { Modes = modes };

        private static void Snapped(CadSnapResult result, CadSnapKind kind, double x, double y)
        {
            Assert.Equal(kind, result.Kind);
            Near(P(x, y), result.Point, 1E-9);
        }

        [Fact]
        public void EndpointAndMidpointOfALine_AreTakenExactlyNearTheCursor()
        {
            PickIndex index = Index(TestDrawings.Line(0, 0, 100, 0, handle: "A1"));
            ObjectSnap snap = Snaps(CadSnapModes.Endpoint | CadSnapModes.Midpoint);

            Snapped(snap.Snap(index, P(99.5, 0.4), 2.0), CadSnapKind.Endpoint, 100, 0);
            Snapped(snap.Snap(index, P(50.3, -0.2), 2.0), CadSnapKind.Midpoint, 50, 0);
            Assert.Equal("A1", snap.Snap(index, P(50.3, -0.2), 2.0).Handle);
            Snapped(snap.Snap(index, P(30, 0.2), 2.0), CadSnapKind.None, 30, 0.2);
        }

        [Fact]
        public void TheCentreOfACircle_IsTakenNearItOrFromItsRim_AndAQuadrantNearIt()
        {
            PickIndex index = Index(TestDrawings.Circle(200, 0, 10, handle: "A3"));
            ObjectSnap snap = Snaps(CadSnapModes.Center | CadSnapModes.Quadrant);

            Snapped(snap.Snap(index, P(200.5, 0.5), 2.0), CadSnapKind.Center, 200, 0);
            Snapped(snap.Snap(index, P(209.6, 0.3), 2.0), CadSnapKind.Quadrant, 210, 0);
            Snapped(snap.Snap(index, P(200 + 10 * Math.Cos(0.7), 10 * Math.Sin(0.7)), 2.0), CadSnapKind.Center, 200, 0);
        }

        [Fact]
        public void AnArcGivesItsEndsItsMiddleAndOnlyTheQuadrantsOnIt()
        {
            PickIndex index = Index(TestDrawings.Arc(0, 0, 10, 0, 90, handle: "A4"));
            ObjectSnap snap = Snaps(CadSnapModes.Endpoint | CadSnapModes.Midpoint | CadSnapModes.Quadrant);
            double half = 10.0 / Math.Sqrt(2.0);

            Snapped(snap.Snap(index, P(0.3, 9.8), 1.0), CadSnapKind.Endpoint, 0, 10);
            Snapped(snap.Snap(index, P(half + 0.2, half - 0.1), 1.0), CadSnapKind.Midpoint, half, half);
            Assert.Equal(CadSnapKind.None, snap.Snap(index, P(-9.8, 0.2), 1.0).Kind);
        }

        [Fact]
        public void TwoLinesCrossing_GiveTheirIntersectionExactly()
        {
            PickIndex index = Index(TestDrawings.Line(0, 0, 60, 60, handle: "A1"), TestDrawings.Line(0, 40, 80, 0, handle: "A2"));
            ObjectSnap snap = Snaps(CadSnapModes.Intersection);

            Snapped(snap.Snap(index, P(27, 26), 2.0), CadSnapKind.Intersection, 80.0 / 3.0, 80.0 / 3.0);
        }

        [Fact]
        public void ALineCrossingAnArc_AndTwoCirclesCrossing_GiveTheirIntersections()
        {
            PickIndex index = Index(
                TestDrawings.Arc(0, 0, 10, 0, 90, handle: "A4"),
                TestDrawings.Line(0, 5, 20, 5, handle: "A1"),
                TestDrawings.Circle(100, 0, 10, handle: "A3"),
                TestDrawings.Circle(112, 0, 10, handle: "A5"));
            ObjectSnap snap = Snaps(CadSnapModes.Intersection);

            Snapped(snap.Snap(index, P(8.5, 5.3), 1.0), CadSnapKind.Intersection, Math.Sqrt(75.0), 5);
            Snapped(snap.Snap(index, P(106.2, 7.7), 1.0), CadSnapKind.Intersection, 106, 8);
        }

        [Fact]
        public void PerpendicularGivesTheFootFromTheBasePoint_WhereverTheCursorIsOnTheLine()
        {
            PickIndex index = Index(TestDrawings.Line(0, 0, 100, 0, handle: "A1"), TestDrawings.Circle(200, 0, 10, handle: "A3"));
            ObjectSnap snap = Snaps(CadSnapModes.Perpendicular);

            Snapped(snap.Snap(index, P(20, 0.5), 1.0, P(50, 30)), CadSnapKind.Perpendicular, 50, 0);
            Snapped(snap.Snap(index, P(190.2, 1), 1.0, P(150, 0)), CadSnapKind.Perpendicular, 190, 0);
        }

        [Fact]
        public void TangentGivesWhereALineFromTheBasePointTouchesTheCircle()
        {
            PickIndex index = Index(TestDrawings.Circle(0, 0, 10, handle: "A3"));
            ObjectSnap snap = Snaps(CadSnapModes.Tangent);
            double angle = Math.Acos(10.0 / 30.0);

            CadSnapResult result = snap.Snap(index, P(10 * Math.Cos(angle) + 0.3, 10 * Math.Sin(angle)), 1.0, P(30, 0));

            Snapped(result, CadSnapKind.Tangent, 10 * Math.Cos(angle), 10 * Math.Sin(angle));
            Assert.Equal(0.0, (result.Point.X - 30) * result.Point.X + result.Point.Y * result.Point.Y, 9);
        }

        [Fact]
        public void NearestIsThePointOfTheObjectUnderTheCursor()
        {
            PickIndex index = Index(TestDrawings.Line(0, 0, 100, 0, handle: "A1"), TestDrawings.Circle(200, 0, 10, handle: "A3"));
            ObjectSnap snap = Snaps(CadSnapModes.Nearest);

            Snapped(snap.Snap(index, P(40, 0.7), 1.0), CadSnapKind.Nearest, 40, 0);
            Snapped(snap.Snap(index, P(200, 10.5), 1.0), CadSnapKind.Nearest, 200, 10);
        }

        [Fact]
        public void APolylineGivesItsVerticesAndTheMiddlesOfItsSegments()
        {
            PickIndex index = Index(TestDrawings.Polyline("0", "A5", false, 0, 0, 0, 100, 0, 0, 100, 50, 0));
            ObjectSnap snap = Snaps(CadSnapModes.Endpoint | CadSnapModes.Midpoint);

            Snapped(snap.Snap(index, P(99.4, 0.5), 1.0), CadSnapKind.Endpoint, 100, 0);
            Snapped(snap.Snap(index, P(100.3, 25.2), 1.0), CadSnapKind.Midpoint, 100, 25);
        }

        [Fact]
        public void AnInsertIsSnappedToWhereItPlacesTheLinesOfItsBlock()
        {
            string block = TestDrawings.Block("BAR", TestDrawings.Line(0, 0, 10, 0));
            CadDocument document = CadDocument.From(TestDrawings.Drawing(null, block, TestDrawings.Insert("BAR", 500, 500, 2.0, 90.0, handle: "B1")), "snap.dxf");
            ObjectSnap snap = Snaps(CadSnapModes.Endpoint);

            CadSnapResult result = snap.Snap(document.GetPickIndex(document.ModelSpace), P(500.4, 519.6), 1.0);

            Snapped(result, CadSnapKind.Endpoint, 500, 520);
            Assert.Equal("B1", result.Handle);
        }

        [Fact]
        public void APointNearTheCursorWinsOverNearest_AndOffSnapsTakeNone()
        {
            PickIndex index = Index(TestDrawings.Line(0, 0, 100, 0, handle: "A1"));
            ObjectSnap snap = Snaps(CadSnapModes.Endpoint | CadSnapModes.Nearest);

            Snapped(snap.Snap(index, P(99.2, 0.3), 1.0), CadSnapKind.Endpoint, 100, 0);
            snap.IsOn = false;
            Snapped(snap.Snap(index, P(99.2, 0.3), 1.0), CadSnapKind.None, 99.2, 0.3);
        }

        [Fact]
        public void OrthoHoldsTheCursorLevelOrUpright_PolarOnItsAngle_AndGridSnapOnTheGrid()
        {
            PickIndex index = Index(TestDrawings.Line(1000, 1000, 1100, 1000, handle: "A1"));
            var snap = new ObjectSnap(Tight) { IsOn = false, Ortho = true };

            Snapped(snap.Snap(index, P(10, 3), 1.0, P(0, 0)), CadSnapKind.Ortho, 10, 0);
            Snapped(snap.Snap(index, P(2, -9), 1.0, P(0, 0)), CadSnapKind.Ortho, 0, -9);

            snap.Ortho = false;
            snap.Polar = true;
            snap.PolarAngle = Math.PI / 4.0;
            CadSnapResult polar = snap.Snap(index, P(10, 9.5), 1.0, P(0, 0));
            Snapped(polar, CadSnapKind.Polar, 9.75, 9.75);
            Snapped(snap.Snap(index, P(10, 5), 1.0, P(0, 0)), CadSnapKind.None, 10, 5);

            snap.Polar = false;
            snap.GridSnap = true;
            Snapped(snap.Snap(index, P(12, 18), 1.0), CadSnapKind.Grid, 10, 20);
        }
    }
}
