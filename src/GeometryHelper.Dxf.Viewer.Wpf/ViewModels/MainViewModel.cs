using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Input;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// The logic of the presenter's main window, with no window: the DRAWINGS list, the document and space shown with their
    /// view, the command line of each document (a <see cref="CadCommandSession"/>), the object snaps, the selection and what
    /// Properties says of it, the layers, undo, redo and saving, the cursor's coordinates and the status bar. A test opens a
    /// file, types commands, clicks points and reads the result here.
    /// </summary>
    /// <remarks>
    /// The list holds every file opened and the other DXF files of their folders, grouped by folder; a click selects a row,
    /// and a double click or Enter opens it or shows it again. Each space of each file keeps its own view, zoomed to the extents of the space the first time it is
    /// shown and refitted when the canvas changes size until the user zooms or pans. Commands come from
    /// <see cref="CadCommandRegistry.CreateDefault"/>, with two of the Layers panel added: LAYMCUR (Make Current) and LAYMCH
    /// (Match Layer). The status toggles drive <see cref="Snap"/>: OSNAP its object snaps, ORTHO, POLAR, and SNAP a grid of 10.
    /// </remarks>
    public sealed partial class MainViewModel : ObservableObject
    {
        /// <summary>The line the command line shows before anything has been written to it, where a host makes drawings from a model.</summary>
        public const string WelcomeText = "Welcome. Ctrl+O opens a DXF, Ctrl+N makes a drawing from a model.";

        /// <summary>The line the command line shows before anything has been written to it, where no host makes drawings from a model: GeometryHelper DXF's.</summary>
        public const string NewDrawingWelcomeText = "Welcome. Ctrl+O opens a DXF, Ctrl+N starts a new drawing.";

        /// <summary>The label at the top left of the canvas in model space.</summary>
        public const string ModelViewportLabel = "[-][Model][2D Wireframe]";

        private const int HistoryShown = 3;
        private static readonly NumberFormatInfo Spaced = new NumberFormatInfo
        {
            NumberGroupSeparator = " ",
            NumberDecimalSeparator = ".",
        };
        private static readonly string[] ColorOptions =
        {
            "ByLayer",
            "ByBlock",
            "Red",
            "Yellow",
            "Green",
            "Cyan",
            "Blue",
            "Magenta",
            "White",
            SelectColorOption,
        };

        private static readonly string[] WeightOptions =
        {
            "ByLayer",
            "ByBlock",
            "Default",
            "0.00 mm",
            "0.05 mm",
            "0.09 mm",
            "0.13 mm",
            "0.15 mm",
            "0.18 mm",
            "0.20 mm",
            "0.25 mm",
            "0.30 mm",
            "0.35 mm",
            "0.40 mm",
            "0.50 mm",
            "0.53 mm",
            "0.60 mm",
            "0.70 mm",
            "0.80 mm",
            "0.90 mm",
            "1.00 mm",
            "1.06 mm",
            "1.20 mm",
            "1.40 mm",
            "1.58 mm",
            "2.00 mm",
            "2.11 mm",
        };

        private readonly Dictionary<string, KnownFile> _files = new Dictionary<string, KnownFile>(
            StringComparer.OrdinalIgnoreCase
        );
        private readonly List<string> _inputs = new List<string>();
        private readonly List<CadWorkspaceExtension> _extensions = new List<CadWorkspaceExtension>();
        private KnownFile _current;
        private int _revision;
        private string _searchText = string.Empty;
        private bool _dxfOnly;
        private bool _ownOnly;
        private bool _outOfDateOnly;
        private CadWorkspaceExtension _selectedTab;
        private string _banner;
        private string _selectionLabel = string.Empty;
        private IReadOnlyList<PropertyGroupViewModel> _groups = Array.Empty<PropertyGroupViewModel>();
        private IReadOnlyList<CadLayerInfo> _layers = Array.Empty<CadLayerInfo>();
        private string _commandText = string.Empty;
        private int _recall = -1;
        private string _cursorText = Coordinates(0.0, 0.0);
        private double _canvasWidth = 1000.0;
        private double _canvasHeight = 600.0;

        /// <summary>Makes the view model with no drawing open.</summary>
        public MainViewModel()
        {
            Snap = new ObjectSnap(Tolerance.Default)
            {
                Modes = CadSnapModes.All & ~CadSnapModes.Nearest,
                GridX = 10.0,
                GridY = 10.0,
                PolarAngle = Math.PI / 2.0,
            };
            // As AutoCAD 2021 starts: SNAPMODE 0 and ORTHOMODE 0, AUTOSNAP 63 (POLAR and OTRACK on), OSMODE 4133 (OSNAP on).
            Toggles = new ObservableCollection<StatusToggleViewModel>
            {
                new StatusToggleViewModel("GRID", false),
                new StatusToggleViewModel("SNAP", false),
                new StatusToggleViewModel("ORTHO", false),
                new StatusToggleViewModel("POLAR", true),
                new StatusToggleViewModel("OTRACK", true),
                new StatusToggleViewModel("OSNAP", true),
            };

            foreach (StatusToggleViewModel toggle in Toggles)
            {
                toggle.PropertyChanged += OnToggleChanged;
            }

            ApplyToggles();
            DynamicInput = new StatusToggleViewModel("DYN", _settings.DynamicInput);
            DynamicInput.PropertyChanged += OnDynamicInputChanged;
            PropertyChanged += OnInputSourceChanged;
            ZoomExtentsCommand = new DelegateCommand(_ => Execute("ZOOM E"));
            PanCommand = new DelegateCommand(_ => StartCommand("PAN"));
            OpenCommand = new DelegateCommand(_ => OpenRequested?.Invoke(this, EventArgs.Empty));
            RefreshCommand = new DelegateCommand(_ => Refresh());
            ActivateFileCommand = new DelegateCommand(p => Activate(p as FileRowViewModel));
            SelectSpaceCommand = new DelegateCommand(p => SelectSpace((p as SpaceTabViewModel)?.Space));
            StartCommandCommand = new DelegateCommand(p => StartCommand(p as string), _ => HasDocument);
            UndoCommand = new DelegateCommand(_ => Undo());
            RedoCommand = new DelegateCommand(_ => Redo());
            SaveCommand = new DelegateCommand(_ => Save());
            LayerManagerCommand = new DelegateCommand(_ => StartCommand("LAYER"));
            MakeCurrentCommand = new DelegateCommand(_ => StartCommand("LAYMCUR"));
            MatchLayerCommand = new DelegateCommand(_ => StartCommand("LAYMCH"));
            NewFromModelCommand = new DelegateCommand(
                _ => _newFromModel?.Invoke(this, EventArgs.Empty),
                _ => _newFromModel != null
            );
            SelectTabCommand = new DelegateCommand(p => SelectedTab = p as CadWorkspaceExtension);

            // The runs of an MTEXT of several looks are put one after another as the canvas measures their letters.
            CadTrueTypeFonts.MeasureWidth = SkiaCanvasRenderer.MeasureWidth;
        }

        /// <summary>
        /// Raised by Ctrl+N and the start screen's "New drawing from a model": a host that makes drawings from a model (Part B)
        /// shows its New drawing dialog. With no handler the button is hidden and Ctrl+N makes a new empty drawing (NEW).
        /// </summary>
        public event EventHandler NewFromModelRequested
        {
            add
            {
                _newFromModel += value;
                OnNewFromModelChanged();
            }
            remove
            {
                _newFromModel -= value;
                OnNewFromModelChanged();
            }
        }

        private EventHandler _newFromModel;

        /// <summary>
        /// Gets whether a host makes drawings from a model, handling <see cref="NewFromModelRequested"/>: the start screen then
        /// offers "New drawing from a model" and Ctrl+N asks for one; without, as in GeometryHelper DXF, Ctrl+N is NEW.
        /// </summary>
        public bool CanMakeFromModel => _newFromModel != null;

        /// <summary>Gets the line under the name of the app on the start screen.</summary>
        public string StartScreenText =>
            CanMakeFromModel
                ? "Open and edit any 2D drawing, or make drawings from a model. No file is open."
                : "Open and edit any 2D drawing. No file is open.";

        /// <summary>
        /// Ctrl+N: a new drawing from a model where a host makes them (<see cref="NewFromModelRequested"/>), else a new empty
        /// drawing (NEW), as AutoCAD's Ctrl+N.
        /// </summary>
        public void NewByShortcut()
        {
            if (CanMakeFromModel)
            {
                NewFromModelCommand.Execute(null);
            }
            else
            {
                NewDrawing();
            }
        }

        private void OnNewFromModelChanged()
        {
            ((DelegateCommand)NewFromModelCommand).RaiseCanExecuteChanged();
            Raise(nameof(CanMakeFromModel));
            Raise(nameof(StartScreenText));
            Raise(nameof(RecentHistory));
        }

        /// <summary>Gets the extensions added, in the order they were added; their ribbon tabs follow Home in that order.</summary>
        /// <remarks>Each read gives a new list, so that a window bound to it before an extension was added shows its tab.</remarks>
        public IReadOnlyList<CadWorkspaceExtension> Extensions => _extensions.ToList().AsReadOnly();

        /// <summary>Gets or sets the extension whose ribbon tab is shown; null for Home. A tab hidden while shown gives Home back.</summary>
        public CadWorkspaceExtension SelectedTab
        {
            get => _selectedTab;
            set
            {
                if (
                    value != null
                    && ((!_extensions.Contains(value) && !_builtInTabs.Contains(value)) || !value.IsTabVisible)
                )
                {
                    value = null;
                }

                if (Set(ref _selectedTab, value))
                {
                    foreach (CadWorkspaceExtension extension in _extensions.Concat(_builtInTabs))
                    {
                        extension.IsTabSelected = extension == value;
                    }

                    Raise(nameof(IsHomeTabSelected));
                }
            }
        }

        /// <summary>Gets whether the ribbon shows Home.</summary>
        public bool IsHomeTabSelected => _selectedTab == null;

        /// <summary>Gets the text of the banner on top of Properties, from an extension that owns the selection; null for none.</summary>
        public string PropertiesBanner
        {
            get => _banner;
            private set
            {
                if (Set(ref _banner, value))
                {
                    Raise(nameof(HasPropertiesBanner));
                }
            }
        }

        /// <summary>Gets whether Properties shows a banner.</summary>
        public bool HasPropertiesBanner => !string.IsNullOrEmpty(_banner);

        /// <summary>Gets the command that raises <see cref="NewFromModelRequested"/> (Ctrl+N, the start screen's button).</summary>
        public ICommand NewFromModelCommand { get; }

        /// <summary>Gets the command that shows a ribbon tab, given its extension; null for Home.</summary>
        public ICommand SelectTabCommand { get; }

        /// <summary>
        /// Adds an extension: its commands go into the command line of every document, open or opened later, and its ribbon tab,
        /// file words, Properties rows and status text show from then on.
        /// </summary>
        /// <param name="extension">The extension.</param>
        /// <exception cref="ArgumentNullException">Thrown when the extension is null.</exception>
        public void AddExtension(CadWorkspaceExtension extension)
        {
            if (extension == null)
            {
                throw new ArgumentNullException(nameof(extension));
            }

            if (_extensions.Contains(extension))
            {
                return;
            }

            _extensions.Add(extension);
            extension.PropertyChanged += OnExtensionChanged;

            foreach (KnownFile file in _files.Values.Where(f => f.Session != null))
            {
                extension.AddCommands(file.Session.Registry);
            }

            Raise(nameof(Extensions));
            RefreshFromExtensions();
        }

        /// <summary>
        /// Reads again what the extensions say of the files, the selection and the status, as after one of their documents
        /// changed state (made again, found out of date).
        /// </summary>
        public void RefreshFromExtensions()
        {
            RebuildRows();
            UpdateProperties();
            RaiseModified();
        }

        /// <summary>Raised when the user asks to open a file, by the Open button of the list or of the start screen; the window shows its dialog.</summary>
        public event EventHandler OpenRequested;

        /// <summary>Raised when a drawing with no file of its own is saved; the window asks where, then calls <see cref="SaveAs"/>.</summary>
        public event EventHandler SaveAsRequested;

        /// <summary>Raised by the ribbon's Layer Properties and by LAYER; the window shows the Layer manager over <see cref="CreateLayerManager"/>.</summary>
        public event EventHandler LayerManagerRequested;

        /// <summary>Raised by PROPERTIES; the window shows the Properties panel.</summary>
        public event EventHandler PropertiesRequested;

        /// <summary>Gets the rows of the DRAWINGS list as filtered: folder headers and files.</summary>
        public ObservableCollection<FileRowViewModel> FileRows { get; } = new ObservableCollection<FileRowViewModel>();

        /// <summary>Gets or sets the text the list is filtered by: the files whose name holds it, in either case.</summary>
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (Set(ref _searchText, value ?? string.Empty))
                {
                    RebuildRows();
                }
            }
        }

        /// <summary>Gets or sets whether the list shows every file (the chip All).</summary>
        public bool FilterAll
        {
            get => !_dxfOnly && !_ownOnly && !_outOfDateOnly;
            set => SetFilter(false, false, false);
        }

        /// <summary>Gets or sets whether the list shows the DXF files only (the chip DXF), not those an extension describes.</summary>
        public bool FilterDxf
        {
            get => _dxfOnly;
            set => SetFilter(value, false, false);
        }

        /// <summary>Gets or sets whether the list shows only the files an extension describes (the chip "Model drawings").</summary>
        public bool FilterOwn
        {
            get => _ownOnly;
            set => SetFilter(false, value, false);
        }

        /// <summary>Gets or sets whether the list shows only the files an extension says are out of date (the chip "Out of date").</summary>
        public bool FilterOutOfDate
        {
            get => _outOfDateOnly;
            set => SetFilter(false, false, value);
        }

        /// <summary>Gets the text of the chip All, with the number of files: "All 3".</summary>
        public string AllChipText => "All " + Count(Listed().Count());

        /// <summary>Gets the text of the chip DXF, with the number of DXF files no extension describes: "DXF 3".</summary>
        public string DxfChipText => "DXF " + Count(Listed().Count(f => f.IsDxf && f.Info(_extensions) == null));

        /// <summary>Gets whether an extension is added that names its files, so that its chips show.</summary>
        public bool HasOwnFiles => _extensions.Any(e => !string.IsNullOrEmpty(e.FileKindName));

        /// <summary>Gets the text of the chip of the files extensions describe, with their number: "Model drawings 4".</summary>
        public string OwnChipText =>
            (_extensions.Select(e => e.FileKindName).FirstOrDefault(n => !string.IsNullOrEmpty(n)) ?? "Other")
            + " "
            + Count(_files.Values.Count(f => f.Info(_extensions) != null));

        /// <summary>Gets the text of the chip of the files out of date, with their number: "Out of date 1".</summary>
        public string OutOfDateChipText =>
            "Out of date " + Count(_files.Values.Count(f => f.Info(_extensions)?.State == CadFileState.OutOfDate));

        /// <summary>Gets whether a drawing is shown; false on the start screen.</summary>
        public bool HasDocument => _current != null;

        /// <summary>Gets the document shown; null on the start screen.</summary>
        public CadDocument Document => _current?.Document;

        /// <summary>Gets the command line of the document shown; null on the start screen.</summary>
        public CadCommandSession Session => _current?.Session;

        /// <summary>Gets the object snaps, ortho, polar and grid snap points are taken through, which the status toggles set.</summary>
        public ObjectSnap Snap { get; }

        /// <summary>Gets the space shown.</summary>
        public CadSpace Space => _current?.Document.CurrentSpace;

        /// <summary>Gets the view of the space shown, in the canvas's device pixels.</summary>
        public CadView View => _current?.ViewOf(_current.Document.CurrentSpace, _canvasWidth, _canvasHeight).View;

        /// <summary>Gets a number that changes whenever the canvas must draw again: the view or the drawing changed.</summary>
        public int Revision
        {
            get => _revision;
            private set => Set(ref _revision, value);
        }

        /// <summary>Gets the text of the title bar: the file, "*" when it has unsaved changes, the layout shown after it.</summary>
        public string Title
        {
            get
            {
                if (_current == null)
                {
                    return string.Empty;
                }

                string title =
                    (_current.Info(_extensions)?.Title ?? _current.Document.Title)
                    + (HasUnsavedChanges ? "*" : string.Empty);
                CadSpace space = Space;
                return space == null || space.IsModel ? title : title + " · " + space.Name;
            }
        }

        /// <summary>Gets the tabs under the canvas: Model and each layout.</summary>
        public ObservableCollection<SpaceTabViewModel> SpaceTabs { get; } =
            new ObservableCollection<SpaceTabViewModel>();

        /// <summary>Gets whether model space is shown.</summary>
        public bool IsModelSpace => Space?.IsModel ?? false;

        /// <summary>Gets MODEL or PAPER, as the status bar shows: MODEL on the Model tab and through a viewport of a layout; empty with no drawing.</summary>
        public string SpaceLabel =>
            _current == null ? string.Empty
            : IsWorkingInModel ? "MODEL"
            : "PAPER";

        /// <summary>Gets the label at the top left of the canvas, <see cref="ModelViewportLabel"/> in model space and empty in a layout.</summary>
        public string ViewportLabel =>
            Document?.BlockEditor is CadBlockEditor editor ? "[-][Block Editor: " + editor.Name + "][2D Wireframe]"
            : IsModelSpace ? ModelViewportLabel
            : string.Empty;

        /// <summary>Gets the text of the selection box of Properties: "No selection", "Line (1)", "Circle (2)".</summary>
        public string SelectionLabel
        {
            get => _selectionLabel;
            private set => Set(ref _selectionLabel, value);
        }

        /// <summary>Gets the groups of rows Properties shows, their values edited in place.</summary>
        public IReadOnlyList<PropertyGroupViewModel> PropertyGroups
        {
            get => _groups;
            private set => Set(ref _groups, value);
        }

        /// <summary>Gets the layers of the drawing shown, by name.</summary>
        public IReadOnlyList<CadLayerInfo> Layers
        {
            get => _layers;
            private set => Set(ref _layers, value);
        }

        /// <summary>Gets or sets the current layer, as the layer box of the ribbon sets it; a frozen layer is refused, said on the command line.</summary>
        public CadLayerInfo CurrentLayer
        {
            get
            {
                if (_current == null)
                {
                    return null;
                }

                string name = _current.Document.Database.Clayer;
                return _layers.FirstOrDefault(l => string.Equals(l.Name, name, StringComparison.OrdinalIgnoreCase));
            }
            set
            {
                if (_current == null || value == null || ReferenceEquals(value, CurrentLayer))
                {
                    return;
                }

                CadEditResult result = CadLayerEditor.SetCurrent(_current.Document, value.Name);

                if (!result.Succeeded)
                {
                    Say(result.Message);
                }

                Raise(nameof(CurrentLayer));
            }
        }

        /// <summary>Gets every line the command line has written, the oldest first.</summary>
        public ObservableCollection<string> History { get; } = new ObservableCollection<string>();

        /// <summary>
        /// Gets the last three lines of <see cref="History"/>, those the command line shows above its input; before any,
        /// <see cref="WelcomeText"/> where a host makes drawings from a model and <see cref="NewDrawingWelcomeText"/> where none does.
        /// </summary>
        public IReadOnlyList<string> RecentHistory =>
            History.Count == 0
                ? new[] { CanMakeFromModel ? WelcomeText : NewDrawingWelcomeText }
                : History.Skip(Math.Max(0, History.Count - HistoryShown)).ToList();

        /// <summary>Gets the prompt of the command running, empty with none.</summary>
        public string Prompt => Session?.PromptText ?? string.Empty;

        /// <summary>Gets whether a command is running.</summary>
        public bool IsCommandRunning => Session?.IsRunning ?? false;

        /// <summary>Gets whether the prompt waiting takes text with spaces in it, so that Space types a space rather than ending the line.</summary>
        public bool PromptTakesSpaces => Session?.Prompt?.Kind == CadPromptKind.String;

        /// <summary>Gets or sets the text typed in the command line; emptied, it closes the AutoComplete list.</summary>
        public string CommandText
        {
            get => _commandText;
            set
            {
                if (!Set(ref _commandText, value ?? string.Empty))
                {
                    return;
                }

                // With the AutoComplete list open it follows what is typed at once; emptied, it closes.
                if (_commandText.Length == 0)
                {
                    CloseSuggestions();
                }
                else if (IsSuggestionListOpen)
                {
                    ShowSuggestions();
                }
            }
        }

        /// <summary>Gets the coordinates of the cursor as the status bar shows them: "X 1 234.50   Y 567.80".</summary>
        public string CursorText
        {
            get => _cursorText;
            private set => Set(ref _cursorText, value);
        }

        /// <summary>Gets whether the canvas shows its dot grid (GRID, F7).</summary>
        public bool GridOn => Toggles.First(t => t.Name == "GRID").IsOn;

        /// <summary>Gets the toggles of the status bar: GRID, SNAP, ORTHO, POLAR, OTRACK and OSNAP.</summary>
        public ObservableCollection<StatusToggleViewModel> Toggles { get; }

        /// <summary>Gets whether the drawing shown differs from its file.</summary>
        public bool HasUnsavedChanges => _current?.Document.IsModified ?? false;

        /// <summary>
        /// Gets the state text of the status bar: "Saved" or "Unsaved changes", or what an extension says of a document of its
        /// own; empty with no drawing.
        /// </summary>
        public string StatusText =>
            _current == null
                ? string.Empty
                : BlockEditorStatus
                    ?? _current.Info(_extensions)?.StatusText
                    ?? (HasUnsavedChanges ? "Unsaved changes" : "Saved");

        /// <summary>Gets a second state text an extension gives for a document of its own, drawn as a warning; empty for none.</summary>
        public string StatusNote => _current?.Info(_extensions)?.StatusNote ?? string.Empty;

        /// <summary>Gets the command that zooms to the extents of the space shown (the ribbon's Zoom Extents).</summary>
        public ICommand ZoomExtentsCommand { get; }

        /// <summary>Gets the command that runs PAN (the ribbon's Pan).</summary>
        public ICommand PanCommand { get; }

        /// <summary>Gets the command that asks the window to show its Open dialog.</summary>
        public ICommand OpenCommand { get; }

        /// <summary>Gets the command that looks again for the DXF files of the folders listed.</summary>
        public ICommand RefreshCommand { get; }

        /// <summary>Gets the command that opens or shows the file of a row of the list, given the row.</summary>
        public ICommand ActivateFileCommand { get; }

        /// <summary>Gets the command that shows the space of a tab, given the tab.</summary>
        public ICommand SelectSpaceCommand { get; }

        /// <summary>Gets the command that starts a command of the session by its name, given the name: the ribbon's buttons.</summary>
        public ICommand StartCommandCommand { get; }

        /// <summary>Gets the command that undoes the last step (Ctrl+Z).</summary>
        public ICommand UndoCommand { get; }

        /// <summary>Gets the command that redoes the step undone last (Ctrl+Y).</summary>
        public ICommand RedoCommand { get; }

        /// <summary>Gets the command that saves the drawing (Ctrl+S).</summary>
        public ICommand SaveCommand { get; }

        /// <summary>Gets the command that runs LAYER, which shows the Layer manager (the ribbon's Layer Properties).</summary>
        public ICommand LayerManagerCommand { get; }

        /// <summary>Gets the command that makes the layer of a selected object current (Make Current, LAYMCUR).</summary>
        public ICommand MakeCurrentCommand { get; }

        /// <summary>Gets the command that moves objects to the layer of another (Match Layer, LAYMCH).</summary>
        public ICommand MatchLayerCommand { get; }

        /// <summary>
        /// Opens a DXF file, or shows it again where it is open already: opened from it, or shown under a key of its own and
        /// saved to it, as an extension's drawing just made is. It lists the other DXF files of its folder. A file that
        /// cannot be read leaves the drawing shown as it was and says why on the command line. Each extension is told of the
        /// document before it is shown (<see cref="CadWorkspaceExtension.DocumentOpened"/>). Where
        /// <see cref="OpensInBackground"/> is set, a file not open yet is opened as <see cref="OpenAsync"/> opens it.
        /// </summary>
        /// <param name="path">The path of the file.</param>
        /// <returns>True when the file is shown, or, opened in the background, when its opening has started.</returns>
        public bool Open(string path) => Open(path, OpensInBackground, out _);

        /// <summary>
        /// <see cref="Open(string)"/>, a file not open yet read and drawn in the background or at once; the opening in the
        /// background it started or joined, null for none.
        /// </summary>
        private bool Open(string path, bool inBackground, out Opening opening)
        {
            opening = null;

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
                Say("Cannot open " + path + ": " + ex.Message);
                return false;
            }

            if (_files.TryGetValue(full, out KnownFile known) && known.Document != null)
            {
                Show(known);
                NoteRecentFile(full);
                return true;
            }

            // A document shown under a key of its own and saved to the file, as an extension's drawing just made is, is that file open already.
            KnownFile saved = _files
                .Values.Where(f => f.Document != null && SamePath(f.Document.FileName, full))
                .OrderBy(f => f.Path, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();

            if (saved != null)
            {
                Show(saved);
                NoteRecentFile(full);
                return true;
            }

            if (inBackground)
            {
                return StartOpening(full, out opening);
            }

            CadDocument document;

            try
            {
                document = CadDocument.Open(full);
            }
            catch (Exception ex) when (!(ex is OutOfMemoryException))
            {
                return OpenRecovered(full, ex);
            }

            foreach (CadWorkspaceExtension extension in _extensions)
            {
                extension.DocumentOpened(document);
            }

            return ShowOpened(full, known, document, null);
        }

        /// <summary>
        /// Shows a document just opened from a file, its extensions told already: listed under the file, its folder's files
        /// listed, the lines its fonts said while it was drawn on a worker thread written, then the line that it is open.
        /// </summary>
        private bool ShowOpened(string full, KnownFile known, CadDocument document, IReadOnlyList<string> said)
        {
            known = known ?? new KnownFile(full, System.IO.Path.GetDirectoryName(full));
            Attach(known, document);
            _files[full] = known;
            ListFolder(known.Folder);
            Show(known);

            foreach (string line in said ?? Array.Empty<string>())
            {
                Say(line);
            }

            Say(
                "Opened "
                    + document.Title
                    + ": "
                    + Count(document.EntityCount)
                    + " entities, "
                    + Count(document.Layers.Count)
                    + " layers."
            );
            NoteRecentFile(full);
            OfferMissingFonts(document);
            return true;
        }

        /// <summary>Whether a document's file name is the full path of a file; a document with no file of its own has none.</summary>
        private static bool SamePath(string fileName, string full) =>
            !string.IsNullOrEmpty(fileName)
            && System.IO.Path.IsPathRooted(fileName)
            && string.Equals(fileName, full, StringComparison.OrdinalIgnoreCase);

        /// <summary>Shows a document already in memory, as a host that made the drawing itself shows it.</summary>
        /// <param name="document">The document.</param>
        public void OpenDocument(CadDocument document) => OpenDocument(document, null);

        /// <summary>
        /// Shows a document already in memory under a key of its own in the list, so that two documents of one name are both
        /// listed; a document given again under its key replaces the one shown there, as a drawing made again does. A document
        /// listed already, under its file since it was saved there, is shown in its row, not listed twice; and a file of a
        /// folder that a document listed under a key of its own is saved to has no row of its own while that document is open.
        /// </summary>
        /// <param name="document">The document.</param>
        /// <param name="key">The key; null for its file name or title.</param>
        public void OpenDocument(CadDocument document, string key)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            key = key ?? document.FileName ?? document.Title;
            string folder =
                string.IsNullOrEmpty(document.FileName) || !System.IO.Path.IsPathRooted(document.FileName)
                    ? "Open drawings"
                    : System.IO.Path.GetDirectoryName(document.FileName);

            if (!_files.TryGetValue(key, out KnownFile known) || known.Document != document)
            {
                // A document listed already under another key, as one saved since under its file, is shown there.
                KnownFile listed = _files
                    .Values.Where(f => f.Document == document)
                    .OrderBy(f => f.Path, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(f => f.Path, StringComparer.Ordinal)
                    .FirstOrDefault();

                if (listed != null)
                {
                    known = listed;
                }
                else
                {
                    // The document shown under the key before is closed: its command line lets the plugins go.
                    if (known?.Session != null)
                    {
                        DetachPlugins(known.Session.Registry);
                    }

                    known = new KnownFile(key, folder);
                    Attach(known, document);
                    _files[key] = known;
                }
            }

            Show(known);
            Say(
                "Opened "
                    + document.Title
                    + ": "
                    + Count(document.EntityCount)
                    + " entities, "
                    + Count(document.Layers.Count)
                    + " layers."
            );
        }

        /// <summary>Looks again for the DXF files of the folders listed.</summary>
        public void Refresh()
        {
            foreach (
                string folder in _files.Values.Select(f => f.Folder).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
            )
            {
                ListFolder(folder);
            }

            RebuildRows();
        }

        /// <summary>Opens or shows the file of a row of the list; a header does nothing.</summary>
        /// <param name="row">The row.</param>
        public void Activate(FileRowViewModel row)
        {
            if (row == null || row.IsHeader || row.Path == null)
            {
                return;
            }

            if (_files.TryGetValue(row.Path, out KnownFile known) && known.Document != null)
            {
                Show(known);
            }
            else
            {
                Open(row.Path);
            }
        }

        /// <summary>Shows a space of the document: model space or a layout. The command running ends and the selection, which belongs to a space, is cleared.</summary>
        /// <param name="space">The space.</param>
        public void SelectSpace(CadSpace space)
        {
            if (
                _current == null
                || space == null
                || space == _current.Document.CurrentSpace
                || !_current.Document.Spaces.Contains(space)
                || _current.Document.BlockEditor != null
            )
            {
                return;
            }

            _current.Session.Escape();
            _current.Document.CurrentSpace = space;
            _current.Document.Selection.Clear();

            foreach (SpaceTabViewModel tab in SpaceTabs)
            {
                tab.IsActive = tab.Space == space;
            }

            RaiseSpace();
            SyncHistory();
        }

        /// <summary>Picks at a point of the drawing with no command running, as a click does: see <see cref="ClickAt"/>.</summary>
        /// <param name="x">The X of the point.</param>
        /// <param name="y">The Y of the point.</param>
        /// <param name="tolerance">The greatest distance in drawing units; the canvas gives 5 pixels over the zoom.</param>
        /// <param name="toggle">Whether Shift is held.</param>
        /// <returns>The handle picked, or null.</returns>
        public string PickAt(double x, double y, double tolerance, bool toggle)
        {
            return _current == null ? null : CadPicking.Pick(_current.Document, Space, x, y, tolerance, toggle);
        }

        /// <summary>
        /// A left click on the canvas at a point of the drawing, as the canvas makes it. With a command waiting for a point it
        /// takes the point <see cref="Snap"/> gives within the aperture; at "Select objects:" it picks the object there; with no
        /// command running the entity nearest within the aperture is added to the selection, or added or removed with
        /// <paramref name="shift"/>, and a click on nothing clears the selection.
        /// </summary>
        /// <param name="point">The point under the cursor.</param>
        /// <param name="aperture">How near, in drawing units: the canvas gives 5 pixels over the zoom to pick, 10 to snap.</param>
        /// <param name="shift">Whether Shift is held.</param>
        /// <returns>Whether the selection or the command changed.</returns>
        public bool ClickAt(GeoPoint2 point, double aperture, bool shift = false)
        {
            if (_current == null)
            {
                return false;
            }

            _current.Session.Cursor = point;
            bool changed = CadPicking.Click(_current.Document, Space, _current.Session, Snap, point, aperture, shift);
            SyncHistory();
            return changed;
        }

        /// <summary>
        /// A box dragged on the canvas: a window takes the entities wholly inside it, a crossing those it touches. With no
        /// command running what it takes is added to the selection, or removed with <paramref name="remove"/> (Shift); at
        /// "Select objects:" it answers the prompt.
        /// </summary>
        /// <param name="box">The box in drawing units.</param>
        /// <param name="crossing">Whether it is a crossing box (dragged right to left).</param>
        /// <param name="remove">Whether Shift is held.</param>
        /// <returns>The handles the box took.</returns>
        public IReadOnlyList<string> SelectBox(DisplayBox box, bool crossing, bool remove)
        {
            if (_current == null)
            {
                return Array.Empty<string>();
            }

            IReadOnlyList<string> found = CadPicking.BoxDone(
                _current.Document,
                Space,
                _current.Session,
                box,
                crossing,
                remove
            );
            SyncHistory();
            return found;
        }

        /// <summary>Gets the point a click at a point of the drawing would take now, and which snap gives it; null with no point prompt waiting.</summary>
        /// <param name="point">The point under the cursor.</param>
        /// <param name="aperture">How near, in drawing units.</param>
        /// <returns>The snap, or null.</returns>
        public CadSnapResult SnapAt(GeoPoint2 point, double aperture)
        {
            if (_current == null || !CadPicking.WantsPoint(_current.Session))
            {
                return null;
            }

            return CadPicking.Snap(
                _current.Document,
                Space,
                Snap,
                point,
                aperture,
                _current.Session.Prompt.BasePoint,
                _current.Session.Prompt.SnapFrom
            );
        }

        /// <summary>Selects nothing.</summary>
        public void ClearSelection() => _current?.Document.Selection.Clear();

        /// <summary>
        /// Esc: ends the command running, saying *Cancel*; with none running, clears the selection. With the options menu of
        /// dynamic input open it closes the menu alone, and the command goes on; while a drawing is opening in the background,
        /// it stops the opening alone (<see cref="CancelOpening"/>).
        /// </summary>
        public void Escape()
        {
            if (IsDynamicInputMenuOpen)
            {
                CloseDynamicInputMenu();
                return;
            }

            // Esc throws away what is typed on the command line, the AutoComplete list with it, as AutoCAD's does.
            CommandText = string.Empty;
            CloseSuggestions();

            if (CancelOpening())
            {
                return;
            }

            if (_current == null)
            {
                return;
            }

            if (_current.Session.IsRunning)
            {
                _current.Session.Escape();
                SyncHistory();
            }
            else
            {
                // At the Command prompt Esc says so, as AutoCAD's does.
                _current.Session.Write("*Cancel*");
                SyncHistory();
                ClearSelection();
            }
        }

        /// <summary>
        /// Enter or Space with nothing typed: the prompt's default or the end of the selection; with no command running, the last
        /// command again. A field of dynamic input locked by Tab gives the point instead (<see cref="TabDynamicInput"/>).
        /// </summary>
        public void Enter()
        {
            if (!TakeDynamicInput(string.Empty))
            {
                Execute(string.Empty);
            }
        }

        /// <summary>
        /// Runs what is typed in the command line, then empties it. While the fields of dynamic input are shown, a number typed
        /// into one, or a field locked by Tab, gives the point with the cursor's value in the other; a coordinate typed whole,
        /// "@300&lt;0" or "10,20", goes to the command line as typed.
        /// </summary>
        public void SubmitCommand()
        {
            string text = CommandText;
            CommandText = string.Empty;
            CloseSuggestions();
            _recall = -1;

            if (!TakeDynamicInput(text))
            {
                Execute(text);
            }
        }

        /// <summary>
        /// Runs a line of the command line through the session: a command's name or alias, or the answer to the prompt waiting.
        /// Words parted by spaces go in one by one, as Space ends each in AutoCAD, so that "ZOOM E" zooms to the extents; a
        /// prompt for text takes the line whole. An empty line is Enter.
        /// </summary>
        /// <param name="input">The line.</param>
        public void Execute(string input)
        {
            if (RefusedWhileOpening(input))
            {
                return;
            }

            string line = input ?? string.Empty;

            if (line.Trim().Length > 0)
            {
                _inputs.Add(line.Trim());
            }

            if (_current == null)
            {
                if (line.Trim().Length > 0)
                {
                    Say("Command: " + line.Trim().ToUpperInvariant());

                    // NEW needs no drawing open: it makes one.
                    if (string.Equals(line.Trim(), "NEW", StringComparison.OrdinalIgnoreCase))
                    {
                        NewDrawing();
                    }
                    else if (!_extensions.Any(e => e.TryRunWithoutDocument(line.Trim())))
                    {
                        Say("No drawing is open: Ctrl+O opens a DXF.");
                    }
                }

                return;
            }

            CadCommandSession session = _current.Session;

            if (line.Trim().Length > 0 && !session.IsRunning)
            {
                NoteCommandTyped(session.Registry, line);
            }

            if (line.Trim().Length == 0)
            {
                session.Enter();
            }
            else
            {
                if (PromptTakesSpaces)
                {
                    session.Feed(line);
                }
                else
                {
                    foreach (string word in line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        if (!TakeSnapOverride(session, word))
                        {
                            session.Feed(word);
                        }
                    }
                }
            }

            SyncHistory();
            RunAfterCommand();
        }

        /// <summary>Starts a command by its name, as a button of the ribbon does, ending any command running first.</summary>
        /// <param name="name">The name: LINE, MOVE, DIMLINEAR.</param>
        public void StartCommand(string name)
        {
            if (_current == null || string.IsNullOrWhiteSpace(name) || RefusedWhileOpening(name))
            {
                return;
            }

            string known = _current.Session.Registry.Find(name)?.Name
                ?? _current.Session.Registry.FindVariable(name)?.Name;

            if (known != null)
            {
                NoteCommandUsed(known);
            }

            _current.Session.Start(name);
            SyncHistory();
            RunAfterCommand();
        }

        /// <summary>Erases the selected objects, as the Delete key does; nothing with none selected or a command running.</summary>
        public void EraseSelection()
        {
            if (_current != null && !_current.Session.IsRunning && _current.Document.Selection.Count > 0)
            {
                StartCommand("ERASE");
            }
        }

        /// <summary>Undoes the last step (Ctrl+Z), ending any command running first.</summary>
        /// <returns>Whether a step was undone.</returns>
        public bool Undo()
        {
            if (_current == null)
            {
                return false;
            }

            // The same path as U typed on the command line: the session's command, which ends any command running first.
            CadUndoStep step = _current.Document.NextUndo;
            StartCommand("U");
            return step != null && !ReferenceEquals(step, _current.Document.NextUndo);
        }

        /// <summary>Redoes the step undone last (Ctrl+Y).</summary>
        /// <returns>Whether a step was redone.</returns>
        public bool Redo()
        {
            if (_current == null)
            {
                return false;
            }

            // The same path as REDO typed on the command line.
            CadUndoStep step = _current.Document.NextRedo;
            StartCommand("REDO");
            return step != null && !ReferenceEquals(step, _current.Document.NextRedo);
        }

        /// <summary>Saves the drawing to its file (Ctrl+S); one with no file yet raises <see cref="SaveAsRequested"/>.</summary>
        /// <returns>Whether it was saved.</returns>
        public bool Save()
        {
            if (_current == null)
            {
                return false;
            }

            if (!_current.Document.HasFile)
            {
                if (SaveAsRequested == null)
                {
                    Say("The drawing has no file yet: save it with Save As.");
                    return false;
                }

                SaveAsRequested(this, EventArgs.Empty);
                return !HasUnsavedChanges;
            }

            return SaveAs(_current.Document.FileName);
        }

        /// <summary>
        /// Saves the drawing to a file, which it is then known by in the list. The file of another document open, a model
        /// drawing saved as B-101.dxf among them, is refused until that document is closed.
        /// </summary>
        /// <param name="path">The path of the file.</param>
        /// <returns>Whether it was saved; why not is said on the command line.</returns>
        public bool SaveAs(string path)
        {
            if (_current == null || string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            _current.Session.Escape();
            string old = _current.Path;

            try
            {
                // A file another open document has, under its file or under a key of its own, is not written over: that document
                // would leave the list unasked, and the two be saved to one file.
                string target = System.IO.Path.GetFullPath(path);
                bool taken = _files.Values.Any(f =>
                    f != _current
                    && f.Document != null
                    && f.Document != _current.Document
                    && (
                        string.Equals(f.Path, target, StringComparison.OrdinalIgnoreCase)
                        || (
                            f.Document.HasFile
                            && string.Equals(
                                System.IO.Path.GetFullPath(f.Document.FileName),
                                target,
                                StringComparison.OrdinalIgnoreCase
                            )
                        )
                    )
                );

                if (taken)
                {
                    Say(System.IO.Path.GetFileName(target) + " is open as another drawing; close it first.");
                    return false;
                }

                _current.Document.SaveAs(path);
            }
            catch (Exception ex)
                when (ex is IOException
                    || ex is UnauthorizedAccessException
                    || ex is ArgumentException
                    || ex is NotSupportedException
                )
            {
                Say("Cannot save " + path + ": " + ex.Message);
                return false;
            }

            string full = _current.Document.FileName;

            if (!string.Equals(old, full, StringComparison.OrdinalIgnoreCase))
            {
                _files.Remove(old);
                _current.Path = full;
                _current.Folder = System.IO.Path.GetDirectoryName(full);

                if (_files.TryGetValue(full, out KnownFile other) && other.Document != null)
                {
                    other.Document.Changed -= OnDocumentChanged;
                    other.Document.ActiveViewportChanged -= OnActiveViewportChanged;
                }

                _files[full] = _current;
                ListFolder(_current.Folder);
                RebuildRows();
            }

            Say("Saved " + full + ".");
            RaiseModified();
            return true;
        }

        /// <summary>Makes the view model of the Layer manager over the drawing shown; null with none. Dispose it when the dialog closes.</summary>
        /// <returns>The view model.</returns>
        public LayerManagerViewModel CreateLayerManager() =>
            _current == null ? null : new LayerManagerViewModel(_current.Document, Say);

        /// <summary>Puts the line typed before the one shown into the command line, as Up does.</summary>
        public void RecallPrevious()
        {
            if (_inputs.Count == 0)
            {
                return;
            }

            _recall = _recall < 0 ? _inputs.Count - 1 : Math.Max(0, _recall - 1);
            CommandText = _inputs[_recall];
        }

        /// <summary>Puts the line typed after the one shown into the command line, as Down does; past the last, empties it.</summary>
        public void RecallNext()
        {
            if (_recall < 0)
            {
                return;
            }

            _recall++;

            if (_recall >= _inputs.Count)
            {
                _recall = -1;
                CommandText = string.Empty;
            }
            else
            {
                CommandText = _inputs[_recall];
            }
        }

        /// <summary>Zooms the view of the space shown to its extents, the sheet of a layout among them, as double-clicking the middle button does.</summary>
        public void ZoomExtents()
        {
            if (_current == null)
            {
                return;
            }

            ViewState state = _current.ViewOf(Space, _canvasWidth, _canvasHeight);
            state.View.ZoomTo(CadPicking.Extents(_current.Document, Space));
            state.FitPending = true;
            Revision++;
            RefreshViewGroup();
        }

        /// <summary>
        /// Tells the view model the canvas's size in device pixels: every view takes it, and a view still fitted to its
        /// extents is fitted again.
        /// </summary>
        /// <param name="width">The width in pixels.</param>
        /// <param name="height">The height in pixels.</param>
        public void SetCanvasSize(double width, double height)
        {
            if (width < 1.0 || height < 1.0)
            {
                return;
            }

            _canvasWidth = width;
            _canvasHeight = height;
            bool redraw = false;

            foreach (KnownFile file in _files.Values)
            {
                foreach (KeyValuePair<CadSpace, ViewState> pair in file.Views)
                {
                    pair.Value.View.Resize(width, height);

                    if (pair.Value.FitPending)
                    {
                        pair.Value.View.ZoomTo(CadPicking.Extents(file.Document, pair.Key));
                        redraw |= file == _current;
                    }
                }
            }

            if (redraw)
            {
                Revision++;
                RefreshViewGroup();
            }
        }

        /// <summary>Tells the view model the user zoomed or panned the view shown, so a new canvas size no longer refits it.</summary>
        public void NotifyViewChangedByUser()
        {
            if (_current != null)
            {
                _current.ViewOf(Space, _canvasWidth, _canvasHeight).FitPending = false;
            }

            RefreshViewGroup();
        }

        /// <summary>Copies into <see cref="History"/> what the session has written since, as after a grip drag the canvas made.</summary>
        public void RefreshHistory() => SyncHistory();

        /// <summary>Sets the coordinates the status bar shows, in the current UCS, as AutoCAD's: the world point (100, 50) in a UCS of origin (100, 0) shows "X 0.00   Y 50.00".</summary>
        /// <param name="x">The X of the drawing point under the cursor, in world coordinates.</param>
        /// <param name="y">The Y.</param>
        public void SetCursor(double x, double y) => SetCursor(x, y, new GeoPoint2(x, y));

        /// <summary>
        /// Sets the coordinates the status bar shows, as <see cref="SetCursor(double, double)"/> does, and the point the rubber band
        /// ends at, which the fields of dynamic input measure to: with an endpoint snapped at (1 300, 1 400) and the cursor a
        /// few pixels off it, LINE's next point from (1 000, 1 000) shows a length of 500 and an angle of 53.1.
        /// </summary>
        /// <param name="x">The X of the drawing point under the cursor, in world coordinates.</param>
        /// <param name="y">The Y.</param>
        /// <param name="bandEnd">Where the rubber band ends, snapped, ortho or polar, in world coordinates; the cursor where nothing holds it.</param>
        public void SetCursor(double x, double y, GeoPoint2 bandEnd)
        {
            _bandAt = bandEnd;
            var cursor = new GeoPoint2(x, y);
            GeoPoint2 shown = Ucs.ToUcs(cursor);
            DxfDatabase database = _current?.Document.Database;

            // A drawing in units other than decimal (LUNITS 2) shows them as AutoCAD does, 1'-3 1/2"; decimal keeps the settings' style.
            CursorText =
                database != null && database.Lunits != 2
                    ? "X "
                        + CadUnits.Length(shown.X, database.Lunits, database.Luprec)
                        + "   Y "
                        + CadUnits.Length(shown.Y, database.Lunits, database.Luprec)
                    : "X "
                        + _settings.FormatNumber(shown.X, _settings.CoordinateDecimals)
                        + "   Y "
                        + _settings.FormatNumber(shown.Y, _settings.CoordinateDecimals);
        }

        /// <summary>Formats a whole number as the design writes it, thousands set apart by a space: 2 418.</summary>
        /// <param name="value">The number.</param>
        /// <returns>The text.</returns>
        public static string Count(int value) => value.ToString("#,0", Spaced);

        private static string Coordinates(double x, double y) =>
            "X " + x.ToString("#,0.00", Spaced) + "   Y " + y.ToString("#,0.00", Spaced);

        /// <summary>The commands of the session: AutoCAD's of C2, Make Current and Match Layer of the Layers panel, the window's commands and variables, and the extensions'.</summary>
        private CadCommandRegistry Commands()
        {
            CadCommandRegistry registry = CadCommandRegistry.CreateDefault();
            registry.Add(new CadCommand("LAYMCUR", MakeCurrent, true));
            registry.Add(new CadCommand("LAYMCH", MatchLayer, true));
            registry.Add(new CadCommand("CLOSE", CloseCommandBody));
            AddWindowCommands(registry);
            AddWindowVariables(registry);
            AttachPlugins(registry);

            foreach (CadWorkspaceExtension extension in _extensions)
            {
                extension.AddCommands(registry);
            }

            ApplyShortcuts(registry);

            return registry;
        }

        /// <summary>LAYMCUR: the layer of the object selected, or of one picked, becomes current.</summary>
        private static IEnumerable<CadPrompt> MakeCurrent(CadCommandContext c)
        {
            string handle = null;

            foreach (string selected in c.Document.Selection.Handles.OrderBy(h => h, StringComparer.OrdinalIgnoreCase))
            {
                handle = selected;
                break;
            }

            if (handle == null)
            {
                yield return CadPrompt.ForEntity("Select object whose layer will become current");

                if (c.Input.Kind != CadInputKind.Selection)
                {
                    yield break;
                }

                handle = c.Input.Handles[0];
            }

            DxfEntity entity = c.Document.FindEntity(handle);

            if (entity == null)
            {
                yield break;
            }

            c.OpenUndoStep();
            CadEditResult result = CadLayerEditor.SetCurrent(c.Document, entity.Layer);
            c.Write(result.Succeeded ? entity.Layer + " is now the current layer." : result.Message);
            c.ClearsSelection = true;
        }

        /// <summary>LAYMCH: the objects selected go onto the layer of an object picked after.</summary>
        private static IEnumerable<CadPrompt> MatchLayer(CadCommandContext c)
        {
            foreach (CadPrompt prompt in c.SelectObjects())
            {
                yield return prompt;
            }

            if (c.Selected.Count == 0)
            {
                yield break;
            }

            yield return CadPrompt.ForEntity("Select object on destination layer");

            if (c.Input.Kind != CadInputKind.Selection)
            {
                yield break;
            }

            DxfEntity target = c.Document.FindEntity(c.Input.Handles[0]);

            if (target == null)
            {
                yield break;
            }

            c.OpenUndoStep();
            CadEditResult result = CadPropertyEditor.Set(c.Document, c.Selected, "Layer", target.Layer);
            c.Write(
                result.Succeeded
                    ? Count(c.Selected.Count) + " object(s) changed to layer \"" + target.Layer + "\"."
                    : result.Message
            );
        }

        private void Attach(KnownFile file, CadDocument document)
        {
            file.Document = document;
            file.Drafting = ReadDrafting(document);
            file.Session = new CadCommandSession(document, Commands());
            file.Session.PromptChanged += OnPromptChanged;
            file.Session.UiRequested += OnUiRequested;
            file.Session.Clipboard = _clipboard;
            ApplyTextDefaults(file.Session);
            document.Fonts = _settings.ToFonts(SkiaCanvasRenderer.HasFamily);
            document.SupportFolders = _settings.SupportSearchFolders;
            document.FontMissing += (s, e) => Say(e.Message);
            document.Changed += OnDocumentChanged;
            document.ActiveViewportChanged += OnActiveViewportChanged;
            WatchReferences(document);
        }

        internal void Say(string line)
        {
            if (_current != null)
            {
                _current.Session.Write(line);
                SyncHistory();
            }
            else
            {
                History.Add(line);
                Raise(nameof(RecentHistory));
            }
        }

        private void SyncHistory()
        {
            if (_current == null)
            {
                return;
            }

            IReadOnlyList<string> lines = _current.Session.History;

            if (_current.Synced >= lines.Count)
            {
                return;
            }

            for (int i = _current.Synced; i < lines.Count; i++)
            {
                History.Add(lines[i]);
            }

            _current.Synced = lines.Count;
            Raise(nameof(RecentHistory));
        }

        private void OnPromptChanged(object sender, EventArgs e)
        {
            if (_current == null || sender != _current.Session)
            {
                return;
            }

            // An object snap taken for one point (END typed, the Shift+right-click menu) ends with the prompt it was taken at, and
            // so do the points acquired for tracking and the object clicked first for an apparent intersection.
            // The temporary track points (TT) given for the point asked stay acquired until it is given.
            Snap.Override = null;
            Snap.ClearAcquired();

            // POLARMODE 1 counts the polar angles from the last segment drawn, a line of the LINE running among them.
            Snap.LastSegment = _current.Session.LastSegment;

            foreach (GeoPoint2 tracked in _current.Session.TemporaryTrackPoints)
            {
                Snap.AcquireTrackPoint(tracked);
            }

            SyncHistory();
            Raise(nameof(Prompt));
            Raise(nameof(IsCommandRunning));
            Raise(nameof(PromptTakesSpaces));

            // ZOOM and PAN move the view; a view the user moved is no longer refitted to a new canvas size.
            if (
                !_current.Session.IsRunning
                && (_current.Session.LastCommandName == "ZOOM" || _current.Session.LastCommandName == "PAN")
            )
            {
                NotifyViewChangedByUser();
            }

            // A UCS changed is a change of the drawing, as in AutoCAD, though not a step of undo: the title and the list show it.
            if (!_current.Session.IsRunning && _current.Session.LastCommandName == "UCS")
            {
                RaiseModified();
            }

            // LAYOUT Set shows another space from the command line: the tabs follow, as a tab clicked.
            if (SpaceTabs.Any(t => t.IsActive != (t.Space == _current.Document.CurrentSpace)))
            {
                foreach (SpaceTabViewModel tab in SpaceTabs)
                {
                    tab.IsActive = tab.Space == _current.Document.CurrentSpace;
                }

                RaiseSpace();
            }

            SyncInPlace();
            SyncTableCell();
            SyncBlockEditor();
            Revision++;

            // PICKSTYLE typed or switched by its key: Group Selection follows. A split button's option answers its prompt.
            Raise(nameof(IsGroupSelectionOn));
            AnswerPendingOption();
        }

        private void OnUiRequested(object sender, CadUiRequestEventArgs e)
        {
            switch (e.Name)
            {
                case "PROPERTIES":
                    IsPropertiesPanelShown = true;
                    PropertiesRequested?.Invoke(this, EventArgs.Empty);
                    e.Handled = true;
                    break;
                case "LAYER":
                    if (LayerManagerRequested != null)
                    {
                        LayerManagerRequested(this, EventArgs.Empty);
                        e.Handled = true;
                    }

                    break;
                case "OPTIONS":
                    if (_settingsRequested != null)
                    {
                        RequestSettings(null);
                    }
                    else
                    {
                        ((CadCommandSession)sender).Write("Global settings are shown by the window.");
                    }

                    e.Handled = true;
                    break;
                case "QSAVE":
                    if (SaveAsRequested != null)
                    {
                        SaveAsRequested(this, EventArgs.Empty);
                        e.Handled = true;
                    }

                    break;
                case "RECOVER":
                    ShowRecovered(e);
                    break;
                case "LINETYPE":
                case "HATCHSETTINGS":
                case "HATCHEDIT":
                    OnHatchOrLinetypeRequested(e);
                    break;
                case "STYLE":
                case "DIMSTYLE":
                case "TABLESTYLE":
                case "MLEADERSTYLE":
                    OnStyleRequest(e);
                    break;
                case "TABLE":
                    OnInsertTableRequested(e);
                    break;
                case "PTYPE":
                    OnPointStyleRequested(e);
                    break;
                case "BEDIT":
                case "BSAVEAS":
                case "BCLOSE":
                    OnBlockEditorRequested(e);
                    break;
                case "BLOCK":
                case "INSERT":
                case "WBLOCK":
                case "ATTDEF":
                case "EATTEDIT":
                case "REDEFINE":
                    OnBlockRequested(e);
                    break;
                case "BATTMAN":
                case "ATTDIA":
                case "PURGE":
                case "PURGEITEM":
                case "CLASSICGROUP":
                case "LAYERSTATE":
                    OnManagerRequested(e);
                    break;
                case "VIEW":
                case "UNITS":
                    OnViewsRequested(e);
                    break;
                case "PASTESPEC":
                    OnPasteSpecialRequested(e);
                    break;
                case "MATCHPROP":
                    OnMatchPropRequested(sender, e);
                    break;
                case "PLOT":
                case "PAGESETUP":
                    OnPlotRequested(sender, e);
                    break;
                case "PREVIEW":
                    OnPreviewRequested(sender, e);
                    break;
                case "-PLOT":
                    OnPlotFileRequested(sender, e);
                    break;
                case "PNGOUT":
                case "JPGOUT":
                case "BMPOUT":
                    OnImageFileRequested(e);
                    break;
                case "EXPORT":
                case "-EXPORT":
                    OnExportFileRequested(e);
                    break;
                case "QSELECT":
                    OnQuickSelectRequested(e);
                    break;
                case "FIND":
                    OnFindRequested(e);
                    break;
                case "XREF":
                case "XOPEN":
                case "XATTACH":
                case "IMAGEATTACH":
                    OnXrefRequested(e);
                    break;
            }
        }

        private void OnDocumentChanged(object sender, CadDocumentChangedEventArgs e)
        {
            KnownFile file = _files.Values.FirstOrDefault(f => f.Document == sender);

            if (file == null)
            {
                return;
            }

            FileRowViewModel row = FileRows.FirstOrDefault(r =>
                string.Equals(r.Path, file.Path, StringComparison.OrdinalIgnoreCase)
            );

            if (row != null)
            {
                CadFileInfo info = file.Info(_extensions);
                row.HasUnsavedChanges = file.Document.IsModified;
                row.Detail = info?.Detail ?? file.Detail();
                row.State = StateOf(file, info);
            }

            if (file != _current)
            {
                return;
            }

            SyncBlockEditor();
            SyncSpaceTabs();

            if (e.TablesChanged || e.Kind == CadChangeKind.Undo || e.Kind == CadChangeKind.Redo)
            {
                Layers = SortedLayers(file.Document);
                Raise(nameof(CurrentLayer));
            }

            RaiseCurrentColor();
            RaiseCurrentLinetype();
            RaiseStyles();
            UpdateProperties();
            RaiseModified();
            SyncTableCell();

            if (e.Kind != CadChangeKind.Saved)
            {
                Revision++;
            }
        }

        private void RaiseModified()
        {
            Raise(nameof(Title));
            Raise(nameof(HasUnsavedChanges));
            Raise(nameof(StatusText));
            Raise(nameof(StatusNote));

            foreach (FileRowViewModel row in FileRows)
            {
                if (!row.IsHeader && _files.TryGetValue(row.Path, out KnownFile file))
                {
                    row.HasUnsavedChanges = file.Document != null && file.Document.IsModified;
                    row.State = StateOf(file, file.Info(_extensions));
                }
            }
        }

        /// <summary>The dot of a file: what its extension says, else edited while it has unsaved changes.</summary>
        private static CadFileState StateOf(KnownFile file, CadFileInfo info)
        {
            if (info != null)
            {
                return info.State;
            }

            return file.Document != null && file.Document.IsModified ? CadFileState.Edited : CadFileState.None;
        }

        private void OnExtensionChanged(object sender, PropertyChangedEventArgs e)
        {
            if (
                e.PropertyName == nameof(CadWorkspaceExtension.IsTabVisible)
                && sender == _selectedTab
                && !_selectedTab.IsTabVisible
            )
            {
                SelectedTab = null;
            }
        }

        /// <summary>The View group of Properties, shown with nothing selected, read again after the view moved.</summary>
        private void RefreshViewGroup()
        {
            if (_current != null && _current.Document.Selection.Count == 0)
            {
                UpdateProperties();
            }
        }

        private void OnToggleChanged(object sender, PropertyChangedEventArgs e)
        {
            ApplyToggles();

            // SNAP, GRID and ORTHO switched are written to the drawing too, as AutoCAD saves SNAPMODE, GRIDMODE and ORTHOMODE with it.
            if (sender is StatusToggleViewModel toggle)
            {
                WriteDraftingToggle(toggle);
            }
        }

        private void ApplyToggles()
        {
            bool On(string name) => Toggles.First(t => t.Name == name).IsOn;
            Snap.IsOn = On("OSNAP");
            Snap.Ortho = On("ORTHO");
            Snap.Polar = On("POLAR");
            Snap.Tracking = On("OTRACK");
            Snap.GridSnap = On("SNAP");
            Raise(nameof(GridOn));
        }

        private void SetFilter(bool dxf, bool own, bool outOfDate)
        {
            if (_dxfOnly == dxf && _ownOnly == own && _outOfDateOnly == outOfDate)
            {
                return;
            }

            _dxfOnly = dxf;
            _ownOnly = own;
            _outOfDateOnly = outOfDate;
            Raise(nameof(FilterAll));
            Raise(nameof(FilterDxf));
            Raise(nameof(FilterOwn));
            Raise(nameof(FilterOutOfDate));
            RebuildRows();
        }

        private void ListFolder(string folder)
        {
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
            {
                return;
            }

            string[] found;

            try
            {
                found = Directory.GetFiles(folder, "*.dxf");
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                return;
            }

            foreach (string path in found)
            {
                string full = System.IO.Path.GetFullPath(path);

                // The copies autosave keeps beside a file are not listed.
                if (IsAutosaveCopy(full))
                {
                    continue;
                }

                if (!_files.ContainsKey(full))
                {
                    _files[full] = new KnownFile(full, folder);
                }
            }
        }

        private static IReadOnlyList<CadLayerInfo> SortedLayers(CadDocument document)
        {
            var layers = document.Layers.ToList();
            layers.Sort(
                (a, b) =>
                {
                    int order = string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
                    return order != 0 ? order : string.CompareOrdinal(a.Name, b.Name);
                }
            );
            return layers;
        }

        private void Show(KnownFile file)
        {
            CadDocument old = _current?.Document;

            if (old != null && old != file.Document)
            {
                old.Selection.Changed -= OnSelectionChanged;
                _current.Session.Escape();
            }

            if (old != file.Document)
            {
                file.Document.Selection.Changed += OnSelectionChanged;
            }

            _current = file;
            LoadDrafting();
            SpaceTabs.Clear();

            foreach (CadSpace space in TabSpaces(file.Document))
            {
                SpaceTabs.Add(new SpaceTabViewModel(space) { IsActive = space == file.Document.CurrentSpace });
            }

            Layers = SortedLayers(file.Document);
            RebuildRows();
            Raise(nameof(HasDocument));
            Raise(nameof(Document));
            Raise(nameof(Session));
            Raise(nameof(CurrentLayer));
            RaiseCurrentColor();
            RaiseCurrentLinetype();
            RaiseStyles();
            Raise(nameof(Prompt));
            Raise(nameof(IsCommandRunning));
            RaiseModified();
            RaiseSpace();
            ((DelegateCommand)StartCommandCommand).RaiseCanExecuteChanged();
            _startCommandWithOptionCommand?.RaiseCanExecuteChanged();
            Raise(nameof(IsGroupSelectionOn));
            SyncHistory();
            SyncInPlace();
            SyncBlockEditor();
        }

        private void RaiseSpace()
        {
            ShowPendingView();
            _current.Session.View = View;
            Raise(nameof(Space));
            Raise(nameof(View));
            Raise(nameof(Title));
            Raise(nameof(IsModelSpace));
            Raise(nameof(SpaceLabel));
            Raise(nameof(ViewportLabel));
            UpdateProperties();
            Revision++;
        }

        private void OnSelectionChanged(object sender, EventArgs e) => UpdateProperties();

        private void UpdateProperties()
        {
            if (_current == null)
            {
                SelectionLabel = string.Empty;
                PropertyGroups = Array.Empty<PropertyGroupViewModel>();
                PropertiesBanner = null;
                return;
            }

            var handles = _current
                .Document.Selection.Handles.Where(h => _current.Document.FindEntity(h) != null)
                .ToList();
            handles.Sort(StringComparer.OrdinalIgnoreCase);
            CadSelectionInfo info = CadProperties.Describe(_current.Document, Space, handles, View);
            string banner = null;

            foreach (CadWorkspaceExtension extension in _extensions)
            {
                info = extension.DescribeSelection(_current.Document, handles, info, out string said) ?? info;
                banner = said ?? banner;
            }

            PropertiesBanner = banner;
            var groups = new List<PropertyGroupViewModel>();

            foreach (CadPropertyGroup group in info.Groups)
            {
                var rows = new List<PropertyRowViewModel>();

                foreach (CadPropertyRow row in group.Rows)
                {
                    // A dynamic block's property is set by its name, whatever it is named, through its own lists.
                    if (group.Title == CadProperties.CustomGroup)
                    {
                        rows.Add(
                            new PropertyRowViewModel(row, row.Choices, (r, text) =>
                                CommitCustomProperty(handles, r.Label, text)
                            )
                        );
                        continue;
                    }

                    // An MTEXT's contents are edited in its editor, as the palette's button opens it.
                    IReadOnlyList<string> options =
                        row.Label == "Contents" && info.Label == "MText (1)"
                            ? new[] { EditMTextOption }
                            : OptionsOf(row.Label) ?? row.Choices;
                    rows.Add(
                        new PropertyRowViewModel(row, options, (r, text) => CommitProperty(handles, r.Label, text))
                    );
                }

                groups.Add(new PropertyGroupViewModel(group.Title, rows));
            }

            SelectionLabel = info.Label;
            PropertyGroups = groups;
            RaiseViewportScale();
        }

        /// <summary>The values a row's drop-down offers: the layers, the colours by name, the linetypes, AutoCAD's lineweights, a viewport's standard scales and Yes or No for its display locked.</summary>
        private IReadOnlyList<string> OptionsOf(string label)
        {
            switch (label)
            {
                case "Layer":
                    return _layers.Select(l => l.Name).ToList();
                case "Color":
                    return ColorOptions;
                case "Linetype":
                    var linetypes = new List<string> { "ByLayer", "ByBlock" };

                    foreach (DxfLinetypeTableRecord linetype in _current.Document.Database.LinetypeTable)
                    {
                        if (!linetypes.Contains(linetype.Name, StringComparer.OrdinalIgnoreCase))
                        {
                            linetypes.Add(linetype.Name);
                        }
                    }

                    return linetypes;
                case "Lineweight":
                    return WeightOptions;
                case "Standard scale":
                    return CadViewports.StandardScales;
                case "Display locked":
                    return new[] { "Yes", "No" };
                default:
                    return null;
            }
        }

        private bool CommitProperty(IReadOnlyCollection<string> handles, string label, string text)
        {
            if (_current == null)
            {
                return false;
            }

            if (label == "Color" && text == SelectColorOption)
            {
                return PickPropertyColor(handles);
            }

            if (label == "Contents" && text == EditMTextOption)
            {
                if (handles.Count == 1)
                {
                    EditTextAt(handles.First());
                }

                return false;
            }

            CadEditResult result = CadPropertyEditor.Set(_current.Document, handles, label, text);

            if (!result.Succeeded)
            {
                Say(result.Message);
            }

            return result.Succeeded;
        }

        /// <summary>
        /// "Select Color..." of the Color row: AutoCAD's Select Color dialog on the colour of the selection, ByLayer and ByBlock
        /// offered, and the colour chosen given to it as one undo step. The row shows the colour the selection then has.
        /// </summary>
        private bool PickPropertyColor(IReadOnlyCollection<string> handles)
        {
            PropertyRowViewModel row = PropertyGroups.SelectMany(g => g.Rows).FirstOrDefault(r => r.Label == "Color");
            SelectColorViewModel.TryParse(row?.Value, out DxfColor old);
            var dialog = new SelectColorViewModel(old ?? DxfColor.ByLayer, true);

            if (PickColor == null)
            {
                Say("The Select Color dialog is shown by the window.");
            }
            else if (PickColor(dialog) && (old == null || !dialog.Color.Equals(old)))
            {
                CadEditResult result = CadPropertyEditor.Set(
                    _current.Document,
                    handles,
                    "Color",
                    dialog.Color.ToString()
                );

                if (!result.Succeeded)
                {
                    Say(result.Message);
                }
            }

            return false;
        }

        private void RebuildRows()
        {
            FileRows.Clear();

            // A file an extension describes is listed under the group it names, after the folders of plain DXF files.
            var shown = Listed()
                .Select(f => new { File = f, Info = f.Info(_extensions) })
                .Where(f =>
                    (!_dxfOnly || (f.File.IsDxf && f.Info == null))
                    && (!_ownOnly || f.Info != null)
                    && (!_outOfDateOnly || f.Info?.State == CadFileState.OutOfDate)
                    && (
                        _searchText.Length == 0
                        || (f.Info?.Label ?? f.File.Name).IndexOf(_searchText, StringComparison.OrdinalIgnoreCase) >= 0
                        || (f.Info?.Detail ?? string.Empty).IndexOf(_searchText, StringComparison.OrdinalIgnoreCase)
                            >= 0
                    )
                )
                .GroupBy(
                    f => (f.Info == null ? "0" : "1") + (f.Info == null ? f.File.Folder : f.Info.Group),
                    StringComparer.OrdinalIgnoreCase
                )
                .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
                .ThenBy(g => g.Key, StringComparer.Ordinal);

            foreach (var group in shown)
            {
                string key = group.Key.Substring(1);
                FileRows.Add(new FileRowViewModel(true, group.Key[0] == '0' ? key + " · DXF" : key, null, null));

                foreach (
                    var item in group
                        .OrderBy(f => f.Info?.Label ?? f.File.Name, StringComparer.OrdinalIgnoreCase)
                        .ThenBy(f => f.Info?.Label ?? f.File.Name, StringComparer.Ordinal)
                        .ThenBy(f => f.File.Path, StringComparer.Ordinal)
                )
                {
                    FileRows.Add(
                        new FileRowViewModel(
                            false,
                            item.Info?.Label ?? item.File.Name,
                            item.File.Path,
                            item.Info?.Detail ?? item.File.Detail()
                        )
                        {
                            IsCurrent = item.File == _current,
                            HasUnsavedChanges = item.File.Document != null && item.File.Document.IsModified,
                            Right = item.Info?.Right,
                            State = StateOf(item.File, item.Info),
                            IsOpen = item.File.Document != null,
                        }
                    );
                }
            }

            RestoreFileSelection();
            Raise(nameof(AllChipText));
            Raise(nameof(DxfChipText));
            Raise(nameof(HasOwnFiles));
            Raise(nameof(OwnChipText));
            Raise(nameof(OutOfDateChipText));
        }

        /// <summary>
        /// The files the list shows: all but a file found in a folder that a document listed under another key is saved to,
        /// as a model drawing just made is saved; the document's row stands for it.
        /// </summary>
        private IEnumerable<KnownFile> Listed()
        {
            var saved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (KnownFile file in _files.Values)
            {
                if (
                    file.Document != null
                    && !string.IsNullOrEmpty(file.Document.FileName)
                    && System.IO.Path.IsPathRooted(file.Document.FileName)
                )
                {
                    saved.Add(file.Document.FileName);
                }
            }

            return _files.Values.Where(f => f.Document != null || !saved.Contains(f.Path));
        }

        /// <summary>The view of one space and whether it is still fitted to the space's extents.</summary>
        private sealed class ViewState
        {
            public ViewState(CadView view)
            {
                View = view;
            }

            public CadView View { get; }

            public bool FitPending { get; set; } = true;
        }

        /// <summary>A file of the list: open, with a document, its command line and the view of each space shown, or only found in a folder.</summary>
        private sealed class KnownFile
        {
            public KnownFile(string path, string folder)
            {
                Path = path;
                Folder = folder ?? string.Empty;
            }

            public string Path { get; set; }

            public string Folder { get; set; }

            public string Name
            {
                get
                {
                    string name = System.IO.Path.GetFileName(Path);
                    return string.IsNullOrEmpty(name) ? Path : name;
                }
            }

            public bool IsDxf => Name.EndsWith(".dxf", StringComparison.OrdinalIgnoreCase) || Document != null;

            public CadDocument Document { get; set; }

            public CadCommandSession Session { get; set; }

            /// <summary>The drawing's SNAP, GRID, ORTHO, lineweights and spacings, shown while it is.</summary>
            public DraftingState Drafting { get; set; } = new DraftingState();

            /// <summary>How many lines of the session's history <see cref="MainViewModel.History"/> holds.</summary>
            public int Synced { get; set; }

            public Dictionary<CadSpace, ViewState> Views { get; } = new Dictionary<CadSpace, ViewState>();

            /// <summary>What the first extension that knows the file's document says of it; null for a file none knows.</summary>
            public CadFileInfo Info(IReadOnlyList<CadWorkspaceExtension> extensions)
            {
                if (Document == null)
                {
                    return null;
                }

                foreach (CadWorkspaceExtension extension in extensions)
                {
                    CadFileInfo info = extension.DescribeFile(Document);

                    if (info != null)
                    {
                        return info;
                    }
                }

                return null;
            }

            public ViewState ViewOf(CadSpace space, double width, double height)
            {
                if (!Views.TryGetValue(space, out ViewState state))
                {
                    var view = new CadView(width, height);
                    view.ZoomTo(CadPicking.Extents(Document, space));
                    state = new ViewState(view);
                    Views[space] = state;
                }

                return state;
            }

            public string Detail()
            {
                if (Document != null)
                {
                    int layouts = Document.Spaces.Count - 1;
                    return "DXF · "
                        + Count(Document.EntityCount)
                        + " entities · "
                        + Count(layouts)
                        + (layouts == 1 ? " layout" : " layouts");
                }

                try
                {
                    var info = new FileInfo(Path);
                    return info.Exists
                        ? "DXF · " + Count((int)Math.Max(1, (info.Length + 1023) / 1024)) + " KB"
                        : "DXF";
                }
                catch (Exception ex)
                    when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException)
                {
                    return "DXF";
                }
            }
        }
    }

    /// <summary>The dot of a file in the DRAWINGS list.</summary>
    public enum CadFileState
    {
        /// <summary>No dot: a DXF saved as it is.</summary>
        None,

        /// <summary>Blue: changed since it was saved, or edited by hand since it was made.</summary>
        Edited,

        /// <summary>Amber: made from a model that has changed since.</summary>
        OutOfDate,

        /// <summary>Green: made from the model as it is.</summary>
        UpToDate,
    }

    /// <summary>
    /// How the DRAWINGS list, the title bar and the status bar show a document an extension knows: the group it is listed
    /// under, its label and line of detail, the text at its right, its dot, the title and the state texts.
    /// </summary>
    /// <remarks>
    /// A drawing B-101 of the beam B1 made from Tekla is <c>new CadFileInfo("Tekla 2025 · model drawings", "B-101", "Beam B1 · rev B", "A2",
    /// CadFileState.Edited, "B-101 · Beam B1", "Source: Tekla 2025 · connected", "1 drawing out of date")</c>.
    /// </remarks>
    public sealed class CadFileInfo
    {
        /// <summary>Makes what the list says of a document.</summary>
        /// <param name="group">The header it is listed under.</param>
        /// <param name="label">Its name in the list.</param>
        /// <param name="detail">The line under the name; null for none.</param>
        /// <param name="right">The short text at the right of the row, as the sheet size; null for none.</param>
        /// <param name="state">Its dot.</param>
        /// <param name="title">The title bar's text for it; null for the document's title.</param>
        /// <param name="statusText">The status bar's state text; null for "Saved" or "Unsaved changes".</param>
        /// <param name="statusNote">A second state text, drawn as a warning; null for none.</param>
        public CadFileInfo(
            string group,
            string label,
            string detail,
            string right,
            CadFileState state,
            string title = null,
            string statusText = null,
            string statusNote = null
        )
        {
            Group = group ?? string.Empty;
            Label = label ?? string.Empty;
            Detail = detail;
            Right = right;
            State = state;
            Title = title;
            StatusText = statusText;
            StatusNote = statusNote;
        }

        /// <summary>Gets the header the document is listed under.</summary>
        public string Group { get; }

        /// <summary>Gets its name in the list.</summary>
        public string Label { get; }

        /// <summary>Gets the line under the name; null for none.</summary>
        public string Detail { get; }

        /// <summary>Gets the short text at the right of the row; null for none.</summary>
        public string Right { get; }

        /// <summary>Gets its dot.</summary>
        public CadFileState State { get; }

        /// <summary>Gets the title bar's text for it; null for the document's title.</summary>
        public string Title { get; }

        /// <summary>Gets the status bar's state text; null for the presenter's own.</summary>
        public string StatusText { get; }

        /// <summary>Gets a second state text, drawn as a warning; null for none.</summary>
        public string StatusNote { get; }
    }

    /// <summary>
    /// What a host adds to the presenter without the presenter knowing it (A16: Part B adds model drawings this way): a ribbon
    /// tab after Home, shown while <see cref="IsTabVisible"/>; commands on the command line of every document; words for its
    /// own documents in the DRAWINGS list, the title and the status bar; rows and a banner in Properties. It is added with
    /// <see cref="MainViewModel.AddExtension"/>; every member has a default that adds nothing.
    /// </summary>
    public abstract class CadWorkspaceExtension : ObservableObject
    {
        private bool _isTabVisible;
        private bool _isTabSelected;
        private object _tabContent;

        /// <summary>Gets the name of the ribbon tab it adds, as "Drawing"; null for none.</summary>
        public virtual string TabHeader => null;

        /// <summary>Gets the name of the chip of the DRAWINGS list for its documents, as "Model drawings"; null for none.</summary>
        public virtual string FileKindName => null;

        /// <summary>Gets or sets what its ribbon tab shows: a WPF element, or a view model the window has a template for.</summary>
        public object TabContent
        {
            get => _tabContent;
            set => Set(ref _tabContent, value);
        }

        /// <summary>Gets whether its ribbon tab is shown now, as the Drawing tab only while a model drawing is.</summary>
        public bool IsTabVisible
        {
            get => _isTabVisible;
            protected set => Set(ref _isTabVisible, value);
        }

        /// <summary>Gets whether its ribbon tab is the one shown; <see cref="MainViewModel.SelectedTab"/> sets it.</summary>
        public bool IsTabSelected
        {
            get => _isTabSelected;
            internal set => Set(ref _isTabSelected, value);
        }

        /// <summary>Adds its commands and their aliases to the command line of a document, as the document is opened.</summary>
        /// <param name="registry">The registry of the document's command line.</param>
        public virtual void AddCommands(CadCommandRegistry registry) { }

        /// <summary>Runs a command typed with no document open, as DRAWINGNEW; false for one it does not know.</summary>
        /// <param name="typed">The name or alias typed.</param>
        /// <returns>Whether it ran it.</returns>
        public virtual bool TryRunWithoutDocument(string typed) => false;

        /// <summary>Says how the list shows a document of its own; null for one it does not know.</summary>
        /// <param name="document">The document.</param>
        /// <returns>What the list shows, or null.</returns>
        public virtual CadFileInfo DescribeFile(CadDocument document) => null;

        /// <summary>
        /// Changes what Properties shows of the selection of a document of its own: the groups and the label, and the banner on
        /// top; the description as given, and no banner, for a document it does not know.
        /// </summary>
        /// <param name="document">The document.</param>
        /// <param name="handles">The handles selected, in ordinal order; empty for none.</param>
        /// <param name="shown">What Properties shows otherwise.</param>
        /// <param name="banner">The banner; null for none.</param>
        /// <returns>What Properties shows.</returns>
        public virtual CadSelectionInfo DescribeSelection(
            CadDocument document,
            IReadOnlyList<string> handles,
            CadSelectionInfo shown,
            out string banner
        )
        {
            banner = null;
            return shown;
        }

        /// <summary>
        /// Tells the extension a DXF file has been opened (Open, or a row of the file list), before it is shown, so that it can
        /// take a document of its own back: a model drawing saved and opened again registers its owner here.
        /// </summary>
        /// <param name="document">The document opened.</param>
        public virtual void DocumentOpened(CadDocument document) { }

        /// <summary>Tells the extension a document has been closed (CLOSE), so that it lets go of what it kept of it.</summary>
        /// <param name="document">The document closed.</param>
        public virtual void DocumentClosed(CadDocument document) { }
    }
}
