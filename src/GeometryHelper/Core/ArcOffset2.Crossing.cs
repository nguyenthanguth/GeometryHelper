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
        /// <returns>The loops of the offset, counter-clockwise where they bound the region from outside.</returns>
        internal static List<List<GeoEdge2>> Offset(List<List<GeoEdge2>> loops, List<List<GeoEdge2>> region, double distance, OffsetOptions options, Tolerance tolerance)
        {
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

            return Prune(pieces, mitres, all, true, distance, tolerance, Spans.Of(pieceSizes), Spans.Of(edgeSizes), new Region(region, distance > 0.0), 1);
        }

        /// <summary>
        /// Cuts a loop that crosses itself where it crosses itself, and puts it back together as the loops that bound the
        /// region it encloses, read even-odd.
        /// </summary>
        /// <param name="edges">The edges of the loop, each starting where the one before ended, the last coming back to the first.</param>
        /// <param name="tolerance">The tolerance, which says how near a crossing may lie to an end of an edge and still be at it.</param>
        /// <param name="region">The simple loops the crossings cut it into, which say what lies in the region: inside an odd number of them.</param>
        /// <param name="bounds">The loops that bound the region, each running with it on its left.</param>
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
        internal static bool TrySplitAtCrossings(List<GeoEdge2> edges, Tolerance tolerance, out List<List<GeoEdge2>> region, out List<List<GeoEdge2>> bounds)
        {
            region = null;
            bounds = null;

            List<Crossing> crossings = FindCrossings(edges, tolerance);

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

            for (int a = 0; a < count; a++)
            {
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
                        continue;
                    }

                    CrossingsOf(Math.Min(one, other), Math.Max(one, other), edges, carriers, boxes, tolerance, found);
                }
            }

            return found;
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
        /// <returns>The places, in the order their first crossings were found in.</returns>
        private static List<GeoPoint2> ClusterNodes(List<Crossing> crossings, Tolerance tolerance)
        {
            var nodes = new List<GeoPoint2>();
            var order = new int[crossings.Count];

            for (int i = 0; i < order.Length; i++)
            {
                order[i] = i;
            }

            // In order along X, so that the places a crossing can be at are found among the latest few.
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

            var nodeOrder = new List<int>();
            int from = 0;

            foreach (int index in order)
            {
                Crossing crossing = crossings[index];
                int node = -1;

                while (from < nodeOrder.Count && nodes[nodeOrder[from]].X < crossing.At.X - tolerance.EqualPoint)
                {
                    from++;
                }

                for (int k = from; k < nodeOrder.Count && node < 0; k++)
                {
                    if (nodes[nodeOrder[k]].IsEqualTo(crossing.At, tolerance))
                    {
                        node = nodeOrder[k];
                    }
                }

                if (node < 0)
                {
                    node = nodes.Count;
                    nodes.Add(crossing.At);
                    nodeOrder.Add(node);
                }

                crossing.Node = node;
                crossings[index] = crossing;
            }

            return nodes;
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
        /// is told chain by chain and not loop by loop.
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

                for (int j = 0; j < count; j++)
                {
                    if (j != chain.Loop && indexes[j].Encloses(probe))
                    {
                        left = !left;
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
    }
}
