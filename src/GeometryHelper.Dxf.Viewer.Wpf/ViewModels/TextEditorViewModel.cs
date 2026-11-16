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
    /// greyed, the height), Formatting (bold, italic, strikethrough, underline, overline, Match, the font, the colour, upper and
    /// lower case, clear formatting, oblique, tracking and width), Paragraph (justification, the paragraphs' line spacing with
    /// More… and Clear Paragraph Spacing, the five alignments, the launcher of the Paragraph dialog, bullets and numbering),
    /// Insert (Columns with Column Settings, the Symbol menu with its character map; field greyed), Spell Check (the red
    /// underline and Edit Dictionaries), Tools (Find and Replace, Import Text, AutoCAPS), Options (ruler, undo, redo) and Close Text Editor. The ruler's drags, the dialogs and the menus are each
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
        private string _find = string.Empty;
        private string _replace = string.Empty;
        private readonly CadMTextFindOptions _findOptions = new CadMTextFindOptions();
        private string _findMessage = string.Empty;
        private bool _isSpellCheckOn = true;
        private int _spellStamp;
        private string _spelledText;
        private int _spelledStamp = -1;
        private IReadOnlyList<CadMisspelledWord> _misspelled = Array.Empty<CadMisspelledWord>();
        private int _attachment;
        private double _lineSpacing;
        private bool _boxChanged;
        private CadMTextMask _mask;
        private readonly CadMTextMask _originalMask;

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

            // The mask of the MTEXT edited; none for an MTEXT being made or a table cell.
            _originalMask =
                edit.Handle != null && !edit.IsTableCell && model?.Document?.FindEntity(edit.Handle) is DxfMText masked
                    ? CadMTextMask.Of(masked)
                    : new CadMTextMask(false, CadMTextMask.DefaultOffsetFactor, false, null);
            _mask = _originalMask;
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
                "lcr"[column],
                edit.FieldShown
            );
            Editor.Changed += (s, e) =>
            {
                Refresh();
                LayTableOut();
            };
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
            // AutoCAD 2021's Symbol menu in its order: Degrees, Plus/Minus and Diameter as the % codes, the rest as \U+ codes.
            Symbols = new[]
            {
                Percent("Degrees", 'd', 0xB0),
                Percent("Plus/Minus", 'p', 0xB1),
                Percent("Diameter", 'c', 0xD8),
                Unicode("Almost Equal", 0x2248),
                Unicode("Angle", 0x2220),
                Unicode("Boundary Line", 0xE100),
                Unicode("Centerline", 0x2104),
                Unicode("Delta", 0x0394),
                Unicode("Electrical Phase", 0x0278),
                Unicode("Flow Line", 0xE101),
                Unicode("Identity", 0x2261),
                Unicode("Initial Length", 0xE200),
                Unicode("Monument Line", 0xE102),
                Unicode("Not Equal", 0x2260),
                Unicode("Ohm", 0x2126),
                Unicode("Omega", 0x03A9),
                Unicode("Plate/Property Line", 0x214A),
                Unicode("Subscript 2", 0x2082),
                Unicode("Squared", 0x00B2),
                Unicode("Cubed", 0x00B3),
                new TextEditorChoice(
                    "Non-breaking Space",
                    "Ctrl+Shift+Space",
                    new[] { char.ConvertFromUtf32(0xA0), null }
                ),
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
            FieldCommand = new DelegateCommand(_ => InsertField());
            JustifyCommand = new DelegateCommand(p => Justify(p as TextEditorChoice));
            LineSpacingCommand = new DelegateCommand(p => SetLineSpacing(p as TextEditorChoice));
            FindNextCommand = new DelegateCommand(_ => FindNext());
            ReplaceCommand = new DelegateCommand(_ => Replace());
            ReplaceAllCommand = new DelegateCommand(_ => ReplaceAll());
            FindReplaceCommand = new DelegateCommand(_ => ShowFindReplace());
            MatchCommand = new DelegateCommand(_ => Editor.BeginMatch());
            ImportTextCommand = new DelegateCommand(_ => ImportText());
            EditDictionariesCommand = new DelegateCommand(_ => ShowDictionaries());
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

        /// <summary>Gets or sets the words Find and Replace looks for.</summary>
        public string FindText
        {
            get => _find;
            set => Set(ref _find, value ?? string.Empty);
        }

        /// <summary>Gets or sets what Replace and Replace All put in their place.</summary>
        public string ReplaceText
        {
            get => _replace;
            set => Set(ref _replace, value ?? string.Empty);
        }

        /// <summary>Gets or sets whether capitals must match.</summary>
        public bool MatchCase
        {
            get => _findOptions.MatchCase;
            set
            {
                _findOptions.MatchCase = value;
                Raise(nameof(MatchCase));
            }
        }

        /// <summary>Gets or sets whether a match must be whole words.</summary>
        public bool WholeWords
        {
            get => _findOptions.WholeWords;
            set
            {
                _findOptions.WholeWords = value;
                Raise(nameof(WholeWords));
            }
        }

        /// <summary>Gets or sets whether the words searched for are a wildcard pattern (<c>*</c>, <c>?</c>, <c>#</c>, <c>@</c>, <c>[AB]</c>).</summary>
        public bool UseWildcards
        {
            get => _findOptions.UseWildcards;
            set
            {
                _findOptions.UseWildcards = value;
                Raise(nameof(UseWildcards));
            }
        }

        /// <summary>Gets or sets whether accents must match.</summary>
        public bool MatchDiacritics
        {
            get => _findOptions.MatchDiacritics;
            set
            {
                _findOptions.MatchDiacritics = value;
                Raise(nameof(MatchDiacritics));
            }
        }

        /// <summary>Gets or sets whether half-width and full-width forms must match.</summary>
        public bool MatchHalfFullWidth
        {
            get => _findOptions.MatchHalfFullWidth;
            set
            {
                _findOptions.MatchHalfFullWidth = value;
                Raise(nameof(MatchHalfFullWidth));
            }
        }

        /// <summary>Gets what Find and Replace last found or replaced.</summary>
        public string FindMessage
        {
            get => _findMessage;
            private set => Set(ref _findMessage, value);
        }

        /// <summary>Gets or sets AutoCAPS, the toggle of the Tools panel: letters typed, pasted or imported go in as capitals.</summary>
        public bool IsAutoCaps
        {
            get => Editor.AutoCaps;
            set
            {
                Editor.AutoCaps = value;
                Raise(nameof(IsAutoCaps));
            }
        }

        /// <summary>Gets whether Match Text Formatting waits for letters to be selected: the brush of the Formatting panel is picked up.</summary>
        public bool IsMatching => Editor.IsMatching;

        /// <summary>Gets or sets whether the words not in the dictionary are underlined in red, the Spell Check toggle of the tab; on until set, as AutoCAD's.</summary>
        public bool IsSpellCheckOn
        {
            get => _isSpellCheckOn;
            set
            {
                if (Set(ref _isSpellCheckOn, value))
                {
                    Raise(nameof(Misspelled));
                }
            }
        }

        /// <summary>Gets the words of the editor that are not in the dictionary, to be underlined; none while Spell Check is off or there is no window dictionary.</summary>
        public IReadOnlyList<CadMisspelledWord> Misspelled
        {
            get
            {
                CadSpellDictionary dictionary = _model?.SpellDictionary;

                if (!_isSpellCheckOn || dictionary == null)
                {
                    return Array.Empty<CadMisspelledWord>();
                }

                string text = Editor.Text;

                if (_spelledStamp != _spellStamp || !string.Equals(text, _spelledText, StringComparison.Ordinal))
                {
                    _misspelled = dictionary.Check(Editor);
                    _spelledText = text;
                    _spelledStamp = _spellStamp;
                }

                return _misspelled;
            }
        }

        /// <summary>Raised when the editor closes, so that a dialog of its own (Find and Replace) closes with it.</summary>
        public event EventHandler Closed;

        internal void RaiseClosed() => Closed?.Invoke(this, EventArgs.Empty);

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

        /// <summary>Gets the command of Insert Field (Ctrl+F): the Field dialog, then the field at the caret.</summary>
        public ICommand FieldCommand { get; }

        /// <summary>Gets the command of a justification of the Justification menu.</summary>
        public ICommand JustifyCommand { get; }

        /// <summary>Gets the command of a factor of the Line Spacing menu: the selected paragraphs, or the caret's, spaced at that multiple of their own (<c>\pxsm1.5;</c>), as AutoCAD 2021 sets them.</summary>
        public ICommand LineSpacingCommand { get; }

        /// <summary>Gets Find and Replace's Find Next.</summary>
        public ICommand FindNextCommand { get; }

        /// <summary>Gets Find and Replace's Replace: the match selected is replaced and the next one selected.</summary>
        public ICommand ReplaceCommand { get; }

        /// <summary>Gets Find and Replace's Replace All.</summary>
        public ICommand ReplaceAllCommand { get; }

        /// <summary>Gets the Tools panel's Find &amp; Replace, and Ctrl+H in the words: the Find and Replace dialog of the editor.</summary>
        public ICommand FindReplaceCommand { get; }

        /// <summary>Gets the Formatting panel's Match: the look of the letters selected is taken up for the next letters selected.</summary>
        public ICommand MatchCommand { get; }

        /// <summary>Gets the Tools panel's Import Text: a text or RTF file put in at the caret.</summary>
        public ICommand ImportTextCommand { get; }

        /// <summary>Gets the Spell Check panel's Edit Dictionaries: the custom words added and deleted.</summary>
        public ICommand EditDictionariesCommand { get; }

        /// <summary>Gets the editor's Undo.</summary>
        public ICommand UndoCommand { get; }

        /// <summary>Gets the editor's Redo.</summary>
        public ICommand RedoCommand { get; }

        /// <summary>Gets Close Text Editor: the words kept.</summary>
        public ICommand CloseCommand { get; }

        /// <summary>Gets whether anything was changed: the words, the width, the justification, the line spacing or the background mask.</summary>
        public bool IsModified =>
            Editor.IsModified
            || _boxChanged
            || !_mask.Equals(_originalMask)
            || !Editor.Frame.Equals(Editor.OriginalFrame);

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

        /// <summary>Gets the background mask the words are shown on: the MTEXT's own until Background Mask sets another.</summary>
        public CadMTextMask Mask => _mask;

        /// <summary>Gets the command of the Style panel's Mask button and the Background mask… box: the Background Mask dialog.</summary>
        public ICommand MaskCommand => new DelegateCommand(_ => ShowBackgroundMask(), _ => !Edit.IsTableCell);

        /// <summary>
        /// Opens the Background Mask dialog over the mask the words stand on, where the window shows it
        /// (<see cref="MainViewModel.ShowBackgroundMask"/>); OK sets it as the mask of the MTEXT, shown at once and written
        /// with the words.
        /// </summary>
        /// <returns>Whether OK was pressed.</returns>
        public bool ShowBackgroundMask()
        {
            if (Edit.IsTableCell || _model?.ShowBackgroundMask == null)
            {
                return false;
            }

            var dialog = new MTextMaskViewModel(_mask);

            if (!_model.ShowBackgroundMask(dialog) || dialog.Result == null)
            {
                return false;
            }

            if (!dialog.Result.Equals(_mask))
            {
                _mask = dialog.Result;
                Raise(nameof(Mask));
                Refresh();
            }

            return true;
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
            layout.SetMask(_mask);
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
        /// Gets the items of a menu of the tab: Symbol (each symbol with its code, Other… opening the character map), Justification and Line Spacing
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
                                new CadMenuItemViewModel("Other…", () => ShowCharacterMap()),
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
                    return FieldMenu()
                        .Concat(
                            new[]
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
                            }
                        )
                        .ToList();
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

            if (!_mask.Equals(_originalMask))
            {
                Edit.NewMask = _mask;
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

        /// <summary>Find and Replace's Find Next: the next match of <see cref="FindText"/> selected, the search going round to the start; <see cref="FindMessage"/> says where none is found.</summary>
        public void FindNext()
        {
            if (_find.Length == 0)
            {
                FindMessage = string.Empty;
                return;
            }

            FindMessage = Editor.FindNext(_find, _findOptions) ? string.Empty : "\"" + _find + "\" was not found.";
        }

        /// <summary>
        /// Find and Replace's Replace: where the match selected is of <see cref="FindText"/> it is replaced by
        /// <see cref="ReplaceText"/> and the next match is selected; else the next match is selected, as Find Next does.
        /// </summary>
        public void Replace()
        {
            if (_find.Length == 0)
            {
                FindMessage = string.Empty;
                return;
            }

            bool replaced = Editor.Replace(_find, _replace, _findOptions);
            bool next = Editor.SelectionLength > 0;
            FindMessage =
                replaced ? (next ? "Replaced." : "Replaced. No more matches.")
                : next ? string.Empty
                : "\"" + _find + "\" was not found.";
        }

        /// <summary>Find and Replace's Replace All: every match of <see cref="FindText"/> replaced, one step of the editor's undo; <see cref="FindMessage"/> says how many.</summary>
        public void ReplaceAll()
        {
            if (_find.Length == 0)
            {
                FindMessage = string.Empty;
                return;
            }

            FindMessage =
                Editor.ReplaceAll(_find, _replace, _findOptions).ToString(CultureInfo.InvariantCulture) + " replaced.";
        }

        /// <summary>Opens the editor's Find and Replace dialog where the window shows it (<see cref="MainViewModel.ShowMTextFindReplace"/>).</summary>
        /// <returns>Whether a dialog was shown.</returns>
        public bool ShowFindReplace()
        {
            if (_model?.ShowMTextFindReplace == null)
            {
                return false;
            }

            _model.ShowMTextFindReplace(this);
            return true;
        }

        /// <summary>
        /// Import Text: asks for a .txt or .rtf file where the window shows its file dialog
        /// (<see cref="MainViewModel.AskImportTextFile"/>) and puts its words in at the caret.
        /// </summary>
        /// <returns>Whether words were put in.</returns>
        public bool ImportText() => ImportText(_model?.AskImportTextFile?.Invoke());

        /// <summary>
        /// Puts the words of a file in at the caret, over the selection, as one step of the editor's undo
        /// (<see cref="CadMTextImport"/>); a file that cannot be read, or is over 32 KB, is said so in the command line's history.
        /// </summary>
        /// <param name="path">The .txt or .rtf file; null or empty does nothing.</param>
        /// <returns>Whether words were put in.</returns>
        public bool ImportText(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            try
            {
                Editor.InsertContent(CadMTextImport.FromFile(path, Editor.Height));
                return true;
            }
            catch (Exception e)
                when (e is System.IO.IOException
                    || e is UnauthorizedAccessException
                    || e is ArgumentException
                    || e is NotSupportedException
                )
            {
                GeometryHelperLog.Warn("TextEditorViewModel: the text of " + path + " was not imported.", e);
                _model?.Say("Import Text: " + e.Message);
                return false;
            }
        }

        /// <summary>Opens Edit Dictionaries where the window shows it (<see cref="MainViewModel.ShowDictionaries"/>); the words are checked again once it closes.</summary>
        /// <returns>Whether a dialog was shown.</returns>
        public bool ShowDictionaries()
        {
            CadSpellDictionary dictionary = _model?.SpellDictionary;

            if (dictionary == null || _model.ShowDictionaries == null)
            {
                return false;
            }

            _model.ShowDictionaries(new SpellDictionariesViewModel(dictionary));
            SpellChanged();
            return true;
        }

        /// <summary>Opens the character map of the font of the letters where the window shows it (<see cref="MainViewModel.ShowCharacterMap"/>): the Symbol menu's Other….</summary>
        /// <returns>Whether a dialog was shown.</returns>
        public bool ShowCharacterMap()
        {
            if (_model?.ShowCharacterMap == null)
            {
                return false;
            }

            _model.ShowCharacterMap(new CharacterMapViewModel(IsShxFont ? "Arial" : Font));
            return true;
        }

        /// <summary>
        /// The items the right-click puts over the editor's menu on a word not in the dictionary: the words nearest it, Add
        /// Word and Ignore All; each suggestion puts its word in place of it, one step of the editor's undo.
        /// </summary>
        /// <param name="place">The place clicked.</param>
        /// <returns>The items, ending with a separator; none where no misspelled word is there.</returns>
        public IReadOnlyList<CadMenuItemViewModel> SpellMenu(int place)
        {
            CadSpellDictionary dictionary = _model?.SpellDictionary;
            CadMisspelledWord word = Misspelled.FirstOrDefault(w => place >= w.Start && place <= w.Start + w.Length);

            if (word == null || dictionary == null)
            {
                return Array.Empty<CadMenuItemViewModel>();
            }

            var items = new List<CadMenuItemViewModel>();
            IReadOnlyList<string> suggestions = dictionary.Suggest(word.Word);

            foreach (string suggestion in suggestions)
            {
                string chosen = suggestion;
                items.Add(new CadMenuItemViewModel(chosen, () => Editor.ReplaceText(word.Start, word.Length, chosen)));
            }

            if (suggestions.Count == 0)
            {
                items.Add(new CadMenuItemViewModel("(no suggestions)", () => { }, false));
            }

            items.Add(CadMenuItemViewModel.Separator());
            items.Add(new CadMenuItemViewModel("Add Word", () =>
                {
                    string failed = dictionary.Add(word.Word);

                    if (failed != null)
                    {
                        _model.Say(failed);
                    }

                    SpellChanged();
                }));
            items.Add(new CadMenuItemViewModel("Ignore All", () =>
                {
                    dictionary.IgnoreAll(word.Word);
                    SpellChanged();
                }));
            items.Add(CadMenuItemViewModel.Separator());
            return items;
        }

        /// <summary>The dictionary changed (a word added, ignored or deleted): the words are checked again and the underlines drawn anew.</summary>
        private void SpellChanged()
        {
            _spellStamp++;
            Raise(nameof(Misspelled));
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

        /// <summary>
        /// Insert Field, as the Text Editor tab's button and Ctrl+F do it: the Field dialog, then the field it ends with at the
        /// caret in place of the selection, shown as the words it comes to and written as its code. Where the selection is one
        /// field the dialog edits it instead, as a double click does.
        /// </summary>
        public void InsertField()
        {
            if (_model?.Document == null || _model.ShowField == null)
            {
                return;
            }

            if (Editor.SelectionLength == 1 && EditFieldAt(Editor.SelectionStart))
            {
                return;
            }

            CadFieldRequest request = _model.NewFieldRequest();
            string code = _model.AskField(request);

            if (code != null)
            {
                Editor.InsertField(request.Preview, code);
            }
        }

        /// <summary>
        /// Edit Field, as a double click on a field in the text does it: the Field dialog on the field of the place, which the
        /// field it ends with takes the place of.
        /// </summary>
        /// <param name="index">The place.</param>
        /// <returns>False where the place holds no field.</returns>
        public bool EditFieldAt(int index)
        {
            string was = Editor.FieldCodeAt(index);

            if (was == null || _model?.ShowField == null)
            {
                return false;
            }

            CadFieldRequest request = _model.NewFieldRequest();
            request.Load(was);
            string code = _model.AskField(request);

            if (code != null)
            {
                Editor.ReplaceField(index, request.Preview, code);
            }

            return true;
        }

        /// <summary>The place of the field the caret is at: the one selected alone, else the one before the caret, else the one after; -1 for none.</summary>
        private int FieldAtCaret()
        {
            int caret = Editor.Caret;

            if (Editor.SelectionLength == 1 && Editor.FieldCodeAt(Editor.SelectionStart) != null)
            {
                return Editor.SelectionStart;
            }

            return Editor.FieldCodeAt(caret - 1) != null ? caret - 1
                : Editor.FieldCodeAt(caret) != null ? caret
                : -1;
        }

        /// <summary>The field items of the editor's right-click: Insert Field, and where the caret is at a field Edit Field and Convert Field To Text, as AutoCAD's.</summary>
        private IEnumerable<CadMenuItemViewModel> FieldMenu()
        {
            var items = new List<CadMenuItemViewModel>
            {
                new CadMenuItemViewModel("Insert Field…    Ctrl+F", InsertField, _model?.ShowField != null),
            };
            int field = FieldAtCaret();

            if (field >= 0)
            {
                items.Add(new CadMenuItemViewModel("Edit Field…", () => EditFieldAt(field), _model?.ShowField != null));
                items.Add(new CadMenuItemViewModel("Convert Field To Text", () => Editor.ConvertFieldToText(field)));
            }

            items.Add(new CadMenuItemViewModel(string.Empty, null));
            return items;
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

        /// <summary>A cell of a table typed in: its row laid out again for the words typed so far, the window told where the rows moved.</summary>
        private void LayTableOut()
        {
            CadTableLive live = Edit.TableLive;

            if (live == null)
            {
                return;
            }

            int was = live.Version;
            live.Update(Editor.Contents);

            if (live.Version != was)
            {
                _model?.RaiseTableLiveChanged();
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
                    nameof(IsMatching),
                    nameof(IsModified),
                    nameof(Width),
                }
            )
            {
                Raise(name);
            }
        }

        /// <summary>A symbol written as a % code: shown as its letter, written <c>%%d</c>.</summary>
        private static TextEditorChoice Percent(string name, char letter, int code) =>
            new TextEditorChoice(name, "%%" + letter, new[] { char.ConvertFromUtf32(code), "%%" + letter });

        /// <summary>A symbol written as a Unicode code: shown as its letter, written <c>\U+2248</c>.</summary>
        private static TextEditorChoice Unicode(string name, int code)
        {
            string written = "\\U+" + code.ToString("X4", CultureInfo.InvariantCulture);
            return new TextEditorChoice(name, written, new[] { char.ConvertFromUtf32(code), written });
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
