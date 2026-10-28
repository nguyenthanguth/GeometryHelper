using System;
using System.Collections.Generic;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;

namespace GeometryHelper.Core
{
    /// <summary>
    /// Offsetting a loop that crosses itself, the way the straight offsets read one: even-odd.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The offset of one loop is built piece by piece and joined at the loop's vertices, so it can tell nothing of
    /// two edges that cross each other away from a vertex, and a loop that crosses itself cannot be offset to one
    /// side: half of it runs the other way round. The straight offsets read such a loop even-odd, a point being inside
    /// when it lies inside an odd number of the simple loops the crossings cut it into, and offset that region. So is
    /// this done, with the arcs kept: the loop is cut at its crossings into the pieces between them, each piece is turned
    /// to run with the region on its left (counter-clockwise round the outside of it, clockwise round a hole), the pieces
    /// are joined again at the crossings as the corners of the region are, all of them are moved to the right of the way
    /// they run together, pruned against the whole shape and against the region, and put back into the loops they make.
    /// </para>
    /// <para>
    /// A loop that does not cross itself is not touched by any of this: it is offset as it always was.
    /// </para>
    /// </remarks>
    internal static partial class ArcOffset2
    {
        /// <summary>
        /// Where the loops of a shape that is made of several begin and end in the one list of its edges, or of the pieces
        /// of its offset: for each item, the first and the last of the loop it is in.
        /// </summary>
        private sealed class Spans
        {
            private Spans(int[] first, int[] last)
            {
                First = first;
                Last = last;
            }

            internal int[] First { get; }

            internal int[] Last { get; }

            /// <summary>
            /// Gets the spans of a shape that is one loop, or one chain, of a number of items.
            /// </summary>
            internal static Spans Single(int count)
            {
                var first = new int[count];
                var last = new int[count];

                for (int i = 0; i < count; i++)
                {
                    last[i] = count - 1;
                }

                return new Spans(first, last);
            }

            /// <summary>
            /// Gets the spans of loops of the sizes given, one after the other.
            /// </summary>
            internal static Spans Of(List<int> sizes)
            {
                int total = 0;

                foreach (int size in sizes)
                {
                    total += size;
                }

                var first = new int[total];
                var last = new int[total];
                int at = 0;

                foreach (int size in sizes)
                {
                    for (int i = 0; i < size; i++)
                    {
                        first[at + i] = at;
                        last[at + i] = at + size - 1;
                    }

                    at += size;
                }

                return new Spans(first, last);
            }
        }

        /// <summary>
        /// The region that loops running with it on their left bound, read even-odd: what lies inside an odd number of them.
        /// </summary>
        /// <remarks>
        /// A piece of the offset of loops like these stands the distance from all of them and lies on one side of the region
        /// only: outside it for a region grown, inside it for one shrunk. The distance cannot say which, and a loop that
        /// bounds a hole narrower than the distance moves its pieces right across it and out of the hole, into the region,
        /// where they stand the distance from the hole's far side and from nothing else.
        /// </remarks>
        private sealed class Region
        {
            private readonly LoopIndex[] _loops;

            internal Region(List<List<GeoEdge2>> loops, bool wantInside)
            {
                WantInside = wantInside;
                _loops = new LoopIndex[loops.Count];

                for (int i = 0; i < loops.Count; i++)
                {
                    _loops[i] = new LoopIndex(loops[i]);
                }
            }

            /// <summary>
            /// Gets a value indicating whether the offset goes into the region (the region is shrunk) or out of it.
            /// </summary>
            internal bool WantInside { get; }

            /// <summary>
            /// Determines whether a point lies in the region: inside an odd number of the loops.
            /// </summary>
            internal bool Holds(GeoPoint2 point)
            {
                bool inside = false;

                foreach (LoopIndex loop in _loops)
                {
                    if (loop.Encloses(point))
                    {
                        inside = !inside;
                    }
                }

                return inside;
            }
        }

        /// <summary>
        /// Offsets loops that together bound a region, each to the left of the way it runs.
        /// </summary>
        /// <param name="loops">The loops that bound the region, each closed, each running with the region on its left.</param>
        /// <param name="region">The simple loops the region is the inside of, read even-odd, each running with it on its left: counter-clockwise round the outside of it, clockwise round a hole.</param>
        /// <param name="distance">How far to move them, to the left of the way they run when positive.</param>
        /// <param name="options">How to fill a corner that opens up.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <param name="open">How many runs of the offset were left open, which no loop closes.</param>
        /// <returns>The loops of the offset, counter-clockwise where they bound the region from outside.</returns>
        internal static List<List<GeoEdge2>> Offset(List<List<GeoEdge2>> loops, List<List<GeoEdge2>> region, double distance, OffsetOptions options, Tolerance tolerance, out int open)
        {
            open = 0;

            var all = new List<GeoEdge2>();
            var edgeSizes = new List<int>(loops.Count);
            var pieces = new List<Piece>();
            var pieceSizes = new List<int>(loops.Count);
            var mitres = new List<Mitre>();

            foreach (List<GeoEdge2> loop in loops)
            {
                int baseIndex = all.Count;

                all.AddRange(loop);
                edgeSizes.Add(loop.Count);

                var moved = new List<GeoEdge2>(loop.Count * 2);
                var kept = new List<int>(loop.Count);
                var spent = new List<bool>(loop.Count);

                for (int i = 0; i < loop.Count; i++)
                {
                    if (TryMove(loop[i], distance, tolerance, out GeoEdge2 piece))
                    {
                        moved.Add(piece);
                        kept.Add(i);
                        spent.Add(false);
                    }
                    else if (loop[i].IsArc)
                    {
                        moved.Add(Spend(loop[i], distance, tolerance, out bool through));
                        kept.Add(i);
                        spent.Add(through);
                    }
                }

                if (moved.Count == 0)
                {
                    continue;
                }

                var local = new List<Mitre>();
                List<Piece> joined = Join(loop, kept, moved, spent, true, distance, options, local, tolerance, true);

                // The loop's own numbers for its edges and corners are made the shape's.
                foreach (Piece piece in joined)
                {
                    pieces.Add(new Piece(piece.Edge, piece.Bridge, piece.Source < 0 ? -1 : piece.Source + baseIndex, piece.Corner < 0 ? -1 : piece.Corner + baseIndex, piece.Across, piece.Spent));
                }

                foreach (Mitre mitre in local)
                {
                    mitres.Add(new Mitre(mitre.Corner + baseIndex, mitre.Before, mitre.After, mitre.First, mitre.Second, mitre.Square, mitre.StraightFirst, mitre.StraightSecond));
                }

                pieceSizes.Add(joined.Count);
            }

            if (pieces.Count == 0)
            {
                return new List<List<GeoEdge2>>();
            }

            return Prune(pieces, mitres, all, true, distance, tolerance, Spans.Of(pieceSizes), Spans.Of(edgeSizes), new Region(region, distance > 0.0), 1, out open);
        }

        /// <summary>
        /// Cuts a loop that crosses itself where it crosses itself, and puts it back together as the loops that bound the
        /// region it encloses, read even-odd.
        /// </summary>
        /// <param name="edges">The edges of the loop, each starting where the one before ended, the last coming back to the first.</param>
        /// <param name="tolerance">The tolerance, which says how near a crossing may lie to an end of an edge and still be at it.</param>
        /// <param name="region">The simple loops the crossings cut it into, which say what lies in the region: inside an odd number of them.</param>
        /// <param name="bounds">The loops that bound the region, each running with it on its left.</param>
        /// <param name="found">How many places two edges were found to cross each other at, whether or not the loop could be cut there.</param>
        /// <returns>true when two edges cross each other, other than at a vertex, and the loop was cut.</returns>
        /// <remarks>
        /// A crossing is a place two edges cross each other, away from the ends of both: edges that meet at a vertex, or
        /// touch with an end, do not cross. A loop that only touches itself is not cut, and is offset as it always was.
        /// Two arcs that leave a vertex one over the other and cross again beside it, as the neighbours of a gear's inner
        /// vertex do, make a lens that the loop runs round the other way: that lens is a loop of its own.
        /// <para>
        /// The loops the crossings cut it into can cross each other at a crossing, where the region lies in two opposite
        /// quarters and the space that is not the region in the other two, so which side of a piece the region is on is read
        /// for each piece, off the loops it is not part of. At a crossing each piece arriving is then joined to the piece
        /// leaving next round from it, counter-clockwise: the pair that bound a quarter of the space that is not the region,
        /// as the corner of a polygon is. The corner of the offset there is the corner of the region, a quarter of space
        /// narrower than the offset being filled up to where its sides meet.
        /// </para>
        /// </remarks>
        internal static bool TrySplitAtCrossings(List<GeoEdge2> edges, Tolerance tolerance, out List<List<GeoEdge2>> region, out List<List<GeoEdge2>> bounds, out int found)
        {
            region = null;
            bounds = null;

            List<Crossing> crossings = FindCrossings(edges, tolerance);
            found = crossings.Count;

            if (crossings.Count == 0)
            {
                return false;
            }

            List<GeoPoint2> nodes = ClusterNodes(crossings, tolerance);
            List<Sub> walk = CutAtNodes(edges, crossings, nodes, tolerance);

            if (walk == null)
            {
                return false;
            }

            List<List<Sub>> simple = UntieAtNodes(walk);

            if (simple.Count < 2)
            {
                return false;
            }

            List<List<GeoEdge2>> loops = EdgesOf(simple);
            List<Chain> chains = ChainsOf(simple);

            if (chains == null)
            {
                return false;
            }

            TurnWithTheRegionOnTheLeft(chains, loops);
            List<List<GeoEdge2>> joined = JoinAcrossTheSpaceBetween(chains);

            if (joined == null)
            {
                return false;
            }

            region = loops;
            bounds = joined;
            return true;
        }

        private static List<List<GeoEdge2>> EdgesOf(List<List<Sub>> loops)
        {
            var edges = new List<List<GeoEdge2>>(loops.Count);

            foreach (List<Sub> loop in loops)
            {
                var one = new List<GeoEdge2>(loop.Count);

                foreach (Sub sub in loop)
                {
                    one.Add(sub.Edge);
                }

                edges.Add(one);
            }

            return edges;
        }

        /// <summary>
        /// Two edges of a loop crossing each other: which, where, and the node the place is.
        /// </summary>
        private struct Crossing
        {
            internal Crossing(int first, int second, GeoPoint2 at, int firstAt, int secondAt)
            {
                First = first;
                Second = second;
                At = at;
                FirstAt = firstAt;
                SecondAt = secondAt;
                Node = -1;
            }

            internal int First { get; }

            internal int Second { get; }

            internal GeoPoint2 At { get; }

            /// <summary>
            /// Gets where the place lies on the first edge: 0 along it, 1 at its start, 2 at its end.
            /// </summary>
            internal int FirstAt { get; }

            /// <summary>
            /// Gets where the place lies on the second edge: 0 along it, 1 at its start, 2 at its end.
            /// </summary>
            internal int SecondAt { get; }

            internal int Node { get; set; }
        }

        /// <summary>
        /// A piece of an edge between two crossings, or between a crossing and an end of the edge, and the nodes it runs
        /// between: -1 where it begins or ends at an end of the edge.
        /// </summary>
        private struct Sub
        {
            internal Sub(GeoEdge2 edge, int from, int to)
            {
                Edge = edge;
                From = from;
                To = to;
            }

            internal GeoEdge2 Edge { get; }

            internal int From { get; }

            internal int To { get; }
        }

        private static List<Crossing> FindCrossings(List<GeoEdge2> edges, Tolerance tolerance)
        {
            var found = new List<Crossing>();
            int count = edges.Count;
            var boxes = new Box[count];
            var carriers = new Carrier[count];

            for (int i = 0; i < count; i++)
            {
                boxes[i] = IsPoint(edges[i]) ? Box.Empty : Box.Of(edges[i]).Grown(tolerance.EqualPoint);
                carriers[i] = new Carrier(edges[i]);
            }

            // The boxes in order along the axis a box overlaps fewest others on, as a sweep goes: each box is tried against
            // the ones that start before it ends, and a pair is tried once. The order the pairs come in does not matter, the
            // crossings being numbered by where they are.
            double widthX = 0.0;
            double widthY = 0.0;
            Box all = Box.Empty;

            foreach (Box box in boxes)
            {
                if (box.MaxX < box.MinX || box.MaxY < box.MinY)
                {
                    continue;
                }

                widthX += box.MaxX - box.MinX;
                widthY += box.MaxY - box.MinY;
                all = all.With(box);
            }

            bool alongX = widthX * (all.MaxY - all.MinY) <= widthY * (all.MaxX - all.MinX);
            var order = new int[count];

            for (int i = 0; i < count; i++)
            {
                order[i] = i;
            }

            Array.Sort(order, (one, other) =>
            {
                int byStart = (alongX ? boxes[one].MinX : boxes[one].MinY).CompareTo(alongX ? boxes[other].MinX : boxes[other].MinY);

                return byStart != 0 ? byStart : one.CompareTo(other);
            });

            // Where many of the boxes a box reaches along the sweep lie off it across the sweep, the rest of the boxes are
            // tried row by row across it, in the same order: the 80 000 teeth of a comb lying across the sweep all reach one
            // another along it, and the plus of two such combs was tried pair by pair, 3.2 billion pairs for the 480 224 whose
            // boxes overlap. A star, whose edges all overlap, is swept as it is, and so are boxes that do not span a finite
            // width and height.
            long missed = 0;
            long enough = Enough(all, count);

            for (int a = 0; a < count; a++)
            {
                if (missed > enough)
                {
                    TryInRows(a, order, boxes, alongX, edges, carriers, tolerance, found);
                    break;
                }

                int one = order[a];
                double reach = alongX ? boxes[one].MaxX : boxes[one].MaxY;

                for (int b = a + 1; b < count; b++)
                {
                    int other = order[b];

                    if ((alongX ? boxes[other].MinX : boxes[other].MinY) > reach)
                    {
                        break;
                    }

                    if (!boxes[one].Overlaps(boxes[other]))
                    {
                        missed++;
                        continue;
                    }

                    CrossingsOf(Math.Min(one, other), Math.Max(one, other), edges, carriers, boxes, tolerance, found);
                }
            }

            return found;
        }

        /// <summary>
        /// Gets how many pairs of boxes the sweep may try and find apart before it reads the rest in rows, or the largest long
        /// where the boxes do not span a finite width and height, as those with corners at 1E308 and -1E308 do: the rows are
        /// counted off the span, and would be of no number.
        /// </summary>
        /// <remarks>Kept out of the sweep, whose loop .NET 8's profile-guided compiler makes slower with it inside.</remarks>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static long Enough(Box all, int count)
            => Guard.IsFinite(all.MaxX - all.MinX) && Guard.IsFinite(all.MaxY - all.MinY) ? 8L * count + 1024L : long.MaxValue;

        /// <summary>
        /// Tries a box against those after it in the order of the sweep that start before it ends, and notes where the edges
        /// of those it overlaps cross.
        /// </summary>
        private static void TryAlong(int a, int[] order, Box[] boxes, bool alongX, List<GeoEdge2> edges, Carrier[] carriers, Tolerance tolerance, List<Crossing> found)
        {
            int one = order[a];
            double reach = alongX ? boxes[one].MaxX : boxes[one].MaxY;

            for (int b = a + 1; b < order.Length; b++)
            {
                int other = order[b];

                if ((alongX ? boxes[other].MinX : boxes[other].MinY) > reach)
                {
                    break;
                }

                if (!boxes[one].Overlaps(boxes[other]))
                {
                    continue;
                }

                CrossingsOf(Math.Min(one, other), Math.Max(one, other), edges, carriers, boxes, tolerance, found);
            }
        }

        /// <summary>
        /// Tries each box from a place in the order of the sweep on against those after it, as the sweep does and in the same
        /// order, reading only those in the rows across the sweep it lies in and those that lie across many.
        /// </summary>
        private static void TryInRows(int from, int[] order, Box[] boxes, bool alongX, List<GeoEdge2> edges, Carrier[] carriers, Tolerance tolerance, List<Crossing> found)
        {
            var rows = new BoxRows(boxes, order, alongX);
            var near = new List<int>();

            for (int a = from; a < order.Length; a++)
            {
                if (!rows.After(a, near))
                {
                    TryAlong(a, order, boxes, alongX, edges, carriers, tolerance, found);
                    continue;
                }

                int one = order[a];
                int last = -1;

                foreach (int b in near)
                {
                    if (b == last)
                    {
                        continue;
                    }

                    last = b;

                    int other = order[b];

                    if (!boxes[one].Overlaps(boxes[other]))
                    {
                        continue;
                    }

                    CrossingsOf(Math.Min(one, other), Math.Max(one, other), edges, carriers, boxes, tolerance, found);
                }
            }
        }

        /// <summary>
        /// The boxes of a loop's edges filed by the rows across the sweep they lie in, the rows as wide as the boxes are on
        /// average across it, so that a box is read against those near it across the sweep only. A box that lies across
        /// more than a few rows is kept apart and read against every box it reaches along the sweep.
        /// </summary>
        /// <remarks>
        /// Two boxes that overlap across the sweep share a row, or one of them is kept apart. The rows hold the boxes by
        /// their places in the order of the sweep, in order.
        /// </remarks>
        private sealed class BoxRows
        {
            private const int MostRows = 16;

            private readonly double[] _starts;
            private readonly double[] _reaches;
            private readonly int[] _firstRow;
            private readonly int[] _lastRow;
            private readonly List<int>[] _rows;
            private readonly List<int> _apart = new List<int>();
            private readonly List<int>[] _runs = new List<int>[MostRows + 1];
            private readonly int[] _lows = new int[MostRows + 1];
            private readonly int[] _highs = new int[MostRows + 1];

            /// <summary>
            /// Initializes the rows of boxes that are each empty or finite, at least one of them not empty.
            /// </summary>
            /// <param name="boxes">The boxes.</param>
            /// <param name="order">Their numbers in the order of the sweep.</param>
            /// <param name="alongX">Whether the sweep goes along X, the rows running along it and lying one over another in Y.</param>
            internal BoxRows(Box[] boxes, int[] order, bool alongX)
            {
                int count = order.Length;
                double low = double.PositiveInfinity;
                double high = double.NegativeInfinity;
                double widths = 0.0;
                int some = 0;

                _starts = new double[count];
                _reaches = new double[count];
                _firstRow = new int[count];
                _lastRow = new int[count];

                foreach (Box box in boxes)
                {
                    if (box.MaxX < box.MinX || box.MaxY < box.MinY)
                    {
                        continue;
                    }

                    low = Math.Min(low, alongX ? box.MinY : box.MinX);
                    high = Math.Max(high, alongX ? box.MaxY : box.MaxX);
                    widths += alongX ? box.MaxY - box.MinY : box.MaxX - box.MinX;
                    some++;
                }

                // No more rows than four for each box, and one where the boxes have no width across the sweep or all lie at
                // one place across it.
                double span = high - low;
                double width = widths / some;
                int rows = 1;

                if (span > 0.0)
                {
                    if (!(width > 0.0) || !(span / width < 4.0 * count))
                    {
                        width = span / (4.0 * count);
                    }

                    rows = (int)Math.Min(Math.Floor(span / width) + 1.0, 4.0 * count);
                }
                else
                {
                    width = 1.0;
                }

                _rows = new List<int>[rows];

                for (int k = 0; k < count; k++)
                {
                    Box box = boxes[order[k]];

                    _starts[k] = alongX ? box.MinX : box.MinY;
                    _reaches[k] = alongX ? box.MaxX : box.MaxY;
                    _firstRow[k] = -1;

                    if (box.MaxX < box.MinX || box.MaxY < box.MinY)
                    {
                        _apart.Add(k);
                        continue;
                    }

                    int first = RowOf(alongX ? box.MinY : box.MinX, low, width, rows);
                    int last = RowOf(alongX ? box.MaxY : box.MaxX, low, width, rows);

                    if (last - first >= MostRows)
                    {
                        _apart.Add(k);
                        continue;
                    }

                    _firstRow[k] = first;
                    _lastRow[k] = last;

                    for (int row = first; row <= last; row++)
                    {
                        (_rows[row] ?? (_rows[row] = new List<int>())).Add(k);
                    }
                }
            }

            /// <summary>
            /// Gets the places after a box's in the order of the sweep of the boxes that start before it ends along the sweep
            /// and share a row with it or are kept apart, in order, one place more than once where the box shares more than one
            /// row with it.
            /// </summary>
            /// <returns>false when the box is kept apart itself, or when the boxes it reaches along the sweep are few, or not many
            /// more than those, and it is to be read against every box it reaches.</returns>
            internal bool After(int place, List<int> into)
            {
                into.Clear();

                if (_firstRow[place] < 0)
                {
                    return false;
                }

                double reach = _reaches[place];
                int reached = End(_starts, place + 1, reach) - place - 1;

                // A box that reaches few others along the sweep is read against them all, without looking in the rows.
                if (reached <= 64)
                {
                    return false;
                }

                int runs = 0;
                int taken = 0;
                int giving = 0;

                for (int row = _firstRow[place]; row <= _lastRow[place]; row++)
                {
                    if (_rows[row] != null)
                    {
                        _runs[runs] = _rows[row];
                        taken += Run(runs++, place, reach, ref giving);
                    }
                }

                _runs[runs] = _apart;
                taken += Run(runs++, place, reach, ref giving);

                // Reading a few more boxes along the sweep costs less than putting those of the rows in order.
                if (reached <= 4 * taken)
                {
                    return false;
                }

                for (int run = 0; run < runs; run++)
                {
                    for (int k = _lows[run]; k < _highs[run]; k++)
                    {
                        into.Add(_runs[run][k]);
                    }
                }

                // The places of one row, or of the boxes kept apart, are in order already.
                if (giving > 1)
                {
                    into.Sort();
                }

                return true;
            }

            /// <summary>
            /// Gets the row a place across the sweep lies in, the first or the last for a place beyond them.
            /// </summary>
            private static int RowOf(double value, double low, double width, int rows)
            {
                double row = Math.Floor((value - low) / width);

                return row <= 0.0 ? 0 : row >= rows - 1 ? rows - 1 : (int)row;
            }

            /// <summary>
            /// Finds where in a run of places in order lie those after a place of boxes that start before a reach along the
            /// sweep, and counts the runs that hold some.
            /// </summary>
            /// <returns>How many there are.</returns>
            private int Run(int run, int place, double reach, ref int giving)
            {
                List<int> places = _runs[run];
                int low = 0;
                int high = places.Count;

                // The first of them after the place.
                while (low < high)
                {
                    int middle = low + (high - low) / 2;

                    if (places[middle] <= place)
                    {
                        low = middle + 1;
                    }
                    else
                    {
                        high = middle;
                    }
                }

                _lows[run] = low;
                _highs[run] = Beyond(places, low, reach);

                if (_highs[run] > low)
                {
                    giving++;
                }

                return _highs[run] - low;
            }

            /// <summary>
            /// Gets the first, of a run of places in order from a given one on, of a box that starts beyond a reach along the
            /// sweep.
            /// </summary>
            private int Beyond(List<int> places, int low, double reach)
            {
                int high = places.Count;

                while (low < high)
                {
                    int middle = low + (high - low) / 2;

                    if (_starts[places[middle]] > reach)
                    {
                        high = middle;
                    }
                    else
                    {
                        low = middle + 1;
                    }
                }

                return low;
            }

            /// <summary>
            /// Gets the first place in the order of the sweep, from a given one on, of a box that starts beyond a reach.
            /// </summary>
            private static int End(double[] starts, int low, double reach)
            {
                int high = starts.Length;

                while (low < high)
                {
                    int middle = low + (high - low) / 2;

                    if (starts[middle] > reach)
                    {
                        high = middle;
                    }
                    else
                    {
                        low = middle + 1;
                    }
                }

                return low;
            }
        }

        /// <summary>
        /// Notes where two edges of a loop cross each other, if they do.
        /// </summary>
        private static void CrossingsOf(int i, int j, List<GeoEdge2> edges, Carrier[] carriers, Box[] boxes, Tolerance tolerance, List<Crossing> found)
        {
            // Two edges that meet at a vertex meet again, if at all, at one place the shape of their circles gives outright,
            // and most do not: that is looked at before the edges are crossed.
            if (j == i + 1 && !MayMeetAgain(carriers[i], carriers[j], edges[j].StartPoint, boxes[i], boxes[j]))
            {
                return;
            }

            if (i == 0 && j == edges.Count - 1 && !MayMeetAgain(carriers[j], carriers[i], edges[i].StartPoint, boxes[j], boxes[i]))
            {
                return;
            }

            foreach (GeoPoint2 at in carriers[i].Meet(carriers[j], tolerance))
            {
                bool endsI = Ends(edges[i], at, tolerance);
                bool endsJ = Ends(edges[j], at, tolerance);

                // At the end of an edge the loop passes through a vertex, and crosses the other only if it comes from one side
                // of it and goes to the other.
                if (endsI || endsJ)
                {
                    if (CrossesAtAVertex(edges, i, j, at, tolerance, out int firstAt, out int secondAt))
                    {
                        found.Add(new Crossing(i, j, at, firstAt, secondAt));
                    }

                    continue;
                }

                // An edge that only grazes the other has no side to cross to.
                GeoVector2 along = Heading(edges[i], at);
                GeoVector2 other = Heading(edges[j], at);

                if (Math.Abs(along.CrossProduct(other)) <= 1E-9 * along.Length * other.Length)
                {
                    continue;
                }

                found.Add(new Crossing(i, j, at, 0, 0));
            }
        }

        /// <summary>
        /// Determines whether the loop crosses itself where two edges meet at the end of one of them: the pass of the loop
        /// through the place, along an edge or through a vertex from one edge to the next, comes from one side of the other
        /// pass and leaves to the other.
        /// </summary>
        /// <remarks>
        /// Edges that meet at the vertex they share are the one pass. Of two passes, each is two rays out of the place, and
        /// they cross when the rays of one are parted by the rays of the other round it. A vertex that lies on an edge with
        /// its neighbours on one side of it touches it and does not cross it, and a vertex with its neighbours on either side
        /// does: the loop <c>0,0; 10,0; 10,10; 5,0; 3,-6</c> crosses the edge from the origin to (10, 0) at (5, 0).
        /// </remarks>
        private static bool CrossesAtAVertex(List<GeoEdge2> edges, int i, int j, GeoPoint2 at, Tolerance tolerance, out int firstAt, out int secondAt)
        {
            firstAt = 0;
            secondAt = 0;

            int count = edges.Count;
            bool startI = at.IsEqualTo(edges[i].StartPoint, tolerance);
            bool endI = at.IsEqualTo(edges[i].EndPoint, tolerance);
            bool startJ = at.IsEqualTo(edges[j].StartPoint, tolerance);
            bool endJ = at.IsEqualTo(edges[j].EndPoint, tolerance);

            // An edge that begins and ends at the place is a point of an edge, and has no way through.
            if ((startI && endI) || (startJ && endJ))
            {
                return false;
            }

            int vertexI = startI ? i : (endI ? (i + 1) % count : -1);
            int vertexJ = startJ ? j : (endJ ? (j + 1) % count : -1);

            if (vertexI >= 0 && vertexI == vertexJ)
            {
                return false;
            }

            firstAt = startI ? 1 : (endI ? 2 : 0);
            secondAt = startJ ? 1 : (endJ ? 2 : 0);

            Ray(edges, i, vertexI, at, count, out GeoVector2 backI, out GeoVector2 forwardI, out double reachI);
            Ray(edges, j, vertexJ, at, count, out GeoVector2 backJ, out GeoVector2 forwardJ, out double reachJ);

            // Rays that part by less than the tolerance over the length of their edges may as well lie along one another: the
            // order they come in is not the loop's, and the meeting is a touch.
            double apart = Math.Max(1E-9, 2.0 * tolerance.EqualPoint / Math.Min(reachI, reachJ));

            // From the way the first pass leaves, counter-clockwise: where it comes back, and where the other pass does.
            double comes = Turn(forwardI, backI);
            double fromJ = Turn(forwardI, backJ);
            double toJ = Turn(forwardI, forwardJ);

            if (double.IsNaN(comes) || double.IsNaN(fromJ) || double.IsNaN(toJ) || Parted(comes, 0.0, apart) || Parted(fromJ, 0.0, apart) || Parted(toJ, 0.0, apart)
                || Parted(fromJ, comes, apart) || Parted(toJ, comes, apart) || Parted(fromJ, toJ, apart))
            {
                return false;
            }

            return (fromJ < comes) != (toJ < comes);
        }

        /// <summary>
        /// Determines whether two angles, taken round a turn, lie closer than a given angle.
        /// </summary>
        private static bool Parted(double one, double other, double apart)
        {
            double between = Math.Abs(one - other);

            return Math.Min(between, 2.0 * Math.PI - between) < apart;
        }

        /// <summary>
        /// Gets the two rays a pass through a place leaves it by: back the way it came and on the way it goes, and the
        /// length of the shorter edge they run along.
        /// </summary>
        private static void Ray(List<GeoEdge2> edges, int edge, int vertex, GeoPoint2 at, int count, out GeoVector2 back, out GeoVector2 forward, out double reach)
        {
            if (vertex < 0)
            {
                back = Heading(edges[edge], at) * -1.0;
                forward = Heading(edges[edge], at);
                reach = edges[edge].StartPoint.DistanceTo(edges[edge].EndPoint);
                return;
            }

            int before = (vertex + count - 1) % count;

            back = Heading(edges[before], at) * -1.0;
            forward = Heading(edges[vertex], at);
            reach = Math.Min(edges[before].StartPoint.DistanceTo(edges[before].EndPoint), edges[vertex].StartPoint.DistanceTo(edges[vertex].EndPoint));
        }

        /// <summary>
        /// Gets how far round, counter-clockwise, from one direction to another, in [0, 2 pi).
        /// </summary>
        private static double Turn(GeoVector2 from, GeoVector2 to)
        {
            double angle = Math.Atan2(from.CrossProduct(to), from.DotProduct(to));

            return angle < 0.0 ? angle + 2.0 * Math.PI : angle;
        }

        /// <summary>
        /// Determines whether two edges that meet at a vertex can meet anywhere else: two circles through a point meet again
        /// at its mirror image in the line of their centres, and a line through a point of a circle meets it again a chord
        /// along, so the place is worked out and looked for in the boxes holding the two edges.
        /// </summary>
        /// <remarks>
        /// Two straight edges meet once. Where the circles are concentric, or the place cannot be worked out, the answer is yes.
        /// </remarks>
        private static bool MayMeetAgain(Carrier first, Carrier second, GeoPoint2 shared, Box firstBox, Box secondBox)
        {
            if (!first.Edge.IsArc && !second.Edge.IsArc)
            {
                return false;
            }

            double x;
            double y;

            if (first.Edge.IsArc && second.Edge.IsArc)
            {
                GeoPoint2 one = first.Arc.Center;
                GeoPoint2 two = second.Arc.Center;
                GeoVector2 axis = one.GetVectorTo(two);
                double square = axis.DotProduct(axis);

                if (!(square > 0.0))
                {
                    return true;
                }

                GeoVector2 toShared = one.GetVectorTo(shared);
                double along = toShared.DotProduct(axis) / square;

                x = one.X + 2.0 * along * axis.X - toShared.X;
                y = one.Y + 2.0 * along * axis.Y - toShared.Y;
            }
            else
            {
                GeoArc2 arc = first.Edge.IsArc ? first.Arc : second.Arc;
                GeoEdge2 straight = first.Edge.IsArc ? second.Edge : first.Edge;
                GeoVector2 along = straight.StartPoint.GetVectorTo(straight.EndPoint);
                double square = along.DotProduct(along);

                if (!(square > 0.0))
                {
                    return true;
                }

                double reach = -2.0 * along.DotProduct(arc.Center.GetVectorTo(shared)) / square;
                GeoVector2 toShared = arc.Center.GetVectorTo(shared);

                x = arc.Center.X + toShared.X + reach * along.X;
                y = arc.Center.Y + toShared.Y + reach * along.Y;
            }

            if (double.IsNaN(x) || double.IsNaN(y) || double.IsInfinity(x) || double.IsInfinity(y))
            {
                return true;
            }

            var place = new GeoPoint2(x, y);

            return firstBox.Holds(place) && secondBox.Holds(place);
        }

        /// <summary>
        /// Gives each place where edges cross a number, the crossings within the tolerance of each other being one place.
        /// </summary>
        /// <returns>The places, each where the first of its crossings along X lies, in order along X.</returns>
        private static List<GeoPoint2> ClusterNodes(List<Crossing> crossings, Tolerance tolerance)
        {
            var nodes = new List<GeoPoint2>();
            var order = new int[crossings.Count];

            for (int i = 0; i < order.Length; i++)
            {
                order[i] = i;
            }

            // In order along X, which numbers the places.
            Array.Sort(order, (one, other) =>
            {
                int byX = crossings[one].At.X.CompareTo(crossings[other].At.X);

                if (byX != 0)
                {
                    return byX;
                }

                int byY = crossings[one].At.Y.CompareTo(crossings[other].At.Y);

                return byY != 0 ? byY : one.CompareTo(other);
            });

            // Crossings within the tolerance of each other are one place, and so are two that are each within it of a third:
            // three edges that cross in a triangle no wider than the tolerance cross at one place, which cut in two would leave
            // an edge to be cut twice within the tolerance. Each place is the one of its crossings that comes first along X, and
            // every edge through the place is cut there, so two places are not made one where that would leave a crossing of
            // either further than the tolerance from it: a row of forty crossings 0.09 apart is twenty places of two, where made
            // one place it had the edges cut up to 3.5 off where they cross.
            var rank = new int[order.Length];
            var first = new int[order.Length];

            for (int k = 0; k < order.Length; k++)
            {
                rank[order[k]] = k;
                first[k] = k;
            }

            var byY = (int[])order.Clone();

            Array.Sort(byY, (one, other) =>
            {
                int byYs = crossings[one].At.Y.CompareTo(crossings[other].At.Y);

                if (byYs != 0)
                {
                    return byYs;
                }

                int byXs = crossings[one].At.X.CompareTo(crossings[other].At.X);

                return byXs != 0 ? byXs : one.CompareTo(other);
            });

            // Swept along the axis fewer pairs lie close on, each crossing is tried against those before it within the reach, the
            // latest in the sweep first: the place of each that lies within the tolerance is joined to the crossing's own, if
            // it can be, and the sweep goes on from there with the place the join made. That is done place by place, not
            // crossing by crossing, each place keeping where its crossings lie in the sweep: the 79 003 crossings of 200
            // diameters of a circle lie in some 60 places within the reach of each, where a crossing at a time read tens of
            // thousands. A place whose first crossing lies further than the tolerance from the first of the crossing's own place
            // cannot be joined to it, and is not tried, and the places in the reach are filed in rows across the sweep by where
            // their first crossings lie, so only those near it are looked at: a loop whose 160 173 crossings lie 0.5 apart in a
            // row along X and a column along Y has a whole line of them in the reach of a sweep along either, and its places
            // took 9.4 s where they take 0.04 once warm.
            double reach = tolerance.EqualPoint;
            bool alongX = CloseAlong(crossings, order, true, reach) <= CloseAlong(crossings, byY, false, reach);
            int[] sweep = alongX ? order : byY;
            var members = new List<int>[sweep.Length];
            var boxes = new Box[sweep.Length];
            var inReach = new int[sweep.Length];
            var near = new List<int>();
            var places = new PlaceRows(crossings, alongX ? byY : order, rank, alongX, reach);
            int from = 0;

            for (int k = 0; k < sweep.Length; k++)
            {
                members[rank[sweep[k]]] = new List<int> { k };
                boxes[rank[sweep[k]]] = Box.Empty.With(crossings[sweep[k]].At);
            }

            for (int b = 0; b < sweep.Length; b++)
            {
                GeoPoint2 at = crossings[sweep[b]].At;
                double key = alongX ? at.X : at.Y;

                while ((alongX ? crossings[sweep[from]].At.X : crossings[sweep[from]].At.Y) < key - reach)
                {
                    Count(places, inReach, FirstOf(first, rank[sweep[from]]), -1);
                    from++;
                }

                int own = FirstOf(first, rank[sweep[b]]);
                int before = b;

                while (true)
                {
                    GeoPoint2 ownAt = crossings[order[own]].At;

                    near.Clear();

                    for (int row = 0; row < 3; row++)
                    {
                        List<int> filed = places.Around(own, row);

                        if (filed == null)
                        {
                            continue;
                        }

                        foreach (int place in filed)
                        {
                            if (place != own && crossings[order[place]].At.IsEqualTo(ownAt, tolerance))
                            {
                                int latest = LatestWithin(crossings, sweep, members[place], from, before, at, tolerance);

                                if (latest >= 0)
                                {
                                    near.Add(latest);
                                }
                            }
                        }
                    }

                    near.Sort((one, other) => other.CompareTo(one));

                    int joined = -1;

                    foreach (int k in near)
                    {
                        int place = FirstOf(first, rank[sweep[k]]);

                        if (TryJoin(crossings, sweep, order, first, members, boxes, Math.Min(place, own), Math.Max(place, own), tolerance))
                        {
                            int keep = Math.Min(place, own);
                            int gone = Math.Max(place, own);

                            Count(places, inReach, keep, inReach[gone]);
                            Count(places, inReach, gone, -inReach[gone]);
                            own = keep;
                            joined = k;
                            break;
                        }
                    }

                    if (joined < 0)
                    {
                        break;
                    }

                    before = joined;
                }

                Count(places, inReach, own, 1);
            }

            var nodeAt = new int[order.Length];

            for (int k = 0; k < order.Length; k++)
            {
                int root = FirstOf(first, k);

                if (root == k)
                {
                    nodeAt[k] = nodes.Count;
                    nodes.Add(crossings[order[k]].At);
                }

                Crossing crossing = crossings[order[k]];
                crossing.Node = nodeAt[root];
                crossings[order[k]] = crossing;
            }

            return nodes;
        }

        /// <summary>
        /// Gets the first of the crossings one is one place with, so far, in order along X.
        /// </summary>
        private static int FirstOf(int[] first, int at)
        {
            while (first[at] != at)
            {
                first[at] = first[first[at]];
                at = first[at];
            }

            return at;
        }

        /// <summary>
        /// Makes two places one, at the first along X of their crossings, unless a crossing of the later place would then lie
        /// further than the tolerance from it.
        /// </summary>
        /// <param name="crossings">The crossings.</param>
        /// <param name="sweep">Their numbers in the order of the sweep.</param>
        /// <param name="byX">Their numbers in order along X.</param>
        /// <param name="first">For each crossing, by its order along X, one before it of the same place, or itself.</param>
        /// <param name="members">For the first crossing of each place, where its crossings lie in the sweep, in order.</param>
        /// <param name="boxes">For the first crossing of each place, the box its crossings lie in.</param>
        /// <param name="keep">The first crossing of the place that comes first along X.</param>
        /// <param name="join">The first crossing of the other place.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <returns>true when the places were made one.</returns>
        private static bool TryJoin(List<Crossing> crossings, int[] sweep, int[] byX, int[] first, List<int>[] members, Box[] boxes, int keep, int join, Tolerance tolerance)
        {
            GeoPoint2 place = crossings[byX[keep]].At;
            Box box = boxes[join];

            // A crossing in the box lies no further from the place along either axis than one of its corners, so where the
            // corners all lie within the tolerance of the place the crossings do, and are not read one by one: a stream of
            // crossings each further left, each taking in a place of thousands, read all of them every time.
            bool within = new GeoPoint2(box.MinX, box.MinY).IsEqualTo(place, tolerance) && new GeoPoint2(box.MinX, box.MaxY).IsEqualTo(place, tolerance)
                && new GeoPoint2(box.MaxX, box.MinY).IsEqualTo(place, tolerance) && new GeoPoint2(box.MaxX, box.MaxY).IsEqualTo(place, tolerance);

            if (!within)
            {
                foreach (int k in members[join])
                {
                    if (!crossings[sweep[k]].At.IsEqualTo(place, tolerance))
                    {
                        return false;
                    }
                }
            }

            first[join] = keep;
            members[keep] = Merged(members[keep], members[join]);
            members[join] = null;
            boxes[keep] = boxes[keep].With(box);
            return true;
        }

        /// <summary>
        /// Merges two lists in order into the longer of them.
        /// </summary>
        private static List<int> Merged(List<int> one, List<int> other)
        {
            List<int> into = one.Count >= other.Count ? one : other;
            List<int> from = one.Count >= other.Count ? other : one;
            int i = into.Count - 1;
            int j = from.Count - 1;

            into.AddRange(from);

            for (int k = into.Count - 1; j >= 0; k--)
            {
                into[k] = i >= 0 && into[i] > from[j] ? into[i--] : from[j--];
            }

            return into;
        }

        /// <summary>
        /// Gets the latest place in the sweep, before a given one and not before another, of a crossing of a place that lies
        /// within the tolerance of a point, or -1 where there is none.
        /// </summary>
        private static int LatestWithin(List<Crossing> crossings, int[] sweep, List<int> members, int from, int before, GeoPoint2 at, Tolerance tolerance)
        {
            int low = 0;
            int high = members.Count;

            // The first of them at or after the end.
            while (low < high)
            {
                int middle = (low + high) / 2;

                if (members[middle] < before)
                {
                    low = middle + 1;
                }
                else
                {
                    high = middle;
                }
            }

            for (int i = low - 1; i >= 0 && members[i] >= from; i--)
            {
                if (crossings[sweep[members[i]]].At.IsEqualTo(at, tolerance))
                {
                    return members[i];
                }
            }

            return -1;
        }

        /// <summary>
        /// Counts crossings of a place in or out of the reach of the sweep, and keeps the places that have some filed.
        /// </summary>
        private static void Count(PlaceRows places, int[] inReach, int place, int change)
        {
            inReach[place] += change;

            if (inReach[place] > 0)
            {
                places.File(place);
            }
            else if (inReach[place] == 0)
            {
                places.Unfile(place);
            }
        }

        /// <summary>
        /// The places crossings are made one at, filed by the row across the sweep their first crossings lie in, the rows a
        /// little wider than the tolerance: a place whose first crossing lies within the tolerance of a point is filed in the
        /// row of the point or in one of the two beside it.
        /// </summary>
        /// <remarks>
        /// A place is numbered as its first crossing is in order along X, and keeps that number and that row while it lasts.
        /// The places filed are those with crossings in the reach of the sweep, whose first crossings lie within the tolerance
        /// of them, so the three rows round a point hold only the places near it. Only the rows some crossing lies in are kept,
        /// numbered in order across the sweep.
        /// </remarks>
        private sealed class PlaceRows
        {
            private readonly int[] _rowOf;
            private readonly int[] _slot;
            private readonly int[] _across;
            private readonly List<int>[] _filed;

            /// <summary>
            /// Initializes the rows of the crossings given, with no place filed.
            /// </summary>
            /// <param name="crossings">The crossings.</param>
            /// <param name="across">Their numbers in order along the axis across the sweep.</param>
            /// <param name="rank">For each crossing, where it lies in order along X.</param>
            /// <param name="alongX">Whether the sweep goes along X, the rows running along it and lying one over another in Y.</param>
            /// <param name="tolerance">The distance within which two crossings are at one place.</param>
            internal PlaceRows(List<Crossing> crossings, int[] across, int[] rank, bool alongX, double tolerance)
            {
                int count = across.Length;
                double origin = double.NaN;
                double last = double.NaN;
                double size = 0.0;

                foreach (int i in across)
                {
                    double value = alongX ? crossings[i].At.Y : crossings[i].At.X;

                    if (!double.IsNaN(value) && !double.IsInfinity(value))
                    {
                        origin = double.IsNaN(origin) ? value : origin;
                        last = value;
                        size = Math.Max(size, Math.Abs(value));
                    }
                }

                // Wider than the tolerance by a millionth, so two points within it fall in rows side by side however the division
                // rounds, and no narrower than a hundred-millionth of the coordinates, so the rounding stays well below that
                // millionth: a point within the tolerance of another is never two rows off. Nor narrower than 1E-150, since below
                // some 1.5E-154 the square of the tolerance comes to nothing and points 1.5E-162 apart are still within it. Where
                // the square of the tolerance is too large to hold, every two points lie within it, and where the coordinates span
                // more than a double holds they cannot be counted off in rows: in either case all lie in the one row, and with a
                // square too large to hold an infinite coordinate lies within the tolerance of a finite one and is filed too.
                double width = tolerance * (1.0 + 1E-6);

                if (!(width >= size * 1E-8))
                {
                    width = size * 1E-8;
                }

                width = Math.Max(width, 1E-150);

                bool oneRow = !(tolerance * tolerance < double.PositiveInfinity) || !(last - origin < double.PositiveInfinity);
                _rowOf = new int[count];
                _slot = new int[count];
                _across = new int[count];

                int rows = 0;

                foreach (int i in across)
                {
                    double value = alongX ? crossings[i].At.Y : crossings[i].At.X;
                    int row = -1;

                    if (oneRow ? !double.IsNaN(value) : !double.IsInfinity(value) && !double.IsNaN(value))
                    {
                        int at = oneRow ? 0 : (int)Math.Floor((value - origin) / width);

                        if (rows == 0 || _across[rows - 1] != at)
                        {
                            _across[rows++] = at;
                        }

                        row = rows - 1;
                    }

                    _rowOf[rank[i]] = row;
                    _slot[rank[i]] = -1;
                }

                _filed = new List<int>[rows];
            }

            /// <summary>
            /// Gets the places filed in the row of the first crossing of a place (1), in the row before it (0) or in the row
            /// after it (2), or null where none ever was.
            /// </summary>
            internal List<int> Around(int place, int which)
            {
                int row = _rowOf[place];

                if (row < 0)
                {
                    return null;
                }

                int beside = row + which - 1;

                return beside >= 0 && beside < _filed.Length && _across[beside] == _across[row] + which - 1 ? _filed[beside] : null;
            }

            /// <summary>
            /// Files a place, if it is not filed.
            /// </summary>
            internal void File(int place)
            {
                int row = _rowOf[place];

                if (row < 0 || _slot[place] >= 0)
                {
                    return;
                }

                List<int> filed = _filed[row] ?? (_filed[row] = new List<int>());

                _slot[place] = filed.Count;
                filed.Add(place);
            }

            /// <summary>
            /// Takes a place out of its row, if it is filed.
            /// </summary>
            internal void Unfile(int place)
            {
                if (_slot[place] < 0)
                {
                    return;
                }

                List<int> filed = _filed[_rowOf[place]];
                int moved = filed[filed.Count - 1];

                filed[_slot[place]] = moved;
                _slot[moved] = _slot[place];
                filed.RemoveAt(filed.Count - 1);
                _slot[place] = -1;
            }
        }

        /// <summary>
        /// Counts the pairs of crossings that lie within a reach of each other along one axis.
        /// </summary>
        private static long CloseAlong(List<Crossing> crossings, int[] order, bool alongX, double reach)
        {
            long pairs = 0;
            int from = 0;

            for (int b = 0; b < order.Length; b++)
            {
                double key = alongX ? crossings[order[b]].At.X : crossings[order[b]].At.Y;

                while ((alongX ? crossings[order[from]].At.X : crossings[order[from]].At.Y) < key - reach)
                {
                    from++;
                }

                pairs += b - from;
            }

            return pairs;
        }

        /// <summary>
        /// Cuts every edge at the places it crosses another, in the order the loop runs.
        /// </summary>
        /// <returns>The pieces, each running to the next, or null where an edge could not be cut where it crosses.</returns>
        private static List<Sub> CutAtNodes(List<GeoEdge2> edges, List<Crossing> crossings, List<GeoPoint2> nodes, Tolerance tolerance)
        {
            int count = edges.Count;
            var onEdge = new List<KeyValuePair<double, int>>[count];
            var startNode = new int[count];
            var endNode = new int[count];

            for (int i = 0; i < count; i++)
            {
                startNode[i] = -1;
                endNode[i] = -1;
            }

            foreach (Crossing crossing in crossings)
            {
                AddNode(onEdge, startNode, endNode, edges, crossing.First, crossing.FirstAt, crossing.At, crossing.Node, tolerance);
                AddNode(onEdge, startNode, endNode, edges, crossing.Second, crossing.SecondAt, crossing.At, crossing.Node, tolerance);
            }

            // A crossing at a vertex is the end of one edge and the start of the next.
            for (int i = 0; i < count; i++)
            {
                int next = (i + 1) % count;

                if (endNode[i] < 0)
                {
                    endNode[i] = startNode[next];
                }
                else if (startNode[next] < 0)
                {
                    startNode[next] = endNode[i];
                }
            }

            var walk = new List<Sub>(count + crossings.Count * 2);

            for (int i = 0; i < count; i++)
            {
                GeoEdge2 rest = edges[i];
                GeoPoint2 start = startNode[i] >= 0 ? nodes[startNode[i]] : edges[i].StartPoint;
                int from = startNode[i];

                if (onEdge[i] != null)
                {
                    onEdge[i].Sort((one, other) =>
                    {
                        int byParameter = one.Key.CompareTo(other.Key);

                        return byParameter != 0 ? byParameter : one.Value.CompareTo(other.Value);
                    });

                    foreach (KeyValuePair<double, int> cut in onEdge[i])
                    {
                        // Two crossings at one place on an edge cut it once.
                        if (cut.Value == from)
                        {
                            continue;
                        }

                        GeoPoint2 at = nodes[cut.Value];

                        if (!rest.TrySplitAtParameter(rest.GetParameterAtPoint(at, tolerance), out GeoEdge2 before, out GeoEdge2 after, tolerance))
                        {
                            return null;
                        }

                        // Both edges that cross end at the one place, exactly, and the arcs keep their bulges.
                        walk.Add(new Sub(new GeoEdge2(start, at, before.Bulge), from, cut.Value));
                        rest = after;
                        start = at;
                        from = cut.Value;
                    }
                }

                GeoPoint2 end = endNode[i] >= 0 ? nodes[endNode[i]] : edges[i].EndPoint;

                // A start and an end at one place leave nothing of the edge to walk, and an arc between them has no chord. Where it
                // leaves a crossing and comes back to it the walk is as it was without the piece; anywhere else the loop cannot be cut.
                if (start.IsEqualTo(end, tolerance))
                {
                    if (from >= 0 && from == endNode[i])
                    {
                        continue;
                    }

                    return null;
                }

                walk.Add(new Sub(new GeoEdge2(start, end, rest.Bulge), from, endNode[i]));
            }

            return walk;
        }

        private static void AddNode(List<KeyValuePair<double, int>>[] onEdge, int[] startNode, int[] endNode, List<GeoEdge2> edges, int edge, int where, GeoPoint2 at, int node, Tolerance tolerance)
        {
            if (where == 1)
            {
                startNode[edge] = node;
                return;
            }

            if (where == 2)
            {
                endNode[edge] = node;
                return;
            }

            if (onEdge[edge] == null)
            {
                onEdge[edge] = new List<KeyValuePair<double, int>>();
            }

            onEdge[edge].Add(new KeyValuePair<double, int>(edges[edge].GetParameterAtPoint(at, tolerance), node));
        }

        /// <summary>
        /// Cuts the walk round a loop into the simple loops between its visits to the same crossing: when it comes to a
        /// crossing it has been at before, what it has walked since is a loop of its own, and the walk goes on as if it
        /// had not been round.
        /// </summary>
        /// <returns>The loops, the last of them what is left of the walk.</returns>
        private static List<List<Sub>> UntieAtNodes(List<Sub> walk)
        {
            var loops = new List<List<Sub>>();
            var stack = new List<Sub>(walk.Count);
            var arrivedAt = new Dictionary<int, int>();

            // The walk begins at the first edge's start, which may be a crossing.
            if (walk.Count > 0 && walk[0].From >= 0)
            {
                arrivedAt[walk[0].From] = 0;
            }

            foreach (Sub sub in walk)
            {
                stack.Add(sub);

                if (sub.To < 0)
                {
                    continue;
                }

                if (arrivedAt.TryGetValue(sub.To, out int before))
                {
                    var loop = new List<Sub>(stack.Count - before);

                    for (int i = before; i < stack.Count; i++)
                    {
                        loop.Add(stack[i]);

                        // The crossings inside the loop are done with.
                        if (i > before && stack[i - 1].To >= 0 && stack[i - 1].To != sub.To)
                        {
                            arrivedAt.Remove(stack[i - 1].To);
                        }
                    }

                    stack.RemoveRange(before, stack.Count - before);
                    loops.Add(loop);
                }
                else
                {
                    arrivedAt[sub.To] = stack.Count;
                }
            }

            if (stack.Count > 0)
            {
                loops.Add(new List<Sub>(stack));
            }

            return loops;
        }

        /// <summary>
        /// A run of pieces from one crossing to the next, in the order the loop runs them or turned the other way round.
        /// </summary>
        private sealed class Chain
        {
            internal Chain(int loop, int from)
            {
                Loop = loop;
                From = from;
                To = -1;
                Edges = new List<GeoEdge2>();
                Next = -1;
            }

            /// <summary>
            /// Gets the number of the simple loop the chain is part of.
            /// </summary>
            internal int Loop { get; }

            internal int From { get; set; }

            internal int To { get; set; }

            internal List<GeoEdge2> Edges { get; set; }

            internal int Next { get; set; }

            internal bool Taken { get; set; }

            internal void TurnAround()
            {
                var turned = new List<GeoEdge2>(Edges.Count);

                for (int i = Edges.Count - 1; i >= 0; i--)
                {
                    turned.Add(Edges[i].Reverse());
                }

                Edges = turned;

                int from = From;
                From = To;
                To = from;
            }
        }

        /// <summary>
        /// Cuts each simple loop into the chains between its crossings.
        /// </summary>
        /// <returns>The chains, or null where a loop has no crossing on it.</returns>
        private static List<Chain> ChainsOf(List<List<Sub>> loops)
        {
            var chains = new List<Chain>();

            for (int l = 0; l < loops.Count; l++)
            {
                List<Sub> loop = loops[l];
                int start = -1;

                for (int i = 0; i < loop.Count && start < 0; i++)
                {
                    if (loop[i].From >= 0)
                    {
                        start = i;
                    }
                }

                if (start < 0)
                {
                    return null;
                }

                Chain current = null;

                for (int k = 0; k < loop.Count; k++)
                {
                    Sub sub = loop[(start + k) % loop.Count];

                    if (current == null)
                    {
                        current = new Chain(l, sub.From);
                    }

                    current.Edges.Add(sub.Edge);

                    if (sub.To >= 0)
                    {
                        current.To = sub.To;
                        chains.Add(current);
                        current = null;
                    }
                }

                if (current != null)
                {
                    return null;
                }
            }

            return chains;
        }

        /// <summary>
        /// Turns each chain so that the region, which is what lies inside an odd number of the loops, is on its left.
        /// </summary>
        /// <remarks>
        /// The loops run the way the loop ran, and the left of a chain is inside its own loop when that runs counter-clockwise.
        /// It is in the region when it is inside an even number of the other loops, since the region is what lies inside an odd
        /// number of all. The other loops are read at the middle of the chain's longest piece, where they do not touch it.
        /// Loops cut off a crossing cross each other at others, so one loop's chains may lie on both sides of another, and this
        /// is told chain by chain and not loop by loop. Where the loops are many only those whose boxes hold the point are
        /// read, found in a tree of the boxes: the 320 358 chains of a plus of 80 000 teeth to each arm were each read against
        /// all its 160 002 loops, and cutting it took over four minutes on .NET 10, where it takes about a second.
        /// </remarks>
        private static void TurnWithTheRegionOnTheLeft(List<Chain> chains, List<List<GeoEdge2>> loops)
        {
            int count = loops.Count;
            var indexes = new LoopIndex[count];
            var counterClockwise = new bool[count];

            for (int i = 0; i < count; i++)
            {
                indexes[i] = new LoopIndex(loops[i]);
                counterClockwise[i] = SignedArea(loops[i]) > 0.0;
            }

            // A few loops are each read, as they were: the boxes of most of them hold most points, as the 24 loops of a star of
            // 49 points do.
            LoopBoxes boxes = count > 64 ? new LoopBoxes(indexes) : null;
            var holding = new List<int>();

            foreach (Chain chain in chains)
            {
                GeoEdge2 longest = chain.Edges[0];

                foreach (GeoEdge2 edge in chain.Edges)
                {
                    if (edge.Length > longest.Length)
                    {
                        longest = edge;
                    }
                }

                GeoPoint2 probe = longest.MidPoint;
                bool left = counterClockwise[chain.Loop];

                if (boxes == null)
                {
                    for (int j = 0; j < count; j++)
                    {
                        if (j != chain.Loop && indexes[j].Encloses(probe))
                        {
                            left = !left;
                        }
                    }
                }
                else
                {
                    boxes.Holding(probe, holding);

                    foreach (int j in holding)
                    {
                        if (j != chain.Loop && indexes[j].Encloses(probe))
                        {
                            left = !left;
                        }
                    }
                }

                if (!left)
                {
                    chain.TurnAround();
                }
            }
        }

        /// <summary>
        /// Puts the chains back together at the crossings so that each chain arriving at one is followed by the chain
        /// leaving next round from it, counter-clockwise, which bounds a quarter of the space that is not the region.
        /// </summary>
        /// <param name="chains">The chains, each running with the region on its left.</param>
        /// <returns>The loops of the region's boundary, or null where the chains do not pair up.</returns>
        private static List<List<GeoEdge2>> JoinAcrossTheSpaceBetween(List<Chain> chains)
        {
            // At each crossing, the chains arriving and leaving.
            var arriving = new SortedDictionary<int, List<int>>();
            var leaving = new Dictionary<int, List<int>>();

            for (int c = 0; c < chains.Count; c++)
            {
                if (!arriving.TryGetValue(chains[c].To, out List<int> into))
                {
                    arriving[chains[c].To] = into = new List<int>();
                }

                into.Add(c);

                if (!leaving.TryGetValue(chains[c].From, out List<int> from))
                {
                    leaving[chains[c].From] = from = new List<int>();
                }

                from.Add(c);
            }

            foreach (KeyValuePair<int, List<int>> pair in arriving)
            {
                if (!leaving.TryGetValue(pair.Key, out List<int> onward) || onward.Count != pair.Value.Count)
                {
                    return null;
                }

                var used = new HashSet<int>();

                foreach (int c in pair.Value)
                {
                    GeoEdge2 last = chains[c].Edges[chains[c].Edges.Count - 1];
                    GeoVector2 back = Heading(last, last.EndPoint) * -1.0;
                    int best = -1;
                    double bestAngle = double.MaxValue;

                    foreach (int o in onward)
                    {
                        if (used.Contains(o))
                        {
                            continue;
                        }

                        GeoEdge2 first = chains[o].Edges[0];
                        GeoVector2 ahead = Heading(first, first.StartPoint);
                        double angle = Math.Atan2(back.CrossProduct(ahead), back.DotProduct(ahead));

                        // Counter-clockwise from the way back, a whole turn at most.
                        if (angle <= 0.0)
                        {
                            angle += 2.0 * Math.PI;
                        }

                        if (angle < bestAngle || (angle == bestAngle && o < best))
                        {
                            bestAngle = angle;
                            best = o;
                        }
                    }

                    if (best < 0)
                    {
                        return null;
                    }

                    used.Add(best);
                    chains[c].Next = best;
                }
            }

            var bounds = new List<List<GeoEdge2>>();

            for (int c = 0; c < chains.Count; c++)
            {
                if (chains[c].Taken)
                {
                    continue;
                }

                var loop = new List<GeoEdge2>();
                int at = c;

                while (!chains[at].Taken)
                {
                    chains[at].Taken = true;
                    loop.AddRange(chains[at].Edges);
                    at = chains[at].Next;

                    if (at < 0)
                    {
                        return null;
                    }
                }

                if (at != c)
                {
                    return null;
                }

                bounds.Add(loop);
            }

            return bounds;
        }

        /// <summary>
        /// A loop with its sides sorted into bands across it, so that a point is read against the sides in its own band
        /// only.
        /// </summary>
        /// <remarks>
        /// The loop of a gear of 3 200 teeth has 6 400 sides, and a point is read against the few that lie across its band,
        /// not all of them. A side lying across more than a few bands is kept apart and read against every point.
        /// </remarks>
        private sealed class LoopIndex
        {
            private const int MostBands = 16;

            private readonly List<GeoEdge2> _loop;
            private readonly Box[] _boxes;
            private readonly Box _box;
            private readonly double _low;
            private readonly double _scale;
            private readonly int _bands;
            private readonly List<int>[] _chords;
            private readonly List<int>[] _arcs;
            private readonly List<int> _longChords = new List<int>();
            private readonly List<int> _longArcs = new List<int>();

            internal LoopIndex(List<GeoEdge2> loop)
            {
                _loop = loop;
                _boxes = new Box[loop.Count];
                _box = Box.Empty;

                for (int k = 0; k < loop.Count; k++)
                {
                    _boxes[k] = Box.Of(loop[k]);
                    _box = _box.With(_boxes[k]);
                }

                _bands = Math.Max(1, loop.Count);
                _low = _box.MinY;
                _scale = _box.MaxY > _box.MinY ? _bands / (_box.MaxY - _box.MinY) : 0.0;
                _chords = new List<int>[_bands];
                _arcs = new List<int>[_bands];

                for (int i = 0, j = loop.Count - 1; i < loop.Count; j = i++)
                {
                    double one = loop[i].StartPoint.Y;
                    double other = loop[j].StartPoint.Y;

                    Register(_chords, _longChords, i, Math.Min(one, other), Math.Max(one, other));

                    if (loop[i].IsArc)
                    {
                        Register(_arcs, _longArcs, i, _boxes[i].MinY, _boxes[i].MaxY);
                    }
                }
            }

            /// <summary>
            /// Gets the box that holds the loop, outside which it encloses nothing.
            /// </summary>
            internal Box Bounds => _box;

            /// <summary>
            /// Determines whether the loop encloses a point, as <see cref="ArcChain2.Encloses(List{GeoEdge2}, GeoPoint2)"/>
            /// does, reading an arc only where the point lies in the box that holds it.
            /// </summary>
            internal bool Encloses(GeoPoint2 point)
            {
                if (!_box.Holds(point))
                {
                    return false;
                }

                int band = BandOf(point.Y);

                return CrossesChords(_chords[band], point)
                    ^ CrossesChords(_longChords, point)
                    ^ CutsOffArcs(_arcs[band], point)
                    ^ CutsOffArcs(_longArcs, point);
            }

            /// <summary>
            /// Determines whether an odd number of the listed sides, read as chords, are crossed by the ray from a point.
            /// </summary>
            private bool CrossesChords(List<int> sides, GeoPoint2 point)
            {
                bool odd = false;

                if (sides == null)
                {
                    return false;
                }

                foreach (int i in sides)
                {
                    GeoPoint2 p1 = _loop[i].StartPoint;
                    GeoPoint2 p2 = _loop[i == 0 ? _loop.Count - 1 : i - 1].StartPoint;

                    if ((p1.Y > point.Y) != (p2.Y > point.Y) && ArcChain2.BeforeChord(p1, p2, point))
                    {
                        odd = !odd;
                    }
                }

                return odd;
            }

            /// <summary>
            /// Determines whether an odd number of the listed arcs have the point in the piece they cut off their chords.
            /// </summary>
            private bool CutsOffArcs(List<int> sides, GeoPoint2 point)
            {
                bool odd = false;

                if (sides == null)
                {
                    return false;
                }

                foreach (int i in sides)
                {
                    if (_boxes[i].Holds(point) && _loop[i].CutsOff(point))
                    {
                        odd = !odd;
                    }
                }

                return odd;
            }

            private int BandOf(double y)
            {
                int band = (int)((y - _low) * _scale);

                return band < 0 ? 0 : band >= _bands ? _bands - 1 : band;
            }

            private void Register(List<int>[] bands, List<int> longs, int side, double low, double high)
            {
                int first = BandOf(low);
                int last = BandOf(high);

                if (last - first >= MostBands)
                {
                    longs.Add(side);
                    return;
                }

                for (int band = first; band <= last; band++)
                {
                    (bands[band] ?? (bands[band] = new List<int>())).Add(side);
                }
            }
        }

        /// <summary>
        /// The boxes of loops in a tree, each part of it holding the box round the boxes in it, so that a point is read
        /// against the loops whose boxes hold it and not against every loop.
        /// </summary>
        /// <remarks>
        /// A plus of 80 000 teeth to each arm is cut into 160 002 loops, and each of its 320 358 chains was read against all
        /// of them, where the boxes of a few hold the point it is read at. The tree halves the boxes by where their middles
        /// lie, along the axis the middles spread further on, down to a few in each part. A box that holds no point, being
        /// empty or of no number, is left out.
        /// </remarks>
        private sealed class LoopBoxes
        {
            private const int Few = 8;

            private readonly Box[] _boxes;
            private readonly int[] _order;
            private readonly List<Box> _bounds = new List<Box>();
            private readonly List<int> _from = new List<int>();
            private readonly List<int> _to = new List<int>();
            private readonly List<int> _second = new List<int>();

            internal LoopBoxes(LoopIndex[] loops)
            {
                _boxes = new Box[loops.Length];

                var kept = new List<int>(loops.Length);

                for (int i = 0; i < loops.Length; i++)
                {
                    _boxes[i] = loops[i].Bounds;

                    if (_boxes[i].MinX <= _boxes[i].MaxX && _boxes[i].MinY <= _boxes[i].MaxY)
                    {
                        kept.Add(i);
                    }
                }

                // In order of their middles along X, and along Y, both kept as the boxes are halved.
                _order = kept.ToArray();

                int[] byY = kept.ToArray();

                Array.Sort(_order, (one, other) => ByMiddle(one, other, true));
                Array.Sort(byY, (one, other) => ByMiddle(one, other, false));

                if (_order.Length > 0)
                {
                    Build(0, _order.Length, byY, new bool[loops.Length], new int[_order.Length]);
                }
            }

            /// <summary>
            /// Gets the numbers of the loops whose boxes hold a point, in no order.
            /// </summary>
            internal void Holding(GeoPoint2 point, List<int> into)
            {
                into.Clear();

                if (_bounds.Count > 0)
                {
                    Holding(0, point, into);
                }
            }

            private void Holding(int part, GeoPoint2 point, List<int> into)
            {
                if (!_bounds[part].Holds(point))
                {
                    return;
                }

                if (_second[part] < 0)
                {
                    for (int k = _from[part]; k < _to[part]; k++)
                    {
                        if (_boxes[_order[k]].Holds(point))
                        {
                            into.Add(_order[k]);
                        }
                    }

                    return;
                }

                Holding(part + 1, point, into);
                Holding(_second[part], point, into);
            }

            /// <summary>
            /// Makes a part of the tree of the boxes from one place to another in both orders, and the parts below it.
            /// </summary>
            /// <returns>The number of the part, the first below it being the next.</returns>
            private int Build(int from, int to, int[] byY, bool[] first, int[] spare)
            {
                int part = _bounds.Count;
                Box bounds = Box.Empty;

                for (int k = from; k < to; k++)
                {
                    bounds = bounds.With(_boxes[_order[k]]);
                }

                _bounds.Add(bounds);
                _from.Add(from);
                _to.Add(to);
                _second.Add(-1);

                if (to - from <= Few)
                {
                    return part;
                }

                // The first half along the axis, which keeps its order; the other order is parted into the boxes of that half
                // and the rest, each in the order it had.
                int middle = from + (to - from) / 2;
                bool alongX = !(Middle(byY[to - 1], false) - Middle(byY[from], false) > Middle(_order[to - 1], true) - Middle(_order[from], true));
                int[] halved = alongX ? _order : byY;
                int[] parted = alongX ? byY : _order;
                int kept = from;
                int put = 0;

                for (int k = from; k < middle; k++)
                {
                    first[halved[k]] = true;
                }

                for (int k = from; k < to; k++)
                {
                    if (first[parted[k]])
                    {
                        parted[kept++] = parted[k];
                    }
                    else
                    {
                        spare[put++] = parted[k];
                    }
                }

                Array.Copy(spare, 0, parted, kept, put);

                for (int k = from; k < middle; k++)
                {
                    first[halved[k]] = false;
                }

                Build(from, middle, byY, first, spare);
                _second[part] = Build(middle, to, byY, first, spare);
                return part;
            }

            private double Middle(int loop, bool alongX)
                => alongX ? _boxes[loop].MinX * 0.5 + _boxes[loop].MaxX * 0.5 : _boxes[loop].MinY * 0.5 + _boxes[loop].MaxY * 0.5;

            private int ByMiddle(int one, int other, bool alongX)
            {
                int byMiddle = Middle(one, alongX).CompareTo(Middle(other, alongX));

                return byMiddle != 0 ? byMiddle : one.CompareTo(other);
            }
        }
    }
}
