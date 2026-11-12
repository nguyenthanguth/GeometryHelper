using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
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

            c.Write("Current settings:  Copy mode = Multiple");
            yield return CadPrompt.ForPoint("Specify base point", null, new[] { "Displacement" }, "Displacement");

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
            yield return CadPrompt.ForPoint("Specify second point or", from, null, "use first point as displacement");

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

                yield return CadPrompt.ForPoint("Specify second point", from, new[] { "Exit", "Undo" }, "Exit");

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

            if (c.Selected.Count > 0)
            {
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

            // The objects turned about the base point by an angle, as they are turned once it is given.
            GeoTransform3 Turned(double by) => GeoTransform3.RotationAxis(P3(center), Up, by);

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
                yield return CadPrompt
                    .ForDistance("Specify scale factor", center, new[] { "Copy", "Reference" }, Length(last))
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
            Func<DxfEntity, ReadableMirror> readable = entity =>
                entity is DxfDimension dimension ? ReadableDimension(dimension, mirror, tolerance)
                : entity is DxfMLeader mleader ? MLeaderMirror(document, mleader, mirror, mirrtext, tolerance)
                : entity is DxfBlockReference insert ? InsertMirror(document, insert, mirror, mirrtext, tolerance)
                : entity is DxfTolerance ? (mirrtext ? null : ReadableTolerance(mirror))
                : mirrtext ? InPlane(entity, mirror, tolerance)
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

        private static IEnumerable<CadPrompt> Explode(CadCommandContext c)
        {
            foreach (CadPrompt prompt in c.SelectObjects())
            {
                yield return prompt;
            }

            var parts = new List<KeyValuePair<string, IReadOnlyList<DxfEntity>>>();
            int refused = 0;

            foreach (string handle in c.Selected)
            {
                DxfEntity entity = c.Document.FindEntity(handle);
                IReadOnlyList<DxfEntity> pieces = null;

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
                    pieces = (entity is DxfMText words ? CadMTextEdit.Explode(words) : null) ?? entity.Explode();
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
        /// -LAYER: AutoCAD's options on the command line, stAte among them, until Enter. A layer of an external reference
        /// (<c>grid|WALL</c>) is switched on and off, given a transparency, plotted or not, frozen, thawed, locked, unlocked
        /// and described, as AutoCAD 2021's -LAYER does, and Set refuses it in AutoCAD 2021's words: "Invalid layer name.".
        /// TRansparency and Plot ask their value first and offer the current layer, as AutoCAD 2021 asks "Enter transparency
        /// value (0-90):", then "Enter name list of layer(s) for transparency 50% &lt;0&gt;:".
        /// </summary>
        private static IEnumerable<CadPrompt> LayerTyped(CadCommandContext c)
        {
            string[] options =
            {
                "Set",
                "New",
                "ON",
                "OFF",
                "TRansparency",
                "Plot",
                "Freeze",
                "Thaw",
                "LOck",
                "Unlock",
                "stAte",
                "Description",
            };
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

                int percent = 0;
                bool plotted = true;
                string description = null;

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
                    case "Set":
                        ask = "Enter layer name to make current";
                        break;
                    case "New":
                        ask = "Enter name list for new layer(s)";
                        break;
                    case "ON":
                        ask = "Enter name list of layer(s) to turn On";
                        break;
                    case "OFF":
                        ask = "Enter name list of layer(s) to turn Off";
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
                    : c.Input.IsNone ? current
                    : null;

                if (names == null)
                {
                    continue;
                }

                c.OpenUndoStep();

                foreach (string piece in names.Split(','))
                {
                    string name = piece.Trim();

                    if (name.Length == 0)
                    {
                        continue;
                    }

                    CadEditResult result;

                    switch (option)
                    {
                        case "Set":
                            result =
                                c.Database.LayerTable.Has(name) && c.Database.LayerTable[name].IsDependent
                                    ? CadEditResult.Refuse("Invalid layer name.")
                                    : CadLayerEditor.SetCurrent(c.Document, name);
                            break;
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

        private static IEnumerable<CadPrompt> Undo(CadCommandContext c)
        {
            yield return CadPrompt.ForInteger("Enter the number of operations to undo", null, "1");
            int count = c.Input.Kind == CadInputKind.Number ? (int)c.Input.Number : 1;

            if (count <= 0)
            {
                c.Write("Requires a positive integer.");
                yield break;
            }

            UndoSteps(c, count);
        }

        private static IEnumerable<CadPrompt> UndoOne(CadCommandContext c)
        {
            UndoSteps(c, 1);
            yield break;
        }

        private static void UndoSteps(CadCommandContext c, int count)
        {
            for (int i = 0; i < count; i++)
            {
                CadUndoStep step = c.Document.NextUndo;

                if (step == null || !c.Document.Undo())
                {
                    c.Write("Everything has been undone");
                    return;
                }

                c.Write(step.Name);
            }
        }

        private static IEnumerable<CadPrompt> Redo(CadCommandContext c)
        {
            CadUndoStep step = c.Document.NextRedo;

            if (step == null || !c.Document.Redo())
            {
                c.Write("Nothing to redo.");
                yield break;
            }

            c.Write(step.Name);
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

                foreach (string handle in handles)
                {
                    if (!copy && s.IsHandled(handle))
                    {
                        continue;
                    }

                    DxfEntity entity = s.Document.FindEntity(handle);
                    ReadableMirror kept = readable?.Invoke(entity);

                    if (copy)
                    {
                        DxfEntity copied = AppendCopy(s, handle, kept?.Transform ?? transform);
                        kept?.Finish(copied);
                        copies[handle] = copied;
                    }
                    else
                    {
                        entity.TransformBy(kept?.Transform ?? transform);
                        kept?.Finish(entity);
                    }
                }

                if (copy)
                {
                    CadDimAssociation.TieCopies(s, copies);
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
