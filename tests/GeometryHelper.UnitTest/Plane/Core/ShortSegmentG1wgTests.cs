using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// A straight side of a curved chain or loop in the plane is the side it is however short to the closest point, the
    /// shortest line from a point and the distance along it to a point, as it already was to the distance and as it is in
    /// space: a point beside a side no longer than the tolerance was carried to the side's start.
    /// </summary>
    /// <remarks>
    /// The loop is (0, 0), (10, 0), (10, 0.006), (0, 10), the chain the hairpin from (0, 0) to (10, 0), on to (10, 0.006) and
    /// back to (0, 0.006); each has a side 0.006 long at x = 10 and is asked at a tolerance of a hundredth. The point
    /// (10.03, 0.003) lies 0.03 to the right of the middle of that side, where its corners are 0.0301496 away. No test sets
    /// the global tolerance.
    /// </remarks>
    public class ShortSegmentG1wgTests
    {
        private static readonly Tolerance Hundredth = new Tolerance(1E-2, Tolerance.DefaultEqualVector, Tolerance.DefaultEqualAngleRad, 1E-2);

        private static readonly GeoPoint2 Beside = new GeoPoint2(10.03, 0.003);

        private static GeoPolygonArc2 Loop()
            => new GeoPolygonArc2(new[] { new GeoPoint2(0, 0), new GeoPoint2(10, 0), new GeoPoint2(10, 0.006), new GeoPoint2(0, 10) });

        private static GeoPolylineArc2 Hairpin()
            => new GeoPolylineArc2(new[] { new GeoPoint2(0, 0), new GeoPoint2(10, 0), new GeoPoint2(10, 0.006), new GeoPoint2(0, 0.006) });

        [Fact]
        public void TheClosestPointOfALoopOrAChain_BesideItsShortSide_IsOnTheSideAsItIs()
        {
            GeoPoint2 fromLoop = Loop().GetClosestPointOnBoundary(Beside, Hundredth);
            GeoPoint2 fromChain = Hairpin().GetClosestPointOnBoundary(Beside, Hundredth);

            Assert.Equal(10, fromLoop.X, 12);
            Assert.Equal(0.003, fromLoop.Y, 12);
            Assert.Equal(10, fromChain.X, 12);
            Assert.Equal(0.003, fromChain.Y, 12);
            Assert.Equal(0.03, Beside.GetClosestPointOnBoundary(Loop(), Hundredth).DistanceTo(Beside), 12);
        }

        [Fact]
        public void TheShortestLineFromALoopOrAChain_ToAPointBesideItsShortSide_IsAsLongAsTheDistance()
        {
            Assert.Equal(0.03, Loop().GetShortestLineTo(Beside, Hundredth).Length, 12);
            Assert.Equal(0.03, Hairpin().GetShortestLineTo(Beside, Hundredth).Length, 12);
            Assert.Equal(Hairpin().DistanceTo(Beside, Hundredth), Hairpin().GetShortestLineTo(Beside, Hundredth).Length, 12);
        }

        [Fact]
        public void TheDistanceAlongALoopOrAChain_ToAPointBesideItsShortSide_IsToTheFootOnTheSide()
        {
            // 10 along the bottom and 0.003 up the short side, where it was 10, to the side's start.
            Assert.Equal(10.003, Hairpin().GetDistanceAtPoint(Beside, Hundredth), 12);
            Assert.Equal(10.003, Loop().GetDistanceAtPoint(Beside, Hundredth), 12);
            Assert.Equal(10.003 / Hairpin().Length, Hairpin().GetParameterAtPoint(Beside, Hundredth), 12);
        }
    }
}
