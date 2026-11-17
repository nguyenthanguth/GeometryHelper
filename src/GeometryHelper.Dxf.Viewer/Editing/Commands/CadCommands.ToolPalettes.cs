namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// TOOLPALETTES and TOOLPALETTESCLOSE, AutoCAD 2021's Tool Palettes commands: the window's tool palettes are shown, or
    /// closed. The palettes are a window of the presenter that stays open beside the drawing; with no window the commands say
    /// so.
    /// </summary>
    internal static partial class CadCommands
    {
        private static void AddToolPalettesTo(CadCommandRegistry registry)
        {
            registry.Add(new CadCommand("TOOLPALETTES", c => ToolPalettes(c, "TOOLPALETTES")));
            registry.Add(new CadCommand("TOOLPALETTESCLOSE", c => ToolPalettes(c, "TOOLPALETTESCLOSE")));
        }

        private static System.Collections.Generic.IEnumerable<CadPrompt> ToolPalettes(
            CadCommandContext c,
            string request
        )
        {
            if (!c.Session.Request(request, c.Document))
            {
                c.Write(
                    "The tool palettes are a palette of the window; there is none to "
                        + (request == "TOOLPALETTES" ? "show." : "close.")
                );
            }

            yield break;
        }
    }
}
