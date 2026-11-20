using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
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

    /// <summary>A folder or a file of the list of Load/Unload Applications: how the list shows it.</summary>
    public sealed class AppLoadEntry
    {
        internal AppLoadEntry(FileSystemInfo info)
        {
            IsFolder = info is DirectoryInfo;
            Path = info.FullName;
            Name = info.Name;
            Type = IsFolder ? "File folder" : TypeOf(info.Extension);
            Modified = info.LastWriteTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        }

        /// <summary>Gets the full path.</summary>
        public string Path { get; }

        /// <summary>Gets the name, with its extension for a file.</summary>
        public string Name { get; }

        /// <summary>Gets whether it is a folder.</summary>
        public bool IsFolder { get; }

        /// <summary>Gets what it is, as the list's Type column says it: File folder, AutoLISP file, Plugin, Compiled AutoLISP, or the extension's words.</summary>
        public string Type { get; }

        /// <summary>Gets when it was last written, as <c>2026-11-20 10:31</c>.</summary>
        public string Modified { get; }

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
    /// AutoCAD 2021's Load/Unload Applications, shown by APPLOAD: a folder browser of its own (Look in, Back, Up, the files of
    /// the type chosen) over Load, Add to History, the Loaded Applications and History list tabs with their Unload or Remove
    /// button, and the Startup Suite. Load takes a file typed or chosen into the document shown, an AutoLISP file as
    /// <see cref="CadCommandSession.LoadLisp"/> does and a .dll through the plugin manager; the footer says what came of the
    /// last Load or Unload. The History list, the folder last used and Add to History are kept in Global settings
    /// (<see cref="GlobalSettings.AppLoadHistory"/>, <see cref="GlobalSettings.AppLoadFolder"/>,
    /// <see cref="GlobalSettings.AppLoadAddToHistory"/>).
    /// </summary>
    public sealed class AppLoadViewModel : ObservableObject
    {
        private const string LispKind = "AutoLISP";
        private const string PluginKind = "Plugin";

        private readonly MainViewModel _model;
        private readonly Stack<string> _back = new Stack<string>();
        private readonly HashSet<string> _switchedOff = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
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

        internal AppLoadViewModel(MainViewModel model)
        {
            _model = model;
            FileTypes = new[]
            {
                new AppLoadFileType("AutoLISP Files (*.lsp)", ".lsp"),
                new AppLoadFileType("Plugins (*.dll)", ".dll"),
                new AppLoadFileType("All files (*.*)", null),
            };
            _fileType = FileTypes[0];
            StartupSuite = new StartupSuiteViewModel(model);
            Navigate(StartFolder());
            RefreshRows();
        }

        /// <summary>Gets the full path of the folder the list shows.</summary>
        public string Folder => _folder;

        /// <summary>Gets the folders from the root down to <see cref="Folder"/>, the drop-down of Look in.</summary>
        public IReadOnlyList<string> FolderChain => _folderChain;

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

        /// <summary>Gets the folders, then the files of the type chosen, in the folder shown, each group by name in ordinal order, either case the same.</summary>
        public IReadOnlyList<AppLoadEntry> Entries => _entries;

        /// <summary>Gets the three file types: AutoLISP Files, Plugins and All files.</summary>
        public IReadOnlyList<AppLoadFileType> FileTypes { get; }

        /// <summary>Gets or sets the file type shown; AutoLISP Files at the start.</summary>
        public AppLoadFileType FileType
        {
            get => _fileType;
            set
            {
                if (value != null && Set(ref _fileType, value))
                {
                    Refresh();
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

        /// <summary>Gets whether Up can go: the folder shown is not a root.</summary>
        public bool CanUp => Directory.GetParent(_folder) != null;

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

            if (full == null || string.Equals(full, _folder, StringComparison.Ordinal))
            {
                return false;
            }

            _back.Push(_folder);
            Navigate(full);
            return true;
        }

        /// <summary>Goes back to the folder shown before this one; nothing where there is none.</summary>
        public void Back()
        {
            if (_back.Count > 0)
            {
                Navigate(_back.Pop());
            }
        }

        /// <summary>Goes up one level, as <see cref="OpenFolder"/> does; nothing in a root.</summary>
        public void Up()
        {
            DirectoryInfo parent = Directory.GetParent(_folder);

            if (parent != null)
            {
                OpenFolder(parent.FullName);
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
        /// Loads the files, in the order of the list: those the History list rows chosen name, or those the File name box names
        /// (a typed name is looked for in the folder shown). Each is loaded as the command line would, an AutoLISP file into
        /// the drawing shown and a .dll through the plugin manager; .arx, .dbx, .dvb, .fas and .vlx files are refused and any
        /// other type said not to be one or the other. With Add to History ticked each file given goes first in the History
        /// list, loaded or not, and a load from the folder shown keeps that folder for next time. The footer shows the last
        /// file's outcome. Nothing happens where <see cref="CanLoad"/> is false.
        /// </summary>
        public void Load()
        {
            if (!CanLoad)
            {
                return;
            }

            List<string> named = (_historyTab ? _chosenRows.Select(r => r.FullPath) : TypedPaths()).ToList();

            if (named.Count == 0)
            {
                return;
            }

            var given = new List<string>();
            AppLoadResult last = null;

            foreach (string path in named)
            {
                if (!TryFullPath(path, out string full))
                {
                    last = new AppLoadResult(AppLoadStatusKind.Refused, path + " is not a valid file name.");
                    continue;
                }

                given.Add(full);
                last = _model.LoadApplication(full);
            }

            bool fromFolder = !_historyTab;
            string folder = _folder;
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

                    if (fromFolder)
                    {
                        s.AppLoadFolder = folder;
                    }
                },
                "APPLOAD loaded files"
            );

            if (last != null)
            {
                SetStatus(last.Message, last.Kind);
            }

            RefreshRows();
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

        /// <summary>Lists the folder shown again, as after a file was written there.</summary>
        public void Refresh()
        {
            var entries = new List<AppLoadEntry>();

            try
            {
                foreach (FileSystemInfo info in new DirectoryInfo(_folder).EnumerateFileSystemInfos())
                {
                    if ((info.Attributes & (FileAttributes.Hidden | FileAttributes.System)) != 0)
                    {
                        continue;
                    }

                    if (
                        info is DirectoryInfo
                        || _fileType.Extension == null
                        || string.Equals(info.Extension, _fileType.Extension, StringComparison.OrdinalIgnoreCase)
                    )
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

        private void Navigate(string path)
        {
            _folder = FullFolder(path) ?? path;
            var chain = new List<string>();

            for (string p = _folder; p != null; p = Path.GetDirectoryName(p))
            {
                chain.Insert(0, p);
            }

            _folderChain = chain;
            _fileName = string.Empty;
            Refresh();
            Raise(nameof(Folder));
            Raise(nameof(FolderChain));
            Raise(nameof(LookIn));
            Raise(nameof(FileName));
            Raise(nameof(CanBack));
            Raise(nameof(CanUp));
            RaiseCanLoad();
        }

        /// <summary>The paths the File name box names: each name in quotes, or the whole text as one name; a name with no folder is in the folder shown.</summary>
        private IEnumerable<string> TypedPaths()
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

            return names.Select(InFolder).ToList();
        }

        /// <summary>The name as it is where it is a full path, else in the folder shown; a name with a character no path may hold is left as it is, for the load to refuse.</summary>
        private string InFolder(string name)
        {
            try
            {
                return Path.IsPathRooted(name) ? name : Path.Combine(_folder, name);
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
