using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;

namespace GeometryHelper.Cad.Wpf
{
    /// <summary>The members the remote channel (<see cref="CadRemoteServer"/>) drives the window's drawings through.</summary>
    public sealed partial class MainViewModel
    {
        /// <summary>
        /// Gets or sets the line Global settings shows under "Allow other programs to connect": who can connect, and by which
        /// pipe. The window's remote switch sets it; a view model with no window says the channel is not started.
        /// </summary>
        internal string RemoteStatus { get; set; } = "The remote channel is not started in this window.";

        /// <summary>
        /// Gets the drawings open, by path (a drawing with no file by its name), so that two runs list them alike. Each is
        /// shown in the file list; the one the user works in is <see cref="Document"/>.
        /// </summary>
        internal IReadOnlyList<CadDocument> RemoteDocuments() =>
            _files
                .Values.Where(f => f.Document != null)
                .Select(f => f.Document)
                .Distinct()
                .OrderBy(d => d.FileName ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ThenBy(d => d.FileName ?? string.Empty, StringComparer.Ordinal)
                .ToList();

        /// <summary>Gets the command session of an open drawing; null for a drawing the window does not hold.</summary>
        internal CadCommandSession SessionOfDocument(CadDocument document) =>
            _files.Values.FirstOrDefault(f => f.Document == document)?.Session;

        /// <summary>Shows an open drawing, as clicking its entry in the file list does; false for a drawing the window does not hold.</summary>
        internal bool ShowDocument(CadDocument document)
        {
            KnownFile file = _files.Values.FirstOrDefault(f => f.Document == document);

            if (file == null)
            {
                return false;
            }

            if (_current != file)
            {
                Show(file);
            }

            return true;
        }

        /// <summary>
        /// Opens a file and shows it, read at once on this thread, or shows it where it is open already.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the file cannot be opened; the message is what the command line said.</exception>
        internal CadDocument OpenForRemote(string path)
        {
            int said = History.Count;

            if (!Open(path, false, out _) || _current == null)
            {
                throw new InvalidOperationException(SaidSince(said, "Cannot open " + path + "."));
            }

            return _current.Document;
        }

        /// <summary>Starts a new drawing, from a template where one is given, and shows it.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the template cannot be read.</exception>
        internal CadDocument NewForRemote(string template)
        {
            int said = History.Count;
            CadDocument made = string.IsNullOrEmpty(template) ? NewDrawing() : NewDrawingFrom(template);
            return made
                ?? throw new InvalidOperationException(
                    SaidSince(said, "Cannot start a drawing from " + template + ".")
                );
        }

        /// <summary>Saves an open drawing to its file, shown first.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the drawing cannot be shown or written; the message is what the command line said.</exception>
        internal void SaveForRemote(CadDocument document)
        {
            int said = History.Count;

            if (!ShowDocument(document) || !Save())
            {
                throw new InvalidOperationException(SaidSince(said, "The drawing could not be saved."));
            }
        }

        /// <summary>Saves an open drawing to a file in a version of AutoCAD, as Save Drawing As does with the type of that version chosen.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the drawing cannot be shown or written, or the version is none a drawing is saved as.</exception>
        internal void SaveAsForRemote(CadDocument document, string path, string version)
        {
            int said = History.Count;
            string type = null;

            if (version != null)
            {
                IEnumerable<string> types = CadDrawingFile.IsDwgName(path)
                    ? CadExportJob.DwgSaveTypes
                    : CadExportJob.DxfSaveTypes;
                type = types.FirstOrDefault(t =>
                    string.Equals(
                        CadExportJob.DwgVersionOfType(t) ?? CadExportJob.DxfVersionOfType(t),
                        version,
                        StringComparison.OrdinalIgnoreCase
                    )
                );

                if (type == null)
                {
                    throw new InvalidOperationException(
                        "A "
                            + (CadDrawingFile.IsDwgName(path) ? "DWG" : "DXF")
                            + " is not saved as version "
                            + version
                            + "."
                    );
                }
            }

            if (!ShowDocument(document) || !SaveAs(path, type))
            {
                throw new InvalidOperationException(SaidSince(said, "The drawing could not be saved."));
            }
        }

        /// <summary>Types a line on the command line of the drawing shown.</summary>
        internal void TypeForRemote(string line) => Execute(line);

        /// <summary>The last line the command line said since a count of lines, else the fallback: why a call that returned false failed.</summary>
        private string SaidSince(int count, string fallback) => History.Count > count ? History.Last() : fallback;
    }
}
