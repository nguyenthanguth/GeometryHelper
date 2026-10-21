using System;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// A transformation of the plane that keeps the lengths of its two axes but skews them is refused for a circle, as a
    /// scaling uneven between them is.
    /// </summary>
    /// <remarks>
    /// The circle was taken to stay round when the two axes came out of the transformation equally long, to the vector
    /// tolerance and with no regard to the size of the stretch, and the angle between them was never looked at. The skew
    /// that sends X to (1, 0) and Y to (0.6, 0.8) keeps both lengths, so the circle of radius 100 came out a circle of
    /// radius 100, though it becomes an ellipse of radii 126.49 and 63.25. Round now means that the two axes come out
    /// equally long and square to each other, to the default vector tolerance of the square of the stretch, as a ball in
    /// space is read, so that a turn whose numbers are rounded to six places or to a float still moves the circle.
    /// </remarks>
    public class CircleTransform2FaultLN1SkewTests
    {
        private static readonly GeoTransform2 Skew = new GeoTransform2(new double[,]
        {
            { 1, 0.6, 0 },
            { 0, 0.8, 0 },
            { 0, 0, 1 },
        });

        private static GeoCircle2 Circle() => new GeoCircle2(new GeoPoint2(10, 20), 100);

        [Fact]
        public void ASkewThatKeepsBothLengths_IsRefusedForACircle()
        {
            Assert.Throws<InvalidOperationException>(() => Skew.Transform(Circle()));
            Assert.Throws<InvalidOperationException>(() => Circle().TransformBy(Skew));
        }

        [Fact]
        public void TheSkewedCircle_IsTheEllipseItBecomes()
        {
            GeoEllipse2 ellipse = GeoEllipse2.FromCircle(Circle()).TransformBy(Skew);

            Assert.Equal(100 * Math.Sqrt(1.6), ellipse.MajorRadius, 9);
            Assert.Equal(100 * Math.Sqrt(0.4), ellipse.MinorRadius, 9);
        }

        [Fact]
        public void ASkewOfATenThousandthOfARadian_IsRefused()
        {
            // Y sent to (sin 1E-4, cos 1E-4): both axes a unit long, a ten-thousandth of a radian off square, ten times the
            // vector tolerance.
            var skew = new GeoTransform2(new double[,]
            {
                { 1, Math.Sin(1E-4), 0 },
                { 0, Math.Cos(1E-4), 0 },
                { 0, 0, 1 },
            });

            Assert.Throws<InvalidOperationException>(() => skew.Transform(Circle()));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ATurnRoundedToSixPlacesOrToAFloat_StillMovesTheCircle(bool toFloat)
        {
            // The turn of 0.7 radians and a move by (5, 6), every number of it rounded as a file or a float holds it.
            double Round(double value) => toFloat ? (float)value : Math.Round(value, 6);
            var rounded = new GeoTransform2(new double[,]
            {
                { Round(Math.Cos(0.7)), -Round(Math.Sin(0.7)), 5 },
                { Round(Math.Sin(0.7)), Round(Math.Cos(0.7)), 6 },
                { 0, 0, 1 },
            });

            GeoCircle2 moved = rounded.Transform(Circle());

            Assert.Equal(100.0, moved.Radius, 4);
        }

        [Fact]
        public void ATurnAShiftAMirrorAndAnEvenScaling_StillMoveTheCircle()
        {
            GeoTransform2 transform = GeoTransform2.Translation(new GeoVector2(5, -7))
                .Multiply(GeoTransform2.Rotation(0.7))
                .Multiply(GeoTransform2.Mirror(new GeoLine2(new GeoPoint2(1, 1), new GeoPoint2(3, 2))))
                .Multiply(GeoTransform2.Scaling(2.5));

            GeoCircle2 moved = transform.Transform(Circle());

            Assert.Equal(250.0, moved.Radius, 9);
            Assert.Equal(0.0, moved.Center.DistanceTo(transform.Transform(Circle().Center)), 9);
        }

        [Fact]
        public void AnEvenScalingByAThousand_StillMovesTheCircle()
        {
            // The lengths are compared as a share of the stretch, so the test reads the same at any size.
            GeoTransform2 transform = GeoTransform2.Rotation(1.1).Multiply(GeoTransform2.Scaling(1E3));

            Assert.Equal(1E5, transform.Transform(Circle()).Radius, 6);
        }

        [Fact]
        public void AnUnevenScaling_IsStillRefused()
        {
            Assert.Throws<InvalidOperationException>(() => GeoTransform2.Scaling(1, 2).Transform(Circle()));
        }
    }
}
