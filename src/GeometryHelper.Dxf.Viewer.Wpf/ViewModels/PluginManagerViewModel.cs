using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows.Input;
using GeometryHelper.Dxf.Viewer.Plugins;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>A plugin as the Plugin manager lists it: on, name, kind, version, commands, file, trusted, at start.</summary>
    public sealed class PluginRowViewModel : ObservableObject
    {
        internal PluginRowViewModel(CadLoadedPlugin plugin)
        {
            Plugin = plugin;
        }

        /// <summary>Gets the plugin.</summary>
        public CadLoadedPlugin Plugin { get; }

        /// <summary>Gets its name.</summary>
        public string Name => Plugin.Name;

        /// <summary>
        /// Gets its kind as the manifest says, "Command" or "Drawing"; for a .dll with no manifest, "Drawing" where it holds
        /// drawing plugins and no command, else "Command".
        /// </summary>
        public string Kind
        {
            get
            {
                bool drawingOnly = Plugin.DrawingPlugins.Count > 0 && Plugin.Plugin?.Commands.Count == 0;
                string kind = Plugin.Manifest?.Kind ?? (drawingOnly ? CadPluginManifest.DrawingKind : null);
                return string.IsNullOrEmpty(kind) ? "Command" : char.ToUpperInvariant(kind[0]) + kind.Substring(1).ToLowerInvariant();
            }
        }

        /// <summary>Gets whether it is a command plugin.</summary>
        public bool IsCommandKind => Kind == "Command";

        /// <summary>Gets its version: the loaded plugin's, else the manifest's.</summary>
        public string Version => Plugin.Plugin?.Version ?? Plugin.Manifest?.Version ?? string.Empty;

        /// <summary>Gets its commands' names then its drawing plugins' while it is loaded, or the manifest's commands while it is not.</summary>
        public string Commands => string.Join(", ", (IEnumerable<string>)Plugin.Plugin?.Commands.Select(c => c.Name).Concat(Plugin.DrawingPlugins) ?? Plugin.Manifest?.Commands ?? Array.Empty<string>());

        /// <summary>Gets the file it was loaded from.</summary>
        public string File => Plugin.Path;

        /// <summary>Gets "Yes", or "No ⚠" for a file outside the trusted folders.</summary>
        public string Trusted => Plugin.IsTrusted ? "Yes" : "No ⚠";

        /// <summary>Gets whether it is trusted.</summary>
        public bool IsTrusted => Plugin.IsTrusted;

        /// <summary>Gets "Yes" where it is loaded at every start: installed, or on the startup list.</summary>
        public string AtStart => Plugin.IsInstalled || Plugin.LoadAtStartup ? "Yes" : "No";

        /// <summary>Gets whether it is loaded.</summary>
        public bool IsLoaded => Plugin.IsLoaded;

        /// <inheritdoc/>
        public override string ToString() => Name;
    }

    /// <summary>A line of the load log: its time, its text, and whether it failed or came from an untrusted folder.</summary>
    public sealed class PluginLogLine
    {
        internal PluginLogLine(CadPluginLogEntry entry)
        {
            Time = entry.Time.ToString("HH:mm", CultureInfo.InvariantCulture);
            Text = entry.Action + " " + entry.Plugin + ": " + entry.Result;
            IsWarning = !entry.Succeeded || !entry.Trusted;
        }

        /// <summary>Gets the time, as 10:42.</summary>
        public string Time { get; }

        /// <summary>Gets what was done and what came of it.</summary>
        public string Text { get; }

        /// <summary>Gets whether the line is drawn amber: a refusal or a failure, or a file from an untrusted folder.</summary>
        public bool IsWarning { get; }

        /// <inheritdoc/>
        public override string ToString() => Time + " " + Text;
    }

    /// <summary>
    /// The Plugin manager, as the design draws it: the plugins known in this session with a filter by kind; Load .dll,
    /// Install .ghplugin, Reload, Unload and Remove; the one selected with its commands, author and API, its settings and
    /// whether it loads at start; the trusted folders; and the load log. It follows the manager while open and lets go of it
    /// when disposed. A drawing plugin is refused with the manager's reason, said in <see cref="Message"/>.
    /// </summary>
    public sealed class PluginManagerViewModel : ObservableObject, IDisposable
    {
        private readonly CadPluginManager _manager;
        private readonly Action<string> _say;
        private PluginRowViewModel _selected;
        private string _filter = "All";
        private string _message = string.Empty;
        private bool _messageFailed;
        private string _selectedFolder;

        /// <summary>Makes the view model over a manager.</summary>
        /// <param name="manager">The manager.</param>
        /// <param name="say">Writes a line on the command line; null for none.</param>
        /// <exception cref="ArgumentNullException">Thrown when the manager is null.</exception>
        public PluginManagerViewModel(CadPluginManager manager, Action<string> say = null)
        {
            _manager = manager ?? throw new ArgumentNullException(nameof(manager));
            _say = say;
            LoadCommand = new DelegateCommand(_ => LoadRequested?.Invoke(this, EventArgs.Empty));
            InstallCommand = new DelegateCommand(_ => InstallRequested?.Invoke(this, EventArgs.Empty));
            ReloadCommand = new DelegateCommand(_ => Show(_manager.Reload(_selected.Name)), _ => _selected != null);
            UnloadCommand = new DelegateCommand(_ => Show(_manager.Unload(_selected.Name)), _ => _selected != null && _selected.IsLoaded);
            RemoveCommand = new DelegateCommand(_ => Show(_manager.Uninstall(_selected.Name)), _ => _selected != null && _selected.Plugin.IsInstalled);
            ToggleLoadedCommand = new DelegateCommand(p => ToggleLoaded(p as PluginRowViewModel));
            FilterCommand = new DelegateCommand(p => Filter = p as string ?? "All");
            AddFolderCommand = new DelegateCommand(_ => AddFolderRequested?.Invoke(this, EventArgs.Empty));
            RemoveFolderCommand = new DelegateCommand(_ => RemoveFolder(_selectedFolder), _ => _selectedFolder != null && !string.Equals(_selectedFolder, _manager.PluginFolder, StringComparison.OrdinalIgnoreCase));
            SettingsCommand = new DelegateCommand(_ => SettingsRequested?.Invoke(this, EventArgs.Empty), _ => HasSettings);
            _manager.PluginsChanged += OnPluginsChanged;
            _manager.LogChanged += OnLogChanged;
            Refresh();
            ReadLog();
        }

        /// <summary>Raised by Load .dll: the window shows its Open dialog, then calls <see cref="Load"/>.</summary>
        public event EventHandler LoadRequested;

        /// <summary>Raised by Install .ghplugin: the window shows its Open dialog, then calls <see cref="Install"/>.</summary>
        public event EventHandler InstallRequested;

        /// <summary>Raised by Add folder: the window asks for a folder, then calls <see cref="AddFolder"/>.</summary>
        public event EventHandler AddFolderRequested;

        /// <summary>Raised by Settings: the window shows the settings of the plugin selected over <see cref="CreateSettings"/>.</summary>
        public event EventHandler SettingsRequested;

        /// <summary>Gets the plugins shown, as filtered.</summary>
        public ObservableCollection<PluginRowViewModel> Rows { get; } = new ObservableCollection<PluginRowViewModel>();

        /// <summary>Gets the load log, the oldest line first.</summary>
        public ObservableCollection<PluginLogLine> LogLines { get; } = new ObservableCollection<PluginLogLine>();

        /// <summary>Gets the trusted folders: the plugin folder first.</summary>
        public IReadOnlyList<string> TrustedFolders => _manager.TrustedFolders;

        /// <summary>Gets or sets the trusted folder selected, which Remove takes away; the plugin folder cannot be.</summary>
        public string SelectedFolder
        {
            get => _selectedFolder;
            set
            {
                if (Set(ref _selectedFolder, value))
                {
                    ((DelegateCommand)RemoveFolderCommand).RaiseCanExecuteChanged();
                }
            }
        }

        /// <summary>Gets or sets the filter: All, Command or Drawing.</summary>
        public string Filter
        {
            get => _filter;
            set
            {
                if (Set(ref _filter, value ?? "All"))
                {
                    Refresh();
                }
            }
        }

        /// <summary>Gets the chip All, with the number of plugins: "All 2".</summary>
        public string AllChipText => "All " + _manager.Plugins.Count.ToString(CultureInfo.InvariantCulture);

        /// <summary>Gets the chip of command plugins: "Command plugins 1".</summary>
        public string CommandChipText => "Command plugins " + _manager.Plugins.Select(p => new PluginRowViewModel(p)).Count(r => r.IsCommandKind).ToString(CultureInfo.InvariantCulture);

        /// <summary>Gets the chip of drawing plugins: "Drawing plugins 0".</summary>
        public string DrawingChipText => "Drawing plugins " + _manager.Plugins.Select(p => new PluginRowViewModel(p)).Count(r => !r.IsCommandKind).ToString(CultureInfo.InvariantCulture);

        /// <summary>Gets or sets the plugin selected.</summary>
        public PluginRowViewModel Selected
        {
            get => _selected;
            set
            {
                if (Set(ref _selected, value))
                {
                    RaiseSelected();
                }
            }
        }

        /// <summary>Gets the title of the selected plugin's card: "Number circles 1.0.0".</summary>
        public string SelectedTitle => _selected == null ? "No plugin selected" : (_selected.Plugin.Manifest?.Name ?? _selected.Name) + " " + _selected.Version;

        /// <summary>Gets the selected plugin's commands, one line each: "NUMCIRCLES (NC): Number the selected circles".</summary>
        public IReadOnlyList<string> SelectedCommands => _selected?.Plugin.Plugin?.Commands
            .Select(c => c.Name + (string.IsNullOrEmpty(c.Alias) ? string.Empty : " (" + c.Alias + ")") + (string.IsNullOrEmpty(c.Description) ? string.Empty : ": " + c.Description)).ToList()
            ?? (IReadOnlyList<string>)Array.Empty<string>();

        /// <summary>Gets the selected plugin's author and the least API version it needs.</summary>
        public string SelectedAuthor
        {
            get
            {
                if (_selected == null)
                {
                    return string.Empty;
                }

                string author = _selected.Plugin.Manifest?.Author;
                Version api = _selected.Plugin.Manifest?.MinimumApiVersion ?? _selected.Plugin.Plugin?.RequiredApiVersion;
                return "Author: " + (string.IsNullOrEmpty(author) ? "not given" : author) + " · Least API version " + (api?.ToString() ?? "1.0") + " · this presenter " + CadPlugin.ApiVersion;
            }
        }

        /// <summary>Gets what is wrong with the selected plugin, as its loading found it; empty for nothing.</summary>
        public string SelectedProblems => _selected?.Plugin.Plugin == null ? string.Empty : string.Join(" ", _selected.Plugin.Plugin.Problems);

        /// <summary>Gets or sets whether the selected plugin loads at every start (the startup list); installed plugins always do.</summary>
        public bool SelectedLoadAtStart
        {
            get => _selected != null && (_selected.Plugin.LoadAtStartup || _selected.Plugin.IsInstalled);
            set
            {
                if (_selected != null && !_selected.Plugin.IsInstalled)
                {
                    _manager.SetLoadAtStartup(_selected.Name, value);
                }

                Raise(nameof(SelectedLoadAtStart));
            }
        }

        /// <summary>Gets whether the selected plugin is loaded and declares settings.</summary>
        public bool HasSettings => _selected?.Plugin.Plugin != null && _selected.Plugin.Plugin.Settings.Count > 0;

        /// <summary>Gets what the last action came to: its message and warnings.</summary>
        public string Message
        {
            get => _message;
            private set => Set(ref _message, value ?? string.Empty);
        }

        /// <summary>Gets whether the last action failed, so its message is drawn amber.</summary>
        public bool MessageFailed
        {
            get => _messageFailed;
            private set => Set(ref _messageFailed, value);
        }

        /// <summary>Gets whether unloading frees a plugin's code: true on .NET 8, false on .NET Framework, which only switches it off.</summary>
        public bool CanUnload => _manager.CanUnload;

        /// <summary>Gets the command of Load .dll….</summary>
        public ICommand LoadCommand { get; }

        /// <summary>Gets the command of Install .ghplugin….</summary>
        public ICommand InstallCommand { get; }

        /// <summary>Gets the command of Reload.</summary>
        public ICommand ReloadCommand { get; }

        /// <summary>Gets the command of Unload.</summary>
        public ICommand UnloadCommand { get; }

        /// <summary>Gets the command of Remove: an installed plugin uninstalled.</summary>
        public ICommand RemoveCommand { get; }

        /// <summary>Gets the command of a row's On box, given the row: loads or unloads it.</summary>
        public ICommand ToggleLoadedCommand { get; }

        /// <summary>Gets the command of a filter chip, given All, Command or Drawing.</summary>
        public ICommand FilterCommand { get; }

        /// <summary>Gets the command of Add folder….</summary>
        public ICommand AddFolderCommand { get; }

        /// <summary>Gets the command that takes the trusted folder selected away.</summary>
        public ICommand RemoveFolderCommand { get; }

        /// <summary>Gets the command of Settings… of the plugin selected.</summary>
        public ICommand SettingsCommand { get; }

        /// <summary>Loads a plugin from a path, as Load .dll does after its dialog.</summary>
        /// <param name="path">The path.</param>
        /// <returns>What came of it.</returns>
        public CadPluginResult Load(string path) => Show(_manager.Load(path ?? throw new ArgumentNullException(nameof(path))));

        /// <summary>Installs a package, as Install .ghplugin does after its dialog.</summary>
        /// <param name="path">The path of the package.</param>
        /// <returns>What came of it.</returns>
        public CadPluginResult Install(string path) => Show(_manager.Install(path ?? throw new ArgumentNullException(nameof(path))));

        /// <summary>Trusts a folder.</summary>
        /// <param name="folder">The folder.</param>
        /// <returns>Whether it was added.</returns>
        public bool AddFolder(string folder)
        {
            bool added = !string.IsNullOrWhiteSpace(folder) && _manager.AddTrustedFolder(folder);
            Raise(nameof(TrustedFolders));
            return added;
        }

        /// <summary>Stops trusting a folder; the plugin folder stays.</summary>
        /// <param name="folder">The folder.</param>
        /// <returns>Whether it was taken away.</returns>
        public bool RemoveFolder(string folder)
        {
            bool removed = folder != null && _manager.RemoveTrustedFolder(folder);
            SelectedFolder = null;
            Raise(nameof(TrustedFolders));
            return removed;
        }

        /// <summary>Makes the view model of the selected plugin's settings; null where it is not loaded or declares none.</summary>
        /// <returns>The view model.</returns>
        public PluginSettingsViewModel CreateSettings() => HasSettings ? new PluginSettingsViewModel(_selected.Plugin.Plugin) : null;

        /// <summary>Lets go of the manager.</summary>
        public void Dispose()
        {
            _manager.PluginsChanged -= OnPluginsChanged;
            _manager.LogChanged -= OnLogChanged;
        }

        private void ToggleLoaded(PluginRowViewModel row)
        {
            if (row == null)
            {
                return;
            }

            Show(row.IsLoaded ? _manager.Unload(row.Name) : _manager.Load(row.File));
        }

        private CadPluginResult Show(CadPluginResult result)
        {
            Message = result.ToString();
            MessageFailed = !result.Succeeded || result.Warnings.Count > 0;
            _say?.Invoke(result.ToString());
            Refresh();
            return result;
        }

        private void Refresh()
        {
            string selected = _selected?.Name;
            Rows.Clear();

            foreach (CadLoadedPlugin plugin in _manager.Plugins)
            {
                var row = new PluginRowViewModel(plugin);

                if (_filter == "All" || (_filter == "Command") == row.IsCommandKind)
                {
                    Rows.Add(row);
                }
            }

            _selected = Rows.FirstOrDefault(r => string.Equals(r.Name, selected, StringComparison.OrdinalIgnoreCase)) ?? Rows.FirstOrDefault();
            Raise(nameof(Selected));
            Raise(nameof(AllChipText));
            Raise(nameof(CommandChipText));
            Raise(nameof(DrawingChipText));
            Raise(nameof(TrustedFolders));
            RaiseSelected();
        }

        private void RaiseSelected()
        {
            Raise(nameof(SelectedTitle));
            Raise(nameof(SelectedCommands));
            Raise(nameof(SelectedAuthor));
            Raise(nameof(SelectedProblems));
            Raise(nameof(SelectedLoadAtStart));
            Raise(nameof(HasSettings));

            foreach (ICommand command in new[] { ReloadCommand, UnloadCommand, RemoveCommand, SettingsCommand })
            {
                ((DelegateCommand)command).RaiseCanExecuteChanged();
            }
        }

        private void ReadLog()
        {
            LogLines.Clear();

            foreach (CadPluginLogEntry entry in _manager.Log)
            {
                LogLines.Add(new PluginLogLine(entry));
            }
        }

        private void OnPluginsChanged(object sender, EventArgs e) => Refresh();

        private void OnLogChanged(object sender, EventArgs e) => ReadLog();
    }

    /// <summary>A setting of a plugin as its dialog draws it: a number box with its range, a check, a text box or a drop-down.</summary>
    public sealed class PluginSettingRow : ObservableObject
    {
        private readonly CadPluginSetting _setting;
        private string _text;
        private string _error = string.Empty;

        internal PluginSettingRow(CadPluginSetting setting)
        {
            _setting = setting;
            _text = Write(setting.Value);
        }

        /// <summary>Gets the label.</summary>
        public string Label => _setting.Label;

        /// <summary>Gets what the setting is for, and its range for a number: "How high the numbers are written. From 0.1 to 1000."</summary>
        public string Description
        {
            get
            {
                string range = _setting.Kind == CadSettingKind.Number || _setting.Kind == CadSettingKind.Integer
                    ? (double.IsInfinity(_setting.Minimum) && double.IsInfinity(_setting.Maximum) ? string.Empty
                        : " From " + Write(_setting.Minimum) + " to " + Write(_setting.Maximum) + ".")
                    : string.Empty;
                return (_setting.Description ?? string.Empty) + range;
            }
        }

        /// <summary>Gets whether it is a number or a whole number.</summary>
        public bool IsNumber => _setting.Kind == CadSettingKind.Number || _setting.Kind == CadSettingKind.Integer;

        /// <summary>Gets whether it is yes or no.</summary>
        public bool IsBoolean => _setting.Kind == CadSettingKind.Boolean;

        /// <summary>Gets whether it is a text.</summary>
        public bool IsText => _setting.Kind == CadSettingKind.Text;

        /// <summary>Gets whether it is one of a list.</summary>
        public bool IsChoice => _setting.Kind == CadSettingKind.Choice;

        /// <summary>Gets the names a choice offers.</summary>
        public IReadOnlyList<string> Choices => _setting.Choices ?? Array.Empty<string>();

        /// <summary>Gets or sets the value as text; one the plugin refuses shows why and the plugin keeps the value it had.</summary>
        public string Value
        {
            get => _text;
            set
            {
                _text = value ?? string.Empty;

                try
                {
                    _setting.Value = _text;
                    Error = string.Empty;
                }
                catch (ArgumentException ex)
                {
                    Error = FirstSentence(ex.Message);
                }

                Raise(nameof(Value));
                Raise(nameof(IsOn));
            }
        }

        /// <summary>Gets or sets a yes-or-no setting.</summary>
        public bool IsOn
        {
            get => string.Equals(_text, "True", StringComparison.OrdinalIgnoreCase);
            set => Value = value ? "True" : "False";
        }

        /// <summary>Gets why the value typed was refused; empty where it was taken.</summary>
        public string Error
        {
            get => _error;
            private set
            {
                if (Set(ref _error, value))
                {
                    Raise(nameof(HasError));
                }
            }
        }

        /// <summary>Gets whether the value typed was refused.</summary>
        public bool HasError => _error.Length > 0;

        private static string Write(object value) => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;

        private static string FirstSentence(string message)
        {
            int end = message.IndexOfAny(new[] { '\r', '\n' });
            return end < 0 ? message : message.Substring(0, end);
        }

        /// <inheritdoc/>
        public override string ToString() => Label + " = " + _text;
    }

    /// <summary>The settings a plugin declares, as its dialog draws them; each value set at once, a refusal shown by its row.</summary>
    public sealed class PluginSettingsViewModel : ObservableObject
    {
        internal PluginSettingsViewModel(CadPlugin plugin)
        {
            Title = plugin.Name + " " + plugin.Version;
            Rows = plugin.Settings.Select(s => new PluginSettingRow(s)).ToList();
        }

        /// <summary>Gets the dialog's title: the plugin's name and version.</summary>
        public string Title { get; }

        /// <summary>Gets the settings, in the order the plugin declares them.</summary>
        public IReadOnlyList<PluginSettingRow> Rows { get; }
    }
}
