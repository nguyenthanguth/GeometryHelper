using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// A segment, a ray, a line or a chain that touches a triangle, a polygon or a face within the point tolerance without
    /// crossing it is given the point of it nearest the region, as an arc and a box are, and none where it lies level over
    /// the material or along the rim; a crossing is given only where the two touch.
    /// </summary>
    /// <remarks>
    /// They were given the crossing of the region's plane held by the region, or nothing: a segment ending 0.0005 over a
    /// triangle, or passing 0.0007 off its corner, collided and was given no point; and a ray standing 0.0008 over the plane
    /// and 0.0008 beside the rim, 0.00113 from the triangle, was given its origin and did not collide.
    /// </remarks>
    public class StraightFlatFaultLN6TouchTests
    {
        private static readonly Tolerance Tol = Tolerance.Default;

        private static readonly GeoTriangle3 Triangle = new GeoTriangle3(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), new GeoPoint3(0, 100, 0));

        private static readonly GeoPolygon3 Square = new GeoPolygon3(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), new GeoPoint3(100, 100, 0), new GeoPoint3(0, 100, 0));

        private static readonly GeoFace3 Plate = new GeoFace3(
            Square,
            new[] { new GeoPolygon3(new GeoPoint3(30, 30, 0), new GeoPoint3(30, 70, 0), new GeoPoint3(70, 70, 0), new GeoPoint3(70, 30, 0)) },
            Tol);

        private static void AssertPoint(GeoPoint3 expected, GeoPoint3[] points)
        {
            Assert.Single(points);
            Assert.True(points[0].IsEqualTo(expected, new Tolerance(1E-9, 1E-9, 1E-9, 1E-9)), points[0].ToString());
        }

        [Fact]
        public void ASegmentEndingJustOverTheMaterial_IsGivenItsEnd()
        {
            var segment = new GeoLine3(new GeoPoint3(20, 20, 0.0005), new GeoPoint3(20, 20, 5));
            var expected = new GeoPoint3(20, 20, 0.0005);

            Assert.True(segment.CollidesWith(Triangle, Tol));
            AssertPoint(expected, segment.GetIntersections(Triangle, Tol));
            AssertPoint(expected, Triangle.GetIntersections(segment, Tol));
            AssertPoint(expected, segment.GetIntersections(Square, Tol));
            AssertPoint(expected, Square.GetIntersections(segment, Tol));
            AssertPoint(expected, segment.GetIntersections(Plate, Tol));
            AssertPoint(expected, Plate.GetIntersections(segment, Tol));
        }

        [Fact]
        public void ASegmentPassingACornerWithinTheTolerance_IsGivenItsPointNearestTheCorner()
        {
            // Level 0.0004 over the plane, 0.00057 beside the corner at the origin, at 45 degrees to both its sides: 0.00069 off.
            var segment = new GeoLine3(new GeoPoint3(-5.0004, 4.9996, 0.0004), new GeoPoint3(4.9996, -5.0004, 0.0004));
            var expected = new GeoPoint3(-0.0004, -0.0004, 0.0004);

            Assert.True(segment.CollidesWith(Triangle, Tol));
            AssertPoint(expected, segment.GetIntersections(Triangle, Tol));
            AssertPoint(expected, segment.Reverse().GetIntersections(Triangle, Tol));
            AssertPoint(expected, segment.GetIntersections(Square, Tol));
            AssertPoint(expected, segment.GetIntersections(Plate, Tol));
        }

        [Fact]
        public void ASegmentLyingLevelOverTheMaterial_IsGivenNothing()
        {
            var segment = new GeoLine3(new GeoPoint3(10, 10, 0.0005), new GeoPoint3(25, 20, 0.0006));

            Assert.True(segment.CollidesWith(Triangle, Tol));
            Assert.Empty(segment.GetIntersections(Triangle, Tol));
            Assert.Empty(segment.GetIntersections(Square, Tol));
            Assert.Empty(segment.GetIntersections(Plate, Tol));
        }

        [Fact]
        public void ASegmentLyingLevelAndRunningInOverTheRim_IsGivenNothing()
        {
            var segment = new GeoLine3(new GeoPoint3(20, -10, 0.0004), new GeoPoint3(20, 10, 0.0005));

            Assert.True(segment.CollidesWith(Triangle, Tol));
            Assert.Empty(segment.GetIntersections(Triangle, Tol));
        }

        [Fact]
        public void ASegmentLyingAlongASideJustOutside_IsGivenNothing()
        {
            var segment = new GeoLine3(new GeoPoint3(20, -0.0006, 0.0003), new GeoPoint3(60, -0.0006, 0.0003));

            Assert.True(segment.CollidesWith(Triangle, Tol));
            Assert.Empty(segment.GetIntersections(Triangle, Tol));
            Assert.Empty(segment.GetIntersections(Plate, Tol));
        }

        [Fact]
        public void ASegmentOnTheLineOfASideStoppingShortOfTheCorner_IsGivenItsEnd()
        {
            // Within 0.0005 of the line of the side along X, but past the corner at the origin: it touches the corner only.
            var segment = new GeoLine3(new GeoPoint3(-10, -0.0004, 0.0003), new GeoPoint3(-0.0006, -0.0004, 0.0003));

            Assert.True(segment.CollidesWith(Triangle, Tol));
            AssertPoint(new GeoPoint3(-0.0006, -0.0004, 0.0003), segment.GetIntersections(Triangle, Tol));
        }

        [Fact]
        public void ARayRunningAwayFromTheRimWithinTheTolerance_IsGivenItsOrigin()
        {
            var ray = new GeoRay3(new GeoPoint3(-0.0006, 50, 0.0003), new GeoVector3(-1, 0, 0));
            var expected = new GeoPoint3(-0.0006, 50, 0.0003);

            Assert.True(ray.CollidesWith(Triangle, Tol));
            AssertPoint(expected, ray.GetIntersections(Triangle, Tol));
            AssertPoint(expected, Triangle.GetIntersections(ray, Tol));
            AssertPoint(expected, ray.GetIntersections(Square, Tol));
            AssertPoint(expected, ray.GetIntersections(Plate, Tol));
        }

        [Fact]
        public void ALinePassingACornerWithinTheTolerance_IsGivenItsPointNearestTheCorner()
        {
            var line = new GeoXLine3(new GeoPoint3(-0.0004, -0.0004, 0.0004), new GeoVector3(1, -1, 0));
            var expected = new GeoPoint3(-0.0004, -0.0004, 0.0004);

            Assert.True(line.CollidesWith(Triangle, Tol));
            AssertPoint(expected, line.GetIntersections(Triangle, Tol));
            AssertPoint(expected, Triangle.GetIntersections(line, Tol));
        }

        [Fact]
        public void AChainWhoseCornerTouchesTheRegion_IsGivenTheCorner()
        {
            var chain = new GeoPolyline3(new[] { new GeoPoint3(20, 20, 5), new GeoPoint3(20, 20, 0.0005), new GeoPoint3(30, 20, 5) });
            var expected = new GeoPoint3(20, 20, 0.0005);

            Assert.True(chain.CollidesWith(Triangle, Tol));
            AssertPoint(expected, chain.GetIntersections(Triangle, Tol));
            AssertPoint(expected, Triangle.GetIntersections(chain, Tol));
            AssertPoint(expected, chain.GetIntersections(Plate, Tol));
        }

        [Fact]
        public void ARayFurtherThanTheToleranceFromTheRegion_IsGivenNothing()
        {
            // 0.0008 over the plane and 0.0008 beside the rim: 0.00113 from the triangle, which it does not touch.
            var ray = new GeoRay3(new GeoPoint3(-0.0008, 50, 0.0008), new GeoVector3(0, 0, 1));

            Assert.False(ray.CollidesWith(Triangle, Tol));
            Assert.Empty(ray.GetIntersections(Triangle, Tol));
        }

        [Fact]
        public void ARayStartingBesideAHoleFurtherThanTheToleranceFromTheMaterial_IsGivenNothing()
        {
            // A plate 2.6 across turned in space, with a hole in its middle; the ray starts 0.00076 under its plane and beside
            // the rim of the hole, 0.00113 from the material, and runs away from it. It was given its origin.
            var c0 = new GeoPoint3(-3.660902247606266, 9.314816379600586, 7.902559590480552);
            var c1 = new GeoPoint3(-5.720930313618348, 8.610986935170672, 6.4594544951073924);
            var c2 = new GeoPoint3(-7.259398693509985, 10.147942472332325, 7.906011288585519);
            var c3 = new GeoPoint3(-5.199370627497903, 10.851771916762239, 9.349116383958679);
            GeoPoint3 At(double x, double y) => c0.Add(c0.GetVectorTo(c1).Multiply(x)).Add(c0.GetVectorTo(c3).Multiply(y));
            var plate = new GeoFace3(new GeoPolygon3(c0, c1, c2, c3), new[] { new GeoPolygon3(At(0.3, 0.3), At(0.3, 0.7), At(0.7, 0.7), At(0.7, 0.3)) }, Tol);
            var origin = new GeoPoint3(-5.011145480321105, 9.833586785030219, 8.157391985502072);
            GeoVector3 toward = origin.GetVectorTo(new GeoPoint3(-5.205488115702256, 8.99131717359962, 8.845606197457652));
            var ray = new GeoRay3(origin, toward.Multiply(1.0 / toward.Length));

            Assert.False(ray.CollidesWith(plate, Tol));
            Assert.Empty(ray.GetIntersections(plate, Tol));
            Assert.Empty(plate.GetIntersections(ray, Tol));
        }

        [Fact]
        public void ACrossing_IsGivenAsBefore()
        {
            var segment = new GeoLine3(new GeoPoint3(20, 20, -5), new GeoPoint3(20, 20, 5));

            AssertPoint(new GeoPoint3(20, 20, 0), segment.GetIntersections(Triangle, Tol));
            Assert.Empty(new GeoLine3(new GeoPoint3(50, 50, -5), new GeoPoint3(50, 50, 5)).GetIntersections(Plate, Tol));
        }
    }
}
