using System;
using System.Collections.Generic;
using System.Linq;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// The rows of the DRAWINGS list picked and acted on: a click selects a row and shows nothing, Ctrl+click adds or removes
    /// one, Shift+click takes the rows from the last clicked; a double click or Enter opens one; the right-click menu opens,
    /// closes the row, the rows selected, every other drawing or all of them, each through the window's close path.
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

            if (!string.Equals(key, "Enter", StringComparison.OrdinalIgnoreCase) && !string.Equals(key, "Return", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            List<FileRowViewModel> selected = SelectedFileRows.ToList();
            FileRowViewModel row = selected.FirstOrDefault(r => Same(r.Path, _anchorPath)) ?? (selected.Count == 1 ? selected[0] : null);

            if (row != null)
            {
                Activate(row);
            }

            return true;
        }

        /// <summary>
        /// Gets the items of the right-click menu of a row of the DRAWINGS list, in AutoCAD's words: Open (on while the drawing
        /// is not the one shown), Close (on while it is open), Close Selected Drawings, Close All Other Drawings and Close All
        /// Drawings (each on while it would close one). Each close goes through <see cref="CloseDocument"/>'s question about
        /// changes not saved, one question for all of them where several have changes, and leaves a file on disk in the list.
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
            List<KnownFile> open = _files.Values.Where(f => f.Document != null).ToList();
            List<KnownFile> selected = SelectedFileRows.Select(r => Known(r.Path)).Where(f => f?.Document != null).ToList();
            List<KnownFile> others = open.Where(f => f != file).ToList();
            return new[]
            {
                new CadMenuItemViewModel("Open", () => Activate(row), !row.IsCurrent),
                new CadMenuItemViewModel("Close", () => CloseFiles(new[] { file }), file?.Document != null),
                CadMenuItemViewModel.Separator(),
                new CadMenuItemViewModel("Close Selected Drawings", () => CloseFiles(selected), selected.Count > 0),
                new CadMenuItemViewModel("Close All Other Drawings", () => CloseFiles(others), others.Count > 0),
                new CadMenuItemViewModel("Close All Drawings", () => CloseFiles(open), open.Count > 0),
            };
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

        private KnownFile Known(string path) => path != null && _files.TryGetValue(path, out KnownFile file) ? file : null;

        private static bool Same(string a, string b) => a != null && b != null && string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }
}
