using System.Collections.Generic;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// FILL and QTEXT, the two switches of how the drawing is shown: with FILL off (FILLMODE 0) solids, wide polylines, traces and
    /// solid or gradient hatches are drawn by their outlines; with QTEXT on (QTEXTMODE 1) every text is drawn as the box that
    /// holds it. Each asks "Enter mode [ON/OFF] &lt;ON&gt;:", is one undo step and draws the drawing again, as AutoCAD's does after
    /// its regen.
    /// </summary>
    internal static partial class CadCommands
    {
        /// <summary>FILL: the fills drawn (ON) or only the outlines of what is filled (OFF).</summary>
        private static IEnumerable<CadPrompt> Fill(CadCommandContext c)
        {
            bool now = c.Database.Fillmode;
            yield return CadPrompt.ForKeyword("Enter mode", new[] { "ON", "OFF" }, now ? "ON" : "OFF");

            if (c.Input.Kind == CadInputKind.Keyword)
            {
                SetSwitch(c, "$FILLMODE", now, c.Input.Is("ON"), d => d.Fillmode = c.Input.Is("ON"));
            }
        }

        /// <summary>QTEXT: texts drawn as their letters (OFF) or as the boxes that hold them (ON).</summary>
        private static IEnumerable<CadPrompt> QuickText(CadCommandContext c)
        {
            bool now = c.Database.Qtextmode;
            yield return CadPrompt.ForKeyword("Enter mode", new[] { "ON", "OFF" }, now ? "ON" : "OFF");

            if (c.Input.Kind == CadInputKind.Keyword)
            {
                SetSwitch(c, "$QTEXTMODE", now, c.Input.Is("ON"), d => d.Qtextmode = c.Input.Is("ON"));
            }
        }
    }
}
