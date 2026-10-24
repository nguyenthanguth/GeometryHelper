using System;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// The review of the first round of near touches in space: the search for where two rims pass within the tolerance of
    /// each other gives up no touch however long the rims run along each other, names no place where it stopped, and two
    /// circles answer alike however they are asked.
    /// </summary>
    public class CurveFaultF5ReviewTests
    {
        private static readonly Tolerance Thousandth = Tolerance.Default;

        private static void AssertOnBothRims(GeoEllipse3 one, GeoEllipse3 other, GeoPoint3 place)
        {
            foreach (GeoEllipse3 ellipse in new[] { one, other })
            {
                Assert.InRange(ellipse.GetClosestPointOnBoundary(place).DistanceTo(place), 0.0, Thousandth.EqualPoint);
            }
        }

        [Theory]
        [InlineData(300.0, 100.0)]
        [InlineData(3000.0, 1000.0)]
        public void ACopyLeaningAboveAnEllipse_IsGivenTheTouchAtTheEndOfItsMinorAxis(double major, double minor)
        {
            // The copy stands 0.0012 above, leaning so that its rim is 0.0001 above at one end of the minor axis and 0.0023 at
            // the other: within the tolerance for most of a half turn, the nearest 0.0001 apart. Walked in steps of the gap
            // less the tolerance, the search ran out of readings and named no place, or one where it stopped.
            double sine = 0.0011 / minor;
            var flat = new GeoEllipse3(GeoPoint3.Origin, new GeoVector3(0, 0, 1), new GeoVector3(1, 0, 0), major, minor);
            var copy = new GeoEllipse3(new GeoPoint3(0, 0, 0.0012), new GeoVector3(0, -sine, Math.Sqrt(1 - sine * sine)), new GeoVector3(1, 0, 0), major, minor);

            Assert.True(flat.CollidesWith(copy, Thousandth));
            Assert.True(copy.CollidesWith(flat, Thousandth));

            GeoPoint3 place = Assert.Single(flat.GetIntersections(copy, Thousandth));
            Assert.Equal(new[] { place }, copy.GetIntersections(flat, Thousandth));
            Assert.InRange(place.DistanceTo(new GeoPoint3(0, -minor, 0.00005)), 0.0, 1E-4);
        }

        [Fact]
        public void LongThinEllipsesAtSiteCoordinatesPassingWithinTheTolerance_Touch()
        {
            // Sampled, the rims come within 0.00086 of each other; the search ran out of readings before it got there.
            var thin = new GeoEllipse3(
                new GeoPoint3(-36455.33311015709, 72114.49657199648, 5735.664887230687),
                new GeoVector3(0.4239588979957439, -0.530214382150887, 0.7342557876997551),
                new GeoVector3(0.14368442416164512, 0.8398305327795766, 0.5234877863565768),
                7659.816349698145,
                4.58786505224459);
            var long_ = new GeoEllipse3(
                new GeoPoint3(-36455.57569115682, 72492.56719221693, 6008.8120498170665),
                new GeoVector3(0.4239589038466236, -0.5302142719084062, 0.7342558639287861),
                new GeoVector3(0.13927069688458557, 0.8392459004048499, 0.5256139188063182),
                10145.45551228591,
                58.00502254240025);

            Assert.True(thin.CollidesWith(long_, Thousandth));
            Assert.True(long_.CollidesWith(thin, Thousandth));

            GeoPoint3[] places = thin.GetIntersections(long_, Thousandth);

            Assert.NotEmpty(places);
            Assert.All(places, place => AssertOnBothRims(thin, long_, place));
        }

        [Fact]
        public void TwoCrossingsOfAThinRimFewerThanTwoTolerancesApart_AreOnePlace()
        {
            var one = new GeoEllipse3(
                new GeoPoint3(22.913807850756598, 21.805376360102272, -24.95470856081448),
                new GeoVector3(0.5318332296246254, 0.7726475525098642, -0.34665425926643),
                new GeoVector3(-0.4385257964068879, 0.6014577833266956, 0.6677901322731885),
                207.95616739800022,
                0.2053652200847733);
            var two = new GeoEllipse3(
                new GeoPoint3(-136.35060810928087, 82.03102166864852, 5.393203627841014),
                new GeoVector3(0.20589695522585574, 0.8490567515713762, -0.48652757007160896),
                new GeoVector3(-0.965541160724889, 0.2571382443569285, 0.04012717576743644),
                148.10906513040376,
                0.1941016407415336);

            foreach (GeoPoint3[] places in new[] { one.GetIntersections(two, Thousandth), two.GetIntersections(one, Thousandth) })
            {
                Assert.NotEmpty(places);

                for (int i = 0; i < places.Length; i++)
                {
                    for (int j = i + 1; j < places.Length; j++)
                    {
                        Assert.True(places[i].DistanceTo(places[j]) > 2.0 * Thousandth.EqualPoint);
                    }
                }
            }
        }

        [Fact]
        public void TryIntersectWithOnTwoCircles_GivesWhatGetIntersectionsGivesEitherWayRound()
        {
            var one = new GeoCircle3(GeoPoint3.Origin, new GeoVector3(0, 0, 1), 473.706316190635);
            var two = new GeoCircle3(
                new GeoPoint3(-37.51983770053826, -84.16675933830754, 0.0008827762221371645),
                new GeoVector3(5.062732878271126E-07, -6.504263396403756E-07, 0.9999999999996604),
                477.6240549877398);

            Assert.True(one.TryIntersectWith(two, out GeoPoint3[] first, Thousandth));
            Assert.True(two.TryIntersectWith(one, out GeoPoint3[] second, Thousandth));
            Assert.Equal(one.GetIntersections(two, Thousandth), first);
            Assert.Equal(two.GetIntersections(one, Thousandth), second);
            Assert.Equal(2, first.Length);
        }

        [Fact]
        public void TwoCirclesOfOnePlaneWithNormalsOppositeWays_GiveTheirPointsInTheOppositeOrderAskedTheOtherWay()
        {
            var up = new GeoCircle3(GeoPoint3.Origin, new GeoVector3(0, 0, 1), 100);
            var down = new GeoCircle3(new GeoPoint3(60, 20, 0), new GeoVector3(0, 0, -1), 90);

            GeoPoint3[] first = up.GetIntersections(down, Thousandth);
            GeoPoint3[] second = down.GetIntersections(up, Thousandth);

            Assert.Equal(2, first.Length);
            Assert.Equal(first, new[] { second[1], second[0] });
        }

        [Fact]
        public void ABallCrossedByACircle_GivesTheCrossingsOfTheCircleItCutsEitherWayAsked()
        {
            // The ball of radius 100 about the origin cuts the plane z = 60 in the circle of radius 80; the circle of radius
            // 80 about (30, 0, 60) crosses that at x = 15.
            var ball = new GeoSphere3(GeoPoint3.Origin, 100);
            var circle = new GeoCircle3(new GeoPoint3(30, 0, 60), new GeoVector3(0, 0, 1), 80);
            double y = Math.Sqrt(80 * 80 - 15 * 15);

            GeoPoint3[] points = ball.GetIntersections(circle, Thousandth);

            Assert.Equal(2, points.Length);
            Assert.Contains(points, p => p.DistanceTo(new GeoPoint3(15, y, 60)) < 1E-9);
            Assert.Contains(points, p => p.DistanceTo(new GeoPoint3(15, -y, 60)) < 1E-9);
            Assert.Equal(points, circle.GetIntersections(ball, Thousandth));
        }
    }
}
