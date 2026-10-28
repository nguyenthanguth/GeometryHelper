using System;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// A part cut off a spline in space shorter than the point tolerance can be flattened; a touch far from the origin in a tilted
    /// plane is one place; and a spline and its near copy in a tilted plane give the places the plane gives, whichever is asked.
    /// </summary>
    /// <remarks>
    /// As in the plane (<see cref="Plane.Spline2FaultF6Tests"/>): the part threw from <c>Flatten</c>, the touch was three places, and
    /// the copy gave a place more or fewer by which of the two was asked first.
    /// </remarks>
    public class Spline3FaultF6Tests
    {
        private static readonly Tolerance Thousandth = Tolerance.Default;

        private static GeoPoint2 P(double x, double y) => new GeoPoint2(x, y);

        private static readonly GeoCoordinateSystem3 Tilted = new GeoCoordinateSystem3(new GeoPoint3(10, 20, 30), new GeoVector3(1, 2, 3), new GeoVector3(-2, 1, 0.5));

        [Fact]
        public void APartCutOffWithinThePointToleranceOfTheStart_FlattensToTheChainOfItsCorners()
        {
            GeoSpline3 spline = GeoSpline3.Interpolate(new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(30, 20, 5), new GeoPoint3(60, -10, 10), new GeoPoint3(100, 0, 0) }, 3, Thousandth);
            GeoSpline3 part = spline.Split(1E-6)[0];

            Assert.InRange(part.Length, 1E-4, 1E-3);

            GeoPolyline3 chain = part.Flatten(1E-3);

            Assert.True(chain.Vertices.Count >= 2);
            Assert.Equal(part.GetPointAtParameter(0.0), chain.Vertices[0]);
            Assert.Equal(part.GetPointAtParameter(1.0), chain.Vertices[chain.Vertices.Count - 1]);
        }

        [Fact]
        public void ASplineTouchingACircleSplineInATiltedPlaneAMillionOut_TouchesItOnce()
        {
            double at = 1E6;
            var frame = new GeoCoordinateSystem3(new GeoPoint3(at, -at, at), new GeoVector3(1, 2, 3), new GeoVector3(-2, 1, 0.5));
            GeoSpline3 ring = GeoSpline2.FromCircle(new GeoCircle2(P(0, 0), 100)).ToSpline3(frame);
            GeoSpline3 bow = GeoSpline2.Interpolate(new[] { P(-300, 100.5), P(0, 100), P(300, 100.5) }, 2, Thousandth).ToSpline3(frame);
            GeoPoint3 top = frame.Origin.Add(frame.YAxis.Multiply(100));

            Assert.InRange(Assert.Single(bow.GetIntersections(ring, Thousandth)).DistanceTo(top), 0.0, 2E-3);
            Assert.InRange(Assert.Single(ring.GetIntersections(bow, Thousandth)).DistanceTo(top), 0.0, 2E-3);
        }

        [Fact]
        public void ACopyThatOverhangsTheEndOfASplineInATiltedPlane_GivesThePlacesThePlaneGives()
        {
            GeoSpline2 flat = GeoSpline2.FromControlPoints(2, new[]
            {
                P(11.145065157276147, -23.358664202205215), P(-25.06507479355907, 10.221748678116942), P(14.831623567655509, 12.513038191251942),
                P(-21.560436120052096, -7.946054617849204), P(-28.635031403338086, -22.31766032628606),
            });
            GeoSpline2 flatCopy = flat.Translate(new GeoVector2(-0.00031086647951441887, -0.0006647301861314643));
            GeoSpline3 spline = flat.ToSpline3(Tilted);
            GeoSpline3 copy = flatCopy.ToSpline3(Tilted);

            GeoPoint3[] places = spline.GetIntersections(copy, Thousandth);

            // The two shallow crossings, and a third where the spline crosses itself, square on, which its other branch crosses the copy
            // at twice, 0.00072 apart, one place (G6); the count of two left it out, as in the plane.
            Assert.Equal(flat.GetIntersections(flatCopy, Thousandth).Length, places.Length);
            Assert.Equal(3, places.Length);
            Assert.Equal(places.Length, copy.GetIntersections(spline, Thousandth).Length);
            Assert.All(places, p => Assert.InRange(copy.DistanceTo(p), 0.0, 1E-9));
        }
    }
}
