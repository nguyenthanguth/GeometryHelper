using System;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// FILLET with two objects picked: an arc tangent to both segments, with each of them trimmed or
    /// extended to where it touches. The arc is checked by what tangency means — it stands the radius away
    /// from each line, and touches each exactly once — rather than by coordinates worked out by hand.
    /// </summary>
    public class FilletCornerTests
    {
        private static readonly Tolerance Tight = new Tolerance(1E-8, 1E-8);

        private static void AssertTangent(GeoArc2 arc, GeoLine2 trimmed, GeoPoint2 touch, double radius)
        {
            // The touching point is on the arc and at the end of the trimmed segment.
            Assert.True(arc.IsPointOn(touch));
            Assert.Equal(0.0, Distance2.DistanceTo(trimmed, touch), 8);

            // The centre stands the radius away from the line carrying the segment.
            Assert.Equal(radius, arc.Center.DistanceTo(Projection2.ProjectToInfiniteLine(trimmed, arc.Center)), 8);

            // And the radius to the touching point is square to the segment.
            GeoVector2 toTouch = arc.Center.GetVectorTo(touch);
            Assert.True(toTouch.IsPerpendicularTo(trimmed.Direction, new Tolerance(1E-6, 1E-6)));
        }

        [Fact]
        public void ARightAngleIsRoundedWithAQuarterArc()
        {
            var along = new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(100, 0));
            var up = new GeoLine2(new GeoPoint2(100, 0), new GeoPoint2(100, 80));

            Assert.True(Lengthen2.TryFilletCorner(along, up, 20.0, out GeoArc2 arc, out GeoLine2 first, out GeoLine2 second));

            // At a right angle the touching points sit exactly the radius back from the corner.
            Assert.True(first.EndPoint.IsEqualTo(new GeoPoint2(80, 0), Tight));
            Assert.True(second.StartPoint.IsEqualTo(new GeoPoint2(100, 20), Tight));
            Assert.True(arc.Center.IsEqualTo(new GeoPoint2(80, 20), Tight));

            Assert.Equal(20.0, arc.Radius, 9);
            Assert.Equal(Math.PI / 2.0, Math.Abs(arc.SweptAngle), 9);
            Assert.Equal(Math.PI / 2.0 * 20.0, arc.Length, 9);

            AssertTangent(arc, first, first.EndPoint, 20.0);
            AssertTangent(arc, second, second.StartPoint, 20.0);

            // The arc runs from the first segment to the second.
            Assert.True(arc.StartPoint.IsEqualTo(first.EndPoint, Tight));
            Assert.True(arc.EndPoint.IsEqualTo(second.StartPoint, Tight));
        }

        [Theory]
        [InlineData(0.3, 5.0)]
        [InlineData(1.0, 12.0)]
        [InlineData(2.0, 8.0)]
        [InlineData(2.8, 2.0)]
        public void TheArcIsTangentToBothSegmentsWhateverTheCornerAndRadius(double turn, double radius)
        {
            var first = new GeoLine2(new GeoPoint2(-60, 0), new GeoPoint2(0, 0));
            var second = new GeoLine2(GeoPoint2.Origin, new GeoPoint2(60.0 * Math.Cos(turn), 60.0 * Math.Sin(turn)));

            Assert.True(Lengthen2.TryFilletCorner(first, second, radius, out GeoArc2 arc, out GeoLine2 trimmed1, out GeoLine2 trimmed2));

            Assert.Equal(radius, arc.Radius, 9);
            AssertTangent(arc, trimmed1, arc.StartPoint, radius);
            AssertTangent(arc, trimmed2, arc.EndPoint, radius);

            // The arc turns through as much as the second segment turns away from the first.
            Assert.Equal(turn, Math.Abs(arc.SweptAngle), 8);

            // The two segments keep the parts away from the corner.
            Assert.True(trimmed1.StartPoint.IsEqualTo(first.StartPoint, Tight));
            Assert.True(trimmed2.EndPoint.IsEqualTo(second.EndPoint, Tight));
        }

        [Fact]
        public void SegmentsThatDoNotReachTheCornerAreExtendedToIt()
        {
            // Neither segment reaches the corner at (100, 0).
            var along = new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(60, 0));
            var up = new GeoLine2(new GeoPoint2(100, 40), new GeoPoint2(100, 90));

            Assert.True(Lengthen2.TryFilletCorner(along, up, 10.0, out GeoArc2 arc, out GeoLine2 first, out GeoLine2 second));

            Assert.True(first.EndPoint.IsEqualTo(new GeoPoint2(90, 0), Tight));
            Assert.True(second.StartPoint.IsEqualTo(new GeoPoint2(100, 10), Tight));
            AssertTangent(arc, first, arc.StartPoint, 10.0);
            AssertTangent(arc, second, arc.EndPoint, 10.0);
        }

        [Fact]
        public void ARadiusTooLargeForTheCornerIsRefused()
        {
            var along = new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(30, 0));
            var up = new GeoLine2(new GeoPoint2(30, 0), new GeoPoint2(30, 25));

            // A right angle needs the radius back along each segment; 40 is longer than either.
            Assert.False(Lengthen2.TryFilletCorner(along, up, 40.0, out _, out GeoLine2 first, out GeoLine2 second));

            // And the segments come back untouched.
            Assert.True(first.IsEqualTo(along));
            Assert.True(second.IsEqualTo(up));

            // The largest radius that fits here is 25, the shorter of the two.
            Assert.True(Lengthen2.TryFilletCorner(along, up, 24.9, out _, out _, out _));
        }

        [Fact]
        public void ParallelSegmentsAndDegenerateOnesAreRefused()
        {
            var along = new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(100, 0));
            var parallel = new GeoLine2(new GeoPoint2(0, 20), new GeoPoint2(100, 20));
            var point = new GeoPoint2(50, 50);

            Assert.False(Lengthen2.TryFilletCorner(along, parallel, 5.0, out _, out _, out _));
            Assert.False(Lengthen2.TryFilletCorner(along, new GeoLine2(point, point), 5.0, out _, out _, out _));

            // Straight on is a corner of no turn, which has no arc either.
            Assert.False(Lengthen2.TryFilletCorner(along, new GeoLine2(new GeoPoint2(100, 0), new GeoPoint2(200, 0)), 5.0, out _, out _, out _));

            Assert.Throws<ArgumentOutOfRangeException>(() => Lengthen2.TryFilletCorner(along, parallel, 0.0, out _, out _, out _));
            Assert.Throws<ArgumentOutOfRangeException>(() => Lengthen2.TryFilletCorner(along, parallel, -5.0, out _, out _, out _));
        }

        [Fact]
        public void AFilletOfNoRadiusIsTheCornerItself()
        {
            var along = new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(80, 0));
            var up = new GeoLine2(new GeoPoint2(100, 20), new GeoPoint2(100, 90));

            // The radius-zero case is the method that was already there, and the two agree about the corner.
            Assert.True(Lengthen2.TryTrimExtendToCorner(along, up, out GeoLine2 sharp1, out GeoLine2 sharp2));
            Assert.True(Lengthen2.TryFilletCorner(along, up, 5.0, out GeoArc2 arc, out GeoLine2 round1, out GeoLine2 round2));

            Assert.True(sharp1.EndPoint.IsEqualTo(new GeoPoint2(100, 0), Tight));
            Assert.True(sharp2.StartPoint.IsEqualTo(new GeoPoint2(100, 0), Tight));

            // The rounded pair stops short of that corner by the same amount on both sides.
            Assert.Equal(5.0, round1.EndPoint.DistanceTo(sharp1.EndPoint), 8);
            Assert.Equal(5.0, round2.StartPoint.DistanceTo(sharp2.StartPoint), 8);
            Assert.Equal(Math.PI / 2.0 * 5.0, arc.Length, 8);
        }

        [Fact]
        public void TheSameCornerFilletsTheSameWayWhicheverSegmentComesFirst()
        {
            var first = new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(50, 0));
            var second = new GeoLine2(new GeoPoint2(50, 0), new GeoPoint2(50, 50));

            Assert.True(Lengthen2.TryFilletCorner(first, second, 15.0, out GeoArc2 oneWay, out _, out _));
            Assert.True(Lengthen2.TryFilletCorner(second, first, 15.0, out GeoArc2 otherWay, out _, out _));

            // The same arc, drawn the other way round.
            Assert.True(oneWay.Center.IsEqualTo(otherWay.Center, Tight));
            Assert.Equal(oneWay.Radius, otherWay.Radius, 9);
            Assert.Equal(oneWay.Length, otherWay.Length, 8);
            Assert.True(oneWay.IsEqualTo(otherWay.Reverse(), new Tolerance(1E-7, 1E-7)));
        }
    }
}
