using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Input;
using GeometryHelper.Dxf.Viewer.Plugins;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>A source of model objects as Global settings › Data sources lists it: its name, its state and whether it can give objects.</summary>
    public sealed class CadSourceInfo
    {
        /// <summary>Makes a source as the list shows it.</summary>
        /// <param name="name">The name, as "Tekla Structures".</param>
        /// <param name="detail">Its state in words, as "Not connected · ...".</param>
        /// <param name="isAvailable">Whether it can give objects now.</param>
        public CadSourceInfo(string name, string detail, bool isAvailable)
        {
            Name = name ?? string.Empty;
            Detail = detail ?? string.Empty;
            IsAvailable = isAvailable;
        }

        /// <summary>Gets the name.</summary>
        public string Name { get; }

        /// <summary>Gets the state in words.</summary>
        public string Detail { get; }

        /// <summary>Gets whether it can give objects now.</summary>
        public bool IsAvailable { get; }
    }

    /// <summary>The Settings ribbon tab: a button per category of Global settings, each opening the dialog on it.</summary>
    public sealed class SettingsRibbonViewModel
    {
        internal SettingsRibbonViewModel(MainViewModel model)
        {
            OpenCommand = new DelegateCommand(p => model.RequestSettings(p as string));
        }

        /// <summary>Gets the command that opens Global settings, given the category to show; null for Appearance.</summary>
        public ICommand OpenCommand { get; }
    }

    /// <summary>The Global settings of the presenter's window: applied to the canvas, the snaps, the status bar and the command lines.</summary>
    public sealed partial class MainViewModel
    {
        private GlobalSettings _settings = new GlobalSettings();
        private CadBuiltInTab _settingsTab;
        private EventHandler<string> _settingsRequested;
        private IReadOnlyList<CadSourceInfo> _sources = Array.Empty<CadSourceInfo>();
        private bool _settingsApplied;

        /// <summary>Raised when settings are applied: the window gives the canvas their colours, font, zoom factor, pickbox and aperture.</summary>
        public event EventHandler SettingsApplied;

        /// <summary>Raised by OPTIONS and the Settings tab: the window shows Global settings over <see cref="CreateGlobalSettings"/>, on the category given (null for the first).</summary>
        public event EventHandler<string> SettingsRequested
        {
            add => _settingsRequested += value;
            remove => _settingsRequested -= value;
        }

        /// <summary>Gets the settings in use; change them through <see cref="ApplySettings"/>, which takes a copy.</summary>
        public GlobalSettings Settings => _settings;

        /// <summary>
        /// Gets or sets the file a new view model keeps its settings in until its <see cref="SettingsPath"/> is set: the user's
        /// own. The tests set a file of their own before they make a view model, so that none of them writes the user's.
        /// </summary>
        internal static string DefaultSettingsPath { get; set; } = GlobalSettings.DefaultPath;

        /// <summary>Gets or sets the file the settings are saved to by the dialog's OK: <see cref="GlobalSettings.DefaultPath"/> until set.</summary>
        public string SettingsPath { get; set; } = DefaultSettingsPath;

        /// <summary>Gets or sets the model sources Global settings › Data sources lists, as a host that makes drawings from a model gives them.</summary>
        public IReadOnlyList<CadSourceInfo> SettingsSources
        {
            get => _sources;
            set => _sources = value ?? Array.Empty<CadSourceInfo>();
        }

        /// <summary>Gets the Settings ribbon tab, always shown, last.</summary>
        public CadWorkspaceExtension SettingsTab
        {
            get
            {
                if (_settingsTab == null)
                {
                    _settingsTab = new CadBuiltInTab("Settings", new SettingsRibbonViewModel(this));
                    _builtInTabs.Add(_settingsTab);
                }

                return _settingsTab;
            }
        }

        /// <summary>
        /// Applies settings at once: a copy is kept; the snaps, the grid, the coordinates of the status bar and the aliases and
        /// keys of every command line take them now; the window gives the canvas the rest (<see cref="SettingsApplied"/>).
        /// </summary>
        /// <param name="settings">The settings.</param>
        /// <exception cref="ArgumentNullException">Thrown when the settings are null.</exception>
        public void ApplySettings(GlobalSettings settings)
        {
            _settings = (settings ?? throw new ArgumentNullException(nameof(settings))).Clone();
            Snap.Modes = _settings.Snaps;
            Snap.PolarAngle = _settings.PolarAngle * Math.PI / 180.0;
            ApplyPolarMode(_settings.PolarMode);
            Snap.PolarSnap = _settings.PolarSnap;
            Snap.PolarDistance = _settings.PolarDistance;
            // GRID and the lineweights are a drawing's own where it has them; the settings' only where it has none.
            foreach (KnownFile file in _files.Values.Where(f => f.Document != null))
            {
                FollowSettings(file);
            }

            if (_current == null)
            {
                Toggles.First(t => t.Name == "GRID").IsOn = _settings.Grid;
            }
            else
            {
                LoadDrafting();
            }

            DynamicInput.IsOn = _settings.DynamicInput;

            foreach (KnownFile file in _files.Values.Where(f => f.Session != null))
            {
                ApplyShortcuts(file.Session.Registry);
            }

            _settingsApplied = true;

            foreach (KnownFile file in _files.Values.Where(f => f.Session != null))
            {
                ApplyTextDefaults(file.Session);
                file.Document.Fonts = FontsOf(file.Document);
                file.Document.SupportFolders = _settings.SupportSearchFolders;
            }

            SetCursor(0.0, 0.0);
            RebuildRecent();
            Raise(nameof(Settings));
            SettingsApplied?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Saves a copy of each open drawing that has a file and changes not saved, as AutoCAD's autosave does: beside the file,
        /// named plan.autosave1.dxf for plan.dxf and plan.dwg.autosave1.dxf for plan.dwg, so that the two in one folder keep
        /// copies of their own, the newest the first and <see cref="GlobalSettings.BackupsKept"/> kept; the user's file itself
        /// is never written. A drawing with a command running, or with no file yet, is left for the next time.
        /// </summary>
        /// <returns>The copies written.</returns>
        public IReadOnlyList<string> Autosave()
        {
            var written = new List<string>();
            int keep = _settings.BackupsKept;

            if (keep <= 0 || _settings.AutosaveMinutes <= 0)
            {
                return written;
            }

            foreach (
                KnownFile file in _files
                    .Values.Where(f => f.Document != null && f.Document.HasFile && f.Document.IsModified)
                    .OrderBy(f => f.Path, StringComparer.OrdinalIgnoreCase)
            )
            {
                if (file.Session.IsRunning || file.Document.IsInGroup)
                {
                    continue;
                }

                string path = file.Document.FileName;
                string folder = System.IO.Path.GetDirectoryName(path);
                // A DWG's copy keeps .dwg in its name, as plan.dxf's would be the same copy otherwise.
                string name = path.EndsWith(".dwg", StringComparison.OrdinalIgnoreCase)
                    ? System.IO.Path.GetFileName(path)
                    : System.IO.Path.GetFileNameWithoutExtension(path);
                string Copy(int k) =>
                    System.IO.Path.Combine(
                        folder,
                        name + ".autosave" + k.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".dxf"
                    );

                try
                {
                    if (File.Exists(Copy(keep)))
                    {
                        File.Delete(Copy(keep));
                    }

                    for (int k = keep - 1; k >= 1; k--)
                    {
                        if (File.Exists(Copy(k)))
                        {
                            File.Move(Copy(k), Copy(k + 1));
                        }
                    }

                    file.Document.Database.Save(Copy(1));
                    written.Add(Copy(1));
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    GeometryHelperLog.Warn(
                        "MainViewModel: the autosave copy of " + path + " could not be written.",
                        ex
                    );
                }
            }

            if (written.Count > 0)
            {
                GeometryHelperLog.Info("MainViewModel: autosaved " + written.Count + " drawing(s).");
            }

            return written;
        }

        /// <summary>Whether a file is a copy autosave keeps: plan.autosave2.dxf, plan.dwg.autosave2.dxf.</summary>
        private static bool IsAutosaveCopy(string path)
        {
            string name = System.IO.Path.GetFileNameWithoutExtension(path);
            int at = name.LastIndexOf(".autosave", StringComparison.OrdinalIgnoreCase);
            return at >= 0 && name.Length > at + 9 && name.Substring(at + 9).All(char.IsDigit);
        }

        /// <summary>
        /// Gives a command line, once settings are applied, the text height of Global settings as the default of TEXT and MTEXT
        /// and the height of the text of the dimensions DIM* makes, and their width factor as that of TEXT and MTEXT.
        /// </summary>
        private void ApplyTextDefaults(CadCommandSession session)
        {
            if (_settingsApplied)
            {
                session.Remembered["text.height"] = _settings.TextHeight;
                session.Remembered["text.widthFactor"] = _settings.WidthFactor;
                session.Remembered["dim.text.height"] = _settings.TextHeight;
            }
        }

        /// <summary>Makes the view model of Global settings over a copy of the settings in use, on a category; null for the first.</summary>
        /// <param name="category">The category shown first, as Shortcuts; null for Appearance.</param>
        /// <returns>The view model.</returns>
        public GlobalSettingsViewModel CreateGlobalSettings(string category = null) =>
            new GlobalSettingsViewModel(this, category);

        /// <summary>
        /// Runs what a key pressed is given in the shortcuts, as "Ctrl+1": a toggle of the status bar, or a command started on the
        /// command line; nothing for a key given nothing, but Ctrl+9, which shows or hides the command line as in AutoCAD,
        /// Ctrl+R, which works through the next viewport of the layout shown while one is worked through, and F5 and Ctrl+E,
        /// which turn to the next isometric plane (SNAPISOPAIR), a command running going on.
        /// </summary>
        /// <param name="gesture">The key, as the shortcuts write it: Ctrl+1, F3, Ctrl+Shift+P.</param>
        /// <returns>Whether the key was given something, and it ran.</returns>
        public bool RunKey(string gesture)
        {
            IReadOnlyDictionary<string, string> keys = Session?.Registry.Shortcuts.Keys ?? _settings.Keys;

            if (string.IsNullOrEmpty(gesture) || !keys.TryGetValue(gesture, out string target))
            {
                if (string.Equals(gesture, "Ctrl+9", StringComparison.OrdinalIgnoreCase))
                {
                    IsCommandLineShown = !IsCommandLineShown;
                    return true;
                }

                if (string.Equals(gesture, "Ctrl+R", StringComparison.OrdinalIgnoreCase))
                {
                    return CycleViewport();
                }

                if (
                    string.Equals(gesture, "F5", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(gesture, "Ctrl+E", StringComparison.OrdinalIgnoreCase)
                )
                {
                    return CycleIsoplane();
                }

                return false;
            }

            // A key given PICKSTYLE (Ctrl+Shift+A) or SELECTIONCYCLING switches it, as AutoCAD's, a command running going on.
            if (Session != null && (target == "PICKSTYLE" || target == "SELECTIONCYCLING"))
            {
                if (target == "PICKSTYLE")
                {
                    Session.TogglePickStyle();
                }
                else
                {
                    Session.ToggleSelectionCycling();
                }

                return true;
            }

            StatusToggleViewModel toggle = FindToggle(target);

            if (toggle != null)
            {
                if (
                    toggle.Name == "OSNAP"
                    && string.Equals(gesture, "F3", StringComparison.OrdinalIgnoreCase)
                    && !_settings.F3TogglesSnaps
                )
                {
                    return false;
                }

                toggle.IsOn = !toggle.IsOn;
                return true;
            }

            if (_current == null)
            {
                return _extensions.Any(e => e.TryRunWithoutDocument(target));
            }

            StartCommand(target);
            return true;
        }

        /// <summary>The names of the commands of the command line shown, or of a new one where none is, in ordinal order.</summary>
        internal IReadOnlyList<string> CommandNames()
        {
            var names = new SortedSet<string>(Preview().Commands.Select(c => c.Name), StringComparer.OrdinalIgnoreCase);

            if (Session == null && _plugins != null)
            {
                names.UnionWith(
                    new[]
                    {
                        CadPluginManager.LoadCommand,
                        CadPluginManager.UnloadCommand,
                        CadPluginManager.ReloadCommand,
                    }
                );
                names.UnionWith(
                    _plugins.Plugins.Where(p => p.IsLoaded).SelectMany(p => p.Plugin.Commands).Select(c => c.Name)
                );
            }

            return names.ToList();
        }

        /// <summary>The aliases and keys of the command line shown, or of a new one where none is.</summary>
        internal CadShortcuts CurrentShortcuts() => Preview().Shortcuts;

        /// <summary>The command line shown's registry, or one made as a new document's would be, with no plugin attached.</summary>
        private CadCommandRegistry Preview()
        {
            if (Session != null)
            {
                return Session.Registry;
            }

            CadCommandRegistry registry = CadCommandRegistry.CreateDefault();
            registry.Add(new CadCommand("LAYMCUR", MakeCurrent, true));
            registry.Add(new CadCommand("LAYMCH", MatchLayer, true));
            AddWindowCommands(registry);

            foreach (CadWorkspaceExtension extension in _extensions)
            {
                extension.AddCommands(registry);
            }

            ApplyShortcuts(registry);
            return registry;
        }

        /// <summary>Asks the window for Global settings on a category.</summary>
        internal void RequestSettings(string category) => _settingsRequested?.Invoke(this, category);

        /// <summary>
        /// Gives a command line the settings' aliases and keys: those the settings hold win, and those a plugin or an extension
        /// added that AutoCAD's table has not stay.
        /// </summary>
        private void ApplyShortcuts(CadCommandRegistry registry)
        {
            var aliases = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var keys = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (
                KeyValuePair<string, string> alias in registry.Shortcuts.Aliases.Where(a =>
                    !CadShortcuts.Default.Aliases.ContainsKey(a.Key)
                )
            )
            {
                aliases[alias.Key] = alias.Value;
            }

            foreach (
                KeyValuePair<string, string> key in registry.Shortcuts.Keys.Where(k =>
                    !CadShortcuts.Default.Keys.ContainsKey(k.Key)
                )
            )
            {
                keys[key.Key] = key.Value;
            }

            foreach (KeyValuePair<string, string> alias in _settings.Aliases)
            {
                aliases[alias.Key] = alias.Value;
            }

            foreach (KeyValuePair<string, string> key in _settings.Keys)
            {
                keys[key.Key] = key.Value;
            }

            registry.Shortcuts = new CadShortcuts(aliases, keys);
        }
    }
}
