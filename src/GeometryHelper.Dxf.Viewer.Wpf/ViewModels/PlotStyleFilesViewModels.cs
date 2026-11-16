using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// The Add Plot Style Table dialog, which New… of the Plot dialog and of the Plot Styles folder shows: the table's name
    /// and what to start from, a table of nothing changed or a copy of one the folder has. The kind of table is the drawing's
    /// (.ctb where it plots by colour, .stb where it plots by name); <see cref="TryCreate"/> writes the file.
    /// </summary>
    public sealed class PlotStyleNewViewModel : ObservableObject
    {
        /// <summary>The words of the choice that starts from nothing.</summary>
        public const string FromScratch = "Start from scratch";

        private readonly string _folder;
        private string _name = string.Empty;
        private string _source = FromScratch;
        private string _message = string.Empty;

        /// <summary>Makes the dialog.</summary>
        /// <param name="folder">The folder the table is written to.</param>
        /// <param name="named">True for a named table (.stb), false for a colour-dependent one (.ctb).</param>
        /// <exception cref="ArgumentException">Thrown when the folder is empty.</exception>
        public PlotStyleNewViewModel(string folder, bool named)
        {
            if (string.IsNullOrWhiteSpace(folder))
            {
                throw new ArgumentException("A new plot style table needs a folder.", nameof(folder));
            }

            _folder = folder;
            IsNamed = named;
            Sources = new[] { FromScratch }.Concat(CadPlotStyleFolder.Names(folder, named)).ToList();
        }

        /// <summary>Gets whether the table is a named one (.stb).</summary>
        public bool IsNamed { get; }

        /// <summary>Gets what kind of table is made: "Colour-dependent plot style table (.ctb)" or "Named plot style table (.stb)".</summary>
        public string Kind => IsNamed ? "Named plot style table (.stb)" : "Colour-dependent plot style table (.ctb)";

        /// <summary>Gets the choices of what to start from: <see cref="FromScratch"/>, then the tables of the folder of this kind.</summary>
        public IReadOnlyList<string> Sources { get; }

        /// <summary>Gets or sets the name of the file, without its extension.</summary>
        public string Name
        {
            get => _name;
            set
            {
                if (Set(ref _name, value ?? string.Empty))
                {
                    Message = string.Empty;
                }
            }
        }

        /// <summary>Gets or sets what to start from, one of <see cref="Sources"/>; another is dropped.</summary>
        public string Source
        {
            get => _source;
            set => Set(ref _source, Sources.Contains(value) ? value : _source);
        }

        /// <summary>Gets why the table was not made; empty where it has not been refused.</summary>
        public string Message
        {
            get => _message;
            private set => Set(ref _message, value);
        }

        /// <summary>Gets the name of the file made, with its extension; empty before <see cref="TryCreate"/> succeeds.</summary>
        public string CreatedFileName { get; private set; } = string.Empty;

        /// <summary>
        /// Writes the table: refused, with the reason in <see cref="Message"/>, where the name is not a good file name
        /// (<see cref="CadPlotStyleFolder.CheckFileName"/>), a table of that name is there already, letters of either case the
        /// same, or the file cannot be written.
        /// </summary>
        /// <returns>True when the file is made.</returns>
        public bool TryCreate()
        {
            string problem = CadPlotStyleFolder.CheckFileName(_name);

            if (problem != null)
            {
                Message = problem;
                return false;
            }

            string extension = IsNamed ? CadPlotStyleSheet.NamedExtension : CadPlotStyleSheet.ColorDependentExtension;
            string fileName = _name.Trim() + extension;

            if (
                CadPlotStyleFolder
                    .Names(_folder, IsNamed)
                    .Any(n => string.Equals(n, fileName, StringComparison.OrdinalIgnoreCase))
            )
            {
                Message = "A plot style table named " + fileName + " is there already.";
                return false;
            }

            CadPlotStyleSheet sheet =
                _source == FromScratch
                    ? (IsNamed ? CadPlotStyleSheet.NewNamed() : CadPlotStyleSheet.NewColorDependent())
                    : CadPlotStyleFolder.Load(_folder, _source)?.Clone();

            if (sheet == null)
            {
                Message = "The table " + _source + " could not be read.";
                return false;
            }

            try
            {
                sheet.Save(Path.Combine(_folder, fileName));
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                Message = "The table could not be saved: " + exception.Message;
                return false;
            }

            CreatedFileName = fileName;
            Message = string.Empty;
            return true;
        }
    }

    /// <summary>One file of the Plot Styles folder, a row of the list STYLESMANAGER shows.</summary>
    public sealed class PlotStyleFileRow
    {
        internal PlotStyleFileRow(string name, bool isNamed, string description, DateTime modified)
        {
            Name = name;
            IsNamed = isNamed;
            Description = description;
            Modified = modified;
        }

        /// <summary>Gets the file's name with its extension.</summary>
        public string Name { get; }

        /// <summary>Gets whether the table is a named one (.stb).</summary>
        public bool IsNamed { get; }

        /// <summary>Gets what kind it is: "Colour-dependent" or "Named".</summary>
        public string Kind => IsNamed ? "Named" : "Colour-dependent";

        /// <summary>Gets the table's description; empty for none, or where the file does not read.</summary>
        public string Description { get; }

        /// <summary>Gets when the file was last written, "2026-11-17 09:30".</summary>
        public string ModifiedText => Modified.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

        /// <summary>Gets when the file was last written.</summary>
        public DateTime Modified { get; }

        /// <inheritdoc/>
        public override string ToString() => Name;
    }

    /// <summary>
    /// The Plot Styles folder STYLESMANAGER shows, as AutoCAD shows its folder of plot style table files: every .ctb and .stb
    /// file by name, its kind, its description and when it was written; double-click (or Edit…) opens the Plot Style Table
    /// Editor on it, New… the Add Plot Style Table dialog, Delete removes the file, and Open folder shows the folder in
    /// Windows' Explorer.
    /// </summary>
    public sealed class PlotStylesManagerViewModel : ObservableObject
    {
        private PlotStyleFileRow _selected;
        private string _message = string.Empty;

        /// <summary>Makes the dialog over a folder.</summary>
        /// <param name="folder">The folder of plot style tables.</param>
        /// <param name="newIsNamed">Whether New… makes a named table (the drawing plots by name) rather than a colour-dependent one.</param>
        /// <exception cref="ArgumentException">Thrown when the folder is empty.</exception>
        public PlotStylesManagerViewModel(string folder, bool newIsNamed)
        {
            if (string.IsNullOrWhiteSpace(folder))
            {
                throw new ArgumentException("The Plot Styles folder needs a path.", nameof(folder));
            }

            Folder = folder;
            NewIsNamed = newIsNamed;
            Refresh();
        }

        /// <summary>Gets the folder.</summary>
        public string Folder { get; }

        /// <summary>Gets whether New… makes a named table.</summary>
        public bool NewIsNamed { get; }

        /// <summary>Gets or sets how the window shows the Plot Style Table Editor: true when the table was saved. Null for none, when Edit… does nothing.</summary>
        public Func<PlotStyleEditorViewModel, bool> ShowEditor { get; set; }

        /// <summary>Gets or sets how the window shows the Add Plot Style Table dialog: true when OK was pressed, which has made the file (<see cref="PlotStyleNewViewModel.TryCreate"/>). Null for none, when New… does nothing.</summary>
        public Func<PlotStyleNewViewModel, bool> ShowNew { get; set; }

        /// <summary>Gets the files, .ctb and .stb together, in ordinal order of their names with letters of either case the same.</summary>
        public ObservableCollection<PlotStyleFileRow> Items { get; } = new ObservableCollection<PlotStyleFileRow>();

        /// <summary>Gets or sets the file chosen.</summary>
        public PlotStyleFileRow Selected
        {
            get => _selected;
            set
            {
                if (Set(ref _selected, value))
                {
                    Raise(nameof(CanEdit));
                }
            }
        }

        /// <summary>Gets whether Edit… and Delete can be pressed: a file is chosen.</summary>
        public bool CanEdit => _selected != null;

        /// <summary>Gets what the dialog says of what it could not do; empty where nothing failed.</summary>
        public string Message
        {
            get => _message;
            private set => Set(ref _message, value);
        }

        /// <summary>Gets the words under the list: "9 plot style tables in D:\Plot Styles".</summary>
        public string Summary =>
            Items.Count.ToString(CultureInfo.InvariantCulture)
            + (Items.Count == 1 ? " plot style table in " : " plot style tables in ")
            + Folder;

        /// <summary>Lists the folder again, keeping the file that was chosen where it is still there.</summary>
        public void Refresh()
        {
            string chosen = _selected?.Name;
            var rows = new List<PlotStyleFileRow>();

            foreach (bool named in new[] { false, true })
            {
                foreach (string name in CadPlotStyleFolder.Names(Folder, named))
                {
                    string description = string.Empty;
                    DateTime modified = DateTime.MinValue;

                    try
                    {
                        string path = Path.Combine(Folder, name);
                        modified = File.GetLastWriteTime(path);
                        description = CadPlotStyleSheet.Load(path).Description;
                    }
                    catch (Exception exception)
                        when (exception is IOException
                            || exception is UnauthorizedAccessException
                            || exception is InvalidDataException
                        )
                    {
                        description = "(does not read)";
                    }

                    rows.Add(new PlotStyleFileRow(name, named, description, modified));
                }
            }

            rows.Sort(
                (a, b) =>
                {
                    int byName = string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
                    return byName != 0 ? byName : string.CompareOrdinal(a.Name, b.Name);
                }
            );
            Items.Clear();

            foreach (PlotStyleFileRow row in rows)
            {
                Items.Add(row);
            }

            Selected = Items.FirstOrDefault(r => r.Name == chosen);
            Raise(nameof(Summary));
        }

        /// <summary>Opens the chosen file in the Plot Style Table Editor (<see cref="ShowEditor"/>); the list is read again after.</summary>
        /// <returns>True when the editor was shown on a table that read.</returns>
        public bool Edit() => Edit(_selected?.Name);

        /// <summary>Opens a file of the folder in the Plot Style Table Editor (<see cref="ShowEditor"/>); the list is read again after.</summary>
        /// <param name="name">The file's name with its extension.</param>
        /// <returns>True when the editor was shown on a table that read.</returns>
        public bool Edit(string name)
        {
            if (ShowEditor == null || string.IsNullOrEmpty(name))
            {
                return false;
            }

            CadPlotStyleSheet sheet;

            try
            {
                sheet = CadPlotStyleSheet.Load(Path.Combine(Folder, name));
            }
            catch (Exception exception)
                when (exception is IOException
                    || exception is UnauthorizedAccessException
                    || exception is InvalidDataException
                )
            {
                Message = exception.Message;
                return false;
            }

            Message = string.Empty;
            ShowEditor(new PlotStyleEditorViewModel(sheet, Folder));
            Refresh();
            return true;
        }

        /// <summary>Shows the Add Plot Style Table dialog (<see cref="ShowNew"/>) and, where a table was made, lists it chosen and opens the editor on it.</summary>
        /// <returns>True when a table was made.</returns>
        public bool New()
        {
            if (ShowNew == null)
            {
                return false;
            }

            var dialog = new PlotStyleNewViewModel(Folder, NewIsNamed);

            // The window's OK makes the file (TryCreate) and closes only where that worked.
            if (!ShowNew(dialog) || dialog.CreatedFileName.Length == 0)
            {
                return false;
            }

            Refresh();
            Selected = Items.FirstOrDefault(r => r.Name == dialog.CreatedFileName);
            Edit(dialog.CreatedFileName);
            return true;
        }

        /// <summary>Deletes the chosen file, as Delete of Windows' Explorer does without the Recycle Bin: said in <see cref="Message"/> where it cannot be.</summary>
        /// <returns>True when the file was deleted.</returns>
        public bool Delete()
        {
            if (_selected == null)
            {
                return false;
            }

            try
            {
                File.Delete(Path.Combine(Folder, _selected.Name));
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                Message = "The file could not be deleted: " + exception.Message;
                return false;
            }

            Message = string.Empty;
            Refresh();
            return true;
        }
    }
}
