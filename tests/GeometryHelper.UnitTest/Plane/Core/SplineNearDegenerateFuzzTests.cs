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
    /// The splines that real models give and a random one rarely does, each question checked against the oracle as in
    /// <see cref="SplineOracleFuzzTests"/>: a million away from the origin, with control points repeated, all on one line, with
    /// spans a millionth as wide as the rest, of a degree up to ten, with weights a thousandfold apart, and with a cusp.
    /// </summary>
    public class SplineNearDegenerateFuzzTests
    {
        private const double Eps = 2.220446049250313E-16;

        private static readonly int Multiplier = int.TryParse(Environment.GetEnvironmentVariable("SPLINE_FUZZ_DRAWS"), out int asked) && asked > 0 ? asked : 1;

        private readonly ITestOutputHelper _output;

        public SplineNearDegenerateFuzzTests(ITestOutputHelper output)
        {
            _output = output;
        }

        public static TheoryData<int, string> Cases()
        {
            var data = new TheoryData<int, string>();

            foreach (int dim in new[] { 2, 3 })
            {
                foreach (string kind in SplineGen.NastyKinds)
                {
                    data.Add(dim, kind);
                }
            }

            return data;
        }

        private static string Name(SplineOracle o, string kind, int draw) => kind + " draw " + draw + ": degree " + o.Degree + ", points " + string.Join(" ", o.Points.Select(x => x.ToString("R"))) + ", knots " + string.Join(" ", o.Knots.Select(x => x.ToString("R"))) + ", weights " + string.Join(" ", o.Weights);

        [Theory]
        [MemberData(nameof(Cases))]
        public void ThePointsTheBoxAndTheLength_AreTheOraclesToRounding(int dim, string kind)
        {
            var random = new Random(52000 + dim * 100 + Array.IndexOf(SplineGen.NastyKinds, kind));
            double worstPoint = 0.0;
            double worstBox = 0.0;
            double worstLength = 0.0;

            // The oracle works slowly on a span a millionth wide, so there are fewer of those.
            int draws = (kind == "SpansAMillionthWide" ? 30 : 120) * Multiplier;

            for (int draw = 0; draw < draws; draw++)
            {
                SplineOracle o = SplineGen.Nasty(random, dim, kind);
                SplineUnderTest s = SplineUnderTest.Make(o);
                string at = Name(o, kind, draw);

                for (int k = 0; k < 8; k++)
                {
                    double t = SplineGen.Parameter(random, o);
                    double error = SplineUnderTest.Distance(o.Point(t), s.PointAt(t)) / (o.Scale * Eps);
                    worstPoint = Math.Max(worstPoint, error);

                    Assert.True(error <= 40.0, at + ": the point at " + t + " is " + error + " units of rounding off");
                }

                o.Box(out double[] min, out double[] max);
                double[][] box = s.Aabb();

                for (int c = 0; c < dim; c++)
                {
                    double error = Math.Max(Math.Abs(box[0][c] - min[c]), Math.Abs(box[1][c] - max[c])) / (o.Scale * Eps);
                    worstBox = Math.Max(worstBox, error);

                    Assert.True(error <= 40.0, at + ": axis " + c + " runs " + box[0][c] + " to " + box[1][c] + ", the oracle says " + min[c] + " to " + max[c]);
                }

                // The length of the curve is not asked of a cusp: the speed has a corner there, and the answer is good to less.
                double expected = kind == "WeightsAThousandfoldApart" ? 0.0 : o.Length();
                double length = s.Length();
                // The oracle samples a span a millionth wide at parameters rounded to the last place of numbers near 4, which is 4E-10 of the span.
                double allowed = Math.Max(kind == "ACusp" ? 1E-9 : kind == "SpansAMillionthWide" ? 1E-11 : 1E-12, 16 * Eps * o.Scale / Math.Max(expected, 1E-300));

                if (kind == "WeightsAThousandfoldApart")
                {
                    // Where the weights are a million to one apart the curve runs a thousandfold faster over some stretches than
                    // others, and the oracle's quadrature, which samples the speed, cannot be trusted any more than the library's rule
                    // would be without cutting the piece where the weights change. The length of a chain inscribed within a millionth
                    // of the size of the spline is a length the curve cannot be shorter than and comes within a millionth of.
                    double extent = o.Points.Max() - o.Points.Min();
                    var curve = new GeometryHelper.Internal.Nurbs.NurbsCurve(dim, o.Degree, o.Points.ToArray(), o.Knots.ToArray(), o.Weights.ToArray());
                    var corners = new List<double>();
                    curve.Flatten(extent * 1E-6, corners);
                    expected = 0.0;

                    for (int i = dim; i < corners.Count; i += dim)
                    {
                        double square = 0.0;

                        for (int c = 0; c < dim; c++)
                        {
                            square += (corners[i + c] - corners[i - dim + c]) * (corners[i + c] - corners[i - dim + c]);
                        }

                        expected += Math.Sqrt(square);
                    }

                    allowed = 2E-6;
                    Assert.True(length >= expected * (1.0 - 1E-12), at + ": the length " + length.ToString("R") + " is less than the chain inscribed in it, " + expected.ToString("R"));
                }

                double relative = Math.Abs(length - expected) / Math.Max(expected, 1E-300);
                worstLength = Math.Max(worstLength, relative);

                // A curve a hundred long at three times ten to the seventh from the origin is known to 6E-9 by its coordinates alone, which is
                // 4E-11 of its length: the length is held to that, and to a trillionth where the curve is near the origin.
                Assert.True(relative <= allowed, at + ": length " + length.ToString("R") + ", the oracle says " + expected.ToString("R"));
            }

            _output.WriteLine(kind + " in " + dim + " dimensions: worst point " + worstPoint.ToString("0.0") + ", worst box " + worstBox.ToString("0.0") + " units of rounding, worst length " + worstLength.ToString("0.0E+0"));
        }

        [Theory]
        [MemberData(nameof(Cases))]
        public void TheNearestPoint_IsTheOraclesToRounding(int dim, string kind)
        {
            var random = new Random(53000 + dim * 100 + Array.IndexOf(SplineGen.NastyKinds, kind));
            double worst = 0.0;

            for (int draw = 0; draw < 50 * Multiplier; draw++)
            {
                SplineOracle o = SplineGen.Nasty(random, dim, kind);
                SplineUnderTest s = SplineUnderTest.Make(o);
                string at = Name(o, kind, draw);

                for (int k = 0; k < 4; k++)
                {
                    double[] q = SplineGen.Query(random, o);
                    double expected = o.Nearest(q, out _);
                    double distance = s.DistanceTo(q);
                    double error = (distance - expected) / (o.Scale * Eps);
                    worst = Math.Max(worst, error);

                    // The library may find a nearer point than the sampling did, and not a farther one.
                    Assert.True(error <= 40.0, at + ", query " + string.Join(", ", q) + ": distance " + distance + ", the oracle says " + expected + " (" + error + " units off)");

                    double t = s.ParameterAtPoint(q);
                    Assert.InRange(t, o.Start, o.End);
                    Assert.True(SplineUnderTest.Distance(o.Point(t), q) <= expected + 40 * Eps * o.Scale + 1E-9 * expected, at + ": the parameter " + t + " is not a nearest point");
                }
            }

            _output.WriteLine(kind + " in " + dim + " dimensions: the nearest point was at worst " + worst.ToString("0.0") + " units of rounding too far");
        }

        [Theory]
        [MemberData(nameof(Cases))]
        public void TheFlattenedChain_StaysWithinTheTolerance_AndCuttingKeepsTheCurve(int dim, string kind)
        {
            var random = new Random(54000 + dim * 100 + Array.IndexOf(SplineGen.NastyKinds, kind));

            for (int draw = 0; draw < 25 * Multiplier; draw++)
            {
                SplineOracle o = SplineGen.Nasty(random, dim, kind);
                SplineUnderTest s = SplineUnderTest.Make(o);
                string at = Name(o, kind, draw);
                double extent = Math.Max(o.Points.Max() - o.Points.Min(), 1.0);
                double tolerance = extent * 0.02;

                if (tolerance < 0.05)
                {
                    tolerance = 0.05;
                }

                double[][] chain = s.Flatten(tolerance);
                double[] breaks = o.Breaks();

                for (int span = 0; span + 1 < breaks.Length; span++)
                {
                    for (int i = 0; i <= 300; i++)
                    {
                        double[] p = o.FastPoint(Math.Min(breaks[span] + (breaks[span + 1] - breaks[span]) * i / 300.0, breaks[span + 1]));
                        double gap = DistanceToChain(chain, p);

                        Assert.True(gap <= tolerance * (1.0 + 1E-9), at + ": a point of the curve is " + gap + " from the chain, the tolerance " + tolerance);
                    }
                }

                double u = SplineGen.Parameter(random, o);

                if (u > o.Start && u < o.End)
                {
                    SplineUnderTest[] pieces;

                    try
                    {
                        pieces = s.Split(u);
                    }
                    catch (ArgumentException)
                    {
                        // A cut where the spline stands still, along a span whose control points are all one point, is refused: the
                        // only kind of draw that has such a span is the one with control points repeated.
                        Assert.Equal("RepeatedControlPoints", kind);
                        continue;
                    }

                    for (int k = 0; k < 4; k++)
                    {
                        double v = o.Start + (u - o.Start) * random.NextDouble();
                        double w = u + (o.End - u) * random.NextDouble();

                        Assert.True(SplineUnderTest.Distance(o.Point(v), pieces[0].PointAt(v)) <= 60 * Eps * o.Scale, at + ": the first piece strays at " + v + " of a cut at " + u);
                        Assert.True(SplineUnderTest.Distance(o.Point(w), pieces[1].PointAt(w)) <= 60 * Eps * o.Scale, at + ": the second piece strays at " + w + " of a cut at " + u);
                    }
                }
            }
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
    }
}
