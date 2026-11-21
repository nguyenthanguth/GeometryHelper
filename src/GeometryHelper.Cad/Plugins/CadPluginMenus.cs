using System;
using System.Collections.Generic;

namespace GeometryHelper.Cad.Plugins
{
    /// <summary>
    /// A button a plugin puts in the header of the DRAWINGS list, left of the presenter's Refresh and Open: an icon with a
    /// tooltip that runs a callback.
    /// </summary>
    public sealed class CadPluginButton
    {
        /// <summary>Makes a button.</summary>
        /// <param name="icon">The icon, drawn 16 pixels.</param>
        /// <param name="toolTip">The tooltip; empty where null.</param>
        /// <param name="run">The callback, run at once on the window's thread; one that throws is logged and its message written on the command line with the plugin's name.</param>
        /// <exception cref="ArgumentNullException">Thrown when the icon or the callback is null.</exception>
        public CadPluginButton(CadPluginIcon icon, string toolTip, Action<CadPluginContext> run)
        {
            Icon = icon ?? throw new ArgumentNullException(nameof(icon));
            ToolTip = toolTip ?? string.Empty;
            Run = run ?? throw new ArgumentNullException(nameof(run));
        }

        /// <summary>Gets the icon.</summary>
        public CadPluginIcon Icon { get; }

        /// <summary>Gets the tooltip.</summary>
        public string ToolTip { get; }

        /// <summary>Gets the callback.</summary>
        public Action<CadPluginContext> Run { get; }

        /// <summary>Gets or sets whether the button is on, asked when the window asks its commands again; null, until set, for always. A predicate that throws gives false.</summary>
        public Func<CadPluginContext, bool> CanRun { get; set; }
    }

    /// <summary>
    /// An item of a menu a plugin adds to, as the presenter's <c>CadExtensionMenuItem</c> is for an extension: a line that
    /// runs a callback, a submenu, or a separator, ticked or not, on or off.
    /// </summary>
    public sealed class CadPluginMenuItem
    {
        private readonly List<CadPluginMenuItem> _items;

        /// <summary>Makes an item that runs a callback.</summary>
        /// <param name="text">The text.</param>
        /// <param name="run">The callback, run at once on the window's thread.</param>
        /// <param name="isEnabled">Whether the item can be chosen.</param>
        /// <param name="isChecked">Null for no tick; true for a ticked item; false for a tick box not ticked.</param>
        /// <exception cref="ArgumentNullException">Thrown when the text or the callback is null.</exception>
        public CadPluginMenuItem(
            string text,
            Action<CadPluginContext> run,
            bool isEnabled = true,
            bool? isChecked = null
        )
        {
            Text = text ?? throw new ArgumentNullException(nameof(text));
            Run = run ?? throw new ArgumentNullException(nameof(run));
            IsEnabled = isEnabled;
            IsChecked = isChecked;
            _items = new List<CadPluginMenuItem>();
        }

        /// <summary>Makes an item that opens a submenu.</summary>
        /// <param name="text">The text.</param>
        /// <param name="items">The items of the submenu, in order; none of them null.</param>
        /// <param name="isEnabled">Whether the submenu can be opened.</param>
        /// <param name="isChecked">Null for no tick; true for a ticked item; false for a tick box not ticked.</param>
        /// <exception cref="ArgumentNullException">Thrown when the text or the items are null.</exception>
        /// <exception cref="ArgumentException">Thrown when an item is null.</exception>
        public CadPluginMenuItem(
            string text,
            IReadOnlyList<CadPluginMenuItem> items,
            bool isEnabled = true,
            bool? isChecked = null
        )
        {
            Text = text ?? throw new ArgumentNullException(nameof(text));
            _items = new List<CadPluginMenuItem>(items ?? throw new ArgumentNullException(nameof(items)));

            if (_items.Contains(null))
            {
                throw new ArgumentException("A submenu holds no null item.", nameof(items));
            }

            IsEnabled = isEnabled;
            IsChecked = isChecked;
        }

        private CadPluginMenuItem()
        {
            Text = string.Empty;
            IsEnabled = true;
            IsSeparator = true;
            _items = new List<CadPluginMenuItem>();
        }

        /// <summary>Gets the text; empty for a separator.</summary>
        public string Text { get; }

        /// <summary>Gets the callback; null for a submenu and a separator.</summary>
        public Action<CadPluginContext> Run { get; }

        /// <summary>Gets whether the item can be chosen.</summary>
        public bool IsEnabled { get; }

        /// <summary>Gets the tick: null for none, true for a ticked item, false for a tick box not ticked.</summary>
        public bool? IsChecked { get; }

        /// <summary>Gets the items of a submenu; empty for the others.</summary>
        public IReadOnlyList<CadPluginMenuItem> Items => _items;

        /// <summary>Gets whether the item is a separator line.</summary>
        public bool IsSeparator { get; }

        /// <summary>Makes a separator line.</summary>
        /// <returns>The separator.</returns>
        public static CadPluginMenuItem Separator() => new CadPluginMenuItem();
    }

    /// <summary>
    /// What a Properties provider is given when the panel is read: the drawing, the objects shown and what the panel shows
    /// so far, the presenter's groups and those of the extensions.
    /// </summary>
    public sealed class CadPropertyRequest
    {
        internal CadPropertyRequest(
            CadDocument document,
            IReadOnlyList<string> handles,
            CadSelectionInfo shown,
            string banner
        )
        {
            Document = document;
            Handles = handles ?? Array.Empty<string>();
            Shown = shown;
            Banner = banner;
        }

        /// <summary>Gets the drawing.</summary>
        public CadDocument Document { get; }

        /// <summary>Gets the handles of the objects shown, in ordinal order; empty for none.</summary>
        public IReadOnlyList<string> Handles { get; }

        /// <summary>Gets what the panel shows so far.</summary>
        public CadSelectionInfo Shown { get; }

        /// <summary>Gets or sets the banner the panel shows over its groups, as an extension's <c>DescribeSelection</c> gives it: null, as it is until a provider sets it, for none.</summary>
        public string Banner { get; set; }
    }

    /// <summary>
    /// What a plugin gives the Properties panel, as the extension's <c>DescribeSelection</c> does: it gets the request and
    /// returns what the panel shows, typically the groups it was given and its own besides. Null shows the panel as it was.
    /// Providers run after the extensions, in the order the plugins were loaded; one that throws is passed over for that call,
    /// with a Warn. A row with a setter is committed by the window as an extension's row is.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <returns>What the panel shows; null for as it was.</returns>
    public delegate CadSelectionInfo CadPropertyProvider(CadPropertyRequest request);

    /// <summary>What a DRAWINGS menu provider is given when the right-click menu of a row opens.</summary>
    public sealed class CadDrawingsRequest
    {
        internal CadDrawingsRequest(string path, CadDocument document, IReadOnlyList<string> selectedPaths)
        {
            Path = path;
            Document = document;
            SelectedPaths = selectedPaths ?? Array.Empty<string>();
        }

        /// <summary>Gets the full path of the row under the cursor.</summary>
        public string Path { get; }

        /// <summary>Gets the drawing of the row; null where the file is not open.</summary>
        public CadDocument Document { get; }

        /// <summary>Gets the full paths of the rows selected, the one under the cursor among them.</summary>
        public IReadOnlyList<string> SelectedPaths { get; }
    }

    /// <summary>
    /// What a plugin adds to the right-click menu of a row of DRAWINGS: items at its end, after a separator, asked each time
    /// the menu opens, for the rows that are files and not for folder headers.
    /// </summary>
    /// <param name="request">The row.</param>
    /// <returns>The items; null or empty for none.</returns>
    public delegate IReadOnlyList<CadPluginMenuItem> CadDrawingsMenuProvider(CadDrawingsRequest request);

    /// <summary>
    /// What a plugin adds to the right-click menu of the canvas, as AutoCAD's object context menu extension does: items after
    /// those of the extensions and before the presenter's own, asked with objects selected and no command running; the
    /// objects are <see cref="CadPluginContext.Selected"/>.
    /// </summary>
    /// <param name="context">The window now.</param>
    /// <returns>The items; null or empty for none.</returns>
    public delegate IReadOnlyList<CadPluginMenuItem> CadCanvasMenuProvider(CadPluginContext context);
}
