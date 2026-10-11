using System;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid.Core
{
    /// <summary>
    /// A segment or a ray lying in the plane of a triangle, a polygon or a face touches it where it reaches it, read in
    /// that plane, as its distance says. Each used to be read by its crossing with the plane alone, which a segment
    /// lying in it has none of, so one running across the shape touched nothing at a distance of nought.
    /// </summary>
    /// <remarks>
    /// The plate is the square 100 by 100 in the floor with a corner at the origin and a hole 40 by 40 in its middle,
    /// from 30 to 70 along X and Y; the triangle has corners (0, 0, 0), (100, 0, 0) and (0, 100, 0). The tester's four
    /// cases, on the square 10 by 10 with the hole from (4, 4) to (6, 6), come first.
    /// </remarks>
    public class OldFaultF8Tests
    {
        private static readonly Tolerance Tol = Tolerance.Default;

        private static GeoPoint3 Pt(double x, double y, double z) => new GeoPoint3(x, y, z);

        private static GeoPolygon3 Square(double x0, double y0, double x1, double y1, double z)
            => new GeoPolygon3(Pt(x0, y0, z), Pt(x1, y0, z), Pt(x1, y1, z), Pt(x0, y1, z));

        private static GeoFace3 Plate() => new GeoFace3(Square(0, 0, 100, 100, 0), new[] { Square(30, 30, 70, 70, 0) });

        private static GeoFace3 SmallPlate() => new GeoFace3(Square(0, 0, 10, 10, 0), new[] { Square(4, 4, 6, 6, 0) });

        private static GeoTriangle3 Triangle() => new GeoTriangle3(Pt(0, 0, 0), Pt(100, 0, 0), Pt(0, 100, 0));

        private static GeoPolygon3 Floor() => Square(0, 0, 100, 100, 0);

        // The plate turned a quarter of the way up about the Y axis, so that it lies in the plane z = x.
        private static GeoFace3 SlopingPlate()
        {
            Func<double, double, GeoPoint3> at = (u, v) => Pt(u * Math.Sqrt(0.5), v, u * Math.Sqrt(0.5));

            var outline = new GeoPolygon3(at(0, 0), at(100, 0), at(100, 100), at(0, 100));
            var hole = new GeoPolygon3(at(30, 30), at(70, 30), at(70, 70), at(30, 70));

            return new GeoFace3(outline, new[] { hole });
        }

        [Fact]
        public void ASegmentLyingInTheFacesPlaneAcrossItsMaterial_CollidesWithIt()
        {
            // Along y = 2 from x = -5 to 15: across the material from x = 0 to 10, clear of the hole by 2.
            var across = new GeoLine3(Pt(-5, 2, 0), Pt(15, 2, 0));

            Assert.Equal(0.0, across.DistanceTo(SmallPlate(), Tol), 9);
            Assert.True(across.CollidesWith(SmallPlate(), Tol));
        }

        [Fact]
        public void ASegmentLevelFourTenThousandthsOverTheFacesPlaneAcrossItsMaterial_CollidesWithIt()
        {
            // The same segment 0.0004 over the plane, 0.4 of the planar tolerance: it lies in the plane as drawn.
            var across = new GeoLine3(Pt(-5, 2, 0.0004), Pt(15, 2, 0.0004));

            Assert.True(across.CollidesWith(SmallPlate(), Tol));
        }

        [Fact]
        public void ARayLyingInTheFacesPlaneRunningIntoItsMaterial_CollidesWithIt()
        {
            // From (-5, 2, 0) along x: it reaches the material at (0, 2, 0).
            var into = new GeoRay3(Pt(-5, 2, 0), new GeoVector3(1, 0, 0));

            Assert.Equal(0.0, into.DistanceTo(SmallPlate(), Tol), 9);
            Assert.True(into.CollidesWith(SmallPlate(), Tol));
        }

        [Fact]
        public void ASegmentAndARayLyingInTheFacesPlaneOverItsHoleOrAwayFromIt_DoNotCollideWithIt()
        {
            // Inside the hole, 0.5 clear of its rim; and a ray from (-5, 2, 0) pointing away along -x, 5 from the face.
            var overHole = new GeoLine3(Pt(4.5, 5, 0), Pt(5.5, 5, 0));
            var away = new GeoRay3(Pt(-5, 2, 0), new GeoVector3(-1, 0, 0));

            Assert.Equal(0.5, overHole.DistanceTo(SmallPlate(), Tol), 9);
            Assert.False(overHole.CollidesWith(SmallPlate(), Tol));
            Assert.Equal(5.0, away.DistanceTo(SmallPlate(), Tol), 9);
            Assert.False(away.CollidesWith(SmallPlate(), Tol));
        }

        [Fact]
        public void ASegmentLyingInTheFloorAcrossThePlate_TouchesIt()
        {
            var line = new GeoLine3(Pt(-10, 10, 0), Pt(110, 10, 0));

            Assert.Equal(0.0, Distance3.DistanceTo(line, Plate(), Tol));
            Assert.True(Collision3.CollidesWith(line, Plate(), Tol));
            Assert.True(Plate().CollidesWith(line, Tol));
        }

        [Fact]
        public void ASegmentLyingInTheFloorAcrossTheHole_TouchesTheMaterialEitherSide()
        {
            var line = new GeoLine3(Pt(20, 50, 0), Pt(80, 50, 0));

            Assert.True(Collision3.CollidesWith(line, Plate(), Tol));
        }

        [Fact]
        public void ASegmentLyingWhollyInsideTheMaterial_TouchesIt()
        {
            var line = new GeoLine3(Pt(5, 5, 0), Pt(25, 15, 0));

            Assert.True(Collision3.CollidesWith(line, Plate(), Tol));
        }

        [Fact]
        public void ASegmentLyingInTheHole_TouchesNothing()
        {
            var line = new GeoLine3(Pt(40, 50, 0), Pt(60, 50, 0));

            Assert.Equal(10.0, Distance3.DistanceTo(line, Plate(), Tol), 9);
            Assert.False(Collision3.CollidesWith(line, Plate(), Tol));
        }

        [Fact]
        public void ASegmentLyingInTheFloorBesideThePlate_TouchesNothing()
        {
            var line = new GeoLine3(Pt(-10, -5, 0), Pt(110, -5, 0));

            Assert.False(Collision3.CollidesWith(line, Plate(), Tol));
        }

        [Fact]
        public void ASegmentLyingInTheFloorHalfAThousandthOffTheRim_TouchesIt()
        {
            var line = new GeoLine3(Pt(-10, -0.0005, 0), Pt(110, -0.0005, 0));

            Assert.True(Collision3.CollidesWith(line, Plate(), Tol));
        }

        [Fact]
        public void ASegmentLyingLevelHalfAThousandthOverThePlate_TouchesIt()
        {
            var line = new GeoLine3(Pt(-10, 10, 0.0005), Pt(110, 10, 0.0005));

            Assert.True(Collision3.CollidesWith(line, Plate(), Tol));
        }

        [Fact]
        public void ASegmentLyingLevelTwoThousandthsOverThePlate_TouchesNothing()
        {
            var line = new GeoLine3(Pt(-10, 10, 0.002), Pt(110, 10, 0.002));

            Assert.False(Collision3.CollidesWith(line, Plate(), Tol));
        }

        [Fact]
        public void ASegmentLyingInTheSlopeOfASlopingPlateAcrossIt_TouchesIt()
        {
            var line = new GeoLine3(Pt(-10, 10, -10), Pt(80, 10, 80));

            Assert.Equal(0.0, Distance3.DistanceTo(line, SlopingPlate(), Tol), 9);
            Assert.True(Collision3.CollidesWith(line, SlopingPlate(), Tol));
        }

        [Fact]
        public void ASegmentLyingInTheSlopeOfASlopingPlateInItsHole_TouchesNothing()
        {
            double c = 50 * Math.Sqrt(0.5);
            var line = new GeoLine3(Pt(c, 40, c), Pt(c, 60, c));

            Assert.False(Collision3.CollidesWith(line, SlopingPlate(), Tol));
        }

        [Fact]
        public void ARayLyingInTheFloorTowardsThePlate_TouchesIt()
        {
            var ray = new GeoRay3(Pt(-10, 10, 0), GeoVector3.XAxis);

            Assert.Equal(0.0, Distance3.DistanceTo(ray, Plate(), Tol));
            Assert.True(Collision3.CollidesWith(ray, Plate(), Tol));
            Assert.True(Plate().CollidesWith(ray, Tol));
        }

        [Fact]
        public void ARayLyingInTheFloorAwayFromThePlate_TouchesNothing()
        {
            var ray = new GeoRay3(Pt(-10, 10, 0), GeoVector3.XAxis.Negate());

            Assert.False(Collision3.CollidesWith(ray, Plate(), Tol));
        }

        [Fact]
        public void ARayLyingInTheFloorFromTheMiddleOfTheHole_TouchesTheRimOfTheHole()
        {
            var ray = new GeoRay3(Pt(50, 50, 0), GeoVector3.YAxis);

            Assert.True(Collision3.CollidesWith(ray, Plate(), Tol));
        }

        [Fact]
        public void ARayLyingInTheFloorBesideThePlate_TouchesNothing()
        {
            var ray = new GeoRay3(Pt(-10, -5, 0), GeoVector3.XAxis);

            Assert.False(Collision3.CollidesWith(ray, Plate(), Tol));
        }

        [Fact]
        public void ARayLyingInTheSlopeOfASlopingPlateTowardsIt_TouchesIt()
        {
            var ray = new GeoRay3(Pt(-10, 10, -10), new GeoVector3(1, 0, 1));

            Assert.True(Collision3.CollidesWith(ray, SlopingPlate(), Tol));
        }

        [Fact]
        public void AChainLyingInTheFloorAcrossThePlate_TouchesIt()
        {
            var chain = new GeoPolyline3(new[] { Pt(-10, 10, 0), Pt(110, 10, 0), Pt(110, 20, 0) });

            Assert.True(Collision3.CollidesWith(chain, Plate(), Tol));
        }

        [Fact]
        public void ASegmentLyingInTheFloorAcrossASliverNarrowerThanTheTolerance_IsAskedWithoutThrowing()
        {
            var sliver = new GeoFace3(Square(0, 0, 10, 0.005, 0));
            var coarse = new Tolerance(0.01, 1E-5, Tol.EqualAngleRad, 0.01);
            var line = new GeoLine3(Pt(5, -1, 0), Pt(5, 1, 0));

            Assert.Null(Record.Exception(() => Collision3.CollidesWith(line, sliver, coarse)));
        }

        [Fact]
        public void ASegmentAndARayLyingInTheFloor_NameNoPlaceOfCrossing()
        {
            var line = new GeoLine3(Pt(-10, 10, 0), Pt(110, 10, 0));
            var ray = new GeoRay3(Pt(-10, 10, 0), GeoVector3.XAxis);

            Assert.False(Intersection3.TryIntersectWith(line, Plate(), out _, Tol));
            Assert.False(Intersection3.TryIntersectWith(line, Floor(), out _, Tol));
            Assert.False(Intersection3.TryIntersectWith(line, Triangle(), out _, Tol));
            Assert.Empty(Intersection3.GetIntersections(ray, Plate(), Tol));
            Assert.Empty(Intersection3.GetIntersections(ray, Floor(), Tol));
            Assert.Empty(Intersection3.GetIntersections(ray, Triangle(), Tol));
        }

        [Fact]
        public void ASegmentLyingInTheFloorAcrossTheTriangle_TouchesIt()
        {
            var line = new GeoLine3(Pt(-10, 10, 0), Pt(110, 10, 0));

            Assert.Equal(0.0, Distance3.DistanceTo(line, Triangle(), Tol), 9);
            Assert.True(Collision3.CollidesWith(line, Triangle(), Tol));
            Assert.True(line.CollidesWith(Triangle(), Tol));
        }

        [Fact]
        public void ASegmentLyingLevelFourTenThousandthsOverTheTriangle_TouchesIt()
        {
            var line = new GeoLine3(Pt(-10, 10, 0.0004), Pt(110, 10, 0.0004));

            Assert.True(Collision3.CollidesWith(line, Triangle(), Tol));
        }

        [Fact]
        public void ASegmentLyingInTheFloorBeyondTheLongSideOfTheTriangle_TouchesNothing()
        {
            var line = new GeoLine3(Pt(60, 60, 0), Pt(90, 60, 0));

            Assert.False(Collision3.CollidesWith(line, Triangle(), Tol));
        }

        [Fact]
        public void ARayLyingInTheFloorTowardsTheTriangle_TouchesIt_AndAwayFromIt_TouchesNothing()
        {
            Assert.True(Collision3.CollidesWith(new GeoRay3(Pt(-10, 10, 0), GeoVector3.XAxis), Triangle(), Tol));
            Assert.False(Collision3.CollidesWith(new GeoRay3(Pt(-10, 10, 0), GeoVector3.XAxis.Negate()), Triangle(), Tol));
        }

        [Fact]
        public void ASegmentLyingInTheFloorAcrossTheSquare_TouchesIt()
        {
            var line = new GeoLine3(Pt(-10, 10, 0), Pt(110, 10, 0));

            Assert.Equal(0.0, Distance3.DistanceTo(line, Floor(), Tol), 9);
            Assert.True(Collision3.CollidesWith(line, Floor(), Tol));
            Assert.True(line.CollidesWith(Floor(), Tol));
        }

        [Fact]
        public void ASegmentLyingWhollyInsideTheSquare_TouchesIt()
        {
            Assert.True(Collision3.CollidesWith(new GeoLine3(Pt(40, 40, 0), Pt(60, 50, 0)), Floor(), Tol));
        }

        [Fact]
        public void ASegmentLyingInTheFloorBesideTheSquare_TouchesNothing()
        {
            Assert.False(Collision3.CollidesWith(new GeoLine3(Pt(-10, -5, 0), Pt(110, -5, 0)), Floor(), Tol));
        }

        [Fact]
        public void ARayLyingInTheFloorFromInsideTheSquare_TouchesIt()
        {
            var ray = new GeoRay3(Pt(50, 50, 0), new GeoVector3(1, 1, 0));

            Assert.True(Collision3.CollidesWith(ray, Floor(), Tol));
            Assert.True(ray.CollidesWith(Floor(), Tol));
        }

        [Fact]
        public void ARayLyingInTheFloorBesideTheSquare_TouchesNothing()
        {
            Assert.False(Collision3.CollidesWith(new GeoRay3(Pt(-10, -5, 0), GeoVector3.XAxis), Floor(), Tol));
        }

        [Fact]
        public void ASegmentLyingInTheFloorAcrossASliverPolygonNarrowerThanTheTolerance_IsAskedWithoutThrowing()
        {
            GeoPolygon3 sliver = Square(0, 0, 10, 0.005, 0);
            var coarse = new Tolerance(0.01, 1E-5, Tol.EqualAngleRad, 0.01);
            var line = new GeoLine3(Pt(5, -1, 0), Pt(5, 1, 0));

            Assert.Null(Record.Exception(() => Collision3.CollidesWith(line, sliver, coarse)));
        }

        // A sliver 10 by 0.005 lying in the floor across a square 1 by 2, asked under a tolerance of 0.01: in one plane,
        // but with no area left to lay out under that tolerance, so the edge walk answers instead of the plane half.
        private static readonly Tolerance Coarse = new Tolerance(0.01, 1E-5, Tolerance.Default.EqualAngleRad, 0.01);

        private static GeoPolygon3 SliverSquare() => Square(0, 0, 10, 0.005, 0);

        private static GeoPolygon3 Across() => Square(5, -1, 6, 1, 0);

        private static GeoTriangle3 AcrossTriangle() => new GeoTriangle3(Pt(5, -1, 0), Pt(6, -1, 0), Pt(5.5, 1, 0));

        [Fact]
        public void TwoRegionsInOnePlaneOneASliverUnderTheTolerance_CollideWithoutThrowing()
        {
            var sliver = new GeoFace3(SliverSquare());

            Assert.True(Collision3.CollidesWith(sliver, new GeoFace3(Across()), Coarse));
            Assert.True(Collision3.CollidesWith(sliver, Across(), Coarse));
            Assert.True(Collision3.CollidesWith(new GeoFace3(Across()), SliverSquare(), Coarse));
            Assert.True(Collision3.CollidesWith(AcrossTriangle(), sliver, Coarse));
            Assert.True(Collision3.CollidesWith(AcrossTriangle(), SliverSquare(), Coarse));
            Assert.True(Collision3.CollidesWith(SliverSquare(), Across(), Coarse));
            Assert.Equal(0.0, Distance3.DistanceTo(sliver, new GeoFace3(Across()), Coarse));
        }

        [Fact]
        public void TwoRegionsInOnePlaneOneASliverUnderTheTolerance_NameTheirCrossingsWithoutThrowing()
        {
            var sliver = new GeoFace3(SliverSquare());

            Assert.Null(Record.Exception(() => Intersection3.GetIntersections(sliver, new GeoFace3(Across()), Coarse)));
            Assert.Null(Record.Exception(() => Intersection3.GetIntersections(sliver, Across(), Coarse)));
            Assert.Null(Record.Exception(() => Intersection3.GetIntersections(new GeoFace3(Across()), SliverSquare(), Coarse)));
            Assert.Null(Record.Exception(() => Intersection3.GetIntersections(AcrossTriangle(), sliver, Coarse)));
            Assert.Null(Record.Exception(() => Intersection3.GetIntersections(AcrossTriangle(), SliverSquare(), Coarse)));
            Assert.Null(Record.Exception(() => Intersection3.GetIntersections(SliverSquare(), Across(), Coarse)));
        }
    }
}
