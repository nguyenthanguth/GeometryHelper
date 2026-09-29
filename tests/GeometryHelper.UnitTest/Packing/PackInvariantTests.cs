using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using GeometryHelper.Geometry;
using GeometryHelper.Packing;
using Xunit;

namespace GeometryHelper.UnitTest.Packing
{
    /// <summary>
    /// What every packing promises, over scenes of every kind: a placed box inside the usable area of its sheet, clear
    /// of the others by the spacing asked for, only moved; and every box that fits a sheet placed.
    /// </summary>
    public class PackInvariantTests
    {
        private const double Slack = 1e-6;

        public static IEnumerable<object[]> Seeds() => Enumerable.Range(1, 40).Select(seed => new object[] { seed });

        private static (List<GeoRectangle2[]> Groups, Sheet Sheet, PackOptions Options) Scene(int seed)
        {
            var random = new Random(seed);
            PaperSize[] sizes = { PaperSize.A0, PaperSize.A1, PaperSize.A2, PaperSize.A3, PaperSize.A4 };
            Sheet sheet = random.Next(5) == 0
                ? Sheet.Custom(100 + random.Next(900), 100 + random.Next(700))
                : new Sheet(sizes[random.Next(sizes.Length)], random.Next(3) == 0 ? SheetOrientation.Portrait : SheetOrientation.Landscape);
            sheet.Scale = random.Next(3) == 0 ? 1.0 : new[] { 10.0, 20.0, 25.0, 50.0 }[random.Next(4)];
            sheet.OffsetLeft = random.Next(25);
            sheet.OffsetRight = random.Next(15);
            sheet.OffsetTop = random.Next(15);
            sheet.OffsetBottom = random.Next(15);
            sheet.NewSheet = (SheetDirection)random.Next(4);
            sheet.SheetSpacing = random.Next(3) * 25.0;
            sheet.Origin = new GeoPoint2(random.Next(-5000, 5000), random.Next(-5000, 5000));

            // Boxes up to a little over the usable area, so that some fit no sheet.
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
            };
            return (groups, sheet, options);
        }

        // The upright box round a rectangle.
        private static (double MinX, double MinY, double MaxX, double MaxY) Round(GeoRectangle2 box)
        {
            GeoPoint2[] v = box.GetVertices();
            return (v.Min(p => p.X), v.Min(p => p.Y), v.Max(p => p.X), v.Max(p => p.Y));
        }

        [Theory]
        [MemberData(nameof(Seeds))]
        public void EveryPacking_KeepsItsPromises(int seed)
        {
            (List<GeoRectangle2[]> groups, Sheet sheet, PackOptions options) = Scene(seed);

            PackResult result = SheetPacker.Pack(groups, sheet, options);

            double tolerance = Slack * Math.Max(sheet.Width, sheet.Height);
            double spacing = options.Spacing * sheet.Scale, groupSpacing = options.GroupSpacing * sheet.Scale;
            SheetFrame first = result.Sheets[0];
            var placed = new List<(int Group, int Sheet, (double MinX, double MinY, double MaxX, double MaxY) Box)>();
            for (int g = 0; g < groups.Count; g++)
            {
                Assert.Equal(groups[g].Length, result.Placements[g].Count);
                for (int i = 0; i < groups[g].Length; i++)
                {
                    GeoRectangle2 box = groups[g][i];
                    PackPlacement placement = result.Placements[g][i];
                    var round = Round(box);
                    bool fits = round.MaxX - round.MinX <= first.UsableArea.Width + tolerance && round.MaxY - round.MinY <= first.UsableArea.Height + tolerance;

                    // Every box that fits a sheet is placed, and only those.
                    Assert.True(placement.Placed == fits, $"group {g} box {i}: fits {fits}, placed {placement.Placed}");
                    if (!placement.Placed)
                    {
                        Assert.Equal(-1, placement.SheetIndex);
                        Assert.Equal(box, placement.ViewBox);
                        continue;
                    }

                    // Only moved.
                    Assert.Equal(box.Translate(placement.Translation), placement.ViewBox);

                    // Inside the usable area of its sheet.
                    Assert.InRange(placement.SheetIndex, 0, result.Sheets.Count - 1);
                    GeoRectangle2 area = result.Sheets[placement.SheetIndex].UsableArea;
                    var moved = Round(placement.ViewBox);
                    Assert.True(moved.MinX >= area.LowerLeft.X - tolerance && moved.MinY >= area.LowerLeft.Y - tolerance
                        && moved.MaxX <= area.UpperRight.X + tolerance && moved.MaxY <= area.UpperRight.Y + tolerance,
                        $"group {g} box {i} out of sheet {placement.SheetIndex}");
                    placed.Add((g, placement.SheetIndex, moved));
                }
            }

            // Clear of one another: the group spacing apart between groups, the spacing (or the group spacing, where a
            // group was split) within one; kept groups stand as they were given.
            for (int a = 0; a < placed.Count; a++)
            {
                for (int b = a + 1; b < placed.Count; b++)
                {
                    if (placed[a].Sheet != placed[b].Sheet)
                    {
                        continue;
                    }

                    bool sameGroup = placed[a].Group == placed[b].Group;
                    if (sameGroup && options.GroupLayout == GroupLayout.Keep)
                    {
                        continue;
                    }

                    double gap = (sameGroup ? Math.Min(spacing, groupSpacing) : groupSpacing) - tolerance;
                    var p = placed[a].Box;
                    var q = placed[b].Box;
                    bool apart = p.MaxX + gap <= q.MinX || q.MaxX + gap <= p.MinX || p.MaxY + gap <= q.MinY || q.MaxY + gap <= p.MinY;
                    Assert.True(apart, $"seed {seed}: boxes of groups {placed[a].Group} and {placed[b].Group} on sheet {placed[a].Sheet}");
                }
            }

            // The sheets taken lie where the sheet says, and none is empty but the first when nothing goes.
            for (int k = 0; k < result.Sheets.Count; k++)
            {
                Assert.Equal(sheet.GetFrame(k), result.Sheets[k]);
                Assert.True(k == 0 || placed.Any(p => p.Sheet == k), $"sheet {k} empty");
            }
        }

        /// <summary>
        /// A thousand boxes in two hundred groups pack in well under a second.
        /// </summary>
        [Fact]
        public void AThousandBoxes_PackQuickly()
        {
            var random = new Random(5);
            var groups = Enumerable.Range(0, 200)
                .Select(g => Enumerable.Range(0, 5).Select(i => new GeoRectangle2(new GeoPoint2(0, 0), 20 + random.Next(150), 15 + random.Next(100))).ToArray())
                .ToList();
            var sheet = new Sheet(PaperSize.A1) { OffsetLeft = 20, OffsetRight = 10, OffsetTop = 10, OffsetBottom = 10 };

            SheetPacker.Pack(groups.Take(5).ToList(), sheet);
            var watch = Stopwatch.StartNew();
            PackResult result = SheetPacker.Pack(groups, sheet, new PackOptions { Spacing = 5, GroupSpacing = 15 });
            watch.Stop();

            Assert.All(result.Placements.SelectMany(g => g), p => Assert.True(p.Placed));
            Assert.True(watch.ElapsedMilliseconds < 2000, $"{watch.ElapsedMilliseconds} ms");
        }

        /// <summary>
        /// Largest first, earlier sheets filled, a thousand boxes take as few sheets as their area allows, or one more:
        /// the method packs tight when the order of the groups is left to it.
        /// </summary>
        [Fact]
        public void PackedLargestFirst_FillingEarlierSheets_TheBoxesTakeNearlyTheFewestSheets()
        {
            var random = new Random(5);
            var groups = Enumerable.Range(0, 1000)
                .Select(g => new[] { new GeoRectangle2(new GeoPoint2(0, 0), 20 + random.Next(150), 15 + random.Next(100)) })
                .ToList();
            var sheet = new Sheet(PaperSize.A1) { OffsetLeft = 20, OffsetRight = 10, OffsetTop = 10, OffsetBottom = 10 };
            double fewest = Math.Ceiling(groups.Sum(g => g[0].Area) / ((841.0 - 30.0) * (594.0 - 20.0)));

            PackResult result = SheetPacker.Pack(groups, sheet, new PackOptions { LargestGroupsFirst = true, FillEarlierSheets = true });

            Assert.True(result.Sheets.Count <= fewest + 1, $"{result.Sheets.Count} sheets, at least {fewest}");
            Assert.True(result.Utilization > 0.9, $"filled {result.Utilization:P1}");
        }
    }
}
