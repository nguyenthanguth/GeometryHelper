using System;
using System.Globalization;
using System.Text;

namespace GeometryHelper.Cad.Lisp
{
    public sealed partial class LispInterpreter
    {
        private void RegisterStrings()
        {
            Define("STRCAT", 0, -1, Strcat);
            Define("SUBSTR", 2, 3, Substr);
            Define("STRLEN", 0, -1, Strlen);
            Define("STRCASE", 1, 2, a => Strcase(Str(a[0]), a.Length > 1 && a[1] != null));
            Define("ASCII", 1, 1, a => Ascii(Str(a[0])));
            Define("CHR", 1, 1, a => Chr(Int(a[0])));
            Define("ATOI", 1, 1, a => Atoi(Str(a[0])));
            Define("ATOF", 1, 1, a => Atof(Str(a[0])));
            Define("ITOA", 1, 1, a => Int(a[0]).ToString(CultureInfo.InvariantCulture));
            Define("VL-SYMBOL-NAME", 1, 1, a => a[0] is LispSymbol s ? s.Name
                : a[0] == null ? "NIL"
                : throw TypeError("symbolp", a[0]));
            Define("VL-PRINC-TO-STRING", 1, 1, a => Print(a[0], false));
            Define("VL-PRIN1-TO-STRING", 1, 1, a => Print(a[0], true));
            Define("WCMATCH", 2, 2, a => Bool(WildcardMatch(Str(a[0]), Str(a[1]))));
            RegisterVlLists();
            RegisterVlStrings();
        }

        /// <summary>
        /// <c>(wcmatch text pattern)</c>: T when the text matches a pattern, which may be several separated by commas, nil when
        /// it does not. <c>#</c> is one digit, <c>@</c> one letter, <c>.</c> one character that is neither, <c>?</c> any one
        /// character, <c>*</c> any run of characters, <c>[abc]</c> and <c>[a-z]</c> one of those, <c>[~abc]</c> one that is not,
        /// a <c>~</c> at the start of a pattern means anything but it, and a reverse quote makes the next character itself.
        /// The comparison is case-sensitive.
        /// </summary>
        private static bool WildcardMatch(string text, string patterns)
        {
            var current = new StringBuilder();
            bool inSet = false;

            for (int i = 0; i <= patterns.Length; i++)
            {
                bool end = i == patterns.Length;
                char c = end ? ',' : patterns[i];

                if (!end && c == '`' && i + 1 < patterns.Length)
                {
                    current.Append(c).Append(patterns[++i]);
                    continue;
                }

                if (!end && c == '[')
                {
                    inSet = true;
                }
                else if (!end && c == ']')
                {
                    inSet = false;
                }

                if (c == ',' && !inSet)
                {
                    string pattern = current.ToString();
                    current.Clear();
                    bool negated = pattern.StartsWith("~", StringComparison.Ordinal);
                    bool matched = WildcardAt(text, 0, negated ? pattern.Substring(1) : pattern, 0);

                    if (matched != negated)
                    {
                        return true;
                    }
                }
                else
                {
                    current.Append(c);
                }
            }

            return false;
        }

        private static bool WildcardAt(string text, int t, string pattern, int p)
        {
            while (p < pattern.Length)
            {
                char c = pattern[p];

                if (c == '*')
                {
                    while (p < pattern.Length && pattern[p] == '*')
                    {
                        p++;
                    }

                    if (p == pattern.Length)
                    {
                        return true;
                    }

                    for (int k = t; k <= text.Length; k++)
                    {
                        if (WildcardAt(text, k, pattern, p))
                        {
                            return true;
                        }
                    }

                    return false;
                }

                if (t >= text.Length)
                {
                    return false;
                }

                char ch = text[t];

                switch (c)
                {
                    case '?':
                        break;
                    case '#':
                        if (ch < '0' || ch > '9')
                        {
                            return false;
                        }

                        break;
                    case '@':
                        if (!char.IsLetter(ch))
                        {
                            return false;
                        }

                        break;
                    case '.':
                        if (char.IsLetterOrDigit(ch))
                        {
                            return false;
                        }

                        break;
                    case '`':
                        if (p + 1 < pattern.Length)
                        {
                            p++;
                        }

                        if (ch != pattern[p])
                        {
                            return false;
                        }

                        break;
                    case '[':
                        int close = pattern.IndexOf(']', p + 1);

                        if (close < 0)
                        {
                            if (ch != c)
                            {
                                return false;
                            }
                        }
                        else
                        {
                            if (!InSet(ch, pattern.Substring(p + 1, close - p - 1)))
                            {
                                return false;
                            }

                            p = close;
                        }

                        break;
                    default:
                        if (ch != c)
                        {
                            return false;
                        }

                        break;
                }

                p++;
                t++;
            }

            return t == text.Length;
        }

        private static bool InSet(char ch, string set)
        {
            bool negated = set.StartsWith("~", StringComparison.Ordinal);
            int i = negated ? 1 : 0;
            bool found = false;

            while (i < set.Length)
            {
                if (i + 2 < set.Length && set[i + 1] == '-')
                {
                    found |= ch >= set[i] && ch <= set[i + 2];
                    i += 3;
                }
                else
                {
                    found |= ch == set[i];
                    i++;
                }
            }

            return found != negated;
        }

        private static object Strcat(object[] arguments)
        {
            var builder = new StringBuilder();

            foreach (object argument in arguments)
            {
                builder.Append(Str(argument));
            }

            return builder.ToString();
        }

        /// <summary>
        /// <c>(substr text start [length])</c>, the start counted from 1. A start below 1 or a negative length is an error; a
        /// start past the end, or a length past it, is cut to what is there.
        /// </summary>
        private static object Substr(object[] a)
        {
            string text = Str(a[0]);
            int start = Int(a[1]);

            if (start < 1)
            {
                throw new LispException("bad argument value: positive " + Inv(start));
            }

            int length = text.Length;

            if (a.Length > 2)
            {
                length = Int(a[2]);

                if (length < 0)
                {
                    throw new LispException("bad argument value: non-negative: " + Inv(length));
                }
            }

            if (start > text.Length)
            {
                return "";
            }

            return text.Substring(start - 1, Math.Min(length, text.Length - start + 1));
        }

        /// <summary>The length of its arguments added together, as AutoCAD does: <c>(strlen "abc" "de")</c> is 5.</summary>
        private static object Strlen(object[] arguments)
        {
            int total = 0;

            foreach (object argument in arguments)
            {
                total += Str(argument).Length;
            }

            return total;
        }

        private static object Strcase(string text, bool lower) =>
            lower ? text.ToLowerInvariant() : text.ToUpperInvariant();

        private static object Ascii(string text) => text.Length == 0 ? 0 : (int)text[0];

        /// <summary>The character with a code from 1 to 65535 (a .NET character, as strings are); any other code gives the empty string.</summary>
        private static object Chr(int code) => code >= 1 && code <= char.MaxValue ? ((char)code).ToString() : "";

        /// <summary>
        /// <c>atoi</c> as C's does: leading spaces, a sign, and the digits up to the first other character. A number past 32 bits
        /// stops at the largest, 2147483647, and -2147483647.
        /// </summary>
        private static object Atoi(string text)
        {
            int i = 0;

            while (i < text.Length && char.IsWhiteSpace(text[i]))
            {
                i++;
            }

            bool negative = false;

            if (i < text.Length && (text[i] == '+' || text[i] == '-'))
            {
                negative = text[i] == '-';
                i++;
            }

            long value = 0;

            while (i < text.Length && text[i] >= '0' && text[i] <= '9')
            {
                value = Math.Min(value * 10 + (text[i] - '0'), 2147483647L);
                i++;
            }

            return (int)(negative ? -value : value);
        }

        /// <summary>
        /// <c>atof</c> as C's does: leading spaces, a sign, digits, a point and digits, an exponent when digits follow it; the
        /// text after that is ignored, and a number too large for a double is 0.0.
        /// </summary>
        private static object Atof(string text)
        {
            int i = 0;

            while (i < text.Length && char.IsWhiteSpace(text[i]))
            {
                i++;
            }

            int start = i;

            if (i < text.Length && (text[i] == '+' || text[i] == '-'))
            {
                i++;
            }

            int digits = 0;

            while (i < text.Length && text[i] >= '0' && text[i] <= '9')
            {
                i++;
                digits++;
            }

            if (i < text.Length && text[i] == '.')
            {
                i++;

                while (i < text.Length && text[i] >= '0' && text[i] <= '9')
                {
                    i++;
                    digits++;
                }
            }

            if (digits == 0)
            {
                return 0.0;
            }

            if (i < text.Length && (text[i] == 'e' || text[i] == 'E'))
            {
                int j = i + 1;

                if (j < text.Length && (text[j] == '+' || text[j] == '-'))
                {
                    j++;
                }

                int exponentStart = j;

                while (j < text.Length && text[j] >= '0' && text[j] <= '9')
                {
                    j++;
                }

                if (j > exponentStart)
                {
                    i = j;
                }
            }

            return LispReader.ParseReal(text.Substring(start, i - start));
        }
    }
}
