using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Geometry;
using GeometryHelper.Packing;
using Xunit;

namespace GeometryHelper.UnitTest.Packing
{
    /// <summary>
    /// <see cref="PackOptions.StartCorner"/>: the packing starts from any corner of the usable area, and comes out as the
    /// mirror image of the packing from the upper left, the same boxes on the same sheets.
    /// </summary>
    public class SheetPackerStartCornerTests
    {
        private static readonly Tolerance Tol = Tolerance.Default;

        private static readonly SheetCorner[] Corners = { SheetCorner.UpperLeft, SheetCorner.UpperRight, SheetCorner.LowerLeft, SheetCorner.LowerRight };

        // A box of a size, somewhere off the sheets, as a view is before it is packed.
        private static GeoRectangle2 Box(double width, double height)
            => new GeoRectangle2(new GeoPoint2(-5000.0 + width, 3000.0 - height), width, height, 0.0);

        private static List<GeoRectangle2[]> OneEach(params GeoRectangle2[] boxes) => boxes.Select(b => new[] { b }).ToList();

        // An A4 sheet, landscape, its usable area 267 by 190 from (20, 10).
        private static Sheet A4() => new Sheet(PaperSize.A4) { OffsetLeft = 20, OffsetRight = 10, OffsetTop = 10, OffsetBottom = 10 };

        private static PackOptions From(SheetCorner corner) => new PackOptions { StartCorner = corner, Tolerance = Tol };

        private static GeoPoint2 CornerOf(GeoRectangle2 box, SheetCorner corner)
        {
            switch (corner)
            {
                case SheetCorner.UpperLeft: return box.UpperLeft;
                case SheetCorner.UpperRight: return box.UpperRight;
                case SheetCorner.LowerLeft: return box.LowerLeft;
                default: return box.LowerRight;
            }
        }

        [Fact]
        public void TheStartCorner_IsTheUpperLeftUnlessSet()
        {
            Assert.Equal(SheetCorner.UpperLeft, new PackOptions().StartCorner);
            Assert.Equal(SheetCorner.UpperLeft, PackOptions.Default.StartCorner);
        }

        [Theory]
        [InlineData(SheetCorner.Center)]
        [InlineData((SheetCorner)9)]
        public void TheMiddle_OrNoCornerAtAll_IsRefused(SheetCorner corner)
        {
            var options = new PackOptions();

            Assert.Throws<ArgumentOutOfRangeException>(() => options.StartCorner = corner);
            Assert.Equal(SheetCorner.UpperLeft, options.StartCorner);
        }

        /// <summary>
        /// Three boxes 100 by 50 on a usable area 267 wide: two fill a row from the start corner, the third begins the
        /// next row, away from it.
        /// </summary>
        [Theory]
        [InlineData(SheetCorner.UpperLeft, 20, 150, 120, 150, 20, 100)]
        [InlineData(SheetCorner.UpperRight, 187, 150, 87, 150, 187, 100)]
        [InlineData(SheetCorner.LowerLeft, 20, 10, 120, 10, 20, 60)]
        [InlineData(SheetCorner.LowerRight, 187, 10, 87, 10, 187, 60)]
        public void ThreeBoxes_FillTheirRowsFromTheStartCorner(SheetCorner corner, double x0, double y0, double x1, double y1, double x2, double y2)
        {
            PackResult result = SheetPacker.Pack(OneEach(Box(100, 50), Box(100, 50), Box(100, 50)), A4(), From(corner));

            GeoPoint2[] lowerLefts = result.Placements.Select(g => g[0].ViewBox.LowerLeft).ToArray();
            Assert.True(lowerLefts[0].IsEqualTo(new GeoPoint2(x0, y0), Tol), lowerLefts[0].ToString());
            Assert.True(lowerLefts[1].IsEqualTo(new GeoPoint2(x1, y1), Tol), lowerLefts[1].ToString());
            Assert.True(lowerLefts[2].IsEqualTo(new GeoPoint2(x2, y2), Tol), lowerLefts[2].ToString());
            Assert.Single(result.Sheets);
        }

        [Theory]
        [InlineData(SheetCorner.UpperLeft)]
        [InlineData(SheetCorner.UpperRight)]
        [InlineData(SheetCorner.LowerLeft)]
        [InlineData(SheetCorner.LowerRight)]
        public void TheFirstBox_GoesToTheStartCornerOfTheUsableArea(SheetCorner corner)
        {
            var sheet = new Sheet(PaperSize.A3) { Scale = 10.0, OffsetLeft = 20, OffsetRight = 10, OffsetTop = 10, OffsetBottom = 10, Origin = new GeoPoint2(500, 500) };

            PackResult result = SheetPacker.Pack(OneEach(Box(1000, 500)), sheet, From(corner));

            GeoPoint2 placed = CornerOf(result.Placements[0][0].ViewBox, corner);
            Assert.True(placed.IsEqualTo(CornerOf(result.Sheets[0].UsableArea, corner), Tol), placed.ToString());
        }

        /// <summary>
        /// A group laid out afresh puts its first box, the main view, at the start corner of its block, and so of the sheet.
        /// </summary>
        [Theory]
        [InlineData(SheetCorner.UpperLeft)]
        [InlineData(SheetCorner.UpperRight)]
        [InlineData(SheetCorner.LowerLeft)]
        [InlineData(SheetCorner.LowerRight)]
        public void ACompactGroup_PutsItsMainViewAtTheStartCorner(SheetCorner corner)
        {
            var groups = new List<GeoRectangle2[]> { new[] { Box(200, 100), Box(50, 40), Box(50, 40), Box(60, 30) } };
            PackOptions options = From(corner);
            options.Spacing = 5;

            PackResult result = SheetPacker.Pack(groups, A4(), options);

            Assert.All(result.Placements[0], p => Assert.True(p.Placed));
            GeoPoint2 main = CornerOf(result.Placements[0][0].ViewBox, corner);
            Assert.True(main.IsEqualTo(CornerOf(result.Sheets[0].UsableArea, corner), Tol), main.ToString());
        }

        /// <summary>
        /// A group kept as it was drawn moves as one, its boxes as they stood, and its block goes to the start corner.
        /// </summary>
        [Theory]
        [InlineData(SheetCorner.UpperLeft)]
        [InlineData(SheetCorner.UpperRight)]
        [InlineData(SheetCorner.LowerLeft)]
        [InlineData(SheetCorner.LowerRight)]
        public void AKeptGroup_MovesAsOne_ToTheStartCorner(SheetCorner corner)
        {
            var drawn = new[]
            {
                new GeoRectangle2(new GeoPoint2(1000, 1000), 120, 80, 0.0),
                new GeoRectangle2(new GeoPoint2(1150, 1020), 40, 30, 0.0),
                new GeoRectangle2(new GeoPoint2(1010, 900), 60, 50, 0.0),
            };
            PackOptions options = From(corner);
            options.GroupLayout = GroupLayout.Keep;

            PackResult result = SheetPacker.Pack(new List<GeoRectangle2[]> { drawn }, A4(), options);

            GeoVector2 move = result.Placements[0][0].Translation;
            Assert.All(result.Placements[0], p => Assert.True(p.Placed && p.Translation.IsEqualTo(move, Tol), p.ToString()));

            // The block runs from (940, 875) to (1170, 1040), its middle (1055, 957.5).
            var block = new GeoRectangle2(new GeoPoint2(1055, 957.5) + move, 230, 165, 0.0);
            Assert.True(CornerOf(block, corner).IsEqualTo(CornerOf(result.Sheets[0].UsableArea, corner), Tol), block.ToString());
        }

        public static IEnumerable<object[]> Seeds() => Enumerable.Range(1, 60).Select(seed => new object[] { seed });

        private static (List<GeoRectangle2[]> Groups, Sheet Sheet, PackOptions Options) Scene(int seed)
        {
            var random = new Random(seed);
            PaperSize[] sizes = { PaperSize.A0, PaperSize.A1, PaperSize.A2, PaperSize.A3, PaperSize.A4 };
            var sheet = new Sheet(sizes[random.Next(sizes.Length)], random.Next(3) == 0 ? SheetOrientation.Portrait : SheetOrientation.Landscape)
            {
                Scale = random.Next(3) == 0 ? 1.0 : new[] { 10.0, 20.0, 25.0, 50.0 }[random.Next(4)],
                OffsetLeft = random.Next(25),
                OffsetRight = random.Next(15),
                OffsetTop = random.Next(15),
                OffsetBottom = random.Next(15),
                NewSheet = (SheetDirection)random.Next(4),
                SheetSpacing = random.Next(3) * 25.0,
                Origin = new GeoPoint2(random.Next(-5000, 5000), random.Next(-5000, 5000)),
            };

            double usableWidth = (sheet.PaperWidth - sheet.OffsetLeft - sheet.OffsetRight) * sheet.Scale;
            double usableHeight = (sheet.PaperHeight - sheet.OffsetTop - sheet.OffsetBottom) * sheet.Scale;
            var groups = new List<GeoRectangle2[]>();
            int count = 5 + random.Next(40);
            for (int g = 0; g < count; g++)
            {
                int members = random.Next(8) == 0 ? 0 : 1 + random.Next(random.Next(4) == 0 ? 12 : 4);
                groups.Add(Enumerable.Range(0, members).Select(i =>
                {
                    double big = random.Next(15) == 0 ? 1.1 : 0.45;
                    double w = usableWidth * (0.02 + random.NextDouble() * big), h = usableHeight * (0.02 + random.NextDouble() * big);
                    double angle = random.Next(6) == 0 ? random.NextDouble() * Math.PI : 0.0;
                    return new GeoRectangle2(new GeoPoint2(random.Next(-100000, 100000), random.Next(-100000, 100000)), w, h, angle);
                }).ToArray());
            }

            var options = new PackOptions
            {
                Spacing = random.Next(3) * 2.5,
                GroupSpacing = random.Next(4) * 5.0,
                GroupLayout = random.Next(4) == 0 ? GroupLayout.Keep : GroupLayout.Compact,
                LargestGroupsFirst = random.Next(2) == 0,
                FillEarlierSheets = random.Next(2) == 0,
                Tolerance = Tol,
            };
            return (groups, sheet, options);
        }

        // The upright box round a rectangle.
        private static (double MinX, double MinY, double MaxX, double MaxY) Round(GeoRectangle2 box)
        {
            GeoPoint2[] v = box.GetVertices();
            return (v.Min(p => p.X), v.Min(p => p.Y), v.Max(p => p.X), v.Max(p => p.Y));
        }

        // The upright box round some rectangles.
        private static (double MinX, double MinY, double MaxX, double MaxY) Union(IEnumerable<GeoRectangle2> boxes)
        {
            var rounds = boxes.Select(Round).ToArray();
            return (rounds.Min(r => r.MinX), rounds.Min(r => r.MinY), rounds.Max(r => r.MaxX), rounds.Max(r => r.MaxY));
        }

        /// <summary>
        /// From every corner the packing is the mirror image of the one from the upper left: each box on the same sheet,
        /// as far from the start corner's sides as it stood from the upper left's, and the sheets filled as much.
        /// </summary>
        [Theory]
        [MemberData(nameof(Seeds))]
        public void FromEveryCorner_ThePackingIsTheMirrorImageOfTheOneFromTheUpperLeft(int seed)
        {
            (List<GeoRectangle2[]> groups, Sheet sheet, PackOptions options) = Scene(seed);
            PackResult upperLeft = SheetPacker.Pack(groups, sheet, options);
            double tolerance = 1e-9 * Math.Max(sheet.Width, sheet.Height);

            foreach (SheetCorner corner in Corners.Skip(1))
            {
                options.StartCorner = corner;
                bool right = corner == SheetCorner.UpperRight || corner == SheetCorner.LowerRight;
                bool lower = corner == SheetCorner.LowerLeft || corner == SheetCorner.LowerRight;

                PackResult result = SheetPacker.Pack(groups, sheet, options);

                Assert.Equal(upperLeft.Sheets.Count, result.Sheets.Count);
                Assert.Equal(upperLeft.Utilization, result.Utilization, 12);
                for (int g = 0; g < groups.Count; g++)
                {
                    int[] placed = Enumerable.Range(0, groups[g].Length).Where(i => upperLeft.Placements[g][i].Placed).ToArray();
                    for (int i = 0; i < groups[g].Length; i++)
                    {
                        Assert.Equal(upperLeft.Placements[g][i].Placed, result.Placements[g][i].Placed);
                        Assert.Equal(upperLeft.Placements[g][i].SheetIndex, result.Placements[g][i].SheetIndex);
                    }

                    // A group kept as drawn is one block, its boxes as they stood: the block is mirrored, not its boxes.
                    bool kept = options.GroupLayout == GroupLayout.Keep && placed.Length > 1
                        && placed.All(i => upperLeft.Placements[g][i].Translation.IsEqualTo(upperLeft.Placements[g][placed[0]].Translation, Tol));
                    IEnumerable<int[]> pieces = kept ? new[] { placed } : placed.Select(i => new[] { i });
                    foreach (int[] piece in pieces)
                    {
                        if (kept)
                        {
                            GeoVector2 move = result.Placements[g][piece[0]].Translation;
                            Assert.All(piece, i => Assert.True(result.Placements[g][i].Translation.IsEqualTo(move, Tol), $"seed {seed} {corner}: group {g} kept"));
                        }

                        GeoRectangle2 area = result.Sheets[result.Placements[g][piece[0]].SheetIndex].UsableArea;
                        var was = Union(piece.Select(i => upperLeft.Placements[g][i].ViewBox));
                        var now = Union(piece.Select(i => result.Placements[g][i].ViewBox));
                        double expectedMinX = right ? area.LowerLeft.X + area.UpperRight.X - was.MaxX : was.MinX;
                        double expectedMinY = lower ? area.LowerLeft.Y + area.UpperRight.Y - was.MaxY : was.MinY;
                        Assert.True(Math.Abs(now.MinX - expectedMinX) <= tolerance && Math.Abs(now.MinY - expectedMinY) <= tolerance,
                            $"seed {seed} {corner}: group {g} box {piece[0]} at ({now.MinX}, {now.MinY}), mirror ({expectedMinX}, {expectedMinY})");
                    }
                }
            }
        }
    }
}
