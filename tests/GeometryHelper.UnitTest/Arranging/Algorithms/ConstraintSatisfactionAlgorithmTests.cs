using System.Collections.Generic;
using GeometryHelper.Geometry;
using Xunit;
using GeometryHelper.Arranging;

namespace GeometryHelper.UnitTest.Arranging
{
    public class ConstraintSatisfactionAlgorithmTests
    {
        [Fact]
        public void Arrange_Run_ConstraintSatisfaction_FindsSolution()
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
                Algorithm = ArrangeAlgorithmType.ConstraintSatisfaction,
                RowGap = 5.0,
                PerpendicularLevels = 2
            };

            ArrangeResult[] results = Arranger.Run(new List<ArrangeItem> { a1, a2 }, options);

            var moved1 = new GeoRectangle2(a1.Box.Center + results[0].Translation, a1.Box.Width, a1.Box.Height);
            var moved2 = new GeoRectangle2(a2.Box.Center + results[1].Translation, a2.Box.Width, a2.Box.Height);

            Assert.False(moved1.CollidesWith(moved2));
            Assert.True(results[0].Placed);
            Assert.True(results[1].Placed);
        }

        /// <summary>
        /// Labels far apart, each going to its candidate nearest to where it stands, which needs no going back. Every
        /// label counted as a step, and with more labels than steps the search gave up and handed the labels to the
        /// greedy algorithm, which takes the first place free.
        /// </summary>
        [Theory]
        [InlineData(20, 10)]
        [InlineData(20, 0)]
        public void Arrange_ConstraintSatisfaction_PlacesMoreLabelsThanItHasStepsBack(int count, int steps)
        {
            var options = new ArrangeOptions
            {
                Algorithm = ArrangeAlgorithmType.ConstraintSatisfaction,
                RowGap = 5.0,
                PerpendicularLevels = 3,
                MaxBacktrackSteps = steps
            };
            ArrangeTestKit.AssertEachGoesToItsNearestCandidate(ArrangeTestKit.LabelsApart(count), options);
        }

        [Fact]
        public void Arrange_UnsolvableGraph_ReturnsBestEffort()
        {
            var leaderLine = new GeoLine2(0.0, 0.0, 10.0, 0.0);

            // 3 labels competing for a small area with very few candidates -> definitely triggers failure
            var a1 = new ArrangeItem { Box = new GeoRectangle2(new GeoPoint2(5.0, 0.0), 20.0, 10.0), Leader = leaderLine };
            var a2 = new ArrangeItem { Box = new GeoRectangle2(new GeoPoint2(5.0, 0.0), 20.0, 10.0), Leader = leaderLine };
            var a3 = new ArrangeItem { Box = new GeoRectangle2(new GeoPoint2(5.0, 0.0), 20.0, 10.0), Leader = leaderLine };

            var options = new ArrangeOptions
            {
                Algorithm = ArrangeAlgorithmType.ConstraintSatisfaction,
                PerpendicularLevels = 1,
                MaximumCandidates = 1 // Enforce strict constraints
            };

            ArrangeResult[] results = Arranger.Run(new List<ArrangeItem> { a1, a2, a3 }, options);

            // Even though the constraint graph is unsolvable, the CSA algorithm must not throw
            // and should return a best-effort result where some labels are still marked as failed.
            Assert.False(results[0].Placed && results[1].Placed && results[2].Placed);
        }

        [Fact]
        public void Arrange_MultipleLabels_ComplexConstraintNetwork()
        {
            var options = new ArrangeOptions
            {
                Algorithm = ArrangeAlgorithmType.ConstraintSatisfaction,
                PerpendicularLevels = 3
            };

            var labels = new List<ArrangeItem>();
            for (int i = 0; i < 4; i++)
            {
                labels.Add(new ArrangeItem
                {
                    Leader = new GeoLine2(i * 10, 0, i * 10 + 20, 0),
                    Box = new GeoRectangle2(new GeoPoint2(i * 10 + 10, 0), 20, 10),
                });
            }

            ArrangeResult[] results = Arranger.Run(labels, options);

            // A chain of 4 overlapping label constraint dependencies must be solved completely when space is sufficient.
            Assert.True(results[0].Placed);
            Assert.True(results[1].Placed);
            Assert.True(results[2].Placed);
            Assert.True(results[3].Placed);
        }

        [Fact]
        public void Arrange_ConstraintSatisfaction_RespectsStaticObstacles()
        {
            var leaderLine = new GeoLine2(0.0, 0.0, 40.0, 0.0);
            var blockPoly = new GeoPolygon2(
                new GeoPoint2(-60.0, 0.0),
                new GeoPoint2(60.0, 0.0),
                new GeoPoint2(60.0, 40.0),
                new GeoPoint2(-60.0, 40.0));

            var label = new ArrangeItem
            {
                Box = new GeoRectangle2(new GeoPoint2(20.0, 0.0), 20.0, 10.0),
                Leader = leaderLine,
                Offset = 5.0,
                BlockPolygons = new List<GeoPolygon2> { blockPoly }
            };

            var options = new ArrangeOptions
            {
                Algorithm = ArrangeAlgorithmType.ConstraintSatisfaction,
                PerpendicularLevels = 3
            };

            ArrangeResult result = Arranger.Run(new List<ArrangeItem> { label }, options)[0];

            var moved = new GeoRectangle2(label.Box.Center + result.Translation, label.Box.Width, label.Box.Height);

            // The resulting position must not intersect with the static obstacle.
            Assert.False(moved.CollidesWith(blockPoly));
            Assert.True(result.Placed);
        }

        [Fact]
        public void Arrange_ConstraintSatisfaction_PartialSolutionWhenPartiallyUnsolvable()
        {
            var leaderLine1 = new GeoLine2(0.0, 0.0, 10.0, 0.0);
            var leaderLine2 = new GeoLine2(100.0, 0.0, 110.0, 0.0); // Located far away

            // 3 labels on leaderLine1 are stuck due to lack of space (only 1 level)
            var a1 = new ArrangeItem { Box = new GeoRectangle2(new GeoPoint2(5.0, 0.0), 20.0, 10.0), Leader = leaderLine1 };
            var a2 = new ArrangeItem { Box = new GeoRectangle2(new GeoPoint2(5.0, 0.0), 20.0, 10.0), Leader = leaderLine1 };
            var a3 = new ArrangeItem { Box = new GeoRectangle2(new GeoPoint2(5.0, 0.0), 20.0, 10.0), Leader = leaderLine1 };

            // 1 label on leaderLine2 is independent and can be placed successfully
            var a4 = new ArrangeItem { Box = new GeoRectangle2(new GeoPoint2(105.0, 0.0), 20.0, 10.0), Leader = leaderLine2 };

            var options = new ArrangeOptions
            {
                Algorithm = ArrangeAlgorithmType.ConstraintSatisfaction,
                PerpendicularLevels = 1,
                MaximumCandidates = 1
            };

            ArrangeResult[] results = Arranger.Run(new List<ArrangeItem> { a1, a2, a3, a4 }, options);

            // a4 must be placed successfully as it is independent from the congested cluster
            Assert.True(results[3].Placed);
            // The congested cluster must contain failed labels
            Assert.False(results[0].Placed && results[1].Placed && results[2].Placed);
        }
    }
}
