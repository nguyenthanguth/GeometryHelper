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
    /// The logic of the Layer manager: the layer table of a drawing with its on, frozen and locked flags, colour, linetype and
    /// weight, each change made through <see cref="CadLayerEditor"/> as one undo step and refused as AutoCAD refuses it (the
    /// current layer cannot be frozen), the refusal said on the command line. It reads the table again whenever the drawing's
    /// tables change, an undo among them.
    /// </summary>
    public sealed class LayerManagerViewModel : ObservableObject, IDisposable
    {
        /// <summary>The colour names of AutoCAD's indices 1 to 7, which the colour cell offers.</summary>
        public static readonly IReadOnlyList<string> ColorNames = new[] { "Red", "Yellow", "Green", "Cyan", "Blue", "Magenta", "White" };

        /// <summary>The weights the weight cell offers: Default and AutoCAD's widths in millimetres.</summary>
        public static readonly IReadOnlyList<string> WeightNames = new[]
        {
            "Default", "0.00", "0.05", "0.09", "0.13", "0.15", "0.18", "0.20", "0.25", "0.30", "0.35", "0.40", "0.50", "0.53", "0.60", "0.70",
            "0.80", "0.90", "1.00", "1.06", "1.20", "1.40", "1.58", "2.00", "2.11",
        };

        private readonly Action<string> _say;
        private readonly DelegateCommand _setCurrent;
        private readonly DelegateCommand _delete;
        private string _searchText = string.Empty;
        private LayerRowViewModel _selected;
        private string _summary = string.Empty;

        /// <summary>Makes the Layer manager of a document.</summary>
        /// <param name="document">The document.</param>
        /// <param name="say">Writes a line on the command line, as a refusal; null for none.</param>
        public LayerManagerViewModel(CadDocument document, Action<string> say = null)
        {
            Document = document ?? throw new ArgumentNullException(nameof(document));
            _say = say ?? (_ => { });
            NewLayerCommand = new DelegateCommand(_ => NewLayer());
            _setCurrent = new DelegateCommand(_ => SetCurrent(), _ => _selected != null && !_selected.IsCurrent);
            SetCurrentCommand = _setCurrent;
            _delete = new DelegateCommand(_ => Delete(), _ => _selected != null);
            DeleteCommand = _delete;
            Document.Changed += OnChanged;
            Refresh();
        }

        /// <summary>Gets the document.</summary>
        public CadDocument Document { get; }

        /// <summary>Gets the title of the dialog: "Layer manager · plan.dxf".</summary>
        public string Title => "Layer manager · " + Document.Title;

        /// <summary>Gets the rows of the layers whose name holds the search text, by name.</summary>
        public ObservableCollection<LayerRowViewModel> Rows { get; } = new ObservableCollection<LayerRowViewModel>();

        /// <summary>Gets or sets the text the rows are filtered by.</summary>
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (Set(ref _searchText, value ?? string.Empty))
                {
                    Refresh();
                }
            }
        }

        /// <summary>Gets or sets the row chosen, which Set current acts on.</summary>
        public LayerRowViewModel SelectedRow
        {
            get => _selected;
            set
            {
                if (Set(ref _selected, value))
                {
                    _setCurrent.RaiseCanExecuteChanged();
                    _delete.RaiseCanExecuteChanged();
                }
            }
        }

        /// <summary>Gets the linetypes of the drawing, which the linetype cell offers.</summary>
        public IReadOnlyList<string> LinetypeNames { get; private set; } = Array.Empty<string>();

        /// <summary>Gets the line under the table: "8 layers · 1 frozen · 1 locked".</summary>
        public string Summary
        {
            get => _summary;
            private set => Set(ref _summary, value);
        }

        /// <summary>Gets the command that adds a layer named Layer1, Layer2 and on, as AutoCAD's New layer.</summary>
        public ICommand NewLayerCommand { get; }

        /// <summary>Gets the command that makes the chosen layer current.</summary>
        public ICommand SetCurrentCommand { get; }

        /// <summary>Gets the command that deletes the chosen layer; one in use, the current layer, 0 and Defpoints are refused.</summary>
        public ICommand DeleteCommand { get; }

        /// <summary>
        /// Gets or sets what shows AutoCAD's Select Color dialog for a row's colour cell, as the window shows
        /// <see cref="SelectColorWindow"/>: given the dialog's logic, it says whether OK was pressed. Null for none, when the
        /// cell does nothing.
        /// </summary>
        public Func<SelectColorViewModel, bool> PickColor { get; set; }

        /// <summary>
        /// Gets or sets what shows AutoCAD's Select Linetype dialog for a row's linetype cell, as the window shows
        /// <see cref="SelectLinetypeWindow"/>: given the dialog's logic, it says whether OK was pressed. Null for none, when the
        /// cell does nothing.
        /// </summary>
        public Func<SelectLinetypeViewModel, bool> PickLinetype { get; set; }

        /// <summary>Adds a layer named Layer1, or the first LayerN the drawing has not, and chooses it.</summary>
        /// <returns>The name added, or null where it was refused.</returns>
        public string NewLayer()
        {
            int n = 1;

            while (Document.Database.LayerTable.Has("Layer" + n.ToString(CultureInfo.InvariantCulture)))
            {
                n++;
            }

            string name = "Layer" + n.ToString(CultureInfo.InvariantCulture);

            if (!Done(CadLayerEditor.Add(Document, name)))
            {
                return null;
            }

            SelectedRow = Rows.FirstOrDefault(r => string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase));
            return name;
        }

        /// <summary>Deletes the chosen layer, as AutoCAD deletes one nothing uses.</summary>
        /// <returns>Whether it was deleted; why not is said on the command line.</returns>
        public bool Delete() => _selected != null && Done(CadLayerEditor.Delete(Document, _selected.Name));

        /// <summary>Makes the chosen layer current.</summary>
        public void SetCurrent()
        {
            if (_selected != null)
            {
                Done(CadLayerEditor.SetCurrent(Document, _selected.Name));
            }
        }

        /// <summary>Stops following the document's changes, as the dialog does when it closes.</summary>
        public void Dispose() => Document.Changed -= OnChanged;

        /// <summary>Reads the layer table again.</summary>
        public void Refresh()
        {
            string chosen = _selected?.Name;
            string current = Document.Database.Clayer;
            var layers = Document.Layers.ToList();
            layers.Sort((a, b) =>
            {
                int order = string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
                return order != 0 ? order : string.CompareOrdinal(a.Name, b.Name);
            });
            LinetypeNames = Document.Database.LinetypeTable.Select(l => l.Name)
                .Where(n => !string.Equals(n, "ByLayer", StringComparison.OrdinalIgnoreCase) && !string.Equals(n, "ByBlock", StringComparison.OrdinalIgnoreCase))
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ThenBy(n => n, StringComparer.Ordinal).ToList();
            Rows.Clear();

            foreach (CadLayerInfo layer in layers)
            {
                if (_searchText.Length > 0 && layer.Name.IndexOf(_searchText, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                DxfLayerTableRecord record = Document.Database.LayerTable.Has(layer.Name) ? Document.Database.LayerTable[layer.Name] : null;
                int index = record == null ? 7 : Math.Abs(record.ColorIndex);
                DxfLineWeight weight = record == null ? DxfLineWeight.ByLineWeightDefault : record.LineWeight;
                DxfColor color = record == null ? DxfColor.FromColorIndex(DxfColorMethod.ByAci, 7) : record.Color;
                Rows.Add(new LayerRowViewModel(this, layer, index, color, weight, string.Equals(layer.Name, current, StringComparison.OrdinalIgnoreCase)));
            }

            _selected = chosen == null ? null : Rows.FirstOrDefault(r => string.Equals(r.Name, chosen, StringComparison.OrdinalIgnoreCase));
            Raise(nameof(SelectedRow));
            _setCurrent.RaiseCanExecuteChanged();
            _delete.RaiseCanExecuteChanged();
            int frozen = layers.Count(l => l.IsFrozen);
            int locked = layers.Count(l => l.IsLocked);
            Summary = MainViewModel.Count(layers.Count) + (layers.Count == 1 ? " layer" : " layers") + " · " + MainViewModel.Count(frozen) + " frozen · " + MainViewModel.Count(locked) + " locked";
        }

        /// <summary>The name of a colour index: Red to White for 1 to 7, else "Color 30".</summary>
        /// <param name="index">The index.</param>
        /// <returns>The name.</returns>
        public static string ColorName(int index) => index >= 1 && index <= 7 ? ColorNames[index - 1] : "Color " + index.ToString(CultureInfo.InvariantCulture);

        internal void Say(string line) => _say(line);

        /// <summary>Shows Select Color for a row and gives its layer the colour chosen, one undo step; nothing on Cancel.</summary>
        internal void ChooseColor(LayerRowViewModel row)
        {
            var dialog = new SelectColorViewModel(row.Color, false);

            if (PickColor != null && PickColor(dialog) && !dialog.Color.Equals(row.Color))
            {
                Done(CadLayerEditor.SetColor(Document, row.Name, dialog.Color));
            }
        }

        /// <summary>Shows Select Linetype for a row and gives its layer the linetype chosen, one undo step; nothing on Cancel. A linetype loaded by its Load... is its own undo step.</summary>
        internal void ChooseLinetype(LayerRowViewModel row)
        {
            var dialog = new SelectLinetypeViewModel(Document, row.Linetype);

            if (PickLinetype != null && PickLinetype(dialog) && !string.Equals(dialog.Linetype, row.Linetype, StringComparison.OrdinalIgnoreCase))
            {
                Done(CadLayerEditor.SetLinetype(Document, row.Name, dialog.Linetype));
            }
        }

        internal bool Done(CadEditResult result)
        {
            if (result.Succeeded)
            {
                return true;
            }

            _say(result.Message);
            return false;
        }

        private void OnChanged(object sender, CadDocumentChangedEventArgs e)
        {
            if (e.TablesChanged || e.Kind == CadChangeKind.Undo || e.Kind == CadChangeKind.Redo)
            {
                Refresh();
            }
        }
    }

    /// <summary>One layer of the Layer manager.</summary>
    public sealed class LayerRowViewModel : ObservableObject
    {
        private readonly LayerManagerViewModel _owner;
        private readonly CadLayerInfo _layer;
        private readonly int _colorIndex;
        private readonly DxfLineWeight _weight;

        internal LayerRowViewModel(LayerManagerViewModel owner, CadLayerInfo layer, int colorIndex, DxfColor color, DxfLineWeight weight, bool isCurrent)
        {
            _owner = owner;
            _layer = layer;
            _colorIndex = colorIndex;
            Color = color;
            _weight = weight;
            IsCurrent = isCurrent;
            ChooseColorCommand = new DelegateCommand(_ => _owner.ChooseColor(this));
            ChooseLinetypeCommand = new DelegateCommand(_ => _owner.ChooseLinetype(this));
        }

        /// <summary>Gets the name.</summary>
        public string Name => _layer.Name;

        /// <summary>Gets the command of the linetype cell: AutoCAD's Select Linetype dialog, through <see cref="LayerManagerViewModel.PickLinetype"/>.</summary>
        public ICommand ChooseLinetypeCommand { get; }

        /// <summary>Gets whether it is the current layer, marked with a tick.</summary>
        public bool IsCurrent { get; }

        /// <summary>Gets the tick of the current layer, empty for the others.</summary>
        public string CurrentMark => IsCurrent ? "✓" : string.Empty;

        /// <summary>Gets or sets whether the layer is on.</summary>
        public bool IsOn
        {
            get => _layer.IsOn;
            set => Change(value != _layer.IsOn && _owner.Done(CadLayerEditor.SetOn(_owner.Document, Name, value)), nameof(IsOn));
        }

        /// <summary>Gets or sets whether the layer is frozen; the current layer is refused.</summary>
        public bool IsFrozen
        {
            get => _layer.IsFrozen;
            set => Change(value != _layer.IsFrozen && _owner.Done(CadLayerEditor.SetFrozen(_owner.Document, Name, value)), nameof(IsFrozen));
        }

        /// <summary>Gets or sets whether the layer is locked.</summary>
        public bool IsLocked
        {
            get => _layer.IsLocked;
            set => Change(value != _layer.IsLocked && _owner.Done(CadLayerEditor.SetLocked(_owner.Document, Name, value)), nameof(IsLocked));
        }

        /// <summary>Gets the colour as 0xRRGGBB, the swatch.</summary>
        public int Rgb => _layer.Rgb;

        /// <summary>Gets the colour of the layer: one of the index, or a true colour.</summary>
        public DxfColor Color { get; }

        /// <summary>Gets the command that shows Select Color for the layer, the colour cell clicked, as in AutoCAD's Layer manager.</summary>
        public ICommand ChooseColorCommand { get; }

        /// <summary>Gets or sets the colour by name (Red to White) or by AutoCAD's index (1 to 255); a true colour reads as its red, green and blue, 10,20,30.</summary>
        public string ColorName
        {
            get => Color.IsByColor ? SelectColorViewModel.Text(Color) : LayerManagerViewModel.ColorName(_colorIndex);
            set
            {
                string typed = (value ?? string.Empty).Trim();
                int index = -1;

                for (int i = 0; i < LayerManagerViewModel.ColorNames.Count; i++)
                {
                    if (string.Equals(LayerManagerViewModel.ColorNames[i], typed, StringComparison.OrdinalIgnoreCase))
                    {
                        index = i + 1;
                    }
                }

                if (index < 0)
                {
                    string number = typed.StartsWith("Color ", StringComparison.OrdinalIgnoreCase) ? typed.Substring(6) : typed;

                    if (!int.TryParse(number, NumberStyles.Integer, CultureInfo.InvariantCulture, out index))
                    {
                        _owner.Done(CadEditResult.Refuse("Invalid colour \"" + typed + "\": give Red to White or 1 to 255."));
                        Raise(nameof(ColorName));
                        return;
                    }
                }

                Change(index != _colorIndex && _owner.Done(CadLayerEditor.SetColor(_owner.Document, Name, index)), nameof(ColorName));
            }
        }

        /// <summary>Gets the colours the colour cell offers: Red to White, and the layer's own where it is another.</summary>
        public IReadOnlyList<string> ColorOptions
        {
            get
            {
                var options = new List<string>(LayerManagerViewModel.ColorNames);

                if (!options.Contains(ColorName))
                {
                    options.Add(ColorName);
                }

                return options;
            }
        }

        /// <summary>Gets or sets the linetype's name: one of the drawing's linetypes.</summary>
        public string Linetype
        {
            get => _owner.LinetypeNames.FirstOrDefault(n => string.Equals(n, _layer.Linetype, StringComparison.OrdinalIgnoreCase)) ?? _layer.Linetype;
            set
            {
                string wanted = (value ?? string.Empty).Trim();
                bool same = string.Equals(wanted, _layer.Linetype, StringComparison.OrdinalIgnoreCase);
                Change(!same && _owner.Done(CadLayerEditor.SetLinetype(_owner.Document, Name, wanted)), nameof(Linetype));
            }
        }

        /// <summary>Gets the linetypes the linetype cell offers: the drawing's, and the layer's own where the drawing lists it not.</summary>
        public IReadOnlyList<string> LinetypeOptions
        {
            get
            {
                var options = new List<string>(_owner.LinetypeNames);

                if (!options.Contains(Linetype, StringComparer.OrdinalIgnoreCase))
                {
                    options.Add(Linetype);
                }

                return options;
            }
        }

        /// <summary>Gets or sets the line weight: Default, or one of AutoCAD's widths in millimetres as 0.35.</summary>
        public string Weight
        {
            get => _weight == DxfLineWeight.ByLineWeightDefault ? "Default" : ((int)_weight / 100.0).ToString("0.00", CultureInfo.InvariantCulture);
            set
            {
                string typed = (value ?? string.Empty).Trim();
                DxfLineWeight weight;

                if (string.Equals(typed, "Default", StringComparison.OrdinalIgnoreCase))
                {
                    weight = DxfLineWeight.ByLineWeightDefault;
                }
                else if (double.TryParse(typed.Replace("mm", string.Empty).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double mm)
                    && Enum.IsDefined(typeof(DxfLineWeight), (int)Math.Round(mm * 100.0)) && mm >= 0.0)
                {
                    weight = (DxfLineWeight)(int)Math.Round(mm * 100.0);
                }
                else
                {
                    _owner.Done(CadEditResult.Refuse("Invalid lineweight \"" + typed + "\": give Default or one of AutoCAD's widths, as 0.35."));
                    Raise(nameof(Weight));
                    return;
                }

                Change(weight != _weight && _owner.Done(CadLayerEditor.SetLineWeight(_owner.Document, Name, weight)), nameof(Weight));
            }
        }

        /// <summary>Gets the weights the weight cell offers.</summary>
        public IReadOnlyList<string> WeightOptions => LayerManagerViewModel.WeightNames;

        /// <summary>Gets the owner of the layer, a model drawing that wrote it; empty, as Part A writes none.</summary>
        public string Owner => string.Empty;

        /// <inheritdoc/>
        public override string ToString() => Name;

        /// <summary>After a change made, the manager has read the table again; after one refused, the cell shows the old value.</summary>
        private void Change(bool made, string property)
        {
            if (!made)
            {
                Raise(property);
            }
        }
    }
}
