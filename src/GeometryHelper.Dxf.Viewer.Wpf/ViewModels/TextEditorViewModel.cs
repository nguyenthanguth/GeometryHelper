using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Input;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>An item of a menu of the Text Editor tab: a colour, a symbol, a justification or a line spacing.</summary>
    public sealed class TextEditorChoice
    {
        internal TextEditorChoice(string name, string detail, object value, int rgb = -1)
        {
            Name = name;
            Detail = detail;
            Value = value;
            Rgb = rgb;
        }

        /// <summary>Gets the name shown: "Degree", "Red", "Middle Center MC", "1.5x".</summary>
        public string Name { get; }

        /// <summary>Gets what is shown at the right: the code a symbol is written as (<c>%%d</c>); empty for none.</summary>
        public string Detail { get; }

        /// <summary>Gets the value chosen: the colour index, the symbol, the attachment or the factor.</summary>
        public object Value { get; }

        /// <summary>Gets the colour of a colour's swatch as 0xRRGGBB; -1 for no swatch.</summary>
        public int Rgb { get; }

        /// <inheritdoc/>
        public override string ToString() => Name;
    }

    /// <summary>
    /// The MTEXT editor of the window: the words being edited (<see cref="CadMTextEditor"/>) with the frame's width,
    /// justification and line spacing, and the contextual Text Editor tab as the design draws it: Style (the style, Annotative
    /// greyed, the height), Formatting (bold, italic, strikethrough, underline, overline, the font, the colour, upper and lower
    /// case, clear formatting, oblique, tracking and width), Paragraph (justification, the paragraphs' line spacing with
    /// More… and Clear Paragraph Spacing, the five alignments, the launcher of the Paragraph dialog, bullets and numbering),
    /// Insert (Columns with Column Settings, the Symbol menu; field greyed), Spell Check greyed, Tools (Find and
    /// Replace), Options (ruler, undo, redo) and Close Text Editor. The ruler's drags, the dialogs and the menus are each
    /// one step of the editor's undo. Close writes the words in AutoCAD's codes and the box (width, height, columns) as the
    /// answer of the prompt that opened the editor, one undo step of the drawing; Esc asks whether to keep them.
    /// </summary>
    public sealed class TextEditorViewModel : ObservableObject
    {
        // The icons of Bullets and Numbering, as the design draws them on a grid of 16.
        private const string NumberedIcon = "M2 3.5h1.5v4M2 7.5h3M2 10h3l-3 3.5h3M7 5h7M7 12h7";
        private const string LowercaseIcon = "M4.5 7.5a1.5 1.5 0 1 0 0.01 0M6 6v3M7.5 5h6.5M7.5 12h6.5";
        private const string UppercaseIcon = "M2 9.5 3.75 4 5.5 9.5M2.6 8h2.3M7.5 5h6.5M7.5 12h6.5";

        private readonly MainViewModel _model;
        private bool _isRulerShown = true;
        private bool _isFindShown;
        private string _find = string.Empty;
        private string _replace = string.Empty;
        private bool _matchCase;
        private string _findMessage = string.Empty;
        private int _attachment;
        private double _lineSpacing;
        private bool _boxChanged;

        internal TextEditorViewModel(
            MainViewModel model,
            CadInPlaceEdit edit,
            string styleFont,
            bool styleIsShx,
            double lineSpacing
        )
        {
            _model = model;
            Edit = edit;
            _attachment = edit.Attachment;
            _lineSpacing = lineSpacing > 0.0 ? lineSpacing : 1.0;
            StyleFont = styleFont;
            StyleIsShx = styleIsShx;
            int column = (edit.Attachment - 1) % 3;

            // The box of the MTEXT edited, its columns kept; of an MTEXT being made, the columns MTEXT's Columns option gave or
            // the edit's width; of a table cell, the edit's width.
            CadMTextFrame frame =
                edit.Handle != null && !edit.IsTableCell && model?.Document?.FindEntity(edit.Handle) is DxfMText mtext
                    ? CadMTextFrame.Of(mtext)
                    : edit.Frame ?? CadMTextFrame.Of(Math.Max(0.0, edit.Width));
            Editor = new CadMTextEditor(
                edit.Value,
                edit.Height,
                frame,
                model?.StyleWidthOf(edit.StyleName) ?? 1.0,
                styleFont,
                styleIsShx,
                "lcr"[column]
            );
            Editor.Changed += (s, e) => Refresh();
            Fonts = new[] { styleFont ?? "Arial" }
                .Concat(CadTrueTypeFonts.Families)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            Colors = new[]
            {
                Color("ByLayer", 256, -1),
                Color("ByBlock", 0, -1),
                Color("Red", 1),
                Color("Yellow", 2),
                Color("Green", 3),
                Color("Cyan", 4),
                Color("Blue", 5),
                Color("Magenta", 6),
                Color("White", 7),
            };
            Symbols = new[]
            {
                new TextEditorChoice("Degree", "%%d", new[] { "°", "%%d" }),
                new TextEditorChoice("Plus/Minus", "%%p", new[] { "±", "%%p" }),
                new TextEditorChoice("Diameter", "%%c", new[] { "Ø", "%%c" }),
                new TextEditorChoice("Almost Equal", "\\U+2248", new[] { "≈", "\\U+2248" }),
                new TextEditorChoice("Angle", "\\U+2220", new[] { "∠", "\\U+2220" }),
                new TextEditorChoice("Centre Line", "\\U+2104", new[] { "℄", "\\U+2104" }),
                new TextEditorChoice("Delta", "\\U+0394", new[] { "Δ", "\\U+0394" }),
                new TextEditorChoice("Not Equal", "\\U+2260", new[] { "≠", "\\U+2260" }),
                new TextEditorChoice("Square", "\\U+00B2", new[] { "²", "\\U+00B2" }),
                new TextEditorChoice("Non-breaking Space", "Ctrl+Shift+Space", new[] { " ", null }),
            };
            Justifications = new[]
            {
                new TextEditorChoice("Top Left TL", string.Empty, 1),
                new TextEditorChoice("Top Center TC", string.Empty, 2),
                new TextEditorChoice("Top Right TR", string.Empty, 3),
                new TextEditorChoice("Middle Left ML", string.Empty, 4),
                new TextEditorChoice("Middle Center MC", string.Empty, 5),
                new TextEditorChoice("Middle Right MR", string.Empty, 6),
                new TextEditorChoice("Bottom Left BL", string.Empty, 7),
                new TextEditorChoice("Bottom Center BC", string.Empty, 8),
                new TextEditorChoice("Bottom Right BR", string.Empty, 9),
            };
            LineSpacings = new[] { 1.0, 1.5, 2.0, 2.5 }
                .Select(f => new TextEditorChoice(
                    f.ToString("0.0", CultureInfo.InvariantCulture) + "x",
                    string.Empty,
                    f
                ))
                .ToList();

            ToggleCommand = new DelegateCommand(p => Toggle(p as string));
            CaseCommand = new DelegateCommand(p =>
                Editor.ChangeCase(string.Equals(p as string, "Upper", StringComparison.Ordinal))
            );
            ClearCommand = new DelegateCommand(_ => Editor.ClearFormatting());
            AlignCommand = new DelegateCommand(p => Editor.SetAlignment(((string)p)[0]));
            SymbolCommand = new DelegateCommand(p => InsertSymbol(p as TextEditorChoice));
            JustifyCommand = new DelegateCommand(p => Justify(p as TextEditorChoice));
            LineSpacingCommand = new DelegateCommand(p => SetLineSpacing(p as TextEditorChoice));
            FindNextCommand = new DelegateCommand(_ =>
                FindMessage = Editor.FindNext(_find, _matchCase) ? string.Empty : "\"" + _find + "\" was not found."
            );
            ReplaceAllCommand = new DelegateCommand(_ =>
                FindMessage =
                    Editor.ReplaceAll(_find, _replace, _matchCase).ToString(CultureInfo.InvariantCulture) + " replaced."
            );
            UndoCommand = new DelegateCommand(_ => Editor.Undo());
            RedoCommand = new DelegateCommand(_ => Editor.Redo());
            CloseCommand = new DelegateCommand(_ => _model.CloseTextEditor(true));
        }

        /// <summary>Gets the words being edited.</summary>
        public CadMTextEditor Editor { get; }

        /// <summary>Gets the MTEXT the editor is over, as the prompt that opened it gives it.</summary>
        public CadInPlaceEdit Edit { get; }

        /// <summary>Gets the font of the MTEXT's style: a TrueType family or an SHX file.</summary>
        public string StyleFont { get; }

        /// <summary>Gets whether the style's font is an SHX file.</summary>
        public bool StyleIsShx { get; }

        /// <summary>Gets the name of the MTEXT's text style, the Style group's.</summary>
        public string StyleName => Edit.StyleName ?? "Standard";

        /// <summary>
        /// Gets or sets the frame's width in drawing units, which the arrows at the ruler's right end drag, one step of the
        /// editor's undo: the MTEXT's, the total of static columns, a dynamic column's; nought for none.
        /// </summary>
        public double Width
        {
            get => Editor.Frame.Width;
            set
            {
                if (value > 0.0 && !double.IsInfinity(value))
                {
                    Editor.SetFrame(Editor.Frame.WithWidth(value));
                }
                else if (!Editor.Frame.HasColumns)
                {
                    Editor.SetFrame(CadMTextFrame.Of(0.0, Editor.Frame.Height));
                }
            }
        }

        /// <summary>Gets the justification: the attachment, 1 top left to 9 bottom right.</summary>
        public int Attachment => _attachment;

        /// <summary>Gets the line spacing factor.</summary>
        public double LineSpacing => _lineSpacing;

        /// <summary>Gets or sets the height of the letters typed next or selected, as the Style group shows it.</summary>
        public string HeightText
        {
            get => Number(Editor.CurrentLook.Height ?? Editor.Height);
            set
            {
                if (TryNumber(value, out double height) && height > 0.0)
                {
                    Editor.SetHeight(height);
                }

                Raise(nameof(HeightText));
            }
        }

        /// <summary>Gets whether the letters are bold.</summary>
        public bool IsBold => Editor.CurrentLook.Bold;

        /// <summary>Gets whether the letters are italic.</summary>
        public bool IsItalic => Editor.CurrentLook.Italic;

        /// <summary>Gets whether the letters are struck through.</summary>
        public bool IsStrikethrough => Editor.CurrentLook.Strikethrough;

        /// <summary>Gets whether the letters are underlined.</summary>
        public bool IsUnderline => Editor.CurrentLook.Underline;

        /// <summary>Gets whether the letters are overlined.</summary>
        public bool IsOverline => Editor.CurrentLook.Overline;

        /// <summary>Gets whether bold and italic can be set: the font of the letters is a TrueType font.</summary>
        public bool CanBold => Editor.CanBold;

        /// <summary>Gets the fonts offered: the style's first, then the TrueType families of the machine.</summary>
        public IReadOnlyList<string> Fonts { get; }

        /// <summary>Gets or sets the font of the letters, as the font box shows it.</summary>
        public string Font
        {
            get => Editor.CurrentLook.Font ?? StyleFont ?? "Arial";
            set
            {
                if (!string.IsNullOrEmpty(value) && !string.Equals(value, Font, StringComparison.OrdinalIgnoreCase))
                {
                    bool style = string.Equals(value, StyleFont, StringComparison.OrdinalIgnoreCase);
                    Editor.SetFont(style ? null : value, false);
                }
            }
        }

        /// <summary>Gets whether the font shown is an SHX file, as the SHX tag before it says.</summary>
        public bool IsShxFont => Editor.CurrentLook.Font != null ? Editor.CurrentLook.IsShx : StyleIsShx;

        /// <summary>Gets the colours offered: ByLayer, ByBlock and the seven standard colours.</summary>
        public IReadOnlyList<TextEditorChoice> Colors { get; }

        /// <summary>Gets or sets the colour of the letters; ByLayer where they have none of their own.</summary>
        public TextEditorChoice SelectedColor
        {
            get
            {
                int index = Editor.CurrentLook.ColorIndex ?? 256;
                return Colors.FirstOrDefault(c => (int)c.Value == index) ?? Colors[0];
            }
            set
            {
                if (value != null && (int)value.Value != (Editor.CurrentLook.ColorIndex ?? 256))
                {
                    Editor.SetColorIndex((int)value.Value);
                }
            }
        }

        /// <summary>Gets or sets the slant of the letters in degrees, the Oblique box.</summary>
        public string ObliqueText
        {
            get => Number(Editor.CurrentLook.Oblique ?? 0.0);
            set
            {
                if (TryNumber(value, out double degrees) && Math.Abs(degrees) <= 85.0)
                {
                    Editor.SetOblique(degrees);
                }

                Raise(nameof(ObliqueText));
            }
        }

        /// <summary>Gets or sets the spacing of the letters, the Tracking box.</summary>
        public string TrackingText
        {
            get => Number(Editor.CurrentLook.Tracking ?? 1.0, "0.0###");
            set
            {
                if (TryNumber(value, out double factor) && factor >= 0.75 && factor <= 4.0)
                {
                    Editor.SetTracking(factor);
                }

                Raise(nameof(TrackingText));
            }
        }

        /// <summary>Gets or sets the width factor of the letters, the Width box.</summary>
        public string WidthFactorText
        {
            get => Number(Editor.CurrentLook.WidthFactor ?? 1.0, "0.0###");
            set
            {
                if (TryNumber(value, out double factor) && factor > 0.0 && factor <= 100.0)
                {
                    Editor.SetWidthFactor(factor);
                }

                Raise(nameof(WidthFactorText));
            }
        }

        /// <summary>Gets the alignment of the paragraph of the caret: l, c, r, j or d.</summary>
        public string Alignment => Editor.Alignment.ToString();

        /// <summary>Gets whether the paragraph of the caret is aligned left.</summary>
        public bool IsAlignedLeft => Editor.Alignment == 'l';

        /// <summary>Gets whether the paragraph of the caret is centred.</summary>
        public bool IsAlignedCentre => Editor.Alignment == 'c';

        /// <summary>Gets whether the paragraph of the caret is aligned right.</summary>
        public bool IsAlignedRight => Editor.Alignment == 'r';

        /// <summary>Gets whether the paragraph of the caret is justified.</summary>
        public bool IsJustified => Editor.Alignment == 'j';

        /// <summary>Gets whether the paragraph of the caret is distributed.</summary>
        public bool IsDistributed => Editor.Alignment == 'd';

        /// <summary>Gets the symbols of the Symbol menu, each with the code it is written as.</summary>
        public IReadOnlyList<TextEditorChoice> Symbols { get; }

        /// <summary>Gets the justifications of the Justification menu, top left to bottom right.</summary>
        public IReadOnlyList<TextEditorChoice> Justifications { get; }

        /// <summary>Gets the factors of the Line Spacing menu.</summary>
        public IReadOnlyList<TextEditorChoice> LineSpacings { get; }

        /// <summary>Gets or sets whether the ruler is shown above the words.</summary>
        public bool IsRulerShown
        {
            get => _isRulerShown;
            set => Set(ref _isRulerShown, value);
        }

        /// <summary>Gets or sets whether Find and Replace is open.</summary>
        public bool IsFindShown
        {
            get => _isFindShown;
            set => Set(ref _isFindShown, value);
        }

        /// <summary>Gets or sets the words Find and Replace looks for.</summary>
        public string FindText
        {
            get => _find;
            set => Set(ref _find, value ?? string.Empty);
        }

        /// <summary>Gets or sets what Replace All puts in their place.</summary>
        public string ReplaceText
        {
            get => _replace;
            set => Set(ref _replace, value ?? string.Empty);
        }

        /// <summary>Gets or sets whether capitals must match.</summary>
        public bool MatchCase
        {
            get => _matchCase;
            set => Set(ref _matchCase, value);
        }

        /// <summary>Gets what Find and Replace last found or replaced.</summary>
        public string FindMessage
        {
            get => _findMessage;
            private set => Set(ref _findMessage, value);
        }

        /// <summary>Gets whether the last change can be taken back.</summary>
        public bool CanUndo => Editor.CanUndo;

        /// <summary>Gets whether a change taken back can be made again.</summary>
        public bool CanRedo => Editor.CanRedo;

        /// <summary>Gets the command of B, I, S, U and O: Bold, Italic, Strikethrough, Underline, Overline.</summary>
        public ICommand ToggleCommand { get; }

        /// <summary>Gets the command of the aA menu: Upper or Lower.</summary>
        public ICommand CaseCommand { get; }

        /// <summary>Gets the command of Clear: the codes of the letters selected taken off.</summary>
        public ICommand ClearCommand { get; }

        /// <summary>Gets the command of the five alignments: l, c, r, j or d.</summary>
        public ICommand AlignCommand { get; }

        /// <summary>Gets the command of a symbol of the Symbol menu.</summary>
        public ICommand SymbolCommand { get; }

        /// <summary>Gets the command of a justification of the Justification menu.</summary>
        public ICommand JustifyCommand { get; }

        /// <summary>Gets the command of a factor of the Line Spacing menu: the selected paragraphs, or the caret's, spaced at that multiple of their own (<c>\pxsm1.5;</c>), as AutoCAD 2021 sets them.</summary>
        public ICommand LineSpacingCommand { get; }

        /// <summary>Gets Find and Replace's Find Next.</summary>
        public ICommand FindNextCommand { get; }

        /// <summary>Gets Find and Replace's Replace All.</summary>
        public ICommand ReplaceAllCommand { get; }

        /// <summary>Gets the editor's Undo.</summary>
        public ICommand UndoCommand { get; }

        /// <summary>Gets the editor's Redo.</summary>
        public ICommand RedoCommand { get; }

        /// <summary>Gets Close Text Editor: the words kept.</summary>
        public ICommand CloseCommand { get; }

        /// <summary>Gets whether anything was changed: the words, the width, the justification or the line spacing.</summary>
        public bool IsModified => Editor.IsModified || _boxChanged || !Editor.Frame.Equals(Editor.OriginalFrame);

        /// <summary>Gets the Paragraph panel's launcher, Line Spacing › More… and the editor's right-click › Paragraph…: the Paragraph dialog.</summary>
        public ICommand ParagraphCommand => new DelegateCommand(_ => ShowParagraph(false));

        /// <summary>
        /// Opens the Paragraph dialog over the selected paragraphs, or the caret's, where the window shows it
        /// (<see cref="MainViewModel.ShowParagraph"/>); OK applies it as one step of the editor's undo.
        /// </summary>
        /// <param name="lineSpacing">Whether it opens with Paragraph Line Spacing ticked, as Line Spacing › More… opens it.</param>
        /// <returns>Whether OK was pressed.</returns>
        public bool ShowParagraph(bool lineSpacing)
        {
            if (Edit.IsTableCell || _model?.ShowParagraph == null)
            {
                return false;
            }

            var dialog = new ParagraphViewModel(Editor, ColumnRoom(), Decimals, lineSpacing);
            return _model.ShowParagraph(dialog) && dialog.Applied;
        }

        /// <summary>Opens Column Settings over the MTEXT where the window shows it (<see cref="MainViewModel.ShowColumnSettings"/>); OK sets the columns as one step of the editor's undo.</summary>
        /// <returns>Whether OK was pressed.</returns>
        public bool ShowColumnSettings()
        {
            if (Edit.IsTableCell || _model?.ShowColumnSettings == null)
            {
                return false;
            }

            var dialog = new ColumnSettingsViewModel(Editor.Frame, Editor.Height, LaidColumns(), Decimals);

            if (!_model.ShowColumnSettings(dialog) || dialog.Result == null)
            {
                return false;
            }

            Editor.SetFrame(Fitted(dialog.Result));
            return true;
        }

        /// <summary>The ruler's right-click: Set Mtext Width or Set Mtext Height, each one step of the editor's undo; with columns both act on every column.</summary>
        /// <param name="height">Whether the height is asked, else the width.</param>
        /// <returns>Whether a value was set.</returns>
        public bool AskSize(bool height)
        {
            if (_model?.AskMTextSize == null)
            {
                return false;
            }

            CadMTextFrame frame = Editor.Frame;
            double value =
                height ? (frame.ColumnHeights.Count > 0 ? frame.ColumnHeights[0] : frame.Height)
                : frame.HasColumns ? frame.ColumnWidth
                : frame.Width;
            var dialog = new MTextSizeViewModel(height, value, Decimals);

            if (!_model.AskMTextSize(dialog))
            {
                return false;
            }

            Editor.SetFrame(
                height ? frame.WithHeight(dialog.Value)
                : frame.HasColumns ? frame.WithColumnWidth(dialog.Value)
                : frame.WithWidth(dialog.Value)
            );
            return true;
        }

        /// <summary>Sets the columns of the Columns menu, one step of the editor's undo: none, dynamic of one height or of their own, static of a count.</summary>
        /// <param name="type">The columns.</param>
        /// <param name="count">How many static columns.</param>
        /// <param name="autoHeight">Whether dynamic columns are all of one height.</param>
        public void SetColumns(DxfColumnType type, int count = 2, bool autoHeight = true)
        {
            CadMTextFrame frame = Editor.Frame;
            double gutter = frame.HasColumns ? frame.Gutter : 5.0 * Editor.Height;

            if (type == DxfColumnType.NoColumns)
            {
                Editor.SetFrame(frame.WithColumns(DxfColumnType.NoColumns, frame.Width, 0.0, 1, false));
                return;
            }

            double total = frame.HasColumns
                ? (
                    frame.ColumnType == DxfColumnType.StaticColumns
                        ? frame.Width
                        : frame.ColumnWidth * Math.Max(1, LaidColumns()) + gutter * (Math.Max(1, LaidColumns()) - 1)
                )
                : frame.Width;

            if (!(total > 0.0))
            {
                total = Math.Max(Editor.Height, ActualSize()[0]);
            }

            if (type == DxfColumnType.StaticColumns)
            {
                double column = (total - ((count - 1) * gutter)) / count;

                if (!(column > 0.0))
                {
                    gutter = 0.0;
                    column = total / count;
                }

                Editor.SetFrame(Fitted(frame.WithColumns(type, column, gutter, count, false), count));
                return;
            }

            double width = frame.HasColumns ? frame.ColumnWidth : total;
            Editor.SetFrame(Fitted(frame.WithColumns(type, width, gutter, Math.Max(1, LaidColumns()), autoHeight), 1));
        }

        /// <summary>The Columns menu of the Insert panel and the editor's right-click, the MTEXT's type and height mode or count ticked.</summary>
        private IReadOnlyList<CadMenuItemViewModel> ColumnsMenu()
        {
            CadMTextFrame frame = Editor.Frame;
            bool dynamic = frame.ColumnType == DxfColumnType.DynamicColumns;
            bool fixedCount = frame.ColumnType == DxfColumnType.StaticColumns;
            return new[]
            {
                new CadMenuItemViewModel(
                    "No Columns",
                    () => SetColumns(DxfColumnType.NoColumns),
                    true,
                    null,
                    !frame.HasColumns
                ),
                new CadMenuItemViewModel(
                    "Dynamic Columns",
                    new[]
                    {
                        new CadMenuItemViewModel(
                            "Auto Height",
                            () => SetColumns(DxfColumnType.DynamicColumns, 2, true),
                            true,
                            null,
                            dynamic && frame.AutoHeight
                        ),
                        new CadMenuItemViewModel(
                            "Manual Height",
                            () => SetColumns(DxfColumnType.DynamicColumns, 2, false),
                            true,
                            null,
                            dynamic && !frame.AutoHeight
                        ),
                    }
                ),
                new CadMenuItemViewModel(
                    "Static Columns",
                    Enumerable
                        .Range(2, 5)
                        .Select(n => new CadMenuItemViewModel(
                            n.ToString(CultureInfo.InvariantCulture),
                            () => SetColumns(DxfColumnType.StaticColumns, n),
                            true,
                            null,
                            fixedCount && frame.ColumnCount == n
                        ))
                        .ToList()
                ),
                new CadMenuItemViewModel(string.Empty, null),
                new CadMenuItemViewModel(
                    "Insert Column Break",
                    () => Editor.InsertColumnBreak(),
                    frame.HasColumns,
                    null,
                    null,
                    CadSnapKind.None,
                    "Alt+Enter"
                ),
                new CadMenuItemViewModel(string.Empty, null),
                new CadMenuItemViewModel("Column Settings…", () => ShowColumnSettings()),
            };
        }

        /// <summary>A frame of columns given a height where it has none: the words' height shared over the columns, as they then flow.</summary>
        private CadMTextFrame Fitted(CadMTextFrame frame, int over = 1)
        {
            if (!frame.HasColumns || frame.Height > 0.0)
            {
                return frame;
            }

            double high = Math.Max(Editor.Height * 5.0 / 3.0, ActualSize()[1] / Math.Max(1, over));
            return frame.WithHeight(Math.Ceiling(high / Editor.Height) * Editor.Height);
        }

        /// <summary>The width a tab or an indent may reach: a column's, or the MTEXT's; the widest line's where it wraps at none.</summary>
        private double ColumnRoom()
        {
            CadMTextFrame frame = Editor.Frame;
            double room = frame.HasColumns ? frame.ColumnWidth : frame.Width;
            return room > 0.0 ? room : ActualSize()[0];
        }

        /// <summary>The width and height the words take now, laid out as the canvas lays them; nought without a drawing.</summary>
        private double[] ActualSize()
        {
            CadMTextEditorLayout layout = Laid();

            if (layout == null)
            {
                return new double[2];
            }

            double[] box = layout.Box;
            return new[] { box[2] - box[0], box[3] - box[1] };
        }

        /// <summary>How many columns the words stand in now.</summary>
        private int LaidColumns() => Laid()?.ColumnCount ?? Math.Max(1, Editor.Frame.ColumnCount);

        private CadMTextEditorLayout Laid()
        {
            CadDocument document = _model?.Document;

            if (document == null)
            {
                return null;
            }

            var layout = new CadMTextEditorLayout(document, Edit);
            layout.Update(Editor, _attachment, _lineSpacing, 0xFFFFFF, 0xFFFFFF);
            return layout;
        }

        /// <summary>The decimals lengths are shown with: the drawing's LUPREC, 4 without a drawing.</summary>
        internal int Decimals => Math.Max(0, Math.Min(8, _model?.Document?.Database.Luprec ?? 4));

        /// <summary>The formats of the selected paragraphs, or the caret's.</summary>
        private CadMTextParagraphFormat[] SelectedFormats()
        {
            (int first, int last) = Editor.SelectedParagraphs;
            return Enumerable.Range(first, last - first + 1).Select(Editor.FormatOf).ToArray();
        }

        /// <summary>
        /// Gets the items of a menu of the tab: Symbol (each symbol with its code, Other greyed), Justification and Line Spacing
        /// (the one set ticked), Case (UPPERCASE, lowercase), Clear (Remove Character Formatting), Bullets (Off, Numbered,
        /// Lettered and Bulleted, the labels every selected paragraph has ticked; Start, Continue and the two Allow items grey),
        /// Editor (the editor's right-click, its Bullets and Lists the same items with no list ticked).
        /// </summary>
        /// <param name="name">Symbol, Justification, LineSpacing, Columns, Editor, Ruler, Case, Clear or Bullets.</param>
        /// <returns>The items; empty for a menu of no such name.</returns>
        public IReadOnlyList<CadMenuItemViewModel> Menu(string name)
        {
            switch (name)
            {
                case "Symbol":
                    return Symbols
                        .Select(s => new CadMenuItemViewModel(s.Name + "    " + s.Detail, () => InsertSymbol(s)))
                        .Concat(
                            new[]
                            {
                                new CadMenuItemViewModel(string.Empty, null),
                                new CadMenuItemViewModel(
                                    "Other…",
                                    () => { },
                                    false,
                                    "The character map is not built yet"
                                ),
                            }
                        )
                        .ToList();
                case "Justification":
                    return Justifications
                        .Select(j => new CadMenuItemViewModel(
                            j.Name,
                            () => Justify(j),
                            true,
                            null,
                            (int)j.Value == _attachment
                        ))
                        .ToList();
                case "LineSpacing":
                    CadMTextParagraphFormat[] formats = SelectedFormats();
                    return LineSpacings
                        .Select(l => new CadMenuItemViewModel(
                            l.Name,
                            () => SetLineSpacing(l),
                            true,
                            null,
                            formats.All(f =>
                                f.LineSpacing == 'm' && Math.Abs(f.LineSpacingValue - (double)l.Value) < 1E-9
                            )
                        ))
                        .Concat(
                            new[]
                            {
                                new CadMenuItemViewModel(string.Empty, null),
                                new CadMenuItemViewModel("More…", () => ShowParagraph(true)),
                                new CadMenuItemViewModel(
                                    "Clear Paragraph Spacing",
                                    () => Editor.SetFormat(f => f.WithLineSpacing('\0', 1.0)),
                                    formats.Any(f => f.LineSpacing != '\0')
                                ),
                            }
                        )
                        .ToList();
                case "Columns":
                    return ColumnsMenu();
                case "Editor":
                    return new[]
                    {
                        new CadMenuItemViewModel(
                            "Paragraph Alignment",
                            new[]
                            {
                                new CadMenuItemViewModel(
                                    "Left",
                                    () => Editor.SetAlignment('l'),
                                    true,
                                    null,
                                    IsAlignedLeft
                                ),
                                new CadMenuItemViewModel(
                                    "Centre",
                                    () => Editor.SetAlignment('c'),
                                    true,
                                    null,
                                    IsAlignedCentre
                                ),
                                new CadMenuItemViewModel(
                                    "Right",
                                    () => Editor.SetAlignment('r'),
                                    true,
                                    null,
                                    IsAlignedRight
                                ),
                                new CadMenuItemViewModel(
                                    "Justified",
                                    () => Editor.SetAlignment('j'),
                                    true,
                                    null,
                                    IsJustified
                                ),
                                new CadMenuItemViewModel(
                                    "Distributed",
                                    () => Editor.SetAlignment('d'),
                                    true,
                                    null,
                                    IsDistributed
                                ),
                            }
                        ),
                        new CadMenuItemViewModel("Paragraph…", () => ShowParagraph(false), !Edit.IsTableCell),
                        new CadMenuItemViewModel("Bullets and Lists", BulletsMenu(false)),
                        new CadMenuItemViewModel("Columns", !Edit.IsTableCell, ColumnsMenu()),
                    };
                case "Ruler":
                    return new[]
                    {
                        new CadMenuItemViewModel("Set Mtext Width…", () => AskSize(false)),
                        new CadMenuItemViewModel("Set Mtext Height…", () => AskSize(true)),
                    };
                case "Case":
                    return new[]
                    {
                        new CadMenuItemViewModel("UPPERCASE", () => Editor.ChangeCase(true)),
                        new CadMenuItemViewModel("lowercase", () => Editor.ChangeCase(false)),
                    };
                case "Clear":
                    return new[]
                    {
                        new CadMenuItemViewModel("Remove Character Formatting", () => Editor.ClearFormatting()),
                    };
                case "Bullets":
                    return BulletsMenu(true);
                default:
                    return Array.Empty<CadMenuItemViewModel>();
            }
        }

        /// <summary>
        /// The items of Bullets and Numbering, as AutoCAD 2021's ribbon menu and the editor's Bullets and Lists have them: Off,
        /// Numbered, Lettered (Lowercase, Uppercase) and Bulleted, each labelling the selected paragraphs, or the caret's;
        /// Start and Continue grey, not built; Allow Auto Bullets and Numbering grey and unticked, as the editor does not turn
        /// "1." typed into a list; Allow Bullets and Lists grey and ticked, as lists are always allowed. On the ribbon
        /// (<paramref name="ticks"/>) the labels every selected paragraph has are ticked, Lettered for either case, its menu
        /// ticking which, with the icons of the design; the right-click menu ticks no list, as AutoCAD's.
        /// </summary>
        private IReadOnlyList<CadMenuItemViewModel> BulletsMenu(bool ticks)
        {
            CadMTextNumbering? all = SelectedNumbering();
            string blank = ticks ? string.Empty : null;

            bool? Tick(bool on) => ticks ? on : (bool?)null;

            CadMenuItemViewModel List(string header, CadMTextNumbering numbering, string icon) =>
                new CadMenuItemViewModel(
                    header,
                    () => Editor.SetNumbering(numbering),
                    true,
                    null,
                    Tick(all == numbering)
                )
                {
                    Icon = ticks ? icon : null,
                };

            CadMenuItemViewModel Grey(string header, bool? isChecked) =>
                new CadMenuItemViewModel(header, () => { }, false, MainViewModel.NotBuiltYet, isChecked)
                {
                    Icon = blank,
                };

            return new[]
            {
                List("Off", CadMTextNumbering.Off, blank),
                List("Numbered", CadMTextNumbering.Number, NumberedIcon),
                new CadMenuItemViewModel(
                    "Lettered",
                    true,
                    new[]
                    {
                        List("Lowercase", CadMTextNumbering.LetterLower, LowercaseIcon),
                        List("Uppercase", CadMTextNumbering.LetterUpper, UppercaseIcon),
                    },
                    isChecked: Tick(all == CadMTextNumbering.LetterLower || all == CadMTextNumbering.LetterUpper)
                )
                {
                    Icon = blank,
                },
                List("Bulleted", CadMTextNumbering.Bullet, blank),
                CadMenuItemViewModel.Separator(),
                Grey("Start", null),
                Grey("Continue", null),
                CadMenuItemViewModel.Separator(),
                Grey("Allow Auto Bullets and Numbering", false),
                Grey("Allow Bullets and Lists", true),
            };
        }

        /// <summary>The label every selected paragraph, or the caret's, opens with; null where they differ.</summary>
        private CadMTextNumbering? SelectedNumbering()
        {
            (int first, int last) = Editor.SelectedParagraphs;
            CadMTextNumbering numbering = Editor.NumberingOf(first);

            for (int p = first + 1; p <= last; p++)
            {
                if (Editor.NumberingOf(p) != numbering)
                {
                    return null;
                }
            }

            return numbering;
        }

        /// <summary>Sets on the edit what the frame changed, before the words answer the prompt.</summary>
        internal void WriteBox()
        {
            CadMTextFrame frame = Editor.Frame;
            CadMTextFrame was = Editor.OriginalFrame;

            // A width alone is written as before; a height or columns as the whole box.
            if (!frame.Equals(was))
            {
                if (!frame.HasColumns && !was.HasColumns && Math.Abs(frame.Height - was.Height) <= 1E-9)
                {
                    Edit.NewWidth = frame.Width;
                }
                else
                {
                    Edit.NewFrame = frame;
                }
            }

            if (!_boxChanged)
            {
                return;
            }

            if (_attachment != Edit.Attachment)
            {
                Edit.NewAttachment = _attachment;
            }

            Edit.NewLineSpacing = _lineSpacing;
        }

        private void Toggle(string what)
        {
            CadMTextLook look = Editor.CurrentLook;

            switch (what)
            {
                case "Bold":
                    Editor.SetBold(!look.Bold);
                    break;
                case "Italic":
                    Editor.SetItalic(!look.Italic);
                    break;
                case "Strikethrough":
                    Editor.SetStrikethrough(!look.Strikethrough);
                    break;
                case "Underline":
                    Editor.SetUnderline(!look.Underline);
                    break;
                case "Overline":
                    Editor.SetOverline(!look.Overline);
                    break;
            }
        }

        private void InsertSymbol(TextEditorChoice choice)
        {
            if (!(choice?.Value is string[] symbol))
            {
                return;
            }

            if (symbol[1] == null)
            {
                Editor.Type(symbol[0]);
            }
            else
            {
                Editor.InsertSymbol(symbol[0], symbol[1]);
            }
        }

        private void Justify(TextEditorChoice choice)
        {
            if (choice?.Value is int attachment && attachment != _attachment)
            {
                _attachment = attachment;
                _boxChanged = true;
                Raise(nameof(Attachment));
                Refresh();
            }
        }

        private void SetLineSpacing(TextEditorChoice choice)
        {
            // As AutoCAD 2021's Line Spacing menu: a multiple of the paragraphs' own (\pxsm1.5;), not the MTEXT's.
            if (choice?.Value is double factor)
            {
                Editor.SetFormat(f => f.WithLineSpacing('m', factor));
            }
        }

        private void Refresh()
        {
            foreach (
                string name in new[]
                {
                    nameof(HeightText),
                    nameof(IsBold),
                    nameof(IsItalic),
                    nameof(IsStrikethrough),
                    nameof(IsUnderline),
                    nameof(IsOverline),
                    nameof(CanBold),
                    nameof(Font),
                    nameof(IsShxFont),
                    nameof(SelectedColor),
                    nameof(ObliqueText),
                    nameof(TrackingText),
                    nameof(WidthFactorText),
                    nameof(Alignment),
                    nameof(IsAlignedLeft),
                    nameof(IsAlignedCentre),
                    nameof(IsAlignedRight),
                    nameof(IsJustified),
                    nameof(IsDistributed),
                    nameof(CanUndo),
                    nameof(CanRedo),
                    nameof(IsModified),
                    nameof(Width),
                }
            )
            {
                Raise(name);
            }
        }

        private static TextEditorChoice Color(string name, int index, int rgb = int.MinValue) =>
            new TextEditorChoice(
                name,
                string.Empty,
                index,
                rgb == int.MinValue
                    ? DxfColor.FromColorIndex(DxfColorMethod.ByAci, (short)index).ColorValue & 0xFFFFFF
                    : rgb
            );

        private static string Number(double value, string format = "0.####") =>
            value.ToString(format, CultureInfo.InvariantCulture);

        private static bool TryNumber(string text, out double value) =>
            double.TryParse((text ?? string.Empty).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value)
            && !double.IsNaN(value)
            && !double.IsInfinity(value);
    }
}
