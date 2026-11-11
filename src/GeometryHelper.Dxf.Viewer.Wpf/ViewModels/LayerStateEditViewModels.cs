using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows.Input;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// AutoCAD's New Layer State to Save: New layer state name, offered with the drawing's states to pick from, and an
    /// optional Description, which the Layer States Manager's Description column shows.
    /// </summary>
    public sealed class NewLayerStateViewModel : ObservableObject
    {
        private string _name;
        private string _description = string.Empty;

        /// <summary>Makes the dialog.</summary>
        /// <param name="names">The drawing's states, which the name's list offers.</param>
        /// <param name="offered">The name offered, as Layer state 1.</param>
        public NewLayerStateViewModel(IEnumerable<string> names, string offered)
        {
            Names = (names ?? Enumerable.Empty<string>()).ToList();
            _name = offered ?? string.Empty;
        }

        /// <summary>Gets the drawing's states, which the name's list offers.</summary>
        public IReadOnlyList<string> Names { get; }

        /// <summary>Gets or sets the new layer state name.</summary>
        public string Name
        {
            get => _name;
            set
            {
                if (Set(ref _name, value ?? string.Empty))
                {
                    Raise(nameof(CanOk));
                }
            }
        }

        /// <summary>Gets or sets the description; empty for none.</summary>
        public string Description
        {
            get => _description;
            set => Set(ref _description, value ?? string.Empty);
        }

        /// <summary>Gets whether OK is live: a name given.</summary>
        public bool CanOk => _name.Trim().Length > 0;
    }

    /// <summary>A property of Edit Layer State's Layer properties to restore: its name, its flag of the mask and whether it is ticked.</summary>
    public sealed class EditLayerStatePropertyViewModel : ObservableObject
    {
        private readonly EditLayerStateViewModel _owner;
        private bool _isChecked;

        internal EditLayerStatePropertyViewModel(
            EditLayerStateViewModel owner,
            string name,
            int flag,
            bool isFixed,
            bool isChecked
        )
        {
            _owner = owner;
            Name = name;
            Flag = flag;
            IsFixed = isFixed;
            _isChecked = isChecked;
        }

        /// <summary>Gets the name, as On/Off.</summary>
        public string Name { get; }

        /// <summary>Gets the flag of the mask.</summary>
        public int Flag { get; }

        /// <summary>Gets whether the property is greyed, as in the Layer States Manager: one of a viewport or a plot style.</summary>
        public bool IsFixed { get; }

        /// <summary>Gets whether the box can be ticked.</summary>
        public bool IsEnabled => !IsFixed;

        /// <summary>Gets or sets whether the state restores the property; unticked, its column greys.</summary>
        public bool IsChecked
        {
            get => _isChecked;
            set
            {
                if (Set(ref _isChecked, value))
                {
                    _owner.RaiseColumns();
                }
            }
        }
    }

    /// <summary>
    /// A layer of Edit Layer State: the properties the state saved for it, each changed in the dialog only. A layer the drawing
    /// no longer has is listed greyed and kept as it is; a layer whose properties differ from the drawing's, in a property the
    /// state restores, is marked.
    /// </summary>
    public sealed class EditLayerStateRowViewModel : ObservableObject
    {
        private readonly EditLayerStateViewModel _owner;
        private bool _isChosen;
        private string _transparency;

        internal EditLayerStateRowViewModel(EditLayerStateViewModel owner, CadLayerStateLayer layer)
        {
            _owner = owner;
            Layer = layer;
            _transparency = layer.Transparency.ToString(CultureInfo.InvariantCulture);
            ChooseColorCommand = new DelegateCommand(_ => _owner.ChooseColor(this), _ => !layer.IsMissing);
            ChooseLinetypeCommand = new DelegateCommand(_ => _owner.ChooseLinetype(this), _ => !layer.IsMissing);
        }

        /// <summary>Gets the layer of the state.</summary>
        public CadLayerStateLayer Layer { get; }

        /// <summary>Gets the name.</summary>
        public string Name => Layer.Name;

        /// <summary>Gets whether the drawing has no such layer: kept in the state, passed over on restore, its cells greyed.</summary>
        public bool IsMissing => Layer.IsMissing;

        /// <summary>Gets whether the row can be changed: a layer the drawing has.</summary>
        public bool IsEditable => !Layer.IsMissing;

        /// <summary>Gets whether the state's layer differs from the drawing's in a property the state restores.</summary>
        public bool Differs => _owner.Edit.Differs(Layer);

        /// <summary>Gets or sets whether the row is chosen, which Remove layers takes out.</summary>
        public bool IsChosen
        {
            get => _isChosen;
            set => Set(ref _isChosen, value);
        }

        /// <summary>Gets or sets whether the state turns the layer on.</summary>
        public bool IsOn
        {
            get => Layer.IsOn;
            set => Change(() => Layer.IsOn = value, nameof(IsOn));
        }

        /// <summary>Gets or sets whether the state freezes the layer.</summary>
        public bool IsFrozen
        {
            get => Layer.IsFrozen;
            set => Change(() => Layer.IsFrozen = value, nameof(IsFrozen));
        }

        /// <summary>Gets or sets whether the state locks the layer.</summary>
        public bool IsLocked
        {
            get => Layer.IsLocked;
            set => Change(() => Layer.IsLocked = value, nameof(IsLocked));
        }

        /// <summary>Gets or sets whether the state plots the layer.</summary>
        public bool IsPlotted
        {
            get => Layer.IsPlotted;
            set => Change(() => Layer.IsPlotted = value, nameof(IsPlotted));
        }

        /// <summary>Gets or sets whether the state freezes the layer in new viewports.</summary>
        public bool IsNewViewportFrozen
        {
            get => Layer.IsNewViewportFrozen;
            set => Change(() => Layer.IsNewViewportFrozen = value, nameof(IsNewViewportFrozen));
        }

        /// <summary>Gets the colour's swatch as 0xRRGGBB.</summary>
        public int Rgb => (Layer.Color.Red << 16) | (Layer.Color.Green << 8) | Layer.Color.Blue;

        /// <summary>Gets the colour's name: Red to White, "Color 30", or a true colour's 10,20,30.</summary>
        public string ColorName =>
            Layer.Color.IsByColor
                ? SelectColorViewModel.Text(Layer.Color)
                : LayerManagerViewModel.ColorName(Math.Abs(Layer.Color.ColorIndex));

        /// <summary>Gets the linetype's name.</summary>
        public string Linetype => Layer.Linetype;

        /// <summary>Gets or sets the lineweight: Default, or one of AutoCAD's widths in millimetres as 0.35.</summary>
        public string Weight
        {
            get =>
                Layer.LineWeight == DxfLineWeight.ByLineWeightDefault
                    ? "Default"
                    : ((int)Layer.LineWeight / 100.0).ToString("0.00", CultureInfo.InvariantCulture);
            set
            {
                string typed = (value ?? string.Empty).Trim();

                if (string.Equals(typed, "Default", StringComparison.OrdinalIgnoreCase))
                {
                    Change(() => Layer.LineWeight = DxfLineWeight.ByLineWeightDefault, nameof(Weight));
                    return;
                }

                if (
                    double.TryParse(typed, NumberStyles.Float, CultureInfo.InvariantCulture, out double mm)
                    && mm >= 0.0
                    && Enum.IsDefined(typeof(DxfLineWeight), (int)Math.Round(mm * 100.0))
                )
                {
                    Change(() => Layer.LineWeight = (DxfLineWeight)(int)Math.Round(mm * 100.0), nameof(Weight));
                    return;
                }

                Raise(nameof(Weight));
            }
        }

        /// <summary>Gets the weights the cell offers.</summary>
        public IReadOnlyList<string> WeightOptions => LayerManagerViewModel.WeightNames;

        /// <summary>Gets or sets the transparency, 0 to 90, as typed; another value is put back.</summary>
        public string Transparency
        {
            get => _transparency;
            set
            {
                if (
                    int.TryParse(
                        (value ?? string.Empty).Trim(),
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out int percent
                    )
                    && percent >= 0
                    && percent <= 90
                )
                {
                    _transparency = percent.ToString(CultureInfo.InvariantCulture);
                    Change(() => Layer.Transparency = percent, nameof(Transparency));
                    return;
                }

                Raise(nameof(Transparency));
            }
        }

        /// <summary>Gets the command of the colour cell: Select Color, through <see cref="EditLayerStateViewModel.PickColor"/>.</summary>
        public ICommand ChooseColorCommand { get; }

        /// <summary>Gets the command of the linetype cell: Select Linetype, through <see cref="EditLayerStateViewModel.PickLinetype"/>.</summary>
        public ICommand ChooseLinetypeCommand { get; }

        /// <inheritdoc/>
        public override string ToString() => Name;

        internal void Changed()
        {
            Raise(nameof(Rgb));
            Raise(nameof(ColorName));
            Raise(nameof(Linetype));
            Raise(nameof(Differs));
        }

        internal void RaiseDiffers() => Raise(nameof(Differs));

        private void Change(Action write, string property)
        {
            if (!Layer.IsMissing)
            {
                write();
            }

            Raise(property);
            Raise(nameof(Differs));
        }
    }

    /// <summary>
    /// AutoCAD's Edit Layer State of the Layer States Manager's Edit…: the layers the state saved with their properties, each
    /// changed in place (the colour by Select Color, the linetype by Select Linetype), Add layers (Select Layers to Add to
    /// Layer State, the drawing's layers the state does not name) and Remove layers, and the properties the state restores, an
    /// unticked one greying its column. OK writes the state, its layers and its properties, as an edit of LAYERSTATE's one
    /// undo step; the drawing's layers change only when the state is restored. Cancel writes nothing.
    /// </summary>
    public sealed class EditLayerStateViewModel : ObservableObject
    {
        private readonly CadDocument _document;

        /// <summary>Makes the dialog over the layers of a state.</summary>
        /// <param name="edit">The state's layers, as <see cref="CadLayerStatesRequest.BeginEdit"/> gives them.</param>
        /// <param name="document">The document, whose linetypes Select Linetype offers.</param>
        /// <exception cref="ArgumentNullException">Thrown when the edit or the document is null.</exception>
        public EditLayerStateViewModel(CadLayerStateEdit edit, CadDocument document)
        {
            Edit = edit ?? throw new ArgumentNullException(nameof(edit));
            _document = document ?? throw new ArgumentNullException(nameof(document));
            int mask = edit.Mask;
            EditLayerStatePropertyViewModel Property(string name, int flag, bool isFixed) =>
                new EditLayerStatePropertyViewModel(this, name, flag, isFixed, (mask & flag) != 0);
            Properties = new ReadOnlyCollection<EditLayerStatePropertyViewModel>(
                new[]
                {
                    Property("On/Off", 0x1, false),
                    Property("Frozen/Thawed", 0x2, false),
                    Property("Visibility in current viewport", 0x200, true),
                    Property("Locked/Unlocked", 0x4, false),
                    Property("Plot/No Plot", 0x8, false),
                    Property("Color", 0x20, false),
                    Property("Linetype", 0x40, false),
                    Property("Lineweight", 0x80, false),
                    Property("Transparency", 0x400, false),
                    Property("Plot style", 0x100, true),
                    Property("New VP freeze", 0x10, true),
                }
            );
            Rows = new ObservableCollection<EditLayerStateRowViewModel>(
                edit.Layers.Select(l => new EditLayerStateRowViewModel(this, l))
            );
        }

        /// <summary>Gets the state's layers being edited.</summary>
        public CadLayerStateEdit Edit { get; }

        /// <summary>Gets the title: "Edit Layer State - Plan - no dims".</summary>
        public string Title => "Edit Layer State - " + Edit.Name;

        /// <summary>Gets the line over the table: "8 of 11 layers of the drawing".</summary>
        public string Summary =>
            MainViewModel.Count(Rows.Count(r => !r.IsMissing))
            + " of "
            + MainViewModel.Count(_document.Database.LayerTable.Count(l => !l.IsDependent))
            + " layers of the drawing";

        /// <summary>Gets the layers of the state, by name.</summary>
        public ObservableCollection<EditLayerStateRowViewModel> Rows { get; }

        /// <summary>Gets the properties the state restores, in the order of the Layer States Manager's panel.</summary>
        public ReadOnlyCollection<EditLayerStatePropertyViewModel> Properties { get; }

        /// <summary>Gets the linetypes of the drawing.</summary>
        public IReadOnlyList<string> LinetypeNames =>
            _document
                .Database.LinetypeTable.Select(l => l.Name)
                .Where(n =>
                    !string.Equals(n, "ByLayer", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(n, "ByBlock", StringComparison.OrdinalIgnoreCase)
                )
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ThenBy(n => n, StringComparer.Ordinal)
                .ToList();

        /// <summary>Gets whether the On column is restored, and so not greyed.</summary>
        public bool RestoresOn => Restores(0x1);

        /// <summary>Gets whether the Freeze column is restored.</summary>
        public bool RestoresFrozen => Restores(0x2);

        /// <summary>Gets whether the Lock column is restored.</summary>
        public bool RestoresLocked => Restores(0x4);

        /// <summary>Gets whether the Plot column is restored.</summary>
        public bool RestoresPlot => Restores(0x8);

        /// <summary>Gets whether the Color column is restored.</summary>
        public bool RestoresColor => Restores(0x20);

        /// <summary>Gets whether the Linetype column is restored.</summary>
        public bool RestoresLinetype => Restores(0x40);

        /// <summary>Gets whether the Lineweight column is restored.</summary>
        public bool RestoresLineweight => Restores(0x80);

        /// <summary>Gets whether the Transparency column is restored.</summary>
        public bool RestoresTransparency => Restores(0x400);

        /// <summary>Gets whether the New VP Freeze column is restored.</summary>
        public bool RestoresNewViewportFreeze => Restores(0x10);

        /// <summary>Gets or sets how the window shows Select Color for a row's colour: true for OK.</summary>
        public Func<SelectColorViewModel, bool> PickColor { get; set; }

        /// <summary>Gets or sets how the window shows Select Linetype for a row's linetype: true for OK.</summary>
        public Func<SelectLinetypeViewModel, bool> PickLinetype { get; set; }

        /// <summary>
        /// Gets or sets how the window shows Select Layers to Add to Layer State: given the drawing's layers the state does not
        /// name, the ones chosen; null or empty for Cancel.
        /// </summary>
        public Func<IReadOnlyList<string>, IReadOnlyList<string>> PickLayers { get; set; }

        /// <summary>Add layers: the layers picked put in the state with their properties as they stand, and chosen.</summary>
        /// <returns>How many were put in.</returns>
        public int AddLayers()
        {
            IReadOnlyList<string> offered = Edit.LayersNotInState();
            IReadOnlyList<string> picked = offered.Count == 0 ? null : PickLayers?.Invoke(offered);
            int added = 0;

            foreach (string name in picked ?? Array.Empty<string>())
            {
                if (Edit.Add(name) != null)
                {
                    added++;
                }
            }

            if (added > 0)
            {
                var chosen = new HashSet<string>(picked, StringComparer.OrdinalIgnoreCase);
                Rows.Clear();

                foreach (CadLayerStateLayer layer in Edit.Layers)
                {
                    Rows.Add(new EditLayerStateRowViewModel(this, layer) { IsChosen = chosen.Contains(layer.Name) });
                }

                Raise(nameof(Summary));
            }

            return added;
        }

        /// <summary>Remove layers: the rows chosen taken out of the state.</summary>
        /// <returns>How many were taken out.</returns>
        public int RemoveLayers()
        {
            List<EditLayerStateRowViewModel> chosen = Rows.Where(r => r.IsChosen).ToList();

            foreach (EditLayerStateRowViewModel row in chosen)
            {
                Rows.Remove(row);
                Edit.Layers.Remove(row.Layer);
            }

            if (chosen.Count > 0)
            {
                Raise(nameof(Summary));
            }

            return chosen.Count;
        }

        /// <summary>Select All or Clear All: every property that can be ticked set at once.</summary>
        /// <param name="on">Whether they are ticked.</param>
        public void SetAll(bool on)
        {
            foreach (EditLayerStatePropertyViewModel property in Properties.Where(p => p.IsEnabled))
            {
                property.IsChecked = on;
            }
        }

        /// <summary>OK: the properties ticked written into the edit's mask, the greyed ones kept as the state has them.</summary>
        /// <returns>The edit, for <see cref="CadLayerStatesRequest.Edit"/>.</returns>
        public CadLayerStateEdit Commit()
        {
            Edit.Mask = Mask();
            return Edit;
        }

        internal void ChooseColor(EditLayerStateRowViewModel row)
        {
            var dialog = new SelectColorViewModel(row.Layer.Color, false);

            if (PickColor != null && PickColor(dialog) && !dialog.Color.Equals(row.Layer.Color))
            {
                row.Layer.Color = dialog.Color;
                row.Changed();
            }
        }

        internal void ChooseLinetype(EditLayerStateRowViewModel row)
        {
            var dialog = new SelectLinetypeViewModel(_document, row.Layer.Linetype);

            if (PickLinetype != null && PickLinetype(dialog) && !string.IsNullOrEmpty(dialog.Linetype))
            {
                row.Layer.Linetype = dialog.Linetype;
                row.Changed();
            }
        }

        internal void RaiseColumns()
        {
            Edit.Mask = Mask();

            foreach (
                string name in new[]
                {
                    nameof(RestoresOn),
                    nameof(RestoresFrozen),
                    nameof(RestoresLocked),
                    nameof(RestoresPlot),
                    nameof(RestoresColor),
                    nameof(RestoresLinetype),
                    nameof(RestoresLineweight),
                    nameof(RestoresTransparency),
                    nameof(RestoresNewViewportFreeze),
                }
            )
            {
                Raise(name);
            }

            foreach (EditLayerStateRowViewModel row in Rows)
            {
                row.RaiseDiffers();
            }
        }

        private bool Restores(int flag) => Properties.Any(p => p.Flag == flag && p.IsChecked);

        private int Mask()
        {
            int mask = Edit.Mask;

            foreach (EditLayerStatePropertyViewModel property in Properties.Where(p => !p.IsFixed))
            {
                mask = property.IsChecked ? mask | property.Flag : mask & ~property.Flag;
            }

            return mask;
        }
    }
}
