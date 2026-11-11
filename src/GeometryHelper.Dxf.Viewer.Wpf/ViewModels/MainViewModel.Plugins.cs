using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf.Viewer.Plugins;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>The command plugins of the presenter's window: the manager, the Plugins tab and the commands that reach them.</summary>
    public sealed partial class MainViewModel
    {
        private readonly List<CadWorkspaceExtension> _builtInTabs = new List<CadWorkspaceExtension>();
        private CadPluginManager _plugins;
        private CadBuiltInTab _pluginsTab;
        private PluginRibbonViewModel _pluginRibbon;

        /// <summary>Raised by PLUGINLOAD (NETLOAD) and the Load .dll buttons: the window shows its Open dialog for a .dll, then calls <see cref="LoadPlugin"/>.</summary>
        public event EventHandler PluginLoadRequested;

        /// <summary>Raised by the Install buttons: the window shows its Open dialog for a .ghplugin, then calls <see cref="InstallPlugin"/>.</summary>
        public event EventHandler PluginInstallRequested;

        /// <summary>Raised by the Plugin manager button: the window shows the Plugin manager over <see cref="CreatePluginManager"/>.</summary>
        public event EventHandler PluginManagerRequested;

        /// <summary>Gets the manager of the command plugins; null until <see cref="UsePlugins"/>.</summary>
        public CadPluginManager Plugins => _plugins;

        /// <summary>Gets the view model of the Plugins ribbon tab: a button per command of each plugin loaded, and Manage.</summary>
        public PluginRibbonViewModel PluginRibbon => _pluginRibbon ?? (_pluginRibbon = new PluginRibbonViewModel(this));

        /// <summary>Gets the Plugins ribbon tab, always shown, after the extensions' tabs and Annotate and View.</summary>
        public CadWorkspaceExtension PluginsTab
        {
            get
            {
                if (_pluginsTab == null)
                {
                    _pluginsTab = new CadBuiltInTab("Plugins", PluginRibbon);
                    _builtInTabs.Add(_pluginsTab);
                }

                return _pluginsTab;
            }
        }

        /// <summary>
        /// Puts the command plugins into the window: their commands on the command line of every document, open or opened later
        /// (after LAYMCUR and LAYMCH, and after the window's own PLUGINLOAD where the window shows a file dialog), and on the
        /// Plugins tab; with <paramref name="loadAtStart"/> the plugins of the plugin folder and the startup list are loaded,
        /// each result a line of the command line.
        /// </summary>
        /// <param name="plugins">The manager.</param>
        /// <param name="loadAtStart">Whether to load the plugins of the plugin folder and the startup list now.</param>
        /// <returns>What came of each plugin loaded at start; empty without <paramref name="loadAtStart"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the manager is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when a manager is in use already.</exception>
        public IReadOnlyList<CadPluginResult> UsePlugins(CadPluginManager plugins, bool loadAtStart)
        {
            if (plugins == null)
            {
                throw new ArgumentNullException(nameof(plugins));
            }

            if (_plugins != null)
            {
                throw new InvalidOperationException("The window has a plugin manager already.");
            }

            _plugins = plugins;
            plugins.PluginsChanged += (s, e) => PluginRibbon.Refresh();

            foreach (KnownFile file in _files.Values.Where(f => f.Session != null))
            {
                AttachPlugins(file.Session.Registry);
            }

            IReadOnlyList<CadPluginResult> results = loadAtStart
                ? plugins.LoadAtStart()
                : Array.Empty<CadPluginResult>();

            foreach (CadPluginResult result in results)
            {
                Say(result.ToString());
            }

            PluginRibbon.Refresh();
            Raise(nameof(Plugins));
            return results;
        }

        /// <summary>Loads a command plugin from a .dll, a .ghplugin or a plugin's folder, as PLUGINLOAD does, and says what came of it.</summary>
        /// <param name="path">The path.</param>
        /// <returns>What came of it; null without a manager.</returns>
        public CadPluginResult LoadPlugin(string path) =>
            Report(_plugins == null || string.IsNullOrWhiteSpace(path) ? null : _plugins.Load(path));

        /// <summary>Installs a .ghplugin package into the plugin folder and loads it, and says what came of it.</summary>
        /// <param name="path">The path of the package.</param>
        /// <returns>What came of it; null without a manager.</returns>
        public CadPluginResult InstallPlugin(string path) =>
            Report(_plugins == null || string.IsNullOrWhiteSpace(path) ? null : _plugins.Install(path));

        /// <summary>Loads a plugin again from its file, as PLUGINRELOAD does, and says what came of it.</summary>
        /// <param name="name">The plugin's name.</param>
        /// <returns>What came of it; null without a manager.</returns>
        public CadPluginResult ReloadPlugin(string name) => Report(_plugins?.Reload(name));

        /// <summary>Unloads a plugin, as PLUGINUNLOAD does, and says what came of it.</summary>
        /// <param name="name">The plugin's name.</param>
        /// <returns>What came of it; null without a manager.</returns>
        public CadPluginResult UnloadPlugin(string name) => Report(_plugins?.Unload(name));

        /// <summary>Makes the view model of the Plugin manager; null without a manager. Dispose it when the dialog closes.</summary>
        /// <returns>The view model.</returns>
        public PluginManagerViewModel CreatePluginManager() =>
            _plugins == null ? null : new PluginManagerViewModel(_plugins, Say);

        /// <summary>Asks the window for the file dialog of PLUGINLOAD.</summary>
        internal void RequestPluginLoad() => PluginLoadRequested?.Invoke(this, EventArgs.Empty);

        /// <summary>Asks the window for the file dialog of a .ghplugin to install.</summary>
        internal void RequestPluginInstall() => PluginInstallRequested?.Invoke(this, EventArgs.Empty);

        /// <summary>Asks the window for the Plugin manager.</summary>
        internal void RequestPluginManager() => PluginManagerRequested?.Invoke(this, EventArgs.Empty);

        /// <summary>
        /// Adds the plugins' commands to a document's command line: first the window's own PLUGINLOAD, which shows a file dialog,
        /// where the window listens for it, then the manager's, which leaves a command already there.
        /// </summary>
        private void AttachPlugins(CadCommandRegistry registry)
        {
            if (_plugins == null)
            {
                return;
            }

            if (PluginLoadRequested != null && registry.Find(CadPluginManager.LoadCommand) == null)
            {
                registry.Add(new CadCommand(CadPluginManager.LoadCommand, PluginLoadByDialog));
            }

            foreach (string refused in _plugins.Attach(registry))
            {
                Say(refused);
            }
        }

        private void DetachPlugins(CadCommandRegistry registry) => _plugins?.Detach(registry);

        /// <summary>PLUGINLOAD with a window: its Open dialog for a .dll, the load done when a file is chosen.</summary>
        private IEnumerable<CadPrompt> PluginLoadByDialog(CadCommandContext c)
        {
            RequestPluginLoad();
            return Array.Empty<CadPrompt>();
        }

        private CadPluginResult Report(CadPluginResult result)
        {
            if (result == null)
            {
                Say("No plugin manager is in use.");
                return null;
            }

            Say(result.ToString());
            PluginRibbon.Refresh();
            return result;
        }
    }

    /// <summary>A ribbon tab of the presenter's own, as Plugins and Settings: always shown, its content given.</summary>
    internal sealed class CadBuiltInTab : CadWorkspaceExtension
    {
        private readonly string _header;

        public CadBuiltInTab(string header, object content)
        {
            _header = header;
            TabContent = content;
            IsTabVisible = true;
        }

        public override string TabHeader => _header;
    }
}
