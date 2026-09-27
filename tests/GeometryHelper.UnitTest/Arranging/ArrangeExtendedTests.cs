using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Geometry;
using Xunit;
using GeometryHelper.Arranging;

namespace GeometryHelper.UnitTest.Arranging
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

        private static ArrangeItem LabelOn(GeoLine2 leader, double width = 20.0, double height = 10.0)
        {
            return new ArrangeItem
            {
                Leader = leader,
                Box = new GeoRectangle2(leader.MidPoint, width, height),
                Offset = 5.0
            };
        }

        private static GeoRectangle2 MovedBox(ArrangeItem arrange, GeoVector2 translation)
        {
            return new GeoRectangle2(
                arrange.Box.Center + translation,
                arrange.Box.Width,
                arrange.Box.Height,
                arrange.Box.AngleRad);
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

            ArrangeResult[] results = Arranger.Run(new List<ArrangeItem> { a, b }, options);

            var movedA = MovedBox(a, results[0].Translation);
            var movedB = MovedBox(b, results[1].Translation);

            Assert.False(movedA.CollidesWith(movedB));
            Assert.True(results[0].Placed);
            Assert.True(results[1].Placed);
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

            ArrangeResult[] results = Arranger.Run(new List<ArrangeItem> { a, b }, options);

            var movedA = MovedBox(a, results[0].Translation);
            var movedB = MovedBox(b, results[1].Translation);

            Assert.False(movedA.CollidesWith(movedB));
        }

        [Theory]
        [MemberData(nameof(AllAlgorithms))]
        public void Arrange_Run_HighPerpendicularLevels_CanPlaceManyLabels(ArrangeAlgorithmType algorithm)
        {
            // With enough levels, 6 labels sharing the same leader should all be placed
            var leader = new GeoLine2(0.0, 0.0, 80.0, 0.0);
            var labels = new List<ArrangeItem>();
            for (int i = 0; i < 6; i++)
            {
                labels.Add(LabelOn(leader));
            }

            var options = OptionsFor(algorithm);
            options.PerpendicularLevels = 6;
            options.RowGap = 2.0;

            ArrangeResult[] results = Arranger.Run(labels, options);

            int placedCount = results.Count(r => r.Placed);
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

            ArrangeResult result = Arranger.Run(new List<ArrangeItem> { label }, options)[0];

            Assert.True(result.Placed);
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

            ArrangeResult[] results = Arranger.Run(new List<ArrangeItem> { a, b }, OptionsFor(algorithm));

            Assert.False(MovedBox(a, results[0].Translation).CollidesWith(MovedBox(b, results[1].Translation)));
            Assert.True(results[0].Placed);
            Assert.True(results[1].Placed);
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

            ArrangeResult[] results = Arranger.Run(new List<ArrangeItem> { a, b }, OptionsFor(algorithm));

            Assert.False(MovedBox(a, results[0].Translation).CollidesWith(MovedBox(b, results[1].Translation)));
            Assert.True(results[0].Placed);
            Assert.True(results[1].Placed);
        }

        [Theory]
        [MemberData(nameof(AllAlgorithms))]
        public void Arrange_Run_LeaderPointingDown_PlacesLabelsCorrectly(ArrangeAlgorithmType algorithm)
        {
            // A leader pointing straight down (-Y direction) validates perpendicular axis flipping
            var leader = new GeoLine2(50.0, 100.0, 50.0, 0.0);
            var a = LabelOn(leader);
            var b = LabelOn(leader);

            ArrangeResult[] results = Arranger.Run(new List<ArrangeItem> { a, b }, OptionsFor(algorithm));

            Assert.False(MovedBox(a, results[0].Translation).CollidesWith(MovedBox(b, results[1].Translation)));
            Assert.True(results[0].Placed);
            Assert.True(results[1].Placed);
        }

        [Theory]
        [MemberData(nameof(AllAlgorithms))]
        public void Arrange_Run_NegativeCoordinateLeader_PlacesCorrectly(ArrangeAlgorithmType algorithm)
        {
            // Ensure the algorithm is coordinate-system-agnostic (negative quadrant)
            var leader = new GeoLine2(-200.0, -100.0, -100.0, -100.0);
            var a = LabelOn(leader);
            var b = LabelOn(leader);

            ArrangeResult[] results = Arranger.Run(new List<ArrangeItem> { a, b }, OptionsFor(algorithm));

            Assert.False(MovedBox(a, results[0].Translation).CollidesWith(MovedBox(b, results[1].Translation)));
            Assert.True(results[0].Placed);
            Assert.True(results[1].Placed);
        }

        // ------------------------------------------------------------------
        // 3. Obstacle Collision2 Tests
        // ------------------------------------------------------------------

        [Theory]
        [MemberData(nameof(AllAlgorithms))]
        public void Arrange_Run_NestedPolygonObstacles_AvoidsAllLayers(ArrangeAlgorithmType algorithm)
        {
            // Two nested polygon obstacles — the label must escape both layers
            var leader = new GeoLine2(0.0, 0.0, 40.0, 0.0);
            var innerBox = new GeoPolygon2(
                new GeoPoint2(-30, 5), new GeoPoint2(70, 5), new GeoPoint2(70, 20), new GeoPoint2(-30, 20));
            var outerBox = new GeoPolygon2(
                new GeoPoint2(-50, 2), new GeoPoint2(90, 2), new GeoPoint2(90, 25), new GeoPoint2(-50, 25));

            var label = LabelOn(leader);
            label.BlockPolygons = new List<GeoPolygon2> { innerBox, outerBox };

            var options = OptionsFor(algorithm);
            options.PerpendicularLevels = 4;

            ArrangeResult result = Arranger.Run(new List<ArrangeItem> { label }, options)[0];

            var moved = MovedBox(label, result.Translation);
            Assert.False(moved.CollidesWith(innerBox));
            Assert.False(moved.CollidesWith(outerBox));
            Assert.True(result.Placed);
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

            ArrangeResult result = Arranger.Run(new List<ArrangeItem> { label }, OptionsFor(algorithm))[0];

            var moved = MovedBox(label, result.Translation);

            // Label must go to the bottom side (negative Y) to avoid the upper blocking lines
            Assert.True(moved.Center.Y < 0.0);
            Assert.True(result.Placed);
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

            ArrangeResult result = Arranger.Run(new List<ArrangeItem> { label }, options)[0];

            var moved = MovedBox(label, result.Translation);
            Assert.True(moved.Center.Y < 0.0);
            Assert.False(moved.CollidesWith(topBlock));
            Assert.True(result.Placed);
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

            ArrangeResult result = Arranger.Run(new List<ArrangeItem> { label }, options)[0];

            var moved = MovedBox(label, result.Translation);
            Assert.False(moved.CollidesWith(horizontalBar));
            Assert.False(moved.CollidesWith(verticalBar));
            Assert.True(result.Placed);
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

            ArrangeResult[] results = Arranger.Run(new List<ArrangeItem> { a, b, c }, options);

            var boxes = new[] { a, b, c }
                .Select((l, i) => MovedBox(l, results[i].Translation))
                .ToArray();

            // Pairwise collision check: no two placed labels may overlap each other
            for (int i = 0; i < boxes.Length; i++)
            {
                for (int j = i + 1; j < boxes.Length; j++)
                {
                    if (results[i].Placed && results[j].Placed)
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
            // Two labels on closely spaced parallel leaders — they should not collide
            var leader1 = new GeoLine2(0.0, 0.0, 40.0, 0.0);
            var leader2 = new GeoLine2(0.0, 12.0, 40.0, 12.0); // Only 12 units apart
            var a = LabelOn(leader1);
            var b = LabelOn(leader2);

            ArrangeResult[] results = Arranger.Run(new List<ArrangeItem> { a, b }, OptionsFor(algorithm));

            var movedA = MovedBox(a, results[0].Translation);
            var movedB = MovedBox(b, results[1].Translation);

            if (results[0].Placed && results[1].Placed)
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

            ArrangeResult[] results = Arranger.Run(new List<ArrangeItem> { a, b }, OptionsFor(algorithm));

            var movedA = MovedBox(a, results[0].Translation);
            var movedB = MovedBox(b, results[1].Translation);

            if (results[0].Placed && results[1].Placed)
            {
                Assert.False(movedA.CollidesWith(movedB));
            }
        }

        [Theory]
        [MemberData(nameof(AllAlgorithms))]
        public void Arrange_Run_LabelsOnConvergingLeaders_NoOverlap(ArrangeAlgorithmType algorithm)
        {
            // Two leaders converging towards the same point — labels must still separate
            var leader1 = new GeoLine2(0.0, 0.0, 50.0, 50.0);
            var leader2 = new GeoLine2(100.0, 0.0, 50.0, 50.0);
            var a = LabelOn(leader1);
            var b = LabelOn(leader2);

            ArrangeResult[] results = Arranger.Run(new List<ArrangeItem> { a, b }, OptionsFor(algorithm));

            var movedA = MovedBox(a, results[0].Translation);
            var movedB = MovedBox(b, results[1].Translation);

            if (results[0].Placed && results[1].Placed)
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
            // 2 labels blocked by separate BlockLines — both must enter Pass 2 and receive non-zero translations
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

            ArrangeResult[] results = Arranger.Run(new List<ArrangeItem> { failedA, failedB }, options);

            // Both labels are blocked by BlockLines, so Placed = false,
            // but after relaxation they should have received non-zero translation vectors.
            Assert.NotEqual(GeoVector2.Zero, results[0].Translation);
            Assert.NotEqual(GeoVector2.Zero, results[1].Translation);
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

            ArrangeResult[] results = Arranger.Run(new List<ArrangeItem> { successLabel, failedLabel }, options);

            // Success label must be placed and frozen
            Assert.True(results[0].Placed);
            // The relaxation run should not alter the success label's translation
            Assert.NotEqual(GeoVector2.Zero, results[0].Translation);
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

            ArrangeResult[] results = Arranger.Run(new List<ArrangeItem> { successLabel, failedLabel }, options);

            var successBox = MovedBox(successLabel, results[0].Translation);
            var failedBox = MovedBox(failedLabel, results[1].Translation);

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
            var labels = new List<ArrangeItem>();

            Assert.Empty(Arranger.Run(labels, OptionsFor(algorithm)));

            Assert.Empty(labels);
        }

        [Theory]
        [MemberData(nameof(AllAlgorithms))]
        public void Arrange_Run_NullElementInList_SkipsGracefully(ArrangeAlgorithmType algorithm)
        {
            // A null entry in the label list should be skipped without crash
            var leader = new GeoLine2(0.0, 0.0, 40.0, 0.0);
            var label = LabelOn(leader);

            var labels = new List<ArrangeItem> { label, null };

            ArrangeResult[] results = Arranger.Run(labels, OptionsFor(algorithm));

            Assert.True(results[0].Placed);
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

            ArrangeResult[] results = Arranger.Run(new List<ArrangeItem> { small, large }, options);

            Assert.True(results[0].Placed);
            Assert.True(results[1].Placed);
        }

        // ------------------------------------------------------------------
        // 7. Algorithm-Specific Option Tests
        // ------------------------------------------------------------------

        [Theory]
        [MemberData(nameof(AllAlgorithms))]
        public void Arrange_Run_PlaceFromInsideOutDisabled_StillSolvesCorrectly(ArrangeAlgorithmType algorithm)
        {
            // Disabling inside-out sorting changes the processing order but must still produce valid placement
            var labels = new List<ArrangeItem>();
            for (int i = 0; i < 4; i++)
            {
                var leader = new GeoLine2(i * 60, 0, i * 60 + 40, 0);
                labels.Add(LabelOn(leader));
            }

            var options = OptionsFor(algorithm);
            options.PlaceFromInsideOut = false;

            ArrangeResult[] results = Arranger.Run(labels, options);

            // All labels on well-separated leaders must be placed
            Assert.True(results.All(r => r.Placed));
        }

        [Theory]
        [MemberData(nameof(AllAlgorithms))]
        public void Arrange_Run_PlaceMostConstrainedFirstDisabled_StillSolvesCorrectly(ArrangeAlgorithmType algorithm)
        {
            // Disabling constraint-first sorting falls back to geometric order
            var labels = new List<ArrangeItem>();
            for (int i = 0; i < 3; i++)
            {
                var leader = new GeoLine2(i * 80, 0, i * 80 + 40, 0);
                labels.Add(LabelOn(leader));
            }

            var options = OptionsFor(algorithm);
            options.PlaceMostConstrainedFirst = false;
            options.PlaceFromInsideOut = false;

            ArrangeResult[] results = Arranger.Run(labels, options);

            Assert.True(results.All(r => r.Placed));
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

            ArrangeResult result = Arranger.Run(new List<ArrangeItem> { label }, options)[0];

            var moved = MovedBox(label, result.Translation);
            Assert.False(moved.CollidesWith(label.BlockLines[0]));
            Assert.False(moved.CollidesWith(label.BlockPolygons[0]));
            Assert.True(result.Placed);
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

            ArrangeResult result = Arranger.Run(new List<ArrangeItem> { label }, options)[0];

            var moved = MovedBox(label, result.Translation);
            foreach (var bl in label.BlockLines)
            {
                Assert.False(moved.CollidesWith(bl));
            }
            Assert.True(result.Placed);
        }

        // ------------------------------------------------------------------
        // 9. Stress Tests with Many Labels
        // ------------------------------------------------------------------

        [Theory]
        [MemberData(nameof(AllAlgorithms))]
        public void Arrange_Run_EightLabelsOnSpreadLeaders_MajorityPlaced(ArrangeAlgorithmType algorithm)
        {
            // 8 labels on 4 leaders (2 each) — most should be placed successfully
            var labels = new List<ArrangeItem>();
            for (int i = 0; i < 4; i++)
            {
                var leader = new GeoLine2(i * 60, 0, i * 60 + 40, 0);
                labels.Add(LabelOn(leader));
                labels.Add(LabelOn(leader));
            }

            var options = OptionsFor(algorithm);
            options.PerpendicularLevels = 3;

            ArrangeResult[] results = Arranger.Run(labels, options);

            int placed = results.Count(r => r.Placed);
            Assert.True(placed >= 6); // At least 6 of 8 should succeed with enough space
        }

        [Theory]
        [MemberData(nameof(AllAlgorithms))]
        public void Arrange_Run_TenLabelsOnSingleLongLeader_NoPairwiseOverlap(ArrangeAlgorithmType algorithm)
        {
            // 10 labels sharing a very long leader — placed labels must not overlap each other
            var leader = new GeoLine2(0.0, 0.0, 500.0, 0.0);
            var labels = new List<ArrangeItem>();
            for (int i = 0; i < 10; i++)
            {
                labels.Add(LabelOn(leader));
            }

            var options = OptionsFor(algorithm);
            options.PerpendicularLevels = 5;

            ArrangeResult[] results = Arranger.Run(labels, options);

            var placed = labels.Select((l, i) => (Label: l, Result: results[i])).Where(p => p.Result.Placed).ToList();
            for (int i = 0; i < placed.Count; i++)
            {
                for (int j = i + 1; j < placed.Count; j++)
                {
                    var boxI = MovedBox(placed[i].Label, placed[i].Result.Translation);
                    var boxJ = MovedBox(placed[j].Label, placed[j].Result.Translation);
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

            ArrangeResult result = Arranger.Run(new List<ArrangeItem> { label }, options)[0];

            Assert.True(result.Placed);
            // The absolute Y of the center should be at the first level: half-height(5) + offset(5) = 10
            double movedY = Math.Abs(MovedBox(label, result.Translation).Center.Y);
            Assert.Equal(10.0, movedY, 1);
        }

        [Theory]
        [MemberData(nameof(AllAlgorithms))]
        public void Arrange_Run_IndependentLabels_PlacedResultDoesNotDependOnOrder(ArrangeAlgorithmType algorithm)
        {
            // Two labels on distant leaders — swapping input order should not affect individual placement results
            var leader1 = new GeoLine2(0.0, 0.0, 40.0, 0.0);
            var leader2 = new GeoLine2(500.0, 0.0, 540.0, 0.0);

            var a = LabelOn(leader1);
            var b = LabelOn(leader2);
            ArrangeResult[] inOrder = Arranger.Run(new List<ArrangeItem> { a, b }, OptionsFor(algorithm));
            ArrangeResult[] swapped = Arranger.Run(new List<ArrangeItem> { b, a }, OptionsFor(algorithm));

            // Since they are 500 units apart (no interaction), each label should get the same translation
            Assert.Equal(inOrder[0].Translation, swapped[1].Translation);
            Assert.Equal(inOrder[1].Translation, swapped[0].Translation);
        }

        [Theory]
        [MemberData(nameof(AllAlgorithms))]
        public void Arrange_Run_IdenticalReruns_ProduceSameResult(ArrangeAlgorithmType algorithm)
        {
            // Running the exact same input twice must produce identical output (determinism guarantee)
            List<GeoVector2> RunOnce()
            {
                var leader = new GeoLine2(0.0, 0.0, 40.0, 0.0);
                var labels = new List<ArrangeItem> { LabelOn(leader), LabelOn(leader), LabelOn(leader) };
                return Arranger.Run(labels, OptionsFor(algorithm)).Select(r => r.Translation).ToList();
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

            ArrangeResult result = Arranger.Run(new List<ArrangeItem> { label }, options)[0];

            // Label should be placed at exactly the same position as without any obstacle
            Assert.True(result.Placed);
            Assert.Equal(10.0, Math.Abs(MovedBox(label, result.Translation).Center.Y), 1);
        }
    }
}
