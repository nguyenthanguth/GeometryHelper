using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Cad.Plugins;

namespace GeometryHelper.Cad.Wpf
{
    /// <summary>
    /// The Manage ribbon tab, as AutoCAD 2021's for what the presenter has: Customization (User Interface and Edit Aliases open Global settings on Shortcuts, Tool Palettes shows the tool palettes), Applications
    /// (Load Application, which loads an AutoLISP file or a plugin, greyed where the window can neither show Load/Unload Applications nor has a plugin manager; Run Script), CAD Standards
    /// (parked, greyed) and Cleanup (Purge, Overkill, Audit, Recover). A button starts its command through
    /// <see cref="MainViewModel.StartCommandCommand"/>, as the buttons of Home do.
    /// </summary>
    public sealed class ManageRibbonViewModel : ObservableObject
    {
        internal ManageRibbonViewModel(MainViewModel model)
        {
            Model = model;
            model.PropertyChanged += (s, e) =>
            {
                if (
                    e.PropertyName == nameof(MainViewModel.HasDocument)
                    || e.PropertyName == nameof(MainViewModel.Plugins)
                    || e.PropertyName == nameof(MainViewModel.ShowAppLoad)
                )
                {
                    Raise(nameof(CanLoadApplication));
                }
            };
        }

        /// <summary>Gets the window's view model, whose commands the buttons run.</summary>
        public MainViewModel Model { get; }

        /// <summary>Gets whether Load Application can be pressed: a drawing is open and the window can show Load/Unload Applications or has a plugin manager.</summary>
        public bool CanLoadApplication => Model.HasDocument && (Model.ShowAppLoad != null || Model.Plugins != null);
    }

    /// <summary>
    /// The Manage ribbon tab and the commands CUI, ALIASEDIT and APPLOAD, which open the window's own pages where AutoCAD opens
    /// its dialogs: Global settings on Shortcuts for the first two, Load/Unload Applications for the third.
    /// </summary>
    public sealed partial class MainViewModel
    {
        private CadBuiltInTab _manageTab;
        private Func<AppLoadViewModel, bool> _showAppLoad;

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
        /// a drafter change), and APPLOAD, which loads an AutoLISP file or a plugin and says so where the window can do neither.
        /// </summary>
        private void AddManageWindowCommands(CadCommandRegistry registry)
        {
            registry.Add(new CadCommand("CUI", c => RunWindow(() => RequestSettings("Shortcuts"))));
            registry.Add(new CadCommand("ALIASEDIT", c => RunWindow(() => RequestSettings("Shortcuts"))));
            registry.Add(new CadCommand("APPLOAD", c => RunWindow(LoadApplications)));
        }

        /// <summary>
        /// Gets or sets how the window shows Load/Unload Applications, over the view model it is given, until the dialog is
        /// closed; the window is modal. Null where no window does, when APPLOAD shows the Plugin manager instead. The Plugin
        /// manager stays on the Plugins tab.
        /// </summary>
        public Func<AppLoadViewModel, bool> ShowAppLoad
        {
            get => _showAppLoad;
            set => Set(ref _showAppLoad, value);
        }

        /// <summary>
        /// APPLOAD: shows Load/Unload Applications with <see cref="ShowAppLoad"/>, which loads an AutoLISP file into the
        /// document's interpreter and a plugin into the plugin manager as its Load button is pressed. A window with no such
        /// dialog shows its Plugin manager, and with neither says that applications cannot be loaded.
        /// </summary>
        private void LoadApplications()
        {
            // A window with no dialog shows its Plugin manager, as APPLOAD did before it loaded AutoLISP.
            if (ShowAppLoad == null)
            {
                if (_plugins == null)
                {
                    Say("No plugin manager is in use: applications cannot be loaded.");
                }
                else
                {
                    RequestPluginManager();
                }

                return;
            }

            ShowAppLoad(new AppLoadViewModel(this));
        }

        /// <summary>
        /// Loads one application as the Load button of Load/Unload Applications does, and says what came of it on the command
        /// line: an AutoLISP file into the document's interpreter (what it wrote, then <c>name.lsp successfully loaded.</c>, or
        /// the <c>; error:</c> line), a .dll into the plugin manager; <c>.arx</c>, <c>.dbx</c>, <c>.dvb</c>, <c>.fas</c> and
        /// <c>.vlx</c> files are refused, and any other type is said to be neither an AutoLISP file nor a plugin.
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
                    .LastOrDefault(l => l.StartsWith("; error:", StringComparison.Ordinal))
                    ?? string.Empty;
                return new AppLoadResult(AppLoadStatusKind.Failed, (name + " was not loaded: " + error).TrimEnd());
            }

            if (extension == ".dll")
            {
                if (_plugins == null)
                {
                    const string none = "No plugin manager is in use: applications cannot be loaded.";
                    Say(none);
                    return new AppLoadResult(AppLoadStatusKind.Failed, none);
                }

                CadPluginResult result = LoadPlugin(path);
                return new AppLoadResult(
                    result.Succeeded ? AppLoadStatusKind.Loaded : AppLoadStatusKind.Failed,
                    result.ToString()
                );
            }

            string refused =
                Array.IndexOf(RefusedApplicationTypes, extension) >= 0
                    ? name + " cannot be loaded: .arx, .dbx, .dvb, .fas and .vlx files are not supported."
                    : name + " is not an AutoLISP file or a plugin.";
            Say(refused);
            return new AppLoadResult(AppLoadStatusKind.Refused, refused);
        }

        /// <summary>Gets the AutoLISP files loaded into the document shown, as full paths in the order they loaded; empty with no document.</summary>
        internal IReadOnlyList<string> LoadedLispFiles =>
            _current?.Session.LoadedLispFiles ?? (IReadOnlyList<string>)Array.Empty<string>();

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
