using System;
using GeometryHelper.Core;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Plane.SegmentFaultShapes;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// The tolerance forms of the segment queries read the tolerance they are given all the way down.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The distance between two segments projected each end onto the other with the default tolerance, and so did the
    /// distance between an arc and a segment. A sliver of 0.0004, shorter than the global 1E-3, was read as the point at
    /// its start even when the query was given 1E-4, under which the sliver is a segment.
    /// </para>
    /// <para>
    /// Each end of one segment was measured to the other read as its start, so two slivers in a row lost the pair of
    /// ends that are nearest: from (0, 0) to (0.0004, 0) and from (5.0008, 0) back to (5.0004, 0), 5 apart, measured
    /// 5.0004. Against a segment of length, one sliver is out by the second order only, since the other three ends are
    /// measured right: the sliver against the segment from (0.0002, 5) to (0.0002, 10) measured the root of 25 + 0.0002
    /// squared, 5.000000004, instead of 5, the most a sliver of 0.0004 can be out by at 5 away; those distances are
    /// compared to twelve places, where the right answer is 5 to the last bit or two. <see cref="Tolerance.Global"/> is
    /// left at its default in every test.
    /// </para>
    /// </remarks>
    public class SegmentFaultB4SliverToleranceTests
    {
        /// <summary>The sliver from (0, 0) to (0.0004, 0).</summary>
        private static GeoLine2 Sliver() => new GeoLine2(P(0, 0), P(0.0004, 0));

        [Fact]
        public void TwoSliversFiveApartEndToEnd_AreFiveApartAtATenThousandth()
        {
            // The nearest ends are (0.0004, 0) and (5.0004, 0), the end of the first and the end of the second, 5 apart.
            // Read each as the point at its start, the nearest pair left is 5.0004 apart.
            GeoLine2 first = Sliver();
            var second = new GeoLine2(P(5.0008, 0), P(5.0004, 0));

            Assert.Equal(5.0, Distance2.DistanceTo(first, second, TenThousandth), 9);
            Assert.Equal(5.0, Distance2.DistanceTo(second, first, TenThousandth), 9);
            Assert.Equal(5.0, first.DistanceTo(second, TenThousandth), 9);
        }

        [Fact]
        public void ASliverFiveFromTheEndOfASegment_IsFiveAwayAtATenThousandth()
        {
            // The segment rises from (0.0002, 5), straight above the sliver's middle, so the nearest pair is that end and
            // (0.0002, 0): 5. Each end of the sliver is the root of 25 + 4E-8 away, 5.000000004.
            GeoLine2 sliver = Sliver();
            var above = new GeoLine2(P(0.0002, 5), P(0.0002, 10));

            Assert.Equal(5.0, Distance2.DistanceTo(sliver, above, TenThousandth), 12);
            Assert.Equal(5.0, Distance2.DistanceTo(above, sliver, TenThousandth), 12);
            Assert.Equal(5.0, sliver.DistanceTo(above, TenThousandth), 12);
            Assert.Equal(5.0, above.DistanceTo(sliver, TenThousandth), 12);
        }

        [Fact]
        public void ASliverFiveFromTheBottomOfAnArc_IsFiveAwayAtATenThousandth()
        {
            // The lower half of the circle of radius 50 about (0.0002, 55) comes down to (0.0002, 5), straight above the
            // sliver's middle: 55 from the centre less the radius, 5. Each end of the sliver is the root of 55 squared +
            // 4E-8 from the centre, 5.00000000036 from the arc.
            GeoLine2 sliver = Sliver();
            var arc = new GeoArc2(P(0.0002, 55), 50, Math.PI, 2 * Math.PI);

            Assert.Equal(5.0, Arc2.DistanceTo(arc, sliver, TenThousandth), 12);
            Assert.Equal(5.0, sliver.DistanceTo(arc, TenThousandth), 12);
            Assert.Equal(5.0, arc.DistanceTo(sliver, TenThousandth), 12);
        }

        [Fact]
        public void APointBesideTheFarEndOfASliver_IsOnItAtATenThousandth()
        {
            // (0.0004, 0.00005) is half a ten-thousandth from the sliver's end, and 0.0004 from its start, where the
            // sliver read as a point puts it.
            GeoLine2 sliver = Sliver();
            GeoPoint2 point = P(0.0004, 0.00005);

            Assert.True(Containment2.IsPointOn(sliver, point, TenThousandth));
            Assert.True(sliver.IsPointOn(point, TenThousandth));
            Assert.Equal(PointLocation.OnSide, Containment2.Locate(sliver, point, TenThousandth));
        }
    }
}
