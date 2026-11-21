using System;
using System.Globalization;

namespace GeometryHelper.Cad.Lisp
{
    /// <summary>
    /// How a variant changes type, and how a number is written as the text of a variant: what <c>vlax-variant-change-type</c>
    /// and <c>vlax-make-variant</c> with a type do, and what <c>vlax-safearray-fill</c> does to each item. A value here is the
    /// CLR value a variant or a safearray keeps: an int, a double, a string, a bool, or null for an empty one.
    /// </summary>
    internal static class LispVariantConversion
    {
        private const int Empty = 0;
        private const int Null = 1;
        private const int Short = 2;
        private const int Long = 3;
        private const int Single = 4;
        private const int Double = 5;
        private const int String = 8;
        private const int Object = 9;
        private const int Boolean = 11;

        /// <summary>
        /// Writes a real as a variant shows it: at most 15 significant digits, and the exponent form from 1E15 up and
        /// below 1E-4, as Visual Basic writes a Double (AutoCAD 2021, a3: 1.5 is <c>1.5</c>, 1E20 is <c>1E+20</c>, 0.333333333
        /// keeps its nine digits; where the exponent starts is ours, not measured: 0.00001 is <c>1E-05</c>).
        /// </summary>
        /// <param name="value">The number.</param>
        /// <returns>The text.</returns>
        internal static string NumberText(double value) =>
            value == 0.0 ? "0" : value.ToString("G15", CultureInfo.InvariantCulture);

        /// <summary>Makes a variant of a scalar type from a value that is already of that type.</summary>
        /// <param name="type">The VARTYPE: 0, 1, 2, 3, 4, 5, 8, 9 or 11.</param>
        /// <param name="value">An int for 2 and 3, a double for 4 and 5, a string, a bool, a <see cref="LispVlaObject"/>; null for 0 and 1.</param>
        /// <returns>The variant.</returns>
        internal static LispVariant Scalar(int type, object value)
        {
            switch (type)
            {
                case Short:
                case Long:
                    return new LispVariant(type, value, ((int)value).ToString(CultureInfo.InvariantCulture));
                case Single:
                    return new LispVariant(
                        type,
                        value,
                        ((float)(double)value).ToString("G7", CultureInfo.InvariantCulture)
                    );
                case Double:
                    return new LispVariant(type, value, NumberText((double)value));
                case String:
                    return new LispVariant(type, value, (string)value);
                case Boolean:
                    return new LispVariant(type, value, (bool)value ? "-1" : "0");
                case Object:
                    return new LispVariant(type, value, "...");
                default:
                    return new LispVariant(type, null, "");
            }
        }

        /// <summary>
        /// Changes a variant to another type as <c>vlax-variant-change-type</c> does (AutoCAD 2021, a3): a real goes to an
        /// integer type rounded half to even (1.5 gives 2, 2.5 gives 2, 3.5 gives 4), a string is parsed, a number becomes its
        /// text, a non-zero number becomes true (-1). An array or an object only keeps its own type.
        /// </summary>
        /// <param name="variant">The variant.</param>
        /// <param name="type">The VARTYPE to go to: 2, 3, 4, 5, 8 or 11.</param>
        /// <param name="result">The variant of the new type.</param>
        /// <returns>False when the value does not go into the type: a string that is no number, an integer out of range, an
        /// array to a scalar, a type that is not one of the six.</returns>
        internal static bool TryChangeType(LispVariant variant, int type, out LispVariant result)
        {
            result = null;

            if (variant.Type == type)
            {
                result = variant;
                return true;
            }

            if (variant.Value is LispSafeArray || variant.Value is LispVlaObject || variant.Type >= 8192)
            {
                return false;
            }

            if (!TryConvert(variant.Value, type, out object converted))
            {
                return false;
            }

            result = Scalar(type, converted);
            return true;
        }

        /// <summary>
        /// Converts a scalar value to the CLR value a scalar type keeps: an int for 2 and 3 (2 within the range of a 16-bit
        /// integer, ours), a double for 4 (as a single) and 5, a string for 8, a bool for 11.
        /// </summary>
        /// <param name="value">An int, a double, a string, a bool, or null for an empty variant.</param>
        /// <param name="type">The VARTYPE: 2, 3, 4, 5, 8 or 11.</param>
        /// <param name="result">The converted value.</param>
        /// <returns>False when it does not go.</returns>
        internal static bool TryConvert(object value, int type, out object result)
        {
            result = null;

            switch (type)
            {
                case String:
                    result = TextOf(value);
                    return result != null;
                case Boolean:
                    if (value is string text && TryBoolean(text, out bool word))
                    {
                        result = word;
                        return true;
                    }

                    if (!TryNumber(value, out double flag))
                    {
                        return false;
                    }

                    result = flag != 0.0;
                    return true;
            }

            if (!TryNumber(value, out double number))
            {
                return false;
            }

            switch (type)
            {
                case Short:
                case Long:
                    double whole = Math.Round(number, MidpointRounding.ToEven);
                    double low = type == Short ? short.MinValue : int.MinValue;
                    double high = type == Short ? short.MaxValue : int.MaxValue;

                    if (double.IsNaN(whole) || whole < low || whole > high)
                    {
                        return false;
                    }

                    result = (int)whole;
                    return true;
                case Single:
                    double narrow = (float)number;

                    if (double.IsInfinity(narrow))
                    {
                        return false;
                    }

                    result = narrow;
                    return true;
                case Double:
                    result = number;
                    return true;
                default:
                    return false;
            }
        }

        private static string TextOf(object value)
        {
            switch (value)
            {
                case null:
                    return "";
                case int i:
                    return i.ToString(CultureInfo.InvariantCulture);
                case double d:
                    return NumberText(d);
                case string s:
                    return s;
                case bool b:
                    return b ? "-1" : "0";
                default:
                    return null;
            }
        }

        private static bool TryBoolean(string text, out bool value)
        {
            string trimmed = text.Trim();
            value = string.Equals(trimmed, "True", StringComparison.OrdinalIgnoreCase);
            return value || string.Equals(trimmed, "False", StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryNumber(object value, out double number)
        {
            switch (value)
            {
                case null:
                    number = 0.0;
                    return true;
                case int i:
                    number = i;
                    return true;
                case double d:
                    number = d;
                    return true;
                case bool b:
                    number = b ? -1.0 : 0.0;
                    return true;
                case string s:
                    return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out number)
                        && !double.IsNaN(number)
                        && !double.IsInfinity(number);
                default:
                    number = 0.0;
                    return false;
            }
        }

        /// <summary>
        /// <c>equal</c> of two variants: the same type and an equal value, and for an array the same elements (AutoCAD 2021, a3:
        /// two <c>(vlax-3d-point 1 2 3)</c> are equal, so are two <c>(vlax-make-variant 1)</c>).
        /// </summary>
        /// <param name="x">One variant.</param>
        /// <param name="y">The other.</param>
        /// <param name="fuzz">How far apart two numbers may be.</param>
        /// <returns>True when they are equal.</returns>
        internal static bool Equal(LispVariant x, LispVariant y, double fuzz) =>
            x.Type == y.Type && ItemsEqual(x.Value, y.Value, fuzz);

        /// <summary><c>equal</c> of two safearrays: the same element type, bounds and elements.</summary>
        /// <param name="x">One array.</param>
        /// <param name="y">The other.</param>
        /// <param name="fuzz">How far apart two numbers may be.</param>
        /// <returns>True when they are equal.</returns>
        internal static bool Equal(LispSafeArray x, LispSafeArray y, double fuzz)
        {
            if (ReferenceEquals(x, y))
            {
                return true;
            }

            if (x.ElementType != y.ElementType || x.Rank != y.Rank || x.Items.Length != y.Items.Length)
            {
                return false;
            }

            for (int d = 0; d < x.Rank; d++)
            {
                if (x.Lower[d] != y.Lower[d] || x.Upper[d] != y.Upper[d])
                {
                    return false;
                }
            }

            for (int i = 0; i < x.Items.Length; i++)
            {
                if (!ItemsEqual(x.Items[i], y.Items[i], fuzz))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool ItemsEqual(object x, object y, double fuzz)
        {
            if (x == null || y == null)
            {
                return x == null && y == null;
            }

            if (x is bool p)
            {
                return y is bool q && p == q;
            }

            return LispInterpreter.Equal(x, y, fuzz);
        }
    }
}
