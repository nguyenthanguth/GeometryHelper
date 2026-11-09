using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Input;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>What to do with the changes of a document closed: save them, drop them, or not close it.</summary>
    public enum CadSaveChoice
    {
        /// <summary>Save, then close.</summary>
        Save,

        /// <summary>Close without saving.</summary>
        DontSave,

        /// <summary>Do not close.</summary>
        Cancel,
    }

    /// <summary>Closing documents: CLOSE, Ctrl+W, the x of a row of the file list, and the window closed.</summary>
    public sealed partial class MainViewModel
    {
        private DelegateCommand _closeFileCommand;

        /// <summary>
        /// Raised by CLOSE typed on the command line: the window closes the document shown once the command has ended
        /// (<see cref="CloseDocument"/>), as a command cannot close its own document while it runs.
        /// </summary>
        public event EventHandler CloseRequested;

        /// <summary>
        /// Gets or sets what is asked before a document with changes not saved is closed: "Save changes to plan.dxf?", the
        /// window's dialog. Null, as it is until set, keeps such a document open.
        /// </summary>
        public Func<CadDocument, CadSaveChoice> AskSaveChanges { get; set; }

        /// <summary>
        /// Gets or sets what is asked once, for all of them, before the window closes with documents not saved. Null keeps the
        /// window open.
        /// </summary>
        public Func<IReadOnlyList<CadDocument>, CadSaveChoice> AskSaveAll { get; set; }

        /// <summary>Gets the command of the x of a row of the file list, given the row: its document closed.</summary>
        public ICommand CloseFileCommand => _closeFileCommand ?? (_closeFileCommand = new DelegateCommand(p => CloseFile(p as FileRowViewModel)));

        /// <summary>Gets the documents open with changes not saved, in the order of their files.</summary>
        public IReadOnlyList<CadDocument> UnsavedDocuments => _files.Values.Where(f => f.Document != null && f.Document.IsModified)
            .OrderBy(f => f.Path, StringComparer.OrdinalIgnoreCase).Select(f => f.Document).ToList();

        /// <summary>
        /// Closes a document, as CLOSE does: with changes not saved, <see cref="AskSaveChanges"/> is asked first, and Save saves
        /// it (asking where for a drawing with no file). Its command line lets the plugins go, the extensions are told, and it
        /// leaves the open list; its file stays in the file list. The next document open is shown, or the start screen where
        /// none is left.
        /// </summary>
        /// <param name="document">The document; null for the one shown.</param>
        /// <returns>Whether it was closed; false where it was not open, or the question was cancelled, or saving failed.</returns>
        public bool CloseDocument(CadDocument document = null)
        {
            KnownFile file = document == null ? _current : _files.Values.FirstOrDefault(f => f.Document == document);

            if (file?.Document == null)
            {
                return false;
            }

            if (file.Session.IsRunning)
            {
                file.Session.Escape();
            }

            if (file.Document.IsModified)
            {
                CadSaveChoice choice = AskSaveChanges?.Invoke(file.Document) ?? CadSaveChoice.Cancel;

                if (choice == CadSaveChoice.Cancel || (choice == CadSaveChoice.Save && !SaveFile(file)))
                {
                    return false;
                }
            }

            Release(file);
            return true;
        }

        /// <summary>
        /// Says whether the window may close: with documents not saved, <see cref="AskSaveAll"/> is asked once for all of them,
        /// and Save saves each; Cancel, or a save that fails, keeps the window open.
        /// </summary>
        /// <returns>Whether the window may close.</returns>
        public bool ConfirmExit()
        {
            IReadOnlyList<CadDocument> unsaved = UnsavedDocuments;

            if (unsaved.Count == 0)
            {
                return true;
            }

            CadSaveChoice choice = AskSaveAll?.Invoke(unsaved) ?? CadSaveChoice.Cancel;

            if (choice == CadSaveChoice.Cancel)
            {
                return false;
            }

            if (choice == CadSaveChoice.Save)
            {
                foreach (CadDocument document in unsaved)
                {
                    KnownFile file = _files.Values.FirstOrDefault(f => f.Document == document);

                    if (file != null && !SaveFile(file))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Closes the documents of several files at once, as Close All Drawings of the list: with one, as
        /// <see cref="CloseDocument"/>; with more, one with changes not saved is asked about with <see cref="AskSaveChanges"/>
        /// and several with <see cref="AskSaveAll"/>, once, listing them. Cancel, or a save that fails, closes none. The one
        /// shown is let go last, so that the next document shown is one left open.
        /// </summary>
        private bool CloseFiles(IEnumerable<KnownFile> files)
        {
            List<KnownFile> open = files.Where(f => f?.Document != null).Distinct()
                .OrderBy(f => f.Path, StringComparer.OrdinalIgnoreCase).ThenBy(f => f.Path, StringComparer.Ordinal).ToList();

            if (open.Count <= 1)
            {
                return open.Count == 1 && CloseDocument(open[0].Document);
            }

            foreach (KnownFile file in open.Where(f => f.Session.IsRunning))
            {
                file.Session.Escape();
            }

            List<KnownFile> unsaved = open.Where(f => f.Document.IsModified).ToList();
            CadSaveChoice choice = unsaved.Count == 0 ? CadSaveChoice.DontSave
                : unsaved.Count == 1 ? AskSaveChanges?.Invoke(unsaved[0].Document) ?? CadSaveChoice.Cancel
                : AskSaveAll?.Invoke(unsaved.Select(f => f.Document).ToList()) ?? CadSaveChoice.Cancel;

            if (choice == CadSaveChoice.Cancel || (choice == CadSaveChoice.Save && unsaved.Any(f => !SaveFile(f))))
            {
                return false;
            }

            foreach (KnownFile file in open.Where(f => f != _current).Concat(open.Where(f => f == _current)).ToList())
            {
                Release(file);
            }

            return true;
        }

        private void CloseFile(FileRowViewModel row)
        {
            if (row != null && !row.IsHeader && row.Path != null && _files.TryGetValue(row.Path, out KnownFile file) && file.Document != null)
            {
                CloseDocument(file.Document);
            }
        }

        /// <summary>Saves a file, shown first, as Ctrl+S does.</summary>
        private bool SaveFile(KnownFile file)
        {
            if (file != _current)
            {
                Show(file);
            }

            return Save() && !file.Document.IsModified;
        }

        /// <summary>The document of a file let go: plugins, extensions, handlers; the file kept in the list where it is on disk.</summary>
        private void Release(KnownFile file)
        {
            CadDocument document = file.Document;
            Say("Closed " + document.Title + ".");
            DetachPlugins(file.Session.Registry);

            foreach (CadWorkspaceExtension extension in _extensions)
            {
                extension.DocumentClosed(document);
            }

            file.Session.PromptChanged -= OnPromptChanged;
            file.Session.UiRequested -= OnUiRequested;
            document.Changed -= OnDocumentChanged;
            bool wasCurrent = file == _current;

            if (wasCurrent)
            {
                document.Selection.Changed -= OnSelectionChanged;
            }

            file.Document = null;
            file.Session = null;
            file.Synced = 0;
            file.Views.Clear();
            string key = _files.FirstOrDefault(p => p.Value == file).Key;

            if (key != null && !(Path.IsPathRooted(file.Path) && File.Exists(file.Path)))
            {
                _files.Remove(key);
            }

            if (!wasCurrent)
            {
                RebuildRows();
                return;
            }

            _current = null;
            KnownFile next = _files.Values.Where(f => f.Document != null).OrderBy(f => f.Path, StringComparer.OrdinalIgnoreCase).FirstOrDefault();

            if (next != null)
            {
                Show(next);
                return;
            }

            // The start screen: no document shown.
            SyncInPlace();
            SpaceTabs.Clear();
            Layers = Array.Empty<CadLayerInfo>();
            RebuildRows();
            UpdateProperties();
            Raise(nameof(HasDocument));
            Raise(nameof(Document));
            Raise(nameof(Session));
            Raise(nameof(Space));
            Raise(nameof(View));
            Raise(nameof(CurrentLayer));
            RaiseStyles();
            Raise(nameof(Prompt));
            Raise(nameof(IsCommandRunning));
            Raise(nameof(IsModelSpace));
            Raise(nameof(SpaceLabel));
            Raise(nameof(ViewportLabel));
            RaiseModified();
            ((DelegateCommand)StartCommandCommand).RaiseCanExecuteChanged();
            Revision++;
        }

        /// <summary>CLOSE: the window closes the document once the command has ended; with no window listening, it says so.</summary>
        private IEnumerable<CadPrompt> CloseCommandBody(CadCommandContext c)
        {
            if (CloseRequested == null)
            {
                c.Write("CLOSE is done by the window; Ctrl+W closes the drawing shown.");
            }
            else
            {
                CloseRequested(this, EventArgs.Empty);
            }

            return Array.Empty<CadPrompt>();
        }
    }
}
