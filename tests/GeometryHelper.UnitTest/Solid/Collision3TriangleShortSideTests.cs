using GeometryHelper;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// A triangle with a side no longer than the point tolerance still clashes by the exact gap. The triangle here has a side 0.001
    /// long, from (0, -0.0005, 0) to (0, 0.0005, 0), and its third corner 50 away; a second triangle has an edge along z at
    /// x = 0.0009, across the middle of that side. The gap is 0.0009, inside the tolerance of 0.001. Asked as two segments the side
    /// reads as its start, which is 0.00103 from the edge, and the pair was missed from both sides.
    /// </summary>
    public class Collision3TriangleShortSideTests
    {
        private static GeoPoint3 P(double x, double y, double z) => new GeoPoint3(x, y, z);

        private static GeoTriangle3 ShortSide() => new GeoTriangle3(P(0, -0.0005, 0), P(0, 0.0005, 0), P(-50, 0, 0));

        private static GeoTriangle3 AcrossIt(double x) => new GeoTriangle3(P(x, 0, -10), P(x, 0, 10), P(x + 20, 0, 0));

        private static GeoTriangle3 AcrossItTilted(double x) => new GeoTriangle3(P(x, 0, -10), P(x, 0, 10), P(x + 20, 5, 3));

        [Theory]
        [InlineData(0.0005)]
        [InlineData(0.0009)]
        [InlineData(0.00099)]
        public void AnEdgeAcrossTheMiddleOfAShortSide_ClashesFromEitherSide(double gap)
        {
            Tolerance tolerance = Tolerance.Default;

            Assert.True(ShortSide().CollidesWith(AcrossIt(gap), tolerance));
            Assert.True(AcrossIt(gap).CollidesWith(ShortSide(), tolerance));
        }

        [Fact]
        public void ATiltedTriangleAcrossTheMiddleOfAShortSide_ClashesFromEitherSide()
        {
            Tolerance tolerance = Tolerance.Default;

            Assert.True(ShortSide().CollidesWith(AcrossItTilted(0.0009), tolerance));
            Assert.True(AcrossItTilted(0.0009).CollidesWith(ShortSide(), tolerance));
        }

        [Fact]
        public void AnEdgeAHairBeyondTheTolerance_StaysApartFromEitherSide()
        {
            Tolerance tolerance = Tolerance.Default;

            Assert.False(ShortSide().CollidesWith(AcrossIt(0.001 + 1E-6), tolerance));
            Assert.False(AcrossIt(0.001 + 1E-6).CollidesWith(ShortSide(), tolerance));
        }
    }
}
