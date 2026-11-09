using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// What RECOVER, AUDIT and the window's opening of a damaged file share: the recovered drawing as a document of no file of
    /// its own, and the lines that tell a report on the command line.
    /// </summary>
    public static class CadRecovery
    {
        /// <summary>How many repairs the command line shows of a report; the rest are in the log.</summary>
        public const int Shown = 20;

        /// <summary>
        /// Opens a damaged DXF file as <see cref="DxfDatabase.Recover(string, out DxfRepairReport)"/> recovers it, as a document
        /// named after the file with " (recovered)" behind it and no file of its own, so that saving it asks where and never
        /// writes over the damaged file: <c>plan.dxf (recovered)</c>.
        /// </summary>
        /// <param name="path">The path of the file.</param>
        /// <param name="report">What was found and repaired.</param>
        /// <returns>The document.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the path is null.</exception>
        /// <exception cref="IOException">Thrown when the file cannot be read.</exception>
        /// <exception cref="FormatException">Thrown for a binary DXF.</exception>
        public static CadDocument Open(string path, out DxfRepairReport report)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            DxfDatabase database = DxfDatabase.Recover(path, out report);
            return CadDocument.From(database, Path.GetFileName(path) + " (recovered)");
        }

        /// <summary>
        /// Gets whether nothing could be recovered of a file: not one record of a DXF was found in it, or what was repaired could
        /// not be read at all (<see cref="DxfRepairKind.Unreadable"/>, not fixed), so that the drawing is empty and is not worth
        /// opening: a letter saved as plan.dxf.
        /// </summary>
        /// <param name="report">The report of the recovery.</param>
        /// <returns>Whether nothing was recovered.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the report is null.</exception>
        public static bool NothingRecovered(DxfRepairReport report)
        {
            if (report == null)
            {
                throw new ArgumentNullException(nameof(report));
            }

            foreach (DxfRepair repair in report.Repairs)
            {
                if (repair.Kind == DxfRepairKind.Unreadable && !repair.IsFixed)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// The lines that tell a report on the command line, as AutoCAD's audit ends: its summary first, then the first
        /// <see cref="Shown"/> repairs, then how many more the log holds ("12 more in the log.").
        /// </summary>
        /// <param name="report">The report.</param>
        /// <returns>The lines.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the report is null.</exception>
        public static IReadOnlyList<string> Lines(DxfRepairReport report)
        {
            if (report == null)
            {
                throw new ArgumentNullException(nameof(report));
            }

            var lines = new List<string> { report.Summary };

            for (int i = 0; i < report.Count && i < Shown; i++)
            {
                lines.Add(report.Repairs[i].Message);
            }

            if (report.Count > Shown)
            {
                lines.Add((report.Count - Shown).ToString(CultureInfo.InvariantCulture) + " more in the log.");
            }

            return lines;
        }
    }
}
