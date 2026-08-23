using System;
using System.Collections.Generic;
using System.Linq;
using CommonGeometry;
using PlaneGeometry.Core;
using PlaneGeometry.Geometry;
using Xunit;

namespace PlaneGeometry.UnitTest.Core
{
    /// <summary>
    /// Properties that must hold for every split, checked against generated geometry rather than
    /// hand-picked examples.
    /// <para>
    /// Splitting is the first operation in the library that builds new geometry instead of answering a
    /// question about existing geometry, and its failures live in configurations nobody thinks to write a
    /// case for ??a cut landing a hair off a vertex, two cuts landing on the same one, a path doubling
    /// back on itself. Driving the same invariants over many generated shapes reaches those; a fixed seed
    /// keeps any failure reproducible.
    /// </para>
    /// </summary>
    public class SplitionInvariantTests
    {
        private const int Seed = 42;
        private const int Cases = 200;

        private static readonly Tolerance Tol = new Tolerance(1E-4, 1E-4);

        /// <summary>
        /// Builds a path whose X always advances, so it can never cross itself. Self-intersection would
        /// make "the arc length of this point" ambiguous, which is a separate concern from the ones these
        /// invariants are checking.
        /// </summary>
        private static GeoPolyline2 RandomPath(Random random, int vertexCount)
        {
            var vertices = new GeoPoint2[vertexCount];
            double x = 0.0;

            for (int i = 0; i < vertexCount; i++)
            {
                vertices[i] = new GeoPoint2(x, random.NextDouble() * 20.0 - 10.0);
                x += 1.0 + random.NextDouble() * 14.0;
            }

            return new GeoPolyline2(vertices);
        }

        private static double[] RandomCuts(Random random, double totalLength, int count)
        {
            var cuts = new double[count];
            for (int i = 0; i < count; i++)
            {
                cuts[i] = random.NextDouble() * totalLength;
            }
            return cuts;
        }

        [Fact]
        public void Polyline_PiecesCoverTheSubjectExactlyOnce()
        {
            var random = new Random(Seed);

            for (int c = 0; c < Cases; c++)
            {
                GeoPolyline2 path = RandomPath(random, random.Next(2, 9));
                double[] cuts = RandomCuts(random, path.Length, random.Next(0, 6));

                GeoPolyline2[] pieces = Splition2.SplitAtDistances(path, cuts, Tol);

                // Nothing is lost and nothing is counted twice.
                Assert.Equal(path.Length, pieces.Sum(p => p.Length), 6);

                // The pieces form one unbroken chain in order along the subject.
                for (int i = 1; i < pieces.Length; i++)
                {
                    GeoPoint2 end = pieces[i - 1][pieces[i - 1].VertexCount - 1];
                    GeoPoint2 start = pieces[i][0];
                    Assert.True(end.IsEqualTo(start, Tol),
                        $"case {c}: piece {i - 1} ends at {end} but piece {i} starts at {start}");
                }

                // An open subject keeps its own endpoints at the outer edges of the result.
                Assert.True(pieces[0][0].IsEqualTo(path[0], Tol));
                GeoPolyline2 last = pieces[pieces.Length - 1];
                Assert.True(last[last.VertexCount - 1].IsEqualTo(path[path.VertexCount - 1], Tol));
            }
        }

        [Fact]
        public void Polyline_NoPieceIsDegenerate()
        {
            var random = new Random(Seed);

            for (int c = 0; c < Cases; c++)
            {
                GeoPolyline2 path = RandomPath(random, random.Next(2, 9));

                // Deliberately cluster cuts: pairs a hair apart, and pairs a hair off a vertex. These are
                // the positions that would produce slivers if merging and snapping did not happen.
                var cuts = new List<double>(RandomCuts(random, path.Length, 4));
                foreach (double cut in cuts.ToArray())
                {
                    cuts.Add(cut + Tol.EqualPoint * 0.4);
                }
                double accumulated = 0.0;
                for (int e = 0; e < path.EdgeCount; e++)
                {
                    accumulated += path.GetEdgeAt(e).Length;
                    cuts.Add(accumulated + Tol.EqualPoint * 0.3);
                    cuts.Add(accumulated - Tol.EqualPoint * 0.3);

                    // Straddling the vertex by 0.9 of the tolerance on each side is the case that slips
                    // past merging ??the pair is 1.8 tolerances apart, so both survive ??yet both land
                    // within snapping range of the same vertex and collapse onto it.
                    cuts.Add(accumulated + Tol.EqualPoint * 0.9);
                    cuts.Add(accumulated - Tol.EqualPoint * 0.9);
                }

                GeoPolyline2[] pieces = Splition2.SplitAtDistances(path, cuts, Tol);

                // A full tolerance is the real bound, not some fraction of one. Merging keeps two cuts
                // at least that far apart, and snapping either pulls a cut onto a vertex or leaves it at
                // least that far clear of one, so nothing shorter can be built. Testing against half a
                // tolerance would wave through exactly the slivers those two rules exist to prevent.
                double floor = Tol.EqualPoint * 0.99;

                foreach (GeoPolyline2 piece in pieces)
                {
                    Assert.True(piece.VertexCount >= 2, $"case {c}: piece with {piece.VertexCount} vertices");
                    Assert.True(piece.Length > floor,
                        $"case {c}: piece only {piece.Length} long");

                    // A sliver edge does not shorten the piece that holds it, so the piece length above
                    // would not notice one; only the edges show it.
                    for (int e = 0; e < piece.EdgeCount; e++)
                    {
                        Assert.True(piece.GetEdgeAt(e).Length > floor,
                            $"case {c}: sliver edge {e} of {piece.GetEdgeAt(e).Length}");
                    }
                }

                Assert.Equal(path.Length, pieces.Sum(p => p.Length), 6);
            }
        }

        [Fact]
        public void Polyline_SplittingIsSymmetricUnderReversal()
        {
            var random = new Random(Seed);

            for (int c = 0; c < Cases; c++)
            {
                GeoPolyline2 path = RandomPath(random, random.Next(2, 9));
                double length = path.Length;
                double[] cuts = RandomCuts(random, length, random.Next(1, 5));

                GeoPolyline2[] forward = Splition2.SplitAtDistances(path, cuts, Tol);

                // The same cuts measured from the other end of the same path.
                double[] mirrored = cuts.Select(cut => length - cut).ToArray();
                GeoPolyline2[] backward = Splition2.SplitAtDistances(path.Reverse(), mirrored, Tol);

                Assert.Equal(forward.Length, backward.Length);

                // Walking the reversed result backwards must retrace the forward one.
                for (int i = 0; i < forward.Length; i++)
                {
                    GeoPolyline2 expected = forward[i];
                    GeoPolyline2 actual = backward[backward.Length - 1 - i].Reverse();

                    Assert.Equal(expected.VertexCount, actual.VertexCount);
                    for (int v = 0; v < expected.VertexCount; v++)
                    {
                        Assert.True(expected[v].IsEqualTo(actual[v], Tol),
                            $"case {c}: piece {i} vertex {v}: {expected[v]} vs {actual[v]}");
                    }
                }
            }
        }

        [Fact]
        public void Polyline_SplittingByAPointMatchesSplittingAtItsDistance()
        {
            var random = new Random(Seed);

            for (int c = 0; c < Cases; c++)
            {
                GeoPolyline2 path = RandomPath(random, random.Next(2, 9));
                double length = path.Length;

                // Keep clear of both ends, where neither form splits at all.
                double distance = length * (0.05 + random.NextDouble() * 0.9);
                GeoPoint2 point = path.GetPointAtDistance(distance);

                bool byDistance = Splition2.TrySplitAtDistance(path, distance, out GeoPolyline2 a1, out GeoPolyline2 a2, Tol);
                bool byPoint = Splition2.TrySplitBy(path, point, out GeoPolyline2 b1, out GeoPolyline2 b2, Tol);

                Assert.Equal(byDistance, byPoint);
                if (!byDistance) continue;

                Assert.Equal(a1.Length, b1.Length, 6);
                Assert.Equal(a2.Length, b2.Length, 6);
                Assert.True(a1[a1.VertexCount - 1].IsEqualTo(b1[b1.VertexCount - 1], Tol));
            }
        }

        [Fact]
        public void Polyline_SplittingByALineMatchesSplittingAtTheCrossings()
        {
            var random = new Random(Seed);

            for (int c = 0; c < Cases; c++)
            {
                GeoPolyline2 path = RandomPath(random, random.Next(2, 9));

                // A long horizontal cutter that the path crosses wherever it changes sign in Y.
                double y = random.NextDouble() * 10.0 - 5.0;
                double right = path[path.VertexCount - 1].X;
                var cutter = new GeoLine2(new GeoPoint2(-50, y), new GeoPoint2(right + 50, y));

                if (Splition2.TrySplitBy(path, cutter, out GeoPolyline2[] pieces, Tol))
                {
                    Assert.Equal(path.Length, pieces.Sum(p => p.Length), 6);

                    for (int i = 1; i < pieces.Length; i++)
                    {
                        Assert.True(pieces[i - 1][pieces[i - 1].VertexCount - 1].IsEqualTo(pieces[i][0], Tol));
                    }

                    // Every internal boundary is a point where the cutter actually met the path.
                    for (int i = 1; i < pieces.Length; i++)
                    {
                        Assert.True(Containment2.IsPointOn(cutter, pieces[i][0], Tol),
                            $"case {c}: boundary {i} at {pieces[i][0]} is not on the cutter");
                    }
                }
            }
        }

        [Fact]
        public void Line_PiecesCoverTheSegmentExactlyOnce()
        {
            var random = new Random(Seed);

            for (int c = 0; c < Cases; c++)
            {
                var line = new GeoLine2(
                    new GeoPoint2(random.NextDouble() * 20.0 - 10.0, random.NextDouble() * 20.0 - 10.0),
                    new GeoPoint2(random.NextDouble() * 20.0 + 10.0, random.NextDouble() * 20.0 + 10.0));

                double[] cuts = RandomCuts(random, line.Length, random.Next(0, 6));
                GeoLine2[] pieces = Splition2.SplitAtDistances(line, cuts, Tol);

                Assert.Equal(line.Length, pieces.Sum(p => p.Length), 6);
                Assert.True(pieces[0].StartPoint.IsEqualTo(line.StartPoint, Tol));
                Assert.True(pieces[pieces.Length - 1].EndPoint.IsEqualTo(line.EndPoint, Tol));

                for (int i = 1; i < pieces.Length; i++)
                {
                    Assert.True(pieces[i - 1].EndPoint.IsEqualTo(pieces[i].StartPoint, Tol));
                }
            }
        }

        [Fact]
        public void NoValidInputThrows()
        {
            var random = new Random(Seed);

            for (int c = 0; c < Cases; c++)
            {
                GeoPolyline2 path = RandomPath(random, random.Next(2, 9));
                double length = path.Length;

                // Wild positions on purpose: far outside, exactly on the ends, NaN and infinities.
                var cuts = new List<double>
                {
                    0.0, length, -length, length * 3.0,
                    double.NaN, double.PositiveInfinity, double.NegativeInfinity,
                    random.NextDouble() * length
                };

                GeoPolyline2[] pieces = Splition2.SplitAtDistances(path, cuts, Tol);
                Assert.NotEmpty(pieces);

                Splition2.TrySplitAtDistance(path, double.NaN, out _, out _, Tol);
                Splition2.TrySplitBy(path, new GeoPoint2(double.NaN, double.NaN), out GeoPolyline2 _, out GeoPolyline2 _, Tol);
                Splition2.TrySplitBy(path, new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(0, 0)), out _, Tol);
            }
        }
    }
}
