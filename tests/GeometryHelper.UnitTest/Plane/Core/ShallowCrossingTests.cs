using System;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// Two members crossing at a shallow angle cross at a point.
    /// </summary>
    /// <remarks>
    /// The crossing was refused as parallel whenever the angle between the two was under the angle tolerance,
    /// a whole degree, however long the members. Two ten-metre members crossing at their middles at 0.9 degrees
    /// — 157 apart at their ends — crossed nowhere, while CollidesWith said true and DistanceTo said nought.
    /// Two members are parallel now when they draw apart by less than the point tolerance along the longer of
    /// them; only two infinite lines, with no length to measure over, still go by the angle.
    /// </remarks>
    public class ShallowCrossingTests
    {
        private static GeoLine2 Member(double length, double angleDegrees)
        {
            double angle = angleDegrees * Math.PI / 180.0;
            var half = new GeoVector2(Math.Cos(angle) * length / 2, Math.Sin(angle) * length / 2);

            return new GeoLine2(new GeoPoint2(-half.X, -half.Y), new GeoPoint2(half.X, half.Y));
        }

        [Theory]
        [InlineData(5.0)]
        [InlineData(0.9)]
        [InlineData(0.5)]
        [InlineData(0.2)]
        [InlineData(0.05)]
        public void TenMetreMembersCrossWhereverTheyCross(double angleDegrees)
        {
            GeoLine2 first = Member(10000, 0);
            GeoLine2 second = Member(10000, angleDegrees);

            GeoPoint2? crossing = first.GetIntersection(second);

            Assert.True(crossing.HasValue);
            Assert.True(crossing.Value.DistanceTo(GeoPoint2.Origin) < 1E-6);
            Assert.True(first.CollidesWith(second));
            Assert.Equal(0.0, first.DistanceTo(second), 9);
        }

        [Fact]
        public void MembersCloserThanTheToleranceAllAlongAreStillParallel()
        {
            // Ten metres drawing apart by half a ten-thousandth: one line to within the tolerance, so there is
            // no single point to give.
            GeoLine2 first = Member(10000, 0);
            GeoLine2 second = Member(10000, 5E-9 * 180.0 / Math.PI);

            Assert.False(first.TryIntersectWith(second, out _));
        }

        [Fact]
        public void NearlyParallelMembersThatDoNotReachEachOtherDoNotCross()
        {
            GeoLine2 first = Member(10000, 0);
            GeoLine2 beside = Member(10000, 0.5).Translate(new GeoVector2(0, 200));

            Assert.False(first.TryIntersectWith(beside, out _));
            Assert.False(first.CollidesWith(beside));
        }

        [Fact]
        public void AShortMemberReachesALongOneReadAsItsLine()
        {
            // The short member drawn, the long one read as its infinite line: the crossing is judged over the
            // short one's length.
            GeoLine2 axis = Member(100, 0);
            GeoLine2 brace = new GeoLine2(new GeoPoint2(4000, -10), new GeoPoint2(6000, 10));

            Assert.True(axis.TryIntersectWith(brace, LineExtension.First, out GeoPoint2 crossing));
            Assert.Equal(5000.0, crossing.X, 6);
            Assert.Equal(0.0, crossing.Y, 6);
        }

        [Fact]
        public void TwoInfiniteLinesStillGoByTheAngle()
        {
            // Read as infinite lines, the two have no length to measure over.
            GeoLine2 first = Member(100, 0);
            GeoLine2 second = Member(100, 0.5).Translate(new GeoVector2(0, 1));

            Assert.False(first.TryIntersectWith(second, LineExtension.Both, out _));
        }
    }
}
