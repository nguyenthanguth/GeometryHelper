using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// A DIESEL expression, as the field of a DieselExpression evaluates it (checked in AutoCAD 2021's console):
    /// <c>$(if,$(&gt;,2,1),yes,no)</c> is <c>yes</c>, <c>$(substr,hello,2,3)</c> is <c>ell</c>, <c>$(/,7,2)</c> is 3.5. It knows
    /// the functions +, -, *, /, =, &lt;, &gt;, &lt;=, &gt;=, !=, and, or, xor, eq, if, upper, strlen, substr, fix, rtos, angtos,
    /// edtime, getvar, index and eval; a function it does not know, or one given too few arguments, gives an empty text, as
    /// AutoCAD's DIESEL does.
    /// </summary>
    internal sealed class CadDiesel
    {
        private readonly Func<string, string> _variable;
        private readonly DateTime _now;
        private readonly CultureInfo _culture;

        /// <summary>Makes an evaluator.</summary>
        /// <param name="variable">Gives the value of a system variable as text, empty where it is none.</param>
        /// <param name="now">The time <c>$(getvar,date)</c> gives.</param>
        /// <param name="culture">The culture of the names of days and months.</param>
        internal CadDiesel(Func<string, string> variable, DateTime now, CultureInfo culture)
        {
            _variable = variable ?? (name => string.Empty);
            _now = now;
            _culture = culture ?? CultureInfo.InvariantCulture;
        }

        /// <summary>Evaluates the functions in a text, the rest of it left as it is.</summary>
        /// <param name="text">The expression.</param>
        /// <returns>The text with each function in it worked out.</returns>
        internal string Evaluate(string text)
        {
            var result = new StringBuilder();
            int at = 0;

            while (at < text.Length)
            {
                if (text[at] == '$' && at + 1 < text.Length && text[at + 1] == '(')
                {
                    int close = Closing(text, at + 1);

                    if (close < 0)
                    {
                        result.Append(text, at, text.Length - at);
                        break;
                    }

                    result.Append(Call(text.Substring(at + 2, close - at - 2)));
                    at = close + 1;
                }
                else
                {
                    result.Append(text[at]);
                    at++;
                }
            }

            return result.ToString();
        }

        /// <summary>Where the bracket that closes the one at an index stands; -1 where none does.</summary>
        private static int Closing(string text, int open)
        {
            int depth = 0;

            for (int i = open; i < text.Length; i++)
            {
                if (text[i] == '(')
                {
                    depth++;
                }
                else if (text[i] == ')' && --depth == 0)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>The arguments of a call, cut at the commas outside brackets, the name first.</summary>
        private static List<string> Split(string call)
        {
            var parts = new List<string>();
            int depth = 0;
            int start = 0;

            for (int i = 0; i < call.Length; i++)
            {
                if (call[i] == '(')
                {
                    depth++;
                }
                else if (call[i] == ')')
                {
                    depth--;
                }
                else if (call[i] == ',' && depth == 0)
                {
                    parts.Add(call.Substring(start, i - start));
                    start = i + 1;
                }
            }

            parts.Add(call.Substring(start));
            return parts;
        }

        private string Call(string call)
        {
            List<string> parts = Split(call);
            string name = parts[0].Trim().ToLowerInvariant();

            // A condition is worked out first and only the branch taken after it.
            if (name == "if")
            {
                if (parts.Count < 3)
                {
                    return string.Empty;
                }

                bool yes = Truth(Evaluate(parts[1]));
                return yes ? Evaluate(parts[2])
                    : parts.Count > 3 ? Evaluate(parts[3])
                    : string.Empty;
            }

            var args = new List<string>();

            for (int i = 1; i < parts.Count; i++)
            {
                args.Add(Evaluate(parts[i]));
            }

            switch (name)
            {
                case "+":
                case "-":
                case "*":
                case "/":
                    return Arithmetic(name, args);
                case "=":
                case "<":
                case ">":
                case "<=":
                case ">=":
                case "!=":
                    return args.Count < 2 ? string.Empty
                        : Compare(name, args[0], args[1]) ? "1"
                        : "0";
                case "and":
                    return Join(args, (a, b) => a && b);
                case "or":
                    return Join(args, (a, b) => a || b);
                case "xor":
                    return Join(args, (a, b) => a ^ b);
                case "eq":
                    return args.Count >= 2 && string.Equals(args[0], args[1], StringComparison.Ordinal) ? "1" : "0";
                case "upper":
                    return args.Count > 0 ? args[0].ToUpperInvariant() : string.Empty;
                case "strlen":
                    return (args.Count > 0 ? args[0].Length : 0).ToString(CultureInfo.InvariantCulture);
                case "substr":
                    return Substring(args);
                case "fix":
                    return args.Count > 0 && Number(args[0], out double fixer)
                        ? Math.Truncate(fixer).ToString("0", CultureInfo.InvariantCulture)
                        : "0";
                case "rtos":
                    return args.Count > 0 && Number(args[0], out double real) ? Rtos(real, args) : string.Empty;
                case "angtos":
                    return args.Count > 0 && Number(args[0], out double angle) ? Angtos(angle, args) : string.Empty;
                case "edtime":
                    return args.Count > 1 && Number(args[0], out double julian)
                        ? EdTime(julian, args[1])
                        : string.Empty;
                case "getvar":
                    return args.Count > 0 ? Variable(args[0]) : string.Empty;
                case "getenv":
                    return string.Empty;
                case "index":
                    return Index(args);
                case "eval":
                    return args.Count > 0 ? Evaluate(args[0]) : string.Empty;
                default:
                    return string.Empty;
            }
        }

        private static bool Number(string text, out double value) =>
            double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);

        private static bool Truth(string text) => Number(text, out double value) && value != 0.0;

        /// <summary>A number written as DIESEL writes one: whole where it is whole, else to seven places without the zeros after.</summary>
        private static string Write(double value)
        {
            if (Math.Abs(value - Math.Round(value)) < 1E-9 && Math.Abs(value) < 1E15)
            {
                return Math.Round(value).ToString("0", CultureInfo.InvariantCulture);
            }

            return value.ToString("0.#######", CultureInfo.InvariantCulture);
        }

        private static string Arithmetic(string name, List<string> args)
        {
            if (args.Count == 0 || !Number(args[0], out double total))
            {
                return string.Empty;
            }

            for (int i = 1; i < args.Count; i++)
            {
                if (!Number(args[i], out double next))
                {
                    return string.Empty;
                }

                switch (name)
                {
                    case "+":
                        total += next;
                        break;
                    case "-":
                        total -= next;
                        break;
                    case "*":
                        total *= next;
                        break;
                    default:
                        if (next == 0.0)
                        {
                            return string.Empty;
                        }

                        total /= next;
                        break;
                }
            }

            return Write(total);
        }

        private static bool Compare(string name, string a, string b)
        {
            int order =
                Number(a, out double x) && Number(b, out double y)
                    ? x.CompareTo(y)
                    : string.Compare(a, b, StringComparison.Ordinal);

            switch (name)
            {
                case "=":
                    return order == 0;
                case "<":
                    return order < 0;
                case ">":
                    return order > 0;
                case "<=":
                    return order <= 0;
                case ">=":
                    return order >= 0;
                default:
                    return order != 0;
            }
        }

        private static string Join(List<string> args, Func<bool, bool, bool> combine)
        {
            bool result = args.Count == 0 ? false : Truth(args[0]);

            for (int i = 1; i < args.Count; i++)
            {
                result = combine(result, Truth(args[i]));
            }

            return result ? "1" : "0";
        }

        /// <summary><c>$(substr,text,from,length)</c>: the letters from the place counted from one, to the end where no length is given.</summary>
        private static string Substring(List<string> args)
        {
            if (args.Count < 2 || !Number(args[1], out double from))
            {
                return string.Empty;
            }

            int start = (int)from - 1;

            if (start < 0 || start >= args[0].Length)
            {
                return string.Empty;
            }

            int length = args[0].Length - start;

            if (args.Count > 2 && Number(args[2], out double count))
            {
                length = Math.Max(0, Math.Min(length, (int)count));
            }

            return args[0].Substring(start, length);
        }

        private string Rtos(double value, List<string> args)
        {
            int mode = args.Count > 1 && Number(args[1], out double m) ? (int)m : 2;
            int precision = args.Count > 2 && Number(args[2], out double p) ? (int)p : 4;
            string format =
                "%lu"
                + Math.Max(1, Math.Min(5, mode)).ToString(CultureInfo.InvariantCulture)
                + "%pr"
                + Math.Max(0, Math.Min(8, precision)).ToString(CultureInfo.InvariantCulture);
            return CadFieldFormat.Parse(format).Number(value, false);
        }

        private string Angtos(double radians, List<string> args)
        {
            int mode = args.Count > 1 && Number(args[1], out double m) ? (int)m : 0;
            int precision = args.Count > 2 && Number(args[2], out double p) ? (int)p : 4;
            string format =
                "%au"
                + Math.Max(0, Math.Min(4, mode)).ToString(CultureInfo.InvariantCulture)
                + "%pr"
                + Math.Max(0, Math.Min(8, precision)).ToString(CultureInfo.InvariantCulture);
            return CadFieldFormat.Parse(format).Number(radians, true);
        }

        /// <summary>The system variable of a name as DIESEL writes it; the date as the Julian date of the time now.</summary>
        private string Variable(string name)
        {
            if (string.Equals(name.Trim(), "date", StringComparison.OrdinalIgnoreCase))
            {
                return Write8(Julian(_now));
            }

            return _variable(name.Trim()) ?? string.Empty;
        }

        private static string Write8(double value) => value.ToString("0.00000000", CultureInfo.InvariantCulture);

        /// <summary>The date AutoCAD's DATE gives: the days since 30 December 1899 counted from 2 415 019.</summary>
        internal static double Julian(DateTime date) => 2415019.0 + (date - new DateTime(1899, 12, 30)).TotalDays;

        /// <summary>The time of a Julian date, as <see cref="Julian"/> gives one.</summary>
        internal static DateTime FromJulian(double julian) => new DateTime(1899, 12, 30).AddDays(julian - 2415019.0);

        private static string Index(List<string> args)
        {
            if (args.Count < 2 || !Number(args[0], out double at) || at < 0.0)
            {
                return string.Empty;
            }

            string[] items = args[1].Split(',');
            return (int)at < items.Length ? items[(int)at] : string.Empty;
        }

        /// <summary>
        /// <c>$(edtime,date,picture)</c>: the picture's letters worked out from a Julian date: YYYY, YY, MONTH, MON, MO, M, DDDD,
        /// DDD, DD, D, HH, H, MM (minutes), SS, AM/PM; the rest as it stands.
        /// </summary>
        private string EdTime(double julian, string picture)
        {
            DateTime time = FromJulian(julian);
            string[] tokens =
            {
                "YYYY",
                "MONTH",
                "AM/PM",
                "DDDD",
                "YY",
                "MON",
                "DDD",
                "MO",
                "DD",
                "HH",
                "SS",
                "MM",
                "M",
                "D",
                "H",
            };
            var text = new StringBuilder();
            int at = 0;

            while (at < picture.Length)
            {
                string matched = null;

                foreach (string token in tokens)
                {
                    if (string.Compare(picture, at, token, 0, token.Length, StringComparison.OrdinalIgnoreCase) == 0)
                    {
                        matched = token;
                        break;
                    }
                }

                if (matched == null)
                {
                    text.Append(picture[at]);
                    at++;
                    continue;
                }

                at += matched.Length;

                switch (matched)
                {
                    case "YYYY":
                        text.Append(time.Year.ToString("0000", CultureInfo.InvariantCulture));
                        break;
                    case "YY":
                        text.Append((time.Year % 100).ToString("00", CultureInfo.InvariantCulture));
                        break;
                    case "MONTH":
                        text.Append(_culture.DateTimeFormat.GetMonthName(time.Month));
                        break;
                    case "MON":
                        text.Append(_culture.DateTimeFormat.GetAbbreviatedMonthName(time.Month));
                        break;
                    case "MO":
                        text.Append(time.Month.ToString("00", CultureInfo.InvariantCulture));
                        break;
                    case "M":
                        text.Append(time.Month.ToString(CultureInfo.InvariantCulture));
                        break;
                    case "DDDD":
                        text.Append(_culture.DateTimeFormat.GetDayName(time.DayOfWeek));
                        break;
                    case "DDD":
                        text.Append(_culture.DateTimeFormat.GetAbbreviatedDayName(time.DayOfWeek));
                        break;
                    case "DD":
                        text.Append(time.Day.ToString("00", CultureInfo.InvariantCulture));
                        break;
                    case "D":
                        text.Append(time.Day.ToString(CultureInfo.InvariantCulture));
                        break;
                    case "HH":
                        text.Append(time.Hour.ToString("00", CultureInfo.InvariantCulture));
                        break;
                    case "H":
                        text.Append(time.Hour.ToString(CultureInfo.InvariantCulture));
                        break;
                    case "MM":
                        text.Append(time.Minute.ToString("00", CultureInfo.InvariantCulture));
                        break;
                    case "SS":
                        text.Append(time.Second.ToString("00", CultureInfo.InvariantCulture));
                        break;
                    default:
                        text.Append(time.Hour < 12 ? "AM" : "PM");
                        break;
                }
            }

            return text.ToString();
        }
    }
}
