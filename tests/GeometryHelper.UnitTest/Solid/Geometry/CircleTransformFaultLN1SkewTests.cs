using System;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// A transformation that keeps the lengths of two axes of a circle's plane but skews them is refused for a whole circle, as
    /// a scaling uneven between them is; an arc goes through where its ends and its middle land where that lands within the
    /// tolerance of the ellipse it becomes, and is refused otherwise.
    /// </summary>
    /// <remarks>
    /// The circle was taken to stay round when the two axes of its plane came out of the transformation equally long, to
    /// the vector tolerance and with no regard to the size of the stretch, and the angle between them was never looked at.
    /// The skew that sends X to (1, 0, 0) and Y to (0.6, 0.8, 0) keeps both lengths, so the circle of radius 100 in the plan
    /// came out a circle of radius 100, though it becomes an ellipse of radii 126.49 and 63.25. Round now means that the two
    /// axes come out equally long and square to each other, to the default vector tolerance of the square of the stretch,
    /// as a ball is read, so that a turn whose numbers are rounded to six places or to a float still moves the circle.
    /// </remarks>
    public class CircleTransformFaultLN1SkewTests
    {
        private static readonly GeoTransform3 Skew = new GeoTransform3(new double[,]
        {
            { 1, 0.6, 0, 0 },
            { 0, 0.8, 0, 0 },
            { 0, 0, 1, 0 },
            { 0, 0, 0, 1 },
        });

        private static GeoCircle3 InThePlan() => new GeoCircle3(new GeoPoint3(10, 20, 30), GeoVector3.ZAxis, 100);

        [Fact]
        public void ASkewThatKeepsBothLengths_IsRefusedForACircle()
        {
            Assert.Throws<InvalidOperationException>(() => Skew.Transform(InThePlan()));
            Assert.Throws<InvalidOperationException>(() => InThePlan().TransformBy(Skew));
        }

        [Fact]
        public void ASkewThatKeepsBothLengths_IsRefusedForAnArcItWouldLandFarOff()
        {
            // An arc under a skew is the arc through where its two ends and its middle land only where that lands within the
            // tolerance of the ellipse it becomes (LN4); this one, of radius 177.7, would stand far off it.
            var arc = new GeoArc3(new GeoPoint3(10, 20, 30), GeoVector3.ZAxis, 100, 0.5, 2.0);

            Assert.Throws<InvalidOperationException>(() => arc.TransformBy(Skew));
        }

        [Fact]
        public void ASkewThatKeepsBothLengths_IsRefusedForAnArcOfAWholeTurn()
        {
            // A whole turn has no ends to go through: it is the whole circle, and refused as the circle is.
            var arc = new GeoArc3(new GeoPoint3(10, 20, 30), GeoVector3.ZAxis, 100, 0.5, 0.5 + 2 * Math.PI);

            Assert.Throws<InvalidOperationException>(() => arc.TransformBy(Skew));
        }

        [Fact]
        public void TheSkewedCircle_IsTheEllipseItBecomes()
        {
            GeoEllipse3 ellipse = GeoEllipse3.FromCircle(InThePlan()).TransformBy(Skew);

            Assert.Equal(100 * Math.Sqrt(1.6), ellipse.MajorRadius, 9);
            Assert.Equal(100 * Math.Sqrt(0.4), ellipse.MinorRadius, 9);
        }

        [Fact]
        public void ASkewOfATenThousandthOfARadian_IsRefused()
        {
            // Y sent to (sin 1E-4, cos 1E-4, 0): both axes a unit long, a ten-thousandth of a radian off square, ten times
            // the vector tolerance.
            var skew = new GeoTransform3(new double[,]
            {
                { 1, Math.Sin(1E-4), 0, 0 },
                { 0, Math.Cos(1E-4), 0, 0 },
                { 0, 0, 1, 0 },
                { 0, 0, 0, 1 },
            });

            Assert.Throws<InvalidOperationException>(() => skew.Transform(InThePlan()));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ATurnRoundedToSixPlacesOrToAFloat_StillMovesTheCircle(bool toFloat)
        {
            // The turn of 0.7 radians about (0.3, 0.7, -0.2) and a move, every number of it rounded as a file or a float
            // holds it: the axes come out a few 1E-7 off a unit long and off square.
            GeoTransform3 exact = GeoTransform3.Translation(new GeoVector3(1E5, 2E5, 3))
                .Multiply(GeoTransform3.RotationAxis(new GeoVector3(0.3, 0.7, -0.2), 0.7));
            var rounded = new double[4, 4];

            for (int row = 0; row < 4; row++)
            {
                for (int column = 0; column < 4; column++)
                {
                    double value = exact[row, column];
                    rounded[row, column] = toFloat ? (float)value : Math.Round(value, 6);
                }
            }

            GeoCircle3 moved = new GeoTransform3(rounded).Transform(InThePlan());

            Assert.Equal(100.0, moved.Radius, 4);
            Assert.True(moved.Center.DistanceTo(exact.Transform(InThePlan().Center)) < 0.1);
        }

        [Fact]
        public void ATurnAShiftAMirrorAndAnEvenScaling_StillMoveTheCircle()
        {
            GeoTransform3 transform = GeoTransform3.Translation(new GeoVector3(5, -7, 9))
                .Multiply(GeoTransform3.RotationAxis(new GeoVector3(1, 2, 3), 0.7))
                .Multiply(GeoTransform3.Mirror(new GeoPlane3(new GeoPoint3(1, 1, 1), new GeoVector3(0, 1, 1))))
                .Multiply(GeoTransform3.Scaling(2.5));

            GeoCircle3 moved = transform.Transform(InThePlan());

            Assert.Equal(250.0, moved.Radius, 9);
            Assert.Equal(0.0, moved.Center.DistanceTo(transform.Transform(InThePlan().Center)), 9);
        }

        [Fact]
        public void AnEvenScalingByAThousand_StillMovesTheCircle()
        {
            // The lengths are compared as a share of the stretch, so the test reads the same at any size.
            GeoTransform3 transform = GeoTransform3.RotationAxis(new GeoVector3(3, -1, 2), 1.1).Multiply(GeoTransform3.Scaling(1E3));

            Assert.Equal(1E5, transform.Transform(InThePlan()).Radius, 6);
        }
    }
}
