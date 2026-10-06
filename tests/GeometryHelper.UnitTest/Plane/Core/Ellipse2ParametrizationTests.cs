using System;
using GeometryHelper;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// Walking round an ellipse. The angle is the eccentric angle t, P(t) = C + a cos t U + b sin t V, counted
    /// counter-clockwise from the end of the major axis; the parameter is t over a whole turn; the distance is the true
    /// length along the rim from t = 0. Lengths are held against adaptive Gauss-Kronrod quadrature of the speed
    /// (<see cref="Ellipse2Oracle.ArcLength"/>) and against mpmath at 40 digits. Past either end the members do what
    /// GeoCircle2's do (Parametrization2): a point wraps round, while a distance and a parameter carry on, a whole
    /// turn's length per whole turn.
    /// </summary>
    public class Ellipse2ParametrizationTests
    {
        private static readonly Tolerance Tight = new Tolerance(1E-9, 1E-9);

        [Fact]
        public void TheAngleIsEccentricNotTheDirectionSeenFromTheCentre()
        {
            // The spec's own example: on 300 by 100, t = 45 degrees is (212.1, 70.7), which lies 18.4 degrees from the
            // axis seen from the centre.
            var ellipse = new GeoEllipse2(new GeoPoint2(0, 0), GeoVector2.XAxis, 300, 100);
            GeoPoint2 point = ellipse.GetPointAtAngle(Math.PI / 4);

            Assert.True(point.IsEqualTo(new GeoPoint2(150 * Math.Sqrt(2), 50 * Math.Sqrt(2)), Tight));
            Assert.Equal(18.4349488, Math.Atan2(point.Y, point.X) * 180 / Math.PI, 1E-7);
        }

        [Fact]
        public void TheAngleRunsCounterClockwiseFromTheEndOfTheMajorAxis()
        {
            // The four ends of the axes, a quarter turn apart, and an angle past a turn or below nought lands where the
            // same angle within one turn does.
            GeoEllipse2 ellipse = Ellipse2Oracle.Tilted();
            GeoPoint2 c = ellipse.Center;

            Assert.True(ellipse.GetPointAtAngle(0).IsEqualTo(c + ellipse.MajorAxis * 300, Tight));
            Assert.True(ellipse.GetPointAtAngle(Math.PI / 2).IsEqualTo(c + ellipse.MinorAxis * 100, Tight));
            Assert.True(ellipse.GetPointAtAngle(Math.PI).IsEqualTo(c - ellipse.MajorAxis * 300, Tight));
            Assert.True(ellipse.GetPointAtAngle(3 * Math.PI / 2).IsEqualTo(c - ellipse.MinorAxis * 100, Tight));
            Assert.True(ellipse.GetPointAtAngle(-Math.PI / 2).IsEqualTo(c - ellipse.MinorAxis * 100, Tight));
            Assert.True(ellipse.GetPointAtAngle(2 * Math.PI + 0.3).IsEqualTo(Ellipse2Oracle.Rim(ellipse, 0.3), Tight));
        }

        [Fact]
        public void TheParameterIsTheAngleOverAWholeTurn()
        {
            GeoEllipse2 ellipse = Ellipse2Oracle.Tilted();

            foreach (double p in new[] { 0.0, 0.1, 0.37, 0.5, 0.93 })
            {
                Assert.True(ellipse.GetPointAtParameter(p).IsEqualTo(Ellipse2Oracle.Rim(ellipse, 2 * Math.PI * p), Tight));
            }
        }

        [Fact]
        public void TheParameterWrapsRoundAsTheCirclesDoes()
        {
            // GeoCircle2: 1.25 is 0.25, -0.25 is 0.75, and 1 is 0.
            GeoEllipse2 ellipse = Ellipse2Oracle.Tilted();

            Assert.True(ellipse.GetPointAtParameter(1.25).IsEqualTo(ellipse.GetPointAtParameter(0.25), Tight));
            Assert.True(ellipse.GetPointAtParameter(-0.25).IsEqualTo(ellipse.GetPointAtParameter(0.75), Tight));
            Assert.True(ellipse.GetPointAtParameter(1.0).IsEqualTo(ellipse.GetPointAtParameter(0.0), Tight));
        }

        [Fact]
        public void TheDistanceAtAParameterIsTheLengthAlongTheRimFromTheMajorAxis()
        {
            // 300 by 100: mpmath's quadrature at 40 digits gives 86.29204189114841 to a tenth of the way round and
            // 427.0011502279188 to three tenths. Elsewhere it is held against Gauss-Kronrod, to 1E-12 of the length.
            GeoEllipse2 ellipse = Ellipse2Oracle.Tilted();
            double length = ellipse.Length;

            Assert.Equal(86.29204189114841, ellipse.GetDistanceAtParameter(0.1), 1E-12 * length);
            Assert.Equal(427.0011502279188, ellipse.GetDistanceAtParameter(0.3), 1E-12 * length);

            foreach (double p in new[] { 0.01, 0.2, 0.45, 0.6, 0.83, 0.99 })
            {
                Assert.Equal(Ellipse2Oracle.ArcLength(300, 100, 0, 2 * Math.PI * p), ellipse.GetDistanceAtParameter(p), 1E-12 * length);
            }
        }

        [Fact]
        public void EachQuarterTurnAddsAQuarterOfTheLength()
        {
            // Measured from the end of the major axis, each quarter is the same length, so the ends of the axes fall at
            // a quarter, a half and three quarters of the way round, to the last few bits.
            GeoEllipse2 ellipse = Ellipse2Oracle.Tilted();
            double length = ellipse.Length;

            Assert.Equal(length / 4, ellipse.GetDistanceAtParameter(0.25), 4E-16 * length);
            Assert.Equal(length / 2, ellipse.GetDistanceAtParameter(0.5), 4E-16 * length);
            Assert.Equal(3 * length / 4, ellipse.GetDistanceAtParameter(0.75), 4E-16 * length);
            Assert.Equal(length, ellipse.GetDistanceAtParameter(1.0), 4E-16 * length);
            Assert.Equal(0.0, ellipse.GetDistanceAtParameter(0.0));
        }

        [Fact]
        public void TheLengthIsTheSameEitherSideOfBothAxes()
        {
            // The rim is mirrored in both axes, so from a tenth of the way round to the half is as long as from nought to
            // four tenths, and so on.
            GeoEllipse2 ellipse = Ellipse2Oracle.Tilted();
            double length = ellipse.Length;

            foreach (double p in new[] { 0.03, 0.1, 0.21 })
            {
                double s = ellipse.GetDistanceAtParameter(p);

                Assert.Equal(length / 2 - s, ellipse.GetDistanceAtParameter(0.5 - p), 1E-13 * length);
                Assert.Equal(length / 2 + s, ellipse.GetDistanceAtParameter(0.5 + p), 1E-13 * length);
                Assert.Equal(length - s, ellipse.GetDistanceAtParameter(1.0 - p), 1E-13 * length);
            }
        }

        [Theory]
        [InlineData(1000.0)]
        [InlineData(300.0)]
        [InlineData(100.0)]
        [InlineData(10.0)]
        [InlineData(1.0)]
        public void DistanceAndParameterGoThereAndBackFromOneToOneToAThousandToOne(double minorRadius)
        {
            // A major radius of 1 000. At a thousand to one, 1E-3 of the way round is only 0.0213 along the rim, and the
            // speed there runs from 1 to 6.4 within it.
            var ellipse = new GeoEllipse2(new GeoPoint2(250, -400), new GeoVector2(-2, 1), 1000, minorRadius);
            double length = ellipse.Length;

            foreach (double p in new[] { 0.001, 0.0137, 0.1, 0.249999, 0.25, 0.3, 0.5, 0.61, 0.75, 0.9, 0.999 })
            {
                double distance = ellipse.GetDistanceAtParameter(p);

                Assert.Equal(Ellipse2Oracle.ArcLength(1000, minorRadius, 0, 2 * Math.PI * p), distance, 1E-12 * length);
                Assert.Equal(p, ellipse.GetParameterAtDistance(distance), 1E-12);
                Assert.True(ellipse.GetPointAtDistance(distance).IsEqualTo(ellipse.GetPointAtParameter(p), new Tolerance(1E-12 * length, 1E-12)));
            }

            for (int k = 1; k < 13; k++)
            {
                double distance = k * length / 13.1;

                Assert.Equal(distance, ellipse.GetDistanceAtParameter(ellipse.GetParameterAtDistance(distance)), 1E-12 * length);
            }
        }

        [Fact]
        public void ADistanceOrAParameterPastEitherEndCarriesOnRoundAsTheCirclesDoes()
        {
            // GeoCircle2 takes a distance and a parameter as proportional, with no wrapping (Parametrization2:
            // parameter * circle.Length and distance / circle.Length): 1.3 of the way round is a whole length and three
            // tenths of one, and -0.1 is minus a tenth. A point at either wraps round to one within the turn.
            GeoEllipse2 ellipse = Ellipse2Oracle.Tilted();
            double length = ellipse.Length;
            double tenth = ellipse.GetDistanceAtParameter(0.1);
            double threeTenths = ellipse.GetDistanceAtParameter(0.3);

            Assert.Equal(length + threeTenths, ellipse.GetDistanceAtParameter(1.3), 1E-12 * length);
            Assert.Equal(-tenth, ellipse.GetDistanceAtParameter(-0.1), 1E-12 * length);
            Assert.Equal(1.3, ellipse.GetParameterAtDistance(length + threeTenths), 1E-12);
            Assert.Equal(-0.1, ellipse.GetParameterAtDistance(-tenth), 1E-12);

            Assert.True(ellipse.GetPointAtDistance(length + threeTenths).IsEqualTo(ellipse.GetPointAtParameter(0.3), Tight));
            Assert.True(ellipse.GetPointAtDistance(-tenth).IsEqualTo(ellipse.GetPointAtParameter(0.9), Tight));
        }

        [Fact]
        public void TheParameterAndDistanceAtAPointAreThoseOfItsClosestRimPoint()
        {
            // 20 outside the rim at t = 2.2 the closest rim point is the one at 2.2, so the parameter is 2.2 over a turn,
            // 0.35014, and the distance the length from nought to 2.2 by quadrature.
            GeoEllipse2 ellipse = Ellipse2Oracle.Tilted();
            GeoPoint2 point = Ellipse2Oracle.OffRim(ellipse, 2.2, 20);

            Assert.Equal(2.2 / (2 * Math.PI), ellipse.GetParameterAtPoint(point), 1E-12);
            Assert.Equal(Ellipse2Oracle.ArcLength(300, 100, 0, 2.2), ellipse.GetDistanceAtPoint(point), 1E-12 * ellipse.Length);
        }

        [Fact]
        public void JustBelowTheEndOfTheMajorAxisTheParameterIsJustUnderOne()
        {
            // A hundredth of a radian below the end of the major axis is 1 - 0.01/(2 pi) = 0.998408 of the way round, not
            // -0.0016; the end itself is nought, never one. The ellipse is upright, so the end's v is exactly nought.
            GeoEllipse2 ellipse = Ellipse2Oracle.Upright(300, 100);

            Assert.Equal(1 - 0.01 / (2 * Math.PI), ellipse.GetParameterAtPoint(Ellipse2Oracle.Rim(ellipse, -0.01)), 1E-12);
            Assert.Equal(0.0, ellipse.GetParameterAtPoint(Ellipse2Oracle.FromFrame(ellipse, 300, 0)));
            Assert.Equal(0.0, ellipse.GetDistanceAtPoint(Ellipse2Oracle.FromFrame(ellipse, 300, 0)));
        }

        [Fact]
        public void ACircleIsParametrizedAsGeoCircle2IsWithTheZeroFollowingTheAxis()
        {
            // Radius 250 with its axis turned 0.7 from X: the same rim and the same lengths as GeoCircle2, to 1E-12 of
            // its size, with t counted from the axis rather than from X.
            var circle = new GeoCircle2(new GeoPoint2(12, -7), 250);
            const double turn = 0.7;
            var ellipse = new GeoEllipse2(circle.Center, new GeoVector2(Math.Cos(turn), Math.Sin(turn)), 250, 250);
            var near = new Tolerance(250 * 1E-12, 1E-12);
            double shift = turn / (2 * Math.PI);

            Assert.Equal(circle.Length, ellipse.Length, 1E-12 * circle.Length);

            foreach (double p in new[] { 0.0, 0.05, 0.25, 0.4, 0.5, 0.77, 0.95 })
            {
                Assert.True(ellipse.GetPointAtParameter(p).IsEqualTo(circle.GetPointAtParameter(p + shift), near));
                Assert.Equal(circle.GetDistanceAtParameter(p), ellipse.GetDistanceAtParameter(p), 1E-12 * circle.Length);
                Assert.Equal(circle.GetParameterAtDistance(p * circle.Length), ellipse.GetParameterAtDistance(p * circle.Length), 1E-12);
            }

            // A point off the rim, a third of a turn round from the axis, 40 outside it.
            double angle = turn + 2 * Math.PI / 3;
            var point = new GeoPoint2(12 + 290 * Math.Cos(angle), -7 + 290 * Math.Sin(angle));

            Assert.Equal(circle.GetParameterAtPoint(point) - shift, ellipse.GetParameterAtPoint(point), 1E-12);
            Assert.Equal(circle.Length / 3, ellipse.GetDistanceAtPoint(point), 1E-12 * circle.Length);
        }
    }
}
