using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// Where the curves behind two edges meet, for a fillet or a mitre, two circles cross where they truly cross, however
    /// large and nearly equal.
    /// </summary>
    /// <remarks>
    /// The meeting of two circles the fillets and the offsets ask for took the difference of the squared radii as the
    /// difference of the two squares, which keeps only the last digits of two radii near 2.5E8 a few apart: the circles of
    /// radius 250 000 007.25 about the origin and 250 000 000 about (40, 30) were crossed 0.035 from where they cross,
    /// worked out to 60 digits. It is the product of their difference and their sum now, as the crossings of two circles
    /// take it, and the crossings are where they are to the rounding of numbers that size.
    /// </remarks>
    public class CircleMeetFaultLN3ProductFormTests
    {
        private static readonly GeoPoint2 FirstCentre = new GeoPoint2(0, 0);
        private const double FirstRadius = 250000007.25;

        private static readonly GeoPoint2 SecondCentre = new GeoPoint2(40, 30);
        private const double SecondRadius = 250000000.0;

        // Worked out to 60 digits from the same doubles.
        private static readonly GeoPoint2[] Crossings =
        {
            new GeoPoint2(-119414729.9878774434, 219636349.1932115853),
            new GeoPoint2(177414770.8288774490, -176136318.5624615848),
        };

        private static void AssertTheCrossings(List<GeoPoint2> found)
        {
            Assert.Equal(2, found.Count);

            foreach (GeoPoint2 crossing in Crossings)
            {
                Assert.True(found.Min(point => point.DistanceTo(crossing)) < 1E-6);
            }
        }

        [Fact]
        public void TwoLargeNearlyEqualCircles_MeetWhereTheyCross()
        {
            var found = new List<GeoPoint2>();

            CurveMeet2.Circles(found, FirstCentre, FirstRadius, SecondCentre, SecondRadius, Tolerance.Default);

            AssertTheCrossings(found);
        }

        [Fact]
        public void TwoLargeNearlyEqualCircles_MeetWhereTheyCrossAskedTheOtherWayRound()
        {
            var found = new List<GeoPoint2>();

            CurveMeet2.Circles(found, SecondCentre, SecondRadius, FirstCentre, FirstRadius, Tolerance.Default);

            AssertTheCrossings(found);
        }
    }
}
