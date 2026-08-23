using System;
using System.Collections.Generic;
using GeometryHelper.PlaneGeometry.Geometry;
using Xunit;

namespace GeometryHelper.ArrangeAlgorithms.UnitTest
{
    public class SimulatedAnnealingAlgorithmTests
    {
        [Fact]
        public void Arrange_Run_SimulatedAnnealing_FindsSolution()
        {
            var leaderLine = new GeoLine2(0.0, 0.0, 10.0, 0.0);
            var a1 = new Arrange
            {
                GeoRectangle2 = new GeoRectangle2(new GeoPoint2(5.0, 0.0), 20.0, 10.0),
                GeoLine2 = leaderLine,
                BaseOffsetFromLine = 5.0
            };
            var a2 = new Arrange
            {
                GeoRectangle2 = new GeoRectangle2(new GeoPoint2(5.0, 0.0), 20.0, 10.0),
                GeoLine2 = leaderLine,
                BaseOffsetFromLine = 5.0
            };

            var options = new ArrangeOptions
            {
                Algorithm = ArrangeAlgorithmType.SimulatedAnnealing,
                RowGap = 5.0,
                PerpendicularLevels = 2
            };

            Arrange.Run(new List<Arrange> { a1, a2 }, options);

            var moved1 = new GeoRectangle2(a1.GeoRectangle2.Center + a1.TranslationVector, a1.GeoRectangle2.Width, a1.GeoRectangle2.Height);
            var moved2 = new GeoRectangle2(a2.GeoRectangle2.Center + a2.TranslationVector, a2.GeoRectangle2.Width, a2.GeoRectangle2.Height);

            Assert.False(moved1.CollidesWith(moved2));
        }

        [Fact]
        public void Arrange_Run_SimulatedAnnealing_UsesFixedSeedSoResultsAreReproducible()
        {
            // Simulated annealing is inherently random, but the library fixes the seed so that the layout remains unchanged
            // after each rerun. This is an important contract with CAD users.
            List<Arrange> Solve()
            {
                var leader = new GeoLine2(0.0, 0.0, 40.0, 0.0);
                var labels = new List<Arrange>();
                for (int i = 0; i < 5; i++)
                {
                    labels.Add(new Arrange
                    {
                        GeoRectangle2 = new GeoRectangle2(leader.MidPoint, 20.0, 10.0),
                        GeoLine2 = leader,
                        BaseOffsetFromLine = 5.0
                    });
                }

                Arrange.Run(labels, new ArrangeOptions
                {
                    Algorithm = ArrangeAlgorithmType.SimulatedAnnealing,
                    RowGap = 5.0,
                    PerpendicularLevels = 3
                });

                return labels;
            }

            var first = Solve();
            var second = Solve();

            for (int i = 0; i < first.Count; i++)
            {
                Assert.Equal(first[i].TranslationVector, second[i].TranslationVector);
            }
        }

        [Fact]
        public void Arrange_Run_SimulatedAnnealing_PenalisesDistanceFromLeader()
        {
            // The energy function adds a penalty based on translation magnitude, so a solitary label
            // must stop at the nearest perpendicular level instead of wandering far away.
            var leader = new GeoLine2(0.0, 0.0, 40.0, 0.0);
            var label = new Arrange
            {
                GeoRectangle2 = new GeoRectangle2(leader.MidPoint, 20.0, 10.0),
                GeoLine2 = leader,
                BaseOffsetFromLine = 5.0
            };

            Arrange.Run(new List<Arrange> { label }, new ArrangeOptions
            {
                Algorithm = ArrangeAlgorithmType.SimulatedAnnealing,
                RowGap = 5.0,
                PerpendicularLevels = 3
            });

            var moved = new GeoRectangle2(label.GeoRectangle2.Center + label.TranslationVector, 20.0, 10.0);

            Assert.Equal(10.0, Math.Abs(moved.Center.Y), 6);
            Assert.True(label.Placed);
        }

        [Fact]
        public void Arrange_Run_SimulatedAnnealing_KeepsLabelsOutOfBlockedRegion()
        {
            var leader = new GeoLine2(0.0, 0.0, 40.0, 0.0);
            var blockPoly = new GeoPolygon2(
                new GeoPoint2(-60.0, 0.0),
                new GeoPoint2(60.0, 0.0),
                new GeoPoint2(60.0, 60.0),
                new GeoPoint2(-60.0, 60.0));

            var a = new Arrange
            {
                GeoRectangle2 = new GeoRectangle2(leader.MidPoint, 20.0, 10.0),
                GeoLine2 = leader,
                BaseOffsetFromLine = 5.0,
                BlockPolygons = new List<GeoPolygon2> { blockPoly }
            };
            var b = new Arrange
            {
                GeoRectangle2 = new GeoRectangle2(leader.MidPoint, 20.0, 10.0),
                GeoLine2 = leader,
                BaseOffsetFromLine = 5.0
            };

            Arrange.Run(new List<Arrange> { a, b }, new ArrangeOptions
            {
                Algorithm = ArrangeAlgorithmType.SimulatedAnnealing,
                RowGap = 5.0,
                PerpendicularLevels = 3
            });

            var movedA = new GeoRectangle2(a.GeoRectangle2.Center + a.TranslationVector, 20.0, 10.0);
            var movedB = new GeoRectangle2(b.GeoRectangle2.Center + b.TranslationVector, 20.0, 10.0);

            // The blocked region is collected globally for the list, so both labels must avoid it.
            Assert.False(movedA.CollidesWith(blockPoly));
            Assert.False(movedB.CollidesWith(blockPoly));
            Assert.False(movedA.CollidesWith(movedB));
        }

        [Fact]
        public void Arrange_Run_SimulatedAnnealing_ZeroTemperature_DoesNotThrow()
        {
            var leader = new GeoLine2(0.0, 0.0, 40.0, 0.0);
            var label = new Arrange
            {
                GeoRectangle2 = new GeoRectangle2(leader.MidPoint, 20.0, 10.0),
                GeoLine2 = leader,
                BaseOffsetFromLine = 5.0
            };

            var options = new ArrangeOptions
            {
                Algorithm = ArrangeAlgorithmType.SimulatedAnnealing,
                AnnealingInitialTemperature = 0.0, // Force T = 0
                RowGap = 5.0,
                PerpendicularLevels = 3
            };

            // This should not throw DivideByZeroException when calculating Boltzmann probability (e.g. deltaEnergy / Temp)
            Arrange.Run(new List<Arrange> { label }, options);

            Assert.True(label.Placed);
        }

        [Fact]
        public void Arrange_Run_SimulatedAnnealing_RespectsLineObstacles()
        {
            var leader = new GeoLine2(0.0, 0.0, 40.0, 0.0);
            var blockLine = new GeoLine2(-50.0, 10.0, 150.0, 10.0); // Blocks the first upper level

            var label = new Arrange
            {
                GeoRectangle2 = new GeoRectangle2(leader.MidPoint, 20.0, 10.0),
                GeoLine2 = leader,
                BaseOffsetFromLine = 5.0,
                BlockLines = new List<GeoLine2> { blockLine }
            };

            Arrange.Run(new List<Arrange> { label }, new ArrangeOptions
            {
                Algorithm = ArrangeAlgorithmType.SimulatedAnnealing,
                RowGap = 5.0,
                PerpendicularLevels = 3
            });

            var moved = new GeoRectangle2(label.GeoRectangle2.Center + label.TranslationVector, 20.0, 10.0);

            // It should either go to the bottom row (Y=-10) or upper row 2 (Y=25) to avoid the line obstacle
            Assert.False(moved.CollidesWith(blockLine));
            Assert.True(label.Placed);
        }
    }
}
