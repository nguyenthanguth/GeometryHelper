using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;
using Xunit;
using GeometryHelper.Arranging;

namespace GeometryHelper.UnitTest.Arranging
{
    public class SimulatedAnnealingAlgorithmTests
    {
        [Fact]
        public void Arrange_Run_SimulatedAnnealing_FindsSolution()
        {
            var leaderLine = new GeoLine2(0.0, 0.0, 10.0, 0.0);
            var a1 = new ArrangeItem
            {
                Box = new GeoRectangle2(new GeoPoint2(5.0, 0.0), 20.0, 10.0),
                Leader = leaderLine,
                Offset = 5.0
            };
            var a2 = new ArrangeItem
            {
                Box = new GeoRectangle2(new GeoPoint2(5.0, 0.0), 20.0, 10.0),
                Leader = leaderLine,
                Offset = 5.0
            };

            var options = new ArrangeOptions
            {
                Algorithm = ArrangeAlgorithmType.SimulatedAnnealing,
                RowGap = 5.0,
                PerpendicularLevels = 2
            };

            ArrangeResult[] results = Arranger.Run(new List<ArrangeItem> { a1, a2 }, options);

            var moved1 = new GeoRectangle2(a1.Box.Center + results[0].Translation, a1.Box.Width, a1.Box.Height);
            var moved2 = new GeoRectangle2(a2.Box.Center + results[1].Translation, a2.Box.Width, a2.Box.Height);

            Assert.False(moved1.CollidesWith(moved2));
        }

        [Fact]
        public void Arrange_Run_SimulatedAnnealing_UsesFixedSeedSoResultsAreReproducible()
        {
            // Simulated annealing is inherently random, but the library fixes the seed so that the layout remains unchanged
            // after each rerun. This is an important contract with CAD users.
            ArrangeResult[] Solve()
            {
                var leader = new GeoLine2(0.0, 0.0, 40.0, 0.0);
                var labels = new List<ArrangeItem>();
                for (int i = 0; i < 5; i++)
                {
                    labels.Add(new ArrangeItem
                    {
                        Box = new GeoRectangle2(leader.MidPoint, 20.0, 10.0),
                        Leader = leader,
                        Offset = 5.0
                    });
                }

                return Arranger.Run(labels, new ArrangeOptions
                {
                    Algorithm = ArrangeAlgorithmType.SimulatedAnnealing,
                    RowGap = 5.0,
                    PerpendicularLevels = 3
                });
            }

            Assert.Equal(Solve(), Solve());
        }

        [Fact]
        public void Arrange_Run_SimulatedAnnealing_PenalisesDistanceFromLeader()
        {
            // The energy function adds a penalty based on translation magnitude, so a solitary label
            // must stop at the nearest perpendicular level instead of wandering far away.
            var leader = new GeoLine2(0.0, 0.0, 40.0, 0.0);
            var label = new ArrangeItem
            {
                Box = new GeoRectangle2(leader.MidPoint, 20.0, 10.0),
                Leader = leader,
                Offset = 5.0
            };

            ArrangeResult result = Arranger.Run(new List<ArrangeItem> { label }, new ArrangeOptions
            {
                Algorithm = ArrangeAlgorithmType.SimulatedAnnealing,
                RowGap = 5.0,
                PerpendicularLevels = 3
            })[0];

            var moved = new GeoRectangle2(label.Box.Center + result.Translation, 20.0, 10.0);

            Assert.Equal(10.0, Math.Abs(moved.Center.Y), 6);
            Assert.True(result.Placed);
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

            var a = new ArrangeItem
            {
                Box = new GeoRectangle2(leader.MidPoint, 20.0, 10.0),
                Leader = leader,
                Offset = 5.0,
                BlockPolygons = new List<GeoPolygon2> { blockPoly }
            };
            var b = new ArrangeItem
            {
                Box = new GeoRectangle2(leader.MidPoint, 20.0, 10.0),
                Leader = leader,
                Offset = 5.0
            };

            ArrangeResult[] results = Arranger.Run(new List<ArrangeItem> { a, b }, new ArrangeOptions
            {
                Algorithm = ArrangeAlgorithmType.SimulatedAnnealing,
                RowGap = 5.0,
                PerpendicularLevels = 3
            });

            var movedA = new GeoRectangle2(a.Box.Center + results[0].Translation, 20.0, 10.0);
            var movedB = new GeoRectangle2(b.Box.Center + results[1].Translation, 20.0, 10.0);

            // The blocked region is collected globally for the list, so both labels must avoid it.
            Assert.False(movedA.CollidesWith(blockPoly));
            Assert.False(movedB.CollidesWith(blockPoly));
            Assert.False(movedA.CollidesWith(movedB));
        }

        [Fact]
        public void Arrange_Run_SimulatedAnnealing_ZeroTemperature_DoesNotThrow()
        {
            var leader = new GeoLine2(0.0, 0.0, 40.0, 0.0);
            var label = new ArrangeItem
            {
                Box = new GeoRectangle2(leader.MidPoint, 20.0, 10.0),
                Leader = leader,
                Offset = 5.0
            };

            var options = new ArrangeOptions
            {
                Algorithm = ArrangeAlgorithmType.SimulatedAnnealing,
                AnnealingInitialTemperature = 0.0, // Force T = 0
                RowGap = 5.0,
                PerpendicularLevels = 3
            };

            // This should not throw DivideByZeroException when calculating Boltzmann probability (e.g. deltaEnergy / Temp)
            ArrangeResult result = Arranger.Run(new List<ArrangeItem> { label }, options)[0];

            Assert.True(result.Placed);
        }

        [Fact]
        public void Arrange_Run_SimulatedAnnealing_RespectsLineObstacles()
        {
            var leader = new GeoLine2(0.0, 0.0, 40.0, 0.0);
            var blockLine = new GeoLine2(-50.0, 10.0, 150.0, 10.0); // Blocks the first upper level

            var label = new ArrangeItem
            {
                Box = new GeoRectangle2(leader.MidPoint, 20.0, 10.0),
                Leader = leader,
                Offset = 5.0,
                BlockLines = new List<GeoLine2> { blockLine }
            };

            ArrangeResult result = Arranger.Run(new List<ArrangeItem> { label }, new ArrangeOptions
            {
                Algorithm = ArrangeAlgorithmType.SimulatedAnnealing,
                RowGap = 5.0,
                PerpendicularLevels = 3
            })[0];

            var moved = new GeoRectangle2(label.Box.Center + result.Translation, 20.0, 10.0);

            // It should either go to the bottom row (Y=-10) or upper row 2 (Y=25) to avoid the line obstacle
            Assert.False(moved.CollidesWith(blockLine));
            Assert.True(result.Placed);
        }
    }
}
