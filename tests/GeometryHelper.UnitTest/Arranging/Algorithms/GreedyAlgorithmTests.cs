using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Geometry;
using Xunit;
using GeometryHelper.Arranging;

namespace GeometryHelper.UnitTest.Arranging
{
    public class GreedyAlgorithmTests
    {
        /// <summary>Shared configuration for tests below, small dimensions for easy manual calculation.</summary>
        private static ArrangeOptions GreedyOptions(int perpendicularLevels = 3)
        {
            return new ArrangeOptions
            {
                Algorithm = ArrangeAlgorithmType.Greedy,
                RowGap = 5.0,
                PerpendicularLevels = perpendicularLevels
            };
        }

        /// <summary>20x10 label initially placed at the midpoint of the guide segment.</summary>
        private static ArrangeItem LabelOn(GeoLine2 leader)
        {
            return new ArrangeItem
            {
                Leader = leader,
                Box = new GeoRectangle2(leader.MidPoint, 20.0, 10.0),
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

        [Fact]
        public void Arrange_Run_Greedy_ArrangesNonOverlappingLabels()
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

            var list = new List<ArrangeItem> { a1, a2 };
            var options = new ArrangeOptions
            {
                Algorithm = ArrangeAlgorithmType.Greedy,
                RowGap = 5.0,
                PerpendicularLevels = 3
            };

            ArrangeResult[] results = Arranger.Run(list, options);

            var moved1 = new GeoRectangle2(a1.Box.Center + results[0].Translation, a1.Box.Width, a1.Box.Height);
            var moved2 = new GeoRectangle2(a2.Box.Center + results[1].Translation, a2.Box.Width, a2.Box.Height);

            Assert.False(moved1.CollidesWith(moved2));
            Assert.True(results[0].Placed);
            Assert.True(results[1].Placed);
        }

        [Fact]
        public void Arrange_Run_Greedy_RespectsObstacles()
        {
            // Horizontal guide segment lies directly below the label. It must have a real length: a degenerate guide segment
            // has no direction and thus generates no candidate positions.
            var leaderLine = new GeoLine2(0.0, 0.0, 20.0, 0.0);
            var rect = new GeoRectangle2(new GeoPoint2(10.0, 10.0), 20.0, 10.0);

            var blockPoly = new GeoPolygon2(
                new GeoPoint2(-50.0, 0.0),
                new GeoPoint2(50.0, 0.0),
                new GeoPoint2(50.0, 30.0),
                new GeoPoint2(-50.0, 30.0)
            );

            var arrange = new ArrangeItem
            {
                Box = rect,
                Leader = leaderLine,
                Offset = 5.0,
                BlockPolygons = new List<GeoPolygon2> { blockPoly }
            };

            var options = new ArrangeOptions
            {
                Algorithm = ArrangeAlgorithmType.Greedy,
                RowGap = 5.0,
                PerpendicularLevels = 2
            };

            ArrangeResult result = Arranger.Run(new List<ArrangeItem> { arrange }, options)[0];

            var moved = new GeoRectangle2(arrange.Box.Center + result.Translation, arrange.Box.Width, arrange.Box.Height);

            Assert.True(moved.Center.Y < 0.0);
            Assert.False(blockPoly.CollidesWith(moved));
            Assert.True(result.Placed);
        }

        [Fact]
        public void Arrange_Run_Greedy_WithNoValidSpaces_FallsBackToFirstCandidate()
        {
            var leaderLine = new GeoLine2(0.0, 0.0, 10.0, 0.0);
            // The label starts far from the candidate cluster. If it is placed exactly at the first candidate,
            // the fallback to that candidate yields a zero vector, and the test becomes meaningless.
            var rect = new GeoRectangle2(new GeoPoint2(5.0, 40.0), 20.0, 10.0);

            // Huge blocked polygon wrapping the entire space around the label area
            var blockPoly = new GeoPolygon2(
                new GeoPoint2(-100.0, -100.0),
                new GeoPoint2(100.0, -100.0),
                new GeoPoint2(100.0, 100.0),
                new GeoPoint2(-100.0, 100.0)
            );

            var arrange = new ArrangeItem
            {
                Box = rect,
                Leader = leaderLine,
                Offset = 5.0,
                BlockPolygons = new List<GeoPolygon2> { blockPoly }
            };

            var options = new ArrangeOptions
            {
                Algorithm = ArrangeAlgorithmType.Greedy,
                RowGap = 5.0,
                PerpendicularLevels = 2
            };

            ArrangeResult result = Arranger.Run(new List<ArrangeItem> { arrange }, options)[0];

            // Regardless of constraints, the Greedy algorithm must fallback to the first candidate
            // instead of staying in place, which causes uncertainty.
            Assert.False(result.Placed);
            Assert.NotEqual(GeoVector2.Zero, result.Translation);
        }

        [Fact]
        public void Arrange_Run_Greedy_ReturnsTranslationsInInputOrder()
        {
            // The second label is constrained by the blocked region and should be processed BEFORE the first label.
            // Still, the returned results must match the original input index.
            var near = LabelOn(new GeoLine2(0.0, 0.0, 40.0, 0.0));
            var far = LabelOn(new GeoLine2(1000.0, 0.0, 1040.0, 0.0));

            far.BlockPolygons = new List<GeoPolygon2>
            {
                new GeoPolygon2(
                    new GeoPoint2(990.0, 0.0),
                    new GeoPoint2(1060.0, 0.0),
                    new GeoPoint2(1060.0, 30.0),
                    new GeoPoint2(990.0, 30.0))
            };

            ArrangeResult[] results = Arranger.Run(new List<ArrangeItem> { near, far }, GreedyOptions());

            // translations[0] must correspond to the left label, translations[1] to the right label.
            Assert.Equal(20.0, MovedBox(near, results[0].Translation).Center.X, 6);
            Assert.Equal(1020.0, MovedBox(far, results[1].Translation).Center.X, 6);

            // And the label blocked above must dodge downwards.
            Assert.True(MovedBox(far, results[1].Translation).Center.Y < 0.0);
        }

        [Fact]
        public void Arrange_Run_Greedy_IsDeterministic()
        {
            ArrangeResult[] Solve()
            {
                var labels = new List<ArrangeItem>();
                for (int i = 0; i < 6; i++)
                {
                    labels.Add(LabelOn(new GeoLine2(i * 12.0, 0.0, i * 12.0 + 30.0, 0.0)));
                }
                return Arranger.Run(labels, GreedyOptions());
            }

            Assert.Equal(Solve(), Solve());
        }

        [Fact]
        public void Arrange_Run_Greedy_WithEmptyList_DoesNotThrow()
        {
            Assert.Empty(Arranger.Run(new List<ArrangeItem>(), GreedyOptions()));
        }

        [Fact]
        public void Arrange_Run_Greedy_SkipsNullEntries()
        {
            var a = LabelOn(new GeoLine2(0.0, 0.0, 40.0, 0.0));
            var b = LabelOn(new GeoLine2(0.0, 0.0, 40.0, 0.0));

            var labels = new List<ArrangeItem> { a, null, b };
            ArrangeResult[] results = Arranger.Run(labels, GreedyOptions());

            // The two real labels must still be arranged normally.
            Assert.False(MovedBox(a, results[0].Translation).CollidesWith(MovedBox(b, results[2].Translation)));
            Assert.True(results[0].Placed);
            Assert.True(results[2].Placed);
        }

        [Fact]
        public void Arrange_Run_Greedy_AvoidsBlockLines()
        {
            var leader = new GeoLine2(0.0, 0.0, 40.0, 0.0);
            var arrange = LabelOn(leader);

            // Block line directly obstructs the first candidate position (above the guide segment).
            var blockLine = new GeoLine2(-20.0, 10.0, 60.0, 10.0);
            arrange.BlockLines = new List<GeoLine2> { blockLine };

            ArrangeResult result = Arranger.Run(new List<ArrangeItem> { arrange }, GreedyOptions())[0];
            var moved = MovedBox(arrange, result.Translation);

            Assert.True(moved.Center.Y < 0.0);
            Assert.False(moved.CollidesWith(blockLine));
            Assert.True(result.Placed);
        }

        [Fact]
        public void Arrange_Run_Greedy_RotatedLabels_AreSeparatedAlongPerpendicular()
        {
            // The guide segment is tilted at 45 degrees; the label rotates to stay parallel to it.
            var leader = new GeoLine2(0.0, 0.0, 40.0, 40.0);
            var box = new GeoRectangle2(leader.MidPoint, 20.0, 10.0, Math.PI / 4.0);

            var a = new ArrangeItem { Leader = leader, Box = box, Offset = 5.0 };
            var b = new ArrangeItem { Leader = leader, Box = box, Offset = 5.0 };

            ArrangeResult[] results = Arranger.Run(new List<ArrangeItem> { a, b }, GreedyOptions());

            Assert.False(MovedBox(a, results[0].Translation).CollidesWith(MovedBox(b, results[1].Translation)));
            Assert.True(results[0].Placed);
            Assert.True(results[1].Placed);

            // The two labels must be placed on opposite sides of the guide segment, i.e., shifted in perpendicular directions.
            Assert.NotEqual(results[0].Translation, results[1].Translation);
        }

        [Fact]
        public void Arrange_Run_Greedy_RepeatedObstacleOnEveryLabel_GivesSameResult()
        {
            // A common usage pattern is to assign the same blocked region to EVERY label. This only inflates
            // the obstacle list and must absolutely not change the arrangement result.
            GeoPolygon2 Block() => new GeoPolygon2(
                new GeoPoint2(-50.0, 0.0),
                new GeoPoint2(50.0, 0.0),
                new GeoPoint2(50.0, 30.0),
                new GeoPoint2(-50.0, 30.0));

            ArrangeResult[] Solve(bool onEveryLabel)
            {
                var block = Block();
                var labels = new List<ArrangeItem>();
                for (int i = 0; i < 4; i++)
                {
                    var label = LabelOn(new GeoLine2(i * 15.0, 0.0, i * 15.0 + 40.0, 0.0));
                    label.BlockPolygons = (onEveryLabel || i == 0)
                        ? new List<GeoPolygon2> { block }
                        : new List<GeoPolygon2>();
                    labels.Add(label);
                }
                return Arranger.Run(labels, GreedyOptions());
            }

            Assert.Equal(Solve(false), Solve(true));
        }

        [Fact]
        public void Arrange_Run_Greedy_LabelOverlappedByLaterFallback_IsNotReportedAsPlaced()
        {
            // Three labels share a short guide segment but only have one perpendicular level, meaning there is only room for two.
            // The third label is forced to fallback and overlap an already placed label.
            var leader = new GeoLine2(0.0, 0.0, 10.0, 0.0);
            var labels = new List<ArrangeItem> { LabelOn(leader), LabelOn(leader), LabelOn(leader) };

            ArrangeResult[] results = Arranger.Run(labels, GreedyOptions(perpendicularLevels: 1));

            // The OVERLAPPED label must also be reported as failed, not just the label causing the collision.
            // Previously it still retained the success flag because that spot was empty when its turn came.
            Assert.Equal(1, results.Count(x => x.Placed));
        }

        [Fact]
        public void Arrange_Run_Greedy_WithoutConstraintOrdering_StillAvoidsOverlap()
        {
            var leader = new GeoLine2(0.0, 0.0, 40.0, 0.0);
            var a = LabelOn(leader);
            var b = LabelOn(leader);

            var options = GreedyOptions();
            options.PlaceMostConstrainedFirst = false;
            options.PlaceFromInsideOut = false;

            ArrangeResult[] results = Arranger.Run(new List<ArrangeItem> { a, b }, options);

            Assert.False(MovedBox(a, results[0].Translation).CollidesWith(MovedBox(b, results[1].Translation)));
            Assert.True(results[0].Placed);
            Assert.True(results[1].Placed);
        }
    }
}
