using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// TRIM and EXTEND of a multiline, as AutoCAD 2021 does them (read from its accoreconsole): an edge counts only where it
    /// crosses every element of a segment; the multiline is cut there, its new end on its line where the element that runs
    /// furthest still reaches the edge, each element stopping where it meets the edge; a piece taken from between two edges
    /// leaves two multilines, and a closed one opens.
    /// </summary>
    internal static partial class CadCommands
    {
        /// <summary>Where every element of a segment of a multiline meets an edge: along the segment's line, from its first vertex.</summary>
        private sealed class MlineCut
        {
            internal MlineCut(int segment, double[] hits, double at)
            {
                Segment = segment;
                Hits = hits;
                At = at;
            }

            internal int Segment { get; }

            /// <summary>For each element, how far along the segment's line from its first vertex the element meets the edge.</summary>
            internal double[] Hits { get; }

            /// <summary>Where the cut stands along the whole multiline, for ordering it against the pick.</summary>
            internal double At { get; }
        }

        /// <summary>
        /// Trims or extends a multiline picked at a point, as <see cref="CutOne"/> does a curve; given a list to preview into, it
        /// adds the lines of what a trim takes out or of the multiline run on, and changes nothing.
        /// </summary>
        private static bool CutMline(
            CadCommandContext c,
            string handle,
            GeoPoint2 at,
            bool extend,
            IReadOnlyList<string> edges,
            List<DxfEntity> preview
        )
        {
            var mline = (DxfUnknownEntity)c.Document.FindEntity(handle);
            var builder = new DxfMlineBuilder(mline);
            bool extendEdges = c.Remembered(EdgeKey, 0.0) != 0.0;
            bool quick = c.Remembered(ModeKey, 1.0) != 0.0 && edges == null;
            double eps = Slack(c);
            GeoPoint3 pick = new GeoPoint3(at.X, at.Y, builder.Points[0].Z);
            double[] starts = SegmentStarts(builder);
            CadMlineEdit.Nearest(builder, pick, out int pickSegment, out int _);
            double pickAt =
                starts[pickSegment]
                + builder.Points[pickSegment].GetVectorTo(pick).DotProduct(builder.SegmentDirection(pickSegment));
            // An end runs on as far as the drawing goes; a trim looks only about the multiline.
            DisplayBox box = extend
                ? BoxOf(c, new DxfRay { BasePoint = builder.Points[0], UnitDir = builder.SegmentDirection(0) })
                : MlineBox(c, builder, mline);
            List<DxfEntity> near = Near(c, box, handle, edges);

            if (extend)
            {
                // AutoCAD 2021 runs on neither a closed multiline nor one no edge stops every element of.
                if (builder.IsClosed)
                {
                    return false;
                }

                double total = starts[starts.Length - 1];
                bool atStart = pickAt < total / 2.0;
                MlineCut reach = MlineReach(builder, near, extendEdges, atStart, eps);

                if (reach == null)
                {
                    c.Write("Object does not intersect an edge.");
                    return false;
                }

                DxfMlineBuilder run = MlinePiece(mline, builder, atStart ? reach : null, atStart ? null : reach, true);

                if (run == null)
                {
                    return false;
                }

                if (preview != null)
                {
                    preview.AddRange(run.Explode(run.Build()));
                    return true;
                }

                return MlineReplace(c, handle, new List<DxfMlineBuilder> { run }, "Extend");
            }

            List<MlineCut> cuts = MlineCuts(builder, near, extendEdges, starts, eps);
            MlineCut low = null;
            MlineCut high = null;

            foreach (MlineCut cut in cuts)
            {
                if (cut.At < pickAt - eps && (low == null || cut.At > low.At))
                {
                    low = cut;
                }

                if (cut.At > pickAt + eps && (high == null || cut.At < high.At))
                {
                    high = cut;
                }
            }

            if (builder.IsClosed && cuts.Count >= 2)
            {
                // Round a closed multiline, the cuts on either side of the pick going round past its first vertex.
                low = low ?? cuts.OrderByDescending(x => x.At).First();
                high = high ?? cuts.OrderBy(x => x.At).First();
            }

            if (builder.IsClosed && (low == null || high == null || low == high))
            {
                low = null;
                high = null;
            }

            if (low == null && high == null)
            {
                if (quick)
                {
                    if (preview != null)
                    {
                        preview.AddRange(builder.Explode(builder.Build()));
                        return true;
                    }

                    // AutoCAD 2021's Quick mode erases an object that no other object cuts.
                    EraseAll(c, new[] { handle });
                    return true;
                }

                c.Write("Object does not intersect an edge.");
                return false;
            }

            var kept = new List<DxfMlineBuilder>();

            if (builder.IsClosed)
            {
                kept.Add(MlinePiece(mline, builder, high, low, false));
            }
            else
            {
                if (low != null)
                {
                    kept.Add(MlinePiece(mline, builder, null, low, false));
                }

                if (high != null)
                {
                    kept.Add(MlinePiece(mline, builder, high, null, false));
                }
            }

            if (kept.Contains(null))
            {
                return false;
            }

            if (preview != null)
            {
                DxfMlineBuilder gone = MlinePiece(mline, builder, low, high, false);

                if (gone != null)
                {
                    preview.AddRange(gone.Explode(gone.Build()));
                }

                return true;
            }

            return MlineReplace(c, handle, kept, "Trim");
        }

        /// <summary>Where each segment of a multiline starts along its whole line, and last its whole length.</summary>
        private static double[] SegmentStarts(DxfMlineBuilder builder)
        {
            int segments = builder.SegmentCount;
            var starts = new double[segments + 1];

            for (int s = 0; s < segments; s++)
            {
                starts[s + 1] =
                    starts[s] + builder.Points[s].DistanceTo(builder.Points[(s + 1) % builder.Points.Count]);
            }

            return starts;
        }

        /// <summary>The box of a multiline's elements, a little larger, for the edges near it.</summary>
        private static DisplayBox MlineBox(CadCommandContext c, DxfMlineBuilder builder, DxfUnknownEntity mline)
        {
            var box = DisplayBox.Empty;

            foreach (GeoPoint3 point in builder.Points)
            {
                box = box.Add(point.X, point.Y);
            }

            foreach (DxfEntity part in mline.Explode())
            {
                GeoAabb3 extents = part.GeometricExtents;

                if (!extents.IsEmpty)
                {
                    box = box.Add(extents.Min.X, extents.Min.Y).Add(extents.Max.X, extents.Max.Y);
                }
            }

            return box.Inflate(Slack(c));
        }

        /// <summary>
        /// The cuts the edges make in a multiline: on each segment, for each edge, where it crosses every element as many times;
        /// an edge that crosses some elements and not the others cuts nothing there, as AutoCAD 2021 leaves it.
        /// </summary>
        private static List<MlineCut> MlineCuts(
            DxfMlineBuilder builder,
            List<DxfEntity> edges,
            bool extendEdges,
            double[] starts,
            double eps
        )
        {
            var cuts = new List<MlineCut>();
            int n = builder.Points.Count;
            int count = builder.ElementCount;

            for (int s = 0; s < builder.SegmentCount; s++)
            {
                GeoPoint3 vertex = builder.Points[s];
                GeoVector3 way = builder.SegmentDirection(s);

                foreach (DxfEntity edge in edges)
                {
                    var hits = new List<double>[count];

                    for (int e = 0; e < count; e++)
                    {
                        GeoPoint3 from = builder.ElementPoint(s, e);
                        GeoPoint3 to = builder.ElementPoint((s + 1) % n, e);
                        hits[e] = new List<double>();

                        if (from.DistanceTo(to) <= eps)
                        {
                            continue;
                        }

                        foreach (GeoPoint3 point in Meet(new DxfLine(from, to), edge, extendEdges))
                        {
                            double along = vertex.GetVectorTo(point).DotProduct(way);

                            if (!hits[e].Exists(h => Math.Abs(h - along) <= eps))
                            {
                                hits[e].Add(along);
                            }
                        }

                        hits[e].Sort();
                    }

                    int times = hits[0].Count;

                    if (times == 0 || Array.Exists(hits, h => h.Count != times))
                    {
                        continue;
                    }

                    for (int k = 0; k < times; k++)
                    {
                        var one = new double[count];

                        for (int e = 0; e < count; e++)
                        {
                            one[e] = hits[e][k];
                        }

                        cuts.Add(new MlineCut(s, one, starts[s] + one.Average()));
                    }
                }
            }

            return cuts;
        }

        /// <summary>
        /// The nearest edge past an end of a multiline that every element meets when run on along the end segment, as EXTEND
        /// reaches; null where none is.
        /// </summary>
        private static MlineCut MlineReach(
            DxfMlineBuilder builder,
            List<DxfEntity> edges,
            bool extendEdges,
            bool atStart,
            double eps
        )
        {
            int n = builder.Points.Count;
            int segment = atStart ? 0 : n - 2;
            int vertex = atStart ? 0 : n - 1;
            GeoPoint3 origin = builder.Points[segment];
            GeoVector3 way = builder.SegmentDirection(segment);
            GeoVector3 out_ = atStart ? way.Negate() : way;
            int count = builder.ElementCount;
            MlineCut best = null;
            double bestMove = double.PositiveInfinity;

            foreach (DxfEntity edge in edges)
            {
                var hits = new double[count];
                bool all = true;

                for (int e = 0; e < count && all; e++)
                {
                    GeoPoint3 end = builder.ElementPoint(vertex, e);
                    double first = double.PositiveInfinity;

                    foreach (GeoPoint3 point in Meet(new DxfRay { BasePoint = end, UnitDir = out_ }, edge, extendEdges))
                    {
                        double travel = end.GetVectorTo(point).DotProduct(out_);

                        if (travel > eps && travel < first)
                        {
                            first = travel;
                            hits[e] = origin.GetVectorTo(point).DotProduct(way);
                        }
                    }

                    all = !double.IsPositiveInfinity(first);
                }

                if (!all)
                {
                    continue;
                }

                // How far the end moves: to where the element that runs furthest still reaches the edge.
                double move = 0.0;

                for (int e = 0; e < count; e++)
                {
                    GeoPoint3 end = builder.ElementPoint(vertex, e);
                    double reach = hits[e] - origin.GetVectorTo(end).DotProduct(way);
                    move = Math.Max(move, atStart ? -reach : reach);
                }

                if (move < bestMove)
                {
                    bestMove = move;
                    best = new MlineCut(segment, hits, 0.0);
                }
            }

            return best;
        }

        /// <summary>
        /// The part of a multiline from one cut to another, as a builder of its record: from its start where no first cut is
        /// given, to its end where no last one is, round past the first vertex of a closed one. The new ends stand on the line
        /// where the element that runs furthest still reaches the cut, each element stopping where it meets it; the stretches
        /// of the segments kept where they were. Null where the part would have no length.
        /// </summary>
        private static DxfMlineBuilder MlinePiece(
            DxfUnknownEntity mline,
            DxfMlineBuilder old,
            MlineCut first,
            MlineCut last,
            bool extending
        )
        {
            int n = old.Points.Count;
            int segments = old.SegmentCount;
            int count = old.ElementCount;
            int from = first?.Segment ?? 0;
            int to = last?.Segment ?? segments - 1;
            bool round =
                old.IsClosed
                && first != null
                && last != null
                && from == to
                && first.Hits.Average() > last.Hits.Average();
            var sequence = new List<int>();

            for (int s = from; ; s = (s + 1) % segments)
            {
                sequence.Add(s);

                if (s == to && (sequence.Count > 1 || !round))
                {
                    break;
                }

                if (sequence.Count > segments + 1)
                {
                    return null;
                }
            }

            // The new points: a provisional start and end on their segments, set once the miters there are known.
            var piece = new DxfMlineBuilder(mline) { IsClosed = false };
            piece.Points.Clear();
            int lastIndex = sequence.Count;
            double[] lengths = sequence.Select(s => old.Points[s].DistanceTo(old.Points[(s + 1) % n])).ToArray();

            for (int j = 0; j <= sequence.Count; j++)
            {
                piece.Points.Add(old.Points[j < sequence.Count ? sequence[j] : (sequence[j - 1] + 1) % n]);
            }

            if (first != null)
            {
                piece.Points[0] = Along(old, from, lengths[0] / (sequence.Count == 1 && last != null ? 3.0 : 2.0));
            }

            if (last != null)
            {
                piece.Points[lastIndex] = Along(
                    old,
                    to,
                    lengths[sequence.Count - 1] * (sequence.Count == 1 && first != null ? 2.0 / 3.0 : 0.5)
                );
            }

            double startAt = 0.0;
            double endAt = 0.0;

            if (first != null)
            {
                startAt = double.PositiveInfinity;
                GeoVector3 way = old.SegmentDirection(from);

                for (int e = 0; e < count; e++)
                {
                    double offset = piece.Points[0].GetVectorTo(piece.ElementPoint(0, e)).DotProduct(way);
                    startAt = Math.Min(startAt, first.Hits[e] - offset);
                }
            }

            if (last != null)
            {
                endAt = double.NegativeInfinity;
                GeoVector3 way = old.SegmentDirection(to);

                for (int e = 0; e < count; e++)
                {
                    double offset = piece
                        .Points[lastIndex]
                        .GetVectorTo(piece.ElementPoint(lastIndex, e))
                        .DotProduct(way);
                    endAt = Math.Max(endAt, last.Hits[e] - offset);
                }
            }

            if (first != null)
            {
                piece.Points[0] = Along(old, from, startAt);
            }

            if (last != null)
            {
                piece.Points[lastIndex] = Along(old, to, endAt);
            }

            double least = Tolerance.Default.EqualPoint;

            for (int j = 0; j < sequence.Count; j++)
            {
                if (piece.Points[j].DistanceTo(piece.Points[j + 1]) <= least)
                {
                    return null;
                }
            }

            // The stretches of each segment, kept where they were along it, cut where the piece starts and ends.
            for (int j = 0; j < sequence.Count; j++)
            {
                int s = sequence[j];
                GeoPoint3 vertex = old.Points[s];
                GeoVector3 way = old.SegmentDirection(s);

                for (int e = 0; e < count; e++)
                {
                    double oldStart = vertex.GetVectorTo(old.ElementPoint(s, e)).DotProduct(way);
                    double newStart = vertex.GetVectorTo(piece.ElementPoint(j, e)).DotProduct(way);
                    double shift = newStart - oldStart;
                    bool cutStart = j == 0 && first != null;
                    var runs = new List<DxfMlineBuilder.Run>();

                    foreach (DxfMlineBuilder.Run run in old.RunsOf(s, e))
                    {
                        // A stretch from the element's start runs back to a start moved back, as an extension leaves it.
                        double a = cutStart && run.From <= least ? double.NegativeInfinity : run.From - shift;
                        runs.Add(new DxfMlineBuilder.Run(a, run.To - shift));
                    }

                    if (cutStart)
                    {
                        runs = Clip(runs, first.Hits[e] - newStart, double.PositiveInfinity);
                    }

                    if (j == sequence.Count - 1 && last != null)
                    {
                        double stop = last.Hits[e] - newStart;
                        double length = piece
                            .ElementPoint(j, e)
                            .GetVectorTo(piece.ElementPoint(j + 1, e))
                            .DotProduct(way);

                        // An extension stops only the elements that meet the edge short of the new end, as AutoCAD 2021 writes it.
                        if (!extending || stop < length - least)
                        {
                            runs = Clip(runs, double.NegativeInfinity, stop);
                        }
                    }

                    piece.SetRuns(j, e, runs);
                }
            }

            for (int e = 0; e < count; e++)
            {
                piece.SetEndExtension(e, last == null && !old.IsClosed ? old.EndExtension(e) : 0.0);
            }

            return piece;
        }

        /// <summary>A point on the line of a segment of a multiline, at a distance from its first vertex.</summary>
        private static GeoPoint3 Along(DxfMlineBuilder mline, int segment, double distance) =>
            mline.Points[segment].Add(mline.SegmentDirection(segment).Multiply(distance));

        /// <summary>The parts of some stretches between two places.</summary>
        private static List<DxfMlineBuilder.Run> Clip(IEnumerable<DxfMlineBuilder.Run> runs, double from, double to)
        {
            var kept = new List<DxfMlineBuilder.Run>();

            foreach (DxfMlineBuilder.Run run in runs)
            {
                double a = Math.Max(run.From, from);
                double b = Math.Min(run.To, to);

                if (b > a)
                {
                    kept.Add(new DxfMlineBuilder.Run(a, b));
                }
            }

            return kept;
        }

        /// <summary>Writes the pieces of a multiline: the first into its own record, the others as copies of it added beside it; one edit.</summary>
        private static bool MlineReplace(CadCommandContext c, string handle, List<DxfMlineBuilder> pieces, string how)
        {
            CadSpace space = c.Document.FindSpace(handle);
            var planned = new[] { new CadEditRecord(CadEditRecordKind.Reshaped, handle, how) };

            return c.Apply(planned, s =>
            {
                var target = (DxfUnknownEntity)s.Document.FindEntity(handle);
                var copies = new List<DxfUnknownEntity>();

                for (int i = 1; i < pieces.Count; i++)
                {
                    copies.Add(s.Append(space, (DxfUnknownEntity)target.Clone()));
                }

                pieces[0].Rebuild(target);

                for (int i = 1; i < pieces.Count; i++)
                {
                    pieces[i].Rebuild(copies[i - 1]);
                }
            }).Succeeded;
        }
    }
}
