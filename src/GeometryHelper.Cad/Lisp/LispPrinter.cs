using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;

namespace GeometryHelper.Cad.Lisp
{
    /// <summary>
    /// Writes AutoLISP values as AutoCAD does. A real is C's <c>%g</c> with six digits and a <c>.0</c> added where the
    /// mantissa has no point: <c>1.0</c>, <c>0.333333</c>, <c>123457.0</c>, <c>1.23457e+06</c>, <c>1.0e+20</c>. A string in
    /// <c>prin1</c> form has <c>\\ \" \n \r \t \e</c> for the characters that have one, three-digit octal for the other
    /// control characters (<c>\001</c>, <c>\177</c>), and the rest as they are.
    /// </summary>
    internal static class LispPrinter
    {
        /// <summary>Writes a value.</summary>
        /// <param name="value">The value; nil is null.</param>
        /// <param name="quoted">True for <c>prin1</c> form, false for <c>princ</c> form, which writes strings as they are.</param>
        /// <returns>The text.</returns>
        internal static string Print(object value, bool quoted) => Print(value, quoted, display: false);

        /// <summary>
        /// Writes a value for the command line: as <see cref="Print(object, bool)"/>, but each <c>\U+0041</c> in a string is
        /// shown as the character, as AutoCAD's console does. The string itself keeps its seven characters.
        /// </summary>
        /// <param name="value">The value; nil is null.</param>
        /// <param name="quoted">True for <c>prin1</c> form, false for <c>princ</c> form.</param>
        /// <param name="display">True to show <c>\U+XXXX</c> as the character.</param>
        /// <returns>The text.</returns>
        internal static string Print(object value, bool quoted, bool display)
        {
            var builder = new StringBuilder();
            Append(builder, value, quoted, display);
            return builder.ToString();
        }

        private static readonly Regex UnicodeEscape = new Regex(
            @"\\[Uu]\+([0-9A-Fa-f]{4})",
            RegexOptions.CultureInvariant
        );

        /// <summary>Shows each <c>\U+XXXX</c> of a text as the character, as AutoCAD's console does.</summary>
        /// <param name="text">The text.</param>
        /// <returns>The text with the characters.</returns>
        internal static string Display(string text) =>
            text.IndexOf('\\') < 0
                ? text
                : UnicodeEscape.Replace(text, m =>
                    (
                        (char)int.Parse(m.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture)
                    ).ToString()
                );

        /// <summary>Writes a real as AutoCAD does.</summary>
        /// <param name="value">The real.</param>
        /// <returns>The text: <c>1.0</c>, <c>-nan(ind).0</c>, <c>inf.0</c>.</returns>
        internal static string FormatReal(double value)
        {
            if (double.IsNaN(value))
            {
                return "-nan(ind).0";
            }

            if (double.IsInfinity(value))
            {
                return value > 0 ? "inf.0" : "-inf.0";
            }

            if (value == 0.0)
            {
                return "0.0";
            }

            // Six significant digits, and the exponent of the number once it is rounded to them: what %g decides by.
            string e = Math.Abs(value).ToString("E5", CultureInfo.InvariantCulture);
            int mark = e.IndexOf('E');
            string digits = e.Substring(0, mark).Replace(".", "");
            int exponent = int.Parse(
                e.Substring(mark + 1),
                NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture
            );
            var builder = new StringBuilder();

            if (value < 0)
            {
                builder.Append('-');
            }

            if (exponent >= -4 && exponent < 6)
            {
                string whole;
                string fraction;

                if (exponent >= 0)
                {
                    whole = digits.Substring(0, exponent + 1);
                    fraction = digits.Substring(exponent + 1);
                }
                else
                {
                    whole = "0";
                    fraction = new string('0', -exponent - 1) + digits;
                }

                fraction = fraction.TrimEnd('0');
                builder.Append(whole).Append('.').Append(fraction.Length == 0 ? "0" : fraction);
            }
            else
            {
                string fraction = digits.Substring(1).TrimEnd('0');
                builder.Append(digits[0]).Append('.').Append(fraction.Length == 0 ? "0" : fraction);
                builder.Append('e').Append(exponent < 0 ? '-' : '+');
                builder.Append(Math.Abs(exponent).ToString("00", CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }

        private static void Append(StringBuilder builder, object value, bool quoted, bool display)
        {
            switch (value)
            {
                case null:
                    builder.Append("nil");
                    break;
                case int i:
                    builder.Append(i.ToString(CultureInfo.InvariantCulture));
                    break;
                case double d:
                    builder.Append(FormatReal(d));
                    break;
                case string text:
                    string s = display ? Display(text) : text;

                    if (quoted)
                    {
                        AppendQuoted(builder, s);
                    }
                    else
                    {
                        builder.Append(s);
                    }

                    break;
                case LispSymbol symbol:
                    builder.Append(symbol.Name);
                    break;
                case LispCons cons:
                    AppendList(builder, cons, quoted, display);
                    break;
                case LispFunction function:
                    builder
                        .Append("#<SUBR @")
                        .Append(function.Address.ToString("x16", CultureInfo.InvariantCulture))
                        .Append(' ')
                        .Append(function.Name)
                        .Append('>');
                    break;
                case LispEntityName entity:
                    builder.Append("<Entity name: ").Append(entity.Handle).Append('>');
                    break;
                case LispSelectionSet set:
                    builder
                        .Append("<Selection set: ")
                        .Append(set.Number.ToString(CultureInfo.InvariantCulture))
                        .Append('>');
                    break;
                case LispCatchAllError _:
                    builder.Append("#<%catch-all-apply-error%>");
                    break;
                default:
                    builder.Append(Convert.ToString(value, CultureInfo.InvariantCulture));
                    break;
            }
        }

        private static void AppendList(StringBuilder builder, LispCons list, bool quoted, bool display)
        {
            try
            {
                RuntimeHelpers.EnsureSufficientExecutionStack();
            }
            catch (InsufficientExecutionStackException)
            {
                throw new LispException("internal stack limit reached (simulated)", isHard: true);
            }

            builder.Append('(');
            object cell = list;
            bool first = true;

            while (cell is LispCons cons)
            {
                if (!first)
                {
                    builder.Append(' ');
                }

                first = false;
                Append(builder, cons.Car, quoted, display);
                cell = cons.Cdr;
            }

            if (cell != null)
            {
                builder.Append(" . ");
                Append(builder, cell, quoted, display);
            }

            builder.Append(')');
        }

        private static void AppendQuoted(StringBuilder builder, string text)
        {
            builder.Append('"');

            foreach (char c in text)
            {
                switch (c)
                {
                    case '\\':
                        builder.Append("\\\\");
                        break;
                    case '"':
                        builder.Append("\\\"");
                        break;
                    case '\n':
                        builder.Append("\\n");
                        break;
                    case '\r':
                        builder.Append("\\r");
                        break;
                    case '\t':
                        builder.Append("\\t");
                        break;
                    case '\u001b':
                        builder.Append("\\e");
                        break;
                    default:
                        if (c < ' ' || c == '\u007f')
                        {
                            builder.Append('\\').Append(Convert.ToString((int)c, 8).PadLeft(3, '0'));
                        }
                        else
                        {
                            builder.Append(c);
                        }

                        break;
                }
            }

            builder.Append('"');
        }
    }
}
