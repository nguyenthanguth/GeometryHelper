using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using GeometryHelper.Cad.Plugins;

namespace GeometryHelper.Cad.Wpf
{
    /// <summary>
    /// The Manage ribbon tab, as AutoCAD 2021's for what the presenter has: Customization (User Interface and Edit Aliases open Global settings on Shortcuts, Tool Palettes shows the tool palettes), Applications
    /// (Load Manager, which opens Load/Unload Applications, greyed where the window cannot show it; Load Lisp, Load Plugin and Load
    /// Python, which ask for files; Run Script), CAD Standards
    /// (Layer Translator; Check and Configure parked, greyed) and Cleanup (Purge, Find Non-Purgeable Items, Overkill, Audit,
    /// Recover). A button starts its command through <see cref="MainViewModel.StartCommandCommand"/>, as the buttons of Home do.
    /// </summary>
    public sealed class ManageRibbonViewModel : ObservableObject
    {
        internal ManageRibbonViewModel(MainViewModel model)
        {
            Model = model;
            FindNonPurgeableCommand = new DelegateCommand(_ => model.FindNonPurgeableItems(), _ => model.HasDocument);
            model.PropertyChanged += (s, e) =>
            {
                if (
                    e.PropertyName == nameof(MainViewModel.HasDocument)
                    || e.PropertyName == nameof(MainViewModel.ShowAppLoad)
                )
                {
                    Raise(nameof(CanLoadApplication));
                    (FindNonPurgeableCommand as DelegateCommand)?.RaiseCanExecuteChanged();
                }
            };
        }

        /// <summary>Gets the window's view model, whose commands the buttons run.</summary>
        public MainViewModel Model { get; }

        /// <summary>Gets whether Load Manager can be pressed: a drawing is open and the window can show Load/Unload Applications.</summary>
        public bool CanLoadApplication => Model.HasDocument && Model.ShowAppLoad != null;

        /// <summary>Gets the command of Find Non-Purgeable Items: PURGE started, its window opening on the named items in use and why each cannot be purged.</summary>
        public ICommand FindNonPurgeableCommand { get; }
    }

    /// <summary>
    /// The Manage ribbon tab and the commands CUI, ALIASEDIT and APPLOAD, which open the window's own pages where AutoCAD opens
    /// its dialogs: Global settings on Shortcuts for the first two, Load/Unload Applications for the third.
    /// </summary>
    public sealed partial class MainViewModel
    {
        private CadBuiltInTab _manageTab;
        private Func<AppLoadViewModel, bool> _showAppLoad;
        private bool _openNonPurgeable;

        // What APPLOAD refuses to load: AutoCAD's other kinds of application, which the presenter cannot run.
        private static readonly string[] RefusedApplicationTypes = { ".arx", ".dbx", ".dvb", ".fas", ".vlx" };

        /// <summary>Gets the Manage ribbon tab, always shown, after View and before Output.</summary>
        public CadWorkspaceExtension ManageTab
        {
            get
            {
                if (_manageTab == null)
                {
                    _manageTab = new CadBuiltInTab("Manage", new ManageRibbonViewModel(this));
                    _builtInTabs.Add(_manageTab);
                }

                return _manageTab;
            }
        }

        /// <summary>
        /// Adds CUI and ALIASEDIT, which show Global settings on the Shortcuts page (the aliases and the keys the presenter lets
        /// a drafter change), APPLOAD, which shows Load/Unload Applications and says so where the window cannot, and LISPLOAD,
        /// which asks for AutoLISP files in a file dialog and loads them.
        /// </summary>
        private void AddManageWindowCommands(CadCommandRegistry registry)
        {
            registry.Add(new CadCommand("CUI", c => RunWindow(() => RequestSettings("Shortcuts"))));
            registry.Add(new CadCommand("ALIASEDIT", c => RunWindow(() => RequestSettings("Shortcuts"))));
            registry.Add(new CadCommand("APPLOAD", c => RunWindow(LoadApplications)));
            registry.Add(new CadCommand("LISPLOAD", c => RunWindow(LoadLispByDialog)));
        }

        /// <summary>
        /// Gets or sets how the window asks for the files of LISPLOAD: given the filter (<see cref="AppLoadViewModel.LispFilter"/>)
        /// and the folder of the last load, it shows an Open dialog with several files allowed and gives the paths chosen; null
        /// where none was. Null where no window shows one, when LISPLOAD says so.
        /// </summary>
        public Func<string, string, IReadOnlyList<string>> AskLispFiles { get; set; }

        /// <summary>
        /// LISPLOAD: asks for AutoLISP files with <see cref="AskLispFiles"/> and loads them in the order chosen, as the Load…
        /// button of Load/Unload Applications does, the folder chosen kept for the next load.
        /// </summary>
        private void LoadLispByDialog()
        {
            if (AskLispFiles == null)
            {
                Say("LISPLOAD needs a file dialog, which the window cannot show here.");
                return;
            }

            IReadOnlyList<string> paths = AskLispFiles(AppLoadViewModel.LispFilter, Settings.AppLoadFolder);

            if (paths != null && paths.Count > 0)
            {
                using (var loading = new AppLoadViewModel(this))
                {
                    loading.Load(paths);
                }
            }
        }

        /// <summary>
        /// Find Non-Purgeable Items of the Manage tab: PURGE started, the window opening on its second view, the named items in use,
        /// which the command's own Purgeable Items view starts on.
        /// </summary>
        internal void FindNonPurgeableItems()
        {
            _openNonPurgeable = true;

            try
            {
                StartCommand("PURGE");
            }
            finally
            {
                _openNonPurgeable = false;
            }
        }

        /// <summary>
        /// Gets or sets how the window shows Load/Unload Applications, over the view model it is given, until the dialog is
        /// closed; the window is modal. It is the window of APPLOAD and of the Load Manager button. Null where no window
        /// does, when APPLOAD says so.
        /// </summary>
        public Func<AppLoadViewModel, bool> ShowAppLoad
        {
            get => _showAppLoad;
            set => Set(ref _showAppLoad, value);
        }

        /// <summary>
        /// APPLOAD, and the Load Manager button: shows Load/Unload Applications with <see cref="ShowAppLoad"/>, which lists
        /// the AutoLISP files and the plugins loaded. A window with no such dialog says that it cannot show it.
        /// </summary>
        private void LoadApplications()
        {
            if (ShowAppLoad == null)
            {
                Say("Load/Unload Applications cannot be shown here.");
                return;
            }

            ShowAppLoad(new AppLoadViewModel(this));
        }

        /// <summary>
        /// Loads one application as the Load button of Load/Unload Applications does, and says what came of it on the command
        /// line: an AutoLISP file into the document's interpreter (what it wrote, then <c>name.lsp successfully loaded.</c>, or
        /// the <c>; error:</c> line), a .py file into Python (see <see cref="LoadPythonApplication"/>), a .dll into the plugin manager;
        /// <c>.arx</c>, <c>.dbx</c>, <c>.dvb</c>, <c>.fas</c> and <c>.vlx</c> files are refused, and any other type is said to be
        /// neither an AutoLISP file, a Python file nor a plugin.
        /// </summary>
        /// <param name="path">The full path.</param>
        /// <returns>The kind of outcome and the line the footer shows.</returns>
        internal AppLoadResult LoadApplication(string path)
        {
            string name = System.IO.Path.GetFileName(path);
            string extension = System.IO.Path.GetExtension(path).ToLowerInvariant();

            if (extension == ".lsp")
            {
                int before = _current.Session.History.Count;
                bool loaded = _current.Session.LoadLisp(path);
                SyncHistory();

                if (loaded)
                {
                    return new AppLoadResult(AppLoadStatusKind.Loaded, name + " successfully loaded.");
                }

                string error = _current
                    .Session.History.Skip(before)
                    .LastOrDefault(l =>
                        l.StartsWith("; error:", StringComparison.Ordinal) || l == "Can't reenter LISP."
                    )
                    ?? string.Empty;
                return new AppLoadResult(
                    AppLoadStatusKind.Failed,
                    (name + " was not loaded: " + error).TrimEnd(),
                    name,
                    error
                );
            }

            if (extension == ".py")
            {
                return LoadPythonApplication(path, name);
            }

            if (extension == ".dll")
            {
                if (_plugins == null)
                {
                    const string none = "No plugin manager is in use: applications cannot be loaded.";
                    Say(none);
                    return new AppLoadResult(AppLoadStatusKind.Failed, none, name, none);
                }

                CadPluginResult result = LoadPlugin(path);
                return new AppLoadResult(
                    result.Succeeded ? AppLoadStatusKind.Loaded : AppLoadStatusKind.Failed,
                    result.ToString(),
                    name,
                    result.ToString()
                );
            }

            string why =
                Array.IndexOf(RefusedApplicationTypes, extension) >= 0
                    ? " cannot be loaded: .arx, .dbx, .dvb, .fas and .vlx files are not supported."
                    : " is not an AutoLISP file, a Python file or a plugin.";
            Say(name + why);
            return new AppLoadResult(AppLoadStatusKind.Refused, name + why, name, why);
        }

        /// <summary>
        /// A <c>.py</c> file of <see cref="LoadApplication"/>: loaded into the drawing shown as PYLOAD does, which writes its own
        /// line (<c>tools.py loaded: C1, DOT.</c>) to the command line; the footer says <c>tools.py successfully loaded.</c>, or
        /// <c>tools.py failed to load:</c> and the last line the load wrote (the error, or why Python could not run it).
        /// </summary>
        private AppLoadResult LoadPythonApplication(string path, string name)
        {
            string reason;

            if (PythonFiles == null)
            {
                reason = "Python is not in use: applications cannot be loaded.";
                Say(reason);
            }
            else if (_current == null)
            {
                reason = "Open a drawing to load " + name + ".";
                Say(reason);
            }
            else
            {
                int before = _current.Session.History.Count;
                bool loaded = PythonFiles.Load(_current.Session, path);
                SyncHistory();

                if (loaded)
                {
                    return new AppLoadResult(AppLoadStatusKind.Loaded, name + " successfully loaded.");
                }

                const string prefix = "Python error: ";
                reason = _current.Session.History.Skip(before).LastOrDefault() ?? string.Empty;

                if (reason.StartsWith(prefix, StringComparison.Ordinal))
                {
                    reason = reason.Substring(prefix.Length);
                }
            }

            return new AppLoadResult(AppLoadStatusKind.Failed, name + " failed to load: " + reason, name, reason);
        }

        /// <summary>Gets the AutoLISP files loaded into the document shown, as full paths in the order they loaded; empty with no document.</summary>
        internal IReadOnlyList<string> LoadedLispFiles =>
            _current?.Session.LoadedLispFiles ?? (IReadOnlyList<string>)Array.Empty<string>();

        /// <summary>Gets the AutoLISP files of the document shown whose load failed after they defined a command, as full paths; their commands stay bound and a row lists them; empty with no document.</summary>
        internal IReadOnlyList<string> FailedLispFiles =>
            _current?.Session.FailedLispFiles ?? (IReadOnlyList<string>)Array.Empty<string>();

        /// <summary>Gets the commands a loaded AutoLISP file defined in the document shown, without <c>c:</c>, in the order it defined them; empty with no document.</summary>
        internal IReadOnlyList<string> LispCommandsOf(string path) =>
            _current?.Session.LispCommandsOf(path) ?? (IReadOnlyList<string>)Array.Empty<string>();

        /// <summary>Unloads an AutoLISP file from the document shown: it leaves the loaded files and its <c>c:</c> commands are unknown again.</summary>
        internal bool UnloadLispFile(string path) => _current != null && _current.Session.UnloadLisp(path);

        /// <summary>
        /// Keeps the applications in use in <see cref="GlobalSettings.KnownApplications"/>, so that Load/Unload Applications still
        /// lists them once unloaded: each AutoLISP file loaded or failed half way in the document shown, with the commands it
        /// defines now, and each plugin of the manager that is not of the plugin folder (loaded now, or unloaded with the
        /// commands it last had). Files of the Lisps folder are left out, since the window lists them anyway. The settings are
        /// saved only where something changed. Called after the history grows (a <c>(load)</c> says something), after a plugin
        /// is loaded or unloaded, and when the window lists its rows.
        /// </summary>
        internal void RememberApplications()
        {
            var known = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (KeyValuePair<string, string> pair in _settings.KnownApplications)
            {
                known[pair.Key] = pair.Value;
            }

            bool changed = false;
            IReadOnlyList<string> lisps = _current?.Session.Document.SupportRoots.ExistingOf(CadSupportKind.Lisps)
                ?? Array.Empty<string>();

            foreach (string file in LoadedLispFiles.Concat(FailedLispFiles))
            {
                string full = AppLoadViewModel.Normal(file);

                if (!lisps.Any(folder => AppLoadViewModel.IsUnder(full, folder)))
                {
                    changed |= Keep(known, full, string.Join(", ", LispCommandsOf(full)));
                }
            }

            IReadOnlyList<string> pythons = _current?.Session.Document.SupportRoots.ExistingOf(CadSupportKind.Pythons)
                ?? Array.Empty<string>();

            foreach (string file in LoadedPythonFiles)
            {
                string full = AppLoadViewModel.Normal(file);

                if (!pythons.Any(folder => AppLoadViewModel.IsDirectlyIn(full, folder)))
                {
                    // The file is kept so that its row stays, unticked, after a restart; a Python row names its commands only while it is loaded.
                    changed |= Keep(known, full, string.Empty);
                }
            }

            if (_plugins != null)
            {
                foreach (CadLoadedPlugin plugin in _plugins.Plugins.Where(p => !p.IsInstalled))
                {
                    known.TryGetValue(plugin.Path, out string before);
                    string commands = plugin.IsLoaded
                        ? string.Join(", ", AppLoadViewModel.CommandsOf(plugin))
                        : before ?? string.Join(", ", AppLoadViewModel.CommandsOf(plugin));
                    changed |= Keep(known, plugin.Path, commands);
                }
            }

            if (changed)
            {
                ChangeSettings(s => s.SetKnownApplications(known), "the applications in use were remembered");
            }
        }

        private static bool Keep(IDictionary<string, string> known, string file, string commands)
        {
            if (known.TryGetValue(file, out string before) && before == commands)
            {
                return false;
            }

            known[file] = commands;
            return true;
        }

        /// <summary>
        /// Changes the settings in use in place and saves them to <see cref="SettingsPath"/>, as the dialogs that keep a choice
        /// of their own do (Do not show again); a file that cannot be written is logged and the change stays for this run.
        /// </summary>
        /// <param name="change">What to change.</param>
        /// <param name="what">What was changed, for the log line.</param>
        internal void ChangeSettings(Action<GlobalSettings> change, string what)
        {
            change(_settings);

            try
            {
                _settings.Save(SettingsPath);
            }
            catch (Exception ex)
                when (ex is System.IO.IOException
                    || ex is UnauthorizedAccessException
                    || ex is ArgumentException
                    || ex is NotSupportedException
                )
            {
                GeometryHelperLog.Warn("MainViewModel: Global settings were not saved after " + what + ".", ex);
            }
        }
    }
}
