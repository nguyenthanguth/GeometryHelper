namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// ADCENTER and ADCCLOSE, AutoCAD 2021's DesignCenter commands: the window's DesignCenter palette is shown, or closed. The
    /// palette is a window of the presenter that stays open beside the drawing; with no window the commands say so.
    /// </summary>
    internal static partial class CadCommands
    {
        private static void AddDesignCenterTo(CadCommandRegistry registry)
        {
            registry.Add(new CadCommand("ADCENTER", c => DesignCenter(c, "ADCENTER")));
            registry.Add(new CadCommand("ADCCLOSE", c => DesignCenter(c, "ADCCLOSE")));
        }

        private static System.Collections.Generic.IEnumerable<CadPrompt> DesignCenter(
            CadCommandContext c,
            string request
        )
        {
            if (!c.Session.Request(request, c.Document))
            {
                c.Write(
                    "DesignCenter is a palette of the window; there is none to "
                        + (request == "ADCENTER" ? "show." : "close.")
                );
            }

            yield break;
        }
    }
}
