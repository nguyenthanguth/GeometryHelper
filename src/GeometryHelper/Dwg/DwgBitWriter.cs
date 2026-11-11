using System;
using System.Text;

namespace GeometryHelper.Dwg
{
    /// <summary>
    /// Writes the bit codes of a DWG file into a run of bytes that grows as it is written, the mirror of
    /// <see cref="DwgBitReader"/>: single bits, the raw little-endian numbers and the compressed forms (bit short, bit long,
    /// bit double, modular char, handle reference and so on) as the Open Design Specification for .dwg files names them.
    /// The position is counted in bits, the high bit of each byte first; the bits of the last byte not written are 0.
    /// Each code is written in the shortest form the specification allows, as AutoCAD writes it, so that what one writes
    /// reads back to the same value and the same bits.
    /// </summary>
    internal sealed class DwgBitWriter
    {
        private byte[] _data = new byte[256];
        private long _position;

        /// <summary>The position of the next bit to write, counted from the first bit; the number of bits written.</summary>
        public long Position => _position;

        /// <summary>The bytes written, the last one padded with 0 bits.</summary>
        public byte[] ToArray()
        {
            var bytes = new byte[(_position + 7) >> 3];
            Array.Copy(_data, bytes, bytes.Length);
            return bytes;
        }

        /// <summary>B: one bit.</summary>
        public void Bit(bool value)
        {
            Grow(1);

            if (value)
            {
                _data[_position >> 3] |= (byte)(0x80 >> (int)(_position & 7));
            }

            _position++;
        }

        /// <summary>The low <paramref name="count"/> bits (at most 32) of <paramref name="value"/>, the highest first.</summary>
        public void Bits(uint value, int count)
        {
            for (int i = count - 1; i >= 0; i--)
            {
                Bit(((value >> i) & 1) != 0);
            }
        }

        /// <summary>BB: two bits, the first the higher.</summary>
        public void Bits2(int value) => Bits((uint)value & 3, 2);

        /// <summary>3B: the number 0, 2, 6 or 7 as the bits read while each is set: 0, 10, 110, 111.</summary>
        public void Bits3(int value)
        {
            switch (value)
            {
                case 0:
                    Bit(false);
                    break;
                case 2:
                    Bit(true);
                    Bit(false);
                    break;
                case 6:
                    Bit(true);
                    Bit(true);
                    Bit(false);
                    break;
                case 7:
                    Bit(true);
                    Bit(true);
                    Bit(true);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(value), "A 3B code holds 0, 2, 6 or 7.");
            }
        }

        /// <summary>RC: one byte.</summary>
        public void RawChar(int value)
        {
            Grow(8);
            int index = (int)(_position >> 3);
            int shift = (int)(_position & 7);
            byte b = (byte)value;

            if (shift == 0)
            {
                _data[index] = b;
            }
            else
            {
                _data[index] |= (byte)(b >> shift);
                _data[index + 1] = (byte)(b << (8 - shift));
            }

            _position += 8;
        }

        /// <summary>The bytes, each as a raw char.</summary>
        public void Bytes(byte[] bytes)
        {
            Bytes(bytes, 0, bytes.Length);
        }

        /// <summary><paramref name="count"/> bytes of <paramref name="bytes"/> from <paramref name="start"/>, each as a raw char.</summary>
        public void Bytes(byte[] bytes, int start, int count)
        {
            for (int i = 0; i < count; i++)
            {
                RawChar(bytes[start + i]);
            }
        }

        /// <summary>RS: a 16-bit number, low byte first.</summary>
        public void RawShort(int value)
        {
            RawChar(value);
            RawChar(value >> 8);
        }

        /// <summary>RL: a 32-bit number, low byte first.</summary>
        public void RawLong(int value)
        {
            RawChar(value);
            RawChar(value >> 8);
            RawChar(value >> 16);
            RawChar(value >> 24);
        }

        /// <summary>RD: a 64-bit IEEE number, low byte first.</summary>
        public void RawDouble(double value)
        {
            long bits = BitConverter.DoubleToInt64Bits(value);

            for (int i = 0; i < 8; i++)
            {
                RawChar((int)(bits >> (8 * i)));
            }
        }

        /// <summary>BS: a bit short: 10 for 0, 11 for 256, 01 and a byte for 1 to 255, else 00 and a raw short.</summary>
        public void BitShort(int value)
        {
            value &= 0xFFFF;

            if (value == 0)
            {
                Bits2(2);
            }
            else if (value == 256)
            {
                Bits2(3);
            }
            else if (value < 256)
            {
                Bits2(1);
                RawChar(value);
            }
            else
            {
                Bits2(0);
                RawShort(value);
            }
        }

        /// <summary>BL: a bit long: 10 for 0, 01 and a byte for 1 to 255, else 00 and a raw long.</summary>
        public void BitLong(int value)
        {
            if (value == 0)
            {
                Bits2(2);
            }
            else if (value > 0 && value < 256)
            {
                Bits2(1);
                RawChar(value);
            }
            else
            {
                Bits2(0);
                RawLong(value);
            }
        }

        /// <summary>BLL: a bit long long: three bits of the count of bytes that follow, as few as hold the value, low byte first.</summary>
        public void BitLongLong(long value)
        {
            ulong rest = (ulong)value;
            int count = 0;

            while (rest != 0)
            {
                rest >>= 8;
                count++;
            }

            if (count > 7)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "A bit long long holds at most seven bytes.");
            }

            Bits((uint)count, 3);

            for (int i = 0; i < count; i++)
            {
                RawChar((int)((ulong)value >> (8 * i)));
            }
        }

        /// <summary>BD: a bit double: 10 for 0, 01 for 1, else 00 and a raw double. A negative zero is written whole.</summary>
        public void BitDouble(double value)
        {
            long bits = BitConverter.DoubleToInt64Bits(value);

            if (bits == 0)
            {
                Bits2(2);
            }
            else if (bits == BitConverter.DoubleToInt64Bits(1.0))
            {
                Bits2(1);
            }
            else
            {
                Bits2(0);
                RawDouble(value);
            }
        }

        /// <summary>
        /// DD: a bit double with a default: 00 where it is the default, 01 and four bytes where only its first four bytes
        /// differ from the default's, 10 and six bytes where only its first four and its fifth and sixth do, else 11 and a
        /// raw double.
        /// </summary>
        public void BitDoubleDefault(double value, double defaultValue)
        {
            byte[] bytes = LittleEndian(value);
            byte[] given = LittleEndian(defaultValue);

            if (Same(bytes, given, 0, 8))
            {
                Bits2(0);
            }
            else if (Same(bytes, given, 4, 8))
            {
                Bits2(1);
                Bytes(bytes, 0, 4);
            }
            else if (Same(bytes, given, 6, 8))
            {
                Bits2(2);
                RawChar(bytes[4]);
                RawChar(bytes[5]);
                Bytes(bytes, 0, 4);
            }
            else
            {
                Bits2(3);
                Bytes(bytes, 0, 8);
            }
        }

        /// <summary>
        /// MC: a modular char. Bytes of seven bits each, the low group first, the high bit of a byte saying another follows;
        /// where <paramref name="signed"/> is true the last byte holds six bits and bit 0x40 is the sign.
        /// </summary>
        public void ModularChar(long value, bool signed = true)
        {
            bool negative = signed && value < 0;
            ulong rest = negative ? (ulong)(-value) : (ulong)value;

            while (true)
            {
                if (signed ? rest < 0x40 : rest < 0x80)
                {
                    RawChar((int)rest | (negative ? 0x40 : 0));
                    return;
                }

                RawChar((int)(rest & 0x7F) | 0x80);
                rest >>= 7;
            }
        }

        /// <summary>MS: a modular short. Raw shorts of fifteen bits each, the low group first, the high bit saying another follows.</summary>
        public void ModularShort(long value)
        {
            ulong rest = (ulong)value;

            while (rest >= 0x8000)
            {
                RawShort((int)(rest & 0x7FFF) | 0x8000);
                rest >>= 15;
            }

            RawShort((int)rest);
        }

        /// <summary>
        /// H: a handle reference: four bits of <paramref name="code"/>, four bits of the count of bytes, as few as hold
        /// <paramref name="value"/> (none for 0), then the bytes, high byte first.
        /// </summary>
        public void Handle(int code, ulong value)
        {
            int count = 0;
            ulong rest = value;

            while (rest != 0)
            {
                rest >>= 8;
                count++;
            }

            Bits((uint)code & 0xF, 4);
            Bits((uint)count, 4);

            for (int i = count - 1; i >= 0; i--)
            {
                RawChar((int)(value >> (8 * i)));
            }
        }

        /// <summary>
        /// A handle written relative to <paramref name="own"/> as AutoCAD writes the handles of entities that name the next
        /// and the previous one: 6 one after, 8 one before, 0xA after by the bytes, 0xC before by the bytes; 0 as code 4.
        /// </summary>
        public void RelativeHandle(ulong own, ulong target)
        {
            if (target == 0)
            {
                Handle(4, 0);
            }
            else if (target == own + 1)
            {
                Handle(6, 0);
            }
            else if (target == own - 1)
            {
                Handle(8, 0);
            }
            else if (target > own)
            {
                Handle(0xA, target - own);
            }
            else
            {
                Handle(0xC, own - target);
            }
        }

        /// <summary>
        /// TV before R2007: a bit short of the count of bytes, which takes in a closing zero, then the bytes and the zero; an
        /// empty text is the count 0 alone.
        /// </summary>
        public void Text(byte[] text)
        {
            if (text == null || text.Length == 0)
            {
                BitShort(0);
                return;
            }

            BitShort(text.Length + 1);
            Bytes(text);
            RawChar(0);
        }

        /// <summary>
        /// TU from R2007: a bit short of the count of UTF-16 characters, then the characters, two bytes each, low byte first,
        /// with no closing zero, as AutoCAD writes them.
        /// </summary>
        public void TextUnicode(string text)
        {
            text = text ?? string.Empty;
            BitShort(text.Length);
            Bytes(Encoding.Unicode.GetBytes(text));
        }

        /// <summary>The text of an XRECORD or of extended data from R2007: a raw short of the count of UTF-16 characters, then the characters, no closing zero.</summary>
        public void TextUnicodeRaw(string text)
        {
            text = text ?? string.Empty;
            RawShort(text.Length);
            Bytes(Encoding.Unicode.GetBytes(text));
        }

        /// <summary>OT from R2010: 00 and a byte below 0x100, 01 and a byte for 0x1F0 to 0x2EF, else 10 and a raw short.</summary>
        public void ObjectType(int type)
        {
            if (type < 0x100)
            {
                Bits2(0);
                RawChar(type);
            }
            else if (type >= 0x1F0 && type < 0x2F0)
            {
                Bits2(1);
                RawChar(type - 0x1F0);
            }
            else
            {
                Bits2(2);
                RawShort(type);
            }
        }

        /// <summary>BT from R2000: a set bit for a thickness of 0, else a clear bit and a bit double.</summary>
        public void BitThickness(double thickness)
        {
            if (BitConverter.DoubleToInt64Bits(thickness) == 0)
            {
                Bit(true);
                return;
            }

            Bit(false);
            BitDouble(thickness);
        }

        /// <summary>BE from R2000: a set bit for the extrusion (0, 0, 1), else a clear bit and three bit doubles.</summary>
        public void BitExtrusion(double[] extrusion)
        {
            if (
                BitConverter.DoubleToInt64Bits(extrusion[0]) == 0
                && BitConverter.DoubleToInt64Bits(extrusion[1]) == 0
                && extrusion[2] == 1.0
            )
            {
                Bit(true);
                return;
            }

            Bit(false);
            Point3(extrusion);
        }

        /// <summary>3BD: three bit doubles.</summary>
        public void Point3(double[] point)
        {
            BitDouble(point[0]);
            BitDouble(point[1]);
            BitDouble(point[2]);
        }

        /// <summary>2RD: two raw doubles.</summary>
        public void RawPoint2(double[] point)
        {
            RawDouble(point[0]);
            RawDouble(point[1]);
        }

        /// <summary>3RD: three raw doubles.</summary>
        public void RawPoint3(double[] point)
        {
            RawDouble(point[0]);
            RawDouble(point[1]);
            RawDouble(point[2]);
        }

        /// <summary>The bits another writer holds, from its first to its position, after those written here.</summary>
        public void Append(DwgBitWriter other)
        {
            long whole = other._position >> 3;

            for (long i = 0; i < whole; i++)
            {
                RawChar(other._data[i]);
            }

            int left = (int)(other._position & 7);

            if (left > 0)
            {
                Bits((uint)(other._data[whole] >> (8 - left)), left);
            }
        }

        /// <summary>Bits up to the next whole byte: zeros, or ones where <paramref name="ones"/> is set, as AutoCAD ends an object.</summary>
        public void PadToByte(bool ones = false)
        {
            while ((_position & 7) != 0)
            {
                Bit(ones);
            }
        }

        private static byte[] LittleEndian(double value)
        {
            byte[] bytes = BitConverter.GetBytes(value);

            if (!BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }

            return bytes;
        }

        private static bool Same(byte[] a, byte[] b, int from, int to)
        {
            for (int i = from; i < to; i++)
            {
                if (a[i] != b[i])
                {
                    return false;
                }
            }

            return true;
        }

        private void Grow(int bits)
        {
            long need = (_position + bits + 7) >> 3;

            if (need + 1 > _data.Length)
            {
                Array.Resize(ref _data, (int)Math.Max(need + 1, _data.Length * 2L));
            }
        }
    }
}
