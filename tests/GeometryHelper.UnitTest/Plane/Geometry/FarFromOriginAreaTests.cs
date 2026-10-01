using GeometryHelper;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// The area and centroid of a small polygon far from the origin, as a part laid out in a site's coordinates is.
    /// </summary>
    /// <remarks>
    /// Summed from the origin, the shoelace's products of coordinates seven kilometres out are near 5E13, whose last digit
    /// is worth a hundredth: what is left of them for a polygon a tenth of a millimetre across is rounding, of either sign.
    /// </remarks>
    public class FarFromOriginAreaTests
    {
        private const double Far = 7000000.0;

        private static GeoPoint2 P(double x, double y) => new GeoPoint2(Far + x, Far + y);

        [Fact]
        public void ASmallTriangleFarOutHasItsArea()
        {
            // A tenth by two hundredths: a thousandth of a square millimetre, counter-clockwise.
            var triangle = new GeoPolygon2(P(0, 0), P(0.1, 0), P(0, 0.02));

            Assert.Equal(0.001, triangle.SignedArea, 8);
            Assert.False(triangle.IsClockwise);
            Assert.Equal(-0.001, new GeoPolygon2(P(0, 0), P(0, 0.02), P(0.1, 0)).SignedArea, 8);
        }

        [Fact]
        public void AThinStripFarOutHasItsArea()
        {
            // Eighteen by five hundredths, which from the origin came out as 0.8984375.
            var strip = new GeoPolygon2(P(0.37, 0.71), P(18.37, 0.71), P(18.37, 0.76), P(0.37, 0.76));

            Assert.Equal(0.9, strip.SignedArea, 6);
            Assert.Equal(0.9, new GeoFace2(strip).Area, 6);
        }

        [Fact]
        public void ASmallTriangleFarOutHasItsCentroid()
        {
            var triangle = new GeoPolygon2(P(0, 0), P(0.3, 0), P(0, 0.3));
            GeoPoint2 centroid = triangle.Centroid;

            Assert.Equal(Far + 0.1, centroid.X, 6);
            Assert.Equal(Far + 0.1, centroid.Y, 6);
        }

        [Fact]
        public void ALoopWithArcsFarOutHasItsArea()
        {
            var square = new GeoPolygonArc2(new[] { P(0, 0), P(0.1, 0), P(0.1, 0.1), P(0, 0.1) });

            Assert.Equal(0.01, square.SignedArea, 8);
        }
    }
}
