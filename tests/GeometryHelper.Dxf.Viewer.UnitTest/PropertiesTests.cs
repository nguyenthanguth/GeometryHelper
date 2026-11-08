using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Dxf.Viewer.UnitTest
{
    public class PropertiesTests
    {
        private static CadSelectionInfo Describe(DxfDatabase database, params string[] handles)
        {
            CadDocument document = CadDocument.From(database, "test.dxf");
            return CadProperties.Describe(document, document.ModelSpace, handles);
        }

        private static CadPropertyRow Row(CadSelectionInfo info, string group, string label)
            => info.Groups.Single(g => g.Title == group).Rows.Single(r => r.Label == label);

        [Fact]
        public void NothingSelectedShowsTheCurrentSettingsThePlotStyleTheViewOfTheExtentsAndMisc()
        {
            DxfDatabase database = TestDrawings.Drawing(TestDrawings.Layer("Steel", 1), null, TestDrawings.Line(0, 0, 300, 400, handle: "A1"));
            database.Clayer = "Steel";
            database.Cecolor = DxfColor.FromColorIndex(DxfColorMethod.ByAci, 1);

            CadSelectionInfo info = Describe(database);

            Assert.Equal("No selection", info.Label);
            Assert.Equal(new[] { "General", "Plot style", "View", "Misc" }, info.Groups.Select(g => g.Title));
            Assert.Equal(new[] { "Color", "Layer", "Linetype", "Linetype scale", "Lineweight", "Transparency" }, info.Groups[0].Rows.Select(r => r.Label));
            Assert.Equal("Red", Row(info, "General", "Color").Value);
            Assert.Equal("Steel", Row(info, "General", "Layer").Value);
            Assert.Equal("ByLayer", Row(info, "General", "Linetype").Value);
            Assert.Equal("150.0000", Row(info, "View", "Center X").Value);
            Assert.Equal("200.0000", Row(info, "View", "Center Y").Value);
            Assert.Equal("400.0000", Row(info, "View", "Height").Value);
            Assert.Equal("300.0000", Row(info, "View", "Width").Value);
            Assert.True(Row(info, "View", "Width").IsReadOnly);
            Assert.Equal("2D Wireframe", Row(info, "Misc", "Visual style").Value);
            Assert.True(Row(info, "Misc", "Visual style").IsReadOnly);
            Assert.Equal("Yes", Row(info, "Misc", "UCS icon On").Value);
        }

        [Fact]
        public void ALineSelectedReadsLine1WithItsLayerColourEndsLengthAndAngle()
        {
            DxfDatabase database = TestDrawings.Drawing(TestDrawings.Layer("Steel", 1), null, TestDrawings.Line(0, 0, 300, 400, "Steel", 3, "A1"));
            database.Auprec = 0;

            CadSelectionInfo info = Describe(database, "A1");

            Assert.Equal("Line (1)", info.Label);
            Assert.Equal("Green", Row(info, "General", "Color").Value);
            Assert.Equal("Steel", Row(info, "General", "Layer").Value);
            Assert.Equal("ByLayer", Row(info, "General", "Lineweight").Value);
            Assert.Equal("1.0000", Row(info, "General", "Linetype scale").Value);
            Assert.Equal("300.0000", Row(info, "Geometry", "End X").Value);
            Assert.Equal("400.0000", Row(info, "Geometry", "End Y").Value);
            Assert.Equal("500.0000", Row(info, "Geometry", "Length").Value);
            Assert.True(Row(info, "Geometry", "Length").IsReadOnly);
            Assert.Equal("53", Row(info, "Geometry", "Angle").Value);
        }

        [Fact]
        public void AColourIsWrittenByLayerByBlockByNameByIndexOrAsRedGreenAndBlue()
        {
            DxfDatabase database = TestDrawings.Drawing(
                null,
                null,
                TestDrawings.Line(0, 0, 1, 0, handle: "A1"),
                TestDrawings.Line(0, 0, 1, 0, color: 0, handle: "A2"),
                TestDrawings.Line(0, 0, 1, 0, color: 5, handle: "A3"),
                TestDrawings.Line(0, 0, 1, 0, color: 8, handle: "A4"));
            var modern = new DxfDatabase("AC1018");
            var orange = new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(1, 0, 0));
            modern.ModelSpace.AppendEntity(orange);
            orange.Color = DxfColor.FromRgb(255, 128, 0);

            Assert.Equal("ByLayer", Row(Describe(database, "A1"), "General", "Color").Value);
            Assert.Equal("ByBlock", Row(Describe(database, "A2"), "General", "Color").Value);
            Assert.Equal("Blue", Row(Describe(database, "A3"), "General", "Color").Value);
            Assert.Equal("8", Row(Describe(database, "A4"), "General", "Color").Value);
            Assert.Equal("255,128,0", Row(Describe(modern, orange.Handle), "General", "Color").Value);
        }

        [Fact]
        public void TwoLinesOnTwoLayersReadLine2AndTheirLayerVaries()
        {
            DxfDatabase database = TestDrawings.Drawing(
                TestDrawings.Layer("Steel", 1) + TestDrawings.Layer("Bolts", 3),
                null,
                TestDrawings.Line(0, 0, 10, 0, "Steel", 2, "A1"),
                TestDrawings.Line(0, 5, 10, 5, "Bolts", 2, "A2"));

            CadSelectionInfo info = Describe(database, "A1", "A2");

            Assert.Equal("Line (2)", info.Label);
            Assert.True(Row(info, "General", "Layer").Varies);
            Assert.Equal("Yellow", Row(info, "General", "Color").Value);
            Assert.Equal("0.0000", Row(info, "Geometry", "Start X").Value);
            Assert.True(Row(info, "Geometry", "Start Y").Varies);
            Assert.Equal("10.0000", Row(info, "Geometry", "Length").Value);
        }

        [Fact]
        public void ALineAndACircleReadAll2AndShowOnlyTheGeneralGroup()
        {
            DxfDatabase database = TestDrawings.Drawing(null, null, TestDrawings.Line(0, 0, 10, 0, handle: "A1"), TestDrawings.Circle(0, 0, 5, handle: "A2"));

            CadSelectionInfo info = Describe(database, "A2", "A1");

            Assert.Equal("All (2)", info.Label);
            Assert.Equal("General", Assert.Single(info.Groups).Title);
            Assert.Equal("ByLayer", Row(info, "General", "Color").Value);
        }

        [Fact]
        public void ACircleAndAnArcGiveTheirCentreRadiusAnglesAndLengths()
        {
            DxfDatabase database = TestDrawings.Drawing(null, null, TestDrawings.Circle(10, 20, 5, handle: "A1"), TestDrawings.Arc(0, 0, 10, 0, 90, handle: "A2"));
            database.Auprec = 0;

            CadSelectionInfo circle = Describe(database, "A1");
            CadSelectionInfo arc = Describe(database, "A2");

            Assert.Equal("Circle (1)", circle.Label);
            Assert.Equal("10.0000", Row(circle, "Geometry", "Center X").Value);
            Assert.Equal("5.0000", Row(circle, "Geometry", "Radius").Value);
            Assert.Equal("10.0000", Row(circle, "Geometry", "Diameter").Value);
            Assert.Equal("31.4159", Row(circle, "Geometry", "Circumference").Value);
            Assert.Equal("Arc (1)", arc.Label);
            Assert.Equal("0", Row(arc, "Geometry", "Start angle").Value);
            Assert.Equal("90", Row(arc, "Geometry", "End angle").Value);
            Assert.Equal("15.7080", Row(arc, "Geometry", "Arc length").Value);
        }

        [Fact]
        public void AClosedPolylineGivesItsVerticesLengthAndArea()
        {
            DxfDatabase database = TestDrawings.Drawing(null, null, TestDrawings.Polyline("0", "A1", true, 0, 0, 0, 30, 0, 0, 30, 20, 0, 0, 20, 0));

            CadSelectionInfo info = Describe(database, "A1");

            Assert.Equal("Polyline (1)", info.Label);
            Assert.Equal("4", Row(info, "Geometry", "Vertices").Value);
            Assert.Equal("100.0000", Row(info, "Geometry", "Length").Value);
            Assert.Equal("600.0000", Row(info, "Geometry", "Area").Value);
            Assert.Equal("Yes", Row(info, "Misc", "Closed").Value);
        }

        [Fact]
        public void ATextAndAnInsertGiveTheirContentsHeightNamePositionAndScale()
        {
            string block = TestDrawings.Block("MARK", TestDrawings.Line(0, 0, 10, 0));
            DxfDatabase database = TestDrawings.Drawing(null, block, TestDrawings.Text(5, 6, 2.5, "45%%d", handle: "T1"), TestDrawings.Insert("MARK", 100, 200, 2.0, 90.0, handle: "B1"));
            database.Auprec = 0;

            CadSelectionInfo text = Describe(database, "T1");
            CadSelectionInfo insert = Describe(database, "B1");

            Assert.Equal("Text (1)", text.Label);
            Assert.Equal("45%%d", Row(text, "Text", "Contents").Value);
            Assert.Equal("2.5000", Row(text, "Text", "Height").Value);
            Assert.Equal("5.0000", Row(text, "Geometry", "Position X").Value);
            Assert.Equal("Block Reference (1)", insert.Label);
            Assert.Equal("MARK", Row(insert, "Misc", "Name").Value);
            Assert.Equal("90", Row(insert, "Misc", "Rotation").Value);
            Assert.Equal("2.0000", Row(insert, "Geometry", "Scale X").Value);
            Assert.Equal("200.0000", Row(insert, "Geometry", "Position Y").Value);
        }

        [Fact]
        public void AHatchGivesItsPatternScaleAndArea()
        {
            var database = new DxfDatabase("AC1018");
            var hatch = new DxfHatch();
            hatch.SetHatchPattern(DxfHatchPatternType.UserDefined, "_USER");
            hatch.PatternSpace = 3.175;
            hatch.AppendLoop(DxfHatchLoopTypes.External, new[] { new GeoPoint2(0, 0), new GeoPoint2(30, 0), new GeoPoint2(30, 20), new GeoPoint2(0, 20) }, null);
            database.ModelSpace.AppendEntity(hatch);

            CadSelectionInfo info = Describe(database, hatch.Handle);

            Assert.Equal("Hatch (1)", info.Label);
            Assert.Equal("_USER", Row(info, "Pattern", "Pattern name").Value);
            Assert.Equal("600.0000", Row(info, "Geometry", "Area").Value);
        }

        [Fact]
        public void AViewportGivesItsCentreSizeAndScale()
        {
            CadDocument document = LayoutTests.TwoViewports();
            CadSpace plan = document.Spaces.Single(s => s.Name == "Plan");
            string handle = document.GetDisplay(plan).Items.OfType<DisplayViewport2>().OrderBy(v => v.CenterX).First().Handle;

            CadSelectionInfo info = CadProperties.Describe(document, plan, new[] { handle });

            Assert.Equal("Viewport (1)", info.Label);
            Assert.Equal("100.0000", Row(info, "Geometry", "Center X").Value);
            Assert.Equal("150.0000", Row(info, "Geometry", "Width").Value);
            Assert.Equal("0.0500", Row(info, "Misc", "Custom scale").Value);
        }

        [Fact]
        public void AHandleTheDrawingHasNotIsPassedOver()
        {
            DxfDatabase database = TestDrawings.Drawing(null, null, TestDrawings.Line(0, 0, 10, 0, handle: "A1"));

            Assert.Equal("Line (1)", Describe(database, "A1", "FFFF").Label);
            Assert.Equal("No selection", Describe(database, "FFFF").Label);
        }
    }
}
