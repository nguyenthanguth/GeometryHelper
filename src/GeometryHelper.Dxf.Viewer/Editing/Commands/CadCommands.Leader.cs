using System;
using System.Collections.Generic;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// LEADER and QLEADER, on the command line in AutoCAD 2021's words: a LEADER drawn by the current dimension style through
    /// the points picked, pointing to an MTEXT, a copy of a text, an INSERT or nothing. The arrow, the hook and the words are
    /// the style's sizes times DIMSCALE, the current dimension variables other than the style's written as the leader's
    /// overrides, as AutoCAD writes them: DIMSCALE 2 with the style's 1 gives <c>1070 40, 1040 2</c> and words twice as high.
    /// </summary>
    internal static partial class CadCommands
    {
        private static readonly string[] LeaderOptions = { "Annotation", "Format", "Undo" };

        private static readonly string[] LeaderFormats = { "Spline", "STraight", "Arrow", "None" };

        private static readonly string[] LeaderAnnotations = { "Tolerance", "Copy", "Block", "None", "Mtext" };

        // The steepest last segment that takes no hook: AutoCAD 2021 hooks one of 15.1 degrees and not one of 14.9.
        private static readonly double LeaderHookSlope = Math.Tan(15.0 * Math.PI / 180.0);

        /// <summary>What a leader points to, as LEADER and QLEADER give it.</summary>
        private sealed class LeaderWords
        {
            /// <summary>The MTEXT the leader points to, its location and attachment yet to be set; null for none.</summary>
            public DxfMText Text { get; set; }

            /// <summary>The handle of the INSERT the leader points to; null for none.</summary>
            public string Block { get; set; }
        }

        /// <summary>
        /// LEADER (LEAD): "Specify leader start point:", "Specify next point:", then "Specify next point or
        /// [Annotation/Format/Undo] &lt;Annotation&gt;:" until the words are asked, "Enter first line of annotation text or
        /// &lt;options&gt;:" and each next line; Enter there asks "Enter an annotation option [Tolerance/Copy/Block/None/Mtext]
        /// &lt;Mtext&gt;:". Format makes the leader a spline or straight, with an arrow or none; Undo takes the last point back. A
        /// point picked again where the last one is asks again, as AutoCAD 2021 takes no segment of no length.
        /// </summary>
        private static IEnumerable<CadPrompt> Leader(CadCommandContext c)
        {
            yield return CadPrompt.ForPoint("Specify leader start point");

            if (!c.Input.IsPoint)
            {
                yield break;
            }

            var points = new List<GeoPoint2> { c.Input.Point };
            bool spline = false;
            bool arrow = true;

            while (true)
            {
                GeoPoint2 last = points[points.Count - 1];
                bool second = points.Count == 1;
                yield return second
                    ? CadPrompt.ForPoint("Specify next point", last)
                    : CadPrompt.ForPoint("Specify next point", last, LeaderOptions, "Annotation");

                if (c.Input.IsPoint)
                {
                    if ((c.Input.Point - last).Length > c.Session.Tolerance.EqualPoint)
                    {
                        points.Add(c.Input.Point);
                    }

                    continue;
                }

                if (second)
                {
                    yield break;
                }

                if (c.Input.Is("Format"))
                {
                    yield return CadPrompt
                        .ForKeyword("Enter leader format option", LeaderFormats, "Exit")
                        .Hiding("eXit");
                    spline = c.Input.Is("Spline") || (spline && !c.Input.Is("STraight"));
                    arrow = c.Input.Is("Arrow") || (arrow && !c.Input.Is("None"));
                    continue;
                }

                if (c.Input.Is("Undo"))
                {
                    points.RemoveAt(points.Count - 1);
                    continue;
                }

                if (c.Input.Is("Annotation"))
                {
                    break;
                }

                yield break;
            }

            var lines = new List<string>();
            yield return CadPrompt.ForString("Enter first line of annotation text or", "options");

            if (c.Input.Kind == CadInputKind.Text && c.Input.Text.Length > 0)
            {
                lines.Add(c.Input.Text);

                foreach (CadPrompt prompt in NextLines(c, lines))
                {
                    yield return prompt;
                }

                MakeLeader(c, points, spline, arrow, new LeaderWords { Text = LeaderText(c, lines) }, false, 0.0);
                yield break;
            }

            if (!c.Input.IsNone)
            {
                yield break;
            }

            while (true)
            {
                yield return CadPrompt.ForKeyword("Enter an annotation option", LeaderAnnotations, "Mtext");

                if (c.Input.Is("None"))
                {
                    MakeLeader(c, points, spline, arrow, null, false, 0.0);
                    yield break;
                }

                if (c.Input.Is("Tolerance"))
                {
                    c.Write(
                        "Tolerance needs the Geometric Tolerance dialog, which the presenter has not: choose another option."
                    );
                    continue;
                }

                if (c.Input.Is("Copy"))
                {
                    yield return CadPrompt.ForEntity("Select an object to copy");
                    DxfMText copy = CopiedText(c);

                    // A miss, or an object that is no text, asks the option again, as AutoCAD 2021 asks it after a miss.
                    if (copy == null)
                    {
                        continue;
                    }

                    MakeLeader(c, points, spline, arrow, new LeaderWords { Text = copy }, false, 0.0);
                    yield break;
                }

                if (c.Input.Is("Block"))
                {
                    var made = new LeaderWords();

                    foreach (CadPrompt prompt in LeaderBlock(c, made))
                    {
                        yield return prompt;
                    }

                    if (made.Block != null)
                    {
                        MakeLeader(c, points, spline, arrow, made, false, 0.0);
                    }

                    yield break;
                }

                if (c.Input.Is("Mtext"))
                {
                    var text = new List<string>();

                    foreach (CadPrompt prompt in LeaderMText(c, points, false, text))
                    {
                        yield return prompt;
                    }

                    if (text.Count > 0)
                    {
                        MakeLeader(
                            c,
                            points,
                            spline,
                            arrow,
                            new LeaderWords { Text = LeaderText(c, text) },
                            false,
                            0.0
                        );
                    }
                }

                yield break;
            }
        }

        /// <summary>
        /// QLEADER (LE) with the default settings of its Leader Settings dialog, which the presenter has not yet: "Specify first
        /// leader point, or [Settings] &lt;Settings&gt;:", two next points at most, "Specify text width &lt;0.0000&gt;:", then
        /// "Enter first line of annotation text &lt;Mtext&gt;:" and each next line, Enter there giving the MTEXT editor. The words
        /// stand with the middle of their top line at the leader's end on the right, of their bottom line on the left, and the
        /// leader overrides DIMGAP and DIMTAD (nought) whatever the style has, as AutoCAD 2021 writes them.
        /// </summary>
        private static IEnumerable<CadPrompt> QuickLeader(CadCommandContext c)
        {
            while (true)
            {
                yield return CadPrompt.ForPoint("Specify first leader point,", null, new[] { "Settings" }, "Settings");

                if (c.Input.IsPoint)
                {
                    break;
                }

                if (!c.Input.Is("Settings"))
                {
                    yield break;
                }

                c.Write(
                    "The Leader Settings dialog is not in the presenter yet: QLEADER draws with its default settings."
                );
            }

            var points = new List<GeoPoint2> { c.Input.Point };

            // Three points at most, AutoCAD's Maximum number of points; Enter ends them sooner.
            while (points.Count < 3)
            {
                GeoPoint2 last = points[points.Count - 1];
                yield return CadPrompt.ForPoint("Specify next point", last);

                if (c.Input.IsPoint)
                {
                    if ((c.Input.Point - last).Length > c.Session.Tolerance.EqualPoint)
                    {
                        points.Add(c.Input.Point);
                    }

                    continue;
                }

                if (c.Input.IsNone && points.Count >= 2)
                {
                    break;
                }

                yield break;
            }

            // The width is not kept from one leader to the next: AutoCAD 2021 offers nought each time, and takes one below
            // nought as nought.
            yield return CadPrompt.ForDistance("Specify text width", points[points.Count - 1], null, Length(0.0));
            double width = c.Input.Kind == CadInputKind.Number && c.Input.Number > 0.0 ? c.Input.Number : 0.0;

            if (!c.Input.IsNone && c.Input.Kind != CadInputKind.Number)
            {
                yield break;
            }

            var lines = new List<string>();
            yield return CadPrompt.ForString("Enter first line of annotation text", "Mtext");

            if (c.Input.Kind == CadInputKind.Text && c.Input.Text.Length > 0)
            {
                lines.Add(c.Input.Text);

                foreach (CadPrompt prompt in NextLines(c, lines))
                {
                    yield return prompt;
                }
            }
            else if (c.Input.IsNone)
            {
                foreach (CadPrompt prompt in LeaderMText(c, points, true, lines))
                {
                    yield return prompt;
                }
            }

            if (lines.Count > 0)
            {
                MakeLeader(c, points, false, true, new LeaderWords { Text = LeaderText(c, lines) }, true, width);
            }
        }

        /// <summary>"Enter next line of annotation text:" until Enter, each line added.</summary>
        private static IEnumerable<CadPrompt> NextLines(CadCommandContext c, List<string> lines)
        {
            while (true)
            {
                yield return CadPrompt.ForString("Enter next line of annotation text");

                if (c.Input.Kind != CadInputKind.Text || c.Input.Text.Length == 0)
                {
                    yield break;
                }

                lines.Add(c.Input.Text);
            }
        }

        /// <summary>The words of a leader's MTEXT edited in place at the leader's end, as AutoCAD opens its MTEXT editor there; the command line takes them typed.</summary>
        private static IEnumerable<CadPrompt> LeaderMText(
            CadCommandContext c,
            List<GeoPoint2> points,
            bool quick,
            List<string> words
        )
        {
            double scale = LeaderScale(c);
            var edit = new CadInPlaceEdit(
                CadInPlaceKind.MText,
                null,
                string.Empty,
                points[points.Count - 1],
                c.Database.Dimtxt * scale,
                0.0,
                0.0,
                LeaderTextStyle(c),
                quick ? 1 : 4
            );
            yield return CadPrompt.ForInPlaceEdit("Enter text", edit);

            if (c.Input.Kind == CadInputKind.Text && c.Input.Text.Length > 0)
            {
                words.Add(c.Input.Text);
            }
        }

        /// <summary>
        /// Block of LEADER: "Enter block name or [?]:", the last block offered, then the INSERT placed as INSERT places one
        /// ("Specify insertion point or [Basepoint/Scale/X/Y/Z/Rotate]:" and on), its handle handed back.
        /// </summary>
        private static IEnumerable<CadPrompt> LeaderBlock(CadCommandContext c, LeaderWords made)
        {
            string name;

            while (true)
            {
                yield return CadPrompt.ForKeywordOrText("Enter block name or", new[] { "?" }, c.Session.LastBlock);

                if (c.Input.Is("?"))
                {
                    WriteBlockList(c);
                    continue;
                }

                name = c.Input.Kind == CadInputKind.Text ? c.Input.Text.Trim() : c.Session.LastBlock;

                if (string.IsNullOrEmpty(name))
                {
                    yield break;
                }

                if (c.Database.BlockTable.Has(name) && !c.Database.BlockTable[name].IsLayout)
                {
                    break;
                }

                // AutoCAD 2021 looks for a drawing of the name and asks again; the presenter's LEADER places the drawing's blocks.
                c.Write("Block \"" + name + "\" not found.");
            }

            var placing = new Placing { Scale = new GeoVector3(1.0, 1.0, 1.0) };

            foreach (CadPrompt prompt in Place(c, name, placing, true, true, true, false))
            {
                yield return prompt;
            }

            if (!placing.Placed)
            {
                yield break;
            }

            DxfEntity insert = null;

            foreach (DxfEntity entity in c.Space.Block)
            {
                if (entity is DxfBlockReference)
                {
                    insert = entity;
                }
            }

            made.Block = insert?.Handle;
        }

        /// <summary>
        /// Copy of LEADER: a copy of the MTEXT picked, or an MTEXT of the words, height and style of the TEXT picked, as AutoCAD
        /// 2021 copies a TEXT; null for a miss or another kind of object.
        /// </summary>
        private static DxfMText CopiedText(CadCommandContext c)
        {
            if (c.Input.Kind != CadInputKind.Selection || c.Input.Handles.Count == 0)
            {
                return null;
            }

            DxfEntity picked = c.Document.FindEntity(c.Input.Handles[0]);

            if (picked is DxfMText mtext)
            {
                var copy = new DxfMText { Contents = mtext.Contents, TextHeight = mtext.TextHeight };
                copy.TextStyleName = mtext.TextStyleName;
                return copy;
            }

            if (picked is DxfDBText text)
            {
                var copy = new DxfMText { Contents = text.TextString, TextHeight = text.Height };
                copy.TextStyleName = text.TextStyleName;
                return copy;
            }

            return null;
        }

        /// <summary>The MTEXT of lines typed: AutoCAD's <c>\A1;</c> before them and <c>\P</c> between, DIMTXT times the scale high, in DIMTXSTY.</summary>
        private static DxfMText LeaderText(CadCommandContext c, List<string> lines)
        {
            var text = new DxfMText
            {
                Contents = "\\A1;" + string.Join("\\P", lines),
                TextHeight = c.Database.Dimtxt * LeaderScale(c),
            };
            text.TextStyleName = LeaderTextStyle(c);
            return text;
        }

        private static string LeaderTextStyle(CadCommandContext c) => c.Database.Dimtxsty?.Name ?? "Standard";

        /// <summary>
        /// The DIMSCALE a leader is drawn by, as AutoCAD 2021 takes it: the drawing's where it is above nought; with nought,
        /// one over the scale of the viewport model space is worked through (2 through one of 1:2), else 1.
        /// </summary>
        private static double LeaderScale(CadCommandContext c)
        {
            double set = c.Database.Dimscale;

            if (set > 0.0)
            {
                return set;
            }

            double paper = c.Document.ActiveViewport?.CustomScale ?? 0.0;
            return paper > 0.0 && !double.IsInfinity(1.0 / paper) ? 1.0 / paper : 1.0;
        }

        /// <summary>
        /// Adds the leader and what it points to, one undo step. An MTEXT stands DIMGAP from the last vertex, to the right of it
        /// where the last segment runs right or straight up or down, else to its left; a hook DIMASZ long runs on to it where
        /// that segment of a straight leader is steeper than 15 degrees. LEADER sets the words' middle at the hook (their
        /// bottom DIMGAP above it with DIMTAD not nought); QLEADER the middle of their top line on the right, of their bottom
        /// line on the left.
        /// </summary>
        private static void MakeLeader(
            CadCommandContext c,
            List<GeoPoint2> points,
            bool spline,
            bool arrow,
            LeaderWords words,
            bool quick,
            double width
        )
        {
            DxfDatabase db = c.Database;
            string styleName = db.Dimstyle;
            DxfDimStyleTableRecord style = db.DimStyleTable.Has(styleName) ? db.DimStyleTable[styleName] : null;
            double scale = LeaderScale(c);
            double gap = db.Dimgap * scale;
            int tad = quick ? 0 : db.Dimtad;
            DxfMText text = words?.Text;

            GeoPoint2 end = points[points.Count - 1];
            GeoVector2 way = end - points[points.Count - 2];
            bool left = way.X < -c.Session.Tolerance.EqualPoint;
            double side = left ? -1.0 : 1.0;
            bool hook = text != null && !spline && Math.Abs(way.Y) > LeaderHookSlope * Math.Abs(way.X);
            var vertices = new List<GeoPoint2>(points);

            if (hook)
            {
                vertices.Insert(vertices.Count - 1, new GeoPoint2(end.X - side * db.Dimasz * scale, end.Y));
            }

            if (text != null)
            {
                double half = text.TextHeight / 2.0;
                text.Location = quick
                    ? new GeoPoint3(end.X + side * gap, end.Y + (left ? -half : half), 0.0)
                    : new GeoPoint3(end.X + side * gap, end.Y + (tad != 0 ? gap : 0.0), 0.0);
                text.Attachment =
                    quick ? (left ? DxfAttachmentPoint.BottomRight : DxfAttachmentPoint.TopLeft)
                    : tad != 0 ? (left ? DxfAttachmentPoint.BottomRight : DxfAttachmentPoint.BottomLeft)
                    : left ? DxfAttachmentPoint.MiddleRight
                    : DxfAttachmentPoint.MiddleLeft;

                if (width > 0.0)
                {
                    text.Width = width;
                }
            }

            CadSpace space = c.Space;
            string block = words?.Block;
            c.Apply(null, s =>
            {
                var leader = new DxfLeader();

                foreach (GeoPoint2 vertex in vertices)
                {
                    leader.AppendVertex(P3(vertex));
                }

                leader.HasArrowHead = arrow;
                leader.IsSplined = spline;
                s.Append(space, Current(c, leader));

                if (style != null)
                {
                    leader.DimensionStyleName = style.Name;
                }

                Override(leader, style, db, scale, quick);
                double height = 0.0;
                double across = 0.0;

                if (text != null)
                {
                    s.Append(space, Current(c, text));
                    leader.Annotation = text;
                    CadMTextLayout layout = CadMTextEdit.LayoutOf(text);
                    height = layout?.ActualHeight ?? 0.0;
                    across = layout?.ActualWidth ?? 0.0;
                }
                else if (block != null)
                {
                    leader.Annotation = s.Document.FindEntity(block);
                }

                // AutoCAD 2021 writes 74 of 1 on a leader of no annotation too.
                leader.SetMade(hook, text != null ? left : block == null, height, across);
            });
        }

        /// <summary>
        /// Writes the current dimension variables the leader is drawn by where they are not its style's, as AutoCAD 2021 writes
        /// them, in the order of their groups: DIMSCALE (40), DIMASZ (41), DIMTAD (77), DIMTXT (140) and DIMGAP (147). QLEADER
        /// writes DIMGAP and DIMTAD (nought) first whatever the style has, and moves each other than the style's after them.
        /// </summary>
        private static void Override(
            DxfLeader leader,
            DxfDimStyleTableRecord style,
            DxfDatabase db,
            double scale,
            bool quick
        )
        {
            if (!quick)
            {
                leader.Dimscale = scale;
                leader.Dimasz = db.Dimasz;
                leader.Dimtad = db.Dimtad;
                leader.Dimtxt = db.Dimtxt;
                leader.Dimgap = db.Dimgap;
                return;
            }

            leader.WriteOverride(147, db.Dimgap, false);
            leader.WriteOverride(77, 0.0, true);

            if (style == null)
            {
                return;
            }

            if (!scale.Equals(style.Dimscale))
            {
                leader.WriteOverride(40, scale, false);
            }

            if (!db.Dimasz.Equals(style.Dimasz))
            {
                leader.WriteOverride(41, db.Dimasz, false);
            }

            if (db.Dimtad != style.Dimtad)
            {
                leader.WriteOverride(77, 0.0, true);
            }

            if (!db.Dimtxt.Equals(style.Dimtxt))
            {
                leader.WriteOverride(140, db.Dimtxt, false);
            }

            if (!db.Dimgap.Equals(style.Dimgap))
            {
                leader.WriteOverride(147, db.Dimgap, false);
            }
        }
    }
}
