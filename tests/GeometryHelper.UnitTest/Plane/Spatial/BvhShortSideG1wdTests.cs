using GeometryHelper.Geometry;
using GeometryHelper.Spatial;
using Xunit;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// Two indexes measure and meet edge against edge as the shapes they were built from do: every edge is a side of a
    /// shape, the side it is however short, never a segment read as its start.
    /// </summary>
    /// <remarks>
    /// Every test passes a tolerance of a thousandth, and in each a side 0.0009 long, shorter than it, comes nearest the
    /// other index somewhere other than at its start. The indexes are built over edges, which keep a side that short.
    /// </remarks>
    public class BvhShortSideG1wdTests
    {
        private static readonly Tolerance Thousandth = new Tolerance(1E-3, Tolerance.DefaultEqualVector, Tolerance.DefaultEqualAngleRad, 1E-3);

        [Fact]
        public void AShortLastSidePointingAtAnotherIndex_IsMeasuredToItsEnd()
        {
            // The chain comes down from (5, 5) to (5, 0.0015) and on to (5, 0.0006), 0.0006 above the segment along the
            // x axis from the origin to (10, 0); read as its start, its last side would be 0.0015 away and clear of it.
            var down = new[]
            {
                new GeoEdge2(new GeoPoint2(5, 5), new GeoPoint2(5, 0.0015)),
                new GeoEdge2(new GeoPoint2(5, 0.0015), new GeoPoint2(5, 0.0006)),
            };
            var along = new[] { new GeoEdge2(new GeoPoint2(0, 0), new GeoPoint2(10, 0)) };
            var one = new GeoBvh2(down);
            var two = new GeoBvh2(along);

            Assert.Equal(0.0006, new GeoPolylineArc2(down).DistanceTo(new GeoPolylineArc2(along), Thousandth), 12);
            Assert.Equal(0.0006, one.DistanceTo(two, Thousandth), 12);
            Assert.Equal(0.0006, two.DistanceTo(one, Thousandth), 12);
            Assert.True(new GeoPolylineArc2(down).CollidesWith(new GeoPolylineArc2(along), Thousandth));
            Assert.True(one.CollidesWith(two, Thousandth));
            Assert.True(two.CollidesWith(one, Thousandth));
        }

        [Fact]
        public void AShortSideOfALoopNearestAnotherIndexInItsMiddle_IsMeasuredThere()
        {
            // The loop's side from (20, 0.00045) down to (20, -0.00045) faces the corner of the chain at (19.9997, 0),
            // 0.0003 off its middle; read as its start, it would be 0.00054 away, as far as its ends are.
            var loop = new[]
            {
                new GeoEdge2(new GeoPoint2(20, 0.00045), new GeoPoint2(20, -0.00045)),
                new GeoEdge2(new GeoPoint2(20, -0.00045), new GeoPoint2(30, -5)),
                new GeoEdge2(new GeoPoint2(30, -5), new GeoPoint2(30, 5), 0.3),
                new GeoEdge2(new GeoPoint2(30, 5), new GeoPoint2(20, 0.00045)),
            };
            var corner = new[]
            {
                new GeoEdge2(new GeoPoint2(10, 5), new GeoPoint2(19.9997, 0)),
                new GeoEdge2(new GeoPoint2(19.9997, 0), new GeoPoint2(10, -5)),
            };
            var one = new GeoBvh2(loop);
            var two = new GeoBvh2(corner);

            Assert.Equal(0.0003, new GeoPolylineArc2(loop).DistanceTo(new GeoPolylineArc2(corner), Thousandth), 9);
            Assert.Equal(0.0003, one.DistanceTo(two, Thousandth), 9);
            Assert.Equal(0.0003, two.DistanceTo(one, Thousandth), 9);
            Assert.True(one.CollidesWith(two, Thousandth));
        }
    }
}
