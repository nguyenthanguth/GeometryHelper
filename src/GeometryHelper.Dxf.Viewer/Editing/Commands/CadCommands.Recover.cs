using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// The commands that repair a drawing: RECOVER, which opens a damaged file as a new document of what could be read, and
    /// AUDIT, which checks the drawing shown and fixes it where asked, one undo step.
    /// </summary>
    internal static partial class CadCommands
    {
        /// <summary>
        /// RECOVER: asks for the file, the drawing's own offered, and opens what could be recovered of it as a new document
        /// "plan.dxf (recovered)", which the window shows; the report is told on the command line.
        /// </summary>
        private static IEnumerable<CadPrompt> Recover(CadCommandContext c)
        {
            string offered = c.Document.HasFile ? c.Document.FileName : null;
            yield return CadPrompt.ForString("Enter file name to recover", offered);
            string name = c.Input.Kind == CadInputKind.Text ? c.Input.Text.Trim().Trim('"') : offered;

            if (string.IsNullOrEmpty(name))
            {
                yield break;
            }

            CadDocument recovered;
            DxfRepairReport report;

            try
            {
                if (!Path.IsPathRooted(name) && c.Document.HasFile)
                {
                    name = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(c.Document.FileName)), name);
                }

                recovered = CadRecovery.Open(name, out report);
            }
            catch (Exception exception)
                when (exception is IOException
                    || exception is UnauthorizedAccessException
                    || exception is ArgumentException
                    || exception is NotSupportedException
                    || exception is FormatException
                )
            {
                c.Write("Cannot recover \"" + name + "\": " + exception.Message);
                yield break;
            }

            foreach (string line in CadRecovery.Lines(report))
            {
                c.Write(line);
            }

            if (CadRecovery.NothingRecovered(report))
            {
                c.Write("Nothing could be recovered of \"" + name + "\".");
                yield break;
            }

            if (!c.Session.Request("RECOVER", recovered))
            {
                c.Write("The recovered drawing " + recovered.Title + " is shown by the window.");
            }
        }

        /// <summary>
        /// AUDIT: checks the drawing as <see cref="DxfDatabase.Audit"/> does and tells the report; with Yes, what can be fixed
        /// is fixed as one undo step.
        /// </summary>
        private static IEnumerable<CadPrompt> Audit(CadCommandContext c)
        {
            yield return CadPrompt.ForKeyword("Fix any errors detected?", new[] { "Yes", "No" }, "No");

            if (c.Input.Kind != CadInputKind.Keyword && !c.Input.IsNone)
            {
                yield break;
            }

            DxfRepairReport report = c.Database.Audit(false);

            // Only what the audit fixes in place makes an undo step: an INSERT of no block, a handle two records have and the
            // like are told, and RECOVER fixes the second.
            if (
                c.Input.Is("Yes")
                && report.Repairs.Any(r =>
                    r.Kind >= DxfRepairKind.BadNumber && r.Kind <= DxfRepairKind.WrongLoopCount
                    || (r.Kind >= DxfRepairKind.MissingLayer && r.Kind <= DxfRepairKind.MissingDimStyle)
                    || r.Kind == DxfRepairKind.BrokenDictionaryEntry
                )
            )
            {
                CadEditResult result = c.Apply(
                    new[] { new CadEditRecord(CadEditRecordKind.TableChanged, null, "Audit", null, null) },
                    s => report = s.Database.Audit(true)
                );

                if (!result.Succeeded)
                {
                    yield break;
                }
            }

            foreach (string line in CadRecovery.Lines(report))
            {
                c.Write(line);
            }
        }
    }
}
