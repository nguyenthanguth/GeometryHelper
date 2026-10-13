using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// A segment against a rectangle, asked under a tolerance finer than the global one, found colliding by its start
    /// point read under the global tolerance.
    /// </summary>
    /// <remarks>
    /// The test of a segment and a rectangle looks for a crossing with each side under the tolerance given, then asks
    /// whether the rectangle holds the segment's start. That last question was asked without the tolerance, so it read
    /// <see cref="Tolerance.Global"/>, a thousandth by default.
    /// </remarks>
    public class SegmentFaultB3Tests
    {
        private static readonly Tolerance Fine = new Tolerance(1E-4, 1E-5);

        // The square 100 by 100 centred at the origin; its right side is x = 50.
        private static GeoRectangle2 Square() => new GeoRectangle2(new GeoPoint2(0, 0), 100, 100);

        // Starts 0.0002 right of that side, twice the fine tolerance and a fifth of the default one, and runs away from it.
        private static GeoLine2 Outside() => new GeoLine2(new GeoPoint2(50.0002, 0), new GeoPoint2(60, 0));

        [Fact]
        public void ASegmentStartingTwoTenThousandthsOutsideARectangle_DoesNotCollideWithItUnderATenThousandth()
        {
            Assert.False(Collision2.CollidesWith(Square(), Outside(), Fine));
            Assert.False(Square().CollidesWith(Outside(), Fine));
            Assert.False(Outside().CollidesWith(Square(), Fine));
        }

        [Fact]
        public void ASegmentStartingTwoTenThousandthsOutsideARectangle_CollidesWithItUnderAThousandth()
        {
            Assert.True(Collision2.CollidesWith(Square(), Outside(), Tolerance.Default));
        }
    }
}
