using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Input;
using GeometryHelper.Cad.Plugins;

namespace GeometryHelper.Cad.Wpf
{
    /// <summary>How the outcome of loading one application is told: loaded, refused for its type, or failed.</summary>
    public enum AppLoadStatusKind
    {
        /// <summary>Nothing to say yet.</summary>
        None,

        /// <summary>A file was loaded.</summary>
        Loaded,

        /// <summary>A file was refused, for its type or its name.</summary>
        Refused,

        /// <summary>A load failed.</summary>
        Failed,

        /// <summary>Something done that is neither.</summary>
        Info,
    }

    /// <summary>What came of loading one application: how to show it, and the line to show.</summary>
    internal sealed class AppLoadResult
    {
        internal AppLoadResult(AppLoadStatusKind kind, string message, string name = null, string detail = null)
        {
            Kind = kind;
            Message = message;
            Name = name;
            Detail = detail;
        }

        internal AppLoadStatusKind Kind { get; }

        internal string Message { get; }

        /// <summary>Gets the file's name, which the load-result line of the window shows in bold; null where the load was of no file.</summary>
        internal string Name { get; }

        /// <summary>
        /// Gets what the load-result line says after the name: the error the history wrote for a failed load, or the sentence
        /// after the name of a refusal.
        /// </summary>
        internal string Detail { get; }
    }

    /// <summary>
    /// A row of Load/Unload Applications: an AutoLISP file or a plugin, with whether it is on, the commands it defines, the file,
    /// and whether it loads at every start. Ticking On loads it and unticking unloads it; ticking At start keeps it for every
    /// start, except where the file lies in the program's Lisps or Plugins folder, which always load, and its box is ticked and
    /// disabled.
    /// </summary>
    public sealed class AppLoadRow : ObservableObject
    {
        private readonly AppLoadViewModel _owner;

        internal AppLoadRow(
            AppLoadViewModel owner,
            string kind,
            string name,
            string commands,
            string file,
            bool isOn,
            bool atStart,
            bool isAtStartFixed,
            string pluginName
        )
        {
            _owner = owner;
            Kind = kind;
            Name = name;
            Commands = commands;
            File = file;
            _isOn = isOn;
            _atStart = atStart;
            IsAtStartFixed = isAtStartFixed;
            PluginName = pluginName;
        }

        /// <summary>Gets the kind, <c>Lisp</c> (shown green) or <c>Plugin</c> (shown blue).</summary>
        public string Kind { get; }

        /// <summary>Gets whether it is a plugin.</summary>
        public bool IsPlugin => PluginName != null;

        /// <summary>Gets the file's name for an AutoLISP file, the plugin's name for a plugin.</summary>
        public string Name { get; }

        /// <summary>
        /// Gets the commands it defines, separated by commas, in the order they were defined: the <c>c:</c> functions the file
        /// defined while it loaded, or the commands the plugin registers; empty where there are none.
        /// </summary>
        public string Commands { get; }

        /// <summary>Gets the full path of the file.</summary>
        public string File { get; }

        /// <summary>Gets the plugin's name; null for an AutoLISP file.</summary>
        internal string PluginName { get; }

        /// <summary>Gets or sets whether it is loaded: unticking is Unload, ticking loads the file again.</summary>
        public bool IsOn
        {
            get => _isOn;
            set => _owner.SetOn(this, value);
        }

        private bool _isOn;

        /// <summary>Gets or sets whether it loads at every start: the Startup Suite for an AutoLISP file, the startup list for a plugin.</summary>
        public bool AtStart
        {
            get => _atStart;
            set => _owner.SetAtStart(this, value);
        }

        private bool _atStart;

        /// <summary>Gets whether the file always loads, and At start is ticked and cannot be changed.</summary>
        public bool IsAtStartFixed { get; }

        /// <summary>Gets whether the box of At start can be changed.</summary>
        public bool CanChangeAtStart => !IsAtStartFixed;

        /// <summary>Gets the tooltip of the On box, which says what a click does to this row: unload it where it is on, load it where it is off.</summary>
        public string OnToolTip => _isOn ? "Loaded: untick to unload it" : "Not loaded: tick to load it again";

        internal void ShowAtStart(bool value)
        {
            _atStart = value;
            Raise(nameof(AtStart));
        }

        /// <inheritdoc/>
        public override string ToString() => Name;
    }

    /// <summary>
    /// <para>
    /// Load/Unload Applications, shown by APPLOAD and by the Plugin manager button: the AutoLISP files loaded into the
    /// document shown, those of the Startup Suite, and the plugins known in this session, in one table (On, Name, Kind,
    /// Commands, File, At start) sorted by name and then by file, with chips that filter it by kind. Load takes .lsp and .dll
    /// files (an AutoLISP file as <see cref="CadCommandSession.LoadLisp"/> does, a .dll through the plugin manager as NETLOAD
    /// does), Reload loads the rows chosen again and Unload unloads them: a plugin as the plugin manager does, an AutoLISP file
    /// by losing the <c>c:</c> commands it defined, which become unknown commands, its row staying in the table unticked;
    /// AutoLISP can unload no more than that. What each action did is a line of the command history. The folder of the last Load is kept in Global
    /// settings (<see cref="GlobalSettings.AppLoadFolder"/>), the Startup Suite in <see cref="GlobalSettings.LispStartupSuite"/>.
    /// </para>
    /// <para>
    /// Every file loaded is also kept in <see cref="GlobalSettings.KnownApplications"/>, with the commands it last defined, so
    /// that an unloaded file stays in the table, unticked, in a new window and after a restart of the app, as AutoCAD keeps its
    /// History list; ticking it loads it. Remove unloads the rows chosen and takes them out of the table, the known list and the
    /// Startup Suite (a plugin is forgotten by the plugin manager, its At start entry too); a file of the Lisps or Plugins
    /// folder cannot be removed, since it loads at every start.
    /// </para>
    /// <para>
    /// A load that fails (a .lsp or a .dll, from Load…, a drop, Reload or the On box), a file type refused, or Load… with no
    /// drawing open is also told by a red line above the hint, until the next load that has no failure
    /// (<see cref="HasLoadError"/>). It follows the plugin manager while open and lets go of it when disposed.
    /// </para>
    /// </summary>
    public sealed class AppLoadViewModel : ObservableObject, IDisposable
    {
        /// <summary>The filter of the file dialog Load… shows: applications (.lsp and .dll), AutoLISP, plugins, and all files.</summary>
        public const string OpenFilter =
            "Applications (*.lsp;*.dll)|*.lsp;*.dll|Lisp files (*.lsp)|*.lsp|Plugins (*.dll)|*.dll|All files (*.*)|*.*";

        /// <summary>The filter of the file dialog LISPLOAD shows: AutoLISP files.</summary>
        public const string LispFilter = "Lisp files (*.lsp)|*.lsp";

        private const string LispKind = "Lisp";
        private const string PluginKind = "Plugin";

        private readonly MainViewModel _model;
        private IReadOnlyList<AppLoadRow> _all = Array.Empty<AppLoadRow>();
        private IReadOnlyList<AppLoadRow> _rows = Array.Empty<AppLoadRow>();
        private IReadOnlyList<AppLoadRow> _chosen = Array.Empty<AppLoadRow>();
        private string _filter = "All";
        private string _loadErrorName;
        private string _loadErrorText;

        // The failures of the load going on, in order: the file's name and what to say after it. The line is made from them when it ends.
        private List<KeyValuePair<string, string>> _failures;

        // The AutoLISP files unticked in this window, by full path: they stay in the table, off, until it closes; Unload keeps them too.
        private readonly HashSet<string> _off = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        internal AppLoadViewModel(MainViewModel model)
        {
            _model = model;
            LoadCommand = new DelegateCommand(_ => Load());
            ReloadCommand = new DelegateCommand(_ => Reload(), _ => _chosen.Count > 0);
            UnloadCommand = new DelegateCommand(_ => Unload(), _ => _chosen.Count > 0);
            RemoveCommand = new DelegateCommand(_ => Remove(), _ => _chosen.Any(r => !r.IsAtStartFixed));
            FilterCommand = new DelegateCommand(p => Filter = p as string ?? "All");

            if (model.Plugins != null)
            {
                model.Plugins.PluginsChanged += OnPluginsChanged;
            }

            Refresh();
        }

        /// <summary>
        /// Gets or sets how Load… asks for files: it shows an Open dialog with <see cref="OpenFilter"/>, several files allowed,
        /// opened on the folder of the last Load, and gives the paths chosen; null where none was. Not set, Load… does nothing.
        /// </summary>
        public Func<string, IReadOnlyList<string>> AskFiles { get; set; }

        /// <summary>Gets the rows shown, as the chip chosen filters them, sorted by name and then by file.</summary>
        public IReadOnlyList<AppLoadRow> Rows => _rows;

        /// <summary>Gets or sets the chip chosen: <c>All</c>, <c>Lisp</c> or <c>Plugin</c>.</summary>
        public string Filter
        {
            get => _filter;
            set
            {
                if (Set(ref _filter, value ?? "All"))
                {
                    ApplyFilter();
                }
            }
        }

        /// <summary>Gets the chip of every row, with their number: <c>All 4</c>.</summary>
        public string AllChipText => "All " + _all.Count.ToString(CultureInfo.InvariantCulture);

        /// <summary>Gets the chip of the AutoLISP files, with their number: <c>Lisp 3</c>.</summary>
        public string LispChipText => "Lisp " + _all.Count(r => !r.IsPlugin).ToString(CultureInfo.InvariantCulture);

        /// <summary>Gets the chip of the plugins, with their number: <c>Plugins 1</c>.</summary>
        public string PluginChipText => "Plugins " + _all.Count(r => r.IsPlugin).ToString(CultureInfo.InvariantCulture);

        /// <summary>Gets the rows chosen, in the order of the table.</summary>
        public IReadOnlyList<AppLoadRow> Chosen => _chosen;

        /// <summary>
        /// Gets whether the load-result line is shown: the last load, a batch of files or one, had a failure. It starts hidden,
        /// is not shown for a load that succeeded, and goes when the next load has no failure.
        /// </summary>
        public bool HasLoadError => _loadErrorName != null;

        /// <summary>Gets the file the load-result line names, shown in bold: the last file that failed; empty where the line has no file to name.</summary>
        public string LoadErrorName => _loadErrorName ?? string.Empty;

        /// <summary>
        /// Gets what the line says after the name: <c> failed to load: ; error: bad argument type: consp 5</c>, the error text
        /// as the command history wrote it, then for an AutoLISP file that defined commands first <c> (ERRONE was defined
        /// before the error)</c>; the sentence of a refusal, or <c>Open a drawing to load NAME.</c>; and <c> (and 2 more)</c>
        /// where three files failed in the load. Empty while the line is hidden.
        /// </summary>
        public string LoadErrorText => _loadErrorText ?? string.Empty;

        /// <summary>Gets the whole line, the name and then its text; empty while the line is hidden.</summary>
        public string LoadError => LoadErrorName + LoadErrorText;

        /// <summary>Gets the command of Load….</summary>
        public ICommand LoadCommand { get; }

        /// <summary>Gets the command of Reload, which needs a row chosen.</summary>
        public ICommand ReloadCommand { get; }

        /// <summary>Gets the command of Unload, which needs a row chosen.</summary>
        public ICommand UnloadCommand { get; }

        /// <summary>Gets the command of Remove, which needs a row chosen that is not of the Lisps or Plugins folder.</summary>
        public ICommand RemoveCommand { get; }

        /// <summary>
        /// Gets the tooltip of Remove: what it does, or, where every row chosen lies in the Lisps or Plugins folder, why it
        /// cannot be pressed.
        /// </summary>
        public string RemoveToolTip =>
            _chosen.Count > 0 && _chosen.All(r => r.IsAtStartFixed)
                ? "Files in the Lisps and Plugins folders load at every start, so they cannot be taken out of the list."
                : "Unload the chosen files and take them out of the list";

        /// <summary>Gets the command of a filter chip, given All, Lisp or Plugin.</summary>
        public ICommand FilterCommand { get; }

        /// <summary>Takes the rows chosen in the table, which Reload and Unload act on.</summary>
        /// <param name="chosen">The rows chosen.</param>
        public void ChooseRows(IEnumerable<AppLoadRow> chosen)
        {
            var wanted = new HashSet<AppLoadRow>(chosen ?? Enumerable.Empty<AppLoadRow>());
            _chosen = _rows.Where(wanted.Contains).ToList();
            Raise(nameof(Chosen));
            ((DelegateCommand)ReloadCommand).RaiseCanExecuteChanged();
            ((DelegateCommand)UnloadCommand).RaiseCanExecuteChanged();
            ((DelegateCommand)RemoveCommand).RaiseCanExecuteChanged();
            Raise(nameof(RemoveToolTip));
        }

        /// <summary>Load…: asks for files with <see cref="AskFiles"/> and loads them as <see cref="Load(IEnumerable{string})"/> does.</summary>
        public void Load()
        {
            IReadOnlyList<string> paths = AskFiles?.Invoke(_model.Settings.AppLoadFolder);

            if (paths != null && paths.Count > 0)
            {
                Load(paths);
            }
        }

        /// <summary>
        /// Loads files in the order given, each as the command line would: an AutoLISP file into the document shown (what it
        /// wrote, then <c>name.lsp successfully loaded.</c>, or the <c>; error:</c> line) and a .dll through the plugin manager,
        /// which asks about a file outside its trusted folders; <c>.arx</c>, <c>.dbx</c>, <c>.dvb</c>, <c>.fas</c> and <c>.vlx</c>
        /// files are refused and any other type is said to be neither. The folder of the last file is kept for the next Load.
        /// </summary>
        /// <param name="paths">The files.</param>
        public void Load(IEnumerable<string> paths)
        {
            string last = LoadMany(paths ?? Enumerable.Empty<string>());

            if (last != null)
            {
                string folder = Path.GetDirectoryName(last);
                _model.ChangeSettings(s => s.AppLoadFolder = folder, "APPLOAD loaded files");
            }
        }

        /// <summary>Loads files dropped on the table, from Explorer, in the order of the drop as <see cref="Load(IEnumerable{string})"/> does, though the folder kept is not changed; a folder is passed over.</summary>
        /// <param name="paths">The full paths dropped.</param>
        public void LoadDropped(IEnumerable<string> paths) =>
            LoadMany((paths ?? Enumerable.Empty<string>()).Where(p => !string.IsNullOrWhiteSpace(p) && File.Exists(p)));

        /// <summary>Reload: loads the rows chosen again, an AutoLISP file as Load does and a plugin as PLUGINRELOAD does.</summary>
        public void Reload()
        {
            BeginLoad();

            foreach (AppLoadRow row in _chosen)
            {
                if (row.IsPlugin)
                {
                    // A plugin the manager does not know yet (known from an earlier run) is loaded from its file.
                    CadLoadedPlugin managed = ManagedPlugin(row);
                    NotePlugin(
                        row.File,
                        managed != null ? _model.ReloadPlugin(managed.Name) : _model.LoadPlugin(row.File)
                    );
                }
                else
                {
                    LoadLisp(row.File);
                }
            }

            EndLoad();
            Refresh();
        }

        /// <summary>
        /// Unload: unloads the rows chosen that are loaded. A plugin is unloaded as PLUGINUNLOAD does. An AutoLISP file stays in
        /// the table, unticked, as a plugin does, so that ticking it loads it again; the <c>c:</c> commands it defined are
        /// removed from the interpreter, and the other functions it defined stay. The row stays in the table, in a new window and
        /// after a restart of the app too, until Remove takes it out.
        /// </summary>
        public void Unload()
        {
            foreach (AppLoadRow row in _chosen)
            {
                if (row.IsOn)
                {
                    UnloadRow(row);
                }

                if (!row.IsPlugin)
                {
                    _off.Add(Normal(row.File));
                }
            }

            Refresh();
        }

        /// <summary>
        /// Remove: unloads the rows chosen that are loaded, as Unload does, and takes them out of the table, out of
        /// <see cref="GlobalSettings.KnownApplications"/> and, for an AutoLISP file, out of the Startup Suite; a plugin is
        /// forgotten by the plugin manager, which takes it off its startup list too. A row of the Lisps or Plugins folder is
        /// left as it is, since it loads at every start.
        /// </summary>
        public void Remove()
        {
            List<AppLoadRow> gone = _chosen.Where(r => !r.IsAtStartFixed).ToList();

            foreach (AppLoadRow row in gone)
            {
                if (row.IsOn)
                {
                    UnloadRow(row);
                }

                if (!row.IsPlugin)
                {
                    _off.Remove(Normal(row.File));
                }
            }

            var files = new HashSet<string>(gone.Select(r => Normal(r.File)), StringComparer.OrdinalIgnoreCase);
            var lisps = new HashSet<string>(
                gone.Where(r => !r.IsPlugin).Select(r => Normal(r.File)),
                StringComparer.OrdinalIgnoreCase
            );
            _model.ChangeSettings(
                s =>
                {
                    s.SetKnownApplications(s.KnownApplications.Where(p => !files.Contains(Normal(p.Key))).ToList());
                    s.LispStartupSuite = s.LispStartupSuite.Where(f => !lisps.Contains(Normal(f))).ToList();
                },
                "applications were removed from the list"
            );

            foreach (AppLoadRow row in gone.Where(r => r.IsPlugin))
            {
                CadLoadedPlugin managed = ManagedPlugin(row);

                if (managed != null)
                {
                    _model.Plugins.Remove(managed.Name);
                }
            }

            Refresh();
        }

        /// <summary>Lets go of the plugin manager.</summary>
        public void Dispose()
        {
            if (_model.Plugins != null)
            {
                _model.Plugins.PluginsChanged -= OnPluginsChanged;
            }
        }

        /// <summary>Unticking On is Unload of the row, ticking it loads the file again.</summary>
        internal void SetOn(AppLoadRow row, bool on)
        {
            if (row.IsOn == on)
            {
                return;
            }

            if (on)
            {
                BeginLoad();

                if (row.IsPlugin)
                {
                    NotePlugin(row.File, _model.LoadPlugin(row.File));
                }
                else
                {
                    LoadLisp(row.File);
                }

                EndLoad();
            }
            else
            {
                UnloadRow(row);

                // The row stays, off, so that a second click on the same box ticks it again and no other row moves.
                if (!row.IsPlugin)
                {
                    _off.Add(Normal(row.File));
                }
            }

            Refresh();
        }

        /// <summary>Ticking At start keeps the file for every start: the Startup Suite for AutoLISP, the startup list for a plugin.</summary>
        internal void SetAtStart(AppLoadRow row, bool value)
        {
            if (row.IsAtStartFixed || row.AtStart == value)
            {
                return;
            }

            if (row.IsPlugin)
            {
                // A plugin the manager does not know yet (known from an earlier run) has no startup entry to change until it
                // is loaded: the box is put back as it was.
                CadLoadedPlugin managed = ManagedPlugin(row);

                if (managed == null)
                {
                    row.ShowAtStart(row.AtStart);
                    return;
                }

                _model.Plugins.SetLoadAtStartup(managed.Name, value);
            }
            else
            {
                string file = row.File;
                _model.ChangeSettings(
                    s =>
                        s.LispStartupSuite = value
                            ? s.LispStartupSuite.Concat(new[] { file }).ToList()
                            : s.LispStartupSuite.Where(f => !SameFile(f, file)).ToList(),
                    "the Startup Suite was changed"
                );
            }

            row.ShowAtStart(value);
        }

        private string LoadMany(IEnumerable<string> paths)
        {
            string last = null;
            BeginLoad();

            foreach (string path in paths)
            {
                string full;

                try
                {
                    full = Path.GetFullPath(path ?? string.Empty);
                }
                catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException || ex is IOException)
                {
                    _model.Say(path + " is not a valid file name.");
                    continue;
                }

                last = full;

                if (IsLisp(full))
                {
                    LoadLisp(full);
                }
                else
                {
                    Note(full, _model.LoadApplication(full));
                }
            }

            EndLoad();
            Refresh();
            return last;
        }

        private void LoadLisp(string path)
        {
            if (_model.HasDocument)
            {
                Note(path, _model.LoadApplication(path));
            }
            else
            {
                string text = "Open a drawing to load " + Path.GetFileName(path) + ".";
                _model.Say(text);
                _failures.Add(new KeyValuePair<string, string>(string.Empty, text));
            }
        }

        private static bool IsLisp(string path) =>
            string.Equals(Path.GetExtension(path), ".lsp", StringComparison.OrdinalIgnoreCase);

        /// <summary>A load begins: the failures of the one before are forgotten, and the line stays as it was until this one ends.</summary>
        private void BeginLoad() => _failures = new List<KeyValuePair<string, string>>();

        /// <summary>Keeps what a failed or refused load said, as the line will show it; a load that succeeded keeps nothing.</summary>
        private void Note(string path, AppLoadResult result)
        {
            if (result.Kind == AppLoadStatusKind.Refused)
            {
                _failures.Add(new KeyValuePair<string, string>(result.Name, result.Detail));
            }
            else if (result.Kind == AppLoadStatusKind.Failed)
            {
                string text = " failed to load: " + result.Detail;

                if (IsLisp(path))
                {
                    IReadOnlyList<string> defined = _model.LispCommandsOf(path);

                    if (defined.Count > 0)
                    {
                        text +=
                            " ("
                            + string.Join(", ", defined)
                            + (defined.Count == 1 ? " was" : " were")
                            + " defined before the error)";
                    }
                }

                _failures.Add(new KeyValuePair<string, string>(result.Name, text));
            }
        }

        /// <summary>Keeps the failure of a plugin loaded or reloaded from a row; the result is null with no plugin manager, which the history says.</summary>
        private void NotePlugin(string path, CadPluginResult result)
        {
            if (result != null && !result.Succeeded)
            {
                _failures.Add(new KeyValuePair<string, string>(Path.GetFileName(path), " failed to load: " + result));
            }
        }

        /// <summary>A load ends: the line shows the last failure of it, with the number of the others, or hides where there was none.</summary>
        private void EndLoad()
        {
            List<KeyValuePair<string, string>> failures = _failures;
            _failures = null;

            if (failures.Count == 0)
            {
                _loadErrorName = null;
                _loadErrorText = null;
            }
            else
            {
                KeyValuePair<string, string> last = failures[failures.Count - 1];
                _loadErrorName = last.Key;
                _loadErrorText =
                    last.Value
                    + (
                        failures.Count > 1
                            ? " (and " + (failures.Count - 1).ToString(CultureInfo.InvariantCulture) + " more)"
                            : string.Empty
                    );
            }

            Raise(nameof(HasLoadError));
            Raise(nameof(LoadErrorName));
            Raise(nameof(LoadErrorText));
            Raise(nameof(LoadError));
        }

        private void UnloadRow(AppLoadRow row)
        {
            if (row.IsPlugin)
            {
                _model.UnloadPlugin(row.PluginName);
            }
            else if (_model.UnloadLispFile(row.File))
            {
                _model.Say(row.Name + " unloaded.");
            }
        }

        /// <summary>Lists the files and plugins again, as after one was loaded or unloaded.</summary>
        private void Refresh()
        {
            _model.RememberApplications();
            var rows = new List<AppLoadRow>();
            string[] suite = _model.Settings.LispStartupSuite.ToArray();
            IReadOnlyDictionary<string, string> known = _model.Settings.KnownApplications;
            IReadOnlyList<string> loaded = _model.LoadedLispFiles;
            IReadOnlyList<string> lisps = _model.Session?.Document.SupportRoots.ExistingOf(CadSupportKind.Lisps)
                ?? Array.Empty<string>();

            // The files loaded, those that failed half way (their commands are bound), the files unticked here, those of the
            // Startup Suite and the AutoLISP files known from before, which stay to be ticked or unticked. A path is one file
            // however it was written.
            IReadOnlyList<string> failed = _model.FailedLispFiles;
            var on = new HashSet<string>(loaded.Concat(failed).Select(Normal), StringComparer.OrdinalIgnoreCase);
            var inSuite = new HashSet<string>(suite.Select(Normal), StringComparer.OrdinalIgnoreCase);
            var remembered = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (KeyValuePair<string, string> pair in known)
            {
                remembered[Normal(pair.Key)] = pair.Value;
            }

            foreach (
                string file in loaded
                    .Concat(failed)
                    .Concat(_off)
                    .Concat(suite)
                    .Concat(known.Keys.Where(IsLisp))
                    .Select(Normal)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
            )
            {
                bool fixedOn = lisps.Any(folder => IsUnder(file, folder));

                // A file not loaded shows the commands it last defined.
                string commands =
                    on.Contains(file) ? string.Join(", ", _model.LispCommandsOf(file))
                    : remembered.TryGetValue(file, out string last) ? last
                    : string.Empty;
                rows.Add(
                    new AppLoadRow(
                        this,
                        LispKind,
                        Path.GetFileName(file),
                        commands,
                        file,
                        on.Contains(file),
                        fixedOn || inSuite.Contains(file),
                        fixedOn,
                        null
                    )
                );
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (_model.Plugins != null)
            {
                foreach (CadLoadedPlugin plugin in _model.Plugins.Plugins)
                {
                    string names = string.Join(", ", CommandsOf(plugin));
                    seen.Add(Normal(plugin.Path));
                    rows.Add(
                        new AppLoadRow(
                            this,
                            PluginKind,
                            plugin.Name,
                            names.Length == 0 && remembered.TryGetValue(Normal(plugin.Path), out string last)
                                ? last
                                : names,
                            plugin.Path,
                            plugin.IsLoaded,
                            plugin.IsInstalled || plugin.LoadAtStartup,
                            plugin.IsInstalled,
                            plugin.Name
                        )
                    );
                }
            }

            // The plugins known from an earlier run, which the manager does not hold yet: off, to be ticked to load them.
            foreach (KeyValuePair<string, string> pair in known.Where(p => !IsLisp(p.Key)))
            {
                string name = Path.GetFileNameWithoutExtension(pair.Key.TrimEnd('\\', '/'));

                if (seen.Add(Normal(pair.Key)))
                {
                    rows.Add(new AppLoadRow(this, PluginKind, name, pair.Value, pair.Key, false, false, false, name));
                }
            }

            rows.Sort(Compare);
            _all = rows;
            Raise(nameof(AllChipText));
            Raise(nameof(LispChipText));
            Raise(nameof(PluginChipText));
            ApplyFilter();
        }

        /// <summary>The plugin of the manager the row stands for, found by its file; null where the manager does not hold it, as for a plugin known only from an earlier run.</summary>
        private CadLoadedPlugin ManagedPlugin(AppLoadRow row) =>
            _model.Plugins?.Plugins.FirstOrDefault(p => SameFile(p.Path, row.File));

        /// <summary>The names of the commands a plugin registers, or its manifest lists while it is not loaded.</summary>
        internal static IEnumerable<string> CommandsOf(CadLoadedPlugin plugin) =>
            plugin.Plugin?.Commands.Select(c => c.Name) ?? plugin.Manifest?.Commands ?? Array.Empty<string>();

        /// <summary>The rows are made anew, so the rows chosen are found again by file or plugin name and stay chosen.</summary>
        private void ApplyFilter()
        {
            var keep = new HashSet<string>(_chosen.Select(KeyOf), StringComparer.OrdinalIgnoreCase);
            _rows = _all.Where(r => _filter == "All" || (_filter == PluginKind) == r.IsPlugin).ToList();
            Raise(nameof(Rows));
            ChooseRows(_rows.Where(r => keep.Contains(KeyOf(r))));
        }

        private static string KeyOf(AppLoadRow row) => row.IsPlugin ? "P|" + row.PluginName : "L|" + Normal(row.File);

        private static int Compare(AppLoadRow a, AppLoadRow b)
        {
            int order = string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);

            if (order == 0)
            {
                order = string.Compare(a.File, b.File, StringComparison.OrdinalIgnoreCase);
            }

            if (order == 0)
            {
                order = string.CompareOrdinal(a.Name, b.Name);
            }

            if (order == 0)
            {
                order = string.CompareOrdinal(a.File, b.File);
            }

            return order != 0 ? order : string.CompareOrdinal(a.Kind, b.Kind);
        }

        private static bool SameFile(string a, string b) =>
            string.Equals(Normal(a), Normal(b), StringComparison.OrdinalIgnoreCase);

        /// <summary>The full path, so that <c>C:/a/b.lsp</c> and <c>C:\a\x\..\b.lsp</c> are one file; a name that is no path is kept as it is.</summary>
        internal static string Normal(string path)
        {
            try
            {
                return Path.GetFullPath(path);
            }
            catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException || ex is IOException)
            {
                return path;
            }
        }

        /// <summary>Whether the file lies in the folder or in any folder below it.</summary>
        internal static bool IsUnder(string file, string folder)
        {
            string root = Normal(folder)?.TrimEnd('\\', '/');
            return !string.IsNullOrEmpty(root)
                && Normal(file).StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }

        private void OnPluginsChanged(object sender, EventArgs e) => Refresh();
    }
}
