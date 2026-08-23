using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.PlaneGeometry.Geometry;
using Xunit;

namespace GeometryHelper.ArrangeAlgorithms.UnitTest
{
    public class BoundedBacktrackingAlgorithmTests
    {
        [Fact]
        public void Arrange_Run_BoundedBacktracking_FindsSolution()
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
                Algorithm = ArrangeAlgorithmType.BoundedBacktracking,
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
        public void Arrange_Run_BoundedBacktracking_ReturnsFalseWhenFullyBlocked()
        {
            var leaderLine = new GeoLine2(0.0, 0.0, 10.0, 0.0);
            var a1 = new Arrange
            {
                GeoRectangle2 = new GeoRectangle2(new GeoPoint2(5.0, 0.0), 20.0, 10.0),
                GeoLine2 = leaderLine,
                BaseOffsetFromLine = 5.0
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

            Arrange.Run(new List<Arrange> { a1 }, options);

            // Bounded Backtracking returns Placed = false when completely blocked
            Assert.False(a1.Placed);
        }

        [Fact]
        public void Arrange_Run_BoundedBacktracking_PrefersCandidateNearestTheLeader()
        {
            // Previously, this sorted candidates by DESCENDING clearance, meaning it always selected the FURTHEST position
            // and threw all labels to the outermost perpendicular level even if closer spots were empty.
            var leader = new GeoLine2(0.0, 0.0, 40.0, 0.0);
            var label = new Arrange
            {
                GeoRectangle2 = new GeoRectangle2(leader.MidPoint, 20.0, 10.0),
                GeoLine2 = leader,
                BaseOffsetFromLine = 5.0
            };

            var options = new ArrangeOptions
            {
                Algorithm = ArrangeAlgorithmType.BoundedBacktracking,
                RowGap = 5.0,
                PerpendicularLevels = 3
            };

            Arrange.Run(new List<Arrange> { label }, options);
            var moved = new GeoRectangle2(label.GeoRectangle2.Center + label.TranslationVector, 20.0, 10.0);

            // There is only one label, so it must lie at the closest level: BaseOffset = 5 + 5 = 10.
            // The outermost level would give |Y| = 10 + 2 * (10 + 5) = 40.
            Assert.Equal(10.0, Math.Abs(moved.Center.Y), 6);
            Assert.True(label.Placed);
        }

        [Fact]
        public void Arrange_Run_BoundedBacktracking_FallsBackToGreedyWhenNoCleanSolutionExists()
        {
            // Four labels share a short guide segment, only one perpendicular level: no complete solution exists.
            // The algorithm must fallback to Greedy instead of throwing errors or leaving labels in their original places.
            var leader = new GeoLine2(0.0, 0.0, 10.0, 0.0);
            var labels = new List<Arrange>();
            for (int i = 0; i < 4; i++)
            {
                labels.Add(new Arrange
                {
                    GeoRectangle2 = new GeoRectangle2(leader.MidPoint, 20.0, 10.0),
                    GeoLine2 = leader,
                    BaseOffsetFromLine = 5.0
                });
            }

            var options = new ArrangeOptions
            {
                Algorithm = ArrangeAlgorithmType.BoundedBacktracking,
                RowGap = 5.0,
                PerpendicularLevels = 1
            };

            Arrange.Run(labels, options);

            // All labels must move away from the guide segment, even those that cannot find a clean spot.
            foreach (var label in labels)
            {
                Assert.NotEqual(GeoVector2.Zero, label.TranslationVector);
            }

            // And at least two labels must be placed cleanly (on opposite sides of the guide segment).
            Assert.True(labels.Count(x => x.Placed) >= 2);
        }

        [Fact]
        public void Arrange_Run_BoundedBacktracking_RespectsMaxBacktrackSteps()
        {
            // Budget equal to 0 forces the algorithm to give up immediately and fallback to Greedy,
            // but the returned result must still be valid and not empty or throwing errors.
            var leader = new GeoLine2(0.0, 0.0, 40.0, 0.0);
            var a = new Arrange { GeoRectangle2 = new GeoRectangle2(leader.MidPoint, 20.0, 10.0), GeoLine2 = leader, BaseOffsetFromLine = 5.0 };
            var b = new Arrange { GeoRectangle2 = new GeoRectangle2(leader.MidPoint, 20.0, 10.0), GeoLine2 = leader, BaseOffsetFromLine = 5.0 };

            var options = new ArrangeOptions
            {
                Algorithm = ArrangeAlgorithmType.BoundedBacktracking,
                RowGap = 5.0,
                PerpendicularLevels = 3,
                MaxBacktrackSteps = 0
            };

            Arrange.Run(new List<Arrange> { a, b }, options);

            var movedA = new GeoRectangle2(a.GeoRectangle2.Center + a.TranslationVector, 20.0, 10.0);
            var movedB = new GeoRectangle2(b.GeoRectangle2.Center + b.TranslationVector, 20.0, 10.0);
            Assert.False(movedA.CollidesWith(movedB));
        }
    }
}
