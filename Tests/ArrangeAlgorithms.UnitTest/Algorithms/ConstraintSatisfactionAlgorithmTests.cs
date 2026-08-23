using System.Collections.Generic;
using PlaneGeometry.Geometry;
using Xunit;

namespace ArrangeAlgorithms.UnitTest
{
    public class ConstraintSatisfactionAlgorithmTests
    {
        [Fact]
        public void Arrange_Run_ConstraintSatisfaction_FindsSolution()
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
                Algorithm = ArrangeAlgorithmType.ConstraintSatisfaction,
                RowGap = 5.0,
                PerpendicularLevels = 2
            };

            Arrange.Run(new List<Arrange> { a1, a2 }, options);

            var moved1 = new GeoRectangle2(a1.GeoRectangle2.Center + a1.TranslationVector, a1.GeoRectangle2.Width, a1.GeoRectangle2.Height);
            var moved2 = new GeoRectangle2(a2.GeoRectangle2.Center + a2.TranslationVector, a2.GeoRectangle2.Width, a2.GeoRectangle2.Height);

            Assert.False(moved1.CollidesWith(moved2));
            Assert.True(a1.Placed);
            Assert.True(a2.Placed);
        }

        [Fact]
        public void Arrange_UnsolvableGraph_ReturnsBestEffort()
        {
            var leaderLine = new GeoLine2(0.0, 0.0, 10.0, 0.0);
            
            // 3 labels competing for a small area with very few candidates -> definitely triggers failure
            var a1 = new Arrange { GeoRectangle2 = new GeoRectangle2(new GeoPoint2(5.0, 0.0), 20.0, 10.0), GeoLine2 = leaderLine };
            var a2 = new Arrange { GeoRectangle2 = new GeoRectangle2(new GeoPoint2(5.0, 0.0), 20.0, 10.0), GeoLine2 = leaderLine };
            var a3 = new Arrange { GeoRectangle2 = new GeoRectangle2(new GeoPoint2(5.0, 0.0), 20.0, 10.0), GeoLine2 = leaderLine };

            var options = new ArrangeOptions
            {
                Algorithm = ArrangeAlgorithmType.ConstraintSatisfaction,
                PerpendicularLevels = 1,
                MaximumCandidates = 1 // Enforce strict constraints
            };

            Arrange.Run(new List<Arrange> { a1, a2, a3 }, options);

            // Even though the constraint graph is unsolvable, the CSA algorithm must not throw
            // and should return a best-effort result where some labels are still marked as failed.
            Assert.False(a1.Placed && a2.Placed && a3.Placed);
        }

        [Fact]
        public void Arrange_MultipleLabels_ComplexConstraintNetwork()
        {
            var options = new ArrangeOptions
            {
                Algorithm = ArrangeAlgorithmType.ConstraintSatisfaction,
                PerpendicularLevels = 3
            };

            var labels = new List<Arrange>();
            for (int i = 0; i < 4; i++)
            {
                labels.Add(new Arrange
                {
                    GeoLine2 = new GeoLine2(i * 10, 0, i * 10 + 20, 0),
                    GeoRectangle2 = new GeoRectangle2(new GeoPoint2(i * 10 + 10, 0), 20, 10),
                });
            }

            Arrange.Run(labels, options);

            // A chain of 4 overlapping label constraint dependencies must be solved completely when space is sufficient.
            Assert.True(labels[0].Placed);
            Assert.True(labels[1].Placed);
            Assert.True(labels[2].Placed);
            Assert.True(labels[3].Placed);
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

            var label = new Arrange
            {
                GeoRectangle2 = new GeoRectangle2(new GeoPoint2(20.0, 0.0), 20.0, 10.0),
                GeoLine2 = leaderLine,
                BaseOffsetFromLine = 5.0,
                BlockPolygons = new List<GeoPolygon2> { blockPoly }
            };

            var options = new ArrangeOptions
            {
                Algorithm = ArrangeAlgorithmType.ConstraintSatisfaction,
                PerpendicularLevels = 3
            };

            Arrange.Run(new List<Arrange> { label }, options);

            var moved = new GeoRectangle2(label.GeoRectangle2.Center + label.TranslationVector, label.GeoRectangle2.Width, label.GeoRectangle2.Height);

            // The resulting position must not intersect with the static obstacle.
            Assert.False(moved.CollidesWith(blockPoly));
            Assert.True(label.Placed);
        }

        [Fact]
        public void Arrange_ConstraintSatisfaction_PartialSolutionWhenPartiallyUnsolvable()
        {
            var leaderLine1 = new GeoLine2(0.0, 0.0, 10.0, 0.0);
            var leaderLine2 = new GeoLine2(100.0, 0.0, 110.0, 0.0); // Located far away

            // 3 labels on leaderLine1 are stuck due to lack of space (only 1 level)
            var a1 = new Arrange { GeoRectangle2 = new GeoRectangle2(new GeoPoint2(5.0, 0.0), 20.0, 10.0), GeoLine2 = leaderLine1 };
            var a2 = new Arrange { GeoRectangle2 = new GeoRectangle2(new GeoPoint2(5.0, 0.0), 20.0, 10.0), GeoLine2 = leaderLine1 };
            var a3 = new Arrange { GeoRectangle2 = new GeoRectangle2(new GeoPoint2(5.0, 0.0), 20.0, 10.0), GeoLine2 = leaderLine1 };

            // 1 label on leaderLine2 is independent and can be placed successfully
            var a4 = new Arrange { GeoRectangle2 = new GeoRectangle2(new GeoPoint2(105.0, 0.0), 20.0, 10.0), GeoLine2 = leaderLine2 };

            var options = new ArrangeOptions
            {
                Algorithm = ArrangeAlgorithmType.ConstraintSatisfaction,
                PerpendicularLevels = 1,
                MaximumCandidates = 1
            };

            Arrange.Run(new List<Arrange> { a1, a2, a3, a4 }, options);

            // a4 must be placed successfully as it is independent from the congested cluster
            Assert.True(a4.Placed);
            // The congested cluster must contain failed labels
            Assert.False(a1.Placed && a2.Placed && a3.Placed);
        }
    }
}
