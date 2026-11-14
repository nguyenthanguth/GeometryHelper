using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// The commands of editing a reference in place, as AutoCAD 2021 names them: REFEDIT (and -REFEDIT) selects the reference
    /// and the objects to edit, REFSET adds objects to the working set and takes them out, REFCLOSE saves them back or
    /// discards them. The Reference Edit dialog and the Refedit toolbar are not built: both commands ask on the command line,
    /// as -REFEDIT does. The work is the document's (<see cref="CadDocument.OpenRefEdit"/> and the rest); these ask the
    /// questions. AutoCAD 2021's accoreconsole has none of the three commands, so the words are the presenter's own.
    /// </summary>
    internal static partial class CadCommands
    {
        // The commands that cannot run while a reference is edited in place: those that open another edit or the Block Editor, and that save the drawing with the edit open.
        private static readonly HashSet<string> LeaveRefEdit = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "BEDIT",
            "-BEDIT",
            "REFEDIT",
            "-REFEDIT",
            "QSAVE",
            "SAVEAS",
        };

        internal static void AddRefEditTo(CadCommandRegistry registry)
        {
            registry.Add(new CadCommand("REFEDIT", RefEditCommand, true));
            registry.Add(new CadCommand("-REFEDIT", RefEditCommand, true));
            registry.Add(new CadCommand("REFSET", RefSet, true));
            registry.Add(new CadCommand("REFCLOSE", RefClose));

            // The name of the block being edited, "" with none, and how far the rest of the drawing is faded (AutoCAD 2021: 70, 0 to 90).
            registry.AddVariable(
                CadSystemVariable.ReadOnly("REFEDITNAME", CadVariableKind.Text, s =>
                    "\"" + (s.Document.RefEdit?.Name ?? string.Empty) + "\""
                )
            );
            registry.AddVariable(
                CadSystemVariable.Whole(
                    "XFADECTL",
                    false,
                    0,
                    90,
                    s => s.Document.XFadeControl,
                    (c, v) => c.Document.XFadeControl = v
                )
            );
        }

        /// <summary>Whether the edit of a reference in place refuses a command, "Not allowed while a reference is being edited.": BEDIT, REFEDIT again, QSAVE and SAVEAS.</summary>
        internal static bool LeavesRefEdit(string name) => name != null && LeaveRefEdit.Contains(name);

        /// <summary>
        /// REFEDIT and -REFEDIT: "Select reference:" (the reference selected before taken instead), then where the point picked
        /// reaches nested references "Reference: A" and "Enter nested reference choice [Ok/Next] &lt;Ok&gt;:", then "Enter object
        /// selection method [Automatic/Prompt/Settings] &lt;Automatic&gt;:": Automatic takes every object of the block, Prompt asks
        /// "Select nested objects or [All]:" for points, Settings asks whether to display the attribute definitions and whether to
        /// lock the objects outside the working set.
        /// </summary>
        private static IEnumerable<CadPrompt> RefEditCommand(CadCommandContext c)
        {
            CadDocument document = c.Document;
            string handle = null;
            GeoPoint2? at = null;

            foreach (string selected in c.PreSelected)
            {
                if (document.FindEntity(selected) is DxfBlockReference && document.FindSpace(selected) != null)
                {
                    handle = selected;
                    break;
                }
            }

            c.ClearsSelection = c.PreSelected.Count > 0;

            while (handle == null)
            {
                yield return CadPrompt.ForEntity("Select reference");

                if (c.Input.Kind != CadInputKind.Selection || c.Input.Handles.Count == 0)
                {
                    yield break;
                }

                string picked = c.Input.Handles[0];

                if (document.FindEntity(picked) is DxfBlockReference && document.FindSpace(picked) != null)
                {
                    handle = picked;
                    at = c.Input.IsPickedAt ? c.Input.Point : (GeoPoint2?)null;
                }
                else
                {
                    c.Write("Selected object is not a reference.");
                }
            }

            IReadOnlyList<CadRefEditLevel> levels = document.FindRefEditLevels(handle, at, c.Session.PickAperture);

            if (levels.Count == 0)
            {
                c.Write("Selected object is not a reference.");
                yield break;
            }

            int level = 0;

            while (levels.Count > 1)
            {
                c.Write("Reference: " + levels[level].Path);
                yield return CadPrompt.ForKeyword("Enter nested reference choice", new[] { "Ok", "Next" }, "Ok");

                if (c.Input.Is("Next"))
                {
                    level = (level + 1) % levels.Count;
                    continue;
                }

                if (c.Input.Is("Ok") || c.Input.IsNone)
                {
                    break;
                }

                yield break;
            }

            bool prompting = false;

            while (true)
            {
                yield return CadPrompt.ForKeyword(
                    "Enter object selection method",
                    new[] { "Automatic", "Prompt", "Settings" },
                    "Automatic"
                );

                if (c.Input.Is("Settings"))
                {
                    yield return CadPrompt.ForKeyword(
                        "Display attribute definitions for editing?",
                        new[] { "Yes", "No" },
                        document.RefEditAttributeDefinitions ? "Yes" : "No"
                    );
                    document.RefEditAttributeDefinitions = Chosen(c.Input, document.RefEditAttributeDefinitions);
                    yield return CadPrompt.ForKeyword(
                        "Lock objects not in working set?",
                        new[] { "Yes", "No" },
                        document.RefEditLocksObjects ? "Yes" : "No"
                    );
                    document.RefEditLocksObjects = Chosen(c.Input, document.RefEditLocksObjects);
                    continue;
                }

                if (!(c.Input.Is("Automatic") || c.Input.Is("Prompt") || c.Input.IsNone))
                {
                    yield break;
                }

                prompting = c.Input.Is("Prompt");
                break;
            }

            List<string> objects = null;

            if (prompting)
            {
                objects = new List<string>();

                while (true)
                {
                    yield return CadPrompt.ForPoint("Select nested objects", null, new[] { "All" });

                    if (c.Input.Is("All"))
                    {
                        objects = null;
                        break;
                    }

                    if (!c.Input.IsPoint)
                    {
                        break;
                    }

                    string found = document.FindRefEditObject(
                        levels[level].Chain,
                        c.Input.Point,
                        c.Session.PickAperture
                    );

                    if (found != null && !objects.Contains(found))
                    {
                        objects.Add(found);
                    }

                    c.Write(found == null ? "0 found" : "1 found");
                }

                if (objects != null && objects.Count == 0)
                {
                    c.Write("No objects selected.");
                    yield break;
                }
            }

            CadEditResult result = document.OpenRefEdit(levels[level].Chain, objects);

            if (!result.Succeeded)
            {
                c.Write(result.Message);
                yield break;
            }

            int count = document.RefEdit.WorkingSet.Count;
            c.Write(Objects(count) + " added to the working set.");

            if (document.RefEdit.Skipped > 0)
            {
                c.Write(Objects(document.RefEdit.Skipped) + " cannot be edited in place and stay in the block.");
            }
        }

        /// <summary>The answer of a Yes/No prompt: Enter keeps the value the prompt offered.</summary>
        private static bool Chosen(CadInput input, bool current) =>
            input.Is("Yes") ? true
            : input.Is("No") ? false
            : current;

        /// <summary>"1 object", "2 objects".</summary>
        private static string Objects(int count) =>
            count.ToString(CultureInfo.InvariantCulture) + (count == 1 ? " object" : " objects");

        /// <summary>
        /// REFSET: "Enter an option [Add/Remove] &lt;Add&gt;:" and "Select objects:", the objects selected before taken instead; Add
        /// puts objects of the drawing into the working set, Remove takes objects out of it and leaves them in the drawing.
        /// </summary>
        private static IEnumerable<CadPrompt> RefSet(CadCommandContext c)
        {
            CadDocument document = c.Document;
            CadRefEdit edit = document.RefEdit;

            if (edit == null)
            {
                c.Write(CadRefEdit.NotEditing);
                yield break;
            }

            yield return CadPrompt.ForKeyword("Enter an option", new[] { "Add", "Remove" }, "Add");
            bool remove = c.Input.Is("Remove");
            IReadOnlyList<string> handles;

            // The objects a REFSET adds are the ones the lock holds, so the selection does not leave those out.
            if (c.PreSelected.Count > 0)
            {
                handles = c.PreSelected;
                c.Session.RememberSelection(handles);
            }
            else
            {
                yield return CadPrompt.ForSelection();
                handles = c.Input.Handles;
            }

            c.ClearsSelection = true;
            int before = edit.WorkingSet.Count;
            CadEditResult result = remove ? document.RemoveFromRefEdit(handles) : document.AddToRefEdit(handles);

            if (!result.Succeeded)
            {
                c.Write(result.Message);
                yield break;
            }

            int changed = Math.Abs(edit.WorkingSet.Count - before);
            c.Write(
                changed == 0
                    ? "No objects " + (remove ? "removed from" : "added to") + " the working set."
                    : Objects(changed) + (remove ? " removed from" : " added to") + " the working set."
            );
        }

        /// <summary>REFCLOSE: "Enter option [Save/Discard] &lt;Save&gt;:", the changes written back into the block or thrown away.</summary>
        private static IEnumerable<CadPrompt> RefClose(CadCommandContext c)
        {
            CadRefEdit edit = c.Document.RefEdit;

            if (edit == null)
            {
                c.Write(CadRefEdit.NotEditing);
                yield break;
            }

            yield return CadPrompt.ForKeyword("Enter option", new[] { "Save", "Discard" }, "Save");
            bool save = !c.Input.Is("Discard");
            string name = edit.Name;
            int count = edit.WorkingSet.Count;
            CadEditResult result = c.Document.CloseRefEdit(save);

            if (!result.Succeeded)
            {
                c.Write(result.Message);
                yield break;
            }

            c.Write(
                save
                    ? "Reference edit saved: " + name + " (" + Objects(count) + ")."
                    : "Reference edit discarded: " + name + "."
            );
        }
    }
}
