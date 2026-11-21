using System;
using System.Collections.Generic;
using System.Windows.Input;

namespace GeometryHelper.Cad.Wpf
{
    /// <summary>
    /// An item an extension adds to the canvas's right-click menu for the objects selected
    /// (<see cref="CadWorkspaceExtension.CanvasMenuItems"/>): its text, whether it can be chosen, whether it is ticked, and what
    /// choosing it does, or the items of a menu of its own. The presenter draws it in its own menu, with its own look.
    /// </summary>
    /// <remarks>
    /// "Fit to sheet" that runs a call is <c>new CadExtensionMenuItem("Fit to sheet", () => Fit())</c>; the four ways to show a
    /// group of bars, the one in use ticked, are the items of <c>new CadExtensionMenuItem("Change representation", ways)</c>
    /// with <c>isChecked: true</c> on one of them.
    /// </remarks>
    public sealed class CadExtensionMenuItem
    {
        private CadExtensionMenuItem() { }

        /// <summary>Makes an item that runs <paramref name="action"/> when it is chosen.</summary>
        /// <param name="text">The text, as "Delete view".</param>
        /// <param name="action">What choosing it does.</param>
        /// <param name="isEnabled">Whether it can be chosen; false draws it grey.</param>
        /// <param name="isChecked">Whether it is ticked; null for an item that is not a tick.</param>
        /// <exception cref="ArgumentException">Thrown when the text is empty.</exception>
        /// <exception cref="ArgumentNullException">Thrown when the text or the action is null.</exception>
        public CadExtensionMenuItem(string text, Action action, bool isEnabled = true, bool? isChecked = null)
        {
            Text = Required(text);
            Action = action ?? throw new ArgumentNullException(nameof(action));
            IsEnabled = isEnabled;
            IsChecked = isChecked;
        }

        /// <summary>Makes an item that opens a menu of its own.</summary>
        /// <param name="text">The text, as "Change representation".</param>
        /// <param name="items">The items of the menu it opens, in order.</param>
        /// <param name="isEnabled">Whether it opens its menu; false draws it grey.</param>
        /// <param name="isChecked">Whether it is ticked; null for an item that is not a tick.</param>
        /// <exception cref="ArgumentException">Thrown when the text is empty.</exception>
        /// <exception cref="ArgumentNullException">Thrown when the text or the items are null.</exception>
        public CadExtensionMenuItem(
            string text,
            IReadOnlyList<CadExtensionMenuItem> items,
            bool isEnabled = true,
            bool? isChecked = null
        )
        {
            Text = Required(text);
            Items = items ?? throw new ArgumentNullException(nameof(items));
            IsEnabled = isEnabled;
            IsChecked = isChecked;
        }

        /// <summary>Gets the text; empty for a separator.</summary>
        public string Text { get; } = string.Empty;

        /// <summary>Gets what choosing it does; null for a separator and for an item that opens a menu.</summary>
        public Action Action { get; }

        /// <summary>Gets whether it can be chosen.</summary>
        public bool IsEnabled { get; } = true;

        /// <summary>Gets whether it is ticked; null for an item that is not a tick.</summary>
        public bool? IsChecked { get; }

        /// <summary>Gets the items of the menu it opens; empty for an item that opens none.</summary>
        public IReadOnlyList<CadExtensionMenuItem> Items { get; } = Array.Empty<CadExtensionMenuItem>();

        /// <summary>Gets whether it is a line between groups of items.</summary>
        public bool IsSeparator => Action == null && Items.Count == 0 && Text.Length == 0;

        /// <summary>Makes a line between groups of items.</summary>
        /// <returns>The separator.</returns>
        public static CadExtensionMenuItem Separator() => new CadExtensionMenuItem();

        private static string Required(string text)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                throw new ArgumentException("The text of a menu item is not empty.", nameof(text));
            }

            return text;
        }
    }

    /// <summary>
    /// A button an extension adds to the header of the DRAWINGS list (<see cref="CadWorkspaceExtension.FileListButtons"/>), beside
    /// the presenter's own Refresh and Open: a drawn icon, a tooltip, and what pressing it does.
    /// </summary>
    public sealed class CadExtensionButton
    {
        /// <summary>Makes a button.</summary>
        /// <param name="icon">The icon, drawn as a line 2 wide on a square of 24, as the presenter's own icons are: <c>Geometry.Parse("M20 11a8 8 0 1 0-2.3 5.7M20 5v6h-6")</c> is its refresh.</param>
        /// <param name="toolTip">The tooltip, as "Read the drawings from the model again".</param>
        /// <param name="action">What pressing it does.</param>
        /// <exception cref="ArgumentException">Thrown when the tooltip is empty.</exception>
        /// <exception cref="ArgumentNullException">Thrown when the icon, the tooltip or the action is null.</exception>
        public CadExtensionButton(System.Windows.Media.Geometry icon, string toolTip, Action action)
        {
            Icon = icon ?? throw new ArgumentNullException(nameof(icon));

            if (toolTip == null)
            {
                throw new ArgumentNullException(nameof(toolTip));
            }

            if (string.IsNullOrWhiteSpace(toolTip))
            {
                throw new ArgumentException("The tooltip of a button is not empty.", nameof(toolTip));
            }

            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            ToolTip = toolTip;
            Command = new DelegateCommand(_ => action());
        }

        /// <summary>
        /// Makes the button a plugin adds to the header of the DRAWINGS list: a drawn icon or a picture, and the plugin's name
        /// shown under its tooltip.
        /// </summary>
        internal CadExtensionButton(
            System.Windows.Media.Geometry icon,
            System.Windows.Media.ImageSource image,
            string toolTip,
            string pluginName,
            ICommand command
        )
        {
            Icon = icon;
            IconImage = image;
            ToolTip = toolTip;
            PluginName = pluginName;
            Command = command;
        }

        /// <summary>Gets the icon; null for the button of a plugin whose icon is a picture (<see cref="IconImage"/>).</summary>
        public System.Windows.Media.Geometry Icon { get; }

        /// <summary>Gets the picture drawn as the icon of a plugin's button, fitted into 16 pixels; null for a drawn icon.</summary>
        public System.Windows.Media.ImageSource IconImage { get; }

        /// <summary>Gets the name of the plugin that added the button, shown under its tooltip; null for an extension's button.</summary>
        public string PluginName { get; }

        /// <summary>Gets the tooltip.</summary>
        public string ToolTip { get; }

        /// <summary>Gets what pressing the button runs.</summary>
        public ICommand Command { get; }
    }
}
