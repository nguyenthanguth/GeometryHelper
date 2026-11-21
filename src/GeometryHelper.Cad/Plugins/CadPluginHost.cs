using System;
using System.Collections.Generic;
using GeometryHelper.Dxf;

namespace GeometryHelper.Cad.Plugins
{
    /// <summary>
    /// What a plugin adds to the window in <see cref="ICadPluginApplication.Initialize"/>: ribbon tabs and panels, Properties
    /// rows, DRAWINGS buttons and menu items, canvas menu items and object snaps. It is a registrar and not a window: it keeps
    /// the descriptions, and the window reads them from <see cref="CadPlugin.Host"/> when a plugin is loaded or unloaded and
    /// draws them, so a test reads the same lists headless. The <c>Add</c> methods take additions only while
    /// <c>Initialize</c> runs.
    /// </summary>
    public sealed class CadPluginHost
    {
        private readonly List<CadRibbonTab> _tabs = new List<CadRibbonTab>();
        private readonly List<CadRibbonPanel> _panels = new List<CadRibbonPanel>();
        private readonly List<CadPropertyProvider> _properties = new List<CadPropertyProvider>();
        private readonly List<CadPluginButton> _drawingsButtons = new List<CadPluginButton>();
        private readonly List<CadDrawingsMenuProvider> _drawingsMenus = new List<CadDrawingsMenuProvider>();
        private readonly List<CadCanvasMenuProvider> _canvasMenus = new List<CadCanvasMenuProvider>();
        private readonly List<CadCustomSnap> _snaps = new List<CadCustomSnap>();
        private bool _closed;

        // The keywords a custom snap may not be or start with, AutoCAD's snap keywords; and the point modifiers it may not be.
        private static readonly string[] SnapKeywords =
        {
            "END",
            "MID",
            "CEN",
            "GCE",
            "NOD",
            "QUA",
            "INT",
            "EXT",
            "INS",
            "PER",
            "TAN",
            "NEA",
            "APP",
            "PAR",
            "NON",
        };

        private static readonly string[] PointModifiers = { "FROM", "TT", "M2P", "MTP" };

        internal CadPluginHost(string pluginName, string folder)
        {
            PluginName = pluginName;
            Folder = folder;
        }

        /// <summary>Gets the plugin's name.</summary>
        public string PluginName { get; }

        /// <summary>
        /// Gets the folder the plugin was loaded from, as given and not its shadow copy, where files beside it can be read
        /// (<c>Path.Combine(host.Folder, "circle16.png")</c>); the folder of a package the plugin's files were unpacked into;
        /// null for a plugin made with <see cref="CadPlugin.FromAssemblies"/> from assemblies that have no location.
        /// </summary>
        public string Folder { get; }

        /// <summary>
        /// Gets or sets whether the plugin's commands get a button each in the "Command plugins" panel of the Plugins tab:
        /// true until set; false for a plugin that puts its commands on its own tab.
        /// </summary>
        public bool ShowsCommandButtons { get; set; } = true;

        /// <summary>Gets the tabs added, in the order added.</summary>
        public IReadOnlyList<CadRibbonTab> RibbonTabs => _tabs;

        /// <summary>Gets the panels added to the Plugins tab, in the order added.</summary>
        public IReadOnlyList<CadRibbonPanel> RibbonPanels => _panels;

        /// <summary>Gets the Properties providers added, in the order added.</summary>
        public IReadOnlyList<CadPropertyProvider> PropertyProviders => _properties;

        /// <summary>Gets the DRAWINGS buttons added, in the order added.</summary>
        public IReadOnlyList<CadPluginButton> DrawingsButtons => _drawingsButtons;

        /// <summary>Gets the DRAWINGS menu providers added, in the order added.</summary>
        public IReadOnlyList<CadDrawingsMenuProvider> DrawingsMenus => _drawingsMenus;

        /// <summary>Gets the canvas menu providers added, in the order added.</summary>
        public IReadOnlyList<CadCanvasMenuProvider> CanvasMenus => _canvasMenus;

        /// <summary>Gets the object snaps added, in the order added.</summary>
        public IReadOnlyList<CadCustomSnap> ObjectSnaps => _snaps;

        /// <summary>Gets whether nothing was added.</summary>
        public bool IsEmpty =>
            _tabs.Count == 0
            && _panels.Count == 0
            && _properties.Count == 0
            && _drawingsButtons.Count == 0
            && _drawingsMenus.Count == 0
            && _canvasMenus.Count == 0
            && _snaps.Count == 0;

        /// <summary>
        /// Adds a ribbon tab: after the Plugins tab and before Settings, in the order the plugins were loaded. Its panels are
        /// read when <c>Initialize</c> returns, so they may be added to it afterwards here. A tab whose id another loaded
        /// plugin, or an earlier tab of this one, uses is left out, with a line in <see cref="CadPlugin.Problems"/>.
        /// </summary>
        /// <param name="tab">The tab.</param>
        /// <exception cref="ArgumentNullException">Thrown when the tab is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when <c>Initialize</c> has returned.</exception>
        public void AddRibbonTab(CadRibbonTab tab)
        {
            Open();
            _tabs.Add(tab ?? throw new ArgumentNullException(nameof(tab)));
        }

        /// <summary>Adds a panel to the Plugins tab, after its "Command plugins" panel and before "Manage".</summary>
        /// <param name="panel">The panel.</param>
        /// <exception cref="ArgumentNullException">Thrown when the panel is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when <c>Initialize</c> has returned.</exception>
        public void AddRibbonPanel(CadRibbonPanel panel)
        {
            Open();
            _panels.Add(panel ?? throw new ArgumentNullException(nameof(panel)));
        }

        /// <summary>Adds a provider of Properties rows, run after the extensions' for every selection, as AutoCAD's dynamic properties are.</summary>
        /// <param name="provider">The provider.</param>
        /// <exception cref="ArgumentNullException">Thrown when the provider is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when <c>Initialize</c> has returned.</exception>
        public void AddProperties(CadPropertyProvider provider)
        {
            Open();
            _properties.Add(provider ?? throw new ArgumentNullException(nameof(provider)));
        }

        /// <summary>Adds a button to the DRAWINGS header, left of the extensions' buttons and the presenter's Refresh and Open.</summary>
        /// <param name="button">The button.</param>
        /// <exception cref="ArgumentNullException">Thrown when the button is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when <c>Initialize</c> has returned.</exception>
        public void AddDrawingsButton(CadPluginButton button)
        {
            Open();
            _drawingsButtons.Add(button ?? throw new ArgumentNullException(nameof(button)));
        }

        /// <summary>Adds items to the right-click menu of a DRAWINGS row, after a separator, asked each time the menu opens.</summary>
        /// <param name="provider">The provider.</param>
        /// <exception cref="ArgumentNullException">Thrown when the provider is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when <c>Initialize</c> has returned.</exception>
        public void AddDrawingsMenu(CadDrawingsMenuProvider provider)
        {
            Open();
            _drawingsMenus.Add(provider ?? throw new ArgumentNullException(nameof(provider)));
        }

        /// <summary>Adds items to the right-click menu of the canvas for the objects selected, after the extensions' items and before the presenter's own.</summary>
        /// <param name="provider">The provider.</param>
        /// <exception cref="ArgumentNullException">Thrown when the provider is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when <c>Initialize</c> has returned.</exception>
        public void AddCanvasMenu(CadCanvasMenuProvider provider)
        {
            Open();
            _canvasMenus.Add(provider ?? throw new ArgumentNullException(nameof(provider)));
        }

        /// <summary>
        /// Adds an object snap. A keyword another loaded plugin, or an earlier snap of this one, uses leaves the snap out, with
        /// a line in <see cref="CadPlugin.Problems"/>.
        /// </summary>
        /// <param name="snap">The snap.</param>
        /// <exception cref="ArgumentNullException">Thrown when the snap is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the keyword is not letters and digits only, 2 to 16 long, is or starts with one of AutoCAD's snap keywords (END MID CEN GCE NOD QUA INT EXT INS PER TAN NEA APP PAR NON), or is FROM, TT, M2P or MTP; or an entity type is null or not a <see cref="DxfEntity"/>.</exception>
        /// <exception cref="InvalidOperationException">Thrown when <c>Initialize</c> has returned.</exception>
        public void AddObjectSnap(CadCustomSnap snap)
        {
            Open();

            if (snap == null)
            {
                throw new ArgumentNullException(nameof(snap));
            }

            string refusal = KeywordRefusal(snap.Keyword);

            if (refusal != null)
            {
                throw new ArgumentException("The snap keyword " + snap.Keyword + " " + refusal, nameof(snap));
            }

            foreach (Type type in snap.EntityTypes)
            {
                if (type == null || !typeof(DxfEntity).IsAssignableFrom(type))
                {
                    throw new ArgumentException(
                        "The snap "
                            + snap.Name
                            + " applies to "
                            + (type?.Name ?? "null")
                            + ", which is not a DxfEntity.",
                        nameof(snap)
                    );
                }
            }

            _snaps.Add(snap);
        }

        /// <summary>Why a keyword cannot be a custom snap's, or null where it can.</summary>
        private static string KeywordRefusal(string keyword)
        {
            if (keyword.Length < 2 || keyword.Length > 16)
            {
                return "is 2 to 16 letters and digits long.";
            }

            foreach (char c in keyword)
            {
                if (!char.IsLetterOrDigit(c))
                {
                    return "is letters and digits only.";
                }
            }

            foreach (string word in SnapKeywords)
            {
                if (keyword.StartsWith(word, StringComparison.Ordinal))
                {
                    return "is, or starts with, " + word + ", an AutoCAD object snap.";
                }
            }

            foreach (string word in PointModifiers)
            {
                if (string.Equals(keyword, word, StringComparison.Ordinal))
                {
                    return "is " + word + ", a point modifier.";
                }
            }

            return null;
        }

        /// <summary>Takes no more additions: called when <c>Initialize</c> has returned.</summary>
        internal void Close() => _closed = true;

        /// <summary>Drops every addition, as for a plugin whose <c>Initialize</c> threw.</summary>
        internal void Clear()
        {
            _tabs.Clear();
            _panels.Clear();
            _properties.Clear();
            _drawingsButtons.Clear();
            _drawingsMenus.Clear();
            _canvasMenus.Clear();
            _snaps.Clear();
        }

        internal bool RemoveTab(CadRibbonTab tab) => _tabs.Remove(tab);

        internal bool RemoveSnap(CadCustomSnap snap) => _snaps.Remove(snap);

        private void Open()
        {
            if (_closed)
            {
                throw new InvalidOperationException(
                    "The plugin " + PluginName + " adds to the window only in Initialize."
                );
            }
        }
    }
}
