using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Arranging;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Arranging
{
    /// <summary>
    /// <see cref="ArrangeItem.OffsetTop"/> and <see cref="ArrangeItem.OffsetBottom"/>: the gap on the side of the leader
    /// that faces up in the drawing and on the side that faces down, each taking <see cref="ArrangeItem.Offset"/> when
    /// not set. The label is 20 wide and 10 high, so across a horizontal leader it stands half of 10 plus the gap off.
    /// </summary>
    public class ArrangeSideOffsetTests : ArrangeTestKit
    {
        private static ArrangeItem Label(GeoLine2 leader, double? top, double? bottom, double offset = 12.0)
            => new ArrangeItem
            {
                Leader = leader,
                Box = new GeoRectangle2(leader.MidPoint, 20.0, 10.0),
                Offset = offset,
                OffsetTop = top,
                OffsetBottom = bottom,
            };

        /// <summary>
        /// The label of the guide's example: 2000 by 1000 on a leader 2000 long, 20 off above it and 300 below, or the
        /// other way round. Its first rows stand 500 + 20 and 500 + 300 off.
        /// </summary>
        private static ArrangeItem DimensionText(bool reversed, bool topNear)
            => new ArrangeItem
            {
                Box = new GeoRectangle2(new GeoPoint2(1000.0, 0.0), 2000.0, 1000.0),
                Leader = reversed ? new GeoLine2(2000.0, 0.0, 0.0, 0.0) : new GeoLine2(0.0, 0.0, 2000.0, 0.0),
                Offset = 50.0,
                OffsetTop = topNear ? 20.0 : 300.0,
                OffsetBottom = topNear ? 300.0 : 20.0,
            };

        // The rows of candidates above a horizontal leader at y = 0, nearest first, and those below it.
        private static double[] Above(IEnumerable<GeoPoint2> points)
            => points.Where(p => p.Y > 0.0).Select(p => Math.Round(p.Y, 9)).Distinct().OrderBy(y => y).ToArray();

        private static double[] Below(IEnumerable<GeoPoint2> points)
            => points.Where(p => p.Y < 0.0).Select(p => Math.Round(p.Y, 9)).Distinct().OrderByDescending(y => y).ToArray();

        // The rows across a horizontal leader at y = 0, in the order the candidates reach them.
        private static double[] RowsInTurn(IEnumerable<GeoPoint2> points)
            => points.Select(p => Math.Round(p.Y, 9)).Distinct().ToArray();

        /// <summary>
        /// How far off the leader the nearest candidates on either side of it stand, measured along the normal of the
        /// leader that faces up: positive above, negative below.
        /// </summary>
        private static (double Top, double Bottom) FirstRows(IEnumerable<GeoPoint2> points, GeoLine2 leader)
        {
            double length = leader.Length;
            double upX = -(leader.EndPoint.Y - leader.StartPoint.Y) / length, upY = (leader.EndPoint.X - leader.StartPoint.X) / length;
            if (upY < 0.0)
            {
                upX = -upX;
                upY = -upY;
            }

            GeoPoint2 middle = leader.MidPoint;
            double[] across = points.Select(p => (p.X - middle.X) * upX + (p.Y - middle.Y) * upY).ToArray();
            return (across.Where(d => d > 0.0).Min(), across.Where(d => d < 0.0).Max());
        }

        public static IEnumerable<object[]> Leaders() => new[]
        {
            new object[] { 0.0, 0.0, 100.0, 0.0 },
            new object[] { 100.0, 0.0, 0.0, 0.0 },
            new object[] { 0.0, 0.0, 0.0, 100.0 },
            new object[] { 0.0, 100.0, 0.0, 0.0 },
            new object[] { 0.0, 0.0, 100.0, 100.0 },
            new object[] { 0.0, 100.0, 100.0, 0.0 },
        };

        // Every algorithm, with the leader drawn either way.
        public static IEnumerable<object[]> AllAlgorithmsBothWays()
            => from algorithm in Enum.GetValues(typeof(ArrangeAlgorithmType)).Cast<ArrangeAlgorithmType>()
               from reversed in new[] { false, true }
               select new object[] { algorithm, reversed };

        // Every algorithm, with the leader drawn either way, the top side the nearer or the further.
        public static IEnumerable<object[]> AllAlgorithmsBothWaysBothSides()
            => from algorithm in Enum.GetValues(typeof(ArrangeAlgorithmType)).Cast<ArrangeAlgorithmType>()
               from reversed in new[] { false, true }
               from topNear in new[] { true, false }
               select new object[] { algorithm, reversed, topNear };

        [Theory]
        [MemberData(nameof(Leaders))]
        public void BothSidesGivenTheOffset_GiveTheCandidatesOfTheOffsetAlone(double x0, double y0, double x1, double y1)
        {
            var leader = new GeoLine2(x0, y0, x1, y1);
            List<GeoPoint2> alone = Label(leader, null, null, 30.0).GetPlacePoints(OptionsFor());

            Assert.NotEmpty(alone);
            Assert.Equal(alone, Label(leader, 30.0, 30.0, 30.0).GetPlacePoints(OptionsFor()));
            Assert.Equal(alone, Label(leader, 30.0, 30.0, 7.0).GetPlacePoints(OptionsFor()));
        }

        [Fact]
        public void EachSide_HasItsFirstRowAtItsOwnGap()
        {
            List<GeoPoint2> points = Label(new GeoLine2(0.0, 0.0, 100.0, 0.0), 30.0, 5.0).GetPlacePoints(OptionsFor());

            // Half the height of the label (5) plus the gap of the side; each further row the height and the row gap more.
            Assert.Equal(new[] { 35.0, 50.0, 65.0 }, Above(points));
            Assert.Equal(new[] { -10.0, -25.0, -40.0 }, Below(points));
            Assert.True(points[0].IsEqualTo(new GeoPoint2(50.0, -10.0)));
        }

        /// <summary>
        /// The rows of both sides are tried nearest first. Taking the rows level by level, as when both sides stood
        /// equally far off, tried the first row of the far side before the second row of the near one, and greedy
        /// placement, which looks at the first few free places only, went to the far side with the near one free.
        /// </summary>
        [Fact]
        public void TheRowsOfBothSides_ComeNearestFirst()
        {
            List<GeoPoint2> points = Label(new GeoLine2(0.0, 0.0, 100.0, 0.0), 5.0, 300.0).GetPlacePoints(new ArrangeOptions());

            Assert.Equal(new[] { 10.0, 40.0, 70.0, -305.0, -335.0, -365.0 }, RowsInTurn(points));

            // A row on its own: straight across first, then a step back along the leader and a step forward, in turn.
            // The leader reaches 50 + 20 * 0.75 each way, in twenty steps of 3.25.
            Assert.True(points[0].IsEqualTo(new GeoPoint2(50.0, 10.0)));
            Assert.True(points[1].IsEqualTo(new GeoPoint2(46.75, 10.0)));
            Assert.True(points[2].IsEqualTo(new GeoPoint2(53.25, 10.0)));
            Assert.True(points[41].IsEqualTo(new GeoPoint2(50.0, 40.0)));
        }

        [Theory]
        [InlineData(ArrangeAlgorithmType.Greedy)]
        [InlineData(ArrangeAlgorithmType.BoundedBacktracking)]
        [InlineData(ArrangeAlgorithmType.SimulatedAnnealing)]
        [InlineData(ArrangeAlgorithmType.ConstraintSatisfaction)]
        public void ARowFreeOnTheNearSide_IsTakenBeforeTheFarSide(ArrangeAlgorithmType algorithm)
        {
            // The first row above (10 off) runs into the region; the second above (40) is free, and far nearer than
            // the first below (305).
            ArrangeItem label = Label(new GeoLine2(0.0, 0.0, 100.0, 0.0), 5.0, 300.0);
            label.BlockPolygons = new[] { Rectangle(-100.0, 6.0, 200.0, 14.0) };

            ArrangeResult result = Arranger.Run(new[] { label }, new ArrangeOptions { Algorithm = algorithm })[0];

            Assert.True(result.Placed);
            Assert.True((label.Box.Center + result.Translation).IsEqualTo(new GeoPoint2(50.0, 40.0)), $"{algorithm}: {result}");
        }

        /// <summary>
        /// Close above the line and well clear below it, or the other way round, whichever way the leader was drawn,
        /// and whether or not the label's own leader is among what it keeps clear of, as it is when every leader of
        /// a drawing is handed to every label. Greedy placement took whichever of its first free places stood
        /// furthest from anything, and with the leader to keep clear of that was the side with the wider gap; it
        /// also tried the leader's left first, so a leader drawn the other way sent the label to the far side. The
        /// force-directed one pushed a label sitting on its own leader along +X, off to one side or the other.
        /// </summary>
        [Theory]
        [MemberData(nameof(AllAlgorithmsBothWaysBothSides))]
        public void EveryAlgorithm_PutsTheLabelOnTheSideWithTheSmallerGap(ArrangeAlgorithmType algorithm, bool reversed, bool topNear)
        {
            foreach (bool leaderBlocked in new[] { false, true })
            {
                ArrangeItem label = DimensionText(reversed, topNear);
                if (leaderBlocked)
                {
                    label.BlockLines = new[] { label.Leader };
                }

                ArrangeResult result = Arranger.Run(new[] { label }, new ArrangeOptions { Algorithm = algorithm })[0];
                GeoPoint2 centre = label.Box.Center + result.Translation;

                Assert.True(result.Placed, $"{algorithm}, leader blocked {leaderBlocked}: {result}");
                Assert.True(centre.IsEqualTo(new GeoPoint2(1000.0, topNear ? 520.0 : -520.0)), $"{algorithm}, leader blocked {leaderBlocked}: {centre}");
            }
        }

        /// <summary>
        /// A label with no free place at all is left on the first of its candidates, which is on the side with the
        /// smaller gap, whichever way the leader was drawn.
        /// </summary>
        [Theory]
        [MemberData(nameof(AllAlgorithmsBothWays))]
        public void AWalledInLabel_IsLeftOnTheSideWithTheSmallerGap(ArrangeAlgorithmType algorithm, bool reversed)
        {
            ArrangeItem label = DimensionText(reversed, topNear: true);
            label.BlockPolygons = new[] { Rectangle(-5000.0, -5000.0, 7000.0, 5000.0) };

            ArrangeResult result = Arranger.Run(new[] { label }, new ArrangeOptions { Algorithm = algorithm })[0];

            Assert.False(result.Placed);
            Assert.True((label.Box.Center + result.Translation).IsEqualTo(new GeoPoint2(1000.0, 520.0)), $"{algorithm}: {result}");
        }

        /// <summary>
        /// Dimension texts turned along their leaders and centred on them, every leader a block line of every label:
        /// each text goes to the top of its leader, the left of a vertical one, 500 + 20 off. Where along its row it
        /// goes is not asked: turned boxes measure a hair nearer or further from their leader from place to place.
        /// </summary>
        [Theory]
        [MemberData(nameof(AllAlgorithms))]
        public void TextsTurnedAlongTheirLeaders_StandOnTheTopSide(ArrangeAlgorithmType algorithm)
        {
            foreach (double degrees in new[] { 90.0, 270.0, 45.0, 225.0 })
            {
                double angle = degrees * Math.PI / 180.0;
                var leaders = Enumerable.Range(0, 3)
                    .Select(i => new GeoLine2(6000.0 * i, 0.0, 6000.0 * i + 4000.0 * Math.Cos(angle), 4000.0 * Math.Sin(angle)))
                    .ToList();
                var labels = leaders.Select(leader => new ArrangeItem
                {
                    Box = new GeoRectangle2(leader.MidPoint, 2000.0, 1000.0, angle),
                    Leader = leader,
                    OffsetTop = 20.0,
                    OffsetBottom = 300.0,
                    BlockLines = leaders.Where(other => !other.Equals(leader)).ToList(),
                }).ToList();

                ArrangeResult[] results = Arranger.Run(labels, new ArrangeOptions { Algorithm = algorithm });

                // The top: the left of a vertical leader, up and to the left of one rising to the right.
                GeoVector2 up = degrees % 180.0 == 90.0 ? new GeoVector2(-1.0, 0.0) : new GeoVector2(-Math.Sqrt(0.5), Math.Sqrt(0.5));
                for (int i = 0; i < results.Length; i++)
                {
                    Assert.True(results[i].Placed, $"{algorithm}, {degrees} degrees, label {i}: {results[i]}");
                    Assert.True(Math.Abs(results[i].Translation.DotProduct(up) - 520.0) < 1e-6, $"{algorithm}, {degrees} degrees, label {i}: {results[i]}");
                }
            }
        }

        /// <summary>
        /// Greedy placement keeps the freest of its first few free places, and the gap a side asks for is not room to
        /// spare. One place is free above, 50 clear of a region, and the whole row below: measured as it stands, each
        /// place below is 300 clear, of its own leader, and won; beyond the gap of its side it is nought, as the place
        /// above is 50 clear beyond its own.
        /// </summary>
        [Fact]
        public void Greedy_DoesNotCountTheGapOfASideAsRoomToSpare()
        {
            ArrangeItem label = DimensionText(reversed: false, topNear: true);
            label.BlockLines = new[] { label.Leader };

            // Over every place of the first row above but the last one back, whose box ends 50 short of it.
            label.BlockPolygons = new[] { Rectangle(-450.0, 500.0, 5000.0, 600.0) };

            ArrangeResult result = Arranger.Run(new[] { label }, new ArrangeOptions())[0];

            Assert.True(result.Placed);
            Assert.True((label.Box.Center + result.Translation).IsEqualTo(new GeoPoint2(-1500.0, 520.0)), result.ToString());
        }

        [Fact]
        public void ALeaderDrawnTheOtherWay_KeepsItsTopAbove()
        {
            List<GeoPoint2> forward = Label(new GeoLine2(0.0, 0.0, 100.0, 0.0), 30.0, 5.0).GetPlacePoints(OptionsFor());
            List<GeoPoint2> points = Label(new GeoLine2(100.0, 0.0, 0.0, 0.0), 30.0, 5.0).GetPlacePoints(OptionsFor());

            Assert.Equal(new[] { 35.0, 50.0, 65.0 }, Above(points));
            Assert.Equal(new[] { -10.0, -25.0, -40.0 }, Below(points));

            // The rows come in the same order whichever way the leader runs, the nearest first: the bottom here.
            Assert.Equal(new[] { -10.0, -25.0, 35.0, -40.0, 50.0, 65.0 }, RowsInTurn(points));
            Assert.Equal(RowsInTurn(forward), RowsInTurn(points));
            Assert.True(points[0].IsEqualTo(new GeoPoint2(50.0, -10.0)));
        }

        [Theory]
        [InlineData(0.0, 0.0, 0.0, 100.0)]
        [InlineData(0.0, 100.0, 0.0, 0.0)]
        [InlineData(0.0, 0.0, 1e-12, 100.0)]
        [InlineData(1e-12, 100.0, 0.0, 0.0)]
        [InlineData(1e-12, 0.0, 0.0, 100.0)]
        [InlineData(0.0, 100.0, 1e-12, 0.0)]
        public void AVerticalLeader_HasItsTopOnTheLeft(double x0, double y0, double x1, double y1)
        {
            List<GeoPoint2> points = Label(new GeoLine2(x0, y0, x1, y1), 30.0, 5.0).GetPlacePoints(OptionsFor());

            // Across a vertical leader the label is 20 wide: 10 plus the gap of the side, rows 20 + 5 apart. A leader
            // leaning a hair to the left as it rises has its upper side on the left, a hair of it facing down: within the
            // tolerance it is vertical, and its top is on the left too.
            double[] left = points.Where(p => p.X < 0.0).Select(p => Math.Round(p.X, 6)).Distinct().OrderByDescending(x => x).ToArray();
            double[] right = points.Where(p => p.X > 0.0).Select(p => Math.Round(p.X, 6)).Distinct().OrderBy(x => x).ToArray();

            Assert.Equal(new[] { -40.0, -65.0, -90.0 }, left);
            Assert.Equal(new[] { 15.0, 40.0, 65.0 }, right);
        }

        [Theory]
        [InlineData(0.0, 0.0, 100.0, 100.0)]
        [InlineData(100.0, 100.0, 0.0, 0.0)]
        [InlineData(0.0, 100.0, 100.0, 0.0)]
        [InlineData(100.0, 0.0, 0.0, 100.0)]
        [InlineData(10.0, 20.0, 250.0, 60.0)]
        public void ASlantedLeader_HasItsTopOnTheSideFacingUp(double x0, double y0, double x1, double y1)
        {
            var leader = new GeoLine2(x0, y0, x1, y1);
            (double top, double bottom) = FirstRows(Label(leader, 30.0, 5.0).GetPlacePoints(OptionsFor()), leader);

            // Half the label's height across the leader, whatever the slant, once the gap of each side is taken away.
            Assert.Equal(top - 30.0, -bottom - 5.0, 9);
        }

        /// <summary>
        /// Which side is the top does not hang on the tolerance of vectors, a length: with EqualVector at 1 the
        /// perpendicular of a horizontal leader passed for that of a vertical one, and the gaps swapped with the way
        /// the leader was drawn.
        /// </summary>
        [Theory]
        [InlineData(0.0, 100.0)]
        [InlineData(100.0, 0.0)]
        public void ACoarseToleranceOfVectors_KeepsTheTopAbove(double x0, double x1)
        {
            ArrangeOptions options = OptionsFor();
            options.Tolerance = new Tolerance(1.0, 1.0);

            List<GeoPoint2> points = Label(new GeoLine2(x0, 0.0, x1, 0.0), 30.0, 5.0).GetPlacePoints(options);

            Assert.Equal(new[] { 35.0, 50.0, 65.0 }, Above(points));
            Assert.Equal(new[] { -10.0, -25.0, -40.0 }, Below(points));
        }

        /// <summary>
        /// A leader twenty degrees off vertical is not vertical, however coarse the tolerance of vectors: its top is
        /// the side that faces up, though it leans to the left as it rises.
        /// </summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ALeaderTwentyDegreesOffVertical_HasItsTopOnTheSideFacingUp(bool reversed)
        {
            double lean = 20.0 * Math.PI / 180.0;
            var leader = reversed
                ? new GeoLine2(-100.0 * Math.Sin(lean), 100.0 * Math.Cos(lean), 0.0, 0.0)
                : new GeoLine2(0.0, 0.0, -100.0 * Math.Sin(lean), 100.0 * Math.Cos(lean));
            ArrangeOptions options = OptionsFor();
            options.Tolerance = new Tolerance(0.5, 0.5);

            (double top, double bottom) = FirstRows(Label(leader, 30.0, 5.0).GetPlacePoints(options), leader);

            Assert.Equal(top - 30.0, -bottom - 5.0, 9);
        }

        /// <summary>
        /// Every place of the label crosses a block line, so it is placed in the second pass, which runs on a copy of the
        /// item with the lines lifted: the copy has to carry the gap of each side, or the side left without its own
        /// comes back 5 + 50 off and the label goes to the other.
        /// </summary>
        [Theory]
        [MemberData(nameof(AllAlgorithmsBothWaysBothSides))]
        public void TheSecondPass_KeepsTheGapOfEachSide(ArrangeAlgorithmType algorithm, bool reversed, bool topNear)
        {
            var lines = new List<GeoLine2>();
            for (int x = -400; x <= 800; x += 7)
            {
                lines.Add(new GeoLine2(x, -600.0, x, 800.0));
            }

            ArrangeItem label = Label(reversed ? new GeoLine2(100.0, 0.0, 0.0, 0.0) : new GeoLine2(0.0, 0.0, 100.0, 0.0),
                topNear ? 5.0 : 30.0, topNear ? 30.0 : 5.0, offset: 50.0);
            label.BlockLines = lines;

            ArrangeResult result = Arranger.Run(new[] { label }, OptionsFor(algorithm))[0];

            Assert.False(result.Placed);
            Assert.True(result.Translation.IsEqualTo(new GeoVector2(0.0, topNear ? 10.0 : -10.0)), $"{algorithm}: {result}");
        }

        /// <summary>
        /// The obstacles a label is checked against are those within reach of its candidates. The top reaches 300 off
        /// here and the bottom 5: an obstacle over the first two top rows is kept clear of, with the bottom walled up, and
        /// the label goes to the third row, whichever way the leader runs.
        /// </summary>
        [Theory]
        [InlineData(0.0, 100.0)]
        [InlineData(100.0, 0.0)]
        public void AnObstacleFarOutOnTheWiderSide_IsKeptClearOf(double x0, double x1)
        {
            ArrangeItem label = Label(new GeoLine2(x0, 0.0, x1, 0.0), 300.0, 5.0);
            label.BlockPolygons = new[]
            {
                Rectangle(-500.0, -1000.0, 600.0, -1.0),
                Rectangle(-100.0, 290.0, 200.0, 320.0),
            };

            ArrangeResult result = Arranger.Run(new[] { label }, OptionsFor())[0];

            // The rows above stand 305, 320 and 335 off; the first two run into the obstacle.
            Assert.True(result.Placed);
            Assert.Equal(335.0, (label.Box.Center + result.Translation).Y, 9);
        }

        /// <summary>
        /// The reach runs out to the last row on each side, rows far apart here: obstacles over the first two rows above,
        /// the bottom walled up, and every algorithm takes the third row, 25 + 2 * (10 + 100) off.
        /// </summary>
        [Theory]
        [MemberData(nameof(AllAlgorithms))]
        public void TheReach_RunsOutToTheLastRow(ArrangeAlgorithmType algorithm)
        {
            ArrangeItem label = Label(new GeoLine2(0.0, 0.0, 100.0, 0.0), 20.0, 20.0);
            label.BlockPolygons = new[]
            {
                Rectangle(-500.0, -1000.0, 600.0, -1.0),
                Rectangle(-100.0, 20.0, 200.0, 30.0),
                Rectangle(-100.0, 130.0, 200.0, 140.0),
            };

            ArrangeResult result = Arranger.Run(new[] { label }, new ArrangeOptions { Algorithm = algorithm, RowGap = 100.0, PerpendicularLevels = 3 })[0];

            Assert.True(result.Placed, $"{algorithm}: {result}");
            Assert.True((label.Box.Center + result.Translation).IsEqualTo(new GeoPoint2(50.0, 245.0)), $"{algorithm}: {result}");
        }

        /// <summary>
        /// A gap negative enough takes the rows of its side across the leader and past those of the other. The reach
        /// ran from the last row on each side, and left out the first rows, and the obstacles over them with them.
        /// </summary>
        [Theory]
        [MemberData(nameof(AllAlgorithms))]
        public void AGapThatCrossesTheLeader_KeepsItsFirstRowWithinReach(ArrangeAlgorithmType algorithm)
        {
            // The top rows stand at -295 to -175, the bottom ones at -10 to -130: the obstacles cover -295 and -10.
            ArrangeItem label = Label(new GeoLine2(0.0, 0.0, 100.0, 0.0), -300.0, 5.0);
            GeoPolygon2[] regions = { Rectangle(-500.0, -20.0, 600.0, -1.0), Rectangle(-500.0, -310.0, 600.0, -280.0) };
            label.BlockPolygons = regions;

            ArrangeResult result = Arranger.Run(new[] { label }, new ArrangeOptions { Algorithm = algorithm, PerpendicularLevels = 5 })[0];
            GeoRectangle2 moved = label.Box.Translate(result.Translation);

            Assert.True(result.Placed, $"{algorithm}: {result}");
            Assert.All(regions, region => Assert.False(moved.CollidesWith(region)));
        }

        /// <summary>
        /// A gap as wide as a number goes cannot pull the label off while a row near the leader is free. Greedy
        /// placement measured such a row as the most open place there was, and a box moved so far lost its shape: the
        /// run then threw making a region of it.
        /// </summary>
        [Theory]
        [MemberData(nameof(AllAlgorithms))]
        public void AHugeGapOnOneSide_DoesNotSendTheLabelFarOff(ArrangeAlgorithmType algorithm)
        {
            foreach (double gap in new[] { 1e9, double.MaxValue })
            {
                ArrangeItem label = Label(new GeoLine2(0.0, 0.0, 100.0, 0.0), 5.0, gap);
                label.BlockPolygons = new[] { Rectangle(-100.0, 6.0, 200.0, 14.0) };

                ArrangeResult result = Arranger.Run(new[] { label }, new ArrangeOptions { Algorithm = algorithm })[0];

                Assert.True(result.Placed, $"{algorithm}, gap {gap}: {result}");
                Assert.True((label.Box.Center + result.Translation).IsEqualTo(new GeoPoint2(50.0, 40.0)), $"{algorithm}, gap {gap}: {result}");
            }
        }

        /// <summary>
        /// One label walled in above has nowhere to go but a row further off than a box can keep its shape at, and
        /// another has nowhere at all, so the second pass runs: it keeps clear of the labels the first placed, and made
        /// a region of the far one, which threw.
        /// </summary>
        [Theory]
        [MemberData(nameof(AllAlgorithms))]
        public void ALabelMovedFurtherThanABoxKeepsItsShape_DoesNotStopTheRun(ArrangeAlgorithmType algorithm)
        {
            ArrangeItem far = Label(new GeoLine2(0.0, 0.0, 100.0, 0.0), 5.0, double.MaxValue);
            far.BlockPolygons = new[] { Rectangle(-100.0, 3.0, 200.0, 500.0) };
            ArrangeItem walled = Label(new GeoLine2(1000.0, 0.0, 1100.0, 0.0), null, null, 5.0);
            walled.BlockPolygons = new[] { Rectangle(900.0, -500.0, 1200.0, 500.0) };

            ArrangeResult[] results = Arranger.Run(new[] { far, walled }, new ArrangeOptions { Algorithm = algorithm });

            Assert.Equal(2, results.Length);
            Assert.False(results[1].Placed);
        }

        [Theory]
        [InlineData(double.NaN)]
        [InlineData(double.PositiveInfinity)]
        [InlineData(double.NegativeInfinity)]
        public void TheGaps_HaveToBeFiniteNumbers(double gap)
        {
            var label = new ArrangeItem();

            Assert.Throws<ArgumentOutOfRangeException>(() => label.Offset = gap);
            Assert.Throws<ArgumentOutOfRangeException>(() => label.OffsetTop = gap);
            Assert.Throws<ArgumentOutOfRangeException>(() => label.OffsetBottom = gap);

            // What was refused left the gaps as they were.
            Assert.Equal(50.0, label.Offset);
            Assert.Null(label.OffsetTop);
            Assert.Null(label.OffsetBottom);
        }

        [Fact]
        public void TheGaps_TakeAnyFiniteNumber_AndTheSidesTakeNull()
        {
            var label = new ArrangeItem { Offset = -3.0, OffsetTop = double.MaxValue, OffsetBottom = -1e9 };

            Assert.Equal(-3.0, label.Offset);
            Assert.Equal(double.MaxValue, label.OffsetTop);
            Assert.Equal(-1e9, label.OffsetBottom);

            label.OffsetTop = null;
            label.OffsetBottom = null;
            Assert.Null(label.OffsetTop);
            Assert.Null(label.OffsetBottom);
        }

        [Fact]
        public void TheSideGapsStartUnset()
        {
            var label = new ArrangeItem();

            Assert.Null(label.OffsetTop);
            Assert.Null(label.OffsetBottom);
            Assert.Equal(50.0, label.Offset);
        }
    }
}
