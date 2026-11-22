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
        internal AppLoadResult(AppLoadStatusKind kind, string message)
        {
            Kind = kind;
            Message = message;
        }

        internal AppLoadStatusKind Kind { get; }

        internal string Message { get; }
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

        internal void ShowAtStart(bool value)
        {
            _atStart = value;
            Raise(nameof(AtStart));
        }

        /// <inheritdoc/>
        public override string ToString() => Name;
    }

    /// <summary>
    /// Load/Unload Applications, shown by APPLOAD and by the Plugin manager button: the AutoLISP files loaded into the
    /// document shown, those of the Startup Suite, and the plugins known in this session, in one table (On, Name, Kind,
    /// Commands, File, At start) sorted by name and then by file, with chips that filter it by kind. Load takes .lsp and .dll
    /// files (an AutoLISP file as <see cref="CadCommandSession.LoadLisp"/> does, a .dll through the plugin manager as NETLOAD
    /// does), Reload loads the rows chosen again and Unload unloads them: a plugin as the plugin manager does, an AutoLISP file
    /// by leaving the list and losing the <c>c:</c> commands it defined, which become unknown commands; AutoLISP can unload no
    /// more than that. What each action did is a line of the command history. The folder of the last Load is kept in Global
    /// settings (<see cref="GlobalSettings.AppLoadFolder"/>), the Startup Suite in <see cref="GlobalSettings.LispStartupSuite"/>.
    /// It follows the plugin manager while open and lets go of it when disposed.
    /// </summary>
    public sealed class AppLoadViewModel : ObservableObject, IDisposable
    {
        /// <summary>The filter of the file dialog Load… shows: applications (.lsp and .dll), AutoLISP, plugins, and all files.</summary>
        public const string OpenFilter =
            "Applications (*.lsp;*.dll)|*.lsp;*.dll|Lisp files (*.lsp)|*.lsp|Plugins (*.dll)|*.dll|All files (*.*)|*.*";

        private const string LispKind = "Lisp";
        private const string PluginKind = "Plugin";

        private readonly MainViewModel _model;
        private IReadOnlyList<AppLoadRow> _all = Array.Empty<AppLoadRow>();
        private IReadOnlyList<AppLoadRow> _rows = Array.Empty<AppLoadRow>();
        private IReadOnlyList<AppLoadRow> _chosen = Array.Empty<AppLoadRow>();
        private string _filter = "All";

        internal AppLoadViewModel(MainViewModel model)
        {
            _model = model;
            LoadCommand = new DelegateCommand(_ => Load());
            ReloadCommand = new DelegateCommand(_ => Reload(), _ => _chosen.Count > 0);
            UnloadCommand = new DelegateCommand(_ => Unload(), _ => _chosen.Count > 0);
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

        /// <summary>Gets the command of Load….</summary>
        public ICommand LoadCommand { get; }

        /// <summary>Gets the command of Reload, which needs a row chosen.</summary>
        public ICommand ReloadCommand { get; }

        /// <summary>Gets the command of Unload, which needs a row chosen.</summary>
        public ICommand UnloadCommand { get; }

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
            foreach (AppLoadRow row in _chosen)
            {
                if (row.IsPlugin)
                {
                    _model.ReloadPlugin(row.PluginName);
                }
                else
                {
                    LoadLisp(row.File);
                }
            }

            Refresh();
        }

        /// <summary>
        /// Unload: unloads the rows chosen that are loaded. A plugin is unloaded as PLUGINUNLOAD does. An AutoLISP file leaves
        /// the list, unless it is in the Startup Suite, when it stays unticked so that it can be taken out of the suite; the
        /// <c>c:</c> commands it defined are removed from the interpreter, and the other functions it defined stay.
        /// </summary>
        public void Unload()
        {
            foreach (AppLoadRow row in _chosen.Where(r => r.IsOn))
            {
                UnloadRow(row);
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
                if (row.IsPlugin)
                {
                    _model.LoadPlugin(row.File);
                }
                else
                {
                    LoadLisp(row.File);
                }
            }
            else
            {
                UnloadRow(row);
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
                _model.Plugins?.SetLoadAtStartup(row.PluginName, value);
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

                if (string.Equals(Path.GetExtension(full), ".lsp", StringComparison.OrdinalIgnoreCase))
                {
                    LoadLisp(full);
                }
                else
                {
                    _model.LoadApplication(full);
                }
            }

            Refresh();
            return last;
        }

        private void LoadLisp(string path)
        {
            if (_model.HasDocument)
            {
                _model.LoadApplication(path);
            }
            else
            {
                _model.Say("Open a drawing to load " + Path.GetFileName(path) + ".");
            }
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
            var rows = new List<AppLoadRow>();
            string[] suite = _model.Settings.LispStartupSuite.ToArray();
            IReadOnlyList<string> loaded = _model.LoadedLispFiles;
            IReadOnlyList<string> lisps = _model.Session?.Document.SupportRoots.ExistingOf(CadSupportKind.Lisps)
                ?? Array.Empty<string>();

            // The files loaded, then those of the Startup Suite that are not loaded now, which stay to be ticked or unticked.
            foreach (string file in loaded.Concat(suite).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                bool on = loaded.Any(f => SameFile(f, file));
                bool fixedOn = lisps.Any(folder => SameFolder(Path.GetDirectoryName(file), folder));
                IReadOnlyList<string> commands = _model.LispCommandsOf(file);
                rows.Add(
                    new AppLoadRow(
                        this,
                        LispKind,
                        Path.GetFileName(file),
                        string.Join(", ", commands),
                        file,
                        on,
                        fixedOn || suite.Any(f => SameFile(f, file)),
                        fixedOn,
                        null
                    )
                );
            }

            if (_model.Plugins != null)
            {
                foreach (CadLoadedPlugin plugin in _model.Plugins.Plugins)
                {
                    IEnumerable<string> names = plugin.Plugin?.Commands.Select(c => c.Name)
                        ?? plugin.Manifest?.Commands
                        ?? Array.Empty<string>();
                    rows.Add(
                        new AppLoadRow(
                            this,
                            PluginKind,
                            plugin.Name,
                            string.Join(", ", names),
                            plugin.Path,
                            plugin.IsLoaded,
                            plugin.IsInstalled || plugin.LoadAtStartup,
                            plugin.IsInstalled,
                            plugin.Name
                        )
                    );
                }
            }

            rows.Sort(Compare);
            _all = rows;
            Raise(nameof(AllChipText));
            Raise(nameof(LispChipText));
            Raise(nameof(PluginChipText));
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            _rows = _all.Where(r => _filter == "All" || (_filter == PluginKind) == r.IsPlugin).ToList();
            Raise(nameof(Rows));
            ChooseRows(Array.Empty<AppLoadRow>());
        }

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

        private static bool SameFile(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

        private static bool SameFolder(string a, string b) =>
            string.Equals(a?.TrimEnd('\\', '/'), b?.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase);

        private void OnPluginsChanged(object sender, EventArgs e) => Refresh();
    }
}
