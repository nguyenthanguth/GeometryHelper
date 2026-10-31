using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// The place round a triangle or a rectangle, and along a polyline, of a point beside a side shorter than the global
    /// tolerance is on the side as it is, not at its start; so a polyline is cut there, by a point or by a line crossing it.
    /// </summary>
    /// <remarks>
    /// The triangle is (0, 0), (10, 0), (10, 0.0006) and the rectangle 10 by 0.0006 from the origin, each with a side 0.0006
    /// long at x = 10; the polyline goes from (0, 0) to (10, 0), on to (10, 0.0006) and back to (0, 0.0006), built as the
    /// library builds one, since the public constructor drops a leg shorter than the global thousandth. The point
    /// (10.003, 0.0003) lies 0.003 beside the middle of the short side. The cuts are asked at a millionth. No test sets the
    /// global tolerance.
    /// </remarks>
    public class ShortSegmentG1wjTests
    {
        private static readonly Tolerance Millionth = new Tolerance(1E-6, Tolerance.DefaultEqualVector, Tolerance.DefaultEqualAngleRad, 1E-6);

        private static readonly GeoPoint2 Beside = new GeoPoint2(10.003, 0.0003);

        private static GeoPolyline2 Hairpin()
            => new GeoPolyline2(new[] { new GeoPoint2(0, 0), new GeoPoint2(10, 0), new GeoPoint2(10, 0.0006), new GeoPoint2(0, 0.0006) }, 4);

        [Fact]
        public void ThePlaceRoundATriangle_OfAPointBesideAShortSide_IsOnTheSideAsItIs()
        {
            // 10.0003 round, 0.0003 along the side 0.0006 long; it was 10 round, at the start of that side.
            var triangle = new GeoTriangle2(new GeoPoint2(0, 0), new GeoPoint2(10, 0), new GeoPoint2(10, 0.0006));

            Assert.Equal(10.0003, triangle.GetDistanceAtPoint(Beside), 12);
            Assert.Equal(10.0003 / triangle.Length, triangle.GetParameterAtPoint(Beside), 12);
        }

        [Fact]
        public void ThePlaceRoundARectangle_OfAPointBesideAShortSide_IsOnTheSideAsItIs()
        {
            // 10.0003 round, where it was 10, at the corner (10, 0).
            var rectangle = new GeoRectangle2(0, 0, 10, 0.0006);

            Assert.Equal(10.0003, rectangle.GetDistanceAtPoint(Beside), 12);
        }

        [Fact]
        public void ThePlaceAlongAPolyline_OfAPointBesideAShortLeg_IsOnTheLegAsItIs()
        {
            // 10.0003 along, halfway, where it was 10 along.
            GeoPolyline2 hairpin = Hairpin();

            Assert.Equal(10.0003, hairpin.GetDistanceAtPoint(Beside), 12);
            Assert.Equal(0.5, hairpin.GetParameterAtPoint(Beside), 12);
        }

        [Fact]
        public void APolylineCutByAPointOnAShortLeg_IsCutAtThePoint()
        {
            // The cut was made at the leg's start (10, 0), 0.0003 from the point, three hundred tolerances.
            Assert.True(Hairpin().TrySplitBy(new GeoPoint2(10, 0.0003), out GeoPolyline2 first, out GeoPolyline2 second, Millionth));

            Assert.Equal(10, first.EndPoint.X, 12);
            Assert.Equal(0.0003, first.EndPoint.Y, 12);
            Assert.Equal(10, second.StartPoint.X, 12);
            Assert.Equal(0.0003, second.StartPoint.Y, 12);
        }

        [Fact]
        public void APolylineCutByALineCrossingAShortLeg_IsCutWhereItCrosses()
        {
            // The line along y = 0.0003 crosses the leg 0.0006 long at its middle; the cut was made at the leg's start (10, 0).
            var cutter = new GeoLine2(new GeoPoint2(9, 0.0003), new GeoPoint2(11, 0.0003));

            Assert.True(Hairpin().TrySplitBy(cutter, out GeoPolyline2[] pieces, Millionth));

            Assert.Equal(2, pieces.Length);
            Assert.Equal(10, pieces[1].StartPoint.X, 12);
            Assert.Equal(0.0003, pieces[1].StartPoint.Y, 12);
        }
    }
}
