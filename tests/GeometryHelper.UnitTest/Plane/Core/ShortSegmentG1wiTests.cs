using GeometryHelper.Geometry;
using GeometryHelper.Spatial;
using Xunit;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// An index over the edges of a shape in the plane gives a point beside a short straight edge its closest point on the
    /// edge as it is, as the shape itself does: a point beside an edge no longer than the tolerance was carried to the edge's
    /// start.
    /// </summary>
    /// <remarks>
    /// The polygon is (0, 0), (10, 0), (10, 0.006), (0, 10), with a side 0.006 long at x = 10, asked at a tolerance of a
    /// hundredth. The point (10.03, 0.003) lies 0.03 to the right of the middle of that side, where its corners are
    /// 0.0301496 away. No test sets the global tolerance.
    /// </remarks>
    public class ShortSegmentG1wiTests
    {
        private static readonly Tolerance Hundredth = new Tolerance(1E-2, Tolerance.DefaultEqualVector, Tolerance.DefaultEqualAngleRad, 1E-2);

        private static readonly GeoPoint2 Beside = new GeoPoint2(10.03, 0.003);

        private static GeoPolygon2 Polygon()
            => new GeoPolygon2(new GeoPoint2(0, 0), new GeoPoint2(10, 0), new GeoPoint2(10, 0.006), new GeoPoint2(0, 10));

        [Fact]
        public void TheClosestPointOfAnIndex_BesideAShortStraightEdge_IsOnTheEdgeAsItIs()
        {
            // The foot (10, 0.003), 0.03 away, where it was the edge's start (10, 0), 0.0301496 away.
            GeoBvh2 index = GeoBvh2.FromPolygon(Polygon());

            GeoPoint2 closest = index.GetClosestPoint(Beside, Hundredth);

            Assert.Equal(10, closest.X, 12);
            Assert.Equal(0.003, closest.Y, 12);
            Assert.Equal(0.03, index.DistanceTo(Beside, Hundredth), 12);
            Assert.Equal(Polygon().DistanceTo(Beside), index.DistanceTo(Beside, Hundredth), 12);
        }
    }
}
