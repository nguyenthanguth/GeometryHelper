using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// The look of a run of letters of an MTEXT, as its inline codes set it: the font and whether it is bold or italic, the lines
    /// under, over and through, the colour, the height, the slant, the spacing and the width of the letters, and how the run
    /// stands in its line. A value left null (or false) is the MTEXT's own, so that <see cref="Plain"/> is the look of words
    /// written with no code at all. A look is never changed: each <c>With</c> gives a new one.
    /// </summary>
    public sealed class CadMTextLook : IEquatable<CadMTextLook>
    {
        private CadMTextLook()
        {
        }

        /// <summary>Gets the look of words with no code: everything the MTEXT's own.</summary>
        public static CadMTextLook Plain { get; } = new CadMTextLook();

        /// <summary>Gets the font a code switches to: a TrueType family (Arial) or an SHX file (romans); null for the MTEXT style's.</summary>
        public string Font { get; private set; }

        /// <summary>Gets whether <see cref="Font"/> is an SHX file (<c>\F</c>) rather than a TrueType family (<c>\f</c>).</summary>
        public bool IsShx { get; private set; }

        /// <summary>Gets whether the letters are bold (<c>|b1</c> of a TrueType font code).</summary>
        public bool Bold { get; private set; }

        /// <summary>Gets whether the letters are italic (<c>|i1</c> of a TrueType font code).</summary>
        public bool Italic { get; private set; }

        /// <summary>Gets the character set of the font code (<c>|c0</c>); null where the code gave none.</summary>
        public int? Charset { get; private set; }

        /// <summary>Gets the pitch and family of the font code (<c>|p34</c>); null where the code gave none.</summary>
        public int? Pitch { get; private set; }

        /// <summary>Gets whether the letters are underlined (<c>\L</c> to <c>\l</c>).</summary>
        public bool Underline { get; private set; }

        /// <summary>Gets whether the letters are overlined (<c>\O</c> to <c>\o</c>).</summary>
        public bool Overline { get; private set; }

        /// <summary>Gets whether the letters are struck through (<c>\K</c> to <c>\k</c>).</summary>
        public bool Strikethrough { get; private set; }

        /// <summary>Gets the colour index of <c>\C</c>: 0 ByBlock, 256 ByLayer, 1 to 255 a colour of the index; null for the MTEXT's own.</summary>
        public int? ColorIndex { get; private set; }

        /// <summary>Gets the true colour of <c>\c</c> as 0xRRGGBB; null for none.</summary>
        public int? TrueColor { get; private set; }

        /// <summary>Gets the height of the letters in drawing units (<c>\H</c>); null for the MTEXT's height.</summary>
        public double? Height { get; private set; }

        /// <summary>Gets the slant of the letters in degrees (<c>\Q</c>); null for the style's.</summary>
        public double? Oblique { get; private set; }

        /// <summary>Gets the spacing of the letters as a factor of their usual spacing (<c>\T</c>); null for 1.</summary>
        public double? Tracking { get; private set; }

        /// <summary>Gets the width factor of the letters (<c>\W</c>); null for the style's.</summary>
        public double? WidthFactor { get; private set; }

        /// <summary>Gets how the run stands in a line of taller letters (<c>\A</c>): 0 on the baseline, 1 at the middle, 2 at the top; null for the baseline.</summary>
        public int? Align { get; private set; }

        /// <summary>Gets whether this is <see cref="Plain"/> in all but the font: what the MTEXT's own codes before its words may set without a run of their own.</summary>
        public bool IsPlain => Equals(Plain);

        /// <summary>Gives the look with a TrueType font, bold and italic as given, the character set and pitch kept.</summary>
        /// <param name="family">The family, as Arial; null for the style's font.</param>
        /// <param name="bold">Whether bold.</param>
        /// <param name="italic">Whether italic.</param>
        /// <returns>The new look.</returns>
        public CadMTextLook WithTrueType(string family, bool bold, bool italic) => Change(l =>
        {
            l.Font = string.IsNullOrEmpty(family) ? null : family;
            l.IsShx = false;
            l.Bold = bold;
            l.Italic = italic;
        });

        /// <summary>Gives the look with an SHX font, which is neither bold nor italic.</summary>
        /// <param name="file">The file, as romans; null for the style's font.</param>
        /// <returns>The new look.</returns>
        public CadMTextLook WithShx(string file) => Change(l =>
        {
            l.Font = string.IsNullOrEmpty(file) ? null : file;
            l.IsShx = l.Font != null;
            l.Bold = false;
            l.Italic = false;
        });

        /// <summary>Gives the look underlined or not.</summary>
        /// <param name="on">Whether underlined.</param>
        /// <returns>The new look.</returns>
        public CadMTextLook WithUnderline(bool on) => Change(l => l.Underline = on);

        /// <summary>Gives the look overlined or not.</summary>
        /// <param name="on">Whether overlined.</param>
        /// <returns>The new look.</returns>
        public CadMTextLook WithOverline(bool on) => Change(l => l.Overline = on);

        /// <summary>Gives the look struck through or not.</summary>
        /// <param name="on">Whether struck through.</param>
        /// <returns>The new look.</returns>
        public CadMTextLook WithStrikethrough(bool on) => Change(l => l.Strikethrough = on);

        /// <summary>Gives the look in a colour of the index (0 ByBlock, 256 ByLayer), any true colour dropped; null for the MTEXT's own.</summary>
        /// <param name="index">The index.</param>
        /// <returns>The new look.</returns>
        public CadMTextLook WithColorIndex(int? index) => Change(l =>
        {
            l.ColorIndex = index;
            l.TrueColor = null;
        });

        /// <summary>Gives the look in a true colour, 0xRRGGBB, any colour index dropped; null for none.</summary>
        /// <param name="rgb">The colour.</param>
        /// <returns>The new look.</returns>
        public CadMTextLook WithTrueColor(int? rgb) => Change(l =>
        {
            l.TrueColor = rgb & 0xFFFFFF;
            l.ColorIndex = null;
        });

        /// <summary>Gives the look of a height in drawing units; null for the MTEXT's.</summary>
        /// <param name="height">The height.</param>
        /// <returns>The new look.</returns>
        public CadMTextLook WithHeight(double? height) => Change(l => l.Height = height);

        /// <summary>Gives the look of a slant in degrees; null for the style's.</summary>
        /// <param name="degrees">The slant.</param>
        /// <returns>The new look.</returns>
        public CadMTextLook WithOblique(double? degrees) => Change(l => l.Oblique = degrees);

        /// <summary>Gives the look of a spacing factor; null for 1.</summary>
        /// <param name="factor">The factor.</param>
        /// <returns>The new look.</returns>
        public CadMTextLook WithTracking(double? factor) => Change(l => l.Tracking = factor);

        /// <summary>Gives the look of a width factor; null for the style's.</summary>
        /// <param name="factor">The factor.</param>
        /// <returns>The new look.</returns>
        public CadMTextLook WithWidthFactor(double? factor) => Change(l => l.WidthFactor = factor);

        /// <summary>Gives the look standing in its line as <c>\A</c> says (0, 1, 2); null for the baseline.</summary>
        /// <param name="align">The alignment.</param>
        /// <returns>The new look.</returns>
        public CadMTextLook WithAlign(int? align) => Change(l => l.Align = align);

        /// <inheritdoc/>
        public bool Equals(CadMTextLook other)
        {
            // Values as written, compared exactly: two runs are one where their codes read the same.
            return other != null && string.Equals(Font, other.Font, StringComparison.OrdinalIgnoreCase) && IsShx == other.IsShx && Bold == other.Bold
                && Italic == other.Italic && Charset == other.Charset && Pitch == other.Pitch && Underline == other.Underline && Overline == other.Overline
                && Strikethrough == other.Strikethrough && ColorIndex == other.ColorIndex && TrueColor == other.TrueColor && Height == other.Height
                && Oblique == other.Oblique && Tracking == other.Tracking && WidthFactor == other.WidthFactor && Align == other.Align;
        }

        /// <inheritdoc/>
        public override bool Equals(object obj) => Equals(obj as CadMTextLook);

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = StringComparer.OrdinalIgnoreCase.GetHashCode(Font ?? string.Empty);
                hash = (hash * 31) + (Bold ? 1 : 0) + (Italic ? 2 : 0) + (Underline ? 4 : 0) + (Overline ? 8 : 0) + (Strikethrough ? 16 : 0) + (IsShx ? 32 : 0);
                hash = (hash * 31) + (ColorIndex ?? -1);
                hash = (hash * 31) + (TrueColor ?? -1);
                hash = (hash * 31) + (Height ?? 0.0).GetHashCode();
                return hash;
            }
        }

        internal CadMTextLook WithFontCode(string font, bool shx, bool bold, bool italic, int? charset, int? pitch) => Change(l =>
        {
            l.Font = font;
            l.IsShx = shx;
            l.Bold = bold;
            l.Italic = italic;
            l.Charset = charset;
            l.Pitch = pitch;
        });

        private CadMTextLook Change(Action<CadMTextLook> change)
        {
            var copy = (CadMTextLook)MemberwiseClone();
            change(copy);
            return copy;
        }
    }

    /// <summary>
    /// A run of an MTEXT's paragraph: letters of one look, or one code kept as written, a stacked fraction (<c>\S1/2;</c>), a
    /// symbol (<c>%%d</c>, <c>\U+2248</c>), shown as its letters and written back as it was.
    /// </summary>
    public sealed class CadMTextRun
    {
        /// <summary>Makes a run.</summary>
        /// <param name="text">The letters, as shown.</param>
        /// <param name="look">The look.</param>
        /// <param name="code">The code written for the run where it is one, as <c>%%d</c>; null for letters written as they are.</param>
        /// <exception cref="ArgumentNullException">Thrown when the text or the look is null.</exception>
        public CadMTextRun(string text, CadMTextLook look, string code = null)
        {
            Text = text ?? throw new ArgumentNullException(nameof(text));
            Look = look ?? throw new ArgumentNullException(nameof(look));
            Code = code;
        }

        /// <summary>Gets the letters as shown: ° for <c>%%d</c>, 1/2 for <c>\S1/2;</c>.</summary>
        public string Text { get; }

        /// <summary>Gets the look.</summary>
        public CadMTextLook Look { get; }

        /// <summary>Gets the code written for the run where it is one; null for letters.</summary>
        public string Code { get; }

        /// <inheritdoc/>
        public override string ToString() => Code ?? Text;
    }

    /// <summary>A paragraph of an MTEXT: its runs, how it is aligned (<c>\pxqc;</c>) and any other paragraph codes it carries, kept as written.</summary>
    public sealed class CadMTextParagraph
    {
        /// <summary>Makes a paragraph.</summary>
        /// <param name="runs">The runs.</param>
        /// <param name="alignment">How it is aligned: l, c, r, j or d as <c>\pxq</c> writes them; nought for as the MTEXT is attached.</param>
        /// <param name="codes">Other paragraph codes (<c>\pi-3,l3;</c>), kept as written; null for none.</param>
        /// <exception cref="ArgumentNullException">Thrown when the runs are null.</exception>
        public CadMTextParagraph(IReadOnlyList<CadMTextRun> runs, char alignment = '\0', string codes = null)
        {
            Runs = runs ?? throw new ArgumentNullException(nameof(runs));
            Alignment = alignment;
            Codes = string.IsNullOrEmpty(codes) ? null : codes;
        }

        /// <summary>Gets the runs, in order.</summary>
        public IReadOnlyList<CadMTextRun> Runs { get; }

        /// <summary>Gets how the paragraph is aligned: l, c, r, j or d; nought for as the MTEXT is attached.</summary>
        public char Alignment { get; }

        /// <summary>Gets the paragraph codes other than the alignment, as written; null for none.</summary>
        public string Codes { get; }

        /// <summary>Whether the paragraph was begun by a column break (<c>N</c>), its first line then set at the top of the next column.</summary>
        internal bool StartsColumn { get; set; }

        /// <summary>Gets the letters of the paragraph as shown.</summary>
        public string Text
        {
            get
            {
                var builder = new StringBuilder();

                foreach (CadMTextRun run in Runs)
                {
                    builder.Append(run.Text);
                }

                return builder.ToString();
            }
        }
    }

    /// <summary>
    /// The words of an MTEXT read into paragraphs of runs of one look, and written back in AutoCAD's inline codes. Reading takes
    /// every code AutoCAD 2021 writes: <c>\P</c> (and the column and dimension breaks <c>\N</c>, <c>\X</c>) between paragraphs,
    /// <c>\L \l \O \o \K \k</c> the lines under, over and through, <c>\f</c> and <c>\F</c> the fonts, <c>\C</c> and <c>\c</c> the
    /// colours, <c>\H</c> the height (<c>x</c> after it a factor of the height before), <c>\Q</c>, <c>\T</c>, <c>\W</c> the
    /// slant, spacing and width, <c>\A</c> the alignment in the line, <c>\p</c> the paragraph, braces around a look of its own,
    /// <c>\\ \{ \}</c> the letters themselves, <c>\~</c> a hard space; stacks (<c>\S</c>), symbols (<c>%%d</c>, <c>%%p</c>,
    /// <c>%%c</c>, <c>\U+2248</c>) and codes not known are kept as written, each one run.
    /// </summary>
    /// <remarks>
    /// Writing gives each run that is not <see cref="CadMTextLook.Plain"/> its codes as AutoCAD writes them: a font, colour,
    /// height, slant, spacing, width or alignment in braces, <c>{\fArial|b1|i0|c0|p34;Bold}</c>, <c>{\C1;red}</c>,
    /// <c>{\H1.5x;tall}</c> (the height a factor of the MTEXT's), and the lines alone as <c>\Lunder\l</c>,
    /// <c>\Oover\o</c>, <c>\Kthrough\k</c>; paragraphs after <c>\P</c>, each with <c>\pxqc;</c> where its alignment differs from
    /// the paragraph before it. "AB CD" of an MTEXT 2.5 high with CD bold in Arial writes <c>AB {\fArial|b1|i0|c0|p34;CD}</c>.
    /// </remarks>
    public sealed class CadMTextContent
    {
        private CadMTextContent(IReadOnlyList<CadMTextParagraph> paragraphs)
        {
            Paragraphs = paragraphs;
        }

        /// <summary>Gets the paragraphs, at least one.</summary>
        public IReadOnlyList<CadMTextParagraph> Paragraphs { get; }

        /// <summary>Gets the letters as shown, the paragraphs separated by line feeds.</summary>
        public string PlainText
        {
            get
            {
                var lines = new string[Paragraphs.Count];

                for (int i = 0; i < lines.Length; i++)
                {
                    lines[i] = Paragraphs[i].Text;
                }

                return string.Join("\n", lines);
            }
        }

        /// <summary>Gets whether every run is <see cref="CadMTextLook.Plain"/> but for one font, slant or width set before the first letter, and no paragraph is aligned: what a drawing of the words in one look shows whole.</summary>
        public bool IsOneLook
        {
            get
            {
                CadMTextLook first = null;

                foreach (CadMTextParagraph paragraph in Paragraphs)
                {
                    if (paragraph.Alignment != '\0')
                    {
                        return false;
                    }

                    foreach (CadMTextRun run in paragraph.Runs)
                    {
                        if (first == null)
                        {
                            first = run.Look;
                        }
                        else if (!first.Equals(run.Look))
                        {
                            return false;
                        }
                    }
                }

                // The font and the width set before the first letter are what the words drawn whole take; a bold or italic face is not.
                return first == null || (!first.Bold && !first.Italic && first.WithTrueType(null, false, false).WithWidthFactor(null).IsPlain);
            }
        }

        /// <summary>Makes the content of paragraphs.</summary>
        /// <param name="paragraphs">The paragraphs; none is taken as one empty paragraph.</param>
        /// <returns>The content.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the paragraphs are null.</exception>
        public static CadMTextContent Of(IEnumerable<CadMTextParagraph> paragraphs)
        {
            var list = new List<CadMTextParagraph>(paragraphs ?? throw new ArgumentNullException(nameof(paragraphs)));

            if (list.Count == 0)
            {
                list.Add(new CadMTextParagraph(Array.Empty<CadMTextRun>()));
            }

            return new CadMTextContent(list);
        }

        /// <summary>
        /// Reads the words of an MTEXT as its group 1 and 3 write them. A height set to the MTEXT's own is read as no height,
        /// so that <c>{\H2.5;A}</c> of an MTEXT 2.5 high is plain.
        /// </summary>
        /// <param name="contents">The words with their codes; null is none.</param>
        /// <param name="height">The MTEXT's height, which <c>\H2x;</c> doubles.</param>
        /// <returns>The content.</returns>
        public static CadMTextContent Parse(string contents, double height)
        {
            return Parse(contents, height, 1.0);
        }

        /// <summary>
        /// Reads the words as <see cref="Parse(string, double)"/> does, for laying them out: a width factor in times
        /// (<c>W1.2x;</c>) before any other is times the style's, 0.9 in a style of 0.75.
        /// </summary>
        /// <param name="contents">The words with their codes; null is none.</param>
        /// <param name="height">The MTEXT's height, which <c>H2x;</c> doubles.</param>
        /// <param name="styleWidth">The width factor of the MTEXT's style.</param>
        /// <returns>The content.</returns>
        public static CadMTextContent Parse(string contents, double height, double styleWidth)
        {
            return new Reader(contents ?? string.Empty, height, styleWidth > 0.0 ? styleWidth : 1.0).Read();
        }

        /// <summary>Writes the content in AutoCAD's codes, as the class says.</summary>
        /// <param name="height">The MTEXT's height, of which a run's height is written as a factor.</param>
        /// <param name="defaultAlignment">The alignment a paragraph has with no code, l, c or r, as the MTEXT's attachment gives it.</param>
        /// <returns>The words with their codes.</returns>
        public string Write(double height, char defaultAlignment = 'l')
        {
            var builder = new StringBuilder();
            char current = defaultAlignment;

            for (int p = 0; p < Paragraphs.Count; p++)
            {
                CadMTextParagraph paragraph = Paragraphs[p];

                if (p > 0)
                {
                    builder.Append("\\P");
                }

                if (paragraph.Codes != null)
                {
                    builder.Append(paragraph.Codes);
                }

                char wanted = paragraph.Alignment == '\0' ? defaultAlignment : paragraph.Alignment;

                if (wanted != current)
                {
                    builder.Append("\\pxq").Append(wanted).Append(';');
                    current = wanted;
                }

                List<CadMTextRun> runs = Merge(paragraph.Runs);

                foreach (CadMTextRun run in runs)
                {
                    WriteRun(builder, run, height);
                }
            }

            return builder.ToString();
        }

        /// <summary>The letters of a run as written, the codes letters stand for escaped: \\ \{ \} and \~ for a hard space.</summary>
        internal static string Escape(string text)
        {
            var builder = new StringBuilder(text.Length);

            foreach (char c in text)
            {
                switch (c)
                {
                    case '\\':
                        builder.Append("\\\\");
                        break;
                    case '{':
                        builder.Append("\\{");
                        break;
                    case '}':
                        builder.Append("\\}");
                        break;
                    case ' ':
                        builder.Append("\\~");
                        break;
                    default:
                        builder.Append(c);
                        break;
                }
            }

            return builder.ToString();
        }

        internal static string Number(double value) => value.ToString("0.####", CultureInfo.InvariantCulture);

        /// <summary>A colour of <c>\c</c>, written as Windows writes one, blue in the high byte (0xBBGGRR), as 0xRRGGBB, or back.</summary>
        private static int Swap(int color) => ((color & 0xFF) << 16) | (color & 0xFF00) | ((color >> 16) & 0xFF);

        private static List<CadMTextRun> Merge(IReadOnlyList<CadMTextRun> runs)
        {
            var merged = new List<CadMTextRun>(runs.Count);

            foreach (CadMTextRun run in runs)
            {
                if (run.Text.Length == 0 && run.Code == null)
                {
                    continue;
                }

                if (merged.Count > 0 && run.Code == null && merged[merged.Count - 1].Code == null && merged[merged.Count - 1].Look.Equals(run.Look))
                {
                    merged[merged.Count - 1] = new CadMTextRun(merged[merged.Count - 1].Text + run.Text, run.Look);
                }
                else
                {
                    merged.Add(run);
                }
            }

            return merged;
        }

        private static void WriteRun(StringBuilder builder, CadMTextRun run, double height)
        {
            CadMTextLook look = run.Look;
            string words = run.Code ?? Escape(run.Text);
            var codes = new StringBuilder();

            if (look.Font != null || look.Bold || look.Italic)
            {
                if (look.IsShx)
                {
                    codes.Append("\\F").Append(look.Font).Append("|c").Append((look.Charset ?? 0).ToString(CultureInfo.InvariantCulture)).Append(';');
                }
                else
                {
                    codes.Append("\\f").Append(look.Font ?? string.Empty)
                        .Append("|b").Append(look.Bold ? '1' : '0')
                        .Append("|i").Append(look.Italic ? '1' : '0')
                        .Append("|c").Append((look.Charset ?? 0).ToString(CultureInfo.InvariantCulture))
                        .Append("|p").Append((look.Pitch ?? 34).ToString(CultureInfo.InvariantCulture)).Append(';');
                }
            }

            if (look.ColorIndex.HasValue)
            {
                codes.Append("\\C").Append(look.ColorIndex.Value.ToString(CultureInfo.InvariantCulture)).Append(';');
            }
            else if (look.TrueColor.HasValue)
            {
                codes.Append("\\c").Append(Swap(look.TrueColor.Value).ToString(CultureInfo.InvariantCulture)).Append(';');
            }

            if (look.Height.HasValue)
            {
                codes.Append("\\H").Append(Number(height > 0.0 ? look.Height.Value / height : look.Height.Value)).Append(height > 0.0 ? "x;" : ";");
            }

            if (look.Oblique.HasValue)
            {
                codes.Append("\\Q").Append(Number(look.Oblique.Value)).Append(';');
            }

            if (look.Tracking.HasValue)
            {
                codes.Append("\\T").Append(look.Tracking.Value.ToString("0.0000", CultureInfo.InvariantCulture)).Append(';');
            }

            if (look.WidthFactor.HasValue)
            {
                codes.Append("\\W").Append(look.WidthFactor.Value.ToString("0.0000", CultureInfo.InvariantCulture)).Append(';');
            }

            if (look.Align.HasValue)
            {
                codes.Append("\\A").Append(look.Align.Value.ToString(CultureInfo.InvariantCulture)).Append(';');
            }

            string on = (look.Underline ? "\\L" : string.Empty) + (look.Overline ? "\\O" : string.Empty) + (look.Strikethrough ? "\\K" : string.Empty);
            string off = (look.Strikethrough ? "\\k" : string.Empty) + (look.Overline ? "\\o" : string.Empty) + (look.Underline ? "\\l" : string.Empty);

            if (codes.Length == 0)
            {
                builder.Append(on).Append(words).Append(off);
                return;
            }

            builder.Append('{').Append(codes).Append(on).Append(words).Append(off).Append('}');
        }

        /// <summary>Reads the codes of an MTEXT's words, as the class says.</summary>
        private sealed class Reader
        {
            private readonly string _text;
            private readonly double _height;
            private readonly double _styleWidth;
            private readonly Stack<CadMTextLook> _saved = new Stack<CadMTextLook>();
            private readonly List<CadMTextParagraph> _paragraphs = new List<CadMTextParagraph>();
            private List<CadMTextRun> _runs = new List<CadMTextRun>();
            private readonly StringBuilder _letters = new StringBuilder();
            private CadMTextLook _look = CadMTextLook.Plain;
            private CadMTextLook _lettersLook = CadMTextLook.Plain;
            private char _alignment;
            private string _paragraphCodes;
            private bool _startsColumn;
            private int _at;

            public Reader(string text, double height, double styleWidth)
            {
                _text = text;
                _height = height;
                _styleWidth = styleWidth;
            }

            public CadMTextContent Read()
            {
                while (_at < _text.Length)
                {
                    char c = _text[_at];

                    if (c == '{')
                    {
                        _saved.Push(_look);
                        _at++;
                    }
                    else if (c == '}')
                    {
                        _look = _saved.Count > 0 ? _saved.Pop() : _look;
                        _at++;
                    }
                    else if (c == '\\' && _at + 1 < _text.Length)
                    {
                        Code();
                    }
                    else if (c == '%' && _at + 2 < _text.Length && _text[_at + 1] == '%')
                    {
                        Percent();
                    }
                    else
                    {
                        Letter(c.ToString());
                        _at++;
                    }
                }

                EndParagraph();
                return new CadMTextContent(_paragraphs);
            }

            private void Code()
            {
                char code = _text[_at + 1];

                switch (code)
                {
                    case '\\':
                    case '{':
                    case '}':
                        Letter(code.ToString());
                        _at += 2;
                        return;
                    case '~':
                        Letter(" ");
                        _at += 2;
                        return;
                    case 'P':
                    case 'N':
                    case 'X':
                        EndParagraph();
                        _startsColumn = code == 'N';
                        _at += 2;
                        return;
                    case 'L':
                    case 'l':
                        _look = _look.WithUnderline(code == 'L');
                        _at += 2;
                        return;
                    case 'O':
                    case 'o':
                        _look = _look.WithOverline(code == 'O');
                        _at += 2;
                        return;
                    case 'K':
                    case 'k':
                        _look = _look.WithStrikethrough(code == 'K');
                        _at += 2;
                        return;
                    case 'U':
                        if (_at + 7 <= _text.Length && _text[_at + 2] == '+'
                            && int.TryParse(_text.Substring(_at + 3, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int unicode))
                        {
                            Symbol(((char)unicode).ToString(), _text.Substring(_at, 7));
                            _at += 7;
                            return;
                        }

                        break;
                }

                int end = _text.IndexOf(';', _at + 2);

                if ("fFHCcQTWApS".IndexOf(code) < 0 || end < 0)
                {
                    // A code not known: kept as written, its letter shown.
                    Symbol(code.ToString(), _text.Substring(_at, 2));
                    _at += 2;
                    return;
                }

                string value = _text.Substring(_at + 2, end - _at - 2);
                string written = _text.Substring(_at, end - _at + 1);
                _at = end + 1;

                switch (code)
                {
                    case 'f':
                    case 'F':
                        FontCode(value, code == 'F');
                        break;
                    case 'H':
                        if (TryNumber(value, out double heightValue, out bool times) && heightValue > 0.0)
                        {
                            double before = _look.Height ?? _height;
                            double now = times ? before * heightValue : heightValue;
                            _look = _look.WithHeight(now == _height ? (double?)null : now);
                        }

                        break;
                    case 'W':
                        if (TryNumber(value, out double width, out bool widthTimes) && width > 0.0)
                        {
                            _look = _look.WithWidthFactor(widthTimes ? (_look.WidthFactor ?? _styleWidth) * width : width);
                        }

                        break;
                    case 'Q':
                        if (TryNumber(value, out double slant, out _))
                        {
                            _look = _look.WithOblique(slant);
                        }

                        break;
                    case 'T':
                        if (TryNumber(value, out double tracking, out bool trackingTimes) && tracking > 0.0)
                        {
                            _look = _look.WithTracking(trackingTimes ? (_look.Tracking ?? 1.0) * tracking : tracking);
                        }

                        break;
                    case 'C':
                        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int index) && index >= 0 && index <= 257)
                        {
                            _look = _look.WithColorIndex(index);
                        }

                        break;
                    case 'c':
                        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int bgr))
                        {
                            _look = _look.WithTrueColor(Swap(bgr));
                        }

                        break;
                    case 'A':
                        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int align) && align >= 0 && align <= 2)
                        {
                            _look = _look.WithAlign(align == 0 ? (int?)null : align);
                        }

                        break;
                    case 'p':
                        ParagraphCode(written, value);
                        break;
                    case 'S':
                        Symbol(Stacked(value), written);
                        break;
                }
            }

            private void FontCode(string value, bool shx)
            {
                string[] parts = value.Split('|');
                string name = parts[0].Trim();
                bool bold = false;
                bool italic = false;
                int? charset = null;
                int? pitch = null;

                for (int i = 1; i < parts.Length; i++)
                {
                    string part = parts[i].Trim();

                    if (part.Length < 2 || !int.TryParse(part.Substring(1), NumberStyles.Integer, CultureInfo.InvariantCulture, out int number))
                    {
                        continue;
                    }

                    switch (char.ToLowerInvariant(part[0]))
                    {
                        case 'b':
                            bold = number != 0;
                            break;
                        case 'i':
                            italic = number != 0;
                            break;
                        case 'c':
                            charset = number;
                            break;
                        case 'p':
                            pitch = number;
                            break;
                    }
                }

                _look = _look.WithFontCode(name.Length == 0 ? null : name, shx && name.Length > 0, !shx && bold, !shx && italic, charset, shx ? null : pitch);
            }

            private void ParagraphCode(string written, string value)
            {
                // \pxqc; aligns the paragraph; the rest of a paragraph code (indents, tabs) is kept as written.
                string[] parts = value.Split(',');
                var kept = new List<string>();

                foreach (string raw in parts)
                {
                    string part = raw.Trim();
                    string bare = part.StartsWith("x", StringComparison.Ordinal) ? part.Substring(1) : part;

                    if (bare.Length == 2 && bare[0] == 'q' && "lcrjd".IndexOf(bare[1]) >= 0)
                    {
                        _alignment = bare[1];
                        continue;
                    }

                    kept.Add(part);
                }

                // The x before the first item says every value is in text heights: it stays with the items kept when its own is taken out.
                if (kept.Count > 0 && kept.Count < parts.Length && parts[0].TrimStart().StartsWith("x", StringComparison.Ordinal) && !kept[0].StartsWith("x", StringComparison.Ordinal))
                {
                    kept[0] = "x" + kept[0];
                }

                if (kept.Count > 0 && !(kept.Count == 1 && kept[0] == "x"))
                {
                    _paragraphCodes = (_paragraphCodes ?? string.Empty) + (kept.Count == parts.Length ? written : "\\p" + string.Join(",", kept) + ";");
                }
            }

            private void Percent()
            {
                char code = char.ToLowerInvariant(_text[_at + 2]);
                string shown;

                switch (code)
                {
                    case 'd':
                        shown = "°";
                        break;
                    case 'p':
                        shown = "±";
                        break;
                    case 'c':
                        shown = "Ø";
                        break;
                    case '%':
                        shown = "%";
                        break;
                    default:
                        Letter("%");
                        _at++;
                        return;
                }

                Symbol(shown, _text.Substring(_at, 3));
                _at += 3;
            }

            private void Letter(string letters)
            {
                if (!_lettersLook.Equals(_look))
                {
                    Flush();
                    _lettersLook = _look;
                }

                _letters.Append(letters);
            }

            private void Symbol(string shown, string written)
            {
                Flush();
                _runs.Add(new CadMTextRun(shown, _look, written));
                _lettersLook = _look;
            }

            private void Flush()
            {
                if (_letters.Length > 0)
                {
                    _runs.Add(new CadMTextRun(_letters.ToString(), _lettersLook));
                    _letters.Clear();
                }
            }

            private void EndParagraph()
            {
                Flush();
                // An alignment holds from the paragraph it is set in on, as AutoCAD keeps it.
                _paragraphs.Add(new CadMTextParagraph(_runs, _alignment, _paragraphCodes) { StartsColumn = _startsColumn });
                _startsColumn = false;
                _runs = new List<CadMTextRun>();
                _paragraphCodes = null;
            }

            private static string Stacked(string value)
            {
                int split = value.IndexOfAny(new[] { '^', '/', '#' });

                if (split < 0)
                {
                    return value;
                }

                string top = value.Substring(0, split).Trim();
                string bottom = value.Substring(split + 1).Trim();
                return value[split] == '^' ? top + " " + bottom : top + "/" + bottom;
            }

            private static bool TryNumber(string value, out double number, out bool times)
            {
                string text = value.Trim();
                times = text.EndsWith("x", StringComparison.OrdinalIgnoreCase);

                if (times)
                {
                    text = text.Substring(0, text.Length - 1);
                }

                return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out number) && !double.IsNaN(number) && !double.IsInfinity(number);
            }
        }
    }
}
