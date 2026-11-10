using System;
using System.IO;
using System.Linq;
using System.Text;
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

        [Fact]
        public void AnObjectSnapUnderTheAperture_WinsOverGridSnapOrthoAndPolar()
        {
            PickIndex index = Index(TestDrawings.Line(0, 0, 4000, 3000, handle: "A1"));
            var snap = new ObjectSnap(Tight) { Modes = CadSnapModes.Endpoint, GridSnap = true, GridX = 10, GridY = 10 };

            Snapped(snap.Snap(index, P(3987, 2988), 50.0, P(1000, 500)), CadSnapKind.Endpoint, 4000, 3000);
            snap.Ortho = true;
            Snapped(snap.Snap(index, P(3987, 2988), 50.0, P(1000, 500)), CadSnapKind.Endpoint, 4000, 3000);
            snap.Ortho = false;
            snap.Polar = true;
            snap.PolarAngle = Math.PI / 4.0;
            Snapped(snap.Snap(index, P(3987, 2988), 50.0, P(1000, 500)), CadSnapKind.Endpoint, 4000, 3000);

            // Away from the line the grid holds the cursor again.
            snap.Polar = false;
            Snapped(snap.Snap(index, P(2987, 1012), 50.0, P(1000, 500)), CadSnapKind.Grid, 2990, 1010);
        }

        // Three walls of a room 4000 by 3000 and a column at its middle, written by hand as R12: no header, no tables, no handles.
        private const string HandleLessRoom =
            "  0\nSECTION\n  2\nENTITIES\n" +
            "  0\nLINE\n  8\n0\n 10\n0\n 20\n0\n 30\n0\n 11\n4000\n 21\n0\n 31\n0\n" +
            "  0\nLINE\n  8\n0\n 10\n4000\n 20\n0\n 30\n0\n 11\n4000\n 21\n3000\n 31\n0\n" +
            "  0\nLINE\n  8\n0\n 10\n4000\n 20\n3000\n 30\n0\n 11\n0\n 21\n3000\n 31\n0\n" +
            "  0\nCIRCLE\n  8\n0\n 10\n2000\n 20\n1500\n 30\n0\n 40\n300\n" +
            "  0\nENDSEC\n  0\nEOF\n";

        [Fact]
        public void AHandleLessDrawingOfR12_IsSnappedAsOneWithHandles_ObjectSnapBeforeTheGrid()
        {
            CadDocument document = CadDocument.From(DxfDatabase.Parse(HandleLessRoom), "room.dxf");
            PickIndex index = document.GetPickIndex(document.ModelSpace);
            var snap = new ObjectSnap(Tight) { Modes = CadSnapModes.Endpoint | CadSnapModes.Center, GridSnap = true, GridX = 10, GridY = 10 };

            CadSnapResult corner = snap.Snap(index, P(3987, 2988), 50.0, P(1000, 500));
            Snapped(corner, CadSnapKind.Endpoint, 4000, 3000);
            Assert.False(string.IsNullOrEmpty(corner.Handle));
            Snapped(snap.Snap(index, P(2290, 1503), 50.0), CadSnapKind.Center, 2000, 1500);
            Assert.Single(index.Pick(2000, 0.4, 1.0));
        }

        [Fact]
        public void AHandleLessDrawing_KeepsEachEntityUnderItsHandleThroughEraseUndoAndRedo()
        {
            CadDocument document = CadDocument.From(DxfDatabase.Parse(HandleLessRoom), "room.dxf");
            string bottom = Assert.Single(document.GetPickIndex(document.ModelSpace).Pick(2000, 0.4, 1.0));

            Assert.True(document.ApplyEdit("ERASE", new[] { new CadEditRecord(CadEditRecordKind.Erased, bottom) }, s => s.Document.FindEntity(bottom).Erase()).Succeeded);
            PickedIs(document, P(4000, 1500), 4000, 0, 4000, 3000);
            document.Undo();
            PickedIs(document, P(2000, 0), 0, 0, 4000, 0);
            PickedIs(document, P(4000, 1500), 4000, 0, 4000, 3000);
            document.Redo();
            Assert.Empty(document.GetPickIndex(document.ModelSpace).Pick(2000, 0.4, 1.0));
            PickedIs(document, P(4000, 1500), 4000, 0, 4000, 3000);
            PickedIs(document, P(2000, 3000), 4000, 3000, 0, 3000);

            // The handles are written, as AutoCAD saves the drawing, $HANDLING 1 in a header made for them.
            string saved = document.Database.ToText();
            Assert.Contains("$HANDLING", saved, StringComparison.Ordinal);
            Assert.Contains("  0\nLINE\n  5\n", saved, StringComparison.Ordinal);
        }

        [Fact]
        public void AHandleLessDrawingOfR12WithAHeader_IsGivenHandlesAndItsHeaderTheSeed_ItsLineEndsKept()
        {
            string text = ("  0\nSECTION\n  2\nHEADER\n  9\n$ACADVER\n  1\nAC1009\n  9\n$HANDLING\n 70\n     0\n  0\nENDSEC\n" +
                HandleLessRoom).Replace("\n", "\r\n");
            CadDocument document = CadDocument.Open(new MemoryStream(Encoding.ASCII.GetBytes(text)), "room.dxf");

            string saved = document.Database.ToText();
            Assert.Contains("$HANDLING\r\n 70\r\n     1\r\n", saved, StringComparison.Ordinal);
            Assert.Contains("$HANDSEED\r\n  5\r\n5\r\n", saved, StringComparison.Ordinal);
            Assert.DoesNotContain("\r\r", saved, StringComparison.Ordinal);
            Assert.Equal(new[] { "1", "2", "3", "4" }, document.ModelSpace.Block.OfType<DxfEntity>().Select(e => e.Handle));
            Assert.Equal(4, saved.Split(new[] { "\r\n  5\r\n" }, StringSplitOptions.None).Length - 2);
        }

        /// <summary>The one object picked at a point is the line between two points, found by its handle in the drawing as it stands.</summary>
        private static void PickedIs(CadDocument document, GeoPoint2 at, double x1, double y1, double x2, double y2)
        {
            string handle = Assert.Single(document.GetPickIndex(document.ModelSpace).Pick(at.X, at.Y, 1.0));
            var line = Assert.IsType<DxfLine>(document.FindEntity(handle));
            Near(new GeoPoint3(x1, y1, 0), line.StartPoint, 1E-9);
            Near(new GeoPoint3(x2, y2, 0), line.EndPoint, 1E-9);
        }
    }
}
