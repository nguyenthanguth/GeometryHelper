using System.Collections.Generic;

namespace GeometryHelper.Cad
{
    /// <summary>
    /// MULTIPLE: asks "Enter command name to repeat:" and starts that command again each time it ends, until Esc ends it. A
    /// command that ends having asked nothing, or by an error, ends the repetition too, as a command of the wrong name does
    /// with AutoCAD's own "Unknown command".
    /// </summary>
    internal static partial class CadCommands
    {
        private static IEnumerable<CadPrompt> Multiple(CadCommandContext c)
        {
            yield return CadPrompt.ForString("Enter command name to repeat");

            if (c.Input.Kind != CadInputKind.Text || c.Input.Text.Trim().Length == 0)
            {
                yield break;
            }

            string name = c.Input.Text.Trim();

            if (c.Session.Registry.Find(name) == null && c.Session.Registry.FindVariable(name) == null)
            {
                c.Write("Unknown command \"" + name + "\".  Press F1 for help.");
                yield break;
            }

            c.Session.Repeat(name);
        }
    }
}
