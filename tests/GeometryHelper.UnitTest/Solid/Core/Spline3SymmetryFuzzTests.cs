using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GeometryHelper;
using GeometryHelper.Geometry;
using GeometryHelper.UnitTest.Plane;
using Xunit;
using Xunit.Abstractions;
using static GeometryHelper.UnitTest.Solid.XLine3Probe;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// A spline in space and a shape asked the five queries from either side must give the same answer to the bit, whatever the draw: the
    /// shape's member hands the question to the spline's, and the answer is turned round where it has a direction. Splines are asked about
    /// each other, which is the one place it is not a hand-over, since both are splines: the work is done from the one that comes first, and
    /// the places they meet are given in order along the one asked.
    /// </summary>
    /// <remarks>
    /// A shape that is a curve, an arc, a circle, an ellipse, an edge, a chain or a loop, is measured as a pair of splines and costs a
    /// hundred times what the others do: it has twenty draws, at the strict tolerance and the default, where the others have eighty at
    /// three; the environment variable SPLINE_FUZZ_DRAWS multiplies both for a longer run by hand.
    /// </remarks>
    public class Spline3SymmetryFuzzTests
    {
        private static readonly int Multiplier = int.TryParse(Environment.GetEnvironmentVariable("SPLINE_FUZZ_DRAWS"), out int asked) && asked > 0 ? asked : 1;

        private readonly ITestOutputHelper _output;

        public Spline3SymmetryFuzzTests(ITestOutputHelper output)
        {
            _output = output;
        }

        private static long Bits(double value) => BitConverter.DoubleToInt64Bits(value);

        private static bool SameBits(GeoPoint3 a, GeoPoint3 b) => Bits(a.X) == Bits(b.X) && Bits(a.Y) == Bits(b.Y) && Bits(a.Z) == Bits(b.Z);

        private static void AssertSamePoints(GeoPoint3[] expected, GeoPoint3[] actual, string what)
        {
            Assert.True(expected.Length == actual.Length, what + ": " + actual.Length + " points and " + expected.Length);

            for (int i = 0; i < expected.Length; i++)
            {
                Assert.True(SameBits(expected[i], actual[i]), what + ": point " + i + " is " + actual[i] + " and " + expected[i]);
            }
        }

        public static TheoryData<string> Kinds()
        {
            var data = new TheoryData<string>();

            foreach (string kind in Spline3MeetShapes.Kinds)
            {
                data.Add(kind);
            }

            return data;
        }

        /// <summary>The kinds that are curves, which the spline measures as a pair of splines: a draw costs a hundred times that of the others.</summary>
        private static readonly string[] Curved = { "arc", "circle", "ellipse", "edge", "chain", "loop" };

        [Theory]
        [MemberData(nameof(Kinds))]
        public void EveryQueryOfASplineAndAShapeInSpace_IsTheSameFromEitherSide_ToTheBit(string kind)
        {
            var random = new Random(65000 + Array.IndexOf(Spline3MeetShapes.Kinds, kind));
            int pairs = 0;
            int touching = 0;
            bool curved = Curved.Contains(kind);
            var fine = new Tolerance(1E-4, 1E-5);
            Tolerance[] tolerances = curved ? new[] { Strict, Tolerance.Default } : new[] { Strict, fine, Tolerance.Global };

            for (int draw = 0; draw < (curved ? 20 : 80) * Multiplier; draw++)
            {
                SplineOracle o = draw % 6 == 5 ? SplineGen.Nasty(random, 3, SplineGen.NastyKinds[random.Next(SplineGen.NastyKinds.Length)]) : SplineGen.Draw(random, 3, maxDegree: 5, thinSpans: draw % 4 == 0);
                GeoSpline3 spline;

                try
                {
                    spline = Spline3MeetFuzzTests.Make(o);
                }
                catch (ArgumentException)
                {
                    continue;
                }

                double[] box = Spline3MeetFuzzTests.BoxOf(o);
                double size = Spline3MeetFuzzTests.Reach(o);

                for (int k = 0; k < 3; k++)
                {
                    Meet3Shape shape = Spline3MeetShapes.Draw(random, kind, 0.5 * (box[0] + box[3]), 0.5 * (box[1] + box[4]), 0.5 * (box[2] + box[5]), size);
                    string label = kind + ", draw " + draw + ", " + o.Degree + "/" + o.Count;

                    foreach (Tolerance tolerance in tolerances)
                    {
                        Assert.Equal(Bits(Distance(spline, shape.Shape, tolerance)), Bits(Distance(shape.Shape, spline, tolerance)));
                        Assert.Equal(Collides(spline, shape.Shape, tolerance), Collides(shape.Shape, spline, tolerance));
                        AssertSamePoints(Crossings(spline, shape.Shape, tolerance), Crossings(shape.Shape, spline, tolerance), label + " crossings");

                        bool tried = Try(spline, shape.Shape, out GeoPoint3[] fromSpline, tolerance);
                        bool triedBack = Try(shape.Shape, spline, out GeoPoint3[] fromShape, tolerance);
                        Assert.Equal(tried, triedBack);
                        AssertSamePoints(fromSpline, fromShape, label + " try");
                        AssertSamePoints(Crossings(spline, shape.Shape, tolerance), fromSpline, label + " try against crossings");

                        GeoLine3 leaving = Shortest(spline, shape.Shape, tolerance);
                        GeoLine3 landing = Shortest(shape.Shape, spline, tolerance);
                        Assert.True(SameBits(leaving.StartPoint, landing.EndPoint) && SameBits(leaving.EndPoint, landing.StartPoint), label + ": the segments are not the same turned round");

                        // A collision is the distance being within the point tolerance, exactly.
                        Assert.Equal(Distance(spline, shape.Shape, tolerance) <= tolerance.EqualPoint, Collides(spline, shape.Shape, tolerance));

                        // The crossings, with no more than one place within the point tolerance of another.
                        GeoPoint3[] places = Crossings(spline, shape.Shape, tolerance);

                        for (int i = 0; i < places.Length; i++)
                        {
                            for (int j = 0; j < i; j++)
                            {
                                Assert.True(places[i].DistanceTo(places[j]) > tolerance.EqualPoint, label + ": two places within the point tolerance of each other");
                            }
                        }
                    }

                    // The forms without a tolerance are the forms with the global one, to the bit.
                    Assert.Equal(Bits(Distance(spline, shape.Shape, Tolerance.Global)), Bits(Distance(spline, shape.Shape)));
                    Assert.Equal(Bits(Distance(shape.Shape, spline, Tolerance.Global)), Bits(Distance(shape.Shape, spline)));
                    AssertSamePoints(Crossings(spline, shape.Shape, Tolerance.Global), Crossings(spline, shape.Shape), "plain crossings");
                    Assert.Equal(Bits(Shortest(spline, shape.Shape, Tolerance.Global).Length), Bits(Shortest(spline, shape.Shape).Length));
                    touching += Collides(spline, shape.Shape) ? 1 : 0;
                    pairs++;
                }
            }

            _output.WriteLine(kind + ": " + pairs + " pairs, " + touching + " touching, each asked from both sides at " + tolerances.Length + " tolerances, all the same to the bit");
        }

        [Fact]
        public void EveryQueryOfTwoSplinesInSpace_IsTheSameFromEitherSide_ToTheBit_AndThePlacesAreInOrderAlongTheOneAsked()
        {
            var random = new Random(65100);
            int pairs = 0;
            int meeting = 0;

            for (int draw = 0; draw < 120 * Multiplier; draw++)
            {
                GeoSpline3 a;
                GeoSpline3 b;

                try
                {
                    SplineOracle first = draw % 5 == 4 ? SplineGen.Nasty(random, 3, SplineGen.NastyKinds[random.Next(SplineGen.NastyKinds.Length)]) : SplineGen.Draw(random, 3, maxDegree: 5, thinSpans: draw % 4 == 0);
                    SplineOracle second = SplineGen.Draw(random, 3, maxDegree: 5);
                    a = Spline3MeetFuzzTests.Make(first);
                    GeoAabb3 box = a.GetAabb();
                    GeoAabb3 other = Spline3MeetFuzzTests.Make(second).GetAabb();
                    GeoVector3 size = box.Min.GetVectorTo(box.Max);
                    b = Spline3MeetFuzzTests.Make(second) + new GeoVector3(
                        box.Center.X - other.Center.X + (random.NextDouble() - 0.5) * 3.0 * size.X,
                        box.Center.Y - other.Center.Y + (random.NextDouble() - 0.5) * 3.0 * size.Y,
                        box.Center.Z - other.Center.Z + (random.NextDouble() - 0.5) * 3.0 * size.Z);
                }
                catch (ArgumentException)
                {
                    continue;
                }

                foreach (Tolerance tolerance in new[] { Strict, Tolerance.Global })
                {
                    Assert.Equal(Bits(a.DistanceTo(b, tolerance)), Bits(b.DistanceTo(a, tolerance)));
                    Assert.Equal(a.CollidesWith(b, tolerance), b.CollidesWith(a, tolerance));
                    Assert.Equal(a.DistanceTo(b, tolerance) <= tolerance.EqualPoint, a.CollidesWith(b, tolerance));

                    GeoPoint3[] along = a.GetIntersections(b, tolerance);
                    GeoPoint3[] back = b.GetIntersections(a, tolerance);
                    Assert.Equal(along.Length, back.Length);

                    // The same places, bit for bit.
                    foreach (GeoPoint3 p in along)
                    {
                        Assert.Contains(back, q => SameBits(p, q));
                    }

                    // Each list is in order along the spline that was asked.
                    double[] knotsA = along.Select(p => a.GetKnotAtPoint(p)).ToArray();
                    double[] knotsB = back.Select(p => b.GetKnotAtPoint(p)).ToArray();

                    for (int i = 1; i < knotsA.Length; i++)
                    {
                        Assert.True(knotsA[i] >= knotsA[i - 1] - 1E-9 * (a.DomainEnd - a.DomainStart), "the places are not in order along the first spline: " + string.Join(" ", knotsA));
                    }

                    for (int i = 1; i < knotsB.Length; i++)
                    {
                        Assert.True(knotsB[i] >= knotsB[i - 1] - 1E-9 * (b.DomainEnd - b.DomainStart), "the places are not in order along the second spline: " + string.Join(" ", knotsB));
                    }

                    bool tried = a.TryIntersectWith(b, out GeoPoint3[] fromA, tolerance);
                    bool triedBack = b.TryIntersectWith(a, out GeoPoint3[] fromB, tolerance);
                    Assert.Equal(along.Length > 0, tried);
                    Assert.Equal(back.Length > 0, triedBack);
                    AssertSamePoints(along, fromA, "try from the first");
                    AssertSamePoints(back, fromB, "try from the second");

                    GeoLine3 line = a.GetShortestLineTo(b, tolerance);
                    GeoLine3 reversed = b.GetShortestLineTo(a, tolerance);
                    Assert.True(SameBits(line.StartPoint, reversed.EndPoint) && SameBits(line.EndPoint, reversed.StartPoint), "the shortest segments are not the same turned round");
                    meeting += along.Length > 0 ? 1 : 0;
                }

                Assert.Equal(Bits(a.DistanceTo(b, Tolerance.Global)), Bits(a.DistanceTo(b)));
                AssertSamePoints(a.GetIntersections(b, Tolerance.Global), a.GetIntersections(b), "plain places");
                Assert.Equal(Bits(a.GetShortestLineTo(b, Tolerance.Global).Length), Bits(a.GetShortestLineTo(b).Length));
                pairs++;
            }

            _output.WriteLine(pairs + " pairs of splines in space, " + meeting + " meeting, all the same to the bit from either side");
        }

        [Fact]
        public void ANullShapeOrSplineInSpace_IsRefusedByEveryMemberFromEitherSide()
        {
            Type[] shapes =
            {
                typeof(GeoPolyline3), typeof(GeoPolygon3), typeof(GeoFace3), typeof(GeoObb3), typeof(GeoSolid3), typeof(GeoPolylineArc3), typeof(GeoPolygonArc3), typeof(GeoSpline3)
            };
            string[] queries = { "DistanceTo", "CollidesWith", "GetIntersections", "TryIntersectWith", "GetShortestLineTo" };
            int refused = 0;

            // The spline asked about a null shape of each class kind.
            GeoSpline3 spline = GeoSpline3.FromControlPoints(2, new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(10, 10, 5), new GeoPoint3(20, 0, 0) });

            foreach (MethodInfo method in typeof(GeoSpline3).GetMethods(BindingFlags.Public | BindingFlags.Instance).Where(m => queries.Contains(m.Name)))
            {
                ParameterInfo[] parameters = method.GetParameters();

                if (parameters.Length == 0 || !shapes.Contains(parameters[0].ParameterType))
                {
                    continue;
                }

                object[] arguments = parameters.Select(p => p.ParameterType == typeof(Tolerance) ? (object)Strict : null).ToArray();
                TargetInvocationException thrown = Assert.Throws<TargetInvocationException>(() => method.Invoke(spline, arguments));
                Assert.IsType<ArgumentNullException>(thrown.InnerException);
                refused++;
            }

            // Each class of shape asked about a null spline.
            var instances = new object[]
            {
                new GeoPolyline3(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0)),
                new GeoPolygon3(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0), new GeoPoint3(0, 10, 0)),
                new GeoFace3(new GeoPolygon3(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0), new GeoPoint3(0, 10, 0))),
                new GeoObb3(new GeoPoint3(0, 0, 0), 10, 10, 10),
                new GeoObb3(new GeoPoint3(0, 0, 0), 10, 10, 10).ToSolid(),
                new GeoPolylineArc3(new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0) }),
                new GeoPolygonArc3(new[] { new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0), new GeoPoint3(0, 10, 0) }),
                new GeoLine3(new GeoPoint3(0, 0, 0), new GeoPoint3(10, 0, 0)),
                new GeoSphere3(new GeoPoint3(0, 0, 0), 5)
            };

            foreach (object instance in instances)
            {
                foreach (MethodInfo method in instance.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance).Where(m => queries.Contains(m.Name)))
                {
                    ParameterInfo[] parameters = method.GetParameters();

                    if (parameters.Length == 0 || parameters[0].ParameterType != typeof(GeoSpline3))
                    {
                        continue;
                    }

                    object[] arguments = parameters.Select(p => p.ParameterType == typeof(Tolerance) ? (object)Strict : null).ToArray();
                    TargetInvocationException thrown = Assert.Throws<TargetInvocationException>(() => method.Invoke(instance, arguments));
                    Assert.IsType<ArgumentNullException>(thrown.InnerException);
                    refused++;
                }
            }

            _output.WriteLine(refused + " members refuse a null with an ArgumentNullException");
            Assert.True(refused >= 80, refused.ToString());
        }
    }
}
