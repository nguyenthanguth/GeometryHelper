using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace GeometryHelper.Cad.Wpf
{
    /// <summary>
    /// The rows of the DRAWINGS list picked and acted on: a click selects a row and shows nothing, Ctrl+click adds or removes
    /// one, Shift+click takes the rows from the last clicked; a double click or Enter opens one; the right-click menu is AutoCAD's file tab menu: it makes, opens, saves,
    /// and closes drawings (each close through the window's close path), and copies a path or shows the file in Explorer.
    /// </summary>
    public sealed partial class MainViewModel
    {
        private readonly List<string> _selectedPaths = new List<string>();
        private string _anchorPath;

        /// <summary>Gets the rows selected in the DRAWINGS list, in the order of the list.</summary>
        public IReadOnlyList<FileRowViewModel> SelectedFileRows => FileRows.Where(r => r.IsSelected).ToList();

        /// <summary>
        /// A click on a row of the DRAWINGS list: it selects the row and opens nothing, as in a list of files. With
        /// <paramref name="control"/> the row is added to the selection or taken out of it; with <paramref name="shift"/> the
        /// rows from the one clicked last to this one are selected, added to the selection with both. A header selects nothing.
        /// </summary>
        /// <param name="row">The row.</param>
        /// <param name="control">Whether Ctrl is held.</param>
        /// <param name="shift">Whether Shift is held.</param>
        public void SelectFileRow(FileRowViewModel row, bool control = false, bool shift = false)
        {
            if (row == null || row.IsHeader || row.Path == null)
            {
                return;
            }

            List<FileRowViewModel> files = FileRows.Where(r => !r.IsHeader).ToList();
            int anchor = files.FindIndex(r => Same(r.Path, _anchorPath));

            if (shift && anchor >= 0)
            {
                int at = files.IndexOf(row);

                if (!control)
                {
                    _selectedPaths.Clear();
                }

                for (int i = Math.Min(anchor, at); i <= Math.Max(anchor, at); i++)
                {
                    SetSelected(files[i].Path, true);
                }
            }
            else
            {
                if (control)
                {
                    SetSelected(row.Path, !_selectedPaths.Any(p => Same(p, row.Path)));
                }
                else
                {
                    _selectedPaths.Clear();
                    SetSelected(row.Path, true);
                }

                _anchorPath = row.Path;
            }

            foreach (FileRowViewModel file in files)
            {
                file.IsSelected = _selectedPaths.Any(p => Same(p, file.Path));
            }
        }

        /// <summary>
        /// A key pressed with the focus in the DRAWINGS list: Enter opens the row clicked last, or shows it where it is open;
        /// Delete is taken and does nothing, so that it neither erases objects of the drawing nor removes a file.
        /// </summary>
        /// <param name="key">The key, as WPF names it: Enter (or Return), Delete.</param>
        /// <returns>Whether the list took the key.</returns>
        public bool FileListKey(string key)
        {
            if (string.Equals(key, "Delete", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (
                !string.Equals(key, "Enter", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(key, "Return", StringComparison.OrdinalIgnoreCase)
            )
            {
                return false;
            }

            List<FileRowViewModel> selected = SelectedFileRows.ToList();
            FileRowViewModel row = selected.FirstOrDefault(r => Same(r.Path, _anchorPath))
                ?? (selected.Count == 1 ? selected[0] : null);

            if (row != null)
            {
                Activate(row);
            }

            return true;
        }

        /// <summary>
        /// Gets or sets how the window starts a program, for Open File Location: Explorer with the file selected. The window
        /// leaves the one that starts the process; a test sets its own, so that no Explorer opens.
        /// </summary>
        public Action<ProcessStartInfo> StartProcess { get; set; } = info => Process.Start(info);

        /// <summary>
        /// Gets the items of the right-click menu of a row of the DRAWINGS list, as AutoCAD 2021's file tab menu has them: New...,
        /// Open..., Save, Save As..., Save All; Close, Close Selected Drawings (only where several rows are chosen), Close All,
        /// Close All Other Drawings; Copy Full File Path, Open File Location. Save, Save As and Close act on the row clicked,
        /// bringing its drawing forward, and are grey on a row whose file is not open, where Open... opens that file; on an open
        /// row Open... shows the Open dialog. Copy Full File Path and Open File Location are grey for a drawing never saved.
        /// Close All Other Drawings is grey unless two drawings are open. Each close goes through <see cref="CloseDocument"/>'s
        /// question about changes not saved, one question for all of them where several have changes, and leaves a file on disk
        /// in the list.
        /// </summary>
        /// <param name="row">The row under the cursor.</param>
        /// <returns>The items; none for a header.</returns>
        public IReadOnlyList<CadMenuItemViewModel> FileRowMenu(FileRowViewModel row)
        {
            if (row == null || row.IsHeader || row.Path == null)
            {
                return Array.Empty<CadMenuItemViewModel>();
            }

            KnownFile file = Known(row.Path);
            bool isOpen = file?.Document != null;
            List<KnownFile> open = _files.Values.Where(f => f.Document != null).ToList();
            List<KnownFile> chosen = SelectedFileRows.Select(r => Known(r.Path)).ToList();
            List<KnownFile> selected = chosen.Where(f => f?.Document != null).ToList();
            List<KnownFile> others = open.Where(f => f != file).ToList();
            string path = isOpen ? (file.Document.HasFile ? file.Document.FileName : null) : row.Path;
            bool hasPath = path != null && System.IO.Path.IsPathRooted(path);
            var menu = new List<CadMenuItemViewModel>
            {
                new CadMenuItemViewModel("New...", () => NewCommand.Execute(null)),
                new CadMenuItemViewModel("Open...", () => OpenOrShowDialog(row, isOpen)),
                new CadMenuItemViewModel("Save", () => SaveFile(file), isOpen),
                new CadMenuItemViewModel("Save As...", () => SaveFileAs(file), isOpen),
                new CadMenuItemViewModel("Save All", () => SaveAllDocuments(), open.Count > 0),
                CadMenuItemViewModel.Separator(),
                new CadMenuItemViewModel("Close", () => CloseFiles(new[] { file }), isOpen),
            };

            if (SelectedFileRows.Count > 1)
            {
                menu.Add(
                    new CadMenuItemViewModel("Close Selected Drawings", () => CloseFiles(selected), selected.Count > 0)
                );
            }

            menu.Add(new CadMenuItemViewModel("Close All", () => CloseAllDocuments(), open.Count > 0));
            menu.Add(
                new CadMenuItemViewModel(
                    "Close All Other Drawings",
                    () => CloseFiles(others),
                    open.Count > 1 && others.Count > 0
                )
            );
            menu.Add(CadMenuItemViewModel.Separator());
            menu.Add(
                new CadMenuItemViewModel(
                    "Copy Full File Path",
                    () => WriteClipboardText(path),
                    hasPath && WriteClipboardText != null
                )
            );
            menu.Add(new CadMenuItemViewModel("Open File Location", () => ShowInExplorer(path), hasPath));

            // What the plugins add comes after the presenter's items, behind a separator.
            menu.AddRange(PluginRowItems(row));
            return menu;
        }

        /// <summary>
        /// Gets the items of the right-click menu of the empty part of the DRAWINGS list, as the file tab menu of AutoCAD
        /// gives where no tab is under the cursor: New..., Open..., Save All and Close All, the last two grey with no drawing
        /// open.
        /// </summary>
        /// <returns>The four items.</returns>
        public IReadOnlyList<CadMenuItemViewModel> FileListMenu()
        {
            bool any = _files.Values.Any(f => f.Document != null);
            return new[]
            {
                new CadMenuItemViewModel("New...", () => NewCommand.Execute(null)),
                new CadMenuItemViewModel("Open...", () => OpenCommand.Execute(null)),
                new CadMenuItemViewModel("Save All", () => SaveAllDocuments(), any),
                new CadMenuItemViewModel("Close All", () => CloseAllDocuments(), any),
            };
        }

        /// <summary>Open... of a row: the Open dialog where the drawing is open, the file of the row opened where it is not.</summary>
        private void OpenOrShowDialog(FileRowViewModel row, bool isOpen)
        {
            if (isOpen)
            {
                OpenCommand.Execute(null);
            }
            else
            {
                Activate(row);
            }
        }

        /// <summary>Save As... of a row: its drawing shown first, then SAVEAS as the quick access Save As runs it.</summary>
        private void SaveFileAs(KnownFile file)
        {
            if (file != _current)
            {
                Show(file);
            }

            SaveAsCommand.Execute(null);
        }

        /// <summary>Opens Explorer with a file selected; a program that will not start is said on the command line and logged.</summary>
        private void ShowInExplorer(string path)
        {
            try
            {
                StartProcess?.Invoke(
                    new ProcessStartInfo("explorer.exe", "/select,\"" + path + "\"") { UseShellExecute = true }
                );
            }
            catch (Exception exception)
                when (exception is System.ComponentModel.Win32Exception || exception is InvalidOperationException)
            {
                GeometryHelperLog.Warn("MainViewModel: Explorer could not be started for " + path + ".", exception);
                Say("Explorer could not be started.");
            }
        }

        /// <summary>The selection of the list kept across a rebuild of its rows: the paths still shown, selected again.</summary>
        private void RestoreFileSelection()
        {
            _selectedPaths.RemoveAll(p => !FileRows.Any(r => !r.IsHeader && Same(r.Path, p)));

            foreach (FileRowViewModel row in FileRows.Where(r => !r.IsHeader))
            {
                row.IsSelected = _selectedPaths.Any(p => Same(p, row.Path));
            }
        }

        private void SetSelected(string path, bool on)
        {
            _selectedPaths.RemoveAll(p => Same(p, path));

            if (on)
            {
                _selectedPaths.Add(path);
            }
        }

        private KnownFile Known(string path) =>
            path != null && _files.TryGetValue(path, out KnownFile file) ? file : null;

        private static bool Same(string a, string b) =>
            a != null && b != null && string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }
}
