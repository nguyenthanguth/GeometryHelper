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
    /// case, clear formatting, oblique, tracking and width), Paragraph (justification, line spacing, the five alignments;
    /// bullets and numbering greyed), Insert (the Symbol menu; columns and field greyed), Spell Check greyed, Tools (Find and
    /// Replace), Options (ruler, undo, redo) and Close Text Editor. Close writes the words in AutoCAD's codes as the answer
    /// of the prompt that opened the editor, one undo step; Esc asks whether to keep them.
    /// </summary>
    public sealed class TextEditorViewModel : ObservableObject
    {
        private readonly MainViewModel _model;
        private bool _isRulerShown = true;
        private bool _isFindShown;
        private string _find = string.Empty;
        private string _replace = string.Empty;
        private bool _matchCase;
        private string _findMessage = string.Empty;
        private double _width;
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
            _width = edit.Width;
            _attachment = edit.Attachment;
            _lineSpacing = lineSpacing > 0.0 ? lineSpacing : 1.0;
            StyleFont = styleFont;
            StyleIsShx = styleIsShx;
            int column = (edit.Attachment - 1) % 3;
            Editor = new CadMTextEditor(edit.Value, edit.Height, styleFont, styleIsShx, "lcr"[column]);
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

        /// <summary>Gets or sets the frame's width in drawing units, which the diamond of the ruler drags; nought for none.</summary>
        public double Width
        {
            get => _width;
            set
            {
                if (Set(ref _width, Math.Max(0.0, value)))
                {
                    _boxChanged = true;
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

        /// <summary>Gets the command of a factor of the Line Spacing menu.</summary>
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
        public bool IsModified => Editor.IsModified || _boxChanged;

        /// <summary>
        /// Gets the items of a menu of the tab: Symbol (each symbol with its code, Other greyed), Justification and Line Spacing
        /// (the one set ticked), Case (UPPERCASE, lowercase), Clear (Remove Character Formatting), Bullets (greyed: not built).
        /// </summary>
        /// <param name="name">Symbol, Justification, LineSpacing, Case, Clear or Bullets.</param>
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
                    return LineSpacings
                        .Select(l => new CadMenuItemViewModel(
                            l.Name,
                            () => SetLineSpacing(l),
                            true,
                            null,
                            (double)l.Value == _lineSpacing
                        ))
                        .ToList();
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
                    return new[]
                    {
                        new CadMenuItemViewModel("Bullets and numbering are not built yet", () => { }, false),
                    };
                default:
                    return Array.Empty<CadMenuItemViewModel>();
            }
        }

        /// <summary>Sets on the edit what the frame changed, before the words answer the prompt.</summary>
        internal void WriteBox()
        {
            if (!_boxChanged)
            {
                return;
            }

            if (_width != Edit.Width)
            {
                Edit.NewWidth = _width;
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
            if (choice?.Value is double factor && factor != _lineSpacing)
            {
                _lineSpacing = factor;
                _boxChanged = true;
                Raise(nameof(LineSpacing));
                Refresh();
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
