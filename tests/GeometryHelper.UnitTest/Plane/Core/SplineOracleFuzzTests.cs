using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Geometry;
using Xunit;
using Xunit.Abstractions;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// Random splines in the plane and in space, each answer of the library checked against an oracle that shares no code
    /// with it: points and derivatives against exact fractions worked out from the Cox-de Boor recursion, distances, lengths and
    /// boxes against a dense sampling polished in 28-digit decimals.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A case draws a spline (degree one to five, three to eleven control points, knots with repeats and with spans a thousand
    /// times thinner than the rest, weights for one in two) on a seeded generator, so a failure names its draw and repeats.
    /// Every error is held to a number of units of rounding of the size of the spline, and the worst seen is written to the
    /// test output so that a drift shows before it fails.
    /// </para>
    /// <para>
    /// The draws per test are those below, or what the environment variable SPLINE_FUZZ_DRAWS says for a longer run by hand.
    /// </para>
    /// </remarks>
    public class SplineOracleFuzzTests
    {
        private const double Eps = 2.220446049250313E-16;

        private static readonly int Multiplier = int.TryParse(Environment.GetEnvironmentVariable("SPLINE_FUZZ_DRAWS"), out int asked) && asked > 0 ? asked : 1;

        private readonly ITestOutputHelper _output;

        public SplineOracleFuzzTests(ITestOutputHelper output)
        {
            _output = output;
        }

        public static TheoryData<int> Dims()
        {
            return new TheoryData<int> { 2, 3 };
        }

        private static string Name(SplineOracle o, int draw) => "draw " + draw + ": degree " + o.Degree + ", " + o.Count + " points, knots " + string.Join(" ", o.Knots) + ", weights " + string.Join(" ", o.Weights);

        [Theory]
        [MemberData(nameof(Dims))]
        public void ThePointAtAParameter_IsTheExactOneToRounding(int dim)
        {
            var random = new Random(41000 + dim);
            double worst = 0.0;

            for (int draw = 0; draw < 400 * Multiplier; draw++)
            {
                SplineOracle o = SplineGen.Draw(random, dim, maxDegree: draw % 5 == 0 ? 10 : 5);
                SplineUnderTest s = SplineUnderTest.Make(o);

                for (int k = 0; k < 12; k++)
                {
                    double t = SplineGen.Parameter(random, o);
                    double[] exact = o.Point(t);
                    double error = SplineUnderTest.Distance(exact, s.PointAt(t)) / (o.Scale * Eps);
                    worst = Math.Max(worst, error);

                    Assert.True(error <= 24.0, Name(o, draw) + " at " + t + ": " + error + " units of rounding off");
                }
            }

            _output.WriteLine("worst point error " + worst.ToString("0.00") + " units of 2.2E-16 times the size of the spline");
        }

        [Theory]
        [MemberData(nameof(Dims))]
        public void TheDerivatives_AreTheExactOnes(int dim)
        {
            var random = new Random(41100 + dim);
            double worst = 0.0;

            for (int draw = 0; draw < 250 * Multiplier; draw++)
            {
                SplineOracle o = SplineGen.Draw(random, dim, maxDegree: 5, thinSpans: true);
                SplineUnderTest s = SplineUnderTest.Make(o);
                double thinnest = o.Breaks().Zip(o.Breaks().Skip(1), (a, b) => b - a).Min();

                for (int k = 0; k < 6; k++)
                {
                    double t = SplineGen.Parameter(random, o);
                    SplRat[][] exact = o.ExactDerivatives(t, 3);

                    for (int order = 1; order <= 3; order++)
                    {
                        double[] want = exact[order].Select(x => x.ToDouble()).ToArray();
                        double[] got = s.Derivative(t, order);
                        double size = o.Scale * Math.Pow(o.Degree / thinnest, order);
                        double error = SplineUnderTest.Distance(want, got) / (SplineUnderTest.Distance(want, new double[dim]) + size * 1E-6);
                        worst = Math.Max(worst, error);

                        Assert.True(error <= 1E-9, Name(o, draw) + " derivative " + order + " at " + t + ": " + string.Join(", ", got) + ", exact " + string.Join(", ", want));
                    }
                }
            }

            _output.WriteLine("worst derivative error " + worst.ToString("0.0E+0") + " of the derivative");
        }

        [Theory]
        [MemberData(nameof(Dims))]
        public void TheNearestPoint_IsTheNearestToRounding_AndFromEitherSideTheSame(int dim)
        {
            var random = new Random(41200 + dim);
            double worst = 0.0;
            int oracleMissed = 0;

            for (int draw = 0; draw < 160 * Multiplier; draw++)
            {
                SplineOracle o = SplineGen.Draw(random, dim, maxDegree: 5);
                SplineUnderTest s = SplineUnderTest.Make(o);

                for (int k = 0; k < 5; k++)
                {
                    double[] q = SplineGen.Query(random, o);
                    double expected = o.Nearest(q, out _);
                    double distance = s.DistanceTo(q);
                    double t = s.ParameterAtPoint(q);
                    double[] closest = s.ClosestPoint(q);
                    string at = Name(o, draw) + ", query " + string.Join(", ", q);

                    Assert.InRange(t, o.Start, o.End);
                    Assert.True(Math.Abs(SplineUnderTest.Distance(closest, q) - distance) <= 4 * Eps * o.Scale, at + ": the closest point is " + SplineUnderTest.Distance(closest, q) + " away, the distance " + distance);

                    // The distance may not be longer than the true one by more than rounding; a shorter one is a point the
                    // oracle's sampling can have stepped over, so it is checked to be a real point of the curve.
                    double error = (distance - expected) / (o.Scale * Eps);
                    worst = Math.Max(worst, error);

                    Assert.True(error <= 24.0, at + ": distance " + distance + ", the oracle says " + expected + " (" + error + " units off)");

                    if (error < -24.0)
                    {
                        double[] real = o.Point(t);
                        Assert.True(Math.Abs(SplineUnderTest.Distance(real, q) - distance) <= 24 * Eps * o.Scale, at + ": a distance of " + distance + " under the oracle's " + expected + " that is not a point of the curve");
                        oracleMissed++;
                    }

                    // From the other side, to the bit, and the shortest line leaving each way.
                    Assert.True(s.DistanceFromPoint(q).Equals(distance), at + ": from the point it is " + s.DistanceFromPoint(q));
                    double[][] line = s.ShortestLine(q);
                    double[][] back = s.ShortestLineFromPoint(q);
                    Assert.True(line[0].SequenceEqual(closest) && line[1].SequenceEqual(q), at + ": the shortest line is not from the closest point to the query");
                    Assert.True(back[0].SequenceEqual(line[1]) && back[1].SequenceEqual(line[0]), at + ": the shortest line from the point is not the reverse");
                }
            }

            _output.WriteLine("worst distance error " + worst.ToString("0.00") + " units of rounding; sampling stepped over a nearer point " + oracleMissed + " times");
        }

        [Theory]
        [MemberData(nameof(Dims))]
        public void TheLength_IsTheIntegralOfTheSpeedToATrillionth(int dim)
        {
            var random = new Random(41300 + dim);
            double worst = 0.0;

            for (int draw = 0; draw < 200 * Multiplier; draw++)
            {
                SplineOracle o = SplineGen.Draw(random, dim, maxDegree: 5);
                SplineUnderTest s = SplineUnderTest.Make(o);
                double expected = o.Length();
                double length = s.Length();
                double relative = Math.Abs(length - expected) / expected;
                worst = Math.Max(worst, relative);

                Assert.True(relative <= 1E-12, Name(o, draw) + ": length " + length.ToString("R") + ", the oracle says " + expected.ToString("R"));
            }

            _output.WriteLine("worst length error " + worst.ToString("0.0E+0") + " of the length");
        }

        [Theory]
        [MemberData(nameof(Dims))]
        public void TheParameterAtALength_HasThatLengthBehindIt(int dim)
        {
            var random = new Random(41350 + dim);
            double worst = 0.0;

            for (int draw = 0; draw < 120 * Multiplier; draw++)
            {
                SplineOracle o = SplineGen.Draw(random, dim, maxDegree: 5);
                SplineUnderTest s = SplineUnderTest.Make(o);
                double total = s.Length();

                for (int k = 0; k < 4; k++)
                {
                    double wanted = total * random.NextDouble();
                    double t = s.ParameterAtLength(wanted);
                    double behind = o.Length(o.Start, t);
                    double relative = Math.Abs(behind - wanted) / total;
                    worst = Math.Max(worst, relative);

                    Assert.InRange(t, o.Start, o.End);
                    Assert.True(relative <= 1E-11, Name(o, draw) + ": " + wanted + " along is the parameter " + t + ", which has " + behind + " behind it");
                }

                Assert.Equal(o.Start, s.ParameterAtLength(-1.0));
                Assert.Equal(o.End, s.ParameterAtLength(total * 1.5));
            }

            _output.WriteLine("worst length error of a parameter " + worst.ToString("0.0E+0") + " of the length");
        }

        [Theory]
        [MemberData(nameof(Dims))]
        public void TheBox_IsTheBoxTheCurveFills(int dim)
        {
            var random = new Random(41400 + dim);
            double worst = 0.0;

            for (int draw = 0; draw < 200 * Multiplier; draw++)
            {
                SplineOracle o = SplineGen.Draw(random, dim, maxDegree: 5);
                SplineUnderTest s = SplineUnderTest.Make(o);
                o.Box(out double[] min, out double[] max);
                double[][] box = s.Aabb();

                for (int c = 0; c < dim; c++)
                {
                    double lo = Math.Abs(box[0][c] - min[c]) / (o.Scale * Eps);
                    double hi = Math.Abs(box[1][c] - max[c]) / (o.Scale * Eps);
                    worst = Math.Max(worst, Math.Max(lo, hi));

                    Assert.True(lo <= 24.0 && hi <= 24.0, Name(o, draw) + ": axis " + c + " runs " + box[0][c] + " to " + box[1][c] + ", the oracle says " + min[c] + " to " + max[c]);
                }
            }

            _output.WriteLine("worst box error " + worst.ToString("0.00") + " units of rounding");
        }

        [Theory]
        [MemberData(nameof(Dims))]
        public void TheFlattenedChain_KeepsTheChordPromise(int dim)
        {
            var random = new Random(41500 + dim);
            double worstGap = 0.0;

            for (int draw = 0; draw < 70 * Multiplier; draw++)
            {
                SplineOracle o = SplineGen.Draw(random, dim, maxDegree: 5);
                SplineUnderTest s = SplineUnderTest.Make(o);
                double tolerance = o.Scale * new[] { 0.2, 0.05, 0.01 }[random.Next(3)];
                double[][] chain = s.Flatten(tolerance);

                Assert.True(SplineUnderTest.Distance(chain[0], o.Point(o.Start)) <= 8 * Eps * o.Scale, Name(o, draw) + ": the chain does not start at the start");
                Assert.True(SplineUnderTest.Distance(chain[chain.Length - 1], o.Point(o.End)) <= 8 * Eps * o.Scale, Name(o, draw) + ": the chain does not end at the end");

                // Every corner is on the curve, and every point of the curve is within the tolerance of the chain.
                foreach (double[] corner in chain)
                {
                    double off = o.Nearest(corner, out _, 400);

                    if (off > 1E-9 * o.Scale)
                    {
                        // The sampling can step over a place where the curve nearly turns back; the exact point at the parameter the
                        // library names is then what the corner is measured from.
                        off = SplineUnderTest.Distance(o.Point(s.ParameterAtPoint(corner)), corner);
                    }

                    Assert.True(off <= 1E-9 * o.Scale, Name(o, draw) + ": a corner " + string.Join(", ", corner) + " is off the curve by " + off);
                }

                int samples = 600;
                double[] breaks = o.Breaks();

                for (int span = 0; span + 1 < breaks.Length; span++)
                {
                    for (int i = 0; i <= samples; i++)
                    {
                        double[] p = o.FastPoint(Math.Min(breaks[span] + (breaks[span + 1] - breaks[span]) * i / samples, breaks[span + 1]));
                        double gap = DistanceToChain(chain, p);
                        worstGap = Math.Max(worstGap, gap / tolerance);

                        Assert.True(gap <= tolerance * (1.0 + 1E-9), Name(o, draw) + ": a point of the curve is " + gap + " from the chain, the tolerance " + tolerance);
                    }
                }
            }

            _output.WriteLine("the farthest the curve strayed from a chain was " + worstGap.ToString("0.000") + " of the tolerance");
        }

        private static double DistanceToChain(double[][] chain, double[] p)
        {
            double best = double.PositiveInfinity;

            for (int i = 0; i + 1 < chain.Length; i++)
            {
                double[] a = chain[i];
                double[] b = chain[i + 1];
                double lengthSquared = 0.0;
                double along = 0.0;

                for (int c = 0; c < p.Length; c++)
                {
                    lengthSquared += (b[c] - a[c]) * (b[c] - a[c]);
                    along += (p[c] - a[c]) * (b[c] - a[c]);
                }

                double share = lengthSquared > 0.0 ? Math.Min(Math.Max(along / lengthSquared, 0.0), 1.0) : 0.0;
                double square = 0.0;

                for (int c = 0; c < p.Length; c++)
                {
                    double d = p[c] - (a[c] + share * (b[c] - a[c]));
                    square += d * d;
                }

                best = Math.Min(best, Math.Sqrt(square));
            }

            return best;
        }

        [Theory]
        [MemberData(nameof(Dims))]
        public void CuttingASpline_GivesTwoPiecesThatAreTheSplineToRounding(int dim)
        {
            var random = new Random(41600 + dim);
            double worst = 0.0;

            for (int draw = 0; draw < 250 * Multiplier; draw++)
            {
                SplineOracle o = SplineGen.Draw(random, dim, maxDegree: 5);
                SplineUnderTest s = SplineUnderTest.Make(o);
                double u = SplineGen.Parameter(random, o);

                if (!(u > o.Start && u < o.End))
                {
                    continue;
                }

                SplineUnderTest[] pieces = s.Split(u);
                SplineUnderTest first = pieces[0];
                SplineUnderTest second = pieces[1];
                string at = Name(o, draw) + " cut at " + u;

                Assert.Equal(o.Start, first.DomainStart());
                Assert.Equal(u, first.DomainEnd());
                Assert.Equal(u, second.DomainStart());
                Assert.Equal(o.End, second.DomainEnd());
                Assert.Equal(s.Degree, first.Degree);
                Assert.Equal(s.Degree, second.Degree);

                int c = dim;
                Assert.True(first.ControlPoints.Skip(first.ControlPoints.Length - c).SequenceEqual(second.ControlPoints.Take(c)), at + ": the pieces do not meet at one control point");
                Assert.True(SplineUnderTest.Distance(first.ControlPoints.Skip(first.ControlPoints.Length - c).ToArray(), o.Point(u)) <= 16 * Eps * o.Scale, at + ": the pieces do not meet where the spline is");

                for (int k = 0; k < 6; k++)
                {
                    double v = o.Start + (u - o.Start) * random.NextDouble();
                    double w = u + (o.End - u) * random.NextDouble();
                    double a = SplineUnderTest.Distance(o.Point(v), first.PointAt(v)) / (o.Scale * Eps);
                    double b = SplineUnderTest.Distance(o.Point(w), second.PointAt(w)) / (o.Scale * Eps);
                    worst = Math.Max(worst, Math.Max(a, b));

                    Assert.True(a <= 40.0 && b <= 40.0, at + ": a piece is " + Math.Max(a, b) + " units of rounding off the spline");
                }

                Assert.True(Math.Abs(first.Length() + second.Length() - s.Length()) <= 1E-12 * s.Length(), at + ": the lengths of the pieces do not add up");
            }

            _output.WriteLine("worst error of a piece " + worst.ToString("0.00") + " units of rounding");
        }

        [Theory]
        [MemberData(nameof(Dims))]
        public void TheReversedSpline_RunsTheOtherWay(int dim)
        {
            var random = new Random(41700 + dim);
            double worst = 0.0;

            for (int draw = 0; draw < 200 * Multiplier; draw++)
            {
                SplineOracle o = SplineGen.Draw(random, dim, maxDegree: 5);
                SplineUnderTest s = SplineUnderTest.Make(o);
                SplineUnderTest reversed = s.Reverse();

                Assert.Equal(o.Start, reversed.DomainStart());
                Assert.Equal(o.End, reversed.DomainEnd());

                for (int k = 0; k < 6; k++)
                {
                    double t = SplineGen.Parameter(random, o);
                    double mirror = Math.Min(Math.Max(o.Start + o.End - t, o.Start), o.End);
                    double error = SplineUnderTest.Distance(o.Point(mirror), reversed.PointAt(t)) / (o.Scale * Eps);
                    worst = Math.Max(worst, error);

                    // A knot mirrored about the middle of the domain, a + b - k, is rounded to the last place of a + b, which
                    // moves the curve by that much along itself: the speed times 2.2E-16 of the size of the domain's ends.
                    double speed = o.SpeedAt(mirror);
                    double allowance = 60.0 + 8.0 * (Math.Abs(o.Start) + Math.Abs(o.End)) * speed / o.Scale;

                    Assert.True(error <= allowance, Name(o, draw) + ": the reverse at " + t + " is " + error + " units of rounding off, " + allowance + " allowed");
                }

                Assert.True(s.Reverse().Reverse().ControlPoints.SequenceEqual(s.ControlPoints), Name(o, draw) + ": reversing twice moves a control point");
            }

            _output.WriteLine("worst error of a reversed spline " + worst.ToString("0.00") + " units of rounding");
        }

        [Theory]
        [MemberData(nameof(Dims))]
        public void TheTangentAndTheCurvature_AreTheOnesTheExactDerivativesGive(int dim)
        {
            var random = new Random(41800 + dim);
            double worstTangent = 0.0;
            double worstCurvature = 0.0;

            for (int draw = 0; draw < 200 * Multiplier; draw++)
            {
                SplineOracle o = SplineGen.Draw(random, dim, maxDegree: 5);
                SplineUnderTest s = SplineUnderTest.Make(o);
                double t = SplineGen.Parameter(random, o);
                SplRat[][] exact = o.ExactDerivatives(t, 2);
                double[] d1 = exact[1].Select(x => x.ToDouble()).ToArray();
                double[] d2 = exact[2].Select(x => x.ToDouble()).ToArray();
                double speed = SplineUnderTest.Distance(d1, new double[dim]);

                if (speed < 1E-6 * o.Scale / (o.End - o.Start))
                {
                    continue;
                }

                double[] tangent = s.Tangent(t);
                double[] unit = d1.Select(x => x / speed).ToArray();
                double tangentError = SplineUnderTest.Distance(tangent, unit);
                worstTangent = Math.Max(worstTangent, tangentError);

                Assert.True(tangentError <= 1E-9, Name(o, draw) + ": tangent at " + t + " is " + string.Join(", ", tangent) + ", the exact one " + string.Join(", ", unit));

                double cross = dim == 2
                    ? d1[0] * d2[1] - d1[1] * d2[0]
                    : Math.Sqrt(Math.Pow(d1[1] * d2[2] - d1[2] * d2[1], 2) + Math.Pow(d1[2] * d2[0] - d1[0] * d2[2], 2) + Math.Pow(d1[0] * d2[1] - d1[1] * d2[0], 2));
                double expected = cross / (speed * speed * speed);
                double curvature = s.Curvature(t);
                double scale = SplineUnderTest.Distance(d2, new double[dim]) / (speed * speed);
                double error = Math.Abs(curvature - expected) / (scale + 1E-300);
                worstCurvature = Math.Max(worstCurvature, error);

                Assert.True(error <= 1E-8, Name(o, draw) + ": curvature at " + t + " is " + curvature + ", the exact one " + expected);
            }

            _output.WriteLine("worst tangent error " + worstTangent.ToString("0.0E+0") + ", worst curvature error " + worstCurvature.ToString("0.0E+0") + " of the acceleration over the speed squared");
        }
    }
}
