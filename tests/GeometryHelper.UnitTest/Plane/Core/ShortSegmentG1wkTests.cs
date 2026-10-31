using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// A segment shorter than the global tolerance is cut where a point, a line, lines, a polyline, points or a polygon
    /// cut it, measured along the segment as it is, not at its start.
    /// </summary>
    /// <remarks>
    /// The segment runs from (5, 0.0004) to (5, -0.0005), 0.0009 long, and every cut is asked at a billionth. Each cutter
    /// meets it at (5, 0). The place was measured at the global thousandth, which reads a segment no longer than that as its
    /// start, so it came out 0 along, within the tolerance of the start, and the segment came back whole. No test sets the
    /// global tolerance.
    /// </remarks>
    public class ShortSegmentG1wkTests
    {
        private static readonly Tolerance Billionth = new Tolerance(1E-9, Tolerance.DefaultEqualVector, Tolerance.DefaultEqualAngleRad, 1E-9);

        private static readonly GeoLine2 Segment = new GeoLine2(new GeoPoint2(5, 0.0004), new GeoPoint2(5, -0.0005));

        private static readonly GeoLine2 Across = new GeoLine2(new GeoPoint2(4, 0), new GeoPoint2(6, 0));

        private static void AssertCutAtTheCrossing(GeoLine2 first, GeoLine2 second)
        {
            Assert.Equal(5, first.EndPoint.X, 12);
            Assert.Equal(0, first.EndPoint.Y, 12);
            Assert.Equal(first.EndPoint, second.StartPoint);
            Assert.Equal(Segment.StartPoint, first.StartPoint);
            Assert.Equal(Segment.EndPoint, second.EndPoint);
        }

        [Fact]
        public void ASegmentShorterThanTheGlobalTolerance_CutByAPointOnIt_IsCutAtThePoint()
        {
            Assert.True(Segment.TrySplitBy(new GeoPoint2(5, 0), out GeoLine2 first, out GeoLine2 second, Billionth));

            AssertCutAtTheCrossing(first, second);
        }

        [Fact]
        public void ASegmentShorterThanTheGlobalTolerance_CutByALineCrossingIt_IsCutWhereItCrosses()
        {
            Assert.True(Segment.TrySplitBy(Across, out GeoLine2 first, out GeoLine2 second, Billionth));

            AssertCutAtTheCrossing(first, second);
        }

        [Fact]
        public void ASegmentShorterThanTheGlobalTolerance_CutByLines_IsCutWhereTheOneCrossingItCrosses()
        {
            var farOff = new GeoLine2(new GeoPoint2(4, 50), new GeoPoint2(6, 50));

            Assert.True(Segment.TrySplitBy(new[] { farOff, Across }, out GeoLine2[] pieces, Billionth));

            Assert.Equal(2, pieces.Length);
            AssertCutAtTheCrossing(pieces[0], pieces[1]);
        }

        [Fact]
        public void ASegmentShorterThanTheGlobalTolerance_CutByAPolylineCrossingIt_IsCutWhereItCrosses()
        {
            var polyline = new GeoPolyline2(new GeoPoint2(4, 0), new GeoPoint2(6, 0), new GeoPoint2(6, 3));

            Assert.True(Segment.TrySplitBy(polyline, out GeoLine2[] pieces, Billionth));

            Assert.Equal(2, pieces.Length);
            AssertCutAtTheCrossing(pieces[0], pieces[1]);
        }

        [Fact]
        public void ASegmentShorterThanTheGlobalTolerance_CutByPoints_IsCutAtTheOneOnIt()
        {
            Assert.True(Segment.TrySplitBy(new[] { new GeoPoint2(0, 0), new GeoPoint2(5, 0) }, out GeoLine2[] pieces, Billionth));

            Assert.Equal(2, pieces.Length);
            AssertCutAtTheCrossing(pieces[0], pieces[1]);
        }

        [Fact]
        public void ASegmentShorterThanTheGlobalTolerance_CutByASquare_IsCutAtTheSquaresSide()
        {
            // The square 2 by 2 from (4, 0) holds the part of the segment above its bottom side, 0.0004 long.
            var square = new GeoPolygon2(new GeoPoint2(4, 0), new GeoPoint2(6, 0), new GeoPoint2(6, 2), new GeoPoint2(4, 2));

            Assert.True(Segment.TrySplitBy(new[] { square }, out GeoLine2[] inside, out GeoLine2[] outside, Billionth));

            Assert.Single(inside);
            Assert.Single(outside);
            AssertCutAtTheCrossing(inside[0], outside[0]);
        }
    }
}
