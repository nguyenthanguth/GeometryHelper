using System;
using System.Collections.Generic;
using System.Globalization;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// The point style, in AutoCAD's words: PTYPE (DDPTYPE) shows the window's Point Style dialog, or with none asks PDMODE and
    /// then PDSIZE on the command line; PDMODE and PDSIZE ask their variable as SETVAR asks it. Each change is one undo step
    /// and draws every point again.
    /// </summary>
    internal static partial class CadCommands
    {
        /// <summary>PTYPE: the window's Point Style dialog; with no window, PDMODE and then PDSIZE asked in turn.</summary>
        private static IEnumerable<CadPrompt> PointStyle(CadCommandContext c) => c.Session.Request("PTYPE", c.Document) ? Array.Empty<CadPrompt>() : PointStyleTyped(c);

        private static IEnumerable<CadPrompt> PointStyleTyped(CadCommandContext c)
        {
            foreach (CadPrompt prompt in PointMode(c))
            {
                yield return prompt;
            }

            foreach (CadPrompt prompt in PointSize(c))
            {
                yield return prompt;
            }
        }

        /// <summary>
        /// PDMODE: "Enter new value for PDMODE &lt;0&gt;:", a whole number from -32768 to 32767, as AutoCAD takes it (0 a dot, 1
        /// nothing, 2 a plus, 3 a cross, 4 a tick, plus 32 for a circle and 64 for a square); Enter keeps it.
        /// </summary>
        private static IEnumerable<CadPrompt> PointMode(CadCommandContext c)
        {
            int old = c.Database.Pdmode;

            while (true)
            {
                yield return CadPrompt.ForInteger("Enter new value for PDMODE", null, old.ToString(CultureInfo.InvariantCulture));

                if (c.Input.Kind != CadInputKind.Number)
                {
                    yield break;
                }

                if (c.Input.Number < short.MinValue || c.Input.Number > short.MaxValue)
                {
                    c.Write("Requires an integer between -32768 and 32767.");
                    continue;
                }

                int mode = (int)c.Input.Number;

                if (mode != old)
                {
                    string before = old.ToString(CultureInfo.InvariantCulture);
                    string after = mode.ToString(CultureInfo.InvariantCulture);
                    c.Apply(new[] { new CadEditRecord(CadEditRecordKind.SettingChanged, null, "$PDMODE", before, after) }, s => s.Database.Pdmode = mode);
                }

                yield break;
            }
        }

        /// <summary>
        /// PDSIZE: "Enter new value for PDSIZE &lt;0.0000&gt;:", a size in drawing units above nought, a percentage of the view
        /// below it, or nought for five per cent of the view, as AutoCAD takes it; Enter keeps it.
        /// </summary>
        private static IEnumerable<CadPrompt> PointSize(CadCommandContext c)
        {
            double old = c.Database.Pdsize;
            yield return CadPrompt.ForDistance("Enter new value for PDSIZE", null, null, Length(old));

            if (c.Input.Kind != CadInputKind.Number || double.IsNaN(c.Input.Number) || double.IsInfinity(c.Input.Number))
            {
                yield break;
            }

            double size = c.Input.Number;

            if (size != old)
            {
                c.Apply(new[] { new CadEditRecord(CadEditRecordKind.SettingChanged, null, "$PDSIZE", Values(old), Values(size)) }, s => s.Database.Pdsize = size);
            }
        }
    }
}
