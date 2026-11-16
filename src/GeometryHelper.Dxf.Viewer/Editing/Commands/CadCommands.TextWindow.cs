using System.Collections.Generic;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>TEXTSCR, which F2 runs: the text window with the command history, which the window shows.</summary>
    internal static partial class CadCommands
    {
        private static void AddTextWindowTo(CadCommandRegistry registry) =>
            registry.Add(new CadCommand("TEXTSCR", TextScreen));

        /// <summary>TEXTSCR: asks the window for its text window; with no window, the command says so.</summary>
        private static IEnumerable<CadPrompt> TextScreen(CadCommandContext c)
        {
            if (!c.Session.Request("TEXTSCR", c.Document))
            {
                c.Write("The text window is shown by the window.");
            }

            yield break;
        }
    }
}
