using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using GeometryHelper.Geometry;
using GeometryHelper.Takeoff;

namespace GeometryHelper.UnitTest.Takeoff
{
    /// <summary>
    /// What the tests of the volume takeoff share: boxes lined up with the axes, each built as the library builds a box, an
    /// oracle that works out by itself, with no call into the library, what each box keeps, and the check that holds a run
    /// to the oracle.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The oracle cuts space into cells by every x, y and z any box or opening has, so that each cell lies wholly inside
    /// or wholly outside each box. A cell belongs to the highest-ranked box whose material holds it, the rank being
    /// (Priority descending, index ascending), as the takeoff ranks its items. A box's net volume is the sum of the cells it owns,
    /// what it gives up to box k the sum of its cells k owns, and the union the sum of every cell owned at all.
    /// </para>
    /// <para>
    /// The corners are whole millimetres, so every cell is a whole number of cubic millimetres, at most some 10^10, and
    /// every sum here is exact: the oracle is the answer to the last digit, not an estimate of it.
    /// </para>
    /// </remarks>
    internal static class VolumeTakeoffTestKit
    {
        /// <summary>
        /// How far a run's volumes may stand from the oracle's, as a share of the item's gross volume: a part of a
        /// billion, as SPEC D14 and A10 put it.
        /// </summary>
        internal const double Relative = 1E-9;

        #region Boxes for the volume takeoff, built by the library's own box

        /// <summary>
        /// An axis-aligned box from (x0, y0, z0) to (x1, y1, z1), with any openings it carries, each a box itself.
        /// </summary>
        internal sealed class Box
        {
            internal Box(double x0, double y0, double z0, double x1, double y1, double z1, params Box[] openings)
            {
                Min = new[] { x0, y0, z0 };
                Max = new[] { x1, y1, z1 };
                Openings = openings ?? new Box[0];
            }

            internal double[] Min { get; }

            internal double[] Max { get; }

            internal IReadOnlyList<Box> Openings { get; }

            /// <summary>The box as a solid: <see cref="GeoObb3.ToSolid"/> of the box, its openings carried as openings.</summary>
            internal GeoSolid3 ToSolid()
            {
                GeoSolid3 solid = Outline(this);
                return Openings.Count == 0 ? solid : solid.WithOpenings(Openings.Select(Outline));
            }

            /// <summary>Whether the material holds a point: inside the box and inside none of its openings.</summary>
            internal bool Holds(double x, double y, double z)
                => Inside(this, x, y, z) && !Openings.Any(opening => Inside(opening, x, y, z));

            public override string ToString()
            {
                string text = string.Format(CultureInfo.InvariantCulture, "[{0} {1} {2} - {3} {4} {5}]", Min[0], Min[1], Min[2], Max[0], Max[1], Max[2]);
                return Openings.Count == 0 ? text : text + " less " + string.Join(", ", Openings);
            }

            private static GeoSolid3 Outline(Box box)
                => new GeoAabb3(new GeoPoint3(box.Min[0], box.Min[1], box.Min[2]), new GeoPoint3(box.Max[0], box.Max[1], box.Max[2])).ToObb().ToSolid();

            // Strictly inside: the oracle only asks at the middle of a cell, which never lies on a face.
            private static bool Inside(Box box, double x, double y, double z)
                => box.Min[0] < x && x < box.Max[0] && box.Min[1] < y && y < box.Max[1] && box.Min[2] < z && z < box.Max[2];
        }

        /// <summary>
        /// A set of boxes to take off, each with its priority; a null box stands for a null entry in the list of items.
        /// <see cref="SameInstanceAs"/> says, for each box, the index of an earlier box whose very solid it is to share, or -1.
        /// </summary>
        internal sealed class BoxSet
        {
            internal BoxSet(IReadOnlyList<Box> boxes, IReadOnlyList<int> priorities, IReadOnlyList<int> sameInstanceAs = null)
            {
                Boxes = boxes;
                Priorities = priorities;
                SameInstanceAs = sameInstanceAs ?? boxes.Select(_ => -1).ToArray();
            }

            internal IReadOnlyList<Box> Boxes { get; }

            internal IReadOnlyList<int> Priorities { get; }

            internal IReadOnlyList<int> SameInstanceAs { get; }

            /// <summary>The items to run: one per box, named by its index, null where the box is null.</summary>
            internal VolumeItem[] ToItems()
            {
                var solids = new GeoSolid3[Boxes.Count];
                var items = new VolumeItem[Boxes.Count];

                for (int i = 0; i < Boxes.Count; i++)
                {
                    if (Boxes[i] == null)
                    {
                        continue;
                    }

                    solids[i] = SameInstanceAs[i] >= 0 ? solids[SameInstanceAs[i]] : Boxes[i].ToSolid();
                    items[i] = new VolumeItem(solids[i], "#" + i.ToString(CultureInfo.InvariantCulture), Priorities[i]);
                }

                return items;
            }

            public override string ToString()
            {
                var text = new StringBuilder();
                for (int i = 0; i < Boxes.Count; i++)
                {
                    text.AppendFormat(CultureInfo.InvariantCulture, "  #{0} priority {1}{2}: {3}\n", i, Priorities[i], SameInstanceAs[i] >= 0 ? " (the solid of #" + SameInstanceAs[i] + ")" : "", Boxes[i]?.ToString() ?? "null");
                }

                return text.ToString();
            }
        }

        /// <summary>
        /// A random set for the fuzz, from its seed: 2 to 12 boxes with corners on a grid 100 apart in a cube 600 across,
        /// each 1 to 4 steps long on every axis, so that flush faces are common; one in four a copy of an earlier box,
        /// half of those its very solid; priorities from -2 to 2, so that equal priorities are common too.
        /// </summary>
        internal static BoxSet RandomSet(int seed)
        {
            var random = new Random(seed);
            int count = random.Next(2, 13);
            var boxes = new Box[count];
            var priorities = new int[count];
            var same = new int[count];

            for (int i = 0; i < count; i++)
            {
                priorities[i] = random.Next(-2, 3);
                same[i] = -1;

                if (i > 0 && random.Next(4) == 0)
                {
                    int of = random.Next(i);
                    boxes[i] = boxes[of];
                    same[i] = random.Next(2) == 0 ? of : -1;
                    continue;
                }

                var min = new double[3];
                var max = new double[3];
                for (int axis = 0; axis < 3; axis++)
                {
                    int from = random.Next(0, 6);
                    int to = Math.Min(6, from + random.Next(1, 5));
                    min[axis] = 100.0 * from;
                    max[axis] = 100.0 * to;
                }

                boxes[i] = new Box(min[0], min[1], min[2], max[0], max[1], max[2]);
            }

            return new BoxSet(boxes, priorities, same);
        }

        #endregion

        #region The sets of the fixed cases

        /// <summary>
        /// A slab, a beam and a column meeting at a joint, listed slab first: the slab 3 000 by 3 000 by 200 at the top,
        /// priority 1; the beam 3 000 long, 400 wide and 400 deep under it, its top flush with the slab's, priority 2; and
        /// the column 300 by 400 and 1 000 high, its top flush too and its sides flush with the beam's, priority 3.
        /// </summary>
        /// <remarks>
        /// The block all three share is 300 by 400 by 200, 24 000 000. The column keeps all of itself, 120 000 000. The
        /// beam gives the column 300 by 400 by 400, 48 000 000, and keeps 432 000 000. The slab gives the column the
        /// shared block and the beam the rest of what lies over it, 3 000 by 400 by 200 less the block, 216 000 000, and
        /// keeps 1 800 000 000 less 240 000 000, 1 560 000 000. The union is 2 112 000 000.
        /// </remarks>
        internal static BoxSet Joint() => new BoxSet(
            new[]
            {
                new Box(-1000, -1000, 800, 2000, 2000, 1000),
                new Box(-1000, 0, 600, 2000, 400, 1000),
                new Box(0, 0, 0, 300, 400, 1000),
            },
            new[] { 1, 2, 3 });

        /// <summary>
        /// Four cubes 1 000 across, of one priority, laid 800 apart in a square so that each pair beside each other shares
        /// a slab 200 thick and all four share the post 200 by 200 by 1 000 at the middle.
        /// </summary>
        /// <remarks>
        /// In list order: the first keeps 1 000 000 000. The second gives the first 200 000 000. The third gives the
        /// first 200 000 000, and the second nothing, since all it shares with the second is the post the first has
        /// taken. The fourth gives the first the post, 40 000 000, and the second and the third 160 000 000 each, keeping
        /// 640 000 000. The union is the square 1 800 across, 3 240 000 000.
        /// </remarks>
        internal static BoxSet FourAtACorner() => new BoxSet(
            new[]
            {
                new Box(0, 0, 0, 1000, 1000, 1000),
                new Box(800, 0, 0, 1800, 1000, 1000),
                new Box(0, 800, 0, 1000, 1800, 1000),
                new Box(800, 800, 0, 1800, 1800, 1000),
            },
            new[] { 0, 0, 0, 0 });

        /// <summary>
        /// Three copies of a block 1 000 by 500 by 300, 150 000 000, of one priority, each a solid of its own.
        /// </summary>
        internal static BoxSet ThreeCopies() => new BoxSet(
            new[] { new Box(0, 0, 0, 1000, 500, 300), new Box(0, 0, 0, 1000, 500, 300), new Box(0, 0, 0, 1000, 500, 300) },
            new[] { 0, 0, 0 });

        /// <summary>
        /// A block 300 across, 27 000 000, held wholly inside a cube 1 000 across clear of its faces, the block listed
        /// first; the priorities as given.
        /// </summary>
        internal static BoxSet BlockInACube(int blockPriority, int cubePriority) => new BoxSet(
            new[] { new Box(200, 300, 400, 500, 600, 700), new Box(0, 0, 0, 1000, 1000, 1000) },
            new[] { blockPriority, cubePriority });

        /// <summary>
        /// A cube 1 000 across with four more about it touching it only: one flush against its face at x = 1 000, one
        /// along its edge at x = y = 1 000, one at its corner, and a post 400 by 400 standing on its top; all of one
        /// priority, the cube first.
        /// </summary>
        internal static BoxSet Touching() => new BoxSet(
            new[]
            {
                new Box(0, 0, 0, 1000, 1000, 1000),
                new Box(1000, 0, 0, 2000, 1000, 1000),
                new Box(1000, 1000, 0, 2000, 2000, 1000),
                new Box(1000, 1000, 1000, 2000, 2000, 2000),
                new Box(200, 200, 1000, 600, 600, 1400),
            },
            new[] { 0, 0, 0, 0, 0 });

        /// <summary>
        /// Two cubes 1 000 across, the second moved 500 along x, so that they share half of each, 500 000 000; the
        /// priorities as given.
        /// </summary>
        internal static BoxSet HalfOverlap(int firstPriority, int secondPriority) => new BoxSet(
            new[] { new Box(0, 0, 0, 1000, 1000, 1000), new Box(500, 0, 0, 1500, 1000, 1000) },
            new[] { firstPriority, secondPriority });

        /// <summary>
        /// The two cubes of <see cref="HalfOverlap"/>, the first of priority <see cref="int.MinValue"/> and the second of
        /// <see cref="int.MaxValue"/>, and a post 500 by 500 by 2 000 of priority 0 standing through both where they meet.
        /// </summary>
        /// <remarks>
        /// Ranked: the second cube, the post, the first cube. The second keeps 1 000 000 000. The post gives it 250 by 500 by
        /// 1 000, 125 000 000, and keeps 375 000 000. The first gives the second 500 000 000 and the post the other
        /// 125 000 000 it holds of it, keeping 375 000 000. The union is 1 750 000 000.
        /// </remarks>
        internal static BoxSet ExtremePriorities() => new BoxSet(
            new[] { new Box(0, 0, 0, 1000, 1000, 1000), new Box(500, 0, 0, 1500, 1000, 1000), new Box(250, 250, -500, 750, 750, 1500) },
            new[] { int.MinValue, int.MaxValue, 0 });

        /// <summary>
        /// A slab 3 000 by 3 000 by 200, priority 0, with a duct 600 by 600 through it running 100 past both faces, and a
        /// column 400 by 400 by 2 000, priority 3, through the slab across a corner of the duct.
        /// </summary>
        /// <remarks>
        /// The slab's material is 1 800 000 000 less the duct's 72 000 000, 1 728 000 000. The column crosses 400 by 400
        /// of the slab, 32 000 000, of which 200 by 200 by 200, 8 000 000, is in the duct: the slab gives it 24 000 000 and
        /// keeps 1 704 000 000. The column keeps all of itself, 320 000 000.
        /// </remarks>
        internal static BoxSet ColumnThroughADuct() => new BoxSet(
            new[]
            {
                new Box(0, 0, 0, 3000, 3000, 200, new Box(1000, 1000, -100, 1600, 1600, 300)),
                new Box(1400, 1400, -1000, 1800, 1800, 1000),
            },
            new[] { 0, 3 });

        /// <summary>
        /// A hollow column, 400 by 400 by 2 000 with a duct 200 by 200 through its length running 100 past both ends,
        /// priority 3, through a slab 2 400 by 2 400 by 200, priority 0.
        /// </summary>
        /// <remarks>
        /// The column's material is 400 by 400 less 200 by 200, 120 000, by 2 000: 240 000 000. Of the slab's
        /// 1 152 000 000 it takes 120 000 by 200, 24 000 000; the slab keeps the 8 000 000 inside the duct, and
        /// 1 128 000 000 in all. The union is 1 368 000 000.
        /// </remarks>
        internal static BoxSet HollowColumnThroughASlab() => new BoxSet(
            new[]
            {
                new Box(-1000, -1000, 0, 1400, 1400, 200),
                new Box(0, 0, -1000, 400, 400, 1000, new Box(100, 100, -1100, 300, 300, 1100)),
            },
            new[] { 0, 3 });

        /// <summary>
        /// A cube 1 000 across with a corner notch 500 by 500 cut through its height, priority 0, and a block filling the
        /// notch flush, priority 1: their boxes overlap by 500 by 500 by 1 000, their material not at all.
        /// </summary>
        /// <remarks>The notched cube holds 750 000 000, the block 250 000 000.</remarks>
        internal static BoxSet BlockInANotch() => new BoxSet(
            new[]
            {
                new Box(0, 0, 0, 1000, 1000, 1000, new Box(500, 500, -100, 1100, 1100, 1100)),
                new Box(500, 500, 0, 1000, 1000, 1000),
            },
            new[] { 0, 1 });

        /// <summary>
        /// Two copies of a block 100 by 200 by 300, 6 000 000, of priority 0, and a bar 400 by 100 by 100 of priority 1
        /// running through both, 1 000 000 of each: the later copy is taken by two parts, the bar and the first copy.
        /// </summary>
        /// <remarks>
        /// The first copy gives the bar 1 000 000 and keeps 5 000 000. The later gives the bar 1 000 000 and the first
        /// copy the 5 000 000 left, keeping nothing. Taken off by hand at the pin, as the spec takes it, the later copy's
        /// two deductions come to 9.3E-10 more than its gross, a part in 10^16: its net clamps to nought on rounding.
        /// </remarks>
        internal static BoxSet CopiesWithABarThrough() => new BoxSet(
            new[] { new Box(0, 0, 0, 100, 200, 300), new Box(0, 0, 0, 100, 200, 300), new Box(0, 0, 100, 400, 100, 200) },
            new[] { 0, 0, 1 });

        #endregion

        #region The oracle: cells of the boxes, each owned by the highest-ranked box holding it

        /// <summary>What the oracle works out for a set of boxes.</summary>
        internal sealed class Oracle
        {
            private Oracle(int count)
            {
                Gross = new double[count];
                Net = new double[count];
                Taken = new double[count, count];
            }

            /// <summary>The material of each box; 0 for a null one.</summary>
            internal double[] Gross { get; }

            /// <summary>The cells each box owns.</summary>
            internal double[] Net { get; }

            /// <summary>Taken[i, k]: what box k, ranked above box i, owns of the material of box i.</summary>
            internal double[,] Taken { get; }

            /// <summary>The indices of the boxes not null, highest-ranked first.</summary>
            internal int[] Ranked { get; private set; }

            /// <summary>The volume of the union, every cell owned at all.</summary>
            internal double Union { get; private set; }

            /// <summary>What box i gives up, (keeper, volume), the keepers in rank order and none that takes nothing.</summary>
            internal IReadOnlyList<(int By, double Volume)> Deductions(int i)
                => Ranked.Where(k => Taken[i, k] > 0.0).Select(k => (k, Taken[i, k])).ToList();

            internal static Oracle Of(BoxSet set)
            {
                int count = set.Boxes.Count;
                var oracle = new Oracle(count);
                oracle.Ranked = Enumerable.Range(0, count).Where(i => set.Boxes[i] != null)
                    .OrderByDescending(i => set.Priorities[i]).ThenBy(i => i).ToArray();

                double[][] cuts = Enumerable.Range(0, 3).Select(axis => Coordinates(set.Boxes, axis)).ToArray();

                for (int ix = 0; ix + 1 < cuts[0].Length; ix++)
                {
                    for (int iy = 0; iy + 1 < cuts[1].Length; iy++)
                    {
                        for (int iz = 0; iz + 1 < cuts[2].Length; iz++)
                        {
                            double x = (cuts[0][ix] + cuts[0][ix + 1]) / 2.0;
                            double y = (cuts[1][iy] + cuts[1][iy + 1]) / 2.0;
                            double z = (cuts[2][iz] + cuts[2][iz + 1]) / 2.0;
                            double volume = (cuts[0][ix + 1] - cuts[0][ix]) * (cuts[1][iy + 1] - cuts[1][iy]) * (cuts[2][iz + 1] - cuts[2][iz]);

                            int owner = -1;
                            foreach (int i in oracle.Ranked)
                            {
                                if (!set.Boxes[i].Holds(x, y, z))
                                {
                                    continue;
                                }

                                oracle.Gross[i] += volume;
                                if (owner < 0)
                                {
                                    owner = i;
                                    oracle.Net[i] += volume;
                                    oracle.Union += volume;
                                }
                                else
                                {
                                    oracle.Taken[i, owner] += volume;
                                }
                            }
                        }
                    }
                }

                return oracle;
            }

            private static double[] Coordinates(IReadOnlyList<Box> boxes, int axis)
            {
                var all = new List<double>();
                foreach (Box box in boxes.Where(b => b != null))
                {
                    foreach (Box part in new[] { box }.Concat(box.Openings))
                    {
                        all.Add(part.Min[axis]);
                        all.Add(part.Max[axis]);
                    }
                }

                return all.Distinct().OrderBy(v => v).ToArray();
            }
        }

        #endregion

        #region Holding a run to the oracle

        /// <summary>
        /// Every way the results of a run stand off the oracle, one line each; empty when they agree.
        /// </summary>
        /// <remarks>
        /// Checked: one result per item, in the order of the items, each holding its own item; the gross, net and each
        /// deduction within <see cref="Relative"/> of the oracle's gross; the deductions by exactly the keepers the oracle
        /// names, in rank order; the deducted volume the sum of the deductions in their order, and the net the gross less
        /// it, both to the bit; <see cref="VolumeTakeoffResult.IsExact"/> as the issues say; the nets summing to the
        /// union within <see cref="Relative"/> of the gross of them all; and no issue on any item, a part left nothing too: its
        /// deductions may come to a rounding more than its gross, and a clamp within the point tolerance times its area is
        /// no issue.
        /// </remarks>
        internal static List<string> Disagreements(VolumeItem[] items, IReadOnlyList<VolumeTakeoffResult> results, Oracle oracle)
        {
            var wrong = new List<string>();
            if (results.Count != items.Length)
            {
                wrong.Add($"{results.Count} results for {items.Length} items");
                return wrong;
            }

            double sumNet = 0.0;
            for (int i = 0; i < items.Length; i++)
            {
                VolumeTakeoffResult result = results[i];
                double scale = Relative * oracle.Gross[i];
                if (!ReferenceEquals(items[i], result.Item))
                {
                    wrong.Add($"#{i}: the result holds another item");
                }

                Near(wrong, $"#{i} gross", oracle.Gross[i], result.GrossVolume, scale);
                Near(wrong, $"#{i} net", oracle.Net[i], result.NetVolume, scale);

                IReadOnlyList<(int By, double Volume)> expected = oracle.Deductions(i);
                string by = string.Join(" ", result.Deductions.Select(d => d.ByIndex));
                string expectedBy = string.Join(" ", expected.Select(d => d.By));
                if (by != expectedBy)
                {
                    wrong.Add($"#{i}: deductions by [{by}], the oracle's by [{expectedBy}]");
                }
                else
                {
                    for (int d = 0; d < expected.Count; d++)
                    {
                        Near(wrong, $"#{i} deduction by #{expected[d].By}", expected[d].Volume, result.Deductions[d].Volume, scale);
                    }
                }

                double deducted = 0.0;
                foreach (VolumeDeduction deduction in result.Deductions)
                {
                    deducted += deduction.Volume;
                }

                if (!BitEqual(deducted, result.DeductedVolume))
                {
                    wrong.Add($"#{i}: deducted {result.DeductedVolume:R}, its deductions summing to {deducted:R}");
                }

                double net = Math.Min(Math.Max(result.GrossVolume - result.DeductedVolume, 0.0), result.GrossVolume);
                if (!BitEqual(net, result.NetVolume))
                {
                    wrong.Add($"#{i}: net {result.NetVolume:R}, gross less deducted {net:R}");
                }

                if (result.IsExact != (result.Issues.Count == 0))
                {
                    wrong.Add($"#{i}: IsExact {result.IsExact} with {result.Issues.Count} issues");
                }

                if (items[i] != null && result.Issues.Count > 0)
                {
                    wrong.Add($"#{i}: issues {string.Join(" / ", result.Issues)}");
                }

                sumNet += result.NetVolume;
            }

            Near(wrong, "the nets summed against the union", oracle.Union, sumNet, Relative * oracle.Gross.Sum());
            return wrong;
        }

        /// <summary>Every way two runs differ, field by field and bit for bit; empty when they are the same.</summary>
        internal static List<string> Differences(IReadOnlyList<VolumeTakeoffResult> a, IReadOnlyList<VolumeTakeoffResult> b)
        {
            var differ = new List<string>();
            if (a.Count != b.Count)
            {
                differ.Add($"{a.Count} results against {b.Count}");
                return differ;
            }

            for (int i = 0; i < a.Count; i++)
            {
                if (!BitEqual(a[i].GrossVolume, b[i].GrossVolume) || !BitEqual(a[i].DeductedVolume, b[i].DeductedVolume) || !BitEqual(a[i].NetVolume, b[i].NetVolume))
                {
                    differ.Add($"#{i}: gross/deducted/net {a[i].GrossVolume:R}/{a[i].DeductedVolume:R}/{a[i].NetVolume:R} against {b[i].GrossVolume:R}/{b[i].DeductedVolume:R}/{b[i].NetVolume:R}");
                }

                string da = string.Join(" ", a[i].Deductions.Select(d => d.ByIndex + ":" + d.Volume.ToString("R", CultureInfo.InvariantCulture)));
                string db = string.Join(" ", b[i].Deductions.Select(d => d.ByIndex + ":" + d.Volume.ToString("R", CultureInfo.InvariantCulture)));
                if (da != db)
                {
                    differ.Add($"#{i}: deductions [{da}] against [{db}]");
                }

                if (!a[i].Issues.SequenceEqual(b[i].Issues) || a[i].IsExact != b[i].IsExact)
                {
                    differ.Add($"#{i}: issues [{string.Join(" / ", a[i].Issues)}] against [{string.Join(" / ", b[i].Issues)}]");
                }
            }

            return differ;
        }

        private static void Near(List<string> wrong, string what, double expected, double actual, double within)
        {
            if (!(Math.Abs(actual - expected) <= within))
            {
                wrong.Add(string.Format(CultureInfo.InvariantCulture, "{0}: {1:R}, the oracle's {2:R}, off by {3:G3}", what, actual, expected, actual - expected));
            }
        }

        private static bool BitEqual(double a, double b) => BitConverter.DoubleToInt64Bits(a) == BitConverter.DoubleToInt64Bits(b);

        #endregion
    }
}
