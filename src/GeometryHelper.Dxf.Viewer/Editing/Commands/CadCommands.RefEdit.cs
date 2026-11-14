using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// The commands of editing a reference in place, as AutoCAD 2021 names them: REFEDIT (and -REFEDIT) selects the reference
    /// and the objects to edit, REFSET adds objects to the working set and takes them out, REFCLOSE saves them back or
    /// discards them. The Reference Edit dialog and the Refedit toolbar are not built: both commands ask on the command line,
    /// as -REFEDIT does. The work is the document's (<see cref="CadDocument.OpenRefEdit"/> and the rest); these ask the
    /// questions. AutoCAD 2021's accoreconsole has none of the three commands, fed by a script or by standard input (it says
    /// <c>Unknown command</c>, and ARXLOAD of AcRefEd.arx fails there), so the prompts and messages are those of the string table
    /// of AcRefEdRes.dll, the resource file of AutoCAD 2021's own REFEDIT, read as data; what order they come in is the presenter's guess.
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
                    c.Write(CadRefEdit.NotAValidReference);
                }
            }

            IReadOnlyList<CadRefEditLevel> levels = document.FindRefEditLevels(handle, at, c.Session.PickAperture);

            if (levels.Count == 0)
            {
                c.Write(CadRefEdit.NotAValidReference);
                yield break;
            }

            int level = 0;

            while (levels.Count > 1)
            {
                c.Write("Reference: " + levels[level].Path);
                yield return CadPrompt.ForKeyword("Select nesting level", new[] { "Ok", "Next" }, "Next");

                if (c.Input.Is("Next") || c.Input.IsNone)
                {
                    level = (level + 1) % levels.Count;
                    continue;
                }

                if (c.Input.Is("Ok"))
                {
                    break;
                }

                yield break;
            }

            yield return CadPrompt.ForKeyword("Enter object selection method", new[] { "All", "Nested" }, "All");

            if (!(c.Input.Is("All") || c.Input.Is("Nested") || c.Input.IsNone))
            {
                yield break;
            }

            bool prompting = c.Input.Is("Nested");

            // A block with attribute definitions asks whether they are edited too; Enter is No, as the prompt says.
            if (document.RefEditBlockHasAttributeDefinitions(levels[level].Chain))
            {
                yield return CadPrompt.ForKeyword("Display attribute definitions", new[] { "Yes", "No" }, "No");
                document.RefEditAttributeDefinitions = c.Input.Is("Yes");
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
                    c.Write(CadRefEdit.NoObjects);
                    yield break;
                }
            }

            CadEditResult result = document.OpenRefEdit(levels[level].Chain, objects);

            if (!result.Succeeded)
            {
                c.Write(result.Message);
                yield break;
            }

            c.Write(document.RefEdit.WorkingSet.Count.ToString(CultureInfo.InvariantCulture) + " Added to working set");

            if (document.RefEdit.Skipped > 0)
            {
                c.Write(Objects(document.RefEdit.Skipped) + " cannot be edited in place and stay in the block.");
            }
        }

        /// <summary>"1 object", "2 objects".</summary>
        private static string Objects(int count) => Counted(count, "object");

        /// <summary>"1 block instance", "2 block instances".</summary>
        private static string Counted(int count, string noun) =>
            count.ToString(CultureInfo.InvariantCulture) + " " + noun + (count == 1 ? string.Empty : "s");

        /// <summary>
        /// REFSET: "Transfer objects between the RefEdit working set and host drawing..." and "Enter an option [Add/Remove]
        /// &lt;Add&gt;:", then "Select objects:", the objects selected before taken instead; Add puts objects of the drawing into
        /// the working set, Remove takes objects out of it and leaves them in the drawing. It says "** 2 selected objects are
        /// already in the working set **" (or "are not in") for those it has nothing to do with and "3 Added to working set" (or
        /// "Removed from") for the others.
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

            c.Write("Transfer objects between the RefEdit working set and host drawing...");
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
            var inSet = new HashSet<string>(edit.WorkingSet, StringComparer.OrdinalIgnoreCase);
            int before = inSet.Count;
            int unchanged = handles.Distinct(StringComparer.OrdinalIgnoreCase).Count(h => inSet.Contains(h) == !remove);
            CadEditResult result = remove ? document.RemoveFromRefEdit(handles) : document.AddToRefEdit(handles);

            if (!result.Succeeded)
            {
                c.Write(result.Message);
                yield break;
            }

            // AutoCAD counts the objects it has nothing to do with first; the others are the change in the set.
            if (unchanged > 0)
            {
                c.Write(
                    "** "
                        + Counted(unchanged, "selected object")
                        + (
                            remove
                                ? (unchanged == 1 ? " is not" : " are not")
                                : (unchanged == 1 ? " is already" : " are already")
                        )
                        + " in the working set **"
                );
            }

            int changed = Math.Abs(edit.WorkingSet.Count - before);

            if (changed > 0)
            {
                c.Write(
                    changed.ToString(CultureInfo.InvariantCulture)
                        + (remove ? " Removed from" : " Added to")
                        + " working set"
                );
            }
        }

        /// <summary>
        /// REFCLOSE: "Enter option [Save/Discard reference changes] &lt;Save&gt;:", the changes written back into the block or
        /// thrown away. Saved, it says "2 objects added to BOX" and "1 object removed from BOX" (each only where there is one) and
        /// "3 block instances updated" for the references of the block drawn anew; discarded, it says nothing.
        /// </summary>
        private static IEnumerable<CadPrompt> RefClose(CadCommandContext c)
        {
            CadRefEdit edit = c.Document.RefEdit;

            if (edit == null)
            {
                c.Write(CadRefEdit.NotEditing);
                yield break;
            }

            yield return CadPrompt
                .ForKeyword("Enter option", new[] { "Save", "Discard" }, "Save")
                .WithKeywordNote(" reference changes");
            bool save = !c.Input.Is("Discard");
            string name = edit.Name;
            c.Document.RefEditSaveCounts(out int added, out int removed, out int updated);
            CadEditResult result = c.Document.CloseRefEdit(save);

            if (!result.Succeeded)
            {
                c.Write(result.Message);
                yield break;
            }

            if (!save)
            {
                yield break;
            }

            if (added > 0)
            {
                c.Write(Objects(added) + " added to " + name);
            }

            if (removed > 0)
            {
                c.Write(Objects(removed) + " removed from " + name);
            }

            c.Write(Counted(updated, "block instance") + " updated");
        }
    }
}
