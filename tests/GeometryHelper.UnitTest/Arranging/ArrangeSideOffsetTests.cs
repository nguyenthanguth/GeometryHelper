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
    public class ArrangeSideOffsetTests
    {
        private static ArrangeOptions Rows(ArrangeAlgorithmType algorithm = ArrangeAlgorithmType.Greedy)
            => new ArrangeOptions { Algorithm = algorithm, RowGap = 5.0, PerpendicularLevels = 3 };

        private static ArrangeItem Label(GeoLine2 leader, double? top, double? bottom, double offset = 12.0)
            => new ArrangeItem
            {
                Leader = leader,
                Box = new GeoRectangle2(leader.MidPoint, 20.0, 10.0),
                Offset = offset,
                OffsetTop = top,
                OffsetBottom = bottom,
            };

        private static GeoPolygon2 Rectangle(double x0, double y0, double x1, double y1)
            => new GeoPolygon2(new GeoPoint2(x0, y0), new GeoPoint2(x1, y0), new GeoPoint2(x1, y1), new GeoPoint2(x0, y1));

        // The rows of candidates above a horizontal leader at y = 0, nearest first, and those below it.
        private static double[] Above(IEnumerable<GeoPoint2> points)
            => points.Where(p => p.Y > 0.0).Select(p => Math.Round(p.Y, 9)).Distinct().OrderBy(y => y).ToArray();

        private static double[] Below(IEnumerable<GeoPoint2> points)
            => points.Where(p => p.Y < 0.0).Select(p => Math.Round(p.Y, 9)).Distinct().OrderByDescending(y => y).ToArray();

        public static IEnumerable<object[]> Leaders() => new[]
        {
            new object[] { 0.0, 0.0, 100.0, 0.0 },
            new object[] { 100.0, 0.0, 0.0, 0.0 },
            new object[] { 0.0, 0.0, 0.0, 100.0 },
            new object[] { 0.0, 100.0, 0.0, 0.0 },
            new object[] { 0.0, 0.0, 100.0, 100.0 },
            new object[] { 0.0, 100.0, 100.0, 0.0 },
        };

        public static IEnumerable<object[]> AllAlgorithms()
            => Enum.GetValues(typeof(ArrangeAlgorithmType)).Cast<ArrangeAlgorithmType>().Select(algorithm => new object[] { algorithm });

        [Theory]
        [MemberData(nameof(Leaders))]
        public void BothSidesGivenTheOffset_GiveTheCandidatesOfTheOffsetAlone(double x0, double y0, double x1, double y1)
        {
            var leader = new GeoLine2(x0, y0, x1, y1);
            List<GeoPoint2> alone = Label(leader, null, null, 30.0).GetPlacePoints(Rows());

            Assert.NotEmpty(alone);
            Assert.Equal(alone, Label(leader, 30.0, 30.0, 30.0).GetPlacePoints(Rows()));
            Assert.Equal(alone, Label(leader, 30.0, 30.0, 7.0).GetPlacePoints(Rows()));
        }

        [Fact]
        public void EachSide_HasItsFirstRowAtItsOwnGap()
        {
            List<GeoPoint2> points = Label(new GeoLine2(0.0, 0.0, 100.0, 0.0), 30.0, 5.0).GetPlacePoints(Rows());

            // Half the height of the label (5) plus the gap of the side; each further row the height and the row gap more.
            Assert.True(points[0].IsEqualTo(new GeoPoint2(50.0, 35.0)));
            Assert.True(points[1].IsEqualTo(new GeoPoint2(50.0, -10.0)));
            Assert.Equal(new[] { 35.0, 50.0, 65.0 }, Above(points));
            Assert.Equal(new[] { -10.0, -25.0, -40.0 }, Below(points));
        }

        [Fact]
        public void ASideNotGivenItsOwnGap_TakesTheOffset()
        {
            var leader = new GeoLine2(0.0, 0.0, 100.0, 0.0);

            // Offset 12: the side left unset stands 5 + 12 off.
            List<GeoPoint2> topOnly = Label(leader, 30.0, null).GetPlacePoints(Rows());
            Assert.Equal(new[] { 35.0, 50.0, 65.0 }, Above(topOnly));
            Assert.Equal(new[] { -17.0, -32.0, -47.0 }, Below(topOnly));

            List<GeoPoint2> bottomOnly = Label(leader, null, 5.0).GetPlacePoints(Rows());
            Assert.Equal(new[] { 17.0, 32.0, 47.0 }, Above(bottomOnly));
            Assert.Equal(new[] { -10.0, -25.0, -40.0 }, Below(bottomOnly));
        }

        [Fact]
        public void ALeaderDrawnTheOtherWay_KeepsItsTopAbove()
        {
            List<GeoPoint2> points = Label(new GeoLine2(100.0, 0.0, 0.0, 0.0), 30.0, 5.0).GetPlacePoints(Rows());

            Assert.Equal(new[] { 35.0, 50.0, 65.0 }, Above(points));
            Assert.Equal(new[] { -10.0, -25.0, -40.0 }, Below(points));

            // The side tried first follows the leader, as it always has: its left, here the bottom.
            Assert.True(points[0].IsEqualTo(new GeoPoint2(50.0, -10.0)));
            Assert.True(points[1].IsEqualTo(new GeoPoint2(50.0, 35.0)));
        }

        [Theory]
        [InlineData(0.0, 0.0, 0.0, 100.0)]
        [InlineData(0.0, 100.0, 0.0, 0.0)]
        [InlineData(0.0, 0.0, 1e-12, 100.0)]
        [InlineData(1e-12, 100.0, 0.0, 0.0)]
        public void AVerticalLeader_HasItsTopOnTheLeft(double x0, double y0, double x1, double y1)
        {
            List<GeoPoint2> points = Label(new GeoLine2(x0, y0, x1, y1), 30.0, 5.0).GetPlacePoints(Rows());

            // Across a vertical leader the label is 20 wide: 10 plus the gap of the side, rows 20 + 5 apart.
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
        public void ASlantedLeader_HasItsTopOnTheSideFacingUp(double x0, double y0, double x1, double y1)
        {
            List<GeoPoint2> points = Label(new GeoLine2(x0, y0, x1, y1), 30.0, 5.0).GetPlacePoints(Rows());

            // How far each of the first pair stands off the leader, counted up: the normal of the leader facing up.
            double length = Math.Sqrt((x1 - x0) * (x1 - x0) + (y1 - y0) * (y1 - y0));
            double upX = -(y1 - y0) / length, upY = (x1 - x0) / length;
            if (upY < 0.0)
            {
                upX = -upX;
                upY = -upY;
            }

            double[] pair = points.Take(2).Select(p => (p.X - 50.0) * upX + (p.Y - 50.0) * upY).ToArray();
            double top = pair.Max(), bottom = pair.Min();

            Assert.True(top > 0.0 && bottom < 0.0);

            // Half the label's height across the leader, whatever the slant, once the gap of each side is taken away.
            Assert.Equal(top - 30.0, -bottom - 5.0, 9);
        }

        /// <summary>
        /// Every place of the label crosses a block line, so it is placed in the second pass, which runs on a copy of the
        /// item with the lines lifted: the copy has to carry the gap of each side, or the label comes back 5 + 50 off.
        /// </summary>
        [Fact]
        public void TheSecondPass_KeepsTheGapOfEachSide()
        {
            var lines = new List<GeoLine2>();
            for (int x = -400; x <= 800; x += 7)
            {
                lines.Add(new GeoLine2(x, -600.0, x, 800.0));
            }

            ArrangeItem label = Label(new GeoLine2(0.0, 0.0, 100.0, 0.0), 30.0, 5.0, offset: 50.0);
            label.BlockLines = lines;

            ArrangeResult result = Arranger.Run(new[] { label }, Rows())[0];

            Assert.False(result.Placed);
            Assert.Equal(0.0, result.Translation.X, 9);
            Assert.Equal(35.0, result.Translation.Y, 9);
        }

        [Theory]
        [MemberData(nameof(AllAlgorithms))]
        public void EveryAlgorithm_PlacesTheLabelOnARowOfItsSide(ArrangeAlgorithmType algorithm)
        {
            ArrangeItem label = Label(new GeoLine2(0.0, 0.0, 100.0, 0.0), 30.0, 5.0);

            ArrangeResult result = Arranger.Run(new[] { label }, Rows(algorithm))[0];
            GeoPoint2 centre = label.Box.Center + result.Translation;

            Assert.True(result.Placed);
            Assert.Contains(label.GetPlacePoints(Rows(algorithm)), p => p.IsEqualTo(centre));
            Assert.True(centre.Y >= 35.0 - 1e-9 || centre.Y <= -10.0 + 1e-9, $"{algorithm}: {centre}");
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

            ArrangeResult result = Arranger.Run(new[] { label }, Rows())[0];

            // The rows above stand 305, 320 and 335 off; the first two run into the obstacle.
            Assert.True(result.Placed);
            Assert.Equal(335.0, (label.Box.Center + result.Translation).Y, 9);
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
