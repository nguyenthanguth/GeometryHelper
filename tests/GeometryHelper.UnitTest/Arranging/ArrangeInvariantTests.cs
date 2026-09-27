using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Arranging;
using GeometryHelper;
using GeometryHelper.Core;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Arranging
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
        private static List<ArrangeItem> Scene(Random rng, int count)
        {
            var scene = new List<ArrangeItem>();

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

                scene.Add(new ArrangeItem
                {
                    Box = new GeoRectangle2(new GeoPoint2(x, y + 60), 90, 30),
                    Leader = new GeoLine2(new GeoPoint2(x - 40, y), new GeoPoint2(x + 40, y)),
                    Offset = 25,
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
                    List<ArrangeItem> scene = Scene(rng, count);

                    var options = new ArrangeOptions { Algorithm = which };
                    ArrangeResult[] results = Arranger.Run(scene, options);

                    Assert.Equal(count, results.Length);

                    // Each result answers for the label in its place: it sends that label to one of that label's
                    // own candidate positions, or leaves it where it is.
                    for (int i = 0; i < count; i++)
                    {
                        GeoPoint2 centre = scene[i].Box.Center + results[i].Translation;
                        bool stays = results[i].Translation.IsEqualTo(GeoVector2.Zero, Tol);
                        bool own = scene[i].GetPlacePoints(options).Any(p => p.IsEqualTo(centre, Tol));

                        Assert.True(stays || own, $"{which}: label {i} was sent to {centre}, none of its own candidates");
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
                    List<ArrangeItem> scene = Scene(rng, 4 + rng.Next(6));
                    ArrangeResult[] results = Arranger.Run(scene, new ArrangeOptions { Algorithm = which });

                    var settled = new List<GeoRectangle2>();

                    for (int i = 0; i < scene.Count; i++)
                    {
                        GeoRectangle2 box = scene[i].Box.Translate(results[i].Translation);

                        if (!results[i].Placed) { unplaced++; continue; }

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
                var trapped = new List<ArrangeItem>
                {
                    new ArrangeItem
                    {
                        Box = new GeoRectangle2(new GeoPoint2(0, 100), 90, 30),
                        Leader = new GeoLine2(new GeoPoint2(-40, 0), new GeoPoint2(40, 0)),
                        Offset = 25,
                        BlockPolygons = new List<GeoPolygon2> { wall },
                        BlockLines = new List<GeoLine2>()
                    }
                };

                ArrangeResult[] results = Arranger.Run(trapped, new ArrangeOptions { Algorithm = which });

                Assert.Single(results);
                Assert.False(results[0].Placed,
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

                List<ArrangeItem> first = Scene(new Random(99), 4);
                List<ArrangeItem> second = Scene(new Random(99), 4);

                ArrangeResult[] a = Arranger.Run(first, options);
                ArrangeResult[] b = Arranger.Run(second, options);

                Assert.Equal(a.Length, b.Length);

                for (int i = 0; i < a.Length; i++)
                {
                    Assert.True(a[i].Translation.IsEqualTo(b[i].Translation, Tol) && a[i].Placed == b[i].Placed,
                                $"{which}: the same scene arranged twice gave {a[i]} then {b[i]}");
                }
            }
        }

        [Fact]
        public void ALabelWithNothingInTheWayIsPlaced()
        {
            foreach (ArrangeAlgorithmType which in Algorithms)
            {
                var alone = new List<ArrangeItem>
                {
                    new ArrangeItem
                    {
                        Box = new GeoRectangle2(new GeoPoint2(0, 100), 90, 30),
                        Leader = new GeoLine2(new GeoPoint2(-40, 0), new GeoPoint2(40, 0)),
                        Offset = 25,
                        BlockPolygons = new List<GeoPolygon2>(),
                        BlockLines = new List<GeoLine2>()
                    }
                };

                ArrangeResult[] results = Arranger.Run(alone, new ArrangeOptions { Algorithm = which });

                Assert.Single(results);
                Assert.True(results[0].Placed, $"{which}: a lone label with an empty sheet was not placed");
            }
        }

        [Fact]
        public void AnEmptyListIsAnsweredWithAnEmptyList()
        {
            foreach (ArrangeAlgorithmType which in Algorithms)
            {
                Assert.Empty(Arranger.Run(new List<ArrangeItem>(), new ArrangeOptions { Algorithm = which }));
            }
        }

        [Fact]
        public void NullArgumentsAreRefused()
        {
            Assert.Throws<ArgumentNullException>(() => Arranger.Run(null));
            Assert.Throws<ArgumentNullException>(() => Arranger.Run(new List<ArrangeItem>(), null));
        }
    }
}
