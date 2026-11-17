using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;

namespace GeometryHelper.Cad
{
    /// <summary>
    /// LAYMRG and LAYDEL (the command line forms of AutoCAD 2021's, -LAYMRG and -LAYDEL, probed in its console; the dialogs of
    /// the plain names are not built, so the plain names ask on the command line too): the layers are picked by an object on
    /// each or by Name until Enter, then the target layer (LAYMRG), then the warning and "Do you wish to continue?". LAYMRG
    /// puts every object of the layers picked on the target and deletes them; LAYDEL erases every object on them and the layers.
    /// </summary>
    internal static partial class CadCommands
    {
        private static void AddLayerMergeTo(CadCommandRegistry registry)
        {
            registry.Add(new CadCommand("LAYMRG", LayerMerge));
            registry.Add(new CadCommand("-LAYMRG", LayerMerge));
            registry.Add(new CadCommand("LAYDEL", LayerDelete));
            registry.Add(new CadCommand("-LAYDEL", LayerDelete));
        }

        private static IEnumerable<CadPrompt> LayerMerge(CadCommandContext c)
        {
            var chosen = new List<string>();

            foreach (CadPrompt prompt in SelectLayers(c, "merge", chosen))
            {
                yield return prompt;
            }

            if (chosen.Count == 0)
            {
                yield break;
            }

            string target = null;

            while (target == null)
            {
                yield return CadPrompt
                    .ForEntity("Select object on target layer", new[] { "Name" })
                    .Expecting("Last/Name");

                if (c.Input.IsNone)
                {
                    yield break;
                }

                string picked = null;

                if (c.Input.Is("Name"))
                {
                    var typed = new Ref<string>();

                    foreach (CadPrompt prompt in AskLayerName(c, typed))
                    {
                        yield return prompt;
                    }

                    picked = typed.Value;
                }
                else if (c.Input.Kind == CadInputKind.Selection)
                {
                    picked = LayerOf(c);
                }

                if (picked == null)
                {
                    continue;
                }

                if (c.Database.LayerTable[picked].IsDependent)
                {
                    c.Write("Invalid Layer name.");
                }
                else if (chosen.Contains(picked, StringComparer.OrdinalIgnoreCase))
                {
                    c.Write("Cannot merge a layer with itself. Layer " + picked + " is already selected.");
                }
                else
                {
                    target = picked;
                }
            }

            c.Write(string.Empty);
            c.Write("******** WARNING ********");
            c.Write(string.Empty);
            c.Write(
                chosen.Count == 1
                    ? "You are about to merge layer \"" + chosen[0] + "\" into layer \"" + target + "\"."
                    : "You are about to merge "
                        + chosen.Count.ToString(CultureInfo.InvariantCulture)
                        + " layers into layer \""
                        + target
                        + "\"."
            );
            c.Write(string.Empty);
            yield return CadPrompt.ForKeyword("Do you wish to continue?", new[] { "Yes", "No" }, "No");

            if (!c.Input.Is("Yes"))
            {
                c.Write("Operation aborted.");
                yield break;
            }

            c.OpenUndoStep();
            ThawInViewports(c, chosen);
            CadEditResult result = CadLayerEditor.Merge(c.Document, chosen, target);
            WriteDeleted(c, result, chosen);
        }

        private static IEnumerable<CadPrompt> LayerDelete(CadCommandContext c)
        {
            var chosen = new List<string>();

            foreach (CadPrompt prompt in SelectLayers(c, "delete", chosen))
            {
                yield return prompt;
            }

            if (chosen.Count == 0)
            {
                yield break;
            }

            c.Write(string.Empty);
            c.Write("******** WARNING ********");

            if (chosen.Count == 1)
            {
                c.Write(string.Empty);
                c.Write("You are about to delete layer \"" + chosen[0] + "\" from this drawing.");
            }
            else
            {
                c.Write("You are about to delete the following layers from this drawing.");

                foreach (string name in chosen)
                {
                    c.Write(name);
                }
            }

            c.Write(string.Empty);
            yield return CadPrompt.ForKeyword("Do you wish to continue?", new[] { "Yes", "No" }, "No");

            if (!c.Input.Is("Yes"))
            {
                c.Write("Operation aborted.");
                yield break;
            }

            c.OpenUndoStep();
            ThawInViewports(c, chosen);
            CadEditResult result = CadLayerEditor.DeleteWithObjects(c.Document, chosen);
            WriteDeleted(c, result, chosen);
        }

        /// <summary>
        /// Asks for the layers, "Select object on layer to merge or [Name]:" and, once one is picked, "[Name/Undo]:", until Enter;
        /// a layer is picked by an object on it or typed by Name, and the layers so far are said ("Selected layers: A,B.").
        /// The current layer, layer 0, Defpoints and a layer of an external reference are refused where they are picked.
        /// </summary>
        private static IEnumerable<CadPrompt> SelectLayers(CadCommandContext c, string verb, List<string> chosen)
        {
            while (true)
            {
                yield return CadPrompt
                    .ForEntity(
                        "Select object on layer to " + verb,
                        chosen.Count == 0 ? new[] { "Name" } : new[] { "Name", "Undo" }
                    )
                    .Expecting("Last/Name");

                if (c.Input.IsNone)
                {
                    yield break;
                }

                if (c.Input.Is("Undo"))
                {
                    if (chosen.Count > 0)
                    {
                        chosen.RemoveAt(chosen.Count - 1);
                    }

                    c.Write("Selected layers: " + string.Join(",", chosen) + ".");
                    continue;
                }

                string picked = null;

                if (c.Input.Is("Name"))
                {
                    var typed = new Ref<string>();

                    foreach (CadPrompt prompt in AskLayerName(c, typed))
                    {
                        yield return prompt;
                    }

                    picked = typed.Value;
                }
                else if (c.Input.Kind == CadInputKind.Selection)
                {
                    picked = LayerOf(c);
                }

                if (picked == null)
                {
                    continue;
                }

                string refusal = LayerRefusal(c.Database, picked, verb);

                if (refusal != null)
                {
                    c.Write(refusal);
                    continue;
                }

                if (!chosen.Contains(picked, StringComparer.OrdinalIgnoreCase))
                {
                    chosen.Add(picked);
                }

                c.Write("Selected layers: " + string.Join(",", chosen) + ".");
            }
        }

        /// <summary>Why a layer cannot be merged or deleted, in AutoCAD 2021's words ("Cannot delete current layer."); null where it can.</summary>
        private static string LayerRefusal(DxfDatabase database, string name, string verb)
        {
            DxfLayerTableRecord layer = database.LayerTable[name];

            if (string.Equals(layer.Name, database.Clayer, StringComparison.OrdinalIgnoreCase))
            {
                return "Cannot " + verb + " current layer.";
            }

            if (layer.Name == "0" || string.Equals(layer.Name, "Defpoints", StringComparison.OrdinalIgnoreCase))
            {
                return "Cannot " + verb + " layer \"" + layer.Name + "\".";
            }

            return layer.IsDependent ? "Invalid Layer name." : null;
        }

        /// <summary>
        /// "Enter layer name or [?]:", asked until a layer of the drawing is typed, "Invalid Layer name." said for another; ? asks
        /// "Enter layer name(s) to list &lt;*&gt;:" and lists them; Enter gives none. The layer's name as the table spells it.
        /// </summary>
        private static IEnumerable<CadPrompt> AskLayerName(CadCommandContext c, Ref<string> name)
        {
            while (true)
            {
                yield return CadPrompt.ForKeywordOrText("Enter layer name or", new[] { "?" }, null);

                if (c.Input.Is("?"))
                {
                    foreach (CadPrompt prompt in ListLayerNames(c))
                    {
                        yield return prompt;
                    }

                    continue;
                }

                if (c.Input.Kind != CadInputKind.Text)
                {
                    yield break;
                }

                string typed = c.Input.Text.Trim();

                if (typed.Length > 0 && c.Database.LayerTable.Has(typed))
                {
                    name.Value = c.Database.LayerTable[typed].Name;
                    yield break;
                }

                c.Write("Invalid Layer name.");
            }
        }

        /// <summary>The ? of a layer name: "Enter layer name(s) to list &lt;*&gt;:", then the names that match, one on a line, in alphabetical order.</summary>
        private static IEnumerable<CadPrompt> ListLayerNames(CadCommandContext c)
        {
            yield return CadPrompt.ForString("Enter layer name(s) to list", "*");
            string pattern =
                c.Input.Kind == CadInputKind.Text && c.Input.Text.Trim().Length > 0 ? c.Input.Text.Trim() : "*";
            List<string> names = LayerNames(c).Where(n => CadWildcard.IsMatch(n, pattern, true)).ToList();
            names.Sort(StringComparer.OrdinalIgnoreCase);

            if (names.Count == 0)
            {
                c.Write("No matching names found.");
                yield break;
            }

            foreach (string name in names)
            {
                c.Write(name);
            }
        }

        /// <summary>Thaws layers in the viewports of the layouts, so that no viewport points at a layer about to be deleted.</summary>
        private static void ThawInViewports(CadCommandContext c, IEnumerable<string> names) =>
            CadViewportLayerEditor.SetFrozenInAllViewports(c.Document, names, false);

        /// <summary>Says what a merge or a delete came to: 'Deleting layer "A".' for each layer, then "2 layers deleted."; the reason where it was not made.</summary>
        private static void WriteDeleted(CadCommandContext c, CadEditResult result, IReadOnlyCollection<string> names)
        {
            if (!result.Succeeded)
            {
                c.Write(result.Message);
                return;
            }

            foreach (string name in names)
            {
                c.Write(string.Empty);
                c.Write("Deleting layer \"" + name + "\".");
            }

            c.Write(
                names.Count == 1
                    ? "1 layer deleted."
                    : names.Count.ToString(CultureInfo.InvariantCulture) + " layers deleted."
            );
        }
    }
}
