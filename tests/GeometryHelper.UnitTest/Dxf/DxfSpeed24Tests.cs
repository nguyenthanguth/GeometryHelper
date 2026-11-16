using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Dxf
{
    /// <summary>
    /// The work of small1 in the core: an open chain of arcs in space (<see cref="GeoPolylineArc3"/>) whose arcs share one
    /// normal, and whose last vertex, which no edge leaves, is given none, keeps no array of normals, as a closed loop does
    /// since speed16; <c>Normals</c> makes the array, the same one for every call, when it is asked for. Every answer is held
    /// to the bit against the constructor as it was before, written out below with its lists, and the size by the bytes
    /// allocated, which do not vary with the machine as a time does.
    /// </summary>
    public class DxfSpeed24Tests
    {
        private static readonly Tolerance Tol = Tolerance.Default;

        private static long Bits(double d) => BitConverter.DoubleToInt64Bits(d);

        private static void AssertSameBits(GeoVector3 expected, GeoVector3 got, string step)
        {
            if (Bits(expected.X) != Bits(got.X) || Bits(expected.Y) != Bits(got.Y) || Bits(expected.Z) != Bits(got.Z))
            {
                Assert.Fail(step + ": got " + got + " where " + expected + " is wanted");
            }
        }

        /// <summary>
        /// The constructor of a chain as it was before the array of normals went: three lists grown a vertex at a time, a vertex on
        /// the one before dropped with the edge between them, every edge made once and its bulge and normal kept as it makes them,
        /// the last vertex's bulge and normal kept as given. It throws what the chain throws.
        /// </summary>
        private static void Before(
            IReadOnlyList<GeoPoint3> vertices,
            double[] bulges,
            GeoVector3[] normals,
            out GeoPoint3[] keptVertices,
            out double[] keptBulges,
            out GeoVector3[] keptNormals
        )
        {
            var kept = new List<GeoPoint3>();
            var bulgeList = new List<double>();
            var normalList = new List<GeoVector3>();

            for (int index = 0; index < vertices.Count; index++)
            {
                double bulge = index < bulges.Length ? bulges[index] : 0.0;
                GeoVector3 normal = index < normals.Length ? normals[index] : default;

                if (kept.Count > 0 && kept[kept.Count - 1].IsEqualTo(vertices[index], Tol))
                {
                    bulgeList[bulgeList.Count - 1] = bulge;
                    normalList[normalList.Count - 1] = normal;
                    continue;
                }

                kept.Add(vertices[index]);
                bulgeList.Add(bulge);
                normalList.Add(normal);
            }

            if (kept.Count < 2)
            {
                throw new ArgumentException("A chain needs at least 2 distinct vertices.");
            }

            keptVertices = kept.ToArray();
            keptBulges = bulgeList.ToArray();
            keptNormals = normalList.ToArray();

            for (int i = 0; i < keptVertices.Length - 1; i++)
            {
                var edge = new GeoEdge3(keptVertices[i], keptVertices[i + 1], keptBulges[i], keptNormals[i], Tol);
                keptBulges[i] = edge.Bulge;
                keptNormals[i] = edge.Normal;
            }
        }

        /// <summary>Makes the chain and the chain as it was before, and holds every number of the one to the bit of the other, or both to a refusal.</summary>
        private static GeoPolylineArc3 AssertAsBefore(
            GeoPoint3[] vertices,
            double[] bulges,
            GeoVector3[] normals,
            string step
        )
        {
            GeoPolylineArc3 chain;

            try
            {
                chain = new GeoPolylineArc3(vertices, bulges, normals, Tol);
            }
            catch (ArgumentException)
            {
                Assert.ThrowsAny<ArgumentException>(() => Before(vertices, bulges, normals, out _, out _, out _));
                return null;
            }

            Before(
                vertices,
                bulges,
                normals,
                out GeoPoint3[] wantedVertices,
                out double[] wantedBulges,
                out GeoVector3[] wantedNormals
            );

            Assert.Equal(wantedVertices.Length, chain.VertexCount);
            Assert.Equal(wantedVertices.Length, chain.Normals.Count);
            Assert.Equal(wantedVertices.Length, chain.Bulges.Count);

            for (int i = 0; i < wantedVertices.Length; i++)
            {
                Assert.True(wantedVertices[i].Equals(chain[i]), step + " vertex " + i);
                Assert.True(Bits(wantedBulges[i]) == Bits(chain.GetBulgeAt(i)), step + " bulge " + i);
                Assert.True(Bits(wantedBulges[i]) == Bits(chain.Bulges[i]), step + " Bulges " + i);
                AssertSameBits(wantedNormals[i], chain.GetNormalAt(i), step + " GetNormalAt " + i);
                AssertSameBits(wantedNormals[i], chain.Normals[i], step + " Normals " + i);

                if (i < wantedVertices.Length - 1)
                {
                    GeoEdge3 edge = chain.GetEdgeAt(i);
                    GeoEdge3 wantedEdge =
                        wantedBulges[i] == 0.0
                            ? new GeoEdge3(wantedVertices[i], wantedVertices[i + 1])
                            : new GeoEdge3(wantedVertices[i], wantedVertices[i + 1], wantedBulges[i], wantedNormals[i]);
                    AssertSameBits(wantedEdge.Normal, edge.Normal, step + " GetEdgeAt " + i);
                    Assert.True(Bits(wantedEdge.Bulge) == Bits(edge.Bulge), step + " edge bulge " + i);
                }
            }

            return chain;
        }

        private static GeoPoint3 Stand(GeoPoint3 p, double a, double b)
        {
            GeoVector3 v = Stand(new GeoVector3(p.X, p.Y, p.Z), a, b);
            return new GeoPoint3(v.X + 120.0, v.Y - 45.0, v.Z + 300.0);
        }

        private static GeoVector3 Stand(GeoVector3 v, double a, double b)
        {
            double x1 = v.X;
            double y1 = v.Y * Math.Cos(a) - v.Z * Math.Sin(a);
            double z1 = v.Y * Math.Sin(a) + v.Z * Math.Cos(a);
            return new GeoVector3(x1 * Math.Cos(b) + z1 * Math.Sin(b), y1, -x1 * Math.Sin(b) + z1 * Math.Cos(b));
        }

        private static GeoPoint3[] Zigzag(int count, double step) =>
            Enumerable.Range(0, count).Select(i => new GeoPoint3(i * step, (i % 2) * step * 0.5, 0.0)).ToArray();

        [Fact]
        public void AChainWhoseArcsShareOneNormal_GivesEveryVertexTheNormalItHadBefore_ToTheBit()
        {
            // 600 chains of two to nine vertices in a plane turned about two axes, an arc on some edges, the normal of each the
            // plane's but for its length (2 or 5), with the last vertex given nothing, as a drawing's polyline gives it: each reads
            // the normal the constructor made before, or is refused as before.
            var random = new Random(24);
            int made = 0;
            int none = 0;

            for (int n = 0; n < 600; n++)
            {
                int count = 2 + random.Next(8);
                var flat = new GeoPoint3[count];
                var bulges = new double[count];
                var given = new GeoVector3[count];
                double scale = random.Next(2) == 0 ? 2.0 : 5.0;

                for (int i = 0; i < count; i++)
                {
                    flat[i] = new GeoPoint3(i * 10.0 + random.NextDouble() * 6.0, random.NextDouble() * 40.0, 0.0);
                    bool arc = i < count - 1 && random.Next(3) == 0;
                    bulges[i] = arc ? (random.NextDouble() - 0.5) * 1.2 : 0.0;
                    given[i] = arc ? new GeoVector3(0.0, 0.0, scale) : default;
                }

                double a = random.NextDouble() * 6.28;
                double b = random.NextDouble() * 6.28;
                GeoPoint3[] stood = flat.Select(p => Stand(p, a, b)).ToArray();
                GeoVector3[] normals = given.Select(v => v.Length == 0.0 ? v : Stand(v, a, b)).ToArray();

                if (AssertAsBefore(stood, bulges, normals, "chain " + n) != null)
                {
                    made++;
                    none += bulges.All(x => x == 0.0) ? 1 : 0;
                }
            }

            Assert.True(made > 500, "only " + made + " chains were made");
            Assert.True(none > 20, "only " + none + " chains had no arc");
        }

        [Fact]
        public void AChainGivenWhatEverySlotCanHold_ReadsAsItDidBefore_ToTheBit()
        {
            // 3 000 chains with vertices on each other, a bulge or a normal on the last vertex, normals of two directions and several
            // lengths, a normal on a straight edge, a missing or short list of either, and a normal of no length: whatever the
            // chain made before, it makes now, or both refuse.
            var random = new Random(240);
            GeoVector3[] directions =
            {
                new GeoVector3(0.0, 0.0, 1.0),
                new GeoVector3(0.0, 0.0, 2.0),
                new GeoVector3(0.0, 0.0, -1.0),
                new GeoVector3(0.0, 0.0, 1.0000000000000002),
                new GeoVector3(0.0, 0.0, -0.0),
                default,
            };
            int made = 0;
            int refused = 0;

            for (int n = 0; n < 3000; n++)
            {
                int count = 2 + random.Next(6);
                var points = new GeoPoint3[count];
                var bulges = new double[count];
                var normals = new GeoVector3[count];

                for (int i = 0; i < count; i++)
                {
                    bool repeat = i > 0 && random.Next(6) == 0;
                    points[i] = repeat
                        ? new GeoPoint3(points[i - 1].X + 0.0004, points[i - 1].Y, 0.0)
                        : new GeoPoint3(i * 10.0 + random.NextDouble(), random.Next(3) * 5.0, 0.0);
                    bulges[i] =
                        random.Next(3) == 0 ? 0.25 * (1 + random.Next(4)) * (random.Next(2) == 0 ? 1 : -1) : 0.0;
                    normals[i] = directions[random.Next(directions.Length)];
                }

                double[] shortBulges = random.Next(8) == 0 ? bulges.Take(random.Next(count)).ToArray() : bulges;
                GeoVector3[] shortNormals = random.Next(8) == 0 ? normals.Take(random.Next(count)).ToArray() : normals;

                if (AssertAsBefore(points, shortBulges, shortNormals, "chain " + n) != null)
                {
                    made++;
                }
                else
                {
                    refused++;
                }
            }

            Assert.True(made > 400, "only " + made + " chains were made");
            Assert.True(refused > 400, "only " + refused + " chains were refused");
        }

        [Fact]
        public void AChainOfStraightEdges_HasNoNormalOnAnyVertex()
        {
            var chain = new GeoPolylineArc3(Zigzag(5, 10.0), null, null, Tol);

            Assert.Equal(5, chain.Normals.Count);

            for (int i = 0; i < 5; i++)
            {
                AssertSameBits(default, chain.GetNormalAt(i), "vertex " + i);
                AssertSameBits(default, chain.Normals[i], "normals " + i);
            }
        }

        [Fact]
        public void AChainWhoseArcsGiveNormalsOfOneDirectionButNotOneLength_ReadsTheUnitVectorOnEach()
        {
            // (0, 0, 1) and (0, 0, 2) are two normals to the bit and one to an edge: both arcs read (0, 0, 1).
            GeoPoint3[] points = Zigzag(5, 10.0).Select(p => new GeoPoint3(p.X, 0.0, p.Y)).ToArray();
            double[] bulges = { 0.5, 0.0, 0.3, 0.0, 0.0 };
            GeoVector3[] normals =
            {
                new GeoVector3(0.0, 1.0, 0.0),
                default,
                new GeoVector3(0.0, 2.0, 0.0),
                default,
                default,
            };

            GeoPolylineArc3 chain = AssertAsBefore(points, bulges, normals, "two lengths");

            AssertSameBits(new GeoVector3(0.0, 1.0, 0.0), chain.GetNormalAt(2), "second arc");
        }

        [Fact]
        public void AChainWhoseArcsFaceOppositeWays_KeepsTheNormalOfEach()
        {
            GeoPoint3[] points = Zigzag(5, 10.0);
            double[] bulges = { 0.5, 0.0, -0.3, 0.0, 0.0 };
            GeoVector3[] normals =
            {
                new GeoVector3(0.0, 0.0, 1.0),
                default,
                new GeoVector3(0.0, 0.0, -1.0),
                default,
                default,
            };

            GeoPolylineArc3 chain = AssertAsBefore(points, bulges, normals, "opposite");

            Assert.Equal(1.0, chain.GetNormalAt(0).Z);
            Assert.Equal(-1.0, chain.GetNormalAt(2).Z);
        }

        [Fact]
        public void TheLastVertex_KeepsABulgeAndANormalAsItWasGiven()
        {
            // No edge leaves the last vertex, so its bulge and its normal are kept as given, the normal of length 5 and not made a unit.
            GeoPoint3[] points = Zigzag(4, 10.0);
            GeoVector3[] normals = { new GeoVector3(0.0, 0.0, 1.0), default, default, new GeoVector3(0.0, 0.0, 5.0) };
            GeoPolylineArc3 chain = AssertAsBefore(points, new[] { 0.5, 0.0, 0.0, 0.7 }, normals, "bulge and normal");

            Assert.Equal(0.7, chain.GetBulgeAt(3));
            AssertSameBits(new GeoVector3(0.0, 0.0, 5.0), chain.GetNormalAt(3), "last");
            AssertSameBits(new GeoVector3(0.0, 0.0, 5.0), chain.Normals[3], "last in Normals");
            AssertSameBits(new GeoVector3(0.0, 0.0, 1.0), chain.GetNormalAt(0), "first arc");

            // A normal with no bulge on the last vertex is kept too.
            chain = AssertAsBefore(points, new[] { 0.5, 0.0, 0.0, 0.0 }, normals, "normal alone");
            AssertSameBits(new GeoVector3(0.0, 0.0, 5.0), chain.GetNormalAt(3), "last, no bulge");
        }

        [Fact]
        public void AVertexOnItsNeighbour_IsDropped_AndTheKeptOneTakesTheBulgeAndNormalOfTheEdgeRunningOn()
        {
            // The chain of the constructor's own remark: (0, 0, 0), (10, 0, 0), (10.0004, 0, 0), (20, 0, 0), bulges 0, 0, 1 about Z.
            GeoPoint3[] points =
            {
                new GeoPoint3(0.0, 0.0, 0.0),
                new GeoPoint3(10.0, 0.0, 0.0),
                new GeoPoint3(10.0004, 0.0, 0.0),
                new GeoPoint3(20.0, 0.0, 0.0),
            };
            GeoVector3[] normals = { default, default, new GeoVector3(0.0, 0.0, 1.0), default };

            GeoPolylineArc3 chain = AssertAsBefore(points, new[] { 0.0, 0.0, 1.0, 0.0 }, normals, "dropped");

            Assert.Equal(3, chain.VertexCount);
            Assert.Equal(new[] { 0.0, 1.0, 0.0 }, chain.Bulges);
            AssertSameBits(new GeoVector3(0.0, 0.0, 1.0), chain.GetNormalAt(1), "arc");
        }

        [Fact]
        public void AnArcDroppedWithItsVertex_LeavesTheEdgeStraight_AndTheNormalNone()
        {
            GeoPoint3[] points =
            {
                new GeoPoint3(0.0, 0.0, 0.0),
                new GeoPoint3(0.0004, 0.0, 0.0),
                new GeoPoint3(20.0, 0.0, 0.0),
            };
            GeoPolylineArc3 chain = AssertAsBefore(
                points,
                new[] { 0.5, 0.0, 0.0 },
                new[] { new GeoVector3(0.0, 0.0, 1.0), default, default },
                "arc dropped"
            );

            Assert.Equal(2, chain.VertexCount);
            Assert.Equal(new[] { 0.0, 0.0 }, chain.Bulges);
            AssertSameBits(default, chain.GetNormalAt(0), "edge");
        }

        [Fact]
        public void TheNormalsOfAChainAskedFor_AreOneArray_EachCallAndEveryThread()
        {
            var chain = new GeoPolylineArc3(
                Zigzag(5, 10.0),
                new[] { 0.5, 0.0, 0.0, 0.0, 0.0 },
                new[] { new GeoVector3(0.0, 0.0, 1.0), default, default, default, default },
                Tol
            );
            var seen = new IReadOnlyList<GeoVector3>[16];

            System.Threading.Tasks.Parallel.For(0, seen.Length, i => seen[i] = chain.Normals);

            Assert.All(seen, s => Assert.Same(seen[0], s));
            Assert.Same(seen[0], chain.Normals);
            AssertSameBits(new GeoVector3(0.0, 0.0, 1.0), seen[0][0], "arc");
            AssertSameBits(default, seen[0][1], "straight");
            AssertSameBits(default, seen[0][4], "last");
        }

        [Fact]
        public void ACopyOfAChain_AndTheChainMoved_ReadTheNormalsOfTheChain_WhetherItsNormalsWereAskedFor()
        {
            GeoPolylineArc3 Make() =>
                new GeoPolylineArc3(
                    Zigzag(5, 10.0),
                    new[] { 0.5, 0.0, 0.25, 0.0, 0.0 },
                    new[] { new GeoVector3(0.0, 0.0, 1.0), default, new GeoVector3(0.0, 0.0, 1.0), default, default },
                    Tol
                );

            GeoPolylineArc3 untouched = Make();
            GeoPolylineArc3 asked = Make();
            _ = asked.Normals;

            foreach (GeoPolylineArc3 chain in new[] { untouched, asked })
            {
                GeoPolylineArc3 clone = chain.Clone();
                GeoPolylineArc3 moved = chain.Translate(new GeoVector3(5.0, -3.0, 0.0));

                Assert.True(clone.Equals(chain));
                Assert.True(chain.Equals(clone));
                Assert.Equal(chain.GetHashCode(), clone.GetHashCode());

                for (int i = 0; i < 5; i++)
                {
                    AssertSameBits(chain.GetNormalAt(i), clone.GetNormalAt(i), "clone " + i);
                    AssertSameBits(chain.GetNormalAt(i), moved.GetNormalAt(i), "moved " + i);
                    Assert.Equal(Bits(chain.GetBulgeAt(i)), Bits(moved.GetBulgeAt(i)));
                }
            }

            Assert.True(untouched.Equals(asked));
            Assert.True(asked.Equals(untouched));
        }

        [Fact]
        public void AnArcWithANormalOfNoLength_IsRefusedAsBefore_ThoughNoArrayIsKept()
        {
            Assert.Throws<ArgumentException>(() =>
                new GeoPolylineArc3(Zigzag(4, 10.0), new[] { 0.5, 0.0, 0.0, 0.0 }, null, Tol)
            );
            Assert.Throws<ArgumentException>(() =>
                new GeoPolylineArc3(
                    Zigzag(4, 10.0),
                    new[] { 0.5, 0.0, 0.3, 0.0 },
                    new[] { new GeoVector3(0.0, 0.0, 1.0), default, default, default },
                    Tol
                )
            );
        }

        [Fact]
        public void AnArcWhoseNormalIsNotSquareToItsChord_IsRefusedAsBefore()
        {
            // The chord runs along x; a normal along x is not square to it.
            Assert.Throws<ArgumentException>(() =>
                new GeoPolylineArc3(
                    new[]
                    {
                        new GeoPoint3(0.0, 0.0, 0.0),
                        new GeoPoint3(10.0, 0.0, 0.0),
                        new GeoPoint3(20.0, 0.0, 0.0),
                    },
                    new[] { 0.5, 0.0, 0.0 },
                    new[] { new GeoVector3(1.0, 0.0, 0.0), default, default },
                    Tol
                )
            );
        }

        [Fact]
        public void AChainBuiltOfASequenceAndOfAList_ReadAlike()
        {
            GeoPoint3[] points = Zigzag(5, 10.0);
            double[] bulges = { 0.5, 0.0, 0.3, 0.0, 0.0 };
            GeoVector3[] normals =
            {
                new GeoVector3(0.0, 0.0, 1.0),
                default,
                new GeoVector3(0.0, 0.0, 1.0),
                default,
                default,
            };

            var fromArrays = new GeoPolylineArc3(points, bulges, normals, Tol);
            var fromSequences = new GeoPolylineArc3(
                points.Select(p => p),
                bulges.Select(b => b),
                normals.Select(v => v),
                Tol
            );

            Assert.True(fromArrays.Equals(fromSequences));
        }

        [Fact]
        public void AChainOfFiveVerticesWithOneArc_HoldsNoArrayOfNormals()
        {
            // The object, the vertices (5 * 24 + 24 bytes) and the bulges (5 * 8 + 24): the array of normals (144) and the three lists
            // the constructor grew a vertex at a time are not made: 272 bytes under .NET Framework where it made 1 648.
            GeoPoint3[] points = Zigzag(5, 10.0);
            double[] bulges = { 0.0, 0.5, 0.0, 0.0, 0.0 };
            GeoVector3[] normals = { default, new GeoVector3(0.0, 0.0, 1.0), default, default, default };
            _ = new GeoPolylineArc3(points, bulges, normals, Tol);

            GC.Collect();
            long before = GC.GetAllocatedBytesForCurrentThread();

            for (int i = 0; i < 1_000; i++)
            {
                _ = new GeoPolylineArc3(points, bulges, normals, Tol);
            }

            long used = (GC.GetAllocatedBytesForCurrentThread() - before) / 1_000;

            Assert.True(used <= 300, "allocated " + used + " bytes a chain");
        }
    }
}
