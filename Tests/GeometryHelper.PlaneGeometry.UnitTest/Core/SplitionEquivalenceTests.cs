using System;
using System.Linq;
using GeometryHelper.CommonGeometry;
using GeometryHelper.PlaneGeometry.Core;
using GeometryHelper.PlaneGeometry.Geometry;
using Xunit;

namespace GeometryHelper.PlaneGeometry.UnitTest.Core
{
    /// <summary>
    /// Every split that can be asked two ways, asked both ways on the same geometry.
    /// <para>
    /// Each cutter type has a single cutter overload and an array overload, and an array holding one
    /// cutter asks exactly the question the single overload answers. They reach that answer through
    /// different code, though, so nothing forces them to agree — and three times already they had not:
    /// over whether a point off the subject is refused, over what the out parameters hold when nothing
    /// was cut, and over whether a boundary counts as inside. Those were each found one at a time. This
    /// asks the whole matrix at once, on generated geometry, so the next divergence is found by the suite
    /// rather than by inspection.
    /// </para>
    /// </summary>
    public class SplitionEquivalenceTests
    {
        private const int Cases = 300;
        private static readonly Tolerance Tol = new Tolerance(1E-4, 1E-4);

        private static GeoPoint2 Somewhere(Random random) =>
            new GeoPoint2(random.NextDouble() * 30.0 - 10.0, random.NextDouble() * 30.0 - 10.0);

        private static GeoLine2 RandomLine(Random random) =>
            new GeoLine2(Somewhere(random), Somewhere(random));

        private static GeoPolyline2 RandomPath(Random random)
        {
            var vertices = new GeoPoint2[random.Next(2, 5)];
            double x = -10.0;
            for (int i = 0; i < vertices.Length; i++)
            {
                vertices[i] = new GeoPoint2(x, random.NextDouble() * 20.0 - 10.0);
                x += 1.0 + random.NextDouble() * 9.0;
            }
            return new GeoPolyline2(vertices);
        }

        private static GeoPolygon2 RandomSquare(Random random)
        {
            double x = random.NextDouble() * 10.0 - 5.0;
            double y = random.NextDouble() * 10.0 - 5.0;
            double size = 2.0 + random.NextDouble() * 8.0;
            return new GeoPolygon2(
                new GeoPoint2(x, y), new GeoPoint2(x + size, y),
                new GeoPoint2(x + size, y + size), new GeoPoint2(x, y + size));
        }

        private static void SameLines(GeoLine2[] a, GeoLine2[] b, string what, int c)
        {
            Assert.True(a.Length == b.Length, $"case {c}: {what} count {a.Length} vs {b.Length}");
            for (int i = 0; i < a.Length; i++)
            {
                Assert.True(a[i].StartPoint.IsEqualTo(b[i].StartPoint, Tol), $"case {c}: {what} start {i}");
                Assert.True(a[i].EndPoint.IsEqualTo(b[i].EndPoint, Tol), $"case {c}: {what} end {i}");
            }
        }

        private static void SamePaths(GeoPolyline2[] a, GeoPolyline2[] b, string what, int c)
        {
            Assert.True(a.Length == b.Length, $"case {c}: {what} count {a.Length} vs {b.Length}");
            for (int i = 0; i < a.Length; i++)
            {
                Assert.True(a[i].VertexCount == b[i].VertexCount,
                    $"case {c}: {what} piece {i} has {a[i].VertexCount} vs {b[i].VertexCount} vertices");
                for (int v = 0; v < a[i].VertexCount; v++)
                {
                    Assert.True(a[i][v].IsEqualTo(b[i][v], Tol), $"case {c}: {what} piece {i} vertex {v}");
                }
            }
        }

        [Fact]
        public void Line_CutByOneLine_AnswersTheSameSinglyAndInAnArray()
        {
            var random = new Random(1);

            for (int c = 0; c < Cases; c++)
            {
                GeoLine2 subject = RandomLine(random);
                GeoLine2 cutter = RandomLine(random);

                bool single = subject.TrySplitBy(cutter, out GeoLine2 first, out GeoLine2 second, Tol);
                bool array = subject.TrySplitBy(new[] { cutter }, out GeoLine2[] pieces, Tol);

                Assert.True(single == array, $"case {c}: single {single} vs array {array}");
                SameLines(single ? new[] { first, second } : new[] { subject }, pieces, "line by line", c);
            }
        }

        [Fact]
        public void Line_CutByOnePolygon_AnswersTheSameSinglyAndInAnArray()
        {
            var random = new Random(2);

            for (int c = 0; c < Cases; c++)
            {
                GeoLine2 subject = RandomLine(random);
                GeoPolygon2 cutter = RandomSquare(random);

                bool single = subject.TrySplitBy(cutter, out GeoLine2[] inA, out GeoLine2[] outA, Tol);
                bool array = subject.TrySplitBy(new[] { cutter }, out GeoLine2[] inB, out GeoLine2[] outB, Tol);

                Assert.True(single == array, $"case {c}: single {single} vs array {array}");
                SameLines(inA, inB, "inside", c);
                SameLines(outA, outB, "outside", c);
            }
        }

        [Fact]
        public void Line_CutByOnePolylineCutter_AnswersTheSameSinglyAndInAnArray()
        {
            var random = new Random(3);

            for (int c = 0; c < Cases; c++)
            {
                GeoLine2 subject = RandomLine(random);
                GeoPolyline2 cutter = RandomPath(random);

                bool single = subject.TrySplitBy(cutter, out GeoLine2[] a, Tol);
                bool array = subject.TrySplitBy(new[] { cutter }, out GeoLine2[] b, Tol);

                Assert.True(single == array, $"case {c}: single {single} vs array {array}");
                SameLines(a, b, "line by polyline", c);
            }
        }

        [Fact]
        public void Polyline_CutByOneLine_AnswersTheSameSinglyAndInAnArray()
        {
            var random = new Random(4);

            for (int c = 0; c < Cases; c++)
            {
                GeoPolyline2 subject = RandomPath(random);
                GeoLine2 cutter = RandomLine(random);

                bool single = subject.TrySplitBy(cutter, out GeoPolyline2[] a, Tol);
                bool array = subject.TrySplitBy(new[] { cutter }, out GeoPolyline2[] b, Tol);

                Assert.True(single == array, $"case {c}: single {single} vs array {array}");
                SamePaths(a, b, "polyline by line", c);
            }
        }

        [Fact]
        public void Polyline_CutByOnePolygon_AnswersTheSameSinglyAndInAnArray()
        {
            var random = new Random(5);

            for (int c = 0; c < Cases; c++)
            {
                GeoPolyline2 subject = RandomPath(random);
                GeoPolygon2 cutter = RandomSquare(random);

                bool single = subject.TrySplitBy(cutter, out GeoPolyline2[] inA, out GeoPolyline2[] outA, Tol);
                bool array = subject.TrySplitBy(new[] { cutter }, out GeoPolyline2[] inB, out GeoPolyline2[] outB, Tol);

                Assert.True(single == array, $"case {c}: single {single} vs array {array}");
                SamePaths(inA, inB, "inside", c);
                SamePaths(outA, outB, "outside", c);
            }
        }

        [Fact]
        public void CuttingAtAPoint_AnswersTheSameSinglyAndInAnArray()
        {
            var random = new Random(6);

            for (int c = 0; c < Cases; c++)
            {
                GeoLine2 subject = RandomLine(random);

                // A point genuinely on the subject, plus one that is not.
                GeoPoint2 onIt = subject.GetPointAtDistance(subject.Length * (0.05 + random.NextDouble() * 0.9));
                GeoPoint2 offIt = new GeoPoint2(onIt.X, onIt.Y + 3.0);

                foreach (GeoPoint2 probe in new[] { onIt, offIt })
                {
                    bool single = subject.TrySplitBy(probe, out GeoLine2 first, out GeoLine2 second, Tol);
                    bool array = subject.TrySplitBy(new[] { probe }, out GeoLine2[] pieces, Tol);

                    Assert.True(single == array, $"case {c}: single {single} vs array {array}");
                    SameLines(single ? new[] { first, second } : new[] { subject }, pieces, "at a point", c);
                }
            }
        }

        [Fact]
        public void EveryForm_PreservesTheSubjectLength()
        {
            var random = new Random(7);

            for (int c = 0; c < Cases; c++)
            {
                GeoPolyline2 subject = RandomPath(random);
                GeoLine2 cutter = RandomLine(random);
                GeoPolygon2 polygon = RandomSquare(random);

                subject.TrySplitBy(cutter, out GeoPolyline2[] byCutter, Tol);
                subject.TrySplitBy(new[] { cutter }, out GeoPolyline2[] byCutterArray, Tol);
                subject.TrySplitBy(polygon, out GeoPolyline2[] inside, out GeoPolyline2[] outside, Tol);
                subject.TrySplitBy(new[] { polygon }, out GeoPolyline2[] within, out GeoPolyline2[] beyond, Tol);

                Assert.Equal(subject.Length, byCutter.Sum(p => p.Length), 6);
                Assert.Equal(subject.Length, byCutterArray.Sum(p => p.Length), 6);
                Assert.Equal(subject.Length, inside.Sum(p => p.Length) + outside.Sum(p => p.Length), 6);
                Assert.Equal(subject.Length, within.Sum(p => p.Length) + beyond.Sum(p => p.Length), 6);
            }
        }

        [Fact]
        public void TheSameSegment_SplitsTheSameWhetherItIsALineOrATwoVertexPolyline()
        {
            // The second axis along which these overloads can drift. A segment expressed as a polyline is
            // the same geometry, but it travels through the polyline code: different splitter, different
            // merger. Where the line form joins two touching pieces into one straight segment, the
            // polyline form has to reach the same shape by dropping the junction rather than by having no
            // way to keep it.
            var random = new Random(8);

            for (int c = 0; c < Cases; c++)
            {
                GeoLine2 asLine = RandomLine(random);
                var asPath = new GeoPolyline2(asLine.StartPoint, asLine.EndPoint);

                GeoPolygon2 one = RandomSquare(random);
                GeoPolygon2 two = RandomSquare(random);
                var cutters = new[] { one, two };

                bool lineOk = asLine.TrySplitBy(cutters, out GeoLine2[] lineIn, out GeoLine2[] lineOut, Tol);
                bool pathOk = asPath.TrySplitBy(cutters, out GeoPolyline2[] pathIn, out GeoPolyline2[] pathOut, Tol);

                Assert.True(lineOk == pathOk, $"case {c}: line {lineOk} vs path {pathOk}");
                Assert.True(lineIn.Length == pathIn.Length,
                    $"case {c}: inside {lineIn.Length} vs {pathIn.Length}");
                Assert.True(lineOut.Length == pathOut.Length,
                    $"case {c}: outside {lineOut.Length} vs {pathOut.Length}");

                // A straight subject can only produce straight runs, so every merged run is two vertices.
                foreach (GeoPolyline2 run in pathIn.Concat(pathOut))
                {
                    Assert.True(run.VertexCount == 2,
                        $"case {c}: a straight run came back with {run.VertexCount} vertices");
                }

                for (int i = 0; i < lineIn.Length; i++)
                {
                    Assert.True(lineIn[i].StartPoint.IsEqualTo(pathIn[i][0], Tol), $"case {c}: inside start {i}");
                    Assert.True(lineIn[i].EndPoint.IsEqualTo(pathIn[i][pathIn[i].VertexCount - 1], Tol),
                        $"case {c}: inside end {i}");
                }
            }
        }

    }
}
