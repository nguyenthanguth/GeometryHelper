using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Input;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// The Layer manager in a layout, as AutoCAD 2021's: the VP columns (VP Freeze, New VP Freeze, VP Color, VP Linetype, VP
    /// Lineweight, VP Transparency, VP Plot Style), shown only while a layout is current and acting on the viewport worked
    /// through or the one selected on the paper; the right-click items that freeze and thaw a layer in all viewports, add one
    /// frozen in them all and take overrides out; and the rows chosen together, as the list's own selection.
    /// </summary>
    public sealed partial class LayerManagerViewModel
    {
        /// <summary>The transparencies the VP Transparency cell offers, in per cent as AutoCAD's: 0 to 90 by tens.</summary>
        public static readonly IReadOnlyList<string> TransparencyNames = new[] { "0", "10", "20", "30", "40", "50", "60", "70", "80", "90" };

        private DxfViewport _target;
        private bool _hasViewportColumns;
        private ICommand _newLayerVpFrozen, _vpFreezeAll, _vpThawAll, _removeOverrides, _selectAll, _clearAll, _allButCurrent, _invert, _isolate;

        /// <summary>Gets whether the VP columns are shown: a layout is current, as AutoCAD shows them only there.</summary>
        public bool HasViewportColumns
        {
            get => _hasViewportColumns;
            private set => Set(ref _hasViewportColumns, value);
        }

        /// <summary>Gets the width of the dialog: 1 400 with the VP columns, as the design of a layout draws it, else 900.</summary>
        public double WindowWidth => HasViewportColumns ? 1400 : 900;

        /// <summary>Gets the height of the dialog: 660 with the VP columns, else 560.</summary>
        public double WindowHeight => HasViewportColumns ? 660 : 560;

        /// <summary>Gets whether a viewport is there for the VP columns to act on: one worked through, or one alone selected on the paper.</summary>
        public bool HasTargetViewport => _target != null;

        /// <summary>Gets whether the drawing has named plot styles (an STB table), so that VP Plot Style is set; grey where it plots by colour.</summary>
        public bool HasNamedPlotStyles => !Document.Database.PlotStyleMode;

        /// <summary>Gets the plot styles of the drawing the VP Plot Style cell offers, in the order of their dictionary; empty where it plots by colour.</summary>
        public IReadOnlyList<string> PlotStyleNames { get; private set; } = Array.Empty<string>();

        /// <summary>Gets the command of "New layer VP frozen in all viewports": a layer named Layer1, or the first LayerN the drawing has not, frozen in every viewport.</summary>
        public ICommand NewLayerVpFrozenCommand => _newLayerVpFrozen ?? (_newLayerVpFrozen = new DelegateCommand(_ => NewLayerVpFrozen()));

        /// <summary>Gets the command of "VP Freeze Layer in All Viewports", on the rows chosen.</summary>
        public ICommand VpFreezeAllCommand => _vpFreezeAll ?? (_vpFreezeAll = new DelegateCommand(_ => Done(CadViewportLayerEditor.SetFrozenInAllViewports(Document, ChosenNames(), true))));

        /// <summary>
        /// Gets the command of "Isolate Selected Layers", as AutoCAD 2021's shortcut menu of the Layer Properties Manager: every
        /// layer but the rows chosen turned off, one undo step; the current layer too where it is not chosen, as AutoCAD lets the
        /// current layer be off. With layers A, B and C on and B chosen, A and C go off.
        /// </summary>
        public ICommand IsolateCommand => _isolate ?? (_isolate = new DelegateCommand(_ => Isolate()));

        /// <summary>Gets the command of "VP Thaw Layer in All Viewports", on the rows chosen.</summary>
        public ICommand VpThawAllCommand => _vpThawAll ?? (_vpThawAll = new DelegateCommand(_ => Done(CadViewportLayerEditor.SetFrozenInAllViewports(Document, ChosenNames(), false))));

        /// <summary>
        /// Gets the command of "Remove Viewport Overrides for": its parameter "Selected,Current", "Selected,All", "All,Current" or
        /// "All,All", the layers (those chosen or every one) and the viewports (the one the columns act on, or all).
        /// </summary>
        public ICommand RemoveOverridesCommand => _removeOverrides ?? (_removeOverrides = new DelegateCommand(p => RemoveOverrides(p as string)));

        /// <summary>Gets the command of Select All: every row chosen.</summary>
        public ICommand SelectAllCommand => _selectAll ?? (_selectAll = new DelegateCommand(_ => Choose(r => true)));

        /// <summary>Gets the command of Clear All: no row chosen.</summary>
        public ICommand ClearAllCommand => _clearAll ?? (_clearAll = new DelegateCommand(_ => Choose(r => false)));

        /// <summary>Gets the command of Select All but Current: every row but the current layer's chosen.</summary>
        public ICommand SelectAllButCurrentCommand => _allButCurrent ?? (_allButCurrent = new DelegateCommand(_ => Choose(r => !r.IsCurrent)));

        /// <summary>Gets the command of Invert Selection: the rows chosen let go, the others chosen.</summary>
        public ICommand InvertSelectionCommand => _invert ?? (_invert = new DelegateCommand(_ => Choose(r => !r.IsChosen)));

        /// <summary>Adds a layer named Layer1, or the first LayerN the drawing has not, frozen in every viewport of every layout, and chooses it.</summary>
        /// <returns>The name added, or null where it was refused.</returns>
        public string NewLayerVpFrozen()
        {
            int n = 1;

            while (Document.Database.LayerTable.Has("Layer" + n.ToString(CultureInfo.InvariantCulture)))
            {
                n++;
            }

            string name = "Layer" + n.ToString(CultureInfo.InvariantCulture);

            if (!Done(CadViewportLayerEditor.AddFrozenInAllViewports(Document, name)))
            {
                return null;
            }

            SelectedRow = Rows.FirstOrDefault(r => string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase));
            return name;
        }

        /// <summary>The names of the rows chosen, in the order of the rows; the row of <see cref="SelectedRow"/> where none is.</summary>
        private void Isolate()
        {
            var kept = new HashSet<string>(ChosenNames(), StringComparer.OrdinalIgnoreCase);

            if (kept.Count == 0)
            {
                return;
            }

            Document.BeginGroup("LAYER");

            try
            {
                foreach (string name in Document.Database.LayerTable.Select(l => l.Name).ToList())
                {
                    if (!kept.Contains(name) && !Document.Database.LayerTable[name].IsOff && !Done(CadLayerEditor.SetOn(Document, name, false)))
                    {
                        break;
                    }
                }
            }
            finally
            {
                Document.EndGroup();
            }
        }

        internal List<string> ChosenNames()
        {
            var names = Rows.Where(r => r.IsChosen).Select(r => r.Name).ToList();

            if (names.Count == 0 && SelectedRow != null)
            {
                names.Add(SelectedRow.Name);
            }

            return names;
        }

        /// <summary>The viewport the VP columns act on; null for none.</summary>
        internal DxfViewport Target => _target;

        /// <summary>Reads which viewport the VP columns act on, and the plot styles the drawing has.</summary>
        private void ReadViewport()
        {
            HasViewportColumns = Document.CurrentSpace != null && !Document.CurrentSpace.IsModel;
            _target = CadViewportLayerEditor.TargetViewport(Document);
            Raise(nameof(WindowWidth));
            Raise(nameof(WindowHeight));
            Raise(nameof(HasTargetViewport));
            Raise(nameof(HasNamedPlotStyles));
            Raise(nameof(Title));
            DxfDBDictionary names = HasNamedPlotStyles ? Document.Database.PlotStyleNameDictionary : null;
            PlotStyleNames = names == null ? Array.Empty<string>() : names.Select(e => e.Key).ToList();
        }

        /// <summary>The part of the title naming the layout and the viewport the VP columns act on: " · Layout1, viewport 2 1:20 current".</summary>
        private string ViewportTitle()
        {
            if (!HasViewportColumns)
            {
                return string.Empty;
            }

            string layout = Document.CurrentSpace.Name;
            return _target == null
                ? " · " + layout + ", no viewport current"
                : " · " + layout + ", viewport " + _target.Number.ToString(CultureInfo.InvariantCulture) + " " + CadViewports.ScaleName(_target.CustomScale) + " current";
        }

        /// <summary>The line under the table while a viewport is acted on: "8 layers · 1 frozen in this viewport · 2 overrides".</summary>
        private string ViewportSummary(int layers)
        {
            int frozen = Rows.Count(r => r.IsVpFrozen);
            int overrides = Rows.Sum(r => r.OverrideCount);
            return MainViewModel.Count(layers) + (layers == 1 ? " layer" : " layers") + " · " + MainViewModel.Count(frozen) + " frozen in this viewport · "
                + MainViewModel.Count(overrides) + (overrides == 1 ? " override" : " overrides");
        }

        private void RemoveOverrides(string which)
        {
            string[] parts = (which ?? string.Empty).Split(',');
            bool selected = parts.Length == 2 && parts[0] == "Selected";
            bool current = parts.Length == 2 && parts[1] == "Current";

            if (current && _target == null)
            {
                Say("There is no current viewport: enter one (MSPACE) or select one first.");
                return;
            }

            List<string> layers = selected ? ChosenNames() : Rows.Select(r => r.Name).ToList();
            Done(CadViewportLayerEditor.RemoveOverrides(Document, layers, current ? _target.Handle : null));
        }

        private void Choose(Func<LayerRowViewModel, bool> chosen)
        {
            foreach (LayerRowViewModel row in Rows)
            {
                row.IsChosen = chosen(row);
            }
        }

        /// <summary>Shows Select Color for a row's VP Color and overrides the layer's colour in the viewport with the one chosen, one undo step.</summary>
        internal void ChooseVpColor(LayerRowViewModel row)
        {
            var dialog = new SelectColorViewModel(row.VpColor, false);

            if (_target != null && PickColor != null && PickColor(dialog) && !dialog.Color.Equals(row.VpColor))
            {
                Done(CadViewportLayerEditor.SetColor(Document, _target.Handle, row.Name, dialog.Color));
            }
        }

        /// <summary>Shows Select Linetype for a row's VP Linetype and overrides the layer's linetype in the viewport, one undo step.</summary>
        internal void ChooseVpLinetype(LayerRowViewModel row)
        {
            var dialog = new SelectLinetypeViewModel(Document, row.VpLinetype);

            if (_target != null && PickLinetype != null && PickLinetype(dialog) && !string.Equals(dialog.Linetype, row.VpLinetype, StringComparison.OrdinalIgnoreCase))
            {
                Done(CadViewportLayerEditor.SetLinetype(Document, _target.Handle, row.Name, dialog.Linetype));
            }
        }
    }

    /// <summary>One layer of the Layer manager, as the viewport the VP columns act on shows it.</summary>
    public sealed partial class LayerRowViewModel
    {
        private DxfLayerViewportProperties _look;
        private bool _vpFrozen;
        private bool _newVpFrozen;
        private bool _chosen;
        private string _colorIndexName;
        private DxfTransparency _ownTransparency = new DxfTransparency(255);

        /// <summary>Gets or sets whether the row is among those chosen, which the right-click items act on.</summary>
        public bool IsChosen
        {
            get => _chosen;
            set => Set(ref _chosen, value);
        }

        /// <summary>Gets or sets whether the layer is frozen in the viewport the VP columns act on (VP Freeze).</summary>
        public bool IsVpFrozen
        {
            get => _vpFrozen;
            set
            {
                DxfViewport target = _owner.Target;
                Change(target != null && value != _vpFrozen && _owner.Done(CadViewportLayerEditor.SetFrozen(_owner.Document, new[] { target.Handle }, new[] { Name }, value)), nameof(IsVpFrozen));
            }
        }

        /// <summary>Gets or sets whether the layer is frozen in each viewport made from now on (New VP Freeze).</summary>
        public bool IsNewVpFrozen
        {
            get => _newVpFrozen;
            set => Change(value != _newVpFrozen && _owner.Done(CadViewportLayerEditor.SetFrozenInNewViewports(_owner.Document, Name, value)), nameof(IsNewVpFrozen));
        }

        /// <summary>Gets the colour of the layer in the viewport: its override, else its own.</summary>
        public DxfColor VpColor => _look?.Color ?? Color;

        /// <summary>Gets the VP colour as 0xRRGGBB, the swatch.</summary>
        public int VpRgb => VpColor.ColorValue & 0xFFFFFF;

        /// <summary>Gets the VP colour by name, as the colour cell names it.</summary>
        public string VpColorName => VpColor.IsByColor ? SelectColorViewModel.Text(VpColor) : LayerManagerViewModel.ColorName(VpColor.ColorIndex);

        /// <summary>Gets whether the colour is overridden in the viewport, the cell marked.</summary>
        public bool IsVpColorOverridden => _look != null && _look.IsColorOverridden;

        /// <summary>Gets the command of the VP Color cell: Select Color, the colour chosen overriding the layer's in the viewport.</summary>
        public ICommand ChooseVpColorCommand => new DelegateCommand(_ => _owner.ChooseVpColor(this));

        /// <summary>Gets the linetype of the layer in the viewport.</summary>
        public string VpLinetype => _look?.Linetype ?? Linetype;

        /// <summary>Gets whether the linetype is overridden in the viewport.</summary>
        public bool IsVpLinetypeOverridden => _look != null && _look.IsLinetypeOverridden;

        /// <summary>Gets the command of the VP Linetype cell: Select Linetype, the linetype chosen overriding the layer's in the viewport.</summary>
        public ICommand ChooseVpLinetypeCommand => new DelegateCommand(_ => _owner.ChooseVpLinetype(this));

        /// <summary>Gets or sets the lineweight of the layer in the viewport: Default, or one of AutoCAD's widths as 0.35.</summary>
        public string VpWeight
        {
            get => WeightText(_look?.LineWeight ?? _weight);
            set
            {
                DxfViewport target = _owner.Target;

                if (target == null || !TryWeight(value, out DxfLineWeight weight))
                {
                    Raise(nameof(VpWeight));
                    return;
                }

                Change(weight != (_look?.LineWeight ?? _weight) && _owner.Done(CadViewportLayerEditor.SetLineWeight(_owner.Document, target.Handle, Name, weight)), nameof(VpWeight));
            }
        }

        /// <summary>Gets whether the lineweight is overridden in the viewport.</summary>
        public bool IsVpWeightOverridden => _look != null && _look.IsLineWeightOverridden;

        /// <summary>Gets or sets the transparency of the layer in the viewport in per cent, 0 to 90, as AutoCAD's column.</summary>
        public string VpTransparency
        {
            get => Percent(_look?.Transparency ?? _ownTransparency).ToString(CultureInfo.InvariantCulture);
            set
            {
                DxfViewport target = _owner.Target;

                if (target == null || !int.TryParse((value ?? string.Empty).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int percent) || percent < 0 || percent > 90)
                {
                    if (target != null)
                    {
                        _owner.Done(CadEditResult.Refuse("Invalid transparency \"" + value + "\": give 0 to 90."));
                    }

                    Raise(nameof(VpTransparency));
                    return;
                }

                var alpha = new DxfTransparency((byte)Math.Floor(255.0 * (100 - percent) / 100.0));
                Change(percent.ToString(CultureInfo.InvariantCulture) != VpTransparency && _owner.Done(CadViewportLayerEditor.SetTransparency(_owner.Document, target.Handle, Name, alpha)), nameof(VpTransparency));
            }
        }

        /// <summary>Gets whether the transparency is overridden in the viewport.</summary>
        public bool IsVpTransparencyOverridden => _look != null && _look.IsTransparencyOverridden;

        /// <summary>Gets the transparencies the VP Transparency cell offers: 0 to 90 by tens, and the layer's own where it is another.</summary>
        public IReadOnlyList<string> TransparencyOptions
        {
            get
            {
                var options = new List<string>(LayerManagerViewModel.TransparencyNames);

                if (!options.Contains(VpTransparency))
                {
                    options.Add(VpTransparency);
                }

                return options;
            }
        }

        /// <summary>
        /// Gets or sets the plot style of the layer in the viewport: in a drawing plotted by colour Color_7 after the layer's
        /// colour, grey and not set; in one of named plot styles the name, set from the drawing's plot styles.
        /// </summary>
        public string VpPlotStyle
        {
            get => _owner.HasNamedPlotStyles ? (_look?.PlotStyleName ?? string.Empty) : "Color_" + _colorIndexName;
            set
            {
                DxfViewport target = _owner.Target;
                string wanted = (value ?? string.Empty).Trim();
                Change(target != null && _owner.HasNamedPlotStyles && !string.Equals(wanted, VpPlotStyle, StringComparison.Ordinal)
                    && _owner.Done(CadViewportLayerEditor.SetPlotStyle(_owner.Document, target.Handle, Name, wanted)), nameof(VpPlotStyle));
            }
        }

        /// <summary>Gets whether the plot style is overridden in the viewport.</summary>
        public bool IsVpPlotStyleOverridden => _look != null && _look.IsPlotStyleOverridden;

        /// <summary>Gets the plot styles the VP Plot Style cell offers.</summary>
        public IReadOnlyList<string> PlotStyleOptions => _owner.PlotStyleNames;

        /// <summary>Gets how many properties of the layer are overridden in the viewport.</summary>
        public int OverrideCount => (IsVpColorOverridden ? 1 : 0) + (IsVpLinetypeOverridden ? 1 : 0) + (IsVpWeightOverridden ? 1 : 0) + (IsVpTransparencyOverridden ? 1 : 0) + (IsVpPlotStyleOverridden ? 1 : 0);

        /// <summary>Reads the layer as a viewport shows it; null for none, the VP cells then showing the layer's own.</summary>
        internal void ReadViewport(DxfLayerTableRecord record, DxfViewport viewport)
        {
            _look = viewport == null ? null : record.GetViewportOverrides(viewport);
            _vpFrozen = viewport != null && viewport.IsLayerFrozenInViewport(record);
            _newVpFrozen = record.ViewportVisibilityDefault;
            _colorIndexName = Math.Abs(record.ColorIndex).ToString(CultureInfo.InvariantCulture);
            _ownTransparency = record.Transparency;
        }

        private static string WeightText(DxfLineWeight weight)
            => weight == DxfLineWeight.ByLineWeightDefault ? "Default" : ((int)weight / 100.0).ToString("0.00", CultureInfo.InvariantCulture);

        private static int Percent(DxfTransparency transparency)
            => transparency.IsByAlpha ? (int)Math.Round((255 - transparency.Alpha) * 100.0 / 255.0, MidpointRounding.AwayFromZero) : 0;
    }
}
