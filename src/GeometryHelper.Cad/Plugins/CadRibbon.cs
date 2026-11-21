using System;
using System.Collections.Generic;

namespace GeometryHelper.Cad.Plugins
{
    /// <summary>
    /// A tab of the ribbon a plugin adds, as AutoCAD's <c>RibbonTab</c>: panels of buttons. It comes after the Plugins tab and
    /// before Settings, in the order the plugins were loaded; with <see cref="ShowWhen"/> set it is a contextual tab instead,
    /// green and shown only while the answer is true.
    /// </summary>
    public sealed class CadRibbonTab
    {
        /// <summary>Makes a tab.</summary>
        /// <param name="id">The tab's identity, unique among the loaded plugins, either case the same: "Circles". A tab whose id another plugin loaded first uses is left out, with a line in <see cref="CadPlugin.Problems"/>.</param>
        /// <param name="title">The title on the tab strip.</param>
        /// <exception cref="ArgumentNullException">Thrown when the id or the title is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the id is empty.</exception>
        public CadRibbonTab(string id, string title)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));

            if (id.Trim().Length == 0)
            {
                throw new ArgumentException("A tab's id is not empty.", nameof(id));
            }

            Title = title ?? throw new ArgumentNullException(nameof(title));
        }

        /// <summary>Gets the tab's identity.</summary>
        public string Id { get; }

        /// <summary>Gets the title on the tab strip.</summary>
        public string Title { get; }

        /// <summary>Gets the panels, left to right; read when <see cref="ICadPluginApplication.Initialize"/> returns.</summary>
        public IList<CadRibbonPanel> Panels { get; } = new List<CadRibbonPanel>();

        /// <summary>
        /// Gets or sets what makes the tab contextual, as AutoCAD's Text Editor tab is: asked when the selection, the drawing
        /// or a command changes, the tab is shown, selected as it appears, while it answers true, and goes with Home selected
        /// again when it answers false. Null, until set, for a tab that is always shown. A callback that throws gives false.
        /// </summary>
        public Func<CadPluginContext, bool> ShowWhen { get; set; }
    }

    /// <summary>A panel of a ribbon tab: a titled row of items, as AutoCAD's <c>RibbonPanel</c>.</summary>
    public sealed class CadRibbonPanel
    {
        /// <summary>Makes a panel.</summary>
        /// <param name="title">The title under the panel.</param>
        /// <exception cref="ArgumentNullException">Thrown when the title is null.</exception>
        public CadRibbonPanel(string title)
        {
            Title = title ?? throw new ArgumentNullException(nameof(title));
        }

        /// <summary>Gets the title under the panel.</summary>
        public string Title { get; }

        /// <summary>
        /// Gets the items, left to right: a large item stands alone in its column, and standard items that follow one
        /// another stack in columns of three, top down, as AutoCAD's row panels do.
        /// </summary>
        public IList<CadRibbonItem> Items { get; } = new List<CadRibbonItem>();
    }

    /// <summary>How big an item of a panel is drawn.</summary>
    public enum CadRibbonItemSize
    {
        /// <summary>The icon 16 pixels beside the text, or alone where <see cref="CadRibbonItem.ShowText"/> is false; three to a column.</summary>
        Standard,

        /// <summary>The icon 32 pixels over the text, alone in its column.</summary>
        Large,
    }

    /// <summary>What a panel holds: a <see cref="CadRibbonButton"/> or a <see cref="CadRibbonSplitButton"/>.</summary>
    public abstract class CadRibbonItem
    {
        private protected CadRibbonItem(string text)
        {
            Text = text ?? throw new ArgumentNullException(nameof(text));
        }

        /// <summary>Gets the text under or beside the icon.</summary>
        public string Text { get; }

        /// <summary>Gets or sets the icon; null for the presenter's plugin icon.</summary>
        public CadPluginIcon Icon { get; set; }

        /// <summary>Gets or sets how big the item is drawn: <see cref="CadRibbonItemSize.Standard"/> until set.</summary>
        public CadRibbonItemSize Size { get; set; } = CadRibbonItemSize.Standard;

        /// <summary>Gets or sets whether the text is drawn: true until set; a standard item with no text shows its icon alone.</summary>
        public bool ShowText { get; set; } = true;

        /// <summary>Gets or sets the title line of the tooltip; null, until set, for <see cref="Text"/>.</summary>
        public string ToolTip { get; set; }

        /// <summary>Gets or sets the body of the tooltip; null, until set, for none.</summary>
        public string Description { get; set; }
    }

    /// <summary>
    /// A button that starts a command, as a button of the presenter's own ribbon does, or runs a callback of the plugin at once
    /// on the window's thread.
    /// </summary>
    public sealed class CadRibbonButton : CadRibbonItem
    {
        /// <summary>Makes a button that starts a command: a command running is cancelled first, as AutoCAD's ^C^C is.</summary>
        /// <param name="text">The text of the button.</param>
        /// <param name="command">The command's name: "NUMCIRCLES", "_NUMCIRCLES" and ".NUMCIRCLES" start the same command.</param>
        /// <exception cref="ArgumentNullException">Thrown when the text or the command is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the command is empty.</exception>
        public CadRibbonButton(string text, string command)
            : base(text)
        {
            Command = command ?? throw new ArgumentNullException(nameof(command));

            if (command.Trim().Length == 0)
            {
                throw new ArgumentException("A button's command is not empty.", nameof(command));
            }
        }

        /// <summary>Makes a button that runs a callback at once, on the window's thread.</summary>
        /// <param name="text">The text of the button.</param>
        /// <param name="run">The callback. One that throws is logged as a Warn and its message written on the command line with the plugin's name.</param>
        /// <exception cref="ArgumentNullException">Thrown when the text or the callback is null.</exception>
        public CadRibbonButton(string text, Action<CadPluginContext> run)
            : base(text)
        {
            Run = run ?? throw new ArgumentNullException(nameof(run));
        }

        /// <summary>Gets the command the button starts; null for a button that runs a callback.</summary>
        public string Command { get; }

        /// <summary>Gets the callback the button runs; null for a button that starts a command.</summary>
        public Action<CadPluginContext> Run { get; }

        /// <summary>
        /// Gets or sets whether the button is on, asked when the window asks its commands again (the selection, the drawing
        /// or a command changes). Null, until set: a button that starts a command is on while a drawing is open, one that
        /// runs a callback always. A predicate that throws gives false.
        /// </summary>
        public Func<CadPluginContext, bool> CanRun { get; set; }
    }

    /// <summary>
    /// A button with a drop-down of others, as AutoCAD's Circle button is: the part on top runs one button, the arrow lists
    /// them all. With <see cref="RemembersLast"/> the top shows and runs the one used last.
    /// </summary>
    public sealed class CadRibbonSplitButton : CadRibbonItem
    {
        private readonly List<CadRibbonButton> _buttons;

        /// <summary>Makes a split button.</summary>
        /// <param name="text">The text of the button, shown until a button of the list is used and remembered.</param>
        /// <param name="buttons">The buttons of the drop-down, at least one, none null.</param>
        /// <exception cref="ArgumentNullException">Thrown when the text or the buttons are null.</exception>
        /// <exception cref="ArgumentException">Thrown when there is no button, or one is null.</exception>
        public CadRibbonSplitButton(string text, IReadOnlyList<CadRibbonButton> buttons)
            : base(text)
        {
            if (buttons == null)
            {
                throw new ArgumentNullException(nameof(buttons));
            }

            _buttons = new List<CadRibbonButton>(buttons);

            if (_buttons.Count == 0)
            {
                throw new ArgumentException("A split button holds at least one button.", nameof(buttons));
            }

            if (_buttons.Contains(null))
            {
                throw new ArgumentException("A split button holds no null button.", nameof(buttons));
            }
        }

        /// <summary>Gets the buttons of the drop-down, in the order given.</summary>
        public IReadOnlyList<CadRibbonButton> Buttons => _buttons;

        /// <summary>Gets or sets whether the top shows and runs the button used last, as AutoCAD's Circle split does: true until set; false for always the first.</summary>
        public bool RemembersLast { get; set; } = true;
    }
}
