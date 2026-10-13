using System;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Plane.SegmentFaultShapes;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// A segment collides with an open curve where they cross or come within the point tolerance, as two segments do.
    /// </summary>
    /// <remarks>
    /// The polyline asked only for crossings and whether the segment's start lay on it, so the segment from (0, 0) to
    /// (21, 0) over the polyline from (10, 0) to (20, 0) did not collide with it, though the two were nought apart. The
    /// arc, the curved edge and the chain asked only for crossings, so a segment ending half a thousandth short of an
    /// arc did not collide at a tolerance of a thousandth, and nor did a chain lying along a segment. The ray reads the
    /// same tests through its cut.
    /// </remarks>
    public class SegmentFaultB5OpenCurveOverlapTests
    {
        /// <summary>
        /// The segment 10 long coming down at 10 degrees from the upper left to (0, 50 + gap), straight above the top of
        /// <see cref="SegmentFaultShapes.UpperHalf"/>. Going on, its line would meet the circle about gap / sin 10 degrees
        /// further, 0.0029 past its end for a gap of 0.0005, so the two do not cross; the nearest pair is its end and
        /// (0, 50), gap apart.
        /// </summary>
        private static GeoLine2 EndingAboveTheTop(double gap)
        {
            double angle = 10 * Math.PI / 180;
            GeoPoint2 end = P(0, 50 + gap);

            return new GeoLine2(P(end.X - 10 * Math.Cos(angle), end.Y + 10 * Math.Sin(angle)), end);
        }

        [Fact]
        public void ASegmentRunningOverAPolylineAlongItsLength_CollidesWithIt()
        {
            // The segment from (0, 0) to (21, 0) holds the whole of the polyline from (10, 0) to (20, 0).
            var segment = new GeoLine2(P(0, 0), P(21, 0));
            var polyline = new GeoPolyline2(P(10, 0), P(20, 0));

            Assert.True(Collision2.CollidesWith(polyline, segment, Thousandth));
            Assert.True(polyline.CollidesWith(segment, Thousandth));
            Assert.True(segment.CollidesWith(polyline, Thousandth));
        }

        [Fact]
        public void ARayRunningOverAPolylineAlongItsLength_CollidesWithIt()
        {
            // The ray from the origin along X runs over the polyline from (10, 0) to (20, 0).
            var ray = new GeoRay2(P(0, 0), GeoVector2.XAxis);
            var polyline = new GeoPolyline2(P(10, 0), P(20, 0));

            Assert.Equal(0.0, ray.DistanceTo(polyline, Thousandth));
            Assert.True(ray.CollidesWith(polyline, Thousandth));
            Assert.True(polyline.CollidesWith(ray, Thousandth));
        }

        [Fact]
        public void ASegmentEndingFiveTenThousandthsShortOfAnArc_CollidesAtAThousandth()
        {
            // Half a thousandth apart, within the tolerance of a thousandth; asked of the arc, of the curved edge that is
            // the same arc, and of the chain of that one edge, from either side.
            GeoLine2 segment = EndingAboveTheTop(0.0005);
            GeoArc2 arc = UpperHalf();
            var edge = new GeoEdge2(arc);
            var chain = new GeoPolylineArc2(new[] { edge });

            Assert.Equal(0.0005, segment.DistanceTo(arc, Thousandth), 9);
            Assert.True(Collision2.CollidesWith(segment, arc, Thousandth));
            Assert.True(segment.CollidesWith(arc, Thousandth));
            Assert.True(arc.CollidesWith(segment, Thousandth));
            Assert.True(segment.CollidesWith(edge, Thousandth));
            Assert.True(edge.CollidesWith(segment, Thousandth));
            Assert.True(segment.CollidesWith(chain, Thousandth));
            Assert.True(chain.CollidesWith(segment, Thousandth));
        }

        [Fact]
        public void ARayStartingFiveTenThousandthsAboveAnArc_CollidesAtAThousandth()
        {
            // The ray from (0, 50.0005) going up and to the left at 10 degrees, away from the arc below it.
            double angle = 10 * Math.PI / 180;
            var ray = new GeoRay2(P(0, 50.0005), new GeoVector2(-Math.Cos(angle), Math.Sin(angle)));
            GeoArc2 arc = UpperHalf();

            Assert.Equal(0.0005, ray.DistanceTo(arc, Thousandth), 9);
            Assert.True(ray.CollidesWith(arc, Thousandth));
            Assert.True(arc.CollidesWith(ray, Thousandth));
        }

        [Fact]
        public void ASegmentEndingTwoThousandthsShortOfAnArc_DoesNotCollideAtAThousandth()
        {
            // Twice the tolerance apart.
            GeoLine2 segment = EndingAboveTheTop(0.002);
            GeoArc2 arc = UpperHalf();
            var edge = new GeoEdge2(arc);
            var chain = new GeoPolylineArc2(new[] { edge });

            Assert.Equal(0.002, segment.DistanceTo(arc, Thousandth), 9);
            Assert.False(segment.CollidesWith(arc, Thousandth));
            Assert.False(arc.CollidesWith(segment, Thousandth));
            Assert.False(edge.CollidesWith(segment, Thousandth));
            Assert.False(chain.CollidesWith(segment, Thousandth));
        }

        [Fact]
        public void ACurvedChainLyingAlongASegment_CollidesWithIt()
        {
            // The chain runs straight from (10, 0) to (20, 0), then bows up to (30, 10); the segment from (12, 0) to
            // (18, 0) lies on its straight edge, two from either end, and nowhere near the arc.
            var chain = new GeoPolylineArc2(new[] { P(10, 0), P(20, 0), P(30, 10) }, new[] { 0.0, 0.3 });
            var segment = new GeoLine2(P(12, 0), P(18, 0));

            Assert.Equal(0.0, segment.DistanceTo(chain, Thousandth));
            Assert.True(Collision2.CollidesWith(chain, segment, Thousandth));
            Assert.True(chain.CollidesWith(segment, Thousandth));
            Assert.True(segment.CollidesWith(chain, Thousandth));
        }

        [Fact]
        public void ARayRunningBackAlongTheStraightEdgeOfACurvedChain_CollidesWithIt()
        {
            // From (18, 0) along -X: over the straight edge and off the chain's open end at (10, 0), away from the arc.
            var chain = new GeoPolylineArc2(new[] { P(10, 0), P(20, 0), P(30, 10) }, new[] { 0.0, 0.3 });
            var ray = new GeoRay2(P(18, 0), -GeoVector2.XAxis);

            Assert.Equal(0.0, ray.DistanceTo(chain, Thousandth));
            Assert.True(ray.CollidesWith(chain, Thousandth));
            Assert.True(chain.CollidesWith(ray, Thousandth));
        }
    }
}
