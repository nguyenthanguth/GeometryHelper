using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using GeometryHelper.Dxf;

namespace GeometryHelper.Cad.Wpf
{
    /// <summary>A row of the Data Link Manager: a link's name, what it links, how many tables it fills and when it was last updated.</summary>
    public sealed class DataLinkRowViewModel
    {
        internal DataLinkRowViewModel(CadDataLinkInfo info)
        {
            Info = info;
            Name = info.Name;
            Kind = info.IsExtraction ? "Data extraction" : "Excel";
            Details = info.Details.Replace("\n", " - ");
            Tables = info.Tables == 0 ? "not used" : info.Tables.ToString(CultureInfo.InvariantCulture) + " table(s)";
            Updated = info.LastUpdate.HasValue
                ? info.LastUpdate.Value.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)
                : "never";
        }

        internal CadDataLinkInfo Info { get; }

        /// <summary>Gets the link's name.</summary>
        public string Name { get; }

        /// <summary>Gets the kind of link: Excel for a spreadsheet, Data extraction.</summary>
        public string Kind { get; }

        /// <summary>Gets the file and what of it the link takes, on one line.</summary>
        public string Details { get; }

        /// <summary>Gets how many tables the link fills.</summary>
        public string Tables { get; }

        /// <summary>Gets when the link was last updated.</summary>
        public string Updated { get; }
    }

    /// <summary>
    /// The Data Link Manager of DATALINK, as AutoCAD 2021's: the drawing's data links (Excel links and data extraction links)
    /// with what each links and how many tables it fills, Create a new Excel Data Link, Edit, Rename, Delete (refused for a link
    /// in use, "The data link is being used and cannot be deleted."), and Update. Opened to pick a link (Select a Data Link) it
    /// gives the one chosen. Each change is one undo step of the drawing.
    /// </summary>
    public sealed class DataLinkManagerViewModel : ObservableObject
    {
        private readonly CadDocument _document;
        private DataLinkRowViewModel _selected;
        private string _message = string.Empty;

        /// <summary>Makes the manager over a document.</summary>
        /// <param name="document">The document.</param>
        /// <param name="picking">Whether the manager is opened to pick a link, as Link Cell and the table's Data Link menu do.</param>
        public DataLinkManagerViewModel(CadDocument document, bool picking = false)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
            IsPicking = picking;
            CreateCommand = new DelegateCommand(_ => Create(), _ => CadDataLinks.CanHold(_document));
            EditCommand = new DelegateCommand(_ => Edit(), _ => _selected != null && !_selected.Info.IsExtraction);
            RenameCommand = new DelegateCommand(_ => Rename(), _ => _selected != null);
            DeleteCommand = new DelegateCommand(_ => Delete(), _ => _selected != null);
            UpdateCommand = new DelegateCommand(_ => Update(), _ => _selected != null);
            OkCommand = new DelegateCommand(_ => Choose(), _ => _selected != null);
            CancelCommand = new DelegateCommand(_ => CloseRequested?.Invoke(this, false));
            Refresh(null);
        }

        /// <summary>Raised when the window is to close: true for OK of a manager opened to pick a link.</summary>
        public event EventHandler<bool> CloseRequested;

        /// <summary>Gets or sets what shows the Excel data link dialog, true where OK was pressed. Set by the window; a test sets its own.</summary>
        public Func<ExcelDataLinkViewModel, bool> ShowExcelLink { get; set; }

        /// <summary>Gets or sets what asks "Are you sure you want to delete ...?"; true to go on. Set by the window; null goes on.</summary>
        public Func<string, bool> Confirm { get; set; }

        /// <summary>Gets or sets what asks the new name of a link, given its present one; null for none asked. Set by the window.</summary>
        public Func<string, string> AskName { get; set; }

        /// <summary>Gets whether the manager is opened to pick a link.</summary>
        public bool IsPicking { get; }

        /// <summary>Gets the dialog's title: Data Link Manager, or Select a Data Link.</summary>
        public string Title => IsPicking ? "Select a Data Link" : "Data Link Manager";

        /// <summary>Gets what the button that closes the dialog says: Cancel where a link is picked, Close for the manager.</summary>
        public string CloseText => IsPicking ? "Cancel" : "Close";

        /// <summary>Gets whether the drawing can hold data links: a drawing of AutoCAD 2007 or later.</summary>
        public bool CanCreate => CadDataLinks.CanHold(_document);

        /// <summary>Gets the links of the drawing, by name.</summary>
        public ObservableCollection<DataLinkRowViewModel> Links { get; } =
            new ObservableCollection<DataLinkRowViewModel>();

        /// <summary>Gets or sets the link chosen in the list.</summary>
        public DataLinkRowViewModel Selected
        {
            get => _selected;
            set
            {
                if (Set(ref _selected, value))
                {
                    Raise(nameof(Details));
                    Raise(nameof(HasSelected));
                    EditCommand.RaiseCanExecuteChanged();
                    RenameCommand.RaiseCanExecuteChanged();
                    DeleteCommand.RaiseCanExecuteChanged();
                    UpdateCommand.RaiseCanExecuteChanged();
                    OkCommand.RaiseCanExecuteChanged();
                }
            }
        }

        /// <summary>Gets whether a link is chosen.</summary>
        public bool HasSelected => _selected != null;

        /// <summary>Gets what the link chosen links, over several lines: its file and what of it, its tooltip.</summary>
        public string Details => _selected == null ? string.Empty : _selected.Info.ToolTip.Replace("\r\n", "\n");

        /// <summary>Gets the name of the link picked, once OK is pressed in a manager opened to pick one; null before.</summary>
        public string ChosenName { get; private set; }

        /// <summary>Gets what the last action said, shown red under the list; empty where it went well.</summary>
        public string Message
        {
            get => _message;
            private set => Set(ref _message, value ?? string.Empty);
        }

        /// <summary>Gets Create a new Excel Data Link.</summary>
        public DelegateCommand CreateCommand { get; }

        /// <summary>Gets Edit, Modify Excel Link, of the link chosen.</summary>
        public DelegateCommand EditCommand { get; }

        /// <summary>Gets Rename.</summary>
        public DelegateCommand RenameCommand { get; }

        /// <summary>Gets Delete.</summary>
        public DelegateCommand DeleteCommand { get; }

        /// <summary>Gets Update: the link chosen read again from its source and its tables made to hold it.</summary>
        public DelegateCommand UpdateCommand { get; }

        /// <summary>Gets OK, of a manager opened to pick a link.</summary>
        public DelegateCommand OkCommand { get; }

        /// <summary>Gets Cancel and the close button.</summary>
        public DelegateCommand CancelCommand { get; }

        /// <summary>Reads the links of the drawing again, the one of a name chosen where it is still there.</summary>
        /// <param name="choose">The name to choose; null for the one chosen now.</param>
        public void Refresh(string choose)
        {
            string keep = choose ?? _selected?.Name;
            Links.Clear();

            foreach (CadDataLinkInfo info in CadDataLinks.List(_document))
            {
                Links.Add(new DataLinkRowViewModel(info));
            }

            Selected = Links.FirstOrDefault(l => string.Equals(l.Name, keep, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>Create a new Excel Data Link: the details dialog over new settings, the link made when it is accepted.</summary>
        public void Create()
        {
            var dialog = new ExcelDataLinkViewModel(_document, null);

            if (ShowExcelLink != null && ShowExcelLink(dialog))
            {
                Message = string.Empty;
                Refresh(dialog.Name.Trim());
            }
        }

        /// <summary>Edit: the details dialog over the settings of the link chosen.</summary>
        public void Edit()
        {
            if (_selected == null || _selected.Info.IsExtraction)
            {
                return;
            }

            var dialog = new ExcelDataLinkViewModel(_document, _selected.Name);

            if (ShowExcelLink != null && ShowExcelLink(dialog))
            {
                Message = string.Empty;
                Refresh(dialog.Name.Trim());
            }
        }

        /// <summary>Rename: the new name asked and given to the link chosen; "Data link name in use." where another has it.</summary>
        public void Rename()
        {
            if (_selected == null)
            {
                return;
            }

            string name = AskName?.Invoke(_selected.Name);

            if (name == null)
            {
                return;
            }

            CadEditResult result = CadDataLinks.Rename(_document, _selected.Name, name);
            Message = result.Succeeded ? string.Empty : result.Message;
            Refresh(result.Succeeded ? name.Trim() : null);
        }

        /// <summary>Delete: asked, then the link chosen deleted; refused while a table uses it.</summary>
        public void Delete()
        {
            if (_selected == null)
            {
                return;
            }

            if (Confirm != null && !Confirm("Are you sure you want to delete \"" + _selected.Name + "\"?"))
            {
                return;
            }

            CadEditResult result = CadDataLinks.Delete(_document, _selected.Name);
            Message = result.Succeeded ? string.Empty : result.Message;
            Refresh(null);
        }

        /// <summary>Update: the link chosen updated, what it said written under the list.</summary>
        public void Update()
        {
            if (_selected == null)
            {
                return;
            }

            DxfDataLink link = _selected.Info.Link;
            CadDataLinkRun run = CadDataLinks.Update(_document, new[] { link });
            Message =
                run.Failed > 0 ? string.Join(" ", run.Messages)
                : !run.Edit.Succeeded ? run.Edit.Message
                : string.Empty;
            Refresh(null);
        }

        /// <summary>OK of a manager that picks: the link chosen remembered by name, the window closed.</summary>
        public void Choose()
        {
            if (_selected == null)
            {
                return;
            }

            ChosenName = _selected.Name;
            CloseRequested?.Invoke(this, true);
        }
    }

    /// <summary>
    /// The New Excel Data Link and Modify Excel Link dialog: the link's name, the file (an .xlsx workbook or a .csv file) with
    /// how its path is kept (full, relative to the drawing, or its name alone), which cells the link takes (the entire sheet, a
    /// named range, a range such as A1:D10), a preview of them, and the options of More Options, "Use Excel formatting" and
    /// "Allow writing to the source file". OK makes or changes the link as one undo step.
    /// </summary>
    public sealed class ExcelDataLinkViewModel : ObservableObject
    {
        private readonly CadDocument _document;
        private readonly string _editing;
        private readonly CadDataLinkSettings _settings;
        private string _message = string.Empty;
        private string _preview = string.Empty;

        /// <summary>Makes the dialog.</summary>
        /// <param name="document">The document the link is made in.</param>
        /// <param name="editing">The name of the link changed; null for a new link.</param>
        public ExcelDataLinkViewModel(CadDocument document, string editing)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
            _editing = editing;
            _settings = new CadDataLinkSettings();

            if (editing != null)
            {
                DxfDataLink link = CadDataLinks.Find(document, editing);

                if (link != null)
                {
                    ReadFrom(link);
                }
            }
            else
            {
                _settings.Name = NewName();
            }

            OkCommand = new DelegateCommand(_ => TryAccept());
            CancelCommand = new DelegateCommand(_ => CloseRequested?.Invoke(this, false));
            LoadFile();
        }

        /// <summary>Raised when the dialog is to close: true for OK.</summary>
        public event EventHandler<bool> CloseRequested;

        /// <summary>Gets or sets what asks for a file with the Browse button; null where none is chosen. Set by the window.</summary>
        public Func<string, string> BrowseFile { get; set; }

        /// <summary>Gets the dialog's title: New Excel Data Link, or Modify Excel Link, with the link's name.</summary>
        public string Title => (_editing == null ? "New Excel Data Link: " : "Modify Excel Link: ") + _settings.Name;

        /// <summary>Gets or sets the link's name.</summary>
        public string Name
        {
            get => _settings.Name;
            set
            {
                if (_settings.Name != (value ?? string.Empty))
                {
                    _settings.Name = value ?? string.Empty;
                    Raise(nameof(Name));
                    Raise(nameof(Title));
                }
            }
        }

        /// <summary>Gets or sets the file, as a full path.</summary>
        public string File
        {
            get => _settings.File;
            set
            {
                if (_settings.File != (value ?? string.Empty))
                {
                    _settings.File = value ?? string.Empty;
                    Raise(nameof(File));
                    LoadFile();
                }
            }
        }

        /// <summary>Gets or sets whether the connection string keeps the file's full path.</summary>
        public bool IsFullPath
        {
            get => _settings.PathType == CadDataLinkPathType.FullPath;
            set => PathChosen(value, CadDataLinkPathType.FullPath);
        }

        /// <summary>Gets or sets whether the connection string keeps the path from the drawing's folder; only for a drawing saved.</summary>
        public bool IsRelativePath
        {
            get => _settings.PathType == CadDataLinkPathType.RelativePath;
            set => PathChosen(value, CadDataLinkPathType.RelativePath);
        }

        /// <summary>Gets or sets whether the connection string keeps the file's name alone, looked for in the drawing's folder.</summary>
        public bool IsNoPath
        {
            get => _settings.PathType == CadDataLinkPathType.NoPath;
            set => PathChosen(value, CadDataLinkPathType.NoPath);
        }

        /// <summary>Gets whether a path relative to the drawing can be kept: the drawing is saved.</summary>
        public bool CanBeRelative => CadDataLinks.FolderOf(_document) != null;

        /// <summary>Gets or sets whether the link takes the entire sheet.</summary>
        public bool IsEntireSheet
        {
            get => _settings.RangeKind == CadDataLinkRangeKind.EntireSheet;
            set => KindChosen(value, CadDataLinkRangeKind.EntireSheet);
        }

        /// <summary>Gets or sets whether the link takes a named range.</summary>
        public bool IsNamedRange
        {
            get => _settings.RangeKind == CadDataLinkRangeKind.NamedRange;
            set => KindChosen(value, CadDataLinkRangeKind.NamedRange);
        }

        /// <summary>Gets or sets whether the link takes a range of a sheet.</summary>
        public bool IsCellRange
        {
            get => _settings.RangeKind == CadDataLinkRangeKind.CellRange;
            set => KindChosen(value, CadDataLinkRangeKind.CellRange);
        }

        /// <summary>Gets whether the sheet is chosen in a box: the entire sheet and the range need one.</summary>
        public bool NeedsSheet => _settings.RangeKind != CadDataLinkRangeKind.NamedRange;

        /// <summary>Gets the sheets of the workbook; the file's own name for a CSV file.</summary>
        public ObservableCollection<string> Sheets { get; } = new ObservableCollection<string>();

        /// <summary>Gets the named ranges of the workbook.</summary>
        public ObservableCollection<string> NamedRanges { get; } = new ObservableCollection<string>();

        /// <summary>Gets or sets the sheet chosen.</summary>
        public string Sheet
        {
            get => _settings.Sheet;
            set
            {
                if (_settings.Sheet != (value ?? string.Empty))
                {
                    _settings.Sheet = value ?? string.Empty;
                    Raise(nameof(Sheet));
                    RefreshPreview();
                }
            }
        }

        /// <summary>Gets or sets the named range chosen.</summary>
        public string NamedRange
        {
            get => _settings.NamedRange;
            set
            {
                if (_settings.NamedRange != (value ?? string.Empty))
                {
                    _settings.NamedRange = value ?? string.Empty;
                    Raise(nameof(NamedRange));
                    RefreshPreview();
                }
            }
        }

        /// <summary>Gets or sets the range typed: A1:D10 for a block, A:A for a column, A:D for columns.</summary>
        public string Range
        {
            get => _settings.Range;
            set
            {
                if (_settings.Range != (value ?? string.Empty))
                {
                    _settings.Range = value ?? string.Empty;
                    Raise(nameof(Range));
                    RefreshPreview();
                }
            }
        }

        /// <summary>Gets or sets "Use Excel formatting": the table takes the workbook's alignment, fill and text colour.</summary>
        public bool UseExcelFormatting
        {
            get => _settings.UseExcelFormatting;
            set
            {
                if (_settings.UseExcelFormatting != value)
                {
                    _settings.UseExcelFormatting = value;
                    Raise(nameof(UseExcelFormatting));
                }
            }
        }

        /// <summary>Gets or sets "Allow writing to the source file": the link may write the table back to the file.</summary>
        public bool AllowWritingToSource
        {
            get => _settings.AllowWritingToSource;
            set
            {
                if (_settings.AllowWritingToSource != value)
                {
                    _settings.AllowWritingToSource = value;
                    Raise(nameof(AllowWritingToSource));
                }
            }
        }

        /// <summary>Gets the first cells of what the link takes, one line a row, the cells separated by a bar; or empty.</summary>
        public string Preview
        {
            get => _preview;
            private set => Set(ref _preview, value ?? string.Empty);
        }

        /// <summary>Gets what is wrong, shown red: the file or the range cannot be read, the name is in use.</summary>
        public string Message
        {
            get => _message;
            private set => Set(ref _message, value ?? string.Empty);
        }

        /// <summary>Gets OK.</summary>
        public DelegateCommand OkCommand { get; }

        /// <summary>Gets Cancel and the close button.</summary>
        public DelegateCommand CancelCommand { get; }

        /// <summary>Browse for a file: the one chosen becomes the file, its sheets listed and its first one chosen.</summary>
        public void Browse()
        {
            string chosen = BrowseFile?.Invoke(_settings.File);

            if (!string.IsNullOrEmpty(chosen))
            {
                File = chosen;
            }
        }

        /// <summary>OK: the link made or changed; the reason shown where it is refused.</summary>
        /// <returns>True when it was made, the dialog closed.</returns>
        public bool TryAccept()
        {
            CadEditResult result =
                _editing == null
                    ? CadDataLinks.Create(_document, _settings)
                    : CadDataLinks.Edit(_document, _editing, _settings);

            if (!result.Succeeded)
            {
                Message = result.Message;
                return false;
            }

            Message = string.Empty;
            CloseRequested?.Invoke(this, true);
            return true;
        }

        private string NewName()
        {
            var taken = new HashSet<string>(
                CadDataLinks.List(_document).Select(l => l.Name),
                StringComparer.OrdinalIgnoreCase
            );

            for (int i = 1; ; i++)
            {
                string name = "Data Link " + i.ToString(CultureInfo.InvariantCulture);

                if (!taken.Contains(name))
                {
                    return name;
                }
            }
        }

        private void ReadFrom(DxfDataLink link)
        {
            _settings.Name = link.Name;
            _settings.UseExcelFormatting = (link.UpdateOption & CadDataLinks.SkipFormat) == 0;
            _settings.AllowWritingToSource = (link.UpdateOption & CadDataLinks.AllowSourceUpdate) != 0;

            if (!CadDataSource.TryParse(link.ConnectionString, out string file, out string sheet, out string range))
            {
                return;
            }

            _settings.PathType =
                Path.IsPathRooted(file) ? CadDataLinkPathType.FullPath
                : file.IndexOfAny(new[] { '\\', '/' }) >= 0 ? CadDataLinkPathType.RelativePath
                : CadDataLinkPathType.NoPath;
            _settings.File = CadDataSource.Resolve(file, CadDataLinks.FolderOf(_document)) ?? file;

            if (range.Length > 0)
            {
                _settings.RangeKind = CadDataLinkRangeKind.CellRange;
                _settings.Sheet = sheet;
                _settings.Range = range;
            }
            else
            {
                _settings.Sheet = sheet;
            }
        }

        private void PathChosen(bool on, CadDataLinkPathType type)
        {
            if (on && _settings.PathType != type)
            {
                _settings.PathType = type;
                Raise(nameof(IsFullPath));
                Raise(nameof(IsRelativePath));
                Raise(nameof(IsNoPath));
            }
        }

        private void KindChosen(bool on, CadDataLinkRangeKind kind)
        {
            if (on && _settings.RangeKind != kind)
            {
                _settings.RangeKind = kind;
                Raise(nameof(IsEntireSheet));
                Raise(nameof(IsNamedRange));
                Raise(nameof(IsCellRange));
                Raise(nameof(NeedsSheet));
                RefreshPreview();
            }
        }

        /// <summary>Reads the file's sheets and named ranges for the boxes; a CSV file has its own name for a sheet.</summary>
        private void LoadFile()
        {
            Sheets.Clear();
            NamedRanges.Clear();
            string file = _settings.File ?? string.Empty;

            try
            {
                if (file.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                {
                    Sheets.Add(Path.GetFileNameWithoutExtension(file));
                }
                else if (
                    file.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase)
                    || file.EndsWith(".xlsm", StringComparison.OrdinalIgnoreCase)
                )
                {
                    CadWorkbook book = CadWorkbook.Open(file);

                    foreach (string sheet in book.SheetNames)
                    {
                        Sheets.Add(sheet);
                    }

                    foreach (string name in book.NamedRanges.Keys.OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
                    {
                        NamedRanges.Add(name);
                    }
                }
            }
            catch (Exception exception)
                when (exception is IOException
                    || exception is InvalidDataException
                    || exception is UnauthorizedAccessException
                )
            {
                Message =
                    file.Length == 0
                        ? string.Empty
                        : "The file selected is not a valid Excel file. Please select a different file.";
                Preview = string.Empty;
                return;
            }

            if (Sheets.Count > 0 && !Sheets.Contains(_settings.Sheet, StringComparer.OrdinalIgnoreCase))
            {
                _settings.Sheet = Sheets[0];
                Raise(nameof(Sheet));
            }

            if (NamedRanges.Count > 0 && !NamedRanges.Contains(_settings.NamedRange, StringComparer.OrdinalIgnoreCase))
            {
                _settings.NamedRange = NamedRanges[0];
                Raise(nameof(NamedRange));
            }

            RefreshPreview();
        }

        /// <summary>Shows the first cells the link would take, or why it cannot read them.</summary>
        private void RefreshPreview()
        {
            if (string.IsNullOrWhiteSpace(_settings.File))
            {
                Preview = string.Empty;
                return;
            }

            if (
                _settings.RangeKind == CadDataLinkRangeKind.CellRange
                && !CadSheetRange.TryParse(_settings.Range, out _)
            )
            {
                Preview = string.Empty;
                Message =
                    _settings.Range.Length == 0
                        ? string.Empty
                        : "Invalid Range: enter A1:D10 for a rectangular region, A:A for one whole column, A:D for a set of columns.";
                return;
            }

            try
            {
                CadSheet cells = CadDataLinks.Preview(_document, _settings);
                var text = new StringBuilder();

                for (int r = 0; r < Math.Min(cells.RowCount, 12); r++)
                {
                    text.Append(
                        string.Join(
                            " | ",
                            Enumerable
                                .Range(0, Math.Min(cells.ColumnCount, 8))
                                .Select(c => cells[r, c].Text.Replace('\n', ' '))
                        )
                    );
                    text.Append('\n');
                }

                Preview = text.ToString().TrimEnd('\n');
                Message = string.Empty;
            }
            catch (CadDataSourceException exception)
            {
                Preview = string.Empty;
                Message = exception.Message;
            }
        }
    }
}
