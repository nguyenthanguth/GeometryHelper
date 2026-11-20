using System;
using System.Globalization;
using System.Numerics;

namespace GeometryHelper.Cad.Lisp
{
    public sealed partial class LispInterpreter
    {
        private void RegisterNumbers()
        {
            Define("+", 0, -1, a => Fold(a, 0, Add));
            Define("-", 0, -1, Subtract);
            Define("*", 0, -1, a => Fold(a, 0, Multiply));
            Define("/", 0, -1, Divide);
            Define("REM", 0, -1, a => Fold(a, 0, Remainder, single: Number));
            Define("1+", 1, 1, a => Add(Number(a[0]), 1));
            Define("1-", 1, 1, a => Subtract(Number(a[0]), 1));
            Define("ABS", 1, 1, a => Absolute(Number(a[0])));
            Define("MIN", 0, -1, a => Fold(a, 0, (x, y) => Compare(x, y) <= 0 ? Promote(x, y, x) : Promote(x, y, y)));
            Define("MAX", 0, -1, a => Fold(a, 0, (x, y) => Compare(x, y) >= 0 ? Promote(x, y, x) : Promote(x, y, y)));
            Define("FIX", 1, 1, a => Fix(Number(a[0])));
            Define("FLOAT", 1, 1, a => ToDouble(Number(a[0])));
            Define("GCD", 2, 2, a => Gcd(a[0], a[1]));
            Define("EXPT", 2, 2, a => Expt(Number(a[0]), Number(a[1])));
            Define("SQRT", 1, 1, a => Math.Sqrt(Domain(a[0], false)));
            Define("EXP", 1, 1, a => Math.Exp(ToDouble(Number(a[0]))));
            Define("LOG", 1, 1, a => Math.Log(Domain(a[0], true)));
            Define("SIN", 1, 1, a => Sine(ToDouble(Number(a[0]))));
            Define("COS", 1, 1, a => Cosine(ToDouble(Number(a[0]))));
            Define("ATAN", 1, 2, Atan);
            Define("LOGAND", 0, -1, a => Fold(a, 0, (x, y) => (int)x & (int)y, single: Whole));
            Define("LOGIOR", 0, -1, a => Fold(a, 0, (x, y) => (int)x | (int)y, single: Whole));
            Define("LSH", 0, 2, Lsh);
            Define("~", 1, 1, a => ~Bits(a[0]));
            Define("BOOLE", 2, -1, Boole);
            Define("=", 1, -1, a => Chain(a, (x, y) => EqualsNumbers(x, y)));
            Define("/=", 1, -1, a => Chain(a, (x, y) => !EqualsNumbers(x, y)));
            Define("<", 1, -1, a => Chain(a, (x, y) => Compare(x, y) < 0));
            Define(">", 1, -1, a => Chain(a, (x, y) => Compare(x, y) > 0));
            Define("<=", 1, -1, a => Chain(a, (x, y) => Compare(x, y) <= 0));
            Define(">=", 1, -1, a => Chain(a, (x, y) => Compare(x, y) >= 0));
            Define("MINUSP", 1, 1, a => Bool(Dbl(Number(a[0])) < 0.0));
            Define("ZEROP", 1, 1, a => Bool(Dbl(Number(a[0])) == 0.0));
            Define("NUMBERP", 1, 1, a => Bool(a[0] is int || a[0] is double));
        }

        private static object Fold(
            object[] arguments,
            object empty,
            Func<object, object, object> step,
            Func<object, object> single = null
        )
        {
            if (arguments.Length == 0)
            {
                return empty;
            }

            object result = (single ?? Number)(arguments[0]);

            for (int i = 1; i < arguments.Length; i++)
            {
                result = step(result, (single ?? Number)(arguments[i]));
            }

            return result;
        }

        private static object Number(object value) =>
            value is int || value is double ? value : throw TypeError("numberp", value, colon: true);

        private static object Whole(object value) =>
            value is int ? value : throw TypeError("fixnump", value, colon: true);

        // (boole operator i1 i2 ...): bit 1 of the operator keeps a bit set in both, 2 one set only in the first, 4 one set
        // only in the second, 8 one set in neither (AutoCAD 2021: (boole 1 12 10) 8, (boole 6 5 3 1) 7, (boole 2 12) 12).
        private static object Boole(object[] a)
        {
            int op = Int(a[0]);

            if (op < 0 || op > 15)
            {
                throw new LispException("boole arg1 < 0 or > 15: " + op.ToString(CultureInfo.InvariantCulture));
            }

            int result = Int(a[1]);

            for (int k = 2; k < a.Length; k++)
            {
                int b = Int(a[k]);
                result =
                    ((op & 1) != 0 ? result & b : 0)
                    | ((op & 2) != 0 ? result & ~b : 0)
                    | ((op & 4) != 0 ? ~result & b : 0)
                    | ((op & 8) != 0 ? ~result & ~b : 0);
            }

            return result;
        }

        private static int Bits(object value) => value is int i ? i : throw TypeError("INT", value);

        private static double ToDouble(object number) => number is int i ? i : (double)number;

        private static double Dbl(object number) => ToDouble(number);

        /// <summary>The result of mixing an int and a real is a real: the chosen one as the type of the pair says.</summary>
        private static object Promote(object x, object y, object chosen) =>
            x is int && y is int ? chosen : (object)ToDouble(chosen);

        private static object Add(object x, object y) =>
            x is int a && y is int b ? (object)unchecked(a + b) : ToDouble(x) + ToDouble(y);

        private static object Subtract(object x, object y) =>
            x is int a && y is int b ? (object)unchecked(a - b) : ToDouble(x) - ToDouble(y);

        private static object Multiply(object x, object y) =>
            x is int a && y is int b ? (object)unchecked(a * b) : ToDouble(x) * ToDouble(y);

        private static object Negate(object x) =>
            x is int a ? (a == int.MinValue ? (object)2147483648.0 : (object)(-a)) : (object)(-(double)x);

        private static object Absolute(object x) => x is int a ? (a < 0 ? Negate(x) : x) : (object)Math.Abs((double)x);

        private static object Subtract(object[] a)
        {
            if (a.Length == 1)
            {
                return Negate(Number(a[0]));
            }

            return Fold(a, 0, Subtract);
        }

        private static object Divide(object[] a)
        {
            if (a.Length == 1)
            {
                return Number(a[0]);
            }

            return Fold(a, 0, (x, y) =>
            {
                if (x is int p && y is int q)
                {
                    if (q == 0)
                    {
                        throw new LispException("divide by zero");
                    }

                    return q == -1 ? Negate(x) : (object)(p / q);
                }

                double divisor = ToDouble(y);

                if (divisor == 0.0)
                {
                    throw new LispException("divide by zero");
                }

                return ToDouble(x) / divisor;
            });
        }

        private static object Remainder(object x, object y)
        {
            if (x is int p && y is int q)
            {
                if (q == 0)
                {
                    throw new LispException("divide by zero");
                }

                return q == -1 ? 0 : p % q;
            }

            double divisor = ToDouble(y);

            if (divisor == 0.0)
            {
                throw new LispException("divide by zero");
            }

            return ToDouble(x) % divisor;
        }

        /// <summary>Compares two numbers by value, or two strings by ordinal; anything else is an error, as in AutoCAD.</summary>
        private static int Compare(object x, object y)
        {
            if ((x is int || x is double) && (y is int || y is double))
            {
                return ToDouble(x).CompareTo(ToDouble(y));
            }

            if (x is string s && y is string t)
            {
                return Math.Sign(string.CompareOrdinal(s, t));
            }

            throw new LispException("bad argument type for compare: " + Print(x, true) + " " + Print(y, true));
        }

        private object Chain(object[] a, Func<object, object, bool> test)
        {
            for (int i = 0; i + 1 < a.Length; i++)
            {
                if (!test(a[i], a[i + 1]))
                {
                    return null;
                }
            }

            return _true;
        }

        /// <summary><c>=</c>: numbers by value, int against real too, strings by value, anything else the same object.</summary>
        private static bool EqualsNumbers(object x, object y)
        {
            if ((x is int || x is double) && (y is int || y is double))
            {
                return ToDouble(x) == ToDouble(y);
            }

            return Eq(x, y);
        }

        /// <summary>A real is cut to an int when it fits in 32 bits and stays a real when it does not: <c>(fix 1e20)</c> is 1.0e+20.</summary>
        private static object Fix(object number)
        {
            if (number is int)
            {
                return number;
            }

            double truncated = Math.Truncate((double)number);

            if (truncated >= -2147483648.0 && truncated <= 2147483647.0)
            {
                return (int)truncated;
            }

            return truncated;
        }

        private static object Gcd(object x, object y)
        {
            int a = ToInt(x);
            int b = ToInt(y);

            if (a <= 0)
            {
                throw new LispException("improper argument: " + Inv(a));
            }

            if (b <= 0)
            {
                throw new LispException("improper argument: " + Inv(b));
            }

            while (b != 0)
            {
                int remainder = a % b;
                a = b;
                b = remainder;
            }

            return a;
        }

        private static int ToInt(object value) => (int)Whole(value);

        /// <summary>
        /// <c>(expt base power)</c>: two ints give an int that wraps at 32 bits (<c>(expt 2 31)</c> is -2147483648, and
        /// <c>(expt 2 -1)</c> is 0); a real anywhere gives a real. A zero base with a power of zero or less, and a negative
        /// base with a power that is not whole, are errors, with AutoCAD's words.
        /// </summary>
        private static object Expt(object x, object y)
        {
            if (ToDouble(x) == 0.0 && ToDouble(y) <= 0.0)
            {
                throw new LispException("function undefined for " + Print(x, true) + " in negative power");
            }

            if (x is int b && y is int e)
            {
                if (e < 0)
                {
                    return b == 1 ? 1
                        : b == -1 ? (e % 2 == 0 ? 1 : -1)
                        : 0;
                }

                int result = 1;
                int factor = b;

                for (int n = e; n > 0; n >>= 1)
                {
                    if ((n & 1) != 0)
                    {
                        result = unchecked(result * factor);
                    }

                    factor = unchecked(factor * factor);
                }

                return result;
            }

            double @base = ToDouble(x);
            double power = ToDouble(y);

            if (@base < 0.0 && power != Math.Floor(power))
            {
                throw new LispException("x**y is undefined when x is real < 0, and y is not a whole number");
            }

            return Math.Pow(@base, power);
        }

        /// <summary>The number a function such as <c>sqrt</c> or <c>log</c> is given, which must be above zero for <c>log</c> and not below it for <c>sqrt</c>.</summary>
        private static double Domain(object value, bool positive)
        {
            double number = ToDouble(Number(value));

            if (positive ? number <= 0.0 : number < 0.0)
            {
                throw new LispException("function undefined for argument: " + Print(value, true));
            }

            return number;
        }

        private static object Atan(object[] a)
        {
            double y = ToDouble(Number(a[0]));

            if (a.Length == 1)
            {
                return Math.Atan(y);
            }

            return Math.Atan2(y, ToDouble(Number(a[1])));
        }

        /// <summary>
        /// <c>(lsh n bits)</c> shifts left for a positive count and right for a negative one, filling with zeros, and takes
        /// the count modulo 32: <c>(lsh 1 32)</c> is 1, <c>(lsh -1 -1)</c> is 2147483647. With one argument it gives it back, with none 0.
        /// </summary>
        private static object Lsh(object[] a)
        {
            if (a.Length == 0)
            {
                return 0;
            }

            int value = Bits(a[0]);

            if (a.Length == 1)
            {
                return value;
            }

            int count = Bits(a[1]);

            return count >= 0 ? (int)((uint)value << (count & 31)) : (int)((uint)value >> (-count & 31));
        }

        // Within an eighth of a turn of nought the framework's own function is exact, and the other of the pair is not asked for.
        private static double Sine(double x)
        {
            if (Math.Abs(x) < Math.PI / 4.0)
            {
                return Math.Sin(x);
            }

            SineAndCosine(x, out double sine, out _);
            return sine;
        }

        private static double Cosine(double x)
        {
            if (Math.Abs(x) < Math.PI / 4.0)
            {
                return Math.Cos(x);
            }

            SineAndCosine(x, out _, out double cosine);
            return cosine;
        }

        /// <summary>
        /// Brings a large angle into 0 to 2 pi before <c>sin</c> or <c>cos</c> sees it. The framework's own functions give a
        /// wrong answer for a very large angle (<c>Math.Sin(1e20)</c> is 1e20 on .NET Framework), and AutoCAD gives the right
        /// one (<c>(sin 1e20)</c> is -0.645251), so the angle is reduced here with pi to as many bits as the angle needs.
        /// </summary>
        private static double Reduced(double x)
        {
            if (double.IsNaN(x) || double.IsInfinity(x) || Math.Abs(x) < 1048576.0)
            {
                return x;
            }

            long bits = BitConverter.DoubleToInt64Bits(Math.Abs(x));
            int exponent = (int)((bits >> 52) & 0x7FF) - 1075;
            var mantissa = new BigInteger((bits & 0xFFFFFFFFFFFFFL) | (1L << 52));
            int fraction = 128 + Math.Max(exponent, 0);
            BigInteger twoPi = TwoPi(fraction);
            BigInteger rest = (mantissa << (exponent + fraction)) % twoPi;
            double reduced = (double)(rest >> (fraction - 100)) * Math.Pow(2.0, -100);

            return x < 0 ? -reduced : reduced;
        }

        /// <summary>2 pi times 2 to the power of a number of bits, by Machin's formula.</summary>
        private static BigInteger TwoPi(int bits)
        {
            int guard = bits + 16;
            BigInteger pi = 16 * ArcTangentOfReciprocal(5, guard) - 4 * ArcTangentOfReciprocal(239, guard);

            return (2 * pi) >> 16;
        }

        private static BigInteger ArcTangentOfReciprocal(int divisor, int bits)
        {
            BigInteger term = (BigInteger.One << bits) / divisor;
            BigInteger sum = term;
            int square = divisor * divisor;
            bool subtract = true;

            for (int n = 3; !term.IsZero; n += 2)
            {
                term /= square;
                BigInteger part = term / n;
                sum = subtract ? sum - part : sum + part;
                subtract = !subtract;
            }

            return sum;
        }
    }
}
