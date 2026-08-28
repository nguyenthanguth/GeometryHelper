using System;
using System.Collections.Generic;
using GeometryHelper.ArrangeAlgorithms;
using GeometryHelper.CommonGeometry;
using GeometryHelper.PlaneGeometry.Core;
using GeometryHelper.PlaneGeometry.Geometry;
using Xunit;

namespace GeometryHelper.ArrangeAlgorithms.UnitTest
{
    /// <summary>
    /// Invariants that hold whichever algorithm does the arranging.
    /// <para>
    /// The promise the library makes is narrow and checkable: a label reported as placed sits somewhere
    /// completely empty. Checking that directly is worth more than checking any particular position, since
    /// the five algorithms are free to disagree about where a label goes and must not disagree about
    /// whether the answer is legal.
    /// </para>
    /// </summary>
    public class ArrangeInvariantTests
    {
        private static readonly Tolerance Tol = Tolerance.Global;

        private static readonly ArrangeAlgorithmType[] Algorithms =
        {
            ArrangeAlgorithmType.Greedy,
            ArrangeAlgorithmType.BoundedBacktracking,
            ArrangeAlgorithmType.SimulatedAnnealing,
            ArrangeAlgorithmType.ForceDirected,
            ArrangeAlgorithmType.ConstraintSatisfaction
        };

        /// <summary>
        /// A row of labels hung off a row of paths, with a few obstacles in the way.
        /// </summary>
        private static List<Arrange> Scene(Random rng, int count)
        {
            var scene = new List<Arrange>();

            var blocks = new List<GeoPolygon2>();
            for (int i = 0; i < 2; i++)
            {
                double cx = rng.Next(-200, 201);
                double cy = rng.Next(-200, 201);
                blocks.Add(new GeoPolygon2(
                    new GeoPoint2(cx, cy),
                    new GeoPoint2(cx + 80, cy),
                    new GeoPoint2(cx + 80, cy + 60),
                    new GeoPoint2(cx, cy + 60)));
            }

            for (int i = 0; i < count; i++)
            {
                // Deliberately cramped: the paths are closer together than a label is wide, so the
                // arranger has to shuffle and will not always succeed.
                double x = i * 45 - 100;
                double y = rng.Next(-30, 31);

                scene.Add(new Arrange
                {
                    GeoRectangle2 = new GeoRectangle2(new GeoPoint2(x, y + 60), 90, 30),
                    GeoLine2 = new GeoLine2(new GeoPoint2(x - 40, y), new GeoPoint2(x + 40, y)),
                    BaseOffsetFromLine = 25,
                    BlockPolygons = blocks,
                    BlockLines = new List<GeoLine2>()
                });
            }

            return scene;
        }

        [Fact]
        public void EveryAlgorithmAnswersOncePerLabelInOrder()
        {
            Random rng = new Random(4711);

            foreach (ArrangeAlgorithmType which in Algorithms)
            {
                for (int t = 0; t < 12; t++)
                {
                    int count = 1 + rng.Next(6);
                    List<Arrange> scene = Scene(rng, count);

                    List<GeoVector2> moves = Arrange.Run(scene, new ArrangeOptions { Algorithm = which });

                    Assert.Equal(count, moves.Count);

                    // The vector handed back and the one left on the label must be the same answer.
                    for (int i = 0; i < count; i++)
                    {
                        Assert.True(moves[i].IsEqualTo(scene[i].TranslationVector, Tol),
                                    $"{which}: label {i} was told {moves[i]} but carries {scene[i].TranslationVector}");
                    }
                }
            }
        }

        [Fact]
        public void ALabelReportedAsPlacedSitsSomewhereEmpty()
        {
            Random rng = new Random(1123);
            int placed = 0, unplaced = 0;

            foreach (ArrangeAlgorithmType which in Algorithms)
            {
                for (int t = 0; t < 12; t++)
                {
                    List<Arrange> scene = Scene(rng, 4 + rng.Next(6));
                    Arrange.Run(scene, new ArrangeOptions { Algorithm = which });

                    var settled = new List<GeoRectangle2>();

                    for (int i = 0; i < scene.Count; i++)
                    {
                        GeoRectangle2 box = scene[i].GeoRectangle2.Translate(scene[i].TranslationVector);

                        if (!scene[i].Placed) { unplaced++; continue; }

                        placed++;

                        // Nothing the label was told to avoid may be where it ended up.
                        foreach (GeoPolygon2 block in scene[i].BlockPolygons)
                        {
                            Assert.False(Collision2.CollidesWith(box, block, Tol),
                                         $"{which}: label {i} reported placed but sits on a block");
                        }

                        foreach (GeoRectangle2 neighbour in settled)
                        {
                            Assert.False(Collision2.CollidesWith(box, neighbour, Tol),
                                         $"{which}: label {i} reported placed but overlaps another placed label");
                        }

                        settled.Add(box);
                    }
                }
            }

            // The invariant is only worth checking if labels really were placed; the case where one
            // cannot be placed at all is covered separately below.
            Assert.True(placed > 50, $"only {placed} labels were placed, too few to prove anything");
        }

        [Fact]
        public void ALabelWithNowhereToGoIsNotReportedAsPlaced()
        {
            // A block large enough to cover everywhere the arranger could reach, so there is no empty
            // position to find. What matters is that it says so rather than claiming a spot on the block.
            var wall = new GeoPolygon2(
                new GeoPoint2(-5000, -5000),
                new GeoPoint2(5000, -5000),
                new GeoPoint2(5000, 5000),
                new GeoPoint2(-5000, 5000));

            foreach (ArrangeAlgorithmType which in Algorithms)
            {
                var trapped = new List<Arrange>
                {
                    new Arrange
                    {
                        GeoRectangle2 = new GeoRectangle2(new GeoPoint2(0, 100), 90, 30),
                        GeoLine2 = new GeoLine2(new GeoPoint2(-40, 0), new GeoPoint2(40, 0)),
                        BaseOffsetFromLine = 25,
                        BlockPolygons = new List<GeoPolygon2> { wall },
                        BlockLines = new List<GeoLine2>()
                    }
                };

                List<GeoVector2> moves = Arrange.Run(trapped, new ArrangeOptions { Algorithm = which });

                Assert.Single(moves);
                Assert.False(trapped[0].Placed,
                             $"{which}: claimed to place a label on a sheet that is entirely blocked");
            }
        }

        [Fact]
        public void ArrangingIsRepeatable()
        {
            Random rng = new Random(2244);

            foreach (ArrangeAlgorithmType which in Algorithms)
            {
                // The seed is fixed per algorithm so both runs see the same scene.
                var options = new ArrangeOptions { Algorithm = which };

                List<Arrange> first = Scene(new Random(99), 4);
                List<Arrange> second = Scene(new Random(99), 4);

                List<GeoVector2> a = Arrange.Run(first, options);
                List<GeoVector2> b = Arrange.Run(second, options);

                Assert.Equal(a.Count, b.Count);

                for (int i = 0; i < a.Count; i++)
                {
                    Assert.True(a[i].IsEqualTo(b[i], Tol),
                                $"{which}: the same scene arranged twice gave {a[i]} then {b[i]}");
                }
            }
        }

        [Fact]
        public void ALabelWithNothingInTheWayIsPlaced()
        {
            foreach (ArrangeAlgorithmType which in Algorithms)
            {
                var alone = new List<Arrange>
                {
                    new Arrange
                    {
                        GeoRectangle2 = new GeoRectangle2(new GeoPoint2(0, 100), 90, 30),
                        GeoLine2 = new GeoLine2(new GeoPoint2(-40, 0), new GeoPoint2(40, 0)),
                        BaseOffsetFromLine = 25,
                        BlockPolygons = new List<GeoPolygon2>(),
                        BlockLines = new List<GeoLine2>()
                    }
                };

                List<GeoVector2> moves = Arrange.Run(alone, new ArrangeOptions { Algorithm = which });

                Assert.Single(moves);
                Assert.True(alone[0].Placed, $"{which}: a lone label with an empty sheet was not placed");
            }
        }

        [Fact]
        public void AnEmptyListIsAnsweredWithAnEmptyList()
        {
            foreach (ArrangeAlgorithmType which in Algorithms)
            {
                Assert.Empty(Arrange.Run(new List<Arrange>(), new ArrangeOptions { Algorithm = which }));
            }
        }

        [Fact]
        public void NullArgumentsAreRefused()
        {
            Assert.Throws<ArgumentNullException>(() => Arrange.Run(null));
            Assert.Throws<ArgumentNullException>(() => Arrange.Run(new List<Arrange>(), null));
        }
    }
}
