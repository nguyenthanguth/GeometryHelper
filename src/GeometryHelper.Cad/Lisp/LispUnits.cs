using System;
using System.Globalization;
using System.Text;

namespace GeometryHelper.Cad.Lisp
{
    /// <summary>
    /// What AutoLISP's rtos, angtos, distof and angtof write and read, as AutoCAD 2021 gave each text in accoreconsole.
    /// Pure functions of the value, the mode, the precision and DIMZIN; the interpreter fetches the defaults from the
    /// drawing (LUNITS, LUPREC, AUNITS, AUPREC, DIMZIN). A real is rounded half away from zero on its decimal digits
    /// (1.005 to 2 places is 1.01, 2.5 to none is 3, -0.0004 to 3 places is 0.000), at most 16 significant digits are
    /// written, and a length of 1E+16 or more is written in scientific form in every mode (1E+16 in decimal is 1.00E+16).
    /// </summary>
    internal static class LispUnits
    {
        /// <summary>The most significant digits AutoCAD writes in decimal and in the angle forms.</summary>
        private const int SignificantDigits = 16;

        /// <summary>The most places after the point of a scientific mantissa.</summary>
        private const int ScientificPlaces = 15;

        /// <summary>The most places of seconds in degrees, minutes and seconds.</summary>
        private const int SecondPlaces = 12;

        /// <summary>The largest power of two a fraction is written over.</summary>
        private const int FractionPower = 40;

        /// <summary>A length from this is written in scientific form in every mode.</summary>
        private const double ScientificFrom = 1E16;

        /// <summary>
        /// Writes a length as AutoLISP's rtos: LUNITS 1 scientific, 2 decimal, 3 engineering (inches written as feet and
        /// decimal inches), 4 architectural (feet and inches with a fraction), 5 fractional. 15.5 in mode 2 to 2 places is
        /// "15.50", in mode 1 "1.55E+01", in mode 3 "1'-3.50\"", in mode 4 "1'-3 1/2\"" and in mode 5 "15 1/2".
        /// </summary>
        /// <param name="value">The length, in drawing units (inches where the mode has feet).</param>
        /// <param name="mode">1 to 5; any other throws <see cref="ArgumentOutOfRangeException"/> saying "rtos mode: 6".</param>
        /// <param name="precision">
        /// Places after the point in modes 1 to 3, the fraction over 2 to the precision in 4 and 5; negative throws
        /// <see cref="ArgumentOutOfRangeException"/> saying "rtos prec: -1".
        /// </param>
        /// <param name="dimzin">
        /// DIMZIN: bit 8 drops trailing zeros of a decimal (and its point), bit 4 the leading zero of 0.5; in modes 3 and 4
        /// the low two bits tell whether a zero feet (1 and 2) and a zero inches (1 and 3) are written.
        /// </param>
        /// <returns>The text.</returns>
        public static string Rtos(double value, int mode, int precision, int dimzin)
        {
            if (mode < 1 || mode > 5)
            {
                throw Refused("rtos mode: " + mode.ToString(CultureInfo.InvariantCulture));
            }

            if (precision < 0)
            {
                throw Refused("rtos prec: " + precision.ToString(CultureInfo.InvariantCulture));
            }

            // AutoCAD writes no number for these: a NaN is a zero, an infinity a zero mantissa to the 99th power.
            if (double.IsInfinity(value) && mode <= 2)
            {
                return Scientific(0.0, precision).Replace("E+00", "E+99");
            }

            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                value = 0.0;
            }

            bool negative = value < 0.0;
            double size = Math.Abs(value);

            if (mode == 1 || size >= ScientificFrom)
            {
                return Signed(negative, Scientific(size, precision));
            }

            switch (mode)
            {
                case 2:
                    return Signed(negative, Zeros(Fixed(size, precision), dimzin));
                case 3:
                    return Engineering(negative, size, precision, dimzin);
                default:
                    return Fractional(negative, size, precision, dimzin, mode == 4);
            }
        }

        /// <summary>
        /// Writes an angle as AutoLISP's angtos: AUNITS 0 decimal degrees, 1 degrees, minutes and seconds, 2 grads,
        /// 3 radians, 4 surveyor's units. The angle is taken from nought up to a whole turn first, so -1 radian is
        /// "302.70" degrees; half a radian is 28.6479 degrees, 28d38'52", 31.831g, 0.500r and N 61d21'8" E.
        /// </summary>
        /// <param name="radians">The angle, in radians counter-clockwise from east.</param>
        /// <param name="mode">0 to 4; any other throws <see cref="ArgumentOutOfRangeException"/> saying "angtos mode: 5".</param>
        /// <param name="precision">
        /// Places after the point in modes 0, 2 and 3; in 1 and 4 none gives degrees, 1 and 2 minutes, 3 and 4 whole
        /// seconds, and each place over 4 a place of seconds. A negative one throws
        /// <see cref="ArgumentOutOfRangeException"/> saying "angtos prec: -1".
        /// </param>
        /// <param name="dimzin">DIMZIN: the zero suppression of rtos, for the decimal forms (modes 0, 2 and 3).</param>
        /// <returns>The text.</returns>
        public static string Angtos(double radians, int mode, int precision, int dimzin)
        {
            if (mode < 0 || mode > 4)
            {
                throw Refused("angtos mode: " + mode.ToString(CultureInfo.InvariantCulture));
            }

            if (precision < 0)
            {
                throw Refused("angtos prec: " + precision.ToString(CultureInfo.InvariantCulture));
            }

            double turn = Turn(double.IsNaN(radians) || double.IsInfinity(radians) ? 0.0 : radians);
            double degrees = turn * 180.0 / Math.PI;

            switch (mode)
            {
                case 1:
                    return Degrees(degrees, precision);
                case 4:
                    return Surveyor(degrees, precision);
                case 2:
                    return Decimal(turn * 200.0 / Math.PI, 400.0, precision, dimzin) + "g";
                case 3:
                    return Decimal(turn, 2.0 * Math.PI, precision, dimzin) + "r";
                default:
                    return Decimal(degrees, 360.0, precision, dimzin);
            }
        }

        /// <summary>
        /// Reads a length as AutoLISP's distof: scientific and decimal as a number ("1.5", "1.5E+01", "1/2"), engineering
        /// and architectural also as feet and inches ("1'6\"", "1'-6 1/2\"", "18\""), and in every mode a fraction
        /// ("6 1/2", "6-1/2"). Feet and inches are not read in modes 1, 2 and 5: "18\"" and "1'" in mode 2 are nil.
        /// </summary>
        /// <param name="text">The text; a space around it is ignored.</param>
        /// <param name="mode">1 to 5; any other throws <see cref="ArgumentOutOfRangeException"/> saying "distof mode: 6".</param>
        /// <returns>The length, or null where the text does not read (nil).</returns>
        public static double? Distof(string text, int mode)
        {
            if (mode < 1 || mode > 5)
            {
                throw Refused("distof mode: " + mode.ToString(CultureInfo.InvariantCulture));
            }

            // CadUnits reads feet and inches in its units 3 and 4, and a fraction in 4 and 5.
            return CadUnits.TryLength(text, mode == 3 || mode == 4 ? 4 : 5, out double value) ? value : (double?)null;
        }

        /// <summary>
        /// Reads an angle as AutoLISP's angtof: a number in degrees, grads or radians as the mode says (45, 50g and 0.7854r
        /// carry their own mark), degrees, minutes and seconds ("45d30'15\""), or a bearing ("N45d0'E", "S45dW", "N"). The
        /// angle is taken from nought up to a whole turn: "-90" reads as 4.71239 radians. A bearing is at most 90 degrees
        /// from its north or south: "N100dE" is nil.
        /// </summary>
        /// <param name="text">The text.</param>
        /// <param name="mode">0 to 4; any other throws <see cref="ArgumentOutOfRangeException"/> saying "angtof mode: 5".</param>
        /// <returns>The angle in radians, or null where the text does not read (nil).</returns>
        public static double? Angtof(string text, int mode)
        {
            if (mode < 0 || mode > 4)
            {
                throw Refused("angtof mode: " + mode.ToString(CultureInfo.InvariantCulture));
            }

            if (!CadUnits.TryAngle(text, mode, out double radians))
            {
                return null;
            }

            string typed = text.Trim();

            if (typed.Length > 1 && "NnSs".IndexOf(typed[0]) >= 0 && "EeWw".IndexOf(typed[typed.Length - 1]) >= 0)
            {
                // CadUnits reads "N100dE" as -10 degrees; AutoCAD refuses a bearing past a quarter turn.
                bool north = char.ToUpperInvariant(typed[0]) == 'N';
                bool east = char.ToUpperInvariant(typed[typed.Length - 1]) == 'E';
                double from = north ? (east ? 0.0 : 90.0) : (east ? 270.0 : 180.0);
                double degrees = radians * 180.0 / Math.PI;

                if (degrees < from - 1E-9 || degrees > from + 90.0 + 1E-9)
                {
                    return null;
                }
            }

            return Turn(radians);
        }

        private static ArgumentOutOfRangeException Refused(string message) =>
            new ArgumentOutOfRangeException(null, message);

        /// <summary>An angle from nought up to a whole turn; negative zero and a rounding up to the turn are nought.</summary>
        private static double Turn(double radians)
        {
            double full = 2.0 * Math.PI;
            double turn = radians % full;

            if (turn < 0.0)
            {
                turn += full;
            }

            return turn >= full ? 0.0 : turn + 0.0;
        }

        private static string Signed(bool negative, string text) => negative && HasDigit(text) ? "-" + text : text;

        private static bool HasDigit(string text)
        {
            foreach (char c in text)
            {
                if (c >= '1' && c <= '9')
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// A positive number as its digits and where the point stands: the number is 0.DIGITS times 10 to the POINT, so
        /// 123.45 is "12345" and 3, and 0.0004 is "4" and -3. Zero is no digits and a point of 1. The digits are the
        /// shortest that read back as the same double, which is the decimal value AutoCAD rounds.
        /// </summary>
        private static void Decompose(double size, out string digits, out int point)
        {
            if (size == 0.0)
            {
                digits = string.Empty;
                point = 1;
                return;
            }

            string text = size.ToString("R", CultureInfo.InvariantCulture);
            int exponent = 0;
            int e = text.IndexOf('E');

            if (e >= 0)
            {
                exponent = int.Parse(
                    text.Substring(e + 1),
                    NumberStyles.AllowLeadingSign,
                    CultureInfo.InvariantCulture
                );
                text = text.Substring(0, e);
            }

            int dot = text.IndexOf('.');
            string all = dot < 0 ? text : text.Remove(dot, 1);
            int whole = dot < 0 ? text.Length : dot;
            int lead = 0;

            while (lead < all.Length && all[lead] == '0')
            {
                lead++;
            }

            digits = all.Substring(lead).TrimEnd('0');
            point = whole - lead + exponent;
        }

        /// <summary>
        /// A positive number to a number of places after the point, rounded half away from zero, to at most 16 significant
        /// digits (counted from the first one that is not zero): 1.5 to 20 places is 1.500000000000000, and 0.00001 to 20
        /// places is 0.00001000000000000000.
        /// </summary>
        private static string Fixed(double size, int places)
        {
            Decompose(size, out string digits, out int point);
            places = Math.Min(places, Math.Max(0, SignificantDigits - point));

            if (point < -places)
            {
                return places == 0 ? "0" : "0." + new string('0', places);
            }

            if (point < 0)
            {
                digits = new string('0', -point) + digits;
                point = 0;
            }

            int keep = point + places;
            char[] kept = (
                digits.Length >= keep ? digits.Substring(0, keep) : digits.PadRight(keep, '0')
            ).ToCharArray();
            string all = new string(kept);

            if (digits.Length > keep && digits[keep] >= '5')
            {
                int i = kept.Length - 1;

                while (i >= 0 && kept[i] == '9')
                {
                    kept[i] = '0';
                    i--;
                }

                if (i >= 0)
                {
                    kept[i]++;
                    all = new string(kept);
                }
                else
                {
                    all = "1" + new string(kept);
                    point++;
                }
            }

            string integer = point == 0 ? "0" : all.Substring(0, point);
            return places == 0 ? integer : integer + "." + all.Substring(point);
        }

        /// <summary>A positive number as a mantissa of 1 digit and PLACES after the point, then E, a sign and two digits.</summary>
        private static string Scientific(double size, int places)
        {
            places = Math.Min(places, ScientificPlaces);
            Decompose(size, out string digits, out int point);
            int exponent = 0;
            string mantissa = new string('0', places + 1);

            if (digits.Length > 0)
            {
                int keep = places + 1;
                char[] kept = (
                    digits.Length >= keep ? digits.Substring(0, keep) : digits.PadRight(keep, '0')
                ).ToCharArray();
                exponent = point - 1;

                if (digits.Length > keep && digits[keep] >= '5')
                {
                    int i = kept.Length - 1;

                    while (i >= 0 && kept[i] == '9')
                    {
                        kept[i] = '0';
                        i--;
                    }

                    if (i >= 0)
                    {
                        kept[i]++;
                    }
                    else
                    {
                        kept[0] = '1';
                        exponent++;
                    }
                }

                mantissa = new string(kept);
            }

            return mantissa.Substring(0, 1)
                + (places > 0 ? "." + mantissa.Substring(1) : string.Empty)
                + "E"
                + (exponent < 0 ? "-" : "+")
                + Math.Abs(exponent).ToString("00", CultureInfo.InvariantCulture);
        }

        /// <summary>DIMZIN on a decimal: bit 8 drops the trailing zeros and the point they leave, bit 4 the 0 of 0.5.</summary>
        private static string Zeros(string text, int dimzin)
        {
            if ((dimzin & 8) != 0 && text.IndexOf('.') >= 0)
            {
                text = text.TrimEnd('0').TrimEnd('.');
            }

            return (dimzin & 4) != 0 && text.StartsWith("0.", StringComparison.Ordinal) ? text.Substring(1) : text;
        }

        private static string Engineering(bool negative, double size, int places, int dimzin)
        {
            double feet = Math.Floor(size / 12.0);
            string inches = Fixed(size - feet * 12.0, places);

            // Inches that round up to a foot.
            if (double.Parse(inches, CultureInfo.InvariantCulture) >= 12.0)
            {
                feet++;
                inches = Fixed(0.0, places);
            }

            return FeetAndInches(negative, feet, Zeros(inches, dimzin), !HasDigit(inches), dimzin);
        }

        private static string Fractional(bool negative, double size, int power, int dimzin, bool feetAndInches)
        {
            long denominator = 1L << Math.Min(power, FractionPower);
            double whole = Math.Floor(size);
            double top = Math.Round((size - whole) * denominator, MidpointRounding.AwayFromZero);

            // A fraction that rounds up to the whole.
            if (top >= denominator)
            {
                whole++;
                top = 0.0;
            }

            long numerator = (long)top;

            while (numerator != 0 && numerator % 2 == 0)
            {
                numerator /= 2;
                denominator /= 2;
            }

            string fraction =
                numerator.ToString(CultureInfo.InvariantCulture)
                + "/"
                + denominator.ToString(CultureInfo.InvariantCulture);

            if (!feetAndInches)
            {
                return Signed(
                    negative,
                    numerator == 0 ? Whole(whole)
                        : whole == 0.0 ? fraction
                        : Whole(whole) + " " + fraction
                );
            }

            double feet = Math.Floor(whole / 12.0);
            double inches = whole - feet * 12.0;
            bool feetShown = feet != 0.0 || (dimzin & 3) == 1 || (dimzin & 3) == 2;
            string text =
                numerator == 0 ? Whole(inches)
                : inches == 0.0 && !feetShown ? fraction
                : Whole(inches) + " " + fraction;
            return FeetAndInches(negative, feet, text, inches == 0.0 && numerator == 0, dimzin);
        }

        /// <summary>
        /// Feet and inches as 1'-3.50": a zero feet is left out unless DIMZIN has its low bits 1 or 2, a zero inches unless
        /// they are 1 or 3, and a number with both left out is its inches (0.00").
        /// </summary>
        private static string FeetAndInches(bool negative, double feet, string inches, bool inchesZero, int dimzin)
        {
            int zeros = dimzin & 3;
            bool showFeet = feet != 0.0 || zeros == 1 || zeros == 2;
            bool showInches = !inchesZero || zeros == 1 || zeros == 3 || !showFeet;
            var text = new StringBuilder();

            if (negative && (feet != 0.0 || !inchesZero))
            {
                text.Append('-');
            }

            if (showFeet)
            {
                text.Append(Whole(feet)).Append('\'');
            }

            if (showFeet && showInches)
            {
                text.Append('-');
            }

            if (showInches)
            {
                text.Append(inches).Append('"');
            }

            return text.ToString();
        }

        private static string Whole(double value) => value.ToString("0", CultureInfo.InvariantCulture);

        /// <summary>
        /// An angle of a decimal mode, nought where it rounds up to the whole turn: 359.9951 degrees is 0.00, while 6.2831
        /// radians is 6.28r, as 6.28 is short of a turn.
        /// </summary>
        private static string Decimal(double angle, double turn, int places, int dimzin)
        {
            string text = Fixed(angle, places);
            return Zeros(double.Parse(text, CultureInfo.InvariantCulture) >= turn ? Fixed(0.0, places) : text, dimzin);
        }

        /// <summary>
        /// Degrees as 29d, 28d39' or 28d38'52.4031": counted in whole units of the last place, so that a carry from seconds
        /// to minutes to degrees is exact, and a turn that rounds up is nought.
        /// </summary>
        private static string Degrees(double degrees, int precision)
        {
            int decimals = Math.Min(Math.Max(precision - 4, 0), SecondPlaces);
            long scale = 1L;

            for (int i = 0; i < decimals; i++)
            {
                scale *= 10L;
            }

            long perDegree =
                precision == 0 ? 1L
                : precision <= 2 ? 60L
                : 3600L * scale;
            long total = (long)Math.Round(degrees * perDegree, MidpointRounding.AwayFromZero) % (360L * perDegree);

            if (precision == 0)
            {
                return total.ToString(CultureInfo.InvariantCulture) + "d";
            }

            if (precision <= 2)
            {
                return (total / 60L).ToString(CultureInfo.InvariantCulture)
                    + "d"
                    + (total % 60L).ToString(CultureInfo.InvariantCulture)
                    + "'";
            }

            long inDegree = 3600L * scale;
            long rest = total % inDegree;
            long seconds = rest % (60L * scale);
            string fraction =
                decimals > 0
                    ? "." + (seconds % scale).ToString(CultureInfo.InvariantCulture).PadLeft(decimals, '0')
                    : string.Empty;
            return (total / inDegree).ToString(CultureInfo.InvariantCulture)
                + "d"
                + (rest / (60L * scale)).ToString(CultureInfo.InvariantCulture)
                + "'"
                + (seconds / scale).ToString(CultureInfo.InvariantCulture)
                + fraction
                + "\"";
        }

        /// <summary>A bearing: N or S, degrees from it, E or W; or one letter for east, north, west or south.</summary>
        private static string Surveyor(double degrees, int precision)
        {
            double quarters = Math.Round(degrees / 90.0);

            if (Math.Abs(degrees - quarters * 90.0) < 1E-9)
            {
                return "ENWS"[(int)quarters % 4].ToString();
            }

            bool north = degrees < 180.0;
            bool east = degrees < 90.0 || degrees > 270.0;
            double from = north ? Math.Abs(90.0 - degrees) : Math.Abs(270.0 - degrees);
            return (north ? "N " : "S ") + Degrees(from, precision) + (east ? " E" : " W");
        }
    }
}
