using System.Linq;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// Two circles in space nearly in one plane give the same places whichever is asked first: read from the one asked first
    /// alone, 65 of 4 000 pairs tilted 1E-7 to 1E-3 radians off each other found a different number of places the one way
    /// round and the other.
    /// </summary>
    public class Circle3FaultF5OrderTests
    {
        private static readonly Tolerance Thousandth = Tolerance.Default;

        [Fact]
        public void ACircleTiltedFourMillionthsOfARadianOffAnother_GivesTheSameTwoPlacesWhicheverIsAskedFirst()
        {
            // Each lies within the planar tolerance of the other's plane. Asked the one way the pair crossed at
            // (421.493, -216.198, 0) only, asked the other at (-442.545, 168.972, 0.001) as well.
            var one = new GeoCircle3(GeoPoint3.Origin, new GeoVector3(0, 0, 1), 473.706316190635);
            var two = new GeoCircle3(
                new GeoPoint3(-37.51983770053826, -84.16675933830754, 0.0008827762221371645),
                new GeoVector3(5.062732878271126E-07, -6.504263396403756E-07, 0.9999999999996604),
                477.6240549877398);

            GeoPoint3[] first = one.GetIntersections(two, Thousandth);
            GeoPoint3[] second = two.GetIntersections(one, Thousandth);

            Assert.Equal(2, first.Length);
            Assert.Equal(first, second.Reverse());
            Assert.Contains(first, place => place.DistanceTo(new GeoPoint3(421.493, -216.198, 0)) < 0.01);
            Assert.Contains(first, place => place.DistanceTo(new GeoPoint3(-442.545, 168.972, 0.001)) < 0.01);

            foreach (GeoPoint3 place in first)
            {
                Assert.InRange(one.GetClosestPointOnBoundary(place).DistanceTo(place), 0.0, Thousandth.EqualPoint);
                Assert.InRange(two.GetClosestPointOnBoundary(place).DistanceTo(place), 0.0, Thousandth.EqualPoint);
            }
        }
    }
}
