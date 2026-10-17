using System;
using System.Globalization;
using System.Numerics;

namespace GeometryHelper.Export
{
    /// <summary>
    /// How the writers that keep their numbers exactly print a double: a text that reads back to that very double, on either
    /// runtime.
    /// </summary>
    internal static class Numbers
    {
        private static readonly BigInteger[] PowersOfTen = BuildPowersOfTen(700);

        // Every power of ten up to 1E22 is a double exactly.
        private static readonly double[] SmallPowersOfTen =
        {
            1E0, 1E1, 1E2, 1E3, 1E4, 1E5, 1E6, 1E7, 1E8, 1E9, 1E10, 1E11,
            1E12, 1E13, 1E14, 1E15, 1E16, 1E17, 1E18, 1E19, 1E20, 1E21, 1E22,
        };

        /// <summary>
        /// Prints a double as the shortest of its 15, 16 and 17 digit texts that reads back to exactly that double.
        /// </summary>
        /// <remarks>
        /// <para>
        /// .NET Framework's <c>"R"</c> is not exact: for a number in about one in 25 000 of the doubles a mesh gives, and one
        /// in 1 600 of doubles of any bits, it prints 15 or 16 digits where 16 or 17 are needed, and the number read back is
        /// another double (4.8721540113315704 prints as 4.87215401133157). Parsing the text back to see whether it fits does not
        /// help, as the Framework parser is not correctly rounded either, and its errors match those of its formatter.
        /// </para>
        /// <para>
        /// So the candidates, <c>"G15"</c> then <c>"G16"</c> and at last <c>"G17"</c>, which always fits, are judged here without
        /// a parser: a text fits when it rounds to the double, which is to say lies within half a step of it, a text exactly half
        /// a step away fitting when the double's last bit is even, as rounding ties to even. A text of at most 2^53 as digits
        /// and a power of ten within 1E22 is judged by one correctly rounded multiplication or division, which is exact and
        /// decides both ways (Clinger's fast path); any other, in integers.
        /// </para>
        /// <para>
        /// The text is exact on either runtime, and not always the same text on both: the Framework's digits are not always
        /// correctly rounded, so the text it reaches can differ from .NET 10's, and be a digit longer, where both read back to
        /// the same double. A negative nought is <c>-0</c> on both. Infinities and NaN are printed as <c>"R"</c> prints them.
        /// </para>
        /// </remarks>
        internal static string Exact(double value)
        {
            long bits = BitConverter.DoubleToInt64Bits(value);

            if (value == 0.0)
            {
                return bits < 0 ? "-0" : "0";
            }

            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                return value.ToString("R", CultureInfo.InvariantCulture);
            }

            // A subnormal has fewer digits than fifteen to tell it from its neighbours, and the shortest text that fits is wanted.
            if (Math.Abs(value) < 2.2250738585072014E-308)
            {
                for (int digits = 1; digits < 15; digits++)
                {
                    string shorter = value.ToString("G" + digits.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);

                    if (Fits(shorter, bits))
                    {
                        return shorter;
                    }
                }
            }

            string fifteen = value.ToString("G15", CultureInfo.InvariantCulture);

            // A whole number of fifteen digits or fewer is its own text.
            if (Math.Abs(value) < 1E15 && value == Math.Floor(value))
            {
                return fifteen;
            }

            if (FitsQuickly(fifteen, value, bits))
            {
                return fifteen;
            }

            string sixteen = value.ToString("G16", CultureInfo.InvariantCulture);

            // The same digits fail again.
            if (sixteen != fifteen && FitsQuickly(sixteen, value, bits))
            {
                return sixteen;
            }

            return value.ToString("G17", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Whether a decimal text, of at most 17 digits, is nearer the double of the given bits than to any other, a tie going
        /// to the double with an even last bit: the number read from the text is that double. Judged in integers.
        /// </summary>
        internal static bool Fits(string text, long bits)
        {
            Split(text, out ulong digits, out int ten);
            return FitsInIntegers(digits, ten, bits);
        }

        /// <summary>
        /// The same as <see cref="Fits(string, long)"/>, by Clinger's fast path where it applies: digits that are a double
        /// exactly and a power of ten that is one, so that one multiplication or division, rounded once, is the correctly
        /// rounded number and settles the matter. This needs arithmetic rounded to the 53 bits of a double, which x64 has and
        /// 32-bit x87 has under its default precision; the result is cast to a double all the same.
        /// </summary>
        private static bool FitsQuickly(string text, double value, long bits)
        {
            Split(text, out ulong digits, out int ten);

            if (digits <= (1UL << 53) && ten >= -22 && ten <= 22)
            {
                double whole = digits;
                double read = ten >= 0 ? (double)(whole * SmallPowersOfTen[ten]) : (double)(whole / SmallPowersOfTen[-ten]);

                return read == Math.Abs(value);
            }

            return FitsInIntegers(digits, ten, bits);
        }

        /// <summary>The text as whole digits and a power of ten, its sign left off.</summary>
        private static void Split(string text, out ulong digits, out int ten)
        {
            digits = 0;

            int fraction = 0;
            int exponent = 0;
            bool afterPoint = false;

            for (int i = text[0] == '-' ? 1 : 0; i < text.Length; i++)
            {
                char c = text[i];

                if (c == '.')
                {
                    afterPoint = true;
                }
                else if (c == 'E' || c == 'e')
                {
                    exponent = int.Parse(text.Substring(i + 1), CultureInfo.InvariantCulture);
                    break;
                }
                else
                {
                    digits = digits * 10 + (ulong)(c - '0');

                    if (afterPoint)
                    {
                        fraction++;
                    }
                }
            }

            ten = exponent - fraction;
        }

        private static bool FitsInIntegers(ulong digits, int ten, long bits)
        {
            // The double is m x 2^e, its step 2^e above it and, but just under a power of two, 2^(e-1) below it.
            long fractionBits = bits & 0xFFFFFFFFFFFFFL;
            int biased = (int)((bits >> 52) & 0x7FF);
            long mantissa = biased == 0 ? fractionBits : fractionBits | (1L << 52);
            int two = biased == 0 ? -1074 : biased - 1075;
            bool halvedBelow = fractionBits == 0 && biased > 1;

            // Everything scaled to whole numbers: by 10^up so that the text's exponent is not negative, by 2^left so that the
            // double's and its half-steps' are not.
            int up = ten < 0 ? -ten : 0;
            int left = two < 2 ? 2 - two : 0;

            BigInteger text10 = (new BigInteger(digits) * PowersOfTen[ten + up]) << left;
            BigInteger unit = PowersOfTen[up];
            BigInteger value = (new BigInteger(mantissa) << (two + left)) * unit;
            BigInteger halfAbove = unit << (two - 1 + left);
            BigInteger halfBelow = halvedBelow ? unit << (two - 2 + left) : halfAbove;

            BigInteger gap = text10 - value;
            int order = gap.Sign >= 0 ? gap.CompareTo(halfAbove) : BigInteger.Negate(gap).CompareTo(halfBelow);

            return order < 0 || (order == 0 && (mantissa & 1) == 0);
        }

        private static BigInteger[] BuildPowersOfTen(int count)
        {
            var powers = new BigInteger[count];
            powers[0] = BigInteger.One;

            for (int i = 1; i < count; i++)
            {
                powers[i] = powers[i - 1] * 10;
            }

            return powers;
        }
    }
}
