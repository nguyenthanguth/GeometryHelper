using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GeometryHelper.Clash;
using GeometryHelper.Geometry;
using GeometryHelper.Spatial;
using Xunit;

namespace GeometryHelper.UnitTest.Solid
{
    /// <summary>
    /// A clash check sweeps the boxes of the parts along the axis that leaves it the fewest pairs to look at, and finds
    /// what it found sweeping along X, to the last bit.
    /// </summary>
    /// <remarks>
    /// The bundles are 64 bars 600 long and 20 by 20 across, 8 by 8 at a pitch of 30, from a fixed seed: a third of
    /// them start at nought, so that many boxes start at the same place, and of the rest one in eight stands 12 over
    /// towards its neighbour, sharing 2 with it, one 10 over, touching it, one 7 over, 3 short of it, and one 4.999 over,
    /// 5.001 short of it, which is the clearance of 5 and the point tolerance. The X, Y and Z bundles are the same with
    /// X and Y, or X and Z, swapped in every coordinate. The hashes are of what the check found before it chose its axis.
    /// </remarks>
    public class ClashSweepAxisTests
    {
        private static readonly Tolerance Within = Tolerance.Default;

        private static readonly ClashOptions Options = new ClashOptions(5.0, true, 1);

        private static List<GeoAabb3> Bundle(char axis)
        {
            var random = new Random(64);
            var boxes = new List<GeoAabb3>();

            for (int row = 0; row < 8; row++)
            {
                for (int column = 0; column < 8; column++)
                {
                    double start = random.Next(3) == 0 ? 0.0 : random.Next(201) * 0.5;
                    int pick = random.Next(8);
                    double over = pick == 0 ? 12.0 : pick == 1 ? 10.0 : pick == 2 ? 7.0 : pick == 3 ? 4.999 : 0.0;
                    var box = new GeoAabb3(
                        new GeoPoint3(start, row * 30.0 + over, column * 30.0),
                        new GeoPoint3(start + 600.0, row * 30.0 + over + 20.0, column * 30.0 + 20.0));
                    boxes.Add(SweepAxisText.Along(axis, box));
                }
            }

            return boxes;
        }

        private static bool InFirstSet(int index) => (index / 8 + index % 8) % 2 == 0;

        private static GeoPreparedSolid3 Part(GeoAabb3 box) => new GeoPreparedSolid3(box.ToObb().ToSolid(), Within);

        private static ClashBar Bar(GeoAabb3 box, char axis)
        {
            // The centre line runs along the axis the bundle lies along, through the middle of the box.
            GeoPoint3 middle = box.Center;
            GeoPoint3 Through(GeoPoint3 end) => axis == 'X' ? new GeoPoint3(end.X, middle.Y, middle.Z)
                : axis == 'Y' ? new GeoPoint3(middle.X, end.Y, middle.Z) : new GeoPoint3(middle.X, middle.Y, end.Z);

            return new ClashBar(new GeoPolyline3(Through(box.Min), Through(box.Max)), 10.0);
        }

        private static string Text(IEnumerable<ClashResult> results) => Text(results.Select(r => (r.First, r.Second, r)));

        /// <summary>
        /// Every result to the last bit, under the indexes given for its pair.
        /// </summary>
        private static string Text(IEnumerable<(int First, int Second, ClashResult Result)> results)
        {
            var text = new StringBuilder();

            foreach ((int first, int second, ClashResult r) in results)
            {
                text.Append(first).Append(',').Append(second).Append(' ').Append(r.Kind).Append(' ');
                SweepAxisText.Append(text, r.Location);
                text.Append(SweepAxisText.Number(r.Volume)).Append(' ').Append(SweepAxisText.Number(r.Depth)).Append(' ')
                    .Append(SweepAxisText.Number(r.LengthInside)).Append(' ').Append(SweepAxisText.Number(r.ContactArea)).Append(' ')
                    .Append(SweepAxisText.Number(r.Distance)).Append(' ');

                if (r.Gap.HasValue)
                {
                    SweepAxisText.Append(text, r.Gap.Value.StartPoint);
                    SweepAxisText.Append(text, r.Gap.Value.EndPoint);
                }

                foreach (GeoSolid3 overlap in r.Overlaps)
                {
                    foreach (GeoFace3 face in overlap.Faces)
                    {
                        SweepAxisText.Append(text, face);
                    }

                    text.Append('#');
                }

                foreach (GeoFace3 face in r.Contact)
                {
                    SweepAxisText.Append(text, face);
                }

                text.Append(r.Error?.GetType().FullName).AppendLine();
            }

            return text.ToString();
        }

        private static string Hash(IEnumerable<ClashResult> results) => SweepAxisText.Hash(new StringBuilder(Text(results)));

        [Theory]
        [InlineData('X', "2825ae1072023e3513cba27a68a32dcccac1a5c431cf1ef7f69309975091dcc1")]
        [InlineData('Y', "6e3e81dae9ff8b2aae95e623387c5fc93c9f0f36c5b063f400ed7b591874becc")]
        [InlineData('Z', "4a98d3a5923ec8921354e66b9f54196654547a0ba8fac0ffb54d30d873058aee")]
        public void OneSetOfBarsSideBySide_ClashesAsBeforeTheAxisWasChosen(char axis, string expected)
        {
            GeoPreparedSolid3[] parts = Bundle(axis).Select(Part).ToArray();

            Assert.Equal(expected, Hash(Clash3.Find(parts, Options, Within)));
        }

        [Theory]
        [InlineData('X', "1b89fbedfd13fb9f6246e3df36ac509bb264f63b5370fc067d4d4293ba425297")]
        [InlineData('Y', "61b010edffa609c76a768e2dc3783e5bf8f0a512d0f3d00ca5b7cffbfffff50f")]
        [InlineData('Z', "380a04d72da1a1a67f16fb0769fad1b69ff4cee79542741c7b3b36d3f0b859d0")]
        public void TwoSetsOfBarsSideBySide_ClashAsBeforeTheAxisWasChosen(char axis, string expected)
        {
            List<GeoAabb3> boxes = Bundle(axis);
            GeoPreparedSolid3[] first = boxes.Where((b, i) => InFirstSet(i)).Select(Part).ToArray();
            GeoPreparedSolid3[] second = boxes.Where((b, i) => !InFirstSet(i)).Select(Part).ToArray();

            Assert.Equal(expected, Hash(Clash3.Find(first, second, Options, Within)));
        }

        [Theory]
        [InlineData('X', "ce7146a1f2e40c2a16a440c6518d4df2293434289c8d8b49dd9ba415c8484f6f")]
        [InlineData('Y', "be47f562e4e8fbab0396bf808f37518899e48f041ee10fa9aa4041ba0be6eb32")]
        [InlineData('Z', "8da09bc3e80c9e2dfff3ff24763ad7123957deab3bc717fb99666bb9e13c15d5")]
        public void BarsByTheirCentreLinesSideBySide_ClashAsBeforeTheAxisWasChosen(char axis, string expected)
        {
            List<GeoAabb3> boxes = Bundle(axis);
            ClashBar[] bars = boxes.Where((b, i) => InFirstSet(i)).Select(b => Bar(b, axis)).ToArray();
            GeoPreparedSolid3[] parts = boxes.Where((b, i) => !InFirstSet(i)).Select(Part).ToArray();

            Assert.Equal(expected, Hash(Clash3.Find(bars, parts, Options, Within)));
        }

        [Theory]
        [InlineData('X')]
        [InlineData('Y')]
        [InlineData('Z')]
        public void OneSetOfBarsSideBySide_ClashesAsEveryPairCheckedOnItsOwn(char axis)
        {
            GeoPreparedSolid3[] parts = Bundle(axis).Select(Part).ToArray();
            var expected = new List<(int, int, ClashResult)>();

            for (int i = 0; i < parts.Length; i++)
            {
                for (int j = i + 1; j < parts.Length; j++)
                {
                    expected.AddRange(Clash3.Find(new[] { parts[i], parts[j] }, Options, Within).Select(r => (i, j, r)));
                }
            }

            Assert.Equal(Text(expected), Text(Clash3.Find(parts, Options, Within)));
        }

        [Theory]
        [InlineData('X')]
        [InlineData('Y')]
        [InlineData('Z')]
        public void TwoSetsOfBarsSideBySide_ClashAsEveryPairCheckedOnItsOwn(char axis)
        {
            List<GeoAabb3> boxes = Bundle(axis);
            GeoPreparedSolid3[] first = boxes.Where((b, i) => InFirstSet(i)).Select(Part).ToArray();
            GeoPreparedSolid3[] second = boxes.Where((b, i) => !InFirstSet(i)).Select(Part).ToArray();
            var expected = new List<(int, int, ClashResult)>();

            for (int i = 0; i < first.Length; i++)
            {
                for (int j = 0; j < second.Length; j++)
                {
                    expected.AddRange(Clash3.Find(new[] { first[i] }, new[] { second[j] }, Options, Within).Select(r => (i, j, r)));
                }
            }

            Assert.Equal(Text(expected), Text(Clash3.Find(first, second, Options, Within)));
        }

        /// <summary>
        /// A box with two of its coordinates swapped as asked: "XY" swaps X and Y, "YZ" Y and Z, "XZ" X and Z, and
        /// nothing swaps none.
        /// </summary>
        private static GeoAabb3 Swapped(string swap, GeoAabb3 box)
        {
            GeoPoint3 Of(GeoPoint3 p)
            {
                switch (swap)
                {
                    case "XY": return new GeoPoint3(p.Y, p.X, p.Z);
                    case "YZ": return new GeoPoint3(p.X, p.Z, p.Y);
                    case "XZ": return new GeoPoint3(p.Z, p.Y, p.X);
                    default: return p;
                }
            }

            return new GeoAabb3(Of(box.Min), Of(box.Max));
        }

        private static List<(GeoAabb3, int, bool)> Entries(IEnumerable<GeoAabb3> boxes) => boxes.Select((b, i) => (b, i, true)).ToList();

        /// <summary>
        /// Ten bars 600 long along X, side by side along Y at a pitch of 30, all at the same height: every pair overlaps
        /// along X and along Z, and none along Y.
        /// </summary>
        private static IEnumerable<GeoAabb3> Row(string swap)
            => Enumerable.Range(0, 10).Select(i => Swapped(swap, new GeoAabb3(new GeoPoint3(0, 30.0 * i, 0), new GeoPoint3(600, 30.0 * i + 20, 20))));

        [Theory]
        [InlineData("", 1)]
        [InlineData("XY", 0)]
        [InlineData("YZ", 2)]
        [InlineData("XZ", 1)]
        public void TheBoxesAreSweptAlongTheAxisTheBarsStandSideBySideAlong(string swap, int expected)
        {
            Assert.Equal(expected, Clash3.SweepAxis(Entries(Row(swap)), 5.0));
        }

        [Fact]
        public void BoxesUpTheDiagonalAreSweptAlongX_WhereEveryAxisLeavesAsManyPairs()
        {
            IEnumerable<GeoAabb3> boxes = Enumerable.Range(0, 10).Select(i => new GeoAabb3(new GeoPoint3(i, i, i), new GeoPoint3(i + 1.5, i + 1.5, i + 1.5)));

            Assert.Equal(0, Clash3.SweepAxis(Entries(boxes), 0.0));
        }

        [Fact]
        public void BarsAsFarApartAsTheReach_AreSweptAcross()
        {
            // 25 apart along Y, 20 wide: a reach of 5 makes every two neighbours a pair, and Y leaves 9, against 45
            // along X and Z.
            IEnumerable<GeoAabb3> boxes = Enumerable.Range(0, 10).Select(i => new GeoAabb3(new GeoPoint3(0, 25.0 * i, 0), new GeoPoint3(600, 25.0 * i + 20, 20)));

            Assert.Equal(1, Clash3.SweepAxis(Entries(boxes), 5.0));
            Assert.Equal(9, Clash3.SweepBoxes(boxes.ToArray(), new GeoAabb3[0], true, 5.0).Count);
            Assert.Empty(Clash3.SweepBoxes(boxes.ToArray(), new GeoAabb3[0], true, 4.999));
        }

        /// <summary>
        /// Boxes from a seed at whole millimetres, so that many start, end or stand a reach apart at the same place, a
        /// few of them empty; long along an axis as a swap asks, or of every shape.
        /// </summary>
        private static GeoAabb3[] Scattered(int seed, int count, string swap)
        {
            var random = new Random(seed);
            var boxes = new GeoAabb3[count];

            for (int i = 0; i < count; i++)
            {
                if (random.Next(20) == 0)
                {
                    boxes[i] = GeoAabb3.Empty;
                    continue;
                }

                var low = new GeoPoint3(random.Next(40) * 5, random.Next(40) * 5, random.Next(40) * 5);
                var size = swap == null
                    ? new GeoVector3(random.Next(1, 40) * 5, random.Next(1, 40) * 5, random.Next(1, 40) * 5)
                    : new GeoVector3(1000 + random.Next(10), random.Next(1, 4) * 5, random.Next(1, 4) * 5);
                boxes[i] = Swapped(swap ?? string.Empty, new GeoAabb3(low, low.Add(size)));
            }

            return boxes;
        }

        /// <summary>
        /// The pairs whose boxes come within the reach of each other along every axis, each way round, found by trying
        /// every pair: of one set, lower index first; of two, the first set's first.
        /// </summary>
        private static List<(int, int)> EveryPair(GeoAabb3[] first, GeoAabb3[] second, bool oneSet, double reach)
        {
            bool Apart(double aMin, double aMax, double bMin, double bMax) => aMin > bMax + reach || bMin > aMax + reach;

            bool Near(GeoAabb3 a, GeoAabb3 b) => !a.IsEmpty && !b.IsEmpty
                && !Apart(a.Min.X, a.Max.X, b.Min.X, b.Max.X) && !Apart(a.Min.Y, a.Max.Y, b.Min.Y, b.Max.Y) && !Apart(a.Min.Z, a.Max.Z, b.Min.Z, b.Max.Z);

            var pairs = new List<(int, int)>();
            GeoAabb3[] other = oneSet ? first : second;

            for (int i = 0; i < first.Length; i++)
            {
                for (int j = oneSet ? i + 1 : 0; j < other.Length; j++)
                {
                    if (Near(first[i], other[j]))
                    {
                        pairs.Add((i, j));
                    }
                }
            }

            return pairs;
        }

        private static List<(int, int)> Sorted(List<(int, int)> pairs) => pairs.OrderBy(p => p.Item1).ThenBy(p => p.Item2).ToList();

        [Theory]
        [InlineData(1, null)]
        [InlineData(2, null)]
        [InlineData(3, "")]
        [InlineData(4, "XY")]
        [InlineData(5, "YZ")]
        [InlineData(6, "XZ")]
        public void OneSetOfBoxes_PairsAsEveryPairTried(int seed, string swap)
        {
            foreach (double reach in new[] { 0.0, 5.0, 5.001 })
            {
                GeoAabb3[] boxes = Scattered(seed, 300, swap);

                Assert.Equal(EveryPair(boxes, null, true, reach), Sorted(Clash3.SweepBoxes(boxes, new GeoAabb3[0], true, reach)));
            }
        }

        [Theory]
        [InlineData(11, null)]
        [InlineData(12, "")]
        [InlineData(13, "XY")]
        [InlineData(14, "YZ")]
        [InlineData(15, "XZ")]
        public void TwoSetsOfBoxes_PairAsEveryPairTried(int seed, string swap)
        {
            foreach (double reach in new[] { 0.0, 5.0, 5.001 })
            {
                GeoAabb3[] first = Scattered(seed, 200, swap);
                GeoAabb3[] second = Scattered(seed + 100, 150, swap);

                Assert.Equal(EveryPair(first, second, false, reach), Sorted(Clash3.SweepBoxes(first, second, false, reach)));
            }
        }

        [Theory]
        [InlineData('X')]
        [InlineData('Y')]
        [InlineData('Z')]
        public void TheBundle_PairsAsEveryPairTried(char axis)
        {
            GeoAabb3[] boxes = Bundle(axis).ToArray();
            GeoAabb3[] first = boxes.Where((b, i) => InFirstSet(i)).ToArray();
            GeoAabb3[] second = boxes.Where((b, i) => !InFirstSet(i)).ToArray();
            double reach = Options.Clearance + Within.EqualPoint;

            Assert.Equal(EveryPair(boxes, null, true, reach), Sorted(Clash3.SweepBoxes(boxes, new GeoAabb3[0], true, reach)));
            Assert.Equal(EveryPair(first, second, false, reach), Sorted(Clash3.SweepBoxes(first, second, false, reach)));
        }
    }
}
