using System.Collections.Generic;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// GROUPEDIT, as AutoCAD 2021 asks it: "Select group or [Name]:" (an object of the group, or the group by name), then
    /// "Enter an option [Add objects/Remove objects/REName]:" again and again until Enter. Add and Remove ask the objects as
    /// -GROUP's options do (picked one by one, a group not taken whole); REName asks the new name. A pick that is in no group
    /// is told so and the group asked again.
    /// </summary>
    internal static partial class CadCommands
    {
        private static IEnumerable<CadPrompt> GroupEdit(CadCommandContext c)
        {
            string name = null;

            while (name == null)
            {
                yield return CadPrompt.ForEntity("Select group", new[] { "Name" });

                if (c.Input.Is("Name"))
                {
                    DxfGroup chosen = null;

                    foreach (CadPrompt prompt in AskGroup(c, "Enter a group name or", g => chosen = g))
                    {
                        yield return prompt;
                    }

                    if (chosen == null)
                    {
                        yield break;
                    }

                    name = chosen.Name;
                }
                else if (c.Input.Kind == CadInputKind.Selection)
                {
                    List<string> held = GroupsHolding(c, c.Input.Handles);

                    if (held.Count == 0)
                    {
                        c.Write("Object is not a member of any group.");
                        continue;
                    }

                    name = held[0];
                }
                else
                {
                    yield break;
                }
            }

            while (FindGroup(c, name) != null)
            {
                yield return CadPrompt.ForKeyword(
                    "Enter an option",
                    new[] { "Add objects", "Remove objects", "REName" }
                );

                if (c.Input.Kind != CadInputKind.Keyword)
                {
                    yield break;
                }

                IEnumerable<CadPrompt> rest;

                if (c.Input.Is("REName"))
                {
                    string current = name;
                    rest = RenameKnownGroup(c, current, renamed => name = renamed);
                }
                else
                {
                    rest = PickMembers(c, name, c.Input.Is("Add objects"));
                }

                foreach (CadPrompt prompt in rest)
                {
                    yield return prompt;
                }
            }
        }
    }
}
