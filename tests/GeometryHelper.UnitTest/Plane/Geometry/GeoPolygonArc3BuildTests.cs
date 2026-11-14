using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// A loop of arcs in space built from its vertices, bulges and normals, against the same loop worked out the long way
    /// with lists, a chain made of its vertices to find the plane, and an edge made for each side: the same to the bit
    /// over generated loops (vertices doubled, a last one on the first, a plane given or not, bulges out of the plane,
    /// not finite, short or given as lists, arrays and sequences), and the loop laid out in its plane against the one the
    /// constructor builds.
    /// </summary>
    /// <remarks>
    /// The generated loops run to <c>GH_FUZZ_COUNT</c> a test (1 500 where it is not set), with <c>GH_FUZZ_SEED</c> moving
    /// the seed.
    /// </remarks>
    public class GeoPolygonArc3BuildTests
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private static int Count() =>
            int.TryParse(Environment.GetEnvironmentVariable("GH_FUZZ_COUNT"), NumberStyles.Integer, Inv, out int n)
            && n > 0
                ? n
                : 1500;

        private static int Seed() =>
            int.TryParse(Environment.GetEnvironmentVariable("GH_FUZZ_SEED"), NumberStyles.Integer, Inv, out int n)
                ? n
                : 0;

        [Fact]
        public void ARectangleWithOneRoundedSide_KeepsItsVerticesBulgesAndPlane()
        {
            var vertices = new List<GeoPoint3>
            {
                new GeoPoint3(0, 0, 5),
                new GeoPoint3(20, 0, 5),
                new GeoPoint3(20, 20, 5),
                new GeoPoint3(0, 20, 5),
            };
            var bulges = new List<double> { 0.0, 0.5, 0.0, 0.0 };
            var normals = new List<GeoVector3>
            {
                default(GeoVector3),
                new GeoVector3(0, 0, 1),
                default(GeoVector3),
                default(GeoVector3),
            };

            var loop = new GeoPolygonArc3(vertices, bulges, normals, Tolerance.Default);

            Assert.Equal(vertices, loop.Vertices);
            Assert.Equal(bulges, loop.Bulges);
            Assert.Equal(new GeoVector3(0, 0, 1), loop.Normal);
            Assert.Equal(new GeoVector3(0, 0, 1), loop.Normals[1]);

            // 400 square units and the half circle-ish segment of the bulge 0.5 on a side of 20: a quarter of the angle 4 atan 0.5.
            double sweep = 4.0 * Math.Atan(0.5);
            double radius = 10.0 / Math.Sin(sweep / 2.0);
            Assert.Equal(400.0 + radius * radius * 0.5 * (sweep - Math.Sin(sweep)), loop.Area, 9);
        }

        [Fact]
        public void AVertexWithinTheToleranceOfTheOneBefore_IsDropped_AsItAlwaysWas()
        {
            var vertices = new[]
            {
                new GeoPoint3(0, 0, 0),
                new GeoPoint3(10, 0, 0),
                new GeoPoint3(10, 0.0004, 0),
                new GeoPoint3(10, 10, 0),
                new GeoPoint3(0, 10, 0),
                new GeoPoint3(0, 0, 0),
            };

            var loop = new GeoPolygonArc3(vertices, null, null, Tolerance.Default);

            Assert.Equal(4, loop.Vertices.Count);
            Assert.Equal(100.0, loop.Area, 9);
        }

        [Fact]
        public void GeneratedLoops_AreBuilt_AsTheLongWayBuildsThem()
        {
            int total = Count();
            var wrong = new List<string>();
            int built = 0;
            int refused = 0;
            int arcs = 0;
            var rnd = new Random(8101 + Seed());

            for (int c = 0; c < total; c++)
            {
                Generated g = Generate(rnd);
                string expected = Describe(() => Long(g.Vertices, g.BulgesTaken, g.NormalsTaken, g.Tolerance));
                string got = Describe(() =>
                {
                    var loop = new GeoPolygonArc3(g.Vertices(), g.BulgesGiven(), g.NormalsGiven(), g.Tolerance);
                    return new Built(
                        loop.Vertices.ToArray(),
                        loop.Bulges.ToArray(),
                        loop.Normals.ToArray(),
                        loop.Normal
                    );
                });

                if (got != expected)
                {
                    wrong.Add("loop " + c + ": " + got + " against " + expected);
                }
                else if (got.StartsWith("built", StringComparison.Ordinal))
                {
                    built++;
                    arcs += g.Bulges.Any(b => b != 0.0) ? 1 : 0;
                }
                else
                {
                    refused++;
                }
            }

            Assert.Empty(wrong);
            Assert.True(
                built > total / 4 && refused > total / 20 && arcs > total / 10,
                built + " built, " + refused + " refused, " + arcs + " with arcs"
            );
        }

        [Fact]
        public void GeneratedLoops_AreLaidOutInTheirPlane_AsTheLongWayLaysThemOut()
        {
            int total = Count();
            var wrong = new List<string>();
            int seen = 0;
            var rnd = new Random(8201 + Seed());

            for (int c = 0; c < total; c++)
            {
                Generated g = Generate(rnd);
                GeoPolygonArc3 loop;

                try
                {
                    loop = new GeoPolygonArc3(g.Vertices(), g.BulgesGiven(), g.NormalsGiven(), g.Tolerance);
                }
                catch (ArgumentException)
                {
                    continue;
                }

                GeoPolygonArc2 flat = loop.ToPolygonArc2();
                var starts = new List<GeoPoint2>();
                var bulges = new List<double>();
                GeoCoordinateSystem3 frame = loop.GetFrame();

                for (int i = 0; i < loop.Vertices.Count; i++)
                {
                    GeoEdge3 edge = loop.GetEdgeAt(i);
                    starts.Add(PlanarMap.ProjectToPoint2(frame, edge.StartPoint));
                    bool facingAway = edge.IsArc && edge.Normal.DotProduct(frame.ZAxis) < 0.0;
                    bulges.Add(facingAway ? -edge.Bulge : edge.Bulge);
                }

                string expected = Describe(() => Of(GeoPolygonArc2.FromEdges(starts, bulges, Tolerance.Global)));
                string got = Describe(() => Of(flat));

                if (got != expected)
                {
                    wrong.Add("loop " + c + ": " + got + " against " + expected);
                }

                seen++;
            }

            Assert.Empty(wrong);
            Assert.True(seen > total / 4, seen + " of " + total + " loops built");
        }

        [Fact]
        public void GeneratedStartsOnTheirOwn_MakeTheLoopTheConstructorMakes_OfListsOrOfArrays()
        {
            int total = Count();
            var wrong = new List<string>();
            var rnd = new Random(8301 + Seed());

            for (int c = 0; c < total; c++)
            {
                int n = rnd.Next(2, 8);
                var starts = new List<GeoPoint2>();
                var bulges = new List<double>();

                for (int i = 0; i < n; i++)
                {
                    GeoPoint2 p =
                        i > 0 && rnd.Next(5) == 0 ? starts[i - 1]
                        : i > 1 && rnd.Next(8) == 0 ? starts[0]
                        : new GeoPoint2(
                            Math.Round(rnd.NextDouble() * 20.0, rnd.Next(2) == 0 ? 0 : 4),
                            Math.Round(rnd.NextDouble() * 20.0, rnd.Next(2) == 0 ? 0 : 4)
                        );
                    starts.Add(rnd.Next(7) == 0 ? new GeoPoint2(p.X + 0.0004, p.Y) : p);
                    bulges.Add(rnd.Next(3) == 0 ? 0.0 : Math.Round(rnd.NextDouble() - 0.5, 3));
                }

                Tolerance[] tolerances = { default(Tolerance), Tolerance.Global, new Tolerance(1E-6, 1E-6) };
                Tolerance tolerance = tolerances[rnd.Next(tolerances.Length)];
                string expected = Describe(() => Of(GeoPolygonArc2.FromEdges(starts, bulges, tolerance)));
                string got = Describe(() =>
                    Of(GeoPolygonArc2.FromEdgesOf(starts.ToArray(), bulges.ToArray(), tolerance))
                );

                if (got != expected)
                {
                    wrong.Add("loop " + c + ": " + got + " against " + expected);
                }
            }

            Assert.Empty(wrong);
        }

        #region The long way

        private sealed class Built
        {
            internal Built(GeoPoint3[] vertices, double[] bulges, GeoVector3[] normals, GeoVector3 plane)
            {
                Vertices = vertices;
                Bulges = bulges;
                Normals = normals;
                Plane = plane;
            }

            internal GeoPoint3[] Vertices { get; }

            internal double[] Bulges { get; }

            internal GeoVector3[] Normals { get; }

            internal GeoVector3 Plane { get; }
        }

        private static string Bits(double d) => BitConverter.DoubleToInt64Bits(d).ToString("X16", Inv);

        private static string Of(GeoPolygonArc2 loop)
        {
            var parts = new List<string>();

            for (int i = 0; i < loop.VertexCount; i++)
            {
                parts.Add(Bits(loop.Vertices[i].X) + "," + Bits(loop.Vertices[i].Y) + "/" + Bits(loop.GetBulgeAt(i)));
            }

            return string.Join(";", parts);
        }

        private static string Describe(Func<object> make)
        {
            try
            {
                object made = make();

                if (made is Built b)
                {
                    return "built "
                        + string.Join(";", b.Vertices.Select(p => Bits(p.X) + "," + Bits(p.Y) + "," + Bits(p.Z)))
                        + "|"
                        + string.Join(";", b.Bulges.Select(Bits))
                        + "|"
                        + string.Join(";", b.Normals.Select(p => Bits(p.X) + "," + Bits(p.Y) + "," + Bits(p.Z)))
                        + "|"
                        + Bits(b.Plane.X)
                        + ","
                        + Bits(b.Plane.Y)
                        + ","
                        + Bits(b.Plane.Z);
                }

                return "built " + made;
            }
            catch (ArgumentException e)
            {
                return "refused " + e.GetType().Name + " " + e.ParamName + " " + e.Message;
            }
        }

        /// <summary>The loop as its constructor was written first: lists for what is kept, a chain made of the vertices to find the plane, and an edge made for each side.</summary>
        private static Built Long(
            Func<IEnumerable<GeoPoint3>> vertices,
            IReadOnlyList<double> bulges,
            IReadOnlyList<GeoVector3> normals,
            Tolerance tolerance
        )
        {
            var kept = new List<GeoPoint3>();
            var keptBulges = new List<double>();
            var keptNormals = new List<GeoVector3>();
            var givenBulges = new List<double>(bulges);

            if (!Guard.AreFinite(givenBulges))
            {
                throw new ArgumentException("A bulge has to be a finite number.", "bulges");
            }

            var givenNormals = new List<GeoVector3>(normals);
            int index = 0;

            foreach (GeoPoint3 vertex in vertices())
            {
                double bulge = index < givenBulges.Count ? givenBulges[index] : 0.0;
                GeoVector3 normal = index < givenNormals.Count ? givenNormals[index] : default(GeoVector3);
                index++;

                if (kept.Count > 0 && kept[kept.Count - 1].IsEqualTo(vertex, tolerance))
                {
                    keptBulges[keptBulges.Count - 1] = bulge;
                    keptNormals[keptNormals.Count - 1] = normal;
                    continue;
                }

                kept.Add(vertex);
                keptBulges.Add(bulge);
                keptNormals.Add(normal);
            }

            while (kept.Count > 1 && kept[kept.Count - 1].IsEqualTo(kept[0], tolerance))
            {
                kept.RemoveAt(kept.Count - 1);
                keptBulges.RemoveAt(keptBulges.Count - 1);
                keptNormals.RemoveAt(keptNormals.Count - 1);
            }

            if (kept.Count < 3)
            {
                throw new ArgumentException("A loop needs at least 3 distinct vertices.", "vertices");
            }

            if (!new GeoPolyline3(kept).TryGetPlane(out GeoPlane3 plane, tolerance))
            {
                throw new ArgumentException(
                    "A closed loop of arcs has to lie in one plane; a bar bent out of one belongs in a GeoPolylineArc3.",
                    "vertices"
                );
            }

            GeoPoint3[] vs = kept.ToArray();
            double[] bs = keptBulges.ToArray();
            GeoVector3[] ns = keptNormals.ToArray();

            for (int i = 0; i < vs.Length; i++)
            {
                var edge = new GeoEdge3(vs[i], vs[(i + 1) % vs.Length], bs[i], ns[i], tolerance);

                if (
                    edge.IsArc
                    && !Arc3.LiesIn(plane, edge.StartPoint, edge.EndPoint, edge.Bulge, edge.Normal, tolerance)
                )
                {
                    throw new ArgumentException(
                        "An arc of a closed loop has to bulge in the plane of the loop.",
                        "normals"
                    );
                }

                bs[i] = edge.Bulge;
                ns[i] = edge.Normal;
            }

            return new Built(vs, bs, ns, plane.Normal);
        }

        #endregion

        #region Generated loops

        private sealed class Generated
        {
            internal List<GeoPoint3> Points;
            internal List<double> Bulges;
            internal List<GeoVector3> Normals;
            internal Tolerance Tolerance;
            internal int VertexForm;
            internal int GivenForm;

            internal Func<IEnumerable<GeoPoint3>> Vertices => () => As(Points, VertexForm);

            /// <summary>The bulges the constructor is given, as a list: none where it is given none.</summary>
            internal IReadOnlyList<double> BulgesTaken => GivenForm == 3 ? new List<double>() : Bulges;

            internal IReadOnlyList<GeoVector3> NormalsTaken => GivenForm == 3 ? new List<GeoVector3>() : Normals;

            internal IEnumerable<double> BulgesGiven() =>
                GivenForm == 3 ? null
                : GivenForm == 0 ? (IEnumerable<double>)Bulges
                : GivenForm == 1 ? Bulges.ToArray()
                : Bulges.Select(b => b);

            internal IEnumerable<GeoVector3> NormalsGiven() =>
                GivenForm == 3 ? null
                : GivenForm == 0 ? (IEnumerable<GeoVector3>)Normals
                : GivenForm == 1 ? Normals.ToArray()
                : Normals.Select(b => b);

            internal IEnumerable<GeoPoint3> VerticesGiven() => As(Points, VertexForm);

            private static IEnumerable<GeoPoint3> As(List<GeoPoint3> points, int form) =>
                form == 0 ? (IEnumerable<GeoPoint3>)points
                : form == 1 ? points.ToArray()
                : form == 2 ? points.Select(p => p)
                : new System.Collections.ObjectModel.ReadOnlyCollection<GeoPoint3>(points);
        }

        private static Generated Generate(Random rnd)
        {
            int n = 3 + rnd.Next(6);
            GeoVector3 normal =
                rnd.Next(3) == 0
                    ? new GeoVector3(0, 0, 1)
                    : new GeoVector3(rnd.NextDouble() - 0.5, rnd.NextDouble() - 0.5, rnd.NextDouble() + 0.1);
            double length = normal.Length;
            normal = new GeoVector3(normal.X / length, normal.Y / length, normal.Z / length);
            GeoVector3 helper = Math.Abs(normal.Z) > 0.9 ? new GeoVector3(1, 0, 0) : new GeoVector3(0, 0, 1);
            GeoVector3 a = normal.CrossProduct(helper);
            double al = a.Length;
            a = new GeoVector3(a.X / al, a.Y / al, a.Z / al);
            GeoVector3 b = normal.CrossProduct(a);
            double off = rnd.Next(4) == 0 ? 1000 * rnd.NextDouble() : 0;
            double scale = rnd.Next(5) == 0 ? 0.01 + rnd.NextDouble() * 0.1 : 10 + rnd.NextDouble() * 100;
            double noise = rnd.Next(4) == 0 ? (rnd.Next(2) == 0 ? 1E-4 : 5E-3) * rnd.NextDouble() : 0;
            var points = new List<GeoPoint3>();
            var bulges = new List<double>();
            var normals = new List<GeoVector3>();
            double angle = 0;

            for (int i = 0; i < n; i++)
            {
                angle += (2 * Math.PI / n) * (0.5 + rnd.NextDouble());
                double radius = scale * (0.5 + rnd.NextDouble());
                double x = radius * Math.Cos(angle);
                double y = radius * Math.Sin(angle);
                var p = new GeoPoint3(
                    off + a.X * x + b.X * y + normal.X * noise * (rnd.NextDouble() - 0.5),
                    off + a.Y * x + b.Y * y + normal.Y * noise * (rnd.NextDouble() - 0.5),
                    a.Z * x + b.Z * y + normal.Z * noise * (rnd.NextDouble() - 0.5)
                );
                points.Add(p);
                int bulgeKind = rnd.Next(6);
                bulges.Add(
                    bulgeKind <= 2 ? 0
                    : bulgeKind == 3 ? 0.5 * (rnd.NextDouble() - 0.5)
                    : bulgeKind == 4 ? 1E-9
                    : 2 * (rnd.NextDouble() - 0.5)
                );
                int normalKind = rnd.Next(8);
                normals.Add(
                    normalKind == 0 ? default(GeoVector3)
                    : normalKind == 1 ? new GeoVector3(0, 0, 1)
                    : normalKind == 2 ? new GeoVector3(normal.X * 3, normal.Y * 3, normal.Z * 3)
                    : normalKind == 3 ? new GeoVector3(-normal.X, -normal.Y, -normal.Z)
                    : normal
                );

                if (rnd.Next(12) == 0)
                {
                    points.Add(p);
                    bulges.Add(rnd.Next(2) == 0 ? 0.3 : 0);
                    normals.Add(normal);
                }

                if (rnd.Next(25) == 0)
                {
                    points.Add(new GeoPoint3(p.X + 4E-4, p.Y, p.Z));
                    bulges.Add(0);
                    normals.Add(default(GeoVector3));
                }
            }

            if (rnd.Next(10) == 0)
            {
                points.Add(points[0]);
                bulges.Add(0.4);
                normals.Add(normal);
            }

            if (rnd.Next(30) == 0)
            {
                bulges[rnd.Next(bulges.Count)] = rnd.Next(2) == 0 ? double.NaN : double.PositiveInfinity;
            }

            if (rnd.Next(15) == 0 && bulges.Count > 2)
            {
                bulges.RemoveRange(bulges.Count - 2, 2);
            }

            if (rnd.Next(15) == 0 && normals.Count > 2)
            {
                normals.RemoveRange(normals.Count - 2, 2);
            }

            Tolerance[] tolerances =
            {
                Tolerance.Default,
                Tolerance.Global,
                new Tolerance(1E-6, 1E-6),
                new Tolerance(0.05, 1E-5),
                default(Tolerance),
            };
            var generated = new Generated
            {
                Points = points,
                Bulges = bulges,
                Normals = normals,
                Tolerance = tolerances[rnd.Next(tolerances.Length)],
                VertexForm = rnd.Next(4),
                GivenForm = rnd.Next(4),
            };
            return generated;
        }

        #endregion
    }
}
