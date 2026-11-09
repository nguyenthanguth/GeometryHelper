using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Input;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// A linetype of a list: its name, its appearance drawn as a line and its description; for the Linetype Manager, whether it
    /// is current and whether it waits to be loaded.
    /// </summary>
    public sealed class LinetypeRowViewModel : ObservableObject
    {
        private bool _isCurrent;

        internal LinetypeRowViewModel(string name, string description, IReadOnlyList<double> dashes, CadLinetypeDefinition pending, string path)
        {
            Name = name;
            Description = description ?? string.Empty;
            Dashes = dashes ?? Array.Empty<double>();
            Pending = pending;
            PendingPath = path;
            Appearance = Draw(Dashes);
        }

        /// <summary>Gets the name.</summary>
        public string Name { get; }

        /// <summary>Gets the description.</summary>
        public string Description { get; }

        /// <summary>Gets the lengths of the pattern: above nought a dash, below a gap, nought a dot; none for a continuous line.</summary>
        public IReadOnlyList<double> Dashes { get; }

        /// <summary>Gets the appearance as path data 120 wide and 10 high, the pattern drawn three times across: <c>M0,5 L80,5 M100,5 ...</c>.</summary>
        public string Appearance { get; }

        /// <summary>Gets or sets whether the linetype is the one new objects take.</summary>
        public bool IsCurrent
        {
            get => _isCurrent;
            set => Set(ref _isCurrent, value);
        }

        /// <summary>Gets whether the linetype waits to be loaded when the dialog's OK is pressed.</summary>
        public bool IsPending => Pending != null;

        internal CadLinetypeDefinition Pending { get; }

        internal string PendingPath { get; }

        /// <inheritdoc/>
        public override string ToString() => Name;

        /// <summary>The path data of a pattern drawn three times across 120, a dot a stroke of one.</summary>
        internal static string Draw(IReadOnlyList<double> dashes)
        {
            const double width = 120.0;
            double period = dashes.Sum(d => Math.Abs(d));

            if (dashes.Count == 0 || period <= 0.0)
            {
                return "M0,5 L120,5";
            }

            double factor = width / (3.0 * period);
            var data = new StringBuilder();
            double x = 0.0;

            for (int i = 0; x < width && i < 400; i++)
            {
                double dash = dashes[i % dashes.Count];
                double length = Math.Abs(dash) * factor;

                if (dash >= 0.0)
                {
                    double end = Math.Min(width, x + Math.Max(length, 1.0));
                    data.Append('M').Append(Number(x)).Append(",5 L").Append(Number(end)).Append(",5 ");
                }

                x += length;
            }

            return data.Length == 0 ? "M0,5 L0,5" : data.ToString().TrimEnd();
        }

        private static string Number(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// AutoCAD's Linetype Manager (LINETYPE): the linetypes of the drawing with their appearance and description, the current one
    /// named above them; Load... adds those of a .lin file, Current makes the one chosen current, Delete takes one nothing uses
    /// out, Show details shows the global scale factor ($LTSCALE), the current object scale ($CELTSCALE) and the ISO pen width.
    /// Nothing is written until <see cref="Apply"/>, which OK calls: then every change is one undo step, LINETYPE.
    /// </summary>
    public sealed class LinetypeManagerViewModel : ObservableObject
    {
        /// <summary>The ISO pen widths offered, in millimetres: an ISO linetype is drawn for a pen of 1 mm, so the width is its scale.</summary>
        public static readonly IReadOnlyList<string> IsoPenWidths = Array.AsReadOnly(new[] { "0.13 mm", "0.18 mm", "0.25 mm", "0.35 mm", "0.5 mm", "0.7 mm", "1.0 mm", "1.4 mm", "2.0 mm" });

        private readonly List<string> _deleted = new List<string>();
        private LinetypeRowViewModel _selected;
        private string _current;
        private string _globalScale;
        private string _objectScale;
        private string _isoPenWidth;
        private bool _showDetails;
        private string _message = string.Empty;

        /// <summary>Makes the manager of a document's linetypes.</summary>
        /// <param name="document">The document.</param>
        /// <exception cref="ArgumentNullException">Thrown when the document is null.</exception>
        public LinetypeManagerViewModel(CadDocument document)
        {
            Document = document ?? throw new ArgumentNullException(nameof(document));
            DxfDatabase database = document.Database;
            _current = database.Celtype;
            _globalScale = Text(database.Ltscale);
            _objectScale = Text(database.Celtscale);
            Rows = new ObservableCollection<LinetypeRowViewModel>();

            foreach (string name in new[] { "ByLayer", "ByBlock" }.Concat(CadLinetypes.Names(database)))
            {
                DxfLinetypeTableRecord record = database.LinetypeTable.FirstOrDefault(l => string.Equals(l.Name, name, StringComparison.OrdinalIgnoreCase));
                Rows.Add(new LinetypeRowViewModel(name, record?.AsciiDescription, CadLinetypes.DashesOf(database, name), null, null));
            }

            MarkCurrent();
            LoadCommand = new DelegateCommand(_ => Load());
            CurrentCommand = new DelegateCommand(_ => MakeCurrent());
            DeleteCommand = new DelegateCommand(_ => Delete());
            DetailsCommand = new DelegateCommand(_ => ShowDetails = !ShowDetails);
        }

        /// <summary>Gets the document.</summary>
        public CadDocument Document { get; }

        /// <summary>Gets the linetypes: ByLayer, ByBlock, then the drawing's in the order of their names, then those waiting to be loaded.</summary>
        public ObservableCollection<LinetypeRowViewModel> Rows { get; }

        /// <summary>Gets or sets the linetype chosen in the list.</summary>
        public LinetypeRowViewModel SelectedRow
        {
            get => _selected;
            set => Set(ref _selected, value);
        }

        /// <summary>Gets the linetype new objects take once OK is pressed.</summary>
        public string CurrentLinetype => _current;

        /// <summary>Gets the line above the list: "Current Linetype: ByLayer".</summary>
        public string CurrentText => "Current Linetype: " + _current;

        /// <summary>Gets or sets the global scale factor ($LTSCALE), as typed; a number above nought.</summary>
        public string GlobalScale
        {
            get => _globalScale;
            set => SetScale(ref _globalScale, value, "Global scale factor");
        }

        /// <summary>Gets or sets the current object scale ($CELTSCALE), as typed; a number above nought.</summary>
        public string CurrentObjectScale
        {
            get => _objectScale;
            set
            {
                SetScale(ref _objectScale, value, "Current object scale");
                Set(ref _isoPenWidth, null, nameof(IsoPenWidth));
            }
        }

        /// <summary>Gets or sets the ISO pen width: chosen, it sets the current object scale to the width in millimetres.</summary>
        public string IsoPenWidth
        {
            get => _isoPenWidth;
            set
            {
                if (value == null || !Set(ref _isoPenWidth, value))
                {
                    return;
                }

                _objectScale = Text(double.Parse(value.Replace("mm", string.Empty).Trim(), CultureInfo.InvariantCulture));
                Raise(nameof(CurrentObjectScale));
            }
        }

        /// <summary>Gets or sets whether the details are shown.</summary>
        public bool ShowDetails
        {
            get => _showDetails;
            set
            {
                if (Set(ref _showDetails, value))
                {
                    Raise(nameof(DetailsText));
                }
            }
        }

        /// <summary>Gets the words of the details button: "Show details" or "Hide details".</summary>
        public string DetailsText => _showDetails ? "Hide details" : "Show details";

        /// <summary>Gets why the last action was refused; empty where it was not.</summary>
        public string Message
        {
            get => _message;
            private set => Set(ref _message, value ?? string.Empty);
        }

        /// <summary>Gets the command of Load...: the Load or Reload Linetypes dialog, through <see cref="PickLinetypes"/>.</summary>
        public ICommand LoadCommand { get; }

        /// <summary>Gets the command of Current.</summary>
        public ICommand CurrentCommand { get; }

        /// <summary>Gets the command of Delete.</summary>
        public ICommand DeleteCommand { get; }

        /// <summary>Gets the command of Show details.</summary>
        public ICommand DetailsCommand { get; }

        /// <summary>Gets or sets how the window shows the Load or Reload Linetypes dialog: true when OK was pressed.</summary>
        public Func<LoadLinetypesViewModel, bool> PickLinetypes { get; set; }

        /// <summary>Shows the Load or Reload Linetypes dialog and adds the linetypes chosen.</summary>
        public void Load()
        {
            var dialog = new LoadLinetypesViewModel(Document);

            if (PickLinetypes != null && PickLinetypes(dialog))
            {
                Add(dialog.Chosen, dialog.FilePath);
            }
        }

        /// <summary>
        /// Adds linetypes to load when OK is pressed, as Load... adds those chosen: each the drawing has, or the list has
        /// already, passed over and said.
        /// </summary>
        /// <param name="linetypes">The linetypes.</param>
        /// <param name="path">The file they are of: a full path, or <see cref="CadLinetypeFile.BuiltInName"/>.</param>
        /// <exception cref="ArgumentNullException">Thrown when the linetypes or the path are null.</exception>
        public void Add(IEnumerable<CadLinetypeDefinition> linetypes, string path)
        {
            if (linetypes == null)
            {
                throw new ArgumentNullException(nameof(linetypes));
            }

            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            var present = new List<string>();

            foreach (CadLinetypeDefinition linetype in linetypes.Where(l => l != null && l.Fault == null))
            {
                if (Rows.Any(r => string.Equals(r.Name, linetype.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    present.Add(linetype.Name);
                    continue;
                }

                _deleted.RemoveAll(d => string.Equals(d, linetype.Name, StringComparison.OrdinalIgnoreCase));
                Rows.Add(new LinetypeRowViewModel(linetype.Name, linetype.Description, linetype.Elements.Select(e => e.Length).ToList(), linetype, path));
            }

            Message = present.Count == 0 ? string.Empty : "Already loaded: " + string.Join(", ", present) + ".";
        }

        /// <summary>Makes the linetype chosen current, once OK is pressed.</summary>
        public void MakeCurrent()
        {
            if (SelectedRow == null)
            {
                return;
            }

            _current = SelectedRow.Name;
            MarkCurrent();
            Raise(nameof(CurrentLinetype));
            Raise(nameof(CurrentText));
        }

        /// <summary>
        /// Takes the linetype chosen out, once OK is pressed: refused, the reason in <see cref="Message"/>, for ByLayer, ByBlock and
        /// Continuous, the current linetype, and one an entity, a layer, a style or the header draws with.
        /// </summary>
        /// <returns>Whether it was taken out.</returns>
        public bool Delete()
        {
            LinetypeRowViewModel row = SelectedRow;

            if (row == null)
            {
                return false;
            }

            if (CadLinetypes.IsKept(row.Name))
            {
                Message = "The linetype " + row.Name + " cannot be deleted: AutoCAD keeps ByLayer, ByBlock and Continuous.";
                return false;
            }

            if (string.Equals(row.Name, _current, StringComparison.OrdinalIgnoreCase))
            {
                Message = "The linetype " + row.Name + " is current and cannot be deleted.";
                return false;
            }

            if (!row.IsPending && CadLinetypes.IsInUse(Document.Database, row.Name))
            {
                Message = "The linetype " + row.Name + " is in use and cannot be deleted.";
                return false;
            }

            if (!row.IsPending)
            {
                _deleted.Add(row.Name);
            }

            Rows.Remove(row);
            SelectedRow = null;
            Message = string.Empty;
            return true;
        }

        /// <summary>
        /// Writes what the dialog changed as one undo step, LINETYPE: the linetypes loaded, those of a file through the library's
        /// loader and those of the presenter's own set from their definitions, those deleted, the current linetype and the two
        /// scales. A linetype the drawing cannot take is left out and said in <see cref="Message"/>.
        /// </summary>
        /// <returns>The edit; null where nothing was changed.</returns>
        public CadEditResult Apply()
        {
            DxfDatabase database = Document.Database;
            double global = Parse(_globalScale, database.Ltscale);
            double objects = Parse(_objectScale, database.Celtscale);
            string current = _current;
            List<LinetypeRowViewModel> loads = Rows.Where(r => r.IsPending).ToList();
            var planned = new List<CadEditRecord>();

            if (!string.Equals(current, database.Celtype, StringComparison.Ordinal))
            {
                planned.Add(new CadEditRecord(CadEditRecordKind.SettingChanged, null, "$CELTYPE", database.Celtype, current));
            }

            if (global != database.Ltscale)
            {
                planned.Add(new CadEditRecord(CadEditRecordKind.SettingChanged, null, "$LTSCALE", Text(database.Ltscale), Text(global)));
            }

            if (objects != database.Celtscale)
            {
                planned.Add(new CadEditRecord(CadEditRecordKind.SettingChanged, null, "$CELTSCALE", Text(database.Celtscale), Text(objects)));
            }

            if (planned.Count == 0 && loads.Count == 0 && _deleted.Count == 0)
            {
                return null;
            }

            var refused = new List<string>();
            CadEditResult result = Document.ApplyEdit("LINETYPE", planned, s =>
            {
                foreach (LinetypeRowViewModel row in loads)
                {
                    try
                    {
                        if (string.Equals(row.PendingPath, CadLinetypeFile.BuiltInName, StringComparison.OrdinalIgnoreCase))
                        {
                            CadLinetypes.Add(s.Database, row.Pending);
                        }
                        else
                        {
                            s.Database.LoadLineTypeFile(row.Name, row.PendingPath);
                        }

                        s.Record(new CadEditRecord(CadEditRecordKind.TableChanged, null, "Load linetype", null, row.Name));
                    }
                    catch (ArgumentException e)
                    {
                        refused.Add(e.Message);
                    }
                }

                foreach (string name in _deleted)
                {
                    s.Database.LinetypeTable[name].Erase();
                    s.Record(new CadEditRecord(CadEditRecordKind.TableChanged, null, "Delete linetype", name, null));
                }

                if (!string.Equals(current, s.Database.Celtype, StringComparison.Ordinal) && (CadLinetypes.IsKept(current) || s.Database.LinetypeTable.Has(current)))
                {
                    s.Database.Celtype = current;
                }

                s.Database.Ltscale = global;
                s.Database.Celtscale = objects;
            });

            Message = !result.Succeeded ? result.Message : string.Join(" ", refused);
            return result;
        }

        private void MarkCurrent()
        {
            foreach (LinetypeRowViewModel row in Rows)
            {
                row.IsCurrent = string.Equals(row.Name, _current, StringComparison.OrdinalIgnoreCase);
            }
        }

        private void SetScale(ref string field, string value, string what)
        {
            string typed = (value ?? string.Empty).Trim();

            if (!double.TryParse(typed, NumberStyles.Float, CultureInfo.InvariantCulture, out double scale) || !(scale > 0.0) || double.IsInfinity(scale))
            {
                Message = what + " is a number above nought.";
                Raise(what == "Global scale factor" ? nameof(GlobalScale) : nameof(CurrentObjectScale));
                return;
            }

            Message = string.Empty;
            Set(ref field, typed, what == "Global scale factor" ? nameof(GlobalScale) : nameof(CurrentObjectScale));
        }

        private static double Parse(string text, double fallback)
            => double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) && value > 0.0 && !double.IsInfinity(value) ? value : fallback;

        private static string Text(double value) => value.ToString("0.####", CultureInfo.InvariantCulture);
    }

    /// <summary>A linetype of a .lin file offered by the Load or Reload Linetypes dialog, with whether it is chosen.</summary>
    public sealed class LoadLinetypeRowViewModel : ObservableObject
    {
        private bool _isChosen;

        internal LoadLinetypeRowViewModel(CadLinetypeDefinition definition)
        {
            Definition = definition;
        }

        /// <summary>Gets the linetype as the file gives it.</summary>
        public CadLinetypeDefinition Definition { get; }

        /// <summary>Gets the name.</summary>
        public string Name => Definition.Name;

        /// <summary>Gets the description, or why the linetype cannot be read.</summary>
        public string Description => Definition.Fault ?? Definition.Description;

        /// <summary>Gets whether the linetype can be loaded: its pattern was read.</summary>
        public bool CanLoad => Definition.Fault == null;

        /// <summary>Gets or sets whether the linetype is chosen to be loaded.</summary>
        public bool IsChosen
        {
            get => _isChosen;
            set => Set(ref _isChosen, value && CanLoad);
        }
    }

    /// <summary>
    /// AutoCAD's Load or Reload Linetypes dialog: the file (the presenter's own set, GeometryHelper.lin, unless the user
    /// chooses one of their support folders or another by File...) and its linetypes, several chosen at once.
    /// </summary>
    public sealed class LoadLinetypesViewModel : ObservableObject
    {
        private string _file;
        private string _message = string.Empty;

        /// <summary>Makes the dialog for a document, on the file it loads from unless told otherwise.</summary>
        /// <param name="document">The document.</param>
        /// <exception cref="ArgumentNullException">Thrown when the document is null.</exception>
        public LoadLinetypesViewModel(CadDocument document)
        {
            Document = document ?? throw new ArgumentNullException(nameof(document));
            Files = new ObservableCollection<string> { CadLinetypeFile.BuiltInName };

            foreach (string path in CadSupportFiles.List(document, ".lin"))
            {
                Files.Add(path);
            }

            Rows = new ObservableCollection<LoadLinetypeRowViewModel>();
            File = CadSupportFiles.DefaultLinetypeFile(document);
            SelectAllCommand = new DelegateCommand(_ =>
            {
                foreach (LoadLinetypeRowViewModel row in Rows)
                {
                    row.IsChosen = true;
                }
            });
        }

        /// <summary>Gets the document.</summary>
        public CadDocument Document { get; }

        /// <summary>Gets the files offered: the presenter's own set, then the .lin files of the drawing's folder and the support folders, then any chosen by File...</summary>
        public ObservableCollection<string> Files { get; }

        /// <summary>Gets or sets the file: one of <see cref="Files"/>, or a path chosen by File..., added to them; its linetypes are read at once.</summary>
        public string File
        {
            get => _file;
            set
            {
                if (value != null && !Files.Contains(value, StringComparer.OrdinalIgnoreCase))
                {
                    Files.Add(value);
                }

                value = Files.FirstOrDefault(f => string.Equals(f, value, StringComparison.OrdinalIgnoreCase)) ?? value;

                if (Set(ref _file, value))
                {
                    Read();
                }
            }
        }

        /// <summary>Gets the file the linetypes are read from: a full path, or <see cref="CadLinetypeFile.BuiltInName"/>.</summary>
        public string FilePath { get; private set; }

        /// <summary>Gets the linetypes of the file.</summary>
        public ObservableCollection<LoadLinetypeRowViewModel> Rows { get; }

        /// <summary>Gets the linetypes chosen.</summary>
        public IReadOnlyList<CadLinetypeDefinition> Chosen => Rows.Where(r => r.IsChosen).Select(r => r.Definition).ToList();

        /// <summary>Gets why the file cannot be read; empty where it was.</summary>
        public string Message
        {
            get => _message;
            private set => Set(ref _message, value ?? string.Empty);
        }

        /// <summary>Gets the command that chooses every linetype the file has.</summary>
        public ICommand SelectAllCommand { get; }

        private void Read()
        {
            Rows.Clear();
            IReadOnlyList<CadLinetypeDefinition> linetypes = null;
            string name = (_file ?? string.Empty).Trim();

            if (string.Equals(name, CadLinetypeFile.BuiltInName, StringComparison.OrdinalIgnoreCase))
            {
                FilePath = CadLinetypeFile.BuiltInName;
                linetypes = CadLinetypeFile.BuiltIn;
            }
            else
            {
                FilePath = CadSupportFiles.Find(Document, name);

                try
                {
                    linetypes = FilePath == null ? null : CadLinetypeFile.Read(FilePath);
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException || e is NotSupportedException)
                {
                    linetypes = null;
                }
            }

            Message = linetypes == null ? "Cannot read linetype file \"" + name + "\"." : string.Empty;

            foreach (CadLinetypeDefinition linetype in linetypes ?? Array.Empty<CadLinetypeDefinition>())
            {
                Rows.Add(new LoadLinetypeRowViewModel(linetype));
            }
        }
    }

    /// <summary>
    /// AutoCAD's Select Linetype dialog, as the Layer manager's linetype cell shows it: the linetypes loaded, the one chosen, and
    /// Load... to load more into the drawing at once, one undo step.
    /// </summary>
    public sealed class SelectLinetypeViewModel : ObservableObject
    {
        private LinetypeRowViewModel _selected;
        private string _message = string.Empty;

        /// <summary>Makes the dialog for a document, on a linetype.</summary>
        /// <param name="document">The document.</param>
        /// <param name="current">The linetype chosen at first.</param>
        /// <exception cref="ArgumentNullException">Thrown when the document is null.</exception>
        public SelectLinetypeViewModel(CadDocument document, string current)
        {
            Document = document ?? throw new ArgumentNullException(nameof(document));
            Rows = new ObservableCollection<LinetypeRowViewModel>();
            Refill(current);
            LoadCommand = new DelegateCommand(_ => Load());
        }

        /// <summary>Gets the document.</summary>
        public CadDocument Document { get; }

        /// <summary>Gets the linetypes loaded, in the order of their names.</summary>
        public ObservableCollection<LinetypeRowViewModel> Rows { get; }

        /// <summary>Gets or sets the linetype chosen.</summary>
        public LinetypeRowViewModel SelectedRow
        {
            get => _selected;
            set => Set(ref _selected, value);
        }

        /// <summary>Gets the name of the linetype chosen; null for none.</summary>
        public string Linetype => SelectedRow?.Name;

        /// <summary>Gets what the last load said; empty where nothing.</summary>
        public string Message
        {
            get => _message;
            private set => Set(ref _message, value ?? string.Empty);
        }

        /// <summary>Gets the command of Load...</summary>
        public ICommand LoadCommand { get; }

        /// <summary>Gets or sets how the window shows the Load or Reload Linetypes dialog: true when OK was pressed.</summary>
        public Func<LoadLinetypesViewModel, bool> PickLinetypes { get; set; }

        /// <summary>Shows the Load or Reload Linetypes dialog and loads the linetypes chosen into the drawing, one undo step.</summary>
        public void Load()
        {
            var dialog = new LoadLinetypesViewModel(Document);

            if (PickLinetypes == null || !PickLinetypes(dialog))
            {
                return;
            }

            CadLinetypeLoad load = CadLinetypes.Load(Document, dialog.Chosen, dialog.FilePath);
            Message = string.Join(" ", load.Refused.Concat(load.Result.Succeeded ? Enumerable.Empty<string>() : new[] { load.Result.Message }));
            Refill(load.Loaded.FirstOrDefault() ?? Linetype);
        }

        private void Refill(string chosen)
        {
            Rows.Clear();

            foreach (string name in CadLinetypes.Names(Document.Database))
            {
                DxfLinetypeTableRecord record = Document.Database.LinetypeTable[name];
                Rows.Add(new LinetypeRowViewModel(name, record.AsciiDescription, CadLinetypes.DashesOf(Document.Database, name), null, null));
            }

            SelectedRow = Rows.FirstOrDefault(r => string.Equals(r.Name, chosen, StringComparison.OrdinalIgnoreCase)) ?? Rows.FirstOrDefault();
        }
    }
}
