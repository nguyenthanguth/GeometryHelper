using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// The triangle of the plane in the matrix: every question it answers, and every question the other shapes of the
    /// plane answer about it, is the polygon's answer, and a triangle of no width answers as the segment it is.
    /// </summary>
    /// <remarks>
    /// The members are wiring, not arithmetic: each one reads the triangle as the polygon of its corners and asks that.
    /// So the tests hold each to the answer the same question already had through <see cref="GeoTriangle2.ToPolygon()"/>,
    /// over shapes of every kind crossing the triangle, touching it, holding it, and standing clear of it. Found by
    /// reflection, so a member added later is held to the same answer without being listed here.
    /// </remarks>
    public class Triangle2MatrixTests
    {
        private static readonly Tolerance Tolerance = Tolerance.Default;

        private static GeoPoint2 P(double x, double y) => new GeoPoint2(x, y);

        /// <summary>The triangle the rows are asked of: scalene, counter-clockwise.</summary>
        private static readonly GeoTriangle2 Triangle = new GeoTriangle2(P(0, 0), P(100, 10), P(30, 80));

        private static readonly Type[] PlaneTypes =
        {
            typeof(GeoPoint2), typeof(GeoLine2), typeof(GeoPolyline2), typeof(GeoPolygon2), typeof(GeoFace2), typeof(GeoCircle2),
            typeof(GeoArc2), typeof(GeoEdge2), typeof(GeoPolylineArc2), typeof(GeoPolygonArc2), typeof(GeoRectangle2),
        };

        /// <summary>The members that walk the edges, the one group of the row asked of a number rather than a shape.</summary>
        private static readonly string[] Walking =
        {
            "GetPointAtParameter", "GetParameterAtPoint", "GetPointAtDistance", "GetDistanceAtPoint", "GetDistanceAtParameter", "GetParameterAtDistance",
        };

        private static GeoPolygon2 Square(double x, double y, double side) => new GeoPolygon2(P(x, y), P(x + side, y), P(x + side, y + side), P(x, y + side));

        /// <summary>Shapes of one kind: crossing the triangle, touching it, holding it or held by it, and apart from it.</summary>
        private static IEnumerable<object> Samples(Type type)
        {
            if (type == typeof(GeoPoint2))
            {
                return new object[] { P(40, 30), P(50, 5), P(30, 80), P(200, 200), P(-10, 40) };
            }

            if (type == typeof(GeoLine2))
            {
                return new object[] { new GeoLine2(P(-20, 20), P(120, 40)), new GeoLine2(P(30, 30), P(40, 40)), new GeoLine2(P(100, 10), P(150, -20)), new GeoLine2(P(200, 0), P(250, 50)), new GeoLine2(P(0, 0), P(50, 5)) };
            }

            if (type == typeof(GeoPolyline2))
            {
                return new object[] { new GeoPolyline2(P(-10, 50), P(50, 40), P(60, -20)), new GeoPolyline2(P(200, 0), P(220, 30), P(260, 10)) };
            }

            if (type == typeof(GeoPolygon2))
            {
                return new object[] { Square(50, 20, 60), Square(-50, -50, 300), Square(35, 25, 10), Square(300, 300, 40) };
            }

            if (type == typeof(GeoFace2))
            {
                return new object[]
                {
                    new GeoFace2(Square(-50, -50, 300), new[] { Square(20, 20, 30) }),
                    new GeoFace2(Square(-50, -50, 300), new[] { Square(-40, -40, 250) }),
                    new GeoFace2(Square(80, 50, 60)),
                    new GeoFace2(Square(300, 300, 40)),
                };
            }

            if (type == typeof(GeoCircle2))
            {
                return new object[] { new GeoCircle2(P(100, 50), 30), new GeoCircle2(P(40, 30), 5), new GeoCircle2(P(40, 30), 500), new GeoCircle2(P(300, 0), 10) };
            }

            if (type == typeof(GeoArc2))
            {
                return new object[] { new GeoArc2(P(100, 50), 30, 0.0, Math.PI), new GeoArc2(P(40, 30), 5, 0.0, 1.0), new GeoArc2(P(300, 0), 10, 0.5, 2.5) };
            }

            if (type == typeof(GeoEdge2))
            {
                return new object[] { new GeoEdge2(P(-20, 20), P(120, 40)), new GeoEdge2(P(-20, 20), P(120, 40), 0.4), new GeoEdge2(P(200, 0), P(250, 50), -0.3) };
            }

            if (type == typeof(GeoPolylineArc2))
            {
                return new object[]
                {
                    new GeoPolylineArc2(new[] { P(-10, 50), P(50, 40), P(60, -20) }, new[] { 0.3, -0.2, 0.0 }),
                    new GeoPolylineArc2(new[] { P(200, 0), P(220, 30), P(260, 10) }, new[] { 0.5, 0.0, 0.0 }),
                };
            }

            if (type == typeof(GeoPolygonArc2))
            {
                return new object[] { Square(50, 20, 60).Fillet(10), Square(-50, -50, 300).Fillet(40), Square(300, 300, 40).Fillet(5) };
            }

            if (type == typeof(GeoRectangle2))
            {
                return new object[] { new GeoRectangle2(P(80, 40), 60, 30, 0.3), new GeoRectangle2(P(40, 30), 4, 4), new GeoRectangle2(P(50, 30), 400, 300), new GeoRectangle2(P(300, 300), 20, 10) };
            }

            if (type == typeof(GeoTriangle2))
            {
                return new object[]
                {
                    Triangle,
                    new GeoTriangle2(P(50, 0), P(150, 60), P(60, 70)),
                    new GeoTriangle2(P(30, 20), P(50, 20), P(40, 35)),
                    new GeoTriangle2(P(300, 0), P(320, 0), P(310, 20)),
                    new GeoTriangle2(P(100, 10), P(160, 10), P(130, -40)),
                };
            }

            return null;
        }

        private static bool IsShape(Type type) => type == typeof(GeoTriangle2) || PlaneTypes.Contains(type);

        /// <summary>Every way of calling a method: each sample of each parameter, the tolerance fixed, outs left empty.</summary>
        private static List<object[]> Arguments(ParameterInfo[] parameters)
        {
            List<object[]> calls = new List<object[]> { new object[0] };

            foreach (ParameterInfo parameter in parameters)
            {
                Type type = parameter.ParameterType;
                IEnumerable<object> values;

                if (type.IsByRef)
                {
                    values = new object[] { null };
                }
                else if (type == typeof(Tolerance))
                {
                    values = new object[] { Tolerance };
                }
                else if (type == typeof(LineEnd))
                {
                    values = new object[] { LineEnd.Start, LineEnd.End };
                }
                else if (type == typeof(double))
                {
                    values = parameter.Name.Contains("istance") ? new object[] { 0.0, 57.5, 300.0, -20.0 } : new object[] { 0.0, 0.3, 1.25, -0.2 };
                }
                else
                {
                    values = Samples(type);
                }

                if (values == null)
                {
                    return null;
                }

                List<object> each = values.ToList();
                calls = calls.SelectMany(call => each.Select(value => call.Concat(new[] { value }).ToArray())).ToList();
            }

            return calls;
        }

        private static (object Result, object[] Arguments, Type Thrown) Call(MethodInfo method, object target, object[] arguments)
        {
            object[] copy = (object[])arguments.Clone();

            try
            {
                return (method.Invoke(target, copy), copy, null);
            }
            catch (TargetInvocationException e)
            {
                return (null, copy, e.InnerException.GetType());
            }
        }

        private static string Describe(object target, MethodInfo method, object[] arguments)
            => $"{target}.{method.Name}({string.Join(", ", arguments.Select(x => x?.ToString() ?? "out"))})";

        private static void AssertSame(string what, object expected, object actual, bool lengthOnly)
        {
            switch (expected)
            {
                case null:
                    Assert.True(actual == null, what + ": expected nothing");
                    return;
                case double e:
                    double a = (double)actual;
                    Assert.True(double.IsNaN(e) ? double.IsNaN(a) : Math.Abs(e - a) <= 1E-9 * Math.Max(1.0, Math.Abs(e)), $"{what}: {e:R} against {a:R}");
                    return;
                case GeoPoint2 point:
                    Assert.True(point.IsEqualTo((GeoPoint2)actual, new Tolerance(1E-9, 1E-9)), $"{what}: {point} against {actual}");
                    return;
                case GeoPoint2[] points:
                    var others = (GeoPoint2[])actual;
                    Assert.True(points.Length == others.Length, $"{what}: {points.Length} points against {others.Length}");
                    Assert.True(points.All(p => others.Any(o => o.IsEqualTo(p, new Tolerance(1E-6, 1E-6)))), what + ": the points differ");
                    return;
                case GeoLine2 line:
                    var other = (GeoLine2)actual;
                    if (lengthOnly)
                    {
                        Assert.True(Math.Abs(line.Length - other.Length) <= 1E-9 * Math.Max(1.0, line.Length), $"{what}: length {line.Length:R} against {other.Length:R}");
                    }
                    else
                    {
                        Assert.True(line.StartPoint.IsEqualTo(other.StartPoint, new Tolerance(1E-9, 1E-9)) && line.EndPoint.IsEqualTo(other.EndPoint, new Tolerance(1E-9, 1E-9)), $"{what}: {line} against {other}");
                    }

                    return;
                default:
                    Assert.True(expected.Equals(actual), $"{what}: {expected} against {actual}");
                    return;
            }
        }

        /// <summary>
        /// Asks one member and its twin the same questions, the twin's arguments mapped, and holds the answers, outs and
        /// what is thrown to each other. Returns how many calls were compared.
        /// </summary>
        private static int Compare(MethodInfo method, object target, MethodInfo twin, object twinTarget, Func<object[], object[]> twinArguments, bool lengthOnly)
        {
            ParameterInfo[] parameters = method.GetParameters();
            List<object[]> calls = Arguments(parameters);

            if (calls == null)
            {
                return 0;
            }

            int[] outs = Enumerable.Range(0, parameters.Length).Where(i => parameters[i].ParameterType.IsByRef).ToArray();
            ParameterInfo[] twinParameters = twin.GetParameters();
            int[] twinOuts = Enumerable.Range(0, twinParameters.Length).Where(i => twinParameters[i].ParameterType.IsByRef).ToArray();
            int compared = 0;

            foreach (object[] arguments in calls)
            {
                var (expected, expectedArgs, expectedThrown) = Call(twin, twinTarget, twinArguments(arguments));
                var (actual, actualArgs, actualThrown) = Call(method, target, arguments);
                string what = Describe(target, method, arguments);

                Assert.True(expectedThrown == actualThrown, $"{what}: threw {actualThrown?.Name ?? "nothing"} where the twin threw {expectedThrown?.Name ?? "nothing"}");

                if (expectedThrown == null)
                {
                    AssertSame(what, expected, actual, lengthOnly);

                    for (int k = 0; k < outs.Length; k++)
                    {
                        AssertSame(what + " out " + parameters[outs[k]].Name, expectedArgs[twinOuts[k]], actualArgs[outs[k]], lengthOnly);
                    }
                }

                compared++;
            }

            return compared;
        }

        /// <summary>The questions of the matrix, as against the triangle's own measures, moves and comparisons.</summary>
        private static readonly string[] Matrix =
        {
            "CollidesWith", "Contains", "Locate", "DistanceTo", "SignedDistanceTo", "GetIntersections", "TryIntersectWith",
            "GetClosestPointOnBoundary", "GetShortestLineTo", "GetClosestEdge", "IsParallelTo",
        };

        /// <summary>
        /// Holds a try that lists the crossings to the list the twin gives: true just when it is not empty, and the same
        /// points. Returns how many calls were compared.
        /// </summary>
        private static int CompareTry(MethodInfo method, object target, Func<object[], GeoPoint2[]> listed)
        {
            ParameterInfo[] parameters = method.GetParameters();
            int at = Array.FindIndex(parameters, p => p.ParameterType.IsByRef);
            int compared = 0;

            foreach (object[] arguments in Arguments(parameters))
            {
                GeoPoint2[] expected = listed(arguments.Where((x, i) => i != at).ToArray());
                var (actual, actualArgs, thrown) = Call(method, target, arguments);
                string what = Describe(target, method, arguments);

                Assert.True(thrown == null, $"{what} threw {thrown?.Name}");
                Assert.True((bool)actual == expected.Length > 0, $"{what}: {actual} with {expected.Length} crossings listed");
                AssertSame(what + " out", expected, actualArgs[at], false);
                compared++;
            }

            return compared;
        }

        private static IEnumerable<MethodInfo> RowMembers()
            => typeof(GeoTriangle2).GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => m.DeclaringType == typeof(GeoTriangle2))
                .Where(m => (Matrix.Contains(m.Name) && m.GetParameters().Any(p => IsShape(p.ParameterType))) || Walking.Contains(m.Name));

        [Fact]
        public void EveryRowAnswerIsThePolygonsAnswer()
        {
            GeoPolygon2 polygon = Triangle.ToPolygon();
            int compared = 0, members = 0;
            List<string> unmatched = new List<string>();

            foreach (MethodInfo method in RowMembers())
            {
                Type[] types = method.GetParameters().Select(p => p.ParameterType).ToArray();
                MethodInfo twin = typeof(GeoPolygon2).GetMethod(method.Name, types);

                if (twin != null && twin.ReturnType == method.ReturnType)
                {
                    // A triangle's shortest segment to another is found from its side, a polygon's to a triangle from the
                    // triangle's and turned round: the same length, though where two segments tie either may come back.
                    compared += Compare(method, Triangle, twin, polygon, x => x, method.Name == "GetShortestLineTo");
                    members++;
                    continue;
                }

                // Where the polygon only lists the crossings, the triangle's try is whether there are any, and which.
                MethodInfo listed = method.Name == "TryIntersectWith" ? typeof(GeoPolygon2).GetMethod("GetIntersections", types.Where(t => !t.IsByRef).ToArray()) : null;

                if (listed != null)
                {
                    compared += CompareTry(method, Triangle, args => (GeoPoint2[])listed.Invoke(polygon, args));
                    members++;
                    continue;
                }

                // Where the polygon asks it of the core only, the core is the twin.
                MethodInfo core = typeof(Containment2).GetMethod(method.Name, new[] { typeof(GeoPolygon2) }.Concat(types).ToArray());

                if (core != null && core.ReturnType == method.ReturnType)
                {
                    compared += Compare(method, Triangle, core, null, args => new object[] { polygon }.Concat(args).ToArray(), false);
                    members++;
                    continue;
                }

                unmatched.Add(method.ToString());
            }

            // What the polygon does not answer the triangle answers on its own, and has its own tests below.
            Assert.True(unmatched.All(m => m.Contains("IsParallelTo") || m.Contains("GetClosestEdge")), "no polygon twin: " + string.Join("; ", unmatched));
            Assert.True(members >= 115, $"only {members} members compared");
            Assert.True(compared >= 440, $"only {compared} calls compared");
        }

        [Fact]
        public void EveryColumnAnswerIsTheAnswerAboutThePolygon()
        {
            int compared = 0, members = 0;

            foreach (Type receiver in PlaneTypes)
            {
                foreach (MethodInfo method in receiver.GetMethods(BindingFlags.Public | BindingFlags.Instance).Where(m => m.GetParameters().Any(p => p.ParameterType == typeof(GeoTriangle2))))
                {
                    ParameterInfo[] parameters = method.GetParameters();
                    int at = Array.FindIndex(parameters, p => p.ParameterType == typeof(GeoTriangle2));
                    Type[] polygonTypes = parameters.Select(p => p.ParameterType == typeof(GeoTriangle2) ? typeof(GeoPolygon2) : p.ParameterType).ToArray();
                    MethodInfo twin = receiver.GetMethod(method.Name, polygonTypes);
                    bool lengthOnly = method.Name == "GetShortestLineTo";

                    foreach (object sample in Samples(receiver))
                    {
                        if (twin != null)
                        {
                            compared += Compare(method, sample, twin, sample, args => args.Select(x => x is GeoTriangle2 t ? (object)t.ToPolygon() : x).ToArray(), lengthOnly);
                            continue;
                        }

                        // No polygon asks it: a question asked both ways round, the triangle's own row member is the twin.
                        Type[] mirrorTypes = parameters.Select((p, i) => i == at ? receiver : p.ParameterType).ToArray();
                        MethodInfo mirror = typeof(GeoTriangle2).GetMethod(method.Name, mirrorTypes);
                        Assert.True(mirror != null, $"{receiver.Name}.{method.Name} has a triangle overload and neither a polygon one nor the triangle's own");

                        foreach (object[] arguments in Arguments(parameters))
                        {
                            var triangle = (GeoTriangle2)arguments[at];
                            object[] mirrored = arguments.Select((x, i) => i == at ? sample : x).ToArray();
                            var (expected, _, expectedThrown) = Call(mirror, triangle, mirrored);
                            var (actual, _, actualThrown) = Call(method, sample, arguments);
                            string what = Describe(sample, method, arguments);

                            Assert.True(expectedThrown == actualThrown, $"{what}: threw {actualThrown?.Name ?? "nothing"} where the triangle threw {expectedThrown?.Name ?? "nothing"}");
                            AssertSame(what, expected, actual, lengthOnly);
                            compared++;
                        }
                    }

                    members++;
                }
            }

            Assert.True(members >= 90, $"only {members} members compared");
            Assert.True(compared >= 1500, $"only {compared} calls compared");
        }

        [Fact]
        public void ATriangleOfNoWidthAnswersAsTheSegmentItIs()
        {
            // Two corners on each other: the triangle is the segment from them to the third.
            var pinched = new GeoTriangle2(P(0, 0), P(0, 0), P(100, 50));
            var hull = new GeoLine2(P(0, 0), P(100, 50));
            int compared = 0;
            List<string> unmatched = new List<string>();

            // Walking a pinched triangle goes along the segment and back, which the segment's own walk does not.
            foreach (MethodInfo method in RowMembers().Where(m => !Walking.Contains(m.Name)))
            {
                MethodInfo twin = typeof(GeoLine2).GetMethod(method.Name, method.GetParameters().Select(p => p.ParameterType).ToArray());

                if (twin == null || twin.ReturnType != method.ReturnType)
                {
                    unmatched.Add(method.ToString());
                    continue;
                }

                compared += Compare(method, pinched, twin, hull, x => x, method.Name == "GetShortestLineTo");
            }

            Assert.True(compared >= 300, $"only {compared} calls compared; no segment twin: {string.Join("; ", unmatched)}");
        }

        [Fact]
        public void ATriangleOfOnePointAnswersEverythingWithoutThrowing()
        {
            var point = new GeoTriangle2(P(40, 30), P(40, 30), P(40, 30));
            int called = 0;

            foreach (MethodInfo method in RowMembers())
            {
                foreach (object[] arguments in Arguments(method.GetParameters()))
                {
                    var (_, _, thrown) = Call(method, point, arguments);
                    Assert.True(thrown == null, $"{Describe(point, method, arguments)} threw {thrown?.Name}");
                    called++;
                }
            }

            Assert.True(called >= 450, $"only {called} calls made");
        }

        [Fact]
        public void EveryTryIsWhetherTheListIsEmpty()
        {
            var shapes = new[] { Triangle, new GeoTriangle2(P(0, 0), P(0, 0), P(100, 50)), new GeoTriangle2(P(40, 30), P(40, 30), P(40, 30)) };
            int compared = 0;

            foreach (MethodInfo method in RowMembers().Where(m => m.Name == "TryIntersectWith"))
            {
                MethodInfo listed = typeof(GeoTriangle2).GetMethod("GetIntersections", method.GetParameters().Select(p => p.ParameterType).Where(t => !t.IsByRef).ToArray());
                Assert.True(listed != null, method + " has no list to try");

                foreach (GeoTriangle2 shape in shapes)
                {
                    compared += CompareTry(method, shape, args => (GeoPoint2[])listed.Invoke(shape, args));
                }
            }

            Assert.True(compared >= 230, $"only {compared} calls compared");
        }

        [Fact]
        public void APinchedTriangleMeetsWhatItsSegmentMeets()
        {
            var pinched = new GeoTriangle2(P(0, 0), P(0, 0), P(100, 50));

            GeoPoint2[] crossing = pinched.GetIntersections(new GeoLine2(P(50, 0), P(50, 100)), Tolerance);
            Assert.Single(crossing);
            Assert.True(crossing[0].IsEqualTo(P(50, 25), new Tolerance(1E-9, 1E-9)));
            Assert.Empty(pinched.GetIntersections(new GeoLine2(P(150, 0), P(150, 100)), Tolerance));

            Assert.True(pinched.Contains(P(50, 25), Tolerance));
            Assert.False(pinched.Contains(P(50, 30), Tolerance));
            Assert.Equal(PointLocation.OnSide, pinched.Locate(P(50, 25), Tolerance));

            // Of no width, it has no inside to be negative within.
            Assert.Equal(0.0, pinched.SignedDistanceTo(P(50, 25), Tolerance), 12);
            Assert.Equal(new GeoLine2(P(0, 0), P(100, 50)).DistanceTo(P(50, 30)), pinched.SignedDistanceTo(P(50, 30), Tolerance), 12);
        }

        [Fact]
        public void ATriangleOfOnePointIsWalkedWithoutLeavingIt()
        {
            var point = new GeoTriangle2(P(40, 30), P(40, 30), P(40, 30));

            Assert.Equal(P(40, 30), point.GetPointAtParameter(0.7));
            Assert.Equal(P(40, 30), point.GetPointAtDistance(12));
            Assert.Equal(0.0, point.GetParameterAtPoint(P(90, 30)));
            Assert.Equal(0.0, point.GetDistanceAtPoint(P(90, 30)));
            Assert.Equal(0.0, point.GetParameterAtDistance(12));
            Assert.Equal(0.0, point.GetDistanceAtParameter(0.7));
        }

        [Fact]
        public void APinchedTriangleIsWalkedAlongItsSegmentAndBack()
        {
            // 0 to 100 and back: 200 round, the far corner halfway.
            var pinched = new GeoTriangle2(P(0, 0), P(0, 0), P(100, 0));

            Assert.True(pinched.GetPointAtParameter(0.5).IsEqualTo(P(100, 0), new Tolerance(1E-12, 1E-12)));
            Assert.True(pinched.GetPointAtParameter(0.75).IsEqualTo(P(50, 0), new Tolerance(1E-12, 1E-12)));
            Assert.True(pinched.GetPointAtParameter(1.25).IsEqualTo(P(50, 0), new Tolerance(1E-12, 1E-12)));
            Assert.True(pinched.GetPointAtDistance(250).IsEqualTo(P(50, 0), new Tolerance(1E-12, 1E-12)));
            Assert.Equal(0.25, pinched.GetParameterAtPoint(P(50, 7)), 12);
            Assert.Equal(400.0, pinched.GetDistanceAtParameter(2.0), 12);
        }

        [Fact]
        public void AnEdgeRunsParallelToASegmentAlongIt()
        {
            var t = new GeoTriangle2(P(0, 0), P(100, 0), P(0, 50));

            Assert.True(t.IsParallelTo(new GeoLine2(P(10, 20), P(60, 20)), Tolerance));
            Assert.True(t.IsParallelTo(new GeoLine2(P(0, 0), P(-100, 50)), Tolerance));
            Assert.False(t.IsParallelTo(new GeoLine2(P(0, 0), P(10, 10)), Tolerance));
            Assert.True(new GeoLine2(P(10, 20), P(60, 20)).IsParallelTo(t, Tolerance));

            // An edge of no length has no direction.
            var pinched = new GeoTriangle2(P(0, 0), P(0, 0), P(100, 0));
            Assert.False(pinched.IsParallelTo(new GeoLine2(P(0, 5), P(0, 9)), Tolerance));
            Assert.True(pinched.IsParallelTo(new GeoLine2(P(0, 5), P(9, 5)), Tolerance));
        }

        [Fact]
        public void ItHoldsWhatLiesWithinIt()
        {
            Assert.True(Triangle.Contains(new GeoLine2(P(30, 20), P(40, 30)), Tolerance));
            Assert.False(Triangle.Contains(new GeoLine2(P(30, 20), P(140, 30)), Tolerance));
            Assert.True(Triangle.Contains(new GeoPolyline2(P(30, 20), P(40, 30), P(35, 40)), Tolerance));
            Assert.Equal(Containment2.Contains(Triangle.ToPolygon(), new GeoLine2(P(30, 20), P(40, 30)), Tolerance), Triangle.Contains(new GeoLine2(P(30, 20), P(40, 30)), Tolerance));

            var pinched = new GeoTriangle2(P(0, 0), P(0, 0), P(100, 0));
            Assert.True(pinched.Contains(new GeoLine2(P(10, 0), P(60, 0)), Tolerance));
            Assert.False(pinched.Contains(new GeoLine2(P(10, 0), P(160, 0)), Tolerance));
            Assert.False(pinched.Contains(new GeoLine2(P(10, 0), P(10, 5)), Tolerance));
        }

        [Fact]
        public void ItIsWalkedFromAAroundItsEdges()
        {
            // A 3-4-5 triangle: 12 round, so halfway is 4 along A to B and 2 along B to C.
            var t = new GeoTriangle2(P(0, 0), P(4, 0), P(0, 3));

            Assert.True(t.GetPointAtParameter(0.5).IsEqualTo(P(2.4, 1.2), new Tolerance(1E-12, 1E-12)));
            Assert.True(t.GetPointAtParameter(1.5).IsEqualTo(P(2.4, 1.2), new Tolerance(1E-12, 1E-12)));
            Assert.Equal(0.5, t.GetParameterAtPoint(P(2.4, 1.2)), 12);
            Assert.True(t.GetPointAtDistance(6).IsEqualTo(P(2.4, 1.2), new Tolerance(1E-12, 1E-12)));
            Assert.True(t.GetPointAtDistance(18).IsEqualTo(P(2.4, 1.2), new Tolerance(1E-12, 1E-12)));
            Assert.Equal(6.0, t.GetDistanceAtParameter(0.5), 12);
            Assert.Equal(0.5, t.GetParameterAtDistance(6), 12);
            Assert.Equal(6.0, t.GetDistanceAtPoint(P(2.4, 1.2)), 12);
        }

        [Fact]
        public void APointWithinIsANegativeDistanceFromIt()
        {
            Assert.Equal(0.0, Triangle.DistanceTo(P(40, 30)), 12);
            Assert.True(Triangle.SignedDistanceTo(P(40, 30), Tolerance) < 0.0);
            Assert.Equal(Triangle.GetClosestPointOnBoundary(P(40, 30)).DistanceTo(P(40, 30)), -Triangle.SignedDistanceTo(P(40, 30), Tolerance), 9);
            Assert.True(Triangle.SignedDistanceTo(P(200, 200), Tolerance) > 0.0);
            Assert.Equal(new GeoLine2(P(0, 0), P(100, 10)), Triangle.GetClosestEdge(P(50, 2)));
        }

        [Fact]
        public void TwoTrianglesApartAreTheGapBetweenThemApart()
        {
            var left = new GeoTriangle2(P(0, 0), P(10, 0), P(0, 10));
            var right = new GeoTriangle2(P(20, 0), P(30, 0), P(30, 10));

            Assert.False(left.CollidesWith(right, Tolerance));
            Assert.Equal(10.0, left.DistanceTo(right), 9);
            Assert.Equal(10.0, left.GetShortestLineTo(right, Tolerance).Length, 9);
            Assert.Empty(left.GetIntersections(right, Tolerance));

            GeoTriangle2 touching = right.Translate(new GeoVector2(-10, 0));
            Assert.True(left.CollidesWith(touching, Tolerance));
            Assert.True(left.TryIntersectWith(touching, out GeoPoint2[] meet, Tolerance));
            Assert.NotEmpty(meet);
        }
    }
}
