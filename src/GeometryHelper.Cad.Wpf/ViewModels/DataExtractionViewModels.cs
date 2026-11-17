using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace GeometryHelper.Cad.Wpf
{
    /// <summary>The pages of the Data Extraction wizard, in the order AutoCAD 2021 shows them.</summary>
    public enum DataExtractionPage
    {
        /// <summary>Begin: a new extraction, one from a template, or an existing one to edit.</summary>
        Begin = 0,

        /// <summary>Define Data Source: the drawing files and folders, or the objects selected in the current drawing.</summary>
        DataSource = 1,

        /// <summary>Select Objects: the object types to extract.</summary>
        SelectObjects = 2,

        /// <summary>Select Properties: the properties of those objects.</summary>
        SelectProperties = 3,

        /// <summary>Refine Data: the columns, their order and names, the sorts, the filters.</summary>
        RefineData = 4,

        /// <summary>Choose Output: the table in the drawing, the file.</summary>
        ChooseOutput = 5,

        /// <summary>Table Style: the style of the table and its title.</summary>
        TableStyle = 6,

        /// <summary>Finish.</summary>
        Finish = 7,
    }

    /// <summary>What the Begin page starts from.</summary>
    public enum DataExtractionStart
    {
        /// <summary>"Create a new data extraction".</summary>
        CreateNew = 0,

        /// <summary>"Use previous extraction as a template (.dxe)".</summary>
        UseTemplate = 1,

        /// <summary>"Edit an existing data extraction".</summary>
        EditExisting = 2,
    }

    /// <summary>A row of a checked list of the wizard: an object type or a property, with its box.</summary>
    public sealed class DataExtractionCheckRow : ObservableObject
    {
        private bool _isChecked;

        internal DataExtractionCheckRow(string key, string name, string category, string count, bool isChecked)
        {
            Key = key;
            Name = name;
            Category = category;
            Count = count;
            _isChecked = isChecked;
        }

        /// <summary>Gets the key the settings name the row by.</summary>
        public string Key { get; }

        /// <summary>Gets the type's or property's name.</summary>
        public string Name { get; }

        /// <summary>Gets the property's category; empty for an object type.</summary>
        public string Category { get; }

        /// <summary>Gets how many objects an object type has; empty for a property.</summary>
        public string Count { get; }

        /// <summary>Gets or sets whether the row is chosen.</summary>
        public bool IsChecked
        {
            get => _isChecked;
            set => Set(ref _isChecked, value);
        }
    }

    /// <summary>A column of the Refine Data page: its key, the name it is headed with (editable), and whether it is shown.</summary>
    public sealed class DataExtractionColumnRow : ObservableObject
    {
        private string _header;

        internal DataExtractionColumnRow(string key, string header, string original)
        {
            Key = key;
            _header = header;
            Original = original;
        }

        /// <summary>Gets the column's key: Count, Name or a property's.</summary>
        public string Key { get; }

        /// <summary>Gets the name the column has by itself.</summary>
        public string Original { get; }

        /// <summary>Gets or sets the name the column is headed with; "Rename Column".</summary>
        public string Header
        {
            get => _header;
            set => Set(ref _header, value ?? string.Empty);
        }
    }

    /// <summary>
    /// The Data Extraction wizard of DATAEXTRACTION, as AutoCAD 2021's: Begin (a new extraction, one from a template or an
    /// existing one to edit, and the .dxe file it is saved in), Define Data Source (the drawings and folders, the current
    /// drawing, the objects selected, the additional settings), Select Objects (the types, with the display options),
    /// Select Properties (by category), Refine Data (the columns in their order and names, combine identical rows, the Count
    /// and Name columns, sorts, a filter, a preview), Choose Output (the table, the file), Table Style (the style and the
    /// title) and Finish. Finish writes the settings into the command's request.
    /// </summary>
    public sealed class DataExtractionViewModel : ObservableObject
    {
        private static readonly string[] ConditionNames =
        {
            "No filter",
            "Greater than >",
            "Greater than or equal to >",
            "Less than <",
            "Less than or equal to <",
            "Equal to",
            "Not equal to",
            "Between",
            "Outside of",
            "Contains",
            "Begins with",
        };

        private readonly CadDataExtractionRequest _request;
        private CadExtractionSettings _settings;
        private DataExtractionPage _page;
        private DataExtractionStart _start;
        private string _existingFile = string.Empty;
        private string _message = string.Empty;
        private bool _selectedObjects;
        private string _outputFile;
        private string _sortColumn = string.Empty;
        private bool _sortDescending;
        private string _filterColumn = string.Empty;
        private string _filterCondition = ConditionNames[0];
        private string _filterValue = string.Empty;
        private string _filterSecond = string.Empty;
        private string _preview = string.Empty;
        private string _tableStyle;
        private string _title;

        /// <summary>Makes the wizard over the request of DATAEXTRACTION, at the page it names.</summary>
        /// <param name="request">The request.</param>
        public DataExtractionViewModel(CadDataExtractionRequest request)
        {
            _request = request ?? throw new ArgumentNullException(nameof(request));
            _settings = request.Settings ?? new CadExtractionSettings();
            _selectedObjects = _settings.SelectedHandles != null;
            _outputFile = _settings.OutputFile ?? string.Empty;
            _tableStyle = string.IsNullOrEmpty(_settings.TableStyle)
                ? CadTableStyleEditor.Current(request.Document.Database)
                : _settings.TableStyle;
            _title = _settings.Title ?? string.Empty;
            BackCommand = new DelegateCommand(_ => Back(), _ => _page > DataExtractionPage.Begin);
            NextCommand = new DelegateCommand(_ => Next(), _ => _page < DataExtractionPage.Finish);
            FinishCommand = new DelegateCommand(_ => Finish(), _ => _page == DataExtractionPage.Finish);
            CancelCommand = new DelegateCommand(_ => Cancel());
            _page = (DataExtractionPage)Math.Max(0, Math.Min((int)DataExtractionPage.Finish, request.Page));

            foreach (string file in _settings.Files.Concat(_settings.Folders))
            {
                Sources.Add(file);
            }

            foreach (string style in CadTableStyleEditor.Styles(request.Document.Database))
            {
                TableStyles.Add(style);
            }

            if (!TableStyles.Contains("Standard", StringComparer.OrdinalIgnoreCase))
            {
                TableStyles.Add("Standard");
            }

            Enter(_page);
        }

        /// <summary>Raised when the wizard is to close: true for Finish, false for Cancel or for Select objects.</summary>
        public event EventHandler<bool> CloseRequested;

        /// <summary>Gets or sets what asks where to save the .dxe, given the file offered; null where none is chosen. Set by the window.</summary>
        public Func<string, string> AskSaveAs { get; set; }

        /// <summary>Gets or sets what asks for an existing .dxe file; null where none is chosen. Set by the window.</summary>
        public Func<string, string> AskOpenDxe { get; set; }

        /// <summary>Gets or sets what asks for drawing files to add; none where it is not set. Set by the window.</summary>
        public Func<IEnumerable<string>> AskDrawings { get; set; }

        /// <summary>Gets or sets what asks for a folder to add; null where none is chosen. Set by the window.</summary>
        public Func<string> AskFolder { get; set; }

        /// <summary>Gets or sets what asks for the output file; null where none is chosen. Set by the window.</summary>
        public Func<string, string> AskOutput { get; set; }

        /// <summary>Gets the page the wizard is at.</summary>
        public DataExtractionPage Page => _page;

        /// <summary>Gets the page's number from 1 to 8.</summary>
        public int PageNumber => (int)_page + 1;

        /// <summary>Gets the title of the page, as AutoCAD's wizard heads it.</summary>
        public string PageTitle =>
            _page == DataExtractionPage.Begin ? "Data Extraction - Begin (Page 1 of 8)"
            : _page == DataExtractionPage.DataSource ? "Data Extraction - Define Data Source (Page 2 of 8)"
            : _page == DataExtractionPage.SelectObjects ? "Data Extraction - Select Objects (Page 3 of 8)"
            : _page == DataExtractionPage.SelectProperties ? "Data Extraction - Select Properties (Page 4 of 8)"
            : _page == DataExtractionPage.RefineData ? "Data Extraction - Refine Data (Page 5 of 8)"
            : _page == DataExtractionPage.ChooseOutput ? "Data Extraction - Choose Output (Page 6 of 8)"
            : _page == DataExtractionPage.TableStyle ? "Data Extraction - Table Style (Page 7 of 8)"
            : "Data Extraction - Finish (Page 8 of 8)";

        /// <summary>Gets the words under the title of the page, as AutoCAD's wizard words it.</summary>
        public string PageHelp =>
            _page == DataExtractionPage.Begin
                ? "The wizard extracts object data from drawings that can be exported to a table or to an external file. Select whether to create a new data extraction, use previously saved settings from a template, or edit an existing extraction."
            : _page == DataExtractionPage.DataSource ? "Select the objects to extract data from."
            : _page == DataExtractionPage.SelectObjects ? "Select the objects to extract data from."
            : _page == DataExtractionPage.SelectProperties ? "Select the properties you want to extract."
            : _page == DataExtractionPage.RefineData
                ? "In this view you can reorder and sort columns, filter results, and rename columns."
            : _page == DataExtractionPage.ChooseOutput ? "Select the output types for this extraction:"
            : _page == DataExtractionPage.TableStyle ? "Select the table style to use for the inserted table:"
            : "Click Finish to complete the extraction.";

        /// <summary>Gets the settings the wizard works on, as the pages have written them so far.</summary>
        public CadExtractionSettings Settings => _settings;

        #region Begin

        /// <summary>Gets or sets whether the wizard starts a new extraction.</summary>
        public bool IsCreateNew
        {
            get => _start == DataExtractionStart.CreateNew;
            set => StartChosen(value, DataExtractionStart.CreateNew);
        }

        /// <summary>Gets or sets whether the wizard starts from a .dxe used as a template.</summary>
        public bool IsUseTemplate
        {
            get => _start == DataExtractionStart.UseTemplate;
            set => StartChosen(value, DataExtractionStart.UseTemplate);
        }

        /// <summary>Gets or sets whether the wizard edits an existing .dxe.</summary>
        public bool IsEditExisting
        {
            get => _start == DataExtractionStart.EditExisting;
            set => StartChosen(value, DataExtractionStart.EditExisting);
        }

        /// <summary>Gets whether a .dxe is asked for: the template or the extraction to edit.</summary>
        public bool NeedsFile => _start != DataExtractionStart.CreateNew;

        /// <summary>Gets or sets the .dxe file of the template or of the extraction to edit.</summary>
        public string ExistingFile
        {
            get => _existingFile;
            set => Set(ref _existingFile, value ?? string.Empty);
        }

        /// <summary>Gets the .dxe file the extraction is saved in.</summary>
        public string DxeFile => _request.DxeFile;

        /// <summary>Browse of the Begin page: the .dxe file asked for.</summary>
        public void BrowseExisting()
        {
            string chosen = AskOpenDxe?.Invoke(_existingFile);

            if (!string.IsNullOrEmpty(chosen))
            {
                ExistingFile = chosen;
            }
        }

        #endregion

        #region Define data source

        /// <summary>Gets the drawing files and folders that are sources besides the current drawing.</summary>
        public ObservableCollection<string> Sources { get; } = new ObservableCollection<string>();

        /// <summary>Gets or sets the source chosen in the list, for Remove.</summary>
        public string SelectedSource { get; set; }

        /// <summary>Gets or sets whether the current drawing is a source.</summary>
        public bool IncludeCurrentDrawing
        {
            get => _settings.IncludeCurrentDrawing;
            set
            {
                if (_settings.IncludeCurrentDrawing != value)
                {
                    _settings.IncludeCurrentDrawing = value;
                    Raise(nameof(IncludeCurrentDrawing));
                }
            }
        }

        /// <summary>Gets or sets "Select objects in the current drawing" against "Drawing files and folders".</summary>
        public bool UseSelectedObjects
        {
            get => _selectedObjects;
            set
            {
                if (Set(ref _selectedObjects, value))
                {
                    Raise(nameof(UseDrawingFiles));
                    Raise(nameof(NumberSelected));

                    if (value)
                    {
                        _settings.SelectedHandles = _settings.SelectedHandles ?? new List<string>();
                    }
                    else
                    {
                        _settings.SelectedHandles = null;
                    }
                }
            }
        }

        /// <summary>Gets or sets whether the sources are the drawing files and folders.</summary>
        public bool UseDrawingFiles
        {
            get => !_selectedObjects;
            set => UseSelectedObjects = !value;
        }

        /// <summary>Gets the words beside the Select objects button: how many objects are selected.</summary>
        public string NumberSelected =>
            _settings.SelectedHandles == null
                ? string.Empty
                : _settings.SelectedHandles.Count.ToString(CultureInfo.InvariantCulture) + " object(s) selected";

        /// <summary>Gets or sets whether the folders added are searched in their subfolders.</summary>
        public bool IncludeSubfolders
        {
            get => _settings.IncludeSubfolders;
            set
            {
                if (_settings.IncludeSubfolders != value)
                {
                    _settings.IncludeSubfolders = value;
                    Raise(nameof(IncludeSubfolders));
                }
            }
        }

        /// <summary>Gets or sets "Extract objects from blocks".</summary>
        public bool ExtractFromBlocks
        {
            get => _settings.ExtractFromBlocks;
            set
            {
                if (_settings.ExtractFromBlocks != value)
                {
                    _settings.ExtractFromBlocks = value;
                    Raise(nameof(ExtractFromBlocks));
                }
            }
        }

        /// <summary>Gets or sets "Extract objects from xrefs" (kept in the file; an xref's own objects are not read).</summary>
        public bool ExtractFromXrefs
        {
            get => _settings.ExtractFromXrefs;
            set
            {
                if (_settings.ExtractFromXrefs != value)
                {
                    _settings.ExtractFromXrefs = value;
                    Raise(nameof(ExtractFromXrefs));
                }
            }
        }

        /// <summary>Gets or sets "Include xrefs in block counts".</summary>
        public bool IncludeXrefsInCounts
        {
            get => _settings.IncludeXrefsInCounts;
            set
            {
                if (_settings.IncludeXrefsInCounts != value)
                {
                    _settings.IncludeXrefsInCounts = value;
                    Raise(nameof(IncludeXrefsInCounts));
                }
            }
        }

        /// <summary>Gets or sets "Objects in model space" against "All objects in drawing".</summary>
        public bool ModelSpaceOnly
        {
            get => _settings.ModelSpaceOnly;
            set
            {
                if (_settings.ModelSpaceOnly != value)
                {
                    _settings.ModelSpaceOnly = value;
                    Raise(nameof(ModelSpaceOnly));
                    Raise(nameof(AllObjectsInDrawing));
                }
            }
        }

        /// <summary>Gets or sets "All objects in drawing".</summary>
        public bool AllObjectsInDrawing
        {
            get => !_settings.ModelSpaceOnly;
            set => ModelSpaceOnly = !value;
        }

        /// <summary>Add Drawings: the files asked for are added to the sources.</summary>
        public void AddDrawings()
        {
            foreach (string file in AskDrawings?.Invoke() ?? Enumerable.Empty<string>())
            {
                AddSource(file, false);
            }
        }

        /// <summary>Add Folder: the folder asked for is added to the sources.</summary>
        public void AddFolder()
        {
            string folder = AskFolder?.Invoke();

            if (!string.IsNullOrEmpty(folder))
            {
                AddSource(folder, true);
            }
        }

        /// <summary>Adds a drawing file or a folder to the sources, once.</summary>
        /// <param name="path">The full path.</param>
        /// <param name="isFolder">Whether it is a folder.</param>
        public void AddSource(string path, bool isFolder)
        {
            if (string.IsNullOrWhiteSpace(path) || Sources.Contains(path, StringComparer.OrdinalIgnoreCase))
            {
                return;
            }

            (isFolder ? _settings.Folders : _settings.Files).Add(path);
            Sources.Add(path);
        }

        /// <summary>Remove: the source chosen taken out.</summary>
        public void RemoveSource()
        {
            if (SelectedSource == null)
            {
                return;
            }

            _settings.Files.RemoveAll(f => string.Equals(f, SelectedSource, StringComparison.OrdinalIgnoreCase));
            _settings.Folders.RemoveAll(f => string.Equals(f, SelectedSource, StringComparison.OrdinalIgnoreCase));
            Sources.Remove(SelectedSource);
            SelectedSource = null;
        }

        /// <summary>Select objects &lt;: the wizard is closed for the objects to be picked in the drawing and shown again at this page.</summary>
        public void SelectObjects()
        {
            _selectedObjects = true;
            _settings.SelectedHandles = _settings.SelectedHandles ?? new List<string>();
            Leave(_page);
            _request.Settings = _settings;
            _request.Page = (int)_page;
            _request.IsSelecting = true;
            _request.Action = CadDialogAction.Cancel;
            CloseRequested?.Invoke(this, false);
        }

        #endregion

        #region Select objects

        /// <summary>Gets the object types of the sources, each with its box and how many there are.</summary>
        public ObservableCollection<DataExtractionCheckRow> Types { get; } =
            new ObservableCollection<DataExtractionCheckRow>();

        /// <summary>Gets or sets "Display all object types".</summary>
        public bool DisplayAll
        {
            get => _settings.Display == CadExtractionDisplay.All;
            set => DisplayChosen(value, CadExtractionDisplay.All);
        }

        /// <summary>Gets or sets "Display blocks only".</summary>
        public bool DisplayBlocksOnly
        {
            get => _settings.Display == CadExtractionDisplay.BlocksOnly;
            set => DisplayChosen(value, CadExtractionDisplay.BlocksOnly);
        }

        /// <summary>Gets or sets "Display non-blocks only".</summary>
        public bool DisplayNonBlocksOnly
        {
            get => _settings.Display == CadExtractionDisplay.NonBlocksOnly;
            set => DisplayChosen(value, CadExtractionDisplay.NonBlocksOnly);
        }

        /// <summary>Gets or sets "Display blocks with attributes only".</summary>
        public bool DisplayAttributesOnly
        {
            get => _settings.BlocksWithAttributesOnly;
            set
            {
                if (_settings.BlocksWithAttributesOnly != value)
                {
                    Leave(DataExtractionPage.SelectObjects);
                    _settings.BlocksWithAttributesOnly = value;
                    Raise(nameof(DisplayAttributesOnly));
                    FillTypes();
                }
            }
        }

        /// <summary>Gets or sets "Display objects currently in-use only".</summary>
        public bool DisplayUsedOnly
        {
            get => _settings.UsedOnly;
            set
            {
                if (_settings.UsedOnly != value)
                {
                    Leave(DataExtractionPage.SelectObjects);
                    _settings.UsedOnly = value;
                    Raise(nameof(DisplayUsedOnly));
                    FillTypes();
                }
            }
        }

        /// <summary>Checks or unchecks every object type listed.</summary>
        /// <param name="on">Whether they are checked.</param>
        public void CheckAllTypes(bool on)
        {
            foreach (DataExtractionCheckRow row in Types)
            {
                row.IsChecked = on;
            }
        }

        #endregion

        #region Select properties

        /// <summary>Gets the categories of properties, each with its box ("Category filter").</summary>
        public ObservableCollection<DataExtractionCheckRow> Categories { get; } =
            new ObservableCollection<DataExtractionCheckRow>();

        /// <summary>Gets the properties of the objects chosen, each with its box.</summary>
        public ObservableCollection<DataExtractionCheckRow> Properties { get; } =
            new ObservableCollection<DataExtractionCheckRow>();

        /// <summary>Checks or unchecks every property listed.</summary>
        /// <param name="on">Whether they are checked.</param>
        public void CheckAllProperties(bool on)
        {
            foreach (DataExtractionCheckRow row in Properties)
            {
                row.IsChecked = on;
            }
        }

        /// <summary>Reads the properties again after a category was checked or unchecked, the other boxes kept.</summary>
        public void CategoriesChanged()
        {
            Leave(DataExtractionPage.SelectProperties);
            FillProperties();
        }

        #endregion

        #region Refine data

        /// <summary>Gets the columns of the table in their order, each with the name it is headed with.</summary>
        public ObservableCollection<DataExtractionColumnRow> Columns { get; } =
            new ObservableCollection<DataExtractionColumnRow>();

        /// <summary>Gets or sets the column chosen, for Up, Down and the sort and filter boxes.</summary>
        public DataExtractionColumnRow SelectedColumn { get; set; }

        /// <summary>Gets or sets "Combine identical rows".</summary>
        public bool CombineIdenticalRows
        {
            get => _settings.CombineIdenticalRows;
            set
            {
                if (_settings.CombineIdenticalRows != value)
                {
                    _settings.CombineIdenticalRows = value;
                    Raise(nameof(CombineIdenticalRows));
                    RefreshColumns();
                }
            }
        }

        /// <summary>Gets or sets "Show count column".</summary>
        public bool ShowCount
        {
            get => _settings.ShowCount;
            set
            {
                if (_settings.ShowCount != value)
                {
                    _settings.ShowCount = value;
                    Raise(nameof(ShowCount));
                    RefreshColumns();
                }
            }
        }

        /// <summary>Gets or sets "Show name column".</summary>
        public bool ShowName
        {
            get => _settings.ShowName;
            set
            {
                if (_settings.ShowName != value)
                {
                    _settings.ShowName = value;
                    Raise(nameof(ShowName));
                    RefreshColumns();
                }
            }
        }

        /// <summary>Gets or sets "Use property names as additional column headers".</summary>
        public bool UsePropertyNamesAsHeaders
        {
            get => _settings.UsePropertyNamesAsHeaders;
            set
            {
                if (_settings.UsePropertyNamesAsHeaders != value)
                {
                    _settings.UsePropertyNamesAsHeaders = value;
                    Raise(nameof(UsePropertyNamesAsHeaders));
                }
            }
        }

        /// <summary>Gets the columns the sort and the filter can name, as the boxes list them.</summary>
        public ObservableCollection<string> ColumnKeys { get; } = new ObservableCollection<string>();

        /// <summary>Gets or sets the column the rows are sorted by; empty for none.</summary>
        public string SortColumn
        {
            get => _sortColumn;
            set
            {
                if (Set(ref _sortColumn, value ?? string.Empty))
                {
                    ApplySort();
                }
            }
        }

        /// <summary>Gets or sets whether the sort goes down.</summary>
        public bool SortDescending
        {
            get => _sortDescending;
            set
            {
                if (Set(ref _sortDescending, value))
                {
                    ApplySort();
                }
            }
        }

        /// <summary>Gets the conditions of a filter, as the Filter Options box lists them.</summary>
        public IReadOnlyList<string> FilterConditions => ConditionNames;

        /// <summary>Gets or sets the column the filter is on.</summary>
        public string FilterColumn
        {
            get => _filterColumn;
            set
            {
                if (Set(ref _filterColumn, value ?? string.Empty))
                {
                    ApplyFilter();
                }
            }
        }

        /// <summary>Gets or sets the condition of the filter, one of <see cref="FilterConditions"/>.</summary>
        public string FilterCondition
        {
            get => _filterCondition;
            set
            {
                if (Set(ref _filterCondition, value ?? ConditionNames[0]))
                {
                    Raise(nameof(FilterNeedsSecond));
                    ApplyFilter();
                }
            }
        }

        /// <summary>Gets or sets the value of the filter.</summary>
        public string FilterValue
        {
            get => _filterValue;
            set
            {
                if (Set(ref _filterValue, value ?? string.Empty))
                {
                    ApplyFilter();
                }
            }
        }

        /// <summary>Gets or sets the second value of Between and Outside of.</summary>
        public string FilterSecond
        {
            get => _filterSecond;
            set
            {
                if (Set(ref _filterSecond, value ?? string.Empty))
                {
                    ApplyFilter();
                }
            }
        }

        /// <summary>Gets whether the condition takes two values.</summary>
        public bool FilterNeedsSecond => _filterCondition == "Between" || _filterCondition == "Outside of";

        /// <summary>Gets the first rows of the table as the settings make it, a row a line, the columns separated by a bar.</summary>
        public string Preview
        {
            get => _preview;
            private set => Set(ref _preview, value ?? string.Empty);
        }

        /// <summary>Moves the column chosen one place left (up) or right (down) in the table.</summary>
        /// <param name="down">Whether it moves later.</param>
        public void MoveColumn(bool down)
        {
            if (SelectedColumn == null)
            {
                return;
            }

            int at = Columns.IndexOf(SelectedColumn);
            int to = down ? at + 1 : at - 1;

            if (at < 0 || to < 0 || to >= Columns.Count)
            {
                return;
            }

            Columns.Move(at, to);
            CommitColumns();
            RefreshPreview();
        }

        /// <summary>Writes the names, the order and the sort and the filter of the Refine Data page into the settings.</summary>
        public void CommitColumns()
        {
            _settings.ColumnOrder = Columns.Select(c => c.Key).ToList();
            _settings.ColumnNames.Clear();

            foreach (DataExtractionColumnRow column in Columns)
            {
                if (!string.IsNullOrWhiteSpace(column.Header) && column.Header != column.Original)
                {
                    _settings.ColumnNames[column.Key] = column.Header.Trim();
                }
            }
        }

        #endregion

        #region Choose output

        /// <summary>Gets or sets "Insert data extraction table into drawing".</summary>
        public bool InsertTable
        {
            get => _settings.InsertTable;
            set
            {
                if (_settings.InsertTable != value)
                {
                    _settings.InsertTable = value;
                    Raise(nameof(InsertTable));
                }
            }
        }

        /// <summary>Gets or sets "Output data to external file (.xlsx .csv .txt)".</summary>
        public bool OutputToFile
        {
            get => _settings.OutputToFile;
            set
            {
                if (_settings.OutputToFile != value)
                {
                    _settings.OutputToFile = value;
                    Raise(nameof(OutputToFile));
                }
            }
        }

        /// <summary>Gets or sets the output file.</summary>
        public string OutputFile
        {
            get => _outputFile;
            set
            {
                if (Set(ref _outputFile, value ?? string.Empty))
                {
                    _settings.OutputFile = _outputFile;
                }
            }
        }

        /// <summary>Browse of the Choose Output page.</summary>
        public void BrowseOutput()
        {
            string chosen = AskOutput?.Invoke(_outputFile);

            if (!string.IsNullOrEmpty(chosen))
            {
                OutputFile = chosen;
                OutputToFile = true;
            }
        }

        #endregion

        #region Table style

        /// <summary>Gets the table styles of the drawing.</summary>
        public ObservableCollection<string> TableStyles { get; } = new ObservableCollection<string>();

        /// <summary>Gets or sets the table style of the inserted table.</summary>
        public string TableStyle
        {
            get => _tableStyle;
            set
            {
                if (Set(ref _tableStyle, value ?? string.Empty))
                {
                    _settings.TableStyle = _tableStyle;
                }
            }
        }

        /// <summary>Gets or sets the title of the table.</summary>
        public string Title
        {
            get => _title;
            set
            {
                if (Set(ref _title, value ?? string.Empty))
                {
                    _settings.Title = _title;
                }
            }
        }

        #endregion

        /// <summary>Gets what is wrong with the page, shown red: a file that cannot be read, nothing chosen.</summary>
        public string Message
        {
            get => _message;
            private set => Set(ref _message, value ?? string.Empty);
        }

        /// <summary>Gets Back.</summary>
        public DelegateCommand BackCommand { get; }

        /// <summary>Gets Next.</summary>
        public DelegateCommand NextCommand { get; }

        /// <summary>Gets Finish.</summary>
        public DelegateCommand FinishCommand { get; }

        /// <summary>Gets Cancel and the close button.</summary>
        public DelegateCommand CancelCommand { get; }

        /// <summary>Next: the page's choices taken, the next page entered; from Begin the .dxe is read or asked for.</summary>
        public void Next()
        {
            if (_page >= DataExtractionPage.Finish)
            {
                return;
            }

            if (!Leave(_page))
            {
                return;
            }

            Go(_page + 1);
        }

        /// <summary>Back: the page's choices taken, the page before entered.</summary>
        public void Back()
        {
            if (_page > DataExtractionPage.Begin)
            {
                Leave(_page);
                Go(_page - 1);
            }
        }

        /// <summary>Finish: the settings and the .dxe file written into the request, the wizard closed.</summary>
        public void Finish()
        {
            if (!Leave(_page))
            {
                return;
            }

            if (!_settings.InsertTable && !(_settings.OutputToFile && !string.IsNullOrWhiteSpace(_settings.OutputFile)))
            {
                Message = "Choose an output: a table in the drawing, a file, or both.";
                return;
            }

            _request.Settings = _settings;
            _request.Page = (int)_page;
            _request.IsSelecting = false;
            _request.Action = CadDialogAction.Ok;
            CloseRequested?.Invoke(this, true);
        }

        /// <summary>Cancel: nothing is extracted or written.</summary>
        public void Cancel()
        {
            _request.IsSelecting = false;
            _request.Action = CadDialogAction.Cancel;
            CloseRequested?.Invoke(this, false);
        }

        private void Go(DataExtractionPage page)
        {
            _page = page;
            Message = string.Empty;
            Enter(page);
            Raise(nameof(Page));
            Raise(nameof(PageNumber));
            Raise(nameof(PageTitle));
            Raise(nameof(PageHelp));
            BackCommand.RaiseCanExecuteChanged();
            NextCommand.RaiseCanExecuteChanged();
            FinishCommand.RaiseCanExecuteChanged();
        }

        /// <summary>Takes the choices of a page into the settings, as leaving it by Next, Back or Finish does; false where the page cannot be left.</summary>
        private bool Leave(DataExtractionPage page)
        {
            switch (page)
            {
                case DataExtractionPage.Begin:
                    return LeaveBegin();
                case DataExtractionPage.SelectObjects:
                    if (Types.Count > 0)
                    {
                        List<string> chosen = Types.Where(t => t.IsChecked).Select(t => t.Key).ToList();
                        _settings.SelectedTypes =
                            chosen.Count == Types.Count
                            && !_settings.UsedOnly
                            && _settings.Display == CadExtractionDisplay.All
                            && !_settings.BlocksWithAttributesOnly
                                ? null
                                : chosen;
                    }

                    break;
                case DataExtractionPage.SelectProperties:
                    _settings.UnselectedCategories.Clear();
                    _settings.UnselectedCategories.AddRange(Categories.Where(c => !c.IsChecked).Select(c => c.Key));

                    if (Properties.Count > 0)
                    {
                        _settings.SelectedProperties = Properties.Where(p => p.IsChecked).Select(p => p.Key).ToList();
                    }

                    break;
                case DataExtractionPage.RefineData:
                    CommitColumns();
                    break;
            }

            return true;
        }

        private bool LeaveBegin()
        {
            if (_start != DataExtractionStart.CreateNew)
            {
                string path = (_existingFile ?? string.Empty).Trim().Trim('"');

                if (path.Length == 0 || !File.Exists(path))
                {
                    Message =
                        path.Length == 0
                            ? "Choose a data extraction file (.dxe)."
                            : "\"" + Path.GetFileName(path) + "\": Can't find file.";
                    return false;
                }

                try
                {
                    _settings = CadExtractionSettings.Load(path);
                }
                catch (Exception exception)
                    when (exception is InvalidDataException
                        || exception is IOException
                        || exception is UnauthorizedAccessException
                    )
                {
                    Message = exception.Message;
                    return false;
                }

                _selectedObjects = _settings.SelectedHandles != null;
                _outputFile = _settings.OutputFile ?? string.Empty;
                _title = _settings.Title ?? string.Empty;

                if (!string.IsNullOrEmpty(_settings.TableStyle))
                {
                    _tableStyle = _settings.TableStyle;
                }

                Sources.Clear();

                foreach (string file in _settings.Files.Concat(_settings.Folders))
                {
                    Sources.Add(file);
                }

                _request.Settings = _settings;
            }

            if (_start == DataExtractionStart.EditExisting)
            {
                _request.DxeFile = Path.GetFullPath(_existingFile.Trim().Trim('"'));
            }
            else
            {
                string chosen = AskSaveAs?.Invoke(_request.DxeFile);

                if (chosen == null && AskSaveAs != null)
                {
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(chosen))
                {
                    _request.DxeFile = chosen.EndsWith(".dxe", StringComparison.OrdinalIgnoreCase)
                        ? chosen
                        : chosen + ".dxe";
                }
            }

            RaiseAllSettings();
            return true;
        }

        private void Enter(DataExtractionPage page)
        {
            switch (page)
            {
                case DataExtractionPage.SelectObjects:
                    FillTypes();
                    break;
                case DataExtractionPage.SelectProperties:
                    FillCategories();
                    FillProperties();
                    break;
                case DataExtractionPage.RefineData:
                    RefreshColumns();
                    break;
            }
        }

        private void RaiseAllSettings()
        {
            foreach (
                string name in new[]
                {
                    nameof(IncludeCurrentDrawing),
                    nameof(UseSelectedObjects),
                    nameof(UseDrawingFiles),
                    nameof(NumberSelected),
                    nameof(IncludeSubfolders),
                    nameof(ExtractFromBlocks),
                    nameof(ExtractFromXrefs),
                    nameof(IncludeXrefsInCounts),
                    nameof(ModelSpaceOnly),
                    nameof(AllObjectsInDrawing),
                    nameof(DisplayAll),
                    nameof(DisplayBlocksOnly),
                    nameof(DisplayNonBlocksOnly),
                    nameof(DisplayAttributesOnly),
                    nameof(DisplayUsedOnly),
                    nameof(CombineIdenticalRows),
                    nameof(ShowCount),
                    nameof(ShowName),
                    nameof(UsePropertyNamesAsHeaders),
                    nameof(InsertTable),
                    nameof(OutputToFile),
                    nameof(OutputFile),
                    nameof(TableStyle),
                    nameof(Title),
                    nameof(DxeFile),
                }
            )
            {
                Raise(name);
            }
        }

        private void StartChosen(bool on, DataExtractionStart start)
        {
            if (on && _start != start)
            {
                _start = start;
                Raise(nameof(IsCreateNew));
                Raise(nameof(IsUseTemplate));
                Raise(nameof(IsEditExisting));
                Raise(nameof(NeedsFile));
            }
        }

        private void DisplayChosen(bool on, CadExtractionDisplay display)
        {
            if (on && _settings.Display != display)
            {
                Leave(DataExtractionPage.SelectObjects);
                _settings.Display = display;
                Raise(nameof(DisplayAll));
                Raise(nameof(DisplayBlocksOnly));
                Raise(nameof(DisplayNonBlocksOnly));
                FillTypes();
            }
        }

        private void FillTypes()
        {
            IReadOnlyList<CadExtractionType> types = CadExtraction.Types(
                _request.Document,
                _settings,
                out IReadOnlyList<string> missing
            );
            Types.Clear();

            foreach (CadExtractionType type in types)
            {
                bool on = _settings.SelectedTypes == null || _settings.SelectedTypes.Contains(type.Name);
                Types.Add(
                    new DataExtractionCheckRow(
                        type.Name,
                        type.Name,
                        string.Empty,
                        type.Count.ToString(CultureInfo.InvariantCulture),
                        on
                    )
                );
            }

            Message =
                missing.Count == 0
                    ? string.Empty
                    : "The drawing \"" + Path.GetFileName(missing[0]) + "\" could not be opened.";
        }

        private void FillCategories()
        {
            Categories.Clear();

            foreach (string category in CadExtraction.Categories)
            {
                Categories.Add(
                    new DataExtractionCheckRow(
                        category,
                        category,
                        string.Empty,
                        string.Empty,
                        !_settings.UnselectedCategories.Contains(category)
                    )
                );
            }
        }

        private void FillProperties()
        {
            Properties.Clear();

            foreach (CadExtractionProperty property in CadExtraction.Properties(_request.Document, _settings))
            {
                bool on = _settings.SelectedProperties == null || _settings.SelectedProperties.Contains(property.Key);
                Properties.Add(
                    new DataExtractionCheckRow(property.Key, property.Name, property.Category, string.Empty, on)
                );
            }
        }

        /// <summary>Runs the extraction for the columns and the preview; the columns the user ordered and named kept.</summary>
        private void RefreshColumns()
        {
            CadExtractionResult result = CadExtraction.Run(_request.Document, _settings);
            string[] before = Columns.Select(c => c.Key).ToArray();
            var named = Columns.ToDictionary(c => c.Key, c => c.Header);
            Columns.Clear();
            ColumnKeys.Clear();
            ColumnKeys.Add(string.Empty);

            foreach (string key in result.Columns)
            {
                string original = key == "Count" || key == "Name" ? key : key.Substring(key.IndexOf('/') + 1);
                string header = _settings.ColumnNames.TryGetValue(key, out string alias) ? alias : original;
                Columns.Add(new DataExtractionColumnRow(key, header, original));
                ColumnKeys.Add(key);
            }

            if (_sortColumn.Length > 0 && !ColumnKeys.Contains(_sortColumn))
            {
                _sortColumn = string.Empty;
                Raise(nameof(SortColumn));
            }

            if (before.Length > 0 && named.Count > 0 && Columns.All(c => named.ContainsKey(c.Key)))
            {
                CommitColumns();
            }

            Preview = PreviewOf(result.Sheet);
        }

        private void RefreshPreview() => Preview = PreviewOf(CadExtraction.Run(_request.Document, _settings).Sheet);

        private void ApplySort()
        {
            _settings.Sorts.Clear();

            if (_sortColumn.Length > 0)
            {
                _settings.Sorts.Add(new CadExtractionSort(_sortColumn, _sortDescending));
            }

            RefreshPreview();
        }

        private void ApplyFilter()
        {
            _settings.Filters.Clear();
            int condition = Array.IndexOf(ConditionNames, _filterCondition);

            if (_filterColumn.Length > 0 && condition > 0)
            {
                _settings.Filters.Add(
                    new CadExtractionFilter(_filterColumn, (CadFilterCondition)condition, _filterValue, _filterSecond)
                );
            }

            RefreshPreview();
        }

        private static string PreviewOf(CadSheet sheet)
        {
            var text = new StringBuilder();

            for (int r = 0; r < Math.Min(sheet.RowCount, 12); r++)
            {
                text.Append(
                    string.Join(
                        " | ",
                        Enumerable.Range(0, sheet.ColumnCount).Select(c => sheet[r, c].Text.Replace('\n', ' '))
                    )
                );
                text.Append('\n');
            }

            return text.ToString().TrimEnd('\n');
        }
    }
}
