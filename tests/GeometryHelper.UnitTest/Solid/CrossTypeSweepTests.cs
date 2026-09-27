using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GeometryHelper;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// Every pair of shape types, asked through whatever the library offers for that pair, on random shapes
    /// from a fixed seed: the review sweep that found the disc, the nested arcs and the concave polygon, kept
    /// so that what it found stays found.
    /// </summary>
    /// <remarks>
    /// <para>
    /// For every pair of types <c>a.DistanceTo(b)</c>, <c>a.CollidesWith(b)</c> and <c>a.GetShortestLineTo(b)</c>
    /// are looked up by reflection, so a pair the library does not offer is simply not asked. What is checked is
    /// what must hold whatever the shapes are:
    /// </para>
    /// <list type="bullet">
    /// <item>a distance is a number, nought or more, and the same asked from either side;</item>
    /// <item>two shapes collide exactly when they are nought apart, and say so from either side;</item>
    /// <item>apart, the shortest line is as long as the distance and runs from the one to the other;</item>
    /// <item>turned about any axis or carried a million away, the distance stays and so does the collision;</item>
    /// <item>a shape wholly inside a closed one is nought from it and touches it, from either side;</item>
    /// <item>nothing throws.</item>
    /// </list>
    /// </remarks>
    public class CrossTypeSweepTests
    {
        private static readonly object Missing = new object();

        private sealed class Sweeper
        {
            private readonly Random _random;
            private readonly List<string> _findings = new List<string>();

            public Sweeper(int seed)
            {
                _random = new Random(seed);
            }

            public int Checks { get; private set; }

            public IReadOnlyList<string> Findings => _findings;

            private double U(double low, double high) => low + (high - low) * _random.NextDouble();

            #region Plane shapes

            private GeoPoint2 P2() => new GeoPoint2(U(-100, 100), U(-100, 100));

            private GeoPolygon2 Star(GeoPoint2 c, double rMin, double rMax, int n)
            {
                double[] angles = Enumerable.Range(0, n).Select(_ => U(0, 2 * Math.PI)).OrderBy(a => a).ToArray();

                // Angles sorted round the centre, and no gap of half a turn or more between neighbours, so the
                // star never crosses itself.
                for (int i = 0; i < n; i++)
                {
                    angles[i] = 2 * Math.PI * i / n + U(0, 2 * Math.PI / n * 0.8);
                }

                return new GeoPolygon2(angles.Select(a =>
                {
                    double r = U(rMin, rMax);
                    return new GeoPoint2(c.X + r * Math.Cos(a), c.Y + r * Math.Sin(a));
                }));
            }

            public object Shape2(int kind)
            {
                switch (kind)
                {
                    case 0: return P2();
                    case 1: return new GeoLine2(P2(), P2());
                    case 2: { double s = U(0, 6); return new GeoArc2(P2(), U(10, 60), s, s + U(0.3, 5.5)); }
                    case 3: return new GeoCircle2(P2(), U(10, 60));
                    case 4: return new GeoPolyline2(P2(), P2(), P2(), P2());
                    case 5: return Star(P2(), 20, 60, 6);
                    case 6: return new GeoRectangle2(P2(), U(20, 80), U(20, 80), U(0, 3));
                    case 7: return new GeoPolygonArc2(Star(P2(), 30, 60, 5)).Fillet(4.0);
                    case 8: return new GeoPolylineArc2(new GeoPolyline2(P2(), P2(), P2())).Fillet(5.0);
                    case 9:
                    {
                        GeoPoint2 c = P2();
                        var outer = new GeoPolygon2(new GeoPoint2(c.X - 50, c.Y - 50), new GeoPoint2(c.X + 50, c.Y - 50), new GeoPoint2(c.X + 50, c.Y + 50), new GeoPoint2(c.X - 50, c.Y + 50));
                        var hole = new GeoPolygon2(new GeoPoint2(c.X - 15, c.Y - 15), new GeoPoint2(c.X + 15, c.Y - 15), new GeoPoint2(c.X + 15, c.Y + 15), new GeoPoint2(c.X - 15, c.Y + 15));
                        return new GeoFace2(outer, new[] { hole });
                    }
                    default:
                        return _random.NextDouble() < 0.5
                            ? new GeoEdge2(P2(), P2())
                            : new GeoEdge2(new GeoArc2(P2(), U(10, 50), U(0, 6), U(0, 6)));
                }
            }

            #endregion

            #region Space shapes

            private GeoPoint3 P3() => new GeoPoint3(U(-100, 100), U(-100, 100), U(-100, 100));

            private GeoVector3 V3()
            {
                GeoVector3 v;

                do
                {
                    v = new GeoVector3(U(-1, 1), U(-1, 1), U(-1, 1));
                }
                while (v.Length < 0.2);

                return v.Normalize();
            }

            private GeoPolygon3 Planar(GeoPoint3 c, double rMin, double rMax, int n)
            {
                var plane = new GeoPlane3(c, V3());
                plane.GetAxes(out GeoVector3 u, out GeoVector3 v);

                return new GeoPolygon3(Enumerable.Range(0, n).Select(i =>
                {
                    double a = 2 * Math.PI * i / n + U(0, 2 * Math.PI / n * 0.8);
                    double r = U(rMin, rMax);
                    return c.Add(u.Multiply(r * Math.Cos(a))).Add(v.Multiply(r * Math.Sin(a)));
                }));
            }

            public object Shape3(int kind)
            {
                switch (kind)
                {
                    case 0: return P3();
                    case 1: return new GeoLine3(P3(), P3());
                    case 2: return new GeoRay3(P3(), V3());
                    case 3: return new GeoPlane3(P3(), V3());
                    case 4: return new GeoTriangle3(P3(), P3(), P3());
                    case 5: return Planar(P3(), 20, 60, 6);
                    case 6: return new GeoFace3(Planar(P3(), 30, 60, 5));
                    case 7: { GeoVector3 x = V3(); GeoVector3 y = x.CrossProduct(V3()).Normalize(); return new GeoObb3(P3(), U(10, 80), U(10, 80), U(10, 80), x, y); }
                    case 8: { GeoPoint3 a = P3(); return new GeoAabb3(a, a.Add(new GeoVector3(U(10, 80), U(10, 80), U(10, 80)))); }
                    case 9: { GeoPoint3 c = P3(); double s = U(10, 50); return new GeoAabb3(new GeoPoint3(c.X - s, c.Y - s, c.Z - s), new GeoPoint3(c.X + s, c.Y + s, c.Z + s)).ToObb().ToSolid(); }
                    case 10: return new GeoPolyline3(P3(), P3(), P3(), P3());
                    case 11: { GeoPoint3 a = P3(); return GeoArc3.FromThreePoints(a, a.Add(V3().Multiply(U(20, 60))), a.Add(V3().Multiply(U(20, 60)))); }
                    case 12: return new GeoCircle3(P3(), V3(), U(10, 60));
                    default: return new GeoPolyline3(P3(), P3(), P3()).Fillet(5.0);
                }
            }

            public GeoTransform3 Turn() => GeoTransform3.RotationAxis(P3(), V3(), U(0, 6));

            public GeoTransform3 Far() => GeoTransform3.Translation(new GeoVector3(U(-1e6, 1e6), U(-1e6, 1e6), U(-1e5, 1e5)));

            #endregion

            #region Checks

            private void Note(string what, object a, object b, string detail)
            {
                if (_findings.Count < 40)
                {
                    _findings.Add($"{what} {a.GetType().Name}|{b.GetType().Name}: {detail} [{a} / {b}]");
                }
            }

            public void Pair(object a, object b)
            {
                object dab = Call(a, "DistanceTo", b);
                object dba = Call(b, "DistanceTo", a);
                object cab = Call(a, "CollidesWith", b);
                object cba = Call(b, "CollidesWith", a);

                foreach (object answer in new[] { dab, dba, cab, cba })
                {
                    if (answer is Exception e)
                    {
                        Note("THROWS", a, b, e.GetType().Name + ": " + e.Message);
                        return;
                    }
                }

                double? d = dab as double?;
                double? d2 = dba as double?;
                bool? c = cab as bool?;
                bool? c2 = cba as bool?;

                if (d.HasValue && (double.IsNaN(d.Value) || d.Value < 0))
                {
                    Note("BAD DISTANCE", a, b, d.Value.ToString());
                }

                if (d.HasValue && d2.HasValue)
                {
                    Checks++;

                    if (Math.Abs(d.Value - d2.Value) > 1E-6 * Math.Max(1, d.Value))
                    {
                        Note("ASYMMETRIC DISTANCE", a, b, d.Value + " vs " + d2.Value);
                    }
                }

                if (c.HasValue && c2.HasValue)
                {
                    Checks++;

                    if (c.Value != c2.Value)
                    {
                        Note("ASYMMETRIC COLLISION", a, b, c.Value + " vs " + c2.Value);
                    }
                }

                if (c.HasValue && d.HasValue)
                {
                    Checks++;

                    if (c.Value != d.Value <= 1E-4)
                    {
                        Note("COLLIDES BUT APART, OR TOUCHES BUT NOT", a, b, "collides " + c.Value + ", distance " + d.Value);
                    }
                }

                if (d.HasValue && d.Value > 1E-4)
                {
                    object line = Call(a, "GetShortestLineTo", b);

                    if (line is Exception e)
                    {
                        Note("THROWS", a, b, "GetShortestLineTo: " + e.GetType().Name + ": " + e.Message);
                    }
                    else if (line != Missing)
                    {
                        Checks++;
                        double length = (double)line.GetType().GetProperty("Length").GetValue(line);

                        if (Math.Abs(length - d.Value) > 1E-5 * Math.Max(1, d.Value))
                        {
                            Note("SHORTEST LINE IS NOT THE DISTANCE", a, b, "line " + length + ", distance " + d.Value);
                        }

                        object start = line.GetType().GetProperty("StartPoint").GetValue(line);
                        object end = line.GetType().GetProperty("EndPoint").GetValue(line);

                        if (Call(a, "DistanceTo", start) is double s && s > 1E-4)
                        {
                            Note("SHORTEST LINE STARTS OFF THE FIRST", a, b, s.ToString());
                        }

                        if (Call(b, "DistanceTo", end) is double t && t > 1E-4)
                        {
                            Note("SHORTEST LINE ENDS OFF THE SECOND", a, b, t.ToString());
                        }
                    }
                }
            }

            public void Moved(object a, object b, GeoTransform3 move, string how)
            {
                if (a is GeoAabb3 || b is GeoAabb3 || !(Call(a, "DistanceTo", b) is double before))
                {
                    return;
                }

                object movedA = Call(a, "TransformBy", move);
                object movedB = Call(b, "TransformBy", move);

                if (movedA == Missing || movedB == Missing || movedA is Exception || movedB is Exception)
                {
                    return;
                }

                if (!(Call(movedA, "DistanceTo", movedB) is double after))
                {
                    return;
                }

                Checks++;

                if (Math.Abs(before - after) > 1E-5 * Math.Max(1, before))
                {
                    Note("MOVED (" + how + ") CHANGES THE DISTANCE", a, b, before + " -> " + after);
                }

                if (before > 1E-3 && Call(a, "CollidesWith", b) is bool was && Call(movedA, "CollidesWith", movedB) is bool now && was != now)
                {
                    Note("MOVED (" + how + ") CHANGES THE COLLISION", a, b, was + " -> " + now);
                }
            }

            public void Inside(object container, object probe)
            {
                foreach ((object from, object to) in new[] { (container, probe), (probe, container) })
                {
                    object distance = Call(from, "DistanceTo", to);
                    object collides = Call(from, "CollidesWith", to);

                    if (distance is double d)
                    {
                        Checks++;

                        if (d > 1E-4)
                        {
                            Note("INSIDE BUT APART", container, probe, d.ToString());
                        }
                    }

                    if (collides is bool c)
                    {
                        Checks++;

                        if (!c)
                        {
                            Note("INSIDE BUT NOT TOUCHING", container, probe, "asked from the " + (from == container ? "container" : "probe"));
                        }
                    }

                    if (distance is Exception e || collides is Exception)
                    {
                        Note("THROWS", container, probe, (distance as Exception ?? (Exception)collides).Message);
                    }
                }
            }

            #endregion
        }

        private static object Call(object target, string name, object argument)
        {
            MethodInfo method = target.GetType().GetMethod(name, BindingFlags.Public | BindingFlags.Instance, null, new[] { argument.GetType() }, null);

            if (method == null)
            {
                return Missing;
            }

            try
            {
                return method.Invoke(target, new[] { argument });
            }
            catch (TargetInvocationException e)
            {
                return e.InnerException;
            }
        }

        private static void Report(Sweeper sweeper, int least)
        {
            Assert.True(sweeper.Checks >= least, $"only {sweeper.Checks} checks were run");
            Assert.True(sweeper.Findings.Count == 0, string.Join(Environment.NewLine, sweeper.Findings));
        }

        [Fact]
        public void EveryPairOfPlaneShapesAgreesWithItself()
        {
            var sweeper = new Sweeper(20260927);

            for (int round = 0; round < 40; round++)
            {
                for (int i = 0; i <= 10; i++)
                {
                    for (int j = 0; j <= 10; j++)
                    {
                        sweeper.Pair(sweeper.Shape2(i), sweeper.Shape2(j));
                    }
                }
            }

            Report(sweeper, 2000);
        }

        [Fact]
        public void EveryPairOfShapesInSpaceAgreesWithItselfAndWithMoving()
        {
            var sweeper = new Sweeper(20260928);

            for (int round = 0; round < 20; round++)
            {
                GeoTransform3 turn = sweeper.Turn();
                GeoTransform3 far = sweeper.Far();

                for (int i = 0; i <= 13; i++)
                {
                    for (int j = 0; j <= 13; j++)
                    {
                        object a = sweeper.Shape3(i);
                        object b = sweeper.Shape3(j);

                        sweeper.Pair(a, b);
                        sweeper.Moved(a, b, turn, "turned");
                        sweeper.Moved(a, b, far, "a million away");
                    }
                }
            }

            Report(sweeper, 2000);
        }

        [Fact]
        public void AShapeWhollyInsideAClosedOneIsNoughtAwayAndTouches()
        {
            var sweeper = new Sweeper(1);
            var square = new GeoPolygon2(new GeoPoint2(-100, -100), new GeoPoint2(100, -100), new GeoPoint2(100, 100), new GeoPoint2(-100, 100));
            var small = new GeoPolygon2(new GeoPoint2(-20, -20), new GeoPoint2(20, -20), new GeoPoint2(20, 20), new GeoPoint2(-20, 20));
            object[] containers2 =
            {
                new GeoCircle2(new GeoPoint2(0, 0), 150),
                square,
                new GeoRectangle2(new GeoPoint2(0, 0), 200, 200),
                new GeoPolygonArc2(square).Fillet(20),
                new GeoFace2(square),
            };
            object[] probes2 =
            {
                new GeoPoint2(10, 5),
                new GeoLine2(new GeoPoint2(-20, 0), new GeoPoint2(20, 5)),
                new GeoArc2(new GeoPoint2(0, 0), 30, 0, Math.PI),
                new GeoCircle2(new GeoPoint2(0, 0), 30),
                new GeoEdge2(new GeoPoint2(-20, 0), new GeoPoint2(20, 5)),
                new GeoEdge2(new GeoArc2(new GeoPoint2(0, 0), 30, 0, Math.PI)),
                new GeoPolyline2(new GeoPoint2(-20, 0), new GeoPoint2(20, 0), new GeoPoint2(20, 20)),
                new GeoPolylineArc2(new GeoPolyline2(new GeoPoint2(-20, 0), new GeoPoint2(20, 0), new GeoPoint2(20, 20))).Fillet(5),
                small,
                new GeoRectangle2(new GeoPoint2(0, 0), 40, 40),
                new GeoPolygonArc2(small).Fillet(5),
                new GeoFace2(small),
            };

            GeoSolid3 cube = new GeoAabb3(new GeoPoint3(-100, -100, -100), new GeoPoint3(100, 100, 100)).ToObb().ToSolid();
            object[] containers3 =
            {
                cube,
                new GeoObb3(GeoPoint3.Origin, 200, 200, 200),
                new GeoAabb3(new GeoPoint3(-100, -100, -100), new GeoPoint3(100, 100, 100)),
            };
            var poly = new GeoPolygon3(new GeoPoint3(-20, -20, 5), new GeoPoint3(20, -20, 5), new GeoPoint3(20, 20, 5), new GeoPoint3(-20, 20, 5));
            object[] probes3 =
            {
                new GeoPoint3(10, 5, 3),
                new GeoLine3(new GeoPoint3(-20, 0, 0), new GeoPoint3(20, 5, 3)),
                new GeoTriangle3(new GeoPoint3(-20, 0, 0), new GeoPoint3(20, 0, 0), new GeoPoint3(0, 20, 10)),
                poly,
                new GeoFace3(poly),
                new GeoObb3(GeoPoint3.Origin, 30, 30, 30),
                new GeoAabb3(new GeoPoint3(-15, -15, -15), new GeoPoint3(15, 15, 15)),
                new GeoAabb3(new GeoPoint3(-15, -15, -15), new GeoPoint3(15, 15, 15)).ToObb().ToSolid(),
                new GeoPolyline3(new GeoPoint3(-20, 0, 0), new GeoPoint3(20, 0, 0), new GeoPoint3(20, 20, 0)),
                GeoArc3.FromThreePoints(new GeoPoint3(30, 0, 0), new GeoPoint3(0, 30, 0), new GeoPoint3(-30, 0, 0)),
                new GeoCircle3(GeoPoint3.Origin, GeoVector3.ZAxis, 30),
                new GeoPolyline3(new GeoPoint3(-20, 0, 0), new GeoPoint3(20, 0, 0), new GeoPoint3(20, 20, 0)).Fillet(5),
            };

            foreach (object container in containers2)
            {
                foreach (object probe in probes2)
                {
                    sweeper.Inside(container, probe);
                }
            }

            foreach (object container in containers3)
            {
                foreach (object probe in probes3)
                {
                    sweeper.Inside(container, probe);
                }
            }

            Report(sweeper, 200);
        }
    }
}
