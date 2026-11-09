using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Arranging;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Arranging
{
    /// <summary>
    /// <see cref="ArrangeItem.Rectangle"/>: the label stands along the long side of the rectangle of the object, the
    /// gaps measured from its long edges. The label is 100 by 40 unless said, so across a long side it stands half of 40
    /// plus the gap off the edge, and each next row a label across and the row gap further.
    /// </summary>
    public class ArrangeAlongARectangleTests : ArrangeTestKit
    {
        private static readonly ArrangeOptions Options = new ArrangeOptions { RowGap = 10.0, PerpendicularLevels = 2, Tolerance = Tolerance.Default };

        private static ArrangeItem Label(GeoRectangle2? rectangle, double offset = 50.0, double width = 100.0, double height = 40.0)
            => new ArrangeItem
            {
                Rectangle = rectangle,
                Box = new GeoRectangle2(rectangle.HasValue ? rectangle.Value.Center : new GeoPoint2(0.0, 0.0), width, height),
                Offset = offset,
            };

        private static double[] Rows(IEnumerable<GeoPoint2> points, Func<GeoPoint2, double> across)
            => points.Select(p => Math.Round(across(p), 6)).Distinct().OrderBy(y => y).ToArray();

        /// <summary>
        /// A rectangle 2 000 by 300 and a label 100 by 40 with the gap 50: the first rows stand 150 + 50 + 20 off the middle
        /// line, 70 off the edge, one above and one below, and the next 40 + 10 further.
        /// </summary>
        [Fact]
        public void ALongRectangleWithItsLongSideLevel_HasTheRowsOfTheLabelOffItsTwoEdges()
        {
            ArrangeItem label = Label(new GeoRectangle2(new GeoPoint2(1000.0, 500.0), 2000.0, 300.0));

            double[] rows = Rows(label.GetPlacePoints(Options), p => p.Y - 500.0);

            Assert.Equal(new[] { -270.0, -220.0, 220.0, 270.0 }, rows);
        }

        /// <summary>
        /// The same places as a leader along the middle of the rectangle with the half of its short side added to the gap:
        /// a rectangle is the convenience, and it asks the same of the placing.
        /// </summary>
        [Fact]
        public void ARectangle_GivesTheCandidatesOfItsMiddleLineWithTheHalfOfItsWidthAddedToTheGap()
        {
            var rectangle = new GeoRectangle2(new GeoPoint2(1000.0, 500.0), 2000.0, 300.0);
            ArrangeItem withRectangle = Label(rectangle);
            var withLeader = new ArrangeItem
            {
                Leader = new GeoLine2(0.0, 500.0, 2000.0, 500.0),
                Box = withRectangle.Box,
                Offset = 50.0 + 150.0,
            };

            Assert.Equal(withLeader.GetPlacePoints(Options), withRectangle.GetPlacePoints(Options));
        }

        /// <summary>The gaps of the two long edges are their own: 20 off the top edge and 80 off the bottom one.</summary>
        [Fact]
        public void TheTopAndTheBottomGapsAreMeasuredFromTheirOwnLongEdges()
        {
            ArrangeItem label = Label(new GeoRectangle2(new GeoPoint2(0.0, 0.0), 2000.0, 300.0));
            label.OffsetTop = 20.0;
            label.OffsetBottom = 80.0;

            double[] rows = Rows(label.GetPlacePoints(Options), p => p.Y);

            Assert.Equal(new[] { -300.0, -250.0, 190.0, 240.0 }, rows);
        }

        /// <summary>The side keeps the label off the other edge.</summary>
        [Fact]
        public void AnArrangeSideKeepsTheLabelToOneLongEdge()
        {
            ArrangeItem label = Label(new GeoRectangle2(new GeoPoint2(0.0, 0.0), 2000.0, 300.0));
            label.Side = ArrangeSide.Bottom;

            Assert.All(label.GetPlacePoints(Options), p => Assert.True(p.Y < 0.0));
        }

        /// <summary>The long side of a rectangle that stands upright is the height: the rows are to its left and right.</summary>
        [Fact]
        public void ARectangleWhoseHeightIsGreater_HasItsRowsOnItsLeftAndItsRight()
        {
            ArrangeItem label = Label(new GeoRectangle2(new GeoPoint2(0.0, 0.0), 300.0, 2000.0));

            // The label is 100 wide across an upright side: 150 + 50 + 50 off the middle line.
            double[] rows = Rows(label.GetPlacePoints(Options), p => p.X);

            Assert.Equal(new[] { -360.0, -250.0, 250.0, 360.0 }, rows);
        }

        /// <summary>
        /// A rectangle turned 30 degrees: the places stand along the long side, off it by the half of its width, the gap
        /// and the half of what the label measures across.
        /// </summary>
        [Fact]
        public void ATurnedRectangle_HasItsRowsParallelToItsLongSideAtTheGapOffItsEdges()
        {
            double angle = Math.PI / 6.0;
            var rectangle = new GeoRectangle2(new GeoPoint2(300.0, 200.0), 1600.0, 200.0, angle);
            ArrangeItem label = Label(rectangle);
            var direction = new GeoVector2(Math.Cos(angle), Math.Sin(angle));
            var perpendicular = new GeoVector2(-direction.Y, direction.X);
            // The label is 100 by 40 and not turned: across the side it measures 100 sine + 40 cosine.
            double labelAcross = 100.0 * Math.Abs(Math.Sin(angle)) + 40.0 * Math.Abs(Math.Cos(angle));
            double expected = 100.0 + 50.0 + labelAcross * 0.5;

            List<GeoPoint2> points = label.GetPlacePoints(Options);
            double[] across = points.Select(p => new GeoVector2(p.X - 300.0, p.Y - 200.0).DotProduct(perpendicular)).Select(a => Math.Round(Math.Abs(a), 6)).Distinct().OrderBy(a => a).ToArray();

            Assert.Equal(expected, across[0], 6);
            Assert.Equal(expected + labelAcross + 10.0, across[1], 6);
        }

        /// <summary>A rectangle with no long side cannot be arranged along: no candidate.</summary>
        [Fact]
        public void ARectangleOfNoLength_HasNoPlaces()
        {
            ArrangeItem label = Label(new GeoRectangle2(new GeoPoint2(0.0, 0.0), 0.0, 0.0));

            Assert.Empty(label.GetPlacePoints(Options));
        }

        /// <summary>With both, the rectangle is used and the leader is ignored.</summary>
        [Fact]
        public void WithARectangleAndALeader_TheRectangleIsUsed()
        {
            ArrangeItem label = Label(new GeoRectangle2(new GeoPoint2(0.0, 0.0), 2000.0, 300.0));
            label.Leader = new GeoLine2(5000.0, 5000.0, 5000.0, 9000.0);

            Assert.Equal(Rows(Label(label.Rectangle).GetPlacePoints(Options), p => p.Y), Rows(label.GetPlacePoints(Options), p => p.Y));
            Assert.All(label.GetPlacePoints(Options), p => Assert.True(Math.Abs(p.X) <= 2000.0));
        }

        /// <summary>A run puts a label along the long side and clear of the rectangle, and a second one beside it.</summary>
        [Fact]
        public void ARun_PutsTwoLabelsAlongTheLongSideClearOfEachOtherAndOfTheRectangle()
        {
            var rectangle = new GeoRectangle2(new GeoPoint2(0.0, 0.0), 2000.0, 300.0);
            var items = new List<ArrangeItem> { Label(rectangle), Label(rectangle) };

            ArrangeResult[] results = Arranger.Run(items, Options);

            Assert.All(results, r => Assert.True(r.Placed));
            for (int i = 0; i < items.Count; i++)
            {
                GeoRectangle2 box = MovedBox(items[i], results[i].Translation);
                Assert.True(Math.Abs(box.Center.Y) >= 150.0 + 50.0 + 20.0 - 1E-6);
            }

            GeoRectangle2 first = MovedBox(items[0], results[0].Translation);
            GeoRectangle2 second = MovedBox(items[1], results[1].Translation);
            Assert.False(first.CollidesWith(second, Tolerance.Default));
        }

        /// <summary>A label that is not given a rectangle keeps to its leader as before.</summary>
        [Fact]
        public void ALabelWithoutARectangle_KeepsToItsLeader()
        {
            var label = new ArrangeItem
            {
                Leader = new GeoLine2(0.0, 0.0, 2000.0, 0.0),
                Box = new GeoRectangle2(new GeoPoint2(1000.0, 0.0), 100.0, 40.0),
                Offset = 50.0,
            };

            Assert.Null(label.Rectangle);
            Assert.Equal(new[] { -120.0, -70.0, 70.0, 120.0 }, Rows(label.GetPlacePoints(Options), p => p.Y));
        }
    }
}
