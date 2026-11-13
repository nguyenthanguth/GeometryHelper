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
    /// UNION, SUBTRACT and INTERSECT of regions, and MOVE, ROTATE, SCALE, MIRROR, COPY and STRETCH of one, as AutoCAD 2021 does
    /// them: a box 40 by 30 on layer LA, red and 0.50 mm, and a circle of radius 10 on layer LB, yellow and 1.00 mm, both made
    /// regions; the areas and the messages are those AutoCAD gave for the same.
    /// </summary>
    public class RegionBooleanCommandTests
    {
        private static CadCommandSession Start(CadDocument document, params object[] inputs)
        {
            CadCommandSession session = Session(document);
            session.Tolerance = Tolerance.Default;
            return Run(session, inputs);
        }

        private static DxfRegionShape Box(double x, double y, double w, double h) =>
            DxfRegionShape.Create(
                new DxfCurve[]
                {
                    new DxfLine(new GeoPoint3(x, y, 0), new GeoPoint3(x + w, y, 0)),
                    new DxfLine(new GeoPoint3(x + w, y, 0), new GeoPoint3(x + w, y + h, 0)),
                    new DxfLine(new GeoPoint3(x + w, y + h, 0), new GeoPoint3(x, y + h, 0)),
                    new DxfLine(new GeoPoint3(x, y + h, 0), new GeoPoint3(x, y, 0)),
                },
                null,
                Tolerance.Default
            );

        private static DxfRegionShape Disc(double x, double y, double radius) =>
            DxfRegionShape.Create(
                new DxfCurve[] { new DxfCircle(new GeoPoint3(x, y, 0), new GeoVector3(0, 0, 1), radius) },
                null,
                Tolerance.Default
            );

        private static DxfUnknownEntity Add(
            DxfDatabase database,
            DxfRegionShape shape,
            string layer,
            int color,
            int weight
        )
        {
            DxfUnknownEntity region = DxfUnknownEntity.CreateRegion(shape, database);
            region.Layer = layer;
            region.ColorIndex = color;
            region.LineWeight = (DxfLineWeight)weight;
            database.ModelSpace.AppendEntity(region);
            return region;
        }

        /// <summary>The box on LA and the circle on LB, in a drawing of the version; the circle about x.</summary>
        private static CadDocument Drawing(
            string version,
            double circleX,
            out DxfUnknownEntity box,
            out DxfUnknownEntity disc
        )
        {
            var database = new DxfDatabase(version);
            database.LayerTable.Add(new DxfLayerTableRecord { Name = "LA" });
            database.LayerTable.Add(new DxfLayerTableRecord { Name = "LB" });
            box = Add(database, Box(0, 0, 40, 30), "LA", 1, 50);
            disc = Add(database, Disc(circleX, 15, 10), "LB", 2, 100);
            return CadDocument.From(database, "booleans.dxf");
        }

        /// <summary>The box on LA and the circle on LB as <see cref="Drawing"/> makes them, and a circle of radius 4 on LA five above them.</summary>
        private static CadDocument DrawingWithAnElevatedCircle(
            out DxfUnknownEntity box,
            out DxfUnknownEntity disc,
            out DxfUnknownEntity high
        )
        {
            var database = new DxfDatabase("AC1032");
            database.LayerTable.Add(new DxfLayerTableRecord { Name = "LA" });
            database.LayerTable.Add(new DxfLayerTableRecord { Name = "LB" });
            box = Add(database, Box(0, 0, 40, 30), "LA", 1, 50);
            disc = Add(database, Disc(40, 15, 10), "LB", 2, 100);
            high = Add(database, Disc(20, 15, 4), "LA", 3, 25);
            high.TransformBy(GeoTransform3.Translation(new GeoVector3(0, 0, 5)));
            return CadDocument.From(database, "planes.dxf");
        }

        private static List<DxfUnknownEntity> Regions(CadDocument document) =>
            document
                .ModelSpace.Block.OfType<DxfUnknownEntity>()
                .Where(e => e.DxfName == "REGION" && !e.IsErased)
                .ToList();

        [Theory]
        [InlineData("AC1015")]
        [InlineData("AC1032")]
        public void Union_OfABoxAndACircleOnItsEdge_LeavesOneRegion_ThatKeepsTheFirstsHandleLayerAndLineweight_ByLayerInColour(
            string version
        )
        {
            CadDocument document = Drawing(version, 40, out DxfUnknownEntity box, out DxfUnknownEntity disc);
            string handle = box.Handle;

            CadCommandSession session = Start(document, "UNION", new[] { box.Handle }, Enter);
            Assert.Contains("At least 2 solids, surfaces, or coplanar regions must be selected.", session.History);
            session = Start(document, "UNION", new[] { box.Handle, disc.Handle }, Enter);

            Assert.False(session.IsRunning);
            DxfUnknownEntity only = Assert.Single(Regions(document));
            Assert.Equal(handle, only.Handle);
            Assert.Equal("LA", only.Layer);
            Assert.Equal((DxfLineWeight)50, only.LineWeight);
            Assert.Equal(256, only.ColorIndex);
            Assert.Equal(1357.079632679, only.GetRegionShape().Area, 6);
            Assert.Equal(151.415926536, only.GetRegionShape().Perimeter, 6);
            Assert.Equal("UNION", document.NextUndo.Name);
        }

        [Fact]
        public void Union_IsOneUndoStep_AndUndoBringsBothRegionsBack()
        {
            CadDocument document = Drawing("AC1032", 40, out DxfUnknownEntity box, out DxfUnknownEntity disc);
            Start(document, "UNION", new[] { box.Handle, disc.Handle }, Enter);

            Assert.True(document.Undo());

            List<DxfUnknownEntity> regions = Regions(document);
            Assert.Equal(2, regions.Count);
            Assert.Equal(
                new[] { 314.159265359, 1200.0 },
                regions.Select(r => Math.Round(r.GetRegionShape().Area, 9)).OrderBy(a => a).ToArray()
            );
            Assert.Equal(1, regions.Single(r => r.Layer == "LA").ColorIndex);
        }

        [Fact]
        public void Union_OfTwoRegionsFarApart_IsOneRegionOfTwoFaces_TheLowestHandleKept()
        {
            CadDocument document = Drawing("AC1032", 100, out DxfUnknownEntity box, out DxfUnknownEntity disc);

            Start(document, "UNION", new[] { disc.Handle, box.Handle }, Enter);

            DxfUnknownEntity only = Assert.Single(Regions(document));
            Assert.Equal(box.Handle, only.Handle);
            Assert.Equal(1514.159265359, only.GetRegionShape().Area, 6);
            Assert.Equal(2, only.GetRegionShape().Loops.Count);
        }

        [Theory]
        [InlineData("AC1015")]
        [InlineData("AC1032")]
        public void Explode_OfARegionOfTwoFaces_GivesARegionForEachFace_AsAutoCadsExplodeDoes(string version)
        {
            CadDocument document = Drawing(version, 100, out DxfUnknownEntity box, out DxfUnknownEntity disc);
            Start(document, "UNION", new[] { box.Handle, disc.Handle }, Enter);
            DxfUnknownEntity two = Assert.Single(Regions(document));

            Start(document, "EXPLODE", new[] { two.Handle }, Enter);

            List<DxfUnknownEntity> regions = Regions(document);
            Assert.Equal(2, regions.Count);
            Assert.Equal(
                new[] { 314.159265359, 1200.0 },
                regions.Select(r => Math.Round(r.GetRegionShape().Area, 9)).OrderBy(a => a).ToArray()
            );
            Assert.Equal("EXPLODE", document.NextUndo.Name);
        }

        [Fact]
        public void Union_AlongWithACircleThatIsNotARegion_PassesItOver_AndSaysTwoAreNeeded()
        {
            CadDocument document = Drawing("AC1032", 40, out DxfUnknownEntity box, out _);
            var circle = new DxfCircle(new GeoPoint3(40, 15, 0), new GeoVector3(0, 0, 1), 10);
            document.Database.ModelSpace.AppendEntity(circle);

            CadCommandSession session = Start(document, "UNION", new[] { box.Handle, circle.Handle }, Enter);

            Assert.Contains("At least 2 solids, surfaces, or coplanar regions must be selected.", session.History);
            Assert.Equal(2, Regions(document).Count);
        }

        [Fact]
        public void Union_WithNothingChosen_EndsAtOnce_WithoutAWord()
        {
            CadDocument document = Drawing("AC1032", 40, out _, out _);

            CadCommandSession session = Start(document, "UNION", new string[0], Enter);

            Assert.False(session.IsRunning);
            Assert.DoesNotContain(session.History, l => l.StartsWith("At least", StringComparison.Ordinal));
            Assert.Equal(2, Regions(document).Count);
        }

        [Fact]
        public void Union_OfRegionsInPlanesOneAboveTheOther_SaysTheyAreNotCoplanar_AndLeavesThem()
        {
            CadDocument document = Drawing("AC1032", 40, out DxfUnknownEntity box, out DxfUnknownEntity disc);
            document
                .ModelSpace.Block.OfType<DxfUnknownEntity>()
                .Last()
                .TransformBy(GeoTransform3.Translation(new GeoVector3(0, 0, 5)));

            CadCommandSession session = Start(document, "UNION", new[] { box.Handle, disc.Handle }, Enter);

            Assert.Contains("At least 2 solids, surfaces, or coplanar regions must be selected.", session.History);
            Assert.Equal(2, Regions(document).Count);
        }

        [Fact]
        public void Union_OfABoxAndACircleAndAnElevatedCircleChosenFirst_CombinesTheTwoOfOnePlane_AndLeavesTheOther()
        {
            CadDocument document = DrawingWithAnElevatedCircle(
                out DxfUnknownEntity box,
                out DxfUnknownEntity disc,
                out DxfUnknownEntity high
            );

            Start(document, "UNION", new[] { high.Handle, box.Handle, disc.Handle }, Enter);

            List<DxfUnknownEntity> regions = Regions(document);
            Assert.Equal(2, regions.Count);
            Assert.Equal(
                new[] { 50.265482457, 1357.079632679 },
                regions.Select(r => Math.Round(r.GetRegionShape().Area, 9)).OrderBy(a => a).ToArray()
            );
            Assert.Equal(3, regions.Single(r => r.Handle == high.Handle).ColorIndex);
        }

        [Fact]
        public void Subtract_OfACircleOutOfThePlane_LeavesItAlone_AsAutoCadDoes()
        {
            CadDocument document = DrawingWithAnElevatedCircle(
                out DxfUnknownEntity box,
                out DxfUnknownEntity disc,
                out DxfUnknownEntity high
            );

            Start(document, "SUBTRACT", new[] { box.Handle }, Enter, new[] { disc.Handle, high.Handle }, Enter);

            List<DxfUnknownEntity> regions = Regions(document);
            Assert.Equal(2, regions.Count);
            Assert.Equal(
                new[] { 50.265482457, 1042.920367321 },
                regions.Select(r => Math.Round(r.GetRegionShape().Area, 9)).OrderBy(a => a).ToArray()
            );
        }

        [Fact]
        public void Subtract_TakesTheCircleFromTheBox_AsAutoCadDoes_WithItsTwoPrompts()
        {
            CadDocument document = Drawing("AC1032", 40, out DxfUnknownEntity box, out DxfUnknownEntity disc);
            CadCommandSession session = Session(document);
            session.Tolerance = Tolerance.Default;

            Run(session, "SUBTRACT");
            Assert.Contains("Select solids, surfaces, and regions to subtract from ..", session.History);
            Assert.Equal("Select objects:", session.PromptText);
            Run(session, new[] { box.Handle }, Enter);
            Assert.Contains("Select solids, surfaces, and regions to subtract ..", session.History);
            Assert.Equal("Select objects:", session.PromptText);
            Run(session, new[] { disc.Handle }, Enter);

            Assert.False(session.IsRunning);
            DxfUnknownEntity only = Assert.Single(Regions(document));
            Assert.Equal(box.Handle, only.Handle);
            Assert.Equal(1042.920367321, only.GetRegionShape().Area, 6);
            Assert.Equal(151.415926536, only.GetRegionShape().Perimeter, 6);
            Assert.Equal(256, only.ColorIndex);
            Assert.Equal("SUBTRACT", document.NextUndo.Name);
        }

        [Fact]
        public void Subtract_OfACircleInsideTheBox_LeavesTheBoxWithAHole_AndOfTheBoxFromTheCircle_Nothing()
        {
            CadDocument document = Drawing("AC1027", 20, out DxfUnknownEntity box, out DxfUnknownEntity disc);
            document.Database.ModelSpace.OfType<DxfUnknownEntity>().Last().SetRegionShape(Disc(20, 15, 5));
            Start(document, "SUBTRACT", new[] { box.Handle }, Enter, new[] { disc.Handle }, Enter);

            DxfRegionShape holed = Assert.Single(Regions(document)).GetRegionShape();
            Assert.Equal(1121.46018366, holed.Area, 6);
            Assert.Equal(new[] { true, false }, holed.Loops.Select(l => l.IsHole).ToArray());

            CadDocument other = Drawing("AC1027", 20, out DxfUnknownEntity bigger, out DxfUnknownEntity smaller);
            other.Database.ModelSpace.OfType<DxfUnknownEntity>().Last().SetRegionShape(Disc(20, 15, 5));
            CadCommandSession session = Start(
                other,
                "SUBTRACT",
                new[] { smaller.Handle },
                Enter,
                new[] { bigger.Handle },
                Enter
            );

            Assert.Contains("Null region created - deleted", session.History);
            Assert.Empty(Regions(other));
        }

        [Fact]
        public void Subtract_ChosenWithNoRegionInTheFirstSet_SaysSo_AndWithNothingChosen_EndsInSilence()
        {
            CadDocument document = Drawing("AC1032", 40, out DxfUnknownEntity box, out _);
            var circle = new DxfCircle(new GeoPoint3(40, 15, 0), new GeoVector3(0, 0, 1), 10);
            document.Database.ModelSpace.AppendEntity(circle);

            CadCommandSession session = Start(document, "SUBTRACT", new[] { circle.Handle }, Enter);

            Assert.Contains("No solids, surfaces, or regions selected.", session.History);
            Assert.False(session.IsRunning);

            session = Start(document, "SUBTRACT", new string[0], Enter);
            Assert.False(session.IsRunning);
            Assert.DoesNotContain(session.History, l => l.StartsWith("No solids", StringComparison.Ordinal));

            session = Start(document, "SUBTRACT", new[] { box.Handle }, Enter, new[] { circle.Handle }, Enter);
            Assert.Contains("No solids, surfaces, or regions selected.", session.History);
            Assert.Equal(2, Regions(document).Count);
        }

        [Fact]
        public void Subtract_OfARegionFarFromTheBox_LeavesTheBoxAsItIs_AndErasesTheTool()
        {
            CadDocument document = Drawing("AC1032", 100, out DxfUnknownEntity box, out DxfUnknownEntity disc);

            Start(document, "SUBTRACT", new[] { box.Handle }, Enter, new[] { disc.Handle }, Enter);

            DxfUnknownEntity only = Assert.Single(Regions(document));
            Assert.Equal(1200.0, only.GetRegionShape().Area, 9);
            Assert.Equal(box.Handle, only.Handle);
        }

        [Fact]
        public void Intersect_OfTheBoxAndTheCircleOnItsEdge_IsTheHalfCircleOfTheBox_AndOfRegionsApart_Null()
        {
            CadDocument document = Drawing("AC1032", 40, out DxfUnknownEntity box, out DxfUnknownEntity disc);

            Start(document, "INTERSECT", new[] { box.Handle, disc.Handle }, Enter);

            DxfRegionShape half = Assert.Single(Regions(document)).GetRegionShape();
            Assert.Equal(157.0796326795, half.Area, 6);
            Assert.Equal(51.415926536, half.Perimeter, 6);

            CadDocument apart = Drawing("AC1032", 100, out DxfUnknownEntity a, out DxfUnknownEntity b);
            CadCommandSession session = Start(apart, "IN", new[] { a.Handle, b.Handle }, Enter);

            Assert.Contains("Null region created - deleted", session.History);
            Assert.Empty(Regions(apart));
        }

        [Fact]
        public void TheAliasesOfAcadPgp_RunTheThreeCommands()
        {
            CadDocument document = Drawing("AC1032", 40, out DxfUnknownEntity box, out DxfUnknownEntity disc);

            Start(document, "UNI", new[] { box.Handle, disc.Handle }, Enter);

            Assert.Equal("UNION", document.NextUndo.Name);
            CadCommandSession session = Start(Drawing("AC1032", 40, out _, out _), "SU");
            Assert.Equal("SUBTRACT", session.History.First(l => !l.StartsWith("Command", StringComparison.Ordinal)));
            session = Start(Drawing("AC1032", 40, out _, out _), "IN");
            Assert.Equal("INTERSECT", session.History.First(l => !l.StartsWith("Command", StringComparison.Ordinal)));
        }

        [Theory]
        [InlineData("AC1015")]
        [InlineData("AC1027")]
        [InlineData("AC1032")]
        public void Move_Rotate_Scale_AndMirror_OfARegion_MakeItTheRegionAutoCadLeaves(string version)
        {
            CadDocument document = Drawing(version, 100, out DxfUnknownEntity box, out _);
            string handle = box.Handle;

            Start(document, "MOVE", new[] { handle }, Enter, P(0, 0), P(100, 50));
            DxfRegionShape moved = ((DxfUnknownEntity)document.FindEntity(handle)).GetRegionShape();
            Assert.Equal(1200.0, moved.Area, 9);
            Assert.Equal(100.0, moved.Loops[0].Curves.OfType<DxfLine>().First().StartPoint.X, 9);
            Assert.Equal(50.0, moved.Loops[0].Curves.OfType<DxfLine>().First().StartPoint.Y, 9);
            Assert.Equal("MOVE", document.NextUndo.Name);

            Start(document, "SCALE", new[] { handle }, Enter, P(100, 50), "2");
            DxfRegionShape scaled = ((DxfUnknownEntity)document.FindEntity(handle)).GetRegionShape();
            Assert.Equal(4800.0, scaled.Area, 6);

            Start(document, "ROTATE", new[] { handle }, Enter, P(100, 50), "90");
            DxfRegionShape turned = ((DxfUnknownEntity)document.FindEntity(handle)).GetRegionShape();
            Assert.Equal(4800.0, turned.Area, 6);
            DxfLine side = turned.Loops[0].Curves.OfType<DxfLine>().First();
            Assert.Equal(100.0, side.StartPoint.X, 6);
            Assert.Equal(50.0, side.StartPoint.Y, 6);
            Assert.Equal(100.0, side.EndPoint.X, 6);
            Assert.Equal(130.0, side.EndPoint.Y, 6);

            // MIRROR keeps the source unless told to delete it: the copy is the mirrored region, with its normal down.
            Start(document, "MIRROR", new[] { handle }, Enter, P(100, 0), P(100, 10), Enter);
            DxfUnknownEntity mirroredRegion = Regions(document)
                .Single(r => r.Layer == "LA" && r.GetRegionShape().Normal.Z < 0.0);
            DxfRegionShape mirrored = mirroredRegion.GetRegionShape();
            Assert.Equal(4800.0, mirrored.Area, 6);
            Assert.Equal(-1.0, mirrored.Normal.Z, 9);
            Assert.Equal("MIRROR", document.NextUndo.Name);
            Assert.True(document.Undo());
            Assert.Equal(2, Regions(document).Count);
        }

        [Theory]
        [InlineData("AC1015")]
        [InlineData("AC1032")]
        public void Copy_OfARegion_MakesAnotherOfItsOwn_AndMirrorWithACopyKeepsTheFirst(string version)
        {
            CadDocument document = Drawing(version, 100, out DxfUnknownEntity box, out _);

            Start(document, "COPY", new[] { box.Handle }, Enter, P(0, 0), P(0, 100), Enter);

            List<DxfUnknownEntity> regions = Regions(document);
            Assert.Equal(3, regions.Count);
            DxfUnknownEntity copy = regions.Single(r => r.Handle != box.Handle && r.Layer == "LA");
            Assert.Equal(1200.0, copy.GetRegionShape().Area, 9);
            Assert.Equal(100.0, copy.GetRegionShape().Loops[0].Curves.OfType<DxfLine>().First().StartPoint.Y, 9);
            Assert.Equal(0.0, box.GetRegionShape().Loops[0].Curves.OfType<DxfLine>().First().StartPoint.Y, 9);
            Assert.Equal("COPY", document.NextUndo.Name);

            Start(document, "MIRROR", new[] { copy.Handle }, Enter, P(0, 0), P(10, 0), "N");
            Assert.Equal(4, Regions(document).Count);
        }

        [Fact]
        public void Stretch_OfARegion_ByACrossingWindowOverOneCorner_MovesTheWholeRegion_AsAutoCadsStretchPointsDo()
        {
            CadDocument document = Drawing("AC1032", 100, out DxfUnknownEntity box, out _);

            Start(document, "STRETCH", P(45, -5), P(35, 5), Enter, P(0, 0), P(0, 20));

            DxfLine first = box.GetRegionShape().Loops[0].Curves.OfType<DxfLine>().First();
            Assert.Equal(20.0, first.StartPoint.Y, 9);
            Assert.Equal(1200.0, box.GetRegionShape().Area, 9);
        }

        [Fact]
        public void Stretch_OfARegion_ByAWindowThatHoldsNoStretchPoint_LeavesItWhereItIs()
        {
            CadDocument document = Drawing("AC1032", 100, out DxfUnknownEntity box, out _);

            Start(document, "STRETCH", P(15, -5), P(25, 40), Enter, P(0, 0), P(0, 20));

            Assert.Equal(0.0, box.GetRegionShape().Loops[0].Curves.OfType<DxfLine>().First().StartPoint.Y, 9);
        }
    }
}
