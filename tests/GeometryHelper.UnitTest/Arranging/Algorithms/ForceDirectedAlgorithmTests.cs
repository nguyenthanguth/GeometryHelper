using System.Collections.Generic;
using GeometryHelper.Geometry;
using Xunit;
using GeometryHelper.Arranging;

namespace GeometryHelper.UnitTest.Arranging
{
    public class ForceDirectedAlgorithmTests
    {
        [Fact]
        public void Arrange_Run_ForceDirected_FindsSolution()
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
                Algorithm = ArrangeAlgorithmType.ForceDirected,
                RowGap = 5.0,
                PerpendicularLevels = 2,
                ForceIterations = 10
            };

            ArrangeResult[] results = Arranger.Run(new List<ArrangeItem> { a1, a2 }, options);

            var moved1 = new GeoRectangle2(a1.Box.Center + results[0].Translation, a1.Box.Width, a1.Box.Height);
            var moved2 = new GeoRectangle2(a2.Box.Center + results[1].Translation, a2.Box.Width, a2.Box.Height);

            Assert.False(moved1.CollidesWith(moved2));
        }

        [Fact]
        public void Arrange_Run_ForceDirected_PushesLabelsAwayFromObstacleNotToward()
        {
            // Repulsive force from polygon obstacles was once inverted — it ATTRACTED labels to obstacles instead of repelling them.
            // The blocked region is completely above the guide segment, so the label must end up below it.
            var leader = new GeoLine2(0.0, 0.0, 40.0, 0.0);
            var blockPoly = new GeoPolygon2(
                new GeoPoint2(-60.0, 2.0),
                new GeoPoint2(60.0, 2.0),
                new GeoPoint2(60.0, 60.0),
                new GeoPoint2(-60.0, 60.0));

            var label = new ArrangeItem
            {
                Box = new GeoRectangle2(leader.MidPoint, 20.0, 10.0),
                Leader = leader,
                Offset = 5.0,
                BlockPolygons = new List<GeoPolygon2> { blockPoly }
            };

            ArrangeResult result = Arranger.Run(new List<ArrangeItem> { label }, new ArrangeOptions
            {
                Algorithm = ArrangeAlgorithmType.ForceDirected,
                RowGap = 5.0,
                PerpendicularLevels = 3,
                ForceIterations = 50
            })[0];

            var moved = new GeoRectangle2(label.Box.Center + result.Translation, 20.0, 10.0);

            Assert.True(moved.Center.Y < 0.0);
            Assert.False(moved.CollidesWith(blockPoly));
            Assert.True(result.Placed);
        }

        [Fact]
        public void Arrange_Run_ForceDirected_ZeroIterations_StillProducesValidLayout()
        {
            // Even with zero simulation iterations, the discrete mapping step must still yield a valid layout.
            var leader = new GeoLine2(0.0, 0.0, 40.0, 0.0);
            var a = new ArrangeItem { Box = new GeoRectangle2(leader.MidPoint, 20.0, 10.0), Leader = leader, Offset = 5.0 };
            var b = new ArrangeItem { Box = new GeoRectangle2(leader.MidPoint, 20.0, 10.0), Leader = leader, Offset = 5.0 };

            ArrangeResult[] results = Arranger.Run(new List<ArrangeItem> { a, b }, new ArrangeOptions
            {
                Algorithm = ArrangeAlgorithmType.ForceDirected,
                RowGap = 5.0,
                PerpendicularLevels = 3,
                ForceIterations = 0
            });

            var movedA = new GeoRectangle2(a.Box.Center + results[0].Translation, 20.0, 10.0);
            var movedB = new GeoRectangle2(b.Box.Center + results[1].Translation, 20.0, 10.0);

            Assert.False(movedA.CollidesWith(movedB));
            Assert.True(results[0].Placed);
            Assert.True(results[1].Placed);
        }

        [Fact]
        public void Arrange_Run_ForceDirected_SpreadsManyLabelsSharingOneLeader()
        {
            var leader = new GeoLine2(0.0, 0.0, 200.0, 0.0);
            var labels = new List<ArrangeItem>();
            for (int i = 0; i < 4; i++)
            {
                labels.Add(new ArrangeItem
                {
                    Box = new GeoRectangle2(leader.MidPoint, 20.0, 10.0),
                    Leader = leader,
                    Offset = 5.0
                });
            }

            ArrangeResult[] results = Arranger.Run(labels, new ArrangeOptions
            {
                Algorithm = ArrangeAlgorithmType.ForceDirected,
                RowGap = 5.0,
                PerpendicularLevels = 3,
                ForceIterations = 60
            });

            var boxes = new List<GeoRectangle2>();
            for (int i = 0; i < labels.Count; i++)
            {
                boxes.Add(new GeoRectangle2(labels[i].Box.Center + results[i].Translation, 20.0, 10.0));
            }

            for (int i = 0; i < boxes.Count; i++)
            {
                for (int j = i + 1; j < boxes.Count; j++)
                {
                    Assert.False(boxes[i].CollidesWith(boxes[j]));
                }
            }
        }

        [Fact]
        public void Arrange_Run_ForceDirected_RespectsLineObstacles()
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
                Algorithm = ArrangeAlgorithmType.ForceDirected,
                RowGap = 5.0,
                PerpendicularLevels = 3,
                ForceIterations = 30
            })[0];

            var moved = new GeoRectangle2(label.Box.Center + result.Translation, 20.0, 10.0);

            // Force-directed algorithm must resolve placing layout without intersecting the line obstacle.
            Assert.False(moved.CollidesWith(blockLine));
            Assert.True(result.Placed);
        }

        [Fact]
        public void Arrange_Run_ForceDirected_HighIterations_DoesNotCrash()
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
                Algorithm = ArrangeAlgorithmType.ForceDirected,
                ForceIterations = 500, // Very high iteration simulation
                RowGap = 5.0,
                PerpendicularLevels = 3
            };

            // Checking CPU performance and calculation safety under high loop counts.
            ArrangeResult result = Arranger.Run(new List<ArrangeItem> { label }, options)[0];

            Assert.True(result.Placed);
        }
    }
}
