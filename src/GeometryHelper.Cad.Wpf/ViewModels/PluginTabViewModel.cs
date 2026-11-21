using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GeometryHelper.Cad.Plugins;
using WpfGeometry = System.Windows.Media.Geometry;

namespace GeometryHelper.Cad.Wpf
{
    /// <summary>
    /// What a tooltip of a plugin's button says: a title, a body where the plugin gave one, and under a line the name of the
    /// plugin, so that a user knows whose button it is. The title is the button's tooltip, or its text where it has none, and for
    /// a command button the command in brackets as the presenter's tooltips show it: "Number circles (NUMCIRCLES2)".
    /// </summary>
    public sealed class PluginToolTipInfo
    {
        internal PluginToolTipInfo(string title, string body, string plugin)
        {
            Title = title ?? string.Empty;
            Body = body ?? string.Empty;
            Plugin = plugin ?? string.Empty;
        }

        /// <summary>Gets the first line, in semibold.</summary>
        public string Title { get; }

        /// <summary>Gets the body under it; empty for none.</summary>
        public string Body { get; }

        /// <summary>Gets whether there is a body.</summary>
        public bool HasBody => Body.Length > 0;

        /// <summary>Gets the name of the plugin, in the foot.</summary>
        public string Plugin { get; }

        /// <inheritdoc/>
        public override string ToString() => Title;
    }

    /// <summary>The icons of a plugin's buttons made drawable: a path into a frozen geometry, bytes into a frozen picture.</summary>
    internal static class PluginUi
    {
        // The presenter's own plugin icon (GhIconPluginCommand of Icons.xaml), drawn where the plugin gave no icon or one the
        // window cannot read.
        private const string FallbackPath = "M5 9h14M5 15h14M10 3 8 21M16 3l-2 18";

        /// <summary>
        /// Makes what draws an icon: a geometry for path data, a picture for bytes, the plugin's icon for none or for one that
        /// cannot be read, which is logged as one Warn.
        /// </summary>
        /// <param name="icon">The icon; null for none.</param>
        /// <param name="plugin">The plugin's name, for the Warn.</param>
        /// <param name="owner">What the icon belongs to, for the Warn.</param>
        /// <param name="geometry">The path of a drawn icon; null for a picture.</param>
        /// <param name="image">The picture; null for a drawn icon.</param>
        internal static void Make(
            CadPluginIcon icon,
            string plugin,
            string owner,
            out WpfGeometry geometry,
            out BitmapSource image
        )
        {
            geometry = null;
            image = null;

            if (icon != null)
            {
                try
                {
                    if (icon.PathData != null)
                    {
                        geometry = WpfGeometry.Parse(icon.PathData);
                        geometry.Freeze();
                        return;
                    }

                    var bitmap = new BitmapImage();

                    using (var stream = new MemoryStream(icon.ImageBytes))
                    {
                        bitmap.BeginInit();
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.StreamSource = stream;
                        bitmap.EndInit();
                    }

                    bitmap.Freeze();
                    image = bitmap;
                    return;
                }
                catch (Exception exception) when (!(exception is OutOfMemoryException))
                {
                    geometry = null;
                    image = null;
                    GeometryHelperLog.Warn(
                        "PluginUi: the icon of " + owner + " of the plugin " + plugin + " cannot be read.",
                        exception
                    );
                }
            }

            geometry = WpfGeometry.Parse(FallbackPath);
            geometry.Freeze();
        }

        /// <summary>Gets whether a picture is smaller than the room it is fitted into, so that it is scaled up pixel by pixel.</summary>
        internal static bool ScalesUp(BitmapSource image, double slot) =>
            image != null && Math.Max(image.PixelWidth, image.PixelHeight) < slot;

        /// <summary>
        /// Asks whether a button is on: its predicate, or where it has none, a command button while a document is open and a
        /// callback button always. A predicate that throws gives false, with one Warn for the button.
        /// </summary>
        internal static bool CanRun(
            Func<CadPluginContext, bool> canRun,
            CadPluginContext context,
            bool isCommand,
            string plugin,
            string text,
            ref bool warned
        )
        {
            if (canRun == null)
            {
                return !isCommand || context.Document != null;
            }

            try
            {
                return canRun(context);
            }
            catch (Exception exception) when (!(exception is OutOfMemoryException))
            {
                if (!warned)
                {
                    warned = true;
                    GeometryHelperLog.Warn(
                        "PluginUi: CanRun of the button " + text + " of the plugin " + plugin + " threw.",
                        exception
                    );
                }

                return false;
            }
        }
    }

    /// <summary>An item of a panel of a plugin's ribbon: its icon, its text and its tooltip, large or standard as the plugin said.</summary>
    public abstract class PluginItemViewModel : ObservableObject
    {
        private protected PluginItemViewModel(
            string text,
            bool showText,
            bool isLarge,
            CadPluginIcon icon,
            string plugin,
            PluginToolTipInfo toolTip
        )
        {
            Text = text;
            ShowText = showText;
            IsLarge = isLarge;
            ToolTipInfo = toolTip;
            PluginUi.Make(icon, plugin, text, out WpfGeometry geometry, out BitmapSource image);
            IconGeometry = geometry;
            IconImage = image;
        }

        /// <summary>Gets the text, under a large button's icon or beside a standard one's.</summary>
        public string Text { get; }

        /// <summary>Gets whether the text is shown; false draws the icon alone.</summary>
        public bool ShowText { get; }

        /// <summary>Gets whether the item is a large one: an icon over its text, alone in its column.</summary>
        public bool IsLarge { get; }

        /// <summary>Gets the icon's path, frozen, drawn as the presenter's own icons are; null where the icon is a picture.</summary>
        public WpfGeometry IconGeometry { get; }

        /// <summary>Gets the icon's picture, frozen; null where the icon is a path.</summary>
        public BitmapSource IconImage { get; }

        /// <summary>Gets the room of the icon: 32 pixels for a large item, 16 for a standard one.</summary>
        public double IconSlot => IsLarge ? 32.0 : 16.0;

        /// <summary>Gets the size a path icon is drawn at inside its room: 26 for a large item, as the presenter's, 16 for a standard one.</summary>
        public double PathSize => IsLarge ? 26.0 : 16.0;

        /// <summary>Gets the width of a path icon's line: 1.6 for a large item, 1.8 for a standard one, as the presenter's.</summary>
        public double PathStroke => IsLarge ? 1.6 : 1.8;

        /// <summary>Gets how a picture is scaled: pixel by pixel where it is smaller than its room, smoothly where it is larger.</summary>
        public BitmapScalingMode ImageScaling =>
            PluginUi.ScalesUp(IconImage, IconSlot) ? BitmapScalingMode.NearestNeighbor : BitmapScalingMode.Fant;

        /// <summary>Gets the tooltip: the title, the body and the plugin's name.</summary>
        public PluginToolTipInfo ToolTipInfo { get; }

        /// <summary>Asks the plugin again whether the item is on.</summary>
        /// <param name="context">The context of the window now.</param>
        internal abstract void Refresh(CadPluginContext context);

        /// <inheritdoc/>
        public override string ToString() => Text;
    }

    /// <summary>
    /// A button of a plugin's ribbon: a command it starts as the presenter's ribbon buttons start theirs, or a callback it runs
    /// at once, on while the plugin's <see cref="CadRibbonButton.CanRun"/> says so.
    /// </summary>
    public sealed class PluginButtonViewModel : PluginItemViewModel
    {
        private readonly MainViewModel _model;
        private readonly string _plugin;
        private readonly CadRibbonButton _source;
        private readonly Action<PluginButtonViewModel> _used;
        private bool _isEnabled;
        private bool _warned;

        internal PluginButtonViewModel(
            MainViewModel model,
            string plugin,
            CadRibbonButton source,
            CadRibbonItem look,
            CadPluginIcon icon,
            Action<PluginButtonViewModel> used
        )
            : base(
                source.Text,
                look.ShowText,
                look.Size == CadRibbonItemSize.Large,
                icon,
                plugin,
                new PluginToolTipInfo(
                    (source.ToolTip ?? source.Text)
                        + (source.Command == null ? string.Empty : " (" + CommandName(source.Command) + ")"),
                    source.Description ?? look.Description,
                    plugin
                )
            )
        {
            _model = model;
            _plugin = plugin;
            _source = source;
            _used = used;
            RunCommand = new DelegateCommand(_ => Run());
        }

        /// <summary>Gets whether the button is on: asked again where the window asks its own ribbon commands.</summary>
        public bool IsEnabled
        {
            get => _isEnabled;
            private set => Set(ref _isEnabled, value);
        }

        /// <summary>Gets the command it starts, as typed ("NUMCIRCLES2"); null for a callback button.</summary>
        public string Command => _source.Command;

        /// <summary>Gets what pressing the button does: starts the command, or runs the callback.</summary>
        public ICommand RunCommand { get; }

        /// <summary>Gets a command's name as the tooltips show it: without a leading "." or "_", in capitals.</summary>
        internal static string CommandName(string command) => command.Trim().TrimStart('.', '_').ToUpperInvariant();

        internal override void Refresh(CadPluginContext context) =>
            IsEnabled = PluginUi.CanRun(_source.CanRun, context, _source.Command != null, _plugin, Text, ref _warned);

        private void Run()
        {
            if (_source.Command != null)
            {
                _model.StartPluginCommand(_source.Command);
            }
            else
            {
                _model.RunPluginCallback(_plugin, _source.Run);
            }

            _used?.Invoke(this);
        }
    }

    /// <summary>
    /// A split button of a plugin's ribbon: the top shows and runs the button used last (the first where the plugin does not ask
    /// for the last), the arrow opens the list of them all.
    /// </summary>
    public sealed class PluginSplitViewModel : PluginItemViewModel
    {
        private readonly bool _remembersLast;
        private PluginButtonViewModel _top;
        private bool _isOpen;

        internal PluginSplitViewModel(MainViewModel model, string plugin, CadRibbonSplitButton source)
            : base(
                source.Text,
                source.ShowText,
                source.Size == CadRibbonItemSize.Large,
                source.Icon,
                plugin,
                new PluginToolTipInfo(source.ToolTip ?? source.Text, source.Description, plugin)
            )
        {
            _remembersLast = source.RemembersLast;
            Buttons = source
                .Buttons.Select(b => new PluginButtonViewModel(model, plugin, b, source, b.Icon ?? source.Icon, Used))
                .ToList();
            _top = Buttons[0];
        }

        /// <summary>Gets the buttons the arrow lists, in the order the plugin gave them.</summary>
        public IReadOnlyList<PluginButtonViewModel> Buttons { get; }

        /// <summary>Gets the button the top shows and runs: the one used last, or the first.</summary>
        public PluginButtonViewModel Top
        {
            get => _top;
            private set => Set(ref _top, value);
        }

        /// <summary>Gets or sets whether the list is open, so that the split is drawn lit behind its top and arrow.</summary>
        public bool IsOpen
        {
            get => _isOpen;
            set => Set(ref _isOpen, value);
        }

        internal override void Refresh(CadPluginContext context)
        {
            foreach (PluginButtonViewModel button in Buttons)
            {
                button.Refresh(context);
            }
        }

        private void Used(PluginButtonViewModel button)
        {
            if (_remembersLast)
            {
                Top = button;
            }
        }
    }

    /// <summary>A column of a panel: one large item alone, or up to three standard ones one under the other.</summary>
    public sealed class PluginColumnViewModel
    {
        internal PluginColumnViewModel(IReadOnlyList<PluginItemViewModel> items) => Items = items;

        /// <summary>Gets the items, top down.</summary>
        public IReadOnlyList<PluginItemViewModel> Items { get; }
    }

    /// <summary>
    /// A panel of a plugin's ribbon, on its own tab or on the Plugins tab: its items laid out as AutoCAD's row panels are, a
    /// large item alone in its column and consecutive standard ones in columns of three, top down, and its title under them.
    /// </summary>
    public sealed class PluginPanelViewModel
    {
        internal PluginPanelViewModel(MainViewModel model, string plugin, CadRibbonPanel source)
        {
            Source = source;
            Title = source.Title;
            PluginName = plugin;
            var columns = new List<PluginColumnViewModel>();
            var standard = new List<PluginItemViewModel>();

            void Close()
            {
                if (standard.Count > 0)
                {
                    columns.Add(new PluginColumnViewModel(standard.ToArray()));
                    standard.Clear();
                }
            }

            foreach (CadRibbonItem item in source.Items.ToList())
            {
                if (item == null)
                {
                    continue;
                }

                PluginItemViewModel view = item is CadRibbonSplitButton split
                    ? (PluginItemViewModel)new PluginSplitViewModel(model, plugin, split)
                    : new PluginButtonViewModel(model, plugin, (CadRibbonButton)item, item, item.Icon, null);

                if (item.Size == CadRibbonItemSize.Large)
                {
                    Close();
                    columns.Add(new PluginColumnViewModel(new[] { view }));
                    continue;
                }

                standard.Add(view);

                if (standard.Count == 3)
                {
                    Close();
                }
            }

            Close();
            Columns = columns;
        }

        /// <summary>Gets the title, under the panel.</summary>
        public string Title { get; }

        /// <summary>The panel the plugin described, which tells a panel kept across a rebuild from a new one.</summary>
        internal CadRibbonPanel Source { get; }

        /// <summary>Gets the name of the plugin the panel belongs to.</summary>
        public string PluginName { get; }

        /// <summary>Gets the columns, left to right.</summary>
        public IReadOnlyList<PluginColumnViewModel> Columns { get; }

        internal void Refresh(CadPluginContext context)
        {
            foreach (PluginItemViewModel item in Columns.SelectMany(c => c.Items))
            {
                item.Refresh(context);
            }
        }

        /// <inheritdoc/>
        public override string ToString() => Title;
    }

    /// <summary>What a plugin's ribbon tab shows: its panels, left to right.</summary>
    public sealed class PluginTabViewModel
    {
        internal PluginTabViewModel(CadRibbonTab source, string plugin, IReadOnlyList<PluginPanelViewModel> panels)
        {
            Id = source.Id;
            Title = source.Title;
            PluginName = plugin;
            IsContextual = source.ShowWhen != null;
            Panels = panels;
        }

        /// <summary>Gets the tab's id, as the plugin gave it.</summary>
        public string Id { get; }

        /// <summary>Gets the tab's title.</summary>
        public string Title { get; }

        /// <summary>Gets the name of the plugin the tab belongs to.</summary>
        public string PluginName { get; }

        /// <summary>Gets whether the tab is a contextual one, shown while the plugin's <see cref="CadRibbonTab.ShowWhen"/> is true.</summary>
        public bool IsContextual { get; }

        /// <summary>Gets the panels, left to right.</summary>
        public IReadOnlyList<PluginPanelViewModel> Panels { get; }

        internal void Refresh(CadPluginContext context)
        {
            foreach (PluginPanelViewModel panel in Panels)
            {
                panel.Refresh(context);
            }
        }

        /// <inheritdoc/>
        public override string ToString() => Title;
    }
}
