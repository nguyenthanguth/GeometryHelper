using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using GeometryHelper.UnitTest.Dwg;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// A REGION written and read: <see cref="DxfRegionShape"/> from closed loops, <see cref="DxfUnknownEntity.CreateRegion"/>
    /// for a drawing of R2000 (SAT in the groups of the entity) and of R2018 (SAB in the section ACDSDATA), the region read back
    /// from the saved text, the regions AutoCAD wrote in its own DXF, and <see cref="DxfEntity.Explode"/>.
    /// </summary>
    public class DxfRegionTests
    {
        private static DxfPolyline Rectangle(double x, double y, double width, double height)
        {
            var polyline = new DxfPolyline();
            polyline.AddVertexAt(0, new GeoPoint2(x, y), 0.0, 0.0, 0.0);
            polyline.AddVertexAt(1, new GeoPoint2(x + width, y), 0.0, 0.0, 0.0);
            polyline.AddVertexAt(2, new GeoPoint2(x + width, y + height), 0.0, 0.0, 0.0);
            polyline.AddVertexAt(3, new GeoPoint2(x, y + height), 0.0, 0.0, 0.0);
            polyline.Closed = true;
            return polyline;
        }

        private static DxfCircle Circle(double x, double y, double radius) =>
            new DxfCircle(new GeoPoint3(x, y, 0), new GeoVector3(0, 0, 1), radius);

        private static DxfRegionShape BoxWithAHole() =>
            DxfRegionShape.Create(
                new DxfCurve[] { Rectangle(0, 0, 40, 30) },
                new[] { new DxfCurve[] { Circle(20, 15, 5) } },
                Tolerance.Default
            );

        private const string SectionPattern = @"\nACDSDATA\r?\n";

        private const string RecordPattern = @"\r?\n  0\r?\nACDSRECORD";

        private static DxfDatabase Reopen(DxfDatabase db) => DxfDatabase.Parse(db.ToText());

        [Fact]
        public void ABoxWithACircularHole_HasTheAreaAndPerimeterAutoCadGives()
        {
            DxfRegionShape shape = BoxWithAHole();

            Assert.Equal(1121.4601836602551, shape.Area, 9);
            Assert.Equal(171.41592653589794, shape.Perimeter, 9);
            Assert.Equal(new[] { true, false }, shape.Loops.Select(l => l.IsHole).ToArray());
            Assert.Equal(1.0, shape.Normal.Z, 12);
        }

        [Fact]
        public void ARegionMadeForR2018_NamesItsDataByAGuid_AndTheDataIsInTheSectionAcdsData()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            DxfUnknownEntity region = DxfUnknownEntity.CreateRegion(BoxWithAHole(), db);

            db.ModelSpace.AppendEntity(region);
            DxfDatabase saved = Reopen(db);

            string text = db.ToText();
            Assert.Single(Regex.Matches(text, SectionPattern));
            Assert.Single(Regex.Matches(text, RecordPattern));
            Assert.Matches(@"\n\{[0-9a-f]{8}-([0-9a-f]{4}-){3}[0-9a-f]{12}\}\r?\n", text);
            DxfUnknownEntity read = Assert.Single(saved.ModelSpace.OfType<DxfUnknownEntity>(), e =>
                e.DxfName == "REGION"
            );
            DxfRegionShape shape = read.GetRegionShape();
            Assert.NotNull(shape);
            Assert.Equal(1121.4601836602551, shape.Area, 9);
            Assert.Equal(171.41592653589794, shape.Perimeter, 9);
        }

        [Fact]
        public void TwoRegionsMadeForR2018_ShareOneSection_EachWithItsRecord()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            DxfRegionShape plain = DxfRegionShape.Create(new DxfCurve[] { Circle(0, 0, 7) }, null, Tolerance.Default);

            db.ModelSpace.AppendEntity(DxfUnknownEntity.CreateRegion(BoxWithAHole(), db));
            db.ModelSpace.AppendEntity(DxfUnknownEntity.CreateRegion(plain, db));

            DxfDatabase saved = Reopen(db);
            string text = db.ToText();
            Assert.Single(Regex.Matches(text, SectionPattern));
            Assert.Equal(2, Regex.Matches(text, RecordPattern).Count);
            double[] areas = saved
                .ModelSpace.OfType<DxfUnknownEntity>()
                .Where(e => e.DxfName == "REGION")
                .Select(e => e.GetRegionShape().Area)
                .OrderBy(a => a)
                .ToArray();
            Assert.Equal(153.93804002589985, areas[0], 9);
            Assert.Equal(1121.4601836602551, areas[1], 9);
        }

        [Fact]
        public void ARegionMadeForR2000_HoldsItsSatInGroup1_TheCipherAndTheCaretsAsAutoCadWritesThem()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2000);
            DxfUnknownEntity region = DxfUnknownEntity.CreateRegion(BoxWithAHole(), db);

            db.ModelSpace.AppendEntity(region);

            List<DxfTypedValue> ones = region.Groups.Where(g => g.TypeCode == 1).ToList();
            Assert.Contains(region.Groups, g => g.TypeCode == 70 && g.Value.Trim() == "1");
            // The head of every region AutoCAD 2021 wrote for R2000: "400 N 1 0", then the product "16 Autodesk AutoCAD 20 ASM ...".
            Assert.StartsWith("koo ", ones[0].Value, StringComparison.Ordinal);
            Assert.StartsWith("ni ^ *+0;:,4 ^ *+0\\^ [", ones[1].Value, StringComparison.Ordinal);
            DxfRegionShape shape = Reopen(db)
                .ModelSpace.OfType<DxfUnknownEntity>()
                .Single(e => e.DxfName == "REGION")
                .GetRegionShape();
            Assert.Equal(1121.4601836602551, shape.Area, 9);
            Assert.DoesNotContain("ACDSDATA", db.ToText(), StringComparison.Ordinal);
        }

        [Fact]
        public void ARegionMadeForOneVersion_IsRefusedByADrawingOfAnother()
        {
            DxfDatabase old = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2000);
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            DxfUnknownEntity region = DxfUnknownEntity.CreateRegion(BoxWithAHole(), old);

            Assert.Throws<ArgumentException>(() => db.ModelSpace.AppendEntity(region));
        }

        [Fact]
        public void ARegionJustAddedToADrawingOfR2018_IsReadAtOnce_ItsDataInTheStoreItHasMade()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            DxfUnknownEntity first = DxfUnknownEntity.CreateRegion(BoxWithAHole(), db);
            db.ModelSpace.AppendEntity(first);
            DxfUnknownEntity second = DxfUnknownEntity.CreateRegion(
                DxfRegionShape.Create(new DxfCurve[] { Circle(0, 0, 7) }, null, Tolerance.Default),
                db
            );
            db.ModelSpace.AppendEntity(second);

            Assert.Equal(1121.4601836602551, first.GetRegionShape().Area, 9);
            Assert.Equal(153.93804002589985, second.GetRegionShape().Area, 9);
        }

        [Fact]
        public void ARegionForADrawingOfR12_IsRefused_ForR2000IsTheFirstThatHasRegions()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R12);

            Assert.Throws<NotSupportedException>(() => DxfUnknownEntity.CreateRegion(BoxWithAHole(), db));
        }

        [Fact]
        public void ARegionExploded_GivesTheCurvesOfItsLoops_AHoleFirst_WithItsLook()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            DxfUnknownEntity region = DxfUnknownEntity.CreateRegion(BoxWithAHole(), db);
            db.ModelSpace.AppendEntity(region);
            region.ColorIndex = 3;

            IReadOnlyList<DxfEntity> parts = Reopen(db)
                .ModelSpace.OfType<DxfUnknownEntity>()
                .Single(e => e.DxfName == "REGION")
                .Explode();

            Assert.Equal(
                new[] { "DxfCircle", "DxfLine", "DxfLine", "DxfLine", "DxfLine" },
                parts.Select(p => p.GetType().Name).ToArray()
            );
            Assert.Equal(5.0, ((DxfCircle)parts[0]).Radius, 9);
            Assert.All(parts, p => Assert.Equal(3, p.ColorIndex));
            Assert.Equal(Math.PI * 25.0, ((DxfCircle)parts[0]).Area, 9);
            db.ModelSpace.AppendEntity(parts[0]);
        }

        [Fact]
        public void ARegionOfAStadium_ExplodesIntoTwoLinesAndTwoArcs()
        {
            var stadium = new DxfPolyline();
            stadium.AddVertexAt(0, new GeoPoint2(0, 0), 0.0, 0.0, 0.0);
            stadium.AddVertexAt(1, new GeoPoint2(30, 0), 1.0, 0.0, 0.0);
            stadium.AddVertexAt(2, new GeoPoint2(30, 10), 0.0, 0.0, 0.0);
            stadium.AddVertexAt(3, new GeoPoint2(0, 10), 1.0, 0.0, 0.0);
            stadium.Closed = true;
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);
            DxfRegionShape shape = DxfRegionShape.Create(new DxfCurve[] { stadium }, null, Tolerance.Default);
            DxfUnknownEntity region = DxfUnknownEntity.CreateRegion(shape, db);
            db.ModelSpace.AppendEntity(region);

            IReadOnlyList<DxfEntity> parts = Reopen(db)
                .ModelSpace.OfType<DxfUnknownEntity>()
                .Single(e => e.DxfName == "REGION")
                .Explode();

            Assert.Equal(
                new[] { "DxfLine", "DxfArc", "DxfLine", "DxfArc" },
                parts.Select(p => p.GetType().Name).ToArray()
            );
            var arc = (DxfArc)parts[1];
            Assert.Equal(5.0, arc.Radius, 9);
            Assert.Equal(Math.PI, arc.TotalAngle, 9);
            Assert.Equal(30.0, arc.Center.X, 9);
            Assert.Equal(378.53981633974485, shape.Area, 9);
        }

        [Fact]
        public void LinesAndAnArcGivenOutOfOrder_AreJoinedIntoTheLoopOfARegion()
        {
            var curves = new DxfCurve[]
            {
                new DxfLine(new GeoPoint3(20, 20, 0), new GeoPoint3(0, 20, 0)),
                new DxfArc(new GeoPoint3(20, 10, 0), 10.0, -Math.PI / 2, Math.PI / 2),
                new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(0, 20, 0)),
                new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(20, 0, 0)),
            };

            DxfRegionShape shape = DxfRegionShape.Create(curves, null, Tolerance.Default);

            Assert.Equal(557.07963267948969, shape.Area, 9);
            Assert.Equal(91.415926535897938, shape.Perimeter, 9);
        }

        [Fact]
        public void ASplineAndAnOpenLoopAndLoopsOutOfOnePlane_CannotMakeARegion()
        {
            var spline = new DxfSpline();
            var open = new DxfCurve[] { new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0)) };
            var lifted = Rectangle(0, 0, 10, 10);
            var hole = new DxfCurve[] { new DxfCircle(new GeoPoint3(5, 5, 4), new GeoVector3(0, 0, 1), 1.0) };

            Assert.Throws<ArgumentException>(() =>
                DxfRegionShape.Create(new DxfCurve[] { spline }, null, Tolerance.Default)
            );
            Assert.Throws<ArgumentException>(() => DxfRegionShape.Create(open, null, Tolerance.Default));
            Assert.Throws<ArgumentException>(() =>
                DxfRegionShape.Create(new DxfCurve[] { lifted }, new[] { hole }, Tolerance.Default)
            );
        }

        [Fact]
        public void AClockwiseCircleAndRectangle_GiveTheSameRegionAsCounterClockwiseOnes()
        {
            var clockwise = new DxfPolyline();
            clockwise.AddVertexAt(0, new GeoPoint2(0, 0), 0.0, 0.0, 0.0);
            clockwise.AddVertexAt(1, new GeoPoint2(0, 6), 0.0, 0.0, 0.0);
            clockwise.AddVertexAt(2, new GeoPoint2(10, 6), 0.0, 0.0, 0.0);
            clockwise.AddVertexAt(3, new GeoPoint2(10, 0), 0.0, 0.0, 0.0);
            clockwise.Closed = true;

            DxfRegionShape shape = DxfRegionShape.Create(new DxfCurve[] { clockwise }, null, Tolerance.Default);

            Assert.Equal(60.0, shape.Area, 9);
            Assert.Equal(1.0, shape.Normal.Z, 12);
        }

        [Fact]
        public void TheRegionOfACircleAutoCadWroteInR2004_IsReadFromItsSat()
        {
            DxfDatabase db = DxfDatabase.Open(new MemoryStream(DwgAnswerKey.Key("acis_AC1018")));

            DxfUnknownEntity region = Assert.Single(db.ModelSpace.OfType<DxfUnknownEntity>(), e =>
                e.DxfName == "REGION"
            );

            DxfRegionShape shape = region.GetRegionShape();
            Assert.NotNull(shape);
            Assert.Equal(153.93804002589985, shape.Area, 9);
            Assert.Equal(43.982297150257104, shape.Perimeter, 9);
            DxfCircle circle = Assert.IsType<DxfCircle>(Assert.Single(region.Explode()));
            Assert.Equal(7.0, circle.Radius, 9);
        }

        [Fact]
        public void TheRegionOfACircleAutoCadWroteInR2013_IsReadFromTheDataStore()
        {
            DxfDatabase db = DxfDatabase.Open(new MemoryStream(DwgAnswerKey.Key("acis_AC1027")));

            DxfUnknownEntity region = Assert.Single(db.ModelSpace.OfType<DxfUnknownEntity>(), e =>
                e.DxfName == "REGION"
            );

            Assert.Equal(153.93804002589985, region.GetRegionShape().Area, 9);
        }

        [Fact]
        public void ARegionWithNoData_AndOtherTypes_HaveNoShape()
        {
            DxfDatabase db = DxfDatabase.Parse(DxfFileEzdxfDrawings.R2018);

            Assert.All(db.ModelSpace.OfType<DxfUnknownEntity>(), e => Assert.Null(e.GetRegionShape()));
        }
    }
}
