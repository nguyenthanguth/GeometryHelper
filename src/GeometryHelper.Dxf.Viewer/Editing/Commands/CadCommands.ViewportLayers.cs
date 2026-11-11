using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// The layers of the viewports of the layouts: VPLAYER, which lists the layers frozen in a viewport, freezes, thaws and
    /// resets layers in viewports, adds layers frozen in all of them and sets what new viewports freeze; and LAYVPI, which
    /// freezes the layer of each object picked in every viewport but the one worked through. Each run is one undo step.
    /// </summary>
    internal static partial class CadCommands
    {
        /// <summary>The key LAYVPI keeps its setting under: 1 for the viewports of the current layout only, else of all layouts.</summary>
        private const string LayvpiCurrentLayout = "layvpi:current layout";

        /// <summary>
        /// VPLAYER: "Enter an option [?/Freeze/Thaw/Reset/Newfrz/Vpvisdflt]:" until Enter. Freeze, Thaw and Reset take layer
        /// names (commas and wild cards, as WALL*,DIM) or, on Enter, the layers of objects selected, then the viewports: All of
        /// the layout, those Selected, the Current one or all Except current; on the Model tab, all of every layout.
        /// </summary>
        private static IEnumerable<CadPrompt> ViewportLayer(CadCommandContext c)
        {
            c.OpenUndoStep();

            while (true)
            {
                yield return CadPrompt.ForKeyword(
                    "Enter an option",
                    new[] { "?", "Freeze", "Thaw", "Reset", "Newfrz", "Vpvisdflt" }
                );

                if (c.Input.IsNone)
                {
                    yield break;
                }

                if (c.Input.Is("?"))
                {
                    DxfViewport listed = c.Document.ActiveViewport;

                    if (listed == null)
                    {
                        yield return CadPrompt.ForEntity("Select a viewport");
                        listed =
                            c.Input.Kind == CadInputKind.Selection
                                ? c.Document.FindEntity(c.Input.Handles[0]) as DxfViewport
                                : null;
                    }

                    if (listed == null || listed.Number == 1)
                    {
                        c.Write("The object selected is not a viewport.");
                        continue;
                    }

                    c.Write(
                        "Layers currently frozen in viewport "
                            + listed.Number.ToString(CultureInfo.InvariantCulture)
                            + ":"
                    );

                    foreach (DxfLayerTableRecord layer in listed.GetFrozenLayers())
                    {
                        c.Write("  \"" + layer.Name + "\"");
                    }

                    continue;
                }

                if (c.Input.Is("Newfrz"))
                {
                    yield return CadPrompt.ForString("Enter name(s) of new layers frozen in all viewports");

                    foreach (string name in (c.Input.Text ?? string.Empty).Split(','))
                    {
                        if (name.Trim().Length > 0)
                        {
                            Said(c, CadViewportLayerEditor.AddFrozenInAllViewports(c.Document, name, c.Command.Name));
                        }
                    }

                    continue;
                }

                bool freeze = c.Input.Is("Freeze");
                bool thaw = c.Input.Is("Thaw");
                bool reset = c.Input.Is("Reset");
                string what =
                    freeze ? "freeze"
                    : thaw ? "thaw"
                    : reset ? "reset"
                    : "change viewport visibility";
                yield return CadPrompt.ForString("Enter layer name(s) to " + what + " or <select objects>");
                List<string> layers;

                if (c.Input.IsNone || string.IsNullOrWhiteSpace(c.Input.Text))
                {
                    yield return CadPrompt.ForSelection();
                    layers = LayersOfObjects(c, c.Input.Handles);
                }
                else
                {
                    layers = LayersNamed(c, c.Input.Text);
                }

                if (layers.Count == 0)
                {
                    continue;
                }

                if (!freeze && !thaw && !reset)
                {
                    yield return CadPrompt.ForKeyword(
                        "Enter a viewport visibility option",
                        new[] { "Frozen", "Thawed" },
                        "Thawed"
                    );
                    bool frozen = c.Input.Is("Frozen");

                    foreach (string layer in layers)
                    {
                        Said(
                            c,
                            CadViewportLayerEditor.SetFrozenInNewViewports(c.Document, layer, frozen, c.Command.Name)
                        );
                    }

                    continue;
                }

                List<string> viewports = null;

                if (c.Document.CurrentSpace.IsModel)
                {
                    viewports = Handles(CadViewportLayerEditor.LayoutViewports(c.Document));
                }
                else
                {
                    yield return CadPrompt.ForKeyword(
                        "Enter an option",
                        new[] { "All", "Select", "Current", "Except current" },
                        "Current"
                    );
                    DxfViewport current = CadViewportLayerEditor.TargetViewport(c.Document);

                    if (c.Input.Is("Select"))
                    {
                        yield return CadPrompt.ForSelection("Select viewports");
                        viewports = new List<string>();

                        foreach (string handle in c.Input.Handles)
                        {
                            if (c.Document.FindEntity(handle) is DxfViewport viewport && viewport.Number != 1)
                            {
                                viewports.Add(viewport.Handle);
                            }
                        }
                    }
                    else if (c.Input.Is("All") || c.Input.Is("Except current"))
                    {
                        bool except = c.Input.Is("Except current");
                        viewports = new List<string>();

                        foreach (DxfViewport viewport in ViewportsOfLayout(c.Document.CurrentSpace))
                        {
                            if (
                                !except
                                || current == null
                                || !string.Equals(viewport.Handle, current.Handle, StringComparison.OrdinalIgnoreCase)
                            )
                            {
                                viewports.Add(viewport.Handle);
                            }
                        }
                    }
                    else if (current != null)
                    {
                        viewports = new List<string> { current.Handle };
                    }
                    else
                    {
                        c.Write("There is no current viewport: enter one (MSPACE) or select one first.");
                        continue;
                    }
                }

                if (reset)
                {
                    foreach (string layer in layers)
                    {
                        bool frozen = c.Database.LayerTable[layer].ViewportVisibilityDefault;
                        Said(
                            c,
                            CadViewportLayerEditor.SetFrozen(
                                c.Document,
                                viewports,
                                new[] { layer },
                                frozen,
                                c.Command.Name
                            )
                        );
                    }
                }
                else
                {
                    Said(c, CadViewportLayerEditor.SetFrozen(c.Document, viewports, layers, freeze, c.Command.Name));
                }
            }
        }

        /// <summary>
        /// LAYVPI: the layer of each object picked frozen in every viewport but the one worked through, of all layouts or of the
        /// current one (Settings), until Enter; Undo thaws the last again. Refused on the Model tab and on the paper.
        /// </summary>
        private static IEnumerable<CadPrompt> LayerViewportIsolate(CadCommandContext c)
        {
            if (c.Document.CurrentSpace.IsModel)
            {
                c.Write("** Command not allowed in Model Tab **");
                yield break;
            }

            DxfViewport current = c.Document.ActiveViewport;

            if (current == null)
            {
                c.Write("LAYVPI works in a viewport: enter one (MSPACE) first.");
                yield break;
            }

            c.OpenUndoStep();
            var done = new List<KeyValuePair<string, List<string>>>();

            while (true)
            {
                bool layoutOnly = c.Remembered(LayvpiCurrentLayout, 0.0) > 0.0;
                c.Write("Current settings: Viewports=" + (layoutOnly ? "Current layout" : "All layouts"));
                yield return CadPrompt.ForEntity(
                    "Select objects on the layer to be isolated in viewport",
                    new[] { "Settings", "Undo" }
                );

                if (c.Input.Is("Settings"))
                {
                    yield return CadPrompt.ForKeyword(
                        "Enter an option for viewports",
                        new[] { "All layouts", "Current layout" },
                        layoutOnly ? "Current layout" : "All layouts"
                    );
                    c.Remember(LayvpiCurrentLayout, c.Input.Is("Current layout") ? 1.0 : 0.0);
                    continue;
                }

                if (c.Input.Is("Undo"))
                {
                    if (done.Count == 0)
                    {
                        c.Write("Nothing to undo.");
                        continue;
                    }

                    KeyValuePair<string, List<string>> last = done[done.Count - 1];
                    done.RemoveAt(done.Count - 1);
                    CadViewportLayerEditor.SetFrozen(c.Document, last.Value, new[] { last.Key }, false, c.Command.Name);
                    continue;
                }

                string name = LayerOf(c);

                if (name == null)
                {
                    yield break;
                }

                var others = new List<string>();
                IEnumerable<DxfViewport> scope = layoutOnly
                    ? ViewportsOfLayout(c.Document.CurrentSpace)
                    : CadViewportLayerEditor.LayoutViewports(c.Document);

                foreach (DxfViewport viewport in scope)
                {
                    if (
                        !string.Equals(viewport.Handle, current.Handle, StringComparison.OrdinalIgnoreCase)
                        && !viewport.IsLayerFrozenInViewport(c.Database.LayerTable[name])
                    )
                    {
                        others.Add(viewport.Handle);
                    }
                }

                CadEditResult result = CadViewportLayerEditor.SetFrozen(
                    c.Document,
                    others,
                    new[] { name },
                    true,
                    c.Command.Name
                );

                if (result.Succeeded)
                {
                    done.Add(new KeyValuePair<string, List<string>>(name, others));
                    c.Write("Layer \"" + name + "\" has been frozen in all viewports but the current one.");
                }
                else
                {
                    c.Write(result.Message);
                }
            }
        }

        /// <summary>The viewports of one layout, its sheet's own left out, in the order of its block.</summary>
        private static List<DxfViewport> ViewportsOfLayout(CadSpace space)
        {
            var found = new List<DxfViewport>();

            foreach (DxfEntity entity in space.Block)
            {
                if (entity is DxfViewport viewport && !viewport.IsErased && viewport.Number != 1)
                {
                    found.Add(viewport);
                }
            }

            return found;
        }

        private static List<string> Handles(IEnumerable<DxfViewport> viewports)
        {
            var handles = new List<string>();

            foreach (DxfViewport viewport in viewports)
            {
                handles.Add(viewport.Handle);
            }

            return handles;
        }

        /// <summary>The layers of the drawing names typed match, each pattern of a list split by commas with AutoCAD's wild cards, in ordinal order; a pattern matching none is said.</summary>
        private static List<string> LayersNamed(CadCommandContext c, string typed)
        {
            var found = new SortedSet<string>(StringComparer.Ordinal);

            foreach (string pattern in typed.Split(','))
            {
                string trimmed = pattern.Trim();

                if (trimmed.Length == 0)
                {
                    continue;
                }

                bool any = false;

                foreach (string name in LayerNames(c))
                {
                    if (CadWildcard.IsMatch(name, trimmed, true))
                    {
                        found.Add(name);
                        any = true;
                    }
                }

                if (!any)
                {
                    c.Write("No matching layer names found for \"" + trimmed + "\".");
                }
            }

            return new List<string>(found);
        }

        /// <summary>The layers of the objects selected, in ordinal order.</summary>
        private static List<string> LayersOfObjects(CadCommandContext c, IReadOnlyList<string> handles)
        {
            var found = new SortedSet<string>(StringComparer.Ordinal);

            foreach (string handle in handles)
            {
                DxfEntity entity = c.Document.FindEntity(handle);

                if (entity != null && c.Database.LayerTable.Has(entity.Layer))
                {
                    found.Add(c.Database.LayerTable[entity.Layer].Name);
                }
            }

            return new List<string>(found);
        }

        /// <summary>Writes a refusal on the command line.</summary>
        private static void Said(CadCommandContext c, CadEditResult result)
        {
            if (!result.Succeeded)
            {
                c.Write(result.Message);
            }
        }
    }
}
