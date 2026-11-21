using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Cad
{
    /// <summary>The edit commands: MOVE, COPY, ERASE, ROTATE, SCALE, MIRROR, EXPLODE, MATCHPROP, LAYER, UNDO, U, REDO.</summary>
    internal static partial class CadCommands
    {
        private static readonly GeoVector3 Up = new GeoVector3(0.0, 0.0, 1.0);

        private static IEnumerable<CadPrompt> Move(CadCommandContext c)
        {
            foreach (CadPrompt prompt in c.SelectObjects())
            {
                yield return prompt;
            }

            if (c.Selected.Count == 0)
            {
                yield break;
            }

            var move = new Ref<GeoVector2?>();

            foreach (CadPrompt prompt in Displacement(c, move))
            {
                yield return prompt;
            }

            if (move.Value.HasValue)
            {
                Transform(
                    c,
                    c.Selected,
                    GeoTransform3.Translation(new GeoVector3(move.Value.Value.X, move.Value.Value.Y, 0.0)),
                    false,
                    "Move",
                    Values(move.Value.Value.X, move.Value.Value.Y)
                );
            }
        }

        private static IEnumerable<CadPrompt> Copy(CadCommandContext c)
        {
            foreach (CadPrompt prompt in c.SelectObjects())
            {
                yield return prompt;
            }

            if (c.Selected.Count == 0)
            {
                yield break;
            }

            // COPYMODE: 0, the default, copies again and again until Exit; 1 makes one copy. Typing Multiple for one run does not
            // change it, mOde does.
            bool setSingle = c.Remembered(CopyModeKey, 0.0) != 0.0;
            bool single = setSingle;
            c.Write("Current settings:  Copy mode = " + (single ? "Single" : "Multiple"));
            c.Remember(CopyArrayKey, 0.0);

            // Run by a routine's command, AutoCAD makes the one copy and ends (runs/hhh: the next command of the routine is not
            // taken as an answer to Specify second point).
            single |= c.Session.RunsForLisp;

            while (true)
            {
                yield return CadPrompt.ForPoint(
                    "Specify base point",
                    null,
                    setSingle ? new[] { "Displacement", "mOde", "Multiple" } : new[] { "Displacement", "mOde" },
                    "Displacement"
                );

                if (c.Input.Is("mOde"))
                {
                    yield return CadPrompt.ForKeyword(
                        "Enter a copy mode option",
                        new[] { "Single", "Multiple" },
                        single ? "Single" : "Multiple"
                    );

                    if (c.Input.Is("Single") || c.Input.Is("Multiple"))
                    {
                        single = c.Input.Is("Single");
                        setSingle = single;
                        c.Remember(CopyModeKey, single ? 1.0 : 0.0);
                    }

                    continue;
                }

                if (c.Input.Is("Multiple"))
                {
                    single = false;
                    continue;
                }

                break;
            }

            if (!c.Input.IsPoint)
            {
                var once = new Ref<GeoVector2?>();
                GeoVector2 last = new GeoVector2(
                    c.Remembered("displacement.x", 0.0),
                    c.Remembered("displacement.y", 0.0)
                );
                yield return CadPrompt.ForPoint(
                    "Specify displacement",
                    null,
                    null,
                    Length(last.X) + ", " + Length(last.Y) + ", " + Length(0.0)
                );
                once.Value = c.Input.IsPoint ? AsDisplacement(c, c.Input.Point) : last;
                Remember(c, once.Value.Value);
                Transform(
                    c,
                    c.Selected,
                    GeoTransform3.Translation(new GeoVector3(once.Value.Value.X, once.Value.Value.Y, 0.0)),
                    true
                );
                yield break;
            }

            GeoPoint2 from = c.Input.Point;
            var placed = new List<List<string>>();
            yield return CadPrompt.ForPoint(
                "Specify second point",
                from,
                new[] { "Array" },
                "use first point as displacement"
            );

            if (c.Input.IsNone)
            {
                placed.Add(
                    Transform(
                        c,
                        c.Selected,
                        GeoTransform3.Translation(
                            new GeoVector3(AsDisplacement(c, from).X, AsDisplacement(c, from).Y, 0.0)
                        ),
                        true
                    )
                );
                yield break;
            }

            while (true)
            {
                if (c.Input.IsPoint)
                {
                    GeoVector2 move = c.Input.Point - from;
                    placed.Add(
                        Transform(c, c.Selected, GeoTransform3.Translation(new GeoVector3(move.X, move.Y, 0.0)), true)
                    );

                    if (single)
                    {
                        yield break;
                    }
                }
                else if (c.Input.Is("Array"))
                {
                    var items = new List<string>();

                    foreach (CadPrompt prompt in CopyArray(c, from, items))
                    {
                        yield return prompt;
                    }

                    if (items.Count > 0)
                    {
                        placed.Add(items);
                    }

                    if (single)
                    {
                        yield break;
                    }
                }
                else if (c.Input.Is("Undo"))
                {
                    if (placed.Count > 0)
                    {
                        EraseAll(c, placed[placed.Count - 1]);
                        placed.RemoveAt(placed.Count - 1);
                    }
                }
                else
                {
                    yield break;
                }

                yield return CadPrompt.ForPoint(
                    "Specify second point",
                    from,
                    new[] { "Array", "Exit", "Undo" },
                    "Exit"
                );

                if (c.Input.Is("Exit"))
                {
                    yield break;
                }
            }
        }

        private static IEnumerable<CadPrompt> Erase(CadCommandContext c)
        {
            foreach (CadPrompt prompt in c.SelectObjects())
            {
                yield return prompt;
            }

            // In the Block Editor the parameters and actions selected go too, with what hangs on them.
            CadBlockEditor editor = c.Document.BlockEditor;

            if (editor != null && !editor.IsCurrentDrawing)
            {
                List<string> elements = c.RawSelected.Where(h => editor.ElementOf(h) != null).ToList();

                if (elements.Count > 0)
                {
                    c.OpenUndoStep();
                    CadEditResult erased = c.Document.EraseBlockElements(elements);

                    if (!erased.Succeeded)
                    {
                        c.Write(erased.Message);
                    }
                }
            }

            if (c.Selected.Count > 0)
            {
                RememberErased(c, c.Selected);
                EraseAll(c, c.Selected);
            }
        }

        private static IEnumerable<CadPrompt> Rotate(CadCommandContext c)
        {
            c.Write(
                "Current positive angle in UCS:  ANGDIR="
                    + (c.Database.Angdir ? "clockwise" : "counterclockwise")
                    + "  ANGBASE="
                    + Number(c.Database.Angbase * 180.0 / Math.PI)
            );

            foreach (CadPrompt prompt in c.SelectObjects())
            {
                yield return prompt;
            }

            if (c.Selected.Count == 0)
            {
                yield break;
            }

            yield return CadPrompt.ForPoint("Specify base point");

            if (!c.Input.IsPoint)
            {
                yield break;
            }

            GeoPoint2 center = c.Input.Point;
            bool copy = false;
            double last = c.Remembered("rotate", 0.0);

            double angle;

            // The objects turned about the base point by an angle, as they are turned once it is given: the angle taken from nought
            // up to a whole turn first, as AutoCAD 2021 takes it (a turn of 360 degrees is none, -90 is 270), which it writes into
            // a hatch's pattern lines as it does.
            GeoTransform3 Turned(double by)
            {
                double turn = by % (2.0 * Math.PI);
                return GeoTransform3.RotationAxis(P3(center), Up, turn < 0.0 ? turn + 2.0 * Math.PI : turn);
            }

            while (true)
            {
                yield return CadPrompt
                    .ForAngle("Specify rotation angle", center, new[] { "Copy", "Reference" }, Counted(c, last))
                    .Turning()
                    .Previewing(
                        CadPreview.MovingOne(c, c.Selected, input => CadPreview.Flat(Turned(input.Number)), true, !copy)
                    );

                if (c.Input.Is("Copy"))
                {
                    copy = true;
                    c.Write("Rotating a copy of the selected objects.");
                    continue;
                }

                if (c.Input.Is("Reference"))
                {
                    // The objects turned from a reference angle to a new one, each typed or given by two points.
                    var reference = new Ref<double?>();

                    foreach (
                        CadPrompt prompt in Measured(c, "Specify the reference angle", Counted(c, 0.0), true, reference)
                    )
                    {
                        yield return prompt;
                    }

                    if (!reference.Value.HasValue)
                    {
                        yield break;
                    }

                    double from = reference.Value.Value;
                    yield return CadPrompt
                        .ForAngle("Specify the new angle", center, new[] { "Points" }, Counted(c, 0.0))
                        .Previewing(
                            CadPreview.MovingOne(
                                c,
                                c.Selected,
                                input => CadPreview.Flat(Turned(input.Number - from)),
                                true,
                                !copy
                            )
                        );
                    var target = new Ref<double?>();

                    if (c.Input.Is("Points"))
                    {
                        foreach (
                            CadPrompt prompt in TwoPoints(
                                c,
                                true,
                                target,
                                value => CadPreview.Flat(Turned(value - from)),
                                !copy
                            )
                        )
                        {
                            yield return prompt;
                        }
                    }
                    else
                    {
                        target.Value = c.Input.Kind == CadInputKind.Number ? c.Input.Number : 0.0;
                    }

                    if (!target.Value.HasValue)
                    {
                        yield break;
                    }

                    angle = target.Value.Value - reference.Value.Value;
                    break;
                }

                angle = c.Input.Kind == CadInputKind.Number ? c.Input.Number : last;
                break;
            }

            c.Remember("rotate", angle);
            Transform(c, c.Selected, Turned(angle), copy, "Rotate", Values(center.X, center.Y, angle));
        }

        private static IEnumerable<CadPrompt> Scale(CadCommandContext c)
        {
            foreach (CadPrompt prompt in c.SelectObjects())
            {
                yield return prompt;
            }

            if (c.Selected.Count == 0)
            {
                yield break;
            }

            yield return CadPrompt.ForPoint("Specify base point");

            if (!c.Input.IsPoint)
            {
                yield break;
            }

            GeoPoint2 center = c.Input.Point;
            bool copy = false;
            double last = c.Remembered("scale", 1.0);
            double factor;

            // The objects scaled about the base point by a factor, as they are scaled once it is given; none for a factor of nought.
            Affine2? Scaled(double by) =>
                by > 0.0 ? CadPreview.Flat(GeoTransform3.Scaling(P3(center), by)) : (Affine2?)null;

            while (true)
            {
                // AutoCAD 2021 shows no default here, and Enter takes the factor used last (1 at first).
                yield return CadPrompt
                    .ForDistance("Specify scale factor", center, new[] { "Copy", "Reference" })
                    .Previewing(CadPreview.MovingOne(c, c.Selected, input => Scaled(input.Number), true, !copy));

                if (c.Input.Is("Copy"))
                {
                    copy = true;
                    c.Write("Scaling a copy of the selected objects.");
                    continue;
                }

                if (c.Input.Is("Reference"))
                {
                    // The objects scaled so that a reference length becomes a new one, each typed or given by two points.
                    var reference = new Ref<double?>();

                    foreach (CadPrompt prompt in Measured(c, "Specify reference length", Length(1.0), false, reference))
                    {
                        yield return prompt;
                    }

                    if (!reference.Value.HasValue)
                    {
                        yield break;
                    }

                    double length = reference.Value.Value;
                    yield return CadPrompt
                        .ForDistance("Specify new length", center, new[] { "Points" }, Length(1.0))
                        .Previewing(
                            CadPreview.MovingOne(
                                c,
                                c.Selected,
                                input => length > c.Session.Tolerance.EqualPoint ? Scaled(input.Number / length) : null,
                                true,
                                !copy
                            )
                        );
                    var target = new Ref<double?>();

                    if (c.Input.Is("Points"))
                    {
                        foreach (
                            CadPrompt prompt in TwoPoints(
                                c,
                                false,
                                target,
                                value => length > c.Session.Tolerance.EqualPoint ? Scaled(value / length) : null,
                                !copy
                            )
                        )
                        {
                            yield return prompt;
                        }
                    }
                    else
                    {
                        target.Value = c.Input.Kind == CadInputKind.Number ? c.Input.Number : 1.0;
                    }

                    if (!target.Value.HasValue)
                    {
                        yield break;
                    }

                    if (!(reference.Value.Value > c.Session.Tolerance.EqualPoint))
                    {
                        c.Write("The reference length must be above nought.");
                        continue;
                    }

                    factor = target.Value.Value / reference.Value.Value;
                }
                else
                {
                    factor = c.Input.Kind == CadInputKind.Number ? c.Input.Number : last;
                }

                if (factor > 0.0)
                {
                    break;
                }

                c.Write("Value must be positive and nonzero.");
            }

            c.Remember("scale", factor);
            Transform(
                c,
                c.Selected,
                GeoTransform3.Scaling(P3(center), factor),
                copy,
                "Scale",
                Values(center.X, center.Y, factor)
            );
        }

        private static IEnumerable<CadPrompt> Mirror(CadCommandContext c)
        {
            foreach (CadPrompt prompt in c.SelectObjects())
            {
                yield return prompt;
            }

            if (c.Selected.Count == 0)
            {
                yield break;
            }

            yield return CadPrompt.ForPoint("Specify first point of mirror line");

            if (!c.Input.IsPoint)
            {
                yield break;
            }

            GeoPoint2 first = c.Input.Point;
            GeoPoint2 second;

            // The objects mirrored across the line from the first point, as they are once the second is given.
            GeoTransform3 Mirrored(GeoPoint2 end)
            {
                GeoVector2 way = end - first;
                return GeoTransform3.Mirror(new GeoPlane3(P3(first), new GeoVector3(-way.Y, way.X, 0.0).Normalize()));
            }

            while (true)
            {
                yield return CadPrompt
                    .ForPoint("Specify second point of mirror line", first)
                    .Previewing(
                        CadPreview.MovingOne(
                            c,
                            c.Selected,
                            input =>
                                (input.Point - first).Length > c.Session.Tolerance.EqualPoint
                                    ? CadPreview.Flat(Mirrored(input.Point))
                                    : (Affine2?)null,
                            true
                        )
                    );

                if (!c.Input.IsPoint)
                {
                    yield break;
                }

                second = c.Input.Point;

                if ((second - first).Length > c.Session.Tolerance.EqualPoint)
                {
                    break;
                }

                c.Write("Invalid point: the mirror line needs two points apart.");
            }

            yield return CadPrompt.ForKeyword("Erase source objects?", new[] { "Yes", "No" }, "No");
            GeoTransform3 mirror = Mirrored(second);

            // MIRRTEXT 0, AutoCAD's default, keeps texts readable; 1 mirrors them with the rest, in their plane. A dimension
            // stays readable under either, as AutoCAD keeps it; a multileader keeps its plane under either; an INSERT is written
            // with its x factor turned the other way, as AutoCAD writes it.
            CadDocument document = c.Document;
            Tolerance tolerance = c.Session.Tolerance;
            bool mirrtext = c.Database.Mirrtext;

            // MIRRHATCH 0, AutoCAD's default, keeps the pattern of a hatch as it was; 1 mirrors it with the rest.
            bool mirrhatch = c.Remembered("mirrhatch", 0.0) >= 0.5;
            Func<DxfEntity, ReadableMirror> readable = entity =>
                entity is DxfHatch ? ReadableHatch(mirror, mirrhatch)
                : entity is DxfDimension dimension ? ReadableDimension(dimension, mirror, tolerance)
                : entity is DxfMLeader mleader ? MLeaderMirror(document, mleader, mirror, mirrtext, tolerance)
                : entity is DxfBlockReference insert ? InsertMirror(document, insert, mirror, mirrtext, tolerance)
                : entity is DxfTolerance ? (mirrtext ? null : ReadableTolerance(mirror))
                : mirrtext ? (InPlane(entity, mirror, tolerance) ?? InOwnPlane(entity, mirror))
                : Readable(document, entity, mirror, tolerance);

            Transform(
                c,
                c.Selected,
                mirror,
                !c.Input.Is("Yes"),
                "Mirror",
                Values(first.X, first.Y, second.X, second.Y),
                readable
            );
        }

        /// <summary>The pieces an associative array explodes into; null for any other entity.</summary>
        private static IReadOnlyList<DxfEntity> ExplodedArray(CadCommandContext c, DxfEntity entity)
        {
            if (
                !(entity is DxfBlockReference insert)
                || !(CadArrays.Find(c.Database, insert) is CadArrays.Record record)
            )
            {
                return null;
            }

            List<DxfEntity> pieces = CadArrays.Explode(c.Document, record, insert);
            return pieces.Count == 0 ? null : pieces;
        }

        private static IEnumerable<CadPrompt> Explode(CadCommandContext c)
        {
            foreach (CadPrompt prompt in c.SelectObjects())
            {
                yield return prompt;
            }

            var parts = new List<KeyValuePair<string, IReadOnlyList<DxfEntity>>>();
            var styles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            int refused = 0;
            int minserted = 0;

            foreach (string handle in c.Selected)
            {
                DxfEntity entity = c.Document.FindEntity(handle);
                IReadOnlyList<DxfEntity> pieces = null;

                // A MINSERT is not exploded, as AutoCAD 2021's EXPLODE says: "1 was minserted."
                if (entity is DxfMInsertBlock)
                {
                    minserted++;
                    continue;
                }

                // An entity of an owner is not exploded: its owner would take the erase as its own and the pieces be lost.
                if (entity != null && c.Document.Owners.GetEditRule(entity) != CadEditRule.Free)
                {
                    c.Write(
                        "This "
                            + (entity is DxfDimension ? "dimension" : CadOwnerEditGate.Describe(entity))
                            + " belongs to the model and cannot be exploded here."
                    );
                    continue;
                }

                // A block kept from exploding (Allow exploding cleared) is refused, as AutoCAD's EXPLODE refuses it.
                if (
                    entity is DxfBlockReference insert
                    && insert.BlockTableRecord != null
                    && !insert.BlockTableRecord.Explodable
                )
                {
                    refused++;
                    continue;
                }

                // A feature control frame is refused by name, as AutoCAD 2021's EXPLODE refuses it (probe GHTQ1).
                if (entity is DxfTolerance)
                {
                    c.Write("Cannot explode TOLERANCE");
                    continue;
                }

                try
                {
                    // An MTEXT breaks into its TEXTs where the picture draws them; one in no drawing as the library breaks it.
                    // An associative array breaks into the source objects of each item, not into the INSERTs of its block.
                    pieces = ExplodedArray(c, entity)
                        ?? (entity is DxfMText words ? CadMTextEdit.Explode(words, styles) : null)
                        ?? entity.Explode();
                }
                catch (NotSupportedException)
                {
                    pieces = null;
                }
                catch (InvalidOperationException)
                {
                    pieces = null;
                }

                if (pieces == null || pieces.Count == 0)
                {
                    refused++;
                    continue;
                }

                parts.Add(new KeyValuePair<string, IReadOnlyList<DxfEntity>>(handle, pieces));
            }

            if (minserted > 0)
            {
                c.Write(
                    minserted.ToString(CultureInfo.InvariantCulture)
                        + (minserted == 1 ? " was" : " were")
                        + " minserted."
                );
            }

            if (refused > 0)
            {
                c.Write(
                    refused.ToString(CultureInfo.InvariantCulture)
                        + (refused == 1 ? " was" : " were")
                        + " not able to be exploded."
                );
            }

            if (parts.Count == 0)
            {
                yield break;
            }

            var planned = new List<CadEditRecord>();

            foreach (KeyValuePair<string, IReadOnlyList<DxfEntity>> pair in parts)
            {
                planned.Add(new CadEditRecord(CadEditRecordKind.Erased, pair.Key));
            }

            c.Apply(planned, s =>
            {
                // The styles of the runs of an MTEXT in SHX fonts of their own, before the TEXTs in them.
                CadMTextEdit.AddExplodedStyles(s.Database, styles);

                foreach (KeyValuePair<string, IReadOnlyList<DxfEntity>> pair in parts)
                {
                    if (s.IsHandled(pair.Key))
                    {
                        continue;
                    }

                    CadSpace space = s.Document.FindSpace(pair.Key);
                    DxfEntity entity = s.Document.FindEntity(pair.Key);

                    foreach (DxfEntity piece in pair.Value)
                    {
                        s.Append(space, piece);
                    }

                    entity.Erase();
                }
            });
        }

        private static IEnumerable<CadPrompt> MatchProperties(CadCommandContext c)
        {
            yield return CadPrompt.ForEntity("Select source object");

            if (c.Input.Kind != CadInputKind.Selection)
            {
                yield break;
            }

            string source = c.Input.Handles[0];
            c.Write("Current active settings: " + CadPropertyMatcher.Describe(c.Session.MatchProperties));

            while (true)
            {
                yield return CadPrompt.ForSelection("Select destination object(s)", new[] { "Settings" });

                if (!c.Input.Is("Settings"))
                {
                    break;
                }

                // The window's Property Settings dialog, given the source's handle to show its values, sets the session's
                // choice; the command goes on with it.
                if (!c.Session.Request("MATCHPROP", c.Document, source))
                {
                    c.Write("The Property Settings dialog is shown by the window.");
                }

                c.Write("Current active settings: " + CadPropertyMatcher.Describe(c.Session.MatchProperties));
            }

            if (c.Input.Kind != CadInputKind.Selection)
            {
                yield break;
            }

            var targets = new List<string>();

            foreach (string handle in c.Input.Handles)
            {
                DxfEntity entity = c.Document.FindEntity(handle);

                if (entity == null || string.Equals(handle, source, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (CadLayerEditor.IsLocked(c.Database, entity.Layer))
                {
                    c.Write("1 was on a locked layer.");
                    continue;
                }

                targets.Add(handle);
            }

            if (targets.Count == 0)
            {
                yield break;
            }

            var planned = new List<CadEditRecord>();

            foreach (string handle in targets)
            {
                planned.Add(
                    new CadEditRecord(CadEditRecordKind.PropertyChanged, handle, "Match properties", null, source)
                );
            }

            CadMatchProperties which = c.Session.MatchProperties;
            c.Apply(planned, s =>
            {
                DxfEntity from = s.Document.FindEntity(source);

                foreach (string handle in targets)
                {
                    if (s.IsHandled(handle))
                    {
                        continue;
                    }

                    CadPropertyMatcher.Apply(s.Database, from, s.Document.FindEntity(handle), which);
                }
            });
        }

        private static IEnumerable<CadPrompt> Layer(CadCommandContext c)
        {
            if (c.Session.Request("LAYER"))
            {
                yield break;
            }

            foreach (CadPrompt prompt in LayerTyped(c))
            {
                yield return prompt;
            }
        }

        /// <summary>
        /// -LAYER: AutoCAD's options on the command line, stAte among them, until Enter; Make, Set, Rename, Color, Ltype and
        /// LWeight are asked as <see cref="LayerOption"/> asks them, and ?, MATerial, rEconcile and Xref, which AutoCAD lists and
        /// the presenter does not do, say so. A layer of an external reference
        /// (<c>grid|WALL</c>) is switched on and off, given a transparency, plotted or not, frozen, thawed, locked, unlocked
        /// and described, as AutoCAD 2021's -LAYER does, and Set refuses it in AutoCAD 2021's words: "Invalid layer name.".
        /// TRansparency and Plot ask their value first and offer the current layer, as AutoCAD 2021 asks "Enter transparency
        /// value (0-90):", then "Enter name list of layer(s) for transparency 50% &lt;0&gt;:".
        /// PStyle, offered where the drawing has named plot styles only, asks "Enter Plot Style Name or [?]" (a name the drawing's plot style
        /// names do not hold is "Unknown plot style."), then the layers.
        /// </summary>
        private static IEnumerable<CadPrompt> LayerTyped(CadCommandContext c)
        {
            var optionList = new List<string>
            {
                "?",
                "Make",
                "Set",
                "New",
                "Rename",
                "ON",
                "OFF",
                "Color",
                "Ltype",
                "LWeight",
                "TRansparency",
                "MATerial",
                "Plot",
                "Freeze",
                "Thaw",
                "LOck",
                "Unlock",
                "stAte",
                "Description",
                "rEconcile",
                "Xref",
            };

            // PStyle is offered only where the drawing has named plot styles, as AutoCAD 2021 offers it.
            if (!c.Database.PlotStyleMode)
            {
                optionList.Insert(optionList.IndexOf("Plot") + 1, "PStyle");
            }

            string[] options = optionList.ToArray();
            c.Write("Current layer:  \"" + c.Database.Clayer + "\"");

            while (true)
            {
                yield return CadPrompt.ForKeyword("Enter an option", options);

                if (c.Input.IsNone)
                {
                    yield break;
                }

                string option = c.Input.Text;
                string ask;

                if (option == "stAte")
                {
                    foreach (CadPrompt prompt in LayerStateTyped(c))
                    {
                        yield return prompt;
                    }

                    continue;
                }

                if (LayerOptionAsked(option))
                {
                    foreach (CadPrompt prompt in LayerOption(c, option))
                    {
                        yield return prompt;
                    }

                    continue;
                }

                int percent = 0;
                bool plotted = true;
                string description = null;
                string plotStyle = null;

                if (option == "TRansparency")
                {
                    while (true)
                    {
                        yield return CadPrompt.ForInteger("Enter transparency value (0-90)");

                        if (
                            c.Input.Kind != CadInputKind.Number
                            || (
                                c.Input.Number >= 0
                                && c.Input.Number <= 90
                                && c.Input.Number == Math.Floor(c.Input.Number)
                            )
                        )
                        {
                            break;
                        }

                        c.Write("Requires an integer between 0 and 90.");
                    }

                    if (c.Input.Kind != CadInputKind.Number)
                    {
                        continue;
                    }

                    percent = (int)c.Input.Number;
                }
                else if (option == "Plot")
                {
                    yield return CadPrompt.ForKeyword(
                        "Enter a plotting preference",
                        new[] { "Plot", "No plot" },
                        "Plot"
                    );
                    plotted = !c.Input.Is("No plot");
                }
                else if (option == "PStyle")
                {
                    while (true)
                    {
                        yield return CadPrompt.ForString("Enter Plot Style Name or [?]", "Normal");

                        if (c.Input.IsNone || c.Input.Kind != CadInputKind.Text)
                        {
                            break;
                        }

                        if (c.Input.Text == "?")
                        {
                            c.Write("Plot Style Names:");
                            c.Write("=================");

                            foreach (DxfDBDictionaryEntry entry in c.Database.PlotStyleNameDictionary)
                            {
                                c.Write(entry.Key);
                            }

                            continue;
                        }

                        plotStyle = c
                            .Database.PlotStyleNameDictionary?.Select(e => e.Key)
                            .FirstOrDefault(k =>
                                string.Equals(k, c.Input.Text.Trim(), StringComparison.OrdinalIgnoreCase)
                            );

                        if (plotStyle != null)
                        {
                            break;
                        }

                        c.Write("Unknown plot style.");
                    }

                    if (plotStyle == null)
                    {
                        continue;
                    }
                }
                else if (option == "Description")
                {
                    yield return CadPrompt.ForString("Enter layer description");

                    if (c.Input.Kind != CadInputKind.Text)
                    {
                        continue;
                    }

                    description = c.Input.Text;
                }

                string current = null;

                switch (option)
                {
                    case "New":
                        ask = "Enter name list for new layer(s)";
                        break;
                    case "ON":
                        ask = "Enter name list of layer(s) to turn on";
                        break;
                    case "OFF":
                        ask = "Enter name list of layer(s) to turn off";
                        break;
                    case "TRansparency":
                        ask =
                            "Enter name list of layer(s) for transparency "
                            + percent.ToString(CultureInfo.InvariantCulture)
                            + "%";
                        current = c.Database.Clayer;
                        break;
                    case "Plot":
                        ask = "Enter layer name(s) for this plot preference";
                        current = c.Database.Clayer;
                        break;
                    case "Freeze":
                        ask = "Enter name list of layer(s) to freeze";
                        break;
                    case "Thaw":
                        ask = "Enter name list of layer(s) to thaw";
                        break;
                    case "LOck":
                        ask = "Enter name list of layer(s) to lock";
                        break;
                    case "PStyle":
                        ask = "Enter name list of layer(s) for plot style name " + plotStyle;
                        current = c.Database.Clayer;
                        break;
                    case "Description":
                        ask = "Enter name list of layer(s) to apply description";
                        break;
                    default:
                        ask = "Enter name list of layer(s) to unlock";
                        break;
                }

                yield return CadPrompt.ForString(ask, current);
                string names =
                    c.Input.Kind == CadInputKind.Text ? c.Input.Text
                    : c.Input.IsNone ? EscapedLayerName(current)
                    : null;

                if (names == null)
                {
                    continue;
                }

                c.OpenUndoStep();
                List<string> listed = LayerNameList(c.Database, names, option != "New");

                if (listed.Count == 0)
                {
                    c.Write("No matching layer names found.");
                    continue;
                }

                foreach (string name in listed)
                {
                    CadEditResult result;

                    switch (option)
                    {
                        case "New":
                            result = CadLayerEditor.Add(c.Document, name);
                            break;
                        case "ON":
                            result = CadLayerEditor.SetOn(c.Document, name, true);
                            break;
                        case "OFF":
                            result = CadLayerEditor.SetOn(c.Document, name, false);
                            break;
                        case "Freeze":
                            result = CadLayerEditor.SetFrozen(c.Document, name, true);
                            break;
                        case "Thaw":
                            result = CadLayerEditor.SetFrozen(c.Document, name, false);
                            break;
                        case "LOck":
                            result = CadLayerEditor.SetLocked(c.Document, name, true);
                            break;
                        case "TRansparency":
                            result = CadLayerEditor.SetTransparency(c.Document, name, percent);
                            break;
                        case "Plot":
                            result = CadLayerEditor.SetPlottable(c.Document, name, plotted);
                            break;
                        case "PStyle":
                            result = CadLayerEditor.SetPlotStyle(c.Document, name, plotStyle);
                            break;
                        case "Description":
                            result = CadLayerEditor.SetDescription(c.Document, name, description);
                            break;
                        default:
                            result = CadLayerEditor.SetLocked(c.Document, name, false);
                            break;
                    }

                    if (!result.Succeeded)
                    {
                        c.Write(result.Message);
                    }
                }
            }
        }

        /// <summary>
        /// The layers a -LAYER name list stands for, as AutoCAD reads it: split at commas, each name is a wild-card pattern
        /// (<c>*</c>, <c>?</c>, <c>#</c>, <c>@</c>, <c>.</c>, <c>~</c>, <c>[</c>, a back quote before a character making it itself),
        /// in any case, matched against every layer in the order of the table, so that <c>Pl`#1`.b`@`[2]`~</c> is the layer
        /// <c>Pl#1.b@[2]~</c> and Thaw <c>*</c> thaws them all; a name that matches no layer gives none, and that layer typed as it
        /// is, <c>Pl#1.b@[2]~</c>, is a pattern that matches nothing. The names of -LAYER New (<paramref name="wildcards"/> false)
        /// are taken as typed.
        /// </summary>
        private static List<string> LayerNameList(DxfDatabase database, string names, bool wildcards)
        {
            var list = new List<string>();

            foreach (string piece in names.Split(','))
            {
                string name = piece.Trim();

                if (name.Length == 0)
                {
                    continue;
                }

                if (!wildcards)
                {
                    list.Add(name);
                    continue;
                }

                foreach (DxfLayerTableRecord layer in database.LayerTable)
                {
                    if (
                        Lisp.LispInterpreter.WildcardMatch(layer.Name.ToUpperInvariant(), name.ToUpperInvariant())
                        && !list.Contains(layer.Name)
                    )
                    {
                        list.Add(layer.Name);
                    }
                }
            }

            return list;
        }

        /// <summary>
        /// A layer name as a pattern that matches it alone: a back quote before each character a pattern gives a meaning, for the
        /// layer a prompt offers as its default (<c>Pl#1.b@[2]~</c> becomes <c>Pl`#1`.b`@`[2]`~</c>); null stays null.
        /// </summary>
        private static string EscapedLayerName(string name)
        {
            if (name == null)
            {
                return null;
            }

            var escaped = new System.Text.StringBuilder();

            foreach (char ch in name)
            {
                if ("*?#@.~[]`,".IndexOf(ch) >= 0)
                {
                    escaped.Append('`');
                }

                escaped.Append(ch);
            }

            return escaped.ToString();
        }

        /// <summary>"Current settings: Auto = On, Control = All, Combine = Yes, Layer = Yes", written by UNDO before it asks.</summary>
        private static string UndoSettings(CadDocument document) =>
            "Current settings: Auto = "
            + (document.UndoAuto ? "On" : "Off")
            + ", Control = "
            + document.UndoControl
            + ", Combine = "
            + (document.UndoCombine ? "Yes" : "No")
            + ", Layer = "
            + (document.UndoLayer ? "Yes" : "No");

        /// <summary>
        /// UNDO's Control option: "Enter an UNDO control option [All/None/One/Combine/Layer] &lt;All&gt;:", All keeping every
        /// step, None turning U off and One keeping the last step alone; Combine and Layer ask "Combine zoom and pan
        /// operations? [Yes/No] &lt;Yes&gt;:" and "Combine layer dialog operations? [Yes/No] &lt;Yes&gt;:", Enter being Yes.
        /// Text that names none is "Invalid option keyword." and the prompt again, as it is for Yes and No "Yes or No, please."
        /// </summary>
        private static IEnumerable<CadPrompt> UndoControlOption(CadCommandContext c)
        {
            CadDocument document = c.Document;
            yield return CadPrompt.ForKeyword(
                "Enter an UNDO control option",
                new[] { "All", "None", "One", "Combine", "Layer" },
                "All"
            );

            if (c.Input.Is("Combine") || c.Input.Is("Layer"))
            {
                bool combine = c.Input.Is("Combine");
                yield return CadPrompt
                    .ForKeyword(
                        combine ? "Combine zoom and pan operations?" : "Combine layer dialog operations?",
                        new[] { "Yes", "No" },
                        "Yes"
                    )
                    .Refusing("Yes or No, please.");

                if (combine)
                {
                    document.UndoCombine = c.Input.Is("Yes");
                }
                else
                {
                    document.UndoLayer = c.Input.Is("Yes");
                }
            }
            else if (c.Input.Is("All") || c.Input.Is("None") || c.Input.Is("One"))
            {
                document.SetUndoControl(
                    c.Input.Is("None") ? CadUndoControl.None
                    : c.Input.Is("One") ? CadUndoControl.One
                    : CadUndoControl.All
                );
            }
        }

        private static IEnumerable<CadPrompt> Undo(CadCommandContext c)
        {
            const string refused = "Requires an integer between 1 and 32767, or option keyword.";
            c.Write(UndoSettings(c.Document));

            // With UNDO turned off the Control option is all there is to ask, as AutoCAD 2021 has it.
            if (c.Document.UndoControl == CadUndoControl.None)
            {
                foreach (CadPrompt prompt in UndoControlOption(c))
                {
                    yield return prompt;
                }

                yield break;
            }

            // With Control One U takes back the last command and nothing before it: only that number or Control is asked.
            bool single = c.Document.UndoControl == CadUndoControl.One;

            // A number below 1 or past 32767, as well as text and 2.5, is refused and asked again, as AutoCAD 2021 refuses it.
            while (true)
            {
                yield return single
                    ? CadPrompt
                        .ForInteger("Enter an option", new[] { "Control" }, "1")
                        .WithoutOr()
                        .Refusing("Requires an integer value 1, or option keyword.")
                    : CadPrompt
                        .ForInteger(
                            "Enter the number of operations to undo",
                            new[] { "Auto", "Control", "BEgin", "End", "Mark", "Back" },
                            "1"
                        )
                        .Refusing(refused);

                if (
                    c.Input.Kind != CadInputKind.Number
                    || (single ? c.Input.Number == 1.0 : c.Input.Number >= 1.0 && c.Input.Number <= 32767.0)
                )
                {
                    break;
                }

                c.Write(single ? "Requires an integer value 1, or option keyword." : refused);
            }

            if (c.Input.Is("Control"))
            {
                foreach (CadPrompt prompt in UndoControlOption(c))
                {
                    yield return prompt;
                }

                yield break;
            }

            if (c.Input.Is("Auto"))
            {
                yield return CadPrompt.ForKeyword(
                    "Enter UNDO Auto mode",
                    new[] { "ON", "OFF" },
                    c.Document.UndoAuto ? "On" : "Off"
                );
                c.Document.UndoAuto = c.Input.Is("ON");
                yield break;
            }

            if (c.Input.Is("BEgin"))
            {
                c.Document.BeginUndoGroup();
                yield break;
            }

            if (c.Input.Is("End"))
            {
                c.Document.EndUndoGroup();
                yield break;
            }

            if (c.Input.Is("Mark"))
            {
                c.Remember("undo.mark", c.Document.UndoPosition);
                yield break;
            }

            if (c.Input.Is("Back"))
            {
                bool marked = c.Remembered("undo.mark", -1.0) >= 0.0;

                if (!marked)
                {
                    yield return CadPrompt.ForString("This will undo everything. OK?", "Y").WithoutColon();

                    if (
                        c.Input.Kind == CadInputKind.Text
                        && c.Input.Text.StartsWith("N", StringComparison.OrdinalIgnoreCase)
                    )
                    {
                        yield break;
                    }
                }

                UndoBack(c, marked ? (int)c.Remembered("undo.mark", 0.0) : 0, marked);
                yield break;
            }

            UndoSteps(c, c.Input.Kind == CadInputKind.Number ? (int)c.Input.Number : 1);
        }

        /// <summary>UNDO Back: the steps taken back down to the mark UNDO Mark kept, "Mark encountered", or all of them where none was kept.</summary>
        private static void UndoBack(CadCommandContext c, int mark, bool marked)
        {
            var names = new List<string>();

            while (c.Document.UndoPosition > mark)
            {
                string name = c.Document.NextUndoLabel;

                if (name == null || !c.Document.Undo())
                {
                    break;
                }

                names.Add(name);
            }

            // The names of the steps taken back stand on one line, as AutoCAD 2021 writes them: "LINE LINE", "Mark encountered".
            if (names.Count > 0)
            {
                c.Write(string.Join(" ", names));
            }

            c.Write(marked ? "Mark encountered" : "Everything has been undone");
        }

        private static IEnumerable<CadPrompt> UndoOne(CadCommandContext c)
        {
            // UNDO Control None turns U off; UNDO, which asks the control option then, turns it on again.
            if (c.Document.UndoControl == CadUndoControl.None)
            {
                c.Write("U command disabled.  Use UNDO command to turn it on");
                yield break;
            }

            UndoSteps(c, 1);
        }

        /// <summary>
        /// Takes back a number of steps and writes their names on one line, as AutoCAD 2021 writes them ("LINE LINE"; a group is
        /// "GROUP LINE CIRCLE LINE", and counts as one), and "Everything has been undone" where it ran out of steps, or, under
        /// UNDO Control One, where the one it keeps is taken back.
        /// </summary>
        private static void UndoSteps(CadCommandContext c, int count)
        {
            var names = new List<string>();
            bool ranOut = false;

            for (int i = 0; i < count; i++)
            {
                string name = c.Document.NextUndoLabel;

                if (name == null || !c.Document.Undo())
                {
                    ranOut = true;
                    break;
                }

                names.Add(name);
            }

            if (names.Count > 0)
            {
                c.Write(string.Join(" ", names));
            }

            if (ranOut || (names.Count > 0 && c.Document.UndoControl == CadUndoControl.One))
            {
                c.Write("Everything has been undone");
            }
        }

        private static IEnumerable<CadPrompt> Redo(CadCommandContext c)
        {
            string name = c.Document.NextRedoLabel;

            if (name == null || !c.Document.Redo())
            {
                c.Write("Nothing to redo");
                yield break;
            }

            c.Write(name);

            // The last step put again is the end of the list, as AutoCAD 2021 says after a REDO that brings the drawing back.
            if (!c.Document.CanRedo)
            {
                c.Write("Everything has been redone");
            }
        }

        /// <summary>
        /// Asks a reference angle or length as AutoCAD's Reference option does: typed as a number (an angle in degrees), Enter
        /// for the default (nought degrees, a length of 1), or by two points, the second asked after the first.
        /// </summary>
        private static IEnumerable<CadPrompt> Measured(
            CadCommandContext c,
            string message,
            string offered,
            bool angle,
            Ref<double?> result
        )
        {
            yield return CadPrompt.ForPoint(message, null, null, offered, true);

            if (c.Input.IsNone)
            {
                result.Value = angle ? 0.0 : 1.0;
            }
            else if (c.Input.Kind == CadInputKind.Text)
            {
                if (
                    double.TryParse(c.Input.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double typed)
                    && !double.IsNaN(typed)
                    && !double.IsInfinity(typed)
                )
                {
                    result.Value = angle
                        ? CadUnits.ToDirection(typed * Math.PI / 180.0, c.Database.Angbase, c.Database.Angdir)
                        : typed;
                }
                else
                {
                    c.Write("Requires a number or two points.");
                }
            }
            else if (c.Input.IsPoint)
            {
                GeoPoint2 first = c.Input.Point;
                yield return CadPrompt.ForPoint("Specify second point", first);

                if (c.Input.IsPoint)
                {
                    GeoVector2 between = c.Input.Point - first;
                    result.Value = angle ? c.Session.Ucs.AngleToUcs(Math.Atan2(between.Y, between.X)) : between.Length;
                }
            }
        }

        /// <summary>
        /// Asks two points and gives the angle of the line from the first to the second, or its length; the second point
        /// previewing the objects selected moved as <paramref name="moved"/> moves them by the value to the cursor, where given,
        /// and with <paramref name="fade"/> the objects themselves faded where they stand.
        /// </summary>
        private static IEnumerable<CadPrompt> TwoPoints(
            CadCommandContext c,
            bool angle,
            Ref<double?> result,
            Func<double, Affine2?> moved = null,
            bool fade = false
        )
        {
            yield return CadPrompt.ForPoint("Specify first point");

            if (!c.Input.IsPoint)
            {
                yield break;
            }

            GeoPoint2 first = c.Input.Point;

            double Between(GeoPoint2 to)
            {
                GeoVector2 between = to - first;
                return angle ? c.Session.Ucs.AngleToUcs(Math.Atan2(between.Y, between.X)) : between.Length;
            }

            CadPrompt second = CadPrompt.ForPoint("Specify second point", first);
            yield return moved == null
                ? second
                : second.Previewing(
                    CadPreview.MovingOne(c, c.Selected, input => moved(Between(input.Point)), true, fade)
                );

            if (c.Input.IsPoint)
            {
                result.Value = Between(c.Input.Point);
            }
        }

        /// <summary>
        /// A point given as a displacement, as "use first point as displacement" takes one: its coordinates in the UCS, turned
        /// into the world's. In a UCS turned a quarter at (100, 0), 10,0 typed moves objects 10 up the world's Y.
        /// </summary>
        private static GeoVector2 AsDisplacement(CadCommandContext c, GeoPoint2 point)
        {
            CadUcs ucs = c.Session.Ucs;
            return ucs.IsWorld ? new GeoVector2(point.X, point.Y) : point - new GeoPoint2(ucs.Origin.X, ucs.Origin.Y);
        }

        /// <summary>
        /// Asks the base point and the second point, or a displacement, as MOVE asks them; the second point previewing what
        /// <paramref name="preview"/> makes of the displacement to the cursor, where it is given.
        /// </summary>
        private static IEnumerable<CadPrompt> Displacement(
            CadCommandContext c,
            Ref<GeoVector2?> result,
            Func<GeoVector2, IEnumerable<DxfEntity>> preview = null
        )
        {
            yield return CadPrompt.ForPoint("Specify base point", null, new[] { "Displacement" }, "Displacement");

            if (c.Input.IsPoint)
            {
                GeoPoint2 from = c.Input.Point;
                CadPrompt second = CadPrompt.ForPoint(
                    "Specify second point or",
                    from,
                    null,
                    "use first point as displacement"
                );
                yield return preview == null
                    ? second
                    : second.Previewing(CadPreview.Making(c, input => preview(input.Point - from), true));
                result.Value = c.Input.IsPoint ? c.Input.Point - from : AsDisplacement(c, from);
                yield break;
            }

            if (!c.Input.Is("Displacement"))
            {
                yield break;
            }

            var last = new GeoVector2(c.Remembered("displacement.x", 0.0), c.Remembered("displacement.y", 0.0));
            yield return CadPrompt.ForPoint(
                "Specify displacement",
                null,
                null,
                Length(last.X) + ", " + Length(last.Y) + ", " + Length(0.0)
            );
            result.Value = c.Input.IsPoint ? AsDisplacement(c, c.Input.Point) : last;
            Remember(c, result.Value.Value);
        }

        private static void Remember(CadCommandContext c, GeoVector2 displacement)
        {
            c.Remember("displacement.x", displacement.X);
            c.Remember("displacement.y", displacement.Y);
        }

        /// <summary>
        /// Moves, turns, scales or mirrors entities, or copies of them; gives the handles of the copies. A mirror that keeps
        /// texts readable gives each its own move (<paramref name="readable"/>), worked out before it moves.
        /// </summary>
        private static List<string> Transform(
            CadCommandContext c,
            IReadOnlyList<string> handles,
            GeoTransform3 transform,
            bool copy,
            string how = "Move",
            string value = null,
            Func<DxfEntity, ReadableMirror> readable = null
        )
        {
            var planned = new List<CadEditRecord>();

            if (!copy)
            {
                foreach (string handle in handles)
                {
                    planned.Add(new CadEditRecord(CadEditRecordKind.Transformed, handle, how, null, value));
                }
            }

            CadEditResult result = c.Apply(planned, s =>
            {
                var copies = copy ? new Dictionary<string, DxfEntity>(StringComparer.OrdinalIgnoreCase) : null;
                var together = new HashSet<string>(handles, StringComparer.OrdinalIgnoreCase);
                var clippedViewports = new List<DxfViewport>();

                foreach (string handle in handles)
                {
                    if (!copy && s.IsHandled(handle))
                    {
                        continue;
                    }

                    DxfEntity entity = s.Document.FindEntity(handle);
                    ReadableMirror kept = readable?.Invoke(entity);

                    if (
                        entity is DxfViewport plain
                        && plain.NonRectClipEntity == null
                        && (how == "Rotate" || how == "Mirror" || how == "Scale")
                    )
                    {
                        // A viewport that is not clipped cannot be turned or mirrored itself: a mirror moves its centre, a quarter
                        // turn turns it, any other turn makes it clipped; a scale scales its frame and keeps its view's scale, the
                        // model standing still on the sheet (CadViewportClips.TurnPlain).
                        DxfViewport turned = plain;

                        if (copy)
                        {
                            turned = (DxfViewport)AppendCopy(s, handle, GeoTransform3.Identity);
                            copies[handle] = turned;
                        }

                        CadViewportClips.TurnPlain(s, turned, transform, how);
                        continue;
                    }

                    if (copy)
                    {
                        // A clipped viewport copied with its boundary is not moved itself: it is tied to the copy of the boundary and
                        // fitted to it afterwards, as a turn or a mirror cannot move a viewport.
                        bool paired = entity is DxfViewport shown && CadViewportClips.IsPaired(shown, together);
                        DxfEntity copied;

                        using (Mirroring(kept))
                        {
                            copied = AppendCopy(
                                s,
                                handle,
                                paired ? GeoTransform3.Identity : kept?.Transform ?? transform
                            );
                        }

                        kept?.Finish(copied);
                        copies[handle] = copied;
                    }
                    else if (entity is DxfViewport clipped && clipped.NonRectClipEntity != null)
                    {
                        // A clipped viewport takes its frame from its boundary, which moves with it: the edit refits it.
                        if (CadViewportClips.IsPaired(clipped, together))
                        {
                            clippedViewports.Add(clipped);
                        }

                        continue;
                    }
                    else
                    {
                        if (entity is DxfOle2Frame ole)
                        {
                            CadOleFrames.Transform(ole, kept?.Transform ?? transform);
                        }
                        else
                        {
                            using (Mirroring(kept))
                            {
                                entity.TransformBy(kept?.Transform ?? transform);
                            }

                            CarryManualBreaks(s.Document, entity, kept?.Transform ?? transform);
                        }

                        kept?.Finish(entity);
                    }
                }

                foreach (DxfViewport clipped in clippedViewports)
                {
                    CadViewportClips.Refit(clipped, transform, how);
                }

                if (copy)
                {
                    CadDimAssociation.TieCopies(s, copies);
                    CadViewportClips.TieCopies(s, copies, transform, how);
                }
            });

            var made = new List<string>();

            foreach (CadEditRecord record in result.Records)
            {
                if (record.Kind == CadEditRecordKind.Added)
                {
                    made.Add(record.Handle);
                }
            }

            return made;
        }

        /// <summary>The scope in which a hatch mirrored reflects the lines of its pattern, for an object MIRROR moves under MIRRHATCH 1; none otherwise.</summary>
        private static IDisposable Mirroring(ReadableMirror kept) =>
            kept != null && kept.MirrorsPattern ? new DxfHatch.PatternMirroring() : null;

        private static void EraseAll(CadCommandContext c, IReadOnlyList<string> handles)
        {
            var planned = new List<CadEditRecord>();

            foreach (string handle in handles)
            {
                planned.Add(new CadEditRecord(CadEditRecordKind.Erased, handle));
            }

            c.Apply(planned, s =>
            {
                foreach (string handle in handles)
                {
                    if (!s.IsHandled(handle))
                    {
                        s.Document.FindEntity(handle).Erase();
                    }
                }
            });
        }

        /// <summary>A value an iterator gives back to the body that ran it.</summary>
        private sealed class Ref<T>
        {
            public T Value { get; set; }
        }
    }
}
