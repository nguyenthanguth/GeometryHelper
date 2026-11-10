using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// Lengths and angles written and read as AutoCAD writes and reads them in a drawing's units: LUNITS 1 scientific, 2
    /// decimal, 3 engineering, 4 architectural, 5 fractional, to LUPREC places (a denominator of 2 to the LUPREC in 4 and 5);
    /// AUNITS 0 decimal degrees, 1 degrees, minutes and seconds, 2 grads, 3 radians, 4 surveyor's units, to AUPREC places.
    /// 15.5 is 1.55E+01, 15.50, 1'-3.50", 1'-3 1/2" and 15 1/2; 45 degrees is 45.0000, 45d0'0", 50.0000g, 0.7854r and
    /// N 45d0'0" E, as the examples of -UNITS give them.
    /// </summary>
    public static class CadUnits
    {
        /// <summary>Writes a length in a drawing's units, as AutoCAD's RTOS.</summary>
        /// <param name="value">The length, in drawing units (inches where the units are of feet and inches).</param>
        /// <param name="lunits">LUNITS, 1 to 5; any other as decimal.</param>
        /// <param name="luprec">LUPREC, 0 to 8.</param>
        /// <returns>The text.</returns>
        public static string Length(double value, int lunits, int luprec)
        {
            int places = Math.Max(0, Math.Min(8, luprec));
            double size = Math.Abs(value);
            string text;

            switch (lunits)
            {
                case 1:
                    text = size.ToString((places == 0 ? "0" : "0." + new string('0', places)) + "E+00", CultureInfo.InvariantCulture);
                    break;
                case 3:
                    double feet3 = Math.Floor(size / 12.0);
                    double inches3 = Math.Round(size - feet3 * 12.0, places, MidpointRounding.AwayFromZero);

                    if (inches3 >= 12.0)
                    {
                        feet3++;
                        inches3 -= 12.0;
                    }

                    text = Whole(feet3) + "'-" + inches3.ToString("F" + Whole(places), CultureInfo.InvariantCulture) + "\"";
                    break;
                case 4:
                    long denominator = 1L << places;
                    long sixteenths = (long)Math.Round(size * denominator, MidpointRounding.AwayFromZero);
                    long feet4 = sixteenths / (12L * denominator);
                    long rest = sixteenths - feet4 * 12L * denominator;
                    text = Whole(feet4) + "'-" + Fraction(rest, denominator, true) + "\"";
                    break;
                case 5:
                    long parts = 1L << places;
                    text = Fraction((long)Math.Round(size * parts, MidpointRounding.AwayFromZero), parts, false);
                    break;
                default:
                    text = size.ToString("F" + Whole(places), CultureInfo.InvariantCulture);
                    break;
            }

            return value < 0.0 && Regex.IsMatch(text, "[1-9]") ? "-" + text : text;
        }

        /// <summary>Writes an angle in a drawing's units, as AutoCAD's ANGTOS: measured from east, counter-clockwise.</summary>
        /// <param name="radians">The angle, in radians.</param>
        /// <param name="aunits">AUNITS, 0 to 4; any other as decimal degrees.</param>
        /// <param name="auprec">AUPREC, 0 to 8.</param>
        /// <returns>The text.</returns>
        public static string Angle(double radians, int aunits, int auprec)
        {
            int places = Math.Max(0, Math.Min(8, auprec));
            double degrees = radians * 180.0 / Math.PI;

            switch (aunits)
            {
                case 1:
                    return (degrees < 0.0 ? "-" : string.Empty) + Sexagesimal(Math.Abs(degrees), places);
                case 2:
                    return (radians * 200.0 / Math.PI).ToString("F" + Whole(places), CultureInfo.InvariantCulture) + "g";
                case 3:
                    return radians.ToString("F" + Whole(places), CultureInfo.InvariantCulture) + "r";
                case 4:
                    double turned = degrees % 360.0;
                    turned = turned < 0.0 ? turned + 360.0 : turned;
                    double unit = places == 0 ? 1.0 : places <= 2 ? 1.0 / 60.0 : 1.0 / 3600.0;
                    turned = Math.Round(turned / unit) * unit % 360.0;

                    if (turned == 0.0 || turned == 90.0 || turned == 180.0 || turned == 270.0)
                    {
                        return turned == 0.0 ? "E" : turned == 90.0 ? "N" : turned == 180.0 ? "W" : "S";
                    }

                    string ns = turned < 180.0 ? "N" : "S";
                    string ew = turned < 90.0 || turned > 270.0 ? "E" : "W";
                    double bearing = ns == "N" ? Math.Abs(90.0 - turned) : Math.Abs(270.0 - turned);
                    return ns + " " + Sexagesimal(bearing, places) + " " + ew;
                default:
                    return degrees.ToString("F" + Whole(places), CultureInfo.InvariantCulture);
            }
        }

        /// <summary>
        /// Reads a length typed in a drawing's units: a number as ever, and in engineering and architectural units feet and
        /// inches as AutoCAD reads them, 1'3", 1'-3-1/2" or 3-1/2"; in architectural and fractional units a fraction, 15-1/2.
        /// </summary>
        /// <param name="text">The text typed.</param>
        /// <param name="lunits">LUNITS.</param>
        /// <param name="value">The length, in drawing units.</param>
        /// <returns>Whether it read.</returns>
        public static bool TryLength(string text, int lunits, out double value)
        {
            string typed = (text ?? string.Empty).Trim();

            if (double.TryParse(typed, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && !double.IsNaN(value) && !double.IsInfinity(value))
            {
                return true;
            }

            value = 0.0;

            if (lunits == 3 || lunits == 4)
            {
                Match feet = Regex.Match(typed, "^(?<sign>-?)(?:(?<feet>\\d+(?:\\.\\d*)?)')?-?(?:(?<inches>[0-9./ -]+)\"?)?$");

                if (feet.Success && (feet.Groups["feet"].Success || typed.EndsWith("\"", StringComparison.Ordinal)))
                {
                    double inches = 0.0;

                    if (feet.Groups["inches"].Success && feet.Groups["inches"].Value.Trim().Length > 0 && !TryFraction(feet.Groups["inches"].Value.Trim(), out inches))
                    {
                        return false;
                    }

                    double whole = feet.Groups["feet"].Success ? double.Parse(feet.Groups["feet"].Value, CultureInfo.InvariantCulture) : 0.0;
                    value = (whole * 12.0 + inches) * (feet.Groups["sign"].Value == "-" ? -1.0 : 1.0);
                    return true;
                }
            }

            if ((lunits == 4 || lunits == 5) && TryFraction(typed.TrimStart('-'), out double fraction))
            {
                value = typed.StartsWith("-", StringComparison.Ordinal) ? -fraction : fraction;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Reads an angle typed in a drawing's units, as AutoCAD reads one: a number in the units of AUNITS (degrees, grads or
        /// radians), or with its own mark, 45d30'15", 50g, 0.7854r, or a bearing, N 45d E.
        /// </summary>
        /// <param name="text">The text typed.</param>
        /// <param name="aunits">AUNITS.</param>
        /// <param name="radians">The angle, in radians, measured from east, counter-clockwise.</param>
        /// <returns>Whether it read.</returns>
        public static bool TryAngle(string text, int aunits, out double radians)
        {
            string typed = (text ?? string.Empty).Trim();
            radians = 0.0;

            if (double.TryParse(typed, NumberStyles.Float, CultureInfo.InvariantCulture, out double number) && !double.IsNaN(number) && !double.IsInfinity(number))
            {
                radians = aunits == 2 ? number * Math.PI / 200.0 : aunits == 3 ? number : number * Math.PI / 180.0;
                return true;
            }

            if (typed.Length > 1 && (typed.EndsWith("g", StringComparison.OrdinalIgnoreCase) || typed.EndsWith("r", StringComparison.OrdinalIgnoreCase))
                && double.TryParse(typed.Substring(0, typed.Length - 1), NumberStyles.Float, CultureInfo.InvariantCulture, out number))
            {
                radians = char.ToLowerInvariant(typed[typed.Length - 1]) == 'g' ? number * Math.PI / 200.0 : number;
                return true;
            }

            Match bearing = Regex.Match(typed, "^(?<ns>[NnSs])\\s*(?<angle>[^EeWw]*?)\\s*(?<ew>[EeWw])$");

            if (bearing.Success && TryDegrees(bearing.Groups["angle"].Value, out double off))
            {
                bool north = char.ToUpperInvariant(bearing.Groups["ns"].Value[0]) == 'N';
                bool east = char.ToUpperInvariant(bearing.Groups["ew"].Value[0]) == 'E';
                double degrees = north ? (east ? 90.0 - off : 90.0 + off) : (east ? 270.0 + off : 270.0 - off);
                radians = degrees * Math.PI / 180.0;
                return true;
            }

            if (typed.Length == 1 && "NnEeSsWw".IndexOf(typed[0]) >= 0)
            {
                radians = "EeNnWwSs".IndexOf(typed[0]) / 2 * Math.PI / 2.0;
                return true;
            }

            if (TryDegrees(typed, out double read))
            {
                radians = read * Math.PI / 180.0;
                return true;
            }

            return false;
        }

        /// <summary>Degrees with minutes and seconds, 45d30'15", or a plain number of degrees.</summary>
        private static bool TryDegrees(string text, out double degrees)
        {
            degrees = 0.0;
            Match dms = Regex.Match((text ?? string.Empty).Trim(), "^(?<sign>-?)(?<d>\\d+(?:\\.\\d*)?)(?:[dD°](?:(?<m>\\d+(?:\\.\\d*)?)')?(?:(?<s>\\d+(?:\\.\\d*)?)\")?)?$");

            if (!dms.Success)
            {
                return false;
            }

            degrees = double.Parse(dms.Groups["d"].Value, CultureInfo.InvariantCulture)
                + (dms.Groups["m"].Success ? double.Parse(dms.Groups["m"].Value, CultureInfo.InvariantCulture) / 60.0 : 0.0)
                + (dms.Groups["s"].Success ? double.Parse(dms.Groups["s"].Value, CultureInfo.InvariantCulture) / 3600.0 : 0.0);
            degrees = dms.Groups["sign"].Value == "-" ? -degrees : degrees;
            return true;
        }

        /// <summary>A number with a fraction, 3-1/2, 3 1/2, 1/2 or 3.5.</summary>
        private static bool TryFraction(string text, out double value)
        {
            value = 0.0;
            Match fraction = Regex.Match(text, "^(?:(?<whole>\\d+(?:\\.\\d*)?)(?:[ -]+|$))?(?:(?<top>\\d+)/(?<bottom>\\d+))?$");

            if (!fraction.Success || (!fraction.Groups["whole"].Success && !fraction.Groups["top"].Success))
            {
                return false;
            }

            double bottom = fraction.Groups["bottom"].Success ? double.Parse(fraction.Groups["bottom"].Value, CultureInfo.InvariantCulture) : 1.0;

            if (bottom == 0.0)
            {
                return false;
            }

            value = (fraction.Groups["whole"].Success ? double.Parse(fraction.Groups["whole"].Value, CultureInfo.InvariantCulture) : 0.0)
                + (fraction.Groups["top"].Success ? double.Parse(fraction.Groups["top"].Value, CultureInfo.InvariantCulture) / bottom : 0.0);
            return true;
        }

        private static string Sexagesimal(double degrees, int places)
        {
            if (places == 0)
            {
                return Whole(Math.Round(degrees, MidpointRounding.AwayFromZero)) + "d";
            }

            if (places <= 2)
            {
                double minutesAll = Math.Round(degrees * 60.0, MidpointRounding.AwayFromZero);
                return Whole(Math.Floor(minutesAll / 60.0)) + "d" + Whole(minutesAll - Math.Floor(minutesAll / 60.0) * 60.0) + "'";
            }

            int decimals = places - 4 > 0 ? places - 4 : 0;
            double secondsAll = Math.Round(degrees * 3600.0, decimals, MidpointRounding.AwayFromZero);
            double whole = Math.Floor(secondsAll / 3600.0);
            double minutes = Math.Floor((secondsAll - whole * 3600.0) / 60.0);
            double seconds = secondsAll - whole * 3600.0 - minutes * 60.0;
            return Whole(whole) + "d" + Whole(minutes) + "'" + seconds.ToString("F" + Whole(decimals), CultureInfo.InvariantCulture) + "\"";
        }

        /// <summary>A whole number and its fraction over a power of two, reduced: 3 1/2, 1/2, or 3 where none is left.</summary>
        private static string Fraction(long parts, long denominator, bool zero)
        {
            long whole = parts / denominator;
            long top = parts % denominator;
            long bottom = denominator;

            while (top != 0 && top % 2 == 0)
            {
                top /= 2;
                bottom /= 2;
            }

            if (top == 0)
            {
                return Whole(whole);
            }

            return (whole == 0 && !zero ? string.Empty : Whole(whole) + " ") + Whole(top) + "/" + Whole(bottom);
        }

        private static string Whole(double value) => value.ToString("0", CultureInfo.InvariantCulture);
    }
}
