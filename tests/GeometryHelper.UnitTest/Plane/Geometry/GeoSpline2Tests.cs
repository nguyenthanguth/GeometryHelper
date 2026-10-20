using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// The spline in the plane by hand: what it is made of and refuses, what the curves a draughtsman knows come out as, and
    /// each member on numbers that can be checked on paper.
    /// </summary>
    public class GeoSpline2Tests
    {
        private static readonly Tolerance Tol = Tolerance.Default;

        private static readonly GeoPoint2[] Arch =
        {
            new GeoPoint2(0, 0), new GeoPoint2(100, 150), new GeoPoint2(200, 150), new GeoPoint2(300, 0)
        };

        private static readonly double[] BezierKnots = { 0, 0, 0, 0, 1, 1, 1, 1 };

        private static GeoSpline2 Cubic() => new GeoSpline2(3, Arch, BezierKnots);

        private static GeoSpline2 Circle(double radius = 100.0) => GeoSpline2.FromCircle(new GeoCircle2(new GeoPoint2(0, 0), radius));

        private static SplineOracle OracleOf(GeoSpline2 spline) => SplineUnderTest.Wrap(spline).Oracle();

        private static void AssertPoint(double x, double y, GeoPoint2 actual, double within)
        {
            Assert.True(Math.Abs(actual.X - x) <= within && Math.Abs(actual.Y - y) <= within, "expected (" + x + ", " + y + "), got " + actual);
        }

        #region Making one

        [Fact]
        public void ACubicOfFourPoints_IsTheBezierCurve_RisingTo112AndAHalfInTheMiddle()
        {
            GeoSpline2 spline = Cubic();

            Assert.Equal(3, spline.Degree);
            Assert.Equal(4, spline.ControlPointCount);
            Assert.False(spline.IsRational);
            Assert.Equal(0.0, spline.DomainStart);
            Assert.Equal(1.0, spline.DomainEnd);
            AssertPoint(150.0, 112.5, spline.GetPointAtKnot(0.5), 1E-12);
            AssertPoint(0.0, 0.0, spline.GetPointAtKnot(0.0), 0.0);
            AssertPoint(300.0, 0.0, spline.GetPointAtKnot(1.0), 0.0);
            Assert.Equal(new GeoPoint2(0, 0), spline.StartPoint);
            Assert.Equal(new GeoPoint2(300, 0), spline.EndPoint);
        }

        [Fact]
        public void TheBoxOfTheArch_IsThreeHundredByOneHundredAndTwelveAndAHalf_NotTheBoxOfItsControlPoints()
        {
            GeoRectangle2 box = Cubic().GetAabb();

            Assert.Equal(300.0, box.Width, 12);
            Assert.Equal(112.5, box.Height, 12);
            AssertPoint(150.0, 56.25, box.Center, 1E-12);
        }

        [Fact]
        public void TheLengthOfTheArch_IsWhatTheOracleIntegrates()
        {
            var oracle = new SplineOracle(2, 3, BezierKnots, new double[] { 0, 0, 100, 150, 200, 150, 300, 0 }, null);

            Assert.Equal(oracle.Length(), Cubic().Length, 10);
            Assert.Equal(389.89266738851, Cubic().Length, 8);
        }

        [Fact]
        public void TheKnotsAreKeptAsGiven_NotRescaledOrReordered()
        {
            var knots = new double[] { 5, 5, 5, 5, 9, 12, 12, 12, 12 };
            var spline = new GeoSpline2(3, Arch.Concat(new[] { new GeoPoint2(400, 80) }), knots);

            Assert.Equal(knots, spline.Knots.ToArray());
            Assert.Equal(5.0, spline.DomainStart);
            Assert.Equal(12.0, spline.DomainEnd);
            AssertPoint(400.0, 80.0, spline.GetPointAtKnot(12.0), 0.0);
            AssertPoint(400.0, 80.0, spline.GetPointAtKnot(1000.0), 0.0);
            AssertPoint(0.0, 0.0, spline.GetPointAtKnot(-3.0), 0.0);
        }

        [Fact]
        public void TheNumbersAreCopied_AndWhatTheSplineHandsOutCannotBeWrittenTo()
        {
            GeoPoint2[] points = Arch.ToArray();
            double[] knots = BezierKnots.ToArray();
            var spline = new GeoSpline2(3, points, knots);

            points[1] = new GeoPoint2(-1000, -1000);
            knots[4] = 100.0;

            Assert.Equal(new GeoPoint2(100, 150), spline.ControlPoints[1]);
            Assert.Equal(1.0, spline.Knots[4]);
            Assert.False(spline.ControlPoints is GeoPoint2[]);
            Assert.False(spline.Knots is double[]);
            Assert.False(spline.Weights is double[]);
        }

        [Fact]
        public void FromControlPoints_SpacesTheKnotsEvenlyOverZeroToOne()
        {
            var points = Enumerable.Range(0, 7).Select(i => new GeoPoint2(i * 10, i % 2 * 5));
            GeoSpline2 spline = GeoSpline2.FromControlPoints(3, points);

            Assert.Equal(new[] { 0, 0, 0, 0, 0.25, 0.5, 0.75, 1, 1, 1, 1 }, spline.Knots.ToArray());
            Assert.False(spline.IsRational);

            // With degree plus one points it is the Bezier curve.
            Assert.Equal(Cubic(), GeoSpline2.FromControlPoints(3, Arch));
        }

        [Fact]
        public void WeightsOfAllOne_AreAPlainSpline_AndAnyOtherIsRational()
        {
            Assert.False(new GeoSpline2(3, Arch, BezierKnots, new double[] { 1, 1, 1, 1 }).IsRational);
            Assert.True(new GeoSpline2(3, Arch, BezierKnots, new double[] { 1, 1, 1.0000001, 1 }).IsRational);
            Assert.Equal(new double[] { 1, 1, 1, 1 }, Cubic().Weights.ToArray());
        }

        [Fact]
        public void APolylineAsASpline_IsThePolyline_WithEachVertexAtItsShareOfTheLength()
        {
            var polyline = new GeoPolyline2(new[] { new GeoPoint2(0, 0), new GeoPoint2(100, 0), new GeoPoint2(100, 50) });
            GeoSpline2 spline = GeoSpline2.FromPolyline(polyline);

            Assert.Equal(1, spline.Degree);
            Assert.Equal(new[] { 0, 0, 2.0 / 3.0, 1, 1 }, spline.Knots.ToArray());
            Assert.Equal(150.0, spline.Length, 11);
            AssertPoint(100.0, 0.0, spline.GetPointAtKnot(2.0 / 3.0), 1E-12);
            AssertPoint(100.0, 25.0, spline.GetPointAtKnot(5.0 / 6.0), 1E-12);
            Assert.Equal(polyline.Vertices.ToArray(), spline.ControlPoints.ToArray());
        }

        #endregion

        #region What it refuses

        [Fact]
        public void ADegreeOutsideOneToTen_IsRefused()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new GeoSpline2(0, Arch, BezierKnots));
            Assert.Throws<ArgumentOutOfRangeException>(() => new GeoSpline2(11, Arch, BezierKnots));
            Assert.Throws<ArgumentOutOfRangeException>(() => GeoSpline2.FromControlPoints(0, Arch));
            Assert.Throws<ArgumentOutOfRangeException>(() => GeoSpline2.FromControlPoints(11, Arch));
        }

        [Fact]
        public void TooFewControlPoints_AreRefused_AndNullsAreNamed()
        {
            Assert.Throws<ArgumentException>(() => new GeoSpline2(4, Arch, new double[] { 0, 0, 0, 0, 0, 1, 1, 1, 1, 1 }));
            Assert.Throws<ArgumentException>(() => GeoSpline2.FromControlPoints(4, Arch));
            Assert.Equal("controlPoints", Assert.Throws<ArgumentNullException>(() => new GeoSpline2(3, null, BezierKnots)).ParamName);
            Assert.Equal("knots", Assert.Throws<ArgumentNullException>(() => new GeoSpline2(3, Arch, null)).ParamName);
            Assert.Equal("controlPoints", Assert.Throws<ArgumentNullException>(() => GeoSpline2.FromControlPoints(3, null)).ParamName);
        }

        [Theory]
        [InlineData("count")]
        [InlineData("decreasing")]
        [InlineData("notClampedAtTheStart")]
        [InlineData("notClampedAtTheEnd")]
        [InlineData("tooManyAtTheEnd")]
        [InlineData("empty")]
        [InlineData("innerKnotRepeatedMoreThanTheDegree")]
        [InlineData("notANumber")]
        [InlineData("infinite")]
        public void KnotsThatAreNotAClampedSplines_AreRefused(string fault)
        {
            double[] knots;
            IEnumerable<GeoPoint2> points = Arch.Concat(new[] { new GeoPoint2(400, 80), new GeoPoint2(500, 0) });

            switch (fault)
            {
                case "count": knots = new double[] { 0, 0, 0, 0, 1, 1, 1, 1 }; break;
                case "decreasing": knots = new double[] { 0, 0, 0, 0, 0.7, 0.5, 1, 1, 1, 1 }; break;
                case "notClampedAtTheStart": knots = new double[] { 0, 0, 0, 0.1, 0.4, 0.7, 1, 1, 1, 1 }; break;
                case "notClampedAtTheEnd": knots = new double[] { 0, 0, 0, 0, 0.3, 0.7, 0.9, 1, 1, 1 }; break;
                case "tooManyAtTheEnd": knots = new double[] { 0, 0, 0, 0, 0, 1, 1, 1, 1, 1 }; break;
                case "empty": knots = new double[] { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1 }; break;
                case "innerKnotRepeatedMoreThanTheDegree": knots = new double[] { 0, 0, 0, 0, 0.5, 0.5, 0.5, 0.5, 1, 1, 1, 1, 1 }; break;
                case "notANumber": knots = new[] { 0, 0, 0, 0, double.NaN, 0.5, 1, 1, 1, 1 }; break;
                default: knots = new[] { 0, 0, 0, 0, double.PositiveInfinity, double.PositiveInfinity, 1, 1, 1, 1 }; break;
            }

            if (fault == "innerKnotRepeatedMoreThanTheDegree")
            {
                points = points.Concat(new[] { new GeoPoint2(600, 50), new GeoPoint2(700, 20), new GeoPoint2(800, 30) });
            }

            Assert.Throws<ArgumentException>(() => new GeoSpline2(3, points, knots));
        }

        [Fact]
        public void AnInnerKnotRepeatedTheDegree_IsAllowed_AndBreaksTheCurveToACorner()
        {
            // Three times, with degree three: the curve passes through a control point and turns a corner there.
            var points = new[] { new GeoPoint2(0, 0), new GeoPoint2(50, 100), new GeoPoint2(100, 0), new GeoPoint2(150, -100), new GeoPoint2(200, 0), new GeoPoint2(250, 100), new GeoPoint2(300, 0) };
            var spline = new GeoSpline2(3, points, new double[] { 0, 0, 0, 0, 0.5, 0.5, 0.5, 1, 1, 1, 1 });

            AssertPoint(150.0, -100.0, spline.GetPointAtKnot(0.5), 1E-12);
            Assert.False(spline.GetTangentAtKnot(0.4999999).IsEqualTo(spline.GetTangentAtKnot(0.5), Tol));
        }

        [Fact]
        public void WeightsThatAreNotPositiveNumbers_AreRefused_AsAreTheWrongNumberOfThem()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new GeoSpline2(3, Arch, BezierKnots, new double[] { 1, 0, 1, 1 }));
            Assert.Throws<ArgumentOutOfRangeException>(() => new GeoSpline2(3, Arch, BezierKnots, new double[] { 1, -2, 1, 1 }));
            Assert.Throws<ArgumentOutOfRangeException>(() => new GeoSpline2(3, Arch, BezierKnots, new[] { 1, double.NaN, 1, 1 }));
            Assert.Throws<ArgumentOutOfRangeException>(() => new GeoSpline2(3, Arch, BezierKnots, new[] { 1, double.PositiveInfinity, 1, 1 }));
            Assert.Throws<ArgumentException>(() => new GeoSpline2(3, Arch, BezierKnots, new double[] { 1, 1, 1 }));
        }

        [Fact]
        public void ControlPointsThatAreAllOnePoint_OrNotNumbers_AreRefused()
        {
            var one = new GeoPoint2(7, 7);

            Assert.Throws<ArgumentException>(() => new GeoSpline2(3, new[] { one, one, one, one }, BezierKnots));
            Assert.Throws<ArgumentException>(() => new GeoSpline2(3, new[] { new GeoPoint2(0, 0), new GeoPoint2(double.NaN, 1), new GeoPoint2(2, 2), new GeoPoint2(3, 3) }, BezierKnots));
            Assert.Throws<ArgumentException>(() => new GeoSpline2(3, new[] { new GeoPoint2(0, 0), new GeoPoint2(1, double.PositiveInfinity), new GeoPoint2(2, 2), new GeoPoint2(3, 3) }, BezierKnots));

            // Not all one point is enough: a curve that returns to where it started is a curve.
            var loop = new GeoSpline2(3, new[] { new GeoPoint2(0, 0), new GeoPoint2(100, 100), new GeoPoint2(-100, 100), new GeoPoint2(0, 0) }, BezierKnots);
            Assert.True(loop.IsClosed());
        }

        [Fact]
        public void AParameterOrALengthThatIsNotANumber_IsRefused()
        {
            GeoSpline2 spline = Cubic();

            Assert.Throws<ArgumentOutOfRangeException>(() => spline.GetPointAtKnot(double.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => spline.GetPointAtKnot(double.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => spline.GetDerivativeAtKnot(0.5, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => spline.GetDerivativeAtKnot(0.5, 11));
            Assert.Throws<ArgumentOutOfRangeException>(() => spline.GetTangentAtKnot(double.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => spline.GetCurvatureAtKnot(double.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => spline.GetParameterAtDistance(double.NaN));
        }

        #endregion

        #region Circles and arcs, exactly

        [Fact]
        public void ACircleOfRadiusOneHundred_IsNineControlPointsAndEveryPointOfItIsAtTheRadius()
        {
            GeoSpline2 circle = Circle();

            Assert.Equal(2, circle.Degree);
            Assert.Equal(9, circle.ControlPointCount);
            Assert.True(circle.IsRational);
            Assert.Equal(new[] { 0, 0, 0, 0.25, 0.25, 0.5, 0.5, 0.75, 0.75, 1, 1, 1 }, circle.Knots.ToArray());
            Assert.Equal(new[] { 1, Math.Sqrt(0.5), 1, Math.Sqrt(0.5), 1, Math.Sqrt(0.5), 1, Math.Sqrt(0.5), 1 }, circle.Weights.ToArray());
            AssertPoint(100, 100, circle.ControlPoints[1], 1E-12);
            AssertPoint(-100, 0, circle.ControlPoints[4], 1E-12);

            double worst = 0.0;

            for (int i = 0; i <= 2000; i++)
            {
                GeoPoint2 p = circle.GetPointAtKnot(i / 2000.0);
                worst = Math.Max(worst, Math.Abs(Math.Sqrt(p.X * p.X + p.Y * p.Y) - 100.0));
            }

            Assert.True(worst <= 1E-13, "the worst point is " + worst + " off the radius");
        }

        [Fact]
        public void ACircleOfRadiusOne_HasEveryPointWithinAHundredthOfATrillionthOfTheRadius()
        {
            GeoSpline2 circle = Circle(1.0);
            double worst = 0.0;

            for (int i = 0; i <= 4000; i++)
            {
                GeoPoint2 p = circle.GetPointAtKnot(i / 4000.0);
                worst = Math.Max(worst, Math.Abs(Math.Sqrt(p.X * p.X + p.Y * p.Y) - 1.0));
            }

            Assert.True(worst <= 1E-14, "the worst point is " + worst + " off the radius");
        }

        [Fact]
        public void TheLengthOfACircle_IsTwoPiRToATrillionth()
        {
            foreach (double radius in new[] { 1.0, 100.0, 12345.678 })
            {
                double length = Circle(radius).Length;

                Assert.True(Math.Abs(length - 2.0 * Math.PI * radius) <= 1E-12 * 2.0 * Math.PI * radius, "radius " + radius + ": " + length.ToString("R"));
            }
        }

        [Fact]
        public void TheCurvatureOfACircle_IsOneOverTheRadiusAllRound_PositiveCounterClockwise()
        {
            GeoSpline2 circle = Circle();

            foreach (double t in new[] { 0.0, 0.1, 0.25, 0.3, 0.5, 0.77, 1.0 })
            {
                Assert.Equal(0.01, circle.GetCurvatureAtKnot(t), 12);
            }

            Assert.Equal(-0.01, circle.Reverse().GetCurvatureAtKnot(0.4), 12);
            Assert.Equal(-0.01, circle.TransformBy(GeoTransform2.Mirror(new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(1, 0)))).GetCurvatureAtKnot(0.4), 12);
        }

        [Fact]
        public void TheTangentOfACircle_IsSquareToItsRadius_AndAQuarterTurnIsOnePiece()
        {
            GeoSpline2 circle = Circle();

            Assert.True(circle.GetTangentAtKnot(0.0).IsEqualTo(new GeoVector2(0, 1), Tol));
            Assert.True(circle.GetTangentAtKnot(0.5).IsEqualTo(new GeoVector2(0, -1), Tol));

            var arc = new GeoArc2(new GeoPoint2(0, 0), 100, 0.0, Math.PI * 0.5);
            GeoSpline2 quarter = GeoSpline2.FromArc(arc);

            Assert.Equal(3, quarter.ControlPointCount);
            Assert.Equal(new GeoPoint2(100, 0), quarter.ControlPoints[0]);
            Assert.Equal(new GeoPoint2(100, 100).X, quarter.ControlPoints[1].X, 12);
            Assert.Equal(100.0, quarter.ControlPoints[1].Y, 12);
            Assert.Equal(0.0, quarter.ControlPoints[2].X, 12);
            Assert.Equal(Math.Sqrt(0.5), quarter.Weights[1], 15);
        }

        [Fact]
        public void AnArcOfAHundredDegrees_IsTwoPiecesOfFifty_RunningTheWayTheArcRuns()
        {
            var arc = new GeoArc2(new GeoPoint2(10, 20), 50, 0.3, 0.3 + 100.0 * Math.PI / 180.0);
            GeoSpline2 spline = GeoSpline2.FromArc(arc);

            Assert.Equal(5, spline.ControlPointCount);
            Assert.Equal(Math.Cos(25.0 * Math.PI / 180.0), spline.Weights[1], 14);
            Assert.True(spline.StartPoint.IsEqualTo(arc.StartPoint, Tol));
            Assert.True(spline.EndPoint.IsEqualTo(arc.EndPoint, Tol));
            Assert.Equal(arc.Length, spline.Length, 10);

            for (int i = 0; i <= 100; i++)
            {
                GeoPoint2 p = spline.GetPointAtKnot(i / 100.0);
                Assert.Equal(50.0, p.DistanceTo(new GeoPoint2(10, 20)), 12);
            }

            // The middle of the parameter is the middle of the arc: two pieces of equal angle.
            Assert.True(spline.GetPointAtKnot(0.5).IsEqualTo(arc.MidPoint, Tol));
        }

        [Fact]
        public void AClockwiseArc_RunsClockwise_AndAWholeCircleIsClosed()
        {
            var arc = new GeoArc2(new GeoPoint2(0, 0), 100, Math.PI * 0.5, 0.0, true);
            GeoSpline2 spline = GeoSpline2.FromArc(arc);

            Assert.True(spline.StartPoint.IsEqualTo(new GeoPoint2(0, 100), Tol));
            Assert.True(spline.EndPoint.IsEqualTo(new GeoPoint2(100, 0), Tol));
            Assert.Equal(-0.01, spline.GetCurvatureAtKnot(0.5), 12);
            Assert.True(Circle().IsClosed());
            Assert.False(spline.IsClosed());
            Assert.False(Cubic().IsClosed());
        }

        [Fact]
        public void ACircleUnderAnUnevenScaling_IsTheEllipseItIs_ExactlyBecauseTheControlPointsCarryIt()
        {
            GeoSpline2 ellipse = Circle().TransformBy(GeoTransform2.Scaling(3.0, 1.0));

            for (int i = 0; i <= 400; i++)
            {
                GeoPoint2 p = ellipse.GetPointAtKnot(i / 400.0);
                Assert.Equal(1.0, p.X * p.X / (300.0 * 300.0) + p.Y * p.Y / (100.0 * 100.0), 12);
            }

            Assert.Equal(Circle().Weights.ToArray(), ellipse.Weights.ToArray());
            Assert.Equal(Circle().Knots.ToArray(), ellipse.Knots.ToArray());

            // Ramanujan's perimeter of the ellipse 300 by 100 is 1 336.489 (the library's own AGM figure).
            Assert.Equal(new GeoEllipse2(new GeoPoint2(0, 0), GeoVector2.XAxis, 300, 100).Length, ellipse.Length, 8);
        }

        #endregion

        #region Interpolating

        [Fact]
        public void FourFitPointsOfDegreeThree_AreMetAtTheirShareOfTheChainLength()
        {
            var fit = new[] { new GeoPoint2(0, 0), new GeoPoint2(100, 50), new GeoPoint2(200, 0), new GeoPoint2(300, 50) };
            GeoSpline2 spline = GeoSpline2.Interpolate(fit, 3, Tol);

            Assert.Equal(3, spline.Degree);
            Assert.Equal(4, spline.ControlPointCount);
            Assert.Equal(new double[] { 0, 0, 0, 0, 1, 1, 1, 1 }, spline.Knots.ToArray());

            // The three chords are each the root of 12 500, so the points are met at 0, a third, two thirds and 1.
            AssertPoint(0.0, 0.0, spline.GetPointAtKnot(0.0), 1E-12);
            AssertPoint(100.0, 50.0, spline.GetPointAtKnot(1.0 / 3.0), 1E-10);
            AssertPoint(200.0, 0.0, spline.GetPointAtKnot(2.0 / 3.0), 1E-10);
            AssertPoint(300.0, 50.0, spline.GetPointAtKnot(1.0), 1E-12);
        }

        [Fact]
        public void ManyFitPointsOfEachDegree_AreMetWhereTheMethodSaysThey_AndTheKnotsAreTheAveragesOfTheParameters()
        {
            var fit = new List<GeoPoint2>();

            for (int i = 0; i < 12; i++)
            {
                fit.Add(new GeoPoint2(i * 25.0 + (i % 3) * 7.0, 80.0 * Math.Sin(i * 0.9) + i * 3.0));
            }

            double[] along = new double[fit.Count];

            for (int i = 1; i < fit.Count; i++)
            {
                along[i] = along[i - 1] + fit[i].DistanceTo(fit[i - 1]);
            }

            double[] u = along.Select(x => x / along[along.Length - 1]).ToArray();

            foreach (int degree in new[] { 1, 2, 3, 4, 5 })
            {
                GeoSpline2 spline = GeoSpline2.Interpolate(fit, degree, Tol);

                Assert.Equal(fit.Count, spline.ControlPointCount);

                for (int i = 0; i < fit.Count; i++)
                {
                    Assert.True(spline.GetPointAtKnot(u[i]).DistanceTo(fit[i]) <= 1E-9, "degree " + degree + ", point " + i + " is " + spline.GetPointAtKnot(u[i]).DistanceTo(fit[i]) + " off");
                }

                // The inner knots are the means of degree parameters in a row.
                for (int j = 1; j <= fit.Count - 1 - degree; j++)
                {
                    double mean = Enumerable.Range(j, degree).Sum(i => u[i]) / degree;
                    Assert.Equal(mean, spline.Knots[j + degree], 14);
                }
            }
        }

        [Fact]
        public void ThreeCollinearFitPointsWithTheTangentAlongTheLine_GiveTheLineAtAnEvenSpeed()
        {
            var fit = new[] { new GeoPoint2(0, 0), new GeoPoint2(100, 0), new GeoPoint2(200, 0) };

            foreach (double scale in new[] { 1.0, 0.001, 5000.0 })
            {
                GeoSpline2 spline = GeoSpline2.Interpolate(fit, new GeoVector2(scale, 0), new GeoVector2(scale, 0), 3, Tol);

                Assert.Equal(5, spline.ControlPointCount);
                Assert.True(spline.GetDerivativeAtKnot(0.0, 1).IsEqualTo(new GeoVector2(200, 0), Tol));
                Assert.True(spline.GetDerivativeAtKnot(1.0, 1).IsEqualTo(new GeoVector2(200, 0), Tol));
                Assert.Equal(200.0, spline.Length, 9);

                for (int i = 0; i <= 20; i++)
                {
                    GeoPoint2 p = spline.GetPointAtKnot(i / 20.0);
                    Assert.True(Math.Abs(p.Y) <= 1E-10);
                }

                AssertPoint(100.0, 0.0, spline.GetPointAtKnot(0.5), 1E-9);
            }
        }

        [Fact]
        public void TwoFitPointsWithTangents_AreTheHermiteCubic_AnSThatCrossesItsMiddleAtTheMiddle()
        {
            var fit = new[] { new GeoPoint2(0, 0), new GeoPoint2(100, 0) };
            GeoSpline2 spline = GeoSpline2.Interpolate(fit, new GeoVector2(0, 1), new GeoVector2(0, 7), 3, Tol);

            Assert.Equal(4, spline.ControlPointCount);
            AssertPoint(50.0, 0.0, spline.GetPointAtKnot(0.5), 1E-12);
            Assert.True(spline.GetPointAtKnot(0.25).Y > 0.0);
            Assert.True(spline.GetPointAtKnot(0.75).Y < 0.0);
            Assert.True(spline.GetTangentAtKnot(0.0).IsEqualTo(new GeoVector2(0, 1), Tol));
            Assert.True(spline.GetTangentAtKnot(1.0).IsEqualTo(new GeoVector2(0, 1), Tol));

            // Hermite: the control points are the ends and the ends moved a third of a derivative of 100.
            Assert.Equal(new GeoPoint2(0, 100.0 / 3.0).Y, spline.ControlPoints[1].Y, 10);
            Assert.Equal(100.0, spline.ControlPoints[2].X, 10);
            Assert.Equal(-100.0 / 3.0, spline.ControlPoints[2].Y, 10);
        }

        [Fact]
        public void FitPointsOnACircle_GiveACurveThatStaysCloseToIt()
        {
            var fit = Enumerable.Range(0, 17).Select(i => new GeoPoint2(100 * Math.Cos(i * Math.PI / 8), 100 * Math.Sin(i * Math.PI / 8))).ToArray();
            GeoSpline2 spline = GeoSpline2.Interpolate(fit, new GeoVector2(0, 1), new GeoVector2(0, 1), 3, Tol);
            double worst = 0.0;

            for (int i = 0; i <= 1000; i++)
            {
                GeoPoint2 p = spline.GetPointAtKnot(i / 1000.0);
                worst = Math.Max(worst, Math.Abs(Math.Sqrt(p.X * p.X + p.Y * p.Y) - 100.0));
            }

            Assert.True(worst < 0.05, "the curve strays " + worst + " from the circle");
            Assert.True(spline.IsClosed());
        }

        [Fact]
        public void FitPointsThatCoincideWithTheOneBefore_AreDropped_AndTooFewOrBadOnesAreRefused()
        {
            var fit = new[] { new GeoPoint2(0, 0), new GeoPoint2(0.0004, 0), new GeoPoint2(100, 50), new GeoPoint2(200, 0), new GeoPoint2(300, 50) };
            GeoSpline2 spline = GeoSpline2.Interpolate(fit, 3, Tol);

            Assert.Equal(4, spline.ControlPointCount);
            Assert.Equal(new GeoPoint2(0, 0), spline.StartPoint);

            Assert.Throws<ArgumentException>(() => GeoSpline2.Interpolate(fit.Take(3), 3, Tol));
            Assert.Throws<ArgumentException>(() => GeoSpline2.Interpolate(new[] { new GeoPoint2(0, 0), new GeoPoint2(0, 0) }, 1, Tol));
            Assert.Throws<ArgumentException>(() => GeoSpline2.Interpolate(new[] { new GeoPoint2(0, 0), new GeoPoint2(double.NaN, 1), new GeoPoint2(5, 5), new GeoPoint2(9, 1) }, 3, Tol));
            Assert.Throws<ArgumentNullException>(() => GeoSpline2.Interpolate(null, 3, Tol));
            Assert.Throws<ArgumentOutOfRangeException>(() => GeoSpline2.Interpolate(fit, 0, Tol));
            Assert.Throws<ArgumentOutOfRangeException>(() => GeoSpline2.Interpolate(fit, 11, Tol));
            Assert.Throws<ArgumentOutOfRangeException>(() => GeoSpline2.Interpolate(fit, new GeoVector2(1, 0), new GeoVector2(1, 0), 1, Tol));
            Assert.Throws<ArgumentException>(() => GeoSpline2.Interpolate(fit, GeoVector2.Zero, new GeoVector2(1, 0), 3, Tol));
            Assert.Throws<ArgumentException>(() => GeoSpline2.Interpolate(fit, new GeoVector2(1, 0), new GeoVector2(double.NaN, 0), 3, Tol));
            Assert.Throws<ArgumentException>(() => GeoSpline2.Interpolate(fit.Take(1), new GeoVector2(1, 0), new GeoVector2(1, 0), 3, Tol));

            // A degree as high as the points allow is fine, one more is not.
            Assert.Equal(3, GeoSpline2.Interpolate(fit, 3, Tol).Degree);
            Assert.Equal(1, GeoSpline2.Interpolate(fit, 1, Tol).Degree);
            Assert.Throws<ArgumentException>(() => GeoSpline2.Interpolate(fit, 4, Tol));
        }

        [Fact]
        public void ManyThousandFitPoints_AreSolvedAsOneBandedSystem()
        {
            int count = 20000;
            var fit = Enumerable.Range(0, count).Select(i => new GeoPoint2(i * 3.0, 500.0 * Math.Sin(i * 0.01) + (i % 7))).ToArray();
            GeoSpline2 spline = GeoSpline2.Interpolate(fit, 3, Tol);
            double[] u = new double[count];

            for (int i = 1; i < count; i++)
            {
                u[i] = u[i - 1] + fit[i].DistanceTo(fit[i - 1]);
            }

            for (int i = 0; i < count; i += 997)
            {
                Assert.True(spline.GetPointAtKnot(u[i] / u[count - 1]).DistanceTo(fit[i]) <= 1E-6, "point " + i);
            }
        }

        #endregion

        #region Cutting, turning, moving

        [Fact]
        public void CuttingTheArchAtThreeTenths_GivesTwoCubicsMeetingAtNinetyAndNinetyFourAndAHalf()
        {
            GeoSpline2 spline = Cubic();
            GeoSpline2[] pieces = spline.Split(0.3);

            Assert.Equal(2, pieces.Length);
            Assert.Equal(0.0, pieces[0].DomainStart);
            Assert.Equal(0.3, pieces[0].DomainEnd);
            Assert.Equal(0.3, pieces[1].DomainStart);
            Assert.Equal(1.0, pieces[1].DomainEnd);
            AssertPoint(90.0, 94.5, pieces[0].EndPoint, 1E-12);
            Assert.Equal(pieces[0].EndPoint, pieces[1].StartPoint);
            Assert.Equal(new GeoPoint2(0, 0), pieces[0].StartPoint);
            Assert.Equal(new GeoPoint2(300, 0), pieces[1].EndPoint);

            // The control points of the first piece are de Casteljau's: 0, 30, 60 and 90 across.
            Assert.Equal(30.0, pieces[0].ControlPoints[1].X, 12);
            Assert.Equal(45.0, pieces[0].ControlPoints[1].Y, 12);
            Assert.Equal(spline.Length, pieces[0].Length + pieces[1].Length, 10);

            for (int i = 0; i <= 30; i++)
            {
                double t = i / 30.0;
                GeoPoint2 expected = spline.GetPointAtKnot(t);
                GeoPoint2 actual = t <= 0.3 ? pieces[0].GetPointAtKnot(t) : pieces[1].GetPointAtKnot(t);
                Assert.True(expected.DistanceTo(actual) <= 1E-12, "at " + t);
            }
        }

        [Fact]
        public void CuttingAtAKnotOrWhereAKnotIsAlreadyRepeated_Works_AndCuttingAtAnEndIsRefused()
        {
            var points = new[] { new GeoPoint2(0, 0), new GeoPoint2(50, 100), new GeoPoint2(100, 0), new GeoPoint2(150, -100), new GeoPoint2(200, 0), new GeoPoint2(250, 100), new GeoPoint2(300, 0) };
            var spline = new GeoSpline2(3, points, new double[] { 0, 0, 0, 0, 0.5, 0.5, 0.5, 1, 1, 1, 1 });

            GeoSpline2[] atTheCorner = spline.Split(0.5);

            Assert.Equal(4, atTheCorner[0].ControlPointCount);
            Assert.Equal(4, atTheCorner[1].ControlPointCount);
            AssertPoint(150.0, -100.0, atTheCorner[0].EndPoint, 1E-12);

            var smooth = new GeoSpline2(3, points, new double[] { 0, 0, 0, 0, 0.25, 0.5, 0.75, 1, 1, 1, 1 });
            GeoSpline2[] atAKnot = smooth.Split(0.5);

            Assert.Equal(smooth.GetPointAtKnot(0.5).X, atAKnot[0].EndPoint.X, 12);
            Assert.Equal(smooth.GetPointAtKnot(0.5).Y, atAKnot[0].EndPoint.Y, 12);

            Assert.Throws<ArgumentOutOfRangeException>(() => spline.Split(0.0));
            Assert.Throws<ArgumentOutOfRangeException>(() => spline.Split(1.0));
            Assert.Throws<ArgumentOutOfRangeException>(() => spline.Split(-0.1));
            Assert.Throws<ArgumentOutOfRangeException>(() => spline.Split(double.NaN));
        }

        [Fact]
        public void ARationalSplineCut_KeepsItsWeights_SoACircleCutIsTwoArcsOfTheCircle()
        {
            GeoSpline2[] halves = Circle().Split(0.5);

            foreach (GeoSpline2 half in halves)
            {
                Assert.True(half.IsRational);

                for (int i = 0; i <= 100; i++)
                {
                    GeoPoint2 p = half.GetPointAtKnot(half.DomainStart + (half.DomainEnd - half.DomainStart) * i / 100.0);
                    Assert.Equal(100.0, Math.Sqrt(p.X * p.X + p.Y * p.Y), 11);
                }
            }

            Assert.Equal(Math.PI * 100.0, halves[0].Length, 9);

            GeoSpline2[] thirds = Circle().Split(0.3);
            Assert.Equal(SplineUnderTest.Wrap(Circle()).Oracle().Length(0.0, 0.3), thirds[0].Length, 9);
        }

        [Fact]
        public void TheReverse_HasTheSameDomain_AndRunsFromTheEndToTheStart()
        {
            var points = Arch.Concat(new[] { new GeoPoint2(400, 80) });
            var spline = new GeoSpline2(3, points, new double[] { 5, 5, 5, 5, 9, 12, 12, 12, 12 }, new double[] { 1, 2, 3, 1, 2 });
            GeoSpline2 reversed = spline.Reverse();

            Assert.Equal(5.0, reversed.DomainStart);
            Assert.Equal(12.0, reversed.DomainEnd);
            Assert.Equal(spline.EndPoint, reversed.StartPoint);
            Assert.Equal(spline.StartPoint, reversed.EndPoint);
            Assert.Equal(new double[] { 2, 1, 3, 2, 1 }, reversed.Weights.ToArray());
            Assert.Equal(new double[] { 5, 5, 5, 5, 8, 12, 12, 12, 12 }, reversed.Knots.ToArray());
            Assert.True(reversed.GetPointAtKnot(6.0).IsEqualTo(spline.GetPointAtKnot(11.0), Tol));
            // Equal because these knots are dyadic fractions, for which a + b - k is exact; for knots such as 0.3 the knots come back to
            // within the last place, and only the control points come back to the bit (see GeoSplineParameterTests).
            Assert.Equal(spline, reversed.Reverse());
        }

        [Fact]
        public void MovingTurningAndMirroring_CarryTheControlPoints_AndLeaveTheWeightsAndKnots()
        {
            GeoSpline2 spline = Cubic();
            GeoSpline2 moved = spline.Translate(new GeoVector2(10, -20));

            Assert.Equal(new GeoPoint2(110, 130), moved.ControlPoints[1]);
            Assert.Equal(spline.Knots.ToArray(), moved.Knots.ToArray());
            AssertPoint(160.0, 92.5, moved.GetPointAtKnot(0.5), 1E-12);
            Assert.Equal(moved, spline + new GeoVector2(10, -20));
            Assert.Equal(spline, moved - new GeoVector2(10, -20));

            GeoSpline2 turned = spline.TransformBy(GeoTransform2.Rotation(Math.PI * 0.5));
            AssertPoint(-112.5, 150.0, turned.GetPointAtKnot(0.5), 1E-12);
            Assert.Equal(spline.Length, turned.Length, 10);

            GeoSpline2 mirrored = spline.TransformBy(GeoTransform2.Mirror(new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(0, 1))));
            AssertPoint(-150.0, 112.5, mirrored.GetPointAtKnot(0.5), 1E-12);
            Assert.Equal(new GeoPoint2(-300, 0), mirrored.EndPoint);

            Assert.Throws<ArgumentNullException>(() => spline.TransformBy(null));
        }

        [Fact]
        public void ATransformationThatSqueezesTheSplineToOnePoint_IsRefused_AndOneToALineIsNot()
        {
            GeoSpline2 spline = Cubic();

            Assert.Throws<InvalidOperationException>(() => spline.TransformBy(GeoTransform2.Scaling(0.0, 0.0)));

            GeoSpline2 flat = spline.TransformBy(GeoTransform2.Scaling(1.0, 0.0));
            Assert.All(flat.ControlPoints, p => Assert.Equal(0.0, p.Y));
            Assert.Equal(300.0, flat.GetAabb().Width, 10);
        }

        [Fact]
        public void LiftedIntoSpaceAndLaidBackOut_TheSplineComesBackAsItWent()
        {
            var frame = new GeoCoordinateSystem3(new GeoPoint3(10, 20, 30), new GeoVector3(1, 1, 0), new GeoVector3(0, 0, 1));
            GeoSpline2 spline = new GeoSpline2(3, Arch, BezierKnots, new double[] { 1, 2, 0.5, 1 });
            GeoSpline3 lifted = spline.ToSpline3(frame);

            Assert.Equal(spline.Weights.ToArray(), lifted.Weights.ToArray());
            Assert.Equal(spline.Knots.ToArray(), lifted.Knots.ToArray());
            Assert.Equal(spline.Length, lifted.Length, 10);

            GeoSpline2 back = lifted.ProjectToSpline2(frame);

            for (int i = 0; i < spline.ControlPointCount; i++)
            {
                Assert.True(back.ControlPoints[i].IsEqualTo(spline.ControlPoints[i], Tolerance.Default));
                Assert.True(frame.ToLocal(lifted.ControlPoints[i]).Z == 0.0 || Math.Abs(frame.ToLocal(lifted.ControlPoints[i]).Z) < 1E-12);
            }

            Assert.True(back.GetPointAtKnot(0.4).IsEqualTo(spline.GetPointAtKnot(0.4), Tolerance.Default));
        }

        #endregion

        #region Measuring

        [Fact]
        public void TheDerivativesOfTheArch_AreThoseOfTheBezierCurve()
        {
            GeoSpline2 spline = Cubic();

            // B'(0) = 3 (P1 - P0), B'(1) = 3 (P3 - P2), B''(0) = 6 (P0 - 2 P1 + P2).
            Assert.True(spline.GetDerivativeAtKnot(0.0, 1).IsEqualTo(new GeoVector2(300, 450), Tol));
            Assert.True(spline.GetDerivativeAtKnot(1.0, 1).IsEqualTo(new GeoVector2(300, -450), Tol));
            Assert.True(spline.GetDerivativeAtKnot(0.0, 2).IsEqualTo(new GeoVector2(0, -900), Tol));
            Assert.True(spline.GetDerivativeAtKnot(0.5, 1).IsEqualTo(new GeoVector2(300, 0), Tol));
            Assert.True(spline.GetDerivativeAtKnot(0.0, 3).IsEqualTo(GeoVector2.Zero, Tol));
            Assert.True(spline.GetDerivativeAtKnot(0.0, 4).IsEqualTo(GeoVector2.Zero, Tol));
            Assert.True(spline.GetTangentAtKnot(0.5).IsEqualTo(new GeoVector2(1, 0), Tol));
            Assert.True(spline.GetCurvatureAtKnot(0.5) < 0.0, "the arch turns clockwise at its top");
        }

        [Fact]
        public void TheDerivativeOverALongerDomain_IsSlowerInProportion()
        {
            var longer = new GeoSpline2(3, Arch, new double[] { 0, 0, 0, 0, 2, 2, 2, 2 });

            Assert.True(longer.GetDerivativeAtKnot(0.0, 1).IsEqualTo(new GeoVector2(150, 225), Tol));
            AssertPoint(150.0, 112.5, longer.GetPointAtKnot(1.0), 1E-12);
        }

        [Fact]
        public void WhereTheFirstTwoControlPointsCoincide_TheTangentIsFoundFromTheSecondDerivative()
        {
            var spline = new GeoSpline2(3, new[] { new GeoPoint2(0, 0), new GeoPoint2(0, 0), new GeoPoint2(100, 0), new GeoPoint2(100, 100) }, BezierKnots);

            Assert.True(spline.GetDerivativeAtKnot(0.0, 1).IsEqualTo(GeoVector2.Zero, Tol));
            Assert.True(spline.GetTangentAtKnot(0.0).IsEqualTo(new GeoVector2(1, 0), Tol));
            Assert.Throws<InvalidOperationException>(() => spline.GetCurvatureAtKnot(0.0));
        }

        [Fact]
        public void TheParameterAtALength_IsClampedToTheEnds_AndHalfWayAlongACircleIsHalfWayRound()
        {
            GeoSpline2 circle = Circle();

            Assert.Equal(0.0, circle.GetParameterAtDistance(0.0));
            Assert.Equal(0.0, circle.GetParameterAtDistance(-5.0));
            Assert.Equal(1.0, circle.GetParameterAtDistance(circle.Length));
            Assert.Equal(1.0, circle.GetParameterAtDistance(1E9));
            Assert.Equal(0.5, circle.GetParameterAtDistance(circle.Length * 0.5), 11);
            Assert.Equal(0.125, circle.GetParameterAtDistance(circle.Length * 0.125), 11);
            Assert.True(circle.GetPointAtKnot(circle.GetParameterAtDistance(Math.PI * 100.0)).IsEqualTo(new GeoPoint2(-100, 0), Tol));
        }

        [Fact]
        public void TheBoxOfACircle_IsItsSquare_AndOfARotatedOneItsTrueExtent()
        {
            GeoRectangle2 box = Circle().GetAabb();

            Assert.Equal(200.0, box.Width, 11);
            Assert.Equal(200.0, box.Height, 11);
            AssertPoint(0.0, 0.0, box.Center, 1E-11);

            GeoRectangle2 tilted = Circle().TransformBy(GeoTransform2.Scaling(2.0, 1.0)).TransformBy(GeoTransform2.Rotation(Math.PI / 6.0)).GetAabb();
            double expectedWidth = 2.0 * Math.Sqrt(Math.Pow(200.0 * Math.Cos(Math.PI / 6.0), 2) + Math.Pow(100.0 * Math.Sin(Math.PI / 6.0), 2));

            Assert.Equal(expectedWidth, tilted.Width, 9);
        }

        #endregion

        #region Nearest point and distance

        [Fact]
        public void ThePointsOffACircle_AreAsFarAsTheirDistanceFromItsRadius()
        {
            GeoSpline2 circle = Circle();

            Assert.Equal(60.0, circle.DistanceTo(new GeoPoint2(160, 0)), 11);
            Assert.Equal(100.0, circle.DistanceTo(new GeoPoint2(0, 0)), 11);
            Assert.Equal(40.0, circle.DistanceTo(new GeoPoint2(60, 0)), 11);
            Assert.Equal(120.0, circle.DistanceTo(new GeoPoint2(-176, -132)), 11);
            AssertPoint(100.0, 0.0, circle.GetClosestPointOnBoundary(new GeoPoint2(160, 0)), 1E-11);
            AssertPoint(0.0, 100.0, circle.GetClosestPointOnBoundary(new GeoPoint2(0, 300)), 1E-11);
            Assert.Equal(0.0, circle.GetKnotAtPoint(new GeoPoint2(160, 0)), 11);
            Assert.Equal(0.25, circle.GetKnotAtPoint(new GeoPoint2(0, 300)), 11);
            Assert.Equal(0.5, circle.GetKnotAtPoint(new GeoPoint2(-300, 1E-9)), 8);
        }

        [Fact]
        public void TheCentreOfACircleIsEquallyNearEveryPoint_AndTheLowestParameterIsGiven()
        {
            GeoSpline2 circle = Circle();

            Assert.Equal(0.0, circle.GetKnotAtPoint(new GeoPoint2(0, 0)));
            Assert.Equal(100.0, circle.DistanceTo(new GeoPoint2(0, 0)), 11);
            AssertPoint(100.0, 0.0, circle.GetClosestPointOnBoundary(new GeoPoint2(0, 0)), 1E-11);

            // A point on the axis of a symmetric arch is equally near its two sides: the lower parameter.
            GeoSpline2 arch = new GeoSpline2(2, new[] { new GeoPoint2(-100, 0), new GeoPoint2(0, 200), new GeoPoint2(100, 0) }, new double[] { 0, 0, 0, 1, 1, 1 });
            double t = arch.GetKnotAtPoint(new GeoPoint2(0, 0));

            Assert.True(t < 0.5);
        }

        [Fact]
        public void ADistanceIsNoughtOnlyWhereThePointIsOnTheCurve_AndIsPointOnReadsTheTolerance()
        {
            GeoSpline2 circle = Circle();
            GeoPoint2 on = circle.GetPointAtKnot(0.37);

            Assert.True(circle.DistanceTo(on) <= 1E-12);
            Assert.True(circle.IsPointOn(on));
            Assert.True(circle.IsPointOn(new GeoPoint2(100.0005, 0), Tol));
            Assert.False(circle.IsPointOn(new GeoPoint2(100.002, 0), Tol));
            Assert.True(circle.IsPointOn(new GeoPoint2(100.002, 0), new Tolerance(1E-2, 1E-5)));
            Assert.True(new GeoPoint2(100.0005, 0).IsPointOn(circle, Tol));
            Assert.False(new GeoPoint2(0, 0).IsPointOn(circle));
            Assert.True(circle.DistanceTo(new GeoPoint2(100.0005, 0), Tol) > 0.0);
        }

        [Fact]
        public void AStraightSpline_IsMeasuredAsASegmentIs()
        {
            var line = new GeoSpline2(1, new[] { new GeoPoint2(0, 0), new GeoPoint2(100, 0) }, new double[] { 0, 0, 1, 1 });

            Assert.Equal(30.0, line.DistanceTo(new GeoPoint2(50, 30)), 12);
            Assert.Equal(50.0, line.DistanceTo(new GeoPoint2(150, 0)), 12);
            Assert.Equal(25.0, line.DistanceTo(new GeoPoint2(-20, 15)), 12);
            Assert.Equal(0.5, line.GetKnotAtPoint(new GeoPoint2(50, 30)), 12);
            Assert.Equal(1.0, line.GetKnotAtPoint(new GeoPoint2(150, 0)), 12);
            Assert.Equal(0.0, line.GetKnotAtPoint(new GeoPoint2(-20, 15)), 12);

            GeoLine2 shortest = line.GetShortestLineTo(new GeoPoint2(50, 30));

            Assert.Equal(new GeoPoint2(50, 0), shortest.StartPoint);
            Assert.Equal(new GeoPoint2(50, 30), shortest.EndPoint);
        }

        [Fact]
        public void TheShortestLineLeavesTheSplineAndLandsOnThePoint_AndFromThePointItRunsTheOtherWay()
        {
            GeoSpline2 spline = Cubic();
            var point = new GeoPoint2(150, 200);

            GeoLine2 fromSpline = spline.GetShortestLineTo(point);
            GeoLine2 fromPoint = point.GetShortestLineTo(spline);

            Assert.Equal(point, fromSpline.EndPoint);
            Assert.Equal(fromSpline.StartPoint, fromPoint.EndPoint);
            Assert.Equal(fromSpline.EndPoint, fromPoint.StartPoint);
            Assert.Equal(spline.DistanceTo(point), fromSpline.Length, 12);
            Assert.Equal(spline.DistanceTo(point), point.DistanceTo(spline));
            Assert.Equal(spline.GetClosestPointOnBoundary(point), point.GetClosestPointOnBoundary(spline));
            Assert.Equal(87.5, spline.DistanceTo(point), 9);
            Assert.Equal(spline.DistanceTo(point, Tol), spline.DistanceTo(point));
            Assert.Equal(spline.GetShortestLineTo(point, Tol), fromSpline);
            Assert.Equal(point.GetShortestLineTo(spline, Tol), fromPoint);
            Assert.Throws<ArgumentNullException>(() => point.DistanceTo((GeoSpline2)null));
        }

        [Fact]
        public void ACurveThatLoopsOrDoublesBack_GivesTheNearestOfAllItsParts()
        {
            // A cubic that crosses itself: both ends run to the right of the loop.
            var loop = new GeoSpline2(3, new[] { new GeoPoint2(0, 0), new GeoPoint2(300, 300), new GeoPoint2(-100, 300), new GeoPoint2(200, 0) }, BezierKnots);
            var oracle = OracleOf(loop);

            foreach (GeoPoint2 q in new[] { new GeoPoint2(100, 150), new GeoPoint2(100, 100), new GeoPoint2(50, 200), new GeoPoint2(100, 20), new GeoPoint2(100, 400) })
            {
                double expected = oracle.Nearest(new[] { q.X, q.Y }, out _);

                Assert.True(Math.Abs(loop.DistanceTo(q) - expected) <= 1E-11, q + ": " + loop.DistanceTo(q) + ", the oracle says " + expected);
            }
        }

        [Fact]
        public void ALongSpline_FindsTheNearestPointWithoutAskingEverySpan()
        {
            int count = 100000;
            var fit = new GeoPoint2[count];

            for (int i = 0; i < count; i++)
            {
                fit[i] = new GeoPoint2(i * 2.0, 300.0 * Math.Sin(i * 0.002));
            }

            GeoSpline2 spline = GeoSpline2.FromControlPoints(3, fit);
            var query = new GeoPoint2(77777.0, 123.0);
            double distance = spline.DistanceTo(query);

            // Compare with the distance to the polyline of the control points, which the curve cannot stray far from.
            double polyline = double.MaxValue;

            for (int i = 38000; i < 40000; i++)
            {
                polyline = Math.Min(polyline, new GeoLine2(fit[i], fit[i + 1]).DistanceTo(query));
            }

            Assert.True(Math.Abs(distance - polyline) < 1.0, "distance " + distance + ", the control polygon's " + polyline);
            Assert.True(spline.GetClosestPointOnBoundary(query).X > 77000.0 && spline.GetClosestPointOnBoundary(query).X < 78500.0);
        }

        #endregion

        #region Flattening

        [Fact]
        public void ACircleFlattenedWithinAHundredth_IsAboutTwoHundredAndFiftyChords_AllOnTheCircleAndNoneFurtherThanTheTolerance()
        {
            GeoPolyline2 chain = Circle().Flatten(0.01);

            Assert.InRange(chain.EdgeCount, 223, 300);
            Assert.True(chain.Vertices.First().IsEqualTo(new GeoPoint2(100, 0), Tol));
            Assert.True(chain.Vertices.Last().IsEqualTo(new GeoPoint2(100, 0), Tol));

            foreach (GeoPoint2 corner in chain.Vertices)
            {
                Assert.Equal(100.0, Math.Sqrt(corner.X * corner.X + corner.Y * corner.Y), 10);
            }

            for (int i = 0; i + 1 < chain.VertexCount; i++)
            {
                // The middle of a chord is the farthest the arc stands from it: r - sqrt(r2 - (c / 2)2).
                double half = chain[i].DistanceTo(chain[i + 1]) * 0.5;

                Assert.True(100.0 - Math.Sqrt(100.0 * 100.0 - half * half) <= 0.01 * (1.0 + 1E-9));
            }
        }

        [Fact]
        public void ACubicFlattenedWithinAHundredth_StaysWithinIt_InAboutAHundredAndTwentyEightPieces()
        {
            GeoPolyline2 chain = Cubic().Flatten(0.01);
            var oracle = OracleOf(Cubic());

            Assert.InRange(chain.EdgeCount, 100, 140);

            for (int i = 0; i <= 4000; i++)
            {
                double[] p = oracle.FastPoint(i / 4000.0);
                double gap = chain.DistanceTo(new GeoPoint2(p[0], p[1]));

                Assert.True(gap <= 0.01 * (1.0 + 1E-9), "at " + i + ": " + gap);
            }
        }

        [Fact]
        public void AFlatSpline_IsOnePieceWhateverTheTolerance_AndABentOneCannotBeLessThanItsBend()
        {
            var line = new GeoSpline2(3, new[] { new GeoPoint2(0, 0), new GeoPoint2(100, 0), new GeoPoint2(200, 0), new GeoPoint2(300, 0) }, BezierKnots);

            Assert.Equal(1, line.Flatten(1E-6).EdgeCount);

            // Control points that run past the end and back are not flat for all they lie on a line.
            var back = new GeoSpline2(3, new[] { new GeoPoint2(0, 0), new GeoPoint2(300, 0), new GeoPoint2(-100, 0), new GeoPoint2(200, 0) }, BezierKnots);

            Assert.True(back.Flatten(1.0).EdgeCount > 1);
            Assert.Equal(200.0, back.GetAabb().Width, 9);
        }

        [Fact]
        public void ATinyOrNonsenseTolerance_IsRefused_AndOneThatWouldNeedAMillionChordsIsNamed()
        {
            GeoSpline2 circle = Circle();

            Assert.Throws<ArgumentOutOfRangeException>(() => circle.Flatten(0.0));
            Assert.Throws<ArgumentOutOfRangeException>(() => circle.Flatten(-1.0));
            Assert.Throws<ArgumentOutOfRangeException>(() => circle.Flatten(double.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => circle.Flatten(double.PositiveInfinity));

            ArgumentOutOfRangeException refused = Assert.Throws<ArgumentOutOfRangeException>(() => circle.Flatten(1E-13));
            Assert.Contains("1048576", refused.Message);
        }

        #endregion

        #region Equality and text

        [Fact]
        public void TwoSplinesAreEqualWhenTheyHoldTheSameNumbers_AndNotMerelyDrawTheSameCurve()
        {
            GeoSpline2 a = Cubic();
            GeoSpline2 b = new GeoSpline2(3, Arch, BezierKnots);

            Assert.Equal(a, b);
            Assert.True(a == b);
            Assert.False(a != b);
            Assert.Equal(a.GetHashCode(), b.GetHashCode());
            Assert.Equal(a, a.Clone());
            Assert.NotEqual(a, a.Translate(new GeoVector2(0, 1E-9)));
            Assert.NotEqual(a, new GeoSpline2(3, Arch, BezierKnots, new double[] { 1, 1, 1, 1.0000001 }));
            Assert.NotEqual(a, new GeoSpline2(3, Arch, new double[] { 0, 0, 0, 0, 2, 2, 2, 2 }));
            Assert.NotEqual(a, GeoSpline2.FromControlPoints(2, Arch));

            // A knot inserted that does not move the curve makes a different spline that draws the same one.
            GeoSpline2 cut = a.Split(0.5)[0];
            Assert.NotEqual(a, cut);

            Assert.False(a.Equals(null));
            Assert.False(a.Equals((object)"spline"));
            Assert.False(null == a);
            Assert.True((GeoSpline2)null == null);
        }

        [Fact]
        public void ASplineIsDescribed_ByItsDegreeItsPointsAndItsDomain()
        {
            Assert.Equal("GeoSpline2[Degree:3, 4 control points, Domain:[0, 1]]", Cubic().ToString());
            Assert.Equal("GeoSpline2[Degree:2, 9 control points, Domain:[0, 1], Rational]", Circle().ToString());
        }

        #endregion
    }
}
