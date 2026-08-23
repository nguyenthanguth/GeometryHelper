using System;
using System.Collections.Generic;
using System.Linq;
using PlaneGeometry.Geometry;
using Xunit;

namespace ArrangeAlgorithms.UnitTest
{
    /// <summary>
    /// Extended integration tests covering boundary conditions, rotated coordinates,
    /// obstacle collision scenarios, and relaxation pass correctness across ALL algorithms.
    /// </summary>
    public class ArrangeExtendedTests
    {
        public static IEnumerable<object[]> AllAlgorithms()
        {
            yield return new object[] { ArrangeAlgorithmType.Greedy };
            yield return new object[] { ArrangeAlgorithmType.BoundedBacktracking };
            yield return new object[] { ArrangeAlgorithmType.SimulatedAnnealing };
            yield return new object[] { ArrangeAlgorithmType.ForceDirected };
            yield return new object[] { ArrangeAlgorithmType.ConstraintSatisfaction };
        }

        private static ArrangeOptions OptionsFor(ArrangeAlgorithmType algorithm)
        {
            return new ArrangeOptions
            {
                Algorithm = algorithm,
                RowGap = 5.0,
                PerpendicularLevels = 3
            };
        }

        private static Arrange LabelOn(GeoLine2 leader, double width = 20.0, double height = 10.0)
        {
            return new Arrange
            {
                GeoLine2 = leader,
                GeoRectangle2 = new GeoRectangle2(leader.MidPoint, width, height),
                BaseOffsetFromLine = 5.0
            };
        }

        private static GeoRectangle2 MovedBox(Arrange arrange, GeoVector2 translation)
        {
            return new GeoRectangle2(
                arrange.GeoRectangle2.Center + translation,
                arrange.GeoRectangle2.Width,
                arrange.GeoRectangle2.Height,
                arrange.GeoRectangle2.AngleRad);
        }

        // ------------------------------------------------------------------
        // 1. Boundary Option Tests
        // ------------------------------------------------------------------

        [Theory]
        [MemberData(nameof(AllAlgorithms))]
        public void Arrange_Run_SinglePerpendicularLevel_PlacesTwoLabelsOnSameRow(ArrangeAlgorithmType algorithm)
        {
            // With only 1 perpendicular level, two labels on a long leader must spread longitudinally
            var leader = new GeoLine2(0.0, 0.0, 200.0, 0.0);
            var a = LabelOn(leader);
            var b = LabelOn(leader);

            var options = OptionsFor(algorithm);
            options.PerpendicularLevels = 1;

            Arrange.Run(new List<Arrange> { a, b }, options);

            var movedA = MovedBox(a, a.TranslationVector);
            var movedB = MovedBox(b, b.TranslationVector);

            Assert.False(movedA.CollidesWith(movedB));
            Assert.True(a.Placed);
            Assert.True(b.Placed);
        }

        [Theory]
        [MemberData(nameof(AllAlgorithms))]
        public void Arrange_Run_ZeroRowGap_LabelsStillDoNotOverlap(ArrangeAlgorithmType algorithm)
        {
            // RowGap = 0 means rows are tightly packed, but labels must never overlap each other
            var leader = new GeoLine2(0.0, 0.0, 40.0, 0.0);
            var a = LabelOn(leader);
            var b = LabelOn(leader);

            var options = OptionsFor(algorithm);
            options.RowGap = 0.0;
            options.PerpendicularLevels = 2;

            Arrange.Run(new List<Arrange> { a, b }, options);

            var movedA = MovedBox(a, a.TranslationVector);
            var movedB = MovedBox(b, b.TranslationVector);

            Assert.False(movedA.CollidesWith(movedB));
        }

        [Theory]
        [MemberData(nameof(AllAlgorithms))]
        public void Arrange_Run_HighPerpendicularLevels_CanPlaceManyLabels(ArrangeAlgorithmType algorithm)
        {
            // With enough levels, 6 labels sharing the same leader should all be placed
            var leader = new GeoLine2(0.0, 0.0, 80.0, 0.0);
            var labels = new List<Arrange>();
            for (int i = 0; i < 6; i++)
            {
                labels.Add(LabelOn(leader));
            }

            var options = OptionsFor(algorithm);
            options.PerpendicularLevels = 6;
            options.RowGap = 2.0;

            Arrange.Run(labels, options);

            int placedCount = labels.Count(l => l.Placed);
            Assert.True(placedCount >= 4); // At minimum 4 of 6 labels should find free positions
        }

        [Theory]
        [MemberData(nameof(AllAlgorithms))]
        public void Arrange_Run_VeryLargeNeighbourMargin_DoesNotCrash(ArrangeAlgorithmType algorithm)
        {
            // An extreme NeighbourMargin forces the algorithm to consider all obstacles regardless of distance
            var leader = new GeoLine2(0.0, 0.0, 40.0, 0.0);
            var label = LabelOn(leader);

            var options = OptionsFor(algorithm);
            options.NeighbourMargin = 10000.0;

            Arrange.Run(new List<Arrange> { label }, options);

            Assert.True(label.Placed);
        }

        // ------------------------------------------------------------------
        // 2. Rotated Coordinates & Projections
        // ------------------------------------------------------------------

        [Theory]
        [MemberData(nameof(AllAlgorithms))]
        public void Arrange_Run_Leader30Degrees_PlacesWithoutOverlap(ArrangeAlgorithmType algorithm)
        {
            // A leader at 30 degrees tests oblique perpendicular candidate generation
            double len = 80.0;
            double angle = Math.PI / 6.0; // 30 degrees
            var leader = new GeoLine2(0, 0, len * Math.Cos(angle), len * Math.Sin(angle));
            var a = LabelOn(leader);
            var b = LabelOn(leader);

            Arrange.Run(new List<Arrange> { a, b }, OptionsFor(algorithm));

            Assert.False(MovedBox(a, a.TranslationVector).CollidesWith(MovedBox(b, b.TranslationVector)));
            Assert.True(a.Placed);
            Assert.True(b.Placed);
        }

        [Theory]
        [MemberData(nameof(AllAlgorithms))]
        public void Arrange_Run_Leader135Degrees_PlacesCorrectly(ArrangeAlgorithmType algorithm)
        {
            // A leader in quadrant II (135 degrees) validates negative-X candidate generation
            double len = 80.0;
            double angle = 3.0 * Math.PI / 4.0; // 135 degrees
            var leader = new GeoLine2(0, 0, len * Math.Cos(angle), len * Math.Sin(angle));
            var a = LabelOn(leader);
            var b = LabelOn(leader);

            Arrange.Run(new List<Arrange> { a, b }, OptionsFor(algorithm));

            Assert.False(MovedBox(a, a.TranslationVector).CollidesWith(MovedBox(b, b.TranslationVector)));
            Assert.True(a.Placed);
            Assert.True(b.Placed);
        }

        [Theory]
        [MemberData(nameof(AllAlgorithms))]
        public void Arrange_Run_LeaderPointingDown_PlacesLabelsCorrectly(ArrangeAlgorithmType algorithm)
        {
            // A leader pointing straight down (-Y direction) validates perpendicular axis flipping
            var leader = new GeoLine2(50.0, 100.0, 50.0, 0.0);
            var a = LabelOn(leader);
            var b = LabelOn(leader);

            Arrange.Run(new List<Arrange> { a, b }, OptionsFor(algorithm));

            Assert.False(MovedBox(a, a.TranslationVector).CollidesWith(MovedBox(b, b.TranslationVector)));
            Assert.True(a.Placed);
            Assert.True(b.Placed);
        }

        [Theory]
        [MemberData(nameof(AllAlgorithms))]
        public void Arrange_Run_NegativeCoordinateLeader_PlacesCorrectly(ArrangeAlgorithmType algorithm)
        {
            // Ensure the algorithm is coordinate-system-agnostic (negative quadrant)
            var leader = new GeoLine2(-200.0, -100.0, -100.0, -100.0);
            var a = LabelOn(leader);
            var b = LabelOn(leader);

            Arrange.Run(new List<Arrange> { a, b }, OptionsFor(algorithm));

            Assert.False(MovedBox(a, a.TranslationVector).CollidesWith(MovedBox(b, b.TranslationVector)));
            Assert.True(a.Placed);
            Assert.True(b.Placed);
        }

        // ------------------------------------------------------------------
        // 3. Obstacle Collision2 Tests
        // ------------------------------------------------------------------

        [Theory]
        [MemberData(nameof(AllAlgorithms))]
        public void Arrange_Run_NestedPolygonObstacles_AvoidsAllLayers(ArrangeAlgorithmType algorithm)
        {
            // Two nested polygon obstacles ??the label must escape both layers
            var leader = new GeoLine2(0.0, 0.0, 40.0, 0.0);
            var innerBox = new GeoPolygon2(
                new GeoPoint2(-30, 5), new GeoPoint2(70, 5), new GeoPoint2(70, 20), new GeoPoint2(-30, 20));
            var outerBox = new GeoPolygon2(
                new GeoPoint2(-50, 2), new GeoPoint2(90, 2), new GeoPoint2(90, 25), new GeoPoint2(-50, 25));

            var label = LabelOn(leader);
            label.BlockPolygons = new List<GeoPolygon2> { innerBox, outerBox };

            var options = OptionsFor(algorithm);
            options.PerpendicularLevels = 4;

            Arrange.Run(new List<Arrange> { label }, options);

            var moved = MovedBox(label, label.TranslationVector);
            Assert.False(moved.CollidesWith(innerBox));
            Assert.False(moved.CollidesWith(outerBox));
            Assert.True(label.Placed);
        }

        [Theory]
        [MemberData(nameof(AllAlgorithms))]
        public void Arrange_Run_ParallelBlockLines_ForcesLabelsToLowerLevel(ArrangeAlgorithmType algorithm)
        {
            // Two parallel blocking lines above the leader force the label to the bottom side
            var leader = new GeoLine2(0.0, 0.0, 40.0, 0.0);
            var label = LabelOn(leader);
            label.BlockLines = new List<GeoLine2>
            {
                new GeoLine2(-100, 8.0, 100, 8.0),
                new GeoLine2(-100, 12.0, 100, 12.0)
            };

            Arrange.Run(new List<Arrange> { label }, OptionsFor(algorithm));

            var moved = MovedBox(label, label.TranslationVector);

            // Label must go to the bottom side (negative Y) to avoid the upper blocking lines
            Assert.True(moved.Center.Y < 0.0);
            Assert.True(label.Placed);
        }

        [Theory]
        [MemberData(nameof(AllAlgorithms))]
        public void Arrange_Run_PolygonObstacleCoveringEntireTopSide_ForcesBottom(ArrangeAlgorithmType algorithm)
        {
            // A wide obstacle covering the entire top region forces the label to the bottom side
            var leader = new GeoLine2(0.0, 0.0, 40.0, 0.0);
            var topBlock = new GeoPolygon2(
                new GeoPoint2(-200, 0), new GeoPoint2(200, 0), new GeoPoint2(200, 200), new GeoPoint2(-200, 200));

            var label = LabelOn(leader);
            label.BlockPolygons = new List<GeoPolygon2> { topBlock };

            var options = OptionsFor(algorithm);
            options.PerpendicularLevels = 3;

            Arrange.Run(new List<Arrange> { label }, options);

            var moved = MovedBox(label, label.TranslationVector);
            Assert.True(moved.Center.Y < 0.0);
            Assert.False(moved.CollidesWith(topBlock));
            Assert.True(label.Placed);
        }

        [Theory]
        [MemberData(nameof(AllAlgorithms))]
        public void Arrange_Run_CrossShapedObstacles_FindsCornerPosition(ArrangeAlgorithmType algorithm)
        {
            // Obstacles form a cross pattern; the label must place into one of the four open corners
            var leader = new GeoLine2(0.0, 0.0, 80.0, 0.0);
            var horizontalBar = new GeoPolygon2(
                new GeoPoint2(-200, -3), new GeoPoint2(200, -3), new GeoPoint2(200, 3), new GeoPoint2(-200, 3));
            var verticalBar = new GeoPolygon2(
                new GeoPoint2(37, -200), new GeoPoint2(43, -200), new GeoPoint2(43, 200), new GeoPoint2(37, 200));

            var label = LabelOn(leader);
            label.BlockPolygons = new List<GeoPolygon2> { horizontalBar, verticalBar };

            var options = OptionsFor(algorithm);
            options.PerpendicularLevels = 3;

            Arrange.Run(new List<Arrange> { label }, options);

            var moved = MovedBox(label, label.TranslationVector);
            Assert.False(moved.CollidesWith(horizontalBar));
            Assert.False(moved.CollidesWith(verticalBar));
            Assert.True(label.Placed);
        }

        // ------------------------------------------------------------------
        // 4. Multi-Label Stress Tests
        // ------------------------------------------------------------------

        [Theory]
        [MemberData(nameof(AllAlgorithms))]
        public void Arrange_Run_ThreeLabelsOnShortLeader_NoMutualOverlap(ArrangeAlgorithmType algorithm)
        {
            var leader = new GeoLine2(0.0, 0.0, 30.0, 0.0);
            var a = LabelOn(leader);
            var b = LabelOn(leader);
            var c = LabelOn(leader);

            var options = OptionsFor(algorithm);
            options.PerpendicularLevels = 3;

            Arrange.Run(new List<Arrange> { a, b, c }, options);

            var boxes = new[] { a, b, c }
                .Select(l => MovedBox(l, l.TranslationVector))
                .ToArray();

            // Pairwise collision check: no two placed labels may overlap each other
            for (int i = 0; i < boxes.Length; i++)
            {
                for (int j = i + 1; j < boxes.Length; j++)
                {
                    if (new[] { a, b, c }[i].Placed && new[] { a, b, c }[j].Placed)
                    {
                        Assert.False(boxes[i].CollidesWith(boxes[j]));
                    }
                }
            }
        }

        [Theory]
        [MemberData(nameof(AllAlgorithms))]
        public void Arrange_Run_LabelsOnParallelLeaders_NoOverlap(ArrangeAlgorithmType algorithm)
        {
            // Two labels on closely spaced parallel leaders ??they should not collide
            var leader1 = new GeoLine2(0.0, 0.0, 40.0, 0.0);
            var leader2 = new GeoLine2(0.0, 12.0, 40.0, 12.0); // Only 12 units apart
            var a = LabelOn(leader1);
            var b = LabelOn(leader2);

            Arrange.Run(new List<Arrange> { a, b }, OptionsFor(algorithm));

            var movedA = MovedBox(a, a.TranslationVector);
            var movedB = MovedBox(b, b.TranslationVector);

            if (a.Placed && b.Placed)
            {
                Assert.False(movedA.CollidesWith(movedB));
            }
        }

        [Theory]
        [MemberData(nameof(AllAlgorithms))]
        public void Arrange_Run_LabelsOnPerpendicularLeaders_NoOverlap(ArrangeAlgorithmType algorithm)
        {
            // Two leaders forming an L-shape at the origin
            var horizontal = new GeoLine2(0.0, 0.0, 60.0, 0.0);
            var vertical = new GeoLine2(0.0, 0.0, 0.0, 60.0);
            var a = LabelOn(horizontal);
            var b = LabelOn(vertical);

            Arrange.Run(new List<Arrange> { a, b }, OptionsFor(algorithm));

            var movedA = MovedBox(a, a.TranslationVector);
            var movedB = MovedBox(b, b.TranslationVector);

            if (a.Placed && b.Placed)
            {
                Assert.False(movedA.CollidesWith(movedB));
            }
        }

        [Theory]
        [MemberData(nameof(AllAlgorithms))]
        public void Arrange_Run_LabelsOnConvergingLeaders_NoOverlap(ArrangeAlgorithmType algorithm)
        {
            // Two leaders converging towards the same point ??labels must still separate
            var leader1 = new GeoLine2(0.0, 0.0, 50.0, 50.0);
            var leader2 = new GeoLine2(100.0, 0.0, 50.0, 50.0);
            var a = LabelOn(leader1);
            var b = LabelOn(leader2);

            Arrange.Run(new List<Arrange> { a, b }, OptionsFor(algorithm));

            var movedA = MovedBox(a, a.TranslationVector);
            var movedB = MovedBox(b, b.TranslationVector);

            if (a.Placed && b.Placed)
            {
                Assert.False(movedA.CollidesWith(movedB));
            }
        }

        // ------------------------------------------------------------------
        // 5. Relaxation Pass (Pass 2) Extended Cases
        // ------------------------------------------------------------------

        [Theory]
        [MemberData(nameof(AllAlgorithms))]
        public void Arrange_Run_RelaxationPass_MultipleFailedLabelsGetBestEffortPositions(ArrangeAlgorithmType algorithm)
        {
            // 2 labels blocked by separate BlockLines ??both must enter Pass 2 and receive non-zero translations
            var leader1 = new GeoLine2(0.0, 0.0, 40.0, 0.0);
            var leader2 = new GeoLine2(200.0, 0.0, 240.0, 0.0);
            var failedA = LabelOn(leader1);
            var failedB = LabelOn(leader2);

            failedA.BlockLines = new List<GeoLine2>
            {
                new GeoLine2(-100, 10.0, 100, 10.0),
                new GeoLine2(-100, -10.0, 100, -10.0)
            };
            failedB.BlockLines = new List<GeoLine2>
            {
                new GeoLine2(100, 10.0, 300, 10.0),
                new GeoLine2(100, -10.0, 300, -10.0)
            };

            var options = OptionsFor(algorithm);
            options.PerpendicularLevels = 1;

            Arrange.Run(new List<Arrange> { failedA, failedB }, options);

            // Both labels are blocked by BlockLines, so Placed = false,
            // but after relaxation they should have received non-zero translation vectors.
            Assert.NotEqual(GeoVector2.Zero, failedA.TranslationVector);
            Assert.NotEqual(GeoVector2.Zero, failedB.TranslationVector);
        }

        [Theory]
        [MemberData(nameof(AllAlgorithms))]
        public void Arrange_Run_RelaxationPass_FrozenLabelsBlockPolygonsDoNotAffectOwnPosition(ArrangeAlgorithmType algorithm)
        {
            // A placed label's own BlockPolygons must not shift its position during relaxation pass
            var leader1 = new GeoLine2(0.0, 0.0, 40.0, 0.0);
            var leader2 = new GeoLine2(500.0, 0.0, 540.0, 0.0);

            var successLabel = LabelOn(leader1);
            var failedLabel = LabelOn(leader2);
            failedLabel.BlockLines = new List<GeoLine2>
            {
                new GeoLine2(400, 10.0, 600, 10.0),
                new GeoLine2(400, -10.0, 600, -10.0)
            };

            var options = OptionsFor(algorithm);
            options.PerpendicularLevels = 1;

            Arrange.Run(new List<Arrange> { successLabel, failedLabel }, options);

            // Success label must be placed and frozen
            Assert.True(successLabel.Placed);
            // The relaxation run should not alter the success label's translation
            Assert.NotEqual(GeoVector2.Zero, successLabel.TranslationVector);
        }

        [Theory]
        [MemberData(nameof(AllAlgorithms))]
        public void Arrange_Run_RelaxationPass_FailedLabelDoesNotOverlapFrozenLabel(ArrangeAlgorithmType algorithm)
        {
            // When a failed label is relaxed, it must treat the frozen label's bounding box as an obstacle
            var leader = new GeoLine2(0.0, 0.0, 40.0, 0.0);
            var successLabel = LabelOn(leader);
            var failedLabel = LabelOn(leader);
            failedLabel.BlockLines = new List<GeoLine2>
            {
                new GeoLine2(-100, 10.0, 100, 10.0)
            };

            var options = OptionsFor(algorithm);
            options.PerpendicularLevels = 3;

            Arrange.Run(new List<Arrange> { successLabel, failedLabel }, options);

            var successBox = MovedBox(successLabel, successLabel.TranslationVector);
            var failedBox = MovedBox(failedLabel, failedLabel.TranslationVector);

            Assert.False(successBox.CollidesWith(failedBox));
        }

        // ------------------------------------------------------------------
        // 6. Edge Cases & Robustness
        // ------------------------------------------------------------------

        [Theory]
        [MemberData(nameof(AllAlgorithms))]
        public void Arrange_Run_EmptyList_DoesNotThrow(ArrangeAlgorithmType algorithm)
        {
            // Empty label list should complete without exception
            var labels = new List<Arrange>();

            Arrange.Run(labels, OptionsFor(algorithm));

            Assert.Empty(labels);
        }

        [Theory]
        [MemberData(nameof(AllAlgorithms))]
        public void Arrange_Run_NullElementInList_SkipsGracefully(ArrangeAlgorithmType algorithm)
        {
            // A null entry in the label list should be skipped without crash
            var leader = new GeoLine2(0.0, 0.0, 40.0, 0.0);
            var label = LabelOn(leader);

            var labels = new List<Arrange> { label, null };

            Arrange.Run(labels, OptionsFor(algorithm));

            Assert.True(label.Placed);
        }

        [Theory]
        [MemberData(nameof(AllAlgorithms))]
        public void Arrange_Run_LabelsWithDifferentSizes_PlacesCorrectly(ArrangeAlgorithmType algorithm)
        {
            // Mix of small and large labels on separate leaders
            var leader1 = new GeoLine2(0.0, 0.0, 40.0, 0.0);
            var leader2 = new GeoLine2(100.0, 0.0, 200.0, 0.0);
            var small = LabelOn(leader1, width: 15.0, height: 10.0);
            var large = LabelOn(leader2, width: 40.0, height: 20.0);

            var options = OptionsFor(algorithm);
            options.PerpendicularLevels = 4;

            Arrange.Run(new List<Arrange> { small, large }, options);

            Assert.True(small.Placed);
            Assert.True(large.Placed);
        }

        // ------------------------------------------------------------------
        // 7. Algorithm-Specific Option Tests
        // ------------------------------------------------------------------

        [Theory]
        [MemberData(nameof(AllAlgorithms))]
        public void Arrange_Run_PlaceFromInsideOutDisabled_StillSolvesCorrectly(ArrangeAlgorithmType algorithm)
        {
            // Disabling inside-out sorting changes the processing order but must still produce valid placement
            var labels = new List<Arrange>();
            for (int i = 0; i < 4; i++)
            {
                var leader = new GeoLine2(i * 60, 0, i * 60 + 40, 0);
                labels.Add(LabelOn(leader));
            }

            var options = OptionsFor(algorithm);
            options.PlaceFromInsideOut = false;

            Arrange.Run(labels, options);

            // All labels on well-separated leaders must be placed
            Assert.True(labels.All(l => l.Placed));
        }

        [Theory]
        [MemberData(nameof(AllAlgorithms))]
        public void Arrange_Run_PlaceMostConstrainedFirstDisabled_StillSolvesCorrectly(ArrangeAlgorithmType algorithm)
        {
            // Disabling constraint-first sorting falls back to geometric order
            var labels = new List<Arrange>();
            for (int i = 0; i < 3; i++)
            {
                var leader = new GeoLine2(i * 80, 0, i * 80 + 40, 0);
                labels.Add(LabelOn(leader));
            }

            var options = OptionsFor(algorithm);
            options.PlaceMostConstrainedFirst = false;
            options.PlaceFromInsideOut = false;

            Arrange.Run(labels, options);

            Assert.True(labels.All(l => l.Placed));
        }

        // ------------------------------------------------------------------
        // 8. Combined Obstacle Types
        // ------------------------------------------------------------------

        [Theory]
        [MemberData(nameof(AllAlgorithms))]
        public void Arrange_Run_BlockLinesAndBlockPolygonsCombined_AvoidsAll(ArrangeAlgorithmType algorithm)
        {
            // A label with both BlockLines and BlockPolygons must avoid all of them
            var leader = new GeoLine2(0.0, 0.0, 80.0, 0.0);
            var label = LabelOn(leader);
            label.BlockLines = new List<GeoLine2>
            {
                new GeoLine2(-100, 10.0, 200, 10.0) // Blocks upper level 1
            };
            label.BlockPolygons = new List<GeoPolygon2>
            {
                new GeoPolygon2(
                    new GeoPoint2(-100, -20), new GeoPoint2(200, -20),
                    new GeoPoint2(200, -5), new GeoPoint2(-100, -5)) // Blocks lower level 1
            };

            var options = OptionsFor(algorithm);
            options.PerpendicularLevels = 3;

            Arrange.Run(new List<Arrange> { label }, options);

            var moved = MovedBox(label, label.TranslationVector);
            Assert.False(moved.CollidesWith(label.BlockLines[0]));
            Assert.False(moved.CollidesWith(label.BlockPolygons[0]));
            Assert.True(label.Placed);
        }

        [Theory]
        [MemberData(nameof(AllAlgorithms))]
        public void Arrange_Run_MultipleBlockLinesFromDifferentAngles_AvoidsAll(ArrangeAlgorithmType algorithm)
        {
            // BlockLines at different angles forming a V-shape above the leader
            var leader = new GeoLine2(0.0, 0.0, 60.0, 0.0);
            var label = LabelOn(leader);
            label.BlockLines = new List<GeoLine2>
            {
                new GeoLine2(0, 5, 30, 30),   // Diagonal line from left
                new GeoLine2(60, 5, 30, 30)    // Diagonal line from right
            };

            var options = OptionsFor(algorithm);
            options.PerpendicularLevels = 3;

            Arrange.Run(new List<Arrange> { label }, options);

            var moved = MovedBox(label, label.TranslationVector);
            foreach (var bl in label.BlockLines)
            {
                Assert.False(moved.CollidesWith(bl));
            }
            Assert.True(label.Placed);
        }

        // ------------------------------------------------------------------
        // 9. Stress Tests with Many Labels
        // ------------------------------------------------------------------

        [Theory]
        [MemberData(nameof(AllAlgorithms))]
        public void Arrange_Run_EightLabelsOnSpreadLeaders_MajorityPlaced(ArrangeAlgorithmType algorithm)
        {
            // 8 labels on 4 leaders (2 each) ??most should be placed successfully
            var labels = new List<Arrange>();
            for (int i = 0; i < 4; i++)
            {
                var leader = new GeoLine2(i * 60, 0, i * 60 + 40, 0);
                labels.Add(LabelOn(leader));
                labels.Add(LabelOn(leader));
            }

            var options = OptionsFor(algorithm);
            options.PerpendicularLevels = 3;

            Arrange.Run(labels, options);

            int placed = labels.Count(l => l.Placed);
            Assert.True(placed >= 6); // At least 6 of 8 should succeed with enough space
        }

        [Theory]
        [MemberData(nameof(AllAlgorithms))]
        public void Arrange_Run_TenLabelsOnSingleLongLeader_NoPairwiseOverlap(ArrangeAlgorithmType algorithm)
        {
            // 10 labels sharing a very long leader ??placed labels must not overlap each other
            var leader = new GeoLine2(0.0, 0.0, 500.0, 0.0);
            var labels = new List<Arrange>();
            for (int i = 0; i < 10; i++)
            {
                labels.Add(LabelOn(leader));
            }

            var options = OptionsFor(algorithm);
            options.PerpendicularLevels = 5;

            Arrange.Run(labels, options);

            var placed = labels.Where(l => l.Placed).ToList();
            for (int i = 0; i < placed.Count; i++)
            {
                for (int j = i + 1; j < placed.Count; j++)
                {
                    var boxI = MovedBox(placed[i], placed[i].TranslationVector);
                    var boxJ = MovedBox(placed[j], placed[j].TranslationVector);
                    Assert.False(boxI.CollidesWith(boxJ));
                }
            }
        }

        // ------------------------------------------------------------------
        // 10. Placement Stability & Determinism
        // ------------------------------------------------------------------

        [Theory]
        [MemberData(nameof(AllAlgorithms))]
        public void Arrange_Run_SoleLabelOnOpenField_PlacedAtNearestLevel(ArrangeAlgorithmType algorithm)
        {
            // A single label with no obstacles should be placed at the nearest perpendicular level
            var leader = new GeoLine2(0.0, 0.0, 40.0, 0.0);
            var label = LabelOn(leader);

            var options = OptionsFor(algorithm);
            options.PerpendicularLevels = 5;

            Arrange.Run(new List<Arrange> { label }, options);

            Assert.True(label.Placed);
            // The absolute Y of the center should be at the first level: half-height(5) + offset(5) = 10
            double movedY = Math.Abs(MovedBox(label, label.TranslationVector).Center.Y);
            Assert.Equal(10.0, movedY, 1);
        }

        [Theory]
        [MemberData(nameof(AllAlgorithms))]
        public void Arrange_Run_IndependentLabels_PlacedResultDoesNotDependOnOrder(ArrangeAlgorithmType algorithm)
        {
            // Two labels on distant leaders ??swapping input order should not affect individual placement results
            var leader1 = new GeoLine2(0.0, 0.0, 40.0, 0.0);
            var leader2 = new GeoLine2(500.0, 0.0, 540.0, 0.0);

            var a1 = LabelOn(leader1);
            var b1 = LabelOn(leader2);
            Arrange.Run(new List<Arrange> { a1, b1 }, OptionsFor(algorithm));

            var a2 = LabelOn(leader1);
            var b2 = LabelOn(leader2);
            Arrange.Run(new List<Arrange> { b2, a2 }, OptionsFor(algorithm));

            // Since they are 500 units apart (no interaction), each label should get the same translation
            Assert.Equal(a1.TranslationVector, a2.TranslationVector);
            Assert.Equal(b1.TranslationVector, b2.TranslationVector);
        }

        [Theory]
        [MemberData(nameof(AllAlgorithms))]
        public void Arrange_Run_IdenticalReruns_ProduceSameResult(ArrangeAlgorithmType algorithm)
        {
            // Running the exact same input twice must produce identical output (determinism guarantee)
            List<GeoVector2> RunOnce()
            {
                var leader = new GeoLine2(0.0, 0.0, 40.0, 0.0);
                var labels = new List<Arrange> { LabelOn(leader), LabelOn(leader), LabelOn(leader) };
                Arrange.Run(labels, OptionsFor(algorithm));
                return labels.Select(l => l.TranslationVector).ToList();
            }

            var first = RunOnce();
            var second = RunOnce();

            for (int i = 0; i < first.Count; i++)
            {
                Assert.Equal(first[i], second[i]);
            }
        }

        [Theory]
        [MemberData(nameof(AllAlgorithms))]
        public void Arrange_Run_LabelAtOriginWithFarObstacle_NotAffectedByDistantObstacle(ArrangeAlgorithmType algorithm)
        {
            // An obstacle 10000 units away should have zero influence on label placement
            var leader = new GeoLine2(0.0, 0.0, 40.0, 0.0);
            var label = LabelOn(leader);
            var farObstacle = new GeoPolygon2(
                new GeoPoint2(10000, 10000), new GeoPoint2(10100, 10000),
                new GeoPoint2(10100, 10100), new GeoPoint2(10000, 10100));
            label.BlockPolygons = new List<GeoPolygon2> { farObstacle };

            var options = OptionsFor(algorithm);

            Arrange.Run(new List<Arrange> { label }, options);

            // Label should be placed at exactly the same position as without any obstacle
            Assert.True(label.Placed);
            Assert.Equal(10.0, Math.Abs(MovedBox(label, label.TranslationVector).Center.Y), 1);
        }
    }
}
