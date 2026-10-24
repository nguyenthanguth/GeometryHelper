using System;
using System.Linq;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// An ellipse, a circle or an arc in space touches a circle or an arc whose rim passes within the point tolerance of
    /// its own without crossing it on the line their planes share, and is given the place, whichever is asked first: read
    /// only where one rim crossed the other's plane, 1 056 of 2 000 ellipses and circles drawn to pass within 0.0012 of each
    /// other came within 0.00098 and collided nowhere, and circles and arcs that collided were given no place.
    /// </summary>
    public class CurveFaultF8NearPassTests
    {
        private static readonly Tolerance Thousandth = Tolerance.Default;

        private static readonly GeoEllipse3 Flat = new GeoEllipse3(GeoPoint3.Origin, new GeoVector3(0, 0, 1), new GeoVector3(1, 0, 0), 300, 100);

        private static readonly GeoCircle3 FlatCircle = new GeoCircle3(GeoPoint3.Origin, new GeoVector3(0, 0, 1), 100);

        /// <summary>The quarter of that circle round (0, 100, 0).</summary>
        private static readonly GeoArc3 FlatArc = GeoArc3.FromThreePoints(
            new GeoPoint3(70.71067811865476, 70.71067811865476, 0),
            new GeoPoint3(0, 100, 0),
            new GeoPoint3(-70.71067811865476, 70.71067811865476, 0));

        private static readonly GeoVector3 Axis = new GeoVector3(Math.Cos(0.01), 0, -Math.Sin(0.01));

        private static readonly GeoVector3 Lean = new GeoVector3(Math.Sin(0.01), 0, Math.Cos(0.01));

        /// <summary>
        /// A circle of radius 50 leaning 0.01 radians about the Y axis, the end of its diameter along X a height above
        /// (0, 100, 0), where its rim runs along Y, square to the flat ones'.
        /// </summary>
        private static GeoCircle3 Leaning(double above) => new GeoCircle3(new GeoPoint3(0, 100, above).Subtract(Axis.Multiply(50)), Lean, 50);

        /// <summary>The half of that circle round the same end.</summary>
        private static GeoArc3 LeaningArc(double above)
        {
            GeoCircle3 circle = Leaning(above);
            GeoPoint3 At(double angle) => circle.Center.Add(Axis.Multiply(50 * Math.Cos(angle))).Add(new GeoVector3(0, 1, 0).Multiply(50 * Math.Sin(angle)));

            return GeoArc3.FromThreePoints(At(-1.5), At(0), At(1.5));
        }

        private static void AssertTheTouch(GeoPoint3[] places, double above)
        {
            GeoPoint3 place = Assert.Single(places);

            Assert.InRange(place.DistanceTo(new GeoPoint3(0, 100, 0.5 * above)), 0.0, 1E-8);
        }

        [Theory]
        [InlineData(0.0005)]
        [InlineData(0.0009)]
        public void AnEllipseAndALeaningCirclePassingWithinTheTolerance_TouchWhicheverIsAskedFirst(double above)
        {
            GeoCircle3 circle = Leaning(above);

            Assert.True(Flat.CollidesWith(circle, Thousandth));
            AssertTheTouch(Flat.GetIntersections(circle, Thousandth), above);
        }

        [Theory]
        [InlineData(0.0005)]
        [InlineData(0.0009)]
        public void AnEllipseAndALeaningArcPassingWithinTheTolerance_TouchWhicheverIsAskedFirst(double above)
        {
            GeoArc3 arc = LeaningArc(above);

            Assert.True(Flat.CollidesWith(arc, Thousandth));
            AssertTheTouch(Flat.GetIntersections(arc, Thousandth), above);
        }

        [Theory]
        [InlineData(0.0005)]
        [InlineData(0.0009)]
        public void TwoCirclesThatCollideWithoutCrossing_AreGivenThePlaceTheyTouch(double above)
        {
            GeoCircle3 circle = Leaning(above);

            Assert.True(FlatCircle.CollidesWith(circle, Thousandth));
            AssertTheTouch(FlatCircle.GetIntersections(circle, Thousandth), above);
            Assert.Equal(FlatCircle.GetIntersections(circle, Thousandth), circle.GetIntersections(FlatCircle, Thousandth));
        }

        [Theory]
        [InlineData(0.0005)]
        [InlineData(0.0009)]
        public void TwoArcsThatCollideWithoutCrossing_AreGivenThePlaceTheyTouch(double above)
        {
            GeoArc3 arc = LeaningArc(above);

            Assert.True(FlatArc.CollidesWith(arc, Thousandth));
            AssertTheTouch(FlatArc.GetIntersections(arc, Thousandth), above);
            Assert.Equal(FlatArc.GetIntersections(arc, Thousandth), arc.GetIntersections(FlatArc, Thousandth));
        }

        [Fact]
        public void CurvesPassingFurtherApartThanTheTolerance_DoNotTouch()
        {
            GeoCircle3 circle = Leaning(0.0011);
            GeoArc3 arc = LeaningArc(0.0011);

            Assert.False(Flat.CollidesWith(circle, Thousandth));
            Assert.Empty(Flat.GetIntersections(circle, Thousandth));
            Assert.False(Flat.CollidesWith(arc, Thousandth));
            Assert.Empty(Flat.GetIntersections(arc, Thousandth));
            Assert.False(FlatCircle.CollidesWith(circle, Thousandth));
            Assert.Empty(FlatCircle.GetIntersections(circle, Thousandth));
            Assert.False(FlatArc.CollidesWith(arc, Thousandth));
            Assert.Empty(FlatArc.GetIntersections(arc, Thousandth));
        }

        [Fact]
        public void TwoArcsNearlyInOnePlane_GiveTheSamePlacesWhicheverIsAskedFirst()
        {
            // Each lies within the planar tolerance of the other's plane; read from the one asked first alone, they crossed
            // at one place asked the one way and at two asked the other.
            var one = new GeoArc3(GeoPoint3.Origin, new GeoVector3(0, 0, 1), 473.706316190635, 0, 4.5);
            var two = new GeoArc3(
                new GeoPoint3(-37.51983770053826, -84.16675933830754, 0.0008827762221371645),
                new GeoVector3(5.062732878271126E-07, -6.504263396403756E-07, 0.9999999999996604),
                477.6240549877398,
                1,
                6);

            GeoPoint3[] first = one.GetIntersections(two, Thousandth);
            GeoPoint3[] second = two.GetIntersections(one, Thousandth);

            Assert.Equal(2, first.Length);
            Assert.Equal(first, second.Reverse());
        }

        [Fact]
        public void TwoEllipsesInOnePlane_GiveTheSamePointsToTheLastDigitWhicheverIsAskedFirst()
        {
            var turned = new GeoEllipse3(GeoPoint3.Origin, new GeoVector3(0, 0, 1), new GeoVector3(0, 1, 0), 200, 150);

            GeoPoint3[] first = Flat.GetIntersections(turned, Thousandth);
            GeoPoint3[] second = turned.GetIntersections(Flat, Thousandth);

            Assert.Equal(4, first.Length);
            Assert.Equal(first.OrderBy(p => p.X).ThenBy(p => p.Y), second.OrderBy(p => p.X).ThenBy(p => p.Y));

            // Each in the order of its own eccentric angle, the first at 63.4 degrees.
            Assert.Equal(0.0, first[0].DistanceTo(new GeoPoint3(134.1640786499874, 89.44271909999159, 0)), 9);
            Assert.Equal(0.0, second[0].DistanceTo(new GeoPoint3(-134.1640786499874, 89.44271909999159, 0)), 9);
        }
    }
}
