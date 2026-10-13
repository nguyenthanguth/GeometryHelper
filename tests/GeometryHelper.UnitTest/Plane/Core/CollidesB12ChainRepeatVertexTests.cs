using System;
using GeometryHelper.Geometry;
using Xunit;
using static GeometryHelper.UnitTest.Plane.SegmentFaultShapes;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// A curved chain or loop that drops a vertex as a repeat of the one before keeps the bulge of the edge that has a
    /// length.
    /// </summary>
    /// <remarks>
    /// Two vertices within the global point tolerance leave an edge with no length, and the constructor drops the later
    /// one. It kept the earlier one's bulge, which is the bulge of the edge that vanishes; the bulge it threw away belonged
    /// to the edge running on from the dropped vertex, which survives. So in the chain (0, 0), (10, 0), (10.0004, 0),
    /// (20, 0) with bulges 0, 0, 1 the half circle from (10.0004, 0) to (20, 0) became the straight chord, and with the
    /// bulges 0, 1, 0 the straight edge became a half circle. Dropping the vertex on the global tolerance is by design and
    /// stays.
    /// </remarks>
    public class CollidesB12ChainRepeatVertexTests
    {
        [Fact]
        public void AHalfCircleAfterARepeatedVertex_IsKept()
        {
            // (10.0004, 0) repeats (10, 0) four ten-thousandths on; the edge from it to (20, 0) is the half circle.
            var chain = new GeoPolylineArc2(new[] { P(0, 0), P(10, 0), P(10.0004, 0), P(20, 0) }, new[] { 0.0, 0.0, 1.0 });

            Assert.Equal(3, chain.VertexCount);
            Assert.Equal(0.0, chain.GetEdgeAt(0).Bulge);
            Assert.Equal(1.0, chain.GetEdgeAt(1).Bulge);
        }

        [Fact]
        public void ASegmentAcrossTheKeptHalfCircle_CollidesWithIt()
        {
            // The half circle from (10, 0) to (20, 0) bulging 1 bows to the right of its way, down, to (15, -5). The
            // segment from (15.0002, -4.9998) to (15.0002, -6) runs down through it at y = -5. On 2755f30 the chord was
            // left instead, 4.9998 above the segment's start.
            var chain = new GeoPolylineArc2(new[] { P(0, 0), P(10, 0), P(10.0004, 0), P(20, 0) }, new[] { 0.0, 0.0, 1.0 });
            var segment = new GeoLine2(P(15.0002, -4.9998), P(15.0002, -6));

            Assert.True(chain.CollidesWith(segment, Thousandth));
            Assert.True(segment.CollidesWith(chain, Thousandth));
            Assert.Equal(0.0, chain.DistanceTo(segment, Thousandth));
        }

        [Fact]
        public void AStraightEdgeAfterARepeatedCurvedOne_StaysStraight()
        {
            // The bulge 1 is on the edge of four ten-thousandths that vanishes; the edge from (10.0004, 0) to (20, 0) is
            // straight, so the chain stays on the X axis and the segment from (15, -4.9) to (15, -6) is 4.9 below it. On
            // 2755f30 the bulge moved onto the long edge, a half circle down to (15, -5), which the segment crossed.
            var chain = new GeoPolylineArc2(new[] { P(0, 0), P(10, 0), P(10.0004, 0), P(20, 0) }, new[] { 0.0, 1.0, 0.0 });
            var segment = new GeoLine2(P(15, -4.9), P(15, -6));

            Assert.Equal(0.0, chain.GetEdgeAt(1).Bulge);
            Assert.False(chain.CollidesWith(segment, Thousandth));
            Assert.False(segment.CollidesWith(chain, Thousandth));
            Assert.Equal(4.9, chain.DistanceTo(segment, Thousandth), 1E-12);
        }

        [Fact]
        public void ARepeatAtTheStartOfAChain_KeepsTheBulgeOfTheEdgeAfterIt()
        {
            // (0.0004, 0) repeats the start; the edge from it to (10, 0) bulges 1: after the drop, the half circle from
            // (0, 0) to (10, 0) down to (5, -5), which the segment from (5, -4.9) to (5, -6) crosses.
            var chain = new GeoPolylineArc2(new[] { P(0, 0), P(0.0004, 0), P(10, 0) }, new[] { 0.0, 1.0 });
            var segment = new GeoLine2(P(5, -4.9), P(5, -6));

            Assert.Equal(2, chain.VertexCount);
            Assert.Equal(1.0, chain.GetEdgeAt(0).Bulge);
            Assert.True(chain.CollidesWith(segment, Thousandth));
        }

        [Fact]
        public void ARepeatInsideALoop_KeepsTheBulgeOfTheEdgeAfterIt()
        {
            // The square from the origin to (20, 20) with (10.0004, 0) repeating (10, 0) on its bottom; the edge from it
            // to (20, 0) bulges 1, a half circle of radius 5 bowing out below the square: area 400 + 25 pi / 2.
            var loop = new GeoPolygonArc2(
                new[] { P(0, 0), P(10, 0), P(10.0004, 0), P(20, 0), P(20, 20), P(0, 20) },
                new[] { 0.0, 0.0, 1.0, 0.0, 0.0, 0.0 });

            Assert.Equal(5, loop.VertexCount);
            Assert.Equal(1.0, loop.GetEdgeAt(1).Bulge);
            Assert.Equal(400 + 12.5 * Math.PI, loop.Area, 1E-9);
        }

        [Fact]
        public void ARepeatAtTheStartOfALoop_KeepsTheBulgeOfTheEdgeAfterIt()
        {
            // (0.0004, 0) repeats the first corner of the square 10 by 10; the bottom edge from it to (10, 0) bulges 1,
            // out below the square: area 100 + 25 pi / 2. On 2755f30 the bottom was straight, area 100.
            var loop = new GeoPolygonArc2(
                new[] { P(0, 0), P(0.0004, 0), P(10, 0), P(10, 10), P(0, 10) },
                new[] { 0.0, 1.0, 0.0, 0.0, 0.0 });

            Assert.Equal(4, loop.VertexCount);
            Assert.Equal(1.0, loop.GetEdgeAt(0).Bulge);
            Assert.Equal(100 + 12.5 * Math.PI, loop.Area, 1E-9);
        }

        [Fact]
        public void ARepeatOfTheFirstCornerAtTheEndOfALoop_ClosesAsTheGuideSays()
        {
            // (0, 0.0004) at the end repeats the first corner (0, 0) and is dropped, but its bulge is kept for the
            // closing edge, as plane.md says: the left side from (0, 10) keeps its own bulge when it curves, and takes
            // the repeated vertex's when it is straight.
            GeoPoint2[] corners = { P(0, 0), P(10, 0), P(10, 10), P(0, 10), P(0, 0.0004) };

            // Straight left side: it takes the bulge 1, a half circle out to (-5, 5), which the segment from
            // (-4.9, 5) to (-6, 5) crosses.
            var takesTheRepeats = new GeoPolygonArc2(corners, new[] { 0.0, 0.0, 0.0, 0.0, 1.0 });

            Assert.Equal(4, takesTheRepeats.VertexCount);
            Assert.Equal(1.0, takesTheRepeats.GetEdgeAt(3).Bulge);
            Assert.Equal(100 + 12.5 * Math.PI, takesTheRepeats.Area, 1E-9);
            Assert.True(takesTheRepeats.CollidesWith(new GeoLine2(P(-4.9, 5), P(-6, 5)), Thousandth));

            // Curved left side, bulge -1, a half circle in to (5, 5): it keeps its own, and the bulge 1 goes.
            var keepsItsOwn = new GeoPolygonArc2(corners, new[] { 0.0, 0.0, 0.0, -1.0, 1.0 });

            Assert.Equal(4, keepsItsOwn.VertexCount);
            Assert.Equal(-1.0, keepsItsOwn.GetEdgeAt(3).Bulge);
            Assert.Equal(100 - 12.5 * Math.PI, keepsItsOwn.Area, 1E-9);
        }

        [Fact]
        public void ARepeatedVertex_IsStillDroppedOnTheGlobalToleranceWhateverIsPassedLater()
        {
            // A guard: the dedupe itself stays. Four ten-thousandths is within the default point tolerance of a
            // thousandth, so the chain has three vertices, though the tolerance passed to a query is 1E-4.
            var chain = new GeoPolylineArc2(new[] { P(0, 0), P(10, 0), P(10.0004, 0), P(20, 0) }, new[] { 0.0, 0.0, 0.0 });

            Assert.Equal(3, chain.VertexCount);
            Assert.Equal(P(10, 0), chain.Vertices[1]);
            Assert.True(chain.CollidesWith(new GeoLine2(P(10.0002, -1), P(10.0002, 1)), TenThousandth));
        }

        [Fact]
        public void AChainWithNoRepeat_KeepsEveryBulgeWhereItWasGiven()
        {
            // A guard: nothing dropped, nothing moved.
            var chain = new GeoPolylineArc2(new[] { P(0, 0), P(10, 0), P(10.01, 0), P(20, 0) }, new[] { 0.5, 0.0, 1.0 });

            Assert.Equal(4, chain.VertexCount);
            Assert.Equal(0.5, chain.GetEdgeAt(0).Bulge);
            Assert.Equal(0.0, chain.GetEdgeAt(1).Bulge);
            Assert.Equal(1.0, chain.GetEdgeAt(2).Bulge);
        }
    }
}
