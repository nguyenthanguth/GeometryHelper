using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GeometryHelper;
using GeometryHelper.Geometry;
using Xunit;
using Xunit.Abstractions;
using static GeometryHelper.UnitTest.Plane.XLine2Probe;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// A spline and a shape asked the five queries from either side must give the same answer to the bit, whatever the draw: the
    /// shape's member hands the question to the spline's, and the answer is turned round where it has a direction. Splines are asked about
    /// each other, which is the one place it is not a hand-over, since both are splines: the work is done from the one that comes
    /// first, and the places they cross are given in order along the one asked.
    /// </summary>
    public class Spline2SymmetryFuzzTests
    {
        private static readonly int Multiplier = int.TryParse(Environment.GetEnvironmentVariable("SPLINE_FUZZ_DRAWS"), out int asked) && asked > 0 ? asked : 1;

        private readonly ITestOutputHelper _output;

        public Spline2SymmetryFuzzTests(ITestOutputHelper output)
        {
            _output = output;
        }

        private static long Bits(double value) => BitConverter.DoubleToInt64Bits(value);

        private static bool SameBits(GeoPoint2 a, GeoPoint2 b) => Bits(a.X) == Bits(b.X) && Bits(a.Y) == Bits(b.Y);

        private static void AssertSamePoints(GeoPoint2[] expected, GeoPoint2[] actual, string what)
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

            foreach (string kind in Spline2MeetShapes.Kinds)
            {
                data.Add(kind);
            }

            return data;
        }

        [Theory]
        [MemberData(nameof(Kinds))]
        public void EveryQueryOfASplineAndAShape_IsTheSameFromEitherSide_ToTheBit(string kind)
        {
            var random = new Random(55000 + Array.IndexOf(Spline2MeetShapes.Kinds, kind));
            int pairs = 0;
            int touching = 0;

            for (int draw = 0; draw < 120 * Multiplier; draw++)
            {
                SplineOracle o = draw % 6 == 5 ? SplineGen.Nasty(random, 2, SplineGen.NastyKinds[random.Next(SplineGen.NastyKinds.Length)]) : SplineGen.Draw(random, 2, maxDegree: 5, thinSpans: draw % 4 == 0);
                GeoSpline2 spline;

                try
                {
                    spline = Spline2MeetFuzzTests.Make(o);
                }
                catch (ArgumentException)
                {
                    continue;
                }

                double[] box = { double.MaxValue, double.MaxValue, double.MinValue, double.MinValue };

                for (int i = 0; i < o.Count; i++)
                {
                    box[0] = Math.Min(box[0], o.Points[2 * i]);
                    box[1] = Math.Min(box[1], o.Points[2 * i + 1]);
                    box[2] = Math.Max(box[2], o.Points[2 * i]);
                    box[3] = Math.Max(box[3], o.Points[2 * i + 1]);
                }

                double size = Math.Max(1.0, Math.Max(box[2] - box[0], box[3] - box[1]));

                for (int k = 0; k < 3; k++)
                {
                    MeetShape shape = Spline2MeetShapes.Draw(random, kind, 0.5 * (box[0] + box[2]), 0.5 * (box[1] + box[3]), size);
                    string label = kind + ", draw " + draw + ", " + o.Degree + "/" + o.Count;
                    var fine = new Tolerance(1E-4, 1E-5);

                    foreach (Tolerance tolerance in new[] { Strict, fine, Tolerance.Global })
                    {
                        Assert.Equal(Bits(Distance(spline, shape.Shape, tolerance)), Bits(Distance(shape.Shape, spline, tolerance)));
                        Assert.Equal(Collides(spline, shape.Shape, tolerance), Collides(shape.Shape, spline, tolerance));
                        AssertSamePoints(Crossings(spline, shape.Shape, tolerance), Crossings(shape.Shape, spline, tolerance), label + " crossings");

                        bool tried = Try(spline, shape.Shape, out GeoPoint2[] fromSpline, tolerance);
                        bool triedBack = Try(shape.Shape, spline, out GeoPoint2[] fromShape, tolerance);
                        Assert.Equal(tried, triedBack);
                        AssertSamePoints(fromSpline, fromShape, label + " try");
                        AssertSamePoints(Crossings(spline, shape.Shape, tolerance), fromSpline, label + " try against crossings");

                        GeoLine2 leaving = Shortest(spline, shape.Shape, tolerance);
                        GeoLine2 landing = Shortest(shape.Shape, spline, tolerance);
                        Assert.True(SameBits(leaving.StartPoint, landing.EndPoint) && SameBits(leaving.EndPoint, landing.StartPoint), label + ": the segments are not the same turned round");

                        // A collision is the distance being within the point tolerance, exactly.
                        Assert.Equal(Distance(spline, shape.Shape, tolerance) <= tolerance.EqualPoint, Collides(spline, shape.Shape, tolerance));

                        // The crossings, with no more than one place within the point tolerance of another.
                        GeoPoint2[] places = Crossings(spline, shape.Shape, tolerance);

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

            _output.WriteLine(kind + ": " + pairs + " pairs, " + touching + " touching, each asked from both sides at three tolerances, all the same to the bit");
        }

        [Fact]
        public void EveryQueryOfTwoSplines_IsTheSameFromEitherSide_ToTheBit_AndTheCrossingsAreInOrderAlongTheOneAsked()
        {
            var random = new Random(55100);
            int pairs = 0;
            int crossing = 0;

            for (int draw = 0; draw < 200 * Multiplier; draw++)
            {
                GeoSpline2 a;
                GeoSpline2 b;

                try
                {
                    SplineOracle first = draw % 5 == 4 ? SplineGen.Nasty(random, 2, SplineGen.NastyKinds[random.Next(SplineGen.NastyKinds.Length)]) : SplineGen.Draw(random, 2, maxDegree: 5, thinSpans: draw % 4 == 0);
                    SplineOracle second = SplineGen.Draw(random, 2, maxDegree: 5);
                    a = Spline2MeetFuzzTests.Make(first);
                    GeoRectangle2 box = a.GetAabb();
                    GeoRectangle2 other = Spline2MeetFuzzTests.Make(second).GetAabb();
                    b = Spline2MeetFuzzTests.Make(second) + new GeoVector2(box.Center.X - other.Center.X + (random.NextDouble() - 0.5) * 3.0 * box.Width, box.Center.Y - other.Center.Y + (random.NextDouble() - 0.5) * 3.0 * box.Height);
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

                    GeoPoint2[] along = a.GetIntersections(b, tolerance);
                    GeoPoint2[] back = b.GetIntersections(a, tolerance);
                    Assert.Equal(along.Length, back.Length);

                    // The same places, bit for bit.
                    foreach (GeoPoint2 p in along)
                    {
                        Assert.Contains(back, q => SameBits(p, q));
                    }

                    // Each list is in order along the spline that was asked.
                    double[] knotsA = along.Select(p => a.GetKnotAtPoint(p)).ToArray();
                    double[] knotsB = back.Select(p => b.GetKnotAtPoint(p)).ToArray();

                    for (int i = 1; i < knotsA.Length; i++)
                    {
                        Assert.True(knotsA[i] >= knotsA[i - 1] - 1E-9 * (a.DomainEnd - a.DomainStart), "the crossings are not in order along the first spline: " + string.Join(" ", knotsA));
                    }

                    for (int i = 1; i < knotsB.Length; i++)
                    {
                        Assert.True(knotsB[i] >= knotsB[i - 1] - 1E-9 * (b.DomainEnd - b.DomainStart), "the crossings are not in order along the second spline: " + string.Join(" ", knotsB));
                    }

                    bool tried = a.TryIntersectWith(b, out GeoPoint2[] fromA, tolerance);
                    bool triedBack = b.TryIntersectWith(a, out GeoPoint2[] fromB, tolerance);
                    Assert.Equal(along.Length > 0, tried);
                    Assert.Equal(back.Length > 0, triedBack);
                    AssertSamePoints(along, fromA, "try from the first");
                    AssertSamePoints(back, fromB, "try from the second");

                    GeoLine2 line = a.GetShortestLineTo(b, tolerance);
                    GeoLine2 reversed = b.GetShortestLineTo(a, tolerance);
                    Assert.True(SameBits(line.StartPoint, reversed.EndPoint) && SameBits(line.EndPoint, reversed.StartPoint), "the shortest segments are not the same turned round");
                    crossing += along.Length > 0 ? 1 : 0;
                }

                Assert.Equal(Bits(a.DistanceTo(b, Tolerance.Global)), Bits(a.DistanceTo(b)));
                AssertSamePoints(a.GetIntersections(b, Tolerance.Global), a.GetIntersections(b), "plain crossings");
                Assert.Equal(Bits(a.GetShortestLineTo(b, Tolerance.Global).Length), Bits(a.GetShortestLineTo(b).Length));
                pairs++;
            }

            _output.WriteLine(pairs + " pairs of splines, " + crossing + " crossing, all the same to the bit from either side");
        }

        [Fact]
        public void ANullShapeOrSpline_IsRefusedByEveryMemberFromEitherSide()
        {
            Type[] shapes =
            {
                typeof(GeoPolyline2), typeof(GeoPolygon2), typeof(GeoFace2), typeof(GeoPolylineArc2), typeof(GeoPolygonArc2), typeof(GeoSpline2)
            };
            string[] queries = { "DistanceTo", "CollidesWith", "GetIntersections", "TryIntersectWith", "GetShortestLineTo" };
            int refused = 0;

            // The spline asked about a null shape of each class kind.
            GeoSpline2 spline = GeoSpline2.FromControlPoints(2, new[] { new GeoPoint2(0, 0), new GeoPoint2(10, 10), new GeoPoint2(20, 0) });

            foreach (MethodInfo method in typeof(GeoSpline2).GetMethods(BindingFlags.Public | BindingFlags.Instance).Where(m => queries.Contains(m.Name)))
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
                new GeoPolyline2(new GeoPoint2(0, 0), new GeoPoint2(10, 0)),
                new GeoPolygon2(new GeoPoint2(0, 0), new GeoPoint2(10, 0), new GeoPoint2(0, 10)),
                new GeoFace2(new GeoPolygon2(new GeoPoint2(0, 0), new GeoPoint2(10, 0), new GeoPoint2(0, 10))),
                new GeoPolylineArc2(new[] { new GeoPoint2(0, 0), new GeoPoint2(10, 0) }),
                new GeoPolygonArc2(new[] { new GeoPoint2(0, 0), new GeoPoint2(10, 0), new GeoPoint2(0, 10) }),
                new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(10, 0)),
                new GeoCircle2(new GeoPoint2(0, 0), 5)
            };

            foreach (object instance in instances)
            {
                foreach (MethodInfo method in instance.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance).Where(m => queries.Contains(m.Name)))
                {
                    ParameterInfo[] parameters = method.GetParameters();

                    if (parameters.Length == 0 || parameters[0].ParameterType != typeof(GeoSpline2))
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
            Assert.True(refused >= 60, refused.ToString());
        }
    }
}
