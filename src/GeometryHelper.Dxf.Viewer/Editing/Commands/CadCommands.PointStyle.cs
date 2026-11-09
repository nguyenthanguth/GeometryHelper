using System;
using System.Collections.Generic;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// The point style, in AutoCAD's words: PTYPE (DDPTYPE) shows the window's Point Style dialog, or with none asks PDMODE and
    /// then PDSIZE on the command line; PDMODE and PDSIZE are system variables, asked as SETVAR asks them. Each change is one
    /// undo step and draws every point again.
    /// </summary>
    internal static partial class CadCommands
    {
        /// <summary>PTYPE: the window's Point Style dialog; with no window, PDMODE and then PDSIZE asked in turn.</summary>
        private static IEnumerable<CadPrompt> PointStyle(CadCommandContext c) => c.Session.Request("PTYPE", c.Document) ? Array.Empty<CadPrompt>() : PointStyleTyped(c);

        /// <summary>
        /// PDMODE, "Enter new value for PDMODE &lt;0&gt;:", a whole number from -32768 to 32767 (0 a dot, 1 nothing, 2 a plus, 3
        /// a cross, 4 a tick, plus 32 for a circle and 64 for a square); then PDSIZE, "Enter new value for PDSIZE &lt;0.0000&gt;:",
        /// a size in drawing units above nought, a percentage of the view below it, or nought for five per cent of the view.
        /// </summary>
        private static IEnumerable<CadPrompt> PointStyleTyped(CadCommandContext c)
        {
            foreach (CadPrompt prompt in AskVariable(c, "PDMODE"))
            {
                yield return prompt;
            }

            foreach (CadPrompt prompt in AskVariable(c, "PDSIZE"))
            {
                yield return prompt;
            }
        }
    }
}
