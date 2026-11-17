using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// The words a spreadsheet's number format makes of a number: the built-in formats of a workbook (General, 0, 0.00, #,##0,
    /// 0%, 0.00E+00, the dates and the times) and the custom codes made of the same parts, in the English of the invariant
    /// culture. A code it does not know shows the number as General does.
    /// </summary>
    internal static class CadNumberFormat
    {
        private static readonly Dictionary<int, string> BuiltIn = new Dictionary<int, string>
        {
            [1] = "0",
            [2] = "0.00",
            [3] = "#,##0",
            [4] = "#,##0.00",
            [9] = "0%",
            [10] = "0.00%",
            [11] = "0.00E+00",
            [14] = "m/d/yyyy",
            [15] = "d-mmm-yy",
            [16] = "d-mmm",
            [17] = "mmm-yy",
            [18] = "h:mm AM/PM",
            [19] = "h:mm:ss AM/PM",
            [20] = "h:mm",
            [21] = "h:mm:ss",
            [22] = "m/d/yyyy h:mm",
            [37] = "#,##0 ;(#,##0)",
            [38] = "#,##0 ;(#,##0)",
            [39] = "#,##0.00;(#,##0.00)",
            [40] = "#,##0.00;(#,##0.00)",
            [45] = "mm:ss",
            [46] = "[h]:mm:ss",
            [47] = "mm:ss.0",
            [49] = "@",
        };

        /// <summary>The code of a built-in format number; null for General and for a number that is not built in.</summary>
        internal static string CodeOf(int id) => BuiltIn.TryGetValue(id, out string code) ? code : null;

        /// <summary>Whether a code shows dates or times: a d, m, y, h or s outside quotes and brackets (but [h], [m] and [s] elapsed times too).</summary>
        internal static bool IsDate(string code)
        {
            if (string.IsNullOrEmpty(code))
            {
                return false;
            }

            string first = Sections(code)[0];
            bool quoted = false;
            bool bracket = false;

            for (int i = 0; i < first.Length; i++)
            {
                char ch = first[i];

                if (ch == '"')
                {
                    quoted = !quoted;
                }
                else if (quoted)
                {
                    continue;
                }
                else if (ch == '\\' || ch == '_')
                {
                    i++;
                }
                else if (ch == '[')
                {
                    bracket = true;

                    if (i + 1 < first.Length && "hHmMsS".IndexOf(first[i + 1]) >= 0)
                    {
                        return true;
                    }
                }
                else if (ch == ']')
                {
                    bracket = false;
                }
                else if (!bracket && "dDmMyYhHsS".IndexOf(ch) >= 0)
                {
                    // E+ of a scientific format and the e of "General" are no date; the letters tested above are all a date has.
                    return !first.StartsWith("General", StringComparison.OrdinalIgnoreCase);
                }
            }

            return false;
        }

        /// <summary>The words a number shows in a format code; General (null or empty) gives up to 11 figures.</summary>
        /// <param name="value">The number.</param>
        /// <param name="code">The code.</param>
        /// <param name="date1904">Whether the workbook counts its dates from 1904.</param>
        internal static string Format(double value, string code, bool date1904)
        {
            if (string.IsNullOrEmpty(code) || code.Equals("General", StringComparison.OrdinalIgnoreCase))
            {
                return General(value);
            }

            List<string> sections = Sections(code);
            string section =
                value < 0.0 && sections.Count > 1 ? sections[1]
                : value == 0.0 && sections.Count > 2 ? sections[2]
                : sections[0];

            // A code with a section of its own for the negatives shows no minus; one section shows it.
            bool minus = value < 0.0 && sections.Count == 1;

            if (value < 0.0 && sections.Count > 1)
            {
                value = -value;
            }

            if (section.IndexOf('@') >= 0 && !IsDate(section))
            {
                return Literal(section, General(value));
            }

            return IsDate(section) ? DateText(value, section, date1904) : Number(Math.Abs(value), section, minus);
        }

        /// <summary>A number as General shows it: the figures it needs up to 11, scientific past 1E+11 and below 1E-9.</summary>
        internal static string General(double value)
        {
            if (value == 0.0)
            {
                return "0";
            }

            double size = Math.Abs(value);

            if (size >= 1e11 || size < 1e-9)
            {
                return value.ToString("0.#####E+00", CultureInfo.InvariantCulture);
            }

            // 11 significant figures, so that 0.1 + 0.2 shows 0.3.
            double rounded = double.Parse(
                value.ToString("G11", CultureInfo.InvariantCulture),
                CultureInfo.InvariantCulture
            );
            return rounded.ToString("0.###############", CultureInfo.InvariantCulture);
        }

        /// <summary>The sections of a code, split at the semicolons outside quotes.</summary>
        private static List<string> Sections(string code)
        {
            var parts = new List<string>();
            var current = new StringBuilder();
            bool quoted = false;

            for (int i = 0; i < code.Length; i++)
            {
                char ch = code[i];

                if (ch == '"')
                {
                    quoted = !quoted;
                }
                else if (!quoted && ch == '\\' && i + 1 < code.Length)
                {
                    current.Append(ch).Append(code[++i]);
                    continue;
                }
                else if (!quoted && ch == ';')
                {
                    parts.Add(current.ToString());
                    current.Clear();
                    continue;
                }

                current.Append(ch);
            }

            parts.Add(current.ToString());
            return parts;
        }

        /// <summary>A text section with its @ replaced by the text, its quotes and backslashes resolved.</summary>
        private static string Literal(string section, string text)
        {
            var result = new StringBuilder();
            bool quoted = false;

            for (int i = 0; i < section.Length; i++)
            {
                char ch = section[i];

                if (ch == '"')
                {
                    quoted = !quoted;
                }
                else if (quoted)
                {
                    result.Append(ch);
                }
                else if (ch == '\\' && i + 1 < section.Length)
                {
                    result.Append(section[++i]);
                }
                else if (ch == '@')
                {
                    result.Append(text);
                }
                else if (ch == '_' && i + 1 < section.Length)
                {
                    result.Append(' ');
                    i++;
                }
                else if (ch != '*' && ch != '[' && ch != ']')
                {
                    result.Append(ch);
                }
            }

            return result.ToString();
        }

        private static string Number(double value, string section, bool negative)
        {
            // The first and last place of a digit; what is before and after them is literal.
            int first = -1;
            int last = -1;
            bool quoted = false;
            bool bracket = false;

            for (int i = 0; i < section.Length; i++)
            {
                char ch = section[i];

                if (ch == '"')
                {
                    quoted = !quoted;
                }
                else if (!quoted && ch == '[')
                {
                    bracket = true;
                }
                else if (!quoted && ch == ']')
                {
                    bracket = false;
                }
                else if (
                    !quoted
                    && !bracket
                    && first >= 0
                    && (ch == 'E' || ch == 'e')
                    && i + 1 < section.Length
                    && (section[i + 1] == '+' || section[i + 1] == '-')
                )
                {
                    // The exponent's places are no places of the number.
                    break;
                }
                else if (!quoted && !bracket && (ch == '0' || ch == '#' || ch == '?'))
                {
                    first = first < 0 ? i : first;
                    last = i;
                }
                else if (!quoted && ch == '\\')
                {
                    i++;
                }
            }

            if (first < 0)
            {
                return Literal(section, General(value));
            }

            string head = section.Substring(0, first);
            string body = section.Substring(first, last - first + 1);
            string tail = section.Substring(last + 1);
            bool percent = head.IndexOf('%') >= 0 || tail.IndexOf('%') >= 0;
            bool scientific =
                tail.StartsWith("E+", StringComparison.OrdinalIgnoreCase)
                || tail.StartsWith("E-", StringComparison.OrdinalIgnoreCase);
            int exponentDigits = 0;

            if (scientific)
            {
                int end = 2;

                while (end < tail.Length && tail[end] == '0')
                {
                    end++;
                }

                exponentDigits = end - 2;
            }

            // Thousands after the last place scale the number down; those inside it group its figures.
            bool grouped = false;
            int scale = 0;
            var places = new StringBuilder();

            foreach (char ch in body)
            {
                if (ch == ',')
                {
                    grouped = true;
                }
                else
                {
                    places.Append(ch);
                }
            }

            foreach (char ch in tail)
            {
                if (ch == ',')
                {
                    scale++;
                }
                else
                {
                    break;
                }
            }

            string pattern = places.ToString();
            int point = pattern.IndexOf('.');
            string integerPart = point < 0 ? pattern : pattern.Substring(0, point);
            string fractionPart = point < 0 ? string.Empty : pattern.Substring(point + 1);
            int decimals = fractionPart.Length;
            int minimum = integerPart.Replace("#", string.Empty).Replace("?", string.Empty).Length;
            int required = fractionPart.TrimEnd('#', '?').Length;

            if (percent)
            {
                value *= 100.0;
            }

            for (int i = 0; i < scale; i++)
            {
                value /= 1000.0;
            }

            string digits;
            string exponent = string.Empty;

            if (scientific)
            {
                int power = value == 0.0 ? 0 : (int)Math.Floor(Math.Log10(value));
                double mantissa = value / Math.Pow(10.0, power);
                mantissa = Math.Round(mantissa, decimals, MidpointRounding.AwayFromZero);

                if (mantissa >= 10.0)
                {
                    mantissa /= 10.0;
                    power++;
                }

                digits = mantissa.ToString(
                    "F" + decimals.ToString(CultureInfo.InvariantCulture),
                    CultureInfo.InvariantCulture
                );
                exponent =
                    "E"
                    + (power < 0 ? "-" : "+")
                    + Math.Abs(power)
                        .ToString(new string('0', Math.Max(1, exponentDigits)), CultureInfo.InvariantCulture);
                tail = tail.Substring(2 + exponentDigits);
            }
            else
            {
                digits = Math.Round(value, Math.Min(decimals, 15), MidpointRounding.AwayFromZero)
                    .ToString("F" + decimals.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
            }

            int dot = digits.IndexOf('.');
            string whole = dot < 0 ? digits : digits.Substring(0, dot);
            string fraction = dot < 0 ? string.Empty : digits.Substring(dot + 1);

            while (fraction.Length > required && fraction.EndsWith("0", StringComparison.Ordinal))
            {
                fraction = fraction.Substring(0, fraction.Length - 1);
            }

            if (whole == "0" && minimum == 0)
            {
                whole = fraction.Length > 0 || decimals == 0 ? (decimals == 0 ? "0" : string.Empty) : "0";
            }

            whole = whole.PadLeft(minimum, '0');

            if (grouped && whole.Length > 3)
            {
                var spaced = new StringBuilder();

                for (int i = 0; i < whole.Length; i++)
                {
                    spaced.Append(whole[i]);

                    if ((whole.Length - 1 - i) % 3 == 0 && i < whole.Length - 1)
                    {
                        spaced.Append(',');
                    }
                }

                whole = spaced.ToString();
            }

            string shown = whole + (fraction.Length > 0 ? "." + fraction : string.Empty) + exponent;
            string text = Literal(head, string.Empty) + shown + Literal(tail, string.Empty);
            return
                negative
                && !text.StartsWith("(", StringComparison.Ordinal)
                && !text.StartsWith("-", StringComparison.Ordinal)
                ? "-" + text
                : text;
        }

        /// <summary>The days since 30 December 1899 (or 1 January 1904) as a date and time in a date format code.</summary>
        private static string DateText(double serial, string section, bool date1904)
        {
            DateTime origin = date1904 ? new DateTime(1904, 1, 1) : new DateTime(1899, 12, 30);

            if (serial < 0.0 || serial > 2958465.0)
            {
                return "#####";
            }

            // The whole milliseconds of the day, rounded: .9999999 of a day is the next midnight and not 23:59:60.
            DateTime moment = origin.AddMilliseconds(Math.Round(serial * 86400000.0));
            var result = new StringBuilder();
            bool pm =
                section.IndexOf("AM/PM", StringComparison.OrdinalIgnoreCase) >= 0
                || section.IndexOf("A/P", StringComparison.OrdinalIgnoreCase) >= 0;
            bool quoted = false;

            for (int i = 0; i < section.Length; i++)
            {
                char ch = section[i];

                if (ch == '"')
                {
                    quoted = !quoted;
                    continue;
                }

                if (quoted)
                {
                    result.Append(ch);
                    continue;
                }

                if (ch == '\\' && i + 1 < section.Length)
                {
                    result.Append(section[++i]);
                    continue;
                }

                if (ch == '_' && i + 1 < section.Length)
                {
                    result.Append(' ');
                    i++;
                    continue;
                }

                if (ch == '[')
                {
                    int close = section.IndexOf(']', i);
                    string inside = close < 0 ? string.Empty : section.Substring(i + 1, close - i - 1);

                    if (inside.Length > 0 && "hH".IndexOf(inside[0]) >= 0)
                    {
                        result.Append(((long)Math.Floor(serial * 24.0)).ToString(CultureInfo.InvariantCulture));
                    }
                    else if (inside.Length > 0 && "mM".IndexOf(inside[0]) >= 0)
                    {
                        result.Append(((long)Math.Floor(serial * 1440.0)).ToString(CultureInfo.InvariantCulture));
                    }
                    else if (inside.Length > 0 && "sS".IndexOf(inside[0]) >= 0)
                    {
                        result.Append(((long)Math.Floor(serial * 86400.0)).ToString(CultureInfo.InvariantCulture));
                    }

                    i = close < 0 ? section.Length : close;
                    continue;
                }

                char low = char.ToLowerInvariant(ch);
                int run = 1;

                while (i + run < section.Length && char.ToLowerInvariant(section[i + run]) == low)
                {
                    run++;
                }

                if (low == 'y')
                {
                    result.Append(
                        run <= 2
                            ? (moment.Year % 100).ToString("00", CultureInfo.InvariantCulture)
                            : moment.Year.ToString("0000", CultureInfo.InvariantCulture)
                    );
                    i += run - 1;
                }
                else if (low == 'd')
                {
                    result.Append(
                        run == 1 ? moment.Day.ToString(CultureInfo.InvariantCulture)
                        : run == 2 ? moment.Day.ToString("00", CultureInfo.InvariantCulture)
                        : run == 3 ? moment.ToString("ddd", CultureInfo.InvariantCulture)
                        : moment.ToString("dddd", CultureInfo.InvariantCulture)
                    );
                    i += run - 1;
                }
                else if (low == 'm')
                {
                    // An m next to an h before it or an s after it is minutes, any other is the month.
                    bool minutes = AfterHour(section, i) || BeforeSecond(section, i + run);
                    result.Append(
                        minutes ? moment.Minute.ToString(run == 1 ? "0" : "00", CultureInfo.InvariantCulture)
                        : run == 1 ? moment.Month.ToString(CultureInfo.InvariantCulture)
                        : run == 2 ? moment.Month.ToString("00", CultureInfo.InvariantCulture)
                        : run == 3 ? moment.ToString("MMM", CultureInfo.InvariantCulture)
                        : run == 5 ? moment.ToString("MMMM", CultureInfo.InvariantCulture).Substring(0, 1)
                        : moment.ToString("MMMM", CultureInfo.InvariantCulture)
                    );
                    i += run - 1;
                }
                else if (low == 'h')
                {
                    int hour = pm ? (moment.Hour % 12 == 0 ? 12 : moment.Hour % 12) : moment.Hour;
                    result.Append(hour.ToString(run == 1 ? "0" : "00", CultureInfo.InvariantCulture));
                    i += run - 1;
                }
                else if (low == 's')
                {
                    result.Append(moment.Second.ToString(run == 1 ? "0" : "00", CultureInfo.InvariantCulture));
                    i += run - 1;

                    if (i + 2 < section.Length && section[i + 1] == '.' && section[i + 2] == '0')
                    {
                        int digits = 0;

                        while (i + 2 + digits < section.Length && section[i + 2 + digits] == '0')
                        {
                            digits++;
                        }

                        result
                            .Append('.')
                            .Append(
                                moment
                                    .Millisecond.ToString("000", CultureInfo.InvariantCulture)
                                    .Substring(0, Math.Min(3, digits))
                            );
                        i += 1 + digits;
                    }
                }
                else if (
                    (ch == 'A' || ch == 'a')
                    && string.Compare(section, i, "AM/PM", 0, 5, StringComparison.OrdinalIgnoreCase) == 0
                )
                {
                    result.Append(moment.Hour < 12 ? "AM" : "PM");
                    i += 4;
                }
                else if (
                    (ch == 'A' || ch == 'a')
                    && string.Compare(section, i, "A/P", 0, 3, StringComparison.OrdinalIgnoreCase) == 0
                )
                {
                    result.Append(moment.Hour < 12 ? "A" : "P");
                    i += 2;
                }
                else
                {
                    result.Append(ch);
                }
            }

            return result.ToString();
        }

        /// <summary>Whether the nearest letter before a place of the code is an h, so that an m there is minutes.</summary>
        private static bool AfterHour(string section, int at)
        {
            for (int i = at - 1; i >= 0; i--)
            {
                char low = char.ToLowerInvariant(section[i]);

                if (low == 'h' || section[i] == ']' && i > 0 && char.ToLowerInvariant(section[i - 1]) == 'h')
                {
                    return true;
                }

                if (char.IsLetter(section[i]))
                {
                    return false;
                }
            }

            return false;
        }

        /// <summary>Whether the nearest letter after a place of the code is an s, so that an m there is minutes.</summary>
        private static bool BeforeSecond(string section, int at)
        {
            for (int i = at; i < section.Length; i++)
            {
                if (char.ToLowerInvariant(section[i]) == 's')
                {
                    return true;
                }

                if (char.IsLetter(section[i]))
                {
                    return false;
                }
            }

            return false;
        }
    }
}
