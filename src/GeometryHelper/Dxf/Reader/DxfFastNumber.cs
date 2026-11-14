using System;
using System.Numerics;
using System.Text;
using GeometryHelper.Export;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// The numbers of a record read from its bytes, for the fast read of a LINE, ARC, CIRCLE, LWPOLYLINE, TEXT or MTEXT: the
    /// number <see cref="Numbers.TryReadLong"/> reads from the same text, to the bit, without the text made a string where it
    /// need not be; and the same read of a string, the first thing <see cref="Numbers.TryRead"/> does with it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A decimal of at most 19 digits is the digits times a power of ten. Where the digits are at most 2^53 and the power is
    /// within 1E22 one multiplication or division gives the number nearest it, since both are doubles exactly (Clinger's fast
    /// path). A number of 16 to 19 digits, as AutoCAD writes them (1234.5678901234567), or of a power beyond 1E22, takes the
    /// product of its digits and the first 128 bits of the power of ten, 192 bits worked out exactly. The power is cut off
    /// short of its true value by less than one in its last bit, so the true product lies between that product and the same
    /// plus the digits, and the number is the double that both ends round to (the nearest, an exact half going to the even
    /// one). A power of ten up to 1E38 is held whole by the 128 bits, its product is exact and a tie between two doubles,
    /// 9007199254740993, goes to the even one with no second end to the range. Where the two ends round to different doubles,
    /// which a tie of a negative power does and about one number in 10^22 else, and for any other text,
    /// <see cref="Numbers.TryReadLong"/> reads it.
    /// </para>
    /// </remarks>
    internal static class DxfFastNumber
    {
        // Every power of ten up to 1E22 is a double exactly.
        private static readonly double[] Tens =
        {
            1E0,
            1E1,
            1E2,
            1E3,
            1E4,
            1E5,
            1E6,
            1E7,
            1E8,
            1E9,
            1E10,
            1E11,
            1E12,
            1E13,
            1E14,
            1E15,
            1E16,
            1E17,
            1E18,
            1E19,
            1E20,
            1E21,
            1E22,
        };

        /// <summary>The smallest and largest power of ten the 128-bit table holds: 1E-100 to 1E60.</summary>
        private const int LowestPower = -100;

        private const int HighestPower = 60;

        // For each power of ten: its first 128 bits as two words (the top bit set, the rest cut off, not rounded) and the power
        // of two the top bit stands at, 10^q = (high:low) / 2^127 x 2^two.
        private static readonly ulong[] High;
        private static readonly ulong[] Low;
        private static readonly int[] Two;

        // Whether the 128 bits are the power of ten whole, as every power up to 1E38 is: the product with it is then exact.
        private static readonly bool[] Whole;

        static DxfFastNumber()
        {
            int count = HighestPower - LowestPower + 1;
            High = new ulong[count];
            Low = new ulong[count];
            Two = new int[count];
            Whole = new bool[count];

            for (int q = LowestPower; q <= HighestPower; q++)
            {
                BigInteger fixedPoint;
                int two;

                if (q >= 0)
                {
                    BigInteger power = BigInteger.Pow(10, q);
                    two = BitLength(power) - 1;
                    fixedPoint = two <= 127 ? power << (127 - two) : power >> (two - 127);
                }
                else
                {
                    // 1 / 10^-q lies below 2^-(bits of 10^-q - 1), at or above the power of two under it.
                    BigInteger down = BigInteger.Pow(10, -q);
                    int bits = BitLength(down - BigInteger.One);
                    two = -bits;
                    fixedPoint = (BigInteger.One << (127 + bits)) / down;
                }

                int at = q - LowestPower;
                High[at] = (ulong)(fixedPoint >> 64);
                Low[at] = (ulong)(fixedPoint & ulong.MaxValue);
                Two[at] = two;
                Whole[at] = q >= 0 && two <= 127;
            }
        }

        /// <summary>The number of bits of a positive number: the place of its top bit, counting from one.</summary>
        private static int BitLength(BigInteger value)
        {
            byte[] bytes = value.ToByteArray();
            int top = bytes.Length - 1;

            while (top > 0 && bytes[top] == 0)
            {
                top--;
            }

            int bits = top * 8;

            for (int b = bytes[top]; b != 0; b >>= 1)
            {
                bits++;
            }

            return bits;
        }

        /// <summary>The characters of a text a number is scanned from, whether it is held as bytes or as a string.</summary>
        private interface ISource
        {
            /// <summary>The character at an index, as its number.</summary>
            int At(int index);
        }

        private struct Bytes : ISource
        {
            private readonly byte[] _bytes;

            internal Bytes(byte[] bytes)
            {
                _bytes = bytes;
            }

            public int At(int index) => _bytes[index];
        }

        private struct Chars : ISource
        {
            private readonly string _text;

            internal Chars(string text)
            {
                _text = text;
            }

            public int At(int index) => _text[index];
        }

        /// <summary>
        /// Reads the real of the bytes from <paramref name="at"/> to <paramref name="end"/> as <see cref="Numbers.TryRead"/>
        /// reads the same text, to the bit; false where that does not read it (a word, NaN, an exponent past the largest
        /// double) or the text has a letter beyond ASCII, which the reader may read as another letter.
        /// </summary>
        internal static bool TryReal(byte[] b, int at, int end, out double value) =>
            Scan(new Bytes(b), at, end, out value) || Long(b, at, end, out value);

        /// <summary>
        /// Reads the real of a text from <paramref name="at"/> to <paramref name="end"/> as <see cref="Numbers.TryReadLong"/>
        /// reads it, to the bit, where it is sure to; false where it is not, whether the text is a number or not, which that
        /// reader then settles.
        /// </summary>
        internal static bool TryText(string text, int at, int end, out double value) =>
            Scan(new Chars(text), at, end, out value);

        /// <summary>
        /// The number of the characters from <paramref name="at"/> to <paramref name="end"/>, spaces and tabs round it left
        /// off, where it is sure of the double it is: a sign, up to 19 digits with at most one point and an exponent of at
        /// most three digits, a nought of any size; false for any other text, and for a number that
        /// <see cref="Compose"/> is not sure of.
        /// </summary>
        private static bool Scan<T>(T s, int at, int end, out double value)
            where T : struct, ISource
        {
            value = 0.0;

            while (at < end && (s.At(at) == ' ' || s.At(at) == '\t'))
            {
                at++;
            }

            while (end > at && (s.At(end - 1) == ' ' || s.At(end - 1) == '\t'))
            {
                end--;
            }

            bool negative = false;

            if (at < end && (s.At(at) == '-' || s.At(at) == '+'))
            {
                negative = s.At(at) == '-';
                at++;
            }

            ulong mantissa = 0;
            int digits = 0;
            int seen = 0;
            int fraction = 0;
            bool point = false;

            for (; at < end; at++)
            {
                int c = s.At(at);
                int d = c - '0';

                if (d >= 0 && d <= 9)
                {
                    seen++;

                    if (point)
                    {
                        fraction++;
                    }

                    if (digits == 0 && d == 0)
                    {
                        continue;
                    }

                    if (++digits > 19)
                    {
                        return false;
                    }

                    mantissa = mantissa * 10 + (ulong)d;
                }
                else if (c == '.' && !point)
                {
                    point = true;
                }
                else
                {
                    break;
                }
            }

            if (seen == 0)
            {
                return false;
            }

            int exponent = 0;

            if (at < end)
            {
                int e = s.At(at);

                if (e != 'e' && e != 'E')
                {
                    return false;
                }

                at++;
                bool down = false;

                if (at < end && (s.At(at) == '-' || s.At(at) == '+'))
                {
                    down = s.At(at) == '-';
                    at++;
                }

                if (at == end)
                {
                    return false;
                }

                for (int count = 0; at < end; at++)
                {
                    int d = s.At(at) - '0';

                    if (d < 0 || d > 9 || ++count > 3)
                    {
                        return false;
                    }

                    exponent = exponent * 10 + d;
                }

                exponent = down ? -exponent : exponent;
            }

            if (digits == 0)
            {
                value = negative ? -0.0 : 0.0;
                return true;
            }

            if (!Compose(mantissa, exponent - fraction, out double magnitude))
            {
                return false;
            }

            value = negative ? -magnitude : magnitude;
            return true;
        }

        /// <summary>
        /// The double nearest <paramref name="mantissa"/> times 10^<paramref name="ten"/>, the mantissa of at most 19 digits,
        /// where it is sure of it: by one multiplication or division (Clinger), else by the 128-bit power of ten.
        /// </summary>
        private static bool Compose(ulong mantissa, int ten, out double magnitude)
        {
            if (mantissa <= (1UL << 53) && ten >= -22 && ten <= 22)
            {
                double whole = mantissa;
                magnitude = ten >= 0 ? (double)(whole * Tens[ten]) : (double)(whole / Tens[-ten]);
                return true;
            }

            magnitude = 0.0;
            return ten >= LowestPower && ten <= HighestPower && Wide(mantissa, ten, out magnitude);
        }

        /// <summary>The text the reader reads, by its own number reader; false for a letter beyond ASCII and for what it cannot read.</summary>
        private static bool Long(byte[] b, int at, int end, out double value)
        {
            value = 0.0;

            for (int i = at; i < end; i++)
            {
                if (b[i] >= 0x80)
                {
                    return false;
                }
            }

            return Numbers.TryReadLong(Encoding.ASCII.GetString(b, at, end - at), out value);
        }

        /// <summary>
        /// The double nearest digits times 10^ten, a tie going to the even one, where the 128-bit approximation of the power
        /// settles it; false where it does not, or the number is not a normal double.
        /// </summary>
        private static bool Wide(ulong digits, int ten, out double magnitude)
        {
            magnitude = 0.0;
            int lead = LeadingZeros(digits);
            ulong w = digits << lead;
            int index = ten - LowestPower;

            // w x (high:low) is 192 bits, in three words; the power is cut off, so the true product is at least this much and
            // below it by w.
            ulong xLow = Multiply(w, High[index], out ulong xHigh);
            ulong yLow = Multiply(w, Low[index], out ulong yHigh);
            ulong p0 = yLow;
            ulong p1 = xLow + yHigh;
            ulong p2 = xHigh + (p1 < xLow ? 1UL : 0UL);

            int baseTwo = 11 + Two[index] - lead;

            Round(p2, p1, p0, baseTwo, out ulong low, out int lowTwo);

            // A power of ten the 128 bits hold whole makes the product exact, so that a tie between two doubles is a tie.
            if (!Whole[index])
            {
                ulong u0 = p0 + w;
                ulong carry = u0 < p0 ? 1UL : 0UL;
                ulong u1 = p1 + carry;
                carry = u1 < p1 ? 1UL : 0UL;
                ulong u2 = p2 + carry;

                if (u2 < p2)
                {
                    return false;
                }

                Round(u2, u1, u0, baseTwo, out ulong up, out int upTwo);

                if (low != up || lowTwo != upTwo)
                {
                    return false;
                }
            }

            // The biased exponent of the number: 52 + the power of two of its last bit + 1023, inside the normal doubles.
            int biased = lowTwo + 1075;

            if (biased < 1 || biased > 2046)
            {
                return false;
            }

            magnitude = BitConverter.Int64BitsToDouble(((long)biased << 52) | (long)(low & 0xFFFFFFFFFFFFFUL));
            return true;
        }

        /// <summary>
        /// The 53 bits a 192-bit number rounds to, the nearest and an exact half to the even one, with the power of two the
        /// last of them stands at: the bits of the top word below the 53 are the rest of the first word.
        /// </summary>
        private static void Round(ulong p2, ulong p1, ulong p0, int baseTwo, out ulong mantissa, out int two)
        {
            int top = (int)(p2 >> 63);
            int shift = 10 + top;
            mantissa = p2 >> shift;
            two = baseTwo + top;
            ulong half = 1UL << (shift - 1);
            bool roundBit = (p2 & half) != 0;
            bool rest = (p2 & (half - 1)) != 0 || p1 != 0 || p0 != 0;

            if (roundBit && (rest || (mantissa & 1) == 1))
            {
                mantissa++;

                if (mantissa == (1UL << 53))
                {
                    mantissa = 1UL << 52;
                    two++;
                }
            }
        }

        /// <summary>The number of noughts before the top bit of a word that is not nought.</summary>
        private static int LeadingZeros(ulong value)
        {
            int count = 0;

            if ((value >> 32) == 0)
            {
                count += 32;
                value <<= 32;
            }

            if ((value >> 48) == 0)
            {
                count += 16;
                value <<= 16;
            }

            if ((value >> 56) == 0)
            {
                count += 8;
                value <<= 8;
            }

            if ((value >> 60) == 0)
            {
                count += 4;
                value <<= 4;
            }

            if ((value >> 62) == 0)
            {
                count += 2;
                value <<= 2;
            }

            if ((value >> 63) == 0)
            {
                count++;
            }

            return count;
        }

        /// <summary>The product of two words as two: the low word returned, the high one in <paramref name="high"/>.</summary>
        private static ulong Multiply(ulong a, ulong b, out ulong high)
        {
            ulong aLow = (uint)a;
            ulong aHigh = a >> 32;
            ulong bLow = (uint)b;
            ulong bHigh = b >> 32;
            ulong ll = aLow * bLow;
            ulong lh = aLow * bHigh;
            ulong hl = aHigh * bLow;
            ulong middle = (ll >> 32) + (uint)lh + (uint)hl;
            high = aHigh * bHigh + (lh >> 32) + (hl >> 32) + (middle >> 32);
            return (middle << 32) | (uint)ll;
        }

        /// <summary>
        /// Reads a whole number of at most nine digits with a sign and spaces or tabs round it, which
        /// <c>int.TryParse</c> of the trimmed text reads the same; false for any other text.
        /// </summary>
        internal static bool TryWhole(byte[] b, int at, int end, out int value)
        {
            value = 0;

            while (at < end && (b[at] == (byte)' ' || b[at] == (byte)'\t'))
            {
                at++;
            }

            while (end > at && (b[end - 1] == (byte)' ' || b[end - 1] == (byte)'\t'))
            {
                end--;
            }

            bool negative = false;

            if (at < end && (b[at] == (byte)'-' || b[at] == (byte)'+'))
            {
                negative = b[at] == (byte)'-';
                at++;
            }

            if (at == end || end - at > 9)
            {
                return false;
            }

            for (; at < end; at++)
            {
                int d = b[at] - '0';

                if (d < 0 || d > 9)
                {
                    return false;
                }

                value = value * 10 + d;
            }

            value = negative ? -value : value;
            return true;
        }
    }
}
