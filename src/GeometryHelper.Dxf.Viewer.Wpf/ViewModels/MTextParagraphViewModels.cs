using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows.Input;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>A row of the Paragraph dialog's list of tab stops: its kind and its position.</summary>
    public sealed class ParagraphTabStopRow
    {
        internal ParagraphTabStopRow(CadMTextTabStop stop, int decimals)
        {
            Stop = stop;
            Type = ParagraphViewModel.TypeName(stop.Kind);
            Position = stop.Position.ToString("F" + decimals, CultureInfo.InvariantCulture);
        }

        /// <summary>Gets the stop.</summary>
        public CadMTextTabStop Stop { get; }

        /// <summary>Gets the kind as the list shows it: Left, Centre, Right or Decimal.</summary>
        public string Type { get; }

        /// <summary>Gets the position as the list shows it, with the drawing's LUPREC decimals: "45.0000".</summary>
        public string Position { get; }
    }

    /// <summary>
    /// The MTEXT editor's Paragraph dialog, as AutoCAD 2021's: tab stops (Left, Centre, Right, Decimal with its separator),
    /// the first-line and hanging indents, the right indent, and, each ticked to be the paragraph's own, its alignment, its
    /// paragraph spacing and its line spacing. It opens on the selected paragraphs, or the caret's, with the values of the
    /// first; a box whose value differs between them opens empty and is left as each has it unless typed in. OK applies all
    /// as one step of the editor's undo; a value refused names the box and keeps the dialog open.
    /// </summary>
    /// <remarks>
    /// Limits are AutoCAD 2021's own, as its text editor took them: a multiple of line spacing from 0.25x to 4x, a distance of
    /// exactly or at least from 0.25 to 4, a space before or after nought or from 0.25 to 4. Exactly and at least are given
    /// as AutoCAD's paragraph line spacing reads them, 2.5 being 1x in an MTEXT 2.5 high.
    /// </remarks>
    public sealed class ParagraphViewModel : ObservableObject
    {
        private readonly CadMTextEditor _editor;
        private readonly double _room;
        private readonly int _decimals;
        private readonly bool _wasSpacingOn;
        private readonly bool _wasLineSpacingOn;
        private readonly bool _wasAlignmentOn;
        private string _tabType = "Left";
        private string _positionText = string.Empty;
        private ParagraphTabStopRow _selectedStop;
        private string _decimalStyle = "Period";
        private string _firstLineText;
        private string _hangingText;
        private string _rightText;
        private bool _isAlignmentOn;
        private string _alignment;
        private bool _isSpacingOn;
        private string _beforeText;
        private string _afterText;
        private bool _isLineSpacingOn;
        private string _lineSpacingMode;
        private string _lineSpacingText;
        private string _notice = string.Empty;
        private string _faultBox = string.Empty;
        private bool _tabsChanged;

        /// <summary>Opens the dialog on the editor's selected paragraphs, or the caret's.</summary>
        /// <param name="editor">The editor.</param>
        /// <param name="room">How far a tab or an indent may reach: the width of the frame or of a column.</param>
        /// <param name="decimals">The decimals lengths are shown with, the drawing's LUPREC.</param>
        /// <param name="lineSpacing">Whether Paragraph Line Spacing opens ticked, as Line Spacing › More… opens it.</param>
        /// <exception cref="ArgumentNullException">Thrown when the editor is null.</exception>
        public ParagraphViewModel(CadMTextEditor editor, double room, int decimals = 4, bool lineSpacing = false)
        {
            _editor = editor ?? throw new ArgumentNullException(nameof(editor));
            _room = room > 0.0 ? room : double.MaxValue;
            _decimals = Math.Max(0, Math.Min(8, decimals));
            (int first, int last) = editor.SelectedParagraphs;
            CadMTextParagraphFormat[] formats = Enumerable
                .Range(first, last - first + 1)
                .Select(editor.FormatOf)
                .ToArray();
            CadMTextParagraphFormat format = formats[0];
            Stops = new ObservableCollection<ParagraphTabStopRow>(
                format.TabStops.Select(s => new ParagraphTabStopRow(s, _decimals))
            );
            _firstLineText = Shown(formats, f => f.FirstLineIndent);
            _hangingText = Shown(formats, f => f.HangingIndent);
            _rightText = Shown(formats, f => f.RightIndent);
            char own = editor.OwnAlignmentOf(first);
            _wasAlignmentOn = own != '\0';
            _isAlignmentOn = _wasAlignmentOn;
            _alignment = AlignmentName(own == '\0' ? editor.Alignment : own);
            _wasSpacingOn = formats.Any(f => f.SpaceBefore > 0.0 || f.SpaceAfter > 0.0);
            _isSpacingOn = _wasSpacingOn;
            _beforeText = Shown(formats, f => f.SpaceBefore);
            _afterText = Shown(formats, f => f.SpaceAfter);
            _wasLineSpacingOn = formats.Any(f => f.LineSpacing != '\0');
            _isLineSpacingOn = _wasLineSpacingOn || lineSpacing;
            char mode = format.LineSpacing == '\0' ? 'm' : format.LineSpacing;
            _lineSpacingMode = ModeName(mode);
            bool sameSpacing = formats.All(f =>
                f.LineSpacing == format.LineSpacing && Same(f.LineSpacingValue, format.LineSpacingValue)
            );
            _lineSpacingText = sameSpacing ? SpacingText(mode, format.LineSpacingValue) : string.Empty;
            AddCommand = new DelegateCommand(_ => Add(), _ => CanAdd);
            ModifyCommand = new DelegateCommand(_ => Modify(), _ => SelectedStop != null);
            RemoveCommand = new DelegateCommand(_ => Remove(), _ => SelectedStop != null);
        }

        /// <summary>Gets the kinds of tab stop: Left, Centre, Right, Decimal.</summary>
        public IReadOnlyList<string> TabTypes { get; } = new[] { "Left", "Centre", "Right", "Decimal" };

        /// <summary>Gets the separators a decimal stop is set on: Period, Comma, Space.</summary>
        public IReadOnlyList<string> DecimalStyles { get; } = new[] { "Period", "Comma", "Space" };

        /// <summary>Gets the alignments: Left, Centre, Right, Justified, Distributed.</summary>
        public IReadOnlyList<string> Alignments { get; } =
            new[] { "Left", "Centre", "Right", "Justified", "Distributed" };

        /// <summary>Gets the line spacings: Multiple, Exactly, At least.</summary>
        public IReadOnlyList<string> LineSpacingModes { get; } = new[] { "Multiple", "Exactly", "At least" };

        /// <summary>Gets or sets the kind the next Add or Modify gives.</summary>
        public string TabType
        {
            get => _tabType;
            set
            {
                if (Set(ref _tabType, TabTypes.Contains(value) ? value : "Left"))
                {
                    Raise(nameof(IsDecimalStyleEnabled));
                }
            }
        }

        /// <summary>Gets or sets the position typed, in drawing units from the left of the frame or column.</summary>
        public string PositionText
        {
            get => _positionText;
            set
            {
                if (Set(ref _positionText, value ?? string.Empty))
                {
                    Clear("Position");
                    Raise(nameof(CanAdd));
                }
            }
        }

        /// <summary>Gets whether Add can be pressed: the position holds text.</summary>
        public bool CanAdd => _positionText.Trim().Length > 0;

        /// <summary>Gets the tab stops, by position.</summary>
        public ObservableCollection<ParagraphTabStopRow> Stops { get; }

        /// <summary>Gets whether the list is empty, for its words "No tab stops. Type a position and Add, or click the ruler."</summary>
        public bool HasNoStops => Stops.Count == 0;

        /// <summary>Gets or sets the stop selected in the list: it fills the kind and the position.</summary>
        public ParagraphTabStopRow SelectedStop
        {
            get => _selectedStop;
            set
            {
                if (Set(ref _selectedStop, value) && value != null)
                {
                    TabType = TypeName(value.Stop.Kind);
                    _positionText = value.Position;
                    Raise(nameof(PositionText));
                    Raise(nameof(CanAdd));

                    if (value.Stop.Kind == 'd')
                    {
                        DecimalStyle = StyleName(value.Stop.DecimalSeparator);
                    }
                }

                Raise(nameof(IsDecimalStyleEnabled));
            }
        }

        /// <summary>Gets or sets the separator a decimal stop added or modified is set on.</summary>
        public string DecimalStyle
        {
            get => _decimalStyle;
            set => Set(ref _decimalStyle, DecimalStyles.Contains(value) ? value : "Period");
        }

        /// <summary>Gets whether the decimal style can be chosen: Decimal is the kind, or a decimal stop is selected.</summary>
        public bool IsDecimalStyleEnabled => _tabType == "Decimal" || _selectedStop?.Stop.Kind == 'd';

        /// <summary>Gets Add: a stop of the kind at the position; refused at a position taken or out of the frame.</summary>
        public ICommand AddCommand { get; }

        /// <summary>Gets Modify: the selected stop made the kind and position given.</summary>
        public ICommand ModifyCommand { get; }

        /// <summary>Gets Remove: the selected stop taken out.</summary>
        public ICommand RemoveCommand { get; }

        /// <summary>Gets or sets where the first line starts, the ruler's upper triangle; empty where the paragraphs differ.</summary>
        public string FirstLineText
        {
            get => _firstLineText;
            set => Typed(ref _firstLineText, value, "First line");
        }

        /// <summary>Gets or sets where the later lines start, the ruler's lower triangle; empty where the paragraphs differ.</summary>
        public string HangingText
        {
            get => _hangingText;
            set => Typed(ref _hangingText, value, "Hanging");
        }

        /// <summary>Gets or sets how far the lines stop short of the right; empty where the paragraphs differ.</summary>
        public string RightText
        {
            get => _rightText;
            set => Typed(ref _rightText, value, "Right");
        }

        /// <summary>Gets or sets whether the paragraphs carry an alignment of their own.</summary>
        public bool IsAlignmentOn
        {
            get => _isAlignmentOn;
            set => Set(ref _isAlignmentOn, value);
        }

        /// <summary>Gets or sets the alignment of their own: Left, Centre, Right, Justified or Distributed.</summary>
        public string Alignment
        {
            get => _alignment;
            set => Set(ref _alignment, Alignments.Contains(value) ? value : "Left");
        }

        /// <summary>Gets or sets whether the paragraphs carry spacing of their own.</summary>
        public bool IsSpacingOn
        {
            get => _isSpacingOn;
            set => Set(ref _isSpacingOn, value);
        }

        /// <summary>Gets or sets the space before, nought or from 0.25 to 4.</summary>
        public string BeforeText
        {
            get => _beforeText;
            set => Typed(ref _beforeText, value, "Before");
        }

        /// <summary>Gets or sets the space after, nought or from 0.25 to 4.</summary>
        public string AfterText
        {
            get => _afterText;
            set => Typed(ref _afterText, value, "After");
        }

        /// <summary>Gets or sets whether the paragraphs carry a line spacing of their own.</summary>
        public bool IsLineSpacingOn
        {
            get => _isLineSpacingOn;
            set => Set(ref _isLineSpacingOn, value);
        }

        /// <summary>Gets or sets the line spacing: Multiple, Exactly or At least; a change converts the value, 1.0000x to 2.5000 in an MTEXT 2.5 high.</summary>
        public string LineSpacingMode
        {
            get => _lineSpacingMode;
            set
            {
                string mode = LineSpacingModes.Contains(value) ? value : "Multiple";

                if (mode == _lineSpacingMode)
                {
                    return;
                }

                char from = ModeChar(_lineSpacingMode);
                char to = ModeChar(mode);

                if (TryNumber(_lineSpacingText, out double number) && (from == 'm') != (to == 'm'))
                {
                    _lineSpacingText = SpacingText(to, to == 'm' ? number / _editor.Height : number * _editor.Height);
                    Raise(nameof(LineSpacingText));
                }

                _lineSpacingMode = mode;
                Raise(nameof(LineSpacingMode));
            }
        }

        /// <summary>Gets or sets the value of the line spacing: "1.5000x" for a multiple, a distance for the others.</summary>
        public string LineSpacingText
        {
            get => _lineSpacingText;
            set => Typed(ref _lineSpacingText, value, "Line spacing");
        }

        /// <summary>Gets the refusal shown in the footer; empty for none.</summary>
        public string Notice
        {
            get => _notice;
            private set => Set(ref _notice, value);
        }

        /// <summary>Gets the box at fault, drawn with a red border: Position, First line, Hanging, Right, Before, After or Line spacing; empty for none.</summary>
        public string FaultBox
        {
            get => _faultBox;
            private set => Set(ref _faultBox, value);
        }

        /// <summary>Gets whether OK applied the dialog.</summary>
        public bool Applied { get; private set; }

        /// <summary>
        /// OK: checks every box and, where all are taken, applies the dialog to the paragraphs as one step of the editor's
        /// undo; else names the box at fault and applies nothing.
        /// </summary>
        /// <returns>Whether it was applied and the dialog may close.</returns>
        public bool Ok()
        {
            double? first = Indent(_firstLineText, "First line", out bool ok1);
            double? hanging = Indent(_hangingText, "Hanging", out bool ok2);
            double? right = Indent(_rightText, "Right", out bool ok3);

            if (!ok1 || !ok2 || !ok3)
            {
                return false;
            }

            double? before = null;
            double? after = null;

            if (_isSpacingOn)
            {
                before = Spacing(_beforeText, "Before", out bool ok4);
                after = Spacing(_afterText, "After", out bool ok5);

                if (!ok4 || !ok5)
                {
                    return false;
                }
            }

            char mode = ModeChar(_lineSpacingMode);
            double? spacing = null;

            if (_isLineSpacingOn && _lineSpacingText.Trim().Length > 0)
            {
                if (!TryNumber(_lineSpacingText, out double value) || value < 0.25 - 1E-9 || value > 4.0 + 1E-9)
                {
                    return Refuse(
                        "Line spacing",
                        mode == 'm'
                            ? "The line spacing must be from 0.25x to 4.00x."
                            : "The line spacing must be a number from " + Text(0.25) + " to " + Text(4.0) + "."
                    );
                }

                spacing = value;
            }

            CadMTextTabStop[] stops = Stops.Select(r => r.Stop).ToArray();
            bool spacingOn = _isSpacingOn;
            bool lineOn = _isLineSpacingOn;
            bool clearSpacing = !spacingOn && _wasSpacingOn;
            bool clearLine = !lineOn && _wasLineSpacingOn;
            _editor.BeginChange();
            _editor.SetFormat(f =>
            {
                CadMTextParagraphFormat g = f.WithIndents(
                    first ?? f.FirstLineIndent,
                    hanging ?? f.HangingIndent,
                    right ?? f.RightIndent
                );

                if (_tabsChanged)
                {
                    g = g.WithTabStops(stops);
                }

                if (spacingOn)
                {
                    g = g.WithSpacing(before ?? g.SpaceBefore, after ?? g.SpaceAfter);
                }
                else if (clearSpacing)
                {
                    g = g.WithSpacing(0.0, 0.0);
                }

                if (lineOn && spacing.HasValue)
                {
                    g = g.WithLineSpacing(mode, spacing.Value);
                }
                else if (clearLine)
                {
                    g = g.WithLineSpacing('\0', 1.0);
                }

                return g;
            });

            if (_isAlignmentOn)
            {
                _editor.SetAlignment("lcrjd"[Alignments.ToList().IndexOf(_alignment)]);
            }
            else if (_wasAlignmentOn)
            {
                _editor.SetAlignment(_editor.DefaultAlignment);
            }

            _editor.EndChange(true);
            Applied = true;
            return true;
        }

        internal static string TypeName(char kind) =>
            kind == 'c' ? "Centre"
            : kind == 'r' ? "Right"
            : kind == 'd' ? "Decimal"
            : "Left";

        private static string StyleName(char separator) =>
            separator == ',' ? "Comma"
            : separator == ' ' ? "Space"
            : "Period";

        private static string AlignmentName(char alignment) =>
            alignment == 'c' ? "Centre"
            : alignment == 'r' ? "Right"
            : alignment == 'j' ? "Justified"
            : alignment == 'd' ? "Distributed"
            : "Left";

        private static string ModeName(char mode) =>
            mode == 'e' ? "Exactly"
            : mode == 'a' ? "At least"
            : "Multiple";

        private static char ModeChar(string mode) =>
            mode == "Exactly" ? 'e'
            : mode == "At least" ? 'a'
            : 'm';

        private static bool Same(double a, double b) => Math.Abs(a - b) <= Tolerance.Default.EqualPoint;

        private static bool TryNumber(string text, out double value)
        {
            string trimmed = (text ?? string.Empty).Trim().TrimEnd('x', 'X').Trim();
            return double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                && !double.IsNaN(value)
                && !double.IsInfinity(value);
        }

        private string SpacingText(char mode, double value) =>
            mode == 'm' ? value.ToString("F" + _decimals, CultureInfo.InvariantCulture) + "x" : Text(value);

        private string Text(double value) => value.ToString("F" + _decimals, CultureInfo.InvariantCulture);

        private string Shown(CadMTextParagraphFormat[] formats, Func<CadMTextParagraphFormat, double> value)
        {
            double first = value(formats[0]);
            return formats.All(f => Same(value(f), first)) ? Text(first) : string.Empty;
        }

        private void Typed(ref string field, string value, string box)
        {
            if (Set(ref field, value ?? string.Empty))
            {
                Clear(box);
            }
        }

        private void Clear(string box)
        {
            if (_faultBox == box)
            {
                Notice = string.Empty;
                FaultBox = string.Empty;
            }
        }

        private bool Refuse(string box, string notice)
        {
            FaultBox = box;
            Notice = notice;
            return false;
        }

        private double? Indent(string text, string box, out bool ok)
        {
            ok = true;

            if (text.Trim().Length == 0)
            {
                return null;
            }

            if (!TryNumber(text, out double value) || value < 0.0 || value >= _room)
            {
                ok = Refuse(box, "The indent must be a number from 0 to " + Room() + ".");
                return null;
            }

            return value;
        }

        private double? Spacing(string text, string box, out bool ok)
        {
            ok = true;

            if (text.Trim().Length == 0)
            {
                return null;
            }

            // AutoCAD 2021's text editor refuses a space between nought and 0.25, and past 4.
            if (
                !TryNumber(text, out double value)
                || value < 0.0
                || (value > 1E-12 && value < 0.25 - 1E-9)
                || value > 4.0 + 1E-9
            )
            {
                ok = Refuse(
                    box,
                    "The spacing must be nought or a number from " + Text(0.25) + " to " + Text(4.0) + "."
                );
                return null;
            }

            return value;
        }

        private string Room() => _room == double.MaxValue ? "any" : Text(_room);

        private bool Position(out CadMTextTabStop stop)
        {
            stop = null;

            if (!TryNumber(_positionText, out double value) || !(value > 0.0) || value > _room + 1E-9)
            {
                return Refuse("Position", "The tab position must be a number from 0 to " + Room() + ".");
            }

            char kind = "lcrd"[TabTypes.ToList().IndexOf(_tabType)];
            char separator =
                _decimalStyle == "Comma" ? ','
                : _decimalStyle == "Space" ? ' '
                : '.';
            stop = new CadMTextTabStop(value, kind, separator);
            return true;
        }

        private void Add()
        {
            if (!Position(out CadMTextTabStop stop))
            {
                return;
            }

            if (Stops.Any(r => Same(r.Stop.Position, stop.Position)))
            {
                Refuse("Position", "A tab stop is already at " + Text(stop.Position) + ".");
                return;
            }

            Put(stop, null);
        }

        private void Modify()
        {
            if (_selectedStop == null || !Position(out CadMTextTabStop stop))
            {
                return;
            }

            if (Stops.Any(r => r != _selectedStop && Same(r.Stop.Position, stop.Position)))
            {
                Refuse("Position", "A tab stop is already at " + Text(stop.Position) + ".");
                return;
            }

            Put(stop, _selectedStop);
        }

        private void Remove()
        {
            if (_selectedStop == null)
            {
                return;
            }

            Stops.Remove(_selectedStop);
            _tabsChanged = true;
            SelectedStop = null;
            Raise(nameof(HasNoStops));
        }

        private void Put(CadMTextTabStop stop, ParagraphTabStopRow replaced)
        {
            if (replaced != null)
            {
                Stops.Remove(replaced);
            }

            var row = new ParagraphTabStopRow(stop, _decimals);
            int at = 0;

            while (at < Stops.Count && Stops[at].Stop.Position < stop.Position)
            {
                at++;
            }

            Stops.Insert(at, row);
            _tabsChanged = true;
            _selectedStop = row;
            Raise(nameof(SelectedStop));
            Raise(nameof(HasNoStops));
        }
    }

    /// <summary>
    /// The MTEXT editor's Column Settings dialog, as AutoCAD 2021's: the type (dynamic of one height or of their own, static,
    /// none), the number of static columns, the height, and the widths of a column, the gutter and the total, the total
    /// following the others as they are typed. OK gives <see cref="Result"/>, set by the editor as one step of its undo.
    /// </summary>
    public sealed class ColumnSettingsViewModel : ObservableObject
    {
        private readonly CadMTextFrame _frame;
        private readonly double _textHeight;
        private readonly int _laid;
        private readonly int _decimals;
        private string _type;
        private bool _autoHeight;
        private string _countText;
        private string _heightText;
        private string _columnText;
        private string _gutterText;
        private string _totalText;
        private string _notice = string.Empty;
        private string _faultBox = string.Empty;

        /// <summary>Opens the dialog on an MTEXT's box.</summary>
        /// <param name="frame">The box.</param>
        /// <param name="textHeight">The MTEXT's height, of which the gutter is five to start with.</param>
        /// <param name="laidColumns">How many columns the words make now, the number shown for dynamic ones.</param>
        /// <param name="decimals">The decimals lengths are shown with, the drawing's LUPREC.</param>
        /// <exception cref="ArgumentNullException">Thrown when the frame is null.</exception>
        public ColumnSettingsViewModel(CadMTextFrame frame, double textHeight, int laidColumns, int decimals = 4)
        {
            _frame = frame ?? throw new ArgumentNullException(nameof(frame));
            _textHeight = textHeight > 0.0 ? textHeight : 2.5;
            _laid = Math.Max(1, laidColumns);
            _decimals = Math.Max(0, Math.Min(8, decimals));
            _type =
                frame.ColumnType == DxfColumnType.StaticColumns ? "Static"
                : frame.ColumnType == DxfColumnType.DynamicColumns ? "Dynamic"
                : "None";
            _autoHeight = frame.ColumnType != DxfColumnType.DynamicColumns || frame.AutoHeight;
            _countText = (
                frame.ColumnType == DxfColumnType.StaticColumns ? frame.ColumnCount
                : frame.HasColumns ? _laid
                : 1
            ).ToString(CultureInfo.InvariantCulture);
            _heightText = frame.Height > 0.0 ? Text(frame.Height) : string.Empty;
            _columnText = Text(frame.HasColumns ? frame.ColumnWidth : frame.Width);
            _gutterText = Text(frame.HasColumns ? frame.Gutter : 5.0 * _textHeight);
            _totalText = Text(Total());
        }

        /// <summary>Gets or sets the type: Dynamic, Static or None.</summary>
        public string ColumnType
        {
            get => _type;
            set
            {
                string type = value == "Dynamic" || value == "Static" ? value : "None";

                if (type == _type)
                {
                    return;
                }

                // To none the total is the width; from none a column is the width; static starts at two.
                if (type == "None")
                {
                    _columnText = _totalText;
                }
                else if (_type == "None")
                {
                    _columnText = _totalText;
                    _gutterText = Text(5.0 * _textHeight);
                }

                if (type == "Static" && _type != "Static")
                {
                    _countText = "2";
                }
                else if (type == "None")
                {
                    _countText = "1";
                }
                else if (type == "Dynamic")
                {
                    _countText = _laid.ToString(CultureInfo.InvariantCulture);
                }

                _type = type;
                Clear(null);

                if (type != "None")
                {
                    _totalText = Text(Total());
                }

                foreach (
                    string name in new[]
                    {
                        nameof(ColumnType),
                        nameof(CountText),
                        nameof(ColumnText),
                        nameof(GutterText),
                        nameof(TotalText),
                        nameof(HeightText),
                    }
                )
                {
                    Raise(name);
                }

                RaiseEnabled();
            }
        }

        /// <summary>Gets or sets whether dynamic columns are all of one height (Auto Height), else each of its own (Manual Height).</summary>
        public bool AutoHeight
        {
            get => _autoHeight;
            set
            {
                if (Set(ref _autoHeight, value))
                {
                    RaiseEnabled();
                }
            }
        }

        /// <summary>Gets or sets the number of static columns, 2 to 100; the count the words make, greyed, for dynamic ones.</summary>
        public string CountText
        {
            get => _countText;
            set
            {
                if (Set(ref _countText, value ?? string.Empty))
                {
                    Clear("Number");
                    Follow();
                }
            }
        }

        /// <summary>Gets or sets the height of the columns: for Auto Height and Static; empty and greyed for the others.</summary>
        public string HeightText
        {
            get => IsHeightEnabled ? _heightText : string.Empty;
            set
            {
                if (Set(ref _heightText, value ?? string.Empty))
                {
                    Clear("Height");
                }
            }
        }

        /// <summary>Gets or sets the width of each column.</summary>
        public string ColumnText
        {
            get => _columnText;
            set
            {
                if (Set(ref _columnText, value ?? string.Empty))
                {
                    Clear("Column");
                    Follow();
                }
            }
        }

        /// <summary>Gets or sets the gutter.</summary>
        public string GutterText
        {
            get => _gutterText;
            set
            {
                if (Set(ref _gutterText, value ?? string.Empty))
                {
                    Clear("Gutter");
                    Follow();
                }
            }
        }

        /// <summary>Gets or sets the total: column × number + gutter × (number − 1); typed (Static) it sets the column, the gutter and number kept; with none, the MTEXT's width.</summary>
        public string TotalText
        {
            get => _totalText;
            set
            {
                if (!Set(ref _totalText, value ?? string.Empty))
                {
                    return;
                }

                Clear("Total");

                if (
                    _type == "Static"
                    && Number(_totalText, out double total)
                    && Number(_gutterText, out double gutter)
                    && int.TryParse(_countText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int count)
                    && count >= 1
                )
                {
                    _columnText = Text((total - (gutter * (count - 1))) / count);
                    Raise(nameof(ColumnText));
                }
            }
        }

        /// <summary>Gets whether Number can be typed: static columns.</summary>
        public bool IsCountEnabled => _type == "Static";

        /// <summary>Gets whether Height can be typed: dynamic of one height, or static.</summary>
        public bool IsHeightEnabled => (_type == "Dynamic" && _autoHeight) || _type == "Static";

        /// <summary>Gets whether Auto Height and Manual Height can be chosen: dynamic columns.</summary>
        public bool IsHeightModeEnabled => _type == "Dynamic";

        /// <summary>Gets whether Column and Gutter can be typed: in columns.</summary>
        public bool IsColumnEnabled => _type != "None";

        /// <summary>Gets whether Total can be typed: static columns, or none (the MTEXT's width).</summary>
        public bool IsTotalEnabled => _type == "Static" || _type == "None";

        /// <summary>Gets the line under the picture: how the total is made.</summary>
        public string Formula =>
            _type == "None"
                ? "One column: Total is the MTEXT width."
                : "Total = Column × number + Gutter × (number − 1).";

        /// <summary>Gets the note of the type.</summary>
        public string TypeNote =>
            _type == "Static" ? "Static: the number is fixed; text past the last column runs below it."
            : _type == "None" ? "Number, Height, Column and Gutter greyed."
            : !_autoHeight ? "Each column keeps its own height, set by its grip."
            : "Columns are added and removed as the text flows.";

        /// <summary>Gets the refusal shown in the footer; empty for none.</summary>
        public string Notice
        {
            get => _notice;
            private set => Set(ref _notice, value);
        }

        /// <summary>Gets the box at fault: Number, Height, Column, Gutter or Total; empty for none.</summary>
        public string FaultBox
        {
            get => _faultBox;
            private set => Set(ref _faultBox, value);
        }

        /// <summary>Gets the box OK gave; null before it.</summary>
        public CadMTextFrame Result { get; private set; }

        /// <summary>OK: checks the boxes of the type and gives <see cref="Result"/>; else names the box at fault.</summary>
        /// <returns>Whether the dialog may close.</returns>
        public bool Ok()
        {
            if (_type == "None")
            {
                if (!Number(_totalText, out double width) || !(width > 0.0))
                {
                    return Refuse("Total", "The width must be a number above nought.");
                }

                Result = _frame.WithColumns(DxfColumnType.NoColumns, width, 0.0, 1, false);
                return true;
            }

            int count = 1;

            if (
                _type == "Static"
                && (
                    !int.TryParse(_countText.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out count)
                    || count < 2
                    || count > 100
                )
            )
            {
                return Refuse("Number", "The column number must be a whole number from 2 to 100.");
            }

            double height = 0.0;

            if (IsHeightEnabled && (!Number(_heightText, out height) || !(height > 0.0)))
            {
                return Refuse("Height", "The height must be a number above nought.");
            }

            if (!Number(_columnText, out double column) || !(column > 0.0))
            {
                return Refuse("Column", "The width must be a number above nought.");
            }

            if (!Number(_gutterText, out double gutter) || gutter < 0.0)
            {
                return Refuse("Gutter", "The gutter must be a number, nought or above.");
            }

            DxfColumnType type = _type == "Static" ? DxfColumnType.StaticColumns : DxfColumnType.DynamicColumns;
            CadMTextFrame frame = _frame.WithColumns(
                type,
                column,
                gutter,
                _type == "Static" ? count : _laid,
                _autoHeight
            );
            Result = IsHeightEnabled ? frame.WithHeight(height) : frame;
            return true;
        }

        private static bool Number(string text, out double value) =>
            double.TryParse((text ?? string.Empty).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value)
            && !double.IsNaN(value)
            && !double.IsInfinity(value);

        private string Text(double value) => value.ToString("F" + _decimals, CultureInfo.InvariantCulture);

        private double Total()
        {
            if (_type == "None")
            {
                return Number(_columnText, out double width) ? width : _frame.Width;
            }

            int count =
                int.TryParse(_countText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) && n >= 1
                    ? n
                    : 1;
            Number(_columnText, out double column);
            Number(_gutterText, out double gutter);
            return (count * column) + ((count - 1) * gutter);
        }

        private void Follow()
        {
            if (_type != "None")
            {
                _totalText = Text(Total());
                Raise(nameof(TotalText));
            }
        }

        private void RaiseEnabled()
        {
            foreach (
                string name in new[]
                {
                    nameof(IsCountEnabled),
                    nameof(IsHeightEnabled),
                    nameof(IsHeightModeEnabled),
                    nameof(IsColumnEnabled),
                    nameof(IsTotalEnabled),
                    nameof(HeightText),
                    nameof(Formula),
                    nameof(TypeNote),
                }
            )
            {
                Raise(name);
            }
        }

        private void Clear(string box)
        {
            if (box == null || _faultBox == box)
            {
                Notice = string.Empty;
                FaultBox = string.Empty;
            }
        }

        private bool Refuse(string box, string notice)
        {
            FaultBox = box;
            Notice = notice;
            return false;
        }
    }

    /// <summary>
    /// The ruler's Set Mtext Width and Set Mtext Height, as AutoCAD 2021's: one box, a width above nought, a height of
    /// nought (none) or more; with columns the column's width and height.
    /// </summary>
    public sealed class MTextSizeViewModel : ObservableObject
    {
        private readonly int _decimals;
        private string _text;
        private string _notice = string.Empty;

        /// <summary>Makes the question.</summary>
        /// <param name="isHeight">Whether the height is asked, else the width.</param>
        /// <param name="value">The value it opens with.</param>
        /// <param name="decimals">The decimals lengths are shown with, the drawing's LUPREC.</param>
        public MTextSizeViewModel(bool isHeight, double value, int decimals = 4)
        {
            IsHeight = isHeight;
            _decimals = Math.Max(0, Math.Min(8, decimals));
            _text = value.ToString("F" + _decimals, CultureInfo.InvariantCulture);
            Value = value;
        }

        /// <summary>Gets whether the height is asked.</summary>
        public bool IsHeight { get; }

        /// <summary>Gets the title: Set Mtext Width or Set Mtext Height.</summary>
        public string Title => IsHeight ? "Set Mtext Height" : "Set Mtext Width";

        /// <summary>Gets the label of the box: Width: or Height:.</summary>
        public string Label => IsHeight ? "Height:" : "Width:";

        /// <summary>Gets or sets the value typed.</summary>
        public string Text
        {
            get => _text;
            set
            {
                if (Set(ref _text, value ?? string.Empty))
                {
                    Notice = string.Empty;
                }
            }
        }

        /// <summary>Gets the refusal; empty for none.</summary>
        public string Notice
        {
            get => _notice;
            private set => Set(ref _notice, value);
        }

        /// <summary>Gets the value OK took.</summary>
        public double Value { get; private set; }

        /// <summary>OK: takes the value, or refuses it.</summary>
        /// <returns>Whether the dialog may close.</returns>
        public bool Ok()
        {
            bool number = double.TryParse(
                (_text ?? string.Empty).Trim(),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out double value
            );

            if (!number || double.IsNaN(value) || double.IsInfinity(value) || (IsHeight ? value < 0.0 : !(value > 0.0)))
            {
                Notice = IsHeight
                    ? "The height must be a number, nought or above."
                    : "The width must be a number above nought.";
                return false;
            }

            Value = value;
            return true;
        }
    }
}
