using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// OVERKILL as AutoCAD 2021 does it (probed over its console with -OVERKILL): the lines, arcs, circles, points and
    /// polylines selected are compared by their properties and their place. Two points, circles, arcs or lines of the same
    /// place are duplicates and the last of them stays; lines and arcs on one carrier (one straight line, one circle) that
    /// lie within another are deleted, those that overlap in part or meet end to end are made one, the last of them stretched
    /// over the whole; a segment of a polyline takes part like a line, cut out of the polyline where it is deleted or made one
    /// with another, a polyline whole taking the place of a line it holds, and consecutive collinear segments and duplicate
    /// vertices of one polyline joining up in place. Two polylines of the same vertices are duplicates. Arcs that make up a
    /// whole circle become one; an arc on a circle of the selection is deleted. Polyline arc segments take part only by being
    /// compared whole. A line of 100 over another of 100 is a duplicate; one of 50 within it, an overlap.
    /// </summary>
    internal static class CadOverkill
    {
        // How far two directions may differ, as the cross product of their unit vectors.
        private const double Parallel = 1E-6;

        /// <summary>One straight or curved piece of the selection: a line, an arc, a circle, or a segment of a polyline.</summary>
        private sealed class Piece
        {
            internal int Order;
            internal int Part;
            internal string Key;
            internal DxfEntity Entity;
            internal Segment Segment;
            internal bool Line;
            internal bool Gone;

            // A line: its ends, its direction canonical (X up, then Y up), and its place along it.
            internal GeoPoint3 A;
            internal GeoPoint3 B;
            internal double Ux;
            internal double Uy;
            internal bool Reversed;

            // An arc or a circle: its centre, radius, start and sweep (counter-clockwise), whether it is a whole circle.
            internal GeoPoint3 Center;
            internal double Radius;
            internal double Sweep;
            internal bool Full;

            // The span on the carrier: along the line, or the start angle and the end angle past it.
            internal double S;
            internal double E;

            internal bool InPolyline => Segment != null;
        }

        /// <summary>One segment of a polyline taken apart: its ends, bulge and widths, and what became of it.</summary>
        private sealed class Segment
        {
            internal Pline Owner;
            internal int Index;
            internal GeoPoint2 A;
            internal GeoPoint2 B;
            internal double Bulge;
            internal double StartWidth;
            internal double EndWidth;
            internal bool Removed;
            internal bool Dropped;
        }

        /// <summary>A polyline taken apart.</summary>
        private sealed class Pline
        {
            internal DxfPolyline Entity;
            internal List<Segment> Segments = new List<Segment>();
            internal bool Closed;
            internal bool Touched;
            internal double Elevation;
            internal int Order;
            internal string Key;
        }

        private sealed class Work
        {
            internal CadOverkillSettings Settings;
            internal double Half;
            internal int Duplicates;
            internal int Overlaps;
            internal List<DxfEntity> Erase = new List<DxfEntity>();
            internal List<DxfEntity> Reshape = new List<DxfEntity>();
            internal List<Action<CadEditScope>> Edits = new List<Action<CadEditScope>>();
            internal List<Pline> Plines = new List<Pline>();
        }

        /// <summary>
        /// Runs OVERKILL over objects of the space shown, one undo step. Objects of other kinds, arcs, circles and polylines
        /// not in the plan, and those on locked layers are left as they are.
        /// </summary>
        internal static CadOverkillResult Run(
            CadCommandContext c,
            IReadOnlyList<string> handles,
            CadOverkillSettings settings
        )
        {
            var chosen = new HashSet<string>(handles, StringComparer.OrdinalIgnoreCase);
            var entities = new List<DxfEntity>();

            foreach (DxfEntity entity in c.Space.Block)
            {
                if (!entity.IsErased && entity.Handle != null && chosen.Contains(entity.Handle))
                {
                    entities.Add(entity);
                }
            }

            var work = new Work { Settings = settings, Half = Math.Max(settings.Tolerance, 0.0) / 2.0 };
            Plan(entities, work);

            var planned = new List<CadEditRecord>();

            foreach (DxfEntity entity in work.Erase)
            {
                planned.Add(new CadEditRecord(CadEditRecordKind.Erased, entity.Handle));
            }

            foreach (DxfEntity entity in work.Reshape)
            {
                planned.Add(new CadEditRecord(CadEditRecordKind.Reshaped, entity.Handle));
            }

            if (planned.Count > 0 || work.Edits.Count > 0)
            {
                CadSpace space = c.Space;
                c.Apply(planned, s =>
                {
                    foreach (Action<CadEditScope> edit in work.Edits)
                    {
                        edit(s);
                    }

                    foreach (DxfEntity entity in work.Erase)
                    {
                        entity.Erase();
                    }
                });
            }

            return new CadOverkillResult(work.Duplicates, work.Overlaps);
        }

        private static void Plan(List<DxfEntity> entities, Work w)
        {
            var points = new List<DxfDBPoint>();
            var plines = new List<Pline>();
            var pieces = new List<Piece>();
            int order = 0;

            foreach (DxfEntity entity in entities)
            {
                int at = order++;
                string key = Key(w.Settings, entity);

                switch (entity)
                {
                    case DxfDBPoint point when UpNormal(point.Normal):
                        points.Add(point);
                        break;
                    case DxfLine line:
                        AddLine(
                            pieces,
                            entity,
                            key + (w.Settings.IgnorePolylineWidths ? string.Empty : "|w0,0"),
                            at,
                            0,
                            line.StartPoint,
                            line.EndPoint,
                            w.Half,
                            null
                        );
                        break;
                    case DxfArc arc when UpNormal(arc.Normal):
                    {
                        double sweep = Turn(arc.EndAngle - arc.StartAngle);

                        if (sweep > 1E-12 && arc.Radius > w.Half)
                        {
                            pieces.Add(
                                Round(entity, key, at, arc.Center, arc.Radius, Turn(arc.StartAngle), sweep, false)
                            );
                        }

                        break;
                    }
                    case DxfCircle circle when UpNormal(circle.Normal) && circle.Radius > w.Half:
                        pieces.Add(Round(entity, key, at, circle.Center, circle.Radius, 0.0, 2.0 * Math.PI, true));
                        break;
                    case DxfPolyline polyline when UpNormal(polyline.Normal) && polyline.NumberOfVertices > 1:
                        plines.Add(TakeApart(polyline, at, key));
                        break;
                }
            }

            ErasePointDuplicates(points, entities, w);
            ErasePolylineDuplicates(plines, w);
            w.Plines.AddRange(plines);

            if (w.Settings.OptimizePolylines)
            {
                foreach (Pline pline in plines)
                {
                    if (pline.Entity == null)
                    {
                        continue;
                    }

                    foreach (Segment segment in pline.Segments)
                    {
                        if (segment.Removed)
                        {
                            continue;
                        }

                        double length = segment.A.DistanceTo(segment.B);

                        if (segment.Bulge != 0.0)
                        {
                            continue;
                        }

                        if (length <= w.Half)
                        {
                            // A vertex repeated: the segment is no part of the shape.
                            segment.Dropped = true;
                            pline.Touched = true;
                            w.Overlaps++;
                            continue;
                        }

                        string widths = w.Settings.IgnorePolylineWidths
                            ? string.Empty
                            : "|w" + Num(segment.StartWidth) + "," + Num(segment.EndWidth);
                        AddLine(
                            pieces,
                            pline.Entity,
                            pline.Key + widths,
                            pline.Order,
                            segment.Index,
                            new GeoPoint3(segment.A.X, segment.A.Y, pline.Elevation),
                            new GeoPoint3(segment.B.X, segment.B.Y, pline.Elevation),
                            w.Half,
                            segment
                        );
                    }
                }
            }

            foreach (List<Piece> carrier in Carriers(pieces.Where(p => p.Line).ToList(), true, w.Half))
            {
                Resolve(carrier, true, w);
            }

            foreach (List<Piece> carrier in Carriers(pieces.Where(p => !p.Line).ToList(), false, w.Half))
            {
                Resolve(carrier, false, w);
            }

            foreach (Pline pline in w.Plines)
            {
                if (pline.Touched)
                {
                    Rebuild(pline, w);
                }
            }
        }

        // ---- properties ----

        private static string Key(CadOverkillSettings settings, DxfEntity e)
        {
            CadOverkillIgnore ignore = settings.Ignore;
            var key = new StringBuilder();

            if ((ignore & CadOverkillIgnore.Layer) == 0)
            {
                key.Append("L:").Append(e.Layer).Append('|');
            }

            if ((ignore & CadOverkillIgnore.Color) == 0)
            {
                key.Append("C:").Append(e.Color?.ToString()).Append('|');
            }

            if ((ignore & CadOverkillIgnore.Linetype) == 0)
            {
                key.Append("T:").Append(e.Linetype).Append('|');
            }

            if ((ignore & CadOverkillIgnore.LinetypeScale) == 0)
            {
                key.Append("S:").Append(Num(e.LinetypeScale)).Append('|');
            }

            if ((ignore & CadOverkillIgnore.Lineweight) == 0)
            {
                key.Append("W:").Append((int)e.LineWeight).Append('|');
            }

            if ((ignore & CadOverkillIgnore.Thickness) == 0)
            {
                key.Append("K:").Append(Num(Thickness(e))).Append('|');
            }

            if ((ignore & CadOverkillIgnore.Transparency) == 0)
            {
                DxfTransparency t = e.Transparency;
                key.Append("A:")
                    .Append(
                        t.IsByBlock ? "b"
                        : t.IsByAlpha ? t.Alpha.ToString(CultureInfo.InvariantCulture)
                        : "l"
                    )
                    .Append('|');
            }

            if ((ignore & CadOverkillIgnore.PlotStyle) == 0)
            {
                key.Append("P:").Append(e.Item.File.CommonValueOf(e.Item, 390)?.Trim()).Append('|');
            }

            if ((ignore & CadOverkillIgnore.Material) == 0)
            {
                key.Append("M:").Append(e.Item.File.CommonValueOf(e.Item, 347)?.Trim()).Append('|');
            }

            return key.ToString();
        }

        private static double Thickness(DxfEntity e)
        {
            switch (e)
            {
                case DxfLine line:
                    return line.Thickness;
                case DxfArc arc:
                    return arc.Thickness;
                case DxfCircle circle:
                    return circle.Thickness;
                case DxfPolyline polyline:
                    return polyline.Thickness;
                case DxfDBPoint point:
                    return point.Thickness;
                default:
                    return 0.0;
            }
        }

        private static string Num(double value) =>
            Math.Round(value, 6).ToString("0.######", CultureInfo.InvariantCulture);

        private static bool UpNormal(GeoVector3 normal) =>
            Math.Abs(normal.X) < 1E-9 && Math.Abs(normal.Y) < 1E-9 && normal.Z > 0.0;

        private static double Turn(double angle)
        {
            double whole = 2.0 * Math.PI;
            double a = angle % whole;
            a = a < 0.0 ? a + whole : a;
            return a;
        }

        // ---- pieces ----

        private static void AddLine(
            List<Piece> pieces,
            DxfEntity entity,
            string key,
            int order,
            int part,
            GeoPoint3 a,
            GeoPoint3 b,
            double half,
            Segment segment
        )
        {
            double dx = b.X - a.X;
            double dy = b.Y - a.Y;
            double length = Math.Sqrt(dx * dx + dy * dy);

            if (length <= half || Math.Abs(b.Z - a.Z) > half)
            {
                return;
            }

            double ux = dx / length;
            double uy = dy / length;
            bool reversed = ux < -1E-12 || (Math.Abs(ux) <= 1E-12 && uy < 0.0);

            if (reversed)
            {
                ux = -ux;
                uy = -uy;
            }

            double ta = a.X * ux + a.Y * uy;
            double tb = b.X * ux + b.Y * uy;
            pieces.Add(
                new Piece
                {
                    Line = true,
                    Entity = entity,
                    Key = key,
                    Order = order,
                    Part = part,
                    A = a,
                    B = b,
                    Ux = ux,
                    Uy = uy,
                    Reversed = reversed,
                    S = Math.Min(ta, tb),
                    E = Math.Max(ta, tb),
                    Segment = segment,
                }
            );
        }

        private static Piece Round(
            DxfEntity entity,
            string key,
            int order,
            GeoPoint3 center,
            double radius,
            double start,
            double sweep,
            bool full
        ) =>
            new Piece
            {
                Entity = entity,
                Key = key,
                Order = order,
                Center = center,
                Radius = radius,
                Sweep = sweep,
                Full = full,
                S = start,
                E = start + sweep,
            };

        // ---- carriers ----

        /// <summary>Groups the pieces that lie on one carrier, a straight line or a circle; each group by the order of its first piece, the pieces of one in the order of the drawing.</summary>
        private static List<List<Piece>> Carriers(List<Piece> pieces, bool lines, double half)
        {
            int n = pieces.Count;
            var parent = new int[n];

            for (int i = 0; i < n; i++)
            {
                parent[i] = i;
            }

            int Find(int x)
            {
                while (parent[x] != x)
                {
                    parent[x] = parent[parent[x]];
                    x = parent[x];
                }

                return x;
            }

            double bin = Math.Max(half * 2.0, 1E-9);
            var cells = new Dictionary<Tuple<long, long, long>, List<int>>();

            for (int i = 0; i < n; i++)
            {
                Piece p = pieces[i];
                long a;
                long b;
                long z;

                if (lines)
                {
                    a = (long)Math.Round(Math.Atan2(p.Uy, p.Ux) / Parallel);
                    b = (long)Math.Round((-p.A.X * p.Uy + p.A.Y * p.Ux) / bin);
                    z = (long)Math.Round(p.A.Z / bin);
                }
                else
                {
                    a = (long)Math.Round(p.Center.X / bin);
                    b = (long)Math.Round(p.Center.Y / bin);
                    z = (long)Math.Round(p.Radius / bin);
                }

                for (int da = -1; da <= 1; da++)
                {
                    for (int db = -1; db <= 1; db++)
                    {
                        for (int dz = -1; dz <= 1; dz++)
                        {
                            if (cells.TryGetValue(Tuple.Create(a + da, b + db, z + dz), out List<int> near))
                            {
                                foreach (int j in near)
                                {
                                    if (lines ? SameLine(p, pieces[j], half) : SameCircle(p, pieces[j], half))
                                    {
                                        int ra = Find(i);
                                        int rb = Find(j);

                                        if (ra != rb)
                                        {
                                            parent[Math.Max(ra, rb)] = Math.Min(ra, rb);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                var own = Tuple.Create(a, b, z);

                if (!cells.TryGetValue(own, out List<int> list))
                {
                    list = new List<int>();
                    cells.Add(own, list);
                }

                list.Add(i);
            }

            var groups = new List<List<Piece>>();
            var byRoot = new Dictionary<int, List<Piece>>();

            for (int i = 0; i < n; i++)
            {
                int root = Find(i);

                if (!byRoot.TryGetValue(root, out List<Piece> group))
                {
                    group = new List<Piece>();
                    byRoot.Add(root, group);
                    groups.Add(group);
                }

                group.Add(pieces[i]);
            }

            return groups;
        }

        /// <summary>Whether a distance is within the tolerance: below it, or none at all, so that a tolerance of nought still takes the same for the same.</summary>
        private static bool Within(double distance, double half) =>
            Math.Abs(distance) < half || Math.Abs(distance) <= 1E-12;

        private static bool SameLine(Piece a, Piece b, double half)
        {
            double cross = a.Ux * b.Uy - a.Uy * b.Ux;

            return Math.Abs(cross) <= Parallel
                && Within(a.A.Z - b.A.Z, half)
                && Within((b.A.X - a.A.X) * a.Uy - (b.A.Y - a.A.Y) * a.Ux, half)
                && Within((b.B.X - a.A.X) * a.Uy - (b.B.Y - a.A.Y) * a.Ux, half)
                && a.Key == b.Key;
        }

        private static bool SameCircle(Piece a, Piece b, double half) =>
            Within(a.Center.X - b.Center.X, half)
            && Within(a.Center.Y - b.Center.Y, half)
            && Within(a.Center.Z - b.Center.Z, half)
            && Within(a.Radius - b.Radius, half)
            && a.Key == b.Key;

        // ---- spans ----

        /// <summary>The distance in the span's own measure that stands for the tolerance: the length along a line, the angle on a circle.</summary>
        private static double Slack(Piece p, bool lines, double half) => lines ? half : half / p.Radius;

        /// <summary>Whether Y holds X, within the tolerance.</summary>
        private static bool Holds(Piece y, Piece x, bool lines, double half)
        {
            double slack = Slack(y, lines, half);

            if (lines)
            {
                return y.S <= x.S + slack && y.E >= x.E - slack;
            }

            if (y.Full)
            {
                return true;
            }

            if (x.Full)
            {
                return false;
            }

            double whole = 2.0 * Math.PI;
            double d = Turn(x.S - y.S);
            d = d > whole - slack ? d - whole : d;
            return d >= -slack && d + x.Sweep <= y.Sweep + slack;
        }

        private static bool Same(Piece a, Piece b, bool lines, double half) =>
            Holds(a, b, lines, half) && Holds(b, a, lines, half);

        /// <summary>Whether Y wins over X where they lie the same: a segment of a polyline over a loose object, then the later in the drawing.</summary>
        private static bool Wins(Piece y, Piece x)
        {
            if (y.InPolyline != x.InPolyline)
            {
                return y.InPolyline;
            }

            return y.Order != x.Order ? y.Order > x.Order : y.Part > x.Part;
        }

        private static bool CanGo(Piece p, Work w) =>
            !p.InPolyline || (!w.Settings.DoNotBreakPolylines && !p.Segment.Dropped);

        // ---- one carrier ----

        private static void Resolve(List<Piece> carrier, bool lines, Work w)
        {
            double half = w.Half;
            List<Piece> live = carrier.OrderBy(p => p.Order).ThenBy(p => p.Part).ToList();

            if (lines)
            {
                // One direction for the whole carrier, so that the places along it agree to the tolerance.
                double ux = live[0].Ux;
                double uy = live[0].Uy;

                foreach (Piece p in live)
                {
                    double ta = p.A.X * ux + p.A.Y * uy;
                    double tb = p.B.X * ux + p.B.Y * uy;
                    p.Ux = ux;
                    p.Uy = uy;
                    p.Reversed = tb < ta;
                    p.S = Math.Min(ta, tb);
                    p.E = Math.Max(ta, tb);
                }
            }

            // The same: all but the winner of those that lie alike are deleted. A line over a line is a duplicate; with a
            // segment of a polyline in it, an overlap, taken only where overlaps are.
            var lost = new List<Piece>();

            foreach (Piece x in live)
            {
                foreach (Piece y in live)
                {
                    if (ReferenceEquals(x, y) || !Same(x, y, lines, half) || !Wins(y, x))
                    {
                        continue;
                    }

                    bool duplicate = !x.InPolyline && !y.InPolyline;

                    if (duplicate || (w.Settings.CombinePartialOverlaps && CanGo(x, w)))
                    {
                        lost.Add(x);
                        Lose(x, w, duplicate);
                    }

                    break;
                }
            }

            foreach (Piece x in lost)
            {
                x.Gone = true;
            }

            live = live.Where(p => !p.Gone).ToList();

            // Within another: deleted, where overlaps are combined.
            if (w.Settings.CombinePartialOverlaps)
            {
                var within = new List<Piece>();

                foreach (Piece x in live)
                {
                    if (!CanGo(x, w))
                    {
                        continue;
                    }

                    if (live.Any(y => !ReferenceEquals(x, y) && Holds(y, x, lines, half) && !Holds(x, y, lines, half)))
                    {
                        within.Add(x);
                    }
                }

                foreach (Piece x in within)
                {
                    x.Gone = true;
                    Lose(x, w, false);
                }

                live = live.Where(p => !p.Gone).ToList();
            }

            if (!w.Settings.CombinePartialOverlaps && !w.Settings.CombineEndToEnd)
            {
                return;
            }

            // Made one: those that overlap in part or meet end to end.
            live = live.Where(p => lines || !p.Full).ToList();
            List<List<Piece>> groups = lines ? LineGroups(live, w) : ArcGroups(live, w);

            foreach (List<Piece> group in groups)
            {
                if (group.Count > 1)
                {
                    if (lines)
                    {
                        MergeLines(group, w);
                    }
                    else
                    {
                        MergeArcs(group, w);
                    }
                }
            }
        }

        /// <summary>Deletes a piece that lost: its object erased where it is loose, its segment cut out of its polyline where it is not.</summary>
        private static void Lose(Piece x, Work w, bool duplicate)
        {
            if (duplicate)
            {
                w.Duplicates++;
            }
            else
            {
                w.Overlaps++;
            }

            if (x.InPolyline)
            {
                x.Segment.Removed = true;
                x.Segment.Owner.Touched = true;
            }
            else
            {
                w.Erase.Add(x.Entity);
            }
        }

        private static List<List<Piece>> LineGroups(List<Piece> live, Work w)
        {
            var groups = new List<List<Piece>>();
            List<Piece> sorted = live.OrderBy(p => p.S)
                .ThenByDescending(p => p.E)
                .ThenBy(p => p.Order)
                .ThenBy(p => p.Part)
                .ToList();
            List<Piece> current = null;
            double reach = 0.0;

            foreach (Piece p in sorted)
            {
                bool link =
                    current != null
                    && (
                        (w.Settings.CombinePartialOverlaps && p.S < reach - w.Half)
                        || (w.Settings.CombineEndToEnd && Math.Abs(p.S - reach) <= w.Half)
                    );

                if (link)
                {
                    current.Add(p);
                    reach = Math.Max(reach, p.E);
                }
                else
                {
                    current = new List<Piece> { p };
                    groups.Add(current);
                    reach = p.E;
                }
            }

            return groups;
        }

        private static List<List<Piece>> ArcGroups(List<Piece> live, Work w)
        {
            var groups = new List<List<Piece>>();

            if (live.Count == 0)
            {
                return groups;
            }

            double whole = 2.0 * Math.PI;
            double slack = w.Half / live[0].Radius;
            List<Piece> sorted = live.OrderBy(p => p.S).ThenByDescending(p => p.Sweep).ThenBy(p => p.Order).ToList();
            var reaches = new List<double>();
            List<Piece> current = null;
            double reach = 0.0;

            foreach (Piece p in sorted)
            {
                bool link =
                    current != null
                    && (
                        (w.Settings.CombinePartialOverlaps && p.S < reach - slack)
                        || (w.Settings.CombineEndToEnd && Math.Abs(p.S - reach) <= slack)
                    );

                if (link)
                {
                    current.Add(p);
                    reach = Math.Max(reach, p.S + p.Sweep);
                    reaches[reaches.Count - 1] = reach;
                }
                else
                {
                    current = new List<Piece> { p };
                    groups.Add(current);
                    reach = p.S + p.Sweep;
                    reaches.Add(reach);
                }
            }

            // The last group may run on round to the first.
            double first = groups[0][0].S + whole;
            double last = reaches[reaches.Count - 1];
            bool round =
                (w.Settings.CombinePartialOverlaps && last > first + slack)
                || (w.Settings.CombineEndToEnd && Math.Abs(last - first) <= slack);

            if (round && groups.Count > 1)
            {
                List<Piece> tail = groups[groups.Count - 1];
                tail.AddRange(groups[0]);
                groups.RemoveAt(0);
                // The tail's own start stays its start: the pieces are listed from there.
                double origin = tail[0].S;
                tail.Sort((p, q) => Turn(p.S - origin).CompareTo(Turn(q.S - origin)));
            }
            else if (round && groups[0].Count > 1)
            {
                // One group that closes on itself: a whole circle.
                groups[0].Sort((p, q) => p.S.CompareTo(q.S));
            }

            return groups;
        }

        private static void MergeLines(List<Piece> group, Work w)
        {
            // A run of consecutive segments of one polyline, in one direction: joined in place, the vertices between them gone.
            if (group.All(p => p.InPolyline) && group.Select(p => p.Segment.Owner).Distinct().Count() == 1)
            {
                List<Piece> inOrder = group.OrderBy(p => p.Segment.Index).ToList();
                bool consecutive = true;
                Pline owner = inOrder[0].Segment.Owner;

                for (int i = 1; i < inOrder.Count; i++)
                {
                    // Only segments dropped for being no length may lie between them.
                    for (int k = inOrder[i - 1].Segment.Index + 1; k < inOrder[i].Segment.Index; k++)
                    {
                        consecutive &= owner.Segments[k].Dropped;
                    }

                    consecutive &= inOrder[i].Segment.Index > inOrder[i - 1].Segment.Index;
                    consecutive &= inOrder[i].Reversed == inOrder[0].Reversed;
                }

                if (consecutive)
                {
                    Segment head = inOrder[0].Segment;
                    head.B = inOrder[inOrder.Count - 1].Segment.B;
                    head.EndWidth = inOrder[inOrder.Count - 1].Segment.EndWidth;

                    for (int i = 1; i < inOrder.Count; i++)
                    {
                        inOrder[i].Segment.Dropped = true;
                    }

                    head.Owner.Touched = true;
                    w.Overlaps += group.Count - 1;
                    return;
                }
            }

            List<Piece> loose = group.Where(p => !p.InPolyline).ToList();
            List<Piece> cut = group.Where(p => p.InPolyline).ToList();

            if (cut.Any(p => w.Settings.DoNotBreakPolylines))
            {
                return;
            }

            Piece low = group.OrderBy(p => p.S).ThenByDescending(p => p.Order).ThenByDescending(p => p.Part).First();
            Piece high = group
                .OrderByDescending(p => p.E)
                .ThenByDescending(p => p.Order)
                .ThenByDescending(p => p.Part)
                .First();
            GeoPoint3 start = AtLow(low);
            GeoPoint3 end = AtHigh(high);
            w.Overlaps += group.Count - 1;

            foreach (Piece p in cut)
            {
                p.Segment.Removed = true;
                p.Segment.Owner.Touched = true;
            }

            if (loose.Count > 0)
            {
                Piece survivor = loose.OrderBy(p => p.Order).Last();

                foreach (Piece p in loose)
                {
                    if (!ReferenceEquals(p, survivor))
                    {
                        w.Erase.Add(p.Entity);
                    }
                }

                var line = (DxfLine)survivor.Entity;
                w.Reshape.Add(line);
                bool backwards = survivor.Reversed;
                w.Edits.Add(s =>
                {
                    line.StartPoint = backwards ? end : start;
                    line.EndPoint = backwards ? start : end;
                });
                return;
            }

            Piece template = cut.OrderBy(p => p.Order).ThenBy(p => p.Part).Last();
            w.Edits.Add(s =>
            {
                DxfEntity made = s.Append(s.Document.FindSpace(template.Entity.Handle), new DxfLine(start, end));
                CadPropertyMatcher.Apply(s.Database, template.Entity, made, CadMatchProperties.All);
            });
        }

        private static GeoPoint3 AtLow(Piece p) =>
            (p.A.X * p.Ux + p.A.Y * p.Uy) <= (p.B.X * p.Ux + p.B.Y * p.Uy) ? p.A : p.B;

        private static GeoPoint3 AtHigh(Piece p) =>
            (p.A.X * p.Ux + p.A.Y * p.Uy) <= (p.B.X * p.Ux + p.B.Y * p.Uy) ? p.B : p.A;

        private static void MergeArcs(List<Piece> group, Work w)
        {
            double whole = 2.0 * Math.PI;
            Piece head = group[0];
            double slack = w.Half / head.Radius;
            double start = head.S;
            double end = start;

            foreach (Piece p in group)
            {
                double from = Turn(p.S - start);
                from = from > whole - slack ? from - whole : from;
                end = Math.Max(end, start + from + p.Sweep);
            }

            Piece survivor = group.OrderBy(p => p.Order).Last();
            bool full = end - start >= whole - slack || group.Any(p => p.Full);
            w.Overlaps += group.Count - 1;

            foreach (Piece p in group)
            {
                if (!ReferenceEquals(p, survivor))
                {
                    w.Erase.Add(p.Entity);
                }
            }

            if (full)
            {
                if (survivor.Full)
                {
                    return;
                }

                w.Erase.Add(survivor.Entity);
                GeoPoint3 centre = survivor.Center;
                double radius = survivor.Radius;
                DxfEntity source = survivor.Entity;
                w.Edits.Add(s =>
                {
                    var circle = new DxfCircle(centre, new GeoVector3(0.0, 0.0, 1.0), radius);
                    s.Append(s.Document.FindSpace(source.Handle), circle);
                    CadPropertyMatcher.Apply(s.Database, source, circle, CadMatchProperties.All);
                });
                return;
            }

            var arc = (DxfArc)survivor.Entity;
            w.Reshape.Add(arc);
            double startAngle = Turn(start);
            double endAngle = Turn(end);
            w.Edits.Add(s =>
            {
                arc.StartAngle = startAngle;
                arc.EndAngle = endAngle;
            });
        }

        // ---- points ----

        private static void ErasePointDuplicates(List<DxfDBPoint> points, List<DxfEntity> entities, Work w)
        {
            if (points.Count < 2)
            {
                return;
            }

            double bin = Math.Max(w.Half * 2.0, 1E-9);
            var cells = new Dictionary<Tuple<long, long, long>, List<DxfDBPoint>>();
            var gone = new HashSet<DxfDBPoint>();

            // The last of the points in the drawing stays, so each is compared with those after it.
            for (int i = points.Count - 1; i >= 0; i--)
            {
                DxfDBPoint p = points[i];
                GeoPoint3 at = p.Position;
                string key = Key(w.Settings, p);
                long a = (long)Math.Round(at.X / bin);
                long b = (long)Math.Round(at.Y / bin);
                long z = (long)Math.Round(at.Z / bin);
                bool same = false;

                for (int da = -1; da <= 1 && !same; da++)
                {
                    for (int db = -1; db <= 1 && !same; db++)
                    {
                        for (int dz = -1; dz <= 1 && !same; dz++)
                        {
                            if (cells.TryGetValue(Tuple.Create(a + da, b + db, z + dz), out List<DxfDBPoint> near))
                            {
                                same = near.Any(q =>
                                    Key(w.Settings, q) == key && Within(q.Position.DistanceTo(at), w.Half)
                                );
                            }
                        }
                    }
                }

                if (same)
                {
                    gone.Add(p);
                    w.Duplicates++;
                    w.Erase.Add(p);
                    continue;
                }

                var own = Tuple.Create(a, b, z);

                if (!cells.TryGetValue(own, out List<DxfDBPoint> list))
                {
                    list = new List<DxfDBPoint>();
                    cells.Add(own, list);
                }

                list.Add(p);
            }
        }

        // ---- polylines ----

        private static Pline TakeApart(DxfPolyline polyline, int order, string key)
        {
            int n = polyline.NumberOfVertices;
            var pline = new Pline
            {
                Entity = polyline,
                Closed = polyline.Closed,
                Elevation = polyline.Elevation,
                Order = order,
                Key = key,
            };
            int count = pline.Closed ? n : n - 1;

            for (int i = 0; i < count; i++)
            {
                GeoPoint2 a = polyline.GetPoint2dAt(i);
                GeoPoint2 b = polyline.GetPoint2dAt((i + 1) % n);
                pline.Segments.Add(
                    new Segment
                    {
                        Owner = pline,
                        Index = i,
                        A = a,
                        B = b,
                        Bulge = polyline.GetBulgeAt(i),
                        StartWidth = polyline.GetStartWidthAt(i),
                        EndWidth = polyline.GetEndWidthAt(i),
                    }
                );
            }

            return pline;
        }

        /// <summary>
        /// Two polylines of the same vertices, in the order given or reversed, are duplicates, whatever the setting for
        /// polylines: the later stays. Their widths are compared unless the setting ignores them.
        /// </summary>
        private static void ErasePolylineDuplicates(List<Pline> plines, Work w)
        {
            if (plines.Count < 2)
            {
                return;
            }

            double bin = Math.Max(w.Half * 2.0, 1E-9);
            var cells = new Dictionary<Tuple<long, long, long>, List<Pline>>();

            for (int i = plines.Count - 1; i >= 0; i--)
            {
                Pline p = plines[i];
                GeoPoint2 mid = Middle(p);
                long a = (long)Math.Round(mid.X / bin);
                long b = (long)Math.Round(mid.Y / bin);
                string key = Key(w.Settings, p.Entity);
                bool same = false;

                for (int da = -1; da <= 1 && !same; da++)
                {
                    for (int db = -1; db <= 1 && !same; db++)
                    {
                        if (
                            cells.TryGetValue(
                                Tuple.Create(a + da, b + db, (long)p.Segments.Count),
                                out List<Pline> near
                            )
                        )
                        {
                            same = near.Any(q =>
                                q.Closed == p.Closed && Key(w.Settings, q.Entity) == key && SamePolyline(p, q, w)
                            );
                        }
                    }
                }

                if (same)
                {
                    w.Duplicates++;
                    w.Erase.Add(p.Entity);
                    p.Entity = null;
                    p.Segments.Clear();
                    continue;
                }

                var own = Tuple.Create(a, b, (long)p.Segments.Count);

                if (!cells.TryGetValue(own, out List<Pline> list))
                {
                    list = new List<Pline>();
                    cells.Add(own, list);
                }

                list.Add(p);
            }
        }

        private static GeoPoint2 Middle(Pline p)
        {
            double x0 = double.MaxValue;
            double y0 = double.MaxValue;
            double x1 = double.MinValue;
            double y1 = double.MinValue;

            foreach (Segment s in p.Segments)
            {
                x0 = Math.Min(x0, Math.Min(s.A.X, s.B.X));
                y0 = Math.Min(y0, Math.Min(s.A.Y, s.B.Y));
                x1 = Math.Max(x1, Math.Max(s.A.X, s.B.X));
                y1 = Math.Max(y1, Math.Max(s.A.Y, s.B.Y));
            }

            return new GeoPoint2((x0 + x1) / 2.0, (y0 + y1) / 2.0);
        }

        private static bool SamePolyline(Pline p, Pline q, Work w)
        {
            int n = p.Segments.Count;

            if (n != q.Segments.Count || !(Math.Abs(p.Elevation - q.Elevation) <= w.Half))
            {
                return false;
            }

            bool Forward()
            {
                for (int i = 0; i < n; i++)
                {
                    if (!SameSegment(p.Segments[i], q.Segments[i], false, w))
                    {
                        return false;
                    }
                }

                return true;
            }

            bool Backward()
            {
                for (int i = 0; i < n; i++)
                {
                    if (!SameSegment(p.Segments[i], q.Segments[n - 1 - i], true, w))
                    {
                        return false;
                    }
                }

                return true;
            }

            return Forward() || (!p.Closed && Backward());
        }

        private static bool SameSegment(Segment a, Segment b, bool turned, Work w)
        {
            GeoPoint2 from = turned ? b.B : b.A;
            GeoPoint2 to = turned ? b.A : b.B;
            double bulge = turned ? -b.Bulge : b.Bulge;
            double startWidth = turned ? b.EndWidth : b.StartWidth;
            double endWidth = turned ? b.StartWidth : b.EndWidth;

            return Within(a.A.DistanceTo(from), w.Half)
                && Within(a.B.DistanceTo(to), w.Half)
                && Math.Abs(a.Bulge - bulge) <= 1E-9
                && (
                    w.Settings.IgnorePolylineWidths
                    || (Math.Abs(a.StartWidth - startWidth) <= w.Half && Math.Abs(a.EndWidth - endWidth) <= w.Half)
                );
        }

        /// <summary>
        /// Writes a polyline's runs of kept segments back: the first run into the polyline itself and each other as a copy of
        /// it, or the polyline erased where none is left. A closed polyline with no segment cut out stays closed.
        /// </summary>
        private static void Rebuild(Pline pline, Work w)
        {
            List<Segment> segments = pline.Segments;
            int n = segments.Count;
            bool cut = segments.Any(s => s.Removed);
            var order = new List<int>();

            if (pline.Closed && cut)
            {
                int first = segments.FindIndex(s => s.Removed);

                for (int k = 1; k <= n; k++)
                {
                    order.Add((first + k) % n);
                }
            }
            else
            {
                for (int k = 0; k < n; k++)
                {
                    order.Add(k);
                }
            }

            var runs = new List<List<Segment>>();
            var run = new List<Segment>();

            foreach (int index in order)
            {
                Segment segment = segments[index];

                if (segment.Removed)
                {
                    if (run.Count > 0)
                    {
                        runs.Add(run);
                        run = new List<Segment>();
                    }
                }
                else if (!segment.Dropped)
                {
                    run.Add(segment);
                }
            }

            if (run.Count > 0)
            {
                runs.Add(run);
            }

            DxfPolyline polyline = pline.Entity;
            bool closed = pline.Closed && !cut && runs.Count == 1 && runs[0].Count > 1;

            if (runs.Count == 0)
            {
                w.Erase.Add(polyline);
                return;
            }

            w.Reshape.Add(polyline);
            w.Edits.Add(s =>
            {
                DxfPolyline[] copies = new DxfPolyline[runs.Count];

                for (int r = 1; r < runs.Count; r++)
                {
                    copies[r] = (DxfPolyline)polyline.Clone();
                }

                Fill(polyline, runs[0], closed);

                for (int r = 1; r < runs.Count; r++)
                {
                    Fill(copies[r], runs[r], false);
                    s.Append(s.Document.FindSpace(polyline.Handle), copies[r]);
                }
            });
        }

        private static void Fill(DxfPolyline polyline, List<Segment> run, bool closed)
        {
            // A polyline keeps two vertices at least, so the vertices are rewritten in place and the extra ones trimmed.
            int wanted = run.Count + (closed ? 0 : 1);

            while (polyline.NumberOfVertices < wanted)
            {
                polyline.AddVertexAt(polyline.NumberOfVertices, run[0].A, 0.0, 0.0, 0.0);
            }

            for (int i = 0; i < wanted; i++)
            {
                bool end = i == run.Count;
                GeoPoint2 at = end ? run[run.Count - 1].B : run[i].A;
                polyline.SetPointAt(i, at);
                polyline.SetBulgeAt(i, end ? 0.0 : run[i].Bulge);
                polyline.SetStartWidthAt(i, end ? 0.0 : run[i].StartWidth);
                polyline.SetEndWidthAt(i, end ? 0.0 : run[i].EndWidth);
            }

            while (polyline.NumberOfVertices > wanted)
            {
                polyline.RemoveVertexAt(polyline.NumberOfVertices - 1);
            }

            polyline.Closed = closed;
        }
    }
}
