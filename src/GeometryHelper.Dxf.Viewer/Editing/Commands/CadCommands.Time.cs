using System;
using System.Collections.Generic;
using System.Globalization;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// TIME: shows the time now and the times of the drawing as AutoCAD 2021 writes them (when it was made and last saved, the
    /// time it has been edited in all, the user's elapsed timer and the time left to the next autosave), then asks "Enter option
    /// [Display/ON/OFF/Reset]:" until Esc or Enter. Display shows them again; ON and OFF start and stop the elapsed timer; Reset
    /// sets it to nought. The times read as the variables TDCREATE, TDUCREATE, TDUPDATE, TDUUPDATE, TDINDWG and TDUSRTIMER.
    /// </summary>
    internal static partial class CadCommands
    {
        private static IEnumerable<CadPrompt> Time(CadCommandContext c)
        {
            ShowTimes(c);

            while (true)
            {
                yield return CadPrompt.ForKeyword("Enter option", new[] { "Display", "ON", "OFF", "Reset" });
                CadDocumentTimes times = c.Document.Times;

                if (c.Input.Is("Display"))
                {
                    ShowTimes(c);
                }
                else if (c.Input.Is("ON"))
                {
                    times.SetTimer(true);
                    c.Write("Timer on.");
                }
                else if (c.Input.Is("OFF"))
                {
                    times.SetTimer(false);
                    c.Write("Timer off.");
                }
                else if (c.Input.Is("Reset"))
                {
                    times.ResetTimer();
                    c.Write("Timer reset to zero.");
                }
                else
                {
                    yield break;
                }
            }
        }

        /// <summary>The display of TIME: a blank line, the time now, the times of the drawing, a blank line.</summary>
        private static void ShowTimes(CadCommandContext c)
        {
            CadDocumentTimes times = c.Document.Times;
            int minutes = 10;
            CadSystemVariable saveTime = c.Session.Registry.FindVariable("SAVETIME");

            if (
                saveTime != null
                && int.TryParse(
                    saveTime.GetValue(c.Session),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out int interval
                )
                && interval > 0
            )
            {
                minutes = interval;
            }

            TimeSpan? autosave = times.NextAutosave(minutes);
            c.Write(string.Empty);
            c.Write(TimeLine("Current time:", CadDocumentTimes.Show(times.Clock())));
            c.Write("Times for this drawing:");
            c.Write(TimeLine("  Created:", CadDocumentTimes.Show(times.Created)));
            c.Write(TimeLine("  Last updated:", CadDocumentTimes.Show(times.Updated)));
            c.Write(TimeLine("  Total editing time:", CadDocumentTimes.Show(times.Editing)));
            c.Write(
                TimeLine(
                    "  Elapsed timer (" + (times.TimerRunning ? "on" : "off") + "):",
                    CadDocumentTimes.Show(times.Timer)
                )
            );
            c.Write(
                TimeLine(
                    "  Next automatic save in:",
                    autosave.HasValue ? CadDocumentTimes.Show(autosave.Value) : "<no modifications yet>"
                )
            );
            c.Write(string.Empty);
        }

        /// <summary>A label padded to the column the values of TIME start at.</summary>
        private static string TimeLine(string label, string value) => label.PadRight(26) + value;
    }
}
