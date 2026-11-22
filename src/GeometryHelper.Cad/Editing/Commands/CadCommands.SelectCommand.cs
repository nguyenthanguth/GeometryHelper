using System.Collections.Generic;

namespace GeometryHelper.Cad
{
    /// <summary>
    /// SELECT: asks "Select objects:" with every way the prompt has (a pick, a window, a crossing, a fence, ALL, Last, Previous,
    /// Remove and Add) until Enter, and keeps what it took as the Previous selection set, for the next command's Previous and
    /// <c>(ssget "_P")</c> (AutoCAD 2021, the L8 measurements m/entmod s1 and s2). Typed, it leaves the objects selected, as the
    /// implied set the next command takes at once, and the next SELECT itself takes them with "N found" and no prompt; run by a
    /// routine, <c>(command "_.SELECT" ss "")</c>, it leaves nothing selected.
    /// </summary>
    internal static partial class CadCommands
    {
        private static IEnumerable<CadPrompt> SelectObjectsOnly(CadCommandContext c)
        {
            foreach (CadPrompt prompt in c.SelectObjects())
            {
                yield return prompt;
            }

            if (!c.Session.RunsForLisp)
            {
                c.ClearsSelection = false;
                c.Document.Selection.Set(c.Selected);
            }
        }
    }
}
