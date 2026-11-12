using System;
using System.Globalization;
using System.Text;
using GeometryHelper.Export;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// The text a table's cell opens as in AutoCAD 2021's editor, <c>GetTextString(FormatOption.ForEditing)</c>, from the
    /// number the cell holds and the units of its format (D40): the number as it is, to 16 figures with the zeros left off,
    /// where the cell shows it to the places of the drawing. Every function is exact in decimal digits, so that .NET
    /// Framework, which writes 15 figures of a double, and .NET Core give the same words.
    /// </summary>
    internal static class DxfTableEditingText
    {
        // The parts of an inch a length is read to, AutoCAD's 15 bits: 1/32768.
        private const long Parts = 32768;

        // The figures of a number AutoCAD 2021 opens, and the places of decimals it stops at.
        private const int Figures = 16;

        private const int MostPlaces = 15;

        /// <summary>
        /// A number in decimals: 16 figures, 15 places at most, rounded half away from nought, the zeros after the point left
        /// off, 1/3 as 0.333333333333333, 123456789.12345679 as 123456789.1234568, 0.1 + 0.2 as 0.3 and 1E-16 as 0. A number of
        /// more than 16 figures before the point is in powers of ten, as <see cref="Scientific(double)"/>.
        /// </summary>
        internal static string Decimal(double value)
        {
            if (value == 0.0 || !Guard.IsFinite(value))
            {
                return value == 0.0 ? "0" : Numbers.Exact(value);
            }

            Split(Math.Abs(value), out string digits, out int point);

            if (point > Figures)
            {
                return Scientific(value);
            }

            int places = Math.Min(MostPlaces, Figures - Math.Max(point, 0));
            digits = Round(digits, ref point, point + places);
            return digits.Length == 0 ? "0" : (value < 0.0 ? "-" : string.Empty) + Place(digits, point);
        }

        /// <summary>A number in powers of ten, 16 figures: 1000 as 1.000000000000000E+03 and 0.0015 as 1.500000000000000E-03.</summary>
        internal static string Scientific(double value)
        {
            if (!Guard.IsFinite(value))
            {
                return Numbers.Exact(value);
            }

            string digits = string.Empty;
            int point = 1;

            if (value != 0.0)
            {
                Split(Math.Abs(value), out digits, out point);
                digits = Round(digits, ref point, Figures);
            }

            int exponent = digits.Length == 0 ? 0 : point - 1;
            string mantissa = (digits.Length == 0 ? "0" : digits).PadRight(Figures, '0');
            return (value < 0.0 && digits.Length != 0 ? "-" : string.Empty)
                + mantissa[0]
                + "."
                + mantissa.Substring(1)
                + "E"
                + (exponent < 0 ? "-" : "+")
                + Math.Abs(exponent).ToString("00", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// A number of inches in feet and inches to 1/32768 of an inch, the fraction reduced and what is nought left out:
        /// 12.5 as 1'-0 1/2", 12 as 1', 7 as 7", 0.5 as 1/2" and 0 as 0". A number of more feet than 32 bits hold is in powers of
        /// ten.
        /// </summary>
        internal static string Architectural(double value)
        {
            if (!Counted(value, 12 * Parts, out long count))
            {
                return Scientific(value);
            }

            long feet = count / (12 * Parts);
            long inches = count % (12 * Parts) / Parts;
            string fraction = Reduced(count % Parts);
            string sign = count != 0 && value < 0.0 ? "-" : string.Empty;

            if (feet == 0)
            {
                return sign + Mixed(inches, fraction) + "\"";
            }

            if (inches == 0 && fraction.Length == 0)
            {
                return sign + feet.ToString(CultureInfo.InvariantCulture) + "'";
            }

            return sign
                + feet.ToString(CultureInfo.InvariantCulture)
                + "'-"
                + (inches == 0 ? "0 " + fraction : Mixed(inches, fraction))
                + "\"";
        }

        /// <summary>
        /// A number of inches in feet and decimals of an inch: 75.25 as 6'-3.25", 12 as 1', 0.5 as 0.5" and 1500.5 as
        /// 125'-0.5".
        /// </summary>
        internal static string Engineering(double value)
        {
            double length = Math.Abs(value);
            double feet = Math.Floor(length / 12.0);

            if (!Guard.IsFinite(value) || feet > int.MaxValue)
            {
                return Scientific(value);
            }

            string inches = Decimal(length - 12.0 * feet);
            string sign = value < 0.0 && (feet != 0.0 || inches != "0") ? "-" : string.Empty;

            if (feet == 0.0)
            {
                return sign + inches + "\"";
            }

            string whole = feet.ToString("0", CultureInfo.InvariantCulture);
            return sign + whole + "'" + (inches == "0" ? string.Empty : "-" + inches + "\"");
        }

        /// <summary>A number in whole numbers and fractions to 1/32768: 12.5 as 12 1/2, 0.1 as 3277/32768, -0.5 as -1/2 and 100 as 100.</summary>
        internal static string Fractional(double value)
        {
            if (!Counted(value, Parts, out long count))
            {
                return Scientific(value);
            }

            string sign = count != 0 && value < 0.0 ? "-" : string.Empty;
            return sign + Mixed(count / Parts, Reduced(count % Parts));
        }

        /// <summary>A point's three coordinates in decimals, one comma and space between: 1.5, 2.25, -0.1.</summary>
        internal static string Point(double x, double y, double z) =>
            Decimal(x) + ", " + Decimal(y) + ", " + Decimal(z);

        /// <summary>The digits of a positive number without their zeros at either end, and where the point stands: 0.digits times ten to the point.</summary>
        private static void Split(double magnitude, out string digits, out int point)
        {
            string written = magnitude.ToString("R", CultureInfo.InvariantCulture);
            int exponent = 0;
            int e = written.IndexOfAny(new[] { 'E', 'e' });

            if (e >= 0)
            {
                exponent = int.Parse(
                    written.Substring(e + 1),
                    NumberStyles.AllowLeadingSign,
                    CultureInfo.InvariantCulture
                );
                written = written.Substring(0, e);
            }

            int dot = written.IndexOf('.');
            string before = dot < 0 ? written : written.Substring(0, dot);
            string all = before + (dot < 0 ? string.Empty : written.Substring(dot + 1));
            int lead = 0;

            while (lead < all.Length && all[lead] == '0')
            {
                lead++;
            }

            digits = all.Substring(lead).TrimEnd('0');
            point = before.Length + exponent - lead;
        }

        /// <summary>The digits kept to a count of them, the rest rounded half away from nought; the count may be nought or below, which keeps none or a one.</summary>
        private static string Round(string digits, ref int point, int keep)
        {
            if (keep >= digits.Length)
            {
                return digits;
            }

            if (keep < 0)
            {
                return string.Empty;
            }

            char[] kept = digits.Substring(0, keep).ToCharArray();

            if (digits[keep] < '5')
            {
                return new string(kept).TrimEnd('0');
            }

            int at = kept.Length - 1;

            while (at >= 0 && kept[at] == '9')
            {
                kept[at--] = '0';
            }

            if (at >= 0)
            {
                kept[at]++;
                return new string(kept).TrimEnd('0');
            }

            point++;
            return "1";
        }

        /// <summary>The digits laid out about the point: 0.0015, 1500 and 12.5.</summary>
        private static string Place(string digits, int point)
        {
            if (point <= 0)
            {
                return "0." + new string('0', -point) + digits;
            }

            return digits.Length <= point
                ? digits + new string('0', point - digits.Length)
                : digits.Substring(0, point) + "." + digits.Substring(point);
        }

        /// <summary>A length counted in the parts of an inch it is read to, half away from nought; false for one of more feet than 32 bits hold.</summary>
        private static bool Counted(double value, long perUnit, out long count)
        {
            double rounded = Math.Round(Math.Abs(value) * Parts, MidpointRounding.AwayFromZero);
            count = 0;

            if (!Guard.IsFinite(value) || rounded / perUnit > int.MaxValue)
            {
                return false;
            }

            count = (long)rounded;
            return true;
        }

        /// <summary>A count of parts of an inch as the fraction it is, reduced: 16384 of 32768 as 1/2; empty for none.</summary>
        private static string Reduced(long fraction)
        {
            long under = Parts;

            while (fraction != 0 && fraction % 2 == 0)
            {
                fraction /= 2;
                under /= 2;
            }

            return fraction == 0
                ? string.Empty
                : fraction.ToString(CultureInfo.InvariantCulture) + "/" + under.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>A whole number and a fraction, 2 1/2, either alone where the other is nought, and 0 for neither.</summary>
        private static string Mixed(long whole, string fraction)
        {
            var text = new StringBuilder();

            if (whole != 0 || fraction.Length == 0)
            {
                text.Append(whole.ToString(CultureInfo.InvariantCulture));
            }

            if (fraction.Length != 0)
            {
                text.Append(text.Length == 0 ? string.Empty : " ").Append(fraction);
            }

            return text.ToString();
        }
    }
}
