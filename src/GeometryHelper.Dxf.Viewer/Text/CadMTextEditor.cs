using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// The words of an MTEXT being edited in place, as AutoCAD's text editor holds them: letters of a look each, paragraphs, a
    /// caret and a selection, and what the Text Editor tab does to them, each written back by <see cref="Contents"/> in AutoCAD's
    /// codes (<see cref="CadMTextContent"/>). It knows no window: the editor on the canvas and a test call it the same way.
    /// </summary>
    /// <remarks>
    /// Places are counted in letters from the start, a paragraph break one, a symbol or a stack one. A look set with nothing
    /// selected is the look of the letters typed next, as in AutoCAD; with letters selected it is theirs. "AB CD" with CD
    /// selected and made bold, in an MTEXT 2.5 high of a style in Arial, writes <c>AB {\fArial|b1|i0|c0|p34;CD}</c>. Bold and
    /// italic need a TrueType font: on letters of an SHX font they do nothing (<see cref="CanBold"/>). Each change can be
    /// taken back by <see cref="Undo"/> while the editor is open; closing it is one undo step of the drawing.
    /// </remarks>
    public sealed class CadMTextEditor
    {
        // The look of a list's bullet, as AutoCAD 2021's Bullets and Numbering writes it: the Symbol font, its symbol character set.
        private static readonly CadMTextLook BulletLook = CadMTextContent
            .Parse("{\\fSymbol|b0|i0|c2|p18;x}", 1.0)
            .Paragraphs[0]
            .Runs[0]
            .Look;

        private readonly Stack<State> _undo = new Stack<State>();
        private readonly Stack<State> _redo = new Stack<State>();
        private List<Cell> _cells = new List<Cell>();
        private List<char> _alignments = new List<char>();
        private List<string> _paragraphCodes = new List<string>();
        private CadMTextLook _pending;
        private State _changeStart;
        private bool _changed;

        /// <summary>Opens the words of an MTEXT for editing.</summary>
        /// <param name="contents">The words with their codes.</param>
        /// <param name="height">The MTEXT's height.</param>
        /// <param name="styleFont">The font of the MTEXT's style: a TrueType family (Arial) or an SHX file (romans); null for none known.</param>
        /// <param name="styleIsShx">Whether the style's font is an SHX file, whose letters cannot be bold or italic.</param>
        /// <param name="defaultAlignment">How a paragraph with no alignment code is aligned, l, c or r, as the MTEXT's attachment says.</param>
        public CadMTextEditor(
            string contents,
            double height,
            string styleFont = null,
            bool styleIsShx = false,
            char defaultAlignment = 'l'
        )
            : this(contents, height, CadMTextFrame.Of(0.0), styleFont, styleIsShx, defaultAlignment) { }

        /// <summary>Opens the words of an MTEXT for editing in its box: the width, height and columns the ruler and Column Settings change.</summary>
        /// <param name="contents">The words with their codes.</param>
        /// <param name="height">The MTEXT's height.</param>
        /// <param name="frame">The MTEXT's box as it is (<see cref="CadMTextFrame.Of(DxfMText)"/>); null for none.</param>
        /// <param name="styleFont">The font of the MTEXT's style: a TrueType family (Arial) or an SHX file (romans); null for none known.</param>
        /// <param name="styleIsShx">Whether the style's font is an SHX file, whose letters cannot be bold or italic.</param>
        /// <param name="defaultAlignment">How a paragraph with no alignment code is aligned, l, c or r, as the MTEXT's attachment says.</param>
        public CadMTextEditor(
            string contents,
            double height,
            CadMTextFrame frame,
            string styleFont = null,
            bool styleIsShx = false,
            char defaultAlignment = 'l'
        )
            : this(contents, height, frame, 1.0, styleFont, styleIsShx, defaultAlignment) { }

        /// <summary>
        /// Opens the words of an MTEXT in a style of letters of another width for editing in its box: a width in times of the
        /// style's (<c>{\W1.2x;ABC}</c>) is read as times that width, 0.96 in a style of 0.8, as AutoCAD 2021 draws it, and
        /// written back as that width (<c>\W0.9600;</c>).
        /// </summary>
        /// <param name="contents">The words with their codes.</param>
        /// <param name="height">The MTEXT's height.</param>
        /// <param name="frame">The MTEXT's box as it is (<see cref="CadMTextFrame.Of(DxfMText)"/>); null for none.</param>
        /// <param name="styleWidth">The width factor of the MTEXT's style; 1 where it is not above nought.</param>
        /// <param name="styleFont">The font of the MTEXT's style: a TrueType family (Arial) or an SHX file (romans); null for none known.</param>
        /// <param name="styleIsShx">Whether the style's font is an SHX file, whose letters cannot be bold or italic.</param>
        /// <param name="defaultAlignment">How a paragraph with no alignment code is aligned, l, c or r, as the MTEXT's attachment says.</param>
        public CadMTextEditor(
            string contents,
            double height,
            CadMTextFrame frame,
            double styleWidth,
            string styleFont = null,
            bool styleIsShx = false,
            char defaultAlignment = 'l'
        )
        {
            StyleWidth = styleWidth > 0.0 && !double.IsInfinity(styleWidth) ? styleWidth : 1.0;
            Height = height;
            StyleFont = styleFont;
            StyleIsShx = styleIsShx;
            DefaultAlignment = defaultAlignment == '\0' ? 'l' : defaultAlignment;
            Original = contents ?? string.Empty;
            OriginalFrame = frame ?? CadMTextFrame.Of(0.0);
            Frame = OriginalFrame;
            Load(CadMTextContent.Parse(Original, height, StyleWidth));
        }

        /// <summary>Raised when the words, the looks, the caret or the selection change.</summary>
        public event EventHandler Changed;

        /// <summary>Gets the words as they were when the editor opened.</summary>
        public string Original { get; }

        /// <summary>Gets the MTEXT's height, of which a run's height is written as a factor.</summary>
        public double Height { get; }

        /// <summary>Gets the width factor of the MTEXT's style, of which a width in times (<c>\W1.2x;</c>) is read; 1 for none given.</summary>
        public double StyleWidth { get; }

        /// <summary>Gets the font of the MTEXT's style.</summary>
        public string StyleFont { get; }

        /// <summary>Gets whether the style's font is an SHX file.</summary>
        public bool StyleIsShx { get; }

        /// <summary>Gets how a paragraph with no alignment code is aligned.</summary>
        public char DefaultAlignment { get; }

        /// <summary>Gets the number of places: letters, symbols and paragraph breaks.</summary>
        public int Length => _cells.Count;

        /// <summary>Gets the place of the caret, 0 to <see cref="Length"/>.</summary>
        public int Caret { get; private set; }

        /// <summary>Gets the other end of the selection; the caret where nothing is selected.</summary>
        public int Anchor { get; private set; }

        /// <summary>Gets where the selection starts.</summary>
        public int SelectionStart => Math.Min(Caret, Anchor);

        /// <summary>Gets how many places are selected.</summary>
        public int SelectionLength => Math.Abs(Caret - Anchor);

        /// <summary>Gets the words as shown, a line feed for each paragraph break, a symbol as its letter (° for <c>%%d</c>).</summary>
        public string Text
        {
            get
            {
                var builder = new StringBuilder();

                foreach (Cell cell in _cells)
                {
                    builder.Append(cell.Break ? "\n" : cell.Text);
                }

                return builder.ToString();
            }
        }

        /// <summary>Gets the selected words as shown.</summary>
        public string SelectedText
        {
            get
            {
                var builder = new StringBuilder();

                for (int i = SelectionStart; i < SelectionStart + SelectionLength; i++)
                {
                    builder.Append(_cells[i].Break ? "\n" : _cells[i].Text);
                }

                return builder.ToString();
            }
        }

        /// <summary>Gets the look the ribbon shows: of the letters typed next where nothing is selected, else of the first selected.</summary>
        public CadMTextLook CurrentLook
        {
            get
            {
                if (SelectionLength > 0)
                {
                    int at = SelectionStart;

                    while (at < SelectionStart + SelectionLength && at < _cells.Count && _cells[at].Break)
                    {
                        at++;
                    }

                    return at < _cells.Count && !_cells[at].Break ? _cells[at].Look : TypingLook;
                }

                return TypingLook;
            }
        }

        /// <summary>Gets whether bold and italic can be given to the look shown: its font, or the style's, is a TrueType font.</summary>
        public bool CanBold => CurrentLook.Font != null ? !CurrentLook.IsShx : !StyleIsShx;

        /// <summary>Gets the alignment of the paragraph the caret is in: l, c, r, j or d.</summary>
        public char Alignment
        {
            get
            {
                char alignment = _alignments[ParagraphOf(Caret)];
                return alignment == '\0' ? DefaultAlignment : alignment;
            }
        }

        /// <summary>Gets the alignment a paragraph carries of its own: l, c, r, j or d; nought where it follows the MTEXT's attachment.</summary>
        /// <param name="paragraph">The paragraph, from nought.</param>
        /// <returns>Its own alignment.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when there is no such paragraph.</exception>
        public char OwnAlignmentOf(int paragraph)
        {
            if (paragraph < 0 || paragraph >= _alignments.Count)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(paragraph),
                    paragraph,
                    "The words have " + _alignments.Count + " paragraphs, counted from nought."
                );
            }

            return _alignments[paragraph];
        }

        /// <summary>Gets whether a change can be taken back.</summary>
        public bool CanUndo => _undo.Count > 0;

        /// <summary>Gets whether a change taken back can be made again.</summary>
        public bool CanRedo => _redo.Count > 0;

        /// <summary>Gets the MTEXT's box as it was when the editor opened.</summary>
        public CadMTextFrame OriginalFrame { get; }

        /// <summary>Gets the MTEXT's box as the ruler and Column Settings have it now.</summary>
        public CadMTextFrame Frame { get; private set; }

        /// <summary>Gets whether a ruler drag is going on (<see cref="BeginChange"/>): its changes are one undo step when it ends.</summary>
        public bool IsChanging => _changeStart != null;

        /// <summary>Gets how many paragraphs the words make, at least one.</summary>
        public int ParagraphCount => _paragraphCodes.Count;

        /// <summary>Gets the first and the last paragraph the selection, or the caret, is in.</summary>
        public (int First, int Last) SelectedParagraphs =>
            (ParagraphOf(SelectionStart), ParagraphOf(SelectionStart + SelectionLength));

        /// <summary>Gets how the caret's paragraph is set, or the first selected one's, as the ruler shows it.</summary>
        public CadMTextParagraphFormat CurrentFormat => FormatOf(SelectedParagraphs.First);

        /// <summary>
        /// Gets how a paragraph is set: its own codes over those of the paragraphs before it, as AutoCAD carries them on. In an
        /// MTEXT 2.5 high, <c>\pxt4;A\PB</c> has a tab stop at 10 in both paragraphs.
        /// </summary>
        /// <param name="paragraph">The paragraph, from nought.</param>
        /// <returns>Its format.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when there is no such paragraph.</exception>
        public CadMTextParagraphFormat FormatOf(int paragraph)
        {
            if (paragraph < 0 || paragraph >= _paragraphCodes.Count)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(paragraph),
                    paragraph,
                    "The words have " + _paragraphCodes.Count + " paragraphs, counted from nought."
                );
            }

            CadMTextParagraphFormat format = CadMTextParagraphFormat.Default;

            for (int p = 0; p <= paragraph; p++)
            {
                format = CadMTextParagraphFormat.Read(format, _paragraphCodes[p], Height);
            }

            return format;
        }

        /// <summary>
        /// Changes how the selected paragraphs, or the caret's, are set: their indents, tab stops and spacing, one undo step.
        /// Each is written as AutoCAD writes it, what differs from the paragraph before, and the paragraph after them is
        /// written back to how it was: a tab stop at 10 set on the first of "A", "B" in an MTEXT 2.5 high writes
        /// <c>\pxt4;A\P\ptz;B</c>.
        /// </summary>
        /// <param name="change">What becomes of each paragraph's format.</param>
        /// <exception cref="ArgumentNullException">Thrown when the change is null.</exception>
        public void SetFormat(Func<CadMTextParagraphFormat, CadMTextParagraphFormat> change)
        {
            if (change == null)
            {
                throw new ArgumentNullException(nameof(change));
            }

            (int first, int last) = SelectedParagraphs;
            string[] codes = FormatCodes(change, first, last);

            if (codes == null)
            {
                return;
            }

            Save();
            _paragraphCodes = new List<string>(codes);
            Raise();
        }

        /// <summary>
        /// Labels the selected paragraphs, or the caret's, as a list, as AutoCAD 2021's Bullets and Numbering does, one undo
        /// step: each opens with its label and a tab, the paragraphs hanging 3 heights in with a tab stop there, and a label one
        /// had is replaced. In an MTEXT 2.5 high, "Plate", "Beam" numbered is written <c>\pxi-3,l3,t3;1.\tPlate\P2.\tBeam</c>
        /// and bulleted <c>\pxi-3,l3,t3;{\fSymbol|b0|i0|c2|p18;·\t}Plate\P{\fSymbol|b0|i0|c2|p18;·\t}Beam</c>, as AutoCAD's
        /// TextEditor writes them (probed). <see cref="CadMTextNumbering.Off"/> takes the labels, the indent and the stop away.
        /// </summary>
        /// <param name="numbering">The labels.</param>
        public void SetNumbering(CadMTextNumbering numbering)
        {
            (int first, int last) = SelectedParagraphs;
            double hang = 3.0 * Height;
            bool listed = numbering != CadMTextNumbering.Off;
            string[] codes = FormatCodes(
                f =>
                    listed
                        ? f.WithIndents(0.0, hang, f.RightIndent)
                            .WithTabStops(
                                new[] { new CadMTextTabStop(hang) }.Concat(
                                    f.TabStops.Where(t => t.Position > hang + 1E-9)
                                )
                            )
                        : f.WithIndents(0.0, 0.0, f.RightIndent)
                            .WithTabStops(f.TabStops.Where(t => Math.Abs(t.Position - hang) > 1E-9)),
                first,
                last
            );
            var starts = new List<int>();
            int at = 0;

            for (int p = 0; p <= last; p++)
            {
                if (p >= first)
                {
                    starts.Add(at);
                }

                while (at < _cells.Count && !_cells[at].Break)
                {
                    at++;
                }

                at++;
            }

            bool any = codes != null;

            for (int k = starts.Count - 1; k >= 0 && !any; k--)
            {
                any = LabelLength(starts[k]) > 0;
            }

            if (!any && !listed)
            {
                return;
            }

            Save();

            if (codes != null)
            {
                _paragraphCodes = new List<string>(codes);
            }

            // From the last paragraph back, so that the places of those before stay as they were.
            for (int k = starts.Count - 1; k >= 0; k--)
            {
                int start = starts[k];
                int removed = LabelLength(start);
                _cells.RemoveRange(start, removed);
                List<Cell> label = listed ? Label(numbering, k, start) : new List<Cell>();
                _cells.InsertRange(start, label);
                Caret = Shifted(Caret, start, removed, label.Count);
                Anchor = Shifted(Anchor, start, removed, label.Count);
            }

            _pending = null;
            Raise();
        }

        /// <summary>Changes the MTEXT's box: its width, defined height or columns, one undo step.</summary>
        /// <param name="frame">The box.</param>
        /// <exception cref="ArgumentNullException">Thrown when the frame is null.</exception>
        public void SetFrame(CadMTextFrame frame)
        {
            if (frame == null)
            {
                throw new ArgumentNullException(nameof(frame));
            }

            if (frame.Equals(Frame))
            {
                return;
            }

            Save();
            Frame = frame;
            Raise();
        }

        /// <summary>
        /// Starts a drag of the ruler: what changes until <see cref="EndChange"/> is shown at once but makes one undo step at
        /// the end, or none where the drag is put back.
        /// </summary>
        public void BeginChange()
        {
            if (_changeStart == null)
            {
                _changeStart = Capture();
                _changed = false;
            }
        }

        /// <summary>Ends a drag of the ruler: its changes kept as one undo step (the release), or put back (Esc).</summary>
        /// <param name="keep">Whether to keep the changes.</param>
        public void EndChange(bool keep)
        {
            State start = _changeStart;

            if (start == null)
            {
                return;
            }

            _changeStart = null;

            if (!keep)
            {
                if (_changed)
                {
                    Restore(start);
                }

                return;
            }

            if (_changed)
            {
                _undo.Push(start);
                _redo.Clear();
                Raise();
            }
        }

        /// <summary>Puts a column break at the caret (Alt+Enter), over the selection: the words after it start the next column.</summary>
        public void InsertColumnBreak()
        {
            Save();
            RemoveSelection();
            InsertBreak(true);
            Anchor = Caret;
            Raise();
        }

        /// <summary>Gets whether the words differ from what they were when the editor opened.</summary>
        public bool IsModified =>
            !string.Equals(Contents, Original, StringComparison.Ordinal)
            && !string.Equals(
                Contents,
                CadMTextContent.Parse(Original, Height, StyleWidth).Write(Height, DefaultAlignment),
                StringComparison.Ordinal
            );

        /// <summary>Gets the words with their codes, as AutoCAD writes them.</summary>
        public string Contents => Content.Write(Height, DefaultAlignment);

        /// <summary>Gets the words as paragraphs of runs, for drawing them.</summary>
        public CadMTextContent Content
        {
            get
            {
                var paragraphs = new List<CadMTextParagraph>();
                var runs = new List<CadMTextRun>();
                int paragraph = 0;
                bool startsColumn = false;

                foreach (Cell cell in _cells)
                {
                    if (cell.Break)
                    {
                        paragraphs.Add(
                            new CadMTextParagraph(runs, _alignments[paragraph], _paragraphCodes[paragraph])
                            {
                                StartsColumn = startsColumn,
                            }
                        );
                        runs = new List<CadMTextRun>();
                        paragraph++;
                        startsColumn = cell.Column;
                        continue;
                    }

                    if (
                        cell.Code == null
                        && runs.Count > 0
                        && runs[runs.Count - 1].Code == null
                        && runs[runs.Count - 1].Look.Equals(cell.Look)
                    )
                    {
                        runs[runs.Count - 1] = new CadMTextRun(runs[runs.Count - 1].Text + cell.Text, cell.Look);
                    }
                    else
                    {
                        runs.Add(new CadMTextRun(cell.Text, cell.Look, cell.Code));
                    }
                }

                paragraphs.Add(
                    new CadMTextParagraph(runs, _alignments[paragraph], _paragraphCodes[paragraph])
                    {
                        StartsColumn = startsColumn,
                    }
                );
                return CadMTextContent.Of(paragraphs);
            }
        }

        /// <summary>Gets the look of a place: of its letter, the look the letters typed there take for a break.</summary>
        /// <param name="index">The place.</param>
        /// <returns>The look.</returns>
        public CadMTextLook LookAt(int index) =>
            index >= 0 && index < _cells.Count ? _cells[index].Look : CadMTextLook.Plain;

        /// <summary>Gets the letters a place shows: a line feed for a paragraph break.</summary>
        /// <param name="index">The place.</param>
        /// <returns>The letters.</returns>
        public string TextAt(int index) => _cells[index].Break ? "\n" : _cells[index].Text;

        /// <summary>Moves the caret, the selection grown from its other end where <paramref name="extend"/>, as Shift does.</summary>
        /// <param name="place">The place, kept within 0 and <see cref="Length"/>.</param>
        /// <param name="extend">Whether the selection grows to it.</param>
        public void MoveTo(int place, bool extend = false)
        {
            Caret = Math.Max(0, Math.Min(_cells.Count, place));

            if (!extend)
            {
                Anchor = Caret;
            }

            _pending = null;
            Raise();
        }

        /// <summary>Selects places.</summary>
        /// <param name="start">The first.</param>
        /// <param name="length">How many.</param>
        public void Select(int start, int length)
        {
            Anchor = Math.Max(0, Math.Min(_cells.Count, start));
            Caret = Math.Max(0, Math.Min(_cells.Count, start + length));
            _pending = null;
            Raise();
        }

        /// <summary>Selects every place, as Ctrl+A.</summary>
        public void SelectAll() => Select(0, _cells.Count);

        /// <summary>Types letters over the selection, each line feed a new paragraph, in the look of the letters typed there.</summary>
        /// <param name="text">The letters.</param>
        public void Type(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            Save();
            CadMTextLook look = TypingLook;
            RemoveSelection();
            text = text.Replace("\r\n", "\n").Replace('\r', '\n');

            foreach (char c in text)
            {
                if (c == '\n')
                {
                    InsertBreak();
                }
                else if (c == '\t' || !char.IsControl(c))
                {
                    _cells.Insert(Caret, new Cell(c.ToString(), look, null));
                    Caret++;
                }
            }

            Anchor = Caret;
            _pending = look;
            Raise();
        }

        /// <summary>Inserts a symbol as AutoCAD's Symbol menu writes it: shown as its letter, written as its code (<c>%%d</c>, <c>\U+2248</c>).</summary>
        /// <param name="shown">The letter shown, as °.</param>
        /// <param name="code">The code written.</param>
        /// <exception cref="ArgumentException">Thrown when either is empty.</exception>
        public void InsertSymbol(string shown, string code)
        {
            if (string.IsNullOrEmpty(shown) || string.IsNullOrEmpty(code))
            {
                throw new ArgumentException(
                    "A symbol has a letter and a code.",
                    string.IsNullOrEmpty(shown) ? nameof(shown) : nameof(code)
                );
            }

            Save();
            CadMTextLook look = TypingLook;
            RemoveSelection();
            _cells.Insert(Caret, new Cell(shown, look, code));
            Caret++;
            Anchor = Caret;
            Raise();
        }

        /// <summary>Backspace: the selection taken out, or the place before the caret.</summary>
        public void Backspace()
        {
            if (SelectionLength == 0 && Caret == 0)
            {
                return;
            }

            Save();

            if (SelectionLength == 0)
            {
                Anchor = Caret - 1;
            }

            RemoveSelection();
            Raise();
        }

        /// <summary>Delete: the selection taken out, or the place after the caret.</summary>
        public void Delete()
        {
            if (SelectionLength == 0 && Caret >= _cells.Count)
            {
                return;
            }

            Save();

            if (SelectionLength == 0)
            {
                Anchor = Caret + 1;
            }

            RemoveSelection();
            Raise();
        }

        /// <summary>Makes the letters bold or not (Ctrl+B); nothing on letters of an SHX font.</summary>
        /// <param name="on">Whether bold.</param>
        public void SetBold(bool on) => Restyle(l => Face(l, on, l.Italic));

        /// <summary>Makes the letters italic or not (Ctrl+I); nothing on letters of an SHX font.</summary>
        /// <param name="on">Whether italic.</param>
        public void SetItalic(bool on) => Restyle(l => Face(l, l.Bold, on));

        /// <summary>Underlines the letters or not (Ctrl+U).</summary>
        /// <param name="on">Whether underlined.</param>
        public void SetUnderline(bool on) => Restyle(l => l.WithUnderline(on));

        /// <summary>Overlines the letters or not.</summary>
        /// <param name="on">Whether overlined.</param>
        public void SetOverline(bool on) => Restyle(l => l.WithOverline(on));

        /// <summary>Strikes the letters through or not.</summary>
        /// <param name="on">Whether struck through.</param>
        public void SetStrikethrough(bool on) => Restyle(l => l.WithStrikethrough(on));

        /// <summary>Gives the letters a font: a TrueType family keeping bold and italic, or an SHX file; null for the style's.</summary>
        /// <param name="font">The family or file.</param>
        /// <param name="shx">Whether it is an SHX file.</param>
        public void SetFont(string font, bool shx) =>
            Restyle(l =>
                shx
                    ? l.WithShx(font)
                    : l.WithTrueType(
                        font,
                        font == null && StyleIsShx ? false : l.Bold,
                        font == null && StyleIsShx ? false : l.Italic
                    )
            );

        /// <summary>Gives the letters a colour of the index: 256 ByLayer, 0 ByBlock; null for the MTEXT's own.</summary>
        /// <param name="index">The index.</param>
        public void SetColorIndex(int? index) => Restyle(l => l.WithColorIndex(index));

        /// <summary>Gives the letters a height in drawing units; the MTEXT's own height or less than nought is none of their own.</summary>
        /// <param name="height">The height.</param>
        public void SetHeight(double height) =>
            Restyle(l => l.WithHeight(height > 0.0 && height != Height ? (double?)height : null));

        /// <summary>Slants the letters by degrees; nought is none of their own.</summary>
        /// <param name="degrees">The slant.</param>
        public void SetOblique(double degrees) => Restyle(l => l.WithOblique(degrees == 0.0 ? (double?)null : degrees));

        /// <summary>Spaces the letters by a factor of their usual spacing; 1 is none of their own.</summary>
        /// <param name="factor">The factor, above nought.</param>
        public void SetTracking(double factor) =>
            Restyle(l => l.WithTracking(factor > 0.0 && factor != 1.0 ? (double?)factor : null));

        /// <summary>Widens or narrows the letters by a factor; 1 is none of their own.</summary>
        /// <param name="factor">The factor, above nought.</param>
        public void SetWidthFactor(double factor) =>
            Restyle(l => l.WithWidthFactor(factor > 0.0 && factor != 1.0 ? (double?)factor : null));

        /// <summary>Clears the codes of the selected letters (Clear formatting), as AutoCAD's Remove Character Formatting.</summary>
        public void ClearFormatting() => Restyle(l => CadMTextLook.Plain);

        /// <summary>Changes the selected letters to capitals or to small letters, as AutoCAD's Change Case.</summary>
        /// <param name="upper">Whether to capitals.</param>
        public void ChangeCase(bool upper)
        {
            if (SelectionLength == 0)
            {
                return;
            }

            Save();

            for (int i = SelectionStart; i < SelectionStart + SelectionLength; i++)
            {
                Cell cell = _cells[i];

                if (!cell.Break && cell.Code == null)
                {
                    _cells[i] = new Cell(
                        upper ? cell.Text.ToUpperInvariant() : cell.Text.ToLowerInvariant(),
                        cell.Look,
                        null
                    );
                }
            }

            Raise();
        }

        /// <summary>Aligns the paragraphs the selection or the caret is in: l, c, r, j or d.</summary>
        /// <param name="alignment">The alignment.</param>
        /// <exception cref="ArgumentException">Thrown when the alignment is none of l, c, r, j and d.</exception>
        public void SetAlignment(char alignment)
        {
            if ("lcrjd".IndexOf(alignment) < 0)
            {
                throw new ArgumentException("An alignment is l, c, r, j or d.", nameof(alignment));
            }

            Save();
            int first = ParagraphOf(SelectionStart);
            int last = ParagraphOf(SelectionStart + SelectionLength);

            for (int p = first; p <= last; p++)
            {
                _alignments[p] = alignment;
            }

            Raise();
        }

        /// <summary>Finds words after the caret, the search going round to the start, and selects them.</summary>
        /// <param name="find">The words.</param>
        /// <param name="matchCase">Whether capitals must match.</param>
        /// <returns>Whether they were found.</returns>
        public bool FindNext(string find, bool matchCase)
        {
            if (string.IsNullOrEmpty(find))
            {
                return false;
            }

            string text = Text;
            StringComparison comparison = matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            int from = Math.Min(text.Length, SelectionStart + SelectionLength);
            int found = text.IndexOf(find, from, comparison);

            if (found < 0)
            {
                found = text.IndexOf(find, 0, comparison);
            }

            if (found < 0)
            {
                return false;
            }

            Select(found, find.Length);
            return true;
        }

        /// <summary>Replaces every match of words with others, each in the look of the letter it starts at, as one change; Find and Replace's Replace All.</summary>
        /// <param name="find">The words.</param>
        /// <param name="replace">What they become; null is nothing.</param>
        /// <param name="matchCase">Whether capitals must match.</param>
        /// <returns>How many were replaced.</returns>
        public int ReplaceAll(string find, string replace, bool matchCase)
        {
            if (string.IsNullOrEmpty(find))
            {
                return 0;
            }

            StringComparison comparison = matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            int count = 0;
            int from = 0;
            bool saved = false;

            while (true)
            {
                int found = Text.IndexOf(find, from, comparison);

                if (found < 0)
                {
                    break;
                }

                if (!saved)
                {
                    Save();
                    saved = true;
                }

                CadMTextLook look = _cells[found].Look;
                Anchor = found;
                Caret = found + find.Length;
                RemoveSelection();

                foreach (char c in replace ?? string.Empty)
                {
                    if (c == '\n')
                    {
                        InsertBreak();
                    }
                    else
                    {
                        _cells.Insert(Caret, new Cell(c.ToString(), look, null));
                        Caret++;
                    }
                }

                Anchor = Caret;
                from = Caret;
                count++;
            }

            if (count > 0)
            {
                Raise();
            }

            return count;
        }

        /// <summary>Takes the last change back.</summary>
        /// <returns>Whether there was one.</returns>
        public bool Undo()
        {
            if (_undo.Count == 0)
            {
                return false;
            }

            _redo.Push(Capture());
            Restore(_undo.Pop());
            return true;
        }

        /// <summary>Makes the last change taken back again.</summary>
        /// <returns>Whether there was one.</returns>
        public bool Redo()
        {
            if (_redo.Count == 0)
            {
                return false;
            }

            _undo.Push(Capture());
            Restore(_redo.Pop());
            return true;
        }

        /// <summary>The paragraph a place is in, counted from nought.</summary>
        /// <param name="place">The place.</param>
        /// <returns>The paragraph.</returns>
        public int ParagraphOf(int place)
        {
            int paragraph = 0;

            for (int i = 0; i < place && i < _cells.Count; i++)
            {
                if (_cells[i].Break)
                {
                    paragraph++;
                }
            }

            return paragraph;
        }

        private CadMTextLook TypingLook
        {
            get
            {
                if (_pending != null)
                {
                    return _pending;
                }

                // The look of the letter before the caret, or after it at the start of a paragraph, as AutoCAD types on.
                if (Caret > 0 && !_cells[Caret - 1].Break)
                {
                    return _cells[Caret - 1].Look;
                }

                return Caret < _cells.Count && !_cells[Caret].Break ? _cells[Caret].Look : CadMTextLook.Plain;
            }
        }

        /// <summary>
        /// A look made bold or italic, or not, in its TrueType font or the style's; nothing for an SHX font. Letters of the
        /// style's own font neither bold nor italic take no font code, so that bold set and taken off again writes nothing.
        /// </summary>
        private CadMTextLook Face(CadMTextLook look, bool bold, bool italic)
        {
            if (look.Font != null ? look.IsShx : StyleIsShx)
            {
                return look;
            }

            string font = look.Font ?? StyleFont;
            bool own = font == null || string.Equals(font, StyleFont, StringComparison.OrdinalIgnoreCase);
            return !bold && !italic && own
                ? look.WithTrueType(null, false, false)
                : look.WithTrueType(font, bold, italic);
        }

        private void Restyle(Func<CadMTextLook, CadMTextLook> change)
        {
            if (SelectionLength == 0)
            {
                _pending = change(TypingLook);
                Raise();
                return;
            }

            Save();

            for (int i = SelectionStart; i < SelectionStart + SelectionLength; i++)
            {
                Cell cell = _cells[i];

                if (!cell.Break)
                {
                    _cells[i] = new Cell(cell.Text, change(cell.Look), cell.Code);
                }
            }

            Raise();
        }

        private void RemoveSelection()
        {
            int start = SelectionStart;
            int count = SelectionLength;

            if (count == 0)
            {
                return;
            }

            // The paragraphs a removed break joined: the first keeps its alignment.
            int paragraph = ParagraphOf(start);

            for (int i = start; i < start + count; i++)
            {
                if (_cells[i].Break)
                {
                    _alignments.RemoveAt(paragraph + 1);
                    _paragraphCodes.RemoveAt(paragraph + 1);
                }
            }

            _cells.RemoveRange(start, count);
            Caret = start;
            Anchor = start;
        }

        private void InsertBreak(bool column = false)
        {
            int paragraph = ParagraphOf(Caret);
            _cells.Insert(Caret, column ? Cell.ColumnBreak : Cell.ParagraphBreak);
            _alignments.Insert(paragraph + 1, _alignments[paragraph]);
            _paragraphCodes.Insert(paragraph + 1, _paragraphCodes[paragraph]);
            Caret++;
        }

        /// <summary>The codes of a paragraph that are not its indents, stops or spacing, kept as they were (\pn... and the like).</summary>
        /// <summary>
        /// The paragraph codes with the paragraphs from <paramref name="first"/> to <paramref name="last"/> changed, each written
        /// as what differs from the paragraph before and the paragraph after them written back to how it was; null where no
        /// format changes.
        /// </summary>
        private string[] FormatCodes(Func<CadMTextParagraphFormat, CadMTextParagraphFormat> change, int first, int last)
        {
            int count = _paragraphCodes.Count;
            var formats = new CadMTextParagraphFormat[count];
            CadMTextParagraphFormat format = CadMTextParagraphFormat.Default;

            for (int p = 0; p < count; p++)
            {
                format = CadMTextParagraphFormat.Read(format, _paragraphCodes[p], Height);
                formats[p] = format;
            }

            var wanted = (CadMTextParagraphFormat[])formats.Clone();
            bool changed = false;

            for (int p = first; p <= last; p++)
            {
                wanted[p] = change(formats[p]) ?? formats[p];
                changed |= !wanted[p].Equals(formats[p]);
            }

            if (!changed)
            {
                return null;
            }

            string[] codes = _paragraphCodes.ToArray();

            for (int p = first; p <= Math.Min(last + 1, count - 1); p++)
            {
                CadMTextParagraphFormat before = p == 0 ? CadMTextParagraphFormat.Default : wanted[p - 1];
                string written = wanted[p].WriteAfter(before, Height) + OtherCodes(_paragraphCodes[p]);
                codes[p] = written.Length == 0 ? null : written;
            }

            return codes;
        }

        /// <summary>
        /// How many places a list's label takes at the start of a paragraph, its tab with it: a bullet, a number or letters and
        /// a full stop, then a tab; nought where the paragraph opens with none.
        /// </summary>
        private int LabelLength(int start)
        {
            int i = start;

            while (i < _cells.Count && !_cells[i].Break && _cells[i].Code == null && _cells[i].Text != "\t")
            {
                i++;
            }

            if (i >= _cells.Count || _cells[i].Break || _cells[i].Code != null || i == start)
            {
                return 0;
            }

            var label = new StringBuilder();

            for (int k = start; k < i; k++)
            {
                label.Append(_cells[k].Text);
            }

            string text = label.ToString();
            bool bullet = text == "·" || text == "•";
            bool numbered =
                text.Length >= 2
                && text[text.Length - 1] == '.'
                && (
                    text.Take(text.Length - 1).All(c => c >= '0' && c <= '9')
                    || text.Take(text.Length - 1).All(c => c >= 'a' && c <= 'z')
                    || text.Take(text.Length - 1).All(c => c >= 'A' && c <= 'Z')
                );
            return bullet || numbered ? i - start + 1 : 0;
        }

        /// <summary>The label of the <paramref name="index"/>th paragraph of a list, from nought, its tab after it, in the look of the paragraph's first letter.</summary>
        private List<Cell> Label(CadMTextNumbering numbering, int index, int start)
        {
            if (numbering == CadMTextNumbering.Bullet)
            {
                return new List<Cell> { new Cell("·", BulletLook, null), new Cell("\t", BulletLook, null) };
            }

            string mark;

            if (numbering == CadMTextNumbering.Number)
            {
                mark = (index + 1).ToString(CultureInfo.InvariantCulture);
            }
            else
            {
                // a to z, then aa, ab and on as the columns of a sheet are named.
                var letters = new StringBuilder();

                for (int n = index; n >= 0; n = (n / 26) - 1)
                {
                    letters.Insert(0, (char)((numbering == CadMTextNumbering.LetterUpper ? 'A' : 'a') + (n % 26)));
                }

                mark = letters.ToString();
            }

            CadMTextLook look = start < _cells.Count && !_cells[start].Break ? _cells[start].Look : CadMTextLook.Plain;
            return (mark + ".\t").Select(c => new Cell(c.ToString(), look, null)).ToList();
        }

        /// <summary>A place moved by a label of <paramref name="removed"/> places at <paramref name="start"/> replaced by one of <paramref name="added"/>: one inside the old label goes after the new.</summary>
        private static int Shifted(int place, int start, int removed, int added) =>
            place >= start + removed ? place + added - removed
            : place > start ? start + added
            : place;

        private static string OtherCodes(string codes)
        {
            if (string.IsNullOrEmpty(codes))
            {
                return string.Empty;
            }

            var builder = new StringBuilder();

            foreach (string group in CadMTextParagraphFormat.Groups(codes))
            {
                bool times = group.StartsWith("x", StringComparison.Ordinal);
                var kept = new List<string>();
                bool tabbing = false;

                foreach (string item in CadMTextParagraphFormat.Items(times ? group.Substring(1) : group))
                {
                    if (item.Length == 0)
                    {
                        continue;
                    }

                    tabbing |= item[0] == 't';

                    if (!tabbing && "ilrbas".IndexOf(item[0]) < 0)
                    {
                        kept.Add(item);
                    }
                }

                if (kept.Count > 0)
                {
                    builder.Append("\\p").Append(times ? "x" : string.Empty).Append(string.Join(",", kept)).Append(';');
                }
            }

            return builder.ToString();
        }

        private void Load(CadMTextContent content)
        {
            _cells = new List<Cell>();
            _alignments = new List<char>();
            _paragraphCodes = new List<string>();

            for (int p = 0; p < content.Paragraphs.Count; p++)
            {
                CadMTextParagraph paragraph = content.Paragraphs[p];

                if (p > 0)
                {
                    _cells.Add(paragraph.StartsColumn ? Cell.ColumnBreak : Cell.ParagraphBreak);
                }

                _alignments.Add(paragraph.Alignment);
                _paragraphCodes.Add(paragraph.Codes);

                foreach (CadMTextRun run in paragraph.Runs)
                {
                    if (run.Code != null)
                    {
                        _cells.Add(new Cell(run.Text, run.Look, run.Code));
                        continue;
                    }

                    foreach (char c in run.Text)
                    {
                        _cells.Add(new Cell(c.ToString(), run.Look, null));
                    }
                }
            }
        }

        private void Save()
        {
            // A drag of the ruler is one step, taken when it ends.
            if (_changeStart != null)
            {
                _changed = true;
                return;
            }

            _undo.Push(Capture());
            _redo.Clear();
        }

        private State Capture() =>
            new State(
                new List<Cell>(_cells),
                new List<char>(_alignments),
                new List<string>(_paragraphCodes),
                Caret,
                Anchor,
                Frame
            );

        private void Restore(State state)
        {
            _cells = new List<Cell>(state.Cells);
            _alignments = new List<char>(state.Alignments);
            _paragraphCodes = new List<string>(state.Codes);
            Caret = state.Caret;
            Anchor = state.Anchor;
            Frame = state.Frame;
            _pending = null;
            Raise();
        }

        private void Raise() => Changed?.Invoke(this, EventArgs.Empty);

        /// <summary>A place of the words: a letter, a symbol kept as its code, or a paragraph or column break.</summary>
        private sealed class Cell
        {
            public static readonly Cell ParagraphBreak = new Cell("\n", CadMTextLook.Plain, null, true);

            public static readonly Cell ColumnBreak = new Cell("\n", CadMTextLook.Plain, null, true, true);

            public Cell(string text, CadMTextLook look, string code, bool isBreak = false, bool isColumn = false)
            {
                Text = text;
                Look = look;
                Code = code;
                Break = isBreak;
                Column = isColumn;
            }

            public string Text { get; }

            public CadMTextLook Look { get; }

            public string Code { get; }

            public bool Break { get; }

            /// <summary>Whether a break starts the next column (<c>\N</c>).</summary>
            public bool Column { get; }
        }

        private sealed class State
        {
            public State(
                List<Cell> cells,
                List<char> alignments,
                List<string> codes,
                int caret,
                int anchor,
                CadMTextFrame frame
            )
            {
                Cells = cells;
                Alignments = alignments;
                Codes = codes;
                Caret = caret;
                Anchor = anchor;
                Frame = frame;
            }

            public List<Cell> Cells { get; }

            public List<char> Alignments { get; }

            public List<string> Codes { get; }

            public int Caret { get; }

            public int Anchor { get; }

            public CadMTextFrame Frame { get; }
        }
    }
}
