using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// Layer states, as AutoCAD 2021 keeps and asks them: LAYERSTATE, the Layer States Manager, where the window has one,
    /// and -LAYER's stAte option, its ?, Save, Restore, Edit, Name, Delete, Import and EXport in AutoCAD's words. The states
    /// are the XRECORDs of ACAD_LAYERSTATES (<see cref="CadLayerStates"/>); each run is one undo step.
    /// </summary>
    internal static partial class CadCommands
    {
        private static readonly string[] LayerStateOptions = { "?", "Save", "Restore", "Edit", "Name", "Delete", "Import", "EXport" };

        /// <summary>LAYERSTATE: the Layer States Manager, its changes one undo step; with no window, -LAYER's stAte prompts.</summary>
        private static IEnumerable<CadPrompt> LayerStateManager(CadCommandContext c)
        {
            var request = new CadLayerStatesRequest(c.Document);
            ReadLayerStates(c, request);
            request.Action = CadDialogAction.Cancel;
            request.Changing = (what, texts) =>
            {
                string said = ChangeLayerStateInDialog(c, request, what, texts);
                ReadLayerStates(c, request);
                return said;
            };

            bool asked = c.Session.Request("LAYERSTATE", c.Document, request);
            request.Changing = null;

            if (!asked)
            {
                foreach (CadPrompt prompt in LayerStateTyped(c))
                {
                    yield return prompt;
                }

                yield break;
            }

            if (request.Action == CadDialogAction.Ok && CadLayerStates.Find(c.Database, request.Selected) != null)
            {
                RestoreLayerState(c, request.Selected, request.TurnOffMissing);
            }
        }

        /// <summary>The states read into the request; the one chosen forgotten where it is gone, and the state the drawing is as named.</summary>
        private static void ReadLayerStates(CadCommandContext c, CadLayerStatesRequest request)
        {
            request.States = CadLayerStates.Rows(c.Database, !request.HideXrefStates);

            if (request.Selected != null && !request.States.Any(s => string.Equals(s.Name, request.Selected, StringComparison.OrdinalIgnoreCase)))
            {
                request.Selected = null;
            }

            CadLayerStateRow same = request.States.FirstOrDefault(s => s.SameAsDrawing && (s.Mask & CadLayerStates.LastRestored) != 0)
                ?? request.States.FirstOrDefault(s => s.SameAsDrawing);
            request.Current = same?.Name ?? "*UNSAVED*";
        }

        /// <summary>A change of the dialog's made at once, an edit of the command's step; what it says where it is refused, else null.</summary>
        private static string ChangeLayerStateInDialog(CadCommandContext c, CadLayerStatesRequest request, string what, string[] texts)
        {
            string chosen = CadLayerStates.NameOf(c.Database, request.Selected);

            switch (what)
            {
                case "List":
                    return null;
                case "New":
                    string name = (texts[0] ?? string.Empty).Trim();

                    if (!CadLayerStates.IsName(name))
                    {
                        return "Invalid layer state name.";
                    }

                    if (CadLayerStates.Find(c.Database, name) != null)
                    {
                        return "A layer state named " + name + " already exists.";
                    }

                    SaveLayerState(c, name, (texts.Length > 1 ? texts[1] : null) ?? string.Empty, CadLayerStates.All);
                    request.Selected = name;
                    return null;
                case "Import":
                    return ImportLayerStates(c, texts[0], out List<string> read) ?? Chosen(request, read.LastOrDefault());
            }

            if (chosen == null)
            {
                return "Choose a layer state in the list first.";
            }

            switch (what)
            {
                case "Update":
                    DxfXrecord record = CadLayerStates.Find(c.Database, chosen);
                    int mask = CadLayerStates.MaskOf(record.Data) & ~CadLayerStates.LastRestored;
                    SaveLayerState(c, chosen, CadLayerStates.Text(record.Data, 301), mask);
                    return null;
                case "Rename":
                    string typed = (texts[0] ?? string.Empty).Trim();

                    if (!CadLayerStates.IsName(typed))
                    {
                        return "Invalid layer state name.";
                    }

                    string other = CadLayerStates.NameOf(c.Database, typed);

                    if (other != null && !string.Equals(other, chosen, StringComparison.Ordinal))
                    {
                        return "A layer state named " + typed + " already exists.";
                    }

                    c.OpenUndoStep();
                    c.Apply(new[] { new CadEditRecord(CadEditRecordKind.TableChanged, null, "Layer state", chosen, typed) }, s => CadLayerStates.Rename(s.Database, chosen, typed));
                    request.Selected = typed;
                    return null;
                case "Delete":
                    c.OpenUndoStep();
                    c.Apply(new[] { new CadEditRecord(CadEditRecordKind.TableChanged, null, "Layer state", chosen, null) }, s => CadLayerStates.Delete(s.Database, chosen));
                    request.Selected = null;
                    return null;
                case "Mask":
                    int wanted = int.Parse(texts[0], NumberStyles.Integer, CultureInfo.InvariantCulture);
                    int had = CadLayerStates.MaskOf(CadLayerStates.Find(c.Database, chosen).Data);
                    int written = (wanted & CadLayerStates.All) | (had & ~CadLayerStates.All);

                    if (written != had)
                    {
                        c.OpenUndoStep();
                        c.Apply(new[] { new CadEditRecord(CadEditRecordKind.TableChanged, null, "Layer state mask", chosen, Values(written)) }, s => CadLayerStates.SetMask(CadLayerStates.Find(s.Database, chosen), written));
                    }

                    return null;
                default:
                    return ExportLayerState(c, chosen, texts[0]);
            }
        }

        private static string Chosen(CadLayerStatesRequest request, string name)
        {
            if (name != null)
            {
                request.Selected = name;
            }

            return null;
        }

        /// <summary>
        /// -LAYER's stAte: AutoCAD's options until Enter, as AutoCAD 2021 asks them: ? lists the states, Save asks a name and
        /// then the properties to restore, Restore, Edit, Name and Delete a state, Import a .las file, EXport one state to one.
        /// </summary>
        private static IEnumerable<CadPrompt> LayerStateTyped(CadCommandContext c)
        {
            while (true)
            {
                yield return CadPrompt.ForKeyword("Enter an option", LayerStateOptions);

                if (!c.Input.Is("?") && c.Input.Kind != CadInputKind.Keyword)
                {
                    yield break;
                }

                if (c.Input.Is("?"))
                {
                    ListLayerStates(c);
                    continue;
                }

                if (c.Input.Is("Save"))
                {
                    yield return CadPrompt.ForString("Enter new layer state name");
                    string name = c.Input.Kind == CadInputKind.Text ? c.Input.Text.Trim() : string.Empty;

                    if (!CadLayerStates.IsName(name))
                    {
                        if (name.Length > 0)
                        {
                            c.Write("Invalid layer state name.");
                        }

                        continue;
                    }

                    if (CadLayerStates.Find(c.Database, name) != null)
                    {
                        yield return CadPrompt.ForKeyword("Layer state \"" + CadLayerStates.NameOf(c.Database, name) + "\" already exists. Overwrite?", new[] { "Yes", "No" }, "No");

                        if (!c.Input.Is("Yes"))
                        {
                            continue;
                        }
                    }

                    var mask = new Ref<int> { Value = CadLayerStates.All };

                    foreach (CadPrompt prompt in StatesToChange(c, mask))
                    {
                        yield return prompt;
                    }

                    SaveLayerState(c, name, string.Empty, mask.Value);
                    continue;
                }

                string option = c.Input.Text;

                if (option == "Import")
                {
                    yield return CadPrompt.ForString("Enter file name to import", Path.GetFileNameWithoutExtension(c.Document.FileName ?? "Drawing1"));
                    string file = c.Input.Kind == CadInputKind.Text ? c.Input.Text : Path.GetFileNameWithoutExtension(c.Document.FileName ?? "Drawing1");
                    string said = ImportLayerStates(c, file, out _);

                    if (said != null)
                    {
                        c.Write(said);
                    }

                    continue;
                }

                string verb = option == "Restore" ? "restore" : option == "Edit" ? "edit" : option == "Name" ? "rename" : option == "Delete" ? "delete" : "export";
                var chosen = new Ref<string>();

                foreach (CadPrompt prompt in AskLayerState(c, "Enter name of layer state to " + verb + " or", chosen))
                {
                    yield return prompt;
                }

                if (chosen.Value == null)
                {
                    continue;
                }

                string state = chosen.Value;

                switch (option)
                {
                    case "Restore":
                        RestoreLayerState(c, state, false);
                        break;
                    case "Edit":
                        DxfXrecord record = CadLayerStates.Find(c.Database, state);
                        int had = CadLayerStates.MaskOf(record.Data);
                        var edited = new Ref<int> { Value = had };

                        foreach (CadPrompt prompt in StatesToChange(c, edited))
                        {
                            yield return prompt;
                        }

                        if (edited.Value != had)
                        {
                            c.OpenUndoStep();
                            c.Apply(new[] { new CadEditRecord(CadEditRecordKind.TableChanged, null, "Layer state mask", state, Values(edited.Value)) }, s => CadLayerStates.SetMask(CadLayerStates.Find(s.Database, state), edited.Value));
                        }

                        break;
                    case "Name":
                        yield return CadPrompt.ForString("Enter new layer state name");
                        string typed = c.Input.Kind == CadInputKind.Text ? c.Input.Text.Trim() : string.Empty;
                        string taken = CadLayerStates.NameOf(c.Database, typed);

                        if (!CadLayerStates.IsName(typed))
                        {
                            c.Write("Invalid layer state name.");
                        }
                        else if (taken != null && !string.Equals(taken, state, StringComparison.Ordinal))
                        {
                            c.Write("A layer state named " + typed + " already exists.");
                        }
                        else
                        {
                            c.OpenUndoStep();
                            Said(c, c.Apply(new[] { new CadEditRecord(CadEditRecordKind.TableChanged, null, "Layer state", state, typed) }, s => CadLayerStates.Rename(s.Database, state, typed)));
                        }

                        break;
                    case "Delete":
                        c.OpenUndoStep();
                        c.Apply(new[] { new CadEditRecord(CadEditRecordKind.TableChanged, null, "Layer state", state, null) }, s => CadLayerStates.Delete(s.Database, state));
                        break;
                    default:
                        yield return CadPrompt.ForString("Export state to file name", state);
                        string path = c.Input.Kind == CadInputKind.Text ? c.Input.Text : state;
                        string said = ExportLayerState(c, state, path);

                        if (said != null)
                        {
                            c.Write(said);
                        }

                        break;
                }
            }
        }

        /// <summary>Asks the name of a state until one of the drawing's is given, ? listing them; null in the result where Enter or a name it has not is given.</summary>
        private static IEnumerable<CadPrompt> AskLayerState(CadCommandContext c, string message, Ref<string> result)
        {
            result.Value = null;

            while (true)
            {
                yield return CadPrompt.ForKeywordOrText(message, new[] { "?" }, null);

                if (c.Input.Is("?"))
                {
                    ListLayerStates(c);
                    continue;
                }

                if (c.Input.Kind != CadInputKind.Text)
                {
                    yield break;
                }

                string name = c.Input.Text.Trim();
                string found = CadLayerStates.Rows(c.Database, true).Select(r => r.Name).FirstOrDefault(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));

                if (found == null)
                {
                    c.Write("Unknown layer state name.");
                    continue;
                }

                result.Value = found;
                yield break;
            }
        }

        /// <summary>The properties a state restores listed, Yes or No, and switched one by one as typed, until Enter.</summary>
        private static IEnumerable<CadPrompt> StatesToChange(CadCommandContext c, Ref<int> mask)
        {
            string[] keywords = { "On", "Frozen", "Lock", "Plot", "Newvpfreeze", "Color", "lineType", "lineWeight", "TRansparency", "plotStyle" };

            while (true)
            {
                c.Write("States");

                foreach (KeyValuePair<string, int> property in CadLayerStates.Properties)
                {
                    c.Write(property.Key.PadRight(20) + ((mask.Value & property.Value) != 0 ? "Yes" : "No"));
                }

                yield return CadPrompt.ForKeyword("Enter states to change", keywords);

                if (c.Input.Kind != CadInputKind.Keyword)
                {
                    yield break;
                }

                int at = Array.IndexOf(keywords, c.Input.Text);
                mask.Value ^= CadLayerStates.Properties[at].Value;
            }
        }

        /// <summary>-LAYER State ?: the states, hidden ones left out, under AutoCAD's heading.</summary>
        private static void ListLayerStates(CadCommandContext c)
        {
            c.Write("Layer state");
            c.Write("-----------------------------");

            foreach (CadLayerStateRow row in CadLayerStates.Rows(c.Database, true))
            {
                c.Write(row.Name);
            }
        }

        /// <summary>A state saved from the layers as they stand, an edit of the command's step.</summary>
        private static void SaveLayerState(CadCommandContext c, string name, string description, int mask)
        {
            c.OpenUndoStep();
            c.Apply(new[] { new CadEditRecord(CadEditRecordKind.TableChanged, null, "Layer state", null, name) }, s => CadLayerStates.Save(s.Database, name, description, mask));
        }

        /// <summary>A state restored into the layers, an edit of the command's step.</summary>
        internal static void RestoreLayerState(CadCommandContext c, string name, bool turnOffMissing)
        {
            var planned = new List<CadEditRecord> { new CadEditRecord(CadEditRecordKind.TableChanged, null, "Restore layer state", null, name) { IsLayerLook = true } };
            c.OpenUndoStep();
            c.Apply(planned, s => CadLayerStates.Restore(s.Database, name, turnOffMissing));
        }

        /// <summary>The states of a .las file read in, an edit of the command's step; what it says where it cannot, else null.</summary>
        private static string ImportLayerStates(CadCommandContext c, string typed, out List<string> read)
        {
            read = new List<string>();
            string path = CadLayerStates.LasPath(c.Document, typed);
            string text;

            try
            {
                text = File.ReadAllText(path);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is ArgumentException || exception is NotSupportedException)
            {
                GeometryHelperLog.Debug("CadCommands: the layer states file " + path + " could not be read: " + exception.Message);
                return "Cannot find file \"" + path + "\".";
            }

            List<string> names = null;
            c.OpenUndoStep();
            c.Apply(new[] { new CadEditRecord(CadEditRecordKind.TableChanged, null, "Import layer states", null, path) }, s => names = CadLayerStates.Import(s.Database, text));

            if (names == null)
            {
                return "The file \"" + path + "\" is not a layer states file.";
            }

            read = names;
            return null;
        }

        /// <summary>A state written to a .las file as AutoCAD's Export writes it; what it says where it cannot, else null.</summary>
        private static string ExportLayerState(CadCommandContext c, string name, string typed)
        {
            string path = CadLayerStates.LasPath(c.Document, typed);

            try
            {
                File.WriteAllText(path, CadLayerStates.ExportText(c.Database, name));
                return null;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is ArgumentException || exception is NotSupportedException)
            {
                GeometryHelperLog.Warn("CadCommands: the layer state " + name + " could not be written to " + path + ".", exception);
                return "Cannot write file \"" + path + "\".";
            }
        }
    }
}
