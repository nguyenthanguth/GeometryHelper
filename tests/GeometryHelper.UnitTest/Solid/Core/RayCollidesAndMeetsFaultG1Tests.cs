using System;
using GeometryHelper.Geometry;
using GeometryHelper.Spatial;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// A ray is given a point exactly where it collides, but where it lies level along a shape and names no place: the list of
    /// where it meets a triangle, a polygon, a face, a box, a body, an arc or a circle is held to the test <c>CollidesWith</c>
    /// makes. A ray starting within the tolerance of a region and running away from it collided and was given nothing, one
    /// starting within the planar tolerance of a body's face but further than the point tolerance was given its origin and did
    /// not collide, and a point of a circle on a segment held by each within the tolerance, but not by the two together, was
    /// given without a collision.
    /// </summary>
    public class RayCollidesAndMeetsFaultG1Tests
    {
        private static readonly Tolerance Thousandth = Tolerance.Default;

        private static readonly Tolerance Nought = new Tolerance(0, 0, 0, 0);

        /// <summary>A point tolerance of a hundredth with a planar one of five hundredths, wider than it.</summary>
        private static readonly Tolerance WidePlanar = new Tolerance(0.01, 1E-5, Math.PI / 180, 0.05);

        private static readonly GeoPolygon3 Square = new GeoPolygon3(
            new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), new GeoPoint3(100, 100, 0), new GeoPoint3(0, 100, 0));

        /// <summary>A box 100 by 100 by 50 whose top lies in the floor.</summary>
        private static GeoSolid3 Block() => new GeoObb3(new GeoPoint3(50, 50, -25), 100, 100, 50).ToSolid();

        [Theory]
        [InlineData("polygon")]
        [InlineData("face")]
        [InlineData("triangle")]
        public void ARayStartingBesideTheRimAHairOverTheFloorAndRunningAway_TouchesTheRegionAtItsOrigin(string shape)
        {
            // 0.0006 beside the side y = 0 and 0.0003 over the floor, 0.00067 from the region, running away from the side at a
            // hundredth: its line lies level over the region behind it, and the ray holds none of that.
            var start = new GeoPoint3(50, -0.0006, 0.0003);
            var ray = new GeoRay3(start, new GeoVector3(1, -0.01, 0));
            var triangle = new GeoTriangle3(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), new GeoPoint3(0, 100, 0));

            bool collides = shape == "polygon" ? ray.CollidesWith(Square, Thousandth)
                : shape == "face" ? ray.CollidesWith(new GeoFace3(Square), Thousandth)
                : ray.CollidesWith(triangle, Thousandth);
            GeoPoint3[] places = shape == "polygon" ? ray.GetIntersections(Square, Thousandth)
                : shape == "face" ? ray.GetIntersections(new GeoFace3(Square), Thousandth)
                : ray.GetIntersections(triangle, Thousandth);

            Assert.True(collides);
            Assert.Equal(start, Assert.Single(places));
        }

        [Fact]
        public void ARayStartingBesideTheRimAndRunningAway_IsGivenItsOriginByTryIntersectWithToo()
        {
            var start = new GeoPoint3(50, -0.0006, 0.0003);
            var ray = new GeoRay3(start, new GeoVector3(1, -0.01, 0));

            Assert.True(ray.TryIntersectWith(Square, out GeoPoint3 place, Thousandth));
            Assert.Equal(start, place);
        }

        [Theory]
        [InlineData(4E-12, 1.0)]
        [InlineData(-4E-12, -1.0)]
        public void AtToleranceNought_ARayStartingAHairOffASquareAndLeavingIt_IsGivenNoPoint(double height, double rise)
        {
            // Its line crosses the floor 4E-12 behind its origin, which the list took for the origin to the rounding of its numbers.
            var ray = new GeoRay3(new GeoPoint3(50, 50, height), new GeoVector3(0.1, 0, rise));

            Assert.False(ray.CollidesWith(Square, Nought));
            Assert.Empty(ray.GetIntersections(Square, Nought));
        }

        [Fact]
        public void WithThePlanarToleranceWiderThanThePoint_ARayRisingFromFourHundredthsOverABox_IsGivenNoPoint()
        {
            var ray = new GeoRay3(new GeoPoint3(30, 40, 0.04), new GeoVector3(1, 0, 0.01));

            Assert.False(ray.CollidesWith(Block(), WidePlanar));
            Assert.Empty(ray.GetIntersections(Block(), WidePlanar));
        }

        [Fact]
        public void WithThePlanarToleranceWiderThanThePoint_ARayRisingFromFourThousandthsOverABox_TouchesItAtItsOrigin()
        {
            var start = new GeoPoint3(30, 40, 0.004);
            var ray = new GeoRay3(start, new GeoVector3(1, 0, 0.01));

            Assert.True(ray.CollidesWith(Block(), WidePlanar));
            Assert.Equal(start, Assert.Single(ray.GetIntersections(Block(), WidePlanar)));
        }

        [Fact]
        public void AtToleranceNought_ARayFromTheMiddleOfATurnedBox_IsGivenWhereItLeaves()
        {
            // The crossing of each face, rounded off the face's plane, is refused at a tolerance of nought; the body holds the
            // origin and the ray collides, and is given its point nearest the surface, where it leaves.
            GeoSolid3 box = new GeoObb3(GeoPoint3.Origin, 100, 80, 50).ToSolid()
                .TransformBy(GeoTransform3.RotationAxis(GeoPoint3.Origin, new GeoVector3(1, 2, 3), 0.7));
            var ray = new GeoRay3(GeoPoint3.Origin, new GeoVector3(0.3, 0.2, 0.9));

            GeoPoint3 leaving = Assert.Single(ray.GetIntersections(box, Thousandth));

            Assert.True(ray.CollidesWith(box, Nought));
            Assert.Equal(0.0, Assert.Single(ray.GetIntersections(box, Nought)).DistanceTo(leaving), 9);
        }

        [Theory]
        [InlineData(-49.995, -40.0075, -0.05, -0.9387, -0.3446, 0.0122)]
        [InlineData(49.998, -39.98, 0.008, 0.211, 0.977, 5.7E-5)]
        public void ARayStartingWithinTheToleranceOfAnOrientedBoxAndRunningPastItsRim_TouchesItAtItsOrigin(
            double x, double y, double z, double dx, double dy, double dz)
        {
            // The box 100 by 80 by 50 whose top lies in the floor. The ray starts 0.0075 beside a side near its edge, or 0.008
            // over the top 0.002 from the side, and leaves within the point tolerance of its origin; its cut stands within the
            // planar tolerance of the face at both ends and was read as lying along it.
            var box = new GeoObb3(new GeoPoint3(0, 0, -25), 100, 80, 50);
            var start = new GeoPoint3(x, y, z);
            var ray = new GeoRay3(start, new GeoVector3(dx, dy, dz));

            Assert.True(ray.CollidesWith(box, WidePlanar));
            Assert.Equal(start, Assert.Single(ray.GetIntersections(box, WidePlanar)));
        }

        [Fact]
        public void APointOfTheCircleOnASegmentHeldByEachWithinTheToleranceButNotByBoth_IsNotGiven()
        {
            // The segment runs 0.0008 over the plane of the circle of radius 50 and ends 0.0008 short of its rim: the point of
            // the circle on its line is within the tolerance of each, and the two stand 0.00113 apart.
            var circle = new GeoCircle3(GeoPoint3.Origin, new GeoVector3(0, 0, 1), 50);
            var segment = new GeoLine3(new GeoPoint3(0, 0, 0.0008), new GeoPoint3(49.9992, 0, 0.0008));

            Assert.False(circle.CollidesWith(segment, Thousandth));
            Assert.Empty(circle.GetIntersections(segment, Thousandth));
            Assert.Empty(segment.GetIntersections(circle, Thousandth));
        }

        [Fact]
        public void APointOfTheArcOnTheLineOfARayBehindItsOrigin_IsNotGivenWhereTheyDoNotTouch()
        {
            GeoArc3 arc = GeoArc3.FromThreePoints(new GeoPoint3(0, -50, 0), new GeoPoint3(50, 0, 0), new GeoPoint3(0, 50, 0), Thousandth);
            var ray = new GeoRay3(new GeoPoint3(49.9992, 0, 0.0008), new GeoVector3(-1, 0, 0));

            Assert.False(arc.CollidesWith(ray, Thousandth));
            Assert.Empty(arc.GetIntersections(ray, Thousandth));
        }

        [Fact]
        public void APointOfTheCircleOnALine_IsGivenOnItsOwn_WhereTheOtherIsHeldByTheCircleButNotByTheShape()
        {
            // The segment from (-60, 0, 0.0008) passes 0.0008 over the far rim of the circle, a touch, and ends 0.0008 short of
            // the near rim, 0.00113 from it; the ray from that end along -X holds the far touch ahead and the near point of the
            // circle 0.0008 behind its origin. Only the far touch is given.
            var circle = new GeoCircle3(GeoPoint3.Origin, new GeoVector3(0, 0, 1), 50);
            var segment = new GeoLine3(new GeoPoint3(-60, 0, 0.0008), new GeoPoint3(49.9992, 0, 0.0008));
            var ray = new GeoRay3(new GeoPoint3(49.9992, 0, 0.0008), new GeoVector3(-1, 0, 0));

            Assert.True(circle.CollidesWith(segment, Thousandth));
            Assert.Equal(-50.0, Assert.Single(circle.GetIntersections(segment, Thousandth)).X, 6);
            Assert.True(circle.CollidesWith(ray, Thousandth));
            Assert.Equal(-50.0, Assert.Single(circle.GetIntersections(ray, Thousandth)).X, 6);
        }

        [Theory]
        [InlineData(0.0006, 0.0006)]
        [InlineData(0.0007, 0.0004)]
        public void APointOfTheCircleOnALineWhoseEndTouchesTheRim_IsGiven(double height, double shortOfTheRim)
        {
            // The segment runs 0.0006 over the plane of the circle and ends 0.0006 short of the near rim, 0.000849 from it: it
            // touches both rims, and the point of the circle on its line near the end is given as the far one is.
            var circle = new GeoCircle3(GeoPoint3.Origin, new GeoVector3(0, 0, 1), 50);
            var segment = new GeoLine3(new GeoPoint3(-60, 0, height), new GeoPoint3(50 - shortOfTheRim, 0, height));
            var ray = new GeoRay3(segment.EndPoint, new GeoVector3(-1, 0, 0));

            Assert.True(circle.CollidesWith(segment, Thousandth));
            Assert.Equal(2, circle.GetIntersections(segment, Thousandth).Length);
            Assert.Equal(2, circle.GetIntersections(ray, Thousandth).Length);
        }

        [Fact]
        public void ARayStartingFourHundredthsUnderTheTopOfABox_IsGivenOnlyWhereItLeaves()
        {
            // Within the planar tolerance of the top and held in the box, but further than the point tolerance from its surface:
            // its start meets no surface.
            var ray = new GeoRay3(new GeoPoint3(30, 40, -0.04), new GeoVector3(1, 0, -0.01));

            GeoPoint3 leaving = Assert.Single(ray.GetIntersections(Block(), WidePlanar));

            Assert.Equal(100.0, leaving.X, 9);
        }

        [Theory]
        [InlineData(1.0)]
        [InlineData(0.0)]
        public void ARayStartingInsideATurnedBoxNearAFace_IsGivenTheSamePlaceWhereverTheBoxStands(double far)
        {
            // 0.012 inside the box under a face, within the planar tolerance of five hundredths and further than the point
            // tolerance of a hundredth: given where it leaves, 59.252 along, a hundred million out as at the origin.
            var tolerance = new Tolerance(1E-2, 1E-5, 1E-5, 0.05);
            var at = new GeoVector3(-11521623.845357625, -64124569.86450685, 61809900.33395811).Multiply(far);
            var normal = new GeoVector3(0.38538565846345313, -0.41847365830046684, 0.8224096859590811);
            var along = new GeoVector3(0.5378767117176538, -0.6223050767376763, -0.5687046988185687);
            var box = new GeoObb3(GeoPoint3.Origin.Add(at).Subtract(normal.Multiply(25)), 100, 80, 50, along, normal.CrossProduct(along)).ToSolid();
            var start = new GeoPoint3(-2.519739920, -13.61707684, -5.762646994).Add(at);
            var ray = new GeoRay3(start, new GeoVector3(-0.003866325323057974, 0.04962938228997728, -0.9987602194430911));

            GeoPoint3 leaving = Assert.Single(ray.GetIntersections(box, tolerance));

            Assert.InRange(ray.GetDistanceAtPoint(leaving), 59.252, 59.253);
        }

        [Fact]
        public void ARayStartingFiveHundredthsOutsideATurnedUFiftyMillionOut_IsNotGivenItsOrigin()
        {
            // The origin stands 0.0516 outside the U, past the tolerance of five hundredths, and the ray crosses no face; the
            // rounding a body is read in to a billionth at that size, which takes the origin for one inside, gives it no place.
            var tolerance = new Tolerance(0.05, 1E-5, Math.PI / 180, 0.05);
            var c = new GeoPoint3(29891493.32507117, -50881210.22604462, 1098599.1457004962);
            var u = new GeoVector3(-0.4914452572907459, -0.8688336114330585, 0.060080901546344344);
            var n = new GeoVector3(-0.8703774655684305, 0.49238416531412854, 0.0009493042633146856);
            GeoVector3 v = n.CrossProduct(u);
            double[][] profile = { new[] { 0.0, 0 }, new[] { 100.0, 0 }, new[] { 100.0, 100 }, new[] { 80.0, 100 }, new[] { 80.0, 20 }, new[] { 20.0, 20 }, new[] { 20.0, 100 }, new[] { 0.0, 100 } };
            var corners = new GeoPoint3[profile.Length];

            for (int i = 0; i < profile.Length; i++)
            {
                corners[i] = c.Add(u.Multiply(profile[i][0])).Add(n.Multiply(profile[i][1]));
            }

            GeoSolid3 body = GeoSolid3.Extrude(new GeoPolygon3(corners), v.Multiply(100), tolerance);
            GeoPoint3 start = c.Add(u.Multiply(79.94838496076333)).Add(n.Multiply(20.077697498992876)).Add(v.Multiply(11.935216024022186));
            var ray = new GeoRay3(start, new GeoVector3(-0.2741142077055374, 0.9300847396931561, -0.24454811003947585));

            Assert.Empty(ray.GetIntersections(body, tolerance));
        }

        /// <summary>
        /// A U 100 wide and 100 high in XZ, its prongs 20 thick and its base 20 thick, drawn 100 along Y.
        /// </summary>
        private static GeoSolid3 UBody(Tolerance tolerance) => GeoSolid3.Extrude(
            new GeoPolygon3(
                new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), new GeoPoint3(100, 0, 100), new GeoPoint3(80, 0, 100),
                new GeoPoint3(80, 0, 20), new GeoPoint3(20, 0, 20), new GeoPoint3(20, 0, 100), new GeoPoint3(0, 0, 100)),
            new GeoVector3(0, 100, 0),
            tolerance);

        [Theory]
        [InlineData(20.04, 60.0, 0.0)]
        [InlineData(30.0, 20.04, 0.01)]
        public void ARayStartingFourHundredthsOffTheGapOfAU_IsGivenOnlyWhereItReachesTheFarProng(double x, double z, double rise)
        {
            // Within the planar tolerance of five hundredths of the inner wall or the floor, and further than the point tolerance
            // of a hundredth: the start does not touch the body, whatever the ray meets further on.
            var ray = new GeoRay3(new GeoPoint3(x, 50, z), new GeoVector3(1, 0, rise));

            GeoPoint3[] places = ray.GetIntersections(UBody(WidePlanar), WidePlanar);

            Assert.True(ray.CollidesWith(UBody(WidePlanar), WidePlanar));
            Assert.Equal(2, places.Length);
            Assert.Equal(80.0, places[0].X, 9);
            Assert.Equal(100.0, places[1].X, 9);
        }

        [Fact]
        public void ARayStartingFiveThousandthsOffTheGapOfAU_TouchesItAtItsOriginToo()
        {
            var start = new GeoPoint3(20.005, 50, 60);
            var ray = new GeoRay3(start, new GeoVector3(1, 0, 0));

            GeoPoint3[] places = ray.GetIntersections(UBody(WidePlanar), WidePlanar);

            Assert.Equal(3, places.Length);
            Assert.Equal(start, places[0]);
        }

        [Fact]
        public void APreparedBox_ARayStartingOffItsEdgeWithinTheToleranceAndLeaving_TouchesItAtItsOrigin()
        {
            // 0.0007 over the top and 0.0007 beyond the side, 0.00099 from the edge, with a planar tolerance of 1E-4: the planes
            // of the faces cross nowhere ahead, and the start stands on where the material ends.
            var narrowPlanar = new Tolerance(1E-3, 1E-5, Math.PI / 180, 1E-4);
            var prepared = new GeoPreparedSolid3(Block(), narrowPlanar);
            var start = new GeoPoint3(100.0007, 50, 0.0007);
            var ray = new GeoRay3(start, new GeoVector3(1, 0, 0.1));

            Assert.True(prepared.CollidesWith(ray, narrowPlanar));
            Assert.Equal(start, Assert.Single(prepared.GetIntersections(ray, narrowPlanar)));
            Assert.True(ray.CollidesWith(Block(), narrowPlanar));
            Assert.Equal(start, Assert.Single(ray.GetIntersections(Block(), narrowPlanar)));
        }
    }
}
