using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows.Media;
using GeometryHelper.Dxf;

namespace GeometryHelper.Cad.Wpf
{
    /// <summary>
    /// What a drag of a tool out of a tool palette carries, as one text: the place of its palette in the set and of the tool in
    /// the palette, which the canvas reads back where it is dropped, and the palette reads back to put the tool at another place.
    /// </summary>
    public static class ToolPaletteDrag
    {
        /// <summary>The name of the data format a drag of a tool carries.</summary>
        public const string Format = "GeometryHelper.ToolPaletteTool";

        /// <summary>Writes the places as one text.</summary>
        /// <param name="palette">The place of the palette among the palettes, from 0.</param>
        /// <param name="tool">The place of the tool in the palette, from 0.</param>
        /// <returns>The text.</returns>
        public static string Write(int palette, int tool) =>
            palette.ToString(System.Globalization.CultureInfo.InvariantCulture)
            + "\n"
            + tool.ToString(System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>Reads what <see cref="Write"/> wrote.</summary>
        /// <param name="text">The text.</param>
        /// <param name="palette">Receives the place of the palette.</param>
        /// <param name="tool">Receives the place of the tool.</param>
        /// <returns>Whether the text was one a drag wrote.</returns>
        public static bool TryRead(string text, out int palette, out int tool)
        {
            palette = 0;
            tool = 0;
            string[] parts = (text ?? string.Empty).Split('\n');
            return parts.Length == 2
                && int.TryParse(
                    parts[0],
                    System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out palette
                )
                && int.TryParse(
                    parts[1],
                    System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out tool
                );
        }
    }

    /// <summary>What a drag of objects out of the drawing carries onto a tool palette: their handles, one to a line.</summary>
    public static class ObjectDrag
    {
        /// <summary>The name of the data format a drag of objects carries.</summary>
        public const string Format = "GeometryHelper.Objects";

        /// <summary>Writes the handles as one text.</summary>
        /// <param name="handles">The handles.</param>
        /// <returns>The text.</returns>
        public static string Write(IEnumerable<string> handles) => string.Join("\n", handles ?? Array.Empty<string>());

        /// <summary>Reads the handles back.</summary>
        /// <param name="text">The text.</param>
        /// <returns>The handles, in order.</returns>
        public static IReadOnlyList<string> Read(string text) =>
            (text ?? string.Empty).Split('\n').Where(h => h.Length > 0).ToList();
    }

    /// <summary>A tab of the tool palettes: the name of a palette, which is shown when the tab is chosen.</summary>
    public sealed class ToolPaletteTabViewModel : ObservableObject
    {
        private bool _isActive;

        internal ToolPaletteTabViewModel(CadToolPalette palette)
        {
            Palette = palette;
        }

        /// <summary>Gets the palette the tab is of.</summary>
        public CadToolPalette Palette { get; }

        /// <summary>Gets the name on the tab.</summary>
        public string Name => Palette.Name;

        /// <summary>Gets or sets whether the tab is the one chosen.</summary>
        public bool IsActive
        {
            get => _isActive;
            internal set => Set(ref _isActive, value);
        }

        /// <summary>Says the name changed.</summary>
        internal void Refresh() => Raise(nameof(Name));
    }

    /// <summary>A row or icon of the palette shown: a tool, a line of text or a separator, with its picture where it has one.</summary>
    public sealed class ToolItemViewModel : ObservableObject
    {
        private readonly ToolPalettesViewModel _owner;
        private ImageSource _picture;
        private bool _pictureMade;

        internal ToolItemViewModel(ToolPalettesViewModel owner, CadTool tool)
        {
            _owner = owner;
            Tool = tool;
        }

        /// <summary>Gets the tool.</summary>
        public CadTool Tool { get; }

        /// <summary>Gets what the tool is.</summary>
        public CadToolKind Kind => Tool.Kind;

        /// <summary>Gets the name under the icon, the words of a text, nothing for a separator.</summary>
        public string Name => Tool.Kind == CadToolKind.Separator ? string.Empty : Tool.Name;

        /// <summary>Gets whether the item is a tool that runs: a block, a hatch or a command.</summary>
        public bool IsTool => Tool.Kind != CadToolKind.Text && Tool.Kind != CadToolKind.Separator;

        /// <summary>Gets whether the palette lists its tools as rows, so that the item is drawn as one.</summary>
        public bool UsesRow => _owner.IsList;

        /// <summary>Gets whether the item is a line of text.</summary>
        public bool IsText => Tool.Kind == CadToolKind.Text;

        /// <summary>Gets whether the item is a separator.</summary>
        public bool IsSeparator => Tool.Kind == CadToolKind.Separator;

        /// <summary>Gets the tooltip: the name, the description under it, and the command, block or pattern the tool runs.</summary>
        public string Tooltip
        {
            get
            {
                string what =
                    Tool.Kind == CadToolKind.Command ? "Command: " + Tool.Command
                    : Tool.Kind == CadToolKind.Block
                        ? "Block: "
                            + (Tool.BlockName.Length > 0 ? Tool.BlockName : "the whole drawing")
                            + (Tool.File == null ? string.Empty : "\nFrom: " + Tool.File)
                    : Tool.Kind == CadToolKind.Hatch
                        ? "Hatch: "
                            + (
                                Tool.HatchType == CadHatchType.Solid ? "solid fill"
                                : Tool.HatchType == CadHatchType.Gradient ? Tool.Gradient
                                : Tool.HatchType == CadHatchType.UserDefined ? "lines of your own"
                                : Tool.Pattern
                            )
                    : null;

                return string.Join("\n", new[] { Name, Tool.Description, what }.Where(s => !string.IsNullOrEmpty(s)));
            }
        }

        /// <summary>
        /// Gets the key of the icon of a command tool, as the icon set of the window knows it (<c>GhIconLine</c>); the plain
        /// command icon for a tool that names none, or for a block or a hatch whose picture could not be drawn.
        /// </summary>
        public string IconKey =>
            Tool.Kind == CadToolKind.Block ? "GhIconInsertBlock"
            : Tool.Kind == CadToolKind.Hatch ? "GhIconHatch"
            : string.IsNullOrWhiteSpace(Tool.Icon) ? "GhIconPluginCommand"
            : "GhIcon" + Tool.Icon.Trim();

        /// <summary>Gets the picture of a block or hatch tool, as large as the palette's icons, made when first asked; null for any other, or where it cannot be drawn.</summary>
        public ImageSource Picture
        {
            get
            {
                if (!_pictureMade)
                {
                    _pictureMade = true;
                    _picture = _owner.PictureOf(Tool);
                }

                return _picture;
            }
        }

        /// <summary>Says the tool changed, so that what shows of it is read again; its picture is drawn again.</summary>
        internal void Refresh()
        {
            _pictureMade = false;
            _picture = null;
            Raise(string.Empty);
        }
    }

    /// <summary>
    /// The tool palettes of the window: a tab for each palette, the tools of the one chosen, and everything the menus do to
    /// them: run a tool, make, rename and delete a palette, add a line of text or a separator, cut, copy, paste, delete and
    /// rename tools, how the palette is listed, import and export of palettes, and what is dropped on it (objects of the drawing,
    /// blocks of DesignCenter). Every change is written to the palettes' file at once.
    /// </summary>
    public sealed class ToolPalettesViewModel : ObservableObject
    {
        private readonly MainViewModel _model;
        private readonly Dictionary<string, BlockPictures> _blockPictures = new Dictionary<string, BlockPictures>(
            StringComparer.OrdinalIgnoreCase
        );
        private CadDocument _picturesOf;
        private string _message = string.Empty;

        internal ToolPalettesViewModel(MainViewModel model)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            Tabs = new ObservableCollection<ToolPaletteTabViewModel>();
            Items = new ObservableCollection<ToolItemViewModel>();
            Reload();
            model.ToolPalettesChanged += OnPalettesChanged;
        }

        /// <summary>Stops following the model, as the window does when it closes, so that the model does not keep the view model alive.</summary>
        public void Detach() => _model.ToolPalettesChanged -= OnPalettesChanged;

        private void OnPalettesChanged(object sender, EventArgs e) => Reload();

        /// <summary>Gets the window's view model, whose drawing the tools run on.</summary>
        public MainViewModel Model => _model;

        /// <summary>Gets the set of palettes.</summary>
        public CadToolPalettes ToolSet => _model.ToolPalettes;

        /// <summary>Gets the tabs, one for each palette, in order.</summary>
        public ObservableCollection<ToolPaletteTabViewModel> Tabs { get; }

        /// <summary>Gets the items of the palette chosen, in order.</summary>
        public ObservableCollection<ToolItemViewModel> Items { get; }

        /// <summary>Gets the palette chosen; null where there is none.</summary>
        public CadToolPalette Active => ToolSet.Active;

        /// <summary>Gets whether there is a palette chosen.</summary>
        public bool HasPalette => ToolSet.Active != null;

        /// <summary>Gets what the palette said last: a tool run, a palette imported, a file that did not read.</summary>
        public string Message
        {
            get => _message;
            private set => Set(ref _message, value ?? string.Empty);
        }

        /// <summary>Gets how the chosen palette lists its tools; icons alone where there is none.</summary>
        public CadToolViewStyle ViewStyle => Active?.ViewStyle ?? CadToolViewStyle.IconOnly;

        /// <summary>Gets the size of the icons of the chosen palette, in pixels.</summary>
        public int IconSize => Active?.IconSize ?? CadToolPalette.DefaultIconSize;

        /// <summary>Gets whether the palette lists its tools as rows.</summary>
        public bool IsList => ViewStyle == CadToolViewStyle.List;

        /// <summary>Gets whether a tool shows its name: with its icon beside, or under it.</summary>
        public bool ShowsNames => ViewStyle != CadToolViewStyle.IconOnly;

        /// <summary>Gets the width of the cell of a tool, in pixels.</summary>
        public double CellWidth => IsList ? double.NaN : IconSize + (ShowsNames ? 40 : 12);

        /// <summary>Gets the height of the cell of a tool, in pixels.</summary>
        public double CellHeight => IsList ? double.NaN : IconSize + (ShowsNames ? 32 : 12);

        /// <summary>Gets whether Paste has something to paste: tools were cut or copied.</summary>
        public bool CanPaste => HasPalette && ToolSet.Clipboard.Count > 0;

        /// <summary>Gets whether the palette shows nothing: there is none, or it has no tools.</summary>
        public bool IsEmpty => Items.Count == 0;

        /// <summary>Gets what an empty palette says: how to add tools, or how to make a palette.</summary>
        public string EmptyText =>
            HasPalette
                ? "This palette has no tools: drag objects of the drawing, or blocks of DesignCenter, onto it."
                : "There is no palette: right-click and choose New Palette.";

        /// <summary>
        /// Reads the set again into the tabs and the items: after the set was changed outside the window, or the drawing shown
        /// changed (the pictures of the blocks of the drawing are drawn again).
        /// </summary>
        public void Reload()
        {
            _blockPictures.Clear();
            Tabs.Clear();

            foreach (CadToolPalette palette in ToolSet.Palettes)
            {
                Tabs.Add(new ToolPaletteTabViewModel(palette));
            }

            ShowActive();
        }

        /// <summary>Chooses a palette, as a click on its tab does.</summary>
        /// <param name="tab">The tab; one not of the palettes does nothing.</param>
        public void Choose(ToolPaletteTabViewModel tab)
        {
            int at = tab == null ? -1 : Tabs.IndexOf(tab);

            if (at < 0)
            {
                return;
            }

            ToolSet.ActiveIndex = at;
            ShowActive();
            Changed();
        }

        /// <summary>Runs a tool, as a click on it does; a line of text or a separator does nothing.</summary>
        /// <param name="item">The tool's item.</param>
        /// <returns>Whether it started.</returns>
        public bool Run(ToolItemViewModel item)
        {
            if (item == null || !item.IsTool)
            {
                return false;
            }

            bool started = _model.RunTool(item.Tool);
            Message = started ? item.Name + " started." : item.Name + " did not start: see the command line.";
            return started;
        }

        /// <summary>Makes an empty palette after the last, named as given (made unique), and chooses it.</summary>
        /// <param name="name">The name; blank is "New Palette".</param>
        /// <returns>The tab.</returns>
        public ToolPaletteTabViewModel NewPalette(string name)
        {
            CadToolPalette palette = ToolSet.Add(name);
            Reload();
            Changed();
            return Tabs.Single(t => ReferenceEquals(t.Palette, palette));
        }

        /// <summary>Renames a palette; a name another has gets a number after it.</summary>
        /// <param name="tab">The palette's tab.</param>
        /// <param name="name">The name.</param>
        /// <returns>Whether it was renamed: the name was not blank.</returns>
        public bool RenamePalette(ToolPaletteTabViewModel tab, string name)
        {
            if (tab == null || !ToolSet.Rename(tab.Palette, name))
            {
                return false;
            }

            tab.Refresh();
            Changed();
            return true;
        }

        /// <summary>Deletes a palette with its tools.</summary>
        /// <param name="tab">The palette's tab.</param>
        /// <returns>Whether it was deleted.</returns>
        public bool DeletePalette(ToolPaletteTabViewModel tab)
        {
            if (tab == null || !ToolSet.Remove(tab.Palette))
            {
                return false;
            }

            Reload();
            Changed();
            return true;
        }

        /// <summary>Adds a line of text after the tool given, or last; AutoCAD's Add Text.</summary>
        /// <param name="text">The words.</param>
        /// <param name="after">The item it follows; null for last.</param>
        /// <returns>The item made; null where there is no palette.</returns>
        public ToolItemViewModel AddText(string text, ToolItemViewModel after = null) =>
            AddItem(CadTool.ForText(string.IsNullOrWhiteSpace(text) ? "Text" : text.Trim()), after);

        /// <summary>Adds a separator after the tool given, or last; AutoCAD's Add Separator.</summary>
        /// <param name="after">The item it follows; null for last.</param>
        /// <returns>The item made; null where there is no palette.</returns>
        public ToolItemViewModel AddSeparator(ToolItemViewModel after = null) => AddItem(CadTool.ForSeparator(), after);

        /// <summary>Cuts tools: put on the clipboard and taken out of the palette.</summary>
        /// <param name="items">The items.</param>
        public void Cut(IReadOnlyList<ToolItemViewModel> items)
        {
            if (Active == null || items == null || items.Count == 0)
            {
                return;
            }

            ToolSet.Cut(Active, items.Select(i => i.Tool).ToList());
            ShowActive();
            Changed();
        }

        /// <summary>Copies tools to the clipboard.</summary>
        /// <param name="items">The items.</param>
        public void Copy(IReadOnlyList<ToolItemViewModel> items)
        {
            if (items == null || items.Count == 0)
            {
                return;
            }

            ToolSet.Copy(items.Select(i => i.Tool).ToList());
            Raise(nameof(CanPaste));
        }

        /// <summary>Pastes the clipboard into the palette after the item given, or last.</summary>
        /// <param name="after">The item it follows; null for last.</param>
        /// <returns>The items made.</returns>
        public IReadOnlyList<ToolItemViewModel> Paste(ToolItemViewModel after = null)
        {
            if (Active == null || ToolSet.Clipboard.Count == 0)
            {
                return Array.Empty<ToolItemViewModel>();
            }

            IReadOnlyList<CadTool> made = ToolSet.Paste(Active, PlaceAfter(after));
            ShowActive();
            Changed();
            return Items.Where(i => made.Contains(i.Tool)).ToList();
        }

        /// <summary>Deletes tools from the palette, with no copy.</summary>
        /// <param name="items">The items.</param>
        public void Delete(IReadOnlyList<ToolItemViewModel> items)
        {
            if (Active == null || items == null || items.Count == 0)
            {
                return;
            }

            foreach (ToolItemViewModel item in items)
            {
                Active.Remove(item.Tool);
            }

            ToolSet.Touch();
            ShowActive();
            Changed();
        }

        /// <summary>Renames a tool, or changes the words of a line of text.</summary>
        /// <param name="item">The item.</param>
        /// <param name="name">The name; blank leaves it as it is.</param>
        public void Rename(ToolItemViewModel item, string name)
        {
            if (item == null || item.Kind == CadToolKind.Separator || string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            item.Tool.Name = name.Trim();
            item.Refresh();
            ToolSet.Touch();
            Changed();
        }

        /// <summary>Tells the palette a tool's properties were changed in place (the Tool Properties dialog): its item is read again and the file written.</summary>
        /// <param name="item">The item.</param>
        public void PropertiesChanged(ToolItemViewModel item)
        {
            item?.Refresh();
            ToolSet.Touch();
            Changed();
        }

        /// <summary>Moves a tool to another place of the palette, as dragging it within the palette does.</summary>
        /// <param name="item">The item.</param>
        /// <param name="before">The item it is put before; null for last.</param>
        public void Move(ToolItemViewModel item, ToolItemViewModel before)
        {
            if (Active == null || item == null || ReferenceEquals(item, before))
            {
                return;
            }

            Active.Remove(item.Tool);
            Active.Insert(before == null ? Active.Tools.Count : Active.Tools.ToList().IndexOf(before.Tool), item.Tool);
            ToolSet.Touch();
            ShowActive();
            Changed();
        }

        /// <summary>
        /// Sets how the palette lists its tools, as the View Options dialog does: the style, and the size of the icons; for the
        /// chosen palette or for all.
        /// </summary>
        /// <param name="style">The style.</param>
        /// <param name="iconSize">The size, 16 to 96; one outside is kept inside.</param>
        /// <param name="allPalettes">Whether every palette takes it, not only the one chosen.</param>
        public void SetView(CadToolViewStyle style, int iconSize, bool allPalettes)
        {
            int size = Math.Max(CadToolPalette.MinIconSize, Math.Min(iconSize, CadToolPalette.MaxIconSize));

            foreach (
                CadToolPalette palette in allPalettes
                    ? ToolSet.Palettes
                    : (IEnumerable<CadToolPalette>)new[] { Active }.Where(p => p != null)
            )
            {
                palette.ViewStyle = style;
                palette.IconSize = size;
            }

            ToolSet.Touch();
            ShowActive();
            Changed();
        }

        /// <summary>Exports palettes to a file of their own: the one chosen, or all.</summary>
        /// <param name="path">The file.</param>
        /// <param name="all">Whether all palettes are written, not only the one chosen.</param>
        /// <returns>Whether it was written; the message says why not.</returns>
        public bool Export(string path, bool all)
        {
            try
            {
                CadToolPalettes.Export(
                    path,
                    all ? ToolSet.Palettes : (IEnumerable<CadToolPalette>)new[] { Active }.Where(p => p != null)
                );
                Message = (all ? "All palettes" : Active?.Name) + " exported to " + Path.GetFileName(path) + ".";
                return true;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                GeometryHelperLog.Warn(
                    "ToolPalettesViewModel: the palettes were not exported to " + path + ".",
                    exception
                );
                Message = "Cannot write " + path + ": " + exception.Message;
                return false;
            }
        }

        /// <summary>Imports the palettes of a file written by Export, after the last; the first is chosen.</summary>
        /// <param name="path">The file.</param>
        /// <returns>Whether it was read; the message says why not.</returns>
        public bool Import(string path)
        {
            try
            {
                IReadOnlyList<CadToolPalette> added = ToolSet.Import(path);
                Reload();
                Changed();
                Message =
                    added.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    + (added.Count == 1 ? " palette" : " palettes")
                    + " imported from "
                    + Path.GetFileName(path)
                    + ".";
                return true;
            }
            catch (Exception exception)
                when (exception is IOException
                    || exception is UnauthorizedAccessException
                    || exception is FormatException
                )
            {
                GeometryHelperLog.Warn(
                    "ToolPalettesViewModel: the palettes of " + path + " were not imported.",
                    exception
                );
                Message = "Cannot import " + path + ": " + exception.Message;
                return false;
            }
        }

        /// <summary>
        /// Adds what is dropped on the palette: objects dragged from the drawing become tools of the layer, colour, linetype and
        /// lineweight they have; blocks, or a drawing file, dragged from DesignCenter become block tools of that drawing.
        /// Anything else is not taken.
        /// </summary>
        /// <param name="format">The data format of the drop: <see cref="ObjectDrag.Format"/> or <see cref="DesignCenterDrag.Format"/>.</param>
        /// <param name="text">The text the drag carried.</param>
        /// <param name="before">The item the tools are put before; null for last.</param>
        /// <returns>Whether any tool was added.</returns>
        public bool Drop(string format, string text, ToolItemViewModel before = null)
        {
            if (Active == null)
            {
                return false;
            }

            int index = PlaceBefore(before);
            int count = 0;

            if (format == ObjectDrag.Format)
            {
                count = _model.AddObjectsToToolPalette(Active, ObjectDrag.Read(text), index).Count;
            }
            else if (
                format == DesignCenterDrag.Format
                && DesignCenterDrag.TryRead(
                    text,
                    out string path,
                    out CadDesignCenterKind? kind,
                    out IReadOnlyList<string> names
                )
            )
            {
                if (kind == null)
                {
                    Active.Insert(index, CadTool.ForBlock(Path.GetFileNameWithoutExtension(path), path, string.Empty));
                    count = 1;
                }
                else if (kind == CadDesignCenterKind.Blocks)
                {
                    foreach (string block in names)
                    {
                        Active.Insert(index + count, CadTool.ForBlock(block, path, block));
                        count++;
                    }
                }
            }

            if (count > 0)
            {
                ToolSet.Touch();
                ShowActive();
                Changed();
                Message =
                    count.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    + (count == 1 ? " tool" : " tools")
                    + " added to "
                    + Active.Name
                    + ".";
            }

            return count > 0;
        }

        /// <summary>The picture of a block or hatch tool at the palette's icon size; null for any other tool, or one that cannot be drawn.</summary>
        internal ImageSource PictureOf(CadTool tool)
        {
            int size = IconSize;

            try
            {
                if (tool.Kind == CadToolKind.Hatch)
                {
                    return HatchSettingsViewModel.Render(
                        CadToolRun.HatchSettingsOf(tool, new CadHatchSettings()).Preview(_model.Document),
                        size,
                        size
                    );
                }

                if (tool.Kind == CadToolKind.Block)
                {
                    CadFonts fonts = _model.Document?.Fonts;

                    if (tool.BlockName.Length == 0)
                    {
                        return tool.File == null
                            ? null
                            : ExternalReferencesViewModel.PreviewOf(tool.File, false, fonts, size, size);
                    }

                    return PicturesOf(tool, fonts, size)?.Draw(tool.BlockName);
                }
            }
            catch (Exception exception)
                when (exception is ArgumentException
                    || exception is InvalidOperationException
                    || exception is IOException
                    || exception is NotSupportedException
                    || exception is FormatException
                    || exception is UnauthorizedAccessException
                )
            {
                GeometryHelperLog.Debug("ToolPalettesViewModel: no picture of " + tool.Name + ": " + exception.Message);
            }

            return null;
        }

        /// <summary>The pictures of the blocks of the drawing a tool takes its block from: the drawing shown where it has the block, else the tool's file.</summary>
        private BlockPictures PicturesOf(CadTool tool, CadFonts fonts, int size)
        {
            CadDocument document = _model.Document;

            if (!ReferenceEquals(_picturesOf, document))
            {
                _blockPictures.Clear();
                _picturesOf = document;
            }

            bool own = document != null && document.Database.BlockTable.Has(tool.BlockName);
            string key =
                (own ? string.Empty : tool.File ?? string.Empty)
                + "|"
                + size.ToString(System.Globalization.CultureInfo.InvariantCulture);

            if (_blockPictures.TryGetValue(key, out BlockPictures known))
            {
                return known;
            }

            DxfDatabase source =
                own ? document.Database
                : tool.File == null || !File.Exists(tool.File) ? null
                : CadDesignCenterDrawing.Open(tool.File).WorkingCopy();
            return _blockPictures[key] = source == null ? null : new BlockPictures(source, fonts, size, size);
        }

        private ToolItemViewModel AddItem(CadTool tool, ToolItemViewModel after)
        {
            if (Active == null)
            {
                return null;
            }

            Active.Insert(PlaceAfter(after), tool);
            ToolSet.Touch();
            ShowActive();
            Changed();
            return Items.Single(i => ReferenceEquals(i.Tool, tool));
        }

        private int PlaceAfter(ToolItemViewModel after) =>
            after == null ? Active.Tools.Count : Active.Tools.ToList().IndexOf(after.Tool) + 1;

        private int PlaceBefore(ToolItemViewModel before) =>
            before == null ? Active.Tools.Count : Active.Tools.ToList().IndexOf(before.Tool);

        /// <summary>Lists the tools of the palette chosen, and the view it is listed in.</summary>
        private void ShowActive()
        {
            Items.Clear();

            foreach (ToolPaletteTabViewModel tab in Tabs)
            {
                tab.IsActive = ReferenceEquals(tab.Palette, Active);
            }

            if (Active != null)
            {
                foreach (CadTool tool in Active.Tools)
                {
                    Items.Add(new ToolItemViewModel(this, tool));
                }
            }

            Raise(nameof(Active));
            Raise(nameof(HasPalette));
            Raise(nameof(ViewStyle));
            Raise(nameof(IconSize));
            Raise(nameof(IsList));
            Raise(nameof(ShowsNames));
            Raise(nameof(CellWidth));
            Raise(nameof(CellHeight));
            Raise(nameof(CanPaste));
            Raise(nameof(IsEmpty));
            Raise(nameof(EmptyText));
        }

        /// <summary>Writes the palettes' file after a change.</summary>
        private void Changed()
        {
            _model.SaveToolPalettes();
            Raise(nameof(CanPaste));
        }
    }
}
