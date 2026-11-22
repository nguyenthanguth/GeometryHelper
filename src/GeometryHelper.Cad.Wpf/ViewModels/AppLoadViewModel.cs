using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using GeometryHelper.Cad.Plugins;

namespace GeometryHelper.Cad.Wpf
{
    /// <summary>How the footer of Load/Unload Applications shows the outcome of the last Load or Unload.</summary>
    public enum AppLoadStatusKind
    {
        /// <summary>Nothing to say yet.</summary>
        None,

        /// <summary>A file was loaded: shown in green.</summary>
        Loaded,

        /// <summary>A file was refused, for its type or its name: shown in amber.</summary>
        Refused,

        /// <summary>A load failed: shown in red.</summary>
        Failed,

        /// <summary>Something done that is neither: a plugin switched off, a file added to the Startup Suite.</summary>
        Info,
    }

    /// <summary>What came of loading one application: how to show it, and the line to show.</summary>
    internal sealed class AppLoadResult
    {
        internal AppLoadResult(AppLoadStatusKind kind, string message)
        {
            Kind = kind;
            Message = message;
        }

        internal AppLoadStatusKind Kind { get; }

        internal string Message { get; }
    }

    /// <summary>One of the three file types of Load/Unload Applications: its words, and the extension it shows (null for all files).</summary>
    public sealed class AppLoadFileType
    {
        internal AppLoadFileType(string label, string extension)
        {
            Label = label;
            Extension = extension;
        }

        /// <summary>Gets the words of the drop-down: <c>AutoLISP Files (*.lsp)</c>, <c>Plugins (*.dll)</c> or <c>All files (*.*)</c>.</summary>
        public string Label { get; }

        /// <summary>Gets the extension the list is limited to, with its dot, as <c>.lsp</c>; null for every file.</summary>
        public string Extension { get; }

        /// <inheritdoc/>
        public override string ToString() => Label;
    }

    /// <summary>What kind of place a row of Look in is, which gives it its icon and colour.</summary>
    public enum AppLoadPlaceKind
    {
        /// <summary>The Desktop, the top of the list.</summary>
        Desktop,

        /// <summary>The user's own folder, Documents or Downloads.</summary>
        UserFolder,

        /// <summary>This PC, which has no path: its list is the drives.</summary>
        ThisPc,

        /// <summary>A drive, as <c>Data (D:)</c>.</summary>
        Drive,

        /// <summary>A folder on the way down to the folder shown.</summary>
        Folder,
    }

    /// <summary>One row of Look in's drop-down: a place (Desktop, the user's folders, This PC, a drive, an ancestor of the folder shown).</summary>
    public sealed class AppLoadPlace
    {
        internal AppLoadPlace(
            string name,
            string path,
            int level,
            AppLoadPlaceKind kind,
            DriveType driveType,
            bool isCurrent
        )
        {
            Name = name;
            Path = path;
            Level = level;
            Kind = kind;
            DriveType = driveType;
            IsCurrent = isCurrent;
        }

        /// <summary>Gets the name the row shows: <c>Desktop</c>, <c>Documents</c>, <c>This PC</c>, <c>Local Disk (C:)</c>, or a folder's own name.</summary>
        public string Name { get; }

        /// <summary>Gets the full path; null for This PC.</summary>
        public string Path { get; }

        /// <summary>Gets how far the row is indented: 0 for Desktop, 1 for the user's folders and This PC, 2 for a drive, and one more for each folder down.</summary>
        public int Level { get; }

        /// <summary>Gets what kind of place it is.</summary>
        public AppLoadPlaceKind Kind { get; }

        /// <summary>Gets the kind of drive, which picks the drive's icon; <see cref="System.IO.DriveType.Unknown"/> for a row that is not a drive.</summary>
        public DriveType DriveType { get; }

        /// <summary>Gets whether it is the folder the list shows, the row Look in has chosen.</summary>
        public bool IsCurrent { get; }

        /// <inheritdoc/>
        public override string ToString() => Name;
    }

    /// <summary>A folder or a file of the list of Load/Unload Applications, or a drive of This PC: how the list shows it.</summary>
    public sealed class AppLoadEntry
    {
        internal AppLoadEntry(FileSystemInfo info)
        {
            IsFolder = info is DirectoryInfo;
            Path = info.FullName;
            Name = info.Name;
            Type = IsFolder ? "File folder" : TypeOf(info.Extension);
            Modified = info.LastWriteTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
            Size = info is FileInfo file
                ? ((file.Length + 1023L) / 1024L).ToString(CultureInfo.InvariantCulture) + " KB"
                : string.Empty;
            DriveType = DriveType.Unknown;
        }

        internal AppLoadEntry(AppLoadDrive drive)
        {
            IsFolder = true;
            Path = drive.Root;
            Name = drive.Name;
            Type = drive.TypeName;
            Modified = string.Empty;
            Size = string.Empty;
            DriveType = drive.DriveType;
        }

        /// <summary>Gets the full path.</summary>
        public string Path { get; }

        /// <summary>Gets the name, with its extension for a file.</summary>
        public string Name { get; }

        /// <summary>Gets whether it is a folder; a drive of This PC is one too, and opens as a folder does.</summary>
        public bool IsFolder { get; }

        /// <summary>Gets the kind of drive for a row of This PC, which picks the drive's icon; <see cref="System.IO.DriveType.Unknown"/> for a folder or a file.</summary>
        public DriveType DriveType { get; }

        /// <summary>Gets whether it is a drive of This PC.</summary>
        public bool IsDrive => DriveType != DriveType.Unknown;

        /// <summary>Gets what it is, as the list's Type column says it: File folder, AutoLISP file, Plugin, Compiled AutoLISP, or the extension's words; Local Disk, CD Drive or Network Drive for a drive.</summary>
        public string Type { get; }

        /// <summary>Gets when it was last written, as <c>2026-11-20 10:31</c>; empty for a drive.</summary>
        public string Modified { get; }

        /// <summary>Gets the size of a file in kilobytes, rounded up, as Explorer writes it: <c>1 KB</c> for a file of 1 to 1 024 bytes, <c>2 KB</c> for 1 025, <c>0 KB</c> for an empty one; empty for a folder or a drive.</summary>
        public string Size { get; }

        /// <inheritdoc/>
        public override string ToString() => Name;

        private static string TypeOf(string extension)
        {
            switch (extension.ToLowerInvariant())
            {
                case ".lsp":
                    return "AutoLISP file";
                case ".dll":
                    return "Plugin";
                case ".fas":
                case ".vlx":
                    return "Compiled AutoLISP";
                case ".arx":
                    return "ObjectARX application";
                case ".dbx":
                    return "ObjectDBX application";
                case ".dvb":
                    return "VBA project";
                case ".txt":
                    return "Text document";
                case "":
                    return "File";
                default:
                    return extension.Substring(1).ToUpperInvariant() + " file";
            }
        }
    }

    /// <summary>A row of the Loaded Applications list or of the History list: the file, its kind and the folder it is in.</summary>
    public sealed class AppLoadRow
    {
        internal AppLoadRow(string fullPath, string kind, string pluginName, bool isOff)
        {
            FullPath = fullPath;
            File = System.IO.Path.GetFileName(fullPath);
            Folder = System.IO.Path.GetDirectoryName(fullPath) ?? string.Empty;
            Kind = kind;
            PluginName = pluginName;
            IsOff = isOff;
        }

        /// <summary>Gets the full path of the file.</summary>
        public string FullPath { get; }

        /// <summary>Gets the name of the file, with its extension.</summary>
        public string File { get; }

        /// <summary>Gets the folder the file is in.</summary>
        public string Folder { get; }

        /// <summary>Gets what the file is: <c>AutoLISP</c> (shown green), <c>Plugin</c> (blue), or <c>File</c> for any other a History row can hold.</summary>
        public string Kind { get; }

        /// <summary>Gets whether it is a plugin.</summary>
        public bool IsPlugin => Kind == "Plugin";

        /// <summary>Gets the plugin's name for the plugin manager; null for a row that is not a loaded plugin.</summary>
        public string PluginName { get; }

        /// <summary>Gets whether it is a plugin switched off in this dialog: its row stays, dimmed, so that it can be seen to have gone.</summary>
        public bool IsOff { get; }

        /// <inheritdoc/>
        public override string ToString() => File;
    }

    /// <summary>
    /// AutoCAD 2021's Load/Unload Applications, shown by APPLOAD: a folder browser of its own (Look in with its places, Back,
    /// Up, This PC, the files of the type chosen) over Load, Add to History, the Loaded Applications and History list tabs with
    /// their Unload or Remove button, and the Startup Suite. Load takes a file typed or chosen into the document shown, an
    /// AutoLISP file as <see cref="CadCommandSession.LoadLisp"/> does and a .dll through the plugin manager; a typed folder
    /// opens, a typed pattern filters the list and a typed name that is not there is answered with a box; the footer says
    /// what came of the last Load or Unload. The History list, the folder last used (kept by a Load only), the Files of type
    /// and Add to History are kept in Global settings (<see cref="GlobalSettings.AppLoadHistory"/>,
    /// <see cref="GlobalSettings.AppLoadFolder"/>, <see cref="GlobalSettings.AppLoadFileType"/>,
    /// <see cref="GlobalSettings.AppLoadAddToHistory"/>).
    /// </summary>
    public sealed class AppLoadViewModel : ObservableObject
    {
        private const string LispKind = "AutoLISP";
        private const string PluginKind = "Plugin";
        private static readonly char[] Wildcards = { '*', '?' };

        private readonly MainViewModel _model;
        private readonly IAppLoadPlaceSource _source;
        private readonly Stack<string> _back = new Stack<string>();
        private readonly HashSet<string> _switchedOff = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private IReadOnlyList<AppLoadDrive> _drives;
        private IReadOnlyList<AppLoadPlace> _places = Array.Empty<AppLoadPlace>();
        private string _pattern;
        private string _folder;
        private IReadOnlyList<string> _folderChain = Array.Empty<string>();
        private IReadOnlyList<AppLoadEntry> _entries = Array.Empty<AppLoadEntry>();
        private IReadOnlyList<AppLoadRow> _loadedRows = Array.Empty<AppLoadRow>();
        private IReadOnlyList<AppLoadRow> _historyRows = Array.Empty<AppLoadRow>();
        private IReadOnlyList<AppLoadRow> _chosenRows = Array.Empty<AppLoadRow>();
        private AppLoadFileType _fileType;
        private string _fileName = string.Empty;
        private bool _historyTab;
        private string _status = string.Empty;
        private AppLoadStatusKind _statusKind;

        internal AppLoadViewModel(MainViewModel model, IAppLoadPlaceSource source = null)
        {
            _model = model;
            _source = source ?? new AppLoadSystemPlaces();
            FileTypes = new[]
            {
                new AppLoadFileType("AutoLISP Files (*.lsp)", ".lsp"),
                new AppLoadFileType("Plugins (*.dll)", ".dll"),
                new AppLoadFileType("All files (*.*)", null),
            };
            string kept = model.Settings.AppLoadFileType;
            _fileType =
                kept == "dll" ? FileTypes[1]
                : kept == "all" ? FileTypes[2]
                : FileTypes[0];
            StartupSuite = new StartupSuiteViewModel(model);
            Navigate(StartFolder());
            RefreshRows();
        }

        /// <summary>Gets the full path of the folder the list shows; null at This PC.</summary>
        public string Folder => _folder;

        /// <summary>Gets whether the list shows This PC, which has no folder: its rows are the drives.</summary>
        public bool IsThisPc => _folder == null;

        /// <summary>Gets the folders from the root down to <see cref="Folder"/>; empty at This PC.</summary>
        public IReadOnlyList<string> FolderChain => _folderChain;

        /// <summary>
        /// Gets the rows of Look in's drop-down, in the order Windows shows them: Desktop; the user's folder, Documents and
        /// Downloads, those that exist; This PC; each drive that is ready, by letter; and the folders on the way down to the
        /// folder shown, indented under its drive (or under Documents, Downloads or the user's folder when it lies inside
        /// them), the last of them chosen. A drive or This PC shown is its own row chosen, with nothing under it.
        /// </summary>
        public IReadOnlyList<AppLoadPlace> Places => _places;

        /// <summary>
        /// Gets or sets the row Look in has chosen, the folder shown; choosing another row opens it, as
        /// <see cref="OpenFolder"/> does, and This PC shows the drives. Setting null does nothing.
        /// </summary>
        public AppLoadPlace Place
        {
            get => _places.FirstOrDefault(p => p.IsCurrent);
            set
            {
                if (value == null || value.IsCurrent)
                {
                    return;
                }

                if (value.Kind == AppLoadPlaceKind.ThisPc)
                {
                    GoTo(null);
                }
                else
                {
                    OpenFolder(value.Path);
                }
            }
        }

        /// <summary>Gets or sets the folder Look in shows; choosing another one in the drop-down opens it, as <see cref="OpenFolder"/> does.</summary>
        public string LookIn
        {
            get => _folder;
            set
            {
                if (!string.IsNullOrEmpty(value) && !string.Equals(value, _folder, StringComparison.Ordinal))
                {
                    OpenFolder(value);
                }
            }
        }

        /// <summary>Gets the folders, then the files of the type chosen, in the folder shown, each group by name in ordinal order, either case the same; the drives at This PC.</summary>
        public IReadOnlyList<AppLoadEntry> Entries => _entries;

        /// <summary>
        /// Gets or sets how the window shows a box of one button (title, message, detail), as <see cref="CadQuestionWindow.ShowMessage(System.Windows.Window, string, string, string)"/>
        /// does: what Load answers to a typed name that is not there. Null for none, when the footer says it instead.
        /// </summary>
        public Action<string, string, string> ShowMessage { get; set; }

        /// <summary>Gets the three file types: AutoLISP Files, Plugins and All files.</summary>
        public IReadOnlyList<AppLoadFileType> FileTypes { get; }

        /// <summary>
        /// Gets or sets the file type shown, AutoLISP Files at the first start; choosing one ends a typed pattern and is kept in
        /// Global settings (<see cref="GlobalSettings.AppLoadFileType"/>) the moment it changes, so that the dialog opens on it next time.
        /// </summary>
        public AppLoadFileType FileType
        {
            get => _fileType;
            set
            {
                if (value == null)
                {
                    return;
                }

                bool changed = Set(ref _fileType, value);
                _pattern = null;
                Refresh();

                if (changed)
                {
                    string word =
                        value == FileTypes[1] ? "dll"
                        : value == FileTypes[2] ? "all"
                        : "lsp";
                    _model.ChangeSettings(s => s.AppLoadFileType = word, "APPLOAD changed Files of type");
                }
            }
        }

        /// <summary>
        /// Gets or sets the File name box: the name of the file chosen in the list, or several, each in quotes; or a name typed,
        /// of a file in the folder shown or a full path. Load takes the files it names.
        /// </summary>
        public string FileName
        {
            get => _fileName;
            set
            {
                if (Set(ref _fileName, value ?? string.Empty))
                {
                    RaiseCanLoad();
                }
            }
        }

        /// <summary>Gets or sets whether the History list tab is shown; false for the Loaded Applications tab, shown at the start.</summary>
        public bool IsHistoryTab
        {
            get => _historyTab;
            set
            {
                if (Set(ref _historyTab, value))
                {
                    _chosenRows = Array.Empty<AppLoadRow>();
                    Raise(nameof(IsLoadedTab));
                    Raise(nameof(Rows));
                    RaiseCanLoad();
                    RaiseAction();
                }
            }
        }

        /// <summary>Gets or sets whether the Loaded Applications tab is shown; the other of <see cref="IsHistoryTab"/>.</summary>
        public bool IsLoadedTab
        {
            get => !_historyTab;
            set => IsHistoryTab = !value;
        }

        /// <summary>Gets the rows of the tab shown: the files loaded into the drawing and the plugins loaded, or the History list.</summary>
        public IReadOnlyList<AppLoadRow> Rows => _historyTab ? _historyRows : _loadedRows;

        /// <summary>Gets the AutoLISP files loaded into the drawing shown (APPLOAD, <c>(load)</c>, the startup files), in the order they loaded, then the plugins loaded, and the ones this dialog switched off, dimmed.</summary>
        public IReadOnlyList<AppLoadRow> LoadedRows => _loadedRows;

        /// <summary>Gets the History list: the files given to Load with Add to History ticked, the newest first.</summary>
        public IReadOnlyList<AppLoadRow> HistoryRows => _historyRows;

        /// <summary>Gets or sets Add to History, kept in Global settings the moment it changes.</summary>
        public bool AddToHistory
        {
            get => _model.Settings.AppLoadAddToHistory;
            set
            {
                if (value != _model.Settings.AppLoadAddToHistory)
                {
                    _model.ChangeSettings(s => s.AppLoadAddToHistory = value, "Add to History was changed");
                    Raise(nameof(AddToHistory));
                }
            }
        }

        /// <summary>Gets the line of the footer: the outcome of the last Load or Unload; empty before one.</summary>
        public string Status => _status;

        /// <summary>Gets how the footer shows <see cref="Status"/>.</summary>
        public AppLoadStatusKind StatusKind => _statusKind;

        /// <summary>Gets the Startup Suite, which the briefcase adds files to and Contents… shows.</summary>
        public StartupSuiteViewModel StartupSuite { get; }

        /// <summary>Gets or sets how the window shows the Startup Suite over this dialog; null for none, when Contents… does nothing.</summary>
        public Action<StartupSuiteViewModel> ShowStartupSuite { get; set; }

        /// <summary>Gets the address of the guide where Help opens, at the paragraph on APPLOAD.</summary>
        public string HelpUrl => LispHelp.AppLoad;

        /// <summary>Gets whether Back can go: a folder was shown before this one.</summary>
        public bool CanBack => _back.Count > 0;

        /// <summary>Gets whether Up can go: any folder has an Up, a drive's root going to This PC; This PC is the top, with none.</summary>
        public bool CanUp => _folder != null;

        /// <summary>
        /// Gets whether Load can load: a drawing is open, and on the Loaded Applications tab a name is typed or chosen, on the
        /// History list tab a row is chosen.
        /// </summary>
        public bool CanLoad =>
            _model.HasDocument && (_historyTab ? _chosenRows.Count > 0 : !string.IsNullOrWhiteSpace(_fileName));

        /// <summary>Gets the words of the button beside the tabs: Unload on the Loaded Applications tab, Remove on the History list.</summary>
        public string ActionLabel => _historyTab ? "Remove" : "Unload";

        /// <summary>
        /// Gets whether the button beside the tabs can act: Remove with a row of the History list chosen; Unload with plugin
        /// rows chosen, switched on. AutoLISP cannot be unloaded, so a row of one greys it.
        /// </summary>
        public bool CanAct =>
            _chosenRows.Count > 0 && (_historyTab || _chosenRows.All(r => r.PluginName != null && !r.IsOff));

        /// <summary>Gets the tooltip of the button beside the tabs, which says why Unload is greyed on an AutoLISP row.</summary>
        public string ActionToolTip
        {
            get
            {
                if (_historyTab)
                {
                    return "Take the file out of the History list; the file stays on the disk";
                }

                return _chosenRows.Any(r => r.Kind == LispKind)
                    ? "AutoLISP files cannot be unloaded: what they defined stays in the drawing until it is closed"
                    : "Unload the plugin, as the Plugin manager does";
            }
        }

        /// <summary>
        /// Opens a folder in the list: the folder shown is kept for Back, the File name box is emptied, and the files of the
        /// type chosen are listed.
        /// </summary>
        /// <param name="path">The folder.</param>
        /// <returns>False where there is no such folder, or it is the folder shown.</returns>
        public bool OpenFolder(string path)
        {
            string full = FullFolder(path);
            return full != null && GoTo(full);
        }

        /// <summary>Shows a folder, or This PC for null; the folder left is kept for Back. False where it is the one shown.</summary>
        private bool GoTo(string folder)
        {
            if (string.Equals(folder, _folder, StringComparison.Ordinal))
            {
                return false;
            }

            _back.Push(_folder);
            Navigate(folder);
            return true;
        }

        /// <summary>Goes back to the folder, or This PC, shown before this one; nothing where there is none.</summary>
        public void Back()
        {
            if (_back.Count > 0)
            {
                Navigate(_back.Pop());
            }
        }

        /// <summary>Goes up one level, as <see cref="OpenFolder"/> does; from a drive's root to This PC; nothing at This PC.</summary>
        public void Up()
        {
            if (_folder == null)
            {
                return;
            }

            DirectoryInfo parent = Directory.GetParent(_folder);

            if (parent != null)
            {
                OpenFolder(parent.FullName);
            }
            else
            {
                GoTo(null);
            }
        }

        /// <summary>
        /// Takes the folders and files chosen in the list, the window's selection: the names of the files go to the File name
        /// box, one name, or several each in quotes, in the order of the list. Folders are not named.
        /// </summary>
        /// <param name="chosen">The entries chosen.</param>
        public void ChooseEntries(IEnumerable<AppLoadEntry> chosen)
        {
            IReadOnlyList<AppLoadEntry> inOrder = InListOrder(chosen ?? Enumerable.Empty<AppLoadEntry>(), _entries);
            List<string> files = inOrder.Where(e => !e.IsFolder).Select(e => e.Name).ToList();

            if (files.Count == 1)
            {
                FileName = files[0];
            }
            else if (files.Count > 1)
            {
                FileName = string.Join(" ", files.Select(f => "\"" + f + "\""));
            }
        }

        /// <summary>A double-click on an entry of the list: a folder opens, a file is loaded.</summary>
        /// <param name="entry">The entry.</param>
        public void Activate(AppLoadEntry entry)
        {
            if (entry == null)
            {
                return;
            }

            if (entry.IsFolder)
            {
                OpenFolder(entry.Path);
            }
            else
            {
                FileName = entry.Name;
                Load();
            }
        }

        /// <summary>Takes the rows chosen in the tab shown, the window's selection, which Unload, Remove and (on the History list) Load act on.</summary>
        /// <param name="chosen">The rows chosen.</param>
        public void ChooseRows(IEnumerable<AppLoadRow> chosen)
        {
            _chosenRows = InListOrder(chosen ?? Enumerable.Empty<AppLoadRow>(), Rows);
            RaiseCanLoad();
            RaiseAction();
        }

        /// <summary>
        /// File name's Enter: puts the text in the File name box and does what <see cref="Load"/> does with it.
        /// </summary>
        /// <param name="text">The text typed.</param>
        public void Enter(string text)
        {
            FileName = text;
            Load();
        }

        /// <summary>
        /// Loads the files, in the order of the list: those the History list rows chosen name, or those the File name box names.
        /// A single name typed is taken for what it names, in this order: a folder, full or relative to the folder shown
        /// (<c>..</c> too), is opened and the box emptied; a text with <c>*</c> or <c>?</c> limits the list to the names it
        /// matches, folders kept, the text staying in the box, and a text that is the pattern of one of the Files of type
        /// chooses it; a name with no file or folder behind it is answered with a box (<see cref="ShowMessage"/>) and nothing
        /// is loaded, with several names the box for the first that is missing. Otherwise each file is loaded as the command
        /// line would, an AutoLISP file into the drawing shown and a .dll through the plugin manager; .arx, .dbx, .dvb, .fas
        /// and .vlx files are refused and any other type said not to be one or the other. With Add to History ticked each file
        /// given goes first in the History list, loaded or not, and a load from the folder shown keeps that folder for next
        /// time. The footer shows the last file's outcome. Loading nothing happens where <see cref="CanLoad"/> is false.
        /// </summary>
        public void Load()
        {
            List<string> typed = null;

            if (!_historyTab)
            {
                typed = TypedNames();

                if (typed.Count == 1 && OpenOrLimit(typed[0]))
                {
                    return;
                }
            }

            if (!CanLoad)
            {
                return;
            }

            List<string> named;

            if (_historyTab)
            {
                named = _chosenRows.Select(r => r.FullPath).ToList();
            }
            else
            {
                string missing = typed.FirstOrDefault(IsMissing);

                if (missing != null)
                {
                    ShowNotFound(missing);
                    return;
                }

                named = typed.Select(InFolder).ToList();
            }

            if (named.Count == 0)
            {
                return;
            }

            List<AppLoadResult> results = LoadNamed(named, !_historyTab);
            AppLoadResult last = results.LastOrDefault();

            if (last != null)
            {
                SetStatus(last.Message, last.Kind);
            }

            RefreshRows();
        }

        /// <summary>
        /// Loads files dropped on the Loaded Applications list, from Explorer or from this dialog's own list: at once, in the
        /// order of the drop, each as <see cref="Load"/> loads one and with Add to History as it does, though the folder kept
        /// is not changed. The footer says <c>Successfully loaded 2 files.</c> for several loaded, the file's own line for one,
        /// and the line of the last failure where any file failed. A folder is passed over, and so is a drop on the History
        /// list tab or with no drawing open.
        /// </summary>
        /// <param name="paths">The full paths dropped.</param>
        public void LoadDropped(IEnumerable<string> paths)
        {
            if (_historyTab || !_model.HasDocument)
            {
                return;
            }

            List<string> files = (paths ?? Enumerable.Empty<string>())
                .Where(p => !string.IsNullOrWhiteSpace(p) && File.Exists(p))
                .ToList();

            if (files.Count == 0)
            {
                return;
            }

            List<AppLoadResult> results = LoadNamed(files, false);
            AppLoadResult failure = results.LastOrDefault(r => r.Kind != AppLoadStatusKind.Loaded);

            if (failure != null)
            {
                SetStatus(failure.Message, failure.Kind);
            }
            else if (results.Count > 1)
            {
                SetStatus(
                    "Successfully loaded " + results.Count.ToString(CultureInfo.InvariantCulture) + " files.",
                    AppLoadStatusKind.Loaded
                );
            }
            else
            {
                SetStatus(results[0].Message, results[0].Kind);
            }

            RefreshRows();
        }

        /// <summary>Loads the files, each as the command line would, and keeps them in the History list with Add to History ticked, and the folder shown where asked.</summary>
        private List<AppLoadResult> LoadNamed(IReadOnlyList<string> named, bool keepFolder)
        {
            var given = new List<string>();
            var results = new List<AppLoadResult>();

            foreach (string path in named)
            {
                if (!TryFullPath(path, out string full))
                {
                    results.Add(new AppLoadResult(AppLoadStatusKind.Refused, path + " is not a valid file name."));
                    continue;
                }

                given.Add(full);
                results.Add(_model.LoadApplication(full));
            }

            string folder = keepFolder ? _folder : null;
            bool add = _model.Settings.AppLoadAddToHistory;

            _model.ChangeSettings(
                s =>
                {
                    if (add)
                    {
                        var history = s.AppLoadHistory.ToList();

                        foreach (string full in given)
                        {
                            history.Insert(0, full);
                        }

                        s.AppLoadHistory = history;
                    }

                    if (folder != null)
                    {
                        s.AppLoadFolder = folder;
                    }
                },
                "APPLOAD loaded files"
            );

            return results;
        }

        /// <summary>A single text typed that names a folder (opened, the box emptied) or holds a wildcard (the list limited): true where it did either.</summary>
        private bool OpenOrLimit(string text)
        {
            if (text.IndexOfAny(Wildcards) >= 0)
            {
                Limit(text);
                return true;
            }

            string full = Resolve(text);

            if (full != null && Directory.Exists(full))
            {
                OpenFolder(full);
                FileName = string.Empty;
                return true;
            }

            return false;
        }

        /// <summary>Limits the list to the names a pattern matches, or chooses the Files of type whose pattern it is; the text typed stays.</summary>
        private void Limit(string pattern)
        {
            AppLoadFileType own = FileTypes.FirstOrDefault(t =>
                string.Equals(PatternOf(t), pattern, StringComparison.OrdinalIgnoreCase)
            );

            if (own != null)
            {
                FileType = own;
                return;
            }

            _pattern = pattern;
            Refresh();
        }

        private static string PatternOf(AppLoadFileType type) => type.Extension == null ? "*.*" : "*" + type.Extension;

        /// <summary>Whether a name matches a pattern with <c>*</c> and <c>?</c>, either case; <c>*.*</c> matches every name.</summary>
        private static bool Matches(string name, string pattern)
        {
            if (pattern == "*.*")
            {
                return true;
            }

            string expression = "^" + Regex.Escape(pattern).Replace("\\*", ".*").Replace("\\?", ".") + "$";
            return Regex.IsMatch(name, expression, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }

        /// <summary>The full path a typed name stands for: itself where it is a full path, else in the folder shown; null at This PC for a name that is not, and for one no path can hold.</summary>
        private string Resolve(string name)
        {
            string path;

            try
            {
                if (Path.IsPathRooted(name))
                {
                    path = name;
                }
                else if (_folder == null)
                {
                    return null;
                }
                else
                {
                    path = Path.Combine(_folder, name);
                }
            }
            catch (ArgumentException)
            {
                return null;
            }

            return TryFullPath(path, out string full) ? full : null;
        }

        /// <summary>Whether a typed name has no file and no folder behind it; a name no path can hold is left to the load, which refuses it.</summary>
        private bool IsMissing(string name)
        {
            string full = Resolve(name);

            if (full == null)
            {
                return _folder == null && !HasInvalidCharacter(name);
            }

            return !File.Exists(full) && !Directory.Exists(full);
        }

        private static bool HasInvalidCharacter(string name) => name.IndexOfAny(Path.GetInvalidPathChars()) >= 0;

        /// <summary>Answers a typed name that is not there with a box, as AutoCAD does; the footer says it where the window has no box.</summary>
        private void ShowNotFound(string name)
        {
            string full = Resolve(name) ?? name;
            bool folder = name.EndsWith("\\", StringComparison.Ordinal) || name.EndsWith("/", StringComparison.Ordinal);
            string title = folder ? "Load/Unload Applications" : "Error";
            string message = folder
                ? "Path does not exist. Check the path and try again."
                : "The following file was not found:";

            if (ShowMessage != null)
            {
                ShowMessage(title, message, full);
            }
            else
            {
                SetStatus(message + " " + full, AppLoadStatusKind.Refused);
            }
        }

        /// <summary>
        /// The button beside the tabs. Remove takes the History rows chosen out of the History list, never off the disk.
        /// Unload switches off each plugin row chosen, as the Plugin manager's Unload does; the footer shows the manager's
        /// line for the last, and the row stays, dimmed. Nothing happens where <see cref="CanAct"/> is false.
        /// </summary>
        public void Act()
        {
            if (!CanAct)
            {
                return;
            }

            if (_historyTab)
            {
                var gone = new HashSet<string>(_chosenRows.Select(r => r.FullPath), StringComparer.OrdinalIgnoreCase);
                _model.ChangeSettings(
                    s => s.AppLoadHistory = s.AppLoadHistory.Where(f => !gone.Contains(f)).ToList(),
                    "rows were removed from the History list"
                );
                _chosenRows = Array.Empty<AppLoadRow>();
            }
            else
            {
                CadPluginResult result = null;

                foreach (AppLoadRow row in _chosenRows)
                {
                    result = _model.UnloadPlugin(row.PluginName);
                    _switchedOff.Add(row.PluginName);
                }

                if (result != null)
                {
                    SetStatus(result.Message, result.Succeeded ? AppLoadStatusKind.Info : AppLoadStatusKind.Failed);
                }

                _chosenRows = Array.Empty<AppLoadRow>();
            }

            RefreshRows();
        }

        /// <summary>
        /// Adds files dropped on the Startup Suite's briefcase, from the list or from Explorer, to the Startup Suite, and says
        /// in the footer what was added or why a file was refused.
        /// </summary>
        /// <param name="paths">The files dropped.</param>
        public void AddToStartupSuite(IEnumerable<string> paths)
        {
            int added = StartupSuite.Add(paths);

            if (StartupSuite.Message.Length > 0)
            {
                SetStatus(StartupSuite.Message, AppLoadStatusKind.Refused);
            }
            else if (added > 0)
            {
                SetStatus(
                    added == 1
                        ? Path.GetFileName(StartupSuite.Files[StartupSuite.Files.Count - 1])
                            + " added to the Startup Suite."
                        : added.ToString(CultureInfo.InvariantCulture) + " files added to the Startup Suite.",
                    AppLoadStatusKind.Info
                );
            }
        }

        /// <summary>Shows the Startup Suite over this dialog (Contents…), where the window can.</summary>
        public void ShowContents() => ShowStartupSuite?.Invoke(StartupSuite);

        /// <summary>Lists the folder shown again, as after a file was written there; at This PC the drives are read again.</summary>
        public void Refresh()
        {
            if (_folder == null)
            {
                _drives = null;
                _entries = Drives.Select(d => new AppLoadEntry(d)).ToList();
                Raise(nameof(Entries));
                SetPlaces();
                return;
            }

            var entries = new List<AppLoadEntry>();

            try
            {
                foreach (FileSystemInfo info in new DirectoryInfo(_folder).EnumerateFileSystemInfos())
                {
                    if ((info.Attributes & (FileAttributes.Hidden | FileAttributes.System)) != 0)
                    {
                        continue;
                    }

                    if (info is DirectoryInfo || (_pattern != null ? Matches(info.Name, _pattern) : OfType(info)))
                    {
                        entries.Add(new AppLoadEntry(info));
                    }
                }
            }
            catch (Exception ex)
                when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException)
            {
                entries.Clear();
                SetStatus("The folder cannot be read: " + ex.Message, AppLoadStatusKind.Refused);
            }

            entries.Sort(Compare);
            _entries = entries;
            Raise(nameof(Entries));
        }

        private bool OfType(FileSystemInfo file) =>
            _fileType.Extension == null
            || string.Equals(file.Extension, _fileType.Extension, StringComparison.OrdinalIgnoreCase);

        /// <summary>The drives that are ready, read once and again when This PC is refreshed, so that moving about never waits on one.</summary>
        private IReadOnlyList<AppLoadDrive> Drives => _drives ?? (_drives = _source.Drives());

        private void SetPlaces()
        {
            _places = BuildPlaces();
            Raise(nameof(Places));
            Raise(nameof(Place));
        }

        private static string Trimmed(string path) => path.TrimEnd('\\', '/');

        private static bool Same(string a, string b) =>
            string.Equals(Trimmed(a), Trimmed(b), StringComparison.OrdinalIgnoreCase);

        /// <summary>Whether a path lies below another, either case.</summary>
        private static bool IsInside(string path, string above)
        {
            string top = Trimmed(above);
            return path.Length > top.Length
                && path.StartsWith(top, StringComparison.OrdinalIgnoreCase)
                && (path[top.Length] == '\\' || path[top.Length] == '/');
        }

        /// <summary>The rows of Look in for the folder shown, as <see cref="Places"/> describes them.</summary>
        private IReadOnlyList<AppLoadPlace> BuildPlaces()
        {
            string folder = _folder;
            string desktop = _source.Desktop;
            string user = _source.UserFolder;
            string documents = _source.Documents;
            string downloads = _source.Downloads;
            bool isHome =
                folder != null && new[] { desktop, user, documents, downloads }.Any(h => h != null && Same(h, folder));
            string root = folder == null ? null : Path.GetPathRoot(folder);

            // The home folder the folder shown hangs under: Documents or Downloads first, then the user's folder.
            string under = null;

            if (folder != null && !isHome)
            {
                under = new[] { documents, downloads, user }.FirstOrDefault(h => h != null && IsInside(folder, h));
            }

            var rows = new List<AppLoadPlace>();

            void AddChain(string above, int level)
            {
                if (folder == null || isHome || !IsInside(folder, above))
                {
                    return;
                }

                string path = folder.Substring(0, Trimmed(above).Length);
                string[] parts = folder
                    .Substring(path.Length)
                    .Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);

                for (int i = 0; i < parts.Length; i++)
                {
                    path += "\\" + parts[i];
                    rows.Add(
                        new AppLoadPlace(
                            parts[i],
                            path,
                            level + i,
                            AppLoadPlaceKind.Folder,
                            DriveType.Unknown,
                            i == parts.Length - 1
                        )
                    );
                }
            }

            void AddHome(string name, string path, int level, AppLoadPlaceKind kind)
            {
                rows.Add(
                    new AppLoadPlace(name, path, level, kind, DriveType.Unknown, folder != null && Same(path, folder))
                );

                if (under != null && Same(path, under))
                {
                    AddChain(path, level + 1);
                }
            }

            if (desktop != null)
            {
                AddHome("Desktop", desktop, 0, AppLoadPlaceKind.Desktop);
            }

            if (user != null)
            {
                AddHome(Path.GetFileName(Trimmed(user)), user, 1, AppLoadPlaceKind.UserFolder);
            }

            if (documents != null)
            {
                AddHome("Documents", documents, 1, AppLoadPlaceKind.UserFolder);
            }

            if (downloads != null)
            {
                AddHome("Downloads", downloads, 1, AppLoadPlaceKind.UserFolder);
            }

            rows.Add(new AppLoadPlace("This PC", null, 1, AppLoadPlaceKind.ThisPc, DriveType.Unknown, folder == null));

            List<AppLoadDrive> drives = Drives.ToList();

            // A folder on a drive the list does not have (a network share, a drive that was busy) still hangs under its own row.
            if (root != null && !drives.Any(d => Same(d.Root, root)))
            {
                drives.Add(new AppLoadDrive(root, null, DriveType.Unknown));
            }

            foreach (AppLoadDrive drive in drives.OrderBy(d => d.Root, StringComparer.OrdinalIgnoreCase))
            {
                rows.Add(
                    new AppLoadPlace(
                        drive.Name,
                        drive.Root,
                        2,
                        AppLoadPlaceKind.Drive,
                        drive.DriveType,
                        folder != null && Same(drive.Root, folder)
                    )
                );

                if (under == null && root != null && Same(drive.Root, root))
                {
                    AddChain(drive.Root, 3);
                }
            }

            return rows;
        }

        private static int Compare(AppLoadEntry a, AppLoadEntry b)
        {
            int byKind = b.IsFolder.CompareTo(a.IsFolder);

            if (byKind != 0)
            {
                return byKind;
            }

            int byName = StringComparer.OrdinalIgnoreCase.Compare(a.Name, b.Name);
            return byName != 0 ? byName : StringComparer.Ordinal.Compare(a.Name, b.Name);
        }

        /// <summary>The chosen items in the order of the list they are chosen from, a chosen item that is not in it left out.</summary>
        private static IReadOnlyList<T> InListOrder<T>(IEnumerable<T> chosen, IReadOnlyList<T> list)
            where T : class
        {
            var set = new HashSet<T>(chosen);
            return list.Where(set.Contains).ToList();
        }

        private string StartFolder()
        {
            GlobalSettings settings = _model.Settings;

            if (Directory.Exists(settings.AppLoadFolder))
            {
                return settings.AppLoadFolder;
            }

            string support = settings.SupportSearchFolders.FirstOrDefault(Directory.Exists);
            return support ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        }

        /// <summary>The full path of an existing folder, with no closing separator unless it is a root; null where there is none.</summary>
        private static string FullFolder(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            {
                return null;
            }

            string full = new DirectoryInfo(path).FullName;
            string root = Path.GetPathRoot(full);
            return string.Equals(full, root, StringComparison.OrdinalIgnoreCase) ? full : full.TrimEnd('\\', '/');
        }

        /// <summary>Shows a folder, or This PC for null: the File name box is emptied and a typed pattern dropped.</summary>
        private void Navigate(string path)
        {
            _folder = path == null ? null : FullFolder(path) ?? path;
            var chain = new List<string>();

            for (string p = _folder; p != null; p = Path.GetDirectoryName(p))
            {
                chain.Insert(0, p);
            }

            _folderChain = chain;
            _fileName = string.Empty;
            _pattern = null;
            Refresh();
            SetPlaces();
            Raise(nameof(Folder));
            Raise(nameof(IsThisPc));
            Raise(nameof(FolderChain));
            Raise(nameof(LookIn));
            Raise(nameof(FileName));
            Raise(nameof(CanBack));
            Raise(nameof(CanUp));
            RaiseCanLoad();
        }

        /// <summary>The names the File name box names, as typed: each name in quotes, or the whole text as one name.</summary>
        private List<string> TypedNames()
        {
            string text = _fileName.Trim();
            var names = new List<string>();

            if (text.IndexOf('"') >= 0)
            {
                foreach (string part in text.Split('"').Where((p, i) => i % 2 == 1))
                {
                    if (!string.IsNullOrWhiteSpace(part))
                    {
                        names.Add(part.Trim());
                    }
                }
            }
            else
            {
                names.Add(text);
            }

            return names;
        }

        /// <summary>The name as it is where it is a full path, else in the folder shown; a name with a character no path may hold is left as it is, for the load to refuse.</summary>
        private string InFolder(string name)
        {
            try
            {
                return Path.IsPathRooted(name) || _folder == null ? name : Path.Combine(_folder, name);
            }
            catch (ArgumentException)
            {
                return name;
            }
        }

        private static bool TryFullPath(string path, out string full)
        {
            try
            {
                full = Path.GetFullPath(path);
                return true;
            }
            catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException || ex is IOException)
            {
                full = null;
                return false;
            }
        }

        private void RefreshRows()
        {
            var loaded = _model.LoadedLispFiles.Select(f => new AppLoadRow(f, LispKind, null, false)).ToList();

            if (_model.Plugins != null)
            {
                foreach (CadLoadedPlugin plugin in _model.Plugins.Plugins)
                {
                    if (plugin.IsLoaded || _switchedOff.Contains(plugin.Name))
                    {
                        loaded.Add(new AppLoadRow(plugin.Path, PluginKind, plugin.Name, !plugin.IsLoaded));
                    }
                }
            }

            _loadedRows = loaded;
            _historyRows = _model
                .Settings.AppLoadHistory.Select(f => new AppLoadRow(f, KindOf(f), null, false))
                .ToList();
            _chosenRows = Array.Empty<AppLoadRow>();
            Raise(nameof(LoadedRows));
            Raise(nameof(HistoryRows));
            Raise(nameof(Rows));
            RaiseCanLoad();
            RaiseAction();
        }

        private static string KindOf(string path)
        {
            string extension = Path.GetExtension(path);
            return string.Equals(extension, ".lsp", StringComparison.OrdinalIgnoreCase) ? LispKind
                : string.Equals(extension, ".dll", StringComparison.OrdinalIgnoreCase) ? PluginKind
                : "File";
        }

        private void SetStatus(string text, AppLoadStatusKind kind)
        {
            _status = text;
            _statusKind = kind;
            Raise(nameof(Status));
            Raise(nameof(StatusKind));
        }

        private void RaiseCanLoad() => Raise(nameof(CanLoad));

        private void RaiseAction()
        {
            Raise(nameof(ActionLabel));
            Raise(nameof(CanAct));
            Raise(nameof(ActionToolTip));
        }
    }
}
