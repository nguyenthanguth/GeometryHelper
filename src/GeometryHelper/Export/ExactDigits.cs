using System;
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using GeometryHelper.Dwg;

namespace GeometryHelper.Export
{
    /// <summary>
    /// The digits of a double worked out exactly, in whole numbers, so that every runtime gives the same text. .NET Framework
    /// 4.8 prints a double of 16 or 17 digits from 15 or 17 digits that it rounded once already (0.002986441512055773 is
    /// 0.002986441512055774 there at 16 digits, in about one double in sixteen), and a number half way between two texts goes
    /// up where .NET Core 3.0 and later send it to the even digit (6.9212722778320312 is 6.9212722778320313 at 17); its parse
    /// is not exact either. The text here is what <c>ToString("G15")</c>, <c>"G16"</c> and <c>"G17"</c> give under .NET Core
    /// 3.0 and later, which is right, on every runtime.
    /// </summary>
    internal static class ExactDigits
    {
        /// <summary>The powers of ten to 10^19, which are numbers of 64 bits.</summary>
        private static readonly ulong[] Tens = MakeTens();

        /// <summary>The fives to the power of their index, up to the power 27 (5 to the 27 is 7 450 580 596 923 828 125).</summary>
        private static readonly ulong[] Fives = MakeFives();

        private static ulong[] MakeTens()
        {
            var tens = new ulong[20];
            tens[0] = 1;

            for (int i = 1; i < tens.Length; i++)
            {
                tens[i] = tens[i - 1] * 10;
            }

            return tens;
        }

        private static ulong[] MakeFives()
        {
            var fives = new ulong[28];
            fives[0] = 1;

            for (int i = 1; i < fives.Length; i++)
            {
                fives[i] = fives[i - 1] * 5;
            }

            return fives;
        }

        /// <summary>Five to the power of <paramref name="scale"/>, as <see cref="Cut"/> worked with it.</summary>
        internal static ulong Five(int scale) => Fives[scale];

        /// <summary>The product of two numbers of 64 bits as one of 128, its high and low halves.</summary>
        [MethodImpl(DwgJit.Hot)]
        private static void Multiply(ulong a, ulong b, out ulong high, out ulong low)
        {
            ulong aLow = a & 0xFFFFFFFF;
            ulong aHigh = a >> 32;
            ulong bLow = b & 0xFFFFFFFF;
            ulong bHigh = b >> 32;
            ulong lowLow = aLow * bLow;
            ulong lowHigh = aLow * bHigh;
            ulong highLow = aHigh * bLow;
            ulong middle = (lowLow >> 32) + (lowHigh & 0xFFFFFFFF) + (highLow & 0xFFFFFFFF);

            low = (lowLow & 0xFFFFFFFF) | (middle << 32);
            high = (aHigh * bHigh) + (lowHigh >> 32) + (highLow >> 32) + (middle >> 32);
        }

        /// <summary>
        /// The first 17 digits of a number, cut and not rounded, in whole-number arithmetic: the number times ten to the
        /// power of <paramref name="scale"/> is its mantissa times five to that power, over two to the power of the
        /// difference of the exponents, so <paramref name="floor"/>, a number of 17 digits, is that quotient and
        /// <paramref name="rest"/>, under two to the <paramref name="shift"/>, what is left. False where the number is
        /// subnormal, under 1E-11 or from 1E15 up in size, where the 128 bits this works in do not hold it: <see cref="Digits"/>
        /// then works in integers of any size.
        /// </summary>
        [MethodImpl(DwgJit.Hot)]
        internal static bool Cut(double size, out ulong floor, out ulong rest, out int shift, out int scale)
        {
            floor = 0;
            rest = 0;
            shift = 0;
            scale = 0;

            long bits = BitConverter.DoubleToInt64Bits(size);
            int biased = (int)(bits >> 52);

            if (biased == 0 || biased == 0x7FF)
            {
                return false;
            }

            ulong mantissa = (ulong)(bits & 0xFFFFFFFFFFFFFL) | (1UL << 52);

            // The number is mantissa over two to the power of power.
            int power = 1075 - biased;

            // The scale that makes 17 digits of it: from the first digit's place, 16 - floor(log10), tried once more either way.
            scale = 16 - (int)Math.Floor(Math.Log10(size));

            for (int attempt = 0; ; attempt++)
            {
                shift = power - scale;

                if (scale < 2 || scale >= Fives.Length || shift < 1 || shift > 62 || attempt == 3)
                {
                    return false;
                }

                Multiply(mantissa, Fives[scale], out ulong high, out ulong low);

                if ((high >> shift) != 0)
                {
                    return false;
                }

                floor = (high << (64 - shift)) | (low >> shift);
                rest = low & ((1UL << shift) - 1);

                if (floor < 10000000000000000UL)
                {
                    scale++;
                }
                else if (floor >= 100000000000000000UL)
                {
                    scale--;
                }
                else
                {
                    return true;
                }
            }
        }

        /// <summary>
        /// The first 17 digits of a finite number over nought, in size, cut and not rounded: <paramref name="floor"/>, of 17
        /// digits, over ten to the power of <paramref name="scale"/> is the number less what <paramref name="rest"/> says is
        /// left, which is 0 for nothing, 1 for less than half a unit of the last digit, 2 for half, 3 for more.
        /// </summary>
        internal static void Digits(double size, out ulong floor, out int scale, out int rest)
        {
            if (Cut(size, out floor, out ulong left, out int shift, out scale))
            {
                rest =
                    left == 0 ? 0
                    : left < (1UL << (shift - 1)) ? 1
                    : left == (1UL << (shift - 1)) ? 2
                    : 3;
                return;
            }

            long bits = BitConverter.DoubleToInt64Bits(size);
            int biased = (int)(bits >> 52) & 0x7FF;
            var mantissa = new BigInteger(bits & 0xFFFFFFFFFFFFFL);
            int two = biased == 0 ? -1074 : biased - 1075;

            if (biased != 0)
            {
                mantissa |= BigInteger.One << 52;
            }

            BigInteger low = BigInteger.Pow(10, 16);
            BigInteger high = low * 10;
            int ten = (int)Math.Floor(Math.Log10(size));

            while (true)
            {
                scale = 16 - ten;

                BigInteger top = mantissa;
                BigInteger bottom = BigInteger.One;

                if (two >= 0)
                {
                    top <<= two;
                }
                else
                {
                    bottom <<= -two;
                }

                if (scale >= 0)
                {
                    top *= BigInteger.Pow(10, scale);
                }
                else
                {
                    bottom *= BigInteger.Pow(10, -scale);
                }

                BigInteger quotient = BigInteger.DivRem(top, bottom, out BigInteger remainder);

                if (quotient < low)
                {
                    ten--;
                }
                else if (quotient >= high)
                {
                    ten++;
                }
                else
                {
                    int order = (remainder * 2).CompareTo(bottom);

                    floor = (ulong)quotient;
                    rest =
                        remainder.IsZero ? 0
                        : order < 0 ? 1
                        : order == 0 ? 2
                        : 3;
                    return;
                }
            }
        }

        /// <summary>
        /// A finite number other than nought as <c>ToString("G" + digits)</c> prints it under .NET Core 3.0 and later, the
        /// digits correctly rounded with a number half way between two going to the even one, the zeros at the end left
        /// off, in the form with an exponent (of at least two digits) from 10^digits up and under 1E-4.
        /// </summary>
        internal static string General(double value, int digits)
        {
            Digits(Math.Abs(value), out ulong floor, out int scale, out int rest);

            ulong unit = Tens[17 - digits];
            ulong head = floor / unit;
            ulong tail = floor - (head * unit);
            bool up =
                unit == 1
                    ? rest == 3 || (rest == 2 && (head & 1) == 1)
                    : tail > unit / 2 || (tail == unit / 2 && (rest != 0 || (head & 1) == 1));

            // The first digit's power of ten.
            int exponent = 16 - scale;

            if (up && ++head == Tens[digits])
            {
                head /= 10;
                exponent++;
            }

            int count = digits;

            while (count > 1 && head % 10 == 0)
            {
                head /= 10;
                count--;
            }

            var text = new StringBuilder(count + 8);
            var kept = new char[count];

            for (int i = count - 1; i >= 0; i--)
            {
                kept[i] = (char)('0' + (int)(head % 10));
                head /= 10;
            }

            if (value < 0)
            {
                text.Append('-');
            }

            // Where the point stands: after this many digits, a number that is not over 0 with the first digit.
            int point = exponent + 1;

            if (point > digits || point < -3)
            {
                text.Append(kept[0]);

                if (count > 1)
                {
                    text.Append('.').Append(kept, 1, count - 1);
                }

                int power = point - 1;

                text.Append('E').Append(power < 0 ? '-' : '+');

                if (Math.Abs(power) < 10)
                {
                    text.Append('0');
                }

                text.Append(Math.Abs(power).ToString(CultureInfo.InvariantCulture));
            }
            else if (point <= 0)
            {
                text.Append("0.").Append('0', -point).Append(kept);
            }
            else if (count <= point)
            {
                text.Append(kept).Append('0', point - count);
            }
            else
            {
                text.Append(kept, 0, point).Append('.').Append(kept, point, count - point);
            }

            return text.ToString();
        }
    }
}
