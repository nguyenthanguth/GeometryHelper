using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// CLASSICGROUP, AutoCAD's Object Grouping dialog, as AutoCAD 2021 opens it (its GROUP being the command-line form): the
    /// groups listed, Find Name, Highlight, New, Remove and Add asked on the command line with the dialog hidden, then the
    /// dialog shown again; Rename, Description, Explode, Selectable and Re-Order changing the drawing with it open. OK keeps
    /// every change and Cancel takes them back, the whole one undo step. With no window, -GROUP's prompts.
    /// </summary>
    internal static partial class CadCommands
    {
        /// <summary>CLASSICGROUP: the dialog, shown again after each pick, until OK or Cancel.</summary>
        private static IEnumerable<CadPrompt> GroupManager(CadCommandContext c)
        {
            var request = new CadGroupManagerRequest(c.Document);
            CadGroupMark mark = null;

            while (true)
            {
                ReadGroups(c, request);
                request.Action = CadDialogAction.Cancel;
                request.Pick = null;
                request.Changing = (what, numbers) =>
                {
                    if (mark == null)
                    {
                        c.OpenUndoStep();
                        mark = c.Document.MarkGroup();
                    }

                    string said = ChangeGroupInDialog(c, request, what, numbers);
                    ReadGroups(c, request);
                    return said;
                };

                bool asked = c.Session.Request("CLASSICGROUP", c.Document, request);
                request.Changing = null;

                if (!asked)
                {
                    foreach (CadPrompt prompt in GroupTyped(c))
                    {
                        yield return prompt;
                    }

                    yield break;
                }

                if (request.Action == CadDialogAction.Ok)
                {
                    yield break;
                }

                if (request.Action != CadDialogAction.SelectObjects)
                {
                    if (mark != null)
                    {
                        c.Document.BackTo(mark);
                    }

                    yield break;
                }

                if (mark == null)
                {
                    c.OpenUndoStep();
                    mark = c.Document.MarkGroup();
                }

                foreach (CadPrompt prompt in GroupPick(c, request))
                {
                    yield return prompt;
                }
            }
        }

        /// <summary>The groups of the drawing read into the request, in ordinal order of their names; the one chosen forgotten where it is gone.</summary>
        private static void ReadGroups(CadCommandContext c, CadGroupManagerRequest request)
        {
            var rows = new List<CadGroupRow>();

            foreach (DxfGroup group in c.Document.Groups())
            {
                rows.Add(new CadGroupRow(group.Name, group.Description, group.Selectable, group.IsAnonymous, group.NumEntities));
            }

            rows.Sort((a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.Name, b.Name) != 0 ? StringComparer.OrdinalIgnoreCase.Compare(a.Name, b.Name) : string.CompareOrdinal(a.Name, b.Name));
            request.Groups = rows;

            if (request.Selected != null && FindGroup(c, request.Selected) == null)
            {
                request.Selected = null;
            }
        }

        /// <summary>A change of the dialog's made at once, one edit of the command's step; what it says where it is refused, else null.</summary>
        private static string ChangeGroupInDialog(CadCommandContext c, CadGroupManagerRequest request, string what, int[] numbers)
        {
            DxfGroup group = FindGroup(c, request.Selected);

            if (group == null)
            {
                return "Choose a group in the list first.";
            }

            string name = group.Name;

            switch (what)
            {
                case "Rename":
                    string typed = (request.GroupName ?? string.Empty).Trim();

                    if (!IsGroupName(typed))
                    {
                        return "Invalid group name.";
                    }

                    if (FindGroup(c, typed) != null)
                    {
                        return "A group with this name already exists.";
                    }

                    c.Apply(new[] { new CadEditRecord(CadEditRecordKind.TableChanged, null, "Group", name, typed) }, s => FindGroup(c, name).Name = typed);
                    request.Selected = typed;
                    return null;
                case "Description":
                    string description = (request.Description ?? string.Empty).Trim();
                    c.Apply(new[] { new CadEditRecord(CadEditRecordKind.TableChanged, null, "Group description", name, description) }, s => FindGroup(c, name).Description = description);
                    return null;
                case "Explode":
                    ExplodeGroups(c, new[] { name });
                    request.Selected = null;
                    return null;
                case "Selectable":
                    bool selectable = !group.Selectable;
                    c.Apply(new[] { new CadEditRecord(CadEditRecordKind.TableChanged, null, "Group selectable", name, YesNoText(selectable)) }, s => FindGroup(c, name).Selectable = selectable);
                    return null;
                case "Reverse":
                    c.Apply(new[] { new CadEditRecord(CadEditRecordKind.TableChanged, null, "Group order", name, "reverse") }, s => FindGroup(c, name).Reverse());
                    return null;
                default:
                    int count = group.NumEntities;
                    int from = numbers[0];
                    int to = numbers[1];
                    int many = numbers[2];

                    if (from < 0 || to < 0 || many < 1 || from + many > count || to + many > count)
                    {
                        return "Invalid position number: the group holds " + count.ToString(CultureInfo.InvariantCulture) + " objects, numbered from 0.";
                    }

                    c.Apply(new[] { new CadEditRecord(CadEditRecordKind.TableChanged, null, "Group order", name, Values(from, to, many)) }, s => FindGroup(c, name).Transfer(from, to, many));
                    return null;
            }
        }

        /// <summary>A button ending in &lt; of the dialog: what it asks on the command line, then what it makes of the answer.</summary>
        private static IEnumerable<CadPrompt> GroupPick(CadCommandContext c, CadGroupManagerRequest request)
        {
            request.Message = null;
            DxfGroup group = FindGroup(c, request.Selected);

            switch (request.Pick)
            {
                case "Find Name":
                    // One object is picked, not the group it is in.
                    int style = c.Document.PickStyle;
                    c.Document.PickStyle = style & ~1;

                    try
                    {
                        yield return CadPrompt.ForEntity("Pick a member of a group");
                    }
                    finally
                    {
                        c.Document.PickStyle = style;
                    }

                    if (c.Input.Kind == CadInputKind.Selection)
                    {
                        List<string> names = GroupsHolding(c, c.Input.Handles);
                        names.Sort(StringComparer.OrdinalIgnoreCase);
                        c.Write(names.Count == 0 ? "Not a group member." : "Group Member List: " + string.Join(", ", names));

                        if (names.Count > 0)
                        {
                            request.Selected = names[0];
                        }
                    }

                    break;
                case "Highlight":
                    if (group == null)
                    {
                        request.Message = "Choose a group in the list first.";
                        break;
                    }

                    // The members shown selected on the canvas until Enter, as AutoCAD highlights them.
                    DxfEntity[] members = group.GetAllEntityIds();
                    int first = Math.Max(0, request.HighlightFrom);
                    int many = request.HighlightCount > 0 ? request.HighlightCount : members.Length - first;
                    List<string> shown = members.Skip(first).Take(Math.Max(0, many)).Where(e => e.Handle != null && c.Document.FindEntity(e.Handle) != null).Select(e => e.Handle).ToList();
                    c.Document.Selection.Set(shown);
                    yield return CadPrompt.ForString("Press ENTER to continue");
                    c.Document.Selection.Set(Array.Empty<string>());
                    break;
                case "Order Highlight":
                    if (group == null)
                    {
                        request.Message = "Choose a group in the list first.";
                        break;
                    }

                    // Highlight of Order Group: the members one at a time in their order, "Object 0 of 11" for a group of
                    // twelve; Next and Previous go round, eXit or Enter brings the dialog back.
                    List<string> order = group.GetAllEntityIds().Where(e => e.Handle != null && c.Document.FindEntity(e.Handle) != null).Select(e => e.Handle).ToList();
                    int at = 0;

                    while (order.Count > 0)
                    {
                        c.Document.Selection.Set(new[] { order[at] });
                        string last = (order.Count - 1).ToString(CultureInfo.InvariantCulture);
                        yield return CadPrompt.ForKeyword("Object " + at.ToString(CultureInfo.InvariantCulture) + " of " + last, new[] { "Next", "Previous", "eXit" });

                        if (c.Input.Is("Next"))
                        {
                            at = (at + 1) % order.Count;
                        }
                        else if (c.Input.Is("Previous"))
                        {
                            at = (at + order.Count - 1) % order.Count;
                        }
                        else
                        {
                            break;
                        }
                    }

                    c.Document.Selection.Set(Array.Empty<string>());
                    break;
                case "New":
                    string name = request.NewUnnamed ? null : (request.GroupName ?? string.Empty).Trim();

                    if (name != null && !IsGroupName(name))
                    {
                        request.Message = "Invalid group name.";
                        break;
                    }

                    if (name != null && FindGroup(c, name) != null)
                    {
                        request.Message = "A group with this name already exists.";
                        break;
                    }

                    yield return CadPrompt.ForSelection("Select objects for grouping");

                    if (c.Input.Kind == CadInputKind.Selection && c.Input.Handles.Count > 0)
                    {
                        MakeGroup(c, name, (request.Description ?? string.Empty).Trim(), c.Input.Handles, request.NewSelectable);
                        request.Selected = c.Document.Groups().Select(g => g.Name).Where(n => name == null ? n.StartsWith("*", StringComparison.Ordinal) : string.Equals(n, name, StringComparison.OrdinalIgnoreCase)).LastOrDefault();
                    }

                    break;
                case "Remove":
                case "Add":
                    if (group == null)
                    {
                        request.Message = "Choose a group in the list first.";
                        break;
                    }

                    bool add = request.Pick == "Add";
                    string target = group.Name;
                    c.Write(add ? "Select objects to add to group..." : "Select objects to remove from group...");

                    // The objects are picked one by one, as -GROUP picks them: a group taken whole would add or remove every member.
                    int pickStyle = c.Document.PickStyle;
                    c.Document.PickStyle = pickStyle & ~1;

                    try
                    {
                        yield return CadPrompt.ForSelection(add ? "Select objects" : "Remove objects");
                    }
                    finally
                    {
                        c.Document.PickStyle = pickStyle;
                    }

                    if (c.Input.Kind == CadInputKind.Selection && c.Input.Handles.Count > 0)
                    {
                        ChangeMembers(c, target, add, c.Input.Handles);
                    }

                    break;
            }

            request.HighlightFrom = 0;
            request.HighlightCount = 0;
        }
    }
}
