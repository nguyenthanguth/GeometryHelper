using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Internal.Nurbs;
using Xunit;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// The pieces the splines are built from, each against a closed form: the polynomials in Bernstein form, the rule that
    /// integrates the speed, and the cutting of a curve into Bézier pieces.
    /// </summary>
    public class NurbsKernelTests
    {
        /// <summary>Gets the Bernstein coefficients of the product of (s - root) over the roots.</summary>
        private static double[] FromRoots(params double[] roots)
        {
            double[] c = { -roots[0], 1.0 - roots[0] };

            for (int i = 1; i < roots.Length; i++)
            {
                c = Bernstein.Multiply(c, new[] { -roots[i], 1.0 - roots[i] });
            }

            return c;
        }

        private static double[] Roots(double[] c, double negligible = 0.0)
        {
            var found = new List<double>();
            Bernstein.FindRoots(c, negligible, found);

            return found.OrderBy(x => x).ToArray();
        }

        [Fact]
        public void TheProductOfTwoPolynomials_IsTheirProductAtEveryPoint()
        {
            var random = new Random(1);

            for (int draw = 0; draw < 200; draw++)
            {
                double[] a = Enumerable.Range(0, 1 + random.Next(6)).Select(_ => random.NextDouble() * 20 - 10).ToArray();
                double[] b = Enumerable.Range(0, 1 + random.Next(6)).Select(_ => random.NextDouble() * 20 - 10).ToArray();
                double[] c = Bernstein.Multiply(a, b);

                Assert.Equal(a.Length + b.Length - 1, c.Length);

                for (int k = 0; k < 8; k++)
                {
                    double s = random.NextDouble();

                    Assert.Equal(Bernstein.Evaluate(a, s) * Bernstein.Evaluate(b, s), Bernstein.Evaluate(c, s), 10);
                }
            }
        }

        [Fact]
        public void TheDerivativeAndTheCut_AreTheDerivativeAndTheSamePolynomial()
        {
            double[] c = FromRoots(0.2, 0.5, 0.9, 0.35);
            double[] derivative = Bernstein.Derivative(c);

            for (double s = 0.0; s <= 1.0; s += 0.125)
            {
                Bernstein.Evaluate(c, s, out double slope);

                Assert.Equal(slope, Bernstein.Evaluate(derivative, s), 10);
            }

            Bernstein.Split(c, out double[] left, out double[] right);

            for (double s = 0.0; s <= 1.0; s += 0.125)
            {
                Assert.Equal(Bernstein.Evaluate(c, s * 0.5), Bernstein.Evaluate(left, s), 12);
                Assert.Equal(Bernstein.Evaluate(c, 0.5 + s * 0.5), Bernstein.Evaluate(right, s), 12);
            }
        }

        [Fact]
        public void EveryRootOfAPolynomialWithKnownRoots_IsFound_AndNoOther()
        {
            var random = new Random(2);

            for (int draw = 0; draw < 400; draw++)
            {
                int count = 1 + random.Next(7);
                var roots = new List<double>();

                while (roots.Count < count)
                {
                    double r = random.NextDouble();

                    if (roots.All(x => Math.Abs(x - r) > 0.01))
                    {
                        roots.Add(r);
                    }
                }

                double[] found = Roots(FromRoots(roots.ToArray()));
                double[] expected = roots.OrderBy(x => x).ToArray();

                Assert.Equal(expected.Length, found.Length);

                for (int i = 0; i < expected.Length; i++)
                {
                    Assert.True(Math.Abs(found[i] - expected[i]) <= 1E-9, "root " + expected[i] + ": found " + found[i]);
                }
            }
        }

        [Fact]
        public void RootsOutsideZeroToOne_AreNotFound_AndAPolynomialWithoutRootsHasNone()
        {
            Assert.Empty(Roots(FromRoots(-0.5, 1.5, 2.0)));
            Assert.Empty(Roots(new[] { 1.0, 2.0, 3.0 }));
            Assert.Single(Roots(FromRoots(0.5, 1.5, -2.0)));
        }

        [Fact]
        public void ARootAtAnEndOrAtTheMiddle_IsFoundToo_WhereTheCutsLandOnIt()
        {
            double[] atEnds = Roots(FromRoots(0.0, 0.5, 1.0));

            Assert.Contains(atEnds, r => Math.Abs(r) <= 1E-12);
            Assert.Contains(atEnds, r => Math.Abs(r - 0.5) <= 1E-12);
            Assert.Contains(atEnds, r => Math.Abs(r - 1.0) <= 1E-12);
            Assert.InRange(Roots(FromRoots(0.25, 0.5, 0.75)).Length, 3, 6);
        }

        [Fact]
        public void ARootTheCurveOnlyTouches_DoesNotHideTheOthers_AndAPairAHairApart_IsGivenWhereTheyAre()
        {
            // The double root at 0.4 is not a crossing; it may or may not be given, and the crossing at 0.9 is.
            double[] touching = Roots(FromRoots(0.4, 0.4, 0.9));

            Assert.Contains(touching, r => Math.Abs(r - 0.9) <= 1E-9);
            Assert.True(touching.Length < 200);

            double[] close = Roots(FromRoots(0.5 - 1E-9, 0.5 + 1E-9, 0.1));

            Assert.Contains(close, r => Math.Abs(r - 0.5) <= 1E-7);
            Assert.Contains(close, r => Math.Abs(r - 0.1) <= 1E-9);
        }

        [Fact]
        public void ANoiseFarBelowTheSizeOfTheNumbers_IsOneRootWhereItStands_NotAThousand()
        {
            var random = new Random(3);
            double[] noise = Enumerable.Range(0, 20).Select(_ => (random.NextDouble() - 0.5) * 1E-17).ToArray();
            double[] roots = Roots(noise, 1E-14);

            Assert.Single(roots);
            Assert.Equal(0.5, roots[0]);

            // Without being told what is noise it stops, with a bounded number of answers.
            Assert.True(Roots(noise).Length < 5000);
        }

        [Fact]
        public void TheRuleOfTenPoints_HasWeightsThatAddToTwo_AndIsExactForPolynomialsToDegreeNineteen()
        {
            Assert.Equal(2.0, GaussLegendre.Weights.Sum(), 14);

            for (int i = 1; i < GaussLegendre.Points; i++)
            {
                Assert.True(GaussLegendre.Nodes[i] > GaussLegendre.Nodes[i - 1]);
                Assert.Equal(-GaussLegendre.Nodes[GaussLegendre.Points - 1 - i], GaussLegendre.Nodes[i], 15);
            }

            for (int degree = 0; degree <= 19; degree++)
            {
                double sum = 0.0;

                for (int i = 0; i < GaussLegendre.Points; i++)
                {
                    sum += GaussLegendre.Weights[i] * Math.Pow(GaussLegendre.Nodes[i], degree);
                }

                double exact = degree % 2 == 1 ? 0.0 : 2.0 / (degree + 1);

                Assert.True(Math.Abs(sum - exact) <= 3E-15, "degree " + degree + ": " + sum + " against " + exact);
            }

            // The twentieth power is not integrated exactly; the rule is the ten-point one and not a longer.
            double twentieth = 0.0;

            for (int i = 0; i < GaussLegendre.Points; i++)
            {
                twentieth += GaussLegendre.Weights[i] * Math.Pow(GaussLegendre.Nodes[i], 20);
            }

            Assert.True(Math.Abs(twentieth - 2.0 / 21.0) > 1E-12);
        }

        [Fact]
        public void TheBezierPieces_AreTheCurveToRounding_AndMeetEndToEnd()
        {
            var random = new Random(4);
            double worst = 0.0;

            for (int draw = 0; draw < 200; draw++)
            {
                SplineOracle o = SplineGen.Draw(random, 2 + draw % 2, maxDegree: 6);
                NurbsCurve curve = new NurbsCurve(o.Dim, o.Degree, o.Points.ToArray(), o.Knots.ToArray(), o.Weights.Any(w => w != 1.0) ? o.Weights.ToArray() : null);
                BezierPieces pieces = curve.Pieces;

                Assert.Equal(o.Start, pieces.From[0]);
                Assert.Equal(o.End, pieces.To[pieces.Count - 1]);
                Assert.True(pieces.Count >= o.Breaks().Length - 1, "a span is never lost");

                for (int piece = 0; piece < pieces.Count; piece++)
                {
                    if (piece + 1 < pieces.Count)
                    {
                        Assert.Equal(pieces.To[piece], pieces.From[piece + 1]);

                        for (int k = 0; k < o.Dim; k++)
                        {
                            Assert.Equal(pieces.Points[pieces.PointOffset(piece) + o.Degree * o.Dim + k], pieces.Points[pieces.PointOffset(piece + 1) + k]);
                        }
                    }

                    for (int test = 0; test < 4; test++)
                    {
                        double share = random.NextDouble();
                        double t = pieces.From[piece] + share * (pieces.To[piece] - pieces.From[piece]);
                        double[] exact = o.Point(t);
                        var numerators = new double[o.Dim];
                        double weight = 0.0;

                        for (int j = 0; j <= o.Degree; j++)
                        {
                            double basis = Bernstein.Choose(o.Degree, j) * Math.Pow(share, j) * Math.Pow(1.0 - share, o.Degree - j);
                            double w = pieces.Rational ? pieces.Weights[pieces.WeightOffset(piece) + j] : 1.0;
                            weight += basis * w;

                            for (int k = 0; k < o.Dim; k++)
                            {
                                numerators[k] += basis * w * pieces.Points[pieces.PointOffset(piece) + j * o.Dim + k];
                            }
                        }

                        for (int k = 0; k < o.Dim; k++)
                        {
                            numerators[k] /= weight;
                        }

                        double error = SplineUnderTest.Distance(exact, numerators) / (o.Scale * 2.2E-16);
                        worst = Math.Max(worst, error);

                        Assert.True(error <= 200.0, "piece " + piece + " at " + share + " is " + error + " units of rounding off");
                    }
                }
            }

            Assert.True(worst > 0.0);
        }
    }
}
