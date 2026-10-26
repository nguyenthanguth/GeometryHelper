using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// The words of an MTEXT as RTF and back, for <see cref="DxfMText.ContentsRTF"/>: the codes an MTEXT and RTF both have
    /// carried over, as <see cref="DxfMText.ContentsRTF"/> lists them, every other code of either left out.
    /// </summary>
    internal static class DxfRtf
    {
        // The destinations of RTF whose words are not the document's, left out whole.
        private static readonly HashSet<string> Skipped = new HashSet<string>(StringComparer.Ordinal)
        {
            "stylesheet", "info", "pict", "header", "headerl", "headerr", "headerf", "footer", "footerl", "footerr", "footerf",
            "footnote", "object", "listtable", "listoverridetable", "rsidtbl", "generator", "themedata", "colorschememapping",
            "latentstyles", "datastore", "xmlnstbl", "filetbl", "revtbl", "mmathPr", "fldinst", "bkmkstart", "bkmkend",
        };

        // The words of RTF that stand for a letter, and the letter.
        private static readonly Dictionary<string, char> Letters = new Dictionary<string, char>(StringComparer.Ordinal)
        {
            ["emdash"] = '\u2014', ["endash"] = '\u2013', ["bullet"] = '\u2022', ["lquote"] = '\u2018', ["rquote"] = '\u2019',
            ["ldblquote"] = '\u201C', ["rdblquote"] = '\u201D', ["emspace"] = '\u2003', ["enspace"] = '\u2002', ["qmspace"] = '\u2005',
        };

        /// <summary>
        /// The RTF of the words of an MTEXT: a document of the font table, the style's font first (<c>\f0</c>) and every font
        /// the words name after it in the order they name them, a font's file marked so by the ignored group <c>{\*\ghfontfile}</c>,
        /// the colour table where the words have colours, then the words, closed by a paragraph's mark, which
        /// <see cref="FromRtf"/> leaves out of the last paragraph.
        /// </summary>
        internal static string ToRtf(string contents, string styleFont, bool styleIsFile)
        {
            var fonts = new List<string> { styleFont };
            var files = new List<bool> { styleIsFile };

            // The next semicolon from where a code is, found again only once it is passed: a code that ends in one reads to it.
            int semicolon = -2;
            var colors = new List<int>();
            var body = new StringBuilder(contents.Length + 16);
            int i = 0;

            while (i < contents.Length)
            {
                char c = contents[i];

                if (c == '{' || c == '}')
                {
                    body.Append(c);
                    i++;
                    continue;
                }

                if (c == '^' && i + 1 < contents.Length && (contents[i + 1] == ' ' || (contents[i + 1] >= '@' && contents[i + 1] <= '_')))
                {
                    char caret = contents[i + 1];
                    body.Append(caret == ' ' ? "^" : caret == 'I' ? @"\tab " : caret == 'J' ? @"\par " : string.Empty);
                    i += 2;
                    continue;
                }

                if (c == '%' && i + 2 < contents.Length && contents[i + 1] == '%' && TrySign(contents, i, out char sign, out int length))
                {
                    Letter(body, sign);
                    i += length;
                    continue;
                }

                if (c != '\\' || i + 1 >= contents.Length)
                {
                    Letter(body, c);
                    i++;
                    continue;
                }

                char code = contents[i + 1];

                switch (code)
                {
                    case 'P':
                        body.Append(@"\par ");
                        break;
                    case 'X':
                        body.Append(@"\line ");
                        break;
                    case 'N':
                        body.Append(@"\column ");
                        break;
                    case '~':
                        body.Append(@"\~");
                        break;
                    case '\\':
                    case '{':
                    case '}':
                        body.Append('\\').Append(code);
                        break;
                    case 'L':
                        body.Append(@"\ul ");
                        break;
                    case 'l':
                        body.Append(@"\ulnone ");
                        break;
                    case 'K':
                        body.Append(@"\strike ");
                        break;
                    case 'k':
                        body.Append(@"\strike0 ");
                        break;
                    case 'O':
                    case 'o':
                        break;
                    case 'U':
                        if (i + 6 < contents.Length && contents[i + 2] == '+' && int.TryParse(contents.Substring(i + 3, 4), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out int letter))
                        {
                            Letter(body, (char)letter);
                            i += 7;
                            continue;
                        }

                        Letter(body, '\\');
                        Letter(body, 'U');
                        break;
                    default:
                        if (!char.IsLetter(code))
                        {
                            Letter(body, code);
                            break;
                        }

                        if (semicolon != -1 && semicolon < i + 1)
                        {
                            semicolon = contents.IndexOf(';', i + 1);
                        }

                        int end = semicolon;

                        if (end < 0)
                        {
                            // A code with no semicolon after it is kept as written, as the words read without their codes keep it.
                            Letter(body, '\\');
                            Letter(body, code);
                            break;
                        }

                        Argument(body, code, contents.Substring(i + 2, end - i - 2), fonts, files, colors);
                        i = end + 1;
                        continue;
                }

                i += 2;
            }

            var rtf = new StringBuilder(body.Length + 64);
            rtf.Append(@"{\rtf1\ansi\deff0{\fonttbl");

            for (int f = 0; f < fonts.Count; f++)
            {
                rtf.Append(@"{\f").Append(f.ToString(CultureInfo.InvariantCulture)).Append(files[f] ? @"{\*\ghfontfile}" : " ");

                foreach (char c in fonts[f])
                {
                    Letter(rtf, c);
                }

                rtf.Append(";}");
            }

            rtf.Append('}');

            if (colors.Count > 0)
            {
                rtf.Append(@"{\colortbl ;");

                foreach (int rgb in colors)
                {
                    rtf.Append(@"\red").Append(((rgb >> 16) & 0xFF).ToString(CultureInfo.InvariantCulture))
                        .Append(@"\green").Append(((rgb >> 8) & 0xFF).ToString(CultureInfo.InvariantCulture))
                        .Append(@"\blue").Append((rgb & 0xFF).ToString(CultureInfo.InvariantCulture)).Append(';');
                }

                rtf.Append('}');
            }

            return rtf.Append(body).Append(@"\par}").ToString();
        }

        /// <summary>
        /// A code of an MTEXT that ends in a semicolon, as RTF: a stack its two parts with a slash between, as the words read
        /// without their codes give it; a font its entry of the font table, bold and italic with a TrueType font's; a colour of
        /// AutoCAD's index its entry of the colour table, BYLAYER and BYBLOCK the automatic colour; any other left out.
        /// </summary>
        private static void Argument(StringBuilder body, char code, string argument, List<string> fonts, List<bool> files, List<int> colors)
        {
            switch (code)
            {
                case 'S':
                    foreach (char c in argument.Replace('^', '/').Replace('#', '/'))
                    {
                        Letter(body, c);
                    }

                    return;
                case 'F':
                case 'f':
                    string[] parts = argument.Split('|');
                    int font = fonts.FindIndex(f => string.Equals(f, parts[0], StringComparison.OrdinalIgnoreCase));

                    if (font < 0)
                    {
                        font = fonts.Count;
                        fonts.Add(parts[0]);
                        files.Add(code == 'F');
                    }

                    body.Append(@"\f").Append(font.ToString(CultureInfo.InvariantCulture));

                    if (code == 'f')
                    {
                        body.Append(Array.IndexOf(parts, "b1") > 0 ? @"\b" : @"\b0").Append(Array.IndexOf(parts, "i1") > 0 ? @"\i" : @"\i0");
                    }

                    body.Append(' ');
                    return;
                case 'C':
                    if (int.TryParse(argument.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int index) && index >= 1 && index <= 255)
                    {
                        int rgb = DxfColor.OfIndex(index).ColorValue;
                        int entry = colors.IndexOf(rgb);

                        if (entry < 0)
                        {
                            entry = colors.Count;
                            colors.Add(rgb);
                        }

                        body.Append(@"\cf").Append((entry + 1).ToString(CultureInfo.InvariantCulture)).Append(' ');
                    }
                    else
                    {
                        body.Append(@"\cf0 ");
                    }

                    return;
            }
        }

        /// <summary>
        /// A special character of AutoCAD at an index, as the words read without their codes read it: %%c, %%d and %%p, of either
        /// case, the diameter, degree and plus-minus signs, %%% a per cent sign, %%nnn the character of that code.
        /// </summary>
        private static bool TrySign(string text, int at, out char sign, out int length)
        {
            char code = char.ToLowerInvariant(text[at + 2]);
            length = 3;

            switch (code)
            {
                case 'c': sign = '\u2300'; return true;
                case 'd': sign = '\u00B0'; return true;
                case 'p': sign = '\u00B1'; return true;
                case '%': sign = '%'; return true;
            }

            if (at + 4 < text.Length && IsDigit(text[at + 2]) && IsDigit(text[at + 3]) && IsDigit(text[at + 4]))
            {
                sign = (char)(((text[at + 2] - '0') * 100) + ((text[at + 3] - '0') * 10) + (text[at + 4] - '0'));
                length = 5;
                return true;
            }

            sign = '\0';
            return false;
        }

        private static bool IsDigit(char c) => c >= '0' && c <= '9';

        /// <summary>A letter as RTF writes it: a backslash and a brace after a backslash, a tab as <c>\tab</c>, a letter beyond ASCII as <c>\u</c> of its code and a question mark, a control character left out.</summary>
        private static void Letter(StringBuilder rtf, char c)
        {
            if (c == '\\' || c == '{' || c == '}')
            {
                rtf.Append('\\').Append(c);
            }
            else if (c == '\t')
            {
                rtf.Append(@"\tab ");
            }
            else if (c == '\u00A0')
            {
                rtf.Append(@"\~");
            }
            else if (c > 0x7F)
            {
                rtf.Append(@"\u").Append(((short)c).ToString(CultureInfo.InvariantCulture)).Append('?');
            }
            else if (c >= ' ')
            {
                rtf.Append(c);
            }
        }

        /// <summary>
        /// The words of an MTEXT of an RTF document: its words with the codes of what it sets that an MTEXT has, each written
        /// where the words take it on or off; the font of the style written by no code; a last paragraph's mark left out.
        /// </summary>
        internal static string FromRtf(string rtf, string styleFont, bool styleIsFile)
        {
            var reading = new Reading(styleFont, styleIsFile);
            reading.Run(rtf);
            return reading.Words();
        }

        /// <summary>Where a group of RTF puts its words: the document, the font table, the colour table, or nowhere.</summary>
        private enum Destination
        {
            Body,
            FontTable,
            ColorTable,
            Skip,
        }

        /// <summary>What a group of RTF sets, kept for each group and given back when it closes.</summary>
        private struct State
        {
            internal Destination Destination;
            internal int Font;
            internal bool Bold;
            internal bool Italic;
            internal bool Underline;
            internal bool Strike;
            internal int Color;
            internal int Skip;
        }

        /// <summary>An RTF document read into the words of an MTEXT.</summary>
        private sealed class Reading
        {
            private readonly string _styleFont;
            private readonly bool _styleIsFile;
            private readonly StringBuilder _words = new StringBuilder();
            private readonly Dictionary<int, string> _fonts = new Dictionary<int, string>();
            private readonly List<int?> _colors = new List<int?>();
            private readonly Stack<State> _groups = new Stack<State>();
            private readonly StringBuilder _fontName = new StringBuilder();
            private State _state = new State { Font = -1, Skip = 1 };
            private int _defaultFont;
            private readonly Dictionary<int, bool> _fontFiles = new Dictionary<int, bool>();
            private int _entryFont;
            private bool _entryFile;
            private int _settledIndex;
            private string _settledFont;
            private bool _settledFile;
            private int _red = -1;
            private int _green = -1;
            private int _blue = -1;
            private int _fallback;
            private bool _broken;

            // What the words written so far have set.
            private string _font;
            private bool _bold;
            private bool _italic;
            private bool _underline;
            private bool _strike;
            private int? _color;

            internal Reading(string styleFont, bool styleIsFile)
            {
                _styleFont = styleFont;
                _styleIsFile = styleIsFile;
                _font = styleFont;
            }

            internal string Words()
            {
                if (_broken)
                {
                    _words.Length -= 2;
                }

                return _words.ToString();
            }

            internal void Run(string rtf)
            {
                int i = 0;

                while (i < rtf.Length)
                {
                    char c = rtf[i];

                    if (c == '{')
                    {
                        _groups.Push(_state);
                        _fallback = 0;
                        i++;

                        if (i + 1 < rtf.Length && rtf[i] == '\\' && rtf[i + 1] == '*')
                        {
                            _state.Destination = Destination.Skip;
                            i += 2;
                        }

                        continue;
                    }

                    if (c == '}')
                    {
                        _fallback = 0;
                        i++;

                        if (_groups.Count == 0)
                        {
                            break;
                        }

                        _state = _groups.Pop();
                        continue;
                    }

                    if (c == '\r' || c == '\n')
                    {
                        i++;
                        continue;
                    }

                    if (c != '\\')
                    {
                        Letter(c);
                        i++;
                        continue;
                    }

                    if (i + 1 >= rtf.Length)
                    {
                        break;
                    }

                    char next = rtf[i + 1];

                    if (IsAsciiLetter(next))
                    {
                        int start = i + 1;
                        int end = start;

                        while (end < rtf.Length && IsAsciiLetter(rtf[end]))
                        {
                            end++;
                        }

                        string word = rtf.Substring(start, end - start);
                        int numberStart = end;

                        if (end < rtf.Length && rtf[end] == '-')
                        {
                            end++;
                        }

                        while (end < rtf.Length && rtf[end] >= '0' && rtf[end] <= '9')
                        {
                            end++;
                        }

                        bool has = end > numberStart && !(end == numberStart + 1 && rtf[numberStart] == '-');
                        int parameter = 0;

                        if (has && !int.TryParse(rtf.Substring(numberStart, end - numberStart), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out parameter))
                        {
                            parameter = rtf[numberStart] == '-' ? int.MinValue : int.MaxValue;
                        }

                        if (end < rtf.Length && rtf[end] == ' ')
                        {
                            end++;
                        }

                        Word(word, has, parameter);
                        i = end;
                        continue;
                    }

                    switch (next)
                    {
                        case '\'':
                            if (i + 3 < rtf.Length && int.TryParse(rtf.Substring(i + 2, 2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out int code))
                            {
                                Letter(DxfReader.Windows1252(((char)code).ToString())[0]);
                                i += 4;
                                continue;
                            }

                            break;
                        case '\\':
                        case '{':
                        case '}':
                            Letter(next);
                            break;
                        case '~':
                            Letter('\u00A0');
                            break;
                        case '_':
                            Letter('-');
                            break;
                        case '\r':
                        case '\n':
                            Word("par", false, 0);
                            break;
                    }

                    i += 2;
                }
            }

            /// <summary>A control word of RTF with its number, where it has one.</summary>
            private void Word(string word, bool has, int parameter)
            {
                bool on = !has || parameter != 0;

                switch (word)
                {
                    case "ghfontfile":
                        // The mark the library gives a font's file in the font table, inside an ignored group of the entry.
                        _entryFile |= _groups.Count > 0 && _groups.Peek().Destination == Destination.FontTable;
                        return;
                    case "fonttbl":
                        _state.Destination = Destination.FontTable;
                        return;
                    case "colortbl":
                        _state.Destination = Destination.ColorTable;
                        return;
                    case "uc":
                        _state.Skip = has && parameter >= 0 ? parameter : 1;
                        return;
                    case "u":
                        if (has)
                        {
                            Letter((char)(parameter < 0 ? parameter + 65536 : parameter));
                            _fallback = _state.Skip;
                        }

                        return;
                    case "deff":
                        _defaultFont = parameter;
                        return;
                }

                if (Skipped.Contains(word))
                {
                    _state.Destination = Destination.Skip;
                    return;
                }

                switch (_state.Destination)
                {
                    case Destination.FontTable:
                        if (word == "f")
                        {
                            _entryFont = parameter;
                            _entryFile = false;
                            _fontName.Clear();
                        }

                        return;
                    case Destination.ColorTable:
                        if (word == "red")
                        {
                            _red = parameter;
                        }
                        else if (word == "green")
                        {
                            _green = parameter;
                        }
                        else if (word == "blue")
                        {
                            _blue = parameter;
                        }

                        return;
                    case Destination.Skip:
                        return;
                }

                if (Letters.TryGetValue(word, out char letter))
                {
                    Letter(letter);
                    return;
                }

                switch (word)
                {
                    case "par":
                    case "line":
                    case "sect":
                    case "page":
                        Break(@"\P");
                        return;
                    case "column":
                        Break(@"\N");
                        return;
                    case "tab":
                        Letter('\t');
                        return;
                    case "ulnone":
                        _state.Underline = false;
                        return;
                    case "strike":
                    case "striked":
                        _state.Strike = on;
                        return;
                    case "b":
                        _state.Bold = on;
                        return;
                    case "i":
                        _state.Italic = on;
                        return;
                    case "f":
                        _state.Font = parameter;
                        return;
                    case "cf":
                        _state.Color = parameter;
                        return;
                    case "plain":
                        _state.Bold = false;
                        _state.Italic = false;
                        _state.Underline = false;
                        _state.Strike = false;
                        _state.Font = -1;
                        _state.Color = 0;
                        return;
                }

                // \ul and its kinds, \uld, \uldb, \ulw and the rest, underline; \ulc is the colour of the line.
                if (word.StartsWith("ul", StringComparison.Ordinal) && word != "ulc")
                {
                    _state.Underline = on;
                }
            }

            /// <summary>A letter of the RTF, into the destination of its group: one a <c>\u</c> was given for in place of the letter it stands for left out.</summary>
            private void Letter(char c)
            {
                if (_fallback > 0)
                {
                    _fallback--;
                    return;
                }

                switch (_state.Destination)
                {
                    case Destination.FontTable:
                        if (c == ';')
                        {
                            _fonts[_entryFont] = _fontName.ToString().Trim();
                            _fontFiles[_entryFont] = _entryFile;
                            _entryFile = false;
                            _settledFont = null;
                            _fontName.Clear();
                        }
                        else
                        {
                            _fontName.Append(c);
                        }

                        return;
                    case Destination.ColorTable:
                        if (c == ';')
                        {
                            _colors.Add(_red < 0 && _green < 0 && _blue < 0 ? (int?)null : (Byte(_red) << 16) | (Byte(_green) << 8) | Byte(_blue));
                            _red = _green = _blue = -1;
                        }

                        return;
                    case Destination.Skip:
                        return;
                }

                Settle();
                _broken = false;

                switch (c)
                {
                    case '\\':
                        _words.Append(@"\\");
                        return;
                    case '{':
                        _words.Append(@"\{");
                        return;
                    case '}':
                        _words.Append(@"\}");
                        return;
                    case '\u00A0':
                        _words.Append(@"\~");
                        return;
                    case '^':
                        _words.Append("^ ");
                        return;
                    case '\t':
                        _words.Append("^I");
                        return;
                }

                if (c >= ' ')
                {
                    _words.Append(c);
                }
            }

            /// <summary>A break of a line or a column; one that ends the words is left out when they are read whole.</summary>
            private void Break(string code)
            {
                if (_fallback > 0)
                {
                    _fallback = 0;
                }

                _words.Append(code);
                _broken = code == @"\P";
            }

            /// <summary>Writes the codes of what the group sets that the words written so far do not: the font, bold and italic, the colour, the lines under and through.</summary>
            private void Settle()
            {
                int index = _state.Font < 0 ? _defaultFont : _state.Font;

                // The font of an entry and whether it is a file, worked out again only when the entry changes.
                if (index != _settledIndex || _settledFont == null)
                {
                    _settledIndex = index;
                    _settledFont = _fonts.TryGetValue(index, out string named) && named.Length > 0 ? named : _styleFont;
                    _settledFile = string.Equals(_settledFont, _styleFont, StringComparison.OrdinalIgnoreCase)
                        ? _styleIsFile
                        : (_fontFiles.TryGetValue(index, out bool marked) && marked) || _settledFont.EndsWith(".shx", StringComparison.OrdinalIgnoreCase);
                }

                string font = _settledFont;

                // A font's file has no bold and no italic: they are left out with it.
                bool bold = _state.Bold && !_settledFile;
                bool italic = _state.Italic && !_settledFile;
                bool file = _settledFile;

                if (!string.Equals(font, _font, StringComparison.OrdinalIgnoreCase) || bold != _bold || italic != _italic)
                {
                    if (file)
                    {
                        _words.Append(@"\F").Append(font).Append(';');
                    }
                    else
                    {
                        _words.Append(@"\f").Append(font).Append("|b").Append(bold ? '1' : '0').Append("|i").Append(italic ? '1' : '0').Append(';');
                    }

                    _font = font;
                    _bold = bold;
                    _italic = italic;
                }

                int? color = _state.Color > 0 && _state.Color < _colors.Count ? _colors[_state.Color] : null;

                if (color != _color)
                {
                    _words.Append(@"\C").Append(color.HasValue ? DxfColor.FromRgb((byte)(color.Value >> 16), (byte)(color.Value >> 8), (byte)color.Value).ColorIndex.ToString(CultureInfo.InvariantCulture) : "256").Append(';');
                    _color = color;
                }

                if (_state.Underline != _underline)
                {
                    _words.Append(_state.Underline ? @"\L" : @"\l");
                    _underline = _state.Underline;
                }

                if (_state.Strike != _strike)
                {
                    _words.Append(_state.Strike ? @"\K" : @"\k");
                    _strike = _state.Strike;
                }
            }

            private static int Byte(int value) => Math.Max(0, Math.Min(255, value));

            private static bool IsAsciiLetter(char c) => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');
        }
    }
}
