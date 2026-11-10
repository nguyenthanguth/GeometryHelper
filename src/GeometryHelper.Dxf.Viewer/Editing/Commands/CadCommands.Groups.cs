using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// The groups, in AutoCAD's words: GROUP makes one of the objects selected, named or unnamed; -GROUP lists, orders, adds
    /// to, removes from, explodes, renames and makes selectable or not a group named; UNGROUP explodes the group of an object
    /// picked; PICKSTYLE says whether a pick takes a group whole. The groups are GROUP objects of the drawing's ACAD_GROUP
    /// dictionary, as AutoCAD writes them, and each change is one undo step.
    /// </summary>
    internal static partial class CadCommands
    {
        /// <summary>GROUP: "Select objects or [Name/Description]:" until Enter, then the group made of them, unnamed where no name was given.</summary>
        private static IEnumerable<CadPrompt> Group(CadCommandContext c)
        {
            string name = null;
            string description = string.Empty;
            IReadOnlyList<string> handles = c.PreSelected;

            while (handles.Count == 0)
            {
                yield return CadPrompt.ForSelection("Select objects", new[] { "Name", "Description" });

                if (c.Input.Is("Name"))
                {
                    foreach (CadPrompt prompt in AskNewGroupName(c, "Enter a group name or", n => name = n))
                    {
                        yield return prompt;
                    }

                    continue;
                }

                if (c.Input.Is("Description"))
                {
                    yield return CadPrompt.ForString("Enter a group description");

                    if (c.Input.Kind == CadInputKind.Text)
                    {
                        description = c.Input.Text.Trim();
                    }

                    continue;
                }

                if (c.Input.Kind != CadInputKind.Selection || c.Input.Handles.Count == 0)
                {
                    yield break;
                }

                handles = c.Input.Handles;
            }

            c.ClearsSelection = true;
            MakeGroup(c, name, description, handles);
        }

        /// <summary>
        /// -GROUP: "Enter a group option [?/Order/Add/Remove/Explode/REName/Selectable/Create] &lt;Create&gt;:", each option asking
        /// its group by name as AutoCAD's does; Create takes * for an unnamed group.
        /// </summary>
        private static IEnumerable<CadPrompt> GroupTyped(CadCommandContext c)
        {
            yield return CadPrompt.ForKeyword("Enter a group option", new[] { "?", "Order", "Add", "Remove", "Explode", "REName", "Selectable", "Create" }, "Create");

            if (c.Input.Kind != CadInputKind.Keyword)
            {
                yield break;
            }

            string option = c.Input.Text;
            IEnumerable<CadPrompt> rest;

            switch (option)
            {
                case "?":
                    rest = ListGroups(c);
                    break;
                case "Create":
                    rest = CreateGroupTyped(c);
                    break;
                case "REName":
                    rest = RenameGroup(c);
                    break;
                default:
                    rest = ChangeGroup(c, option);
                    break;
            }

            foreach (CadPrompt prompt in rest)
            {
                yield return prompt;
            }
        }

        /// <summary>UNGROUP: "Select group or [Name]:", the groups of the object picked, or the group named, exploded.</summary>
        private static IEnumerable<CadPrompt> Ungroup(CadCommandContext c)
        {
            var groups = new List<string>();

            if (c.PreSelected.Count > 0)
            {
                groups.AddRange(GroupsHolding(c, c.PreSelected));
            }
            else
            {
                yield return CadPrompt.ForEntity("Select group", new[] { "Name" });

                if (c.Input.Is("Name"))
                {
                    yield return CadPrompt.ForString("Enter a group name");
                    DxfGroup named = c.Input.Kind == CadInputKind.Text ? FindGroup(c, c.Input.Text.Trim()) : null;

                    if (named == null)
                    {
                        c.Write("Group not found.");
                        yield break;
                    }

                    groups.Add(named.Name);
                }
                else if (c.Input.Kind == CadInputKind.Selection)
                {
                    groups.AddRange(GroupsHolding(c, c.Input.Handles));
                }
                else
                {
                    yield break;
                }
            }

            if (groups.Count == 0)
            {
                c.Write("Object is not a member of any group.");
                yield break;
            }

            c.ClearsSelection = true;
            ExplodeGroups(c, groups);
        }

        private static IEnumerable<CadPrompt> AskNewGroupName(CadCommandContext c, string message, Action<string> take)
        {
            while (true)
            {
                yield return CadPrompt.ForKeywordOrText(message, new[] { "?" }, null);

                if (c.Input.Is("?"))
                {
                    WriteGroups(c, "*");
                    continue;
                }

                if (c.Input.Kind != CadInputKind.Text)
                {
                    yield break;
                }

                string typed = c.Input.Text.Trim();

                if (typed == "*")
                {
                    take(null);
                    yield break;
                }

                if (!IsGroupName(typed))
                {
                    c.Write("Invalid group name.");
                    continue;
                }

                if (FindGroup(c, typed) != null)
                {
                    c.Write("Group \"" + typed + "\" already exists.");
                    continue;
                }

                take(typed);
                yield break;
            }
        }

        private static IEnumerable<CadPrompt> CreateGroupTyped(CadCommandContext c)
        {
            bool given = false;
            string name = null;

            foreach (CadPrompt prompt in AskNewGroupName(c, "Enter a group name or", n => { name = n; given = true; }))
            {
                yield return prompt;
            }

            if (!given)
            {
                yield break;
            }

            yield return CadPrompt.ForString("Enter a group description");
            string description = c.Input.Kind == CadInputKind.Text ? c.Input.Text.Trim() : string.Empty;

            foreach (CadPrompt prompt in c.SelectObjects())
            {
                yield return prompt;
            }

            if (c.Selected.Count > 0)
            {
                MakeGroup(c, name, description, c.Selected);
            }
        }

        private static IEnumerable<CadPrompt> ListGroups(CadCommandContext c)
        {
            yield return CadPrompt.ForString("Enter group name(s) to list", "*");
            WriteGroups(c, c.Input.Kind == CadInputKind.Text && c.Input.Text.Trim().Length > 0 ? c.Input.Text.Trim() : "*");
        }

        private static IEnumerable<CadPrompt> RenameGroup(CadCommandContext c)
        {
            DxfGroup group = null;

            foreach (CadPrompt prompt in AskGroup(c, "Enter a group name to rename or", g => group = g))
            {
                yield return prompt;
            }

            if (group == null)
            {
                yield break;
            }

            string old = group.Name;
            string name = null;

            foreach (CadPrompt prompt in AskNewGroupName(c, "Enter a new name for group or", n => name = n))
            {
                yield return prompt;
            }

            if (name == null)
            {
                yield break;
            }

            c.Apply(new[] { new CadEditRecord(CadEditRecordKind.TableChanged, null, "Group", old, name) }, s => FindGroup(c, old).Name = name);
        }

        /// <summary>-GROUP Order, Add, Remove, Explode and Selectable, each of a group asked by name.</summary>
        private static IEnumerable<CadPrompt> ChangeGroup(CadCommandContext c, string option)
        {
            DxfGroup group = null;

            foreach (CadPrompt prompt in AskGroup(c, "Enter a group name or", g => group = g))
            {
                yield return prompt;
            }

            if (group == null)
            {
                yield break;
            }

            string name = group.Name;

            if (option == "Explode")
            {
                ExplodeGroups(c, new[] { name });
                yield break;
            }

            if (option == "Selectable")
            {
                bool selectable = group.Selectable;
                yield return CadPrompt.ForKeyword("This group is " + (selectable ? "selectable" : "unselectable") + ". Do you want to make it " + (selectable ? "unselectable" : "selectable") + "?", new[] { "Yes", "No" }, "Yes");

                if (c.Input.Is("Yes"))
                {
                    c.Apply(new[] { new CadEditRecord(CadEditRecordKind.TableChanged, null, "Group selectable", name, YesNoText(!selectable)) }, s => FindGroup(c, name).Selectable = !selectable);
                }

                yield break;
            }

            if (option == "Order")
            {
                foreach (CadPrompt prompt in OrderGroup(c, name))
                {
                    yield return prompt;
                }

                yield break;
            }

            bool add = option == "Add";
            c.Write(add ? "Select objects to add to group..." : "Select objects to remove from group...");

            // The objects are picked one by one here, as AutoCAD's: a group taken whole would add or remove every member.
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

            if (c.Input.Kind != CadInputKind.Selection || c.Input.Handles.Count == 0)
            {
                yield break;
            }

            var handles = new List<string>(c.Input.Handles);
            handles.Sort(StringComparer.OrdinalIgnoreCase);
            c.ClearsSelection = true;
            c.Apply(new[] { new CadEditRecord(CadEditRecordKind.TableChanged, null, "Group " + option.ToLowerInvariant(), name, string.Join(",", handles)) }, s =>
            {
                DxfGroup target = FindGroup(c, name);
                var entities = new List<DxfEntity>();

                foreach (string handle in handles)
                {
                    DxfEntity entity = c.Document.FindEntity(handle);

                    if (entity != null && target.Has(entity) != add)
                    {
                        entities.Add(entity);
                    }
                }

                if (add)
                {
                    target.Append(entities);
                }
                else
                {
                    target.Remove(entities);
                }
            });
        }

        /// <summary>-GROUP Order: "Replace objects at position number:", "Enter new position number for the objects", "Number of objects to re-order".</summary>
        private static IEnumerable<CadPrompt> OrderGroup(CadCommandContext c, string name)
        {
            int count = FindGroup(c, name).NumEntities;
            string last = (count - 1).ToString(CultureInfo.InvariantCulture);
            yield return CadPrompt.ForInteger("Replace objects at position number (0-" + last + ")");

            if (c.Input.Kind != CadInputKind.Number)
            {
                yield break;
            }

            int from = (int)c.Input.Number;
            yield return CadPrompt.ForInteger("Enter new position number for the objects (0-" + last + ")");

            if (c.Input.Kind != CadInputKind.Number)
            {
                yield break;
            }

            int to = (int)c.Input.Number;
            yield return CadPrompt.ForInteger("Number of objects to re-order", null, "1");
            int many = c.Input.Kind == CadInputKind.Number ? (int)c.Input.Number : 1;

            if (from < 0 || to < 0 || many < 1 || from + many > count || to + many > count)
            {
                c.Write("Invalid position number.");
                yield break;
            }

            c.Apply(new[] { new CadEditRecord(CadEditRecordKind.TableChanged, null, "Group order", name, Values(from, to, many)) }, s => FindGroup(c, name).Transfer(from, to, many));
        }

        private static IEnumerable<CadPrompt> AskGroup(CadCommandContext c, string message, Action<DxfGroup> take)
        {
            while (true)
            {
                yield return CadPrompt.ForKeywordOrText(message, new[] { "?" }, null);

                if (c.Input.Is("?"))
                {
                    WriteGroups(c, "*");
                    continue;
                }

                if (c.Input.Kind != CadInputKind.Text)
                {
                    yield break;
                }

                DxfGroup group = FindGroup(c, c.Input.Text.Trim());

                if (group == null)
                {
                    c.Write("Group not found.");
                    continue;
                }

                take(group);
                yield break;
            }
        }

        private static void MakeGroup(CadCommandContext c, string name, string description, IReadOnlyList<string> handles)
        {
            var sorted = new List<string>(handles);
            sorted.Sort(StringComparer.OrdinalIgnoreCase);
            string made = null;
            c.Document.EnsureNamedObjectsDictionary();
            CadEditResult result = c.Apply(new[] { new CadEditRecord(CadEditRecordKind.TableChanged, null, "Group", null, name ?? "*") }, s =>
            {
                DxfDBDictionary dictionary = s.Database.GroupDictionary;

                if (dictionary == null)
                {
                    (s.Database.NamedObjectsDictionary ?? throw new InvalidOperationException("A drawing of R12 has no dictionary to hold groups.")).SetAt("ACAD_GROUP", new DxfDBDictionary());
                    dictionary = s.Database.GroupDictionary;
                }

                var group = new DxfGroup(description, true);
                dictionary.SetAt(name ?? "*", group);
                var entities = new List<DxfEntity>();

                foreach (string handle in sorted)
                {
                    DxfEntity entity = c.Document.FindEntity(handle);

                    if (entity != null)
                    {
                        entities.Add(entity);
                    }
                }

                group.Append(entities);
                made = group.Name;
            });

            if (result.Succeeded)
            {
                c.Write("Group \"" + made + "\" has been created.");
            }
        }

        private static void ExplodeGroups(CadCommandContext c, IReadOnlyList<string> names)
        {
            CadEditResult result = c.Apply(new[] { new CadEditRecord(CadEditRecordKind.TableChanged, null, "Ungroup", string.Join(",", names), null) }, s =>
            {
                foreach (string name in names)
                {
                    FindGroup(c, name)?.Erase();
                }
            });

            if (result.Succeeded)
            {
                foreach (string name in names)
                {
                    c.Write("Group \"" + name + "\" has been exploded.");
                }
            }
        }

        /// <summary>The names of the groups that hold any of some entities, in the order of the group dictionary.</summary>
        private static List<string> GroupsHolding(CadCommandContext c, IReadOnlyList<string> handles)
        {
            var wanted = new HashSet<string>(handles, StringComparer.OrdinalIgnoreCase);
            var names = new List<string>();

            foreach (DxfGroup group in c.Document.Groups())
            {
                foreach (DxfEntity member in group.GetAllEntityIds())
                {
                    if (member.Handle != null && wanted.Contains(member.Handle))
                    {
                        names.Add(group.Name);
                        break;
                    }
                }
            }

            return names;
        }

        private static DxfGroup FindGroup(CadCommandContext c, string name)
        {
            DxfDBDictionary dictionary = c.Database.GroupDictionary;
            return name != null && name.Length > 0 && dictionary != null && dictionary.Contains(name) ? dictionary.GetAt(name) as DxfGroup : null;
        }

        private static void WriteGroups(CadCommandContext c, string pattern)
        {
            c.Write("Defined groups.");

            foreach (DxfGroup group in c.Document.Groups())
            {
                if (CadWildcard.IsMatch(group.Name, pattern, true))
                {
                    c.Write(group.Description.Length > 0 ? group.Name + "  " + group.Description : group.Name);
                }
            }
        }

        private static bool IsGroupName(string name) => name.Length > 0 && name.Length <= 255 && name.IndexOfAny("<>/\\\":;?*|,=`".ToCharArray()) < 0;

        private static string YesNoText(bool value) => value ? "Yes" : "No";
    }
}
