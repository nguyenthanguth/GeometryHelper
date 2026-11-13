using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// MLSTYLE typed. AutoCAD 2021 has only its Multiline Styles dialog (the console hangs on it and has no -MLSTYLE), so the
    /// questions here are the presenter's own, the dialog's fields in the order it shows them: the current style, a new one,
    /// one modified, with its description, elements (offset, colour, linetype), caps, fill, joints and angles. They are asked
    /// where no window shows the dialog (CadCommands.MlineStyleDialog.cs).
    /// </summary>
    internal static partial class CadCommands
    {
        /// <summary>
        /// MLSTYLE: "Current multiline style: STANDARD", then "Enter an option [?/Current/New/Modify/Rename/Delete/Load/Save] &lt;exit&gt;:". Current
        /// makes a style current as CMLSTYLE does, New asks a name and then the style's options, Modify the same for a style of
        /// the drawing; a style a multiline is drawn by, and STANDARD, can only have its description changed, as AutoCAD's
        /// dialog allows; Rename, Delete, Load and Save as its buttons (CadCommands.MlineStyleFiles). The name of a style is
        /// kept in capitals, as AutoCAD keeps it.
        /// </summary>
        private static IEnumerable<CadPrompt> MlineStyleTyped(CadCommandContext c)
        {
            EnsureStandardMlineStyle(c);

            while (true)
            {
                c.Write("Current multiline style: " + CurrentMlineStyleName(c.Database));
                yield return CadPrompt.ForKeyword(
                    "Enter an option",
                    new[] { "?", "Current", "New", "Modify", "Rename", "Delete", "Load", "Save" },
                    "exit"
                );

                if (c.Input.Kind != CadInputKind.Keyword)
                {
                    yield break;
                }

                if (c.Input.Is("Rename") || c.Input.Is("Delete") || c.Input.Is("Load") || c.Input.Is("Save"))
                {
                    foreach (
                        CadPrompt prompt in c.Input.Is("Rename") ? RenameMlineStyle(c)
                        : c.Input.Is("Delete") ? DeleteMlineStyle(c)
                        : c.Input.Is("Load") ? LoadMlineStyle(c)
                        : SaveMlineStyle(c)
                    )
                    {
                        yield return prompt;
                    }

                    continue;
                }

                if (c.Input.Is("?"))
                {
                    ListMlineStyles(c);
                }
                else if (c.Input.Is("Current"))
                {
                    yield return CadPrompt.ForString(
                        "Enter name of the style to make current",
                        CurrentMlineStyleName(c.Database)
                    );
                    string typed = c.Input.Kind == CadInputKind.Text ? c.Input.Text.Trim() : string.Empty;
                    DxfMlineStyle found = typed.Length == 0 ? null : FindMlineStyle(c.Database, typed);

                    if (typed.Length > 0 && found == null)
                    {
                        c.Write("Cannot find multiline style \"" + typed + "\".");
                    }
                    else if (found != null)
                    {
                        SetMlineStyle(c, found);
                    }
                }
                else if (c.Input.Is("New"))
                {
                    yield return CadPrompt.ForString("Enter name of new multiline style");
                    string name = c.Input.Kind == CadInputKind.Text ? c.Input.Text.Trim() : string.Empty;

                    if (name.Length == 0)
                    {
                        continue;
                    }

                    string refusal = MlineStyleNameRefusal(c.Database, name);

                    if (refusal != null)
                    {
                        c.Write(refusal);
                        continue;
                    }

                    var settings = new DxfMlineStyleSettings();

                    foreach (CadPrompt prompt in MlineStyleOptions(c, settings, false))
                    {
                        yield return prompt;
                    }

                    c.Apply(
                        new[]
                        {
                            new CadEditRecord(CadEditRecordKind.TableChanged, null, "Multiline styles", null, name),
                        },
                        s => s.Database.AddMlineStyle(name, settings)
                    );
                }
                else
                {
                    yield return CadPrompt.ForString(
                        "Enter name of the style to modify",
                        CurrentMlineStyleName(c.Database)
                    );
                    string typed = c.Input.Kind == CadInputKind.Text ? c.Input.Text.Trim() : string.Empty;
                    DxfMlineStyle style = typed.Length == 0 ? null : FindMlineStyle(c.Database, typed);

                    if (typed.Length > 0 && style == null)
                    {
                        c.Write("Cannot find multiline style \"" + typed + "\".");
                        continue;
                    }

                    if (style == null)
                    {
                        continue;
                    }

                    bool locked =
                        string.Equals(style.Name, "STANDARD", StringComparison.OrdinalIgnoreCase)
                        || MlineStyleInUse(c.Database, style);
                    DxfMlineStyleSettings settings = style.GetSettings();

                    if (locked)
                    {
                        c.Write(
                            "The elements and caps of "
                                + style.Name
                                + " cannot be changed, as it is STANDARD or a multiline is drawn by it: only its description can."
                        );
                    }

                    foreach (CadPrompt prompt in MlineStyleOptions(c, settings, locked))
                    {
                        yield return prompt;
                    }

                    c.Apply(
                        new[]
                        {
                            new CadEditRecord(
                                CadEditRecordKind.TableChanged,
                                null,
                                "Multiline styles",
                                null,
                                style.Name
                            ),
                        },
                        s => style.Apply(settings)
                    );
                }
            }
        }

        /// <summary>Why a name cannot be given to a new style: none for a name AutoCAD takes that the drawing has not.</summary>
        private static string MlineStyleNameRefusal(DxfDatabase database, string name)
        {
            try
            {
                DxfSymbolTable.CheckName(name, "name");
            }
            catch (ArgumentException e)
            {
                return e.Message.Split('\r', '\n')[0].Replace(" (Parameter 'name')", string.Empty);
            }

            return FindMlineStyle(database, name) != null ? "Multiline style " + name + " already exists." : null;
        }

        /// <summary>Whether a multiline of the drawing is drawn by the style: one in any block, by the style's handle or name.</summary>
        private static bool MlineStyleInUse(DxfDatabase database, DxfMlineStyle style)
        {
            foreach (DxfBlockTableRecord block in database.BlockTable)
            {
                foreach (DxfEntity entity in block)
                {
                    if (entity is DxfUnknownEntity mline && mline.DxfName == "MLINE")
                    {
                        DxfMline record = DxfMline.Read(mline.MlineGroups());

                        if (
                            string.Equals(record.StyleHandle, style.Handle, StringComparison.OrdinalIgnoreCase)
                            || string.Equals(record.StyleName, style.Name, StringComparison.OrdinalIgnoreCase)
                        )
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        /// <summary>The questions of a style's options, until Done: description, elements, caps, fill, joints and angles.</summary>
        private static IEnumerable<CadPrompt> MlineStyleOptions(
            CadCommandContext c,
            DxfMlineStyleSettings settings,
            bool descriptionOnly
        )
        {
            while (true)
            {
                yield return CadPrompt.ForKeyword(
                    "Enter style option",
                    descriptionOnly
                        ? new[] { "Description", "Done" }
                        : new[] { "Description", "Elements", "Caps", "Fill", "Joints", "Angles", "Done" },
                    "Done"
                );

                if (c.Input.Kind != CadInputKind.Keyword || c.Input.Is("Done"))
                {
                    yield break;
                }

                if (c.Input.Is("Description"))
                {
                    yield return CadPrompt.ForString("Enter description", settings.Description);

                    if (c.Input.Kind == CadInputKind.Text)
                    {
                        settings.Description = c.Input.Text.Trim();
                    }
                }
                else if (c.Input.Is("Elements"))
                {
                    foreach (CadPrompt prompt in MlineStyleElements(c, settings))
                    {
                        yield return prompt;
                    }
                }
                else if (c.Input.Is("Caps"))
                {
                    foreach (CadPrompt prompt in MlineStyleCaps(c, settings))
                    {
                        yield return prompt;
                    }
                }
                else if (c.Input.Is("Fill"))
                {
                    yield return CadPrompt.ForKeyword(
                        "Enter fill option",
                        new[] { "Off", "Color" },
                        settings.Filled ? "Color" : "Off"
                    );

                    if (c.Input.Is("Off"))
                    {
                        settings.Filled = false;
                    }
                    else if (c.Input.Is("Color"))
                    {
                        yield return CadPrompt.ForString("Enter fill color", ColorText(settings.FillColor));
                        DxfColor color = c.Input.Kind == CadInputKind.Text ? ElementColor(c, c.Input.Text) : null;

                        if (color != null)
                        {
                            settings.FillColor = color;
                            settings.Filled = true;
                        }
                        else if (c.Input.Kind != CadInputKind.Text)
                        {
                            settings.Filled = true;
                        }
                    }
                }
                else if (c.Input.Is("Joints"))
                {
                    yield return CadPrompt.ForKeyword(
                        "Display joints",
                        new[] { "Yes", "No" },
                        settings.ShowMiters ? "Yes" : "No"
                    );

                    if (c.Input.Kind == CadInputKind.Keyword)
                    {
                        settings.ShowMiters = c.Input.Is("Yes");
                    }
                }
                else
                {
                    foreach (CadPrompt prompt in MlineStyleAngles(c, settings))
                    {
                        yield return prompt;
                    }
                }
            }
        }

        private static string ColorText(DxfColor color) =>
            color.IsByLayer ? "BYLAYER"
            : color.IsByBlock ? "BYBLOCK"
            : color.ColorIndex.ToString(CultureInfo.InvariantCulture);

        /// <summary>A colour typed for an element or the fill: by layer, by block, a name or an index; null with the reason written where it is not one.</summary>
        private static DxfColor ElementColor(CadCommandContext c, string typed)
        {
            string error = CadPropertyEditor.ParseColor(typed.Trim(), out DxfColor color);

            if (error == null && color.IsByColor)
            {
                error = "A multiline style keeps a colour as an index: by layer, by block or 1 to 255.";
            }

            if (error != null)
            {
                c.Write(error);
                return null;
            }

            return color;
        }

        /// <summary>A linetype typed for an element: BYLAYER, BYBLOCK or one the drawing has, as the drawing names it; null with the reason written where it has none.</summary>
        private static string ElementLinetype(CadCommandContext c, string typed)
        {
            string name = typed.Trim();

            if (string.Equals(name, "ByLayer", StringComparison.OrdinalIgnoreCase))
            {
                return "BYLAYER";
            }

            if (string.Equals(name, "ByBlock", StringComparison.OrdinalIgnoreCase))
            {
                return "BYBLOCK";
            }

            string found = c
                .Database.LinetypeTable.Select(l => l.Name)
                .FirstOrDefault(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));

            if (found == null)
            {
                c.Write("Linetype " + name + " not found. Use LINETYPE to load it.");
            }

            return found;
        }

        /// <summary>The elements of a style written as the list the questions are about, the greatest offset first, as the record keeps them.</summary>
        private static void SortElements(DxfMlineStyleSettings settings)
        {
            List<DxfMlineStyleElement> sorted = settings
                .Elements.Select((element, index) => new { element, index })
                .OrderByDescending(e => e.element.Offset)
                .ThenBy(e => e.index)
                .Select(e => e.element)
                .ToList();
            settings.Elements.Clear();

            foreach (DxfMlineStyleElement element in sorted)
            {
                settings.Elements.Add(element);
            }
        }

        private static IEnumerable<CadPrompt> MlineStyleElements(CadCommandContext c, DxfMlineStyleSettings settings)
        {
            while (true)
            {
                SortElements(settings);

                for (int i = 0; i < settings.Elements.Count; i++)
                {
                    DxfMlineStyleElement element = settings.Elements[i];
                    c.Write(
                        "  "
                            + (i + 1).ToString(CultureInfo.InvariantCulture)
                            + "  Offset "
                            + element.Offset.ToString("0.####", CultureInfo.InvariantCulture)
                            + "  Color "
                            + ColorText(element.Color)
                            + "  Linetype "
                            + element.Linetype
                    );
                }

                yield return CadPrompt.ForKeyword(
                    "Enter element option",
                    new[] { "Add", "Delete", "Change", "Done" },
                    "Done"
                );

                if (c.Input.Kind != CadInputKind.Keyword || c.Input.Is("Done"))
                {
                    yield break;
                }

                bool deleting = c.Input.Is("Delete");
                int at = -1;

                if (c.Input.Is("Add"))
                {
                    if (settings.Elements.Count >= DxfMlineStyleSettings.MostElements)
                    {
                        c.Write("A multiline style has at most 16 elements.");
                        continue;
                    }
                }
                else
                {
                    yield return CadPrompt.ForInteger(
                        deleting ? "Enter number of the element to delete" : "Enter number of the element to change"
                    );

                    at = c.Input.Kind == CadInputKind.Number ? (int)c.Input.Number - 1 : -1;

                    if (at < 0 || at >= settings.Elements.Count)
                    {
                        continue;
                    }
                }

                if (deleting)
                {
                    if (settings.Elements.Count == 1)
                    {
                        c.Write("A multiline style has at least one element.");
                    }
                    else
                    {
                        settings.Elements.RemoveAt(at);
                    }

                    continue;
                }

                DxfMlineStyleElement kept = at >= 0 ? settings.Elements[at] : null;
                yield return CadPrompt.ForDistance(
                    "Specify offset of the element",
                    null,
                    null,
                    kept == null ? null : kept.Offset.ToString("0.####", CultureInfo.InvariantCulture)
                );
                double offset = c.Input.Kind == CadInputKind.Number ? c.Input.Number : kept?.Offset ?? double.NaN;

                if (double.IsNaN(offset))
                {
                    continue;
                }

                yield return CadPrompt.ForString(
                    "Enter color (1 to 255, BYLAYER or BYBLOCK)",
                    kept == null ? "BYLAYER" : ColorText(kept.Color)
                );
                DxfColor color =
                    c.Input.Kind == CadInputKind.Text ? ElementColor(c, c.Input.Text) : kept?.Color ?? DxfColor.ByLayer;

                if (color == null)
                {
                    continue;
                }

                yield return CadPrompt.ForString("Enter linetype name", kept == null ? "BYLAYER" : kept.Linetype);
                string linetype =
                    c.Input.Kind == CadInputKind.Text ? ElementLinetype(c, c.Input.Text) : kept?.Linetype ?? "BYLAYER";

                if (linetype == null)
                {
                    continue;
                }

                var made = new DxfMlineStyleElement(offset, color, linetype);

                if (at >= 0)
                {
                    settings.Elements[at] = made;
                }
                else
                {
                    settings.Elements.Add(made);
                }
            }
        }

        private static IEnumerable<CadPrompt> MlineStyleCaps(CadCommandContext c, DxfMlineStyleSettings settings)
        {
            while (true)
            {
                yield return CadPrompt.ForKeyword(
                    "Enter cap to change",
                    new[] { "StartLine", "StartOuter", "StartInner", "EndLine", "EndOuter", "EndInner", "Done" },
                    "Done"
                );

                if (c.Input.Kind != CadInputKind.Keyword || c.Input.Is("Done"))
                {
                    yield break;
                }

                string cap = c.Input.Text;
                bool on;

                if (c.Input.Is("StartLine"))
                {
                    on = settings.StartSquareCap = !settings.StartSquareCap;
                }
                else if (c.Input.Is("StartOuter"))
                {
                    on = settings.StartRoundCap = !settings.StartRoundCap;
                }
                else if (c.Input.Is("StartInner"))
                {
                    on = settings.StartInnerArcs = !settings.StartInnerArcs;
                }
                else if (c.Input.Is("EndLine"))
                {
                    on = settings.EndSquareCap = !settings.EndSquareCap;
                }
                else if (c.Input.Is("EndOuter"))
                {
                    on = settings.EndRoundCap = !settings.EndRoundCap;
                }
                else
                {
                    on = settings.EndInnerArcs = !settings.EndInnerArcs;
                }

                c.Write(cap + ": " + (on ? "On" : "Off"));
            }
        }

        private static IEnumerable<CadPrompt> MlineStyleAngles(CadCommandContext c, DxfMlineStyleSettings settings)
        {
            foreach (bool start in new[] { true, false })
            {
                double degrees = (start ? settings.StartAngle : settings.EndAngle) * 180.0 / Math.PI;
                yield return CadPrompt.ForDistance(
                    start ? "Enter angle of the start" : "Enter angle of the end",
                    null,
                    null,
                    Math.Round(degrees, 4).ToString("0.####", CultureInfo.InvariantCulture)
                );

                if (c.Input.Kind != CadInputKind.Number)
                {
                    continue;
                }

                if (c.Input.Number < 10.0 || c.Input.Number > 170.0)
                {
                    c.Write("An angle is from 10 to 170 degrees.");
                    continue;
                }

                if (start)
                {
                    settings.StartAngle = c.Input.Number * Math.PI / 180.0;
                }
                else
                {
                    settings.EndAngle = c.Input.Number * Math.PI / 180.0;
                }
            }
        }
    }
}
