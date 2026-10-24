using System;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// A circle and an arc in space are asked about an ellipse as the ellipse is asked about them, with the same answer to
    /// the bit: the ellipse could be asked of a circle or an arc, and they could not be asked of it.
    /// </summary>
    public class CurveFaultF8OverloadTests
    {
        private static readonly Tolerance Thousandth = Tolerance.Default;

        private static readonly GeoEllipse3 Flat = new GeoEllipse3(GeoPoint3.Origin, new GeoVector3(0, 0, 1), new GeoVector3(1, 0, 0), 300, 100);

        /// <summary>
        /// The circle of radius 50 standing upright on the X axis about (250, 0, 0): it crosses the flat ellipse's rim at
        /// (300, 0, 0).
        /// </summary>
        private static readonly GeoCircle3 Upright = new GeoCircle3(new GeoPoint3(250, 0, 0), new GeoVector3(0, 1, 0), 50);

        [Fact]
        public void ACircleAskedAboutAnEllipse_AnswersAsTheEllipseAskedAboutTheCircle()
        {
            Assert.Equal(Flat.CollidesWith(Upright, Thousandth), Upright.CollidesWith(Flat, Thousandth));
            Assert.Equal(Flat.GetIntersections(Upright, Thousandth), Upright.GetIntersections(Flat, Thousandth));
            Assert.Equal(0.0, Assert.Single(Upright.GetIntersections(Flat, Thousandth)).DistanceTo(new GeoPoint3(300, 0, 0)), 9);
            Assert.Equal(Flat.GetIntersections(Upright), Upright.GetIntersections(Flat));
        }

        [Fact]
        public void AnArcAskedAboutAnEllipse_AnswersAsTheEllipseAskedAboutTheArc()
        {
            GeoArc3 half = GeoArc3.FromThreePoints(new GeoPoint3(250, 0, 50), new GeoPoint3(300, 0, 0), new GeoPoint3(250, 0, -50));

            Assert.Equal(Flat.CollidesWith(half, Thousandth), half.CollidesWith(Flat, Thousandth));
            Assert.Equal(Flat.GetIntersections(half, Thousandth), half.GetIntersections(Flat, Thousandth));
            Assert.Equal(0.0, Assert.Single(half.GetIntersections(Flat, Thousandth)).DistanceTo(new GeoPoint3(300, 0, 0)), 9);
            Assert.Equal(Flat.GetIntersections(half), half.GetIntersections(Flat));
        }

        [Fact]
        public void ACircleAndAnArcPassingAnEllipseWithinTheTolerance_TouchItAskedFromTheirSide()
        {
            // A circle of radius 50 leaning 0.01 radians, the end of its diameter along X 0.0005 above (0, 100, 0), where its
            // rim runs square to the ellipse's; and the half of it round that end.
            var axis = new GeoVector3(Math.Cos(0.01), 0, -Math.Sin(0.01));
            var circle = new GeoCircle3(new GeoPoint3(0, 100, 0.0005).Subtract(axis.Multiply(50)), new GeoVector3(Math.Sin(0.01), 0, Math.Cos(0.01)), 50);
            GeoPoint3 At(double angle) => circle.Center.Add(axis.Multiply(50 * Math.Cos(angle))).Add(new GeoVector3(0, 1, 0).Multiply(50 * Math.Sin(angle)));
            GeoArc3 arc = GeoArc3.FromThreePoints(At(-1.5), At(0), At(1.5));

            Assert.True(circle.CollidesWith(Flat, Thousandth));
            Assert.True(arc.CollidesWith(Flat, Thousandth));
            Assert.Equal(0.0, Assert.Single(circle.GetIntersections(Flat, Thousandth)).DistanceTo(new GeoPoint3(0, 100, 0.00025)), 9);
            Assert.Equal(0.0, Assert.Single(arc.GetIntersections(Flat, Thousandth)).DistanceTo(new GeoPoint3(0, 100, 0.00025)), 9);
        }
    }
}
