using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Geometry;
using GeometryHelper.Packing;
using Xunit;

namespace GeometryHelper.UnitTest.Packing
{
    /// <summary>
    /// <see cref="SheetPacker"/>: groups of boxes onto sheets, each group as one block, the sheets filled from the
    /// upper left and a new one begun beside the last when it is full.
    /// </summary>
    public class SheetPackerTests
    {
        // A box of a size, somewhere off the sheets, as a view is before it is packed.
        private static GeoRectangle2 Box(double width, double height, double angle = 0.0)
            => new GeoRectangle2(new GeoPoint2(-5000.0 + width, 3000.0 - height), width, height, angle);

        private static List<GeoRectangle2[]> OneEach(params GeoRectangle2[] boxes) => boxes.Select(b => new[] { b }).ToList();

        [Fact]
        public void FourA4Boxes_FillAnA2SheetExactly_RowByRowFromTheUpperLeft()
        {
            List<GeoRectangle2[]> groups = OneEach(Box(297, 210), Box(297, 210), Box(297, 210), Box(297, 210));

            PackResult result = SheetPacker.Pack(groups, new Sheet(PaperSize.A2));

            GeoPoint2[] corners = result.Placements.Select(g => g[0].ViewBox.LowerLeft).ToArray();
            Assert.True(corners[0].IsEqualTo(new GeoPoint2(0, 210)));
            Assert.True(corners[1].IsEqualTo(new GeoPoint2(297, 210)));
            Assert.True(corners[2].IsEqualTo(new GeoPoint2(0, 0)));
            Assert.True(corners[3].IsEqualTo(new GeoPoint2(297, 0)));
            Assert.All(result.Placements, g => Assert.Equal(0, g[0].SheetIndex));
            Assert.Single(result.Sheets);
            Assert.Equal(1.0, result.Utilization, 12);
        }

        /// <summary>
        /// A box is only moved: what comes back is the box as given, moved by the translation, the same size and turn.
        /// </summary>
        [Fact]
        public void TheViewBox_IsTheBoxMovedByTheTranslation()
        {
            GeoRectangle2[] boxes = { Box(120, 80), Box(60, 40, 0.5), Box(30, 90) };
            PackResult result = SheetPacker.Pack(new List<GeoRectangle2[]> { boxes }, new Sheet(PaperSize.A3));

            for (int i = 0; i < boxes.Length; i++)
            {
                PackPlacement placement = result.Placements[0][i];
                Assert.True(placement.Placed);
                Assert.Equal(boxes[i].Translate(placement.Translation), placement.ViewBox);
                Assert.Equal(boxes[i].Width, placement.ViewBox.Width);
                Assert.Equal(boxes[i].Height, placement.ViewBox.Height);
                Assert.Equal(boxes[i].AngleRad, placement.ViewBox.AngleRad);
            }
        }

        [Fact]
        public void TheFirstBox_GoesToTheUpperLeftOfTheUsableArea()
        {
            var sheet = new Sheet(PaperSize.A3) { Scale = 10.0, OffsetLeft = 20, OffsetRight = 10, OffsetTop = 10, OffsetBottom = 10, Origin = new GeoPoint2(500, 500) };

            PackResult result = SheetPacker.Pack(OneEach(Box(1000, 500)), sheet);

            GeoRectangle2 usable = result.Sheets[0].UsableArea;
            Assert.True(result.Placements[0][0].ViewBox.UpperLeft.IsEqualTo(usable.UpperLeft), result.Placements[0][0].ViewBox.UpperLeft.ToString());
            Assert.True(usable.LowerLeft.IsEqualTo(new GeoPoint2(500 + 200, 500 + 100)));
        }

        /// <summary>
        /// Boxes too large to share a sheet take one each, the sheets one after another on the side asked for.
        /// </summary>
        [Theory]
        [InlineData(SheetDirection.Right, 1.0, 0.0)]
        [InlineData(SheetDirection.Left, -1.0, 0.0)]
        [InlineData(SheetDirection.Top, 0.0, 1.0)]
        [InlineData(SheetDirection.Bottom, 0.0, -1.0)]
        public void WhenASheetIsFull_TheNextGoesBesideIt(SheetDirection direction, double alongX, double alongY)
        {
            var sheet = new Sheet(PaperSize.A4) { NewSheet = direction, SheetSpacing = 50.0, OffsetLeft = 10, OffsetRight = 10, OffsetTop = 10, OffsetBottom = 10 };

            PackResult result = SheetPacker.Pack(OneEach(Box(200, 150), Box(200, 150), Box(200, 150)), sheet);

            Assert.Equal(3, result.Sheets.Count);
            for (int k = 0; k < 3; k++)
            {
                PackPlacement placement = result.Placements[k][0];
                Assert.Equal(k, placement.SheetIndex);
                Assert.Equal(sheet.GetFrame(k), result.Sheets[k]);

                // The step from one sheet to the next is its size plus the spacing, across or up.
                GeoPoint2 corner = result.Sheets[k].Bounds.LowerLeft;
                Assert.True(corner.IsEqualTo(new GeoPoint2(k * alongX * 347.0, k * alongY * 260.0)), corner.ToString());

                GeoRectangle2 usable = result.Sheets[k].UsableArea;
                Assert.True(placement.ViewBox.UpperLeft.IsEqualTo(usable.UpperLeft));
            }
        }

        /// <summary>
        /// The boxes of a group stay together as one block, the spacing apart; the groups stand the group spacing apart.
        /// </summary>
        [Fact]
        public void TheBoxesOfAGroup_StayTogether()
        {
            var groups = new List<GeoRectangle2[]>
            {
                new[] { Box(100, 60), Box(30, 15), Box(30, 15) },
                new[] { Box(80, 40), Box(80, 40) },
            };
            var options = new PackOptions { Spacing = 2.0, GroupSpacing = 20.0 };

            PackResult result = SheetPacker.Pack(groups, new Sheet(PaperSize.A3), options);

            // The box round each group.
            (double MinX, double MinY, double MaxX, double MaxY) Round(IReadOnlyList<PackPlacement> group)
                => (group.Min(p => p.ViewBox.LowerLeft.X), group.Min(p => p.ViewBox.LowerLeft.Y), group.Max(p => p.ViewBox.UpperRight.X), group.Max(p => p.ViewBox.UpperRight.Y));

            var first = Round(result.Placements[0]);
            var second = Round(result.Placements[1]);
            bool apart = first.MaxX + 20.0 <= second.MinX + 1e-9 || second.MaxX + 20.0 <= first.MinX + 1e-9
                || first.MaxY + 20.0 <= second.MinY + 1e-9 || second.MaxY + 20.0 <= first.MinY + 1e-9;
            Assert.True(apart, $"{first} and {second}");
            Assert.All(result.Placements.SelectMany(g => g), p => Assert.Equal(0, p.SheetIndex));
        }

        [Fact]
        public void AGroupTooLargeForASheet_IsSplitAcrossSheets_InItsOrder()
        {
            GeoRectangle2[] boxes = Enumerable.Range(0, 10).Select(i => Box(40, 40)).ToArray();

            PackResult result = SheetPacker.Pack(new List<GeoRectangle2[]> { boxes }, Sheet.Custom(100, 100));

            Assert.Equal(new[] { 0, 0, 0, 0, 1, 1, 1, 1, 2, 2 }, result.Placements[0].Select(p => p.SheetIndex));
            Assert.Equal(3, result.Sheets.Count);
        }

        [Fact]
        public void ABoxLargerThanTheSheet_IsNotPlaced_AndTheOthersAre()
        {
            GeoRectangle2 huge = Box(1000, 10);
            var groups = new List<GeoRectangle2[]> { new[] { Box(50, 50), huge, Box(50, 50) } };

            PackResult result = SheetPacker.Pack(groups, new Sheet(PaperSize.A4));

            PackPlacement left = result.Placements[0][1];
            Assert.False(left.Placed);
            Assert.Equal(-1, left.SheetIndex);
            Assert.Equal(GeoVector2.Zero, left.Translation);
            Assert.Equal(huge, left.ViewBox);
            Assert.True(result.Placements[0][0].Placed);
            Assert.True(result.Placements[0][2].Placed);
        }

        [Fact]
        public void ABoxAtCoordinatesThatAreNotNumbers_IsNotPlaced()
        {
            var lost = new GeoRectangle2(new GeoPoint2(double.NaN, 0.0), 10.0, 10.0);

            PackResult result = SheetPacker.Pack(OneEach(lost, Box(10, 10)), new Sheet(PaperSize.A4));

            Assert.False(result.Placements[0][0].Placed);
            Assert.True(result.Placements[1][0].Placed);
        }

        /// <summary>
        /// Kept, the boxes of a group stand to one another exactly as given, overlapping or not.
        /// </summary>
        [Fact]
        public void AKeptGroup_KeepsItsBoxesAsTheyStand()
        {
            var view = new GeoRectangle2(-800, 400, 120, 80);
            var section = new GeoRectangle2(-660, 400, 40, 80);
            var overlapping = new GeoRectangle2(-700, 450, 60, 50);
            var groups = new List<GeoRectangle2[]> { new[] { view, section, overlapping } };

            PackResult result = SheetPacker.Pack(groups, new Sheet(PaperSize.A3), new PackOptions { GroupLayout = GroupLayout.Keep });

            IReadOnlyList<PackPlacement> placed = result.Placements[0];
            Assert.All(placed, p => Assert.True(p.Placed));
            Assert.True(placed[0].Translation.IsEqualTo(placed[1].Translation));
            Assert.True(placed[0].Translation.IsEqualTo(placed[2].Translation));
        }

        /// <summary>
        /// A box that fits under the first sheet's box, after one that did not: in order it goes on the last sheet,
        /// filling earlier sheets it goes back to the first.
        /// </summary>
        [Fact]
        public void EarlierSheetsAreFilled_OnlyWhenAskedFor()
        {
            List<GeoRectangle2[]> groups = OneEach(Box(100, 60), Box(100, 60), Box(100, 30));
            Sheet sheet = Sheet.Custom(100, 100);

            PackResult inOrder = SheetPacker.Pack(groups, sheet);
            PackResult filling = SheetPacker.Pack(groups, sheet, new PackOptions { FillEarlierSheets = true });

            Assert.Equal(new[] { 0, 1, 1 }, inOrder.Placements.Select(g => g[0].SheetIndex));
            Assert.Equal(new[] { 0, 1, 0 }, filling.Placements.Select(g => g[0].SheetIndex));
        }

        [Fact]
        public void LargestGroupsFirst_PutsTheLargestAtTheUpperLeft()
        {
            List<GeoRectangle2[]> groups = OneEach(Box(30, 20), Box(200, 100));
            var sheet = new Sheet(PaperSize.A3);

            PackResult inOrder = SheetPacker.Pack(groups, sheet);
            PackResult largestFirst = SheetPacker.Pack(groups, sheet, new PackOptions { LargestGroupsFirst = true });

            GeoPoint2 upperLeft = sheet.GetFrame(0).UsableArea.UpperLeft;
            Assert.True(inOrder.Placements[0][0].ViewBox.UpperLeft.IsEqualTo(upperLeft));
            Assert.True(largestFirst.Placements[1][0].ViewBox.UpperLeft.IsEqualTo(upperLeft));
        }

        /// <summary>
        /// A turned box keeps its turn, and is packed by the upright box round it, which stays inside the sheet.
        /// </summary>
        [Fact]
        public void ATurnedBox_KeepsItsTurn_TheBoxRoundItInside()
        {
            GeoRectangle2 turned = Box(200, 100, Math.PI / 6.0);
            Sheet sheet = Sheet.Custom(300, 300);

            PackPlacement placement = SheetPacker.Pack(OneEach(turned), sheet).Placements[0][0];

            Assert.True(placement.Placed);
            Assert.Equal(Math.PI / 6.0, placement.ViewBox.AngleRad);
            Assert.All(placement.ViewBox.GetVertices(), v => Assert.True(v.X >= -1e-9 && v.Y >= -1e-9 && v.X <= 300 + 1e-9 && v.Y <= 300 + 1e-9, v.ToString()));
            Assert.Equal(300.0, placement.ViewBox.GetVertices().Max(v => v.Y), 9);
        }

        [Fact]
        public void TheScale_TurnsTheSheetIntoTheUnitsOfTheBoxes()
        {
            var sheet = new Sheet(PaperSize.A1) { Scale = 50.0, OffsetLeft = 20, OffsetRight = 10, OffsetTop = 10, OffsetBottom = 10 };

            // Four views 10 m by 7 m of the model, at 1:50 200 by 140 mm of paper: four across an A1 fit, less its offsets.
            PackResult result = SheetPacker.Pack(OneEach(Box(10000, 7000), Box(10000, 7000), Box(10000, 7000), Box(10000, 7000)), sheet);

            Assert.All(result.Placements, g => Assert.Equal(0, g[0].SheetIndex));
            Assert.True(result.Placements[3][0].ViewBox.UpperRight.X <= (841 - 10) * 50.0 + 1e-6);
        }

        [Fact]
        public void NullAndEmptyGroups_HaveNoPlacements()
        {
            var groups = new List<GeoRectangle2[]> { null, new GeoRectangle2[0], new[] { Box(10, 10) } };

            PackResult result = SheetPacker.Pack(groups, new Sheet(PaperSize.A4));

            Assert.Equal(3, result.Placements.Count);
            Assert.Empty(result.Placements[0]);
            Assert.Empty(result.Placements[1]);
            Assert.True(result.Placements[2][0].Placed);
        }

        [Fact]
        public void NothingToPack_TakesTheFirstSheet()
        {
            PackResult result = SheetPacker.Pack(new List<GeoRectangle2[]>(), new Sheet(PaperSize.A4));

            Assert.Empty(result.Placements);
            Assert.Equal(new[] { new Sheet(PaperSize.A4).GetFrame(0) }, result.Sheets);
            Assert.Equal(0.0, result.Utilization);
        }

        [Fact]
        public void TheArguments_AreChecked()
        {
            var groups = new List<GeoRectangle2[]>();
            var sheet = new Sheet(PaperSize.A4);

            Assert.Throws<ArgumentNullException>(() => SheetPacker.Pack(null, sheet));
            Assert.Throws<ArgumentNullException>(() => SheetPacker.Pack(groups, null));
            Assert.Throws<ArgumentNullException>(() => SheetPacker.Pack(groups, sheet, null));
            Assert.Throws<ArgumentException>(() => SheetPacker.Pack(groups, new Sheet(PaperSize.A4) { OffsetTop = 200, OffsetBottom = 10 }));

            var options = new PackOptions();
            Assert.Throws<ArgumentOutOfRangeException>(() => options.Spacing = -1.0);
            Assert.Throws<ArgumentOutOfRangeException>(() => options.GroupSpacing = double.NaN);
            Assert.Throws<ArgumentOutOfRangeException>(() => options.GroupLayout = (GroupLayout)4);
        }

        [Fact]
        public void TheSameBoxes_GiveTheSameResult_AndAreOnlyRead()
        {
            var random = new Random(11);
            var groups = Enumerable.Range(0, 30)
                .Select(g => Enumerable.Range(0, 1 + random.Next(4)).Select(i => Box(10 + random.Next(120), 10 + random.Next(80))).ToArray())
                .ToList();
            var copy = groups.Select(g => g.ToArray()).ToList();
            var sheet = new Sheet(PaperSize.A3) { OffsetLeft = 20, OffsetRight = 10, OffsetTop = 10, OffsetBottom = 10 };
            var options = new PackOptions { Spacing = 3, GroupSpacing = 8 };

            PackResult first = SheetPacker.Pack(groups, sheet, options);
            PackResult second = SheetPacker.Pack(groups, sheet, options);

            Assert.Equal(first.Placements.SelectMany(g => g), second.Placements.SelectMany(g => g));
            Assert.Equal(first.Sheets, second.Sheets);
            Assert.Equal(copy.SelectMany(g => g), groups.SelectMany(g => g));
        }
    }
}
