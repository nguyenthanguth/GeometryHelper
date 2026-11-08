using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// The layer commands of the Layers panel that act on the layer of an object picked: LAYOFF and LAYFRZ, a layer for each
    /// pick until Enter; LAYLCK and LAYULK, one; LAYISO, every layer but those of the objects selected off; and those that act
    /// on all: LAYON, LAYTHW, and LAYUNISO, which turns on what LAYISO turned off. Each run is one undo step.
    /// </summary>
    internal static partial class CadCommands
    {
        /// <summary>The prefix the layers LAYISO turned off are remembered under, for LAYUNISO.</summary>
        private const string Isolated = "layiso:";

        private static IEnumerable<CadPrompt> LayerOff(CadCommandContext c)
        {
            var done = new List<string>();
            c.OpenUndoStep();

            while (true)
            {
                yield return CadPrompt.ForEntity("Select an object on the layer to be turned off", new[] { "Undo" });

                if (c.Input.Is("Undo"))
                {
                    UndoLayer(c, done, n => CadLayerEditor.SetOn(c.Document, n, true));
                    continue;
                }

                string name = LayerOf(c);

                if (name == null)
                {
                    yield break;
                }

                if (string.Equals(name, c.Database.Clayer, StringComparison.OrdinalIgnoreCase))
                {
                    yield return CadPrompt.ForKeyword("Layer \"" + name + "\" is the current layer, do you want to turn it off?", new[] { "Yes", "No" }, "No");

                    if (!c.Input.Is("Yes"))
                    {
                        continue;
                    }
                }

                ChangeLayer(c, done, name, CadLayerEditor.SetOn(c.Document, name, false), "turned off");
            }
        }

        private static IEnumerable<CadPrompt> LayerFreeze(CadCommandContext c)
        {
            var done = new List<string>();
            c.OpenUndoStep();

            while (true)
            {
                yield return CadPrompt.ForEntity("Select an object on the layer to be frozen", new[] { "Undo" });

                if (c.Input.Is("Undo"))
                {
                    UndoLayer(c, done, n => CadLayerEditor.SetFrozen(c.Document, n, false));
                    continue;
                }

                string name = LayerOf(c);

                if (name == null)
                {
                    yield break;
                }

                ChangeLayer(c, done, name, CadLayerEditor.SetFrozen(c.Document, name, true), "frozen");
            }
        }

        private static IEnumerable<CadPrompt> LayerLock(CadCommandContext c, bool locked)
        {
            yield return CadPrompt.ForEntity(locked ? "Select an object on the layer to be locked" : "Select an object on the layer to be unlocked");
            string name = LayerOf(c);

            if (name != null)
            {
                c.OpenUndoStep();
                ChangeLayer(c, new List<string>(), name, CadLayerEditor.SetLocked(c.Document, name, locked), locked ? "locked" : "unlocked");
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
                    CadEditResult result = thaw ? CadLayerEditor.SetFrozen(c.Document, name, false) : CadLayerEditor.SetOn(c.Document, name, true);

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
                if (!kept.Contains(name) && !c.Database.LayerTable[name].IsOff && CadLayerEditor.SetOn(c.Document, name, false).Succeeded)
                {
                    c.Remember(Isolated + name, 1.0);
                }
            }

            c.ClearsSelection = true;
            c.Write(kept.Count == 1 ? "Layer \"" + kept.Min + "\" has been isolated." : kept.Count.ToString(CultureInfo.InvariantCulture) + " layers have been isolated.");
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

        /// <summary>The layer of the object the last prompt picked, by its name in the table; null where none was picked.</summary>
        private static string LayerOf(CadCommandContext c)
        {
            DxfEntity entity = c.Input.Kind == CadInputKind.Selection ? c.Document.FindEntity(c.Input.Handles[0]) : null;
            return entity == null || !c.Database.LayerTable.Has(entity.Layer) ? null : c.Database.LayerTable[entity.Layer].Name;
        }

        private static void ChangeLayer(CadCommandContext c, List<string> done, string name, CadEditResult result, string what)
        {
            if (result.Succeeded)
            {
                done.Add(name);
                c.Write("Layer \"" + name + "\" has been " + what + ".");
            }
            else
            {
                c.Write(result.Message);
            }
        }

        private static void UndoLayer(CadCommandContext c, List<string> done, Func<string, CadEditResult> back)
        {
            if (done.Count == 0)
            {
                c.Write("Nothing to undo.");
                return;
            }

            back(done[done.Count - 1]);
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
