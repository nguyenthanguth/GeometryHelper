using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Threading.Tasks;
using GeometryHelper.Core;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;

namespace GeometryHelper.Takeoff
{
    /// <summary>
    /// Takes off the volumes of the parts of a model, each bit of material counted once: by the part ranked first
    /// among those holding it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The parts are ranked by their <see cref="VolumeItem.Priority"/>, the highest first, of two the same by their
    /// <see cref="VolumeItem.Id"/>, the largest first and any id before none, and then by their order in the list. A
    /// part keeps all it shares with the parts ranked after it, and loses to them nothing. Where a
    /// slab 200 thick sits on a beam 300 wide that runs into a column 400 by 400, and the column is ranked first, the
    /// beam second and the slab third, the block the three share, 300 by 400 by 200, is the column's: the slab loses it
    /// to the column, and to the beam only what the beam shares with it beyond the column. Taken off by each part it
    /// meets, the slab would lose those 24 000 000 twice. So the net volumes add up to the volume of all the parts
    /// together, and two exact copies count once: the later comes out nought.
    /// </para>
    /// <para>
    /// The bodies given are never cut; only what they share is. First every pair whose boxes overlap by more than the
    /// point tolerance has its common part worked out, by
    /// <see cref="Boolean3.TryIntersect(GeoSolid3, GeoSolid3, out GeoSolid3, SolidBooleanOptions, out BooleanOutcome)"/>
    /// of the part ranked later with the part ranked first, which is put onto it as the contact says. Then each part's
    /// pieces, in the order of the parts that keep them, have the pieces before them taken out, by
    /// <see cref="Boolean3.TrySubtractAll(GeoSolid3, IEnumerable{GeoSolid3}, out GeoSolid3, SolidBooleanOptions, out SubtractReport)"/>,
    /// so that each is what that part keeps and no part before it kept already. Both steps run in parallel, and the
    /// volumes come out the same, bit for bit, on one thread or on every processor.
    /// </para>
    /// <para>
    /// A common part the booleans cannot make, one that is not valid, holds more than the smaller of the two, or cannot
    /// be worked out at all, is worked out by slicing instead: the part is cut by parallel planes, and the area of what it
    /// shares with the keeper and with no keeper ranked before is added up the height, exactly over flat faces and with
    /// no tolerance. So is a piece the cut of the pieces before it cannot finish, or that has a keeper before it whose
    /// overlap was sliced. A curved wall of 354 faces and a curved
    /// girder of 322 of a Tekla model, met within a thousandth with a contact of a hundredth, share a common part of
    /// 4 256 faces that is not valid; slicing gives them 2 226 236 967 in 0.04 seconds, where Tekla's own boolean gives
    /// 2 226 236 773 cutting the wall and 2 226 236 807 cutting the girder. A deduction worked out by slicing is exact,
    /// and carries no issue. Only a pair that cannot be sliced either is not taken off: a section of a body crossed an
    /// odd number of times, as where a corner sits more than about 1E-9 off its neighbour's edge, a T-junction, even
    /// in a body <see cref="GeoSolid3.Validate(Tolerance)"/> accepts. Its result says so in
    /// <see cref="VolumeTakeoffResult.Issues"/>, and the net volume is then no less than it should be.
    /// No failure of the geometry escapes: each comes back as an issue and is logged.
    /// </para>
    /// <para>
    /// A common part thinner on average than half the point tolerance, its volume no more than the point tolerance
    /// times half its area, is taken as touching, and nothing is taken off for it: a beam standing on a slab, flush
    /// with it or a hair into it, loses nothing. A volume worked out by slicing has no body to measure, and is judged by
    /// the least area a body of its volume can have, a ball's; slicing snaps nothing, so a sheet a hair thick that the
    /// contact would have put away is taken off as the faces give it.
    /// </para>
    /// <para>
    /// The tolerance the booleans work within is the one the options carry. A scope opened with
    /// <see cref="Tolerance.Use(Tolerance)"/> around the call is opened on every worker as well, so that it reaches the
    /// few steps underneath that read the global tolerance whichever thread runs them.
    /// </para>
    /// </remarks>
    public static class VolumeTakeoff
    {
        /// <summary>
        /// Takes off the volumes of the parts with the default options.
        /// </summary>
        /// <param name="items">
        /// The parts. A null entry is passed over: its result has no item, every volume nought and one issue, "no item".
        /// </param>
        /// <returns>The take-off of each part, in the order the parts were given.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="items"/> is null.</exception>
        public static VolumeTakeoffResult[] Run(IReadOnlyList<VolumeItem> items) => Run(items, VolumeTakeoffOptions.Default);

        /// <summary>
        /// Takes off the volumes of the parts.
        /// </summary>
        /// <param name="items">
        /// The parts. A null entry is passed over: its result has no item, every volume nought and one issue, "no item".
        /// The same body may be given in two items, and the later loses all of it to the earlier.
        /// </param>
        /// <param name="options">How each overlap is worked out, and how many threads to use.</param>
        /// <returns>The take-off of each part, in the order the parts were given.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="items"/> or <paramref name="options"/> is null.</exception>
        /// <remarks>
        /// <para>
        /// A column 400 by 400 and 3 200 high, ranked first, a beam 300 wide, 600 deep and 6 000 long running through it,
        /// second, and a slab 6 000 by 6 000 by 200 on both, third, with the tops of the three flush: the column keeps
        /// its 512 000 000; the beam, 1 080 000 000 gross, loses 72 000 000 to the column and keeps 1 008 000 000; the
        /// slab, 7 200 000 000 gross, loses 32 000 000 to the column and 336 000 000 to the beam and keeps
        /// 6 832 000 000. The three add up to 8 352 000 000, the volume of the three together.
        /// </para>
        /// <para>
        /// A common part is checked by <see cref="GeoSolid3.Validate(Tolerance)"/> and by its volume, which can be no more
        /// than the smaller part's, within the point tolerance times the area of the two. A valid body can still have lost
        /// part of what the two share, since nothing bounds a common part from below; a take-off that matters is worth
        /// checking against the parts' net bodies cut by
        /// <see cref="Boolean3.TrySubtractAll(GeoSolid3, IEnumerable{GeoSolid3}, out GeoSolid3, SolidBooleanOptions, out SubtractReport)"/>.
        /// </para>
        /// </remarks>
        public static VolumeTakeoffResult[] Run(IReadOnlyList<VolumeItem> items, VolumeTakeoffOptions options)
        {
            if (items == null)
            {
                throw new ArgumentNullException(nameof(items));
            }

            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            Stopwatch watch = Stopwatch.StartNew();
            SolidBooleanOptions boolean = options.Boolean;
            Tolerance tolerance = boolean.Tolerance;
            var threads = new ParallelOptions { MaxDegreeOfParallelism = options.MaxDegreeOfParallelism };

            // A scope the caller opened with Tolerance.Use holds on the calling thread alone, and a few booleans read the
            // global tolerance underneath, so the same scope is opened on every worker: whichever thread takes a pair or a
            // step, it is worked out the same way.
            Tolerance caller = Tolerance.Global;

            var given = new VolumeItem[items.Count];

            for (int i = 0; i < given.Length; i++)
            {
                given[i] = items[i];
            }

            // Each part measured once: its own volume, its area for the slack of a boolean, and its box.
            var parts = new Part[given.Length];

            Parallel.For(0, given.Length, threads, i =>
            {
                if (given[i] != null)
                {
                    using (Tolerance.Use(caller))
                    {
                        parts[i] = Measure(i, given[i], tolerance);
                    }
                }
            });

            Rank(parts);

            // Phase 1: the common part of each pair, the heaviest pairs first so that no thread is left with one at the end.
            Pair[] pairs = FindPairs(parts, tolerance.EqualPoint);
            var pairWeights = new long[pairs.Length];

            for (int k = 0; k < pairs.Length; k++)
            {
                pairWeights[k] = (long)pairs[k].Loser.Item.Solid.Faces.Count + pairs[k].Keeper.Item.Solid.Faces.Count;
            }

            Parallel.ForEach(Partitioner.Create(HeaviestFirst(pairWeights), EnumerablePartitionerOptions.NoBuffering), threads, k =>
            {
                using (Tolerance.Use(caller))
                {
                    WorkOut(pairs[k], boolean);
                }
            });

            // Phase 2: each piece less the pieces of the same part before it, in the order of the parts that keep them.
            var losing = new List<Pair>[parts.Length];
            int touching = 0, overlaps = 0;

            foreach (Pair pair in pairs)
            {
                if (pair.Kind == Kind.Touching)
                {
                    touching++;
                    continue;
                }

                overlaps++;
                int loser = pair.Loser.Index;
                (losing[loser] ?? (losing[loser] = new List<Pair>())).Add(pair);
            }

            List<Step> steps = PlanSteps(losing, tolerance.EqualPoint);
            var stepWeights = new long[steps.Count];

            for (int s = 0; s < steps.Count; s++)
            {
                stepWeights[s] = steps[s].Weight;
            }

            Parallel.ForEach(Partitioner.Create(HeaviestFirst(stepWeights), EnumerablePartitionerOptions.NoBuffering), threads, s =>
            {
                using (Tolerance.Use(caller))
                {
                    TakeOffOnce(steps[s], boolean);
                }
            });

            // The results, put together in the order of the items once both phases are done.
            var results = new VolumeTakeoffResult[given.Length];
            int issues = 0;

            for (int i = 0; i < given.Length; i++)
            {
                results[i] = parts[i] == null
                    ? new VolumeTakeoffResult(null, 0.0, 0.0, 0.0, new VolumeDeduction[0], new[] { "no item" })
                    : Result(parts[i], losing[i], tolerance);
                issues += results[i].Issues.Count;
            }

            if (touching > 0)
            {
                GeometryHelperLog.Debug("VolumeTakeoff: " + touching + " pairs only touch, and nothing is taken off for them.");
            }

            int slicedPairs = 0, slicedSteps = 0, unsliced = 0;

            foreach (Pair pair in pairs)
            {
                slicedPairs += pair.Kind == Kind.Sliced ? 1 : 0;
                slicedSteps += pair.Kind == Kind.Piece && pair.BySlicing ? 1 : 0;
                unsliced += pair.Issue != null ? 1 : 0;
            }

            if (slicedPairs + slicedSteps > 0)
            {
                GeometryHelperLog.Debug("VolumeTakeoff: " + slicedPairs + " pairs whose common part the booleans could not make, and " + slicedSteps
                    + " pieces whose overlaps before could not be cut out, or were sliced, worked out by slicing; " + unsliced + " of them could not be.");
            }

            GeometryHelperLog.Info("VolumeTakeoff: " + given.Length + " items, " + pairs.Length + " pairs, " + overlaps + " overlaps, " + issues + " issues, " + watch.ElapsedMilliseconds + " ms.");

            return results;
        }

        #region Parts and pairs

        /// <summary>
        /// Measures a part: its own volume, the area of its material, and its box.
        /// </summary>
        private static Part Measure(int index, VolumeItem item, Tolerance tolerance)
        {
            var part = new Part { Index = index, Item = item, Box = item.Solid.GetAabb() };

            try
            {
                if (!item.Solid.TryGetVolume(out double volume, tolerance))
                {
                    volume = item.Solid.GetVolume(tolerance);
                    part.GrossIssue = "the solid's own volume is not to be trusted: an opening could not be cut out, or the material does not close; the volume its faces give is taken";
                }

                part.Gross = volume;
                part.Area = item.Solid.GetSurfaceArea(tolerance);
            }
            catch (Exception exception) when (!(exception is OutOfMemoryException))
            {
                GeometryHelperLog.Warn("VolumeTakeoff: the volume of item " + index + " could not be measured, and is taken as nought.", exception);
                part.Gross = 0.0;
                part.Area = 0.0;
                part.GrossIssue = "the solid's own volume could not be measured: it is taken as 0";
            }

            return part;
        }

        /// <summary>
        /// Ranks the parts by their priority, the highest first; of two the same, one with an id before one without and
        /// the larger id first; and then by their index.
        /// </summary>
        private static void Rank(Part[] parts)
        {
            var present = new List<Part>();

            foreach (Part part in parts)
            {
                if (part != null)
                {
                    present.Add(part);
                }
            }

            // The higher priority first; of two the same, an id before none and the larger id first, then the earlier in the
            // list. CompareTo, not a subtraction, which would wrap round between int.MinValue and int.MaxValue.
            present.Sort((a, b) =>
            {
                if (a.Item.Priority != b.Item.Priority)
                {
                    return b.Item.Priority.CompareTo(a.Item.Priority);
                }

                if (a.Item.Id.HasValue != b.Item.Id.HasValue)
                {
                    return a.Item.Id.HasValue ? -1 : 1;
                }

                if (a.Item.Id.HasValue && a.Item.Id.Value != b.Item.Id.Value)
                {
                    return b.Item.Id.Value.CompareTo(a.Item.Id.Value);
                }

                return a.Index.CompareTo(b.Index);
            });

            for (int r = 0; r < present.Count; r++)
            {
                present[r].Rank = r;
            }
        }

        /// <summary>
        /// The pairs of parts whose boxes overlap by more than a reach along all three axes, each with the part that keeps
        /// what they share, in the order of the ranks of the keeper and then of the loser.
        /// </summary>
        /// <remarks>
        /// The boxes are swept along the axis that leaves the fewest pairs to try: along the axis a floor of slabs is thin
        /// in, or a run of beams long in, nearly every box overlaps nearly every other.
        /// </remarks>
        private static Pair[] FindPairs(Part[] parts, double reach)
        {
            var present = new List<Part>();

            foreach (Part part in parts)
            {
                if (part != null)
                {
                    present.Add(part);
                }
            }

            int count = present.Count;
            int chosen = 0;
            int[] order = null;
            double[] lows = null;
            long fewest = long.MaxValue;

            for (int axis = 0; axis < 3; axis++)
            {
                int[] sorted = SortedAlong(present, axis);
                var starts = new double[count];
                long tried = 0;

                for (int p = 0; p < count; p++)
                {
                    starts[p] = Low(present[sorted[p]].Box, axis);
                }

                // Each box is tried against every later one that starts before it ends, less the reach.
                for (int p = 0; p < count; p++)
                {
                    tried += Math.Max(0, FirstAtOrAbove(starts, High(present[sorted[p]].Box, axis) - reach) - p - 1);
                }

                if (tried < fewest)
                {
                    fewest = tried;
                    chosen = axis;
                    order = sorted;
                    lows = starts;
                }
            }

            var pairs = new List<Pair>();

            for (int p = 0; p < count; p++)
            {
                Part a = present[order[p]];
                double end = High(a.Box, chosen) - reach;

                for (int q = p + 1; q < count && lows[q] < end; q++)
                {
                    Part b = present[order[q]];

                    if (Overlap(a.Box, b.Box, reach))
                    {
                        pairs.Add(a.Rank < b.Rank ? new Pair { Keeper = a, Loser = b } : new Pair { Keeper = b, Loser = a });
                    }
                }
            }

            pairs.Sort((x, y) => x.Keeper.Rank != y.Keeper.Rank ? x.Keeper.Rank.CompareTo(y.Keeper.Rank) : x.Loser.Rank.CompareTo(y.Loser.Rank));

            return pairs.ToArray();
        }

        /// <summary>
        /// The places of the parts sorted by where their boxes start along an axis, and of two the same by their index.
        /// </summary>
        private static int[] SortedAlong(List<Part> present, int axis)
        {
            var sorted = new int[present.Count];

            for (int p = 0; p < sorted.Length; p++)
            {
                sorted[p] = p;
            }

            Array.Sort(sorted, (x, y) =>
            {
                int byLow = Low(present[x].Box, axis).CompareTo(Low(present[y].Box, axis));
                return byLow != 0 ? byLow : present[x].Index.CompareTo(present[y].Index);
            });

            return sorted;
        }

        /// <summary>
        /// The first place in sorted values holding one at or above a value; the count where none does.
        /// </summary>
        private static int FirstAtOrAbove(double[] sorted, double value)
        {
            int below = 0, above = sorted.Length;

            while (below < above)
            {
                int middle = (below + above) >> 1;

                if (sorted[middle] < value)
                {
                    below = middle + 1;
                }
                else
                {
                    above = middle;
                }
            }

            return below;
        }

        private static double Low(GeoAabb3 box, int axis) => axis == 0 ? box.Min.X : axis == 1 ? box.Min.Y : box.Min.Z;

        private static double High(GeoAabb3 box, int axis) => axis == 0 ? box.Max.X : axis == 1 ? box.Max.Y : box.Max.Z;

        /// <summary>
        /// Whether two boxes overlap by more than a reach along all three axes.
        /// </summary>
        private static bool Overlap(GeoAabb3 a, GeoAabb3 b, double reach)
        {
            return Math.Min(a.Max.X, b.Max.X) - Math.Max(a.Min.X, b.Min.X) > reach
                && Math.Min(a.Max.Y, b.Max.Y) - Math.Max(a.Min.Y, b.Min.Y) > reach
                && Math.Min(a.Max.Z, b.Max.Z) - Math.Max(a.Min.Z, b.Min.Z) > reach;
        }

        /// <summary>
        /// The places of the weights, the heaviest first, and of two the same the earlier.
        /// </summary>
        private static int[] HeaviestFirst(long[] weights)
        {
            var order = new int[weights.Length];

            for (int k = 0; k < order.Length; k++)
            {
                order[k] = k;
            }

            Array.Sort(order, (x, y) => weights[x] != weights[y] ? weights[y].CompareTo(weights[x]) : x.CompareTo(y));

            return order;
        }

        /// <summary>
        /// Whether a volume is too thin to be an overlap: no more than the point tolerance times half the area, thinner on
        /// average than half the tolerance.
        /// </summary>
        private static bool Thin(double volume, double area, Tolerance tolerance) => volume <= tolerance.EqualPoint * area * 0.5;

        /// <summary>
        /// The least surface a body of a volume can have, a ball's: the cube root of 36 pi times the volume squared. The
        /// common part of 20 000 has at least 3 563 of it.
        /// </summary>
        internal static double LeastArea(double volume) => Math.Pow(36.0 * Math.PI * volume * volume, 1.0 / 3.0);

        /// <summary>
        /// Test only; null outside the tests. Given the indices of the loser and the keeper of a pair, true has the pair's
        /// common part taken as not made, without trying the intersection, so that it is worked out by slicing.
        /// </summary>
        internal static Func<int, int, bool> BreakIntersection;

        /// <summary>
        /// Test only; null outside the tests. Given the indices of the loser and the keeper of a piece with earlier pieces
        /// to cut out of it, true has the cut taken as skipped, without trying it, so that the piece is worked out by
        /// slicing.
        /// </summary>
        internal static Func<int, int, bool> BreakTakeOff;

        #endregion

        #region Phase 1: what each pair shares

        /// <summary>
        /// Works out what a pair shares: as a body where the common part is valid and holds no more than the smaller can,
        /// otherwise by slicing, which the second phase does.
        /// </summary>
        /// <remarks>
        /// Nothing more is tried after the intersection. A girder of 322 faces and a wall of 354 of a Tekla model, met
        /// within a thousandth with a contact of a hundredth, shared a body of 4 256 faces that was not valid, in 114
        /// seconds; a cut of the one by the other then took 2 215 seconds and left 80 805 faces, not valid either, where
        /// slicing takes under a second.
        /// </remarks>
        private static void WorkOut(Pair pair, SolidBooleanOptions options)
        {
            Tolerance tolerance = options.Tolerance;
            Part loser = pair.Loser, keeper = pair.Keeper;
            double most = Math.Min(loser.Gross, keeper.Gross);
            double slack = tolerance.EqualPoint * (loser.Area + keeper.Area);

            if (BreakIntersection != null && BreakIntersection(loser.Index, keeper.Index))
            {
                pair.Kind = Kind.Sliced;
                return;
            }

            try
            {
                bool made = Boolean3.TryIntersect(loser.Item.Solid, keeper.Item.Solid, out GeoSolid3 piece, options, out BooleanOutcome outcome);

                if (!made && outcome == BooleanOutcome.Empty)
                {
                    pair.Kind = Kind.Touching;
                    return;
                }

                if (made && piece.Validate(tolerance).IsValid)
                {
                    double volume = piece.GetVolume(tolerance);

                    if (Thin(volume, piece.GetSurfaceArea(tolerance), tolerance))
                    {
                        pair.Kind = Kind.Touching;
                        return;
                    }

                    if (volume <= most + slack)
                    {
                        pair.Kind = Kind.Piece;
                        pair.Piece = piece;
                        pair.Volume = volume;
                        return;
                    }
                }
            }
            catch (Exception exception) when (!(exception is OutOfMemoryException))
            {
                GeometryHelperLog.Warn("VolumeTakeoff: what items " + loser.Index + " and " + keeper.Index + " share could not be worked out as a body; it is worked out by slicing.", exception);
            }

            pair.Kind = Kind.Sliced;
        }

        #endregion

        #region Phase 2: each overlap taken off once

        /// <summary>
        /// Plans the steps that take off each overlap once: for each overlap of a part, the overlaps of that part before it
        /// whose boxes meet it. A piece meeting none is taken off whole at once; an overlap to slice is a step even alone.
        /// </summary>
        /// <remarks>
        /// An overlap to slice has no body, so the keeper itself stands for it in the later pieces' cuts: within the
        /// loser, a piece less the keeper is the piece less what the two share.
        /// </remarks>
        private static List<Step> PlanSteps(List<Pair>[] losing, double reach)
        {
            var steps = new List<Step>();

            for (int i = 0; i < losing.Length; i++)
            {
                List<Pair> list = losing[i];

                if (list == null)
                {
                    continue;
                }

                for (int m = 0; m < list.Count; m++)
                {
                    Pair pair = list[m];
                    bool piece = pair.Kind == Kind.Piece;
                    var tools = new List<Pair>();
                    long weight = piece ? pair.Piece.Faces.Count : (long)pair.Loser.Item.Solid.Faces.Count + pair.Keeper.Item.Solid.Faces.Count;

                    for (int j = 0; j < m; j++)
                    {
                        Pair earlier = list[j];
                        GeoSolid3 tool = Tool(earlier);

                        // What the loser shares with this keeper: the piece, or for an overlap to slice the box the two share.
                        if (piece ? Overlap(pair.Piece.GetAabb(), tool.GetAabb(), reach) : Overlap(pair.Loser.Box, pair.Keeper.Box, tool.GetAabb(), reach))
                        {
                            tools.Add(earlier);
                            weight += tool.Faces.Count;
                        }
                    }

                    if (piece && tools.Count == 0)
                    {
                        pair.Deducted = pair.Volume;
                        continue;
                    }

                    steps.Add(new Step { Pair = pair, Tools = tools, Weight = weight });
                }
            }

            return steps;
        }

        /// <summary>
        /// Whether three boxes overlap together by more than a reach along all three axes.
        /// </summary>
        private static bool Overlap(GeoAabb3 a, GeoAabb3 b, GeoAabb3 c, double reach)
        {
            return Math.Min(Math.Min(a.Max.X, b.Max.X), c.Max.X) - Math.Max(Math.Max(a.Min.X, b.Min.X), c.Min.X) > reach
                && Math.Min(Math.Min(a.Max.Y, b.Max.Y), c.Max.Y) - Math.Max(Math.Max(a.Min.Y, b.Min.Y), c.Min.Y) > reach
                && Math.Min(Math.Min(a.Max.Z, b.Max.Z), c.Max.Z) - Math.Max(Math.Max(a.Min.Z, b.Min.Z), c.Min.Z) > reach;
        }

        /// <summary>
        /// The body an earlier overlap is taken out of a later piece by: its piece, or for one to slice the keeper.
        /// </summary>
        private static GeoSolid3 Tool(Pair earlier) => earlier.Kind == Kind.Piece ? earlier.Piece : earlier.Keeper.Item.Solid;

        /// <summary>
        /// Works out what a keeper takes off a part and no keeper before it took: a piece less the earlier pieces, cut,
        /// or where there is no piece, an earlier overlap is sliced, or the cut cannot finish, sliced.
        /// </summary>
        private static void TakeOffOnce(Step step, SolidBooleanOptions options)
        {
            Tolerance tolerance = options.Tolerance;
            Pair pair = step.Pair;

            // A piece with a keeper to cut out whose overlap is sliced is sliced too: the keeper's whole body cut out of the
            // piece is a boolean the slicing need not trust. A girder ranked over a column, its overlap with a wall sliced,
            // cut out of the column's piece of the wall left 324 108 388.10, where slicing gives the exact 324 108 381.91.
            if (pair.Kind == Kind.Piece && (step.Tools.Exists(tool => tool.Kind == Kind.Sliced) || (BreakTakeOff != null && BreakTakeOff(pair.Loser.Index, pair.Keeper.Index))))
            {
                pair.BySlicing = true;
            }
            else if (pair.Kind == Kind.Piece)
            {
                var tools = new GeoSolid3[step.Tools.Count];

                for (int t = 0; t < tools.Length; t++)
                {
                    tools[t] = Tool(step.Tools[t]);
                }

                try
                {
                    if (!Boolean3.TrySubtractAll(pair.Piece, tools, out GeoSolid3 rest, options, out SubtractReport report))
                    {
                        pair.Deducted = 0.0;
                        return;
                    }

                    if (report.Skipped.Count == 0)
                    {
                        double volume = rest.GetVolume(tolerance);
                        pair.Deducted = Thin(volume, rest.GetSurfaceArea(tolerance), tolerance) ? 0.0 : volume;
                        return;
                    }
                }
                catch (Exception exception) when (!(exception is OutOfMemoryException))
                {
                    GeometryHelperLog.Warn("VolumeTakeoff: the overlaps before the one of items " + pair.Loser.Index + " and " + pair.Keeper.Index + " could not be cut out of it; it is worked out by slicing.", exception);
                }

                pair.BySlicing = true;
            }

            // The loser's material within this keeper and outside every keeper before it that meets it: the keepers
            // themselves, since within the loser what a keeper shares with it is the keeper.
            var before = new GeoSolid3[step.Tools.Count];

            for (int t = 0; t < before.Length; t++)
            {
                before[t] = step.Tools[t].Keeper.Item.Solid;
            }

            bool sliced = false;
            double shared = 0.0;

            try
            {
                sliced = Slice3.TryVolume(pair.Loser.Item.Solid, new[] { pair.Keeper.Item.Solid }, before, out shared);
            }
            catch (Exception exception) when (!(exception is OutOfMemoryException))
            {
                GeometryHelperLog.Warn("VolumeTakeoff: what items " + pair.Loser.Index + " and " + pair.Keeper.Index + " share could not be sliced; it is not taken off.", exception);
            }

            if (!sliced)
            {
                pair.Deducted = 0.0;
                pair.Issue = "overlap with #" + pair.Keeper.Index + " could not be worked out, by booleans or by slicing: not deducted, so the net volume is an upper bound";
                return;
            }

            // No body to measure the area of, so the least area a body of this volume can have stands in for it: a ball's.
            // The parts' own areas would be far too loose: two slabs 6 000 by 6 000 by 200 sharing a corner 10 by 10 by
            // 200, 20 000, would pass for touching against the 38 400 their area allows.
            pair.Deducted = Thin(shared, LeastArea(shared), tolerance) ? 0.0 : shared;
        }

        #endregion

        #region The results

        /// <summary>
        /// Puts the take-off of a part together: its deductions in the order of their keepers, and its issues, that of its
        /// own volume first, then those of the overlaps that could not be worked out, in the order of their keepers, then
        /// the clamp.
        /// </summary>
        private static VolumeTakeoffResult Result(Part part, List<Pair> losing, Tolerance tolerance)
        {
            var issues = new List<string>();
            var deductions = new List<VolumeDeduction>();
            double deducted = 0.0;

            if (part.GrossIssue != null)
            {
                issues.Add(part.GrossIssue);
            }

            if (losing != null)
            {
                foreach (Pair pair in losing)
                {
                    if (pair.Issue != null)
                    {
                        issues.Add(pair.Issue);
                    }

                    if (pair.Deducted > 0.0)
                    {
                        deductions.Add(new VolumeDeduction(pair.Keeper.Index, pair.Deducted));
                        deducted += pair.Deducted;
                    }
                }
            }

            double net = part.Gross - deducted;

            if (net < 0.0)
            {
                // Within the point tolerance times the part's area, what is taken off is the whole part as the booleans
                // read it, as for a part wholly inside another; beyond, it is more than the part holds.
                if (-net > tolerance.EqualPoint * part.Area)
                {
                    issues.Add(string.Format(
                        CultureInfo.InvariantCulture,
                        "the deductions, {0:0.###}, come to more than the gross volume, {1:0.###}: the net volume is clamped to 0",
                        deducted,
                        part.Gross));
                }

                net = 0.0;
            }
            else if (net > part.Gross)
            {
                net = part.Gross;
            }

            return new VolumeTakeoffResult(part.Item, part.Gross, deducted, net, deductions.ToArray(), issues.ToArray());
        }

        #endregion

        #region Working state

        /// <summary>
        /// How a pair came out.
        /// </summary>
        private enum Kind
        {
            /// <summary>Not worked out yet.</summary>
            None,

            /// <summary>The two only touch, or share too thin a part to count.</summary>
            Touching,

            /// <summary>What they share is a body.</summary>
            Piece,

            /// <summary>What they share could not be made as a body, and is worked out by slicing.</summary>
            Sliced,
        }

        /// <summary>
        /// A part as the take-off reads it.
        /// </summary>
        private sealed class Part
        {
            public int Index;
            public VolumeItem Item;
            public double Gross;
            public double Area;
            public GeoAabb3 Box;
            public int Rank;
            public string GrossIssue;
        }

        /// <summary>
        /// A pair of parts whose boxes overlap: the slot each phase writes what it found into, one task to a slot.
        /// </summary>
        private sealed class Pair
        {
            public Part Loser;
            public Part Keeper;
            public Kind Kind;
            public GeoSolid3 Piece;
            public double Volume;
            public string Issue;
            public double Deducted;
            public bool BySlicing;
        }

        /// <summary>
        /// An overlap to take the earlier overlaps of its part out of: a piece to cut, or an overlap to slice.
        /// </summary>
        private sealed class Step
        {
            public Pair Pair;
            public List<Pair> Tools;
            public long Weight;
        }

        #endregion
    }
}
