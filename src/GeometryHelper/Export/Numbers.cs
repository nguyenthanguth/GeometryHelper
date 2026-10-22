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

        /// <summary>
        /// The most significant digits a text is read by: past them a digit only decides which way a number half way between
        /// two doubles goes, which a digit 1 standing for all the rest that are not nought decides alike. A double's halves
        /// need at most 767 digits.
        /// </summary>
        private const int MostDigits = 800;

        /// <summary>
        /// Reads a finite number written in decimal, with a point and an exponent where it has them, as the double nearest
        /// it, a tie going to the double with an even last bit: the number a correctly rounding parser reads, the same on
        /// either runtime.
        /// </summary>
        /// <remarks>
        /// .NET Framework's parser is not correctly rounded, and lands a step away from the double now and then, so a text
        /// <see cref="Exact"/> printed would not always read back to its double there; no runtime parser is used. A text of
        /// at most 2^53 as digits and a power of ten within 1E22 is read by one correctly rounded multiplication or division
        /// (Clinger's fast path). Any other is guessed from its first 19 digits and its exponent, a few steps off at most, and
        /// the guess moved a step at a time, judged in integers, until the text rounds to it. Of a text of more than 800
        /// significant digits the rest count only as whether any is not nought. Spaces either side are allowed; a sign, digits
        /// with at most one point, and an exponent are read; anything else, NaN and the infinities among them, and a number too
        /// large for a double, is not a number.
        /// </remarks>
        internal static bool TryRead(string text, out double value)
        {
            value = 0.0;

            if (text == null)
            {
                return false;
            }

            int at = 0;
            int end = text.Length;

            while (at < end && char.IsWhiteSpace(text[at]))
            {
                at++;
            }

            while (end > at && char.IsWhiteSpace(text[end - 1]))
            {
                end--;
            }

            bool negative = false;

            if (at < end && (text[at] == '-' || text[at] == '+'))
            {
                negative = text[at] == '-';
                at++;
            }

            // The significant digits, those that lead them in nought left off, the first 800 of them kept; whether any of the
            // rest is not nought; how many there are in all; and how many stand after the point.
            var kept = new char[Math.Min(MostDigits, Math.Max(0, end - at))];
            int stored = 0;
            int count = 0;
            bool sticky = false;
            int seen = 0;
            long afterPoint = 0;
            bool point = false;

            for (; at < end; at++)
            {
                char c = text[at];

                if (c >= '0' && c <= '9')
                {
                    seen++;

                    if (point)
                    {
                        afterPoint++;
                    }

                    if (count == 0 && c == '0')
                    {
                        continue;
                    }

                    if (stored < MostDigits)
                    {
                        kept[stored++] = c;
                    }
                    else if (c != '0')
                    {
                        sticky = true;
                    }

                    count++;
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

            long exponent = 0;

            if (at < end)
            {
                if (text[at] != 'e' && text[at] != 'E')
                {
                    return false;
                }

                at++;
                bool down = false;

                if (at < end && (text[at] == '-' || text[at] == '+'))
                {
                    down = text[at] == '-';
                    at++;
                }

                if (at == end)
                {
                    return false;
                }

                for (; at < end; at++)
                {
                    char c = text[at];

                    if (c < '0' || c > '9')
                    {
                        return false;
                    }

                    // Past this any exponent is all the same: the number is nought or too large.
                    if (exponent < 1000000000L)
                    {
                        exponent = exponent * 10 + (c - '0');
                    }
                }

                exponent = down ? -exponent : exponent;
            }

            if (count == 0)
            {
                value = negative ? -0.0 : 0.0;
                return true;
            }

            // The number is the kept digits, a whole number, times 10^ten; noughts at the end are taken into the exponent, and
            // digits past those kept that are not nought stand as one digit 1 after them.
            long ten = exponent - afterPoint + (count - stored);

            if (!sticky)
            {
                while (stored > 1 && kept[stored - 1] == '0')
                {
                    stored--;
                    ten++;
                }
            }
            else
            {
                kept = Grown(kept, stored);
                kept[stored++] = '1';
                ten--;
            }

            // Its first digit stands at 10^(stored + ten - 1): beyond the largest double it is too large, below the smallest half a step nought.
            long order = stored + ten - 1;

            if (order > 309)
            {
                return false;
            }

            if (order < -325)
            {
                value = negative ? -0.0 : 0.0;
                return true;
            }

            int power = (int)ten;
            int lead = Math.Min(stored, 19);
            ulong first = 0;

            for (int i = 0; i < lead; i++)
            {
                first = first * 10 + (ulong)(kept[i] - '0');
            }

            double magnitude;

            if (stored <= 19 && first <= (1UL << 53) && power >= -22 && power <= 22)
            {
                double whole = first;
                magnitude = power >= 0 ? (double)(whole * SmallPowersOfTen[power]) : (double)(whole / SmallPowersOfTen[-power]);
            }
            else
            {
                BigInteger digits = stored <= 19 ? new BigInteger(first) : BigInteger.Parse(new string(kept, 0, stored), CultureInfo.InvariantCulture);

                if (!Nearest(digits, power, Guess(first, power + stored - lead), out magnitude))
                {
                    return false;
                }
            }

            if (double.IsInfinity(magnitude) || double.IsNaN(magnitude))
            {
                return false;
            }

            value = negative ? -magnitude : magnitude;
            return true;
        }

        private static char[] Grown(char[] kept, int stored)
        {
            if (stored < kept.Length)
            {
                return kept;
            }

            var grown = new char[stored + 1];
            Array.Copy(kept, grown, stored);
            return grown;
        }

        /// <summary>
        /// A double within a few steps of digits x 10^ten, of at most 19 digits, worked out in doubles: the digits are one
        /// rounding off, and each power of ten from <see cref="Math.Pow"/> one more, taken in halves so that neither runs past
        /// the largest or below the smallest double where the number itself does not.
        /// </summary>
        private static double Guess(ulong digits, int ten)
        {
            double value = digits;
            int half = ten / 2;
            return value * Math.Pow(10.0, half) * Math.Pow(10.0, ten - half);
        }

        /// <summary>
        /// The double nearest digits x 10^ten, from a guess a few steps off: the guess is moved a step at a time, up while the
        /// number lies above half a step up from it, down while below half a step down, a number on the half step going to
        /// the double whose last bit is even. False where 64 steps do not reach it, which a guess from <see cref="Guess"/>
        /// never needs.
        /// </summary>
        private static bool Nearest(BigInteger digits, int ten, double guess, out double value)
        {
            value = double.IsInfinity(guess) ? double.MaxValue : double.IsNaN(guess) ? 0.0 : guess;

            for (int step = 0; step < 64; step++)
            {
                long bits = BitConverter.DoubleToInt64Bits(value);
                long fractionBits = bits & 0xFFFFFFFFFFFFFL;
                int biased = (int)((bits >> 52) & 0x7FF);
                long mantissa = biased == 0 ? fractionBits : fractionBits | (1L << 52);
                int two = biased == 0 ? -1074 : biased - 1075;
                bool even = (mantissa & 1) == 0;

                // Half a step up is (2m + 1) x 2^(e - 1); half a step down the same but under a power of two, where the step below is half.
                int above = Compare(digits, ten, new BigInteger(2 * mantissa + 1), two - 1);

                if (above > 0 || (above == 0 && !even))
                {
                    if (biased == 0x7FE && fractionBits == 0xFFFFFFFFFFFFFL)
                    {
                        value = double.PositiveInfinity;
                        return true;
                    }

                    value = BitConverter.Int64BitsToDouble(bits + 1);
                    continue;
                }

                if (mantissa == 0)
                {
                    return true;
                }

                bool halvedBelow = fractionBits == 0 && biased > 1;
                int below = halvedBelow
                    ? Compare(digits, ten, new BigInteger(4 * mantissa - 1), two - 2)
                    : Compare(digits, ten, new BigInteger(2 * mantissa - 1), two - 1);

                if (below < 0 || (below == 0 && !even))
                {
                    value = BitConverter.Int64BitsToDouble(bits - 1);
                    continue;
                }

                return true;
            }

            return false;
        }

        /// <summary>
        /// Compares digits x 10^ten with whole x 2^two, in integers: below nought, nought or above as the first is less, the
        /// same or more.
        /// </summary>
        private static int Compare(BigInteger digits, int ten, BigInteger whole, int two)
        {
            BigInteger left = ten >= 0 ? digits * PowerOfTen(ten) : digits;
            BigInteger right = ten >= 0 ? whole : whole * PowerOfTen(-ten);

            if (two >= 0)
            {
                right <<= two;
            }
            else
            {
                left <<= -two;
            }

            return left.CompareTo(right);
        }

        private static BigInteger PowerOfTen(int power) => power < PowersOfTen.Length ? PowersOfTen[power] : BigInteger.Pow(10, power);

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
