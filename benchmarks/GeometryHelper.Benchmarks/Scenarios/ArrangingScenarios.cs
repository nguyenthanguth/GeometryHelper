using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Arranging;
using GeometryHelper.Geometry;

namespace GeometryHelper.Benchmarks.Scenarios
{
    /// <summary>
    /// Label placement: <see cref="Arranger.Run(IReadOnlyList{ArrangeItem}, ArrangeOptions)"/>, which counts the free
    /// places of every label against every region and line kept clear of, places each label against what is already
    /// there, and judges every label against every other.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every box stands square and every leader runs along X or Y, so that no sine or cosine goes into a position, and the
    /// options are the defaults with the tolerance given.
    /// </para>
    /// <para>
    /// Measured at 36f56df, 5 000 sparse labels took 8.9 s: 6.6 s counting free places against the 6 250 regions and
    /// lines, 1.4 s picking out what is near each label as it is placed, and 0.8 s judging the 25 000 000 pairs of labels.
    /// </para>
    /// </remarks>
    internal static class ArrangingScenarios
    {
        private static readonly ArrangeOptions Options = new ArrangeOptions { Tolerance = Tolerance.Default };

        /// <summary>
        /// Every case of label placement.
        /// </summary>
        internal static IEnumerable<BenchmarkCase> All()
        {
            foreach (int count in new[] { 100, 500, 1000, 5000 })
            {
                yield return new BenchmarkCase("Arrange.Sparse." + count, count, "labels", count >= 5000, () => Sparse(count));
            }

            yield return new BenchmarkCase("Arrange.Dense", DenseSide * DenseSide, "labels", false, Dense);
            yield return new BenchmarkCase("Arrange.StaticBlocks", StaticCount, "labels", false, StaticBlocks);
        }

        /// <summary>
        /// Parts drawn far apart, 1 500 between them on a grid, each a rectangle with a dimension along each of its four
        /// sides 40 out, a label of 40 by 10 at the middle of each: each label keeps clear of its own part and of the four
        /// dimension lines of it, so that what there is to keep clear of grows with the labels, and almost none of it is
        /// near any one label.
        /// </summary>
        private static Fixture Sparse(int count)
        {
            var random = new Random(count);
            int parts = count / 4;
            int columns = (int)Math.Ceiling(Math.Sqrt(parts));
            var items = new List<ArrangeItem>(count);

            for (int p = 0; p < parts; p++)
            {
                double x0 = 1500.0 * (p % columns) + random.Next(200), y0 = 1500.0 * (p / columns) + random.Next(200);
                double x1 = x0 + 150 + random.Next(150), y1 = y0 + 80 + random.Next(120);

                var region = new[] { Rectangle(x0, y0, x1, y1) };
                var lines = new[]
                {
                    new GeoLine2(x0, y0 - 40, x1, y0 - 40),
                    new GeoLine2(x1 + 40, y0, x1 + 40, y1),
                    new GeoLine2(x1, y1 + 40, x0, y1 + 40),
                    new GeoLine2(x0 - 40, y1, x0 - 40, y0),
                };

                foreach (GeoLine2 leader in lines)
                {
                    items.Add(new ArrangeItem
                    {
                        Leader = leader,
                        Box = new GeoRectangle2(leader.MidPoint, 40.0, 10.0),
                        Offset = 5.0,
                        BlockPolygons = region,
                        BlockLines = lines,
                    });
                }
            }

            return Fixture.Of(() => Arranger.Run(items, Options), Sign, Describe, null);
        }

        private const int DenseSide = 20;

        /// <summary>
        /// 400 labels of 24 by 10 on leaders 30 long, 28 apart each way, with every leader a line every label keeps clear
        /// of and six regions among them: hardly more room than labels, so that every label has much near it, most of
        /// its places are taken, and a few find none, which runs the second pass.
        /// </summary>
        private static Fixture Dense()
        {
            var random = new Random(400);
            var leaders = new List<GeoLine2>();

            for (int i = 0; i < DenseSide; i++)
            {
                for (int j = 0; j < DenseSide; j++)
                {
                    double x = 28.0 * i + random.Next(10), y = 28.0 * j + random.Next(10);
                    leaders.Add(random.Next(2) == 0 ? new GeoLine2(x, y, x + 30, y) : new GeoLine2(x, y, x, y + 30));
                }
            }

            var regions = new List<GeoPolygon2>();

            for (int k = 0; k < 6; k++)
            {
                double x = random.Next(530), y = random.Next(530);
                regions.Add(Rectangle(x, y, x + 20 + random.Next(30), y + 20 + random.Next(30)));
            }

            var items = leaders.Select(leader => new ArrangeItem
            {
                Leader = leader,
                Box = new GeoRectangle2(leader.MidPoint, 24.0, 10.0),
                Offset = 3.0,
                BlockPolygons = regions,
                BlockLines = leaders,
            }).ToList();

            return Fixture.Of(() => Arranger.Run(items, Options), Sign, Describe, null);
        }

        private const int StaticCount = 1000;

        /// <summary>
        /// 1 000 labels on leaders over a sheet 30 000 by 20 000, every one of them keeping clear of one shared list of
        /// 300 regions and of one shared list of every leader: what each label keeps clear of is the same 1 300
        /// regions and lines, held once.
        /// </summary>
        private static Fixture StaticBlocks()
        {
            var random = new Random(1000);
            var leaders = new List<GeoLine2>(StaticCount);

            for (int i = 0; i < StaticCount; i++)
            {
                double x = random.Next(30000), y = random.Next(20000), length = 50 + random.Next(250);
                leaders.Add(random.Next(2) == 0 ? new GeoLine2(x, y, x + length, y) : new GeoLine2(x, y, x, y + length));
            }

            var regions = new List<GeoPolygon2>(300);

            for (int k = 0; k < 300; k++)
            {
                double x = random.Next(30000), y = random.Next(20000);
                regions.Add(Rectangle(x, y, x + 50 + random.Next(400), y + 50 + random.Next(400)));
            }

            var items = leaders.Select(leader => new ArrangeItem
            {
                Leader = leader,
                Box = new GeoRectangle2(leader.MidPoint, 60.0, 12.0),
                Offset = 5.0,
                BlockPolygons = regions,
                BlockLines = leaders,
            }).ToList();

            return Fixture.Of(() => Arranger.Run(items, Options), Sign, Describe, null);
        }

        private static GeoPolygon2 Rectangle(double x0, double y0, double x1, double y1)
            => new GeoPolygon2(new GeoPoint2(x0, y0), new GeoPoint2(x1, y0), new GeoPoint2(x1, y1), new GeoPoint2(x0, y1));

        /// <summary>
        /// Signs the results: for each label in order, how far it moves, then whether it is placed.
        /// </summary>
        private static void Sign(ResultSignature signature, ArrangeResult[] results)
        {
            signature.Add(results.Length);

            foreach (ArrangeResult r in results)
            {
                signature.Add(r.Translation);
                signature.Add(r.Placed);
            }
        }

        private static string Describe(ArrangeResult[] results)
            => Report.Count(results.Count(r => r.Placed)) + " of " + Report.Count(results.Length) + " placed";
    }
}
