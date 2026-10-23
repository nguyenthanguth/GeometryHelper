using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// Two ellipses in space nearly in one plane are read the same whichever is asked first: read in the plane of the larger
    /// only where the smaller lies within the planar tolerance of it, they collided one way round and not the other.
    /// </summary>
    public class Ellipse3FaultF1OrderTests
    {
        private static readonly Tolerance Thousandth = Tolerance.Default;

        [Fact]
        public void AnEllipseTiltedAMillionthOfARadianOffAnother_CollidesWithItWhicheverIsAskedFirst()
        {
            // Sampled, the rims come within 0.00027 of each other at four places. The smaller lies within the planar tolerance
            // of the larger's plane; the larger's rim stands up to 0.0015 off the smaller's.
            var small = new GeoEllipse3(GeoPoint3.Origin, new GeoVector3(0, 0, 1), new GeoVector3(1, 0, 0), 192.3734706325333, 32.17849110820261);
            var large = new GeoEllipse3(
                new GeoPoint3(-27.700839251140522, 61.47480819442998, -0.0002229143852474701),
                new GeoVector3(-9.259874143907305E-07, -2.8137816482094704E-06, 0.9999999999956126),
                new GeoVector3(0.7539022543433912, 0.6569865987137538, 2.546720833794273E-06),
                496.3384276238915,
                45.67828582864175);

            Assert.True(small.CollidesWith(large, Thousandth));
            Assert.True(large.CollidesWith(small, Thousandth));

            GeoPoint3[] one = small.GetIntersections(large, Thousandth);
            GeoPoint3[] other = large.GetIntersections(small, Thousandth);

            Assert.Equal(4, one.Length);
            Assert.Equal(one, other);
            Assert.All(one, place => Assert.InRange(System.Math.Max(small.DistanceTo(place), large.DistanceTo(place)), 0.0, Thousandth.EqualPoint));
        }
    }
}
