using System;
using System.Collections.Generic;

namespace GeometryHelper.Cad.Wpf
{
    /// <summary>
    /// The Manage ribbon tab, as AutoCAD 2021's for what the presenter has: Customization (User Interface and Edit Aliases open Global settings on Shortcuts, Tool Palettes shows the tool palettes), Applications
    /// (Load Application, which loads an AutoLISP file or a plugin, greyed where the window can neither ask for a file nor has a plugin manager; Run Script), CAD Standards
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
                    || e.PropertyName == nameof(MainViewModel.AskApplicationFile)
                )
                {
                    Raise(nameof(CanLoadApplication));
                }
            };
        }

        /// <summary>Gets the window's view model, whose commands the buttons run.</summary>
        public MainViewModel Model { get; }

        /// <summary>Gets whether Load Application can be pressed: a drawing is open and the window can ask for a file or has a plugin manager.</summary>
        public bool CanLoadApplication =>
            Model.HasDocument && (Model.AskApplicationFile != null || Model.Plugins != null);
    }

    /// <summary>
    /// The Manage ribbon tab and the commands CUI, ALIASEDIT and APPLOAD, which open the window's own pages where AutoCAD opens
    /// its dialogs: Global settings on Shortcuts for the first two, a file to load for the third.
    /// </summary>
    public sealed partial class MainViewModel
    {
        private CadBuiltInTab _manageTab;
        private Func<string> _askApplicationFile;

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
        /// Gets or sets what asks for the file APPLOAD loads, an AutoLISP file (.lsp) or a plugin (.dll), and gives its path; null
        /// for none picked, or where no window asks. The Plugin manager stays on the Plugins tab.
        /// </summary>
        public Func<string> AskApplicationFile
        {
            get => _askApplicationFile;
            set => Set(ref _askApplicationFile, value);
        }

        /// <summary>
        /// APPLOAD: asks for a file with <see cref="AskApplicationFile"/> and loads it, an AutoLISP file into the document's
        /// interpreter and a plugin into the plugin manager; any other file gets a line saying it is neither. Nothing happens
        /// where none is picked. A window with no picker shows its Plugin manager, and with neither says that applications
        /// cannot be loaded.
        /// </summary>
        private void LoadApplications()
        {
            // A window that cannot ask for a file shows its Plugin manager, as APPLOAD did before it loaded AutoLISP.
            if (AskApplicationFile == null)
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

            string path = AskApplicationFile();

            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            string extension = System.IO.Path.GetExtension(path);

            if (string.Equals(extension, ".lsp", StringComparison.OrdinalIgnoreCase))
            {
                _current.Session.LoadLisp(path);
                SyncHistory();
            }
            else if (string.Equals(extension, ".dll", StringComparison.OrdinalIgnoreCase))
            {
                if (_plugins == null)
                {
                    Say("No plugin manager is in use: applications cannot be loaded.");
                }
                else
                {
                    LoadPlugin(path);
                }
            }
            else
            {
                Say(System.IO.Path.GetFileName(path) + " is not an AutoLISP file or a plugin.");
            }
        }
    }
}
