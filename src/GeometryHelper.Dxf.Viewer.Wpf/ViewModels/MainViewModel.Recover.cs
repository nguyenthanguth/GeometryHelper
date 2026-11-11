using System;
using System.Globalization;
using System.IO;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// The window's part of repairing drawings: a file that cannot be read as it is opened as what could be recovered of it,
    /// and the drawing RECOVER opened shown.
    /// </summary>
    public sealed partial class MainViewModel
    {
        /// <summary>
        /// Opens what can be recovered of a file that could not be read as it is, as a document "plan.dxf (recovered)" of no file
        /// of its own, so that saving it asks where and never writes over the damaged file; the command line says why the file
        /// could not be read, how many repairs were made, and the report. A file that cannot be read at all, locked or gone, is
        /// only said, and so is a DWG, which is no DXF to repair: "Cannot open plan.dwg:" and the DWG reader's words, as that
        /// a DWG of AC1014 (R14) is not read.
        /// </summary>
        private bool OpenRecovered(string full, Exception why)
        {
            string name = Path.GetFileName(full);

            if (why is IOException || why is UnauthorizedAccessException || IsDwgFile(full))
            {
                Say("Cannot open " + name + ": " + why.Message);
                return false;
            }

            CadDocument document;
            DxfRepairReport report;

            try
            {
                document = CadRecovery.Open(full, out report);
            }
            catch (Exception ex) when (!(ex is OutOfMemoryException))
            {
                GeometryHelperLog.Debug("MainViewModel: " + name + " could not be recovered either: " + ex.Message);
                document = null;
                report = null;
            }

            // A file of which nothing could be recovered, not a DXF at all, is not opened as an empty drawing.
            if (document == null || CadRecovery.NothingRecovered(report))
            {
                Say("Cannot open " + name + ": " + why.Message);
                return false;
            }

            foreach (CadWorkspaceExtension extension in _extensions)
            {
                extension.DocumentOpened(document);
            }

            OpenDocument(document);
            NoteRecentFile(full);
            Say("Cannot open " + name + " as it is: " + why.Message);
            Say(
                "The file could not be read as it is; opening what could be recovered: "
                    + report.Count.ToString(CultureInfo.InvariantCulture)
                    + (report.Count == 1 ? " repair." : " repairs.")
            );

            foreach (string line in CadRecovery.Lines(report))
            {
                Say(line);
            }

            return true;
        }

        /// <summary>Whether a file is a DWG by its first bytes; false where it cannot be read.</summary>
        private static bool IsDwgFile(string full)
        {
            try
            {
                return CadDrawingFile.IsDwg(full);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                return false;
            }
        }

        /// <summary>Shows the drawing RECOVER opened, as a document of its own in the list.</summary>
        private void ShowRecovered(CadUiRequestEventArgs e)
        {
            if (e.Document != null)
            {
                OpenDocument(e.Document);
                e.Handled = true;
            }
        }
    }
}
