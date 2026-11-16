using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace GeometryHelper.Dwg
{
    /// <summary>
    /// Writes the DXF a DWG file is turned into, as bytes: the texts of the DWG go in as the bytes they are, in the
    /// drawing's code page, so the DXF reader decodes them as it decodes any DXF file. A control character in a text is
    /// written the way DXF writes it, a caret and the letter 64 above it, and a caret as a caret and a space. The bytes
    /// are made in one array that doubles as it fills, with no stream and no string made for a code, a whole number, a
    /// handle or a number that is whole: a DXF of 46 MB, 300 000 entities, is some 4 million groups.
    /// </summary>
    internal sealed class DwgDxfWriter
    {
        /// <summary>The most bytes an array holds, as the stream this was made of held.</summary>
        private const int MostBytes = 0x7FFFFFC7;

        /// <summary>Whether a double formats as the shortest text that brings it back, as .NET Core 3.0 and later does and .NET Framework does not.</summary>
        private static readonly bool ShortestRoundTrip =
            double.Epsilon.ToString("R", CultureInfo.InvariantCulture) == "5E-324";

        private byte[] _buffer;
        private int _length;

        /// <summary>A writer with room for <paramref name="capacity"/> bytes made at once, where the DXF will be about that long: its array is then not grown, and the arrays it grew through let go of, as it fills.</summary>
        public DwgDxfWriter(int capacity = 1 << 16)
        {
            _buffer = new byte[Math.Max(capacity, 1 << 12)];
        }

        /// <summary>
        /// The array the DXF is written in and its length, with no copy of it where the array is not much longer than the DXF;
        /// the bytes past the length are not DXF. Where a quarter of the array or more is unused it is cut to its length, for
        /// the drawing that is opened from it holds the array as long as it is open.
        /// </summary>
        public byte[] Take(out int length)
        {
            length = _length;
            return _buffer.Length - _length > _length / 4 ? ToArray() : _buffer;
        }

        /// <summary>The DXF written so far.</summary>
        public byte[] ToArray()
        {
            var bytes = new byte[_length];
            Buffer.BlockCopy(_buffer, 0, bytes, 0, _length);
            return bytes;
        }

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
            Whole(value);
            Line();
        }

        /// <summary>A group whose value is a number, written with the 17 digits that bring every double back.</summary>
        public void Write(int code, double value)
        {
            Code(code);
            Real(value);
            Line();
        }

        /// <summary>A group whose value is a handle, in hex.</summary>
        public void WriteHandle(int code, ulong handle)
        {
            Code(code);
            Reserve(16);

            int digits = 1;

            for (ulong rest = handle >> 4; rest != 0; rest >>= 4)
            {
                digits++;
            }

            for (int at = _length + digits - 1; at >= _length; at--)
            {
                int nibble = (int)(handle & 0xF);
                _buffer[at] = (byte)(nibble < 10 ? '0' + nibble : 'A' + nibble - 10);
                handle >>= 4;
            }

            _length += digits;
            Line();
        }

        /// <summary>A group whose value is a text of the DWG, as its bytes.</summary>
        public void Write(int code, byte[] text)
        {
            Code(code);
            byte[] escaped = Escape(text);
            Reserve(escaped.Length);
            Buffer.BlockCopy(escaped, 0, _buffer, _length, escaped.Length);
            _length += escaped.Length;
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
                Bytes(escaped, from, to - from);
                Line();
                from = to;
            }

            Code(lastCode);
            Bytes(escaped, from, escaped.Length - from);
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

            if (Math.Abs(value) >= 1.797693134862315E+308)
            {
                // The largest real, which AutoCAD keeps for a value not set yet, is in its DXF 1.797693134862314E+99; the
                // digits of G15 round past the largest number, which cannot be read back.
                return value < 0 ? "-1.797693134862314E+99" : "1.797693134862314E+99";
            }

            if (value == Math.Floor(value) && Math.Abs(value) < 1E15)
            {
                return value.ToString("0.0", CultureInfo.InvariantCulture);
            }

            double size = Math.Abs(value);

            if (ShortestRoundTrip && size >= 1E-4 && size < 1E14)
            {
                // Where a runtime gives the shortest text that brings a double back, and the number is one of 15 digits or more
                // (not one of the very small ones, which have fewer), that text is the 15 digits where 15 are enough (they are
                // the same digits, the zeros at the end cut) and the 17 digits where 17 are needed (the nearest 17 digits are
                // the shortest of 17); only a text of 16 digits is not the 17 that were written, and takes its 17. Those that
                // are not written in digits alone, as 1E-07 is, are left to the way below.
                string shortest = value.ToString("R", CultureInfo.InvariantCulture);
                return DigitsOf(shortest) == 16 ? value.ToString("G17", CultureInfo.InvariantCulture) : shortest;
            }

            string text = value.ToString("G15", CultureInfo.InvariantCulture);

            if (double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture) != value)
            {
                text = value.ToString("G17", CultureInfo.InvariantCulture);
            }

            return text;
        }

        /// <summary>The digits of a number's text from its first digit that is not nought, up to its exponent.</summary>
        private static int DigitsOf(string text)
        {
            int digits = 0;
            bool started = false;

            foreach (char c in text)
            {
                if (c == 'E')
                {
                    break;
                }

                if (c >= '1' && c <= '9')
                {
                    started = true;
                }

                if (started && c >= '0' && c <= '9')
                {
                    digits++;
                }
            }

            return digits;
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

        /// <summary>Makes room for <paramref name="extra"/> more bytes, the array doubling where it is full.</summary>
        private void Reserve(int extra)
        {
            if (_length > _buffer.Length - extra)
            {
                long needed = (long)_length + extra;

                if (needed > MostBytes)
                {
                    throw new IOException("Stream was too long.");
                }

                Array.Resize(ref _buffer, (int)Math.Min(Math.Max(needed, (long)_buffer.Length * 2), MostBytes));
            }
        }

        private void Bytes(byte[] from, int start, int count)
        {
            Reserve(count);
            Buffer.BlockCopy(from, start, _buffer, _length, count);
            _length += count;
        }

        private void Code(int code)
        {
            Whole(code);
            Line();
        }

        /// <summary>A whole number in decimal.</summary>
        private void Whole(long value)
        {
            if (value == long.MinValue)
            {
                Ascii(value.ToString(CultureInfo.InvariantCulture));
                return;
            }

            Reserve(20);

            if (value < 0)
            {
                _buffer[_length++] = (byte)'-';
                value = -value;
            }

            int digits = 1;

            for (long rest = value / 10; rest != 0; rest /= 10)
            {
                digits++;
            }

            for (int at = _length + digits - 1; at >= _length; at--)
            {
                long next = value / 10;
                _buffer[at] = (byte)('0' + (int)(value - (next * 10)));
                value = next;
            }

            _length += digits;
        }

        /// <summary>A double as <see cref="Number"/> gives it, the whole ones, which are most, with no string made.</summary>
        private void Real(double value)
        {
            // The nought and the whole numbers under 1E15: their digits and ".0". Negative nought is left to
            // Number, which says "-0.0" under one runtime and "0.0" under the other.
            if (
                value == Math.Floor(value)
                && Math.Abs(value) < 1E15
                && (value != 0.0 || BitConverter.DoubleToInt64Bits(value) == 0)
            )
            {
                Whole((long)value);
                Reserve(2);
                _buffer[_length++] = (byte)'.';
                _buffer[_length++] = (byte)'0';
                return;
            }

            Ascii(Number(value));
        }

        private void Ascii(string text)
        {
            Reserve(text.Length);

            foreach (char c in text)
            {
                _buffer[_length++] = c < 0x80 ? (byte)c : (byte)'?';
            }
        }

        private void Line()
        {
            Reserve(2);
            _buffer[_length++] = (byte)'\r';
            _buffer[_length++] = (byte)'\n';
        }
    }
}
