using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// The Insert Table dialog of TABLE, as AutoCAD 2021's: the table style (and its dialog), Start from empty table (the data
    /// link and Data Extraction greyed), Specify insertion point or window, the columns and their width, the data rows and their
    /// height in lines, and the cell styles of the rows. OK writes them into the command's request.
    /// </summary>
    public sealed class InsertTableViewModel : ObservableObject
    {
        private readonly CadInsertTableRequest _request;
        private string _tableStyle;
        private string _columns;
        private string _columnWidth;
        private string _dataRows;
        private string _rowLines;
        private bool _byWindow;

        /// <summary>Makes the dialog over the request of TABLE.</summary>
        /// <param name="request">The request.</param>
        public InsertTableViewModel(CadInsertTableRequest request)
        {
            _request = request ?? throw new ArgumentNullException(nameof(request));
            _tableStyle = request.TableStyle;
            _columns = request.Columns.ToString(CultureInfo.InvariantCulture);
            _columnWidth = request.ColumnWidth.ToString("0.####", CultureInfo.InvariantCulture);
            _dataRows = request.DataRows.ToString(CultureInfo.InvariantCulture);
            _rowLines = request.RowHeightLines.ToString(CultureInfo.InvariantCulture);
            _byWindow = request.ByWindow;
            ReloadStyles();
            OkCommand = new DelegateCommand(_ => Ok(), _ => IsValid);
            CancelCommand = new DelegateCommand(_ => CloseRequested?.Invoke(this, false));
        }

        /// <summary>Raised when the dialog is to close: true for OK.</summary>
        public event EventHandler<bool> CloseRequested;

        /// <summary>Gets the table styles of the drawing; Standard where it has none yet, which TABLE makes.</summary>
        public ObservableCollection<string> TableStyles { get; } = new ObservableCollection<string>();

        /// <summary>Gets or sets the table style chosen.</summary>
        public string TableStyle
        {
            get => _tableStyle;
            set
            {
                if (Set(ref _tableStyle, value))
                {
                    Raise(nameof(Preview));
                }
            }
        }

        /// <summary>Gets the settings of the style chosen, which the preview draws: AutoCAD's Standard where the drawing has no such style yet.</summary>
        public CadTableStyleSettings Preview
        {
            get
            {
                DxfTableStyle style = CadTableStyleEditor.Find(_request.Document.Database, _tableStyle);
                return style == null ? CadTableStyleEditor.Standard(_request.Document.Database) : CadTableStyleSettings.Of(style);
            }
        }

        /// <summary>Gets or sets whether the table is placed by its insertion point (false) or by a window (true).</summary>
        public bool ByWindow
        {
            get => _byWindow;
            set
            {
                if (Set(ref _byWindow, value))
                {
                    Raise(nameof(ByPoint));
                }
            }
        }

        /// <summary>Gets or sets whether the table is placed by its insertion point.</summary>
        public bool ByPoint
        {
            get => !_byWindow;
            set => ByWindow = !value;
        }

        /// <summary>Gets or sets the number of columns, as typed.</summary>
        public string Columns
        {
            get => _columns;
            set => Typed(ref _columns, value);
        }

        /// <summary>Gets or sets the width of a column, as typed.</summary>
        public string ColumnWidth
        {
            get => _columnWidth;
            set => Typed(ref _columnWidth, value);
        }

        /// <summary>Gets or sets the number of data rows, as typed.</summary>
        public string DataRows
        {
            get => _dataRows;
            set => Typed(ref _dataRows, value);
        }

        /// <summary>Gets or sets the height of a data row in lines, as typed.</summary>
        public string RowLines
        {
            get => _rowLines;
            set => Typed(ref _rowLines, value);
        }

        /// <summary>Gets the cell styles the rows can take, as the boxes list them.</summary>
        public IReadOnlyList<string> CellStyles { get; } = new[] { "Title", "Header", "Data" };

        /// <summary>Gets the cell style of the first row: the title, as a table of the file of R2000 to R2018 holds it.</summary>
        public string FirstRowStyle => "Title";

        /// <summary>Gets the cell style of the second row: the header.</summary>
        public string SecondRowStyle => "Header";

        /// <summary>Gets the cell style of the other rows: the data.</summary>
        public string OtherRowStyle => "Data";

        /// <summary>Gets whether every number typed is one a table takes: whole columns and rows of 1 or more, a width above nought.</summary>
        public bool IsValid => Whole(_columns) >= 1 && Whole(_dataRows) >= 1 && Whole(_rowLines) >= 1 && Real(_columnWidth) > 0.0;

        /// <summary>Gets OK.</summary>
        public DelegateCommand OkCommand { get; }

        /// <summary>Gets Cancel and the close button.</summary>
        public DelegateCommand CancelCommand { get; }

        /// <summary>Reads the table styles again, as after the Table Style dialog launched from this one; the style chosen kept where it is still there.</summary>
        public void ReloadStyles()
        {
            string kept = _tableStyle;
            TableStyles.Clear();

            foreach (string name in CadTableStyleEditor.Styles(_request.Document.Database))
            {
                TableStyles.Add(name);
            }

            if (!TableStyles.Contains("Standard", StringComparer.OrdinalIgnoreCase))
            {
                TableStyles.Add("Standard");
            }

            TableStyle = TableStyles.FirstOrDefault(s => string.Equals(s, kept, StringComparison.OrdinalIgnoreCase)) ?? "Standard";
        }

        /// <summary>OK: the values written into the request, and the dialog closed.</summary>
        public void Ok()
        {
            if (!IsValid)
            {
                return;
            }

            _request.TableStyle = _tableStyle;
            _request.ByWindow = _byWindow;
            _request.Columns = Whole(_columns);
            _request.ColumnWidth = Real(_columnWidth);
            _request.DataRows = Whole(_dataRows);
            _request.RowHeightLines = Whole(_rowLines);
            _request.Action = CadDialogAction.Ok;
            CloseRequested?.Invoke(this, true);
        }

        private void Typed(ref string field, string value)
        {
            if (Set(ref field, value ?? string.Empty))
            {
                Raise(nameof(IsValid));
                OkCommand?.RaiseCanExecuteChanged();
            }
        }

        private static int Whole(string text) => int.TryParse((text ?? string.Empty).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : 0;

        private static double Real(string text)
            => double.TryParse((text ?? string.Empty).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double value) && !double.IsInfinity(value) ? value : 0.0;
    }

    /// <summary>A row of the styles list of the Table Style dialog: the style's name and what the list says of it ("current", "2 tables").</summary>
    public sealed class TableStyleRowViewModel
    {
        /// <summary>Makes a row.</summary>
        /// <param name="name">The style's name.</param>
        /// <param name="hint">What the list says of it.</param>
        public TableStyleRowViewModel(string name, string hint)
        {
            Name = name;
            Hint = hint;
        }

        /// <summary>Gets the style's name.</summary>
        public string Name { get; }

        /// <summary>Gets what the list says of the style.</summary>
        public string Hint { get; }
    }

    /// <summary>
    /// The Table Style dialog (TABLESTYLE), as AutoCAD 2021's: the current style, the styles (All styles or Styles in use), the
    /// preview of the style selected, and Set Current, New…, Modify… and Delete, greyed for Standard, the current style and a
    /// style a table uses. Each button is one undo step of <see cref="CadTableStyleEditor"/>.
    /// </summary>
    public sealed class TableStyleManagerViewModel : ObservableObject
    {
        /// <summary>The filter of every style.</summary>
        public const string AllStyles = "All styles";

        /// <summary>The filter of the styles a table is written in.</summary>
        public const string StylesInUse = "Styles in use";

        private readonly CadDocument _document;
        private string _filter = AllStyles;
        private TableStyleRowViewModel _selected;
        private string _message = string.Empty;

        /// <summary>Makes the dialog's view model over a document, the current style selected.</summary>
        /// <param name="document">The document.</param>
        public TableStyleManagerViewModel(CadDocument document)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
            SetCurrentCommand = new DelegateCommand(_ => SetCurrent(), _ => _selected != null);
            NewCommand = new DelegateCommand(_ => New());
            ModifyCommand = new DelegateCommand(_ => Modify(), _ => _selected != null);
            DeleteCommand = new DelegateCommand(_ => Delete(), _ => CanDelete);
            CloseCommand = new DelegateCommand(_ => CloseRequested?.Invoke(this, true));
            Reload(CadTableStyleEditor.Current(document.Database));
        }

        /// <summary>Raised when the dialog is to close.</summary>
        public event EventHandler<bool> CloseRequested;

        /// <summary>Gets or sets what asks the name of a new style and the style it starts with (the name offered, the styles): the name and the start, or null for Cancel. Set by the window.</summary>
        public Func<string, IReadOnlyList<string>, Tuple<string, string>> AskNewStyle { get; set; }

        /// <summary>Gets or sets what shows the Modify Table Style dialog and says whether it was OK. Set by the window.</summary>
        public Func<TableStyleModifyViewModel, bool> ShowModify { get; set; }

        /// <summary>Gets the filters of the list: All styles, Styles in use.</summary>
        public IReadOnlyList<string> Filters { get; } = new[] { AllStyles, StylesInUse };

        /// <summary>Gets or sets the filter of the list.</summary>
        public string Filter
        {
            get => _filter;
            set
            {
                if (Filters.Contains(value) && Set(ref _filter, value))
                {
                    Reload(_selected?.Name);
                }
            }
        }

        /// <summary>Gets the styles listed.</summary>
        public ObservableCollection<TableStyleRowViewModel> Styles { get; } = new ObservableCollection<TableStyleRowViewModel>();

        /// <summary>Gets the current table style's name.</summary>
        public string CurrentStyle => CadTableStyleEditor.Current(_document.Database);

        /// <summary>Gets or sets the style selected.</summary>
        public TableStyleRowViewModel SelectedStyle
        {
            get => _selected;
            set
            {
                if (Set(ref _selected, value))
                {
                    Raise(nameof(PreviewTitle));
                    Raise(nameof(Preview));
                    Raise(nameof(CanDelete));
                    SetCurrentCommand.RaiseCanExecuteChanged();
                    ModifyCommand.RaiseCanExecuteChanged();
                    DeleteCommand.RaiseCanExecuteChanged();
                }
            }
        }

        /// <summary>Gets the line over the preview: Preview of: GH_SCHEDULE.</summary>
        public string PreviewTitle => _selected == null ? "Preview" : "Preview of: " + _selected.Name;

        /// <summary>Gets the settings of the style selected, which the preview draws; null for none.</summary>
        public CadTableStyleSettings Preview
        {
            get
            {
                DxfTableStyle style = _selected == null ? null : CadTableStyleEditor.Find(_document.Database, _selected.Name);
                return style == null ? (_selected != null && _selected.Name == "Standard" ? CadTableStyleEditor.Standard(_document.Database) : null) : CadTableStyleSettings.Of(style);
            }
        }

        /// <summary>Gets whether the style selected may be deleted: not Standard, not current, not in use.</summary>
        public bool CanDelete => _selected != null && CadTableStyleEditor.Find(_document.Database, _selected.Name) != null && CadTableStyleEditor.WhyNotDeleted(_document.Database, _selected.Name) == null;

        /// <summary>Gets what the last button said where it was refused; empty otherwise.</summary>
        public string Message
        {
            get => _message;
            private set => Set(ref _message, value ?? string.Empty);
        }

        /// <summary>Gets Set Current.</summary>
        public DelegateCommand SetCurrentCommand { get; }

        /// <summary>Gets New….</summary>
        public DelegateCommand NewCommand { get; }

        /// <summary>Gets Modify….</summary>
        public DelegateCommand ModifyCommand { get; }

        /// <summary>Gets Delete.</summary>
        public DelegateCommand DeleteCommand { get; }

        /// <summary>Gets Close.</summary>
        public DelegateCommand CloseCommand { get; }

        /// <summary>Set Current: the style selected made current.</summary>
        public void SetCurrent()
        {
            if (_selected == null)
            {
                return;
            }

            Standard();
            Said(CadTableStyleEditor.SetCurrent(_document, _selected.Name));
            Reload(_selected.Name);
            Raise(nameof(CurrentStyle));
        }

        /// <summary>New…: a style named, a copy of the style chosen to start with, then the Modify dialog over it as New Table Style.</summary>
        public void New()
        {
            Standard();
            IReadOnlyList<string> names = CadTableStyleEditor.Styles(_document.Database);
            string offered = "Copy of " + (_selected?.Name ?? "Standard");
            Tuple<string, string> answer = AskNewStyle?.Invoke(offered, names);

            if (answer == null)
            {
                return;
            }

            CadEditResult result = CadTableStyleEditor.Add(_document, answer.Item1, answer.Item2);
            Said(result);

            if (!result.Succeeded)
            {
                return;
            }

            string name = CadTableStyleEditor.Find(_document.Database, answer.Item1).Name;
            Reload(name);

            if (ShowModify != null)
            {
                ShowModify(new TableStyleModifyViewModel(_document, name, true));
                Reload(name);
            }
        }

        /// <summary>Modify…: the Modify Table Style dialog over the style selected.</summary>
        public void Modify()
        {
            if (_selected == null || ShowModify == null)
            {
                return;
            }

            Standard();
            ShowModify(new TableStyleModifyViewModel(_document, _selected.Name, false));
            Reload(_selected.Name);
        }

        /// <summary>Delete: the style selected deleted, where it may be.</summary>
        public void Delete()
        {
            if (!CanDelete)
            {
                return;
            }

            Said(CadTableStyleEditor.Delete(_document, _selected.Name));
            Reload(CurrentStyle);
        }

        /// <summary>A drawing with no table style is given Standard, as AutoCAD's always has it, before a button changes anything.</summary>
        private void Standard()
        {
            Said(CadTableStyleEditor.AddStandard(_document));
        }

        private void Said(CadEditResult result) => Message = result.Succeeded ? string.Empty : result.Message;

        private void Reload(string select)
        {
            Styles.Clear();
            string current = CurrentStyle;
            List<string> names = CadTableStyleEditor.Styles(_document.Database).ToList();

            if (!names.Contains("Standard", StringComparer.OrdinalIgnoreCase))
            {
                names.Add("Standard");
            }

            foreach (string name in names)
            {
                int uses = CadTableStyleEditor.CountUses(_document.Database, name);

                if (_filter == StylesInUse && uses == 0)
                {
                    continue;
                }

                string hint = string.Equals(name, current, StringComparison.OrdinalIgnoreCase) ? "current"
                    : uses > 0 ? uses.ToString(CultureInfo.InvariantCulture) + (uses == 1 ? " table" : " tables") : string.Empty;
                Styles.Add(new TableStyleRowViewModel(name, hint));
            }

            SelectedStyle = Styles.FirstOrDefault(r => string.Equals(r.Name, select, StringComparison.OrdinalIgnoreCase)) ?? Styles.FirstOrDefault();
            Raise(nameof(Preview));
            Raise(nameof(CanDelete));
        }
    }

    /// <summary>
    /// The Modify Table Style dialog (and New Table Style), as AutoCAD 2021's: the table direction, and the three cell styles,
    /// Data, Header and Title, each in its General tab (fill, alignment, margins), Text tab (text style, height, colour) and
    /// Borders tab (lineweight, colour and the border buttons). OK writes the style, one undo step, and draws its tables anew.
    /// What the file of R2000 to R2018 has no place for is greyed: the starting table, the format and type of a cell, the
    /// merging on creation, the angle of the text, the linetype and the double lines, and new cell styles.
    /// </summary>
    public sealed class TableStyleModifyViewModel : ObservableObject
    {
        /// <summary>The colours the colour boxes offer, by name and index.</summary>
        public static readonly IReadOnlyList<TableFillChoice> Colors = new[]
        {
            new TableFillChoice("ByBlock", 0), new TableFillChoice("ByLayer", 256), new TableFillChoice("Red", 1), new TableFillChoice("Yellow", 2),
            new TableFillChoice("Green", 3), new TableFillChoice("Cyan", 4), new TableFillChoice("Blue", 5), new TableFillChoice("Magenta", 6),
            new TableFillChoice("White", 7), new TableFillChoice("Color 8", 8), new TableFillChoice("Color 9", 9),
        };

        /// <summary>The lineweights the Borders tab offers, by name.</summary>
        public static readonly IReadOnlyList<string> LineWeights = new[] { "ByBlock", "ByLayer", "Default", "0.00 mm", "0.13 mm", "0.18 mm", "0.25 mm", "0.35 mm", "0.50 mm", "0.70 mm", "1.00 mm" };

        private static readonly DxfLineWeight[] Weights =
        {
            DxfLineWeight.ByBlock, DxfLineWeight.ByLayer, DxfLineWeight.ByLineWeightDefault, DxfLineWeight.LineWeight000, DxfLineWeight.LineWeight013,
            DxfLineWeight.LineWeight018, DxfLineWeight.LineWeight025, DxfLineWeight.LineWeight035, DxfLineWeight.LineWeight050, DxfLineWeight.LineWeight070,
            DxfLineWeight.LineWeight100,
        };

        private readonly CadDocument _document;
        private readonly string _name;
        private readonly CadTableStyleSettings _settings;
        private string _cellStyle = "Data";
        private string _tab = "General";
        private string _borderWeight = "ByBlock";
        private TableFillChoice _borderColor = Colors[0];
        private string _message = string.Empty;

        /// <summary>Makes the dialog over a style.</summary>
        /// <param name="document">The document.</param>
        /// <param name="name">The style's name.</param>
        /// <param name="isNew">Whether the style was just made by New….</param>
        public TableStyleModifyViewModel(CadDocument document, string name, bool isNew)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
            DxfTableStyle style = CadTableStyleEditor.Find(document.Database, name) ?? throw new ArgumentException("The drawing has no table style \"" + name + "\".", nameof(name));
            _name = style.Name;
            _settings = CadTableStyleSettings.Of(style);
            Title = (isNew ? "New Table Style: " : "Modify Table Style: ") + _name;
            TextStyles = CadTextStyleEditor.Styles(document.Database).Select(s => s.Name).ToList();
            BorderCommand = new DelegateCommand(p => Border(p as string));
            OkCommand = new DelegateCommand(_ => Ok());
            CancelCommand = new DelegateCommand(_ => CloseRequested?.Invoke(this, false));
        }

        /// <summary>Raised when the dialog is to close: true for OK.</summary>
        public event EventHandler<bool> CloseRequested;

        /// <summary>Gets the title: Modify Table Style: GH_SCHEDULE, or New Table Style: and the name.</summary>
        public string Title { get; }

        /// <summary>Gets the settings shown, which the previews draw.</summary>
        public CadTableStyleSettings Settings => _settings;

        /// <summary>Gets a number that grows with each change of the settings, so that the previews draw them anew.</summary>
        public int Version { get; private set; }

        /// <summary>Gets the table directions: Down, Up.</summary>
        public IReadOnlyList<string> Directions { get; } = new[] { "Down", "Up" };

        /// <summary>Gets or sets the table direction.</summary>
        public string Direction
        {
            get => _settings.FlowDirection == DxfFlowDirection.BottomToTop ? "Up" : "Down";
            set
            {
                _settings.FlowDirection = value == "Up" ? DxfFlowDirection.BottomToTop : DxfFlowDirection.TopToBottom;
                Changed(nameof(Direction));
            }
        }

        /// <summary>Gets the cell styles: Data, Header, Title.</summary>
        public IReadOnlyList<string> CellStyles { get; } = new[] { "Data", "Header", "Title" };

        /// <summary>Gets or sets the cell style shown.</summary>
        public string CellStyle
        {
            get => _cellStyle;
            set
            {
                if (CellStyles.Contains(value) && Set(ref _cellStyle, value))
                {
                    foreach (string name in new[] { nameof(Fill), nameof(Alignment), nameof(TextStyle), nameof(TextHeight), nameof(TextColor), nameof(Lines) })
                    {
                        Raise(name);
                    }
                }
            }
        }

        /// <summary>Gets the tabs of a cell style: General, Text, Borders.</summary>
        public IReadOnlyList<string> Tabs { get; } = new[] { "General", "Text", "Borders" };

        /// <summary>Gets or sets the tab shown.</summary>
        public string Tab
        {
            get => _tab;
            set
            {
                if (Tabs.Contains(value) && Set(ref _tab, value))
                {
                    Raise(nameof(IsGeneral));
                    Raise(nameof(IsText));
                    Raise(nameof(IsBorders));
                }
            }
        }

        /// <summary>Gets whether the General tab is shown.</summary>
        public bool IsGeneral => _tab == "General";

        /// <summary>Gets whether the Text tab is shown.</summary>
        public bool IsText => _tab == "Text";

        /// <summary>Gets whether the Borders tab is shown.</summary>
        public bool IsBorders => _tab == "Borders";

        /// <summary>Gets the fills: None, then the colours.</summary>
        public IReadOnlyList<TableFillChoice> Fills { get; } = new[] { new TableFillChoice("None", null) }.Concat(Colors).ToList();

        /// <summary>Gets or sets the fill of the cell style shown.</summary>
        public TableFillChoice Fill
        {
            get => Choice(Fills, Cell.FillColor);
            set
            {
                Cell.FillColor = value?.Index == null ? null : ColorOf(value.Index.Value);
                Changed(nameof(Fill));
            }
        }

        /// <summary>Gets the alignments: Top Left to Bottom Right.</summary>
        public IReadOnlyList<string> Alignments => TableCellViewModel.Alignments;

        /// <summary>Gets or sets the alignment of the cell style shown.</summary>
        public string Alignment
        {
            get => Alignments[(int)Cell.Alignment - 1];
            set
            {
                int at = Alignments.ToList().IndexOf(value);

                if (at >= 0)
                {
                    Cell.Alignment = (DxfCellAlignment)(at + 1);
                    Changed(nameof(Alignment));
                }
            }
        }

        /// <summary>Gets or sets the horizontal margin, as typed: the style's own, alike for every cell style.</summary>
        public string HorizontalMargin
        {
            get => _settings.HorizontalMargin.ToString("0.####", CultureInfo.InvariantCulture);
            set => Number(value, v => v >= 0.0, v => _settings.HorizontalMargin = v, nameof(HorizontalMargin));
        }

        /// <summary>Gets or sets the vertical margin, as typed: the style's own, alike for every cell style.</summary>
        public string VerticalMargin
        {
            get => _settings.VerticalMargin.ToString("0.####", CultureInfo.InvariantCulture);
            set => Number(value, v => v >= 0.0, v => _settings.VerticalMargin = v, nameof(VerticalMargin));
        }

        /// <summary>Gets the text styles of the drawing.</summary>
        public IReadOnlyList<string> TextStyles { get; }

        /// <summary>Gets or sets the text style of the cell style shown.</summary>
        public string TextStyle
        {
            get => TextStyles.FirstOrDefault(s => string.Equals(s, Cell.TextStyle, StringComparison.OrdinalIgnoreCase)) ?? Cell.TextStyle;
            set
            {
                if (value != null)
                {
                    Cell.TextStyle = value;
                    Changed(nameof(TextStyle));
                }
            }
        }

        /// <summary>Gets or sets the text height of the cell style shown, as typed.</summary>
        public string TextHeight
        {
            get => Cell.TextHeight.ToString("0.####", CultureInfo.InvariantCulture);
            set => Number(value, v => v > 0.0, v => Cell.TextHeight = v, nameof(TextHeight));
        }

        /// <summary>Gets the colours of the text and the lines.</summary>
        public IReadOnlyList<TableFillChoice> ColorChoices => Colors;

        /// <summary>Gets or sets the colour of the text of the cell style shown.</summary>
        public TableFillChoice TextColor
        {
            get => Choice(Colors, Cell.TextColor);
            set
            {
                if (value?.Index != null)
                {
                    Cell.TextColor = ColorOf(value.Index.Value);
                    Changed(nameof(TextColor));
                }
            }
        }

        /// <summary>Gets the lineweights of the Borders tab.</summary>
        public IReadOnlyList<string> LineWeightChoices => LineWeights;

        /// <summary>Gets or sets the lineweight the border buttons give.</summary>
        public string BorderWeight
        {
            get => _borderWeight;
            set => Set(ref _borderWeight, LineWeights.Contains(value) ? value : _borderWeight);
        }

        /// <summary>Gets or sets the colour the border buttons give.</summary>
        public TableFillChoice BorderColor
        {
            get => _borderColor;
            set => Set(ref _borderColor, value ?? Colors[0]);
        }

        /// <summary>Gets the six lines of the cell style shown as the preview draws them: whether each is drawn.</summary>
        public IReadOnlyList<bool> Lines => Cell.LinesShown.ToList();

        /// <summary>
        /// Gets a border button, given its name, as the Borders tab's: All, Outside, Inside, Bottom, Left, Top, Right give the
        /// lines the lineweight and colour chosen and draw them; None hides every line.
        /// </summary>
        public DelegateCommand BorderCommand { get; }

        /// <summary>Gets what OK said where it was refused; empty otherwise.</summary>
        public string Message
        {
            get => _message;
            private set => Set(ref _message, value ?? string.Empty);
        }

        /// <summary>Gets OK.</summary>
        public DelegateCommand OkCommand { get; }

        /// <summary>Gets Cancel and the close button.</summary>
        public DelegateCommand CancelCommand { get; }

        private CadTableCellStyleSettings Cell => _settings.Cell(_cellStyle == "Title" ? DxfRowType.TitleRow : _cellStyle == "Header" ? DxfRowType.HeaderRow : DxfRowType.DataRow);

        /// <summary>OK: the style written, one undo step; the dialog closed, or the refusal shown.</summary>
        public void Ok()
        {
            CadEditResult result = CadTableStyleEditor.Apply(_document, _name, _settings);

            if (!result.Succeeded)
            {
                Message = result.Message;
                return;
            }

            CloseRequested?.Invoke(this, true);
        }

        private void Border(string name)
        {
            int[] lines;

            switch (name)
            {
                case "All":
                case "None":
                    lines = new[] { 0, 1, 2, 3, 4, 5 };
                    break;
                case "Outside":
                    lines = new[] { 0, 2, 3, 5 };
                    break;
                case "Inside":
                    lines = new[] { 1, 4 };
                    break;
                case "Top":
                    lines = new[] { 0 };
                    break;
                case "Bottom":
                    lines = new[] { 2 };
                    break;
                case "Left":
                    lines = new[] { 3 };
                    break;
                case "Right":
                    lines = new[] { 5 };
                    break;
                default:
                    return;
            }

            DxfLineWeight weight = Weights[Math.Max(0, LineWeights.ToList().IndexOf(_borderWeight))];

            foreach (int line in lines)
            {
                Cell.LinesShown[line] = name != "None";

                if (name != "None")
                {
                    Cell.LineWeights[line] = weight;
                    Cell.LineColors[line] = ColorOf(_borderColor.Index ?? 0);
                }
            }

            Changed(nameof(Lines));
        }

        private void Number(string text, Func<double, bool> takes, Action<double> write, string property)
        {
            if (double.TryParse((text ?? string.Empty).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double value) && !double.IsInfinity(value) && takes(value))
            {
                write(value);
            }

            Changed(property);
        }

        private void Changed(string property)
        {
            Raise(property);
            Version++;
            Raise(nameof(Version));
        }

        private static TableFillChoice Choice(IReadOnlyList<TableFillChoice> choices, DxfColor color)
        {
            if (color == null)
            {
                return choices.FirstOrDefault(c => c.Index == null);
            }

            return choices.FirstOrDefault(c => c.Index == color.ColorIndex) ?? new TableFillChoice("Color " + color.ColorIndex.ToString(CultureInfo.InvariantCulture), color.ColorIndex);
        }

        private static DxfColor ColorOf(int index) => index == 0 ? DxfColor.ByBlock : index == 256 ? DxfColor.ByLayer : DxfColor.FromColorIndex(DxfColorMethod.ByAci, (short)index);
    }
}
