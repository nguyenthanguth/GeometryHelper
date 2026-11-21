using System;
using System.Globalization;
using System.Text;

namespace GeometryHelper.Cad.Dcl
{
    /// <summary>What a token of a DCL text is.</summary>
    internal enum DclTokenKind
    {
        /// <summary>A name: letters, digits and <c>_</c>, not starting with a digit.</summary>
        Name,

        /// <summary>A string in double quotes.</summary>
        String,

        /// <summary>An integer such as <c>60</c> or <c>-1</c>.</summary>
        Integer,

        /// <summary>A real such as <c>1.5</c> or <c>.5</c>.</summary>
        Real,

        /// <summary><c>:</c>.</summary>
        Colon,

        /// <summary><c>{</c>.</summary>
        OpenBrace,

        /// <summary><c>}</c>.</summary>
        CloseBrace,

        /// <summary><c>=</c>.</summary>
        Equals,

        /// <summary><c>;</c>.</summary>
        Semicolon,

        /// <summary><c>@</c>, which starts <c>@include</c>.</summary>
        At,

        /// <summary>The end of the text.</summary>
        End,
    }

    /// <summary>One token: what it is, its value, how it was written and the line it starts on.</summary>
    internal readonly struct DclToken
    {
        internal DclToken(DclTokenKind kind, string text, string raw, int line)
        {
            Kind = kind;
            Text = text;
            Raw = raw;
            Line = line;
        }

        /// <summary>Gets what the token is.</summary>
        internal DclTokenKind Kind { get; }

        /// <summary>Gets the value: a string without its quotes and with its escapes resolved, a name or number as written.</summary>
        internal string Text { get; }

        /// <summary>Gets the token as it stands in the text, for the messages; <c>end of file</c> for the end.</summary>
        internal string Raw { get; }

        /// <summary>Gets the line the token starts on, from 1.</summary>
        internal int Line { get; }
    }

    /// <summary>
    /// Cuts the text of a DCL file into tokens (D301, D302): <c>//</c> and <c>/* */</c> comments and white space are skipped, a
    /// string reads the escapes <c>\"</c>, <c>\\</c>, <c>\n</c> and <c>\t</c> (any other <c>\x</c> keeps the x), and a byte
    /// order mark at the start is white space. A character that starts no token, a string left open at the end of its line and a
    /// comment left open at the end of the text are syntax errors.
    /// </summary>
    internal sealed class DclLexer
    {
        private readonly string _text;
        private readonly string _path;
        private int _position;
        private int _line = 1;

        internal DclLexer(string text, string path)
        {
            _text = text;
            _path = path;
        }

        /// <summary>Reads the next token; the end of the text gives <see cref="DclTokenKind.End"/> again and again.</summary>
        /// <exception cref="DclException">Thrown at a character or a string or comment that is not valid.</exception>
        internal DclToken Next()
        {
            SkipBlanks();

            if (_position >= _text.Length)
            {
                // A final line break does not start a line: the end of "a : b {\n" is on line 1.
                int last = _text.Length > 0 && _text[_text.Length - 1] == '\n' && _line > 1 ? _line - 1 : _line;
                return new DclToken(DclTokenKind.End, string.Empty, "end of file", last);
            }

            char c = _text[_position];
            int line = _line;

            switch (c)
            {
                case ':':
                    return Single(DclTokenKind.Colon, line);
                case '{':
                    return Single(DclTokenKind.OpenBrace, line);
                case '}':
                    return Single(DclTokenKind.CloseBrace, line);
                case '=':
                    return Single(DclTokenKind.Equals, line);
                case ';':
                    return Single(DclTokenKind.Semicolon, line);
                case '@':
                    return Single(DclTokenKind.At, line);
                case '"':
                    return ReadString(line);
            }

            if (IsNameStart(c))
            {
                int start = _position;

                while (_position < _text.Length && IsNameChar(_text[_position]))
                {
                    _position++;
                }

                string name = _text.Substring(start, _position - start);
                return new DclToken(DclTokenKind.Name, name, name, line);
            }

            if (char.IsDigit(c) || c == '-' || c == '.')
            {
                return ReadNumber(line);
            }

            throw Error(c.ToString(), line);
        }

        private DclToken Single(DclTokenKind kind, int line)
        {
            string text = _text[_position].ToString();
            _position++;
            return new DclToken(kind, text, text, line);
        }

        private DclToken ReadNumber(int line)
        {
            int start = _position;

            // A run of letters, digits, '_', '.' and '-' that is no number ("10px", "1.2.3") is one bad symbol.
            _position++;

            while (_position < _text.Length && (IsNameChar(_text[_position]) || _text[_position] == '.'))
            {
                _position++;
            }

            string raw = _text.Substring(start, _position - start);
            int digits = raw[0] == '-' ? 1 : 0;
            int dots = 0;
            bool anyDigit = false;

            for (int i = digits; i < raw.Length; i++)
            {
                if (raw[i] == '.')
                {
                    dots++;
                }
                else if (raw[i] >= '0' && raw[i] <= '9')
                {
                    anyDigit = true;
                }
                else
                {
                    throw Error(raw, line);
                }
            }

            if (!anyDigit || dots > 1)
            {
                throw Error(raw, line);
            }

            return new DclToken(dots == 0 ? DclTokenKind.Integer : DclTokenKind.Real, raw, raw, line);
        }

        private DclToken ReadString(int line)
        {
            int start = _position;
            var value = new StringBuilder();
            _position++;

            while (true)
            {
                if (_position >= _text.Length || _text[_position] == '\n' || _text[_position] == '\r')
                {
                    throw Error(_text.Substring(start, _position - start), line);
                }

                char c = _text[_position];

                if (c == '"')
                {
                    _position++;
                    return new DclToken(
                        DclTokenKind.String,
                        value.ToString(),
                        _text.Substring(start, _position - start),
                        line
                    );
                }

                if (
                    c == '\\'
                    && _position + 1 < _text.Length
                    && _text[_position + 1] != '\n'
                    && _text[_position + 1] != '\r'
                )
                {
                    char escaped = _text[_position + 1];
                    value.Append(
                        escaped == 'n' ? '\n'
                        : escaped == 't' ? '\t'
                        : escaped
                    );
                    _position += 2;
                    continue;
                }

                value.Append(c);
                _position++;
            }
        }

        private void SkipBlanks()
        {
            while (_position < _text.Length)
            {
                char c = _text[_position];

                if (c == '\n')
                {
                    _line++;
                    _position++;
                }
                else if (c == ' ' || c == '\t' || c == '\r' || c == '﻿' || c == '\f' || c == '\v')
                {
                    _position++;
                }
                else if (c == '/' && _position + 1 < _text.Length && _text[_position + 1] == '/')
                {
                    while (_position < _text.Length && _text[_position] != '\n')
                    {
                        _position++;
                    }
                }
                else if (c == '/' && _position + 1 < _text.Length && _text[_position + 1] == '*')
                {
                    SkipBlockComment();
                }
                else
                {
                    return;
                }
            }
        }

        private void SkipBlockComment()
        {
            int line = _line;
            _position += 2;

            while (_position < _text.Length)
            {
                if (_text[_position] == '*' && _position + 1 < _text.Length && _text[_position + 1] == '/')
                {
                    _position += 2;
                    return;
                }

                if (_text[_position] == '\n')
                {
                    _line++;
                }

                _position++;
            }

            // Left open: the end of the text is the bad symbol, on the line the text ends on.
            int last = _text.Length > 0 && _text[_text.Length - 1] == '\n' && _line > 1 ? _line - 1 : _line;
            throw new DclException(
                _path,
                Math.Max(last, line),
                "syntax error. Symbol: \"end of file\".",
                "end of file"
            );
        }

        private DclException Error(string symbol, int line) =>
            new DclException(
                _path,
                line,
                string.Format(CultureInfo.InvariantCulture, "syntax error. Symbol: \"{0}\".", symbol),
                symbol
            );

        private static bool IsNameStart(char c) => c == '_' || (c < 128 && char.IsLetter(c));

        private static bool IsNameChar(char c) => c == '_' || (c < 128 && char.IsLetterOrDigit(c));
    }
}
