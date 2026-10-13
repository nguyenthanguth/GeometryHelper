using System;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Plane.SegmentFaultShapes;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// A segment whose line runs within the point tolerance inside a circle's rim crosses it where it truly does.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Where the centre lay within the point tolerance of the radius from the segment's line, the crossing was put at the
    /// foot of the perpendicular, the one point of a tangent. A segment starting just inside the rim of the circle of
    /// radius 50 and running nearly along it, from (0, 49.9995) along (1, 0.001), has its line 49.999475 from the centre,
    /// in that band, and its foot 0.05 behind its start. It was given no crossing, though it leaves the circle 0.179
    /// along, at (0.1791, 49.99968), and its shortest line to the circle ran 20.7 from its far end. The arc on that circle
    /// did the same: no crossing, no collision and half a thousandth away.
    /// </para>
    /// <para>
    /// The two crossings of a line inside the rim are kept now, each where it is, and merged into the foot only when they
    /// lie within twice the point tolerance of each other, the circle's rule for two crossings that are one point. The
    /// crossings of the line y = 50 - h lie 2 root (100 h) apart, nearly: 0.0015 apart for h = 5.625E-9 and 0.01 apart
    /// for h = 2.5E-7.
    /// </para>
    /// </remarks>
    public class SegmentFaultB1CircleBandTests
    {
        /// <summary>
        /// Where <see cref="SegmentFaultShapes.NearlyAlongTheRim"/> leaves the circle: s squared + 2 b s - 0.04999975 = 0
        /// along it, with b = 49.9995 times 0.001 over the root of 1.000001, gives s = 0.17912864963811452.
        /// </summary>
        private static readonly GeoPoint2 Leaving = P(0.17912856007385688, 49.999679128560074);

        /// <summary>The segment from (-10, y) to (10, y).</summary>
        private static GeoLine2 Across(double y) => new GeoLine2(P(-10, y), P(10, y));

        [Fact]
        public void ASegmentStartingJustInsideTheRimAndRunningNearlyAlongIt_LeavesTheCircleWhereItCrossesIt()
        {
            // The other crossing, 0.279 behind the start, is not on the segment.
            GeoCircle2 circle = Circle();
            GeoLine2 segment = NearlyAlongTheRim();

            GeoPoint2 crossing = Assert.Single(segment.GetIntersections(circle, Thousandth));
            Assert.Equal(0.0, crossing.DistanceTo(Leaving), 9);
            Assert.True(Intersection2.TryIntersectWith(circle, segment, out GeoPoint2[] fromTheCircle, Thousandth));
            Assert.Equal(0.0, Assert.Single(fromTheCircle).DistanceTo(Leaving), 9);
        }

        [Fact]
        public void ItsShortestLineToTheCircle_HasNoLengthWhereItCrosses()
        {
            GeoCircle2 circle = Circle();
            GeoLine2 segment = NearlyAlongTheRim();

            GeoLine2 fromSegment = segment.GetShortestLineTo(circle, Thousandth);
            GeoLine2 fromCircle = circle.GetShortestLineTo(segment, Thousandth);

            Assert.Equal(0.0, fromSegment.Length, 9);
            Assert.Equal(0.0, fromSegment.StartPoint.DistanceTo(Leaving), 9);
            Assert.Equal(0.0, fromCircle.Length, 9);
            Assert.Equal(0.0, fromCircle.StartPoint.DistanceTo(Leaving), 9);
        }

        [Fact]
        public void TheRayFromTheSameStart_LeavesTheCircleThereToo()
        {
            GeoCircle2 circle = Circle();
            GeoRay2 ray = RayNearlyAlongTheRim();

            GeoPoint2 crossing = Assert.Single(ray.GetIntersections(circle, Thousandth));
            Assert.Equal(0.0, crossing.DistanceTo(Leaving), 9);

            GeoLine2 shortest = ray.GetShortestLineTo(circle, Thousandth);
            Assert.Equal(0.0, shortest.Length, 9);
            Assert.Equal(0.0, shortest.StartPoint.DistanceTo(Leaving), 9);
            Assert.Equal(0.0, ray.DistanceTo(circle, Thousandth));
        }

        [Fact]
        public void TheSameSegmentAgainstTheUpperHalfOfTheCircle_MeetsItWhereItCrosses()
        {
            // The crossing, at 89.8 degrees, is on the arc from 0 to 180 degrees.
            GeoArc2 arc = UpperHalf();
            GeoLine2 segment = NearlyAlongTheRim();

            GeoPoint2 crossing = Assert.Single(segment.GetIntersections(arc, Thousandth));
            Assert.Equal(0.0, crossing.DistanceTo(Leaving), 9);
            Assert.True(segment.CollidesWith(arc, Thousandth));
            Assert.Equal(0.0, segment.DistanceTo(arc, Thousandth), 12);
            Assert.Equal(0.0, segment.GetShortestLineTo(arc, Thousandth).Length, 9);
        }

        [Fact]
        public void ATangentFourTenThousandthsOutsideTheRim_IsStillOnePointAtTheFoot()
        {
            // The line y = 50.0004 runs outside the circle, within the tolerance of its top.
            GeoPoint2 touch = Assert.Single(Across(50.0004).GetIntersections(Circle(), Thousandth));

            Assert.Equal(0.0, touch.DistanceTo(P(0, 50.0004)), 12);
        }

        [Fact]
        public void ASecantWhoseCrossingsLieFifteenTenThousandthsApart_IsStillOnePointAtTheFoot()
        {
            // y = root(2500 - 0.00075 squared) = 49.999999994375: crossings at x = -0.00075 and 0.00075, within twice the
            // tolerance of each other.
            double y = Math.Sqrt(2500 - 0.00075 * 0.00075);

            GeoPoint2 touch = Assert.Single(Across(y).GetIntersections(Circle(), Thousandth));

            Assert.Equal(0.0, touch.DistanceTo(P(0, y)), 12);
        }

        [Fact]
        public void ASecantWhoseCrossingsLieAHundredthApart_IsTwoPoints()
        {
            // y = root(2500 - 0.005 squared) = 49.99999975: crossings at x = -0.005 and 0.005, five times the tolerance
            // apart.
            double y = Math.Sqrt(2500 - 0.005 * 0.005);

            GeoPoint2[] crossings = Across(y).GetIntersections(Circle(), Thousandth);

            Assert.Equal(2, crossings.Length);
            Assert.Contains(crossings, p => p.DistanceTo(P(-0.005, y)) < 1E-9);
            Assert.Contains(crossings, p => p.DistanceTo(P(0.005, y)) < 1E-9);
        }
    }
}
