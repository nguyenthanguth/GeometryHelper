using System;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// The ball in space as a value: how it is made, moved and transformed, what it measures, where a point stands to it, how it is
    /// compared and printed. Every expectation is worked out by hand.
    /// </summary>
    /// <remarks>
    /// The tolerances are a thousandth for points and 1E-5 for vectors, as the default. Offsets of 0.0005 and 0.002 sit either side of
    /// the thousandth, a factor of two clear of it, so no answer here hangs on rounding at the boundary.
    /// </remarks>
    public class GeoSphere3Tests
    {
        private static readonly Tolerance Strict = new Tolerance(1E-3, 1E-5);

        private static GeoPoint3 P(double x, double y, double z) => new GeoPoint3(x, y, z);

        private static GeoVector3 V(double x, double y, double z) => new GeoVector3(x, y, z);

        private static GeoSphere3 Ball() => new GeoSphere3(P(0, 0, 0), 50);

        #region Making one

        [Fact]
        public void ABallKeepsItsCentreAndRadius()
        {
            var ball = new GeoSphere3(P(1, 2, 3), 4);

            Assert.Equal(P(1, 2, 3), ball.Center);
            Assert.Equal(4.0, ball.Radius);
            Assert.Equal(8.0, ball.Diameter);
            Assert.True(ball.IsValid);
        }

        [Theory]
        [InlineData(0.0)]
        [InlineData(-1.0)]
        [InlineData(double.NaN)]
        [InlineData(double.PositiveInfinity)]
        [InlineData(double.NegativeInfinity)]
        public void ARadiusOfNoSizeOrNoNumber_IsRefused(double radius)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new GeoSphere3(P(0, 0, 0), radius));
        }

        [Fact]
        public void ADefaultBall_IsNotValid_AndABallAtANotANumber_IsNotEither()
        {
            Assert.False(default(GeoSphere3).IsValid);
            Assert.False(new GeoSphere3(P(double.NaN, 0, 0), 1).IsValid);
        }

        [Fact]
        public void ACopy_IsTheOriginalToTheLastBit()
        {
            var ball = new GeoSphere3(P(0.1, 0.2, 0.3), 0.7);

            Assert.Equal(ball, ball.Clone());
            Assert.True(ball == ball.Clone());
            Assert.False(ball != ball.Clone());
        }

        #endregion

        #region What it measures

        [Fact]
        public void ABallOfRadiusFifty_HoldsFiveHundredTwentyThreeThousandCubicUnits_AndHasASurfaceOfThirtyOneThousand()
        {
            Assert.Equal(4.0 / 3.0 * Math.PI * 125000.0, Ball().Volume, 6);
            Assert.Equal(523598.775598, Ball().Volume, 5);
            Assert.Equal(4.0 * Math.PI * 2500.0, Ball().SurfaceArea, 6);
            Assert.Equal(31415.926536, Ball().SurfaceArea, 5);
        }

        [Fact]
        public void TheBoxOfABall_IsTheCentreLessAndPlusTheRadiusOnEveryAxis()
        {
            GeoAabb3 box = new GeoSphere3(P(10, 20, 30), 5).GetAabb();

            Assert.Equal(P(5, 15, 25), box.Min);
            Assert.Equal(P(15, 25, 35), box.Max);
        }

        #endregion

        #region Where a point stands

        [Fact]
        public void APointThirtyOutFromTheCentre_IsInside_AndOneSixtyOutIsOutside()
        {
            Assert.Equal(PointLocation.Inside, Ball().Locate(P(30, 0, 0), Strict));
            Assert.Equal(PointLocation.OutSide, Ball().Locate(P(60, 0, 0), Strict));
            Assert.Equal(PointLocation.Inside, Ball().Locate(P(0, 0, 0), Strict));
        }

        [Fact]
        public void APointWithinAThousandthOfTheSurface_IsOnIt_EitherSide()
        {
            Assert.Equal(PointLocation.OnSide, Ball().Locate(P(50.0005, 0, 0), Strict));
            Assert.Equal(PointLocation.OnSide, Ball().Locate(P(49.9995, 0, 0), Strict));
            Assert.Equal(PointLocation.OutSide, Ball().Locate(P(50.002, 0, 0), Strict));
            Assert.Equal(PointLocation.Inside, Ball().Locate(P(49.998, 0, 0), Strict));
        }

        [Fact]
        public void ABallHoldsItsSurfaceAndItsInside_ButNotWhatIsOutside()
        {
            Assert.True(Ball().Contains(P(30, 0, 0), Strict));
            Assert.True(Ball().Contains(P(50.0005, 0, 0), Strict));
            Assert.False(Ball().Contains(P(50.002, 0, 0), Strict));
        }

        [Fact]
        public void OnlyAPointOnTheShellIsOnTheSurface_NotOneInside()
        {
            Assert.True(Ball().IsPointOn(P(0, 50, 0), Strict));
            Assert.True(Ball().IsPointOn(P(0, 0, -50.0005), Strict));
            Assert.False(Ball().IsPointOn(P(0, 0, 0), Strict));
            Assert.False(Ball().IsPointOn(P(0, 0, 60), Strict));
        }

        [Fact]
        public void TheFormsWithoutATolerance_AnswerWhatTheDefaultToleranceDoes()
        {
            Assert.Equal(Ball().Locate(P(50.0005, 0, 0), Tolerance.Default), Ball().Locate(P(50.0005, 0, 0)));
            Assert.Equal(Ball().Contains(P(50.0005, 0, 0), Tolerance.Default), Ball().Contains(P(50.0005, 0, 0)));
            Assert.Equal(Ball().IsPointOn(P(50.0005, 0, 0), Tolerance.Default), Ball().IsPointOn(P(50.0005, 0, 0)));
            Assert.Equal(Ball().SignedDistanceTo(P(50.0005, 0, 0), Tolerance.Default), Ball().SignedDistanceTo(P(50.0005, 0, 0)));
        }

        [Fact]
        public void TheNearestPointOfTheSurface_IsWhereTheLineFromTheCentreLeavesTheBall()
        {
            Assert.True(P(0, 0, 50).IsEqualTo(Ball().GetClosestPointOnBoundary(P(0, 0, 200)), Strict));
            Assert.True(P(0, 0, 50).IsEqualTo(Ball().GetClosestPointOnBoundary(P(0, 0, 10)), Strict));
            Assert.True(P(30, 40, 0).IsEqualTo(Ball().GetClosestPointOnBoundary(P(3, 4, 0)), Strict));
        }

        [Fact]
        public void TheCentre_GetsTheFirstAxisOneRadiusFromIt_EveryTime()
        {
            var ball = new GeoSphere3(P(10, 20, 30), 5);

            Assert.Equal(P(15, 20, 30), ball.GetClosestPointOnBoundary(P(10, 20, 30)));
            Assert.Equal(P(15, 20, 30), ball.GetClosestPointOnBoundary(P(10, 20, 30)));
        }

        [Fact]
        public void APointThirtyInOrThirtyOut_IsThirtyFromTheSurface_AndTheCentreFifty()
        {
            double[] reaches = { Ball().DistanceToSurface(P(20, 0, 0)), Ball().DistanceToSurface(P(80, 0, 0)) };

            Assert.Equal(30.0, reaches[0], 12);
            Assert.Equal(30.0, reaches[1], 12);
            Assert.Equal(50.0, Ball().DistanceToSurface(P(0, 0, 0)), 12);
            Assert.Equal(0.0, Ball().DistanceToSurface(P(0, 50, 0)), 12);
        }

        [Fact]
        public void TheSignedDistance_IsNegativeInside_NoughtOnTheSurface_AndPositiveOutside()
        {
            Assert.Equal(-30.0, Ball().SignedDistanceTo(P(20, 0, 0), Strict), 12);
            Assert.Equal(30.0, Ball().SignedDistanceTo(P(80, 0, 0), Strict), 12);
            Assert.Equal(-50.0, Ball().SignedDistanceTo(P(0, 0, 0), Strict), 12);
            Assert.Equal(0.0, Ball().SignedDistanceTo(P(50.0005, 0, 0), Strict));
            Assert.Equal(0.0, Ball().SignedDistanceTo(P(49.9995, 0, 0), Strict));
        }

        [Fact]
        public void TheSignedDistanceAgreesWithLocate_AtTheEdgeOfTheTolerance()
        {
            foreach (double x in new[] { 49.99, 49.998, 49.9995, 50.0, 50.0005, 50.002, 50.01 })
            {
                GeoPoint3 point = P(x, 0, 0);
                double signed = Ball().SignedDistanceTo(point, Strict);

                switch (Ball().Locate(point, Strict))
                {
                    case PointLocation.Inside:
                        Assert.True(signed < 0.0, "inside at " + x);
                        break;
                    case PointLocation.OnSide:
                        Assert.Equal(0.0, signed);
                        break;
                    default:
                        Assert.True(signed > 0.0, "outside at " + x);
                        break;
                }
            }
        }

        #endregion

        #region Moving and transforming

        [Fact]
        public void MovingABall_MovesTheCentreAndKeepsTheRadius()
        {
            GeoSphere3 moved = Ball().Translate(V(1, 2, 3));

            Assert.Equal(P(1, 2, 3), moved.Center);
            Assert.Equal(50.0, moved.Radius);
        }

        [Fact]
        public void ATurnAndAShift_LeaveTheRadiusExactlyAsItWas()
        {
            GeoTransform3 motion = GeoTransform3.Translation(V(3, -4, 5)).Multiply(GeoTransform3.RotationAxis(V(1, 2, 3), 0.7));
            var ball = new GeoSphere3(P(1, 2, 3), 7.3);
            GeoSphere3 moved = ball.TransformBy(motion);

            Assert.Equal(7.3, moved.Radius);
            Assert.Equal(motion.Transform(P(1, 2, 3)), moved.Center);
        }

        [Fact]
        public void AnEvenScalingByThree_ScalesTheRadiusByThree()
        {
            GeoSphere3 moved = new GeoSphere3(P(1, 2, 3), 50).TransformBy(GeoTransform3.Scaling(3.0));

            Assert.Equal(150.0, moved.Radius, 12);
            Assert.True(P(3, 6, 9).IsEqualTo(moved.Center, Strict));
        }

        [Fact]
        public void AMirror_LeavesABallABall()
        {
            GeoSphere3 moved = new GeoSphere3(P(1, 2, 3), 50).TransformBy(GeoTransform3.Mirror(GeoPlane3.XY));

            Assert.Equal(50.0, moved.Radius, 12);
            Assert.True(P(1, 2, -3).IsEqualTo(moved.Center, Strict));
        }

        [Fact]
        public void AScalingOfOneAxisOnly_IsRefusedRatherThanAnswerWithAnEllipsoid()
        {
            Assert.Throws<InvalidOperationException>(() => Ball().TransformBy(GeoTransform3.Scaling(3.0, 1.0, 1.0)));
            Assert.Throws<InvalidOperationException>(() => Ball().TransformBy(GeoTransform3.Scaling(1.0, 1.0, 0.5)));
        }

        [Fact]
        public void AShear_IsRefused()
        {
            var matrix = new double[4, 4] { { 1, 0.5, 0, 0 }, { 0, 1, 0, 0 }, { 0, 0, 1, 0 }, { 0, 0, 0, 1 } };

            Assert.Throws<InvalidOperationException>(() => Ball().TransformBy(new GeoTransform3(matrix)));
        }

        [Fact]
        public void AScalingToNothing_IsRefused()
        {
            Assert.Throws<InvalidOperationException>(() => Ball().TransformBy(GeoTransform3.Scaling(0.0)));
        }

        [Fact]
        public void ATransformationThatIsNull_IsRefused()
        {
            Assert.Throws<ArgumentNullException>(() => Ball().TransformBy(null));
        }

        [Fact]
        public void TransformingThroughEitherForm_GivesTheSameBall()
        {
            GeoTransform3 motion = GeoTransform3.RotationAxis(V(1, 1, 1), 0.9).Multiply(GeoTransform3.Scaling(2.0));
            var ball = new GeoSphere3(P(1, 2, 3), 5);

            Assert.Equal(motion.Transform(ball), ball.TransformBy(motion));
        }

        #endregion

        #region Comparing and printing

        [Fact]
        public void TwoBallsAreEqualOnlyWhereCentreAndRadiusAreTheSameToTheLastBit()
        {
            var ball = new GeoSphere3(P(1, 2, 3), 4);

            Assert.Equal(ball, new GeoSphere3(P(1, 2, 3), 4));
            Assert.Equal(ball.GetHashCode(), new GeoSphere3(P(1, 2, 3), 4).GetHashCode());
            Assert.NotEqual(ball, new GeoSphere3(P(1, 2, 3), 4.0000001));
            Assert.NotEqual(ball, new GeoSphere3(P(1, 2, 3.0000001), 4));
            Assert.False(ball.Equals((object)"ball"));
        }

        [Fact]
        public void TwoBallsAreTheSameWithinAThousandth_AndNotWithinTwo()
        {
            var ball = new GeoSphere3(P(1, 2, 3), 4);

            Assert.True(ball.IsEqualTo(new GeoSphere3(P(1.0005, 2, 3), 4.0005), Strict));
            Assert.False(ball.IsEqualTo(new GeoSphere3(P(1.002, 2, 3), 4), Strict));
            Assert.False(ball.IsEqualTo(new GeoSphere3(P(1, 2, 3), 4.002), Strict));
            Assert.True(ball.IsEqualTo(ball));
        }

        [Fact]
        public void ABallPrintsItsCentreAndRadius()
        {
            Assert.Equal("Sphere3(Center: (1, 2, 3), Radius: 4)", new GeoSphere3(P(1, 2, 3), 4).ToString());
        }

        #endregion
    }
}
