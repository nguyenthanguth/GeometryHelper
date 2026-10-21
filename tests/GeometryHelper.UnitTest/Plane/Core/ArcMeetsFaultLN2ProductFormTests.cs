using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// Two arcs that cross, at no tolerance, collide and are nought apart, as their crossing says.
    /// </summary>
    /// <remarks>
    /// The crossings of two circles were worked out from the difference of the squared radii taken as the product of the
    /// difference and the sum, but the test of whether two arcs truly meet, which decides their distance and whether they
    /// collide, took it as the difference of the two squares. Two near-equal circles of radius 8 633 about centres 0.87 apart
    /// lose the last digits that way, and the arc ending on the other's circle crossed it at (96 116.465, 92 341.693) while the
    /// two stood 1.5E-11 apart and did not collide. Both read the radii one way now.
    /// </remarks>
    public class ArcMeetsFaultLN2ProductFormTests
    {
        private static readonly Tolerance None = new Tolerance(0, 0);

        private static readonly GeoArc2 First = new GeoArc2(new GeoPoint2(100088.95804145791, 100006.53604744306), 8633.1064176964137, 3.9342288857097403, 3.9342288857097403 + 0.6);

        private static readonly GeoArc2 Second = new GeoArc2(new GeoPoint2(100088.19029470639, 100006.93718500438), 8633.10933243961, 4.2343292227485634, 4.2343292227485634 + 0.3);

        [Fact]
        public void ArcsThatCrossAtNoTolerance_AreNoDistanceApart()
        {
            Assert.Single(First.GetIntersections(Second, None));
            Assert.Equal(0.0, First.DistanceTo(Second, None));
            Assert.Equal(0.0, Second.DistanceTo(First, None));
        }

        [Fact]
        public void ArcsThatCrossAtNoTolerance_Collide()
        {
            Assert.True(First.CollidesWith(Second, None));
            Assert.True(Second.CollidesWith(First, None));
        }
    }
}
