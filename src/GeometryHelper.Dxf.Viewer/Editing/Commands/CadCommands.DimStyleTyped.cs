using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// -DIMSTYLE and DIMUPDATE, as AutoCAD 2021 asks them on the command line (probed in its console). -DIMSTYLE writes the
    /// current style and its overrides (the dimension variables that differ from the style's record), then asks "Enter a
    /// dimension style option [ANnotative/Save/Restore/STatus/Variables/Apply/?] &lt;Restore&gt;:": Save keeps the current
    /// variables, overrides and all, as a style that becomes current; ANnotative does so as an annotative style, or not; Restore
    /// makes a style, or the style and overrides of a dimension picked, current; STatus lists every variable; Variables those of
    /// a style, or the overrides of a dimension picked; Apply gives the current style and overrides to the dimensions, leaders
    /// and feature control frames selected, and DIMUPDATE (the Annotate tab's Update) does the same; ? lists the styles.
    /// </summary>
    internal static partial class CadCommands
    {
        // What AutoCAD 2021 says each variable is in a listing, by the library's name. DIMALTMZF, DIMALTMZS, DIMMZF and DIMMZS,
        // which it lists, are not kept by the library and so are not listed.
        private static readonly Dictionary<string, string> DimDescriptions = new Dictionary<string, string>(
            StringComparer.Ordinal
        )
        {
            ["Dimadec"] = "Angular decimal places",
            ["Dimalt"] = "Alternate units selected",
            ["Dimaltd"] = "Alternate unit decimal places",
            ["Dimaltf"] = "Alternate unit scale factor",
            ["Dimaltrnd"] = "Alternate units rounding value",
            ["Dimalttd"] = "Alternate tolerance decimal places",
            ["Dimalttz"] = "Alternate tolerance zero suppression",
            ["Dimaltu"] = "Alternate units",
            ["Dimaltz"] = "Alternate unit zero suppression",
            ["Dimapost"] = "Prefix and suffix for alternate text",
            ["Dimarcsym"] = "Arc length symbol",
            ["Dimasz"] = "Arrow size",
            ["Dimatfit"] = "Arrow and text fit",
            ["Dimaunit"] = "Angular unit format",
            ["Dimazin"] = "Angular zero supression",
            ["Dimblk"] = "Arrow block name",
            ["Dimblk1"] = "First arrow block name",
            ["Dimblk2"] = "Second arrow block name",
            ["Dimcen"] = "Center mark size",
            ["Dimclrd"] = "Dimension line and leader color",
            ["Dimclre"] = "Extension line color",
            ["Dimclrt"] = "Dimension text color",
            ["Dimdec"] = "Decimal places",
            ["Dimdle"] = "Dimension line extension",
            ["Dimdli"] = "Dimension line spacing",
            ["Dimdsep"] = "Decimal separator",
            ["Dimexe"] = "Extension above dimension line",
            ["Dimexo"] = "Extension line origin offset",
            ["Dimfrac"] = "Fraction format",
            ["Dimfxlen"] = "Fixed Extension Line",
            ["DimfxlenOn"] = "Enable Fixed Extension Line",
            ["Dimgap"] = "Gap from dimension line to text",
            ["Dimjogang"] = "Radius dimension jog angle",
            ["Dimjust"] = "Justification of text on dimension line",
            ["Dimldrblk"] = "Leader block name",
            ["Dimlfac"] = "Linear unit scale factor",
            ["Dimlim"] = "Generate dimension limits",
            ["Dimltex1"] = "Linetype extension line 1",
            ["Dimltex2"] = "Linetype extension line 2",
            ["Dimltype"] = "Dimension linetype",
            ["Dimlunit"] = "Linear unit format",
            ["Dimlwd"] = "Dimension line and leader lineweight",
            ["Dimlwe"] = "Extension line lineweight",
            ["Dimpost"] = "Prefix and suffix for dimension text",
            ["Dimrnd"] = "Rounding value",
            ["Dimsah"] = "Separate arrow blocks",
            ["Dimscale"] = "Overall scale factor",
            ["Dimsd1"] = "Suppress the first dimension line",
            ["Dimsd2"] = "Suppress the second dimension line",
            ["Dimse1"] = "Suppress the first extension line",
            ["Dimse2"] = "Suppress the second extension line",
            ["Dimsoxd"] = "Suppress outside dimension lines",
            ["Dimtad"] = "Place text above the dimension line",
            ["Dimtdec"] = "Tolerance decimal places",
            ["Dimtfac"] = "Tolerance text height scaling factor",
            ["Dimtfill"] = "Text background enabled",
            ["Dimtfillclr"] = "Text background color",
            ["Dimtih"] = "Text inside extensions is horizontal",
            ["Dimtix"] = "Place text inside extensions",
            ["Dimtm"] = "Minus tolerance",
            ["Dimtmove"] = "Text movement",
            ["Dimtofl"] = "Force line inside extension lines",
            ["Dimtoh"] = "Text outside horizontal",
            ["Dimtol"] = "Tolerance dimensioning",
            ["Dimtolj"] = "Tolerance vertical justification",
            ["Dimtp"] = "Plus tolerance",
            ["Dimtsz"] = "Tick size",
            ["Dimtvp"] = "Text vertical position",
            ["Dimtxsty"] = "Text style",
            ["Dimtxt"] = "Text height",
            ["Dimtxtdirection"] = "Dimension text direction",
            ["Dimtzin"] = "Tolerance zero suppression",
            ["Dimupt"] = "User positioned text",
            ["Dimzin"] = "Zero suppression",
        };

        private static void AddDimStyleTypedTo(CadCommandRegistry registry)
        {
            registry.Add(new CadCommand("-DIMSTYLE", DimStyleTyped));
            registry.Add(new CadCommand("DIMUPDATE", DimStyleUpdate, true));
        }

        private static IEnumerable<CadPrompt> DimStyleTyped(CadCommandContext c)
        {
            WriteCurrentDimStyle(c);

            while (true)
            {
                yield return CadPrompt.ForKeyword(
                    "Enter a dimension style option",
                    new[] { "ANnotative", "Save", "Restore", "STatus", "Variables", "Apply", "?" },
                    "Restore"
                );

                if (c.Input.Is("ANnotative"))
                {
                    yield return CadPrompt.ForKeyword(
                        "Create annotative dimension style",
                        new[] { "Yes", "No" },
                        "Yes"
                    );

                    foreach (CadPrompt prompt in SaveDimStyle(c, !c.Input.Is("No")))
                    {
                        yield return prompt;
                    }

                    // AutoCAD asks for the option again after it, as it does not after Save.
                    continue;
                }

                if (c.Input.Is("Save"))
                {
                    foreach (CadPrompt prompt in SaveDimStyle(c, null))
                    {
                        yield return prompt;
                    }
                }
                else if (c.Input.Is("Restore"))
                {
                    foreach (CadPrompt prompt in RestoreDimStyle(c))
                    {
                        yield return prompt;
                    }
                }
                else if (c.Input.Is("STatus"))
                {
                    WriteDimStatus(c);
                }
                else if (c.Input.Is("Variables"))
                {
                    foreach (CadPrompt prompt in DimStyleVariables(c))
                    {
                        yield return prompt;
                    }
                }
                else if (c.Input.Is("Apply"))
                {
                    foreach (CadPrompt prompt in DimStyleUpdate(c))
                    {
                        yield return prompt;
                    }
                }
                else if (c.Input.Is("?"))
                {
                    foreach (CadPrompt prompt in ListDimStyles(c))
                    {
                        yield return prompt;
                    }
                }

                yield break;
            }
        }

        /// <summary>DIMUPDATE and Apply: the current style and overrides given to the dimensions, leaders and frames selected.</summary>
        private static IEnumerable<CadPrompt> DimStyleUpdate(CadCommandContext c)
        {
            foreach (CadPrompt prompt in c.SelectObjects())
            {
                yield return prompt;
            }

            if (c.Selected.Count == 0)
            {
                yield break;
            }

            ChangeStyled(
                c,
                "Apply",
                (entity, database) =>
                {
                    string style = CadDimStyleEditor.Current(database);
                    DxfDimStyleTableRecord data = database.GetDimstyleData();

                    switch (entity)
                    {
                        case DxfDimension dimension:
                            dimension.DimensionStyleName = style ?? dimension.DimensionStyleName;
                            dimension.SetFromStyle();
                            dimension.SetDimstyleData(data);
                            break;
                        case DxfLeader leader:
                            leader.DimensionStyleName = style ?? leader.DimensionStyleName;
                            leader.SetFromStyle();
                            leader.SetDimstyleData(data);
                            break;
                        case DxfTolerance frame:
                            frame.DimensionStyleName = style ?? frame.DimensionStyleName;
                            frame.SetFromStyle();
                            frame.SetDimstyleData(data);
                            break;
                    }

                    return true;
                },
                c.Selected
            );
        }

        /// <summary>
        /// Save and ANnotative: "Enter name for new dimension style or [?]:", a name the drawing has asking "That name is already
        /// in use, redefine it? &lt;N&gt;:"; the current variables, overrides and all, kept as the style, which becomes the
        /// current style with no overrides left. <paramref name="annotative"/> null keeps the current style's own.
        /// </summary>
        private static IEnumerable<CadPrompt> SaveDimStyle(CadCommandContext c, bool? annotative)
        {
            while (true)
            {
                yield return CadPrompt.ForKeywordOrText("Enter name for new dimension style or", new[] { "?" }, null);

                if (c.Input.Is("?"))
                {
                    foreach (CadPrompt prompt in ListDimStyles(c))
                    {
                        yield return prompt;
                    }

                    continue;
                }

                if (c.Input.Kind != CadInputKind.Text)
                {
                    yield break;
                }

                string name = c.Input.Text.Trim();

                if (!CadLayerEditor.IsLayerName(name) || name.IndexOf('$') >= 0)
                {
                    c.Write("Invalid style name.");
                    continue;
                }

                bool exists = c.Database.DimStyleTable.Has(name);

                if (exists)
                {
                    bool redefine = false;
                    bool asked = false;

                    while (!asked)
                    {
                        yield return CadPrompt.ForString("That name is already in use, redefine it?", "N");
                        string answer = c.Input.Kind == CadInputKind.Text ? c.Input.Text.Trim() : "N";
                        redefine =
                            string.Equals(answer, "Y", StringComparison.OrdinalIgnoreCase)
                            || string.Equals(answer, "Yes", StringComparison.OrdinalIgnoreCase);
                        asked =
                            redefine
                            || string.Equals(answer, "N", StringComparison.OrdinalIgnoreCase)
                            || string.Equals(answer, "No", StringComparison.OrdinalIgnoreCase);

                        if (!asked)
                        {
                            c.Write("Invalid option keyword.");
                        }
                    }

                    if (!redefine)
                    {
                        continue;
                    }
                }

                KeepDimStyle(c, name, exists, annotative);
                yield break;
            }
        }

        /// <summary>Keeps the current variables as a style, a new one or one redefined, and makes it current.</summary>
        private static void KeepDimStyle(CadCommandContext c, string name, bool exists, bool? annotative)
        {
            string current = CadDimStyleEditor.Current(c.Database);
            SortedDictionary<string, object> values = CadDimStyleEditor.Read(c.Database.GetDimstyleData());
            values[CadDimStyleEditor.AnnotativeKey] = annotative
                ?? (current != null && c.Database.DimStyleTable[current].Annotative == DxfAnnotativeStates.True);
            c.OpenUndoStep();
            CadEditResult result = exists
                ? CadDimStyleEditor.Apply(c.Document, name, values)
                : CadDimStyleEditor.Add(c.Document, name, current, values);

            if (result.Succeeded)
            {
                result = CadDimStyleEditor.SetCurrent(c.Document, c.Database.DimStyleTable[name].Name);
            }

            if (!result.Succeeded)
            {
                c.Write(result.Message);
            }
        }

        /// <summary>
        /// Restore: "Enter a dimension style name, [?] or &lt;select dimension&gt;:", a style made current, or Enter to pick a
        /// dimension, whose style and overrides become the current ones; then the current style is written.
        /// </summary>
        private static IEnumerable<CadPrompt> RestoreDimStyle(CadCommandContext c)
        {
            DxfDimension dimension = null;

            while (true)
            {
                yield return CadPrompt.ForString("Enter a dimension style name, [?] or <select dimension>");

                if (c.Input.Kind == CadInputKind.Text && c.Input.Text.Trim() == "?")
                {
                    foreach (CadPrompt prompt in ListDimStyles(c))
                    {
                        yield return prompt;
                    }

                    continue;
                }

                break;
            }

            if (c.Input.Kind == CadInputKind.Text)
            {
                string name = c.Input.Text.Trim();

                if (!CadLayerEditor.IsLayerName(name))
                {
                    c.Write("Invalid style name.");
                    yield break;
                }

                if (!c.Database.DimStyleTable.Has(name))
                {
                    c.Write("Unknown dimension style.");
                }
                else
                {
                    c.OpenUndoStep();
                    CadEditResult result = CadDimStyleEditor.SetCurrent(c.Document, name);

                    if (!result.Succeeded)
                    {
                        c.Write(result.Message);
                    }
                }

                WriteCurrentDimStyle(c);
                yield break;
            }

            yield return CadPrompt.ForEntity("Select dimension");
            dimension =
                c.Input.Kind == CadInputKind.Selection
                    ? c.Document.FindEntity(c.Input.Handles[0]) as DxfDimension
                    : null;

            if (dimension == null)
            {
                yield break;
            }

            string handle = dimension.Handle;
            string style = BaseDimStyle(c.Database, dimension.DimensionStyleName);
            string old = CadDimStyleEditor.Current(c.Database);
            c.OpenUndoStep();
            CadEditResult restored = c.Document.ApplyEdit(
                "DIMSTYLE",
                new[] { new CadEditRecord(CadEditRecordKind.SettingChanged, null, "$DIMSTYLE", old, style) },
                s =>
                {
                    s.Database.Dimstyle = style;
                    s.Database.SetDimstyleData(((DxfDimension)s.Document.FindEntity(handle)).GetDimstyleData());
                }
            );

            if (!restored.Succeeded)
            {
                c.Write(restored.Message);
                yield break;
            }

            WriteCurrentDimStyle(c);
        }

        /// <summary>Variables: "Enter a dimension style name, [?] or &lt;select dimension&gt;:", the variables of a style, or Enter to pick a dimension and see its style and its overrides.</summary>
        private static IEnumerable<CadPrompt> DimStyleVariables(CadCommandContext c)
        {
            while (true)
            {
                yield return CadPrompt.ForString("Enter a dimension style name, [?] or <select dimension>");

                if (c.Input.Kind == CadInputKind.Text && c.Input.Text.Trim() == "?")
                {
                    foreach (CadPrompt prompt in ListDimStyles(c))
                    {
                        yield return prompt;
                    }

                    continue;
                }

                break;
            }

            if (c.Input.Kind == CadInputKind.Text)
            {
                string name = c.Input.Text.Trim();

                if (!CadLayerEditor.IsLayerName(name))
                {
                    c.Write("Invalid style name.");
                }
                else if (!c.Database.DimStyleTable.Has(name))
                {
                    c.Write("Unknown dimension style.");
                }
                else
                {
                    foreach (KeyValuePair<string, string> row in DimRows(c, c.Database.DimStyleTable[name], null))
                    {
                        c.Write(DimLine(row));
                    }
                }

                yield break;
            }

            yield return CadPrompt.ForEntity("Select dimension");

            if (
                !(
                    c.Input.Kind == CadInputKind.Selection
                    && c.Document.FindEntity(c.Input.Handles[0]) is DxfDimension dimension
                )
            )
            {
                yield break;
            }

            c.Write(string.Empty);
            c.Write("Dimension style: " + dimension.DimensionStyleName);

            // The lines LIST writes, but the heading of them.
            foreach (string line in ListedOverrides(c, dimension).Skip(1))
            {
                c.Write(line);
            }
        }

        /// <summary>The ? of the options: "Enter dimension style(s) to list &lt;*&gt;:", then the styles that match, annotative ones marked.</summary>
        private static IEnumerable<CadPrompt> ListDimStyles(CadCommandContext c)
        {
            yield return CadPrompt.ForString("Enter dimension style(s) to list", "*");
            string pattern =
                c.Input.Kind == CadInputKind.Text && c.Input.Text.Trim().Length > 0 ? c.Input.Text.Trim() : "*";
            c.Write(string.Empty);
            c.Write("Named dimension styles:");
            c.Write(string.Empty);
            bool any = false;

            foreach (DxfDimStyleTableRecord style in CadDimStyleEditor.Styles(c.Database))
            {
                if (!CadWildcard.IsMatch(style.Name, pattern, true))
                {
                    continue;
                }

                any = true;
                string line = "  " + style.Name;
                c.Write(style.Annotative == DxfAnnotativeStates.True ? line.PadRight(34) + "Annotative" : line);
            }

            if (!any)
            {
                c.Write("  No matching dimension styles found.");
            }
        }

        /// <summary>"Current dimension style: Standard   Annotative: No", then, where the variables differ from the style's, the overrides.</summary>
        private static void WriteCurrentDimStyle(CadCommandContext c)
        {
            string name = CadDimStyleEditor.Current(c.Database);

            if (name == null)
            {
                return;
            }

            DxfDimStyleTableRecord style = c.Database.DimStyleTable[name];
            c.Write(
                "Current dimension style: "
                    + name
                    + "   Annotative: "
                    + (style.Annotative == DxfAnnotativeStates.True ? "Yes" : "No")
            );
            List<KeyValuePair<string, string>> overrides = DimRows(c, c.Database.GetDimstyleData(), style);

            if (overrides.Count == 0)
            {
                return;
            }

            c.Write("Current dimension overrides:");

            foreach (KeyValuePair<string, string> row in overrides)
            {
                c.Write(OverrideLine(row));
            }
        }

        /// <summary>STatus: DIMASO and DIMSTYLE, then every variable the current style has, in the words of AutoCAD's listing.</summary>
        private static void WriteDimStatus(CadCommandContext c)
        {
            c.Write(
                "DIMASO".PadRight(12) + (c.Database.Dimaso ? "On" : "Off").PadRight(21) + "Create dimension objects"
            );
            c.Write(
                "DIMSTYLE".PadRight(12)
                    + (CadDimStyleEditor.Current(c.Database) ?? string.Empty).PadRight(21)
                    + "Current dimension style (read-only)"
            );
            c.Write(string.Empty);

            foreach (KeyValuePair<string, string> row in DimRows(c, c.Database.GetDimstyleData(), null))
            {
                c.Write(DimLine(row));
            }
        }

        /// <summary>
        /// The variables of a record as AutoCAD lists them, each with its name as AutoCAD spells it (DIMFXL for the fixed
        /// extension line's length) and its value as LIST writes one, in alphabetical order of the names; only those that differ
        /// from <paramref name="against"/> where it is given.
        /// </summary>
        private static List<KeyValuePair<string, string>> DimRows(
            CadCommandContext c,
            DxfDimStyleTableRecord data,
            DxfDimStyleTableRecord against
        )
        {
            var rows = new List<KeyValuePair<string, string>>();

            foreach (DxfDimVariable variable in DxfDimVariable.All)
            {
                object value = data.Get(variable);

                if (against != null && variable.SameValue(against.Get(variable), value))
                {
                    continue;
                }

                string name =
                    variable.Name == "Dimfxlen" ? "DIMFXL"
                    : variable.Name == "DimfxlenOn" ? "DIMFXLON"
                    : variable.Name.ToUpperInvariant();
                rows.Add(new KeyValuePair<string, string>(name, Overridden(c, variable, value)));
            }

            rows.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
            return rows;
        }

        /// <summary>A row of an override list: a space, the name set in twelve places, then the value.</summary>
        private static string OverrideLine(KeyValuePair<string, string> row) =>
            row.Value.Length == 0 ? " " + row.Key : " " + row.Key.PadRight(12) + row.Value;

        /// <summary>A row of the STatus and Variables listings: the name set in twelve places, the value in twenty-one, then what the variable is.</summary>
        private static string DimLine(KeyValuePair<string, string> row)
        {
            string description = DimDescriptions.TryGetValue(DimVariableNamed(row.Key), out string said)
                ? said
                : string.Empty;
            return (row.Key.PadRight(12) + row.Value.PadRight(21) + description).TrimEnd();
        }

        /// <summary>The library's name of a variable by AutoCAD's: DIMFXL is Dimfxlen.</summary>
        private static string DimVariableNamed(string listed) =>
            listed == "DIMFXL" ? "Dimfxlen"
            : listed == "DIMFXLON" ? "DimfxlenOn"
            : DxfDimVariable
                .All.Select(v => v.Name)
                .FirstOrDefault(n => string.Equals(n, listed, StringComparison.OrdinalIgnoreCase))
                ?? listed;

        /// <summary>The style a dimension's own name stands for: a name AutoCAD keeps for one kind of dimension (ISO-25$0) is of the style before the $.</summary>
        private static string BaseDimStyle(DxfDatabase database, string name)
        {
            int mark = name.IndexOf('$');
            return database.DimStyleTable.Has(name) || mark < 0 ? name : name.Substring(0, mark);
        }
    }
}
