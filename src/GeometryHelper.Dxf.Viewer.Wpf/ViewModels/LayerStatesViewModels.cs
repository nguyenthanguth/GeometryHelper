using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>A row of the Layer States Manager: the state's name, its space, Same as DWG and its description.</summary>
    public sealed class LayerStateRowViewModel
    {
        internal LayerStateRowViewModel(CadLayerStateRow row)
        {
            Row = row;
        }

        /// <summary>Gets the state of the request.</summary>
        public CadLayerStateRow Row { get; }

        /// <summary>Gets the name.</summary>
        public string Name => Row.Name;

        /// <summary>Gets Model, or Layout for a state saved in a layout.</summary>
        public string Space => Row.IsLayout ? "Layout" : "Model";

        /// <summary>Gets Yes where every layer is as the state has it, else No.</summary>
        public string Same => Row.SameAsDrawing ? "Yes" : "No";

        /// <summary>Gets the description.</summary>
        public string Description => Row.Description;
    }

    /// <summary>A property of Layer properties to restore: its name, its flag of the mask, and whether it is ticked.</summary>
    public sealed class LayerStatePropertyViewModel : ObservableObject
    {
        private readonly LayerStatesViewModel _owner;
        private bool _isChecked;

        internal LayerStatePropertyViewModel(LayerStatesViewModel owner, string name, int flag, bool fixedOn)
        {
            _owner = owner;
            Name = name;
            Flag = flag;
            IsFixed = fixedOn;
        }

        /// <summary>Gets the name, as On/Off.</summary>
        public string Name { get; }

        /// <summary>Gets the flag of the mask.</summary>
        public int Flag { get; }

        /// <summary>Gets whether the property is greyed: one of a viewport or a plot style, which a drawing here does not restore.</summary>
        public bool IsFixed { get; }

        /// <summary>Gets whether the box can be ticked: a state is chosen and the property is not greyed.</summary>
        public bool IsEnabled => !IsFixed && _owner.HasSelected;

        /// <summary>Gets or sets whether the state chosen restores the property; set, the state's mask is written.</summary>
        public bool IsChecked
        {
            get => _isChecked;
            set
            {
                if (Set(ref _isChecked, value))
                {
                    _owner.WriteMask();
                }
            }
        }

        /// <summary>The tick shown again from a mask, the state's left as it is.</summary>
        internal void Show(int mask)
        {
            _isChecked = (mask & Flag) != 0;
            Raise(nameof(IsChecked));
            Raise(nameof(IsEnabled));
        }
    }

    /// <summary>
    /// AutoCAD's Layer States Manager, which LAYERSTATE shows, over the request its command hands the window: the states,
    /// Don't list layer states in Xrefs, the restore options, New, Update, Edit, Rename, Delete, Import, Export and the
    /// properties the state chosen restores, folded away with &lt;&lt;. Restore restores the state chosen and closes it;
    /// Close keeps every change, the whole one undo step.
    /// </summary>
    public sealed class LayerStatesViewModel : ObservableObject
    {
        private readonly ObservableCollection<LayerStateRowViewModel> _states =
            new ObservableCollection<LayerStateRowViewModel>();
        private LayerStateRowViewModel _selected;
        private bool _showProperties = true;
        private bool _showing;

        /// <summary>Makes the dialog over a request.</summary>
        /// <param name="request">The request of LAYERSTATE.</param>
        /// <exception cref="ArgumentNullException">Thrown when the request is null.</exception>
        public LayerStatesViewModel(CadLayerStatesRequest request)
        {
            Request = request ?? throw new ArgumentNullException(nameof(request));
            Properties = new ReadOnlyCollection<LayerStatePropertyViewModel>(
                new[]
                {
                    new LayerStatePropertyViewModel(this, "On/Off", 0x1, false),
                    new LayerStatePropertyViewModel(this, "Frozen/Thawed", 0x2, false),
                    new LayerStatePropertyViewModel(this, "Visibility in current viewport", 0x200, true),
                    new LayerStatePropertyViewModel(this, "Locked/Unlocked", 0x4, false),
                    new LayerStatePropertyViewModel(this, "Plot/No Plot", 0x8, false),
                    new LayerStatePropertyViewModel(this, "Color", 0x20, false),
                    new LayerStatePropertyViewModel(this, "Linetype", 0x40, false),
                    new LayerStatePropertyViewModel(this, "Lineweight", 0x80, false),
                    new LayerStatePropertyViewModel(this, "Transparency", 0x400, false),
                    new LayerStatePropertyViewModel(this, "Plot style", 0x100, true),
                    new LayerStatePropertyViewModel(this, "New VP freeze", 0x10, true),
                }
            );
            Refresh();
        }

        /// <summary>Gets the request.</summary>
        public CadLayerStatesRequest Request { get; }

        /// <summary>Gets or sets how the window asks a name, from its title and the name offered; null for Cancel.</summary>
        public Func<string, string, string> AskName { get; set; }

        /// <summary>Gets or sets how the window asks the .las file to import; null for Cancel.</summary>
        public Func<string> AskImportFile { get; set; }

        /// <summary>Gets or sets how the window asks the .las file to export to, from the name offered; null for Cancel.</summary>
        public Func<string, string> AskExportFile { get; set; }

        /// <summary>Gets the states listed.</summary>
        public ObservableCollection<LayerStateRowViewModel> States => _states;

        /// <summary>Gets the properties the state chosen restores, in the order of AutoCAD's panel.</summary>
        public ReadOnlyCollection<LayerStatePropertyViewModel> Properties { get; }

        /// <summary>Gets or sets the state chosen.</summary>
        public LayerStateRowViewModel Selected
        {
            get => _selected;
            set
            {
                if (Set(ref _selected, value))
                {
                    Request.Selected = value?.Name;
                    ShowMask();
                    Raise(nameof(HasSelected));
                }
            }
        }

        /// <summary>Gets whether a state is chosen, which Update, Edit, Rename, Delete, Export and Restore need.</summary>
        public bool HasSelected => _selected != null;

        /// <summary>Gets the line under the list: the state the drawing is as, or *UNSAVED*.</summary>
        public string Current => Request.Current;

        /// <summary>Gets or sets whether the states of xrefs are left out of the list.</summary>
        public bool HideXrefStates
        {
            get => Request.HideXrefStates;
            set
            {
                Request.HideXrefStates = value;
                Raise(nameof(HideXrefStates));
                Request.Relist();
                Refresh();
            }
        }

        /// <summary>Gets or sets whether Restore turns off the layers the state does not name.</summary>
        public bool TurnOffMissing
        {
            get => Request.TurnOffMissing;
            set
            {
                Request.TurnOffMissing = value;
                Raise(nameof(TurnOffMissing));
            }
        }

        /// <summary>Gets or sets whether Restore puts the properties on the current viewport as overrides.</summary>
        public bool AsViewportOverrides
        {
            get => Request.AsViewportOverrides;
            set
            {
                Request.AsViewportOverrides = value;
                Raise(nameof(AsViewportOverrides));
            }
        }

        /// <summary>Gets whether Apply properties as viewport overrides can be ticked: in a layout only.</summary>
        public bool CanOverride => !Request.IsModelSpace;

        /// <summary>Gets or sets whether the Layer properties panel is shown (&gt;&gt;) or folded away (&lt;&lt;).</summary>
        public bool ShowProperties
        {
            get => _showProperties;
            set => Set(ref _showProperties, value);
        }

        /// <summary>Gets what the last change said where it was refused; empty where none was.</summary>
        public string Message => Request.Message ?? string.Empty;

        /// <summary>
        /// Gets or sets how the window shows New Layer State to Save, its name and description: true for OK. With none set,
        /// New… asks the name alone through <see cref="AskName"/>.
        /// </summary>
        public Func<NewLayerStateViewModel, bool> AskNewState { get; set; }

        /// <summary>
        /// Gets or sets how the window asks whether to replace a state of the name New was given: true for Yes. With none set,
        /// New refuses the name, "A layer state named Plan already exists.".
        /// </summary>
        public Func<string, bool> ConfirmReplace { get; set; }

        /// <summary>Gets or sets how the window shows Edit Layer State over the state chosen: true for OK, which writes the state.</summary>
        public Func<EditLayerStateViewModel, bool> ShowEditState { get; set; }

        /// <summary>New…: the layers as they stand saved as a state of a name asked.</summary>
        /// <returns>Whether it was saved.</returns>
        public bool New()
        {
            if (AskNewState != null)
            {
                var dialog = new NewLayerStateViewModel(
                    Request.States.Select(s => s.Name),
                    "Layer state "
                        + (Request.States.Count + 1).ToString(System.Globalization.CultureInfo.InvariantCulture)
                );

                if (!AskNewState(dialog))
                {
                    return false;
                }

                string chosen = dialog.Name.Trim();

                if (Request.Has(chosen) && ConfirmReplace != null)
                {
                    return ConfirmReplace(chosen) && Changed(Request.Replace(chosen, dialog.Description));
                }

                return Changed(Request.New(chosen, dialog.Description));
            }

            string name = AskName?.Invoke(
                "New Layer State to Save",
                "Layer state " + (Request.States.Count + 1).ToString(System.Globalization.CultureInfo.InvariantCulture)
            );
            return name != null && Changed(Request.New(name, string.Empty));
        }

        /// <summary>Update: the state chosen saved again from the layers as they stand.</summary>
        /// <returns>Whether it was.</returns>
        public bool Update() => HasSelected && Changed(Request.Update());

        /// <summary>
        /// Edit…: AutoCAD's Edit Layer State shown over the state chosen, its layers and the properties it restores written on
        /// OK. With no window to show it (<see cref="ShowEditState"/> unset), the properties the state chosen restores written
        /// from the panel, as -LAYER State Edit does.
        /// </summary>
        /// <returns>Whether the state was written.</returns>
        public bool Edit()
        {
            if (!HasSelected)
            {
                return false;
            }

            if (ShowEditState == null)
            {
                ShowProperties = true;
                return WriteMask();
            }

            CadLayerStateEdit edit = Request.BeginEdit();

            if (edit == null)
            {
                return false;
            }

            var dialog = new EditLayerStateViewModel(edit, Request.Document);
            return ShowEditState(dialog) && Changed(Request.Edit(dialog.Commit()));
        }

        /// <summary>Rename: the state chosen given a name asked.</summary>
        /// <returns>Whether it was renamed.</returns>
        public bool Rename()
        {
            string name = HasSelected ? AskName?.Invoke("Rename Layer State", _selected.Name) : null;
            return name != null && Changed(Request.Rename(name));
        }

        /// <summary>Delete: the state chosen taken out.</summary>
        /// <returns>Whether it was.</returns>
        public bool Delete() => HasSelected && Changed(Request.Delete());

        /// <summary>Import…: the states of a .las file asked read into the drawing.</summary>
        /// <returns>Whether they were.</returns>
        public bool Import()
        {
            string path = AskImportFile?.Invoke();
            return path != null && Changed(Request.Import(path));
        }

        /// <summary>Export…: the state chosen written to a .las file asked.</summary>
        /// <returns>Whether it was written.</returns>
        public bool Export()
        {
            string path = HasSelected ? AskExportFile?.Invoke(_selected.Name) : null;
            return path != null && Changed(Request.Export(path));
        }

        /// <summary>Select All or Clear All: every property that can be ticked set at once.</summary>
        /// <param name="on">Whether they are ticked.</param>
        public void SetAll(bool on)
        {
            _showing = true;

            foreach (LayerStatePropertyViewModel property in Properties.Where(p => p.IsEnabled))
            {
                property.IsChecked = on;
            }

            _showing = false;
            WriteMask();
        }

        /// <summary>Restore: the dialog closed for the command to restore the state chosen.</summary>
        public void Restore() => Request.Action = HasSelected ? CadDialogAction.Ok : CadDialogAction.Cancel;

        /// <summary>Close: the dialog closed, every change kept.</summary>
        public void Close() => Request.Action = CadDialogAction.Cancel;

        /// <summary>The mask of the state chosen written from the ticks, the greyed properties kept as the state has them.</summary>
        internal bool WriteMask()
        {
            if (_showing || _selected == null)
            {
                return false;
            }

            int mask = _selected.Row.Mask;

            foreach (LayerStatePropertyViewModel property in Properties.Where(p => !p.IsFixed))
            {
                mask = property.IsChecked ? mask | property.Flag : mask & ~property.Flag;
            }

            return Changed(Request.SetMask(mask));
        }

        private bool Changed(bool done)
        {
            Refresh();
            Raise(nameof(Message));
            return done;
        }

        private void ShowMask()
        {
            _showing = true;

            foreach (LayerStatePropertyViewModel property in Properties)
            {
                property.Show(_selected?.Row.Mask ?? 0);
            }

            _showing = false;
        }

        /// <summary>The list made again from the request, the state of <see cref="CadLayerStatesRequest.Selected"/> chosen.</summary>
        private void Refresh()
        {
            string chosen = Request.Selected;
            _states.Clear();

            foreach (CadLayerStateRow row in Request.States)
            {
                _states.Add(new LayerStateRowViewModel(row));
            }

            _selected = _states.FirstOrDefault(s => string.Equals(s.Name, chosen, StringComparison.OrdinalIgnoreCase));
            Request.Selected = _selected?.Name;
            ShowMask();
            Raise(nameof(Selected));
            Raise(nameof(HasSelected));
            Raise(nameof(Current));
            Raise(nameof(Message));
        }
    }
}
