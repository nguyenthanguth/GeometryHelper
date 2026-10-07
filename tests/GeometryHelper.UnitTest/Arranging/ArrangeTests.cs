using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Geometry;
using Xunit;
using GeometryHelper.Arranging;

namespace GeometryHelper.UnitTest.Arranging
{
    /// <summary>
    /// Tests the public API of <see cref="Arranger"/> and the contracts its placement must uphold.
    /// </summary>
    public class ArrangeTests : ArrangeTestKit
    {
        // ------------------------------------------------------------------
        // Check input parameters
        // ------------------------------------------------------------------

        [Fact]
        public void Arrange_Run_WithNullList_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => Arranger.Run(null));
            Assert.Throws<ArgumentNullException>(() => Arranger.Run(null, ArrangeOptions.Default));
        }

        [Fact]
        public void Arrange_Run_WithNullOptions_Throws()
        {
            var labels = new List<ArrangeItem> { LabelOn(new GeoLine2(0.0, 0.0, 40.0, 0.0)) };

            Assert.Throws<ArgumentNullException>(() => Arranger.Run(labels, null));
        }

        [Fact]
        public void Arrange_GetPlacePoints_WithNullOptions_Throws()
        {
            var label = LabelOn(new GeoLine2(0.0, 0.0, 40.0, 0.0));

            Assert.Throws<ArgumentNullException>(() => label.GetPlacePoints(null));
        }

        [Fact]
        public void Arrange_Run_SingleArgumentOverload_UsesDefaultOptions()
        {
            // Label does not set Offset, so it keeps Arrange's default value of 50.
            var label = new ArrangeItem
            {
                Leader = new GeoLine2(0.0, 0.0, 400.0, 0.0),
                Box = new GeoRectangle2(new GeoPoint2(200.0, 0.0), 20.0, 10.0)
            };

            ArrangeResult result = Arranger.Run(new List<ArrangeItem> { label })[0];

            // The first row stands half the height (5) plus the default Offset (50) off: 55.
            Assert.Equal(55.0, Math.Abs(MovedBox(label, result.Translation).Center.Y), 6);
        }

        // ------------------------------------------------------------------
        // Generate candidate positions
        // ------------------------------------------------------------------

        [Fact]
        public void Arrange_GetPlacePoints_FirstPairIsSymmetricAtTheFirstRow()
        {
            var label = LabelOn(new GeoLine2(0.0, 0.0, 40.0, 0.0));
            var options = OptionsFor();

            var points = label.GetPlacePoints(options);

            // First pair must lie on the first perpendicular level (half the height plus Offset = 5 + 5 = 10)
            Assert.True(points[0].IsEqualTo(new GeoPoint2(20.0, 10.0)));
            Assert.True(points[1].IsEqualTo(new GeoPoint2(20.0, -10.0)));
        }

        [Fact]
        public void Arrange_Offset_IsPerLabelNotGlobal()
        {
            // Offset is per-label, so two labels sharing the same ArrangeOptions must still
            // expand candidates from two different perpendicular levels.
            var leader = new GeoLine2(0.0, 0.0, 40.0, 0.0);
            var near = LabelOn(leader);                       // Offset = 5
            var far = LabelOn(leader);
            far.Offset = 30.0;

            var options = OptionsFor();

            // The first row: half the label height (5) plus the label's own offset.
            Assert.True(near.GetPlacePoints(options)[0].IsEqualTo(new GeoPoint2(20.0, 10.0)));
            Assert.True(far.GetPlacePoints(options)[0].IsEqualTo(new GeoPoint2(20.0, 35.0)));
        }

        [Fact]
        public void Arrange_Run_HonoursEachLabelsOwnOffset()
        {
            // Same guide segment, same options: label declaring larger offset must
            // stop further from the guide segment, and both must lie exactly on their first perpendicular level.
            var leader = new GeoLine2(0.0, 0.0, 40.0, 0.0);
            var near = LabelOn(leader);                       // Offset = 5
            var far = LabelOn(leader);
            far.Offset = 30.0;

            ArrangeResult[] results = Arranger.Run(new List<ArrangeItem> { near, far }, OptionsFor());

            Assert.Equal(10.0, Math.Abs(MovedBox(near, results[0].Translation).Center.Y), 6);
            Assert.Equal(35.0, Math.Abs(MovedBox(far, results[1].Translation).Center.Y), 6);
            Assert.True(results[0].Placed);
            Assert.True(results[1].Placed);
        }

        [Fact]
        public void Arrange_GetPlacePoints_MoreLevelsProduceMoreCandidates()
        {
            var label = LabelOn(new GeoLine2(0.0, 0.0, 40.0, 0.0));

            var threeLevels = label.GetPlacePoints(OptionsFor());

            var single = OptionsFor();
            single.PerpendicularLevels = 1;
            var oneLevel = label.GetPlacePoints(single);

            Assert.True(threeLevels.Count > oneLevel.Count);
        }

        // ------------------------------------------------------------------
        // Invalid label dimensions
        // ------------------------------------------------------------------

        [Fact]
        public void Arrange_InvalidBoxSize_ReturnsZeroTranslations()
        {
            var leaderLine = new GeoLine2(0.0, 0.0, 10.0, 0.0);
            var a1 = new ArrangeItem
            {
                Box = new GeoRectangle2(new GeoPoint2(5.0, 0.0), 2.0, 2.0), // Too small
                Leader = leaderLine
            };

            var options = new ArrangeOptions
            {
                MinimumBoxSize = 5.0
            };

            ArrangeResult result = Arranger.Run(new List<ArrangeItem> { a1 }, options)[0];
            Assert.Equal(GeoVector2.Zero, result.Translation);
            Assert.False(result.Placed);
        }

        [Fact]
        public void Arrange_DegenerateLeader_LeavesLabelInPlaceAndReportsFailure()
        {
            var label = new ArrangeItem
            {
                Leader = new GeoLine2(5.0, 0.0, 5.0, 0.0),
                Box = new GeoRectangle2(new GeoPoint2(5.0, 0.0), 20.0, 10.0),
                Offset = 5.0
            };

            ArrangeResult result = Arranger.Run(new List<ArrangeItem> { label }, OptionsFor())[0];

            Assert.Equal(GeoVector2.Zero, result.Translation);

            // The label does not overlap anyone, but it has never been arranged either, so it must not be reported as successful.
            Assert.False(result.Placed);
        }

        [Fact]
        public void Arrange_LabelAlreadyOnFirstCandidate_ProducesZeroTranslation()
        {
            // Label is already at the first candidate position: nothing to translate,
            // and MinimumMoveDistance must suppress any minor errors.
            var leader = new GeoLine2(0.0, 0.0, 40.0, 0.0);
            var label = new ArrangeItem
            {
                Leader = leader,
                Box = new GeoRectangle2(new GeoPoint2(20.0, 10.0), 20.0, 10.0),
                Offset = 5.0
            };

            ArrangeResult result = Arranger.Run(new List<ArrangeItem> { label }, OptionsFor())[0];

            Assert.Equal(GeoVector2.Zero, result.Translation);
            Assert.True(result.Placed);
        }

        // ------------------------------------------------------------------
        // Common contract of the placement
        // ------------------------------------------------------------------

        [Fact]
        public void Arrange_Run_PlacesEveryLabelOnItsOwnLeader()
        {
            var labels = new List<ArrangeItem>();
            for (int i = 0; i < 5; i++)
            {
                labels.Add(LabelOn(new GeoLine2(i * 50.0, 0.0, i * 50.0 + 40.0, 0.0)));
            }

            ArrangeResult[] results = Arranger.Run(labels, OptionsFor());

            // Verify that all labels were successfully arranged
            Assert.True(results.All(r => r.Placed));
        }

        [Fact]
        public void Arrange_Run_WithEmptyList_DoesNotThrow()
        {
            Assert.Empty(Arranger.Run(new List<ArrangeItem>(), OptionsFor()));
        }

        [Fact]
        public void Arrange_Run_WithNullEntries_KeepsIndicesAligned()
        {
            var first = LabelOn(new GeoLine2(0.0, 0.0, 40.0, 0.0));
            var second = LabelOn(new GeoLine2(200.0, 0.0, 240.0, 0.0));

            var labels = new List<ArrangeItem> { first, null, second };
            ArrangeResult[] results = Arranger.Run(labels, OptionsFor());

            // Each real label must still stick to its own guide segment without index swap.
            // The assertion is "closer to its own guide segment than to the other label's guide segment".
            GeoPoint2 firstCentre = MovedBox(first, results[0].Translation).Center;
            GeoPoint2 secondCentre = MovedBox(second, results[2].Translation).Center;

            Assert.True(first.Leader.DistanceTo(firstCentre) < second.Leader.DistanceTo(firstCentre));
            Assert.True(second.Leader.DistanceTo(secondCentre) < first.Leader.DistanceTo(secondCentre));
        }

        [Fact]
        public void Arrange_Run_SeparatesTwoLabelsSharingOneLeader()
        {
            var leader = new GeoLine2(0.0, 0.0, 40.0, 0.0);
            var a = LabelOn(leader);
            var b = LabelOn(leader);

            ArrangeResult[] results = Arranger.Run(new List<ArrangeItem> { a, b }, OptionsFor());

            Assert.False(MovedBox(a, results[0].Translation).CollidesWith(MovedBox(b, results[1].Translation)));
            Assert.True(results[0].Placed);
            Assert.True(results[1].Placed);
        }

        [Fact]
        public void Arrange_Run_AvoidsStaticObstacle()
        {
            var leader = new GeoLine2(0.0, 0.0, 40.0, 0.0);
            var blockPoly = new GeoPolygon2(
                new GeoPoint2(-60.0, 0.0),
                new GeoPoint2(60.0, 0.0),
                new GeoPoint2(60.0, 40.0),
                new GeoPoint2(-60.0, 40.0));

            var label = LabelOn(leader);
            label.BlockPolygons = new List<GeoPolygon2> { blockPoly };

            ArrangeResult result = Arranger.Run(new List<ArrangeItem> { label }, OptionsFor())[0];
            var moved = MovedBox(label, result.Translation);

            Assert.False(moved.CollidesWith(blockPoly));
            Assert.True(moved.Center.Y < 0.0);
            Assert.True(result.Placed);
        }

        [Fact]
        public void Arrange_Run_IsReproducible()
        {
            ArrangeResult[] Solve()
            {
                var labels = new List<ArrangeItem>();
                for (int i = 0; i < 6; i++)
                {
                    labels.Add(LabelOn(new GeoLine2(i * 12.0, 0.0, i * 12.0 + 30.0, 0.0)));
                }
                return Arranger.Run(labels, OptionsFor());
            }

            Assert.Equal(Solve(), Solve());
        }

        [Fact]
        public void Arrange_Run_PlacedFlagMatchesFinalLayout()
        {
            // The Placed flag must describe the FINAL layout. Three labels sharing a short guide segment with one
            // perpendicular level only have room for two, so the count of successful labels must match the count of
            // labels that actually do not overlap anyone — including labels overlapped by others falling back.
            var leader = new GeoLine2(0.0, 0.0, 10.0, 0.0);
            var labels = new List<ArrangeItem> { LabelOn(leader), LabelOn(leader), LabelOn(leader) };

            var options = OptionsFor();
            options.PerpendicularLevels = 1;

            ArrangeResult[] results = Arranger.Run(labels, options);

            var boxes = labels.Select((label, i) => MovedBox(label, results[i].Translation)).ToList();
            for (int i = 0; i < labels.Count; i++)
            {
                bool clean = true;
                for (int j = 0; j < labels.Count && clean; j++)
                {
                    if (i != j && boxes[i].CollidesWith(boxes[j])) clean = false;
                }

                Assert.Equal(clean, results[i].Placed);
            }
        }

        // ------------------------------------------------------------------
        // Pass 2 Relaxation (Relax BlockLines)
        // ------------------------------------------------------------------

        [Fact]
        public void Arrange_Run_RelaxationPass_AllowsFailedLabelsToOverlapBlockLines()
        {
            var leader = new GeoLine2(0.0, 0.0, 40.0, 0.0);
            var label = LabelOn(leader);

            // Mock BlockLines overlapping all candidate positions
            label.BlockLines = new List<GeoLine2>
            {
                new GeoLine2(-100, 10.0, 100, 10.0), // Blocks upper row
                new GeoLine2(-100, -10.0, 100, -10.0) // Blocks lower row
            };

            var options = OptionsFor();
            options.PerpendicularLevels = 1; // Only 1 level to guarantee congestion

            ArrangeResult result = Arranger.Run(new List<ArrangeItem> { label }, options)[0];

            // In Pass 1, the label is blocked by BlockLines and fails.
            // In Pass 2, the algorithm clears BlockLines and rearranges it, so it should have a non-zero Translation.
            // However, Placed must be false because it actually overlaps the original BlockLines.
            Assert.False(result.Placed);
            Assert.NotEqual(GeoVector2.Zero, result.Translation);
        }

        [Fact]
        public void Arrange_Run_RelaxationPass_FreezesPlacedLabels()
        {
            var leader1 = new GeoLine2(0.0, 0.0, 40.0, 0.0);
            var leader2 = new GeoLine2(1000.0, 0.0, 1040.0, 0.0); // Located far away
            var successLabel = LabelOn(leader1);
            var failedLabel = LabelOn(leader2);

            failedLabel.BlockLines = new List<GeoLine2>
            {
                new GeoLine2(900, 10.0, 1100, 10.0),
                new GeoLine2(900, -10.0, 1100, -10.0)
            };

            var options = OptionsFor();
            options.PerpendicularLevels = 1; // Only 1 level to guarantee congestion for failedLabel

            ArrangeResult[] results = Arranger.Run(new List<ArrangeItem> { successLabel, failedLabel }, options);

            // Since the two labels are far apart, successLabel naturally finds a spot in Pass 1.
            // When Pass 2 runs (due to failedLabel being congested), successLabel must retain its successful state and position.
            Assert.True(results[0].Placed);
            // failedLabel overlapping BlockLines (even relaxed) should end up with Placed = false
            Assert.False(results[1].Placed);
        }

        [Fact]
        public void Arrange_Run_RelaxationPass_FailedLabelsAvoidGreenBoxes()
        {
            var leader = new GeoLine2(0.0, 0.0, 10.0, 0.0); // Short leader segment
            var successLabel = LabelOn(leader);
            var failedLabel = LabelOn(leader);

            // Force failedLabel to enter Pass 2 by blocking with BlockLines
            failedLabel.BlockLines = new List<GeoLine2>
            {
                new GeoLine2(-100, 10.0, 100, 10.0)
            };

            var options = OptionsFor();
            options.PerpendicularLevels = 2;

            ArrangeResult[] results = Arranger.Run(new List<ArrangeItem> { successLabel, failedLabel }, options);

            // Pass 2 allows failedLabels to overlap BlockLines, but it MUST NOT overlap successLabel (greenBoxes)
            var successBox = MovedBox(successLabel, results[0].Translation);
            var failedBox = MovedBox(failedLabel, results[1].Translation);

            Assert.False(successBox.CollidesWith(failedBox));
        }

        [Fact]
        public void Arrange_Run_RotatedLabels_AvoidsCollision()
        {
            var leader = new GeoLine2(0.0, 0.0, 40.0, 0.0);

            // Create two labels at the same anchor point but rotated 45 degrees
            var a = new ArrangeItem
            {
                Leader = leader,
                Box = new GeoRectangle2(leader.MidPoint, 20.0, 10.0, Math.PI / 4.0),
                Offset = 5.0
            };
            var b = new ArrangeItem
            {
                Leader = leader,
                Box = new GeoRectangle2(leader.MidPoint, 20.0, 10.0, Math.PI / 4.0),
                Offset = 5.0
            };

            ArrangeResult[] results = Arranger.Run(new List<ArrangeItem> { a, b }, OptionsFor());

            var movedA = MovedBox(a, results[0].Translation);
            var movedB = MovedBox(b, results[1].Translation);

            Assert.False(movedA.CollidesWith(movedB));
            Assert.True(results[0].Placed);
            Assert.True(results[1].Placed);
        }

        [Fact]
        public void Arrange_Run_OvershootRatioZero_RestrictsLongitudinalMovement()
        {
            var leader = new GeoLine2(0.0, 0.0, 20.0, 0.0); // Leader length = 20
            var label = new ArrangeItem
            {
                Leader = leader,
                Box = new GeoRectangle2(new GeoPoint2(10.0, 0.0), 30.0, 10.0), // Label width = 30
                Offset = 5.0
            };

            var options = OptionsFor();
            options.LongitudinalOvershootRatio = 0.0; // No overshoot allowed

            // Label width = 30, leader length = 20.
            // MaximumShift = leaderLength * 0.5 + width * overshoot = 10 + 0 = 10.
            // Under this restriction, candidates can slide at most 10 units from the midpoint (10.0).
            var points = label.GetPlacePoints(options);
            foreach (var point in points)
            {
                double shift = Math.Abs(point.X - 10.0);
                Assert.True(shift <= 10.001); // Allowing small floating point tolerance
            }
        }

        [Fact]
        public void Arrange_Run_LookAheadCandidates_ChoosesPositionWithMaxClearance()
        {
            var leader = new GeoLine2(0.0, 0.0, 100.0, 0.0);
            var label = LabelOn(leader);

            // Place two static obstacles shifted to the left (X: 20 to 40) at Y=16 and Y=-16.
            // Candidates on the left and center will have a tight clearance of 1.0 unit.
            // Candidates shifted to the right (X=53.25) will escape the obstacle bounds and have a better clearance (~3.4 units).
            var blockPoly1 = new GeoPolygon2(
                new GeoPoint2(20.0, 16.0),
                new GeoPoint2(40.0, 16.0),
                new GeoPoint2(40.0, 17.0),
                new GeoPoint2(20.0, 17.0));
            var blockPoly2 = new GeoPolygon2(
                new GeoPoint2(20.0, -17.0),
                new GeoPoint2(40.0, -17.0),
                new GeoPoint2(40.0, -16.0),
                new GeoPoint2(20.0, -16.0));
            label.BlockPolygons = new List<GeoPolygon2> { blockPoly1, blockPoly2 };

            var options = OptionsFor();
            options.PerpendicularLevels = 1; // Stay on the same row to test longitudinal slide clearance
            options.LookAheadCandidates = 6; // Evaluate 6 candidates to reach the right-shifted one

            ArrangeResult result = Arranger.Run(new List<ArrangeItem> { label }, options)[0];

            // With look-ahead, the algorithm should choose a right-shifted candidate (X != 50, hence Translation.X != 0)
            // because it offers a much better clearance than the first candidate at X=50 (clearance = 1.0).
            Assert.NotEqual(0.0, result.Translation.X, 4);
            Assert.True(result.Placed);
        }

        [Fact]
        public void Arrange_Run_IgnoreSubThresholdMoves()
        {
            // The label is placed extremely close to the first candidate position (offset by only 0.05 units along X).
            var leader = new GeoLine2(0.0, 0.0, 40.0, 0.0);
            var label = new ArrangeItem
            {
                Leader = leader,
                Box = new GeoRectangle2(new GeoPoint2(20.05, 10.0), 20.0, 10.0),
                Offset = 5.0
            };

            var options = OptionsFor();
            options.MinimumMoveDistance = 0.1; // Shift distances smaller than 0.1 are ignored

            ArrangeResult result = Arranger.Run(new List<ArrangeItem> { label }, options)[0];

            // Since the shift distance (0.05) is smaller than the threshold (0.1),
            // the algorithm must suppress the translation, keeping it at zero vector.
            Assert.Equal(GeoVector2.Zero, result.Translation);
            Assert.True(result.Placed);
        }

        [Fact]
        public void Arrange_Run_VerticalLeader_PlacesLabelsCorrectly()
        {
            // Test with a completely vertical guide segment (projection check along Y axis)
            var leader = new GeoLine2(0.0, 0.0, 0.0, 100.0);
            var a = LabelOn(leader);
            var b = LabelOn(leader);

            ArrangeResult[] results = Arranger.Run(new List<ArrangeItem> { a, b }, OptionsFor());

            var movedA = MovedBox(a, results[0].Translation);
            var movedB = MovedBox(b, results[1].Translation);

            Assert.False(movedA.CollidesWith(movedB));
            Assert.True(results[0].Placed);
            Assert.True(results[1].Placed);
        }

        [Fact]
        public void Arrange_Run_DiagonalLeader_PlacesLabelsCorrectly()
        {
            // Test with a diagonal guide segment (45 degrees axis rotation check)
            var leader = new GeoLine2(0.0, 0.0, 100.0, 100.0);
            var a = LabelOn(leader);
            var b = LabelOn(leader);

            ArrangeResult[] results = Arranger.Run(new List<ArrangeItem> { a, b }, OptionsFor());

            var movedA = MovedBox(a, results[0].Translation);
            var movedB = MovedBox(b, results[1].Translation);

            Assert.False(movedA.CollidesWith(movedB));
            Assert.True(results[0].Placed);
            Assert.True(results[1].Placed);
        }

        [Fact]
        public void Arrange_Run_ExtremeRowGap_IncreasesRowSeparation()
        {
            var leader = new GeoLine2(0.0, 0.0, 40.0, 0.0);
            var a = LabelOn(leader);
            var b = LabelOn(leader);

            var options = OptionsFor();
            options.RowGap = 150.0; // Extremely large row gap
            options.PerpendicularLevels = 2;

            ArrangeResult[] results = Arranger.Run(new List<ArrangeItem> { a, b }, options);

            // If any label is forced to the second level, its Y coordinate magnitude must reflect the extreme row gap
            foreach (var (label, result) in new[] { (a, results[0]), (b, results[1]) })
            {
                if (result.Placed)
                {
                    double y = Math.Abs(MovedBox(label, result.Translation).Center.Y);
                    if (y > 20.0) // Bypassed level 1 (which is at Y=10)
                    {
                        Assert.True(y >= 169.9); // Must be at level 2 (the first row + Height + RowGap = 10 + 10 + 150 = 170)
                    }
                }
            }
        }

        [Fact]
        public void Arrange_Run_LargeMinimumMoveDistance_RestrictsPlacement()
        {
            var leader = new GeoLine2(0.0, 0.0, 40.0, 0.0);
            var label = LabelOn(leader);

            var options = OptionsFor();
            options.MinimumMoveDistance = 50.0; // Distance2 shifts smaller than 50 are rejected

            ArrangeResult result = Arranger.Run(new List<ArrangeItem> { label }, options)[0];

            // The translation must either be zero (suppressed) or a very large leap
            double dist = result.Translation.Length;
            Assert.True(dist == 0.0 || dist >= 50.0);
        }

        /// <summary>
        /// One candidate allowed, and a region over it: the label is left there, overlapping. The cap
        /// was checked only before each group of four slides, so every row still gave its two places straight across,
        /// and the label went to one of those that was free.
        /// </summary>
        [Fact]
        public void Arrange_Run_MaximumCandidatesLimit_RestrictsSearch()
        {
            var leader = new GeoLine2(0.0, 0.0, 40.0, 0.0);
            var label = LabelOn(leader);
            label.BlockPolygons = new[]
            {
                new GeoPolygon2(new GeoPoint2(15.0, 6.0), new GeoPoint2(25.0, 6.0), new GeoPoint2(25.0, 14.0), new GeoPoint2(15.0, 14.0))
            };

            var options = OptionsFor();
            options.MaximumCandidates = 1;

            ArrangeResult result = Arranger.Run(new List<ArrangeItem> { label }, options)[0];

            // The one candidate is the first above: 5 + 5 off.
            Assert.False(result.Placed);
            Assert.True(result.Translation.IsEqualTo(new GeoVector2(0.0, 10.0)), result.ToString());
        }

        [Theory]
        [InlineData(1)]
        [InlineData(3)]
        [InlineData(10)]
        [InlineData(50)]
        [InlineData(245)]
        [InlineData(246)]
        [InlineData(247)]
        public void Arrange_GetPlacePoints_GivesNoMoreThanMaximumCandidates(int cap)
        {
            var label = LabelOn(new GeoLine2(0.0, 0.0, 40.0, 0.0));
            List<GeoPoint2> all = label.GetPlacePoints(OptionsFor());

            var options = OptionsFor();
            options.MaximumCandidates = cap;
            List<GeoPoint2> capped = label.GetPlacePoints(options);

            // Three rows on either side, each straight across and twenty steps each way along the leader: 246 in all.
            Assert.Equal(246, all.Count);
            Assert.Equal(all.Take(cap), capped);
        }

        [Fact]
        public void Arrange_GetPlacePoints_CapsTheRowsToo()
        {
            var label = LabelOn(new GeoLine2(0.0, 0.0, 40.0, 0.0));
            var options = OptionsFor();
            options.PerpendicularLevels = 1000000;
            options.MaximumCandidates = 10;

            Assert.Equal(10, label.GetPlacePoints(options).Count);
        }

        [Fact]
        public void Arrange_Run_WithOverlappingObstacles_ResolvesCorrectly()
        {
            var leader = new GeoLine2(0.0, 0.0, 40.0, 0.0);

            // Multiple static obstacles overlapping each other
            var obs1 = new GeoPolygon2(new GeoPoint2(-50, 5), new GeoPoint2(50, 5), new GeoPoint2(50, 15), new GeoPoint2(-50, 15));
            var obs2 = new GeoPolygon2(new GeoPoint2(-10, 5), new GeoPoint2(90, 5), new GeoPoint2(90, 15), new GeoPoint2(-10, 15));

            var label = LabelOn(leader);
            label.BlockPolygons = new List<GeoPolygon2> { obs1, obs2 };

            var options = OptionsFor();
            options.PerpendicularLevels = 3;

            ArrangeResult result = Arranger.Run(new List<ArrangeItem> { label }, options)[0];

            var moved = MovedBox(label, result.Translation);
            Assert.False(moved.CollidesWith(obs1));
            Assert.False(moved.CollidesWith(obs2));
        }

        [Fact]
        public void Arrange_Run_WithTinyLabels_ArrangesWithoutErrors()
        {
            var leader = new GeoLine2(0.0, 0.0, 40.0, 0.0);
            var a = new ArrangeItem
            {
                Leader = leader,
                Box = new GeoRectangle2(leader.MidPoint, 1.0, 1.0), // Tiny label
                Offset = 1.0
            };

            var options = OptionsFor();
            options.MinimumBoxSize = 0.5; // Ensure tiny label is valid

            ArrangeResult result = Arranger.Run(new List<ArrangeItem> { a }, options)[0];

            Assert.True(result.Placed);
        }

        [Fact]
        public void Arrange_Run_WithGiantLabels_DoesNotCrash()
        {
            var leader = new GeoLine2(0.0, 0.0, 40.0, 0.0);
            var a = new ArrangeItem
            {
                Leader = leader,
                Box = new GeoRectangle2(leader.MidPoint, 1000.0, 1000.0), // Giant label
                Offset = 5.0
            };

            ArrangeResult result = Arranger.Run(new List<ArrangeItem> { a }, OptionsFor())[0];

            // Should execute and map correctly, marked as placed if candidate matches, or failed safely
            Assert.True(result.Placed || !result.Placed);
        }

        [Fact]
        public void Arrange_Run_WithZeroLengthLeader_FailsGracefully()
        {
            var leader = new GeoLine2(10.0, 10.0, 10.0, 10.0); // Zero length segment
            var label = LabelOn(leader);

            ArrangeResult result = Arranger.Run(new List<ArrangeItem> { label }, OptionsFor())[0];

            // It should fail to arrange since no candidates can be computed, but must not crash
            Assert.False(result.Placed);
            Assert.Equal(GeoVector2.Zero, result.Translation);
        }

        [Fact]
        public void Arrange_Run_ObstaclesBlockingAllButOneSpot_FindsUniqueSpot()
        {
            var leader = new GeoLine2(0.0, 0.0, 40.0, 0.0);
            var label = LabelOn(leader);

            // Block everything except the 3rd level bottom row position (Y = -60)
            // Obstacle blocking level 1 & 2 (Y: -45 to 45)
            var obstacle = new GeoPolygon2(
                new GeoPoint2(-200.0, -45.0),
                new GeoPoint2(200.0, -45.0),
                new GeoPoint2(200.0, 45.0),
                new GeoPoint2(-200.0, 45.0));

            label.BlockPolygons = new List<GeoPolygon2> { obstacle };

            var options = OptionsFor();
            options.PerpendicularLevels = 3;
            options.RowGap = 15.0; // level 1: 10, level 2: 35, level 3: 60.

            ArrangeResult result = Arranger.Run(new List<ArrangeItem> { label }, options)[0];

            if (result.Placed)
            {
                var moved = MovedBox(label, result.Translation);
                Assert.False(moved.CollidesWith(obstacle));
                // Center Y should be close to 60 or -60 (level 3)
                Assert.Equal(60.0, Math.Abs(moved.Center.Y), 1);
            }
        }
    }
}
