using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// MLEADERCOLLECT (MLC), as AutoCAD 2021's (its prompts and groups probed in its console, fed through standard input,
    /// multileaders made by MLEADER in a style of a block): the multileaders stay as they are, each one's block moved so that
    /// they stand side by side (Horizontal), one under another (Vertical) or in rows (Wrap, by a width the next block must stay
    /// short of or by a number to a row), their boxes (the extents of the shapes of each block) touching, their tops level and
    /// the left of the first at the point, which is the top left of the group: three circles of radius 5 collected at (150, 150)
    /// stand with their middles at 155, 165 and 175 and their tops at 150. Only one leader is kept: the leader lines of the
    /// last multileader selected are landed at the left of the first block, or at the right of the last block of the first row
    /// where their arrowhead is nearer that landing (a column has them both at the last block, the middle of that block the
    /// limit), and the leaders of every multileader are taken away before; a last multileader with a leader on each side keeps
    /// each at its end. Multileaders of words, objects that are none and those on a locked layer are not collected.
    /// </summary>
    /// <remarks>
    /// AutoCAD's console reads the point shifted by the arrowhead of the first multileader selected; the numbers here have the
    /// shift taken out.
    /// </remarks>
    internal static partial class CadCommands
    {
        // The keywords as the prompt writes them; the mode kept is the position in MLeaderCollectModes.
        private static readonly string[] MLeaderCollectWords = { "Vertical", "Horizontal", "Wrap" };

        private static readonly string[] MLeaderCollectModes = { "Horizontal", "Vertical", "Wrap" };

        private static IEnumerable<CadPrompt> MLeaderCollect(CadCommandContext c)
        {
            var members = new List<DxfMLeader>();

            while (true)
            {
                yield return CadPrompt.ForSelection("Select multileaders");

                if (c.Input.Kind != CadInputKind.Selection || c.Input.Handles.Count == 0)
                {
                    yield break;
                }

                members.Clear();
                int others = 0;
                int locked = 0;

                // In the order the objects were taken, as AutoCAD lays out by the selection set; the handles in ordinal order where it is not kept.
                IReadOnlyList<string> picked =
                    c.Input.PickOrder.Count == c.Input.Handles.Count ? c.Input.PickOrder : c.Input.Handles;

                foreach (string handle in picked)
                {
                    DxfEntity entity = c.Document.FindEntity(handle);

                    if (entity == null)
                    {
                        continue;
                    }

                    if (!(entity is DxfMLeader mleader) || !IsBlockLeader(mleader))
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
                        others.ToString(CultureInfo.InvariantCulture)
                            + (others == 1 ? " was" : " were")
                            + " filtered out"
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

                // Nothing to collect: it asks again, as AutoCAD does.
                if (members.Count > 0)
                {
                    break;
                }
            }

            GeoPoint2 point = default;

            while (true)
            {
                int mode = (int)c.Remembered("mleadercollect.mode", 0.0);
                yield return CadPrompt.ForPoint(
                    "Specify collected multileader location",
                    null,
                    MLeaderCollectWords,
                    MLeaderCollectModes[mode]
                );

                if (c.Input.IsPoint)
                {
                    point = c.Input.Point;
                    break;
                }

                if (c.Input.Is("Wrap"))
                {
                    foreach (CadPrompt prompt in MLeaderCollectWrap(c))
                    {
                        yield return prompt;
                    }

                    c.Remember("mleadercollect.mode", 2.0);
                    continue;
                }

                int chosen = Array.IndexOf(MLeaderCollectModes, c.Input.Text);

                if (chosen < 0)
                {
                    yield break;
                }

                c.Remember("mleadercollect.mode", chosen);
            }

            Collect(
                c,
                members,
                point,
                (int)c.Remembered("mleadercollect.mode", 0.0),
                c.Remembered("mleadercollect.byNumber", 0.0) != 0.0,
                c.Remembered("mleadercollect.width", 0.0),
                (int)c.Remembered("mleadercollect.number", 1.0)
            );
        }

        /// <summary>Whether a multileader is one MLEADERCOLLECT collects: a block with its content, the words and the empty ones left.</summary>
        private static bool IsBlockLeader(DxfMLeader mleader) =>
            mleader.ContentType == DxfContentType.BlockContent && ContentPoint(mleader) != null;

        /// <summary>
        /// The Wrap option: "Specify wrap width or [Number]: &lt;0.000000&gt;", a number kept for the width (any number, a width of
        /// nought or less puts one block to a row), or Number, "Specify number per row &lt;1&gt;:", a whole number above nought
        /// kept for the rows.
        /// </summary>
        private static IEnumerable<CadPrompt> MLeaderCollectWrap(CadCommandContext c)
        {
            yield return CadPrompt
                .ForDistance(
                    "Specify wrap width",
                    null,
                    new[] { "Number" },
                    c.Remembered("mleadercollect.width", 0.0).ToString("0.000000", CultureInfo.InvariantCulture)
                )
                .WithDefaultAfterColon();

            if (c.Input.Is("Number"))
            {
                while (true)
                {
                    yield return CadPrompt
                        .ForInteger(
                            "Specify number per row",
                            null,
                            ((int)c.Remembered("mleadercollect.number", 1.0)).ToString(CultureInfo.InvariantCulture)
                        )
                        .Refusing("Requires a positive nonzero integer.");

                    if (c.Input.Kind != CadInputKind.Number)
                    {
                        yield break;
                    }

                    if (c.Input.Number < 1.0)
                    {
                        c.Write("Value must be positive and nonzero.");
                        continue;
                    }

                    c.Remember("mleadercollect.number", c.Input.Number);
                    c.Remember("mleadercollect.byNumber", 1.0);
                    yield break;
                }
            }

            if (c.Input.Kind == CadInputKind.Number)
            {
                c.Remember("mleadercollect.width", c.Input.Number);
                c.Remember("mleadercollect.byNumber", 0.0);
            }
        }

        /// <summary>
        /// Stands the blocks in rows from the point and keeps the leader: every leader taken away, each block's content moved
        /// with its base point, and the leader lines of the last multileader put on the block at the end of the row that each
        /// one's arrowhead is nearer, as one edit of the command.
        /// </summary>
        private static void Collect(
            CadCommandContext c,
            IReadOnlyList<DxfMLeader> members,
            GeoPoint2 point,
            int mode,
            bool byNumber,
            double wrap,
            int number
        )
        {
            var boxes = new List<ContentBox>();

            foreach (DxfMLeader member in members)
            {
                TryContentBox(member, c.Database, out ContentBox box);
                boxes.Add(box);
            }

            // The rows of the blocks: one for Horizontal, one block to each for Vertical, and for Wrap a block that would reach the
            // width, or the number a row holds, starts the next.
            var rows = new List<List<int>>();
            double used = 0.0;

            for (int i = 0; i < boxes.Count; i++)
            {
                bool starts =
                    rows.Count == 0
                    || mode == 1
                    || (
                        mode == 2 && (byNumber ? rows[rows.Count - 1].Count >= number : !(used + boxes[i].Width < wrap))
                    );

                if (starts)
                {
                    rows.Add(new List<int>());
                    used = 0.0;
                }

                rows[rows.Count - 1].Add(i);
                used += boxes[i].Width;
            }

            var lefts = new double[boxes.Count];
            var tops = new double[boxes.Count];
            double top = point.Y;

            foreach (List<int> row in rows)
            {
                double x = point.X;
                double high = 0.0;

                foreach (int i in row)
                {
                    lefts[i] = x;
                    tops[i] = top;
                    x += boxes[i].Width;
                    high = Math.Max(high, boxes[i].Height);
                }

                top -= high;
            }

            var moves = new GeoVector2[boxes.Count];

            for (int i = 0; i < boxes.Count; i++)
            {
                var target = new GeoPoint2(lefts[i], tops[i] - boxes[i].Height / 2.0);
                moves[i] = new GeoVector2(target.X - boxes[i].Anchor.X, target.Y - boxes[i].Anchor.Y);
            }

            // The leader lines of the last multileader, leader by leader, read before anything moves: a leader goes to the left
            // end of the group or the right, as its first arrowhead is nearer the landing there; a column has both ends at the
            // last block.
            DxfMLeader last = members[members.Count - 1];
            int leftEnd = mode == 1 ? members.Count - 1 : rows[0][0];
            int rightEnd = mode == 1 ? members.Count - 1 : rows[0][rows[0].Count - 1];
            double leftLanding = lefts[leftEnd] - members[leftEnd].DoglegLength * members[leftEnd].Scale;
            double rightLanding =
                lefts[rightEnd] + boxes[rightEnd].Width + members[rightEnd].DoglegLength * members[rightEnd].Scale;
            double middle = (leftLanding + rightLanding) / 2.0;
            var leftLines = new List<GeoPoint3>();
            var rightLines = new List<GeoPoint3>();

            foreach (int index in last.GetLeaderIndexes())
            {
                List<GeoPoint3> arrows = last.GetLeaderLineIndexes(index).Select(l => last.GetFirstVertex(l)).ToList();

                if (arrows.Count > 0)
                {
                    (arrows[0].X > middle ? rightLines : leftLines).AddRange(arrows);
                }
            }

            var records = new List<CadEditRecord>();

            for (int i = 0; i < members.Count; i++)
            {
                records.Add(
                    new CadEditRecord(
                        CadEditRecordKind.PropertyChanged,
                        members[i].Handle,
                        "Content position",
                        null,
                        Values(moves[i].X, moves[i].Y)
                    )
                );
            }

            CadEditResult result = c.Apply(records, s =>
            {
                for (int i = 0; i < members.Count; i++)
                {
                    if (s.IsHandled(members[i].Handle))
                    {
                        continue;
                    }

                    var member = (DxfMLeader)s.Document.FindEntity(members[i].Handle);

                    foreach (int index in member.GetLeaderIndexes().ToList())
                    {
                        member.RemoveLeader(index);
                    }

                    if (moves[i].Length > 1E-9)
                    {
                        member.MoveMLeader(
                            new GeoVector3(moves[i].X, moves[i].Y, 0.0),
                            DxfMoveType.MoveContentAndDoglegPoints
                        );
                    }
                }

                PutLeaderLines(
                    s,
                    members[leftEnd],
                    leftLines,
                    leftLanding - 1.0,
                    tops[leftEnd] - boxes[leftEnd].Height / 2.0
                );
                PutLeaderLines(
                    s,
                    members[rightEnd],
                    rightLines,
                    rightLanding + 1.0,
                    tops[rightEnd] - boxes[rightEnd].Height / 2.0
                );
            });

            if (!result.Succeeded)
            {
                c.Write(result.Message);
            }
        }

        /// <summary>
        /// Puts leader lines from arrowheads on a multileader whose leaders are gone: the first is added from a point past the landing on the side
        /// of the block it lands at (AddLeaderLine meets the block there, as AutoCAD's MLEADEREDIT does), each further one, from the same point, to the
        /// same leader, and every line's arrowhead set to where it was.
        /// </summary>
        private static void PutLeaderLines(
            CadEditScope s,
            DxfMLeader at,
            IReadOnlyList<GeoPoint3> arrows,
            double x,
            double y
        )
        {
            if (arrows.Count == 0 || s.IsHandled(at.Handle))
            {
                return;
            }

            var member = (DxfMLeader)s.Document.FindEntity(at.Handle);

            foreach (GeoPoint3 arrow in arrows)
            {
                int line = member.AddLeaderLine(new GeoPoint3(x, y, arrow.Z));
                member.SetFirstVertex(line, arrow);
            }
        }
    }
}
