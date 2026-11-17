using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows.Input;
using GeometryHelper.Dxf;

namespace GeometryHelper.Cad.Wpf
{
    /// <summary>
    /// One row of the filter definition of AutoCAD's Layer Filter Properties dialog: a cell for each property of a layer, empty where
    /// the filter asks nothing of it. The layers that pass are those that pass every cell of one row; a second row is another way
    /// to pass (an "or"). Each cell takes the wild cards of AutoCAD: <c>*</c>, <c>?</c>, <c>[AB]</c>, a leading <c>~</c>.
    /// </summary>
    public sealed class LayerFilterRuleRowViewModel : ObservableObject
    {
        private readonly LayerFilterEditViewModel _owner;
        private string _name = string.Empty;
        private string _on = string.Empty;
        private string _freeze = string.Empty;
        private string _lock = string.Empty;
        private string _color = string.Empty;
        private string _linetype = string.Empty;
        private string _lineweight = string.Empty;
        private string _transparency = string.Empty;
        private string _plotStyle = string.Empty;
        private string _plot = string.Empty;
        private string _newVpFreeze = string.Empty;
        private string _description = string.Empty;
        private string _vpFreeze = string.Empty;

        internal LayerFilterRuleRowViewModel(LayerFilterEditViewModel owner)
        {
            _owner = owner;
        }

        /// <summary>Gets or sets the pattern of the name: <c>1_Arch*</c>.</summary>
        public string Name
        {
            get => _name;
            set => Edit(ref _name, value);
        }

        /// <summary>Gets or sets On or Off (the lamp), or empty for either.</summary>
        public string On
        {
            get => _on;
            set => Edit(ref _on, value);
        }

        /// <summary>Gets or sets Freeze or Thaw, or empty for either.</summary>
        public string Freeze
        {
            get => _freeze;
            set => Edit(ref _freeze, value);
        }

        /// <summary>Gets or sets Lock or Unlock, or empty for either.</summary>
        public string Lock
        {
            get => _lock;
            set => Edit(ref _lock, value);
        }

        /// <summary>Gets or sets the colour: a name of Red to White, an index <c>30</c>, a true colour <c>10,20,30</c>, or a pattern as <c>1*</c>.</summary>
        public string Color
        {
            get => _color;
            set => Edit(ref _color, value);
        }

        /// <summary>Gets or sets the pattern of the linetype: <c>DASH*</c>.</summary>
        public string Linetype
        {
            get => _linetype;
            set => Edit(ref _linetype, value);
        }

        /// <summary>Gets or sets the lineweight: Default or a width as 0.35.</summary>
        public string Lineweight
        {
            get => _lineweight;
            set => Edit(ref _lineweight, value);
        }

        /// <summary>Gets or sets the transparency in per cent: <c>50</c>, or a pattern as <c>5*</c>.</summary>
        public string Transparency
        {
            get => _transparency;
            set => Edit(ref _transparency, value);
        }

        /// <summary>Gets or sets the pattern of the plot style: <c>Normal</c>.</summary>
        public string PlotStyle
        {
            get => _plotStyle;
            set => Edit(ref _plotStyle, value);
        }

        /// <summary>Gets or sets Plot or No plot, or empty for either.</summary>
        public string Plot
        {
            get => _plot;
            set => Edit(ref _plot, value);
        }

        /// <summary>Gets or sets Frozen or Thawed for the layers frozen in new viewports, or empty for either.</summary>
        public string NewVpFreeze
        {
            get => _newVpFreeze;
            set => Edit(ref _newVpFreeze, value);
        }

        /// <summary>Gets or sets the pattern of the description: <c>wall*</c>.</summary>
        public string Description
        {
            get => _description;
            set => Edit(ref _description, value);
        }

        /// <summary>Gets or sets Frozen or Thawed for the layers frozen in the current viewport, or empty for either.</summary>
        public string VpFreeze
        {
            get => _vpFreeze;
            set => Edit(ref _vpFreeze, value);
        }

        /// <summary>The rules of the row: a rule for each cell that is not empty.</summary>
        internal List<CadLayerRule> Rules()
        {
            var rules = new List<CadLayerRule>();
            Text(rules, "NAME", _name);
            Flag(rules, "OFF", _on, "Off", "On");
            Flag(rules, "FROZEN", _freeze, "Freeze", "Thaw");
            Flag(rules, "LOCKED", _lock, "Lock", "Unlock");
            Text(rules, "COLOR", ColorIndexOf(_color));
            Text(rules, "LINETYPE", _linetype);
            Text(rules, "LINEWEIGHT", _lineweight);
            Text(rules, "TRANSPARENCY", _transparency);
            Text(rules, "PLOTSTYLE", _plotStyle);
            Flag(rules, "PLOTTABLE", _plot, "Plot", "No plot");
            Flag(rules, "NEWVPFROZEN", _newVpFreeze, "Frozen", "Thawed");
            Text(rules, "DESCRIPTION", _description);
            Flag(rules, "VPFROZEN", _vpFreeze, "Frozen", "Thawed");
            return rules;
        }

        /// <summary>Sets the cell of a rule; false where the row has no cell for it or the cell is set already, or the rule is not one a cell can say.</summary>
        internal bool TryTake(CadLayerRule rule)
        {
            if (rule.Negated)
            {
                return false;
            }

            string value = rule.Pattern;
            bool isTrue = string.Equals(value, "True", StringComparison.OrdinalIgnoreCase);
            bool isFalse = string.Equals(value, "False", StringComparison.OrdinalIgnoreCase);

            switch (rule.Variable)
            {
                case "NAME":
                    return Take(ref _name, value);
                case "COLOR":
                    return Take(ref _color, value);
                case "LINETYPE":
                    return Take(ref _linetype, value);
                case "LINEWEIGHT":
                    return Take(ref _lineweight, value);
                case "TRANSPARENCY":
                    return Take(ref _transparency, value);
                case "PLOTSTYLE":
                    return Take(ref _plotStyle, value);
                case "DESCRIPTION":
                    return Take(ref _description, value);
                case "OFF":
                    return (isTrue || isFalse) && Take(ref _on, isTrue ? "Off" : "On");
                case "FROZEN":
                    return (isTrue || isFalse) && Take(ref _freeze, isTrue ? "Freeze" : "Thaw");
                case "LOCKED":
                    return (isTrue || isFalse) && Take(ref _lock, isTrue ? "Lock" : "Unlock");
                case "PLOTTABLE":
                    return (isTrue || isFalse) && Take(ref _plot, isTrue ? "Plot" : "No plot");
                case "NEWVPFROZEN":
                    return (isTrue || isFalse) && Take(ref _newVpFreeze, isTrue ? "Frozen" : "Thawed");
                case "VPFROZEN":
                    return (isTrue || isFalse) && Take(ref _vpFreeze, isTrue ? "Frozen" : "Thawed");
                default:
                    return false;
            }
        }

        private static bool Take(ref string cell, string value)
        {
            if (cell.Length > 0)
            {
                return false;
            }

            cell = value;
            return true;
        }

        private static void Text(List<CadLayerRule> rules, string variable, string cell)
        {
            if ((cell ?? string.Empty).Trim().Length > 0)
            {
                rules.Add(new CadLayerRule(variable, cell.Trim()));
            }
        }

        private static void Flag(
            List<CadLayerRule> rules,
            string variable,
            string cell,
            string whenTrue,
            string whenFalse
        )
        {
            if (string.Equals(cell, whenTrue, StringComparison.OrdinalIgnoreCase))
            {
                rules.Add(new CadLayerRule(variable, "True"));
            }
            else if (string.Equals(cell, whenFalse, StringComparison.OrdinalIgnoreCase))
            {
                rules.Add(new CadLayerRule(variable, "False"));
            }
        }

        /// <summary>A colour typed as Red to White is its index; any other text is the pattern as it is.</summary>
        private static string ColorIndexOf(string typed)
        {
            string text = (typed ?? string.Empty).Trim();

            for (int i = 0; i < LayerManagerViewModel.ColorNames.Count; i++)
            {
                if (string.Equals(LayerManagerViewModel.ColorNames[i], text, StringComparison.OrdinalIgnoreCase))
                {
                    return (i + 1).ToString(CultureInfo.InvariantCulture);
                }
            }

            return text;
        }

        private void Edit(ref string cell, string value)
        {
            string text = value ?? string.Empty;

            if (cell == text)
            {
                return;
            }

            cell = text;
            Raise(null);
            _owner.Changed();
        }
    }

    /// <summary>A layer in the preview of the Layer Filter Properties dialog: the layers the filter being made lets through.</summary>
    public sealed class LayerFilterPreviewRowViewModel
    {
        internal LayerFilterPreviewRowViewModel(DxfLayerTableRecord layer)
        {
            Name = layer.Name;
            On = layer.IsOff ? "Off" : "On";
            Freeze = layer.IsFrozen ? "Frozen" : "Thawed";
            Lock = layer.IsLocked ? "Locked" : "Unlocked";
            Color = layer.TrueColor.HasValue
                ? ((layer.TrueColor.Value >> 16) & 255).ToString(CultureInfo.InvariantCulture)
                    + ","
                    + ((layer.TrueColor.Value >> 8) & 255).ToString(CultureInfo.InvariantCulture)
                    + ","
                    + (layer.TrueColor.Value & 255).ToString(CultureInfo.InvariantCulture)
                : LayerManagerViewModel.ColorName(Math.Abs(layer.ColorIndex));
            Rgb = layer.Color == null ? 0xFFFFFF : layer.Color.ColorValue & 0xFFFFFF;
            Linetype = layer.LinetypeName;
            Plot = layer.IsPlottable ? "Plot" : "No plot";
            Description = layer.Description;
        }

        /// <summary>Gets the name.</summary>
        public string Name { get; }

        /// <summary>Gets On or Off.</summary>
        public string On { get; }

        /// <summary>Gets Frozen or Thawed.</summary>
        public string Freeze { get; }

        /// <summary>Gets Locked or Unlocked.</summary>
        public string Lock { get; }

        /// <summary>Gets the colour's name, or red, green and blue for a true colour.</summary>
        public string Color { get; }

        /// <summary>Gets the colour as 0xRRGGBB, the swatch.</summary>
        public int Rgb { get; }

        /// <summary>Gets the linetype.</summary>
        public string Linetype { get; }

        /// <summary>Gets Plot or No plot.</summary>
        public string Plot { get; }

        /// <summary>Gets the description.</summary>
        public string Description { get; }
    }

    /// <summary>
    /// The logic of AutoCAD 2021's Layer Filter Properties dialog: the name of the filter, its definition as rows of cells (a cell for
    /// each property of a layer, the rows "or"), and a preview of the layers it lets through, kept up to date as the cells are
    /// edited. OK adds the filter, or changes the one the dialog was opened over, in one undo step.
    /// </summary>
    public sealed class LayerFilterEditViewModel : ObservableObject
    {
        /// <summary>The choices of the On cell.</summary>
        public static readonly IReadOnlyList<string> OnChoices = new[] { string.Empty, "On", "Off" };

        /// <summary>The choices of the Freeze cell.</summary>
        public static readonly IReadOnlyList<string> FreezeChoices = new[] { string.Empty, "Freeze", "Thaw" };

        /// <summary>The choices of the Lock cell.</summary>
        public static readonly IReadOnlyList<string> LockChoices = new[] { string.Empty, "Lock", "Unlock" };

        /// <summary>The choices of the Plot cell.</summary>
        public static readonly IReadOnlyList<string> PlotChoices = new[] { string.Empty, "Plot", "No plot" };

        /// <summary>The choices of the New VP Freeze and VP Freeze cells.</summary>
        public static readonly IReadOnlyList<string> VpChoices = new[] { string.Empty, "Frozen", "Thawed" };

        private readonly CadDocument _document;
        private readonly IReadOnlyList<string> _parent;
        private readonly CadLayerFilter _existing;
        private readonly DxfViewport _viewport;
        private string _name;
        private bool _isRaw;
        private string _expression = string.Empty;
        private string _message = string.Empty;

        /// <summary>Makes the dialog over a new filter or an existing one.</summary>
        /// <param name="document">The document.</param>
        /// <param name="parent">The names of the filter it is nested in; none for the top.</param>
        /// <param name="existing">The property filter to change; null for a new one.</param>
        /// <param name="viewport">The viewport VP Freeze is read in; null for none.</param>
        public LayerFilterEditViewModel(
            CadDocument document,
            IReadOnlyList<string> parent,
            CadLayerFilter existing,
            DxfViewport viewport
        )
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
            _parent = parent ?? Array.Empty<string>();
            _existing = existing;
            _viewport = viewport;
            AddRowCommand = new DelegateCommand(_ => AddRow());
            RemoveRowCommand = new DelegateCommand(p => RemoveRow(p as LayerFilterRuleRowViewModel));

            if (existing == null)
            {
                IEnumerable<string> siblings = (
                    _parent.Count == 0
                        ? CadLayerFilters.Read(document.Database)
                        : CadLayerFilters.Find(CadLayerFilters.Read(document.Database), _parent)?.Children
                            ?? new List<CadLayerFilter>()
                ).Select(f => f.Name);
                var taken = new HashSet<string>(siblings, StringComparer.OrdinalIgnoreCase);
                int n = 1;

                while (taken.Contains("Properties Filter" + n.ToString(CultureInfo.InvariantCulture)))
                {
                    n++;
                }

                _name = "Properties Filter" + n.ToString(CultureInfo.InvariantCulture);
                AddRow();
            }
            else
            {
                _name = existing.Name;
                Read(existing.Expression);
            }

            Update();
        }

        /// <summary>Gets the title of the dialog.</summary>
        public string Title => "Layer Filter Properties";

        /// <summary>Gets or sets the name of the filter.</summary>
        public string Name
        {
            get => _name;
            set
            {
                if (Set(ref _name, value ?? string.Empty))
                {
                    Update();
                }
            }
        }

        /// <summary>Gets the rows of the definition.</summary>
        public ObservableCollection<LayerFilterRuleRowViewModel> Rows { get; } =
            new ObservableCollection<LayerFilterRuleRowViewModel>();

        /// <summary>Gets the layers the filter, with the filters it is nested in, lets through now.</summary>
        public ObservableCollection<LayerFilterPreviewRowViewModel> Preview { get; } =
            new ObservableCollection<LayerFilterPreviewRowViewModel>();

        /// <summary>Gets whether the definition the filter has cannot be shown as rows (a rule twice in a row, a "not equal"), so that it is kept as it is and edited as text.</summary>
        public bool IsRaw
        {
            get => _isRaw;
            private set
            {
                if (Set(ref _isRaw, value))
                {
                    Raise(nameof(ExpressionIsReadOnly));
                }
            }
        }

        /// <summary>Gets whether the definition as text is shown and not typed: the rows make it, unless <see cref="IsRaw"/>.</summary>
        public bool ExpressionIsReadOnly => !_isRaw;

        /// <summary>
        /// Gets or sets the definition as AutoCAD keeps it, <c>NAME=="1_Arch*" AND OFF=="False"</c>: made from the rows, or typed where
        /// the rows cannot show it.
        /// </summary>
        public string Expression
        {
            get => _expression;
            set
            {
                if (_isRaw && Set(ref _expression, value ?? string.Empty))
                {
                    Update();
                }
            }
        }

        /// <summary>Gets what is wrong with the filter: its name or its definition; empty where OK can be pressed.</summary>
        public string Message
        {
            get => _message;
            private set => Set(ref _message, value);
        }

        /// <summary>Gets whether OK can be pressed: the name is free and the definition has a rule that reads.</summary>
        public bool CanOk => _message.Length == 0;

        /// <summary>Gets the command of Add row: another way for a layer to pass.</summary>
        public ICommand AddRowCommand { get; }

        /// <summary>Gets the command of Remove row, its parameter the row.</summary>
        public ICommand RemoveRowCommand { get; }

        /// <summary>Adds an empty row to the definition.</summary>
        public void AddRow() => Rows.Add(new LayerFilterRuleRowViewModel(this));

        /// <summary>Takes a row out of the definition; the last row is kept, emptied.</summary>
        /// <param name="row">The row.</param>
        public void RemoveRow(LayerFilterRuleRowViewModel row)
        {
            if (row == null || !Rows.Contains(row))
            {
                return;
            }

            if (Rows.Count == 1)
            {
                row.Name = row.On = row.Freeze = row.Lock = row.Color = row.Linetype = string.Empty;
                row.Lineweight = row.Transparency = row.PlotStyle = row.Plot = row.NewVpFreeze = string.Empty;
                row.Description = row.VpFreeze = string.Empty;
            }
            else
            {
                Rows.Remove(row);
            }

            Update();
        }

        /// <summary>
        /// Makes the filter: added, or the one the dialog was opened over given its new definition and name, one undo step.
        /// </summary>
        /// <returns>What came of it; the refusal says why not.</returns>
        public CadEditResult Apply()
        {
            if (!CanOk)
            {
                return CadEditResult.Refuse(_message);
            }

            string name = _name.Trim();

            if (_existing == null)
            {
                return CadLayerFilters.AddProperty(_document, _parent, name, _expression);
            }

            _document.BeginGroup("LAYER");

            try
            {
                CadEditResult changed = CadLayerFilters.SetExpression(_document, _existing.Path, _expression);

                if (changed.Succeeded && !string.Equals(_existing.Name, name, StringComparison.Ordinal))
                {
                    changed = CadLayerFilters.Rename(_document, _existing.Path, name);
                }

                return changed;
            }
            finally
            {
                _document.EndGroup();
            }
        }

        /// <summary>Called by a row when a cell is edited.</summary>
        internal void Changed() => Update();

        /// <summary>Reads a definition into rows; where a row cannot show it, the text is kept as it is.</summary>
        private void Read(string text)
        {
            Rows.Clear();
            _expression = text ?? string.Empty;
            IsRaw = false;

            if (!CadLayerExpression.TryParse(_expression, out CadLayerExpression parsed))
            {
                IsRaw = true;
                AddRow();
                return;
            }

            foreach (IReadOnlyList<CadLayerRule> alternative in parsed.Alternatives)
            {
                var row = new LayerFilterRuleRowViewModel(this);

                foreach (CadLayerRule rule in alternative)
                {
                    if (!row.TryTake(rule))
                    {
                        IsRaw = true;
                    }
                }

                Rows.Add(row);
            }

            if (Rows.Count == 0)
            {
                AddRow();
            }

            if (IsRaw)
            {
                // The rows show what they can; the text is the definition.
                _expression = text;
            }
        }

        /// <summary>The definition made from the rows, and the preview and the message read again.</summary>
        private void Update()
        {
            if (!_isRaw)
            {
                var alternatives = new List<IReadOnlyList<CadLayerRule>>();

                foreach (LayerFilterRuleRowViewModel row in Rows)
                {
                    List<CadLayerRule> rules = row.Rules();

                    if (rules.Count > 0)
                    {
                        alternatives.Add(rules.AsReadOnly());
                    }
                }

                _expression = new CadLayerExpression(alternatives).ToString();
                Raise(nameof(Expression));
            }

            string name = (_name ?? string.Empty).Trim();
            bool parses = CadLayerExpression.TryParse(_expression, out CadLayerExpression expression);
            IEnumerable<CadLayerFilter> siblings =
                _parent.Count == 0
                    ? CadLayerFilters.Read(_document.Database)
                    : CadLayerFilters.Find(CadLayerFilters.Read(_document.Database), _parent)?.Children
                        ?? new List<CadLayerFilter>();

            if (name.Length == 0)
            {
                Message = "A layer filter needs a name.";
            }
            else if (
                siblings.Any(f =>
                    string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase)
                    && (_existing == null || !string.Equals(f.Name, _existing.Name, StringComparison.OrdinalIgnoreCase))
                )
                || (
                    _parent.Count == 0
                    && CadLayerFilters.Dynamic.Any(d => string.Equals(d.Key, name, StringComparison.OrdinalIgnoreCase))
                )
            )
            {
                Message = "A layer filter named \"" + name + "\" already exists.";
            }
            else if (!parses)
            {
                Message = "The filter definition is not valid.";
            }
            else if (_expression.Trim().Length == 0)
            {
                Message = "The filter definition is empty: give at least one property a value.";
            }
            else
            {
                Message = string.Empty;
            }

            Raise(nameof(CanOk));
            Preview.Clear();

            if (!parses)
            {
                return;
            }

            // The layers the filter lets through, with the filters it is nested in.
            var context = new CadLayerFilterContext(_document.Database, _viewport);
            CadLayerFilter parent =
                _parent.Count == 0 ? null : CadLayerFilters.Find(CadLayerFilters.Read(_document.Database), _parent);

            foreach (DxfLayerTableRecord layer in _document.Database.LayerTable)
            {
                if (
                    expression.Matches(layer, context)
                    && (parent == null || CadLayerFilters.Passes(parent, layer, context))
                )
                {
                    Preview.Add(new LayerFilterPreviewRowViewModel(layer));
                }
            }
        }
    }
}
