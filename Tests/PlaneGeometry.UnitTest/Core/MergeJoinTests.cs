using System;
using System.Collections.Generic;
using System.Diagnostics;
using CommonGeometry;
using PlaneGeometry.Core;
using PlaneGeometry.Geometry;
using Xunit;

namespace PlaneGeometry.UnitTest.Core
{
    /// <summary>
    /// Covers the parts of <see cref="Merge2"/> that guard against bad input, and the parts of
    /// <see cref="Merge2.Join"/> that decide how a run is grown rather than what it looks like once grown.
    /// </summary>
    public class MergeJoinTests
    {
        #region Argument validation

        [Fact]
        public void ConsecutiveLines_NullSegments_Throws()
        {
            var ex = Assert.Throws<ArgumentNullException>(() => Merge2.ConsecutiveLines(null, Tolerance.Global));
            Assert.Equal("segments", ex.ParamName);
        }

        [Fact]
        public void ConsecutivePolylines_NullPolylines_Throws()
        {
            var ex = Assert.Throws<ArgumentNullException>(() => Merge2.ConsecutivePolylines(null, Tolerance.Global));
            Assert.Equal("polylines", ex.ParamName);
        }

        [Fact]
        public void Polylines_NullFirst_Throws()
        {
            var second = new GeoPolyline2(new GeoPoint2(0, 0), new GeoPoint2(1, 0));
            var ex = Assert.Throws<ArgumentNullException>(() => Merge2.Polylines(null, second, Tolerance.Global));
            Assert.Equal("first", ex.ParamName);
        }

        [Fact]
        public void Polylines_NullSecond_Throws()
        {
            var first = new GeoPolyline2(new GeoPoint2(0, 0), new GeoPoint2(1, 0));
            var ex = Assert.Throws<ArgumentNullException>(() => Merge2.Polylines(first, null, Tolerance.Global));
            Assert.Equal("second", ex.ParamName);
        }

        [Fact]
        public void Join_NullLines_Throws()
        {
            var ex = Assert.Throws<ArgumentNullException>(() => Merge2.Join(null, Tolerance.Global));
            Assert.Equal("lines", ex.ParamName);
        }

        #endregion

        #region Degenerate input

        [Fact]
        public void Join_LoneZeroLengthSegment_ReturnsNothing()
        {
            // The public GeoPolyline2 constructor rejects two coincident vertices, so Join must not be
            // able to hand one back through the trusted constructor it uses internally.
            var segments = new[] { new GeoLine2(new GeoPoint2(3, 4), new GeoPoint2(3, 4)) };

            var result = Merge2.Join(segments, Tolerance.Global);

            Assert.Empty(result);
        }

        [Fact]
        public void Join_AllSegmentsZeroLength_ReturnsNothing()
        {
            var segments = new[]
            {
                new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(0, 0)),
                new GeoLine2(new GeoPoint2(5, 5), new GeoPoint2(5, 5)),
                new GeoLine2(new GeoPoint2(9, 1), new GeoPoint2(9, 1))
            };

            var result = Merge2.Join(segments, Tolerance.Global);

            Assert.Empty(result);
        }

        [Fact]
        public void Join_ZeroLengthSegmentAmongRealOnes_IsIgnored()
        {
            var segments = new[]
            {
                new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(5, 0)),
                new GeoLine2(new GeoPoint2(5, 0), new GeoPoint2(5, 0)),   // sits on the junction
                new GeoLine2(new GeoPoint2(5, 0), new GeoPoint2(5, 5))
            };

            var result = Merge2.Join(segments, Tolerance.Global);

            Assert.Single(result);
            AssertVertices(result[0], new GeoPoint2(0, 0), new GeoPoint2(5, 0), new GeoPoint2(5, 5));
        }

        [Fact]
        public void Join_SegmentShorterThanTolerance_CountsAsZeroLength()
        {
            var tolerance = new Tolerance(0.5, 0.5);
            var segments = new[] { new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(0.1, 0)) };

            Assert.Empty(Merge2.Join(segments, tolerance));

            // The same segment survives once the tolerance no longer swallows it.
            Assert.Single(Merge2.Join(segments, new Tolerance(0.01, 0.01)));
        }

        [Fact]
        public void Join_EmptyInput_ReturnsNothing()
        {
            Assert.Empty(Merge2.Join(new List<GeoLine2>(), Tolerance.Global));
        }

        [Fact]
        public void Join_EveryResultHasDistinctNeighbouringVertices()
        {
            // Runs that fold back, close up, or start from a degenerate segment are the ways a
            // coincident pair of vertices could reach the output.
            var awkward = new List<GeoLine2[]>
            {
                new[]
                {
                    new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(5, 0)),
                    new GeoLine2(new GeoPoint2(5, 0), new GeoPoint2(0, 0))      // straight back on itself
                },
                new[]
                {
                    new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(10, 0)),
                    new GeoLine2(new GeoPoint2(10, 0), new GeoPoint2(4, 0)),    // folds back
                    new GeoLine2(new GeoPoint2(4, 0), new GeoPoint2(4, 4))
                },
                new[]
                {
                    new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(4, 0)),
                    new GeoLine2(new GeoPoint2(4, 0), new GeoPoint2(4, 4)),
                    new GeoLine2(new GeoPoint2(4, 4), new GeoPoint2(0, 0)),     // closes the loop
                    new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(0, 0))      // degenerate on the seam
                }
            };

            foreach (var segments in awkward)
            {
                foreach (var polyline in Merge2.Join(segments, Tolerance.Global))
                {
                    Assert.True(polyline.VertexCount >= 2, "a run reached the output with fewer than 2 vertices");
                    for (int i = 1; i < polyline.VertexCount; i++)
                    {
                        Assert.False(
                            polyline[i].IsEqualTo(polyline[i - 1], Tolerance.Global),
                            "vertices " + (i - 1) + " and " + i + " coincide: " + polyline[i]);
                    }
                }
            }
        }

        #endregion

        #region Growing a run

        [Fact]
        public void Join_SeedInTheMiddle_GrowsBothWays()
        {
            // The middle segment comes first, so the run has to grow backwards as well as forwards.
            var segments = new[]
            {
                new GeoLine2(new GeoPoint2(5, 0), new GeoPoint2(5, 5)),
                new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(5, 0)),
                new GeoLine2(new GeoPoint2(5, 5), new GeoPoint2(10, 5))
            };

            var result = Merge2.Join(segments, Tolerance.Global);

            Assert.Single(result);
            AssertVertices(result[0], new GeoPoint2(0, 0), new GeoPoint2(5, 0), new GeoPoint2(5, 5), new GeoPoint2(10, 5));
        }

        [Fact]
        public void Join_SegmentsPointingTheWrongWay_AreTurnedRound()
        {
            // Every segment after the first is stored end-to-start, so each one has to be reversed.
            var segments = new[]
            {
                new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(5, 0)),
                new GeoLine2(new GeoPoint2(5, 5), new GeoPoint2(5, 0)),
                new GeoLine2(new GeoPoint2(10, 5), new GeoPoint2(5, 5))
            };

            var result = Merge2.Join(segments, Tolerance.Global);

            Assert.Single(result);
            AssertVertices(result[0], new GeoPoint2(0, 0), new GeoPoint2(5, 0), new GeoPoint2(5, 5), new GeoPoint2(10, 5));
        }

        [Fact]
        public void Join_ClosedSquare_ComesBackAsOneClosedRun()
        {
            var segments = new[]
            {
                new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(10, 0)),
                new GeoLine2(new GeoPoint2(10, 0), new GeoPoint2(10, 10)),
                new GeoLine2(new GeoPoint2(10, 10), new GeoPoint2(0, 10)),
                new GeoLine2(new GeoPoint2(0, 10), new GeoPoint2(0, 0))
            };

            var result = Merge2.Join(segments, Tolerance.Global);

            Assert.Single(result);
            Assert.Equal(5, result[0].VertexCount);
            Assert.True(result[0][0].IsEqualTo(result[0][4], Tolerance.Global), "the run should close on itself");
            Assert.Equal(40.0, result[0].Length, 9);
        }

        [Fact]
        public void Join_NoTwoResultsTouchEndToEnd()
        {
            // Anything that could still be joined should already have been, whatever order it arrived in.
            var segments = ShuffledStaircases(chains: 4, segmentsPerChain: 6, seed: 7);

            var result = Merge2.Join(segments, Tolerance.Global);

            for (int i = 0; i < result.Length; i++)
            {
                for (int j = i + 1; j < result.Length; j++)
                {
                    foreach (var a in Ends(result[i]))
                    {
                        foreach (var b in Ends(result[j]))
                        {
                            Assert.False(a.IsEqualTo(b, Tolerance.Global), "runs " + i + " and " + j + " still meet at " + a);
                        }
                    }
                }
            }
        }

        [Fact]
        public void Join_ForkedInput_GivesTheSameAnswerEveryTime()
        {
            // Three segments at one point is a fork, and which branch the run takes is settled by input
            // order rather than by geometry. That is allowed to be arbitrary, but not to wander.
            var segments = new[]
            {
                new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(5, 0)),
                new GeoLine2(new GeoPoint2(5, 0), new GeoPoint2(10, 0)),
                new GeoLine2(new GeoPoint2(5, 0), new GeoPoint2(5, 5))
            };

            var first = Describe(Merge2.Join(segments, Tolerance.Global));
            for (int attempt = 0; attempt < 5; attempt++)
            {
                Assert.Equal(first, Describe(Merge2.Join(segments, Tolerance.Global)));
            }

            // Both branches survive; the fork splits the input into two runs rather than losing one.
            Assert.Equal(2, Merge2.Join(segments, Tolerance.Global).Length);
        }

        #endregion

        #region Tolerance

        [Fact]
        public void Join_GapWithinTolerance_IsBridged()
        {
            var segments = new[]
            {
                new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(5, 0)),
                new GeoLine2(new GeoPoint2(5.5, 0), new GeoPoint2(5.5, 5))   // 0.5 away from the first end
            };

            Assert.Single(Merge2.Join(segments, new Tolerance(1.0, 1.0)));
            Assert.Equal(2, Merge2.Join(segments, new Tolerance(0.1, 0.1)).Length);
        }

        [Fact]
        public void Join_ZeroTolerance_MatchesExactEndpointsOnly()
        {
            // A zero tolerance cannot be used as a grid cell size, so the grid falls back to a floor.
            // These runs still have to be found, and near misses still have to be turned away.
            var exact = new[]
            {
                new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(5, 0)),
                new GeoLine2(new GeoPoint2(5, 0), new GeoPoint2(5, 5))
            };
            var nearMiss = new[]
            {
                new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(5, 0)),
                new GeoLine2(new GeoPoint2(5, 1E-12), new GeoPoint2(5, 5))
            };

            Assert.Single(Merge2.Join(exact, new Tolerance(0.0, 0.0)));
            Assert.Equal(2, Merge2.Join(nearMiss, new Tolerance(0.0, 0.0)).Length);
        }

        [Fact]
        public void Join_ZeroTolerance_JoinsRunsMeetingAtTheOrigin()
        {
            // Dividing a coordinate by the cell size gives infinity at a zero tolerance, and at the
            // origin it gives not-a-number instead. Both have to land somewhere the search can find.
            var zero = new Tolerance(0.0, 0.0);
            var throughOrigin = new[]
            {
                new GeoLine2(new GeoPoint2(-5, 0), new GeoPoint2(0, 0)),
                new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(0, 5))
            };
            var awayFromOrigin = new[]
            {
                new GeoLine2(new GeoPoint2(7, 3), new GeoPoint2(12, 3)),
                new GeoLine2(new GeoPoint2(12, 3), new GeoPoint2(12, 8))
            };

            Assert.Single(Merge2.Join(throughOrigin, zero));
            Assert.Single(Merge2.Join(awayFromOrigin, zero));

            // Together they are two runs, not one: nothing has been swept into a shared bucket and
            // joined by accident.
            var both = new List<GeoLine2>(throughOrigin);
            both.AddRange(awayFromOrigin);
            Assert.Equal(2, Merge2.Join(both, zero).Length);
        }

        [Fact]
        public void Join_ZeroTolerance_StillSpreadsSegmentsOverTheGrid()
        {
            // Without a floor under the cell size every endpoint divides by zero, lands in one cell,
            // and the search degrades into a scan of the whole input. The answer is the same either
            // way, so the cost is the only thing that shows it up.
            const int count = 40000;
            var zero = new Tolerance(0.0, 0.0);
            var segments = new List<GeoLine2>(count);
            for (int i = 0; i < count; i++)
            {
                segments.Add(new GeoLine2(new GeoPoint2(i, i % 2), new GeoPoint2(i + 1, (i + 1) % 2)));
            }

            var watch = Stopwatch.StartNew();
            var result = Merge2.Join(segments, zero);
            watch.Stop();

            Assert.Single(result);
            Assert.Equal(count + 1, result[0].VertexCount);
            Assert.True(
                watch.Elapsed < TimeSpan.FromSeconds(3),
                "joining " + count + " segments at a zero tolerance took " + watch.ElapsedMilliseconds + " ms");
        }

        [Fact]
        public void Join_EndpointsStraddlingAGridCellBoundary_StillMeet()
        {
            // Cells are one tolerance wide, so two ends that touch can land in different cells. The
            // search has to look at the neighbouring cells or this pair is missed.
            var tolerance = new Tolerance(1.0, 1.0);
            for (int step = 0; step <= 20; step++)
            {
                double x = step * 0.1;   // walks across several cell boundaries
                var segments = new[]
                {
                    new GeoLine2(new GeoPoint2(x - 4, 0), new GeoPoint2(x, 0)),
                    new GeoLine2(new GeoPoint2(x + 0.4, 0), new GeoPoint2(x + 0.4, 5))  // 0.4 away, inside tolerance
                };

                Assert.True(
                    Merge2.Join(segments, tolerance).Length == 1,
                    "the pair at x=" + x + " was not joined");
            }
        }

        #endregion

        #region Structure preserved

        [Fact]
        public void Join_KnownChains_ComeBackWholeWhateverTheInputOrder()
        {
            const int chains = 5;
            const int perChain = 7;
            var expected = Staircases(chains, perChain, out var segments);

            var shuffled = Shuffle(segments, seed: 31);
            var result = Merge2.Join(shuffled, Tolerance.Global);

            Assert.Equal(chains, result.Length);

            var outstanding = new List<string>();
            foreach (var chain in expected)
            {
                outstanding.Add(Describe(chain));
            }

            foreach (var polyline in result)
            {
                // A run has no inherent direction, so either reading of it settles the debt.
                string forward = Describe(Vertices(polyline));
                var backward = Vertices(polyline);
                backward.Reverse();

                Assert.True(
                    outstanding.Remove(forward) || outstanding.Remove(Describe(backward)),
                    "unexpected run: " + forward);
            }

            Assert.Empty(outstanding);
        }

        [Fact]
        public void Join_TotalLengthIsPreserved()
        {
            var segments = ShuffledStaircases(chains: 3, segmentsPerChain: 9, seed: 99);

            double before = 0.0;
            foreach (var segment in segments)
            {
                before += segment.Length;
            }

            double after = 0.0;
            foreach (var polyline in Merge2.Join(segments, Tolerance.Global))
            {
                after += polyline.Length;
            }

            Assert.Equal(before, after, 9);
        }

        #endregion

        #region Scaling

        [Fact]
        public void Join_LargeInput_DoesNotScaleWithTheSquareOfTheCount()
        {
            // Comparing every pair of runs took about 50 seconds for this many segments. The margin
            // here is wide enough not to trip on a slow machine and still far below that.
            const int count = 40000;
            var segments = new List<GeoLine2>(count);
            for (int i = 0; i < count; i++)
            {
                segments.Add(new GeoLine2(new GeoPoint2(i, i % 2), new GeoPoint2(i + 1, (i + 1) % 2)));
            }

            var shuffled = Shuffle(segments.ToArray(), seed: 5);

            var watch = Stopwatch.StartNew();
            var result = Merge2.Join(shuffled, Tolerance.Global);
            watch.Stop();

            Assert.Single(result);
            Assert.Equal(count + 1, result[0].VertexCount);
            Assert.True(
                watch.Elapsed < TimeSpan.FromSeconds(5),
                "joining " + count + " segments took " + watch.ElapsedMilliseconds + " ms");
        }

        [Fact]
        public void Join_ManySegmentsMeetingAtOnePoint_StaysCheap()
        {
            // Every segment lands in the same grid cell, which is the case a grid is worst at. Work
            // here grows faster than the segment count, so the guard is against a search that walks
            // more of the grid than it needs to rather than against the shared cell itself.
            const int spokes = 4000;
            var segments = new List<GeoLine2>(spokes);
            for (int i = 0; i < spokes; i++)
            {
                double angle = 2.0 * Math.PI * i / spokes;
                segments.Add(new GeoLine2(new GeoPoint2(0, 0), new GeoPoint2(Math.Cos(angle) * 100.0, Math.Sin(angle) * 100.0)));
            }

            var watch = Stopwatch.StartNew();
            var result = Merge2.Join(segments, Tolerance.Global);
            watch.Stop();

            // Spokes pair up through the hub, so half as many runs come back.
            Assert.Equal(spokes / 2, result.Length);
            Assert.True(
                watch.Elapsed < TimeSpan.FromSeconds(5),
                "joining " + spokes + " spokes took " + watch.ElapsedMilliseconds + " ms");
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Builds separated chains of alternating horizontal and vertical steps, so that every vertex
        /// is a right angle and none of them is dropped as collinear.
        /// </summary>
        private static List<List<GeoPoint2>> Staircases(int chains, int segmentsPerChain, out GeoLine2[] segments)
        {
            var random = new Random(12345);
            var expected = new List<List<GeoPoint2>>();
            var built = new List<GeoLine2>();

            for (int c = 0; c < chains; c++)
            {
                // Chains are set far enough apart that none of them can touch another.
                var at = new GeoPoint2(c * 1000.0, c * 1000.0);
                var vertices = new List<GeoPoint2> { at };

                for (int s = 0; s < segmentsPerChain; s++)
                {
                    double step = 1.0 + random.Next(1, 5);
                    at = (s % 2 == 0) ? new GeoPoint2(at.X + step, at.Y) : new GeoPoint2(at.X, at.Y + step);
                    vertices.Add(at);
                }

                for (int v = 1; v < vertices.Count; v++)
                {
                    // Half the segments are stored backwards, so joining has to turn them round.
                    built.Add(random.Next(2) == 0
                        ? new GeoLine2(vertices[v - 1], vertices[v])
                        : new GeoLine2(vertices[v], vertices[v - 1]));
                }

                expected.Add(vertices);
            }

            segments = built.ToArray();
            return expected;
        }

        private static GeoLine2[] ShuffledStaircases(int chains, int segmentsPerChain, int seed)
        {
            Staircases(chains, segmentsPerChain, out var segments);
            return Shuffle(segments, seed);
        }

        private static GeoLine2[] Shuffle(GeoLine2[] segments, int seed)
        {
            var copy = (GeoLine2[])segments.Clone();
            var random = new Random(seed);
            for (int i = copy.Length - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                var swap = copy[i];
                copy[i] = copy[j];
                copy[j] = swap;
            }

            return copy;
        }

        private static List<GeoPoint2> Vertices(GeoPolyline2 polyline)
        {
            var vertices = new List<GeoPoint2>(polyline.VertexCount);
            for (int i = 0; i < polyline.VertexCount; i++)
            {
                vertices.Add(polyline[i]);
            }

            return vertices;
        }

        private static IEnumerable<GeoPoint2> Ends(GeoPolyline2 polyline)
        {
            yield return polyline[0];
            yield return polyline[polyline.VertexCount - 1];
        }

        private static string Describe(IEnumerable<GeoPoint2> vertices)
        {
            var text = new System.Text.StringBuilder();
            foreach (var vertex in vertices)
            {
                text.Append(vertex.X.ToString("F6")).Append(',').Append(vertex.Y.ToString("F6")).Append(';');
            }

            return text.ToString();
        }

        private static string Describe(GeoPolyline2[] polylines)
        {
            var text = new System.Text.StringBuilder();
            foreach (var polyline in polylines)
            {
                text.Append(Describe(Vertices(polyline))).Append('|');
            }

            return text.ToString();
        }

        private static void AssertVertices(GeoPolyline2 polyline, params GeoPoint2[] expected)
        {
            Assert.Equal(expected.Length, polyline.VertexCount);
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.True(
                    expected[i].IsEqualTo(polyline[i], Tolerance.Global),
                    "vertex " + i + ": expected " + expected[i] + " but found " + polyline[i]);
            }
        }

        #endregion
    }
}
