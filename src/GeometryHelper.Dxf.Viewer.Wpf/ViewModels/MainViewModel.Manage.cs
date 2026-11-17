using System;
using System.Collections.Generic;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// The Manage ribbon tab, as AutoCAD 2021's for what the presenter has: Action Recorder (parked, its buttons greyed),
    /// Customization (User Interface and Edit Aliases open Global settings on Shortcuts, Tool Palettes greyed), Applications
    /// (Load Application, which opens the Plugin manager, greyed where the window has none; Run Script), CAD Standards
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
                )
                {
                    Raise(nameof(CanLoadApplication));
                }
            };
        }

        /// <summary>Gets the window's view model, whose commands the buttons run.</summary>
        public MainViewModel Model { get; }

        /// <summary>Gets whether Load Application can be pressed: a drawing is open and the window has a plugin manager to show.</summary>
        public bool CanLoadApplication => Model.HasDocument && Model.Plugins != null;
    }

    /// <summary>
    /// The Manage ribbon tab and the commands CUI, ALIASEDIT and APPLOAD, which open the window's own pages where AutoCAD opens
    /// its dialogs: Global settings on Shortcuts for the first two, the Plugin manager for the third.
    /// </summary>
    public sealed partial class MainViewModel
    {
        private CadBuiltInTab _manageTab;

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
        /// a drafter change), and APPLOAD, which shows the Plugin manager where the window has one and says so where it has none.
        /// </summary>
        private void AddManageWindowCommands(CadCommandRegistry registry)
        {
            registry.Add(new CadCommand("CUI", c => RunWindow(() => RequestSettings("Shortcuts"))));
            registry.Add(new CadCommand("ALIASEDIT", c => RunWindow(() => RequestSettings("Shortcuts"))));
            registry.Add(new CadCommand("APPLOAD", c => RunWindow(LoadApplications)));
        }

        /// <summary>APPLOAD: the Plugin manager, where plugins are loaded and unloaded; a line saying there is none where the window has no manager.</summary>
        private void LoadApplications()
        {
            if (_plugins == null)
            {
                Say("No plugin manager is in use: applications cannot be loaded.");
            }
            else
            {
                RequestPluginManager();
            }
        }
    }
}
