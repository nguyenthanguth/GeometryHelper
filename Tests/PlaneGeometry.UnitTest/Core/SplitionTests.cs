using System;
using System.Linq;
using CommonGeometry;
using PlaneGeometry.Core;
using PlaneGeometry.Geometry;
using Xunit;

namespace PlaneGeometry.UnitTest.Core
{
    public class SplitionTests
    {
        // A 10 unit segment along X from the origin.
        private static GeoLine2 Segment() => new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(10, 0));

        // An L of three vertices: 10 along X, then 10 up. Total length 20, corner at distance 10.
        private static GeoPolyline2 OpenPath() => new GeoPolyline2(
            new GeoPoint2(0, 0), new GeoPoint2(10, 0), new GeoPoint2(10, 10));

        #region Line - split at a distance

        [Fact]
        public void Line_SplitAtDistance_CutsWhereAsked()
        {
            Assert.True(Splition2.TrySplitAtDistance(Segment(), 4.0, out GeoLine2 first, out GeoLine2 second));

            Assert.True(first.StartPoint.IsEqualTo(new GeoPoint2(0, 0)));
            Assert.True(first.EndPoint.IsEqualTo(new GeoPoint2(4, 0)));
            Assert.True(second.StartPoint.IsEqualTo(new GeoPoint2(4, 0)));
            Assert.True(second.EndPoint.IsEqualTo(new GeoPoint2(10, 0)));
        }

        [Theory]
        [InlineData(0.0)]      // on the start point
        [InlineData(10.0)]     // on the end point
        [InlineData(-1.0)]     // before the start
        [InlineData(11.0)]     // past the end
        [InlineData(double.NaN)]
        public void Line_SplitAtDistance_RefusesPositionsThatCutNothing(double distance)
        {
            Assert.False(Splition2.TrySplitAtDistance(Segment(), distance, out _, out _));
        }

        [Fact]
        public void Line_SplitAtDistance_RefusesPositionsWithinToleranceOfAnEnd()
        {
            var tolerance = new Tolerance(0.5, 0.5);

            Assert.False(Splition2.TrySplitAtDistance(Segment(), 0.4, out _, out _, tolerance));
            Assert.False(Splition2.TrySplitAtDistance(Segment(), 9.6, out _, out _, tolerance));
            Assert.True(Splition2.TrySplitAtDistance(Segment(), 0.6, out _, out _, tolerance));
        }

        #endregion

        #region Line - split by a point

        [Fact]
        public void Line_SplitByPoint_CutsAtThePoint()
        {
            Assert.True(Splition2.TrySplitBy(Segment(), new GeoPoint2(7, 0), out GeoLine2 first, out GeoLine2 second));

            Assert.True(first.EndPoint.IsEqualTo(new GeoPoint2(7, 0)));
            Assert.True(second.StartPoint.IsEqualTo(new GeoPoint2(7, 0)));
        }

        [Fact]
        public void Line_SplitByPoint_RefusesAPointOffTheSegment()
        {
            // Off to the side, and beyond the far end: neither is on the segment, and neither is
            // projected onto it.
            Assert.False(Splition2.TrySplitBy(Segment(), new GeoPoint2(5, 3), out _, out _));
            Assert.False(Splition2.TrySplitBy(Segment(), new GeoPoint2(20, 0), out _, out _));
        }

        [Fact]
        public void Line_SplitByPoint_RefusesTheEndpoints()
        {
            Assert.False(Splition2.TrySplitBy(Segment(), new GeoPoint2(0, 0), out _, out _));
            Assert.False(Splition2.TrySplitBy(Segment(), new GeoPoint2(10, 0), out _, out _));
        }

        #endregion

        #region Line - split by a line

        [Fact]
        public void Line_SplitByLine_CutsWhereTheyCross()
        {
            var cutter = new GeoLine2(new GeoPoint2(6, -5), new GeoPoint2(6, 5));

            Assert.True(Splition2.TrySplitBy(Segment(), cutter, out GeoLine2 first, out GeoLine2 second));

            Assert.True(first.EndPoint.IsEqualTo(new GeoPoint2(6, 0)));
            Assert.True(second.StartPoint.IsEqualTo(new GeoPoint2(6, 0)));
        }

        [Fact]
        public void Line_SplitByLine_CutsAtATJunction()
        {
            // The cutter only touches; it does not pass through.
            var cutter = new GeoLine2(new GeoPoint2(3, 0), new GeoPoint2(3, 8));

            Assert.True(Splition2.TrySplitBy(Segment(), cutter, out GeoLine2 first, out _));
            Assert.True(first.EndPoint.IsEqualTo(new GeoPoint2(3, 0)));
        }

        [Fact]
        public void Line_SplitByLine_RefusesWhenTheyMiss()
        {
            var cutter = new GeoLine2(new GeoPoint2(6, 2), new GeoPoint2(6, 5));

            Assert.False(Splition2.TrySplitBy(Segment(), cutter, out _, out _));
        }

        [Fact]
        public void Line_SplitByLine_RefusesParallelAndCollinearCutters()
        {
            var parallel = new GeoLine2(new GeoPoint2(0, 3), new GeoPoint2(10, 3));
            var collinear = new GeoLine2(new GeoPoint2(3, 0), new GeoPoint2(8, 0));

            Assert.False(Splition2.TrySplitBy(Segment(), parallel, out _, out _));

            // A collinear overlap has no single position to cut at, so it is treated as no cut.
            Assert.False(Splition2.TrySplitBy(Segment(), collinear, out _, out _));
        }

        [Fact]
        public void Line_SplitByLine_RefusesACrossingAtAnEndpoint()
        {
            var cutter = new GeoLine2(new GeoPoint2(10, -5), new GeoPoint2(10, 5));

            Assert.False(Splition2.TrySplitBy(Segment(), cutter, out _, out _));
        }

        #endregion

        #region Line - split at several distances

        [Fact]
        public void Line_SplitAtDistances_ProducesOnePieceMoreThanCuts()
        {
            GeoLine2[] pieces = Splition2.SplitAtDistances(Segment(), new[] { 2.0, 5.0, 8.0 });

            Assert.Equal(4, pieces.Length);
            Assert.True(pieces[0].StartPoint.IsEqualTo(new GeoPoint2(0, 0)));
            Assert.True(pieces[1].StartPoint.IsEqualTo(new GeoPoint2(2, 0)));
            Assert.True(pieces[2].StartPoint.IsEqualTo(new GeoPoint2(5, 0)));
            Assert.True(pieces[3].EndPoint.IsEqualTo(new GeoPoint2(10, 0)));
        }

        [Fact]
        public void Line_SplitAtDistances_SortsAndDropsUnusablePositions()
        {
            // Unsorted, with one duplicate, one at the start, and two outside the segment.
            GeoLine2[] pieces = Splition2.SplitAtDistances(
                Segment(), new[] { 8.0, -3.0, 2.0, 8.0, 0.0, 40.0 });

            Assert.Equal(3, pieces.Length);
            Assert.True(pieces[0].EndPoint.IsEqualTo(new GeoPoint2(2, 0)));
            Assert.True(pieces[1].EndPoint.IsEqualTo(new GeoPoint2(8, 0)));
        }

        [Fact]
        public void Line_SplitAtDistances_MergesPositionsCloserThanTolerance()
        {
            var tolerance = new Tolerance(0.5, 0.5);

            GeoLine2[] pieces = Splition2.SplitAtDistances(Segment(), new[] { 4.0, 4.2 }, tolerance);

            Assert.Equal(2, pieces.Length);
            Assert.True(pieces[0].EndPoint.IsEqualTo(new GeoPoint2(4, 0)));
        }

        [Fact]
        public void Line_SplitAtDistances_ReturnsTheSubjectWhenNothingIsUsable()
        {
            GeoLine2[] pieces = Splition2.SplitAtDistances(Segment(), new double[0]);

            Assert.Single(pieces);
            Assert.True(pieces[0].StartPoint.IsEqualTo(new GeoPoint2(0, 0)));
            Assert.True(pieces[0].EndPoint.IsEqualTo(new GeoPoint2(10, 0)));
        }

        [Fact]
        public void Line_SplitAtDistances_RejectsNullPositions()
        {
            Assert.Throws<ArgumentNullException>(() => Splition2.SplitAtDistances(Segment(), null));
        }

        #endregion

        #region Polyline - split at a distance

        [Fact]
        public void Polyline_SplitAtDistance_CutsInsideAnEdge()
        {
            Assert.True(Splition2.TrySplitAtDistance(OpenPath(), 5.0, out GeoPolyline2 first, out GeoPolyline2 second));

            // The first piece stops halfway along the horizontal leg.
            Assert.Equal(2, first.VertexCount);
            Assert.True(first[1].IsEqualTo(new GeoPoint2(5, 0)));

            // The second keeps the corner and the far end.
            Assert.Equal(3, second.VertexCount);
            Assert.True(second[0].IsEqualTo(new GeoPoint2(5, 0)));
            Assert.True(second[1].IsEqualTo(new GeoPoint2(10, 0)));
            Assert.True(second[2].IsEqualTo(new GeoPoint2(10, 10)));
        }

        [Fact]
        public void Polyline_SplitAtDistance_CutsExactlyOnACorner()
        {
            // Distance2 10 is the corner vertex itself; neither piece should gain a duplicate vertex.
            Assert.True(Splition2.TrySplitAtDistance(OpenPath(), 10.0, out GeoPolyline2 first, out GeoPolyline2 second));

            Assert.Equal(2, first.VertexCount);
            Assert.True(first[0].IsEqualTo(new GeoPoint2(0, 0)));
            Assert.True(first[1].IsEqualTo(new GeoPoint2(10, 0)));

            Assert.Equal(2, second.VertexCount);
            Assert.True(second[0].IsEqualTo(new GeoPoint2(10, 0)));
            Assert.True(second[1].IsEqualTo(new GeoPoint2(10, 10)));
        }

        [Fact]
        public void Polyline_SplitAtDistance_SnapsAPositionNearACornerOntoIt()
        {
            var tolerance = new Tolerance(0.5, 0.5);

            // Just past the corner: snapping keeps the second piece from starting with a sliver edge.
            Assert.True(Splition2.TrySplitAtDistance(OpenPath(), 10.2, out GeoPolyline2 first, out GeoPolyline2 second, tolerance));

            Assert.Equal(2, first.VertexCount);
            Assert.True(first[1].IsEqualTo(new GeoPoint2(10, 0)));
            Assert.Equal(2, second.VertexCount);
        }

        [Theory]
        [InlineData(0.0)]
        [InlineData(20.0)]
        [InlineData(-4.0)]
        [InlineData(25.0)]
        public void Polyline_SplitAtDistance_RefusesPositionsThatCutNothing(double distance)
        {
            Assert.False(Splition2.TrySplitAtDistance(OpenPath(), distance, out _, out _));
        }

        [Fact]
        public void Polyline_SplitAtDistance_RejectsNullSubject()
        {
            Assert.Throws<ArgumentNullException>(
                () => Splition2.TrySplitAtDistance(null, 5.0, out GeoPolyline2 _, out GeoPolyline2 _));
        }

        #endregion

        #region Polyline - split by a point

        [Fact]
        public void Polyline_SplitByPoint_CutsAtThePoint()
        {
            Assert.True(Splition2.TrySplitBy(OpenPath(), new GeoPoint2(10, 4), out GeoPolyline2 first, out GeoPolyline2 second));

            Assert.True(first[first.VertexCount - 1].IsEqualTo(new GeoPoint2(10, 4)));
            Assert.True(second[0].IsEqualTo(new GeoPoint2(10, 4)));
        }

        [Fact]
        public void Polyline_SplitByPoint_RefusesAPointOffThePath()
        {
            Assert.False(Splition2.TrySplitBy(OpenPath(), new GeoPoint2(4, 4), out _, out _));
        }

        [Fact]
        public void Polyline_SplitByPoint_RefusesTheEndpoints()
        {
            Assert.False(Splition2.TrySplitBy(OpenPath(), new GeoPoint2(0, 0), out _, out _));
            Assert.False(Splition2.TrySplitBy(OpenPath(), new GeoPoint2(10, 10), out _, out _));
        }

        #endregion

        #region Polyline - split by a line

        [Fact]
        public void Polyline_SplitByLine_CutsAtASingleCrossing()
        {
            var cutter = new GeoLine2(new GeoPoint2(4, -5), new GeoPoint2(4, 5));

            Assert.True(Splition2.TrySplitBy(OpenPath(), cutter, out GeoPolyline2[] pieces));

            Assert.Equal(2, pieces.Length);
            Assert.True(pieces[0][pieces[0].VertexCount - 1].IsEqualTo(new GeoPoint2(4, 0)));
        }

        [Fact]
        public void Polyline_SplitByLine_CutsEverywhereAZigzagCrossesIt()
        {
            // A zigzag that crosses y = 0 three times.
            var zigzag = new GeoPolyline2(
                new GeoPoint2(0, -4), new GeoPoint2(2, 4), new GeoPoint2(4, -4),
                new GeoPoint2(6, 4), new GeoPoint2(8, 4));
            var cutter = new GeoLine2(new GeoPoint2(-1, 0), new GeoPoint2(9, 0));

            Assert.True(Splition2.TrySplitBy(zigzag, cutter, out GeoPolyline2[] pieces));

            Assert.Equal(4, pieces.Length);
        }

        [Fact]
        public void Polyline_SplitByLine_TrySplitBySupportsMoreThanOneCrossing()
        {
            var zigzag = new GeoPolyline2(
                new GeoPoint2(0, -4), new GeoPoint2(2, 4), new GeoPoint2(4, -4),
                new GeoPoint2(6, 4), new GeoPoint2(8, 4));
            var cutter = new GeoLine2(new GeoPoint2(-1, 0), new GeoPoint2(9, 0));

            Assert.True(Splition2.TrySplitBy(zigzag, cutter, out GeoPolyline2[] pieces));
            Assert.Equal(4, pieces.Length);
        }

        [Fact]
        public void Polyline_SplitByLine_ReturnsFalseWhenNothingCrosses()
        {
            var cutter = new GeoLine2(new GeoPoint2(-5, -5), new GeoPoint2(-5, 5));

            Assert.False(Splition2.TrySplitBy(OpenPath(), cutter, out GeoPolyline2[] pieces));

            // false says nothing was cut, not that the call failed, so the subject still comes back.
            Assert.Single(pieces);
            Assert.Equal(OpenPath().Length, pieces[0].Length, 9);
        }

        [Fact]
        public void Polyline_SplitByLine_RejectsNullSubject()
        {
            Assert.Throws<ArgumentNullException>(() => Splition2.TrySplitBy(null, Segment(), out _));
        }

        #endregion

        #region Tolerance independence

        [Fact]
        public void Split_UsesTheSuppliedToleranceNotTheGlobalOne()
        {
            // The trusted constructor exists so that results are not re-filtered against Tolerance.Global.
            // With the global widened past the edge lengths, the public constructor would collapse the
            // pieces; splitting must not care.
            Tolerance saved = Tolerance.Global;
            try
            {
                Tolerance.Global = new Tolerance(5.0, 5.0);

                GeoPolyline2[] pieces = Splition2.SplitAtDistances(
                    OpenPath(), new[] { 5.0 }, new Tolerance(1E-4, 1E-4));

                Assert.Equal(2, pieces.Length);
                Assert.Equal(2, pieces[0].VertexCount);
                Assert.Equal(3, pieces[1].VertexCount);
                Assert.Equal(20.0, pieces.Sum(p => p.Length), 9);
            }
            finally
            {
                Tolerance.Global = saved;
            }
        }

        #endregion
    }
}
