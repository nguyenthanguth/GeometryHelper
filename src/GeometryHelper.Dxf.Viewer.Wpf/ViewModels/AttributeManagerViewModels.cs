using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>A column of the Block Attribute Manager's grid: its header and its width in pixels.</summary>
    public sealed class AttributeColumnViewModel
    {
        internal AttributeColumnViewModel(string name, double width)
        {
            Name = name;
            Width = width;
        }

        /// <summary>Gets the header, as Prompt.</summary>
        public string Name { get; }

        /// <summary>Gets the width.</summary>
        public double Width { get; }
    }

    /// <summary>A cell of a row of the manager's grid: its text, its width and whether it is in the mono font.</summary>
    public sealed class AttributeCellViewModel
    {
        internal AttributeCellViewModel(string text, double width, bool mono)
        {
            Text = text;
            Width = width;
            IsMono = mono;
        }

        /// <summary>Gets the text.</summary>
        public string Text { get; }

        /// <summary>Gets the width, that of its column.</summary>
        public double Width { get; }

        /// <summary>Gets whether it is written in the mono font, as tags, defaults, modes and numbers are.</summary>
        public bool IsMono { get; }
    }

    /// <summary>A row of the manager's grid: an attribute definition of the block, its cells in the columns shown, and whether its tag is given twice.</summary>
    public sealed class AttributeManagerRowViewModel : ObservableObject
    {
        internal AttributeManagerRowViewModel(
            CadAttributeDefinitionRow row,
            IReadOnlyList<AttributeCellViewModel> cells,
            bool duplicate
        )
        {
            Row = row;
            Cells = cells;
            IsDuplicate = duplicate;
        }

        /// <summary>Gets the row of the request.</summary>
        public CadAttributeDefinitionRow Row { get; }

        /// <summary>Gets the tag.</summary>
        public string Tag => Row.Tag;

        /// <summary>Gets the cells, in the order of the columns shown.</summary>
        public IReadOnlyList<AttributeCellViewModel> Cells { get; }

        /// <summary>Gets whether another row has its tag and the settings ask for it to be drawn in red.</summary>
        public bool IsDuplicate { get; }
    }

    /// <summary>
    /// AutoCAD's Block Attribute Manager, which BATTMAN shows, over the request its command hands the window: Select block and
    /// the list of the blocks with attributes, how many INSERTs of the one shown the drawing holds, the grid of its attribute
    /// definitions, and Sync, Move Up, Move Down, Edit and Remove; Settings, Apply, Cancel and OK. A block GRID_BUBBLE of four
    /// attributes opens on its first, so Move Up is greyed; Remove is greyed when one attribute is left.
    /// </summary>
    public sealed class BlockAttributeManagerViewModel : ObservableObject
    {
        private readonly ObservableCollection<AttributeManagerRowViewModel> _rows =
            new ObservableCollection<AttributeManagerRowViewModel>();
        private AttributeManagerRowViewModel _selected;
        private IReadOnlyList<AttributeColumnViewModel> _columns = Array.Empty<AttributeColumnViewModel>();

        /// <summary>Makes the manager over a request.</summary>
        /// <param name="request">The request of BATTMAN.</param>
        /// <exception cref="ArgumentNullException">Thrown when the request is null.</exception>
        public BlockAttributeManagerViewModel(CadAttributeManagerRequest request)
        {
            Request = request ?? throw new ArgumentNullException(nameof(request));
            Rebuild(0);
        }

        /// <summary>Gets the request.</summary>
        public CadAttributeManagerRequest Request { get; }

        /// <summary>Gets or sets how the window shows Edit Attribute; true where it was closed with OK.</summary>
        public Func<EditAttributeViewModel, bool> ShowEditor { get; set; }

        /// <summary>Gets or sets how the window shows Block Attribute Settings; true where it was closed with OK.</summary>
        public Func<AttributeManagerSettingsViewModel, bool> ShowSettings { get; set; }

        /// <summary>Gets the blocks with attributes, which the Block list offers.</summary>
        public IReadOnlyList<string> Blocks => Request.Blocks;

        /// <summary>
        /// Gets or sets how the window asks whether to save the changes made to a block before another is shown, as AutoCAD's
        /// manager asks: true writes them, false drops them. With none set, they are written without asking.
        /// </summary>
        public Func<string, bool> AskSaveChanges { get; set; }

        /// <summary>
        /// Gets or sets the block shown: another one chosen asks first whether to save what was changed in this one, where
        /// anything was, then shows it.
        /// </summary>
        public string Block
        {
            get => Request.Block;
            set
            {
                if (value != null && !string.Equals(value, Request.Block, StringComparison.Ordinal))
                {
                    AskBeforeLeaving();
                    Request.Show(value);
                    Rebuild(0);
                    Raise(nameof(Block));
                }
            }
        }

        /// <summary>Gets "Found in drawing: 14" for the block shown.</summary>
        public string FoundInDrawing => Request.FoundInDrawing.ToString(CultureInfo.InvariantCulture);

        /// <summary>Gets "Found in model space: 12" for the block shown.</summary>
        public string FoundInModelSpace => Request.FoundInModelSpace.ToString(CultureInfo.InvariantCulture);

        /// <summary>Gets the columns shown, as the settings say.</summary>
        public IReadOnlyList<AttributeColumnViewModel> Columns => _columns;

        /// <summary>Gets the rows, one for each attribute definition of the block, in its order.</summary>
        public ObservableCollection<AttributeManagerRowViewModel> Rows => _rows;

        /// <summary>Gets or sets the row chosen, which Move Up, Move Down, Edit and Remove act on.</summary>
        public AttributeManagerRowViewModel Selected
        {
            get => _selected;
            set
            {
                if (Set(ref _selected, value))
                {
                    RaiseButtons();
                }
            }
        }

        /// <summary>Gets whether Move Up can run: a row is chosen, it is not the first, and it is not of a constant attribute.</summary>
        public bool CanMoveUp => Index > 0 && !_selected.Row.Constant;

        /// <summary>Gets whether Move Down can run: a row is chosen, it is not the last, and it is not of a constant attribute.</summary>
        public bool CanMoveDown => Index >= 0 && Index < _rows.Count - 1 && !_selected.Row.Constant;

        /// <summary>Gets whether Edit can run: a row is chosen.</summary>
        public bool CanEdit => Index >= 0;

        /// <summary>Gets whether Remove can run: a row is chosen and the block has more than one attribute.</summary>
        public bool CanRemove => Index >= 0 && _rows.Count > 1;

        private int Index => _selected == null ? -1 : _rows.IndexOf(_selected);

        /// <summary>Move Up: the row chosen put above the one before it, which the block takes on Apply or OK.</summary>
        public void MoveUp() => Move(-1);

        /// <summary>Move Down: the row chosen put below the one after it.</summary>
        public void MoveDown() => Move(1);

        /// <summary>
        /// Edit: Edit Attribute over a copy of the row chosen; OK puts what was typed into the row, which the block takes on
        /// Apply or OK, and the INSERTs on Sync. A number that does not read, or a height of nought, keeps the dialog open.
        /// </summary>
        /// <returns>Whether the row was changed.</returns>
        public bool Edit()
        {
            int at = Index;

            if (at < 0 || ShowEditor == null)
            {
                return false;
            }

            var editor = new EditAttributeViewModel(Request.Document, Request.Block, Request.Rows[at]);

            if (!ShowEditor(editor) || editor.Edited == null)
            {
                return false;
            }

            Request.Rows[at] = editor.Edited;
            Rebuild(at);
            return true;
        }

        /// <summary>Remove: the row chosen taken out, its definition erased from the block on Apply or OK.</summary>
        public void Remove()
        {
            int at = Index;

            if (at >= 0 && _rows.Count > 1)
            {
                Request.Rows.RemoveAt(at);
                Rebuild(Math.Min(at, Request.Rows.Count - 1));
            }
        }

        /// <summary>Sync: the rows written into the block and every INSERT of it brought in line, one undo step.</summary>
        public void Sync()
        {
            int at = Math.Max(0, Index);
            Request.Sync();
            Rebuild(at);
        }

        /// <summary>Apply: the rows written into the block, one undo step, the manager staying open.</summary>
        public void Apply()
        {
            int at = Math.Max(0, Index);
            Request.Apply();
            Rebuild(at);
        }

        /// <summary>Settings: Block Attribute Settings over the request's settings; OK shows the grid again with the columns chosen.</summary>
        /// <returns>Whether the settings were changed.</returns>
        public bool OpenSettings()
        {
            if (ShowSettings == null)
            {
                return false;
            }

            var settings = new AttributeManagerSettingsViewModel(Request.Settings);

            if (!ShowSettings(settings))
            {
                return false;
            }

            settings.WriteTo(Request.Settings);
            Rebuild(Math.Max(0, Index));
            return true;
        }

        /// <summary>
        /// Closes the manager with an action: OK writes what is left to write, Cancel writes nothing more, Select block asks
        /// first whether to save what was changed, then asks a block on the command line.
        /// </summary>
        /// <param name="action">OK, Cancel or Select block.</param>
        public void Close(CadDialogAction action)
        {
            if (action == CadDialogAction.SelectBlock)
            {
                AskBeforeLeaving();
            }

            Request.Action = action;
        }

        /// <summary>Where the rows of the block shown were changed, asks whether to save them; No reads them again from the block.</summary>
        private void AskBeforeLeaving()
        {
            if (AskSaveChanges != null && Request.HasChanges && !AskSaveChanges(Request.Block))
            {
                Request.Revert();
            }
        }

        private void Move(int by)
        {
            int at = Index;

            if (at < 0 || at + by < 0 || at + by >= Request.Rows.Count || Request.Rows[at].Constant)
            {
                return;
            }

            CadAttributeDefinitionRow row = Request.Rows[at];
            Request.Rows.RemoveAt(at);
            Request.Rows.Insert(at + by, row);
            Rebuild(at + by);
        }

        /// <summary>The columns and rows made again from the request, the row at an index chosen.</summary>
        private void Rebuild(int choose)
        {
            List<string> shown = CadAttributeManagerSettings
                .AllColumns.Where(c => c == "Tag" || Request.Settings.Columns.Contains(c))
                .ToList();
            double fixedWidth = shown.Where(c => c != "Prompt").Sum(Width);
            var columns = shown
                .Select(c => new AttributeColumnViewModel(
                    c,
                    c == "Prompt" ? Math.Max(120.0, 552.0 - fixedWidth) : Width(c)
                ))
                .ToList();
            _columns = columns;
            var tags = Request
                .Rows.GroupBy(r => r.Tag ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();
            _rows.Clear();

            foreach (CadAttributeDefinitionRow row in Request.Rows)
            {
                bool duplicate =
                    Request.Settings.EmphasizeDuplicateTags
                    && tags.Contains(row.Tag ?? string.Empty, StringComparer.OrdinalIgnoreCase);
                _rows.Add(
                    new AttributeManagerRowViewModel(
                        row,
                        columns
                            .Select(c => new AttributeCellViewModel(
                                Cell(row, c.Name),
                                c.Width,
                                c.Name != "Prompt"
                                    && c.Name != "Style"
                                    && c.Name != "Justification"
                                    && c.Name != "Layer"
                            ))
                            .ToList(),
                        duplicate
                    )
                );
            }

            _selected = _rows.Count == 0 ? null : _rows[Math.Max(0, Math.Min(choose, _rows.Count - 1))];
            Raise(nameof(Columns));
            Raise(nameof(Selected));
            Raise(nameof(Block));
            Raise(nameof(FoundInDrawing));
            Raise(nameof(FoundInModelSpace));
            RaiseButtons();
        }

        private void RaiseButtons()
        {
            Raise(nameof(CanMoveUp));
            Raise(nameof(CanMoveDown));
            Raise(nameof(CanEdit));
            Raise(nameof(CanRemove));
        }

        private static double Width(string column)
        {
            switch (column)
            {
                case "Tag":
                    return 120.0;
                case "Default":
                    return 120.0;
                case "Modes":
                    return 70.0;
                case "Annotative":
                    return 80.0;
                case "Justification":
                    return 110.0;
                case "Width Factor":
                case "Oblique Angle":
                case "Boundary Width":
                    return 100.0;
                default:
                    return 90.0;
            }
        }

        /// <summary>What a row shows in a column, as AutoCAD's grid writes it.</summary>
        internal static string Cell(CadAttributeDefinitionRow row, string column)
        {
            switch (column)
            {
                case "Tag":
                    return row.Tag;
                case "Prompt":
                    return row.Prompt;
                case "Default":
                    return row.Default;
                case "Modes":
                    return row.Modes;
                case "Annotative":
                    return "No";
                case "Style":
                    return row.TextStyle ?? "Standard";
                case "Justification":
                    return JustificationNames.Of(row.Justification);
                case "Height":
                    return BlockNumbers.Plain(row.Height);
                case "Rotation":
                    return BlockNumbers.Plain(row.Rotation * 180.0 / Math.PI);
                case "Width Factor":
                    return BlockNumbers.Plain(row.WidthFactor);
                case "Oblique Angle":
                    return BlockNumbers.Plain(row.Oblique * 180.0 / Math.PI);
                case "Boundary Width":
                    return BlockNumbers.Plain(row.BoundaryWidth);
                case "Layer":
                    return row.Layer;
                case "Linetype":
                    return row.Linetype;
                case "Color":
                    return AttributeEditorViewModel.ColorName(row.ColorIndex);
                case "Lineweight":
                    return AttributeEditorViewModel.WeightName(row.LineWeight);
                default:
                    return "ByColor";
            }
        }
    }

    /// <summary>
    /// AutoCAD's Edit Attribute dialog of the Block Attribute Manager, over a copy of one attribute definition: the tabs
    /// Attribute (the modes and the tag, prompt and default), Text Options and Properties. Constant and Multiple lines are
    /// fixed when the attribute is defined, and stay greyed; Boundary Width opens only for an attribute of several lines.
    /// </summary>
    public sealed class EditAttributeViewModel : ObservableObject
    {
        private readonly CadAttributeDefinitionRow _row;
        private string _tab = "Attribute";
        private string _height;
        private string _rotation;
        private string _width;
        private string _oblique;
        private string _message = string.Empty;

        /// <summary>Makes the dialog over a row, which it does not change: OK leaves the change in <see cref="Edited"/>.</summary>
        /// <param name="document">The document, whose styles, layers and linetypes the lists offer.</param>
        /// <param name="block">The name of the block, Active Block.</param>
        /// <param name="row">The row.</param>
        /// <exception cref="ArgumentNullException">Thrown when the document or the row is null.</exception>
        public EditAttributeViewModel(CadDocument document, string block, CadAttributeDefinitionRow row)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            _row = (row ?? throw new ArgumentNullException(nameof(row))).Clone();
            Block = block ?? string.Empty;
            _height = BlockNumbers.Plain(_row.Height);
            _rotation = BlockNumbers.Plain(_row.Rotation * 180.0 / Math.PI);
            _width = BlockNumbers.Plain(_row.WidthFactor);
            _oblique = BlockNumbers.Plain(_row.Oblique * 180.0 / Math.PI);
            DxfDatabase database = document.Database;
            Styles = database
                .TextStyleTable.Select(s => s.Name)
                .Where(n => n.Length > 0)
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ThenBy(n => n, StringComparer.Ordinal)
                .ToList();
            Layers = database
                .LayerTable.Select(l => l.Name)
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ThenBy(n => n, StringComparer.Ordinal)
                .ToList();
            Linetypes = database
                .LinetypeTable.Select(l => l.Name)
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ThenBy(n => n, StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>Gets the name of the block, which the dialog shows as Active Block.</summary>
        public string Block { get; }

        /// <summary>Gets the row as OK left it; null until OK.</summary>
        public CadAttributeDefinitionRow Edited { get; private set; }

        /// <summary>Gets the tabs.</summary>
        public IReadOnlyList<string> Tabs { get; } = new[] { "Attribute", "Text Options", "Properties" };

        /// <summary>Gets or sets the tab shown.</summary>
        public string Tab
        {
            get => _tab;
            set
            {
                if (Set(ref _tab, Tabs.Contains(value) ? value : "Attribute"))
                {
                    Raise(nameof(IsAttributeTab));
                    Raise(nameof(IsTextTab));
                    Raise(nameof(IsPropertiesTab));
                }
            }
        }

        /// <summary>Gets whether the Attribute tab is shown.</summary>
        public bool IsAttributeTab => _tab == "Attribute";

        /// <summary>Gets whether the Text Options tab is shown.</summary>
        public bool IsTextTab => _tab == "Text Options";

        /// <summary>Gets whether the Properties tab is shown.</summary>
        public bool IsPropertiesTab => _tab == "Properties";

        /// <summary>Gets or sets the tag.</summary>
        public string Tag
        {
            get => _row.Tag;
            set
            {
                _row.Tag = value ?? string.Empty;
                Raise(nameof(Tag));
            }
        }

        /// <summary>Gets or sets the prompt.</summary>
        public string Prompt
        {
            get => _row.Prompt;
            set
            {
                _row.Prompt = value ?? string.Empty;
                Raise(nameof(Prompt));
            }
        }

        /// <summary>Gets or sets the default value.</summary>
        public string Default
        {
            get => _row.Default;
            set
            {
                _row.Default = value ?? string.Empty;
                Raise(nameof(Default));
            }
        }

        /// <summary>Gets or sets whether the attribute is not shown.</summary>
        public bool Invisible
        {
            get => _row.Invisible;
            set
            {
                _row.Invisible = value;
                Raise(nameof(Invisible));
            }
        }

        /// <summary>Gets whether its value is constant, which only ATTDEF sets.</summary>
        public bool Constant => _row.Constant;

        /// <summary>Gets or sets whether its value is asked twice.</summary>
        public bool Verify
        {
            get => _row.Verify;
            set
            {
                _row.Verify = value;
                Raise(nameof(Verify));
            }
        }

        /// <summary>Gets or sets whether its default is taken without asking.</summary>
        public bool Preset
        {
            get => _row.Preset;
            set
            {
                _row.Preset = value;
                Raise(nameof(Preset));
            }
        }

        /// <summary>Gets or sets whether it keeps its place in the block.</summary>
        public bool LockPosition
        {
            get => _row.LockPosition;
            set
            {
                _row.LockPosition = value;
                Raise(nameof(LockPosition));
            }
        }

        /// <summary>Gets whether it is of several lines, which only ATTDEF sets.</summary>
        public bool MultipleLines => _row.MultipleLines;

        /// <summary>Gets the text styles of the drawing.</summary>
        public IReadOnlyList<string> Styles { get; }

        /// <summary>Gets the justifications.</summary>
        public IReadOnlyList<string> Justifications { get; } = JustificationNames.All;

        /// <summary>Gets the layers of the drawing.</summary>
        public IReadOnlyList<string> Layers { get; }

        /// <summary>Gets the linetypes of the drawing.</summary>
        public IReadOnlyList<string> Linetypes { get; }

        /// <summary>Gets the colours offered: ByLayer, ByBlock and the seven named.</summary>
        public IReadOnlyList<string> Colors { get; } =
            new[] { "ByLayer", "ByBlock", "Red", "Yellow", "Green", "Cyan", "Blue", "Magenta", "White" };

        /// <summary>Gets the lineweights offered: ByLayer, ByBlock, Default and AutoCAD's widths in millimetres.</summary>
        public IReadOnlyList<string> LineWeights { get; } =
            new[] { "ByLayer", "ByBlock", "Default" }
                .Concat(
                    new[]
                    {
                        0,
                        5,
                        9,
                        13,
                        15,
                        18,
                        20,
                        25,
                        30,
                        35,
                        40,
                        50,
                        53,
                        60,
                        70,
                        80,
                        90,
                        100,
                        106,
                        120,
                        140,
                        158,
                        200,
                        211,
                    }.Select(w => (w / 100.0).ToString("0.00", CultureInfo.InvariantCulture) + " mm")
                )
                .ToList();

        /// <summary>Gets or sets the text style.</summary>
        public string TextStyle
        {
            get => _row.TextStyle;
            set
            {
                _row.TextStyle = value;
                Raise(nameof(TextStyle));
            }
        }

        /// <summary>Gets or sets the justification, by its name.</summary>
        public string Justification
        {
            get => JustificationNames.Of(_row.Justification);
            set
            {
                _row.Justification = JustificationNames.From(value);
                Raise(nameof(Justification));
            }
        }

        /// <summary>Gets or sets the height, as typed.</summary>
        public string Height
        {
            get => _height;
            set => Set(ref _height, value ?? string.Empty);
        }

        /// <summary>Gets or sets the rotation in degrees, as typed.</summary>
        public string Rotation
        {
            get => _rotation;
            set => Set(ref _rotation, value ?? string.Empty);
        }

        /// <summary>Gets or sets whether the text is written backwards.</summary>
        public bool Backwards
        {
            get => _row.Backwards;
            set
            {
                _row.Backwards = value;
                Raise(nameof(Backwards));
            }
        }

        /// <summary>Gets or sets whether the text is upside down.</summary>
        public bool UpsideDown
        {
            get => _row.UpsideDown;
            set
            {
                _row.UpsideDown = value;
                Raise(nameof(UpsideDown));
            }
        }

        /// <summary>Gets or sets the width factor, as typed.</summary>
        public string WidthFactor
        {
            get => _width;
            set => Set(ref _width, value ?? string.Empty);
        }

        /// <summary>Gets or sets the oblique angle in degrees, as typed.</summary>
        public string Oblique
        {
            get => _oblique;
            set => Set(ref _oblique, value ?? string.Empty);
        }

        /// <summary>Gets the boundary width, which the dialog shows greyed: the width of an attribute of several lines, nought for one line.</summary>
        public string BoundaryWidth => BlockNumbers.Plain(_row.BoundaryWidth);

        /// <summary>Gets or sets the layer.</summary>
        public string Layer
        {
            get => _row.Layer;
            set
            {
                _row.Layer = value;
                Raise(nameof(Layer));
            }
        }

        /// <summary>Gets or sets the linetype.</summary>
        public string Linetype
        {
            get => _row.Linetype;
            set
            {
                _row.Linetype = value;
                Raise(nameof(Linetype));
            }
        }

        /// <summary>Gets or sets the colour by its name: ByLayer, ByBlock, Red to White, or Color n.</summary>
        public string Color
        {
            get => AttributeEditorViewModel.ColorName(_row.ColorIndex);
            set
            {
                _row.ColorIndex = AttributeEditorViewModel.ColorIndex(value, _row.ColorIndex);
                Raise(nameof(Color));
            }
        }

        /// <summary>Gets or sets the lineweight by its name: ByLayer, ByBlock, Default, or millimetres.</summary>
        public string LineWeight
        {
            get => AttributeEditorViewModel.WeightName(_row.LineWeight);
            set
            {
                _row.LineWeight = AttributeEditorViewModel.Weight(value, _row.LineWeight);
                Raise(nameof(LineWeight));
            }
        }

        /// <summary>Gets what OK said where it kept the dialog open, as "Value must be positive and nonzero."; empty otherwise.</summary>
        public string Message
        {
            get => _message;
            private set => Set(ref _message, value ?? string.Empty);
        }

        /// <summary>
        /// OK: the row as typed put in <see cref="Edited"/>. A tag of more than one word, or a number that does not read, a
        /// height or width factor not above nought, keeps the dialog open, <see cref="Message"/> saying why.
        /// </summary>
        /// <returns>Whether the dialog may close.</returns>
        public bool Ok()
        {
            if (!CadBlocks.IsTag(_row.Tag))
            {
                Message = "The tag is one word, with no space.";
                return false;
            }

            if (
                !BlockNumbers.TryRead(_height, out double height)
                || !BlockNumbers.TryRead(_rotation, out double rotation)
                || !BlockNumbers.TryRead(_width, out double width)
                || !BlockNumbers.TryRead(_oblique, out double oblique)
            )
            {
                Message = "Requires a number.";
                return false;
            }

            if (!(height > 0.0) || !(width > 0.0))
            {
                Message = "Value must be positive and nonzero.";
                return false;
            }

            CadAttributeDefinitionRow edited = _row.Clone();
            edited.Tag = edited.Tag.ToUpperInvariant();
            edited.Height = height;
            edited.Rotation = rotation * Math.PI / 180.0;
            edited.WidthFactor = width;
            edited.Oblique = oblique * Math.PI / 180.0;
            Edited = edited;
            Message = string.Empty;
            return true;
        }
    }

    /// <summary>
    /// AutoCAD's Block Attribute Settings dialog of the manager, over a copy of its settings: the columns of the grid (Tag
    /// always, greyed), Select All and Clear All, Emphasize duplicate tags and Apply changes to existing references.
    /// </summary>
    public sealed class AttributeManagerSettingsViewModel : ObservableObject
    {
        private bool _emphasize;
        private bool _apply;

        /// <summary>Makes the dialog over settings, which it does not change until <see cref="WriteTo"/>.</summary>
        /// <param name="settings">The settings.</param>
        /// <exception cref="ArgumentNullException">Thrown when the settings are null.</exception>
        public AttributeManagerSettingsViewModel(CadAttributeManagerSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            Columns = CadAttributeManagerSettings
                .AllColumns.Select(c => new AttributeSettingColumnViewModel(
                    c,
                    c == "Tag" || settings.Columns.Contains(c),
                    c != "Tag" && c != "Plot style"
                ))
                .ToList();
            _emphasize = settings.EmphasizeDuplicateTags;
            _apply = settings.ApplyToExistingReferences;
        }

        /// <summary>Gets the columns, each checked where the grid shows it.</summary>
        public IReadOnlyList<AttributeSettingColumnViewModel> Columns { get; }

        /// <summary>Gets or sets whether a tag given twice is drawn in red.</summary>
        public bool EmphasizeDuplicateTags
        {
            get => _emphasize;
            set => Set(ref _emphasize, value);
        }

        /// <summary>Gets or sets whether Apply and OK bring the INSERTs of the block in line at once.</summary>
        public bool ApplyToExistingReferences
        {
            get => _apply;
            set => Set(ref _apply, value);
        }

        /// <summary>Select All: every column that may be chosen checked.</summary>
        public void SelectAll() => Columns.Where(c => c.IsEnabled).ToList().ForEach(c => c.IsChecked = true);

        /// <summary>Clear All: every column that may be chosen cleared, Tag left.</summary>
        public void ClearAll() => Columns.Where(c => c.IsEnabled).ToList().ForEach(c => c.IsChecked = false);

        /// <summary>OK: the settings as checked written into the manager's.</summary>
        /// <param name="settings">The settings of the manager.</param>
        /// <exception cref="ArgumentNullException">Thrown when the settings are null.</exception>
        public void WriteTo(CadAttributeManagerSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            settings.Columns.Clear();

            foreach (AttributeSettingColumnViewModel column in Columns.Where(c => c.IsChecked))
            {
                settings.Columns.Add(column.Name);
            }

            settings.EmphasizeDuplicateTags = _emphasize;
            settings.ApplyToExistingReferences = _apply;
        }
    }

    /// <summary>A column of Block Attribute Settings: its name, whether it is checked, and whether it may be changed.</summary>
    public sealed class AttributeSettingColumnViewModel : ObservableObject
    {
        private bool _checked;

        internal AttributeSettingColumnViewModel(string name, bool isChecked, bool enabled)
        {
            Name = name;
            _checked = isChecked;
            IsEnabled = enabled;
        }

        /// <summary>Gets the name of the column.</summary>
        public string Name { get; }

        /// <summary>Gets or sets whether the grid shows it.</summary>
        public bool IsChecked
        {
            get => _checked;
            set => Set(ref _checked, IsEnabled ? value : _checked);
        }

        /// <summary>Gets whether it may be changed: not Tag, which is always shown, nor Plot style, which the presenter has not.</summary>
        public bool IsEnabled { get; }
    }

    /// <summary>
    /// AutoCAD's Edit Attributes dialog, which INSERT shows with ATTDIA 1, over the request its command hands the window: the
    /// block's name and a box for each attribute's value, eight a page in the order of the block, Previous and Next for the
    /// pages, Cancel and OK. A title block of eleven attributes opens on Page 1 of 2.
    /// </summary>
    public sealed class EditAttributesViewModel : ObservableObject
    {
        /// <summary>How many attributes a page holds, as AutoCAD's dialog.</summary>
        public const int PageSize = 8;

        private int _page;

        /// <summary>Makes the dialog over a request.</summary>
        /// <param name="request">The request of INSERT.</param>
        /// <exception cref="ArgumentNullException">Thrown when the request is null.</exception>
        public EditAttributesViewModel(CadAttributeValuesRequest request)
        {
            Request = request ?? throw new ArgumentNullException(nameof(request));
        }

        /// <summary>Gets the request.</summary>
        public CadAttributeValuesRequest Request { get; }

        /// <summary>Gets the name of the block.</summary>
        public string Block => Request.Block;

        /// <summary>Gets the rows of the page shown.</summary>
        public IReadOnlyList<CadAttributeValueRow> Rows => Request.Rows.Skip(_page * PageSize).Take(PageSize).ToList();

        /// <summary>Gets how many pages there are, one at least.</summary>
        public int Pages => Math.Max(1, (Request.Rows.Count + PageSize - 1) / PageSize);

        /// <summary>Gets or sets the page shown, nought the first.</summary>
        public int Page
        {
            get => _page;
            set
            {
                if (Set(ref _page, Math.Max(0, Math.Min(Pages - 1, value))))
                {
                    Raise(nameof(Rows));
                    Raise(nameof(PageText));
                    Raise(nameof(CanPrevious));
                    Raise(nameof(CanNext));
                }
            }
        }

        /// <summary>Gets "Page 1 of 2".</summary>
        public string PageText =>
            "Page "
            + (_page + 1).ToString(CultureInfo.InvariantCulture)
            + " of "
            + Pages.ToString(CultureInfo.InvariantCulture);

        /// <summary>Gets whether there is a page before.</summary>
        public bool CanPrevious => _page > 0;

        /// <summary>Gets whether there is a page after.</summary>
        public bool CanNext => _page < Pages - 1;

        /// <summary>Closes the dialog with an action: OK takes the values, Cancel the defaults.</summary>
        /// <param name="action">OK or Cancel.</param>
        public void Close(CadDialogAction action) => Request.Action = action;
    }
}
