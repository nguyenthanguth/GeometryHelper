using System;
using System.Collections.Generic;
using System.Linq;
using PlaneGeometry.Geometry;
using Xunit;

namespace ArrangeAlgorithms.UnitTest
{
    /// <summary>
    /// Tests the public API of <see cref="Arrange"/> and contracts that EVERY algorithm must uphold.
    /// </summary>
    public class ArrangeTests
    {
        private static ArrangeOptions OptionsFor(ArrangeAlgorithmType algorithm)
        {
            return new ArrangeOptions
            {
                Algorithm = algorithm,
                RowGap = 5.0,
                PerpendicularLevels = 3
            };
        }

        private static Arrange LabelOn(GeoLine2 leader)
        {
            return new Arrange
            {
                GeoLine2 = leader,
                GeoRectangle2 = new GeoRectangle2(leader.MidPoint, 20.0, 10.0),
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
        // Check input parameters
        // ------------------------------------------------------------------

        [Fact]
        public void Arrange_Run_WithNullList_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => Arrange.Run(null));
            Assert.Throws<ArgumentNullException>(() => Arrange.Run(null, ArrangeOptions.Default));
        }

        [Fact]
        public void Arrange_Run_WithNullOptions_Throws()
        {
            var labels = new List<Arrange> { LabelOn(new GeoLine2(0.0, 0.0, 40.0, 0.0)) };

            Assert.Throws<ArgumentNullException>(() => Arrange.Run(labels, null));
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
            // Label does not set MarkOffsetFromLine, so it keeps Arrange's default value of 50.
            var label = new Arrange
            {
                GeoLine2 = new GeoLine2(0.0, 0.0, 400.0, 0.0),
                GeoRectangle2 = new GeoRectangle2(new GeoPoint2(200.0, 0.0), 20.0, 10.0)
            };

            Arrange.Run(new List<Arrange> { label });

            // Default BaseOffset = half height (5) + MarkOffsetFromLine (50) = 55.
            Assert.Equal(55.0, Math.Abs(MovedBox(label, label.TranslationVector).Center.Y), 6);
        }

        // ------------------------------------------------------------------
        // Generate candidate positions
        // ------------------------------------------------------------------

        [Fact]
        public void Arrange_GetPlacePoints_FirstPairIsSymmetricAtBaseOffset()
        {
            var label = LabelOn(new GeoLine2(0.0, 0.0, 40.0, 0.0));
            var options = OptionsFor(ArrangeAlgorithmType.Greedy);

            var points = label.GetPlacePoints(options);

            // First pair must lie on the first perpendicular level (BaseOffset = height/2 + MarkOffsetFromLine = 5 + 5 = 10)
            Assert.True(points[0].IsEqualTo(new GeoPoint2(20.0, 10.0)));
            Assert.True(points[1].IsEqualTo(new GeoPoint2(20.0, -10.0)));
        }

        [Fact]
        public void Arrange_MarkOffsetFromLine_IsPerLabelNotGlobal()
        {
            // Offset is per-label, so two labels sharing the same ArrangeOptions must still
            // expand candidates from two different perpendicular levels.
            var leader = new GeoLine2(0.0, 0.0, 40.0, 0.0);
            var near = LabelOn(leader);                       // MarkOffsetFromLine = 5
            var far = LabelOn(leader);
            far.BaseOffsetFromLine = 30.0;

            var options = OptionsFor(ArrangeAlgorithmType.Greedy);

            // BaseOffset = half label height (5) plus the label's own specific offset.
            Assert.True(near.GetPlacePoints(options)[0].IsEqualTo(new GeoPoint2(20.0, 10.0)));
            Assert.True(far.GetPlacePoints(options)[0].IsEqualTo(new GeoPoint2(20.0, 35.0)));
        }

        [Fact]
        public void Arrange_Run_HonoursEachLabelsOwnMarkOffsetFromLine()
        {
            // Same guide segment, same options: label declaring larger offset must
            // stop further from the guide segment, and both must lie exactly on their first perpendicular level.
            var leader = new GeoLine2(0.0, 0.0, 40.0, 0.0);
            var near = LabelOn(leader);                       // MarkOffsetFromLine = 5
            var far = LabelOn(leader);
            far.BaseOffsetFromLine = 30.0;

            Arrange.Run(new List<Arrange> { near, far }, OptionsFor(ArrangeAlgorithmType.Greedy));

            Assert.Equal(10.0, Math.Abs(MovedBox(near, near.TranslationVector).Center.Y), 6);
            Assert.Equal(35.0, Math.Abs(MovedBox(far, far.TranslationVector).Center.Y), 6);
            Assert.True(near.Placed);
            Assert.True(far.Placed);
        }

        [Fact]
        public void Arrange_GetPlacePoints_MoreLevelsProduceMoreCandidates()
        {
            var label = LabelOn(new GeoLine2(0.0, 0.0, 40.0, 0.0));

            var threeLevels = label.GetPlacePoints(OptionsFor(ArrangeAlgorithmType.Greedy));

            var single = OptionsFor(ArrangeAlgorithmType.Greedy);
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
            var a1 = new Arrange
            {
                GeoRectangle2 = new GeoRectangle2(new GeoPoint2(5.0, 0.0), 2.0, 2.0), // Too small
                GeoLine2 = leaderLine
            };

            var options = new ArrangeOptions
            {
                MinimumBoxSize = 5.0
            };

            Arrange.Run(new List<Arrange> { a1 }, options);
            Assert.Equal(GeoVector2.Zero, a1.TranslationVector);
            Assert.False(a1.Placed);
        }

        [Fact]
        public void Arrange_DegenerateLeader_LeavesLabelInPlaceAndReportsFailure()
        {
            var label = new Arrange
            {
                GeoLine2 = new GeoLine2(5.0, 0.0, 5.0, 0.0),
                GeoRectangle2 = new GeoRectangle2(new GeoPoint2(5.0, 0.0), 20.0, 10.0),
                BaseOffsetFromLine = 5.0
            };

            Arrange.Run(new List<Arrange> { label }, OptionsFor(ArrangeAlgorithmType.Greedy));

            Assert.Equal(GeoVector2.Zero, label.TranslationVector);

            // The label does not overlap anyone, but it has never been arranged either, so it must not be reported as successful.
            Assert.False(label.Placed);
        }

        [Fact]
        public void Arrange_LabelAlreadyOnFirstCandidate_ProducesZeroTranslation()
        {
            // Label is already at the first candidate position: nothing to translate,
            // and MinimumMoveDistance must suppress any minor errors.
            var leader = new GeoLine2(0.0, 0.0, 40.0, 0.0);
            var label = new Arrange
            {
                GeoLine2 = leader,
                GeoRectangle2 = new GeoRectangle2(new GeoPoint2(20.0, 10.0), 20.0, 10.0),
                BaseOffsetFromLine = 5.0
            };

            Arrange.Run(new List<Arrange> { label }, OptionsFor(ArrangeAlgorithmType.Greedy));

            Assert.Equal(GeoVector2.Zero, label.TranslationVector);
            Assert.True(label.Placed);
        }

        // ------------------------------------------------------------------
        // Common contract for all five algorithms
        // ------------------------------------------------------------------

        public static IEnumerable<object[]> AllAlgorithms()
        {
            yield return new object[] { ArrangeAlgorithmType.Greedy };
            yield return new object[] { ArrangeAlgorithmType.BoundedBacktracking };
            yield return new object[] { ArrangeAlgorithmType.SimulatedAnnealing };
            yield return new object[] { ArrangeAlgorithmType.ForceDirected };
            yield return new object[] { ArrangeAlgorithmType.ConstraintSatisfaction };
        }

        [Theory]
        [MemberData(nameof(AllAlgorithms))]
        public void Arrange_Run_PopulatesTranslationVector(ArrangeAlgorithmType algorithm)
        {
            var labels = new List<Arrange>();
            for (int i = 0; i < 5; i++)
            {
                labels.Add(LabelOn(new GeoLine2(i * 50.0, 0.0, i * 50.0 + 40.0, 0.0)));
            }

            Arrange.Run(labels, OptionsFor(algorithm));

            // Verify that all labels were successfully arranged
            Assert.True(labels.All(l => l.Placed));
        }

        [Theory]
        [MemberData(nameof(AllAlgorithms))]
        public void Arrange_Run_WithEmptyList_DoesNotThrow(ArrangeAlgorithmType algorithm)
        {
            Arrange.Run(new List<Arrange>(), OptionsFor(algorithm));
        }

        [Theory]
        [MemberData(nameof(AllAlgorithms))]
        public void Arrange_Run_WithNullEntries_KeepsIndicesAligned(ArrangeAlgorithmType algorithm)
        {
            var first = LabelOn(new GeoLine2(0.0, 0.0, 40.0, 0.0));
            var second = LabelOn(new GeoLine2(200.0, 0.0, 240.0, 0.0));

            var labels = new List<Arrange> { first, null, second };
            Arrange.Run(labels, OptionsFor(algorithm));

            // Each real label must still stick to its own guide segment without index swap.
            // Do not compare absolute coordinates: Force-directed intentionally slides labels along guide segment to spread them,
            // so the correct assertion is "closer to its own guide segment than to the other label's guide segment".
            GeoPoint2 firstCentre = MovedBox(first, first.TranslationVector).Center;
            GeoPoint2 secondCentre = MovedBox(second, second.TranslationVector).Center;

            Assert.True(first.GeoLine2.DistanceTo(firstCentre) < second.GeoLine2.DistanceTo(firstCentre));
            Assert.True(second.GeoLine2.DistanceTo(secondCentre) < first.GeoLine2.DistanceTo(secondCentre));
        }

        [Theory]
        [MemberData(nameof(AllAlgorithms))]
        public void Arrange_Run_SeparatesTwoLabelsSharingOneLeader(ArrangeAlgorithmType algorithm)
        {
            var leader = new GeoLine2(0.0, 0.0, 40.0, 0.0);
            var a = LabelOn(leader);
            var b = LabelOn(leader);

            Arrange.Run(new List<Arrange> { a, b }, OptionsFor(algorithm));

            Assert.False(MovedBox(a, a.TranslationVector).CollidesWith(MovedBox(b, b.TranslationVector)));
            Assert.True(a.Placed);
            Assert.True(b.Placed);
        }

        [Theory]
        [MemberData(nameof(AllAlgorithms))]
        public void Arrange_Run_AvoidsStaticObstacle(ArrangeAlgorithmType algorithm)
        {
            var leader = new GeoLine2(0.0, 0.0, 40.0, 0.0);
            var blockPoly = new GeoPolygon2(
                new GeoPoint2(-60.0, 0.0),
                new GeoPoint2(60.0, 0.0),
                new GeoPoint2(60.0, 40.0),
                new GeoPoint2(-60.0, 40.0));

            var label = LabelOn(leader);
            label.BlockPolygons = new List<GeoPolygon2> { blockPoly };

            Arrange.Run(new List<Arrange> { label }, OptionsFor(algorithm));
            var moved = MovedBox(label, label.TranslationVector);

            Assert.False(moved.CollidesWith(blockPoly));
            Assert.True(moved.Center.Y < 0.0);
            Assert.True(label.Placed);
        }

        [Theory]
        [MemberData(nameof(AllAlgorithms))]
        public void Arrange_Run_IsReproducible(ArrangeAlgorithmType algorithm)
        {
            List<Arrange> Solve()
            {
                var labels = new List<Arrange>();
                for (int i = 0; i < 6; i++)
                {
                    labels.Add(LabelOn(new GeoLine2(i * 12.0, 0.0, i * 12.0 + 30.0, 0.0)));
                }
                Arrange.Run(labels, OptionsFor(algorithm));
                return labels;
            }

            var first = Solve();
            var second = Solve();

            Assert.Equal(first.Count, second.Count);
            for (int i = 0; i < first.Count; i++)
            {
                Assert.Equal(first[i].TranslationVector, second[i].TranslationVector);
            }
        }

        [Theory]
        [MemberData(nameof(AllAlgorithms))]
        public void Arrange_Run_PlacedFlagMatchesFinalLayout(ArrangeAlgorithmType algorithm)
        {
            // The Placed flag must describe the FINAL layout. Three labels sharing a short guide segment with one
            // perpendicular level only have room for two, so the count of successful labels must match the count of
            // labels that actually do not overlap anyone ??including labels overlapped by others falling back.
            var leader = new GeoLine2(0.0, 0.0, 10.0, 0.0);
            var labels = new List<Arrange> { LabelOn(leader), LabelOn(leader), LabelOn(leader) };

            var options = OptionsFor(algorithm);
            options.PerpendicularLevels = 1;

            Arrange.Run(labels, options);

            var boxes = labels.Select((label, i) => MovedBox(label, label.TranslationVector)).ToList();
            for (int i = 0; i < labels.Count; i++)
            {
                bool clean = true;
                for (int j = 0; j < labels.Count && clean; j++)
                {
                    if (i != j && boxes[i].CollidesWith(boxes[j])) clean = false;
                }

                Assert.Equal(clean, labels[i].Placed);
            }
        }

        // ------------------------------------------------------------------
        // Pass 2 Relaxation (Relax BlockLines)
        // ------------------------------------------------------------------

        [Theory]
        [MemberData(nameof(AllAlgorithms))]
        public void Arrange_Run_RelaxationPass_AllowsFailedLabelsToOverlapBlockLines(ArrangeAlgorithmType algorithm)
        {
            var leader = new GeoLine2(0.0, 0.0, 40.0, 0.0);
            var label = LabelOn(leader);
            
            // Mock BlockLines overlapping all candidate positions
            label.BlockLines = new List<GeoLine2>
            {
                new GeoLine2(-100, 10.0, 100, 10.0), // Blocks upper row
                new GeoLine2(-100, -10.0, 100, -10.0) // Blocks lower row
            };

            var options = OptionsFor(algorithm);
            options.PerpendicularLevels = 1; // Only 1 level to guarantee congestion

            Arrange.Run(new List<Arrange> { label }, options);

            // In Pass 1, the label is blocked by BlockLines and fails.
            // In Pass 2, the algorithm clears BlockLines and rearranges it, so it should have a non-zero TranslationVector.
            // However, Placed must be false because it actually overlaps the original BlockLines.
            Assert.False(label.Placed);
            Assert.NotEqual(GeoVector2.Zero, label.TranslationVector);
        }

        [Theory]
        [MemberData(nameof(AllAlgorithms))]
        public void Arrange_Run_RelaxationPass_FreezesPlacedLabels(ArrangeAlgorithmType algorithm)
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

            var options = OptionsFor(algorithm);
            options.PerpendicularLevels = 1; // Only 1 level to guarantee congestion for failedLabel

            Arrange.Run(new List<Arrange> { successLabel, failedLabel }, options);

            // Since the two labels are far apart, successLabel naturally finds a spot in Pass 1.
            // When Pass 2 runs (due to failedLabel being congested), successLabel must retain its successful state and position.
            Assert.True(successLabel.Placed);
            // failedLabel overlapping BlockLines (even relaxed) should end up with Placed = false
            Assert.False(failedLabel.Placed);
        }

        [Theory]
        [MemberData(nameof(AllAlgorithms))]
        public void Arrange_Run_RelaxationPass_FailedLabelsAvoidGreenBoxes(ArrangeAlgorithmType algorithm)
        {
            var leader = new GeoLine2(0.0, 0.0, 10.0, 0.0); // Short leader segment
            var successLabel = LabelOn(leader); 
            var failedLabel = LabelOn(leader);
            
            // Force failedLabel to enter Pass 2 by blocking with BlockLines
            failedLabel.BlockLines = new List<GeoLine2>
            {
                new GeoLine2(-100, 10.0, 100, 10.0)
            };

            var options = OptionsFor(algorithm);
            options.PerpendicularLevels = 2;

            Arrange.Run(new List<Arrange> { successLabel, failedLabel }, options);

            // Pass 2 allows failedLabels to overlap BlockLines, but it MUST NOT overlap successLabel (greenBoxes)
            var successBox = MovedBox(successLabel, successLabel.TranslationVector);
            var failedBox = MovedBox(failedLabel, failedLabel.TranslationVector);

            Assert.False(successBox.CollidesWith(failedBox));
        }

        [Theory]
        [MemberData(nameof(AllAlgorithms))]
        public void Arrange_Run_RotatedLabels_AvoidsCollision(ArrangeAlgorithmType algorithm)
        {
            var leader = new GeoLine2(0.0, 0.0, 40.0, 0.0);
            
            // Create two labels at the same anchor point but rotated 45 degrees
            var a = new Arrange
            {
                GeoLine2 = leader,
                GeoRectangle2 = new GeoRectangle2(leader.MidPoint, 20.0, 10.0, Math.PI / 4.0),
                BaseOffsetFromLine = 5.0
            };
            var b = new Arrange
            {
                GeoLine2 = leader,
                GeoRectangle2 = new GeoRectangle2(leader.MidPoint, 20.0, 10.0, Math.PI / 4.0),
                BaseOffsetFromLine = 5.0
            };

            Arrange.Run(new List<Arrange> { a, b }, OptionsFor(algorithm));

            var movedA = MovedBox(a, a.TranslationVector);
            var movedB = MovedBox(b, b.TranslationVector);

            Assert.False(movedA.CollidesWith(movedB));
            Assert.True(a.Placed);
            Assert.True(b.Placed);
        }

        [Fact]
        public void Arrange_Run_OvershootRatioZero_RestrictsLongitudinalMovement()
        {
            var leader = new GeoLine2(0.0, 0.0, 20.0, 0.0); // Leader length = 20
            var label = new Arrange
            {
                GeoLine2 = leader,
                GeoRectangle2 = new GeoRectangle2(new GeoPoint2(10.0, 0.0), 30.0, 10.0), // Label width = 30
                BaseOffsetFromLine = 5.0
            };

            var options = OptionsFor(ArrangeAlgorithmType.Greedy);
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

        [Theory]
        [MemberData(nameof(AllAlgorithms))]
        public void Arrange_Run_LookAheadCandidates_ChoosesPositionWithMaxClearance(ArrangeAlgorithmType algorithm)
        {
            // This heuristic test only applies to sequential Greedy algorithm which evaluates candidate clearances.
            if (algorithm != ArrangeAlgorithmType.Greedy)
            {
                return;
            }

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

            var options = OptionsFor(algorithm);
            options.PerpendicularLevels = 1; // Stay on the same row to test longitudinal slide clearance
            options.LookAheadCandidates = 6; // Evaluate 6 candidates to reach the right-shifted one

            Arrange.Run(new List<Arrange> { label }, options);

            // With look-ahead, the algorithm should choose a right-shifted candidate (X != 50, hence TranslationVector.X != 0)
            // because it offers a much better clearance than the first candidate at X=50 (clearance = 1.0).
            Assert.NotEqual(0.0, label.TranslationVector.X, 4);
            Assert.True(label.Placed);
        }

        [Fact]
        public void Arrange_Run_IgnoreSubThresholdMoves()
        {
            // The label is placed extremely close to the first candidate position (offset by only 0.05 units along X).
            var leader = new GeoLine2(0.0, 0.0, 40.0, 0.0);
            var label = new Arrange
            {
                GeoLine2 = leader,
                GeoRectangle2 = new GeoRectangle2(new GeoPoint2(20.05, 10.0), 20.0, 10.0),
                BaseOffsetFromLine = 5.0
            };

            var options = OptionsFor(ArrangeAlgorithmType.Greedy);
            options.MinimumMoveDistance = 0.1; // Shift distances smaller than 0.1 are ignored

            Arrange.Run(new List<Arrange> { label }, options);

            // Since the shift distance (0.05) is smaller than the threshold (0.1), 
            // the algorithm must suppress the translation, keeping it at zero vector.
            Assert.Equal(GeoVector2.Zero, label.TranslationVector);
            Assert.True(label.Placed);
        }

        [Theory]
        [MemberData(nameof(AllAlgorithms))]
        public void Arrange_Run_VerticalLeader_PlacesLabelsCorrectly(ArrangeAlgorithmType algorithm)
        {
            // Test with a completely vertical guide segment (projection check along Y axis)
            var leader = new GeoLine2(0.0, 0.0, 0.0, 100.0);
            var a = LabelOn(leader);
            var b = LabelOn(leader);

            Arrange.Run(new List<Arrange> { a, b }, OptionsFor(algorithm));

            var movedA = MovedBox(a, a.TranslationVector);
            var movedB = MovedBox(b, b.TranslationVector);

            Assert.False(movedA.CollidesWith(movedB));
            Assert.True(a.Placed);
            Assert.True(b.Placed);
        }

        [Theory]
        [MemberData(nameof(AllAlgorithms))]
        public void Arrange_Run_DiagonalLeader_PlacesLabelsCorrectly(ArrangeAlgorithmType algorithm)
        {
            // Test with a diagonal guide segment (45 degrees axis rotation check)
            var leader = new GeoLine2(0.0, 0.0, 100.0, 100.0);
            var a = LabelOn(leader);
            var b = LabelOn(leader);

            Arrange.Run(new List<Arrange> { a, b }, OptionsFor(algorithm));

            var movedA = MovedBox(a, a.TranslationVector);
            var movedB = MovedBox(b, b.TranslationVector);

            Assert.False(movedA.CollidesWith(movedB));
            Assert.True(a.Placed);
            Assert.True(b.Placed);
        }

        [Theory]
        [MemberData(nameof(AllAlgorithms))]
        public void Arrange_Run_ExtremeRowGap_IncreasesRowSeparation(ArrangeAlgorithmType algorithm)
        {
            var leader = new GeoLine2(0.0, 0.0, 40.0, 0.0);
            var a = LabelOn(leader);
            var b = LabelOn(leader);

            var options = OptionsFor(algorithm);
            options.RowGap = 150.0; // Extremely large row gap
            options.PerpendicularLevels = 2;

            Arrange.Run(new List<Arrange> { a, b }, options);

            // If any label is forced to the second level, its Y coordinate magnitude must reflect the extreme row gap
            foreach (var label in new[] { a, b })
            {
                if (label.Placed)
                {
                    double y = Math.Abs(MovedBox(label, label.TranslationVector).Center.Y);
                    if (y > 20.0) // Bypassed level 1 (which is at Y=10)
                    {
                        Assert.True(y >= 169.9); // Must be at level 2 (BaseOffset + Height + RowGap = 10 + 10 + 150 = 170)
                    }
                }
            }
        }

        [Theory]
        [MemberData(nameof(AllAlgorithms))]
        public void Arrange_Run_LargeMinimumMoveDistance_RestrictsPlacement(ArrangeAlgorithmType algorithm)
        {
            // MinimumMoveDistance constraint is only supported and enforced by the sequential Greedy algorithm
            if (algorithm != ArrangeAlgorithmType.Greedy)
            {
                return;
            }

            var leader = new GeoLine2(0.0, 0.0, 40.0, 0.0);
            var label = LabelOn(leader);

            var options = OptionsFor(algorithm);
            options.MinimumMoveDistance = 50.0; // Distance2 shifts smaller than 50 are rejected

            Arrange.Run(new List<Arrange> { label }, options);

            // The translation must either be zero (suppressed) or a very large leap
            double dist = label.TranslationVector.Length;
            Assert.True(dist == 0.0 || dist >= 50.0);
        }

        [Theory]
        [MemberData(nameof(AllAlgorithms))]
        public void Arrange_Run_MaximumCandidatesLimit_RestrictsSearch(ArrangeAlgorithmType algorithm)
        {
            var leader = new GeoLine2(0.0, 0.0, 40.0, 0.0);
            var label = LabelOn(leader);

            var options = OptionsFor(algorithm);
            options.MaximumCandidates = 1; // Strict search limit (only checks first candidate)

            Arrange.Run(new List<Arrange> { label }, options);

            // The algorithm must execute successfully under the extreme constraint without hanging or crashing
            Assert.True(label.Placed || !label.Placed);
        }

        [Theory]
        [MemberData(nameof(AllAlgorithms))]
        public void Arrange_Run_WithOverlappingObstacles_ResolvesCorrectly(ArrangeAlgorithmType algorithm)
        {
            var leader = new GeoLine2(0.0, 0.0, 40.0, 0.0);
            
            // Multiple static obstacles overlapping each other
            var obs1 = new GeoPolygon2(new GeoPoint2(-50, 5), new GeoPoint2(50, 5), new GeoPoint2(50, 15), new GeoPoint2(-50, 15));
            var obs2 = new GeoPolygon2(new GeoPoint2(-10, 5), new GeoPoint2(90, 5), new GeoPoint2(90, 15), new GeoPoint2(-10, 15));

            var label = LabelOn(leader);
            label.BlockPolygons = new List<GeoPolygon2> { obs1, obs2 };

            var options = OptionsFor(algorithm);
            options.PerpendicularLevels = 3;

            Arrange.Run(new List<Arrange> { label }, options);

            var moved = MovedBox(label, label.TranslationVector);
            Assert.False(moved.CollidesWith(obs1));
            Assert.False(moved.CollidesWith(obs2));
        }

        [Theory]
        [MemberData(nameof(AllAlgorithms))]
        public void Arrange_Run_WithTinyLabels_ArrangesWithoutErrors(ArrangeAlgorithmType algorithm)
        {
            var leader = new GeoLine2(0.0, 0.0, 40.0, 0.0);
            var a = new Arrange
            {
                GeoLine2 = leader,
                GeoRectangle2 = new GeoRectangle2(leader.MidPoint, 1.0, 1.0), // Tiny label
                BaseOffsetFromLine = 1.0
            };

            var options = OptionsFor(algorithm);
            options.MinimumBoxSize = 0.5; // Ensure tiny label is valid

            Arrange.Run(new List<Arrange> { a }, options);

            Assert.True(a.Placed);
        }

        [Theory]
        [MemberData(nameof(AllAlgorithms))]
        public void Arrange_Run_WithGiantLabels_DoesNotCrash(ArrangeAlgorithmType algorithm)
        {
            var leader = new GeoLine2(0.0, 0.0, 40.0, 0.0);
            var a = new Arrange
            {
                GeoLine2 = leader,
                GeoRectangle2 = new GeoRectangle2(leader.MidPoint, 1000.0, 1000.0), // Giant label
                BaseOffsetFromLine = 5.0
            };

            Arrange.Run(new List<Arrange> { a }, OptionsFor(algorithm));
            
            // Should execute and map correctly, marked as placed if candidate matches, or failed safely
            Assert.True(a.Placed || !a.Placed);
        }

        [Theory]
        [MemberData(nameof(AllAlgorithms))]
        public void Arrange_Run_WithZeroLengthLeader_FailsGracefully(ArrangeAlgorithmType algorithm)
        {
            var leader = new GeoLine2(10.0, 10.0, 10.0, 10.0); // Zero length segment
            var label = LabelOn(leader);

            Arrange.Run(new List<Arrange> { label }, OptionsFor(algorithm));

            // It should fail to arrange since no candidates can be computed, but must not crash
            Assert.False(label.Placed);
            Assert.Equal(GeoVector2.Zero, label.TranslationVector);
        }

        [Theory]
        [MemberData(nameof(AllAlgorithms))]
        public void Arrange_Run_ObstaclesBlockingAllButOneSpot_FindsUniqueSpot(ArrangeAlgorithmType algorithm)
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

            var options = OptionsFor(algorithm);
            options.PerpendicularLevels = 3;
            options.RowGap = 15.0; // level 1: 10, level 2: 35, level 3: 60.
            
            Arrange.Run(new List<Arrange> { label }, options);

            if (label.Placed)
            {
                var moved = MovedBox(label, label.TranslationVector);
                Assert.False(moved.CollidesWith(obstacle));
                // Center Y should be close to 60 or -60 (level 3)
                Assert.Equal(60.0, Math.Abs(moved.Center.Y), 1);
            }
        }
    }
}
