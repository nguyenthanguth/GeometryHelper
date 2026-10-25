using System;
using System.Collections.Generic;
using GeometryHelper.Geometry;
using static GeometryHelper.Dxf.DxfLeaderGroups;

namespace GeometryHelper.Dxf
{
    public sealed partial class DxfMLeader
    {
        // The groups of AcDbMLeader after its context data, in the order AutoCAD writes them: where one the record has not goes.
        private static readonly int[] TopOrder =
        {
            270, 340, 90, 170, 91, 341, 171, 290, 291, 41, 342, 42, 172, 343, 173, 95, 174, 175, 92, 292, 344, 93, 10, 20, 30, 43, 176, 293,
            94, 345, 330, 177, 44, 302, 294, 178, 179, 45, 271, 272, 273, 295,
        };

        // The groups of the context data before its leaders, in the order AutoCAD writes them, its MTEXT's after 290 and its block's after 296.
        private static readonly int[] ContextOrder =
        {
            40, 10, 20, 30, 41, 140, 145, 174, 175, 176, 177, 290, 304, 11, 21, 31, 340, 12, 22, 32, 13, 23, 33, 42, 43, 44, 45, 170, 90, 171,
            172, 91, 141, 92, 291, 292, 173, 293, 142, 143, 294, 144, 295, 296, 341, 14, 24, 34, 15, 25, 35, 16, 26, 36, 46, 93, 47, 110, 120,
            130, 111, 121, 131, 112, 122, 132, 297,
        };

        // The groups of a leader before its lines, in the order AutoCAD writes them.
        private static readonly int[] LeaderOrder = { 290, 291, 10, 20, 30, 11, 21, 31, 12, 22, 32, 13, 23, 33, 90, 40 };

        // The groups of the MTEXT of the context data and of its block, as ezdxf reads them.
        private static readonly HashSet<int> MTextCodes = new HashSet<int> { 304, 11, 21, 31, 340, 12, 22, 32, 13, 23, 33, 42, 43, 44, 45, 170, 90, 171, 172, 91, 141, 92, 291, 292, 173, 293, 142, 143, 294, 144, 295 };
        private static readonly HashSet<int> BlockCodes = new HashSet<int> { 341, 14, 24, 34, 15, 25, 35, 16, 26, 36, 46, 93, 47 };

        /// <summary>
        /// The own groups of the MULTILEADER: those before its context data, its context data with its leaders and their lines,
        /// and those after it; each block's opening and closing groups kept as the file writes them.
        /// </summary>
        private sealed class Rec
        {
            internal List<KeyValuePair<int, string>> Before { get; } = new List<KeyValuePair<int, string>>();

            internal Ctx Context { get; private set; }

            internal List<KeyValuePair<int, string>> After { get; } = new List<KeyValuePair<int, string>>();

            internal static Rec Read(List<KeyValuePair<int, string>> run)
            {
                var rec = new Rec();
                int i = 0;

                for (; i < run.Count; i++)
                {
                    if (run[i].Key == 300 && rec.Context == null)
                    {
                        rec.Context = Ctx.Read(run, ref i);
                        continue;
                    }

                    (rec.Context == null ? rec.Before : rec.After).Add(run[i]);
                }

                return rec;
            }

            internal List<KeyValuePair<int, string>> Groups()
            {
                var run = new List<KeyValuePair<int, string>>(Before);
                Context?.Write(run);
                run.AddRange(After);
                return run;
            }
        }

        /// <summary>The context data: its groups before the leaders, the leaders, those after them.</summary>
        private sealed class Ctx
        {
            internal string Open { get; private set; }

            internal string Close { get; private set; } = "}";

            internal List<KeyValuePair<int, string>> Head { get; } = new List<KeyValuePair<int, string>>();

            internal List<LeaderPart> Leaders { get; } = new List<LeaderPart>();

            internal List<KeyValuePair<int, string>> Tail { get; } = new List<KeyValuePair<int, string>>();

            internal static Ctx Read(List<KeyValuePair<int, string>> run, ref int i)
            {
                var ctx = new Ctx { Open = run[i].Value };

                for (i++; i < run.Count; i++)
                {
                    KeyValuePair<int, string> group = run[i];

                    if (group.Key == 301)
                    {
                        ctx.Close = group.Value;
                        return ctx;
                    }

                    if (group.Key == 302 && group.Value.Trim() == "LEADER{")
                    {
                        ctx.Leaders.Add(LeaderPart.Read(run, ref i));
                        continue;
                    }

                    (ctx.Leaders.Count == 0 ? ctx.Head : ctx.Tail).Add(group);
                }

                return ctx;
            }

            internal void Write(List<KeyValuePair<int, string>> run)
            {
                run.Add(G(300, Open));
                run.AddRange(Head);

                foreach (LeaderPart leader in Leaders)
                {
                    leader.Write(run);
                }

                run.AddRange(Tail);
                run.Add(G(301, Close));
            }
        }

        /// <summary>A leader of the context data: its groups before its lines, its lines, those after them.</summary>
        private sealed class LeaderPart
        {
            internal string Open { get; private set; } = "LEADER{";

            internal string Close { get; private set; } = "}";

            internal List<KeyValuePair<int, string>> Head { get; } = new List<KeyValuePair<int, string>>();

            internal List<LinePart> Lines { get; } = new List<LinePart>();

            internal List<KeyValuePair<int, string>> Tail { get; } = new List<KeyValuePair<int, string>>();

            /// <summary>The index of the leader (90).</summary>
            internal int Index => Whole(Text(Head, 90), 0);

            internal static LeaderPart Read(List<KeyValuePair<int, string>> run, ref int i)
            {
                var leader = new LeaderPart { Open = run[i].Value };

                for (i++; i < run.Count; i++)
                {
                    KeyValuePair<int, string> group = run[i];

                    if (group.Key == 303)
                    {
                        leader.Close = group.Value;
                        return leader;
                    }

                    if (group.Key == 304 && group.Value.Trim() == "LEADER_LINE{")
                    {
                        leader.Lines.Add(LinePart.Read(run, ref i));
                        continue;
                    }

                    (leader.Lines.Count == 0 ? leader.Head : leader.Tail).Add(group);
                }

                return leader;
            }

            internal static LeaderPart Made(List<KeyValuePair<int, string>> head, List<KeyValuePair<int, string>> tail)
            {
                var leader = new LeaderPart();
                leader.Head.AddRange(head);
                leader.Tail.AddRange(tail);
                return leader;
            }

            internal void Write(List<KeyValuePair<int, string>> run)
            {
                run.Add(G(302, Open));
                run.AddRange(Head);

                foreach (LinePart line in Lines)
                {
                    run.Add(G(304, line.Open));
                    run.AddRange(line.Groups);
                    run.Add(G(305, line.Close));
                }

                run.AddRange(Tail);
                run.Add(G(303, Close));
            }
        }

        /// <summary>A leader line: its groups between its braces.</summary>
        private sealed class LinePart
        {
            internal string Open { get; private set; } = "LEADER_LINE{";

            internal string Close { get; private set; } = "}";

            internal List<KeyValuePair<int, string>> Groups { get; } = new List<KeyValuePair<int, string>>();

            /// <summary>The index of the line (91).</summary>
            internal int Index => Whole(Text(Groups, 91), 0);

            internal static LinePart Read(List<KeyValuePair<int, string>> run, ref int i)
            {
                var line = new LinePart { Open = run[i].Value };

                for (i++; i < run.Count; i++)
                {
                    if (run[i].Key == 305)
                    {
                        line.Close = run[i].Value;
                        return line;
                    }

                    line.Groups.Add(run[i]);
                }

                return line;
            }

            internal static LinePart Made(IEnumerable<KeyValuePair<int, string>> groups)
            {
                var line = new LinePart();
                line.Groups.AddRange(groups);
                return line;
            }

            /// <summary>The vertices of the line (10, 20, 30), the points of its breaks (11, 12) aside.</summary>
            internal List<GeoPoint3> Vertices()
            {
                var vertices = new List<GeoPoint3>();

                for (int i = 0; i < Groups.Count; i++)
                {
                    if (Groups[i].Key == 10)
                    {
                        vertices.Add(PointAt(Groups, i));
                    }
                }

                return vertices;
            }

            /// <summary>Writes the vertices of the line in place of those it has, where the first was, first where it had none; a vertex as it was, to the bit, keeping its text; its other groups kept.</summary>
            internal void SetVertices(List<GeoPoint3> vertices)
            {
                var old = new List<List<KeyValuePair<int, string>>>();
                var others = new List<KeyValuePair<int, string>>();
                int first = -1;

                for (int i = 0; i < Groups.Count; i++)
                {
                    if (Groups[i].Key != 10)
                    {
                        others.Add(Groups[i]);
                        continue;
                    }

                    first = first < 0 ? others.Count : first;
                    var vertex = new List<KeyValuePair<int, string>> { Groups[i] };

                    while (i + 1 < Groups.Count && (Groups[i + 1].Key == 20 || Groups[i + 1].Key == 30))
                    {
                        vertex.Add(Groups[++i]);
                    }

                    old.Add(vertex);
                }

                var run = new List<KeyValuePair<int, string>>();

                for (int i = 0; i < vertices.Count; i++)
                {
                    if (i < old.Count && SamePoint(PointAt(old[i], 0), vertices[i]))
                    {
                        run.AddRange(old[i]);
                    }
                    else
                    {
                        run.AddRange(PointGroups(10, vertices[i]));
                    }
                }

                others.InsertRange(first < 0 ? 0 : first, run);
                Groups.Clear();
                Groups.AddRange(others);
            }
        }

        /// <summary>The point of the groups of a code and the two above it from an index on: a coordinate left out is nought.</summary>
        private static GeoPoint3 PointAt(List<KeyValuePair<int, string>> groups, int at)
        {
            int code = groups[at].Key;
            double x = Num(groups[at].Value, 0.0);
            double y = 0.0;
            double z = 0.0;

            for (int i = at + 1; i < groups.Count && i <= at + 2; i++)
            {
                if (groups[i].Key == code + 10)
                {
                    y = Num(groups[i].Value, 0.0);
                }
                else if (groups[i].Key == code + 20)
                {
                    z = Num(groups[i].Value, 0.0);
                }
                else
                {
                    break;
                }
            }

            return new GeoPoint3(x, y, z);
        }

        /// <summary>The point of the first group of a code in a run and the two above it; the fallback where there is none.</summary>
        private static GeoPoint3 PointOf(List<KeyValuePair<int, string>> groups, int code, GeoPoint3 fallback)
        {
            int at = groups.FindIndex(g => g.Key == code);
            return at < 0 ? fallback : PointAt(groups, at);
        }

        private static KeyValuePair<int, string>[] PointGroups(int code, GeoPoint3 p) => new[] { G(code, p.X), G(code + 10, p.Y), G(code + 20, p.Z) };

        private static bool SamePoint(GeoPoint3 a, GeoPoint3 b) => Same(a.X, b.X) && Same(a.Y, b.Y) && Same(a.Z, b.Z);

        /// <summary>
        /// Sets the first group of a code in a run, or puts one after the last of those the order writes before it; null takes
        /// every one out. False where it is as it was.
        /// </summary>
        private static bool Put(List<KeyValuePair<int, string>> groups, int code, string value, int[] order, int from = 0, int to = -1)
        {
            to = to < 0 ? groups.Count : to;
            int at = -1;

            for (int i = from; i < to; i++)
            {
                if (groups[i].Key == code)
                {
                    at = i;
                    break;
                }
            }

            if (value == null)
            {
                if (at < 0)
                {
                    return false;
                }

                groups.RemoveAt(at);
                return true;
            }

            if (at >= 0)
            {
                if (string.Equals(groups[at].Value, value, StringComparison.Ordinal))
                {
                    return false;
                }

                groups[at] = G(code, value);
                return true;
            }

            int rank = Array.IndexOf(order, code);
            int put = from;

            for (int i = from; i < to; i++)
            {
                int other = Array.IndexOf(order, groups[i].Key);

                if (other >= 0 && other <= rank)
                {
                    put = i + 1;
                }
            }

            groups.Insert(put, G(code, value));
            return true;
        }

        /// <summary>Sets a number in a run where it differs from the one read, to the bit.</summary>
        private static void PutNumber(List<KeyValuePair<int, string>> groups, int code, double value, double fallback, int[] order, int from = 0, int to = -1)
        {
            to = to < 0 ? groups.Count : to;
            int at = groups.FindIndex(from, to - from, g => g.Key == code);

            if (at >= 0 && Same(Num(groups[at].Value, fallback), value))
            {
                return;
            }

            Put(groups, code, Real(value), order, from, to);
        }

        /// <summary>Sets a point in a run, each coordinate that differs written anew, those left out with it.</summary>
        private static void PutPoint(List<KeyValuePair<int, string>> groups, int code, GeoPoint3 value, int[] order, int from = 0, int to = -1)
        {
            to = to < 0 ? groups.Count : to;
            int at = groups.FindIndex(from, to - from, g => g.Key == code);
            GeoPoint3 now = at < 0 ? new GeoPoint3(double.NaN, double.NaN, double.NaN) : PointAt(groups, at);

            if (at >= 0 && SamePoint(now, value))
            {
                return;
            }

            if (at < 0)
            {
                Put(groups, code, Real(value.X), order, from, to);
                at = groups.FindIndex(from, g => g.Key == code);
                groups.Insert(at + 1, G(code + 20, value.Z));
                groups.Insert(at + 1, G(code + 10, value.Y));
                return;
            }

            double[] values = { value.X, value.Y, value.Z };
            double[] was = { now.X, now.Y, now.Z };
            int next = at;

            for (int k = 0; k < 3; k++)
            {
                int c = code + 10 * k;

                if (next < groups.Count && groups[next].Key == c)
                {
                    if (!Same(values[k], was[k]))
                    {
                        groups[next] = G(c, values[k]);
                    }

                    next++;
                }
                else
                {
                    groups.Insert(next, G(c, values[k]));
                    next++;
                }
            }
        }

        /// <summary>Where a section of the context data runs, after its flag of 1 (290 for the MTEXT, 296 for the block): its first group and the one after its last; (-1, -1) where it has none.</summary>
        private static void Section(List<KeyValuePair<int, string>> head, int flag, HashSet<int> codes, out int from, out int to)
        {
            int at = head.FindIndex(g => g.Key == flag);
            from = -1;
            to = -1;

            if (at < 0 || Whole(head[at].Value, 0) != 1)
            {
                return;
            }

            from = at + 1;
            to = from;

            while (to < head.Count && codes.Contains(head[to].Key))
            {
                to++;
            }
        }
    }
}
