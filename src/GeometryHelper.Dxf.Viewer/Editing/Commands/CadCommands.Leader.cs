using System;
using System.Collections.Generic;
using System.Linq;
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

            /// <summary>The handle of an INSERT whose copy the leader points to, as Copy and Reuse Current copy one; null for none.</summary>
            public string Copy { get; set; }

            /// <summary>Whether <see cref="Text"/> holds words typed, which take DIMCLRT where it is not BYBLOCK; false for a copy.</summary>
            public bool Typed { get; set; }

            /// <summary>Whether the words stand with their middle at the leader's end, as a copy of QLEADER's Copy an Object stands.</summary>
            public bool Middle { get; set; }

            /// <summary>The width QLEADER was given for the words; nought for none.</summary>
            public double Width { get; set; }

            /// <summary>Whether QLEADER ends with no leader: Esc, or words or a block not given.</summary>
            public bool Ended { get; set; }
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
                yield return (
                    second
                        ? CadPrompt.ForPoint("Specify next point", last)
                        : CadPrompt.ForPoint("Specify next point", last, LeaderOptions, "Annotation")
                ).Previewing(LeaderPreview(c, points, spline, arrow, CadQLeaderAngle.Any));

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

                MakeLeader(
                    c,
                    points,
                    spline,
                    arrow,
                    new LeaderWords { Text = LeaderText(c, lines), Typed = true },
                    false,
                    0.0
                );
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
                    var copied = new LeaderWords();

                    // A miss, or an object that is no text or INSERT, asks the option again, as AutoCAD 2021 asks it after a miss.
                    if (!Copied(c, copied))
                    {
                        continue;
                    }

                    MakeLeader(c, points, spline, arrow, copied, false, 0.0);
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
                            new LeaderWords { Text = LeaderText(c, text), Typed = true },
                            false,
                            0.0
                        );
                    }
                }

                yield break;
            }
        }

        /// <summary>
        /// QLEADER (LE) by the drawing's Leader Settings (<see cref="CadQLeaderSettings"/>), as AutoCAD 2021 draws it: "Specify
        /// first leader point, or [Settings] &lt;Settings&gt;:", Settings or Enter showing the Leader Settings dialog where the
        /// window has it; "Specify next point:" up to the Maximum number of points (three by default) or until Enter with No
        /// Limit, the first and second segments held to their angle constraints; then the annotation of the settings. MText asks
        /// "Specify text width &lt;0.0000&gt;:" (Prompt for width, not with Always left justify), then "Enter first line of
        /// annotation text &lt;Mtext&gt;:" and each next line, Enter there giving the MTEXT editor; Copy an Object asks "Select an
        /// object to copy:"; Block Reference "Enter block name or [?]:" and INSERT's prompts; None and Tolerance end the leader
        /// with no annotation; Reuse Current copies the annotation kept. The words stand by the Attachment settings (the middle
        /// of their top line at the leader's end on the right, of their bottom line on the left, by default), and the leader
        /// overrides DIMGAP and DIMTAD whatever the style has, as AutoCAD 2021 writes them.
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

                if (!c.Input.Is("Settings") && !c.Input.IsNone)
                {
                    yield break;
                }

                LeaderSettingsDialog(c);
            }

            CadQLeaderSettings settings = CadQLeaderSettings.Read(c.Database);
            var points = new List<GeoPoint2> { c.Input.Point };

            // The Maximum number of points, the arrow's counted, or with No Limit until Enter; Enter ends them sooner.
            while (settings.NoLimit || points.Count < settings.MaxPoints)
            {
                GeoPoint2 last = points[points.Count - 1];
                CadQLeaderAngle angle =
                    points.Count == 1 ? settings.FirstSegment
                    : points.Count == 2 ? settings.SecondSegment
                    : CadQLeaderAngle.Any;
                yield return CadPrompt
                    .ForPoint("Specify next point", last)
                    .Previewing(LeaderPreview(c, points, settings.Spline, true, angle));

                if (c.Input.IsPoint)
                {
                    GeoPoint2 held = HeldToAngle(last, c.Input.Point, angle);

                    if ((held - last).Length > c.Session.Tolerance.EqualPoint)
                    {
                        points.Add(held);
                    }

                    continue;
                }

                if (c.Input.IsNone && points.Count >= 2)
                {
                    break;
                }

                yield break;
            }

            var words = new LeaderWords();

            foreach (CadPrompt prompt in QuickLeaderWords(c, settings, points, words))
            {
                yield return prompt;
            }

            if (words.Ended)
            {
                yield break;
            }

            MakeLeader(
                c,
                points,
                settings.Spline,
                true,
                words.Text == null && words.Block == null && words.Copy == null ? null : words,
                true,
                words.Width,
                settings
            );
        }

        /// <summary>
        /// The annotation of a QLEADER leader, by its settings, into <paramref name="words"/>: <see cref="LeaderWords.Ended"/>
        /// set where the command ends with no leader (Esc, or an MText or a block not given).
        /// </summary>
        private static IEnumerable<CadPrompt> QuickLeaderWords(
            CadCommandContext c,
            CadQLeaderSettings settings,
            List<GeoPoint2> points,
            LeaderWords words
        )
        {
            // Reuse Current: a copy of the annotation kept, words or an INSERT, no width or words asked, as AutoCAD 2021 copies it.
            if (
                settings.Reuse == CadQLeaderReuse.ReuseCurrent
                && settings.ReusedHandle != null
                && Took(c.Document.FindEntity(settings.ReusedHandle), words)
            )
            {
                yield break;
            }

            switch (settings.Annotation)
            {
                case CadQLeaderAnnotation.None:
                    yield break;
                case CadQLeaderAnnotation.Tolerance:
                    c.Write(
                        "Tolerance needs the Geometric Tolerance dialog, which the presenter has not: the leader has no annotation."
                    );
                    yield break;
                case CadQLeaderAnnotation.CopyObject:
                    while (true)
                    {
                        yield return CadPrompt.ForEntity("Select an object to copy");

                        if (c.Input.Kind != CadInputKind.Selection)
                        {
                            words.Ended = true;
                            yield break;
                        }

                        if (Copied(c, words))
                        {
                            words.Middle = true;
                            yield break;
                        }
                    }
                case CadQLeaderAnnotation.BlockReference:
                    foreach (CadPrompt prompt in LeaderBlock(c, words))
                    {
                        yield return prompt;
                    }

                    words.Ended = words.Block == null;
                    yield break;
            }

            // The width is not kept from one leader to the next: AutoCAD 2021 offers nought each time, and takes one below
            // nought as nought. It asks none with Always left justify.
            if (settings.PromptForWidth && !settings.AlwaysLeftJustify)
            {
                yield return CadPrompt.ForDistance("Specify text width", points[points.Count - 1], null, Length(0.0));
                words.Width = c.Input.Kind == CadInputKind.Number && c.Input.Number > 0.0 ? c.Input.Number : 0.0;

                if (!c.Input.IsNone && c.Input.Kind != CadInputKind.Number)
                {
                    words.Ended = true;
                    yield break;
                }
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
                words.Text = LeaderText(c, lines);
                words.Typed = true;
            }
            else
            {
                words.Ended = true;
            }
        }

        /// <summary>
        /// A point of a QLEADER segment held to an angle constraint, as AutoCAD 2021 holds it: the point picked brought square
        /// onto the nearest ray from the last point at a multiple of the constraint's angle (level for Horizontal), so that
        /// (100, 30) from the origin held level is (100, 0), and (50, 100) held to 45 degrees is (75, 75).
        /// </summary>
        private static GeoPoint2 HeldToAngle(GeoPoint2 from, GeoPoint2 picked, CadQLeaderAngle angle)
        {
            double step = CadQLeaderSettings.Increment(angle);
            GeoVector2 way = picked - from;

            if (step <= 0.0 || way.Length <= 0.0)
            {
                return picked;
            }

            double direction = Math.Atan2(way.Y, way.X);
            double held = Math.Round(direction / step, MidpointRounding.AwayFromZero) * step;
            double along = way.Length * Math.Cos(direction - held);
            return new GeoPoint2(from.X + along * Math.Cos(held), from.Y + along * Math.Sin(held));
        }

        /// <summary>
        /// The preview of a leader at "Specify next point:": the leader through the points picked and the cursor, held to its
        /// angle constraint as the point will be, its arrowhead and its line or spline as the leader made there draws them,
        /// with no rubber band of its own, so that the segment held level from the origin with the cursor at (100, 30) runs to
        /// (100, 0).
        /// </summary>
        private static CadPreview LeaderPreview(
            CadCommandContext c,
            List<GeoPoint2> picked,
            bool spline,
            bool arrow,
            CadQLeaderAngle angle
        )
        {
            var points = new List<GeoPoint2>(picked);
            GeoPoint2 last = points[points.Count - 1];
            return CadPreview.Making(c, input =>
            {
                GeoPoint2 at = HeldToAngle(last, input.Point, angle);

                if ((at - last).Length <= c.Session.Tolerance.EqualPoint)
                {
                    return null;
                }

                var vertices = new List<GeoPoint2>(points) { at };
                return LeaderParts(c, vertices, spline, arrow);
            });
        }

        /// <summary>
        /// The parts a leader through vertices breaks into, as <see cref="DxfLeader.Explode"/> gives them for the leader made
        /// with the current settings: the arrowhead at the first vertex, DIMASZ times the scale long, a SOLID for closed
        /// filled or an INSERT of the DIMLDRBLK block, then the line or spline from the arrow's back where the first segment
        /// is longer than the arrow; each in the current layer and colour.
        /// </summary>
        private static List<DxfEntity> LeaderParts(
            CadCommandContext c,
            List<GeoPoint2> vertices,
            bool spline,
            bool arrow
        )
        {
            double tolerance = Tolerance.Default.EqualPoint;
            double size = c.Database.Dimasz * LeaderScale(c);
            var line = vertices.ConvertAll(P3);
            var parts = new List<DxfEntity>();

            if (arrow && line[0].DistanceTo(line[1]) > tolerance)
            {
                GeoVector3 away = DxfOcsTransform.Normalized(line[1].GetVectorTo(line[0]));
                DxfBlockTableRecord block = c.Database.Dimldrblk;
                DxfEntity head;

                if (block == null)
                {
                    GeoPoint3 back = line[0].Add(away.Multiply(-size));
                    GeoVector3 side = new GeoVector3(-away.Y, away.X, 0.0).Multiply(size / 6.0);
                    head = new DxfSolid(back.Add(side), line[0], back.Add(side.Negate()));
                }
                else
                {
                    head = new DxfBlockReference(line[0], block)
                    {
                        ScaleFactors = new GeoVector3(size, size, size),
                        Rotation = Math.Atan2(away.Y, away.X),
                    };
                }

                parts.Add(Current(c, head));

                if (line[0].DistanceTo(line[1]) > size + tolerance)
                {
                    line[0] = line[0].Add(away.Multiply(-size));
                }
            }

            var shape = new DxfLeader();

            foreach (GeoPoint3 vertex in line)
            {
                shape.AppendVertex(vertex);
            }

            shape.HasArrowHead = false;
            shape.IsSplined = spline;

            foreach (DxfEntity part in shape.Explode())
            {
                parts.Add(Current(c, part));
            }

            return parts;
        }

        /// <summary>
        /// Settings of QLEADER: the Leader Settings dialog shown over the drawing's settings and DIMLDRBLK, OK writing them as
        /// one undo step; with no window, a line saying so, QLEADER drawing by the settings the drawing has.
        /// </summary>
        private static void LeaderSettingsDialog(CadCommandContext c)
        {
            DxfDatabase db = c.Database;
            var request = new CadQLeaderRequest(
                c.Document,
                CadQLeaderSettings.Read(db),
                CadArrowheads.NameOf(db.Dimldrblk)
            );

            if (!c.Session.Request("QLEADER", c.Document, request))
            {
                c.Write("Leader Settings is shown by the window: QLEADER draws by the settings the drawing has.");
                return;
            }

            if (request.Action != CadDialogAction.Ok)
            {
                return;
            }

            CadEditResult result = c.Apply(
                new[] { new CadEditRecord(CadEditRecordKind.SettingChanged, null, "Leader settings", null, "QLEADER") },
                s =>
                {
                    request.Settings.Write(s.Database);
                    DxfBlockTableRecord arrow = CadArrowheads.Ensure(s.Database, request.Arrow);

                    if (!string.Equals(arrow?.Name, s.Database.Dimldrblk?.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        s.Database.Dimldrblk = arrow;
                    }
                }
            );

            if (!result.Succeeded)
            {
                c.Write(result.Message);
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
        /// Copy of LEADER and QLEADER: the object picked taken into <paramref name="words"/> as <see cref="Took"/> takes it;
        /// false for a miss or another kind of object.
        /// </summary>
        private static bool Copied(CadCommandContext c, LeaderWords words) =>
            c.Input.Kind == CadInputKind.Selection
            && c.Input.Handles.Count > 0
            && Took(c.Document.FindEntity(c.Input.Handles[0]), words);

        /// <summary>
        /// An object a leader copies, as AutoCAD 2021's Copy and Reuse Current copy it: an MTEXT or a TEXT as the MTEXT of
        /// <see cref="CopyOfText"/>, an INSERT by its handle, its copy made with the leader; false, nothing taken, for any
        /// other object (a TOLERANCE among them, which the library cannot move) or none.
        /// </summary>
        private static bool Took(DxfEntity picked, LeaderWords words)
        {
            if (picked is DxfBlockReference insert && !(picked is DxfMInsertBlock) && !insert.IsErased)
            {
                words.Copy = insert.Handle;
                return true;
            }

            DxfMText text = CopyOfText(picked);

            if (text == null)
            {
                return false;
            }

            words.Text = text;
            return true;
        }

        /// <summary>An MTEXT copying an MTEXT, or the words, height and style of a TEXT; null for any other object or none.</summary>
        private static DxfMText CopyOfText(DxfEntity picked)
        {
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
        /// bottom DIMGAP above it with DIMTAD not nought); QLEADER by its Attachment settings, the middle of their top line on the
        /// right and of their bottom line on the left by default, and a copy of Copy an Object by its middle. Words typed take
        /// DIMCLRT where it is not BYBLOCK. A copy of an INSERT stands at the end with no hook. In a UCS other than the world's,
        /// as AutoCAD 2021 draws it, the last segment is measured along the UCS's axes, the hook gets no vertex of its own, and
        /// the words run along its X, DIMGAP along it from the end: up its Y for LEADER's DIMTAD, up the world's Y for QLEADER.
        /// </summary>
        private static void MakeLeader(
            CadCommandContext c,
            List<GeoPoint2> points,
            bool spline,
            bool arrow,
            LeaderWords words,
            bool quick,
            double width,
            CadQLeaderSettings settings = null
        )
        {
            DxfDatabase db = c.Database;
            DxfDimStyleTableRecord style = NamedDimStyle(c);
            double scale = LeaderScale(c);
            double gap = db.Dimgap * scale;
            int tad = quick ? 0 : db.Dimtad;
            DxfMText text = words?.Text;
            settings = settings ?? new CadQLeaderSettings();
            bool byAttachment = quick && words?.Middle != true;
            bool leftJustified = false;

            // In a UCS other than the world's the last segment is measured along its axes, as AutoCAD 2021 measures it.
            CadUcs ucs = c.Session.Ucs;
            bool world = ucs.IsWorld;
            GeoPoint2 end = points[points.Count - 1];
            GeoVector2 way = end - points[points.Count - 2];
            GeoVector2 along = world ? way : ucs.ToUcs(way);
            bool left = along.X < -c.Session.Tolerance.EqualPoint;
            double side = left ? -1.0 : 1.0;
            bool hook = text != null && !spline && Math.Abs(along.Y) > LeaderHookSlope * Math.Abs(along.X);
            var vertices = new List<GeoPoint2>(points);

            // AutoCAD 2021 adds the hook's own vertex in the world alone: in any other UCS, a moved one too, the leader keeps
            // the points picked and only says it has a hook.
            if (hook && world)
            {
                vertices.Insert(vertices.Count - 1, new GeoPoint2(end.X - side * db.Dimasz * scale, end.Y));
            }

            GeoVector2 xAxis = ucs.ToWorld(new GeoVector2(1.0, 0.0));
            GeoVector2 yAxis = ucs.ToWorld(new GeoVector2(0.0, 1.0));

            // A point of the words: along the UCS's X from the end, then up its Y, or up the world's Y where QLEADER raises
            // them, as AutoCAD 2021 does in a turned UCS.
            GeoPoint3 Beside(double alongX, double up, bool upWorld)
            {
                if (world)
                {
                    return new GeoPoint3(end.X + alongX, end.Y + up, 0.0);
                }

                GeoVector2 rise = upWorld ? new GeoVector2(0.0, up) : yAxis * up;
                return new GeoPoint3(end.X + xAxis.X * alongX + rise.X, end.Y + xAxis.Y * alongX + rise.Y, 0.0);
            }

            if (text != null && !world)
            {
                text.Direction = new GeoVector3(xAxis.X, xAxis.Y, 0.0);
            }

            if (text != null && byAttachment && settings.Underline)
            {
                // Underline bottom line: the words stand DIMGAP above the leader's end, which runs on under their last line.
                text.Location = Beside(side * gap, gap, true);
                text.Attachment = left ? DxfAttachmentPoint.BottomRight : DxfAttachmentPoint.BottomLeft;
            }
            else if (text != null && byAttachment)
            {
                CadQLeaderAttachment row = left ? settings.LeftSide : settings.RightSide;
                leftJustified = left && settings.AlwaysLeftJustify;
                double half = text.TextHeight / 2.0;
                double rise =
                    row == CadQLeaderAttachment.MiddleOfTopLine ? half
                    : row == CadQLeaderAttachment.MiddleOfBottomLine ? -half
                    : 0.0;
                text.Location = Beside(side * gap, rise, true);
                text.Attachment = QuickAttachment(row, !left || leftJustified);
            }
            else if (text != null)
            {
                text.Location = Beside(side * gap, tad != 0 ? gap : 0.0, false);
                text.Attachment =
                    tad != 0 ? (left ? DxfAttachmentPoint.BottomRight : DxfAttachmentPoint.BottomLeft)
                    : left ? DxfAttachmentPoint.MiddleRight
                    : DxfAttachmentPoint.MiddleLeft;
            }

            if (text != null)
            {
                if (width > 0.0)
                {
                    text.Width = width;
                }
            }

            CadSpace space = c.Space;
            string block = words?.Block;
            string copied = words?.Copy;
            bool typed = words?.Typed == true;
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

                Override(leader, style, db, scale, quick, settings);
                double height = 0.0;
                double across = 0.0;

                if (text != null)
                {
                    s.Append(space, Current(c, text));

                    // Words typed take DIMCLRT, as AutoCAD 2021 colours them, the current colour where it is BYBLOCK.
                    if (typed && db.Dimclrt != null && !db.Dimclrt.IsByBlock)
                    {
                        text.Color = db.Dimclrt;
                    }

                    leader.Annotation = text;
                    CadMTextLayout layout = CadMTextEdit.LayoutOf(text);
                    height = layout?.ActualHeight ?? 0.0;
                    across = layout?.ActualWidth ?? 0.0;

                    // Always left justify on the left: the words' left edge set back their width, their right DIMGAP short of the end.
                    if (leftJustified)
                    {
                        GeoPoint3 at = text.Location;
                        text.Location = world
                            ? new GeoPoint3(at.X - across, at.Y, at.Z)
                            : new GeoPoint3(at.X - xAxis.X * across, at.Y - xAxis.Y * across, at.Z);
                    }

                    // Reuse Next: these words are the ones reused from now on, the settings turned to Reuse Current.
                    if (quick && settings.Reuse == CadQLeaderReuse.ReuseNext)
                    {
                        CadQLeaderSettings kept = settings.Clone();
                        kept.Reuse = CadQLeaderReuse.ReuseCurrent;
                        kept.ReusedHandle = text.Handle;
                        kept.Write(s.Database);
                    }
                }
                else if (block != null)
                {
                    leader.Annotation = s.Document.FindEntity(block);
                }
                else if (copied != null && s.Document.FindEntity(copied) is DxfBlockReference source)
                {
                    leader.Annotation = CopyAtEnd(s, space, source, P3(end), ucs.Angle);
                }

                // AutoCAD 2021 writes 74 of 1 on a leader of no annotation too.
                leader.SetMade(hook, text != null ? left : block == null && copied == null, height, across);
            });
        }

        /// <summary>
        /// The attachment of QLEADER's words by the row of the Attachment tab, on the left column (words on the leader's right,
        /// or left justified) or the right: the top rows hang from the top, the middle one from the middle, the bottom rows
        /// stand on the bottom, as AutoCAD 2021 sets them.
        /// </summary>
        private static DxfAttachmentPoint QuickAttachment(CadQLeaderAttachment row, bool leftColumn)
        {
            switch (row)
            {
                case CadQLeaderAttachment.TopOfTopLine:
                case CadQLeaderAttachment.MiddleOfTopLine:
                    return leftColumn ? DxfAttachmentPoint.TopLeft : DxfAttachmentPoint.TopRight;
                case CadQLeaderAttachment.MiddleOfText:
                    return leftColumn ? DxfAttachmentPoint.MiddleLeft : DxfAttachmentPoint.MiddleRight;
                default:
                    return leftColumn ? DxfAttachmentPoint.BottomLeft : DxfAttachmentPoint.BottomRight;
            }
        }

        /// <summary>
        /// Adds a copy of an INSERT as the words of a leader, as AutoCAD 2021's Copy and Reuse Current copy one: its insertion
        /// point at the leader's end, its scale kept, turned to the X axis of the UCS (0 in the world, 30 degrees in a UCS
        /// turned 30), so that an INSERT at (500, 500) scaled 1.5 by 2.5 and turned 0.3 is copied to (10, 320) scaled 1.5 by
        /// 2.5 and turned 0.
        /// </summary>
        private static DxfEntity CopyAtEnd(
            CadEditScope s,
            CadSpace space,
            DxfBlockReference source,
            GeoPoint3 end,
            double angle
        )
        {
            GeoVector3 normal = source.Normal;
            double turn = (normal.Z < 0.0 ? -angle : angle) - source.Rotation;
            GeoTransform3 move =
                GeoTransform3.Translation(end - source.Position)
                * GeoTransform3.RotationAxis(source.Position, normal, turn);
            DxfEntity copy = source.GetTransformedCopy(move);
            CadOwnerTag.Remove(copy);
            DxfEntity added = s.Append(space, copy);

            if (added is DxfBlockReference made)
            {
                made.KeepRepresentationOf(source);
            }

            return added;
        }

        // The dimension variables in the order of their groups, as AutoCAD 2021 writes a leader's overrides; the name breaks a tie.
        private static readonly DxfDimVariable[] VariablesByGroup = DxfDimVariable
            .All.OrderBy(v => v.Code)
            .ThenBy(v => v.Name, StringComparer.Ordinal)
            .ToArray();

        /// <summary>
        /// Writes every current dimension variable the leader is drawn by where it is not its style's, as AutoCAD 2021 writes
        /// them, in the order of their groups: DIMSCALE (40) as the scale the leader is drawn by, DIMASZ (41), DIMEXO (42),
        /// DIMTAD (77), DIMTXT (140), DIMGAP (147), DIMCLRD (176), DIMCLRT (178), DIMLDRBLK (341, <c>1005</c> and the handle of
        /// <c>_Dot</c>), DIMLWD (371) and each other. QLEADER writes DIMGAP and DIMTAD first whatever the style has, and moves
        /// each other than the style's to its place among the rest: DIMGAP below nought with Frame text, DIMTAD 1 with
        /// Underline bottom line, else nought. A variable the drawing's version cannot hold is left out, as SAVEAS leaves it.
        /// </summary>
        private static void Override(
            DxfLeader leader,
            DxfDimStyleTableRecord style,
            DxfDatabase db,
            double scale,
            bool quick,
            CadQLeaderSettings settings
        )
        {
            double gap = settings.FrameText ? -db.Dimgap : db.Dimgap;
            double tad = settings.Underline ? 1.0 : 0.0;

            if (quick)
            {
                leader.WriteOverride(147, gap, false);
                leader.WriteOverride(77, tad, true);
            }

            DxfDimStyleTableRecord current = db.GetDimstyleData();

            foreach (DxfDimVariable variable in VariablesByGroup)
            {
                object wanted = variable.Code == 40 ? scale : current.Get(variable);
                object own =
                    style != null ? style.Get(variable)
                    : variable.IsReference ? null
                    : variable.Read(null);

                if (wanted == null && variable.Kind == DxfDimKind.TextStyle || variable.SameValue(wanted, own))
                {
                    continue;
                }

                if (quick && (variable.Code == 147 || variable.Code == 77))
                {
                    leader.WriteOverride(variable.Code, variable.Code == 147 ? gap : tad, variable.Code == 77);
                    continue;
                }

                // One variable at a time, so that each is written last, in the order of the groups.
                DxfDimStyleTableRecord one = leader.GetDimstyleData();
                one.Keep(variable, wanted);

                try
                {
                    leader.SetDimstyleData(one);
                }
                catch (Exception e) when (e is ArgumentException || e is InvalidOperationException)
                {
                    GeometryHelperLog.Debug(
                        "LEADER: the override of " + variable.Name.ToUpperInvariant() + " is left out: " + e.Message
                    );
                }
            }
        }
    }
}
