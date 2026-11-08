using System;
using System.Collections.Generic;

namespace GeometryHelper.Drawing.Projection
{
    /// <summary>
    /// Draws each line of the view once: pieces lying on one line within the point tolerance are laid along it, and each
    /// stretch of it is drawn seen where any piece there is seen, hidden only where every piece there is hidden.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The objects of a stretch are those of the pieces that give it its visibility, so the line where two plates touch
    /// edge to edge lists both, and a hidden edge of a part behind, under a seen one, adds nothing to it. Of the pieces of a
    /// stretch, a section outline outranks a sharp edge, and a sharp edge a silhouette. Stretches side by side with the
    /// same visibility, kind and objects are joined into one line.
    /// </para>
    /// <para>
    /// The pieces are taken longest first. Each joins the line of a longer piece where both its ends lie within the
    /// tolerance of that line and it overlaps or touches the stretch the line covers so far, the one it overlaps most where
    /// it could join several; otherwise it starts a line of its own. So a piece barely longer than the tolerance at a shallow
    /// corner, within the tolerance of both sides, goes to the side it lies along, not the side it only touches, and is not
    /// drawn a second time beside it. Two lines that lie on one line and touch are then made one, as pieces end to end along
    /// a line are when the longest of them is not in the middle. Each line is laid along its longest piece; a piece that
    /// strays from that line by more than the tolerance is put into a line of its own.
    /// </para>
    /// </remarks>
    internal static class LineMerge
    {
        internal static List<Piece2> Run(List<Piece2> input, double tol)
        {
            var pieces = new List<Piece2>(input.Count);

            foreach (Piece2 piece in input)
            {
                if (piece.Length > tol)
                {
                    pieces.Add(piece.Canonical());
                }
            }

            pieces.Sort(Piece2.Compare);
            var output = new List<Piece2>(pieces.Count);
            var all = new int[pieces.Count];
            var units = new Unit[pieces.Count];

            for (int i = 0; i < all.Length; i++)
            {
                all[i] = i;
                units[i] = new Unit(pieces[i]);
            }

            Group(pieces, units, all, tol, output);
            return output;
        }

        /// <summary>
        /// Groups some of the pieces into lines and lays each line out.
        /// </summary>
        private static void Group(List<Piece2> pieces, Unit[] units, int[] members, double tol, List<Piece2> output)
        {
            int n = members.Length;

            if (n == 0)
            {
                return;
            }

            // Pieces with the same ends to the bit, as rows of like parts one behind another give, lie side by side in the
            // order of pieces and go into one line together: only the first of each run is looked up, in a grid of the
            // firsts alone shaped as one of every piece, and the others follow it.
            var first = new int[n];
            var unique = new List<int>(n);

            for (int i = 0; i < n; i++)
            {
                first[i] = i > 0 && SameEnds(pieces[members[i]], pieces[members[i - 1]]) ? first[i - 1] : i;

                if (first[i] == i)
                {
                    unique.Add(i);
                }
            }

            int m = unique.Count;
            var minX = new double[m];
            var minY = new double[m];
            var maxX = new double[m];
            var maxY = new double[m];

            for (int u = 0; u < m; u++)
            {
                Piece2 p = pieces[members[unique[u]]];
                minX[u] = Math.Min(p.X0, p.X1);
                minY[u] = Math.Min(p.Y0, p.Y1);
                maxX[u] = Math.Max(p.X0, p.X1);
                maxY[u] = Math.Max(p.Y0, p.Y1);
            }

            var grid = new BoxGrid2(minX, minY, maxX, maxY, m, tol, n);
            var found = new List<int>();

            // The longest pieces first, each the line of a group of its own or joining the line it overlaps most, along the
            // stretch that line's group covers so far.
            var order = new int[n];

            for (int i = 0; i < n; i++)
            {
                order[i] = i;
            }

            Array.Sort(order, (a, b) => a == b ? 0 : Longer(units, members[a], members[b]) ? -1 : 1);
            var groupOf = new int[n];
            var lines = new List<int>();
            var from = new List<double>();
            var to = new List<double>();

            foreach (int i in order)
            {
                Piece2 p = pieces[members[i]];
                int best = -1;
                double bestOverlap = double.NegativeInfinity;
                double bestS0 = 0.0, bestS1 = 0.0;

                // A piece the same as the one before it, taken just before it, overlaps that one's line wholly, and no
                // line overlaps it more or comes first among those that overlap it as much.
                if (first[i] != i)
                {
                    best = groupOf[first[i]] - 1;
                    int r = members[lines[best]];
                    Along(pieces[r], units[r], p, out bestS0, out bestS1);
                    from[best] = Math.Min(from[best], bestS0);
                    to[best] = Math.Max(to[best], bestS1);
                    groupOf[i] = best + 1;
                    continue;
                }

                grid.QuerySegment(p.X0, p.Y0, p.X1, p.Y1, tol, found);

                foreach (int u in found)
                {
                    int j = unique[u];
                    int g = j == i ? -1 : groupOf[j] - 1;

                    if (g < 0)
                    {
                        continue;
                    }

                    int r = members[lines[g]];
                    Piece2 line = pieces[r];

                    if (!Near(line, units[r], p, tol))
                    {
                        continue;
                    }

                    Along(line, units[r], p, out double s0, out double s1);
                    double overlap = Math.Min(s1, to[g]) - Math.Max(s0, from[g]);

                    if (overlap >= -tol && (overlap > bestOverlap || overlap == bestOverlap && g < best))
                    {
                        best = g;
                        bestOverlap = overlap;
                        bestS0 = s0;
                        bestS1 = s1;
                    }
                }

                if (best < 0)
                {
                    best = lines.Count;
                    lines.Add(i);
                    from.Add(0.0);
                    to.Add(units[members[i]].Length);
                }
                else
                {
                    from[best] = Math.Min(from[best], bestS0);
                    to[best] = Math.Max(to[best], bestS1);
                }

                groupOf[i] = best + 1;
            }

            // Groups whose lines lie on one line and whose pieces touch are one line, as pieces end to end along it are
            // when the middle one is not the longest.
            var parent = new int[lines.Count];

            for (int g = 0; g < parent.Length; g++)
            {
                parent[g] = g;
            }

            foreach (int i in unique)
            {
                Piece2 p = pieces[members[i]];
                grid.QuerySegment(p.X0, p.Y0, p.X1, p.Y1, tol, found);

                foreach (int u in found)
                {
                    int j = unique[u];
                    int gi = groupOf[i] - 1;
                    int gj = groupOf[j] - 1;

                    if (Find(parent, gi) == Find(parent, gj))
                    {
                        continue;
                    }

                    bool iLonger = Longer(units, members[lines[gi]], members[lines[gj]]);
                    int r = members[lines[iLonger ? gi : gj]];
                    Piece2 longer = pieces[r];
                    Piece2 shorter = pieces[members[lines[iLonger ? gj : gi]]];

                    if (Near(longer, units[r], shorter, tol) && Near(longer, units[r], p, tol) && Near(longer, units[r], pieces[members[j]], tol))
                    {
                        Along(longer, units[r], p, out double a0, out double a1);
                        Along(longer, units[r], pieces[members[j]], out double b0, out double b1);

                        if (Math.Min(a1, b1) - Math.Max(a0, b0) >= -tol)
                        {
                            Union(parent, gi, gj);
                        }
                    }
                }
            }

            // The groups in the order of their first piece, each with its pieces in order.
            var groups = new Dictionary<int, List<int>>();
            var roots = new List<int>();

            for (int i = 0; i < n; i++)
            {
                int root = Find(parent, groupOf[i] - 1);

                if (!groups.TryGetValue(root, out List<int> group))
                {
                    group = new List<int>();
                    groups.Add(root, group);
                    roots.Add(root);
                }

                group.Add(members[i]);
            }

            var strays = new List<int>();

            foreach (int root in roots)
            {
                List<int> group = groups[root];
                int reference = group[0];

                foreach (int k in group)
                {
                    if (Longer(units, k, reference))
                    {
                        reference = k;
                    }
                }

                var kept = new List<int>(group.Count);

                foreach (int k in group)
                {
                    if (k == reference || Near(pieces[reference], units[reference], pieces[k], tol))
                    {
                        kept.Add(k);
                    }
                    else
                    {
                        strays.Add(k);
                    }
                }

                Lay(pieces, kept, reference, tol, output);
            }

            if (strays.Count > 0)
            {
                strays.Sort();
                Group(pieces, units, strays.ToArray(), tol, output);
            }
        }

        /// <summary>
        /// Whether one piece is longer than another, the lower in the order of pieces first where they are as long.
        /// </summary>
        private static bool Longer(Unit[] units, int a, int b)
        {
            double la = units[a].Length;
            double lb = units[b].Length;
            return la > lb || la == lb && a < b;
        }

        /// <summary>
        /// Where the ends of a piece lie along a reference piece's line, from its start: the lower first.
        /// </summary>
        private static void Along(Piece2 reference, Unit unit, Piece2 piece, out double s0, out double s1)
        {
            double ux = unit.Ux;
            double uy = unit.Uy;
            double a = ux * (piece.X0 - reference.X0) + uy * (piece.Y0 - reference.Y0);
            double b = ux * (piece.X1 - reference.X0) + uy * (piece.Y1 - reference.Y0);
            s0 = Math.Min(a, b);
            s1 = Math.Max(a, b);
        }

        /// <summary>
        /// Whether both ends of a piece lie within the tolerance of a reference piece's line.
        /// </summary>
        private static bool Near(Piece2 reference, Unit unit, Piece2 piece, double tol)
        {
            double ux = unit.Ux;
            double uy = unit.Uy;
            double ax = piece.X0 - reference.X0;
            double ay = piece.Y0 - reference.Y0;
            double bx = piece.X1 - reference.X0;
            double by = piece.Y1 - reference.Y0;
            return Math.Abs(ux * ay - uy * ax) <= tol && Math.Abs(ux * by - uy * bx) <= tol;
        }

        /// <summary>
        /// Whether two pieces have the same ends to the bit, so that every test of one gives what it gives of the other.
        /// </summary>
        private static bool SameEnds(Piece2 a, Piece2 b)
            => BitConverter.DoubleToInt64Bits(a.X0) == BitConverter.DoubleToInt64Bits(b.X0)
            && BitConverter.DoubleToInt64Bits(a.Y0) == BitConverter.DoubleToInt64Bits(b.Y0)
            && BitConverter.DoubleToInt64Bits(a.X1) == BitConverter.DoubleToInt64Bits(b.X1)
            && BitConverter.DoubleToInt64Bits(a.Y1) == BitConverter.DoubleToInt64Bits(b.Y1);

        /// <summary>
        /// Lays the pieces of one line along its reference piece, and gives the line's stretches.
        /// </summary>
        private static void Lay(List<Piece2> pieces, List<int> group, int reference, double tol, List<Piece2> output)
        {
            Piece2 r = pieces[reference];
            double length = r.Length;
            double ux = (r.X1 - r.X0) / length;
            double uy = (r.Y1 - r.Y0) / length;
            int m = group.Count;
            var from = new double[m];
            var to = new double[m];
            var breaks = new double[2 * m];

            for (int i = 0; i < m; i++)
            {
                Piece2 p = pieces[group[i]];
                double s0 = ux * (p.X0 - r.X0) + uy * (p.Y0 - r.Y0);
                double s1 = ux * (p.X1 - r.X0) + uy * (p.Y1 - r.Y0);
                from[i] = Math.Min(s0, s1);
                to[i] = Math.Max(s0, s1);
                breaks[2 * i] = from[i];
                breaks[2 * i + 1] = to[i];
            }

            Array.Sort(breaks);

            // Ends within the tolerance of the one before are one end, standing where the first of them does.
            var stations = new List<double>();
            var stationOf = new int[breaks.Length];

            for (int i = 0; i < breaks.Length; i++)
            {
                if (i == 0 || breaks[i] - breaks[i - 1] > tol)
                {
                    stations.Add(breaks[i]);
                }

                stationOf[i] = stations.Count - 1;
            }

            var first = new int[m];
            var last = new int[m];

            for (int i = 0; i < m; i++)
            {
                first[i] = stationOf[Array.BinarySearch(breaks, from[i])];
                last[i] = stationOf[Array.BinarySearch(breaks, to[i])];
            }

            int open = -1;
            Piece2 current = default(Piece2);

            for (int s = 0; s + 1 < stations.Count; s++)
            {
                bool any = false;
                bool seen = false;

                for (int i = 0; i < m; i++)
                {
                    if (first[i] <= s && last[i] > s)
                    {
                        any = true;
                        seen |= pieces[group[i]].Visibility == EdgeVisibility.Visible;
                    }
                }

                Piece2 stretch = default(Piece2);

                if (any)
                {
                    EdgeVisibility visibility = seen ? EdgeVisibility.Visible : EdgeVisibility.Hidden;
                    EdgeKind kind = EdgeKind.Silhouette;
                    var objects = new List<int>();

                    for (int i = 0; i < m; i++)
                    {
                        Piece2 p = pieces[group[i]];

                        if (first[i] <= s && last[i] > s && p.Visibility == visibility)
                        {
                            kind = Rank(p.Kind) > Rank(kind) ? p.Kind : kind;

                            foreach (int o in p.Objects)
                            {
                                objects.Add(o);
                            }
                        }
                    }

                    objects.Sort();
                    int distinct = 0;

                    for (int i = 0; i < objects.Count; i++)
                    {
                        if (i == 0 || objects[i] != objects[i - 1])
                        {
                            objects[distinct++] = objects[i];
                        }
                    }

                    objects.RemoveRange(distinct, objects.Count - distinct);
                    stretch = new Piece2 { Visibility = visibility, Kind = kind, Objects = objects.ToArray() };
                }

                if (open >= 0 && (!any || !Same(current, stretch)))
                {
                    output.Add(Along(r, ux, uy, stations[open], stations[s], current));
                    open = -1;
                }

                if (any && open < 0)
                {
                    open = s;
                    current = stretch;
                }
            }

            if (open >= 0)
            {
                output.Add(Along(r, ux, uy, stations[open], stations[stations.Count - 1], current));
            }
        }

        private static int Rank(EdgeKind kind) => kind == EdgeKind.SectionOutline ? 2 : kind == EdgeKind.Sharp ? 1 : 0;

        private static bool Same(Piece2 a, Piece2 b)
            => a.Visibility == b.Visibility && a.Kind == b.Kind && Piece2.CompareObjects(a.Objects, b.Objects) == 0;

        private static Piece2 Along(Piece2 r, double ux, double uy, double s0, double s1, Piece2 attributes)
        {
            Piece2 piece = attributes;
            piece.X0 = s0 == 0.0 ? r.X0 : r.X0 + s0 * ux;
            piece.Y0 = s0 == 0.0 ? r.Y0 : r.Y0 + s0 * uy;
            piece.X1 = r.X0 + s1 * ux;
            piece.Y1 = r.Y0 + s1 * uy;
            return piece.Canonical();
        }

        private static int Find(int[] parent, int i)
        {
            while (parent[i] != i)
            {
                parent[i] = parent[parent[i]];
                i = parent[i];
            }

            return i;
        }

        /// <summary>
        /// Joins two groups, the lower root standing for both, so that the groups do not hang on the order of the unions.
        /// </summary>
        private static void Union(int[] parent, int a, int b)
        {
            int ra = Find(parent, a);
            int rb = Find(parent, b);

            if (ra < rb)
            {
                parent[rb] = ra;
            }
            else if (rb < ra)
            {
                parent[ra] = rb;
            }
        }

        /// <summary>
        /// The length of a piece and the unit vector along it, worked out once for every test that reads them.
        /// </summary>
        private readonly struct Unit
        {
            internal Unit(Piece2 piece)
            {
                Length = piece.Length;
                Ux = (piece.X1 - piece.X0) / Length;
                Uy = (piece.Y1 - piece.Y0) / Length;
            }

            internal double Length { get; }

            internal double Ux { get; }

            internal double Uy { get; }
        }
    }
}
