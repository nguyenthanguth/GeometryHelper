using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// MLEADERALIGN (MLA), as AutoCAD 2021's (its prompts and numbers probed in its console, fed through standard input):
    /// "Select multileaders:", "Current mode: Use current spacing", "Select multileader to align to or [Options]:", then the
    /// direction; the content of each multileader is moved with its landing (<see cref="DxfMoveType.MoveContentAndDoglegPoints"/>,
    /// the arrowheads staying) onto the line through the left middle of the box of the one picked along the direction, each by
    /// the left middle of its own box. Use current spacing keeps each content's place along the line; specify Spacing puts the
    /// contents a gap apart along it, the one picked staying where it is and the others following it in the order they stand
    /// along the direction; Distribute spaces them evenly between two points, the first one's left middle on the first point and
    /// the last one's on the second; make leader segments Parallel turns each leader about its arrowhead to run the way the one
    /// picked runs, its length kept, its content moving with it. A content stands in a box of its words' width and height and
    /// the landing gap before and after them (a block's extents), measured in the drawing's fonts, and the gaps are measured
    /// between those boxes: words 1.14 wide in a style of a landing gap of 0.09, a spacing of 20 puts the next box 21.32
    /// beyond the first. A content whose arrowhead stands past the middle of its box once it is moved has its leader turned to
    /// meet it on the other side, the words or block staying where they are.
    /// </summary>
    /// <remarks>
    /// AutoCAD's console reads a typed point of Distribute or Specify direction shifted by the arrowhead of the first
    /// multileader selected; the numbers here have the shift taken out. The left middle is the point the leader's dogleg
    /// ends at where the leader is on the left of the words, whatever their side.
    /// </remarks>
    internal static partial class CadCommands
    {
        // Distribute 0, make leader segments Parallel 1, specify Spacing 2, Use current spacing 3 (the default).
        private static readonly string[] MLeaderAlignModes =
        {
            "Distribute",
            "make leader segments Parallel",
            "specify Spacing",
            "Use current spacing",
        };

        // What the option prompt shows as the default: not the words of the keywords, AutoCAD's own.
        private static readonly string[] MLeaderAlignDefaults =
        {
            "Distribute",
            "make segments Parallel",
            "specify Spacing",
            "Use current spacing",
        };

        private static IEnumerable<CadPrompt> MLeaderAlign(CadCommandContext c)
        {
            yield return CadPrompt.ForSelection("Select multileaders");

            if (c.Input.Kind != CadInputKind.Selection)
            {
                yield break;
            }

            var members = new List<DxfMLeader>();
            int others = 0;
            int locked = 0;

            foreach (string handle in c.Input.Handles)
            {
                DxfEntity entity = c.Document.FindEntity(handle);

                if (entity == null)
                {
                    continue;
                }

                if (!(entity is DxfMLeader mleader) || ContentPoint(mleader) == null)
                {
                    others++;
                }
                else if (CadLayerEditor.IsLocked(c.Database, entity.Layer))
                {
                    locked++;
                }
                else
                {
                    members.Add(mleader);
                }
            }

            if (others > 0)
            {
                c.Write(
                    others.ToString(CultureInfo.InvariantCulture) + (others == 1 ? " was" : " were") + " filtered out."
                );
            }

            if (locked > 0)
            {
                c.Write(
                    locked.ToString(CultureInfo.InvariantCulture)
                        + (locked == 1 ? " was" : " were")
                        + " on a locked layer."
                );
            }

            if (members.Count == 0)
            {
                yield break;
            }

            c.Write(MLeaderAlignMode(c));
            DxfMLeader reference = null;
            GeoVector2 direction = default;
            GeoPoint2 first = default;
            GeoPoint2 second = default;
            bool between = false;

            while (true)
            {
                int mode = (int)c.Remembered("mleaderalign.mode", 3.0);

                if (mode == 0)
                {
                    yield return CadPrompt.ForPoint("Specify first point", null, new[] { "Options" });
                }
                else
                {
                    yield return CadPrompt.ForEntity("Select multileader to align to", new[] { "Options" });
                }

                if (c.Input.Is("Options"))
                {
                    foreach (CadPrompt prompt in MLeaderAlignChooseMode(c))
                    {
                        yield return prompt;
                    }

                    c.Write(MLeaderAlignMode(c));
                    continue;
                }

                if (mode == 0)
                {
                    if (!c.Input.IsPoint)
                    {
                        yield break;
                    }

                    first = c.Input.Point;
                    yield return CadPrompt.ForPoint("Specify second point", first);

                    if (!c.Input.IsPoint)
                    {
                        yield break;
                    }

                    second = c.Input.Point;
                    between = true;
                    break;
                }

                if (c.Input.Kind != CadInputKind.Selection)
                {
                    yield break;
                }

                reference = c.Document.FindEntity(c.Input.Handles[0]) as DxfMLeader;

                ContentBox box = default;
                bool boxed;

                using (Measuring(c.Document))
                {
                    boxed = reference != null && TryContentBox(reference, c.Database, out box);
                }

                if (!boxed)
                {
                    c.Write("Object selected is not a multileader with content.");
                    continue;
                }

                if (mode == 1)
                {
                    break;
                }

                yield return CadPrompt.ForPoint("Specify direction", box.Anchor);

                if (!c.Input.IsPoint)
                {
                    yield break;
                }

                direction = c.Input.Point - box.Anchor;

                if (direction.Length < 1E-9)
                {
                    c.Write("The direction needs a second point apart from the first.");
                    yield break;
                }

                break;
            }

            int chosen = (int)c.Remembered("mleaderalign.mode", 3.0);

            if (chosen == 1 && !between)
            {
                ParallelLeaders(c, members, reference);
                yield break;
            }

            Dictionary<string, GeoVector2> moves;
            Dictionary<string, KeyValuePair<bool, double>> turns;

            using (Measuring(c.Document))
            {
                moves = between
                    ? MLeaderDistribute(members, c.Database, first, second)
                    : MLeaderAlignMoves(
                        members,
                        c.Database,
                        reference,
                        direction,
                        chosen == 2 ? c.Remembered("mleaderalign.spacing", 0.0) : (double?)null
                    );
                turns = MLeaderTurns(members, c.Database, moves, between ? null : reference);
            }

            MoveContents(c, members, moves, turns);
        }

        /// <summary>The point the content of a multileader is held by: its words' attachment point or its block's position; null for a multileader with neither.</summary>
        private static GeoPoint3? ContentPoint(DxfMLeader mleader)
        {
            switch (mleader.ContentType)
            {
                case DxfContentType.MTextContent:
                    return mleader.HasContent() ? mleader.TextLocation : (GeoPoint3?)null;
                case DxfContentType.BlockContent:
                    return mleader.HasContent() ? mleader.BlockPosition : (GeoPoint3?)null;
                default:
                    return null;
            }
        }

        /// <summary>
        /// Where a content stands, as MLEADERALIGN measures it: the box of the words widened by the landing gap before and after
        /// them, as wide and high as AutoCAD gives the words (<see cref="DxfMText.ActualWidth"/>), or of a block its extents; and
        /// the anchor the box is held by, its left edge level with the middle of the words' first line (the block's middle), whichever
        /// side its leader is on. A direction is taken from the anchor of the multileader picked, and every content is put on the
        /// line through it by its own.
        /// </summary>
        private struct ContentBox
        {
            /// <summary>The left middle of the box, the point the content is held by.</summary>
            public GeoPoint2 Anchor;

            /// <summary>The centre of the box.</summary>
            public GeoPoint2 Centre;

            /// <summary>The width of the box, across the words.</summary>
            public double Width;

            /// <summary>The height of the box, the words' own.</summary>
            public double Height;

            /// <summary>The width of the words' ink alone, nought for a block.</summary>
            public double Ink;

            /// <summary>Whether the content is a block.</summary>
            public bool IsBlock;
        }

        private static bool TryContentBox(DxfMLeader mleader, DxfDatabase database, out ContentBox box)
        {
            box = default;
            GeoPoint3? at = ContentPoint(mleader);

            if (at == null)
            {
                return false;
            }

            if (mleader.ContentType == DxfContentType.BlockContent)
            {
                if (TryBlockExtents(mleader, out double blockLeft, out double right, out double low, out double high))
                {
                    var middle = new GeoPoint2(blockLeft, (low + high) / 2.0);
                    box = new ContentBox
                    {
                        Anchor = middle,
                        Centre = new GeoPoint2((blockLeft + right) / 2.0, middle.Y),
                        Width = right - blockLeft,
                        Height = high - low,
                        IsBlock = true,
                    };
                    return true;
                }

                var here = new GeoPoint2(at.Value.X, at.Value.Y);
                box = new ContentBox
                {
                    Anchor = here,
                    Centre = here,
                    IsBlock = true,
                };
                return true;
            }

            DxfMText words = mleader.MText;

            if (words != null)
            {
                // The words are measured in the drawing's fonts, as AutoCAD measures them.
                words.MeasuredIn = database;
            }

            double gap = mleader.LandingGap * mleader.Scale;
            double wide = words == null ? 0.0 : words.ActualWidth;
            double high2 = words == null ? 0.0 : words.ActualHeight;
            double line = words == null ? mleader.TextHeight * mleader.Scale : words.TextHeight;

            if (high2 <= 0.0)
            {
                high2 = line;
            }

            // The middle of the first line: of words of one line, those of the whole (their ink may reach below the baseline).
            bool one = words == null || (!words.Contents.Contains("\\P") && !words.Contents.Contains("\n"));
            double middleDown = (one ? high2 : line) / 2.0;
            bool onRight = words != null && words.Attachment == DxfAttachmentPoint.TopRight;
            double left = (onRight ? at.Value.X - wide : at.Value.X) - gap;
            double width = wide + 2.0 * gap;
            box = new ContentBox
            {
                Anchor = new GeoPoint2(left, at.Value.Y - middleDown),
                Centre = new GeoPoint2(left + width / 2.0, at.Value.Y - high2 / 2.0),
                Width = width,
                Height = high2,
                Ink = wide,
            };
            return true;
        }

        /// <summary>
        /// The extents of a multileader's block as it stands, scaled by the block's scale and the multileader's and turned by its
        /// rotation (attribute definitions left out): false where the block has no shape the library reads.
        /// </summary>
        private static bool TryBlockExtents(
            DxfMLeader mleader,
            out double left,
            out double right,
            out double low,
            out double high
        )
        {
            left = right = low = high = 0.0;
            DxfBlockTableRecord block = mleader.BlockContent;

            if (block == null)
            {
                return false;
            }

            GeoPoint3 position = mleader.BlockPosition;
            GeoVector3 scale = mleader.BlockScale;
            double sx = scale.X * mleader.Scale;
            double sy = scale.Y * mleader.Scale;
            double cos = Math.Cos(mleader.BlockRotation);
            double sin = Math.Sin(mleader.BlockRotation);
            bool boxed = false;

            foreach (DxfEntity entity in block)
            {
                if (entity is DxfAttributeDefinition)
                {
                    continue;
                }

                GeoAabb3 own;

                try
                {
                    own = entity.GeometricExtents;
                }
                catch (InvalidOperationException)
                {
                    continue;
                }

                foreach (double cx in new[] { own.Min.X, own.Max.X })
                {
                    foreach (double cy in new[] { own.Min.Y, own.Max.Y })
                    {
                        double px = cx * sx;
                        double py = cy * sy;
                        double wx = position.X + px * cos - py * sin;
                        double wy = position.Y + px * sin + py * cos;
                        left = boxed ? Math.Min(left, wx) : wx;
                        right = boxed ? Math.Max(right, wx) : wx;
                        low = boxed ? Math.Min(low, wy) : wy;
                        high = boxed ? Math.Max(high, wy) : wy;
                        boxed = true;
                    }
                }
            }

            return boxed;
        }

        private static IEnumerable<CadPrompt> MLeaderAlignChooseMode(CadCommandContext c)
        {
            yield return CadPrompt.ForKeyword(
                "Enter an option",
                MLeaderAlignModes,
                MLeaderAlignDefaults[(int)c.Remembered("mleaderalign.mode", 3.0)]
            );

            int chosen = Array.IndexOf(MLeaderAlignModes, c.Input.Text);

            if (chosen < 0)
            {
                yield break;
            }

            c.Remember("mleaderalign.mode", chosen);

            if (chosen == 2)
            {
                yield return CadPrompt.ForDistance(
                    "Specify spacing",
                    null,
                    null,
                    c.Remembered("mleaderalign.spacing", 0.0).ToString("0.000000", CultureInfo.InvariantCulture)
                );

                if (c.Input.Kind == CadInputKind.Number)
                {
                    c.Remember("mleaderalign.spacing", c.Input.Number);
                }
            }
        }

        private static string MLeaderAlignMode(CadCommandContext c)
        {
            switch ((int)c.Remembered("mleaderalign.mode", 3.0))
            {
                case 0:
                    return "Current mode: Distribute";
                case 1:
                    return "Current mode: Make leader lines parallel";
                case 2:
                    return "Current mode: Spacing: "
                        + c.Remembered("mleaderalign.spacing", 0.0).ToString("0.000000", CultureInfo.InvariantCulture);
                default:
                    return "Current mode: Use current spacing";
            }
        }

        /// <summary>
        /// How far each content moves onto the line through the landing of the multileader picked along a direction: across the
        /// line only, keeping its place along it; with a spacing, a gap apart along the line, the one picked staying and the
        /// others following it in the order they stand along the direction.
        /// </summary>
        private static Dictionary<string, GeoVector2> MLeaderAlignMoves(
            IReadOnlyList<DxfMLeader> members,
            DxfDatabase database,
            DxfMLeader reference,
            GeoVector2 direction,
            double? spacing
        )
        {
            var moves = new Dictionary<string, GeoVector2>(StringComparer.OrdinalIgnoreCase);
            GeoVector2 along = direction / direction.Length;
            var across = new GeoVector2(-along.Y, along.X);
            TryContentBox(reference, database, out ContentBox pivot);
            double lineAt = Dot(pivot.Anchor, across);
            List<KeyValuePair<DxfMLeader, ContentBox>> items = Boxes(members, database, along, pivot.Anchor);
            var wanted = new double[items.Count];
            int fixedAt = items.FindIndex(i =>
                string.Equals(i.Key.Handle, reference.Handle, StringComparison.OrdinalIgnoreCase)
            );

            for (int i = 0; i < items.Count; i++)
            {
                wanted[i] = Dot(items[i].Value.Centre, along);
            }

            if (spacing.HasValue)
            {
                // The one picked first, the others after it in the order they stand along the direction.
                List<int> order = new List<int>();

                if (fixedAt >= 0)
                {
                    order.Add(fixedAt);
                }

                for (int i = 0; i < items.Count; i++)
                {
                    if (i != fixedAt)
                    {
                        order.Add(i);
                    }
                }

                double cursor = Dot(pivot.Centre, along);
                double previous = Extent(pivot, along) / 2.0;
                bool start = fixedAt >= 0;

                foreach (int i in order)
                {
                    double half = Extent(items[i].Value, along) / 2.0;

                    if (start)
                    {
                        wanted[i] = cursor;
                        start = false;
                    }
                    else
                    {
                        cursor += previous + half + spacing.Value;
                        wanted[i] = cursor;
                    }

                    previous = half;
                }
            }

            for (int i = 0; i < items.Count; i++)
            {
                AddMove(moves, items[i], along, across, wanted[i], lineAt);
            }

            return moves;
        }

        /// <summary>
        /// How far each content moves to stand evenly between two points, the first one's landing on the first point and the last
        /// one's on the second, in the order they stand along the line, the gaps between their boxes all the same.
        /// </summary>
        private static Dictionary<string, GeoVector2> MLeaderDistribute(
            IReadOnlyList<DxfMLeader> members,
            DxfDatabase database,
            GeoPoint2 first,
            GeoPoint2 second
        )
        {
            var moves = new Dictionary<string, GeoVector2>(StringComparer.OrdinalIgnoreCase);
            GeoVector2 line = second - first;
            double length = line.Length;

            if (length < 1E-9)
            {
                return moves;
            }

            GeoVector2 along = line / length;
            var across = new GeoVector2(-along.Y, along.X);
            double lineAt = Dot(first, across);
            List<KeyValuePair<DxfMLeader, ContentBox>> items = Boxes(members, database, along, first);
            double total = 0.0;

            for (int i = 0; i + 1 < items.Count; i++)
            {
                total += Extent(items[i].Value, along);
            }

            double gap = items.Count > 1 ? (length - total) / (items.Count - 1) : 0.0;
            double start = Dot(first, along);

            for (int i = 0; i < items.Count; i++)
            {
                double extent = Extent(items[i].Value, along);
                AddMove(moves, items[i], along, across, start + extent / 2.0, lineAt);
                start += extent + gap;
            }

            return moves;
        }

        /// <summary>The contents with their boxes, in the order they stand along a direction from a point, the handle breaking a tie.</summary>
        private static List<KeyValuePair<DxfMLeader, ContentBox>> Boxes(
            IReadOnlyList<DxfMLeader> members,
            DxfDatabase database,
            GeoVector2 along,
            GeoPoint2 origin
        )
        {
            var items = new List<KeyValuePair<DxfMLeader, ContentBox>>();

            foreach (DxfMLeader member in members)
            {
                if (TryContentBox(member, database, out ContentBox box))
                {
                    items.Add(new KeyValuePair<DxfMLeader, ContentBox>(member, box));
                }
            }

            return items
                .OrderBy(i => Dot(i.Value.Anchor - origin, along))
                .ThenBy(i => i.Key.Handle, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static double Dot(GeoPoint2 p, GeoVector2 v) => p.X * v.X + p.Y * v.Y;

        /// <summary>How far a box reaches along a direction, corner to corner.</summary>
        private static double Extent(ContentBox box, GeoVector2 along) =>
            Math.Abs(along.X) * box.Width + Math.Abs(along.Y) * box.Height;

        /// <summary>Notes the move that stands a content's box centre at a place along the line and its landing on the line.</summary>
        private static void AddMove(
            Dictionary<string, GeoVector2> moves,
            KeyValuePair<DxfMLeader, ContentBox> item,
            GeoVector2 along,
            GeoVector2 across,
            double centreAt,
            double lineAt
        )
        {
            ContentBox box = item.Value;
            var offset = new GeoVector2(box.Anchor.X - box.Centre.X, box.Anchor.Y - box.Centre.Y);
            double alongLanding = centreAt + Dot(offset, along);
            GeoPoint2 target = new GeoPoint2(0.0, 0.0) + along * alongLanding + across * lineAt;
            var move = new GeoVector2(target.X - box.Anchor.X, target.Y - box.Anchor.Y);

            if (move.Length > 1E-9)
            {
                moves[item.Key.Handle] = move;
            }
        }

        /// <summary>
        /// make leader segments Parallel: each multileader but the one picked has its first leader line turned about its
        /// arrowhead to run the way the picked one's runs, the length it had kept, the content and landing moving with its end.
        /// </summary>
        private static void ParallelLeaders(
            CadCommandContext c,
            IReadOnlyList<DxfMLeader> members,
            DxfMLeader reference
        )
        {
            if (reference.GetLeaderIndexes().Count == 0)
            {
                return;
            }

            IReadOnlyList<int> lines = reference.GetLeaderLineIndexes(reference.GetLeaderIndexes()[0]);

            if (lines.Count == 0)
            {
                return;
            }

            GeoVector2 way = Leg(reference, lines[0]);

            if (way.Length < 1E-9)
            {
                return;
            }

            way = way / way.Length;
            var planned = new List<KeyValuePair<DxfMLeader, GeoPoint3>>();

            foreach (DxfMLeader member in members.OrderBy(m => m.Handle, StringComparer.OrdinalIgnoreCase))
            {
                if (string.Equals(member.Handle, reference.Handle, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                IReadOnlyList<int> indexes = member.GetLeaderIndexes();

                if (indexes.Count == 0 || member.GetLeaderLineIndexes(indexes[0]).Count == 0)
                {
                    continue;
                }

                int line = member.GetLeaderLineIndexes(indexes[0])[0];
                GeoVector2 leg = Leg(member, line);
                GeoPoint3 arrow = member.GetFirstVertex(line);
                GeoPoint3 end = member.GetLastVertex(line);
                var turned = new GeoPoint3(arrow.X + way.X * leg.Length, arrow.Y + way.Y * leg.Length, end.Z);

                if (new GeoVector2(turned.X - end.X, turned.Y - end.Y).Length > 1E-9)
                {
                    planned.Add(new KeyValuePair<DxfMLeader, GeoPoint3>(member, turned));
                }
            }

            if (planned.Count == 0)
            {
                return;
            }

            List<CadEditRecord> records = planned
                .Select(p => new CadEditRecord(
                    CadEditRecordKind.PropertyChanged,
                    p.Key.Handle,
                    "Leader end",
                    null,
                    Values(p.Value.X, p.Value.Y)
                ))
                .ToList();
            CadEditResult result = c.Apply(records, s =>
            {
                foreach (KeyValuePair<DxfMLeader, GeoPoint3> p in planned)
                {
                    if (!s.IsHandled(p.Key.Handle))
                    {
                        var member = (DxfMLeader)s.Document.FindEntity(p.Key.Handle);
                        member.SetLastVertex(member.GetLeaderLineIndexes(member.GetLeaderIndexes()[0])[0], p.Value);
                    }
                }
            });

            if (!result.Succeeded)
            {
                c.Write(result.Message);
            }
        }

        /// <summary>The way a leader line runs, from its arrowhead to the end of its leader.</summary>
        private static GeoVector2 Leg(DxfMLeader mleader, int line)
        {
            GeoPoint3 arrow = mleader.GetFirstVertex(line);
            GeoPoint3 end = mleader.GetLastVertex(line);
            return new GeoVector2(end.X - arrow.X, end.Y - arrow.Y);
        }

        /// <summary>
        /// Which contents have their leader turned to the other side once they are moved, and to which: a content whose
        /// arrowhead stands past the middle of its box, which is the left of it for a leader on the right, as AutoCAD 2021 turns
        /// them (the box the one picked stays out of). Only a multileader of one leader line is turned, its words held at their top
        /// left or top right, or a block.
        /// </summary>
        private static Dictionary<string, KeyValuePair<bool, double>> MLeaderTurns(
            IReadOnlyList<DxfMLeader> members,
            DxfDatabase database,
            IReadOnlyDictionary<string, GeoVector2> moves,
            DxfMLeader reference
        )
        {
            var turns = new Dictionary<string, KeyValuePair<bool, double>>(StringComparer.OrdinalIgnoreCase);

            foreach (DxfMLeader member in members)
            {
                if (
                    reference != null
                    && string.Equals(member.Handle, reference.Handle, StringComparison.OrdinalIgnoreCase)
                )
                {
                    continue;
                }

                IReadOnlyList<int> leaders = member.GetLeaderIndexes();

                if (leaders.Count != 1 || member.GetLeaderLineIndexes(leaders[0]).Count != 1)
                {
                    continue;
                }

                if (!TryContentBox(member, database, out ContentBox box))
                {
                    continue;
                }

                bool onRight;

                if (box.IsBlock)
                {
                    double along = member.GetDogleg(leaders[0]).X;

                    if (Math.Abs(along) < 1E-9)
                    {
                        continue;
                    }

                    onRight = along < 0.0;
                }
                else
                {
                    DxfMText words = member.MText;

                    if (
                        words == null
                        || (
                            words.Attachment != DxfAttachmentPoint.TopLeft
                            && words.Attachment != DxfAttachmentPoint.TopRight
                        )
                    )
                    {
                        continue;
                    }

                    onRight = words.Attachment == DxfAttachmentPoint.TopRight;
                }

                double centre = box.Centre.X + (moves.TryGetValue(member.Handle, out GeoVector2 move) ? move.X : 0.0);
                double arrow = member.GetFirstVertex(member.GetLeaderLineIndexes(leaders[0])[0]).X;
                bool wanted = arrow > centre + 1E-9;

                if (wanted != onRight)
                {
                    turns[member.Handle] = new KeyValuePair<bool, double>(wanted, box.Ink);
                }
            }

            return turns;
        }

        /// <summary>Moves the content of each multileader by its own vector, with its landing, and turns the leaders that are to meet their content on the other side, as one edit of the command.</summary>
        private static void MoveContents(
            CadCommandContext c,
            IReadOnlyList<DxfMLeader> members,
            IReadOnlyDictionary<string, GeoVector2> moves,
            IReadOnlyDictionary<string, KeyValuePair<bool, double>> turns
        )
        {
            if (moves.Count == 0 && turns.Count == 0)
            {
                return;
            }

            List<string> handles = moves
                .Keys.Union(turns.Keys, StringComparer.OrdinalIgnoreCase)
                .OrderBy(h => h, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var planned = handles
                .Select(h => new CadEditRecord(
                    CadEditRecordKind.PropertyChanged,
                    h,
                    "Content position",
                    null,
                    moves.TryGetValue(h, out GeoVector2 by) ? Values(by.X, by.Y) : Values(0.0, 0.0)
                ))
                .ToList();
            CadEditResult result = c.Apply(planned, s =>
            {
                foreach (string handle in handles)
                {
                    if (s.IsHandled(handle))
                    {
                        continue;
                    }

                    var member = (DxfMLeader)s.Document.FindEntity(handle);

                    if (moves.TryGetValue(handle, out GeoVector2 by))
                    {
                        member.MoveMLeader(new GeoVector3(by.X, by.Y, 0.0), DxfMoveType.MoveContentAndDoglegPoints);
                    }

                    if (turns.TryGetValue(handle, out KeyValuePair<bool, double> turn))
                    {
                        member.SwitchSide(turn.Key, turn.Value);
                    }
                }
            });

            if (!result.Succeeded)
            {
                c.Write(result.Message);
            }
        }

        /// <summary>
        /// Lets the drawing's fonts measure words for as long as the scope lives: the folders the presenter looks in and the
        /// Fonts folder of Windows, as MLEADEREDIT measures them; the drawing's own search path is put back after.
        /// </summary>
        private static IDisposable Measuring(CadDocument document) => new MeasuringScope(document);

        private sealed class MeasuringScope : IDisposable
        {
            private readonly DxfDatabase _database;
            private readonly string _searched;

            public MeasuringScope(CadDocument document)
            {
                _database = document.Database;
                _searched = _database.FontSearchPath;
                _database.FontSearchPath = string.Join(
                    ";",
                    document.Fonts.Folders.Concat(new[] { Environment.GetFolderPath(Environment.SpecialFolder.Fonts) })
                );
            }

            public void Dispose() => _database.FontSearchPath = _searched;
        }
    }
}
