using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;
using System.Windows.Media;
using GeometryHelper.Cad.Plugins;
using GeometryHelper.Dxf;
using WpfGeometry = System.Windows.Media.Geometry;

namespace GeometryHelper.Cad.Wpf
{
    /// <summary>
    /// What the command plugins loaded, and the Python files loaded, add to the window: the ribbon tabs and the panels of the Plugins tab (shown while a plugin has panels), rows of Properties,
    /// buttons and row items of the DRAWINGS list and items of the canvas's right-click menu. All of it is read from the
    /// <see cref="CadPluginHost"/> of each plugin loaded when the manager says the plugins changed (and of each Python file, when the host says its files changed), plugin by plugin in the order
    /// they were loaded, and none of it is kept once a plugin is unloaded.
    /// </summary>
    public sealed partial class MainViewModel
    {
        private readonly List<CadPluginTab> _pluginTabs = new List<CadPluginTab>();
        private IReadOnlyList<CadWorkspaceExtension> _plainPluginTabs = Array.Empty<CadWorkspaceExtension>();
        private IReadOnlyList<CadWorkspaceExtension> _contextPluginTabs = Array.Empty<CadWorkspaceExtension>();
        private IReadOnlyList<PluginPanelViewModel> _pluginPanels = Array.Empty<PluginPanelViewModel>();
        private List<PluginDrawingsButton> _pluginDrawingsButtons = new List<PluginDrawingsButton>();
        private bool _pluginUiHooked;
        private bool _pluginUiBusy;

        /// <summary>Called after every rebuild of what the plugins add: the custom object snaps of the plugins loaded are read again by the snap part of the window.</summary>
        partial void SyncPluginSnaps();

        /// <summary>
        /// Gets the ribbon tabs the plugins loaded add, in the order they were loaded and each plugin's in the order it added
        /// them: after Plugins and before Settings, in the colours of the other tabs. Their <see cref="CadWorkspaceExtension.TabContent"/>
        /// is a <see cref="PluginTabViewModel"/>.
        /// </summary>
        public IReadOnlyList<CadWorkspaceExtension> PluginTabs => _plainPluginTabs;

        /// <summary>
        /// Gets the contextual ribbon tabs the plugins loaded add (those with a <see cref="CadRibbonTab.ShowWhen"/>): after Block
        /// Editor, in the green of the built-in contextual tabs, shown while their predicate is true.
        /// </summary>
        public IReadOnlyList<CadWorkspaceExtension> PluginContextTabs => _contextPluginTabs;

        /// <summary>Gets whether the ribbon shows a contextual tab of a plugin, whose panels get the green line over them.</summary>
        public bool IsPluginContextTabSelected => _selectedTab is CadPluginTab tab && tab.View.IsContextual;

        /// <summary>
        /// Reads what the plugins loaded add to the window again: their tabs and panels, the buttons of the DRAWINGS list, then
        /// the custom object snaps and Properties. Tabs and panels still described by the same objects are kept as they are, with
        /// the button a split button shows; what an unloaded plugin added is dropped, and Home is shown where one of its tabs was.
        /// </summary>
        private void RebuildPluginUi()
        {
            List<PluginSource> loaded = PluginSources();
            var kept = new Dictionary<CadRibbonTab, CadPluginTab>();

            foreach (CadPluginTab tab in _pluginTabs)
            {
                kept[tab.Source] = tab;
            }

            var panels = new Dictionary<CadRibbonPanel, PluginPanelViewModel>();

            foreach (PluginPanelViewModel panel in _pluginPanels.Concat(_pluginTabs.SelectMany(t => t.View.Panels)))
            {
                panels[panel.Source] = panel;
            }

            PluginPanelViewModel PanelOf(string plugin, CadRibbonPanel source) =>
                panels.TryGetValue(source, out PluginPanelViewModel panel)
                    ? panel
                    : new PluginPanelViewModel(this, plugin, source);

            var tabs = new List<CadPluginTab>();
            var plugins = new List<PluginPanelViewModel>();
            var drawings = new List<PluginDrawingsButton>();

            foreach (PluginSource entry in loaded)
            {
                CadPluginHost host = entry.Host;

                foreach (CadRibbonTab source in host.RibbonTabs)
                {
                    if (!kept.TryGetValue(source, out CadPluginTab tab))
                    {
                        tab = new CadPluginTab(
                            source,
                            entry.Name,
                            new PluginTabViewModel(
                                source,
                                entry.Name,
                                source.Panels.Where(p => p != null).Select(p => PanelOf(entry.Name, p)).ToList()
                            )
                        );
                    }

                    tabs.Add(tab);
                }

                plugins.AddRange(host.RibbonPanels.Where(p => p != null).Select(p => PanelOf(entry.Name, p)));
                drawings.AddRange(host.DrawingsButtons.Select(b => new PluginDrawingsButton(this, entry.Name, b)));
            }

            // A tab whose plugin is gone is taken out; Home is shown where it was the tab shown.
            foreach (CadPluginTab gone in _pluginTabs.Where(t => !tabs.Contains(t)))
            {
                if (_selectedTab == gone)
                {
                    SelectedTab = null;
                }

                _builtInTabs.Remove(gone);
            }

            foreach (CadPluginTab added in tabs.Where(t => !_builtInTabs.Contains(t)))
            {
                _builtInTabs.Add(added);
            }

            _pluginTabs.Clear();
            _pluginTabs.AddRange(tabs);
            _plainPluginTabs = tabs.Where(t => !t.View.IsContextual).ToList();
            _contextPluginTabs = tabs.Where(t => t.View.IsContextual).ToList();
            _pluginPanels = plugins;
            _pluginDrawingsButtons = drawings;
            PluginRibbon.SetPanels(plugins);
            ShowPluginsTab(plugins.Count > 0);
            Raise(nameof(PluginTabs));
            Raise(nameof(PluginContextTabs));
            Raise(nameof(FileListButtons));
            SyncPluginSnaps();

            if (!_pluginUiHooked)
            {
                _pluginUiHooked = true;
                PropertyChanged += OnPluginUiSourceChanged;
            }

            RefreshPluginUi();
            UpdateProperties();
        }

        /// <summary>Shows the Plugins tab while a plugin has added a panel to it and hides it otherwise; Home is shown where it was the tab shown.</summary>
        private void ShowPluginsTab(bool shown)
        {
            var tab = (CadBuiltInTab)PluginsTab;

            if (tab.IsTabVisible == shown)
            {
                return;
            }

            if (!shown && _selectedTab == tab)
            {
                SelectedTab = null;
            }

            tab.SetVisible(shown);
        }

        /// <summary>The plugins loaded and on, in the order they were loaded; none without a manager.</summary>
        private List<CadLoadedPlugin> LoadedPlugins() =>
            _plugins == null
                ? new List<CadLoadedPlugin>()
                : _plugins.LoadedInOrder().Where(p => p.Plugin != null && p.Plugin.IsEnabled).ToList();

        /// <summary>A host the window draws and the name its additions are shown and logged under: a plugin's, or a Python file's.</summary>
        private sealed class PluginSource
        {
            internal PluginSource(string name, CadPluginHost host)
            {
                Name = name;
                Host = host;
            }

            internal string Name { get; }

            internal CadPluginHost Host { get; }
        }

        /// <summary>The hosts the window draws: those of the plugins loaded and on, in the order they were loaded, then those of the Python files loaded, in the order they were loaded.</summary>
        private List<PluginSource> PluginSources()
        {
            List<PluginSource> sources = LoadedPlugins().Select(p => new PluginSource(p.Name, p.Plugin.Host)).ToList();

            if (PythonFiles != null)
            {
                sources.AddRange(PythonFiles.PluginHosts.Select(h => new PluginSource(h.PluginName, h)));
            }

            return sources;
        }

        private void OnPluginUiSourceChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(IsHomeTabSelected):
                    Raise(nameof(IsPluginContextTabSelected));
                    break;
                case nameof(HasDocument):
                case nameof(Document):
                case nameof(IsCommandRunning):
                    RefreshPluginUi();
                    break;
            }
        }

        /// <summary>
        /// Asks the plugins again what depends on the window's state, where the window asks its own ribbon commands again: the
        /// selection changed, a document shown or closed, a command started or ended. Each button is asked its
        /// <see cref="CadRibbonButton.CanRun"/>; a contextual tab is shown while its <see cref="CadRibbonTab.ShowWhen"/> is true,
        /// selected as it appears, and Home returns as it goes.
        /// </summary>
        private void RefreshPluginUi()
        {
            if (
                _pluginUiBusy
                || (_pluginTabs.Count == 0 && _pluginPanels.Count == 0 && _pluginDrawingsButtons.Count == 0)
            )
            {
                return;
            }

            _pluginUiBusy = true;

            try
            {
                CadPluginContext context = PluginContext();

                foreach (CadPluginTab tab in _pluginTabs)
                {
                    if (tab.View.IsContextual)
                    {
                        bool shown = tab.AskShowWhen(context);

                        if (shown && !tab.IsTabVisible)
                        {
                            tab.SetVisible(true);
                            SelectedTab = tab;
                        }
                        else if (!shown && tab.IsTabVisible)
                        {
                            tab.SetVisible(false);

                            if (_selectedTab == tab)
                            {
                                SelectedTab = null;
                            }
                        }

                        if (!tab.IsTabVisible)
                        {
                            continue;
                        }
                    }

                    tab.View.Refresh(context);
                }

                PluginRibbon.RefreshPanels(context);

                foreach (PluginDrawingsButton button in _pluginDrawingsButtons)
                {
                    button.Refresh(context);
                }
            }
            finally
            {
                _pluginUiBusy = false;
            }
        }

        /// <summary>Makes the context a plugin's callback, predicate or provider gets: the drawing shown, the objects selected, whether a command runs.</summary>
        internal CadPluginContext PluginContext()
        {
            CadDocument document = _current?.Document;
            return PluginContext(document, document == null ? new List<string>() : SelectedHandles(document));
        }

        private CadPluginContext PluginContext(CadDocument document, IReadOnlyList<string> selected) =>
            new CadPluginContext(document, selected, IsCommandRunning, Say, StartPluginCommand);

        /// <summary>The handles of the objects selected that the drawing has, in ordinal order.</summary>
        private static List<string> SelectedHandles(CadDocument document)
        {
            List<string> handles = document.Selection.Handles.Where(h => document.FindEntity(h) != null).ToList();
            handles.Sort(StringComparer.OrdinalIgnoreCase);
            return handles;
        }

        /// <summary>Starts a plugin's command as the presenter's ribbon buttons start theirs, ending a command running first; "_NUMCIRCLES2" and ".NUMCIRCLES2" are NUMCIRCLES2.</summary>
        internal void StartPluginCommand(string command) =>
            StartCommand((command ?? string.Empty).Trim().TrimStart('.', '_'));

        /// <summary>Runs a plugin's callback at once with the context of the window now; one that throws is logged and its message written on the command line after the plugin's name.</summary>
        internal void RunPluginCallback(string plugin, Action<CadPluginContext> run)
        {
            try
            {
                run(PluginContext());
            }
            catch (Exception exception) when (!(exception is OutOfMemoryException))
            {
                GeometryHelperLog.Warn("PluginUi: a callback of the plugin " + plugin + " threw.", exception);
                Say(plugin + ": " + exception.Message.Split('\n')[0].Trim());
            }
        }

        /// <summary>
        /// Runs what the plugins loaded add to Properties, after the extensions: each provider is given what is shown so far and
        /// the banner, and what it returns is shown; null leaves the rows as they were. One that throws is passed over for this
        /// reading, with a Warn.
        /// </summary>
        private CadSelectionInfo DescribeByPlugins(
            CadSelectionInfo info,
            IReadOnlyList<string> shown,
            ref string banner
        )
        {
            foreach (PluginSource entry in PluginSources())
            {
                foreach (CadPropertyProvider provider in entry.Host.PropertyProviders)
                {
                    var request = new CadPropertyRequest(_current.Document, shown, info, banner);

                    try
                    {
                        info = provider(request) ?? info;
                        banner = request.Banner;
                    }
                    catch (Exception exception) when (!(exception is OutOfMemoryException))
                    {
                        GeometryHelperLog.Warn(
                            "MainViewModel: the rows of the plugin " + entry.Name + " were not read.",
                            exception
                        );
                    }
                }
            }

            return info;
        }

        /// <summary>
        /// Makes the row of a property an extension or a plugin gave a setter: the setter's own list where it gave one, else for
        /// a colour, layer, linetype or lineweight the window's list; "Select Color..." on a colour opens the Select Color dialog.
        /// </summary>
        private PropertyRowViewModel SetterRow(CadPropertyRow row) =>
            new PropertyRowViewModel(row, row.Choices ?? OptionsOfKind(row.Kind), (r, text) =>
                row.Kind == CadPropertyKind.Color && text == SelectColorOption
                    ? PickRowColor(row)
                    : CommitExtensionProperty(row, text)
            );

        private IReadOnlyList<string> OptionsOfKind(CadPropertyKind kind)
        {
            switch (kind)
            {
                case CadPropertyKind.Color:
                    return OptionsOf("Color");
                case CadPropertyKind.Layer:
                    return OptionsOf("Layer");
                case CadPropertyKind.Linetype:
                    return OptionsOf("Linetype");
                case CadPropertyKind.Lineweight:
                    return OptionsOf("Lineweight");
                default:
                    return null;
            }
        }

        /// <summary>
        /// "Select Color..." of a colour row a plugin gave: AutoCAD's Select Color dialog on the colour of the row, ByLayer and
        /// ByBlock offered, the colour chosen given to the row's setter as the palette writes it. The rows are read again, so the
        /// row shown is the one the setter left; false, so that this row keeps the text it had.
        /// </summary>
        private bool PickRowColor(CadPropertyRow row)
        {
            CadPropertyEditor.ParseColor(row.Value, out DxfColor old);
            var dialog = new SelectColorViewModel(old ?? DxfColor.ByLayer, true);

            if (PickColor == null)
            {
                Say("The Select Color dialog is shown by the window.");
            }
            else if (PickColor(dialog) && (old == null || !dialog.Color.Equals(old)))
            {
                CommitExtensionProperty(row, CadPropertyEditor.ColorName(dialog.Color));
            }

            return false;
        }

        /// <summary>The items the plugins add to the right-click menu of a file row of the DRAWINGS list, behind a separator; none where no provider gives one.</summary>
        private List<CadMenuItemViewModel> PluginRowItems(FileRowViewModel row)
        {
            var made = new List<CadMenuItemViewModel>();
            IReadOnlyList<string> paths = SelectedFileRows.Select(r => r.Path).ToList();
            CadDocument document = Known(row.Path)?.Document;

            foreach (PluginSource entry in PluginSources())
            {
                foreach (CadDrawingsMenuProvider provider in entry.Host.DrawingsMenus)
                {
                    try
                    {
                        AddPluginItems(made, entry.Name, provider(new CadDrawingsRequest(row.Path, document, paths)));
                    }
                    catch (Exception exception) when (!(exception is OutOfMemoryException))
                    {
                        GeometryHelperLog.Warn(
                            "MainViewModel: the DRAWINGS menu items of the plugin " + entry.Name + " were not read.",
                            exception
                        );
                    }
                }
            }

            if (made.Count > 0)
            {
                made.Insert(0, CadMenuItemViewModel.Separator());
            }

            return made;
        }

        /// <summary>The items the plugins add to the canvas's right-click menu for the objects selected; none where nothing is selected.</summary>
        private List<CadMenuItemViewModel> PluginCanvasMenu()
        {
            var made = new List<CadMenuItemViewModel>();
            CadDocument document = _current.Document;
            List<string> handles = SelectedHandles(document);

            if (handles.Count == 0)
            {
                return made;
            }

            foreach (PluginSource entry in PluginSources())
            {
                foreach (CadCanvasMenuProvider provider in entry.Host.CanvasMenus)
                {
                    try
                    {
                        AddPluginItems(made, entry.Name, provider(PluginContext(document, handles)));
                    }
                    catch (Exception exception) when (!(exception is OutOfMemoryException))
                    {
                        GeometryHelperLog.Warn(
                            "MainViewModel: the canvas menu items of the plugin " + entry.Name + " were not read.",
                            exception
                        );
                    }
                }
            }

            return made;
        }

        private void AddPluginItems(
            List<CadMenuItemViewModel> made,
            string plugin,
            IReadOnlyList<CadPluginMenuItem> items
        )
        {
            if (items == null)
            {
                return;
            }

            foreach (CadPluginMenuItem item in items)
            {
                CadMenuItemViewModel menuItem = MadeByPlugin(plugin, item);

                if (menuItem != null)
                {
                    made.Add(menuItem);
                }
            }
        }

        /// <summary>A plugin's menu item as the window's own: a submenu, a tick, grey, a separator; null for none and for a submenu of no items.</summary>
        private CadMenuItemViewModel MadeByPlugin(string plugin, CadPluginMenuItem item)
        {
            if (item == null)
            {
                return null;
            }

            if (item.IsSeparator)
            {
                return CadMenuItemViewModel.Separator();
            }

            if (item.Run != null)
            {
                return new CadMenuItemViewModel(
                    item.Text,
                    () => RunPluginCallback(plugin, item.Run),
                    item.IsEnabled,
                    isChecked: item.IsChecked
                );
            }

            List<CadMenuItemViewModel> items = item
                .Items.Select(i => MadeByPlugin(plugin, i))
                .Where(i => i != null)
                .ToList();
            return items.Count == 0
                ? null
                : new CadMenuItemViewModel(item.Text, item.IsEnabled, items, isChecked: item.IsChecked);
        }

        /// <summary>The buttons the plugins loaded add to the header of the DRAWINGS list, in the order the plugins were loaded.</summary>
        private IEnumerable<CadExtensionButton> PluginFileListButtons() => _pluginDrawingsButtons.Select(b => b.Button);

        /// <summary>A button a plugin added to the header of the DRAWINGS list: it runs the plugin's callback, and is on while its <see cref="CadPluginButton.CanRun"/> says so.</summary>
        private sealed class PluginDrawingsButton
        {
            private readonly MainViewModel _model;
            private readonly string _plugin;
            private readonly CadPluginButton _source;
            private readonly DelegateCommand _command;
            private bool _enabled = true;
            private bool _warned;

            public PluginDrawingsButton(MainViewModel model, string plugin, CadPluginButton source)
            {
                _model = model;
                _plugin = plugin;
                _source = source;
                _command = new DelegateCommand(_ => _model.RunPluginCallback(_plugin, _source.Run), _ => _enabled);
                PluginUi.Make(
                    source.Icon,
                    plugin,
                    "the DRAWINGS button " + source.ToolTip,
                    out WpfGeometry geometry,
                    out System.Windows.Media.Imaging.BitmapSource image
                );
                Button = new CadExtensionButton(geometry, image, source.ToolTip, plugin, _command);
            }

            public CadExtensionButton Button { get; }

            public void Refresh(CadPluginContext context)
            {
                bool enabled = PluginUi.CanRun(_source.CanRun, context, false, _plugin, _source.ToolTip, ref _warned);

                if (enabled != _enabled)
                {
                    _enabled = enabled;
                    _command.RaiseCanExecuteChanged();
                }
            }
        }
    }

    /// <summary>A ribbon tab a plugin added: its content is a <see cref="PluginTabViewModel"/>; a contextual one is hidden until its predicate is true.</summary>
    internal sealed class CadPluginTab : CadWorkspaceExtension
    {
        private bool _warned;

        public CadPluginTab(CadRibbonTab source, string plugin, PluginTabViewModel view)
        {
            Source = source;
            PluginName = plugin;
            View = view;
            TabContent = view;
            IsTabVisible = !view.IsContextual;
        }

        public CadRibbonTab Source { get; }

        public string PluginName { get; }

        public PluginTabViewModel View { get; }

        public override string TabHeader => Source.Title;

        public void SetVisible(bool visible) => IsTabVisible = visible;

        /// <summary>Asks whether a contextual tab shows now; a predicate that throws says no, with one Warn for the tab.</summary>
        public bool AskShowWhen(CadPluginContext context)
        {
            try
            {
                return Source.ShowWhen(context);
            }
            catch (Exception exception) when (!(exception is OutOfMemoryException))
            {
                if (!_warned)
                {
                    _warned = true;
                    GeometryHelperLog.Warn(
                        "PluginUi: ShowWhen of the tab " + Source.Id + " of the plugin " + PluginName + " threw.",
                        exception
                    );
                }

                return false;
            }
        }
    }
}
