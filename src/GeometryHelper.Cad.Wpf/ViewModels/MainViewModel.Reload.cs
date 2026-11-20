using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GeometryHelper.Cad.Wpf
{
    /// <summary>A document opened again from its file, in the same place of the list.</summary>
    public sealed partial class MainViewModel
    {
        /// <summary>
        /// Opens a document again from its file, in the same place of the list, after the file was written by something other
        /// than this window: a drawing an extension rebuilt and saved while it is open. The extensions are told that the old
        /// document is closed and the new one opened, as a file opened is; the view of each space the user zoomed or panned,
        /// and the space that was current, are kept where the new document has a space of that name, and a view still fitted
        /// to its drawing is fitted to the new one. The undo history, the selection and the lines of the command line of the
        /// old document are gone with it. A file that cannot be read leaves the old document as it was.
        /// </summary>
        /// <param name="path">The path of the file.</param>
        /// <param name="discardChanges">
        /// Whether changes not saved to the old document are dropped; false, as it is by default, keeps a document with
        /// such changes as it is and says so on the command line.
        /// </param>
        /// <returns>Whether the document was read again; false where the file is not open, has unsaved changes, or cannot be read.</returns>
        public bool Reload(string path, bool discardChanges = false)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            string full;

            try
            {
                full = System.IO.Path.GetFullPath(path);
            }
            catch (Exception ex)
                when (ex is ArgumentException || ex is NotSupportedException || ex is PathTooLongException)
            {
                Say("Cannot reload " + path + ": " + ex.Message);
                return false;
            }

            KnownFile file = _files.TryGetValue(full, out KnownFile known) && known.Document != null ? known : null;
            file = file
                ?? _files
                    .Values.Where(f => f.Document != null && SamePath(f.Document.FileName, full))
                    .OrderBy(f => f.Path, StringComparer.OrdinalIgnoreCase)
                    .FirstOrDefault();

            if (file == null)
            {
                return false;
            }

            CadDocument old = file.Document;
            string name = System.IO.Path.GetFileName(full);

            if (old.IsModified && !discardChanges)
            {
                Say(name + " has changes not saved: save them, or reload it and drop them.");
                return false;
            }

            CadDocument document;

            try
            {
                document = CadDocument.Open(full);
            }
            catch (Exception ex) when (!(ex is OutOfMemoryException))
            {
                Say("Cannot reload " + name + ": " + ex.Message);
                return false;
            }

            EndCommands(file.Session);

            // The views the user moved, by the name of their space: the spaces of the new document are other objects.
            Dictionary<string, ViewState> moved = file
                .Views.Where(p => !p.Value.FitPending)
                .GroupBy(p => p.Key.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().Value, StringComparer.OrdinalIgnoreCase);
            string space = old.CurrentSpace?.Name;

            LetGoOfDocument(file);
            bool wasCurrent = file == _current;

            if (wasCurrent)
            {
                old.Selection.Changed -= OnSelectionChanged;
                _current = null;
            }

            file.Document = null;
            file.Session = null;
            file.Synced = 0;
            file.Views.Clear();

            foreach (CadWorkspaceExtension extension in _extensions)
            {
                extension.DocumentOpened(document);
            }

            Attach(file, document);

            foreach (CadSpace each in document.Spaces)
            {
                if (moved.TryGetValue(each.Name, out ViewState view))
                {
                    file.Views[each] = view;
                }
            }

            CadSpace current =
                space == null
                    ? null
                    : document.Spaces.FirstOrDefault(s =>
                        string.Equals(s.Name, space, StringComparison.OrdinalIgnoreCase)
                    );

            if (current != null)
            {
                document.CurrentSpace = current;
            }

            if (wasCurrent)
            {
                Show(file);
            }
            else
            {
                RebuildRows();
            }

            Say("Reloaded " + document.Title + " from its file.");
            return true;
        }
    }
}
