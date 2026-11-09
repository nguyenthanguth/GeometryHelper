using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// A file of Recent Documents, as the application menu and the start screen list it: its name, folder and when it was
    /// opened, its pin, and, read only once asked for, its version, date, size and preview picture.
    /// </summary>
    public sealed class RecentDocumentViewModel : ObservableObject
    {
        private readonly Func<DateTime> _now;
        private bool _read;
        private string _versionText;
        private ImageSource _thumbnail;

        internal RecentDocumentViewModel(GlobalRecentFile file, Func<DateTime> now, Action<RecentDocumentViewModel> open, Action<RecentDocumentViewModel> pin)
        {
            Path = file.Path;
            IsPinned = file.IsPinned;
            Opened = file.Opened;
            _now = now;
            OpenCommand = new DelegateCommand(_ => open(this));
            PinCommand = new DelegateCommand(_ => pin(this));
        }

        /// <summary>Gets the full path of the file.</summary>
        public string Path { get; }

        /// <summary>Gets the name of the file, as plan-level2.dxf.</summary>
        public string Name => System.IO.Path.GetFileName(Path);

        /// <summary>Gets whether the file is pinned.</summary>
        public bool IsPinned { get; }

        /// <summary>Gets whether it is the first file not pinned after pinned ones, drawn below a rule in the application menu.</summary>
        public bool IsFirstAfterPinned { get; internal set; }

        /// <summary>Gets "Unpin" or "Pin", what the pin button does.</summary>
        public string PinText => IsPinned ? "Unpin" : "Pin";

        /// <summary>Gets when the file was last opened, in UTC.</summary>
        public DateTime Opened { get; }

        /// <summary>Gets when the file was last opened, in words: "today 09:14", "yesterday 16:40", or the date, "2026-10-30".</summary>
        public string OpenedText
        {
            get
            {
                DateTime local = Opened.ToLocalTime();
                DateTime today = _now().Date;
                string time = local.ToString("HH:mm", CultureInfo.InvariantCulture);
                return local.Date == today ? "today " + time
                    : local.Date == today.AddDays(-1) ? "yesterday " + time
                    : local.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            }
        }

        /// <summary>Gets the version of the file in words, as "AutoCAD 2018 DXF (AC1032)"; read from the top of the file the first time it is asked for.</summary>
        public string VersionText
        {
            get
            {
                Read();
                return _versionText;
            }
        }

        /// <summary>Gets when the file was last written, as "2026-11-10 09:14"; "not found" where the file is gone.</summary>
        public string ModifiedText => File.Exists(Path) ? File.GetLastWriteTime(Path).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) : "not found";

        /// <summary>Gets the size of the file, as "1.2 MB" or "412 KB"; empty where the file is gone.</summary>
        public string SizeText
        {
            get
            {
                if (!File.Exists(Path))
                {
                    return string.Empty;
                }

                long bytes = new FileInfo(Path).Length;
                return bytes >= 1024 * 1024 ? (bytes / (1024.0 * 1024.0)).ToString("0.0", CultureInfo.InvariantCulture) + " MB"
                    : Math.Max(1L, (bytes + 1023) / 1024).ToString(CultureInfo.InvariantCulture) + " KB";
            }
        }

        /// <summary>Gets the preview picture the file keeps (its THUMBNAILIMAGE section), read from its end the first time it is asked for; null for none.</summary>
        public ImageSource Thumbnail
        {
            get
            {
                Read();
                return _thumbnail;
            }
        }

        /// <summary>Gets whether the file keeps a preview picture.</summary>
        public bool HasThumbnail => Thumbnail != null;

        /// <summary>Gets the command that opens the file.</summary>
        public ICommand OpenCommand { get; }

        /// <summary>Gets the command that pins or unpins the file.</summary>
        public ICommand PinCommand { get; }

        /// <inheritdoc/>
        public override string ToString() => Name;

        private void Read()
        {
            if (_read)
            {
                return;
            }

            _read = true;
            CadFileHeader header = CadFileHeader.Read(Path);
            _versionText = header.VersionText;

            if (header.Thumbnail == null)
            {
                return;
            }

            try
            {
                using (var stream = new MemoryStream(header.Thumbnail))
                {
                    BitmapFrame frame = BitmapFrame.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                    frame.Freeze();
                    _thumbnail = frame;
                }
            }
            catch (Exception exception) when (exception is NotSupportedException || exception is FileFormatException || exception is ArgumentException || exception is InvalidOperationException)
            {
                GeometryHelperLog.Debug("RecentDocumentViewModel: the preview of " + Path + " does not read: " + exception.Message);
            }
        }
    }

    /// <summary>
    /// An item of the application menu: a command of its left column (New, Open, Save, Save As, Export, Print, Close), or an
    /// entry of the list one of them shows (PNG, Plot, Page Setup), with its words, icon and what it runs.
    /// </summary>
    public sealed class AppMenuItemViewModel : ObservableObject
    {
        private bool _isShown;

        internal AppMenuItemViewModel(string key, string name, string iconKey, string description, Action run, bool isEnabled = true, IReadOnlyList<AppMenuItemViewModel> items = null)
        {
            Key = key;
            Name = name ?? string.Empty;
            IconKey = iconKey;
            Description = description ?? string.Empty;
            IsEnabled = isEnabled && (run != null || (items != null && items.Any(i => i.IsEnabled)));
            Items = items ?? Array.Empty<AppMenuItemViewModel>();
            Command = new DelegateCommand(_ => (run ?? Items.FirstOrDefault(i => i.IsEnabled)?.Run)?.Invoke(), _ => IsEnabled);
            Run = run;
        }

        /// <summary>Gets the key of the item, as "export"; null for an entry of a list.</summary>
        public string Key { get; }

        /// <summary>Gets the words, as "Save As" or "PNG"; empty for a rule between groups.</summary>
        public string Name { get; }

        /// <summary>Gets its icon: a key of the icon set, as GhIconSaveAs, or path data on a grid of 24; null for none.</summary>
        public string IconKey { get; }

        /// <summary>Gets the line under an entry's name, as "An image of the drawing area as shown (PNGOUT)".</summary>
        public string Description { get; }

        /// <summary>Gets whether it can be chosen; an entry not built yet is shown greyed.</summary>
        public bool IsEnabled { get; }

        /// <summary>Gets whether it is a rule between groups.</summary>
        public bool IsSeparator => Name.Length == 0;

        /// <summary>Gets whether its list is the one shown at the right, the item then drawn chosen.</summary>
        public bool IsShown
        {
            get => _isShown;
            internal set => Set(ref _isShown, value);
        }

        /// <summary>Gets the entries of the list it shows where the pointer rests on it; empty for an item with none.</summary>
        public IReadOnlyList<AppMenuItemViewModel> Items { get; }

        /// <summary>Gets whether it shows a list, drawn with ▸.</summary>
        public bool HasItems => Items.Count > 0;

        /// <summary>Gets what choosing it does: its own action, or the first entry of its list that can be chosen.</summary>
        public ICommand Command { get; }

        internal Action Run { get; }

        /// <inheritdoc/>
        public override string ToString() => Name;
    }

    /// <summary>
    /// The application menu of the G button, as AutoCAD 2021's: a search box for the commands, the column of New, Open, Save,
    /// Save As, Export, Print and Close, and at the right Recent Documents, or the list of the item the pointer rests on;
    /// Options and Exit at the foot.
    /// </summary>
    /// <remarks>
    /// Export's PDF runs PLOT, whose device is GeometryHelper PDF; PNG, JPG and BMP run PNGOUT, JPGOUT and BMPOUT; Block or
    /// objects to a DXF runs WBLOCK. Entries the presenter has not built (Drawing Template, Other Formats, Batch Plot, Manage
    /// Plot Styles) are listed greyed, so that the menu matches AutoCAD's.
    /// </remarks>
    public sealed class AppMenuViewModel : ObservableObject
    {
        // The icons of the design (AppMenu), as path data on a grid of 24.
        private const string IconNew = "M14 3H7a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h10a2 2 0 0 0 2-2V8zM14 3v5h5M12 11v6M9 14h6";
        private const string IconOpen = "M3 7a2 2 0 0 1 2-2h4l2 2h8a2 2 0 0 1 2 2v8a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z";
        private const string IconSave = "M5 3h11l3 3v15H5zM8 3v5h7V3M8 21v-7h8v7";
        private const string IconSaveAs = "M5 3h11l3 3v7M8 3v5h7V3M5 3v18h7M15 21l6-6-2-2-6 6v2z";
        private const string IconExport = "M14 3H7a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h4M14 3v5h5v4M15 18h7M19 15l3 3-3 3";
        private const string IconPrint = "M7 9V3h10v6M7 17H5a2 2 0 0 1-2-2v-4a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2v4a2 2 0 0 1-2 2h-2M7 14h10v7H7z";
        private const string IconClose = "M14 3H7a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h10a2 2 0 0 0 2-2V8zM14 3v5h5M9.5 12.5l5 5M14.5 12.5l-5 5";
        private const string IconDxf = "M14 3H7a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h10a2 2 0 0 0 2-2V8zM14 3v5h5M12 11v6M9 14l3 3 3-3";
        private const string IconPdf = "M14 3H7a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h10a2 2 0 0 0 2-2V8zM14 3v5h5M9 13h6M9 17h6";
        private const string IconImage = "M4 5h16v14H4zM4 15l5-5 4 4 3-3 4 4M15 9h.01";
        private const string IconBlock = "M4 7l8-4 8 4v10l-8 4-8-4zM4 7l8 4 8-4M12 11v10";
        private const string IconOther = "M5 12h.01M12 12h.01M19 12h.01";
        private const string IconPreview = "M10 10m-6 0a6 6 0 1 0 12 0a6 6 0 1 0-12 0M14.5 14.5 20 20";
        private const string IconSetup = "M4 4h16v16H4zM8 8h8M8 12h8M8 16h5";
        private const string IconBatch = "M7 3h12v14M4 6h12v15H4z";
        private const string IconStyles = "M12 3a9 9 0 1 0 0 18c1 0 2-1 1-2-1-2 0-3 2-3h3a3 3 0 0 0 3-3 9 9 0 0 0-9-10zM7 12h.01M9 7h.01M15 7h.01";

        private readonly MainViewModel _model;
        private string _searchText = string.Empty;
        private AppMenuItemViewModel _shown;
        private IReadOnlyList<CommandSuggestionViewModel> _results = Array.Empty<CommandSuggestionViewModel>();

        internal AppMenuViewModel(MainViewModel model, string appName)
        {
            _model = model;
            ExitText = "Exit " + (string.IsNullOrWhiteSpace(appName) ? "GeometryHelper" : appName.Trim());
            bool open = model.HasDocument;
            Items = new[]
            {
                new AppMenuItemViewModel("new", "New", IconNew, null, null, true, NewItems(model)),
                new AppMenuItemViewModel("open", "Open", IconOpen, null, null, true, new[]
                {
                    Entry("Drawing", IconOpen, "Open a DXF file (OPEN, Ctrl+O)", () => model.OpenCommand.Execute(null)),
                }),
                new AppMenuItemViewModel("save", "Save", IconSave, "Save the drawing (QSAVE, Ctrl+S)", () => model.Save(), open),
                new AppMenuItemViewModel("saveas", "Save As", IconSaveAs, null, null, true, new[]
                {
                    Entry("Drawing", IconDxf, "Save the drawing as a DXF file, choosing its version (R12 to 2018) (SAVEAS)", Start("SAVEAS"), open),
                    Entry("Drawing Template", IconSaveAs, "Save as a template for new drawings (not built yet)", null),
                    Entry("Other Formats", IconOther, "Save in another format (EXPORT, not built yet)", null),
                }),
                Rule(),
                new AppMenuItemViewModel("export", "Export", IconExport, null, null, true, new[]
                {
                    Entry("PDF", IconPdf, "A PDF of the model or a layout, through GeometryHelper PDF (PLOT)", Start("PLOT"), open),
                    Entry("PNG", IconImage, "An image of the drawing area as shown (PNGOUT)", Start("PNGOUT"), open),
                    Entry("JPG", IconImage, "An image of the drawing area as shown (JPGOUT)", Start("JPGOUT"), open),
                    Entry("BMP", IconImage, "An image of the drawing area as shown (BMPOUT)", Start("BMPOUT"), open),
                    Entry("Block or objects to a DXF", IconBlock, "Write a block or the objects selected to a new file (WBLOCK)", Start("WBLOCK"), open),
                    Entry("Other Formats", IconOther, "Export to another format (EXPORT, not built yet)", null),
                }),
                new AppMenuItemViewModel("print", "Print", IconPrint, null, null, true, new[]
                {
                    Entry("Plot", IconPrint, "Plot the model or a layout to a printer or a file (PLOT)", Start("PLOT"), open),
                    Entry("Batch Plot", IconBatch, "Plot several drawings and layouts at once (not built yet)", null),
                    Entry("Plot Preview", IconPreview, "See the drawing as it will be plotted (PREVIEW)", Start("PREVIEW"), open),
                    Entry("Page Setup", IconSetup, "Control the page layout, plot device, paper size of each layout (PAGESETUP)", Start("PAGESETUP"), open),
                    Entry("Manage Plot Styles", IconStyles, "Plot style tables: Monochrome, Grayscale, None are built in (not editable yet)", null),
                }),
                Rule(),
                new AppMenuItemViewModel("close", "Close", IconClose, null, null, true, new[]
                {
                    Entry("Current Drawing", IconClose, "Close the drawing shown (CLOSE)", () => model.CloseDocument(), open),
                    Entry("All Drawings", IconClose, "Close every drawing open, asking once about those not saved (CLOSEALL)", () => model.CloseAllDocuments(), open),
                }),
            };
            OptionsCommand = new DelegateCommand(_ => Done(() => model.RequestSettings(null)));
            ExitCommand = new DelegateCommand(_ => Done(() => ExitRequested?.Invoke(this, EventArgs.Empty)));
        }

        /// <summary>Raised when an item has been chosen: the window closes the menu.</summary>
        public event EventHandler Chosen;

        /// <summary>Raised by Exit: the window closes, asking first about the drawings not saved.</summary>
        public event EventHandler ExitRequested;

        /// <summary>Gets the items of the left column, a rule between groups.</summary>
        public IReadOnlyList<AppMenuItemViewModel> Items { get; }

        /// <summary>Gets or sets the item whose list is shown at the right; null for Recent Documents.</summary>
        public AppMenuItemViewModel ShownItem
        {
            get => _shown;
            set
            {
                if (Set(ref _shown, value != null && value.HasItems ? value : null))
                {
                    foreach (AppMenuItemViewModel item in Items)
                    {
                        item.IsShown = item == _shown;
                    }

                    Raise(nameof(IsRecentShown));
                    Raise(nameof(ShownTitle));
                }
            }
        }

        /// <summary>Gets whether the right shows Recent Documents.</summary>
        public bool IsRecentShown => _shown == null && !IsSearching;

        /// <summary>Gets the title of the list shown at the right, as "Export a copy of the drawing to other file formats".</summary>
        public string ShownTitle
        {
            get
            {
                switch (_shown?.Key)
                {
                    case "new":
                        return "Make a new drawing";
                    case "open":
                        return "Open a drawing";
                    case "saveas":
                        return "Save a copy of the drawing";
                    case "export":
                        return "Export a copy of the drawing to other file formats";
                    case "print":
                        return "Plot a drawing";
                    case "close":
                        return "Close drawings";
                    default:
                        return string.Empty;
                }
            }
        }

        /// <summary>Gets the recent files the right shows, the pinned ones first: the window's list.</summary>
        public IReadOnlyList<RecentDocumentViewModel> RecentDocuments => _model.RecentDocuments;

        /// <summary>Gets or sets the text of the search box; the right then lists the commands whose name holds it.</summary>
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (Set(ref _searchText, (value ?? string.Empty).Trim()))
                {
                    SearchResults = _searchText.Length == 0 ? Array.Empty<CommandSuggestionViewModel>() : _model.SearchCommands(_searchText);
                    Raise(nameof(IsSearching));
                    Raise(nameof(IsRecentShown));
                }
            }
        }

        /// <summary>Gets whether the search box holds text.</summary>
        public bool IsSearching => _searchText.Length > 0;

        /// <summary>Gets the commands the search found, as the command line's AutoComplete orders them.</summary>
        public IReadOnlyList<CommandSuggestionViewModel> SearchResults
        {
            get => _results;
            private set => Set(ref _results, value);
        }

        /// <summary>Gets the command of Options at the foot: Global settings.</summary>
        public ICommand OptionsCommand { get; }

        /// <summary>Gets the command of Exit at the foot.</summary>
        public ICommand ExitCommand { get; }

        /// <summary>Gets the words of Exit, with the name of the app: "Exit GeometryHelper DXF".</summary>
        public string ExitText { get; }

        /// <summary>Opens a file of Recent Documents; the menu closes.</summary>
        /// <param name="document">The file.</param>
        public void OpenRecent(RecentDocumentViewModel document)
        {
            if (document != null)
            {
                Done(() => _model.OpenRecent(document));
            }
        }

        /// <summary>Runs a command the search found.</summary>
        /// <param name="row">The row.</param>
        public void RunSearchResult(CommandSuggestionViewModel row)
        {
            if (row != null)
            {
                Done(() => _model.RunSuggestion(row));
            }
        }

        /// <summary>Chooses an item: its action, or the first entry of its list; the menu closes.</summary>
        /// <param name="item">The item.</param>
        public void Choose(AppMenuItemViewModel item)
        {
            if (item != null && item.Command.CanExecute(null))
            {
                Done(() => item.Command.Execute(null));
            }
        }

        private static IReadOnlyList<AppMenuItemViewModel> NewItems(MainViewModel model)
        {
            var items = new List<AppMenuItemViewModel>
            {
                Entry("Drawing", IconNew, "A new empty drawing of the metric template (NEW)", () => model.NewDrawing()),
            };

            if (model.CanMakeFromModel)
            {
                items.Add(Entry("Drawing from a model", "GhIconModel", "Make a drawing from the objects of a model (DRAWINGNEW)", () => model.NewFromModelCommand.Execute(null)));
            }

            return items;
        }

        private static AppMenuItemViewModel Entry(string name, string icon, string description, Action run, bool isEnabled = true)
            => new AppMenuItemViewModel(null, name, icon, description, run, isEnabled);

        private static AppMenuItemViewModel Rule() => new AppMenuItemViewModel(null, string.Empty, null, null, null, false);

        private Action Start(string command) => () => _model.StartCommand(command);

        private void Done(Action action)
        {
            Chosen?.Invoke(this, EventArgs.Empty);
            action();
        }
    }

    /// <summary>The recent files of the window, kept in the settings file, and the application menu.</summary>
    public sealed partial class MainViewModel
    {
        private IReadOnlyList<RecentDocumentViewModel> _recentDocuments = Array.Empty<RecentDocumentViewModel>();
        private bool _isRecentLarge;

        /// <summary>
        /// Gets the files of Recent Documents, as the application menu and the start screen list them: the pinned ones first
        /// in the order pinned, then the others by the date opened, newest first, <see cref="GlobalSettings.RecentFilesShown"/>
        /// in all unless more are pinned. A file opened by any way (the Open dialog, a drop, the command line of the app, the
        /// lists themselves) goes to the top.
        /// </summary>
        public IReadOnlyList<RecentDocumentViewModel> RecentDocuments => _recentDocuments;

        /// <summary>Gets the pinned files of <see cref="RecentDocuments"/>.</summary>
        public IReadOnlyList<RecentDocumentViewModel> PinnedDocuments => _recentDocuments.Where(d => d.IsPinned).ToList();

        /// <summary>Gets the files of <see cref="RecentDocuments"/> not pinned.</summary>
        public IReadOnlyList<RecentDocumentViewModel> UnpinnedDocuments => _recentDocuments.Where(d => !d.IsPinned).ToList();

        /// <summary>Gets whether a file is pinned, so that the PINNED heading shows.</summary>
        public bool HasPinnedDocuments => _recentDocuments.Any(d => d.IsPinned);

        /// <summary>Gets whether a file not pinned is listed, so that the RECENT heading shows.</summary>
        public bool HasUnpinnedDocuments => _recentDocuments.Any(d => !d.IsPinned);

        /// <summary>Gets whether any recent file is listed.</summary>
        public bool HasRecentDocuments => _recentDocuments.Count > 0;

        /// <summary>Gets or sets whether the start screen shows the recent files as large pictures rather than a list; a list by default.</summary>
        public bool IsRecentLarge
        {
            get => _isRecentLarge;
            set => Set(ref _isRecentLarge, value);
        }

        /// <summary>Gets the command of the start screen's list button: Recent Documents as a list.</summary>
        public ICommand ShowRecentAsListCommand => _recentListCommand ?? (_recentListCommand = new DelegateCommand(_ => IsRecentLarge = false));

        /// <summary>Gets the command of the start screen's pictures button: Recent Documents as large pictures.</summary>
        public ICommand ShowRecentLargeCommand => _recentLargeCommand ?? (_recentLargeCommand = new DelegateCommand(_ => IsRecentLarge = true));

        private DelegateCommand _recentListCommand;
        private DelegateCommand _recentLargeCommand;

        /// <summary>Gets or sets the clock the "today" and "yesterday" of the lists are told by; the machine's local time by default.</summary>
        internal Func<DateTime> Now { get; set; } = () => DateTime.Now;

        /// <summary>Makes the application menu of the G button, its items as the drawing shown allows.</summary>
        /// <param name="appName">The name of the app, which Exit names: "GeometryHelper DXF"; null for "GeometryHelper".</param>
        /// <returns>The menu.</returns>
        public AppMenuViewModel CreateAppMenu(string appName = null) => new AppMenuViewModel(this, appName);

        /// <summary>Opens a file of Recent Documents.</summary>
        /// <param name="document">The file.</param>
        /// <returns>True when it is shown.</returns>
        public bool OpenRecent(RecentDocumentViewModel document) => document != null && Open(document.Path);

        /// <summary>Pins a file of Recent Documents, or unpins it; the settings file keeps the pins, which the start screen and the menu share.</summary>
        /// <param name="document">The file.</param>
        /// <param name="pinned">Whether to pin it.</param>
        public void PinRecent(RecentDocumentViewModel document, bool pinned)
        {
            if (document != null && _settings.SetFilePinned(document.Path, pinned))
            {
                RebuildRecent();
                SettingsChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Closes every drawing open, as Close › All Drawings of the application menu: one with changes not saved asked about
        /// alone, several once together; Cancel closes none.
        /// </summary>
        /// <returns>Whether they were closed.</returns>
        public bool CloseAllDocuments() => CloseFiles(_files.Values.ToList());

        /// <summary>The commands and variables whose name holds a text, as the application menu's search lists them.</summary>
        internal IReadOnlyList<CommandSuggestionViewModel> SearchCommands(string text)
            => _current == null ? Array.Empty<CommandSuggestionViewModel>() : Suggest(_current.Session.Registry, text.Trim());

        /// <summary>A file opened: first among the recent files, kept in the settings file.</summary>
        private void NoteRecentFile(string full)
        {
            _settings.NoteFileOpened(full, DateTime.UtcNow);
            RebuildRecent();
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>Makes the rows of Recent Documents again from the settings in use.</summary>
        private void RebuildRecent()
        {
            int pinned = _settings.RecentFiles.Count(f => f.IsPinned);
            _recentDocuments = _settings.RecentFiles.Where(f => f.IsPinned)
                .Concat(_settings.RecentFiles.Where(f => !f.IsPinned).Take(Math.Max(0, GlobalSettings.RecentFilesShown - pinned)))
                .Select(f => new RecentDocumentViewModel(f, Now, d => OpenRecent(d), d => PinRecent(d, !d.IsPinned)))
                .ToList();

            if (pinned > 0 && pinned < _recentDocuments.Count)
            {
                _recentDocuments[pinned].IsFirstAfterPinned = true;
            }

            Raise(nameof(RecentDocuments));
            Raise(nameof(PinnedDocuments));
            Raise(nameof(UnpinnedDocuments));
            Raise(nameof(HasPinnedDocuments));
            Raise(nameof(HasUnpinnedDocuments));
            Raise(nameof(HasRecentDocuments));
        }
    }
}
