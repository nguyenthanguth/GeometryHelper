using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// The authoring commands of the Block Editor, in AutoCAD 2021's words: BPARAMETER adds a parameter (Alignment, Base, Point,
    /// Linear, Polar, XY, Rotation, Flip, Visibility), BACTION an action (Array, Flip, Move, Rotate, Scale, Stretch, Polar stretch)
    /// to the parameter picked, BVSTATE manages the visibility states, BVHIDE and BVSHOW hide and show objects in them. The
    /// evaluation graph they write is the library's (<see cref="DxfBlockTableRecord.AddLinearParameter"/> and the rest).
    /// </summary>
    internal static partial class CadCommands
    {
        private static readonly string[] ParameterKinds =
        {
            "Alignment",
            "Base",
            "pOint",
            "Linear",
            "Polar",
            "Xy",
            "Rotation",
            "Flip",
            "Visibility",
            "looKup",
        };

        private static readonly string[] ActionKinds =
        {
            "Array",
            "Lookup",
            "Flip",
            "Move",
            "Rotate",
            "Scale",
            "sTretch",
            "Polar stretch",
        };

        internal static void AddBlockAuthoringTo(CadCommandRegistry registry)
        {
            registry.Add(new CadCommand("BPARAMETER", BlockParameter));
            registry.Add(new CadCommand("BACTION", BlockAction));
            registry.Add(new CadCommand("BVSTATE", BlockVisibilityStates));
            registry.Add(new CadCommand("-BVSTATE", BlockVisibilityStatesTyped));
            registry.Add(new CadCommand("BVHIDE", c => BlockVisibilityObjects(c, false)));
            registry.Add(new CadCommand("BVSHOW", c => BlockVisibilityObjects(c, true)));
            registry.Add(new CadCommand("BTESTBLOCK", BlockTest));
            registry.Add(new CadCommand("BAUTHORPALETTE", BlockPalette));
            registry.Add(new CadCommand("BAUTHORPALETTECLOSE", BlockPaletteClose));
        }

        /// <summary>BAUTHORPALETTE: the Block Authoring Palettes, the parameters and actions to add to the block, shown by the window.</summary>
        private static IEnumerable<CadPrompt> BlockPalette(CadCommandContext c)
        {
            if (!c.Session.Request("BAUTHORPALETTE", c.Document, new CadAuthoringPaletteRequest(true)))
            {
                c.Write(
                    "The Block Authoring Palettes are shown by the window: BPARAMETER and BACTION add what they list."
                );
            }

            yield break;
        }

        /// <summary>BAUTHORPALETTECLOSE: closes the Block Authoring Palettes.</summary>
        private static IEnumerable<CadPrompt> BlockPaletteClose(CadCommandContext c)
        {
            c.Session.Request("BAUTHORPALETTE", c.Document, new CadAuthoringPaletteRequest(false));
            yield break;
        }

        /// <summary>
        /// BTESTBLOCK: the block as it stands in the editor, saved or not, put into a copy of the drawing with nothing else in model
        /// space, which the window opens as a drawing of its own, "Test Block Window - DOOR", to try its grips and properties in.
        /// The drawing and the block open are not changed.
        /// </summary>
        private static IEnumerable<CadPrompt> BlockTest(CadCommandContext c)
        {
            CadBlockEditor editor = AuthoringEditor(c);

            if (editor == null)
            {
                yield break;
            }

            DxfDatabase copy = DxfDatabase.Parse(c.Database.ToText());
            DxfBlockTableRecord block = copy.BlockTable[editor.Name];

            foreach (DxfEntity entity in copy.ModelSpace.ToList())
            {
                entity.Erase();
            }

            copy.ModelSpace.AppendEntity(new DxfBlockReference(new GeoPoint3(0, 0, 0), block));
            string title = "Test Block Window - " + editor.Name;

            if (!c.Session.Request("BTESTBLOCK", c.Document, new CadTestBlockRequest(copy, title)))
            {
                c.Write("The Test Block window is shown by the window.");
            }
        }

        /// <summary>The Block Editor open on a block, or null with "** BPARAMETER command only allowed in Block Editor. **".</summary>
        private static CadBlockEditor AuthoringEditor(CadCommandContext c)
        {
            CadBlockEditor editor = c.Document.BlockEditor;

            if (editor == null || editor.IsCurrentDrawing)
            {
                c.Write("** " + c.Command.Name + " command only allowed in Block Editor. **");
                return null;
            }

            return editor;
        }

        // ----- BPARAMETER -----

        /// <summary>What a parameter is given before its points are asked: the options of its first prompt.</summary>
        private sealed class ParameterOptions
        {
            public string Name;
            public readonly string[] Labels = new string[2];
            public readonly string[] Descriptions = new string[2];
            public readonly DxfBlockParameterValueSet[] Sets = new DxfBlockParameterValueSet[2];
            public readonly bool[] SetGiven = new bool[2];
            public bool? Chain;
            public bool? Palette;
        }

        /// <summary>The properties a kind of parameter gives and what AutoCAD's prompts call them: "distance", or "horizontal" and "vertical".</summary>
        private static string[] PropertiesOf(string kind)
        {
            switch (kind)
            {
                case "Linear":
                    return new[] { "distance" };
                case "Polar":
                    return new[] { "distance", "angle" };
                case "Xy":
                    return new[] { "horizontal distance", "vertical distance" };
                case "Rotation":
                    return new[] { "rotation" };
                case "pOint":
                    return new[] { "position" };
                case "Flip":
                    return new[] { "Flip" };
                case "Visibility":
                    return new[] { "visibility" };
                default:
                    return new string[0];
            }
        }

        /// <summary>The word a label starts from before its number: Distance1, Angle1, Position1.</summary>
        private static string[] LabelWords(string kind)
        {
            switch (kind)
            {
                case "Linear":
                    return new[] { "Distance" };
                case "Polar":
                    return new[] { "Distance", "Angle" };
                case "Xy":
                    return new[] { "Horizontal", "Vertical" };
                case "Rotation":
                    return new[] { "Angle" };
                case "pOint":
                    return new[] { "Position" };
                case "Flip":
                    return new[] { "Flip state" };
                case "Visibility":
                    return new[] { "Visibility" };
                default:
                    return new string[0];
            }
        }

        /// <summary>The element name the library gives next of a kind: Linear, then Linear1.</summary>
        private static string ElementNameOf(string kind)
        {
            switch (kind)
            {
                case "pOint":
                    return "Point";
                case "Xy":
                    return "XY";
                case "Visibility":
                    return "Visibility State";
                case "Base":
                    return "Base Point";
                default:
                    return kind;
            }
        }

        private static string FreeElementName(DxfBlockTableRecord block, string word)
        {
            var taken = new HashSet<string>(
                DxfBlockAuthoring.Elements(block).Select(e => e.Name),
                StringComparer.OrdinalIgnoreCase
            );
            string free = word;

            for (int i = 1; taken.Contains(free); i++)
            {
                free = word + i.ToString(CultureInfo.InvariantCulture);
            }

            return free;
        }

        /// <summary>BPARAMETER: "Enter parameter type [...] &lt;Linear&gt;:", then the prompts of the kind chosen.</summary>
        private static IEnumerable<CadPrompt> BlockParameter(CadCommandContext c)
        {
            CadBlockEditor editor = AuthoringEditor(c);

            if (editor == null)
            {
                yield break;
            }

            int last = (int)c.Remembered("BPARAMETER.kind", 3.0);
            yield return CadPrompt.ForKeyword("Enter parameter type", ParameterKinds, ParameterKinds[last]);

            string kind =
                c.Input.IsNone ? ParameterKinds[last]
                : c.Input.Kind == CadInputKind.Keyword ? c.Input.Text
                : null;

            if (kind == null)
            {
                yield break;
            }

            c.Remember("BPARAMETER.kind", Array.IndexOf(ParameterKinds, kind));

            switch (kind)
            {
                case "Linear":
                case "Polar":
                    foreach (CadPrompt prompt in TwoPointParameter(c, editor, kind))
                    {
                        yield return prompt;
                    }

                    break;
                case "Xy":
                    foreach (CadPrompt prompt in TwoPointParameter(c, editor, kind))
                    {
                        yield return prompt;
                    }

                    break;
                case "Rotation":
                    foreach (CadPrompt prompt in RotationParameter(c, editor))
                    {
                        yield return prompt;
                    }

                    break;
                case "Flip":
                    foreach (CadPrompt prompt in FlipParameter(c, editor))
                    {
                        yield return prompt;
                    }

                    break;
                case "pOint":
                case "Visibility":
                case "Base":
                    foreach (CadPrompt prompt in OnePointParameter(c, editor, kind))
                    {
                        yield return prompt;
                    }

                    break;
                case "Alignment":
                    foreach (CadPrompt prompt in AlignmentParameter(c, editor))
                    {
                        yield return prompt;
                    }

                    break;
                default:
                    c.Write(
                        "A lookup parameter works with its lookup table, which the Block Editor of this version does not make."
                    );
                    break;
            }
        }

        /// <summary>
        /// The options of a parameter's first prompt, each asked in turn and the first prompt asked again: Name, Label, Chain,
        /// Description, Palette and Value set, as AutoCAD 2021 words them. Yields the prompts of one option; the caller asks the
        /// first prompt again afterwards.
        /// </summary>
        private static IEnumerable<CadPrompt> ParameterOption(
            CadCommandContext c,
            DxfBlockTableRecord block,
            string kind,
            ParameterOptions options,
            string option
        )
        {
            string[] properties = PropertiesOf(kind);
            string[] words = LabelWords(kind);

            switch (option)
            {
                case "Name":
                {
                    string free = FreeElementName(block, ElementNameOf(kind));
                    yield return CadPrompt.ForString("Enter parameter name", options.Name ?? free);

                    if (!c.Input.IsNone && c.Input.Text != null)
                    {
                        string typed = c.Input.Text.Trim();

                        if (
                            DxfBlockAuthoring
                                .Elements(block)
                                .Any(e => string.Equals(e.Name, typed, StringComparison.OrdinalIgnoreCase))
                        )
                        {
                            c.Write("A parameter with that name already exists in the block");
                        }
                        else
                        {
                            options.Name = typed;
                        }
                    }

                    break;
                }

                case "Label":
                    for (int i = 0; i < properties.Length; i++)
                    {
                        string now = options.Labels[i] ?? CadBlockAuthoring.FreeLabel(block, words[i]);
                        yield return CadPrompt.ForString("Enter " + properties[i] + " property label", now);

                        if (!c.Input.IsNone && c.Input.Text != null)
                        {
                            string typed = c.Input.Text.Trim();

                            if (
                                DxfBlockAuthoring
                                    .Elements(block)
                                    .Any(e =>
                                        e.Labels.Any(l => string.Equals(l, typed, StringComparison.OrdinalIgnoreCase))
                                    )
                            )
                            {
                                c.Write("A property with that label already exists in the block");
                            }
                            else
                            {
                                options.Labels[i] = typed;
                            }
                        }
                    }

                    break;
                case "Description":
                    for (int i = 0; i < properties.Length; i++)
                    {
                        yield return CadPrompt.ForString(
                            "Enter "
                                + (properties.Length > 1 ? properties[i] + " " : string.Empty)
                                + "property description"
                        );

                        if (c.Input.Text != null)
                        {
                            options.Descriptions[i] = c.Input.Text.Trim();
                        }
                    }

                    break;
                case "Chain":
                    yield return CadPrompt.ForKeyword(
                        "Evaluate associated actions when parameter is edited by another action?",
                        new[] { "Yes", "No" },
                        options.Chain == true ? "Yes" : "No"
                    );
                    options.Chain = c.Input.IsNone ? options.Chain : c.Input.Is("Yes");
                    break;
                case "Palette":
                    yield return CadPrompt.ForKeyword(
                        "Display property in Properties palette?",
                        new[] { "Yes", "No" },
                        options.Palette == false ? "No" : "Yes"
                    );
                    options.Palette = c.Input.IsNone ? options.Palette : c.Input.Is("Yes");
                    break;
                case "Value set":
                    for (int i = 0; i < properties.Length; i++)
                    {
                        bool angle = properties[i] == "angle" || properties[i] == "rotation";

                        if (kind == "Linear" || kind == "Xy" || kind == "Polar" || kind == "Rotation")
                        {
                            foreach (CadPrompt prompt in ValueSetPrompts(c, properties[i], angle, options, i))
                            {
                                yield return prompt;
                            }
                        }
                    }

                    break;
            }
        }

        /// <summary>
        /// The value set of a property: "Enter distance value set type [None/List/Increment] &lt;None&gt;:", then the list of values
        /// or the increment with its minimum and maximum; angles are asked and given in degrees.
        /// </summary>
        private static IEnumerable<CadPrompt> ValueSetPrompts(
            CadCommandContext c,
            string property,
            bool angle,
            ParameterOptions options,
            int index
        )
        {
            string word = angle ? "angle" : "distance";
            yield return CadPrompt.ForKeyword(
                "Enter " + (property == "rotation" ? "angle" : property) + " value set type",
                new[] { "None", "List", "Increment" },
                "None"
            );

            string type = c.Input.IsNone ? "None" : c.Input.Text;

            if (type == "List")
            {
                yield return CadPrompt.ForString("Enter list of " + word + " values (separated by commas)");
                var values = new List<double>();

                foreach (
                    string part in (c.Input.Text ?? string.Empty).Split(
                        new[] { ',' },
                        StringSplitOptions.RemoveEmptyEntries
                    )
                )
                {
                    if (
                        !double.TryParse(
                            part.Trim(),
                            NumberStyles.Float,
                            CultureInfo.InvariantCulture,
                            out double value
                        )
                        || value < 0.0
                    )
                    {
                        c.Write("Requires numeric nonzero value.");
                        values.Clear();
                        break;
                    }

                    values.Add(angle ? value * Math.PI / 180.0 : value);
                }

                options.Sets[index] = values.Count == 0 ? null : DxfBlockParameterValueSet.List(values);
                options.SetGiven[index] = true;
            }
            else if (type == "Increment")
            {
                yield return CadPrompt.ForDistance("Enter " + word + " increment");
                double increment = c.Input.Number;
                yield return CadPrompt.ForDistance("Enter minimum " + word);
                double minimum = c.Input.Number;
                yield return CadPrompt.ForDistance("Enter maximum " + word);
                double maximum = c.Input.Number;

                if (angle)
                {
                    increment *= Math.PI / 180.0;
                    minimum *= Math.PI / 180.0;
                    maximum *= Math.PI / 180.0;
                }

                if (!(increment > 0.0) || !(maximum > minimum) || minimum < 0.0)
                {
                    c.Write("Requires numeric nonzero value.");
                    options.Sets[index] = null;
                }
                else
                {
                    options.Sets[index] = DxfBlockParameterValueSet.Increments(minimum, maximum, increment);
                }

                options.SetGiven[index] = true;
            }
            else
            {
                options.Sets[index] = null;
                options.SetGiven[index] = true;
            }
        }

        /// <summary>The first point of a parameter: "Specify start point or [Name/Label/Chain/Description/Palette/Value set]:", the options asked until a point is given; null where it is cancelled.</summary>
        private static IEnumerable<CadPrompt> FirstPoint(
            CadCommandContext c,
            DxfBlockTableRecord block,
            string kind,
            string message,
            string[] keywords,
            ParameterOptions options,
            Action<GeoPoint2?> got
        )
        {
            while (true)
            {
                yield return CadPrompt.ForPoint(message, null, keywords);

                if (c.Input.IsPoint)
                {
                    got(c.Input.Point);
                    yield break;
                }

                string option = c.Input.Kind == CadInputKind.Keyword ? c.Input.Text : null;

                if (option == null)
                {
                    got(null);
                    yield break;
                }

                foreach (CadPrompt prompt in ParameterOption(c, block, kind, options, option))
                {
                    yield return prompt;
                }
            }
        }

        /// <summary>Linear, Polar and XY: the start point, the endpoint and where the label stands.</summary>
        private static IEnumerable<CadPrompt> TwoPointParameter(CadCommandContext c, CadBlockEditor editor, string kind)
        {
            DxfBlockTableRecord block = editor.Space.Block;
            var options = new ParameterOptions();
            GeoPoint2? start = null;
            string message = kind == "Xy" ? "Specify base point" : "Specify start point";
            string[] keywords =
                kind == "Xy"
                    ? new[] { "Name", "Label", "Chain", "Description", "Palette", "Value set" }
                    : new[] { "Name", "Label", "Chain", "Description", "Palette", "Value set" };

            foreach (CadPrompt prompt in FirstPoint(c, block, kind, message, keywords, options, p => start = p))
            {
                yield return prompt;
            }

            if (!start.HasValue)
            {
                yield break;
            }

            yield return CadPrompt.ForPoint(kind == "Xy" ? "Specify endpoint" : "Specify endpoint", start);

            if (!c.Input.IsPoint)
            {
                yield break;
            }

            GeoPoint2 end = c.Input.Point;
            yield return CadPrompt.ForPoint("Specify label location", start);
            GeoPoint2? label = c.Input.IsPoint ? c.Input.Point : (GeoPoint2?)null;
            AddTwoPoint(c, editor, kind, options, start.Value, end, label);
        }

        private static void AddTwoPoint(
            CadCommandContext c,
            CadBlockEditor editor,
            string kind,
            ParameterOptions options,
            GeoPoint2 start,
            GeoPoint2 end,
            GeoPoint2? label
        )
        {
            string blockName = editor.Name;
            double z = editor.Space.Block.Origin.Z;
            var a = new GeoPoint3(start.X, start.Y, z);
            var b = new GeoPoint3(end.X, end.Y, z);
            double dx = end.X - start.X,
                dy = end.Y - start.Y;
            double length = Math.Sqrt(dx * dx + dy * dy);

            // The label stands off the line to its left looking from the start to the end, by the distance the point given is off it.
            double offset = -0.1 * length;

            if (label.HasValue && length > 0.0)
            {
                offset = ((label.Value.X - start.X) * -dy + (label.Value.Y - start.Y) * dx) / length;
            }

            c.Apply(
                new[] { new CadEditRecord(CadEditRecordKind.TableChanged, null, kind + " parameter", null, null) },
                s =>
                {
                    DxfBlockTableRecord block = s.Database.BlockTable[blockName];
                    string[] words = LabelWords(kind);
                    string first = options.Labels[0] ?? CadBlockAuthoring.FreeLabel(block, words[0]);
                    string description = options.Descriptions[0] ?? string.Empty;
                    string handle;

                    if (kind == "Linear")
                    {
                        handle = block.AddLinearParameter(first, description, a, b, offset, options.Sets[0]).Handle;
                    }
                    else if (kind == "Polar")
                    {
                        string second = options.Labels[1] ?? CadBlockAuthoring.FreeLabel(block, words[1]);
                        handle = block
                            .AddPolarParameter(
                                first,
                                description,
                                second,
                                options.Descriptions[1] ?? string.Empty,
                                a,
                                b,
                                offset,
                                options.Sets[0],
                                options.Sets[1]
                            )
                            .Handle;
                    }
                    else
                    {
                        string second = options.Labels[1] ?? CadBlockAuthoring.FreeLabel(block, words[1]);
                        handle = block
                            .AddXYParameter(
                                first,
                                description,
                                second,
                                options.Descriptions[1] ?? string.Empty,
                                a,
                                b,
                                options.Sets[0],
                                options.Sets[1]
                            )
                            .Handle;
                    }

                    Finish(
                        block,
                        handle,
                        options,
                        label.HasValue ? new GeoPoint3(label.Value.X, label.Value.Y, z) : (GeoPoint3?)null
                    );
                }
            );
        }

        /// <summary>The name, the flags and the label's place of a parameter just written, as its options and its label location gave them.</summary>
        private static void Finish(DxfBlockTableRecord block, string handle, ParameterOptions options, GeoPoint3? label)
        {
            DxfBlockElement element = CadBlockAuthoring.Find(block, handle);

            if (element == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(options.Name))
            {
                DxfBlockAuthoring.Rename(element, options.Name);
            }

            if (options.Chain == true)
            {
                DxfBlockAuthoring.SetChainActions(element, true);
            }

            if (options.Palette == false)
            {
                DxfBlockAuthoring.SetShowProperties(element, false);
            }

            if (label.HasValue)
            {
                DxfBlockAuthoring.SetLocation(element, label.Value);
            }
        }

        /// <summary>Rotation: the base point, the radius of the parameter, the default rotation angle and where the label stands.</summary>
        private static IEnumerable<CadPrompt> RotationParameter(CadCommandContext c, CadBlockEditor editor)
        {
            DxfBlockTableRecord block = editor.Space.Block;
            var options = new ParameterOptions();
            GeoPoint2? start = null;

            foreach (
                CadPrompt prompt in FirstPoint(
                    c,
                    block,
                    "Rotation",
                    "Specify base point",
                    new[] { "Name", "Label", "Chain", "Description", "Palette", "Value set" },
                    options,
                    p => start = p
                )
            )
            {
                yield return prompt;
            }

            if (!start.HasValue)
            {
                yield break;
            }

            yield return CadPrompt.ForDistance("Specify radius of parameter", start);
            double radius = c.Input.Number;
            yield return CadPrompt.ForAngle("Specify default rotation angle", start, null, "0");
            double angle = c.Input.IsNone ? 0.0 : c.Input.Number;
            yield return CadPrompt.ForPoint("Specify label location", start);
            GeoPoint2? label = c.Input.IsPoint ? c.Input.Point : (GeoPoint2?)null;
            string blockName = editor.Name;
            double z = block.Origin.Z;
            GeoPoint2 at = start.Value;

            c.Apply(
                new[] { new CadEditRecord(CadEditRecordKind.TableChanged, null, "Rotation parameter", null, null) },
                s =>
                {
                    DxfBlockTableRecord target = s.Database.BlockTable[blockName];
                    string name = options.Labels[0] ?? CadBlockAuthoring.FreeLabel(target, "Angle");
                    string handle = target
                        .AddRotationParameter(
                            name,
                            options.Descriptions[0] ?? string.Empty,
                            new GeoPoint3(at.X, at.Y, z),
                            radius,
                            angle,
                            options.Sets[0]
                        )
                        .Handle;
                    Finish(
                        target,
                        handle,
                        options,
                        label.HasValue ? new GeoPoint3(label.Value.X, label.Value.Y, z) : (GeoPoint3?)null
                    );
                }
            );
        }

        /// <summary>Flip: the base point and the endpoint of the reflection line, and where the label stands.</summary>
        private static IEnumerable<CadPrompt> FlipParameter(CadCommandContext c, CadBlockEditor editor)
        {
            DxfBlockTableRecord block = editor.Space.Block;
            var options = new ParameterOptions();
            GeoPoint2? start = null;

            foreach (
                CadPrompt prompt in FirstPoint(
                    c,
                    block,
                    "Flip",
                    "Specify base point of reflection line",
                    new[] { "Name", "Label", "Description", "Palette" },
                    options,
                    p => start = p
                )
            )
            {
                yield return prompt;
            }

            if (!start.HasValue)
            {
                yield break;
            }

            yield return CadPrompt.ForPoint("Specify endpoint of reflection line", start);

            if (!c.Input.IsPoint)
            {
                yield break;
            }

            GeoPoint2 end = c.Input.Point;
            yield return CadPrompt.ForPoint("Specify label location", start);
            GeoPoint2? label = c.Input.IsPoint ? c.Input.Point : (GeoPoint2?)null;
            string blockName = editor.Name;
            double z = block.Origin.Z;
            GeoPoint2 at = start.Value;

            c.Apply(
                new[] { new CadEditRecord(CadEditRecordKind.TableChanged, null, "Flip parameter", null, null) },
                s =>
                {
                    DxfBlockTableRecord target = s.Database.BlockTable[blockName];
                    string name = options.Labels[0] ?? CadBlockAuthoring.FreeLabel(target, "Flip state");
                    string handle = target
                        .AddFlipParameter(
                            name,
                            options.Descriptions[0] ?? string.Empty,
                            new GeoPoint3(at.X, at.Y, z),
                            new GeoPoint3(end.X, end.Y, z)
                        )
                        .Handle;
                    Finish(
                        target,
                        handle,
                        options,
                        label.HasValue ? new GeoPoint3(label.Value.X, label.Value.Y, z) : (GeoPoint3?)null
                    );
                }
            );
        }

        /// <summary>Point, Visibility and Base: the one location of the parameter.</summary>
        private static IEnumerable<CadPrompt> OnePointParameter(CadCommandContext c, CadBlockEditor editor, string kind)
        {
            DxfBlockTableRecord block = editor.Space.Block;
            var options = new ParameterOptions();
            GeoPoint2? at = null;
            string[] keywords =
                kind == "pOint" ? new[] { "Name", "Label", "Chain", "Description", "Palette" }
                : kind == "Visibility" ? new[] { "Name", "Label", "Description", "Palette" }
                : new[] { "Name", "Label", "Description", "Palette" };

            if (kind == "Visibility" && block.GetVisibilityStates().Any())
            {
                c.Write("** Visibility parameter already exists in block definition. **");
                yield break;
            }

            if (kind == "Base" && DxfBlockAuthoring.Elements(block).Any(e => e.Kind == DxfBlockElementKind.BasePoint))
            {
                c.Write("** Base point parameter already exists in block definition. **");
                yield break;
            }

            foreach (
                CadPrompt prompt in FirstPoint(c, block, kind, "Specify parameter location", keywords, options, p =>
                    at = p
                )
            )
            {
                yield return prompt;
            }

            if (!at.HasValue)
            {
                yield break;
            }

            GeoPoint2? label = null;

            if (kind == "pOint")
            {
                yield return CadPrompt.ForPoint("Specify label location", at);
                label = c.Input.IsPoint ? c.Input.Point : (GeoPoint2?)null;
            }

            string blockName = editor.Name;
            double z = block.Origin.Z;
            GeoPoint2 where = at.Value;

            c.Apply(
                new[] { new CadEditRecord(CadEditRecordKind.TableChanged, null, kind + " parameter", null, null) },
                s =>
                {
                    DxfBlockTableRecord target = s.Database.BlockTable[blockName];
                    var location = new GeoPoint3(where.X, where.Y, z);
                    string handle;

                    if (kind == "pOint")
                    {
                        string name = options.Labels[0] ?? CadBlockAuthoring.FreeLabel(target, "Position");
                        handle = target
                            .AddPointParameter(name, options.Descriptions[0] ?? string.Empty, location)
                            .Handle;
                    }
                    else if (kind == "Visibility")
                    {
                        string name = options.Labels[0] ?? CadBlockAuthoring.FreeLabel(target, "Visibility");

                        // The first state shows every object of the block, as AutoCAD's VisibilityState0 does.
                        var first = new DxfBlockVisibilityState(
                            "VisibilityState0",
                            target.Where(e => !e.IsErased).ToList()
                        );
                        target.AddVisibilityParameter(
                            name,
                            options.Descriptions[0] ?? string.Empty,
                            location,
                            new[] { first }
                        );
                        handle = DxfBlockAuthoring
                            .Elements(target)
                            .First(e => e.Kind == DxfBlockElementKind.Visibility)
                            .Handle;
                    }
                    else
                    {
                        target.AddBasePointParameter(location);
                        handle = DxfBlockAuthoring
                            .Elements(target)
                            .First(e => e.Kind == DxfBlockElementKind.BasePoint)
                            .Handle;
                    }

                    Finish(
                        target,
                        handle,
                        options,
                        label.HasValue ? new GeoPoint3(label.Value.X, label.Value.Y, z) : (GeoPoint3?)null
                    );
                }
            );
        }

        /// <summary>Alignment: "Specify base point of alignment or [Name]:", then the direction.</summary>
        private static IEnumerable<CadPrompt> AlignmentParameter(CadCommandContext c, CadBlockEditor editor)
        {
            DxfBlockTableRecord block = editor.Space.Block;

            if (DxfBlockAuthoring.Elements(block).Any(e => e.Kind == DxfBlockElementKind.Alignment))
            {
                c.Write("An alignment parameter exists in the block already.");
                yield break;
            }

            var options = new ParameterOptions();
            GeoPoint2? start = null;

            foreach (
                CadPrompt prompt in FirstPoint(
                    c,
                    block,
                    "Alignment",
                    "Specify base point of alignment",
                    new[] { "Name" },
                    options,
                    p => start = p
                )
            )
            {
                yield return prompt;
            }

            if (!start.HasValue)
            {
                yield break;
            }

            yield return CadPrompt.ForPoint("Specify alignment direction", start);

            if (!c.Input.IsPoint)
            {
                yield break;
            }

            GeoPoint2 end = c.Input.Point;
            string blockName = editor.Name;
            double z = block.Origin.Z;
            GeoPoint2 at = start.Value;

            c.Apply(
                new[] { new CadEditRecord(CadEditRecordKind.TableChanged, null, "Alignment parameter", null, null) },
                s =>
                {
                    DxfBlockTableRecord target = s.Database.BlockTable[blockName];
                    target.AddAlignmentParameter(new GeoPoint3(at.X, at.Y, z), new GeoPoint3(end.X, end.Y, z));
                    DxfBlockElement element = DxfBlockAuthoring
                        .Elements(target)
                        .First(e => e.Kind == DxfBlockElementKind.Alignment);

                    if (!string.IsNullOrWhiteSpace(options.Name))
                    {
                        DxfBlockAuthoring.Rename(element, options.Name);
                    }
                }
            );
        }

        // ----- BACTION -----

        /// <summary>The words of AutoCAD's refusal of a parameter an action cannot follow.</summary>
        private static string InvalidParameter(string action)
        {
            switch (action)
            {
                case "Rotate":
                    return "Invalid selection.  Action requires a Rotation parameter.";
                case "Flip":
                    return "Invalid selection.  Action requires a Flip parameter.";
                case "Polar stretch":
                    return "Invalid selection.  Action requires a Polar parameter.";
                case "Lookup":
                    return "Invalid selection.  Action requires a Lookup parameter.";
                case "Move":
                case "sTretch":
                    return "Invalid selection.  Action requires a Point, Linear, Polar, or XY parameter.";
                default:
                    return "Invalid selection.  Action requires a Linear, Polar, or XY parameter.";
            }
        }

        /// <summary>Whether an action of a kind follows a kind of parameter, in AutoCAD: the parameters its refusal lists.</summary>
        private static bool Follows(string action, DxfBlockElementKind parameter)
        {
            switch (action)
            {
                case "Rotate":
                    return parameter == DxfBlockElementKind.Rotation;
                case "Flip":
                    return parameter == DxfBlockElementKind.Flip;
                case "Polar stretch":
                    return parameter == DxfBlockElementKind.Polar;
                case "Lookup":
                    return parameter == DxfBlockElementKind.Lookup;
                case "Move":
                    return parameter == DxfBlockElementKind.Point
                        || parameter == DxfBlockElementKind.Linear
                        || parameter == DxfBlockElementKind.Polar
                        || parameter == DxfBlockElementKind.XY;
                case "sTretch":
                    return parameter == DxfBlockElementKind.Linear
                        || parameter == DxfBlockElementKind.Polar
                        || parameter == DxfBlockElementKind.XY;
                default:
                    return parameter == DxfBlockElementKind.Linear
                        || parameter == DxfBlockElementKind.Polar
                        || parameter == DxfBlockElementKind.XY;
            }
        }

        /// <summary>BACTION: "Enter action type [...] &lt;Move&gt;:", "Select parameter:", then the prompts of the action.</summary>
        private static IEnumerable<CadPrompt> BlockAction(CadCommandContext c)
        {
            CadBlockEditor editor = AuthoringEditor(c);

            if (editor == null)
            {
                yield break;
            }

            int last = (int)c.Remembered("BACTION.kind", 3.0);
            yield return CadPrompt.ForKeyword("Enter action type", ActionKinds, ActionKinds[last]);
            string kind =
                c.Input.IsNone ? ActionKinds[last]
                : c.Input.Kind == CadInputKind.Keyword ? c.Input.Text
                : null;

            if (kind == null)
            {
                yield break;
            }

            c.Remember("BACTION.kind", Array.IndexOf(ActionKinds, kind));

            if (kind == "Lookup")
            {
                c.Write(
                    "A lookup action works with its lookup table, which the Block Editor of this version does not make."
                );
                yield break;
            }

            // The parameter: a point near its glyph; a pick that finds none or one an action cannot follow asks again.
            CadAuthoringElement parameter = null;

            while (parameter == null)
            {
                yield return CadPrompt.ForPoint("Select parameter");

                if (!c.Input.IsPoint)
                {
                    yield break;
                }

                CadAuthoringElement found = CadBlockAuthoring.ElementAt(
                    editor,
                    c.Input.Point,
                    c.Session.PickAperture * 2.0
                );

                if (found == null || !found.IsParameter)
                {
                    c.Write("Object selected is not a parameter object");
                    continue;
                }

                if (!Follows(kind, found.Kind))
                {
                    c.Write(InvalidParameter(kind));
                    continue;
                }

                parameter = found;
            }

            string name = editor.Name;
            string handle = parameter.Handle;
            DxfBlockElementKind pkind = parameter.Kind;

            // The point of the parameter the action is tied to, asked for a move, a stretch and an array: AutoCAD takes a point near
            // the one meant, or a keyword; the library ties an action to the second point, the end, alone.
            if ((kind == "Move" || kind == "sTretch" || kind == "Array") && pkind != DxfBlockElementKind.Point)
            {
                bool xy = pkind == DxfBlockElementKind.XY;
                string[] points = xy
                    ? new[] { "Base point", "Second point", "Xcorner", "Ycorner" }
                    : new[] { "sTart point", "Second point" };
                DxfBlockElement shown = parameter.Element;

                while (true)
                {
                    yield return CadPrompt
                        .ForPoint(
                            "Specify parameter point to associate with action or enter",
                            null,
                            points,
                            "Second point"
                        )
                        .WithoutOr();

                    if (c.Input.IsNone || c.Input.Is("Second point"))
                    {
                        break;
                    }

                    if (c.Input.IsPoint)
                    {
                        // A point nearer the base point than the end point names the start point.
                        GeoPoint2 at = c.Input.Point;
                        GeoPoint3? b = shown.Base;
                        GeoPoint3? e = shown.End;

                        if (
                            b.HasValue
                            && e.HasValue
                            && (at - new GeoPoint2(b.Value.X, b.Value.Y)).Length
                                < (at - new GeoPoint2(e.Value.X, e.Value.Y)).Length
                        )
                        {
                            c.Write(
                                "Only the second point of a parameter can be associated with an action in this version."
                            );
                            continue;
                        }

                        break;
                    }

                    if (c.Input.Kind != CadInputKind.Keyword)
                    {
                        yield break;
                    }

                    c.Write("Only the second point of a parameter can be associated with an action in this version.");
                }
            }

            switch (kind)
            {
                case "sTretch":
                case "Polar stretch":
                    foreach (CadPrompt prompt in StretchAction(c, editor, handle, pkind, kind == "Polar stretch"))
                    {
                        yield return prompt;
                    }

                    break;
                case "Array":
                    foreach (CadPrompt prompt in ArrayAction(c, editor, handle, pkind))
                    {
                        yield return prompt;
                    }

                    break;
                default:
                    foreach (CadPrompt prompt in SimpleAction(c, editor, handle, pkind, kind))
                    {
                        yield return prompt;
                    }

                    break;
            }
        }

        private static List<DxfEntity> EntitiesOf(CadCommandContext c, IReadOnlyList<string> handles)
        {
            var entities = new List<DxfEntity>();

            foreach (string handle in handles)
            {
                DxfEntity entity = c.Document.FindEntity(handle);

                if (entity != null && !entity.IsErased)
                {
                    entities.Add(entity);
                }
            }

            return entities;
        }

        /// <summary>"Specify action location or [Multiplier/Offset]:": a point, Enter for the library's place; the multiplier and offset as the options give them.</summary>
        private static IEnumerable<CadPrompt> ActionLocation(
            CadCommandContext c,
            bool factors,
            Action<GeoPoint2?, double, double> got
        )
        {
            double multiplier = 1.0,
                offset = 0.0;

            while (true)
            {
                yield return CadPrompt.ForPoint(
                    "Specify action location",
                    null,
                    factors ? new[] { "Multiplier", "Offset" } : null
                );

                if (c.Input.IsPoint)
                {
                    got(c.Input.Point, multiplier, offset);
                    yield break;
                }

                if (c.Input.Is("Multiplier"))
                {
                    yield return CadPrompt.ForDistance(
                        "Enter distance multiplier",
                        null,
                        null,
                        multiplier.ToString("0.0000", CultureInfo.InvariantCulture)
                    );

                    if (c.Input.Kind == CadInputKind.Number && c.Input.Number != 0.0)
                    {
                        multiplier = c.Input.Number;
                    }
                    else if (!c.Input.IsNone)
                    {
                        c.Write("Requires numeric nonzero value.");
                    }

                    continue;
                }

                if (c.Input.Is("Offset"))
                {
                    yield return CadPrompt.ForAngle("Enter angle offset", null, null, "0");

                    if (c.Input.Kind == CadInputKind.Number)
                    {
                        offset = c.Input.Number;
                    }

                    continue;
                }

                // Enter takes the place the library gives the action.
                got(null, multiplier, offset);
                yield break;
            }
        }

        /// <summary>Move, Scale, Rotate and Flip: the objects, then the action location.</summary>
        private static IEnumerable<CadPrompt> SimpleAction(
            CadCommandContext c,
            CadBlockEditor editor,
            string handle,
            DxfBlockElementKind pkind,
            string kind
        )
        {
            yield return CadPrompt.ForSelection("Select objects");
            IReadOnlyList<string> picked = c.Input.Handles;

            if (picked.Count == 0)
            {
                yield break;
            }

            GeoPoint2? at = null;
            double multiplier = 1.0,
                offset = 0.0;

            foreach (CadPrompt prompt in ActionLocation(c, kind == "Move", (p, m, o) =>
                {
                    at = p;
                    multiplier = m;
                    offset = o;
                }))
            {
                yield return prompt;
            }

            List<DxfEntity> entities = EntitiesOf(c, picked);
            string blockName = editor.Name;
            double z = editor.Space.Block.Origin.Z;
            GeoPoint2? where = at;

            c.Apply(
                new[] { new CadEditRecord(CadEditRecordKind.TableChanged, null, kind + " action", null, null) },
                s =>
                {
                    DxfBlockTableRecord block = s.Database.BlockTable[blockName];
                    DxfBlockElement parameter = CadBlockAuthoring.Find(block, handle);
                    object typed = parameter?.Parameter
                        ?? throw new InvalidOperationException("The parameter is no longer in the block.");
                    IEnumerable<DxfEntity> acted = entities;

                    switch (kind)
                    {
                        case "Move":
                            if (typed is DxfBlockLinearParameter linear)
                            {
                                block.AddMoveAction(linear, acted, multiplier, offset);
                            }
                            else if (typed is DxfBlockXYParameter xy)
                            {
                                block.AddMoveAction(xy, acted, multiplier, offset);
                            }
                            else if (typed is DxfBlockPolarParameter polar)
                            {
                                block.AddMoveAction(polar, acted, multiplier, offset);
                            }
                            else if (typed is DxfBlockPointParameter point)
                            {
                                block.AddMoveAction(point, acted, multiplier, offset);
                            }

                            break;
                        case "Scale":
                            if (!(typed is DxfBlockLinearParameter scaled))
                            {
                                throw new NotSupportedException(
                                    "A scale action follows a linear parameter in this version."
                                );
                            }

                            block.AddScaleAction(scaled, acted);
                            break;
                        case "Rotate":
                            block.AddRotateAction((DxfBlockRotationParameter)typed, acted);
                            break;
                        default:
                            block.AddFlipAction((DxfBlockFlipParameter)typed, acted);
                            break;
                    }

                    PlaceLast(block, where, z);
                }
            );
        }

        /// <summary>The newest action of a block put at the point given, at AutoCAD's action location.</summary>
        private static void PlaceLast(DxfBlockTableRecord block, GeoPoint2? at, double z)
        {
            if (!at.HasValue)
            {
                return;
            }

            DxfBlockElement last = DxfBlockAuthoring.Elements(block).LastOrDefault(e => !e.IsParameter);

            if (last != null)
            {
                DxfBlockAuthoring.SetLocation(last, new GeoPoint3(at.Value.X, at.Value.Y, z));
            }
        }

        /// <summary>Stretch and Polar stretch: the frame, the objects to stretch, for a polar stretch the objects to turn only, then the action location.</summary>
        private static IEnumerable<CadPrompt> StretchAction(
            CadCommandContext c,
            CadBlockEditor editor,
            string handle,
            DxfBlockElementKind pkind,
            bool polar
        )
        {
            yield return CadPrompt.ForPoint("Specify first corner of stretch frame");

            if (!c.Input.IsPoint)
            {
                yield break;
            }

            GeoPoint2 corner = c.Input.Point;
            yield return CadPrompt.ForPoint("Specify opposite corner", corner);

            if (!c.Input.IsPoint)
            {
                yield break;
            }

            GeoPoint2 opposite = c.Input.Point;
            c.Write("Specify objects to stretch");
            yield return CadPrompt.ForSelection("Select objects");
            IReadOnlyList<string> stretched = c.Input.Handles;

            if (stretched.Count == 0)
            {
                yield break;
            }

            IReadOnlyList<string> turned = new string[0];

            if (polar)
            {
                c.Write("Specify objects to rotate only");
                yield return CadPrompt.ForSelection("Select objects");
                turned = c.Input.Handles;
            }

            GeoPoint2? at = null;
            double multiplier = 1.0,
                offset = 0.0;

            foreach (CadPrompt prompt in ActionLocation(c, true, (p, m, o) =>
                {
                    at = p;
                    multiplier = m;
                    offset = o;
                }))
            {
                yield return prompt;
            }

            List<DxfEntity> entities = EntitiesOf(c, stretched);
            List<DxfEntity> rotateOnly = EntitiesOf(c, turned);
            string blockName = editor.Name;
            double z = editor.Space.Block.Origin.Z;
            GeoPoint2? where = at;

            c.Apply(
                new[]
                {
                    new CadEditRecord(
                        CadEditRecordKind.TableChanged,
                        null,
                        polar ? "Polar stretch action" : "Stretch action",
                        null,
                        null
                    ),
                },
                s =>
                {
                    DxfBlockTableRecord block = s.Database.BlockTable[blockName];
                    DxfBlockElement parameter = CadBlockAuthoring.Find(block, handle);
                    object typed = parameter?.Parameter
                        ?? throw new InvalidOperationException("The parameter is no longer in the block.");

                    if (polar)
                    {
                        block.AddPolarStretchAction(
                            (DxfBlockPolarParameter)typed,
                            corner,
                            opposite,
                            entities,
                            rotateOnly
                        );
                    }
                    else if (typed is DxfBlockLinearParameter linear)
                    {
                        block.AddStretchAction(linear, corner, opposite, entities, multiplier, offset);
                    }
                    else if (typed is DxfBlockXYParameter xy)
                    {
                        block.AddStretchAction(xy, corner, opposite, entities, multiplier, offset);
                    }
                    else if (typed is DxfBlockPolarParameter polarParameter)
                    {
                        block.AddStretchAction(polarParameter, corner, opposite, entities, multiplier, offset);
                    }

                    PlaceLast(block, where, z);
                }
            );
        }

        /// <summary>Array: the objects, the distance between rows (an XY parameter) and between columns.</summary>
        private static IEnumerable<CadPrompt> ArrayAction(
            CadCommandContext c,
            CadBlockEditor editor,
            string handle,
            DxfBlockElementKind pkind
        )
        {
            yield return CadPrompt.ForSelection("Select objects");
            IReadOnlyList<string> picked = c.Input.Handles;

            if (picked.Count == 0)
            {
                yield break;
            }

            double rows = 0.0;

            if (pkind == DxfBlockElementKind.XY)
            {
                yield return CadPrompt.ForDistance("Enter the distance between rows or specify unit cell (---)");

                if (c.Input.Kind != CadInputKind.Number)
                {
                    yield break;
                }

                rows = c.Input.Number;
            }

            yield return CadPrompt.ForDistance("Enter the distance between columns (|||)");

            if (c.Input.Kind != CadInputKind.Number)
            {
                yield break;
            }

            double columns = c.Input.Number;
            List<DxfEntity> entities = EntitiesOf(c, picked);
            string blockName = editor.Name;

            c.Apply(new[] { new CadEditRecord(CadEditRecordKind.TableChanged, null, "Array action", null, null) }, s =>
            {
                DxfBlockTableRecord block = s.Database.BlockTable[blockName];
                DxfBlockElement parameter = CadBlockAuthoring.Find(block, handle);
                object typed = parameter?.Parameter
                    ?? throw new InvalidOperationException("The parameter is no longer in the block.");

                if (typed is DxfBlockXYParameter xy)
                {
                    block.AddArrayAction(xy, entities, columns, rows);
                }
                else if (typed is DxfBlockLinearParameter linear)
                {
                    block.AddArrayAction(linear, entities, columns);
                }
                else
                {
                    throw new NotSupportedException(
                        "An array action follows a linear or an XY parameter in this version."
                    );
                }
            });
        }

        // ----- BVSTATE, BVHIDE, BVSHOW -----

        /// <summary>BVSTATE: the Visibility States dialog; with no window, -BVSTATE's prompts.</summary>
        private static IEnumerable<CadPrompt> BlockVisibilityStates(CadCommandContext c)
        {
            CadBlockEditor editor = AuthoringEditor(c);

            if (editor == null)
            {
                yield break;
            }

            if (!editor.Space.Block.GetVisibilityStates().Any())
            {
                c.Write("** No Visibility Parameter Present. **");
                yield break;
            }

            if (c.Session.Request("BVSTATE", c.Document, new CadVisibilityStatesRequest(c.Document)))
            {
                yield break;
            }

            foreach (CadPrompt prompt in BlockVisibilityStatesTyped(c))
            {
                yield return prompt;
            }
        }

        /// <summary>-BVSTATE: "Enter an option [New/Set/Delete] &lt;Set&gt;:".</summary>
        private static IEnumerable<CadPrompt> BlockVisibilityStatesTyped(CadCommandContext c)
        {
            CadBlockEditor editor = AuthoringEditor(c);

            if (editor == null)
            {
                yield break;
            }

            if (!editor.Space.Block.GetVisibilityStates().Any())
            {
                c.Write("** No Visibility Parameter Present. **");
                yield break;
            }

            yield return CadPrompt.ForKeyword("Enter an option", new[] { "New", "Set", "Delete" }, "Set");
            string option = c.Input.IsNone ? "Set" : c.Input.Text;

            if (option == "New")
            {
                yield return CadPrompt.ForString("Enter name of new visibility state");
                string name = c.Input.Text;

                if (c.Input.IsNone || name == null)
                {
                    yield break;
                }

                yield return CadPrompt.ForKeyword(
                    "Enter object visibility for new state",
                    new[] { "Hide all", "Show all", "Current visibility" },
                    "Show all"
                );
                string how = c.Input.IsNone ? "Show all" : c.Input.Text;
                CadEditResult result = c.Document.AddVisibilityState(name, how);

                if (!result.Succeeded)
                {
                    c.Write(result.Message);
                }
            }
            else if (option == "Delete")
            {
                yield return CadPrompt.ForString("Enter visibility state name(s) to delete");

                if (c.Input.IsNone || c.Input.Text == null)
                {
                    yield break;
                }

                CadEditResult result = c.Document.DeleteVisibilityStates(
                    c.Input.Text.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(n => n.Trim())
                        .ToList()
                );

                if (!result.Succeeded)
                {
                    c.Write(result.Message);
                }
            }
            else
            {
                while (true)
                {
                    yield return CadPrompt.ForKeywordOrText(
                        "Enter visibility state name to make current or",
                        new[] { "?" },
                        null
                    );

                    if (c.Input.Is("?"))
                    {
                        c.Write("Current visibility state: " + (editor.VisibilityState ?? string.Empty));

                        foreach (string state in editor.VisibilityStates)
                        {
                            c.Write("  " + state);
                        }

                        continue;
                    }

                    if (c.Input.Kind != CadInputKind.Text)
                    {
                        yield break;
                    }

                    CadEditResult result = c.Document.SetBlockEditorVisibilityState(c.Input.Text.Trim());

                    if (!result.Succeeded)
                    {
                        c.Write("Invalid visibility state.");
                        continue;
                    }

                    yield break;
                }
            }
        }

        /// <summary>BVHIDE and BVSHOW: "Select objects to hide:", then "Hide for current state or all visibility states [Current/All] &lt;Current&gt;:".</summary>
        private static IEnumerable<CadPrompt> BlockVisibilityObjects(CadCommandContext c, bool show)
        {
            CadBlockEditor editor = AuthoringEditor(c);

            if (editor == null)
            {
                yield break;
            }

            if (!editor.Space.Block.GetVisibilityStates().Any())
            {
                c.Write("** No Visibility Parameter Present. **");
                yield break;
            }

            yield return CadPrompt.ForSelection(show ? "Select objects to make visible" : "Select objects to hide");
            IReadOnlyList<string> picked = c.Input.Handles;

            if (picked.Count == 0)
            {
                yield break;
            }

            yield return CadPrompt.ForKeyword(
                show
                    ? "Make visible for current state or all visibility states"
                    : "Hide for current state or all visibility states",
                new[] { "Current", "All" },
                "Current"
            );
            bool all = c.Input.Is("All");
            CadEditResult result = c.Document.SetObjectVisibility(picked, show, all);

            if (!result.Succeeded)
            {
                c.Write(result.Message);
            }
        }
    }

    /// <summary>
    /// The Visibility States dialog of BVSTATE, as data the command hands the window: the document whose Block Editor is open.
    /// The window changes the states through the document (each change an undo step), so that its list is read again from
    /// <see cref="CadBlockEditor.VisibilityStates"/> after each.
    /// </summary>
    public sealed class CadVisibilityStatesRequest
    {
        internal CadVisibilityStatesRequest(CadDocument document)
        {
            Document = document;
        }

        /// <summary>Gets the document the dialog works on.</summary>
        public CadDocument Document { get; }
    }

    /// <summary>
    /// The Block Authoring Palettes, as data BAUTHORPALETTE and BAUTHORPALETTECLOSE hand the window: shown or closed. The palette
    /// is a window of its own that starts BPARAMETER and BACTION with their kind answered.
    /// </summary>
    public sealed class CadAuthoringPaletteRequest
    {
        internal CadAuthoringPaletteRequest(bool show)
        {
            Show = show;
        }

        /// <summary>Gets whether the palettes are to be shown, else closed.</summary>
        public bool Show { get; }
    }

    /// <summary>
    /// The Test Block window of BTESTBLOCK, as data the command hands the window: a copy of the drawing with the block as the
    /// editor holds it, inserted alone in model space, and the name the window is given. The window opens it as a drawing.
    /// </summary>
    public sealed class CadTestBlockRequest
    {
        internal CadTestBlockRequest(DxfDatabase database, string title)
        {
            Database = database;
            Title = title;
        }

        /// <summary>Gets the copy of the drawing the block is tried in.</summary>
        public DxfDatabase Database { get; }

        /// <summary>Gets the title of the window: "Test Block Window - DOOR".</summary>
        public string Title { get; }
    }
}
