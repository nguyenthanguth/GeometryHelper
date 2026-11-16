using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// What the MTEXT editor's Import Text reads from a file: a text file (.txt) as paragraphs of plain letters, or a rich text
    /// file (.rtf) as paragraphs of runs with the font, bold, italic, underline, strikethrough, colour and size they have there.
    /// A file over 32 KB is refused, as AutoCAD 2021 refuses it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// An RTF is read for what an MTEXT can hold: <c>\b</c>, <c>\i</c>, <c>\ul</c>, <c>\strike</c>, <c>\fN</c> (a font other
    /// than the document's default), <c>\cfN</c> (a colour of the colour table, written as a true colour), <c>\fsN</c> (a
    /// size other than that of the first letters, in drawing units the MTEXT's height times the ratio), <c>\par</c> and
    /// <c>\line</c> (a new paragraph), <c>\tab</c>, the quotes, dashes and bullet, and the letters of <c>\'hh</c> and
    /// <c>\uN</c>. What is dropped: paragraph formatting (alignment, indents, tab stops, spacing, lists), tables, pictures
    /// and objects, headers and footers, footnotes, hyperlinks (their words stay), and every other character effect (small
    /// capitals, sub- and superscripts, highlight, spacing, shadow). A run that has none of the rest is <see cref="CadMTextLook.Plain"/>,
    /// and <see cref="CadMTextEditor.InsertContent"/> gives it the look of the letters typed there.
    /// </para>
    /// </remarks>
    public static class CadMTextImport
    {
        /// <summary>The largest file Import Text takes, in bytes: 32 KB.</summary>
        public const int MaxBytes = 32768;

        // What Windows-1252 puts at 0x80 to 0x9F, where Latin-1 has controls.
        private static readonly char[] Windows1252 =
        {
            '\u20AC',
            '\u0081',
            '\u201A',
            '\u0192',
            '\u201E',
            '\u2026',
            '\u2020',
            '\u2021',
            '\u02C6',
            '\u2030',
            '\u0160',
            '\u2039',
            '\u0152',
            '\u008D',
            '\u017D',
            '\u008F',
            '\u0090',
            '\u2018',
            '\u2019',
            '\u201C',
            '\u201D',
            '\u2022',
            '\u2013',
            '\u2014',
            '\u02DC',
            '\u2122',
            '\u0161',
            '\u203A',
            '\u0153',
            '\u009D',
            '\u017E',
            '\u0178',
        };

        // Groups an RTF holds that are no text of the page: skipped with everything in them.
        private static readonly HashSet<string> Skipped = new HashSet<string>(StringComparer.Ordinal)
        {
            "stylesheet",
            "info",
            "pict",
            "object",
            "objdata",
            "header",
            "headerl",
            "headerr",
            "headerf",
            "footer",
            "footerl",
            "footerr",
            "footerf",
            "footnote",
            "fldinst",
            "themedata",
            "colorschememapping",
            "latentstyles",
            "datastore",
            "listtable",
            "listoverridetable",
            "rsidtbl",
            "xmlnstbl",
            "template",
            "revtbl",
            "filetbl",
            "generator",
            "private",
            "pgdsctbl",
            "listtext",
            "pntext",
            "shpinst",
            "nonshppict",
        };

        /// <summary>Reads a file: an RTF where it begins <c>{\rtf</c>, else a text file.</summary>
        /// <param name="path">The .txt or .rtf file.</param>
        /// <param name="height">The MTEXT's height, which an RTF's sizes are taken as ratios of.</param>
        /// <returns>The paragraphs of runs; one empty paragraph for an empty file.</returns>
        /// <exception cref="ArgumentException">Thrown when the path is empty.</exception>
        /// <exception cref="IOException">Thrown when the file cannot be read, or is over <see cref="MaxBytes"/>.</exception>
        /// <exception cref="UnauthorizedAccessException">Thrown when the file may not be read.</exception>
        public static CadMTextContent FromFile(string path, double height)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentException("A file is needed.", nameof(path));
            }

            var info = new FileInfo(path);

            if (info.Length > MaxBytes)
            {
                throw new IOException("The file is over 32 KB, which Import Text does not take.");
            }

            byte[] bytes = File.ReadAllBytes(path);

            if (IsRtf(bytes))
            {
                return FromRtf(Encoding.GetEncoding("iso-8859-1").GetString(bytes), height);
            }

            return FromText(Decode(bytes));
        }

        /// <summary>Reads plain words: each line a paragraph, in no look of its own.</summary>
        /// <param name="text">The words; a carriage return, a line feed or both end a line.</param>
        /// <returns>The paragraphs of runs; one empty paragraph for no words.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the text is null.</exception>
        public static CadMTextContent FromText(string text)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            var paragraphs = new List<CadMTextParagraph>();

            foreach (string line in text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
            {
                paragraphs.Add(Paragraph(line.Length == 0 ? null : new CadMTextRun(line, CadMTextLook.Plain)));
            }

            return CadMTextContent.Of(paragraphs);
        }

        /// <summary>Reads rich text as the class says.</summary>
        /// <param name="rtf">The text of an .rtf file, one character to a byte.</param>
        /// <param name="height">The MTEXT's height, which the sizes are taken as ratios of.</param>
        /// <returns>The paragraphs of runs; one empty paragraph for no words.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the text is null.</exception>
        public static CadMTextContent FromRtf(string rtf, double height)
        {
            if (rtf == null)
            {
                throw new ArgumentNullException(nameof(rtf));
            }

            return new RtfReader(rtf, height).Read();
        }

        private static bool IsRtf(byte[] bytes)
        {
            int at = 0;

            while (at < bytes.Length && (bytes[at] == ' ' || bytes[at] == '\r' || bytes[at] == '\n'))
            {
                at++;
            }

            return at + 5 <= bytes.Length
                && bytes[at] == (byte)'{'
                && bytes[at + 1] == (byte)'\\'
                && bytes[at + 2] == (byte)'r'
                && bytes[at + 3] == (byte)'t'
                && bytes[at + 4] == (byte)'f';
        }

        /// <summary>The letters of a text file: by its byte order mark, else UTF-8 where it is valid UTF-8, else Latin-1.</summary>
        private static string Decode(byte[] bytes)
        {
            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            {
                return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
            }

            if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
            {
                return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
            }

            if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
            {
                return Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);
            }

            try
            {
                return new UTF8Encoding(false, true).GetString(bytes);
            }
            catch (ArgumentException)
            {
                var letters = new StringBuilder(bytes.Length);

                foreach (byte b in bytes)
                {
                    letters.Append(b >= 0x80 && b <= 0x9F ? Windows1252[b - 0x80] : (char)b);
                }

                return letters.ToString();
            }
        }

        private static CadMTextParagraph Paragraph(CadMTextRun run) =>
            new CadMTextParagraph(run == null ? Array.Empty<CadMTextRun>() : new[] { run });

        /// <summary>The look of the letters at a place of the RTF: a group keeps its own copy.</summary>
        private sealed class Format
        {
            public bool Bold;
            public bool Italic;
            public bool Underline;
            public bool Strike;
            public int Font = -1;
            public int Size;
            public int Color;
            public int Skip = 1;
            public bool Skipping;
            public string Destination;

            public Format Copy() => (Format)MemberwiseClone();
        }

        private sealed class RtfReader
        {
            private readonly string _rtf;
            private readonly double _height;
            private readonly Dictionary<int, string> _fonts = new Dictionary<int, string>();
            private readonly List<int> _colors = new List<int>();
            private readonly List<CadMTextParagraph> _paragraphs = new List<CadMTextParagraph>();
            private readonly List<CadMTextRun> _runs = new List<CadMTextRun>();
            private readonly StringBuilder _text = new StringBuilder();
            private readonly StringBuilder _word = new StringBuilder();
            private readonly Stack<Format> _stack = new Stack<Format>();
            private Format _format = new Format { Size = 24 };
            private int _defaultFont;
            private int _baseSize;
            private int _tableFont = -1;
            private int[] _rgb = { -1, -1, -1 };
            private int _skipLetters;

            internal RtfReader(string rtf, double height)
            {
                _rtf = rtf;
                _height = height;
            }

            internal CadMTextContent Read()
            {
                int i = 0;

                while (i < _rtf.Length)
                {
                    char c = _rtf[i];

                    if (c == '{')
                    {
                        Flush();
                        _stack.Push(_format);
                        _format = _format.Copy();
                        i++;
                    }
                    else if (c == '}')
                    {
                        Flush();

                        if (_stack.Count > 0)
                        {
                            _format = _stack.Pop();
                        }

                        i++;
                    }
                    else if (c == '\\')
                    {
                        i = Control(i + 1);
                    }
                    else if (c == '\r' || c == '\n')
                    {
                        i++;
                    }
                    else
                    {
                        Letter(c);
                        i++;
                    }
                }

                Flush();
                _paragraphs.Add(new CadMTextParagraph(_runs.ToArray()));
                return CadMTextContent.Of(_paragraphs);
            }

            /// <summary>A control word or symbol after its backslash; where the next character is.</summary>
            private int Control(int i)
            {
                if (i >= _rtf.Length)
                {
                    return i;
                }

                char c = _rtf[i];

                if (!char.IsLetter(c))
                {
                    i++;

                    switch (c)
                    {
                        case '\'':
                            if (
                                i + 1 < _rtf.Length
                                && int.TryParse(
                                    _rtf.Substring(i, 2),
                                    NumberStyles.HexNumber,
                                    CultureInfo.InvariantCulture,
                                    out int code
                                )
                            )
                            {
                                Letter(code >= 0x80 && code <= 0x9F ? Windows1252[code - 0x80] : (char)code);
                                return i + 2;
                            }

                            return i;
                        case '*':
                            _format.Skipping = true;
                            return i;
                        case '~':
                            Letter('\u00A0');
                            return i;
                        case '_':
                            Letter('-');
                            return i;
                        case '\\':
                        case '{':
                        case '}':
                            Letter(c);
                            return i;
                        default:
                            return i;
                    }
                }

                int start = i;

                while (i < _rtf.Length && char.IsLetter(_rtf[i]))
                {
                    i++;
                }

                string word = _rtf.Substring(start, i - start);
                int number = 0;
                bool hasNumber = false;
                int sign = 1;

                if (i < _rtf.Length && _rtf[i] == '-')
                {
                    sign = -1;
                    i++;
                }

                int digits = i;

                while (i < _rtf.Length && _rtf[i] >= '0' && _rtf[i] <= '9' && i - digits < 9)
                {
                    number = (number * 10) + (_rtf[i] - '0');
                    i++;
                }

                hasNumber = i > digits;
                number *= sign;

                // One space after a control word belongs to it.
                if (i < _rtf.Length && _rtf[i] == ' ')
                {
                    i++;
                }

                Word(word, number, hasNumber);
                return i;
            }

            private void Word(string word, int number, bool hasNumber)
            {
                switch (word)
                {
                    case "fonttbl":
                        _format.Destination = "fonttbl";
                        _tableFont = -1;
                        return;
                    case "colortbl":
                        _format.Destination = "colortbl";
                        _rgb = new[] { -1, -1, -1 };
                        return;
                    case "deff":
                        _defaultFont = number;
                        return;
                }

                if (Skipped.Contains(word))
                {
                    _format.Skipping = true;
                    return;
                }

                if (_format.Destination == "colortbl")
                {
                    int part =
                        word == "red" ? 0
                        : word == "green" ? 1
                        : word == "blue" ? 2
                        : -1;

                    if (part >= 0)
                    {
                        _rgb[part] = Math.Max(0, Math.Min(255, number));
                    }

                    return;
                }

                if (_format.Destination == "fonttbl")
                {
                    if (word == "f")
                    {
                        _tableFont = number;
                    }

                    return;
                }

                if (_format.Skipping)
                {
                    return;
                }

                bool on = !hasNumber || number != 0;

                switch (word)
                {
                    case "par":
                    case "line":
                    case "sect":
                    case "page":
                        Flush();
                        _paragraphs.Add(new CadMTextParagraph(_runs.ToArray()));
                        _runs.Clear();
                        break;
                    case "tab":
                        Letter('\t');
                        break;
                    case "emspace":
                    case "enspace":
                    case "qmspace":
                        Letter(' ');
                        break;
                    case "lquote":
                        Letter('\u2018');
                        break;
                    case "rquote":
                        Letter('\u2019');
                        break;
                    case "ldblquote":
                        Letter('\u201C');
                        break;
                    case "rdblquote":
                        Letter('\u201D');
                        break;
                    case "endash":
                        Letter('\u2013');
                        break;
                    case "emdash":
                        Letter('\u2014');
                        break;
                    case "bullet":
                        Letter('\u2022');
                        break;
                    case "uc":
                        _format.Skip = Math.Max(0, number);
                        break;
                    case "u":
                        Flush();
                        _text.Append((char)(number < 0 ? number + 65536 : number));
                        _skipLetters = _format.Skip;
                        break;
                    case "b":
                        Flush();
                        _format.Bold = on;
                        break;
                    case "i":
                        Flush();
                        _format.Italic = on;
                        break;
                    case "ul":
                    case "uldb":
                    case "ulw":
                        Flush();
                        _format.Underline = on;
                        break;
                    case "ulnone":
                        Flush();
                        _format.Underline = false;
                        break;
                    case "strike":
                    case "striked":
                        Flush();
                        _format.Strike = on;
                        break;
                    case "f":
                        Flush();
                        _format.Font = number;
                        break;
                    case "fs":
                        Flush();
                        _format.Size = number > 0 ? number : 24;
                        break;
                    case "cf":
                        Flush();
                        _format.Color = number;
                        break;
                    case "plain":
                        Flush();
                        _format.Bold = false;
                        _format.Italic = false;
                        _format.Underline = false;
                        _format.Strike = false;
                        _format.Font = -1;
                        _format.Size = 24;
                        _format.Color = 0;
                        break;
                }
            }

            /// <summary>A letter of the text: of a font name or a colour table where one is being read.</summary>
            private void Letter(char c)
            {
                // The one letter after \uN that older readers show in its place is not the text.
                if (_skipLetters > 0)
                {
                    _skipLetters--;
                    return;
                }

                if (_format.Destination == "fonttbl")
                {
                    if (c == ';')
                    {
                        if (_tableFont >= 0)
                        {
                            _fonts[_tableFont] = _word.ToString().Trim();
                        }

                        _word.Clear();
                    }
                    else
                    {
                        _word.Append(c);
                    }

                    return;
                }

                if (_format.Destination == "colortbl")
                {
                    if (c == ';')
                    {
                        _colors.Add(
                            _rgb[0] < 0 ? -1 : (_rgb[0] << 16) | (Math.Max(0, _rgb[1]) << 8) | Math.Max(0, _rgb[2])
                        );
                        _rgb = new[] { -1, -1, -1 };
                    }

                    return;
                }

                if (_format.Skipping || _format.Destination != null)
                {
                    return;
                }

                if (_baseSize == 0 && !char.IsWhiteSpace(c))
                {
                    _baseSize = _format.Size;
                }

                _text.Append(c);
            }

            /// <summary>Ends the run being read: its letters in the look the format gives.</summary>
            private void Flush()
            {
                if (_text.Length == 0)
                {
                    return;
                }

                _runs.Add(new CadMTextRun(_text.ToString(), LookOf(_format)));
                _text.Clear();
            }

            private CadMTextLook LookOf(Format format)
            {
                CadMTextLook look = CadMTextLook.Plain;
                string font =
                    format.Font >= 0 && format.Font != _defaultFont && _fonts.TryGetValue(format.Font, out string name)
                        ? name
                        : null;

                if (font != null || format.Bold || format.Italic)
                {
                    look = look.WithTrueType(font, format.Bold, format.Italic);
                }

                if (format.Underline)
                {
                    look = look.WithUnderline(true);
                }

                if (format.Strike)
                {
                    look = look.WithStrikethrough(true);
                }

                if (format.Color > 0 && format.Color < _colors.Count && _colors[format.Color] >= 0)
                {
                    look = look.WithTrueColor(_colors[format.Color]);
                }

                if (_baseSize > 0 && format.Size != _baseSize && _height > 0.0)
                {
                    look = look.WithHeight(Math.Round(_height * format.Size / _baseSize, 4));
                }

                return look;
            }
        }
    }
}
