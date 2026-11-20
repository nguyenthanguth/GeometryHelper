using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;

namespace GeometryHelper.Cad.Lisp
{
    /// <summary>
    /// Reads AutoLISP text one form at a time, the way AutoCAD does, corners included: a symbol ends at a dot and the rest of it is dropped (<c>a.b</c> reads
    /// as <c>A</c>), <c>.5</c> is an error and <c>1.</c> is the real 1.0, an integer past 32 bits is a real, <c>1e400</c> is
    /// 0.0, and <c>#|c|#</c> is a symbol, not a comment. Reading one form at a time is what lets <c>load</c> keep what ran
    /// before a bad form.
    /// </summary>
    internal sealed class LispReader
    {
        private const string Delimiters = "()\"';";

        private readonly string _text;
        private readonly Func<string, LispSymbol> _intern;
        private readonly LispSymbol _quote;
        private int _position;

        /// <summary>Starts reading text.</summary>
        /// <param name="text">The text.</param>
        /// <param name="intern">Gives the symbol of an upper-case name.</param>
        /// <param name="quote">The symbol <c>'x</c> is read as the list <c>(QUOTE x)</c> of.</param>
        internal LispReader(string text, Func<string, LispSymbol> intern, LispSymbol quote)
        {
            _text = text;
            _intern = intern;
            _quote = quote;
        }

        /// <summary>Reads the next form.</summary>
        /// <param name="form">The form: nil is null.</param>
        /// <returns>False when only spaces and comments are left.</returns>
        /// <exception cref="LispException">Thrown for the reader errors of AutoCAD: a list or string left open, a <c>)</c> with no <c>(</c>, a misplaced dot.</exception>
        internal bool TryRead(out object form)
        {
            SkipSpace();

            if (_position >= _text.Length)
            {
                form = null;
                return false;
            }

            form = ReadForm();
            return true;
        }

        private static bool IsSpace(char c) => c <= ' ' || char.IsWhiteSpace(c);

        private static bool IsDelimiter(char c) => IsSpace(c) || Delimiters.IndexOf(c) >= 0;

        private void SkipSpace()
        {
            while (_position < _text.Length)
            {
                char c = _text[_position];

                if (IsSpace(c))
                {
                    _position++;
                }
                else if (c == ';')
                {
                    if (_position + 1 < _text.Length && _text[_position + 1] == '|')
                    {
                        int end = _text.IndexOf("|;", _position + 2, StringComparison.Ordinal);
                        _position = end < 0 ? _text.Length : end + 2;
                    }
                    else
                    {
                        int end = _text.IndexOf('\n', _position);
                        _position = end < 0 ? _text.Length : end + 1;
                    }
                }
                else
                {
                    return;
                }
            }
        }

        private static void CheckStack()
        {
            try
            {
                RuntimeHelpers.EnsureSufficientExecutionStack();
            }
            catch (InsufficientExecutionStackException)
            {
                throw new LispException("internal stack limit reached (simulated)", isHard: true);
            }
        }

        private object ReadForm()
        {
            CheckStack();
            char c = _text[_position];

            switch (c)
            {
                case '(':
                    _position++;
                    return ReadList();
                case ')':
                    throw new LispException("extra right paren on input");
                case '"':
                    _position++;
                    return ReadString();
                case '\'':
                    _position++;
                    SkipSpace();

                    if (_position >= _text.Length || _text[_position] == ')')
                    {
                        throw new LispException("malformed list on input");
                    }

                    return new LispCons(_quote, new LispCons(ReadForm(), null));
                case '.':
                    throw new LispException("misplaced dot on input");
                default:
                    return ReadAtom();
            }
        }

        private object ReadList()
        {
            var items = new List<object>();
            object tail = null;

            while (true)
            {
                SkipSpace();

                if (_position >= _text.Length)
                {
                    throw new LispException("malformed list on input");
                }

                char c = _text[_position];

                if (c == ')')
                {
                    _position++;
                    break;
                }

                if (c == '.')
                {
                    int next = _position + 1;

                    if (items.Count == 0 || (next < _text.Length && !IsDelimiter(_text[next])))
                    {
                        throw new LispException("misplaced dot on input");
                    }

                    _position++;
                    SkipSpace();

                    if (_position >= _text.Length)
                    {
                        throw new LispException("malformed list on input");
                    }

                    if (_text[_position] == ')')
                    {
                        throw new LispException("misplaced dot on input");
                    }

                    tail = ReadForm();
                    SkipSpace();

                    if (_position >= _text.Length)
                    {
                        throw new LispException("malformed list on input");
                    }

                    if (_text[_position] != ')')
                    {
                        throw new LispException("extra cdrs in dotted pair on input");
                    }

                    _position++;
                    break;
                }

                items.Add(ReadForm());
            }

            object list = tail;

            for (int i = items.Count - 1; i >= 0; i--)
            {
                list = new LispCons(items[i], list);
            }

            return list;
        }

        /// <summary>
        /// Reads a string as AutoCAD does when it loads one: <c>\n \r \t \e</c> in lower case are the escapes and any other
        /// letter after a backslash is itself (<c>\E</c> is E, <c>\w</c> is w, <c>\x41</c> is x41); up to three octal digits
        /// are one character, taken modulo 256, and a NUL ends the string (<c>"\0"</c> and <c>"\400"</c> are empty);
        /// <c>\U+</c> and four hex digits stay in the string as the seven characters they are, which the command line shows as
        /// the character; and a <c>\U</c> with no such digits after it is UU.
        /// </summary>
        private string ReadString()
        {
            var builder = new StringBuilder();
            bool ended = false;

            while (_position < _text.Length)
            {
                char c = _text[_position++];

                if (c == '"')
                {
                    return builder.ToString();
                }

                if (c != '\\')
                {
                    if (!ended)
                    {
                        builder.Append(c);
                    }

                    continue;
                }

                if (_position >= _text.Length)
                {
                    break;
                }

                char e = _text[_position++];
                string made;

                switch (e)
                {
                    case 'n':
                        made = "\n";
                        break;
                    case 'r':
                        made = "\r";
                        break;
                    case 't':
                        made = "\t";
                        break;
                    case 'e':
                        made = "\u001b";
                        break;
                    case 'U':
                    case 'u':
                        if (IsUnicodeEscape(_position))
                        {
                            made = "\\" + e + _text.Substring(_position, 5);
                            _position += 5;
                        }
                        else
                        {
                            made = e == 'U' ? "UU" : "u";
                        }

                        break;
                    default:
                        if (e >= '0' && e <= '7')
                        {
                            int code = e - '0';
                            int digits = 1;

                            while (
                                digits < 3
                                && _position < _text.Length
                                && _text[_position] >= '0'
                                && _text[_position] <= '7'
                            )
                            {
                                code = code * 8 + (_text[_position++] - '0');
                                digits++;
                            }

                            code &= 255;

                            if (code == 0)
                            {
                                ended = true;
                            }

                            made = ((char)code).ToString();
                        }
                        else
                        {
                            made = e.ToString();
                        }

                        break;
                }

                if (!ended)
                {
                    builder.Append(made);
                }
            }

            throw new LispException("malformed string on input");
        }

        /// <summary>Tells whether a plus sign and four hex digits are at a position: what follows <c>\U</c> in <c>\U+0041</c>.</summary>
        private bool IsUnicodeEscape(int at)
        {
            if (at + 5 > _text.Length || _text[at] != '+')
            {
                return false;
            }

            for (int i = at + 1; i < at + 5; i++)
            {
                char h = _text[i];

                if (!(h >= '0' && h <= '9') && !(h >= 'a' && h <= 'f') && !(h >= 'A' && h <= 'F'))
                {
                    return false;
                }
            }

            return true;
        }

        private object ReadAtom()
        {
            int start = _position;
            int end = start;

            while (end < _text.Length && !IsDelimiter(_text[end]))
            {
                end++;
            }

            string run = _text.Substring(start, end - start);

            if (TryNumber(run, out object number))
            {
                _position = end;
                return number;
            }

            // A symbol ends at a dot: AutoCAD reads a.b as A.
            int dot = run.IndexOf('.');

            if (dot > 0)
            {
                run = run.Substring(0, dot);

                if (TryNumber(run, out number))
                {
                    _position = start + dot;
                    return number;
                }
            }

            // What follows the dot, up to the next delimiter, is dropped: (setq e.d 5) sets E, as AutoCAD 2021 does.
            _position = end;
            string name = run.ToUpperInvariant();

            return name == "NIL" ? null : _intern(name);
        }

        /// <summary>
        /// Reads an integer or a real as AutoCAD does: an optional sign, digits, and for a real a point and digits, an exponent
        /// or both; an integer past 32 bits is a real, and a real too large for a double is 0.0.
        /// </summary>
        /// <param name="token">The text of the atom.</param>
        /// <param name="number">The int or the double.</param>
        /// <returns>False when the text is not a number.</returns>
        internal static bool TryNumber(string token, out object number)
        {
            number = null;
            int i = 0;
            int n = token.Length;

            if (i < n && (token[i] == '+' || token[i] == '-'))
            {
                i++;
            }

            int digitsStart = i;

            while (i < n && token[i] >= '0' && token[i] <= '9')
            {
                i++;
            }

            if (i == digitsStart)
            {
                return false;
            }

            bool real = false;

            if (i < n && token[i] == '.')
            {
                real = true;
                i++;

                while (i < n && token[i] >= '0' && token[i] <= '9')
                {
                    i++;
                }
            }

            if (i < n && (token[i] == 'e' || token[i] == 'E'))
            {
                int j = i + 1;

                if (j < n && (token[j] == '+' || token[j] == '-'))
                {
                    j++;
                }

                int exponentStart = j;

                while (j < n && token[j] >= '0' && token[j] <= '9')
                {
                    j++;
                }

                if (j == exponentStart)
                {
                    return false;
                }

                real = true;
                i = j;
            }

            if (i != n)
            {
                return false;
            }

            if (!real)
            {
                if (
                    long.TryParse(token, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long whole)
                    && whole >= -2147483647L
                    && whole <= 2147483647L
                )
                {
                    number = (int)whole;
                    return true;
                }
            }

            number = ParseReal(token);
            return true;
        }

        /// <summary>Parses a real the way C's atof does for the text it is given: one too large for a double is 0.0.</summary>
        /// <param name="text">Text that is a real.</param>
        /// <returns>The value.</returns>
        internal static double ParseReal(string text)
        {
            try
            {
                double value = double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);
                return double.IsInfinity(value) ? 0.0 : value;
            }
            catch (OverflowException)
            {
                return 0.0;
            }
        }
    }
}
