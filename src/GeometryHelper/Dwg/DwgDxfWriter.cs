using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace GeometryHelper.Dwg
{
    /// <summary>
    /// Writes the DXF a DWG file is turned into, as bytes: the texts of the DWG go in as the bytes they are, in the
    /// drawing's code page, so the DXF reader decodes them as it decodes any DXF file. A control character in a text is
    /// written the way DXF writes it, a caret and the letter 64 above it, and a caret as a caret and a space.
    /// </summary>
    internal sealed class DwgDxfWriter
    {
        private readonly MemoryStream _stream = new MemoryStream();

        /// <summary>The DXF written so far.</summary>
        public byte[] ToArray() => _stream.ToArray();

        /// <summary>A group whose value is plain ASCII.</summary>
        public void Write(int code, string value)
        {
            Code(code);
            Ascii(value);
            Line();
        }

        /// <summary>A group whose value is a whole number.</summary>
        public void Write(int code, long value)
        {
            Code(code);
            Ascii(value.ToString(CultureInfo.InvariantCulture));
            Line();
        }

        /// <summary>A group whose value is a number, written with the 17 digits that bring every double back.</summary>
        public void Write(int code, double value)
        {
            Code(code);
            Ascii(Number(value));
            Line();
        }

        /// <summary>A group whose value is a handle, in hex.</summary>
        public void WriteHandle(int code, ulong handle)
        {
            Code(code);
            Ascii(handle.ToString("X", CultureInfo.InvariantCulture));
            Line();
        }

        /// <summary>A group whose value is a text of the DWG, as its bytes.</summary>
        public void Write(int code, byte[] text)
        {
            Code(code);
            byte[] escaped = Escape(text);
            _stream.Write(escaped, 0, escaped.Length);
            Line();
        }

        /// <summary>A point: its coordinates under <paramref name="code"/>, the code plus 10 and, of three, plus 20.</summary>
        public void WritePoint(int code, double[] point)
        {
            for (int i = 0; i < point.Length; i++)
            {
                Write(code + (10 * i), point[i]);
            }
        }

        /// <summary>
        /// A long text in pieces of at most <paramref name="size"/> bytes: every piece but the last under
        /// <paramref name="pieceCode"/>, the last under <paramref name="lastCode"/>. No piece ends between a caret and the
        /// character it marks, nor, where <paramref name="doubleByte"/> is true, between the two bytes of a character, nor,
        /// where <paramref name="utf8"/> is true, inside a character of UTF-8.
        /// </summary>
        public void WriteChunks(int pieceCode, int lastCode, byte[] text, int size, bool doubleByte, bool utf8 = false)
        {
            byte[] escaped = Escape(text);
            int from = 0;

            while (escaped.Length - from > size)
            {
                int to = from;

                while (true)
                {
                    byte b = escaped[to];
                    int step =
                        b == (byte)'^' || (doubleByte && b > 0x80) ? 2
                        : !utf8 || b < 0xC0 ? 1
                        : b < 0xE0 ? 2
                        : b < 0xF0 ? 3
                        : 4;

                    if (to + step - from > size)
                    {
                        break;
                    }

                    to += step;
                }

                Code(pieceCode);
                _stream.Write(escaped, from, to - from);
                Line();
                from = to;
            }

            Code(lastCode);
            _stream.Write(escaped, from, escaped.Length - from);
            Line();
        }

        /// <summary>The value of a group written as DXF writes it: an int, a long, a double, a string, bytes or a handle.</summary>
        public void WriteValue(int code, object value)
        {
            switch (value)
            {
                case byte number:
                    Write(code, (long)number);
                    break;
                case short number:
                    Write(code, (long)number);
                    break;
                case int number:
                    Write(code, (long)number);
                    break;
                case long number:
                    Write(code, number);
                    break;
                case double number:
                    Write(code, number);
                    break;
                case string text:
                    Write(code, text);
                    break;
                case byte[] text:
                    Write(code, text);
                    break;
                case ulong handle:
                    WriteHandle(code, handle);
                    break;
                case double[] point:
                    WritePoint(code, point);
                    break;
                default:
                    throw new InvalidOperationException(
                        "A DXF group cannot carry a value of the kind "
                            + (value == null ? "null" : value.GetType().Name)
                            + "."
                    );
            }
        }

        /// <summary>A number as DXF text: a whole number with one decimal, any other with the digits that bring it back.</summary>
        public static string Number(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                return "0.0";
            }

            if (value == Math.Floor(value) && Math.Abs(value) < 1E15)
            {
                return value.ToString("0.0", CultureInfo.InvariantCulture);
            }

            string text = value.ToString("G15", CultureInfo.InvariantCulture);

            if (double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture) != value)
            {
                text = value.ToString("G17", CultureInfo.InvariantCulture);
            }

            return text;
        }

        private static byte[] Escape(byte[] text)
        {
            int extra = 0;

            foreach (byte b in text)
            {
                if (b < 0x20 || b == (byte)'^')
                {
                    extra++;
                }
            }

            if (extra == 0)
            {
                return text;
            }

            var escaped = new List<byte>(text.Length + extra);

            foreach (byte b in text)
            {
                if (b == (byte)'^')
                {
                    escaped.Add(b);
                    escaped.Add((byte)' ');
                }
                else if (b < 0x20)
                {
                    escaped.Add((byte)'^');
                    escaped.Add((byte)(b + 0x40));
                }
                else
                {
                    escaped.Add(b);
                }
            }

            return escaped.ToArray();
        }

        private void Code(int code)
        {
            Ascii(code.ToString(CultureInfo.InvariantCulture));
            Line();
        }

        private void Ascii(string text)
        {
            foreach (char c in text)
            {
                _stream.WriteByte(c < 0x80 ? (byte)c : (byte)'?');
            }
        }

        private void Line()
        {
            _stream.WriteByte((byte)'\r');
            _stream.WriteByte((byte)'\n');
        }
    }
}
