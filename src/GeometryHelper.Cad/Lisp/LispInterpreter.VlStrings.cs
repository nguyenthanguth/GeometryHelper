using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace GeometryHelper.Cad.Lisp
{
    public sealed partial class LispInterpreter
    {
        private void RegisterVlStrings()
        {
            Define("VL-STRING-SEARCH", 2, 3, a => VlStringSearch(a));
            Define("VL-STRING-SUBST", 3, 4, a => VlStringSubst(a));
            Define("VL-STRING-POSITION", 2, 4, a => VlStringPosition(a));
            Define("VL-STRING-MISMATCH", 2, 5, a => VlStringMismatch(a));
            Define("VL-STRING-ELT", 2, 2, a => VlStringElt(a[0], a[1]));
            Define("VL-STRING-TRANSLATE", 3, 3, a => VlStringTranslate(Name(a[0]), Name(a[1]), Name(a[2])));
            Define("VL-STRING-TRIM", 2, 2, a => VlStringTrim(Name(a[0]), Name(a[1]), left: true, right: true));
            Define("VL-STRING-LEFT-TRIM", 2, 2, a => VlStringTrim(Name(a[0]), Name(a[1]), left: true, right: false));
            Define("VL-STRING-RIGHT-TRIM", 2, 2, a => VlStringTrim(Name(a[0]), Name(a[1]), left: false, right: true));
            Define("VL-STRING->LIST", 1, 1, a => VlStringToList(Str(a[0])));
            Define("VL-LIST->STRING", 1, 1, a => VlListToString(a[0]));
        }

        /// <summary>Takes a string argument of a <c>vl-string</c> function, where a symbol stands for its name.</summary>
        private static string Name(object value) =>
            value as string
            ?? (value as LispSymbol)?.Name
            ?? throw TypeError("(or stringp symbolp)", value, colon: true);

        /// <summary>
        /// Takes a position that may be left out or nil (then <paramref name="fallback"/>): from 0 to the length of the
        /// text, where a negative one is <c>non-negative</c> and one past the end is <c>string position out of range</c>.
        /// </summary>
        private static int Position(object value, int length, int fallback)
        {
            if (value == null)
            {
                return fallback;
            }

            int position = Int(value);

            if (position < 0)
            {
                throw new LispException("bad argument value: non-negative: " + Inv(position));
            }

            if (position > length)
            {
                throw new LispException("bad argument value: string position out of range " + Inv(position));
            }

            return position;
        }

        /// <summary>
        /// Where a pattern starts in a text at or after a position, by case; the empty pattern is found at the end of the
        /// text, as AutoCAD finds it, and nothing is found from the end itself.
        /// </summary>
        private static int Find(string pattern, string text, int start)
        {
            if (start >= text.Length)
            {
                return -1;
            }

            return pattern.Length == 0 ? text.Length : text.IndexOf(pattern, start, StringComparison.Ordinal);
        }

        private static object VlStringSearch(object[] a)
        {
            string pattern = Name(a[0]);
            string text = Name(a[1]);
            int start = Position(a.Length > 2 ? a[2] : null, text.Length, 0);
            int found = Find(pattern, text, start);
            return found < 0 ? null : (object)found;
        }

        /// <summary><c>(vl-string-subst new old text [start])</c>: the first <c>old</c> at or after start becomes <c>new</c>; the text itself when it is not there.</summary>
        private static object VlStringSubst(object[] a)
        {
            string replacement = Name(a[0]);
            string old = Name(a[1]);
            string text = Name(a[2]);
            int start = Position(a.Length > 3 ? a[3] : null, text.Length, 0);
            int found = Find(old, text, start);

            return found < 0 ? text : text.Substring(0, found) + replacement + text.Substring(found + old.Length);
        }

        /// <summary>
        /// <c>(vl-string-position code text [start [from-end]])</c>: the first place at or after start with the character of
        /// that code, or with from-end not nil the last one; nil when there is none.
        /// </summary>
        private static object VlStringPosition(object[] a)
        {
            int code = Int(a[0]);
            string text = Name(a[1]);
            int start = Position(a.Length > 2 ? a[2] : null, text.Length, 0);

            if (code < 0 || code > char.MaxValue)
            {
                return null;
            }

            bool fromEnd = a.Length > 3 && a[3] != null;
            int found = fromEnd ? text.LastIndexOf((char)code) : text.IndexOf((char)code, start);
            return found < start ? null : (object)found;
        }

        /// <summary>
        /// <c>(vl-string-mismatch text1 text2 [start1 [start2 [ignore-case]]])</c>: how many characters the two texts share,
        /// read from their starts.
        /// </summary>
        private static object VlStringMismatch(object[] a)
        {
            string first = Name(a[0]);
            string second = Name(a[1]);
            int start1 = Position(a.Length > 2 ? a[2] : null, first.Length, 0);
            int start2 = Position(a.Length > 3 ? a[3] : null, second.Length, 0);
            bool ignoreCase = a.Length > 4 && a[4] != null;
            int same = 0;

            while (start1 + same < first.Length && start2 + same < second.Length)
            {
                char x = first[start1 + same];
                char y = second[start2 + same];

                if (x != y && !(ignoreCase && char.ToUpperInvariant(x) == char.ToUpperInvariant(y)))
                {
                    break;
                }

                same++;
            }

            return same;
        }

        private static object VlStringElt(object text, object index)
        {
            string value = Name(text);
            int position = Int(index);

            if (position < 0)
            {
                throw new LispException("bad argument value: non-negative: " + Inv(position));
            }

            if (position >= value.Length)
            {
                throw new LispException("bad argument value: string position out of range " + Inv(position));
            }

            return (int)value[position];
        }

        /// <summary>
        /// <c>(vl-string-translate source target text)</c>: each character of the text that is the i-th of source becomes the
        /// i-th of target; one with no i-th in target stays as it is.
        /// </summary>
        private static object VlStringTranslate(string source, string target, string text)
        {
            var result = new StringBuilder(text.Length);

            foreach (char c in text)
            {
                int index = source.IndexOf(c);
                result.Append(index >= 0 && index < target.Length ? target[index] : c);
            }

            return result.ToString();
        }

        /// <summary>The trims: the characters of the set are taken off the start, the end, or both, of the text.</summary>
        private static object VlStringTrim(string set, string text, bool left, bool right)
        {
            int start = 0;
            int end = text.Length;

            while (left && start < end && set.IndexOf(text[start]) >= 0)
            {
                start++;
            }

            while (right && end > start && set.IndexOf(text[end - 1]) >= 0)
            {
                end--;
            }

            return text.Substring(start, end - start);
        }

        /// <summary>
        /// <c>(vl-string->list text)</c>: the character codes, where <c>\U+0111</c> (four hex digits) is one code, 273. This
        /// is the only function that reads the escape as a character; <c>strlen</c> and the others count its seven.
        /// </summary>
        private static object VlStringToList(string text)
        {
            var codes = new List<object>(text.Length);

            for (int i = 0; i < text.Length; i++)
            {
                if (
                    text[i] == '\\'
                    && i + 6 < text.Length
                    && (text[i + 1] == 'U' || text[i + 1] == 'u')
                    && text[i + 2] == '+'
                    && int.TryParse(
                        text.Substring(i + 3, 4),
                        NumberStyles.AllowHexSpecifier,
                        CultureInfo.InvariantCulture,
                        out int code
                    )
                )
                {
                    codes.Add(code);
                    i += 6;
                }
                else
                {
                    codes.Add((int)text[i]);
                }
            }

            return ListOf(codes);
        }

        /// <summary>
        /// <c>(vl-list->string codes)</c>: a string of the characters with those codes, 1 to 65535. Code 0 and a code outside
        /// that range are left out, which is ours: AutoCAD gave garbage for them.
        /// </summary>
        private static object VlListToString(object list)
        {
            var result = new StringBuilder();

            foreach (object item in ListArgument(list))
            {
                if (!(item is int code))
                {
                    throw new LispException("bad char code in list: " + Print(item, true));
                }

                if (code >= 1 && code <= char.MaxValue)
                {
                    result.Append((char)code);
                }
            }

            return result.ToString();
        }
    }
}
