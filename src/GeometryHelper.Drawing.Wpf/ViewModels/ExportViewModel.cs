using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using GeometryHelper.Dxf.Viewer.Wpf;

namespace GeometryHelper.Drawing.Wpf
{
    /// <summary>A model drawing as the Export dialog lists it, ticked to be written.</summary>
    public sealed class ExportRow : ObservableObject
    {
        private readonly Action _ticked;
        private bool _isChecked;

        internal ExportRow(ModelDrawing drawing, bool isChecked, Action ticked)
        {
            Drawing = drawing;
            _isChecked = isChecked;
            _ticked = ticked;
        }

        /// <summary>Gets the drawing.</summary>
        public ModelDrawing Drawing { get; }

        /// <summary>Gets its number.</summary>
        public string Number => Drawing.Number;

        /// <summary>Gets its name.</summary>
        public string Name => Drawing.Name;

        /// <summary>Gets its sheet's size.</summary>
        public string Sheet => Drawing.SheetSize;

        /// <summary>Gets or sets whether it is written.</summary>
        public bool IsChecked
        {
            get => _isChecked;
            set
            {
                if (Set(ref _isChecked, value))
                {
                    _ticked();
                }
            }
        }
    }

    /// <summary>
    /// The Export dialog, as the design draws it: the model drawings to write, the format (DXF; PDF and Print come later), the
    /// DXF layout (D10: a layout of viewports, or the sheet in model space), the version AC1027 the core writes, the folder and
    /// the file names. Each drawing is one DXF (A19), its document inside it.
    /// </summary>
    /// <remarks>
    /// A drawing laid out the way chosen is saved as it is shown, hand edits and all (<see cref="Drawing.Save"/>); one laid out
    /// the other way is saved aside, opened again and updated with that layout off the UI thread, the core's update keeping its
    /// hand edits and what was drawn on it by hand, and that copy is saved; the drawing shown is not changed. B-101 "Beam B1" written with the names
    /// "&lt;Number&gt; &lt;Name&gt;.dxf" is "B-101 Beam B1.dxf".
    /// </remarks>
    public sealed class ExportViewModel : DialogViewModel
    {
        /// <summary>The DXF version the core writes, as the dialog names it.</summary>
        public const string VersionText = "AutoCAD 2013 (AC1027)";

        private readonly DrawingWorkspace _workspace;
        private bool _sheetInModelSpace;
        private string _folder;
        private string _fileNames = "<Number> <Name>.dxf";
        private string _notice = string.Empty;

        /// <summary>Makes the dialog's view model over the model drawings of a presenter, the one shown ticked.</summary>
        /// <param name="workspace">The model drawing of the presenter.</param>
        /// <exception cref="ArgumentNullException">Thrown when it is null.</exception>
        public ExportViewModel(DrawingWorkspace workspace)
        {
            _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
            ModelDrawing shown = workspace.Current ?? workspace.Drawings.LastOrDefault();
            Rows = workspace.Drawings.Select(d => new ExportRow(d, d == shown, OnTicked)).ToList();
            _sheetInModelSpace = shown != null && shown.Drawing.Settings.ExportMode == DrawingExportMode.SheetInModelSpace;
            _folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "GeometryHelper Drawing");
            ExportCommand = new DelegateCommand(_ => _ = ExportAsync(), _ => CanExport);
            CancelCommand = new DelegateCommand(_ => Close(false));
            LayoutModeCommand = new DelegateCommand(p => SheetInModelSpace = string.Equals(p as string, "Sheet", StringComparison.Ordinal));
        }

        /// <summary>Gets the model drawings, in the order they were made.</summary>
        public IReadOnlyList<ExportRow> Rows { get; }

        /// <summary>Gets how many are ticked, in words: "1 of 2 selected".</summary>
        public string SelectedText => Rows.Count(r => r.IsChecked).ToString(CultureInfo.InvariantCulture) + " of " + Rows.Count.ToString(CultureInfo.InvariantCulture) + " selected";

        /// <summary>Gets the label of the button: "Export 2".</summary>
        public string ExportText => "Export " + Rows.Count(r => r.IsChecked).ToString(CultureInfo.InvariantCulture);

        /// <summary>Gets or sets whether the sheet is written at 1:1 in model space; false, the default, for a layout of viewports.</summary>
        public bool SheetInModelSpace
        {
            get => _sheetInModelSpace;
            set
            {
                if (Set(ref _sheetInModelSpace, value))
                {
                    Raise(nameof(LayoutAndViewports));
                }
            }
        }

        /// <summary>Gets or sets whether each view is a viewport of a layout, as MVIEW makes them; the default.</summary>
        public bool LayoutAndViewports
        {
            get => !_sheetInModelSpace;
            set => SheetInModelSpace = !value;
        }

        /// <summary>Gets the version written, which the core fixes.</summary>
        public string Version => VersionText;

        /// <summary>Gets or sets the folder written to; made where it is missing.</summary>
        public string Folder
        {
            get => _folder;
            set
            {
                if (Set(ref _folder, value ?? string.Empty))
                {
                    RaiseExport();
                }
            }
        }

        /// <summary>Gets or sets how the files are named: &lt;Number&gt;, &lt;Name&gt; and &lt;Revision&gt; stand for the drawing's.</summary>
        public string FileNames
        {
            get => _fileNames;
            set
            {
                if (Set(ref _fileNames, value ?? string.Empty))
                {
                    RaiseExport();
                }
            }
        }

        /// <summary>Gets a line at the foot: what went wrong.</summary>
        public string Notice
        {
            get => _notice;
            private set => Set(ref _notice, value ?? string.Empty);
        }

        /// <summary>Gets the files written by the last export, in the order of the rows.</summary>
        public IReadOnlyList<string> Written { get; private set; } = Array.Empty<string>();

        /// <summary>Gets the command of Export.</summary>
        public ICommand ExportCommand { get; }

        /// <summary>Gets the command of Cancel.</summary>
        public ICommand CancelCommand { get; }

        /// <summary>Gets the command that chooses the DXF layout, given "Layout" or "Sheet".</summary>
        public ICommand LayoutModeCommand { get; }

        private bool CanExport => !IsBusy && Rows.Any(r => r.IsChecked) && _folder.Trim().Length > 0 && _fileNames.Trim().Length > 0;

        /// <summary>The file a drawing is written to: the names with its number, name and revision, characters a file name cannot hold made "_".</summary>
        /// <param name="drawing">The drawing.</param>
        /// <returns>The full path.</returns>
        public string PathOf(ModelDrawing drawing)
        {
            if (drawing == null)
            {
                throw new ArgumentNullException(nameof(drawing));
            }

            string name = _fileNames.Replace("<Number>", drawing.Number).Replace("<Name>", drawing.Name).Replace("<Revision>", drawing.Revision).Trim();

            foreach (char bad in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(bad, '_');
            }

            if (!name.EndsWith(".dxf", StringComparison.OrdinalIgnoreCase))
            {
                name += ".dxf";
            }

            return Path.Combine(_folder.Trim(), name);
        }

        /// <summary>
        /// Writes the drawings ticked, each one DXF, then shows a message of what was written with Open folder; a drawing that
        /// fails is said and the others are written.
        /// </summary>
        /// <returns>The files written.</returns>
        public async Task<IReadOnlyList<string>> ExportAsync()
        {
            if (!CanExport)
            {
                return Array.Empty<string>();
            }

            DrawingExportMode mode = _sheetInModelSpace ? DrawingExportMode.SheetInModelSpace : DrawingExportMode.LayoutAndViewports;
            var written = new List<string>();
            var failed = new List<string>();
            string folder;

            try
            {
                folder = Path.GetFullPath(_folder.Trim());
                Directory.CreateDirectory(folder);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException || ex is NotSupportedException)
            {
                Notice = "The folder cannot be made: " + ex.Message;
                return written;
            }

            foreach (ExportRow row in Rows.Where(r => r.IsChecked).ToList())
            {
                ModelDrawing entry = row.Drawing;
                string path = PathOf(entry);

                try
                {
                    if (entry.Drawing.Settings.ExportMode == mode || entry.Drawing.Source == null)
                    {
                        Drawing drawing = entry.Drawing;
                        await RunAsync("Writing " + Path.GetFileName(path) + "…", () =>
                        {
                            drawing.Save(path);
                            return true;
                        });
                    }
                    else
                    {
                        // A copy laid out the other way: the drawing as it is saved aside, opened again and updated with the
                        // layout asked, its hand edits and what was drawn by hand kept by the core's update; the drawing shown
                        // is not changed.
                        DrawingSettings settings = entry.Drawing.Settings;
                        settings.ExportMode = mode;
                        DrawingSource source = entry.Drawing.Source;
                        Tolerance tolerance = _workspace.Tolerance;
                        string aside = Path.Combine(Path.GetTempPath(), "GeometryHelper", "Export", Guid.NewGuid().ToString("N") + ".dxf");
                        Directory.CreateDirectory(Path.GetDirectoryName(aside));
                        entry.Drawing.Save(aside);

                        try
                        {
                            await RunAsync("Laying " + entry.Number + " out " + (_sheetInModelSpace ? "in model space" : "as a layout of viewports") + "…", () =>
                            {
                                var copy = Drawing.Open(aside, source, tolerance);
                                copy.Update(source, settings);
                                copy.Save(path);
                                return true;
                            });
                        }
                        finally
                        {
                            TryDelete(aside);
                        }
                    }

                    written.Add(path);
                }
                catch (OperationCanceledException)
                {
                    Notice = "Stopped: " + DrawingWorkspace.Count(written.Count, "drawing") + " written.";
                    break;
                }
                catch (Exception ex) when (!(ex is OutOfMemoryException))
                {
                    GeometryHelperLog.Warn("ExportViewModel: " + entry.Number + " was not written to " + path + ".", ex);
                    failed.Add(entry.Number + ": " + ex.Message);
                }
            }

            Written = written.AsReadOnly();
            _workspace.Say("Exported " + DrawingWorkspace.Count(written.Count, "drawing") + " to " + folder + (failed.Count == 0 ? "." : "; " + failed.Count.ToString(CultureInfo.InvariantCulture) + " not written."));

            if (failed.Count > 0)
            {
                _workspace.ShowMessage(new MessageViewModel(MessageKind.Error, "Export not finished", DrawingWorkspace.Count(written.Count, "drawing") + " written to " + folder + "; these were not:", string.Join("\n", failed), "Open folder", () => OpenFolder(folder)));
            }
            else if (written.Count > 0)
            {
                _workspace.ShowMessage(new MessageViewModel(MessageKind.Done, "Export done", "Exported " + DrawingWorkspace.Count(written.Count, "drawing") + " to " + folder + ".", null, "Open folder", () => OpenFolder(folder)));
            }

            if (Notice.Length == 0)
            {
                Close(true);
            }

            return Written;
        }

        /// <inheritdoc/>
        protected override void OnBusyChanged() => RaiseExport();

        private static void TryDelete(string path)
        {
            try
            {
                File.Delete(path);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                GeometryHelperLog.Debug("ExportViewModel: the copy " + path + " was left: " + ex.Message);
            }
        }

        private static void OpenFolder(string folder)
        {
            try
            {
                Process.Start(new ProcessStartInfo("explorer.exe", "\"" + folder + "\"") { UseShellExecute = true });
            }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception || ex is InvalidOperationException)
            {
                GeometryHelperLog.Warn("ExportViewModel: the folder " + folder + " could not be opened.", ex);
            }
        }

        private void OnTicked() => RaiseExport();

        private void RaiseExport()
        {
            Raise(nameof(SelectedText));
            Raise(nameof(ExportText));
            ((DelegateCommand)ExportCommand)?.RaiseCanExecuteChanged();
        }
    }
}
