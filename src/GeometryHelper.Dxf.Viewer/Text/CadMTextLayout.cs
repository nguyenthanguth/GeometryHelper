using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// The words of an MTEXT laid out as AutoCAD 2021 lays them, in the MTEXT's own frame: its location at nought, x along its
    /// direction and y up. What is drawn, picked, gripped and exploded reads this one layout.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Wrapping. A line breaks at spaces only, never after a hyphen, and a word longer than the width stays whole. A word moves
    /// down when the pen at its start and its ink, taken from its own left edge, go past the width; one that ends on it stays.
    /// The spaces at a break are dropped; those inside a line are kept; a hard space (<c>\~</c>) joins its neighbours. A stack
    /// is one word. A width of nought never wraps, and the box is then as wide as the widest line.
    /// </para>
    /// <para>
    /// Across. A line is set by its ink: its first ink at the left edge, its ink centred, or its last ink at the right edge, as
    /// the paragraph says (<c>\pxqc;</c>) or, where it says nothing, as the attachment does. The left edge is the paragraph's
    /// left indent, with the first-line indent on its first line; the right edge the width less the right indent. A justified
    /// line shares what is left over its spaces, but for the paragraph's last; a distributed one after every letter. A tab
    /// moves the pen to the next stop, those of the paragraph and then every four heights of the MTEXT, and the next word
    /// starts, is centred or ends there, or has its decimal point there.
    /// </para>
    /// <para>
    /// Down, "at least" (H the MTEXT's height, f its factor). The first baseline lies the line's top below the top of the
    /// box, the top the greater of its tallest letters' height and their ink; each next line's top lies f times (the box of
    /// the line before, its top and a third of its tallest height, and H / 3) below the top before. Lines of plain letters are
    /// f * 5/3 * H apart, and a taller run pushes its line and the next apart. "Exactly": the first baseline lies H below
    /// the top and the baselines f * 5/3 * H apart whatever the letters. A paragraph may multiply the pitch
    /// (<c>\pxsm2;</c>), fix it (<c>\pxse2;</c>) or bound it from below (<c>\pxsa2;</c>), and stand apart from its
    /// neighbours (<c>\pxb1,a1;</c>).
    /// </para>
    /// <para>
    /// A stack (<c>\S1/2;</c>) is drawn at the height of its run, each part as high as its ink from bottom to top (digits
    /// the height, a hyphen an eighth of it): the lower part on the baseline, the upper part its height and a third of the
    /// run's above it and a bar a sixth above the lower part, as wide as the wider part, both centred on it; a tolerance
    /// (<c>^</c>) the same without the bar, both at the left; a diagonal fraction (<c>#</c>) the upper part raised the
    /// lower part's height, a gap of a third and the lower part on the baseline, with a stroke two heights long leaning
    /// 55 degrees between them.
    /// </para>
    /// <para>
    /// Columns stand a width and a gutter apart. A line stays in its column while its baseline and the ink below it come
    /// inside the column's height; a column break (<c>\N</c>) starts the next. The last of a fixed number takes the rest.
    /// </para>
    /// </remarks>
    internal sealed class CadMTextLayout
    {
        /// <summary>How far past its ink a stack moves the pen, over its height: a seventh in an SHX font, a fifteenth in a TrueType one, as AutoCAD 2021 sets ghsimplex and Arial.</summary>
        private const double StackGapShx = 1.0 / 7.0;
        private const double StackGapTrueType = 1.0 / 15.0;

        private readonly List<Piece> _pieces = new List<Piece>();
        private readonly List<Stroke> _strokes = new List<Stroke>();

        private CadMTextLayout()
        {
        }

        /// <summary>The runs of letters, each where its pen starts on its baseline.</summary>
        internal IReadOnlyList<Piece> Pieces => _pieces;

        /// <summary>The lines drawn with the letters: under, over and through them, and the bars of stacks.</summary>
        internal IReadOnlyList<Stroke> Strokes => _strokes;

        /// <summary>How many lines the words make.</summary>
        internal int LineCount { get; private set; }

        /// <summary>How many columns the lines stand in: 1 without columns.</summary>
        internal int ColumnCount { get; private set; }

        /// <summary>The width of the widest line's ink, as AutoCAD's ActualWidth.</summary>
        internal double ActualWidth { get; private set; }

        /// <summary>The height of the lines from the top of the first to the ink below the last, of the tallest column, as AutoCAD's ActualHeight.</summary>
        internal double ActualHeight { get; private set; }

        /// <summary>The box of the ink of the lines: left, bottom, right, top.</summary>
        internal double[] Ink { get; private set; }

        /// <summary>The box the words are set in, by the defined width (the widest line with none) and the actual height: left, bottom, right, top. A frame and a mask are this box grown.</summary>
        internal double[] TextBox { get; private set; }

        /// <summary>The box the MTEXT is defined by: its width, all its columns, and the greater of its defined and its actual height, put at the attachment point: left, bottom, right, top.</summary>
        internal double[] Box { get; private set; }

        /// <summary>Lays out an MTEXT's words. The face of a look is asked of <paramref name="faceOf"/>, the style's where the look names no font.</summary>
        internal static CadMTextLayout Of(DxfMText text, CadMTextContent content, Func<CadMTextLook, CadFontFace> faceOf, double styleWidth, double styleOblique)
        {
            var layout = new CadMTextLayout();
            new Builder(layout, text, content, faceOf, styleWidth, styleOblique, false).Build();
            return layout;
        }

        /// <summary>
        /// Lays out the words of an MTEXT being edited as <see cref="Of"/> does, and finds where each place of the editor
        /// (<see cref="CadMTextEditor"/>: a letter, a symbol or a stack one, a paragraph break one) stands: <see cref="Places"/>.
        /// An empty paragraph at the end is a line of its own here, for the caret after the last break to stand on.
        /// </summary>
        internal static CadMTextLayout OfEditor(DxfMText text, CadMTextContent content, Func<CadMTextLook, CadFontFace> faceOf, double styleWidth, double styleOblique)
        {
            var layout = new CadMTextLayout();
            new Builder(layout, text, content, faceOf, styleWidth, styleOblique, true).Build();
            return layout;
        }

        /// <summary>Where each place of the words stands; null but for a layout made by <see cref="OfEditor"/>.</summary>
        internal PlaceMap Places { get; private set; }

        /// <summary>
        /// Where each place of the words being edited stands, in the frame of the layout: the left and the right of its letter,
        /// and its line. A place the layout does not set, a space dropped at a break or a paragraph break, stands after the
        /// place before it on that place's line, or at the left of its paragraph's first line. The place after the last is
        /// one more, where the caret stands at the end.
        /// </summary>
        internal sealed class PlaceMap
        {
            internal PlaceMap(int count)
            {
                X = new double[count + 1];
                Right = new double[count + 1];
                Line = new int[count + 1];

                for (int i = 0; i <= count; i++)
                {
                    Line[i] = -1;
                }
            }

            internal double[] X { get; }

            internal double[] Right { get; }

            internal int[] Line { get; }

            internal List<LineBox> Lines { get; } = new List<LineBox>();
        }

        /// <summary>A line of the words being edited: its baseline, the top of its letters and the foot of its cell, where an empty line's caret stands, and its column.</summary>
        internal sealed class LineBox
        {
            internal double Baseline { get; set; }

            internal double Top { get; set; }

            internal double Bottom { get; set; }

            internal double Left { get; set; }

            internal int Column { get; set; }

            internal double ColumnLeft { get; set; }

            internal double ColumnRight { get; set; }
        }

        /// <summary>A run of letters of one look on a line: where its pen starts on its baseline, and how it is drawn.</summary>
        internal sealed class Piece
        {
            internal string Text { get; set; }

            internal double X { get; set; }

            internal double Y { get; set; }

            internal CadMTextLook Look { get; set; }

            internal CadFontFace Face { get; set; }

            internal CadTextInk Ink { get; set; }

            internal double Height { get; set; }

            internal double WidthFactor { get; set; }

            /// <summary>The slant in radians.</summary>
            internal double Slant { get; set; }

            internal bool Bold { get; set; }

            internal bool Italic { get; set; }

            /// <summary>How far the letters move the pen.</summary>
            internal double Advance { get; set; }

            internal int Line { get; set; }

            internal int Column { get; set; }
        }

        /// <summary>A straight line drawn with the letters, in the colour of a look.</summary>
        internal sealed class Stroke
        {
            internal double X0 { get; set; }

            internal double Y0 { get; set; }

            internal double X1 { get; set; }

            internal double Y1 { get; set; }

            internal CadMTextLook Look { get; set; }

            /// <summary>Whether it is the bar of a stack, which EXPLODE makes a LINE of; a line under, over or through letters is written in their TEXT.</summary>
            internal bool IsBar { get; set; }
        }

        private enum Kind
        {
            Word,
            Spaces,
            Tab,
            Stack,
        }

        /// <summary>A piece a line is made of: letters with no space among them, a run of spaces, a tab or a stack.</summary>
        private sealed class Atom
        {
            public Kind Kind;
            public string Text;
            public CadMTextLook Look;
            public CadFontFace Face;
            public CadTextInk Ink;
            public double H;
            public double Wf;
            public double Slant;
            public bool Bold;
            public bool Italic;
            public double Track = 1.0;
            public bool Joins;
            public int Letters;

            // The editor's place of its first letter; the whole atom one place where Single (a stack, a symbol).
            public int Place;
            public bool Single;

            // From the pen at its start, on the baseline, upright.
            public double Advance;
            public double Left;
            public double Right;
            public double Top;
            public double Bottom;
            public bool HasInk;

            // A stack: its parts, where each starts, the bar or the diagonal, and how high it stands.
            public string Upper;
            public string Lower;
            public double UpperX;
            public double UpperY;
            public double LowerX;
            public double UpperAdvance;
            public double LowerAdvance;
            public double[] Bar;
            public double StackHigh;

            // Placed.
            public double X;
            public double Spread;
        }

        /// <summary>How a paragraph is set, carried on to the paragraphs after it until one changes it.</summary>
        private sealed class Format
        {
            public double Indent;
            public double Left;
            public double Right;
            public double Before;
            public double After;
            public char Mode;
            public double Factor = 1.0;
            public List<KeyValuePair<double, char>> Tabs = new List<KeyValuePair<double, char>>();

            public Format Copy()
            {
                var copy = (Format)MemberwiseClone();
                copy.Tabs = new List<KeyValuePair<double, char>>(Tabs);
                return copy;
            }
        }

        private sealed class Line
        {
            public readonly List<Atom> Atoms = new List<Atom>();
            public Format Format;
            public char Align;
            public bool First;
            public bool Last;
            public bool StartsColumn;
            public bool HasTab;
            public double Left;
            public double InkRight;
            public double Top;
            public double BoxHigh;
            public double Depth;
            public double Baseline;
            public int Column;
            public double Shift;
            public double Lead;
            public double Push;
        }

        private sealed class Builder
        {
            private readonly CadMTextLayout _layout;
            private readonly DxfMText _text;
            private readonly CadMTextContent _content;
            private readonly Func<CadMTextLook, CadFontFace> _faceOf;
            private readonly double _styleWidth;
            private readonly double _styleOblique;
            private readonly double _h;
            private readonly double _width;
            private readonly bool _columns;
            private readonly bool _editing;
            private readonly List<Line> _lines = new List<Line>();
            private readonly List<int> _paragraphPlace = new List<int>();
            private readonly List<int> _paragraphLine = new List<int>();

            public Builder(CadMTextLayout layout, DxfMText text, CadMTextContent content, Func<CadMTextLook, CadFontFace> faceOf, double styleWidth, double styleOblique, bool editing)
            {
                _editing = editing;
                _layout = layout;
                _text = text;
                _content = content;
                _faceOf = faceOf;
                _styleWidth = styleWidth;
                _styleOblique = styleOblique;
                _h = text.TextHeight;
                _columns = text.ColumnType != DxfColumnType.NoColumns && text.ColumnWidth > 0.0;
                _width = _columns ? text.ColumnWidth : Math.Max(0.0, text.Width);
            }

            public void Build()
            {
                int attach = (int)_text.Attachment;
                int across = attach >= 1 && attach <= 9 ? (attach - 1) % 3 : 0;
                int down = attach >= 1 && attach <= 9 ? (attach - 1) / 3 : 0;
                var format = new Format();
                int count = _content.Paragraphs.Count;
                int place = 0;

                for (int p = 0; p < count; p++)
                {
                    CadMTextParagraph paragraph = _content.Paragraphs[p];
                    format = Read(format, paragraph.Codes);
                    _paragraphPlace.Add(place);
                    _paragraphLine.Add(_lines.Count);

                    // An empty paragraph at the end is no line: "ABC\P" is one line, "ABC\P\P" two; in the editor it is the caret's.
                    if (p == count - 1 && p > 0 && paragraph.Runs.Count == 0 && !_editing)
                    {
                        break;
                    }

                    char align = paragraph.Alignment == '\0' ? "lcr"[across] : paragraph.Alignment;
                    Wrap(Atoms(paragraph, place), format, align, paragraph.StartsColumn);

                    foreach (CadMTextRun run in paragraph.Runs)
                    {
                        place += run.Code != null ? 1 : run.Text.Length;
                    }

                    place++;
                }

                if (_editing)
                {
                    _layout.Places = new PlaceMap(Math.Max(0, place - 1));
                }

                // The box the lines are set across: the width, or with none the widest line and its indents.
                double box = _width;

                if (!(box > 0.0))
                {
                    foreach (Line line in _lines)
                    {
                        box = Math.Max(box, line.InkRight + line.Format.Right);
                    }
                }

                foreach (Line line in _lines)
                {
                    Align(line, box);
                }

                Place(box, across, down);
            }

            /// <summary>The format a paragraph's codes leave: <c>\pxi-3,l3,t3;</c> an indent, a left edge and a tab stop, in heights of the MTEXT after x and in units without.</summary>
            private Format Read(Format before, string codes)
            {
                if (string.IsNullOrEmpty(codes))
                {
                    return before;
                }

                Format format = before.Copy();
                int at = 0;

                while (true)
                {
                    int start = codes.IndexOf("\\p", at, StringComparison.Ordinal);
                    int end = start < 0 ? -1 : codes.IndexOf(';', start);

                    if (end < 0)
                    {
                        return format;
                    }

                    string value = codes.Substring(start + 2, end - start - 2);
                    at = end + 1;
                    bool times = value.StartsWith("x", StringComparison.Ordinal);
                    double unit = times ? _h : 1.0;
                    bool tabs = false;

                    foreach (string raw in (times ? value.Substring(1) : value).Split(','))
                    {
                        string item = raw.Trim();

                        if (item.Length == 0)
                        {
                            continue;
                        }

                        if (item[0] == 't' && !tabs)
                        {
                            tabs = true;
                            format.Tabs.Clear();
                            item = item.Substring(1);
                        }

                        if (tabs)
                        {
                            char kind = item.Length > 0 && "lcrd".IndexOf(item[0]) >= 0 ? item[0] : 'l';
                            string stop = item.Length > 0 && "lcrd".IndexOf(item[0]) >= 0 ? item.Substring(1) : item;

                            if (Number(stop, out double place) && place > 0.0)
                            {
                                format.Tabs.Add(new KeyValuePair<double, char>(place * unit, kind));
                            }

                            continue;
                        }

                        string rest = item.Substring(1);
                        bool reset = rest == "*";

                        switch (item[0])
                        {
                            case 'i':
                                format.Indent = reset ? 0.0 : Number(rest, out double indent) ? indent * unit : format.Indent;
                                break;
                            case 'l':
                                format.Left = reset ? 0.0 : Number(rest, out double left) ? left * unit : format.Left;
                                break;
                            case 'r':
                                format.Right = reset ? 0.0 : Number(rest, out double right) ? right * unit : format.Right;
                                break;
                            case 'b':
                                format.Before = reset ? 0.0 : Number(rest, out double b) ? (times ? Math.Min(4.0, Math.Max(0.25, b)) : Math.Max(0.0, b)) * unit : format.Before;
                                break;
                            case 'a':
                                format.After = reset ? 0.0 : Number(rest, out double a) ? (times ? Math.Min(4.0, Math.Max(0.25, a)) : Math.Max(0.0, a)) * unit : format.After;
                                break;
                            case 's':
                                if (reset || rest.Length == 0 || "mea".IndexOf(rest[0]) < 0)
                                {
                                    format.Mode = '\0';
                                    format.Factor = 1.0;
                                }
                                else if (Number(rest.Substring(1), out double factor) && factor > 0.0)
                                {
                                    format.Mode = rest[0];
                                    format.Factor = rest[0] == 'm' ? factor : Math.Min(4.0, Math.Max(0.25, times ? factor : factor / _h));
                                }

                                break;
                        }
                    }

                    format.Tabs.Sort((x, y) => x.Key != y.Key ? x.Key.CompareTo(y.Key) : x.Value.CompareTo(y.Value));
                }
            }

            private static bool Number(string text, out double number)
            {
                return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out number) && !double.IsNaN(number) && !double.IsInfinity(number);
            }

            /// <summary>The atoms of a paragraph, measured, each given the editor's place of its first letter, the paragraph's first at <paramref name="place"/>.</summary>
            private List<Atom> Atoms(CadMTextParagraph paragraph, int place)
            {
                var atoms = new List<Atom>();
                int previous = ' ';

                foreach (CadMTextRun run in paragraph.Runs)
                {
                    CadMTextLook look = run.Look;
                    string code = run.Code;
                    int runPlace = place;
                    int firstAtom = atoms.Count;
                    place += code != null ? 1 : run.Text.Length;

                    if (code != null && code.Length > 3 && code.StartsWith("\\S", StringComparison.Ordinal) && code[code.Length - 1] == ';')
                    {
                        Atom stack = Stack(look, code.Substring(2, code.Length - 3));
                        stack.Joins = previous != ' ' && previous != '\t';
                        stack.Place = runPlace;
                        stack.Single = true;
                        atoms.Add(stack);
                        previous = '1';
                        continue;
                    }

                    // A code that is not known is drawn as it is written, as AutoCAD draws \M+1A1A.
                    string words = code != null && code.Length == 2 && code[0] == '\\' && run.Text.Length == 1 && code[1] == run.Text[0] ? code : run.Text;
                    int i = 0;

                    while (i < words.Length)
                    {
                        char c = words[i];
                        int from = i;

                        if (c == '\t')
                        {
                            atoms.Add(New(Kind.Tab, "\t", look));
                            previous = '\t';
                            i++;
                            continue;
                        }

                        if (c == ' ')
                        {
                            while (i < words.Length && words[i] == ' ')
                            {
                                i++;
                            }

                            Atom spaces = New(Kind.Spaces, words.Substring(from, i - from), look);
                            Measure(spaces);
                            atoms.Add(spaces);
                            previous = ' ';
                            continue;
                        }

                        int before = previous;
                        int last = previous;

                        while (i < words.Length && words[i] != ' ' && words[i] != '\t')
                        {
                            int letter = char.IsHighSurrogate(words[i]) && i + 1 < words.Length && char.IsLowSurrogate(words[i + 1]) ? char.ConvertToUtf32(words[i], words[i + 1]) : words[i];

                            if (i > from && DxfMTextLayout.Breaks(last, letter))
                            {
                                break;
                            }

                            last = letter;
                            i += letter > 0xFFFF ? 2 : 1;
                        }

                        Atom word = New(Kind.Word, words.Substring(from, i - from), look);
                        int first = char.IsHighSurrogate(words[from]) && from + 1 < words.Length && char.IsLowSurrogate(words[from + 1]) ? char.ConvertToUtf32(words[from], words[from + 1]) : words[from];
                        word.Joins = before != ' ' && before != '\t' && !DxfMTextLayout.Breaks(before, first);
                        Measure(word);
                        atoms.Add(word);
                        previous = last;
                    }

                    // A symbol is one place however many letters it shows; plain letters a place each.
                    int at = 0;

                    for (int n = firstAtom; n < atoms.Count; n++)
                    {
                        atoms[n].Place = code != null ? runPlace : runPlace + at;
                        atoms[n].Single = code != null;
                        at += atoms[n].Text.Length;
                    }
                }

                // The spaces a paragraph ends with are not set.
                while (atoms.Count > 0 && atoms[atoms.Count - 1].Kind == Kind.Spaces)
                {
                    atoms.RemoveAt(atoms.Count - 1);
                }

                return atoms;
            }

            private Atom New(Kind kind, string text, CadMTextLook look)
            {
                CadFontFace face = _faceOf(look);
                bool bold = face.Shx == null && look.Bold;
                bool italic = face.Shx == null && look.Italic;

                return new Atom
                {
                    Kind = kind,
                    Text = text,
                    Look = look,
                    Face = face,
                    Ink = CadTextInk.Of(face, bold, italic),
                    H = look.Height ?? _h,
                    Wf = look.WidthFactor ?? _styleWidth,
                    Slant = look.Oblique.HasValue ? look.Oblique.Value * Math.PI / 180.0 : _styleOblique,
                    Bold = bold,
                    Italic = italic,
                    Track = look.Tracking ?? 1.0,
                };
            }

            /// <summary>Measures the letters of an atom: its advance and the box of its ink, a line over or under it taken in up and down.</summary>
            private static void Measure(Atom atom)
            {
                Span(atom.Ink, atom.Text, atom.H, atom.Wf, atom.Track, LeanOf(atom), out double advance, out double left, out double right, out double bottom, out double top, out bool inked, out int letters);
                atom.Advance = advance;
                atom.Left = left;
                atom.Right = right;
                atom.HasInk = inked;
                atom.Letters = letters;
                atom.Top = Math.Max(atom.H, inked ? top : 0.0);
                atom.Bottom = Math.Min(0.0, inked ? bottom : 0.0);

                if (atom.Look.Overline)
                {
                    atom.Top = Math.Max(atom.Top, 1.2 * atom.H);
                }

                if (atom.Look.Underline)
                {
                    atom.Bottom = Math.Min(atom.Bottom, -0.2 * atom.H);
                }
            }

            private static void Span(CadTextInk ink, string text, double h, double wf, double track, double lean, out double advance, out double left, out double right, out double bottom, out double top, out bool inked, out int letters)
            {
                advance = 0.0;
                left = right = bottom = top = 0.0;
                inked = false;
                letters = 0;
                double across = h * wf;
                int i = 0;

                while (i < text.Length)
                {
                    int at = i;
                    CadTextInk.Letter letter = ink.Next(text, ref i);
                    letters++;

                    if (letter.HasInk)
                    {
                        double l = letter.Left;
                        double r = letter.Right;

                        if (lean != 0.0)
                        {
                            ink.Lean(text, at, lean, ref l, ref r);
                        }

                        l = advance + (l * across);
                        r = advance + (r * across);
                        left = inked ? Math.Min(left, l) : l;
                        right = inked ? Math.Max(right, r) : r;
                        bottom = inked ? Math.Min(bottom, letter.Bottom * h) : letter.Bottom * h;
                        top = inked ? Math.Max(top, letter.Top * h) : letter.Top * h;
                        inked = true;
                    }

                    advance += letter.Advance * across * track;
                }
            }

            /// <summary>A stack measured and set in itself, as the class says.</summary>
            private Atom Stack(CadMTextLook look, string value)
            {
                var upper = new StringBuilder();
                var lower = new StringBuilder();
                char sign = '\0';

                for (int i = 0; i < value.Length; i++)
                {
                    char c = value[i];

                    if (c == '\\' && i + 1 < value.Length)
                    {
                        (sign == '\0' ? upper : lower).Append(value[++i]);
                    }
                    else if (sign == '\0' && (c == '^' || c == '/' || c == '#'))
                    {
                        sign = c;
                    }
                    else
                    {
                        (sign == '\0' ? upper : lower).Append(c);
                    }
                }

                Atom atom = New(Kind.Stack, value, look);
                double h = atom.H;
                atom.Upper = upper.ToString();
                atom.Lower = lower.ToString();
                atom.Letters = 1;
                Span(atom.Ink, atom.Upper, h, atom.Wf, atom.Track, LeanOf(atom), out atom.UpperAdvance, out double ul, out double ur, out double ub, out double ut, out bool uInked, out _);
                Span(atom.Ink, atom.Lower, h, atom.Wf, atom.Track, LeanOf(atom), out atom.LowerAdvance, out double ll, out double lr, out double lb, out double lt, out bool lInked, out _);
                double uw = uInked ? ur - ul : 0.0;
                double lw = lInked ? lr - ll : 0.0;
                double wide = Math.Max(uw, lw);

                // Each part is as high as its ink from bottom to top: Arial's 0 a fiftieth over its height, a y as high as a capital, a hyphen an eighth.
                double upperTop = uInked ? ut - ub : h;
                double lowerTop = lInked ? lt - lb : h;
                double end;

                if (sign == '#')
                {
                    // The upper part raised a height, a gap of a third, the lower part on the baseline, and between them a stroke two heights long at 55 degrees.
                    atom.UpperX = -ul;
                    atom.UpperY = lowerTop;
                    atom.LowerX = uw + (h / 3.0) - ll;
                    double cx = uw + (h / 6.0);
                    double dx = h * Math.Cos(55.0 * Math.PI / 180.0);
                    double dy = h * Math.Sin(55.0 * Math.PI / 180.0);
                    atom.Bar = new[] { cx - dx, lowerTop - dy, cx + dx, lowerTop + dy };
                    atom.StackHigh = lowerTop + upperTop;
                    end = uw + (h / 3.0) + lw;
                }
                else if (sign == '/')
                {
                    atom.UpperX = (wide / 2.0) - ((ul + ur) / 2.0);
                    atom.UpperY = lowerTop + (h / 3.0);
                    atom.LowerX = (wide / 2.0) - ((ll + lr) / 2.0);
                    atom.Bar = new[] { 0.0, lowerTop + (h / 6.0), wide, lowerTop + (h / 6.0) };
                    atom.StackHigh = lowerTop + (h / 3.0) + upperTop;
                    end = wide;
                }
                else
                {
                    // A tolerance: both parts at the left; an upper part alone raised a third, a lower part alone on the baseline.
                    atom.UpperX = -ul;
                    atom.UpperY = atom.Lower.Length == 0 ? h / 3.0 : lowerTop + (h / 3.0);
                    atom.LowerX = -ll;
                    atom.StackHigh = atom.Lower.Length == 0 ? (h / 3.0) + upperTop : atom.Upper.Length == 0 ? lowerTop : lowerTop + (h / 3.0) + upperTop;
                    end = wide;
                }

                atom.Left = 0.0;
                atom.Right = end;
                atom.HasInk = uInked || lInked;
                atom.Advance = end + (h * (atom.Ink.IsShx ? StackGapShx : StackGapTrueType));
                atom.Top = atom.StackHigh;
                atom.Bottom = 0.0;
                return atom;
            }

            /// <summary>How far a letter of an atom leans along its line for each unit it stands up, over its width.</summary>
            private static double LeanOf(Atom atom) => atom.Slant != 0.0 && atom.Wf > 0.0 ? Math.Tan(atom.Slant) / atom.Wf : 0.0;

            /// <summary>The ink of an atom from its own left edge: ink left of the pen is not counted in a TrueType font.</summary>
            private static double LeftOf(Atom atom) => !atom.HasInk ? 0.0 : atom.Ink.IsShx || atom.Kind == Kind.Stack ? atom.Left : Math.Max(0.0, atom.Left);

            /// <summary>Breaks the atoms of a paragraph into lines.</summary>
            private void Wrap(List<Atom> atoms, Format format, char align, bool startsColumn)
            {
                int firstLine = _lines.Count;
                Line line = Start(format, align, true, startsColumn);
                double right = _width - format.Right;
                bool wraps = _width > 0.0;
                double pen = line.Left;
                double origin = line.Left;
                bool any = false;
                int i = 0;

                while (i < atoms.Count)
                {
                    Atom a = atoms[i];

                    if (a.Kind == Kind.Spaces)
                    {
                        // The spaces after a break are dropped; those a paragraph starts with are set.
                        if (any || line.First)
                        {
                            a.X = pen;
                            pen += a.Advance;
                            line.Atoms.Add(a);
                            any = true;
                        }

                        i++;
                        continue;
                    }

                    // A tab and the word after it, or a word: the atoms up to where a line may break.
                    Atom tab = a.Kind == Kind.Tab ? a : null;
                    int j = tab != null ? i + 1 : i;
                    int k = j;
                    double advance = 0.0;
                    double inkLeft = 0.0;
                    double inkRight = 0.0;
                    bool inked = false;
                    double point = double.NaN;

                    while (k < atoms.Count && (atoms[k].Kind == Kind.Word || atoms[k].Kind == Kind.Stack) && (k == j || atoms[k].Joins))
                    {
                        Atom w = atoms[k];

                        if (w.HasInk)
                        {
                            double l = advance + (k == j ? LeftOf(w) : w.Left);
                            double r = advance + w.Right;
                            inkLeft = inked ? Math.Min(inkLeft, l) : l;
                            inkRight = inked ? Math.Max(inkRight, r) : r;
                            inked = true;
                        }

                        if (double.IsNaN(point) && w.Kind == Kind.Word && w.Text.IndexOf('.') >= 0)
                        {
                            Span(w.Ink, w.Text.Substring(0, w.Text.IndexOf('.')), w.H, w.Wf, w.Track, 0.0, out double to, out _, out _, out _, out _, out _, out _);
                            point = advance + to;
                        }

                        advance += w.Advance;
                        k++;
                    }

                    if (!any && tab == null)
                    {
                        // The first ink of a line stands at its left edge.
                        origin = line.Left - inkLeft;
                        line.Lead = inkLeft;
                        pen = origin;
                    }

                    double start = pen;
                    double after = double.NaN;

                    if (tab != null)
                    {
                        KeyValuePair<double, char> stop = Stop(format, pen);

                        // A word at a left stop stands with its ink on the stop, and the pen goes on as if it began there.
                        after = stop.Value == 'l' ? stop.Key + advance : double.NaN;
                        start = stop.Value == 'c' ? stop.Key - ((inkLeft + inkRight) / 2.0)
                            : stop.Value == 'r' ? stop.Key - inkRight
                            : stop.Value == 'd' ? stop.Key - (double.IsNaN(point) ? advance : point)
                            : stop.Key - inkLeft;
                    }

                    // A word that ends on the width fits, as "with end" 27 wide in ghsimplex 4.5 high stays one line in AutoCAD 2021.
                    if (wraps && any && !((start - origin) + (inkRight - inkLeft) <= right - line.Left + 1E-9))
                    {
                        End(line);
                        line = Start(format, align, false, false);
                        pen = line.Left;
                        origin = line.Left;
                        any = false;
                        continue;
                    }

                    if (tab != null)
                    {
                        tab.X = pen;
                        tab.Advance = start - pen;
                        line.Atoms.Add(tab);
                        line.HasTab = true;
                    }

                    pen = start;

                    for (int m = j; m < k; m++)
                    {
                        atoms[m].X = pen;
                        pen += atoms[m].Advance;
                        line.Atoms.Add(atoms[m]);
                    }

                    pen = double.IsNaN(after) ? pen : after;
                    any = true;
                    i = k;
                }

                End(line);
                _lines[_lines.Count - 1].Last = true;

                if (firstLine == _lines.Count - 1 && atoms.Count == 0)
                {
                    // An empty paragraph is a line as high as the MTEXT's letters.
                    line.Top = _h;
                    line.BoxHigh = _h + (_h / 3.0);
                }
            }

            private Line Start(Format format, char align, bool first, bool startsColumn)
            {
                var line = new Line { Format = format, Align = align, First = first, StartsColumn = startsColumn };
                line.Left = format.Left + (first ? format.Indent : 0.0);
                line.InkRight = line.Left;
                _lines.Add(line);
                return line;
            }

            /// <summary>The stop a tab moves the pen to: the next of the paragraph's past the pen, then the next of every four heights.</summary>
            private KeyValuePair<double, char> Stop(Format format, double pen)
            {
                foreach (KeyValuePair<double, char> stop in format.Tabs)
                {
                    if (stop.Key > pen + (1E-6 * _h))
                    {
                        return stop;
                    }
                }

                double step = 4.0 * _h;
                return new KeyValuePair<double, char>((Math.Floor((pen + (1E-6 * _h)) / step) + 1.0) * step, 'l');
            }

            /// <summary>Ends a line: the spaces it ends with are dropped, and its heights and the right of its ink are taken.</summary>
            private void End(Line line)
            {
                List<Atom> atoms = line.Atoms;

                Atom dropped = null;

                while (atoms.Count > 0 && atoms[atoms.Count - 1].Kind == Kind.Spaces && HasWord(atoms))
                {
                    dropped = atoms[atoms.Count - 1];
                    atoms.RemoveAt(atoms.Count - 1);
                }

                // In a text that wraps, a line whose dropped spaces are a run of their own, after a stack or in another font or size
                // than the word before them, begins with the pen at its left edge and not its ink: a B there stands 0.19 of
                // its height further right, whatever the line is aligned by.
                if (dropped != null && _width > 0.0 && atoms.Count > 0)
                {
                    Atom last = atoms[atoms.Count - 1];
                    bool own = last.Kind == Kind.Stack || !SameFace(last.Face, dropped.Face) || last.H != dropped.H || last.Wf != dropped.Wf
                        || last.Slant != dropped.Slant || last.Track != dropped.Track || last.Bold != dropped.Bold || last.Italic != dropped.Italic;
                    line.Push = own ? line.Lead : 0.0;
                }

                double textTop = 0.0;
                double textHigh = 0.0;
                double stackHigh = 0.0;
                bool words = false;
                line.InkRight = line.Left;

                foreach (Atom a in atoms)
                {
                    if (a.Kind == Kind.Stack)
                    {
                        stackHigh = Math.Max(stackHigh, a.StackHigh);
                        line.InkRight = Math.Max(line.InkRight, a.X + a.Right);
                        words = true;
                    }
                    else if (a.Kind == Kind.Word)
                    {
                        textTop = Math.Max(textTop, a.Top);
                        textHigh = Math.Max(textHigh, a.H);
                        line.InkRight = Math.Max(line.InkRight, a.HasInk ? a.X + a.Right : a.X + a.Advance);
                        words = true;
                    }
                    else if (a.Kind == Kind.Spaces)
                    {
                        textTop = Math.Max(textTop, a.H);
                        textHigh = Math.Max(textHigh, a.H);
                    }
                }

                if (!words && atoms.Count > 0)
                {
                    Atom last = atoms[atoms.Count - 1];
                    line.InkRight = last.X + last.Advance;
                }

                if (textHigh <= 0.0 && stackHigh <= 0.0)
                {
                    textTop = textHigh = _h;
                }

                line.Top = Math.Max(textTop, stackHigh);
                line.BoxHigh = Math.Max(textHigh > 0.0 ? textTop + (textHigh / 3.0) : 0.0, stackHigh);
            }

            private static bool SameFace(CadFontFace a, CadFontFace b)
            {
                return ReferenceEquals(a, b) || (ReferenceEquals(a.Shx, b.Shx) && ReferenceEquals(a.Big, b.Big) && string.Equals(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
            }

            private static bool HasWord(List<Atom> atoms)
            {
                foreach (Atom a in atoms)
                {
                    if (a.Kind != Kind.Spaces)
                    {
                        return true;
                    }
                }

                return false;
            }

            /// <summary>Sets a line across its box, as its paragraph is aligned.</summary>
            private void Align(Line line, double box)
            {
                double right = box - line.Format.Right;
                double spare = right - line.InkRight;

                switch (line.Align)
                {
                    case 'c':
                        line.Shift = spare / 2.0;
                        break;
                    case 'r':
                        line.Shift = spare;
                        break;
                    case 'j':
                        if (!line.Last && !line.HasTab && spare > 0.0)
                        {
                            int spaces = 0;

                            foreach (Atom a in line.Atoms)
                            {
                                spaces += a.Kind == Kind.Spaces ? a.Text.Length : 0;
                            }

                            double moved = 0.0;

                            foreach (Atom a in line.Atoms)
                            {
                                a.X += moved;

                                if (a.Kind == Kind.Spaces && spaces > 0)
                                {
                                    a.Spread = spare / spaces;
                                    moved += a.Text.Length * a.Spread;
                                }
                            }

                            line.InkRight += moved;
                        }

                        break;
                    case 'd':
                        if (!line.HasTab && spare > 0.0)
                        {
                            int letters = 0;

                            foreach (Atom a in line.Atoms)
                            {
                                letters += a.Kind == Kind.Stack ? 1 : a.Letters;
                            }

                            if (letters > 1)
                            {
                                double each = spare / (letters - 1);
                                double moved = 0.0;

                                foreach (Atom a in line.Atoms)
                                {
                                    a.X += moved;
                                    a.Spread = each;
                                    moved += (a.Kind == Kind.Stack ? 1 : a.Letters) * each;
                                }

                                line.InkRight += spare;
                            }
                        }

                        break;
                }
            }

            /// <summary>How far below the top of its column the first baseline of a column lies.</summary>
            private double Drop(Line line, bool exactly)
            {
                return Fall(line, exactly) + (line.First ? line.Format.Before : 0.0);
            }

            /// <summary>How far below the baseline before it a line's baseline lies.</summary>
            private double Pitch(Line before, Line line, bool exactly, double factor)
            {
                // Each line has a cell, as high as its own paragraph sets it, and its baseline a drop below the top of it: a
                // line set twice as far apart (8.333 for letters 2.5 high) before one set exactly at 4 (drop 10) is 8.333 - 2.5 + 10.
                Format g = before.Format;
                double third = _h / 3.0;
                double natural = exactly ? factor * 5.0 * third : factor * (before.BoxHigh + third);
                double cell = g.Mode == 'm' ? g.Factor * natural
                    : g.Mode == 'e' ? g.Factor * 5.0 * third
                    : g.Mode == 'a' ? Math.Max(natural, g.Factor * 5.0 * third)
                    : natural;
                Format own = line.Format;
                return cell - Fall(before, exactly) + Fall(line, exactly) + (before.Last ? g.After : 0.0) + (line.First ? own.Before : 0.0);
            }

            /// <summary>How far below the top of its cell the baseline of a line lies.</summary>
            private double Fall(Line line, bool exactly)
            {
                Format g = line.Format;
                return g.Mode == 'e' ? g.Factor * _h
                    : g.Mode == 'a' ? Math.Max(line.Top, (g.Factor * _h) + (_h / 3.0))
                    : exactly ? _h : line.Top;
            }

            /// <summary>Sets the lines down their columns, puts the whole at the attachment point and writes the pieces out.</summary>
            private void Place(double box, int across, int down)
            {
                bool exactly = _text.LineSpacingStyle == DxfLineSpacingStyle.Exactly;
                double factor = _text.LineSpacingFactor > 0.0 ? _text.LineSpacingFactor : 1.0;
                DxfColumnType type = _columns ? _text.ColumnType : DxfColumnType.NoColumns;
                bool manual = type == DxfColumnType.DynamicColumns && !_text.ColumnAutoHeight;
                int fixedCount = type == DxfColumnType.StaticColumns || manual ? Math.Max(1, _text.ColumnCount) : int.MaxValue;
                int column = 0;
                Line before = null;
                var highs = new List<double>();
                double depth = 0.0;

                foreach (Line line in _lines)
                {
                    // How far the ink of the line hangs below its baseline, a run raised in its line less.
                    double lineHigh = exactly ? _h : line.Top;
                    line.Depth = 0.0;

                    foreach (Atom a in line.Atoms)
                    {
                        line.Depth = Math.Max(line.Depth, -(a.Bottom + Rise(a, lineHigh)));
                    }

                    if (_columns && before != null && line.StartsColumn && line.First && column < fixedCount - 1)
                    {
                        highs.Add(High(before, depth, exactly));
                        column++;
                        before = null;
                    }

                    double at = before == null ? Drop(line, exactly) : depth + Pitch(before, line, exactly, factor);
                    double limit = ColumnHigh(type, manual, column);

                    if (_columns && before != null && limit > 0.0 && at + line.Depth > limit + 1E-9 && column < fixedCount - 1)
                    {
                        highs.Add(High(before, depth, exactly));
                        column++;
                        before = null;
                        at = Drop(line, exactly);
                    }

                    line.Column = column;
                    line.Baseline = -at;
                    depth = at;
                    before = line;
                }

                highs.Add(before == null ? 0.0 : High(before, depth, exactly));
                int count = _columns ? (fixedCount != int.MaxValue ? fixedCount : highs.Count) : 1;
                double gutter = _columns ? Math.Max(0.0, _text.ColumnGutterWidth) : 0.0;
                double total = (count * box) + ((count - 1) * gutter);
                double actualHigh = 0.0;

                foreach (double one in highs)
                {
                    actualHigh = Math.Max(actualHigh, one);
                }

                // The height the block is hung by: of the columns where there are any, else of the lines.
                double defined = 0.0;

                for (int c = 0; c < count && _columns; c++)
                {
                    defined = Math.Max(defined, ColumnHigh(type, manual, c));
                }

                double hung = _columns && defined > 0.0 ? defined : actualHigh;
                double left = across == 0 ? 0.0 : across == 1 ? -total / 2.0 : -total;
                double top = down == 0 ? 0.0 : down == 1 ? hung / 2.0 : hung;
                var shifts = new double[highs.Count];

                for (int c = 0; c < highs.Count; c++)
                {
                    double room = _columns && defined > 0.0 ? ColumnHigh(type, manual, c) : hung;
                    room = room > 0.0 ? room : hung;
                    shifts[c] = top - (down == 0 ? 0.0 : down == 1 ? (room - highs[c]) / 2.0 : room - highs[c]);
                }

                double inkLeft = double.MaxValue;
                double inkRight = double.MinValue;
                double inkBottom = double.MaxValue;
                double inkTop = double.MinValue;
                double widest = 0.0;

                for (int n = 0; n < _lines.Count; n++)
                {
                    Line line = _lines[n];
                    double x = left + (line.Column * (box + gutter)) + line.Shift + line.Push;
                    double y = line.Baseline + shifts[line.Column];
                    double lineHigh = exactly ? _h : line.Top;
                    Write(line, n, x, y, lineHigh);

                    if (_editing)
                    {
                        double columnLeft = left + (line.Column * (box + gutter));
                        Map(line, n, x, y, columnLeft, columnLeft + box);
                    }

                    if (line.Atoms.Count > 0)
                    {
                        inkLeft = Math.Min(inkLeft, x + line.Left);
                        inkRight = Math.Max(inkRight, x + line.InkRight);
                        widest = Math.Max(widest, line.InkRight - line.Left);
                    }

                    inkBottom = Math.Min(inkBottom, y - line.Depth);
                    inkTop = Math.Max(inkTop, y + line.Top);
                }

                if (inkLeft > inkRight)
                {
                    inkLeft = inkRight = left;
                }

                if (inkBottom > inkTop)
                {
                    inkBottom = inkTop = top;
                }

                // AutoCAD's box is as high as the columns are set, as the tallest is where each has a height of its own, and with no columns as the lines or the height given, whichever is more.
                double high = !_columns ? Math.Max(_text.Height, actualHigh) : manual || defined <= 0.0 ? actualHigh : defined;
                double boxTop = down == 0 ? 0.0 : down == 1 ? high / 2.0 : high;
                _layout.LineCount = _lines.Count;
                _layout.ColumnCount = count;
                _layout.ActualWidth = widest;
                _layout.ActualHeight = actualHigh;
                _layout.Ink = new[] { inkLeft, inkBottom, inkRight, inkTop };
                _layout.TextBox = new[] { left, top - (_columns && defined > 0.0 ? defined : actualHigh), left + total, top };
                _layout.Box = new[] { left, boxTop - high, left + total, boxTop };

                if (_editing)
                {
                    Fill();
                }
            }

            /// <summary>Sets where the places of a line's atoms stand, letter by letter, as <see cref="Write"/> sets the letters.</summary>
            private void Map(Line line, int number, double x, double y, double columnLeft, double columnRight)
            {
                PlaceMap map = _layout.Places;
                map.Lines.Add(new LineBox
                {
                    Baseline = y,
                    Top = y + line.Top,
                    Bottom = y - Math.Max(line.Depth, _h / 3.0),
                    Left = x + line.Left,
                    Column = line.Column,
                    ColumnLeft = columnLeft,
                    ColumnRight = columnRight,
                });

                foreach (Atom a in line.Atoms)
                {
                    double ax = x + a.X;

                    if (a.Single || a.Kind == Kind.Tab)
                    {
                        double end = ax + a.Advance + (a.Kind == Kind.Tab ? 0.0 : a.Letters * a.Spread);

                        if (map.Line[a.Place] < 0)
                        {
                            Set(map, a.Place, ax, end, number);
                        }
                        else
                        {
                            map.Right[a.Place] = Math.Max(map.Right[a.Place], end);
                        }

                        continue;
                    }

                    double pen = ax;
                    int i = 0;

                    while (i < a.Text.Length)
                    {
                        int from = i;
                        CadTextInk.Letter letter = a.Ink.Next(a.Text, ref i);
                        double step = letter.Advance * a.H * a.Wf * a.Track;

                        for (int k = from; k < i && a.Place + k < map.Line.Length; k++)
                        {
                            Set(map, a.Place + k, k == from ? pen : pen + step, pen + step + a.Spread, number);
                        }

                        pen += step + a.Spread;
                    }
                }
            }

            private static void Set(PlaceMap map, int place, double x, double right, int line)
            {
                map.X[place] = x;
                map.Right[place] = right;
                map.Line[place] = line;
            }

            /// <summary>Puts each place the lines did not set after the place before it, or at the left of its paragraph's first line.</summary>
            private void Fill()
            {
                PlaceMap map = _layout.Places;
                int end = map.Line.Length;

                for (int p = 0; p < _paragraphPlace.Count; p++)
                {
                    int first = _paragraphPlace[p];
                    int last = p + 1 < _paragraphPlace.Count ? _paragraphPlace[p + 1] - 1 : end - 1;

                    for (int q = first; q <= last && q < end; q++)
                    {
                        if (map.Line[q] >= 0)
                        {
                            continue;
                        }

                        if (q > first)
                        {
                            Set(map, q, map.Right[q - 1], map.Right[q - 1], map.Line[q - 1]);
                        }
                        else
                        {
                            int line = Math.Min(_paragraphLine[p], map.Lines.Count - 1);
                            double x = line >= 0 ? map.Lines[line].Left : 0.0;
                            Set(map, q, x, x, Math.Max(0, line));
                        }
                    }
                }
            }

            /// <summary>
            /// How high a column of lines is, from its top to below its last line: the ink below it, or a third of the MTEXT's
            /// height where the lines are set by a rule; under a paragraph set exactly, a third of the height for each of its
            /// factor and four more below the top of the last cell (5.833 for letters 2.5 high at 3).
            /// </summary>
            private double High(Line last, double depth, bool exactly)
            {
                Format g = last.Format;

                if (g.Mode == 'e')
                {
                    return depth - (g.Factor * _h) + ((g.Factor + 4.0) * _h / 3.0);
                }

                return depth + (exactly || g.Mode == 'm' || g.Mode == 'a' ? _h / 3.0 : last.Depth);
            }

            /// <summary>The height a column holds its lines in; nought for no bound.</summary>
            private double ColumnHigh(DxfColumnType type, bool manual, int column)
            {
                if (type == DxfColumnType.NoColumns)
                {
                    return 0.0;
                }

                if (manual)
                {
                    double own = column < _text.ColumnCount ? _text.GetColumnHeight(column) : 0.0;
                    return own > 0.0 ? own : _text.Height;
                }

                return _text.Height;
            }

            /// <summary>How far a run stands above the baseline of its line (<c>\A1;</c> centred, <c>\A2;</c> at the top).</summary>
            private static double Rise(Atom a, double lineHigh)
            {
                if (a.Kind == Kind.Stack || a.Kind == Kind.Tab || !a.Look.Align.HasValue)
                {
                    return 0.0;
                }

                return a.Look.Align.Value == 1 ? (lineHigh - a.H) / 2.0 : a.Look.Align.Value == 2 ? lineHigh - a.H : 0.0;
            }

            /// <summary>Writes the pieces and strokes of a line, the atoms that follow one another in one look as one piece.</summary>
            private void Write(Line line, int number, double x, double y, double lineHigh)
            {
                Piece open = null;
                Atom openAtom = null;

                foreach (Atom a in line.Atoms)
                {
                    if (a.Kind == Kind.Tab)
                    {
                        open = null;
                        continue;
                    }

                    double ax = x + a.X;

                    if (a.Kind == Kind.Stack)
                    {
                        open = null;

                        if (a.Upper.Length > 0)
                        {
                            Add(a, a.Upper, ax + a.UpperX, y + a.UpperY, number, line.Column).Advance = a.UpperAdvance;
                        }

                        if (a.Lower.Length > 0)
                        {
                            Add(a, a.Lower, ax + a.LowerX, y, number, line.Column).Advance = a.LowerAdvance;
                        }

                        if (a.Bar != null)
                        {
                            _layout._strokes.Add(new Stroke { X0 = ax + a.Bar[0], Y0 = y + a.Bar[1], X1 = ax + a.Bar[2], Y1 = y + a.Bar[3], Look = a.Look, IsBar = true });
                        }

                        continue;
                    }

                    double ay = y + Rise(a, lineHigh);
                    bool apart = a.Spread != 0.0 || a.Track != 1.0;
                    double end = ax + a.Advance + (a.Letters * a.Spread);

                    if (apart)
                    {
                        // Letters set apart are each a piece of its own.
                        open = null;
                        double pen = ax;
                        int i = 0;

                        while (i < a.Text.Length)
                        {
                            int from = i;
                            CadTextInk.Letter letter = a.Ink.Next(a.Text, ref i);
                            double step = letter.Advance * a.H * a.Wf * a.Track;

                            if (a.Kind == Kind.Word)
                            {
                                Add(a, a.Text.Substring(from, i - from), pen, ay, number, line.Column).Advance = step;
                            }

                            pen += step + a.Spread;
                        }
                    }
                    else if (open != null && ReferenceEquals(openAtom.Look, a.Look) && ReferenceEquals(openAtom.Face, a.Face) && Math.Abs(open.X + open.Advance - ax) < 1E-9 && Math.Abs(open.Y - ay) < 1E-9)
                    {
                        open.Text += a.Text;
                        open.Advance += a.Advance;
                    }
                    else
                    {
                        open = Add(a, a.Text, ax, ay, number, line.Column);
                        open.Advance = a.Advance;
                        openAtom = a;
                    }

                    Marks(a, ax, end, ay);
                }
            }

            private Piece Add(Atom a, string text, double x, double y, int line, int column)
            {
                var piece = new Piece
                {
                    Text = text,
                    X = x,
                    Y = y,
                    Look = a.Look,
                    Face = a.Face,
                    Ink = a.Ink,
                    Height = a.H,
                    WidthFactor = a.Wf,
                    Slant = a.Slant,
                    Bold = a.Bold,
                    Italic = a.Italic,
                    Line = line,
                    Column = column,
                };
                _layout._pieces.Add(piece);
                return piece;
            }

            /// <summary>The lines under, through and over an atom, each joined to the one before it where it goes on from it.</summary>
            private void Marks(Atom a, double from, double to, double y)
            {
                CadMTextLook look = a.Look;

                if (!look.Underline && !look.Strikethrough && !look.Overline)
                {
                    return;
                }

                for (int level = 0; level < 3; level++)
                {
                    if (!(level == 0 ? look.Underline : level == 1 ? look.Strikethrough : look.Overline))
                    {
                        continue;
                    }

                    double at = y + ((level == 0 ? -0.2 : level == 1 ? 0.5 : 1.2) * a.H);
                    Stroke joined = null;

                    for (int s = _layout._strokes.Count - 1; s >= 0 && s >= _layout._strokes.Count - 3; s--)
                    {
                        Stroke last = _layout._strokes[s];

                        if (ReferenceEquals(last.Look, look) && Math.Abs(last.Y0 - at) < 1E-9 && Math.Abs(last.Y1 - at) < 1E-9 && Math.Abs(last.X1 - from) < 1E-9)
                        {
                            joined = last;
                            break;
                        }
                    }

                    if (joined != null)
                    {
                        joined.X1 = to;
                    }
                    else
                    {
                        _layout._strokes.Add(new Stroke { X0 = from, Y0 = at, X1 = to, Y1 = at, Look = look });
                    }
                }
            }
        }
    }
}
