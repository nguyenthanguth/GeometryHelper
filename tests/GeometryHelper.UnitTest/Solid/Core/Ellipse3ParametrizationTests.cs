using System;
using GeometryHelper;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// Walking the rim of an ellipse in space: by the eccentric angle, by the parameter (uniform in that angle) and by
    /// true length. Inside one turn each answer is the 2D ellipse's laid out in the ellipse's own frame, the scalar ones
    /// to the bit. Past the ends they wrap as <see cref="GeoCircle3"/> wraps (Parametrization3.cs:435-453), which is not
    /// how the 2D ellipse runs on past them (2D amendment A3): a parameter of 1.3 is 0.3 here, not a turn and 0.3.
    /// </summary>
    public class Ellipse3ParametrizationTests
    {
        private static readonly Tolerance Tight = new Tolerance(1E-9, 1E-12);

        private const double Turn = 2 * Math.PI;

        [Fact]
        public void ThePointAtAnAngleIsTheCentrePlusTheTwoSemiAxesTurnedByIt()
        {
            // P(t) = C + a cos t U + b sin t V, every t, negative and past a turn included.
            Ellipse3Oracle.Specimen specimen = Ellipse3Oracle.Tilted();
            GeoEllipse3 ellipse = specimen.Build();

            for (int k = -10; k <= 30; k++)
            {
                double t = k * 0.41;
                Assert.True(ellipse.GetPointAtAngle(t).IsEqualTo(specimen.Rim(t), Tight), $"t = {t}");
            }

            // The measured example: on 300 by 100 along X, t = 45° is (212.1, 70.7) from the centre, 18.4° off the axis
            // seen from the centre.
            GeoEllipse3 level = Ellipse3Oracle.Upright(300, 100).Build();
            Assert.True(level.GetPointAtAngle(Math.PI / 4).IsEqualTo(new GeoPoint3(40 + 212.13203435596427, -25 + 70.71067811865476, 60), Tight));
        }

        [Fact]
        public void TheAngleAtAPointIsThatOfTheNearestRimPointFromNoughtUpToATurn()
        {
            // A rim point, or one straight above or below it, gives its own t, wrapped into [0, 2π).
            Ellipse3Oracle.Specimen specimen = Ellipse3Oracle.Tilted();
            GeoEllipse3 ellipse = specimen.Build();

            // Started 0.05 off the end of the major axis, so that no t lands where nought and a whole turn are one rounding
            // apart.
            for (int k = -6; k <= 20; k++)
            {
                double t = 0.05 + k * 0.43;
                double wrapped = t - Turn * Math.Floor(t / Turn);

                foreach (double h in new[] { 0.0, 35.0, -80.0 })
                {
                    double angle = ellipse.GetAngleAtPoint(specimen.Rim(t).Add(specimen.N.Multiply(h)));
                    Assert.Equal(wrapped, angle, 1E-9);
                    Assert.True(angle >= 0.0 && angle < Turn, $"{angle} is not in [0, 2π)");
                }
            }

            // Off the rim, in the plane or not, it is the 2D ellipse's angle at the foot.
            GeoEllipse2 flat = specimen.Flat();
            foreach (GeoPoint3 point in new[] { specimen.At(150, 40, 0), specimen.At(-420, 260, 55), specimen.At(250, -10, -30), specimen.At(5, -90, 300) })
            {
                Assert.Equal(flat.GetAngleAtPoint(specimen.Foot(point)), ellipse.GetAngleAtPoint(point), 1E-12);
            }
        }

        [Fact]
        public void TheCentreAndEveryPointAboveItGiveAQuarterTurn()
        {
            // 2D decision 9 and A1: the centre's nearest rim point is the end of the minor axis on its positive side, t =
            // π/2, a circle's too. GeoCircle3 gives parameter 0 there instead (Parametrization3.cs:487-490).
            foreach (Ellipse3Oracle.Specimen specimen in new[] { Ellipse3Oracle.Tilted(), Ellipse3Oracle.Tilted(200, 200) })
            {
                GeoEllipse3 ellipse = specimen.Build();

                foreach (double h in new[] { 0.0, 70.0, -70.0 })
                {
                    GeoPoint3 point = specimen.At(0, 0, h);
                    Assert.Equal(Math.PI / 2, ellipse.GetAngleAtPoint(point), 1E-12);
                    Assert.Equal(0.25, ellipse.GetParameterAtPoint(point, Tight), 1E-12);
                    Assert.Equal(0.25 * ellipse.Length, ellipse.GetDistanceAtPoint(point, Tight), 1E-9);
                }
            }
        }

        [Fact]
        public void APointOnTheMajorAxisInsideTakesTheRimPointOnThePositiveSide()
        {
            // (100, 0) is nearer the centre than the centre of curvature of the end of the axis, 266.7 out, so two rim
            // points mirrored across the axis are nearest; the one with v > 0 is given (2D decision 9), from above too.
            Ellipse3Oracle.Specimen specimen = Ellipse3Oracle.Tilted();
            GeoEllipse3 ellipse = specimen.Build();
            double angle = ellipse.GetAngleAtPoint(specimen.At(100, 0, 30));

            Assert.InRange(angle, 0.1, Math.PI - 0.1);
            Assert.Equal(specimen.Flat().GetAngleAtPoint(new GeoPoint2(100, 0)), angle, 1E-12);
        }

        [Fact]
        public void TheParameterIsUniformInTheAngleAndWrapsRoundTheRim()
        {
            // Parameter p is t = 2πp; 0 is the end of the major axis and 0.25 the end of the minor. As the circle's
            // (Parametrization3.cs:435-438), it wraps: 1.25 is 0.25, -0.25 is 0.75 and 3 is 0.
            Ellipse3Oracle.Specimen specimen = Ellipse3Oracle.Tilted();
            GeoEllipse3 ellipse = specimen.Build();

            for (int k = 0; k < 20; k++)
            {
                double p = k / 20.0;
                Assert.True(ellipse.GetPointAtParameter(p).IsEqualTo(specimen.Rim(Turn * p), Tight), $"p = {p}");
            }

            Assert.True(ellipse.GetPointAtParameter(0).IsEqualTo(specimen.At(300, 0, 0), Tight));
            Assert.True(ellipse.GetPointAtParameter(0.25).IsEqualTo(specimen.At(0, 100, 0), Tight));
            Assert.True(ellipse.GetPointAtParameter(1.25).IsEqualTo(ellipse.GetPointAtParameter(0.25), Tight));
            Assert.True(ellipse.GetPointAtParameter(-0.25).IsEqualTo(ellipse.GetPointAtParameter(0.75), Tight));
            Assert.True(ellipse.GetPointAtParameter(3.0).IsEqualTo(ellipse.GetPointAtParameter(0.0), Tight));
        }

        [Fact]
        public void TheLengthToAParameterWithinATurnIsTheEllipseLaidOutsToTheBit()
        {
            // Decision 4: the same arithmetic on the same a and b. A quarter is 334.122, a quarter of 1 336.489 exactly.
            Ellipse3Oracle.Specimen specimen = Ellipse3Oracle.Tilted();
            GeoEllipse3 ellipse = specimen.Build();
            GeoEllipse2 flat = specimen.Flat();

            foreach (double p in new[] { 0.0, 0.1, 0.25, 0.3, 0.5, 0.77, 0.999 })
            {
                Assert.Equal(flat.GetDistanceAtParameter(p), ellipse.GetDistanceAtParameter(p));
            }

            Assert.Equal(ellipse.Length / 4, ellipse.GetDistanceAtParameter(0.25), 1E-12);
        }

        [Fact]
        public void TheLengthToAParameterPastTheEndsWrapsAsTheCirclesDoes()
        {
            // The circle in space measures Wrap(p) times its length (Parametrization3.cs:443): 1.3 goes as far as 0.3, and
            // -0.1 as far as 0.9, which is the length less the run to 0.1. The 2D ellipse would carry on to L + s(0.3) and
            // -s(0.1) (2D A3); in space it wraps.
            Ellipse3Oracle.Specimen specimen = Ellipse3Oracle.Tilted();
            GeoEllipse3 ellipse = specimen.Build();
            GeoEllipse2 flat = specimen.Flat();

            Assert.Equal(flat.GetDistanceAtParameter(0.3), ellipse.GetDistanceAtParameter(1.3), 1E-9);
            Assert.Equal(flat.Length - flat.GetDistanceAtParameter(0.1), ellipse.GetDistanceAtParameter(-0.1), 1E-9);
            Assert.Equal(0.0, ellipse.GetDistanceAtParameter(2.0), 1E-9);

            // The same numbers a circle in space gives, read off its own: Wrap(1.3) L and Wrap(-0.1) L.
            var circle = new GeoCircle3(specimen.Center, specimen.N, 200);
            Assert.Equal(0.3 * circle.Length, circle.GetDistanceAtParameter(1.3), 1E-9);
            Assert.Equal(0.9 * circle.Length, circle.GetDistanceAtParameter(-0.1), 1E-9);
        }

        [Fact]
        public void TheParameterAtALengthWithinATurnIsTheEllipseLaidOutsToTheBit()
        {
            Ellipse3Oracle.Specimen specimen = Ellipse3Oracle.Tilted();
            GeoEllipse3 ellipse = specimen.Build();
            GeoEllipse2 flat = specimen.Flat();

            foreach (double d in new[] { 0.0, 1.0, 100.0, 334.0, 600.0, 1000.0, 1336.0 })
            {
                Assert.Equal(flat.GetParameterAtDistance(d), ellipse.GetParameterAtDistance(d));
            }
        }

        [Fact]
        public void TheParameterAtALengthPastTheEndsWrapsAsTheCirclesDoes()
        {
            // As Parametrization3.cs:448-453: a length and 0.3 of the way round more is 0.3, a length back to 0.1 short of
            // the start is 0.9, and two whole lengths are nought.
            Ellipse3Oracle.Specimen specimen = Ellipse3Oracle.Tilted();
            GeoEllipse3 ellipse = specimen.Build();
            GeoEllipse2 flat = specimen.Flat();
            double length = flat.Length;

            Assert.Equal(0.3, ellipse.GetParameterAtDistance(length + flat.GetDistanceAtParameter(0.3)), 1E-12);
            Assert.Equal(0.9, ellipse.GetParameterAtDistance(-flat.GetDistanceAtParameter(0.1)), 1E-12);
            Assert.Equal(0.0, ellipse.GetParameterAtDistance(2 * length), 1E-12);
        }

        [Fact]
        public void ThePointAtALengthIsTheEllipseLaidOutsAndWrapsRoundTheRim()
        {
            Ellipse3Oracle.Specimen specimen = Ellipse3Oracle.Tilted();
            GeoEllipse3 ellipse = specimen.Build();
            GeoEllipse2 flat = specimen.Flat();

            foreach (double d in new[] { 0.0, 120.0, 334.0, 777.0, 1300.0 })
            {
                Assert.True(ellipse.GetPointAtDistance(d).IsEqualTo(specimen.Lift(flat.GetPointAtDistance(d)), Tight), $"d = {d}");
                Assert.True(ellipse.GetPointAtDistance(d + flat.Length).IsEqualTo(specimen.Lift(flat.GetPointAtDistance(d)), Tight), $"d = {d} + L");
            }
        }

        [Fact]
        public void TheParameterAndTheLengthAtAPointAreThoseOfItsFootInThePlane()
        {
            // Points inside, outside, above and below: the 2D ellipse's answers at the foot, with a tolerance and without.
            Ellipse3Oracle.Specimen specimen = Ellipse3Oracle.Tilted();
            GeoEllipse3 ellipse = specimen.Build();
            GeoEllipse2 flat = specimen.Flat();

            foreach (GeoPoint3 point in new[] { specimen.At(150, 40, 0), specimen.At(-420, 260, 55), specimen.At(250, -10, -30), specimen.Rim(4.0) })
            {
                GeoPoint2 foot = specimen.Foot(point);

                Assert.Equal(flat.GetParameterAtPoint(foot), ellipse.GetParameterAtPoint(point), 1E-12);
                Assert.Equal(flat.GetParameterAtPoint(foot), ellipse.GetParameterAtPoint(point, Tight), 1E-12);
                Assert.Equal(flat.GetDistanceAtPoint(foot), ellipse.GetDistanceAtPoint(point), 1E-9);
                Assert.Equal(flat.GetDistanceAtPoint(foot), ellipse.GetDistanceAtPoint(point, Tight), 1E-9);
            }
        }

        [Fact]
        public void ALengthAndItsParameterGoBackAndForth()
        {
            // On a thin one, 1 000 by 1, where length and parameter are furthest from proportional.
            GeoEllipse3 ellipse = Ellipse3Oracle.Tilted(1000, 1).Build();

            for (int k = 1; k < 40; k++)
            {
                double p = k / 40.0;
                Assert.Equal(p, ellipse.GetParameterAtDistance(ellipse.GetDistanceAtParameter(p)), 1E-12);
            }
        }
    }
}
