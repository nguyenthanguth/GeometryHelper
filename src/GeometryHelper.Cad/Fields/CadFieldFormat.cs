using System;
using System.Globalization;
using System.Text;

namespace GeometryHelper.Cad
{
    /// <summary>
    /// The format of a field, the text of its <c>\f "..."</c>, as AutoCAD 2021 reads it (checked on its fields in the console):
    /// for a number <c>%lu</c> the units (1 scientific, 2 decimal, 3 engineering, 4 architectural, 5 fractional), <c>%pr</c> the
    /// precision, <c>%ps[prefix,suffix]</c> the words before and after, <c>%ct8[factor]</c> a factor the value is multiplied by,
    /// <c>%ds</c> and <c>%th</c> the decimal and thousands separators as the character's code, <c>%zs</c> the zeros left out (1
    /// leading, 2 feet, 4 inches, 8 trailing); for an angle <c>%au</c> the units (0 degrees, 1 degrees minutes seconds, 2 grads,
    /// 3 radians, 4 surveyor's); for words <c>%tc</c> the case (1 upper, 2 lower, 3 first capital, 4 each word capital) and for a
    /// file name <c>%fn</c> the parts (1 path, 2 name, 4 extension, 8 drive). A date is written by its letters: d dd ddd dddd,
    /// M MM MMM MMMM, yy yyyy, h hh H HH, m mm, s ss, t tt, any other letter as it stands. A 50 by 12 rectangle of 600 square units
    /// with <c>%lu2%pr2%ct8[1000]%th44</c> is 600,000.00.
    /// </summary>
    internal sealed class CadFieldFormat
    {
        private CadFieldFormat() { }

        /// <summary>The units of a number: 1 scientific, 2 decimal, 3 engineering, 4 architectural, 5 fractional.</summary>
        internal int Units { get; private set; } = 2;

        /// <summary>The precision; -1 where the format gives none.</summary>
        internal int Precision { get; private set; } = -1;

        internal string Prefix { get; private set; } = string.Empty;

        internal string Suffix { get; private set; } = string.Empty;

        /// <summary>The factor a value is multiplied by; 1 where the format gives none.</summary>
        internal double Factor { get; private set; } = 1.0;

        /// <summary>The decimal separator.</summary>
        internal char Decimal { get; private set; } = '.';

        /// <summary>The thousands separator; nought for none.</summary>
        internal char Thousands { get; private set; }

        /// <summary>The zeros left out: 1 leading, 2 feet, 4 inches, 8 trailing.</summary>
        internal int Zeros { get; private set; }

        /// <summary>The units of an angle: 0 degrees, 1 degrees minutes seconds, 2 grads, 3 radians, 4 surveyor's.</summary>
        internal int AngleUnits { get; private set; }

        /// <summary>The case of words: 0 as they are, 1 upper, 2 lower, 3 first capital, 4 each word capital.</summary>
        internal int Case { get; private set; }

        /// <summary>The parts of a file name shown: 1 path, 2 name, 4 extension, 8 drive; 15 where the format gives none.</summary>
        internal int FileParts { get; private set; } = 15;

        /// <summary>Whether the format sets how a number is written (<c>%lu</c>, <c>%pr</c>, <c>%au</c>, <c>%ct</c>, <c>%ps</c>, <c>%ds</c>, <c>%th</c> or <c>%zs</c>).</summary>
        internal bool IsNumeric { get; private set; }

        /// <summary>Whether the format sets the angle units.</summary>
        internal bool HasAngleUnits { get; private set; }

        /// <summary>Reads a format; an empty one gives the defaults.</summary>
        /// <param name="format">The text after <c>\f</c>, without its quotes.</param>
        /// <returns>The format.</returns>
        internal static CadFieldFormat Parse(string format)
        {
            var result = new CadFieldFormat();
            string text = format ?? string.Empty;
            int at = 0;

            while (at < text.Length)
            {
                if (text[at] != '%' || at + 2 >= text.Length)
                {
                    at++;
                    continue;
                }

                string key = text.Substring(at + 1, 2);
                int number = at + 3;
                int digits = number;

                while (digits < text.Length && char.IsDigit(text[digits]))
                {
                    digits++;
                }

                int value =
                    digits > number
                        ? int.Parse(text.Substring(number, digits - number), CultureInfo.InvariantCulture)
                        : -1;
                string bracket = null;

                if (digits < text.Length && text[digits] == '[')
                {
                    int close = text.IndexOf(']', digits);

                    if (close > digits)
                    {
                        bracket = text.Substring(digits + 1, close - digits - 1);
                        digits = close + 1;
                    }
                }

                result.Take(key, value, bracket);
                at = Math.Max(digits, at + 3);
            }

            return result;
        }

        private void Take(string key, int value, string bracket)
        {
            switch (key)
            {
                case "lu":
                    Units = value >= 1 && value <= 5 ? value : 2;
                    IsNumeric = true;
                    break;
                case "pr":
                    Precision = Math.Max(0, Math.Min(value, 8));
                    IsNumeric = true;
                    break;
                case "ps":
                    IsNumeric = true;

                    if (bracket != null)
                    {
                        int comma = bracket.IndexOf(',');
                        Prefix = comma < 0 ? bracket : bracket.Substring(0, comma);
                        Suffix = comma < 0 ? string.Empty : bracket.Substring(comma + 1);
                    }

                    break;
                case "ct":
                    IsNumeric = true;

                    if (
                        bracket != null
                        && double.TryParse(bracket, NumberStyles.Float, CultureInfo.InvariantCulture, out double factor)
                    )
                    {
                        Factor = factor;
                    }

                    break;
                case "ds":
                    IsNumeric = true;
                    Decimal = value > 0 ? (char)value : '.';
                    break;
                case "th":
                    IsNumeric = true;
                    Thousands =
                        value == 1 ? ','
                        : value > 1 ? (char)value
                        : '\0';
                    break;
                case "zs":
                    IsNumeric = true;
                    Zeros = Math.Max(0, value);
                    break;
                case "au":
                    IsNumeric = true;
                    HasAngleUnits = true;
                    AngleUnits = value >= 0 && value <= 4 ? value : 0;
                    break;
                case "tc":
                    Case = value >= 1 && value <= 4 ? value : 0;
                    break;
                case "fn":
                    FileParts = value > 0 ? value : 15;
                    break;
            }
        }

        /// <summary>
        /// Writes a number as the format says: 314.159265 with <c>%lu2%pr2</c> is 314.16, with <c>%lu1%pr3</c> 3.142E+02, with
        /// <c>%lu2%pr2%ps[A=,mm2]</c> A=314.16mm2; an angle in radians as its units say.
        /// </summary>
        /// <param name="value">The value; an angle in radians.</param>
        /// <param name="isAngle">Whether the value is an angle.</param>
        /// <returns>The words.</returns>
        internal string Number(double value, bool isAngle)
        {
            double scaled = value * Factor;
            string body;

            if (isAngle && HasAngleUnits)
            {
                body = Angle(scaled);
            }
            else
            {
                int precision = Precision < 0 ? 4 : Precision;

                switch (Units)
                {
                    case 1:
                        body = Scientific(scaled, precision);
                        break;
                    case 3:
                    case 4:
                        body = Feet(scaled, precision, Units == 4);
                        break;
                    case 5:
                        body = Fraction(scaled, precision);
                        break;
                    default:
                        body = Decimals(scaled, precision);
                        break;
                }
            }

            return Prefix + body + Suffix;
        }

        /// <summary>A number with its places, its zeros left out as <see cref="Zeros"/> says and its separators.</summary>
        private string Decimals(double value, int precision)
        {
            string text = Math.Abs(value)
                .ToString("F" + precision.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
            string whole = text;
            string places = string.Empty;
            int point = text.IndexOf('.');

            if (point >= 0)
            {
                whole = text.Substring(0, point);
                places = text.Substring(point + 1);
            }

            if ((Zeros & 8) != 0)
            {
                places = places.TrimEnd('0');
            }

            if (Thousands != '\0')
            {
                var grouped = new StringBuilder();

                for (int i = 0; i < whole.Length; i++)
                {
                    if (i > 0 && (whole.Length - i) % 3 == 0)
                    {
                        grouped.Append(Thousands);
                    }

                    grouped.Append(whole[i]);
                }

                whole = grouped.ToString();
            }

            if ((Zeros & 1) != 0 && whole == "0" && places.Length > 0)
            {
                whole = string.Empty;
            }

            bool negative =
                value < 0.0
                && (whole.Trim('0', Thousands == '\0' ? '0' : Thousands).Length > 0 || places.Trim('0').Length > 0);
            return (negative ? "-" : string.Empty) + whole + (places.Length > 0 ? Decimal + places : string.Empty);
        }

        private static string Scientific(double value, int precision)
        {
            string mantissa = precision > 0 ? "0." + new string('0', precision) : "0";
            return value.ToString(mantissa + "E+00", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// A length in inches as feet and inches: engineering <c>4'-2.000"</c>, architectural <c>4'-2 1/2"</c>, the inches to the
        /// precision places or the 2 to the precision part of an inch; the zero feet or zero inches left out where
        /// <see cref="Zeros"/> says.
        /// </summary>
        private string Feet(double inches, int precision, bool architectural)
        {
            bool negative = inches < 0.0;
            double length = Math.Abs(inches);
            long feet;
            string rest;
            bool zeroInches;

            if (architectural)
            {
                long denominator = 1L << Math.Min(precision, 8);
                long total = (long)Math.Round(length * denominator, MidpointRounding.AwayFromZero);
                long perFoot = 12 * denominator;
                feet = total / perFoot;
                long remainder = total - feet * perFoot;
                long whole = remainder / denominator;
                long numerator = remainder - whole * denominator;
                long divisor = Gcd(numerator, denominator);
                rest = whole.ToString(CultureInfo.InvariantCulture);

                if (numerator > 0)
                {
                    rest +=
                        " "
                        + (numerator / divisor).ToString(CultureInfo.InvariantCulture)
                        + "/"
                        + (denominator / divisor).ToString(CultureInfo.InvariantCulture);
                }

                zeroInches = remainder == 0;
            }
            else
            {
                double step = Math.Pow(10.0, -precision);
                double total = Math.Round(length / step, MidpointRounding.AwayFromZero) * step;
                feet = (long)Math.Floor(total / 12.0 + 1E-9);
                double remainder = total - feet * 12.0;
                rest = Decimals(remainder, precision).Replace("-", string.Empty);
                zeroInches = remainder < step / 2.0;
            }

            string text;

            if (feet == 0 && (Zeros & 2) != 0)
            {
                text = rest + "\"";
            }
            else if (zeroInches && (Zeros & 4) != 0)
            {
                text = feet.ToString(CultureInfo.InvariantCulture) + "'";
            }
            else
            {
                text = feet.ToString(CultureInfo.InvariantCulture) + "'-" + rest + "\"";
            }

            return (negative ? "-" : string.Empty) + text;
        }

        /// <summary>A number as a whole and a fraction of the 2 to the precision part: <c>314 1/4</c>.</summary>
        private static string Fraction(double value, int precision)
        {
            bool negative = value < 0.0;
            long denominator = 1L << Math.Min(precision, 8);
            long total = (long)Math.Round(Math.Abs(value) * denominator, MidpointRounding.AwayFromZero);
            long whole = total / denominator;
            long numerator = total - whole * denominator;
            long divisor = Gcd(numerator, denominator);
            string text = whole.ToString(CultureInfo.InvariantCulture);

            if (numerator > 0)
            {
                string part =
                    (numerator / divisor).ToString(CultureInfo.InvariantCulture)
                    + "/"
                    + (denominator / divisor).ToString(CultureInfo.InvariantCulture);
                text = whole == 0 ? part : text + " " + part;
            }

            return (negative ? "-" : string.Empty) + text;
        }

        private static long Gcd(long a, long b)
        {
            while (b != 0)
            {
                long t = a % b;
                a = b;
                b = t;
            }

            return a == 0 ? 1 : a;
        }

        /// <summary>An angle in radians in the units of <see cref="AngleUnits"/>: 0.9273 rad at two places is 53.13, 53d8', 59.03g, 0.93r or N 36d52' E.</summary>
        private string Angle(double radians)
        {
            int precision = Precision < 0 ? 4 : Precision;
            double degrees = radians * 180.0 / Math.PI;

            switch (AngleUnits)
            {
                case 1:
                    return Dms(degrees, precision, false);
                case 2:
                    return Decimals(degrees * 400.0 / 360.0, precision) + "g";
                case 3:
                    return Decimals(radians, precision) + "r";
                case 4:
                    return Surveyor(degrees, precision);
                default:
                    return Decimals(degrees, precision);
            }
        }

        /// <summary>Degrees as 53d, 53d8' or 53d7'48" by the precision (up to 1, to 3, to 4); places of seconds above.</summary>
        private static string Dms(double degrees, int precision, bool unsigned)
        {
            bool negative = degrees < 0.0 && !unsigned;
            double angle = Math.Abs(degrees);

            if (precision < 2)
            {
                return (negative ? "-" : string.Empty)
                    + Math.Round(angle, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.InvariantCulture)
                    + "d";
            }

            if (precision < 4)
            {
                long minutes = (long)Math.Round(angle * 60.0, MidpointRounding.AwayFromZero);
                return (negative ? "-" : string.Empty)
                    + (minutes / 60).ToString(CultureInfo.InvariantCulture)
                    + "d"
                    + (minutes % 60).ToString(CultureInfo.InvariantCulture)
                    + "'";
            }

            int places = precision - 4;
            double scale = Math.Pow(10.0, places);
            long units = (long)Math.Round(angle * 3600.0 * scale, MidpointRounding.AwayFromZero);
            long wholeSeconds = units / (long)scale;
            string seconds = (wholeSeconds % 60).ToString(CultureInfo.InvariantCulture);

            if (places > 0)
            {
                seconds += "." + (units % (long)scale).ToString(CultureInfo.InvariantCulture).PadLeft(places, '0');
            }

            return (negative ? "-" : string.Empty)
                + (wholeSeconds / 3600).ToString(CultureInfo.InvariantCulture)
                + "d"
                + ((wholeSeconds / 60) % 60).ToString(CultureInfo.InvariantCulture)
                + "'"
                + seconds
                + "\"";
        }

        /// <summary>A bearing: the angle from north or south towards east or west, <c>N 36d52' E</c> for 53.13 degrees from east.</summary>
        private static string Surveyor(double degrees, int precision)
        {
            double angle = degrees % 360.0;

            if (angle < 0.0)
            {
                angle += 360.0;
            }

            if (angle == 0.0)
            {
                return "E";
            }

            if (angle == 90.0)
            {
                return "N";
            }

            if (angle == 180.0)
            {
                return "W";
            }

            if (angle == 270.0)
            {
                return "S";
            }

            string north = angle < 180.0 ? "N" : "S";
            string side = angle > 90.0 && angle < 270.0 ? "W" : "E";
            double from =
                angle < 90.0 ? 90.0 - angle
                : angle < 180.0 ? angle - 90.0
                : angle < 270.0 ? 270.0 - angle
                : angle - 270.0;
            return north + " " + Dms(from, precision, true) + " " + side;
        }

        /// <summary>Words in the case the format says.</summary>
        /// <param name="text">The words.</param>
        /// <returns>The words.</returns>
        internal string Words(string text)
        {
            switch (Case)
            {
                case 1:
                    return text.ToUpperInvariant();
                case 2:
                    return text.ToLowerInvariant();
                case 3:
                    return text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text.Substring(1);
                case 4:
                    var each = new StringBuilder(text.Length);
                    bool start = true;

                    foreach (char c in text)
                    {
                        each.Append(start ? char.ToUpperInvariant(c) : c);
                        start = !char.IsLetterOrDigit(c);
                    }

                    return each.ToString();
                default:
                    return text;
            }
        }

        /// <summary>
        /// Writes a date by its letters (d dd ddd dddd, M MM MMM MMMM, yy yyyy, h hh H HH, m mm, s ss, t tt; the rest as it
        /// stands, a quote left out, as AutoCAD 2021 writes it): "M/d/yyyy h:mm tt" of 17 November 2026 at 3:21 is
        /// 11/17/2026 3:21 AM.
        /// </summary>
        /// <param name="date">The date.</param>
        /// <param name="format">The format; empty for the short date of the culture.</param>
        /// <param name="culture">The culture whose names and short date it takes.</param>
        /// <returns>The words.</returns>
        internal static string Date(DateTime date, string format, CultureInfo culture)
        {
            if (string.IsNullOrEmpty(format))
            {
                return date.ToString("d", culture);
            }

            var text = new StringBuilder();
            int at = 0;

            while (at < format.Length)
            {
                char c = format[at];
                int run = 1;

                while (at + run < format.Length && format[at + run] == c)
                {
                    run++;
                }

                switch (c)
                {
                    case 'd':
                        text.Append(
                            run == 1 ? date.Day.ToString(CultureInfo.InvariantCulture)
                            : run == 2 ? date.Day.ToString("00", CultureInfo.InvariantCulture)
                            : run == 3 ? culture.DateTimeFormat.GetAbbreviatedDayName(date.DayOfWeek)
                            : culture.DateTimeFormat.GetDayName(date.DayOfWeek)
                        );
                        break;
                    case 'M':
                        text.Append(
                            run == 1 ? date.Month.ToString(CultureInfo.InvariantCulture)
                            : run == 2 ? date.Month.ToString("00", CultureInfo.InvariantCulture)
                            : run == 3 ? culture.DateTimeFormat.GetAbbreviatedMonthName(date.Month)
                            : culture.DateTimeFormat.GetMonthName(date.Month)
                        );
                        break;
                    case 'y':
                        text.Append(
                            run <= 2
                                ? (date.Year % 100).ToString("00", CultureInfo.InvariantCulture)
                                : date.Year.ToString("0000", CultureInfo.InvariantCulture)
                        );
                        break;
                    case 'h':
                        int twelve = date.Hour % 12 == 0 ? 12 : date.Hour % 12;
                        text.Append(twelve.ToString(run == 1 ? "0" : "00", CultureInfo.InvariantCulture));
                        break;
                    case 'H':
                        text.Append(date.Hour.ToString(run == 1 ? "0" : "00", CultureInfo.InvariantCulture));
                        break;
                    case 'm':
                        text.Append(date.Minute.ToString(run == 1 ? "0" : "00", CultureInfo.InvariantCulture));
                        break;
                    case 's':
                        text.Append(date.Second.ToString(run == 1 ? "0" : "00", CultureInfo.InvariantCulture));
                        break;
                    case 't':
                        string marker =
                            date.Hour < 12 ? culture.DateTimeFormat.AMDesignator : culture.DateTimeFormat.PMDesignator;
                        text.Append(run == 1 ? (marker.Length > 0 ? marker.Substring(0, 1) : marker) : marker);
                        break;
                    case '\'':
                        break;
                    default:
                        text.Append(c, run);
                        break;
                }

                at += run;
            }

            return text.ToString();
        }
    }
}
