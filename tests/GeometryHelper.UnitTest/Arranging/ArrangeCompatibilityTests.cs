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
    /// What 6.2.0 gave for labels with the same gap on both sides of their leader: the candidates and the results of a
    /// spread of scenes, written out to the last bit and hashed. Every release since promises the same, so any change
    /// to where such a label goes, or to the order its candidates come in, shows up here.
    /// </summary>
    /// <remarks>
    /// The hashes were taken from a build of 5eea18b, the 6.2.0 release. The scenes keep to what later fixes left
    /// alone on purpose: caps that do not bind, one list of regions shared by every item, searches within their
    /// step budget.
    /// </remarks>
    public class ArrangeCompatibilityTests : ArrangeTestKit
    {
        private const string CandidatesOf620 = "bb1ebe4fae6b325b8c65ebd6d1ff4f7de447164b16afce6614d790aee4ffcd42";

        private static readonly Dictionary<ArrangeAlgorithmType, string> ResultsOf620 = new Dictionary<ArrangeAlgorithmType, string>
        {
            [ArrangeAlgorithmType.Greedy] = "e68c9f75633e5fdefeba3cc8b9b4e97c5822079a6fd2962035fd8e7855649cd5",
            [ArrangeAlgorithmType.BoundedBacktracking] = "9bad7a249c11c1a46d1ef8c9922ec5ce0d161f81512a740d1b4c372f636ed42e",
            [ArrangeAlgorithmType.SimulatedAnnealing] = "e83856749695f5589da0126e8c079a28affc0dc7db7022f5175989c31bc1bc2e",
            [ArrangeAlgorithmType.ForceDirected] = "7fc66ae159e34f420f0a5c7d850006c3b6f7854b6dc908102a242aabaaa127ff",
            [ArrangeAlgorithmType.ConstraintSatisfaction] = "9160817f1c7ca07c6f4f10c7daeb9fad74df4b5beb7749118afcfd55f2d53d5f",
        };

        [Fact]
        public void TheCandidates_AreThoseOf620()
        {
            var text = new StringBuilder();
            var leaders = new[]
            {
                new GeoLine2(0, 0, 100, 0), new GeoLine2(100, 0, 0, 0), new GeoLine2(0, 0, 0, 100), new GeoLine2(0, 100, 0, 0),
                new GeoLine2(0, 0, 100, 100), new GeoLine2(0, 100, 100, 0), new GeoLine2(10, 20, 250, 60), new GeoLine2(3000, 1200, -500, 4000),
            };
            var optionSets = new[]
            {
                new ArrangeOptions(),
                new ArrangeOptions { RowGap = 5.0, PerpendicularLevels = 4, LongitudinalOvershootRatio = 0.3 },
                new ArrangeOptions { PerpendicularLevels = 1, LongitudinalOvershootRatio = 0.0, MinimumBoxSize = 0.5 },
            };

            foreach (GeoLine2 leader in leaders)
            {
                foreach (double offset in new[] { 5.0, 50.0, 0.0, -3.0 })
                {
                    foreach (double angle in new[] { 0.0, 0.3 })
                    {
                        foreach (ArrangeOptions options in optionSets)
                        {
                            var item = new ArrangeItem { Leader = leader, Box = new GeoRectangle2(new GeoPoint2(7, -3), 20.0, 10.0, angle), Offset = offset };
                            foreach (GeoPoint2 p in item.GetPlacePoints(options))
                            {
                                text.Append(Number(p.X)).Append(',').Append(Number(p.Y)).Append(' ');
                            }

                            text.AppendLine();
                        }
                    }
                }
            }

            Assert.Equal(CandidatesOf620, Hash(text));
        }

        [Theory]
        [MemberData(nameof(AllAlgorithms))]
        public void TheResults_AreThoseOf620(ArrangeAlgorithmType algorithm)
        {
            var text = new StringBuilder();
            Append(text, Arranger.Run(Crowd(lifted: false), new ArrangeOptions { Algorithm = algorithm, RowGap = 5.0 }));
            Append(text, Arranger.Run(Crowd(lifted: true), new ArrangeOptions { Algorithm = algorithm, RowGap = 5.0 }));

            for (int seed = 1; seed <= 6; seed++)
            {
                List<ArrangeItem> scene = Sheet(seed, out ArrangeOptions options);
                options.Algorithm = algorithm;
                Append(text, Arranger.Run(scene, options));
            }

            Assert.Equal(ResultsOf620[algorithm], Hash(text));
        }

        /// <summary>
        /// A label with a small region over the middle of its leader and one under it: every algorithm but annealing
        /// slides it back along the first row above, annealing forward.
        /// </summary>
        [Theory]
        [InlineData(ArrangeAlgorithmType.Greedy, -16.25)]
        [InlineData(ArrangeAlgorithmType.BoundedBacktracking, -16.25)]
        [InlineData(ArrangeAlgorithmType.SimulatedAnnealing, 16.25)]
        [InlineData(ArrangeAlgorithmType.ForceDirected, -16.25)]
        [InlineData(ArrangeAlgorithmType.ConstraintSatisfaction, -16.25)]
        public void ALabelBetweenTwoRegions_GoesWhere620PutIt(ArrangeAlgorithmType algorithm, double along)
        {
            var leader = new GeoLine2(0, 0, 100, 0);
            var label = new ArrangeItem
            {
                Leader = leader,
                Box = new GeoRectangle2(leader.MidPoint, 20.0, 10.0),
                Offset = 12.0,
                BlockPolygons = new[]
                {
                    new GeoPolygon2(new GeoPoint2(45, 10), new GeoPoint2(55, 10), new GeoPoint2(55, 30), new GeoPoint2(45, 30)),
                    new GeoPolygon2(new GeoPoint2(45, -30), new GeoPoint2(55, -30), new GeoPoint2(55, -10), new GeoPoint2(45, -10)),
                },
            };

            ArrangeResult result = Arranger.Run(new[] { label }, new ArrangeOptions { Algorithm = algorithm, RowGap = 5.0, PerpendicularLevels = 3 })[0];

            Assert.True(result.Placed);
            Assert.Equal(along, result.Translation.X, 9);
            Assert.Equal(17.0, result.Translation.Y, 9);
        }

        private static string Number(double value) => value.ToString("R", CultureInfo.InvariantCulture);

        private static void Append(StringBuilder text, ArrangeResult[] results)
        {
            foreach (ArrangeResult r in results)
            {
                text.Append(Number(r.Translation.X)).Append(',').Append(Number(r.Translation.Y)).Append(r.Placed ? '+' : '-').Append(' ');
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

        /// <summary>
        /// Thirty labels on leaders every which way, crowded, one region every label keeps clear of, and when lifted is
        /// asked for block lines across them all, so that the second pass runs.
        /// </summary>
        private static List<ArrangeItem> Crowd(bool lifted)
        {
            var regions = new[] { new GeoPolygon2(new GeoPoint2(40, 40), new GeoPoint2(90, 40), new GeoPoint2(90, 80), new GeoPoint2(40, 80)) };
            var lines = new List<GeoLine2>();
            if (lifted)
            {
                for (int x = -400; x <= 800; x += 7)
                {
                    lines.Add(new GeoLine2(x, -600, x, 800));
                }
            }

            var items = new List<ArrangeItem>();
            for (int i = 0; i < 30; i++)
            {
                double a = i * 0.7;
                var start = new GeoPoint2(20 * (i % 6), 25 * (i / 6));
                var leader = new GeoLine2(start, new GeoPoint2(start.X + 60 * Math.Cos(a), start.Y + 60 * Math.Sin(a)));
                items.Add(new ArrangeItem
                {
                    Leader = leader,
                    Box = new GeoRectangle2(leader.MidPoint, 18.0 + (i % 4) * 3, 8.0 + (i % 3) * 2, (i % 5) * 0.2),
                    Offset = 3.0 + (i % 7),
                    BlockPolygons = regions,
                    BlockLines = lifted && i % 2 == 0 ? lines : new List<GeoLine2>(),
                });
            }

            return items;
        }

        /// <summary>
        /// Labels as a drawing has them: on leaders spread over a sheet, most centred on them and many turned along them,
        /// in some scenes every leader a block line of every label, and a few regions every label keeps clear of.
        /// </summary>
        private static List<ArrangeItem> Sheet(int seed, out ArrangeOptions options)
        {
            var random = new Random(seed);
            int count = 20 + random.Next(30);
            double scale = seed % 3 == 0 ? 25.0 : 1.0;

            var regions = new List<GeoPolygon2>();
            for (int k = 0; k < 4; k++)
            {
                double x = random.NextDouble() * 600 * scale, y = random.NextDouble() * 400 * scale;
                double w = (10 + random.NextDouble() * 60) * scale, h = (10 + random.NextDouble() * 60) * scale;
                regions.Add(new GeoPolygon2(new GeoPoint2(x, y), new GeoPoint2(x + w, y), new GeoPoint2(x + w, y + h), new GeoPoint2(x, y + h)));
            }

            var leaders = new List<GeoLine2>();
            for (int i = 0; i < count; i++)
            {
                var start = new GeoPoint2(random.NextDouble() * 600 * scale, random.NextDouble() * 400 * scale);
                double a = random.Next(4) == 0 ? random.Next(4) * Math.PI / 2 : random.NextDouble() * 2 * Math.PI;
                double length = (20 + random.NextDouble() * 150) * scale;
                leaders.Add(new GeoLine2(start, new GeoPoint2(start.X + length * Math.Cos(a), start.Y + length * Math.Sin(a))));
            }

            var items = new List<ArrangeItem>();
            foreach (GeoLine2 leader in leaders)
            {
                double along = Math.Atan2(leader.EndPoint.Y - leader.StartPoint.Y, leader.EndPoint.X - leader.StartPoint.X);
                double width = (15 + random.NextDouble() * 40) * scale, height = (8 + random.NextDouble() * 12) * scale;
                GeoPoint2 centre = random.Next(3) == 0
                    ? new GeoPoint2(leader.MidPoint.X + (random.NextDouble() - 0.5) * 40 * scale, leader.MidPoint.Y + (random.NextDouble() - 0.5) * 40 * scale)
                    : leader.MidPoint;
                items.Add(new ArrangeItem
                {
                    Leader = leader,
                    Box = new GeoRectangle2(centre, width, height, random.Next(2) == 0 ? along : 0.0),
                    Offset = random.Next(5) == 0 ? 50.0 : random.NextDouble() * 30 * scale,
                    BlockPolygons = regions,
                    BlockLines = seed % 2 == 0 ? leaders : (IReadOnlyList<GeoLine2>)Array.Empty<GeoLine2>(),
                });
            }

            options = new ArrangeOptions
            {
                RowGap = random.NextDouble() * 20 * scale,
                PerpendicularLevels = 1 + random.Next(4),
                LookAheadCandidates = 1 + random.Next(5),
                PlaceMostConstrainedFirst = random.Next(3) != 0,
                PlaceFromInsideOut = random.Next(2) == 0,
                LongitudinalOvershootRatio = random.Next(3) == 0 ? 0.3 : 0.75,
            };
            return items;
        }
    }
}
