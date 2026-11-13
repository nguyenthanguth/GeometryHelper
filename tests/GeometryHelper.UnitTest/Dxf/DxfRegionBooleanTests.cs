using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The union, difference and intersection of regions (<see cref="DxfRegionShape.Unite"/>, <see cref="DxfRegionShape.Subtract"/>
    /// and <see cref="DxfRegionShape.Intersect"/>): the area and perimeter AutoCAD 2021 gave for the same regions made by its
    /// <c>BooleanOperation</c>, arcs left arcs, a box and a circle far apart one region of two faces.
    /// </summary>
    public class DxfRegionBooleanTests
    {
        private static readonly Tolerance Tol = Tolerance.Default;

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
                Tol
            );

        private static DxfRegionShape Disc(double x, double y, double radius) =>
            DxfRegionShape.Create(
                new DxfCurve[] { new DxfCircle(new GeoPoint3(x, y, 0), new GeoVector3(0, 0, 1), radius) },
                null,
                Tol
            );

        private static DxfRegionShape Oval(double x, double y, double a, double b) =>
            DxfRegionShape.Create(
                new DxfCurve[]
                {
                    new DxfEllipse(
                        new GeoPoint3(x, y, 0),
                        new GeoVector3(0, 0, 1),
                        new GeoVector3(a, 0, 0),
                        b / a,
                        0.0,
                        2.0 * Math.PI
                    ),
                },
                null,
                Tol
            );

        private static IEnumerable<string> Kinds(DxfRegionShape shape) =>
            shape.Loops.Select(l => string.Join(" ", l.Curves.Select(c => c.GetType().Name.Substring(3))));

        [Fact]
        public void ABoxAndACircleOnItsRightEdge_Unite_ToAreaAndPerimeterAutoCadGives_TheCircleStayingAnArc()
        {
            DxfRegionShape united = DxfRegionShape.Unite(new[] { Box(0, 0, 40, 30), Disc(40, 15, 10) }, Tol);

            Assert.Equal(1357.079632679, united.Area, 6);
            Assert.Equal(151.415926536, united.Perimeter, 6);
            Assert.Equal(new[] { "Arc Line Line Line Line Line" }, Kinds(united));
            DxfArc arc = Assert.Single(united.Loops[0].Curves.OfType<DxfArc>());
            Assert.Equal(40.0, arc.Center.X, 9);
            Assert.Equal(15.0, arc.Center.Y, 9);
            Assert.Equal(10.0, arc.Radius, 9);
            Assert.Equal(3.0 * Math.PI / 2.0, arc.StartAngle, 6);
            Assert.Equal(Math.PI / 2.0, arc.EndAngle, 6);
        }

        [Fact]
        public void ABoxLessACircleOnItsRightEdge_IsANotchOfOneArc()
        {
            DxfRegionShape notch = DxfRegionShape.Subtract(
                new[] { Box(0, 0, 40, 30) },
                new[] { Disc(40, 15, 10) },
                Tol
            );

            Assert.Equal(1042.920367321, notch.Area, 6);
            Assert.Equal(151.415926536, notch.Perimeter, 6);
            Assert.Equal(new[] { "Arc Line Line Line Line Line" }, Kinds(notch));
        }

        [Fact]
        public void ABoxLessACircleInsideIt_IsOneFaceWithTheCircleAHole()
        {
            DxfRegionShape holed = DxfRegionShape.Subtract(new[] { Box(0, 0, 40, 30) }, new[] { Disc(20, 15, 5) }, Tol);

            Assert.Equal(1121.46018366, holed.Area, 6);
            Assert.Equal(171.415926536, holed.Perimeter, 6);
            Assert.Equal(new[] { "Circle", "Line Line Line Line" }, Kinds(holed));
            Assert.Equal(new[] { true, false }, holed.Loops.Select(l => l.IsHole).ToArray());
        }

        [Fact]
        public void TwoCirclesOfRadius10_IntersectInALensOfTwoArcs()
        {
            DxfRegionShape lens = DxfRegionShape.Intersect(new[] { Disc(0, 0, 10), Disc(10, 0, 10) }, Tol);

            Assert.Equal(122.836969861, lens.Area, 6);
            Assert.Equal(41.887902048, lens.Perimeter, 6);
            Assert.Equal(new[] { "Arc Arc" }, Kinds(lens));
        }

        [Fact]
        public void ABoxAndACircleFarApart_Unite_AsOneShapeOfTwoFaces_AsAutoCadKeepsThem()
        {
            DxfRegionShape united = DxfRegionShape.Unite(new[] { Box(0, 0, 40, 30), Disc(100, 15, 10) }, Tol);

            Assert.Equal(1514.159265359, united.Area, 6);
            Assert.Equal(202.831853072, united.Perimeter, 6);
            Assert.Equal(new[] { "Line Line Line Line", "Circle" }, Kinds(united));
        }

        [Fact]
        public void ABoxFrameWithACircleInItsHole_IsTwoFacesAndAFrameOfTwoLoops()
        {
            DxfRegionShape frame = DxfRegionShape.Subtract(
                new[] { Box(0, 0, 60, 60) },
                new[] { Box(10, 10, 40, 40) },
                Tol
            );
            DxfRegionShape united = DxfRegionShape.Unite(new[] { frame, Disc(30, 30, 8) }, Tol);

            Assert.Equal(2201.06192983, united.Area, 6);
            Assert.Equal(450.265482457, united.Perimeter, 6);
            Assert.Equal(new[] { "Line Line Line Line", "Line Line Line Line", "Circle" }, Kinds(united));
        }

        [Fact]
        public void AnEllipseAndABox_Intersect_InTwoLinesAndTwoEllipticalArcs()
        {
            DxfRegionShape both = DxfRegionShape.Intersect(new[] { Oval(20, 15, 30, 10), Box(0, 0, 40, 30) }, Tol);

            Assert.Equal(735.978990736, both.Area, 6);
            Assert.Equal(110.723870859, both.Perimeter, 5);
            Assert.Equal(new[] { "Line Ellipse Line Ellipse" }, Kinds(both));
        }

        [Fact]
        public void ABoxLessTwoCircles_HasTwoHoles_TheLatestCutFirst_AsAutoCadListsThem()
        {
            DxfRegionShape holed = DxfRegionShape.Subtract(
                new[] { Box(0, 0, 60, 30) },
                new[] { Disc(15, 15, 5), Disc(45, 15, 7) },
                Tol
            );

            Assert.Equal(1567.522143634, holed.Area, 6);
            Assert.Equal(255.398223686, holed.Perimeter, 6);
            DxfCircle first = Assert.IsType<DxfCircle>(holed.Loops[0].Curves[0]);
            Assert.Equal(45.0, first.Center.X, 9);
        }

        [Fact]
        public void ABoxLessTwoCirclesThatNotchIt_AndAThreeWayUnionAndIntersection_AgreeWithAutoCad()
        {
            DxfRegionShape cut = DxfRegionShape.Subtract(
                new[] { Box(0, 0, 40, 30) },
                new[] { Disc(40, 15, 10), Disc(20, 30, 8) },
                Tol
            );
            DxfRegionShape three = DxfRegionShape.Unite(
                new[] { Disc(20, 30, 8), Box(0, 0, 40, 30), Disc(40, 15, 10) },
                Tol
            );
            DxfRegionShape common = DxfRegionShape.Intersect(
                new[] { Box(0, 0, 40, 30), Disc(40, 15, 10), Disc(35, 25, 8) },
                Tol
            );

            Assert.Equal(942.389402406, cut.Area, 6);
            Assert.Equal(160.548667765, cut.Perimeter, 6);
            Assert.Equal(1457.610597594, three.Area, 6);
            Assert.Equal(160.548667765, three.Perimeter, 6);
            Assert.Equal(53.719230893, common.Area, 6);
            Assert.Equal(28.64350895, common.Perimeter, 6);
        }

        [Fact]
        public void SeveralShapesToTakeFrom_AreTakenAsTheirUnion_BeforeTheToolsGo()
        {
            DxfRegionShape left = DxfRegionShape.Subtract(
                new[] { Box(0, 0, 40, 30), Disc(60, 15, 10) },
                new[] { Disc(20, 15, 8) },
                Tol
            );

            Assert.Equal(1313.097335529, left.Area, 6);
            Assert.Equal(253.097335529, left.Perimeter, 6);
            Assert.Equal(new[] { "Circle", "Line Line Line Line", "Circle" }.Length, left.Loops.Count);
        }

        [Fact]
        public void WhereNothingIsCommon_Intersect_GivesNull_AndSoDoesATakeAwayOfTheWholeThing()
        {
            Assert.Null(DxfRegionShape.Intersect(new[] { Box(0, 0, 40, 30), Disc(100, 15, 10) }, Tol));
            Assert.Null(DxfRegionShape.Subtract(new[] { Disc(20, 15, 5) }, new[] { Box(0, 0, 40, 30) }, Tol));
        }

        [Fact]
        public void ATakeAwayThatMissesTheShape_LeavesTheShapeAsItWas()
        {
            DxfRegionShape left = DxfRegionShape.Subtract(
                new[] { Box(0, 0, 40, 30) },
                new[] { Disc(100, 15, 10) },
                Tol
            );

            Assert.Equal(1200.0, left.Area, 9);
            Assert.Equal(140.0, left.Perimeter, 9);
        }

        [Fact]
        public void TwoHalvesOfOneDisc_Unite_WithNoSliverBetweenThem_BecauseTheyShareTheCutsOfTheirCircle()
        {
            // The upper half of the disc of radius 25 about (3, 4) and the lower half, each an arc and a diameter.
            DxfRegionShape Half(double sign) =>
                DxfRegionShape.Create(
                    new DxfCurve[]
                    {
                        new DxfArc(
                            new GeoPoint3(3, 4, 0),
                            new GeoVector3(0, 0, 1),
                            25.0,
                            sign > 0 ? 0.0 : Math.PI,
                            sign > 0 ? Math.PI : 2.0 * Math.PI
                        ),
                        new DxfLine(new GeoPoint3(3 - 25.0 * sign, 4, 0), new GeoPoint3(3 + 25.0 * sign, 4, 0)),
                    },
                    null,
                    Tol
                );

            DxfRegionShape united = DxfRegionShape.Unite(new[] { Half(1.0), Half(-1.0) }, Tol);

            Assert.Equal(Math.PI * 625.0, united.Area, 6);
            Assert.Equal(2.0 * Math.PI * 25.0, united.Perimeter, 6);
        }

        [Fact]
        public void RegionsOnATiltedPlane_AreCombinedAsTheyWouldBeFlat()
        {
            var tilt = GeoTransform3.RotationX(Math.PI / 6.0);
            DxfRegionShape Tilted(DxfRegionShape flat)
            {
                var db = new DxfDatabase("AC1032");
                DxfUnknownEntity region = DxfUnknownEntity.CreateRegion(flat, db);
                db.ModelSpace.AppendEntity(region);
                region.TransformBy(tilt);
                return region.GetRegionShape();
            }

            DxfRegionShape united = DxfRegionShape.Unite(
                new[] { Tilted(Box(0, 0, 40, 30)), Tilted(Disc(40, 15, 10)) },
                Tol
            );

            Assert.Equal(1357.079632679, united.Area, 6);
            Assert.Equal(151.415926536, united.Perimeter, 6);
            Assert.Equal(Math.Cos(Math.PI / 6.0), united.Normal.Z, 9);
        }

        [Fact]
        public void ARegionMirroredAndOneNot_ShareAPlane_AndAreCombinedAsIfBothWereFlat()
        {
            // The mirrored box has its normal down, its loops counter-clockwise about it: the same plane, faced the other way.
            var db = new DxfDatabase("AC1032");
            DxfUnknownEntity box = DxfUnknownEntity.CreateRegion(Box(0, 0, 40, 30), db);
            db.ModelSpace.AppendEntity(box);
            box.TransformBy(GeoTransform3.Mirror(new GeoPlane3(new GeoPoint3(50, 0, 0), new GeoVector3(1, 0, 0))));
            DxfRegionShape mirrored = box.GetRegionShape();
            DxfRegionShape disc = Disc(60, 15, 10);

            Assert.Equal(-1.0, mirrored.Normal.Z, 12);
            Assert.True(mirrored.IsCoplanarWith(disc, Tol));
            DxfRegionShape united = DxfRegionShape.Unite(new[] { mirrored, disc }, Tol);
            DxfRegionShape other = DxfRegionShape.Unite(new[] { disc, mirrored }, Tol);
            DxfRegionShape cut = DxfRegionShape.Subtract(new[] { mirrored }, new[] { disc }, Tol);

            Assert.Equal(1357.079632679, united.Area, 6);
            Assert.Equal(1357.079632679, other.Area, 6);
            Assert.Equal(1042.920367321, cut.Area, 6);
            Assert.Equal(151.415926536, cut.Perimeter, 6);
        }

        [Fact]
        public void RegionsInDifferentPlanes_AreRefused_AndSaidToBeSoByTheCheck()
        {
            var db = new DxfDatabase("AC1032");
            DxfUnknownEntity high = DxfUnknownEntity.CreateRegion(Box(0, 0, 40, 30), db);
            db.ModelSpace.AppendEntity(high);
            high.TransformBy(GeoTransform3.Translation(new GeoVector3(0, 0, 5)));
            DxfRegionShape flat = Box(0, 0, 40, 30);
            DxfRegionShape up = high.GetRegionShape();

            Assert.False(flat.IsCoplanarWith(up, Tol));
            Assert.True(flat.IsCoplanarWith(Disc(100, 100, 3), Tol));
            Assert.Throws<ArgumentException>(() => DxfRegionShape.Unite(new[] { flat, up }, Tol));
        }

        [Fact]
        public void AShapeOfTwoFaces_IsWrittenAsARegionOfTwoLumps_InEveryVersion()
        {
            DxfRegionShape two = DxfRegionShape.Unite(new[] { Box(0, 0, 40, 30), Disc(100, 15, 10) }, Tol);

            foreach (string version in new[] { "AC1015", "AC1018", "AC1021", "AC1024", "AC1027", "AC1032" })
            {
                var db = new DxfDatabase(version);
                db.ModelSpace.AppendEntity(DxfUnknownEntity.CreateRegion(two, db));

                DxfDatabase back = DxfDatabase.Parse(db.ToText());
                DxfRegionShape read = back.ModelSpace.OfType<DxfUnknownEntity>().Single().GetRegionShape();

                Assert.NotNull(read);
                Assert.Equal(1514.159265359, read.Area, 6);
                Assert.Equal(202.831853072, read.Perimeter, 6);
                Assert.Equal(2, read.Loops.Count);
            }
        }

        [Theory]
        [InlineData("AC1015")]
        [InlineData("AC1032")]
        public void ARegionOfTwoFaces_ExplodesIntoARegionForEachFace_AsAutoCadsExplodeDoes(string version)
        {
            DxfRegionShape two = DxfRegionShape.Unite(new[] { Box(0, 0, 40, 30), Disc(100, 15, 10) }, Tol);
            var db = new DxfDatabase(version);
            DxfUnknownEntity region = DxfUnknownEntity.CreateRegion(two, db);
            region.Layer = "0";
            db.ModelSpace.AppendEntity(region);

            IReadOnlyList<DxfEntity> parts = region.Explode();

            Assert.Equal(2, parts.Count);
            Assert.All(parts, p => db.ModelSpace.AppendEntity(p));
            double[] areas = parts
                .Cast<DxfUnknownEntity>()
                .Select(p => Math.Round(p.GetRegionShape().Area, 6))
                .ToArray();
            Assert.Equal(new[] { 1200.0, 314.159265 }, areas);
            DxfRegionShape disc = ((DxfUnknownEntity)parts[1]).GetRegionShape();
            Assert.IsType<DxfCircle>(Assert.Single(disc.Loops).Curves[0]);
        }

        [Fact]
        public void TheSatOfTwoFaces_HasTwoLumps_EachWithAShellOfItsOwn()
        {
            DxfRegionShape two = DxfRegionShape.Unite(new[] { Box(0, 0, 40, 30), Disc(100, 15, 10) }, Tol);
            AcisFormat format = AcisFormat.For("AC1015");

            List<string> sat = AcisWriter.Sat(two.Body.Records(format), format);

            Assert.Equal(2, sat.Count(l => l.StartsWith("lump ", StringComparison.Ordinal)));
            Assert.Equal(2, sat.Count(l => l.StartsWith("shell ", StringComparison.Ordinal)));
            Assert.Equal(2, sat.Count(l => l.StartsWith("face ", StringComparison.Ordinal)));
            Assert.Contains("body $-1 $1 $-1 $-1 #", sat);
            Assert.Contains("lump $-1 $2 $3 $0 #", sat);
            Assert.Contains("lump $-1 $-1 $4 $0 #", sat);
        }

        [Fact]
        public void NoShapes_AreRefused_AndANullOne_IsOne()
        {
            Assert.Throws<ArgumentException>(() => DxfRegionShape.Unite(new DxfRegionShape[0], Tol));
            Assert.Throws<ArgumentNullException>(() => DxfRegionShape.Unite(null, Tol));
            Assert.Throws<ArgumentNullException>(() => DxfRegionShape.Unite(new DxfRegionShape[] { null }, Tol));
            Assert.Throws<ArgumentNullException>(() => DxfRegionShape.Subtract(new[] { Box(0, 0, 1, 1) }, null, Tol));
        }
    }
}
