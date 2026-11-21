using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;

namespace GeometryHelper.Cad
{
    /// <summary>
    /// The options of -LAYER AutoCAD 2021's console asks as a routine's <c>command "layer" "m" ...</c> types them: Make names
    /// a new layer and makes it current, Color and Ltype each ask their value and then the layers it goes to, and Set takes a
    /// name or an object picked.
    /// </summary>
    internal static partial class CadCommands
    {
        /// <summary>Whether -LAYER asks the option here, in this file, and not in the loop of <c>LayerTyped</c>.</summary>
        private static bool LayerOptionAsked(string option) =>
            option == "Make"
            || option == "Set"
            || option == "Rename"
            || option == "Color"
            || option == "Ltype"
            || option == "LWeight"
            || option == "?"
            || option == "MATerial"
            || option == "rEconcile"
            || option == "Xref";

        /// <summary>
        /// Asks one of the options <see cref="LayerOptionAsked"/> names; ?, MATerial, rEconcile and Xref, which AutoCAD 2021 lists
        /// and the presenter does not do, write that they are not there (the Layer manager lists and reconciles the layers).
        /// </summary>
        private static IEnumerable<CadPrompt> LayerOption(CadCommandContext c, string option)
        {
            IEnumerable<CadPrompt> asked;

            switch (option)
            {
                case "Make":
                    asked = LayerMake(c);
                    break;
                case "Set":
                    asked = LayerSet(c);
                    break;
                case "Rename":
                    asked = LayerRename(c);
                    break;
                case "Color":
                    asked = LayerColor(c);
                    break;
                case "Ltype":
                    asked = LayerLinetype(c);
                    break;
                case "LWeight":
                    asked = LayerLineWeight(c);
                    break;
                default:
                    c.Write("-LAYER " + option + " is not available here: the Layer manager does it.");
                    yield break;
            }

            foreach (CadPrompt prompt in asked)
            {
                yield return prompt;
            }
        }

        /// <summary>-LAYER Rename: "Enter old layer name:", then "Enter new layer name:"; a layer the drawing lacks is said.</summary>
        private static IEnumerable<CadPrompt> LayerRename(CadCommandContext c)
        {
            yield return CadPrompt.ForString("Enter old layer name");

            if (c.Input.Kind != CadInputKind.Text)
            {
                yield break;
            }

            string old = c.Input.Text.Trim();

            if (!c.Database.LayerTable.Has(old))
            {
                c.Write("Cannot find layer \"" + old + "\".");
                yield break;
            }

            yield return CadPrompt.ForString("Enter new layer name");

            if (c.Input.Kind != CadInputKind.Text)
            {
                yield break;
            }

            c.OpenUndoStep();
            CadEditResult result = CadLayerEditor.Rename(c.Document, old, c.Input.Text);

            if (!result.Succeeded)
            {
                c.Write(result.Message);
            }
        }

        /// <summary>
        /// -LAYER LWeight: "Enter lineweight (0.0mm - 2.11mm):", then "Enter name list of layers(s) for lineweight 0.30mm
        /// &lt;current&gt;:", with the "(s)" as AutoCAD 2021 spells it; a width AutoCAD has none of is refused and asked again.
        /// </summary>
        private static IEnumerable<CadPrompt> LayerLineWeight(CadCommandContext c)
        {
            DxfLineWeight weight = DxfLineWeight.ByLineWeightDefault;

            while (true)
            {
                yield return CadPrompt.ForString("Enter lineweight (0.0mm - 2.11mm)");

                if (c.Input.Kind != CadInputKind.Text)
                {
                    yield break;
                }

                if (
                    CadPropertyEditor.ParseLineWeight(c.Input.Text.Trim(), out weight) == null
                    && weight != DxfLineWeight.ByLayer
                    && weight != DxfLineWeight.ByBlock
                )
                {
                    break;
                }

                c.Write("Invalid lineweight.");
            }

            string words =
                weight == DxfLineWeight.ByLineWeightDefault
                    ? "Default"
                    : ((int)weight / 100.0).ToString("0.00", CultureInfo.InvariantCulture) + "mm";
            yield return CadPrompt.ForString("Enter name list of layers(s) for lineweight " + words, c.Database.Clayer);
            string names =
                c.Input.Kind == CadInputKind.Text ? c.Input.Text
                : c.Input.IsNone ? EscapedLayerName(c.Database.Clayer)
                : null;

            if (names != null)
            {
                DxfLineWeight given = weight;
                LayersEach(c, names, name => CadLayerEditor.SetLineWeight(c.Document, name, given));
            }
        }

        /// <summary>
        /// -LAYER Make: "Enter name for new layer (becomes the current layer) &lt;0&gt;:", the layer made where the drawing has
        /// none and made current; Enter keeps the current layer.
        /// </summary>
        private static IEnumerable<CadPrompt> LayerMake(CadCommandContext c)
        {
            string current = c.Database.Clayer;
            yield return CadPrompt.ForString("Enter name for new layer (becomes the current layer)", current);

            if (c.Input.Kind != CadInputKind.Text || c.Input.Text.Trim().Length == 0)
            {
                yield break;
            }

            string name = c.Input.Text.Trim();
            c.OpenUndoStep();

            if (!c.Database.LayerTable.Has(name))
            {
                CadEditResult added = CadLayerEditor.Add(c.Document, name);

                if (!added.Succeeded)
                {
                    c.Write(added.Message);
                    yield break;
                }
            }

            if (!string.Equals(name, current, StringComparison.OrdinalIgnoreCase))
            {
                CadEditResult made = CadLayerEditor.SetCurrent(c.Document, name);

                if (!made.Succeeded)
                {
                    c.Write(made.Message);
                }
            }
        }

        /// <summary>
        /// -LAYER Set: "Enter layer name to make current or &lt;select object&gt;:", a layer the drawing lacks said and asked again,
        /// Enter asking for an object whose layer becomes current.
        /// </summary>
        private static IEnumerable<CadPrompt> LayerSet(CadCommandContext c)
        {
            string layer = null;

            while (layer == null)
            {
                yield return CadPrompt.ForString("Enter layer name to make current or <select object>");

                if (c.Input.Kind == CadInputKind.Text)
                {
                    string name = c.Input.Text.Trim();

                    if (!c.Database.LayerTable.Has(name))
                    {
                        c.Write("Cannot find layer \"" + name + "\".");
                        continue;
                    }

                    layer = name;
                }
                else if (c.Input.IsNone)
                {
                    yield return CadPrompt.ForEntity("Select object");

                    if (c.Input.Kind != CadInputKind.Selection || c.Input.Handles.Count == 0)
                    {
                        yield break;
                    }

                    DxfEntity picked = c.Document.FindEntity(c.Input.Handles[0]);

                    if (picked == null)
                    {
                        yield break;
                    }

                    layer = picked.Layer;
                }
                else
                {
                    yield break;
                }
            }

            c.OpenUndoStep();
            CadEditResult result = c.Database.LayerTable[layer].IsDependent
                ? CadEditResult.Refuse("Invalid layer name.")
                : CadLayerEditor.SetCurrent(c.Document, layer);

            if (!result.Succeeded)
            {
                c.Write(result.Message);
            }
        }

        /// <summary>
        /// -LAYER Color: "New color [Truecolor/COlorbook]:" for a colour number 1 to 255 or a standard name, then "Enter name list
        /// of layer(s) for color 3 (green) &lt;current&gt;:"; the colours 1 to 7 are named there, as AutoCAD 2021 names them.
        /// </summary>
        private static IEnumerable<CadPrompt> LayerColor(CadCommandContext c)
        {
            DxfColor colour = null;
            string words = null;

            while (colour == null)
            {
                yield return CadPrompt.ForKeywordOrText("New color", new[] { "Truecolor", "COlorbook" }, null);

                if (c.Input.IsNone)
                {
                    yield break;
                }

                string typed;

                if (c.Input.Is("COlorbook"))
                {
                    c.Write("Color books are not available here.");
                    yield break;
                }

                if (c.Input.Is("Truecolor"))
                {
                    yield return CadPrompt.ForString("Red,Green,Blue");
                    typed = c.Input.Kind == CadInputKind.Text ? c.Input.Text.Trim() : null;

                    if (typed == null)
                    {
                        yield break;
                    }

                    if (CadPropertyEditor.ParseColor(typed, out DxfColor rgb) != null || !rgb.IsByColor)
                    {
                        c.Write("Invalid color number.");
                        continue;
                    }

                    colour = rgb;
                    words = CadPropertyEditor.ColorName(rgb);
                    continue;
                }

                typed = c.Input.Kind == CadInputKind.Text ? c.Input.Text.Trim() : null;

                if (typed == null)
                {
                    yield break;
                }

                if (int.TryParse(typed, NumberStyles.Integer, CultureInfo.InvariantCulture, out int index))
                {
                    if (index < 1 || index > 255)
                    {
                        c.Write("Invalid color number.");
                        continue;
                    }

                    colour = DxfColor.FromColorIndex(DxfColorMethod.ByAci, (short)index);
                    words = typed;
                    continue;
                }

                if (
                    CadPropertyEditor.ParseColor(typed, out DxfColor named) != null
                    || !named.IsByAci
                    || named.ColorIndex > 7
                )
                {
                    c.Write("A color number or standard color name is required.");
                    continue;
                }

                colour = named;
                words = named.ColorIndex.ToString(CultureInfo.InvariantCulture);
            }

            if (colour.IsByAci && colour.ColorIndex >= 1 && colour.ColorIndex <= 7)
            {
                words += " (" + CadPropertyEditor.ColorName(colour).ToLowerInvariant() + ")";
            }

            string asked = "Enter name list of layer(s) for color " + words;
            yield return CadPrompt.ForString(asked, c.Database.Clayer);
            string names =
                c.Input.Kind == CadInputKind.Text ? c.Input.Text
                : c.Input.IsNone ? EscapedLayerName(c.Database.Clayer)
                : null;

            if (names != null)
            {
                DxfColor given = colour;
                LayersEach(c, names, name => CadLayerEditor.SetColor(c.Document, name, given));
            }
        }

        /// <summary>
        /// -LAYER Ltype: "Enter loaded linetype name or [?] &lt;Continuous&gt;:", a linetype the drawing lacks loaded from the
        /// presenter's own set, then "Enter name list of layer(s) for linetype "HIDDEN" &lt;current&gt;:". One no set has is
        /// "Linetype "x" not found.  Use LINETYPE command to load it." and the question is over.
        /// </summary>
        private static IEnumerable<CadPrompt> LayerLinetype(CadCommandContext c)
        {
            yield return CadPrompt.ForString("Enter loaded linetype name or [?]", "Continuous");
            string typed =
                c.Input.Kind == CadInputKind.Text ? c.Input.Text.Trim().Trim('"')
                : c.Input.IsNone ? "Continuous"
                : null;

            if (typed == null || typed.Length == 0)
            {
                yield break;
            }

            c.OpenUndoStep();
            string linetype = c
                .Database.LinetypeTable.Select(l => l.Name)
                .FirstOrDefault(n => string.Equals(n, typed, StringComparison.OrdinalIgnoreCase));

            if (linetype == null)
            {
                CadLinetypeDefinition found = CadLinetypeFile.BuiltIn.FirstOrDefault(l =>
                    string.Equals(l.Name, typed, StringComparison.OrdinalIgnoreCase)
                );

                if (found == null)
                {
                    c.Write("Linetype \"" + typed + "\" not found.  Use LINETYPE command to load it.");
                    yield break;
                }

                CadLinetypeLoad load = CadLinetypes.Load(
                    c.Document,
                    new[] { found },
                    CadLinetypeFile.BuiltInName,
                    c.Command.Name
                );

                if (load.Refused.Count > 0 || !load.Result.Succeeded)
                {
                    Say(c, load);
                    yield break;
                }

                linetype = found.Name;
            }

            yield return CadPrompt.ForString(
                "Enter name list of layer(s) for linetype \"" + linetype.ToUpperInvariant() + "\"",
                c.Database.Clayer
            );
            string names =
                c.Input.Kind == CadInputKind.Text ? c.Input.Text
                : c.Input.IsNone ? EscapedLayerName(c.Database.Clayer)
                : null;

            if (names != null)
            {
                string given = linetype;
                LayersEach(c, names, name => CadLayerEditor.SetLinetype(c.Document, name, given));
            }
        }

        /// <summary>
        /// Gives each layer a list of names stands for (<see cref="LayerNameList"/>: patterns parted by commas, a back quote
        /// making the character after it itself) what <paramref name="change"/> does, as one undo step; where none of the names
        /// matches a layer AutoCAD says "No matching layer names found.".
        /// </summary>
        private static void LayersEach(CadCommandContext c, string names, Func<string, CadEditResult> change)
        {
            c.OpenUndoStep();
            List<string> layers = LayerNameList(c.Database, names, true);

            foreach (string name in layers)
            {
                CadEditResult result = change(name);

                if (!result.Succeeded)
                {
                    c.Write(result.Message);
                }
            }

            if (layers.Count == 0)
            {
                c.Write("No matching layer names found.");
            }
        }
    }
}
