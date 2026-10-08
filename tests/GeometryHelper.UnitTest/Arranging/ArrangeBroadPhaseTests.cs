using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using GeometryHelper.Arranging;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.UnitTest.Arranging
{
    /// <summary>
    /// What a run gave before what is near each label was picked out through an index rather than by going over
    /// everything: scenes built to trouble such an index, run under options that bend the reach of each label, their
    /// results written out to the last bit and hashed, and three scenes where one obstacle more or less in what a label
    /// is weighed against changes where it goes.
    /// </summary>
    /// <remarks>
    /// The hashes and the translations were taken from a build of 3a2da08, the last to go over every obstacle for every
    /// label.
    /// </remarks>
    public class ArrangeBroadPhaseTests : ArrangeTestKit
    {
        private static readonly Tolerance Tol = Tolerance.Default;

        [Theory]
        [InlineData("sparse", "6e3f42769b87b2e904374164c21ee3496830b14d4c384126ea79719fe46a5e19")]
        [InlineData("clusters", "852a0f2e283132a74a5d37ed630d93f1eddeeae9852d76015bd471296c398c3d")]
        [InlineData("huge regions", "523d1cc09e6a5e1a72adfc7372a869739b1c37749913a5a9bf237dfb4df21205")]
        public void EveryScene_UnderEveryOptionSet_GivesWhat3a2da08Gave(string scene, string expected)
        {
            List<ArrangeItem> items = Scene(scene);
            var text = new StringBuilder();

            foreach (ArrangeOptions options in OptionSets())
            {
                Append(text, Arranger.Run(items, options));
            }

            Assert.Equal(expected, Hash(text));
        }

        /// <summary>
        /// A label 20 by 10 on a leader 100 long, with a region far up to the left and a second just below the reach
        /// within which it looks for what to keep clear of. Weighed against the first alone, the first row below is the
        /// more open, 141.5 from it against 132 for the row above; the second, 65.5 below that row, would turn it round.
        /// It lies half a unit beyond the reach, and is not weighed; a margin one unit wider takes it in.
        /// </summary>
        [Fact]
        public void AnObstacleJustBeyondTheReachOfALabel_IsNotWeighedForItsClearance()
        {
            var leader = new GeoLine2(0, 0, 100, 0);
            var label = new ArrangeItem
            {
                Leader = leader,
                Box = new GeoRectangle2(leader.MidPoint, 20.0, 10.0),
                Offset = 5.0,
                BlockPolygons = new[] { Rectangle(-84, 70, -80, 75), Rectangle(45, -85, 55, -80.5) },
            };

            // The reach runs 70 out from the candidates, 20 the span of the box and 50 the margin: down to -80.
            ArrangeResult within = Arranger.Run(new[] { label }, new ArrangeOptions { PerpendicularLevels = 1, Tolerance = Tol })[0];
            ArrangeResult wider = Arranger.Run(new[] { label }, new ArrangeOptions { PerpendicularLevels = 1, NeighbourMargin = 51.0, Tolerance = Tol })[0];

            Assert.True(within.Placed);
            Assert.Equal(0.0, within.Translation.X);
            Assert.Equal(-10.0, within.Translation.Y);

            Assert.True(wider.Placed);
            Assert.Equal(0.0, wider.Translation.X);
            Assert.Equal(10.0, wider.Translation.Y);
        }

        /// <summary>
        /// Two labels 20 by 10, one kept above its leader and one below its own, ending up 0.0005 apart, half the
        /// tolerance: the boxes around them stand apart, so they are not judged against each other, and both are placed,
        /// though the rectangles themselves, judged within the tolerance, would collide.
        /// </summary>
        [Fact]
        public void TwoLabelsLessThanTheToleranceApart_AreBothPlaced()
        {
            var lower = new GeoLine2(0, 0, 100, 0);
            var upper = new GeoLine2(0, 30.0005, 100, 30.0005);
            var items = new[]
            {
                new ArrangeItem { Leader = lower, Box = new GeoRectangle2(lower.MidPoint, 20.0, 10.0), Offset = 5.0, Side = ArrangeSide.Top },
                new ArrangeItem { Leader = upper, Box = new GeoRectangle2(upper.MidPoint, 20.0, 10.0), Offset = 5.0, Side = ArrangeSide.Bottom },
            };

            ArrangeResult[] results = Arranger.Run(items, new ArrangeOptions { Tolerance = Tol });

            Assert.True(results[0].Placed);
            Assert.True(results[1].Placed);
            Assert.Equal(new GeoVector2(0.0, 10.0), results[0].Translation);
            Assert.Equal(-10.0, results[1].Translation.Y, 9);
            Assert.True(MovedBox(items[0], results[0].Translation).CollidesWith(MovedBox(items[1], results[1].Translation), Tol));
        }

        /// <summary>
        /// Two labels on leaders 20 apart, the first row above the lower one the same as the first row below the upper
        /// one, and a region grazing the box of the twelfth place the upper one counts, its last, by two units at the far
        /// end of all it counts. That region leaves the upper label eleven free places of twelve, so it is placed first
        /// and takes the row the two share; counted without it, the two tie, the lower goes first, as listed, and takes
        /// that row instead. The margin is negative, five units in: the region lies within it.
        /// </summary>
        [Fact]
        public void ARegionGrazingTheLastPlaceCounted_CountsAgainstTheFreedomOfALabel()
        {
            var lower = new GeoLine2(0, 0, 100, 0);
            var upper = new GeoLine2(0, 20, 100, 20);
            var below = new GeoLine2(-100, -20, 200, -20);
            var grazing = Rectangle(28.25, 31, 32.25, 33);
            var options = new ArrangeOptions { NeighbourMargin = -5.0, PlaceFromInsideOut = false, Tolerance = Tol };

            ArrangeResult[] grazed = Arranger.Run(Pair(lower, upper, below, grazing), options);
            ArrangeResult[] clear = Arranger.Run(Pair(lower, upper, below, Rectangle(25.25, 31, 29.25, 33)), options);

            Assert.Equal(new GeoVector2(0.0, -10.0), grazed[0].Translation);
            Assert.Equal(new GeoVector2(0.0, -10.0), grazed[1].Translation);
            Assert.True(grazed[0].Placed);
            Assert.True(grazed[1].Placed);

            // Clear of every place counted, the region leaves the two tied, and the lower label takes the shared row.
            Assert.Equal(new GeoVector2(0.0, 10.0), clear[0].Translation);
            Assert.True(clear[0].Placed);
            Assert.True(clear[1].Placed);
        }

        private static ArrangeItem[] Pair(GeoLine2 lower, GeoLine2 upper, GeoLine2 below, GeoPolygon2 region)
            => new[]
            {
                new ArrangeItem { Leader = lower, Box = new GeoRectangle2(lower.MidPoint, 20.0, 10.0), Offset = 5.0, BlockLines = new[] { below } },
                new ArrangeItem { Leader = upper, Box = new GeoRectangle2(upper.MidPoint, 20.0, 10.0), Offset = 5.0, BlockPolygons = new[] { region } },
            };

        /// <summary>
        /// The options each scene runs under: the defaults, and those that move the reach of a label, the freedom it is
        /// counted by, or the order labels are placed in.
        /// </summary>
        private static IEnumerable<ArrangeOptions> OptionSets()
        {
            yield return new ArrangeOptions { Tolerance = Tol };
            yield return new ArrangeOptions { NeighbourMargin = 0.0, Tolerance = Tol };
            yield return new ArrangeOptions { NeighbourMargin = -15.0, Tolerance = Tol };
            yield return new ArrangeOptions { NeighbourMargin = -400.0, Tolerance = Tol };
            yield return new ArrangeOptions { FreedomSampleSize = 0, Tolerance = Tol };
            yield return new ArrangeOptions { FreedomSampleSize = 1, Tolerance = Tol };
            yield return new ArrangeOptions { PlaceMostConstrainedFirst = false, PlaceFromInsideOut = false, Tolerance = Tol };
            yield return new ArrangeOptions { PlaceMostConstrainedFirst = false, Tolerance = Tol };
            yield return new ArrangeOptions { PlaceFromInsideOut = false, Tolerance = Tol };
            yield return new ArrangeOptions { MaximumCandidates = 5, LookAheadCandidates = 1, Tolerance = Tol };
            yield return new ArrangeOptions { RowGap = -12.0, PerpendicularLevels = 4, NeighbourMargin = -20.0, Tolerance = Tol };
        }

        private static List<ArrangeItem> Scene(string name)
        {
            switch (name)
            {
                case "sparse": return SparseParts(60, 11);
                case "clusters": return Clusters(23);
                case "huge regions": return HugeRegions(37);
                default: throw new ArgumentException(name, nameof(name));
            }
        }

        /// <summary>
        /// Parts 900 apart on a grid, each a rectangle with a dimension line along each side 30 out, and on each line a
        /// label of its own size, a quarter of them turned: each label keeps clear of its own part and its four lines.
        /// One part lies ten million out.
        /// </summary>
        private static List<ArrangeItem> SparseParts(int parts, int seed)
        {
            var random = new Random(seed);
            int columns = (int)Math.Ceiling(Math.Sqrt(parts));
            var items = new List<ArrangeItem>();

            for (int p = 0; p < parts; p++)
            {
                double far = p == parts - 1 ? 1E7 : 0.0;
                double x0 = far + 900.0 * (p % columns) + random.Next(200), y0 = far + 900.0 * (p / columns) + random.Next(200);
                double x1 = x0 + 100 + random.Next(150), y1 = y0 + 60 + random.Next(120);
                var region = new[] { Rectangle(x0, y0, x1, y1) };
                var lines = new[]
                {
                    new GeoLine2(x0, y0 - 30, x1, y0 - 30),
                    new GeoLine2(x1 + 30, y0, x1 + 30, y1),
                    new GeoLine2(x1, y1 + 30, x0, y1 + 30),
                    new GeoLine2(x0 - 30, y1, x0 - 30, y0),
                };

                foreach (GeoLine2 leader in lines)
                {
                    double angle = random.Next(4) == 0 ? random.NextDouble() * Math.PI : 0.0;
                    items.Add(new ArrangeItem
                    {
                        Leader = leader,
                        Box = new GeoRectangle2(leader.MidPoint, 20 + random.Next(40), 8 + random.Next(8), angle),
                        Offset = 2 + random.Next(8),
                        BlockPolygons = region,
                        BlockLines = lines,
                    });
                }
            }

            return items;
        }

        /// <summary>
        /// Five clusters of 24 labels each on leaders every which way within 160 by 160, every leader a line every label
        /// keeps clear of, one region in each cluster and the first given again as an equal copy; nulls among them, labels
        /// listed twice or copied, so that boxes stand on the same bounds, a label too small to arrange and one on a leader
        /// of no length.
        /// </summary>
        private static List<ArrangeItem> Clusters(int seed)
        {
            var random = new Random(seed);
            var leaders = new List<GeoLine2>();
            var regions = new List<GeoPolygon2>();

            for (int c = 0; c < 5; c++)
            {
                double cx = 2000.0 * c + random.Next(300), cy = 700.0 * (c % 2);
                for (int k = 0; k < 24; k++)
                {
                    var start = new GeoPoint2(cx + random.NextDouble() * 160, cy + random.NextDouble() * 160);
                    double a = random.Next(3) == 0 ? random.NextDouble() * 2 * Math.PI : random.Next(4) * Math.PI / 2;
                    double length = 15 + random.NextDouble() * 60;
                    leaders.Add(new GeoLine2(start, new GeoPoint2(start.X + length * Math.Cos(a), start.Y + length * Math.Sin(a))));
                }

                regions.Add(Rectangle(cx + 60, cy + 60, cx + 90, cy + 85));
            }

            GeoPolygon2 first = regions[0];
            regions.Add(new GeoPolygon2(first[0], first[1], first[2], first[3]));

            var items = new List<ArrangeItem>();
            for (int i = 0; i < leaders.Count; i++)
            {
                if (i % 9 == 4)
                {
                    items.Add(null);
                }

                var item = new ArrangeItem
                {
                    Leader = leaders[i],
                    Box = new GeoRectangle2(leaders[i].MidPoint, 14 + random.Next(20), 8 + random.Next(6), random.Next(3) == 0 ? random.NextDouble() : 0.0),
                    Offset = 1 + random.Next(6),
                    BlockPolygons = regions,
                    BlockLines = leaders,
                };
                items.Add(item);

                if (i % 11 == 3)
                {
                    items.Add(item);
                }

                if (i % 13 == 6)
                {
                    items.Add(new ArrangeItem { Leader = item.Leader, Box = item.Box, Offset = 3.0, BlockPolygons = regions, BlockLines = leaders });
                }
            }

            GeoPoint2 point = leaders[40].MidPoint;
            items.Insert(7, new ArrangeItem { Leader = leaders[3], Box = new GeoRectangle2(leaders[3].MidPoint, 6.0, 4.0), Offset = 3.0, BlockPolygons = regions, BlockLines = leaders });
            items.Insert(30, new ArrangeItem { Leader = new GeoLine2(point, point), Box = new GeoRectangle2(point, 20.0, 10.0), Offset = 3.0, BlockPolygons = regions, BlockLines = leaders });
            return items;
        }

        /// <summary>
        /// A comb 20 000 wide, its spine along the bottom and a tooth 100 wide and 6 000 high every 1 000, so that the
        /// box around it takes in every label though only a tooth or the spine is ever near one; a slab across the top;
        /// a line across the whole sheet; thirty block lines of no length. 150 labels stand in the gaps between the teeth.
        /// </summary>
        private static List<ArrangeItem> HugeRegions(int seed)
        {
            var random = new Random(seed);
            var comb = new List<GeoPoint2> { new GeoPoint2(0, 0), new GeoPoint2(20000, 0) };
            for (int t = 20; t >= 1; t--)
            {
                comb.Add(new GeoPoint2(1000.0 * t, 6000));
                comb.Add(new GeoPoint2(1000.0 * t - 100, 6000));
                comb.Add(new GeoPoint2(1000.0 * t - 100, 100));
                comb.Add(new GeoPoint2(1000.0 * (t - 1), 100));
            }

            var regions = new[] { new GeoPolygon2(comb), Rectangle(-500, 6500, 20500, 7000) };
            var lines = new List<GeoLine2> { new GeoLine2(-1000, -1000, 21000, 7500) };
            for (int k = 0; k < 30; k++)
            {
                var spot = new GeoPoint2(random.Next(20000), 200 + random.Next(5600));
                lines.Add(new GeoLine2(spot, spot));
            }

            var items = new List<ArrangeItem>();
            for (int i = 0; i < 150; i++)
            {
                var start = new GeoPoint2(1000.0 * random.Next(20) + 150 + random.Next(600), 300 + random.Next(5400));
                double a = random.Next(2) == 0 ? random.Next(4) * Math.PI / 2 : random.NextDouble() * 2 * Math.PI;
                double length = 30 + random.Next(170);
                var leader = new GeoLine2(start, new GeoPoint2(start.X + length * Math.Cos(a), start.Y + length * Math.Sin(a)));
                items.Add(new ArrangeItem
                {
                    Leader = leader,
                    Box = new GeoRectangle2(leader.MidPoint, 20 + random.Next(40), 8 + random.Next(8)),
                    Offset = 2 + random.Next(10),
                    BlockPolygons = regions,
                    BlockLines = lines,
                });
            }

            return items;
        }

        private static string Bits(double value)
            => BitConverter.DoubleToInt64Bits(value).ToString("x16", CultureInfo.InvariantCulture);

        private static void Append(StringBuilder text, ArrangeResult[] results)
        {
            foreach (ArrangeResult r in results)
            {
                text.Append(Bits(r.Translation.X)).Append(',').Append(Bits(r.Translation.Y)).Append(r.Placed ? '+' : '-').Append(' ');
            }

            text.AppendLine();
        }

        private static string Hash(StringBuilder text)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(text.ToString()));
                return string.Concat(hash.Select(b => b.ToString("x2", CultureInfo.InvariantCulture)));
            }
        }
    }
}
