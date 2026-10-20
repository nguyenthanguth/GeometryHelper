using System;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// The two kinds of parameter of a spline, told apart by their names (a share of the domain from 0 to 1, as the other curves
    /// have it, and a value of the knots), the lengths, the mirrored knots of a reversed spline, the refusals that keep a
    /// length right, and the tolerance of the fit points.
    /// </summary>
    public class GeoSplineParameterTests
    {
        private static readonly Tolerance Tol = Tolerance.Default;

        private static readonly GeoPoint2[] Arch =
        {
            new GeoPoint2(0, 0), new GeoPoint2(100, 150), new GeoPoint2(200, 150), new GeoPoint2(300, 0)
        };

        /// <summary>The arch over the domain [5, 12], where a share of it and a knot value are different numbers.</summary>
        private static GeoSpline2 Shifted() => new GeoSpline2(3, Arch, new double[] { 5, 5, 5, 5, 12, 12, 12, 12 });

        private static GeoSpline2 Circle() => GeoSpline2.FromCircle(new GeoCircle2(new GeoPoint2(0, 0), 100));

        [Fact]
        public void AShareOfTheDomain_IsNotAKnotValue_AndEachMemberSaysWhichItTakes()
        {
            GeoSpline2 s = Shifted();

            Assert.Equal(5.0, s.GetKnotAtParameter(0.0));
            Assert.Equal(12.0, s.GetKnotAtParameter(1.0));
            Assert.Equal(8.5, s.GetKnotAtParameter(0.5));
            Assert.Equal(5.0, s.GetKnotAtParameter(-3.0));
            Assert.Equal(12.0, s.GetKnotAtParameter(30.0));
            Assert.Equal(0.0, s.GetParameterAtKnot(5.0));
            Assert.Equal(1.0, s.GetParameterAtKnot(12.0));
            Assert.Equal(0.5, s.GetParameterAtKnot(8.5));
            Assert.Equal(0.0, s.GetParameterAtKnot(-100.0));
            Assert.Equal(1.0, s.GetParameterAtKnot(100.0));

            // The same point by either name, to the bit.
            foreach (double share in new[] { 0.0, 0.1, 0.5, 0.77, 1.0 })
            {
                Assert.Equal(s.GetPointAtKnot(s.GetKnotAtParameter(share)), s.GetPointAtParameter(share));
                Assert.Equal(s.GetTangentAtKnot(s.GetKnotAtParameter(share)), s.GetTangentAtParameter(share));
                Assert.Equal(s.GetCurvatureAtKnot(s.GetKnotAtParameter(share)), s.GetCurvatureAtParameter(share));
            }

            Assert.Equal(new GeoPoint2(150, 112.5).X, s.GetPointAtParameter(0.5).X, 12);
            Assert.Equal(112.5, s.GetPointAtParameter(0.5).Y, 12);
            Assert.Equal(112.5, s.GetPointAtKnot(8.5).Y, 12);

            // A knot value is not a share: 0.5 as a knot value is before the domain starts, so it is the start.
            Assert.Equal(s.StartPoint, s.GetPointAtKnot(0.5));

            // The derivative goes by the knots, and a spline over a domain 7 long runs 7 times slower than over 1.
            Assert.True(s.GetDerivativeAtKnot(5.0, 1).IsEqualTo(new GeoVector2(300.0 / 7.0, 450.0 / 7.0), Tol));
        }

        [Fact]
        public void TheShareOfTheDomainsEndsAreTheEndsOfTheKnots_ToTheBit_WhateverTheDomain()
        {
            foreach (double[] ends in new[] { new[] { 0.1, 0.3 }, new[] { -5.0, 7.7 }, new[] { 1E6, 1E6 + 0.001 }, new[] { 3.0, 3.0 + 1E-12 } })
            {
                var s = new GeoSpline2(3, Arch, new[] { ends[0], ends[0], ends[0], ends[0], ends[1], ends[1], ends[1], ends[1] });

                Assert.True(s.GetKnotAtParameter(0.0).Equals(ends[0]));
                Assert.True(s.GetKnotAtParameter(1.0).Equals(ends[1]));
                Assert.True(s.GetKnotAtParameter(1.0 - 1E-16).CompareTo(ends[1]) <= 0);
                Assert.Equal(s.StartPoint, s.GetPointAtParameter(0.0));
                Assert.Equal(s.EndPoint, s.GetPointAtParameter(1.0));
            }
        }

        [Fact]
        public void ParametersThatAreNotNumbers_AreRefused_ByEveryMemberThatTakesOne()
        {
            GeoSpline2 s = Shifted();

            Assert.Throws<ArgumentOutOfRangeException>(() => s.GetPointAtParameter(double.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => s.GetTangentAtParameter(double.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => s.GetCurvatureAtParameter(double.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => s.GetKnotAtParameter(double.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => s.GetParameterAtKnot(double.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => s.GetDistanceAtParameter(double.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => s.GetParameterAtDistance(double.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => s.GetPointAtDistance(double.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => s.GetPointAtKnot(double.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => s.GetDerivativeAtKnot(double.NaN, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => s.Split(double.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => s.SplitAtKnot(double.NaN));
        }

        [Fact]
        public void LengthsAndShares_OfACircle_AreTheArcsTheyAre()
        {
            GeoSpline2 circle = Circle();
            double quarter = Math.PI * 50.0;

            Assert.Equal(0.0, circle.GetDistanceAtParameter(0.0));
            Assert.Equal(circle.Length, circle.GetDistanceAtParameter(1.0), 12);
            Assert.Equal(quarter, circle.GetDistanceAtParameter(0.25), 9);
            Assert.Equal(2.0 * quarter, circle.GetDistanceAtParameter(0.5), 9);
            Assert.Equal(0.25, circle.GetParameterAtDistance(quarter), 11);
            Assert.Equal(0.0, circle.GetParameterAtDistance(-1.0));
            Assert.Equal(1.0, circle.GetParameterAtDistance(1E9));
            Assert.True(circle.GetPointAtDistance(quarter).IsEqualTo(new GeoPoint2(0, 100), Tol));
            Assert.Equal(circle.StartPoint, circle.GetPointAtDistance(0.0));
            Assert.True(circle.GetPointAtDistance(1E9).IsEqualTo(circle.EndPoint, Tol));

            // Where the nearest point of the circle to a point is, as a share and as a length from the start.
            var above = new GeoPoint2(0, 300);
            Assert.Equal(0.25, circle.GetParameterAtPoint(above), 11);
            Assert.Equal(0.25, circle.GetParameterAtPoint(above, Tol), 11);
            Assert.Equal(quarter, circle.GetDistanceAtPoint(above), 9);
            Assert.Equal(quarter, circle.GetDistanceAtPoint(above, Tol), 9);
            Assert.Equal(0.25, circle.GetKnotAtPoint(above), 11);
        }

        [Fact]
        public void OverADomainOfSevenTheShareTheLengthAndTheKnotAgree()
        {
            GeoSpline2 s = Shifted();
            var oracle = SplineUnderTest.Wrap(s).Oracle();

            foreach (double share in new[] { 0.1, 0.35, 0.5, 0.9 })
            {
                double knot = s.GetKnotAtParameter(share);
                double expected = oracle.Length(5.0, knot);

                Assert.Equal(expected, s.GetDistanceAtParameter(share), 9);
                Assert.Equal(share, s.GetParameterAtDistance(expected), 10);
                Assert.True(s.GetPointAtDistance(expected).IsEqualTo(s.GetPointAtParameter(share), Tol));
            }

            GeoPoint2 q = new GeoPoint2(150, 200);

            Assert.Equal(s.GetParameterAtKnot(s.GetKnotAtPoint(q)), s.GetParameterAtPoint(q));
            Assert.Equal(oracle.Length(5.0, s.GetKnotAtPoint(q)), s.GetDistanceAtPoint(q), 9);
        }

        [Fact]
        public void CuttingAtAShare_IsCuttingAtTheKnotValueItComesTo()
        {
            GeoSpline2 s = Shifted();
            GeoSpline2[] byShare = s.Split(0.5);
            GeoSpline2[] byKnot = s.SplitAtKnot(8.5);

            Assert.Equal(byKnot[0], byShare[0]);
            Assert.Equal(byKnot[1], byShare[1]);
            Assert.Equal(5.0, byShare[0].DomainStart);
            Assert.Equal(8.5, byShare[0].DomainEnd);
            Assert.Equal(8.5, byShare[1].DomainStart);
            Assert.Equal(12.0, byShare[1].DomainEnd);

            // A share is between 0 and 1 and a knot value between the knots: each refuses the other's numbers.
            Assert.Throws<ArgumentOutOfRangeException>(() => s.Split(0.0));
            Assert.Throws<ArgumentOutOfRangeException>(() => s.Split(1.0));
            Assert.Throws<ArgumentOutOfRangeException>(() => s.Split(8.5));
            Assert.Throws<ArgumentOutOfRangeException>(() => s.SplitAtKnot(0.5));
            Assert.Throws<ArgumentOutOfRangeException>(() => s.SplitAtKnot(5.0));
            Assert.Throws<ArgumentOutOfRangeException>(() => s.SplitAtKnot(12.0));
            Assert.Equal("parameter", Assert.Throws<ArgumentOutOfRangeException>(() => s.Split(2.0)).ParamName);
            Assert.Equal("knot", Assert.Throws<ArgumentOutOfRangeException>(() => s.SplitAtKnot(2.0)).ParamName);

            // A share so near an end that its knot value is the end's is refused, not turned into an empty part.
            var thin = new GeoSpline2(3, Arch, new[] { 5.0, 5.0, 5.0, 5.0, 5.0 + 1E-12, 5.0 + 1E-12, 5.0 + 1E-12, 5.0 + 1E-12 });
            Assert.Throws<ArgumentOutOfRangeException>(() => thin.Split(1E-9));
            Assert.Equal(2, thin.Split(0.5).Length);
        }

        [Fact]
        public void ASplineThatStandsStillAlongASpan_CannotBeCutThere_AndSaysItStandsStill()
        {
            // The first four control points are one point: along the first span the spline does not move.
            var points = new[] { new GeoPoint2(0, 0), new GeoPoint2(0, 0), new GeoPoint2(0, 0), new GeoPoint2(0, 0), new GeoPoint2(50, 50), new GeoPoint2(100, 0) };
            var s = new GeoSpline2(3, points, new double[] { 0, 0, 0, 0, 0.4, 0.7, 1, 1, 1, 1 });

            ArgumentException refused = Assert.Throws<ArgumentException>(() => s.SplitAtKnot(0.2));
            Assert.Equal("knot", refused.ParamName);
            Assert.Equal("parameter", Assert.Throws<ArgumentException>(() => s.Split(0.2)).ParamName);
            Assert.Throws<InvalidOperationException>(() => s.GetTangentAtKnot(0.2));
            Assert.Throws<InvalidOperationException>(() => s.GetCurvatureAtKnot(0.2));
            Assert.Throws<InvalidOperationException>(() => s.GetTangentAtParameter(0.2));

            // Beyond the span it moves, and can be cut.
            Assert.Equal(2, s.Split(0.5).Length);
            GeoVector2 tangent = s.GetTangentAtKnot(0.55);
            Assert.True(tangent.Length > 0.99 && tangent.Length < 1.01);
            Assert.Equal(0.0, s.GetPointAtKnot(0.2).X);
            Assert.Equal(s.Length, s.Split(0.5)[0].Length + s.Split(0.5)[1].Length, 9);
        }

        [Fact]
        public void TheReverseOfASplineWhoseKnotsRoundTogether_IsStillASpline_OfTheSameLength()
        {
            var points = new[] { new GeoPoint2(0, 0), new GeoPoint2(10, 20), new GeoPoint2(30, 10), new GeoPoint2(50, 40), new GeoPoint2(70, 0) };

            // An inner knot 1E-17 from the start mirrors onto the end of the domain, where a clamped spline cannot have it.
            var tiny = new GeoSpline2(3, points, new double[] { 0, 0, 0, 0, 1E-17, 1, 1, 1, 1 });
            GeoSpline2 back = tiny.Reverse();

            Assert.Equal(0.0, back.DomainStart);
            Assert.Equal(1.0, back.DomainEnd);
            Assert.True(back.Knots[4] > 0.0 && back.Knots[4] < 1.0);
            Assert.Equal(tiny.Length, back.Length, 9);

            // Two inner knots one unit in the last place apart mirror onto one number; they are kept apart.
            var close = new GeoSpline2(1, new[] { new GeoPoint2(0, 0), new GeoPoint2(10, 20), new GeoPoint2(30, 10), new GeoPoint2(50, 40) }, new double[] { 0, 0, 0.3, 0.30000000000000004, 1, 1 });
            GeoSpline2 closeBack = close.Reverse();

            Assert.True(closeBack.Knots[2] < closeBack.Knots[3]);
            Assert.Equal(close.Length, closeBack.Length, 9);
            Assert.Equal(close.GetPointAtKnot(0.5).X, closeBack.GetPointAtKnot(0.5).X, 9);

            // Knots that were equal stay equal.
            var repeated = new GeoSpline2(3, new[] { new GeoPoint2(0, 0), new GeoPoint2(10, 20), new GeoPoint2(30, 10), new GeoPoint2(50, 40), new GeoPoint2(70, 0), new GeoPoint2(80, 30) }, new double[] { 0, 0, 0, 0, 0.3, 0.3, 1, 1, 1, 1 });
            GeoSpline2 repeatedBack = repeated.Reverse();

            Assert.True(repeatedBack.Knots[4].Equals(repeatedBack.Knots[5]));
            Assert.Equal(0.7, repeatedBack.Knots[4], 15);
            Assert.Equal(repeated.Length, repeatedBack.Length, 10);
        }

        [Fact]
        public void TheReverseOfAReverse_IsTheSplineWhenItsKnotsAreDyadic()
        {
            // a + b - k is exact for knots that are dyadic fractions (0, 1/4, 1/2, 5, 12), so the knots come back as they went;
            // for knots such as 0.3 they come back to within the last place, which is what the control points do always.
            var s = new GeoSpline2(3, Arch.Concat(new[] { new GeoPoint2(400, 80) }), new double[] { 5, 5, 5, 5, 9, 12, 12, 12, 12 });

            Assert.Equal(s, s.Reverse().Reverse());

            var third = new GeoSpline2(3, Arch.Concat(new[] { new GeoPoint2(400, 80) }), new double[] { 0, 0, 0, 0, 0.3, 1, 1, 1, 1 });
            GeoSpline2 twice = third.Reverse().Reverse();

            Assert.True(twice.ControlPoints.SequenceEqual(third.ControlPoints));
            Assert.True(Math.Abs(twice.Knots[4] - 0.3) <= 1E-16);
        }

        [Fact]
        public void WeightsMoreThanATrillionToOneApart_AreRefused_AndNarrowerOnesGiveTheRightLength()
        {
            var points = new[] { new GeoPoint2(0, 0), new GeoPoint2(100, 0), new GeoPoint2(200, 50) };
            double[] knots = { 0, 0, 0, 1, 1, 1 };
            double polygon = 100.0 + Math.Sqrt(100.0 * 100.0 + 50.0 * 50.0);

            // A conic with a very heavy middle point is the polygon with its corner rounded away: its length is the polygon's.
            var heavy = new GeoSpline2(2, points, knots, new[] { 1.0, 1E12, 1.0 });

            Assert.Equal(polygon, heavy.Length, 6);

            ArgumentOutOfRangeException refused = Assert.Throws<ArgumentOutOfRangeException>(() => new GeoSpline2(2, points, knots, new[] { 1.0, 1.01E12, 1.0 }));
            Assert.Equal("weights", refused.ParamName);
            Assert.Throws<ArgumentOutOfRangeException>(() => new GeoSpline2(2, points, knots, new[] { 1.0, 1E20, 1.0 }));
            Assert.Throws<ArgumentOutOfRangeException>(() => new GeoSpline2(2, points, knots, new[] { 1E-7, 1.0, 1E6 }));

            var space = new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), new GeoPoint3(200, 50, 0) };

            Assert.Throws<ArgumentOutOfRangeException>(() => new GeoSpline3(2, space, knots, new[] { 1.0, 1E20, 1.0 }));
            Assert.Equal(polygon, new GeoSpline3(2, space, knots, new[] { 1.0, 1E12, 1.0 }).Length, 6);
        }

        [Fact]
        public void TheFitPointsToleranceDecidesWhichNeighboursAreOnePoint_AndThePlainFormReadsTheGlobalOne()
        {
            var chain = new[] { new GeoPoint2(0, 0), new GeoPoint2(100, 0), new GeoPoint2(100.0011, 0.0005), new GeoPoint2(100.0022, 0), new GeoPoint2(200, 50), new GeoPoint2(300, 0) };

            Assert.Equal(GeoSpline2.Interpolate(chain, 3, Tolerance.Global), GeoSpline2.Interpolate(chain));
            Assert.Equal(GeoSpline2.Interpolate(chain, 3, Tol), GeoSpline2.Interpolate(chain, 3));

            // Kept at a thousandth, the three near points make chords 0.0012 long beside chords of 100 and the curve swings far.
            GeoSpline2 kept = GeoSpline2.Interpolate(chain, 3, Tol);
            GeoSpline2 dropped = GeoSpline2.Interpolate(chain, 3, new Tolerance(1E-2, 1E-5));

            Assert.Equal(6, kept.ControlPointCount);
            Assert.Equal(4, dropped.ControlPointCount);
            Assert.True(OvershootOf(kept) > 1E5, "the curve swings " + OvershootOf(kept));
            Assert.True(OvershootOf(dropped) < 14.0, "the curve swings " + OvershootOf(dropped));

            // The tangent forms take a tolerance too.
            var start = new GeoVector2(1, 0);
            var end = new GeoVector2(1, 0);

            Assert.Equal(GeoSpline2.Interpolate(chain, start, end, 3, Tol), GeoSpline2.Interpolate(chain, start, end));
            Assert.Equal(4 + 2, GeoSpline2.Interpolate(chain, start, end, 3, new Tolerance(1E-2, 1E-5)).ControlPointCount);

            var chain3 = chain.Select(p => new GeoPoint3(p.X, p.Y, 0.0)).ToArray();

            Assert.Equal(GeoSpline3.Interpolate(chain3, 3, Tolerance.Global), GeoSpline3.Interpolate(chain3));
            Assert.Equal(4, GeoSpline3.Interpolate(chain3, 3, new Tolerance(1E-2, 1E-5)).ControlPointCount);
            Assert.Equal(4 + 2, GeoSpline3.Interpolate(chain3, new GeoVector3(1, 0, 0), new GeoVector3(1, 0, 0), 3, new Tolerance(1E-2, 1E-5)).ControlPointCount);
        }

        private static double OvershootOf(GeoSpline2 s)
        {
            GeoRectangle2 box = s.GetAabb();
            double low = box.Center.Y - box.Height / 2;
            double high = box.Center.Y + box.Height / 2;
            double left = box.Center.X - box.Width / 2;
            double right = box.Center.X + box.Width / 2;

            // The points lie in x from 0 to 300 and in y from 0 to 50.
            return Math.Max(Math.Max(0.0 - low, high - 50.0), Math.Max(0.0 - left, right - 300.0));
        }

        [Fact]
        public void ASpline3AsksTheSameWays_ByShareByKnotAndByLength()
        {
            var points = new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(100, 150, 60), new GeoPoint3(200, 150, 60), new GeoPoint3(300, 0, 0) };
            var s = new GeoSpline3(3, points, new double[] { 5, 5, 5, 5, 12, 12, 12, 12 });

            Assert.Equal(8.5, s.GetKnotAtParameter(0.5));
            Assert.Equal(0.5, s.GetParameterAtKnot(8.5));
            Assert.Equal(s.GetPointAtKnot(8.5), s.GetPointAtParameter(0.5));
            Assert.Equal(s.GetTangentAtKnot(8.5), s.GetTangentAtParameter(0.5));
            Assert.Equal(s.GetCurvatureAtKnot(8.5), s.GetCurvatureAtParameter(0.5));
            Assert.Equal(0.0, s.GetDistanceAtParameter(0.0));
            Assert.Equal(s.Length, s.GetDistanceAtParameter(1.0), 12);
            Assert.Equal(0.5, s.GetParameterAtDistance(s.GetDistanceAtParameter(0.5)), 10);
            Assert.True(s.GetPointAtDistance(s.Length * 0.3).IsEqualTo(s.GetPointAtParameter(s.GetParameterAtDistance(s.Length * 0.3)), Tol));

            var q = new GeoPoint3(150, 200, 45);

            Assert.Equal(s.GetParameterAtKnot(s.GetKnotAtPoint(q)), s.GetParameterAtPoint(q));
            Assert.Equal(s.GetParameterAtPoint(q), s.GetParameterAtPoint(q, Tol));
            Assert.Equal(s.GetDistanceAtPoint(q), s.GetDistanceAtPoint(q, Tol));
            Assert.Equal(s.GetDistanceAtParameter(s.GetParameterAtPoint(q)), s.GetDistanceAtPoint(q), 9);
            Assert.Equal(s.SplitAtKnot(8.5)[0], s.Split(0.5)[0]);
            Assert.Throws<ArgumentOutOfRangeException>(() => s.Split(1.0));
            Assert.Throws<ArgumentOutOfRangeException>(() => s.SplitAtKnot(5.0));

            var still = new GeoSpline3(3, new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(0, 0, 0), new GeoPoint3(0, 0, 0), new GeoPoint3(0, 0, 0), new GeoPoint3(50, 50, 5), new GeoPoint3(100, 0, 0) }, new double[] { 0, 0, 0, 0, 0.4, 0.7, 1, 1, 1, 1 });

            Assert.Equal("knot", Assert.Throws<ArgumentException>(() => still.SplitAtKnot(0.2)).ParamName);
            Assert.Throws<InvalidOperationException>(() => still.GetTangentAtKnot(0.2));

            var five = points.Concat(new[] { new GeoPoint3(350, 10, 5) }).ToArray();
            var tiny = new GeoSpline3(3, five, new double[] { 0, 0, 0, 0, 1E-17, 1, 1, 1, 1 });
            GeoSpline3 back = tiny.Reverse();

            Assert.Equal(tiny.Length, back.Length, 9);
        }
    }
}
