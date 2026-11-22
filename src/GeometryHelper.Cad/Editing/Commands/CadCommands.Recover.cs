using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using GeometryHelper.Dxf;

namespace GeometryHelper.Cad
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
        /// AUDIT: "Fix any errors detected? [Yes/No] &lt;N&gt;:", then AutoCAD 2021's report, "Auditing Header", "Auditing Tables",
        /// "Auditing Entities Pass 1" and the objects it counted, a hundred at a time, "Pass 1 100     objects audited", Pass 2 the
        /// same, "Auditing Blocks" and the blocks audited, "Auditing AcDsRecords", each error the presenter found as
        /// <see cref="DxfDatabase.Audit"/> words it, "Total errors found 1 fixed 0" and "Erased 0 objects". With Yes, what can be
        /// fixed is fixed as one undo step.
        /// </summary>
        private static IEnumerable<CadPrompt> Audit(CadCommandContext c)
        {
            yield return CadPrompt.ForKeyword("Fix any errors detected?", new[] { "Yes", "No" }, "N");

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

            // AutoCAD counts the objects a hundred at a time: 100 on a blank drawing, 200 with 150 lines more (the count here is the
            // presenter's own, which is not AutoCAD's).
            string counted = (Math.Max(1, (c.Database.ApproxNumObjects + 99) / 100) * 100)
                .ToString(CultureInfo.InvariantCulture)
                .PadRight(8);
            int blocks = c.Database.BlockTable.Count(b => !b.IsLayout);
            c.Write("Auditing Header");
            c.Write("Auditing Tables");
            c.Write("Auditing Entities Pass 1");
            c.Write("Pass 1 " + counted + "objects audited");
            c.Write("Auditing Entities Pass 2");
            c.Write("Pass 2 " + counted + "objects audited");
            c.Write("Auditing Blocks");
            c.Write(" " + blocks.ToString(CultureInfo.InvariantCulture).PadRight(8) + "Blocks audited");
            c.Write("Auditing AcDsRecords");

            for (int i = 0; i < report.Count && i < CadRecovery.Shown; i++)
            {
                c.Write(report.Repairs[i].Message);
            }

            if (report.Count > CadRecovery.Shown)
            {
                c.Write(
                    (report.Count - CadRecovery.Shown).ToString(CultureInfo.InvariantCulture) + " more in the log."
                );
            }

            c.Write(
                "Total errors found "
                    + report.Count.ToString(CultureInfo.InvariantCulture)
                    + " fixed "
                    + report.FixedCount.ToString(CultureInfo.InvariantCulture)
            );
            c.Write("Erased 0 objects");
        }
    }
}
