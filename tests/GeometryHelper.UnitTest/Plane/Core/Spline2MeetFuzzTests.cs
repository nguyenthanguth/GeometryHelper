using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Geometry;
using Xunit;
using Xunit.Abstractions;
using static GeometryHelper.UnitTest.Plane.XLine2Probe;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// Random splines in the plane against random shapes of each of the fourteen kinds, each answer of the library checked against an
    /// oracle that shares no code with it (<see cref="Spline2MeetOracle"/>): the spline sampled densely over every span, the crossings
    /// found by bisection on a function that changes sign across each piece of the shape, the distance by golden section round each least
    /// of the samples. Both sides of every pair are asked and must agree to the bit.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A case draws a spline (degree one to five, knots with repeats and thin spans, weights for one in two) and shapes about it on a
    /// seeded generator, so a failure names its draw and repeats. A pair whose nearest approach without a crossing is within twice the
    /// point tolerance is left out, since the library reports a touch there and the oracle a near miss; those are the cases of
    /// <see cref="Spline2NearDegenerateTests"/>, with the numbers worked out by hand. The skipped are counted in the test output.
    /// </para>
    /// <para>
    /// The draws per test are those below, or what the environment variable SPLINE_FUZZ_DRAWS says for a longer run by hand.
    /// </para>
    /// </remarks>
    public class Spline2MeetFuzzTests
    {
        private static readonly int Multiplier = int.TryParse(Environment.GetEnvironmentVariable("SPLINE_FUZZ_DRAWS"), out int asked) && asked > 0 ? asked : 1;

        private readonly ITestOutputHelper _output;
        private int _extra;

        public Spline2MeetFuzzTests(ITestOutputHelper output)
        {
            _output = output;
        }

        internal static GeoSpline2 Make(SplineOracle o)
        {
            var points = new GeoPoint2[o.Count];

            for (int i = 0; i < points.Length; i++)
            {
                points[i] = new GeoPoint2(o.Points[2 * i], o.Points[2 * i + 1]);
            }

            return new GeoSpline2(o.Degree, points, o.Knots, o.Weights.Any(w => w != 1.0) ? o.Weights : null);
        }

        private static long Bits(double value) => BitConverter.DoubleToInt64Bits(value);

        private static string Name(SplineOracle o, string kind, int draw) => kind + ", draw " + draw + ": degree " + o.Degree + ", " + o.Count + " points " + string.Join(" ", o.Points.Select(v => v.ToString("R"))) + ", knots " + string.Join(" ", o.Knots.Select(v => v.ToString("R"))) + ", weights " + string.Join(" ", o.Weights.Select(v => v.ToString("R")));

        public static TheoryData<string> Kinds()
        {
            var data = new TheoryData<string>();

            foreach (string kind in Spline2MeetShapes.Kinds)
            {
                data.Add(kind);
            }

            return data;
        }

        private static double Reach(SplineOracle o)
        {
            double[] min = new double[2];
            double[] max = new double[2];

            for (int k = 0; k < 2; k++)
            {
                min[k] = double.MaxValue;
                max[k] = double.MinValue;
            }

            for (int i = 0; i < o.Count; i++)
            {
                for (int k = 0; k < 2; k++)
                {
                    min[k] = Math.Min(min[k], o.Points[2 * i + k]);
                    max[k] = Math.Max(max[k], o.Points[2 * i + k]);
                }
            }

            return Math.Max(1.0, Math.Max(max[0] - min[0], max[1] - min[1]));
        }

        /// <summary>
        /// Asks the library and the oracle about one spline and one shape and holds the library to the oracle; gives false for a pair left out.
        /// </summary>
        private bool Check(string name, GeoSpline2 spline, SplineOracle o, MeetShape shape, ref double worst)
        {
            double size = Reach(o);
            double coordinates = Math.Max(1.0, o.Points.Max(Math.Abs));
            double slack = 1E-9 * size + 64.0 * 2.220446049250313E-16 * coordinates * 16.0;
            Spline2MeetOracle.Answer expected = Spline2MeetOracle.Ask(o, shape, 1E-3);

            // A pair whose collision, or whose count of places, turns on a few rounding errors, a least of the distance at the point
            // tolerance, is a matter for the hand cases. A near touch within the tolerance is not: it is a place, and counted.
            if (Math.Abs(expected.Distance - 1E-3) <= 4.0 * slack || expected.Leasts.Any(v => Math.Abs(v - 1E-3) <= 4.0 * slack))
            {
                return false;
            }

            // A spline that hugs an ellipse, within the point tolerance and level, for a thousand times the tolerance or a quarter of the
            // smaller of the two, lies along it: no place is given there, which is the hand cases' to show.
            if (shape.Shape is GeoEllipse2 ellipse && expected.LongestLevel >= Math.Min(1000.0 * 1E-3, 0.25 * Math.Min(size, 2.0 * ellipse.MajorRadius)))
            {
                return false;
            }

            string label = name + " against " + shape.Kind;
            double fromSpline = Distance(spline, shape.Shape, Strict);
            double fromShape = Distance(shape.Shape, spline, Strict);

            Assert.True(Bits(fromSpline) == Bits(fromShape), label + ": the distance is " + fromSpline.ToString("R") + " from the spline and " + fromShape.ToString("R") + " from the shape");
            double error = Math.Abs(fromSpline - expected.Distance);
            worst = Math.Max(worst, error / size);
            Assert.True(error <= slack, label + ": distance " + fromSpline.ToString("R") + ", the oracle says " + expected.Distance.ToString("R") + " (shape " + shape.Shape + ")");

            bool collides = expected.Distance <= 1E-3;
            Assert.Equal(collides, Collides(spline, shape.Shape, Strict));
            Assert.Equal(collides, Collides(shape.Shape, spline, Strict));

            GeoPoint2[] crossings = Crossings(spline, shape.Shape, Strict);
            GeoPoint2[] back = Crossings(shape.Shape, spline, Strict);

            Assert.True(crossings.Length == back.Length && crossings.Zip(back, (p, q) => Bits(p.X) == Bits(q.X) && Bits(p.Y) == Bits(q.Y)).All(b => b), label + ": the crossings differ from the two sides");
            string shown = " (shape " + string.Join("; ", shape.Parts) + ")";

            // The places are the oracle's: every place the oracle found (a crossing, or a least of the distance within the tolerance that no
            // other piece of the shape is nearer at) is given, within two thousandths of where the oracle puts it, and none is given that
            // the oracle did not find; every place lies on the spline and within the tolerance of the shape, and no two are within the point
            // tolerance of each other.
            Assert.True(
                crossings.Length == expected.Places.Count,
                label + ": " + crossings.Length + " places " + string.Join(" ", crossings.Select(c => c.X.ToString("R") + "," + c.Y.ToString("R"))) + ", the oracle's " + expected.Places.Count + ": " + string.Join(" ", expected.Places.Select(c => c[0].ToString("R") + "," + c[1].ToString("R"))) + shown);

            foreach (double[] at in expected.Places)
            {
                Assert.True(
                    crossings.Any(c => Math.Sqrt((at[0] - c.X) * (at[0] - c.X) + (at[1] - c.Y) * (at[1] - c.Y)) <= 2E-3 + slack),
                    label + ": the place (" + at[0].ToString("R") + ", " + at[1].ToString("R") + ") the oracle found is not among " + string.Join(" ", crossings.Select(c => c.X.ToString("R") + "," + c.Y.ToString("R"))) + shown);
            }

            for (int i = 0; i < crossings.Length; i++)
            {
                double onSpline = spline.DistanceTo(crossings[i]);
                double onShape = shape.Parts.Min(part => part.Gap(crossings[i].X, crossings[i].Y));
                Assert.True(onSpline <= slack && onShape <= 1E-3 + slack, label + ": the place " + crossings[i] + " is " + onSpline + " off the spline and " + onShape + " off the shape" + shown);
                Assert.True(expected.Places.Any(c => Math.Sqrt((c[0] - crossings[i].X) * (c[0] - crossings[i].X) + (c[1] - crossings[i].Y) * (c[1] - crossings[i].Y)) <= 2E-3 + slack), label + ": the place " + crossings[i] + " is one the oracle did not find" + shown);

                for (int j = 0; j < i; j++)
                {
                    Assert.True(crossings[i].DistanceTo(crossings[j]) > 1E-3, label + ": the places " + crossings[j] + " and " + crossings[i] + " are within the point tolerance");
                }
            }

            _extra += Math.Abs(crossings.Length - expected.Knots.Count);

            bool tried = Try(spline, shape.Shape, out GeoPoint2[] points, Strict);
            Assert.Equal(crossings.Length > 0, tried);
            Assert.Equal(crossings.Length, points.Length);

            // The shortest segment runs from the spline to the boundary: of no length where they cross, otherwise as long as the gap.
            GeoLine2 leaving = Shortest(spline, shape.Shape, Strict);
            GeoLine2 landing = Shortest(shape.Shape, spline, Strict);

            Assert.True(Math.Abs(leaving.Length - expected.Boundary) <= slack, label + ": the shortest segment is " + leaving.Length.ToString("R") + " long, the oracle says " + expected.Boundary.ToString("R"));
            Assert.True(Bits(leaving.StartPoint.X) == Bits(landing.EndPoint.X) && Bits(leaving.StartPoint.Y) == Bits(landing.EndPoint.Y) && Bits(leaving.EndPoint.X) == Bits(landing.StartPoint.X) && Bits(leaving.EndPoint.Y) == Bits(landing.StartPoint.Y), label + ": the shortest segments are not the same turned round");
            return true;
        }

        [Theory]
        [MemberData(nameof(Kinds))]
        public void ASplineAndAShape_AreTheOraclesToRounding_AndFromEitherSideTheSame(string kind)
        {
            var random = new Random(52000 + Array.IndexOf(Spline2MeetShapes.Kinds, kind));
            double worst = 0.0;
            int checkedCount = 0;
            int left = 0;
            int crossed = 0;

            for (int draw = 0; draw < 40 * Multiplier; draw++)
            {
                SplineOracle o = SplineGen.Draw(random, 2, maxDegree: 5, thinSpans: draw % 4 == 0);
                GeoSpline2 spline = Make(o);
                double[] box = BoxOf(o);

                for (int k = 0; k < 4; k++)
                {
                    MeetShape shape = Spline2MeetShapes.Draw(random, kind, 0.5 * (box[0] + box[2]), 0.5 * (box[1] + box[3]), Reach(o));

                    if (Check(Name(o, kind, draw), spline, o, shape, ref worst))
                    {
                        checkedCount++;
                        crossed += Crossings(spline, shape.Shape, Strict).Length > 0 ? 1 : 0;
                    }
                    else
                    {
                        left++;
                    }
                }
            }

            _output.WriteLine(kind + ": " + checkedCount + " pairs held to the oracle (" + crossed + " crossing, " + left + " left out as at the tolerance); worst distance error " + worst.ToString("0.0E+0") + " of the size of the spline; " + _extra + " places that are near touches within the tolerance and no crossing, each the oracle's too");
            Assert.True(checkedCount > 50, "too few pairs checked: " + checkedCount);
        }

        /// <summary>
        /// A shape that touches a spline's tangent at a point of it, a few ten-thousandths of a millimetre off it either way: a line, a circle
        /// outside or inside the curve, an ellipse. The places such a pair has are a near touch within the point tolerance, one, or two
        /// crossings, and the stationary point between two crossings, a greatest of the distance, is no place.
        /// </summary>
        private static MeetShape TangentShape(Random random, SplineOracle o, string kind, double offset)
        {
            double u = o.Start + (o.End - o.Start) * (0.1 + 0.8 * random.NextDouble());
            double step = 1E-6 * (o.End - o.Start);
            double[] at = o.FastPoint(u);
            double[] before = o.FastPoint(u - step);
            double[] after = o.FastPoint(u + step);
            double tx = after[0] - before[0];
            double ty = after[1] - before[1];
            double length = Math.Sqrt(tx * tx + ty * ty);
            tx /= length;
            ty /= length;
            double nx = -ty;
            double ny = tx;
            double reach = Reach(o);

            // The point of the shape at the offset from the curve, along its normal, where the shape's tangent runs along the curve's.
            double qx = at[0] + nx * offset;
            double qy = at[1] + ny * offset;
            var shape = new MeetShape { Kind = kind };
            double side = random.Next(2) == 0 ? 1.0 : -1.0;
            double size = reach * Math.Max(0.05, random.NextDouble() * 0.5);

            switch (kind)
            {
                case "segment":
                {
                    var a = new GeoPoint2(qx - tx * size, qy - ty * size);
                    var b = new GeoPoint2(qx + tx * size, qy + ty * size);
                    shape.Shape = new GeoLine2(a, b);
                    shape.Parts.Add(MeetSegment.Between(a, b));
                    break;
                }

                case "circle":
                {
                    double cx = qx + nx * side * size;
                    double cy = qy + ny * side * size;
                    shape.Shape = new GeoCircle2(new GeoPoint2(cx, cy), size);
                    shape.Parts.Add(new MeetRound(cx, cy, size, 0.0, 2.0 * Math.PI, true));
                    shape.Inside = (x, y) => Math.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) <= size;
                    break;
                }

                default:
                {
                    double b = size * (0.2 + 0.8 * random.NextDouble());
                    double cx = qx + nx * side * b;
                    double cy = qy + ny * side * b;
                    shape.Shape = new GeoEllipse2(new GeoPoint2(cx, cy), new GeoVector2(tx, ty), size, b);
                    shape.Parts.Add(new MeetEllipse(cx, cy, tx, ty, size, b));
                    shape.Inside = (x, y) =>
                    {
                        double along = ((x - cx) * tx + (y - cy) * ty) / size;
                        double across = ((y - cy) * tx - (x - cx) * ty) / b;

                        return along * along + across * across <= 1.0;
                    };
                    break;
                }
            }

            return shape;
        }

        [Theory]
        [InlineData("segment")]
        [InlineData("circle")]
        [InlineData("ellipse")]
        public void ASplineAndAShapeTouchingItsTangentWithinTheTolerance_HaveTheOraclesPlacesAndNoOthers(string kind)
        {
            var random = new Random(55000 + kind.Length);
            double[] offsets = { 0.0, 1E-4, -1E-4, 4E-4, -4E-4, 9E-4, -9E-4, 1.3E-3, -1.3E-3, 3E-3, -3E-3 };
            double worst = 0.0;
            int checkedCount = 0;
            int touched = 0;

            for (int draw = 0; draw < 30 * Multiplier; draw++)
            {
                SplineOracle o;

                do
                {
                    o = SplineGen.Draw(random, 2, maxDegree: 5);
                }
                while (o.Degree < 2);

                GeoSpline2 spline = Make(o);

                foreach (double offset in offsets)
                {
                    MeetShape shape = TangentShape(random, o, kind, offset);

                    if (Check(Name(o, kind + " at " + offset, draw), spline, o, shape, ref worst))
                    {
                        checkedCount++;
                        touched += Crossings(spline, shape.Shape, Strict).Length > 0 ? 1 : 0;
                    }
                }
            }

            _output.WriteLine(kind + ": " + checkedCount + " pairs at the tolerance held to the oracle's places (" + touched + " with a place), " + _extra + " places that are near touches and no crossing");
            Assert.True(checkedCount > 150, "too few pairs checked: " + checkedCount);
            Assert.True(touched > 100, "too few pairs with a place: " + touched);
        }

        private static double[] BoxOf(SplineOracle o)
        {
            double[] box = { double.MaxValue, double.MaxValue, double.MinValue, double.MinValue };

            for (int i = 0; i < o.Count; i++)
            {
                box[0] = Math.Min(box[0], o.Points[2 * i]);
                box[1] = Math.Min(box[1], o.Points[2 * i + 1]);
                box[2] = Math.Max(box[2], o.Points[2 * i]);
                box[3] = Math.Max(box[3], o.Points[2 * i + 1]);
            }

            return box;
        }

        public static TheoryData<string> NastyKinds()
        {
            var data = new TheoryData<string>();

            foreach (string kind in SplineGen.NastyKinds)
            {
                data.Add(kind);
            }

            return data;
        }

        [Theory]
        [MemberData(nameof(NastyKinds))]
        public void ANastySpline_AgainstTheShapes_IsTheOracle(string nasty)
        {
            var random = new Random(53000 + Array.IndexOf(SplineGen.NastyKinds, nasty));
            string[] kinds = { "segment", "xline", "polygon", "circle", "ellipse", "arc", "chain" };
            double worst = 0.0;
            int checkedCount = 0;

            for (int draw = 0; draw < 14 * Multiplier; draw++)
            {
                SplineOracle o;

                try
                {
                    o = SplineGen.Nasty(random, 2, nasty);
                    Make(o);
                }
                catch (ArgumentException)
                {
                    continue;
                }

                GeoSpline2 spline = Make(o);
                double[] box = BoxOf(o);

                foreach (string kind in kinds)
                {
                    MeetShape shape = Spline2MeetShapes.Draw(random, kind, 0.5 * (box[0] + box[2]), 0.5 * (box[1] + box[3]), Reach(o));

                    if (Check(Name(o, nasty + "/" + kind, draw), spline, o, shape, ref worst))
                    {
                        checkedCount++;
                    }
                }
            }

            _output.WriteLine(nasty + ": " + checkedCount + " pairs held to the oracle; worst distance error " + worst.ToString("0.0E+0") + " of the size of the spline; " + _extra + " places that are near touches within the tolerance and no crossing, each the oracle's too");
            Assert.True(checkedCount > 12, "too few pairs checked: " + checkedCount);
        }

        [Fact]
        public void ASplineAndAnotherSpline_AreTheOracle_AndFromEitherSideTheSame()
        {
            var random = new Random(54000);
            int checkedCount = 0;
            int crossed = 0;
            int missedByTheOracle = 0;

            for (int draw = 0; draw < 24 * Multiplier; draw++)
            {
                SplineOracle first = SplineGen.Draw(random, 2, maxDegree: 5, thinSpans: draw % 4 == 0);
                SplineOracle second = SplineGen.Draw(random, 2, maxDegree: 5);
                double[] box = BoxOf(first);
                double[] box2 = BoxOf(second);

                // The second is moved to lie about the first.
                double dx = 0.5 * (box[0] + box[2]) - 0.5 * (box2[0] + box2[2]) + (random.NextDouble() - 0.5) * Reach(first);
                double dy = 0.5 * (box[1] + box[3]) - 0.5 * (box2[1] + box2[3]) + (random.NextDouble() - 0.5) * Reach(first);
                var moved = new double[second.Points.Length];

                for (int i = 0; i < moved.Length; i += 2)
                {
                    moved[i] = Math.Round(second.Points[i] + dx, 4);
                    moved[i + 1] = Math.Round(second.Points[i + 1] + dy, 4);
                }

                second = new SplineOracle(2, second.Degree, second.Knots, moved, second.Weights.Any(w => w != 1.0) ? second.Weights : null);
                GeoSpline2 a = Make(first);
                GeoSpline2 b = Make(second);
                double size = Math.Max(Reach(first), Reach(second));
                double slack = 1E-6 * size;
                Spline2MeetOracle.SplineAnswer expected = Spline2MeetOracle.AskSplines(first, second, size);
                string label = "draw " + draw + ": " + Name(first, "A", draw) + " | " + Name(second, "B", draw);

                double ab = a.DistanceTo(b, Strict);
                double ba = b.DistanceTo(a, Strict);
                Assert.True(Bits(ab) == Bits(ba), label + ": the distance is " + ab.ToString("R") + " one way and " + ba.ToString("R") + " the other");

                // A least distance found by the oracle's search is above the true one; the library's is a real pair of points, so it is never above it.
                Assert.True(ab <= expected.Distance + slack, label + ": the library says " + ab.ToString("R") + ", more than the oracle's " + expected.Distance.ToString("R"));

                if (expected.Crossings.Count > 0)
                {
                    Assert.Equal(0.0, ab);
                    crossed++;
                }
                else if (ab > 2E-3)
                {
                    Assert.True(ab >= expected.Distance - slack, label + ": the library says " + ab.ToString("R") + ", the oracle " + expected.Distance.ToString("R"));
                }

                GeoPoint2[] along = a.GetIntersections(b, Strict);
                GeoPoint2[] other = b.GetIntersections(a, Strict);

                Assert.Equal(along.Length, other.Length);

                foreach (GeoPoint2 p in along)
                {
                    Assert.Contains(other, q => Bits(p.X) == Bits(q.X) && Bits(p.Y) == Bits(q.Y));

                    // Every crossing given is on both curves.
                    Assert.True(a.DistanceTo(p) <= slack && b.DistanceTo(p) <= slack, label + ": the crossing " + p + " is off a curve");
                }

                foreach (double[] p in expected.Crossings)
                {
                    Assert.True(along.Any(q => Math.Sqrt((p[0] - q.X) * (p[0] - q.X) + (p[1] - q.Y) * (p[1] - q.Y)) <= 2E-3), label + ": the oracle's crossing (" + p[0] + ", " + p[1] + ") is missing from " + string.Join(" ", along));
                }

                missedByTheOracle += Math.Max(0, along.Length - expected.Crossings.Count);

                GeoLine2 line = a.GetShortestLineTo(b, Strict);
                GeoLine2 reversed = b.GetShortestLineTo(a, Strict);

                Assert.True(Bits(line.StartPoint.X) == Bits(reversed.EndPoint.X) && Bits(line.EndPoint.Y) == Bits(reversed.StartPoint.Y), label + ": the shortest segments are not the same turned round");
                Assert.True(Math.Abs(line.Length - ab) <= 1E-9 * size, label + ": the shortest segment is " + line.Length.ToString("R") + " long and the distance " + ab.ToString("R"));
                checkedCount++;
            }

            _output.WriteLine(checkedCount + " pairs of splines held to the oracle (" + crossed + " crossing; the library found " + missedByTheOracle + " crossings more than the sampled oracle saw, each checked to lie on both curves)");
        }
    }
}
