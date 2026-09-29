using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Arranging;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Arranging
{
    /// <summary>
    /// <see cref="ArrangeItem.Side"/>: a label kept to one side of its leader, the side that faces up in the drawing or
    /// the side that faces down, the sides of <see cref="ArrangeItem.OffsetTop"/> and <see cref="ArrangeItem.OffsetBottom"/>.
    /// </summary>
    public class ArrangeSideTests : ArrangeTestKit
    {
        /// <summary>
        /// The label of the guide's example, 2000 by 1000 on a leader 2000 long: its first rows stand 500 plus the gap
        /// of each side off.
        /// </summary>
        private static ArrangeItem DimensionText(bool reversed, ArrangeSide side, double top, double bottom)
            => new ArrangeItem
            {
                Box = new GeoRectangle2(new GeoPoint2(1000.0, 0.0), 2000.0, 1000.0),
                Leader = reversed ? new GeoLine2(2000.0, 0.0, 0.0, 0.0) : new GeoLine2(0.0, 0.0, 2000.0, 0.0),
                OffsetTop = top,
                OffsetBottom = bottom,
                Side = side,
            };

        /// <summary>The normal of the leader on its top side: facing up, or to the left for a vertical leader.</summary>
        private static GeoVector2 TopNormal(GeoLine2 leader)
        {
            double length = leader.Length;
            var normal = new GeoVector2(-(leader.EndPoint.Y - leader.StartPoint.Y) / length, (leader.EndPoint.X - leader.StartPoint.X) / length);
            return normal.Y < 0.0 || (normal.Y == 0.0 && normal.X > 0.0) ? -normal : normal;
        }

        // How far a point stands off the leader, towards its top side.
        private static double Across(GeoPoint2 point, GeoLine2 leader) => leader.MidPoint.GetVectorTo(point).DotProduct(TopNormal(leader));

        public static IEnumerable<object[]> Leaders() => new[]
        {
            new object[] { 0.0, 0.0, 100.0, 0.0 },
            new object[] { 100.0, 0.0, 0.0, 0.0 },
            new object[] { 0.0, 0.0, 0.0, 100.0 },
            new object[] { 0.0, 100.0, 0.0, 0.0 },
            new object[] { 0.0, 0.0, 100.0, 100.0 },
            new object[] { 0.0, 100.0, 100.0, 0.0 },
        };

        // Every algorithm, with the leader drawn either way, kept to either side.
        public static IEnumerable<object[]> AllAlgorithmsBothWaysOneSide()
            => from algorithm in Enum.GetValues(typeof(ArrangeAlgorithmType)).Cast<ArrangeAlgorithmType>()
               from reversed in new[] { false, true }
               from side in new[] { ArrangeSide.Top, ArrangeSide.Bottom }
               select new object[] { algorithm, reversed, side };

        [Fact]
        public void TheSide_StartsAsBoth()
        {
            Assert.Equal(ArrangeSide.Both, new ArrangeItem().Side);
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(3)]
        public void ASideThatIsNone_IsRefused(int value)
        {
            var label = new ArrangeItem { Side = ArrangeSide.Top };

            Assert.Throws<ArgumentOutOfRangeException>(() => label.Side = (ArrangeSide)value);
            Assert.Equal(ArrangeSide.Top, label.Side);
        }

        /// <summary>
        /// Kept to one side, a label has the candidates of that side and no others, in the order they come in when both
        /// sides are open, whichever way the leader runs; the top of a vertical leader is its left.
        /// </summary>
        [Theory]
        [MemberData(nameof(Leaders))]
        public void OneSide_HasTheCandidatesOfThatSideAlone(double x0, double y0, double x1, double y1)
        {
            var leader = new GeoLine2(x0, y0, x1, y1);
            foreach (double[] gaps in new[] { new[] { 12.0, 12.0 }, new[] { 5.0, 30.0 }, new[] { 30.0, 5.0 } })
            {
                ArrangeItem label = LabelOn(leader);
                label.OffsetTop = gaps[0];
                label.OffsetBottom = gaps[1];
                List<GeoPoint2> both = label.GetPlacePoints(OptionsFor());

                label.Side = ArrangeSide.Top;
                List<GeoPoint2> top = label.GetPlacePoints(OptionsFor());
                label.Side = ArrangeSide.Bottom;
                List<GeoPoint2> bottom = label.GetPlacePoints(OptionsFor());

                Assert.Equal(both.Where(p => Across(p, leader) > 0.0), top);
                Assert.Equal(both.Where(p => Across(p, leader) < 0.0), bottom);
                Assert.Equal(both.Count, top.Count + bottom.Count);
                Assert.Equal(top.Count, bottom.Count);

                // The first row of each side stands half the height of the label across the leader plus its gap off.
                Assert.Equal(top.Min(p => Across(p, leader)) - gaps[0], -bottom.Max(p => Across(p, leader)) - gaps[1], 9);
            }
        }

        /// <summary>
        /// Kept to one side, the label stays there though the other side stands nearer and is free, and though its own
        /// leader is among what it keeps clear of or not. With the same gap on both sides, the force-directed algorithm
        /// pushed a label sitting on its own leader along +X, off along the leader.
        /// </summary>
        [Theory]
        [MemberData(nameof(AllAlgorithmsBothWaysOneSide))]
        public void EveryAlgorithm_KeepsTheLabelToItsSide(ArrangeAlgorithmType algorithm, bool reversed, ArrangeSide side)
        {
            foreach (double otherGap in new[] { 20.0, 300.0 })
            foreach (bool leaderBlocked in new[] { false, true })
            {
                // The side kept to has a gap of 300, the other side the smaller or the same.
                ArrangeItem label = side == ArrangeSide.Top
                    ? DimensionText(reversed, side, 300.0, otherGap)
                    : DimensionText(reversed, side, otherGap, 300.0);
                if (leaderBlocked)
                {
                    label.BlockLines = new[] { label.Leader };
                }

                ArrangeResult result = Arranger.Run(new[] { label }, new ArrangeOptions { Algorithm = algorithm })[0];
                GeoPoint2 centre = label.Box.Center + result.Translation;

                string what = $"{algorithm}, other gap {otherGap}, leader blocked {leaderBlocked}: {centre}";
                Assert.True(result.Placed, what);
                Assert.True(centre.IsEqualTo(new GeoPoint2(1000.0, side == ArrangeSide.Top ? 800.0 : -800.0)), what);
            }
        }

        /// <summary>
        /// With no free place on its side, the label is left on the first of them, overlapping, however free the other side.
        /// </summary>
        [Theory]
        [MemberData(nameof(AllAlgorithmsBothWaysOneSide))]
        public void AWalledInSide_IsWhereTheLabelIsLeft(ArrangeAlgorithmType algorithm, bool reversed, ArrangeSide side)
        {
            ArrangeItem label = DimensionText(reversed, side, 20.0, 20.0);
            label.BlockPolygons = new[]
            {
                side == ArrangeSide.Top ? Rectangle(-5000.0, 1.0, 7000.0, 5000.0) : Rectangle(-5000.0, -5000.0, 7000.0, -1.0)
            };

            ArrangeResult result = Arranger.Run(new[] { label }, new ArrangeOptions { Algorithm = algorithm })[0];

            Assert.False(result.Placed);
            Assert.True((label.Box.Center + result.Translation).IsEqualTo(new GeoPoint2(1000.0, side == ArrangeSide.Top ? 520.0 : -520.0)), $"{algorithm}: {result}");
        }

        /// <summary>
        /// Every place crosses a block line, so the label is placed in the second pass, which runs on a copy of the item:
        /// the copy keeps the side.
        /// </summary>
        [Theory]
        [MemberData(nameof(AllAlgorithmsBothWaysOneSide))]
        public void TheSecondPass_KeepsTheSide(ArrangeAlgorithmType algorithm, bool reversed, ArrangeSide side)
        {
            var lines = new List<GeoLine2>();
            for (int x = -400; x <= 800; x += 7)
            {
                lines.Add(new GeoLine2(x, -600.0, x, 800.0));
            }

            ArrangeItem label = LabelOn(reversed ? new GeoLine2(100.0, 0.0, 0.0, 0.0) : new GeoLine2(0.0, 0.0, 100.0, 0.0));
            label.Side = side;
            label.BlockLines = lines;

            ArrangeResult result = Arranger.Run(new[] { label }, OptionsFor(algorithm))[0];

            Assert.False(result.Placed);
            Assert.True(result.Translation.IsEqualTo(new GeoVector2(0.0, side == ArrangeSide.Top ? 10.0 : -10.0)), $"{algorithm}: {result}");
        }

        /// <summary>
        /// The obstacles a label is checked against are those within reach of the candidates of its side, out to the
        /// last row: over the first two rows above, and the label kept to the top takes the third, the side below free.
        /// </summary>
        [Theory]
        [MemberData(nameof(AllAlgorithms))]
        public void TheReachOfOneSide_RunsOutToItsLastRow(ArrangeAlgorithmType algorithm)
        {
            ArrangeItem label = LabelOn(new GeoLine2(0.0, 0.0, 100.0, 0.0));
            label.Offset = 20.0;
            label.Side = ArrangeSide.Top;
            label.BlockPolygons = new[] { Rectangle(-100.0, 20.0, 200.0, 30.0), Rectangle(-100.0, 130.0, 200.0, 140.0) };

            ArrangeResult result = Arranger.Run(new[] { label }, new ArrangeOptions { Algorithm = algorithm, RowGap = 100.0, PerpendicularLevels = 3 })[0];

            Assert.True(result.Placed, $"{algorithm}: {result}");
            Assert.True((label.Box.Center + result.Translation).IsEqualTo(new GeoPoint2(50.0, 245.0)), $"{algorithm}: {result}");
        }

        [Theory]
        [InlineData(0.0, 0.0, 0.0, 100.0)]
        [InlineData(0.0, 100.0, 0.0, 0.0)]
        public void TheTopOfAVerticalLeader_IsItsLeft(double x0, double y0, double x1, double y1)
        {
            ArrangeItem label = LabelOn(new GeoLine2(x0, y0, x1, y1));

            label.Side = ArrangeSide.Top;
            Assert.All(label.GetPlacePoints(OptionsFor()), p => Assert.True(p.X < 0.0, p.ToString()));

            label.Side = ArrangeSide.Bottom;
            Assert.All(label.GetPlacePoints(OptionsFor()), p => Assert.True(p.X > 0.0, p.ToString()));
        }
    }
}
