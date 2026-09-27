using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Geometry;
using Xunit;
using GeometryHelper.Arranging;

namespace GeometryHelper.UnitTest.Arranging
{
    public class BoundedBacktrackingAlgorithmTests
    {
        [Fact]
        public void Arrange_Run_BoundedBacktracking_FindsSolution()
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
                Algorithm = ArrangeAlgorithmType.BoundedBacktracking,
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

        [Fact]
        public void Arrange_Run_BoundedBacktracking_ReturnsFalseWhenFullyBlocked()
        {
            var leaderLine = new GeoLine2(0.0, 0.0, 10.0, 0.0);
            var a1 = new ArrangeItem
            {
                Box = new GeoRectangle2(new GeoPoint2(5.0, 0.0), 20.0, 10.0),
                Leader = leaderLine,
                Offset = 5.0
            };

            // Huge blocked polygon wrapping the entire candidate space
            var blockPoly = new GeoPolygon2(
                new GeoPoint2(-100.0, -100.0),
                new GeoPoint2(100.0, -100.0),
                new GeoPoint2(100.0, 100.0),
                new GeoPoint2(-100.0, 100.0)
            );
            a1.BlockPolygons = new List<GeoPolygon2> { blockPoly };

            var options = new ArrangeOptions
            {
                Algorithm = ArrangeAlgorithmType.BoundedBacktracking,
                RowGap = 5.0,
                PerpendicularLevels = 2
            };

            ArrangeResult result = Arranger.Run(new List<ArrangeItem> { a1 }, options)[0];

            // Bounded Backtracking returns Placed = false when completely blocked
            Assert.False(result.Placed);
        }

        [Fact]
        public void Arrange_Run_BoundedBacktracking_PrefersCandidateNearestTheLeader()
        {
            // Previously, this sorted candidates by DESCENDING clearance, meaning it always selected the FURTHEST position
            // and threw all labels to the outermost perpendicular level even if closer spots were empty.
            var leader = new GeoLine2(0.0, 0.0, 40.0, 0.0);
            var label = new ArrangeItem
            {
                Box = new GeoRectangle2(leader.MidPoint, 20.0, 10.0),
                Leader = leader,
                Offset = 5.0
            };

            var options = new ArrangeOptions
            {
                Algorithm = ArrangeAlgorithmType.BoundedBacktracking,
                RowGap = 5.0,
                PerpendicularLevels = 3
            };

            ArrangeResult result = Arranger.Run(new List<ArrangeItem> { label }, options)[0];
            var moved = new GeoRectangle2(label.Box.Center + result.Translation, 20.0, 10.0);

            // There is only one label, so it must lie at the closest level: BaseOffset = 5 + 5 = 10.
            // The outermost level would give |Y| = 10 + 2 * (10 + 5) = 40.
            Assert.Equal(10.0, Math.Abs(moved.Center.Y), 6);
            Assert.True(result.Placed);
        }

        [Fact]
        public void Arrange_Run_BoundedBacktracking_FallsBackToGreedyWhenNoCleanSolutionExists()
        {
            // Four labels share a short guide segment, only one perpendicular level: no complete solution exists.
            // The algorithm must fallback to Greedy instead of throwing errors or leaving labels in their original places.
            var leader = new GeoLine2(0.0, 0.0, 10.0, 0.0);
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

            var options = new ArrangeOptions
            {
                Algorithm = ArrangeAlgorithmType.BoundedBacktracking,
                RowGap = 5.0,
                PerpendicularLevels = 1
            };

            ArrangeResult[] results = Arranger.Run(labels, options);

            // All labels must move away from the guide segment, even those that cannot find a clean spot.
            foreach (ArrangeResult result in results)
            {
                Assert.NotEqual(GeoVector2.Zero, result.Translation);
            }

            // And at least two labels must be placed cleanly (on opposite sides of the guide segment).
            Assert.True(results.Count(x => x.Placed) >= 2);
        }

        [Fact]
        public void Arrange_Run_BoundedBacktracking_RespectsMaxBacktrackSteps()
        {
            // Budget equal to 0 forces the algorithm to give up immediately and fallback to Greedy,
            // but the returned result must still be valid and not empty or throwing errors.
            var leader = new GeoLine2(0.0, 0.0, 40.0, 0.0);
            var a = new ArrangeItem { Box = new GeoRectangle2(leader.MidPoint, 20.0, 10.0), Leader = leader, Offset = 5.0 };
            var b = new ArrangeItem { Box = new GeoRectangle2(leader.MidPoint, 20.0, 10.0), Leader = leader, Offset = 5.0 };

            var options = new ArrangeOptions
            {
                Algorithm = ArrangeAlgorithmType.BoundedBacktracking,
                RowGap = 5.0,
                PerpendicularLevels = 3,
                MaxBacktrackSteps = 0
            };

            ArrangeResult[] results = Arranger.Run(new List<ArrangeItem> { a, b }, options);

            var movedA = new GeoRectangle2(a.Box.Center + results[0].Translation, 20.0, 10.0);
            var movedB = new GeoRectangle2(b.Box.Center + results[1].Translation, 20.0, 10.0);
            Assert.False(movedA.CollidesWith(movedB));
        }
    }
}
