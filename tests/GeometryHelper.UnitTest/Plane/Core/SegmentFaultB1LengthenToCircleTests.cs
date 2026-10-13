using System;
using GeometryHelper.Core;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Plane.SegmentFaultShapes;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// A segment extended or trimmed to a circle stops where its line truly crosses the rim, also when the line runs
    /// within the point tolerance inside it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The crossings of a segment's line with a circle, for extending and trimming, kept their own copy of the circle's
    /// tangent band: a line whose distance from the centre lay within the point tolerance of the radius met the circle
    /// once, at the foot of the perpendicular. The line y = 49.9995 runs half a thousandth inside the rim of the circle
    /// of radius 50 about the origin and crosses it at x = -0.2236 and 0.2236, the roots of 2500 - 49.9995 squared =
    /// 0.04999975; extended or trimmed at a tolerance of a thousandth it stopped at the foot, (0, 49.9995), 0.2236 from
    /// either crossing.
    /// </para>
    /// <para>
    /// The two crossings of a line inside the rim are kept now, and merged into the foot only when they lie within twice
    /// the point tolerance of each other, as the circle's own intersections do. The line y = 50 - h crosses at
    /// x = -c and c with c = root(2500 - y squared): 0.00075 for y = 49.999999994375, merged, and 0.005 for
    /// y = 49.99999975, kept.
    /// </para>
    /// </remarks>
    public class SegmentFaultB1LengthenToCircleTests
    {
        /// <summary>Where the line y = 49.9995 crosses the rim: x = ±root(0.04999975).</summary>
        private const double HalfChord = 0.22360623873228582;

        /// <summary>The segment from (x0, y) to (x1, y).</summary>
        private static GeoLine2 Along(double y, double x0, double x1) => new GeoLine2(P(x0, y), P(x1, y));

        [Fact]
        public void ExtendingAlongALineJustInsideTheRim_StopsWhereItEntersTheCircle()
        {
            // From (-20, 49.9995) to (-10, 49.9995), outside the circle, its end moved on along +X: it enters at
            // x = -0.2236, not at the foot x = 0.
            GeoLine2 segment = Along(49.9995, -20, -10);

            Assert.True(Lengthen2.TryExtendTo(segment, Circle(), LineEnd.End, out GeoLine2 extended, Thousandth));
            Assert.Equal(0.0, extended.EndPoint.DistanceTo(P(-HalfChord, 49.9995)), 9);
            Assert.Equal(0.0, extended.StartPoint.DistanceTo(P(-20, 49.9995)), 12);

            Assert.True(segment.TryExtendTo(Circle(), LineEnd.End, out GeoLine2 fromTheSegment, Thousandth));
            Assert.Equal(0.0, fromTheSegment.EndPoint.DistanceTo(P(-HalfChord, 49.9995)), 9);
        }

        [Fact]
        public void ExtendingTheStartAlongALineJustInsideTheRim_StopsWhereItEntersTheCircle()
        {
            // From (10, 49.9995) to (20, 49.9995), its start moved back along -X: it enters at x = 0.2236.
            GeoLine2 segment = Along(49.9995, 10, 20);

            Assert.True(Lengthen2.TryExtendTo(segment, Circle(), LineEnd.Start, out GeoLine2 extended, Thousandth));
            Assert.Equal(0.0, extended.StartPoint.DistanceTo(P(HalfChord, 49.9995)), 9);
            Assert.Equal(0.0, extended.EndPoint.DistanceTo(P(20, 49.9995)), 12);
        }

        [Fact]
        public void TrimmingALineJustInsideTheRim_StopsAtTheCrossingNearestTheEnd()
        {
            // From (-20, 49.9995) to (20, 49.9995), its end trimmed back: the last crossing before it is x = 0.2236.
            GeoLine2 segment = Along(49.9995, -20, 20);

            Assert.True(Lengthen2.TryTrimTo(segment, Circle(), LineEnd.End, out GeoLine2 trimmed, Thousandth));
            Assert.Equal(0.0, trimmed.EndPoint.DistanceTo(P(HalfChord, 49.9995)), 9);

            Assert.True(segment.TryTrimTo(Circle(), LineEnd.End, out GeoLine2 fromTheSegment, Thousandth));
            Assert.Equal(0.0, fromTheSegment.EndPoint.DistanceTo(P(HalfChord, 49.9995)), 9);
        }

        [Fact]
        public void TrimmingTheStartOfALineJustInsideTheRim_StopsAtTheCrossingNearestTheStart()
        {
            // The same segment, its start trimmed forward: the first crossing after it is x = -0.2236.
            GeoLine2 segment = Along(49.9995, -20, 20);

            Assert.True(Lengthen2.TryTrimTo(segment, Circle(), LineEnd.Start, out GeoLine2 trimmed, Thousandth));
            Assert.Equal(0.0, trimmed.StartPoint.DistanceTo(P(-HalfChord, 49.9995)), 9);
        }

        [Fact]
        public void ExtendingAlongALineWhoseCrossingsLieAHundredthApart_StopsAtTheFirst()
        {
            // y = root(2500 - 0.005 squared) = 49.99999975: crossings at x = -0.005 and 0.005, five times the tolerance
            // apart, so the end stops at the first, 0.005 short of the foot.
            double y = Math.Sqrt(2500 - 0.005 * 0.005);
            GeoLine2 segment = Along(y, -20, -10);

            Assert.True(Lengthen2.TryExtendTo(segment, Circle(), LineEnd.End, out GeoLine2 extended, Thousandth));
            Assert.Equal(0.0, extended.EndPoint.DistanceTo(P(-0.005, y)), 9);
        }

        [Fact]
        public void ExtendingAlongALineWhoseCrossingsLieFifteenTenThousandthsApart_StopsAtTheFoot()
        {
            // y = root(2500 - 0.00075 squared) = 49.999999994375: crossings at x = -0.00075 and 0.00075, within twice the
            // tolerance of each other, one point at the foot.
            double y = Math.Sqrt(2500 - 0.00075 * 0.00075);
            GeoLine2 segment = Along(y, -20, -10);

            Assert.True(Lengthen2.TryExtendTo(segment, Circle(), LineEnd.End, out GeoLine2 extended, Thousandth));
            Assert.Equal(0.0, extended.EndPoint.DistanceTo(P(0, y)), 9);
        }

        [Fact]
        public void ExtendingAlongATangentFourTenThousandthsOutsideTheRim_StopsAtTheFoot()
        {
            // The line y = 50.0004 passes outside the circle within the tolerance of its top: it touches once, at (0, 50.0004).
            GeoLine2 segment = Along(50.0004, -20, -10);

            Assert.True(Lengthen2.TryExtendTo(segment, Circle(), LineEnd.End, out GeoLine2 extended, Thousandth));
            Assert.Equal(0.0, extended.EndPoint.DistanceTo(P(0, 50.0004)), 12);
        }

        [Fact]
        public void TrimmingAlongATangentFourTenThousandthsOutsideTheRim_StopsAtTheFoot()
        {
            GeoLine2 segment = Along(50.0004, -20, 20);

            Assert.True(Lengthen2.TryTrimTo(segment, Circle(), LineEnd.End, out GeoLine2 trimmed, Thousandth));
            Assert.Equal(0.0, trimmed.EndPoint.DistanceTo(P(0, 50.0004)), 12);
        }
    }
}
