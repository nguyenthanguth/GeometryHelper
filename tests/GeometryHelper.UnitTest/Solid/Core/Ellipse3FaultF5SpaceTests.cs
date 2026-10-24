using System;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// Two ellipses in space not in one plane give the same places whichever is asked first, and touch where their rims pass
    /// within the point tolerance of each other without crossing: read only where the rim of the one asked first crossed the
    /// other's plane, 134 of 4 000 pairs drawn to pass within 0.0012 of each other collided one way and not the other, and
    /// 1 517 of them came within 0.00098 and collided neither way.
    /// </summary>
    public class Ellipse3FaultF5SpaceTests
    {
        private static readonly Tolerance Thousandth = Tolerance.Default;

        private static readonly GeoEllipse3 Flat = new GeoEllipse3(GeoPoint3.Origin, new GeoVector3(0, 0, 1), new GeoVector3(1, 0, 0), 300, 100);

        /// <summary>
        /// An ellipse 200 by 100 leaning 0.01 radians about the Y axis, the end of its major axis a height above (0, 100, 0),
        /// the top of the flat one's minor axis, where its rim runs along Y, square to the flat one's.
        /// </summary>
        private static GeoEllipse3 Leaning(double above)
        {
            double sine = Math.Sin(0.01);
            double cosine = Math.Cos(0.01);
            var axis = new GeoVector3(cosine, 0, -sine);

            return new GeoEllipse3(new GeoPoint3(0, 100, above).Subtract(axis.Multiply(200)), new GeoVector3(sine, 0, cosine), axis, 200, 100);
        }

        private static void AssertOnBothRims(GeoEllipse3 one, GeoEllipse3 other, GeoPoint3 place)
        {
            foreach (GeoEllipse3 ellipse in new[] { one, other })
            {
                double gap = Ellipse3Oracle.GapToRim(ellipse, place, out double offPlane);

                Assert.InRange(Math.Sqrt(gap * gap + offPlane * offPlane), 0.0, Thousandth.EqualPoint);
            }
        }

        [Theory]
        [InlineData(0.0005)]
        [InlineData(0.0009)]
        public void RimsPassingWithinTheToleranceSquareToEachOther_TouchAtTheMiddleOfTheGap(double above)
        {
            // The flat rim meets the leaning one's plane 0.05 to 0.09 along X from where the two pass, and the leaning rim
            // never comes down to the plan: there is no crossing to read, either way round.
            GeoEllipse3 leaning = Leaning(above);

            Assert.True(Flat.CollidesWith(leaning, Thousandth));
            Assert.True(leaning.CollidesWith(Flat, Thousandth));

            GeoPoint3[] one = Flat.GetIntersections(leaning, Thousandth);
            GeoPoint3[] other = leaning.GetIntersections(Flat, Thousandth);

            GeoPoint3 place = Assert.Single(one);
            Assert.Equal(one, other);
            Assert.Equal(0.0, place.DistanceTo(new GeoPoint3(0, 100, 0.5 * above)), 9);
        }

        [Theory]
        [InlineData(0.0011)]
        [InlineData(0.0015)]
        public void RimsPassingFurtherApartThanTheTolerance_DoNotTouch(double above)
        {
            GeoEllipse3 leaning = Leaning(above);

            Assert.False(Flat.CollidesWith(leaning, Thousandth));
            Assert.False(leaning.CollidesWith(Flat, Thousandth));
            Assert.Empty(Flat.GetIntersections(leaning, Thousandth));
            Assert.Empty(leaning.GetIntersections(Flat, Thousandth));
        }

        [Fact]
        public void EllipsesTiltedAFifthOfARadianCrossingAtSiteCoordinates_GiveTheSamePlaceWhicheverIsAskedFirst()
        {
            // Sampled, the rims come within 0.000013 of each other. Asked the one way, the first rim crossed the other's plane
            // 0.00006 off the other's rim; asked the other way, the second crossed the first's plane 0.0017 off the first rim,
            // and the pair did not collide.
            var one = new GeoEllipse3(
                new GeoPoint3(-37147.02061244613, -71885.78647183524, -1667.1560293376242),
                new GeoVector3(-0.08933003703984131, -0.6256422405582144, 0.7749786650687605),
                new GeoVector3(0.8169067253158232, -0.4911659108808543, -0.30235649508875945),
                143.21295162812476,
                83.24131518468458);
            var two = new GeoEllipse3(
                new GeoPoint3(-37229.71874314866, -71915.16981274236, -1706.4046278795947),
                new GeoVector3(0.022704551494178757, -0.7499175142622664, 0.6611416074822032),
                new GeoVector3(0.793912217324907, 0.4154468947174194, 0.4439676439245353),
                32.805011129381604,
                23.056648033724947);

            Assert.True(one.CollidesWith(two, Thousandth));
            Assert.True(two.CollidesWith(one, Thousandth));

            GeoPoint3[] first = one.GetIntersections(two, Thousandth);
            GeoPoint3[] second = two.GetIntersections(one, Thousandth);

            GeoPoint3 place = Assert.Single(first);
            Assert.Equal(first, second);
            AssertOnBothRims(one, two, place);
        }
    }
}
