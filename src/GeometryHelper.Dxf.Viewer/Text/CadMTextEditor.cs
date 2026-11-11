using System;
using System.Collections.Generic;
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
        private readonly Stack<State> _undo = new Stack<State>();
        private readonly Stack<State> _redo = new Stack<State>();
        private List<Cell> _cells = new List<Cell>();
        private List<char> _alignments = new List<char>();
        private List<string> _paragraphCodes = new List<string>();
        private CadMTextLook _pending;

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
        {
            Height = height;
            StyleFont = styleFont;
            StyleIsShx = styleIsShx;
            DefaultAlignment = defaultAlignment == '\0' ? 'l' : defaultAlignment;
            Original = contents ?? string.Empty;
            Load(CadMTextContent.Parse(Original, height));
        }

        /// <summary>Raised when the words, the looks, the caret or the selection change.</summary>
        public event EventHandler Changed;

        /// <summary>Gets the words as they were when the editor opened.</summary>
        public string Original { get; }

        /// <summary>Gets the MTEXT's height, of which a run's height is written as a factor.</summary>
        public double Height { get; }

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

        /// <summary>Gets whether a change can be taken back.</summary>
        public bool CanUndo => _undo.Count > 0;

        /// <summary>Gets whether a change taken back can be made again.</summary>
        public bool CanRedo => _redo.Count > 0;

        /// <summary>Gets whether the words differ from what they were when the editor opened.</summary>
        public bool IsModified =>
            !string.Equals(Contents, Original, StringComparison.Ordinal)
            && !string.Equals(
                Contents,
                CadMTextContent.Parse(Original, Height).Write(Height, DefaultAlignment),
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

                foreach (Cell cell in _cells)
                {
                    if (cell.Break)
                    {
                        paragraphs.Add(new CadMTextParagraph(runs, _alignments[paragraph], _paragraphCodes[paragraph]));
                        runs = new List<CadMTextRun>();
                        paragraph++;
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

                paragraphs.Add(new CadMTextParagraph(runs, _alignments[paragraph], _paragraphCodes[paragraph]));
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

        private void InsertBreak()
        {
            int paragraph = ParagraphOf(Caret);
            _cells.Insert(Caret, Cell.ParagraphBreak);
            _alignments.Insert(paragraph + 1, _alignments[paragraph]);
            _paragraphCodes.Insert(paragraph + 1, _paragraphCodes[paragraph]);
            Caret++;
        }

        private void Load(CadMTextContent content)
        {
            _cells = new List<Cell>();
            _alignments = new List<char>();
            _paragraphCodes = new List<string>();

            for (int p = 0; p < content.Paragraphs.Count; p++)
            {
                if (p > 0)
                {
                    _cells.Add(Cell.ParagraphBreak);
                }

                CadMTextParagraph paragraph = content.Paragraphs[p];
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
            _undo.Push(Capture());
            _redo.Clear();
        }

        private State Capture() =>
            new State(
                new List<Cell>(_cells),
                new List<char>(_alignments),
                new List<string>(_paragraphCodes),
                Caret,
                Anchor
            );

        private void Restore(State state)
        {
            _cells = new List<Cell>(state.Cells);
            _alignments = new List<char>(state.Alignments);
            _paragraphCodes = new List<string>(state.Codes);
            Caret = state.Caret;
            Anchor = state.Anchor;
            _pending = null;
            Raise();
        }

        private void Raise() => Changed?.Invoke(this, EventArgs.Empty);

        /// <summary>A place of the words: a letter, a symbol kept as its code, or a paragraph break.</summary>
        private sealed class Cell
        {
            public static readonly Cell ParagraphBreak = new Cell("\n", CadMTextLook.Plain, null, true);

            public Cell(string text, CadMTextLook look, string code, bool isBreak = false)
            {
                Text = text;
                Look = look;
                Code = code;
                Break = isBreak;
            }

            public string Text { get; }

            public CadMTextLook Look { get; }

            public string Code { get; }

            public bool Break { get; }
        }

        private sealed class State
        {
            public State(List<Cell> cells, List<char> alignments, List<string> codes, int caret, int anchor)
            {
                Cells = cells;
                Alignments = alignments;
                Codes = codes;
                Caret = caret;
                Anchor = anchor;
            }

            public List<Cell> Cells { get; }

            public List<char> Alignments { get; }

            public List<string> Codes { get; }

            public int Caret { get; }

            public int Anchor { get; }
        }
    }
}
