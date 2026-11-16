using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// DIMBREAK on a multileader, as AutoCAD 2021 breaks one (probed in its console, some 150 breaks of leader lines of one
    /// and of two segments, of one leader line and of two): the break is kept in the leader line (90, 11 and 12 of its
    /// LEADER_LINE, see <see cref="DxfLeaderBreak"/>) and, as for a dimension, a BREAKDATA under ACAD_BREAKDATA in the
    /// multileader's extension dictionary names what it was made by.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Auto breaks each leader line where an object of the space crosses it, a gap of the break size of the multileader style
    /// (<see cref="DxfMLeaderStyle.BreakSize"/>) times the scale of the multileader wide, half either side of the crossing: a
    /// line from (0, 0) to (100, -1.25) crossed by a line at x 40, at the style's 0.125, is broken from (39.9375, -0.499219)
    /// to (40.0625, -0.500781), and from 39.875 to 40.125 at scale 2. The line is drawn from the back of its arrow, which
    /// no break reaches into, and the dogleg is not broken, though an object that crosses only the dogleg or the arrow is
    /// named in the BREAKDATA all the same. Manual breaks the leader line nearest the first point (91 its index) and, in it,
    /// the segment nearest the first point: from the foot of the first point on that segment to the foot of the second,
    /// which AutoCAD holds at the end of the line where it falls beyond (from (30, 0) to (130, 0) on a line to (100, -1.25)
    /// leaves nothing drawn from 30 on); where it falls beyond the end of a segment at a vertex between two, or neither point
    /// falls on the drawn segment, nothing is broken; breaks that meet are one. Remove takes them out.
    /// </para>
    /// </remarks>
    internal static partial class CadCommands
    {
        /// <summary>One leader line of a multileader as it is drawn: its index (91), its vertices from its arrow to its landing, and how far the arrow takes the first segment.</summary>
        private sealed class LeaderStretch
        {
            internal int LineIndex { get; set; }

            internal List<GeoPoint3> Vertices { get; set; }

            internal double Trim { get; set; }

            /// <summary>Gets where the drawn part of a segment starts, from its first vertex: the back of the arrow in the first, the vertex itself in the others.</summary>
            internal double DrawnFrom(int segment) => segment == 0 ? Trim : 0.0;
        }

        /// <summary>The leader lines of a multileader that are drawn, each with the vertices it runs through and the length its arrow takes.</summary>
        private static List<LeaderStretch> LeaderStretches(DxfMLeader mleader)
        {
            var found = new List<LeaderStretch>();
            double scale = mleader.Scale;

            foreach (int leader in mleader.GetLeaderIndexes())
            {
                foreach (int line in mleader.GetLeaderLineIndexes(leader))
                {
                    int count = mleader.VerticesCount(line);

                    if (count < 2)
                    {
                        continue;
                    }

                    var vertices = new List<GeoPoint3>();

                    for (int k = 0; k < count; k++)
                    {
                        vertices.Add(mleader.GetVertex(line, k));
                    }

                    // The arrow is drawn where the first segment is more than twice as long as it, and the line starts at its back.
                    double size = mleader.GetArrowSize(line) * scale;
                    double run = vertices[0].DistanceTo(vertices[1]);
                    found.Add(
                        new LeaderStretch
                        {
                            LineIndex = line,
                            Vertices = vertices,
                            Trim = run * size > 0.0 && run > 2.0 * size ? size : 0.0,
                        }
                    );
                }
            }

            return found;
        }

        /// <summary>The break size of a multileader: its style's, else AutoCAD 2021's default of the drawing, times its scale.</summary>
        private static double MLeaderBreakSize(DxfMLeader mleader)
        {
            DxfDatabase database = mleader.Item?.File.Database;
            DxfMLeaderStyle style = null;

            try
            {
                style = database == null ? null : CadMLeaderStyleEditor.Find(database, mleader.MLeaderStyle);
            }
            catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException)
            {
                // A multileader that names no style takes the default.
            }

            return (style != null ? style.BreakSize : DefaultBreakSize(database)) * mleader.Scale;
        }

        /// <summary>The gaps of one segment as distances from its first vertex, the breaks it has (converted) and those given, those that meet one.</summary>
        private static List<KeyValuePair<double, double>> Merged(
            List<DxfLeaderBreak> breaks,
            int segment,
            GeoPoint3 a,
            GeoVector3 way,
            IEnumerable<KeyValuePair<double, double>> more
        )
        {
            var gaps = new List<KeyValuePair<double, double>>(more);

            foreach (DxfLeaderBreak item in breaks.Where(b => b.Segment == segment))
            {
                double from = a.GetVectorTo(item.Start).DotProduct(way);
                double to = a.GetVectorTo(item.End).DotProduct(way);
                gaps.Add(new KeyValuePair<double, double>(Math.Min(from, to), Math.Max(from, to)));
            }

            gaps.Sort((x, y) => x.Key != y.Key ? x.Key.CompareTo(y.Key) : x.Value.CompareTo(y.Value));
            var merged = new List<KeyValuePair<double, double>>();

            foreach (KeyValuePair<double, double> gap in gaps)
            {
                if (merged.Count > 0 && gap.Key <= merged[merged.Count - 1].Value + 1E-9)
                {
                    merged[merged.Count - 1] = new KeyValuePair<double, double>(
                        merged[merged.Count - 1].Key,
                        Math.Max(merged[merged.Count - 1].Value, gap.Value)
                    );
                }
                else
                {
                    merged.Add(gap);
                }
            }

            return merged;
        }

        /// <summary>Puts the breaks a segment gets in place of those it had in a leader line's.</summary>
        private static void SetSegmentBreaks(
            DxfMLeader mleader,
            LeaderStretch stretch,
            int segment,
            List<KeyValuePair<double, double>> gaps
        )
        {
            GeoPoint3 a = stretch.Vertices[segment];
            GeoVector3 way = a.GetVectorTo(stretch.Vertices[segment + 1]);
            way = way.Multiply(1.0 / way.Length);
            List<DxfLeaderBreak> breaks = mleader
                .GetBreaks(stretch.LineIndex)
                .Where(b => b.Segment != segment)
                .ToList();

            foreach (KeyValuePair<double, double> gap in gaps)
            {
                breaks.Add(new DxfLeaderBreak(segment, a.Add(way.Multiply(gap.Key)), a.Add(way.Multiply(gap.Value))));
            }

            mleader.SetBreaks(stretch.LineIndex, breaks);
        }

        /// <summary>
        /// Breaks the leader lines of a multileader where the objects given cross them (every cutter of the space where none are
        /// picked): a gap of the break size about each place, none in the arrow or the dogleg. Gives the objects that cross a
        /// leader line or a dogleg, in the order given.
        /// </summary>
        private static List<DxfEntity> BreakLeadersAt(DxfMLeader mleader, List<DxfEntity> cutters)
        {
            var crossed = new List<DxfEntity>();
            List<KeyValuePair<DxfEntity, DxfEntity>> shapes = CutterShapes(cutters);
            double size = MLeaderBreakSize(mleader);

            foreach (LeaderStretch stretch in LeaderStretches(mleader))
            {
                for (int k = 0; k + 1 < stretch.Vertices.Count; k++)
                {
                    GeoPoint3 first = stretch.Vertices[k];
                    GeoPoint3 second = stretch.Vertices[k + 1];
                    double length = first.DistanceTo(second);

                    if (length <= 1E-9)
                    {
                        continue;
                    }

                    GeoPoint2 a = Flat(first);
                    GeoPoint2 b = Flat(second);
                    var gaps = new List<KeyValuePair<double, double>>();

                    foreach (KeyValuePair<DxfEntity, DxfEntity> shape in shapes)
                    {
                        List<double> at;

                        if (shape.Value is DxfDBText || shape.Value is DxfMText)
                        {
                            at = TextSpan(a, b, shape.Value, out double enter, out double leave)
                                ? new List<double> { enter, leave }
                                : new List<double>();

                            if (at.Count > 0)
                            {
                                Note(crossed, shape.Key);
                                gaps.Add(new KeyValuePair<double, double>(enter - size / 2.0, leave + size / 2.0));
                            }

                            continue;
                        }

                        at = Crossings(a, b, new List<DxfEntity> { shape.Value });

                        if (at.Count > 0)
                        {
                            Note(crossed, shape.Key);
                        }

                        foreach (double t in at)
                        {
                            gaps.Add(new KeyValuePair<double, double>(t - size / 2.0, t + size / 2.0));
                        }
                    }

                    // Only the drawn part of the segment is broken: not the arrow, nor beyond the vertices.
                    double from = stretch.DrawnFrom(k);
                    var inside = new List<KeyValuePair<double, double>>();

                    foreach (KeyValuePair<double, double> gap in gaps)
                    {
                        double lo = Math.Max(gap.Key, from);
                        double hi = Math.Min(gap.Value, length);

                        if (hi - lo > 1E-9)
                        {
                            inside.Add(new KeyValuePair<double, double>(lo, hi));
                        }
                    }

                    if (inside.Count > 0)
                    {
                        GeoVector3 way = first.GetVectorTo(second).Multiply(1.0 / length);
                        List<KeyValuePair<double, double>> merged = Merged(
                            mleader.GetBreaks(stretch.LineIndex).ToList(),
                            k,
                            first,
                            way,
                            inside
                        );
                        SetSegmentBreaks(mleader, stretch, k, merged);
                    }
                }
            }

            // An object that crosses only the dogleg is named all the same, as AutoCAD names it.
            foreach (int leader in mleader.GetLeaderIndexes())
            {
                IReadOnlyList<int> lines = mleader.GetLeaderLineIndexes(leader);

                if (!mleader.EnableDogleg || lines.Count == 0)
                {
                    continue;
                }

                GeoPoint3 landing = mleader.GetLastVertex(lines[0]);
                GeoVector3 along = mleader.GetDogleg(leader);
                double reach = mleader.GetDoglegLength(leader) * mleader.Scale;

                if (along.Length <= 0.0 || reach <= 0.0)
                {
                    continue;
                }

                GeoVector3 end = along.Multiply(reach / along.Length);

                foreach (KeyValuePair<DxfEntity, DxfEntity> shape in shapes)
                {
                    GeoPoint2 a = Flat(landing);
                    GeoPoint2 b = new GeoPoint2(landing.X + end.X, landing.Y + end.Y);

                    if (
                        Crossings(a, b, new List<DxfEntity> { shape.Value }).Count > 0
                        || (
                            (shape.Value is DxfDBText || shape.Value is DxfMText)
                            && TextSpan(a, b, shape.Value, out _, out _)
                        )
                    )
                    {
                        Note(crossed, shape.Key);
                    }
                }
            }

            crossed.Sort((x, y) => cutters.IndexOf(x).CompareTo(cutters.IndexOf(y)));
            return crossed;
        }

        private static void Note(List<DxfEntity> crossed, DxfEntity owner)
        {
            if (!crossed.Contains(owner))
            {
                crossed.Add(owner);
            }
        }

        /// <summary>
        /// Breaks a multileader between two points as DIMBREAK Manual does, and gives the index of the leader line broken (91):
        /// the leader line nearest the first point, in it the segment nearest it, broken from the foot of the first point on that
        /// segment to that of the second, each held at the ends of the leader line where it falls beyond; no more than that
        /// where one of the two falls on the drawn segment and the other does not pass a vertex between two segments.
        /// </summary>
        private static int ManualLeaderBreak(DxfMLeader mleader, GeoPoint2 first, GeoPoint2 second)
        {
            List<LeaderStretch> stretches = LeaderStretches(mleader);

            if (stretches.Count == 0)
            {
                return 0;
            }

            LeaderStretch chosen = null;
            int segment = 0;
            double best = double.MaxValue;

            foreach (LeaderStretch stretch in stretches)
            {
                for (int k = 0; k + 1 < stretch.Vertices.Count; k++)
                {
                    GeoPoint2 a = Flat(stretch.Vertices[k]);
                    GeoPoint2 b = Flat(stretch.Vertices[k + 1]);
                    double length = (b - a).Length;

                    if (length <= 1E-9)
                    {
                        continue;
                    }

                    GeoPoint2 drawn = a + (b - a) / length * stretch.DrawnFrom(k);
                    double distance = SegmentDistance(drawn, b, first);

                    if (distance < best - 1E-9)
                    {
                        best = distance;
                        chosen = stretch;
                        segment = k;
                    }
                }
            }

            if (chosen == null)
            {
                return stretches[0].LineIndex;
            }

            GeoPoint3 start = chosen.Vertices[segment];
            GeoPoint3 end = chosen.Vertices[segment + 1];
            double run = start.DistanceTo(end);
            GeoVector3 way = start.GetVectorTo(end).Multiply(1.0 / run);
            var way2 = new GeoVector2(way.X, way.Y);
            double s1 = Dot(first - Flat(start), way2);
            double s2 = Dot(second - Flat(start), way2);
            double lo = chosen.DrawnFrom(segment);
            bool on1 = s1 >= lo - 1E-9 && s1 <= run + 1E-9;
            bool on2 = s2 >= lo - 1E-9 && s2 <= run + 1E-9;
            bool last = segment + 2 >= chosen.Vertices.Count;

            // Neither point on the drawn segment, or one past a vertex between two segments: nothing is broken.
            if ((!on1 && !on2) || (segment > 0 && Math.Min(s1, s2) < -1E-9) || (!last && Math.Max(s1, s2) > run + 1E-9))
            {
                return chosen.LineIndex;
            }

            double from = Math.Max(Math.Min(s1, s2), lo);
            double to = Math.Min(Math.Max(s1, s2), run);

            if (to - from > 1E-9)
            {
                SetSegmentBreaks(
                    mleader,
                    chosen,
                    segment,
                    Merged(
                        mleader.GetBreaks(chosen.LineIndex).ToList(),
                        segment,
                        start,
                        way,
                        new[] { new KeyValuePair<double, double>(from, to) }
                    )
                );
            }

            return chosen.LineIndex;
        }

        /// <summary>Takes every break out of the leader lines of a multileader, and what it keeps of them.</summary>
        private static void ClearLeaderBreaks(DxfMLeader mleader)
        {
            ClearLeaderLineBreaks(mleader);
            ForgetBreaks(mleader);
        }

        /// <summary>Takes every break out of the leader lines of a multileader, the BREAKDATA it keeps left as it is.</summary>
        private static void ClearLeaderLineBreaks(DxfMLeader mleader)
        {
            foreach (int leader in mleader.GetLeaderIndexes())
            {
                foreach (int line in mleader.GetLeaderLineIndexes(leader))
                {
                    if (mleader.GetBreaks(line).Count > 0)
                    {
                        mleader.SetBreaks(line, new DxfLeaderBreak[0]);
                    }
                }
            }
        }

        /// <summary>
        /// Makes the breaks of a multileader again from what it keeps: Auto at the objects named that are still there, where
        /// each crosses its leader lines now, and each break between two points where the points stand.
        /// </summary>
        private static void BreakAgain(CadDocument document, DxfMLeader mleader, List<DimensionBreak> kept)
        {
            ClearLeaderLineBreaks(mleader);
            var cutters = new List<DxfEntity>();

            foreach (DimensionBreak item in kept)
            {
                if (
                    item.ObjectHandle != null
                    && document.FindEntity(item.ObjectHandle) is DxfEntity found
                    && !found.IsErased
                    && !ReferenceEquals(found, mleader)
                    && IsCutter(found)
                )
                {
                    cutters.Add(found);
                }
            }

            if (cutters.Count > 0)
            {
                BreakLeadersAt(mleader, cutters);
            }

            foreach (DimensionBreak item in kept)
            {
                if (item.ObjectHandle == null)
                {
                    ManualLeaderBreak(mleader, P2(item.First), P2(item.Second));
                }
            }
        }

        /// <summary>
        /// DIMBREAK of a multileader, once it is selected: Auto at every object of the space, or at the objects picked one by
        /// one; Manual between two points; Remove takes the breaks out.
        /// </summary>
        private static IEnumerable<CadPrompt> DimBreakMLeader(CadCommandContext c, DxfMLeader mleader)
        {
            var record = new CadEditRecord(CadEditRecordKind.PropertyChanged, mleader.Handle, "Multileader break");

            if (!c.Document.EditGate.Allows(c.Document, mleader, record, out string reason))
            {
                c.Write(reason ?? "The multileader " + mleader.Handle + " may not be changed.");
                yield break;
            }

            yield return CadPrompt.ForEntity(
                "Select object to break dimension",
                new[] { "Auto", "Manual", "Remove" },
                "Auto"
            );
            string value;
            Action<DxfMLeader> change;

            if (c.Input.Is("Auto"))
            {
                value = "Auto";
                change = m => BreakMLeaderAt(c, m, null);
            }
            else if (c.Input.Is("Remove"))
            {
                value = "Remove";
                change = ClearLeaderBreaks;
            }
            else if (c.Input.Is("Manual"))
            {
                yield return CadPrompt.ForPoint("Specify first break point");

                if (!c.Input.IsPoint)
                {
                    yield break;
                }

                GeoPoint2 first = c.Input.Point;
                yield return CadPrompt.ForPoint("Specify second break point", first);

                if (!c.Input.IsPoint)
                {
                    yield break;
                }

                GeoPoint2 second = c.Input.Point;
                value = Values(first.X, first.Y, second.X, second.Y);
                change = m => KeepManualBreak(m, first, second, ManualLeaderBreak(m, first, second));
            }
            else if (c.Input.Kind == CadInputKind.Selection)
            {
                var picked = new List<string> { c.Input.Handles[0] };

                while (true)
                {
                    yield return CadPrompt.ForEntity("Select object to break dimension");

                    if (c.Input.Kind != CadInputKind.Selection)
                    {
                        break;
                    }

                    picked.Add(c.Input.Handles[0]);
                }

                value = string.Join(",", picked);
                change = m => BreakMLeaderAt(c, m, picked);
            }
            else
            {
                yield break;
            }

            var planned = new List<CadEditRecord>
            {
                new CadEditRecord(CadEditRecordKind.PropertyChanged, mleader.Handle, "Multileader break", null, value),
            };
            CadEditResult done = c.Apply(planned, s =>
            {
                if (!s.IsHandled(mleader.Handle))
                {
                    change((DxfMLeader)s.Document.FindEntity(mleader.Handle));
                }
            });

            if (done != null && done.Succeeded)
            {
                c.Write(Modified(1));
            }
        }

        /// <summary>
        /// DIMBREAK Multiple, as AutoCAD 2021 asks it: "Select dimensions:" (dimensions and multileaders both, probed: a level
        /// dimension and a multileader with a line across both are broken, "2 objects modified"), then "Select object to break
        /// dimensions or [Auto/Remove] &lt;Auto&gt;:", Auto at every object of the space, or at the objects picked one by one, or
        /// Remove.
        /// </summary>
        private static IEnumerable<CadPrompt> DimBreakMany(CadCommandContext c)
        {
            yield return CadPrompt.ForSelection("Select dimensions");

            if (c.Input.Kind != CadInputKind.Selection)
            {
                yield break;
            }

            IReadOnlyList<string> handles = c.Input.Handles;
            yield return CadPrompt.ForEntity("Select object to break dimensions", new[] { "Auto", "Remove" }, "Auto");
            bool remove = c.Input.Is("Remove");
            List<string> picked = null;

            if (c.Input.Kind == CadInputKind.Selection)
            {
                picked = new List<string> { c.Input.Handles[0] };

                while (true)
                {
                    yield return CadPrompt.ForEntity("Select object to break dimensions");

                    if (c.Input.Kind != CadInputKind.Selection)
                    {
                        break;
                    }

                    picked.Add(c.Input.Handles[0]);
                }
            }
            else if (!remove && !c.Input.Is("Auto"))
            {
                yield break;
            }

            List<DxfDimension> dimensions = Changeable(c, handles, "Dimension break", d => true);
            var leaders = new List<DxfMLeader>();

            foreach (string handle in handles)
            {
                if (c.Document.FindEntity(handle) is DxfMLeader leader && !leaders.Contains(leader))
                {
                    var record = new CadEditRecord(
                        CadEditRecordKind.PropertyChanged,
                        leader.Handle,
                        "Multileader break"
                    );

                    if (c.Document.EditGate.Allows(c.Document, leader, record, out string reason))
                    {
                        leaders.Add(leader);
                    }
                    else
                    {
                        c.Write(reason ?? "The multileader " + leader.Handle + " may not be changed.");
                    }
                }
            }

            if (dimensions.Count + leaders.Count == 0)
            {
                yield break;
            }

            string value =
                remove ? "Remove"
                : picked == null ? "Auto"
                : string.Join(",", picked);
            var planned = new List<CadEditRecord>();

            foreach (DxfDimension dimension in dimensions)
            {
                planned.Add(
                    new CadEditRecord(
                        CadEditRecordKind.PropertyChanged,
                        dimension.Handle,
                        "Dimension break",
                        null,
                        value
                    )
                );
            }

            foreach (DxfMLeader leader in leaders)
            {
                planned.Add(
                    new CadEditRecord(
                        CadEditRecordKind.PropertyChanged,
                        leader.Handle,
                        "Multileader break",
                        null,
                        value
                    )
                );
            }

            CadEditResult done = c.Apply(planned, s =>
            {
                foreach (CadEditRecord record in planned)
                {
                    if (s.IsHandled(record.Handle))
                    {
                        continue;
                    }

                    DxfEntity entity = s.Document.FindEntity(record.Handle);

                    if (entity is DxfMLeader leader)
                    {
                        if (remove)
                        {
                            ClearLeaderBreaks(leader);
                        }
                        else
                        {
                            BreakMLeaderAt(c, leader, picked);
                        }

                        continue;
                    }

                    var dimension = (DxfDimension)entity;
                    OwnPicture(s.Database, dimension);

                    if (remove)
                    {
                        ForgetBreaks(dimension);
                        Redraw(dimension);
                    }
                    else
                    {
                        BreakAtWithin(c, dimension, picked);
                        RedrawAnnotativeBreaks(dimension);
                    }
                }
            });

            if (done != null && done.Succeeded)
            {
                c.Write(Modified(dimensions.Count + leaders.Count));
            }
        }

        /// <summary>The box a multileader fills (<see cref="DxfEntity.GeometricExtents"/>); empty for one with no line and no content.</summary>
        private static GeoAabb3 MLeaderBox(DxfMLeader mleader)
        {
            try
            {
                return mleader.GeometricExtents;
            }
            catch (InvalidOperationException)
            {
                return GeoAabb3.Empty;
            }
        }

        /// <summary>Breaks a multileader at the objects given (every cutter of the space where none), the words among them measured in the drawing's fonts, and keeps what it was broken at.</summary>
        private static void BreakMLeaderAt(CadCommandContext c, DxfMLeader mleader, IReadOnlyList<string> picked)
        {
            using (Measuring(c.Document))
            {
                List<DxfEntity> cutters = Cutters(c.Document, c.Space, mleader, picked);
                List<DxfEntity> crossed = BreakLeadersAt(mleader, cutters);
                KeepObjectBreaks(
                    mleader,
                    picked == null ? NamedByAuto(MLeaderBox(mleader), cutters, crossed) : crossed
                );
            }
        }
    }
}
