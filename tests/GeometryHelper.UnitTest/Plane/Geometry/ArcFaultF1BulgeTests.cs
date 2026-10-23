using System;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// An arc built from a bulge puts its centre off the chord's middle by c (1 - b²) / 4b, worked out as it stands: from the
    /// radius, as the root of r² - c²/4, the two cancelled near a half turn.
    /// </summary>
    public class ArcFaultF1BulgeTests
    {
        [Theory]
        [InlineData(0.99999999, 0.001000000005)]
        [InlineData(-0.99999999, -0.001000000005)]
        public void AnArcAHairShortOfAHalfTurnOverAChordOf200000_HasItsCentreOffTheChord(double bulge, double apothem)
        {
            // The centre came out on the chord, 0.001 off, and the arc ended 0.002 off its end.
            GeoArc2 arc = GeoArc2.FromBulge(new GeoPoint2(0, 0), new GeoPoint2(200000, 0), bulge);

            Assert.Equal(100000.0, arc.Center.X, 9);
            Assert.Equal(apothem, arc.Center.Y, 9);
            Assert.Equal(0.0, arc.EndPoint.DistanceTo(new GeoPoint2(200000, 0)), 9);
        }

        [Theory]
        [InlineData(0.5, 37.5)]
        [InlineData(-0.5, -37.5)]
        [InlineData(3.0, -200.0 / 3.0)]
        [InlineData(-3.0, 200.0 / 3.0)]
        [InlineData(1.0, 0.0)]
        public void AnArcFromABulge_HasItsCentreWhereTheBulgeSays(double bulge, double apothem)
        {
            GeoArc2 arc = GeoArc2.FromBulge(new GeoPoint2(0, 0), new GeoPoint2(100, 0), bulge);

            Assert.Equal(50.0, arc.Center.X, 12);
            Assert.Equal(apothem, arc.Center.Y, 12);
            Assert.Equal(100 * (1 + bulge * bulge) / (4 * Math.Abs(bulge)), arc.Radius, 12);
            Assert.Equal(0.0, arc.EndPoint.DistanceTo(new GeoPoint2(100, 0)), 12);
        }
    }
}
