using System;
using System.Collections.Generic;
using GeometryHelper.Cad.Plugins;

namespace GeometryHelper.Cad.Wpf
{
    /// <summary>
    /// The content of the Plugins ribbon tab, which shows only while a plugin has added panels with
    /// <see cref="CadPluginHost.AddRibbonPanel"/>: those panels, plugin by plugin in the order the plugins were loaded. The
    /// presenter adds no panel of its own: a plugin's commands are run from the command line, and plugins are loaded from the
    /// Applications panel of the Manage tab.
    /// </summary>
    public sealed class PluginRibbonViewModel : ObservableObject
    {
        private IReadOnlyList<PluginPanelViewModel> _panels = Array.Empty<PluginPanelViewModel>();

        internal PluginRibbonViewModel() { }

        /// <summary>Gets the panels the plugins loaded add to this tab with <see cref="CadPluginHost.AddRibbonPanel"/>, plugin by plugin in the order they were loaded.</summary>
        public IReadOnlyList<PluginPanelViewModel> Panels
        {
            get => _panels;
            private set => Set(ref _panels, value);
        }

        /// <summary>Takes the panels the plugins add to this tab, made by the window when it reads what the plugins add.</summary>
        internal void SetPanels(IReadOnlyList<PluginPanelViewModel> panels) => Panels = panels;

        /// <summary>Asks the buttons of the panels again whether they are on.</summary>
        internal void RefreshPanels(CadPluginContext context)
        {
            foreach (PluginPanelViewModel panel in _panels)
            {
                panel.Refresh(context);
            }
        }
    }
}
