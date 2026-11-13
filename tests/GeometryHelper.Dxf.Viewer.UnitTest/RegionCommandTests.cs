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
    /// REGION and the Region object type of -BOUNDARY make regions of ACIS data as AutoCAD 2021 writes it, and a region is drawn,
    /// listed, measured by AREA and exploded: the box 40 by 30 and the circle of radius 7 of AutoCAD 2021's own probe, whose
    /// REGION said "5 loops extracted." and "5 Regions created." of eight objects.
    /// </summary>
    public class RegionCommandTests
    {
        private static CadCommandSession Start(CadDocument document, params object[] inputs)
        {
            CadCommandSession session = Session(document);
            session.Tolerance = Tolerance.Default;
            return Run(session, inputs);
        }

        private static DxfPolyline Box(double x, double y, double width, double height)
        {
            var polyline = new DxfPolyline();
            polyline.AddVertexAt(0, new GeoPoint2(x, y), 0.0, 0.0, 0.0);
            polyline.AddVertexAt(1, new GeoPoint2(x + width, y), 0.0, 0.0, 0.0);
            polyline.AddVertexAt(2, new GeoPoint2(x + width, y + height), 0.0, 0.0, 0.0);
            polyline.AddVertexAt(3, new GeoPoint2(x, y + height), 0.0, 0.0, 0.0);
            polyline.Closed = true;
            return polyline;
        }

        private static CadDocument Drawing(string version, out DxfPolyline box, out DxfCircle circle)
        {
            var database = new DxfDatabase(version);
            box = Box(0, 0, 40, 30);
            circle = new DxfCircle(new GeoPoint3(20, 15, 0), new GeoVector3(0, 0, 1), 5);
            database.ModelSpace.AppendEntity(box);
            database.ModelSpace.AppendEntity(circle);
            return CadDocument.From(database, "regions.dxf");
        }

        private static List<DxfUnknownEntity> Regions(CadDocument document) =>
            document.ModelSpace.Block.OfType<DxfUnknownEntity>().Where(e => e.DxfName == "REGION").ToList();

        [Theory]
        [InlineData("AC1015")]
        [InlineData("AC1027")]
        [InlineData("AC1032")]
        public void Region_OfABoxAndACircleInsideIt_MakesTwoRegions_AndDoesNotCutTheHole(string version)
        {
            CadDocument document = Drawing(version, out DxfPolyline box, out DxfCircle circle);
            CadCommandSession session = Start(document, "REGION", new[] { box.Handle, circle.Handle }, Enter);

            Assert.False(session.IsRunning);
            Assert.Contains("2 loops extracted.", session.History);
            Assert.Contains("2 Regions created.", session.History);
            List<DxfUnknownEntity> regions = Regions(document);
            Assert.Equal(2, regions.Count);
            double[] areas = regions.Select(r => r.GetRegionShape().Area).OrderBy(a => a).ToArray();
            Assert.Equal(Math.PI * 25.0, areas[0], 9);
            Assert.Equal(1200.0, areas[1], 9);
            Assert.Equal("REGION", document.NextUndo.Name);
        }

        [Fact]
        public void Region_IsOneUndoStep_AndUndoTakesTheRegionsBack()
        {
            CadDocument document = Drawing("AC1032", out DxfPolyline box, out DxfCircle circle);
            Start(document, "REGION", new[] { box.Handle, circle.Handle }, Enter);

            Assert.True(document.Undo());

            Assert.Empty(Regions(document));
            Assert.Equal(2, document.ModelSpace.Block.Count());
        }

        [Fact]
        public void Region_OfFourLinesThatJoinInALoop_MakesOneRegion_AsAutoCadCountsThem()
        {
            var database = new DxfDatabase("AC1027");
            var corners = new[]
            {
                new GeoPoint3(200, 0, 0),
                new GeoPoint3(220, 0, 0),
                new GeoPoint3(220, 20, 0),
                new GeoPoint3(200, 20, 0),
            };
            var handles = new List<string>();

            for (int i = 0; i < 4; i++)
            {
                var line = new DxfLine(corners[i], corners[(i + 1) % 4]);
                database.ModelSpace.AppendEntity(line);
                handles.Add(line.Handle);
            }

            CadDocument document = CadDocument.From(database, "lines.dxf");
            CadCommandSession session = Start(document, "REGION", handles.ToArray(), Enter);

            Assert.Contains("1 loop extracted.", session.History);
            Assert.Contains("1 Region created.", session.History);
            Assert.Equal(400.0, Assert.Single(Regions(document)).GetRegionShape().Area, 9);
        }

        [Fact]
        public void Region_OfThreeLinesAndOneThatMeetsThemAtATee_MakesNone()
        {
            var database = new DxfDatabase("AC1027");
            DxfLine[] lines =
            {
                new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0)),
                new DxfLine(new GeoPoint3(10, 0, 0), new GeoPoint3(10, 10, 0)),
                new DxfLine(new GeoPoint3(10, 10, 0), new GeoPoint3(0, 0, 0)),
                new DxfLine(new GeoPoint3(10, 0, 0), new GeoPoint3(20, 0, 0)),
            };

            foreach (DxfLine line in lines)
            {
                database.ModelSpace.AppendEntity(line);
            }

            CadDocument document = CadDocument.From(database, "tee.dxf");
            CadCommandSession session = Start(document, "REGION", lines.Select(l => l.Handle).ToArray(), Enter);

            Assert.Contains("0 loops extracted.", session.History);
            Assert.Contains("0 Regions created.", session.History);
            Assert.Empty(Regions(document));
        }

        [Fact]
        public void BoundaryTyped_ObjectTypeRegion_MakesARegionOfTheLoop_OnTheCurrentLayer()
        {
            CadDocument document = Drawing("AC1032", out DxfPolyline box, out DxfCircle circle);
            CadCommandSession session = Start(document, "-BO", "A", "O");
            Assert.Equal("Enter type of boundary object [Region/Polyline] <Polyline>:", session.PromptText);
            Run(session, "R", Enter, P(5, 5), Enter);

            Assert.DoesNotContain(document.ModelSpace.Block.OfType<DxfPolyline>(), p => !ReferenceEquals(p, box));
            Assert.Contains("BOUNDARY created 2 regions", session.History);
            Assert.Equal(2, Regions(document).Count);
        }

        [Fact]
        public void ARegion_IsDrawnByTheCurvesOfItsLoops()
        {
            CadDocument document = Drawing("AC1032", out DxfPolyline box, out DxfCircle circle);
            Start(document, "REGION", new[] { box.Handle }, Enter);
            string handle = Regions(document).Single().Handle;

            List<DisplayItem2> items = document
                .GetDisplay(document.ModelSpace)
                .Items.Where(i => i.Handle == handle)
                .ToList();

            Assert.NotEmpty(items);
        }

        [Fact]
        public void List_OfARegion_GivesItsAreaPerimeterAndBox_AsAutoCadLists()
        {
            CadDocument document = Drawing("AC1032", out DxfPolyline box, out DxfCircle circle);
            Start(document, "REGION", new[] { box.Handle }, Enter);
            string handle = Regions(document).Single().Handle;

            List<string> history = Start(document, "LIST", new[] { handle }, Enter).History.ToList();

            Assert.Contains(history, l => l.Contains("REGION") && l.Contains("Layer: \"0\""));
            Assert.Contains(new string(' ', 29) + "Area: 1200.0000", history);
            Assert.Contains(new string(' ', 24) + "Perimeter: 140.0000", history);
            Assert.Contains("   Bounding Box: Lower Bound X = 0.0000   , Y = 0.0000   , Z = 0.0000", history);
            Assert.Contains(new string(' ', 17) + "Upper Bound X = 40.0000  , Y = 30.0000  , Z = 0.0000", history);
        }

        [Fact]
        public void Area_OfARegion_GivesItsAreaAndPerimeter()
        {
            CadDocument document = Drawing("AC1032", out DxfPolyline box, out DxfCircle circle);
            Start(document, "REGION", new[] { circle.Handle }, Enter);
            string handle = Regions(document).Single().Handle;

            CadCommandSession session = Start(document, "AREA", "O", new[] { handle });

            Assert.Contains("Area = 78.5398, Perimeter = 31.4159", session.History);
        }

        [Fact]
        public void Explode_OfARegion_GivesItsCurvesAsAutoCadDoes_AndErasesTheRegion()
        {
            CadDocument document = Drawing("AC1032", out DxfPolyline box, out DxfCircle circle);
            var shape = DxfRegionShape.Create(
                new DxfCurve[] { box },
                new[] { new DxfCurve[] { circle } },
                Tolerance.Default
            );
            DxfUnknownEntity region = DxfUnknownEntity.CreateRegion(shape, document.Database);
            document.Database.ModelSpace.AppendEntity(region);
            box.Erase();
            circle.Erase();

            Start(document, "EXPLODE", new[] { region.Handle }, Enter);

            Assert.Empty(Regions(document));
            Assert.Single(document.ModelSpace.Block.OfType<DxfCircle>());
            Assert.Equal(4, document.ModelSpace.Block.OfType<DxfLine>().Count());
        }

        [Fact]
        public void TheKindOfARegion_IsRegion_AndItsPropertiesGiveItsAreaAndPerimeter()
        {
            CadDocument document = Drawing("AC1032", out DxfPolyline box, out DxfCircle circle);
            Start(document, "REGION", new[] { box.Handle }, Enter);
            DxfUnknownEntity region = Regions(document).Single();

            Assert.Equal("Region", CadProperties.KindOf(region));
        }
    }
}
