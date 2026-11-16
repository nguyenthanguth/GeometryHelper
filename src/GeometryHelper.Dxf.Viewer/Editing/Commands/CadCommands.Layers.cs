using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// The layer commands of the Layers panel that act on the layer of an object picked: LAYOFF and LAYFRZ, a layer for each
    /// pick until Enter; LAYLCK and LAYULK, one; LAYISO, every layer but those of the objects selected off; and those that act
    /// on all: LAYON, LAYTHW, and LAYUNISO, which turns on what LAYISO turned off; and LAYERP, which puts back the last change to
    /// the settings of layers, and LAYERPMODE, which turns keeping them on or off. Each run is one undo step.
    /// </summary>
    internal static partial class CadCommands
    {
        /// <summary>The prefix the layers LAYISO turned off are remembered under, for LAYUNISO.</summary>
        private const string Isolated = "layiso:";

        // The settings of LAYOFF and LAYFRZ, kept for the session as AutoCAD keeps them for each: how an object in a paper space
        // viewport is treated (0 Vpfreeze, 1 Off at LAYOFF and Freeze at LAYFRZ, which says Off for both) and how deep a pick in
        // a block goes (0 Block, 1 Entity, 2 None). A pick takes the layer of the object by them (LayerPicked).
        private static readonly string[] BlockNesting = { "Block", "Entity", "None" };

        /// <summary>A layer LAYOFF or LAYFRZ changed, with the viewport it was frozen in where it was frozen in one alone, for U.</summary>
        private sealed class LayerChanged
        {
            public LayerChanged(string name, string viewport)
            {
                Name = name;
                Viewport = viewport;
            }

            public string Name { get; }

            public string Viewport { get; }
        }

        /// <summary>"Current settings: Viewports=Vpfreeze, Block nesting level=Block", written by LAYOFF and LAYFRZ before they ask.</summary>
        private static void WriteLayerSettings(CadCommandContext c, string key)
        {
            c.Write(
                "Current settings: Viewports="
                    + (c.Remembered(key + ".viewports", 0.0) > 0.5 ? "Off" : "Vpfreeze")
                    + ", Block nesting level="
                    + BlockNesting[(int)c.Remembered(key + ".block", 0.0)]
            );
        }

        /// <summary>
        /// The Settings option of LAYOFF and LAYFRZ: "Enter setting type for [Viewports/Block selection]:", then
        /// "In paper space viewport use [Vpfreeze/Off] &lt;Vpfreeze&gt;:" (Freeze in place of Off at LAYFRZ) or "Enter Block
        /// Selection nesting level [Block/Entity/None] &lt;Block&gt;:", each answered by a keyword or Enter, which keeps the
        /// setting; text that is none is "Invalid option keyword." and asked again. The prompt for the object comes back.
        /// </summary>
        private static IEnumerable<CadPrompt> LayerSettings(CadCommandContext c, string key, string offWord)
        {
            yield return CadPrompt.ForKeyword("Enter setting type for", new[] { "Viewports", "Block selection" });

            if (c.Input.Is("Viewports"))
            {
                double now = c.Remembered(key + ".viewports", 0.0);
                yield return CadPrompt.ForKeyword(
                    "In paper space viewport use",
                    offWord == "Off" ? new[] { "Vpfreeze", "Off" } : new[] { offWord, "Vpfreeze" },
                    now > 0.5 ? offWord : "Vpfreeze"
                );

                if (c.Input.Kind == CadInputKind.Keyword)
                {
                    c.Remember(key + ".viewports", c.Input.Is("Vpfreeze") ? 0.0 : 1.0);
                }
            }
            else if (c.Input.Is("Block selection"))
            {
                int now = (int)c.Remembered(key + ".block", 0.0);
                yield return CadPrompt.ForKeyword(
                    "Enter Block Selection nesting level",
                    BlockNesting,
                    BlockNesting[now]
                );

                if (c.Input.Kind == CadInputKind.Keyword)
                {
                    c.Remember(key + ".block", Array.IndexOf(BlockNesting, c.Input.Text));
                }
            }
        }

        private static IEnumerable<CadPrompt> LayerOff(CadCommandContext c)
        {
            var done = new List<LayerChanged>();
            WriteLayerSettings(c, "layoff");
            c.OpenUndoStep();

            while (true)
            {
                yield return CadPrompt
                    .ForEntity("Select an object on the layer to be turned off", new[] { "Settings", "Undo" })
                    .Expecting("Settings")
                    .MissingWith("Nothing Selected.");

                if (c.Input.Is("Settings"))
                {
                    foreach (CadPrompt prompt in LayerSettings(c, "layoff", "Off"))
                    {
                        yield return prompt;
                    }

                    continue;
                }

                if (c.Input.Is("Undo"))
                {
                    UndoLayer(c, done, n => CadLayerEditor.SetOn(c.Document, n, true));
                    continue;
                }

                string name = LayerOf(c, "layoff");

                if (name == null)
                {
                    yield break;
                }

                // In a viewport worked through, Viewports=Vpfreeze freezes the layer in that viewport alone.
                DxfViewport viewport = ViewportToFreezeIn(c, "layoff");

                if (string.Equals(name, c.Database.Clayer, StringComparison.OrdinalIgnoreCase))
                {
                    yield return CadPrompt.ForKeyword(
                        "Layer \""
                            + name
                            + "\" is current, do you want to "
                            + (viewport != null ? "Vpfreeze it" : "turn it off")
                            + "?",
                        new[] { "Yes", "No" },
                        "No"
                    );

                    if (!c.Input.Is("Yes"))
                    {
                        continue;
                    }
                }

                if (viewport != null)
                {
                    FreezeInViewport(c, done, viewport, name, "LAYOFF");
                    continue;
                }

                ChangeLayer(c, done, name, CadLayerEditor.SetOn(c.Document, name, false), "turned off");
            }
        }

        private static IEnumerable<CadPrompt> LayerFreeze(CadCommandContext c)
        {
            var done = new List<LayerChanged>();
            WriteLayerSettings(c, "layfrz");
            c.OpenUndoStep();

            while (true)
            {
                yield return CadPrompt
                    .ForEntity("Select an object on the layer to be frozen", new[] { "Settings", "Undo" })
                    .Expecting("Settings")
                    .MissingWith("Nothing Selected.");

                if (c.Input.Is("Settings"))
                {
                    foreach (CadPrompt prompt in LayerSettings(c, "layfrz", "Freeze"))
                    {
                        yield return prompt;
                    }

                    continue;
                }

                if (c.Input.Is("Undo"))
                {
                    UndoLayer(c, done, n => CadLayerEditor.SetFrozen(c.Document, n, false));
                    continue;
                }

                string name = LayerOf(c, "layfrz");

                if (name == null)
                {
                    yield break;
                }

                DxfViewport viewport = ViewportToFreezeIn(c, "layfrz");
                bool current = string.Equals(name, c.Database.Clayer, StringComparison.OrdinalIgnoreCase);

                if (current && viewport != null)
                {
                    yield return CadPrompt.ForKeyword(
                        "Layer \"" + name + "\" is current, do you want to Vpfreeze it?",
                        new[] { "Yes", "No" },
                        "No"
                    );

                    if (!c.Input.Is("Yes"))
                    {
                        continue;
                    }
                }
                else if (current)
                {
                    // LAYFRZ words it with one space after the full stop, where -LAYER and the Layer manager have two.
                    c.Write("Cannot freeze layer \"" + name + "\". It is the CURRENT layer.");
                    continue;
                }

                if (viewport != null)
                {
                    FreezeInViewport(c, done, viewport, name, "LAYFRZ");
                    continue;
                }

                ChangeLayer(c, done, name, CadLayerEditor.SetFrozen(c.Document, name, true), "frozen");
            }
        }

        /// <summary>
        /// The viewport LAYOFF or LAYFRZ freezes a layer in: the one worked through, where its Viewports setting is Vpfreeze, as
        /// AutoCAD 2021 does for an object picked in a paper space viewport; null where the layer is turned off or frozen
        /// everywhere, with the setting at Off or Freeze, in model space, and on the paper of a layout.
        /// </summary>
        private static DxfViewport ViewportToFreezeIn(CadCommandContext c, string key) =>
            c.Remembered(key + ".viewports", 0.0) > 0.5 ? null : c.Document.ActiveViewport;

        /// <summary>Freezes the layer in a viewport as LAYOFF and LAYFRZ with Vpfreeze do: "Layer "WALLS" has been frozen in this viewport."</summary>
        private static void FreezeInViewport(
            CadCommandContext c,
            List<LayerChanged> done,
            DxfViewport viewport,
            string name,
            string command
        )
        {
            CadEditResult result = CadViewportLayerEditor.SetFrozen(
                c.Document,
                new[] { viewport.Handle },
                new[] { name },
                true,
                command
            );

            if (result.Succeeded)
            {
                done.Add(new LayerChanged(name, viewport.Handle));
                c.Write("Layer \"" + name + "\" has been frozen in this viewport.");
            }
            else
            {
                c.Write(result.Message);
            }
        }

        private static IEnumerable<CadPrompt> LayerLock(CadCommandContext c, bool locked)
        {
            string name;

            // Enter picks nothing: "Nothing selected." and the prompt again, as AutoCAD 2021 asks until an object or Esc.
            while (true)
            {
                yield return CadPrompt
                    .ForEntity(
                        locked
                            ? "Select an object on the layer to be locked"
                            : "Select an object on the layer to be unlocked"
                    )
                    .Expecting("Last")
                    .MissingWith("Nothing Selected.");
                name = LayerOf(c);

                if (name != null || !c.Input.IsNone)
                {
                    break;
                }

                c.Write("Nothing selected.");
            }

            if (name != null)
            {
                c.OpenUndoStep();
                ChangeLayer(
                    c,
                    new List<LayerChanged>(),
                    name,
                    CadLayerEditor.SetLocked(c.Document, name, locked),
                    locked ? "locked" : "unlocked"
                );
            }
        }

        /// <summary>LAYON and LAYTHW: every layer turned on, or thawed.</summary>
        private static IEnumerable<CadPrompt> LayersAll(CadCommandContext c, bool thaw)
        {
            c.OpenUndoStep();

            foreach (string name in LayerNames(c))
            {
                DxfLayerTableRecord layer = c.Database.LayerTable[name];

                if (thaw ? layer.IsFrozen : layer.IsOff)
                {
                    CadEditResult result = thaw
                        ? CadLayerEditor.SetFrozen(c.Document, name, false)
                        : CadLayerEditor.SetOn(c.Document, name, true);

                    if (!result.Succeeded)
                    {
                        c.Write(result.Message);
                    }
                }
            }

            c.Write(thaw ? "All layers have been thawed." : "All layers have been turned on.");
            yield break;
        }

        /// <summary>LAYISO: the layers of the objects selected kept on, every other turned off, the current layer moved to one kept.</summary>
        private static IEnumerable<CadPrompt> LayerIsolate(CadCommandContext c)
        {
            yield return CadPrompt.ForSelection("Select objects on the layer(s) to be isolated");
            var kept = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string handle in c.Input.Handles)
            {
                DxfEntity entity = c.Document.FindEntity(handle);

                if (entity != null && c.Database.LayerTable.Has(entity.Layer))
                {
                    kept.Add(c.Database.LayerTable[entity.Layer].Name);
                }
            }

            if (kept.Count == 0)
            {
                yield break;
            }

            c.OpenUndoStep();

            if (!kept.Contains(c.Database.Clayer))
            {
                CadLayerEditor.SetCurrent(c.Document, kept.Min);
            }

            foreach (string name in LayerNames(c))
            {
                if (
                    !kept.Contains(name)
                    && !c.Database.LayerTable[name].IsOff
                    && CadLayerEditor.SetOn(c.Document, name, false).Succeeded
                )
                {
                    c.Remember(Isolated + name, 1.0);
                }
            }

            c.ClearsSelection = true;
            c.Write(
                kept.Count == 1
                    ? "Layer \"" + kept.Min + "\" has been isolated."
                    : kept.Count.ToString(CultureInfo.InvariantCulture) + " layers have been isolated."
            );
        }

        /// <summary>LAYUNISO: the layers the last LAYISOs turned off turned on again.</summary>
        private static IEnumerable<CadPrompt> LayerUnisolate(CadCommandContext c)
        {
            var names = new List<string>();

            foreach (string key in c.Session.Remembered.Keys)
            {
                if (key.StartsWith(Isolated, StringComparison.Ordinal))
                {
                    names.Add(key);
                }
            }

            if (names.Count == 0)
            {
                c.Write("No layers were isolated by LAYISO.");
                yield break;
            }

            names.Sort(StringComparer.Ordinal);
            c.OpenUndoStep();

            foreach (string key in names)
            {
                c.Session.Remembered.Remove(key);
                string name = key.Substring(Isolated.Length);

                if (c.Database.LayerTable.Has(name))
                {
                    CadLayerEditor.SetOn(c.Document, name, true);
                }
            }

            c.Write("Layers isolated by LAYISO have been restored.");
        }

        /// <summary>
        /// LAYERP: the last change or set of changes to the settings of layers undone, as AutoCAD 2021 words it; one undo step.
        /// </summary>
        private static IEnumerable<CadPrompt> LayerPrevious(CadCommandContext c)
        {
            var notes = new List<string>();
            CadEditResult result = c.Document.RestorePreviousLayers(notes);

            if (!result.Succeeded)
            {
                c.Write(result.Message);
                yield break;
            }

            c.Write("Restored previous layer status.");

            foreach (string note in notes)
            {
                c.Write(note);
            }
        }

        /// <summary>LAYERPMODE: turns the tracking of layer changes on or off, as AutoCAD 2021's command does; the mode it has is the default.</summary>
        private static IEnumerable<CadPrompt> LayerPreviousMode(CadCommandContext c)
        {
            yield return CadPrompt.ForKeyword(
                "Enter LAYERP mode",
                new[] { "ON", "OFF" },
                c.Document.LayerPreviousMode ? "ON" : "OFF"
            );
            c.Document.LayerPreviousMode = c.Input.Is("ON");
        }

        /// <summary>
        /// The layer of the object the last prompt picked, by its name in the table; null where none was picked. Given the key of
        /// LAYOFF or LAYFRZ, a pick on a block reference takes the layer by their Block selection setting, as AutoCAD 2021 does
        /// (<see cref="CadDocument.FindNestedPick"/>): Block the reference the object picked is in, the innermost, Entity the
        /// object itself, None the reference picked.
        /// </summary>
        private static string LayerOf(CadCommandContext c, string key = null)
        {
            DxfEntity entity =
                c.Input.Kind == CadInputKind.Selection ? c.Document.FindEntity(c.Input.Handles[0]) : null;

            if (key != null && entity is DxfBlockReference top && c.Input.IsPickedAt)
            {
                int nesting = (int)c.Remembered(key + ".block", 0.0);

                if (nesting != 2)
                {
                    entity = CadDocument.FindNestedPick(top, c.Input.Point, c.Session.PickAperture, nesting == 1);
                }
            }

            return entity == null || !c.Database.LayerTable.Has(entity.Layer)
                ? null
                : c.Database.LayerTable[entity.Layer].Name;
        }

        private static void ChangeLayer(
            CadCommandContext c,
            List<LayerChanged> done,
            string name,
            CadEditResult result,
            string what
        )
        {
            if (result.Succeeded)
            {
                done.Add(new LayerChanged(name, null));
                c.Write("Layer \"" + name + "\" has been " + what + ".");
            }
            else
            {
                c.Write(result.Message);
            }
        }

        private static void UndoLayer(CadCommandContext c, List<LayerChanged> done, Func<string, CadEditResult> back)
        {
            if (done.Count == 0)
            {
                c.Write("Everything has been undone.");
                return;
            }

            LayerChanged last = done[done.Count - 1];

            if (last.Viewport != null)
            {
                CadViewportLayerEditor.SetFrozen(
                    c.Document,
                    new[] { last.Viewport },
                    new[] { last.Name },
                    false,
                    c.Command.Name
                );
            }
            else
            {
                back(last.Name);
            }

            done.RemoveAt(done.Count - 1);
        }

        /// <summary>The names of the layers of the drawing, in ordinal order.</summary>
        private static List<string> LayerNames(CadCommandContext c)
        {
            var names = new List<string>();

            foreach (DxfLayerTableRecord layer in c.Database.LayerTable)
            {
                names.Add(layer.Name);
            }

            names.Sort(StringComparer.Ordinal);
            return names;
        }
    }
}
