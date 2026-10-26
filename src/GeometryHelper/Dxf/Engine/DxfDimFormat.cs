using System;
using System.Globalization;
using System.Text;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// The text of a dimension's measurement, as AutoCAD writes it from the variables of its style: a length by DIMLFAC,
    /// DIMRND, DIMLUNIT, DIMDEC, DIMZIN, DIMDSEP, DIMFRAC and DIMPOST, its alternate units after it in brackets where DIMALT is
    /// on; an angle by DIMAUNIT, DIMADEC, DIMAZIN, DIMDSEP and DIMPOST.
    /// </summary>
    /// <remarks>
    /// A number is rounded as a .NET decimal of its 15 significant figures, half away from nought, so that 2.675 to two places
    /// is 2.68 and the text is the same under .NET Framework and .NET: the double nearest 2.675 lies just below it, and is read
    /// as the 2.675 its 15 figures give.
    /// </remarks>
    internal static class DxfDimFormat
    {
        private const string Degree = "\u00B0";

        /// <summary>
        /// The text a dimension of the variables given writes for a measurement and a text of its own: the measurement for an
        /// empty or null text, none for a single space, else the text with its first <c>&lt;&gt;</c> made the measurement. An
        /// angle, in radians, is written by the angular variables; a length is multiplied by DIMLFAC (a negative one taken as its
        /// size in paper space and as 1 elsewhere, as AutoCAD takes it), rounded by DIMRND and written in DIMLUNIT, and where
        /// DIMALT is on its alternate measurement follows it in brackets, or, where the text holds <c>[]</c>, stands there in
        /// their place, as AutoCAD places the alternate measurement.
        /// </summary>
        /// <param name="dimensionText">The text of the dimension's own.</param>
        /// <param name="measurement">The measurement, finite.</param>
        /// <param name="angular">Whether it is an angle.</param>
        /// <param name="paperSpace">Whether the dimension is in a layout of paper space.</param>
        /// <param name="value">The value of a variable by its name as AutoCAD's .NET gives it, <c>Dimdec</c>.</param>
        internal static string Text(string dimensionText, double measurement, bool angular, bool paperSpace, Func<string, object> value)
        {
            if (dimensionText == " ")
            {
                return string.Empty;
            }

            string post = (string)value("Dimpost");
            char separator = (char)value("Dimdsep");
            string primary;
            string alternate = null;

            if (angular)
            {
                int places = (int)value("Dimadec");
                places = places < 0 ? (int)value("Dimdec") : places;
                primary = Post(Angle(measurement, (int)value("Dimaunit"), places, (int)value("Dimazin"), separator), post, "<>");
            }
            else
            {
                double factor = (double)value("Dimlfac");
                double length = measurement * (factor >= 0.0 ? factor : paperSpace ? -factor : 1.0);
                int fraction = (int)value("Dimfrac");
                double stack = (double)value("Dimtfac");
                primary = Length(Round(length, (double)value("Dimrnd")), (int)value("Dimlunit"), (int)value("Dimdec"), (int)value("Dimzin"), separator, fraction, stack);
                primary = Post(primary, post, "<>");

                if ((bool)value("Dimalt"))
                {
                    // DIMALTU: 1 scientific, 2 decimal, 3 engineering, 4 architectural and 5 fractional stacked as DIMFRAC stacks
                    // them (across where it stacks none), 6 and 7 the same not stacked, 8 the Windows desktop.
                    int alternateUnits = (int)value("Dimaltu");
                    int units = alternateUnits == 6 ? 4 : alternateUnits == 7 ? 5 : alternateUnits == 8 ? 6 : alternateUnits;
                    int alternateFraction = alternateUnits == 6 || alternateUnits == 7 ? 2 : fraction == 2 ? 0 : fraction;
                    double size = Round(length * (double)value("Dimaltf"), (double)value("Dimaltrnd"));
                    string text = Length(size, units, (int)value("Dimaltd"), (int)value("Dimaltz"), separator, alternateFraction, stack);
                    alternate = Post(text, (string)value("Dimapost"), "[]");
                }
            }

            bool placed = alternate != null && dimensionText != null && dimensionText.IndexOf("[]", StringComparison.Ordinal) >= 0;
            string measured = alternate == null || placed ? primary : primary + "[" + alternate + "]";

            if (string.IsNullOrEmpty(dimensionText))
            {
                return measured;
            }

            string written = First(dimensionText, "<>", measured);
            return placed ? First(written, "[]", alternate) : written;
        }

        /// <summary>A text with the first of a mark in it made another text; the text itself where it holds none.</summary>
        private static string First(string text, string mark, string by)
        {
            int at = text.IndexOf(mark, StringComparison.Ordinal);
            return at < 0 ? text : text.Substring(0, at) + by + text.Substring(at + mark.Length);
        }

        /// <summary>
        /// A length in units of DIMLUNIT: 1 scientific, 2 decimal, 3 engineering (feet and decimal inches), 4 architectural (feet
        /// and inches with a fraction), 5 fractional, 6 the Windows desktop's; any other as decimal. Places run from 0 to 8.
        /// </summary>
        internal static string Length(double value, int units, int places, int zin, char separator, int fraction, double stack)
        {
            places = Math.Max(0, Math.Min(8, places));
            double size = Math.Abs(value);
            string text;

            switch (units)
            {
                case 1:
                    text = Scientific(size, places, zin, separator);
                    break;
                case 3:
                    text = Engineering(size, places, zin, separator);
                    break;
                case 4:
                    text = Architectural(size, places, zin, fraction, stack);
                    break;
                case 5:
                    text = Fractional(size, places, fraction, stack);
                    break;
                case 6:
                    text = Desktop(size, places, zin);
                    break;
                default:
                    text = Decimal(size, places, zin, separator);
                    break;
            }

            return value < 0.0 && HasValue(text) ? "-" + text : text;
        }

        /// <summary>
        /// An angle, in radians, in units of DIMAUNIT: 0 decimal degrees, 1 degrees, minutes and seconds, 2 grads, 3 radians;
        /// any other as decimal degrees. DIMAZIN leaves out leading (1) and trailing (2) zeros.
        /// </summary>
        internal static string Angle(double radians, int units, int places, int azin, char separator)
        {
            places = Math.Max(0, Math.Min(8, places));
            int zin = ((azin & 1) != 0 ? 4 : 0) | ((azin & 2) != 0 ? 8 : 0);
            double size = Math.Abs(radians);
            string text;

            switch (units)
            {
                case 1:
                    text = Sexagesimal(size * 180.0 / Math.PI, places, zin, separator);
                    break;
                case 2:
                    text = Decimal(size * 200.0 / Math.PI, places, zin, separator) + "g";
                    break;
                case 3:
                    text = Decimal(size, places, zin, separator) + "r";
                    break;
                default:
                    text = Decimal(size * 180.0 / Math.PI, places, zin, separator) + Degree;
                    break;
            }

            return radians < 0.0 && HasValue(text) ? "-" + text : text;
        }

        /// <summary>A value rounded to the nearest whole multiple of a step, half away from nought; the value itself for a step of nought or less.</summary>
        internal static double Round(double value, double step)
        {
            if (!(step > 0.0) || !(Math.Abs(value / step) < 1E15) || !(Math.Abs(value) < 1E27) || !(step < 1E27))
            {
                return value;
            }

            decimal steps = Math.Round(Exact(value) / Exact(step), 0, MidpointRounding.AwayFromZero);
            return (double)(steps * Exact(step));
        }

        /// <summary>The text of a prefix and suffix about a measurement: the mark replaced by it where the text holds one, else the text after it.</summary>
        private static string Post(string measurement, string post, string mark)
        {
            if (string.IsNullOrEmpty(post))
            {
                return measurement;
            }

            int at = post.IndexOf(mark, StringComparison.Ordinal);
            return at < 0 ? measurement + post : post.Substring(0, at) + measurement + post.Substring(at + mark.Length);
        }

        /// <summary>A decimal of a number of 0 to 8 places, DIMZIN's 4 leaving out the nought before the point and its 8 the noughts after the last figure.</summary>
        private static string Decimal(double size, int places, int zin, char separator)
            => Zeros(Fixed(size, places), (zin & 4) != 0, (zin & 8) != 0).Replace('.', separator);

        /// <summary>A number as 1.5500E+01: one figure before the point, the places after it, the power of ten of two figures at least, 1.0E-20 and 1.0E+300 alike.</summary>
        private static string Scientific(double size, int places, int zin, char separator)
        {
            decimal mantissa;
            int power = 0;

            if (size != 0.0 && !(size >= 1E-12 && size < 1E27))
            {
                // Past what a decimal holds to 15 significant figures: the power of ten of the double, its mantissa from 1 to 10.
                power = (int)Math.Floor(Math.Log10(size));
                double scaled = size / Math.Pow(10.0, power);
                mantissa = Exact(Guard.IsFinite(scaled) && scaled > 0.0 ? scaled : 1.0);
            }
            else
            {
                mantissa = Exact(size);
            }

            while (mantissa >= 10m)
            {
                mantissa /= 10m;
                power++;
            }

            while (mantissa != 0m && mantissa < 1m)
            {
                mantissa *= 10m;
                power--;
            }

            mantissa = Math.Round(mantissa, places, MidpointRounding.AwayFromZero);

            if (mantissa >= 10m)
            {
                mantissa /= 10m;
                power++;
            }

            string figures = Zeros(mantissa.ToString("F" + places.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture), false, (zin & 8) != 0).Replace('.', separator);
            return figures + "E" + (power < 0 ? "-" : "+") + Math.Abs(power).ToString("00", CultureInfo.InvariantCulture);
        }

        /// <summary>Feet and decimal inches, 1'-3.50": the whole rounded to the places in inches first, so that 11.9999 inches to two places is 1'.</summary>
        private static string Engineering(double size, int places, int zin, char separator)
        {
            if (!(size < 1E27))
            {
                return Decimal(size, places, zin, separator) + "\"";
            }

            decimal inches = Math.Round(Exact(size), places, MidpointRounding.AwayFromZero);
            decimal feet = Math.Floor(inches / 12m);
            inches -= feet * 12m;
            string text = Zeros(inches.ToString("F" + places.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture), (zin & 4) != 0, (zin & 8) != 0).Replace('.', separator);
            return FeetAndInches(feet.ToString("0", CultureInfo.InvariantCulture), feet == 0m, inches == 0m, text, zin);
        }

        /// <summary>Feet and inches with a fraction of a 2 to the power of the places, 1'-3 1/2" not stacked: 1/256 at 8 places, whole inches at nought.</summary>
        private static string Architectural(double size, int places, int zin, int fraction, double stack)
        {
            long denominator = 1L << places;

            if (!(size * denominator < 9E15))
            {
                return Decimal(size, 0, 0, '.') + "\"";
            }

            long parts = (long)Math.Round(Exact(size) * denominator, 0, MidpointRounding.AwayFromZero);
            long foot = 12L * denominator;
            long feet = parts / foot;
            long rest = parts % foot;
            bool feetShown = feet != 0 || (zin & 3) == 1 || (zin & 3) == 2;
            string inches = Mixed(rest / denominator, rest % denominator, denominator, fraction, stack, feetShown);
            return FeetAndInches(feet.ToString(CultureInfo.InvariantCulture), feet == 0, rest == 0, inches, zin);
        }

        /// <summary>A whole number and a fraction of a 2 to the power of the places, 15 1/2 not stacked.</summary>
        private static string Fractional(double size, int places, int fraction, double stack)
        {
            long denominator = 1L << places;

            if (!(size * denominator < 9E15))
            {
                return Decimal(size, 0, 0, '.');
            }

            long parts = (long)Math.Round(Exact(size) * denominator, 0, MidpointRounding.AwayFromZero);
            return Mixed(parts / denominator, parts % denominator, denominator, fraction, stack, false);
        }

        /// <summary>
        /// A decimal as the Windows desktop writes one under the culture of the thread, its separator and groups of figures,
        /// 12 345,50 under one that writes them so; DIMZIN's 4 and 8 leave out the zeros as of a decimal.
        /// </summary>
        private static string Desktop(double size, int places, int zin)
        {
            NumberFormatInfo format = CultureInfo.CurrentCulture.NumberFormat;
            string text = Zeros(Fixed(size, places), (zin & 4) != 0, (zin & 8) != 0);
            int point = text.IndexOf('.');
            string whole = point < 0 ? text : text.Substring(0, point);
            string rest = point < 0 ? string.Empty : format.NumberDecimalSeparator + text.Substring(point + 1);
            int[] sizes = format.NumberGroupSizes;
            var grouped = new StringBuilder();
            int end = whole.Length;
            int index = 0;

            while (end > 0)
            {
                int group = sizes.Length == 0 ? 0 : sizes[Math.Min(index, sizes.Length - 1)];
                int start = group <= 0 ? 0 : Math.Max(0, end - group);
                grouped.Insert(0, whole.Substring(start, end - start));

                if (start > 0)
                {
                    grouped.Insert(0, format.NumberGroupSeparator);
                }

                end = start;
                index++;
            }

            return grouped + rest;
        }

        /// <summary>
        /// Degrees, minutes and seconds: whole degrees at nought places, minutes at one or two, seconds at three or four, and
        /// seconds of one to four places at five to eight, 45°30'15.5" at five; the whole rounded in the last unit shown first.
        /// </summary>
        private static string Sexagesimal(double degrees, int places, int zin, char separator)
        {
            if (!(degrees < 1E20))
            {
                return Decimal(degrees, 0, 0, separator) + Degree;
            }

            decimal whole = Exact(degrees);

            if (places == 0)
            {
                return Math.Round(whole, 0, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.InvariantCulture) + Degree;
            }

            if (places <= 2)
            {
                decimal minutes = Math.Round(whole * 60m, 0, MidpointRounding.AwayFromZero);
                decimal d = Math.Floor(minutes / 60m);
                return d.ToString("0", CultureInfo.InvariantCulture) + Degree + (minutes - d * 60m).ToString("0", CultureInfo.InvariantCulture) + "'";
            }

            int decimals = Math.Max(0, places - 4);
            decimal seconds = Math.Round(whole * 3600m, decimals, MidpointRounding.AwayFromZero);
            decimal deg = Math.Floor(seconds / 3600m);
            decimal min = Math.Floor((seconds - deg * 3600m) / 60m);
            decimal sec = seconds - deg * 3600m - min * 60m;
            string secs = sec.ToString("F" + decimals.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
            secs = decimals == 0 ? secs : Zeros(secs, false, (zin & 8) != 0).Replace('.', separator);
            return deg.ToString("0", CultureInfo.InvariantCulture) + Degree + min.ToString("0", CultureInfo.InvariantCulture) + "'" + secs + "\"";
        }

        /// <summary>
        /// Feet and inches as DIMZIN's two low bits keep them: 0 leaves out nought feet and nought inches, 1 shows both, 2 shows
        /// nought feet and leaves out nought inches, 3 shows nought inches and leaves out nought feet. Where both would be left
        /// out, the inches are shown.
        /// </summary>
        private static string FeetAndInches(string feet, bool noFeet, bool noInches, string inches, int zin)
        {
            int mode = zin & 3;
            bool feetShown = !noFeet || mode == 1 || mode == 2;
            bool inchesShown = !noInches || mode == 1 || mode == 3 || !feetShown;

            if (feetShown && inchesShown)
            {
                return feet + "'-" + inches + "\"";
            }

            return feetShown ? feet + "'" : inches + "\"";
        }

        /// <summary>
        /// A whole number and a fraction, reduced, as DIMFRAC writes the fraction: 0 stacked across and 1 stacked aslant in
        /// AutoCAD's codes of an MTEXT (<c>{\H0.7x;\S1/2;}</c>, <c>{\H0.7x;\S1#2;}</c>), its height DIMTFAC times the text's; 2
        /// not stacked, after a space. The whole is left out where it is nought, unless it is to be written.
        /// </summary>
        private static string Mixed(long whole, long numerator, long denominator, int fraction, double stack, bool wholeShown)
        {
            if (numerator == 0)
            {
                return whole.ToString(CultureInfo.InvariantCulture);
            }

            long divisor = Divisor(numerator, denominator);
            string top = (numerator / divisor).ToString(CultureInfo.InvariantCulture);
            string bottom = (denominator / divisor).ToString(CultureInfo.InvariantCulture);
            string part = fraction == 2 ? top + "/" + bottom
                : "{\\H" + stack.ToString("R", CultureInfo.InvariantCulture) + "x;\\S" + top + (fraction == 1 ? "#" : "/") + bottom + ";}";

            if (whole == 0 && !wholeShown)
            {
                return part;
            }

            return whole.ToString(CultureInfo.InvariantCulture) + (fraction == 2 ? " " : string.Empty) + part;
        }

        private static long Divisor(long a, long b)
        {
            while (b != 0)
            {
                long t = a % b;
                a = b;
                b = t;
            }

            return a;
        }

        /// <summary>A number of nought or more to the places, rounded half away from nought, with a point; as .NET writes it past what a decimal holds.</summary>
        private static string Fixed(double size, int places)
        {
            string format = "F" + places.ToString(CultureInfo.InvariantCulture);

            if (!(size < 7.9E27))
            {
                return size.ToString(format, CultureInfo.InvariantCulture);
            }

            return Math.Round(Exact(size), places, MidpointRounding.AwayFromZero).ToString(format, CultureInfo.InvariantCulture);
        }

        /// <summary>The .NET decimal of a double, its 15 significant figures.</summary>
        private static decimal Exact(double value) => (decimal)value;

        /// <summary>
        /// The noughts left out of a decimal written with a point, as ezdxf leaves them out: nought itself as 0 where either is
        /// left out; the leading noughts, so that 0.50 is .50; the trailing ones and the point after them, so that 12.50 is 12.5.
        /// </summary>
        private static string Zeros(string text, bool leading, bool trailing)
        {
            if (!leading && !trailing)
            {
                return text;
            }

            if (!HasValue(text))
            {
                return "0";
            }

            if (leading)
            {
                text = text.TrimStart('0');
            }

            if (trailing && text.IndexOf('.') >= 0)
            {
                text = text.TrimEnd('0');
            }

            return text.EndsWith(".", StringComparison.Ordinal) ? text.Substring(0, text.Length - 1) : text;
        }

        /// <summary>Whether a text of a number has a figure other than nought.</summary>
        private static bool HasValue(string text)
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
    }
}
