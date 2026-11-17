using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace GeometryHelper.Cad
{
    /// <summary>
    /// Reads the objects of a PDF from bytes: numbers, strings, names, arrays, dictionaries, references and, for a content
    /// stream, the operators among them. One parser walks one buffer from a position; it never throws on a file cut short, it
    /// reads what is there and returns null at the end.
    /// </summary>
    internal sealed class PdfParser
    {
        private readonly byte[] _data;
        private readonly int _end;
        private readonly bool _references;

        /// <summary>Makes a parser of a buffer from a position.</summary>
        /// <param name="data">The bytes.</param>
        /// <param name="start">Where to begin.</param>
        /// <param name="end">Where the bytes end.</param>
        /// <param name="references">Whether <c>12 0 R</c> is read as a reference: in a file, not in a content stream.</param>
        public PdfParser(byte[] data, int start, int end, bool references)
        {
            _data = data;
            Position = start;
            _end = Math.Min(end, data.Length);
            _references = references;
        }

        /// <summary>Gets or sets where the parser is.</summary>
        public int Position { get; set; }

        /// <summary>Gets whether the parser is at the end of its bytes, ignoring white space and comments.</summary>
        public bool AtEnd
        {
            get
            {
                SkipWhite();
                return Position >= _end;
            }
        }

        internal static bool IsWhite(int c) => c == 0 || c == 9 || c == 10 || c == 12 || c == 13 || c == 32;

        internal static bool IsDelimiter(int c) =>
            c == '('
            || c == ')'
            || c == '<'
            || c == '>'
            || c == '['
            || c == ']'
            || c == '{'
            || c == '}'
            || c == '/'
            || c == '%';

        /// <summary>Skips white space and comments.</summary>
        public void SkipWhite()
        {
            while (Position < _end)
            {
                int c = _data[Position];

                if (IsWhite(c))
                {
                    Position++;
                }
                else if (c == '%')
                {
                    while (Position < _end && _data[Position] != 10 && _data[Position] != 13)
                    {
                        Position++;
                    }
                }
                else
                {
                    break;
                }
            }
        }

        /// <summary>
        /// Reads the next object: a double, a <see cref="PdfString"/>, a <see cref="PdfName"/>, a <c>List&lt;object&gt;</c>, a
        /// <see cref="PdfDict"/>, a <see cref="PdfRef"/>, a bool, a <see cref="PdfKeyword"/>; null at the end of the bytes and for
        /// <c>null</c>. A closing <c>]</c> or <c>&gt;&gt;</c> met out of place is a keyword.
        /// </summary>
        public object Read() => Read(0);

        private object Read(int depth)
        {
            SkipWhite();

            if (Position >= _end)
            {
                return null;
            }

            if (depth > 64)
            {
                throw new PdfException("The objects are nested too deeply.");
            }

            int c = _data[Position];

            switch (c)
            {
                case '/':
                    return new PdfName(ReadName());
                case '(':
                    return new PdfString(ReadLiteral());
                case '[':
                    Position++;
                    return ReadArray(depth);
                case ']':
                    Position++;
                    return new PdfKeyword("]");
                case '<':
                    if (Position + 1 < _end && _data[Position + 1] == '<')
                    {
                        Position += 2;
                        return ReadDictionary(depth);
                    }

                    return new PdfString(ReadHex());
                case '>':
                    if (Position + 1 < _end && _data[Position + 1] == '>')
                    {
                        Position += 2;
                        return new PdfKeyword(">>");
                    }

                    Position++;
                    return new PdfKeyword(">");
                case '{':
                case '}':
                case ')':
                    Position++;
                    return new PdfKeyword(((char)c).ToString());
            }

            if (c == '+' || c == '-' || c == '.' || (c >= '0' && c <= '9'))
            {
                int start = Position;

                if (TryReadNumber(out double number, out bool whole))
                {
                    if (whole && _references && number >= 0 && number < int.MaxValue)
                    {
                        int mark = Position;
                        PdfRef reference = TryReference((int)number);

                        if (reference != null)
                        {
                            return reference;
                        }

                        Position = mark;
                    }

                    return number;
                }

                Position = start;
            }

            string word = ReadWord();

            switch (word)
            {
                case "true":
                    return true;
                case "false":
                    return false;
                case "null":
                    return null;
                default:
                    return new PdfKeyword(word);
            }
        }

        /// <summary>After an integer: a generation and <c>R</c> make a reference; null where they do not follow.</summary>
        private PdfRef TryReference(int number)
        {
            SkipWhite();
            int start = Position;

            while (Position < _end && _data[Position] >= '0' && _data[Position] <= '9')
            {
                Position++;
            }

            if (Position == start || Position - start > 6)
            {
                return null;
            }

            int generation = int.Parse(
                System.Text.Encoding.ASCII.GetString(_data, start, Position - start),
                CultureInfo.InvariantCulture
            );
            SkipWhite();

            if (
                Position < _end
                && _data[Position] == 'R'
                && (Position + 1 >= _end || IsWhite(_data[Position + 1]) || IsDelimiter(_data[Position + 1]))
            )
            {
                Position++;
                return new PdfRef(number, generation);
            }

            return null;
        }

        private bool TryReadNumber(out double value, out bool whole)
        {
            value = 0.0;
            whole = true;
            int start = Position;
            int p = Position;
            bool negative = false;
            bool any = false;

            while (p < _end && (_data[p] == '+' || _data[p] == '-'))
            {
                negative = _data[p] == '-' ? true : negative;
                p++;
            }

            double integer = 0.0;

            while (p < _end && _data[p] >= '0' && _data[p] <= '9')
            {
                integer = integer * 10.0 + (_data[p] - '0');
                any = true;
                p++;
            }

            double fraction = 0.0;

            if (p < _end && _data[p] == '.')
            {
                whole = false;
                p++;
                double scale = 0.1;

                while (p < _end && _data[p] >= '0' && _data[p] <= '9')
                {
                    fraction += (_data[p] - '0') * scale;
                    scale *= 0.1;
                    any = true;
                    p++;
                }

                // A second dot ends a number the way Acrobat reads one: "1.2.3" is 1.2.
                while (p < _end && (_data[p] == '.' || (_data[p] >= '0' && _data[p] <= '9')))
                {
                    p++;
                }
            }

            if (!any || (p < _end && !IsWhite(_data[p]) && !IsDelimiter(_data[p])))
            {
                // Something like 5l or 1e5: not a number of PDF; read it as a keyword.
                if (any && p < _end && (_data[p] == 'e' || _data[p] == 'E'))
                {
                    int q = p + 1;

                    if (q < _end && (_data[q] == '+' || _data[q] == '-'))
                    {
                        q++;
                    }

                    int digits = q;

                    while (q < _end && _data[q] >= '0' && _data[q] <= '9')
                    {
                        q++;
                    }

                    if (q > digits)
                    {
                        string text = System.Text.Encoding.ASCII.GetString(_data, start, q - start);

                        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double big))
                        {
                            value = big;
                            whole = false;
                            Position = q;
                            return true;
                        }
                    }
                }

                if (!any)
                {
                    return false;
                }
            }

            value = integer + fraction;

            if (negative)
            {
                value = -value;
            }

            Position = p;
            return true;
        }

        private string ReadName()
        {
            Position++;
            var bytes = new List<byte>();

            while (Position < _end)
            {
                int c = _data[Position];

                if (IsWhite(c) || IsDelimiter(c))
                {
                    break;
                }

                if (c == '#' && Position + 2 < _end && IsHex(_data[Position + 1]) && IsHex(_data[Position + 2]))
                {
                    bytes.Add((byte)((Hex(_data[Position + 1]) << 4) | Hex(_data[Position + 2])));
                    Position += 3;
                    continue;
                }

                bytes.Add((byte)c);
                Position++;
            }

            return System.Text.Encoding.UTF8.GetString(bytes.ToArray());
        }

        private string ReadWord()
        {
            int start = Position;

            while (Position < _end && !IsWhite(_data[Position]) && !IsDelimiter(_data[Position]))
            {
                Position++;
            }

            if (Position == start)
            {
                Position++;
            }

            return System.Text.Encoding.ASCII.GetString(_data, start, Position - start);
        }

        private byte[] ReadLiteral()
        {
            Position++;
            var bytes = new List<byte>();
            int depth = 1;

            while (Position < _end)
            {
                int c = _data[Position++];

                if (c == '\\' && Position < _end)
                {
                    int e = _data[Position++];

                    switch (e)
                    {
                        case 'n':
                            bytes.Add(10);
                            break;
                        case 'r':
                            bytes.Add(13);
                            break;
                        case 't':
                            bytes.Add(9);
                            break;
                        case 'b':
                            bytes.Add(8);
                            break;
                        case 'f':
                            bytes.Add(12);
                            break;
                        case 13:
                            if (Position < _end && _data[Position] == 10)
                            {
                                Position++;
                            }

                            break;
                        case 10:
                            break;
                        default:
                            if (e >= '0' && e <= '7')
                            {
                                int octal = e - '0';

                                for (
                                    int i = 0;
                                    i < 2 && Position < _end && _data[Position] >= '0' && _data[Position] <= '7';
                                    i++
                                )
                                {
                                    octal = octal * 8 + (_data[Position++] - '0');
                                }

                                bytes.Add((byte)octal);
                            }
                            else
                            {
                                bytes.Add((byte)e);
                            }

                            break;
                    }

                    continue;
                }

                if (c == '(')
                {
                    depth++;
                }
                else if (c == ')')
                {
                    depth--;

                    if (depth == 0)
                    {
                        break;
                    }
                }

                bytes.Add((byte)c);
            }

            return bytes.ToArray();
        }

        private byte[] ReadHex()
        {
            Position++;
            var bytes = new List<byte>();
            int high = -1;

            while (Position < _end)
            {
                int c = _data[Position++];

                if (c == '>')
                {
                    break;
                }

                if (!IsHex(c))
                {
                    continue;
                }

                if (high < 0)
                {
                    high = Hex(c);
                }
                else
                {
                    bytes.Add((byte)((high << 4) | Hex(c)));
                    high = -1;
                }
            }

            if (high >= 0)
            {
                bytes.Add((byte)(high << 4));
            }

            return bytes.ToArray();
        }

        private static bool IsHex(int c) => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');

        private static int Hex(int c) => c <= '9' ? c - '0' : (c | 0x20) - 'a' + 10;

        private List<object> ReadArray(int depth)
        {
            var list = new List<object>();

            while (true)
            {
                object item = Read(depth + 1);

                if (item == null && AtEnd)
                {
                    break;
                }

                if (item is PdfKeyword keyword)
                {
                    if (keyword.Value == "]")
                    {
                        break;
                    }

                    if (keyword.Value == ">>" || keyword.Value == "endobj")
                    {
                        break;
                    }
                }

                list.Add(item);
            }

            return list;
        }

        private PdfDict ReadDictionary(int depth)
        {
            var dictionary = new PdfDict();

            while (true)
            {
                object key = Read(depth + 1);

                if (key == null && AtEnd)
                {
                    break;
                }

                if (key is PdfKeyword closing)
                {
                    if (closing.Value == ">>" || closing.Value == "endobj" || closing.Value == "stream")
                    {
                        if (closing.Value != ">>")
                        {
                            Position -= closing.Value.Length;
                        }

                        break;
                    }

                    continue;
                }

                if (!(key is PdfName name))
                {
                    continue;
                }

                int mark = Position;
                object value = Read(depth + 1);

                if (value is PdfKeyword stop && (stop.Value == ">>" || stop.Value == "endobj"))
                {
                    if (stop.Value == "endobj")
                    {
                        Position = mark;
                    }

                    dictionary[name.Value] = null;
                    break;
                }

                dictionary[name.Value] = value;
            }

            return dictionary;
        }
    }
}
