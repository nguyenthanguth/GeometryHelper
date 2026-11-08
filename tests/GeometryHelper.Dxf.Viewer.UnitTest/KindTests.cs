using System;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    /// <summary>The kinds of entity the display builder turns into items, beyond lines, arcs, circles, polylines and texts.</summary>
    public class KindTests
    {
        private static DisplayList2 Display(DxfDatabase database)
        {
            CadDocument document = CadDocument.From(database, "test.dxf");
            return document.GetDisplay(document.ModelSpace);
        }

        private static DisplayList2 Display(params string[] entities) => Display(TestDrawings.Drawing(null, null, entities));

        /// <summary>The least distance from a point to the items drawn.</summary>
        private static double Distance(DisplayList2 list, double x, double y) => list.Items.Min(i => i.DistanceTo(x, y));

        [Fact]
        public void AClosedPolylineWithABulgeDrawsEveryEdgeTheClosingOneAmongThem()
        {
            // A slot: straight along the bottom, a half circle up the right, straight back along the top and down the left.
            DisplayList2 list = Display(TestDrawings.Polyline("0", "A1", true, 0, 0, 0, 40, 0, 1, 40, 20, 0, 0, 20, 0));

            Assert.True(Distance(list, 0, 10) < 1E-9);
            Assert.True(Distance(list, 20, 0) < 1E-9);
            Assert.True(Distance(list, 20, 20) < 1E-9);
            Assert.True(Distance(list, 50, 10) < 1E-9);
        }

        [Fact]
        public void AnArcWhoseNormalPointsDownIsDrawnMirroredIntoTheWorld()
        {
            // Centre (10, 0) of its own system, which runs along the world's -x: the world centre is (-10, 0), the arc from
            // (-15, 0) round through (-13.54, 3.54) to (-10, 5).
            DisplayList2 list = Display(TestDrawings.Entity("ARC", "0", 256, "A1", "10", 10.0, "20", 0.0, "30", 0.0, "40", 5.0, "210", 0.0, "220", 0.0, "230", -1.0, "50", 0.0, "51", 90.0));

            DisplayArc2 arc = Assert.IsType<DisplayArc2>(Assert.Single(list.Items));
            Assert.Equal(-10.0, arc.Cx, 9);
            double h = 5.0 / Math.Sqrt(2.0);
            Assert.True(arc.DistanceTo(-10 - h, h) < 1E-9);
            Assert.True(arc.DistanceTo(-15, 0) < 1E-9);
            Assert.True(arc.DistanceTo(-10, 5) < 1E-9);
            Assert.True(arc.DistanceTo(-10 + h, h) > 1.0);
        }

        [Fact]
        public void AnInsertMirroredInXDrawsItsLineAndArcMirrored()
        {
            string block = TestDrawings.Block("MARK", TestDrawings.Line(0, 0, 10, 0), TestDrawings.Arc(5, 0, 5, 0, 90));
            DxfDatabase database = TestDrawings.Drawing(null, block, TestDrawings.Entity("INSERT", "0", 256, "B1", "2", "MARK", "10", 100.0, "20", 200.0, "30", 0.0, "41", -1.0, "42", 1.0, "43", 1.0));

            DisplayList2 list = Display(database);

            DisplayPolyline2 line = Assert.Single(list.Items.OfType<DisplayPolyline2>());
            Assert.Equal(new[] { 100.0, 200.0, 90.0, 200.0 }, line.Xy);
            DisplayArc2 arc = Assert.Single(list.Items.OfType<DisplayArc2>());
            double h = 5.0 / Math.Sqrt(2.0);
            Assert.True(arc.DistanceTo(95 - h, 200 + h) < 1E-9);
            Assert.True(arc.DistanceTo(95 + h, 200 + h) > 1.0);
            Assert.True(arc.DistanceTo(90, 200) < 1E-9);
            Assert.True(arc.DistanceTo(95, 205) < 1E-9);
        }

        [Fact]
        public void ABlockInsideABlockIsPlacedByBothInsertsAndKeepsTheOuterHandle()
        {
            string blocks = TestDrawings.Block("INNER", TestDrawings.Line(0, 0, 10, 0))
                + TestDrawings.Block("OUTER", TestDrawings.Insert("INNER", 5, 0, rotation: 90.0));
            DxfDatabase database = TestDrawings.Drawing(null, blocks, TestDrawings.Insert("OUTER", 100, 100, scale: 2.0, handle: "B1"));

            DisplayPolyline2 line = Assert.IsType<DisplayPolyline2>(Assert.Single(Display(database).Items));

            Assert.Equal("B1", line.Handle);
            Assert.Equal(110.0, line.Xy[0], 9);
            Assert.Equal(100.0, line.Xy[1], 9);
            Assert.Equal(110.0, line.Xy[2], 9);
            Assert.Equal(120.0, line.Xy[3], 9);
        }

        [Fact]
        public void AnArrayInsertDrawsItsBlockOncePerCellAlongItsTurnedRowsAndColumns()
        {
            string block = TestDrawings.Block("MARK", TestDrawings.Line(0, 0, 10, 0));
            DxfDatabase database = TestDrawings.Drawing(null, block, TestDrawings.Entity("INSERT", "0", 256, "B1", "2", "MARK", "10", 0.0, "20", 0.0, "30", 0.0, "50", 90.0, "70", 3, "71", 2, "44", 50.0, "45", 30.0));

            DisplayList2 list = Display(database);

            Assert.Equal(6, list.Count);
            Assert.All(list.Items, item => Assert.Equal("B1", item.Handle));

            // Turned 90 degrees, the columns run up the y axis and the rows to the left.
            double[] starts = list.Items.Cast<DisplayPolyline2>().Select(c => Math.Round(c.Xy[0]) * 1000 + Math.Round(c.Xy[1])).OrderBy(v => v).ToArray();
            Assert.Equal(new[] { -30000.0, -29950.0, -29900.0, 0.0, 50.0, 100.0 }, starts);
        }

        [Fact]
        public void AWholeEllipseIsAClosedChainAndAPartOfOneAnOpenChainOfItsPartOnly()
        {
            DisplayList2 whole = Display(TestDrawings.Entity("ELLIPSE", "0", 256, "E1", "10", 0.0, "20", 0.0, "30", 0.0, "11", 20.0, "21", 0.0, "31", 0.0, "40", 0.5, "41", 0.0, "42", 2 * Math.PI));
            DisplayList2 part = Display(TestDrawings.Entity("ELLIPSE", "0", 256, "E2", "10", 0.0, "20", 0.0, "30", 0.0, "11", 20.0, "21", 0.0, "31", 0.0, "40", 0.5, "41", 0.0, "42", Math.PI / 2));

            DisplayPolyline2 ring = Assert.IsType<DisplayPolyline2>(Assert.Single(whole.Items));
            Assert.True(ring.Closed);
            Assert.Equal(-20.0, ring.Bounds.MinX, 6);
            Assert.Equal(-10.0, ring.Bounds.MinY, 6);
            DisplayPolyline2 quarter = Assert.IsType<DisplayPolyline2>(Assert.Single(part.Items));
            Assert.False(quarter.Closed);
            Assert.Equal(0.0, quarter.Bounds.MinX, 6);
            Assert.Equal(0.0, quarter.Bounds.MinY, 6);
            Assert.Equal(20.0, quarter.Xy[0], 6);
            Assert.Equal(10.0, quarter.Xy[quarter.Xy.Count - 1], 6);
        }

        [Fact]
        public void ASplineRunsFromItsFirstControlPointToItsLast()
        {
            // A cubic Bezier from (0, 0) to (30, 0) pulled up by (10, 20) and (20, 20): its top is 15 high.
            DisplayList2 list = Display(TestDrawings.Entity(
                "SPLINE", "0", 256, "S1", "100", "AcDbSpline", "70", 8, "71", 3, "72", 8, "73", 4, "74", 0,
                "40", 0.0, "40", 0.0, "40", 0.0, "40", 0.0, "40", 1.0, "40", 1.0, "40", 1.0, "40", 1.0,
                "10", 0.0, "20", 0.0, "30", 0.0, "10", 10.0, "20", 20.0, "30", 0.0, "10", 20.0, "20", 20.0, "30", 0.0, "10", 30.0, "20", 0.0, "30", 0.0));

            DisplayPolyline2 chain = Assert.IsType<DisplayPolyline2>(Assert.Single(list.Items));
            Assert.Equal(0.0, chain.Xy[0], 9);
            Assert.Equal(30.0, chain.Xy[chain.Xy.Count - 2], 9);
            Assert.Equal(15.0, chain.Bounds.MaxY, 3);
        }

        [Fact]
        public void AThreeDFaceIsAClosedChainOfItsCornersAndATriangleOfThreeWhenTheLastTwoAreOne()
        {
            DisplayList2 quad = Display(TestDrawings.Entity("3DFACE", "0", 256, "F1", "10", 0.0, "20", 0.0, "30", 0.0, "11", 10.0, "21", 0.0, "31", 0.0, "12", 10.0, "22", 10.0, "32", 0.0, "13", 0.0, "23", 10.0, "33", 0.0));
            DisplayList2 triangle = Display(TestDrawings.Entity("3DFACE", "0", 256, "F2", "10", 0.0, "20", 0.0, "30", 0.0, "11", 10.0, "21", 0.0, "31", 0.0, "12", 10.0, "22", 10.0, "32", 0.0, "13", 10.0, "23", 10.0, "33", 0.0));

            Assert.True(quad.Items.All(i => !(i is DisplayFill2)));
            Assert.True(Distance(quad, 0, 5) < 1E-9);
            Assert.True(Distance(quad, 5, 10) < 1E-9);
            Assert.True(Distance(triangle, 5, 5) < 1E-9);
            Assert.True(Distance(triangle, 0, 5) > 1.0);
        }

        [Fact]
        public void ASolidOfFourPointsInItsBowTieOrderFillsTheRectangle()
        {
            DisplayList2 list = Display(TestDrawings.Solid(0, 0, 10, 0, 0, 20, 10, 20, color: 5, handle: "S1"));

            DisplayFill2 fill = Assert.IsType<DisplayFill2>(Assert.Single(list.Items));
            Assert.Equal(0x0000FF, fill.Style.Rgb);
            Assert.True(fill.Contains(5, 10));
            Assert.True(fill.Contains(1, 19));
            Assert.True(fill.Contains(9, 1));
        }

        [Fact]
        public void AWipeoutIsAMaskOverItsBoundary()
        {
            var database = new DxfDatabase("AC1018");
            var wipeout = new DxfWipeout();
            wipeout.SetFrom(new[] { new GeoPoint2(0, 0), new GeoPoint2(40, 0), new GeoPoint2(40, 30), new GeoPoint2(0, 30), new GeoPoint2(0, 0) }, new GeoVector3(0, 0, 1));
            database.ModelSpace.AppendEntity(wipeout);

            DisplayFill2 mask = Assert.IsType<DisplayFill2>(Assert.Single(Display(database).Items));

            Assert.True(mask.IsMask);
            Assert.True(mask.Contains(20, 15));
            Assert.Equal(40.0, mask.Bounds.MaxX, 6);
            Assert.Equal(30.0, mask.Bounds.MaxY, 6);
        }

        [Fact]
        public void APolylineOfTheOldKindIn2DAnd3DDrawsItsVerticesAndBulges()
        {
            string old2d = "0\nPOLYLINE\n5\nP1\n8\n0\n66\n1\n10\n0\n20\n0\n30\n0\n70\n1\n"
                + "0\nVERTEX\n5\nV1\n8\n0\n10\n0\n20\n0\n30\n0\n42\n1\n"
                + "0\nVERTEX\n5\nV2\n8\n0\n10\n20\n20\n0\n30\n0\n"
                + "0\nVERTEX\n5\nV3\n8\n0\n10\n20\n20\n30\n30\n0\n"
                + "0\nSEQEND\n5\nV4\n8\n0\n";
            string old3d = "0\nPOLYLINE\n5\nP2\n8\n0\n66\n1\n10\n0\n20\n0\n30\n0\n70\n8\n"
                + "0\nVERTEX\n5\nW1\n8\n0\n10\n100\n20\n0\n30\n0\n70\n32\n"
                + "0\nVERTEX\n5\nW2\n8\n0\n10\n110\n20\n10\n30\n50\n70\n32\n"
                + "0\nSEQEND\n5\nW3\n8\n0\n";

            DisplayList2 list = Display(old2d, old3d);

            // The half circle from (0, 0) to (20, 0) bulges below the x axis, its lowest point (10, -10).
            Assert.True(list.Items.Where(i => i.Handle == "P1").Min(i => i.DistanceTo(10, -10)) < 1E-6);
            Assert.True(list.Items.Where(i => i.Handle == "P1").Min(i => i.DistanceTo(10, 15)) < 1E-6);
            Assert.True(list.Items.Where(i => i.Handle == "P2").Min(i => i.DistanceTo(105, 5)) < 1E-6);
        }

        [Fact]
        public void ALeaderDrawsItsLineAndAFilledArrowheadAtItsFirstVertex()
        {
            DisplayList2 list = Display(TestDrawings.Entity(
                "LEADER", "0", 256, "L1", "100", "AcDbLeader", "3", "Standard", "71", 1, "72", 0, "76", 3,
                "10", 0.0, "20", 0.0, "30", 0.0, "10", 10.0, "20", 10.0, "30", 0.0, "10", 20.0, "20", 10.0, "30", 0.0));

            // The arrow is 0.18 long, the size of AutoCAD's own style where the drawing names none, along the first segment.
            Assert.Contains(list.Items, i => i is DisplayFill2 fill && fill.Contains(0.07, 0.07) && !fill.Contains(0.3, 0.3));
            Assert.True(Distance(list, 15, 10) < 1E-9);
            Assert.True(Distance(list, 5, 5) < 1E-9);
        }

        [Fact]
        public void ATableIsDrawnByItsBlockAtItsPoint()
        {
            string block = "0\nBLOCK\n8\n0\n2\n*T1\n70\n1\n10\n0\n20\n0\n30\n0\n3\n*T1\n1\n\n" + TestDrawings.Line(0, 0, 50, 0) + TestDrawings.Line(0, -10, 50, -10) + "0\nENDBLK\n8\n0\n";
            string table = TestDrawings.Entity(
                "ACAD_TABLE", "0", 256, "T1", "100", "AcDbBlockReference", "2", "*T1", "10", 100.0, "20", 50.0, "30", 0.0,
                "100", "AcDbTable", "280", 0, "11", 1.0, "21", 0.0, "31", 0.0, "91", 1, "92", 1, "141", 10.0, "142", 50.0);
            DxfDatabase database = TestDrawings.Drawing(null, block, table);

            DisplayList2 list = Display(database);

            Assert.Equal(2, list.Count);
            Assert.All(list.Items, item => Assert.Equal("T1", item.Handle));
            Assert.Equal(100.0, list.Bounds.MinX, 9);
            Assert.Equal(150.0, list.Bounds.MaxX, 9);
            Assert.Equal(40.0, list.Bounds.MinY, 9);
        }
    }
}
