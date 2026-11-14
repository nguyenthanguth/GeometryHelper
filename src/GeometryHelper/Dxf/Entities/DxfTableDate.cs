using System;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// A date or a time typed in a table's cell, as AutoCAD 2021 reads it into the value of a cell (D41, the console's
    /// <c>SetValue</c> of a text with <c>ConvertTextToValue</c>, in the culture of the machine): which words are one, the
    /// 16 bytes of a <c>SYSTEMTIME</c> it keeps them in (92 and 310), the format it gives each (<c>%x</c> for a date,
    /// <c>%X</c> for a time, <c>%c</c> for both) and the words it shows. A time of the day alone is a date of 30 December
    /// 1899, the day of an OLE date's nought.
    /// </summary>
    internal static class DxfTableDate
    {
        // The day an OLE date counts from, which a time given with no day is on.
        private static readonly DateTime TimeDay = new DateTime(1899, 12, 30);

        /// <summary>
        /// Reads the words as a date or a time, as AutoCAD 2021 does in the culture given: a date in the order of the culture
        /// (1/2/2020 the second of January in en-US), with a month in words (Jan 1 2020, 1 January 2020), a year of two
        /// figures (1/1/49 is 2049 and 1/1/50 is 1950), no year (the present one), a time of 12 or 24 hours (12:30,
        /// 12:30:45, 12:30 PM, 1 AM) alone or after the date, and 1-2 and 2020-01. It reads no fraction of a second, no
        /// name of a weekday, no zone and no point where the culture has none as its mark (12:30:45.5, Sat Jan 4 2020, 12:30Z,
        /// 3.4.2020 in en-US, a date in de-DE), nor a day or
        /// a time that does not exist (2/30/2020, 24:00), nor a year before 100; those stay words.
        /// </summary>
        internal static bool TryRead(string text, CultureInfo culture, out DateTime value)
        {
            value = default(DateTime);
            string words = (text ?? string.Empty).Trim();

            // A fraction of a second is none of AutoCAD's: 12:30:45.5 stays words in every culture.
            if (words.Length == 0 || !Regex.IsMatch(words, @"\d") || Regex.IsMatch(words, @":\d+[.,]\d"))
            {
                return false;
            }

            DateTimeFormatInfo names = culture.DateTimeFormat;
            bool dated = words.IndexOf('/') >= 0 || words.IndexOf('-') >= 0;

            // The marks a date is written with, and the culture's own for the date and the time (a point in de-DE, not in en-US).
            foreach (char c in words)
            {
                if (
                    !(
                        char.IsLetterOrDigit(c)
                        || c == ' '
                        || c == '/'
                        || c == '-'
                        || c == ':'
                        || c == ','
                        || names.DateSeparator.IndexOf(c) >= 0
                        || names.TimeSeparator.IndexOf(c) >= 0
                    )
                )
                {
                    return false;
                }
            }

            // Only the names of months and the marks of the morning and the afternoon: no weekday, no zone, no T.
            bool named = false;

            foreach (Match letters in Regex.Matches(words, @"\p{L}+"))
            {
                if (IsMonth(letters.Value, names))
                {
                    dated = true;
                    named = true;
                }
                else if (
                    !string.Equals(letters.Value, names.AMDesignator, StringComparison.CurrentCultureIgnoreCase)
                    && !string.Equals(letters.Value, names.PMDesignator, StringComparison.CurrentCultureIgnoreCase)
                )
                {
                    return false;
                }
            }

            // A comma is for a month in words (Jan 1, 2020): 1,5 is a point, and over a date it is no date.
            if (!named && words.IndexOf(',') >= 0)
            {
                return false;
            }

            if (
                !DateTime.TryParse(
                    words,
                    culture,
                    DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.NoCurrentDateDefault,
                    out DateTime read
                )
            )
            {
                return false;
            }

            if (!dated && read.Date == DateTime.MinValue)
            {
                value = TimeDay + read.TimeOfDay;
                return true;
            }

            if (read.Year < 100)
            {
                return false;
            }

            value = new DateTime(read.Year, read.Month, read.Day, read.Hour, read.Minute, read.Second);
            return true;
        }

        private static bool IsMonth(string word, DateTimeFormatInfo names)
        {
            for (int month = 0; month < 12; month++)
            {
                if (
                    string.Equals(word, names.MonthNames[month], StringComparison.CurrentCultureIgnoreCase)
                    || string.Equals(
                        word,
                        names.AbbreviatedMonthNames[month],
                        StringComparison.CurrentCultureIgnoreCase
                    )
                )
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// The format AutoCAD 2021 gives a date typed in a cell of no kind: <c>%x</c>, the date, where the day has no time or
        /// the time is that of midnight (12:30 AM is a time, and 0:00 is the date 12/30/1899), <c>%X</c>, the time, where it
        /// is on the day of a time alone, and <c>%c</c>, both, otherwise.
        /// </summary>
        internal static string FormatOf(DateTime value) =>
            value.TimeOfDay == TimeSpan.Zero ? "%x"
            : value.Date == TimeDay ? "%X"
            : "%c";

        /// <summary>
        /// The words a date shows in a format, as AutoCAD 2021 shows them in the culture given: the date for none or
        /// <c>%x</c>, the time for <c>%X</c>, both for <c>%c</c>, the letters of a C library's <c>strftime</c> after a percent
        /// (<c>%A %a %B %b %d %e %H %I %M %m %p %S %w %Y %y %D %R %T</c>, as <c>%d/%m/%Y</c> 07/03/2020 for the 7th of March
        /// 2020), the figures d, M and y of a format with no percent (M/d/yyyy), the rest as it is; the date for a format
        /// the library does not write.
        /// </summary>
        internal static string Text(DateTime value, string format, CultureInfo culture)
        {
            if (string.IsNullOrEmpty(format) || format == "%x")
            {
                return value.ToString("d", culture);
            }

            if (format == "%X")
            {
                return value.ToString("T", culture);
            }

            if (format == "%c")
            {
                return value.ToString("G", culture);
            }

            if (format.IndexOf('%') < 0)
            {
                return Regex.IsMatch(format, "^[dMyg/ .,:\\-]+$")
                    ? value.ToString(format.Replace("/", "'/'").Replace(":", "':'"), culture)
                    : format;
            }

            DateTimeFormatInfo names = culture.DateTimeFormat;
            var text = new StringBuilder();

            for (int i = 0; i < format.Length; i++)
            {
                if (format[i] != '%' || i + 1 >= format.Length)
                {
                    text.Append(format[i]);
                    continue;
                }

                char letter = format[++i];
                string part;

                switch (letter)
                {
                    case 'a':
                        part = names.AbbreviatedDayNames[(int)value.DayOfWeek];
                        break;
                    case 'A':
                        part = names.DayNames[(int)value.DayOfWeek];
                        break;
                    case 'b':
                        part = names.AbbreviatedMonthNames[value.Month - 1];
                        break;
                    case 'B':
                        part = names.MonthNames[value.Month - 1];
                        break;
                    case 'c':
                        part = value.ToString("G", culture);
                        break;
                    case 'd':
                        part = value.Day.ToString("00", CultureInfo.InvariantCulture);
                        break;
                    case 'e':
                        part = value.Day.ToString(CultureInfo.InvariantCulture).PadLeft(2);
                        break;
                    case 'H':
                        part = value.Hour.ToString("00", CultureInfo.InvariantCulture);
                        break;
                    case 'I':
                        part = (value.Hour % 12 == 0 ? 12 : value.Hour % 12).ToString(
                            "00",
                            CultureInfo.InvariantCulture
                        );
                        break;
                    case 'm':
                        part = value.Month.ToString("00", CultureInfo.InvariantCulture);
                        break;
                    case 'M':
                        part = value.Minute.ToString("00", CultureInfo.InvariantCulture);
                        break;
                    case 'p':
                        part = value.Hour < 12 ? names.AMDesignator : names.PMDesignator;
                        break;
                    case 'S':
                        part = value.Second.ToString("00", CultureInfo.InvariantCulture);
                        break;
                    case 'w':
                        part = ((int)value.DayOfWeek).ToString(CultureInfo.InvariantCulture);
                        break;
                    case 'x':
                        part = value.ToString("d", culture);
                        break;
                    case 'X':
                        part = value.ToString("T", culture);
                        break;
                    case 'y':
                        part = (value.Year % 100).ToString("00", CultureInfo.InvariantCulture);
                        break;
                    case 'Y':
                        part = value.Year.ToString(CultureInfo.InvariantCulture);
                        break;
                    case 'D':
                        part = value.ToString("MM/dd/yy", CultureInfo.InvariantCulture);
                        break;
                    case 'R':
                        part = value.ToString("HH:mm", CultureInfo.InvariantCulture);
                        break;
                    case 'T':
                        part = value.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
                        break;
                    case '%':
                        part = "%";
                        break;
                    default:
                        return value.ToString("d", culture);
                }

                text.Append(part);
            }

            return text.ToString();
        }

        /// <summary>
        /// The words a cell of a date opens as in AutoCAD 2021's editor, <c>GetTextString(FormatOption.ForEditing)</c>, whatever
        /// its format: the date alone where the day has no time (1/1/2020), the time alone where it is on the day of a time
        /// (12:30:00 PM), and both otherwise (1/1/2020 12:30:00 PM).
        /// </summary>
        internal static string Editing(DateTime value, CultureInfo culture) =>
            value.ToString(
                value.TimeOfDay == TimeSpan.Zero ? "d"
                    : value.Date == TimeDay ? "T"
                    : "G",
                culture
            );

        /// <summary>
        /// The 16 bytes AutoCAD 2021 keeps a date in (310 after a 92 of 16), a <c>SYSTEMTIME</c> of its year, month, day of
        /// the week from Sunday, day, hour, minute, second and thousandths, each of two bytes, the low one first, in capital
        /// letters of hexadecimal: 1/1/2020 12:30 is <c>E4070100030001000C001E0000000000</c>.
        /// </summary>
        internal static string Bytes(DateTime value)
        {
            int[] parts =
            {
                value.Year,
                value.Month,
                (int)value.DayOfWeek,
                value.Day,
                value.Hour,
                value.Minute,
                value.Second,
                0,
            };
            var hex = new StringBuilder(32);

            foreach (int part in parts)
            {
                hex.Append((part & 0xFF).ToString("X2", CultureInfo.InvariantCulture));
                hex.Append((part >> 8).ToString("X2", CultureInfo.InvariantCulture));
            }

            return hex.ToString();
        }

        /// <summary>The date the 16 bytes of a cell's 310 hold, as <see cref="Bytes"/> writes them; false for any other group.</summary>
        internal static bool TryFromBytes(string hex, out DateTime value)
        {
            value = default(DateTime);
            string bytes = hex?.Trim();

            if (bytes == null || bytes.Length != 32)
            {
                return false;
            }

            var parts = new int[8];

            for (int i = 0; i < 8; i++)
            {
                if (
                    !int.TryParse(
                        bytes.Substring(4 * i, 2),
                        NumberStyles.AllowHexSpecifier,
                        CultureInfo.InvariantCulture,
                        out int low
                    )
                    || !int.TryParse(
                        bytes.Substring(4 * i + 2, 2),
                        NumberStyles.AllowHexSpecifier,
                        CultureInfo.InvariantCulture,
                        out int high
                    )
                )
                {
                    return false;
                }

                parts[i] = low + 256 * high;
            }

            if (
                parts[0] < 1
                || parts[0] > 9999
                || parts[1] < 1
                || parts[1] > 12
                || parts[3] < 1
                || parts[3] > DateTime.DaysInMonth(parts[0], parts[1])
                || parts[4] > 23
                || parts[5] > 59
                || parts[6] > 59
            )
            {
                return false;
            }

            value = new DateTime(parts[0], parts[1], parts[3], parts[4], parts[5], parts[6]);
            return true;
        }
    }
}
