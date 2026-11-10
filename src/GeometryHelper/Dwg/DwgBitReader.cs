using System;

namespace GeometryHelper.Dwg
{
    /// <summary>
    /// Reads the bit codes of a DWG file from a run of bytes: single bits, the raw little-endian numbers, and the
    /// compressed forms (bit short, bit long, bit double, modular char, handle reference and so on) as the Open Design
    /// Specification for .dwg files names them. The position is counted in bits from the start of the bytes, the high bit
    /// of each byte first. Reading past the end throws <see cref="FormatException"/>, so no loop over a broken file runs
    /// beyond its bytes.
    /// </summary>
    internal sealed class DwgBitReader
    {
        private readonly byte[] _data;
        private long _position;
        private long _end;

        /// <summary>A reader over the whole of <paramref name="data"/>, starting at its first bit.</summary>
        public DwgBitReader(byte[] data)
            : this(data, 0, data.LongLength * 8)
        {
        }

        /// <summary>A reader over the bits from <paramref name="startBit"/> up to, not including, <paramref name="endBit"/>.</summary>
        public DwgBitReader(byte[] data, long startBit, long endBit)
        {
            _data = data;
            _end = Math.Max(0, Math.Min(endBit, data.LongLength * 8));
            _position = Math.Max(0, Math.Min(startBit, _end));
        }

        /// <summary>The bytes being read.</summary>
        public byte[] Data => _data;

        /// <summary>The position of the next bit to read, counted from the first bit of the bytes.</summary>
        public long Position
        {
            get => _position;
            set
            {
                if (value < 0 || value > _end)
                {
                    throw Ended();
                }

                _position = value;
            }
        }

        /// <summary>The position one past the last bit that may be read.</summary>
        public long End => _end;

        /// <summary>The number of bits left to read.</summary>
        public long Remaining => _end - _position;

        /// <summary>B: one bit.</summary>
        public bool Bit()
        {
            if (_position >= _end)
            {
                throw Ended();
            }

            bool set = (_data[_position >> 3] & (0x80 >> (int)(_position & 7))) != 0;
            _position++;
            return set;
        }

        /// <summary>BB: two bits, the first the higher.</summary>
        public int Bits2() => (int)Bits(2);

        /// <summary>3B: up to three bits, read while each bit is set; the bits read so far make the number.</summary>
        public int Bits3()
        {
            int value = 0;

            for (int i = 0; i < 3; i++)
            {
                if (!Bit())
                {
                    value <<= 1;
                    break;
                }

                value = (value << 1) | 1;
            }

            return value;
        }

        /// <summary>The next <paramref name="count"/> bits (at most 32) as one number, the first bit the highest.</summary>
        public uint Bits(int count)
        {
            uint value = 0;

            for (int i = 0; i < count; i++)
            {
                value = (value << 1) | (Bit() ? 1u : 0u);
            }

            return value;
        }

        /// <summary>RC: one byte.</summary>
        public byte RawChar()
        {
            if (_position + 8 > _end)
            {
                throw Ended();
            }

            int index = (int)(_position >> 3);
            int shift = (int)(_position & 7);
            _position += 8;

            if (shift == 0)
            {
                return _data[index];
            }

            return (byte)((_data[index] << shift) | (_data[index + 1] >> (8 - shift)));
        }

        /// <summary>The next <paramref name="count"/> bytes.</summary>
        public byte[] Bytes(int count)
        {
            if (count < 0 || _position + (count * 8L) > _end)
            {
                throw Ended();
            }

            byte[] bytes = new byte[count];

            for (int i = 0; i < count; i++)
            {
                bytes[i] = RawChar();
            }

            return bytes;
        }

        /// <summary>RS: a 16-bit number, low byte first.</summary>
        public int RawShort()
        {
            int low = RawChar();
            return (short)(low | (RawChar() << 8));
        }

        /// <summary>RL: a 32-bit number, low byte first.</summary>
        public int RawLong()
        {
            int value = RawChar();
            value |= RawChar() << 8;
            value |= RawChar() << 16;
            return value | (RawChar() << 24);
        }

        /// <summary>RD: a 64-bit IEEE number, low byte first.</summary>
        public double RawDouble()
        {
            long bits = 0;

            for (int i = 0; i < 8; i++)
            {
                bits |= (long)RawChar() << (8 * i);
            }

            return BitConverter.Int64BitsToDouble(bits);
        }

        /// <summary>BS: a bit short. Two bits say what follows: 00 a raw short, 01 one byte, 10 nothing (0), 11 nothing (256).</summary>
        public int BitShort()
        {
            switch (Bits2())
            {
                case 0: return RawShort();
                case 1: return RawChar();
                case 2: return 0;
                default: return 256;
            }
        }

        /// <summary>BL: a bit long. Two bits say what follows: 00 a raw long, 01 one byte, 10 nothing (0); 11 is not used.</summary>
        public int BitLong()
        {
            switch (Bits2())
            {
                case 0: return RawLong();
                case 1: return RawChar();
                case 2: return 0;
                default: throw new FormatException("The DWG data has a bit long of the unused kind 11.");
            }
        }

        /// <summary>BLL: a bit long long. Three bits give the count of bytes that follow, low byte first.</summary>
        public long BitLongLong()
        {
            int count = Bits3();
            long value = 0;

            for (int i = 0; i < count; i++)
            {
                value |= (long)RawChar() << (8 * i);
            }

            return value;
        }

        /// <summary>BD: a bit double. Two bits say what follows: 00 a raw double, 01 nothing (1.0), 10 nothing (0.0); 11 is not used.</summary>
        public double BitDouble()
        {
            switch (Bits2())
            {
                case 0: return RawDouble();
                case 1: return 1.0;
                case 2: return 0.0;
                default: throw new FormatException("The DWG data has a bit double of the unused kind 11.");
            }
        }

        /// <summary>
        /// DD: a bit double with a default. Two bits say what follows: 00 nothing (the default), 01 four bytes that replace
        /// the first four of the default's eight, 10 six bytes (the fifth and sixth of the default, then its first four),
        /// 11 a raw double.
        /// </summary>
        public double BitDoubleDefault(double defaultValue)
        {
            int kind = Bits2();

            if (kind == 0)
            {
                return defaultValue;
            }

            if (kind == 3)
            {
                return RawDouble();
            }

            byte[] bytes = BitConverter.GetBytes(defaultValue);

            if (!BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }

            if (kind == 2)
            {
                bytes[4] = RawChar();
                bytes[5] = RawChar();
            }

            bytes[0] = RawChar();
            bytes[1] = RawChar();
            bytes[2] = RawChar();
            bytes[3] = RawChar();

            if (!BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }

            return BitConverter.ToDouble(bytes, 0);
        }

        /// <summary>
        /// MC: a modular char. Bytes of seven bits each, the low group first, the high bit of a byte saying another follows;
        /// in the last byte bit 0x40 is the sign where <paramref name="signed"/> is true.
        /// </summary>
        public long ModularChar(bool signed = true)
        {
            long value = 0;
            int shift = 0;

            for (int i = 0; i < 10; i++)
            {
                int b = RawChar();

                if ((b & 0x80) != 0)
                {
                    value |= (long)(b & 0x7F) << shift;
                    shift += 7;
                    continue;
                }

                if (signed)
                {
                    value |= (long)(b & 0x3F) << shift;
                    return (b & 0x40) != 0 ? -value : value;
                }

                return value | ((long)b << shift);
            }

            throw new FormatException("The DWG data has a modular char longer than ten bytes.");
        }

        /// <summary>
        /// MS: a modular short. Raw shorts of fifteen bits each, the low group first, the high bit of a short saying
        /// another follows.
        /// </summary>
        public long ModularShort()
        {
            long value = 0;
            int shift = 0;

            for (int i = 0; i < 4; i++)
            {
                int word = RawShort() & 0xFFFF;
                value |= (long)(word & 0x7FFF) << shift;

                if ((word & 0x8000) == 0)
                {
                    return value;
                }

                shift += 15;
            }

            throw new FormatException("The DWG data has a modular short longer than four shorts.");
        }

        /// <summary>
        /// H: a handle reference. Four bits of code, four bits giving the count of bytes, then the bytes, high byte first.
        /// Codes 2 to 5 (and 0, an object's own handle) give the handle itself; 6 and 8 the handle one after and one before
        /// <paramref name="reference"/>; 0xA and 0xC <paramref name="reference"/> plus and minus the bytes.
        /// </summary>
        public ulong Handle(ulong reference = 0) => Handle(reference, out _);

        /// <summary>As <see cref="Handle(ulong)"/>, also giving the code of the reference.</summary>
        public ulong Handle(ulong reference, out int code)
        {
            code = (int)Bits(4);
            int count = (int)Bits(4);

            if (count > 8)
            {
                throw new FormatException("The DWG data has a handle of " + count + " bytes; a handle has at most 8.");
            }

            ulong value = 0;

            for (int i = 0; i < count; i++)
            {
                value = (value << 8) | RawChar();
            }

            switch (code)
            {
                case 6: return reference + 1;
                case 8: return reference - 1;
                case 0xA: return reference + value;
                case 0xC: return reference - value;
                default: return value;
            }
        }

        /// <summary>
        /// TV before R2007: a bit short giving the count of bytes, then the bytes in the drawing's code page. The count
        /// takes in the zero that ends the text; the text is returned up to it.
        /// </summary>
        public byte[] Text()
        {
            int count = BitShort();

            if (count <= 0)
            {
                return new byte[0];
            }

            byte[] text = Bytes(count);
            int end = Array.IndexOf(text, (byte)0);

            if (end < 0)
            {
                return text;
            }

            var cut = new byte[end];
            Array.Copy(text, cut, end);
            return cut;
        }

        /// <summary>BT from R2000: a bit, set for a thickness of 0, else a bit double follows.</summary>
        public double BitThickness() => Bit() ? 0.0 : BitDouble();

        /// <summary>BE from R2000: a bit, set for the extrusion (0, 0, 1), else three bit doubles follow.</summary>
        public double[] BitExtrusion() => Bit() ? new[] { 0.0, 0.0, 1.0 } : Point3();

        /// <summary>3BD: three bit doubles.</summary>
        public double[] Point3()
        {
            double x = BitDouble();
            double y = BitDouble();
            return new[] { x, y, BitDouble() };
        }

        /// <summary>2RD: two raw doubles.</summary>
        public double[] RawPoint2()
        {
            double x = RawDouble();
            return new[] { x, RawDouble() };
        }

        /// <summary>3RD: three raw doubles.</summary>
        public double[] RawPoint3()
        {
            double x = RawDouble();
            double y = RawDouble();
            return new[] { x, y, RawDouble() };
        }

        private static FormatException Ended() => new FormatException("The DWG data ends before the value being read.");
    }
}
