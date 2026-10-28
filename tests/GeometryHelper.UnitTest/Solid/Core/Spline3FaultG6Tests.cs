using System;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// A copy of a spline that crosses itself, in a tilted plane of space, crosses the spline at that crossing, inside the stretch the two
    /// lie along each other, as in the plane (<see cref="Plane.Spline2FaultG6Tests"/>).
    /// </summary>
    /// <remarks>
    /// The pair is measured in space, where a stretch along is not cut either, and the crossing at the loop's own crossing was never
    /// looked for.
    /// </remarks>
    public class Spline3FaultG6Tests
    {
        private static readonly Tolerance Thousandth = Tolerance.Default;

        private static GeoPoint2 P(double x, double y) => new GeoPoint2(x, y);

        private static readonly GeoCoordinateSystem3 Tilted = new GeoCoordinateSystem3(new GeoPoint3(10, 20, 30), new GeoVector3(1, 2, 3), new GeoVector3(-2, 1, 0.5));

        [Theory]
        [InlineData(3E-4, 4E-4, 3)]
        [InlineData(-5E-4, 1E-4, 2)]
        [InlineData(2E-4, -6E-4, 3)]
        public void ACopyOfASplineThatCrossesItselfInATiltedPlane_CrossesItAtTheLoopsOwnCrossing(double x, double y, int count)
        {
            GeoSpline2 flat = GeoSpline2.FromControlPoints(3, new[] { P(0, 0), P(150, 100), P(-50, 100), P(100, 0) });
            GeoSpline3 loop = flat.ToSpline3(Tilted);
            GeoSpline3 copy = flat.Translate(new GeoVector2(x, y)).ToSpline3(Tilted);
            GeoPoint3 crossing = Tilted.Origin.Add(Tilted.XAxis.Multiply(50)).Add(Tilted.YAxis.Multiply(42.857142857142854));

            foreach (GeoPoint3[] places in new[] { loop.GetIntersections(copy, Thousandth), copy.GetIntersections(loop, Thousandth) })
            {
                Assert.Equal(count, places.Length);
                Assert.Contains(places, p => p.DistanceTo(crossing) <= 1E-3);
                Assert.All(places, p => Assert.InRange(loop.DistanceTo(p), 0.0, 1E-9));
                Assert.All(places, p => Assert.InRange(copy.DistanceTo(p), 0.0, 1E-9));
            }
        }
    }
}
