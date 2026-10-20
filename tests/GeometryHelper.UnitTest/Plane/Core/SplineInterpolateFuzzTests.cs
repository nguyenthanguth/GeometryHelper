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
    /// The spline through fit points, in the plane and in space: every fit point met at the parameter the method gives it, the
    /// knots the averages the method says, and the tangents the ones asked for, each read off the exact fractions of the
    /// oracle.
    /// </summary>
    public class SplineInterpolateFuzzTests
    {
        private const double Eps = 2.220446049250313E-16;

        private static readonly int Multiplier = int.TryParse(Environment.GetEnvironmentVariable("SPLINE_FUZZ_DRAWS"), out int asked) && asked > 0 ? asked : 1;

        private readonly ITestOutputHelper _output;

        public SplineInterpolateFuzzTests(ITestOutputHelper output)
        {
            _output = output;
        }

        public static TheoryData<int> Dims()
        {
            return new TheoryData<int> { 2, 3 };
        }

        /// <summary>Draws a chain of fit points: a random walk whose steps are a tenth to a half of the size apart.</summary>
        private static double[] Fit(Random random, int dim, int count, double size)
        {
            var fit = new double[count * dim];
            var at = new double[dim];

            for (int c = 0; c < dim; c++)
            {
                at[c] = (random.NextDouble() - 0.5) * size;
            }

            for (int i = 0; i < count; i++)
            {
                for (int c = 0; c < dim; c++)
                {
                    fit[i * dim + c] = Math.Round(at[c], 4);
                    at[c] += (random.NextDouble() - 0.5) * size * 0.8 + (c == 0 ? size * 0.1 : 0.0);
                }
            }

            return fit;
        }

        private static GeoPoint2[] P2(double[] fit) => Enumerable.Range(0, fit.Length / 2).Select(i => new GeoPoint2(fit[2 * i], fit[2 * i + 1])).ToArray();

        private static GeoPoint3[] P3(double[] fit) => Enumerable.Range(0, fit.Length / 3).Select(i => new GeoPoint3(fit[3 * i], fit[3 * i + 1], fit[3 * i + 2])).ToArray();

        private static double[] Parameters(double[] fit, int dim, out double total)
        {
            int count = fit.Length / dim;
            var u = new double[count];
            total = 0.0;

            for (int i = 1; i < count; i++)
            {
                double square = 0.0;

                for (int c = 0; c < dim; c++)
                {
                    square += (fit[i * dim + c] - fit[(i - 1) * dim + c]) * (fit[i * dim + c] - fit[(i - 1) * dim + c]);
                }

                total += Math.Sqrt(square);
                u[i] = total;
            }

            for (int i = 1; i < count; i++)
            {
                u[i] /= total;
            }

            u[count - 1] = 1.0;

            return u;
        }

        private static SplineUnderTest Interpolate(int dim, double[] fit, int degree, double[] startTangent, double[] endTangent)
        {
            if (dim == 2)
            {
                GeoSpline2 s = startTangent == null
                    ? GeoSpline2.Interpolate(P2(fit), degree, Tolerance.Default)
                    : GeoSpline2.Interpolate(P2(fit), new GeoVector2(startTangent[0], startTangent[1]), new GeoVector2(endTangent[0], endTangent[1]), degree, Tolerance.Default);

                return SplineUnderTest.Wrap(s);
            }

            GeoSpline3 t = startTangent == null
                ? GeoSpline3.Interpolate(P3(fit), degree, Tolerance.Default)
                : GeoSpline3.Interpolate(P3(fit), new GeoVector3(startTangent[0], startTangent[1], startTangent[2]), new GeoVector3(endTangent[0], endTangent[1], endTangent[2]), degree, Tolerance.Default);

            return SplineUnderTest.Wrap(t);
        }

        [Theory]
        [MemberData(nameof(Dims))]
        public void TheCurveMeetsEveryFitPointAtItsShareOfTheChain_AndTheKnotsAreTheAverages(int dim)
        {
            var random = new Random(61000 + dim);
            double worst = 0.0;

            for (int draw = 0; draw < 300 * Multiplier; draw++)
            {
                int count = 2 + random.Next(40);
                int degree = 1 + random.Next(Math.Min(5, count - 1));
                double size = new[] { 10.0, 100.0, 1000.0 }[random.Next(3)];
                double[] fit = Fit(random, dim, count, size);
                double[] u = Parameters(fit, dim, out _);
                SplineUnderTest s = Interpolate(dim, fit, degree, null, null);
                SplineOracle o = s.Oracle();
                string at = "draw " + draw + ": " + count + " points of degree " + degree;

                Assert.Equal(count, s.ControlPoints.Length / dim);
                Assert.Equal(degree, s.Degree);
                Assert.False(s.IsRational());
                Assert.Equal(0.0, s.DomainStart());
                Assert.Equal(1.0, s.DomainEnd());

                for (int i = 0; i < count; i++)
                {
                    double[] expected = fit.Skip(i * dim).Take(dim).ToArray();
                    double error = SplineUnderTest.Distance(o.Point(u[i]), expected) / (o.Scale * Eps);
                    worst = Math.Max(worst, error);

                    Assert.True(error <= 200.0, at + ": fit point " + i + " is met " + error + " units of rounding off");
                }

                for (int j = 1; j <= count - 1 - degree; j++)
                {
                    double mean = Enumerable.Range(j, degree).Sum(i => u[i]) / degree;

                    Assert.True(Math.Abs(s.Knots[j + degree] - mean) <= 4 * Eps, at + ": knot " + (j + degree) + " is " + s.Knots[j + degree] + ", the mean is " + mean);
                }
            }

            _output.WriteLine("a fit point was met at worst " + worst.ToString("0") + " units of rounding of the size of the spline off");
        }

        [Theory]
        [MemberData(nameof(Dims))]
        public void WithTangentsTheCurveMeetsTheFitPoints_AndLeavesAndArrivesInTheDirectionsAsked(int dim)
        {
            var random = new Random(62000 + dim);
            double worst = 0.0;
            double worstDirection = 0.0;

            for (int draw = 0; draw < 300 * Multiplier; draw++)
            {
                int count = 2 + random.Next(30);
                int degree = 2 + random.Next(Math.Min(4, count));
                degree = Math.Min(degree, count + 1);
                double size = new[] { 10.0, 100.0, 1000.0 }[random.Next(3)];
                double[] fit = Fit(random, dim, count, size);
                double[] u = Parameters(fit, dim, out double total);
                double[] start = Enumerable.Range(0, dim).Select(_ => random.NextDouble() * 2 - 1).ToArray();
                double[] end = Enumerable.Range(0, dim).Select(_ => random.NextDouble() * 2 - 1).ToArray();

                if (start.Sum(x => x * x) < 0.01 || end.Sum(x => x * x) < 0.01)
                {
                    continue;
                }

                double startLength = Math.Sqrt(start.Sum(x => x * x));
                double endLength = Math.Sqrt(end.Sum(x => x * x));
                SplineUnderTest s = Interpolate(dim, fit, degree, start.Select(x => x * 7.0).ToArray(), end.Select(x => x * 0.01).ToArray());
                SplineOracle o = s.Oracle();
                string at = "draw " + draw + ": " + count + " points of degree " + degree;

                Assert.Equal(count + 2, s.ControlPoints.Length / dim);

                for (int i = 0; i < count; i++)
                {
                    double[] expected = fit.Skip(i * dim).Take(dim).ToArray();
                    double error = SplineUnderTest.Distance(o.Point(u[i]), expected) / (o.Scale * Eps);
                    worst = Math.Max(worst, error);

                    Assert.True(error <= 200.0, at + ": fit point " + i + " is met " + error + " units of rounding off");
                }

                // The derivative at each end is the direction asked for, as long as the chain through the points.
                double[] first = o.ExactDerivatives(0.0, 1)[1].Select(x => x.ToDouble()).ToArray();
                double[] last = o.ExactDerivatives(1.0, 1)[1].Select(x => x.ToDouble()).ToArray();

                for (int c = 0; c < dim; c++)
                {
                    double a = Math.Abs(first[c] - start[c] / startLength * total) / total;
                    double b = Math.Abs(last[c] - end[c] / endLength * total) / total;
                    worstDirection = Math.Max(worstDirection, Math.Max(a, b));

                    Assert.True(a <= 1E-10 && b <= 1E-10, at + ": the derivative at an end is " + string.Join(", ", first) + " and " + string.Join(", ", last) + ", not the tangents times " + total);
                }
            }

            _output.WriteLine("a fit point was met at worst " + worst.ToString("0") + " units of rounding off; an end direction was off at worst " + worstDirection.ToString("0.0E+0") + " of the length");
        }

        [Theory]
        [MemberData(nameof(Dims))]
        public void ThousandsOfFitPoints_AreSolvedAndMet(int dim)
        {
            var random = new Random(63000 + dim);
            int count = 6000;
            double[] fit = Fit(random, dim, count, 100.0);
            double[] u = Parameters(fit, dim, out _);

            foreach (bool tangents in new[] { false, true })
            {
                SplineUnderTest s = tangents
                    ? Interpolate(dim, fit, 3, Enumerable.Repeat(1.0, dim).ToArray(), Enumerable.Repeat(-1.0, dim).ToArray())
                    : Interpolate(dim, fit, 3, null, null);

                for (int i = 0; i < count; i += 211)
                {
                    double[] expected = fit.Skip(i * dim).Take(dim).ToArray();

                    Assert.True(SplineUnderTest.Distance(s.PointAt(u[i]), expected) <= 1E-6, "fit point " + i + " with tangents " + tangents);
                }
            }
        }

        [Fact]
        public void TheFitPointsOfTheDocumentation_GiveTheCurveItDescribes()
        {
            // Four points of degree 3 whose three chords are as long: parameters 0, 1/3, 2/3 and 1, and the knots 0 0 0 0 1 1 1 1.
            GeoSpline2 spline = GeoSpline2.Interpolate(new[] { new GeoPoint2(0, 0), new GeoPoint2(100, 50), new GeoPoint2(200, 0), new GeoPoint2(300, 50) }, 3, Tolerance.Default);

            Assert.Equal(new double[] { 0, 0, 0, 0, 1, 1, 1, 1 }, spline.Knots.ToArray());
            Assert.True(spline.GetPointAtKnot(1.0 / 3.0).IsEqualTo(new GeoPoint2(100, 50), Tolerance.Default));
            Assert.True(spline.GetPointAtKnot(2.0 / 3.0).IsEqualTo(new GeoPoint2(200, 0), Tolerance.Default));

            // Five points of degree 3: the one inner knot is the mean of the three parameters 1/4, 1/2 and 3/4 of the points
            // on a line a unit apart.
            var line = Enumerable.Range(0, 5).Select(i => new GeoPoint2(i * 10, 0)).ToArray();
            GeoSpline2 evenly = GeoSpline2.Interpolate(line, 3, Tolerance.Default);

            Assert.Equal(0.5, evenly.Knots[4], 14);
            Assert.Equal(new double[] { 0, 0, 0, 0, 0.5, 1, 1, 1, 1 }, evenly.Knots.ToArray());
        }
    }
}
