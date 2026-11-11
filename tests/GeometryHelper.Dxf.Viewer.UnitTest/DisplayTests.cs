using System;
using System.Linq;
using GeometryHelper.Dxf;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    public class DisplayTests
    {
        private static DisplayList2 Display(DxfDatabase database)
        {
            CadDocument document = CadDocument.From(database, "test.dxf");
            return document.GetDisplay(document.ModelSpace);
        }

        [Fact]
        public void ALineIsOneChainOfTwoVerticesOnItsLayerInItsColour()
        {
            DxfDatabase database = TestDrawings.Drawing(
                TestDrawings.Layer("Steel", 1),
                null,
                TestDrawings.Line(0, 0, 300, 400, "Steel", handle: "A1")
            );

            DisplayList2 list = Display(database);

            DisplayPolyline2 chain = Assert.IsType<DisplayPolyline2>(Assert.Single(list.Items));
            Assert.Equal(new[] { 0.0, 0.0, 300.0, 400.0 }, chain.Xy);
            Assert.False(chain.Closed);
            Assert.Equal("Steel", chain.Layer);
            Assert.Equal("A1", chain.Handle);
            Assert.Equal(0xFF0000, chain.Style.Rgb);
            Assert.Equal(300.0, list.Bounds.MaxX, 9);
            Assert.Equal(400.0, list.Bounds.MaxY, 9);
        }

        [Fact]
        public void AnEntityWithItsOwnColourKeepsItAndOneByLayerTakesTheLayers()
        {
            DxfDatabase database = TestDrawings.Drawing(
                TestDrawings.Layer("Steel", 1),
                null,
                TestDrawings.Line(0, 0, 1, 0, "Steel", 3, "A1"),
                TestDrawings.Line(0, 1, 1, 1, "Steel", 256, "A2")
            );

            DisplayList2 list = Display(database);

            Assert.Equal(0x00FF00, list.Items.Single(i => i.Handle == "A1").Style.Rgb);
            Assert.Equal(0xFF0000, list.Items.Single(i => i.Handle == "A2").Style.Rgb);
        }

        [Fact]
        public void AnArcKeepsItsCentreRadiusAndSweepAndAWholeCircleIsOneTurn()
        {
            DxfDatabase database = TestDrawings.Drawing(
                null,
                null,
                TestDrawings.Arc(10, 20, 5, 90, 180, handle: "A1"),
                TestDrawings.Circle(0, 0, 8, handle: "A2")
            );

            DisplayList2 list = Display(database);

            DisplayArc2 arc = Assert.IsType<DisplayArc2>(list.Items.Single(i => i.Handle == "A1"));
            Assert.Equal(10.0, arc.Cx, 9);
            Assert.Equal(20.0, arc.Cy, 9);
            Assert.Equal(5.0, arc.Radius, 9);
            Assert.Equal(Math.PI / 2.0, arc.Start, 9);
            Assert.Equal(Math.PI / 2.0, arc.Sweep, 9);
            Assert.False(arc.IsCircle);
            Assert.Equal(5.0, arc.Bounds.MinX, 9);
            Assert.Equal(10.0, arc.Bounds.MaxX, 9);
            Assert.Equal(20.0, arc.Bounds.MinY, 9);
            Assert.Equal(25.0, arc.Bounds.MaxY, 9);

            DisplayArc2 circle = Assert.IsType<DisplayArc2>(list.Items.Single(i => i.Handle == "A2"));
            Assert.True(circle.IsCircle);
            Assert.Equal(-8.0, circle.Bounds.MinX, 9);
        }

        [Fact]
        public void APolylineWithABulgeIsAChainWhoseRoundEdgeIsAnArc()
        {
            // A slot end: from (0, 0) up to (0, 20) straight, then a half circle (bulge 1) to (20, 20) over the top.
            DxfDatabase database = TestDrawings.Drawing(
                null,
                null,
                TestDrawings.Polyline("0", "A1", false, 0, 0, 0, 0, 20, -1, 20, 20, 0)
            );

            DisplayList2 list = Display(database);

            Assert.Equal(2, list.Count);
            DisplayArc2 arc = Assert.IsType<DisplayArc2>(list.Items.Single(i => i is DisplayArc2));
            Assert.Equal(10.0, arc.Cx, 9);
            Assert.Equal(20.0, arc.Cy, 9);
            Assert.Equal(10.0, arc.Radius, 9);
            Assert.Equal(Math.PI, Math.Abs(arc.Sweep), 9);
            Assert.Equal(30.0, list.Bounds.MaxY, 9);
        }

        [Fact]
        public void ALayerSwitchedOffDrawsNothing()
        {
            DxfDatabase database = TestDrawings.Drawing(
                TestDrawings.Layer("Hidden", 8, off: true),
                null,
                TestDrawings.Line(0, 0, 1, 1, "Hidden", handle: "A1"),
                TestDrawings.Line(0, 0, 2, 2, "0", handle: "A2")
            );

            DisplayList2 list = Display(database);

            Assert.Equal("A2", Assert.Single(list.Items).Handle);
        }

        [Fact]
        public void AnInsertDrawsItsBlockMovedTurnedAndScaledAndTheItemsKeepTheInsertsHandle()
        {
            string block = TestDrawings.Block("MARK", TestDrawings.Line(0, 0, 10, 0));
            DxfDatabase database = TestDrawings.Drawing(
                null,
                block,
                TestDrawings.Insert("MARK", 100, 200, scale: 2.0, rotation: 90.0, handle: "B1")
            );

            DisplayList2 list = Display(database);

            DisplayPolyline2 chain = Assert.IsType<DisplayPolyline2>(Assert.Single(list.Items));
            Assert.Equal("B1", chain.Handle);
            Assert.Equal(100.0, chain.Xy[0], 9);
            Assert.Equal(200.0, chain.Xy[1], 9);
            Assert.Equal(100.0, chain.Xy[2], 9);
            Assert.Equal(220.0, chain.Xy[3], 9);
        }

        [Fact]
        public void AnEntityOnLayerZeroInABlockTakesTheLayerOfTheInsertAndOneByBlockItsColour()
        {
            string block = TestDrawings.Block("MARK", TestDrawings.Line(0, 0, 10, 0, "0", 0, "C1"));
            DxfDatabase database = TestDrawings.Drawing(
                TestDrawings.Layer("Bolts", 3),
                block,
                TestDrawings.Insert("MARK", 0, 0, layer: "Bolts", color: 5, handle: "B1")
            );

            DisplayList2 list = Display(database);

            DisplayItem2 item = Assert.Single(list.Items);
            Assert.Equal("Bolts", item.Layer);
            Assert.Equal(0x0000FF, item.Style.Rgb);
        }

        [Fact]
        public void TextKeepsItsHeightPositionAndTheDegreeSignOfPercentPercentD()
        {
            DxfDatabase database = TestDrawings.Drawing(
                null,
                null,
                TestDrawings.Text(5, 6, 2.5, "45%%d", handle: "T1")
            );

            DisplayList2 list = Display(database);

            DisplayText2 text = Assert.IsType<DisplayText2>(Assert.Single(list.Items));
            Assert.Equal("45°", text.Text);
            Assert.Equal(2.5, text.Height, 9);
            Assert.Equal(5.0, text.X, 9);
            Assert.Equal(6.0, text.Y, 9);
        }
    }
}
